using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LightSide
{
    /// <summary>Collects the glyph requests of shaped content, runs them per font through prepare, render, pack and registration, and applies the atlas upgrades mesh generation asks for.</summary>
    internal static class GlyphBatchRasterizer
    {
        internal const int GlyphBitsLength = 1024;

        internal struct GlyphBatch
        {
            public UniTextFont.Core font;
            public UniTextRenderMode mode;
            public long[] glyphBits;
            public int glyphBitsMax;
            public List<(uint unicode, uint glyphIndex)> characterEntries;
            public UniTextFont.Core.PreparedBatch? prepared;
            public object rendered;
            /// <summary>varHash48 for variable font runs. 0 = use DefaultVarHash48.</summary>
            public long varHash48;
            /// <summary>FT design coordinates for variable fonts. Null = default axes.</summary>
            public int[] ftCoords;
            /// <summary>Silhouette-field requests by glyph index (see <see cref="ColorGlyphField"/>); null when no colour glyph of this batch is decorated.</summary>
            public FastIntDictionary<byte> fieldRequests;

            /// <summary>Marks one glyph index for rasterization (deduplicated by the bit set).</summary>
            public void Add(uint glyphIndex)
            {
                glyphBits[glyphIndex >> 6] |= 1L << (int)(glyphIndex & 63);
                if ((int)glyphIndex > glyphBitsMax)
                    glyphBitsMax = (int)glyphIndex;
            }

            /// <summary>Marks a glyph whose silhouette field must reach at least <paramref name="extent"/> (the larger of repeated requests wins); the glyph itself is collected too.</summary>
            public void RequestField(uint glyphIndex, byte extent)
            {
                Add(glyphIndex);
                if (extent == 0) return;
                fieldRequests ??= fieldRequestPool.Count > 0
                    ? fieldRequestPool.Pop()
                    : new FastIntDictionary<byte>(16);
                if (!fieldRequests.TryGetValue((int)glyphIndex, out var current) || extent > current)
                    fieldRequests[(int)glyphIndex] = extent;
            }

            /// <summary>Marks a glyph that must also register a unicode→glyph character entry (virtual codepoints).</summary>
            public void AddCharacter(uint unicode, uint glyphIndex)
            {
                Add(glyphIndex);
                characterEntries ??= charEntryPool.Count > 0
                    ? charEntryPool.Pop()
                    : new List<(uint, uint)>(64);
                characterEntries.Add((unicode, glyphIndex));
            }
        }

        private struct GlyphBatchKey : IEquatable<GlyphBatchKey>
        {
            public UniTextFont.Core font;
            public UniTextRenderMode mode;
            public long varHash48;

            public bool Equals(GlyphBatchKey other) =>
                ReferenceEquals(font, other.font) && mode == other.mode && varHash48 == other.varHash48;

            public override bool Equals(object obj) => obj is GlyphBatchKey k && Equals(k);
            public override int GetHashCode() => font.GetCachedInstanceId() ^ ((int)mode * 397) ^ varHash48.GetHashCode();
        }

        private static Dictionary<GlyphBatchKey, int> fontIndexMap;
        private static GlyphBatch[] fontBatches;
        private static int fontBatchCount;
        private static Stack<List<(uint, uint)>> charEntryPool;
        private static readonly Stack<FastIntDictionary<byte>> fieldRequestPool = new();
        private static List<uint> tempGlyphList;

        /// <summary>Recycles the previous frame's batches and opens a new collection window.</summary>
        internal static void BeginCollection()
        {
            fontIndexMap ??= new Dictionary<GlyphBatchKey, int>(16);
            charEntryPool ??= new Stack<List<(uint, uint)>>();
            fontBatches ??= new GlyphBatch[8];

            for (int i = 0; i < fontBatchCount; i++)
            {
                ref var prev = ref fontBatches[i];
                if (prev.glyphBits != null && prev.glyphBitsMax >= 0)
                    Array.Clear(prev.glyphBits, 0, (prev.glyphBitsMax >> 6) + 1);
                if (prev.characterEntries != null) { prev.characterEntries.Clear(); charEntryPool.Push(prev.characterEntries); }
                if (prev.fieldRequests != null) { prev.fieldRequests.Clear(); fieldRequestPool.Push(prev.fieldRequests); }
                if (prev.rendered != null) prev.font?.ReleaseRenderedBatch(prev.rendered);
                if (prev.prepared.HasValue) prev.prepared.Value.Return();
                var keepBits = prev.glyphBits;
                prev = default;
                prev.glyphBits = keepBits;
                prev.glyphBitsMax = -1;
            }
            fontIndexMap.Clear();
            fontBatchCount = 0;
        }

        internal static ref GlyphBatch GetOrCreateEntry(UniTextFont.Core font, UniTextRenderMode mode, long varHash48 = 0)
        {
            if (font is { IsColor: true })
            {
                mode = UniTextRenderMode.SDF;
                varHash48 = 0;
            }
            var key = new GlyphBatchKey { font = font, mode = mode, varHash48 = varHash48 };
            if (!fontIndexMap.TryGetValue(key, out var index))
            {
                index = fontBatchCount++;
                if (fontBatches.Length <= index)
                    Array.Resize(ref fontBatches, fontBatches.Length * 2);

                var existingBits = fontBatches[index].glyphBits ?? new long[GlyphBitsLength];
                fontBatches[index] = new GlyphBatch
                {
                    font = font,
                    mode = mode,
                    glyphBits = existingBits,
                    glyphBitsMax = -1,
                    varHash48 = varHash48,
                };
                fontIndexMap[key] = index;
            }
            return ref fontBatches[index];
        }

        /// <summary>
        /// Marks every glyph the shaped content needs into the open batches — run glyphs, virtual codepoints
        /// in each run's face and in the stack's own choice, pre-shaped virtual glyphs, and every
        /// variation-axis twin — and stages the same keys into <paramref name="contentRefs"/>. Both ref sets
        /// are released first, so the outgoing set's orphans park in the LRU immediately and the incoming set
        /// reuses their tiles in the SAME frame — a font swap or a variable-axis change re-rasterizes in place
        /// with zero atlas growth. Safe because this pass enumerates every glyph the new state uses and the
        /// batch-prepare phase revives and pins any still-used refCount-0 key BEFORE any allocation can evict
        /// (all prepares run before all renders), and batch protection then holds everything until the
        /// renderer commit.
        /// </summary>
        internal static void Collect(UniTextBuffers buf, UniTextFontProvider fontProvider,
            UniTextRenderMode renderMode, ref GlyphAtlasRefs meshRefs, ref GlyphAtlasRefs contentRefs)
        {
            meshRefs.ReleaseAll();
            contentRefs.BeginStaging();

            var shapedRuns = buf.shapedRuns.Span;
            var shapedGlyphs = buf.shapedGlyphs.Span;
            var codepoints = buf.codepoints;
            var provider = UnicodeData.Provider;

            var varMap = buf.variationMap;
            var virtualCodepoints = buf.virtualCodepoints;
            var fieldAttribute = buf.GetAttributeData<PooledArrayAttribute<byte>>(AttributeKeys.ColorGlyphField);
            var fieldRequests = fieldAttribute is { Count: > 0 } ? fieldAttribute.buffer.data : null;
            var fieldRequestCount = fieldRequests != null ? fieldAttribute.Count : 0;

            for (int r = 0; r < shapedRuns.Length; r++)
            {
                ref readonly var run = ref shapedRuns[r];
                var font = fontProvider.GetFont(run.fontId);
                if (font is null) continue;
                var runFieldRequests = font.IsColor ? fieldRequests : null;

                long runVarHash = 0;
                int[] runFtCoords = null;
                if (varMap != null && varMap.TryGetValue(run.fontId, out var varInfo))
                {
                    runVarHash = varInfo.varHash48;
                    runFtCoords = varInfo.ftCoords;
                }

                var end = run.glyphStart + run.glyphCount;

                ref var batchEntry = ref GetOrCreateEntry(font, renderMode, runVarHash);
                if (runFtCoords != null)
                    batchEntry.ftCoords = runFtCoords;

                for (int i = 0; i < virtualCodepoints.count; i++)
                {
                    var virtualIndex = font.GetGlyphIndexForUnicode(virtualCodepoints[i]);
                    if (virtualIndex == 0) continue;
                    batchEntry.AddCharacter(virtualCodepoints[i], virtualIndex);
                    contentRefs.Stage(font, runVarHash, virtualIndex, 0);
                }

                for (int g = run.glyphStart; g < end; g++)
                {
                    var glyphIndex = (uint)shapedGlyphs[g].glyphId;
                    if (glyphIndex == 0)
                    {
                        var cp = codepoints[shapedGlyphs[g].cluster];
                        var cat = provider.GetGeneralCategory(cp);
                        if (cat is GeneralCategory.Cc or GeneralCategory.Cf
                            or GeneralCategory.Zl or GeneralCategory.Zp)
                            continue;
                    }

                    if (glyphIndex >= GlyphBitsLength * 64)
                        continue;
                    var cluster = shapedGlyphs[g].cluster;
                    var fieldExtent = runFieldRequests != null && (uint)cluster < (uint)fieldRequestCount
                        ? runFieldRequests[cluster]
                        : (byte)0;
                    if (fieldExtent != 0)
                        batchEntry.RequestField(glyphIndex, fieldExtent);
                    else
                        batchEntry.Add(glyphIndex);
                    contentRefs.Stage(font, runVarHash, glyphIndex, fieldExtent);
                }
            }

            for (int i = 0; i < virtualCodepoints.count; i++)
            {
                var unicode = virtualCodepoints[i];
                var fontId = fontProvider.FindFontForCodepoint((int)unicode);
                var font = fontProvider.GetFont(fontId);
                if (font == null) continue;

                var glyphIndex = font.GetGlyphIndexForUnicode(unicode);

                ref var entry = ref GetOrCreateEntry(font, renderMode);
                entry.AddCharacter(unicode, glyphIndex);
                contentRefs.Stage(font, 0, glyphIndex, 0);

                if (varMap != null)
                {
                    var baseFontHash = font.FontDataHash;
                    foreach (var kvp in varMap)
                    {
                        if (kvp.Value.baseFontHash != baseFontHash) continue;

                        ref var varEntry = ref GetOrCreateEntry(font, renderMode, kvp.Value.varHash48);
                        if (varEntry.ftCoords == null)
                            varEntry.ftCoords = kvp.Value.ftCoords;
                        varEntry.AddCharacter(unicode, glyphIndex);
                        contentRefs.Stage(font, kvp.Value.varHash48, glyphIndex, 0);
                    }
                }
            }

            var virtualGlyphs = buf.virtualGlyphs;
            for (int i = 0; i < virtualGlyphs.count; i++)
            {
                var vGlyph = virtualGlyphs[i];
                var vFont = fontProvider.GetFont(vGlyph.fontId);
                if (vFont == null) continue;

                var vIndex = vGlyph.glyphId;
                if (vIndex >= GlyphBitsLength * 64) continue;

                ref var vEntry = ref GetOrCreateEntry(vFont, renderMode);
                if (vGlyph.fieldExtent != 0 && vFont.IsColor)
                    vEntry.RequestField(vIndex, vGlyph.fieldExtent);
                else
                    vEntry.Add(vIndex);
                contentRefs.Stage(vFont, 0, vIndex, vGlyph.fieldExtent);

                if (varMap != null)
                {
                    var baseFontHash = vFont.FontDataHash;
                    foreach (var kvp in varMap)
                    {
                        if (kvp.Value.baseFontHash != baseFontHash) continue;

                        ref var vVarEntry = ref GetOrCreateEntry(vFont, renderMode, kvp.Value.varHash48);
                        if (vVarEntry.ftCoords == null)
                            vVarEntry.ftCoords = kvp.Value.ftCoords;
                        vVarEntry.Add(vIndex);
                        contentRefs.Stage(vFont, kvp.Value.varHash48, vIndex, 0);
                    }
                }
            }
        }

        internal struct TierUpgradeRequest
        {
            public long glyphKey;
            public uint glyphIndex;
            public byte requiredTier;
            public UniTextFont.Core font;
            public long varHash48;
            public int[] ftCoords;
            public UniTextRenderMode mode;
        }

        internal struct TileSizeUpgradeRequest
        {
            public long glyphKey;
            public uint glyphIndex;
            public int tileSizeBoost;
            public UniTextFont.Core font;
            public long varHash48;
            public int[] ftCoords;
            public UniTextRenderMode mode;
        }

        private static Dictionary<(long, UniTextRenderMode), TierUpgradeRequest> tierUpgrades;
        private static Dictionary<(long, UniTextRenderMode), TileSizeUpgradeRequest> tileSizeUpgrades;

        /// <summary>Queues one generator's upgrade requests for <see cref="ProcessUpgrades"/>, keeping per glyph the highest pad tier and the largest tile boost queued so far.</summary>
        internal static void QueueUpgrades(List<TierUpgradeRequest> tiers, List<TileSizeUpgradeRequest> tileSizes)
        {
            for (int i = 0; i < tiers.Count; i++)
            {
                var req = tiers[i];
                var key = (req.glyphKey, req.mode);
                tierUpgrades ??= new Dictionary<(long, UniTextRenderMode), TierUpgradeRequest>();
                if (!tierUpgrades.TryGetValue(key, out var existing) || req.requiredTier > existing.requiredTier)
                    tierUpgrades[key] = req;
            }

            for (int i = 0; i < tileSizes.Count; i++)
            {
                var req = tileSizes[i];
                var key = (req.glyphKey, req.mode);
                tileSizeUpgrades ??= new Dictionary<(long, UniTextRenderMode), TileSizeUpgradeRequest>();
                if (!tileSizeUpgrades.TryGetValue(key, out var existing) || req.tileSizeBoost > existing.tileSizeBoost)
                    tileSizeUpgrades[key] = req;
            }
        }

        /// <summary>Applies the queued upgrades and empties the queue. Grow-only tile-size relocations go first — <see cref="GlyphAtlas.UpgradeGlyphTileSize"/> publishes through the transform-table row, so meshes are untouched and a same-frame tier bump lands in the relocated tile — then pad-tier re-rasterization, in place while the raster is still pending.</summary>
        internal static void ProcessUpgrades()
        {
            if (tileSizeUpgrades is { Count: > 0 })
            {
                int promoted = 0;
                foreach (var kvp in tileSizeUpgrades)
                {
                    var req = kvp.Value;
                    if (req.font.ReExtractForTileSizeUpgrade(
                            req.glyphIndex, req.varHash48, req.ftCoords, req.mode, req.tileSizeBoost))
                        promoted++;
                }
                if (promoted > 0)
                    CatZones.raster.MeowFormat(
                        "[UniText] TileSizeUpgrades: {0} glyph(s) promoted to a larger atlas tile (grow-only, shared)", promoted);
                tileSizeUpgrades.Clear();
            }

            if (tierUpgrades is { Count: > 0 })
            {
                int reExtracted = 0;
                foreach (var kvp in tierUpgrades)
                {
                    var req = kvp.Value;
                    if (GlyphAtlas.GetInstance(req.mode).TryUpgradePendingTier(req.glyphKey, req.requiredTier))
                        continue;
                    req.font.ReExtractForTierUpgrade(
                        req.glyphIndex, req.varHash48, req.ftCoords, req.mode, req.requiredTier);
                    reExtracted++;
                }
                CatZones.raster.MeowFormat("[UniText] TierUpgrades: {0} unique glyphs ({1} re-extracted)",
                    tierUpgrades.Count, reExtracted);
                tierUpgrades.Clear();
            }
        }

        /// <summary>Runs the collected batches through the full rasterization pipeline. No-op when nothing was collected.</summary>
        internal static void Run()
        {
            int batchCount = fontBatchCount;
            if (batchCount == 0) return;

            CatZones.raster.Meow($"[UniText Raster] batchCount={batchCount}");
            tempGlyphList ??= new List<uint>(256);

            try
            {
                for (int i = 0; i < batchCount; i++)
                {
                    ref var batch = ref fontBatches[i];
                    if (batch.glyphBitsMax < 0) continue;

                    tempGlyphList.Clear();
                    int maxWord = batch.glyphBitsMax >> 6;
                    for (int w = 0; w <= maxWord; w++)
                    {
                        ulong bits = (ulong)batch.glyphBits[w];
                        if (bits == 0) continue;
                        int baseIndex = w << 6;
                        for (int b = 0; bits != 0; b++, bits >>= 1)
                            if ((bits & 1) != 0)
                                tempGlyphList.Add((uint)(baseIndex + b));
                    }

                    CatZones.raster.Meow($"[UniText Raster] batch[{i}]: font={batch.font?.Name}, glyphs={tempGlyphList.Count}, mode={batch.mode}");
                    long varHash = batch.varHash48 != 0
                        ? batch.varHash48
                        : batch.font.DefaultVarHash48;
                    batch.prepared = batch.font.PrepareGlyphBatch(
                        tempGlyphList, batch.mode, varHash, batch.ftCoords, batch.fieldRequests);
                    CatZones.raster.Meow($"[UniText Raster] batch[{i}]: prepared={batch.prepared.HasValue}");
                }

                int fontsToRender = 0;
                int totalGlyphsToRender = 0;
                int largestBatch = 0;
                for (int i = 0; i < batchCount; i++)
                {
                    if (!fontBatches[i].prepared.HasValue) continue;
                    fontsToRender++;
                    int glyphCount = fontBatches[i].prepared.Value.filteredGlyphs.count;
                    totalGlyphsToRender += glyphCount;
                    largestBatch = Math.Max(largestBatch, glyphCount);
                }

                var timer = new DebugTimer();
                timer.Mark();
                GlyphCurveCache.ResetTimers();

#if !UNITY_WEBGL || UNITY_EDITOR
                if (!GlyphAtlas.forceSingleThreaded && fontsToRender > 1
                                                        && totalGlyphsToRender >= 16
                                                        && largestBatch < 16)
                {
                    Parallel.For(0, batchCount, i =>
                    {
                        if (fontBatches[i].prepared.HasValue)
                            fontBatches[i].rendered = fontBatches[i].font.RenderPreparedBatch(
                                fontBatches[i].prepared.Value);
                    });
                }
                else
#endif
                {
                    for (int i = 0; i < batchCount; i++)
                    {
                        if (!fontBatches[i].prepared.HasValue) continue;
                        CatZones.raster.Meow($"[UniText Raster] RenderPreparedBatch[{i}] START font={fontBatches[i].font?.Name}");
                        fontBatches[i].rendered = fontBatches[i].font.RenderPreparedBatch(
                            fontBatches[i].prepared.Value);
                        CatZones.raster.Meow($"[UniText Raster] RenderPreparedBatch[{i}] DONE rendered={fontBatches[i].rendered != null}");
                    }
                }
                timer.Mark();

                int sdfGlyphs = 0;
                int colorGlyphs = 0;
                for (int i = 0; i < batchCount; i++)
                {
                    if (fontBatches[i].rendered == null) continue;
                    int glyphCount = fontBatches[i].prepared?.filteredGlyphs.count ?? 0;
                    if (fontBatches[i].font is { IsColor: true }) colorGlyphs += glyphCount;
                    else sdfGlyphs += glyphCount;
                }
                timer.Mark();

                for (int i = 0; i < batchCount; i++)
                {
                    ref var batch = ref fontBatches[i];
                    if (batch.rendered != null)
                        batch.font.PackRenderedBatch(
                            batch.rendered, batch.prepared.Value, batch.mode);
                    if (batch.characterEntries is { Count: > 0 })
                        batch.font.RegisterCharacterEntries(batch.characterEntries);
                }
                timer.Mark();

                CatZones.raster.Meow($"[UniText] {sdfGlyphs} sdf + {colorGlyphs} color = {timer.Total:F0}ms | " +
                         $"extract={timer.Phase(0):F0}ms " +
                         $"(ft={GlyphCurveCache.TicksToMs(GlyphCurveCache.ftTicks):F0} " +
                         $"union={GlyphCurveCache.TicksToMs(GlyphCurveCache.unionTicks):F0}" +
                         $"[{ContourUnionBurst.statChanged}ch/{ContourUnionBurst.statPromoted}pr/{ContourUnionBurst.statBailed}bail" +
                         $"(in={ContourUnionBurst.statBailInput} bud={ContourUnionBurst.statBailBudget} " +
                         $"cap={ContourUnionBurst.statBailCaps} cls={ContourUnionBurst.statBailClassify})] " +
                         $"norm={GlyphCurveCache.TicksToMs(GlyphCurveCache.normalizeTicks):F0} " +
                         $"color={GlyphCurveCache.TicksToMs(GlyphCurveCache.edgeColorTicks):F0}) " +
                         $"texture={timer.Phase(1):F0}ms " +
                         $"pack={timer.Phase(2):F0}ms");
            }
            finally
            {
                for (int i = 0; i < batchCount; i++)
                {
                    ref var batch = ref fontBatches[i];
                    if (batch.rendered != null)
                        batch.font?.ReleaseRenderedBatch(batch.rendered);
                    batch.rendered = null;
                    if (batch.prepared.HasValue)
                        batch.prepared.Value.Return();
                    batch.prepared = null;
                }
            }
        }
    }
}
