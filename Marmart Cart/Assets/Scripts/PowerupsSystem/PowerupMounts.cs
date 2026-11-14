using UnityEngine;

/// <summary>
/// Optional authored attachment points on a runtime leading-cart prefab.
/// Keeping these references on the cart makes target range and projectile
/// visuals independent from the persistent per-player controller hierarchy.
/// </summary>
[DisallowMultipleComponent]
public class PowerupMounts : MonoBehaviour
{
    [Header("Targeting Origins")]
    [Tooltip("Horizontal range is measured from this point. Falls back to the CartControlScript transform.")]
    [SerializeField] private Transform rangeOrigin;

    [Tooltip("The deterministic projectile arc begins here. Falls back to a profile-authored local offset.")]
    [SerializeField] private Transform throwOrigin;

    [Header("Future Effect Mounts")]
    [Tooltip("Optional location for a held/equipped power-up model.")]
    [SerializeField] private Transform heldItemMount;

    [Tooltip("Optional location for the future Strong Fan model.")]
    [SerializeField] private Transform fanMount;

    public Transform RangeOrigin => rangeOrigin;
    public Transform ThrowOrigin => throwOrigin;
    public Transform HeldItemMount => heldItemMount;
    public Transform FanMount => fanMount;

    public Vector3 GetRangeOriginPosition(Transform fallback)
    {
        if (rangeOrigin != null) return rangeOrigin.position;
        return fallback != null ? fallback.position : transform.position;
    }

    public Vector3 GetThrowOriginPosition(
        Transform fallback,
        Vector3 fallbackLocalOffset)
    {
        if (throwOrigin != null) return throwOrigin.position;
        if (fallback != null) return fallback.TransformPoint(fallbackLocalOffset);
        return transform.TransformPoint(fallbackLocalOffset);
    }
}
