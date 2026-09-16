using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Per-player owner of the replacement power-up lifecycle.
///
/// Current implementation stage:
/// - owns a one-slot, newest-pickup-wins inventory;
/// - binds the runtime-spawned leading cart by Player1..Player4 tag;
/// - derives Aim/Activate input permissions;
/// - combines independent blocker reasons;
/// - validates targeted requests against the targeting layer;
/// - publishes accepted use requests without consuming the item yet.
///
/// Effect executors are intentionally added in later steps. A use request is
/// therefore diagnostic-only until an executor accepts it and explicitly
/// consumes the stored item.
/// </summary>
[DisallowMultipleComponent]
public class PlayerPowerupController : MonoBehaviour
{
    #region Identity

    [Header("Identity")]
    [Tooltip(
        "Player slot used by the runtime registry and Player1..Player4 tag lookup. " +
        "A valid SnakeCartManager player ID overrides this value automatically."
    )]
    [Range(1, PowerupRuntimeSystem.MaxPlayerSlots)]
    [SerializeField] private int playerIndex = 1;

    #endregion

    #region References

    [Header("References")]
    [SerializeField] private CartControlScript cartControlInput;
    [SerializeField] private SnakeCartManager snakeCartManager;

    [Tooltip("Optional explicit scene reference. Automatically resolved when left empty.")]
    [SerializeField] private PowerupRuntimeSystem runtimeSystem;

    [Header("Runtime Cart Binding")]
    [Tooltip("Delay before the first Player1..Player4 tag lookup. The leading cart is spawned at runtime.")]
    [Min(0f)]
    [SerializeField] private float initialCartBindingDelay = 0.1f;

    [Tooltip("Retry interval while the runtime leading cart has not appeared yet. Uses unscaled time.")]
    [Min(0.02f)]
    [SerializeField] private float cartBindingRetryInterval = 0.1f;

    [Tooltip("Log one warning after waiting this long, while continuing to retry. 0 disables the warning.")]
    [Min(0f)]
    [SerializeField] private float cartBindingWarningDelay = 3f;

    #endregion

    #region Diagnostics

    [Header("Diagnostics")]
    [SerializeField] private bool logAcceptedUseRequests = true;
    [SerializeField] private bool logRejectedUseRequests = true;

    [Header("Runtime - Read Only")]
    [SerializeField] private GameObject boundPlayerObject;
    [SerializeField] private bool hasStoredPowerup;
    [SerializeField] private PowerupId storedPowerup;
    [SerializeField] private PowerupUseBlockReason activeBlockReasons;
    [SerializeField] private bool canUseStoredPowerup;
    [SerializeField] private uint acceptedUseRequestVersion;

    private bool missingRuntimeSystemLogged;
    private bool invalidPlayerIndexLogged;
    private bool delayedCartBindingWarningLogged;
    private Coroutine cartBindingRoutine;
    private CartControlScript subscribedCartControlInput;

    #endregion

    #region Public State

    public int PlayerIndex => playerIndex;
    public CartControlScript CartControlInput => cartControlInput;
    public bool HasStoredPowerup => hasStoredPowerup;
    public PowerupId StoredPowerup => storedPowerup;
    public PowerupUseBlockReason ActiveBlockReasons => activeBlockReasons;
    // A newly collected item intentionally replaces an unused stored item.
    public bool CanStorePowerup => true;
    public bool CanUseStoredPowerup =>
        hasStoredPowerup &&
        playerIndex >= 1 &&
        playerIndex <= PowerupRuntimeSystem.MaxPlayerSlots &&
        activeBlockReasons == PowerupUseBlockReason.None;

    public uint AcceptedUseRequestVersion => acceptedUseRequestVersion;

    #endregion

    #region Events

    public event Action<PlayerPowerupController, PowerupId?> OnStoredPowerupChanged;
    public event Action<PlayerPowerupController, PowerupId, PowerupId> OnStoredPowerupReplaced;
    public event Action<PlayerPowerupController, PowerupUseBlockReason> OnBlockReasonsChanged;

    /// <summary>
    /// Optional execution-layer gate evaluated after inventory/blocker checks
    /// but before a request is accepted. Projectile power-ups require at least
    /// one validator so a neutral or invalid target can never become a launch.
    /// Every subscribed validator must return true.
    /// </summary>
    public event Func<PlayerPowerupController, PowerupId, bool>
        OnPowerupUseValidationRequested;

    public event Action<PlayerPowerupController, PowerupId> OnPowerupUseRejected;

    /// <summary>
    /// Raised only after the input press passes inventory and blocker checks.
    /// A future executor decides whether the request can launch/activate and
    /// consumes the item only after successful acceptance.
    /// </summary>
    public event Action<PlayerPowerupController, PowerupId> OnPowerupUseRequested;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        ResolveStaticReferences();
    }

    private void Awake()
    {
        ResolveStaticReferences();
        RefreshPlayerIndex();

        // This succeeds immediately only when the runtime cart already exists
        // or an explicit reference was assigned. Otherwise OnEnable starts the
        // delayed Player1..Player4 binding routine.
        TryBindCartControlInputNow();
        RefreshInputPermissions();
    }

    private void OnEnable()
    {
        TryBindCartControlInputNow();
        StartCartControlBindingIfNeeded();
        TryRegisterWithRuntimeSystem();
        RefreshInputPermissions();
    }

    private void Start()
    {
        // Runtime identity can also finish after this component's Awake.
        RefreshPlayerIndex();
        TryRegisterWithRuntimeSystem();
        TryBindCartControlInputNow();
        StartCartControlBindingIfNeeded();
        RefreshInputPermissions();

        if (playerIndex < 1 || playerIndex > PowerupRuntimeSystem.MaxPlayerSlots)
        {
            LogInvalidPlayerIndexOnce();
        }
    }

    private void OnDisable()
    {
        StopCartControlBinding();

        if (cartControlInput != null)
        {
            cartControlInput.SetAimInputEnabled(false);
            cartControlInput.SetPowerupInputEnabled(false);
        }

        UnsubscribeFromCartControlInput();

        runtimeSystem?.UnregisterPlayer(this);
    }

    private void OnValidate()
    {
        playerIndex = Mathf.Clamp(
            playerIndex,
            1,
            PowerupRuntimeSystem.MaxPlayerSlots
        );

        initialCartBindingDelay = Mathf.Max(0f, initialCartBindingDelay);
        cartBindingRetryInterval = Mathf.Max(0.02f, cartBindingRetryInterval);
        cartBindingWarningDelay = Mathf.Max(0f, cartBindingWarningDelay);
    }

    #endregion

    #region Inventory

    public bool TryStorePowerup(PowerupId powerupId)
    {
        if (!PowerupIdRules.IsDefined(powerupId))
        {
            Debug.LogError(
                $"[PlayerPowerupController] Cannot store undefined power-up value '{powerupId}'.",
                this
            );
            return false;
        }

        bool replacedExistingPowerup = hasStoredPowerup;
        PowerupId previousPowerup = storedPowerup;

        storedPowerup = powerupId;
        hasStoredPowerup = true;

        RefreshInputPermissions();

        if (replacedExistingPowerup)
        {
            OnStoredPowerupReplaced?.Invoke(
                this,
                previousPowerup,
                storedPowerup
            );
        }

        // Always publish the newly stored value, including same-item
        // replacement, so future pickup feedback can react to every collect.
        OnStoredPowerupChanged?.Invoke(this, storedPowerup);
        return true;
    }

    /// <summary>
    /// Reserved for future effect executors. Input alone never calls this.
    /// </summary>
    public bool TryConsumeStoredPowerup(out PowerupId consumedPowerup)
    {
        if (!hasStoredPowerup)
        {
            consumedPowerup = default;
            return false;
        }

        consumedPowerup = storedPowerup;
        hasStoredPowerup = false;
        storedPowerup = default;

        RefreshInputPermissions();
        OnStoredPowerupChanged?.Invoke(this, null);
        return true;
    }

    public void ClearStoredPowerup()
    {
        if (!hasStoredPowerup) return;

        hasStoredPowerup = false;
        storedPowerup = default;
        RefreshInputPermissions();
        OnStoredPowerupChanged?.Invoke(this, null);
    }

    #endregion

    #region Blockers

    public void SetUseBlocked(PowerupUseBlockReason reason, bool blocked)
    {
        if (reason == PowerupUseBlockReason.None) return;

        PowerupUseBlockReason previousReasons = activeBlockReasons;

        if (blocked)
        {
            activeBlockReasons |= reason;
        }
        else
        {
            activeBlockReasons &= ~reason;
        }

        if (previousReasons == activeBlockReasons) return;

        RefreshInputPermissions();
        OnBlockReasonsChanged?.Invoke(this, activeBlockReasons);
    }

    public bool IsUseBlocked(PowerupUseBlockReason reason)
    {
        if (reason == PowerupUseBlockReason.None)
        {
            return activeBlockReasons == PowerupUseBlockReason.None;
        }

        return (activeBlockReasons & reason) != 0;
    }

    #endregion

    #region Input Permission

    public void RefreshInputPermissions()
    {
        canUseStoredPowerup = CanUseStoredPowerup;

        if (cartControlInput == null) return;

        cartControlInput.SetPowerupInputEnabled(canUseStoredPowerup);

        bool allowProjectileAim =
            canUseStoredPowerup &&
            PowerupIdRules.RequiresProjectileAim(storedPowerup);

        cartControlInput.SetAimInputEnabled(allowProjectileAim);
    }

    private void HandlePowerupUsePressed()
    {
        if (!CanUseStoredPowerup) return;

        PowerupId requestedPowerup = storedPowerup;

        if (!ValidateUseRequest(requestedPowerup))
        {
            if (logRejectedUseRequests)
            {
                Debug.Log(
                    $"[PlayerPowerupController] P{playerIndex} rejected use request " +
                    $"for {requestedPowerup}: no valid target is currently available. " +
                    "The stored power-up was retained.",
                    this
                );
            }

            OnPowerupUseRejected?.Invoke(this, requestedPowerup);
            return;
        }

        acceptedUseRequestVersion++;

        if (logAcceptedUseRequests)
        {
            Debug.Log(
                $"[PlayerPowerupController] P{playerIndex} accepted use request " +
                $"#{acceptedUseRequestVersion} for {requestedPowerup}. " +
                "No effect is launched or consumed in this targeting-preview step.",
                this
            );
        }

        OnPowerupUseRequested?.Invoke(this, requestedPowerup);
    }

    private bool ValidateUseRequest(PowerupId requestedPowerup)
    {
        Func<PlayerPowerupController, PowerupId, bool> validators =
            OnPowerupUseValidationRequested;

        if (validators == null)
        {
            // Immediate-use power-ups do not need projectile targeting. A
            // targeted item fails closed if its targeting controller is absent.
            return !PowerupIdRules.RequiresProjectileAim(requestedPowerup);
        }

        Delegate[] invocationList = validators.GetInvocationList();

        for (int i = 0; i < invocationList.Length; i++)
        {
            var validator =
                (Func<PlayerPowerupController, PowerupId, bool>)invocationList[i];

            if (!validator(this, requestedPowerup)) return false;
        }

        return true;
    }

    #endregion

    #region Runtime Cart Binding

    private void StartCartControlBindingIfNeeded()
    {
        if (cartControlInput != null)
        {
            SubscribeToCartControlInput();
            return;
        }

        if (cartBindingRoutine == null && isActiveAndEnabled)
        {
            cartBindingRoutine = StartCoroutine(BindRuntimeCartControlRoutine());
        }
    }

    private void StopCartControlBinding()
    {
        if (cartBindingRoutine == null) return;

        StopCoroutine(cartBindingRoutine);
        cartBindingRoutine = null;
    }

    private IEnumerator BindRuntimeCartControlRoutine()
    {
        if (initialCartBindingDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(initialCartBindingDelay);
        }

        float startedWaitingAt = Time.unscaledTime;

        while (isActiveAndEnabled && cartControlInput == null)
        {
            RefreshPlayerIndex();

            if (TryBindCartControlInputNow())
            {
                TryRegisterWithRuntimeSystem();
                cartBindingRoutine = null;
                yield break;
            }

            if (!delayedCartBindingWarningLogged &&
                cartBindingWarningDelay > 0f &&
                Time.unscaledTime - startedWaitingAt >= cartBindingWarningDelay)
            {
                delayedCartBindingWarningLogged = true;
                Debug.LogWarning(
                    $"[PlayerPowerupController] Still waiting for runtime Player{playerIndex} " +
                    "and its CartControlScript. Binding will continue retrying.",
                    this
                );
            }

            yield return new WaitForSecondsRealtime(cartBindingRetryInterval);
        }

        cartBindingRoutine = null;
    }

    private bool TryBindCartControlInputNow()
    {
        if (cartControlInput == null)
        {
            if (!TryResolveRuntimeCartControl(out CartControlScript resolvedInput))
            {
                return false;
            }

            cartControlInput = resolvedInput;
        }

        SubscribeToCartControlInput();
        delayedCartBindingWarningLogged = false;
        RefreshInputPermissions();
        return cartControlInput != null;
    }

    private bool TryResolveRuntimeCartControl(out CartControlScript resolvedInput)
    {
        resolvedInput = null;
        boundPlayerObject = null;

        // Match the existing MapEventPointer solution: runtime leading carts
        // expose Player1..Player4 tags after they are instantiated.
        if (playerIndex >= 1 && playerIndex <= PowerupRuntimeSystem.MaxPlayerSlots)
        {
            boundPlayerObject = GameObject.FindGameObjectWithTag(
                $"Player{playerIndex}"
            );

            if (boundPlayerObject != null)
            {
                resolvedInput =
                    boundPlayerObject.GetComponent<CartControlScript>() ??
                    boundPlayerObject.GetComponentInChildren<CartControlScript>(true) ??
                    boundPlayerObject.GetComponentInParent<CartControlScript>();

                if (resolvedInput != null) return true;
            }
        }

        // Hierarchy fallbacks keep prefab and isolated test scenes convenient.
        if (snakeCartManager != null)
        {
            resolvedInput =
                snakeCartManager.GetComponentInChildren<CartControlScript>(true);

            if (resolvedInput != null) return true;
        }

        resolvedInput =
            GetComponentInChildren<CartControlScript>(true) ??
            GetComponentInParent<CartControlScript>();

        return resolvedInput != null;
    }

    private void SubscribeToCartControlInput()
    {
        if (cartControlInput == null) return;
        if (subscribedCartControlInput == cartControlInput) return;

        UnsubscribeFromCartControlInput();
        subscribedCartControlInput = cartControlInput;
        subscribedCartControlInput.OnPowerupUsePressed += HandlePowerupUsePressed;
    }

    private void UnsubscribeFromCartControlInput()
    {
        if (subscribedCartControlInput != null)
        {
            subscribedCartControlInput.OnPowerupUsePressed -= HandlePowerupUsePressed;
        }

        subscribedCartControlInput = null;
    }

    #endregion

    #region Runtime Registration

    public void RefreshRuntimeRegistration()
    {
        ResolveStaticReferences();
        RefreshPlayerIndex();
        TryBindCartControlInputNow();
        StartCartControlBindingIfNeeded();
        TryRegisterWithRuntimeSystem();
        RefreshInputPermissions();
    }

    private void TryRegisterWithRuntimeSystem()
    {
        if (runtimeSystem == null)
        {
            runtimeSystem = FindFirstObjectByType<PowerupRuntimeSystem>();
        }

        if (runtimeSystem == null)
        {
            if (!missingRuntimeSystemLogged)
            {
                missingRuntimeSystemLogged = true;
                Debug.LogWarning(
                    "[PlayerPowerupController] No PowerupRuntimeSystem was found. " +
                    "Inventory can still be tested, but match lifecycle blocking is unavailable.",
                    this
                );
            }

            return;
        }

        missingRuntimeSystemLogged = false;

        if (playerIndex < 1 || playerIndex > PowerupRuntimeSystem.MaxPlayerSlots)
        {
            return;
        }

        runtimeSystem.RegisterPlayer(this);
    }

    private void RefreshPlayerIndex()
    {
        if (snakeCartManager == null)
        {
            snakeCartManager = GetComponentInParent<SnakeCartManager>();
        }

        if (snakeCartManager != null)
        {
            int resolvedPlayerIndex = snakeCartManager.GetPlayerId();

            if (resolvedPlayerIndex >= 1 &&
                resolvedPlayerIndex <= PowerupRuntimeSystem.MaxPlayerSlots)
            {
                playerIndex = resolvedPlayerIndex;
            }
        }

        if (playerIndex >= 1 && playerIndex <= PowerupRuntimeSystem.MaxPlayerSlots)
        {
            invalidPlayerIndexLogged = false;
        }
    }

    private void ResolveStaticReferences()
    {
        if (snakeCartManager == null)
        {
            snakeCartManager = GetComponentInParent<SnakeCartManager>();
        }
    }

    private void LogInvalidPlayerIndexOnce()
    {
        if (invalidPlayerIndexLogged) return;

        invalidPlayerIndexLogged = true;
        Debug.LogError(
            "[PlayerPowerupController] Could not resolve a valid player index (expected 1..4). " +
            "Assign its Identity value or place it inside the correct SnakeCartManager hierarchy.",
            this
        );
    }

    #endregion
}
