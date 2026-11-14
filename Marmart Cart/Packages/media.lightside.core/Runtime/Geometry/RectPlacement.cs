using System;
using UnityEngine;

namespace LightSide
{
    /// <summary>
    /// Where a rectangle stands inside its parent, on the model a <c>RectTransform</c> uses: anchors that tie
    /// each corner to a fraction of the parent, a position and a size measured against those anchors, and a
    /// pivot the rectangle turns about. Anchors apart lets a rectangle stretch with its parent, anchors together
    /// pins it; the same value therefore expresses a margin, an offset, a fixed size, or any mixture of them.
    /// </summary>
    /// <remarks>
    /// The default is the whole parent: anchors spanning it, no position, no size of its own. Rectangles nest —
    /// a shape stands in its component's rect, a layer in the shape's, a composite element in the combination's —
    /// so one vocabulary describes placement at every level.
    /// </remarks>
    [Serializable]
    public struct RectPlacement : IEquatable<RectPlacement>, IStateSnapshot<RectPlacement>
    {
        /// <summary>Lower-left corner's anchor, as a fraction of the parent.</summary>
        public Vector2 anchorMin;

        /// <summary>Upper-right corner's anchor, as a fraction of the parent.</summary>
        public Vector2 anchorMax;

        /// <summary>Position of <see cref="pivot"/> relative to the anchors, in local units.</summary>
        [VectorDragField]
        public Vector2 anchoredPosition;

        /// <summary>Size beyond what the anchors already span, in local units; negative insets the rectangle.</summary>
        [VectorDragField]
        public Vector2 sizeDelta;

        /// <summary>The point the rectangle is positioned and turned about, as a fraction of its own size.</summary>
        public Vector2 pivot;

        /// <summary>Turn about <see cref="pivot"/>, in degrees counter-clockwise.</summary>
        public float rotation;

        /// <summary>The whole parent, turned about its centre by nothing.</summary>
        public static RectPlacement Fill => new()
        {
            anchorMax = Vector2.one,
            pivot = new Vector2(0.5f, 0.5f),
        };

        /// <summary>Whether this places the rectangle over the whole parent, untouched.</summary>
        public bool IsFill => anchorMin == Vector2.zero && anchorMax == Vector2.one &&
                              anchoredPosition == Vector2.zero && sizeDelta == Vector2.zero &&
                              rotation == 0f;

        /// <summary>
        /// The rectangle this places inside <paramref name="parent"/>. The anchors span their fractions of the
        /// parent; the position and size then offset each corner from its own anchor, which is what makes an
        /// inset, a pinned offset, and a fixed size one value. A size the parent cannot carry collapses to
        /// nothing rather than inverting.
        /// </summary>
        public Rect Resolve(Rect parent)
        {
            var anchoredMin = parent.min + Vector2.Scale(anchorMin, parent.size);
            var anchoredMax = parent.min + Vector2.Scale(anchorMax, parent.size);
            var min = anchoredMin + anchoredPosition - Vector2.Scale(sizeDelta, pivot);
            var max = anchoredMax + anchoredPosition + Vector2.Scale(sizeDelta, Vector2.one - pivot);
            return Rect.MinMaxRect(min.x, min.y, Mathf.Max(max.x, min.x), Mathf.Max(max.y, min.y));
        }

        /// <summary>The point <paramref name="resolved"/> is turned about — its own pivot, in local units.</summary>
        public Vector2 PivotOf(Rect resolved)
            => resolved.min + Vector2.Scale(pivot, resolved.size);

        /// <inheritdoc/>
        public RectPlacement CaptureStateSnapshot() => this;

        /// <inheritdoc/>
        public bool StateEquals(in RectPlacement snapshot) => Equals(snapshot);

        /// <inheritdoc/>
        public bool Equals(RectPlacement other)
            => anchorMin.Equals(other.anchorMin) &&
               anchorMax.Equals(other.anchorMax) &&
               anchoredPosition.Equals(other.anchoredPosition) &&
               sizeDelta.Equals(other.sizeDelta) &&
               pivot.Equals(other.pivot) &&
               rotation.Equals(other.rotation);

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is RectPlacement other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
            => HashCode.Combine(anchorMin, anchorMax, anchoredPosition, sizeDelta, pivot, rotation);
    }
}
