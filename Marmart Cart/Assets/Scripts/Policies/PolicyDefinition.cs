using UnityEngine;

/// <summary>
/// One Marmart Policy: a global rule for a Round that every player reacts to.
/// Definition only. What it does lives in its PolicyEffect component in the
/// gameplay scene, which references this asset.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Policies/Policy",
    fileName = "Policy"
)]
public sealed class PolicyDefinition : ScriptableObject
{
    [Tooltip("Player-facing name. Empty uses the asset name.")]
    [SerializeField] private string displayName;

    [Tooltip(
        "Shares the Ability Sample tiers, for a later random Policy pool and the card style. " +
        "The fixed schedule ignores it."
    )]
    [SerializeField] private AbilitySampleTier tier;

    [Header("Card")]
    [Tooltip("What it does, in one or two short lines.")]
    [TextArea(2, 4)]
    [SerializeField] private string description;

    [Tooltip("Icon on the reveal card and the banner card. Empty shows no icon.")]
    [SerializeField] private Sprite icon;

    [Header("Announcer (not built yet)")]
    [Tooltip("What the store announcer says when this Policy is revealed.")]
    [TextArea(1, 3)]
    [SerializeField] private string announcerLine;

    [Tooltip("Voice clip for the announcer line. Empty for now.")]
    [SerializeField] private AudioClip announcerClip;

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public AbilitySampleTier Tier => tier;
    public string Description => description;
    public Sprite Icon => icon;
    public string AnnouncerLine => announcerLine;
    public AudioClip AnnouncerClip => announcerClip;
}
