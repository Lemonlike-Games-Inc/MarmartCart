using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Shapes-only presentation settings for the local player's static aim preview.
/// Gameplay range, timing, ground mask, and impact radii live in
/// PowerupGameplayProfile instead.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Powerups/Aim Profile",
    fileName = "PowerupAimProfile"
)]
public class PowerupAimProfile : ScriptableObject
{
    #region Projection

    [Header("Near-Camera Projection")]
    [Tooltip("Reconstruct every projected world sample on a plane close to the gameplay camera so shelves cannot hide the preview.")]
    [SerializeField] private bool useNearCameraRenderPlane = true;

    [Min(0.01f)]
    [SerializeField] private float nearCameraRenderDistance = 1f;

    [Min(0f)]
    [SerializeField] private float nearClipSafetyPadding = 0.05f;

    [SerializeField] private bool skipPointsBehindCamera = true;

    #endregion

    #region Trajectory

    [Header("Trajectory")]
    [Range(4, 96)]
    [SerializeField] private int trajectorySampleCount = 28;

    [Min(0.1f)]
    [SerializeField] private float trajectoryThicknessPixels = 4f;

    [Min(0f)]
    [SerializeField] private float trajectoryUnderlayExtraPixels = 3f;

    [SerializeField] private Color trajectoryUnderlayColor = new Color(0.035f, 0.04f, 0.06f, 0.85f);

    #endregion

    #region Landing Envelope

    [Header("Landing Envelope")]
    [Range(12, 128)]
    [SerializeField] private int landingCircleSampleCount = 48;

    [Min(0.1f)]
    [SerializeField] private float landingOutlineThicknessPixels = 4f;

    [Min(0f)]
    [SerializeField] private float landingUnderlayExtraPixels = 3f;

    [Range(0f, 1f)]
    [SerializeField] private float landingFillAlpha = 0.14f;

    [Tooltip("Total screen-space width and height of the endpoint X. It stays identical for every power-up and impact radius.")]
    [Min(1f)]
    [SerializeField] private float centerCrossSizePixels = 22f;

    [Min(0.1f)]
    [SerializeField] private float centerCrossThicknessPixels = 3f;

    [Min(0.1f)]
    [SerializeField] private float centerDotRadiusPixels = 4f;

    #endregion

    #region Colors

    [Header("Shared Preview Color")]
    [FormerlySerializedAs("tomatoColor")]
    [SerializeField] private Color previewColor = new Color(1f, 0.24f, 0.12f, 1f);

    [Tooltip("Shared warning color used whenever the trajectory first hits a configured blocking layer.")]
    [SerializeField] private Color obstructedPreviewColor = new Color(1f, 0.08f, 0.04f, 1f);

    #endregion

    #region Public Values

    public bool UseNearCameraRenderPlane => useNearCameraRenderPlane;
    public float NearCameraRenderDistance => nearCameraRenderDistance;
    public float NearClipSafetyPadding => nearClipSafetyPadding;
    public bool SkipPointsBehindCamera => skipPointsBehindCamera;
    public int TrajectorySampleCount => trajectorySampleCount;
    public float TrajectoryThicknessPixels => trajectoryThicknessPixels;
    public float TrajectoryUnderlayExtraPixels => trajectoryUnderlayExtraPixels;
    public Color TrajectoryUnderlayColor => trajectoryUnderlayColor;
    public int LandingCircleSampleCount => landingCircleSampleCount;
    public float LandingOutlineThicknessPixels => landingOutlineThicknessPixels;
    public float LandingUnderlayExtraPixels => landingUnderlayExtraPixels;
    public float CenterCrossSizePixels => centerCrossSizePixels;
    public float CenterCrossThicknessPixels => centerCrossThicknessPixels;
    public float CenterDotRadiusPixels => centerDotRadiusPixels;

    public Color GetMainColor(bool trajectoryObstructed)
    {
        return trajectoryObstructed
            ? obstructedPreviewColor
            : previewColor;
    }

    public Color GetFillColor(bool trajectoryObstructed)
    {
        Color color = GetMainColor(trajectoryObstructed);
        color.a *= landingFillAlpha;
        return color;
    }

    #endregion

    #region Validation

    private void OnValidate()
    {
        nearCameraRenderDistance = Mathf.Max(0.01f, nearCameraRenderDistance);
        nearClipSafetyPadding = Mathf.Max(0f, nearClipSafetyPadding);

        trajectorySampleCount = Mathf.Clamp(trajectorySampleCount, 4, 96);
        trajectoryThicknessPixels = Mathf.Max(0.1f, trajectoryThicknessPixels);
        trajectoryUnderlayExtraPixels = Mathf.Max(0f, trajectoryUnderlayExtraPixels);

        landingCircleSampleCount = Mathf.Clamp(landingCircleSampleCount, 12, 128);
        landingOutlineThicknessPixels = Mathf.Max(0.1f, landingOutlineThicknessPixels);
        landingUnderlayExtraPixels = Mathf.Max(0f, landingUnderlayExtraPixels);
        landingFillAlpha = Mathf.Clamp01(landingFillAlpha);
        centerCrossSizePixels = Mathf.Max(1f, centerCrossSizePixels);
        centerCrossThicknessPixels = Mathf.Max(0.1f, centerCrossThicknessPixels);
        centerDotRadiusPixels = Mathf.Max(0.1f, centerDotRadiusPixels);
    }

    #endregion
}
