using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared spatial path for normal forward snake movement.
///
/// The physical trailing probe writes distance-based path samples.
/// Active followers never consume samples; they query poses at logical progress
/// values behind HeadProgress. Pending followers reserve enough history before
/// they are promoted so long chains cannot collapse onto the oldest sample.
///
/// After MoveBackward, the recovered Probe -> C1 -> ... -> Tail geometry can be
/// reseeded as a fresh normal path without snapping any follower.
/// </summary>
[DisallowMultipleComponent]
public class SnakePathHistory : MonoBehaviour
{
    #region Data Types

    [System.Serializable]
    public struct PathPoint
    {
        public Vector3 position;
        public float distance;

        public PathPoint(Vector3 position, float distance)
        {
            this.position = position;
            this.distance = distance;
        }
    }

    private struct SeedAnchor
    {
        public Vector3 position;
        public float distance;

        public SeedAnchor(Vector3 position, float distance)
        {
            this.position = position;
            this.distance = distance;
        }
    }

    #endregion

    #region Recording Settings

    [Header("Recording")]
    [Min(0.001f)]
    [SerializeField] private float sampleSpacing = 0.1f;

    [Tooltip("Minimum probe displacement accepted as real path movement.")]
    [Min(0f)]
    [SerializeField] private float movementAcceptanceDistance = 0.01f;

    [Tooltip("Ignore suspension/bounce when measuring path distance.")]
    [SerializeField] private bool ignoreVerticalMotion = true;

    #endregion

    #region History Settings

    [Header("History")]
    [Min(0f)]
    [SerializeField] private float initialBackfillDistance = 20f;

    [Min(1f)]
    [SerializeField] private float maxHistoryDistance = 80f;

    [Min(1)]
    [SerializeField] private int pruneBatchSize = 32;

    [Header("Follower Rotation")]
    [Min(0.01f)]
    [SerializeField] private float tangentProbeDistance = 0.35f;

    #endregion

    #region Runtime

    private Transform pathSource;
    private Rigidbody leaderBody;

    private readonly List<PathPoint> samples = new List<PathPoint>(512);

    // Reused only during MoveBackward recovery so repeated recoveries do not
    // allocate temporary anchor lists every time.
    private readonly List<SeedAnchor> recoveryFollowerAnchors = new List<SeedAnchor>(32);
    private readonly List<SeedAnchor> recoveryAnchors = new List<SeedAnchor>(34);
    private readonly List<PathPoint> backwardExtensionBuffer = new List<PathPoint>(128);

    private Vector3 lastObservedPosition;
    private Vector3 livePathEndPosition;

    #endregion

    #region Runtime Debug

    [Header("Runtime Debug - Read Only")]
    [SerializeField] private bool isInitialized;
    [SerializeField] private bool acceptedMovementThisTick;
    [SerializeField] private float currentPlanarSpeed;
    [SerializeField] private float headProgress;
    [SerializeField] private float recordedEndProgress;
    [SerializeField] private float distanceSinceLastSample;
    [SerializeField] private int currentSampleCount;

    [Header("Dynamic Chain Coverage - Read Only")]
    [SerializeField] private float requiredHistoryDistance;
    [SerializeField] private float availableHistoryDistance;
    [SerializeField] private int backwardExtensionCount;

    #endregion

    #region Debug Drawing

    [Header("Debug Drawing")]
    [SerializeField] private bool drawPath = true;
    [SerializeField] private bool drawStoredSamples = true;

    [Min(1)]
    [SerializeField] private int drawEveryNthSample = 5;

    [Min(0.001f)]
    [SerializeField] private float sampleGizmoRadius = 0.04f;

    #endregion

    #region Public State

    public bool IsInitialized => isInitialized;
    public float HeadProgress => headProgress;
    public float RecordedEndProgress => recordedEndProgress;
    public float OldestProgress => samples.Count > 0 ? samples[0].distance : headProgress;
    public int SampleCount => samples.Count;
    public IReadOnlyList<PathPoint> Samples => samples;
    public float RequiredHistoryDistance => requiredHistoryDistance;
    public float AvailableHistoryDistance => availableHistoryDistance;

    #endregion

    #region Initialization

    public void SetLeaderBody(Rigidbody body)
    {
        leaderBody = body;
    }

    public void Initialize(Transform newPathSource)
    {
        if (newPathSource == null)
        {
            Debug.LogError("[SnakePathHistory] Cannot initialize because Path Source is null.", this);
            return;
        }

        pathSource = newPathSource;
        ResetHistoryToSource();
    }

    public void ResetHistoryToSource()
    {
        if (pathSource == null) return;

        samples.Clear();

        Vector3 sourcePosition = pathSource.position;
        Vector3 forward = Vector3.ProjectOnPlane(pathSource.forward, Vector3.up);

        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        if (initialBackfillDistance > 0f)
        {
            int backfillCount = Mathf.CeilToInt(initialBackfillDistance / sampleSpacing);

            for (int i = backfillCount; i >= 1; i--)
            {
                float distance = -i * sampleSpacing;
                samples.Add(new PathPoint(sourcePosition + forward * distance, distance));
            }
        }

        samples.Add(new PathPoint(sourcePosition, 0f));

        ResetRuntimeTracking(sourcePosition);
        isInitialized = true;

        EnsureRequiredHistoryCoverage();
    }

    #endregion

    #region MoveBackward Recovery Seeding

    /// <summary>
    /// Rebuilds normal path history from the exact current recovered chain pose.
    ///
    /// Follower world positions become anchors at their normal logical spacing
    /// values. Geometric spacing may be compressed, but logical progress remains
    /// correct and normal forward movement stretches the chain naturally.
    /// </summary>
    public bool ResetHistoryFromCurrentChain(
        Transform newPathSource,
        IReadOnlyList<Vector3> followerPositions,
        float firstFollowerSpacing,
        float followerSpacing)
    {
        if (newPathSource == null)
        {
            Debug.LogError("[SnakePathHistory] Recovery Path Source is null.", this);
            return false;
        }

        if (followerPositions == null || followerPositions.Count == 0)
        {
            Debug.LogError("[SnakePathHistory] Recovery requires at least C1.", this);
            return false;
        }

        pathSource = newPathSource;

        float safeFirstSpacing = Mathf.Max(0.01f, firstFollowerSpacing);
        float safeFollowerSpacing = Mathf.Max(0.01f, followerSpacing);

        Vector3 sourcePosition = pathSource.position;
        float seedY = sourcePosition.y;

        recoveryFollowerAnchors.Clear();
        recoveryAnchors.Clear();

        for (int i = 0; i < followerPositions.Count; i++)
        {
            Vector3 position = followerPositions[i];
            position.y = seedY;

            float progress = -(safeFirstSpacing + i * safeFollowerSpacing);
            recoveryFollowerAnchors.Add(new SeedAnchor(position, progress));
        }

        SeedAnchor tail = recoveryFollowerAnchors[recoveryFollowerAnchors.Count - 1];
        Vector3 backwardDirection = GetRecoveryBackfillDirection(sourcePosition, tail);

        if (initialBackfillDistance > 0.01f)
        {
            recoveryAnchors.Add(
                new SeedAnchor(
                    tail.position + backwardDirection * initialBackfillDistance,
                    tail.distance - initialBackfillDistance
                )
            );
        }

        // Samples are stored oldest progress -> newest progress.
        for (int i = recoveryFollowerAnchors.Count - 1; i >= 0; i--)
        {
            recoveryAnchors.Add(recoveryFollowerAnchors[i]);
        }

        recoveryAnchors.Add(new SeedAnchor(sourcePosition, 0f));

        samples.Clear();

        AddSeedAnchor(recoveryAnchors[0]);

        for (int i = 1; i < recoveryAnchors.Count; i++)
        {
            AddSeedSegment(recoveryAnchors[i - 1], recoveryAnchors[i]);
        }

        ResetRuntimeTracking(sourcePosition);
        isInitialized = true;

        EnsureRequiredHistoryCoverage();
        return true;
    }

    private Vector3 GetRecoveryBackfillDirection(Vector3 sourcePosition, SeedAnchor tail)
    {
        Vector3 backwardDirection;

        if (recoveryFollowerAnchors.Count >= 2)
        {
            Vector3 previous = recoveryFollowerAnchors[recoveryFollowerAnchors.Count - 2].position;
            backwardDirection = Vector3.ProjectOnPlane(tail.position - previous, Vector3.up);
        }
        else
        {
            backwardDirection = Vector3.ProjectOnPlane(tail.position - sourcePosition, Vector3.up);
        }

        if (backwardDirection.sqrMagnitude >= 0.0001f)
        {
            return backwardDirection.normalized;
        }

        Vector3 sourceForward = Vector3.ProjectOnPlane(pathSource.forward, Vector3.up);

        return sourceForward.sqrMagnitude > 0.0001f
            ? -sourceForward.normalized
            : -Vector3.forward;
    }

    private void AddSeedAnchor(SeedAnchor anchor)
    {
        if (samples.Count > 0 &&
            Mathf.Abs(samples[samples.Count - 1].distance - anchor.distance) < 0.0001f)
        {
            samples[samples.Count - 1] = new PathPoint(anchor.position, anchor.distance);
            return;
        }

        samples.Add(new PathPoint(anchor.position, anchor.distance));
    }

    private void AddSeedSegment(SeedAnchor from, SeedAnchor to)
    {
        float progressSpan = to.distance - from.distance;

        if (progressSpan <= 0.00001f)
        {
            AddSeedAnchor(to);
            return;
        }

        int steps = Mathf.Max(1, Mathf.CeilToInt(progressSpan / sampleSpacing));

        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;

            AddSeedAnchor(
                new SeedAnchor(
                    Vector3.Lerp(from.position, to.position, t),
                    Mathf.Lerp(from.distance, to.distance, t)
                )
            );
        }
    }

    private void ResetRuntimeTracking(Vector3 sourcePosition)
    {
        lastObservedPosition = sourcePosition;
        livePathEndPosition = sourcePosition;

        distanceSinceLastSample = 0f;
        recordedEndProgress = 0f;
        headProgress = 0f;

        acceptedMovementThisTick = false;
        currentPlanarSpeed = 0f;
        currentSampleCount = samples.Count;
        availableHistoryDistance = samples.Count > 0 ? Mathf.Max(0f, headProgress - samples[0].distance) : 0f;
    }

    #endregion

    #region Dynamic Chain Coverage

    /// <summary>
    /// Called by SnakeCartManager with the logical path distance needed to
    /// support the current chain and queued followers.
    ///
    /// maxHistoryDistance remains the normal authored history budget, but the
    /// chain requirement always wins so a long chain can never be pruned below
    /// its own required tail distance.
    /// </summary>
    public void SetRequiredHistoryDistance(float distance)
    {
        requiredHistoryDistance = Mathf.Max(0f, distance);

        if (isInitialized) EnsureRequiredHistoryCoverage();
    }

    /// <summary>
    /// If the chain becomes longer than the history currently available
    /// (for example, many carts are collected before the leader has driven far
    /// enough), extend the OLDEST end of the history backward along its oldest
    /// tangent.
    ///
    /// Real recorded history is always preferred. This straight extension is
    /// only filling path space that never existed, and is much safer than
    /// clamping every extra follower onto the same oldest point.
    /// </summary>
    private void EnsureRequiredHistoryCoverage()
    {
        if (!isInitialized || samples.Count == 0) return;

        availableHistoryDistance = Mathf.Max(0f, headProgress - samples[0].distance);

        if (availableHistoryDistance + 0.0001f >= requiredHistoryDistance) return;

        float missingDistance = requiredHistoryDistance - availableHistoryDistance;
        int extensionSteps = Mathf.Max(1, Mathf.CeilToInt(missingDistance / sampleSpacing));

        PathPoint oldest = samples[0];
        Vector3 oldestForward = GetOldestForwardTangent();

        backwardExtensionBuffer.Clear();

        // Build oldest -> newest so InsertRange preserves ascending progress.
        for (int i = extensionSteps; i >= 1; i--)
        {
            float extensionDistance = i * sampleSpacing;

            backwardExtensionBuffer.Add(
                new PathPoint(
                    oldest.position - oldestForward * extensionDistance,
                    oldest.distance - extensionDistance
                )
            );
        }

        samples.InsertRange(0, backwardExtensionBuffer);

        backwardExtensionCount += extensionSteps;
        availableHistoryDistance = Mathf.Max(0f, headProgress - samples[0].distance);
        currentSampleCount = samples.Count;
    }

    private Vector3 GetOldestForwardTangent()
    {
        if (samples.Count >= 2)
        {
            Vector3 tangent = samples[1].position - samples[0].position;

            if (ignoreVerticalMotion) tangent = Vector3.ProjectOnPlane(tangent, Vector3.up);
            if (tangent.sqrMagnitude > 0.0001f) return tangent.normalized;
        }

        if (pathSource != null)
        {
            Vector3 forward = Vector3.ProjectOnPlane(pathSource.forward, Vector3.up);
            if (forward.sqrMagnitude > 0.0001f) return forward.normalized;
        }

        if (leaderBody != null)
        {
            Vector3 forward = Vector3.ProjectOnPlane(leaderBody.transform.forward, Vector3.up);
            if (forward.sqrMagnitude > 0.0001f) return forward.normalized;
        }

        return Vector3.forward;
    }

    #endregion

    #region Recording

    public void TickHistory()
    {
        if (!isInitialized || pathSource == null) return;

        RecordPathSourceMovement(pathSource.position);
    }

    private void RecordPathSourceMovement(Vector3 currentSourcePosition)
    {
        acceptedMovementThisTick = false;

        Vector3 frameDelta = currentSourcePosition - lastObservedPosition;

        if (ignoreVerticalMotion)
        {
            frameDelta = Vector3.ProjectOnPlane(frameDelta, Vector3.up);
        }

        float frameDisplacement = frameDelta.magnitude;

        currentPlanarSpeed = frameDisplacement / Mathf.Max(Time.fixedDeltaTime, 0.00001f);
        lastObservedPosition = currentSourcePosition;

        // Reject solver jitter only. Legitimate physical hinge swing remains
        // valid path movement.
        if (frameDisplacement < movementAcceptanceDistance) return;

        Vector3 segmentDelta = currentSourcePosition - livePathEndPosition;

        if (ignoreVerticalMotion)
        {
            segmentDelta = Vector3.ProjectOnPlane(segmentDelta, Vector3.up);
        }

        float segmentLength = segmentDelta.magnitude;

        if (segmentLength <= 0.00001f) return;

        acceptedMovementThisTick = true;

        AppendSegment(livePathEndPosition, currentSourcePosition, segmentLength);

        livePathEndPosition = currentSourcePosition;
        recordedEndProgress = samples[samples.Count - 1].distance + distanceSinceLastSample;
        headProgress = recordedEndProgress;
        currentSampleCount = samples.Count;

        PruneHistory();
        availableHistoryDistance = Mathf.Max(0f, headProgress - samples[0].distance);
    }

    private void AppendSegment(
        Vector3 segmentStartPosition,
        Vector3 segmentEndPosition,
        float segmentLength)
    {
        float remainingLength = segmentLength;
        Vector3 remainingStart = segmentStartPosition;

        while (distanceSinceLastSample + remainingLength >= sampleSpacing)
        {
            float distanceNeeded = sampleSpacing - distanceSinceLastSample;
            float t = remainingLength > 0.00001f ? distanceNeeded / remainingLength : 1f;

            Vector3 newPosition = Vector3.Lerp(
                remainingStart,
                segmentEndPosition,
                Mathf.Clamp01(t)
            );

            PathPoint previous = samples[samples.Count - 1];

            samples.Add(
                new PathPoint(
                    newPosition,
                    previous.distance + sampleSpacing
                )
            );

            remainingStart = newPosition;
            remainingLength -= distanceNeeded;
            distanceSinceLastSample = 0f;
        }

        distanceSinceLastSample += Mathf.Max(0f, remainingLength);
    }

    #endregion

    #region Path Queries

    public bool TryGetPoseAtProgress(
        float targetProgress,
        out Vector3 position,
        out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (!TryGetPositionAtProgress(targetProgress, out position)) return false;

        float newest = recordedEndProgress;

        // Allow progress older than the oldest real sample. Position queries
        // extrapolate that missing section backward instead of clamping every
        // long-chain follower onto one point.
        targetProgress = Mathf.Min(targetProgress, newest);

        float beforeProgress = targetProgress - tangentProbeDistance;
        float afterProgress = Mathf.Min(newest, targetProgress + tangentProbeDistance);

        if (!TryGetPositionAtProgress(beforeProgress, out Vector3 beforePosition)) return false;
        if (!TryGetPositionAtProgress(afterProgress, out Vector3 afterPosition)) return false;

        Vector3 tangent = Vector3.ProjectOnPlane(
            afterPosition - beforePosition,
            Vector3.up
        );

        if (tangent.sqrMagnitude < 0.0001f) tangent = GetFallbackTangent();
        if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.forward;

        rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
        return true;
    }

    public bool TryGetPositionAtProgress(float targetProgress, out Vector3 position)
    {
        position = Vector3.zero;

        if (!isInitialized || samples.Count == 0) return false;

        PathPoint oldestStored = samples[0];

        if (targetProgress < oldestStored.distance)
        {
            float distanceBeforeOldest = oldestStored.distance - targetProgress;
            Vector3 oldestForward = GetOldestForwardTangent();

            position = oldestStored.position - oldestForward * distanceBeforeOldest;
            return true;
        }

        targetProgress = Mathf.Min(targetProgress, recordedEndProgress);

        PathPoint newestStored = samples[samples.Count - 1];

        if (targetProgress >= newestStored.distance)
        {
            float liveDistance = recordedEndProgress - newestStored.distance;

            if (liveDistance <= 0.00001f)
            {
                position = newestStored.position;
                return true;
            }

            float t = Mathf.Clamp01(
                (targetProgress - newestStored.distance) / liveDistance
            );

            position = Vector3.Lerp(
                newestStored.position,
                livePathEndPosition,
                t
            );

            return true;
        }

        int low = 0;
        int high = samples.Count - 1;

        while (low < high)
        {
            int mid = (low + high) / 2;

            if (samples[mid].distance < targetProgress)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        int upperIndex = low;

        if (upperIndex == 0)
        {
            position = samples[0].position;
            return true;
        }

        PathPoint lower = samples[upperIndex - 1];
        PathPoint upper = samples[upperIndex];

        float distanceRange = upper.distance - lower.distance;

        float t2 = distanceRange > 0.00001f
            ? (targetProgress - lower.distance) / distanceRange
            : 0f;

        position = Vector3.Lerp(lower.position, upper.position, t2);
        return true;
    }

    private Vector3 GetFallbackTangent()
    {
        if (samples.Count >= 2)
        {
            Vector3 tangent = Vector3.ProjectOnPlane(
                samples[samples.Count - 1].position - samples[samples.Count - 2].position,
                Vector3.up
            );

            if (tangent.sqrMagnitude > 0.0001f) return tangent.normalized;
        }

        if (leaderBody != null)
        {
            Vector3 forward = Vector3.ProjectOnPlane(
                leaderBody.transform.forward,
                Vector3.up
            );

            if (forward.sqrMagnitude > 0.0001f) return forward.normalized;
        }

        if (pathSource != null)
        {
            Vector3 forward = Vector3.ProjectOnPlane(
                pathSource.forward,
                Vector3.up
            );

            if (forward.sqrMagnitude > 0.0001f) return forward.normalized;
        }

        return Vector3.forward;
    }

    #endregion

    #region History Pruning

    private void PruneHistory()
    {
        if (samples.Count <= 2) return;

        float effectiveHistoryDistance = Mathf.Max(maxHistoryDistance, requiredHistoryDistance);
        float keepFrom = headProgress - effectiveHistoryDistance;
        int removeCount = 0;

        while (removeCount < samples.Count - 2 &&
               samples[removeCount + 1].distance < keepFrom)
        {
            removeCount++;
        }

        if (removeCount >= pruneBatchSize)
        {
            samples.RemoveRange(0, removeCount);
        }

        currentSampleCount = samples.Count;
    }

    #endregion

    #region Validation / Debug Drawing

    private void OnValidate()
    {
        sampleSpacing = Mathf.Max(0.001f, sampleSpacing);
        movementAcceptanceDistance = Mathf.Max(0f, movementAcceptanceDistance);
        tangentProbeDistance = Mathf.Max(0.01f, tangentProbeDistance);
        initialBackfillDistance = Mathf.Max(0f, initialBackfillDistance);
        maxHistoryDistance = Mathf.Max(initialBackfillDistance + sampleSpacing * 2f, maxHistoryDistance);
        pruneBatchSize = Mathf.Max(1, pruneBatchSize);
        drawEveryNthSample = Mathf.Max(1, drawEveryNthSample);
        sampleGizmoRadius = Mathf.Max(0.001f, sampleGizmoRadius);
    }

    private void OnDrawGizmos()
    {
        if (!drawPath || !Application.isPlaying || !isInitialized || samples.Count == 0) return;

        Gizmos.color = Color.cyan;

        for (int i = 1; i < samples.Count; i++)
        {
            Gizmos.DrawLine(
                samples[i - 1].position,
                samples[i].position
            );
        }

        Gizmos.DrawLine(
            samples[samples.Count - 1].position,
            livePathEndPosition
        );

        if (drawStoredSamples)
        {
            Gizmos.color = Color.yellow;

            int stride = Mathf.Max(1, drawEveryNthSample);

            for (int i = 0; i < samples.Count; i += stride)
            {
                Gizmos.DrawSphere(
                    samples[i].position,
                    sampleGizmoRadius
                );
            }
        }

        if (TryGetPositionAtProgress(headProgress, out Vector3 activeHeadPosition))
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(activeHeadPosition, sampleGizmoRadius * 2f);
        }
    }

    #endregion
}
