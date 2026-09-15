using UnityEngine;

/// <summary>
/// Step 5G.1.2 visual profile - simplified low-speed warning cart/weight icon.
///
/// LOAD now supports:
/// - minimum 10-slot visual baseline;
/// - 20-slot rolling capacity windows;
/// - dynamic capsule thickness;
/// - Pre/Empty/Filled/Overload states;
/// - decade milestone ticks;
/// - moving current-load tick + numeric label;
/// - looping overload pointer;
/// - overload severity gradient.
///
/// HYPE remains the stable background + current fill from Step 5B.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Player HUD/World HUD Layout Profile",
    fileName = "PlayerWorldHUDLayoutProfile"
)]
public class PlayerWorldHUDLayoutProfile : ScriptableObject
{
    #region Pair Placement

    [Header("Pair Placement")]
    [Min(0.01f)]
    [SerializeField] private float masterScale = 1f;

    [Min(0f)]
    [SerializeField] private float horizontalSeparationPixels = 24f;

    [SerializeField] private float verticalOffsetPixels = 0f;

    #endregion

    #region Move Backward Prompt

    [Header("Move Backward Prompt - Group")]
    [Tooltip(
        "Allows the renderer to show the prompt while the player's semantic " +
        "Can Move Backward state is true.")]
    [SerializeField] private bool showMoveBackwardPrompt = true;

    [Tooltip(
        "Group position relative to HUDWorldAnchor in screen pixels. " +
        "Positive X = right, positive Y = up.")]
    [SerializeField]
    private Vector2 moveBackwardPromptOffsetPixels =
        new Vector2(0f, -105f);

    [Tooltip(
        "Uniform scale for the complete prompt. Does not move the group position.")]
    [Min(0.05f)]
    [SerializeField] private float moveBackwardPromptMasterScale = 1f;

    [Header("Move Backward Prompt - Background")]
    [SerializeField]
    private Vector2 moveBackwardPromptBackgroundOffsetPixels =
        Vector2.zero;

    [SerializeField]
    private Vector2 moveBackwardPromptBackgroundSizePixels =
        new Vector2(150f, 38f);

    [Min(0f)]
    [SerializeField] private float moveBackwardPromptCornerRadiusPixels = 10f;

    [SerializeField]
    private Color moveBackwardPromptBackgroundColor =
        new Color(0.05f, 0.06f, 0.08f, 0.86f);

    [Header("Move Backward Prompt - Text")]
    [SerializeField] private string moveBackwardPromptText = "MOVE BACKWARD";

    [SerializeField]
    private Vector2 moveBackwardPromptTextOffsetPixels =
        Vector2.zero;

    [Min(1f)]
    [SerializeField] private float moveBackwardPromptFontSizePixels = 18f;

    [SerializeField] private Color moveBackwardPromptTextColor = Color.white;

    #endregion

    #region Render Plane

    [Header("Near-Camera Render Plane")]
    [Tooltip(
        "Render the complete world HUD on a fixed plane close to each gameplay camera, " +
        "while preserving the cart HUDWorldAnchor's screen X/Y. " +
        "This lets normal depth testing keep the HUD in front of level geometry without a ZTest override."
    )]
    [SerializeField] private bool useNearCameraRenderPlane = true;

    [Tooltip(
        "Distance in world units from the gameplay camera to the HUD render plane. " +
        "Automatically clamped beyond that camera's Near Clip Plane."
    )]
    [Min(0.01f)]
    [SerializeField] private float nearCameraRenderDistance = 0.5f;

    [Tooltip("Minimum safety distance beyond the camera Near Clip Plane.")]
    [Min(0.001f)]
    [SerializeField] private float nearClipSafetyPadding = 0.03f;

    #endregion

    #region Load Shell

    [Header("LOAD Meter - Left Shell")]
    [Min(1f)]
    [SerializeField] private float loadRadiusPixels = 82f;

    [Min(1f)]
    [SerializeField] private float loadTrackThicknessPixels = 38f;

    [Range(10f, 300f)]
    [SerializeField] private float loadSpanDegrees = 120f;

    [SerializeField] private float loadCenterAngleDegrees = 180f;

    #endregion

    #region Load Capsules

    [Header("Load Capsule Geometry")]
    [Tooltip("Radial length of each capsule across the Load track.")]
    [Min(1f)]
    [SerializeField] private float loadCapsuleLengthPixels = 15f;

    [Header("Load Visible Range")]
    [Tooltip("Minimum number of capsule positions shown before larger capacity exists.")]
    [Min(1)]
    [SerializeField] private int minimumVisualSlotCount = 10;

    [Tooltip(
        "Rolling window size once capacity exceeds the first page. " +
        "The intended design is 20."
    )]
    [Min(2)]
    [SerializeField] private int loadWindowSize = 20;

    [Header("Dynamic Capsule Thickness - Lerp Milestones")]
    [Min(1f)]
    [SerializeField] private float capsuleThicknessAt10Slots = 12f;

    [Min(1f)]
    [SerializeField] private float capsuleThicknessAt15Slots = 9f;

    [Min(1f)]
    [SerializeField] private float capsuleThicknessAt20Slots = 6f;

    [Header("Load Colors")]
    [SerializeField] private Color loadTrackColor = new Color(0.20f, 0.24f, 0.30f, 0.40f);

    [Tooltip("Subtle placeholder for capacity that has not been earned yet.")]
    [SerializeField] private Color loadPreSlotColor = new Color(0.52f, 0.56f, 0.62f, 0.12f);

    [Tooltip("Earned but unloaded Safe Capacity.")]
    [SerializeField] private Color loadEmptyColor = new Color(0.65f, 0.70f, 0.78f, 0.40f);

    [Tooltip("Loaded non-overload cargo.")]
    [SerializeField] private Color loadFilledColor = new Color(0.10f, 0.92f, 1f, 1f);

    #endregion

    #region Overload

    [Header("Load Overload Severity")]
    [Tooltip(
        "Overall overload amount maps through this Gradient. " +
        "Suggested: yellow-orange -> orange -> red -> deep red."
    )]
    [SerializeField] private Gradient loadOverloadSeverityGradient = CreateDefaultOverloadGradient();

    [Tooltip("Overload amount at which the gradient reaches its final/deepest color.")]
    [Min(1f)]
    [SerializeField] private float overloadAmountForMaximumSeverity = 30f;

    #endregion

    #region Load Ticks

    [Header("Load Milestone Ticks")]
    [SerializeField] private bool showLoadMilestoneTicks = true;

    [Tooltip("Milestone interval. 10 produces 10, 20, 30, 40, ...")]
    [Min(1)]
    [SerializeField] private int loadMilestoneInterval = 10;

    [Min(1f)]
    [SerializeField] private float loadMilestoneTickLengthPixels = 13f;

    [Min(0.5f)]
    [SerializeField] private float loadMilestoneTickThicknessPixels = 2f;

    [SerializeField] private Color loadMilestoneTickColor = new Color(0.82f, 0.86f, 0.92f, 0.65f);

    [Tooltip("Independent text color for the 10 / 20 / 30 / ... milestone labels.")]
    [SerializeField] private Color loadMilestoneTextColor = new Color(0.88f, 0.92f, 1f, 0.90f);

    [Min(1f)]
    [SerializeField] private float loadMilestoneFontSizePixels = 12f;

    [Min(0f)]
    [SerializeField] private float loadMilestoneLabelOffsetPixels = 6f;

    [Header("Current Load Tick")]
    [SerializeField] private bool showCurrentLoadTick = true;

    [Min(1f)]
    [SerializeField] private float currentLoadTickLengthPixels = 19f;

    [Min(0.5f)]
    [SerializeField] private float currentLoadTickThicknessPixels = 3f;

    [SerializeField] private Color currentLoadTickColor = Color.white;

    [Tooltip("Independent text color for the moving Current Load number.")]
    [SerializeField] private Color currentLoadTextColor = new Color(1f, 0.95f, 0.72f, 1f);

    [Min(1f)]
    [SerializeField] private float currentLoadFontSizePixels = 15f;

    [Min(0f)]
    [SerializeField] private float currentLoadLabelOffsetPixels = 8f;


    #endregion

    #region Hype Meter

    [Header("HYPE Meter - Right")]
    [Min(1f)]
    [SerializeField] private float hypeRadiusPixels = 82f;

    [Min(1f)]
    [SerializeField] private float hypeTrackThicknessPixels = 24f;

    [Min(1f)]
    [SerializeField] private float hypeFillThicknessPixels = 14f;

    [Range(10f, 300f)]
    [SerializeField] private float hypeSpanDegrees = 140f;

    [SerializeField] private float hypeCenterAngleDegrees = 0f;

    [Header("Hype Colors")]
    [SerializeField] private Color hypeTrackColor = new Color(0.20f, 0.24f, 0.30f, 0.60f);
    [SerializeField] private Color hypeFillColor = new Color(0.20f, 1f, 0.60f, 1f);

    [Header("Hype Speed Up Flash")]
    [Tooltip(
        "Pulses the current Hype fill between its normal color and the flash " +
        "color while the semantic Is Speeding Up state is true.")]
    [SerializeField] private bool flashHypeFillWhileSpeedingUp = true;

    [SerializeField]
    private Color hypeSpeedupFlashColor =
        new Color(1f, 0.92f, 0.25f, 1f);

    [Tooltip(
        "Complete normal -> flash -> normal color cycles per second while " +
        "Speed Up is active.")]
    [Min(0.01f)]
    [SerializeField] private float hypeSpeedupFlashFrequencyHz = 3f;

    [Header("Hype Fill Animation")]
    [Tooltip(
        "Animate positive Hype changes so the active fill travels toward the new value instead of teleporting. " +
        "Hype decreases remain immediate so Speedup spending and losses stay responsive."
    )]
    [SerializeField] private bool animateHypeGainFill = true;

    [Tooltip(
        "How quickly the displayed Hype fill moves upward, in normalized bar units per second. " +
        "Example: 0.75 means a +25% Hype gain takes about 0.33 seconds."
    )]
    [Min(0.01f)]
    [SerializeField] private float hypeGainFillSpeedNormalizedPerSecond = 0.75f;

    [Header("Hype Drift SUCCESS Preview")]
    [Tooltip("Thickness of the potential success reward segment.")]
    [Min(1f)]
    [SerializeField] private float hypeRewardPreviewThicknessPixels = 10f;

    [Tooltip(
        "Optional radius offset relative to the main Hype centerline. " +
        "0 keeps it directly on the same bar."
    )]
    [SerializeField] private float hypeRewardPreviewRadiusOffsetPixels = 0f;

    [SerializeField] private Color hypeRewardPreviewColor = new Color(0.42f, 1f, 0.78f, 1f);

    [Header("Hype Drift FAILURE Preview")]
    [Tooltip("Thickness of the potential failure penalty segment.")]
    [Min(1f)]
    [SerializeField] private float hypePenaltyPreviewThicknessPixels = 10f;

    [Tooltip(
        "Optional radius offset relative to the main Hype centerline. " +
        "0 keeps it directly on the same bar."
    )]
    [SerializeField] private float hypePenaltyPreviewRadiusOffsetPixels = 0f;

    [SerializeField] private Color hypePenaltyPreviewColor = new Color(1f, 0.34f, 0.08f, 1f);
    [Header("Hype Burn Warning - Icon")]
    [Tooltip(
        "Group position relative to the central HUDWorldAnchor screen position. " +
        "Positive X = right, positive Y = up."
    )]
    [SerializeField] private Vector2 hypeBurnWarningOffsetPixels = new Vector2(18f, -60f);

    [Tooltip("Uniform scale for the COMPLETE Hype burn warning icon combination. Does not move its group position.")]
    [Min(0.05f)]
    [SerializeField] private float hypeBurnWarningMasterScale = 1f;

    [Header("Hype Burn Warning - Background Circle")]
    [SerializeField] private Vector2 hypeBurnWarningBackgroundOffsetPixels = Vector2.zero;

    [Min(1f)]
    [SerializeField] private float hypeBurnWarningBackgroundRadiusPixels = 22f;

    [SerializeField] private Color hypeBurnWarningBackgroundColor = new Color(0.05f, 0.06f, 0.08f, 0.72f);

    [Tooltip(
        "Fallback unified icon color used only if the burn warning Gradient is unavailable. " +
        "Kept so existing serialized color tuning is preserved."
    )]
    [SerializeField] private Color hypeBurnWarningColor = new Color(1f, 0.55f, 0.12f, 0.95f);

    [Header("Hype Burn Warning - Severity Color")]
    [Tooltip(
        "Maps Current Hype Burn Multiplier to ONE unified color for ring, arrow, and bolt. " +
        "Suggested progression: yellow/orange -> orange -> red -> deep red."
    )]
    [SerializeField] private Gradient hypeBurnWarningColorGradient = CreateDefaultHypeBurnWarningGradient();

    [Tooltip("Multiplier mapped to the LEFT/start of the warning Gradient.")]
    [Min(1f)]
    [SerializeField] private float hypeBurnWarningColorMinMultiplier = 1.5f;

    [Tooltip("Multiplier mapped to the RIGHT/end of the warning Gradient.")]
    [Min(1f)]
    [SerializeField] private float hypeBurnWarningColorMaxMultiplier = 2.5f;

    [Header("Hype Burn Warning - Ring")]
    [Min(1f)]
    [SerializeField] private float hypeBurnWarningRingRadiusPixels = 14f;

    [Min(0.5f)]
    [SerializeField] private float hypeBurnWarningRingThicknessPixels = 3f;

    [Range(1f, 180f)]
    [SerializeField] private float hypeBurnWarningGapDegrees = 90f;

    [Tooltip(
        "Center angle of the missing ring gap in screen-space degrees. " +
        "0 = right, 90 = up, 180 = left, 270 = down."
    )]
    [SerializeField] private float hypeBurnWarningGapCenterDegrees = -45f;

    [Header("Hype Burn Warning - Arrow")]
    [SerializeField] private Vector2 hypeBurnWarningArrowOffsetPixels = new Vector2(12f, -12f);

    [Min(1f)]
    [SerializeField] private float hypeBurnWarningArrowShaftLengthPixels = 11f;

    [Min(0.5f)]
    [SerializeField] private float hypeBurnWarningArrowShaftThicknessPixels = 3f;

    [Min(1f)]
    [SerializeField] private float hypeBurnWarningArrowHeadWidthPixels = 10f;

    [Min(1f)]
    [SerializeField] private float hypeBurnWarningArrowHeadHeightPixels = 8f;

    [Header("Hype Burn Warning - Bolt (Two Triangles)")]
    [SerializeField] private Vector2 hypeBurnWarningBoltOffsetPixels = new Vector2(0f, 0f);

    [Header("Hype Burn Warning - Bolt Triangle A")]
    [SerializeField] private Vector2 hypeBurnWarningBoltTriangleAOffsetPixels = new Vector2(-1f, 5f);

    [Min(1f)]
    [SerializeField] private float hypeBurnWarningBoltTriangleAWidthPixels = 11f;

    [Min(1f)]
    [SerializeField] private float hypeBurnWarningBoltTriangleAHeightPixels = 16f;

    [Tooltip("Horizontal pull on the third point. Larger values make the triangle more obtuse / slanted.")]
    [SerializeField] private float hypeBurnWarningBoltTriangleASkewPixels = 6f;

    [SerializeField] private float hypeBurnWarningBoltTriangleARotationDegrees = -22f;

    [Header("Hype Burn Warning - Bolt Triangle B")]
    [SerializeField] private Vector2 hypeBurnWarningBoltTriangleBOffsetPixels = new Vector2(1f, -6f);

    [Min(1f)]
    [SerializeField] private float hypeBurnWarningBoltTriangleBWidthPixels = 11f;

    [Min(1f)]
    [SerializeField] private float hypeBurnWarningBoltTriangleBHeightPixels = 16f;

    [Tooltip("Horizontal pull on the third point. Larger values make the triangle more obtuse / slanted.")]
    [SerializeField] private float hypeBurnWarningBoltTriangleBSkewPixels = 6f;

    [SerializeField] private float hypeBurnWarningBoltTriangleBRotationDegrees = 158f;

    #endregion

    #region Low Speed Warning - Icon

    [Header("Low Speed Warning - Icon")]
    [Tooltip(
        "Group position relative to the central HUDWorldAnchor screen position. " +
        "Positive X = right, positive Y = up."
    )]
    [SerializeField] private Vector2 lowSpeedWarningOffsetPixels = new Vector2(-18f, -60f);

    [Tooltip("Uniform scale for the COMPLETE low-speed warning icon combination. Does not move its group position.")]
    [Min(0.05f)]
    [SerializeField] private float lowSpeedWarningMasterScale = 1f;

    [Header("Low Speed Warning - Background Circle")]
    [SerializeField] private Vector2 lowSpeedWarningBackgroundOffsetPixels = Vector2.zero;

    [Min(1f)]
    [SerializeField] private float lowSpeedWarningBackgroundRadiusPixels = 24f;

    [SerializeField] private Color lowSpeedWarningBackgroundColor = new Color(0.05f, 0.06f, 0.08f, 0.72f);

    [Tooltip(
        "Fallback unified icon color used only if the low-speed warning Gradient is unavailable."
    )]
    [SerializeField] private Color lowSpeedWarningColor = new Color(1f, 0.55f, 0.12f, 0.95f);

    [Header("Low Speed Warning - Severity Color")]
    [Tooltip(
        "Maps normalized speed penalty to one unified icon color. " +
        "Suggested progression: yellow/orange -> orange -> red -> deep red."
    )]
    [SerializeField] private Gradient lowSpeedWarningColorGradient = CreateDefaultLowSpeedWarningGradient();

    [Tooltip("Normalized penalty mapped to the LEFT/start of the warning Gradient.")]
    [Range(0f, 1f)]
    [SerializeField] private float lowSpeedWarningColorMinPenalty = 0.05f;

    [Tooltip("Normalized penalty mapped to the RIGHT/end of the warning Gradient.")]
    [Range(0f, 1f)]
    [SerializeField] private float lowSpeedWarningColorMaxPenalty = 0.50f;

    [Header("Low Speed Warning - Group Offsets")]
    [SerializeField] private Vector2 lowSpeedWarningCartOffsetPixels = new Vector2(0f, 0f);
    [SerializeField] private Vector2 lowSpeedWarningWeightOffsetPixels = new Vector2(0f, 10f);
    [SerializeField] private Vector2 lowSpeedWarningArrowOffsetPixels = new Vector2(16f, -4f);

    [Header("Low Speed Warning - Cart")]
    [Min(0.5f)]
    [SerializeField] private float lowSpeedWarningCartStrokeThicknessPixels = 3f;

    [Header("Low Speed Warning - Cart Basket Corners")]
    [SerializeField] private Vector2 lowSpeedWarningCartBasketTopLeftOffsetPixels = new Vector2(-12f, 11f);
    [SerializeField] private Vector2 lowSpeedWarningCartBasketTopRightOffsetPixels = new Vector2(12f, 11f);
    [SerializeField] private Vector2 lowSpeedWarningCartBasketBottomRightOffsetPixels = new Vector2(9f, -1f);
    [SerializeField] private Vector2 lowSpeedWarningCartBasketBottomLeftOffsetPixels = new Vector2(-9f, -1f);

    [Header("Low Speed Warning - Cart Handle Segments")]
    [SerializeField] private Vector2 lowSpeedWarningCartHandleJointOffsetPixels = new Vector2(-15f, 11f);
    [SerializeField] private Vector2 lowSpeedWarningCartHandleEndOffsetPixels = new Vector2(-18f, 15f);

    [Header("Low Speed Warning - Cart Wheels")]
    [SerializeField] private Vector2 lowSpeedWarningCartLeftWheelOffsetPixels = new Vector2(-7f, -12f);
    [SerializeField] private Vector2 lowSpeedWarningCartRightWheelOffsetPixels = new Vector2(11f, -12f);
    [SerializeField] private float lowSpeedWarningCartWheelRadiusPixels = 4f;

    [Header("Low Speed Warning - Cart Base Two Segments")]
    [SerializeField] private Vector2 lowSpeedWarningCartBaseLeftEndOffsetPixels = new Vector2(-9f, -1f);
    [SerializeField] private Vector2 lowSpeedWarningCartBaseMidOffsetPixels = new Vector2(-13f, -7f);
    [SerializeField] private Vector2 lowSpeedWarningCartBaseRightEndOffsetPixels = new Vector2(16f, -7f);

    [Header("Low Speed Warning - Weight")]
    [Min(1f)]
    [SerializeField] private float lowSpeedWarningWeightTopWidthPixels = 10f;

    [Min(1f)]
    [SerializeField] private float lowSpeedWarningWeightBottomWidthPixels = 14f;

    [Min(1f)]
    [SerializeField] private float lowSpeedWarningWeightBodyHeightPixels = 10f;

    [SerializeField] private Vector2 lowSpeedWarningWeightRingOffsetPixels = new Vector2(0f, 8f);
    [Min(1f)]
    [SerializeField] private float lowSpeedWarningWeightRingRadiusPixels = 3f;

    [Min(0.5f)]
    [SerializeField] private float lowSpeedWarningWeightRingThicknessPixels = 2.5f;

    [Header("Low Speed Warning - Arrow")]
    [Min(1f)]
    [SerializeField] private float lowSpeedWarningArrowShaftLengthPixels = 10f;

    [Min(0.5f)]
    [SerializeField] private float lowSpeedWarningArrowShaftThicknessPixels = 3f;

    [Min(1f)]
    [SerializeField] private float lowSpeedWarningArrowHeadWidthPixels = 9f;

    [Min(1f)]
    [SerializeField] private float lowSpeedWarningArrowHeadHeightPixels = 7f;

    #endregion


    #region Runtime Mode Adaptation

    // Runtime-only multiplier supplied by MatchSceneModeController.
    // It never overwrites the tuned serialized masterScale value in this asset.
    [System.NonSerialized] private float runtimeModeScaleMultiplier = 1f;

    private float EffectiveMasterScale => masterScale * runtimeModeScaleMultiplier;

    public float RuntimeModeScaleMultiplier => runtimeModeScaleMultiplier;
    public float EffectiveHUDMasterScale => EffectiveMasterScale;

    public void SetRuntimeModeScaleMultiplier(float multiplier)
    {
        runtimeModeScaleMultiplier = Mathf.Max(0.05f, multiplier);
    }

    #endregion

    #region Public API

    public float MasterScale => masterScale;
    public float HorizontalSeparationPixels => horizontalSeparationPixels * EffectiveMasterScale;
    public float VerticalOffsetPixels => verticalOffsetPixels * EffectiveMasterScale;

    private float EffectiveMoveBackwardPromptScale =>
        EffectiveMasterScale *
        Mathf.Max(0.05f, moveBackwardPromptMasterScale);

    public bool ShowMoveBackwardPrompt => showMoveBackwardPrompt;
    public Vector2 MoveBackwardPromptOffsetPixels =>
        moveBackwardPromptOffsetPixels * EffectiveMasterScale;
    public float MoveBackwardPromptMasterScale => moveBackwardPromptMasterScale;
    public Vector2 MoveBackwardPromptBackgroundOffsetPixels =>
        moveBackwardPromptBackgroundOffsetPixels * EffectiveMoveBackwardPromptScale;
    public Vector2 MoveBackwardPromptBackgroundSizePixels =>
        moveBackwardPromptBackgroundSizePixels * EffectiveMoveBackwardPromptScale;
    public float MoveBackwardPromptCornerRadiusPixels =>
        moveBackwardPromptCornerRadiusPixels * EffectiveMoveBackwardPromptScale;
    public Color MoveBackwardPromptBackgroundColor => moveBackwardPromptBackgroundColor;
    public string MoveBackwardPromptText => moveBackwardPromptText;
    public Vector2 MoveBackwardPromptTextOffsetPixels =>
        moveBackwardPromptTextOffsetPixels * EffectiveMoveBackwardPromptScale;
    public float MoveBackwardPromptFontSizePixels =>
        moveBackwardPromptFontSizePixels * EffectiveMoveBackwardPromptScale;
    public Color MoveBackwardPromptTextColor => moveBackwardPromptTextColor;

    public bool UseNearCameraRenderPlane => useNearCameraRenderPlane;
    public float NearCameraRenderDistance => nearCameraRenderDistance;
    public float NearClipSafetyPadding => nearClipSafetyPadding;

    public float LoadRadiusPixels => loadRadiusPixels * EffectiveMasterScale;
    public float LoadTrackThicknessPixels => loadTrackThicknessPixels * EffectiveMasterScale;
    public float LoadSpanDegrees => loadSpanDegrees;
    public float LoadCenterAngleDegrees => loadCenterAngleDegrees;
    public float LoadCapsuleLengthPixels => loadCapsuleLengthPixels * EffectiveMasterScale;

    public int MinimumVisualSlotCount => minimumVisualSlotCount;
    public int LoadWindowSize => loadWindowSize;

    public float CapsuleThicknessAt10Slots => capsuleThicknessAt10Slots * EffectiveMasterScale;
    public float CapsuleThicknessAt15Slots => capsuleThicknessAt15Slots * EffectiveMasterScale;
    public float CapsuleThicknessAt20Slots => capsuleThicknessAt20Slots * EffectiveMasterScale;

    public Color LoadTrackColor => loadTrackColor;
    public Color LoadPreSlotColor => loadPreSlotColor;
    public Color LoadEmptyColor => loadEmptyColor;
    public Color LoadFilledColor => loadFilledColor;

    public Gradient LoadOverloadSeverityGradient => loadOverloadSeverityGradient;
    public float OverloadAmountForMaximumSeverity => overloadAmountForMaximumSeverity;

    public bool ShowLoadMilestoneTicks => showLoadMilestoneTicks;
    public int LoadMilestoneInterval => loadMilestoneInterval;
    public float LoadMilestoneTickLengthPixels => loadMilestoneTickLengthPixels * EffectiveMasterScale;
    public float LoadMilestoneTickThicknessPixels => loadMilestoneTickThicknessPixels * EffectiveMasterScale;
    public Color LoadMilestoneTickColor => loadMilestoneTickColor;
    public Color LoadMilestoneTextColor => loadMilestoneTextColor;
    public float LoadMilestoneFontSizePixels => loadMilestoneFontSizePixels * EffectiveMasterScale;
    public float LoadMilestoneLabelOffsetPixels => loadMilestoneLabelOffsetPixels * EffectiveMasterScale;

    public bool ShowCurrentLoadTick => showCurrentLoadTick;
    public float CurrentLoadTickLengthPixels => currentLoadTickLengthPixels * EffectiveMasterScale;
    public float CurrentLoadTickThicknessPixels => currentLoadTickThicknessPixels * EffectiveMasterScale;
    public Color CurrentLoadTickColor => currentLoadTickColor;
    public Color CurrentLoadTextColor => currentLoadTextColor;
    public float CurrentLoadFontSizePixels => currentLoadFontSizePixels * EffectiveMasterScale;
    public float CurrentLoadLabelOffsetPixels => currentLoadLabelOffsetPixels * EffectiveMasterScale;

    public float HypeRadiusPixels => hypeRadiusPixels * EffectiveMasterScale;
    public float HypeTrackThicknessPixels => hypeTrackThicknessPixels * EffectiveMasterScale;
    public float HypeFillThicknessPixels => hypeFillThicknessPixels * EffectiveMasterScale;
    public float HypeSpanDegrees => hypeSpanDegrees;
    public float HypeCenterAngleDegrees => hypeCenterAngleDegrees;
    public Color HypeTrackColor => hypeTrackColor;
    public Color HypeFillColor => hypeFillColor;
    public bool FlashHypeFillWhileSpeedingUp => flashHypeFillWhileSpeedingUp;
    public Color HypeSpeedupFlashColor => hypeSpeedupFlashColor;
    public float HypeSpeedupFlashFrequencyHz =>
        Mathf.Max(0.01f, hypeSpeedupFlashFrequencyHz);
    public bool AnimateHypeGainFill => animateHypeGainFill;
    public float HypeGainFillSpeedNormalizedPerSecond => hypeGainFillSpeedNormalizedPerSecond;

    public float HypeRewardPreviewThicknessPixels => hypeRewardPreviewThicknessPixels * EffectiveMasterScale;
    public float HypeRewardPreviewRadiusOffsetPixels => hypeRewardPreviewRadiusOffsetPixels * EffectiveMasterScale;
    public Color HypeRewardPreviewColor => hypeRewardPreviewColor;

    public float HypePenaltyPreviewThicknessPixels => hypePenaltyPreviewThicknessPixels * EffectiveMasterScale;
    public float HypePenaltyPreviewRadiusOffsetPixels => hypePenaltyPreviewRadiusOffsetPixels * EffectiveMasterScale;
    public Color HypePenaltyPreviewColor => hypePenaltyPreviewColor;

    public Vector2 HypeBurnWarningOffsetPixels => hypeBurnWarningOffsetPixels * EffectiveMasterScale;
    public float HypeBurnWarningMasterScale => hypeBurnWarningMasterScale;
    public Vector2 HypeBurnWarningBackgroundOffsetPixels => hypeBurnWarningBackgroundOffsetPixels * EffectiveMasterScale * hypeBurnWarningMasterScale;
    public float HypeBurnWarningBackgroundRadiusPixels => hypeBurnWarningBackgroundRadiusPixels * EffectiveMasterScale * hypeBurnWarningMasterScale;
    public Color HypeBurnWarningBackgroundColor => hypeBurnWarningBackgroundColor;
    public Color HypeBurnWarningColor => hypeBurnWarningColor;
    public float HypeBurnWarningColorMinMultiplier => hypeBurnWarningColorMinMultiplier;
    public float HypeBurnWarningColorMaxMultiplier => hypeBurnWarningColorMaxMultiplier;
    public float HypeBurnWarningRingRadiusPixels => hypeBurnWarningRingRadiusPixels * EffectiveMasterScale * hypeBurnWarningMasterScale;
    public float HypeBurnWarningRingThicknessPixels => hypeBurnWarningRingThicknessPixels * EffectiveMasterScale * hypeBurnWarningMasterScale;
    public float HypeBurnWarningGapDegrees => hypeBurnWarningGapDegrees;
    public float HypeBurnWarningGapCenterDegrees => hypeBurnWarningGapCenterDegrees;
    public Vector2 HypeBurnWarningArrowOffsetPixels => hypeBurnWarningArrowOffsetPixels * EffectiveMasterScale * hypeBurnWarningMasterScale;
    public float HypeBurnWarningArrowShaftLengthPixels => hypeBurnWarningArrowShaftLengthPixels * EffectiveMasterScale * hypeBurnWarningMasterScale;
    public float HypeBurnWarningArrowShaftThicknessPixels => hypeBurnWarningArrowShaftThicknessPixels * EffectiveMasterScale * hypeBurnWarningMasterScale;
    public float HypeBurnWarningArrowHeadWidthPixels => hypeBurnWarningArrowHeadWidthPixels * EffectiveMasterScale * hypeBurnWarningMasterScale;
    public float HypeBurnWarningArrowHeadHeightPixels => hypeBurnWarningArrowHeadHeightPixels * EffectiveMasterScale * hypeBurnWarningMasterScale;
    public Vector2 HypeBurnWarningBoltOffsetPixels => hypeBurnWarningBoltOffsetPixels * EffectiveMasterScale * hypeBurnWarningMasterScale;
    public Vector2 HypeBurnWarningBoltTriangleAOffsetPixels => hypeBurnWarningBoltTriangleAOffsetPixels * EffectiveMasterScale * hypeBurnWarningMasterScale;
    public float HypeBurnWarningBoltTriangleAWidthPixels => hypeBurnWarningBoltTriangleAWidthPixels * EffectiveMasterScale * hypeBurnWarningMasterScale;
    public float HypeBurnWarningBoltTriangleAHeightPixels => hypeBurnWarningBoltTriangleAHeightPixels * EffectiveMasterScale * hypeBurnWarningMasterScale;
    public float HypeBurnWarningBoltTriangleASkewPixels => hypeBurnWarningBoltTriangleASkewPixels * EffectiveMasterScale * hypeBurnWarningMasterScale;
    public float HypeBurnWarningBoltTriangleARotationDegrees => hypeBurnWarningBoltTriangleARotationDegrees;
    public Vector2 HypeBurnWarningBoltTriangleBOffsetPixels => hypeBurnWarningBoltTriangleBOffsetPixels * EffectiveMasterScale * hypeBurnWarningMasterScale;
    public float HypeBurnWarningBoltTriangleBWidthPixels => hypeBurnWarningBoltTriangleBWidthPixels * EffectiveMasterScale * hypeBurnWarningMasterScale;
    public float HypeBurnWarningBoltTriangleBHeightPixels => hypeBurnWarningBoltTriangleBHeightPixels * EffectiveMasterScale * hypeBurnWarningMasterScale;
    public float HypeBurnWarningBoltTriangleBSkewPixels => hypeBurnWarningBoltTriangleBSkewPixels * EffectiveMasterScale * hypeBurnWarningMasterScale;
    public float HypeBurnWarningBoltTriangleBRotationDegrees => hypeBurnWarningBoltTriangleBRotationDegrees;

    public Vector2 LowSpeedWarningOffsetPixels => lowSpeedWarningOffsetPixels * EffectiveMasterScale;
    public float LowSpeedWarningMasterScale => lowSpeedWarningMasterScale;
    public Vector2 LowSpeedWarningBackgroundOffsetPixels => lowSpeedWarningBackgroundOffsetPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public float LowSpeedWarningBackgroundRadiusPixels => lowSpeedWarningBackgroundRadiusPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public Color LowSpeedWarningBackgroundColor => lowSpeedWarningBackgroundColor;
    public Color LowSpeedWarningColor => lowSpeedWarningColor;
    public float LowSpeedWarningColorMinPenalty => lowSpeedWarningColorMinPenalty;
    public float LowSpeedWarningColorMaxPenalty => lowSpeedWarningColorMaxPenalty;
    public Vector2 LowSpeedWarningCartOffsetPixels => lowSpeedWarningCartOffsetPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public Vector2 LowSpeedWarningWeightOffsetPixels => lowSpeedWarningWeightOffsetPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public Vector2 LowSpeedWarningArrowOffsetPixels => lowSpeedWarningArrowOffsetPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public float LowSpeedWarningCartStrokeThicknessPixels => lowSpeedWarningCartStrokeThicknessPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public Vector2 LowSpeedWarningCartBasketTopLeftOffsetPixels => lowSpeedWarningCartBasketTopLeftOffsetPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public Vector2 LowSpeedWarningCartBasketTopRightOffsetPixels => lowSpeedWarningCartBasketTopRightOffsetPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public Vector2 LowSpeedWarningCartBasketBottomRightOffsetPixels => lowSpeedWarningCartBasketBottomRightOffsetPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public Vector2 LowSpeedWarningCartBasketBottomLeftOffsetPixels => lowSpeedWarningCartBasketBottomLeftOffsetPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public Vector2 LowSpeedWarningCartHandleJointOffsetPixels => lowSpeedWarningCartHandleJointOffsetPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public Vector2 LowSpeedWarningCartHandleEndOffsetPixels => lowSpeedWarningCartHandleEndOffsetPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public Vector2 LowSpeedWarningCartLeftWheelOffsetPixels => lowSpeedWarningCartLeftWheelOffsetPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public Vector2 LowSpeedWarningCartRightWheelOffsetPixels => lowSpeedWarningCartRightWheelOffsetPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public float LowSpeedWarningCartWheelRadiusPixels => lowSpeedWarningCartWheelRadiusPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public Vector2 LowSpeedWarningCartBaseLeftEndOffsetPixels => lowSpeedWarningCartBaseLeftEndOffsetPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public Vector2 LowSpeedWarningCartBaseMidOffsetPixels => lowSpeedWarningCartBaseMidOffsetPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public Vector2 LowSpeedWarningCartBaseRightEndOffsetPixels => lowSpeedWarningCartBaseRightEndOffsetPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public float LowSpeedWarningWeightTopWidthPixels => lowSpeedWarningWeightTopWidthPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public float LowSpeedWarningWeightBottomWidthPixels => lowSpeedWarningWeightBottomWidthPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public float LowSpeedWarningWeightBodyHeightPixels => lowSpeedWarningWeightBodyHeightPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public Vector2 LowSpeedWarningWeightRingOffsetPixels => lowSpeedWarningWeightRingOffsetPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public float LowSpeedWarningWeightRingRadiusPixels => lowSpeedWarningWeightRingRadiusPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public float LowSpeedWarningWeightRingThicknessPixels => lowSpeedWarningWeightRingThicknessPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public float LowSpeedWarningArrowShaftLengthPixels => lowSpeedWarningArrowShaftLengthPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public float LowSpeedWarningArrowShaftThicknessPixels => lowSpeedWarningArrowShaftThicknessPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public float LowSpeedWarningArrowHeadWidthPixels => lowSpeedWarningArrowHeadWidthPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;
    public float LowSpeedWarningArrowHeadHeightPixels => lowSpeedWarningArrowHeadHeightPixels * EffectiveMasterScale * lowSpeedWarningMasterScale;

    #endregion

    #region Load Helpers

    public float GetLoadCapsuleThicknessPixels(int visibleSlotCount)
    {
        int count = Mathf.Clamp(
            visibleSlotCount,
            minimumVisualSlotCount,
            loadWindowSize
        );

        if (count <= 10)
        {
            return CapsuleThicknessAt10Slots;
        }

        if (count <= 15)
        {
            float t = Mathf.InverseLerp(10f, 15f, count);

            return Mathf.Lerp(
                CapsuleThicknessAt10Slots,
                CapsuleThicknessAt15Slots,
                t
            );
        }

        float upperT = Mathf.InverseLerp(15f, 20f, count);

        return Mathf.Lerp(
            CapsuleThicknessAt15Slots,
            CapsuleThicknessAt20Slots,
            upperT
        );
    }

    public Color GetLoadOverloadColor(float overloadAmount)
    {
        if (loadOverloadSeverityGradient == null)
        {
            return new Color(1f, 0.18f, 0.04f, 1f);
        }

        float t = overloadAmountForMaximumSeverity > 0f
            ? Mathf.Clamp01(overloadAmount / overloadAmountForMaximumSeverity)
            : 1f;

        return loadOverloadSeverityGradient.Evaluate(t);
    }


    public Color GetHypeBurnWarningColor(float burnMultiplier)
    {
        if (hypeBurnWarningColorGradient == null)
        {
            return hypeBurnWarningColor;
        }

        float minMultiplier = hypeBurnWarningColorMinMultiplier;
        float maxMultiplier = Mathf.Max(minMultiplier + 0.0001f, hypeBurnWarningColorMaxMultiplier);
        float t = Mathf.InverseLerp(minMultiplier, maxMultiplier, burnMultiplier);

        return hypeBurnWarningColorGradient.Evaluate(t);
    }

    public Color GetLowSpeedWarningColor(float penaltyNormalized)
    {
        if (lowSpeedWarningColorGradient == null)
        {
            return lowSpeedWarningColor;
        }

        float minPenalty = lowSpeedWarningColorMinPenalty;
        float maxPenalty = Mathf.Max(minPenalty + 0.0001f, lowSpeedWarningColorMaxPenalty);
        float t = Mathf.InverseLerp(minPenalty, maxPenalty, Mathf.Clamp01(penaltyNormalized));

        return lowSpeedWarningColorGradient.Evaluate(t);
    }

    #endregion

    #region Validation

    private void OnValidate()
    {
        masterScale = Mathf.Max(0.01f, masterScale);
        horizontalSeparationPixels = Mathf.Max(0f, horizontalSeparationPixels);

        moveBackwardPromptMasterScale =
            Mathf.Max(0.05f, moveBackwardPromptMasterScale);

        moveBackwardPromptBackgroundSizePixels.x =
            Mathf.Max(1f, moveBackwardPromptBackgroundSizePixels.x);

        moveBackwardPromptBackgroundSizePixels.y =
            Mathf.Max(1f, moveBackwardPromptBackgroundSizePixels.y);

        moveBackwardPromptCornerRadiusPixels =
            Mathf.Clamp(
                moveBackwardPromptCornerRadiusPixels,
                0f,
                Mathf.Min(
                    moveBackwardPromptBackgroundSizePixels.x,
                    moveBackwardPromptBackgroundSizePixels.y
                ) * 0.5f
            );

        moveBackwardPromptFontSizePixels =
            Mathf.Max(1f, moveBackwardPromptFontSizePixels);

        nearCameraRenderDistance = Mathf.Max(0.01f, nearCameraRenderDistance);
        nearClipSafetyPadding = Mathf.Max(0.001f, nearClipSafetyPadding);

        loadRadiusPixels = Mathf.Max(1f, loadRadiusPixels);
        loadTrackThicknessPixels = Mathf.Max(1f, loadTrackThicknessPixels);
        loadSpanDegrees = Mathf.Clamp(loadSpanDegrees, 10f, 300f);
        loadCapsuleLengthPixels = Mathf.Max(1f, loadCapsuleLengthPixels);

        minimumVisualSlotCount = Mathf.Max(1, minimumVisualSlotCount);
        loadWindowSize = Mathf.Max(minimumVisualSlotCount, loadWindowSize);

        capsuleThicknessAt10Slots = Mathf.Clamp(capsuleThicknessAt10Slots, 1f, loadTrackThicknessPixels);
        capsuleThicknessAt15Slots = Mathf.Clamp(capsuleThicknessAt15Slots, 1f, loadTrackThicknessPixels);
        capsuleThicknessAt20Slots = Mathf.Clamp(capsuleThicknessAt20Slots, 1f, loadTrackThicknessPixels);

        overloadAmountForMaximumSeverity = Mathf.Max(1f, overloadAmountForMaximumSeverity);

        loadMilestoneInterval = Mathf.Max(1, loadMilestoneInterval);
        loadMilestoneTickLengthPixels = Mathf.Max(1f, loadMilestoneTickLengthPixels);
        loadMilestoneTickThicknessPixels = Mathf.Max(0.5f, loadMilestoneTickThicknessPixels);
        loadMilestoneFontSizePixels = Mathf.Max(1f, loadMilestoneFontSizePixels);
        loadMilestoneLabelOffsetPixels = Mathf.Max(0f, loadMilestoneLabelOffsetPixels);

        currentLoadTickLengthPixels = Mathf.Max(1f, currentLoadTickLengthPixels);
        currentLoadTickThicknessPixels = Mathf.Max(0.5f, currentLoadTickThicknessPixels);
        currentLoadFontSizePixels = Mathf.Max(1f, currentLoadFontSizePixels);
        currentLoadLabelOffsetPixels = Mathf.Max(0f, currentLoadLabelOffsetPixels);

        hypeRadiusPixels = Mathf.Max(1f, hypeRadiusPixels);
        hypeTrackThicknessPixels = Mathf.Max(1f, hypeTrackThicknessPixels);
        hypeFillThicknessPixels = Mathf.Clamp(
            hypeFillThicknessPixels,
            1f,
            hypeTrackThicknessPixels
        );
        hypeSpeedupFlashFrequencyHz =
            Mathf.Max(0.01f, hypeSpeedupFlashFrequencyHz);
        hypeGainFillSpeedNormalizedPerSecond = Mathf.Max(0.01f, hypeGainFillSpeedNormalizedPerSecond);

        hypeRewardPreviewThicknessPixels = Mathf.Max(1f, hypeRewardPreviewThicknessPixels);
        hypePenaltyPreviewThicknessPixels = Mathf.Max(1f, hypePenaltyPreviewThicknessPixels);
        hypeBurnWarningMasterScale = Mathf.Max(0.05f, hypeBurnWarningMasterScale);
        hypeBurnWarningBackgroundRadiusPixels = Mathf.Max(1f, hypeBurnWarningBackgroundRadiusPixels);
        hypeBurnWarningRingRadiusPixels = Mathf.Max(1f, hypeBurnWarningRingRadiusPixels);
        hypeBurnWarningRingThicknessPixels = Mathf.Max(0.5f, hypeBurnWarningRingThicknessPixels);
        hypeBurnWarningArrowShaftLengthPixels = Mathf.Max(1f, hypeBurnWarningArrowShaftLengthPixels);
        hypeBurnWarningArrowShaftThicknessPixels = Mathf.Max(0.5f, hypeBurnWarningArrowShaftThicknessPixels);
        hypeBurnWarningArrowHeadWidthPixels = Mathf.Max(1f, hypeBurnWarningArrowHeadWidthPixels);
        hypeBurnWarningArrowHeadHeightPixels = Mathf.Max(1f, hypeBurnWarningArrowHeadHeightPixels);
        hypeBurnWarningBoltTriangleAWidthPixels = Mathf.Max(1f, hypeBurnWarningBoltTriangleAWidthPixels);
        hypeBurnWarningBoltTriangleAHeightPixels = Mathf.Max(1f, hypeBurnWarningBoltTriangleAHeightPixels);
        hypeBurnWarningBoltTriangleBWidthPixels = Mathf.Max(1f, hypeBurnWarningBoltTriangleBWidthPixels);
        hypeBurnWarningBoltTriangleBHeightPixels = Mathf.Max(1f, hypeBurnWarningBoltTriangleBHeightPixels);
        hypeBurnWarningGapDegrees = Mathf.Clamp(hypeBurnWarningGapDegrees, 1f, 180f);
        hypeBurnWarningColorMinMultiplier = Mathf.Max(1f, hypeBurnWarningColorMinMultiplier);
        hypeBurnWarningColorMaxMultiplier = Mathf.Max(hypeBurnWarningColorMinMultiplier + 0.01f, hypeBurnWarningColorMaxMultiplier);

        lowSpeedWarningMasterScale = Mathf.Max(0.05f, lowSpeedWarningMasterScale);
        lowSpeedWarningBackgroundRadiusPixels = Mathf.Max(1f, lowSpeedWarningBackgroundRadiusPixels);
        lowSpeedWarningColorMinPenalty = Mathf.Clamp(lowSpeedWarningColorMinPenalty, 0f, 0.99f);
        lowSpeedWarningColorMaxPenalty = Mathf.Clamp(lowSpeedWarningColorMaxPenalty, lowSpeedWarningColorMinPenalty + 0.01f, 1f);
        lowSpeedWarningCartStrokeThicknessPixels = Mathf.Max(0.5f, lowSpeedWarningCartStrokeThicknessPixels);
        lowSpeedWarningCartWheelRadiusPixels = Mathf.Max(1f, lowSpeedWarningCartWheelRadiusPixels);
        lowSpeedWarningWeightTopWidthPixels = Mathf.Max(1f, lowSpeedWarningWeightTopWidthPixels);
        lowSpeedWarningWeightBottomWidthPixels = Mathf.Max(1f, lowSpeedWarningWeightBottomWidthPixels);
        lowSpeedWarningWeightBodyHeightPixels = Mathf.Max(1f, lowSpeedWarningWeightBodyHeightPixels);
        lowSpeedWarningWeightRingRadiusPixels = Mathf.Max(1f, lowSpeedWarningWeightRingRadiusPixels);
        lowSpeedWarningWeightRingThicknessPixels = Mathf.Max(0.5f, lowSpeedWarningWeightRingThicknessPixels);
        lowSpeedWarningArrowShaftLengthPixels = Mathf.Max(1f, lowSpeedWarningArrowShaftLengthPixels);
        lowSpeedWarningArrowShaftThicknessPixels = Mathf.Max(0.5f, lowSpeedWarningArrowShaftThicknessPixels);
        lowSpeedWarningArrowHeadWidthPixels = Mathf.Max(1f, lowSpeedWarningArrowHeadWidthPixels);
        lowSpeedWarningArrowHeadHeightPixels = Mathf.Max(1f, lowSpeedWarningArrowHeadHeightPixels);

        hypeSpanDegrees = Mathf.Clamp(hypeSpanDegrees, 10f, 300f);
    }

    #endregion

    #region Defaults

    private static Gradient CreateDefaultOverloadGradient()
    {
        Gradient gradient = new Gradient();

        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.68f, 0.05f), 0f),
                new GradientColorKey(new Color(1f, 0.36f, 0.02f), 0.33f),
                new GradientColorKey(new Color(1f, 0.10f, 0.02f), 0.67f),
                new GradientColorKey(new Color(0.55f, 0.01f, 0.01f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        );

        return gradient;
    }

    private static Gradient CreateDefaultHypeBurnWarningGradient()
    {
        Gradient gradient = new Gradient();

        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.72f, 0.08f), 0f),
                new GradientColorKey(new Color(1f, 0.42f, 0.03f), 0.35f),
                new GradientColorKey(new Color(1f, 0.12f, 0.02f), 0.72f),
                new GradientColorKey(new Color(0.58f, 0.015f, 0.015f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        );

        return gradient;
    }

    private static Gradient CreateDefaultLowSpeedWarningGradient()
    {
        Gradient gradient = new Gradient();

        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.72f, 0.08f), 0f),
                new GradientColorKey(new Color(1f, 0.46f, 0.03f), 0.35f),
                new GradientColorKey(new Color(1f, 0.14f, 0.02f), 0.72f),
                new GradientColorKey(new Color(0.58f, 0.015f, 0.015f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        );

        return gradient;
    }

    #endregion
}
