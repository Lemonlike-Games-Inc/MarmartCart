#if UNITY_EDITOR
using System.Threading;
using UnityEditor;
using UnityEngine;
#endif

namespace LightSide
{
    /// <summary>Ambient provenance of the state change being applied on the current thread.</summary>
    public static class StateAuthoring
    {
#if UNITY_EDITOR
        private static bool playing;

        [InitializeOnLoadMethod]
        private static void Capture()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Volatile.Write(ref playing, Application.isPlaying);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnPlayModeEntering() => Volatile.Write(ref playing, true);

        private static void OnPlayModeChanged(PlayModeStateChange _)
            => Volatile.Write(ref playing, Application.isPlaying);
#endif

        /// <summary>
        /// Whether the current change manipulates the authored document rather than live player
        /// state: any edit-mode change, or a play-mode change arriving through the editor's
        /// serialized ingress (Inspector, Undo/Redo, prefab operations). Constant false in
        /// players. Readable from any thread: play mode is a main-thread snapshot and the
        /// transaction is thread-local. Read it during the change itself — deferred work observes
        /// the value recorded at the change, not this property.
        /// </summary>
        public static bool IsActive =>
#if UNITY_EDITOR
            !Volatile.Read(ref playing) || StateOwnership.InEditorTransaction;
#else
            false;
#endif
    }
}
