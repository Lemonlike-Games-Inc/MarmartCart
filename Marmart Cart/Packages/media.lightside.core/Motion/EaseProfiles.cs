using UnityEngine;

namespace LightSide
{
    /// <summary>One named curve a picker or a gallery offers.</summary>
    public readonly struct EasePreset
    {
        /// <summary>Name shown for it.</summary>
        public readonly string Name;

        /// <summary>The curve it applies.</summary>
        public readonly Ease Curve;

        public EasePreset(string name, Ease curve)
        {
            Name = name;
            Curve = curve;
        }
    }

    /// <summary>
    /// Curves shaped to run along something rather than over time — a stroke's thickness down its path, a
    /// value across a span. Unlike the easings, they are free at both ends: a profile may start and finish
    /// at any height.
    /// </summary>
    public static class EaseProfiles
    {
        private const float Smooth = 1f / 6f;

        /// <summary>One the whole way — the profile that changes nothing.</summary>
        public static Ease Flat => Level(1f);

        /// <summary>Rises from nothing at the start to full at the end.</summary>
        public static Ease TaperIn => Ramp(0f, 1f);

        /// <summary>Full at the start, falling to nothing at the end — a stroke that runs out.</summary>
        public static Ease TaperOut => Ramp(1f, 0f);

        /// <summary>Full at both ends, drawn in at the middle.</summary>
        public static Ease Waist => Arch(1f, 0.3f, 1f);

        /// <summary>Narrow at both ends, swelling through the middle — a leaf.</summary>
        public static Ease Bulge => Arch(0.08f, 1f, 0.08f);

        /// <summary>Full until the middle, then falling away.</summary>
        public static Ease FadeOut => Arch(1f, 1f, 0f);

        /// <summary>The gallery a profile picker offers, in order.</summary>
        public static readonly EasePreset[] All =
        {
            new("Flat", Flat),
            new("Taper In", TaperIn),
            new("Taper Out", TaperOut),
            new("Waist", Waist),
            new("Bulge", Bulge),
            new("Fade Out", FadeOut),
        };

        /// <summary>A curve holding <paramref name="value"/> from end to end.</summary>
        public static Ease Level(float value) => Ease.Keyed(new[]
        {
            Key(0f, value, 0f, 0f),
            Key(1f, value, 0f, 0f),
        });

        /// <summary>A straight run from <paramref name="from"/> at the start to <paramref name="to"/> at the end.</summary>
        public static Ease Ramp(float from, float to) => Ease.Keyed(new[]
        {
            Key(0f, from, 0f, 0f),
            Key(1f, to, 0f, 0f),
        });

        /// <summary>A smooth run through three heights — start, middle, end — with a level tangent at each.</summary>
        public static Ease Arch(float start, float middle, float end) => Ease.Keyed(new[]
        {
            Key(0f, start, 0f, Smooth),
            Key(0.5f, middle, Smooth, Smooth),
            Key(1f, end, Smooth, 0f),
        });

        private static BezierKnot Key(float x, float y, float back, float forward) => new(
            new Vector2(x, y),
            new Vector2(x - back, y),
            new Vector2(x + forward, y),
            TangentMode.Aligned);
    }
}
