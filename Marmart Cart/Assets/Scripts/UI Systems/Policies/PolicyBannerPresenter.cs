using UnityEngine;

/// <summary>
/// Shows this Round's Policies as banner cards in the top-left of each active
/// player's view (Banner layer of ViewportCanvasSystem), from the reveal's
/// collapse. Compact while a later pre-round stage (the Ability pick) runs,
/// full during the Round title and countdown. When the Round starts they turn
/// compact and stay until it ends (Keep Compact During Round), or fade out.
/// Presentation only.
/// </summary>
[DisallowMultipleComponent]
public sealed class PolicyBannerPresenter : MonoBehaviour
{
    private const int MaxViews = PlayerViewService.MaxViews;

    [Header("References")]
    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private PolicyRevealStage revealStage;

    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private MetaRoundController roundController;

    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private ViewportCanvasSystem viewportCanvas;

    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private GameTimeManager gameTimeManager;

    [SerializeField] private PolicyUIProfile profile;

    [Header("In Round")]
    [Tooltip("On: the banners turn compact when the Round starts and stay until it ends. Off: they fade out when the Round starts.")]
    [SerializeField] private bool keepCompactDuringRound = true;

    private readonly PolicyBannerView[] views = new PolicyBannerView[MaxViews];
    private bool showing;
    private bool compact;

    private void Awake()
    {
        if (revealStage == null) revealStage = FindFirstObjectByType<PolicyRevealStage>();
        if (roundController == null) roundController = FindFirstObjectByType<MetaRoundController>();
        if (gameTimeManager == null) gameTimeManager = FindFirstObjectByType<GameTimeManager>();

        if (viewportCanvas == null)
            viewportCanvas = ViewportCanvasSystem.Instance != null
                ? ViewportCanvasSystem.Instance
                : FindFirstObjectByType<ViewportCanvasSystem>();

        if (profile == null || profile.BannerViewPrefab == null)
            Debug.LogError("[UI] PolicyBannerPresenter needs a PolicyUIProfile with a banner view prefab. The banners will not show.", this);
    }

    private void OnEnable()
    {
        if (revealStage != null) revealStage.CollapseStarted += HandleCollapseStarted;

        if (roundController != null)
        {
            roundController.PreRoundStageBegan += HandleStageBegan;
            roundController.PreRoundStagesEnded += HandleStagesEnded;
            roundController.RoundStarted += HandleRoundStarted;
            roundController.LoadStarted += HandleLoadStarted;
        }

        if (gameTimeManager != null) gameTimeManager.OnPostGameEntered += HandlePostGameEntered;
        if (viewportCanvas != null) viewportCanvas.LayoutChanged += HandleLayoutChanged;
    }

    private void OnDisable()
    {
        if (revealStage != null) revealStage.CollapseStarted -= HandleCollapseStarted;

        if (roundController != null)
        {
            roundController.PreRoundStageBegan -= HandleStageBegan;
            roundController.PreRoundStagesEnded -= HandleStagesEnded;
            roundController.RoundStarted -= HandleRoundStarted;
            roundController.LoadStarted -= HandleLoadStarted;
        }

        if (gameTimeManager != null) gameTimeManager.OnPostGameEntered -= HandlePostGameEntered;
        if (viewportCanvas != null) viewportCanvas.LayoutChanged -= HandleLayoutChanged;

        showing = false;
        HideAll(false);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] != null) Destroy(views[i].gameObject);
        }
    }

    private void HandleCollapseStarted()
    {
        showing = true;
        compact = false;
        EnsureViews();
        RefreshAll(true);
        Debug.Log("[UI] Policy banners shown.", this);
    }

    private void HandleStageBegan(PreRoundStage stage)
    {
        if (!showing || stage == revealStage) return;
        SetCompact(true);
    }

    private void HandleStagesEnded()
    {
        if (showing) SetCompact(false);
    }

    private void HandleRoundStarted()
    {
        if (!showing) return;

        if (keepCompactDuringRound)
        {
            SetCompact(true);
            Debug.Log("[UI] Policy banners compact for the Round.", this);
            return;
        }

        showing = false;
        HideAll(true);
        Debug.Log("[UI] Policy banners hidden: Round started.", this);
    }

    private void HandlePostGameEntered()
    {
        if (!showing) return;

        showing = false;
        HideAll(true);
        Debug.Log("[UI] Policy banners hidden: Round ended.", this);
    }

    private void HandleLoadStarted()
    {
        showing = false;
        HideAll(false);
    }

    private void HandleLayoutChanged()
    {
        if (!showing) return;

        EnsureViews();
        RefreshAll(false);
    }

    private void SetCompact(bool value)
    {
        compact = value;

        for (int i = 0; i < MaxViews; i++)
        {
            if (views[i] != null && views[i].gameObject.activeSelf) views[i].SetCompact(value, true);
        }
    }

    private void EnsureViews()
    {
        if (viewportCanvas == null || profile == null || profile.BannerViewPrefab == null) return;

        for (int i = 0; i < MaxViews; i++)
        {
            if (views[i] != null) continue;

            if (!viewportCanvas.TryGetPlayerLayer(i + 1, ViewportCanvasSystem.PlayerLayer.Banner, out RectTransform layer))
                continue;

            PolicyBannerView view = Instantiate(profile.BannerViewPrefab, layer, false);
            view.name = $"P{i + 1} Policy Banners";

            RectTransform rect = (RectTransform)view.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            view.gameObject.SetActive(false);
            views[i] = view;
        }
    }

    private void RefreshAll(bool animate)
    {
        for (int i = 0; i < MaxViews; i++)
        {
            PolicyBannerView view = views[i];
            if (view == null) continue;

            bool visible = showing && viewportCanvas != null && viewportCanvas.IsPlayerViewActive(i + 1);

            if (!visible)
            {
                view.Hide(false);
                continue;
            }

            view.SetPolicies(revealStage.CardCount, revealStage);
            view.ApplyLayout(profile.GetBannerLayout(viewportCanvas.ActivePlayerCount));
            view.SetCompact(compact, false);
            view.Show(animate);
        }
    }

    private void HideAll(bool animate)
    {
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] != null) views[i].Hide(animate);
        }
    }
}
