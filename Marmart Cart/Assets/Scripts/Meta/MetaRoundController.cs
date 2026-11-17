using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene-local Round loop. One Round is one load of the gameplay scene.
///
/// - On load it logs the Round held in MatchSession.
/// - When the score reveal ends it works out the Round winners, holds the
///   result for a set time, then commits the Round and reloads the active scene.
/// - The final Round is not committed, because no next Round loads. The
///   controller logs the Match result and waits for staff; a reload replays
///   that Round.
///
/// Staff keys, keyboard only: F1 Restart Match, F2 Restart Round, F3 Skip Round.
/// </summary>
[DisallowMultipleComponent]
public sealed class MetaRoundController : MonoBehaviour
{
    private const int MaxPlayers = MatchSession.MaxPlayers;

    private enum RoundPhase
    {
        InRound,
        RoundResult,
        MatchOver,
        Loading
    }

    [Header("References")]
    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private MatchResultsPresentationController resultsPresentation;

    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private CashScoreManager cashScoreManager;

    [Header("Round Flow")]
    [Tooltip(
        "Real-time seconds the Round result is held after the score reveal, " +
        "before the next Round loads. After the final Round the Match result follows instead."
    )]
    [Min(0f)]
    [SerializeField] private float roundResultDuration = 3f;

    [Header("Staff Keys")]
    [Tooltip("Keyboard F1 Restart Match, F2 Restart Round, F3 Skip Round.")]
    [SerializeField] private bool listenForStaffKeys = true;

    [Header("Runtime - Read Only")]
    [SerializeField] private RoundPhase phase = RoundPhase.InRound;
    [SerializeField] private int roundNumber = 1;

    private readonly int[] roundBanked = new int[MaxPlayers];
    private readonly bool[] roundWinners = new bool[MaxPlayers];
    private readonly int[] finalMatchPoints = new int[MaxPlayers];
    private readonly bool[] matchWinners = new bool[MaxPlayers];

    private int sceneBuildIndex = -1;
    private float roundResultEndTime;

    // Diagnostic only: when this controller requested a load, so the next load can log its duration.
    private static float loadRequestedRealtime = -1f;

    private static int ActivePlayerCount =>
        Mathf.Clamp(GMode.Instance != null ? GMode.Instance.PlayerCount() : 2, 1, MaxPlayers);

    #region Unity

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        loadRequestedRealtime = -1f;
    }

    private void Awake()
    {
        if (resultsPresentation == null)
            resultsPresentation = FindFirstObjectByType<MatchResultsPresentationController>();

        if (cashScoreManager == null)
            cashScoreManager = FindFirstObjectByType<CashScoreManager>();

        roundNumber = MatchSession.RoundNumber;
        phase = RoundPhase.InRound;
    }

    private void OnEnable()
    {
        if (resultsPresentation != null)
            resultsPresentation.OnAllScoreRevealsCompleted += HandleAllScoreRevealsCompleted;
    }

    private void OnDisable()
    {
        if (resultsPresentation != null)
            resultsPresentation.OnAllScoreRevealsCompleted -= HandleAllScoreRevealsCompleted;
    }

    private void Start()
    {
        sceneBuildIndex = SceneManager.GetActiveScene().buildIndex;

        if (sceneBuildIndex < 0)
        {
            Debug.LogError("[Meta] The active scene is not in Build Settings, so Rounds cannot reload. Disabled.", this);
            enabled = false;
            return;
        }

        if (resultsPresentation == null)
            Debug.LogError("[Meta] MatchResultsPresentationController not found. Rounds will not advance.", this);

        if (cashScoreManager == null)
            Debug.LogError("[Meta] CashScoreManager not found. Banked values will read as 0.", this);

        string loadTime = string.Empty;

        if (loadRequestedRealtime >= 0f)
        {
            loadTime = $" | Load took {Time.realtimeSinceStartup - loadRequestedRealtime:0.00} s";
            loadRequestedRealtime = -1f;
        }

        int playerCount = ActivePlayerCount;

        Debug.Log(
            $"[Meta] Round {roundNumber} of {MatchSession.RoundsPerMatch} loaded | " +
            $"Match Points {FormatPerPlayer(playerCount, MatchSession.GetMatchPoints)} | " +
            $"Banked so far {FormatPerPlayer(playerCount, MatchSession.GetTotalBanked)}{loadTime}",
            this
        );
    }

    private void Update()
    {
        if (phase == RoundPhase.Loading) return;

        if (listenForStaffKeys) HandleStaffKeys();

        if (phase == RoundPhase.RoundResult && Time.unscaledTime >= roundResultEndTime)
            FinishRound();
    }

    #endregion

    #region Round Flow

    private void HandleAllScoreRevealsCompleted()
    {
        if (phase != RoundPhase.InRound) return;

        int playerCount = ActivePlayerCount;

        for (int i = 0; i < MaxPlayers; i++)
        {
            roundBanked[i] = i < playerCount && cashScoreManager != null
                ? Mathf.Max(0, cashScoreManager.GetPlayerScore(i + 1))
                : 0;
        }

        WorkOutWinners(roundBanked, playerCount, roundWinners);

        roundResultEndTime = Time.unscaledTime + roundResultDuration;
        phase = RoundPhase.RoundResult;

        string next = MatchSession.IsFinalRound ? "Match result" : $"Round {roundNumber + 1}";

        Debug.Log(
            $"[Meta] Round {roundNumber} result | Banked {FormatPerPlayer(playerCount, p => roundBanked[p - 1])} | " +
            $"Winners {FormatWinners(roundWinners, playerCount)} | {next} in {roundResultDuration:0.#} s",
            this
        );
    }

    private void FinishRound()
    {
        if (MatchSession.IsFinalRound)
        {
            EnterMatchOver();
            return;
        }

        int playerCount = ActivePlayerCount;
        MatchSession.CommitRound(roundBanked, roundWinners, playerCount);

        Debug.Log(
            $"[Meta] Round {roundNumber} committed | " +
            $"Match Points {FormatPerPlayer(playerCount, MatchSession.GetMatchPoints)} | " +
            $"Loading Round {MatchSession.RoundNumber}",
            this
        );

        LoadThisScene();
    }

    private void EnterMatchOver()
    {
        phase = RoundPhase.MatchOver;

        int playerCount = ActivePlayerCount;

        for (int i = 0; i < MaxPlayers; i++)
        {
            finalMatchPoints[i] = i < playerCount
                ? MatchSession.GetMatchPoints(i + 1) + (roundWinners[i] ? 1 : 0)
                : 0;
        }

        WorkOutWinners(finalMatchPoints, playerCount, matchWinners);

        Debug.Log(
            $"[Meta] Match over | Match Points {FormatPerPlayer(playerCount, p => finalMatchPoints[p - 1])} | " +
            $"Match winners {FormatWinners(matchWinners, playerCount)} | " +
            $"Round {roundNumber} is not saved: F1 starts a new Match, F2 replays this Round.",
            this
        );
    }

    // Every player tied on the highest value wins. Nobody wins when that value is 0.
    private static void WorkOutWinners(int[] values, int playerCount, bool[] winners)
    {
        int best = 0;

        for (int i = 0; i < playerCount; i++)
        {
            if (values[i] > best) best = values[i];
        }

        for (int i = 0; i < winners.Length; i++)
        {
            winners[i] = best > 0 && i < playerCount && values[i] == best;
        }
    }

    #endregion

    #region Staff Keys

    private void HandleStaffKeys()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.f1Key.wasPressedThisFrame)
            RestartMatch();
        else if (keyboard.f2Key.wasPressedThisFrame)
            RestartRound();
        else if (keyboard.f3Key.wasPressedThisFrame)
            SkipRound();
    }

    private void RestartMatch()
    {
        Debug.Log("[Meta] F1 Restart Match.", this);
        MatchSession.Clear();
        LoadThisScene();
    }

    private void RestartRound()
    {
        Debug.Log($"[Meta] F2 Restart Round {roundNumber}. Nothing awarded.", this);
        LoadThisScene();
    }

    private void SkipRound()
    {
        if (!MatchSession.SkipRound())
        {
            Debug.Log($"[Meta] F3 ignored: Round {roundNumber} is the final Round. F1 restarts the Match.", this);
            return;
        }

        Debug.Log(
            $"[Meta] F3 Skip Round {roundNumber}. No Match Point awarded. Loading Round {MatchSession.RoundNumber}",
            this
        );

        LoadThisScene();
    }

    private void LoadThisScene()
    {
        phase = RoundPhase.Loading;
        Time.timeScale = 1f;
        loadRequestedRealtime = Time.realtimeSinceStartup;
        SceneManager.LoadScene(sceneBuildIndex, LoadSceneMode.Single);
    }

    #endregion

    #region Log Formatting

    private static string FormatPerPlayer(int playerCount, Func<int, int> valueOf)
    {
        StringBuilder text = new StringBuilder(48);

        for (int playerIndex = 1; playerIndex <= playerCount; playerIndex++)
        {
            if (playerIndex > 1) text.Append(", ");
            text.Append('P').Append(playerIndex).Append(' ').Append(valueOf(playerIndex));
        }

        return text.ToString();
    }

    private static string FormatWinners(bool[] winners, int playerCount)
    {
        StringBuilder text = new StringBuilder(24);

        for (int i = 0; i < playerCount; i++)
        {
            if (!winners[i]) continue;
            if (text.Length > 0) text.Append(", ");
            text.Append('P').Append(i + 1);
        }

        return text.Length > 0 ? text.ToString() : "none";
    }

    #endregion
}
