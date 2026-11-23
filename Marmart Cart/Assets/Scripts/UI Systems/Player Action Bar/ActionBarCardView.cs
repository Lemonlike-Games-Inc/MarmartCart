using System;
using LightSide;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>How a card moves while its item is ready. A charging or empty card stays still.</summary>
public enum ActionBarCardMotion
{
    None,

    /// <summary>A shine sweeps across the card now and then.</summary>
    Shine,

    /// <summary>The rim cycles through the colours, plus the shine.</summary>
    Prismatic,

    /// <summary>A slow glow pulse in the rim colour.</summary>
    Pulse
}

/// <summary>Colours and motion of one kind of card: the Powerup, or one Sample tier.</summary>
[Serializable]
public struct ActionBarCardStyle
{
    public Color rim;

    [Tooltip("Body inside the rim. The body's gradient darkens it toward the bottom.")]
    public Color body;

    [Tooltip("Thin line just inside the rim. Alpha 0 hides it.")]
    public Color innerLine;

    public ActionBarCardMotion motion;

    public ActionBarCardStyle(Color rim, Color body, Color innerLine, ActionBarCardMotion motion)
    {
        this.rim = rim;
        this.body = body;
        this.innerLine = innerLine;
        this.motion = motion;
    }
}

/// <summary>Effect timings shared by every card of one bar. Set on the bar root.</summary>
[Serializable]
public sealed class ActionBarCardTimings
{
    [Tooltip("Alpha of the glow behind the equipped card once its flash settles.")]
    [Range(0f, 1f)] public float equippedGlowAlpha = 0.45f;

    [Tooltip("Seconds of the bright flash when a card becomes the equipped one.")]
    [Min(0f)] public float equipFlashSeconds = 0.25f;

    [Tooltip("Start scale of the equipped outline during the flash.")]
    [Min(1f)] public float equipFlashOutlineScale = 1.12f;

    [Min(0.1f)] public float shineEverySeconds = 2.6f;
    [Min(0.05f)] public float shineSweepSeconds = 0.45f;

    [Tooltip("Seconds for the Prismatic rim to go through every colour.")]
    [Min(0.1f)] public float prismaticCycleSeconds = 3f;

    [Range(0f, 1f)] public float prismaticSaturation = 0.55f;

    [Tooltip("Alpha of the Prismatic card's glow.")]
    [Range(0f, 1f)] public float prismaticGlowAlpha = 0.5f;

    [Min(0.1f)] public float pulseSeconds = 1.6f;
    [Range(0f, 1f)] public float pulseGlowAlpha = 0.75f;

    [Min(1f)] public float readyBurstScale = 1.6f;
    [Min(0.05f)] public float readyBurstSeconds = 0.4f;
}

/// <summary>
/// One card of the Player Action Bar (Powerup, Ability, later Emergency):
/// a rimmed card with an icon, a radial cooldown wipe with the seconds left,
/// the input prompts and an outline and glow while equipped, and its tier
/// motion while ready. It animates in unscaled time and turns its Update off
/// while idle. It only displays what PlayerActionBarView tells it.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class ActionBarCardView : MonoBehaviour
{
    private static readonly string[] SecondsTexts = BuildSecondsTexts(100);

    [Header("Parts")]
    [Tooltip("Fades the whole card while its slot is empty.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("Card border, in the style's rim colour.")]
    [SerializeField] private Image frame;

    [Tooltip("Card body inside the rim, in the style's body colour.")]
    [SerializeField] private Image body;

    [SerializeField] private Image innerLine;
    [SerializeField] private Image icon;

    [Tooltip("Dark radial wipe over the part of the charge still to fill.")]
    [SerializeField] private Image cooldownFill;

    [SerializeField] private UniText secondsLabel;

    [Tooltip("Parent of the prompt images. Shown only on the equipped card.")]
    [SerializeField] private GameObject promptRow;

    [SerializeField]
    private Image[] promptImages = new Image[PlayerActionBarProfile.PromptSlotCount];

    [Header("Effects")]
    [Tooltip("Ring around the equipped card.")]
    [SerializeField] private Image equippedOutline;

    [Tooltip("Soft glow behind the equipped card.")]
    [SerializeField] private Image equippedGlow;

    [Tooltip("Soft glow in the rim colour, for the Pulse and Prismatic motion.")]
    [SerializeField] private Image tierGlow;

    [Tooltip("Ring that bursts out once when the item becomes ready.")]
    [SerializeField] private Image readyBurst;

    [Tooltip("Stripe that sweeps across the card for the shine. Sits under a RectMask2D.")]
    [SerializeField] private RectTransform shine;

    [Header("Look")]
    [SerializeField] private Color readyIconColor = Color.white;
    [SerializeField] private Color chargingIconColor = new Color(1f, 1f, 1f, 0.35f);

    [Tooltip("Alpha of the whole card while its slot holds nothing.")]
    [FormerlySerializedAs("emptyFrameAlpha")]
    [Range(0f, 1f)]
    [SerializeField] private float emptyAlpha = 0.3f;

    private ActionBarCardTimings timings;
    private ActionBarCardMotion motion;
    private ActionBarCardMotion displayMotion;
    private Color displayRim = Color.white;
    private float oneShotLeft = -1f;
    private bool equipped;

    private float currentScale = 1f;
    private float targetScale = 1f;
    private float resizeSpeed;
    private float popElapsed = -1f;
    private float popDuration;
    private float popScale = 1f;
    private float flashElapsed = -1f;
    private float burstElapsed = -1f;
    private float motionTime;
    private int shownSeconds = -1;

    private void Awake()
    {
        currentScale = transform.localScale.x;
        targetScale = currentScale;

        // The Prefab Mode preview may have saved these on.
        if (shine != null) SetActive(shine.gameObject, false);
        if (tierGlow != null) SetActive(tierGlow.gameObject, false);
        if (readyBurst != null) SetActive(readyBurst.gameObject, false);

        enabled = false;
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;

        bool busy = UpdateScale(deltaTime);
        busy |= UpdateFlash(deltaTime);
        busy |= UpdateBurst(deltaTime);
        busy |= UpdateMotion(deltaTime);
        busy |= UpdateOneShot(deltaTime);

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

    /// <param name="empty">The slot holds nothing: the card shows faded.</param>
    /// <param name="charging">Dim the icon and show the cooldown wipe and seconds.</param>
    /// <param name="isEquipped">This card's item is the equipped one: prompts, outline and glow.</param>
    /// <param name="animate">Flash when it becomes equipped; otherwise show the settled look.</param>
    public void Display(
        in ActionBarCardStyle style,
        ActionBarCardTimings cardTimings,
        bool empty,
        bool charging,
        bool isEquipped,
        bool animate)
    {
        timings = cardTimings;

        bool showCharge = !empty && charging;
        bool ready = !empty && !charging;

        if (canvasGroup != null) canvasGroup.alpha = empty ? emptyAlpha : 1f;

        SetRimColor(style.rim);
        if (body != null) body.color = style.body;

        if (innerLine != null)
        {
            innerLine.color = style.innerLine;
            innerLine.enabled = style.innerLine.a > 0f;
        }

        if (icon != null)
        {
            icon.enabled = !empty && icon.sprite != null;
            icon.color = charging ? chargingIconColor : readyIconColor;
        }

        if (cooldownFill != null) SetActive(cooldownFill.gameObject, showCharge);
        if (secondsLabel != null) SetActive(secondsLabel.gameObject, showCharge);
        if (!showCharge) shownSeconds = -1;

        bool equippedNow = isEquipped && !empty;
        SetActive(promptRow, equippedNow);
        if (equippedOutline != null) SetActive(equippedOutline.gameObject, equippedNow);
        if (equippedGlow != null) SetActive(equippedGlow.gameObject, equippedNow);

        if (equippedNow && !equipped && animate && Application.isPlaying)
        {
            flashElapsed = 0f;
            ApplyEquippedLook(0f);
            Wake();
        }
        else if (flashElapsed < 0f)
        {
            ApplyEquippedLook(1f);
        }

        equipped = equippedNow;

        displayMotion = ready ? style.motion : ActionBarCardMotion.None;
        displayRim = style.rim;

        // A one-shot motion finishes first, then hands over to this motion.
        if (oneShotLeft < 0f) SetMotion(displayMotion, displayRim);
    }

    /// <summary>
    /// Plays one round of a tier motion (one shine sweep, or one glow pulse),
    /// whatever motion the card shows now, then returns to it. Play Mode only.
    /// </summary>
    public void PlayMotionOnce(ActionBarCardMotion oneShot)
    {
        if (oneShot == ActionBarCardMotion.None || timings == null || !Application.isPlaying) return;

        oneShotLeft = oneShot == ActionBarCardMotion.Pulse ? timings.pulseSeconds : timings.shineSweepSeconds;

        // Restart it, even if the card already shows this motion.
        motion = ActionBarCardMotion.None;
        SetMotion(oneShot, displayRim);
    }

    public void SetCharge(float progress01, float secondsLeft)
    {
        if (cooldownFill != null) cooldownFill.fillAmount = 1f - Mathf.Clamp01(progress01);

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

        if (currentScale != targetScale) Wake();
    }

    /// <summary>The ready moment: a quick swell to the given scale and back, and a ring burst.</summary>
    public void PlayReady(float scale, float seconds)
    {
        if (seconds > 0f && scale > 1f)
        {
            popScale = scale;
            popDuration = seconds;
            popElapsed = 0f;
        }

        if (readyBurst != null && timings != null)
        {
            burstElapsed = 0f;
            readyBurst.rectTransform.localScale = Vector3.one;
            SetAlpha(readyBurst, 1f);
            readyBurst.gameObject.SetActive(true);
        }

        Wake();
    }

    private void SetMotion(ActionBarCardMotion newMotion, Color rim)
    {
        bool changed = newMotion != motion;
        motion = newMotion;

        if (changed)
        {
            motionTime = 0f;
            if (shine != null) SetActive(shine.gameObject, false);
        }

        if (tierGlow != null)
        {
            bool glowing = motion == ActionBarCardMotion.Pulse || motion == ActionBarCardMotion.Prismatic;
            SetActive(tierGlow.gameObject, glowing);

            Color glow = rim;
            glow.a = changed ? 0f : tierGlow.color.a;
            tierGlow.color = glow;
        }

        if (motion == ActionBarCardMotion.None) return;

        if (Application.isPlaying)
        {
            Wake();
        }
        else
        {
            ShowMotionStill();
        }
    }

    // Prefab Mode preview: a frozen frame of the motion, so it can be judged and tuned.
    private void ShowMotionStill()
    {
        if (timings == null) return;

        if (motion != ActionBarCardMotion.Pulse && shine != null)
        {
            SetActive(shine.gameObject, true);
            shine.anchoredPosition = Vector2.zero;
        }

        if (tierGlow != null)
        {
            SetAlpha(tierGlow, motion == ActionBarCardMotion.Pulse ? timings.pulseGlowAlpha : timings.prismaticGlowAlpha);
        }
    }

    private bool UpdateScale(float deltaTime)
    {
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
        return busy;
    }

    private bool UpdateFlash(float deltaTime)
    {
        if (flashElapsed < 0f) return false;

        flashElapsed += deltaTime;

        float seconds = timings != null ? timings.equipFlashSeconds : 0f;
        float settle = seconds > 0f ? Mathf.Clamp01(flashElapsed / seconds) : 1f;

        ApplyEquippedLook(settle);

        if (settle < 1f) return true;

        flashElapsed = -1f;
        return false;
    }

    private void ApplyEquippedLook(float settle01)
    {
        float glowAlpha = timings != null ? timings.equippedGlowAlpha : 0.45f;
        float outlineScale = timings != null ? timings.equipFlashOutlineScale : 1f;

        if (equippedGlow != null) SetAlpha(equippedGlow, Mathf.Lerp(1f, glowAlpha, settle01));

        if (equippedOutline != null)
        {
            float scale = Mathf.Lerp(outlineScale, 1f, settle01);
            equippedOutline.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    private bool UpdateBurst(float deltaTime)
    {
        if (burstElapsed < 0f || readyBurst == null || timings == null) return false;

        burstElapsed += deltaTime;
        float t = burstElapsed / timings.readyBurstSeconds;

        if (t >= 1f)
        {
            burstElapsed = -1f;
            readyBurst.gameObject.SetActive(false);
            return false;
        }

        float scale = Mathf.Lerp(1f, timings.readyBurstScale, t);
        readyBurst.rectTransform.localScale = new Vector3(scale, scale, 1f);
        SetAlpha(readyBurst, 1f - t);
        return true;
    }

    private bool UpdateMotion(float deltaTime)
    {
        if (motion == ActionBarCardMotion.None || timings == null) return false;

        motionTime += deltaTime;

        switch (motion)
        {
            case ActionBarCardMotion.Shine:
                UpdateShine();
                break;

            case ActionBarCardMotion.Prismatic:
                float hue = Mathf.Repeat(motionTime / timings.prismaticCycleSeconds, 1f);
                Color rim = Color.HSVToRGB(hue, timings.prismaticSaturation, 1f);
                SetRimColor(rim);

                if (tierGlow != null)
                {
                    rim.a = timings.prismaticGlowAlpha;
                    tierGlow.color = rim;
                }

                UpdateShine();
                break;

            case ActionBarCardMotion.Pulse:
                if (tierGlow != null)
                {
                    float wave = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * motionTime / timings.pulseSeconds);
                    SetAlpha(tierGlow, timings.pulseGlowAlpha * wave);
                }
                break;
        }

        return true;
    }

    private bool UpdateOneShot(float deltaTime)
    {
        if (oneShotLeft < 0f) return false;

        oneShotLeft -= deltaTime;
        if (oneShotLeft > 0f) return true;

        oneShotLeft = -1f;
        SetRimColor(displayRim);
        SetMotion(displayMotion, displayRim);
        return false;
    }

    private void UpdateShine()
    {
        if (shine == null) return;

        float cycle = Mathf.Repeat(motionTime, timings.shineEverySeconds);
        bool sweeping = cycle < timings.shineSweepSeconds;

        SetActive(shine.gameObject, sweeping);
        if (!sweeping) return;

        float travel = ((RectTransform)shine.parent).rect.width * 0.5f + shine.rect.width;
        float x = Mathf.Lerp(-travel, travel, cycle / timings.shineSweepSeconds);
        shine.anchoredPosition = new Vector2(x, 0f);
    }

    private void SetRimColor(Color color)
    {
        if (frame != null) frame.color = color;

        if (readyBurst != null)
        {
            color.a = readyBurst.color.a;
            readyBurst.color = color;
        }
    }

    private void Wake()
    {
        if (Application.isPlaying) enabled = true;
    }

    private void ApplyScale(float multiplier)
    {
        float scale = currentScale * multiplier;
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active) target.SetActive(active);
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
