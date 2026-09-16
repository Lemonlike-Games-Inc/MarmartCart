using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Small scene-level pool dedicated to the fixed Tomato/Ice projectile scope.
/// It is intentionally not a project-wide generic pooling framework.
/// </summary>
[DisallowMultipleComponent]
public class PowerupProjectilePool : MonoBehaviour
{
    private sealed class PoolBucket
    {
        public PowerupId PowerupId;
        public FakeArcProjectile Prefab;
        public readonly Stack<FakeArcProjectile> Available =
            new Stack<FakeArcProjectile>();
        public int TotalCreated;
    }

    [Header("References")]
    [SerializeField] private PowerupProjectileProfile projectileProfile;

    [Tooltip("Optional hierarchy parent for pooled actors. Defaults to this object.")]
    [SerializeField] private Transform poolRoot;

    [Header("Diagnostics")]
    [SerializeField] private bool logNeutralCompletions = true;

    [Header("Runtime - Read Only")]
    [SerializeField] private int totalCreatedActorCount;
    [SerializeField] private int rentedActorCount;

    private readonly Dictionary<PowerupId, PoolBucket> buckets =
        new Dictionary<PowerupId, PoolBucket>();

    private readonly Dictionary<FakeArcProjectile, PoolBucket> actorBuckets =
        new Dictionary<FakeArcProjectile, PoolBucket>();

    public PowerupProjectileProfile ProjectileProfile => projectileProfile;
    public int TotalCreatedActorCount => totalCreatedActorCount;
    public int RentedActorCount => rentedActorCount;

    public event Action<PowerupProjectileCompletion> OnProjectileCompleted;

    private void Awake()
    {
        if (poolRoot == null) poolRoot = transform;

        Prewarm(PowerupId.Tomato);
        Prewarm(PowerupId.IceCube);
    }

    public bool CanProvide(PowerupId powerupId, int requiredCount)
    {
        if (requiredCount <= 0 || projectileProfile == null) return false;

        PoolBucket bucket = GetOrCreateBucket(powerupId);
        if (bucket == null || bucket.Prefab == null) return false;

        return projectileProfile.AllowPoolGrowth ||
               bucket.Available.Count >= requiredCount;
    }

    public bool TryRent(
        PowerupId powerupId,
        out FakeArcProjectile projectile)
    {
        projectile = null;

        PoolBucket bucket = GetOrCreateBucket(powerupId);
        if (bucket == null || bucket.Prefab == null) return false;

        while (bucket.Available.Count > 0 && projectile == null)
        {
            projectile = bucket.Available.Pop();
        }

        if (projectile == null)
        {
            if (projectileProfile == null ||
                !projectileProfile.AllowPoolGrowth)
            {
                return false;
            }

            projectile = CreateActor(bucket);
        }

        if (projectile == null) return false;

        projectile.MarkRented();
        rentedActorCount++;
        return true;
    }

    public void ReturnUnlaunched(FakeArcProjectile projectile)
    {
        ReturnToBucket(projectile);
    }

    internal void ReturnCompleted(
        FakeArcProjectile projectile,
        PowerupProjectileCompletion completion)
    {
        if (logNeutralCompletions)
        {
            string targetText = completion.HitCartKind !=
                PowerupCartTargetKind.None
                ? $" P{completion.HitPlayerIndex}/{completion.HitCartKind}"
                : string.Empty;

            Debug.Log(
                $"[PowerupProjectilePool] Shot #{completion.ShotVersion} " +
                $"{completion.PowerupId}[{completion.PatternEntryIndex}] " +
                $"completed: {completion.Reason}{targetText}.",
                this
            );
        }

        OnProjectileCompleted?.Invoke(completion);
        ReturnToBucket(projectile);
    }

    private void Prewarm(PowerupId powerupId)
    {
        if (projectileProfile == null) return;

        PoolBucket bucket = GetOrCreateBucket(powerupId);
        if (bucket == null || bucket.Prefab == null) return;

        int desiredCount = projectileProfile.GetPrewarmCount(powerupId);

        while (bucket.TotalCreated < desiredCount)
        {
            FakeArcProjectile actor = CreateActor(bucket);
            if (actor == null) break;
            bucket.Available.Push(actor);
        }
    }

    private PoolBucket GetOrCreateBucket(PowerupId powerupId)
    {
        if (!PowerupIdRules.RequiresProjectileAim(powerupId) ||
            projectileProfile == null)
        {
            return null;
        }

        if (buckets.TryGetValue(powerupId, out PoolBucket existing))
        {
            return existing;
        }

        PoolBucket created = new PoolBucket
        {
            PowerupId = powerupId,
            Prefab = projectileProfile.GetPrefab(powerupId)
        };

        buckets.Add(powerupId, created);
        return created;
    }

    private FakeArcProjectile CreateActor(PoolBucket bucket)
    {
        if (bucket == null || bucket.Prefab == null) return null;

        Transform parent = poolRoot != null ? poolRoot : transform;
        FakeArcProjectile actor = Instantiate(bucket.Prefab, parent);

        if (actor == null) return null;

        actor.name = $"{bucket.Prefab.name} (Pooled)";
        actor.InitializeForPool(this);

        bucket.TotalCreated++;
        totalCreatedActorCount++;
        actorBuckets[actor] = bucket;
        return actor;
    }

    private void ReturnToBucket(FakeArcProjectile projectile)
    {
        if (projectile == null ||
            !actorBuckets.TryGetValue(projectile, out PoolBucket bucket))
        {
            return;
        }

        if (!projectile.IsRentedFromPool) return;

        projectile.ResetForPool();
        bucket.Available.Push(projectile);
        rentedActorCount = Mathf.Max(0, rentedActorCount - 1);
    }
}
