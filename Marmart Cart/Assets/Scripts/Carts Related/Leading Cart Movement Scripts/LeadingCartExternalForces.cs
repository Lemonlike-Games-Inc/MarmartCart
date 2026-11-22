using UnityEngine;

/// <summary>
/// The one entry point for outside forces on a leading cart (Magnet pull,
/// later winds, pushes...). Lives on the leading cart root next to its
/// Rigidbody.
///
/// Sources call TryAddAcceleration from FixedUpdate for every physics step
/// they act. The acceleration is flattened, capped per step across all
/// sources, kept from pushing the cart forward past its target speed, and
/// applied at the centre of mass, so it slides the cart without rolling or
/// spinning it. The wheels' grip and drive keep working, so the player can
/// steer and drive against it.
///
/// Outside forces never push a leading cart backward: they can slow it down
/// to Minimum Forward Speed, and sideways slides stay. Driving the cart
/// backward folds the snake's trailing hitch arm around the cart, and the
/// chain then runs into the cart's own front and self-crashes.
///
/// Carts in ghost mode, in a checkout pit or frozen cannot be moved.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class LeadingCartExternalForces : MonoBehaviour
{
    [Header("References (auto-resolved when empty)")]
    [SerializeField] private Rigidbody cartBody;
    [SerializeField] private CartControlScript cartControl;
    [SerializeField] private LeadingCartBattleController battleController;

    [Tooltip("Any one of this cart's wheels; read for the current target speed.")]
    [SerializeField] private LeadingCartBehaviour referenceWheel;

    [Header("Limits")]
    [Tooltip("Largest total outside acceleration (m/s²) in one physics step, from all sources together.")]
    [Min(0f)]
    [SerializeField] private float maximumAcceleration = 60f;

    [Tooltip(
        "On: outside forces never push the cart forward past its current target " +
        "speed, so normal top speed stays the same for every player."
    )]
    [SerializeField] private bool limitForwardToTargetSpeed = true;

    [Tooltip(
        "Outside forces slow the cart no further than this forward speed (m/s) and " +
        "never push it backward. 0 lets them stop the cart but still not reverse it."
    )]
    [Min(0f)]
    [SerializeField] private float minimumForwardSpeed = 3f;

    [Header("Runtime - Read Only")]
    [SerializeField] private Vector3 lastStepAcceleration;
    [SerializeField] private float lastStepTime = -1f;

    private float accumulatedStepTime = -1f;
    private Vector3 accumulatedThisStep;

    public Rigidbody Body => cartBody;
    public CartControlScript CartControl => cartControl;

    /// <summary>False in ghost mode, in a checkout pit, while frozen, or when disabled.</summary>
    public bool CanBeMoved =>
        isActiveAndEnabled &&
        cartBody != null &&
        !cartBody.isKinematic &&
        (cartControl == null || (!cartControl.GetIsInPit() && !cartControl.IsPowerupFrozen)) &&
        (battleController == null || !battleController.IsInGhostMode) &&
        (referenceWheel == null || !referenceWheel.IsPowerupFreezeSuppressed);

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    /// <summary>
    /// Adds a flat acceleration (m/s², mass independent) for the current
    /// physics step. Call it from FixedUpdate each step the force acts.
    /// Returns false when the cart cannot be moved right now.
    /// </summary>
    public bool TryAddAcceleration(Vector3 acceleration)
    {
        if (!CanBeMoved) return false;

        acceleration.y = 0f;

        float stepTime = Time.fixedTime;

        if (!Mathf.Approximately(stepTime, accumulatedStepTime))
        {
            accumulatedStepTime = stepTime;
            accumulatedThisStep = Vector3.zero;
        }

        Vector3 limitedTotal = LimitStepTotal(accumulatedThisStep + acceleration);
        Vector3 added = limitedTotal - accumulatedThisStep;

        if (added.sqrMagnitude <= 0.000001f) return true;

        accumulatedThisStep = limitedTotal;
        cartBody.AddForce(added, ForceMode.Acceleration);

        lastStepAcceleration = accumulatedThisStep;
        lastStepTime = stepTime;
        return true;
    }

    /// <summary>
    /// Caps this step's total and limits its part along the cart's forward
    /// axis: no further back than Minimum Forward Speed allows, and, when
    /// enabled, no further forward than the target speed allows.
    /// </summary>
    private Vector3 LimitStepTotal(Vector3 total)
    {
        total = Vector3.ClampMagnitude(total, maximumAcceleration);

        Vector3 forward = cartBody.transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.000001f) return total;

        forward.Normalize();

        float step = Time.fixedDeltaTime;
        float forwardSpeed = Vector3.Dot(cartBody.linearVelocity, forward);
        float forwardPart = Vector3.Dot(total, forward);

        float lowestForwardPart = -Mathf.Max(0f, forwardSpeed - minimumForwardSpeed) / step;
        float highestForwardPart = limitForwardToTargetSpeed && referenceWheel != null
            ? Mathf.Max(0f, referenceWheel.TargetSpeed - forwardSpeed) / step
            : float.PositiveInfinity;

        float limitedForwardPart = Mathf.Clamp(forwardPart, lowestForwardPart, highestForwardPart);
        return total + forward * (limitedForwardPart - forwardPart);
    }

    private void ResolveReferences()
    {
        if (cartBody == null) cartBody = GetComponent<Rigidbody>();
        if (cartControl == null) cartControl = GetComponentInChildren<CartControlScript>(true);
        if (battleController == null) battleController = GetComponentInChildren<LeadingCartBattleController>(true);
        if (referenceWheel == null) referenceWheel = GetComponentInChildren<LeadingCartBehaviour>(true);
    }
}
