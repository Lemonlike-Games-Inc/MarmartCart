using TMPro;
using UnityEngine;

public class CartMovementDirectionDebug : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody cartBody;

    [Tooltip("Transform representing the actual physics cart forward.")]
    [SerializeField] private Transform physicsFacing;

    [Tooltip("Optional. The visible/drifted cart model forward.")]
    [SerializeField] private Transform visualFacing;

    [SerializeField] private TMP_Text debugText;


    [Header("Thresholds")]
    [Tooltip("Ignore direction classification below this planar speed.")]
    [Min(0f)]
    [SerializeField] private float minimumSpeed = 0.15f;

    [Tooltip("Optional negative threshold before calling movement BACKWARD.")]
    [Range(-1f, 0f)]
    [SerializeField] private float backwardDotThreshold = -0.05f;


    [Header("Debug - Read Only")]
    [SerializeField] private float planarSpeed;
    [SerializeField] private float physicsDot;
    [SerializeField] private float visualDot;
    [SerializeField] private float physicsAngle;
    [SerializeField] private float visualAngle;

    [SerializeField] private MovementDirection physicsDirection;
    [SerializeField] private MovementDirection visualDirection;


    public enum MovementDirection
    {
        Stopped,
        Forward,
        Sideways,
        Backward
    }


    private void FixedUpdate()
    {
        if (cartBody == null || physicsFacing == null) return;

        Vector3 velocity = Vector3.ProjectOnPlane(cartBody.linearVelocity, Vector3.up);
        planarSpeed = velocity.magnitude;

        if (planarSpeed < minimumSpeed)
        {
            physicsDot = 0f;
            visualDot = 0f;

            physicsAngle = 0f;
            visualAngle = 0f;

            physicsDirection = MovementDirection.Stopped;
            visualDirection = MovementDirection.Stopped;

            UpdateText();
            return;
        }

        Vector3 movementDirection = velocity.normalized;

        Vector3 physicsForward = Vector3.ProjectOnPlane(physicsFacing.forward, Vector3.up).normalized;

        physicsDot = Vector3.Dot(physicsForward, movementDirection);
        physicsAngle = Vector3.SignedAngle(physicsForward, movementDirection, Vector3.up);
        physicsDirection = Classify(physicsDot);

        if (visualFacing != null)
        {
            Vector3 visualForward = Vector3.ProjectOnPlane(visualFacing.forward, Vector3.up).normalized;

            visualDot = Vector3.Dot(visualForward, movementDirection);
            visualAngle = Vector3.SignedAngle(visualForward, movementDirection, Vector3.up);
            visualDirection = Classify(visualDot);
        }

        UpdateText();
    }


    private MovementDirection Classify(float dot)
    {
        if (dot < backwardDotThreshold) return MovementDirection.Backward;

        if (Mathf.Abs(dot) <= 0.1f) return MovementDirection.Sideways;

        return MovementDirection.Forward;
    }


    private void UpdateText()
    {
        if (debugText == null) return;

        debugText.text =
            $"Speed: {planarSpeed:F2} m/s\n" +
            $"\n" +
            $"PHYSICS\n" +
            $"Dot: {physicsDot:F3}\n" +
            $"Angle: {physicsAngle:F1}°\n" +
            $"State: {physicsDirection}\n" +
            $"\n" +
            $"VISUAL\n" +
            $"Dot: {visualDot:F3}\n" +
            $"Angle: {visualAngle:F1}°\n" +
            $"State: {visualDirection}";
    }
}