using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Inspector side of LeadingCartFeedbackRelay. Sits on the Cart Feedback
/// prefab root (nested under Leading Cart Base), so effects are wired inside
/// that prefab only.
///
/// Anchors: the hit anchor is moved to the hit point and turned along the
/// hit direction before the crash events fire. The rear wheel anchors follow
/// the rear wheel models every frame, so effects under them sit on the tyres.
/// </summary>
[DisallowMultipleComponent]
public class CartFeedbackEvents : MonoBehaviour
{
    #region Anchors

    [Header("Anchors")]
    [SerializeField] private Transform hitAnchor;
    [SerializeField] private Transform rearLeftAnchor;
    [SerializeField] private Transform rearRightAnchor;

    #endregion

    #region Inspector Events

    [Header("Crash")]
    [SerializeField] private UnityEvent onCrashed = new UnityEvent();
    [SerializeField] private UnityEvent onCrashedIntoOwnChain = new UnityEvent();
    [SerializeField] private UnityEvent onCrashedIntoRival = new UnityEvent();

    [Tooltip("A rival crashed into this player's leading cart or chain.")]
    [SerializeField] private UnityEvent onCausedCrash = new UnityEvent();

    [Header("Speedup")]
    [SerializeField] private UnityEvent onSpeedupStarted = new UnityEvent();
    [SerializeField] private UnityEvent onSpeedupStopped = new UnityEvent();

    [Tooltip("Cola with Mentos item boost. Separate from the Hype speedup.")]
    [SerializeField] private UnityEvent onColaBoostStarted = new UnityEvent();
    [SerializeField] private UnityEvent onColaBoostStopped = new UnityEvent();

    [Header("Drift (any side)")]
    [SerializeField] private UnityEvent onDriftStarted = new UnityEvent();
    [SerializeField] private UnityEvent onDriftStopped = new UnityEvent();

    [Header("Drift per side (a side switch stops one and starts the other)")]
    [SerializeField] private UnityEvent onDriftLeftStarted = new UnityEvent();
    [SerializeField] private UnityEvent onDriftLeftStopped = new UnityEvent();
    [SerializeField] private UnityEvent onDriftRightStarted = new UnityEvent();
    [SerializeField] private UnityEvent onDriftRightStopped = new UnityEvent();

    #endregion

    #region Runtime

    private LeadingCartFeedbackRelay relay;

    /// <summary>Data and state for scripts placed in the Cart Feedback prefab.</summary>
    public LeadingCartFeedbackRelay Relay => relay;

    public Transform HitAnchor => hitAnchor;
    public Transform RearLeftAnchor => rearLeftAnchor;
    public Transform RearRightAnchor => rearRightAnchor;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        relay = GetComponentInParent<LeadingCartFeedbackRelay>();

        if (relay == null) Debug.LogError("[CartFeedback] No LeadingCartFeedbackRelay above Cart Feedback.", this);
    }

    private void OnEnable()
    {
        if (relay == null) return;

        relay.Crashed += HandleCrashed;
        relay.CausedCrash += HandleCausedCrash;
        relay.SpeedupStarted += HandleSpeedupStarted;
        relay.SpeedupStopped += HandleSpeedupStopped;
        relay.ColaBoostStarted += HandleColaBoostStarted;
        relay.ColaBoostStopped += HandleColaBoostStopped;
        relay.DriftStarted += HandleDriftStarted;
        relay.DriftSideChanged += HandleDriftSideChanged;
        relay.DriftStopped += HandleDriftStopped;
    }

    private void OnDisable()
    {
        if (relay == null) return;

        relay.Crashed -= HandleCrashed;
        relay.CausedCrash -= HandleCausedCrash;
        relay.SpeedupStarted -= HandleSpeedupStarted;
        relay.SpeedupStopped -= HandleSpeedupStopped;
        relay.ColaBoostStarted -= HandleColaBoostStarted;
        relay.ColaBoostStopped -= HandleColaBoostStopped;
        relay.DriftStarted -= HandleDriftStarted;
        relay.DriftSideChanged -= HandleDriftSideChanged;
        relay.DriftStopped -= HandleDriftStopped;
    }

    private void LateUpdate()
    {
        if (relay == null) return;

        Quaternion visualRotation = relay.CartVisualRoot != null ? relay.CartVisualRoot.rotation : transform.rotation;

        FollowWheel(rearLeftAnchor, relay.RearLeftWheel, visualRotation);
        FollowWheel(rearRightAnchor, relay.RearRightWheel, visualRotation);
    }

    private static void FollowWheel(Transform anchor, Transform wheel, Quaternion rotation)
    {
        if (anchor == null || wheel == null) return;

        anchor.SetPositionAndRotation(wheel.position, rotation);
    }

    #endregion

    #region Handlers

    private void HandleCrashed(CartCrashInfo info)
    {
        PlaceHitAnchor(info);

        onCrashed.Invoke();

        if (info.Cause == CartCrashCause.OwnChain) onCrashedIntoOwnChain.Invoke();
        else if (info.IsIntoRival) onCrashedIntoRival.Invoke();
    }

    private void PlaceHitAnchor(CartCrashInfo info)
    {
        if (hitAnchor == null) return;

        Vector3 direction = info.HitDirection.sqrMagnitude > 0.001f ? info.HitDirection : transform.forward;

        hitAnchor.SetPositionAndRotation(info.HitPoint, Quaternion.LookRotation(direction, Vector3.up));
    }

    private void HandleCausedCrash(CartCrashInfo info) => onCausedCrash.Invoke();

    private void HandleSpeedupStarted() => onSpeedupStarted.Invoke();
    private void HandleSpeedupStopped() => onSpeedupStopped.Invoke();

    private void HandleColaBoostStarted() => onColaBoostStarted.Invoke();
    private void HandleColaBoostStopped() => onColaBoostStopped.Invoke();

    private void HandleDriftStarted(CartDriftSide side)
    {
        onDriftStarted.Invoke();
        InvokeSideStarted(side);
    }

    private void HandleDriftSideChanged(CartDriftSide newSide)
    {
        InvokeSideStopped(newSide == CartDriftSide.Left ? CartDriftSide.Right : CartDriftSide.Left);
        InvokeSideStarted(newSide);
    }

    private void HandleDriftStopped(CartDriftSide side, bool interrupted)
    {
        InvokeSideStopped(side);
        onDriftStopped.Invoke();
    }

    private void InvokeSideStarted(CartDriftSide side)
    {
        if (side == CartDriftSide.Left) onDriftLeftStarted.Invoke();
        else if (side == CartDriftSide.Right) onDriftRightStarted.Invoke();
    }

    private void InvokeSideStopped(CartDriftSide side)
    {
        if (side == CartDriftSide.Left) onDriftLeftStopped.Invoke();
        else if (side == CartDriftSide.Right) onDriftRightStopped.Invoke();
    }

    #endregion
}
