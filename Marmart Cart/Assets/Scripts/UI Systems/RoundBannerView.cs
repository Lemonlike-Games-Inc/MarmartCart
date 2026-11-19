using LightSide;
using UnityEngine;

/// <summary>
/// Shows the pre-round banner ("ROUND 2", "3", "2", "1", "SHOP!") in every
/// active player's Banner layer. See Docs/Design/ui-architecture.md.
///
/// Presentation only: MetaRoundController owns the timing and raises the
/// events; this view only sets text and shows or hides one label per player.
/// The label look lives in the label prefab, so it can be restyled without code.
/// </summary>
[DisallowMultipleComponent]
public sealed class RoundBannerView : MonoBehaviour
{
    private const int MaxViews = PlayerViewService.MaxViews;

    [Header("References")]
    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private MetaRoundController roundController;

    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private ViewportCanvasSystem viewportCanvas;

    [Tooltip("Prefab with the UniText label. One copy goes into each player's Banner layer.")]
    [SerializeField] private UniText labelPrefab;

    [Header("Texts")]
    [Tooltip("{0} is the Round number.")]
    [SerializeField] private string roundTitleFormat = "ROUND {0}";
    [SerializeField] private string finalRoundTitle = "FINAL ROUND";
    [SerializeField] private string roundStartText = "SHOP!";

    private readonly UniText[] labels = new UniText[MaxViews];
    private string currentText;
    private bool visible;

    #region Unity

    private void Awake()
    {
        if (roundController == null)
            roundController = FindFirstObjectByType<MetaRoundController>();

        if (viewportCanvas == null)
            viewportCanvas = ViewportCanvasSystem.Instance != null
                ? ViewportCanvasSystem.Instance
                : FindFirstObjectByType<ViewportCanvasSystem>();

        if (labelPrefab == null)
            Debug.LogError("[UI] RoundBannerView has no label prefab. The banner will not show.", this);
    }

    private void OnEnable()
    {
        if (roundController != null)
        {
            roundController.RoundTitleShown += HandleRoundTitleShown;
            roundController.CountdownTicked += HandleCountdownTicked;
            roundController.RoundStarted += HandleRoundStarted;
            roundController.PreRoundFinished += HandlePreRoundFinished;
        }

        if (viewportCanvas != null)
            viewportCanvas.LayoutChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (roundController != null)
        {
            roundController.RoundTitleShown -= HandleRoundTitleShown;
            roundController.CountdownTicked -= HandleCountdownTicked;
            roundController.RoundStarted -= HandleRoundStarted;
            roundController.PreRoundFinished -= HandlePreRoundFinished;
        }

        if (viewportCanvas != null)
            viewportCanvas.LayoutChanged -= Refresh;
    }

    #endregion

    #region Controller Events

    private void HandleRoundTitleShown(int roundNumber, bool isFinalRound)
    {
        Show(isFinalRound ? finalRoundTitle : string.Format(roundTitleFormat, roundNumber));
    }

    private void HandleCountdownTicked(int value)
    {
        Show(value.ToString());
    }

    private void HandleRoundStarted()
    {
        Show(roundStartText);
    }

    private void HandlePreRoundFinished()
    {
        visible = false;
        Refresh();
    }

    #endregion

    #region Labels

    private void Show(string text)
    {
        currentText = text;
        visible = true;
        Refresh();
    }

    // Makes every label match the current text, visibility and player views.
    private void Refresh()
    {
        if (viewportCanvas == null || !viewportCanvas.IsReady) return;

        for (int i = 0; i < MaxViews; i++)
        {
            bool showHere = visible && viewportCanvas.IsPlayerViewActive(i + 1);
            UniText label = showHere ? GetOrCreateLabel(i) : labels[i];
            if (label == null) continue;

            if (showHere && label.Text != currentText)
                label.Text = currentText;

            label.IsVisible = showHere;
        }
    }

    private UniText GetOrCreateLabel(int slot)
    {
        if (labels[slot] != null || labelPrefab == null) return labels[slot];

        if (!viewportCanvas.TryGetPlayerLayer(slot + 1, ViewportCanvasSystem.PlayerLayer.Banner, out RectTransform layer))
            return null;

        UniText label = Instantiate(labelPrefab, layer, false);
        label.name = $"P{slot + 1} Round Banner";
        labels[slot] = label;
        return label;
    }

    #endregion
}
