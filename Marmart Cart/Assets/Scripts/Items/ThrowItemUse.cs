using UnityEngine;

/// <summary>
/// Thrown along the aimed arc (Tomato, Ice Cube). Holds what differs per
/// thrown item. The shared throw rules (aim deadzone, ranges, ground and
/// blocking masks, arc heights) stay in PowerupGameplayProfile, and the shared
/// projectile rules (cart mask, pool growth) in PowerupProjectileProfile.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Items/Uses/Throw",
    fileName = "Throw Use"
)]
public sealed class ThrowItemUse : ItemUseDefinition
{
    [Header("Projectile")]
    [Tooltip("Root must contain FakeArcProjectile with a designer-fitted hitbox.")]
    [SerializeField] private GameObject projectilePrefab;

    [Tooltip("Projectiles created up front in the scene projectile pool.")]
    [Min(0)]
    [SerializeField] private int prewarmCount = 4;

    [SerializeField] private ProjectileShotPattern pattern = new ProjectileShotPattern();

    [Header("Aim Preview")]
    [Tooltip("Advertised landing radius in world units (the whole volley for a multi-projectile throw).")]
    [Min(0.01f)]
    [SerializeField] private float previewRadius = 1f;

    public override bool RequiresAim => true;

    public GameObject ProjectilePrefabObject => projectilePrefab;
    public int PrewarmCount => prewarmCount;
    public ProjectileShotPattern Pattern => pattern;
    public float PreviewRadius => previewRadius;

    public FakeArcProjectile GetProjectilePrefab()
    {
        return projectilePrefab != null
            ? projectilePrefab.GetComponent<FakeArcProjectile>()
            : null;
    }

    private void OnValidate()
    {
        prewarmCount = Mathf.Max(0, prewarmCount);
        previewRadius = Mathf.Max(0.01f, previewRadius);

        if (pattern == null) pattern = new ProjectileShotPattern();
        pattern.Sanitize();
    }
}
