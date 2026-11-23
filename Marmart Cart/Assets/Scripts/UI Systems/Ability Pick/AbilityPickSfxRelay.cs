using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Sound hooks for the Ability pick. Wire each hook in the Inspector, for
/// example to SfxManager.PlaySFX. Lives next to AbilityPickPresenter
/// (Game UIs/Ability Pick Presenter in each gameplay scene).
///
/// Every player's cards flip at the same moment, so by default the reveal
/// hook fires once per player view at each flip and the sounds stack in 4P.
/// Turn on Reveal Once Per Flip to hear it once for the whole screen.
/// </summary>
[DisallowMultipleComponent]
public sealed class AbilityPickSfxRelay : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private AbilitySamplePickController pickController;

    [Header("Hooks")]
    [Tooltip("A card starts to flip face up. The same for every tier.")]
    [SerializeField] private UnityEvent onCardRevealed = new UnityEvent();

    [Tooltip("Off: the reveal hook fires once per player view (stacks in 4P). On: once per flip for the whole screen.")]
    [SerializeField] private bool revealOncePerFlip;

    [Tooltip("A player moved their highlight to another card.")]
    [SerializeField] private UnityEvent onHighlightMoved = new UnityEvent();

    [Tooltip("A player picked a card with A, or the time ran out and the highlighted card was picked for them.")]
    [SerializeField] private UnityEvent onPicked = new UnityEvent();

    [Tooltip("Every player has picked. Leave empty to stay silent.")]
    [SerializeField] private UnityEvent onEveryonePicked = new UnityEvent();

    [Header("Debug")]
    [Tooltip("Logs each hook as it fires, to check the timing before sounds are wired.")]
    [SerializeField] private bool logHooks;

    private void Awake()
    {
        if (pickController == null) pickController = FindFirstObjectByType<AbilitySamplePickController>();
    }

    private void OnEnable()
    {
        if (pickController == null) return;

        pickController.CardRevealed += HandleCardRevealed;
        pickController.HighlightChanged += HandleHighlightChanged;
        pickController.CardPicked += HandleCardPicked;
        pickController.AllPicked += HandleAllPicked;
    }

    private void OnDisable()
    {
        if (pickController == null) return;

        pickController.CardRevealed -= HandleCardRevealed;
        pickController.HighlightChanged -= HandleHighlightChanged;
        pickController.CardPicked -= HandleCardPicked;
        pickController.AllPicked -= HandleAllPicked;
    }

    private void HandleCardRevealed(int cardIndex)
    {
        if (revealOncePerFlip)
        {
            Fire(onCardRevealed, $"card {cardIndex + 1} revealed");
            return;
        }

        for (int playerIndex = 1; playerIndex <= pickController.PlayerCount; playerIndex++)
        {
            if (pickController.GetOffer(playerIndex, cardIndex) == null) continue;
            Fire(onCardRevealed, $"P{playerIndex} card {cardIndex + 1} revealed");
        }
    }

    private void HandleHighlightChanged(int playerIndex, int cardIndex)
    {
        Fire(onHighlightMoved, $"P{playerIndex} highlight moved");
    }

    private void HandleCardPicked(int playerIndex, int cardIndex, bool auto)
    {
        Fire(onPicked, $"P{playerIndex} picked{(auto ? " (time ran out)" : string.Empty)}");
    }

    private void HandleAllPicked()
    {
        Fire(onEveryonePicked, "everyone picked");
    }

    private void Fire(UnityEvent hook, string label)
    {
        if (logHooks) Debug.Log($"[Sfx] Pick: {label}.", this);
        hook.Invoke();
    }
}
