using System;
using UnityEngine;

/// <summary>
/// Exact per-player Tomato state consumed later by the viewport Canvas.
/// Presentation can listen for changes and derive its fade from the timestamps
/// without owning gameplay timing.
/// </summary>
[Serializable]
public struct TomatoBlindState
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

    public int SplashCount;
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
