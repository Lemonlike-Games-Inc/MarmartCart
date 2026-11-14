using System;
using UnityEngine;

namespace LightSide
{
    /// <summary>Fades a glyph from an authored opacity.</summary>
    [Serializable, TypeGroup("Essentials", 1)]
    [TypeDescription("Fades each glyph into its authored colour.")]
    public sealed partial class FadeRevealHandler : EasedRevealHandler
    {
        /// <summary>Opacity at the reveal frontier.</summary>
        [Tooltip("Opacity a glyph starts at as it appears, 0 transparent to 1 opaque.")]
        [SerializeField, Range(0f, 1f), StateProperty]
        private float startOpacity;

        /// <inheritdoc/>
        public override void Apply(in RevealGlyphInfo info)
            => ApplyFade(in info, Mathf.LerpUnclamped(startOpacity, 1f, Progress(in info)));
    }

    /// <summary>Grows or shrinks a glyph from an authored two-axis scale.</summary>
    [Serializable, TypeGroup("Essentials", 2)]
    [TypeDescription("Scales glyphs in from a configurable size and pivot.")]
    public sealed partial class ScaleRevealHandler : GeometricRevealHandler
    {
        /// <summary>Scale at the reveal frontier.</summary>
        [Tooltip("Horizontal and vertical scale a glyph starts at as it appears; 1,1 is its final size.")]
        [SerializeField, StateProperty]
        private Vector2 startScale = Vector2.zero;

        /// <summary>Whether opacity follows reveal progress.</summary>
        [Tooltip("Whether the glyph also fades in as it scales into place.")]
        [SerializeField, StateProperty]
        private bool fade = true;

        /// <inheritdoc/>
        public override void Apply(in RevealGlyphInfo info)
        {
            var t = Progress(in info);
            var scale = Vector2.LerpUnclamped(startScale, Vector2.one, t);
            ApplyScale(in info, scale.x, scale.y);
            if (fade) ApplyFade(in info, t);
        }
    }

    /// <summary>Moves a glyph into place from an authored offset.</summary>
    [Serializable, TypeGroup("Essentials", 3)]
    [TypeDescription("Slides glyphs into place from any direction.")]
    public sealed partial class SlideRevealHandler : EasedRevealHandler
    {
        /// <summary>Position offset at the reveal frontier, in pixels.</summary>
        [Tooltip("Position offset a glyph starts at, in pixels; it slides from there into place.")]
        [SerializeField, StateProperty]
        private Vector2 offset = new(0f, -12f);

        /// <summary>Whether opacity follows reveal progress.</summary>
        [Tooltip("Whether the glyph also fades in as it slides into place.")]
        [SerializeField, StateProperty]
        private bool fade = true;

        /// <inheritdoc/>
        public override void Apply(in RevealGlyphInfo info)
        {
            var t = Progress(in info);
            ApplyOffset(in info, offset.x * (1f - t), offset.y * (1f - t));
            if (fade) ApplyFade(in info, t);
        }
    }

    /// <summary>Rotates and scales a glyph into place around an authored pivot.</summary>
    [Serializable, TypeGroup("Essentials", 4)]
    [TypeDescription("Spins glyphs into place with optional scale and fade.")]
    public sealed partial class SpinRevealHandler : GeometricRevealHandler
    {
        /// <summary>Rotation at the reveal frontier, in degrees.</summary>
        [Tooltip("Rotation a glyph starts at, in degrees; it turns back upright as it appears.")]
        [SerializeField, StateProperty]
        private float angle = 180f;

        /// <summary>Uniform scale at the reveal frontier.</summary>
        [Tooltip("Uniform scale a glyph starts at; 1 is its final size.")]
        [SerializeField, StateProperty]
        private float startScale = 0.35f;

        /// <summary>Whether opacity follows reveal progress.</summary>
        [Tooltip("Whether the glyph also fades in as it spins into place.")]
        [SerializeField, StateProperty]
        private bool fade = true;

        /// <inheritdoc/>
        public override void Apply(in RevealGlyphInfo info)
        {
            var t = Progress(in info);
            var scale = Mathf.LerpUnclamped(startScale, 1f, t);
            ApplyScale(in info, scale, scale);
            ApplyRotation(in info, angle * (1f - t));
            if (fade) ApplyFade(in info, t);
        }
    }

    /// <summary>Transitions a glyph from a colour flash into its authored colour.</summary>
    [Serializable, TypeGroup("Essentials", 5)]
    [TypeDescription("Introduces glyphs through a coloured flash.")]
    public sealed partial class TintRevealHandler : EasedRevealHandler
    {
        /// <summary>Multiplicative colour at the reveal frontier.</summary>
        [Tooltip("Colour the glyph starts tinted with; it blends back to the authored colour as the glyph appears.")]
        [SerializeField, StateProperty]
        private Color32 startTint = new(64, 224, 255, 255);

        /// <summary>Opacity at the reveal frontier.</summary>
        [Tooltip("Opacity a glyph starts at as it appears, 0 transparent to 1 opaque.")]
        [SerializeField, Range(0f, 1f), StateProperty]
        private float startOpacity = 0.15f;

        public TintRevealHandler() => Easing = Ease.Of(EasingType.Linear);

        /// <inheritdoc/>
        public override void Apply(in RevealGlyphInfo info)
        {
            var t = Progress(in info);
            ApplyTint(in info, startTint, 1f - t);
            ApplyFade(in info, Mathf.LerpUnclamped(startOpacity, 1f, t));
        }
    }

    /// <summary>Springs a glyph from zero scale past its final size and settles.</summary>
    [Serializable, TypeGroup("Physical", 1)]
    [TypeDescription("Pops glyphs in with a configurable overshoot.")]
    public sealed partial class PopRevealHandler : RevealHandler
    {
        /// <summary>Back-easing overshoot; 0 removes the overshoot.</summary>
        [Tooltip("How far the glyph springs past its final size before settling; 0 removes the overshoot.")]
        [SerializeField, Min(0f), StateProperty]
        private float overshoot = 1.7f;

        /// <summary>Fixed point of the scale transform, normalized over the glyph quad.</summary>
        [Tooltip("Point the glyph scales around, as a fraction of the glyph box: 0,0 bottom-left, 1,1 top-right.")]
        [SerializeField, VectorDragField, StateProperty]
        private Vector2 pivot = new(0.5f, 0.5f);

        /// <inheritdoc/>
        public override void Apply(in RevealGlyphInfo info)
        {
            var t = info.Progress;
            var x = t - 1f;
            var scale = 1f + (overshoot + 1f) * x * x * x + overshoot * x * x;
            ApplyScale(in info, scale, scale, pivot);
            ApplyFade(in info, t);
        }
    }

    /// <summary>Drops a glyph onto its baseline with bounce, tilt and landing squash.</summary>
    [Serializable, TypeGroup("Physical", 2)]
    [TypeDescription("Drops glyphs from above and bounces them onto the baseline.")]
    public sealed partial class DropRevealHandler : RevealHandler
    {
        /// <summary>Initial height above the final position, in pixels.</summary>
        [Tooltip("Height a glyph drops from, above its final position, in pixels.")]
        [SerializeField, StateProperty]
        private float height = 28f;

        /// <summary>Initial tilt in degrees.</summary>
        [Tooltip("Tilt a glyph starts with, in degrees, straightening as it falls.")]
        [SerializeField, StateProperty]
        private float angle = 12f;

        /// <summary>Peak landing squash.</summary>
        [Tooltip("How much the glyph flattens on landing, as a fraction of its size; 0 removes the squash.")]
        [SerializeField, Range(0f, 0.8f), StateProperty]
        private float squash = 0.18f;

        /// <inheritdoc/>
        public override void Apply(in RevealGlyphInfo info)
        {
            var t = info.Progress;
            var bounce = EasingType.BounceOut.Evaluate(t);
            ApplyOffset(in info, 0f, height * (1f - bounce));
            ApplyRotation(in info, angle * (1f - t), new Vector2(0.5f, 0f));
            var impactT = Mathf.Clamp01((t - 0.65f) / 0.35f);
            var impact = Mathf.Sin(impactT * Mathf.PI) * squash;
            ApplyScale(in info, 1f + impact, 1f - impact, new Vector2(0.5f, 0f));
            ApplyFade(in info, t);
        }
    }

    /// <summary>Raises a glyph from a bottom corner like a falling domino in reverse.</summary>
    [Serializable, TypeGroup("Physical", 3)]
    [TypeDescription("Swings glyphs upright from either bottom corner.")]
    public sealed partial class DominoRevealHandler : EasedRevealHandler
    {
        /// <summary>Starting rotation in degrees.</summary>
        [Tooltip("Angle a glyph starts leaning at, in degrees; it swings upright from its bottom corner.")]
        [SerializeField, StateProperty]
        private float angle = 82f;

        /// <summary>Whether the glyph hinges on its bottom-right corner.</summary>
        [Tooltip("Whether the glyph hinges on its bottom-right corner instead of the bottom-left.")]
        [SerializeField, StateProperty]
        private bool fromRight;

        /// <inheritdoc/>
        public override void Apply(in RevealGlyphInfo info)
        {
            var t = Progress(in info);
            var pivot = fromRight ? new Vector2(1f, 0f) : new Vector2(0f, 0f);
            ApplyRotation(in info, angle * (fromRight ? -1f : 1f) * (1f - t), pivot);
            ApplyFade(in info, t);
        }
    }

    /// <summary>Axis collapsed by a flip reveal.</summary>
    public enum RevealFlipAxis : byte
    {
        Horizontal,
        Vertical,
    }

    /// <summary>Unfolds a glyph around its horizontal or vertical axis.</summary>
    [Serializable, TypeGroup("Physical", 4)]
    [TypeDescription("Flips glyphs open horizontally or vertically.")]
    public sealed partial class FlipRevealHandler : GeometricRevealHandler
    {
        /// <summary>Axis that starts edge-on.</summary>
        [Tooltip("Axis the glyph starts collapsed along: Horizontal opens it from zero width, Vertical from zero height.")]
        [SerializeField, StateProperty]
        private RevealFlipAxis axis = RevealFlipAxis.Horizontal;

        /// <inheritdoc/>
        public override void Apply(in RevealGlyphInfo info)
        {
            var t = Mathf.Clamp01(Progress(in info));
            var edge = Mathf.Sin(t * Mathf.PI * 0.5f);
            ApplyScale(in info,
                axis == RevealFlipAxis.Horizontal ? edge : 1f,
                axis == RevealFlipAxis.Vertical ? edge : 1f);
            ApplyFade(in info, t);
        }
    }

    /// <summary>Settles a glyph from a tall, narrow or wide, flat silhouette.</summary>
    [Serializable, TypeGroup("Physical", 5)]
    [TypeDescription("Stretches glyphs into their final proportions.")]
    public sealed partial class StretchRevealHandler : GeometricRevealHandler
    {
        /// <summary>Horizontal scale at the reveal frontier.</summary>
        [Tooltip("Horizontal scale a glyph starts at; 1 is its final width.")]
        [SerializeField, StateProperty]
        private float startWidth = 0.35f;

        /// <summary>Vertical scale at the reveal frontier.</summary>
        [Tooltip("Vertical scale a glyph starts at; 1 is its final height.")]
        [SerializeField, StateProperty]
        private float startHeight = 1.65f;

        public StretchRevealHandler() => Pivot = new Vector2(0.5f, 0f);

        /// <inheritdoc/>
        public override void Apply(in RevealGlyphInfo info)
        {
            var t = Progress(in info);
            ApplyScale(in info,
                Mathf.LerpUnclamped(startWidth, 1f, t),
                Mathf.LerpUnclamped(startHeight, 1f, t));
            ApplyFade(in info, t);
        }
    }

    /// <summary>Lets a glyph swing from its top edge and damp into place.</summary>
    [Serializable, TypeGroup("Physical", 6)]
    [TypeDescription("Swings glyphs from their top edge with damped motion.")]
    public sealed partial class SwingRevealHandler : RevealHandler
    {
        /// <summary>Initial swing angle in degrees.</summary>
        [Tooltip("Angle the glyph starts swung out to, in degrees; each swing is smaller until it hangs straight.")]
        [SerializeField, StateProperty]
        private float angle = 55f;

        /// <summary>Complete swings before settling.</summary>
        [Tooltip("Complete back-and-forth swings before the glyph settles.")]
        [SerializeField, Min(0f), StateProperty]
        private float oscillations = 1.25f;

        /// <inheritdoc/>
        public override void Apply(in RevealGlyphInfo info)
        {
            var t = info.Progress;
            var rotation = Mathf.Cos(t * oscillations * 2f * Mathf.PI) * angle * (1f - t);
            ApplyRotation(in info, rotation, new Vector2(0.5f, 1f));
            ApplyFade(in info, t);
        }
    }

    /// <summary>Pulls a glyph inward along a shrinking spiral.</summary>
    [Serializable, TypeGroup("Expressive", 1)]
    [TypeDescription("Spirals glyphs into place while scaling and rotating them.")]
    public sealed partial class SpiralRevealHandler : GeometricRevealHandler
    {
        /// <summary>Initial orbit radius in pixels.</summary>
        [Tooltip("Radius of the glyph's orbit at the start, in pixels; it tightens to zero as the glyph settles.")]
        [SerializeField, StateProperty]
        private float radius = 24f;

        /// <summary>Orbit turns before settling.</summary>
        [Tooltip("Full orbit turns the glyph travels before settling.")]
        [SerializeField, StateProperty]
        private float turns = 0.8f;

        /// <summary>Angular offset between adjacent clusters, in radians.</summary>
        [Tooltip("Angular offset between neighbouring glyphs on the spiral, in radians.")]
        [SerializeField, StateProperty]
        private float spread = 0.65f;

        /// <summary>Uniform scale at the reveal frontier.</summary>
        [Tooltip("Uniform scale a glyph starts at; 1 is its final size.")]
        [SerializeField, StateProperty]
        private float startScale = 0.2f;

        /// <inheritdoc/>
        public override void Apply(in RevealGlyphInfo info)
        {
            var t = Progress(in info);
            var envelope = 1f - t;
            var travel = turns * 2f * Mathf.PI * envelope;
            var direction = travel + info.ordinal * spread;
            ApplyOffset(in info, Mathf.Cos(direction) * radius * envelope,
                Mathf.Sin(direction) * radius * envelope);
            ApplyRotation(in info, travel * Mathf.Rad2Deg);
            var scale = Mathf.LerpUnclamped(startScale, 1f, t);
            ApplyScale(in info, scale, scale);
            ApplyFade(in info, t);
        }
    }

    /// <summary>Launches glyphs from deterministic radial directions into their final cells.</summary>
    [Serializable, TypeGroup("Expressive", 2)]
    [TypeDescription("Bursts glyphs inward from different radial directions.")]
    public sealed partial class BurstRevealHandler : GeometricRevealHandler
    {
        /// <summary>Initial radial distance in pixels.</summary>
        [Tooltip("Distance each glyph starts from its final position, in pixels; the direction differs per glyph.")]
        [SerializeField, StateProperty]
        private float distance = 22f;

        /// <summary>Peak deterministic starting rotation in degrees.</summary>
        [Tooltip("Largest rotation a glyph may start with, in degrees; each glyph gets its own within this.")]
        [SerializeField, StateProperty]
        private float angle = 90f;

        /// <summary>Uniform scale at the reveal frontier.</summary>
        [Tooltip("Uniform scale a glyph starts at; 1 is its final size.")]
        [SerializeField, StateProperty]
        private float startScale = 0.15f;

        /// <inheritdoc/>
        public override void Apply(in RevealGlyphInfo info)
        {
            var t = Progress(in info);
            var envelope = 1f - t;
            var direction = HashNoise.Hash01(info.ordinal, 17) * 2f * Mathf.PI;
            ApplyOffset(in info, Mathf.Cos(direction) * distance * envelope,
                Mathf.Sin(direction) * distance * envelope);
            ApplyRotation(in info, HashNoise.HashSigned(info.ordinal, 19) * angle * envelope);
            var scale = Mathf.LerpUnclamped(startScale, 1f, t);
            ApplyScale(in info, scale, scale);
            ApplyFade(in info, t);
        }
    }

    /// <summary>Shakes a glyph with deterministic, progressively damped jitter.</summary>
    [Serializable, TypeGroup("Expressive", 3)]
    [TypeDescription("Shakes glyphs into place with deterministic jitter.")]
    public sealed partial class ShakeRevealHandler : RevealHandler
    {
        /// <summary>Peak positional jitter in pixels.</summary>
        [Tooltip("Peak positional jitter in pixels, fading out as the glyph settles.")]
        [SerializeField, StateProperty]
        private float amplitude = 7f;

        /// <summary>Peak angular jitter in degrees.</summary>
        [Tooltip("Peak angular jitter in degrees, fading out as the glyph settles.")]
        [SerializeField, StateProperty]
        private float angle = 16f;

        /// <summary>Noise changes across one appearance.</summary>
        [Tooltip("How many times the jitter re-rolls while one glyph appears.")]
        [SerializeField, Min(1f), StateProperty]
        private float frequency = 8f;

        /// <inheritdoc/>
        public override void Apply(in RevealGlyphInfo info)
        {
            var t = info.Progress;
            var envelope = 1f - t;
            var step = Mathf.FloorToInt(t * frequency);
            ApplyOffset(in info,
                HashNoise.HashSigned(info.ordinal, step, 1) * amplitude * envelope,
                HashNoise.HashSigned(info.ordinal, step, 2) * amplitude * envelope);
            ApplyRotation(in info, HashNoise.HashSigned(info.ordinal, step, 3) * angle * envelope,
                new Vector2(0.5f, 0.5f));
            ApplyFade(in info, t);
        }
    }

    /// <summary>Corrupts a glyph with jitter, shear, chromatic tint and opacity flicker.</summary>
    [Serializable, TypeGroup("Expressive", 4)]
    [TypeDescription("Glitches glyphs through chromatic jitter and shear.")]
    public sealed partial class GlitchRevealHandler : RevealHandler
    {
        private static readonly Color32 cyan = new(0, 255, 255, 255);
        private static readonly Color32 magenta = new(255, 0, 255, 255);

        /// <summary>Peak positional corruption in pixels.</summary>
        [Tooltip("Peak positional jitter in pixels, mostly horizontal, fading out as the glyph settles.")]
        [SerializeField, StateProperty]
        private float amplitude = 7f;

        /// <summary>Peak horizontal shear.</summary>
        [Tooltip("Peak horizontal slant; 1 leans the glyph 45 degrees, fading out as it settles.")]
        [SerializeField, StateProperty]
        private float shear = 0.3f;

        /// <summary>Opacity instability during appearance.</summary>
        [Tooltip("How strongly the glyph's opacity flickers while it appears, 0 none to 1 full.")]
        [SerializeField, Range(0f, 1f), StateProperty]
        private float flicker = 0.7f;

        /// <summary>Corruption changes across one appearance.</summary>
        [Tooltip("How many times the corruption re-rolls while one glyph appears.")]
        [SerializeField, Min(1f), StateProperty]
        private float frequency = 12f;

        /// <inheritdoc/>
        public override void Apply(in RevealGlyphInfo info)
        {
            var t = info.Progress;
            var envelope = 1f - t;
            var step = Mathf.FloorToInt(t * frequency);
            ApplyOffset(in info,
                HashNoise.HashSigned(info.ordinal, step, 1) * amplitude * envelope,
                HashNoise.HashSigned(info.ordinal, step, 2) * amplitude * 0.25f * envelope);
            ApplySkewX(in info, HashNoise.HashSigned(info.ordinal, step, 3) * shear * envelope,
                new Vector2(0.5f, 0.5f));
            ApplyTint(in info, HashNoise.Hash01(info.ordinal, step, 4) < 0.5f ? cyan : magenta,
                envelope * 0.8f);
            var opacity = t + HashNoise.HashSigned(info.ordinal, step, 5) * flicker * envelope;
            ApplyFade(in info, opacity);
        }
    }

    /// <summary>Drops glyphs from staggered heights with deterministic wind and tilt.</summary>
    [Serializable, TypeGroup("Expressive", 5)]
    [TypeDescription("Rains glyphs down with per-glyph wind and tilt.")]
    public sealed partial class RainRevealHandler : GeometricRevealHandler
    {
        /// <summary>Base falling distance in pixels.</summary>
        [Tooltip("Base height glyphs fall from, in pixels; each glyph varies it between 70 and 130 percent.")]
        [SerializeField, StateProperty]
        private float height = 38f;

        /// <summary>Peak horizontal wind drift in pixels.</summary>
        [Tooltip("Peak sideways drift while a glyph falls, in pixels.")]
        [SerializeField, StateProperty]
        private float wind = 9f;

        /// <summary>Peak starting tilt in degrees.</summary>
        [Tooltip("Largest tilt a glyph may start with, in degrees; each glyph gets its own within this.")]
        [SerializeField, StateProperty]
        private float angle = 18f;

        /// <inheritdoc/>
        public override void Apply(in RevealGlyphInfo info)
        {
            var t = Progress(in info);
            var envelope = 1f - t;
            var variation = Mathf.Lerp(0.7f, 1.3f, HashNoise.Hash01(info.ordinal, 23));
            var phase = HashNoise.Hash01(info.ordinal, 29) * 2f * Mathf.PI;
            var x = Mathf.Sin(t * 2f * Mathf.PI + phase) * wind * envelope;
            ApplyOffset(in info, x, height * variation * envelope);
            ApplyRotation(in info, HashNoise.HashSigned(info.ordinal, 31) * angle * envelope);
            ApplyFade(in info, t);
        }
    }

    /// <summary>Floats a glyph through damped vertical ripples before it settles.</summary>
    [Serializable, TypeGroup("Expressive", 6)]
    [TypeDescription("Ripples glyphs into place along a damped wave.")]
    public sealed partial class WaveRevealHandler : RevealHandler
    {
        /// <summary>Peak vertical displacement in pixels.</summary>
        [Tooltip("Peak vertical displacement in pixels, damping out as the glyph settles.")]
        [SerializeField, StateProperty]
        private float amplitude = 12f;

        /// <summary>Wave cycles across one appearance.</summary>
        [Tooltip("Wave cycles a glyph rides while it appears.")]
        [SerializeField, Min(0f), StateProperty]
        private float cycles = 1.5f;

        /// <summary>Phase offset between adjacent clusters.</summary>
        [Tooltip("Phase offset between neighbouring glyphs, in radians, so the ripple travels along the text.")]
        [SerializeField, StateProperty]
        private float spread = 0.7f;

        /// <inheritdoc/>
        public override void Apply(in RevealGlyphInfo info)
        {
            var t = info.Progress;
            var envelope = 1f - t;
            var phase = t * cycles * 2f * Mathf.PI + info.ordinal * spread;
            ApplyOffset(in info, 0f,
                amplitude * envelope * (0.55f + 0.45f * Mathf.Sin(phase)));
            ApplyFade(in info, t);
        }
    }

    /// <summary>Straightens a glyph from a horizontal shear.</summary>
    [Serializable, TypeGroup("Expressive", 7)]
    [TypeDescription("Skews glyphs into place from a selected pivot.")]
    public sealed partial class SkewRevealHandler : GeometricRevealHandler
    {
        /// <summary>Starting shear angle in degrees.</summary>
        [Tooltip("Slant a glyph starts at, in degrees; it straightens as it appears.")]
        [SerializeField, StateProperty]
        private float angle = 34f;

        public SkewRevealHandler() => Pivot = new Vector2(0.5f, 0f);

        /// <inheritdoc/>
        public override void Apply(in RevealGlyphInfo info)
        {
            var t = Progress(in info);
            ApplySkewX(in info, Mathf.Tan(angle * (1f - t) * Mathf.Deg2Rad));
            ApplyFade(in info, t);
        }
    }

    /// <summary>Combines deterministic direction, scale, rotation and colour variation per glyph.</summary>
    [Serializable, TypeGroup("Expressive", 8)]
    [TypeDescription("Throws every glyph in through a different deterministic transformation.")]
    public sealed partial class ChaosRevealHandler : GeometricRevealHandler
    {
        /// <summary>Peak initial distance in pixels.</summary>
        [Tooltip("Largest distance a glyph may start from its final position, in pixels; each glyph gets its own within this.")]
        [SerializeField, StateProperty]
        private float distance = 26f;

        /// <summary>Peak initial rotation in degrees.</summary>
        [Tooltip("Largest rotation a glyph may start with, in degrees; each glyph gets its own within this.")]
        [SerializeField, StateProperty]
        private float angle = 220f;

        /// <summary>Strength of the random colour flash.</summary>
        [Tooltip("Strength of the random colour flash each glyph starts with, 0 none to 1 full.")]
        [SerializeField, Range(0f, 1f), StateProperty]
        private float color = 0.65f;

        /// <inheritdoc/>
        public override void Apply(in RevealGlyphInfo info)
        {
            var t = Progress(in info);
            var envelope = 1f - t;
            var direction = HashNoise.Hash01(info.ordinal, 37) * 2f * Mathf.PI;
            var radius = distance * Mathf.Lerp(0.45f, 1f, HashNoise.Hash01(info.ordinal, 41));
            ApplyOffset(in info, Mathf.Cos(direction) * radius * envelope,
                Mathf.Sin(direction) * radius * envelope);
            ApplyRotation(in info, HashNoise.HashSigned(info.ordinal, 43) * angle * envelope);
            var startScale = Mathf.Lerp(0.08f, 0.65f, HashNoise.Hash01(info.ordinal, 47));
            var scale = Mathf.LerpUnclamped(startScale, 1f, t);
            ApplyScale(in info, scale, scale);
            var tint = new Color32(
                (byte)Mathf.RoundToInt(HashNoise.Hash01(info.ordinal, 53) * 255f),
                (byte)Mathf.RoundToInt(HashNoise.Hash01(info.ordinal, 59) * 255f),
                (byte)Mathf.RoundToInt(HashNoise.Hash01(info.ordinal, 61) * 255f), 255);
            ApplyTint(in info, tint, envelope * color);
            ApplyFade(in info, t);
        }
    }
}
