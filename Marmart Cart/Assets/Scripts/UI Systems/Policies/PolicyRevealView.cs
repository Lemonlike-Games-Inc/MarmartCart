using LightSide;
using UnityEngine;

/// <summary>
/// One player's Policy reveal, over their whole view: the header and one to
/// three vertical Policy cards in a centred row. At the end
/// the cards shrink toward the top-left banners and fade. Lives on the root
/// of the Policy Reveal prefab; it only displays what PolicyRevealPresenter
/// gives it. Animates in unscaled time.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class PolicyRevealView : MonoBehaviour
{
    public const int MaxCards = PolicyRevealStage.MaxCards;

    private static readonly ActionBarCardStyle FallbackStyle = new ActionBarCardStyle(
        Color.white, new Color(0.3f, 0.34f, 0.4f), Color.clear, ActionBarCardMotion.None);

    [Header("Parts")]
    [Tooltip("Fades the whole reveal during the collapse.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [SerializeField] private RectTransform header;
    [SerializeField] private UniText subtitleLabel;

    [Tooltip("Spans the view's width; the card slots are placed in it from the middle.")]
    [SerializeField] private RectTransform cardRow;

    [SerializeField] private RectTransform[] cardSlots = new RectTransform[MaxCards];
    [SerializeField] private PolicyCardView[] cards = new PolicyCardView[MaxCards];

    [Header("Cards")]
    [Tooltip("Card colours and motion per tier. Shared with the Ability pick and the Player Action Bar.")]
    [SerializeField] private AbilityTierStyleProfile tierStyles;

    [SerializeField] private ActionBarCardTimings cardTimings = new ActionBarCardTimings();
    [SerializeField] private PolicyCardRevealTimings revealTimings = new PolicyCardRevealTimings();

    [Header("Collapse")]
    [Tooltip("Card scale at the end of the collapse, times the layout's card scale.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float collapsedScale = 0.2f;

    [Header("Texts")]
    [SerializeField] private string oneSubtitle = "A new store policy this Round";
    [SerializeField] private string manySubtitle = "New store policies this Round";

    private readonly Vector3[] slotStart = new Vector3[MaxCards];
    private RectTransform rectTransform;
    private int cardCount;
    private float cardScale = 1f;

    public RectTransform RectTransform
    {
        get
        {
            if (rectTransform == null) rectTransform = (RectTransform)transform;
            return rectTransform;
        }
    }

    public void ApplyLayout(in PolicyRevealLayout layout)
    {
        if (header != null)
        {
            header.anchoredPosition = new Vector2(0f, -layout.headerTopOffset);
            header.localScale = new Vector3(layout.headerScale, layout.headerScale, 1f);
        }

        if (cardRow != null) cardRow.anchoredPosition = new Vector2(0f, layout.cardRowOffsetY);

        cardScale = layout.cardScale;

        for (int i = 0; i < cardSlots.Length && i < MaxCards; i++)
        {
            RectTransform slot = cardSlots[i];
            if (slot == null) continue;

            float x = (i - (cardCount - 1) * 0.5f) * layout.cardSpacing;
            slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 0.5f);
            slot.anchoredPosition = new Vector2(x, 0f);
            slot.localScale = new Vector3(cardScale, cardScale, 1f);
            slotStart[i] = slot.localPosition;
        }
    }

    /// <summary>Fills the cards face down. Call ApplyLayout after, so the row centres on this count.</summary>
    public void SetPolicies(int count, PolicyRevealStage stage)
    {
        cardCount = Mathf.Clamp(count, 0, MaxCards);

        if (subtitleLabel != null) subtitleLabel.Text = cardCount > 1 ? manySubtitle : oneSubtitle;
        if (canvasGroup != null) canvasGroup.alpha = 1f;

        for (int i = 0; i < MaxCards; i++)
        {
            bool used = i < cardCount;
            if (i < cardSlots.Length && cardSlots[i] != null) SetActive(cardSlots[i].gameObject, used);

            PolicyCardView card = i < cards.Length ? cards[i] : null;
            if (card == null || !used) continue;

            PolicyDefinition policy = stage != null ? stage.GetPolicy(i) : null;
            ActionBarCardStyle style = policy != null && tierStyles != null ? tierStyles.Get(policy.Tier) : FallbackStyle;

            card.SetContent(
                policy != null ? policy.Icon : null,
                policy != null ? policy.DisplayName : string.Empty,
                policy != null ? policy.Description : string.Empty);

            card.SetRevealed(false, false, null);
            card.SetStyle(style, cardTimings);
        }
    }

    public void RevealCard(int index, bool animate)
    {
        if (index < 0 || index >= cardCount || index >= cards.Length || cards[index] == null) return;
        cards[index].SetRevealed(true, animate, revealTimings);
    }

    /// <summary>Shows every card up to this count face up at once (a layout change mid-reveal).</summary>
    public void ShowRevealed(int revealedCount)
    {
        for (int i = 0; i < cardCount && i < cards.Length; i++)
        {
            if (cards[i] != null) cards[i].SetRevealed(i < revealedCount, false, null);
        }
    }

    /// <summary>
    /// 0 = cards in the row, 1 = shrunk onto the banner spot and faded out.
    /// <paramref name="targetFromTopLeft"/> is where the first card goes, from the view's top-left corner;
    /// each next card goes <paramref name="stepY"/> lower.
    /// </summary>
    public void SetCollapse(float progress01, Vector2 targetFromTopLeft, float stepY)
    {
        float t = progress01 * progress01 * (3f - 2f * progress01);
        if (canvasGroup != null) canvasGroup.alpha = 1f - progress01 * progress01;
        if (cardRow == null) return;

        Rect view = RectTransform.rect;
        Vector3 rowPosition = cardRow.localPosition;
        float endScale = cardScale * collapsedScale;

        for (int i = 0; i < cardCount && i < cardSlots.Length; i++)
        {
            RectTransform slot = cardSlots[i];
            if (slot == null) continue;

            Vector3 target = new Vector3(
                view.xMin + targetFromTopLeft.x,
                view.yMax - targetFromTopLeft.y - stepY * i,
                0f) - rowPosition;

            slot.localPosition = Vector3.LerpUnclamped(slotStart[i], target, t);

            float scale = Mathf.LerpUnclamped(cardScale, endScale, t);
            slot.localScale = new Vector3(scale, scale, 1f);
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active) target.SetActive(active);
    }
}
