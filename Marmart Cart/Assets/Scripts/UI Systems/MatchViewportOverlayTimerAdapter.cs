using UnityEngine;

/// <summary>
/// Presentation adapter between GameTimeManager and MatchViewportOverlayStateSystem.
///
/// GameTimeManager remains the gameplay/lifecycle authority.
/// This adapter translates its public timer data into centralized overlay state.
///
/// Step 1 intentionally does not draw anything.
/// </summary>
[DisallowMultipleComponent]
public class MatchViewportOverlayTimerAdapter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameTimeManager gameTimeManager;
    [SerializeField] private MatchViewportOverlayStateSystem stateSystem;

    [Header("Runtime - Read Only")]
    [SerializeField] private int lastPublishedDisplaySeconds = int.MinValue;
    [SerializeField] private GameSessionState lastPublishedSessionState = (GameSessionState)(-1);

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (gameTimeManager != null)
        {
            gameTimeManager.OnPreGameEntered += HandleSessionChanged;
            gameTimeManager.OnMatchStarted += HandleSessionChanged;
            gameTimeManager.OnPostGameEntered += HandleSessionChanged;
        }
    }

    private void Start()
    {
        PublishTimerState(true);
    }

    private void LateUpdate()
    {
        PublishTimerState(false);
    }

    private void OnDisable()
    {
        if (gameTimeManager != null)
        {
            gameTimeManager.OnPreGameEntered -= HandleSessionChanged;
            gameTimeManager.OnMatchStarted -= HandleSessionChanged;
            gameTimeManager.OnPostGameEntered -= HandleSessionChanged;
        }
    }

    private void ResolveReferences()
    {
        if (gameTimeManager == null)
            gameTimeManager = FindFirstObjectByType<GameTimeManager>();

        if (stateSystem == null)
            stateSystem = MatchViewportOverlayStateSystem.Instance;

        if (stateSystem == null)
            stateSystem = FindFirstObjectByType<MatchViewportOverlayStateSystem>();
    }

    private void HandleSessionChanged()
    {
        PublishTimerState(true);
    }

    private void PublishTimerState(bool force)
    {
        if (gameTimeManager == null || stateSystem == null) return;

        float plannedDuration = gameTimeManager.GetPlannedGameDuration();
        float elapsedTime = gameTimeManager.GetCurrentGameTime();

        float remainingTime = gameTimeManager.IsPreGame
            ? plannedDuration
            : gameTimeManager.GetRemainingGameTime();

        int displaySeconds = Mathf.CeilToInt(Mathf.Max(0f, remainingTime));
        GameSessionState sessionState = gameTimeManager.SessionState;

        if (!force &&
            displaySeconds == lastPublishedDisplaySeconds &&
            sessionState == lastPublishedSessionState)
        {
            return;
        }

        lastPublishedDisplaySeconds = displaySeconds;
        lastPublishedSessionState = sessionState;

        stateSystem.SetTimer(
            remainingTime,
            elapsedTime,
            plannedDuration,
            sessionState);
    }
}
