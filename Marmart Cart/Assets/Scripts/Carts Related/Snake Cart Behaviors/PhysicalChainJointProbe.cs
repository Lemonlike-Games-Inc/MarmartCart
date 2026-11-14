using UnityEngine;

/// <summary>
/// Isolated physical trailing hinge used as the normal snake path source.
///
/// Real RearHitch -> Kinematic proxy -> Free HingeJoint -> Dynamic arm -> Probe
///
/// The real leading-cart Rigidbody is never physically connected to this
/// simulation. The isolated hinge only provides a natural trailing path source.
///
/// RearHitch is assigned at runtime by SnakeCartManager after the leading cart
/// prefab has been spawned.
/// </summary>
[DisallowMultipleComponent]
public class PhysicalChainJointProbe : MonoBehaviour
{
    #region Physical Arm Settings

    [Header("Physical Arm")]
    [Min(0.1f)]
    [SerializeField] private float armLength = 1.5f;

    [Min(0.01f)]
    [SerializeField] private float armMass = 1f;

    [Min(0f)]
    [SerializeField] private float linearDamping = 0.2f;

    [Min(0f)]
    [SerializeField] private float angularDamping = 1f;

    [Min(1f)]
    [SerializeField] private float maxArmAngularVelocity = 25f;

    #endregion

    #region Swing Limits

    [Header("Optional Swing Limits")]
    [SerializeField] private bool limitSwingAngle = false;

    [Range(0f, 180f)]
    [SerializeField] private float maxSwingAngle = 120f;

    #endregion

    #region Auto Recenter

    [Header("Auto Recenter")]
    [SerializeField] private bool enableAutoRecenter = true;

    [Min(0f)]
    [SerializeField] private float minSpeedForRecenter = 2f;

    [Min(0f)]
    [SerializeField] private float maxYawRateForRecenter = 20f;

    [Min(0f)]
    [SerializeField] private float recenterDeadAngle = 1.5f;

    [Min(0f)]
    [SerializeField] private float recenterSpring = 250f;

    [Min(0f)]
    [SerializeField] private float recenterDamper = 25f;

    #endregion

    #region Debug Visuals

    [Header("Debug Visuals")]
    [SerializeField] private bool showPhysicalArm = true;
    [SerializeField] private bool showProbeSphere = true;

    [Min(0.01f)]
    [SerializeField] private float probeSphereSize = 0.25f;

    #endregion

    #region Runtime Debug

    [Header("Runtime Debug - Read Only")]
    [SerializeField] private float currentHingeAngle;
    [SerializeField] private float currentDistanceToHitch;
    [SerializeField] private float currentProbeSpeed;

    [Header("Recenter Debug - Read Only")]
    [SerializeField] private bool isRecentering;
    [SerializeField] private float currentHitchSpeed;
    [SerializeField] private float currentHitchYawRate;

    #endregion

    #region Runtime

    private Transform leaderRearHitch;

    private GameObject proxyObject;
    private Rigidbody proxyBody;

    private GameObject armObject;
    private Rigidbody armBody;

    private HingeJoint hingeJoint;
    private Transform probePoint;

    private Vector3 previousProbePosition;
    private Vector3 previousHitchPosition;
    private Vector3 previousHitchForward;

    #endregion

    #region Public State

    public Transform ProbeTransform => probePoint;
    public Transform LeaderRearHitch => leaderRearHitch;

    public Vector3 ProbePosition =>
        probePoint != null
            ? probePoint.position
            : Vector3.zero;

    public Vector3 ProbeForward
    {
        get
        {
            if (probePoint == null) return Vector3.forward;

            Vector3 forward = Vector3.ProjectOnPlane(
                probePoint.forward,
                Vector3.up
            );

            return forward.sqrMagnitude > 0.0001f
                ? forward.normalized
                : Vector3.forward;
        }
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Creates or rebuilds the isolated hinge at the current RearHitch pose.
    /// MoveBackward recovery intentionally uses the same initialization path.
    /// </summary>
    public void Initialize(Transform rearHitch)
    {
        if (rearHitch == null)
        {
            Debug.LogError("[PhysicalChainJointProbe] Cannot initialize because RearHitch is null.", this);
            return;
        }

        leaderRearHitch = rearHitch;
        CreateSimulation();
    }

    #endregion

    #region Simulation Creation

    private void CreateSimulation()
    {
        CleanupSimulation();

        if (leaderRearHitch == null) return;

        CreateKinematicProxy();
        CreateTrailingArm();
        CreateHinge();
        CreateProbeVisual();

        previousProbePosition = probePoint != null
            ? probePoint.position
            : leaderRearHitch.position;

        previousHitchPosition = leaderRearHitch.position;

        Vector3 initialForward = Vector3.ProjectOnPlane(
            leaderRearHitch.forward,
            Vector3.up
        );

        previousHitchForward = initialForward.sqrMagnitude > 0.0001f
            ? initialForward.normalized
            : Vector3.forward;

        ResetRuntimeDebug();
    }

    private void CreateKinematicProxy()
    {
        proxyObject = new GameObject("PhysicalJoint_HitchProxy");

        proxyObject.transform.SetPositionAndRotation(
            leaderRearHitch.position,
            leaderRearHitch.rotation
        );

        proxyBody = proxyObject.AddComponent<Rigidbody>();

        proxyBody.isKinematic = true;
        proxyBody.useGravity = false;
        proxyBody.detectCollisions = false;
        proxyBody.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void CreateTrailingArm()
    {
        armObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        armObject.name = "PhysicalJoint_TrailingArm";

        Vector3 forward = Vector3.ProjectOnPlane(
            leaderRearHitch.forward,
            Vector3.up
        );

        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        Quaternion startingRotation = Quaternion.LookRotation(
            forward,
            Vector3.up
        );

        Vector3 startingPosition =
            leaderRearHitch.position -
            forward * (armLength * 0.5f);

        armObject.transform.SetPositionAndRotation(
            startingPosition,
            startingRotation
        );

        armObject.transform.localScale =
            new Vector3(0.08f, 0.08f, armLength);

        MeshRenderer renderer = armObject.GetComponent<MeshRenderer>();

        if (renderer != null)
        {
            renderer.enabled = showPhysicalArm;
        }

        Collider armCollider = armObject.GetComponent<Collider>();

        if (armCollider != null)
        {
            Destroy(armCollider);
        }

        armBody = armObject.AddComponent<Rigidbody>();

        armBody.mass = armMass;
        armBody.useGravity = false;
        armBody.detectCollisions = false;
        armBody.linearDamping = linearDamping;
        armBody.angularDamping = angularDamping;
        armBody.maxAngularVelocity = maxArmAngularVelocity;
        armBody.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void CreateHinge()
    {
        hingeJoint = armObject.AddComponent<HingeJoint>();

        hingeJoint.connectedBody = proxyBody;
        hingeJoint.autoConfigureConnectedAnchor = false;

        hingeJoint.anchor = new Vector3(0f, 0f, 0.5f);
        hingeJoint.connectedAnchor = Vector3.zero;
        hingeJoint.axis = Vector3.up;

        hingeJoint.useMotor = false;
        hingeJoint.useSpring = false;
        hingeJoint.enableCollision = false;

        if (!limitSwingAngle)
        {
            hingeJoint.useLimits = false;
            return;
        }

        JointLimits limits = hingeJoint.limits;

        limits.min = -maxSwingAngle;
        limits.max = maxSwingAngle;
        limits.bounciness = 0f;

        hingeJoint.limits = limits;
        hingeJoint.useLimits = true;
    }

    private void CreateProbeVisual()
    {
        GameObject probeSphere =
            GameObject.CreatePrimitive(PrimitiveType.Sphere);

        probeSphere.name = "PhysicalJoint_Probe";

        Collider probeCollider =
            probeSphere.GetComponent<Collider>();

        if (probeCollider != null)
        {
            Destroy(probeCollider);
        }

        probeSphere.transform.SetParent(
            armObject.transform,
            false
        );

        probeSphere.transform.localPosition =
            new Vector3(0f, 0f, -0.5f);

        float safeArmLength =
            Mathf.Max(0.01f, armLength);

        probeSphere.transform.localScale =
            new Vector3(
                probeSphereSize / 0.08f,
                probeSphereSize / 0.08f,
                probeSphereSize / safeArmLength
            );

        MeshRenderer renderer =
            probeSphere.GetComponent<MeshRenderer>();

        if (renderer != null)
        {
            renderer.enabled = showProbeSphere;
        }

        probePoint = probeSphere.transform;
    }

    private void ResetRuntimeDebug()
    {
        currentHingeAngle = 0f;
        currentDistanceToHitch = armLength;
        currentProbeSpeed = 0f;

        isRecentering = false;
        currentHitchSpeed = 0f;
        currentHitchYawRate = 0f;
    }

    #endregion

    #region Physics Update

    private void FixedUpdate()
    {
        if (leaderRearHitch == null ||
            proxyBody == null ||
            hingeJoint == null)
        {
            return;
        }

        proxyBody.MovePosition(leaderRearHitch.position);
        proxyBody.MoveRotation(leaderRearHitch.rotation);

        currentHingeAngle = hingeJoint.angle;

        UpdateHitchMotionState();
        UpdateAutoRecenter();
        UpdateProbeDebug();
    }

    private void UpdateHitchMotionState()
    {
        float dt =
            Mathf.Max(Time.fixedDeltaTime, 0.00001f);

        Vector3 currentPosition =
            leaderRearHitch.position;

        Vector3 positionDelta =
            Vector3.ProjectOnPlane(
                currentPosition - previousHitchPosition,
                Vector3.up
            );

        currentHitchSpeed =
            positionDelta.magnitude / dt;

        Vector3 currentForward =
            Vector3.ProjectOnPlane(
                leaderRearHitch.forward,
                Vector3.up
            );

        if (currentForward.sqrMagnitude > 0.0001f)
        {
            currentForward.Normalize();

            currentHitchYawRate =
                Mathf.Abs(
                    Vector3.SignedAngle(
                        previousHitchForward,
                        currentForward,
                        Vector3.up
                    )
                ) / dt;

            previousHitchForward = currentForward;
        }
        else
        {
            currentHitchYawRate = 0f;
        }

        previousHitchPosition = currentPosition;
    }

    private void UpdateAutoRecenter()
    {
        if (hingeJoint == null) return;

        if (!enableAutoRecenter)
        {
            hingeJoint.useSpring = false;
            isRecentering = false;
            return;
        }

        bool movingEnough =
            currentHitchSpeed >= minSpeedForRecenter;

        bool movingMostlyStraight =
            currentHitchYawRate <= maxYawRateForRecenter;

        bool hingeNeedsCorrection =
            Mathf.Abs(currentHingeAngle) > recenterDeadAngle;

        isRecentering =
            movingEnough &&
            movingMostlyStraight &&
            hingeNeedsCorrection;

        if (!isRecentering)
        {
            hingeJoint.useSpring = false;
            return;
        }

        JointSpring spring = hingeJoint.spring;

        spring.spring = recenterSpring;
        spring.damper = recenterDamper;
        spring.targetPosition = 0f;

        hingeJoint.spring = spring;
        hingeJoint.useSpring = true;
    }

    private void UpdateProbeDebug()
    {
        if (probePoint == null || leaderRearHitch == null) return;

        currentDistanceToHitch =
            Vector3.Distance(
                leaderRearHitch.position,
                probePoint.position
            );

        Vector3 probeDelta =
            Vector3.ProjectOnPlane(
                probePoint.position - previousProbePosition,
                Vector3.up
            );

        currentProbeSpeed =
            probeDelta.magnitude /
            Mathf.Max(Time.fixedDeltaTime, 0.00001f);

        previousProbePosition =
            probePoint.position;
    }

    #endregion

    #region Cleanup / Validation

    private void CleanupSimulation()
    {
        if (armObject != null) Destroy(armObject);
        if (proxyObject != null) Destroy(proxyObject);

        armObject = null;
        proxyObject = null;

        armBody = null;
        proxyBody = null;

        hingeJoint = null;
        probePoint = null;
    }

    private void OnDestroy()
    {
        CleanupSimulation();
    }

    private void OnValidate()
    {
        armLength = Mathf.Max(0.1f, armLength);
        armMass = Mathf.Max(0.01f, armMass);
        linearDamping = Mathf.Max(0f, linearDamping);
        angularDamping = Mathf.Max(0f, angularDamping);
        maxArmAngularVelocity = Mathf.Max(1f, maxArmAngularVelocity);

        maxSwingAngle = Mathf.Clamp(maxSwingAngle, 0f, 180f);

        minSpeedForRecenter = Mathf.Max(0f, minSpeedForRecenter);
        maxYawRateForRecenter = Mathf.Max(0f, maxYawRateForRecenter);
        recenterDeadAngle = Mathf.Max(0f, recenterDeadAngle);
        recenterSpring = Mathf.Max(0f, recenterSpring);
        recenterDamper = Mathf.Max(0f, recenterDamper);

        probeSphereSize = Mathf.Max(0.01f, probeSphereSize);
    }

    #endregion
}
