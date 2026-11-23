using LightSide;
using UnityEngine;

/// <summary>What one player's Ability pick shows. Filled by AbilityPickPresenter.</summary>
public struct AbilityPickDisplay
{
    /// <summary>Cards face up, counted from the left.</summary>
    public int RevealedCount;

    /// <summary>Every card is face up and choosing has started (or is over).</summary>
    public bool Choosing;

    /// <summary>The highlighted card, or -1.</summary>
    public int Highlight;

    /// <summary>This player has picked the highlighted card.</summary>
    public bool Locked;

    public bool AllPicked;
    public bool ShowTimer;
    public int TimerSeconds;
    public string PlayerLabel;
    public Color PlayerColor;
}

/// <summary>
/// One player's Ability pick, over their whole view: a dim backdrop, the
/// header ("FREE SAMPLES!", sub-line, timer), three cards spread evenly across
/// the view, and the input hints or status at the bottom. Cards flip over when
/// revealed; the tier motion plays only on the highlighted card. Lives on the root of
/// the Ability Pick prefab, which holds the look. It only displays what
/// AbilityPickPresenter gives it.
///
/// Pick a Preview State in Prefab Mode to see it with the Preview Samples.
/// The presenter sets everything again at runtime.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class AbilityPickView : MonoBehaviour
{
    public const int CardCount = AbilitySamplePickController.CardCount;

    private static readonly string[] TimerTexts = BuildNumberTexts(100);

    private static readonly ActionBarCardStyle FallbackStyle = new ActionBarCardStyle(
        Color.white, new Color(0.3f, 0.34f, 0.4f), Color.clear, ActionBarCardMotion.None);

    public enum PreviewState
    {
        CardBacks,
        Choosing,
        Picked,
        EveryonePicked
    }

    [Header("Parts")]
    [SerializeField] private RectTransform header;

    [Tooltip("Spans the view's width; the card slots sit at 1/6, 1/2 and 5/6 of it.")]
    [SerializeField] private RectTransform cardRow;

    [Tooltip("Parents of the cards. The layout scales these.")]
    [SerializeField] private RectTransform[] cardSlots = new RectTransform[CardCount];

    [SerializeField] private AbilityPickCardView[] cards = new AbilityPickCardView[CardCount];
    [SerializeField] private RectTransform footer;
    [SerializeField] private UniText timerLabel;

    [Tooltip("Input hints (stick or D-pad to choose, A to pick). Shown while the player can choose.")]
    [SerializeField] private GameObject hints;

    [Tooltip("Shown instead of the hints once the player has picked.")]
    [SerializeField] private UniText statusLabel;

    [Header("Cards")]
    [Tooltip("Card colours and motion per Sample tier. Shared with the Player Action Bar.")]
    [SerializeField] private AbilityTierStyleProfile tierStyles;

    [Tooltip("Card effect timings: highlight flash, shine, glow pulse, the ring burst of the reveal pop.")]
    [SerializeField] private ActionBarCardTimings cardTimings = new ActionBarCardTimings();

    [Tooltip("The flip and the pop when a card is revealed.")]
    [SerializeField] private AbilityPickRevealTimings revealTimings = new AbilityPickRevealTimings();

    [Tooltip("Scale of the highlighted card.")]
    [Min(1f)]
    [SerializeField] private float highlightScale = 1.1f;

    [Tooltip("Seconds to grow or shrink when the highlight moves.")]
    [Min(0f)]
    [SerializeField] private float resizeSeconds = 0.12f;

    [Header("Texts")]
    [SerializeField] private string waitingText = "Waiting for others...";
    [SerializeField] private string everyonePickedText = "Everyone picked!";

    [Tooltip("Stamp on the picked card. {0} is the player label, for example P1.")]
    [SerializeField] private string pickedFormat = "{0} PICKED";

    [Header("Editor Preview (Prefab Mode only)")]
    [SerializeField] private PreviewState previewState = PreviewState.Choosing;

    [Tooltip("Shown left to right. An empty entry hides that card.")]
    [SerializeField] private AbilitySampleDefinition[] previewSamples = new AbilitySampleDefinition[CardCount];

    [Tooltip("Item icons for Samples without their own Card Icon.")]
    [SerializeField] private PlayerActionBarProfile previewItemIcons;

    [Tooltip("Sizes the view like one player's view and applies that layout.")]
    [SerializeField] private AbilityPickProfile previewProfile;

    [SerializeField] private bool previewFourPlayer;

    [Range(0, CardCount - 1)]
    [SerializeField] private int previewHighlight = 1;

    [SerializeField] private Color previewPlayerColor = new Color(0.22f, 0.62f, 1f, 1f);

    private readonly AbilitySampleTier[] tiers = new AbilitySampleTier[CardCount];
    private readonly bool[] hasCard = new bool[CardCount];

    private RectTransform rectTransform;
    private int shownTimer = -1;
    private string shownPlayerLabel;
    private string stampText;

    public RectTransform RectTransform
    {
        get
        {
            if (rectTransform == null) rectTransform = (RectTransform)transform;
            return rectTransform;
        }
    }

    public void ApplyLayout(in AbilityPickLayout layout)
    {
        if (header != null)
        {
            header.anchoredPosition = new Vector2(0f, -layout.headerTopOffset);
            header.localScale = Uniform(layout.headerScale);
        }

        if (cardRow != null) cardRow.anchoredPosition = new Vector2(0f, layout.cardRowOffsetY);

        for (int i = 0; i < cardSlots.Length; i++)
        {
            if (cardSlots[i] != null) cardSlots[i].localScale = Uniform(layout.cardScale);
        }

        if (footer != null)
        {
            footer.anchoredPosition = new Vector2(0f, layout.footerBottomOffset);
            footer.localScale = Uniform(layout.footerScale);
        }
    }

    /// <summary>Content of one card. A null Sample hides the card.</summary>
    public void SetCard(int index, AbilitySampleDefinition sample, Sprite icon)
    {
        if (index < 0 || index >= CardCount) return;

        hasCard[index] = sample != null;

        AbilityPickCardView card = cards[index];
        if (card == null) return;

        card.SetVisible(sample != null);
        if (sample == null) return;

        tiers[index] = sample.Tier;
        card.SetContent(icon, sample.DisplayName, sample.Description, sample.CooldownSeconds);
    }

    public void Display(in AbilityPickDisplay display, bool animate)
    {
        if (display.PlayerLabel != shownPlayerLabel)
        {
            shownPlayerLabel = display.PlayerLabel;
            stampText = string.Format(pickedFormat, display.PlayerLabel);
        }

        for (int i = 0; i < CardCount; i++)
        {
            AbilityPickCardView card = cards[i];
            if (card == null || !hasCard[i]) continue;

            bool faceUp = i < display.RevealedCount;
            card.SetRevealed(faceUp, animate, revealTimings);

            if (!faceUp)
            {
                card.SetPicked(false, null, default);
                continue;
            }

            bool highlighted = display.Choosing && i == display.Highlight;
            bool picked = display.Locked && i == display.Highlight;

            card.Display(GetStyle(tiers[i]), cardTimings, highlighted, display.Locked && !picked, animate);
            card.SetHighlightScale(highlighted ? highlightScale : 1f, resizeSeconds, animate);
            card.SetPicked(picked, stampText, display.PlayerColor);
        }

        SetActive(hints, display.Choosing && !display.Locked);

        if (statusLabel != null)
        {
            string status = display.AllPicked ? everyonePickedText : display.Locked ? waitingText : null;
            SetActive(statusLabel.gameObject, status != null);
            if (status != null && statusLabel.Text != status) statusLabel.Text = status;
        }

        if (timerLabel != null)
        {
            SetActive(timerLabel.gameObject, display.ShowTimer);
            if (display.ShowTimer) SetTimer(display.TimerSeconds);
        }
    }

    public void SetTimer(int seconds)
    {
        if (timerLabel == null || seconds == shownTimer) return;

        shownTimer = seconds;
        timerLabel.Text = seconds >= 0 && seconds < TimerTexts.Length ? TimerTexts[seconds] : seconds.ToString();
    }

    private ActionBarCardStyle GetStyle(AbilitySampleTier tier)
    {
        return tierStyles != null ? tierStyles.Get(tier) : FallbackStyle;
    }

    private static Vector3 Uniform(float scale)
    {
        return new Vector3(scale, scale, 1f);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active) target.SetActive(active);
    }

    private static string[] BuildNumberTexts(int count)
    {
        string[] texts = new string[count];

        for (int i = 0; i < count; i++)
        {
            texts[i] = i.ToString();
        }

        return texts;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        UnityEditor.EditorApplication.delayCall -= ApplyPreview;
        UnityEditor.EditorApplication.delayCall += ApplyPreview;
    }

    private void ApplyPreview()
    {
        // Only in Prefab Mode or a scene; never on the prefab asset itself.
        if (this == null || Application.isPlaying || !gameObject.scene.IsValid()) return;

        if (previewProfile != null)
        {
            RectTransform rect = RectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = previewFourPlayer ? new Vector2(960f, 540f) : new Vector2(960f, 1080f);
            ApplyLayout(previewProfile.GetLayout(previewFourPlayer ? 4 : 2));
        }

        for (int i = 0; i < CardCount; i++)
        {
            AbilitySampleDefinition sample = previewSamples != null && i < previewSamples.Length ? previewSamples[i] : null;
            Sprite icon = null;

            if (sample != null)
            {
                icon = sample.CardIcon != null ? sample.CardIcon
                    : previewItemIcons != null ? previewItemIcons.GetIcon(sample.Item)
                    : null;
            }

            SetCard(i, sample, icon);
        }

        AbilityPickDisplay display = new AbilityPickDisplay
        {
            Highlight = previewHighlight,
            ShowTimer = true,
            TimerSeconds = 20,
            PlayerLabel = "P1",
            PlayerColor = previewPlayerColor
        };

        switch (previewState)
        {
            case PreviewState.Choosing:
                display.RevealedCount = CardCount;
                display.Choosing = true;
                display.TimerSeconds = 12;
                break;

            case PreviewState.Picked:
                display.RevealedCount = CardCount;
                display.Choosing = true;
                display.Locked = true;
                display.TimerSeconds = 8;
                break;

            case PreviewState.EveryonePicked:
                display.RevealedCount = CardCount;
                display.Choosing = true;
                display.Locked = true;
                display.AllPicked = true;
                display.ShowTimer = false;
                break;
        }

        Display(display, false);
    }
#endif
}
