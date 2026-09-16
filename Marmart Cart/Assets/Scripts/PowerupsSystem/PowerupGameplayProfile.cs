using UnityEngine;

/// <summary>
/// Shared gameplay tuning for deterministic power-up targeting.
///
/// The requested landing point always remains ground-based. A separate
/// projectile-blocking mask lets presentation show the first shelf/wall hit
/// along that unchanged arc, and can be reused by future projectile actors.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Powerups/Gameplay Profile",
    fileName = "PowerupGameplayProfile"
)]
public class PowerupGameplayProfile : ScriptableObject
{
    #region Analog Targeting

    [Header("Analog Targeting")]
    [Range(0f, 0.95f)]
    [SerializeField] private float aimDeadzone = 0.15f;

    [Min(0f)]
    [SerializeField] private float minimumThrowRange = 2.5f;

    [Min(0.01f)]
    [SerializeField] private float maximumThrowRange = 12f;

    #endregion

    #region Ground Resolution

    [Header("Ground Resolution")]
    [Tooltip("Assign only the explicit walkable PowerupGround layer(s). Do not include shelves, carts, pickups, or props.")]
    [SerializeField] private LayerMask powerupGroundMask;

    [Min(0.01f)]
    [SerializeField] private float groundProbeHeight = 8f;

    [Min(0.01f)]
    [SerializeField] private float groundProbeDistance = 20f;

    [Min(0f)]
    [SerializeField] private float landingClearance = 0.03f;

    #endregion

    #region Deterministic Arc

    // Preserved invisibly so existing assets and any older callers keep their
    // serialized/API compatibility. Active Tomato/Ice flight times now live in
    // their ProjectileShotPattern inside PowerupProjectileProfile.
    [HideInInspector]
    [Min(0.01f)]
    [SerializeField] private float minimumFlightTime = 0.5f;

    [HideInInspector]
    [Min(0.01f)]
    [SerializeField] private float maximumFlightTime = 1f;

    [Header("Deterministic Arc Height")]
    [Min(0f)]
    [SerializeField] private float minimumArcHeight = 2.5f;

    [Min(0f)]
    [SerializeField] private float maximumArcHeight = 6f;

    [Tooltip("Local-space fallback used only when the leading-cart prefab has no assigned PowerupMounts Throw Origin.")]
    [SerializeField] private Vector3 fallbackThrowOriginOffset = new Vector3(0f, 1.25f, 0f);

    #endregion

    #region Projectile Blocking

    [Header("Projectile Blocking / Visual Lifting")]
    [Tooltip(
        "Shelves, walls, map-edge blockers, and other solid layers that stop a flying power-up. " +
        "Do not include PowerupGround or carts. The aim preview truncates at the first hit while " +
        "the authoritative ground destination remains unchanged."
    )]
    [SerializeField] private LayerMask projectileBlockingMask;

    [Tooltip("Segment count used to sweep the curved preview for its first blocking-layer hit.")]
    [Range(4, 128)]
    [SerializeField] private int projectileBlockingSampleCount = 40;

    [Tooltip(
        "When enabled, a shelf/wall/map-edge hit keeps the preview visible but rejects Activate. " +
        "The stored power-up is retained. Disable this to allow firing into blockers."
    )]
    [SerializeField] private bool blockActivationWhenTrajectoryObstructed = true;

    #endregion

    #region Impact Envelopes

    [Header("Impact Preview Envelopes")]
    [Tooltip("Complete advertised Tomato volley radius in world units.")]
    [Min(0.01f)]
    [SerializeField] private float tomatoPreviewRadius = 2.5f;

    [Tooltip("Advertised Ice Cube landing/hazard radius in world units.")]
    [Min(0.01f)]
    [SerializeField] private float iceCubePreviewRadius = 0.9f;

    #endregion

    #region Public Values

    public float AimDeadzone => aimDeadzone;
    public float MinimumThrowRange => minimumThrowRange;
    public float MaximumThrowRange => maximumThrowRange;
    public LayerMask PowerupGroundMask => powerupGroundMask;
    public float GroundProbeHeight => groundProbeHeight;
    public float GroundProbeDistance => groundProbeDistance;
    public float LandingClearance => landingClearance;
    public float MinimumFlightTime => minimumFlightTime;
    public float MaximumFlightTime => maximumFlightTime;
    public float MinimumArcHeight => minimumArcHeight;
    public float MaximumArcHeight => maximumArcHeight;
    public Vector3 FallbackThrowOriginOffset => fallbackThrowOriginOffset;
    public LayerMask ProjectileBlockingMask => projectileBlockingMask;
    public int ProjectileBlockingSampleCount => projectileBlockingSampleCount;
    public bool BlockActivationWhenTrajectoryObstructed =>
        blockActivationWhenTrajectoryObstructed;

    public bool HasGroundMask => powerupGroundMask.value != 0;
    public bool HasProjectileBlockingMask => projectileBlockingMask.value != 0;

    public float RemapAimMagnitude(float rawMagnitude)
    {
        float clampedMagnitude = Mathf.Clamp01(rawMagnitude);

        if (clampedMagnitude <= aimDeadzone) return 0f;

        return Mathf.InverseLerp(
            aimDeadzone,
            1f,
            clampedMagnitude
        );
    }

    public float EvaluateThrowRange(float adjustedMagnitude)
    {
        return Mathf.Lerp(
            minimumThrowRange,
            maximumThrowRange,
            Mathf.Clamp01(adjustedMagnitude)
        );
    }

    public float EvaluateFlightTime(float planarDistance)
    {
        float distance01 = Mathf.InverseLerp(
            minimumThrowRange,
            maximumThrowRange,
            Mathf.Max(0f, planarDistance)
        );

        return Mathf.Lerp(
            minimumFlightTime,
            maximumFlightTime,
            distance01
        );
    }

    public float EvaluateArcHeight(float planarDistance)
    {
        float distance01 = Mathf.InverseLerp(
            minimumThrowRange,
            maximumThrowRange,
            Mathf.Max(0f, planarDistance)
        );

        return Mathf.Lerp(
            minimumArcHeight,
            maximumArcHeight,
            distance01
        );
    }

    public float GetImpactPreviewRadius(PowerupId powerupId)
    {
        switch (powerupId)
        {
            case PowerupId.Tomato:
                return tomatoPreviewRadius;

            case PowerupId.IceCube:
                return iceCubePreviewRadius;

            default:
                return 0f;
        }
    }

    #endregion

    #region Validation

    private void OnValidate()
    {
        aimDeadzone = Mathf.Clamp(aimDeadzone, 0f, 0.95f);
        minimumThrowRange = Mathf.Max(0f, minimumThrowRange);
        maximumThrowRange = Mathf.Max(minimumThrowRange + 0.01f, maximumThrowRange);

        groundProbeHeight = Mathf.Max(0.01f, groundProbeHeight);
        groundProbeDistance = Mathf.Max(groundProbeHeight + 0.01f, groundProbeDistance);
        landingClearance = Mathf.Max(0f, landingClearance);

        minimumFlightTime = Mathf.Max(0.01f, minimumFlightTime);
        maximumFlightTime = Mathf.Max(minimumFlightTime, maximumFlightTime);
        minimumArcHeight = Mathf.Max(0f, minimumArcHeight);
        maximumArcHeight = Mathf.Max(minimumArcHeight, maximumArcHeight);
        projectileBlockingSampleCount = Mathf.Clamp(projectileBlockingSampleCount, 4, 128);

        tomatoPreviewRadius = Mathf.Max(0.01f, tomatoPreviewRadius);
        iceCubePreviewRadius = Mathf.Max(0.01f, iceCubePreviewRadius);
    }

    #endregion
}
