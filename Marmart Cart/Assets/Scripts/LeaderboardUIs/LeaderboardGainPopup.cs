using System.Collections;
using TMPro;
using UnityEngine;

public class LeaderboardGainPopup : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CashScoreManager cashScoreManager;

    [Header("FFA / Duel Anchor (local player's row)")]
    [SerializeField] private TMP_Text popupTMP;     // "+123"
    [SerializeField] private TMP_Text scoreTMP;     // anchor position

    [Header("Team Mode Anchor (your team row)")]
    [SerializeField] private TMP_Text teamPopupTMP; // "+123" on Team1/Team2 row
    [SerializeField] private TMP_Text teamScoreTMP; // anchor position on Team1/Team2 row

    [Header("Identity")]
    [Range(1, 4)]
    [SerializeField] private int localPlayerIndex = 1;

    [Header("Team Mapping (must match CashScoreManager)")]
    [SerializeField] private int[] team1Players = new[] { 1, 3 };
    [SerializeField] private int[] team2Players = new[] { 2, 4 };

    [Header("Anim")]
    [SerializeField] private float riseY = 22f;
    [SerializeField] private float duration = 0.9f;

    private Coroutine animRoutine;
    void Awake()
    {
        if (!cashScoreManager)
            cashScoreManager = FindFirstObjectByType<CashScoreManager>();

        HideAllPopups();
    }

    void OnEnable()
    {
        if (!cashScoreManager) return;

        cashScoreManager.OnPlayerScoreGained += HandlePlayerGain;
        cashScoreManager.OnTeamScoreGained += HandleTeamGain;
    }

    void OnDisable()
    {
        if (!cashScoreManager) return;

        cashScoreManager.OnPlayerScoreGained -= HandlePlayerGain;
        cashScoreManager.OnTeamScoreGained -= HandleTeamGain;
    }

    private void HandlePlayerGain(int playerIndex, int gain, int newTotal)
    {
        if (GMode.Instance != null && GMode.Instance.IsTeamBattle)
            return;

        if (playerIndex != localPlayerIndex)
            return;

        Play(gain, useTeamAnchor: false);
    }

    private void HandleTeamGain(int teamIndex, int gain, int teamTotal)
    {
        if (GMode.Instance == null || !GMode.Instance.IsTeamBattle)
            return;

        int localTeam = GetTeamIndexForPlayer(localPlayerIndex);
        if (teamIndex != localTeam)
            return;

        Play(gain, useTeamAnchor: true);
    }

    private void Play(int gain, bool useTeamAnchor)
    {
        var popup = useTeamAnchor ? teamPopupTMP : popupTMP;
        var score = useTeamAnchor ? teamScoreTMP : scoreTMP;

        // If team refs aren't wired, fall back to default refs (won't crash)
        if (popup == null) popup = popupTMP;
        if (score == null) score = scoreTMP;

        if (popup == null) return; // still nothing to show

        if (animRoutine != null)
            StopCoroutine(animRoutine);

        animRoutine = StartCoroutine(AnimateGain(popup, score, gain));
    }

    private IEnumerator AnimateGain(TMP_Text popup, TMP_Text anchorScore, int gain)
    {
        popup.gameObject.SetActive(true);
        popup.text = "+" + gain;
        popup.alpha = 1f;

        RectTransform rt = popup.rectTransform;

        Vector2 origin = rt.anchoredPosition;
        if (anchorScore != null)
            origin = anchorScore.rectTransform.anchoredPosition;

        Vector2 start = origin;
        Vector2 end = origin + new Vector2(0f, riseY);

        float t = 0f;
        while (t < duration)
        {
            float a = Mathf.Clamp01(t / duration);
            float eased = 1f - Mathf.Pow(1f - a, 3f);

            rt.anchoredPosition = Vector2.Lerp(start, end, eased);

            if (a > 0.75f)
                popup.alpha = Mathf.Lerp(1f, 0f, (a - 0.75f) / 0.25f);

            t += Time.deltaTime;
            yield return null;
        }

        rt.anchoredPosition = origin;
        popup.gameObject.SetActive(false);
        animRoutine = null;
    }

    private void HideAllPopups()
    {
        if (popupTMP) popupTMP.gameObject.SetActive(false);
        if (teamPopupTMP) teamPopupTMP.gameObject.SetActive(false);
    }

    private int GetTeamIndexForPlayer(int playerIndex)
    {
        if (team1Players != null)
            for (int i = 0; i < team1Players.Length; i++)
                if (team1Players[i] == playerIndex) return 1;

        if (team2Players != null)
            for (int i = 0; i < team2Players.Length; i++)
                if (team2Players[i] == playerIndex) return 2;

        return 0;
    }

    public void SetLocalPlayerIndex(int idx)
    {
        localPlayerIndex = Mathf.Clamp(idx, 1, 4);
    }
}