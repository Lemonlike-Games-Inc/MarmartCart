using UnityEngine;

/// <summary>
/// Reliable stall watchdog for the leading cart.
///
/// Replaces the old persistent front-trigger stall sensor.
///
/// Detection:
/// 1) Observe actual planar displacement over a short sample window.
/// 2) Only when displacement is very low, query nearby blocking geometry
///    with a tunable OverlapBoxNonAlloc volume.
/// 3) Nearby obstacle + low displacement = Stall.
/// 4) If the obstacle query finds nothing but the cart stays nearly motionless
///    for the longer fail-safe duration, grant Stall anyway.
///
/// Stall is latched until MoveBackward successfully starts or stall detection
/// is intentionally suppressed.
/// </summary>
[DisallowMultipleComponent]
public class LeadingCartStallController : MonoBehaviour
{
    #region References

    [Header("References")]
    [SerializeField] private Rigidbody cartBody;
    [SerializeField] private CartControlScript cartControlInput;
    [SerializeField] private CartDriftController driftController;
    [SerializeField] private SnakeMoveBackwardController moveBackwardController;

    #endregion

    #region Displacement Detection

    [Header("Gate 1 - Actual Displacement")]
    [Tooltip("How long one displacement observation window lasts.")]
    [Min(0.05f)]
    [SerializeField] private float displacementSampleWindow = 0.30f;

    [Tooltip("If the cart moves this distance or less during one sample window, it is considered suspiciously stationary.")]
    [Min(0f)]
    [SerializeField] private float lowDisplacementThreshold = 0.12f;

    [Tooltip("While already stalled, moving at least this far during one sample window means the cart clearly freed itself without MoveBackward.")]
    [Min(0f)]
    [SerializeField] private float naturalStallExitDisplacement = 0.40f;

    #endregion

    #region Obstacle Query

    [Header("Gate 2 - Nearby Obstacle Query")]
    [Tooltip("World layers that count as stall-causing environment geometry.")]
    [SerializeField] private LayerMask blockingLayers;

    [Tooltip("Local-space center offset of the invisible obstacle query box relative to the leading cart Rigidbody.")]
    [SerializeField] private Vector3 obstacleCheckCenterOffset = new Vector3(0f, 0.5f, 0f);

    [Tooltip("FULL SIZE of the invisible obstacle query box. This is drawn in Scene view.")]
    [SerializeField] private Vector3 obstacleCheckSize = new Vector3(3.5f, 2f, 5f);

    [Tooltip("Rotate the obstacle query box with the cart's Y rotation.")]
    [SerializeField] private bool rotateObstacleCheckWithCart = true;

    [Tooltip("Draw the exact obstacle query box in Scene view.")]
    [SerializeField] private bool drawObstacleCheckGizmo = true;

    #endregion

    #region Fail Safe

    [Header("Fail-Safe Recovery")]
    [Tooltip("If the cart remains under the low-displacement threshold this long, grant Stall even if the obstacle query finds nothing.")]
    [Min(0.1f)]
    [SerializeField] private float failSafeStallDuration = 1.25f;

    [Tooltip("Ignore stall detection briefly after startup.")]
    [Min(0f)]
    [SerializeField] private float initialDetectionGrace = 1f;

    [Tooltip("Ignore stall detection briefly after MoveBackward finishes so normal locomotion can resume.")]
    [Min(0f)]
    [SerializeField] private float postMoveBackwardGrace = 0.35f;

    #endregion

    #region Runtime Debug

    [Header("Runtime - Read Only")]
    [SerializeField] private bool isStalled;
    [SerializeField] private float lastSampleDisplacement;
    [SerializeField] private float lowDisplacementAccumulatedTime;
    [SerializeField] private bool lastObstacleNearby;
    [SerializeField] private int lastObstacleHitCount;
    [SerializeField] private float detectionGraceRemaining;
    [SerializeField] private bool externalStallDetectionSuppressed;

    private Vector3 sampleStartPosition;
    private float sampleElapsed;

    private bool restoreDriftAfterStall;
    private bool restoreSpeedupAfterStall;
    private bool stallRestrictionsActive;

    private readonly Collider[] obstacleResults = new Collider[32];

    public bool IsStalled => isStalled;
    public bool IsObstacleNearby => lastObstacleNearby;
    public float LastSampleDisplacement => lastSampleDisplacement;

    #endregion

    #region Events

    public System.Action OnStallStarted;
    public System.Action OnStallEnded;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (moveBackwardController == null) moveBackwardController = GetComponentInParent<SnakeMoveBackwardController>();

        if (cartBody == null) Debug.LogError("[LeadingCartStallController] Cart Rigidbody is not assigned.", this);
        if (cartControlInput == null) Debug.LogError("[LeadingCartStallController] CartControlScript is not assigned.", this);
        if (driftController == null) Debug.LogError("[LeadingCartStallController] CartDriftController is not assigned.", this);
        if (moveBackwardController == null) Debug.LogError("[LeadingCartStallController] SnakeMoveBackwardController is not assigned.", this);

        ResetObservation(initialDetectionGrace);
    }

    private void OnEnable()
    {
        if (moveBackwardController != null)
        {
            moveBackwardController.OnMoveBackwardStarted += HandleMoveBackwardStarted;
            moveBackwardController.OnMoveBackwardFinished += HandleMoveBackwardFinished;
        }
    }

    private void OnDisable()
    {
        if (moveBackwardController != null)
        {
            moveBackwardController.OnMoveBackwardStarted -= HandleMoveBackwardStarted;
            moveBackwardController.OnMoveBackwardFinished -= HandleMoveBackwardFinished;
        }

        cartControlInput?.DisallowMoveBackward();

        if (stallRestrictionsActive) ReleaseStallRestrictions();

        isStalled = false;
        ResetObservation(0f);
    }

    private void FixedUpdate()
    {
        if (cartBody == null || cartControlInput == null)
        {
            cartControlInput?.DisallowMoveBackward();
            return;
        }

        // Safety cleanup if MoveBackward ended unexpectedly without its normal
        // completion event.
        if (!isStalled && stallRestrictionsActive && (moveBackwardController == null || !moveBackwardController.IsMovingBackward))
        {
            ReleaseStallRestrictions();
        }

        if (ShouldSuppressStallDetection())
        {
            if (isStalled) ClearStall(true);

            cartControlInput.DisallowMoveBackward();
            ResetSampleOnly();
            return;
        }

        if (detectionGraceRemaining > 0f)
        {
            detectionGraceRemaining = Mathf.Max(0f, detectionGraceRemaining - Time.fixedDeltaTime);
            cartControlInput.DisallowMoveBackward();
            ResetSampleOnly();
            return;
        }

        bool sampleCompleted = UpdateDisplacementSample();

        if (isStalled)
        {
            // Stall is latched against small physics jitter, but it must also be
            // able to self-heal if the cart clearly slides/rotates free without
            // using MoveBackward.
            //
            // Use a separate, higher exit threshold than the entry threshold so
            // tiny collision movement cannot rapidly toggle Stall on/off.
            if (sampleCompleted && lastSampleDisplacement >= naturalStallExitDisplacement)
            {
                ClearStall(true);
                ResetObservation(0f);
                return;
            }

            MaintainMoveBackwardPermission();
            return;
        }

        cartControlInput.DisallowMoveBackward();

        if (!sampleCompleted) return;

        EvaluateCompletedSample();
    }

    #endregion

    #region Detection

    private bool UpdateDisplacementSample()
    {
        sampleElapsed += Time.fixedDeltaTime;

        if (sampleElapsed < displacementSampleWindow) return false;

        Vector3 currentPosition = FlattenPosition(cartBody.position);
        Vector3 startPosition = FlattenPosition(sampleStartPosition);

        lastSampleDisplacement = Vector3.Distance(startPosition, currentPosition);

        sampleStartPosition = cartBody.position;
        sampleElapsed = 0f;

        return true;
    }

    private void EvaluateCompletedSample()
    {
        if (lastSampleDisplacement > lowDisplacementThreshold)
        {
            lowDisplacementAccumulatedTime = 0f;
            lastObstacleNearby = false;
            lastObstacleHitCount = 0;
            return;
        }

        lowDisplacementAccumulatedTime += displacementSampleWindow;

        // Gate 2 only runs after Gate 1 reports very low actual displacement.
        lastObstacleNearby = CheckNearbyBlockingGeometry();

        if (lastObstacleNearby)
        {
            SetStalled();
            return;
        }

        // Fail-safe: a normally self-driving cart should not remain almost
        // completely motionless for this long unless something is wrong.
        if (lowDisplacementAccumulatedTime >= failSafeStallDuration)
        {
            SetStalled();
        }
    }

    private bool CheckNearbyBlockingGeometry()
    {
        GetObstacleCheckPose(out Vector3 center, out Quaternion rotation);

        Vector3 halfExtents = new Vector3(
            Mathf.Abs(obstacleCheckSize.x) * 0.5f,
            Mathf.Abs(obstacleCheckSize.y) * 0.5f,
            Mathf.Abs(obstacleCheckSize.z) * 0.5f
        );

        int hitCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            obstacleResults,
            rotation,
            blockingLayers,
            QueryTriggerInteraction.Ignore
        );

        lastObstacleHitCount = 0;

        for (int i = 0; i < hitCount; i++)
        {
            Collider candidate = obstacleResults[i];
            if (candidate == null) continue;
            if (BelongsToThisLeadingCart(candidate)) continue;

            lastObstacleHitCount++;
        }

        return lastObstacleHitCount > 0;
    }

    private bool BelongsToThisLeadingCart(Collider candidate)
    {
        if (candidate == null || cartBody == null) return false;

        if (candidate.attachedRigidbody == cartBody) return true;
        if (candidate.transform == cartBody.transform) return true;
        if (candidate.transform.IsChildOf(cartBody.transform)) return true;

        return false;
    }

    #endregion

    #region Stall State

    private void SetStalled()
    {
        if (isStalled) return;

        isStalled = true;

        ApplyStallRestrictions();
        MaintainMoveBackwardPermission();

        OnStallStarted?.Invoke();
    }

    private void ClearStall(bool restoreRestrictionsNow)
    {
        if (!isStalled) return;

        isStalled = false;
        cartControlInput.DisallowMoveBackward();

        if (restoreRestrictionsNow && stallRestrictionsActive) ReleaseStallRestrictions();

        OnStallEnded?.Invoke();
    }

    private void MaintainMoveBackwardPermission()
    {
        if (!isStalled)
        {
            cartControlInput.DisallowMoveBackward();
            return;
        }

        // CartControlScript still owns canMoveBackward. We continuously repair
        // it while stalled. If input consumes the bool but MoveBackward fails
        // to start, the watchdog grants it again on the next FixedUpdate.
        if (moveBackwardController == null || !moveBackwardController.IsMovingBackward) cartControlInput.AllowMoveBackward();
        else cartControlInput.DisallowMoveBackward();
    }

    private void ApplyStallRestrictions()
    {
        if (stallRestrictionsActive) return;

        // Save the raw permissions owned by Stall's neighboring systems, not
        // the derived result after temporary Cola/Freeze gates. Otherwise a
        // Stall that begins during Cola could permanently leave Drift and
        // normal Hype Speed Up disabled after both effects end.
        restoreDriftAfterStall =
            cartControlInput.IsDriftPermissionEnabled;
        restoreSpeedupAfterStall =
            cartControlInput.IsSpeedupPermissionEnabled;

        if (driftController != null) driftController.CancelDrift("Cart stalled");

        cartControlInput.DisallowDrift();
        cartControlInput.DisallowSpeedingUp();

        stallRestrictionsActive = true;
    }

    private void ReleaseStallRestrictions()
    {
        if (!stallRestrictionsActive) return;

        if (restoreDriftAfterStall) cartControlInput.AllowDrift();
        if (restoreSpeedupAfterStall) cartControlInput.AllowSpeedingUp();

        restoreDriftAfterStall = false;
        restoreSpeedupAfterStall = false;
        stallRestrictionsActive = false;
    }

    #endregion

    #region MoveBackward Integration

    private void HandleMoveBackwardStarted()
    {
        // A button press does NOT clear Stall. Only a reverse that actually
        // starts successfully reaches this callback.
        if (isStalled) ClearStall(false);

        cartControlInput.DisallowMoveBackward();
        ResetObservation(0f);
    }

    private void HandleMoveBackwardFinished()
    {
        if (stallRestrictionsActive) ReleaseStallRestrictions();

        cartControlInput.DisallowMoveBackward();
        ResetObservation(postMoveBackwardGrace);
    }

    #endregion

    #region Intentional Stop Suppression

    private bool ShouldSuppressStallDetection()
    {
        if (externalStallDetectionSuppressed) return true;
        if (cartControlInput.GetIsInPit()) return true;
        if (moveBackwardController != null && moveBackwardController.IsMovingBackward) return true;

        return false;
    }

    /// <summary>
    /// Future freeze/stun/cutscene systems can use this to tell the watchdog
    /// that the cart is intentionally not expected to move.
    /// </summary>
    public void SetStallDetectionSuppressed(bool suppressed)
    {
        externalStallDetectionSuppressed = suppressed;

        if (suppressed)
        {
            if (isStalled) ClearStall(true);

            cartControlInput?.DisallowMoveBackward();
            ResetObservation(0f);
        }
        else
        {
            ResetObservation(postMoveBackwardGrace);
        }
    }

    #endregion

    #region Observation Reset

    private void ResetObservation(float graceDuration)
    {
        sampleStartPosition = cartBody != null ? cartBody.position : transform.position;
        sampleElapsed = 0f;

        lastSampleDisplacement = 0f;
        lowDisplacementAccumulatedTime = 0f;

        lastObstacleNearby = false;
        lastObstacleHitCount = 0;

        detectionGraceRemaining = Mathf.Max(0f, graceDuration);
    }

    private void ResetSampleOnly()
    {
        sampleStartPosition = cartBody != null ? cartBody.position : transform.position;
        sampleElapsed = 0f;

        lastSampleDisplacement = 0f;
        lowDisplacementAccumulatedTime = 0f;

        lastObstacleNearby = false;
        lastObstacleHitCount = 0;
    }

    private Vector3 FlattenPosition(Vector3 position)
    {
        position.y = 0f;
        return position;
    }

    #endregion

    #region Obstacle Query Pose / Gizmos

    private void GetObstacleCheckPose(out Vector3 center, out Quaternion rotation)
    {
        Transform reference = cartBody != null ? cartBody.transform : transform;

        if (rotateObstacleCheckWithCart)
        {
            Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up);
            rotation = forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward.normalized, Vector3.up) : Quaternion.identity;
        }
        else
        {
            rotation = Quaternion.identity;
        }

        center = reference.position + rotation * obstacleCheckCenterOffset;
    }

    private void OnDrawGizmos()
    {
        if (!drawObstacleCheckGizmo) return;

        GetObstacleCheckPose(out Vector3 center, out Quaternion rotation);

        if (Application.isPlaying)
        {
            if (isStalled) Gizmos.color = Color.red;
            else if (lastObstacleNearby) Gizmos.color = Color.yellow;
            else Gizmos.color = Color.cyan;
        }
        else
        {
            Gizmos.color = Color.cyan;
        }

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(Mathf.Abs(obstacleCheckSize.x), Mathf.Abs(obstacleCheckSize.y), Mathf.Abs(obstacleCheckSize.z)));
        Gizmos.matrix = oldMatrix;
    }

    private void OnValidate()
    {
        displacementSampleWindow = Mathf.Max(0.05f, displacementSampleWindow);
        lowDisplacementThreshold = Mathf.Max(0f, lowDisplacementThreshold);
        naturalStallExitDisplacement = Mathf.Max(lowDisplacementThreshold, naturalStallExitDisplacement);

        obstacleCheckSize.x = Mathf.Max(0.01f, Mathf.Abs(obstacleCheckSize.x));
        obstacleCheckSize.y = Mathf.Max(0.01f, Mathf.Abs(obstacleCheckSize.y));
        obstacleCheckSize.z = Mathf.Max(0.01f, Mathf.Abs(obstacleCheckSize.z));

        failSafeStallDuration = Mathf.Max(displacementSampleWindow, failSafeStallDuration);
        initialDetectionGrace = Mathf.Max(0f, initialDetectionGrace);
        postMoveBackwardGrace = Mathf.Max(0f, postMoveBackwardGrace);
    }

    #endregion
}
