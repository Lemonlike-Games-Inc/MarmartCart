using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Temporary on-screen designer/debug wall for the authored Match Flow.
///
/// This is intentionally presentation-only:
/// - MatchFlowDirector remains the gameplay authority.
/// - This component reads existing runtime state and formats it for playtests.
/// - No final HUD/announcement behavior should depend on this script.
///
/// Recommended setup:
/// Screen Space Overlay Canvas
/// └ Debug Panel
///    └ TMP Text
///       └ MatchFlowDebugHUD
/// </summary>
[DisallowMultipleComponent]
public class MatchFlowDebugHUD : MonoBehaviour
{
    #region References

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI debugText;

    [Tooltip("Optional root/panel to show or hide together with the debug text.")]
    [SerializeField] private GameObject debugRoot;

    [Header("Runtime Sources")]
    [SerializeField] private MatchFlowDirector matchFlowDirector;
    [SerializeField] private GameTimeManager gameTimeManager;
    [SerializeField] private CartRestockSpawner cartRestockSpawner;
    [SerializeField] private ArenaZone[] zones;
    [SerializeField] private CheckoutStationFlowController[] checkoutStations;

    #endregion

    #region Display

    [Header("Display")]
    [SerializeField] private bool visibleOnStart = true;

    [Tooltip("Uses unscaled time so the wall still updates during paused PreGame/PostGame.")]
    [Min(0.02f)]
    [SerializeField] private float refreshInterval = 0.1f;

    [SerializeField] private bool showAllZoneStates = true;
    [SerializeField] private bool showSectionDetails = true;
    [SerializeField] private bool showCheckoutStationStates = true;
    [SerializeField] private bool showNextSession = true;

    #endregion

    #region Runtime

    private readonly StringBuilder builder = new StringBuilder(2048);

    private readonly Dictionary<ArenaZoneId, ArenaZone> zoneById =
        new Dictionary<ArenaZoneId, ArenaZone>();

    private readonly Dictionary<ZoneLootSpawner, int> sectionSpawnBaseline =
        new Dictionary<ZoneLootSpawner, int>();

    private ZoneLootSpawner[] currentZoneSectionSpawners;
    private float refreshTimer;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();
        RebuildZoneLookup();

        if (debugRoot == null && debugText != null)
        {
            debugRoot = debugText.gameObject;
        }

        SetVisible(visibleOnStart);
    }

    private void OnEnable()
    {
        if (matchFlowDirector != null)
        {
            matchFlowDirector.OnSessionStarted += HandleSessionStarted;
        }

        CaptureCurrentZoneSectionBaselineIfNeeded();
        ForceRefresh();
    }

    private void OnDisable()
    {
        if (matchFlowDirector != null)
        {
            matchFlowDirector.OnSessionStarted -= HandleSessionStarted;
        }
    }

    private void Update()
    {
        if (debugText == null) return;

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f) return;

        refreshTimer = refreshInterval;
        RefreshText();
    }

    private void OnValidate()
    {
        refreshInterval = Mathf.Max(0.02f, refreshInterval);
    }

    #endregion

    #region Public Control

    public void SetVisible(bool visible)
    {
        visibleOnStart = visible;

        if (debugRoot != null)
        {
            debugRoot.SetActive(visible);
        }
        else if (debugText != null)
        {
            debugText.enabled = visible;
        }
    }

    public void ForceRefresh()
    {
        refreshTimer = 0f;

        if (debugText != null)
        {
            RefreshText();
        }
    }

    #endregion

    #region Reference Setup

    private void ResolveReferences()
    {
        if (matchFlowDirector == null)
        {
            matchFlowDirector = FindFirstObjectByType<MatchFlowDirector>();
        }

        if (gameTimeManager == null)
        {
            gameTimeManager = FindFirstObjectByType<GameTimeManager>();
        }

        if (cartRestockSpawner == null)
        {
            cartRestockSpawner = FindFirstObjectByType<CartRestockSpawner>();
        }

        if (zones == null || zones.Length == 0)
        {
            zones = FindObjectsByType<ArenaZone>(FindObjectsSortMode.None);
        }

        if (checkoutStations == null || checkoutStations.Length == 0)
        {
            checkoutStations = FindObjectsByType<CheckoutStationFlowController>(FindObjectsSortMode.None);
        }
    }

    private void RebuildZoneLookup()
    {
        zoneById.Clear();

        if (zones == null) return;

        for (int i = 0; i < zones.Length; i++)
        {
            ArenaZone zone = zones[i];
            if (zone == null) continue;

            zoneById[zone.ZoneId] = zone;
        }
    }

    #endregion

    #region Session Baseline Tracking

    private void HandleSessionStarted(int index, MatchFlowSession session)
    {
        CaptureSectionBaseline(session);
        ForceRefresh();
    }

    private void CaptureCurrentZoneSectionBaselineIfNeeded()
    {
        MatchFlowSession session = GetCurrentSession();
        if (session == null) return;

        CaptureSectionBaseline(session);
    }

    private void CaptureSectionBaseline(MatchFlowSession session)
    {
        currentZoneSectionSpawners = null;
        sectionSpawnBaseline.Clear();

        if (session == null || session.type != MatchFlowSessionType.ZoneLoot) return;
        if (!zoneById.TryGetValue(session.zone, out ArenaZone zone) || zone == null) return;

        currentZoneSectionSpawners = zone.GetComponentsInChildren<ZoneLootSpawner>(true);

        for (int i = 0; i < currentZoneSectionSpawners.Length; i++)
        {
            ZoneLootSpawner spawner = currentZoneSectionSpawners[i];
            if (spawner == null) continue;

            sectionSpawnBaseline[spawner] = spawner.TotalSpawned;
        }
    }

    #endregion

    #region Main Text

    private void RefreshText()
    {
        builder.Clear();

        AppendHeader();
        AppendLifecycle();
        AppendMatchTiming();

        MatchFlowSession currentSession = GetCurrentSession();

        if (currentSession == null)
        {
            builder.AppendLine();
            builder.AppendLine("SESSION      --");
            builder.AppendLine(GetNoActiveSessionMessage());

            if (showAllZoneStates) AppendAllZoneStates();
            if (showCheckoutStationStates) AppendCheckoutStationStates();

            debugText.text = builder.ToString();
            return;
        }

        AppendCurrentSession(currentSession);

        switch (currentSession.type)
        {
            case MatchFlowSessionType.FreePlay:
                AppendFreePlay(currentSession);
                break;

            case MatchFlowSessionType.CartRestock:
                AppendCartRestock(currentSession);
                break;

            case MatchFlowSessionType.ZoneLoot:
                AppendZoneLoot(currentSession);
                break;

            case MatchFlowSessionType.CheckoutWindow:
                AppendCheckoutWindow(currentSession);
                break;

            case MatchFlowSessionType.EndGameWrap:
                AppendEndGameWrap(currentSession);
                break;
        }

        if (showAllZoneStates) AppendAllZoneStates();
        if (showCheckoutStationStates) AppendCheckoutStationStates();
        if (showNextSession) AppendNextSession();

        debugText.text = builder.ToString();
    }

    private void AppendHeader()
    {
        builder.AppendLine("========== MATCH FLOW DEBUG ==========");
    }

    private void AppendLifecycle()
    {
        string lifecycle;

        if (gameTimeManager != null)
        {
            lifecycle = gameTimeManager.SessionState.ToString().ToUpperInvariant();
        }
        else if (matchFlowDirector != null && matchFlowDirector.IsRunning)
        {
            lifecycle = "PLAYING";
        }
        else
        {
            lifecycle = "IDLE";
        }

        builder.Append("STATE        ");
        builder.AppendLine(lifecycle);
    }

    private void AppendMatchTiming()
    {
        if (matchFlowDirector == null)
        {
            builder.AppendLine("MATCH        Director missing");
            return;
        }

        float elapsed = matchFlowDirector.ElapsedFlowTime;
        float total = matchFlowDirector.PlannedProfileDuration;
        float remaining = matchFlowDirector.RemainingFlowTime;

        builder.Append("MATCH        ");
        builder.Append(FormatTime(elapsed));
        builder.Append(" / ");
        builder.Append(FormatTime(total));
        builder.Append("   LEFT ");
        builder.AppendLine(FormatTime(remaining));
    }

    private void AppendCurrentSession(MatchFlowSession session)
    {
        int index = matchFlowDirector != null ? matchFlowDirector.CurrentSessionIndex : -1;
        int totalSessions = matchFlowDirector != null && matchFlowDirector.Profile != null
            ? matchFlowDirector.Profile.Sessions.Count
            : 0;

        builder.AppendLine();
        builder.Append("SESSION      ");

        if (index >= 0)
        {
            builder.Append(index + 1);
            builder.Append(" / ");
            builder.Append(totalSessions);
        }
        else
        {
            builder.Append("--");
        }

        builder.AppendLine();

        builder.Append("LABEL        ");
        builder.AppendLine(string.IsNullOrWhiteSpace(session.label) ? session.type.ToString() : session.label);

        builder.Append("TYPE         ");
        builder.AppendLine(FormatSessionType(session.type));

        float elapsed = GetCurrentSessionElapsed(session);
        float total = session.GetPlannedDuration();
        float remaining = Mathf.Max(0f, total - elapsed);

        builder.Append("SESSION TIME ");
        builder.Append(FormatTime(elapsed));
        builder.Append(" / ");
        builder.Append(FormatTime(total));
        builder.Append("   LEFT ");
        builder.AppendLine(FormatTime(remaining));
    }

    #endregion

    #region Session-Specific Blocks

    private void AppendFreePlay(MatchFlowSession session)
    {
        float elapsed = GetCurrentSessionElapsed(session);

        builder.Append("PHASE        FREE PLAY   ");
        builder.Append(FormatTime(elapsed));
        builder.Append(" / ");
        builder.AppendLine(FormatTime(session.duration));
    }

    private void AppendCartRestock(MatchFlowSession session)
    {
        float elapsed = GetCurrentSessionElapsed(session);

        builder.Append("PHASE        CART RESTOCK   ");
        builder.Append(FormatTime(elapsed));
        builder.Append(" / ");
        builder.AppendLine(FormatTime(session.duration));

        int spawned = cartRestockSpawner != null ? cartRestockSpawner.LastSpawnedCount : 0;
        int requested = session.resourceBudget;
        int remaining = Mathf.Max(0, requested - spawned);

        builder.Append("CARTS        ");
        builder.Append(spawned);
        builder.Append(" / ");
        builder.Append(requested);
        builder.Append(" released   WAITING ");
        builder.Append(remaining);
        builder.Append("   BATCH ");
        builder.AppendLine(session.batchSize.ToString());

        if (cartRestockSpawner == null)
        {
            builder.AppendLine("             ! CartRestockSpawner missing");
        }
    }

    private void AppendZoneLoot(MatchFlowSession session)
    {
        float elapsed = GetCurrentSessionElapsed(session);

        bool telegraphing = elapsed < session.telegraphDuration;
        float phaseElapsed;
        float phaseDuration;
        string phaseName;

        if (telegraphing)
        {
            phaseName = "TELEGRAPH";
            phaseElapsed = elapsed;
            phaseDuration = session.telegraphDuration;
        }
        else
        {
            phaseName = "LOOT ACTIVE";
            phaseElapsed = Mathf.Max(0f, elapsed - session.telegraphDuration);
            phaseDuration = session.duration;
        }

        builder.Append("PHASE        ");
        builder.Append(phaseName);
        builder.Append("   ");
        builder.Append(FormatTime(phaseElapsed));
        builder.Append(" / ");
        builder.AppendLine(FormatTime(phaseDuration));

        ArenaZone zone = GetZone(session.zone);

        builder.Append("ZONE         ");
        builder.Append(session.zone);

        if (zone != null)
        {
            builder.Append("  [");
            builder.Append(zone.DisplayName);
            builder.Append("]  STATE ");
            builder.Append(zone.State);
        }

        builder.AppendLine();

        int spawned = zone != null ? zone.LastSpawnedCount : 0;
        int requested = session.resourceBudget;
        int remaining = Mathf.Max(0, requested - spawned);

        builder.Append("LOOT         ");
        builder.Append(spawned);
        builder.Append(" / ");
        builder.Append(requested);
        builder.Append(" released   WAITING ");
        builder.Append(remaining);
        builder.Append("   BATCH ");
        builder.AppendLine(session.batchSize.ToString());

        if (showSectionDetails)
        {
            AppendSectionDetails(zone);
        }
    }

    private void AppendCheckoutWindow(MatchFlowSession session)
    {
        float elapsed = GetCurrentSessionElapsed(session);

        bool telegraphing = elapsed < session.telegraphDuration;
        float phaseElapsed;
        float phaseDuration;
        string phaseName;

        if (telegraphing)
        {
            phaseName = "CHECKOUT TELEGRAPH";
            phaseElapsed = elapsed;
            phaseDuration = session.telegraphDuration;
        }
        else
        {
            phaseName = "CHECKOUT OPEN";
            phaseElapsed = Mathf.Max(0f, elapsed - session.telegraphDuration);
            phaseDuration = session.duration;
        }

        builder.Append("PHASE        ");
        builder.Append(phaseName);
        builder.Append("   ");
        builder.Append(FormatTime(phaseElapsed));
        builder.Append(" / ");
        builder.AppendLine(FormatTime(phaseDuration));

        builder.Append("TARGET       ");
        builder.AppendLine(FormatCheckoutMask(session.checkoutStations));
    }

    private void AppendEndGameWrap(MatchFlowSession session)
    {
        float elapsed = GetCurrentSessionElapsed(session);

        builder.Append("PHASE        END GAME WRAP   ");
        builder.Append(FormatTime(elapsed));
        builder.Append(" / ");
        builder.AppendLine(FormatTime(session.duration));

        builder.AppendLine("             no new macro resources / checkout entry");
        builder.AppendLine("             Director will request PostGame when this ends");
    }

    #endregion

    #region Zone / Section Details

    private void AppendSectionDetails(ArenaZone zone)
    {
        if (zone == null)
        {
            builder.AppendLine("SECTIONS     ! ArenaZone missing");
            return;
        }

        ZoneLootSpawner[] spawners = currentZoneSectionSpawners;

        if (spawners == null || spawners.Length == 0)
        {
            spawners = zone.GetComponentsInChildren<ZoneLootSpawner>(true);
        }

        if (spawners == null || spawners.Length == 0)
        {
            builder.AppendLine("SECTIONS     --");
            return;
        }

        builder.AppendLine("SECTIONS");

        for (int i = 0; i < spawners.Length; i++)
        {
            ZoneLootSpawner spawner = spawners[i];
            if (spawner == null) continue;

            int baseline = 0;
            sectionSpawnBaseline.TryGetValue(spawner, out baseline);

            int eventSpawned = Mathf.Max(0, spawner.TotalSpawned - baseline);

            builder.Append("  - ");
            builder.Append(PadRight(spawner.SectionLabel, 14));
            builder.Append(" weight ");
            builder.Append(spawner.SelectionWeight.ToString("0.##"));
            builder.Append("   event ");
            builder.Append(eventSpawned);
            builder.Append("   total ");
            builder.Append(spawner.TotalSpawned);
            builder.Append("   prefabs ");
            builder.Append(spawner.ValidPrefabCount);
            builder.Append("   ");
            builder.AppendLine(spawner.CanSpawn ? "READY" : "BLOCKED/INVALID");
        }
    }

    private void AppendAllZoneStates()
    {
        builder.AppendLine();
        builder.Append("ZONES        ");

        if (zones == null || zones.Length == 0)
        {
            builder.AppendLine("--");
            return;
        }

        for (int i = 0; i < zones.Length; i++)
        {
            ArenaZone zone = zones[i];
            if (zone == null) continue;

            if (i > 0) builder.Append(" | ");

            builder.Append(zone.ZoneId);
            builder.Append(":");
            builder.Append(zone.State);
        }

        builder.AppendLine();
    }

    #endregion

    #region Checkout Details

    private void AppendCheckoutStationStates()
    {
        builder.Append("CHECKOUTS    ");

        if (checkoutStations == null || checkoutStations.Length == 0)
        {
            builder.AppendLine("--");
            return;
        }

        for (int i = 0; i < checkoutStations.Length; i++)
        {
            CheckoutStationFlowController station = checkoutStations[i];
            if (station == null) continue;

            if (i > 0) builder.Append(" | ");

            builder.Append(ShortStationName(station.StationId));
            builder.Append(":");
            builder.Append(station.IsOpen ? "OPEN" : "closed");
        }

        builder.AppendLine();
    }

    #endregion

    #region Next Session

    private void AppendNextSession()
    {
        if (matchFlowDirector == null || matchFlowDirector.Profile == null) return;

        int currentIndex = matchFlowDirector.CurrentSessionIndex;
        IReadOnlyList<MatchFlowSession> sessions = matchFlowDirector.Profile.Sessions;

        if (currentIndex < 0 || sessions == null) return;

        for (int i = currentIndex + 1; i < sessions.Count; i++)
        {
            MatchFlowSession next = sessions[i];
            if (next == null) continue;

            builder.AppendLine();
            builder.Append("NEXT         ");
            builder.Append(FormatSessionType(next.type));
            builder.Append(" — ");
            builder.Append(string.IsNullOrWhiteSpace(next.label) ? next.type.ToString() : next.label);
            builder.Append("   ");
            builder.AppendLine(FormatTime(next.GetPlannedDuration()));
            return;
        }

        builder.AppendLine();
        builder.AppendLine("NEXT         -- END OF PROFILE --");
    }

    #endregion

    #region Helpers

    private MatchFlowSession GetCurrentSession()
    {
        if (matchFlowDirector == null || matchFlowDirector.Profile == null) return null;

        int index = matchFlowDirector.CurrentSessionIndex;
        IReadOnlyList<MatchFlowSession> sessions = matchFlowDirector.Profile.Sessions;

        if (sessions == null || index < 0 || index >= sessions.Count) return null;

        return sessions[index];
    }

    private float GetCurrentSessionElapsed(MatchFlowSession currentSession)
    {
        if (matchFlowDirector == null || matchFlowDirector.Profile == null || currentSession == null) return 0f;

        int currentIndex = matchFlowDirector.CurrentSessionIndex;
        IReadOnlyList<MatchFlowSession> sessions = matchFlowDirector.Profile.Sessions;

        if (currentIndex < 0 || sessions == null) return 0f;

        float sessionStart = 0f;

        for (int i = 0; i < currentIndex && i < sessions.Count; i++)
        {
            MatchFlowSession session = sessions[i];
            if (session != null) sessionStart += session.GetPlannedDuration();
        }

        return Mathf.Clamp(
            matchFlowDirector.ElapsedFlowTime - sessionStart,
            0f,
            currentSession.GetPlannedDuration()
        );
    }

    private ArenaZone GetZone(ArenaZoneId zoneId)
    {
        if (zoneById.TryGetValue(zoneId, out ArenaZone zone)) return zone;
        return null;
    }

    private string GetNoActiveSessionMessage()
    {
        if (gameTimeManager != null)
        {
            switch (gameTimeManager.SessionState)
            {
                case GameSessionState.PreGame:
                    return "             waiting in PreGame intro window";

                case GameSessionState.PostGame:
                    return "             playable flow finished / PostGame";

                case GameSessionState.Playing:
                    return "             Director is between/without sessions";
            }
        }

        return "             Director not currently running a session";
    }

    private string FormatSessionType(MatchFlowSessionType type)
    {
        switch (type)
        {
            case MatchFlowSessionType.FreePlay:
                return "FREE PLAY";

            case MatchFlowSessionType.CartRestock:
                return "CART RESTOCK";

            case MatchFlowSessionType.ZoneLoot:
                return "ZONE LOOT";

            case MatchFlowSessionType.CheckoutWindow:
                return "CHECKOUT WINDOW";

            case MatchFlowSessionType.EndGameWrap:
                return "END GAME WRAP";

            default:
                return type.ToString().ToUpperInvariant();
        }
    }

    private string FormatCheckoutMask(CheckoutStationMask mask)
    {
        if (mask == CheckoutStationMask.None) return "NONE";
        if (mask == CheckoutStationMask.All) return "ALL";

        builderMask.Clear();

        AppendMaskName(mask, CheckoutStationMask.North, "North");
        AppendMaskName(mask, CheckoutStationMask.East, "East");
        AppendMaskName(mask, CheckoutStationMask.South, "South");
        AppendMaskName(mask, CheckoutStationMask.West, "West");

        return builderMask.ToString();
    }

    private readonly StringBuilder builderMask = new StringBuilder(64);

    private void AppendMaskName(CheckoutStationMask mask, CheckoutStationMask flag, string name)
    {
        if ((mask & flag) == 0) return;

        if (builderMask.Length > 0) builderMask.Append(" + ");
        builderMask.Append(name);
    }

    private string ShortStationName(CheckoutStationId id)
    {
        switch (id)
        {
            case CheckoutStationId.North:
                return "N";

            case CheckoutStationId.East:
                return "E";

            case CheckoutStationId.South:
                return "S";

            case CheckoutStationId.West:
                return "W";

            default:
                return "?";
        }
    }

    private string FormatTime(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);

        int minutes = Mathf.FloorToInt(seconds / 60f);
        float remainingSeconds = seconds - minutes * 60f;

        return $"{minutes:D2}:{remainingSeconds:00.0}";
    }

    private string PadRight(string value, int width)
    {
        if (string.IsNullOrEmpty(value)) value = "--";
        if (value.Length >= width) return value;

        return value.PadRight(width);
    }

    #endregion
}
