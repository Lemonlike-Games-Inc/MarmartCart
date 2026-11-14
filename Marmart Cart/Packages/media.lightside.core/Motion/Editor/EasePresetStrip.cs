using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace LightSide
{
    /// <summary>
    /// A row of curve thumbnails, one per named preset; clicking one applies it and the thumbnail matching
    /// the current value carries the active accent.
    /// </summary>
    public sealed class EasePresetStrip : VisualElement
    {
        private const string ActiveClass = "lightside-ease-preset--active";
        private const int Samples = 20;

        /// <summary>The easings a timing curve is offered, in order.</summary>
        public static readonly EasePreset[] Easings =
        {
            new("Emphasized", Ease.Emphasized),
            new("Emphasized In", Ease.EmphasizedIn),
            new("Emphasized Out", Ease.EmphasizedOut),
            new("Standard In", Ease.StandardIn),
            new("Standard Out", Ease.StandardOut),
            new("Ease", Ease.Cubic(0.25f, 0.1f, 0.25f, 1f)),
            new("Ease In", Ease.Cubic(0.42f, 0f, 1f, 1f)),
            new("Ease Out", Ease.Cubic(0f, 0f, 0.58f, 1f)),
            new("Ease In Out", Ease.Cubic(0.42f, 0f, 0.58f, 1f)),
        };

        private readonly EasePreset[] presets;
        private readonly VisualElement[] thumbnails;

        /// <summary>Raised with the chosen preset's value.</summary>
        public event Action<Ease> Applied;

        /// <summary>Creates a strip of <paramref name="gallery"/>, or of the easings when it is <see langword="null"/>.</summary>
        public EasePresetStrip(EasePreset[] gallery = null)
        {
            presets = gallery ?? Easings;
            thumbnails = new VisualElement[presets.Length];
            AddToClassList("lightside-ease-presets");
            for (var i = 0; i < presets.Length; i++)
            {
                var preset = presets[i];
                var thumb = new VisualElement { tooltip = preset.Name };
                thumb.AddToClassList("lightside-ease-preset");
                thumb.generateVisualContent += context => DrawThumb(context, preset.Curve);
                thumb.RegisterCallback<PointerDownEvent>(evt =>
                {
                    Applied?.Invoke(preset.Curve);
                    evt.StopImmediatePropagation();
                });
                thumbnails[i] = thumb;
                Add(thumb);
            }
        }

        /// <summary>Marks the thumbnail whose preset equals <paramref name="current"/> as active.</summary>
        public void SetCurrent(in Ease current)
        {
            for (var i = 0; i < presets.Length; i++)
                thumbnails[i].EnableInClassList(ActiveClass, presets[i].Curve.Equals(current));
        }

        /// <summary>Strokes <paramref name="ease"/> across the element being painted — the thumbnail every gallery and picker draws.</summary>
        public static void DrawThumb(MeshGenerationContext context, Ease ease)
        {
            var rect = context.visualElement.contentRect;
            if (rect.width <= 8f || rect.height <= 8f) return;

            var inset = 4f;
            var left = rect.x + inset;
            var width = rect.width - inset * 2f;
            var bottom = rect.yMax - inset;
            var height = rect.height - inset * 2f;

            var painter = context.painter2D;
            painter.strokeColor = EditorResources.ToggleAccent;
            painter.lineWidth = 1.5f;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;
            painter.BeginPath();
            for (var i = 0; i <= Samples; i++)
            {
                var t = i / (float)Samples;
                var point = new Vector2(
                    left + width * t,
                    bottom - height * Mathf.Clamp(ease.Evaluate(t), -0.25f, 1.25f) * 0.8f - height * 0.1f);
                if (i == 0) painter.MoveTo(point);
                else painter.LineTo(point);
            }
            painter.Stroke();
        }
    }
}
