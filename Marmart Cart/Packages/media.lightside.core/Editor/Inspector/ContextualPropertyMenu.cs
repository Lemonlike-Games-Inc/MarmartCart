using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LightSide
{
    /// <summary>
    /// Carries the commands other packages register on <see cref="EditorApplication.contextualPropertyMenu"/>
    /// into a LightSide context menu, so a tool that follows Unity's property menus follows a LightSide field too.
    /// </summary>
    public static class ContextualPropertyMenu
    {
        private static readonly PropertyInfo items =
            typeof(GenericMenu).GetProperty("menuItems", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Type itemType =
            typeof(GenericMenu).GetNestedType("MenuItem", BindingFlags.NonPublic);
        private static readonly FieldInfo content = itemType?.GetField("content");
        private static readonly FieldInfo separator = itemType?.GetField("separator");
        private static readonly FieldInfo on = itemType?.GetField("on");
        private static readonly FieldInfo func = itemType?.GetField("func");
        private static readonly FieldInfo func2 = itemType?.GetField("func2");
        private static readonly FieldInfo userData = itemType?.GetField("userData");
        private static readonly bool readable = items != null && content != null && separator != null &&
                                                on != null && func != null && func2 != null && userData != null;
        private static bool reported;

        /// <summary>
        /// Appends, behind a separator, every command the registered callbacks add for
        /// <paramref name="property"/>; nothing when no package registered one or none applies. A command
        /// added without an action is omitted, as every unavailable command is.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="menu"/> or <paramref name="property"/> is <see langword="null"/>.</exception>
        public static void Append(DropdownMenu menu, SerializedProperty property)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));
            if (property == null) throw new ArgumentNullException(nameof(property));
            var callbacks = EditorApplication.contextualPropertyMenu;
            if (callbacks == null) return;
            if (!readable)
            {
                Report();
                return;
            }

            var native = new GenericMenu();
            callbacks(native, property);
            if (native.GetItemCount() == 0) return;

            menu.AppendSeparator();
            foreach (var item in (IEnumerable)items.GetValue(native))
            {
                var label = ((GUIContent)content.GetValue(item)).text;
                if ((bool)separator.GetValue(item))
                {
                    var group = label.TrimEnd('/');
                    menu.AppendSeparator(group.Length == 0 ? null : group);
                    continue;
                }

                var data = userData.GetValue(item);
                Action execute = null;
                if (func2.GetValue(item) is GenericMenu.MenuFunction2 withData) execute = () => withData(data);
                else if (func.GetValue(item) is GenericMenu.MenuFunction plain) execute = () => plain();
                if (execute == null) continue;

                var status = (bool)on.GetValue(item)
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal;
                menu.AppendAction(label, _ => execute(), _ => status);
            }
        }

        private static void Report()
        {
            if (reported) return;
            reported = true;
            Debug.LogError($"[LightSide] Unity {Application.unityVersion} changed the layout of GenericMenu; " +
                           "commands other packages add through EditorApplication.contextualPropertyMenu " +
                           "cannot be shown in LightSide context menus until the bridge is updated.");
        }
    }
}
