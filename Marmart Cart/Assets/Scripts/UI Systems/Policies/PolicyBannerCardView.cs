using LightSide;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One horizontal Policy banner card: tier rim, icon, name and description.
/// Compact (icon and name only, smaller) while another pre-round stage such
/// as the Ability pick fills the view. The name stays on one line; a name
/// wider than its space scrolls back and forth. Display only: PolicyBannerView
/// sets it. Sizes are in the prefab's own pixels, before the layout scale.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class PolicyBannerCardView : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] private Image shadow;

    [Tooltip("Card border, in the tier's rim colour.")]
    [SerializeField] private Image frame;

    [Tooltip("Card body inside the rim, in the tier's body colour.")]
    [SerializeField] private Image body;

    [SerializeField] private Image innerLine;
    [SerializeField] private Image icon;

    [Tooltip("Clips the name. Gets the name insets; the name scrolls inside it.")]
    [SerializeField] private RectTransform nameMask;

    [SerializeField] private UniText nameLabel;
    [SerializeField] private UniText descriptionLabel;

    [Header("Full")]
    [SerializeField] private Vector2 fullSize = new Vector2(420f, 96f);
    [SerializeField] private float fullIconSize = 72f;
    [SerializeField] private float fullNameFontSize = 26f;

    [Tooltip("Name area: bottom, right and top insets from the card edges. The left inset follows the icon.")]
    [SerializeField] private Vector3 fullNameEdges = new Vector3(54f, 12f, 8f);

    [Tooltip("Description area: bottom, right and top insets from the card edges. The left inset follows the icon.")]
    [SerializeField] private Vector3 descriptionEdges = new Vector3(8f, 12f, 42f);

    [Tooltip("Rim thickness.")]
    [Min(0f)]
    [SerializeField] private float fullRimWidth = 4f;

    [Tooltip("Pixels Per Unit Multiplier of the rounded sprites. Higher = smaller corners.")]
    [Min(0.01f)]
    [SerializeField] private float fullCornerScale = 2f;

    [Header("Compact")]
    [SerializeField] private Vector2 compactSize = new Vector2(160f, 44f);
    [SerializeField] private float compactIconSize = 32f;
    [SerializeField] private float compactNameFontSize = 12f;
    [SerializeField] private Vector3 compactNameEdges = new Vector3(4f, 8f, 4f);

    [Min(0f)]
    [SerializeField] private float compactRimWidth = 2.5f;

    [Min(0.01f)]
    [SerializeField] private float compactCornerScale = 3.5f;

    [Header("Layout")]
    [Tooltip("Gap from the card's left edge to the icon.")]
    [SerializeField] private float iconLeftInset = 12f;

    [Tooltip("Gap between the icon and the text.")]
    [SerializeField] private float iconTextGap = 10f;

    [Tooltip("Fills the icon slot while a Policy has no icon, so the slot reads as a placeholder. Alpha 0 leaves it empty.")]
    [SerializeField] private Color emptyIconColor = new Color(1f, 1f, 1f, 0.12f);

    [Tooltip("Gap between the rim and the inner line.")]
    [Min(0f)]
    [SerializeField] private float innerLineGap = 3f;

    [Tooltip("The inner line's ring sprite needs a higher multiplier than the rounded sprites for the same corner.")]
    [Min(0.01f)]
    [SerializeField] private float innerLineCornerRatio = 2.1f;

    [Tooltip("The shadow sits this far below the card.")]
    [SerializeField] private float shadowOffset = 4f;

    [Header("Name Scroll (real-time seconds)")]
    [Tooltip("Scroll speed of a name too wide for its space, in prefab pixels per second.")]
    [Min(1f)]
    [SerializeField] private float scrollSpeed = 40f;

    [Tooltip("Pause at each end of the scroll.")]
    [Min(0f)]
    [SerializeField] private float scrollHoldSeconds = 1.2f;

    private RectTransform rectTransform;
    private RectTransform iconRect;
    private RectTransform nameRect;
    private RectTransform descriptionRect;
    private Color descriptionColor = Color.white;

    private float nameWidth;
    private float scrollTime;
    private float nameFontSize = -1f;

    public RectTransform RectTransform
    {
        get
        {
            if (rectTransform == null) rectTransform = (RectTransform)transform;
            return rectTransform;
        }
    }

    /// <summary>The card's height at this compact amount, in prefab pixels.</summary>
    public float GetHeight(float compact01)
    {
        return Mathf.Lerp(fullSize.y, compactSize.y, compact01);
    }

    private void Awake()
    {
        CacheParts();
        enabled = false;
    }

    private void OnEnable()
    {
        scrollTime = 0f;
    }

    private void Update()
    {
        if (nameRect == null || nameMask == null)
        {
            enabled = false;
            return;
        }

        float overflow = nameWidth - nameMask.rect.width;

        if (overflow <= 0.5f)
        {
            nameRect.anchoredPosition = Vector2.zero;
            return;
        }

        // Hold, scroll to the end, hold, scroll back.
        float travel = overflow / scrollSpeed;
        float cycle = 2f * (scrollHoldSeconds + travel);
        scrollTime = (scrollTime + Time.unscaledDeltaTime) % cycle;

        float t = scrollTime;
        float offset;

        if (t < scrollHoldSeconds) offset = 0f;
        else if ((t -= scrollHoldSeconds) < travel) offset = t * scrollSpeed;
        else if ((t -= travel) < scrollHoldSeconds) offset = overflow;
        else offset = overflow - (t - scrollHoldSeconds) * scrollSpeed;

        nameRect.anchoredPosition = new Vector2(-Mathf.Clamp(offset, 0f, overflow), 0f);
    }

    public void SetContent(Sprite iconSprite, string title, string description, in ActionBarCardStyle style)
    {
        CacheParts();

        if (icon != null)
        {
            bool hasIcon = iconSprite != null;
            icon.sprite = iconSprite;
            icon.color = hasIcon ? Color.white : emptyIconColor;
            icon.enabled = hasIcon || emptyIconColor.a > 0f;
        }

        if (descriptionLabel != null) descriptionLabel.Text = description;

        if (nameLabel != null)
        {
            nameLabel.Text = title;
            MeasureName();
        }

        if (frame != null) frame.color = style.rim;
        if (body != null) body.color = style.body;

        if (innerLine != null)
        {
            innerLine.color = style.innerLine;
            innerLine.enabled = style.innerLine.a > 0f;
        }
    }

    /// <summary>0 = full, 1 = compact.</summary>
    public void SetCompact(float compact01)
    {
        CacheParts();

        float t = Mathf.Clamp01(compact01);
        RectTransform.sizeDelta = Vector2.Lerp(fullSize, compactSize, t);

        float rim = Mathf.Lerp(fullRimWidth, compactRimWidth, t);
        float cornerScale = Mathf.Lerp(fullCornerScale, compactCornerScale, t);

        SetSliced(frame, 0f, cornerScale);
        SetSliced(body, rim, cornerScale);
        SetSliced(innerLine, rim + innerLineGap, cornerScale * innerLineCornerRatio);

        if (shadow != null)
        {
            SetSliced(shadow, 0f, cornerScale);
            shadow.rectTransform.anchoredPosition = new Vector2(0f, -shadowOffset);
        }

        float iconSize = Mathf.Lerp(fullIconSize, compactIconSize, t);

        if (iconRect != null)
        {
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            iconRect.anchoredPosition = new Vector2(iconLeftInset, 0f);
        }

        // The icon slot is always kept, also while a Policy has no icon.
        float textLeft = iconLeftInset + iconSize + iconTextGap;
        float fullTextLeft = iconLeftInset + fullIconSize + iconTextGap;

        RectTransform nameArea = nameMask != null ? nameMask : nameRect;
        if (nameArea != null) SetInsets(nameArea, textLeft, Vector3.Lerp(fullNameEdges, compactNameEdges, t));

        // The name changes size halfway, while the card is mid-change.
        float fontSize = t < 0.5f ? fullNameFontSize : compactNameFontSize;

        if (nameLabel != null && !Mathf.Approximately(fontSize, nameFontSize))
        {
            nameFontSize = fontSize;
            nameLabel.FontSize = fontSize;
            MeasureName();
        }

        if (descriptionLabel != null)
        {
            // Gone in the first half of the change, so it never squeezes into the compact card.
            float alpha = Mathf.Clamp01(1f - t * 2f);
            SetActive(descriptionLabel.gameObject, alpha > 0f);

            Color color = descriptionColor;
            color.a *= alpha;
            descriptionLabel.color = color;
        }

        if (descriptionRect != null) SetInsets(descriptionRect, fullTextLeft, descriptionEdges);
    }

    // One line, as wide as the text, left-aligned in the mask. Update scrolls it when it is wider.
    private void MeasureName()
    {
        if (nameRect == null || nameMask == null) return;

        nameLabel.WordWrap = false;
        nameLabel.AutoSize = false;

        ILayoutElement layout = nameLabel;
        layout.CalculateLayoutInputHorizontal();
        nameWidth = Mathf.Ceil(layout.preferredWidth);

        nameRect.anchorMin = new Vector2(0f, 0f);
        nameRect.anchorMax = new Vector2(0f, 1f);
        nameRect.pivot = new Vector2(0f, 0.5f);
        nameRect.sizeDelta = new Vector2(nameWidth, 0f);
        nameRect.anchoredPosition = Vector2.zero;

        scrollTime = 0f;
        enabled = true;
    }

    private void CacheParts()
    {
        if (iconRect == null && icon != null) iconRect = icon.rectTransform;
        if (nameRect == null && nameLabel != null) nameRect = (RectTransform)nameLabel.transform;

        if (descriptionRect == null && descriptionLabel != null)
        {
            descriptionRect = (RectTransform)descriptionLabel.transform;
            descriptionColor = descriptionLabel.color;
        }
    }

    private static void SetSliced(Image image, float inset, float cornerScale)
    {
        if (image == null) return;

        image.pixelsPerUnitMultiplier = cornerScale;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    // edges: bottom, right, top.
    private static void SetInsets(RectTransform target, float left, Vector3 edges)
    {
        target.anchorMin = Vector2.zero;
        target.anchorMax = Vector2.one;
        target.offsetMin = new Vector2(left, edges.x);
        target.offsetMax = new Vector2(-edges.y, -edges.z);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active) target.SetActive(active);
    }
}
