using UnityEngine;

/// <summary>
/// Per-wheel raycast suspension, lateral grip, and forward drive for the leading cart.
///
/// This component lives on each virtual/raycast wheel object.
/// All four wheel instances should share the same CartMovementProfile and Rigidbody.
///
/// Movement design:
/// - Normal Drive uses cargo-overload-adjusted base speed.
/// - Drift uses cargo-overload-adjusted base speed with tight-drift dip and recoverable fatigue.
/// - Speedup is the Hype-funded speedup mode from CartControlScript.
/// - Turn assist changes engine authority only; lateral grip remains independent.
/// - Battle/crash systems may temporarily stop and resume wheel drive through
///   SetSpeedToZero() and ResetSpeed().
/// </summary>
public class LeadingCartBehaviour : MonoBehaviour
{
    #region References

    [Header("References")]
    [Tooltip("Reads movement, Hype-funded Speedup, pit, and control state.")]
    [SerializeField] private CartControlScript cartControlInput;
    public CartControlScript CartControlInput => cartControlInput;

    [Tooltip("Main Rigidbody of the leading cart.")]
    [SerializeField] private Rigidbody cartBody;
    public Rigidbody CartBody => cartBody;

    [Tooltip("Shared movement tuning asset for Normal Drive, Drift, and Speedup.")]
    [SerializeField] private CartMovementProfile movementProfile;

    [Tooltip("Current drift state and tightness.")]
    [SerializeField] private CartDriftController driftController;
    public CartDriftController DriftController => driftController;

    [Tooltip("Player cargo controller used to read the current GLOBAL overload movement multiplier.")]
    [SerializeField] private CargoCapacityController cargoCapacityController;

    #endregion

    #region Raycast Settings

    [Header("Raycast Settings")]
    [SerializeField] private LayerMask layerMask;

    private bool isGrounded;

    #endregion

    #region Suspension Settings

    [Header("Suspension Settings")]
    [SerializeField] private float springRestLength = 1f;
    [SerializeField] private float springRaycastExtraLength = 0.3f;
    [SerializeField] private float springStrength = 1000f;
    [SerializeField] private float springDamping = 150f;

    private Vector3 springDirection;
    private Vector3 wheelVelocity;

    #endregion

    #region Lateral Grip Settings

    [Header("Lateral Grip Settings")]
    [Tooltip("Curve that decides how strongly the wheel cancels lateral sliding.")]
    [SerializeField] private AnimationCurve wheelGripCurve;

    [Tooltip("Lateral speed used to normalize the wheel grip curve.")]
    [Min(0.01f)]
    [SerializeField] private float maxLateralVelocity = 20f;

    [Tooltip("Effective wheel mass used for lateral correction force.")]
    [SerializeField] private float wheelMass = 1f;

    private Vector3 steeringDirection;

    #endregion

    #region Forward Drive Settings

    [Header("Forward Drive Settings")]
    [Tooltip("Engine torque available at normalized speed 0..1.")]
    [SerializeField] private AnimationCurve engineTorqueCurve;

    [Tooltip("Base cap for forward drive force. Mode profile turn assist may multiply this during turns.")]
    [SerializeField] private float maxEngineTorque = 100f;

    [Tooltip("Runtime speed target calculated from movement profile, chain length, and drive mode.")]
    [SerializeField] private float targetSpeed = 20f;

    private CartDriveMode currentDriveMode = CartDriveMode.NormalDrive;

    #endregion

    #region Brake Settings

    [Header("Brake Settings")]
    [SerializeField] private float minSpeed = 0f;
    [SerializeField] private float brakeFactor = 1f;
    [SerializeField] private float maxBrakeForce = 50f;

    #endregion



    #region Stop State

    private bool isStopping;

    [Header("Power-up Freeze - Runtime Read Only")]
    [SerializeField] private bool powerupFreezeSuppressed;

    public bool IsPowerupFreezeSuppressed => powerupFreezeSuppressed;

    #endregion

    #region Drift Runtime State

    private bool wasDriftingLastFrame;
    private float currentDriftFatigue;
    private float tightDriftDipTimer;
    private bool tightDriftDipArmed = true;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (cartControlInput == null) Debug.LogError("[LeadingCartBehaviour] CartControlScript is not assigned.", this);
        if (cartBody == null) Debug.LogError("[LeadingCartBehaviour] Cart Rigidbody is not assigned.", this);
        if (movementProfile == null) Debug.LogWarning("[LeadingCartBehaviour] CartMovementProfile is not assigned. Fallback movement values will be used.", this);
        if (driftController == null) Debug.LogError("[LeadingCartBehaviour] CartDriftController is not assigned.", this);
    }

    private void Start()
    {
        if (cargoCapacityController == null) cargoCapacityController = GetComponentInParent<CargoCapacityController>();

        if (cargoCapacityController == null)
        {
            Debug.LogWarning("[LeadingCartBehaviour] CargoCapacityController was not found. Overload movement penalty will remain inactive.", this);
        }
    }

    private void Update()
    {
        UpdateDriveModeAndTargetSpeed(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (cartBody == null || powerupFreezeSuppressed) return;

        bool didHitGround = Physics.Raycast(
            transform.position,
            -transform.up,
            out RaycastHit hit,
            springRestLength + springRaycastExtraLength,
            layerMask
        );

        if (!didHitGround)
        {
            isGrounded = false;
            return;
        }

        isGrounded = true;

        ApplySuspension(hit);
        ApplyLateralGrip();
        ApplyForwardDrive();
    }

    #endregion

    #region Drive Mode / Target Speed

    private void UpdateDriveModeAndTargetSpeed(float deltaTime)
    {
        if (cartControlInput == null) return;

        if (cartControlInput.GetIsInPit() ||
            isStopping ||
            powerupFreezeSuppressed)
        {
            targetSpeed = 0f;
            ResetDriftRuntimeState();
            return;
        }

        bool isDrifting = driftController != null && driftController.IsDrifting;

        UpdateDriftRuntimeState(isDrifting, deltaTime);

        currentDriveMode = ResolveDriveMode(isDrifting);

        float cargoAdjustedBaseSpeed = ComputeCargoAdjustedBaseSpeed();
        targetSpeed = ComputeTargetSpeed(currentDriveMode, cargoAdjustedBaseSpeed);
    }

    private CartDriveMode ResolveDriveMode(bool isDrifting)
    {
        if (isDrifting) return CartDriveMode.Drift;

        bool isUsingHypeSpeedup = cartControlInput.IsSpeedingUp() && cartControlInput.CanSpeedingUp();

        if (isUsingHypeSpeedup) return CartDriveMode.Speedup;

        return CartDriveMode.NormalDrive;
    }

    private float ComputeTargetSpeed(CartDriveMode mode, float cargoAdjustedBaseSpeed)
    {
        switch (mode)
        {
            case CartDriveMode.Speedup:
                return Mathf.Max(0f, cargoAdjustedBaseSpeed + GetSpeedupAdditiveBonus());

            case CartDriveMode.Drift:
                return Mathf.Max(0f, ComputeDriftTargetSpeed(cargoAdjustedBaseSpeed));

            default:
                return Mathf.Max(0f, cargoAdjustedBaseSpeed);
        }
    }

    private float ComputeCargoAdjustedBaseSpeed()
    {
        float baseSpeed = GetProfileBaseSpeed();
        float overloadMultiplier = cargoCapacityController != null ? cargoCapacityController.MovementSpeedMultiplier : 1f;

        return Mathf.Max(0f, baseSpeed * overloadMultiplier);
    }

    #endregion

    #region Drift Speed Feel

    private void UpdateDriftRuntimeState(bool isDrifting, float deltaTime)
    {
        if (!isDrifting)
        {
            ResetDriftRuntimeState();
            return;
        }

        if (!wasDriftingLastFrame)
        {
            currentDriftFatigue = 0f;
            tightDriftDipTimer = 0f;
            tightDriftDipArmed = true;
        }

        wasDriftingLastFrame = true;

        float tightness = GetDriftTightness();

        UpdateTightDriftDip(tightness, deltaTime);
        UpdateDriftFatigue(tightness, deltaTime);
    }

    private void ResetDriftRuntimeState()
    {
        wasDriftingLastFrame = false;
        currentDriftFatigue = 0f;
        tightDriftDipTimer = 0f;
        tightDriftDipArmed = true;
    }

    private void UpdateTightDriftDip(float tightness, float deltaTime)
    {
        CartDriftFeelSettings driftFeel = GetDriftFeelSettings();

        if (!driftFeel.enableTightDriftDip)
        {
            tightDriftDipTimer = 0f;
            tightDriftDipArmed = true;
            return;
        }

        if (tightness <= driftFeel.tightDriftDipRearmTightness) tightDriftDipArmed = true;

        if (tightDriftDipArmed && tightness >= driftFeel.tightDriftDipTriggerTightness)
        {
            tightDriftDipTimer = driftFeel.tightDriftDipDuration;
            tightDriftDipArmed = false;
        }

        if (tightDriftDipTimer > 0f) tightDriftDipTimer = Mathf.Max(0f, tightDriftDipTimer - deltaTime);
    }

    private void UpdateDriftFatigue(float tightness, float deltaTime)
    {
        CartDriftFeelSettings driftFeel = GetDriftFeelSettings();

        if (!driftFeel.enableDriftFatigueSpeedLoss)
        {
            currentDriftFatigue = 0f;
            return;
        }

        currentDriftFatigue += driftFeel.GetFatigueBuildAmount(tightness, deltaTime);
        currentDriftFatigue -= driftFeel.GetFatigueRecoverAmount(tightness, deltaTime);
        currentDriftFatigue = Mathf.Clamp01(currentDriftFatigue);
    }

    private float ComputeDriftTargetSpeed(float chainAdjustedBaseSpeed)
    {
        CartDriftFeelSettings driftFeel = GetDriftFeelSettings();

        float finalMultiplier = 1f;

        if (tightDriftDipTimer > 0f) finalMultiplier *= driftFeel.tightDriftDipMultiplier;

        finalMultiplier *= driftFeel.GetFatigueSpeedMultiplier(currentDriftFatigue);

        return chainAdjustedBaseSpeed * finalMultiplier;
    }

    #endregion

    #region Suspension

    private void ApplySuspension(RaycastHit hit)
    {
        springDirection = transform.up;
        wheelVelocity = cartBody.GetPointVelocity(transform.position);

        float wheelVelocityAlongSpring = Vector3.Dot(springDirection, wheelVelocity);
        float springCompression = springRestLength - hit.distance;
        float springForceMagnitude = springCompression * springStrength - wheelVelocityAlongSpring * springDamping;

        cartBody.AddForceAtPosition(springDirection * springForceMagnitude, transform.position);
    }

    #endregion

    #region Lateral Grip

    private void ApplyLateralGrip()
    {
        steeringDirection = transform.right;

        float lateralVelocity = Vector3.Dot(steeringDirection, wheelVelocity);
        float normalizedLateralVelocity = Mathf.Clamp01(Mathf.Abs(lateralVelocity) / maxLateralVelocity);
        float gripFactor = wheelGripCurve.Evaluate(normalizedLateralVelocity);

        float desiredVelocityChange = -lateralVelocity * gripFactor;
        float desiredAcceleration = desiredVelocityChange / Time.fixedDeltaTime;

        Vector3 steeringForce = steeringDirection * wheelMass * desiredAcceleration;
        cartBody.AddForceAtPosition(steeringForce, transform.position);
    }

    #endregion

    #region Forward Drive

    private void ApplyForwardDrive()
    {
        if (!isGrounded || targetSpeed <= 0.01f) return;

        Vector3 accelerationDirection = transform.forward;
        float cartForwardSpeed = Vector3.Dot(cartBody.transform.forward, cartBody.linearVelocity);
        float speedError = targetSpeed - cartForwardSpeed;

        if (speedError <= 0.1f) return;

        float normalizedSpeed = Mathf.Clamp01(Mathf.Abs(cartForwardSpeed) / targetSpeed);
        float availableTorque = engineTorqueCurve.Evaluate(normalizedSpeed);
        float desiredAcceleration = speedError / Time.fixedDeltaTime;

        float forceCap = maxEngineTorque * availableTorque * GetTurnAssistTorqueMultiplier();
        float forceMagnitude = Mathf.Min(desiredAcceleration * cartBody.mass, forceCap);

        cartBody.AddForceAtPosition(accelerationDirection * forceMagnitude, transform.position);
    }

    private float GetTurnAssistTorqueMultiplier()
    {
        CartMovementModeSettings settings = GetModeSettings(currentDriveMode);

        if (!settings.enableTurnSpeedAssist) return 1f;

        float requestedTurnAngle = GetRequestedTurnAngleForCurrentMode();
        float referenceAngle = Mathf.Max(1f, settings.turnAssistReferenceAngle);
        float normalizedTurnIntensity = Mathf.Clamp01(requestedTurnAngle / referenceAngle);

        return settings.GetTurnAssistTorqueMultiplier(normalizedTurnIntensity);
    }

    private float GetRequestedTurnAngleForCurrentMode()
    {
        if (currentDriveMode == CartDriveMode.Drift && driftController != null && driftController.IsDrifting)
        {
            return Mathf.Abs(driftController.DriftSteeringAngle);
        }

        if (cartControlInput == null) return 0f;

        Vector3 desiredDirection = cartControlInput.desiredDirection;

        if (desiredDirection.sqrMagnitude < 0.001f) return 0f;

        Vector3 cartForward = Vector3.ProjectOnPlane(cartBody.transform.forward, Vector3.up);
        desiredDirection = Vector3.ProjectOnPlane(desiredDirection, Vector3.up);

        if (cartForward.sqrMagnitude < 0.001f || desiredDirection.sqrMagnitude < 0.001f) return 0f;

        return Mathf.Abs(Vector3.SignedAngle(cartForward.normalized, desiredDirection.normalized, Vector3.up));
    }

    #endregion

    #region Brake

    public void Brake()
    {
        if (!isGrounded || powerupFreezeSuppressed) return;

        Vector3 accelerationDirection = transform.forward;
        float cartSpeed = Vector3.Dot(cartBody.transform.forward, cartBody.linearVelocity);

        if (cartSpeed <= minSpeed) return;

        float desiredVelocityChange = -cartSpeed * brakeFactor;
        float desiredAcceleration = desiredVelocityChange / Time.fixedDeltaTime;
        float brakeForceMagnitude = Mathf.Min(Mathf.Abs(desiredAcceleration * cartBody.mass), maxBrakeForce);

        cartBody.AddForceAtPosition(-accelerationDirection * brakeForceMagnitude, transform.position);
    }

    #endregion

    #region External Stop / Resume

    /// <summary>
    /// Immediately stops this wheel's drive state and zeros the shared cart Rigidbody velocity.
    /// Battle resolution currently calls this on all virtual wheels before applying its single
    /// centralized knockback impulse.
    /// </summary>
    public void SetSpeedToZero()
    {
        InterruptDriftFromCrash("SetSpeedToZero");

        isStopping = true;
        targetSpeed = 0f;

        if (cartBody != null) cartBody.linearVelocity = Vector3.zero;
    }

    public void ResetSpeed()
    {
        isStopping = false;
    }

    /// <summary>
    /// Independent force gate owned by the Ice effect. This does not modify
    /// the crash/Checkout stop flag, so either system may finish first without
    /// resuming movement owned by the other one.
    /// </summary>
    public void SetPowerupFreezeSuppressed(bool suppressed)
    {
        if (powerupFreezeSuppressed == suppressed) return;

        powerupFreezeSuppressed = suppressed;

        if (powerupFreezeSuppressed)
        {
            targetSpeed = 0f;
            ResetDriftRuntimeState();
        }
    }

    private void InterruptDriftFromCrash(string reason)
    {
        if (!ShouldInterruptDriftWhenSetSpeedToZero()) return;
        if (driftController == null) return;
        if (!driftController.IsDrifting && !driftController.IsDriftArmed) return;

        driftController.InterruptDrift(reason);
    }

    private bool ShouldInterruptDriftWhenSetSpeedToZero()
    {
        if (movementProfile == null) return true;

        return movementProfile.interruptDriftWhenSetSpeedToZero;
    }

    #endregion



    #region Movement Profile

    private CartMovementModeSettings GetModeSettings(CartDriveMode mode)
    {
        if (movementProfile != null) return movementProfile.GetSettings(mode);

        switch (mode)
        {
            case CartDriveMode.Drift:
                return CartMovementModeSettings.CreateDrift();

            case CartDriveMode.Speedup:
                return CartMovementModeSettings.CreateSpeedup();

            default:
                return CartMovementModeSettings.CreateNormalDrive();
        }
    }

    private CartDriftFeelSettings GetDriftFeelSettings()
    {
        if (movementProfile != null && movementProfile.driftFeel != null) return movementProfile.driftFeel;

        return CartDriftFeelSettings.CreateDefault();
    }

    private float GetProfileBaseSpeed()
    {
        return movementProfile != null ? movementProfile.baseSpeed : 20f;
    }

    private float GetSpeedupAdditiveBonus()
    {
        return movementProfile != null ? movementProfile.speedupAdditiveBonus : 5f;
    }

    private float GetDriftTightness()
    {
        if (driftController == null || !driftController.IsDrifting) return 0f;

        return Mathf.Clamp01(driftController.CurrentTightness);
    }

    #endregion
}
