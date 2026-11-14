using UnityEngine;

public class CartDriftYawOffsetExperiment : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CartDriftController driftController;

    [Tooltip("Parent that contains the cart/body model only.")]
    [SerializeField] private Transform cartVisualRoot;

    [Tooltip("Parent that contains fake wheel models only.")]
    [SerializeField] private Transform wheelVisualRoot;

    [Tooltip("Optional parent of the virtual/raycast wheel objects. This affects physics/path if enabled.")]
    [SerializeField] private Transform wheelPhysicsRoot;

    [Header("Cart Visual Rotation Exclusions")]
    [SerializeField] private bool enableCartVisualRotationExclusions = true;

    [Tooltip("Objects under Cart Visual Root that should NOT visually yaw/roll with the cart body.")]
    [SerializeField] private Transform[] cartVisualRotationExclusions;
    private Quaternion[] cartExclusionInitialRootRelativeRotations;

    [Header("Cart Visual Toggles")]
    [SerializeField] private bool enableCartVisualYaw = true;
    [SerializeField] private bool enableCartVisualRoll = true;

    [Header("Wheel Visual Toggles")]
    [SerializeField] private bool enableWheelVisualYaw = true;

    [Tooltip("Usually false. Fake wheel models often look better staying visually grounded.")]
    [SerializeField] private bool enableWheelVisualRoll = false;

    [Header("Physics Wheel Yaw Toggle")]
    [Tooltip("Experimental. This rotates the virtual wheel force frame and WILL affect path.")]
    [SerializeField] private bool enablePhysicsWheelYawOffset = false;

    [Header("Visual Yaw")]
    [SerializeField] private float visualWideYawAngle = 6f;
    [SerializeField] private float visualTightYawAngle = 22f;
    [SerializeField] private float visualYawSmoothSpeed = 10f;

    [Tooltip("Flip this if the visual yaw points the wrong way.")]
    [SerializeField] private bool invertVisualYaw = false;

    [Header("Visual Roll / Tilt")]
    [SerializeField] private float visualWideRollAngle = 2f;
    [SerializeField] private float visualTightRollAngle = 7f;
    [SerializeField] private float visualRollSmoothSpeed = 10f;

    [Tooltip("Flip this if the cart leans the wrong way.")]
    [SerializeField] private bool invertVisualRoll = false;

    [Header("Separate Visual Multipliers")]
    [Tooltip("1 = full yaw on cart model.")]
    [SerializeField] private float cartVisualYawMultiplier = 1f;

    [Tooltip("1 = full roll on cart model.")]
    [SerializeField] private float cartVisualRollMultiplier = 1f;

    [Tooltip("1 = fake wheels yaw with the cart. 0 = fake wheels do not yaw.")]
    [SerializeField] private float wheelVisualYawMultiplier = 1f;

    [Tooltip("Usually 0. If 1, fake wheels roll/tilt with the cart body.")]
    [SerializeField] private float wheelVisualRollMultiplier = 0f;

    [Header("Physics Wheel Yaw")]
    [SerializeField] private float physicsWideYawAngle = 2f;
    [SerializeField] private float physicsTightYawAngle = 10f;
    [SerializeField] private float physicsYawSmoothSpeed = 8f;

    [Tooltip("0 = no physics offset, 1 = full physics offset.")]
    [SerializeField, Range(0f, 1f)] private float physicsYawInfluence = 0f;

    [Tooltip("Flip this if physics yaw pushes the cart the wrong way.")]
    [SerializeField] private bool invertPhysicsYaw = false;

    [Header("Exit")]
    [SerializeField] private bool resetWhenNotDrifting = true;

    [Header("Debug")]
    [SerializeField] private bool debugYawOffset = false;

    public float CurrentVisualYaw => currentVisualYaw;
    public float CurrentVisualRoll => currentVisualRoll;
    public float CurrentPhysicsYaw => currentPhysicsYaw;

    private Quaternion cartVisualInitialLocalRotation;
    private Quaternion wheelVisualInitialLocalRotation;
    private Quaternion wheelPhysicsInitialLocalRotation;

    private float currentVisualYaw = 0f;
    private float currentVisualRoll = 0f;
    private float currentPhysicsYaw = 0f;

    private void Awake()
    {
        if (cartVisualRoot != null)
            cartVisualInitialLocalRotation = cartVisualRoot.localRotation;

        if (wheelVisualRoot != null)
            wheelVisualInitialLocalRotation = wheelVisualRoot.localRotation;

        if (wheelPhysicsRoot != null)
            wheelPhysicsInitialLocalRotation = wheelPhysicsRoot.localRotation;

        CacheCartVisualExclusions();
    }

    private void LateUpdate()
    {
        UpdateOffsets();
    }

    private void UpdateOffsets()
    {
        bool isDrifting = driftController != null && driftController.IsDrifting;

        float driftSign = isDrifting ? driftController.DriftSign : 0f;
        float tightness = isDrifting ? Mathf.Clamp01(driftController.CurrentTightness) : 0f;

        float targetVisualYaw = 0f;
        float targetVisualRoll = 0f;
        float targetPhysicsYaw = 0f;

        if (isDrifting || !resetWhenNotDrifting)
        {
            float visualYawMagnitude = Mathf.Lerp(
                visualWideYawAngle,
                visualTightYawAngle,
                tightness
            );

            float visualRollMagnitude = Mathf.Lerp(
                visualWideRollAngle,
                visualTightRollAngle,
                tightness
            );

            float physicsYawMagnitude = Mathf.Lerp(
                physicsWideYawAngle,
                physicsTightYawAngle,
                tightness
            );

            float visualYawSign = invertVisualYaw ? -driftSign : driftSign;
            float visualRollSign = invertVisualRoll ? -driftSign : driftSign;
            float physicsYawSign = invertPhysicsYaw ? -driftSign : driftSign;

            targetVisualYaw = visualYawSign * visualYawMagnitude;
            targetVisualRoll = visualRollSign * visualRollMagnitude;

            targetPhysicsYaw = enablePhysicsWheelYawOffset
                ? physicsYawSign * physicsYawMagnitude * physicsYawInfluence
                : 0f;
        }

        currentVisualYaw = Mathf.Lerp(
            currentVisualYaw,
            targetVisualYaw,
            Time.deltaTime * visualYawSmoothSpeed
        );

        currentVisualRoll = Mathf.Lerp(
            currentVisualRoll,
            targetVisualRoll,
            Time.deltaTime * visualRollSmoothSpeed
        );

        currentPhysicsYaw = Mathf.Lerp(
            currentPhysicsYaw,
            targetPhysicsYaw,
            Time.deltaTime * physicsYawSmoothSpeed
        );

        ApplyCartVisualOffset();
        ApplyWheelVisualOffset();
        ApplyPhysicsWheelOffset();

        if (debugYawOffset && isDrifting)
        {
            Debug.Log(
                $"[Drift Yaw Offset] tightness:{tightness:F2}, " +
                $"visualYaw:{currentVisualYaw:F1}, " +
                $"visualRoll:{currentVisualRoll:F1}, " +
                $"physicsYaw:{currentPhysicsYaw:F1}"
            );
        }
    }

    private void ApplyCartVisualOffset()
    {
        if (cartVisualRoot == null)
            return;

        float yaw = enableCartVisualYaw
            ? currentVisualYaw * cartVisualYawMultiplier
            : 0f;

        float roll = enableCartVisualRoll
            ? currentVisualRoll * cartVisualRollMultiplier
            : 0f;

        Quaternion offset = Quaternion.Euler(
            0f,
            yaw,
            -roll
        );

        cartVisualRoot.localRotation = cartVisualInitialLocalRotation * offset;

        ApplyCartVisualRotationExclusions();
    }

    private void ApplyWheelVisualOffset()
    {
        if (wheelVisualRoot == null)
            return;

        float yaw = enableWheelVisualYaw
            ? currentVisualYaw * wheelVisualYawMultiplier
            : 0f;

        float roll = enableWheelVisualRoll
            ? currentVisualRoll * wheelVisualRollMultiplier
            : 0f;

        Quaternion offset = Quaternion.Euler(
            0f,
            yaw,
            -roll
        );

        wheelVisualRoot.localRotation = wheelVisualInitialLocalRotation * offset;
    }

    private void ApplyPhysicsWheelOffset()
    {
        if (wheelPhysicsRoot == null)
            return;

        Quaternion offset = Quaternion.Euler(
            0f,
            currentPhysicsYaw,
            0f
        );

        wheelPhysicsRoot.localRotation = wheelPhysicsInitialLocalRotation * offset;
    }
    private void CacheCartVisualExclusions()
    {
        if (cartVisualRoot == null || cartVisualRotationExclusions == null)
            return;

        cartExclusionInitialRootRelativeRotations =
            new Quaternion[cartVisualRotationExclusions.Length];

        Quaternion cartVisualRootWorldRotation = cartVisualRoot.rotation;

        for (int i = 0; i < cartVisualRotationExclusions.Length; i++)
        {
            Transform excluded = cartVisualRotationExclusions[i];

            if (excluded == null)
            {
                cartExclusionInitialRootRelativeRotations[i] = Quaternion.identity;
                continue;
            }

            // Store the excluded object's original rotation relative to the cart visual root.
            // Later, we rebuild that rotation using the cart root's non-drift rotation.
            cartExclusionInitialRootRelativeRotations[i] =
                Quaternion.Inverse(cartVisualRootWorldRotation) * excluded.rotation;
        }
    }

    private void ApplyCartVisualRotationExclusions()
    {
        if (!enableCartVisualRotationExclusions)
            return;

        if (cartVisualRoot == null ||
            cartVisualRotationExclusions == null ||
            cartExclusionInitialRootRelativeRotations == null)
            return;

        Quaternion parentWorldRotation = cartVisualRoot.parent != null
            ? cartVisualRoot.parent.rotation
            : Quaternion.identity;

        // This is where the cart visual root would be WITHOUT the drift yaw/roll offset.
        Quaternion cartVisualRootBaseWorldRotation =
            parentWorldRotation * cartVisualInitialLocalRotation;

        int count = Mathf.Min(
            cartVisualRotationExclusions.Length,
            cartExclusionInitialRootRelativeRotations.Length
        );

        for (int i = 0; i < count; i++)
        {
            Transform excluded = cartVisualRotationExclusions[i];

            if (excluded == null)
                continue;

            excluded.rotation =
                cartVisualRootBaseWorldRotation *
                cartExclusionInitialRootRelativeRotations[i];
        }
    }
    public void ResetOffsetsImmediately()
    {
        currentVisualYaw = 0f;
        currentVisualRoll = 0f;
        currentPhysicsYaw = 0f;

        if (cartVisualRoot != null)
            cartVisualRoot.localRotation = cartVisualInitialLocalRotation;

        if (wheelVisualRoot != null)
            wheelVisualRoot.localRotation = wheelVisualInitialLocalRotation;

        if (wheelPhysicsRoot != null)
            wheelPhysicsRoot.localRotation = wheelPhysicsInitialLocalRotation;

        ApplyCartVisualRotationExclusions();
    }
}