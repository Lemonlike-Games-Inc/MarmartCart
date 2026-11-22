using UnityEngine;

/// <summary>
/// Item effect: pulls the loose carts inside the item's cone toward the
/// thrower's leading cart. They join a chain by the normal collect rule, so a
/// rival chain in the way can collect them first. Leading and chained carts
/// are never moved. Run by MagnetPullEffectSystem.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Items/Effects/Magnet Pull",
    fileName = "MagnetPullEffect"
)]
public sealed class MagnetPullEffect : ItemEffectDefinition
{
    // Layer indices from the project's Tag Manager: Cart 7, Obstacles 9, Walls 10.
    private const int CartLayerMask = 1 << 7;
    private const int ObstacleAndWallLayerMask = (1 << 9) | (1 << 10);

    [Header("Pull")]
    [Tooltip("Metres per second a pulled cart moves toward the leading cart. Carts drive at 20 (25 with speedup).")]
    [Min(1f)]
    [SerializeField] private float pullSpeed = 35f;

    [Tooltip("A pulled cart that is not collected after this many seconds is let go and stays loose.")]
    [Min(0.1f)]
    [SerializeField] private float maximumPullSeconds = 1.5f;

    [Header("What It Finds")]
    [Tooltip("Layers searched for loose carts. Only loose carts are pulled.")]
    [SerializeField] private LayerMask cartLayers = CartLayerMask;

    [Tooltip(
        "Solid colliders on these layers between the leading cart and a loose " +
        "cart shield it from the pull."
    )]
    [SerializeField] private LayerMask lineOfSightBlockingLayers = ObstacleAndWallLayerMask;

    [Tooltip("Height above the carts' pivots used for the line-of-sight check.")]
    [Min(0f)]
    [SerializeField] private float lineOfSightHeight = 0.8f;

    public float PullSpeed => pullSpeed;
    public float MaximumPullSeconds => maximumPullSeconds;
    public LayerMask CartLayers => cartLayers;
    public LayerMask LineOfSightBlockingLayers => lineOfSightBlockingLayers;
    public float LineOfSightHeight => lineOfSightHeight;
}
