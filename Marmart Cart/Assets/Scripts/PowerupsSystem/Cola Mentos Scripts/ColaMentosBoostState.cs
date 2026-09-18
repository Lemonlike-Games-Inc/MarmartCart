using System;
using UnityEngine;

/// <summary>
/// Authoritative per-player Cola Mentos duration state. Presentation and HUD
/// code may inspect this without owning movement permissions or timing.
/// </summary>
[Serializable]
public struct ColaMentosBoostState
{
    public int PlayerIndex;
    public bool Active;
    public uint Revision;
    public uint EffectInstanceId;
    public uint ActivationVersion;

    public PlayerPowerupController PlayerController;
    public CartControlScript CartControl;

    public bool BlocksOtherPowerupUse;
    public float FixedTargetSpeed;
    public float DurationSeconds;
    public float StartedAtTime;
    public float EndsAtTime;

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
