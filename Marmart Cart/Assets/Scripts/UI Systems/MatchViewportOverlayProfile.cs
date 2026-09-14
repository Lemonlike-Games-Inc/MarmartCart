using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Shared layout/presentation profile for the viewport-attached match overlay.
///
/// STEP 4:
/// - viewport anchoring;
/// - near-camera render plane;
/// - rounded timer;
/// - shared multi-player leaderboard below timer;
/// - banked score + carried potential score visualization.
///
/// Existing Step 3 timer serialized field names are intentionally preserved.
/// </summary>
[CreateAssetMenu(
    fileName = "MatchViewportOverlayProfile",
    menuName = "Marmart Carts/UI/Match Viewport Overlay Profile")]
public class MatchViewportOverlayProfile : ScriptableObject
{
    #region Root

    [Header("Root Viewport Anchor")]
    [Tooltip(
        "Normalized point inside EACH player's camera viewport. " +
        "(0.5, 1) = exact top-center.")]
    [SerializeField] private Vector2 viewportAnchorNormalized = new Vector2(0.5f, 1f);

    [Header("2 Player Overlay Root")]
    [Tooltip(
        "Pixel offset applied after resolving the viewport anchor. " +
        "Positive X = right. Positive Y = up.")]
    [FormerlySerializedAs("rootOffsetPixels")]
    [SerializeField] private Vector2 twoPlayerRootOffsetPixels = new Vector2(0f, -32f);

    [Tooltip("Master scale applied to the complete viewport overlay in 2P mode.")]
    [FormerlySerializedAs("masterScale")]
    [Min(0.01f)]
    [SerializeField] private float twoPlayerMasterScale = 1f;

    [Header("4 Player Overlay Root")]
    [Tooltip(
        "Pixel offset applied after resolving the viewport anchor in 4P mode. " +
        "Positive X = right. Positive Y = up.")]
    [SerializeField] private Vector2 fourPlayerRootOffsetPixels = new Vector2(0f, -32f);

    [Tooltip("Master scale applied to the complete viewport overlay in 4P mode.")]
    [Min(0.01f)]
    [SerializeField] private float fourPlayerMasterScale = 1f;

    // Version 1 copies the previously shared root offset and master scale into
    // the new 4P fields once. Existing profile assets therefore look identical
    // in both modes until their new 4P values are deliberately tuned.
    private const int CurrentModeSpecificRootLayoutVersion = 1;

    [HideInInspector]
    [SerializeField] private int modeSpecificRootLayoutVersion;

    [System.NonSerialized] private int runtimePlayerCount = 2;

    #endregion

    #region Render Plane

    [Header("Near-Camera Render Plane")]
    [Min(0.001f)]
    [SerializeField] private float nearCameraRenderDistance = 0.5f;

    [Min(0f)]
    [SerializeField] private float nearClipSafetyPadding = 0.03f;

    #endregion

    #region Timer

    [Header("Timer - Position")]
    [Tooltip("Moves the whole timer combination relative to the overlay root.")]
    [SerializeField] private Vector2 timerOffsetPixels = Vector2.zero;

    [Header("Timer - Rounded Background")]
    [Tooltip("Moves only the rounded background relative to the timer center.")]
    [SerializeField] private Vector2 timerBackgroundOffsetPixels = Vector2.zero;

    [Min(1f)]
    [SerializeField] private float timerBackgroundWidthPixels = 112f;

    [Min(1f)]
    [SerializeField] private float timerBackgroundHeightPixels = 48f;

    [SerializeField]
    private Color timerBackgroundColor =
        new Color(0.055f, 0.065f, 0.085f, 0.88f);

    [Header("Timer - Text")]
    [Tooltip("Moves only the text relative to the timer center.")]
    [SerializeField] private Vector2 timerTextOffsetPixels = Vector2.zero;

    [Min(1f)]
    [SerializeField] private float timerFontSizePixels = 30f;

    [SerializeField] private Color timerTextColor = Color.white;

    #endregion

    #region Leaderboard Root

    [Header("Leaderboard - Root")]
    [Tooltip(
        "Independent master scale for leaderboard only. " +
        "The global Master Scale still applies first.")]
    [Min(0.01f)]
    [SerializeField] private float leaderboardMasterScale = 1f;

    [Tooltip(
        "Moves the whole leaderboard after its automatic placement below the timer.")]
    [SerializeField] private Vector2 leaderboardOffsetPixels = Vector2.zero;

    [Tooltip(
        "Master vertical gap from the bottom of the timer background " +
        "to the top of the first leaderboard row.")]
    [Min(0f)]
    [SerializeField] private float timerToLeaderboardPaddingPixels = 16f;

    [Tooltip("Visual height reserved for one leaderboard row.")]
    [Min(1f)]
    [SerializeField] private float leaderboardRowHeightPixels = 22f;

    [Tooltip("Vertical space between leaderboard rows.")]
    [Min(0f)]
    [SerializeField] private float leaderboardRowSpacingPixels = 7f;

    #endregion

    #region Leaderboard Bar

    [Header("Leaderboard - Score Bar")]
    [SerializeField] private Vector2 leaderboardBarOffsetPixels = Vector2.zero;

    [Min(1f)]
    [SerializeField] private float leaderboardBarWidthPixels = 150f;

    [Min(1f)]
    [SerializeField] private float leaderboardBarBackgroundHeightPixels = 18f;

    [Min(1f)]
    [SerializeField] private float leaderboardBarFillHeightPixels = 12f;

    [Tooltip(
        "Empty space kept between both horizontal ends of the outer " +
        "background and every inner score fill.")]
    [Min(0f)]
    [SerializeField] private float leaderboardBarHorizontalPaddingPixels = 3f;

    [Tooltip(
        "Corner radius shared by the leaderboard background and score fills. " +
        "Keep this small for a mostly-rectangular bar with only softened corners.")]
    [Min(0f)]
    [SerializeField] private float leaderboardBarCornerRadiusPixels = 3f;

    [SerializeField]
    private Color leaderboardBarBackgroundColor =
        new Color(0.055f, 0.065f, 0.085f, 0.78f);

    [Header("Leaderboard - Inner Fill Outline")]
    [Tooltip(
        "Color of the larger banked and potential layers drawn behind their " +
        "smaller foreground fills. The exposed edge reads as an outline.")]
    [SerializeField]
    private Color leaderboardBarFillOutlineColor =
        new Color(1f, 1f, 1f, 0.65f);

    [Tooltip(
        "How many pixels the foreground fills are inset on every side, " +
        "revealing the larger colored layers behind them.")]
    [Min(0f)]
    [SerializeField] private float leaderboardBarFillOutlineThicknessPixels = 1f;

    // Kept serialized for backwards compatibility with the Step 4 asset.
    // The renderer now uses the per-player potential colors below.
    [SerializeField, HideInInspector]
    private Color leaderboardPotentialFillColor =
        new Color(1f, 0.78f, 0.18f, 1f);

    [Header("Leaderboard - Banked Player Colors")]
    [SerializeField]
    private Color leaderboardP1FillColor =
        new Color(0.20f, 0.66f, 1f, 1f);

    [SerializeField]
    private Color leaderboardP2FillColor =
        new Color(1f, 0.38f, 0.25f, 1f);

    [SerializeField]
    private Color leaderboardP3FillColor =
        new Color(0.30f, 0.88f, 0.48f, 1f);

    [SerializeField]
    private Color leaderboardP4FillColor =
        new Color(0.72f, 0.42f, 1f, 1f);

    [Header("Leaderboard - Shared Potential Background")]
    [Tooltip(
        "One shared background color drawn beneath every player's potential " +
        "chevrons. Its alpha controls the background opacity directly.")]
    [SerializeField]
    private Color leaderboardPotentialBackgroundColor =
        new Color(0.16f, 0.18f, 0.22f, 0.88f);

    [Header("Leaderboard - Potential Chevron Color Pairs")]
    [Tooltip("P1 chevron Pattern A color.")]
    [SerializeField]
    private Color leaderboardP1PotentialPatternColorA =
        new Color(0.20f, 0.66f, 1f, 1f);

    [Tooltip("P1 chevron Pattern B color.")]
    [FormerlySerializedAs("leaderboardP1PotentialFillColor")]
    [SerializeField]
    private Color leaderboardP1PotentialPatternColorB =
        new Color(0.52f, 0.82f, 1f, 1f);

    [Tooltip("P2 chevron Pattern A color.")]
    [SerializeField]
    private Color leaderboardP2PotentialPatternColorA =
        new Color(1f, 0.38f, 0.25f, 1f);

    [Tooltip("P2 chevron Pattern B color.")]
    [FormerlySerializedAs("leaderboardP2PotentialFillColor")]
    [SerializeField]
    private Color leaderboardP2PotentialPatternColorB =
        new Color(1f, 0.62f, 0.52f, 1f);

    [Tooltip("P3 chevron Pattern A color.")]
    [SerializeField]
    private Color leaderboardP3PotentialPatternColorA =
        new Color(0.30f, 0.88f, 0.48f, 1f);

    [Tooltip("P3 chevron Pattern B color.")]
    [FormerlySerializedAs("leaderboardP3PotentialFillColor")]
    [SerializeField]
    private Color leaderboardP3PotentialPatternColorB =
        new Color(0.56f, 1f, 0.68f, 1f);

    [Tooltip("P4 chevron Pattern A color.")]
    [SerializeField]
    private Color leaderboardP4PotentialPatternColorA =
        new Color(0.72f, 0.42f, 1f, 1f);

    [Tooltip("P4 chevron Pattern B color.")]
    [FormerlySerializedAs("leaderboardP4PotentialFillColor")]
    [SerializeField]
    private Color leaderboardP4PotentialPatternColorB =
        new Color(0.86f, 0.68f, 1f, 1f);

    [Header("Leaderboard - Streak Reward Chevron Highlight")]
    [Tooltip(
        "Global Pattern A color used only by the right-end portion of potential " +
        "score that comes from the projected streak or milestone reward.")]
    [SerializeField]
    private Color leaderboardStreakRewardPatternColorA =
        new Color(1f, 0.72f, 0.10f, 1f);

    [Tooltip(
        "Global Pattern B color used only by the right-end portion of potential " +
        "score that comes from the projected streak or milestone reward.")]
    [SerializeField]
    private Color leaderboardStreakRewardPatternColorB =
        new Color(1f, 0.94f, 0.52f, 1f);

    [Header("Leaderboard - Potential Chevron Pattern")]
    // Preserved so existing Step 4.2 profile assets do not lose serialized data.
    // The shared Potential Background color now owns its opacity directly.
    [HideInInspector]
    [SerializeField] private float leaderboardPotentialPatternBaseOpacity = 0.38f;

    [Tooltip("Thickness of each chevron line segment in pixels.")]
    [Min(0.25f)]
    [SerializeField] private float leaderboardPotentialPatternLineThicknessPixels = 2f;

    [Tooltip(
        "Horizontal distance from a chevron's top/bottom origin to its " +
        "middle point. This controls how far the arrow points to the right.")]
    [Min(0.25f)]
    [SerializeField] private float leaderboardPotentialPatternWidthPixels = 6f;

    [Tooltip(
        "Horizontal distance between the origins of consecutive chevrons. " +
        "This is a fixed pixel pitch, so the pattern never stretches with score.")]
    [Min(0.25f)]
    [SerializeField] private float leaderboardPotentialPatternSpacingPixels = 9f;

    [Tooltip(
        "Extra empty space above and below the chevrons inside the potential fill.")]
    [Min(0f)]
    [SerializeField] private float leaderboardPotentialPatternVerticalPaddingPixels = 0f;

    #endregion

    #region Leaderboard Text

    [Header("Leaderboard - Player Label Group")]
    [Tooltip(
        "Scales the player-label background and text together around their " +
        "shared center.")]
    [Min(0.01f)]
    [SerializeField] private float leaderboardPlayerLabelGroupScale = 1f;

    [Tooltip(
        "Horizontal gap from the score bar's left edge to the player-label " +
        "background's nearest edge.")]
    [Min(0f)]
    [SerializeField] private float leaderboardPlayerLabelGapPixels = 14f;

    [Tooltip("Moves the player-label background and text together.")]
    [FormerlySerializedAs("leaderboardPlayerLabelExtraOffsetPixels")]
    [SerializeField] private Vector2 leaderboardPlayerLabelGroupOffsetPixels = Vector2.zero;

    [Tooltip("Unscaled width and height of the player-label background.")]
    [SerializeField]
    private Vector2 leaderboardPlayerLabelBackgroundSizePixels =
        new Vector2(48f, 22f);

    [Min(0f)]
    [SerializeField] private float leaderboardPlayerLabelBackgroundCornerRadiusPixels = 5f;

    // Preserved as the fallback for invalid/unsupported player indices and for
    // compatibility with earlier profile assets.
    [SerializeField, HideInInspector]
    private Color leaderboardPlayerLabelBackgroundColor =
        new Color(0.055f, 0.065f, 0.085f, 0.88f);

    [Header("Leaderboard - Player Label Colors")]
    [Tooltip("P1 player-label background color in other players' viewports.")]
    [SerializeField]
    private Color leaderboardP1LabelBackgroundColor =
        new Color(0.20f, 0.66f, 1f, 1f);

    [Tooltip("P2 player-label background color in other players' viewports.")]
    [SerializeField]
    private Color leaderboardP2LabelBackgroundColor =
        new Color(1f, 0.38f, 0.25f, 1f);

    [Tooltip("P3 player-label background color in other players' viewports.")]
    [SerializeField]
    private Color leaderboardP3LabelBackgroundColor =
        new Color(0.30f, 0.88f, 0.48f, 1f);

    [Tooltip("P4 player-label background color in other players' viewports.")]
    [SerializeField]
    private Color leaderboardP4LabelBackgroundColor =
        new Color(0.72f, 0.42f, 1f, 1f);

    [Min(1f)]
    [SerializeField] private float leaderboardPlayerLabelFontSizePixels = 18f;

    [Header("Leaderboard - Rank Group")]
    [Tooltip(
        "Scales the rank background and text together around their shared " +
        "center.")]
    [Min(0.01f)]
    [SerializeField] private float leaderboardRankGroupScale = 1f;

    [Tooltip(
        "Horizontal gap from the score bar's right edge to the rank " +
        "background's nearest edge.")]
    [Min(0f)]
    [SerializeField] private float leaderboardRankTextGapPixels = 16f;

    [Tooltip("Moves the rank background and text together.")]
    [FormerlySerializedAs("leaderboardRankTextExtraOffsetPixels")]
    [SerializeField] private Vector2 leaderboardRankGroupOffsetPixels = Vector2.zero;

    [Tooltip("Unscaled width and height of the rank background.")]
    [SerializeField]
    private Vector2 leaderboardRankBackgroundSizePixels =
        new Vector2(42f, 22f);

    [Min(0f)]
    [SerializeField] private float leaderboardRankBackgroundCornerRadiusPixels = 5f;

    [SerializeField]
    private Color leaderboardRankBackgroundColor =
        new Color(0.055f, 0.065f, 0.085f, 0.88f);

    [Min(1f)]
    [SerializeField] private float leaderboardRankFontSizePixels = 18f;

    [Header("Leaderboard - Shared Text Color")]
    [SerializeField] private Color leaderboardTextColor = Color.white;

    [Header("Leaderboard - YOU Color Set")]
    [Tooltip(
        "Overrides the outer score-bar background for the player viewing " +
        "their own leaderboard entry.")]
    [SerializeField]
    private Color leaderboardYouBarBackgroundColor =
        new Color(0.08f, 0.17f, 0.28f, 0.96f);

    [Tooltip(
        "Overrides the player-label background when that label reads YOU.")]
    [SerializeField]
    private Color leaderboardYouPlayerLabelBackgroundColor =
        new Color(0.16f, 0.55f, 0.92f, 0.96f);

    [Tooltip(
        "Overrides the rank background on the viewer's own leaderboard entry.")]
    [SerializeField]
    private Color leaderboardYouRankBackgroundColor =
        new Color(0.16f, 0.55f, 0.92f, 0.96f);

    [Tooltip(
        "Overrides both the YOU label text and rank text colors on the " +
        "viewer's own leaderboard entry.")]
    [SerializeField]
    private Color leaderboardYouTextColor = Color.white;

    #endregion

    #region Leaderboard Normalization

    [Header("Leaderboard - Dynamic Global Maximum")]
    [Tooltip(
        "First tier for the shared score-bar maximum. " +
        "The bar remains on this tier until a player's combined value exceeds it.")]
    [Min(1)]
    [SerializeField] private int leaderboardStartingGlobalMaximum = 20;

    [Tooltip(
        "Amount added whenever a player's combined score exceeds the current tier. " +
        "For example, Starting 20 + Increment 20 produces 20, 40, 60, 80...")]
    [Min(1)]
    [SerializeField] private int leaderboardGlobalMaximumIncrement = 20;

    #endregion

    #region Leaderboard Animation

    [Header("Leaderboard - Bar Animation")]
    [Tooltip(
        "Animates banked, potential, and global-maximum bar movements. " +
        "Disable to make every score-bar change snap immediately.")]
    [SerializeField] private bool leaderboardBarAnimationEnabled = true;

    [Tooltip(
        "Seconds taken for a bar endpoint to reach its newest value. " +
        "Smaller values are faster. Uses unscaled time.")]
    [Min(0f)]
    [SerializeField] private float leaderboardBarAnimationDurationSeconds = 0.35f;

    [Tooltip(
        "Ease applied from the currently displayed endpoint to its newest " +
        "target. The default moves quickly first, then settles slowly.")]
    [SerializeField]
    private AnimationCurve leaderboardBarAnimationEasing =
        CreateDefaultLeaderboardBarAnimationEasing();

    // Version 1 migrates the earlier symmetric EaseInOut default to the new
    // fast-start / slow-finish curve exactly once, without overwriting later
    // Inspector tuning.
    private const int CurrentLeaderboardBarAnimationCurveVersion = 1;

    [HideInInspector]
    [SerializeField] private int leaderboardBarAnimationCurveVersion;

    [Header("Leaderboard - Entry Reorder Animation")]
    [Tooltip(
        "Animates complete leaderboard entries between row positions when " +
        "their sorted order changes.")]
    [SerializeField] private bool leaderboardEntryReorderAnimationEnabled = true;

    [Tooltip(
        "Seconds taken for an entry to reach its new row. Smaller values are " +
        "faster. Uses unscaled time and the same fast-start/slow-finish curve " +
        "as the score bars.")]
    [Min(0f)]
    [SerializeField] private float leaderboardEntryReorderAnimationDurationSeconds = 0.35f;

    #endregion

    #region Public API

    public Vector2 ViewportAnchorNormalized => viewportAnchorNormalized;

    public Vector2 RootOffsetPixels =>
        GetRootOffsetPixels(runtimePlayerCount) *
        MasterScale;

    public float MasterScale =>
        GetMasterScale(runtimePlayerCount);

    public int RuntimePlayerCount => runtimePlayerCount;

    public Vector2 GetRootOffsetPixels(int playerCount)
    {
        return
            playerCount <= 2
                ? twoPlayerRootOffsetPixels
                : fourPlayerRootOffsetPixels;
    }

    public float GetMasterScale(int playerCount)
    {
        return
            Mathf.Max(
                0.01f,
                playerCount <= 2
                    ? twoPlayerMasterScale
                    : fourPlayerMasterScale
            );
    }

    /// <summary>
    /// Selects which serialized 2P/4P layout pair all scaled profile getters
    /// expose. The scene-mode controller owns when this runtime choice changes.
    /// </summary>
    public void SetRuntimePlayerCount(int playerCount)
    {
        runtimePlayerCount =
            playerCount <= 2
                ? 2
                : 4;
    }

    public float NearCameraRenderDistance => nearCameraRenderDistance;
    public float NearClipSafetyPadding => nearClipSafetyPadding;

    public Vector2 TimerOffsetPixels =>
        timerOffsetPixels * MasterScale;

    public Vector2 TimerBackgroundOffsetPixels =>
        timerBackgroundOffsetPixels * MasterScale;

    public float TimerBackgroundWidthPixels =>
        timerBackgroundWidthPixels * MasterScale;

    public float TimerBackgroundHeightPixels =>
        timerBackgroundHeightPixels * MasterScale;

    public Color TimerBackgroundColor => timerBackgroundColor;

    public Vector2 TimerTextOffsetPixels =>
        timerTextOffsetPixels * MasterScale;

    public float TimerFontSizePixels =>
        timerFontSizePixels * MasterScale;

    public Color TimerTextColor => timerTextColor;

    private float EffectiveLeaderboardScale =>
        MasterScale * leaderboardMasterScale;

    public float LeaderboardMasterScale =>
        leaderboardMasterScale;

    public Vector2 LeaderboardOffsetPixels =>
        leaderboardOffsetPixels * EffectiveLeaderboardScale;

    public float TimerToLeaderboardPaddingPixels =>
        timerToLeaderboardPaddingPixels * EffectiveLeaderboardScale;

    public float LeaderboardRowHeightPixels =>
        leaderboardRowHeightPixels * EffectiveLeaderboardScale;

    public float LeaderboardRowSpacingPixels =>
        leaderboardRowSpacingPixels * EffectiveLeaderboardScale;

    public Vector2 LeaderboardBarOffsetPixels =>
        leaderboardBarOffsetPixels * EffectiveLeaderboardScale;

    public float LeaderboardBarWidthPixels =>
        leaderboardBarWidthPixels * EffectiveLeaderboardScale;

    public float LeaderboardBarBackgroundHeightPixels =>
        leaderboardBarBackgroundHeightPixels * EffectiveLeaderboardScale;

    public float LeaderboardBarFillHeightPixels =>
        leaderboardBarFillHeightPixels * EffectiveLeaderboardScale;

    public float LeaderboardBarHorizontalPaddingPixels =>
        leaderboardBarHorizontalPaddingPixels * EffectiveLeaderboardScale;

    public float LeaderboardBarCornerRadiusPixels =>
        leaderboardBarCornerRadiusPixels * EffectiveLeaderboardScale;

    public Color LeaderboardBarBackgroundColor =>
        leaderboardBarBackgroundColor;

    public Color LeaderboardBarFillOutlineColor =>
        leaderboardBarFillOutlineColor;

    public float LeaderboardBarFillOutlineThicknessPixels =>
        leaderboardBarFillOutlineThicknessPixels *
        EffectiveLeaderboardScale;

    // Legacy Step 4 shared color getter retained so other code does not break.
    public Color LeaderboardPotentialFillColor =>
        leaderboardPotentialFillColor;

    public Color LeaderboardPotentialBackgroundColor =>
        leaderboardPotentialBackgroundColor;

    public float LeaderboardPotentialPatternLineThicknessPixels =>
        leaderboardPotentialPatternLineThicknessPixels *
        EffectiveLeaderboardScale;

    public float LeaderboardPotentialPatternWidthPixels =>
        leaderboardPotentialPatternWidthPixels *
        EffectiveLeaderboardScale;

    public float LeaderboardPotentialPatternSpacingPixels =>
        leaderboardPotentialPatternSpacingPixels *
        EffectiveLeaderboardScale;

    public float LeaderboardPotentialPatternVerticalPaddingPixels =>
        leaderboardPotentialPatternVerticalPaddingPixels *
        EffectiveLeaderboardScale;

    public float LeaderboardPlayerLabelGapPixels =>
        leaderboardPlayerLabelGapPixels * EffectiveLeaderboardScale;

    private float EffectivePlayerLabelGroupScale =>
        EffectiveLeaderboardScale *
        Mathf.Max(
            0.01f,
            leaderboardPlayerLabelGroupScale
        );

    public Vector2 LeaderboardPlayerLabelGroupOffsetPixels =>
        leaderboardPlayerLabelGroupOffsetPixels *
        EffectiveLeaderboardScale;

    // Legacy API retained; this offset now moves the complete label group.
    public Vector2 LeaderboardPlayerLabelExtraOffsetPixels =>
        LeaderboardPlayerLabelGroupOffsetPixels;

    public Vector2 LeaderboardPlayerLabelBackgroundSizePixels =>
        leaderboardPlayerLabelBackgroundSizePixels *
        EffectivePlayerLabelGroupScale;

    public float LeaderboardPlayerLabelBackgroundCornerRadiusPixels =>
        leaderboardPlayerLabelBackgroundCornerRadiusPixels *
        EffectivePlayerLabelGroupScale;

    public Color LeaderboardPlayerLabelBackgroundColor =>
        leaderboardPlayerLabelBackgroundColor;

    public float LeaderboardPlayerLabelFontSizePixels =>
        leaderboardPlayerLabelFontSizePixels *
        EffectivePlayerLabelGroupScale;

    public float LeaderboardRankTextGapPixels =>
        leaderboardRankTextGapPixels * EffectiveLeaderboardScale;

    private float EffectiveRankGroupScale =>
        EffectiveLeaderboardScale *
        Mathf.Max(
            0.01f,
            leaderboardRankGroupScale
        );

    public Vector2 LeaderboardRankGroupOffsetPixels =>
        leaderboardRankGroupOffsetPixels *
        EffectiveLeaderboardScale;

    // Legacy API retained; this offset now moves the complete rank group.
    public Vector2 LeaderboardRankTextExtraOffsetPixels =>
        LeaderboardRankGroupOffsetPixels;

    public Vector2 LeaderboardRankBackgroundSizePixels =>
        leaderboardRankBackgroundSizePixels *
        EffectiveRankGroupScale;

    public float LeaderboardRankBackgroundCornerRadiusPixels =>
        leaderboardRankBackgroundCornerRadiusPixels *
        EffectiveRankGroupScale;

    public Color LeaderboardRankBackgroundColor =>
        leaderboardRankBackgroundColor;

    public float LeaderboardRankFontSizePixels =>
        leaderboardRankFontSizePixels *
        EffectiveRankGroupScale;

    public Color LeaderboardTextColor =>
        leaderboardTextColor;

    public Color GetLeaderboardBarBackgroundColor(
        bool isLocalPlayer)
    {
        return
            isLocalPlayer
                ? leaderboardYouBarBackgroundColor
                : leaderboardBarBackgroundColor;
    }

    public Color GetLeaderboardPlayerLabelBackgroundColor(
        bool isLocalPlayer)
    {
        return
            isLocalPlayer
                ? leaderboardYouPlayerLabelBackgroundColor
                : leaderboardPlayerLabelBackgroundColor;
    }

    public Color GetLeaderboardPlayerLabelBackgroundColor(
        int playerIndex,
        bool isLocalPlayer)
    {
        if (isLocalPlayer)
        {
            return
                leaderboardYouPlayerLabelBackgroundColor;
        }

        switch (playerIndex)
        {
            case 1:
                return leaderboardP1LabelBackgroundColor;

            case 2:
                return leaderboardP2LabelBackgroundColor;

            case 3:
                return leaderboardP3LabelBackgroundColor;

            case 4:
                return leaderboardP4LabelBackgroundColor;

            default:
                return leaderboardPlayerLabelBackgroundColor;
        }
    }

    public Color GetLeaderboardRankBackgroundColor(
        bool isLocalPlayer)
    {
        return
            isLocalPlayer
                ? leaderboardYouRankBackgroundColor
                : leaderboardRankBackgroundColor;
    }

    public Color GetLeaderboardTextColor(
        bool isLocalPlayer)
    {
        return
            isLocalPlayer
                ? leaderboardYouTextColor
                : leaderboardTextColor;
    }

    public int LeaderboardStartingGlobalMaximum =>
        Mathf.Max(1, leaderboardStartingGlobalMaximum);

    public int LeaderboardGlobalMaximumIncrement =>
        Mathf.Max(1, leaderboardGlobalMaximumIncrement);

    public bool LeaderboardBarAnimationEnabled =>
        leaderboardBarAnimationEnabled;

    public float LeaderboardBarAnimationDurationSeconds =>
        Mathf.Max(
            0f,
            leaderboardBarAnimationDurationSeconds
        );

    public float EvaluateLeaderboardBarAnimationEasing(
        float normalizedTime)
    {
        float safeTime =
            Mathf.Clamp01(
                normalizedTime
            );

        if (leaderboardBarAnimationCurveVersion <
            CurrentLeaderboardBarAnimationCurveVersion)
        {
            return
                EvaluateDefaultLeaderboardBarAnimationEasing(
                    safeTime
                );
        }

        if (leaderboardBarAnimationEasing == null ||
            leaderboardBarAnimationEasing.length <= 0)
        {
            return
                EvaluateDefaultLeaderboardBarAnimationEasing(
                    safeTime
                );
        }

        return
            Mathf.Clamp01(
                leaderboardBarAnimationEasing
                    .Evaluate(
                        safeTime
                    )
            );
    }

    public bool LeaderboardEntryReorderAnimationEnabled =>
        leaderboardEntryReorderAnimationEnabled;

    public float LeaderboardEntryReorderAnimationDurationSeconds =>
        Mathf.Max(
            0f,
            leaderboardEntryReorderAnimationDurationSeconds
        );

    public float EvaluateLeaderboardEntryReorderAnimationEasing(
        float normalizedTime)
    {
        // Reuse the same authored curve so bar changes and rank changes share
        // one coherent fast-start / slow-finish motion language.
        return
            EvaluateLeaderboardBarAnimationEasing(
                normalizedTime
            );
    }

    public Color GetLeaderboardPlayerFillColor(
        int playerIndex)
    {
        switch (playerIndex)
        {
            case 1:
                return leaderboardP1FillColor;

            case 2:
                return leaderboardP2FillColor;

            case 3:
                return leaderboardP3FillColor;

            case 4:
                return leaderboardP4FillColor;

            default:
                return Color.white;
        }
    }

    public Color GetLeaderboardPlayerPotentialFillColor(
        int playerIndex)
    {
        switch (playerIndex)
        {
            case 1:
                return leaderboardP1PotentialPatternColorB;

            case 2:
                return leaderboardP2PotentialPatternColorB;

            case 3:
                return leaderboardP3PotentialPatternColorB;

            case 4:
                return leaderboardP4PotentialPatternColorB;

            default:
                return leaderboardPotentialFillColor;
        }
    }

    public Color GetLeaderboardPlayerPotentialBaseColor(
        int playerIndex)
    {
        return leaderboardPotentialBackgroundColor;
    }

    /// <summary>
    /// Returns the independently authored A/B chevron colors for one player.
    /// </summary>
    public Color GetLeaderboardPlayerPotentialPatternColor(
        int playerIndex,
        bool useAlternateColor)
    {
        if (useAlternateColor)
        {
            return
                GetLeaderboardPlayerPotentialFillColor(
                    playerIndex
                );
        }

        switch (playerIndex)
        {
            case 1:
                return leaderboardP1PotentialPatternColorA;

            case 2:
                return leaderboardP2PotentialPatternColorA;

            case 3:
                return leaderboardP3PotentialPatternColorA;

            case 4:
                return leaderboardP4PotentialPatternColorA;

            default:
                return Color.white;
        }
    }

    /// <summary>
    /// Returns the shared A/B chevron colors that identify projected streak
    /// reward value at the right end of every player's potential interval.
    /// </summary>
    public Color GetLeaderboardStreakRewardPatternColor(
        bool useAlternateColor)
    {
        return
            useAlternateColor
                ? leaderboardStreakRewardPatternColorB
                : leaderboardStreakRewardPatternColorA;
    }

    #endregion

    #region Validation

    private void OnEnable()
    {
        UpgradeModeSpecificRootLayoutIfNeeded();
    }

    private void OnValidate()
    {
        UpgradeModeSpecificRootLayoutIfNeeded();

        twoPlayerMasterScale =
            Mathf.Max(
                0.01f,
                twoPlayerMasterScale
            );

        fourPlayerMasterScale =
            Mathf.Max(
                0.01f,
                fourPlayerMasterScale
            );

        nearCameraRenderDistance =
            Mathf.Max(0.001f, nearCameraRenderDistance);

        nearClipSafetyPadding =
            Mathf.Max(0f, nearClipSafetyPadding);

        timerBackgroundWidthPixels =
            Mathf.Max(1f, timerBackgroundWidthPixels);

        timerBackgroundHeightPixels =
            Mathf.Max(1f, timerBackgroundHeightPixels);

        timerFontSizePixels =
            Mathf.Max(1f, timerFontSizePixels);

        leaderboardMasterScale =
            Mathf.Max(0.01f, leaderboardMasterScale);

        timerToLeaderboardPaddingPixels =
            Mathf.Max(0f, timerToLeaderboardPaddingPixels);

        leaderboardRowHeightPixels =
            Mathf.Max(1f, leaderboardRowHeightPixels);

        leaderboardRowSpacingPixels =
            Mathf.Max(0f, leaderboardRowSpacingPixels);

        leaderboardBarWidthPixels =
            Mathf.Max(1f, leaderboardBarWidthPixels);

        leaderboardBarBackgroundHeightPixels =
            Mathf.Max(1f, leaderboardBarBackgroundHeightPixels);

        leaderboardBarFillHeightPixels =
            Mathf.Clamp(
                leaderboardBarFillHeightPixels,
                1f,
                leaderboardBarBackgroundHeightPixels
            );

        leaderboardBarHorizontalPaddingPixels =
            Mathf.Clamp(
                leaderboardBarHorizontalPaddingPixels,
                0f,
                Mathf.Max(
                    0f,
                    (leaderboardBarWidthPixels - 1f) * 0.5f
                )
            );

        leaderboardBarCornerRadiusPixels =
            Mathf.Clamp(
                leaderboardBarCornerRadiusPixels,
                0f,
                Mathf.Min(
                    leaderboardBarBackgroundHeightPixels,
                    leaderboardBarFillHeightPixels
                ) * 0.5f
            );

        leaderboardBarFillOutlineThicknessPixels =
            Mathf.Clamp(
                leaderboardBarFillOutlineThicknessPixels,
                0f,
                leaderboardBarFillHeightPixels * 0.5f
            );

        leaderboardPotentialPatternBaseOpacity =
            Mathf.Clamp01(
                leaderboardPotentialPatternBaseOpacity
            );

        leaderboardPotentialPatternLineThicknessPixels =
            Mathf.Max(
                0.25f,
                leaderboardPotentialPatternLineThicknessPixels
            );

        leaderboardPotentialPatternWidthPixels =
            Mathf.Max(
                0.25f,
                leaderboardPotentialPatternWidthPixels
            );

        leaderboardPotentialPatternSpacingPixels =
            Mathf.Max(
                0.25f,
                leaderboardPotentialPatternSpacingPixels
            );

        leaderboardPotentialPatternVerticalPaddingPixels =
            Mathf.Clamp(
                leaderboardPotentialPatternVerticalPaddingPixels,
                0f,
                leaderboardBarFillHeightPixels * 0.5f
            );

        leaderboardPlayerLabelGapPixels =
            Mathf.Max(0f, leaderboardPlayerLabelGapPixels);

        leaderboardPlayerLabelGroupScale =
            Mathf.Max(
                0.01f,
                leaderboardPlayerLabelGroupScale
            );

        leaderboardPlayerLabelBackgroundSizePixels =
            new Vector2(
                Mathf.Max(
                    1f,
                    leaderboardPlayerLabelBackgroundSizePixels.x
                ),
                Mathf.Max(
                    1f,
                    leaderboardPlayerLabelBackgroundSizePixels.y
                )
            );

        leaderboardPlayerLabelBackgroundCornerRadiusPixels =
            Mathf.Clamp(
                leaderboardPlayerLabelBackgroundCornerRadiusPixels,
                0f,
                Mathf.Min(
                    leaderboardPlayerLabelBackgroundSizePixels.x,
                    leaderboardPlayerLabelBackgroundSizePixels.y
                ) * 0.5f
            );

        leaderboardPlayerLabelFontSizePixels =
            Mathf.Max(1f, leaderboardPlayerLabelFontSizePixels);

        leaderboardRankTextGapPixels =
            Mathf.Max(0f, leaderboardRankTextGapPixels);

        leaderboardRankGroupScale =
            Mathf.Max(
                0.01f,
                leaderboardRankGroupScale
            );

        leaderboardRankBackgroundSizePixels =
            new Vector2(
                Mathf.Max(
                    1f,
                    leaderboardRankBackgroundSizePixels.x
                ),
                Mathf.Max(
                    1f,
                    leaderboardRankBackgroundSizePixels.y
                )
            );

        leaderboardRankBackgroundCornerRadiusPixels =
            Mathf.Clamp(
                leaderboardRankBackgroundCornerRadiusPixels,
                0f,
                Mathf.Min(
                    leaderboardRankBackgroundSizePixels.x,
                    leaderboardRankBackgroundSizePixels.y
                ) * 0.5f
            );

        leaderboardRankFontSizePixels =
            Mathf.Max(1f, leaderboardRankFontSizePixels);

        leaderboardStartingGlobalMaximum =
            Mathf.Max(1, leaderboardStartingGlobalMaximum);

        leaderboardGlobalMaximumIncrement =
            Mathf.Max(1, leaderboardGlobalMaximumIncrement);

        leaderboardBarAnimationDurationSeconds =
            Mathf.Max(
                0f,
                leaderboardBarAnimationDurationSeconds
            );

        leaderboardEntryReorderAnimationDurationSeconds =
            Mathf.Max(
                0f,
                leaderboardEntryReorderAnimationDurationSeconds
            );

        if (leaderboardBarAnimationCurveVersion <
            CurrentLeaderboardBarAnimationCurveVersion)
        {
            leaderboardBarAnimationEasing =
                CreateDefaultLeaderboardBarAnimationEasing();

            leaderboardBarAnimationCurveVersion =
                CurrentLeaderboardBarAnimationCurveVersion;
        }
        else if (leaderboardBarAnimationEasing == null ||
            leaderboardBarAnimationEasing.length <= 0)
        {
            leaderboardBarAnimationEasing =
                CreateDefaultLeaderboardBarAnimationEasing();
        }
    }

    private void UpgradeModeSpecificRootLayoutIfNeeded()
    {
        if (modeSpecificRootLayoutVersion >=
            CurrentModeSpecificRootLayoutVersion)
        {
            return;
        }

        fourPlayerRootOffsetPixels =
            twoPlayerRootOffsetPixels;

        fourPlayerMasterScale =
            twoPlayerMasterScale;

        modeSpecificRootLayoutVersion =
            CurrentModeSpecificRootLayoutVersion;
    }

    private static AnimationCurve
        CreateDefaultLeaderboardBarAnimationEasing()
    {
        // These Hermite tangents form the cubic ease-out curve
        // 1 - (1 - t)^3: high velocity immediately, approaching zero at the end.
        return
            new AnimationCurve(
                new Keyframe(
                    0f,
                    0f,
                    3f,
                    3f
                ),
                new Keyframe(
                    1f,
                    1f,
                    0f,
                    0f
                )
            );
    }

    private static float
        EvaluateDefaultLeaderboardBarAnimationEasing(
            float normalizedTime)
    {
        float remaining =
            1f -
            Mathf.Clamp01(
                normalizedTime
            );

        return
            1f -
            remaining *
            remaining *
            remaining;
    }

    #endregion
}
