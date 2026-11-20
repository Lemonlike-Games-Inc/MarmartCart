using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene-local Round loop. One Round is one load of the gameplay scene.
///
/// - On load it logs the Round held in MatchSession.
/// - Pre-round: it takes over the match start from GameTimeManager, shows the
///   Round title and a countdown in real time while PreGame keeps the world
///   frozen, then starts the match. Presentation listens to the pre-round events.
/// - When the score reveal ends it works out the Round winners, holds the
///   result for a set time, then commits the Round and reloads the active scene.
/// - The final Round is not committed, because no next Round loads. The
///   controller logs the Match result and waits for staff; a reload replays
///   that Round.
/// - Screen fade: every load of this scene fades in from black, and every load
///   it starts fades out first. ScreenFadeAlpha is the state; ScreenFadeView shows it.
///
/// Staff keys, keyboard only: F1 Restart Match, F2 Restart Round, F3 Skip Round,
/// F4 Main Menu (leaves the Match).
/// </summary>
[DisallowMultipleComponent]
public sealed class MetaRoundController : MonoBehaviour
{
    private const int MaxPlayers = MatchSession.MaxPlayers;

    private enum RoundPhase
    {
        PreRound,
        InRound,
        RoundResult,
        MatchOver,
        Loading
    }

    private enum LoadTarget
    {
        None,
        ThisScene,
        MainMenu
    }

    public enum PreRoundBeat
    {
        None,
        RoundTitle,
        Countdown,
        Go
    }

    [Header("References")]
    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private MatchResultsPresentationController resultsPresentation;

    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private CashScoreManager cashScoreManager;

    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private GameTimeManager gameTimeManager;

    [Header("Pre-Round (real-time seconds)")]
    [Min(0f)]
    [SerializeField] private float roundTitleDuration = 2.5f;

    [Tooltip("First countdown number. 0 skips the countdown.")]
    [Min(0)]
    [SerializeField] private int countdownFrom = 3;

    [Min(0.1f)]
    [SerializeField] private float countdownStepDuration = 1f;

    [Tooltip("How long the start text stays after the match has started.")]
    [Min(0f)]
    [SerializeField] private float goDuration = 1.5f;

    [Header("Round Flow")]
    [Tooltip(
        "Real-time seconds the Round result is held after the score reveal, " +
        "before the next Round loads. After the final Round the Match result follows instead."
    )]
    [Min(0f)]
    [SerializeField] private float roundResultDuration = 3f;

    [Header("Screen Fade (real-time seconds)")]
    [Tooltip("Fade from black when this scene loads.")]
    [Min(0f)]
    [SerializeField] private float fadeInDuration = 0.5f;

    [Tooltip("Fade to black before any load this controller starts (next Round, staff keys, main menu).")]
    [Min(0f)]
    [SerializeField] private float fadeOutDuration = 0.5f;

    [Header("Staff Keys")]
    [Tooltip("Keyboard F1 Restart Match, F2 Restart Round, F3 Skip Round, F4 Main Menu.")]
    [SerializeField] private bool listenForStaffKeys = true;

    [Tooltip("Scene F4 loads. Must be in Build Settings.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Runtime - Read Only")]
    [SerializeField] private RoundPhase phase = RoundPhase.InRound;
    [SerializeField] private int roundNumber = 1;
    [SerializeField] private PreRoundBeat currentBeat = PreRoundBeat.None;
    [SerializeField] private int countdownValue;

    private float nextBeatTime;

    /// <summary>Round title shown: Round number, and whether it is the final Round.</summary>
    public event Action<int, bool> RoundTitleShown;

    /// <summary>Countdown number shown (for example 3, 2, 1).</summary>
    public event Action<int> CountdownTicked;

    /// <summary>The match has started; the start text shows.</summary>
    public event Action RoundStarted;

    /// <summary>The pre-round is over; presentation can hide.</summary>
    public event Action PreRoundFinished;

    public int RoundNumber => roundNumber;
    public bool IsFinalRound => MatchSession.IsFinalRound;
    public PreRoundBeat CurrentBeat => currentBeat;
    public int CurrentCountdownValue => countdownValue;

    private readonly int[] roundBanked = new int[MaxPlayers];
    private readonly bool[] roundWinners = new bool[MaxPlayers];
    private readonly int[] finalMatchPoints = new int[MaxPlayers];
    private readonly bool[] matchWinners = new bool[MaxPlayers];

    private int sceneBuildIndex = -1;
    private float roundResultEndTime;

    // Fades advance by at most this per frame, so a hitch (the first frames
    // after a load are slow) never skips them.
    private const float MaxFadeStep = 1f / 30f;

    private float fadeInElapsed;
    private float fadeOutElapsed;
    private LoadTarget pendingLoad = LoadTarget.None;

    /// <summary>
    /// 0 = clear, 1 = black. Fades in after this scene loads and out before a
    /// load this controller starts. Real time.
    /// </summary>
    public float ScreenFadeAlpha
    {
        get
        {
            // A disabled controller (for example a scene outside Build Settings) never fades.
            if (!isActiveAndEnabled) return 0f;

            if (phase == RoundPhase.Loading)
                return fadeOutDuration > 0f ? Mathf.Clamp01(fadeOutElapsed / fadeOutDuration) : 1f;

            return fadeInDuration > 0f ? 1f - Mathf.Clamp01(fadeInElapsed / fadeInDuration) : 0f;
        }
    }

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

        if (gameTimeManager == null)
            gameTimeManager = FindFirstObjectByType<GameTimeManager>();

        roundNumber = MatchSession.RoundNumber;
        currentBeat = PreRoundBeat.None;
        countdownValue = 0;

        if (gameTimeManager != null)
        {
            gameTimeManager.TakeOverMatchStart();
            phase = RoundPhase.PreRound;
        }
        else
        {
            Debug.LogError("[Meta] GameTimeManager not found. No pre-round; the match starts on its own if it can.", this);
            phase = RoundPhase.InRound;
        }
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
        float fadeStep = Mathf.Min(Time.unscaledDeltaTime, MaxFadeStep);

        if (phase == RoundPhase.Loading)
        {
            fadeOutElapsed += fadeStep;

            if (pendingLoad != LoadTarget.None && fadeOutElapsed >= fadeOutDuration)
                CompletePendingLoad();

            return;
        }

        fadeInElapsed += fadeStep;

        if (listenForStaffKeys) HandleStaffKeys();

        if (phase == RoundPhase.PreRound)
            UpdatePreRound();
        else if (phase == RoundPhase.RoundResult && Time.unscaledTime >= roundResultEndTime)
            FinishRound();
    }

    #endregion

    #region Pre-Round

    private void UpdatePreRound()
    {
        if (currentBeat == PreRoundBeat.None)
        {
            // GameTimeManager enters PreGame in its Start; wait for it so the world is frozen.
            if (!gameTimeManager.IsPreGame) return;

            SetBeat(PreRoundBeat.RoundTitle, roundTitleDuration);
            Debug.Log($"[Meta] Pre-round | Round {roundNumber}{(MatchSession.IsFinalRound ? " (final)" : string.Empty)}", this);
            RoundTitleShown?.Invoke(roundNumber, MatchSession.IsFinalRound);
            return;
        }

        if (Time.unscaledTime < nextBeatTime) return;

        switch (currentBeat)
        {
            case PreRoundBeat.RoundTitle:
                if (countdownFrom > 0) ShowCountdown(countdownFrom);
                else StartRound();
                break;

            case PreRoundBeat.Countdown:
                if (countdownValue > 1) ShowCountdown(countdownValue - 1);
                else StartRound();
                break;

            case PreRoundBeat.Go:
                EndPreRound();
                break;
        }
    }

    private void ShowCountdown(int value)
    {
        countdownValue = value;
        SetBeat(PreRoundBeat.Countdown, countdownStepDuration);
        CountdownTicked?.Invoke(value);
    }

    private void StartRound()
    {
        countdownValue = 0;
        gameTimeManager.BeginMatch();

        if (!gameTimeManager.IsPlaying)
        {
            Debug.LogError($"[Meta] Round {roundNumber} could not start. F2 restarts the Round.", this);
            EndPreRound();
            return;
        }

        SetBeat(PreRoundBeat.Go, goDuration);
        Debug.Log($"[Meta] Round {roundNumber} started", this);
        RoundStarted?.Invoke();
    }

    private void EndPreRound()
    {
        currentBeat = PreRoundBeat.None;
        phase = RoundPhase.InRound;
        PreRoundFinished?.Invoke();
    }

    private void SetBeat(PreRoundBeat beat, float duration)
    {
        currentBeat = beat;
        nextBeatTime = Time.unscaledTime + duration;
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
        else if (keyboard.f4Key.wasPressedThisFrame)
            ReturnToMainMenu();
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

    private void ReturnToMainMenu()
    {
        if (string.IsNullOrWhiteSpace(mainMenuSceneName) || !Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            Debug.LogError($"[Meta] F4 ignored: main menu scene '{mainMenuSceneName}' is not in Build Settings.", this);
            return;
        }

        Debug.Log($"[Meta] F4 Main Menu. Leaving the Match in Round {roundNumber}.", this);
        BeginLoad(LoadTarget.MainMenu);
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
        BeginLoad(LoadTarget.ThisScene);
    }

    // Fades out first; Update completes the load when the fade ends.
    private void BeginLoad(LoadTarget target)
    {
        phase = RoundPhase.Loading;
        pendingLoad = target;
        fadeOutElapsed = 0f;
    }

    private void CompletePendingLoad()
    {
        LoadTarget target = pendingLoad;
        pendingLoad = LoadTarget.None;

        if (gameTimeManager != null)
            gameTimeManager.PrepareForSceneLoad();
        else
            Time.timeScale = 1f;

        if (target == LoadTarget.MainMenu)
        {
            // Leaving the Match: clear Match data and the mode choice, as the main menu expects.
            MatchSession.Clear();
            GMode.DestroyPersistentInstance();
            SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
            return;
        }

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
