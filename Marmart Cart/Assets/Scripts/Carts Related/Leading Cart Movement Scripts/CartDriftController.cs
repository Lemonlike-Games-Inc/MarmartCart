using UnityEngine;

/// <summary>
/// Owns the leading cart's drift state, drift side, input-to-tightness mapping,
/// and drift steering output.
///
/// Drift flow:
/// Hold drift input -> arm -> meet entry requirements -> drift.
/// While drifting, strong mapped input controls tightness and can optionally
/// switch sides. Interrupted drift remains blocked until the button is released.
/// </summary>
public class CartDriftController : MonoBehaviour
{
    #region Types

    public enum DriftState
    {
        None,
        DriftArmed,
        Drifting,
        DriftBlockedUntilRelease
    }

    public enum DriftInputMode
    {
        None,
        Holding,
        LiveMapped
    }

    #endregion

    #region References

    [Header("References")]
    [SerializeField] private CartControlScript cartControlInput;
    [SerializeField] private Rigidbody cartBody;

    #endregion

    #region Drift Entry

    [Header("Drift Entry")]
    [SerializeField] private float minSpeedToStartDrift = 5f;

    [Tooltip("Only used to choose a stable left/right drift side.")]
    [SerializeField] private float minInputMagnitudeToChooseSide = 0.15f;

    [Tooltip("Avoids random side selection from tiny angle noise.")]
    [SerializeField] private float minInputAngleToChooseSide = 5f;

    [Tooltip("If true, entry side is measured from cart forward. If false, it is measured from current travel direction.")]
    [SerializeField] private bool useCartForwardForEntrySide = true;

    #endregion

    #region Drift Side Switching

    [Header("Drift Side Switching")]
    [Tooltip("If true, drift side remains locked until the drift ends.")]
    [SerializeField] private bool lockDriftSide = true;

    [Tooltip("Minimum input magnitude required to switch to the opposite drift side.")]
    [SerializeField] private float sideSwitchMinInputMagnitude = 0.9f;

    [Tooltip("Input must reach this angle on the opposite side before switching.")]
    [SerializeField] private float sideSwitchMinAngleFromReference = 25f;

    #endregion

    #region Tightness Mapping

    [Header("Drift Tightness Mapping")]
    [Tooltip("Below this input magnitude, preserve the current tightness instead of remapping it.")]
    [SerializeField] private float driftInputHoldDeadzone = 0.9f;

    [Tooltip("Same-side angle at or below this maps to the widest drift.")]
    [SerializeField] private float wideSameSideAngle = 10f;

    [Tooltip("Same-side angle at or above this maps to the tightest drift.")]
    [SerializeField] private float tightSameSideAngle = 70f;

    [Tooltip("If true, tightness is measured from current travel direction. If false, it is measured from cart forward.")]
    [SerializeField] private bool usePathDirectionForTightness = true;

    #endregion

    #region Drift Steering

    [Header("Drift Steering")]
    [Tooltip("Wheel angle for the widest drift.")]
    [SerializeField] private float wideDriftSteerAngle = 5f;

    [Tooltip("Wheel angle at Base Tightness Point.")]
    [SerializeField] private float baseDriftSteerAngle = 15f;

    [Tooltip("Wheel angle at maximum tightness.")]
    [SerializeField] private float tightDriftSteerAngle = 30f;

    [Tooltip("Tightness value where steering reaches Base Drift Steer Angle.")]
    [Range(0.01f, 0.99f)]
    [SerializeField] private float baseTightnessPoint = 0.5f;

    [SerializeField] private bool invertDriftSteeringSign = false;

    #endregion

    #region Debug

    [Header("Debug")]
    [SerializeField] private bool debugDriftState = false;

    [Min(0.05f)]
    [SerializeField] private float debugLogInterval = 0.35f;

    #endregion

    #region Runtime State

    private DriftState driftState = DriftState.None;
    private DriftInputMode currentInputMode = DriftInputMode.None;

    private float driftSign;
    private float currentInputAngle;
    private float currentSameSideAngle;
    private float currentSpeed;
    private float currentTightness;
    private float debugTimer;

    private int sideSwitchCount;

    private Vector3 entryForward = Vector3.zero;
    private Vector3 entryInputDirection = Vector3.zero;
    private Vector3 entryPathDirection = Vector3.zero;

    #endregion

    #region Public State

    public bool IsDrifting => driftState == DriftState.Drifting;
    public bool IsDriftArmed => driftState == DriftState.DriftArmed;
    public bool IsDriftBlockedUntilRelease => driftState == DriftState.DriftBlockedUntilRelease;

    public bool LockDriftSide => lockDriftSide;
    public int SideSwitchCount => sideSwitchCount;

    /// <summary>
    /// -1 = left drift, +1 = right drift.
    /// </summary>
    public float DriftSign => driftSign;

    public string DriftSideName
    {
        get
        {
            if (!IsDrifting) return "None";
            return driftSign > 0f ? "Right" : "Left";
        }
    }

    public string CurrentStateName => driftState.ToString();
    public DriftInputMode CurrentInputMode => currentInputMode;

    public Vector3 EntryForward => entryForward;
    public Vector3 EntryInputDirection => entryInputDirection;
    public Vector3 EntryPathDirection => entryPathDirection;

    public float CurrentInputAngle => currentInputAngle;
    public float CurrentSameSideAngle => currentSameSideAngle;
    public float CurrentSpeed => currentSpeed;
    public float CurrentTightness => currentTightness;

    public float DriftSteeringAngle
    {
        get
        {
            if (!IsDrifting) return 0f;

            float steerMagnitude;

            if (currentTightness <= baseTightnessPoint)
            {
                float t = Mathf.InverseLerp(0f, baseTightnessPoint, currentTightness);
                steerMagnitude = Mathf.Lerp(wideDriftSteerAngle, baseDriftSteerAngle, t);
            }
            else
            {
                float t = Mathf.InverseLerp(baseTightnessPoint, 1f, currentTightness);
                steerMagnitude = Mathf.Lerp(baseDriftSteerAngle, tightDriftSteerAngle, t);
            }

            float sign = invertDriftSteeringSign ? -driftSign : driftSign;
            return sign * steerMagnitude;
        }
    }

    #endregion

    #region Events

    public System.Action OnDriftStarted;
    public System.Action<string> OnDriftEndedClean;
    public System.Action<string> OnDriftInterrupted;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (cartControlInput == null) Debug.LogError("[CartDriftController] CartControlScript is not assigned.", this);
        if (cartBody == null) Debug.LogError("[CartDriftController] Cart Rigidbody is not assigned.", this);
    }

    private void Update()
    {
        UpdateDriftState();
    }

    #endregion

    #region Drift State Machine

    private void UpdateDriftState()
    {
        if (cartControlInput == null || cartBody == null)
        {
            ForceClearDrift();
            return;
        }

        bool driftHeld = cartControlInput.IsDriftHeld();
        Vector3 desiredDirection = cartControlInput.desiredDirection;
        Vector2 moveInput = cartControlInput.MoveInput;

        currentSpeed = GetPlanarSpeed();

        if (driftState == DriftState.DriftBlockedUntilRelease)
        {
            if (!driftHeld)
            {
                ForceClearDrift();
                LogDebug("[Drift] UNBLOCKED after drift button release.");
            }

            return;
        }

        if (!driftHeld)
        {
            EndDrift();
            return;
        }

        if (driftState == DriftState.None)
        {
            driftState = DriftState.DriftArmed;
            currentInputMode = DriftInputMode.Holding;
            LogDebug("[Drift] ARMED");
        }

        if (driftState == DriftState.DriftArmed)
        {
            TryStartDrift(desiredDirection, moveInput);
            return;
        }

        if (driftState == DriftState.Drifting)
        {
            UpdateTightnessDirectMapping(desiredDirection, moveInput);
            DebugActiveDrift();
        }
    }

    private void TryStartDrift(Vector3 desiredDirection, Vector2 moveInput)
    {
        if (currentSpeed < minSpeedToStartDrift) return;
        if (moveInput.magnitude < minInputMagnitudeToChooseSide) return;
        if (desiredDirection.sqrMagnitude < 0.001f) return;

        Vector3 referenceDirection = useCartForwardForEntrySide ? GetCartForward() : GetPathDirection();
        float entryAngle = Vector3.SignedAngle(referenceDirection, desiredDirection.normalized, Vector3.up);

        if (Mathf.Abs(entryAngle) < minInputAngleToChooseSide) return;

        driftSign = Mathf.Sign(entryAngle);
        if (Mathf.Approximately(driftSign, 0f)) return;

        entryForward = GetCartForward();
        entryInputDirection = desiredDirection.normalized;
        entryPathDirection = GetPathDirection();

        driftState = DriftState.Drifting;
        currentInputMode = DriftInputMode.Holding;

        debugTimer = 0f;
        sideSwitchCount = 0;

        // Entry uses the same direct mapping as active drift, but bypasses the
        // hold deadzone so the first tightness value is established immediately.
        UpdateTightnessDirectMapping(desiredDirection, moveInput, true);

        OnDriftStarted?.Invoke();

        LogDebug(
            $"[Drift] START {DriftSideName} | entryAngle:{entryAngle:F1} | " +
            $"tightness:{currentTightness:F2} | steer:{DriftSteeringAngle:F1} | speed:{currentSpeed:F1}"
        );
    }

    public void InterruptDrift(string reason = "Interrupted")
    {
        if (driftState == DriftState.None || driftState == DriftState.DriftBlockedUntilRelease) return;

        LogDebug($"[Drift] INTERRUPTED: {reason}");
        OnDriftInterrupted?.Invoke(reason);

        ForceClearDrift();

        // If drift is still held after an interruption, do not let it restart
        // until the player releases and presses the drift input again.
        driftState = DriftState.DriftBlockedUntilRelease;
        currentInputMode = DriftInputMode.Holding;
    }

    public void CancelDrift(string reason = "Cancelled")
    {
        if (driftState == DriftState.None) return;

        LogDebug($"[Drift] CANCELLED CLEANLY: {reason}");
        OnDriftEndedClean?.Invoke(reason);

        ForceClearDrift();
    }

    private void EndDrift()
    {
        if (driftState == DriftState.None) return;

        LogDebug($"[Drift] END {DriftSideName}");
        OnDriftEndedClean?.Invoke("Released drift button");

        ForceClearDrift();
    }

    private void ForceClearDrift()
    {
        driftState = DriftState.None;
        currentInputMode = DriftInputMode.None;

        driftSign = 0f;
        currentInputAngle = 0f;
        currentSameSideAngle = 0f;
        currentSpeed = 0f;
        currentTightness = 0f;

        debugTimer = 0f;
        sideSwitchCount = 0;

        entryForward = Vector3.zero;
        entryInputDirection = Vector3.zero;
        entryPathDirection = Vector3.zero;
    }

    #endregion

    #region Tightness / Side Mapping

    private void UpdateTightnessDirectMapping(Vector3 desiredDirection, Vector2 moveInput, bool forceLiveMap = false)
    {
        float moveMagnitude = moveInput.magnitude;

        bool shouldHold = !forceLiveMap &&
                          (moveMagnitude < driftInputHoldDeadzone || desiredDirection.sqrMagnitude < 0.001f);

        if (shouldHold)
        {
            currentInputMode = DriftInputMode.Holding;
            return;
        }

        Vector3 referenceDirection = usePathDirectionForTightness ? GetPathDirection() : GetCartForward();

        currentInputAngle = Vector3.SignedAngle(referenceDirection, desiredDirection.normalized, Vector3.up);

        TrySwitchDriftSideFromInput(desiredDirection, moveInput);

        currentSameSideAngle = currentInputAngle * driftSign;
        currentTightness = Mathf.Clamp01(Mathf.InverseLerp(wideSameSideAngle, tightSameSideAngle, currentSameSideAngle));
        currentInputMode = DriftInputMode.LiveMapped;
    }

    private void TrySwitchDriftSideFromInput(Vector3 desiredDirection, Vector2 moveInput)
    {
        if (lockDriftSide || !IsDrifting) return;
        if (moveInput.magnitude < sideSwitchMinInputMagnitude) return;
        if (desiredDirection.sqrMagnitude < 0.001f) return;
        if (Mathf.Abs(currentInputAngle) < sideSwitchMinAngleFromReference) return;

        float inputSideSign = Mathf.Sign(currentInputAngle);

        if (Mathf.Approximately(inputSideSign, 0f)) return;
        if (Mathf.Approximately(inputSideSign, driftSign)) return;

        float oldSign = driftSign;

        driftSign = inputSideSign;
        sideSwitchCount++;

        // Keep debug/reference entry data representative of the latest side.
        entryForward = GetCartForward();
        entryInputDirection = desiredDirection.normalized;
        entryPathDirection = GetPathDirection();

        string oldSide = oldSign > 0f ? "Right" : "Left";
        string newSide = driftSign > 0f ? "Right" : "Left";

        LogDebug($"[Drift] SIDE SWITCH {oldSide} -> {newSide} | inputAngle:{currentInputAngle:F1} | switches:{sideSwitchCount}");
    }

    #endregion

    #region Direction / Speed Helpers

    private float GetPlanarSpeed()
    {
        return Vector3.ProjectOnPlane(cartBody.linearVelocity, Vector3.up).magnitude;
    }

    private Vector3 GetPathDirection()
    {
        Vector3 planarVelocity = Vector3.ProjectOnPlane(cartBody.linearVelocity, Vector3.up);

        if (planarVelocity.sqrMagnitude > 0.1f) return planarVelocity.normalized;
        return GetCartForward();
    }

    private Vector3 GetCartForward()
    {
        Vector3 forward = Vector3.ProjectOnPlane(cartBody.transform.forward, Vector3.up);

        if (forward.sqrMagnitude > 0.001f) return forward.normalized;
        return Vector3.forward;
    }

    #endregion

    #region Debug

    private void DebugActiveDrift()
    {
        if (!debugDriftState) return;

        debugTimer += Time.deltaTime;
        if (debugTimer < debugLogInterval) return;

        debugTimer = 0f;

        Debug.Log(
            $"[Drift] HOLD {DriftSideName} | lockSide:{lockDriftSide} | switches:{sideSwitchCount} | " +
            $"mode:{currentInputMode} | inputAngle:{currentInputAngle:F1} | sameSideAngle:{currentSameSideAngle:F1} | " +
            $"tightness:{currentTightness:F2} | steer:{DriftSteeringAngle:F1} | speed:{currentSpeed:F1}",
            this
        );
    }

    private void LogDebug(string message)
    {
        if (debugDriftState) Debug.Log(message, this);
    }

    #endregion
}
