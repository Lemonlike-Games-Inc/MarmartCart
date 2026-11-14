using UnityEngine;

/// <summary>
/// Universal presentation/profile data for the Shapes-based other-player pointers.
///
/// This profile owns HOW the pointers look and where they orbit around the
/// local player's HUD anchor. Player discovery / camera ownership remains in
/// PlayerWorldHUDSystem.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Player HUD/Other Player Pointer Profile",
    fileName = "OtherPlayerPointerProfile"
)]
public class OtherPlayerPointerProfile : ScriptableObject
{
    #region Orbit

    [Header("Orbit")]
    [Tooltip("Screen-space radius of the invisible circle that all other-player pointers sit on.")]
    [Min(1f)]
    [SerializeField] private float orbitRadiusPixels = 108f;

    [Tooltip("Moves the entire pointer orbit relative to the local HUDWorldAnchor screen position.")]
    [SerializeField] private Vector2 orbitCenterOffsetPixels = Vector2.zero;

    #endregion

    #region Render Plane

    [Header("Near-Camera Render Plane")]
    [Tooltip("Distance from the gameplay camera used to reconstruct all pointer Shapes.")]
    [Min(0.01f)]
    [SerializeField] private float nearCameraRenderDistance = 0.5f;

    [Tooltip("Minimum distance beyond the camera near clip plane.")]
    [Min(0.001f)]
    [SerializeField] private float nearClipSafetyPadding = 0.03f;

    #endregion

    #region Pointer Shape

    [Header("Rounded Pointer - Two Capsules")]
    [Tooltip(
        "Both capsules are authored in local screen pixels. +X is the direction the pointer faces. " +
        "The renderer rotates these local points toward the target player."
    )]
    [SerializeField] private Vector2 upperCapsuleStartPixels = new Vector2(-9f, 4.5f);

    [SerializeField] private Vector2 upperCapsuleEndPixels = new Vector2(10f, 0f);

    [SerializeField] private Vector2 lowerCapsuleStartPixels = new Vector2(-9f, -4.5f);

    [SerializeField] private Vector2 lowerCapsuleEndPixels = new Vector2(10f, 0f);

    [Tooltip("Thickness of both rounded capsules that overlap to form the pointer body.")]
    [Min(1f)]
    [SerializeField] private float pointerCapsuleThicknessPixels = 10f;

    [Tooltip("Uniform scale for the pointer body only, not the orbit radius.")]
    [Min(0.05f)]
    [SerializeField] private float pointerScale = 1f;

    #endregion


    #region Player Colors

    [Header("Target Player Colors")]
    [SerializeField] private Color player1Color = new Color(1f, 0.27f, 0.22f, 1f);
    [SerializeField] private Color player2Color = new Color(0.22f, 0.62f, 1f, 1f);
    [SerializeField] private Color player3Color = new Color(0.28f, 0.90f, 0.43f, 1f);
    [SerializeField] private Color player4Color = new Color(1f, 0.72f, 0.16f, 1f);

    #endregion


    #region Runtime Mode Adaptation

    [System.NonSerialized] private float runtimeModeScaleMultiplier = 1f;

    private float EffectivePointerBodyScale => pointerScale * runtimeModeScaleMultiplier;

    public float RuntimeModeScaleMultiplier => runtimeModeScaleMultiplier;

    public void SetRuntimeModeScaleMultiplier(float multiplier)
    {
        runtimeModeScaleMultiplier = Mathf.Max(0.05f, multiplier);
    }

    #endregion

    #region Public API

    public float OrbitRadiusPixels => orbitRadiusPixels * runtimeModeScaleMultiplier;
    public Vector2 OrbitCenterOffsetPixels => orbitCenterOffsetPixels * runtimeModeScaleMultiplier;

    public float NearCameraRenderDistance => nearCameraRenderDistance;
    public float NearClipSafetyPadding => nearClipSafetyPadding;

    public Vector2 UpperCapsuleStartPixels => upperCapsuleStartPixels * EffectivePointerBodyScale;
    public Vector2 UpperCapsuleEndPixels => upperCapsuleEndPixels * EffectivePointerBodyScale;
    public Vector2 LowerCapsuleStartPixels => lowerCapsuleStartPixels * EffectivePointerBodyScale;
    public Vector2 LowerCapsuleEndPixels => lowerCapsuleEndPixels * EffectivePointerBodyScale;
    public float PointerCapsuleThicknessPixels => pointerCapsuleThicknessPixels * EffectivePointerBodyScale;
    public float PointerScale => pointerScale;


    public Color GetPlayerColor(int playerIndex)
    {
        switch (playerIndex)
        {
            case 1: return player1Color;
            case 2: return player2Color;
            case 3: return player3Color;
            case 4: return player4Color;
            default: return Color.white;
        }
    }

    #endregion

    #region Validation

    private void OnValidate()
    {
        orbitRadiusPixels = Mathf.Max(1f, orbitRadiusPixels);
        nearCameraRenderDistance = Mathf.Max(0.01f, nearCameraRenderDistance);
        nearClipSafetyPadding = Mathf.Max(0.001f, nearClipSafetyPadding);
        pointerCapsuleThicknessPixels = Mathf.Max(1f, pointerCapsuleThicknessPixels);
        pointerScale = Mathf.Max(0.05f, pointerScale);
    }

    #endregion
}
