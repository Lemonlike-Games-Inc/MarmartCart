using System;
using UnityEngine;

namespace LightSide
{
    /// <summary>
    /// Slow two-axis drift on smooth noise — glyphs hover as if suspended in air. Low
    /// <see cref="Params.spread"/> makes neighbors drift together; high values decouple them.
    /// </summary>
    [Serializable]
    [TypeGroup("Animation", 6)]
    [TypeDescription("Smooth hovering drift; drive Phase to animate.")]
    [GenerateParameters]
    public partial class FloatModifier : GlyphParamModifier<FloatModifier.Params>
    {
        public struct Params
        {
            public float amplitude;
            public float frequency;
            public float spread;
        }

        /// <summary>Peak drift in pixels.</summary>
        [Tooltip("Peak drift in pixels.")]
        [SerializeField, Parameter, StateProperty(nameof(MarkParamsDirty))]
        private float amplitude = 2f;

        /// <summary>Noise steps per phase unit.</summary>
        [Tooltip("Drift direction changes per phase unit; higher values make the hovering busier.")]
        [SerializeField, Parameter, StateProperty(nameof(MarkParamsDirty))]
        private float frequency = 0.6f;

        /// <summary>Noise offset between adjacent clusters — drift coherence along the text.</summary>
        [Tooltip("Noise offset between neighbouring glyphs; 0 drifts them all together, higher values decouple them.")]
        [SerializeField, Parameter, StateProperty(nameof(MarkParamsDirty))]
        private float spread = 1.3f;

        protected override string AttributeKey => AttributeKeys.Float;

        protected override Params ResolveParams(in RangeApplyContext context) => new()
        {
            amplitude = Param.Amplitude.Resolve(this, in context),
            frequency = Param.Frequency.Resolve(this, in context),
            spread = Param.Spread.Resolve(this, in context),
        };

        protected override void OnGlyph(UniTextMeshGenerator gen, int cluster, in Params p, float phase)
        {
            var t = phase * p.frequency + cluster * p.spread;
            var dx = HashNoise.ValueSigned(t, 11) * p.amplitude;
            var dy = HashNoise.ValueSigned(t, 12) * p.amplitude;
            GlyphQuad.Offset(gen.Vertices, gen.faceBaseIdx, dx, dy);
        }
    }
}
