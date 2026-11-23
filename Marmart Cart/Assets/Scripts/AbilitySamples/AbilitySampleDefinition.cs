using UnityEngine;

public enum AbilitySampleKind
{
    /// <summary>A slow personal charge of a normal map item.</summary>
    Baseline,

    /// <summary>An upgraded item: its own charge, plus a passive on map pickups of that item.</summary>
    Upgrade,

    /// <summary>An item that is not on the map. Charge only.</summary>
    Gadget
}

/// <remarks>Saved as numbers in the Sample assets: keep the order, add new tiers at the end.</remarks>
public enum AbilitySampleTier
{
    /// <summary>Baselines and stat-only upgrades.</summary>
    Silver,

    /// <summary>Upgrades and gadgets that change behaviour.</summary>
    Gold,

    /// <summary>Rarer and more spectacular than Gold. No Sample uses it yet.</summary>
    Prismatic,

    /// <summary>A specific Unique appears once per Match.</summary>
    Unique
}

/// <summary>
/// One Ability Sample: picked between Rounds, lasts one Round. It charges an
/// Item into the player's ability slot. Definition only; the charge state
/// lives in PlayerAbilityController.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Ability Samples/Sample",
    fileName = "Ability Sample"
)]
public sealed class AbilitySampleDefinition : ScriptableObject
{
    [Tooltip("Player-facing name. Empty uses the asset name.")]
    [SerializeField] private string displayName;

    [SerializeField] private AbilitySampleKind kind;
    [SerializeField] private AbilitySampleTier tier;

    [Tooltip("The item this Sample charges into the ability slot.")]
    [SerializeField] private ItemDefinition item;

    [Tooltip("Game seconds from using the charge until the next one is ready.")]
    [Min(0.1f)]
    [SerializeField] private float cooldownSeconds = 20f;

    [Tooltip("Game seconds from the start of the Round until the first charge is ready.")]
    [Min(0f)]
    [SerializeField] private float firstChargeSeconds = 10f;

    [Header("Passive (Upgrade)")]
    [Tooltip(
        "While this Sample is held, the player's map pickups of this item give " +
        "the Sample's item instead. Empty means no passive."
    )]
    [SerializeField] private ItemDefinition upgradesMapItem;

    [Header("Pick Card")]
    [Tooltip("What it does, in one or two short lines. The pick card shows the name and cooldown on their own.")]
    [TextArea(2, 4)]
    [SerializeField] private string description;

    [Tooltip("Icon on the pick card. Empty uses the item's icon from the Player Action Bar Profile.")]
    [SerializeField] private Sprite cardIcon;

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public string Description => description;
    public Sprite CardIcon => cardIcon;
    public AbilitySampleKind Kind => kind;
    public AbilitySampleTier Tier => tier;
    public ItemDefinition Item => item;
    public float CooldownSeconds => cooldownSeconds;
    public float FirstChargeSeconds => firstChargeSeconds;
    public ItemDefinition UpgradesMapItem => upgradesMapItem;
}
