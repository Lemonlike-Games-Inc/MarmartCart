using LightSide;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One card of the Player Action Bar (Powerup, Ability, later Emergency):
/// an icon in a frame, a cooldown shade with the seconds left, and the input
/// prompts while its item is the equipped one. It animates its own scale
/// (equipped size and the ready pop) in unscaled time and turns its Update off
/// while idle. It only displays what PlayerActionBarView tells it.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class ActionBarCardView : MonoBehaviour
{
    private static readonly string[] SecondsTexts = BuildSecondsTexts(100);

    [Header("Parts")]
    [Tooltip("Card border. The bar tints it: the Powerup colour, or the Sample's tier colour.")]
    [SerializeField] private Image frame;

    [SerializeField] private Image icon;

    [Tooltip(
        "Dark cover over the part of the charge still to fill. Keep its top anchor " +
        "at 1; its bottom anchor follows the charge."
    )]
    [SerializeField] private RectTransform cooldownShade;

    [SerializeField] private UniText secondsLabel;

    [Tooltip("Parent of the prompt images. Shown only on the equipped card.")]
    [SerializeField] private GameObject promptRow;

    [SerializeField]
    private Image[] promptImages = new Image[PlayerActionBarProfile.PromptSlotCount];

    [Header("Look")]
    [SerializeField] private Color readyIconColor = Color.white;
    [SerializeField] private Color chargingIconColor = new Color(1f, 1f, 1f, 0.35f);

    [Tooltip("Frame alpha while the slot holds nothing.")]
    [Range(0f, 1f)]
    [SerializeField] private float emptyFrameAlpha = 0.3f;

    private float currentScale = 1f;
    private float targetScale = 1f;
    private float resizeSpeed;
    private float popElapsed = -1f;
    private float popDuration;
    private float popScale = 1f;
    private int shownSeconds = -1;

    private void Awake()
    {
        currentScale = transform.localScale.x;
        targetScale = currentScale;
        enabled = false;
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;
        bool busy = false;

        if (currentScale != targetScale)
        {
            currentScale = resizeSpeed > 0f
                ? Mathf.MoveTowards(currentScale, targetScale, resizeSpeed * deltaTime)
                : targetScale;
            busy = currentScale != targetScale;
        }

        float popMultiplier = 1f;

        if (popElapsed >= 0f)
        {
            popElapsed += deltaTime;

            if (popElapsed < popDuration)
            {
                popMultiplier = 1f + (popScale - 1f) * Mathf.Sin(Mathf.PI * popElapsed / popDuration);
                busy = true;
            }
            else
            {
                popElapsed = -1f;
            }
        }

        ApplyScale(popMultiplier);

        if (!busy) enabled = false;
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
    }

    public void SetIcon(Sprite sprite)
    {
        if (icon != null) icon.sprite = sprite;
    }

    public void SetPrompt(int index, Sprite sprite)
    {
        if (promptImages == null || index < 0 || index >= promptImages.Length) return;

        Image image = promptImages[index];
        if (image == null) return;

        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    /// <param name="empty">The slot holds nothing: only a faint frame shows.</param>
    /// <param name="charging">Dim the icon and show the cooldown shade and seconds.</param>
    /// <param name="showPrompts">This card's item is the equipped one.</param>
    public void Display(Color frameColor, bool empty, bool charging, bool showPrompts)
    {
        bool showCharge = !empty && charging;

        if (frame != null)
        {
            if (empty) frameColor.a *= emptyFrameAlpha;
            frame.color = frameColor;
        }

        if (icon != null)
        {
            icon.enabled = !empty && icon.sprite != null;
            icon.color = charging ? chargingIconColor : readyIconColor;
        }

        if (cooldownShade != null && cooldownShade.gameObject.activeSelf != showCharge)
        {
            cooldownShade.gameObject.SetActive(showCharge);
        }

        if (secondsLabel != null && secondsLabel.gameObject.activeSelf != showCharge)
        {
            secondsLabel.gameObject.SetActive(showCharge);
        }

        if (!showCharge) shownSeconds = -1;

        bool promptsVisible = showPrompts && !empty;

        if (promptRow != null && promptRow.activeSelf != promptsVisible)
        {
            promptRow.SetActive(promptsVisible);
        }
    }

    public void SetCharge(float progress01, float secondsLeft)
    {
        if (cooldownShade != null)
        {
            Vector2 anchorMin = cooldownShade.anchorMin;
            anchorMin.y = Mathf.Clamp01(progress01);
            cooldownShade.anchorMin = anchorMin;
        }

        int seconds = Mathf.Clamp(Mathf.CeilToInt(secondsLeft), 0, SecondsTexts.Length - 1);

        if (secondsLabel == null || seconds == shownSeconds) return;

        shownSeconds = seconds;
        secondsLabel.Text = SecondsTexts[seconds];
    }

    /// <summary>Grows or shrinks to the given scale; instantly when not animated.</summary>
    public void SetTargetScale(float scale, float seconds, bool animate)
    {
        targetScale = scale;

        if (!animate || seconds <= 0f)
        {
            currentScale = scale;
            resizeSpeed = 0f;

            // A running pop applies the new size on its next frame.
            if (popElapsed < 0f) ApplyScale(1f);
            return;
        }

        resizeSpeed = Mathf.Abs(targetScale - currentScale) / seconds;

        if (currentScale != targetScale) enabled = true;
    }

    /// <summary>A quick swell up to the given scale and back.</summary>
    public void Pop(float scale, float seconds)
    {
        if (seconds <= 0f || scale <= 1f) return;

        popScale = scale;
        popDuration = seconds;
        popElapsed = 0f;
        enabled = true;
    }

    private void ApplyScale(float multiplier)
    {
        float scale = currentScale * multiplier;
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    private static string[] BuildSecondsTexts(int count)
    {
        string[] texts = new string[count];

        for (int i = 0; i < count; i++)
        {
            texts[i] = i.ToString();
        }

        return texts;
    }
}
