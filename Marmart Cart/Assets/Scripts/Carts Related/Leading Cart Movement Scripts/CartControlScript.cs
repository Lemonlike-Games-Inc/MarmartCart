using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using UnityEngine.Serialization;

/// <summary>
/// Central input/state gateway for the leading cart.
/// Reads the assigned player's Input System actions and exposes movement state/events.
/// It does not directly move the Rigidbody.
/// </summary>
public class CartControlScript : MonoBehaviour
{
    #region Input Runtime

    private InputSystem_Actions _inputActions;
    private InputUser user;

    private Vector2 _inputVector;
    private Vector3 _input;

    public Vector3 desiredDirection { get; private set; }
    public Vector2 MoveInput => powerupFrozen ? Vector2.zero : _inputVector;

    #endregion

    #region General Control State

    [Header("Control State")]
    [SerializeField] private bool controllable = true;
    [SerializeField] private bool isInPit = false;

    [Header("Power-up Freeze - Runtime Read Only")]
    [SerializeField] private bool powerupFrozen;

    public bool IsPowerupFrozen => powerupFrozen;
    public event System.Action<bool> OnPowerupFrozenChanged;

    #endregion

    #region Drift

    [Header("Drift")]
    [FormerlySerializedAs("canDrift")]
    [SerializeField] private bool allowDrift = true;

    [FormerlySerializedAs("prototypeSteerDeadzone")]
    [SerializeField] private float steerDeadzone = 0.15f;

    [Header("Drift / Speedup Mutual Override")]
    [SerializeField] private CartDriftController driftController;
    [SerializeField] private bool enableDriftSpeedupOverride = true;

    private bool isDriftHeld;

    public float GetSteerInput()
    {
        if (powerupFrozen) return 0f;
        if (Mathf.Abs(_inputVector.x) < steerDeadzone) return 0f;
        return Mathf.Clamp(_inputVector.x, -1f, 1f);
    }

    #endregion

    #region Aiming

    private Vector2 _aimInputVector;
    private Vector3 _aimDirection;

    [Header("Aim Input")]
    [SerializeField] private bool canAim = false;

    [Tooltip(
        "Keyboard/mouse test fallback only. Mouse distance from the current screen center " +
        "reaches full analog magnitude at this many pixels. Controller aim already supplies 0..1 magnitude directly."
    )]
    [Min(1f)]
    [SerializeField] private float keyboardAimFullMagnitudePixels = 300f;

    /// <summary>
    /// Unnormalized 2D aim input after device filtering and a unit-circle clamp.
    /// Targeting code must use this value so stick magnitude can control range.
    /// </summary>
    public Vector2 RawAimInput => _aimInputVector;
    public float AimInputMagnitude => Mathf.Clamp01(_aimInputVector.magnitude);

    /// <summary>
    /// Compatibility world-space aim vector. Its magnitude is intentionally
    /// preserved; consumers that need only a direction must normalize locally.
    /// </summary>
    public Vector3 AimDirection => _aimDirection;

    #endregion

    #region Hype / Speedup

    [Header("Hype Resource")]
    [Min(0.01f)]
    [SerializeField] private float maxHype = 100f;

    [Min(0f)]
    [SerializeField] private float startingHype = 50f;

    [Tooltip("Literal Hype consumed per second while Speedup is active before the cart-count multiplier.")]
    [Min(0f)]
    [SerializeField] private float baseHypeBurnPerSecond = 100f;

    [Header("Hype Burn / Owned Cart Penalty")]
    [Tooltip("Owned follower carts at or below this amount do not increase Hype burn. The leading cart is not counted.")]
    [Min(0)]
    [SerializeField] private int safeOwnedFollowerCount = 2;

    [Tooltip(
        "Burn multiplier used by the FIRST owned follower above the safe amount. " +
        "Example: 1.5 means the first penalized cart immediately burns Hype at 150% of base rate."
    )]
    [Min(1f)]
    [SerializeField] private float firstPenalizedHypeBurnMultiplier = 1.5f;

    [Tooltip(
        "Additional multiplier added by EACH penalized owned follower AFTER the first one. " +
        "Example: First Penalty = 1.5 and Increase = 0.15 gives 1.50x, 1.65x, 1.80x..."
    )]
    [Min(0f)]
    [SerializeField] private float hypeBurnMultiplierIncreasePerOwnedCart = 0.15f;

    [Tooltip("Maximum multiplier that owned follower carts may apply to Hype burn.")]
    [Min(1f)]
    [SerializeField] private float maxHypeBurnMultiplier = 2.5f;

    [Tooltip("Owned follower source. Includes both active and compact pending carts.")]
    [SerializeField] private SnakeCartManager snakeCartManager;

    [Header("Speedup")]
    [SerializeField] private bool canSpeedup = true;

    [Header("Hype Runtime - Read Only")]
    [SerializeField] private float currentHype;
    [SerializeField] private int hypeBurnOwnedFollowerCount;

    private bool isSpeedingUp;

    public float CurrentHype => currentHype;
    public float MaxHype => maxHype;
    public float HypeNormalized => maxHype > 0.01f ? Mathf.Clamp01(currentHype / maxHype) : 0f;
    public int HypeBurnOwnedFollowerCount => hypeBurnOwnedFollowerCount;
    public int SafeOwnedFollowerCount => safeOwnedFollowerCount;
    public int PenalizedOwnedFollowerCount => Mathf.Max(0, hypeBurnOwnedFollowerCount - safeOwnedFollowerCount);
    public float FirstPenalizedHypeBurnMultiplier => firstPenalizedHypeBurnMultiplier;

    public float CurrentHypeBurnMultiplier
    {
        get
        {
            int penalizedCount = PenalizedOwnedFollowerCount;
            if (penalizedCount <= 0) return 1f;

            float multiplier =
                firstPenalizedHypeBurnMultiplier +
                (penalizedCount - 1) * hypeBurnMultiplierIncreasePerOwnedCart;

            return Mathf.Min(maxHypeBurnMultiplier, multiplier);
        }
    }

    public float CurrentHypeBurnPerSecond => baseHypeBurnPerSecond * CurrentHypeBurnMultiplier;

    public event System.Action<float, float> OnHypeChanged;
    public event System.Action<float> OnHypeBurnRateChanged;

    [Header("Cola Mentos Boost - Runtime Read Only")]
    [SerializeField] private bool colaMentosBoostActive;
    [SerializeField] private float colaMentosTargetSpeed;

    public bool IsColaMentosBoostActive => colaMentosBoostActive;
    public float ColaMentosTargetSpeed => colaMentosTargetSpeed;
    public event System.Action<bool, float> OnColaMentosBoostChanged;

    #endregion

    #region Move Backward

    [Header("Move Backward")]
    [SerializeField] private bool canMoveBackward = false;

    public bool CanMoveBackward => canMoveBackward && !powerupFrozen;
    public event System.Action<bool> OnCanMoveBackwardChanged;

    #endregion

    #region Powerup / Checkout

    [Header("Powerup")]
    [SerializeField] private bool canActivatePowerUp = false;

    [Tooltip(
        "Temporary migration fallback for the legacy power-up system. " +
        "The new PlayerPowerupController receives OnPowerupUsePressed instead."
    )]
    [SerializeField] private PowerupsManager powerupsManager;

    private CheckOutManager activeCheckoutManager;

    #endregion

    #region Input Events

    public System.Action OnTutorialPrev;
    public System.Action OnTutorialNext;

    public System.Action OnMoveBackwardPressed;
    public System.Action OnCheckoutReleased;

    /// <summary>
    /// Raised after the Activate Power-up action passes this input gateway.
    /// PlayerPowerupController owns all inventory and blocker validation.
    /// </summary>
    public event System.Action OnPowerupUsePressed;

    /// <summary>
    /// Raised only when the stored raw aim value actually changes.
    /// </summary>
    public event System.Action<Vector2> OnAimInputChanged;

    // Kept for existing feedback listeners during the migration.
    public System.Action OnShootPressed;

    public System.Action<bool> OnMoveHeld;
    public System.Action<bool> OnAimHeld;
    public System.Action<bool> OnSpeedupHeld;

    #endregion

    #region Initialization

    public void InitializeWithDevice(InputDevice device)
    {
        _inputActions = new InputSystem_Actions();

        user = InputUser.CreateUserWithoutPairedDevices();
        user.AssociateActionsWithUser(_inputActions);
        InputUser.PerformPairingWithDevice(device, user);

        BindControllerActions(device);
        _inputActions.Enable();
    }

    public void InitializeWithKeyboard()
    {
        _inputActions = new InputSystem_Actions();

        user = InputUser.CreateUserWithoutPairedDevices();
        user.AssociateActionsWithUser(_inputActions);
        InputUser.PerformPairingWithDevice(Keyboard.current, user);

        BindKeyboardActions();
        _inputActions.Enable();
    }

    private void BindControllerActions(InputDevice device)
    {
        _inputActions.Player.Move.performed += ctx =>
        {
            if (ctx.control.device == device) _inputVector = ctx.ReadValue<Vector2>();
        };

        _inputActions.Player.Move.canceled += ctx =>
        {
            if (ctx.control.device == device) _inputVector = Vector2.zero;
        };

        _inputActions.Player.Drift.performed += ctx =>
        {
            if (ctx.control.device == device && CanDrift()) HandleDriftPressed();
        };

        _inputActions.Player.Drift.canceled += ctx =>
        {
            if (ctx.control.device == device) HandleDriftReleased();
        };

        _inputActions.Player.Aim.performed += ctx =>
        {
            if (ctx.control.device == device && GetCanAim())
            {
                SetAimInput(ctx.ReadValue<Vector2>());
            }
        };

        _inputActions.Player.Aim.canceled += ctx =>
        {
            if (ctx.control.device == device)
            {
                SetAimInput(Vector2.zero);
            }
        };

        _inputActions.Player.Speedup.performed += ctx =>
        {
            if (ctx.control.device == device) HandleSpeedupPressed();
        };

        _inputActions.Player.Speedup.canceled += ctx =>
        {
            if (ctx.control.device == device) HandleSpeedupReleased();
        };

        _inputActions.Player.ActivatePowerUp.performed += ctx =>
        {
            if (ctx.control.device == device && GetCanActivatePowerUp())
            {
                ActivatePowerUp();
            }
        };

        _inputActions.Player.MoveBackward.performed += ctx =>
        {
            if (ctx.control.device == device && CanMoveBackward)
            {
                SetCanMoveBackward(false);
                OnMoveBackwardPressed?.Invoke();
            }
        };

        _inputActions.Player.CheckOut.performed += ctx =>
        {
            if (ctx.control.device == device && activeCheckoutManager != null)
            {
                activeCheckoutManager.TryCheckoutCart();
                OnCheckoutReleased?.Invoke();
            }
        };


        _inputActions.Player.TutorialPrev.performed += ctx =>
        {
            if (ctx.control.device == device) OnTutorialPrev?.Invoke();
        };

        _inputActions.Player.TutorialNext.performed += ctx =>
        {
            if (ctx.control.device == device) OnTutorialNext?.Invoke();
        };
    }

    private void BindKeyboardActions()
    {
        _inputActions.Player.Move.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current) _inputVector = ctx.ReadValue<Vector2>();
        };

        _inputActions.Player.Move.canceled += ctx =>
        {
            if (ctx.control.device == Keyboard.current) _inputVector = Vector2.zero;
        };

        _inputActions.Player.Drift.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current && CanDrift()) HandleDriftPressed();
        };

        _inputActions.Player.Drift.canceled += ctx =>
        {
            if (ctx.control.device == Keyboard.current) HandleDriftReleased();
        };

        _inputActions.Player.Aim.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current && GetCanAim())
            {
                Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
                Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
                Vector2 offset = mouseScreenPos - screenCenter;
                float magnitude = Mathf.Clamp01(
                    offset.magnitude / Mathf.Max(1f, keyboardAimFullMagnitudePixels)
                );

                Vector2 analogMouseAim = offset.sqrMagnitude > 0.0001f
                    ? offset.normalized * magnitude
                    : Vector2.zero;

                SetAimInput(analogMouseAim);
            }
        };

        _inputActions.Player.Aim.canceled += ctx =>
        {
            if (ctx.control.device == Keyboard.current)
            {
                SetAimInput(Vector2.zero);
            }
        };

        _inputActions.Player.Speedup.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current) HandleSpeedupPressed();
        };

        _inputActions.Player.Speedup.canceled += ctx =>
        {
            if (ctx.control.device == Keyboard.current) HandleSpeedupReleased();
        };

        _inputActions.Player.ActivatePowerUp.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current && GetCanActivatePowerUp())
            {
                ActivatePowerUp();
            }
        };

        _inputActions.Player.MoveBackward.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current && CanMoveBackward)
            {
                SetCanMoveBackward(false);
                OnMoveBackwardPressed?.Invoke();
            }
        };

        _inputActions.Player.CheckOut.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current && activeCheckoutManager != null)
            {
                activeCheckoutManager.TryCheckoutCart();
                OnCheckoutReleased?.Invoke();
            }
        };


        _inputActions.Player.TutorialPrev.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current) OnTutorialPrev?.Invoke();
        };

        _inputActions.Player.TutorialNext.performed += ctx =>
        {
            if (ctx.control.device == Keyboard.current) OnTutorialNext?.Invoke();
        };
    }

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        currentHype = Mathf.Clamp(startingHype, 0f, maxHype);
        BindSnakeCartManager();
        NotifyHypeChanged();
    }

    private void OnDestroy()
    {
        UnbindSnakeCartManager();
    }

    private void OnValidate()
    {
        keyboardAimFullMagnitudePixels = Mathf.Max(1f, keyboardAimFullMagnitudePixels);
        maxHype = Mathf.Max(0.01f, maxHype);
        startingHype = Mathf.Clamp(startingHype, 0f, maxHype);
        baseHypeBurnPerSecond = Mathf.Max(0f, baseHypeBurnPerSecond);
        safeOwnedFollowerCount = Mathf.Max(0, safeOwnedFollowerCount);
        firstPenalizedHypeBurnMultiplier = Mathf.Max(1f, firstPenalizedHypeBurnMultiplier);
        hypeBurnMultiplierIncreasePerOwnedCart = Mathf.Max(0f, hypeBurnMultiplierIncreasePerOwnedCart);
        maxHypeBurnMultiplier = Mathf.Max(1f, maxHypeBurnMultiplier);

        // Keep the cap logically at or above the first penalized value.
        maxHypeBurnMultiplier = Mathf.Max(maxHypeBurnMultiplier, firstPenalizedHypeBurnMultiplier);

        if (!Application.isPlaying) currentHype = Mathf.Clamp(startingHype, 0f, maxHype);
    }

    private void Update()
    {
        if (controllable && !isInPit && !powerupFrozen)
        {
            GatherInput();
        }
        else
        {
            desiredDirection = Vector3.zero;
        }

        if (!controllable || isInPit || !CanDrift()) isDriftHeld = false;
        if (!controllable || isInPit || !CanSpeedingUp()) StopSpeedupInput();

        if (powerupFrozen && _aimInputVector.sqrMagnitude > 0f)
        {
            SetAimInput(Vector2.zero);
        }

        UpdateSpeedup();
        UpdateHeldEvents();
    }

    #endregion

    #region Movement Input

    private void GatherInput()
    {
        _input = new Vector3(_inputVector.x, 0f, _inputVector.y);
        desiredDirection = controllable ? _input.ToIso() : Vector3.zero;
    }

    public void CleanupInput()
    {
        SetAimInput(Vector2.zero);
        _inputActions?.Disable();
        InputUser.PerformPairingWithDevice(null, user);
    }

    #endregion

    #region Aim Runtime

    private void SetAimInput(Vector2 aimInput)
    {
        Vector2 nextInput = GetCanAim()
            ? Vector2.ClampMagnitude(aimInput, 1f)
            : Vector2.zero;

        if ((_aimInputVector - nextInput).sqrMagnitude <= 0.000001f)
        {
            return;
        }

        _aimInputVector = nextInput;
        _aimDirection = new Vector3(
            _aimInputVector.x,
            0f,
            _aimInputVector.y
        ).ToIso();

        OnAimInputChanged?.Invoke(_aimInputVector);
    }

    #endregion

    #region Drift / Speedup Interaction

    private void HandleDriftPressed()
    {
        if (!CanDrift()) return;

        if (enableDriftSpeedupOverride) StopSpeedupInput();
        isDriftHeld = true;
    }

    private void HandleDriftReleased()
    {
        isDriftHeld = false;
    }

    private void HandleSpeedupPressed()
    {
        if (!CanBeginSpeedup()) return;

        if (enableDriftSpeedupOverride) StopDriftInputForSpeedup("Speedup pressed");
        isSpeedingUp = true;
    }

    private bool CanBeginSpeedup()
    {
        return CanSpeedingUp() && currentHype > 0.01f;
    }

    private void HandleSpeedupReleased()
    {
        isSpeedingUp = false;
    }

    private void StopSpeedupInput()
    {
        if (!isSpeedingUp) return;

        isSpeedingUp = false;
        OnSpeedupHeld?.Invoke(false);
    }

    private void StopDriftInputForSpeedup(string reason)
    {
        if (!isDriftHeld && (driftController == null || !driftController.IsDrifting)) return;

        isDriftHeld = false;

        if (driftController != null) driftController.CancelDrift(reason);
    }

    #endregion

    #region Speedup Runtime

    private void UpdateSpeedup()
    {
        if (!isSpeedingUp)
        {
            OnSpeedupHeld?.Invoke(false);
            return;
        }

        if (!CanBeginSpeedup())
        {
            StopSpeedupInput();
            return;
        }

        RemoveHype(CurrentHypeBurnPerSecond * Time.deltaTime);
        OnSpeedupHeld?.Invoke(true);

        if (currentHype <= 0.01f) StopSpeedupInput();
    }

    private void UpdateHeldEvents()
    {
        OnMoveHeld?.Invoke(MoveInput.sqrMagnitude > 0.05f);
        OnAimHeld?.Invoke(_aimInputVector.sqrMagnitude > 0.05f);
    }

    #endregion

    #region Checkout / Powerup References

    public void SetActiveCheckoutHandler(CheckOutManager currentCheckoutManager)
    {
        activeCheckoutManager = currentCheckoutManager;
    }

    public void SetPowerupsManager(PowerupsManager manager)
    {
        // Temporary legacy migration path. The replacement system subscribes
        // to OnPowerupUsePressed and does not use this reference.
        powerupsManager = manager;
    }

    #endregion

    #region Move Backward State

    public void AllowMoveBackward()
    {
        SetCanMoveBackward(true);
    }

    public void DisallowMoveBackward()
    {
        SetCanMoveBackward(false);
    }

    public bool GetCanMoveBackward()
    {
        return CanMoveBackward;
    }

    private void SetCanMoveBackward(bool canMove)
    {
        if (canMoveBackward == canMove)
        {
            return;
        }

        bool wasAvailable = CanMoveBackward;
        canMoveBackward = canMove;
        bool isAvailable = CanMoveBackward;

        if (wasAvailable != isAvailable)
        {
            OnCanMoveBackwardChanged?.Invoke(isAvailable);
        }
    }

    #endregion

    #region Drift State

    public bool IsDriftHeld()
    {
        return isDriftHeld;
    }

    public bool CanDrift()
    {
        return allowDrift &&
               !powerupFrozen &&
               !colaMentosBoostActive;
    }

    /// <summary>
    /// Raw permission owned by systems such as Stall/Checkout. Duration
    /// power-up gates are intentionally excluded so another system can save
    /// and restore only the permission it actually owns.
    /// </summary>
    public bool IsDriftPermissionEnabled => allowDrift;

    public void AllowDrift()
    {
        allowDrift = true;
    }

    public void DisallowDrift()
    {
        allowDrift = false;
        isDriftHeld = false;
    }

    #endregion

    #region Powerup State

    public bool GetCanActivatePowerUp()
    {
        return canActivatePowerUp && !powerupFrozen;
    }

    public void AllowActivatePowerUp()
    {
        SetPowerupInputEnabled(true);
    }

    public void DisallowActivatePowerUp()
    {
        SetPowerupInputEnabled(false);
    }

    public void SetPowerupInputEnabled(bool enabled)
    {
        canActivatePowerUp = enabled;
    }

    public void ActivatePowerUp()
    {
        if (!GetCanActivatePowerUp()) return;

        // During migration, a subscribed PlayerPowerupController is always
        // authoritative. The legacy manager is used only when no replacement
        // listener exists yet, which keeps the project compile-safe while old
        // prefab components are removed one step at a time.
        if (OnPowerupUsePressed != null)
        {
            OnPowerupUsePressed.Invoke();
        }
        else if (powerupsManager != null)
        {
            powerupsManager.ActivateStoredPowerup();
        }

        OnShootPressed?.Invoke();
    }

    #endregion

    #region General Control / Pit State

    public void DisableControl()
    {
        controllable = false;
        isDriftHeld = false;
    }

    public void EnableControl()
    {
        controllable = true;
    }

    public void SetInPit()
    {
        isInPit = true;
        isDriftHeld = false;
    }

    public void SetOutPit()
    {
        isInPit = false;
    }

    public bool GetIsInPit()
    {
        return isInPit;
    }

    public bool GetCanAim()
    {
        return canAim && !powerupFrozen;
    }

    public void AllowAim()
    {
        SetAimInputEnabled(true);
    }

    public void DisallowAim()
    {
        SetAimInputEnabled(false);
    }

    public void SetAimInputEnabled(bool enabled)
    {
        if (canAim == enabled)
        {
            if (!enabled) SetAimInput(Vector2.zero);
            return;
        }

        canAim = enabled;

        if (!canAim)
        {
            SetAimInput(Vector2.zero);
        }
    }

    // Legacy charging state used by the old powerup/combat code.
    public bool IsCharing()
    {
        return !controllable;
    }

    #endregion

    #region Speedup State

    public bool IsSpeedingUp()
    {
        return isSpeedingUp;
    }

    public bool CanSpeedingUp()
    {
        return canSpeedup &&
               !powerupFrozen &&
               !colaMentosBoostActive;
    }

    /// <summary>
    /// Raw Hype-Speed-Up permission before Freeze or duration-effect gates.
    /// </summary>
    public bool IsSpeedupPermissionEnabled => canSpeedup;

    public void AllowSpeedingUp()
    {
        canSpeedup = true;
    }

    public void DisallowSpeedingUp()
    {
        canSpeedup = false;
        StopSpeedupInput();
    }

    #endregion

    #region Cola Mentos Boost State

    /// <summary>
    /// Independent duration-effect gate owned by ColaMentosEffectSystem.
    /// It never rewrites the base Drift/Speedup permissions used by Stall or
    /// Checkout, and it does not interfere with Freeze or pit state.
    /// </summary>
    public void SetColaMentosBoost(
        bool active,
        float fixedTargetSpeed)
    {
        float resolvedTargetSpeed = active
            ? Mathf.Max(0f, fixedTargetSpeed)
            : 0f;

        bool changed =
            colaMentosBoostActive != active ||
            !Mathf.Approximately(
                colaMentosTargetSpeed,
                resolvedTargetSpeed
            );

        colaMentosBoostActive = active;
        colaMentosTargetSpeed = resolvedTargetSpeed;

        if (colaMentosBoostActive)
        {
            isDriftHeld = false;
            StopSpeedupInput();
            driftController?.CancelDrift("Cola Mentos boost activated");
        }

        if (changed)
        {
            OnColaMentosBoostChanged?.Invoke(
                colaMentosBoostActive,
                colaMentosTargetSpeed
            );
        }
    }

    public bool TryGetColaMentosTargetSpeed(out float fixedTargetSpeed)
    {
        fixedTargetSpeed = colaMentosTargetSpeed;
        return colaMentosBoostActive;
    }

    #endregion

    #region Power-up Freeze State

    /// <summary>
    /// Independent runtime gate owned by the Ice effect. It never rewrites
    /// the authored/temporary Drift, Speedup, Aim, Activate, or general
    /// control permissions owned by Stall, Checkout, and other systems.
    /// </summary>
    public void SetPowerupFrozen(bool frozen)
    {
        if (powerupFrozen == frozen) return;

        bool moveBackwardWasAvailable = CanMoveBackward;
        powerupFrozen = frozen;

        if (powerupFrozen)
        {
            desiredDirection = Vector3.zero;
            isDriftHeld = false;
            StopSpeedupInput();
            SetAimInput(Vector2.zero);
        }

        bool moveBackwardIsAvailable = CanMoveBackward;

        if (moveBackwardWasAvailable != moveBackwardIsAvailable)
        {
            OnCanMoveBackwardChanged?.Invoke(moveBackwardIsAvailable);
        }

        OnPowerupFrozenChanged?.Invoke(powerupFrozen);
    }

    #endregion

    #region Hype Resource

    public void AddHype(float amount)
    {
        if (amount <= 0f) return;

        float previousHype = currentHype;
        currentHype = Mathf.Clamp(currentHype + amount, 0f, maxHype);

        if (!Mathf.Approximately(previousHype, currentHype)) NotifyHypeChanged();
    }

    public void RemoveHype(float amount)
    {
        if (amount <= 0f || currentHype <= 0f) return;

        float previousHype = currentHype;
        currentHype = Mathf.Max(0f, currentHype - amount);

        if (!Mathf.Approximately(previousHype, currentHype)) NotifyHypeChanged();
    }

    private void NotifyHypeChanged()
    {
        OnHypeChanged?.Invoke(currentHype, maxHype);
    }

    #endregion

    #region Hype Burn Cart Count

    private void BindSnakeCartManager()
    {
        if (snakeCartManager == null) snakeCartManager = GetComponentInParent<SnakeCartManager>();

        if (snakeCartManager == null)
        {
            hypeBurnOwnedFollowerCount = 0;
            Debug.LogWarning("[CartControlScript] SnakeCartManager was not found. Hype burn will remain at its base rate.", this);
            return;
        }

        snakeCartManager.OnOwnedFollowersChanged -= HandleOwnedFollowersChanged;
        snakeCartManager.OnOwnedFollowersChanged += HandleOwnedFollowersChanged;

        RefreshHypeBurnCartCount();
    }

    private void UnbindSnakeCartManager()
    {
        if (snakeCartManager == null) return;
        snakeCartManager.OnOwnedFollowersChanged -= HandleOwnedFollowersChanged;
    }

    private void HandleOwnedFollowersChanged()
    {
        RefreshHypeBurnCartCount();
    }

    private void RefreshHypeBurnCartCount()
    {
        hypeBurnOwnedFollowerCount = snakeCartManager != null ? snakeCartManager.GetOwnedFollowerCount() : 0;
        OnHypeBurnRateChanged?.Invoke(CurrentHypeBurnPerSecond);
    }

    #endregion
}
