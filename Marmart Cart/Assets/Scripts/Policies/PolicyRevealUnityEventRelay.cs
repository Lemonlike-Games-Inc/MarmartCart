using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Inspector hooks for the pre-round Policy reveal: sound or VFX as it starts,
/// as each card flips, as the cards collapse into the banners, and when it is
/// done. Wire each hook in the Inspector, for example to SfxManager.PlaySFX.
/// For one Policy's own card, use onRevealed on its PolicyUnityEventRelay.
///
/// Announcer: with an Announcer Source set, each revealed Policy's Announcer
/// Clip (on its PolicyDefinition) plays on it as its card flips. The planned
/// store announcer system can replace this by listening to PolicyRevealStage.
/// Sits next to PolicyRevealStage (Meta Game Controller/Policy Reveal).
/// </summary>
[DisallowMultipleComponent]
public sealed class PolicyRevealUnityEventRelay : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Auto-found (on this object first) if left empty.")]
    [SerializeField] private PolicyRevealStage revealStage;

    [Header("Hooks")]
    [Tooltip("The reveal begins: the shade and card backs appear.")]
    [SerializeField] private UnityEvent onRevealStarted = new UnityEvent();

    [Tooltip("Any card flips face up, once per card.")]
    [SerializeField] private UnityEvent onCardRevealed = new UnityEvent();

    [Tooltip("The cards start to collapse into the top-left banners.")]
    [SerializeField] private UnityEvent onCollapseStarted = new UnityEvent();

    [Tooltip("The reveal finished. Not called when it is skipped or cut by a load.")]
    [SerializeField] private UnityEvent onRevealFinished = new UnityEvent();

    [Header("Announcer")]
    [Tooltip("Plays each revealed Policy's Announcer Clip as its card flips. Empty: no announcer.")]
    [SerializeField] private AudioSource announcerSource;

    private void Awake()
    {
        if (revealStage == null) revealStage = GetComponent<PolicyRevealStage>();
        if (revealStage == null) revealStage = FindFirstObjectByType<PolicyRevealStage>();
    }

    private void OnEnable()
    {
        if (revealStage == null) return;

        revealStage.RevealStarted += HandleRevealStarted;
        revealStage.CardRevealed += HandleCardRevealed;
        revealStage.CollapseStarted += HandleCollapseStarted;
        revealStage.RevealEnded += HandleRevealEnded;
    }

    private void OnDisable()
    {
        if (revealStage == null) return;

        revealStage.RevealStarted -= HandleRevealStarted;
        revealStage.CardRevealed -= HandleCardRevealed;
        revealStage.CollapseStarted -= HandleCollapseStarted;
        revealStage.RevealEnded -= HandleRevealEnded;
    }

    private void HandleRevealStarted() => onRevealStarted.Invoke();

    private void HandleCardRevealed(int index, PolicyDefinition policy)
    {
        onCardRevealed.Invoke();

        if (announcerSource != null && policy != null && policy.AnnouncerClip != null)
            announcerSource.PlayOneShot(policy.AnnouncerClip);
    }

    private void HandleCollapseStarted() => onCollapseStarted.Invoke();

    // A cancel also comes from loads and teardown, when sound targets may be gone.
    private void HandleRevealEnded()
    {
        if (!revealStage.WasCancelled) onRevealFinished.Invoke();
    }
}
