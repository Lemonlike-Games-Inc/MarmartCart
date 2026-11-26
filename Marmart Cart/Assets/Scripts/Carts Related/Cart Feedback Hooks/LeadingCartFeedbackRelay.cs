using System;
using UnityEngine;

public enum CartDriftSide
{
    None,
    Left,
    Right
}

/// <summary>
/// Presentation-only hook point for leading-cart feedback (crash, speedup,
/// drift). Lives on the Leading Cart Base root and turns the gameplay
/// controllers' events into clean, once-per-change C# events. It never
/// changes gameplay.
///
/// Inspector wiring lives in CartFeedbackEvents on the nested Cart Feedback
/// prefab, so effects can be wired without editing Leading Cart Base.
/// </summary>
[DisallowMultipleComponent]
public class LeadingCartFeedbackRelay : MonoBehaviour
{
    #region References

    [Header("Gameplay Sources")]
    [SerializeField] private LeadingCartBattleController battleController;
    [SerializeField] private CartControlScript cartControl;
    [SerializeField] private CartDriftController driftController;

    [Header("Visual Sources")]
    [Tooltip("Cart Model: the visual root that yaws and leans with drift.")]
    [SerializeField] private Transform cartVisualRoot;

    [SerializeField] private Transform rearLeftWheel;
    [SerializeField] private Transform rearRightWheel;

    [Header("Debug")]
    [SerializeField] private bool logEvents = true;

    #endregion

    #region Runtime

    private SnakeCartManager snakeCartManager;
    private bool isSpeedingUp;
    private bool isColaBoostActive;
    private CartDriftSide driftSide;

    public int PlayerIndex
    {
        get
        {
            if (snakeCartManager == null) snakeCartManager = GetComponentInParent<SnakeCartManager>();
            return snakeCartManager != null ? snakeCartManager.GetPlayerId() : 0;
        }
    }

    public bool IsSpeedingUp => isSpeedingUp;
    public bool IsColaBoostActive => isColaBoostActive;
    public bool IsDrifting => driftSide != CartDriftSide.None;
    public CartDriftSide DriftSide => driftSide;

    /// <summary>0 = wide, 1 = tight. 0 while not drifting.</summary>
    public float DriftTightness => IsDrifting && driftController != null ? driftController.CurrentTightness : 0f;

    public Transform CartVisualRoot => cartVisualRoot;
    public Transform RearLeftWheel => rearLeftWheel;
    public Transform RearRightWheel => rearRightWheel;

    #endregion

    #region Events

    public event Action<CartCrashInfo> Crashed;

    /// <summary>This player's leading cart or chain was hit by a rival who crashed.</summary>
    public event Action<CartCrashInfo> CausedCrash;

    public event Action SpeedupStarted;
    public event Action SpeedupStopped;

    public event Action ColaBoostStarted;
    public event Action ColaBoostStopped;

    public event Action<CartDriftSide> DriftStarted;

    /// <summary>Argument: the new side.</summary>
    public event Action<CartDriftSide> DriftSideChanged;

    /// <summary>Arguments: the side that stopped, and whether it was interrupted (wall, stall, item) rather than released.</summary>
    public event Action<CartDriftSide, bool> DriftStopped;

    #endregion

    #region Camera (this player only)

    /// <summary>Shakes this player's camera. Amplitude in world units.</summary>
    public void ShakeCamera(float amplitude, float duration)
    {
        CameraManager cameraManager = CameraManager.Instance;
        if (cameraManager != null) cameraManager.ShakePlayerCamera(PlayerIndex, amplitude, duration);
    }

    /// <summary>
    /// Adds a zoom offset (orthographic size, positive = zoom out) to this
    /// player's camera. Change handle.SizeOffset to animate it; always remove it.
    /// Returns null outside a gameplay scene.
    /// </summary>
    public CameraZoomHandle AddCameraZoom(float sizeOffset)
    {
        CameraManager cameraManager = CameraManager.Instance;
        return cameraManager != null ? cameraManager.AddZoomOffset(PlayerIndex, sizeOffset) : null;
    }

    public void RemoveCameraZoom(CameraZoomHandle handle)
    {
        CameraManager cameraManager = CameraManager.Instance;
        if (cameraManager != null) cameraManager.RemoveZoomOffset(handle);
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (battleController == null) battleController = GetComponentInChildren<LeadingCartBattleController>(true);
        if (cartControl == null) cartControl = GetComponentInChildren<CartControlScript>(true);
        if (driftController == null) driftController = GetComponentInChildren<CartDriftController>(true);

        if (battleController == null) Debug.LogError("[CartFeedback] LeadingCartBattleController not found.", this);
        if (cartControl == null) Debug.LogError("[CartFeedback] CartControlScript not found.", this);
        if (driftController == null) Debug.LogError("[CartFeedback] CartDriftController not found.", this);
        if (rearLeftWheel == null || rearRightWheel == null) Debug.LogWarning("[CartFeedback] Rear wheel references are not assigned.", this);
    }

    private void OnEnable()
    {
        if (battleController != null)
        {
            battleController.OnCrashed += HandleCrashed;
            battleController.OnCausedCrash += HandleCausedCrash;
        }

        if (cartControl != null)
        {
            cartControl.OnSpeedupHeld += HandleSpeedupHeld;
            cartControl.OnColaMentosBoostChanged += HandleColaBoostChanged;
        }

        if (driftController != null)
        {
            driftController.OnDriftStarted += HandleDriftStarted;
            driftController.OnDriftSideChanged += HandleDriftSideChanged;
            driftController.OnDriftEndedClean += HandleDriftEndedClean;
            driftController.OnDriftInterrupted += HandleDriftInterrupted;
        }
    }

    private void OnDisable()
    {
        if (battleController != null)
        {
            battleController.OnCrashed -= HandleCrashed;
            battleController.OnCausedCrash -= HandleCausedCrash;
        }

        if (cartControl != null)
        {
            cartControl.OnSpeedupHeld -= HandleSpeedupHeld;
            cartControl.OnColaMentosBoostChanged -= HandleColaBoostChanged;
        }

        if (driftController != null)
        {
            driftController.OnDriftStarted -= HandleDriftStarted;
            driftController.OnDriftSideChanged -= HandleDriftSideChanged;
            driftController.OnDriftEndedClean -= HandleDriftEndedClean;
            driftController.OnDriftInterrupted -= HandleDriftInterrupted;
        }

        // Never leave a looping effect on when the cart is disabled or torn down.
        SetSpeedup(false);
        SetColaBoost(false);
        StopDrift(true);
    }

    #endregion

    #region Crash

    private void HandleCrashed(CartCrashInfo info)
    {
        if (logEvents)
        {
            Debug.Log(
                $"[CartFeedback] P{info.CrashedPlayerIndex} crashed: {info.Cause}" +
                (info.OtherPlayerIndex > 0 ? $" (P{info.OtherPlayerIndex})" : "") +
                $", lost {info.CartsLost}, speed {info.SpeedBeforeHit:F1}",
                this
            );
        }

        Crashed?.Invoke(info);
    }

    private void HandleCausedCrash(CartCrashInfo info)
    {
        if (logEvents)
        {
            Debug.Log($"[CartFeedback] P{PlayerIndex} caused P{info.CrashedPlayerIndex} to crash ({info.Cause})", this);
        }

        CausedCrash?.Invoke(info);
    }

    #endregion

    #region Speedup

    private void HandleSpeedupHeld(bool held) => SetSpeedup(held);

    private void SetSpeedup(bool active)
    {
        if (isSpeedingUp == active) return;

        isSpeedingUp = active;

        if (logEvents) Debug.Log($"[CartFeedback] P{PlayerIndex} speedup {(active ? "started" : "stopped")}", this);

        if (active) SpeedupStarted?.Invoke();
        else SpeedupStopped?.Invoke();
    }

    private void HandleColaBoostChanged(bool active, float targetSpeed) => SetColaBoost(active);

    private void SetColaBoost(bool active)
    {
        if (isColaBoostActive == active) return;

        isColaBoostActive = active;

        if (logEvents) Debug.Log($"[CartFeedback] P{PlayerIndex} cola boost {(active ? "started" : "stopped")}", this);

        if (active) ColaBoostStarted?.Invoke();
        else ColaBoostStopped?.Invoke();
    }

    #endregion

    #region Drift

    private void HandleDriftStarted()
    {
        CartDriftSide side = ToSide(driftController.DriftSign);

        if (side == CartDriftSide.None) return;
        if (IsDrifting) StopDrift(true);

        driftSide = side;

        if (logEvents) Debug.Log($"[CartFeedback] P{PlayerIndex} drift started {side}", this);

        DriftStarted?.Invoke(side);
    }

    private void HandleDriftSideChanged(float newSign)
    {
        CartDriftSide side = ToSide(newSign);

        if (!IsDrifting || side == CartDriftSide.None || side == driftSide) return;

        driftSide = side;

        if (logEvents) Debug.Log($"[CartFeedback] P{PlayerIndex} drift side changed to {side}", this);

        DriftSideChanged?.Invoke(side);
    }

    private void HandleDriftEndedClean(string reason) => StopDrift(false);

    private void HandleDriftInterrupted(string reason) => StopDrift(true);

    // The drift controller also reports ends from the armed state; only a real drift raises Stopped.
    private void StopDrift(bool interrupted)
    {
        if (!IsDrifting) return;

        CartDriftSide side = driftSide;
        driftSide = CartDriftSide.None;

        if (logEvents) Debug.Log($"[CartFeedback] P{PlayerIndex} drift stopped {side}{(interrupted ? " (interrupted)" : "")}", this);

        DriftStopped?.Invoke(side, interrupted);
    }

    private static CartDriftSide ToSide(float driftSign)
    {
        if (driftSign < 0f) return CartDriftSide.Left;
        if (driftSign > 0f) return CartDriftSide.Right;
        return CartDriftSide.None;
    }

    #endregion
}
