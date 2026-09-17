using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds one non-interactive full-screen Canvas with four camera-aligned
/// Tomato overlay slots. Only the viewport belonging to the struck player is
/// shown. Camera.rect is read continuously so 2P/4P layout changes remain
/// aligned without manually wiring four Canvases.
/// </summary>
[DisallowMultipleComponent]
public class TomatoBlindCanvasPresenter : MonoBehaviour
{
    private const int MaxPlayerSlots = PowerupRuntimeSystem.MaxPlayerSlots;

    [Header("References")]
    [SerializeField] private TomatoBlindEffectSystem blindEffectSystem;
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private TomatoPresentationProfile presentationProfile;

    [Tooltip(
        "Optional existing full-screen Screen Space - Overlay Canvas. When " +
        "empty, the presenter creates and owns a dedicated runtime Canvas."
    )]
    [SerializeField] private Canvas fullScreenCanvas;

    [Header("Auto-created Canvas")]
    [SerializeField] private int autoCreatedCanvasSortingOrder = 500;

    [Header("Diagnostics")]
    [SerializeField] private bool logMissingReferences = true;

    [Header("Runtime - Read Only")]
    [SerializeField] private bool usingAutoCreatedCanvas;
    [SerializeField] private int visibleViewportCount;

    private readonly Image[] viewportImages = new Image[MaxPlayerSlots];
    private readonly RectTransform[] viewportRects =
        new RectTransform[MaxPlayerSlots];
    private readonly TomatoBlindState[] displayedStates =
        new TomatoBlindState[MaxPlayerSlots];
    private readonly bool[] hasDisplayedState = new bool[MaxPlayerSlots];
    private readonly float[] visualStartedAtTime = new float[MaxPlayerSlots];

    private TomatoBlindEffectSystem subscribedBlindEffectSystem;
    private RectTransform generatedRoot;
    private Canvas ownedCanvas;
    private bool missingBlindSystemLogged;
    private bool missingCameraManagerLogged;
    private bool missingProfileLogged;
    private bool missingSpriteLogged;

    public int VisibleViewportCount => visibleViewportCount;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureCanvasAndSlots();
        HideAllViewports();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureSubscriptions();
        EnsureCanvasAndSlots();

        if (generatedRoot != null)
        {
            generatedRoot.gameObject.SetActive(true);
        }

        SynchronizeAllStates();
    }

    private void Start()
    {
        ResolveReferences();
        EnsureSubscriptions();
        EnsureCanvasAndSlots();
        SynchronizeAllStates();
    }

    private void Update()
    {
        if (blindEffectSystem == null || cameraManager == null)
        {
            ResolveReferences();
            EnsureSubscriptions();
        }

        EnsureCanvasAndSlots();
        UpdateViewports();
    }

    private void OnDisable()
    {
        Unsubscribe();
        HideAllViewports();

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

    private void HandleBlindStateChanged(
        int playerIndex,
        TomatoBlindState state)
    {
        int slotIndex = playerIndex - 1;

        if (slotIndex < 0 || slotIndex >= MaxPlayerSlots) return;

        bool beginsNewVisual =
            !hasDisplayedState[slotIndex] ||
            !displayedStates[slotIndex].Active ||
            displayedStates[slotIndex].EffectInstanceId !=
                state.EffectInstanceId;

        displayedStates[slotIndex] = state;
        hasDisplayedState[slotIndex] = state.Active;

        if (beginsNewVisual)
        {
            visualStartedAtTime[slotIndex] = Time.time;
        }
    }

    private void HandleBlindStateEnded(
        int playerIndex,
        TomatoBlindState state,
        PowerupEffectEndReason endReason)
    {
        int slotIndex = playerIndex - 1;

        if (slotIndex < 0 || slotIndex >= MaxPlayerSlots) return;

        displayedStates[slotIndex] = state;
        hasDisplayedState[slotIndex] = false;
        SetImageVisible(slotIndex, false, 0f);
    }

    private void UpdateViewports()
    {
        visibleViewportCount = 0;

        if (presentationProfile == null)
        {
            LogMissingProfileOnce();
            HideAllViewports();
            return;
        }

        missingProfileLogged = false;

        if (presentationProfile.BlindOverlaySprite == null)
        {
            if (logMissingReferences && !missingSpriteLogged)
            {
                missingSpriteLogged = true;
                Debug.LogWarning(
                    "[TomatoBlindCanvasPresenter] Assign the blind Sprite in " +
                    "TomatoPresentationProfile to display Tomato blindness.",
                    this
                );
            }

            HideAllViewports();
            return;
        }

        missingSpriteLogged = false;

        if (cameraManager == null)
        {
            if (logMissingReferences && !missingCameraManagerLogged)
            {
                missingCameraManagerLogged = true;
                Debug.LogWarning(
                    "[TomatoBlindCanvasPresenter] CameraManager was not found; " +
                    "viewport overlays cannot be aligned yet.",
                    this
                );
            }

            HideAllViewports();
            return;
        }

        missingCameraManagerLogged = false;

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            Camera gameplayCamera = cameraManager.GetOutputCamera(i + 1);
            UpdateViewportRect(i, gameplayCamera);

            bool cameraCanRender =
                gameplayCamera != null &&
                gameplayCamera.enabled &&
                gameplayCamera.gameObject.activeInHierarchy;

            if (!cameraCanRender ||
                !hasDisplayedState[i] ||
                !displayedStates[i].Active)
            {
                SetImageVisible(i, false, 0f);
                continue;
            }

            TomatoBlindState state = displayedStates[i];
            float remainingSeconds = state.GetRemainingSeconds(Time.time);

            if (remainingSeconds <= 0f)
            {
                SetImageVisible(i, false, 0f);
                continue;
            }

            float opacity = CalculateOpacity(
                i,
                state,
                remainingSeconds
            );

            SetImageVisible(i, opacity > 0.0001f, opacity);

            if (opacity > 0.0001f) visibleViewportCount++;
        }
    }

    private float CalculateOpacity(
        int slotIndex,
        TomatoBlindState state,
        float remainingSeconds)
    {
        float stackOpacity =
            presentationProfile.FirstHitOpacity +
            Mathf.Max(0, state.SplashCount - 1) *
            presentationProfile.AdditionalHitOpacity;

        stackOpacity = Mathf.Min(
            stackOpacity,
            presentationProfile.MaximumBlindOpacity
        );

        float fadeIn = presentationProfile.BlindFadeInSeconds <= 0f
            ? 1f
            : Mathf.Clamp01(
                (Time.time - visualStartedAtTime[slotIndex]) /
                presentationProfile.BlindFadeInSeconds
            );

        float fadeOut = presentationProfile.BlindFadeOutSeconds <= 0f
            ? 1f
            : Mathf.Clamp01(
                remainingSeconds /
                presentationProfile.BlindFadeOutSeconds
            );

        return Mathf.Clamp01(stackOpacity * Mathf.Min(fadeIn, fadeOut));
    }

    private void SetImageVisible(
        int slotIndex,
        bool visible,
        float opacity)
    {
        Image image = viewportImages[slotIndex];
        if (image == null) return;

        ApplyProfileToImage(image);

        Color tint = presentationProfile != null
            ? presentationProfile.BlindOverlayTint
            : Color.white;

        tint.a *= Mathf.Clamp01(opacity);
        image.color = tint;
        image.enabled = visible;
    }

    private void UpdateViewportRect(int slotIndex, Camera gameplayCamera)
    {
        RectTransform viewportRect = viewportRects[slotIndex];
        if (viewportRect == null || gameplayCamera == null) return;

        Rect cameraRect = gameplayCamera.rect;

        viewportRect.anchorMin = new Vector2(
            cameraRect.xMin,
            cameraRect.yMin
        );
        viewportRect.anchorMax = new Vector2(
            cameraRect.xMax,
            cameraRect.yMax
        );
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
    }

    private void EnsureCanvasAndSlots()
    {
        if (generatedRoot != null) return;

        Canvas hostCanvas = fullScreenCanvas;

        if (hostCanvas == null)
        {
            GameObject canvasObject = new GameObject(
                "Tomato Blind Canvas (Runtime)",
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
                    "[TomatoBlindCanvasPresenter] The assigned Canvas is not " +
                    "Screen Space - Overlay. Camera viewport anchors assume a " +
                    "full-screen overlay Canvas.",
                    hostCanvas
                );
            }
        }

        GameObject rootObject = new GameObject(
            "Tomato Blind Viewports",
            typeof(RectTransform)
        );

        generatedRoot = rootObject.GetComponent<RectTransform>();
        generatedRoot.SetParent(hostCanvas.transform, false);
        generatedRoot.anchorMin = Vector2.zero;
        generatedRoot.anchorMax = Vector2.one;
        generatedRoot.offsetMin = Vector2.zero;
        generatedRoot.offsetMax = Vector2.zero;
        generatedRoot.SetAsLastSibling();

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            GameObject viewportObject = new GameObject(
                $"P{i + 1} Tomato Blind",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

            RectTransform viewportRect =
                viewportObject.GetComponent<RectTransform>();
            viewportRect.SetParent(generatedRoot, false);
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            Image image = viewportObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.type = Image.Type.Simple;
            image.enabled = false;
            ApplyProfileToImage(image);

            viewportRects[i] = viewportRect;
            viewportImages[i] = image;
        }
    }

    private void ApplyProfileToImage(Image image)
    {
        if (image == null || presentationProfile == null) return;

        image.sprite = presentationProfile.BlindOverlaySprite;
        image.material = presentationProfile.BlindOverlayMaterial;
        image.preserveAspect = presentationProfile.PreserveBlindSpriteAspect;
    }

    private void SynchronizeAllStates()
    {
        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            int playerIndex = i + 1;

            if (blindEffectSystem != null &&
                blindEffectSystem.TryGetState(
                    playerIndex,
                    out TomatoBlindState state
                ))
            {
                HandleBlindStateChanged(playerIndex, state);
            }
            else
            {
                hasDisplayedState[i] = false;
                SetImageVisible(i, false, 0f);
            }
        }
    }

    private void HideAllViewports()
    {
        visibleViewportCount = 0;

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            SetImageVisible(i, false, 0f);
        }
    }

    private void ResolveReferences()
    {
        if (blindEffectSystem == null)
        {
            blindEffectSystem =
                FindFirstObjectByType<TomatoBlindEffectSystem>();
        }

        if (cameraManager == null)
        {
            cameraManager = FindFirstObjectByType<CameraManager>();
        }

        if (logMissingReferences &&
            blindEffectSystem == null &&
            !missingBlindSystemLogged)
        {
            missingBlindSystemLogged = true;
            Debug.LogWarning(
                "[TomatoBlindCanvasPresenter] TomatoBlindEffectSystem was not " +
                "found; waiting for the gameplay effect service.",
                this
            );
        }

        if (blindEffectSystem != null) missingBlindSystemLogged = false;
    }

    private void EnsureSubscriptions()
    {
        if (subscribedBlindEffectSystem == blindEffectSystem) return;

        Unsubscribe();
        subscribedBlindEffectSystem = blindEffectSystem;

        if (subscribedBlindEffectSystem != null)
        {
            subscribedBlindEffectSystem.OnBlindStateChanged +=
                HandleBlindStateChanged;
            subscribedBlindEffectSystem.OnBlindStateEnded +=
                HandleBlindStateEnded;
        }
    }

    private void Unsubscribe()
    {
        if (subscribedBlindEffectSystem != null)
        {
            subscribedBlindEffectSystem.OnBlindStateChanged -=
                HandleBlindStateChanged;
            subscribedBlindEffectSystem.OnBlindStateEnded -=
                HandleBlindStateEnded;
        }

        subscribedBlindEffectSystem = null;
    }

    private void LogMissingProfileOnce()
    {
        if (!logMissingReferences || missingProfileLogged) return;

        missingProfileLogged = true;
        Debug.LogWarning(
            "[TomatoBlindCanvasPresenter] Assign a " +
            "TomatoPresentationProfile.",
            this
        );
    }
}
