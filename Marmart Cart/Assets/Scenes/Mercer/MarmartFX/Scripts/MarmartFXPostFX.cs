using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Volume override for the stylized post effect: screen-space ink outlines
/// (from depth + normals), optional color posterize and a shadow hue push.
/// Use together with URP's built-in Bloom / Tonemapping / Color Adjustments /
/// Split Toning / Vignette for the full look.
/// </summary>
[Serializable, VolumeComponentMenu("MarmartFX/Ink Outline & Paint")]
public sealed class MarmartFXPostFX : VolumeComponent, IPostProcessComponent
{
    [Tooltip("Overall strength of the effect. 0 disables it.")]
    public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);

    [Header("Ink Outline")]
    public ColorParameter outlineColor = new ColorParameter(new Color(0.07f, 0.03f, 0.06f, 1f));
    public ClampedFloatParameter outlineThickness = new ClampedFloatParameter(1.2f, 0.5f, 4f);
    [Tooltip("Relative depth difference that counts as an edge.")]
    public ClampedFloatParameter depthThreshold = new ClampedFloatParameter(0.06f, 0.001f, 1f);
    [Tooltip("Normal difference that counts as an edge.")]
    public ClampedFloatParameter normalThreshold = new ClampedFloatParameter(0.5f, 0.05f, 2f);
    [Tooltip("Outlines fade out beyond this distance (world units).")]
    public MinFloatParameter fadeDistance = new MinFloatParameter(80f, 1f);

    [Header("Paint")]
    [Tooltip("Color quantize steps per channel. 0 = off.")]
    public ClampedIntParameter posterizeSteps = new ClampedIntParameter(0, 0, 16);
    [Tooltip("Hue that dark areas are pushed toward.")]
    public ColorParameter shadowHue = new ColorParameter(new Color(0.32f, 0.18f, 0.48f, 1f));
    public ClampedFloatParameter shadowHueStrength = new ClampedFloatParameter(0.35f, 0f, 1f);

    [Header("Style Filters")]
    [Tooltip("Pixelate block size in pixels. 0/1 = off.")]
    public ClampedIntParameter pixelSize = new ClampedIntParameter(0, 0, 16);
    [Tooltip("Oil-paint (Kuwahara) brush radius. 0 = off.")]
    public ClampedIntParameter kuwaharaRadius = new ClampedIntParameter(0, 0, 6);
    public ClampedFloatParameter watercolorBleed = new ClampedFloatParameter(0f, 0f, 1f);
    [Tooltip("Hand-drawn line wobble in pixels.")]
    public ClampedFloatParameter lineWobble = new ClampedFloatParameter(0f, 0f, 6f);
    [Tooltip("Wobble redraws per second (0 = static).")]
    public ClampedFloatParameter wobbleFps = new ClampedFloatParameter(8f, 0f, 24f);
    public ClampedFloatParameter wobbleScale = new ClampedFloatParameter(40f, 2f, 200f);
    public ClampedFloatParameter paperStrength = new ClampedFloatParameter(0f, 0f, 1f);
    public ClampedFloatParameter paperScale = new ClampedFloatParameter(6f, 1f, 40f);
    public ClampedFloatParameter filmGrain = new ClampedFloatParameter(0f, 0f, 0.3f);
    public ClampedFloatParameter scanlines = new ClampedFloatParameter(0f, 0f, 1f);
    [Tooltip("Print misregistration / chromatic offset in pixels.")]
    public ClampedFloatParameter misregistration = new ClampedFloatParameter(0f, 0f, 8f);
    [Tooltip("Ordered dither used with posterize.")]
    public ClampedFloatParameter ditherStrength = new ClampedFloatParameter(0f, 0f, 1f);

    public bool IsActive() => active && intensity.value > 0f;

    public bool IsTileCompatible() => false;
}
