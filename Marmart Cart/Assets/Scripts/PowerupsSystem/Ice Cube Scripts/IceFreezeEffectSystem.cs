using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-level Ice behavior. It converts direct Ice leading-cart impacts and
/// landed-hazard triggers into one refreshable per-player Freeze state.
///
/// Freeze owns independent movement/input gates. It never borrows Checkout's
/// wheel stop or restores permissions owned by Stall, Checkout, or another
/// system.
/// </summary>
[DefaultExecutionOrder(150)]
[DisallowMultipleComponent]
public class IceFreezeEffectSystem : MonoBehaviour
{
    [Serializable]
    private sealed class FreezeBinding
    {
        public CartControlScript CartControl;
        public Rigidbody CartBody;
        public CartDriftController DriftController;
        public LeadingCartStallController StallController;
        public LeadingCartBehaviour[] WheelBehaviours =
            Array.Empty<LeadingCartBehaviour>();

        public RigidbodyConstraints OriginalConstraints;
        public bool OriginalConstraintsCaptured;
        public bool FreezeConstraintsApplied;
        public bool SuspendedForCheckout;
        public Vector3 LockedPosition;
        public Quaternion LockedRotation;
    }

    [Header("References")]
    [SerializeField] private PowerupLifecycleEventSystem lifecycleEventSystem;
    [SerializeField] private PowerupRuntimeSystem runtimeSystem;
    [SerializeField] private IcePowerupProfile iceProfile;

    [Header("Diagnostics")]
    [SerializeField] private bool logStateChanges = true;

    [Header("Runtime - Read Only")]
    [SerializeField]
    private IceFreezeState[] playerStates =
        new IceFreezeState[PowerupRuntimeSystem.MaxPlayerSlots];

    private readonly FreezeBinding[] bindings =
        new FreezeBinding[PowerupRuntimeSystem.MaxPlayerSlots];

    private PowerupLifecycleEventSystem subscribedLifecycleEventSystem;
    private PowerupRuntimeSystem subscribedRuntimeSystem;
    private bool missingProfileLogged;

    public IcePowerupProfile IceProfile => iceProfile;

    public event Action<int, IceFreezeState> OnFreezeStateChanged;
    public event Action<
        int,
        IceFreezeState,
        PowerupEffectEndReason> OnFreezeStateEnded;

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
        ResolveReferences();
        EnsureSubscriptions();
    }

    private void Update()
    {
        for (int i = 0; i < playerStates.Length; i++)
        {
            IceFreezeState state = playerStates[i];
            if (!state.Active) continue;

            FreezeBinding binding = bindings[i];

            if (binding == null ||
                binding.CartControl == null ||
                binding.CartBody == null ||
                state.TargetController == null)
            {
                EndFreeze(
                    i,
                    PowerupEffectEndReason.TargetUnavailable,
                    true
                );
                continue;
            }

            if (Time.time >= state.EndsAtTime)
            {
                EndFreeze(
                    i,
                    PowerupEffectEndReason.DurationExpired,
                    true
                );
            }
        }
    }

    private void FixedUpdate()
    {
        for (int i = 0; i < playerStates.Length; i++)
        {
            if (!playerStates[i].Active) continue;
            MaintainFrozenBody(bindings[i]);
        }
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
        out IceFreezeState state)
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

        EndFreeze(slotIndex, endReason, true);
    }

    /// <summary>
    /// Shared entry point used by direct projectiles and ground hazards.
    /// Returns true only when Freeze actually starts or refreshes.
    /// </summary>
    public bool TryApplyFreeze(IceFreezeRequest request)
    {
        if (iceProfile == null)
        {
            LogMissingProfileOnce();
            return false;
        }

        missingProfileLogged = false;

        ResolveRequestReferences(ref request);

        int slotIndex = request.TargetPlayerIndex - 1;

        if (slotIndex < 0 || slotIndex >= playerStates.Length ||
            request.TargetController == null ||
            request.TargetCartControl == null)
        {
            return false;
        }

        // Checkout owns the cart path and grants immunity. This is checked
        // again here even though projectile targeting normally filters it.
        if (IsTargetInCheckout(
                request.TargetController,
                request.TargetCartControl))
        {
            return false;
        }

        IceFreezeState previousState = playerStates[slotIndex];
        bool wasActive =
            previousState.Active &&
            previousState.EndsAtTime > Time.time;

        if (previousState.Active && !wasActive)
        {
            EndFreeze(
                slotIndex,
                PowerupEffectEndReason.DurationExpired,
                true
            );
            previousState = playerStates[slotIndex];
        }

        if (wasActive &&
            previousState.TargetCartControl != request.TargetCartControl)
        {
            EndFreeze(
                slotIndex,
                PowerupEffectEndReason.Replaced,
                true
            );
            previousState = playerStates[slotIndex];
            wasActive = false;
        }

        FreezeBinding binding = bindings[slotIndex];

        if (!wasActive ||
            binding == null ||
            binding.CartControl != request.TargetCartControl)
        {
            if (!TryBuildBinding(request.TargetCartControl, out binding))
            {
                return false;
            }

            bindings[slotIndex] = binding;

            if (!ApplyFreezeOwnership(
                    binding,
                    request.TargetController))
            {
                bindings[slotIndex] = null;
                return false;
            }
        }
        else
        {
            // Reassert the independent gates on a duration refresh. This is
            // idempotent and protects against external setup scripts that may
            // have refreshed their own permissions since the first hit.
            ApplyFreezeOwnership(binding, request.TargetController);
        }

        float now = Time.time;
        float duration = iceProfile.FreezeDurationSeconds;
        uint effectInstanceId = wasActive
            ? previousState.EffectInstanceId
            : lifecycleEventSystem != null
                ? lifecycleEventSystem.ReserveEffectInstanceId()
                : NextRevision(previousState.Revision);

        IceFreezeState nextState = new IceFreezeState
        {
            TargetPlayerIndex = request.TargetPlayerIndex,
            Active = true,
            Revision = NextRevision(previousState.Revision),
            EffectInstanceId = effectInstanceId,
            SourceActivationVersion = request.ActivationVersion,
            SourcePatternEntryIndex = request.SourcePatternEntryIndex,
            SourcePlayerIndex = request.SourcePlayerIndex,
            SourceController = request.SourceController,
            TargetController = request.TargetController,
            TargetCartControl = request.TargetCartControl,
            AppliedFromGroundHazard = request.FromGroundHazard,
            DurationSeconds = duration,
            StartedAtTime = now,
            EndsAtTime = now + duration,
            LastImpactPosition = request.ImpactPosition
        };

        playerStates[slotIndex] = nextState;
        InvokeFreezeStateChanged(slotIndex + 1, nextState);

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
                $"[IceFreezeEffectSystem] P{slotIndex + 1} Freeze " +
                $"{(wasActive ? "refreshed" : "started")} until " +
                $"{nextState.EndsAtTime:0.###} " +
                $"({(request.FromGroundHazard ? "ground hazard" : "direct hit")}).",
                this
            );
        }

        return true;
    }

    private void HandleProjectileImpacted(
        PowerupProjectileEvent projectileEvent)
    {
        PowerupProjectileCompletion completion =
            projectileEvent.Completion;

        if (completion.PowerupId != PowerupId.IceCube ||
            completion.Reason !=
                PowerupProjectileCompletionReason.LeadingCartHit ||
            completion.HitCartKind !=
                PowerupCartTargetKind.LeadingCart)
        {
            return;
        }

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
                    completion.HitPlayerIndex,
                    out targetController
                );
            }
        }

        TryApplyFreeze(new IceFreezeRequest
        {
            ActivationVersion = completion.ShotVersion,
            SourcePatternEntryIndex = completion.PatternEntryIndex,
            SourcePlayerIndex = completion.OwnerPlayerIndex,
            SourceController = sourceController,
            TargetPlayerIndex = completion.HitPlayerIndex,
            TargetController = targetController,
            TargetCartControl = completion.HitLeadingCartControl,
            FromGroundHazard = false,
            ImpactPosition = completion.Position
        });
    }

    private bool TryBuildBinding(
        CartControlScript cartControl,
        out FreezeBinding binding)
    {
        binding = null;
        if (cartControl == null) return false;

        LeadingCartBehaviour[] allWheels =
            FindObjectsByType<LeadingCartBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        var matchingWheels = new List<LeadingCartBehaviour>(4);
        Rigidbody cartBody = null;
        CartDriftController driftController = null;

        for (int i = 0; i < allWheels.Length; i++)
        {
            LeadingCartBehaviour wheel = allWheels[i];

            if (wheel == null ||
                wheel.CartControlInput != cartControl)
            {
                continue;
            }

            matchingWheels.Add(wheel);

            if (cartBody == null) cartBody = wheel.CartBody;
            if (driftController == null)
            {
                driftController = wheel.DriftController;
            }
        }

        if (cartBody == null)
        {
            cartBody =
                cartControl.GetComponent<Rigidbody>() ??
                cartControl.GetComponentInParent<Rigidbody>() ??
                cartControl.GetComponentInChildren<Rigidbody>(true);
        }

        if (cartBody == null)
        {
            Debug.LogError(
                "[IceFreezeEffectSystem] Could not resolve the leading " +
                "cart Rigidbody from the hit CartControlScript or its " +
                "LeadingCartBehaviour wheels.",
                cartControl
            );
            return false;
        }

        if (driftController == null)
        {
            driftController =
                cartControl.GetComponent<CartDriftController>() ??
                cartControl.GetComponentInParent<CartDriftController>() ??
                cartBody.GetComponentInChildren<CartDriftController>(true) ??
                cartBody.GetComponentInParent<CartDriftController>();
        }

        LeadingCartStallController stallController =
            cartControl.GetComponent<LeadingCartStallController>() ??
            cartControl.GetComponentInParent<LeadingCartStallController>() ??
            cartBody.GetComponentInChildren<LeadingCartStallController>(true) ??
            cartBody.GetComponentInParent<LeadingCartStallController>();

        binding = new FreezeBinding
        {
            CartControl = cartControl,
            CartBody = cartBody,
            DriftController = driftController,
            StallController = stallController,
            WheelBehaviours = matchingWheels.ToArray(),
            OriginalConstraints = cartBody.constraints,
            OriginalConstraintsCaptured = true,
            LockedPosition = cartBody.position,
            LockedRotation = cartBody.rotation
        };

        if (binding.WheelBehaviours.Length == 0)
        {
            Debug.LogWarning(
                "[IceFreezeEffectSystem] No LeadingCartBehaviour wheel " +
                "matched the frozen CartControlScript. The Rigidbody will " +
                "still be locked, but verify each wheel's Cart Control Input reference.",
                cartControl
            );
        }

        if (stallController == null)
        {
            Debug.LogWarning(
                "[IceFreezeEffectSystem] LeadingCartStallController was not " +
                "resolved. Freeze will work, but Stall suppression cannot be applied.",
                cartControl
            );
        }

        return true;
    }

    private bool ApplyFreezeOwnership(
        FreezeBinding binding,
        PlayerPowerupController targetController)
    {
        if (binding == null ||
            binding.CartControl == null ||
            binding.CartBody == null ||
            targetController == null)
        {
            return false;
        }

        // Clear an existing Stall before the Freeze gate becomes effective so
        // Stall restores only its own saved permissions.
        binding.StallController?.SetStallDetectionSuppressed(true);
        binding.DriftController?.CancelDrift("Ice Freeze");

        targetController.SetUseBlocked(
            PowerupUseBlockReason.Frozen,
            true
        );
        binding.CartControl.SetPowerupFrozen(true);

        for (int i = 0; i < binding.WheelBehaviours.Length; i++)
        {
            binding.WheelBehaviours[i]?.SetPowerupFreezeSuppressed(true);
        }

        if (!IsTargetInCheckout(targetController, binding.CartControl))
        {
            ApplyFrozenBodyConstraints(binding, true);
        }

        return true;
    }

    private void MaintainFrozenBody(FreezeBinding binding)
    {
        if (binding == null ||
            binding.CartControl == null ||
            binding.CartBody == null)
        {
            return;
        }

        bool checkoutOwnsBody = binding.CartControl.GetIsInPit();

        if (checkoutOwnsBody)
        {
            if (binding.FreezeConstraintsApplied)
            {
                RestoreOriginalConstraints(binding);
            }

            binding.SuspendedForCheckout = true;
            return;
        }

        if (binding.SuspendedForCheckout ||
            !binding.FreezeConstraintsApplied)
        {
            ApplyFrozenBodyConstraints(binding, true);
            binding.SuspendedForCheckout = false;
        }

        Rigidbody body = binding.CartBody;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.position = binding.LockedPosition;
        body.rotation = binding.LockedRotation;

        if (body.constraints != RigidbodyConstraints.FreezeAll)
        {
            body.constraints = RigidbodyConstraints.FreezeAll;
        }

        body.Sleep();
    }

    private void ApplyFrozenBodyConstraints(
        FreezeBinding binding,
        bool recapturePose)
    {
        if (binding == null || binding.CartBody == null) return;

        Rigidbody body = binding.CartBody;

        if (!binding.OriginalConstraintsCaptured)
        {
            binding.OriginalConstraints = body.constraints;
            binding.OriginalConstraintsCaptured = true;
        }

        if (recapturePose)
        {
            binding.LockedPosition = body.position;
            binding.LockedRotation = body.rotation;
        }

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.constraints = RigidbodyConstraints.FreezeAll;
        binding.FreezeConstraintsApplied = true;
        body.Sleep();
    }

    private void RestoreOriginalConstraints(FreezeBinding binding)
    {
        if (binding == null || binding.CartBody == null) return;

        if (binding.OriginalConstraintsCaptured)
        {
            binding.CartBody.constraints = binding.OriginalConstraints;
        }

        binding.FreezeConstraintsApplied = false;
        binding.CartBody.WakeUp();
    }

    private void EndFreeze(
        int slotIndex,
        PowerupEffectEndReason endReason,
        bool publishLifecycleEvent)
    {
        if (slotIndex < 0 || slotIndex >= playerStates.Length) return;

        IceFreezeState endingState = playerStates[slotIndex];
        if (!endingState.Active) return;

        FreezeBinding binding = bindings[slotIndex];
        ReleaseFreezeOwnership(binding, endingState.TargetController);
        bindings[slotIndex] = null;

        endingState.Active = false;
        endingState.Revision = NextRevision(endingState.Revision);
        endingState.EndsAtTime = Mathf.Min(
            endingState.EndsAtTime,
            Time.time
        );
        playerStates[slotIndex] = endingState;

        InvokeFreezeStateEnded(
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
                $"[IceFreezeEffectSystem] P{slotIndex + 1} Freeze ended: " +
                $"{endReason}.",
                this
            );
        }
    }

    private static void ReleaseFreezeOwnership(
        FreezeBinding binding,
        PlayerPowerupController targetController)
    {
        if (binding == null)
        {
            targetController?.SetUseBlocked(
                PowerupUseBlockReason.Frozen,
                false
            );
            return;
        }

        if (binding.FreezeConstraintsApplied)
        {
            if (binding.OriginalConstraintsCaptured &&
                binding.CartBody != null)
            {
                binding.CartBody.constraints =
                    binding.OriginalConstraints;
            }

            binding.FreezeConstraintsApplied = false;
        }

        if (binding.CartBody != null &&
            binding.CartControl != null &&
            !binding.CartControl.GetIsInPit())
        {
            binding.CartBody.linearVelocity = Vector3.zero;
            binding.CartBody.angularVelocity = Vector3.zero;
            binding.CartBody.WakeUp();
        }

        for (int i = 0; i < binding.WheelBehaviours.Length; i++)
        {
            binding.WheelBehaviours[i]?.SetPowerupFreezeSuppressed(false);
        }

        binding.CartControl?.SetPowerupFrozen(false);
        targetController?.SetUseBlocked(
            PowerupUseBlockReason.Frozen,
            false
        );

        // Resume Stall detection last, after movement/input ownership is back
        // in its normal state.
        binding.StallController?.SetStallDetectionSuppressed(false);
    }

    private PowerupEffectEvent BuildEffectEvent(IceFreezeState state)
    {
        return new PowerupEffectEvent
        {
            EffectId = PowerupEffectId.IceFrozen,
            SourcePowerupId = PowerupId.IceCube,
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
            StackCount = 1
        };
    }

    private void ResolveRequestReferences(ref IceFreezeRequest request)
    {
        if (runtimeSystem == null) ResolveReferences();

        if (runtimeSystem != null)
        {
            if (request.SourceController == null &&
                IsValidPlayerIndex(request.SourcePlayerIndex))
            {
                if (runtimeSystem.TryGetPlayer(
                    request.SourcePlayerIndex,
                    out PlayerPowerupController sourceController))
                {
                    request.SourceController = sourceController;
                }
            }

            if (request.TargetController == null &&
                IsValidPlayerIndex(request.TargetPlayerIndex))
            {
                if (runtimeSystem.TryGetPlayer(
                    request.TargetPlayerIndex,
                    out PlayerPowerupController targetController))
                {
                    request.TargetController = targetController;
                }
            }
        }

        if (request.TargetController != null)
        {
            request.TargetPlayerIndex = request.TargetController.PlayerIndex;

            if (request.TargetCartControl == null)
            {
                request.TargetCartControl =
                    request.TargetController.CartControlInput;
            }
        }
    }

    private static bool IsTargetInCheckout(
        PlayerPowerupController targetController,
        CartControlScript cartControl)
    {
        return (cartControl != null && cartControl.GetIsInPit()) ||
               (targetController != null &&
                targetController.IsUseBlocked(
                    PowerupUseBlockReason.Checkout
                ));
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

        EndFreeze(
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
            EndFreeze(i, endReason, publishLifecycleEvents);
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
            playerStates[i] = new IceFreezeState
            {
                TargetPlayerIndex = i + 1
            };
            bindings[i] = null;
        }
    }

    private void InvokeFreezeStateChanged(
        int playerIndex,
        IceFreezeState state)
    {
        Action<int, IceFreezeState> handlers = OnFreezeStateChanged;
        if (handlers == null) return;

        Delegate[] invocationList = handlers.GetInvocationList();

        for (int i = 0; i < invocationList.Length; i++)
        {
            try
            {
                ((Action<int, IceFreezeState>)invocationList[i]).Invoke(
                    playerIndex,
                    state
                );
            }
            catch (Exception exception)
            {
                LogSubscriberException(
                    nameof(OnFreezeStateChanged),
                    exception
                );
            }
        }
    }

    private void InvokeFreezeStateEnded(
        int playerIndex,
        IceFreezeState state,
        PowerupEffectEndReason endReason)
    {
        Action<int, IceFreezeState, PowerupEffectEndReason> handlers =
            OnFreezeStateEnded;

        if (handlers == null) return;

        Delegate[] invocationList = handlers.GetInvocationList();

        for (int i = 0; i < invocationList.Length; i++)
        {
            try
            {
                ((Action<
                    int,
                    IceFreezeState,
                    PowerupEffectEndReason>)invocationList[i]).Invoke(
                        playerIndex,
                        state,
                        endReason
                    );
            }
            catch (Exception exception)
            {
                LogSubscriberException(
                    nameof(OnFreezeStateEnded),
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
            $"[IceFreezeEffectSystem] A subscriber to {eventName} threw " +
            "an exception. Ice gameplay state remains authoritative.",
            this
        );
        Debug.LogException(exception, this);
    }

    private void LogMissingProfileOnce()
    {
        if (missingProfileLogged) return;

        missingProfileLogged = true;
        Debug.LogError(
            "[IceFreezeEffectSystem] Assign an IcePowerupProfile. " +
            "Ice impacts cannot create Freeze without effect tuning.",
            this
        );
    }

    private static bool IsValidPlayerIndex(int playerIndex)
    {
        return playerIndex >= 1 &&
               playerIndex <= PowerupRuntimeSystem.MaxPlayerSlots;
    }

    private static uint NextRevision(uint currentRevision)
    {
        uint nextRevision = unchecked(currentRevision + 1u);
        return nextRevision == 0u ? 1u : nextRevision;
    }
}
