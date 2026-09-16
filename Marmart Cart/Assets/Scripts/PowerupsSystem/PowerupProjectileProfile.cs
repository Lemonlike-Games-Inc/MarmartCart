using System;
using UnityEngine;

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
    [Min(0f)]
    [SerializeField] private float spreadRadius = 2f;

    [SerializeField]
    private PowerupProjectileSweepShape sweepShape =
        PowerupProjectileSweepShape.Sphere;

    [Min(0.01f)]
    [SerializeField] private float sphereRadius = 0.22f;

    [SerializeField]
    private Vector3 boxHalfExtents =
        new Vector3(0.35f, 0.35f, 0.35f);

    [Tooltip("Fixed cast orientation offset after aligning the actor with the aim direction.")]
    [SerializeField] private Vector3 castEulerAngles;

    [SerializeField] private bool ignoreOwnerInFlight = true;
    [SerializeField] private bool ignoreCheckoutTargets = true;

    [SerializeField]
    private ProjectilePatternEntry[] entries =
        Array.Empty<ProjectilePatternEntry>();

    public float SpreadRadius => spreadRadius;
    public PowerupProjectileSweepShape SweepShape => sweepShape;
    public float SphereRadius => sphereRadius;
    public Vector3 BoxHalfExtents => boxHalfExtents;
    public Vector3 CastEulerAngles => castEulerAngles;
    public bool IgnoreOwnerInFlight => ignoreOwnerInFlight;
    public bool IgnoreCheckoutTargets => ignoreCheckoutTargets;
    public int EntryCount => entries != null ? entries.Length : 0;

    public ProjectilePatternEntry GetEntry(int index)
    {
        if (entries == null || index < 0 || index >= entries.Length)
        {
            return null;
        }

        return entries[index];
    }

    public void Sanitize()
    {
        spreadRadius = Mathf.Max(0f, spreadRadius);
        sphereRadius = Mathf.Max(0.01f, sphereRadius);
        boxHalfExtents = new Vector3(
            Mathf.Max(0.01f, boxHalfExtents.x),
            Mathf.Max(0.01f, boxHalfExtents.y),
            Mathf.Max(0.01f, boxHalfExtents.z)
        );

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
            spreadRadius = 2.5f,
            sweepShape = PowerupProjectileSweepShape.Sphere,
            sphereRadius = 0.22f,
            boxHalfExtents = Vector3.one * 0.22f,
            castEulerAngles = Vector3.zero,
            ignoreOwnerInFlight = true,
            ignoreCheckoutTargets = true,
            entries = defaultEntries
        };
    }

    public static ProjectileShotPattern CreateRecommendedIceCube()
    {
        return new ProjectileShotPattern
        {
            spreadRadius = 0f,
            sweepShape = PowerupProjectileSweepShape.Box,
            sphereRadius = 0.35f,
            boxHalfExtents = new Vector3(0.35f, 0.35f, 0.35f),
            castEulerAngles = Vector3.zero,
            ignoreOwnerInFlight = true,
            ignoreCheckoutTargets = true,
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
    [SerializeField] private FakeArcProjectile tomatoProjectilePrefab;
    [SerializeField] private FakeArcProjectile iceCubeProjectilePrefab;

    [Header("Leading-Cart Target Query")]
    [Tooltip("Assign only the layer used by colliders under LeadingCartPowerupTarget. Never include follower-cart layers.")]
    [SerializeField] private LayerMask leadingCartTargetMask;

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

    public LayerMask LeadingCartTargetMask => leadingCartTargetMask;
    public bool HasLeadingCartTargetMask => leadingCartTargetMask.value != 0;
    public bool AllowPoolGrowth => allowPoolGrowth;
    public float MinimumProjectileDuration => minimumProjectileDuration;
    public float MinimumProjectileArcHeight => minimumProjectileArcHeight;

    public FakeArcProjectile GetPrefab(PowerupId powerupId)
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
