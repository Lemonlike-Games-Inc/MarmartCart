using UnityEngine;

/// <summary>
/// Shared visual/layout authoring for the per-player session guide.
/// Every value is expressed in screen pixels before the selected 2P/4P scale
/// is applied, so the guide keeps the same relationship to HUDWorldAnchor at
/// every resolution.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Player HUD/Session Guide Profile",
    fileName = "PlayerSessionGuideProfile"
)]
public sealed class PlayerSessionGuideProfile : ScriptableObject
{
    #region General Scale / Placement

    [Header("General Scale / Placement")]
    [SerializeField] private bool showSessionGuide = true;

    [Tooltip("Shared scale applied before the active 2P/4P multiplier.")]
    [Min(0.01f)]
    [SerializeField] private float masterScale = 1f;

    [Tooltip("Applied when one or two PlayerWorldHUD slots are bound.")]
    [Min(0.01f)]
    [SerializeField] private float twoPlayerScaleMultiplier = 1f;

    [Tooltip("Applied when three or four PlayerWorldHUD slots are bound.")]
    [Min(0.01f)]
    [SerializeField] private float fourPlayerScaleMultiplier = 0.82f;

    [Tooltip(
        "Position of the complete guide relative to that player's " +
        "HUDWorldAnchor, in screen pixels."
    )]
    [SerializeField] private Vector2 groupOffsetPixels = new Vector2(0f, 135f);

    #endregion

    #region Dialogue

    [Header("Countdown Dialogue Templates")]
    [Tooltip("Tokens: {ZONE} and {COUNTDOWN}.")]
    [TextArea(1, 2)]
    [SerializeField]
    private string zoneLootMessage =
        "FLASH SALE IN {ZONE}! {COUNTDOWN}";

    [Tooltip("Token: {COUNTDOWN}.")]
    [TextArea(1, 2)]
    [SerializeField]
    private string checkoutRestockMessage =
        "CHECKOUTS OPEN IN {COUNTDOWN}!";

    [Header("Active Dialogue Templates")]
    [TextArea(1, 2)]
    [SerializeField]
    private string zoneLootActiveMessage =
        "FLASH SALE THERE";

    [TextArea(1, 2)]
    [SerializeField]
    private string checkoutRestockActiveMessage =
        "CHECKOUTS OPENING";

    [Header("Dialogue Background")]
    [Tooltip("Rounded-rectangle size for a Zone Loot forecast.")]
    [SerializeField]
    private Vector2 zoneLootBackgroundSizePixels =
        new Vector2(310f, 50f);

    [Tooltip("Rounded-rectangle size for a Checkout + Restock forecast.")]
    [SerializeField]
    private Vector2 checkoutRestockBackgroundSizePixels =
        new Vector2(270f, 50f);

    [SerializeField] private Vector2 dialogueBackgroundOffsetPixels = Vector2.zero;

    [Min(0f)]
    [SerializeField] private float dialogueCornerRadiusPixels = 13f;

    [SerializeField]
    private Color dialogueBackgroundColor =
        new Color(0.035f, 0.05f, 0.075f, 0.88f);

    [Header("Dialogue Text")]
    [SerializeField] private Vector2 dialogueTextOffsetPixels = Vector2.zero;

    [Min(1f)]
    [SerializeField] private float dialogueFontSizePixels = 19f;

    [SerializeField] private Color dialogueTextColor = Color.white;

    #endregion

    #region Compass Arrow

    [Header("Compass Background")]
    [Tooltip("Position of the compass circle relative to the dialogue group.")]
    [SerializeField] private Vector2 compassOffsetPixels = new Vector2(0f, 62f);

    [Min(0f)]
    [SerializeField] private float compassCircleRadiusPixels = 25f;

    [SerializeField]
    private Color compassCircleColor =
        new Color(0.035f, 0.05f, 0.075f, 0.88f);

    [Header("Live Pointing Arrow")]
    [Tooltip("Fine positioning of the arrow inside its compass circle.")]
    [SerializeField] private Vector2 arrowOffsetPixels = Vector2.zero;

    [Min(0f)]
    [SerializeField] private float arrowLengthPixels = 31f;

    [Min(0f)]
    [SerializeField] private float arrowShaftThicknessPixels = 4f;

    [Min(0f)]
    [SerializeField] private float arrowHeadLengthPixels = 10f;

    [Min(0f)]
    [SerializeField] private float arrowHeadWidthPixels = 14f;

    [SerializeField] private Color arrowColor = new Color(1f, 0.79f, 0.16f, 1f);

    [Header("You Are Here Raindrop")]
    [Tooltip("Fine positioning of the raindrop inside its compass circle.")]
    [SerializeField] private Vector2 raindropOffsetPixels = Vector2.zero;

    [Min(0f)]
    [SerializeField] private float raindropWidthPixels = 16f;

    [Min(0f)]
    [SerializeField] private float raindropHeightPixels = 24f;

    [SerializeField]
    private Color raindropColor =
        new Color(0.3f, 0.9f, 1f, 1f);

    [Header("Raindrop Center Circle")]
    [Tooltip(
        "Radius of the filled circle centered exactly on the raindrop bulb. " +
        "Set to 0 to hide it."
    )]
    [Min(0f)]
    [SerializeField] private float raindropCenterCircleRadiusPixels = 4.5f;

    [SerializeField]
    private Color raindropCenterCircleColor =
        new Color(0.035f, 0.05f, 0.075f, 1f);

    #endregion

    #region Render Plane

    [Header("Render Plane")]
    [Tooltip(
        "Rebuild the guide on a camera-near plane while preserving the " +
        "HUDWorldAnchor's screen position. This prevents world geometry from " +
        "covering the guide."
    )]
    [SerializeField] private bool useNearCameraRenderPlane = true;

    [Min(0.01f)]
    [SerializeField] private float nearCameraRenderDistance = 0.5f;

    [Min(0f)]
    [SerializeField] private float nearClipSafetyPadding = 0.03f;

    #endregion

    #region Public API

    public bool ShowSessionGuide => showSessionGuide;
    public Vector2 GroupOffsetPixels => groupOffsetPixels;

    public Vector2 ZoneLootBackgroundSizePixels => zoneLootBackgroundSizePixels;
    public Vector2 CheckoutRestockBackgroundSizePixels =>
        checkoutRestockBackgroundSizePixels;
    public Vector2 DialogueBackgroundOffsetPixels => dialogueBackgroundOffsetPixels;
    public float DialogueCornerRadiusPixels => dialogueCornerRadiusPixels;
    public Color DialogueBackgroundColor => dialogueBackgroundColor;

    public Vector2 DialogueTextOffsetPixels => dialogueTextOffsetPixels;
    public float DialogueFontSizePixels => dialogueFontSizePixels;
    public Color DialogueTextColor => dialogueTextColor;

    public Vector2 CompassOffsetPixels => compassOffsetPixels;
    public float CompassCircleRadiusPixels => compassCircleRadiusPixels;
    public Color CompassCircleColor => compassCircleColor;

    public Vector2 ArrowOffsetPixels => arrowOffsetPixels;
    public float ArrowLengthPixels => arrowLengthPixels;
    public float ArrowShaftThicknessPixels => arrowShaftThicknessPixels;
    public float ArrowHeadLengthPixels => arrowHeadLengthPixels;
    public float ArrowHeadWidthPixels => arrowHeadWidthPixels;
    public Color ArrowColor => arrowColor;

    public Vector2 RaindropOffsetPixels => raindropOffsetPixels;
    public float RaindropWidthPixels => raindropWidthPixels;
    public float RaindropHeightPixels => raindropHeightPixels;
    public Color RaindropColor => raindropColor;
    public float RaindropCenterCircleRadiusPixels =>
        raindropCenterCircleRadiusPixels;
    public Color RaindropCenterCircleColor => raindropCenterCircleColor;

    public bool UseNearCameraRenderPlane => useNearCameraRenderPlane;
    public float NearCameraRenderDistance => nearCameraRenderDistance;
    public float NearClipSafetyPadding => nearClipSafetyPadding;

    public string ZoneLootMessage => zoneLootMessage;
    public string CheckoutRestockMessage => checkoutRestockMessage;
    public string ZoneLootActiveMessage => zoneLootActiveMessage;
    public string CheckoutRestockActiveMessage => checkoutRestockActiveMessage;

    public float GetEffectiveScale(int activePlayerCount)
    {
        float modeScale =
            activePlayerCount >= 3
                ? fourPlayerScaleMultiplier
                : twoPlayerScaleMultiplier;

        return Mathf.Max(0.01f, masterScale * modeScale);
    }

    public Vector2 GetDialogueBackgroundSize(
        PlayerSessionGuideTargetKind targetKind)
    {
        return targetKind == PlayerSessionGuideTargetKind.CheckoutRestock
            ? checkoutRestockBackgroundSizePixels
            : zoneLootBackgroundSizePixels;
    }

    public string BuildMessage(
        PlayerSessionGuideTargetKind targetKind,
        string zoneDisplayName,
        int countdownSeconds)
    {
        return BuildMessage(
            PlayerSessionGuideIndicationPhase.Countdown,
            targetKind,
            zoneDisplayName,
            countdownSeconds
        );
    }

    public string GetMessageTemplate(
        PlayerSessionGuideIndicationPhase indicationPhase,
        PlayerSessionGuideTargetKind targetKind)
    {
        if (indicationPhase == PlayerSessionGuideIndicationPhase.Active)
        {
            return targetKind == PlayerSessionGuideTargetKind.CheckoutRestock
                ? checkoutRestockActiveMessage
                : zoneLootActiveMessage;
        }

        return
            targetKind == PlayerSessionGuideTargetKind.CheckoutRestock
                ? checkoutRestockMessage
                : zoneLootMessage;
    }

    public string BuildMessage(
        PlayerSessionGuideIndicationPhase indicationPhase,
        PlayerSessionGuideTargetKind targetKind,
        string zoneDisplayName,
        int countdownSeconds)
    {
        string template = GetMessageTemplate(indicationPhase, targetKind);

        if (string.IsNullOrWhiteSpace(template)) return string.Empty;

        return template
            .Replace("{ZONE}", zoneDisplayName ?? string.Empty)
            .Replace(
                "{COUNTDOWN}",
                Mathf.Max(1, countdownSeconds).ToString()
            );
    }

    #endregion

    #region Validation

    private void OnValidate()
    {
        masterScale = Mathf.Max(0.01f, masterScale);
        twoPlayerScaleMultiplier = Mathf.Max(0.01f, twoPlayerScaleMultiplier);
        fourPlayerScaleMultiplier = Mathf.Max(0.01f, fourPlayerScaleMultiplier);

        zoneLootBackgroundSizePixels = ClampPositiveSize(
            zoneLootBackgroundSizePixels
        );

        checkoutRestockBackgroundSizePixels = ClampPositiveSize(
            checkoutRestockBackgroundSizePixels
        );

        dialogueCornerRadiusPixels = Mathf.Max(0f, dialogueCornerRadiusPixels);
        dialogueFontSizePixels = Mathf.Max(1f, dialogueFontSizePixels);
        compassCircleRadiusPixels = Mathf.Max(0f, compassCircleRadiusPixels);
        arrowLengthPixels = Mathf.Max(0f, arrowLengthPixels);
        arrowShaftThicknessPixels = Mathf.Max(0f, arrowShaftThicknessPixels);
        arrowHeadLengthPixels = Mathf.Clamp(
            arrowHeadLengthPixels,
            0f,
            arrowLengthPixels
        );
        arrowHeadWidthPixels = Mathf.Max(0f, arrowHeadWidthPixels);
        raindropWidthPixels = Mathf.Max(0f, raindropWidthPixels);
        raindropHeightPixels = Mathf.Max(0f, raindropHeightPixels);
        raindropCenterCircleRadiusPixels = Mathf.Max(
            0f,
            raindropCenterCircleRadiusPixels
        );
        nearCameraRenderDistance = Mathf.Max(0.01f, nearCameraRenderDistance);
        nearClipSafetyPadding = Mathf.Max(0f, nearClipSafetyPadding);
    }

    private static Vector2 ClampPositiveSize(Vector2 size)
    {
        return new Vector2(
            Mathf.Max(0.001f, size.x),
            Mathf.Max(0.001f, size.y)
        );
    }

    #endregion
}
