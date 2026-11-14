using Shapes;
using UnityEngine;

/// <summary>
/// Scene-level Shapes renderer for pointing from each local player toward every
/// OTHER currently-bound player.
///
/// Important architecture:
/// - ONE scene-level renderer, not one GameObject per pointer.
/// - PlayerWorldHUDSystem remains the source of truth for player roots,
///   gameplay cameras, and local HUD anchors.
/// - DrawShapes(Camera) is called once per gameplay camera; that camera tells
///   us which player owns the viewport.
/// - The renderer then loops P1..P4 and draws only the OTHER players that
///   currently exist.
/// - Pointers are shape-only; no player text labels are drawn.
///
/// Therefore the exact same script supports 2, 3, or 4 players automatically.
/// </summary>
[DisallowMultipleComponent]
public class OtherPlayerPointerRenderer : ImmediateModeShapeDrawer
{
    #region References

    [Header("References")]
    [SerializeField] private PlayerWorldHUDSystem hudSystem;
    [SerializeField] private OtherPlayerPointerProfile profile;

    #endregion

    #region Visibility

    [Header("Visibility")]
    [SerializeField] private bool drawingEnabled = true;
    [SerializeField] private bool skipWhenOwnerBehindCamera = true;

    #endregion


    #region Mode Adaptation

    public void SetModeScaleMultiplier(float multiplier)
    {
        if (profile != null)
        {
            profile.SetRuntimeModeScaleMultiplier(multiplier);
        }
    }

    #endregion

    #region Unity

    private void Awake()
    {
        if (hudSystem == null) hudSystem = FindFirstObjectByType<PlayerWorldHUDSystem>();
    }

    public override void DrawShapes(Camera cam)
    {
        if (!drawingEnabled) return;
        if (cam == null || hudSystem == null || profile == null) return;

        if (!hudSystem.TryGetRenderableSlotForCamera(cam, out int ownerPlayerIndex, out Transform ownerHUDAnchor)) return;
        if (ownerHUDAnchor == null) return;

        GameObject ownerRoot = hudSystem.GetPlayerRoot(ownerPlayerIndex);
        if (ownerRoot == null) return;

        Vector3 ownerAnchorScreen = cam.WorldToScreenPoint(ownerHUDAnchor.position);
        if (skipWhenOwnerBehindCamera && ownerAnchorScreen.z <= 0f) return;

        float renderDepth = Mathf.Max(
            profile.NearCameraRenderDistance,
            cam.nearClipPlane + profile.NearClipSafetyPadding
        );

        Vector2 orbitCenter = new Vector2(ownerAnchorScreen.x, ownerAnchorScreen.y) + profile.OrbitCenterOffsetPixels;

        using (Draw.Command(cam))
        {
            Draw.ResetAllDrawStates();
            Draw.BlendMode = ShapesBlendMode.Transparent;
            Draw.ThicknessSpace = ThicknessSpace.Pixels;
            Draw.RadiusSpace = ThicknessSpace.Pixels;
            Draw.LineGeometry = LineGeometry.Billboard;
            Draw.LineEndCaps = LineEndCap.Round;

            for (int targetPlayerIndex = 1; targetPlayerIndex <= PlayerWorldHUDSystem.MaxPlayerSlots; targetPlayerIndex++)
            {
                if (targetPlayerIndex == ownerPlayerIndex) continue;

                GameObject targetRoot = hudSystem.GetPlayerRoot(targetPlayerIndex);
                if (targetRoot == null) continue;

                DrawPointerToTarget(
                    cam,
                    renderDepth,
                    orbitCenter,
                    ownerRoot.transform,
                    targetRoot.transform,
                    targetPlayerIndex
                );
            }
        }
    }

    #endregion

    #region Pointer Drawing

    private void DrawPointerToTarget(
        Camera cam,
        float renderDepth,
        Vector2 orbitCenter,
        Transform owner,
        Transform target,
        int targetPlayerIndex)
    {
        Vector3 worldDirection = target.position - owner.position;

        // The old mesh pointer intentionally ignored vertical difference.
        // Preserve that behavior so pointers communicate horizontal player direction.
        worldDirection.y = 0f;

        if (worldDirection.sqrMagnitude < 0.0001f) return;
        worldDirection.Normalize();

        // Convert the horizontal world direction into this gameplay camera's
        // screen-space basis. This remains stable for split-screen and tilted /
        // isometric gameplay cameras without needing to project the target itself.
        Vector2 screenDirection = new Vector2(
            Vector3.Dot(worldDirection, cam.transform.right),
            Vector3.Dot(worldDirection, cam.transform.up)
        );

        if (screenDirection.sqrMagnitude < 0.0001f) return;
        screenDirection.Normalize();

        Vector2 pointerCenter = orbitCenter + screenDirection * profile.OrbitRadiusPixels;
        float pointerAngleRadians = Mathf.Atan2(screenDirection.y, screenDirection.x);
        Color pointerColor = profile.GetPlayerColor(targetPlayerIndex);

        DrawRoundedPointerBody(
            cam,
            renderDepth,
            pointerCenter,
            pointerAngleRadians,
            pointerColor
        );

    }

    private void DrawRoundedPointerBody(
        Camera cam,
        float renderDepth,
        Vector2 pointerCenter,
        float pointerAngleRadians,
        Color color)
    {
        Vector2 upperStart = pointerCenter + Rotate2D(profile.UpperCapsuleStartPixels, pointerAngleRadians);
        Vector2 upperEnd = pointerCenter + Rotate2D(profile.UpperCapsuleEndPixels, pointerAngleRadians);
        Vector2 lowerStart = pointerCenter + Rotate2D(profile.LowerCapsuleStartPixels, pointerAngleRadians);
        Vector2 lowerEnd = pointerCenter + Rotate2D(profile.LowerCapsuleEndPixels, pointerAngleRadians);

        Draw.Line(
            ScreenPointToWorld(cam, upperStart, renderDepth),
            ScreenPointToWorld(cam, upperEnd, renderDepth),
            profile.PointerCapsuleThicknessPixels,
            color
        );

        Draw.Line(
            ScreenPointToWorld(cam, lowerStart, renderDepth),
            ScreenPointToWorld(cam, lowerEnd, renderDepth),
            profile.PointerCapsuleThicknessPixels,
            color
        );
    }

    #endregion

    #region Helpers

    private Vector2 Rotate2D(Vector2 value, float radians)
    {
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);

        return new Vector2(
            value.x * cos - value.y * sin,
            value.x * sin + value.y * cos
        );
    }

    private Vector3 ScreenPointToWorld(
        Camera cam,
        Vector2 screenPoint,
        float screenDepth)
    {
        return cam.ScreenToWorldPoint(
            new Vector3(
                screenPoint.x,
                screenPoint.y,
                screenDepth
            )
        );
    }


    #endregion
}
