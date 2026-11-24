using System;
using UnityEngine;

/// <summary>Where the Policy reveal sits in one player's view, for one player count.</summary>
[Serializable]
public struct PolicyRevealLayout
{
    [Tooltip("Gap from the top of the view to the top of the header, in 1920x1080 pixels.")]
    public float headerTopOffset;

    [Min(0.1f)] public float headerScale;

    [Tooltip("Card row centre, up from the middle of the view. Negative moves it down.")]
    public float cardRowOffsetY;

    [Min(0.1f)] public float cardScale;

    [Tooltip("Distance between card centres.")]
    [Min(0f)] public float cardSpacing;

    public PolicyRevealLayout(float headerTopOffset, float headerScale, float cardRowOffsetY, float cardScale, float cardSpacing)
    {
        this.headerTopOffset = headerTopOffset;
        this.headerScale = headerScale;
        this.cardRowOffsetY = cardRowOffsetY;
        this.cardScale = cardScale;
        this.cardSpacing = cardSpacing;
    }
}

/// <summary>Where the top-left Policy banners sit in one player's view, for one player count.</summary>
[Serializable]
public struct PolicyBannerLayout
{
    [Tooltip("Gap from the left of the view, in 1920x1080 pixels.")]
    public float leftOffset;

    [Tooltip("Gap from the top of the view.")]
    public float topOffset;

    [Tooltip("Scale of the banners as authored in the prefab.")]
    [Min(0.1f)] public float scale;

    public PolicyBannerLayout(float leftOffset, float topOffset, float scale)
    {
        this.leftOffset = leftOffset;
        this.topOffset = topOffset;
        this.scale = scale;
    }
}

/// <summary>
/// The Policy reveal and banner setup: the view prefabs each player gets, and
/// where they sit in 2P and 4P. The look lives in the prefabs. Style only;
/// nothing writes here at runtime.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/UI/Policy UI Profile",
    fileName = "PolicyUIProfile"
)]
public sealed class PolicyUIProfile : ScriptableObject
{
    [Tooltip("One copy goes into each player's Banner layer during the reveal.")]
    [SerializeField] private PolicyRevealView revealViewPrefab;

    [Tooltip("One copy goes into each player's Banner layer after the reveal.")]
    [SerializeField] private PolicyBannerView bannerViewPrefab;

    [Header("2 Player (each view 960x1080)")]
    [SerializeField] private PolicyRevealLayout twoPlayerReveal = new PolicyRevealLayout(60f, 1f, -60f, 1f, 310f);
    [SerializeField] private PolicyBannerLayout twoPlayerBanners = new PolicyBannerLayout(16f, 16f, 1f);

    [Header("4 Player (each view 960x540)")]
    [SerializeField] private PolicyRevealLayout fourPlayerReveal = new PolicyRevealLayout(12f, 0.5f, -30f, 0.68f, 220f);
    [SerializeField] private PolicyBannerLayout fourPlayerBanners = new PolicyBannerLayout(10f, 8f, 0.65f);

    public PolicyRevealView RevealViewPrefab => revealViewPrefab;
    public PolicyBannerView BannerViewPrefab => bannerViewPrefab;

    public PolicyRevealLayout GetRevealLayout(int activePlayerCount)
    {
        return activePlayerCount > 2 ? fourPlayerReveal : twoPlayerReveal;
    }

    public PolicyBannerLayout GetBannerLayout(int activePlayerCount)
    {
        return activePlayerCount > 2 ? fourPlayerBanners : twoPlayerBanners;
    }
}
