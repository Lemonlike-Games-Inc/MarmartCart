using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-level owner of landed Ice hazards. It listens for neutral Ice landing
/// completions, rents a persistent Rigidbody-free hazard, and publishes the
/// IceGroundHazard start/end lifecycle used by future VFX and SFX.
/// </summary>
[DisallowMultipleComponent]
public class IceGroundHazardPool : MonoBehaviour
{
    [Header("References")]
    [Tooltip(
        "Pool settings and the cube prefab. Each cube's lifetime and placement " +
        "come from the Ice Cube effect of the item that landed."
    )]
    [SerializeField] private IceCubeEffect iceProfile;
    [SerializeField] private IcePresentationProfile icePresentationProfile;
    [SerializeField] private IceFreezeEffectSystem freezeEffectSystem;
    [SerializeField] private PowerupProjectilePool projectilePool;
    [SerializeField] private PowerupLifecycleEventSystem lifecycleEventSystem;
    [SerializeField] private PowerupRuntimeSystem runtimeSystem;

    [Tooltip("Optional hierarchy parent for pooled hazards. Defaults to this object.")]
    [SerializeField] private Transform poolRoot;

    [Header("Diagnostics")]
    [SerializeField] private bool logHazardLifecycle = true;

    [Header("Runtime - Read Only")]
    [SerializeField] private int totalCreatedHazardCount;
    [SerializeField] private int activeHazardCount;
    [SerializeField] private int fadingHazardCount;

    private readonly Stack<IceGroundHazard> available =
        new Stack<IceGroundHazard>();
    private readonly HashSet<IceGroundHazard> active =
        new HashSet<IceGroundHazard>();
    private readonly HashSet<IceGroundHazard> fading =
        new HashSet<IceGroundHazard>();
    private readonly List<IceGroundHazard> activeScratch =
        new List<IceGroundHazard>();

    private PowerupLifecycleEventSystem subscribedLifecycleEventSystem;
    private PowerupRuntimeSystem subscribedRuntimeSystem;
    private bool invalidSetupLogged;

    public int TotalCreatedHazardCount => totalCreatedHazardCount;
    public int ActiveHazardCount => activeHazardCount;
    public int FadingHazardCount => fadingHazardCount;

    public event Action<IceGroundHazard> OnHazardStarted;
    public event Action<
        IceGroundHazard,
        PowerupEffectEndReason> OnHazardEnded;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        if (poolRoot == null) poolRoot = transform;
        Prewarm();
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
        Prewarm();
    }

    private void OnDisable()
    {
        ClearAllActiveHazards(
            PowerupEffectEndReason.SystemDisabled,
            false
        );
        ForceReturnAllFadingHazards();
        Unsubscribe();
    }

    internal void ReturnExpired(IceGroundHazard hazard)
    {
        ReturnHazard(
            hazard,
            PowerupEffectEndReason.DurationExpired,
            default,
            null,
            hazard != null ? hazard.transform.position : Vector3.zero
        );
    }

    internal void ReturnTriggered(
        IceGroundHazard hazard,
        PowerupCartTargetSnapshot target,
        Collider hitCollider,
        Vector3 triggerPosition)
    {
        ReturnHazard(
            hazard,
            PowerupEffectEndReason.Triggered,
            target,
            hitCollider,
            triggerPosition
        );
    }

    private void HandleProjectileImpacted(
        PowerupProjectileEvent projectileEvent)
    {
        PowerupProjectileCompletion completion =
            projectileEvent.Completion;

        if (completion.Item == null ||
            !completion.Item.TryGetEffect(out IceCubeEffect iceEffect) ||
            completion.Reason !=
                PowerupProjectileCompletionReason.Landed)
        {
            return;
        }

        TrySpawnHazard(completion, iceEffect);
    }

    private bool TrySpawnHazard(
        PowerupProjectileCompletion completion,
        IceCubeEffect iceEffect)
    {
        ResolveReferences();

        if (!TryGetRuntimeSetup(
                out PowerupProjectileProfile projectileProfile,
                out IceGroundHazard prefab))
        {
            LogInvalidSetupOnce();
            return false;
        }

        // The cube freezes leading carts when the thrown item's pattern does.
        bool affectsLeadingCarts =
            !completion.Item.TryGetUse(out ThrowItemUse throwUse) ||
            throwUse.Pattern == null ||
            throwUse.Pattern.HasEffectOnLeadingCart;

        invalidSetupLogged = false;

        if (!TryRent(prefab, out IceGroundHazard hazard))
        {
            Debug.LogWarning(
                "[IceGroundHazardPool] The Ice ground-hazard pool is " +
                "exhausted. Increase Maximum Ground Hazard Instances or " +
                "enable growth in IceCubeEffect.",
                this
            );
            return false;
        }

        PlayerItemHand ownerController = null;

        if (runtimeSystem != null)
        {
            runtimeSystem.TryGetPlayer(
                completion.OwnerPlayerIndex,
                out ownerController
            );
        }

        Vector3 surfaceNormal = completion.SurfaceNormal.sqrMagnitude > 0.0001f
            ? completion.SurfaceNormal.normalized
            : Vector3.up;

        Quaternion surfaceRotation = iceEffect.AlignHazardToSurfaceNormal
            ? Quaternion.FromToRotation(Vector3.up, surfaceNormal)
            : Quaternion.identity;

        Quaternion finalRotation =
            surfaceRotation *
            Quaternion.Euler(iceEffect.GroundHazardEulerOffset);

        Vector3 finalPosition =
            completion.Position +
            surfaceNormal * iceEffect.GroundHazardSurfaceOffset;

        uint effectInstanceId = lifecycleEventSystem != null
            ? lifecycleEventSystem.ReserveEffectInstanceId()
            : NextFallbackEffectInstanceId();

        float lifetimeSeconds = iceEffect.StaysUntilTriggered
            ? float.PositiveInfinity
            : iceEffect.GroundHazardLifetimeSeconds;

        bool activated = hazard.Activate(
            freezeEffectSystem,
            projectileProfile.CartTargetMask,
            affectsLeadingCarts,
            completion.ShotVersion,
            effectInstanceId,
            completion.PatternEntryIndex,
            completion.OwnerPlayerIndex,
            ownerController,
            completion.Item,
            iceEffect,
            finalPosition,
            finalRotation,
            iceEffect.GroundHazardScaleMultiplier,
            lifetimeSeconds,
            GetVisualStartAlphaByte()
        );

        if (!activated)
        {
            ReturnUnlaunched(hazard);
            LogInvalidSetupOnce();
            return false;
        }

        MeltOldestOverCap(completion.OwnerPlayerIndex, iceEffect);
        active.Add(hazard);
        activeHazardCount = active.Count;

        lifecycleEventSystem?.PublishEffectStarted(
            BuildHazardEffectEvent(
                hazard,
                0,
                null,
                finalPosition
            )
        );

        InvokeHazardStarted(hazard);

        if (logHazardLifecycle)
        {
            string untilText = iceEffect.StaysUntilTriggered
                ? "until triggered"
                : $"until {hazard.EndsAtTime:0.###}";

            Debug.Log(
                $"[IceGroundHazardPool] Ice hazard " +
                $"#{hazard.EffectInstanceId} started at " +
                $"{finalPosition} {untilText}.",
                this
            );
        }

        return true;
    }

    // Called before the new cube joins the active set: melts the owner's oldest
    // cubes of this effect until there is room for one more.
    private void MeltOldestOverCap(int ownerPlayerIndex, IceCubeEffect effect)
    {
        int cap = effect.MaximumCubesPerPlayer;
        if (cap <= 0) return;

        while (true)
        {
            int count = 0;
            IceGroundHazard oldest = null;

            foreach (IceGroundHazard hazard in active)
            {
                if (hazard == null ||
                    hazard.OwnerPlayerIndex != ownerPlayerIndex ||
                    hazard.Effect != effect)
                {
                    continue;
                }

                count++;

                if (oldest == null || hazard.StartedAtTime < oldest.StartedAtTime)
                {
                    oldest = hazard;
                }
            }

            if (count < cap || oldest == null) return;

            ReturnHazard(
                oldest,
                PowerupEffectEndReason.Replaced,
                default,
                null,
                oldest.transform.position
            );
        }
    }

    private bool TryGetRuntimeSetup(
        out PowerupProjectileProfile projectileProfile,
        out IceGroundHazard prefab)
    {
        projectileProfile = projectilePool != null
            ? projectilePool.ProjectileProfile
            : null;
        prefab = iceProfile != null
            ? iceProfile.GetGroundHazardPrefab()
            : null;

        return iceProfile != null &&
               freezeEffectSystem != null &&
               projectileProfile != null &&
               projectileProfile.HasCartTargetMask &&
               prefab != null &&
               prefab.HasValidAuthoredHitbox;
    }

    private void Prewarm()
    {
        if (!TryGetRuntimeSetup(
                out _,
                out IceGroundHazard prefab))
        {
            return;
        }

        int desiredCount = iceProfile.PrewarmGroundHazardCount;
        int maximumCount = iceProfile.MaximumGroundHazardInstances;

        if (maximumCount > 0)
        {
            desiredCount = Mathf.Min(desiredCount, maximumCount);
        }

        while (totalCreatedHazardCount < desiredCount)
        {
            IceGroundHazard created = CreateHazard(prefab);
            if (created == null) break;
            available.Push(created);
        }
    }

    private bool TryRent(
        IceGroundHazard prefab,
        out IceGroundHazard hazard)
    {
        hazard = null;

        while (available.Count > 0 && hazard == null)
        {
            hazard = available.Pop();
        }

        if (hazard != null) return true;

        if (iceProfile == null ||
            !iceProfile.AllowGroundHazardPoolGrowth)
        {
            return false;
        }

        int maximumCount = iceProfile.MaximumGroundHazardInstances;

        if (maximumCount > 0 &&
            totalCreatedHazardCount >= maximumCount)
        {
            return false;
        }

        hazard = CreateHazard(prefab);
        return hazard != null;
    }

    private IceGroundHazard CreateHazard(IceGroundHazard prefab)
    {
        if (prefab == null) return null;

        Transform parent = poolRoot != null ? poolRoot : transform;
        IceGroundHazard actor = Instantiate(prefab, parent);

        if (actor == null) return null;

        actor.name = $"{prefab.name} (Pooled)";
        actor.InitializeForPool(this);
        totalCreatedHazardCount++;
        return actor;
    }

    private void ReturnUnlaunched(IceGroundHazard hazard)
    {
        if (hazard == null) return;

        hazard.ResetForPool();
        available.Push(hazard);
    }

    private void ReturnHazard(
        IceGroundHazard hazard,
        PowerupEffectEndReason endReason,
        PowerupCartTargetSnapshot target,
        Collider hitCollider,
        Vector3 endPosition,
        bool allowPresentationFade = true)
    {
        if (hazard == null ||
            !hazard.IsActiveHazard ||
            !active.Remove(hazard))
        {
            return;
        }

        hazard.MarkGameplayEndingNow();
        hazard.IncrementEffectVersion();
        activeHazardCount = active.Count;

        lifecycleEventSystem?.PublishEffectEnded(
            BuildHazardEffectEvent(
                hazard,
                target.PlayerIndex,
                target.OwnerController,
                endPosition
            ),
            endReason
        );

        InvokeHazardEnded(hazard, endReason);

        if (logHazardLifecycle)
        {
            string targetText = target.PlayerIndex > 0
                ? $" after triggering on P{target.PlayerIndex}"
                : string.Empty;

            Debug.Log(
                $"[IceGroundHazardPool] Ice hazard " +
                $"#{hazard.EffectInstanceId} ended: {endReason}" +
                $"{targetText}.",
                this
            );
        }

        float fadeDuration = allowPresentationFade
            ? GetVisualFadeDuration()
            : 0f;

        if (fadeDuration <= 0.0001f)
        {
            CompleteHazardReturn(hazard);
            return;
        }

        fading.Add(hazard);
        fadingHazardCount = fading.Count;
        hazard.BeginPresentationFade(
            fadeDuration,
            CompleteHazardFade
        );
    }

    private void CompleteHazardFade(IceGroundHazard hazard)
    {
        if (hazard == null || !fading.Remove(hazard)) return;

        fadingHazardCount = fading.Count;
        CompleteHazardReturn(hazard);
    }

    private void CompleteHazardReturn(IceGroundHazard hazard)
    {
        if (hazard == null) return;

        hazard.ResetForPool();
        available.Push(hazard);
    }

    private float GetVisualFadeDuration()
    {
        return icePresentationProfile != null
            ? icePresentationProfile.VisualFadeOutSeconds
            : 1f;
    }

    private int GetVisualStartAlphaByte()
    {
        return icePresentationProfile != null
            ? icePresentationProfile.VisualStartAlphaByte
            : 175;
    }

    private PowerupEffectEvent BuildHazardEffectEvent(
        IceGroundHazard hazard,
        int targetPlayerIndex,
        PlayerItemHand targetController,
        Vector3 position)
    {
        return new PowerupEffectEvent
        {
            EffectId = PowerupEffectId.IceGroundHazard,
            SourceItem = hazard.SourceItem,
            ActivationVersion = hazard.ActivationVersion,
            EffectInstanceId = hazard.EffectInstanceId,
            EffectVersion = hazard.EffectVersion,
            SourcePatternEntryIndex = hazard.SourcePatternEntryIndex,
            OwnerPlayerIndex = hazard.OwnerPlayerIndex,
            TargetPlayerIndex = targetPlayerIndex,
            OwnerController = hazard.OwnerController,
            TargetController = targetController,
            Position = position,
            DurationSeconds = hazard.DurationSeconds,
            StartedAtTime = hazard.StartedAtTime,
            EndsAtTime = hazard.EndsAtTime,
            StackCount = 1
        };
    }

    private void HandleMatchPlayingChanged(bool matchPlaying)
    {
        if (matchPlaying) return;
        ClearAllActiveHazards(
            PowerupEffectEndReason.MatchEnded,
            true
        );
    }

    private void ClearAllActiveHazards(
        PowerupEffectEndReason endReason,
        bool allowPresentationFade)
    {
        activeScratch.Clear();

        foreach (IceGroundHazard hazard in active)
        {
            if (hazard != null) activeScratch.Add(hazard);
        }

        for (int i = 0; i < activeScratch.Count; i++)
        {
            IceGroundHazard hazard = activeScratch[i];

            ReturnHazard(
                hazard,
                endReason,
                default,
                null,
                hazard.transform.position,
                allowPresentationFade
            );
        }

        activeScratch.Clear();
    }

    private void ForceReturnAllFadingHazards()
    {
        activeScratch.Clear();

        foreach (IceGroundHazard hazard in fading)
        {
            if (hazard != null) activeScratch.Add(hazard);
        }

        fading.Clear();
        fadingHazardCount = 0;

        for (int i = 0; i < activeScratch.Count; i++)
        {
            CompleteHazardReturn(activeScratch[i]);
        }

        activeScratch.Clear();
    }

    private void ResolveReferences()
    {
        if (freezeEffectSystem == null)
        {
            freezeEffectSystem =
                FindFirstObjectByType<IceFreezeEffectSystem>();
        }

        if (projectilePool == null)
        {
            projectilePool = FindFirstObjectByType<PowerupProjectilePool>();
        }

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
            }

            subscribedRuntimeSystem = runtimeSystem;

            if (subscribedRuntimeSystem != null)
            {
                subscribedRuntimeSystem.OnMatchPlayingChanged +=
                    HandleMatchPlayingChanged;
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
        }

        subscribedLifecycleEventSystem = null;
        subscribedRuntimeSystem = null;
    }

    private void InvokeHazardStarted(IceGroundHazard hazard)
    {
        Action<IceGroundHazard> handlers = OnHazardStarted;
        if (handlers == null) return;

        Delegate[] invocationList = handlers.GetInvocationList();

        for (int i = 0; i < invocationList.Length; i++)
        {
            try
            {
                ((Action<IceGroundHazard>)invocationList[i]).Invoke(hazard);
            }
            catch (Exception exception)
            {
                LogSubscriberException(nameof(OnHazardStarted), exception);
            }
        }
    }

    private void InvokeHazardEnded(
        IceGroundHazard hazard,
        PowerupEffectEndReason endReason)
    {
        Action<IceGroundHazard, PowerupEffectEndReason> handlers =
            OnHazardEnded;

        if (handlers == null) return;

        Delegate[] invocationList = handlers.GetInvocationList();

        for (int i = 0; i < invocationList.Length; i++)
        {
            try
            {
                ((Action<
                    IceGroundHazard,
                    PowerupEffectEndReason>)invocationList[i]).Invoke(
                        hazard,
                        endReason
                    );
            }
            catch (Exception exception)
            {
                LogSubscriberException(nameof(OnHazardEnded), exception);
            }
        }
    }

    private void LogSubscriberException(
        string eventName,
        Exception exception)
    {
        Debug.LogError(
            $"[IceGroundHazardPool] A subscriber to {eventName} threw an " +
            "exception. Hazard pooling remains authoritative.",
            this
        );
        Debug.LogException(exception, this);
    }

    private void LogInvalidSetupOnce()
    {
        if (invalidSetupLogged) return;

        invalidSetupLogged = true;
        Debug.LogError(
            "[IceGroundHazardPool] Ice hazard setup is incomplete. Assign " +
            "IceCubeEffect, IceFreezeEffectSystem, PowerupProjectilePool, " +
            "a Cart target mask, and a hazard prefab with IceGroundHazard plus " +
            "a BoxCollider.",
            this
        );
    }

    private uint fallbackEffectInstanceId;

    private uint NextFallbackEffectInstanceId()
    {
        fallbackEffectInstanceId = unchecked(fallbackEffectInstanceId + 1u);
        if (fallbackEffectInstanceId == 0u) fallbackEffectInstanceId = 1u;
        return fallbackEffectInstanceId;
    }
}
