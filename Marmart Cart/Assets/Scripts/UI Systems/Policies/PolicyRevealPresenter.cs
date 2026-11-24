using UnityEngine;

/// <summary>
/// Shows the Policy reveal: one PolicyRevealView per active player, in that
/// player's Banner layer of ViewportCanvasSystem. Presentation only:
/// PolicyRevealStage owns the reveal and its timing.
/// </summary>
[DisallowMultipleComponent]
public sealed class PolicyRevealPresenter : MonoBehaviour
{
    private const int MaxViews = PlayerViewService.MaxViews;

    [Header("References")]
    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private PolicyRevealStage revealStage;

    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private ViewportCanvasSystem viewportCanvas;

    [SerializeField] private PolicyUIProfile profile;

    [Header("Collapse Target (prefab pixels of the banners, before their layout scale)")]
    [Tooltip("Where the first card shrinks to, from the banners' top-left corner.")]
    [SerializeField] private Vector2 collapseTarget = new Vector2(120f, 70f);

    [Tooltip("How much lower each next card shrinks to.")]
    [SerializeField] private float collapseStepY = 80f;

    private readonly PolicyRevealView[] views = new PolicyRevealView[MaxViews];
    private bool showing;

    private void Awake()
    {
        if (revealStage == null) revealStage = FindFirstObjectByType<PolicyRevealStage>();

        if (viewportCanvas == null)
            viewportCanvas = ViewportCanvasSystem.Instance != null
                ? ViewportCanvasSystem.Instance
                : FindFirstObjectByType<ViewportCanvasSystem>();

        if (profile == null || profile.RevealViewPrefab == null)
            Debug.LogError("[UI] PolicyRevealPresenter needs a PolicyUIProfile with a reveal view prefab. The reveal will not show.", this);
    }

    private void OnEnable()
    {
        if (revealStage != null)
        {
            revealStage.RevealStarted += HandleRevealStarted;
            revealStage.CardRevealed += HandleCardRevealed;
            revealStage.RevealEnded += HandleRevealEnded;
        }

        if (viewportCanvas != null) viewportCanvas.LayoutChanged += HandleLayoutChanged;
    }

    private void OnDisable()
    {
        if (revealStage != null)
        {
            revealStage.RevealStarted -= HandleRevealStarted;
            revealStage.CardRevealed -= HandleCardRevealed;
            revealStage.RevealEnded -= HandleRevealEnded;
        }

        if (viewportCanvas != null) viewportCanvas.LayoutChanged -= HandleLayoutChanged;

        showing = false;
        HideAll();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] != null) Destroy(views[i].gameObject);
        }
    }

    private void Update()
    {
        if (!showing || revealStage.Phase != PolicyRevealStage.RevealPhase.Collapsing) return;

        float progress = revealStage.CollapseProgress01;

        for (int i = 0; i < MaxViews; i++)
        {
            PolicyRevealView view = views[i];
            if (view == null || !view.gameObject.activeSelf) continue;

            PolicyBannerLayout banners = profile.GetBannerLayout(viewportCanvas.ActivePlayerCount);
            Vector2 target = new Vector2(
                banners.leftOffset + collapseTarget.x * banners.scale,
                banners.topOffset + collapseTarget.y * banners.scale);

            view.SetCollapse(progress, target, collapseStepY * banners.scale);
        }
    }

    private void HandleRevealStarted()
    {
        showing = true;
        EnsureViews();
        RefreshAll(true);
        Debug.Log($"[UI] Policy reveal shown | {revealStage.CardCount} card(s).", this);
    }

    private void HandleCardRevealed(int index, PolicyDefinition policy)
    {
        for (int i = 0; i < MaxViews; i++)
        {
            if (views[i] != null && views[i].gameObject.activeSelf) views[i].RevealCard(index, true);
        }
    }

    private void HandleRevealEnded()
    {
        if (!showing) return;

        showing = false;
        HideAll();
        Debug.Log("[UI] Policy reveal hidden.", this);
    }

    private void HandleLayoutChanged()
    {
        if (!showing) return;

        EnsureViews();
        RefreshAll(false);
    }

    private void EnsureViews()
    {
        if (viewportCanvas == null || profile == null || profile.RevealViewPrefab == null) return;

        for (int i = 0; i < MaxViews; i++)
        {
            if (views[i] != null) continue;

            if (!viewportCanvas.TryGetPlayerLayer(i + 1, ViewportCanvasSystem.PlayerLayer.Banner, out RectTransform layer))
                continue;

            PolicyRevealView view = Instantiate(profile.RevealViewPrefab, layer, false);
            view.name = $"P{i + 1} Policy Reveal";
            Stretch(view.RectTransform);
            view.gameObject.SetActive(false);
            views[i] = view;
        }
    }

    // fresh: the reveal just started, so the cards start face down; otherwise
    // the cards revealed so far show face up at once.
    private void RefreshAll(bool fresh)
    {
        for (int i = 0; i < MaxViews; i++)
        {
            PolicyRevealView view = views[i];
            if (view == null) continue;

            bool visible =
                showing &&
                viewportCanvas != null &&
                viewportCanvas.IsPlayerViewActive(i + 1);

            if (view.gameObject.activeSelf != visible) view.gameObject.SetActive(visible);
            if (!visible) continue;

            view.SetPolicies(revealStage.CardCount, revealStage);
            view.ApplyLayout(profile.GetRevealLayout(viewportCanvas.ActivePlayerCount));
            if (!fresh) view.ShowRevealed(revealStage.RevealedCount);
        }
    }

    private void HideAll()
    {
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] != null && views[i].gameObject.activeSelf) views[i].gameObject.SetActive(false);
        }
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
