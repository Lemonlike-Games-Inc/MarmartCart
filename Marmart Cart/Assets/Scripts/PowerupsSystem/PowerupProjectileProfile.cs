using System;
using UnityEngine;
using UnityEngine.Serialization;

public enum PowerupProjectileSweepShape
{
    Sphere,
    Box
}

[Serializable]
public sealed class ProjectilePatternEntry
{
    [Tooltip("Offset inside the pattern's unit circle. X is pattern-right and Y is pattern-forward.")]
    [SerializeField] private Vector2 normalizedLandingOffset;

    [Tooltip("Added to the center flight duration. Negative values arrive earlier.")]
    [SerializeField] private float arrivalTimeOffsetSeconds;

    [Tooltip("Added to the center trajectory height for this independent actor.")]
    [SerializeField] private float arcHeightOffset;

    [SerializeField] private Vector3 visualSpinAxis = Vector3.up;
    [SerializeField] private float visualSpinDegreesPerSecond = 360f;

    [Tooltip("Uniformly scales both the visual and the prefab-authored sweep hitbox for this copy.")]
    [Min(0.01f)]
    [SerializeField] private float visualScaleMultiplier = 1f;

    public Vector2 NormalizedLandingOffset => normalizedLandingOffset;
    public float ArrivalTimeOffsetSeconds => arrivalTimeOffsetSeconds;
    public float ArcHeightOffset => arcHeightOffset;
    public Vector3 VisualSpinAxis => visualSpinAxis;
    public float VisualSpinDegreesPerSecond => visualSpinDegreesPerSecond;
    public float VisualScaleMultiplier => visualScaleMultiplier;

    public ProjectilePatternEntry()
    {
        normalizedLandingOffset = Vector2.zero;
        arrivalTimeOffsetSeconds = 0f;
        arcHeightOffset = 0f;
        visualSpinAxis = Vector3.up;
        visualSpinDegreesPerSecond = 360f;
        visualScaleMultiplier = 1f;
    }

    public ProjectilePatternEntry(
        Vector2 normalizedOffset,
        float arrivalOffset,
        float heightOffset,
        Vector3 spinAxis,
        float spinSpeed,
        float scaleMultiplier = 1f)
    {
        normalizedLandingOffset = normalizedOffset;
        arrivalTimeOffsetSeconds = arrivalOffset;
        arcHeightOffset = heightOffset;
        visualSpinAxis = spinAxis;
        visualSpinDegreesPerSecond = spinSpeed;
        visualScaleMultiplier = scaleMultiplier;
        Sanitize();
    }

    public void Sanitize()
    {
        normalizedLandingOffset = Vector2.ClampMagnitude(
            normalizedLandingOffset,
            1f
        );

        if (visualSpinAxis.sqrMagnitude <= 0.000001f)
        {
            visualSpinAxis = Vector3.up;
        }

        visualScaleMultiplier = Mathf.Max(0.01f, visualScaleMultiplier);
    }
}

[Serializable]
public sealed class ProjectileShotPattern
{
    [Header("Flight Time By Throw Distance")]
    [Tooltip("Flight time used at the shared minimum throw range.")]
    [Min(0.01f)]
    [SerializeField] private float minimumFlightTime = 0.5f;

    [Tooltip("Flight time used at the shared maximum throw range.")]
    [Min(0.01f)]
    [SerializeField] private float maximumFlightTime = 1f;

    [Header("Volley Spread")]
    [Min(0f)]
    [SerializeField] private float spreadRadius = 2f;

    [Tooltip("Fixed cast orientation offset after aligning the actor with the aim direction.")]
    [SerializeField] private Vector3 castEulerAngles;

    [SerializeField] private bool ignoreOwnerInFlight = true;
    [SerializeField] private bool ignoreCheckoutTargets = true;

    [Header("Cart Hit Policy")]
    [Tooltip("Whether this projectile completes and applies its future effect when it hits a leading cart.")]
    [SerializeField] private bool hasEffectOnLeadingCart = true;

    [Tooltip("Whether this projectile completes and applies its future effect when it hits an owned follower cart. Loose carts remain ignored.")]
    [SerializeField] private bool hasEffectOnChainedCarts;

    [SerializeField]
    private ProjectilePatternEntry[] entries =
        Array.Empty<ProjectilePatternEntry>();

    public float MinimumFlightTime => minimumFlightTime;
    public float MaximumFlightTime => maximumFlightTime;
    public float SpreadRadius => spreadRadius;
    public Vector3 CastEulerAngles => castEulerAngles;
    public bool IgnoreOwnerInFlight => ignoreOwnerInFlight;
    public bool IgnoreCheckoutTargets => ignoreCheckoutTargets;
    public bool HasEffectOnLeadingCart => hasEffectOnLeadingCart;
    public bool HasEffectOnChainedCarts => hasEffectOnChainedCarts;
    public int EntryCount => entries != null ? entries.Length : 0;

    public ProjectilePatternEntry GetEntry(int index)
    {
        if (entries == null || index < 0 || index >= entries.Length)
        {
            return null;
        }

        return entries[index];
    }

    public float EvaluateFlightTime(
        float planarDistance,
        float minimumThrowRange,
        float maximumThrowRange)
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

    public void Sanitize()
    {
        minimumFlightTime = Mathf.Max(0.01f, minimumFlightTime);
        maximumFlightTime = Mathf.Max(
            minimumFlightTime,
            maximumFlightTime
        );
        spreadRadius = Mathf.Max(0f, spreadRadius);

        if (entries == null)
        {
            entries = Array.Empty<ProjectilePatternEntry>();
            return;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            entries[i]?.Sanitize();
        }
    }

    public static ProjectileShotPattern CreateRecommendedTomato()
    {
        const int surroundingCount = 5;
        ProjectilePatternEntry[] defaultEntries =
            new ProjectilePatternEntry[surroundingCount + 1];

        defaultEntries[0] = new ProjectilePatternEntry(
            Vector2.zero,
            0f,
            0f,
            new Vector3(1f, 1f, 0.2f),
            420f
        );

        float[] arrivalOffsets = { -0.12f, -0.06f, 0f, 0.06f, 0.12f };
        float[] heightOffsets = { -0.15f, 0.1f, 0.2f, 0.05f, -0.1f };

        for (int i = 0; i < surroundingCount; i++)
        {
            float radians = i * Mathf.PI * 2f / surroundingCount;
            Vector2 offset = new Vector2(
                Mathf.Cos(radians),
                Mathf.Sin(radians)
            ) * 0.78f;

            defaultEntries[i + 1] = new ProjectilePatternEntry(
                offset,
                arrivalOffsets[i],
                heightOffsets[i],
                new Vector3(0.4f + i * 0.1f, 1f, 0.8f - i * 0.08f),
                330f + i * 35f
            );
        }

        return new ProjectileShotPattern
        {
            minimumFlightTime = 0.5f,
            maximumFlightTime = 1f,
            spreadRadius = 2.5f,
            castEulerAngles = Vector3.zero,
            ignoreOwnerInFlight = true,
            ignoreCheckoutTargets = true,
            hasEffectOnLeadingCart = true,
            hasEffectOnChainedCarts = false,
            entries = defaultEntries
        };
    }

    public static ProjectileShotPattern CreateRecommendedIceCube()
    {
        return new ProjectileShotPattern
        {
            minimumFlightTime = 0.5f,
            maximumFlightTime = 1f,
            spreadRadius = 0f,
            castEulerAngles = Vector3.zero,
            ignoreOwnerInFlight = true,
            ignoreCheckoutTargets = true,
            hasEffectOnLeadingCart = true,
            hasEffectOnChainedCarts = false,
            entries = new[]
            {
                new ProjectilePatternEntry(
                    Vector2.zero,
                    0f,
                    0f,
                    new Vector3(0.3f, 1f, 0.2f),
                    150f
                )
            }
        };
    }
}

/// <summary>
/// Shared prefab, pooling, hit-query, and deterministic shot-pattern settings
/// for the two projectile power-ups. Gameplay effects are deliberately absent.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Powerups/Projectile Profile",
    fileName = "PowerupProjectileProfile"
)]
public class PowerupProjectileProfile : ScriptableObject
{
    [Header("Swappable Projectile Prefabs")]
    [SerializeField] private GameObject tomatoProjectilePrefab;
    [SerializeField] private GameObject iceCubeProjectilePrefab;

    [Header("Cart Target Query")]
    [Tooltip(
        "Assign the game's existing Cart layer. PowerupCartTarget and " +
        "ChainedCartManager state distinguish leading, chained, and loose carts in code."
    )]
    [FormerlySerializedAs("leadingCartTargetMask")]
    [SerializeField] private LayerMask cartTargetMask;

    [Header("Pool")]
    [Min(0)]
    [SerializeField] private int tomatoPrewarmCount = 12;

    [Min(0)]
    [SerializeField] private int iceCubePrewarmCount = 4;

    [SerializeField] private bool allowPoolGrowth = true;

    [Header("Runtime Safety")]
    [Min(0.01f)]
    [SerializeField] private float minimumProjectileDuration = 0.05f;

    [Min(0f)]
    [SerializeField] private float minimumProjectileArcHeight = 0f;

    [Header("Patterns")]
    [SerializeField]
    private ProjectileShotPattern tomatoPattern =
        ProjectileShotPattern.CreateRecommendedTomato();

    [SerializeField]
    private ProjectileShotPattern iceCubePattern =
        ProjectileShotPattern.CreateRecommendedIceCube();

    public LayerMask CartTargetMask => cartTargetMask;
    public bool HasCartTargetMask => cartTargetMask.value != 0;
    public bool AllowPoolGrowth => allowPoolGrowth;
    public float MinimumProjectileDuration => minimumProjectileDuration;
    public float MinimumProjectileArcHeight => minimumProjectileArcHeight;

    public GameObject GetPrefabGameObject(PowerupId powerupId)
    {
        switch (powerupId)
        {
            case PowerupId.Tomato:
                return tomatoProjectilePrefab;

            case PowerupId.IceCube:
                return iceCubeProjectilePrefab;

            default:
                return null;
        }
    }

    public FakeArcProjectile GetPrefab(PowerupId powerupId)
    {
        GameObject prefab = GetPrefabGameObject(powerupId);
        return prefab != null
            ? prefab.GetComponent<FakeArcProjectile>()
            : null;
    }

    public int GetPrewarmCount(PowerupId powerupId)
    {
        switch (powerupId)
        {
            case PowerupId.Tomato:
                return tomatoPrewarmCount;

            case PowerupId.IceCube:
                return iceCubePrewarmCount;

            default:
                return 0;
        }
    }

    public ProjectileShotPattern GetPattern(PowerupId powerupId)
    {
        switch (powerupId)
        {
            case PowerupId.Tomato:
                return tomatoPattern;

            case PowerupId.IceCube:
                return iceCubePattern;

            default:
                return null;
        }
    }

    public float EvaluateFlightTime(
        PowerupId powerupId,
        float planarDistance,
        float minimumThrowRange,
        float maximumThrowRange)
    {
        ProjectileShotPattern pattern = GetPattern(powerupId);

        float evaluatedTime = pattern != null
            ? pattern.EvaluateFlightTime(
                planarDistance,
                minimumThrowRange,
                maximumThrowRange
            )
            : minimumProjectileDuration;

        return Mathf.Max(minimumProjectileDuration, evaluatedTime);
    }

    public bool TryGetAuthoredHitboxPlanarRadius(
        PowerupId powerupId,
        out float planarRadius)
    {
        FakeArcProjectile prefabActor = GetPrefab(powerupId);

        if (prefabActor != null &&
            prefabActor.TryGetAuthoredHitboxPlanarRadius(
                out planarRadius
            ))
        {
            return true;
        }

        planarRadius = 0f;
        return false;
    }

    [ContextMenu("Reset Recommended Projectile Patterns")]
    private void ResetRecommendedPatterns()
    {
        tomatoPattern = ProjectileShotPattern.CreateRecommendedTomato();
        iceCubePattern = ProjectileShotPattern.CreateRecommendedIceCube();
    }

    private void OnValidate()
    {
        tomatoPrewarmCount = Mathf.Max(0, tomatoPrewarmCount);
        iceCubePrewarmCount = Mathf.Max(0, iceCubePrewarmCount);
        minimumProjectileDuration = Mathf.Max(0.01f, minimumProjectileDuration);
        minimumProjectileArcHeight = Mathf.Max(0f, minimumProjectileArcHeight);

        if (tomatoPattern == null)
        {
            tomatoPattern = ProjectileShotPattern.CreateRecommendedTomato();
        }

        if (iceCubePattern == null)
        {
            iceCubePattern = ProjectileShotPattern.CreateRecommendedIceCube();
        }

        tomatoPattern.Sanitize();
        iceCubePattern.Sanitize();
    }
}
