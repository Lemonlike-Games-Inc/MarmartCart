using System;
using UnityEngine;

/// <summary>
/// The Policy reveal before a Round that has Policies: the first Pre-Round
/// Stage, before the Ability pick, so players pick knowing the Round. Every
/// player sees the same cards in their own view. The cards flip face up one
/// by one, stay up for reading, then collapse into the top-left banner cards.
/// Fixed timer, no input.
///
/// A Pre-Round Stage of MetaRoundController, so it runs in real time while
/// PreGame keeps the world frozen. Presentation listens to the events and
/// reads the queries; it never changes the reveal.
/// </summary>
[DisallowMultipleComponent]
public sealed class PolicyRevealStage : PreRoundStage
{
    /// <summary>Most Policies one reveal shows. Later ones are left out of the reveal (they still run).</summary>
    public const int MaxCards = 3;

    public enum RevealPhase
    {
        Idle,
        Intro,
        Revealing,
        Reading,
        Collapsing,
        Done
    }

    [Header("References")]
    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private MarmartPolicyDirector director;

    [Header("Timing (real-time seconds)")]
    [Tooltip("Wait before the first card flips, so the scene fade-in can finish.")]
    [Min(0f)]
    [SerializeField] private float introSeconds = 0.8f;

    [Tooltip("Time between card flips, left to right.")]
    [Min(0.05f)]
    [SerializeField] private float revealStepSeconds = 0.8f;

    [Tooltip("Reading time after the last card flips, with one Policy.")]
    [Min(0f)]
    [SerializeField] private float readSeconds = 3f;

    [Tooltip("Extra reading time for each Policy after the first.")]
    [Min(0f)]
    [SerializeField] private float extraReadSecondsPerPolicy = 1.5f;

    [Tooltip("Time for the cards to collapse into the top-left banners.")]
    [Min(0.05f)]
    [SerializeField] private float collapseSeconds = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool logReveal = true;

    [Header("Runtime - Read Only")]
    [SerializeField] private RevealPhase phase = RevealPhase.Idle;
    [SerializeField] private int cardCount;
    [SerializeField] private int revealedCount;

    private float phaseEndTime;
    private float collapseStartTime;

    /// <summary>The reveal began. Every card shows its back.</summary>
    public event Action RevealStarted;

    /// <summary>The card at this index (0 = left) flipped face up, for every player at once.</summary>
    public event Action<int, PolicyDefinition> CardRevealed;

    /// <summary>The cards start to collapse; the banner cards appear.</summary>
    public event Action CollapseStarted;

    /// <summary>The reveal ended, finished or cancelled. The reveal hides.</summary>
    public event Action RevealEnded;

    public RevealPhase Phase => phase;
    public bool IsShowing => phase != RevealPhase.Idle && phase != RevealPhase.Done;
    public int CardCount => cardCount;
    public int RevealedCount => revealedCount;

    /// <summary>0 to 1 while the cards collapse, 1 after.</summary>
    public float CollapseProgress01
    {
        get
        {
            if (phase == RevealPhase.Collapsing)
                return Mathf.Clamp01((Time.unscaledTime - collapseStartTime) / collapseSeconds);

            return phase == RevealPhase.Done ? 1f : 0f;
        }
    }

    public override bool IsFinished => phase == RevealPhase.Done;

    public PolicyDefinition GetPolicy(int cardIndex)
    {
        if (director == null || cardIndex < 0 || cardIndex >= cardCount) return null;
        return director.ActivePolicies[cardIndex];
    }

    private void Awake()
    {
        if (director == null) director = FindFirstObjectByType<MarmartPolicyDirector>();
    }

    public override bool RunsBeforeRound(int round)
    {
        return director != null && director.HasPolicies;
    }

    public override void Begin(int round)
    {
        cardCount = Mathf.Min(director.ActivePolicies.Count, MaxCards);
        revealedCount = 0;

        if (director.ActivePolicies.Count > MaxCards)
            Debug.LogWarning($"[Policy] {director.ActivePolicies.Count} Policies this Round; the reveal shows the first {MaxCards}.", this);

        SetPhase(RevealPhase.Intro, introSeconds);
        if (logReveal) Debug.Log($"[Policy] Reveal started | Round {round} | {cardCount} card(s)", this);
        RevealStarted?.Invoke();
    }

    public override void Cancel()
    {
        if (!IsShowing) return;

        phase = RevealPhase.Done;
        if (logReveal) Debug.Log("[Policy] Reveal cancelled.", this);
        RevealEnded?.Invoke();
    }

    private void OnDisable()
    {
        Cancel();
    }

    private void Update()
    {
        if (!IsShowing || Time.unscaledTime < phaseEndTime) return;

        switch (phase)
        {
            case RevealPhase.Intro:
            case RevealPhase.Revealing:
                RevealNextCard();
                break;

            case RevealPhase.Reading:
                collapseStartTime = Time.unscaledTime;
                SetPhase(RevealPhase.Collapsing, collapseSeconds);
                CollapseStarted?.Invoke();
                break;

            case RevealPhase.Collapsing:
                phase = RevealPhase.Done;
                if (logReveal) Debug.Log("[Policy] Reveal finished.", this);
                RevealEnded?.Invoke();
                break;
        }
    }

    private void RevealNextCard()
    {
        int index = revealedCount++;
        PolicyDefinition policy = GetPolicy(index);

        if (revealedCount < cardCount)
            SetPhase(RevealPhase.Revealing, revealStepSeconds);
        else
            SetPhase(RevealPhase.Reading, readSeconds + extraReadSecondsPerPolicy * Mathf.Max(0, cardCount - 1));

        if (logReveal && policy != null) Debug.Log($"[Policy] Revealed: {policy.DisplayName}", this);
        CardRevealed?.Invoke(index, policy);
    }

    private void SetPhase(RevealPhase next, float duration)
    {
        phase = next;
        phaseEndTime = Time.unscaledTime + duration;
    }
}
