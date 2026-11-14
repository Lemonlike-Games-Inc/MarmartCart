using System.Collections.Generic;

namespace LightSide
{
    /// <summary>
    /// One holder's refs into the three glyph key spaces — outline glyphs of a render mode, colour tiles,
    /// and colour-glyph silhouette fields, which always live in the SDF atlas. A set is replaced either
    /// incrementally (<see cref="Update"/>: new keys are refd before old ones are released, so a key in
    /// both never touches zero) or from scratch (<see cref="BeginStaging"/>, <see cref="Stage"/>,
    /// <see cref="Commit"/>: released first, refd once the staged keys exist in their atlases). Main
    /// thread only, one holder staging at a time; <see cref="Return"/> releases everything held.
    /// </summary>
    internal struct GlyphAtlasRefs
    {
        private Tracker glyph;
        private Tracker color;
        private Tracker field;

        private static readonly HashSet<long> stagingSeen = new();
        private bool staging;

        public int GlyphCount => glyph.Count;
        public int ColorCount => color.Count;
        public int FieldCount => field.Count;

        public void Update(UniTextRenderMode mode, ref PooledBuffer<long> glyphKeys,
            ref PooledBuffer<long> colorKeys, ref PooledBuffer<long> fieldKeys)
        {
            glyph.Update(GlyphAtlas.GetInstance(mode), ref glyphKeys);

            var colorAtlas = GlyphAtlas.Color;
            if (colorAtlas != null) color.Update(colorAtlas, ref colorKeys);
            else color.ReleaseAll();

            if (fieldKeys.count > 0 || field.Count > 0)
            {
                if (GlyphAtlas.TryGetExistingInstance(UniTextRenderMode.SDF, out var fieldAtlas))
                    field.Update(fieldAtlas, ref fieldKeys);
                else field.ReleaseAll();
            }
        }

        /// <summary>Releases the held set and opens a fresh one for <see cref="Stage"/>; open until <see cref="Commit"/>.</summary>
        public void BeginStaging()
        {
            ReleaseAll();
            stagingSeen.Clear();
            staging = true;
        }

        /// <summary>Stages the keys one glyph request resolves to, each once: the colour tile and, when a silhouette field is requested, its SDF field; otherwise the outline glyph under <paramref name="varHash48"/>, or the font's default variation when that is zero.</summary>
        public void Stage(UniTextFont.Core font, long varHash48, uint glyphIndex, byte fieldExtent)
        {
            if (font.IsColor)
            {
                var colorKey = GlyphAtlas.MakeKey(font.DefaultVarHash48, glyphIndex);
                if (stagingSeen.Add(colorKey)) color.Stage(colorKey);
                if (fieldExtent == 0) return;
                var fieldKey = GlyphAtlas.MakeKey(GlyphAtlas.FieldVarHash48(font.FontDataHash), glyphIndex);
                if (stagingSeen.Add(fieldKey)) field.Stage(fieldKey);
                return;
            }

            var key = GlyphAtlas.MakeKey(varHash48 != 0 ? varHash48 : font.DefaultVarHash48, glyphIndex);
            if (stagingSeen.Add(key)) glyph.Stage(key);
        }

        /// <summary>Refs every staged key and closes the staging; a no-op while none is open. Colour and field keys staged while their atlas does not exist are dropped.</summary>
        public void Commit(UniTextRenderMode mode)
        {
            if (!staging) return;
            staging = false;
            glyph.Commit(GlyphAtlas.GetInstance(mode));

            var colorAtlas = GlyphAtlas.Color;
            if (colorAtlas != null) color.Commit(colorAtlas);
            else color.ReleaseAll();

            if (GlyphAtlas.TryGetExistingInstance(UniTextRenderMode.SDF, out var fieldAtlas))
                field.Commit(fieldAtlas);
            else field.ReleaseAll();
        }

        public void ReleaseAll()
        {
            staging = false;
            glyph.ReleaseAll();
            color.ReleaseAll();
            field.ReleaseAll();
        }

        /// <summary>Forgets keys their atlas no longer holds without releasing the rest; true when any key was forgotten.</summary>
        public bool DropMissing() => glyph.DropMissing() | color.DropMissing() | field.DropMissing();

        /// <summary>Releases the held keys and returns the buffers.</summary>
        public void Return()
        {
            glyph.Return();
            color.Return();
            field.Return();
        }

        private struct Tracker
        {
            private PooledBuffer<long> current;
            private PooledBuffer<long> previous;
            /// <summary>Atlas the current keys are AddRef'd into. Releases must target it even if the holder's atlas changed since (render mode switch, atlas recreation) — releasing old keys into the new atlas both leaks the old entries and steals refs from same-keyed new ones.</summary>
            private GlyphAtlas atlas;

            public int Count => current.count;

            public void Update(GlyphAtlas newAtlas, ref PooledBuffer<long> newKeys)
            {
                var previousAtlas = atlas;
                atlas = newAtlas;
                (previous, current) = (current, previous);
                current.FakeClear();
                current.EnsureCapacity(newKeys.count);
                newKeys.Span.CopyTo(current.data);
                current.count = newKeys.count;
                for (int i = 0; i < current.count; i++)
                    newAtlas.AddRef(current[i]);
                if (previousAtlas != null)
                    for (int i = 0; i < previous.count; i++)
                        previousAtlas.Release(previous[i]);
            }

            public void ReleaseAll()
            {
                if (atlas != null)
                    for (int i = 0; i < current.count; i++)
                        atlas.Release(current[i]);
                current.FakeClear();
                atlas = null;
            }

            /// <summary>Appends a key for the next <see cref="Commit"/>, straight into the held set: legal only after <see cref="ReleaseAll"/>, while nothing is refd, so an abandoned staging is dropped by the next release instead of released.</summary>
            public void Stage(long key) => current.Add(key);

            /// <summary>Refs every staged key in <paramref name="newAtlas"/>, which becomes the atlas the set releases into.</summary>
            public void Commit(GlyphAtlas newAtlas)
            {
                atlas = newAtlas;
                for (int i = 0; i < current.count; i++)
                    newAtlas.AddRef(current[i]);
            }

            public bool DropMissing()
            {
                if (current.count == 0 || atlas == null) return false;
                int write = 0;
                for (int i = 0; i < current.count; i++)
                {
                    long key = current[i];
                    if (atlas.TryGetEntry(key, out _))
                        current[write++] = key;
                }
                bool removed = write != current.count;
                current.count = write;
                return removed;
            }

            public void Return()
            {
                ReleaseAll();
                current.Return();
                previous.Return();
            }
        }
    }
}
