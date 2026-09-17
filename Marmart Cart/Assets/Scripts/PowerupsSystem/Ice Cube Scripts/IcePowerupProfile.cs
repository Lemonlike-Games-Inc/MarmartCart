using UnityEngine;

/// <summary>
/// Ice gameplay tuning. Projectile flight, spread, and in-air collision policy
/// remain in PowerupProjectileProfile.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Powerups/Ice Profile",
    fileName = "IcePowerupProfile"
)]
public class IcePowerupProfile : ScriptableObject
{
    [Header("Freeze Effect")]
    [Tooltip(
        "Scaled gameplay seconds that a direct Ice hit or triggered ground " +
        "hazard freezes a leading cart. Repeated hits refresh this duration."
    )]
    [Min(0.05f)]
    [SerializeField] private float freezeDurationSeconds = 2.5f;

    [Header("Ground Hazard")]
    [Tooltip(
        "Prefab created when an Ice projectile reaches the ground. Its root " +
        "must contain IceGroundHazard and one designer-fitted BoxCollider."
    )]
    [SerializeField] private GameObject groundHazardPrefab;

    [Tooltip(
        "Scaled gameplay seconds an untriggered ground hazard remains active."
    )]
    [Min(0.05f)]
    [SerializeField] private float groundHazardLifetimeSeconds = 5f;

    [Tooltip("Small offset along the landing normal to avoid visual z-fighting.")]
    [SerializeField] private float groundHazardSurfaceOffset = 0.02f;

    [Tooltip("Align the hazard's up axis to the projectile landing normal.")]
    [SerializeField] private bool alignHazardToSurfaceNormal = true;

    [Tooltip("Additional local rotation applied after surface alignment.")]
    [SerializeField] private Vector3 groundHazardEulerOffset;

    [Tooltip("Additional scale applied to the authored hazard prefab.")]
    [SerializeField] private Vector3 groundHazardScaleMultiplier = Vector3.one;

    [Header("Ground Hazard Pool")]
    [Min(0)]
    [SerializeField] private int prewarmGroundHazardCount = 4;

    [SerializeField] private bool allowGroundHazardPoolGrowth = true;

    [Tooltip(
        "Maximum total hazard instances, including active and available. " +
        "Use 0 for no explicit cap when growth is enabled."
    )]
    [Min(0)]
    [SerializeField] private int maximumGroundHazardInstances = 16;

    public float FreezeDurationSeconds => freezeDurationSeconds;
    public GameObject GroundHazardPrefab => groundHazardPrefab;
    public float GroundHazardLifetimeSeconds => groundHazardLifetimeSeconds;
    public float GroundHazardSurfaceOffset => groundHazardSurfaceOffset;
    public bool AlignHazardToSurfaceNormal => alignHazardToSurfaceNormal;
    public Vector3 GroundHazardEulerOffset => groundHazardEulerOffset;
    public Vector3 GroundHazardScaleMultiplier => groundHazardScaleMultiplier;
    public int PrewarmGroundHazardCount => prewarmGroundHazardCount;
    public bool AllowGroundHazardPoolGrowth => allowGroundHazardPoolGrowth;
    public int MaximumGroundHazardInstances => maximumGroundHazardInstances;

    public IceGroundHazard GetGroundHazardPrefab()
    {
        return groundHazardPrefab != null
            ? groundHazardPrefab.GetComponent<IceGroundHazard>()
            : null;
    }

    private void OnValidate()
    {
        freezeDurationSeconds = Mathf.Max(0.05f, freezeDurationSeconds);
        groundHazardLifetimeSeconds = Mathf.Max(
            0.05f,
            groundHazardLifetimeSeconds
        );
        prewarmGroundHazardCount = Mathf.Max(0, prewarmGroundHazardCount);
        maximumGroundHazardInstances = Mathf.Max(
            0,
            maximumGroundHazardInstances
        );

        groundHazardScaleMultiplier.x = Mathf.Max(
            0.0001f,
            groundHazardScaleMultiplier.x
        );
        groundHazardScaleMultiplier.y = Mathf.Max(
            0.0001f,
            groundHazardScaleMultiplier.y
        );
        groundHazardScaleMultiplier.z = Mathf.Max(
            0.0001f,
            groundHazardScaleMultiplier.z
        );
    }
}
