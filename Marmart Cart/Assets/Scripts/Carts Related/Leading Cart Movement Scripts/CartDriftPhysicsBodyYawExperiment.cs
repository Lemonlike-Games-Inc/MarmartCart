using UnityEngine;

public class CartDriftPhysicsBodyYawExperiment : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CartDriftController driftController;

    [Tooltip("The actual Rigidbody object that also owns the main cart collider/box collider.")]
    [SerializeField] private Rigidbody targetBody;

    [Header("Master Toggle")]
    [SerializeField] private bool enablePhysicsBodyYawOffset = false;

    [Header("Yaw Offset")]
    [SerializeField] private float physicsBodyWideYawAngle = 10f;
    [SerializeField] private float physicsBodyTightYawAngle = 35f;

    [Tooltip("0 = no effect, 1 = full target yaw offset.")]
    [SerializeField, Range(0f, 1f)] private float physicsBodyYawInfluence = 1f;

    [Tooltip("How fast the Rigidbody rotates toward the drift yaw target.")]
    [SerializeField] private float yawFollowSpeed = 8f;

    [Tooltip("Flip this if the physics body angles the wrong direction.")]
    [SerializeField] private bool invertYaw = false;

    [Header("Reference Direction")]
    [Tooltip("If true, body yaw target is based on current velocity/path. If false, it offsets from current body forward.")]
    [SerializeField] private bool useVelocityAsReference = true;

    [SerializeField] private float minSpeedForVelocityReference = 0.5f;

    [Header("Rotation Safety")]
    [Tooltip("If true, target rotation is upright yaw-only. Recommended for first test on flat supermarket floor.")]
    [SerializeField] private bool forceUprightYawOnly = true;

    [Tooltip("When drift ends, rotate back toward velocity/body-forward alignment instead of snapping.")]
    [SerializeField] private bool smoothReturnWhenNotDrifting = true;

    [Tooltip("If false, this script does nothing when not drifting.")]
    [SerializeField] private bool controlRotationWhenNotDrifting = false;

    [Header("Debug")]
    [SerializeField] private bool debugPhysicsBodyYaw = false;

    public float CurrentTargetYawOffset => currentTargetYawOffset;

    private float currentTargetYawOffset = 0f;

    private void Reset()
    {
        targetBody = GetComponentInParent<Rigidbody>();
        driftController = GetComponentInParent<CartDriftController>();
    }

    private void FixedUpdate()
    {
        if (!enablePhysicsBodyYawOffset)
            return;

        if (targetBody == null || driftController == null)
            return;

        UpdatePhysicsBodyYaw();
    }

    private void UpdatePhysicsBodyYaw()
    {
        bool isDrifting = driftController.IsDrifting;

        if (!isDrifting && !controlRotationWhenNotDrifting)
        {
            currentTargetYawOffset = 0f;
            return;
        }

        float targetOffset = 0f;

        if (isDrifting)
        {
            float tightness = Mathf.Clamp01(driftController.CurrentTightness);

            float yawMagnitude = Mathf.Lerp(
                physicsBodyWideYawAngle,
                physicsBodyTightYawAngle,
                tightness
            );

            float sign = invertYaw ? -driftController.DriftSign : driftController.DriftSign;

            targetOffset = sign * yawMagnitude * physicsBodyYawInfluence;
        }

        if (!isDrifting && smoothReturnWhenNotDrifting)
            targetOffset = 0f;

        currentTargetYawOffset = Mathf.Lerp(
            currentTargetYawOffset,
            targetOffset,
            Time.fixedDeltaTime * yawFollowSpeed
        );

        Vector3 referenceForward = GetReferenceForward();

        if (referenceForward.sqrMagnitude < 0.001f)
            return;

        Quaternion offsetRotation = Quaternion.AngleAxis(currentTargetYawOffset, Vector3.up);
        Vector3 targetForward = offsetRotation * referenceForward;

        if (targetForward.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation;

        if (forceUprightYawOnly)
        {
            targetForward = Vector3.ProjectOnPlane(targetForward, Vector3.up).normalized;
            targetRotation = Quaternion.LookRotation(targetForward, Vector3.up);
        }
        else
        {
            targetRotation = Quaternion.LookRotation(targetForward, targetBody.transform.up);
        }

        Quaternion newRotation = Quaternion.Slerp(
            targetBody.rotation,
            targetRotation,
            Time.fixedDeltaTime * yawFollowSpeed
        );

        targetBody.MoveRotation(newRotation);

        if (debugPhysicsBodyYaw && isDrifting)
        {
            Debug.Log(
                $"[Physics Body Yaw] tightness:{driftController.CurrentTightness:F2}, " +
                $"offset:{currentTargetYawOffset:F1}, " +
                $"targetForward:{targetForward}"
            );
        }
    }

    private Vector3 GetReferenceForward()
    {
        if (useVelocityAsReference)
        {
            Vector3 planarVelocity = Vector3.ProjectOnPlane(targetBody.linearVelocity, Vector3.up);

            if (planarVelocity.magnitude >= minSpeedForVelocityReference)
                return planarVelocity.normalized;
        }

        Vector3 forward = Vector3.ProjectOnPlane(targetBody.transform.forward, Vector3.up);

        if (forward.sqrMagnitude > 0.001f)
            return forward.normalized;

        return Vector3.forward;
    }
}
