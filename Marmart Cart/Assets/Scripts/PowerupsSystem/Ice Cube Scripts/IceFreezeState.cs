using System;
using UnityEngine;

/// <summary>
/// Authoritative per-player Ice freeze state. UI and feedback code may inspect
/// this without owning gameplay timing or movement permissions.
/// </summary>
[Serializable]
public struct IceFreezeState
{
    public int TargetPlayerIndex;
    public bool Active;
    public uint Revision;
    public uint EffectInstanceId;

    public uint SourceActivationVersion;
    public int SourcePatternEntryIndex;
    public int SourcePlayerIndex;
    public PlayerPowerupController SourceController;
    public PlayerPowerupController TargetController;
    public CartControlScript TargetCartControl;

    public bool AppliedFromGroundHazard;
    public float DurationSeconds;
    public float StartedAtTime;
    public float EndsAtTime;
    public Vector3 LastImpactPosition;

    public float GetRemainingSeconds(float scaledTime)
    {
        return Active
            ? Mathf.Max(0f, EndsAtTime - scaledTime)
            : 0f;
    }

    public float GetRemainingNormalized(float scaledTime)
    {
        if (!Active || DurationSeconds <= 0.0001f) return 0f;

        return Mathf.Clamp01(
            GetRemainingSeconds(scaledTime) / DurationSeconds
        );
    }
}

/// <summary>
/// One validated request to start or refresh Freeze. Both direct projectile
/// impacts and landed hazards use this exact contract.
/// </summary>
[Serializable]
public struct IceFreezeRequest
{
    public uint ActivationVersion;
    public int SourcePatternEntryIndex;
    public int SourcePlayerIndex;
    public PlayerPowerupController SourceController;

    public int TargetPlayerIndex;
    public PlayerPowerupController TargetController;
    public CartControlScript TargetCartControl;

    public bool FromGroundHazard;
    public Vector3 ImpactPosition;
}
