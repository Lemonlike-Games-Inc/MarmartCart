using Shapes;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Scene-level Shapes renderer for pointing from each local player toward every
/// OTHER currently-bound player.
///
/// - ONE scene-level renderer, not one GameObject per pointer.
/// - PlayerViewService says which player owns each camera; PlayerWorldHUDSystem
///   supplies player roots and HUD anchors.
/// - DrawShapes(Camera) is called once per gameplay camera, then loops P1..P4 and
///   draws only the OTHER players that currently exist.
/// - Pointers are shape-only; no player text labels are drawn.
/// - Sizes are 1920x1080 reference pixels times UI scale and the 2P/4P factor.
/// </summary>
[DisallowMultipleComponent]
public class OtherPlayerPointerRenderer : ImmediateModeShapeDrawer
{
    #region References

    [Header("References")]
    [SerializeField] private PlayerWorldHUDSystem hudSystem;
    [SerializeField] private OtherPlayerPointerProfile profile;
    [SerializeField] private PlayerIdentityProfile playerIdentity;

    #endregion

    #region Visibility

    [Header("Visibility")]
    [SerializeField] private bool drawingEnabled = true;
    [SerializeField] private bool skipWhenOwnerBehindCamera = true;

    #endregion

    #region Unity

    private void Awake()
    {
        if (hudSystem == null) hudSystem = FindFirstObjectByType<PlayerWorldHUDSystem>();
    }

    public override void DrawShapes(Camera cam)
    {
        if (!drawingEnabled) return;
        if (cam == null || hudSystem == null || profile == null || playerIdentity == null) return;

        if (!HudShapes.TryGetViewPlayer(cam, out int ownerPlayerIndex)) return;
        if (!PlayerHudVisibility.IsShown(ownerPlayerIndex)) return;

        // Pointers sit inside the lit circle, so Hidden fades them out with the viewer's darkness.
        DarknessHudMode darkMode = PlayerViewDarkness.GetHudMode(ownerPlayerIndex, DarknessHudElement.RivalPointers, out float darkStrength);
        float alpha = darkMode == DarknessHudMode.Hidden ? 1f - darkStrength : 1f;
        if (alpha <= 0f) return;

        RenderPassEvent pass = darkMode == DarknessHudMode.OnTop && darkStrength > 0f ? HudShapes.AboveDarknessPass : HudShapes.HudPass;

        if (!hudSystem.TryGetRenderableAnchor(ownerPlayerIndex, out Transform ownerHUDAnchor)) return;

        GameObject ownerRoot = hudSystem.GetPlayerRoot(ownerPlayerIndex);
        if (ownerRoot == null) return;

        Vector3 ownerAnchorScreen = cam.WorldToScreenPoint(ownerHUDAnchor.position);
        if (skipWhenOwnerBehindCamera && ownerAnchorScreen.z <= 0f) return;

        float scale = HudShapes.Scale(profile.GetModeScale(HudShapes.ViewCount()));
        float renderDepth = HudShapes.RenderDepth(cam, profile.NearCameraRenderDistance, profile.NearClipSafetyPadding);
        Vector2 orbitCenter = (Vector2)ownerAnchorScreen + profile.OrbitCenterOffsetPixels * scale;

        using (Draw.Command(cam, pass))
        {
            HudShapes.ApplyHudDrawState();

            for (int targetPlayerIndex = 1; targetPlayerIndex <= PlayerWorldHUDSystem.MaxPlayerSlots; targetPlayerIndex++)
            {
                if (targetPlayerIndex == ownerPlayerIndex) continue;

                GameObject targetRoot = hudSystem.GetPlayerRoot(targetPlayerIndex);
                if (targetRoot == null) continue;

                DrawPointerToTarget(
                    cam,
                    renderDepth,
                    scale,
                    orbitCenter,
                    ownerRoot.transform,
                    targetRoot.transform,
                    targetPlayerIndex,
                    alpha
                );
            }
        }
    }

    #endregion

    #region Pointer Drawing

    private void DrawPointerToTarget(
        Camera cam,
        float renderDepth,
        float scale,
        Vector2 orbitCenter,
        Transform owner,
        Transform target,
        int targetPlayerIndex,
        float alpha)
    {
        Vector3 worldDirection = target.position - owner.position;

        // Pointers show horizontal direction only, so ignore height differences.
        worldDirection.y = 0f;

        if (worldDirection.sqrMagnitude < 0.0001f) return;
        worldDirection.Normalize();

        // Convert the horizontal world direction into this camera's screen basis.
        // Stable for split-screen and tilted / isometric cameras without
        // projecting the target itself.
        Vector2 screenDirection = new Vector2(
            Vector3.Dot(worldDirection, cam.transform.right),
            Vector3.Dot(worldDirection, cam.transform.up)
        );

        if (screenDirection.sqrMagnitude < 0.0001f) return;
        screenDirection.Normalize();

        Vector2 pointerCenter = orbitCenter + screenDirection * (profile.OrbitRadiusPixels * scale);
        float pointerAngleRadians = Mathf.Atan2(screenDirection.y, screenDirection.x);

        Color color = playerIdentity.GetColor(targetPlayerIndex);
        color.a *= alpha;

        DrawRoundedPointerBody(
            cam,
            renderDepth,
            scale,
            pointerCenter,
            pointerAngleRadians,
            color
        );
    }

    private void DrawRoundedPointerBody(
        Camera cam,
        float renderDepth,
        float scale,
        Vector2 pointerCenter,
        float pointerAngleRadians,
        Color color)
    {
        Vector2 upperStart = pointerCenter + HudShapes.Rotate(profile.UpperCapsuleStartPixels * scale, pointerAngleRadians);
        Vector2 upperEnd = pointerCenter + HudShapes.Rotate(profile.UpperCapsuleEndPixels * scale, pointerAngleRadians);
        Vector2 lowerStart = pointerCenter + HudShapes.Rotate(profile.LowerCapsuleStartPixels * scale, pointerAngleRadians);
        Vector2 lowerEnd = pointerCenter + HudShapes.Rotate(profile.LowerCapsuleEndPixels * scale, pointerAngleRadians);
        float thickness = profile.PointerCapsuleThicknessPixels * scale;

        Draw.Line(
            HudShapes.ScreenToWorld(cam, upperStart, renderDepth),
            HudShapes.ScreenToWorld(cam, upperEnd, renderDepth),
            thickness,
            color
        );

        Draw.Line(
            HudShapes.ScreenToWorld(cam, lowerStart, renderDepth),
            HudShapes.ScreenToWorld(cam, lowerEnd, renderDepth),
            thickness,
            color
        );
    }

    #endregion
}
