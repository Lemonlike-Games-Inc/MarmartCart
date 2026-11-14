using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the MoveBackward action for the leading cart.
///
/// There are two deliberately separate movement modes:
///
/// 1) Leader only:
///    Drives the leading Rigidbody backward using a short speed curve.
///    The normal physical probe/path may continue recording this real motion.
///
/// 2) Leader + followers:
///    The tail retreats along old normal path history.
///    C1 -> Tail follow a temporary reverse path.
///    C1 then pulls the Leader through a slack reverse-tow constraint.
///    SnakeCartManager converts the final geometry into a fresh normal path.
///
/// During either mode:
/// - normal player driving is disabled,
/// - the four LeadingCartBehaviour wheel-drive components are stopped,
/// - powerup permission is NOT changed,
/// - normal control is restored when MoveBackward finishes.
/// </summary>
public class SnakeMoveBackwardController : MonoBehaviour
{
    #region Chain Reverse Settings

    [Header("Chain Move Backward")]
    [Min(0.1f)]
    [SerializeField] private float moveBackwardDistance = 3f;

    [Min(0.05f)]
    [SerializeField] private float moveBackwardDuration = 0.7f;

    [Tooltip("X = normalized time, Y = normalized tail travel.")]
    [SerializeField] private AnimationCurve moveBackwardMotionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Min(0f)]
    [SerializeField] private float moveBackwardCooldown = 1.25f;

    [Header("Reverse Follower Path")]
    [Min(0.01f)]
    [SerializeField] private float tangentSampleDistance = 0.2f;

    [Min(0f)]
    [SerializeField] private float followerRotationFollowSpeed = 12f;

    [Tooltip("If old path history is too short, continue backward from its oldest available tangent.")]
    [SerializeField] private bool allowStraightExtensionWhenHistoryRunsOut = true;

    [Header("C1 -> Leader Reverse Tow")]
    [Tooltip("Extra separation C1 may create before it begins pulling the Leader.")]
    [Min(0f)]
    [SerializeField] private float reverseTowExtraSlack = 0.75f;

    [Min(0f)]
    [SerializeField] private float towActivationTolerance = 0.01f;

    [Min(0f)]
    [SerializeField] private float leaderTowRotationFollowSpeed = 10f;

    [SerializeField] private bool rotateLeaderFromReverseTow = true;

    #endregion

    #region Single Cart Settings

    [Header("Single Cart Move Backward")]
    [Tooltip("Maximum backward planar speed when the Leader has no followers.")]
    [Min(0.1f)]
    [SerializeField] private float singleCartReverseSpeed = 6f;

    [Tooltip("Duration of the lone-cart reverse drive.")]
    [Min(0.05f)]
    [SerializeField] private float singleCartReverseDuration = 0.7f;

    [Tooltip("X = normalized time, Y = normalized backward speed.")]
    [SerializeField]
    private AnimationCurve singleCartReverseSpeedCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.2f, 1f),
        new Keyframe(0.75f, 1f),
        new Keyframe(1f, 0f)
    );

    #endregion

    #region Runtime Debug

    [Header("Runtime - Read Only")]
    [SerializeField] private bool isInitialized;
    [SerializeField] private bool isChainReversing;
    [SerializeField] private bool isSingleCartReversing;

    [SerializeField] private float requestedMoveBackwardDistance;
    [SerializeField] private float movedTailDistance;
    [SerializeField] private float tailMainStartProgress;
    [SerializeField] private float availableOldPathDistance;
    [SerializeField] private float temporaryHeadProgress;

    [Header("Reverse Tow Runtime - Read Only")]
    [SerializeField] private float startingLeaderToC1Distance;
    [SerializeField] private float activeReverseTowDistance;
    [SerializeField] private float currentLeaderToC1Distance;
    [SerializeField] private bool reverseTowIsTaut;
    [SerializeField] private float leaderMovedDistance;

    #endregion

    #region Debug

    [Header("Debug")]
    [SerializeField] private bool drawTemporaryPath = true;
    [SerializeField] private bool drawReverseTow = true;
    [SerializeField] private bool debugMoveBackward = false;

    #endregion

    #region Runtime References

    private SnakeCartManager snakeManager;
    private SnakePathHistory normalPath;
    private PhysicalChainJointProbe physicalProbe;

    private Rigidbody leaderBody;
    private LeadingCartBehaviour[] leadingMovements;
    private CartControlScript cartControl;

    #endregion

    #region Runtime State

    private TemporaryPath temporaryPath;
    private float[] followerOffsetsFromTemporaryHead;

    private int expectedSnakeCount;

    private float elapsedTime;
    private float singleCartReverseElapsed;
    private float nextAllowedMoveBackwardTime;

    private bool leaderWasKinematic;
    private float rewindPlaneY;

    private Vector3 leaderStartPosition;

    private Vector3 oldestPathPosition;
    private Vector3 oldestPathBackwardDirection;

    private Vector3 noHistoryExtensionOrigin;
    private Vector3 noHistoryExtensionDirection;

    #endregion

    #region Public API / Events

    public bool IsMovingBackward => isChainReversing || isSingleCartReversing;

    /// <summary>
    /// While MoveBackward owns locomotion, SnakeCartManager must not insert newly
    /// collected followers into the ACTIVE snakeBody topology. New carts may still
    /// spawn immediately as compact pending followers behind the tail; they simply
    /// remain outside the temporary reverse-path follower arrays until recovery ends.
    /// </summary>
    public bool IsChainTopologyLocked => IsMovingBackward;

    public System.Action OnMoveBackwardStarted;
    public System.Action OnMoveBackwardFinished;

    /// <summary>
    /// Called once by SnakeCartManager after the runtime leading-cart prefab exists.
    /// </summary>
    public void Initialize(
        SnakeCartManager manager,
        Rigidbody body,
        LeadingCartBehaviour[] movements,
        CartControlScript control,
        SnakePathHistory pathHistory,
        PhysicalChainJointProbe probe)
    {
        if (manager == null ||
            body == null ||
            movements == null ||
            movements.Length == 0 ||
            control == null ||
            pathHistory == null ||
            probe == null)
        {
            Debug.LogError("[SnakeMoveBackwardController] Missing initialization reference.", this);
            return;
        }

        if (cartControl != null) cartControl.OnMoveBackwardPressed -= TryBeginMoveBackward;

        snakeManager = manager;
        leaderBody = body;
        leadingMovements = movements;
        cartControl = control;
        normalPath = pathHistory;
        physicalProbe = probe;

        cartControl.OnMoveBackwardPressed += TryBeginMoveBackward;

        if (temporaryPath == null) temporaryPath = new TemporaryPath();

        isInitialized = true;
    }

    public bool PlayBackward(float distance)
    {
        if (!CanBeginMoveBackward(distance)) return false;

        List<GameObject> snakeBody = snakeManager.GetSnakeBody();
        if (snakeBody == null || snakeBody.Count == 0) return false;

        if (snakeBody.Count == 1)
        {
            BeginSingleCartReverse();
            return true;
        }

        if (normalPath == null || !normalPath.IsInitialized) return false;

        return BeginChainMoveBackward(distance, snakeBody);
    }

    [ContextMenu("TEST - Move Backward")]
    private void TestMoveBackward()
    {
        PlayBackward(moveBackwardDistance);
    }

    #endregion

    #region Tick / Input

    private void TryBeginMoveBackward()
    {
        PlayBackward(moveBackwardDistance);
    }

    /// <summary>
    /// Called by SnakeCartManager every FixedUpdate.
    ///
    /// Returns TRUE only while the 1+ follower reverse-tow mode owns the full
    /// chain. Lone-cart reverse returns FALSE so the normal physical probe/path
    /// can continue recording the leader's real reverse motion.
    /// </summary>
    public bool TickMoveBackward()
    {
        if (!isInitialized) return false;

        if (isSingleCartReversing)
        {
            TickSingleCartReverse();
            return false;
        }

        if (!isChainReversing) return false;

        TickChainMoveBackward();
        return true;
    }

    private bool CanBeginMoveBackward(float distance)
    {
        if (!isInitialized) return false;
        if (IsMovingBackward) return false;
        if (Time.time < nextAllowedMoveBackwardTime) return false;
        if (distance <= 0f) return false;
        if (snakeManager == null || leaderBody == null || cartControl == null) return false;

        return true;
    }

    #endregion

    #region Shared MoveBackward Control

    private void BeginMoveBackwardControlLock()
    {
        // MoveBackward owns locomotion, but NOT powerup permission.
        // CartControlScript's powerup input is independent from controllable,
        // so DisableControl() still allows an already-available powerup to fire.
        cartControl.DisableControl();
        cartControl.DisallowMoveBackward();

        StopLeadingMovement();
    }

    private void EndMoveBackwardControlLock()
    {
        ResetLeadingMovement();

        // StallController owns whether MoveBackward becomes available again.
        // We intentionally do NOT call AllowMoveBackward() here.
        cartControl.EnableControl();
    }

    private void StopLeadingMovement()
    {
        if (leadingMovements == null) return;

        for (int i = 0; i < leadingMovements.Length; i++)
        {
            if (leadingMovements[i] != null) leadingMovements[i].SetSpeedToZero();
        }
    }

    private void ResetLeadingMovement()
    {
        if (leadingMovements == null) return;

        for (int i = 0; i < leadingMovements.Length; i++)
        {
            if (leadingMovements[i] != null) leadingMovements[i].ResetSpeed();
        }
    }

    #endregion

    #region Single Cart Reverse

    private void BeginSingleCartReverse()
    {
        singleCartReverseElapsed = 0f;

        BeginMoveBackwardControlLock();

        isSingleCartReversing = true;

        OnMoveBackwardStarted?.Invoke();

        if (debugMoveBackward)
        {
            Debug.Log($"[MoveBackward] SINGLE START | speed:{singleCartReverseSpeed:F2} | duration:{singleCartReverseDuration:F2}", this);
        }
    }

    private void TickSingleCartReverse()
    {
        if (leaderBody == null)
        {
            FinishSingleCartReverse();
            return;
        }

        singleCartReverseElapsed += Time.fixedDeltaTime;

        float normalizedTime = Mathf.Clamp01(singleCartReverseElapsed / singleCartReverseDuration);
        float speedMultiplier = singleCartReverseSpeedCurve != null ? Mathf.Max(0f, singleCartReverseSpeedCurve.Evaluate(normalizedTime)) : 1f;
        float reverseSpeed = singleCartReverseSpeed * speedMultiplier;

        Vector3 forward = Vector3.ProjectOnPlane(leaderBody.transform.forward, Vector3.up);

        if (forward.sqrMagnitude > 0.0001f)
        {
            forward.Normalize();

            // Author only planar velocity. Vertical velocity and angular velocity
            // remain physical so the cart can still react naturally to the floor.
            float verticalVelocity = Vector3.Dot(leaderBody.linearVelocity, Vector3.up);
            leaderBody.linearVelocity = -forward * reverseSpeed + Vector3.up * verticalVelocity;
        }

        if (normalizedTime >= 1f) FinishSingleCartReverse();
    }

    private void FinishSingleCartReverse()
    {
        if (!isSingleCartReversing) return;

        isSingleCartReversing = false;
        nextAllowedMoveBackwardTime = Time.time + moveBackwardCooldown;

        EndMoveBackwardControlLock();

        OnMoveBackwardFinished?.Invoke();

        if (debugMoveBackward)
        {
            Debug.Log("[MoveBackward] SINGLE FINISH", this);
        }
    }

    #endregion

    #region Chain Reverse Start

    private bool BeginChainMoveBackward(float distance, List<GameObject> snakeBody)
    {
        if (snakeBody == null || snakeBody.Count < 2) return false;
        if (physicalProbe == null) return false;

        expectedSnakeCount = snakeBody.Count;
        rewindPlaneY = leaderBody.position.y;

        int tailIndex = snakeBody.Count - 1;

        tailMainStartProgress = snakeManager.GetDistancePathProgressForSnakeIndex(tailIndex);
        tailMainStartProgress = Mathf.Clamp(tailMainStartProgress, normalPath.OldestProgress, normalPath.HeadProgress);

        availableOldPathDistance = Mathf.Max(0f, tailMainStartProgress - normalPath.OldestProgress);
        requestedMoveBackwardDistance = distance;

        if (!allowStraightExtensionWhenHistoryRunsOut && availableOldPathDistance <= 0.05f) return false;
        if (!BuildTemporaryFollowerPath(snakeBody)) return false;

        PrepareOldPathExtensionData(snakeBody);

        GameObject firstFollower = snakeBody[1];

        Vector3 leaderToC1 = Vector3.ProjectOnPlane(
            leaderBody.position - firstFollower.transform.position,
            Vector3.up
        );

        startingLeaderToC1Distance = leaderToC1.magnitude;
        activeReverseTowDistance = startingLeaderToC1Distance + reverseTowExtraSlack;

        movedTailDistance = 0f;
        elapsedTime = 0f;
        temporaryHeadProgress = temporaryPath.HeadProgress;

        currentLeaderToC1Distance = startingLeaderToC1Distance;
        reverseTowIsTaut = false;

        leaderStartPosition = leaderBody.position;
        leaderMovedDistance = 0f;

        BeginMoveBackwardControlLock();

        // Reverse-tow owns the Leader pose. Clear old physical motion before
        // handing position/rotation ownership to MovePosition / MoveRotation.
        leaderBody.linearVelocity = Vector3.zero;
        leaderBody.angularVelocity = Vector3.zero;

        leaderWasKinematic = leaderBody.isKinematic;
        leaderBody.isKinematic = true;

        isChainReversing = true;

        OnMoveBackwardStarted?.Invoke();

        if (debugMoveBackward)
        {
            Debug.Log(
                $"[MoveBackward] CHAIN START | carts:{snakeBody.Count} | requested:{requestedMoveBackwardDistance:F2} | " +
                $"availableOldPath:{availableOldPathDistance:F2} | startTow:{startingLeaderToC1Distance:F2} | activeTow:{activeReverseTowDistance:F2}",
                this
            );
        }

        return true;
    }

    #endregion

    #region Temporary Reverse Path

    /// <summary>
    /// Seeds only C1 -> C2 -> ... -> Tail.
    /// The Leader and physical probe are deliberately excluded.
    /// </summary>
    private bool BuildTemporaryFollowerPath(List<GameObject> snakeBody)
    {
        if (snakeBody == null || snakeBody.Count < 2 || snakeBody[1] == null) return false;

        temporaryPath.Clear();

        followerOffsetsFromTemporaryHead = new float[snakeBody.Count];
        float[] followerInitialProgress = new float[snakeBody.Count];

        Vector3 c1Position = FlattenToRewindPlane(snakeBody[1].transform.position);

        temporaryPath.Reset(c1Position);
        followerInitialProgress[1] = 0f;

        for (int i = 2; i < snakeBody.Count; i++)
        {
            if (snakeBody[i] == null) return false;

            Vector3 position = FlattenToRewindPlane(snakeBody[i].transform.position);
            followerInitialProgress[i] = temporaryPath.Append(position);
        }

        float initialHeadProgress = temporaryPath.HeadProgress;

        for (int i = 1; i < snakeBody.Count; i++)
        {
            followerOffsetsFromTemporaryHead[i] = initialHeadProgress - followerInitialProgress[i];
        }

        temporaryHeadProgress = initialHeadProgress;
        return true;
    }

    private void PrepareOldPathExtensionData(List<GameObject> snakeBody)
    {
        GameObject tail = snakeBody[snakeBody.Count - 1];

        noHistoryExtensionOrigin = FlattenToRewindPlane(tail.transform.position);

        Vector3 endTangent = temporaryPath.GetEndTangent();

        if (endTangent.sqrMagnitude < 0.0001f)
        {
            endTangent = -Vector3.ProjectOnPlane(tail.transform.forward, Vector3.up);
        }

        if (endTangent.sqrMagnitude < 0.0001f) endTangent = -Vector3.forward;

        noHistoryExtensionDirection = endTangent.normalized;

        if (availableOldPathDistance > 0.001f &&
            normalPath.TryGetPoseAtProgress(normalPath.OldestProgress, out Vector3 oldestPosition, out Quaternion oldestRotation))
        {
            oldestPathPosition = FlattenToRewindPlane(oldestPosition);

            Vector3 oldestForward = Vector3.ProjectOnPlane(oldestRotation * Vector3.forward, Vector3.up);

            if (oldestForward.sqrMagnitude < 0.0001f)
            {
                oldestForward = -noHistoryExtensionDirection;
            }

            oldestPathBackwardDirection = -oldestForward.normalized;
        }
        else
        {
            oldestPathPosition = noHistoryExtensionOrigin;
            oldestPathBackwardDirection = noHistoryExtensionDirection;
        }
    }

    private bool TryGetTailDestination(float desiredTailTravel, out Vector3 position)
    {
        position = Vector3.zero;

        if (desiredTailTravel <= availableOldPathDistance + 0.0001f)
        {
            float desiredProgress = Mathf.Max(
                normalPath.OldestProgress,
                tailMainStartProgress - desiredTailTravel
            );

            if (!normalPath.TryGetPositionAtProgress(desiredProgress, out position)) return false;

            position = FlattenToRewindPlane(position);
            return true;
        }

        if (!allowStraightExtensionWhenHistoryRunsOut) return false;

        float extensionDistance = desiredTailTravel - availableOldPathDistance;

        if (availableOldPathDistance > 0.001f)
        {
            position = oldestPathPosition + oldestPathBackwardDirection * extensionDistance;
        }
        else
        {
            position = noHistoryExtensionOrigin + noHistoryExtensionDirection * desiredTailTravel;
        }

        position = FlattenToRewindPlane(position);
        return true;
    }

    #endregion

    #region Chain Reverse Tick

    private void TickChainMoveBackward()
    {
        List<GameObject> snakeBody = snakeManager.GetSnakeBody();

        if (snakeBody == null || snakeBody.Count != expectedSnakeCount)
        {
            Debug.LogWarning(
                "[MoveBackward] Active snake topology changed unexpectedly during reverse. " +
                "Normal collection should only create pending followers and must not change snakeBody. Ending reverse safely.",
                this
            );

            FinishChainMoveBackward();
            return;
        }

        elapsedTime += Time.fixedDeltaTime;

        float normalizedTime = Mathf.Clamp01(elapsedTime / moveBackwardDuration);
        float curveValue = moveBackwardMotionCurve != null ? Mathf.Clamp01(moveBackwardMotionCurve.Evaluate(normalizedTime)) : normalizedTime;
        float desiredMovedDistance = Mathf.Max(movedTailDistance, requestedMoveBackwardDistance * curveValue);

        if (desiredMovedDistance > movedTailDistance + 0.00001f)
        {
            if (!TryGetTailDestination(desiredMovedDistance, out Vector3 tailDestination))
            {
                FinishChainMoveBackward();
                return;
            }

            temporaryPath.Append(tailDestination);

            movedTailDistance = desiredMovedDistance;
            temporaryHeadProgress = temporaryPath.HeadProgress;
        }

        MoveFollowerChain(snakeBody);
        ApplyReverseTowToLeader(snakeBody[1]);

        if (normalizedTime >= 1f || movedTailDistance >= requestedMoveBackwardDistance - 0.001f)
        {
            FinishChainMoveBackward();
        }
    }

    private void MoveFollowerChain(List<GameObject> snakeBody)
    {
        float temporaryHead = temporaryPath.HeadProgress;

        for (int i = 1; i < snakeBody.Count; i++)
        {
            GameObject cart = snakeBody[i];
            if (cart == null) continue;

            float cartProgress = temporaryHead - followerOffsetsFromTemporaryHead[i];

            if (!temporaryPath.TryGetPose(
                    cartProgress,
                    tangentSampleDistance,
                    out Vector3 cartPosition,
                    out Vector3 cartTangent))
            {
                continue;
            }

            cartPosition = FlattenToRewindPlane(cartPosition);

            Quaternion targetRotation = cart.transform.rotation;

            if (cartTangent.sqrMagnitude > 0.0001f)
            {
                // Temporary path tangent points in reverse-travel direction.
                // The cart itself faces opposite that tangent while backing up.
                Quaternion desiredRotation = Quaternion.LookRotation(-cartTangent, Vector3.up);
                float rotationT = 1f - Mathf.Exp(-followerRotationFollowSpeed * Time.fixedDeltaTime);

                targetRotation = Quaternion.Slerp(
                    cart.transform.rotation,
                    desiredRotation,
                    rotationT
                );
            }

            cart.transform.SetPositionAndRotation(cartPosition, targetRotation);
        }
    }

    #endregion

    #region Reverse Tow

    /// <summary>
    /// C1 temporarily acts as the tractor for the Leader.
    ///
    /// Starting spacing + Reverse Tow Extra Slack is the maximum allowed
    /// separation. Until that distance is exceeded, C1 retreats while the
    /// Leader remains where it is. Once taut, C1 begins towing the Leader.
    /// </summary>
    private void ApplyReverseTowToLeader(GameObject firstFollower)
    {
        if (firstFollower == null || leaderBody == null) return;

        Vector3 c1Position = FlattenToRewindPlane(firstFollower.transform.position);
        Vector3 currentLeaderPosition = FlattenToRewindPlane(leaderBody.position);

        Vector3 c1ToLeader = Vector3.ProjectOnPlane(
            currentLeaderPosition - c1Position,
            Vector3.up
        );

        currentLeaderToC1Distance = c1ToLeader.magnitude;

        float tautThreshold = activeReverseTowDistance + towActivationTolerance;

        if (currentLeaderToC1Distance <= tautThreshold)
        {
            reverseTowIsTaut = false;
            return;
        }

        reverseTowIsTaut = true;

        Vector3 towDirection = c1ToLeader.sqrMagnitude > 0.0001f
            ? c1ToLeader.normalized
            : Vector3.ProjectOnPlane(leaderBody.transform.forward, Vector3.up).normalized;

        if (towDirection.sqrMagnitude < 0.0001f) towDirection = Vector3.forward;

        Vector3 targetLeaderPosition = c1Position + towDirection * activeReverseTowDistance;
        targetLeaderPosition.y = rewindPlaneY;

        leaderBody.MovePosition(targetLeaderPosition);

        if (rotateLeaderFromReverseTow)
        {
            Vector3 desiredForward = Vector3.ProjectOnPlane(
                targetLeaderPosition - c1Position,
                Vector3.up
            );

            if (desiredForward.sqrMagnitude > 0.0001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(
                    desiredForward.normalized,
                    Vector3.up
                );

                float rotationT = 1f - Mathf.Exp(
                    -leaderTowRotationFollowSpeed * Time.fixedDeltaTime
                );

                leaderBody.MoveRotation(
                    Quaternion.Slerp(
                        leaderBody.rotation,
                        desiredRotation,
                        rotationT
                    )
                );
            }
        }

        leaderMovedDistance = Vector3.ProjectOnPlane(
            targetLeaderPosition - leaderStartPosition,
            Vector3.up
        ).magnitude;
    }

    #endregion

    #region Chain Recovery

    private void FinishChainMoveBackward()
    {
        if (!isChainReversing) return;

        // SnakeCartManager rebuilds the physical probe and seeds a fresh normal
        // path from the exact current Probe -> C1 -> ... -> Tail geometry.
        bool recoveryBuilt = snakeManager.CompleteMoveBackwardRecovery();

        leaderBody.isKinematic = leaderWasKinematic;

        if (!leaderBody.isKinematic)
        {
            leaderBody.linearVelocity = Vector3.zero;
            leaderBody.angularVelocity = Vector3.zero;
        }

        isChainReversing = false;
        nextAllowedMoveBackwardTime = Time.time + moveBackwardCooldown;

        temporaryPath.Clear();

        EndMoveBackwardControlLock();

        OnMoveBackwardFinished?.Invoke();

        if (!recoveryBuilt)
        {
            Debug.LogError("[MoveBackward] Recovery path failed to build.", this);
        }

        if (debugMoveBackward)
        {
            Debug.Log(
                $"[MoveBackward] CHAIN FINISH | recoveryBuilt:{recoveryBuilt} | " +
                $"tailMoved:{movedTailDistance:F2} | leaderMoved:{leaderMovedDistance:F2}",
                this
            );
        }
    }

    #endregion

    #region Helpers / Cleanup

    private Vector3 FlattenToRewindPlane(Vector3 position)
    {
        position.y = rewindPlaneY;
        return position;
    }

    private void OnDisable()
    {
        RestoreRuntimeStateWithoutEvents();
    }

    private void OnDestroy()
    {
        if (cartControl != null)
        {
            cartControl.OnMoveBackwardPressed -= TryBeginMoveBackward;
        }
    }

    private void RestoreRuntimeStateWithoutEvents()
    {
        if (!IsMovingBackward) return;

        if (isChainReversing && leaderBody != null)
        {
            leaderBody.isKinematic = leaderWasKinematic;
        }

        isChainReversing = false;
        isSingleCartReversing = false;

        if (temporaryPath != null) temporaryPath.Clear();

        if (leadingMovements != null) ResetLeadingMovement();

        if (cartControl != null) cartControl.EnableControl();
    }

    #endregion

    #region Debug Drawing

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        if (drawTemporaryPath && temporaryPath != null && temporaryPath.Count >= 2)
        {
            Gizmos.color = Color.magenta;

            for (int i = 1; i < temporaryPath.Count; i++)
            {
                Gizmos.DrawLine(
                    temporaryPath.GetPosition(i - 1),
                    temporaryPath.GetPosition(i)
                );
            }
        }

        if (drawReverseTow && snakeManager != null)
        {
            List<GameObject> snakeBody = snakeManager.GetSnakeBody();

            if (snakeBody != null &&
                snakeBody.Count >= 2 &&
                snakeBody[1] != null &&
                leaderBody != null)
            {
                Gizmos.color = reverseTowIsTaut ? Color.green : Color.yellow;

                Gizmos.DrawLine(
                    snakeBody[1].transform.position,
                    leaderBody.position
                );
            }
        }
    }

    #endregion

    #region Temporary Path

    private class TemporaryPath
    {
        private struct Point
        {
            public Vector3 position;
            public float distance;

            public Point(Vector3 position, float distance)
            {
                this.position = position;
                this.distance = distance;
            }
        }

        private readonly List<Point> points = new List<Point>(256);

        public int Count => points.Count;

        public float HeadProgress =>
            points.Count > 0
                ? points[points.Count - 1].distance
                : 0f;

        public void Clear()
        {
            points.Clear();
        }

        public void Reset(Vector3 position)
        {
            points.Clear();
            points.Add(new Point(position, 0f));
        }

        public float Append(Vector3 position)
        {
            if (points.Count == 0)
            {
                Reset(position);
                return 0f;
            }

            Point lastPoint = points[points.Count - 1];

            Vector3 delta = Vector3.ProjectOnPlane(
                position - lastPoint.position,
                Vector3.up
            );

            float addedDistance = delta.magnitude;

            if (addedDistance <= 0.00001f) return lastPoint.distance;

            float newDistance = lastPoint.distance + addedDistance;

            points.Add(new Point(position, newDistance));

            return newDistance;
        }

        public Vector3 GetEndTangent()
        {
            if (points.Count < 2) return Vector3.zero;

            Vector3 tangent = Vector3.ProjectOnPlane(
                points[points.Count - 1].position - points[points.Count - 2].position,
                Vector3.up
            );

            if (tangent.sqrMagnitude > 0.0001f) tangent.Normalize();

            return tangent;
        }

        public bool TryGetPose(
            float progress,
            float tangentDistance,
            out Vector3 position,
            out Vector3 tangent)
        {
            position = Vector3.zero;
            tangent = Vector3.zero;

            if (!TryGetPosition(progress, out position)) return false;

            float sampleDistance = Mathf.Max(0.01f, tangentDistance);

            TryGetPosition(progress - sampleDistance, out Vector3 before);
            TryGetPosition(progress + sampleDistance, out Vector3 after);

            tangent = Vector3.ProjectOnPlane(after - before, Vector3.up);

            if (tangent.sqrMagnitude < 0.0001f && points.Count >= 2)
            {
                tangent = Vector3.ProjectOnPlane(
                    points[points.Count - 1].position - points[points.Count - 2].position,
                    Vector3.up
                );
            }

            if (tangent.sqrMagnitude > 0.0001f) tangent.Normalize();

            return true;
        }

        public bool TryGetPosition(float progress, out Vector3 position)
        {
            position = Vector3.zero;

            if (points.Count == 0) return false;

            if (points.Count == 1)
            {
                position = points[0].position;
                return true;
            }

            progress = Mathf.Clamp(
                progress,
                points[0].distance,
                HeadProgress
            );

            int low = 0;
            int high = points.Count - 1;

            while (low < high)
            {
                int mid = (low + high) / 2;

                if (points[mid].distance < progress) low = mid + 1;
                else high = mid;
            }

            int upperIndex = low;

            if (upperIndex == 0)
            {
                position = points[0].position;
                return true;
            }

            Point lower = points[upperIndex - 1];
            Point upper = points[upperIndex];

            float range = upper.distance - lower.distance;
            float interpolation = range > 0.00001f
                ? (progress - lower.distance) / range
                : 0f;

            position = Vector3.Lerp(
                lower.position,
                upper.position,
                interpolation
            );

            return true;
        }

        public Vector3 GetPosition(int index)
        {
            return index >= 0 && index < points.Count
                ? points[index].position
                : Vector3.zero;
        }
    }

    #endregion
}