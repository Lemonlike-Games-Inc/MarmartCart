using System;
using UnityEngine;

/// <summary>
/// The Prismatic tier's look. Its rim is a band of colours (UIRimGradient)
/// instead of one colour, and its motion is always the band flowing around
/// the rim, so it has no flat rim colour or motion setting.
/// </summary>
[Serializable]
public struct PrismaticCardStyle
{
    [Tooltip(
        "Colours running around the rim, the glow and the ready ring, by angle around the card. " +
        "End on the start colour so the band has no seam. The flow speed is Prismatic Cycle Seconds in each card setup's timings."
    )]
    public Gradient rimColors;

    [Tooltip("Body inside the rim. The body's gradient darkens it toward the bottom.")]
    public Color body;

    [Tooltip("Thin line just inside the rim. Alpha 0 hides it.")]
    public Color innerLine;

    [Tooltip("Colour of the soft glow behind a selected card. Alpha 0 = white.")]
    public Color selectGlow;
}

/// <summary>
/// Card colours and motion of each tier, shared by every card that shows a
/// tier (the Player Action Bar, the Ability pick and the Policy cards), so
/// they always match. Style only; nothing writes here at runtime.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/UI/Ability Tier Styles",
    fileName = "Ability Tier Styles"
)]
public sealed class AbilityTierStyleProfile : ScriptableObject
{
    [SerializeField]
    private ActionBarCardStyle silver = new ActionBarCardStyle(
        new Color(0.78f, 0.81f, 0.86f), new Color(0.3f, 0.34f, 0.4f), Color.clear, ActionBarCardMotion.None);

    [SerializeField]
    private ActionBarCardStyle gold = new ActionBarCardStyle(
        new Color(1f, 0.78f, 0.2f), new Color(0.44f, 0.3f, 0.08f), Color.clear, ActionBarCardMotion.Shine,
        new Color(1f, 0.78f, 0.25f, 1f));

    [Tooltip("Rim colours, body and inner line. The motion is always the colour band flowing around the rim.")]
    [SerializeField]
    private PrismaticCardStyle prismatic = new PrismaticCardStyle
    {
        rimColors = DefaultPrismaticRimColors(),
        body = new Color(0.24f, 0.19f, 0.38f),
        innerLine = Color.clear,
        selectGlow = Color.clear
    };

    [SerializeField]
    private ActionBarCardStyle unique = new ActionBarCardStyle(
        new Color(0.74f, 0.04f, 0.12f), new Color(0.24f, 0.03f, 0.05f), new Color(0.88f, 0.7f, 0.33f), ActionBarCardMotion.Pulse);

    public ActionBarCardStyle Get(AbilitySampleTier tier)
    {
        switch (tier)
        {
            case AbilitySampleTier.Gold:
                return gold;

            case AbilitySampleTier.Prismatic:
                return GetPrismatic();

            case AbilitySampleTier.Unique:
                return unique;

            default:
                return silver;
        }
    }

    private ActionBarCardStyle GetPrismatic()
    {
        Gradient colors = prismatic.rimColors;

        // The flat rim is only seen where the band cannot show (edit-mode previews).
        ActionBarCardStyle style = new ActionBarCardStyle(
            colors != null ? colors.Evaluate(0f) : Color.white,
            prismatic.body,
            prismatic.innerLine,
            ActionBarCardMotion.Prismatic,
            prismatic.selectGlow);

        style.rimGradient = colors;
        return style;
    }

    // Pastel holographic: pink, violet, sky, mint, gold, back to pink.
    private static Gradient DefaultPrismaticRimColors()
    {
        Gradient gradient = new Gradient();
        Color pink = new Color(1f, 0.55f, 0.8f);

        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(pink, 0f),
                new GradientColorKey(new Color(0.72f, 0.56f, 1f), 0.2f),
                new GradientColorKey(new Color(0.45f, 0.8f, 1f), 0.4f),
                new GradientColorKey(new Color(0.5f, 1f, 0.78f), 0.6f),
                new GradientColorKey(new Color(1f, 0.9f, 0.45f), 0.8f),
                new GradientColorKey(pink, 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });

        return gradient;
    }
}
