using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Small scene-level pool for thrown items, one bucket per ThrowItemUse.
/// It prewarms every thrown item in the Item Library.
/// It is intentionally not a project-wide generic pooling framework.
/// </summary>
[DisallowMultipleComponent]
public class PowerupProjectilePool : MonoBehaviour
{
    private sealed class PoolBucket
    {
        public ThrowItemUse Use;
        public FakeArcProjectile Prefab;
        public readonly Stack<FakeArcProjectile> Available =
            new Stack<FakeArcProjectile>();
        public int TotalCreated;
    }

    [Header("References")]
    [SerializeField] private PowerupProjectileProfile projectileProfile;

    [Tooltip(
        "Optional explicit reference. Automatically resolved when left empty."
    )]
    [SerializeField] private PowerupLifecycleEventSystem lifecycleEventSystem;

    [Tooltip("Optional explicit reference (holds the Item Library). Automatically resolved when left empty.")]
    [SerializeField] private PowerupRuntimeSystem runtimeSystem;

    [Tooltip("Optional hierarchy parent for pooled actors. Defaults to this object.")]
    [SerializeField] private Transform poolRoot;

    [Header("Diagnostics")]
    [SerializeField] private bool logNeutralCompletions = true;

    [Header("Runtime - Read Only")]
    [SerializeField] private int totalCreatedActorCount;
    [SerializeField] private int rentedActorCount;

    private readonly Dictionary<ThrowItemUse, PoolBucket> buckets =
        new Dictionary<ThrowItemUse, PoolBucket>();

    private readonly Dictionary<FakeArcProjectile, PoolBucket> actorBuckets =
        new Dictionary<FakeArcProjectile, PoolBucket>();

    private readonly List<FakeArcProjectile> cancelBuffer =
        new List<FakeArcProjectile>();

    private PowerupRuntimeSystem subscribedRuntimeSystem;

    public PowerupProjectileProfile ProjectileProfile => projectileProfile;
    public int TotalCreatedActorCount => totalCreatedActorCount;
    public int RentedActorCount => rentedActorCount;

    public event Action<PowerupProjectileCompletion> OnProjectileCompleted;

    private void Awake()
    {
        ResolveLifecycleEventSystem();

        if (poolRoot == null) poolRoot = transform;

        PrewarmLibraryItems();
    }

    private void OnEnable()
    {
        ResolveRuntimeSystem();

        if (runtimeSystem != null && subscribedRuntimeSystem != runtimeSystem)
        {
            subscribedRuntimeSystem = runtimeSystem;
            subscribedRuntimeSystem.OnMatchPlayingChanged += HandleMatchPlayingChanged;
        }
    }

    private void OnDisable()
    {
        if (subscribedRuntimeSystem != null)
        {
            subscribedRuntimeSystem.OnMatchPlayingChanged -= HandleMatchPlayingChanged;
            subscribedRuntimeSystem = null;
        }
    }

    // Normal throws end within seconds, but an item that rolls until it hits
    // could outlive the match, so rolling items are cancelled when it ends.
    private void HandleMatchPlayingChanged(bool matchPlaying)
    {
        if (matchPlaying) return;

        cancelBuffer.Clear();

        foreach (FakeArcProjectile actor in actorBuckets.Keys)
        {
            if (actor != null && actor.ActiveRollingItem)
            {
                cancelBuffer.Add(actor);
            }
        }

        for (int i = 0; i < cancelBuffer.Count; i++)
        {
            cancelBuffer[i].Cancel();
        }

        if (cancelBuffer.Count > 0)
        {
            Debug.Log(
                $"[PowerupProjectilePool] Match ended: cancelled {cancelBuffer.Count} rolling projectile(s).",
                this
            );
        }

        cancelBuffer.Clear();
    }

    public bool CanProvide(ThrowItemUse use, int requiredCount)
    {
        if (requiredCount <= 0 || projectileProfile == null) return false;

        PoolBucket bucket = GetOrCreateBucket(use);
        if (bucket == null ||
            bucket.Prefab == null ||
            !bucket.Prefab.HasValidAuthoredHitbox)
        {
            return false;
        }

        return projectileProfile.AllowPoolGrowth ||
               bucket.Available.Count >= requiredCount;
    }

    public bool TryRent(
        ThrowItemUse use,
        out FakeArcProjectile projectile)
    {
        projectile = null;

        PoolBucket bucket = GetOrCreateBucket(use);
        if (bucket == null ||
            bucket.Prefab == null ||
            !bucket.Prefab.HasValidAuthoredHitbox)
        {
            return false;
        }

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
                $"{(completion.Item != null ? completion.Item.name : "no item")}[{completion.PatternEntryIndex}] " +
                $"completed: {completion.Reason}{targetText}.",
                this
            );
        }

        try
        {
            ResolveLifecycleEventSystem();
            lifecycleEventSystem?.PublishProjectileCompletion(completion);
            OnProjectileCompleted?.Invoke(completion);
        }
        finally
        {
            // Feedback/behavior subscriber failures must never strand an actor
            // outside its pool.
            ReturnToBucket(projectile);
        }
    }

    private void ResolveLifecycleEventSystem()
    {
        if (lifecycleEventSystem == null)
        {
            lifecycleEventSystem =
                FindFirstObjectByType<PowerupLifecycleEventSystem>();
        }
    }

    private void ResolveRuntimeSystem()
    {
        if (runtimeSystem == null)
        {
            runtimeSystem = FindFirstObjectByType<PowerupRuntimeSystem>();
        }
    }

    private void PrewarmLibraryItems()
    {
        ResolveRuntimeSystem();

        ItemLibrary library = runtimeSystem != null ? runtimeSystem.ItemLibrary : null;
        if (library == null) return;

        for (int i = 0; i < library.ItemCount; i++)
        {
            ItemDefinition item = library.GetItem(i);

            if (item != null && item.TryGetUse(out ThrowItemUse use))
            {
                Prewarm(use);
            }
        }
    }

    private void Prewarm(ThrowItemUse use)
    {
        if (projectileProfile == null) return;

        PoolBucket bucket = GetOrCreateBucket(use);
        if (bucket == null ||
            bucket.Prefab == null ||
            !bucket.Prefab.HasValidAuthoredHitbox)
        {
            return;
        }

        while (bucket.TotalCreated < use.PrewarmCount)
        {
            FakeArcProjectile actor = CreateActor(bucket);
            if (actor == null) break;
            bucket.Available.Push(actor);
        }
    }

    private PoolBucket GetOrCreateBucket(ThrowItemUse use)
    {
        if (use == null || projectileProfile == null)
        {
            return null;
        }

        if (buckets.TryGetValue(use, out PoolBucket existing))
        {
            return existing;
        }

        PoolBucket created = new PoolBucket
        {
            Use = use,
            Prefab = use.GetProjectilePrefab()
        };

        buckets.Add(use, created);
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
