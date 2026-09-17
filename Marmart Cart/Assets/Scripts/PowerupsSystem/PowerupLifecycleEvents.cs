using System;
using UnityEngine;

/// <summary>
/// Describes how a stored power-up begins. This is intentionally independent
/// from how long its later gameplay effect lives.
/// </summary>
public enum PowerupActivationMode
{
    ProjectileVolley,
    Instant,
    Duration
}

/// <summary>
/// Stable semantic identities for effects produced by the fixed four-power-up
/// roster. One power-up may eventually produce more than one effect identity.
/// </summary>
public enum PowerupEffectId
{
    TomatoBlind,
    IceFrozen,
    IceGroundHazard,
    ColaMentosBoost,
    StrongFanWind
}

public enum PowerupEffectPhase
{
    Started,
    Refreshed,
    Ended
}

public enum PowerupEffectEndReason
{
    None,
    DurationExpired,
    MatchEnded,
    TargetUnavailable,
    Replaced,
    Cancelled,
    SystemDisabled
}

/// <summary>
/// Published once when a validated power-up use has successfully created its
/// runtime work and consumed the stored item. Projectile volleys publish once,
/// not once per projectile.
/// </summary>
[Serializable]
public struct PowerupActivationEvent
{
    public PowerupId PowerupId;
    public PowerupActivationMode Mode;
    public uint ActivationVersion;
    public int OwnerPlayerIndex;
    public PlayerPowerupController OwnerController;
    public Vector3 Position;
    public Vector3 Direction;
    public int ProjectileCount;
    public float OccurredAtTime;
    public float OccurredAtUnscaledTime;
}

/// <summary>
/// A physical projectile resolution with exact publication timestamps. A
/// Cancelled completion is published through the separate cancellation event,
/// never through the impact event.
/// </summary>
[Serializable]
public struct PowerupProjectileEvent
{
    public PowerupProjectileCompletion Completion;
    public float OccurredAtTime;
    public float OccurredAtUnscaledTime;
}

/// <summary>
/// Target/world gameplay-effect lifecycle. This is deliberately separate from
/// projectile pooling: an impact can apply no effect, and a duration effect can
/// outlive the projectile that created it.
/// </summary>
[Serializable]
public struct PowerupEffectEvent
{
    public PowerupEffectId EffectId;
    public PowerupId SourcePowerupId;
    public PowerupEffectPhase Phase;
    public PowerupEffectEndReason EndReason;

    public uint ActivationVersion;
    public uint EffectInstanceId;
    public uint EffectVersion;
    public int SourcePatternEntryIndex;

    public int OwnerPlayerIndex;
    public int TargetPlayerIndex;
    public PlayerPowerupController OwnerController;
    public PlayerPowerupController TargetController;

    public Vector3 Position;
    public float DurationSeconds;
    public float StartedAtTime;
    public float EndsAtTime;
    public float OccurredAtTime;
    public float OccurredAtUnscaledTime;
    public int StackCount;
}
