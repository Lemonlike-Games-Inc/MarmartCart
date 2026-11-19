using Shapes;
using UnityEngine;

/// <summary>
/// Shared helpers for Shapes HUD drawers. See Docs/Design/ui-architecture.md.
///
/// The Shapes HUD is drawn on a plane just in front of each player camera.
/// Sizes and offsets are authored in 1920x1080 reference pixels and multiplied
/// by Scale(): the UI scale from PlayerViewService times the drawer's 2P/4P factor.
/// </summary>
public static class HudShapes
{
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
}
