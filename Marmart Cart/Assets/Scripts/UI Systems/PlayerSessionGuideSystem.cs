using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Converts the authored MatchFlowProfile into one shared next-major-event
/// forecast. It never owns player transforms or presentation.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerSessionGuideSystem : MonoBehaviour
{
    private struct ForecastWindow
    {
        public int SourceSessionIndex;
        public int TargetSessionIndex;
        public float StartTime;
        public float EndTime;
    }

    private struct ResolvedGuideTarget
    {
        public ArenaZone ArenaZone;
        public RandomGroundSpawnArea DirectArea;
        public Transform Anchor;
        public string DisplayName;
    }

    #region References

    [Header("Match Flow")]
    [SerializeField] private MatchFlowDirector matchFlowDirector;

    [Header("Five Hard Zone References")]
    [SerializeField] private ArenaZone zoneA;
    [SerializeField] private ArenaZone zoneB;
    [SerializeField] private ArenaZone zoneC;
    [SerializeField] private ArenaZone zoneD;

    [Tooltip(
        "PRIMARY RandomGroundSpawnArea for Center / Checkout + Restock. " +
        "Its Transform remains the arrow target. Its authored rectangle is also " +
        "included in the Center arrival-area union."
    )]
    [SerializeField] private RandomGroundSpawnArea centerZoneArea;

    [Tooltip(
        "Optional EXTRA rectangles used ONLY by the Player Session Guide when " +
        "testing whether a player has reached Center. These areas are never used " +
        "for cart spawning, so they can freely refine the Center shape."
    )]
    [SerializeField] private RandomGroundSpawnArea[] centerZoneGuideOnlyAreas;

    [SerializeField] private string centerDisplayName = "Center";

    [Header("Target Range")]
    [Tooltip("Optional world-space X/Z padding added around every authored spawn rectangle.")]
    [Min(0f)]
    [SerializeField] private float targetRangePadding;

    #endregion

    #region Runtime

    [Header("Runtime - Read Only")]
    [SerializeField]
    private PlayerSessionGuideState currentState =
        new PlayerSessionGuideState();

    [SerializeField] private int forecastWindowCount;
    [SerializeField] private bool monitorRunning;

    private readonly List<ForecastWindow> forecastWindows =
        new List<ForecastWindow>();

    private MatchFlowProfile cachedProfile;
    private int cachedSessionCount = -1;
    private Coroutine monitorRoutine;

    // Tutorial Zone Loot is the only guide mode with a different destination
    // per local player. These states mirror the shared forecast timing/phase
    // while replacing only the resolved zone target.
    private readonly PlayerSessionGuideState[] tutorialPlayerStates =
    {
        new PlayerSessionGuideState(),
        new PlayerSessionGuideState(),
        new PlayerSessionGuideState(),
        new PlayerSessionGuideState()
    };

    public PlayerSessionGuideState CurrentState => currentState;
    public float TargetRangePadding => targetRangePadding;

    public event Action<PlayerSessionGuideState> OnGuideStateChanged;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (matchFlowDirector == null)
        {
            matchFlowDirector = FindFirstObjectByType<MatchFlowDirector>();
        }

        EnsureState();
        RebuildForecastSchedule();
    }

    private void OnEnable()
    {
        SubscribeToDirector();
        RefreshAndManageMonitor();
    }

    private void OnDisable()
    {
        UnsubscribeFromDirector();
        StopMonitor();

        if (currentState != null && currentState.Clear())
        {
            OnGuideStateChanged?.Invoke(currentState);
        }
    }

    private void OnValidate()
    {
        targetRangePadding = Mathf.Max(0f, targetRangePadding);
        EnsureState();
    }

    #endregion

    #region Public Query API

    public bool TryGetActiveState(out PlayerSessionGuideState state)
    {
        state = currentState;
        return state != null && state.Visible;
    }

    public bool IsPointInsideCurrentTarget(Vector3 worldPoint)
    {
        return IsPointInsideGuideTarget(currentState, worldPoint);
    }

    /// <summary>
    /// Player-aware version of TryGetActiveState.
    ///
    /// Existing session types still return the original shared state exactly
    /// as before. TutorialZoneLoot is the one exception: P1/P2/P3/P4 receive
    /// ZoneA/ZoneB/ZoneC/ZoneD while sharing the same countdown, indication
    /// phase, and session indices.
    /// </summary>
    public bool TryGetActiveStateForPlayer(
        int playerIndex,
        out PlayerSessionGuideState state)
    {
        if (!TryGetActiveState(out PlayerSessionGuideState sharedState) ||
            sharedState == null)
        {
            state = sharedState;
            return false;
        }

        if (!TryGetStateTargetSession(
                sharedState,
                out MatchFlowSession targetSession
            ) ||
            targetSession.type != MatchFlowSessionType.TutorialZoneLoot)
        {
            state = sharedState;
            return true;
        }

        if (!TryGetTutorialZoneIdForPlayer(
                playerIndex,
                out ArenaZoneId playerZoneId
            ))
        {
            // The renderer only supplies valid local player indices (1..4).
            // Keep a safe fallback for any external caller without changing
            // the original shared-query behavior.
            state = sharedState;
            return true;
        }

        ResolvedGuideTarget playerTarget =
            ResolveZoneTarget(playerZoneId);

        PlayerSessionGuideState playerState =
            tutorialPlayerStates[playerIndex - 1];

        playerState.Apply(
            PlayerSessionGuideTargetKind.ZoneLoot,
            sharedState.IndicationPhase,
            sharedState.SourceSessionIndex,
            sharedState.TargetSessionIndex,
            playerZoneId,
            playerTarget.ArenaZone,
            playerTarget.DirectArea,
            playerTarget.Anchor,
            playerTarget.DisplayName,
            sharedState.SecondsUntilActive
        );

        state = playerState;
        return true;
    }

    /// <summary>
    /// Player-aware arrival test used only by per-player rendering.
    /// Normal sessions resolve to the same shared target as before.
    /// </summary>
    public bool IsPointInsideCurrentTargetForPlayer(
        int playerIndex,
        Vector3 worldPoint)
    {
        if (!TryGetActiveStateForPlayer(
                playerIndex,
                out PlayerSessionGuideState state
            ))
        {
            return false;
        }

        return IsPointInsideGuideTarget(state, worldPoint);
    }

    [ContextMenu("Rebuild Forecast Schedule")]
    public void RebuildForecastSchedule()
    {
        forecastWindows.Clear();
        forecastWindowCount = 0;

        MatchFlowProfile profile =
            matchFlowDirector != null ? matchFlowDirector.Profile : null;

        cachedProfile = profile;
        cachedSessionCount =
            profile != null && profile.Sessions != null
                ? profile.Sessions.Count
                : 0;

        if (profile == null ||
            profile.Sessions == null ||
            profile.Sessions.Count == 0)
        {
            return;
        }

        int sessionCount = profile.Sessions.Count;
        float[] sessionStartTimes = new float[sessionCount];
        float cursor = 0f;

        for (int i = 0; i < sessionCount; i++)
        {
            sessionStartTimes[i] = cursor;

            MatchFlowSession session = profile.Sessions[i];
            if (session != null) cursor += session.GetPlannedDuration();
        }

        for (int sourceIndex = 0; sourceIndex < sessionCount; sourceIndex++)
        {
            MatchFlowSession source = profile.Sessions[sourceIndex];
            if (!IsMajorSession(source)) continue;

            int targetIndex = FindNextMajorSessionIndex(
                profile,
                sourceIndex + 1
            );

            if (targetIndex < 0) continue;

            MatchFlowSession target = profile.Sessions[targetIndex];

            float windowStart =
                sessionStartTimes[sourceIndex] +
                Mathf.Max(0f, source.telegraphDuration) +
                Mathf.Max(0f, source.duration) +
                Mathf.Max(0f, source.freePlayDuration);

            float windowEnd =
                sessionStartTimes[targetIndex] +
                Mathf.Max(0f, target.telegraphDuration);

            if (windowEnd <= windowStart) continue;

            forecastWindows.Add(
                new ForecastWindow
                {
                    SourceSessionIndex = sourceIndex,
                    TargetSessionIndex = targetIndex,
                    StartTime = windowStart,
                    EndTime = windowEnd
                }
            );
        }

        forecastWindowCount = forecastWindows.Count;
    }

    #endregion

    #region Director Events

    private void SubscribeToDirector()
    {
        if (matchFlowDirector == null)
        {
            matchFlowDirector = FindFirstObjectByType<MatchFlowDirector>();
        }

        if (matchFlowDirector == null) return;

        matchFlowDirector.OnSessionStarted -= HandleSessionStarted;
        matchFlowDirector.OnSessionStarted += HandleSessionStarted;

        matchFlowDirector.OnSessionEnded -= HandleSessionEnded;
        matchFlowDirector.OnSessionEnded += HandleSessionEnded;

        matchFlowDirector.OnSessionPhaseChanged -= HandleSessionPhaseChanged;
        matchFlowDirector.OnSessionPhaseChanged += HandleSessionPhaseChanged;

        matchFlowDirector.OnFlowCompleted -= HandleFlowCompleted;
        matchFlowDirector.OnFlowCompleted += HandleFlowCompleted;
    }

    private void UnsubscribeFromDirector()
    {
        if (matchFlowDirector == null) return;

        matchFlowDirector.OnSessionStarted -= HandleSessionStarted;
        matchFlowDirector.OnSessionEnded -= HandleSessionEnded;
        matchFlowDirector.OnSessionPhaseChanged -= HandleSessionPhaseChanged;
        matchFlowDirector.OnFlowCompleted -= HandleFlowCompleted;
    }

    private void HandleSessionStarted(int sessionIndex, MatchFlowSession session)
    {
        EnsureForecastScheduleCurrent();
        RefreshAndManageMonitor();
    }

    private void HandleSessionEnded(int sessionIndex, MatchFlowSession session)
    {
        RefreshAndManageMonitor();
    }

    private void HandleSessionPhaseChanged(
        int sessionIndex,
        MatchFlowSession session,
        MatchFlowSessionPhase phase)
    {
        RefreshAndManageMonitor();
    }

    private void HandleFlowCompleted()
    {
        StopMonitor();
        ClearState();
    }

    #endregion

    #region Forecast Evaluation

    private void RefreshAndManageMonitor()
    {
        bool visible = EvaluateAndApplyCurrentGuide();

        if (visible)
        {
            StartMonitorIfNeeded();
        }
        else
        {
            StopMonitor();
        }
    }

    private bool EvaluateAndApplyCurrentGuide()
    {
        EnsureState();
        EnsureForecastScheduleCurrent();

        if (matchFlowDirector == null ||
            !matchFlowDirector.IsRunning ||
            cachedProfile == null)
        {
            ClearState();
            return false;
        }

        float elapsed = matchFlowDirector.ElapsedFlowTime;

        if (TryApplyActiveIndication())
        {
            return true;
        }

        if (!TryFindCurrentWindow(elapsed, out ForecastWindow window))
        {
            ClearState();
            return false;
        }

        MatchFlowSession target =
            cachedProfile.Sessions[window.TargetSessionIndex];

        if (target == null)
        {
            ClearState();
            return false;
        }

        return ApplyGuideState(
            target,
            PlayerSessionGuideIndicationPhase.Countdown,
            window.SourceSessionIndex,
            window.TargetSessionIndex,
            window.EndTime - elapsed
        );
    }

    private bool TryApplyActiveIndication()
    {
        MatchFlowSessionPhase phase = matchFlowDirector.CurrentSessionPhase;

        if (phase != MatchFlowSessionPhase.Active)
        {
            return false;
        }

        int sessionIndex = matchFlowDirector.CurrentSessionIndex;

        if (sessionIndex < 0 ||
            cachedProfile.Sessions == null ||
            sessionIndex >= cachedProfile.Sessions.Count)
        {
            return false;
        }

        MatchFlowSession session = cachedProfile.Sessions[sessionIndex];
        if (!IsMajorSession(session)) return false;

        return ApplyGuideState(
            session,
            PlayerSessionGuideIndicationPhase.Active,
            sessionIndex,
            sessionIndex,
            0f
        );
    }

    private bool ApplyGuideState(
        MatchFlowSession target,
        PlayerSessionGuideIndicationPhase indicationPhase,
        int sourceSessionIndex,
        int targetSessionIndex,
        float secondsUntilActive)
    {
        PlayerSessionGuideTargetKind kind =
            target.type == MatchFlowSessionType.ZoneLoot ||
            target.type == MatchFlowSessionType.TutorialZoneLoot
                ? PlayerSessionGuideTargetKind.ZoneLoot
                : PlayerSessionGuideTargetKind.CheckoutRestock;

        ResolvedGuideTarget resolvedTarget = ResolveTarget(target);

        bool changed = currentState.Apply(
            kind,
            indicationPhase,
            sourceSessionIndex,
            targetSessionIndex,
            target.zone,
            resolvedTarget.ArenaZone,
            resolvedTarget.DirectArea,
            resolvedTarget.Anchor,
            resolvedTarget.DisplayName,
            secondsUntilActive
        );

        if (changed) OnGuideStateChanged?.Invoke(currentState);
        return true;
    }

    private bool TryFindCurrentWindow(
        float elapsed,
        out ForecastWindow result)
    {
        const float boundaryTolerance = 0.075f;

        int currentIndex = matchFlowDirector.CurrentSessionIndex;
        MatchFlowSessionPhase phase = matchFlowDirector.CurrentSessionPhase;

        for (int i = 0; i < forecastWindows.Count; i++)
        {
            ForecastWindow candidate = forecastWindows[i];

            if (currentIndex < candidate.SourceSessionIndex ||
                currentIndex > candidate.TargetSessionIndex)
            {
                continue;
            }

            if (currentIndex == candidate.SourceSessionIndex)
            {
                bool sourceWindowOpen =
                    phase == MatchFlowSessionPhase.Closing ||
                    (
                        phase == MatchFlowSessionPhase.None &&
                        elapsed >= candidate.StartTime - boundaryTolerance
                    );

                if (!sourceWindowOpen) continue;
            }
            else if (currentIndex == candidate.TargetSessionIndex)
            {
                // This explicit phase check makes the guide disappear at the
                // exact Warning -> Active boundary, even if frame-level wait
                // rounding has left elapsedFlowTime slightly behind schedule.
                bool targetWindowOpen =
                    phase == MatchFlowSessionPhase.Warning ||
                    (
                        phase == MatchFlowSessionPhase.None &&
                        elapsed < candidate.EndTime + boundaryTolerance
                    );

                if (!targetWindowOpen) continue;
            }
            // Any authored minor session between source and target is inside
            // the same forecast window but is never itself announced.

            result = candidate;
            return true;
        }

        result = default(ForecastWindow);
        return false;
    }

    private ResolvedGuideTarget ResolveTarget(MatchFlowSession target)
    {
        ResolvedGuideTarget result = default(ResolvedGuideTarget);
        if (target == null) return result;

        if (target.type == MatchFlowSessionType.CheckoutRestock)
        {
            result.DirectArea = centerZoneArea;
            result.Anchor =
                centerZoneArea != null ? centerZoneArea.transform : null;
            result.DisplayName =
                string.IsNullOrWhiteSpace(centerDisplayName)
                    ? "Center"
                    : centerDisplayName;

            return result;
        }

        ArenaZone resolvedZone;

        switch (target.zone)
        {
            case ArenaZoneId.ZoneA:
                resolvedZone = zoneA;
                break;

            case ArenaZoneId.ZoneB:
                resolvedZone = zoneB;
                break;

            case ArenaZoneId.ZoneC:
                resolvedZone = zoneC;
                break;

            case ArenaZoneId.ZoneD:
                resolvedZone = zoneD;
                break;

            default:
                resolvedZone = null;
                break;
        }

        result.ArenaZone = resolvedZone;
        result.Anchor = resolvedZone != null ? resolvedZone.transform : null;
        result.DisplayName =
            resolvedZone != null
                ? resolvedZone.DisplayName
                : target.zone.ToString();

        return result;
    }

    private bool TryGetStateTargetSession(
        PlayerSessionGuideState state,
        out MatchFlowSession session)
    {
        session = null;

        if (state == null ||
            cachedProfile == null ||
            cachedProfile.Sessions == null)
        {
            return false;
        }

        int targetIndex = state.TargetSessionIndex;

        if (targetIndex < 0 ||
            targetIndex >= cachedProfile.Sessions.Count)
        {
            return false;
        }

        session = cachedProfile.Sessions[targetIndex];
        return session != null;
    }

    private static bool TryGetTutorialZoneIdForPlayer(
        int playerIndex,
        out ArenaZoneId zoneId)
    {
        switch (playerIndex)
        {
            case 1:
                zoneId = ArenaZoneId.ZoneA;
                return true;

            case 2:
                zoneId = ArenaZoneId.ZoneB;
                return true;

            case 3:
                zoneId = ArenaZoneId.ZoneC;
                return true;

            case 4:
                zoneId = ArenaZoneId.ZoneD;
                return true;

            default:
                zoneId = ArenaZoneId.ZoneA;
                return false;
        }
    }

    private ResolvedGuideTarget ResolveZoneTarget(ArenaZoneId zoneId)
    {
        ResolvedGuideTarget result = default(ResolvedGuideTarget);
        ArenaZone resolvedZone;

        switch (zoneId)
        {
            case ArenaZoneId.ZoneA:
                resolvedZone = zoneA;
                break;

            case ArenaZoneId.ZoneB:
                resolvedZone = zoneB;
                break;

            case ArenaZoneId.ZoneC:
                resolvedZone = zoneC;
                break;

            case ArenaZoneId.ZoneD:
                resolvedZone = zoneD;
                break;

            default:
                resolvedZone = null;
                break;
        }

        result.ArenaZone = resolvedZone;
        result.Anchor =
            resolvedZone != null ? resolvedZone.transform : null;
        result.DisplayName =
            resolvedZone != null
                ? resolvedZone.DisplayName
                : zoneId.ToString();

        return result;
    }

    /// <summary>
    /// Shared arrival test for both the original shared guide query and the
    /// player-aware tutorial query.
    ///
    /// Arena zones already own a union of section spawn rectangles.
    /// Checkout + Restock uses the same union concept here: the primary Center
    /// area plus any guide-only refinement rectangles.
    /// </summary>
    private bool IsPointInsideGuideTarget(
        PlayerSessionGuideState state,
        Vector3 worldPoint)
    {
        if (state == null || !state.Visible)
        {
            return false;
        }

        if (state.TargetZone != null)
        {
            return state.TargetZone.ContainsSessionGuidePoint(
                worldPoint,
                targetRangePadding
            );
        }

        // Checkout + Restock is the Center target. Its displayed/arrow target
        // remains the primary centerZoneArea, but arrival accepts the union of
        // every authored Center guide rectangle.
        if (state.TargetKind ==
            PlayerSessionGuideTargetKind.CheckoutRestock)
        {
            return ContainsCenterGuidePoint(worldPoint);
        }

        if (state.TargetDirectArea != null)
        {
            return state.TargetDirectArea.ContainsHorizontalPoint(
                worldPoint,
                targetRangePadding
            );
        }

        return false;
    }

    /// <summary>
    /// Union test for Center.
    ///
    /// centerZoneGuideOnlyAreas are presentation/navigation geometry only.
    /// Nothing in this method requests spawn positions, so adding rectangles
    /// here cannot cause CartRestockSpawner to spawn inside them.
    /// </summary>
    private bool ContainsCenterGuidePoint(Vector3 worldPoint)
    {
        if (centerZoneArea != null &&
            centerZoneArea.ContainsHorizontalPoint(
                worldPoint,
                targetRangePadding
            ))
        {
            return true;
        }

        if (centerZoneGuideOnlyAreas == null)
        {
            return false;
        }

        for (int i = 0; i < centerZoneGuideOnlyAreas.Length; i++)
        {
            RandomGroundSpawnArea area =
                centerZoneGuideOnlyAreas[i];

            if (area == null || area == centerZoneArea)
            {
                continue;
            }

            if (area.ContainsHorizontalPoint(
                    worldPoint,
                    targetRangePadding
                ))
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureForecastScheduleCurrent()
    {
        MatchFlowProfile profile =
            matchFlowDirector != null ? matchFlowDirector.Profile : null;

        int sessionCount =
            profile != null && profile.Sessions != null
                ? profile.Sessions.Count
                : 0;

        if (profile != cachedProfile || sessionCount != cachedSessionCount)
        {
            RebuildForecastSchedule();
        }
    }

    private static bool IsMajorSession(MatchFlowSession session)
    {
        return
            session != null &&
            (
                session.type == MatchFlowSessionType.ZoneLoot ||
                session.type == MatchFlowSessionType.TutorialZoneLoot ||
                session.type == MatchFlowSessionType.CheckoutRestock
            );
    }

    private static int FindNextMajorSessionIndex(
        MatchFlowProfile profile,
        int startIndex)
    {
        if (profile == null || profile.Sessions == null) return -1;

        for (int i = Mathf.Max(0, startIndex); i < profile.Sessions.Count; i++)
        {
            if (IsMajorSession(profile.Sessions[i])) return i;
        }

        return -1;
    }

    #endregion

    #region Monitor Coroutine

    private void StartMonitorIfNeeded()
    {
        if (monitorRoutine != null) return;

        monitorRoutine = StartCoroutine(MonitorVisibleGuide());
        monitorRunning = true;
    }

    private void StopMonitor()
    {
        if (monitorRoutine != null)
        {
            StopCoroutine(monitorRoutine);
            monitorRoutine = null;
        }

        monitorRunning = false;
    }

    private IEnumerator MonitorVisibleGuide()
    {
        while (isActiveAndEnabled)
        {
            if (!EvaluateAndApplyCurrentGuide()) break;
            yield return null;
        }

        monitorRoutine = null;
        monitorRunning = false;
    }

    #endregion

    #region Helpers

    private void EnsureState()
    {
        if (currentState == null)
        {
            currentState = new PlayerSessionGuideState();
        }
    }

    private void ClearState()
    {
        EnsureState();

        if (currentState.Clear())
        {
            OnGuideStateChanged?.Invoke(currentState);
        }
    }

    #endregion
}
