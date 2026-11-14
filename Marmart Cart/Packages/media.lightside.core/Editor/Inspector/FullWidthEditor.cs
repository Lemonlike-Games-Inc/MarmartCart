using System;
using System.Reflection;
using UnityEditor;

namespace LightSide
{
    /// <summary>Base class for inspectors that own their full-width layout; the enable lifecycle arrives through <see cref="OnEnabled"/> and <see cref="OnDisabled"/>, never through Unity's messages.</summary>
    public abstract class FullWidthEditor : Editor
    {
        /// <inheritdoc/>
        public sealed override bool UseDefaultMargins() => false;

        private void OnEnable()
        {
            foreach (var candidate in targets)
                if (candidate == null) return;
            OnEnabled();
        }

        private void OnDisable() => OnDisabled();

        /// <summary>Runs in place of OnEnable, only while every target is alive: Unity re-enables an editor after a domain reload even when its targets were destroyed, and such an editor skips this hook until the Inspector discards it.</summary>
        protected virtual void OnEnabled() { }

        /// <summary>Runs in place of OnDisable, whether or not the targets are still alive.</summary>
        protected virtual void OnDisabled() { }
    }

    /// <summary>Marks a custom inspector whose product behavior intentionally requires one selected object.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class SingleObjectEditorAttribute : Attribute
    {
    }

    internal static class EditorArchitectureGuard
    {
        private static readonly string[] ownedMessages = { "OnEnable", "OnDisable" };

        [InitializeOnLoadMethod]
        private static void ValidateCustomEditors()
        {
            foreach (var type in TypeCache.GetTypesWithAttribute<CustomEditor>())
            {
                var assemblyName = type.Assembly.GetName().Name;
                if (type.IsAbstract || assemblyName == null ||
                    !assemblyName.StartsWith("LightSide.", StringComparison.Ordinal)) continue;
                if (!Attribute.IsDefined(type, typeof(CanEditMultipleObjects), false) &&
                    !Attribute.IsDefined(type, typeof(SingleObjectEditorAttribute), false))
                    throw new InvalidOperationException(
                        $"{type.FullName} must declare [CanEditMultipleObjects] or [SingleObjectEditor].");
                if (typeof(FullWidthEditor).IsAssignableFrom(type)) ValidateOwnedMessages(type);
            }
        }

        private static void ValidateOwnedMessages(Type editor)
        {
            const BindingFlags declared = BindingFlags.Instance | BindingFlags.Public |
                                          BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (var type = editor; type != typeof(FullWidthEditor); type = type.BaseType)
                foreach (var message in ownedMessages)
                    if (type.GetMethod(message, declared) != null)
                        throw new InvalidOperationException(
                            $"{type.FullName} must not declare {message}(); override FullWidthEditor.{message}d() instead.");
        }
    }
}
