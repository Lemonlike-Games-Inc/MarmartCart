using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The Ability Sample pick before a Round (from Round 2). Each player is dealt
/// three cards from the Sample Pool. The cards flip face up one by one, left to
/// right; then each player highlights a card with left/right (stick, D-pad) and
/// locks it with A. When the time runs out, anyone who has not locked gets the
/// card they highlight. Once everyone has locked, the pick holds briefly and
/// ends, the rest of the time is skipped, and each player's Sample is set for
/// the Round.
///
/// A Pre-Round Stage of MetaRoundController, so it runs in real time while
/// PreGame keeps the world frozen. Presentation listens to the events and reads
/// the queries; it never changes the pick. Player indices are 1-based.
///
/// Debug Mode: the pick time becomes 99999 s and F5 deals new cards to every
/// player. "Deal New Cards" in this component's ⋮ menu does the same.
/// </summary>
[DisallowMultipleComponent]
public sealed class AbilitySamplePickController : PreRoundStage
{
    public const int CardCount = 3;
    private const int MaxPlayers = MatchSession.MaxPlayers;
    private const float DebugPickSeconds = 99999f;

    public enum PickPhase
    {
        Idle,
        Intro,
        Revealing,
        Choosing,
        AllPicked,
        Done
    }

    [Header("Offer")]
    [SerializeField] private AbilitySamplePool pool;

    [Tooltip("The first Round with a pick. 2 in the event build; set 1 to test the pick without skipping a Round.")]
    [Min(1)]
    [SerializeField] private int firstPickRound = 2;

    [Header("Timing (real-time seconds)")]
    [Tooltip("Wait before the first card flips, so the scene fade-in can finish.")]
    [Min(0f)]
    [SerializeField] private float introSeconds = 0.6f;

    [Tooltip("Time between card flips, left to right. Choosing starts one step after the last flip.")]
    [Min(0.05f)]
    [SerializeField] private float revealStepSeconds = 0.3f;

    [Tooltip("Time to choose, counted from when every card is face up.")]
    [Min(1f)]
    [SerializeField] private float pickSeconds = 20f;

    [Tooltip("Hold after everyone has locked, before the Round title. The rest of the pick time is skipped.")]
    [Min(0f)]
    [SerializeField] private float allPickedHoldSeconds = 3f;

    [Header("Debug")]
    [Tooltip("Pick time becomes 99999 s, and F5 deals new cards to every player. Turn off for the booth build.")]
    [SerializeField] private bool debugMode;

    [SerializeField] private bool logPick = true;

    [Header("Runtime - Read Only")]
    [SerializeField] private PickPhase phase = PickPhase.Idle;
    [SerializeField] private int round;
    [SerializeField] private int playerCount;

    private readonly AbilitySampleDefinition[] offers = new AbilitySampleDefinition[MaxPlayers * CardCount];
    private readonly int[] cardsInHand = new int[MaxPlayers];
    private readonly int[] highlights = new int[MaxPlayers];
    private readonly bool[] locked = new bool[MaxPlayers];

    private readonly CartControlScript[] inputs = new CartControlScript[MaxPlayers];
    private readonly PlayerAbilityController[] abilities = new PlayerAbilityController[MaxPlayers];
    private readonly Action<int>[] stepHandlers = new Action<int>[MaxPlayers];
    private readonly Action[] submitHandlers = new Action[MaxPlayers];

    private readonly AbilitySampleDefinition[] rolledHand = new AbilitySampleDefinition[CardCount];
    private readonly List<AbilitySampleDefinition> rollCandidates = new List<AbilitySampleDefinition>(16);

    private PowerupRuntimeSystem runtimeSystem;
    private bool bindingPending;
    private float phaseStartTime;
    private float pickEndTime;
    private float holdEndTime;
    private int revealedCount;

    /// <summary>New cards were dealt (the pick began, or a debug deal). Every card shows its back.</summary>
    public event Action CardsDealt;

    /// <summary>The card at this index (0 = left) flipped face up, for every player at once.</summary>
    public event Action<int> CardRevealed;

    /// <summary>Every card is face up; players can choose now.</summary>
    public event Action ChoosingStarted;

    /// <summary>A player highlighted another card: player, card index.</summary>
    public event Action<int, int> HighlightChanged;

    /// <summary>A player locked a card: player, card index, and whether the time ran out (auto pick).</summary>
    public event Action<int, int, bool> CardPicked;

    /// <summary>Every player has locked a card. The pick holds, then ends.</summary>
    public event Action AllPicked;

    /// <summary>The pick ended, finished or cancelled. Presentation hides.</summary>
    public event Action PickEnded;

    public PickPhase Phase => phase;
    public bool IsShowing => phase != PickPhase.Idle && phase != PickPhase.Done;
    public bool IsChoosing => phase == PickPhase.Choosing;
    public int Round => round;
    public int PlayerCount => playerCount;
    public int RevealedCount => revealedCount;
    public bool IsDebugMode => debugMode;

    /// <summary>The full pick time: 20 s, or 99999 s in Debug Mode.</summary>
    public float PickDuration => debugMode ? DebugPickSeconds : pickSeconds;

    /// <summary>Pick seconds left. The full time before choosing starts.</summary>
    public float RemainingPickSeconds
    {
        get
        {
            switch (phase)
            {
                case PickPhase.Intro:
                case PickPhase.Revealing:
                    return PickDuration;
                case PickPhase.Choosing:
                case PickPhase.AllPicked:
                    return Mathf.Max(0f, pickEndTime - Time.unscaledTime);
                default:
                    return 0f;
            }
        }
    }

    public override bool IsFinished => phase == PickPhase.Done;

    public AbilitySampleDefinition GetOffer(int playerIndex, int cardIndex)
    {
        if (!IsValidPlayer(playerIndex) || cardIndex < 0 || cardIndex >= CardCount) return null;
        return offers[(playerIndex - 1) * CardCount + cardIndex];
    }

    /// <summary>How many cards this player was dealt: 3 unless the pool ran out.</summary>
    public int GetCardsInHand(int playerIndex)
    {
        return IsValidPlayer(playerIndex) ? cardsInHand[playerIndex - 1] : 0;
    }

    /// <summary>The highlighted card index, or -1 when the player has no cards.</summary>
    public int GetHighlight(int playerIndex)
    {
        return IsValidPlayer(playerIndex) ? highlights[playerIndex - 1] : -1;
    }

    public bool IsLocked(int playerIndex)
    {
        return IsValidPlayer(playerIndex) && locked[playerIndex - 1];
    }

    #region Unity

    private void Awake()
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            int slot = i;
            stepHandlers[i] = direction => HandleStep(slot, direction);
            submitHandlers[i] = () => HandleSubmit(slot);
        }
    }

    private void OnDisable()
    {
        UnbindInputs();
    }

    private void Update()
    {
        if (!IsShowing) return;

        if (bindingPending) BindInputs();

        if (debugMode && Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame)
        {
            DealNewCards();
            return;
        }

        float now = Time.unscaledTime;

        switch (phase)
        {
            case PickPhase.Intro:
                if (now >= phaseStartTime) EnterPhase(PickPhase.Revealing, now);
                break;

            case PickPhase.Revealing:
                while (revealedCount < CardCount && now >= phaseStartTime + revealedCount * revealStepSeconds)
                {
                    RevealCard(revealedCount);
                }

                if (now >= phaseStartTime + CardCount * revealStepSeconds) StartChoosing(now);
                break;

            case PickPhase.Choosing:
                if (now >= pickEndTime) AutoPickRemaining();
                break;

            case PickPhase.AllPicked:
                if (now >= holdEndTime) Finish();
                break;
        }
    }

    #endregion

    #region Stage

    public override bool RunsBeforeRound(int roundNumber)
    {
        return roundNumber >= firstPickRound;
    }

    public override void Begin(int roundNumber)
    {
        round = roundNumber;
        playerCount = MetaRoundController.ActivePlayerCount;

        if (pool == null)
        {
            Debug.LogError("[Ability] Pick skipped: no Sample Pool assigned.", this);
            phase = PickPhase.Done;
            return;
        }

        if (runtimeSystem == null) runtimeSystem = FindFirstObjectByType<PowerupRuntimeSystem>();

        if (runtimeSystem == null)
            Debug.LogError("[Ability] PowerupRuntimeSystem not found: no player can pick or receive a Sample.", this);

        bindingPending = true;
        BindInputs();
        Deal(introSeconds);
    }

    public override void Cancel()
    {
        if (!IsShowing) return;

        UnbindInputs();
        phase = PickPhase.Done;
        Log("Pick cancelled. No Sample set.");
        PickEnded?.Invoke();
    }

    #endregion

    #region Pick

    /// <summary>Debug: deals every player new cards and replays the reveal. Play Mode, during a pick.</summary>
    [ContextMenu("Deal New Cards")]
    public void DealNewCards()
    {
        if (!IsShowing)
        {
            Debug.Log("[Ability] New cards only during a pick in Play Mode.", this);
            return;
        }

        Log("Debug: new cards.");
        Deal(revealStepSeconds);
    }

    private void Deal(float delayBeforeFirstFlip)
    {
        Array.Clear(offers, 0, offers.Length);

        for (int slot = 0; slot < MaxPlayers; slot++)
        {
            int count = 0;

            if (slot < playerCount)
            {
                count = pool.RollHand(round, rolledHand, rollCandidates);
                Array.Copy(rolledHand, 0, offers, slot * CardCount, CardCount);

                if (count < CardCount)
                    Debug.LogWarning($"[Ability] P{slot + 1} got only {count} cards: the Sample Pool has too few Samples.", this);
            }

            cardsInHand[slot] = count;
            highlights[slot] = count > 0 ? Mathf.Min(CardCount / 2, count - 1) : -1;

            // A player with no cards has nothing to choose.
            locked[slot] = slot < playerCount && count == 0;
        }

        revealedCount = 0;
        phase = PickPhase.Intro;
        phaseStartTime = Time.unscaledTime + delayBeforeFirstFlip;

        if (logPick) Debug.Log(DescribeHands(), this);
        CardsDealt?.Invoke();
    }

    private void EnterPhase(PickPhase nextPhase, float now)
    {
        phase = nextPhase;
        phaseStartTime = now;
    }

    private void RevealCard(int cardIndex)
    {
        revealedCount = cardIndex + 1;
        CardRevealed?.Invoke(cardIndex);
    }

    private void StartChoosing(float now)
    {
        EnterPhase(PickPhase.Choosing, now);
        pickEndTime = now + PickDuration;

        Log($"Choose! {PickDuration:0.#} s.");
        ChoosingStarted?.Invoke();

        // Only players without cards: nothing to wait for.
        if (AllLocked()) EnterAllPicked(now);
    }

    private void HandleStep(int slot, int direction)
    {
        if (phase != PickPhase.Choosing || locked[slot]) return;

        int next = Mathf.Clamp(highlights[slot] + direction, 0, cardsInHand[slot] - 1);
        if (next == highlights[slot]) return;

        highlights[slot] = next;
        Log($"P{slot + 1} highlights card {next + 1}: {DescribeOffer(slot, next)}.");
        HighlightChanged?.Invoke(slot + 1, next);
    }

    private void HandleSubmit(int slot)
    {
        if (phase != PickPhase.Choosing || locked[slot]) return;
        Lock(slot, false);
    }

    private void AutoPickRemaining()
    {
        for (int slot = 0; slot < playerCount && phase == PickPhase.Choosing; slot++)
        {
            if (!locked[slot]) Lock(slot, true);
        }
    }

    private void Lock(int slot, bool auto)
    {
        locked[slot] = true;

        int card = highlights[slot];
        Log($"P{slot + 1} picked card {card + 1}: {DescribeOffer(slot, card)}{(auto ? " (time ran out)" : string.Empty)}.");
        CardPicked?.Invoke(slot + 1, card, auto);

        if (AllLocked()) EnterAllPicked(Time.unscaledTime);
    }

    private void EnterAllPicked(float now)
    {
        EnterPhase(PickPhase.AllPicked, now);
        holdEndTime = now + allPickedHoldSeconds;

        Log($"Everyone picked. Round title in {allPickedHoldSeconds:0.#} s.");
        AllPicked?.Invoke();
    }

    private void Finish()
    {
        UnbindInputs();
        ApplyPicks();
        phase = PickPhase.Done;
        PickEnded?.Invoke();
    }

    // The picked Sample replaces any earlier one (one ability slot).
    private void ApplyPicks()
    {
        for (int slot = 0; slot < playerCount; slot++)
        {
            int card = highlights[slot];
            AbilitySampleDefinition sample = card >= 0 ? offers[slot * CardCount + card] : null;

            PlayerAbilityController ability = ResolveAbility(slot);

            if (ability == null)
            {
                Debug.LogError($"[Ability] P{slot + 1} has no PlayerAbilityController: the pick is lost.", this);
                continue;
            }

            ability.SetSample(sample);
        }
    }

    private bool AllLocked()
    {
        for (int slot = 0; slot < playerCount; slot++)
        {
            if (!locked[slot]) return false;
        }

        return true;
    }

    private bool IsValidPlayer(int playerIndex)
    {
        return playerIndex >= 1 && playerIndex <= playerCount;
    }

    #endregion

    #region Players

    // Players can finish binding their cart input after the pick begins, so this retries each frame until all are bound.
    private void BindInputs()
    {
        bindingPending = false;

        for (int slot = 0; slot < playerCount; slot++)
        {
            if (inputs[slot] != null) continue;

            CartControlScript input = TryGetHand(slot, out PlayerItemHand hand) ? hand.CartControlInput : null;

            if (input == null)
            {
                bindingPending = true;
                continue;
            }

            inputs[slot] = input;
            input.OnMenuHorizontalStep += stepHandlers[slot];
            input.OnMenuSubmitPressed += submitHandlers[slot];
        }
    }

    private void UnbindInputs()
    {
        bindingPending = false;

        for (int slot = 0; slot < MaxPlayers; slot++)
        {
            CartControlScript input = inputs[slot];
            if (input == null) continue;

            input.OnMenuHorizontalStep -= stepHandlers[slot];
            input.OnMenuSubmitPressed -= submitHandlers[slot];
            inputs[slot] = null;
        }
    }

    private PlayerAbilityController ResolveAbility(int slot)
    {
        if (abilities[slot] == null && TryGetHand(slot, out PlayerItemHand hand))
            abilities[slot] = hand.GetComponent<PlayerAbilityController>();

        return abilities[slot];
    }

    private bool TryGetHand(int slot, out PlayerItemHand hand)
    {
        hand = null;
        return runtimeSystem != null && runtimeSystem.TryGetPlayer(slot + 1, out hand);
    }

    #endregion

    #region Logging

    private void Log(string message)
    {
        if (logPick) Debug.Log($"[Ability] Pick | {message}", this);
    }

    private string DescribeOffer(int slot, int card)
    {
        AbilitySampleDefinition sample = card >= 0 ? offers[slot * CardCount + card] : null;
        return sample != null ? $"{sample.DisplayName} ({sample.Tier})" : "nothing";
    }

    private string DescribeHands()
    {
        StringBuilder text = new StringBuilder(256);
        text.Append("[Ability] Pick | Round ").Append(round).Append(" cards");

        for (int slot = 0; slot < playerCount; slot++)
        {
            text.Append(" | P").Append(slot + 1).Append(':');

            for (int card = 0; card < cardsInHand[slot]; card++)
            {
                text.Append(card == 0 ? " " : ", ").Append(DescribeOffer(slot, card));
            }
        }

        return text.ToString();
    }

    #endregion
}
