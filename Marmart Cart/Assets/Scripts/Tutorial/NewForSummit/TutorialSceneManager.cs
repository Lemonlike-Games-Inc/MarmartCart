using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tutorial-only scene transition wrapper.
///
/// Normal completion:
/// MatchFlowDirector finishes
///     -> GameTimeManager enters PostGame
///     -> OnPostGameEntered fires
///     -> load the authored gameplay scene.
///
/// Hidden skip:
/// Any bound player's CartControlScript may hold the Input System "Reset"
/// action continuously for the authored duration (default 5 seconds).
/// Releasing Reset before the threshold resets only that player's timer.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialSceneManager : MonoBehaviour
{
    private const int MaxPlayers = PlayerInputManager.MaxPlayers;

    #region Configuration

    [Header("Match Lifecycle")]
    [SerializeField] private GameTimeManager gameTimeManager;

    [Header("Runtime Player Input")]
    [Tooltip(
        "Used only for the hidden tutorial skip. " +
        "If left empty, this component finds the scene PlayerInputManager."
    )]
    [SerializeField] private PlayerInputManager playerInputManager;

    [Header("Tutorial Player Mode")]
    [Tooltip("OFF = 2P destination. ON = 4P destination.")]
    [SerializeField] private bool fourPlayerMode;

    [Header("Destination Scenes")]
    [SerializeField] private string twoPlayerSceneName;
    [SerializeField] private string fourPlayerSceneName;

    [Header("Normal Completion Transition")]
    [Min(0f)]
    [SerializeField] private float loadDelay = 0f;

    [Header("Hidden Tutorial Skip")]
    [Tooltip(
        "When enabled, any player can hold the CartControlScript Reset action " +
        "continuously to skip directly to the selected gameplay scene."
    )]
    [SerializeField] private bool enableHiddenSkip = true;

    [Tooltip(
        "Continuous Reset-hold duration required from ONE player. " +
        "Releasing the button resets that player's progress."
    )]
    [Min(0.1f)]
    [SerializeField] private float skipHoldDuration = 5f;

    #endregion

    #region Runtime

    [Header("Runtime - Read Only")]
    [SerializeField] private bool loadRequested;
    [SerializeField] private string selectedSceneName = string.Empty;

    [Header("Hidden Skip - Runtime Read Only")]
    [SerializeField] private int skipHoldingPlayerIndex;
    [SerializeField] private float skipHoldingSeconds;
    [SerializeField] private bool skipTriggered;

    private readonly float[] playerSkipHoldTimes =
        new float[MaxPlayers];

    private GameTimeManager subscribedGameTimeManager;
    private Coroutine loadRoutine;
    private Coroutine skipMonitorRoutine;

    public bool FourPlayerMode => fourPlayerMode;
    public bool LoadRequested => loadRequested;
    public string SelectedSceneName => selectedSceneName;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();
        RefreshSelectedSceneName();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        StartHiddenSkipMonitor();
    }

    private void Start()
    {
        ResolveReferences();
        Subscribe();
        RefreshSelectedSceneName();
        StartHiddenSkipMonitor();
    }

    private void OnDisable()
    {
        Unsubscribe();

        if (loadRoutine != null)
        {
            StopCoroutine(loadRoutine);
            loadRoutine = null;
        }

        StopHiddenSkipMonitor();
    }

    private void OnValidate()
    {
        loadDelay = Mathf.Max(0f, loadDelay);
        skipHoldDuration = Mathf.Max(0.1f, skipHoldDuration);
        RefreshSelectedSceneName();
    }

    #endregion

    #region Normal Match End Redirect

    private void HandlePostGameEntered()
    {
        if (loadRequested) return;

        loadRequested = true;
        RefreshSelectedSceneName();

        if (!ValidateDestinationScene(selectedSceneName))
        {
            loadRequested = false;
            return;
        }

        if (loadDelay <= 0f)
        {
            LoadSelectedScene();
            return;
        }

        loadRoutine = StartCoroutine(LoadAfterRealtimeDelay());
    }

    private IEnumerator LoadAfterRealtimeDelay()
    {
        yield return new WaitForSecondsRealtime(loadDelay);

        loadRoutine = null;
        LoadSelectedScene();
    }

    #endregion

    #region Hidden Skip

    private void StartHiddenSkipMonitor()
    {
        if (!enableHiddenSkip ||
            !isActiveAndEnabled ||
            skipMonitorRoutine != null ||
            loadRequested)
        {
            return;
        }

        skipMonitorRoutine =
            StartCoroutine(MonitorHiddenTutorialSkip());
    }

    private void StopHiddenSkipMonitor()
    {
        if (skipMonitorRoutine != null)
        {
            StopCoroutine(skipMonitorRoutine);
            skipMonitorRoutine = null;
        }

        ResetAllSkipProgress();
    }

    private IEnumerator MonitorHiddenTutorialSkip()
    {
        while (isActiveAndEnabled && !loadRequested)
        {
            if (playerInputManager == null)
            {
                playerInputManager =
                    FindFirstObjectByType<PlayerInputManager>();
            }

            int strongestPlayer = 0;
            float strongestHold = 0f;

            for (int playerIndex = 1;
                 playerIndex <= MaxPlayers;
                 playerIndex++)
            {
                CartControlScript cart =
                    playerInputManager != null
                        ? playerInputManager.GetBoundCartControl(playerIndex)
                        : null;

                int slot = playerIndex - 1;

                if (cart != null && cart.IsResetHeld)
                {
                    playerSkipHoldTimes[slot] +=
                        Time.unscaledDeltaTime;

                    if (playerSkipHoldTimes[slot] > strongestHold)
                    {
                        strongestHold = playerSkipHoldTimes[slot];
                        strongestPlayer = playerIndex;
                    }

                    if (playerSkipHoldTimes[slot] >= skipHoldDuration)
                    {
                        skipTriggered = true;
                        skipHoldingPlayerIndex = playerIndex;
                        skipHoldingSeconds =
                            playerSkipHoldTimes[slot];

                        Debug.Log(
                            $"[TutorialSceneManager] Player {playerIndex} " +
                            $"held Reset for {skipHoldDuration:0.##} seconds. " +
                            "Skipping tutorial.",
                            this
                        );

                        skipMonitorRoutine = null;
                        LoadDestinationNow();
                        yield break;
                    }
                }
                else
                {
                    // Continuous hold is required.
                    playerSkipHoldTimes[slot] = 0f;
                }
            }

            skipHoldingPlayerIndex = strongestPlayer;
            skipHoldingSeconds = strongestHold;

            yield return null;
        }

        skipMonitorRoutine = null;
    }

    private void ResetAllSkipProgress()
    {
        for (int i = 0; i < playerSkipHoldTimes.Length; i++)
        {
            playerSkipHoldTimes[i] = 0f;
        }

        skipHoldingPlayerIndex = 0;
        skipHoldingSeconds = 0f;
    }

    #endregion

    #region Scene Loading

    private void LoadSelectedScene()
    {
        if (!ValidateDestinationScene(selectedSceneName))
        {
            loadRequested = false;
            return;
        }

        // PostGame may pause the game. timeScale survives scene loads.
        Time.timeScale = 1f;

        Debug.Log(
            $"[TutorialSceneManager] Loading " +
            $"{(fourPlayerMode ? "4P" : "2P")} scene " +
            $"'{selectedSceneName}'.",
            this
        );

        SceneManager.LoadScene(
            selectedSceneName,
            LoadSceneMode.Single
        );
    }

    public void SetFourPlayerMode(bool fourPlayers)
    {
        fourPlayerMode = fourPlayers;
        RefreshSelectedSceneName();
    }

    /// <summary>
    /// Shared immediate route used by the hidden skip and manual testing.
    /// It deliberately bypasses the normal completion Load Delay.
    /// </summary>
    public void LoadDestinationNow()
    {
        if (loadRequested) return;

        loadRequested = true;
        RefreshSelectedSceneName();

        if (!ValidateDestinationScene(selectedSceneName))
        {
            loadRequested = false;
            return;
        }

        LoadSelectedScene();
    }

    [ContextMenu("TEST - Load Tutorial Destination")]
    private void DebugLoadDestination()
    {
        LoadDestinationNow();
    }

    #endregion

    #region References / Subscription

    private void ResolveReferences()
    {
        if (gameTimeManager == null)
        {
            gameTimeManager =
                FindFirstObjectByType<GameTimeManager>();
        }

        if (playerInputManager == null)
        {
            playerInputManager =
                FindFirstObjectByType<PlayerInputManager>();
        }
    }

    private void Subscribe()
    {
        if (subscribedGameTimeManager == gameTimeManager)
        {
            return;
        }

        Unsubscribe();

        subscribedGameTimeManager = gameTimeManager;

        if (subscribedGameTimeManager != null)
        {
            subscribedGameTimeManager.OnPostGameEntered +=
                HandlePostGameEntered;
        }
        else
        {
            Debug.LogError(
                "[TutorialSceneManager] GameTimeManager is missing.",
                this
            );
        }
    }

    private void Unsubscribe()
    {
        if (subscribedGameTimeManager == null) return;

        subscribedGameTimeManager.OnPostGameEntered -=
            HandlePostGameEntered;

        subscribedGameTimeManager = null;
    }

    #endregion

    #region Helpers

    private void RefreshSelectedSceneName()
    {
        selectedSceneName =
            fourPlayerMode
                ? fourPlayerSceneName
                : twoPlayerSceneName;
    }

    private bool ValidateDestinationScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError(
                $"[TutorialSceneManager] The " +
                $"{(fourPlayerMode ? "4P" : "2P")} " +
                "destination scene name is empty.",
                this
            );

            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"[TutorialSceneManager] Scene '{sceneName}' " +
                "cannot be loaded. Check the scene name and " +
                "Build Settings / Build Profile.",
                this
            );

            return false;
        }

        return true;
    }

    #endregion
}
