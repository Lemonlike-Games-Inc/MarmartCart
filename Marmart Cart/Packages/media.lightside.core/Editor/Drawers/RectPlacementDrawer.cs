using UnityEditor;
using UnityEngine.UIElements;

namespace LightSide
{
    /// <summary>
    /// Draws a <see cref="RectPlacement"/> as one list: every facet is opt-in behind the add action, and the
    /// pencil beside it hands editing to the Scene view. A resting rectangle costs one row wherever it nests.
    /// </summary>
    [CustomPropertyDrawer(typeof(RectPlacement))]
    internal sealed class RectPlacementDrawer : LightSidePropertyDrawer<RectPlacement>
    {
        /// <summary>Every facet of a placement, each hidden while it rests at the value it resolves without.</summary>
        private static readonly OptInField[] Fields = OptInParameters.SchemaFrom(
            RectPlacement.Fill,
            new[]
            {
                ("anchoredPosition", "Position"),
                ("sizeDelta", "Size"),
                ("anchorMin", "Anchor Min"),
                ("anchorMax", "Anchor Max"),
                ("pivot", "Pivot"),
                ("rotation", "Rotation"),
            });

        protected override VisualElement CreateToolkit(SerializedPropertyContext context)
        {
            var list = OptInParameters.CreateList(context.Property, Fields, context.Label, out var header);
            if (list == null) return InspectorVisuals.CreateStack();

            var pencil = new InspectorIconButton { tooltip = "Edit in the Scene view" };
            pencil.AddToClassList(InspectorFoldoutHeader.ActionButtonUssClassName);
            header.ActionRow.Insert(0, pencil);

            var binding = context.Binding;

            void Refresh() => pencil.SetState(
                RectPlacementEditing.IsEditing(binding.FindSerializedProperty()),
                EditorResources.ToggleAccent, "edit");

            pencil.clicked += () =>
            {
                var current = binding.FindSerializedProperty();
                if (current != null) RectPlacementEditing.Toggle(current);
                Refresh();
            };
            pencil.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                RectPlacementEditing.Changed += Refresh;
                Refresh();
            });
            pencil.RegisterCallback<DetachFromPanelEvent>(_ => RectPlacementEditing.Changed -= Refresh);
            return list;
        }
    }
}
