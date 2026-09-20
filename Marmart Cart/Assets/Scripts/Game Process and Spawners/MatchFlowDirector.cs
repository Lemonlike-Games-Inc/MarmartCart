using System;
using System.Collections;
using UnityEngine;

public enum MatchFlowSessionPhase
{
    None = 0,
    Warning = 1,
    Active = 2,
    Closing = 3,
    FreePlay = 4
}

/// <summary>
/// Authoritative playable-match timeline.
///
/// IMPORTANT LIFECYCLE OWNERSHIP:
/// - GameTimeManager owns PRE-GAME and POST-GAME windows.
/// - MatchFlowDirector owns the entire PLAYING timeline.
/// - EndGameWrap MUST be the final authored session.
/// - When EndGameWrap completes, the Director raises OnMatchEndRequested.
/// - GameTimeManager then takes control again and enters PostGame.
///
/// The Director does not start automatically when the scene loads.
/// </summary>
[DisallowMultipleComponent]
public class MatchFlowDirector : MonoBehaviour
{
    #region Configuration

    [Header("Flow")]
    [SerializeField] private MatchFlowProfile profile;

    [Header("Arena Bindings")]
    [SerializeField] private CartRestockSpawner cartRestockSpawner;
    [SerializeField] private ArenaZone[] zones;
    [SerializeField] private CheckoutStationFlowController[] checkoutStations;

    [Header("Area Indicator Presentation")]
    [SerializeField]
    private MatchFlowAreaIndicatorController areaIndicatorController;

    [Header("Debug")]
    [SerializeField] private bool logSessions = true;

    #endregion

    #region Runtime

    [Header("Runtime - Read Only")]
    [SerializeField] private bool isRunning;
    [SerializeField] private int currentSessionIndex = -1;
    [SerializeField] private MatchFlowSessionType currentSessionType;
    [SerializeField] private MatchFlowSessionPhase currentSessionPhase;
    [SerializeField] private string currentSessionLabel;
    [SerializeField] private float plannedProfileDuration;
    [SerializeField] private float elapsedFlowTime;
    [SerializeField] private int phaseChangeCount;
    [SerializeField] private int lastPhaseChangeFrame = -1;

    private Coroutine flowRoutine;
    private MatchFlowSession currentSession;

    // Tutorial Zone Loot launches one loot coroutine per arena zone so all
    // four zones distribute their budgets concurrently across one shared
    // Match Flow Active phase.
    private Coroutine[] tutorialZoneLootCoroutines;
    private int tutorialZoneLootRoutinesRunning;

    public MatchFlowProfile Profile => profile;
    public bool IsRunning => isRunning;
    public int CurrentSessionIndex => currentSessionIndex;
    public MatchFlowSessionType CurrentSessionType => currentSessionType;
    public MatchFlowSessionPhase CurrentSessionPhase => currentSessionPhase;
    public MatchFlowSession CurrentSession => currentSession;
    public string CurrentSessionLabel => currentSessionLabel;

    public float PlannedProfileDuration => plannedProfileDuration;
    public float ElapsedFlowTime => elapsedFlowTime;
    public float RemainingFlowTime => Mathf.Max(0f, plannedProfileDuration - elapsedFlowTime);
    public float NormalizedFlowTime => plannedProfileDuration > 0.01f ? Mathf.Clamp01(elapsedFlowTime / plannedProfileDuration) : 0f;
    public int PhaseChangeCount => phaseChangeCount;
    public int LastPhaseChangeFrame => lastPhaseChangeFrame;

    public event Action<int, MatchFlowSession> OnSessionStarted;
    public event Action<int, MatchFlowSession> OnSessionEnded;

    /// <summary>
    /// Raised at the exact semantic boundary between Warning, Active,
    /// Closing, FreePlay, and None. Presentation systems should use this
    /// instead of reconstructing session timing or polling ArenaZone state.
    /// </summary>
    public event Action<int, MatchFlowSession, MatchFlowSessionPhase>
        OnSessionPhaseChanged;

    /// <summary>
    /// Raised only when the authored FINAL EndGameWrap finishes.
    /// This is the authoritative "the playable match is over" signal.
    /// </summary>
    public event Action OnMatchEndRequested;

    /// <summary>
    /// General director completion event. This fires after OnMatchEndRequested
    /// for a normal valid match profile.
    /// </summary>
    public event Action OnFlowCompleted;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        currentSession = null;
        currentSessionPhase = MatchFlowSessionPhase.None;

        if (zones == null || zones.Length == 0) zones = FindObjectsByType<ArenaZone>(FindObjectsSortMode.None);
        if (checkoutStations == null || checkoutStations.Length == 0) checkoutStations = FindObjectsByType<CheckoutStationFlowController>(FindObjectsSortMode.None);
        if (cartRestockSpawner == null) cartRestockSpawner = FindFirstObjectByType<CartRestockSpawner>();
        if (areaIndicatorController == null) areaIndicatorController = FindFirstObjectByType<MatchFlowAreaIndicatorController>();

        plannedProfileDuration = profile != null ? profile.GetPlannedDuration() : 0f;

        ResetArenaMacroState();
        areaIndicatorController?.ShowPreGame();
    }

    private void Update()
    {
        if (!isRunning) return;

        elapsedFlowTime = Mathf.Min(plannedProfileDuration, elapsedFlowTime + Time.deltaTime);
    }

    private void OnDisable()
    {
        StopFlow();
    }

    #endregion

    #region Flow Control

    [ContextMenu("START Match Flow")]
    public void StartFlow()
    {
        if (isRunning) return;

        if (!ValidateProfileForPlayableMatch()) return;

        SetSessionPhase(currentSession, MatchFlowSessionPhase.None);
        currentSession = null;

        ResetArenaMacroState();
        areaIndicatorController?.ShowPreGame();

        plannedProfileDuration = profile.GetPlannedDuration();
        elapsedFlowTime = 0f;
        phaseChangeCount = 0;
        lastPhaseChangeFrame = -1;
        currentSessionIndex = -1;
        currentSessionLabel = string.Empty;

        flowRoutine = StartCoroutine(RunFlow());
    }

    [ContextMenu("STOP Match Flow")]
    public void StopFlow()
    {
        if (flowRoutine != null)
        {
            StopCoroutine(flowRoutine);
            flowRoutine = null;
        }

        StopTutorialZoneLootCoroutines();

        SetSessionPhase(currentSession, MatchFlowSessionPhase.None);
        currentSession = null;

        isRunning = false;
        currentSessionIndex = -1;
        currentSessionLabel = string.Empty;

        cartRestockSpawner?.CancelSpawning();

        if (zones != null)
        {
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i] != null) zones[i].CancelLootSpawning();
            }
        }

        ResetArenaMacroState();
        areaIndicatorController?.ShowPreGame();
    }

    [ContextMenu("RESTART Match Flow")]
    public void RestartFlow()
    {
        StopFlow();
        StartFlow();
    }

    private bool ValidateProfileForPlayableMatch()
    {
        if (profile == null)
        {
            Debug.LogError("[MatchFlowDirector] Match Flow Profile is missing.", this);
            return false;
        }

        if (profile.Sessions == null || profile.Sessions.Count == 0)
        {
            Debug.LogError("[MatchFlowDirector] Match Flow Profile contains no sessions.", this);
            return false;
        }

        if (!profile.HasValidTerminalEndGameWrap())
        {
            Debug.LogError(
                "[MatchFlowDirector] A playable MatchFlowProfile must contain exactly ONE EndGameWrap and it must be the FINAL session.",
                profile
            );
            return false;
        }

        return true;
    }

    private IEnumerator RunFlow()
    {
        isRunning = true;

        for (int i = 0; i < profile.Sessions.Count; i++)
        {
            MatchFlowSession session = profile.Sessions[i];
            if (session == null) continue;

            currentSession = session;
            currentSessionIndex = i;
            currentSessionType = session.type;
            currentSessionLabel = string.IsNullOrWhiteSpace(session.label) ? session.type.ToString() : session.label;

            if (session.type == MatchFlowSessionType.TutorialZoneLoot)
            {
                // The existing area-indicator API has no "all four zones but
                // not center" selection. ShowAll prevents the new tutorial
                // type from falling through to None in ShowForSession().
                areaIndicatorController?.ShowAll();
            }
            else
            {
                areaIndicatorController?.ShowForSession(session);
            }

            if (logSessions)
            {
                Debug.Log($"[MatchFlow] START {i}: {currentSessionLabel} ({session.type})", this);
            }

            OnSessionStarted?.Invoke(i, session);

            yield return RunSession(session);

            SetSessionPhase(session, MatchFlowSessionPhase.None);
            OnSessionEnded?.Invoke(i, session);

            if (logSessions)
            {
                Debug.Log($"[MatchFlow] END {i}: {currentSessionLabel}", this);
            }

            bool isTerminalSession =
                session.type == MatchFlowSessionType.EndGameWrap;

            currentSession = null;

            if (isTerminalSession)
            {
                CompletePlayableMatch();
                yield break;
            }
        }

        // Validation should make this unreachable.
        Debug.LogError("[MatchFlowDirector] Flow ended without reaching EndGameWrap.", this);
        StopFlow();
    }

    private void CompletePlayableMatch()
    {
        elapsedFlowTime = plannedProfileDuration;
        isRunning = false;
        flowRoutine = null;

        currentSessionIndex = -1;
        currentSessionLabel = string.Empty;
        currentSession = null;

        ResetArenaMacroState();
        areaIndicatorController?.ShowPostGame();

        if (logSessions)
        {
            Debug.Log("[MatchFlow] FINAL EndGameWrap completed. Requesting match end.", this);
        }

        OnMatchEndRequested?.Invoke();
        OnFlowCompleted?.Invoke();
    }

    #endregion

    #region Session Execution

    private IEnumerator RunSession(MatchFlowSession session)
    {
        switch (session.type)
        {
            case MatchFlowSessionType.FreePlay:
                SetSessionPhase(session, MatchFlowSessionPhase.Active);
                yield return WaitSeconds(session.duration);
                break;

            case MatchFlowSessionType.CartRestock:
                yield return RunCartRestock(session);
                break;

            case MatchFlowSessionType.ZoneLoot:
                yield return RunZoneLoot(session);
                break;

            case MatchFlowSessionType.TutorialZoneLoot:
                yield return RunTutorialZoneLoot(session);
                break;

            case MatchFlowSessionType.CheckoutWindow:
                yield return RunCheckoutWindow(session);
                break;

            case MatchFlowSessionType.CheckoutRestock:
                yield return RunCheckoutRestock(session);
                break;

            case MatchFlowSessionType.EndGameWrap:
                yield return RunEndGameWrap(session);
                break;
        }
    }

    private IEnumerator RunCartRestock(MatchFlowSession session)
    {
        SetSessionPhase(session, MatchFlowSessionPhase.Active);

        if (cartRestockSpawner == null)
        {
            Debug.LogError("[MatchFlowDirector] Cart Restock session has no CartRestockSpawner.", this);
            yield return WaitSeconds(session.duration);
            yield break;
        }

        yield return cartRestockSpawner.SpawnExactBudget(session.resourceBudget, session.duration, session.batchSize);
    }

    private IEnumerator RunZoneLoot(MatchFlowSession session)
    {
        ArenaZone zone = FindZone(session.zone);

        if (zone == null)
        {
            Debug.LogError($"[MatchFlowDirector] Could not find ArenaZone {session.zone}.", this);
            SetSessionPhase(session, MatchFlowSessionPhase.None);
            yield return WaitSeconds(session.GetPlannedDuration());
            yield break;
        }

        zone.SetWarning();
        SetSessionPhase(session, MatchFlowSessionPhase.Warning);
        yield return WaitSeconds(session.telegraphDuration);

        zone.SetActive();
        SetSessionPhase(session, MatchFlowSessionPhase.Active);
        yield return zone.SpawnLootBudget(session.resourceBudget, session.duration, session.batchSize);

        // FreePlay preserves the old long breathing-window behavior. The zone
        // remains Active, so its power-up spawners and existing chaos remain,
        // but SpawnLootBudget has already completed and releases no new loot.
        SetSessionPhase(session, MatchFlowSessionPhase.FreePlay);
        yield return WaitSeconds(session.freePlayDuration);

        // Closing is the short final transition. Gameplay remains in the same
        // quiet state, but the Player HUD Session Guide now forecasts the next
        // major session through the end of that session's Telegraph phase.
        SetSessionPhase(session, MatchFlowSessionPhase.Closing);
        yield return WaitSeconds(session.closingDuration);

        zone.SetIdle();
    }

    private IEnumerator RunTutorialZoneLoot(MatchFlowSession session)
    {
        ArenaZone[] tutorialZones = GetAllFourTutorialZones();

        if (tutorialZones == null)
        {
            Debug.LogError(
                "[MatchFlowDirector] Tutorial Zone Loot requires ZoneA, ZoneB, ZoneC, and ZoneD. " +
                "The session will preserve its authored timeline, but no tutorial-zone loot will run.",
                this
            );

            SetSessionPhase(session, MatchFlowSessionPhase.None);
            yield return WaitSeconds(session.GetPlannedDuration());
            yield break;
        }

        // Same semantic flow as normal ZoneLoot, but applied to every zone.
        for (int i = 0; i < tutorialZones.Length; i++)
        {
            tutorialZones[i].SetWarning();
        }

        SetSessionPhase(session, MatchFlowSessionPhase.Warning);
        yield return WaitSeconds(session.telegraphDuration);

        for (int i = 0; i < tutorialZones.Length; i++)
        {
            tutorialZones[i].SetActive();
        }

        SetSessionPhase(session, MatchFlowSessionPhase.Active);

        // Each zone receives the full authored resourceBudget. These four
        // routines run in parallel; they are NOT yielded sequentially.
        StartTutorialZoneLootCoroutines(tutorialZones, session);

        while (tutorialZoneLootRoutinesRunning > 0)
        {
            yield return null;
        }

        ClearTutorialZoneLootCoroutineTracking();

        // Match normal ZoneLoot behavior: all zones stay Active during the
        // quiet FreePlay and Closing windows, so power-up spawners and
        // existing pickups remain available while no new loot is released.
        SetSessionPhase(session, MatchFlowSessionPhase.FreePlay);
        yield return WaitSeconds(session.freePlayDuration);

        SetSessionPhase(session, MatchFlowSessionPhase.Closing);
        yield return WaitSeconds(session.closingDuration);

        for (int i = 0; i < tutorialZones.Length; i++)
        {
            tutorialZones[i].SetIdle();
        }
    }

    private ArenaZone[] GetAllFourTutorialZones()
    {
        ArenaZone zoneA = FindZone(ArenaZoneId.ZoneA);
        ArenaZone zoneB = FindZone(ArenaZoneId.ZoneB);
        ArenaZone zoneC = FindZone(ArenaZoneId.ZoneC);
        ArenaZone zoneD = FindZone(ArenaZoneId.ZoneD);

        if (zoneA == null || zoneB == null || zoneC == null || zoneD == null)
        {
            if (zoneA == null) Debug.LogError("[MatchFlowDirector] Tutorial Zone Loot is missing ZoneA.", this);
            if (zoneB == null) Debug.LogError("[MatchFlowDirector] Tutorial Zone Loot is missing ZoneB.", this);
            if (zoneC == null) Debug.LogError("[MatchFlowDirector] Tutorial Zone Loot is missing ZoneC.", this);
            if (zoneD == null) Debug.LogError("[MatchFlowDirector] Tutorial Zone Loot is missing ZoneD.", this);

            return null;
        }

        return new[] { zoneA, zoneB, zoneC, zoneD };
    }

    private void StartTutorialZoneLootCoroutines(
        ArenaZone[] tutorialZones,
        MatchFlowSession session
    )
    {
        StopTutorialZoneLootCoroutines();

        tutorialZoneLootCoroutines = new Coroutine[tutorialZones.Length];
        tutorialZoneLootRoutinesRunning = 0;

        for (int i = 0; i < tutorialZones.Length; i++)
        {
            ArenaZone zone = tutorialZones[i];
            if (zone == null) continue;

            tutorialZoneLootRoutinesRunning++;

            tutorialZoneLootCoroutines[i] = StartCoroutine(
                RunTutorialZoneLootBudget(zone, session)
            );
        }
    }

    private IEnumerator RunTutorialZoneLootBudget(
        ArenaZone zone,
        MatchFlowSession session
    )
    {
        yield return zone.SpawnLootBudget(
            session.resourceBudget,
            session.duration,
            session.batchSize
        );

        tutorialZoneLootRoutinesRunning =
            Mathf.Max(0, tutorialZoneLootRoutinesRunning - 1);
    }

    private void StopTutorialZoneLootCoroutines()
    {
        if (tutorialZoneLootCoroutines != null)
        {
            for (int i = 0; i < tutorialZoneLootCoroutines.Length; i++)
            {
                Coroutine routine = tutorialZoneLootCoroutines[i];
                if (routine != null)
                {
                    StopCoroutine(routine);
                }
            }
        }

        ClearTutorialZoneLootCoroutineTracking();
    }

    private void ClearTutorialZoneLootCoroutineTracking()
    {
        tutorialZoneLootCoroutines = null;
        tutorialZoneLootRoutinesRunning = 0;
    }

    private IEnumerator RunCheckoutWindow(MatchFlowSession session)
    {
        CloseAllCheckoutStations();
        SetCheckoutTelegraph(session.checkoutStations, true);
        SetSessionPhase(session, MatchFlowSessionPhase.Warning);

        yield return WaitSeconds(session.telegraphDuration);

        SetCheckoutTelegraph(session.checkoutStations, false);
        SetCheckoutOpen(session.checkoutStations, true);
        SetSessionPhase(session, MatchFlowSessionPhase.Active);

        yield return WaitSeconds(session.duration);

        SetCheckoutOpen(session.checkoutStations, false);
    }

    private IEnumerator RunCheckoutRestock(MatchFlowSession session)
    {
        // Warning owns checkout telegraph only. Restock does not begin early.
        CloseAllCheckoutStations();
        SetCheckoutTelegraph(session.checkoutStations, true);
        SetSessionPhase(session, MatchFlowSessionPhase.Warning);

        yield return WaitSeconds(session.telegraphDuration);

        // Active is the one shared window: selected checkouts are open while
        // the exact empty-cart budget is distributed across the same duration.
        SetCheckoutTelegraph(session.checkoutStations, false);
        SetCheckoutOpen(session.checkoutStations, true);
        SetSessionPhase(session, MatchFlowSessionPhase.Active);

        if (cartRestockSpawner == null)
        {
            Debug.LogError(
                "[MatchFlowDirector] Checkout + Restock session has no CartRestockSpawner. " +
                "Checkout will remain active for the authored duration, but no carts can spawn.",
                this
            );

            yield return WaitSeconds(session.duration);
        }
        else
        {
            yield return cartRestockSpawner.SpawnExactBudget(
                session.resourceBudget,
                session.duration,
                session.batchSize
            );
        }

        // FreePlay is the long breathing window. Stop both gameplay services
        // first, then retain only the session's center-area light selection.
        SetCheckoutOpen(session.checkoutStations, false);
        SetSessionPhase(session, MatchFlowSessionPhase.FreePlay);
        yield return WaitSeconds(session.freePlayDuration);

        // Closing keeps that quiet state for the short guide/transition window.
        SetSessionPhase(session, MatchFlowSessionPhase.Closing);
        yield return WaitSeconds(session.closingDuration);
    }

    private IEnumerator RunEndGameWrap(MatchFlowSession session)
    {
        // EndGameWrap is deliberately quiet:
        // no new loot, no cart restock, no new checkout entry.
        // Existing normal player gameplay may continue during this tiny buffer.
        ResetArenaMacroState();
        SetSessionPhase(session, MatchFlowSessionPhase.Active);

        yield return WaitSeconds(session.duration);
    }

    private IEnumerator WaitSeconds(float duration)
    {
        if (duration <= 0f) yield break;
        yield return new WaitForSeconds(duration);
    }

    #endregion

    #region Session Phase State

    private void SetSessionPhase(
        MatchFlowSession session,
        MatchFlowSessionPhase phase
    )
    {
        if (currentSessionPhase == phase) return;

        currentSessionPhase = phase;
        phaseChangeCount++;
        lastPhaseChangeFrame = Time.frameCount;

        if (logSessions)
        {
            string sessionName = session != null
                ? session.type.ToString()
                : "No Session";

            Debug.Log(
                $"[MatchFlow] PHASE {currentSessionIndex}: " +
                $"{sessionName} -> {phase}",
                this
            );
        }

        OnSessionPhaseChanged?.Invoke(currentSessionIndex, session, phase);
    }

    #endregion

    #region Arena Lookup / State

    private ArenaZone FindZone(ArenaZoneId zoneId)
    {
        if (zones == null) return null;

        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] != null && zones[i].ZoneId == zoneId) return zones[i];
        }

        return null;
    }

    private void ResetArenaMacroState()
    {
        if (zones != null)
        {
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i] != null) zones[i].SetIdle();
            }
        }

        CloseAllCheckoutStations();
    }

    private void CloseAllCheckoutStations()
    {
        if (checkoutStations == null) return;

        for (int i = 0; i < checkoutStations.Length; i++)
        {
            if (checkoutStations[i] != null) checkoutStations[i].SetOpen(false);
        }
    }

    private void SetCheckoutTelegraph(CheckoutStationMask mask, bool telegraphing)
    {
        if (checkoutStations == null) return;

        for (int i = 0; i < checkoutStations.Length; i++)
        {
            CheckoutStationFlowController station = checkoutStations[i];
            if (station == null || !MaskContainsStation(mask, station.StationId)) continue;

            station.SetTelegraphing(telegraphing);
        }
    }

    private void SetCheckoutOpen(CheckoutStationMask mask, bool open)
    {
        if (checkoutStations == null) return;

        for (int i = 0; i < checkoutStations.Length; i++)
        {
            CheckoutStationFlowController station = checkoutStations[i];
            if (station == null || !MaskContainsStation(mask, station.StationId)) continue;

            station.SetOpen(open);
        }
    }

    private bool MaskContainsStation(CheckoutStationMask mask, CheckoutStationId stationId)
    {
        CheckoutStationMask stationMask;

        switch (stationId)
        {
            case CheckoutStationId.North:
                stationMask = CheckoutStationMask.North;
                break;

            case CheckoutStationId.East:
                stationMask = CheckoutStationMask.East;
                break;

            case CheckoutStationId.South:
                stationMask = CheckoutStationMask.South;
                break;

            case CheckoutStationId.West:
                stationMask = CheckoutStationMask.West;
                break;

            default:
                return false;
        }

        return (mask & stationMask) != 0;
    }

    #endregion
}
