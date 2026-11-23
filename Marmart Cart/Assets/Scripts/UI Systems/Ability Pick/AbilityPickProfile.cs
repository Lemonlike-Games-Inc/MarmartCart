using System;
using UnityEngine;

/// <summary>Where the parts of the Ability pick sit in one player's view, for one player count.</summary>
[Serializable]
public struct AbilityPickLayout
{
    [Tooltip("Gap from the top of the view to the top of the header, in 1920x1080 pixels.")]
    public float headerTopOffset;

    [Tooltip("Scale of the header (title, sub-line, timer) as authored in the prefab.")]
    [Min(0.1f)] public float headerScale;

    [Tooltip("Card row centre, up from the middle of the view. Negative moves it down.")]
    public float cardRowOffsetY;

    [Tooltip("Scale of each card as authored. The cards stay spread evenly across the view.")]
    [Min(0.1f)] public float cardScale;

    [Tooltip("Gap from the bottom of the view to the bottom of the footer (input hints).")]
    public float footerBottomOffset;

    [Min(0.1f)] public float footerScale;

    public AbilityPickLayout(
        float headerTopOffset,
        float headerScale,
        float cardRowOffsetY,
        float cardScale,
        float footerBottomOffset,
        float footerScale)
    {
        this.headerTopOffset = headerTopOffset;
        this.headerScale = headerScale;
        this.cardRowOffsetY = cardRowOffsetY;
        this.cardScale = cardScale;
        this.footerBottomOffset = footerBottomOffset;
        this.footerScale = footerScale;
    }
}

/// <summary>
/// The Ability pick setup: the view prefab each player gets, and where its
/// parts sit in 2P and 4P. The look lives in the prefabs (Ability Pick and
/// Ability Pick Card). Style only; nothing writes here at runtime.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/UI/Ability Pick Profile",
    fileName = "AbilityPickProfile"
)]
public sealed class AbilityPickProfile : ScriptableObject
{
    [Tooltip("One copy goes into each player's Banner layer.")]
    [SerializeField] private AbilityPickView viewPrefab;

    [Header("2 Player Layout (each view 960x1080)")]
    [SerializeField]
    private AbilityPickLayout twoPlayerLayout = new AbilityPickLayout(60f, 1f, -80f, 1f, 60f, 1f);

    [Header("4 Player Layout (each view 960x540)")]
    [SerializeField]
    private AbilityPickLayout fourPlayerLayout = new AbilityPickLayout(12f, 0.5f, -36f, 0.68f, 12f, 0.5f);

    public AbilityPickView ViewPrefab => viewPrefab;

    public AbilityPickLayout GetLayout(int activePlayerCount)
    {
        return activePlayerCount > 2 ? fourPlayerLayout : twoPlayerLayout;
    }
}
