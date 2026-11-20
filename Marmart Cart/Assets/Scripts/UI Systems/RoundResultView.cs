using System;
using System.Text;
using LightSide;
using UnityEngine;

/// <summary>
/// Round and Match result banner in every active player's Banner layer, built
/// like RoundBannerView. See Docs/Design/ui-architecture.md.
///
/// Presentation only: MetaRoundController decides winners, Match Points and
/// ranks and raises RoundResultShown and MatchResultShown. Texts come from
/// RoundResultTextProfile; the look and layout live in the two label prefabs.
///
/// Per player view:
/// - Title: who won the Round, or this player's Match rank line.
/// - Detail: Match Points, with this player shown as "YOU".
/// </summary>
[DisallowMultipleComponent]
public sealed class RoundResultView : MonoBehaviour
{
    private const int MaxViews = PlayerViewService.MaxViews;

    [Header("References")]
    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private MetaRoundController roundController;

    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private ViewportCanvasSystem viewportCanvas;

    [Tooltip("All result texts and rank lines.")]
    [SerializeField] private RoundResultTextProfile texts;

    [Tooltip("Player labels (P1-P4) and colours.")]
    [SerializeField] private PlayerIdentityProfile playerIdentity;

    [Header("Label Prefabs")]
    [Tooltip("Big label: the Round winner, or this player's Match rank line.")]
    [SerializeField] private UniText titlePrefab;

    [Tooltip("Small label: Match Points.")]
    [SerializeField] private UniText detailPrefab;

    private readonly UniText[] titles = new UniText[MaxViews];
    private readonly UniText[] details = new UniText[MaxViews];
    private readonly string[] titleTexts = new string[MaxViews];
    private readonly string[] detailTexts = new string[MaxViews];
    private readonly StringBuilder builder = new StringBuilder(160);
    private readonly StringBuilder labelBuilder = new StringBuilder(32);
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

        if (texts == null || playerIdentity == null || titlePrefab == null || detailPrefab == null)
            Debug.LogError("[UI] RoundResultView is missing its texts, player identity or label prefabs. The result banner will not show.", this);
    }

    private void OnEnable()
    {
        if (roundController != null)
        {
            roundController.RoundResultShown += HandleRoundResultShown;
            roundController.MatchResultShown += HandleMatchResultShown;
        }

        if (viewportCanvas != null)
            viewportCanvas.LayoutChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (roundController != null)
        {
            roundController.RoundResultShown -= HandleRoundResultShown;
            roundController.MatchResultShown -= HandleMatchResultShown;
        }

        if (viewportCanvas != null)
            viewportCanvas.LayoutChanged -= Refresh;
    }

    #endregion

    #region Controller Events

    private void HandleRoundResultShown()
    {
        if (texts == null || playerIdentity == null) return;

        for (int i = 0; i < MaxViews; i++)
        {
            titleTexts[i] = BuildRoundTitle(i + 1);
            detailTexts[i] = BuildPointsLine(i + 1, texts.RoundPointsFormat);
        }

        visible = true;
        Refresh();
    }

    private void HandleMatchResultShown()
    {
        if (texts == null || playerIdentity == null) return;

        for (int i = 0; i < MaxViews; i++)
        {
            titleTexts[i] = BuildRankLine(i + 1);
            detailTexts[i] = BuildPointsLine(i + 1, texts.FinalPointsFormat);
        }

        visible = true;
        Refresh();
    }

    #endregion

    #region Texts

    private string BuildRoundTitle(int viewPlayer)
    {
        int round = roundController.RoundNumber;

        if (!roundController.RoundHasWinner)
            return SafeFormat(texts.NoRoundWinner, round);

        int winnerCount = 0;
        for (int p = 1; p <= roundController.ResultPlayerCount; p++)
        {
            if (roundController.IsRoundWinner(p)) winnerCount++;
        }

        if (roundController.IsRoundWinner(viewPlayer))
            return SafeFormat(winnerCount > 1 ? texts.YouShareRound : texts.YouWinRound, round);

        builder.Clear();
        for (int p = 1; p <= roundController.ResultPlayerCount; p++)
        {
            if (!roundController.IsRoundWinner(p)) continue;
            if (builder.Length > 0) builder.Append(texts.WinnerJoiner);
            AppendPlayerLabel(builder, p, playerIdentity.GetLabel(p));
        }

        return SafeFormat(winnerCount > 1 ? texts.OthersWinRound : texts.OtherWinsRound, round, builder.ToString());
    }

    private string BuildRankLine(int viewPlayer)
    {
        if (!roundController.MatchHasWinner)
            return texts.NoMatchWinnerLine;

        int rank = roundController.GetMatchRank(viewPlayer);

        int sameRankCount = 0;
        for (int p = 1; p <= roundController.ResultPlayerCount; p++)
        {
            if (roundController.GetMatchRank(p) == rank) sameRankCount++;
        }

        return texts.GetRankLine(rank, rank == 1 && sameRankCount > 1);
    }

    private string BuildPointsLine(int viewPlayer, string format)
    {
        builder.Clear();

        for (int p = 1; p <= roundController.ResultPlayerCount; p++)
        {
            if (builder.Length > 0) builder.Append(texts.PointsSeparator);

            string label = p == viewPlayer ? texts.YouLabel : playerIdentity.GetLabel(p);
            labelBuilder.Clear();
            AppendPlayerLabel(labelBuilder, p, label);
            builder.Append(SafeFormat(texts.PointsEntry, labelBuilder.ToString(), roundController.GetMatchPointsAfterRound(p)));
        }

        return SafeFormat(format, builder.ToString());
    }

    private void AppendPlayerLabel(StringBuilder target, int playerIndex, string label)
    {
        if (!texts.ColorPlayerLabels)
        {
            target.Append(label);
            return;
        }

        target.Append("<color=#")
            .Append(ColorUtility.ToHtmlStringRGB(playerIdentity.GetColor(playerIndex)))
            .Append('>')
            .Append(label)
            .Append("</color>");
    }

    // Texts are edited in the Inspector; a broken {} placeholder shows the raw text instead of throwing.
    private string SafeFormat(string format, params object[] args)
    {
        if (string.IsNullOrEmpty(format)) return string.Empty;

        try
        {
            return string.Format(format, args);
        }
        catch (FormatException)
        {
            Debug.LogWarning($"[UI] Result text has a broken {{}} placeholder: \"{format}\"", texts);
            return format;
        }
    }

    #endregion

    #region Labels

    // Makes every label match the current texts, visibility and player views.
    private void Refresh()
    {
        if (viewportCanvas == null || !viewportCanvas.IsReady) return;

        for (int i = 0; i < MaxViews; i++)
        {
            bool showHere = visible && viewportCanvas.IsPlayerViewActive(i + 1);

            UpdateLabel(titles, titlePrefab, i, showHere, titleTexts[i], "Round Result Title");
            UpdateLabel(details, detailPrefab, i, showHere, detailTexts[i], "Round Result Detail");
        }
    }

    private void UpdateLabel(UniText[] labels, UniText prefab, int slot, bool showHere, string text, string objectName)
    {
        UniText label = showHere ? GetOrCreateLabel(labels, prefab, slot, objectName) : labels[slot];
        if (label == null) return;

        if (showHere && label.Text != text)
            label.Text = text;

        label.IsVisible = showHere;
    }

    private UniText GetOrCreateLabel(UniText[] labels, UniText prefab, int slot, string objectName)
    {
        if (labels[slot] != null || prefab == null) return labels[slot];

        if (!viewportCanvas.TryGetPlayerLayer(slot + 1, ViewportCanvasSystem.PlayerLayer.Banner, out RectTransform layer))
            return null;

        UniText label = Instantiate(prefab, layer, false);
        label.name = $"P{slot + 1} {objectName}";
        labels[slot] = label;
        return label;
    }

    #endregion
}
