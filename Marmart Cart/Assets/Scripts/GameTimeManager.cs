using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

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

    [Tooltip("Freezes gameplay with Time.timeScale = 0 during the simple PreGame intro window.")]
    [FormerlySerializedAs("pauseWorldOutsideGameplay")]
    [SerializeField] private bool pauseWorldDuringPreGame = true;

    [Tooltip(
        "Usually leave this disabled: the final-results ceremony uses real-time cart, cargo, text, " +
        "and camera animation. The results controller explicitly locks gameplay systems instead."
    )]
    [SerializeField] private bool pauseWorldDuringPostGame = false;

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
        EnterPreGame();
    }

    #endregion

    #region Lifecycle

    private void EnterPreGame()
    {
        sessionState = GameSessionState.PreGame;

        matchFlowDirector?.StopFlow();

        SetWorldPaused(pauseWorldDuringPreGame);

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

        SetWorldPaused(false);

        matchFlowDirector.StartFlow();

        if (!matchFlowDirector.IsRunning)
        {
            Debug.LogError("[GameTimeManager] MatchFlowDirector failed to start. Returning to PreGame.", this);
            EnterPreGame();
            return;
        }

        musicManager?.PlayMusic("BackgroundMusic");

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

        SetWorldPaused(pauseWorldDuringPostGame);

        OnPostGameEntered?.Invoke();
    }

    private void SetWorldPaused(bool paused)
    {
        Time.timeScale = paused ? 0f : 1f;
    }

    #endregion

    #region Timer

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
}
