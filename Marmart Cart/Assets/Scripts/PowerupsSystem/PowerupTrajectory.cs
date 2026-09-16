using UnityEngine;

/// <summary>
/// Pure deterministic trajectory math shared by preview rendering and future
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
        float t = Mathf.Clamp01(normalizedTime);
        Vector3 linearPosition = Vector3.LerpUnclamped(start, end, t);
        float verticalOffset = 4f * Mathf.Max(0f, arcHeight) * t * (1f - t);
        return linearPosition + Vector3.up * verticalOffset;
    }

    public static float PlanarDistance(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        delta.y = 0f;
        return delta.magnitude;
    }
}
