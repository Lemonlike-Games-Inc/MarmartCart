using System;
using UnityEngine;

public enum ColaMentosActivationFailureReason
{
    None,
    MissingProfile,
    InvalidPlayer,
    MissingCartControl,
    InventoryChanged,
    InventoryConsumeFailed
}

/// <summary>
/// Scene-level executor and authoritative duration owner for Cola Mentos.
///
/// It subscribes to every registered PlayerPowerupController, validates the
/// immediate-use request, consumes inventory only after all required runtime
/// references exist, then applies one refreshable fixed-speed state.
/// </summary>
[DefaultExecutionOrder(140)]
[DisallowMultipleComponent]
public class ColaMentosEffectSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PowerupRuntimeSystem runtimeSystem;
    [SerializeField] private PowerupLifecycleEventSystem lifecycleEventSystem;
    [SerializeField] private ColaMentosPowerupProfile colaProfile;

    [Header("Diagnostics")]
    [SerializeField] private bool logStateChanges = true;
    [SerializeField] private bool logActivationFailures = true;

    [Header("Runtime - Read Only")]
    [SerializeField]
    private ColaMentosBoostState[] playerStates =
        new ColaMentosBoostState[PowerupRuntimeSystem.MaxPlayerSlots];
    [SerializeField]
    private ColaMentosActivationFailureReason
        lastFailureReason;
    [SerializeField] private int lastFailurePlayerIndex;

    private readonly PlayerPowerupController[] subscribedPlayers =
        new PlayerPowerupController[PowerupRuntimeSystem.MaxPlayerSlots];

    private PowerupRuntimeSystem subscribedRuntimeSystem;
    private uint fallbackEffectInstanceId;

    public ColaMentosPowerupProfile ColaProfile => colaProfile;
    public ColaMentosActivationFailureReason LastFailureReason =>
        lastFailureReason;

    public event Action<int, ColaMentosBoostState> OnBoostStateChanged;
    public event Action<
        int,
        ColaMentosBoostState,
        PowerupEffectEndReason> OnBoostStateEnded;

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
        EnsureRuntimeSubscription();
    }

    private void Start()
    {
        ResolveReferences();
        EnsureRuntimeSubscription();
    }

    private void Update()
    {
        if (runtimeSystem == null)
        {
            ResolveReferences();
            EnsureRuntimeSubscription();
        }

        for (int i = 0; i < playerStates.Length; i++)
        {
            ColaMentosBoostState state = playerStates[i];
            if (!state.Active) continue;

            if (state.PlayerController == null ||
                state.CartControl == null ||
                state.PlayerController.CartControlInput != state.CartControl)
            {
                EndBoost(
                    i,
                    PowerupEffectEndReason.TargetUnavailable,
                    true
                );
                continue;
            }

            if (Time.time >= state.EndsAtTime)
            {
                EndBoost(
                    i,
                    PowerupEffectEndReason.DurationExpired,
                    true
                );
                continue;
            }

            // Reassert only Cola's independent override if an external setup
            // script temporarily rebuilt the CartControl runtime state.
            if (!state.CartControl.IsColaMentosBoostActive ||
                !Mathf.Approximately(
                    state.CartControl.ColaMentosTargetSpeed,
                    state.FixedTargetSpeed
                ))
            {
                state.CartControl.SetColaMentosBoost(
                    true,
                    state.FixedTargetSpeed
                );
            }
        }
    }

    private void OnDisable()
    {
        ClearAllActiveStates(
            PowerupEffectEndReason.SystemDisabled,
            true
        );
        UnsubscribeRuntimeAndPlayers();
    }

    private void OnValidate()
    {
        EnsureStateStorage();
    }

    public bool TryGetState(
        int playerIndex,
        out ColaMentosBoostState state)
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

        EndBoost(slotIndex, endReason, true);
    }

    private bool ValidatePowerupUse(
        PlayerPowerupController controller,
        PowerupId requestedPowerup)
    {
        if (requestedPowerup != PowerupId.ColaMentos) return true;

        if (colaProfile == null)
        {
            RecordFailure(
                controller,
                ColaMentosActivationFailureReason.MissingProfile
            );
            return false;
        }

        if (controller == null ||
            controller.PlayerIndex < 1 ||
            controller.PlayerIndex > PowerupRuntimeSystem.MaxPlayerSlots)
        {
            RecordFailure(
                controller,
                ColaMentosActivationFailureReason.InvalidPlayer
            );
            return false;
        }

        if (controller.CartControlInput == null)
        {
            RecordFailure(
                controller,
                ColaMentosActivationFailureReason.MissingCartControl
            );
            return false;
        }

        return true;
    }

    private void HandlePowerupUseRequested(
        PlayerPowerupController controller,
        PowerupId requestedPowerup)
    {
        if (requestedPowerup != PowerupId.ColaMentos) return;
        TryActivateOrRefresh(controller);
    }

    private bool TryActivateOrRefresh(
        PlayerPowerupController controller)
    {
        if (!ValidatePowerupUse(controller, PowerupId.ColaMentos))
        {
            return false;
        }

        int slotIndex = controller.PlayerIndex - 1;
        CartControlScript cartControl = controller.CartControlInput;

        if (!controller.HasStoredPowerup ||
            controller.StoredPowerup != PowerupId.ColaMentos)
        {
            RecordFailure(
                controller,
                ColaMentosActivationFailureReason.InventoryChanged
            );
            return false;
        }

        ColaMentosBoostState previousState = playerStates[slotIndex];
        bool wasActive =
            previousState.Active &&
            previousState.EndsAtTime > Time.time;

        if (previousState.Active && !wasActive)
        {
            EndBoost(
                slotIndex,
                PowerupEffectEndReason.DurationExpired,
                true
            );
            previousState = playerStates[slotIndex];
        }

        if (!controller.TryConsumeStoredPowerup(
                out PowerupId consumedPowerup) ||
            consumedPowerup != PowerupId.ColaMentos)
        {
            RecordFailure(
                controller,
                ColaMentosActivationFailureReason.InventoryConsumeFailed
            );
            return false;
        }

        if (wasActive && previousState.CartControl != cartControl)
        {
            EndBoost(
                slotIndex,
                PowerupEffectEndReason.Replaced,
                true
            );
            previousState = playerStates[slotIndex];
            wasActive = false;
        }

        float now = Time.time;
        float duration = colaProfile.DurationSeconds;
        float fixedTargetSpeed = colaProfile.FixedTargetSpeed;
        bool blockOtherPowerupUse =
            colaProfile.BlockOtherPowerupUseWhileActive;

        uint effectInstanceId = wasActive
            ? previousState.EffectInstanceId
            : lifecycleEventSystem != null
                ? lifecycleEventSystem.ReserveEffectInstanceId()
                : NextFallbackEffectInstanceId();

        ColaMentosBoostState nextState = new ColaMentosBoostState
        {
            PlayerIndex = controller.PlayerIndex,
            Active = true,
            Revision = NextRevision(previousState.Revision),
            EffectInstanceId = effectInstanceId,
            ActivationVersion = controller.AcceptedUseRequestVersion,
            PlayerController = controller,
            CartControl = cartControl,
            BlocksOtherPowerupUse = blockOtherPowerupUse,
            FixedTargetSpeed = fixedTargetSpeed,
            DurationSeconds = duration,
            StartedAtTime = now,
            EndsAtTime = now + duration
        };

        cartControl.SetColaMentosBoost(true, fixedTargetSpeed);
        controller.SetUseBlocked(
            PowerupUseBlockReason.ActiveEffectLock,
            blockOtherPowerupUse
        );
        playerStates[slotIndex] = nextState;
        lastFailureReason = ColaMentosActivationFailureReason.None;
        lastFailurePlayerIndex = 0;

        lifecycleEventSystem?.PublishActivated(
            new PowerupActivationEvent
            {
                PowerupId = PowerupId.ColaMentos,
                Mode = PowerupActivationMode.Duration,
                ActivationVersion = nextState.ActivationVersion,
                OwnerPlayerIndex = nextState.PlayerIndex,
                OwnerController = controller,
                Position = cartControl.transform.position,
                Direction = cartControl.transform.forward,
                ProjectileCount = 0
            }
        );

        InvokeBoostStateChanged(nextState.PlayerIndex, nextState);

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
                $"[ColaMentosEffectSystem] P{nextState.PlayerIndex} Cola " +
                $"Mentos {(wasActive ? "refreshed" : "started")} at target " +
                $"speed {fixedTargetSpeed:0.##} until " +
                $"{nextState.EndsAtTime:0.###}.",
                this
            );
        }

        return true;
    }

    private void EndBoost(
        int slotIndex,
        PowerupEffectEndReason endReason,
        bool publishLifecycleEvent)
    {
        if (slotIndex < 0 || slotIndex >= playerStates.Length) return;

        ColaMentosBoostState endingState = playerStates[slotIndex];
        if (!endingState.Active) return;

        if (endingState.CartControl != null)
        {
            endingState.CartControl.SetColaMentosBoost(false, 0f);
        }

        if (endingState.BlocksOtherPowerupUse &&
            endingState.PlayerController != null)
        {
            endingState.PlayerController.SetUseBlocked(
                PowerupUseBlockReason.ActiveEffectLock,
                false
            );
        }

        endingState.Active = false;
        endingState.Revision = NextRevision(endingState.Revision);
        endingState.EndsAtTime = Mathf.Min(
            endingState.EndsAtTime,
            Time.time
        );
        playerStates[slotIndex] = endingState;

        InvokeBoostStateEnded(
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
                $"[ColaMentosEffectSystem] P{slotIndex + 1} Cola Mentos " +
                $"ended: {endReason}.",
                this
            );
        }
    }

    private PowerupEffectEvent BuildEffectEvent(
        ColaMentosBoostState state)
    {
        Vector3 position = state.CartControl != null
            ? state.CartControl.transform.position
            : Vector3.zero;

        return new PowerupEffectEvent
        {
            EffectId = PowerupEffectId.ColaMentosBoost,
            SourcePowerupId = PowerupId.ColaMentos,
            ActivationVersion = state.ActivationVersion,
            EffectInstanceId = state.EffectInstanceId,
            EffectVersion = state.Revision,
            SourcePatternEntryIndex = -1,
            OwnerPlayerIndex = state.PlayerIndex,
            TargetPlayerIndex = state.PlayerIndex,
            OwnerController = state.PlayerController,
            TargetController = state.PlayerController,
            Position = position,
            DurationSeconds = state.DurationSeconds,
            StartedAtTime = state.StartedAtTime,
            EndsAtTime = state.EndsAtTime,
            StackCount = 1
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

    private void HandlePlayerRegistered(
        int playerIndex,
        PlayerPowerupController controller)
    {
        SubscribePlayer(playerIndex, controller);
    }

    private void HandlePlayerUnregistered(int playerIndex)
    {
        int slotIndex = playerIndex - 1;
        if (slotIndex < 0 || slotIndex >= subscribedPlayers.Length) return;

        EndBoost(
            slotIndex,
            PowerupEffectEndReason.TargetUnavailable,
            true
        );
        UnsubscribePlayer(slotIndex);
    }

    private void ClearAllActiveStates(
        PowerupEffectEndReason endReason,
        bool publishLifecycleEvents)
    {
        EnsureStateStorage();

        for (int i = 0; i < playerStates.Length; i++)
        {
            EndBoost(i, endReason, publishLifecycleEvents);
        }
    }

    private void ResolveReferences()
    {
        if (runtimeSystem == null)
        {
            runtimeSystem = FindFirstObjectByType<PowerupRuntimeSystem>();
        }

        if (lifecycleEventSystem == null)
        {
            lifecycleEventSystem =
                FindFirstObjectByType<PowerupLifecycleEventSystem>();
        }
    }

    private void EnsureRuntimeSubscription()
    {
        if (subscribedRuntimeSystem == runtimeSystem)
        {
            SynchronizeRegisteredPlayers();
            return;
        }

        UnsubscribeRuntimeAndPlayers();
        subscribedRuntimeSystem = runtimeSystem;

        if (subscribedRuntimeSystem == null) return;

        subscribedRuntimeSystem.OnPlayerRegistered +=
            HandlePlayerRegistered;
        subscribedRuntimeSystem.OnPlayerUnregistered +=
            HandlePlayerUnregistered;
        subscribedRuntimeSystem.OnMatchPlayingChanged +=
            HandleMatchPlayingChanged;

        SynchronizeRegisteredPlayers();
    }

    private void SynchronizeRegisteredPlayers()
    {
        if (subscribedRuntimeSystem == null) return;

        for (int playerIndex = 1;
             playerIndex <= PowerupRuntimeSystem.MaxPlayerSlots;
             playerIndex++)
        {
            if (subscribedRuntimeSystem.TryGetPlayer(
                    playerIndex,
                    out PlayerPowerupController controller))
            {
                SubscribePlayer(playerIndex, controller);
            }
            else
            {
                UnsubscribePlayer(playerIndex - 1);
            }
        }
    }

    private void SubscribePlayer(
        int playerIndex,
        PlayerPowerupController controller)
    {
        int slotIndex = playerIndex - 1;

        if (slotIndex < 0 ||
            slotIndex >= subscribedPlayers.Length ||
            controller == null)
        {
            return;
        }

        if (subscribedPlayers[slotIndex] == controller) return;

        UnsubscribePlayer(slotIndex);
        subscribedPlayers[slotIndex] = controller;
        controller.OnPowerupUseValidationRequested += ValidatePowerupUse;
        controller.OnPowerupUseRequested += HandlePowerupUseRequested;
    }

    private void UnsubscribePlayer(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= subscribedPlayers.Length) return;

        PlayerPowerupController controller = subscribedPlayers[slotIndex];

        if (controller != null)
        {
            controller.OnPowerupUseValidationRequested -= ValidatePowerupUse;
            controller.OnPowerupUseRequested -= HandlePowerupUseRequested;
        }

        subscribedPlayers[slotIndex] = null;
    }

    private void UnsubscribeRuntimeAndPlayers()
    {
        if (subscribedRuntimeSystem != null)
        {
            subscribedRuntimeSystem.OnPlayerRegistered -=
                HandlePlayerRegistered;
            subscribedRuntimeSystem.OnPlayerUnregistered -=
                HandlePlayerUnregistered;
            subscribedRuntimeSystem.OnMatchPlayingChanged -=
                HandleMatchPlayingChanged;
        }

        for (int i = 0; i < subscribedPlayers.Length; i++)
        {
            UnsubscribePlayer(i);
        }

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
            playerStates[i] = new ColaMentosBoostState
            {
                PlayerIndex = i + 1
            };
        }
    }

    private void RecordFailure(
        PlayerPowerupController controller,
        ColaMentosActivationFailureReason reason)
    {
        lastFailureReason = reason;
        lastFailurePlayerIndex = controller != null
            ? controller.PlayerIndex
            : 0;

        if (!logActivationFailures) return;

        Debug.LogWarning(
            $"[ColaMentosEffectSystem] P{lastFailurePlayerIndex} could not " +
            $"activate Cola Mentos: {reason}. The stored item is retained " +
            "whenever it is still present.",
            this
        );
    }

    private void InvokeBoostStateChanged(
        int playerIndex,
        ColaMentosBoostState state)
    {
        Action<int, ColaMentosBoostState> handlers = OnBoostStateChanged;
        if (handlers == null) return;

        Delegate[] invocationList = handlers.GetInvocationList();

        for (int i = 0; i < invocationList.Length; i++)
        {
            try
            {
                ((Action<int, ColaMentosBoostState>)invocationList[i])
                    .Invoke(playerIndex, state);
            }
            catch (Exception exception)
            {
                LogSubscriberException(
                    nameof(OnBoostStateChanged),
                    exception
                );
            }
        }
    }

    private void InvokeBoostStateEnded(
        int playerIndex,
        ColaMentosBoostState state,
        PowerupEffectEndReason endReason)
    {
        Action<
            int,
            ColaMentosBoostState,
            PowerupEffectEndReason> handlers = OnBoostStateEnded;

        if (handlers == null) return;

        Delegate[] invocationList = handlers.GetInvocationList();

        for (int i = 0; i < invocationList.Length; i++)
        {
            try
            {
                ((Action<
                    int,
                    ColaMentosBoostState,
                    PowerupEffectEndReason>)invocationList[i]).Invoke(
                        playerIndex,
                        state,
                        endReason
                    );
            }
            catch (Exception exception)
            {
                LogSubscriberException(
                    nameof(OnBoostStateEnded),
                    exception
                );
            }
        }
    }

    private void LogSubscriberException(
        string eventName,
        Exception exception)
    {
        Debug.LogError(
            $"[ColaMentosEffectSystem] A subscriber to {eventName} threw an " +
            "exception. Cola gameplay remains authoritative.",
            this
        );
        Debug.LogException(exception, this);
    }

    private uint NextFallbackEffectInstanceId()
    {
        fallbackEffectInstanceId = unchecked(fallbackEffectInstanceId + 1u);
        if (fallbackEffectInstanceId == 0u) fallbackEffectInstanceId = 1u;
        return fallbackEffectInstanceId;
    }

    private static uint NextRevision(uint currentRevision)
    {
        uint nextRevision = unchecked(currentRevision + 1u);
        return nextRevision == 0u ? 1u : nextRevision;
    }
}
