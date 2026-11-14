using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LightSide
{
    /// <summary>
    /// The curve editor an <see cref="Ease"/> field opens: the editing canvas, a readout row with
    /// exact-coordinate fields, and — where the curve is played over time — a live motion preview above and
    /// a gallery below.
    /// </summary>
    internal sealed class EaseCurvePopupWindow : InspectorPopupWindow
    {
        private const float WindowWidth = 340f;
        private const float WindowHeight = 296f;
        private const float PreviewHeight = 29f;
        private const float PresetsHeight = 25f;

        [NonSerialized] private EaseCurveOptions options;
        [NonSerialized] private UnityEngine.Object[] targets;
        [NonSerialized] private string propertyPath;
        [NonSerialized] private SerializedObject serializedObject;
        [NonSerialized] private SerializedPropertyBinding binding;

        private EaseCurveField canvas;
        private EasePreviewStrip preview;
        private EasePresetStrip presets;
        private Label readout;
        private FloatField coordX;
        private FloatField coordY;

        /// <summary>Opens an editor bound to the supplied serialized ease on every selected target, presented as <paramref name="options"/> asks.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="property"/> is <see langword="null"/>.</exception>
        public static void Show(Rect anchor, SerializedProperty property, in EaseCurveOptions options)
        {
            if (property == null) throw new ArgumentNullException(nameof(property));
            var window = CreateInstance<EaseCurvePopupWindow>();
            window.options = options;
            window.targets = property.serializedObject.targetObjects;
            window.propertyPath = property.propertyPath;
            window.serializedObject = new SerializedObject(window.targets);
            window.binding = new SerializedPropertyBinding(
                InspectorHelpers.RequireProperty(window.serializedObject, window.propertyPath));
            var height = WindowHeight
                         - (options.ShowPreview ? 0f : PreviewHeight)
                         - (options.HasPresets ? 0f : PresetsHeight);
            window.ShowAtScreenRect(InspectorVisuals.CaptureScreenRect(anchor), WindowWidth, height);
        }

        /// <summary>Builds the retained-mode curve editor.</summary>
        public void CreateGUI()
        {
            if (!TryResolve(out var property))
            {
                Close();
                return;
            }

            var root = CreatePopupRoot("lightside-ease-popup");

            if (options.ShowPreview)
            {
                preview = new EasePreviewStrip();
                root.Add(preview);
            }

            canvas = new EaseCurveField(binding, options);
            root.Add(canvas);

            var footer = new VisualElement();
            footer.AddToClassList("lightside-ease-popup__footer");
            readout = new Label();
            readout.AddToClassList("lightside-ease-popup__readout");
            footer.Add(readout);
            footer.Add(CoordLabel("X"));
            coordX = Coord();
            footer.Add(coordX);
            footer.Add(CoordLabel("Y"));
            coordY = Coord();
            footer.Add(coordY);
            root.Add(footer);

            if (options.HasPresets)
            {
                presets = new EasePresetStrip(options.Presets);
                root.Add(presets);
                presets.Applied += ApplyPreset;
            }

            canvas.Changed += SyncChrome;
            coordX.RegisterValueChangedCallback(_ => CommitCoords());
            coordY.RegisterValueChangedCallback(_ => CommitCoords());
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnPanelKeyDown);
            root.TrackPropertyValue(property, _ => RefreshAll());
            RefreshAll();
        }

        private static Label CoordLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList("lightside-ease-popup__coord-label");
            return label;
        }

        private static FloatField Coord()
        {
            var field = new FloatField { isDelayed = true };
            field.AddToClassList("lightside-ease-popup__coord");
            return field;
        }

        private bool TryResolve(out SerializedProperty property)
        {
            property = null;
            if (targets == null || targets.Length == 0 ||
                string.IsNullOrEmpty(propertyPath)) return false;
            for (var i = 0; i < targets.Length; i++)
                if (targets[i] == null) return false;
            serializedObject ??= new SerializedObject(targets);
            serializedObject.UpdateIfRequiredOrScript();
            property = serializedObject.FindProperty(propertyPath);
            return property != null;
        }

        private void RefreshAll()
        {
            if (!TryResolve(out _))
            {
                Close();
                return;
            }
            canvas.Refresh();
            SyncChrome();
        }

        private void SyncChrome()
        {
            readout.text = canvas.ReadoutText;
            var current = canvas.Value;
            preview?.SetEase(in current);
            presets?.SetCurrent(in current);

            var solo = canvas.TryGetSoloPoint(out var position);
            coordX.SetEnabled(solo);
            coordY.SetEnabled(solo);
            if (solo && !canvas.IsBusy)
            {
                coordX.SetValueWithoutNotify(position.x);
                coordY.SetValueWithoutNotify(position.y);
            }
        }

        private void CommitCoords()
        {
            if (!canvas.TryGetSoloPoint(out _)) return;
            canvas.MoveSoloPoint(new Vector2(coordX.value, coordY.value));
        }

        private void ApplyPreset(Ease preset)
        {
            if (!TryResolve(out _)) return;
            binding.TransformValue(_ => preset, "Apply Curve Preset");
            RefreshAll();
            preview?.Replay();
        }

        private void OnPanelKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape || canvas.IsBusy) return;
            evt.StopImmediatePropagation();
            CloseToOwner();
        }
    }
}
