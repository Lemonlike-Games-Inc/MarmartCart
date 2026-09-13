using System;
using System.Collections;
using TMPro;
using UnityEngine;

public enum GameSessionState
{
    PreGame,
    Playing,
    PostGame
}

/// <summary>
/// Outer gameplay-scene lifecycle shell.
///
/// Expected scene flow:
/// Title/Menu Scene
/// -> Tutorial / Skip
/// -> Gameplay Scene
/// -> PreGame intro window
/// -> Playing
/// -> PostGame results window
///
/// The Gameplay Scene itself has NO title-screen confirmation and NO start input.
///
/// PreGame is intentionally an extension point:
/// - today: a short automatic real-time pause;
/// - later: map camera flyover, split-screen intro, countdown, announcer, etc.
///
/// MatchFlowDirector remains the authority for the complete playable timeline
/// and decides when gameplay ends through the final EndGameWrap.
/// </summary>
public class GameTimeManager : MonoBehaviour
{
    #region Match Lifecycle

    [Header("Match Lifecycle")]
    [SerializeField] private MatchFlowDirector matchFlowDirector;

    [Header("PreGame Intro Window")]
    [Tooltip(
        "Current placeholder intro window. The gameplay scene pauses for this many REAL-TIME seconds, " +
        "then BeginMatch() is called automatically."
    )]
    [Min(0f)]
    [SerializeField] private float preGamePauseDuration = 1f;

    [Tooltip(
        "Leave enabled for the current simple paused intro. " +
        "Later disable this when a camera intro/countdown controller should call BeginMatch() itself."
    )]
    [SerializeField] private bool autoBeginAfterPreGamePause = true;

    [Tooltip("Current prototype freezes gameplay with Time.timeScale = 0 during PreGame and PostGame.")]
    [SerializeField] private bool pauseWorldOutsideGameplay = true;

    [Header("Runtime - Read Only")]
    [SerializeField] private GameSessionState sessionState = GameSessionState.PreGame;

    private Coroutine preGameRoutine;

    public GameSessionState SessionState => sessionState;
    public bool IsPreGame => sessionState == GameSessionState.PreGame;
    public bool IsPlaying => sessionState == GameSessionState.Playing;
    public bool IsPostGame => sessionState == GameSessionState.PostGame;

    public event Action OnPreGameEntered;
    public event Action OnMatchStarted;
    public event Action OnPostGameEntered;

    #endregion

    #region UI References

    [Header("PostGame UI")]
    [Tooltip("Optional placeholder for the future rankings / final-score presentation.")]
    [SerializeField] private GameObject finalScoreScreen;

    [Header("Timer UI")]
    [SerializeField] private TextMeshProUGUI timerTextP1;
    [SerializeField] private TextMeshProUGUI timerTextP2;
    [SerializeField] private TextMeshProUGUI timerTextP3;
    [SerializeField] private TextMeshProUGUI timerTextP4;

    #endregion

    #region Player Cart HUD

    [Header("Player Carts - P1")]
    [SerializeField] private SnakeCartManager snakeCartManagerP1;
    [SerializeField] private TextMeshProUGUI currentTotalCartCountP1Text;
    [SerializeField] private TextMeshProUGUI currentItemCartCountP1Text;
    [SerializeField] private TextMeshProUGUI currentTotalCartCountP1TextFor4pMode;
    [SerializeField] private TextMeshProUGUI currentItemCartCountP1TextFor4pMode;

    [Header("Player Carts - P2")]
    [SerializeField] private SnakeCartManager snakeCartManagerP2;
    [SerializeField] private TextMeshProUGUI currentTotalCartCountP2Text;
    [SerializeField] private TextMeshProUGUI currentItemCartCountP2Text;
    [SerializeField] private TextMeshProUGUI currentTotalCartCountP2TextFor4pMode;
    [SerializeField] private TextMeshProUGUI currentItemCartCountP2TextFor4pMode;

    [Header("Player Carts - P3")]
    [SerializeField] private SnakeCartManager snakeCartManagerP3;
    [SerializeField] private TextMeshProUGUI currentTotalCartCountP3Text;
    [SerializeField] private TextMeshProUGUI currentItemCartCountP3Text;

    [Header("Player Carts - P4")]
    [SerializeField] private SnakeCartManager snakeCartManagerP4;
    [SerializeField] private TextMeshProUGUI currentTotalCartCountP4Text;
    [SerializeField] private TextMeshProUGUI currentItemCartCountP4Text;

    private TextMeshProUGUI activeTotalCartCountP1Text;
    private TextMeshProUGUI activeItemCartCountP1Text;
    private TextMeshProUGUI activeTotalCartCountP2Text;
    private TextMeshProUGUI activeItemCartCountP2Text;

    private int cartCountP1;
    private int cartCountP2;
    private int cartCountP3;
    private int cartCountP4;

    private bool isAnimatingCartCount;

    #endregion

    #region Camera Integration

    [Header("Camera Integration")]
    [Tooltip(
        "Camera lens settings and calculations now live in CameraManager + MatchSceneModeProfile. " +
        "GameTimeManager only forwards cart-count changes."
    )]
    [SerializeField] private CameraManager cameraManager;

    #endregion

    #region Audio

    [Header("Music")]
    [SerializeField] private MusicManager musicManager;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (matchFlowDirector == null) matchFlowDirector = FindFirstObjectByType<MatchFlowDirector>();
        if (cameraManager == null) cameraManager = FindFirstObjectByType<CameraManager>();
    }

    private void OnEnable()
    {
        if (matchFlowDirector != null)
        {
            matchFlowDirector.OnMatchEndRequested += HandleDirectorMatchEndRequested;
        }
    }

    private void OnDisable()
    {
        if (matchFlowDirector != null)
        {
            matchFlowDirector.OnMatchEndRequested -= HandleDirectorMatchEndRequested;
        }

        if (preGameRoutine != null)
        {
            StopCoroutine(preGameRoutine);
            preGameRoutine = null;
        }
    }

    private void Start()
    {
        ConfigurePlayerHudReferences();
        ResetCartHud();

        EnterPreGame();
    }

    private void Update()
    {
        if (sessionState != GameSessionState.Playing) return;

        UpdateTimerDisplay();
        UpdateCartHud();
    }

    #endregion

    #region Lifecycle

    private void EnterPreGame()
    {
        sessionState = GameSessionState.PreGame;

        matchFlowDirector?.StopFlow();

        SetWorldPaused(true);

        if (finalScoreScreen != null) finalScoreScreen.SetActive(false);

        UpdateTimerDisplay();

        OnPreGameEntered?.Invoke();

        if (autoBeginAfterPreGamePause)
        {
            if (preGameRoutine != null) StopCoroutine(preGameRoutine);
            preGameRoutine = StartCoroutine(AutoBeginMatchAfterDelay());
        }
    }

    private IEnumerator AutoBeginMatchAfterDelay()
    {
        if (preGamePauseDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(preGamePauseDuration);
        }

        preGameRoutine = null;
        BeginMatch();
    }

    /// <summary>
    /// Starts the playable match.
    ///
    /// Current prototype:
    /// GameTimeManager calls this automatically after preGamePauseDuration.
    ///
    /// Future:
    /// Disable Auto Begin After PreGame Pause and let a camera intro/countdown
    /// controller call BeginMatch() when its presentation is complete.
    /// </summary>
    public void BeginMatch()
    {
        if (sessionState != GameSessionState.PreGame) return;

        if (preGameRoutine != null)
        {
            StopCoroutine(preGameRoutine);
            preGameRoutine = null;
        }

        if (matchFlowDirector == null)
        {
            Debug.LogError("[GameTimeManager] MatchFlowDirector is missing.", this);
            return;
        }

        sessionState = GameSessionState.Playing;

        if (finalScoreScreen != null) finalScoreScreen.SetActive(false);

        SetWorldPaused(false);

        matchFlowDirector.StartFlow();

        if (!matchFlowDirector.IsRunning)
        {
            Debug.LogError("[GameTimeManager] MatchFlowDirector failed to start. Returning to PreGame.", this);
            EnterPreGame();
            return;
        }

        musicManager?.PlayMusic("BackgroundMusic");

        UpdateTimerDisplay();
        OnMatchStarted?.Invoke();
    }

    private void HandleDirectorMatchEndRequested()
    {
        if (sessionState != GameSessionState.Playing) return;

        EnterPostGame();
    }

    private void EnterPostGame()
    {
        sessionState = GameSessionState.PostGame;

        musicManager?.StopMusic();

        SetWorldPaused(true);

        UpdateTimerDisplay();

        if (finalScoreScreen != null) finalScoreScreen.SetActive(true);

        OnPostGameEntered?.Invoke();
    }

    private void SetWorldPaused(bool paused)
    {
        if (!pauseWorldOutsideGameplay) return;
        Time.timeScale = paused ? 0f : 1f;
    }

    #endregion

    #region Timer

    private void UpdateTimerDisplay()
    {
        float timeRemaining = matchFlowDirector != null ? matchFlowDirector.RemainingFlowTime : 0f;

        if (sessionState == GameSessionState.PreGame && matchFlowDirector != null)
        {
            timeRemaining = matchFlowDirector.PlannedProfileDuration;
        }

        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, timeRemaining));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        string formatted = $"{minutes:D2}:{seconds:D2}";

        SetTextIfAssigned(timerTextP1, formatted);
        SetTextIfAssigned(timerTextP2, formatted);
        SetTextIfAssigned(timerTextP3, formatted);
        SetTextIfAssigned(timerTextP4, formatted);
    }

    public float GetCurrentGameTime()
    {
        return matchFlowDirector != null ? matchFlowDirector.ElapsedFlowTime : 0f;
    }

    public float GetRemainingGameTime()
    {
        return matchFlowDirector != null ? matchFlowDirector.RemainingFlowTime : 0f;
    }

    public float GetPlannedGameDuration()
    {
        return matchFlowDirector != null ? matchFlowDirector.PlannedProfileDuration : 0f;
    }

    #endregion

    #region Cart HUD

    private void ConfigurePlayerHudReferences()
    {
        int playerCount = GMode.Instance != null ? GMode.Instance.PlayerCount() : 2;

        if (playerCount == 4)
        {
            activeTotalCartCountP1Text = currentTotalCartCountP1TextFor4pMode;
            activeItemCartCountP1Text = currentItemCartCountP1TextFor4pMode;

            activeTotalCartCountP2Text = currentTotalCartCountP2TextFor4pMode;
            activeItemCartCountP2Text = currentItemCartCountP2TextFor4pMode;
        }
        else
        {
            activeTotalCartCountP1Text = currentTotalCartCountP1Text;
            activeItemCartCountP1Text = currentItemCartCountP1Text;

            activeTotalCartCountP2Text = currentTotalCartCountP2Text;
            activeItemCartCountP2Text = currentItemCartCountP2Text;
        }
    }

    private void ResetCartHud()
    {
        cartCountP1 = 0;
        cartCountP2 = 0;
        cartCountP3 = 0;
        cartCountP4 = 0;

        SetTextIfAssigned(activeTotalCartCountP1Text, "0");
        SetTextIfAssigned(activeItemCartCountP1Text, "0");

        SetTextIfAssigned(activeTotalCartCountP2Text, "0");
        SetTextIfAssigned(activeItemCartCountP2Text, "0");

        SetTextIfAssigned(currentTotalCartCountP3Text, "0");
        SetTextIfAssigned(currentItemCartCountP3Text, "0");

        SetTextIfAssigned(currentTotalCartCountP4Text, "0");
        SetTextIfAssigned(currentItemCartCountP4Text, "0");
    }

    private void UpdateCartHud()
    {
        UpdatePlayerCartState(snakeCartManagerP1, ref cartCountP1, activeTotalCartCountP1Text, 1);
        UpdatePlayerCartState(snakeCartManagerP2, ref cartCountP2, activeTotalCartCountP2Text, 2);
        UpdatePlayerCartState(snakeCartManagerP3, ref cartCountP3, currentTotalCartCountP3Text, 3);
        UpdatePlayerCartState(snakeCartManagerP4, ref cartCountP4, currentTotalCartCountP4Text, 4);
    }

    private void UpdatePlayerCartState(
        SnakeCartManager manager,
        ref int cachedCartCount,
        TextMeshProUGUI cartCountText,
        int playerIndex)
    {
        if (manager == null) return;

        int newCount = Mathf.Max(0, manager.GetSnakeBodyLength() - 1);
        if (newCount == cachedCartCount) return;

        cachedCartCount = newCount;

        SetTextIfAssigned(cartCountText, cachedCartCount.ToString());

        if (cartCountText != null) StartCoroutine(AnimateCartCountText(cartCountText));

        cameraManager?.SetPlayerCartCount(playerIndex, cachedCartCount);
    }

    private IEnumerator AnimateCartCountText(TextMeshProUGUI text)
    {
        if (text == null || isAnimatingCartCount) yield break;

        isAnimatingCartCount = true;

        const float animationDuration = 0.3f;
        float halfDuration = animationDuration * 0.5f;

        Vector3 originalScale = text.transform.localScale;

        float elapsed = 0f;

        while (elapsed < halfDuration)
        {
            float t = elapsed / halfDuration;
            text.transform.localScale = Vector3.Lerp(originalScale, originalScale * 2f, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        elapsed = 0f;

        while (elapsed < halfDuration)
        {
            float t = elapsed / halfDuration;
            text.transform.localScale = Vector3.Lerp(originalScale * 2f, originalScale, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        text.transform.localScale = originalScale;
        isAnimatingCartCount = false;
    }

    private void SetTextIfAssigned(TextMeshProUGUI text, string value)
    {
        if (text != null) text.text = value;
    }

    #endregion

}
