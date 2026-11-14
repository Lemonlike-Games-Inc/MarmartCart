using System;
using UnityEngine;

namespace LightSide
{
    /// <summary>
    /// Bends the glyph outline per pixel: covered glyphs sample their distance field through a
    /// travelling noise field, so strokes wobble and boil while every layer derived from that field —
    /// fill, stroke, shadow, glow, inner-shadow — bends with them and the edge keeps its analytic
    /// anti-aliasing. Drive <see cref="GlyphParamModifier{TParams}.Phase"/> to animate; a fixed phase
    /// renders that exact state, which is a still hand-drawn unevenness.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This deforms the field a glyph is drawn from, not the quad it is drawn on: it composes with the
    /// vertex animations (wave, wobble, spin, reveal handlers), which move the glyph as a rigid body.
    /// </para>
    /// <para>
    /// <see cref="Amplitude"/> and <see cref="Scale"/> are in glyph-height units, so a deformation
    /// renders identically at every font size, and the amplitude is reserved as the glyph's outward
    /// reach — the atlas tile re-rasterizes at a wider rim and the quad expands, exactly as a stroke
    /// or glow does. The reach saturates at the atlas's signed-distance spread
    /// (<see cref="GlyphAtlas.Pad"/> em); past that the field holds no more distance to displace into.
    /// Colour glyphs are left undeformed.
    /// </para>
    /// </remarks>
    [Serializable]
    [TypeGroup("Animation", 12)]
    [TypeDescription("Bends the glyph outline per pixel through a travelling noise field; drive Phase to animate.")]
    [GenerateParameters]
    public partial class DeformModifier : GlyphParamModifier<DeformModifier.Params>
    {
        public struct Params
        {
            public float amplitude;
            public float scale;
            public Vector2 drift;
        }

        /// <summary>Peak displacement in glyph-height units.</summary>
        [Tooltip("Peak displacement, in glyph heights. 0.02 is a subtle unevenness; past ~0.1 the glyph loses its shape.")]
        [SerializeField, Parameter, StateProperty(nameof(MarkParamsDirty))]
        private float amplitude = 0.02f;

        /// <summary>Noise cells per glyph height — fine cells ripple the outline, coarse ones sway the whole stroke.</summary>
        [Tooltip("Noise cells per glyph height: fine cells ripple the outline, coarse ones sway the whole stroke.")]
        [SerializeField, Parameter, StateProperty(nameof(MarkParamsDirty))]
        private float scale = 5f;

        /// <summary>How far the noise field travels per phase unit, in glyph-height units.</summary>
        [Tooltip("How far the noise field travels per phase unit, in glyph heights. Drives which way the deformation flows once Phase advances.")]
        [SerializeField, StateProperty(nameof(MarkParamsDirty))]
        private Vector2 drift = new(0.5f, 0f);

        /// <summary>
        /// One published parameter set and the table row it is published through. Rows are claimed in
        /// the order the rebuild first meets each set, so a set keeps its row across rebuilds and an
        /// animated phase rewrites one row instead of consuming a row per frame.
        /// </summary>
        private struct Slot
        {
            public int row;
            public DeformParams value;
        }

        private PooledList<Slot> slots;
        private int usedSlots;
        private Action onRebuildStartCallback;

        protected override string AttributeKey => AttributeKeys.Deform;

        protected override void OnEnable()
        {
            slots ??= new PooledList<Slot>();
            onRebuildStartCallback ??= ResetSlotCursor;
            uniText.MeshGenerator.onRebuildStart.Subscribe(onRebuildStartCallback);
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            uniText.MeshGenerator.onRebuildStart.Unsubscribe(onRebuildStartCallback);
            ReleaseRows();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            ReleaseRows();
            slots?.Return();
            slots = null;
            base.OnDestroy();
        }

        private void ResetSlotCursor() => usedSlots = 0;

        private void ReleaseRows()
        {
            if (slots == null) return;
            for (var i = 0; i < slots.Count; i++)
                DeformFieldTable.Instance.Release(slots[i].row);
            slots.FakeClear();
            usedSlots = 0;
        }

        protected override Params ResolveParams(in RangeApplyContext context) => new()
        {
            amplitude = Param.Amplitude.Resolve(this, in context),
            scale = Param.Scale.Resolve(this, in context),
            drift = drift,
        };

        protected override void OnGlyph(UniTextMeshGenerator gen, int cluster, in Params p, float phase)
        {
            if (p.amplitude == 0f) return;

            var baseIdx = gen.faceBaseIdx;
            if (baseIdx < 0 || gen.font is { IsColor: true }) return;

            var published = new DeformParams
            {
                amplitude = p.amplitude,
                scale = p.scale,
                offset = p.drift * phase,
            };

            var lane = DeformFieldTable.PackRowLane(gen.Uvs1[baseIdx].x, ResolveRow(in published));
            var uv1 = gen.Uvs1;
            uv1[baseIdx].x = lane;
            uv1[baseIdx + 1].x = lane;
            uv1[baseIdx + 2].x = lane;
            uv1[baseIdx + 3].x = lane;

            ReserveOutwardReach(gen, baseIdx, p.amplitude);
        }

        private int ResolveRow(in DeformParams value)
        {
            for (var i = 0; i < usedSlots; i++)
                if (slots[i].value.Equals(value))
                    return slots[i].row;

            if (usedSlots == slots.Count)
                slots.Add(new Slot { row = DeformFieldTable.Instance.Acquire() });

            ref var slot = ref slots[usedSlots++];
            slot.value = value;
            DeformFieldTable.Instance.Write(slot.row, in value);
            return slot.row;
        }

        /// <summary>
        /// Reserves the rim the displaced sample point reaches into: the tier request re-rasterizes the
        /// tile with valid field out there, and the quad expansion makes that field visible. Both are
        /// capped by the atlas's signed-distance spread and by the room the face dilate left.
        /// </summary>
        private static void ReserveOutwardReach(UniTextMeshGenerator gen, int baseIdx, float amplitude)
        {
            var glyphH = gen.FaceGlyphH(baseIdx);
            if (glyphH < 1e-6f) return;

            var padGlyph = GlyphAtlas.Pad / glyphH;
            var reach = amplitude < 0f ? -amplitude : amplitude;
            if (reach > padGlyph) reach = padGlyph;
            if (reach > gen.currentMaxGlyphExtent) gen.currentMaxGlyphExtent = reach;

            var room = padGlyph - gen.Uvs1[baseIdx].y * padGlyph;
            var delta = reach < room ? reach : room;
            if (delta > 0f) gen.ExpandQuad(baseIdx, delta);
        }
    }
}
