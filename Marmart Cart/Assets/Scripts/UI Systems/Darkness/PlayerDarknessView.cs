using Shapes;
using UnityEngine;

/// <summary>
/// Draws PlayerViewDarkness in each player camera: a dark layer with a soft lit
/// circle around that player's own leading cart. The lit radius is in world
/// metres, turned into view pixels each frame.
///
/// It draws in HudShapes.DarknessPass, after the normal Shapes HUD and before HUD
/// set to On Top (DarknessHudRules), so it covers the world and the covered HUD.
/// Canvas UI draws after all cameras and is never covered; canvas parts fade
/// themselves through PlayerViewDarkness.CanvasAlpha. Presentation only.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerDarknessView : ImmediateModeShapeDrawer
{
    private const int MaxViews = PlayerViewService.MaxViews;

    [Header("References")]
    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private PlayerViewDarkness darkness;

    [Tooltip("Auto-found if left empty. Gives each player's leading cart position.")]
    [SerializeField] private PlayerWorldHUDSystem hudSystem;

    [Header("Look")]
    [SerializeField] private Color darknessColor = new Color(0.01f, 0.01f, 0.03f, 0.97f);

    [Tooltip("Soft edge of the lit circle, as a share of its radius.")]
    [Range(0f, 1f)]
    [SerializeField] private float edgeSoftness = 0.35f;

    [Header("Render Plane")]
    [Tooltip("Distance in front of the camera where the darkness is drawn. In front of the world, like the HUD.")]
    [Min(0.01f)]
    [SerializeField] private float renderDistance = 0.5f;

    [Min(0f)]
    [SerializeField] private float nearClipPadding = 0.03f;

    // Last lit circle per view (screen pixels), kept while the cart or its anchor is missing.
    private readonly Vector2[] lastCentres = new Vector2[MaxViews];
    private readonly float[] lastRadii = new float[MaxViews];
    private readonly bool[] hasLast = new bool[MaxViews];

    private void Awake()
    {
        if (darkness == null) darkness = PlayerViewDarkness.Instance != null ? PlayerViewDarkness.Instance : FindFirstObjectByType<PlayerViewDarkness>();
        if (hudSystem == null) hudSystem = FindFirstObjectByType<PlayerWorldHUDSystem>();
    }

    public override void DrawShapes(Camera cam)
    {
        if (cam == null || darkness == null) return;
        if (!HudShapes.TryGetViewPlayer(cam, out int playerIndex)) return;
        if (playerIndex < 1 || playerIndex > MaxViews) return;
        if (!darkness.TryGet(playerIndex, out float strength, out float litRadius)) return;

        Rect pixels = cam.pixelRect;
        if (pixels.width <= 0f || pixels.height <= 0f) return;

        int slot = playerIndex - 1;
        UpdateLitCircle(cam, slot, playerIndex, litRadius);

        // Before the first cart position is known the whole view is dark.
        Vector2 centre = hasLast[slot] ? lastCentres[slot] : pixels.center;
        float radius = hasLast[slot] ? lastRadii[slot] : 0f;
        float outer = FarthestCornerDistance(pixels, centre) + 2f;

        float depth = HudShapes.RenderDepth(cam, renderDistance, nearClipPadding);
        Vector3 centreWorld = HudShapes.ScreenToWorld(cam, centre, depth);
        Vector3 normal = cam.transform.forward;

        Color dark = darknessColor;
        dark.a *= strength;
        Color clear = dark;
        clear.a = 0f;

        float soft = radius * edgeSoftness;

        // Separate commands: two rings in one command can be merged into one instanced batch.
        if (soft > 0.5f)
        {
            using (Draw.Command(cam, HudShapes.DarknessPass))
            {
                HudShapes.ApplyHudDrawState();
                Draw.Ring(centreWorld, normal, radius - soft * 0.5f, soft, DiscColors.Radial(clear, dark));
            }
        }

        // Overlaps the soft edge by a pixel so no seam shows.
        float solidInner = Mathf.Max(0f, radius - 1f);
        if (outer <= solidInner) return;

        using (Draw.Command(cam, HudShapes.DarknessPass))
        {
            HudShapes.ApplyHudDrawState();
            Draw.Ring(centreWorld, normal, (solidInner + outer) * 0.5f, outer - solidInner, dark);
        }
    }

    private void UpdateLitCircle(Camera cam, int slot, int playerIndex, float litRadius)
    {
        if (hudSystem == null || !hudSystem.TryGetOverlayAnchor(playerIndex, out Transform anchor)) return;

        Vector3 centre = cam.WorldToScreenPoint(anchor.position);
        if (centre.z <= 0f) return;

        Vector3 edge = cam.WorldToScreenPoint(anchor.position + cam.transform.right * litRadius);

        lastCentres[slot] = centre;
        lastRadii[slot] = Vector2.Distance(centre, edge);
        hasLast[slot] = true;
    }

    private static float FarthestCornerDistance(Rect pixels, Vector2 point)
    {
        float dx = Mathf.Max(Mathf.Abs(point.x - pixels.xMin), Mathf.Abs(point.x - pixels.xMax));
        float dy = Mathf.Max(Mathf.Abs(point.y - pixels.yMin), Mathf.Abs(point.y - pixels.yMax));
        return Mathf.Sqrt(dx * dx + dy * dy);
    }
}
