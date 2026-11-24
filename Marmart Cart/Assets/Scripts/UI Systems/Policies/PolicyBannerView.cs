using LightSide;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One player's top-left Policy banners: the header ("THIS ROUND'S POLICIES")
/// and one horizontal card per Policy, stacked downward. Slides in when the
/// reveal collapses, turns compact while another pre-round stage runs, and
/// fades out when the Round starts. Lives on the root of the Policy Banners
/// prefab; it only displays what PolicyBannerPresenter gives it. Animates in
/// unscaled time and turns its Update off when settled.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class PolicyBannerView : MonoBehaviour
{
    public const int MaxCards = PolicyRevealStage.MaxCards;

    private static readonly ActionBarCardStyle FallbackStyle = new ActionBarCardStyle(
        Color.white, new Color(0.3f, 0.34f, 0.4f), Color.clear, ActionBarCardMotion.None);

    [Header("Parts")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("Top-left anchored; holds the header and the cards. The layout moves and scales it.")]
    [SerializeField] private RectTransform content;

    [SerializeField] private UniText headerLabel;

    [Tooltip("Soft dark backing behind the header text, so it reads over a bright floor. Sized to the text.")]
    [SerializeField] private RectTransform headerBacking;

    [SerializeField] private PolicyBannerCardView[] cards = new PolicyBannerCardView[MaxCards];

    [Header("Look")]
    [Tooltip("Card colours per tier. Shared with the Ability pick and the Player Action Bar.")]
    [SerializeField] private AbilityTierStyleProfile tierStyles;

    [SerializeField] private string headerText = "THIS ROUND'S POLICIES";

    [Tooltip("Space for the header above the first card, in prefab pixels.")]
    [Min(0f)]
    [SerializeField] private float headerHeight = 30f;

    [Tooltip("How far the header backing reaches past the text: sideways, and up and down.")]
    [SerializeField] private Vector2 headerBackingPadding = new Vector2(28f, 12f);

    [Tooltip("Gap between cards, in prefab pixels.")]
    [Min(0f)]
    [SerializeField] private float cardGap = 6f;

    [Header("Motion (real-time seconds)")]
    [Tooltip("How far the banners slide in from the left, in prefab pixels.")]
    [SerializeField] private float slideDistance = 60f;

    [Min(0.01f)]
    [SerializeField] private float showSeconds = 0.3f;

    [Min(0.01f)]
    [SerializeField] private float hideSeconds = 0.3f;

    [Min(0.01f)]
    [SerializeField] private float compactSeconds = 0.25f;

    private int cardCount;
    private float shown01;
    private bool shownTarget;
    private float compact01;
    private bool compactTarget;
    private Vector2 contentPosition;

    public bool IsShown => shownTarget;

    private void Awake()
    {
        if (headerLabel != null)
        {
            headerLabel.Text = headerText;
            FitHeaderBacking();
        }

        enabled = false;
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;

        shown01 = Mathf.MoveTowards(shown01, shownTarget ? 1f : 0f, deltaTime / (shownTarget ? showSeconds : hideSeconds));
        compact01 = Mathf.MoveTowards(compact01, compactTarget ? 1f : 0f, deltaTime / compactSeconds);

        Apply();

        bool settled = shown01 == (shownTarget ? 1f : 0f) && compact01 == (compactTarget ? 1f : 0f);
        if (!settled) return;

        enabled = false;
        if (!shownTarget) gameObject.SetActive(false);
    }

    public void ApplyLayout(in PolicyBannerLayout layout)
    {
        if (content == null) return;

        content.anchorMin = content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.localScale = new Vector3(layout.scale, layout.scale, 1f);
        contentPosition = new Vector2(layout.leftOffset, -layout.topOffset);
        Apply();
    }

    public void SetPolicies(int count, PolicyRevealStage stage)
    {
        cardCount = Mathf.Clamp(count, 0, MaxCards);

        for (int i = 0; i < cards.Length; i++)
        {
            PolicyBannerCardView card = cards[i];
            if (card == null) continue;

            bool used = i < cardCount;
            if (card.gameObject.activeSelf != used) card.gameObject.SetActive(used);
            if (!used) continue;

            PolicyDefinition policy = stage != null ? stage.GetPolicy(i) : null;
            ActionBarCardStyle style = policy != null && tierStyles != null ? tierStyles.Get(policy.Tier) : FallbackStyle;

            card.SetContent(
                policy != null ? policy.Icon : null,
                policy != null ? policy.DisplayName : string.Empty,
                policy != null ? policy.Description : string.Empty,
                style);
        }

        Apply();
    }

    public void Show(bool animate)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        shownTarget = true;
        if (!animate) shown01 = 1f;
        Wake();
    }

    public void Hide(bool animate)
    {
        shownTarget = false;

        if (!animate || !gameObject.activeSelf)
        {
            shown01 = 0f;
            enabled = false;
            if (gameObject.activeSelf) gameObject.SetActive(false);
            return;
        }

        Wake();
    }

    public void SetCompact(bool compact, bool animate)
    {
        compactTarget = compact;
        if (!animate) compact01 = compact ? 1f : 0f;
        Wake();
    }

    private void FitHeaderBacking()
    {
        if (headerBacking == null) return;

        ILayoutElement layout = headerLabel;
        layout.CalculateLayoutInputHorizontal();

        RectTransform headerRect = (RectTransform)headerLabel.transform;
        headerBacking.anchorMin = headerBacking.anchorMax = new Vector2(0f, 1f);
        headerBacking.pivot = new Vector2(0f, 1f);
        headerBacking.anchoredPosition = headerRect.anchoredPosition + new Vector2(-headerBackingPadding.x, headerBackingPadding.y);
        headerBacking.sizeDelta = new Vector2(
            Mathf.Ceil(layout.preferredWidth) + headerBackingPadding.x * 2f,
            headerRect.rect.height + headerBackingPadding.y * 2f);
    }

    private void Wake()
    {
        Apply();
        if (gameObject.activeInHierarchy) enabled = true;
    }

    private void Apply()
    {
        float eased = 1f - (1f - shown01) * (1f - shown01);
        if (canvasGroup != null) canvasGroup.alpha = eased;

        if (content != null)
            content.anchoredPosition = contentPosition + new Vector2(-(1f - eased) * slideDistance * content.localScale.x, 0f);

        float y = -headerHeight;

        for (int i = 0; i < cardCount && i < cards.Length; i++)
        {
            PolicyBannerCardView card = cards[i];
            if (card == null) continue;

            card.SetCompact(compact01);

            RectTransform rect = card.RectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);

            y -= card.GetHeight(compact01) + cardGap;
        }
    }
}
