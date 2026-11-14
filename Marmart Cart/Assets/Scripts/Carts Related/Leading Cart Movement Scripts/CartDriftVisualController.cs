using UnityEngine;

/// <summary>
/// Applies drift presentation to the cart visual hierarchy.
///
/// Cart Model is the single visual root. Everything that should visually yaw
/// and lean with drift should live under this transform, including wheel models.
///
/// Virtual Wheels remain outside the visual hierarchy because rotating them
/// changes their physics force directions. Their yaw offset is therefore kept
/// as a separate optional setting.
/// </summary>
public class CartDriftVisualController : MonoBehaviour
{
    #region References

    [Header("References")]
    [SerializeField] private CartDriftController driftController;

    [Tooltip("Root containing every model that should visually yaw/lean with drift.")]
    [SerializeField] private Transform cartVisualRoot;

    [Tooltip("Root containing the four virtual/raycast wheels. Rotating this affects movement physics.")]
    [SerializeField] private Transform wheelPhysicsRoot;

    #endregion

    #region Visual Rotation

    [Header("Visual Rotation")]
    [SerializeField] private bool enableVisualYaw = true;
    [SerializeField] private bool enableVisualRoll = true;

    [Header("Visual Yaw")]
    [SerializeField] private float visualWideYawAngle = 30f;
    [SerializeField] private float visualTightYawAngle = 75f;
    [SerializeField] private float visualYawSmoothSpeed = 10f;
    [SerializeField] private bool invertVisualYaw = false;

    [Header("Visual Roll / Tilt")]
    [SerializeField] private float visualWideRollAngle = 0f;
    [SerializeField] private float visualTightRollAngle = 5f;
    [SerializeField] private float visualRollSmoothSpeed = 10f;
    [SerializeField] private bool invertVisualRoll = false;

    #endregion

    #region Visual Rotation Exclusions

    [Header("Visual Rotation Exclusions")]
    [Tooltip("Optional descendants of Cart Visual Root that should preserve their original non-drift world rotation.")]
    [SerializeField] private bool enableVisualRotationExclusions = true;

    [SerializeField] private Transform[] visualRotationExclusions;

    private Quaternion[] exclusionInitialRootRelativeRotations;

    #endregion

    #region Virtual Wheel Physics Yaw

    [Header("Virtual Wheel Physics Yaw")]
    [Tooltip("Rotates the virtual wheel force frame during drift. This changes the actual cart path.")]
    [SerializeField] private bool enablePhysicsWheelYawOffset = false;

    [SerializeField] private float physicsWideYawAngle = 10f;
    [SerializeField] private float physicsTightYawAngle = 35f;
    [SerializeField] private float physicsYawSmoothSpeed = 10f;

    [Tooltip("0 = no physics yaw offset, 1 = full configured offset.")]
    [SerializeField, Range(0f, 1f)] private float physicsYawInfluence = 0.569f;

    [SerializeField] private bool invertPhysicsYaw = false;

    #endregion

    #region Debug

    [Header("Debug")]
    [SerializeField] private bool debugDriftVisuals = false;

    [Min(0.05f)]
    [SerializeField] private float debugLogInterval = 0.35f;

    #endregion

    #region Runtime

    public float CurrentVisualYaw => currentVisualYaw;
    public float CurrentVisualRoll => currentVisualRoll;
    public float CurrentPhysicsYaw => currentPhysicsYaw;

    private Quaternion cartVisualInitialLocalRotation;
    private Quaternion wheelPhysicsInitialLocalRotation;

    private float currentVisualYaw;
    private float currentVisualRoll;
    private float currentPhysicsYaw;
    private float debugTimer;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (driftController == null) Debug.LogError("[CartDriftVisualController] CartDriftController is not assigned.", this);
        if (cartVisualRoot == null) Debug.LogError("[CartDriftVisualController] Cart Visual Root is not assigned.", this);

        if (cartVisualRoot != null) cartVisualInitialLocalRotation = cartVisualRoot.localRotation;
        if (wheelPhysicsRoot != null) wheelPhysicsInitialLocalRotation = wheelPhysicsRoot.localRotation;

        CacheVisualRotationExclusions();
    }

    private void LateUpdate()
    {
        UpdateDriftVisuals();
    }

    #endregion

    #region Drift Visual Update

    private void UpdateDriftVisuals()
    {
        bool isDrifting = driftController != null && driftController.IsDrifting;

        float driftSign = isDrifting ? driftController.DriftSign : 0f;
        float tightness = isDrifting ? Mathf.Clamp01(driftController.CurrentTightness) : 0f;

        float targetVisualYaw = 0f;
        float targetVisualRoll = 0f;
        float targetPhysicsYaw = 0f;

        if (isDrifting)
        {
            float visualYawMagnitude = Mathf.Lerp(visualWideYawAngle, visualTightYawAngle, tightness);
            float visualRollMagnitude = Mathf.Lerp(visualWideRollAngle, visualTightRollAngle, tightness);

            float visualYawSign = invertVisualYaw ? -driftSign : driftSign;
            float visualRollSign = invertVisualRoll ? -driftSign : driftSign;

            targetVisualYaw = visualYawSign * visualYawMagnitude;
            targetVisualRoll = visualRollSign * visualRollMagnitude;

            if (enablePhysicsWheelYawOffset)
            {
                float physicsYawMagnitude = Mathf.Lerp(physicsWideYawAngle, physicsTightYawAngle, tightness);
                float physicsYawSign = invertPhysicsYaw ? -driftSign : driftSign;
                targetPhysicsYaw = physicsYawSign * physicsYawMagnitude * physicsYawInfluence;
            }
        }

        currentVisualYaw = Mathf.Lerp(currentVisualYaw, targetVisualYaw, Time.deltaTime * visualYawSmoothSpeed);
        currentVisualRoll = Mathf.Lerp(currentVisualRoll, targetVisualRoll, Time.deltaTime * visualRollSmoothSpeed);
        currentPhysicsYaw = Mathf.Lerp(currentPhysicsYaw, targetPhysicsYaw, Time.deltaTime * physicsYawSmoothSpeed);

        ApplyCartVisualRotation();
        ApplyPhysicsWheelYaw();
        UpdateDebug(isDrifting, tightness);
    }

    #endregion

    #region Visual Rotation

    private void ApplyCartVisualRotation()
    {
        if (cartVisualRoot == null) return;

        float yaw = enableVisualYaw ? currentVisualYaw : 0f;
        float roll = enableVisualRoll ? currentVisualRoll : 0f;

        Quaternion driftOffset = Quaternion.Euler(0f, yaw, -roll);
        cartVisualRoot.localRotation = cartVisualInitialLocalRotation * driftOffset;

        ApplyVisualRotationExclusions();
    }

    private void CacheVisualRotationExclusions()
    {
        if (cartVisualRoot == null || visualRotationExclusions == null) return;

        exclusionInitialRootRelativeRotations = new Quaternion[visualRotationExclusions.Length];
        Quaternion visualRootWorldRotation = cartVisualRoot.rotation;

        for (int i = 0; i < visualRotationExclusions.Length; i++)
        {
            Transform excluded = visualRotationExclusions[i];

            if (excluded == null)
            {
                exclusionInitialRootRelativeRotations[i] = Quaternion.identity;
                continue;
            }

            exclusionInitialRootRelativeRotations[i] = Quaternion.Inverse(visualRootWorldRotation) * excluded.rotation;
        }
    }

    private void ApplyVisualRotationExclusions()
    {
        if (!enableVisualRotationExclusions) return;
        if (cartVisualRoot == null || visualRotationExclusions == null || exclusionInitialRootRelativeRotations == null) return;

        Quaternion parentWorldRotation = cartVisualRoot.parent != null ? cartVisualRoot.parent.rotation : Quaternion.identity;
        Quaternion visualRootBaseWorldRotation = parentWorldRotation * cartVisualInitialLocalRotation;

        int count = Mathf.Min(visualRotationExclusions.Length, exclusionInitialRootRelativeRotations.Length);

        for (int i = 0; i < count; i++)
        {
            Transform excluded = visualRotationExclusions[i];
            if (excluded == null) continue;

            excluded.rotation = visualRootBaseWorldRotation * exclusionInitialRootRelativeRotations[i];
        }
    }

    #endregion

    #region Virtual Wheel Physics Yaw

    private void ApplyPhysicsWheelYaw()
    {
        if (wheelPhysicsRoot == null) return;

        Quaternion yawOffset = Quaternion.Euler(0f, currentPhysicsYaw, 0f);
        wheelPhysicsRoot.localRotation = wheelPhysicsInitialLocalRotation * yawOffset;
    }

    #endregion

    #region Public API

    public void ResetOffsetsImmediately()
    {
        currentVisualYaw = 0f;
        currentVisualRoll = 0f;
        currentPhysicsYaw = 0f;
        debugTimer = 0f;

        if (cartVisualRoot != null) cartVisualRoot.localRotation = cartVisualInitialLocalRotation;
        if (wheelPhysicsRoot != null) wheelPhysicsRoot.localRotation = wheelPhysicsInitialLocalRotation;

        ApplyVisualRotationExclusions();
    }

    #endregion

    #region Debug

    private void UpdateDebug(bool isDrifting, float tightness)
    {
        if (!debugDriftVisuals || !isDrifting) return;

        debugTimer += Time.deltaTime;
        if (debugTimer < debugLogInterval) return;

        debugTimer = 0f;

        Debug.Log(
            $"[Drift Visuals] tightness:{tightness:F2} | visualYaw:{currentVisualYaw:F1} | " +
            $"visualRoll:{currentVisualRoll:F1} | physicsYaw:{currentPhysicsYaw:F1}",
            this
        );
    }

    #endregion
}