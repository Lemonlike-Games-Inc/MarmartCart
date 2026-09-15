using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns persistent player/team score and checkout-session reward calculation.
///
/// NEW checkout scoring:
/// - base score comes from CargoEntry.ScoreValue;
/// - streak / milestone bonuses are based on total CargoEntry count submitted
///   during the current checkout session;
/// - physical cart count is tracked for diagnostics only, not streak value.
///
/// </summary>
public class CashScoreManager : MonoBehaviour
{
    public const int MaxPlayers = 4;
    #region Cargo Streak Bonuses

    [Header("Cargo Checkout Milestone Bonuses")]
    [Tooltip("Thresholds are now TOTAL CARGO ENTRIES submitted during one checkout session.")]
    [SerializeField] private int streakThresholdLvl1 = 10;
    [SerializeField] private int streakThresholdLvl2 = 20;
    [SerializeField] private int streakThresholdLvl3 = 30;
    [SerializeField] private int streakThresholdLvl4 = 30;
    [SerializeField] private int streakThresholdLvl5 = 30;
    [SerializeField] private int streakThresholdLvl6 = 30;

    [SerializeField] private int streakThresholdBonusPts1 = 50;
    [SerializeField] private int streakThresholdBonusPts2 = 70;
    [SerializeField] private int streakThresholdBonusPts3 = 100;
    [SerializeField] private int streakThresholdBonusPts4 = 140;
    [SerializeField] private int streakThresholdBonusPts5 = 190;
    [SerializeField] private int streakThresholdBonusPts6 = 250;

    #endregion

    #region Team Mapping

    [Header("Team Mapping (used only in TeamBattle mode)")]
    [Tooltip("Example: Team 1 = players 1 & 3")]
    [SerializeField] private int[] team1Players = new[] { 1, 3 };

    [Tooltip("Example: Team 2 = players 2 & 4")]
    [SerializeField] private int[] team2Players = new[] { 2, 4 };

    #endregion

    #region Totals

    [Header("Totals (debug)")]
    [SerializeField] private float[] playerTotalScore = new float[MaxPlayers];

    [Header("Checkout Reward Debug")]
    [Min(0)]
    [SerializeField] private int debugProjectedCargoCount = 10;

    #endregion

    #region Context Menu Tests

    [ContextMenu("TEST - Log Projected Cargo Reward")]
    private void DebugLogProjectedCargoReward()
    {
        int cargoCount = Mathf.Max(0, debugProjectedCargoCount);
        int tier = GetProjectedStreakTier(cargoCount);
        int bonus = GetProjectedBonusPoints(cargoCount);
        int nextThreshold = GetNextStreakThreshold(cargoCount);

        Debug.Log(
            $"[CashScoreManager] Projected Cargo Reward | Cargo:{cargoCount} | " +
            $"StreakAchievable:{IsCheckoutStreakAchievable(cargoCount)} | Tier:{tier} | " +
            $"Bonus:{bonus} | NextThreshold:{nextThreshold}",
            this
        );
    }

    #endregion

    #region Session Data

    [Serializable]
    public class CheckoutSessionData
    {
        public bool isActive;

        [Header("Cargo Checkout")]
        public int cargoCount;
        public int checkedOutCartCount;

        public float basePoints;
        public float bonusPoints;
        public float subtotal;


        public void Reset()
        {
            isActive = false;

            cargoCount = 0;
            checkedOutCartCount = 0;

            basePoints = 0f;
            bonusPoints = 0f;
            subtotal = 0f;

        }

        public void CopyFrom(CheckoutSessionData other)
        {
            if (other == null)
            {
                Reset();
                return;
            }

            isActive = other.isActive;
            cargoCount = other.cargoCount;
            checkedOutCartCount = other.checkedOutCartCount;
            basePoints = other.basePoints;
            bonusPoints = other.bonusPoints;
            subtotal = other.subtotal;
        }
    }

    private readonly CheckoutSessionData[] currentSession = new CheckoutSessionData[MaxPlayers];
    private readonly CheckoutSessionData[] lastSession = new CheckoutSessionData[MaxPlayers];

    // Transient checkout-registration feedback. The timestamp is authored at
    // the scoring boundary rather than at the input boundary, so manual and
    // automatic checkout produce the exact same HUD pulse.
    private readonly int[] checkoutScorePulseValues = new int[MaxPlayers];
    private readonly float[] checkoutScorePulseTimes = new float[MaxPlayers];
    private readonly int[] checkoutScorePulseVersions = new int[MaxPlayers];
    private readonly bool[] checkoutBonusPulsePublished = new bool[MaxPlayers];

    #endregion

    #region Events

    public event Action<int, int, int> OnPlayerScoreGained;
    public event Action<int, int, int> OnTeamScoreGained;

    #endregion

    #region Unity Lifecycle

    private int ActivePlayerCount => Mathf.Clamp(GMode.Instance ? GMode.Instance.PlayerCount() : 2, 1, MaxPlayers);

    private void Awake()
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            currentSession[i] = new CheckoutSessionData();
            lastSession[i] = new CheckoutSessionData();
        }
    }

    private void Start()
    {
        ResetAllScores();
    }

    #endregion

    #region Checkout Session

    public void StartCheckoutSession(int playerIndex)
    {
        if (!IsValidPlayer(playerIndex)) return;

        int playerArrayIndex = playerIndex - 1;
        CheckoutSessionData session = currentSession[playerArrayIndex];
        session.Reset();
        session.isActive = true;

        ClearCheckoutScorePulse(playerArrayIndex);
    }

    /// <summary>
    /// Registers one physical follower cart being checked out.
    ///
    /// Every CargoEntry on that cart contributes:
    /// - +1 cargo toward streak thresholds;
    /// - CargoEntry.ScoreValue toward base score.
    ///
    /// Returns the base score added by this physical cart.
    /// Empty carts register nothing and do not increment checkedOutCartCount.
    /// </summary>
    public int RegisterCargoCheckout(int playerIndex, IReadOnlyList<CargoEntry> cargoEntries)
    {
        if (!IsValidPlayer(playerIndex) || cargoEntries == null || cargoEntries.Count == 0) return 0;

        CheckoutSessionData session = EnsureActiveSession(playerIndex);

        int cargoAdded = 0;
        int scoreAdded = 0;

        for (int i = 0; i < cargoEntries.Count; i++)
        {
            CargoEntry entry = cargoEntries[i];
            if (entry == null) continue;

            cargoAdded++;
            scoreAdded += Mathf.Max(0, entry.ScoreValue);
        }

        if (cargoAdded <= 0) return 0;

        session.checkedOutCartCount++;
        session.cargoCount += cargoAdded;
        session.basePoints += scoreAdded;
        RefreshSessionReward(session);

        PublishCheckoutScorePulse(
            playerIndex,
            Mathf.RoundToInt(session.basePoints)
        );

        return scoreAdded;
    }

    /// <summary>
    /// Marks the point where every physical cart has been registered, without
    /// committing the checkout session to banked score yet. If this session
    /// earned a streak/milestone bonus, presentation consumers receive one
    /// final cumulative pulse: submitted base score + earned bonus.
    /// </summary>
    public void CompleteCheckoutRegistration(int playerIndex)
    {
        if (!IsValidPlayer(playerIndex)) return;

        int playerArrayIndex = playerIndex - 1;
        CheckoutSessionData session = currentSession[playerArrayIndex];

        if (!session.isActive ||
            session.cargoCount <= 0 ||
            checkoutBonusPulsePublished[playerArrayIndex])
        {
            return;
        }

        checkoutBonusPulsePublished[playerArrayIndex] = true;

        int bonusScore = Mathf.RoundToInt(session.bonusPoints);
        if (bonusScore <= 0) return;

        PublishCheckoutScorePulse(
            playerIndex,
            Mathf.RoundToInt(session.subtotal)
        );
    }

    public void EndCheckoutSession(int playerIndex)
    {
        if (!IsValidPlayer(playerIndex)) return;

        CheckoutSessionData session = currentSession[playerIndex - 1];
        CheckoutSessionData last = lastSession[playerIndex - 1];

        if (!session.isActive || session.cargoCount <= 0)
        {
            session.Reset();
            return;
        }

        int gain = Mathf.RoundToInt(session.subtotal);

        playerTotalScore[playerIndex - 1] += gain;
        int newTotal = GetPlayerScore(playerIndex);

        OnPlayerScoreGained?.Invoke(playerIndex, gain, newTotal);

        if (GMode.Instance && GMode.Instance.IsTeamBattle)
        {
            int team = GetTeamIndexForPlayer(playerIndex);

            if (team != 0)
            {
                int teamTotal = GetTeamScore(team == 1 ? team1Players : team2Players);
                OnTeamScoreGained?.Invoke(team, gain, teamTotal);
            }
        }

        last.CopyFrom(session);
        last.isActive = false;

        session.Reset();
        ClearCheckoutScorePulse(playerIndex - 1);
    }

    public CheckoutSessionData GetCurrentSessionData(int playerIndex)
    {
        if (!IsValidPlayer(playerIndex)) return null;
        return currentSession[playerIndex - 1];
    }

    public CheckoutSessionData GetLastSessionData(int playerIndex)
    {
        if (!IsValidPlayer(playerIndex)) return null;
        return lastSession[playerIndex - 1];
    }

    /// <summary>
    /// Returns the latest successful cart-registration total, or the final
    /// base-plus-bonus total when the checkout reward was completed.
    /// Consumers decide how long that pulse remains visible.
    /// </summary>
    public bool TryGetCheckoutScorePulse(
        int playerIndex,
        out int cumulativeScore,
        out float registeredAtUnscaledTime)
    {
        cumulativeScore = 0;
        registeredAtUnscaledTime = 0f;

        if (!IsValidPlayer(playerIndex)) return false;

        int playerArrayIndex = playerIndex - 1;
        if (checkoutScorePulseVersions[playerArrayIndex] <= 0) return false;

        cumulativeScore = checkoutScorePulseValues[playerArrayIndex];
        registeredAtUnscaledTime = checkoutScorePulseTimes[playerArrayIndex];
        return true;
    }

    #endregion

    #region Projected Cargo Streak API

    /// <summary>
    /// True when this cargo count reaches at least the first configured milestone.
    /// Intended for future "streak achievable" HUD messaging.
    /// </summary>
    public bool IsCheckoutStreakAchievable(int cargoCount)
    {
        return GetProjectedStreakTier(cargoCount) > 0;
    }

    /// <summary>
    /// Number of configured milestone thresholds reached by this cargo count.
    /// Duplicate thresholds intentionally count as separate configured milestones,
    /// matching the existing milestone-bonus behavior.
    /// </summary>
    public int GetProjectedStreakTier(int cargoCount)
    {
        int tier = 0;

        if (cargoCount >= streakThresholdLvl1) tier++;
        if (cargoCount >= streakThresholdLvl2) tier++;
        if (cargoCount >= streakThresholdLvl3) tier++;
        if (cargoCount >= streakThresholdLvl4) tier++;
        if (cargoCount >= streakThresholdLvl5) tier++;
        if (cargoCount >= streakThresholdLvl6) tier++;

        return tier;
    }

    public int GetProjectedBonusPoints(int cargoCount)
    {
        return GetMilestoneBonus(Mathf.Max(0, cargoCount));
    }

    /// <summary>
    /// Returns the smallest configured threshold greater than cargoCount.
    /// Returns -1 when every configured milestone has already been reached.
    /// </summary>
    public int GetNextStreakThreshold(int cargoCount)
    {
        int next = int.MaxValue;

        TrySelectNextThreshold(streakThresholdLvl1, cargoCount, ref next);
        TrySelectNextThreshold(streakThresholdLvl2, cargoCount, ref next);
        TrySelectNextThreshold(streakThresholdLvl3, cargoCount, ref next);
        TrySelectNextThreshold(streakThresholdLvl4, cargoCount, ref next);
        TrySelectNextThreshold(streakThresholdLvl5, cargoCount, ref next);
        TrySelectNextThreshold(streakThresholdLvl6, cargoCount, ref next);

        return next == int.MaxValue ? -1 : next;
    }

    public int GetFirstStreakThreshold()
    {
        int first = int.MaxValue;

        SelectPositiveMinimum(streakThresholdLvl1, ref first);
        SelectPositiveMinimum(streakThresholdLvl2, ref first);
        SelectPositiveMinimum(streakThresholdLvl3, ref first);
        SelectPositiveMinimum(streakThresholdLvl4, ref first);
        SelectPositiveMinimum(streakThresholdLvl5, ref first);
        SelectPositiveMinimum(streakThresholdLvl6, ref first);

        return first == int.MaxValue ? -1 : first;
    }

    private void TrySelectNextThreshold(int threshold, int cargoCount, ref int currentNext)
    {
        if (threshold > cargoCount && threshold < currentNext) currentNext = threshold;
    }

    private void SelectPositiveMinimum(int threshold, ref int currentMinimum)
    {
        if (threshold > 0 && threshold < currentMinimum) currentMinimum = threshold;
    }

    #endregion


    #region Score / Team API

    public int GetPlayerScore(int playerIndex)
    {
        if (!IsValidPlayer(playerIndex)) return 0;
        return Mathf.RoundToInt(playerTotalScore[playerIndex - 1]);
    }

    public int GetTeamScore(int[] teamPlayers)
    {
        int sum = 0;
        if (teamPlayers == null) return 0;

        for (int i = 0; i < teamPlayers.Length; i++)
        {
            sum += GetPlayerScore(teamPlayers[i]);
        }

        return sum;
    }

    public int GetTeamIndexForPlayer(int playerIndex)
    {
        if (team1Players != null)
        {
            for (int i = 0; i < team1Players.Length; i++)
            {
                if (team1Players[i] == playerIndex) return 1;
            }
        }

        if (team2Players != null)
        {
            for (int i = 0; i < team2Players.Length; i++)
            {
                if (team2Players[i] == playerIndex) return 2;
            }
        }

        return 0;
    }

    #endregion

    #region Internal Reward Calculation

    private CheckoutSessionData EnsureActiveSession(int playerIndex)
    {
        int playerArrayIndex = playerIndex - 1;
        CheckoutSessionData session = currentSession[playerArrayIndex];

        if (!session.isActive)
        {
            session.Reset();
            session.isActive = true;
            ClearCheckoutScorePulse(playerArrayIndex);
        }

        return session;
    }

    private void PublishCheckoutScorePulse(
        int playerIndex,
        int cumulativeScore)
    {
        int playerArrayIndex = playerIndex - 1;

        checkoutScorePulseValues[playerArrayIndex] =
            Mathf.Max(0, cumulativeScore);

        checkoutScorePulseTimes[playerArrayIndex] =
            Time.unscaledTime;

        checkoutScorePulseVersions[playerArrayIndex]++;
    }

    private void ClearCheckoutScorePulse(int playerArrayIndex)
    {
        checkoutScorePulseValues[playerArrayIndex] = 0;
        checkoutScorePulseTimes[playerArrayIndex] = 0f;
        checkoutScorePulseVersions[playerArrayIndex] = 0;
        checkoutBonusPulsePublished[playerArrayIndex] = false;
    }

    private void RefreshSessionReward(CheckoutSessionData session)
    {
        session.bonusPoints = GetMilestoneBonus(session.cargoCount);
        session.subtotal = session.basePoints + session.bonusPoints;
    }

    private int GetMilestoneBonus(int cargoCount)
    {
        int bonus = 0;

        if (cargoCount >= streakThresholdLvl1) bonus += streakThresholdBonusPts1;
        if (cargoCount >= streakThresholdLvl2) bonus += streakThresholdBonusPts2;
        if (cargoCount >= streakThresholdLvl3) bonus += streakThresholdBonusPts3;
        if (cargoCount >= streakThresholdLvl4) bonus += streakThresholdBonusPts4;
        if (cargoCount >= streakThresholdLvl5) bonus += streakThresholdBonusPts5;
        if (cargoCount >= streakThresholdLvl6) bonus += streakThresholdBonusPts6;

        return bonus;
    }

    private bool IsValidPlayer(int playerIndex)
    {
        return playerIndex >= 1 && playerIndex <= ActivePlayerCount;
    }

    private void ResetAllScores()
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            playerTotalScore[i] = 0f;
            currentSession[i].Reset();
            lastSession[i].Reset();
            ClearCheckoutScorePulse(i);
        }
    }

    #endregion
}
