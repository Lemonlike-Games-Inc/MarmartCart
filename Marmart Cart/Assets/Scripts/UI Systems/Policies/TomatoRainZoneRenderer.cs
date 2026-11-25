using Shapes;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Draws the Tomato Rain circle on the floor in every player view: a red outline
/// with a fill that grows from the centre while it fills, then pulses while it
/// rains, then fades out. World space, so it sits under the Blackout darkness
/// like the rest of the world. Presentation only; reads TomatoRainPolicyEffect.
/// </summary>
[DisallowMultipleComponent]
public sealed class TomatoRainZoneRenderer : ImmediateModeShapeDrawer
{
    // Before transparent effects: some Tomato splash particles write depth, and
    // drawn after them the circle would be cut by their square quads. This way
    // the splashes blend over the circle instead.
    private const RenderPassEvent ZonePass = RenderPassEvent.BeforeRenderingTransparents;

    [Tooltip("Auto-found on this object if left empty.")]
    [SerializeField] private TomatoRainPolicyEffect rain;

    [Header("Look")]
    [SerializeField] private Color outlineColor = new Color(0.95f, 0.12f, 0.08f, 0.95f);
    [SerializeField] private Color fillColor = new Color(0.95f, 0.12f, 0.08f, 0.35f);

    [Tooltip("Outline thickness, in metres.")]
    [Min(0.01f)]
    [SerializeField] private float outlineThickness = 0.18f;

    [Tooltip("Lift above the floor so the circle does not flicker into it, in metres.")]
    [Min(0f)]
    [SerializeField] private float heightOffset = 0.04f;

    [Tooltip("Fill pulses per second while it rains.")]
    [Min(0f)]
    [SerializeField] private float rainPulsesPerSecond = 4f;

    [Range(0f, 1f)]
    [SerializeField] private float rainPulseDepth = 0.35f;

    [Min(0f)]
    [SerializeField] private float fadeOutSeconds = 0.4f;

    private Vector3 lastCentre;
    private Vector3 lastNormal = Vector3.up;
    private float lastRadius;
    private float zoneEndTime = float.NegativeInfinity;
    private bool hadZone;

    private void Awake()
    {
        if (rain == null) rain = GetComponent<TomatoRainPolicyEffect>();
    }

    public override void DrawShapes(Camera cam)
    {
        if (cam == null || rain == null) return;
        if (!HudShapes.TryGetViewPlayer(cam, out _)) return;

        float alpha = 1f;
        float fill01;
        bool raining = false;

        if (rain.HasZone)
        {
            hadZone = true;
            lastCentre = rain.ZoneCentre;
            lastNormal = rain.ZoneNormal;
            lastRadius = rain.ZoneRadius;
            fill01 = rain.Fill01;
            raining = rain.CurrentPhase == TomatoRainPolicyEffect.Phase.Raining;
        }
        else
        {
            if (hadZone)
            {
                hadZone = false;
                zoneEndTime = Time.time;
            }

            alpha = fadeOutSeconds > 0f ? 1f - (Time.time - zoneEndTime) / fadeOutSeconds : 0f;
            if (alpha <= 0f) return;
            fill01 = 1f;
        }

        Vector3 position = lastCentre + lastNormal * heightOffset;

        Color fill = fillColor;
        fill.a *= alpha;

        if (raining && rainPulsesPerSecond > 0f)
            fill.a *= 1f - rainPulseDepth * (0.5f + 0.5f * Mathf.Sin(Time.time * rainPulsesPerSecond * Mathf.PI * 2f));

        Color outline = outlineColor;
        outline.a *= alpha;

        // Separate commands: two disc-type shapes in one command can be merged into one instanced batch.
        float fillRadius = lastRadius * fill01;
        if (fillRadius > 0.01f)
        {
            using (Draw.Command(cam, ZonePass))
            {
                ApplyWorldDrawState();
                Draw.Disc(position, lastNormal, fillRadius, fill);
            }
        }

        using (Draw.Command(cam, ZonePass))
        {
            ApplyWorldDrawState();
            Draw.Ring(position, lastNormal, Mathf.Max(0f, lastRadius - outlineThickness * 0.5f), outlineThickness, outline);
        }
    }

    private static void ApplyWorldDrawState()
    {
        Draw.ResetAllDrawStates();
        Draw.BlendMode = ShapesBlendMode.Transparent;
        Draw.ThicknessSpace = ThicknessSpace.Meters;
        Draw.RadiusSpace = ThicknessSpace.Meters;
    }
}
