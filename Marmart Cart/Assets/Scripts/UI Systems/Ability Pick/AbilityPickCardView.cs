using System;
using LightSide;
using UnityEngine;
using UnityEngine.UI;

/// <summary>How a pick card turns over. Shared by the three cards of one view; set on the view root.</summary>
[Serializable]
public sealed class AbilityPickRevealTimings
{
    [Tooltip("Seconds for the whole flip: the back turns edge-on, then the face turns back to full width.")]
    [Min(0f)] public float flipSeconds = 0.3f;

    [Tooltip("Extra height at the middle of the flip, so the card seems to lift off the table.")]
    [Min(0f)] public float flipLift = 0.1f;

    [Tooltip("Tilt in degrees at the middle of the flip.")]
    public float flipTiltDegrees = 5f;

    [Tooltip("Peak scale of the pop once the face is up. 1 turns the pop off.")]
    [Min(1f)] public float popScale = 1.15f;

    [Min(0.01f)] public float popSeconds = 0.25f;
}

/// <summary>
/// One card of the Ability pick: its back until it is revealed, then a large
/// face with the Sample's icon, name, short description and cooldown. The
/// face is an ActionBarCardView, so its rim, body, highlight outline and glow
/// match the Player Action Bar.
///
/// - Reveal: the card flips over, pops with a ring burst in the rim colour and
///   plays its tier motion once.
/// - The tier motion (shine, colour cycle, pulse) then runs only while the
///   card is highlighted or picked.
/// - Once the player has picked, the other cards fade and the picked one shows
///   the player's stamp.
///
/// Display only: AbilityPickView sets it. Animates in unscaled time.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class AbilityPickCardView : MonoBehaviour
{
    [Header("Parts")]
    [Tooltip("Fades the whole card when another card was picked.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("Shown until the card is revealed.")]
    [SerializeField] private GameObject back;

    [Tooltip("The face: rim, body, icon, highlight and tier motion, as on the Player Action Bar.")]
    [SerializeField] private ActionBarCardView face;

    [SerializeField] private UniText nameLabel;
    [SerializeField] private UniText descriptionLabel;
    [SerializeField] private UniText cooldownLabel;

    [Tooltip("On the picked card: the player's label on their colour.")]
    [SerializeField] private GameObject pickedStamp;

    [SerializeField] private Image pickedStampBackground;
    [SerializeField] private UniText pickedStampLabel;

    [Header("Look")]
    [Tooltip("{0} is the cooldown in whole seconds.")]
    [SerializeField] private string cooldownFormat = "Cooldown {0}s";

    [Tooltip("Alpha of a card that was not picked, once the player has picked.")]
    [Range(0f, 1f)]
    [SerializeField] private float fadedAlpha = 0.35f;

    private string stampText;
    private bool revealed;
    private ActionBarCardMotion tierMotion;

    private AbilityPickRevealTimings revealTimings;
    private float flipElapsed = -1f;
    private bool flipFaceShown;

    private void Awake()
    {
        enabled = false;
    }

    private void Update()
    {
        if (flipElapsed < 0f || revealTimings == null)
        {
            enabled = false;
            return;
        }

        flipElapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(flipElapsed / revealTimings.flipSeconds);

        // First half: the back narrows to edge-on. Second half: the face widens again.
        float width = t < 0.5f
            ? 1f - EaseIn(t * 2f)
            : EaseOut((t - 0.5f) * 2f);

        float arc = Mathf.Sin(Mathf.PI * t);
        float lift = 1f + revealTimings.flipLift * arc;

        transform.localScale = new Vector3(Mathf.Max(0.001f, width) * lift, lift, 1f);
        transform.localEulerAngles = new Vector3(0f, 0f, revealTimings.flipTiltDegrees * arc);

        if (!flipFaceShown && t >= 0.5f)
        {
            flipFaceShown = true;
            ShowFace(true);
        }

        if (t < 1f) return;

        flipElapsed = -1f;
        ResetPose();
        PlayRevealPop();
        enabled = false;
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
    }

    public void SetContent(Sprite icon, string title, string description, float cooldownSeconds)
    {
        if (face != null) face.SetIcon(icon);
        if (nameLabel != null) nameLabel.Text = title;
        if (descriptionLabel != null) descriptionLabel.Text = description;
        if (cooldownLabel != null) cooldownLabel.Text = string.Format(cooldownFormat, Mathf.CeilToInt(cooldownSeconds));
    }

    /// <summary>
    /// Face down, or face up. Turning face up flips the card when animated;
    /// turning face down is always instant (a new deal).
    /// </summary>
    public void SetRevealed(bool faceUp, bool animate, AbilityPickRevealTimings timings)
    {
        if (!faceUp)
        {
            revealed = false;
            flipElapsed = -1f;
            ResetPose();
            ShowFace(false);
            return;
        }

        if (revealed) return;
        revealed = true;

        if (!animate || !Application.isPlaying || timings == null)
        {
            ShowFace(true);
            return;
        }

        revealTimings = timings;

        if (timings.flipSeconds <= 0f)
        {
            ShowFace(true);
            PlayRevealPop();
            return;
        }

        flipElapsed = 0f;
        flipFaceShown = false;
        ShowFace(false);
        enabled = true;
    }

    /// <param name="highlighted">The player's current choice or pick: outline, glow, flash and the tier motion.</param>
    /// <param name="faded">The player picked another card.</param>
    public void Display(
        in ActionBarCardStyle style,
        ActionBarCardTimings timings,
        bool highlighted,
        bool faded,
        bool animate)
    {
        tierMotion = style.motion;

        ActionBarCardStyle shown = style;
        if (!highlighted) shown.motion = ActionBarCardMotion.None;

        if (face != null) face.Display(shown, timings, false, false, highlighted, animate);
        if (canvasGroup != null) canvasGroup.alpha = faded ? fadedAlpha : 1f;
    }

    public void SetHighlightScale(float scale, float seconds, bool animate)
    {
        if (face != null) face.SetTargetScale(scale, seconds, animate);
    }

    public void SetPicked(bool picked, string text, Color color)
    {
        SetActive(pickedStamp, picked);
        if (!picked) return;

        if (pickedStampBackground != null) pickedStampBackground.color = color;

        if (pickedStampLabel != null && text != stampText)
        {
            stampText = text;
            pickedStampLabel.Text = text;
        }
    }

    private void PlayRevealPop()
    {
        if (face == null || revealTimings == null) return;

        face.PlayReady(revealTimings.popScale, revealTimings.popSeconds);
        face.PlayMotionOnce(tierMotion);
    }

    private void ShowFace(bool faceUp)
    {
        SetActive(back, !faceUp);
        if (face != null) SetActive(face.gameObject, faceUp);
    }

    private void ResetPose()
    {
        transform.localScale = Vector3.one;
        transform.localEulerAngles = Vector3.zero;
    }

    private static float EaseIn(float t)
    {
        return t * t;
    }

    private static float EaseOut(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active) target.SetActive(active);
    }
}
