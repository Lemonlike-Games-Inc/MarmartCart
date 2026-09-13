using UnityEngine;

/// <summary>
/// Presentation and visibility settings for the Shapes-based marker that sits
/// above OTHER players while they are actually visible in the current camera.
///
/// Player colors intentionally remain owned by OtherPlayerPointerProfile so
/// the orbit pointer and visible-player marker always share the same P1-P4
/// color language.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Player HUD/Other Player Visible Marker Profile",
    fileName = "OtherPlayerVisibleMarkerProfile"
)]
public class OtherPlayerVisibleMarkerProfile : ScriptableObject
{
    #region Placement

    [Header("Marker Placement")]
    [Tooltip(
        "Screen-space offset from the target player's HUDWorldAnchor to the TIP of the marker. " +
        "Positive X = right, positive Y = up."
    )]
    [SerializeField] private Vector2 markerTipOffsetPixels = new Vector2(0f, 42f);

    [Tooltip("Uniform scale for the complete marker: body, tail, and text.")]
    [Min(0.05f)]
    [SerializeField] private float markerMasterScale = 1f;

    #endregion

    #region Shape

    [Header("Raindrop / Map-Pin Shape")]
    [Tooltip("Radius of the rounded upper body.")]
    [Min(1f)]
    [SerializeField] private float bodyRadiusPixels = 18f;

    [Tooltip("Width of the triangular downward-pointing tail at its base.")]
    [Min(1f)]
    [SerializeField] private float tailWidthPixels = 20f;

    [Tooltip("Distance from the tail's base down to its pointed tip.")]
    [Min(1f)]
    [SerializeField] private float tailHeightPixels = 14f;

    [Tooltip(
        "How far the circular body overlaps downward across the tail base. " +
        "Higher values make the circle + triangle read as one continuous raindrop."
    )]
    [Min(0f)]
    [SerializeField] private float tailBodyOverlapPixels = 7f;

    #endregion

    #region Text

    [Header("Player Label")]
    [Tooltip("Screen-pixel offset of P1/P2/P3/P4 relative to the center of the rounded body.")]
    [SerializeField] private Vector2 textOffsetPixels = Vector2.zero;

    [Min(1f)]
    [SerializeField] private float fontSizePixels = 15f;

    [SerializeField] private Color textColor = Color.white;

    #endregion

    #region Visibility

    [Header("Visibility")]
    [Tooltip(
        "Extra screen-space margin used by the on-screen test. " +
        "Positive values allow the target anchor to remain eligible slightly beyond the viewport edge."
    )]
    [Min(0f)]
    [SerializeField] private float screenVisibilityPaddingPixels = 0f;

    [Tooltip(
        "When enabled, the marker is hidden if world geometry is between this gameplay camera and the target. " +
        "This prevents the near-camera Shapes marker from appearing through walls/shelves."
    )]
    [SerializeField] private bool requireLineOfSight = true;

    [Tooltip("Layers that may block line of sight to another player marker.")]
    [SerializeField] private LayerMask lineOfSightBlockingMask = ~0;

    [Tooltip("Small extra ray distance so a hit on the target's own collider counts as visible reliably.")]
    [Min(0f)]
    [SerializeField] private float lineOfSightTargetTolerance = 0.15f;

    #endregion

    #region Render Plane

    [Header("Near-Camera Render Plane")]
    [Tooltip("Distance from the gameplay camera used to reconstruct marker Shapes.")]
    [Min(0.01f)]
    [SerializeField] private float nearCameraRenderDistance = 0.5f;

    [Tooltip("Minimum distance beyond the camera near clip plane.")]
    [Min(0.001f)]
    [SerializeField] private float nearClipSafetyPadding = 0.03f;

    #endregion


    #region Runtime Mode Adaptation

    [System.NonSerialized] private float runtimeModeScaleMultiplier = 1f;

    private float EffectiveMarkerScale => markerMasterScale * runtimeModeScaleMultiplier;

    public float RuntimeModeScaleMultiplier => runtimeModeScaleMultiplier;

    public void SetRuntimeModeScaleMultiplier(float multiplier)
    {
        runtimeModeScaleMultiplier = Mathf.Max(0.05f, multiplier);
    }

    #endregion

    #region Public API

    public Vector2 MarkerTipOffsetPixels => markerTipOffsetPixels * EffectiveMarkerScale;
    public float MarkerMasterScale => markerMasterScale;

    public float BodyRadiusPixels => bodyRadiusPixels * EffectiveMarkerScale;
    public float TailWidthPixels => tailWidthPixels * EffectiveMarkerScale;
    public float TailHeightPixels => tailHeightPixels * EffectiveMarkerScale;
    public float TailBodyOverlapPixels => tailBodyOverlapPixels * EffectiveMarkerScale;

    public Vector2 TextOffsetPixels => textOffsetPixels * EffectiveMarkerScale;
    public float FontSizePixels => fontSizePixels * EffectiveMarkerScale;
    public Color TextColor => textColor;

    public float ScreenVisibilityPaddingPixels => screenVisibilityPaddingPixels;
    public bool RequireLineOfSight => requireLineOfSight;
    public LayerMask LineOfSightBlockingMask => lineOfSightBlockingMask;
    public float LineOfSightTargetTolerance => lineOfSightTargetTolerance;

    public float NearCameraRenderDistance => nearCameraRenderDistance;
    public float NearClipSafetyPadding => nearClipSafetyPadding;

    #endregion

    #region Validation

    private void OnValidate()
    {
        markerMasterScale = Mathf.Max(0.05f, markerMasterScale);
        bodyRadiusPixels = Mathf.Max(1f, bodyRadiusPixels);
        tailWidthPixels = Mathf.Max(1f, tailWidthPixels);
        tailHeightPixels = Mathf.Max(1f, tailHeightPixels);
        tailBodyOverlapPixels = Mathf.Max(0f, tailBodyOverlapPixels);
        fontSizePixels = Mathf.Max(1f, fontSizePixels);
        screenVisibilityPaddingPixels = Mathf.Max(0f, screenVisibilityPaddingPixels);
        lineOfSightTargetTolerance = Mathf.Max(0f, lineOfSightTargetTolerance);
        nearCameraRenderDistance = Mathf.Max(0.01f, nearCameraRenderDistance);
        nearClipSafetyPadding = Mathf.Max(0.001f, nearClipSafetyPadding);
    }

    #endregion
}
