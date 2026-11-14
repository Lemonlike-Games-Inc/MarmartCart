using UnityEngine.UIElements;

namespace LightSide
{
    /// <summary>
    /// Inspector rows for an <see cref="Ease"/> in a role its own drawer cannot infer. A host that draws its
    /// own body calls these directly, which is also the only way a curve nested behind a
    /// <c>[SerializeReference]</c> can be presented as anything but a timing curve.
    /// </summary>
    public static class EaseCurveRow
    {
        private const string CustomLabel = "Custom…";

        /// <summary>
        /// The row a profile curve gets: the named shapes a profile is usually one of, an entry for the one
        /// that matches none of them, and the pencil that opens the canvas. Nothing that belongs to a curve
        /// played over time is offered.
        /// </summary>
        public static VisualElement CreateProfile(SerializedPropertyContext context)
        {
            var gallery = EaseProfiles.All;
            var items = new Selector.SelectorItem[gallery.Length + 1];
            for (var i = 0; i < gallery.Length; i++)
                items[i] = new Selector.SelectorItem
                {
                    displayName = gallery[i].Name,
                    searchText = gallery[i].Name,
                    value = i,
                };
            items[gallery.Length] = new Selector.SelectorItem
            {
                displayName = CustomLabel,
                searchText = CustomLabel,
                value = gallery.Length,
            };

            var field = new SelectorField<int>(context.Label, gallery.Length, () => items);
            var pencil = new InspectorIconButton { tooltip = "Edit profile" };
            var row = InspectorVisuals.CreateFieldActionRow(field, pencil);

            void Open()
            {
                var editing = context.Binding.FindSerializedProperty();
                if (editing != null)
                    EaseCurvePopupWindow.Show(pencil.worldBound, editing, EaseCurveOptions.Profile);
            }

            void Refresh()
            {
                var current = context.Binding.FindSerializedProperty();
                if (current == null) return;
                var value = (Ease)current.boxedValue;
                var match = gallery.Length;
                for (var i = 0; i < gallery.Length; i++)
                    if (gallery[i].Curve.Equals(value))
                    {
                        match = i;
                        break;
                    }

                field.SetValueWithoutNotify(match);
                field.showMixedValue = context.Binding.HasMultipleValues;
                pencil.SetState(false, EditorResources.ToggleAccent, "edit");
            }

            pencil.clicked += Open;
            field.RegisterValueChangedCallback(evt =>
            {
                if ((uint)evt.newValue >= (uint)gallery.Length)
                {
                    Open();
                    return;
                }

                var chosen = gallery[evt.newValue].Curve;
                context.Edit(current => current.boxedValue = chosen, "Change Profile");
                Refresh();
            });

            return context.Observe(row, Refresh);
        }
    }
}
