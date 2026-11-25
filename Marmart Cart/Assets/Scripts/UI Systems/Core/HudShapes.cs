using Shapes;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Shared helpers for Shapes HUD drawers. See Docs/Design/ui-architecture.md.
///
/// The Shapes HUD is drawn on a plane just in front of each player camera.
/// Sizes and offsets are authored in 1920x1080 reference pixels and multiplied
/// by Scale(): the UI scale from PlayerViewService times the drawer's 2P/4P factor,
/// which each drawer reads from its own profile using ViewCount().
/// </summary>
public static class HudShapes
{
    /// <summary>
    /// Passes in each player camera: the normal HUD (Draw.Command's default), then the
    /// PlayerViewDarkness overlay, then HUD set to draw on top of it. All are before
    /// post-processing, so HUD looks the same in either pass.
    /// </summary>
    public const RenderPassEvent HudPass = RenderPassEvent.BeforeRenderingPostProcessing;
    public const RenderPassEvent DarknessPass = HudPass + 1;
    public const RenderPassEvent AboveDarknessPass = HudPass + 2;

    /// <summary>Darkness strength at which a Hidden part stops drawing, for drawers that cannot fade.</summary>
    public const float HiddenAtStrength = 0.5f;

    /// <summary>
    /// The pass a HUD part draws in for this viewer: Above Darkness Pass when its darkness
    /// setting is On Top and the view is dark, else Hud Pass. False when its setting is
    /// Hidden and the view is dark enough to hide it.
    /// </summary>
    public static bool TryGetDarknessPass(int playerIndex, DarknessHudElement element, out RenderPassEvent pass)
    {
        DarknessHudMode mode = PlayerViewDarkness.GetHudMode(playerIndex, element, out float strength);
        pass = mode == DarknessHudMode.OnTop && strength > 0f ? AboveDarknessPass : HudPass;
        return mode != DarknessHudMode.Hidden || strength < HiddenAtStrength;
    }

    /// <summary>Above Darkness Pass while this viewer is dark at all, else Hud Pass. For parts that always stay on top, like the split-screen dividers.</summary>
    public static RenderPassEvent OnTopPass(int playerIndex)
    {
        return PlayerViewDarkness.StrengthFor(playerIndex) > 0f ? AboveDarknessPass : HudPass;
    }

    /// <summary>The player whose view this camera renders.</summary>
    public static bool TryGetViewPlayer(Camera cam, out int playerIndex)
    {
        PlayerViewService views = PlayerViewService.Instance;

        if (views == null)
        {
            playerIndex = 0;
            return false;
        }

        return views.TryGetPlayerIndex(cam, out playerIndex);
    }

    /// <summary>
    /// Number of active player views (2 without a PlayerViewService). Drawers pass it
    /// to their own profile to get its 2P/4P values.
    /// </summary>
    public static int ViewCount()
    {
        PlayerViewService views = PlayerViewService.Instance;
        return views != null ? views.ActivePlayerCount : 2;
    }

    /// <summary>UI scale times a drawer's 2P/4P factor. The UI scale is 1 without a PlayerViewService.</summary>
    public static float Scale(float modeFactor = 1f)
    {
        PlayerViewService views = PlayerViewService.Instance;
        return (views != null ? views.UiScale : 1f) * modeFactor;
    }

    /// <summary>Distance in front of the camera where HUD shapes are drawn.</summary>
    public static float RenderDepth(Camera cam, float nearCameraDistance, float nearClipPadding)
    {
        return Mathf.Max(nearCameraDistance, cam.nearClipPlane + nearClipPadding);
    }

    /// <summary>Standard state for screen-space HUD: transparent, pixel thickness and radius, billboard lines, round caps.</summary>
    public static void ApplyHudDrawState()
    {
        Draw.ResetAllDrawStates();
        Draw.BlendMode = ShapesBlendMode.Transparent;
        Draw.ThicknessSpace = ThicknessSpace.Pixels;
        Draw.RadiusSpace = ThicknessSpace.Pixels;
        Draw.LineGeometry = LineGeometry.Billboard;
        Draw.LineEndCaps = LineEndCap.Round;
    }

    public static Vector3 ScreenToWorld(Camera cam, Vector2 screenPoint, float depth)
    {
        return cam.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, depth));
    }

    /// <summary>World size that covers the given number of screen pixels at a world position.</summary>
    public static float PixelsToWorldSize(Camera cam, Vector3 worldPosition, float pixels)
    {
        Vector3 screenA = cam.WorldToScreenPoint(worldPosition);
        Vector3 screenB = screenA;
        screenB.y += pixels;

        return Vector3.Distance(cam.ScreenToWorldPoint(screenA), cam.ScreenToWorldPoint(screenB));
    }

    public static Vector2 Rotate(Vector2 value, float radians)
    {
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(value.x * cos - value.y * sin, value.x * sin + value.y * cos);
    }

    /// <summary>
    /// Rounded rectangle facing the camera, sized in screen pixels. Restores Draw.Color and resets Draw.Matrix.
    /// Known pitfall: two or more rectangles drawn back to back in one Draw.Command are merged into one
    /// instanced Shapes batch, which does not render in this project. Put other draws between them or give
    /// each its own command (see MatchViewportOverlayRenderer dividers).
    /// </summary>
    public static void DrawRoundedScreenRectangle(
        Camera cam,
        float depth,
        Vector2 centerScreen,
        Vector2 sizePixels,
        float cornerRadiusPixels,
        Color color)
    {
        float widthPixels = Mathf.Max(0.001f, sizePixels.x);
        float heightPixels = Mathf.Max(0.001f, sizePixels.y);
        float safeCornerPixels = Mathf.Clamp(cornerRadiusPixels, 0f, Mathf.Min(widthPixels, heightPixels) * 0.5f);

        Vector3 centerWorld = ScreenToWorld(cam, centerScreen, depth);
        float widthWorld = PixelsToWorldSize(cam, centerWorld, widthPixels);
        float heightWorld = PixelsToWorldSize(cam, centerWorld, heightPixels);
        float cornerRadiusWorld = PixelsToWorldSize(cam, centerWorld, safeCornerPixels);

        Color previousColor = Draw.Color;

        Draw.Matrix = Matrix4x4.TRS(centerWorld, cam.transform.rotation, Vector3.one);
        Draw.Color = color;
        Draw.Rectangle(Vector3.zero, widthWorld, heightWorld, cornerRadiusWorld);

        Draw.Color = previousColor;
        Draw.Matrix = Matrix4x4.identity;
    }

    /// <summary>Centred text facing the camera, font size in screen pixels. Restores Draw.Color.</summary>
    public static void DrawCenteredScreenText(
        Camera cam,
        float depth,
        Vector2 centerScreen,
        string text,
        float fontSizePixels,
        Color color)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        Vector3 textWorld = ScreenToWorld(cam, centerScreen, depth);
        Draw.FontSize = PixelsToWorldSize(cam, textWorld, fontSizePixels);

        Color previousColor = Draw.Color;
        Draw.Color = color;
        Draw.Text(textWorld, cam.transform.rotation, text, TextAlign.Center);
        Draw.Color = previousColor;
    }
}
