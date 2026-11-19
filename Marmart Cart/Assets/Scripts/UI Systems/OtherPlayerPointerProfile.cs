using UnityEngine;

/// <summary>
/// Universal presentation/profile data for the Shapes-based other-player pointers.
///
/// This profile owns HOW the pointers look and where they orbit around the
/// local player's HUD anchor. Values are 1920x1080 reference pixels; the
/// renderer applies UI scale and the 2P/4P factor. Player colours come from
/// PlayerIdentityProfile.
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

    #region Public API

    public float OrbitRadiusPixels => orbitRadiusPixels;
    public Vector2 OrbitCenterOffsetPixels => orbitCenterOffsetPixels;

    public float NearCameraRenderDistance => nearCameraRenderDistance;
    public float NearClipSafetyPadding => nearClipSafetyPadding;

    // Pointer body values include the authored pointerScale.
    public Vector2 UpperCapsuleStartPixels => upperCapsuleStartPixels * pointerScale;
    public Vector2 UpperCapsuleEndPixels => upperCapsuleEndPixels * pointerScale;
    public Vector2 LowerCapsuleStartPixels => lowerCapsuleStartPixels * pointerScale;
    public Vector2 LowerCapsuleEndPixels => lowerCapsuleEndPixels * pointerScale;
    public float PointerCapsuleThicknessPixels => pointerCapsuleThicknessPixels * pointerScale;
    public float PointerScale => pointerScale;

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
