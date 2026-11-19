using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the one canvas for all canvas UI. See Docs/Design/ui-architecture.md.
///
/// - Screen Space Overlay, scaled from 1920x1080 with the shared UI scale rule
///   (CanvasScaler Expand = min(width / 1920, height / 1080)).
/// - One root per player, matching that player's viewport from PlayerViewService
///   and clipped to it. Layers inside, bottom to top: Effects, Hud, Banner.
/// - One full-screen root above all players, for fades and staff/debug UI.
///
/// Every player root and the full-screen root is a nested canvas, so a change in
/// one player's UI does not rebuild the others.
/// </summary>
[DefaultExecutionOrder(-300)]
[DisallowMultipleComponent]
public sealed class ViewportCanvasSystem : MonoBehaviour
{
    public enum PlayerLayer
    {
        Effects,
        Hud,
        Banner
    }

    private const int MaxViews = PlayerViewService.MaxViews;
    private const int LayerCount = 3;

    [Header("References")]
    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private PlayerViewService viewService;

    [Header("Canvas")]
    [Tooltip("Sorting order of the runtime canvas among other overlay canvases.")]
    [SerializeField] private int sortingOrder = 500;

    [Header("Runtime - Read Only")]
    [SerializeField] private bool ready;
    [SerializeField] private int activePlayerCount;

    private Canvas rootCanvas;
    private RectTransform fullscreenRoot;
    private readonly RectTransform[] playerRoots = new RectTransform[MaxViews];
    private readonly RectTransform[,] layerRoots = new RectTransform[MaxViews, LayerCount];
    private PlayerViewService subscribedViewService;

    public static ViewportCanvasSystem Instance { get; private set; }

    /// <summary>Raised after the player roots match the current views.</summary>
    public event Action LayoutChanged;

    public bool IsReady => ready;
    public int ActivePlayerCount => activePlayerCount;
    public Canvas RootCanvas => rootCanvas;
    public RectTransform FullscreenRoot => fullscreenRoot;

    #region Unity

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[UI] A second ViewportCanvasSystem was found in the scene. The newest one is used.", this);
        }

        Instance = this;
        ResolveViewService();
        BuildHierarchy();
    }

    private void OnEnable()
    {
        ResolveViewService();

        if (subscribedViewService != viewService)
        {
            Unsubscribe();
            subscribedViewService = viewService;

            if (subscribedViewService != null)
                subscribedViewService.LayoutChanged += ApplyLayout;
        }

        if (viewService != null && viewService.IsConfigured)
            ApplyLayout();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    #endregion

    #region Queries

    public bool TryGetPlayerLayer(int playerIndex, PlayerLayer layer, out RectTransform layerRoot)
    {
        int slot = playerIndex - 1;

        layerRoot = slot >= 0 && slot < MaxViews
            ? layerRoots[slot, (int)layer]
            : null;

        return layerRoot != null;
    }

    public bool IsPlayerViewActive(int playerIndex)
    {
        return ready && viewService != null && viewService.IsViewActive(playerIndex);
    }

    #endregion

    #region Layout

    private void ApplyLayout()
    {
        if (viewService == null) return;

        activePlayerCount = viewService.ActivePlayerCount;

        for (int i = 0; i < MaxViews; i++)
        {
            RectTransform root = playerRoots[i];
            if (root == null) continue;

            bool active = viewService.TryGetViewportRect(i + 1, out Rect viewport);

            if (active)
            {
                root.anchorMin = viewport.min;
                root.anchorMax = viewport.max;
                root.offsetMin = Vector2.zero;
                root.offsetMax = Vector2.zero;
            }

            if (root.gameObject.activeSelf != active)
                root.gameObject.SetActive(active);
        }

        if (!ready)
        {
            ready = true;
            Debug.Log($"[UI] Viewport canvas ready | {activePlayerCount} players", this);
        }

        LayoutChanged?.Invoke();
    }

    private void BuildHierarchy()
    {
        if (rootCanvas != null) return;

        var canvasObject = new GameObject(
            "Viewport Canvas (Runtime)",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler)
        );

        canvasObject.transform.SetParent(transform, false);

        rootCanvas = canvasObject.GetComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = sortingOrder;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(PlayerViewService.ReferenceWidth, PlayerViewService.ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        var canvasRect = (RectTransform)canvasObject.transform;

        for (int i = 0; i < MaxViews; i++)
        {
            RectTransform root = CreateRoot($"P{i + 1} View", canvasRect);
            root.gameObject.AddComponent<Canvas>();
            root.gameObject.AddComponent<RectMask2D>();
            playerRoots[i] = root;

            // Creation order is draw order: Effects, then Hud, then Banner.
            layerRoots[i, (int)PlayerLayer.Effects] = CreateRoot("Effects", root);
            layerRoots[i, (int)PlayerLayer.Hud] = CreateRoot("HUD", root);
            layerRoots[i, (int)PlayerLayer.Banner] = CreateRoot("Banner", root);

            root.gameObject.SetActive(false);
        }

        fullscreenRoot = CreateRoot("Fullscreen", canvasRect);
        fullscreenRoot.gameObject.AddComponent<Canvas>();
    }

    private static RectTransform CreateRoot(string objectName, Transform parent)
    {
        var rootObject = new GameObject(objectName, typeof(RectTransform));
        var root = (RectTransform)rootObject.transform;

        root.SetParent(parent, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.localScale = Vector3.one;

        return root;
    }

    #endregion

    #region References

    private void ResolveViewService()
    {
        if (viewService != null) return;

        viewService = PlayerViewService.Instance != null
            ? PlayerViewService.Instance
            : FindFirstObjectByType<PlayerViewService>();
    }

    private void Unsubscribe()
    {
        if (subscribedViewService != null)
            subscribedViewService.LayoutChanged -= ApplyLayout;

        subscribedViewService = null;
    }

    #endregion
}
