using UnityEngine;

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
        float time = Mathf.Clamp01(normalizedTime);
        Vector3 linearPosition = Vector3.LerpUnclamped(start, end, time);
        float verticalOffset =
            4f * Mathf.Max(0f, arcHeight) * time * (1f - time);

        return linearPosition + Vector3.up * verticalOffset;
    }

    /// <summary>
    /// Returns the normalized time of the true highest world-space point. This
    /// accounts for throw and landing positions having different Y values.
    /// </summary>
    public static float CalculateApexNormalizedTime(
        Vector3 start,
        Vector3 end,
        float arcHeight)
    {
        float safeArcHeight = Mathf.Max(0f, arcHeight);

        if (safeArcHeight <= 0.0001f)
        {
            return end.y > start.y ? 1f : 0f;
        }

        float verticalDelta = end.y - start.y;
        float apexTime = 0.5f + verticalDelta / (8f * safeArcHeight);
        return Mathf.Clamp01(apexTime);
    }

    public static float PlanarDistance(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        delta.y = 0f;
        return delta.magnitude;
    }
}
