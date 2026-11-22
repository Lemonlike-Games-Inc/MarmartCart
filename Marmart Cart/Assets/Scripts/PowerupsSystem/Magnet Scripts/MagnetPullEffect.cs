using UnityEngine;

/// <summary>
/// Item effect: pulls the loose carts, and optionally the rival leading carts,
/// inside the item's cone toward the thrower's leading cart. Both use the same
/// pull: accelerate toward the thrower until the closing speed reaches the
/// pull speed. Loose carts also lose their sideways drift so they home in and
/// join a chain by the normal collect rule. Leading carts keep their own grip
/// and steering, so their players can fight the pull, and are never dragged
/// backward (LeadingCartExternalForces). Chained carts are never moved. Run
/// by MagnetPullEffectSystem.
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

    [Header("Loose Carts")]
    [Tooltip("Closing speed (m/s) a loose cart is pulled up to. Carts drive at 20 (25 with speedup).")]
    [Min(1f)]
    [SerializeField] private float pullSpeed = 35f;

    [Tooltip("Strength of the loose-cart pull (m/s²). High values reach Pull Speed almost at once.")]
    [Min(1f)]
    [SerializeField] private float pullAcceleration = 200f;

    [Tooltip("A pulled cart that is not collected after this many seconds is let go and stays loose.")]
    [Min(0.1f)]
    [SerializeField] private float maximumPullSeconds = 1.5f;

    [Header("Leading Carts")]
    [Tooltip(
        "Also pull rival leading carts in the cone. Carts in ghost mode, checkout or frozen are " +
        "never pulled. A pull from behind only slows a cart down to its Leading Cart External " +
        "Forces' Minimum Forward Speed; it never drags it backward."
    )]
    [SerializeField] private bool pullLeadingCarts = true;

    [Tooltip("Closing speed (m/s) a rival leading cart is pulled up to.")]
    [Min(0.1f)]
    [SerializeField] private float leadingCartPullSpeed = 12f;

    [Tooltip(
        "Strength of the leading-cart pull (m/s²). The cart's own drive gives about 16 " +
        "and its grip cancels sideways slides quickly, so lower values can be fought."
    )]
    [Min(0.1f)]
    [SerializeField] private float leadingCartPullAcceleration = 30f;

    [Tooltip("How long a rival leading cart is pulled.")]
    [Min(0.05f)]
    [SerializeField] private float leadingCartPullSeconds = 0.5f;

    [Header("What It Finds")]
    [Tooltip("Layers searched for loose and leading carts. Chained carts are never pulled.")]
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
    public float PullAcceleration => pullAcceleration;
    public float MaximumPullSeconds => maximumPullSeconds;
    public bool PullLeadingCarts => pullLeadingCarts;
    public float LeadingCartPullSpeed => leadingCartPullSpeed;
    public float LeadingCartPullAcceleration => leadingCartPullAcceleration;
    public float LeadingCartPullSeconds => leadingCartPullSeconds;
    public LayerMask CartLayers => cartLayers;
    public LayerMask LineOfSightBlockingLayers => lineOfSightBlockingLayers;
    public float LineOfSightHeight => lineOfSightHeight;
}
