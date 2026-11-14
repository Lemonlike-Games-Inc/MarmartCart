using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One immutable visual/value record copied at the exact checkout scoring
/// boundary. It deliberately stores no runtime cargo ownership state.
/// </summary>
[Serializable]
public sealed class MatchResultsCargoRecord
{
    [SerializeField] private CargoVisualDefinition cargoVisual;
    [SerializeField] private int scoreValue;

    public CargoVisualDefinition CargoVisual => cargoVisual;
    public int ScoreValue => scoreValue;

    public MatchResultsCargoRecord(
        CargoVisualDefinition cargoVisual,
        int scoreValue)
    {
        this.cargoVisual = cargoVisual;
        this.scoreValue = Mathf.Max(0, scoreValue);
    }
}

/// <summary>
/// Read-only end-of-match data consumed by one FinalScoreCargoTower.
/// FinalScore is always authoritative; the ledger breakdown is reconciled to
/// it so presentation can never finish on a different number than gameplay.
/// </summary>
public sealed class MatchResultsPlayerSnapshot
{
    public int PlayerIndex { get; }
    public int FinalScore { get; }
    public int BaseScore { get; }
    public int BonusScore { get; }
    public int Rank { get; }
    public IReadOnlyList<MatchResultsCargoRecord> CargoRecords { get; }

    public MatchResultsPlayerSnapshot(
        int playerIndex,
        int finalScore,
        int baseScore,
        int bonusScore,
        int rank,
        IReadOnlyList<MatchResultsCargoRecord> cargoRecords)
    {
        PlayerIndex = Mathf.Clamp(playerIndex, 1, CashScoreManager.MaxPlayers);
        FinalScore = Mathf.Max(0, finalScore);
        BaseScore = Mathf.Clamp(baseScore, 0, FinalScore);
        BonusScore = Mathf.Clamp(bonusScore, 0, FinalScore - BaseScore);
        Rank = Mathf.Max(1, rank);
        CargoRecords = cargoRecords ?? Array.Empty<MatchResultsCargoRecord>();
    }
}

/// <summary>
/// Scene-level record of exactly which cargo visuals were successfully
/// submitted by every player, plus the milestone-bonus score later committed
/// by CashScoreManager.
///
/// CashScoreManager remains gameplay authority. This component is only a
/// presentation ledger and is safe to omit outside the gameplay scene.
/// </summary>
[DefaultExecutionOrder(-350)]
[DisallowMultipleComponent]
public sealed class MatchResultsCargoLedger : MonoBehaviour
{
    public const int MaxPlayers = CashScoreManager.MaxPlayers;

    [Serializable]
    private sealed class PlayerLedger
    {
        [SerializeField] private int playerIndex;
        [SerializeField] private int committedBaseScore;
        [SerializeField] private int committedBonusScore;
        [SerializeField]
        private List<MatchResultsCargoRecord> submittedCargo =
            new List<MatchResultsCargoRecord>(64);

        public int CommittedBaseScore => committedBaseScore;
        public int CommittedBonusScore => committedBonusScore;
        public IReadOnlyList<MatchResultsCargoRecord> SubmittedCargo => submittedCargo;

        public void Initialize(int index)
        {
            playerIndex = index;
            if (submittedCargo == null)
                submittedCargo = new List<MatchResultsCargoRecord>(64);
        }

        public void Reset()
        {
            committedBaseScore = 0;
            committedBonusScore = 0;
            submittedCargo.Clear();
        }

        public void AddCargo(IReadOnlyList<CargoEntry> entries)
        {
            if (entries == null) return;

            for (int i = 0; i < entries.Count; i++)
            {
                CargoEntry entry = entries[i];
                if (entry == null) continue;

                submittedCargo.Add(
                    new MatchResultsCargoRecord(
                        entry.SelectedCargoVisual,
                        entry.ScoreValue
                    )
                );
            }
        }

        public void AddCommit(CheckoutSessionCommit commit)
        {
            committedBaseScore += Mathf.Max(0, commit.BasePoints);
            committedBonusScore += Mathf.Max(0, commit.BonusPoints);
        }

        public MatchResultsCargoRecord[] CopyCargoRecords()
        {
            return submittedCargo.ToArray();
        }
    }

    public static MatchResultsCargoLedger Instance { get; private set; }

    [Header("References")]
    [SerializeField] private CashScoreManager cashScoreManager;
    [SerializeField] private GameTimeManager gameTimeManager;

    [Header("Runtime - Read Only")]
    [SerializeField]
    private PlayerLedger[] players =
        new PlayerLedger[MaxPlayers];

    private CashScoreManager subscribedScoreManager;
    private GameTimeManager subscribedGameTimeManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "[MatchResultsCargoLedger] More than one ledger exists in the scene.",
                this
            );
            enabled = false;
            return;
        }

        Instance = this;
        EnsurePlayerLedgers();
        ResetLedger();
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
    }

    private void Start()
    {
        ResolveReferences();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    [ContextMenu("Reset Results Cargo Ledger")]
    public void ResetLedger()
    {
        EnsurePlayerLedgers();

        for (int i = 0; i < players.Length; i++)
        {
            players[i].Reset();
        }
    }

    public MatchResultsPlayerSnapshot CreateSnapshot(
        int playerIndex,
        int authoritativeFinalScore,
        int rank)
    {
        EnsurePlayerLedgers();

        int safeFinalScore = Mathf.Max(0, authoritativeFinalScore);
        int slot = playerIndex - 1;

        if (slot < 0 || slot >= MaxPlayers)
        {
            return new MatchResultsPlayerSnapshot(
                playerIndex,
                safeFinalScore,
                safeFinalScore,
                0,
                rank,
                Array.Empty<MatchResultsCargoRecord>()
            );
        }

        PlayerLedger ledger = players[slot];

        // The final gameplay total always wins. Recorded bonus is clamped and
        // every remaining point is presented through the ordinary-cargo channel.
        int bonusScore = Mathf.Clamp(
            ledger.CommittedBonusScore,
            0,
            safeFinalScore
        );

        int baseScore = safeFinalScore - bonusScore;

        return new MatchResultsPlayerSnapshot(
            playerIndex,
            safeFinalScore,
            baseScore,
            bonusScore,
            rank,
            ledger.CopyCargoRecords()
        );
    }

    private void HandleCargoCheckoutRegistered(
        int playerIndex,
        IReadOnlyList<CargoEntry> entries)
    {
        int slot = playerIndex - 1;
        if (slot < 0 || slot >= MaxPlayers) return;

        players[slot].AddCargo(entries);
    }

    private void HandleCheckoutSessionCommitted(CheckoutSessionCommit commit)
    {
        int slot = commit.PlayerIndex - 1;
        if (slot < 0 || slot >= MaxPlayers) return;

        players[slot].AddCommit(commit);
    }

    private void ResolveReferences()
    {
        if (cashScoreManager == null)
            cashScoreManager = FindFirstObjectByType<CashScoreManager>();

        if (gameTimeManager == null)
            gameTimeManager = FindFirstObjectByType<GameTimeManager>();
    }

    private void Subscribe()
    {
        if (subscribedScoreManager != cashScoreManager)
        {
            if (subscribedScoreManager != null)
            {
                subscribedScoreManager.OnCargoCheckoutRegistered -=
                    HandleCargoCheckoutRegistered;
                subscribedScoreManager.OnCheckoutSessionCommitted -=
                    HandleCheckoutSessionCommitted;
            }

            subscribedScoreManager = cashScoreManager;

            if (subscribedScoreManager != null)
            {
                subscribedScoreManager.OnCargoCheckoutRegistered +=
                    HandleCargoCheckoutRegistered;
                subscribedScoreManager.OnCheckoutSessionCommitted +=
                    HandleCheckoutSessionCommitted;
            }
        }

        if (subscribedGameTimeManager != gameTimeManager)
        {
            if (subscribedGameTimeManager != null)
                subscribedGameTimeManager.OnMatchStarted -= ResetLedger;

            subscribedGameTimeManager = gameTimeManager;

            if (subscribedGameTimeManager != null)
                subscribedGameTimeManager.OnMatchStarted += ResetLedger;
        }
    }

    private void Unsubscribe()
    {
        if (subscribedScoreManager != null)
        {
            subscribedScoreManager.OnCargoCheckoutRegistered -=
                HandleCargoCheckoutRegistered;
            subscribedScoreManager.OnCheckoutSessionCommitted -=
                HandleCheckoutSessionCommitted;
            subscribedScoreManager = null;
        }

        if (subscribedGameTimeManager != null)
        {
            subscribedGameTimeManager.OnMatchStarted -= ResetLedger;
            subscribedGameTimeManager = null;
        }
    }

    private void EnsurePlayerLedgers()
    {
        if (players == null || players.Length != MaxPlayers)
        {
            PlayerLedger[] oldPlayers = players;
            players = new PlayerLedger[MaxPlayers];

            if (oldPlayers != null)
            {
                int copyCount = Mathf.Min(oldPlayers.Length, MaxPlayers);
                for (int i = 0; i < copyCount; i++) players[i] = oldPlayers[i];
            }
        }

        for (int i = 0; i < MaxPlayers; i++)
        {
            if (players[i] == null) players[i] = new PlayerLedger();
            players[i].Initialize(i + 1);
        }
    }
}
