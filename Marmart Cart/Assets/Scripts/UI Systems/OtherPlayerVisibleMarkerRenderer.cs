using Shapes;
using UnityEngine;

/// <summary>
/// Scene-level Shapes renderer that highlights OTHER players while those
/// players are actually visible in the current gameplay camera.
///
/// For each gameplay camera:
/// - determine which player owns that viewport;
/// - never draw that player's own marker;
/// - loop over every other currently-bound player (2/3/4-player safe);
/// - draw a downward-pointing raindrop/map-pin marker above targets that are
///   on-screen and, optionally, not occluded by world geometry.
///
/// This is intentionally separate from OtherPlayerPointerRenderer:
/// - OtherPlayerPointerRenderer = directional/orbit information.
/// - OtherPlayerVisibleMarkerRenderer = "that visible cart is Player X".
/// </summary>
[DisallowMultipleComponent]
public class OtherPlayerVisibleMarkerRenderer : ImmediateModeShapeDrawer
{
    #region References

    [Header("References")]
    [SerializeField] private PlayerWorldHUDSystem hudSystem;

    [Tooltip(
        "Existing pointer profile used only as the shared P1-P4 color source, " +
        "so orbit pointers and visible markers always match."
    )]
    [SerializeField] private OtherPlayerPointerProfile playerColorProfile;

    [SerializeField] private OtherPlayerVisibleMarkerProfile markerProfile;

    #endregion

    #region Visibility

    [Header("Visibility")]
    [SerializeField] private bool drawingEnabled = true;

    #endregion


    #region Mode Adaptation

    public void SetModeScaleMultiplier(float multiplier)
    {
        if (markerProfile != null)
        {
            markerProfile.SetRuntimeModeScaleMultiplier(multiplier);
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
        if (cam == null || hudSystem == null || markerProfile == null) return;

        if (!hudSystem.TryGetRenderableSlotForCamera(cam, out int ownerPlayerIndex, out _)) return;

        float renderDepth = Mathf.Max(
            markerProfile.NearCameraRenderDistance,
            cam.nearClipPlane + markerProfile.NearClipSafetyPadding
        );

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
                // Critical behavior: never show the owning player's own marker
                // in their own split-screen viewport.
                if (targetPlayerIndex == ownerPlayerIndex) continue;

                GameObject targetRoot = hudSystem.GetPlayerRoot(targetPlayerIndex);
                Transform targetAnchor = hudSystem.GetHUDWorldAnchor(targetPlayerIndex);

                if (targetRoot == null || targetAnchor == null) continue;

                Vector3 targetScreen = cam.WorldToScreenPoint(targetAnchor.position);

                if (!IsTargetOnScreen(cam, targetScreen)) continue;
                if (!HasLineOfSight(cam, targetScreen, targetAnchor.position, targetRoot.transform)) continue;

                DrawTargetMarker(
                    cam,
                    renderDepth,
                    targetScreen,
                    targetPlayerIndex
                );
            }
        }
    }

    #endregion

    #region Visibility Tests

    private bool IsTargetOnScreen(Camera cam, Vector3 targetScreen)
    {
        if (targetScreen.z <= 0f) return false;

        float padding = markerProfile.ScreenVisibilityPaddingPixels;
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
        Vector3 targetScreen,
        int targetPlayerIndex)
    {
        Vector2 targetScreen2D = new Vector2(targetScreen.x, targetScreen.y);

        // This point is the downward TIP of the raindrop marker.
        Vector2 tip = targetScreen2D + markerProfile.MarkerTipOffsetPixels;

        float radius = markerProfile.BodyRadiusPixels;
        float tailHeight = markerProfile.TailHeightPixels;
        float tailHalfWidth = markerProfile.TailWidthPixels * 0.5f;
        float overlap = Mathf.Min(markerProfile.TailBodyOverlapPixels, radius * 1.8f);

        Vector2 tailBaseCenter = tip + new Vector2(0f, tailHeight);

        // Place the circular body so its lower portion overlaps the triangle's
        // base, making the two simple Shapes read as one continuous raindrop.
        Vector2 bodyCenter = tailBaseCenter + new Vector2(0f, radius - overlap);

        Color playerColor = playerColorProfile != null
            ? playerColorProfile.GetPlayerColor(targetPlayerIndex)
            : Color.white;

        // Tail first, body second. Drawing the disc after the triangle hides
        // the triangle's top seam and gives a cleaner rounded silhouette.
        Draw.Triangle(
            ScreenPointToWorld(cam, tailBaseCenter + new Vector2(-tailHalfWidth, 0f), renderDepth),
            ScreenPointToWorld(cam, tailBaseCenter + new Vector2(tailHalfWidth, 0f), renderDepth),
            ScreenPointToWorld(cam, tip, renderDepth),
            playerColor
        );

        Vector3 bodyWorld = ScreenPointToWorld(cam, bodyCenter, renderDepth);

        Draw.Disc(
            bodyWorld,
            cam.transform.rotation,
            radius,
            playerColor
        );

        DrawPlayerLabel(
            cam,
            renderDepth,
            bodyCenter,
            targetPlayerIndex
        );
    }

    private void DrawPlayerLabel(
        Camera cam,
        float renderDepth,
        Vector2 bodyCenter,
        int playerIndex)
    {
        Vector2 labelScreen = bodyCenter + markerProfile.TextOffsetPixels;
        Vector3 labelWorld = ScreenPointToWorld(cam, labelScreen, renderDepth);

        Draw.FontSize = PixelsToWorldSizeAtDepth(
            cam,
            labelWorld,
            markerProfile.FontSizePixels
        );

        Color previousColor = Draw.Color;
        Draw.Color = markerProfile.TextColor;

        Draw.Text(
            labelWorld,
            cam.transform.rotation,
            $"P{playerIndex}",
            TextAlign.Center
        );

        Draw.Color = previousColor;
    }

    #endregion

    #region Helpers

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

    private float PixelsToWorldSizeAtDepth(
        Camera cam,
        Vector3 worldPosition,
        float pixelSize)
    {
        Vector3 screenA = cam.WorldToScreenPoint(worldPosition);
        Vector3 screenB = screenA;
        screenB.y += pixelSize;

        Vector3 worldA = cam.ScreenToWorldPoint(screenA);
        Vector3 worldB = cam.ScreenToWorldPoint(screenB);

        return Vector3.Distance(worldA, worldB);
    }

    #endregion
}
