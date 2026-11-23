using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// System sound hooks for one player's items: generic feedback that does not
/// depend on which item it is. Wire each hook in the Inspector, for example
/// to SfxManager.PlaySFX. Item-specific sounds (throw, impact, effect) stay on
/// PowerupLifecycleUnityEventRelay.
///
/// Lives on the Chain of Carts root next to PlayerItemHand and
/// PlayerAbilityController, so every player, including runtime-spawned ones,
/// has it. Several hooks can fire in the same frame: a pickup or a ready
/// Ability also equips the new item, and a swap equips the other one.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerItemSfxRelay : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private PlayerItemHand hand;
    [SerializeField] private PlayerAbilityController ability;

    [Header("Hooks")]
    [Tooltip("A map powerup was collected. Fires on every collect, also for the same item again.")]
    [SerializeField] private UnityEvent onPowerupCollected = new UnityEvent();

    [Tooltip(
        "A different item became the equipped one: after a pickup, a ready Ability, " +
        "an LB swap, or when the other item was used up."
    )]
    [SerializeField] private UnityEvent onItemEquipped = new UnityEvent();

    [Tooltip("LB swapped the equipped item.")]
    [SerializeField] private UnityEvent onSwapped = new UnityEvent();

    [Tooltip("LB was pressed with nothing to swap to (only one item, or none). Leave empty to stay silent.")]
    [SerializeField] private UnityEvent onSwapFailed = new UnityEvent();

    [Tooltip("The Ability charge is ready and loaded into the hand.")]
    [SerializeField] private UnityEvent onAbilityReady = new UnityEvent();

    [Header("Debug")]
    [Tooltip("Logs each hook as it fires, to check the timing before sounds are wired.")]
    [SerializeField] private bool logHooks;

    private HandSlotId lastEquippedSlot;
    private ItemDefinition lastEquippedItem;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (hand != null)
        {
            // What is equipped now counts as already announced.
            lastEquippedSlot = hand.EquippedSlotId;
            lastEquippedItem = hand.EquippedItem;

            hand.OnMapItemChanged += HandleMapItemChanged;
            hand.OnAbilityItemChanged += HandleAbilityItemChanged;
            hand.OnEquippedSlotChanged += HandleEquippedSlotChanged;
            hand.OnSwapPressed += HandleSwapPressed;
        }

        if (ability != null) ability.OnChargeReady += HandleAbilityReady;
    }

    private void OnDisable()
    {
        if (hand != null)
        {
            hand.OnMapItemChanged -= HandleMapItemChanged;
            hand.OnAbilityItemChanged -= HandleAbilityItemChanged;
            hand.OnEquippedSlotChanged -= HandleEquippedSlotChanged;
            hand.OnSwapPressed -= HandleSwapPressed;
        }

        if (ability != null) ability.OnChargeReady -= HandleAbilityReady;
    }

    private void HandleMapItemChanged(PlayerItemHand source, ItemDefinition item)
    {
        if (item != null) Fire(onPowerupCollected, "powerup collected");
        CheckEquippedItem();
    }

    private void HandleAbilityItemChanged(PlayerItemHand source, ItemDefinition item)
    {
        CheckEquippedItem();
    }

    private void HandleEquippedSlotChanged(PlayerItemHand source, HandSlotId slot)
    {
        CheckEquippedItem();
    }

    private void HandleSwapPressed(PlayerItemHand source, bool swapped)
    {
        if (swapped)
        {
            Fire(onSwapped, "swapped");
        }
        else
        {
            Fire(onSwapFailed, "swap failed");
        }
    }

    private void HandleAbilityReady(PlayerAbilityController source)
    {
        Fire(onAbilityReady, "Ability ready");
    }

    private void CheckEquippedItem()
    {
        HandSlotId slot = hand.EquippedSlotId;
        ItemDefinition item = hand.EquippedItem;

        if (slot == lastEquippedSlot && item == lastEquippedItem) return;

        lastEquippedSlot = slot;
        lastEquippedItem = item;

        if (item != null) Fire(onItemEquipped, "item equipped");
    }

    private void Fire(UnityEvent hook, string label)
    {
        if (logHooks) Debug.Log($"[Sfx] P{hand.PlayerIndex} {label}.", this);
        hook.Invoke();
    }

    private void ResolveReferences()
    {
        if (hand == null) hand = GetComponent<PlayerItemHand>();
        if (ability == null) ability = GetComponent<PlayerAbilityController>();
    }
}
