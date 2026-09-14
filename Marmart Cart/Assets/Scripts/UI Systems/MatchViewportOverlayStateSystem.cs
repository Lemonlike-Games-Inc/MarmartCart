using System;
using UnityEngine;

/// <summary>
/// Central semantic state for the viewport-attached match overlay.
///
/// Owns:
/// - match timer state;
/// - leaderboard state shared by every player viewport.
///
/// It contains no drawing code and no gameplay lookup logic.
/// </summary>
[DisallowMultipleComponent]
public class MatchViewportOverlayStateSystem : MonoBehaviour
{
    public const int MaxPlayerSlots = 4;

    public static MatchViewportOverlayStateSystem Instance { get; private set; }

    #region Timer State

    [Serializable]
    public struct TimerState
    {
        [SerializeField] private float remainingTime;
        [SerializeField] private float elapsedTime;
        [SerializeField] private float plannedDuration;
        [SerializeField] private int displaySeconds;
        [SerializeField] private GameSessionState sessionState;

        public float RemainingTime => remainingTime;
        public float ElapsedTime => elapsedTime;
        public float PlannedDuration => plannedDuration;
        public int DisplaySeconds => displaySeconds;
        public int Minutes => displaySeconds / 60;
        public int Seconds => displaySeconds % 60;
        public GameSessionState SessionState => sessionState;

        public TimerState(
            float remainingTime,
            float elapsedTime,
            float plannedDuration,
            GameSessionState sessionState)
        {
            this.remainingTime = Mathf.Max(0f, remainingTime);
            this.elapsedTime = Mathf.Max(0f, elapsedTime);
            this.plannedDuration = Mathf.Max(0f, plannedDuration);
            this.sessionState = sessionState;

            displaySeconds = Mathf.CeilToInt(this.remainingTime);
        }
    }

    #endregion

    #region Leaderboard State

    [Serializable]
    public struct LeaderboardPlayerState
    {
        [SerializeField] private int playerIndex;
        [SerializeField] private bool active;

        [SerializeField] private int committedBankedScore;
        [SerializeField] private int checkoutSubmittedScore;
        [SerializeField] private int bankedScore;
        [SerializeField] private int carriedPotentialScore;
        [SerializeField] private int projectedStreakRewardScore;
        [SerializeField] private int combinedPotentialScore;

        [SerializeField] private int rank;

        [SerializeField, Range(0f, 1f)] private float bankedNormalized;
        [SerializeField, Range(0f, 1f)] private float streakRewardStartNormalized;
        [SerializeField, Range(0f, 1f)] private float combinedNormalized;

        public int PlayerIndex => playerIndex;
        public bool Active => active;

        public int CommittedBankedScore => committedBankedScore;
        public int CheckoutSubmittedScore => checkoutSubmittedScore;
        public int BankedScore => bankedScore;
        public int CarriedPotentialScore => carriedPotentialScore;
        public int ProjectedStreakRewardScore => projectedStreakRewardScore;
        public int CombinedPotentialScore => combinedPotentialScore;

        public int Rank => rank;

        public float BankedNormalized => bankedNormalized;
        public float StreakRewardStartNormalized => streakRewardStartNormalized;
        public float CombinedNormalized => combinedNormalized;

        public LeaderboardPlayerState(
            int playerIndex,
            bool active,
            int committedBankedScore,
            int checkoutSubmittedScore,
            int carriedPotentialScore,
            int projectedStreakRewardScore,
            int rank,
            int globalMaximum)
        {
            this.playerIndex = playerIndex;
            this.active = active;

            this.committedBankedScore =
                Mathf.Max(0, committedBankedScore);

            this.checkoutSubmittedScore =
                Mathf.Max(0, checkoutSubmittedScore);

            bankedScore =
                this.committedBankedScore +
                this.checkoutSubmittedScore;

            this.carriedPotentialScore = Mathf.Max(0, carriedPotentialScore);
            this.projectedStreakRewardScore =
                Mathf.Clamp(
                    projectedStreakRewardScore,
                    0,
                    this.carriedPotentialScore
                );

            combinedPotentialScore =
                bankedScore +
                this.carriedPotentialScore;

            this.rank = Mathf.Max(0, rank);

            int safeMaximum = Mathf.Max(1, globalMaximum);

            bankedNormalized =
                Mathf.Clamp01((float)this.bankedScore / safeMaximum);

            streakRewardStartNormalized =
                Mathf.Clamp01(
                    (float)(
                        combinedPotentialScore -
                        this.projectedStreakRewardScore
                    ) /
                    safeMaximum
                );

            combinedNormalized =
                Mathf.Clamp01((float)combinedPotentialScore / safeMaximum);
        }
    }

    #endregion

    #region Serialized Runtime State

    [Header("Timer - Runtime Read Only")]
    [SerializeField] private TimerState timer;

    [Header("Leaderboard - Runtime Read Only")]
    [SerializeField, Range(0, MaxPlayerSlots)]
    private int leaderboardActivePlayerCount;

    [SerializeField] private int leaderboardGlobalMaximum = 20;

    [SerializeField]
    private LeaderboardPlayerState[] leaderboardPlayers =
        new LeaderboardPlayerState[MaxPlayerSlots];

    [Tooltip(
        "Player indices in visual row order. " +
        "Only the first Active Player Count entries are used.")]
    [SerializeField]
    private int[] leaderboardDisplayOrder = new int[MaxPlayerSlots]
    {
        1, 2, 3, 4
    };

    #endregion

    #region Public State

    public TimerState Timer => timer;

    public int LeaderboardActivePlayerCount =>
        leaderboardActivePlayerCount;

    public int LeaderboardGlobalMaximum =>
        leaderboardGlobalMaximum;

    public event Action<TimerState> OnTimerChanged;
    public event Action OnLeaderboardChanged;

    #endregion

    #region Unity

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "[MatchViewportOverlayStateSystem] More than one state system exists in the scene.",
                this
            );

            enabled = false;
            return;
        }

        Instance = this;

        EnsureLeaderboardArrays();
        ResetLeaderboard();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    #endregion

    #region Timer API

    public void SetTimer(
        float remainingTime,
        float elapsedTime,
        float plannedDuration,
        GameSessionState sessionState)
    {
        timer = new TimerState(
            remainingTime,
            elapsedTime,
            plannedDuration,
            sessionState
        );

        OnTimerChanged?.Invoke(timer);
    }

    public void ResetTimer()
    {
        timer = new TimerState(
            0f,
            0f,
            0f,
            GameSessionState.PreGame
        );

        OnTimerChanged?.Invoke(timer);
    }

    #endregion

    #region Leaderboard API

    /// <summary>
    /// Publishes one complete leaderboard snapshot.
    ///
    /// Ranking is based on visible banked score and is calculated by the adapter.
    /// During checkout, submitted base points are supplied separately so the
    /// renderer can move them from potential into banked on every submission.
    /// Normalization is calculated here from the shared global maximum.
    /// </summary>
    public void SetLeaderboard(
        int activePlayerCount,
        int globalMaximum,
        int[] committedBankedScores,
        int[] checkoutSubmittedScores,
        int[] carriedPotentialScores,
        int[] projectedStreakRewardScores,
        int[] ranks,
        int[] displayOrder)
    {
        EnsureLeaderboardArrays();

        int clampedActiveCount =
            Mathf.Clamp(activePlayerCount, 0, MaxPlayerSlots);

        int safeGlobalMaximum =
            Mathf.Max(1, globalMaximum);

        bool changed =
            leaderboardActivePlayerCount != clampedActiveCount ||
            leaderboardGlobalMaximum != safeGlobalMaximum;

        leaderboardActivePlayerCount = clampedActiveCount;
        leaderboardGlobalMaximum = safeGlobalMaximum;

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            int playerIndex = i + 1;
            bool active = i < clampedActiveCount;

            int committedBanked =
                committedBankedScores != null &&
                i < committedBankedScores.Length
                    ? Mathf.Max(0, committedBankedScores[i])
                    : 0;

            int checkoutSubmitted =
                checkoutSubmittedScores != null &&
                i < checkoutSubmittedScores.Length
                    ? Mathf.Max(0, checkoutSubmittedScores[i])
                    : 0;

            int potential =
                carriedPotentialScores != null &&
                i < carriedPotentialScores.Length
                    ? Mathf.Max(0, carriedPotentialScores[i])
                    : 0;

            int projectedStreakReward =
                projectedStreakRewardScores != null &&
                i < projectedStreakRewardScores.Length
                    ? Mathf.Clamp(
                        projectedStreakRewardScores[i],
                        0,
                        potential
                    )
                    : 0;

            int rank =
                ranks != null && i < ranks.Length
                    ? Mathf.Max(0, ranks[i])
                    : 0;

            LeaderboardPlayerState next =
                new LeaderboardPlayerState(
                    playerIndex,
                    active,
                    committedBanked,
                    checkoutSubmitted,
                    potential,
                    projectedStreakReward,
                    rank,
                    safeGlobalMaximum
                );

            if (!LeaderboardPlayerEquals(
                    leaderboardPlayers[i],
                    next))
            {
                changed = true;
            }

            leaderboardPlayers[i] = next;

            int nextOrder =
                displayOrder != null &&
                i < displayOrder.Length
                    ? Mathf.Clamp(
                        displayOrder[i],
                        1,
                        MaxPlayerSlots
                    )
                    : playerIndex;

            if (leaderboardDisplayOrder[i] != nextOrder)
            {
                changed = true;
                leaderboardDisplayOrder[i] = nextOrder;
            }
        }

        if (changed) OnLeaderboardChanged?.Invoke();
    }

    /// <summary>
    /// Compatibility overload for callers that publish checkout progress but
    /// do not separate the projected streak reward from total potential.
    /// </summary>
    public void SetLeaderboard(
        int activePlayerCount,
        int globalMaximum,
        int[] committedBankedScores,
        int[] checkoutSubmittedScores,
        int[] carriedPotentialScores,
        int[] ranks,
        int[] displayOrder)
    {
        SetLeaderboard(
            activePlayerCount,
            globalMaximum,
            committedBankedScores,
            checkoutSubmittedScores,
            carriedPotentialScores,
            null,
            ranks,
            displayOrder
        );
    }

    /// <summary>
    /// Compatibility overload for existing callers that do not publish
    /// per-submission checkout progress.
    /// </summary>
    public void SetLeaderboard(
        int activePlayerCount,
        int globalMaximum,
        int[] bankedScores,
        int[] carriedPotentialScores,
        int[] ranks,
        int[] displayOrder)
    {
        SetLeaderboard(
            activePlayerCount,
            globalMaximum,
            bankedScores,
            null,
            carriedPotentialScores,
            null,
            ranks,
            displayOrder
        );
    }

    public bool TryGetLeaderboardPlayer(
        int playerIndex,
        out LeaderboardPlayerState state)
    {
        EnsureLeaderboardArrays();

        int index = playerIndex - 1;

        if (index < 0 || index >= MaxPlayerSlots)
        {
            state = default;
            return false;
        }

        state = leaderboardPlayers[index];
        return true;
    }

    public int GetLeaderboardDisplayPlayerIndex(
        int rowIndex)
    {
        EnsureLeaderboardArrays();

        if (rowIndex < 0 ||
            rowIndex >= leaderboardActivePlayerCount)
        {
            return 0;
        }

        return leaderboardDisplayOrder[rowIndex];
    }

    [ContextMenu("Reset Leaderboard")]
    public void ResetLeaderboard()
    {
        EnsureLeaderboardArrays();

        leaderboardActivePlayerCount = 0;
        leaderboardGlobalMaximum = 20;

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            leaderboardPlayers[i] =
                new LeaderboardPlayerState(
                    i + 1,
                    false,
                    0,
                    0,
                    0,
                    0,
                    0,
                    leaderboardGlobalMaximum
                );

            leaderboardDisplayOrder[i] = i + 1;
        }

        OnLeaderboardChanged?.Invoke();
    }

    #endregion

    #region Internal

    private void EnsureLeaderboardArrays()
    {
        if (leaderboardPlayers == null ||
            leaderboardPlayers.Length != MaxPlayerSlots)
        {
            leaderboardPlayers =
                new LeaderboardPlayerState[MaxPlayerSlots];
        }

        if (leaderboardDisplayOrder == null ||
            leaderboardDisplayOrder.Length != MaxPlayerSlots)
        {
            leaderboardDisplayOrder =
                new int[MaxPlayerSlots]
                {
                    1, 2, 3, 4
                };
        }
    }

    private static bool LeaderboardPlayerEquals(
        LeaderboardPlayerState a,
        LeaderboardPlayerState b)
    {
        return
            a.PlayerIndex == b.PlayerIndex &&
            a.Active == b.Active &&
            a.CommittedBankedScore ==
                b.CommittedBankedScore &&
            a.CheckoutSubmittedScore ==
                b.CheckoutSubmittedScore &&
            a.BankedScore == b.BankedScore &&
            a.CarriedPotentialScore ==
                b.CarriedPotentialScore &&
            a.ProjectedStreakRewardScore ==
                b.ProjectedStreakRewardScore &&
            a.Rank == b.Rank;
    }

    #endregion
}
