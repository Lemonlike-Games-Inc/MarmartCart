using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns one full-screen Canvas and four camera-aligned viewport roots for all
/// power-up UI. Presentation modules request either the fullscreen-effect
/// layer or the HUD layer instead of creating additional Canvases.
///
/// Fullscreen effects render below HUD by design, so Tomato blindness can
/// obscure gameplay without hiding the player's held-power-up icon.
/// </summary>
[DefaultExecutionOrder(-300)]
[DisallowMultipleComponent]
public class PowerupViewportCanvasSystem : MonoBehaviour
{
    public enum ViewportLayer
    {
        FullscreenEffect,
        Hud
    }

    [Serializable]
    private sealed class ViewportSlot
    {
        [SerializeField] private int playerIndex;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private bool active;
        [SerializeField] private Rect normalizedRect;
        [SerializeField] private RectTransform viewportRoot;
        [SerializeField] private RectTransform fullscreenEffectRoot;
        [SerializeField] private RectTransform hudRoot;

        public int PlayerIndex
        {
            get => playerIndex;
            set => playerIndex = value;
        }

        public Camera GameplayCamera
        {
            get => gameplayCamera;
            set => gameplayCamera = value;
        }

        public bool Active
        {
            get => active;
            set => active = value;
        }

        public Rect NormalizedRect
        {
            get => normalizedRect;
            set => normalizedRect = value;
        }

        public RectTransform ViewportRoot
        {
            get => viewportRoot;
            set => viewportRoot = value;
        }

        public RectTransform FullscreenEffectRoot
        {
            get => fullscreenEffectRoot;
            set => fullscreenEffectRoot = value;
        }

        public RectTransform HudRoot
        {
            get => hudRoot;
            set => hudRoot = value;
        }
    }

    private const int MaxPlayerSlots = PowerupRuntimeSystem.MaxPlayerSlots;

    [Header("References")]
    [SerializeField] private CameraManager cameraManager;

    [Tooltip(
        "Optional existing full-screen Screen Space - Overlay Canvas. When " +
        "empty, this system creates and owns one runtime Canvas."
    )]
    [SerializeField] private Canvas fullScreenCanvas;

    [Header("Auto-created Canvas")]
    [SerializeField] private int autoCreatedCanvasSortingOrder = 500;

    [Header("Runtime Camera Binding")]
    [Tooltip(
        "Unscaled retry interval while CameraManager has not finished its 2P/4P " +
        "configuration. The retry routine stops once the layout is ready."
    )]
    [Min(0.02f)]
    [SerializeField] private float cameraBindingRetryInterval = 0.1f;

    [Tooltip("Log one warning after this long. Zero disables the warning.")]
    [Min(0f)]
    [SerializeField] private float cameraBindingWarningDelay = 3f;

    [Header("Runtime - Read Only")]
    [SerializeField] private bool usingAutoCreatedCanvas;
    [SerializeField] private bool layoutReady;
    [SerializeField] private int activePlayerCount;
    [SerializeField]
    private ViewportSlot[] slots =
        new ViewportSlot[MaxPlayerSlots];

    private RectTransform generatedRoot;
    private Canvas ownedCanvas;
    private Coroutine cameraBindingRoutine;
    private bool bindingWarningLogged;

    public Canvas HostCanvas =>
        ownedCanvas != null ? ownedCanvas : fullScreenCanvas;

    public bool LayoutReady => layoutReady;
    public int ActivePlayerCount => activePlayerCount;

    public event Action OnViewportLayoutRefreshed;

    private void Reset()
    {
        EnsureSlots();
        ResolveReferences();
    }

    private void Awake()
    {
        EnsureSlots();
        ResolveReferences();
        EnsureCanvasHierarchy();
        RefreshViewportLayout();
    }

    private void OnEnable()
    {
        EnsureSlots();
        ResolveReferences();
        EnsureCanvasHierarchy();

        if (generatedRoot != null)
        {
            generatedRoot.gameObject.SetActive(true);
        }

        RefreshViewportLayout();
        StartCameraBindingIfNeeded();
    }

    private void Start()
    {
        ResolveReferences();
        RefreshViewportLayout();
        StartCameraBindingIfNeeded();
    }

    private void OnDisable()
    {
        StopCameraBinding();

        if (generatedRoot != null)
        {
            generatedRoot.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (ownedCanvas != null)
        {
            Destroy(ownedCanvas.gameObject);
        }
        else if (generatedRoot != null)
        {
            Destroy(generatedRoot.gameObject);
        }

        generatedRoot = null;
        ownedCanvas = null;
    }

    private void OnValidate()
    {
        cameraBindingRetryInterval = Mathf.Max(
            0.02f,
            cameraBindingRetryInterval
        );
        cameraBindingWarningDelay = Mathf.Max(
            0f,
            cameraBindingWarningDelay
        );
        EnsureSlots();
    }

    /// <summary>
    /// Re-reads every physical output Camera and reapplies its normalized rect.
    /// Call this only if the match changes between 2P and 4P after startup.
    /// Normal scene startup is handled automatically.
    /// </summary>
    [ContextMenu("Refresh Viewport Layout")]
    public void RefreshViewportLayout()
    {
        EnsureSlots();
        EnsureCanvasHierarchy();
        ResolveReferences();

        int configuredCount = cameraManager != null
            ? cameraManager.ConfiguredPlayerCount
            : 0;

        bool hasConfiguredLayout =
            configuredCount == 2 || configuredCount == 4;

        int detectedActiveCount = 0;
        int configuredCameraCount = 0;

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            int playerIndex = i + 1;
            ViewportSlot slot = slots[i];
            Camera gameplayCamera = cameraManager != null
                ? cameraManager.GetOutputCamera(playerIndex)
                : null;

            slot.GameplayCamera = gameplayCamera;
            if (gameplayCamera != null &&
                (!hasConfiguredLayout || playerIndex <= configuredCount))
            {
                configuredCameraCount++;
            }

            bool cameraEnabled =
                gameplayCamera != null &&
                gameplayCamera.enabled &&
                gameplayCamera.gameObject.activeInHierarchy;

            bool active = hasConfiguredLayout
                ? playerIndex <= configuredCount && cameraEnabled
                : cameraEnabled;

            slot.Active = active;

            if (gameplayCamera != null)
            {
                Rect cameraRect = gameplayCamera.rect;
                slot.NormalizedRect = cameraRect;
                ApplyRect(slot.ViewportRoot, cameraRect);
            }

            if (slot.ViewportRoot != null)
            {
                slot.ViewportRoot.gameObject.SetActive(active);
            }

            if (active) detectedActiveCount++;
        }

        activePlayerCount = hasConfiguredLayout
            ? configuredCount
            : detectedActiveCount;

        layoutReady =
            cameraManager != null &&
            hasConfiguredLayout &&
            configuredCameraCount >= configuredCount;

        OnViewportLayoutRefreshed?.Invoke();
    }

    public bool TryGetLayerRoot(
        int playerIndex,
        ViewportLayer layer,
        out RectTransform layerRoot)
    {
        EnsureSlots();
        EnsureCanvasHierarchy();

        int slotIndex = playerIndex - 1;

        if (slotIndex < 0 || slotIndex >= slots.Length)
        {
            layerRoot = null;
            return false;
        }

        ViewportSlot slot = slots[slotIndex];
        layerRoot = layer == ViewportLayer.FullscreenEffect
            ? slot.FullscreenEffectRoot
            : slot.HudRoot;

        return layerRoot != null;
    }

    public bool IsViewportActive(int playerIndex)
    {
        int slotIndex = playerIndex - 1;
        return slotIndex >= 0 &&
               slotIndex < slots.Length &&
               slots[slotIndex].Active;
    }

    public Camera GetGameplayCamera(int playerIndex)
    {
        int slotIndex = playerIndex - 1;

        return slotIndex >= 0 && slotIndex < slots.Length
            ? slots[slotIndex].GameplayCamera
            : null;
    }

    private void EnsureCanvasHierarchy()
    {
        if (generatedRoot != null) return;

        Canvas hostCanvas = fullScreenCanvas;

        if (hostCanvas == null)
        {
            GameObject canvasObject = new GameObject(
                "Power-up Viewport Canvas (Runtime)",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler)
            );

            canvasObject.transform.SetParent(transform, false);
            ownedCanvas = canvasObject.GetComponent<Canvas>();
            ownedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            ownedCanvas.sortingOrder = autoCreatedCanvasSortingOrder;
            hostCanvas = ownedCanvas;
            usingAutoCreatedCanvas = true;
        }
        else
        {
            usingAutoCreatedCanvas = false;

            if (hostCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                Debug.LogWarning(
                    "[PowerupViewportCanvasSystem] The assigned Canvas is not " +
                    "Screen Space - Overlay. Camera.rect viewport alignment " +
                    "assumes a full-screen overlay Canvas.",
                    hostCanvas
                );
            }
        }

        GameObject rootObject = new GameObject(
            "Power-up Viewports",
            typeof(RectTransform)
        );

        generatedRoot = rootObject.GetComponent<RectTransform>();
        generatedRoot.SetParent(hostCanvas.transform, false);
        StretchToParent(generatedRoot);
        generatedRoot.SetAsLastSibling();

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            ViewportSlot slot = slots[i];
            int playerIndex = i + 1;

            slot.PlayerIndex = playerIndex;
            slot.ViewportRoot = CreateRectTransform(
                $"P{playerIndex} Power-up Viewport",
                generatedRoot
            );

            // Creation order defines render order inside each viewport.
            // Effects are intentionally below normal HUD.
            slot.FullscreenEffectRoot = CreateRectTransform(
                "Fullscreen Effects",
                slot.ViewportRoot
            );
            StretchToParent(slot.FullscreenEffectRoot);

            slot.HudRoot = CreateRectTransform(
                "HUD",
                slot.ViewportRoot
            );
            StretchToParent(slot.HudRoot);
            slot.HudRoot.SetAsLastSibling();

            slot.ViewportRoot.gameObject.SetActive(false);
        }
    }

    private void StartCameraBindingIfNeeded()
    {
        if (layoutReady || cameraBindingRoutine != null || !isActiveAndEnabled)
        {
            return;
        }

        cameraBindingRoutine = StartCoroutine(BindCameraLayoutRoutine());
    }

    private void StopCameraBinding()
    {
        if (cameraBindingRoutine == null) return;

        StopCoroutine(cameraBindingRoutine);
        cameraBindingRoutine = null;
    }

    private IEnumerator BindCameraLayoutRoutine()
    {
        float startedAt = Time.unscaledTime;

        while (isActiveAndEnabled && !layoutReady)
        {
            ResolveReferences();
            RefreshViewportLayout();

            if (layoutReady) break;

            if (!bindingWarningLogged &&
                cameraBindingWarningDelay > 0f &&
                Time.unscaledTime - startedAt >= cameraBindingWarningDelay)
            {
                bindingWarningLogged = true;
                Debug.LogWarning(
                    "[PowerupViewportCanvasSystem] Still waiting for " +
                    "CameraManager to finish its 2P/4P configuration. The " +
                    "system will continue retrying.",
                    this
                );
            }

            yield return new WaitForSecondsRealtime(
                cameraBindingRetryInterval
            );
        }

        cameraBindingRoutine = null;
    }

    private void ResolveReferences()
    {
        if (cameraManager == null)
        {
            cameraManager = FindFirstObjectByType<CameraManager>();
        }
    }

    private void EnsureSlots()
    {
        if (slots == null || slots.Length != MaxPlayerSlots)
        {
            Array.Resize(ref slots, MaxPlayerSlots);
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) slots[i] = new ViewportSlot();
            slots[i].PlayerIndex = i + 1;
        }
    }

    private static RectTransform CreateRectTransform(
        string objectName,
        Transform parent)
    {
        GameObject child = new GameObject(
            objectName,
            typeof(RectTransform)
        );

        RectTransform rectTransform =
            child.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        return rectTransform;
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        if (rectTransform == null) return;

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
    }

    private static void ApplyRect(
        RectTransform rectTransform,
        Rect normalizedRect)
    {
        if (rectTransform == null) return;

        rectTransform.anchorMin = new Vector2(
            normalizedRect.xMin,
            normalizedRect.yMin
        );
        rectTransform.anchorMax = new Vector2(
            normalizedRect.xMax,
            normalizedRect.yMax
        );
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
    }
}
