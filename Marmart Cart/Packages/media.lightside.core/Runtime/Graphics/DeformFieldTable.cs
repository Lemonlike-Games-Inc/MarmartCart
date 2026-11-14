using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightSide
{
    /// <summary>
    /// One deformation's shader-side parameter set: the travelling value-noise field a deformed glyph
    /// samples its distance field through, in glyph-height units so it renders identically at every
    /// font size.
    /// </summary>
    public struct DeformParams : IEquatable<DeformParams>
    {
        /// <summary>Peak displacement of the sample point, in glyph-height units.</summary>
        public float amplitude;

        /// <summary>Noise cells per glyph height.</summary>
        public float scale;

        /// <summary>Where the field has travelled to, in glyph-height units — the animated value.</summary>
        public Vector2 offset;

        public bool Equals(DeformParams other)
            => amplitude == other.amplitude && scale == other.scale
               && offset.x == other.offset.x && offset.y == other.offset.y;

        public override bool Equals(object obj) => obj is DeformParams other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(amplitude, scale, offset);
    }

    /// <summary>
    /// Shared data-texture table holding one RGBAFloat texel per live deformation, fetched with
    /// <c>Load</c> by the glyph surfaces. Rows are allocated to a writer rather than shared by value —
    /// a deformation's offset changes every frame, and rewriting one row is what keeps an animated
    /// deformation from consuming a row per frame. Acquired rows stay stable until released and
    /// reclaimed, and GPU uploads are deferred until <see cref="Flush"/>.
    /// </summary>
    public sealed class DeformFieldTable
    {
        /// <summary>The process-wide deform table shared by rendering systems.</summary>
        public static readonly DeformFieldTable Instance = new();

        /// <summary>Row of a deformation that needs no storage.</summary>
        public const int InvalidRow = -1;

        /// <summary>RGBAFloat texels one row occupies.</summary>
        public const int RowTexels = 1;

        /// <summary>
        /// Step at which a row index rides the glyph's <c>UV1.x</c> lane, above the glyph aspect that
        /// shares it: the lane carries <c>aspect + RowLaneStep · (row + 1)</c>, so a quad no deformation
        /// touched decodes to <see cref="InvalidRow"/>.
        /// </summary>
        /// <remarks>SYNC: mirrors <c>LIGHTSIDE_DEFORM_ROW_STEP</c>.</remarks>
        public const float RowLaneStep = 1024f;

        private static readonly int textureId = Shader.PropertyToID("_LightSideDeformTable");

        private const int InitialCapacity = 8;
        private const int MaintenanceInterval = 300;

        private readonly object sync = new();
        private readonly List<DeformParams> rows = new();
        private readonly List<int> refCounts = new();
        private readonly List<int> idleFrame = new();
        private readonly Stack<int> freeSlots = new();
        private readonly List<int> pending = new();
        private readonly Color[] rowBuffer = new Color[RowTexels];

        private Texture2D texture;
        private int capacity;
        private int lastMaintenanceFrame = -1;

        private DeformFieldTable()
        {
        }

        static DeformFieldTable()
        {
            CoreLoop.Maintaining += Instance.MaintenanceTick;
#if UNITY_EDITOR
            EditorLifecycle.UnmanagedCleaning += Instance.DestroyTextures;
#endif
        }

        /// <summary>
        /// Encodes a row into the glyph <c>UV1.x</c> lane alongside the aspect already there. Pass
        /// <see cref="InvalidRow"/> to leave the lane carrying aspect alone.
        /// </summary>
        public static float PackRowLane(float aspect, int row)
            => row < 0 ? aspect : aspect + RowLaneStep * (row + 1);

        /// <summary>
        /// Acquires a row for one writer to keep and rewrite. Pair every call with
        /// <see cref="Release"/>. Thread-safe and allocation-free once the table has grown.
        /// </summary>
        public int Acquire()
        {
            lock (sync)
            {
                int row;
                if (freeSlots.Count > 0)
                {
                    row = freeSlots.Pop();
                    rows[row] = default;
                    refCounts[row] = 1;
                    idleFrame[row] = -1;
                }
                else
                {
                    row = rows.Count;
                    rows.Add(default);
                    refCounts.Add(1);
                    idleFrame.Add(-1);
                }

                pending.Add(row);
                return row;
            }
        }

        /// <summary>
        /// Stores <paramref name="value"/> in <paramref name="row"/> and queues its upload; an
        /// unchanged value is not re-uploaded. Writing <see cref="InvalidRow"/> is the defined no-op
        /// for a deformation that needs no storage; other invalid rows fail immediately.
        /// </summary>
        public void Write(int row, in DeformParams value)
        {
            if (row == InvalidRow) return;

            lock (sync)
            {
                if ((uint)row >= (uint)refCounts.Count || refCounts[row] <= 0)
                    throw new ArgumentOutOfRangeException(nameof(row), row, "The deform row is not allocated.");
                if (rows[row].Equals(value)) return;

                rows[row] = value;
                pending.Add(row);
            }
        }

        /// <summary>
        /// Releases the reference acquired through <see cref="Acquire"/>. The row remains stable and
        /// available for reacquisition until a later maintenance sweep reclaims it — a mesh still on
        /// screen keeps pointing at it until its next rebuild. Releasing <see cref="InvalidRow"/> is
        /// the defined no-op; other invalid or unbalanced releases fail immediately.
        /// </summary>
        public void Release(int row)
        {
            if (row == InvalidRow) return;

            lock (sync)
            {
                if ((uint)row >= (uint)refCounts.Count || refCounts[row] < 0)
                    throw new ArgumentOutOfRangeException(nameof(row), row, "The deform row is not allocated.");
                if (refCounts[row] == 0)
                    throw new InvalidOperationException($"Deform row {row} has already been released.");
                if (--refCounts[row] == 0) idleFrame[row] = -1;
            }
        }

        /// <summary>
        /// Runs the table's periodic reclaim policy. Call once per frame with a monotonically
        /// increasing frame number; work is performed every 300 frames.
        /// </summary>
        public void MaintenanceTick(int frame)
        {
            if (lastMaintenanceFrame == frame) return;
            lastMaintenanceFrame = frame;
            if (frame % MaintenanceInterval != 0) return;
            Sweep(frame, MaintenanceInterval);
            Flush();
        }

        /// <summary>
        /// Reclaims rows that have remained unreferenced for at least <paramref name="graceFrames"/>.
        /// Live row indices never move. Call on the main thread with the current frame number.
        /// </summary>
        public void Sweep(int frame, int graceFrames)
        {
            if (graceFrames < 0) throw new ArgumentOutOfRangeException(nameof(graceFrames));

            lock (sync)
            {
                for (var row = 0; row < rows.Count; row++)
                {
                    if (refCounts[row] != 0) continue;
                    if (idleFrame[row] < 0)
                    {
                        idleFrame[row] = frame;
                        continue;
                    }
                    if (frame - idleFrame[row] < graceFrames) continue;

                    rows[row] = default;
                    refCounts[row] = -1;
                    idleFrame[row] = -1;
                    freeSlots.Push(row);
                }
            }
        }

        /// <summary>
        /// Bakes every queued row and publishes the table through its shared shader global. Growth
        /// re-bakes all live rows. Main thread only.
        /// </summary>
        public void Flush()
        {
            Texture2D retired = null;

            lock (sync)
            {
                if (pending.Count == 0) return;

                if (EnsureCapacity(rows.Count, out retired))
                {
                    for (var row = 0; row < rows.Count; row++)
                    {
                        if (refCounts[row] < 0) continue;
                        BakeRow(row);
                    }
                }
                else
                {
                    for (var i = 0; i < pending.Count; i++)
                    {
                        var row = pending[i];
                        if (refCounts[row] < 0) continue;
                        BakeRow(row);
                    }
                }

                texture.Apply(false, false);
                pending.Clear();
                BindGlobals();
            }

            ObjectUtils.SafeDestroy(retired);
        }

        private bool EnsureCapacity(int rowCount, out Texture2D retired)
        {
            retired = null;
            if (texture != null && capacity >= rowCount) return false;

            var newCapacity = Mathf.Max(InitialCapacity, capacity);
            while (newCapacity < rowCount) newCapacity *= 2;

            var replacement = new Texture2D(RowTexels, newCapacity, TextureFormat.RGBAFloat, false, true)
            {
                name = "Deform Field Table",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave,
            };

            retired = texture;
            texture = replacement;
            capacity = newCapacity;
            return true;
        }

        private void BakeRow(int row)
        {
            var p = rows[row];
            rowBuffer[0] = new Color(p.amplitude, p.scale, p.offset.x, p.offset.y);
            texture.SetPixels(0, row, RowTexels, 1, rowBuffer);
        }

        private void BindGlobals()
        {
            Shader.SetGlobalTexture(textureId, texture);
        }

        /// <summary>Destroys native texture state without invalidating rows still held during reload teardown.</summary>
        internal void DestroyTextures()
        {
            Texture2D retired;

            lock (sync)
            {
                retired = texture;
                texture = null;
                capacity = 0;
                lastMaintenanceFrame = -1;
                pending.Clear();
                for (var row = 0; row < rows.Count; row++)
                    if (refCounts[row] >= 0)
                        pending.Add(row);
                BindGlobals();
            }

            ObjectUtils.SafeDestroy(retired);
        }
    }
}
