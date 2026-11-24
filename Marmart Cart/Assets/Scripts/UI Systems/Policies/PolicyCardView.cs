using System;
using LightSide;
using UnityEngine;

/// <summary>How a Policy card turns over. Shared by the cards of one reveal; set on the view root.</summary>
[Serializable]
public sealed class PolicyCardRevealTimings
{
    [Tooltip("Seconds for the whole flip: the back turns edge-on, then the face turns back to full width.")]
    [Min(0f)] public float flipSeconds = 0.35f;

    [Tooltip("Extra height at the middle of the flip, so the card seems to lift off the table.")]
    [Min(0f)] public float flipLift = 0.1f;

    [Tooltip("Tilt in degrees at the middle of the flip.")]
    public float flipTiltDegrees = 5f;

    [Tooltip("Peak scale of the pop once the face is up. 1 turns the pop off.")]
    [Min(1f)] public float popScale = 1.15f;

    [Min(0.01f)] public float popSeconds = 0.25f;
}

/// <summary>
/// One vertical card of the Policy reveal: its back until it is revealed,
/// then a face with the Policy's icon, name and description. The face is an
/// ActionBarCardView, so its rim, body and tier motion match the other cards.
/// Revealing flips the card, pops it and plays its tier motion once.
///
/// Its own copy of the Ability pick card's flip, so the two can change apart.
/// Display only: PolicyRevealView sets it. Animates in unscaled time.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class PolicyCardView : MonoBehaviour
{
    [Header("Parts")]
    [Tooltip("Shown until the card is revealed.")]
    [SerializeField] private GameObject back;

    [Tooltip("The face: rim, body, icon and tier motion.")]
    [SerializeField] private ActionBarCardView face;

    [SerializeField] private UniText nameLabel;
    [SerializeField] private UniText descriptionLabel;

    private bool revealed;
    private ActionBarCardMotion tierMotion;

    private PolicyCardRevealTimings revealTimings;
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

    public void SetContent(Sprite icon, string title, string description)
    {
        if (face != null) face.SetIcon(icon);
        if (nameLabel != null) nameLabel.Text = title;
        if (descriptionLabel != null) descriptionLabel.Text = description;
    }

    public void SetStyle(in ActionBarCardStyle style, ActionBarCardTimings timings)
    {
        tierMotion = style.motion;

        // The tier motion plays once on reveal, not all the time.
        ActionBarCardStyle shown = style;
        shown.motion = ActionBarCardMotion.None;

        if (face != null) face.Display(shown, timings, false, false, false, false);
    }

    /// <summary>
    /// Face down, or face up. Turning face up flips the card when animated;
    /// turning face down is always instant.
    /// </summary>
    public void SetRevealed(bool faceUp, bool animate, PolicyCardRevealTimings timings)
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
