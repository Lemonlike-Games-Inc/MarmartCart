using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OverlayLeaderboardUI : MonoBehaviour
{
//    [Header("Refs")]
//    [SerializeField] private CashScoreManager cashScoreManager;
//    [SerializeField] private LeaderboardEntryUI[] entries; // entries[0]=P1, [1]=P2, etc.

//    [Header("Per Screen")]
//    [Range(1, 4)]
//    [SerializeField] private int localPlayerIndex = 1;

//    [Header("Overlay Layout")]
//    [SerializeField] private Vector2 overlayAnchoredPos = Vector2.zero; // where all bars stack
//    [SerializeField] private float refreshRate = 0.10f;

//    private float _timer;

//    private void Awake()
//    {
//        if (!cashScoreManager)
//            cashScoreManager = FindFirstObjectByType<CashScoreManager>();

//        ApplyStaticOverlayPos();
//        ApplyModeVisibility();
//        Refresh(immediate: true);
//    }

//    private void Update()
//    {
//        _timer += Time.deltaTime;
//        if (_timer >= refreshRate)
//        {
//            _timer = 0f;
//            Refresh(immediate: false);
//        }
//    }

//    private void ApplyStaticOverlayPos()
//    {
//        foreach (var e in entries)
//        {
//            if (!e) continue;
//            e.Rect.anchoredPosition = overlayAnchoredPos;
//        }
//    }

//    private void ApplyModeVisibility()
//    {
//        int count = GMode.Instance ? GMode.Instance.PlayerCount() : 2;

//        if (GMode.Instance != null && GMode.Instance.IsTeamBattle)
//        {
//            // Team mode: only keep entries 1 & 2 (Team 1/2)
//            SetActive(1, true);
//            SetActive(2, true);
//            SetActive(3, false);
//            SetActive(4, false);
//        }
//        else
//        {
//            // Duel: 2 bars; FFA: 4 bars
//            SetActive(1, true);
//            SetActive(2, true);
//            SetActive(3, count >= 3);
//            SetActive(4, count >= 4);
//        }
//    }

//    private void Refresh(bool immediate)
//    {
//        if (!cashScoreManager || entries == null || entries.Length == 0) return;

//        if (GMode.Instance != null && GMode.Instance.IsTeamBattle)
//        {
//            RefreshTeams();
//        }
//        else
//        {
//            RefreshPlayers();
//        }
//    }

//    private void RefreshPlayers()
//    {
//        int count = GMode.Instance ? GMode.Instance.PlayerCount() : 2;
//        var ids = (count == 2) ? new List<int> { 1, 2 } : new List<int> { 1, 2, 3, 4 };

//        var scored = ids.Select(id => (id, score: cashScoreManager.GetPlayerScore(id))).ToList();
//        var ranked = BuildRanking(scored);

//        int leaderScore = ranked.Count > 0 ? ranked[0].score : 0;
//        int dynamicMax = Mathf.Max(1, leaderScore); // leader always 100%

//        // Overlay order rule:
//        // leaders should render at bottom -> lower sibling index
//        // non-leaders render above -> higher sibling index
//        // We'll set sibling index based on rank descending (worst on top)
//        // easiest: sort by rank descending so bigger rank number becomes later sibling (on top)
//        var overlayOrder = ranked.OrderByDescending(r => r.rank).ToList();

//        for (int i = 0; i < overlayOrder.Count; i++)
//        {
//            var r = overlayOrder[i];
//            var e = GetEntry(r.id);
//            if (!e) continue;

//            // render order
//            e.transform.SetSiblingIndex(i);

//            // label: YOU vs P#
//            bool isLocal = (r.id == localPlayerIndex);
//            e.SetLeftLabel(isLocal ? "YOU" : $"P{r.id}");
//            e.SetHighlight(isLocal);

//            // handle anchor placement rule:
//            // local uses bottom; others use top
//            e.SetUseBottomAnchors(isLocal);

//            // crown + rank
//            e.SetRank(RankToString(r.rank), r.isLeader);

//            // dynamic max normalized
//            float score01 = (float)r.score / dynamicMax;
//            e.SetBarNormalizedTarget(score01);

//            // score text rule:
//            // show ONLY leaders' score on everyone’s screen (top shared area),
//            // plus local score on local handle area (bottom).
//            bool showScore = isLocal || r.isLeader;
//            e.SetScoreVisible(showScore);
//            if (showScore) e.SetScoreValue(r.score);
//        }
//    }

//    private void RefreshTeams()
//    {
//        // Uses your CashScoreManager team mappings via arrays you already have in UI manager
//        // Simple approach: treat entry 1 = Team 1, entry 2 = Team 2.
//        // You already have team1Players/team2Players inside the cash manager (or in UI manager).
//        // Here we just compute both totals by asking cash manager.

//        int team1 = cashScoreManager.GetTeamScore(new[] { 1, 3 });
//        int team2 = cashScoreManager.GetTeamScore(new[] { 2, 4 });

//        var scored = new List<(int id, int score)>
//        {
//            (1, team1),
//            (2, team2)
//        };

//        var ranked = BuildRanking(scored);

//        int leaderScore = ranked.Count > 0 ? ranked[0].score : 0;
//        int dynamicMax = Mathf.Max(1, leaderScore);

//        var overlayOrder = ranked.OrderByDescending(r => r.rank).ToList();

//        int localTeam = GetTeamIndexForPlayer(localPlayerIndex);

//        for (int i = 0; i < overlayOrder.Count; i++)
//        {
//            var r = overlayOrder[i];
//            var e = GetEntry(r.id);
//            if (!e) continue;

//            e.transform.SetSiblingIndex(i);

//            bool isLocalTeam = (r.id == localTeam);
//            e.SetLeftLabel(isLocalTeam ? "YOU" : $"Team {r.id}");
//            e.SetHighlight(isLocalTeam);

//            e.SetUseBottomAnchors(isLocalTeam);

//            e.SetRank(RankToString(r.rank), r.isLeader);

//            float score01 = (float)r.score / dynamicMax;
//            e.SetBarNormalizedTarget(score01);

//            // Team mode: show team score for YOU only (and leaders if you want)
//            bool showScore = isLocalTeam; // keep it simple
//            e.SetScoreVisible(showScore);
//            if (showScore) e.SetScoreValue(r.score);
//        }
//    }

//    // ---------- ranking with ties ----------
//    private struct RankedEntry
//    {
//        public int id;
//        public int score;
//        public int rank;
//        public bool isLeader;
//    }

//    private List<RankedEntry> BuildRanking(List<(int id, int score)> scored)
//    {
//        var sorted = scored.OrderByDescending(x => x.score).ToList();
//        int topScore = sorted.Count > 0 ? sorted[0].score : 0;

//        var result = new List<RankedEntry>(sorted.Count);
//        int currentRank = 1;
//        int prevScore = int.MinValue;

//        foreach (var s in sorted)
//        {
//            if (prevScore != int.MinValue && s.score < prevScore)
//                currentRank += 1;

//            result.Add(new RankedEntry
//            {
//                id = s.id,
//                score = s.score,
//                rank = currentRank,
//                isLeader = (s.score == topScore) // includes 0 at game start
//            });

//            prevScore = s.score;
//        }

//        return result;
//    }

//    private string RankToString(int r)
//    {
//        return r switch
//        {
//            1 => "1st",
//            2 => "2nd",
//            3 => "3rd",
//            _ => $"{r}th"
//        };
//    }

//    private int GetTeamIndexForPlayer(int playerIndex)
//    {
//        // match your current mapping
//        if (playerIndex == 1 || playerIndex == 3) return 1;
//        if (playerIndex == 2 || playerIndex == 4) return 2;
//        return 0;
//    }

//    private LeaderboardEntryUI GetEntry(int id)
//    {
//        int idx = id - 1;
//        if (idx < 0 || idx >= entries.Length) return null;
//        return entries[idx];
//    }

//    private void SetActive(int id, bool on)
//    {
//        var e = GetEntry(id);
//        if (e) e.gameObject.SetActive(on);
//    }

//    public void SetLocalPlayerIndex(int idx)
//    {
//        localPlayerIndex = Mathf.Clamp(idx, 1, 4);
//        ApplyModeVisibility();
//        ApplyStaticOverlayPos();
//        Refresh(immediate: true);
//    }
}