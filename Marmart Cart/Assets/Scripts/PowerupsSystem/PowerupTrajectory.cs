using System;
using UnityEngine;

[Serializable]
public struct PowerupTrajectoryTiming
{
    [SerializeField] private bool useAsymmetricTiming;

    [Range(0.05f, 0.95f)]
    [SerializeField] private float apexNormalizedTime;

    [Min(1.01f)]
    [SerializeField] private float ascentEaseOutPower;

    [Min(1.01f)]
    [SerializeField] private float descentEaseInPower;

    public bool UseAsymmetricTiming => useAsymmetricTiming;
    public float ApexNormalizedTime => apexNormalizedTime;
    public float AscentEaseOutPower => ascentEaseOutPower;
    public float DescentEaseInPower => descentEaseInPower;

    public PowerupTrajectoryTiming(
        bool useAsymmetric,
        float apexTime,
        float ascentPower,
        float descentPower)
    {
        useAsymmetricTiming = useAsymmetric;
        apexNormalizedTime = Mathf.Clamp(apexTime, 0.05f, 0.95f);
        ascentEaseOutPower = Mathf.Max(1.01f, ascentPower);
        descentEaseInPower = Mathf.Max(1.01f, descentPower);
    }

    public static PowerupTrajectoryTiming Linear =>
        new PowerupTrajectoryTiming(false, 0.5f, 2f, 2f);

    public static PowerupTrajectoryTiming RecommendedAsymmetric =>
        new PowerupTrajectoryTiming(true, 0.58f, 1.6f, 2f);

    /// <summary>
    /// Converts evenly advancing flight time into progress along the unchanged
    /// geometric arc. The supplied path progress identifies its true apex.
    /// </summary>
    public float RemapTime(float normalizedTime)
    {
        return RemapTime(normalizedTime, 0.5f);
    }

    public float RemapTime(
        float normalizedTime,
        float apexPathProgress)
    {
        float time = Mathf.Clamp01(normalizedTime);
        if (!useAsymmetricTiming) return time;

        float apexTime = Mathf.Clamp(apexNormalizedTime, 0.05f, 0.95f);
        float apexProgress = Mathf.Clamp(apexPathProgress, 0.05f, 0.95f);
        float ascentPower = Mathf.Max(1.01f, ascentEaseOutPower);
        float descentPower = Mathf.Max(1.01f, descentEaseInPower);

        if (time <= apexTime)
        {
            float ascentTime = Mathf.Clamp01(time / apexTime);
            float easedAscent =
                1f - Mathf.Pow(1f - ascentTime, ascentPower);
            return apexProgress * easedAscent;
        }

        float descentTime = Mathf.Clamp01(
            (time - apexTime) / (1f - apexTime)
        );

        float easedDescent = Mathf.Pow(descentTime, descentPower);
        return apexProgress + (1f - apexProgress) * easedDescent;
    }

    /// <summary>
    /// Converts geometric path progress back into normalized flight time. This
    /// keeps blocker contacts and truncated previews synchronized with playback.
    /// </summary>
    public float InverseRemapTime(float pathProgress)
    {
        return InverseRemapTime(pathProgress, 0.5f);
    }

    public float InverseRemapTime(
        float pathProgress,
        float apexPathProgress)
    {
        float progress = Mathf.Clamp01(pathProgress);
        if (!useAsymmetricTiming) return progress;

        float apexTime = Mathf.Clamp(apexNormalizedTime, 0.05f, 0.95f);
        float apexProgress = Mathf.Clamp(apexPathProgress, 0.05f, 0.95f);
        float ascentPower = Mathf.Max(1.01f, ascentEaseOutPower);
        float descentPower = Mathf.Max(1.01f, descentEaseInPower);

        if (progress <= apexProgress)
        {
            float easedAscent = Mathf.Clamp01(progress / apexProgress);
            float ascentTime = 1f - Mathf.Pow(
                1f - easedAscent,
                1f / ascentPower
            );

            return ascentTime * apexTime;
        }

        float easedDescent = Mathf.Clamp01(
            (progress - apexProgress) / (1f - apexProgress)
        );
        float descentTime = Mathf.Pow(
            easedDescent,
            1f / descentPower
        );

        return apexTime + descentTime * (1f - apexTime);
    }
}

/// <summary>
/// Pure deterministic trajectory math shared by preview rendering and runtime
/// projectile playback. No Rigidbody, gravity setting, or frame rate affects
/// this path.
/// </summary>
public static class PowerupTrajectory
{
    public static Vector3 Evaluate(
        Vector3 start,
        Vector3 end,
        float arcHeight,
        float normalizedTime)
    {
        return Evaluate(
            start,
            end,
            arcHeight,
            normalizedTime,
            PowerupTrajectoryTiming.Linear
        );
    }

    public static Vector3 Evaluate(
        Vector3 start,
        Vector3 end,
        float arcHeight,
        float normalizedTime,
        PowerupTrajectoryTiming timing)
    {
        float apexPathProgress = CalculateApexPathProgress(
            start,
            end,
            arcHeight
        );

        return EvaluatePathProgress(
            start,
            end,
            arcHeight,
            timing.RemapTime(normalizedTime, apexPathProgress)
        );
    }

    public static Vector3 EvaluatePathProgress(
        Vector3 start,
        Vector3 end,
        float arcHeight,
        float normalizedPathProgress)
    {
        float pathProgress = Mathf.Clamp01(normalizedPathProgress);
        Vector3 linearPosition = Vector3.LerpUnclamped(
            start,
            end,
            pathProgress
        );

        float verticalOffset =
            4f *
            Mathf.Max(0f, arcHeight) *
            pathProgress *
            (1f - pathProgress);

        return linearPosition + Vector3.up * verticalOffset;
    }

    public static float CalculateApexPathProgress(
        Vector3 start,
        Vector3 end,
        float arcHeight)
    {
        float safeArcHeight = Mathf.Max(0f, arcHeight);
        if (safeArcHeight <= 0.0001f) return 0.5f;

        float verticalDelta = end.y - start.y;
        float apexPathProgress =
            0.5f + verticalDelta / (8f * safeArcHeight);

        return Mathf.Clamp(apexPathProgress, 0.05f, 0.95f);
    }

    public static float PlanarDistance(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        delta.y = 0f;
        return delta.magnitude;
    }
}
