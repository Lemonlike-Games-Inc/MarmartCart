using System.Collections.Generic;
using UnityEngine;

public class MatchBalanceManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AssistTuningData tuningData;
    [SerializeField] private MonoBehaviour[] playerDataSources;
    // Assign your 4 player scripts here, each must implement IAssistPlayerDataSource

    [Header("Debug")]
    [SerializeField] private bool refreshEveryFrame = true;
    [SerializeField] private bool verboseLogging = false;

    private readonly List<PlayerAssistState> playerStates = new List<PlayerAssistState>(4);
    private readonly List<IAssistPlayerDataSource> cachedSources = new List<IAssistPlayerDataSource>(4);

    public IReadOnlyList<PlayerAssistState> PlayerStates => playerStates;
    public AssistTuningData TuningData => tuningData;

    private void Awake()
    {
        CacheSources();
        BuildInitialStates();
        RefreshStates();
    }

    private void Update()
    {
        if (refreshEveryFrame)
        {
            RefreshStates();
        }
    }

    private void CacheSources()
    {
        cachedSources.Clear();

        for (int i = 0; i < playerDataSources.Length; i++)
        {
            if (playerDataSources[i] is IAssistPlayerDataSource source)
            {
                cachedSources.Add(source);
            }
            else if (playerDataSources[i] != null)
            {
                Debug.LogWarning($"{playerDataSources[i].name} does not implement IAssistPlayerDataSource.");
            }
        }
    }

    private void BuildInitialStates()
    {
        playerStates.Clear();

        for (int i = 0; i < cachedSources.Count; i++)
        {
            PlayerAssistState state = new PlayerAssistState
            {
                playerId = cachedSources[i].GetPlayerId()
            };

            playerStates.Add(state);
        }
    }

    public void RefreshStates()
    {
        if (cachedSources.Count == 0)
            return;

        // Pull fresh live values
        for (int i = 0; i < cachedSources.Count; i++)
        {
            playerStates[i].playerId = cachedSources[i].GetPlayerId();
            playerStates[i].score = cachedSources[i].GetCurrentScore();
            playerStates[i].cartCount = cachedSources[i].GetCurrentCartCount();
        }

        // Compute rank by score descending
        List<PlayerAssistState> sorted = new List<PlayerAssistState>(playerStates);
        sorted.Sort((a, b) => b.score.CompareTo(a.score));

        int firstScore = sorted[0].score;

        for (int rankIndex = 0; rankIndex < sorted.Count; rankIndex++)
        {
            PlayerAssistState s = sorted[rankIndex];
            s.rank = rankIndex + 1;
            s.scoreGapToFirst = Mathf.Max(0, firstScore - s.score);
        }
#if UNITY_EDITOR
        if (verboseLogging)
        {
            for (int i = 0; i < playerStates.Count; i++)
            {
                PlayerAssistState s = playerStates[i];
                Debug.Log($"Player {s.playerId} | Score {s.score} | Carts {s.cartCount} | Rank {s.rank} | GapTo1st {s.scoreGapToFirst}");
            }
        }
#endif
    }

    public PlayerAssistState GetPlayerState(int playerId)
    {
        for (int i = 0; i < playerStates.Count; i++)
        {
            if (playerStates[i].playerId == playerId)
                return playerStates[i];
        }

        return null;
    }

    public int GetFirstPlaceScore()
    {
        if (playerStates.Count == 0)
            return 0;

        int highest = int.MinValue;
        for (int i = 0; i < playerStates.Count; i++)
        {
            if (playerStates[i].score > highest)
                highest = playerStates[i].score;
        }

        return highest;
    }

    public int GetLastPlaceScore()
    {
        if (playerStates.Count == 0)
            return 0;

        int lowest = int.MaxValue;
        for (int i = 0; i < playerStates.Count; i++)
        {
            if (playerStates[i].score < lowest)
                lowest = playerStates[i].score;
        }

        return lowest;
    }

    public AssistEvaluationResult EvaluatePlayerForAssist(int playerId, int currentZoneStock)
    {
        PlayerAssistState state = GetPlayerState(playerId);
        if (state == null)
        {
            return new AssistEvaluationResult
            {
                eligible = false,
                reason = $"No player state found for playerId {playerId}"
            };
        }

        return AssistEvaluator.EvaluatePlayer(
            state,
            GetFirstPlaceScore(),
            GetLastPlaceScore(),
            currentZoneStock,
            tuningData
        );
    }

    public void MarkPlayerClaimedAssist(int playerId)
    {
        PlayerAssistState state = GetPlayerState(playerId);
        if (state != null)
        {
            state.lastAssistClaimTime = Time.time;
        }
    }
}