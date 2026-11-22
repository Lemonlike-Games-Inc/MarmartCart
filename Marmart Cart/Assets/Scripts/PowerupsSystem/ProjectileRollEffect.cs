using UnityEngine;

/// <summary>
/// Item effect: a thrown projectile keeps rolling flat after it lands. It
/// stops at the first cart or blocking collider it meets, or (when Roll Until
/// Hit is off) after Maximum Roll Distance. Only the projectile's path
/// changes; the item's other effects (such as Tomato Blind) apply where it
/// stops. The match end cancels rolling projectiles.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Items/Effects/Projectile Roll",
    fileName = "ProjectileRollEffect"
)]
public sealed class ProjectileRollEffect : ItemEffectDefinition
{
    // Layer indices from the project's Tag Manager: Cart 7, Obstacles 9, Walls 10.
    private const int CartLayerMask = 1 << 7;
    private const int ObstacleAndWallLayerMask = (1 << 9) | (1 << 10);

    [Header("How Far")]
    [Tooltip(
        "On: rolls until it hits a cart or a blocking collider, or the match ends. " +
        "Off: also splats on the floor after Maximum Roll Distance."
    )]
    [SerializeField] private bool rollUntilHit = true;

    [Tooltip("Used only when Roll Until Hit is off. Metres rolled after landing.")]
    [Min(0.1f)]
    [SerializeField] private float maximumRollDistance = 30f;

    [Header("Movement")]
    [Tooltip("Metres per second while rolling.")]
    [Min(0.1f)]
    [SerializeField] private float rollSpeed = 15f;

    [Tooltip(
        "Height of the projectile's pivot above the floor while rolling. " +
        "Match it to the model so it sits on the floor."
    )]
    [Min(0f)]
    [SerializeField] private float rollHeight = 0.9f;

    [Tooltip(
        "On: the whole volley rolls in the aim direction, side by side. " +
        "Off: each projectile rolls on along its own flight line, so a volley fans out."
    )]
    [SerializeField] private bool rollAlongAim = true;

    [Header("What Stops It")]
    [Tooltip(
        "Radius of the hit sphere while rolling, centred on the projectile pivot. " +
        "0 = use the projectile prefab's hitbox (the Tomato's is about 1.7 m, " +
        "wider than its model). Keep it below Roll Height so the floor is never hit."
    )]
    [Min(0f)]
    [SerializeField] private float rollHitRadius = 0.8f;

    [Tooltip(
        "Carts on these layers stop the roll. A leading cart gets the item's " +
        "effect; chained carts, loose carts and carts in checkout only stop it. " +
        "The thrower's own leading cart is rolled through when the throw has " +
        "Ignore Owner In Flight on and the item does not hit its thrower " +
        "(Tomato: Blinds Thrower)."
    )]
    [SerializeField] private LayerMask cartLayers = CartLayerMask;

    [Tooltip("Solid (non-trigger) colliders on these layers stop the roll with a splat and no effect.")]
    [SerializeField] private LayerMask blockingLayers = ObstacleAndWallLayerMask;

    public bool RollUntilHit => rollUntilHit;
    public float MaximumRollDistance => maximumRollDistance;
    public float RollSpeed => rollSpeed;
    public float RollHeight => rollHeight;
    public bool RollAlongAim => rollAlongAim;
    public float RollHitRadius => rollHitRadius;
    public LayerMask CartLayers => cartLayers;
    public LayerMask BlockingLayers => blockingLayers;

    /// <summary>Distance the projectile rolls before it splats; infinite when Roll Until Hit is on.</summary>
    public float ResolvedRollDistance =>
        rollUntilHit ? float.PositiveInfinity : maximumRollDistance;
}
