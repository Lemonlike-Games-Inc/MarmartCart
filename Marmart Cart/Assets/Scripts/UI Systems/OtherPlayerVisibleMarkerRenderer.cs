using Shapes;
using UnityEngine;

/// <summary>
/// Scene-level Shapes renderer that highlights OTHER players while those
/// players are actually visible in the current gameplay camera.
///
/// For each gameplay camera:
/// - PlayerViewService says which player owns that viewport;
/// - never draw that player's own marker;
/// - loop over every other currently-bound player (2/3/4-player safe);
/// - draw a downward-pointing raindrop/map-pin marker above targets that are
///   on-screen and, optionally, not occluded by world geometry.
///
/// This is intentionally separate from OtherPlayerPointerRenderer:
/// - OtherPlayerPointerRenderer = directional/orbit information.
/// - OtherPlayerVisibleMarkerRenderer = "that visible cart is Player X".
///
/// Sizes are 1920x1080 reference pixels times UI scale and the 2P/4P factor.
/// </summary>
[DisallowMultipleComponent]
public class OtherPlayerVisibleMarkerRenderer : ImmediateModeShapeDrawer
{
    #region References

    [Header("References")]
    [SerializeField] private PlayerWorldHUDSystem hudSystem;
    [SerializeField] private OtherPlayerVisibleMarkerProfile markerProfile;

    [Tooltip("Player colours and labels.")]
    [SerializeField] private PlayerIdentityProfile playerIdentity;

    #endregion

    #region Visibility

    [Header("Visibility")]
    [SerializeField] private bool drawingEnabled = true;

    #endregion

    #region Unity

    private void Awake()
    {
        if (hudSystem == null) hudSystem = FindFirstObjectByType<PlayerWorldHUDSystem>();
    }

    public override void DrawShapes(Camera cam)
    {
        if (!drawingEnabled) return;
        if (cam == null || hudSystem == null || markerProfile == null || playerIdentity == null) return;

        if (!HudShapes.TryGetViewPlayer(cam, out int ownerPlayerIndex)) return;
        if (!hudSystem.TryGetRenderableAnchor(ownerPlayerIndex, out _)) return;

        float uiScale = HudShapes.Scale();
        float scale = uiScale * markerProfile.GetModeScale(HudShapes.ViewCount());
        float renderDepth = HudShapes.RenderDepth(cam, markerProfile.NearCameraRenderDistance, markerProfile.NearClipSafetyPadding);

        using (Draw.Command(cam))
        {
            HudShapes.ApplyHudDrawState();

            for (int targetPlayerIndex = 1; targetPlayerIndex <= PlayerWorldHUDSystem.MaxPlayerSlots; targetPlayerIndex++)
            {
                // Never show the owning player's own marker in their own viewport.
                if (targetPlayerIndex == ownerPlayerIndex) continue;

                GameObject targetRoot = hudSystem.GetPlayerRoot(targetPlayerIndex);
                Transform targetAnchor = hudSystem.GetHUDWorldAnchor(targetPlayerIndex);

                if (targetRoot == null || targetAnchor == null) continue;

                Vector3 targetScreen = cam.WorldToScreenPoint(targetAnchor.position);

                if (!IsTargetOnScreen(cam, targetScreen, markerProfile.ScreenVisibilityPaddingPixels * uiScale)) continue;
                if (!HasLineOfSight(cam, targetScreen, targetAnchor.position, targetRoot.transform)) continue;

                DrawTargetMarker(cam, renderDepth, scale, targetScreen, targetPlayerIndex);
            }
        }
    }

    #endregion

    #region Visibility Tests

    private static bool IsTargetOnScreen(Camera cam, Vector3 targetScreen, float padding)
    {
        if (targetScreen.z <= 0f) return false;

        Rect rect = cam.pixelRect;

        return targetScreen.x >= rect.xMin - padding &&
               targetScreen.x <= rect.xMax + padding &&
               targetScreen.y >= rect.yMin - padding &&
               targetScreen.y <= rect.yMax + padding;
    }

    private bool HasLineOfSight(
        Camera cam,
        Vector3 targetScreen,
        Vector3 targetWorld,
        Transform targetRoot)
    {
        if (!markerProfile.RequireLineOfSight) return true;

        Ray ray = cam.ScreenPointToRay(targetScreen);
        float targetDistance = Vector3.Dot(targetWorld - ray.origin, ray.direction);

        if (targetDistance <= 0f) return false;

        float rayDistance = targetDistance + markerProfile.LineOfSightTargetTolerance;

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                rayDistance,
                markerProfile.LineOfSightBlockingMask,
                QueryTriggerInteraction.Ignore))
        {
            // No blocker before the target position.
            return true;
        }

        // If the first thing hit belongs to the target player itself, the
        // target is visible. Otherwise some other world/player geometry is in front.
        return hit.transform == targetRoot || hit.transform.IsChildOf(targetRoot);
    }

    #endregion

    #region Marker Drawing

    private void DrawTargetMarker(
        Camera cam,
        float renderDepth,
        float scale,
        Vector3 targetScreen,
        int targetPlayerIndex)
    {
        // This point is the downward TIP of the raindrop marker.
        Vector2 tip = (Vector2)targetScreen + markerProfile.MarkerTipOffsetPixels * scale;

        float radius = markerProfile.BodyRadiusPixels * scale;
        float tailHeight = markerProfile.TailHeightPixels * scale;
        float tailHalfWidth = markerProfile.TailWidthPixels * scale * 0.5f;
        float overlap = Mathf.Min(markerProfile.TailBodyOverlapPixels * scale, radius * 1.8f);

        Vector2 tailBaseCenter = tip + new Vector2(0f, tailHeight);

        // Place the circular body so its lower portion overlaps the triangle's
        // base, making the two simple Shapes read as one continuous raindrop.
        Vector2 bodyCenter = tailBaseCenter + new Vector2(0f, radius - overlap);

        Color playerColor = playerIdentity.GetColor(targetPlayerIndex);

        // Tail first, body second. Drawing the disc after the triangle hides
        // the triangle's top seam and gives a cleaner rounded silhouette.
        Draw.Triangle(
            HudShapes.ScreenToWorld(cam, tailBaseCenter + new Vector2(-tailHalfWidth, 0f), renderDepth),
            HudShapes.ScreenToWorld(cam, tailBaseCenter + new Vector2(tailHalfWidth, 0f), renderDepth),
            HudShapes.ScreenToWorld(cam, tip, renderDepth),
            playerColor
        );

        Draw.Disc(
            HudShapes.ScreenToWorld(cam, bodyCenter, renderDepth),
            cam.transform.rotation,
            radius,
            playerColor
        );

        DrawPlayerLabel(cam, renderDepth, scale, bodyCenter, targetPlayerIndex);
    }

    private void DrawPlayerLabel(
        Camera cam,
        float renderDepth,
        float scale,
        Vector2 bodyCenter,
        int playerIndex)
    {
        Vector2 labelScreen = bodyCenter + markerProfile.TextOffsetPixels * scale;
        Vector3 labelWorld = HudShapes.ScreenToWorld(cam, labelScreen, renderDepth);

        Draw.FontSize = HudShapes.PixelsToWorldSize(cam, labelWorld, markerProfile.FontSizePixels * scale);

        Color previousColor = Draw.Color;
        Draw.Color = markerProfile.TextColor;

        Draw.Text(
            labelWorld,
            cam.transform.rotation,
            playerIdentity.GetLabel(playerIndex),
            TextAlign.Center
        );

        Draw.Color = previousColor;
    }

    #endregion
}
