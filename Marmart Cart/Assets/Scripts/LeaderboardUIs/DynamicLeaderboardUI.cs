using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SocialPlatforms;

public class DynamicLeaderboardUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CashScoreManager cashScoreManager;
    [SerializeField] private RectTransform container;
    [SerializeField] private LeaderboardEntryUI[] entries; // wire 4 entries in inspector (P1..P4)

    [Header("Per Screen Settings")]
    [Range(1, 4)]
    [SerializeField] private int localPlayerIndex = 1; // this screen belongs to which player

    [Header("Scores")]
    [SerializeField] private int maxScore = 12500;

    [Header("Layout")]
    [SerializeField] private float reorderAnimTime = 0.18f;
    [SerializeField] private float refreshRate = 0.10f;

    [Header("Mode Scaling")]
    [SerializeField] private float duelScale = 2.0f;

    [Header("Team Mode Mapping")]
    [Tooltip("Which players belong to Team 1 (blue). Example: 1,3")]
    [SerializeField] private int[] team1Players = new int[] { 1, 3 };

    [Tooltip("Which players belong to Team 2 (red). Example: 2,4")]
    [SerializeField] private int[] team2Players = new int[] { 2, 4 };

    private Coroutine _animRoutine;
    private readonly Dictionary<int, Vector2> _targetPos = new();
    private Dictionary<int, Vector2> _slotPosByRank = new(); // rank (1..N) -> anchoredPosition

    private void Awake()
    {
        
    }
    private void Start()
    {
        ApplyModeLayout();
        SnapToCurrentOrder();
        CacheManualSlotPositions();
    }

    private void OnEnable()
    {
        StartCoroutine(RefreshLoop());
    }
    private void CacheManualSlotPositions()
    {
        _slotPosByRank.Clear();

        // Use whichever entries are ACTIVE in this mode as the "slots"
        var active = entries.Where(e => e && e.gameObject.activeInHierarchy).ToList();

        // IMPORTANT: the order in this list defines rank slots.
        for (int i = 0; i < active.Count; i++)
        {
            int rank = i + 1;
            _slotPosByRank[rank] = active[i].Rect.anchoredPosition;
        }
    }
    private IEnumerator RefreshLoop()
    {
        while (enabled)
        {
            TickUpdate(immediate: false);
            yield return new WaitForSeconds(refreshRate);
        }
    }
    private void ApplyModeLayout()
    {
        // default
        transform.localScale = Vector3.one;

        if (GMode.Instance.IsTwoPlayer)
        {
            transform.localScale = Vector3.one * duelScale;

            SetEntryActive(1, true);
            SetEntryActive(2, true);
            SetEntryActive(3, false);
            SetEntryActive(4, false);

            // Player labels + highlight
            for (int i = 1; i <= 2; i++)
            {
                var e = GetEntry(i);
                if (!e) continue;

                bool isLocal = (i == localPlayerIndex);
                e.SetHighlight(isLocal);
                e.SetLeftLabel(isLocal ? "YOU" : $"Player{i}");
                e.SetScoreVisible(false); // handled in TickUpdate (leader + local)
            }
        }
        else if (GMode.Instance.IsFreeForAll)
        {
            for (int i = 1; i <= 4; i++) SetEntryActive(i, true);

            // Player labels + highlight
            for (int i = 1; i <= 4; i++)
            {
                var e = GetEntry(i);
                if (!e) continue;

                bool isLocal = (i == localPlayerIndex);
                e.SetHighlight(isLocal);
                e.SetLeftLabel(isLocal ? "YOU" : $"Player{i}");
                e.SetScoreVisible(false); // handled in TickUpdate (leader + local)
            }
        }
        else if (GMode.Instance.IsTeamBattle)
        {
            // Use entry 1 and 2 as Team 1 and Team 2
            SetEntryActive(1, true);
            SetEntryActive(2, true);
            SetEntryActive(3, false);
            SetEntryActive(4, false);

            var e1 = GetEntry(1);
            var e2 = GetEntry(2);

            int localTeam = GetTeamIndexForPlayer(localPlayerIndex); // 1 or 2 (or 0 if not found)

            // Labels: show YOU for your team, Team X for the other team
            if (e1 != null) e1.SetLeftLabel(localTeam == 1 ? "YOU" : "Team 1");
            if (e2 != null) e2.SetLeftLabel(localTeam == 2 ? "YOU" : "Team 2");

            // Highlight your team
            if (e1 != null) e1.SetHighlight(localTeam == 1);
            if (e2 != null) e2.SetHighlight(localTeam == 2);

            // Show team scores
            if (e1 != null) e1.SetScoreVisible(true);
            if (e2 != null) e2.SetScoreVisible(true);
        }

        CacheManualSlotPositions();
    }

    // Returns 1 if player is in team1Players, 2 if in team2Players, else 0
    private int GetTeamIndexForPlayer(int playerIndex)
    {
        if (team1Players != null)
        {
            for (int i = 0; i < team1Players.Length; i++)
                if (team1Players[i] == playerIndex) return 1;
        }

        if (team2Players != null)
        {
            for (int i = 0; i < team2Players.Length; i++)
                if (team2Players[i] == playerIndex) return 2;
        }

        return 0;
    }

    private void TickUpdate(bool immediate)
    {
        if (!GMode.Instance) return;
        if (!cashScoreManager) return;
        if (!cashScoreManager) return;

        if (GMode.Instance.IsTeamBattle)
        {
            UpdateTeamMode(immediate);
            return;
        }

        int count = GMode.Instance.PlayerCount();
        var ids = (count == 2) ? new List<int> { 1, 2 } : new List<int> { 1, 2, 3, 4 };

        // scores
        var scored = ids
            .Select(id => (id: id, score: cashScoreManager.GetPlayerScore(id)))
            .ToList();

        // Dynamic max = current leader score (avoid divide by 0)
        int leaderScore = scored.Max(x => x.score);

        if (leaderScore <= 0)
        {
            // Game start: everyone is 0, show full bars
            foreach (var s in scored)
            {
                var e = GetEntry(s.id);
                if (!e) continue;
                e.SetBarNormalized(1f);
            }
        }
        else
        {
            int dynamicMax = leaderScore; // leader always full
            foreach (var s in scored)
            {
                var e = GetEntry(s.id);
                if (!e) continue;

                float score01 = (float)s.score / dynamicMax;
                e.SetBarNormalized(score01);
            }
        }

        // Ranking with ties
        var ranked = BuildRanking(scored);

        // Apply rank text + crowns (leaders)
        foreach (var r in ranked)
        {
            var e = GetEntry(r.id);
            if (!e) continue;

            e.SetRank(RankToString(r.rank), r.isLeader); // crown on ALL leaders
        }

        // Show score text:
        // - always for local player
        // - always for ALL leaders (ties included, including at 0)
        foreach (var r in ranked)
        {
            var e = GetEntry(r.id);
            if (!e) continue;

            bool showScore = (r.id == localPlayerIndex) || r.isLeader;
            e.SetScoreVisible(showScore);
            if (showScore) e.SetScoreValue(r.score);
        }

        // Order animation: ties can be in any order.
        // We'll keep the sorted order (stable) — doesn't matter as you said.
        var orderedIds = ranked.Select(x => x.id).ToList();
        MoveToOrder(orderedIds, immediate);
    }

    private void UpdateTeamMode(bool immediate)
    {
        int team1Score = cashScoreManager.GetTeamScore(team1Players);
        int team2Score = cashScoreManager.GetTeamScore(team2Players);

        int leaderScore = Mathf.Max(team1Score, team2Score);

        if (leaderScore <= 0)
        {
            // Game start: both teams 0, show full bars
            GetEntry(1)?.SetBarNormalized(1f);
            GetEntry(2)?.SetBarNormalized(1f);
        }
        else
        {
            int dynamicMax = leaderScore; // leader team always full
            GetEntry(1)?.SetBarNormalized((float)team1Score / dynamicMax);
            GetEntry(2)?.SetBarNormalized((float)team2Score / dynamicMax);
        }

        var scoredTeams = new List<(int id, int score)>
        {
            (1, team1Score),
            (2, team2Score)
        };

        var rankedTeams = BuildRanking(scoredTeams);

        foreach (var r in rankedTeams)
        {
            var e = GetEntry(r.id);
            if (!e) continue;
            e.SetRank(RankToString(r.rank), r.isLeader);
            e.SetScoreVisible(true);
            e.SetScoreValue(r.score);
        }

        MoveToOrder(rankedTeams.Select(x => x.id).ToList(), immediate);
    }
    private struct RankedEntry
    {
        public int id;
        public int score;
        public int rank;
        public bool isLeader;
    }

    private List<RankedEntry> BuildRanking(List<(int id, int score)> scored)
    {
        // Sort descending by score
        var sorted = scored.OrderByDescending(x => x.score).ToList();

        int topScore = sorted.Count > 0 ? sorted[0].score : 0;

        var result = new List<RankedEntry>(sorted.Count);

        int currentRank = 1;
        int prevScore = int.MinValue;

        foreach (var s in sorted)
        {
            if (prevScore == int.MinValue)
            {
                currentRank = 1;
            }
            else if (s.score < prevScore)
            {
                // score dropped -> next rank number increases by 1
                currentRank += 1;
            }
            // else tie -> keep same rank

            result.Add(new RankedEntry
            {
                id = s.id,
                score = s.score,
                rank = currentRank,
                isLeader = (topScore > 0 && s.score == topScore) // excludes 0-0-0-0 at game start
            });

            prevScore = s.score;
        }

        return result;
    }
    private void ApplyRanks(List<int> orderedIds)
    {
        for (int i = 0; i < orderedIds.Count; i++)
        {
            int id = orderedIds[i];
            var e = GetEntry(id);
            if (!e) continue;

            int rank = i + 1;
            e.SetRank(RankToString(rank), rank == 1);
        }
    }

    private string RankToString(int r)
    {
        return r switch
        {
            1 => "1st",
            2 => "2nd",
            3 => "3rd",
            _ => $"{r}th"
        };
    }

    private void MoveToOrder(List<int> orderedIds, bool immediate)
    {
        _targetPos.Clear();

        for (int i = 0; i < orderedIds.Count; i++)
        {
            int id = orderedIds[i];
            int rank = i + 1;

            if (_slotPosByRank.TryGetValue(rank, out var slotPos))
                _targetPos[id] = slotPos;
        }

        if (immediate)
        {
            foreach (var kv in _targetPos)
            {
                var e = GetEntry(kv.Key);
                if (e) e.Rect.anchoredPosition = kv.Value;
            }
            return;
        }

        if (_animRoutine != null) StopCoroutine(_animRoutine);
        _animRoutine = StartCoroutine(AnimateToTargets());
    }

    private IEnumerator AnimateToTargets()
    {
        var start = new Dictionary<int, Vector2>();
        foreach (var kv in _targetPos)
        {
            var e = GetEntry(kv.Key);
            if (e) start[kv.Key] = e.Rect.anchoredPosition;
        }

        float t = 0f;
        while (t < reorderAnimTime)
        {
            t += Time.deltaTime;
            float a = reorderAnimTime <= 0f ? 1f : Mathf.Clamp01(t / reorderAnimTime);
            float eased = 1f - Mathf.Pow(1f - a, 3f);

            foreach (var kv in _targetPos)
            {
                var e = GetEntry(kv.Key);
                if (!e) continue;

                Vector2 s = start.TryGetValue(kv.Key, out var sp) ? sp : e.Rect.anchoredPosition;
                Vector2 target = kv.Value;

                // Y-only animate (keep X stable)
                Vector2 p = Vector2.Lerp(s, target, eased);
                p.x = e.Rect.anchoredPosition.x;

                e.Rect.anchoredPosition = p;
            }

            yield return null;
        }

        foreach (var kv in _targetPos)
        {
            var e = GetEntry(kv.Key);
            if (!e) continue;

            Vector2 p = e.Rect.anchoredPosition;
            p.y = kv.Value.y;
            e.Rect.anchoredPosition = p;
        }

        _animRoutine = null;
    }

    private void SnapToCurrentOrder()
    {
        TickUpdate(immediate: true);
    }

    private LeaderboardEntryUI GetEntry(int id)
    {
        // entries are expected to have their Id set (1..4)
        foreach (var e in entries)
            if (e && e.Id == id) return e;
        return null;
    }

    private void SetEntryActive(int id, bool on)
    {
        var e = GetEntry(id);
        if (e) e.gameObject.SetActive(on);
    }

    public void SetLocalPlayerIndex(int idx)
    {
        localPlayerIndex = Mathf.Clamp(idx, 1, 4);
        ApplyModeLayout();
        SnapToCurrentOrder();
    }
}