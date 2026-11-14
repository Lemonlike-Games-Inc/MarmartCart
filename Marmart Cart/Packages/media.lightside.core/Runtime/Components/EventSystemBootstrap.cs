using UnityEngine;
using UnityEngine.EventSystems;

namespace LightSide
{
    /// <summary>
    /// Creates an EventSystem with the appropriate input module if none exists in the scene.
    /// Uses InputSystemUIInputModule when the new Input System is active, otherwise StandaloneInputModule.
    /// </summary>
    [AddComponentMenu(LightSideMenu.AddComponent.EventSystemBootstrap)]
    public class EventSystemBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            if (EventSystem.current != null || ObjectUtils.FindAny<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            AddInputModule(go);
        }

        /// <summary>
        /// Adds the input module matching the project's active input handling: the Input System
        /// package's <c>InputSystemUIInputModule</c> when that package is present,
        /// <see cref="StandaloneInputModule"/> otherwise.
        /// </summary>
        public static void AddInputModule(GameObject target)
        {
#if ENABLE_INPUT_SYSTEM
            var moduleType = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (moduleType != null)
            {
                target.AddComponent(moduleType);
                return;
            }
#endif
            target.AddComponent<StandaloneInputModule>();
        }
    }
}