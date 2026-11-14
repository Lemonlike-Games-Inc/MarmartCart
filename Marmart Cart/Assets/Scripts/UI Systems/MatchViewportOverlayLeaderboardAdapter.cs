using UnityEngine;

/// <summary>
/// Gameplay -> viewport leaderboard adapter.
///
/// Reads:
/// - banked score from CashScoreManager;
/// - submitted-but-not-yet-committed checkout base points from the active
///   CashScoreManager session;
/// - remaining projected checkout score from carried CargoEntry values + the
///   same streak/milestone bonus rules owned by CashScoreManager.
///
/// Publishes one shared snapshot to MatchViewportOverlayStateSystem.
/// Every player camera then renders the same leaderboard data, while the
/// renderer replaces that camera's own P# label with YOU.
/// </summary>
[DisallowMultipleComponent]
public class MatchViewportOverlayLeaderboardAdapter : MonoBehaviour
{
    private const int MaxPlayerSlots = 4;

    #region References

    [Header("References")]
    [SerializeField] private CashScoreManager cashScoreManager;
    [SerializeField] private GameTimeManager gameTimeManager;
    [SerializeField] private MatchViewportOverlayStateSystem stateSystem;
    [SerializeField] private MatchViewportOverlayProfile profile;

    [Tooltip(
        "Optional manual binding. Index 0..3 = P1..P4. " +
        "Missing entries are auto-resolved from runtime SnakeCartManagers.")]
    [SerializeField]
    private SnakeCartManager[] playerSnakeManagers =
        new SnakeCartManager[MaxPlayerSlots];

    #endregion

    #region Refresh

    [Header("Refresh")]
    [Tooltip(
        "Leaderboard semantic refresh rate. " +
        "0.05 = 20 updates/sec. No visual animation is applied.")]
    [Min(0.01f)]
    [SerializeField] private float refreshRate = 0.05f;

    [Tooltip(
        "How often missing runtime player bindings are searched for.")]
    [Min(0.05f)]
    [SerializeField] private float playerBindingRetryRate = 0.5f;

    [Tooltip(
        "How often each player's ChainCartCargo component cache is rebuilt, " +
        "even if cart count has not changed. This also catches pending carts.")]
    [Min(0.05f)]
    [SerializeField] private float cargoBindingRefreshRate = 0.5f;

    #endregion

    #region Runtime Cache

    private readonly int[] committedBankedScores =
        new int[MaxPlayerSlots];

    private readonly int[] checkoutSubmittedScores =
        new int[MaxPlayerSlots];

    private readonly int[] potentialScores =
        new int[MaxPlayerSlots];

    private readonly int[] projectedStreakRewardScores =
        new int[MaxPlayerSlots];

    private readonly int[] ranks =
        new int[MaxPlayerSlots];

    private readonly int[] displayOrder =
        new int[MaxPlayerSlots]
        {
            1, 2, 3, 4
        };

    private readonly ChainCartCargo[][] cachedCargoByPlayer =
        new ChainCartCargo[MaxPlayerSlots][];

    private readonly int[] cachedOwnedCartCounts =
        new int[MaxPlayerSlots]
        {
            -1, -1, -1, -1
        };

    private readonly float[] nextCargoBindingRefreshTimes =
        new float[MaxPlayerSlots];

    private float nextRefreshTime;
    private float nextPlayerBindingRetryTime;
    private int currentGlobalMaximum;

    #endregion

    #region Unity

    private void Awake()
    {
        EnsurePlayerArray();
        ResolveStaticReferences();
    }

    private void Start()
    {
        TryBindRuntimePlayers(force: true);
        RefreshLeaderboard();
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextPlayerBindingRetryTime)
        {
            TryBindRuntimePlayers(force: false);

            nextPlayerBindingRetryTime =
                Time.unscaledTime +
                playerBindingRetryRate;
        }

        if (Time.unscaledTime < nextRefreshTime) return;

        nextRefreshTime =
            Time.unscaledTime +
            refreshRate;

        RefreshLeaderboard();
    }

    private void OnValidate()
    {
        refreshRate = Mathf.Max(0.01f, refreshRate);

        playerBindingRetryRate =
            Mathf.Max(0.05f, playerBindingRetryRate);

        cargoBindingRefreshRate =
            Mathf.Max(0.05f, cargoBindingRefreshRate);
    }

    #endregion

    #region Refresh

    private void RefreshLeaderboard()
    {
        if (cashScoreManager == null ||
            stateSystem == null ||
            profile == null)
        {
            ResolveStaticReferences();

            if (cashScoreManager == null ||
                stateSystem == null ||
                profile == null)
            {
                return;
            }
        }

        int activePlayerCount =
            Mathf.Clamp(
                GMode.Instance != null
                    ? GMode.Instance.PlayerCount()
                    : 2,
                1,
                MaxPlayerSlots
            );

        int highestCombinedScore = 0;

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            bool active = i < activePlayerCount;

            if (!active)
            {
                committedBankedScores[i] = 0;
                checkoutSubmittedScores[i] = 0;
                potentialScores[i] = 0;
                projectedStreakRewardScores[i] = 0;
                ranks[i] = 0;
                displayOrder[i] = i + 1;
                continue;
            }

            int playerIndex = i + 1;

            committedBankedScores[i] =
                Mathf.Max(
                    0,
                    cashScoreManager.GetPlayerScore(
                        playerIndex
                    )
                );

            if (gameTimeManager != null && gameTimeManager.IsPostGame)
            {
                // Results are final: unsold carried cargo and an interrupted
                // checkout projection must not appear in the banked-score bar.
                checkoutSubmittedScores[i] = 0;
                potentialScores[i] = 0;
                projectedStreakRewardScores[i] = 0;
            }
            else
            {
                GetCheckoutScoreBreakdown(
                    playerIndex,
                    out checkoutSubmittedScores[i],
                    out potentialScores[i],
                    out projectedStreakRewardScores[i]
                );
            }

            int combined =
                committedBankedScores[i] +
                checkoutSubmittedScores[i] +
                potentialScores[i];

            if (combined > highestCombinedScore)
            {
                highestCombinedScore = combined;
            }

            displayOrder[i] = playerIndex;
        }

        BuildBankedScoreRanking(
            activePlayerCount
        );

        int tieredGlobalMaximum =
            RaiseGlobalMaximumTier(
                highestCombinedScore
            );

        stateSystem.SetLeaderboard(
            activePlayerCount,
            tieredGlobalMaximum,
            committedBankedScores,
            checkoutSubmittedScores,
            potentialScores,
            projectedStreakRewardScores,
            ranks,
            displayOrder
        );
    }

    /// <summary>
    /// Ranking is VISIBLE BANKED score only. During an active checkout this is
    /// committed score plus base points already submitted through checkout.
    /// Still-carried potential and the projected streak reward never affect rank.
    ///
    /// Tie behavior intentionally matches the previous leaderboard:
    /// dense ranking (10,10,5 -> 1st,1st,2nd).
    /// Tied rows are kept stable by player index.
    /// </summary>
    private void BuildBankedScoreRanking(
        int activePlayerCount)
    {
        for (int i = 0; i < activePlayerCount; i++)
        {
            displayOrder[i] = i + 1;
        }

        // Tiny fixed set: simple insertion sort is allocation-free and clear.
        for (int i = 1; i < activePlayerCount; i++)
        {
            int keyPlayerIndex = displayOrder[i];
            int keyScore =
                GetVisibleBankedScore(
                    keyPlayerIndex - 1
                );

            int j = i - 1;

            while (j >= 0)
            {
                int comparedPlayerIndex =
                    displayOrder[j];

                int comparedScore =
                    GetVisibleBankedScore(
                        comparedPlayerIndex - 1
                    );

                bool keyShouldComeBefore =
                    keyScore > comparedScore ||
                    (
                        keyScore == comparedScore &&
                        keyPlayerIndex <
                        comparedPlayerIndex
                    );

                if (!keyShouldComeBefore) break;

                displayOrder[j + 1] =
                    comparedPlayerIndex;

                j--;
            }

            displayOrder[j + 1] =
                keyPlayerIndex;
        }

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            ranks[i] = 0;
        }

        int currentRank = 1;
        int previousScore = int.MinValue;

        for (int row = 0; row < activePlayerCount; row++)
        {
            int playerIndex =
                displayOrder[row];

            int score =
                GetVisibleBankedScore(
                    playerIndex - 1
                );

            if (row == 0)
            {
                currentRank = 1;
            }
            else if (score < previousScore)
            {
                currentRank++;
            }

            ranks[playerIndex - 1] =
                currentRank;

            previousScore = score;
        }
    }

    #endregion

    #region Global Maximum Tier

    /// <summary>
    /// Keeps one monotonic shared maximum for the match. It only grows when a
    /// combined score no longer fits, and grows in profile-authored increments.
    /// Example: start 20, increment 20 -> 20, 40, 60, 80...
    /// </summary>
    private int RaiseGlobalMaximumTier(
        int requiredCombinedScore)
    {
        int startingMaximum =
            profile.LeaderboardStartingGlobalMaximum;

        int increment =
            profile.LeaderboardGlobalMaximumIncrement;

        currentGlobalMaximum =
            Mathf.Max(
                currentGlobalMaximum,
                startingMaximum
            );

        if (requiredCombinedScore <=
            currentGlobalMaximum)
        {
            return currentGlobalMaximum;
        }

        int amountAboveStart =
            requiredCombinedScore -
            startingMaximum;

        int tiersAboveStart =
            Mathf.CeilToInt(
                (float)amountAboveStart /
                increment
            );

        int requiredTier =
            startingMaximum +
            tiersAboveStart *
            increment;

        currentGlobalMaximum =
            Mathf.Max(
                currentGlobalMaximum,
                requiredTier
            );

        return currentGlobalMaximum;
    }

    private int GetVisibleBankedScore(
        int playerArrayIndex)
    {
        return
            committedBankedScores[playerArrayIndex] +
            checkoutSubmittedScores[playerArrayIndex];
    }

    #endregion

    #region Potential Score

    private void GetCheckoutScoreBreakdown(
        int playerIndex,
        out int checkoutSubmittedScore,
        out int remainingPotentialScore,
        out int projectedStreakRewardScore)
    {
        checkoutSubmittedScore = 0;
        remainingPotentialScore = 0;
        projectedStreakRewardScore = 0;

        int index = playerIndex - 1;

        if (index < 0 ||
            index >= MaxPlayerSlots)
        {
            return;
        }

        SnakeCartManager snakeManager =
            playerSnakeManagers[index];

        int remainingCargoCount = 0;
        int remainingBaseScore = 0;

        if (snakeManager != null)
        {
            int currentOwnedCartCount =
                snakeManager.GetSnakeBodyLength();

            bool cartCountChanged =
                currentOwnedCartCount !=
                cachedOwnedCartCounts[index];

            bool periodicRefreshDue =
                Time.unscaledTime >=
                nextCargoBindingRefreshTimes[index];

            if (cachedCargoByPlayer[index] == null ||
                cartCountChanged ||
                periodicRefreshDue)
            {
                RebuildCargoCacheForPlayer(
                    index,
                    snakeManager,
                    currentOwnedCartCount
                );
            }

            ChainCartCargo[] cargoComponents =
                cachedCargoByPlayer[index];

            if (cargoComponents != null)
            {
                for (int i = 0;
                     i < cargoComponents.Length;
                     i++)
                {
                    ChainCartCargo cargo =
                        cargoComponents[i];

                    if (cargo == null) continue;

                    remainingCargoCount +=
                        Mathf.Max(
                            0,
                            cargo.CargoEntryCount
                        );

                    remainingBaseScore +=
                        Mathf.Max(
                            0,
                            cargo.LocalScoreValue
                        );
                }
            }
        }

        // Processed cargo leaves the chain before CashScoreManager commits the
        // full session. Treat its base points as visible banked progress while
        // keeping the projected milestone reward in the remaining potential.
        int alreadySubmittedCargoCount = 0;

        CashScoreManager.CheckoutSessionData
            currentSession =
                cashScoreManager
                    .GetCurrentSessionData(
                        playerIndex
                    );

        if (currentSession != null &&
            currentSession.isActive)
        {
            alreadySubmittedCargoCount =
                Mathf.Max(
                    0,
                    currentSession.cargoCount
                );

            checkoutSubmittedScore =
                Mathf.Max(
                    0,
                    Mathf.RoundToInt(
                        currentSession.basePoints
                    )
                );
        }

        int projectedCargoCount =
            alreadySubmittedCargoCount +
            remainingCargoCount;

        projectedStreakRewardScore =
            Mathf.Max(
                0,
                cashScoreManager
                    .GetProjectedBonusPoints(
                        projectedCargoCount
                    )
            );

        remainingPotentialScore =
            remainingBaseScore +
            projectedStreakRewardScore;
    }

    private void RebuildCargoCacheForPlayer(
        int playerArrayIndex,
        SnakeCartManager snakeManager,
        int currentOwnedCartCount)
    {
        cachedCargoByPlayer[playerArrayIndex] =
            snakeManager
                .GetComponentsInChildren<
                    ChainCartCargo
                >(true);

        cachedOwnedCartCounts[playerArrayIndex] =
            currentOwnedCartCount;

        nextCargoBindingRefreshTimes[
            playerArrayIndex
        ] =
            Time.unscaledTime +
            cargoBindingRefreshRate;
    }

    #endregion

    #region Binding

    private void ResolveStaticReferences()
    {
        if (cashScoreManager == null)
        {
            cashScoreManager =
                FindFirstObjectByType<
                    CashScoreManager
                >();
        }

        if (gameTimeManager == null)
        {
            gameTimeManager =
                FindFirstObjectByType<
                    GameTimeManager
                >();
        }

        if (stateSystem == null)
        {
            stateSystem =
                MatchViewportOverlayStateSystem
                    .Instance;
        }

        if (stateSystem == null)
        {
            stateSystem =
                FindFirstObjectByType<
                    MatchViewportOverlayStateSystem
                >();
        }
    }

    private void TryBindRuntimePlayers(
        bool force)
    {
        EnsurePlayerArray();

        bool needsSearch = force;

        for (int i = 0;
             i < MaxPlayerSlots;
             i++)
        {
            if (playerSnakeManagers[i] == null)
            {
                needsSearch = true;
                break;
            }
        }

        if (!needsSearch) return;

        SnakeCartManager[] found =
            FindObjectsByType<
                SnakeCartManager
            >(FindObjectsSortMode.None);

        for (int i = 0;
             i < found.Length;
             i++)
        {
            SnakeCartManager manager =
                found[i];

            if (manager == null) continue;

            int playerIndex =
                manager.GetPlayerId();

            int arrayIndex =
                playerIndex - 1;

            if (arrayIndex < 0 ||
                arrayIndex >= MaxPlayerSlots)
            {
                continue;
            }

            if (playerSnakeManagers[arrayIndex] ==
                manager)
            {
                continue;
            }

            playerSnakeManagers[arrayIndex] =
                manager;

            InvalidateCargoCache(
                arrayIndex
            );
        }
    }

    private void InvalidateCargoCache(
        int playerArrayIndex)
    {
        cachedCargoByPlayer[
            playerArrayIndex
        ] = null;

        cachedOwnedCartCounts[
            playerArrayIndex
        ] = -1;

        nextCargoBindingRefreshTimes[
            playerArrayIndex
        ] = 0f;
    }

    private void EnsurePlayerArray()
    {
        if (playerSnakeManagers != null &&
            playerSnakeManagers.Length ==
            MaxPlayerSlots)
        {
            return;
        }

        SnakeCartManager[] resized =
            new SnakeCartManager[
                MaxPlayerSlots
            ];

        if (playerSnakeManagers != null)
        {
            int copyCount =
                Mathf.Min(
                    playerSnakeManagers.Length,
                    MaxPlayerSlots
                );

            for (int i = 0;
                 i < copyCount;
                 i++)
            {
                resized[i] =
                    playerSnakeManagers[i];
            }
        }

        playerSnakeManagers = resized;
    }

    #endregion
}
