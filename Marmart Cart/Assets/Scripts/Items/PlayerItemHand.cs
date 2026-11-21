using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Per-player hand: everything about using an item, whichever source gave it.
///
/// - Owns the item slots. Map Powerup pickups load the map slot (a new pickup
///   replaces an unused item); the Ability charge loads the ability slot.
/// - The equipped item is the one LT uses and the aim preview shows. The slot
///   loaded last is equipped; LB swaps when both are loaded.
/// - Binds the runtime-spawned leading cart by Player1..Player4 tag.
/// - Derives Aim/Activate input permissions.
/// - Combines independent blocker reasons (match inactive, checkout, frozen...).
/// - Validates use requests, then publishes accepted ones without consuming.
///
/// The system that runs the item listens to accepted requests and consumes the
/// equipped item only after it has successfully created its runtime work.
/// </summary>
[DisallowMultipleComponent]
public class PlayerItemHand : MonoBehaviour
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
    [SerializeField] private bool logEquipChanges = true;

    [Header("Runtime - Read Only")]
    [SerializeField] private GameObject boundPlayerObject;
    [SerializeField] private HandSlot mapSlot = new HandSlot();
    [SerializeField] private HandSlot abilitySlot = new HandSlot();
    [SerializeField] private HandSlotId equippedSlot;
    [SerializeField] private PowerupUseBlockReason activeBlockReasons;
    [SerializeField] private bool canUseEquippedItem;
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
    public PowerupUseBlockReason ActiveBlockReasons => activeBlockReasons;

    /// <summary>The slot map Powerup pickups load.</summary>
    public HandSlot MapSlot => mapSlot;

    /// <summary>The slot the Ability charge loads.</summary>
    public HandSlot AbilitySlot => abilitySlot;

    public HandSlotId EquippedSlotId => EquippedSlot != null ? equippedSlot : HandSlotId.None;

    public bool HasEquippedItem => EquippedSlot != null;

    /// <summary>The item LT uses, or null.</summary>
    public ItemDefinition EquippedItem => EquippedSlot != null ? EquippedSlot.Item : null;

    public bool CanUseEquippedItem =>
        HasEquippedItem &&
        playerIndex >= 1 &&
        playerIndex <= PowerupRuntimeSystem.MaxPlayerSlots &&
        activeBlockReasons == PowerupUseBlockReason.None;

    public uint AcceptedUseRequestVersion => acceptedUseRequestVersion;

    private HandSlot EquippedSlot
    {
        get
        {
            HandSlot slot = GetSlot(equippedSlot);
            return slot != null && slot.IsLoaded ? slot : null;
        }
    }

    #endregion

    #region Events

    /// <summary>The map slot was loaded (also with the same item again) or emptied (null).</summary>
    public event Action<PlayerItemHand, ItemDefinition> OnMapItemChanged;

    /// <summary>A pickup replaced an unused map item: previous, new.</summary>
    public event Action<PlayerItemHand, ItemDefinition, ItemDefinition> OnMapItemReplaced;

    /// <summary>The Ability slot was loaded or emptied (null).</summary>
    public event Action<PlayerItemHand, ItemDefinition> OnAbilityItemChanged;

    /// <summary>The Ability slot's item was used. The Ability starts its cooldown.</summary>
    public event Action<PlayerItemHand, ItemDefinition> OnAbilityItemUsed;

    public event Action<PlayerItemHand, HandSlotId> OnEquippedSlotChanged;

    public event Action<PlayerItemHand, PowerupUseBlockReason> OnBlockReasonsChanged;

    /// <summary>
    /// Optional execution-layer gate evaluated after slot/blocker checks
    /// but before a request is accepted. Aimed items require at least one
    /// validator so a neutral or invalid target can never become a launch.
    /// Every subscribed validator must return true.
    /// </summary>
    public event Func<PlayerItemHand, ItemDefinition, bool>
        OnItemUseValidationRequested;

    public event Action<PlayerItemHand, ItemDefinition> OnItemUseRejected;

    /// <summary>
    /// Raised only after the input press passes slot, blocker and validation
    /// checks. The system that runs the item consumes the equipped item only
    /// after its work succeeded.
    /// </summary>
    public event Action<PlayerItemHand, ItemDefinition> OnItemUseRequested;

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

        // Unity null check, not ?. : on scene unload the system may already be
        // destroyed, and ?. would still raise its events to destroyed listeners.
        if (runtimeSystem != null) runtimeSystem.UnregisterPlayer(this);
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

    #region Slots

    // Equip rule: the slot loaded last is equipped. When the equipped slot
    // empties, the other slot is equipped if it is loaded. Swap toggles when
    // both are loaded.

    /// <summary>
    /// Loads a map Powerup item. A new pickup intentionally replaces an
    /// unused map item.
    /// </summary>
    public bool TryLoadMapItem(ItemDefinition item)
    {
        if (item == null)
        {
            Debug.LogError("[PlayerItemHand] Cannot load a missing item.", this);
            return false;
        }

        bool replacedExistingItem = mapSlot.IsLoaded;
        ItemDefinition previousItem = mapSlot.Item;

        mapSlot.Load(item, Time.time);
        Equip(HandSlotId.Map);

        if (replacedExistingItem)
        {
            OnMapItemReplaced?.Invoke(this, previousItem, item);
        }

        // Always publish the newly loaded value, including same-item
        // replacement, so pickup feedback can react to every collect.
        OnMapItemChanged?.Invoke(this, item);
        return true;
    }

    public void ClearMapItem()
    {
        if (!mapSlot.IsLoaded) return;

        mapSlot.Clear();
        EquipAfterEmptied(HandSlotId.Map);
        OnMapItemChanged?.Invoke(this, null);
    }

    /// <summary>Loads the Ability charge's item.</summary>
    public bool TryLoadAbilityItem(ItemDefinition item)
    {
        if (item == null)
        {
            Debug.LogError("[PlayerItemHand] Cannot load a missing Ability item.", this);
            return false;
        }

        abilitySlot.Load(item, Time.time);
        Equip(HandSlotId.Ability);
        OnAbilityItemChanged?.Invoke(this, item);
        return true;
    }

    /// <summary>Removes an unused Ability item without starting a cooldown.</summary>
    public void ClearAbilityItem()
    {
        if (!abilitySlot.IsLoaded) return;

        abilitySlot.Clear();
        EquipAfterEmptied(HandSlotId.Ability);
        OnAbilityItemChanged?.Invoke(this, null);
    }

    /// <summary>Equips the other slot when both are loaded. Returns whether it swapped.</summary>
    public bool TrySwapEquipped()
    {
        if (!mapSlot.IsLoaded || !abilitySlot.IsLoaded) return false;

        Equip(equippedSlot == HandSlotId.Map ? HandSlotId.Ability : HandSlotId.Map);
        return true;
    }

    /// <summary>
    /// Spends the equipped item. Called by the system that runs the item, only
    /// after its work succeeded. Input alone never calls this.
    /// </summary>
    public bool TryConsumeEquippedItem(out ItemDefinition consumedItem)
    {
        HandSlot slot = EquippedSlot;

        if (slot == null)
        {
            consumedItem = null;
            return false;
        }

        HandSlotId usedSlot = equippedSlot;
        consumedItem = slot.Item;
        slot.Clear();
        EquipAfterEmptied(usedSlot);

        if (usedSlot == HandSlotId.Map)
        {
            OnMapItemChanged?.Invoke(this, null);
        }
        else
        {
            OnAbilityItemChanged?.Invoke(this, null);
            OnAbilityItemUsed?.Invoke(this, consumedItem);
        }

        return true;
    }

    private HandSlot GetSlot(HandSlotId id)
    {
        return id == HandSlotId.Map ? mapSlot
            : id == HandSlotId.Ability ? abilitySlot
            : null;
    }

    private void EquipAfterEmptied(HandSlotId emptiedSlot)
    {
        if (equippedSlot != emptiedSlot)
        {
            RefreshInputPermissions();
            return;
        }

        HandSlotId other = emptiedSlot == HandSlotId.Map ? HandSlotId.Ability : HandSlotId.Map;
        Equip(GetSlot(other).IsLoaded ? other : HandSlotId.None);
    }

    private void Equip(HandSlotId slot)
    {
        if (equippedSlot == slot)
        {
            RefreshInputPermissions();
            return;
        }

        equippedSlot = slot;
        RefreshInputPermissions();

        if (logEquipChanges)
        {
            Debug.Log(
                $"[PlayerItemHand] P{playerIndex} equipped {slot}" +
                $"{(EquippedItem != null ? $" ({EquippedItem.name})" : string.Empty)}.",
                this
            );
        }

        OnEquippedSlotChanged?.Invoke(this, slot);
    }

    private void HandleSwapPressed()
    {
        TrySwapEquipped();
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
        canUseEquippedItem = CanUseEquippedItem;

        if (cartControlInput == null) return;

        cartControlInput.SetPowerupInputEnabled(canUseEquippedItem);

        bool allowAim =
            canUseEquippedItem &&
            EquippedItem.RequiresAim;

        cartControlInput.SetAimInputEnabled(allowAim);
    }

    private void HandlePowerupUsePressed()
    {
        if (!CanUseEquippedItem) return;

        ItemDefinition requestedItem = EquippedItem;

        if (!ValidateUseRequest(requestedItem))
        {
            if (logRejectedUseRequests)
            {
                Debug.Log(
                    $"[PlayerItemHand] P{playerIndex} rejected use request " +
                    $"for {requestedItem.name}: no valid target is currently available. " +
                    "The equipped item was kept.",
                    this
                );
            }

            OnItemUseRejected?.Invoke(this, requestedItem);
            return;
        }

        acceptedUseRequestVersion++;

        if (logAcceptedUseRequests)
        {
            Debug.Log(
                $"[PlayerItemHand] P{playerIndex} accepted use request " +
                $"#{acceptedUseRequestVersion} for {requestedItem.name}. " +
                "The responsible executor now owns launch and consumption.",
                this
            );
        }

        OnItemUseRequested?.Invoke(this, requestedItem);
    }

    private bool ValidateUseRequest(ItemDefinition requestedItem)
    {
        Func<PlayerItemHand, ItemDefinition, bool> validators =
            OnItemUseValidationRequested;

        if (validators == null)
        {
            // Items used at once need no targeting. An aimed item fails
            // closed if its targeting controller is absent.
            return !requestedItem.RequiresAim;
        }

        Delegate[] invocationList = validators.GetInvocationList();

        for (int i = 0; i < invocationList.Length; i++)
        {
            var validator =
                (Func<PlayerItemHand, ItemDefinition, bool>)invocationList[i];

            if (!validator(this, requestedItem)) return false;
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
                    $"[PlayerItemHand] Still waiting for runtime Player{playerIndex} " +
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
        subscribedCartControlInput.OnSwapItemPressed += HandleSwapPressed;
    }

    private void UnsubscribeFromCartControlInput()
    {
        if (subscribedCartControlInput != null)
        {
            subscribedCartControlInput.OnPowerupUsePressed -= HandlePowerupUsePressed;
            subscribedCartControlInput.OnSwapItemPressed -= HandleSwapPressed;
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
                    "[PlayerItemHand] No PowerupRuntimeSystem was found. " +
                    "Item slots can still be tested, but match lifecycle blocking is unavailable.",
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
            "[PlayerItemHand] Could not resolve a valid player index (expected 1..4). " +
            "Assign its Identity value or place it inside the correct SnakeCartManager hierarchy.",
            this
        );
    }

    #endregion
}
