using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Scene-level Tomato behavior. It converts valid Tomato leading-cart impacts
/// into per-player duration state and publishes effect lifecycle events.
///
/// It contains no Canvas, SFX, VFX, or projectile-destruction logic.
/// </summary>
[DisallowMultipleComponent]
public class TomatoBlindEffectSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PowerupLifecycleEventSystem lifecycleEventSystem;
    [SerializeField] private PowerupRuntimeSystem runtimeSystem;
    [SerializeField] private TomatoPowerupProfile tomatoProfile;

    [Header("Diagnostics")]
    [SerializeField] private bool logStateChanges = true;

    [Header("Runtime - Read Only")]
    [SerializeField]
    private TomatoBlindState[] playerStates =
        new TomatoBlindState[PowerupRuntimeSystem.MaxPlayerSlots];

    private readonly Coroutine[] expiryRoutines =
        new Coroutine[PowerupRuntimeSystem.MaxPlayerSlots];

    private PowerupLifecycleEventSystem subscribedLifecycleEventSystem;
    private PowerupRuntimeSystem subscribedRuntimeSystem;
    private bool missingProfileLogged;

    public TomatoPowerupProfile TomatoProfile => tomatoProfile;

    public event Action<int, TomatoBlindState> OnBlindStateChanged;
    public event Action<
        int,
        TomatoBlindState,
        PowerupEffectEndReason> OnBlindStateEnded;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        EnsureStateStorage();
        ResetStateStorage();
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureSubscriptions();
    }

    private void Start()
    {
        // Supports scene objects whose references finish initializing after
        // this component's OnEnable.
        ResolveReferences();
        EnsureSubscriptions();
    }

    private void OnDisable()
    {
        ClearAllActiveStates(
            PowerupEffectEndReason.SystemDisabled,
            true
        );
        Unsubscribe();
    }

    private void OnValidate()
    {
        EnsureStateStorage();
    }

    public bool TryGetState(
        int playerIndex,
        out TomatoBlindState state)
    {
        int slotIndex = playerIndex - 1;

        if (slotIndex < 0 || slotIndex >= playerStates.Length)
        {
            state = default;
            return false;
        }

        state = playerStates[slotIndex];
        return state.Active;
    }

    public void ClearPlayerState(
        int playerIndex,
        PowerupEffectEndReason endReason =
            PowerupEffectEndReason.Cancelled)
    {
        int slotIndex = playerIndex - 1;

        if (slotIndex < 0 || slotIndex >= playerStates.Length) return;

        EndBlindState(slotIndex, endReason, true);
    }

    private void HandleProjectileImpacted(
        PowerupProjectileEvent projectileEvent)
    {
        PowerupProjectileCompletion completion =
            projectileEvent.Completion;

        if (completion.PowerupId != PowerupId.Tomato ||
            completion.Reason !=
                PowerupProjectileCompletionReason.LeadingCartHit ||
            completion.HitCartKind !=
                PowerupCartTargetKind.LeadingCart)
        {
            return;
        }

        if (tomatoProfile == null)
        {
            if (!missingProfileLogged)
            {
                missingProfileLogged = true;
                Debug.LogError(
                    "[TomatoBlindEffectSystem] Assign a " +
                    "TomatoPowerupProfile. Valid impacts cannot create a " +
                    "blind state without effect tuning.",
                    this
                );
            }

            return;
        }

        missingProfileLogged = false;

        int targetPlayerIndex = completion.HitPlayerIndex;
        int slotIndex = targetPlayerIndex - 1;

        if (slotIndex < 0 || slotIndex >= playerStates.Length) return;

        PlayerPowerupController sourceController = null;
        PlayerPowerupController targetController =
            completion.HitOwnerController;

        if (runtimeSystem != null)
        {
            runtimeSystem.TryGetPlayer(
                completion.OwnerPlayerIndex,
                out sourceController
            );

            if (targetController == null)
            {
                runtimeSystem.TryGetPlayer(
                    targetPlayerIndex,
                    out targetController
                );
            }
        }

        ApplyBlindState(
            slotIndex,
            completion,
            sourceController,
            targetController
        );
    }

    private void ApplyBlindState(
        int slotIndex,
        PowerupProjectileCompletion completion,
        PlayerPowerupController sourceController,
        PlayerPowerupController targetController)
    {
        float now = Time.time;
        TomatoBlindState previousState = playerStates[slotIndex];
        bool wasActive =
            previousState.Active &&
            previousState.EndsAtTime > now;

        if (previousState.Active && !wasActive)
        {
            EndBlindState(
                slotIndex,
                PowerupEffectEndReason.DurationExpired,
                true
            );

            previousState = playerStates[slotIndex];
        }

        float duration = tomatoProfile.BlindDurationSeconds;
        bool refreshDuration =
            !wasActive ||
            tomatoProfile.RefreshDurationOnAdditionalHit;

        float startedAt = refreshDuration
            ? now
            : previousState.StartedAtTime;

        float endsAt = refreshDuration
            ? now + duration
            : previousState.EndsAtTime;

        int splashCount = wasActive
            ? Mathf.Min(
                tomatoProfile.MaximumVisualSplashCount,
                previousState.SplashCount + 1
            )
            : 1;

        uint effectInstanceId = wasActive
            ? previousState.EffectInstanceId
            : lifecycleEventSystem != null
                ? lifecycleEventSystem.ReserveEffectInstanceId()
                : NextRevision(previousState.Revision);

        TomatoBlindState nextState = new TomatoBlindState
        {
            TargetPlayerIndex = slotIndex + 1,
            Active = true,
            Revision = NextRevision(previousState.Revision),
            EffectInstanceId = effectInstanceId,
            SourceActivationVersion = completion.ShotVersion,
            SourcePatternEntryIndex = completion.PatternEntryIndex,
            SourcePlayerIndex = completion.OwnerPlayerIndex,
            SourceController = sourceController,
            TargetController = targetController,
            SplashCount = splashCount,
            DurationSeconds = Mathf.Max(0.0001f, endsAt - startedAt),
            StartedAtTime = startedAt,
            EndsAtTime = endsAt,
            LastImpactPosition = completion.Position
        };

        playerStates[slotIndex] = nextState;

        if (refreshDuration)
        {
            RestartExpiryRoutine(slotIndex);
        }

        InvokeBlindStateChanged(slotIndex + 1, nextState);

        PowerupEffectEvent effectEvent = BuildEffectEvent(nextState);

        if (wasActive)
        {
            lifecycleEventSystem?.PublishEffectRefreshed(effectEvent);
        }
        else
        {
            lifecycleEventSystem?.PublishEffectStarted(effectEvent);
        }

        if (logStateChanges)
        {
            Debug.Log(
                $"[TomatoBlindEffectSystem] P{slotIndex + 1} " +
                $"Tomato blind {(wasActive ? "refreshed" : "started")} " +
                $"with {splashCount} splash layer(s) until " +
                $"{endsAt:0.###}.",
                this
            );
        }
    }

    private void RestartExpiryRoutine(int slotIndex)
    {
        if (expiryRoutines[slotIndex] != null)
        {
            StopCoroutine(expiryRoutines[slotIndex]);
        }

        expiryRoutines[slotIndex] =
            StartCoroutine(ExpireBlindStateRoutine(slotIndex));
    }

    private IEnumerator ExpireBlindStateRoutine(int slotIndex)
    {
        while (playerStates[slotIndex].Active)
        {
            float remaining =
                playerStates[slotIndex].EndsAtTime - Time.time;

            if (remaining <= 0f) break;

            // Scaled time is intentional: duration gameplay effects pause with
            // the match. Match-end state also clears explicitly below.
            yield return new WaitForSeconds(remaining);
        }

        expiryRoutines[slotIndex] = null;
        EndBlindState(
            slotIndex,
            PowerupEffectEndReason.DurationExpired,
            true
        );
    }

    private void EndBlindState(
        int slotIndex,
        PowerupEffectEndReason endReason,
        bool publishLifecycleEvent)
    {
        if (slotIndex < 0 || slotIndex >= playerStates.Length) return;

        TomatoBlindState endingState = playerStates[slotIndex];
        if (!endingState.Active) return;

        Coroutine expiryRoutine = expiryRoutines[slotIndex];

        if (expiryRoutine != null)
        {
            StopCoroutine(expiryRoutine);
            expiryRoutines[slotIndex] = null;
        }

        endingState.Active = false;
        endingState.Revision = NextRevision(endingState.Revision);
        endingState.EndsAtTime = Mathf.Min(
            endingState.EndsAtTime,
            Time.time
        );
        playerStates[slotIndex] = endingState;

        InvokeBlindStateEnded(
            slotIndex + 1,
            endingState,
            endReason
        );

        if (publishLifecycleEvent)
        {
            lifecycleEventSystem?.PublishEffectEnded(
                BuildEffectEvent(endingState),
                endReason
            );
        }

        if (logStateChanges)
        {
            Debug.Log(
                $"[TomatoBlindEffectSystem] P{slotIndex + 1} " +
                $"Tomato blind ended: {endReason}.",
                this
            );
        }
    }

    private PowerupEffectEvent BuildEffectEvent(
        TomatoBlindState state)
    {
        return new PowerupEffectEvent
        {
            EffectId = PowerupEffectId.TomatoBlind,
            SourcePowerupId = PowerupId.Tomato,
            ActivationVersion = state.SourceActivationVersion,
            EffectInstanceId = state.EffectInstanceId,
            EffectVersion = state.Revision,
            SourcePatternEntryIndex = state.SourcePatternEntryIndex,
            OwnerPlayerIndex = state.SourcePlayerIndex,
            TargetPlayerIndex = state.TargetPlayerIndex,
            OwnerController = state.SourceController,
            TargetController = state.TargetController,
            Position = state.LastImpactPosition,
            DurationSeconds = state.DurationSeconds,
            StartedAtTime = state.StartedAtTime,
            EndsAtTime = state.EndsAtTime,
            StackCount = state.SplashCount
        };
    }

    private void HandleMatchPlayingChanged(bool matchPlaying)
    {
        if (matchPlaying) return;

        ClearAllActiveStates(
            PowerupEffectEndReason.MatchEnded,
            true
        );
    }

    private void HandlePlayerUnregistered(int playerIndex)
    {
        int slotIndex = playerIndex - 1;

        if (slotIndex < 0 || slotIndex >= playerStates.Length) return;

        EndBlindState(
            slotIndex,
            PowerupEffectEndReason.TargetUnavailable,
            true
        );
    }

    private void ClearAllActiveStates(
        PowerupEffectEndReason endReason,
        bool publishLifecycleEvents)
    {
        EnsureStateStorage();

        for (int i = 0; i < playerStates.Length; i++)
        {
            EndBlindState(i, endReason, publishLifecycleEvents);
        }
    }

    private void ResolveReferences()
    {
        if (lifecycleEventSystem == null)
        {
            lifecycleEventSystem =
                FindFirstObjectByType<PowerupLifecycleEventSystem>();
        }

        if (runtimeSystem == null)
        {
            runtimeSystem = FindFirstObjectByType<PowerupRuntimeSystem>();
        }
    }

    private void EnsureSubscriptions()
    {
        if (subscribedLifecycleEventSystem != lifecycleEventSystem)
        {
            if (subscribedLifecycleEventSystem != null)
            {
                subscribedLifecycleEventSystem.OnProjectileImpacted -=
                    HandleProjectileImpacted;
            }

            subscribedLifecycleEventSystem = lifecycleEventSystem;

            if (subscribedLifecycleEventSystem != null)
            {
                subscribedLifecycleEventSystem.OnProjectileImpacted +=
                    HandleProjectileImpacted;
            }
        }

        if (subscribedRuntimeSystem != runtimeSystem)
        {
            if (subscribedRuntimeSystem != null)
            {
                subscribedRuntimeSystem.OnMatchPlayingChanged -=
                    HandleMatchPlayingChanged;
                subscribedRuntimeSystem.OnPlayerUnregistered -=
                    HandlePlayerUnregistered;
            }

            subscribedRuntimeSystem = runtimeSystem;

            if (subscribedRuntimeSystem != null)
            {
                subscribedRuntimeSystem.OnMatchPlayingChanged +=
                    HandleMatchPlayingChanged;
                subscribedRuntimeSystem.OnPlayerUnregistered +=
                    HandlePlayerUnregistered;
            }
        }
    }

    private void Unsubscribe()
    {
        if (subscribedLifecycleEventSystem != null)
        {
            subscribedLifecycleEventSystem.OnProjectileImpacted -=
                HandleProjectileImpacted;
        }

        if (subscribedRuntimeSystem != null)
        {
            subscribedRuntimeSystem.OnMatchPlayingChanged -=
                HandleMatchPlayingChanged;
            subscribedRuntimeSystem.OnPlayerUnregistered -=
                HandlePlayerUnregistered;
        }

        subscribedLifecycleEventSystem = null;
        subscribedRuntimeSystem = null;
    }

    private void EnsureStateStorage()
    {
        if (playerStates == null ||
            playerStates.Length != PowerupRuntimeSystem.MaxPlayerSlots)
        {
            Array.Resize(
                ref playerStates,
                PowerupRuntimeSystem.MaxPlayerSlots
            );
        }
    }

    private void ResetStateStorage()
    {
        for (int i = 0; i < playerStates.Length; i++)
        {
            playerStates[i] = new TomatoBlindState
            {
                TargetPlayerIndex = i + 1
            };
        }
    }

    private void InvokeBlindStateChanged(
        int playerIndex,
        TomatoBlindState state)
    {
        Action<int, TomatoBlindState> handlers = OnBlindStateChanged;
        if (handlers == null) return;

        Delegate[] invocationList = handlers.GetInvocationList();

        for (int i = 0; i < invocationList.Length; i++)
        {
            try
            {
                ((Action<int, TomatoBlindState>)invocationList[i]).Invoke(
                    playerIndex,
                    state
                );
            }
            catch (Exception exception)
            {
                LogSubscriberException(nameof(OnBlindStateChanged), exception);
            }
        }
    }

    private void InvokeBlindStateEnded(
        int playerIndex,
        TomatoBlindState state,
        PowerupEffectEndReason endReason)
    {
        Action<int, TomatoBlindState, PowerupEffectEndReason> handlers =
            OnBlindStateEnded;

        if (handlers == null) return;

        Delegate[] invocationList = handlers.GetInvocationList();

        for (int i = 0; i < invocationList.Length; i++)
        {
            try
            {
                ((Action<
                    int,
                    TomatoBlindState,
                    PowerupEffectEndReason>)invocationList[i]).Invoke(
                        playerIndex,
                        state,
                        endReason
                    );
            }
            catch (Exception exception)
            {
                LogSubscriberException(nameof(OnBlindStateEnded), exception);
            }
        }
    }

    private void LogSubscriberException(
        string eventName,
        Exception exception)
    {
        Debug.LogError(
            $"[TomatoBlindEffectSystem] A subscriber to {eventName} threw " +
            "an exception. Tomato gameplay state remains authoritative.",
            this
        );
        Debug.LogException(exception, this);
    }

    private static uint NextRevision(uint currentRevision)
    {
        uint nextRevision = unchecked(currentRevision + 1u);
        return nextRevision == 0u ? 1u : nextRevision;
    }
}
