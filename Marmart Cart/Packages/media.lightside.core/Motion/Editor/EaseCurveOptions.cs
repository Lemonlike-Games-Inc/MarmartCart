namespace LightSide
{
    /// <summary>
    /// What a curve editor offers, so one editor serves a timing curve and a free profile alike. A timing
    /// curve is played over a duration, so it pins its ends and shows the motion preview and the easing
    /// gallery; a profile runs along something instead, so its ends move and it offers profile shapes.
    /// </summary>
    public readonly struct EaseCurveOptions
    {
        /// <summary>Whether the first and last knots hold the heights they were given, moving only the ones between.</summary>
        public readonly bool PinEndValues;

        /// <summary>Whether the strip that plays the curve is shown.</summary>
        public readonly bool ShowPreview;

        /// <summary>The gallery offered under the canvas; empty for none.</summary>
        public readonly EasePreset[] Presets;

        public EaseCurveOptions(bool pinEndValues, bool showPreview, EasePreset[] presets)
        {
            PinEndValues = pinEndValues;
            ShowPreview = showPreview;
            Presets = presets;
        }

        /// <summary>A curve played over time: ends pinned, with the preview and the easing gallery.</summary>
        public static EaseCurveOptions Timing => new(true, true, null);

        /// <summary>A curve read along something: ends free, no preview, with the profile gallery.</summary>
        public static EaseCurveOptions Profile => new(false, false, EaseProfiles.All);

        /// <summary>Whether a gallery is offered at all.</summary>
        public bool HasPresets => Presets == null || Presets.Length > 0;
    }
}
