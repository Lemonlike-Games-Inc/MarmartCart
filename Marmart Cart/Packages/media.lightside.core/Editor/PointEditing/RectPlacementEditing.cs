using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LightSide
{
    /// <summary>
    /// The one placement being edited in the Scene view, and the seam that tells it where it stands. A drawer
    /// hands a <see cref="RectPlacement"/> property here and the handles appear; the package that owns the
    /// property answers <see cref="FrameResolver"/> with the rectangle it is placed inside and the world frame
    /// that rectangle lives in, so this stays ignorant of what a placement is placed in.
    /// </summary>
    /// <remarks>
    /// One placement is editable at a time: starting another ends the first. Editing ends by itself when the
    /// property stops resolving, its object is destroyed, or that object leaves the selection. Only the edited
    /// objects and the property path are held: the inspector owns and disposes the <see cref="SerializedObject"/>
    /// a drawer hands over, and editing outlives the inspector window.
    /// </remarks>
    [InitializeOnLoad]
    public static class RectPlacementEditing
    {
        /// <summary>
        /// Answers where <paramref name="placement"/> stands: <paramref name="parent"/> is the rectangle it
        /// resolves inside, in the coordinates <paramref name="localToWorld"/> maps to the world. Returns
        /// <see langword="false"/> for a property the resolver does not own.
        /// </summary>
        public delegate bool FrameResolver(SerializedProperty placement, out Rect parent,
            out Matrix4x4 localToWorld);

        private static readonly List<FrameResolver> resolvers = new();

        private static UnityEngine.Object[] targets;
        private static string propertyPath;
        private static SerializedObject state;
        private static SerializedPropertyBinding target;
        private static SceneEditSurface surface;
        private static bool gesturing;
        private static string gestureName;
        private static int undoGroup;

        static RectPlacementEditing() => SceneView.duringSceneGui += OnSceneGui;

        /// <summary>Raised when editing starts or stops, so a host can reflect it on its own control.</summary>
        public static event Action Changed;

        /// <summary>Adds a resolver for the placements one package owns. Later registrations answer first.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="resolver"/> is <see langword="null"/>.</exception>
        public static void RegisterResolver(FrameResolver resolver)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            if (!resolvers.Contains(resolver)) resolvers.Add(resolver);
        }

        /// <summary>Whether <paramref name="property"/> is the placement currently edited in the Scene view.</summary>
        public static bool IsEditing(SerializedProperty property)
            => targets is { Length: > 0 } && property != null &&
               propertyPath == property.propertyPath &&
               targets[0] == property.serializedObject.targetObject;

        /// <summary>Starts editing <paramref name="property"/>, or stops when it is already the edited one.</summary>
        public static void Toggle(SerializedProperty property)
        {
            if (IsEditing(property)) Stop();
            else Begin(property);
        }

        /// <summary>Hands the Scene view <paramref name="property"/> to edit, replacing whatever it held.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="property"/> is <see langword="null"/>.</exception>
        public static void Begin(SerializedProperty property)
        {
            if (property == null) throw new ArgumentNullException(nameof(property));
            RectPlacementHandles.Cancel();
            targets = property.serializedObject.targetObjects;
            propertyPath = property.propertyPath;
            state = null;
            target = null;
            gesturing = false;
            Changed?.Invoke();
            SceneView.RepaintAll();
        }

        /// <summary>Ends Scene-view editing.</summary>
        public static void Stop()
        {
            if (targets == null) return;
            RectPlacementHandles.Cancel();
            targets = null;
            propertyPath = null;
            state = null;
            target = null;
            gesturing = false;
            Changed?.Invoke();
            SceneView.RepaintAll();
        }

        private static void OnSceneGui(SceneView view)
        {
            if (targets is not { Length: > 0 }) return;

            var owner = targets[0];
            var go = owner as GameObject ?? (owner as Component)?.gameObject;
            if (owner == null || (go != null && !Selection.Contains(go)))
            {
                Stop();
                return;
            }

            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape &&
                !RectPlacementHandles.IsBusy)
            {
                Stop();
                e.Use();
                return;
            }

            if (state == null)
            {
                state = new SerializedObject(targets);
                var found = state.FindProperty(propertyPath);
                if (found == null)
                {
                    Stop();
                    return;
                }
                target = new SerializedPropertyBinding(found);
            }

            state.UpdateIfRequiredOrScript();
            var property = target.FindSerializedProperty();
            if (property == null || !Resolve(property, out var parent, out var frame)) return;
            if (target.Value is not RectPlacement placement) return;

            surface ??= new SceneEditSurface(Vector3.zero, Vector3.right, Vector3.up);
            surface.SetPlane(frame.MultiplyPoint3x4(Vector3.zero),
                frame.MultiplyVector(Vector3.right), frame.MultiplyVector(Vector3.up));

            var wasGesturing = gesturing;
            var edited = RectPlacementHandles.Draw(surface, parent, ref placement,
                EditorResources.ToggleAccent);
            gesturing = RectPlacementHandles.IsBusy;

            if (!wasGesturing && gesturing)
            {
                gestureName = RectPlacementHandles.GestureName;
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName(gestureName);
                undoGroup = Undo.GetCurrentGroup();
            }
            if (edited)
            {
                target.SetValue(placement, gestureName);
                view.Repaint();
            }
            if (wasGesturing && !gesturing) Undo.CollapseUndoOperations(undoGroup);
            if (e.type == EventType.MouseMove) HandleUtility.Repaint();
        }

        private static bool Resolve(SerializedProperty property, out Rect parent,
            out Matrix4x4 localToWorld)
        {
            for (var i = resolvers.Count - 1; i >= 0; i--)
                if (resolvers[i](property, out parent, out localToWorld)) return true;
            parent = default;
            localToWorld = Matrix4x4.identity;
            return false;
        }
    }
}
