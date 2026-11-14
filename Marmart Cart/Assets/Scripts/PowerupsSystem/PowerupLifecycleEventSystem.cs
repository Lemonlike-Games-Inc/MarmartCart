using System;
using UnityEngine;

/// <summary>
/// Scene-level semantic event hub for gameplay, UI, VFX, and SFX adapters.
///
/// It does not play feedback and does not own power-up behavior. It only
/// publishes precise, typed moments:
/// - one activation per accepted use;
/// - one impact or cancellation per projectile;
/// - start/refresh/end for effects that actually exist over time.
///
/// There is deliberately no universal "destroy" event. Returning a projectile
/// to its pool is an implementation detail, not a shared gameplay lifecycle.
/// </summary>
[DefaultExecutionOrder(-350)]
[DisallowMultipleComponent]
public class PowerupLifecycleEventSystem : MonoBehaviour
{
    [Header("Diagnostics")]
    [SerializeField] private bool logPublishedEvents;

    [Header("Runtime - Read Only")]
    [SerializeField] private uint activationEventCount;
    [SerializeField] private uint projectileImpactEventCount;
    [SerializeField] private uint projectileCancellationEventCount;
    [SerializeField] private uint effectEventCount;
    [SerializeField] private uint lastReservedEffectInstanceId;
    [SerializeField] private PowerupActivationEvent lastActivation;
    [SerializeField] private PowerupProjectileEvent lastProjectileEvent;
    [SerializeField] private PowerupEffectEvent lastEffectEvent;

    public uint ActivationEventCount => activationEventCount;
    public uint ProjectileImpactEventCount => projectileImpactEventCount;
    public uint ProjectileCancellationEventCount =>
        projectileCancellationEventCount;
    public uint EffectEventCount => effectEventCount;

    public event Action<PowerupActivationEvent> OnActivated;
    public event Action<PowerupProjectileEvent> OnProjectileImpacted;
    public event Action<PowerupProjectileEvent> OnProjectileCancelled;
    public event Action<PowerupEffectEvent> OnEffectStarted;
    public event Action<PowerupEffectEvent> OnEffectRefreshed;
    public event Action<PowerupEffectEvent> OnEffectEnded;
    public event Action<PowerupEffectEvent> OnAnyEffectEvent;

    /// <summary>
    /// Reserves a stable non-zero identity shared by one effect's start,
    /// refresh, and end events. Revision numbers may change; this identity does
    /// not. Future systems can also reserve multiple concurrent world effects.
    /// </summary>
    public uint ReserveEffectInstanceId()
    {
        lastReservedEffectInstanceId =
            unchecked(lastReservedEffectInstanceId + 1u);

        if (lastReservedEffectInstanceId == 0u)
        {
            lastReservedEffectInstanceId = 1u;
        }

        return lastReservedEffectInstanceId;
    }

    public void PublishActivated(PowerupActivationEvent activationEvent)
    {
        activationEvent.OccurredAtTime = Time.time;
        activationEvent.OccurredAtUnscaledTime = Time.unscaledTime;
        activationEventCount++;
        lastActivation = activationEvent;

        if (logPublishedEvents)
        {
            Debug.Log(
                $"[PowerupLifecycleEventSystem] P" +
                $"{activationEvent.OwnerPlayerIndex} activated " +
                $"{activationEvent.PowerupId} " +
                $"#{activationEvent.ActivationVersion}.",
                this
            );
        }

        InvokeSafely(OnActivated, activationEvent, nameof(OnActivated));
    }

    /// <summary>
    /// Classifies the neutral projectile completion into either an impact or a
    /// cancellation. Call this before returning/resetting the pooled actor.
    /// </summary>
    public void PublishProjectileCompletion(
        PowerupProjectileCompletion completion)
    {
        PowerupProjectileEvent projectileEvent =
            new PowerupProjectileEvent
            {
                Completion = completion,
                OccurredAtTime = Time.time,
                OccurredAtUnscaledTime = Time.unscaledTime
            };

        lastProjectileEvent = projectileEvent;

        if (completion.Reason ==
            PowerupProjectileCompletionReason.Cancelled)
        {
            projectileCancellationEventCount++;

            if (logPublishedEvents)
            {
                Debug.Log(
                    $"[PowerupLifecycleEventSystem] " +
                    $"{completion.PowerupId}[{completion.PatternEntryIndex}] " +
                    $"#{completion.ShotVersion} cancelled.",
                    this
                );
            }

            InvokeSafely(
                OnProjectileCancelled,
                projectileEvent,
                nameof(OnProjectileCancelled)
            );
            return;
        }

        projectileImpactEventCount++;

        if (logPublishedEvents)
        {
            Debug.Log(
                $"[PowerupLifecycleEventSystem] " +
                $"{completion.PowerupId}[{completion.PatternEntryIndex}] " +
                $"#{completion.ShotVersion} impacted: {completion.Reason}.",
                this
            );
        }

        InvokeSafely(
            OnProjectileImpacted,
            projectileEvent,
            nameof(OnProjectileImpacted)
        );
    }

    public void PublishEffectStarted(PowerupEffectEvent effectEvent)
    {
        PublishEffect(
            effectEvent,
            PowerupEffectPhase.Started,
            PowerupEffectEndReason.None
        );
    }

    public void PublishEffectRefreshed(PowerupEffectEvent effectEvent)
    {
        PublishEffect(
            effectEvent,
            PowerupEffectPhase.Refreshed,
            PowerupEffectEndReason.None
        );
    }

    public void PublishEffectEnded(
        PowerupEffectEvent effectEvent,
        PowerupEffectEndReason endReason)
    {
        PublishEffect(
            effectEvent,
            PowerupEffectPhase.Ended,
            endReason
        );
    }

    private void PublishEffect(
        PowerupEffectEvent effectEvent,
        PowerupEffectPhase phase,
        PowerupEffectEndReason endReason)
    {
        effectEvent.Phase = phase;
        effectEvent.EndReason = endReason;
        effectEvent.OccurredAtTime = Time.time;
        effectEvent.OccurredAtUnscaledTime = Time.unscaledTime;
        effectEventCount++;
        lastEffectEvent = effectEvent;

        if (logPublishedEvents)
        {
            Debug.Log(
                $"[PowerupLifecycleEventSystem] " +
                $"{effectEvent.EffectId} {phase} for P" +
                $"{effectEvent.TargetPlayerIndex} " +
                $"(version {effectEvent.EffectVersion}).",
                this
            );
        }

        switch (phase)
        {
            case PowerupEffectPhase.Started:
                InvokeSafely(
                    OnEffectStarted,
                    effectEvent,
                    nameof(OnEffectStarted)
                );
                break;

            case PowerupEffectPhase.Refreshed:
                InvokeSafely(
                    OnEffectRefreshed,
                    effectEvent,
                    nameof(OnEffectRefreshed)
                );
                break;

            case PowerupEffectPhase.Ended:
                InvokeSafely(
                    OnEffectEnded,
                    effectEvent,
                    nameof(OnEffectEnded)
                );
                break;
        }

        InvokeSafely(
            OnAnyEffectEvent,
            effectEvent,
            nameof(OnAnyEffectEvent)
        );
    }

    private void InvokeSafely<TEvent>(
        Action<TEvent> handlers,
        TEvent eventData,
        string eventName)
    {
        if (handlers == null) return;

        Delegate[] invocationList = handlers.GetInvocationList();

        for (int i = 0; i < invocationList.Length; i++)
        {
            try
            {
                ((Action<TEvent>)invocationList[i]).Invoke(eventData);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[PowerupLifecycleEventSystem] A subscriber to " +
                    $"{eventName} threw an exception. Other lifecycle " +
                    $"subscribers will still receive the event.",
                    this
                );
                Debug.LogException(exception, this);
            }
        }
    }
}
