using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presentation adapter that spawns a separately pooled particle prefab at
/// every physical Tomato completion point.
///
/// The VFX is deliberately not parented to FakeArcProjectile: the projectile
/// returns to its own pool in the same completion call, while this effect must
/// remain alive until all of its particles finish.
/// </summary>
[DisallowMultipleComponent]
public class TomatoImpactVfxPresenter : MonoBehaviour
{
    private sealed class ImpactInstance
    {
        public GameObject Root;
        public ParticleSystem[] ParticleSystems;
        public float StartedAtTime;
        public int StartedFrame;
    }

    [Header("References")]
    [SerializeField] private PowerupLifecycleEventSystem lifecycleEventSystem;
    [SerializeField] private TomatoPresentationProfile presentationProfile;

    [Tooltip("Optional hierarchy parent for pooled impact objects.")]
    [SerializeField] private Transform poolRoot;

    [Header("Diagnostics")]
    [SerializeField] private bool logPoolExhaustion = true;

    [Header("Runtime - Read Only")]
    [SerializeField] private int totalCreatedCount;
    [SerializeField] private int activeCount;
    [SerializeField] private int impactEventCount;
    [SerializeField] private int droppedImpactCount;
    [SerializeField] private PowerupProjectileEvent lastImpactEvent;

    private readonly Stack<ImpactInstance> available =
        new Stack<ImpactInstance>();

    private readonly List<ImpactInstance> active =
        new List<ImpactInstance>();

    private PowerupLifecycleEventSystem subscribedLifecycleEventSystem;
    private GameObject pooledPrefab;
    private bool invalidPrefabLogged;
    private bool poolExhaustionLogged;

    public int TotalCreatedCount => totalCreatedCount;
    public int ActiveCount => activeCount;
    public int ImpactEventCount => impactEventCount;
    public int DroppedImpactCount => droppedImpactCount;
    public PowerupProjectileEvent LastImpactEvent => lastImpactEvent;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        EnsurePoolRoot();
        EnsurePoolMatchesProfile();
        Prewarm();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureSubscriptions();
        EnsurePoolRoot();
        EnsurePoolMatchesProfile();
        Prewarm();
    }

    private void Start()
    {
        ResolveReferences();
        EnsureSubscriptions();
    }

    private void Update()
    {
        if (lifecycleEventSystem == null)
        {
            ResolveReferences();
            EnsureSubscriptions();
        }

        UpdateActiveInstances();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ReturnAllActiveInstances();
    }

    private void HandleProjectileImpacted(
        PowerupProjectileEvent projectileEvent)
    {
        if (projectileEvent.Completion.PowerupId != PowerupId.Tomato)
        {
            return;
        }

        impactEventCount++;
        lastImpactEvent = projectileEvent;

        EnsurePoolMatchesProfile();

        if (!TryRent(out ImpactInstance instance))
        {
            droppedImpactCount++;

            if (logPoolExhaustion && !poolExhaustionLogged)
            {
                poolExhaustionLogged = true;
                Debug.LogWarning(
                    "[TomatoImpactVfxPresenter] The Tomato impact VFX pool " +
                    "is exhausted. Increase Maximum Impact VFX Instances or " +
                    "enable pool growth in TomatoPresentationProfile.",
                    this
                );
            }

            return;
        }

        poolExhaustionLogged = false;

        PowerupProjectileCompletion completion =
            projectileEvent.Completion;

        Vector3 surfaceNormal =
            completion.SurfaceNormal.sqrMagnitude > 0.000001f
                ? completion.SurfaceNormal.normalized
                : Vector3.up;

        instance.Root.transform.SetPositionAndRotation(
            completion.Position +
                surfaceNormal * presentationProfile.ImpactSurfaceOffset,
            BuildImpactRotation(surfaceNormal)
        );

        instance.Root.SetActive(true);

        for (int i = 0; i < instance.ParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = instance.ParticleSystems[i];
            if (particleSystem == null) continue;

            particleSystem.Stop(
                false,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
            particleSystem.Play(false);
        }

        instance.StartedAtTime = Time.time;
        instance.StartedFrame = Time.frameCount;
        active.Add(instance);
        activeCount = active.Count;
    }

    private Quaternion BuildImpactRotation(Vector3 surfaceNormal)
    {
        Quaternion authoredOffset = Quaternion.Euler(
            presentationProfile.ImpactVfxEulerOffset
        );

        if (!presentationProfile.AlignImpactVfxUpToSurfaceNormal)
        {
            return authoredOffset;
        }

        return Quaternion.FromToRotation(Vector3.up, surfaceNormal) *
               authoredOffset;
    }

    private void UpdateActiveInstances()
    {
        if (active.Count == 0) return;

        float maximumLifetime = presentationProfile != null
            ? presentationProfile.MaximumImpactVfxLifetimeSeconds
            : 0.1f;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            ImpactInstance instance = active[i];
            bool anyAlive = false;

            for (int j = 0; j < instance.ParticleSystems.Length; j++)
            {
                ParticleSystem particleSystem =
                    instance.ParticleSystems[j];

                if (particleSystem != null &&
                    particleSystem.IsAlive(false))
                {
                    anyAlive = true;
                    break;
                }
            }

            bool passedFirstFrame = Time.frameCount > instance.StartedFrame;
            bool exceededSafetyLifetime =
                Time.time - instance.StartedAtTime >= maximumLifetime;

            if ((!anyAlive && passedFirstFrame) || exceededSafetyLifetime)
            {
                active.RemoveAt(i);
                ReturnInstance(instance);
            }
        }

        activeCount = active.Count;
    }

    private bool TryRent(out ImpactInstance instance)
    {
        instance = null;

        if (!HasUsablePrefab()) return false;

        while (available.Count > 0 && instance == null)
        {
            instance = available.Pop();

            if (instance == null || instance.Root == null)
            {
                instance = null;
            }
        }

        if (instance != null) return true;

        if (!presentationProfile.AllowImpactVfxPoolGrowth ||
            totalCreatedCount >=
                presentationProfile.MaximumImpactVfxInstances)
        {
            return false;
        }

        instance = CreateInstance();
        return instance != null;
    }

    private ImpactInstance CreateInstance()
    {
        if (!HasUsablePrefab()) return null;

        EnsurePoolRoot();

        GameObject root = Instantiate(
            presentationProfile.ImpactVfxPrefab,
            poolRoot
        );

        if (root == null) return null;

        root.name =
            $"{presentationProfile.ImpactVfxPrefab.name} (Tomato Impact Pooled)";

        ParticleSystem[] particleSystems =
            root.GetComponentsInChildren<ParticleSystem>(true);

        if (particleSystems == null || particleSystems.Length == 0)
        {
            Debug.LogError(
                "[TomatoImpactVfxPresenter] Impact VFX prefab must contain " +
                "at least one ParticleSystem on its root or children.",
                presentationProfile.ImpactVfxPrefab
            );

            Destroy(root);
            invalidPrefabLogged = true;
            return null;
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            if (particleSystems[i] == null) continue;

            particleSystems[i].Stop(
                false,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }

        root.SetActive(false);
        totalCreatedCount++;

        return new ImpactInstance
        {
            Root = root,
            ParticleSystems = particleSystems
        };
    }

    private void ReturnInstance(ImpactInstance instance)
    {
        if (instance == null || instance.Root == null) return;

        for (int i = 0; i < instance.ParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = instance.ParticleSystems[i];
            if (particleSystem == null) continue;

            particleSystem.Stop(
                false,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }

        instance.Root.SetActive(false);
        instance.Root.transform.SetParent(poolRoot, false);
        available.Push(instance);
    }

    private void ReturnAllActiveInstances()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            ReturnInstance(active[i]);
        }

        active.Clear();
        activeCount = 0;
    }

    private void Prewarm()
    {
        if (!HasUsablePrefab()) return;

        int targetCount = Mathf.Min(
            presentationProfile.ImpactVfxPrewarmCount,
            presentationProfile.MaximumImpactVfxInstances
        );

        while (totalCreatedCount < targetCount)
        {
            ImpactInstance instance = CreateInstance();
            if (instance == null) break;
            available.Push(instance);
        }
    }

    private bool HasUsablePrefab()
    {
        if (presentationProfile != null &&
            presentationProfile.ImpactVfxPrefab != null &&
            !invalidPrefabLogged)
        {
            return true;
        }

        if (!invalidPrefabLogged)
        {
            invalidPrefabLogged = true;
            Debug.LogWarning(
                "[TomatoImpactVfxPresenter] Assign an impact VFX prefab in " +
                "TomatoPresentationProfile to enable Tomato impact particles.",
                this
            );
        }

        return false;
    }

    private void EnsurePoolMatchesProfile()
    {
        GameObject requestedPrefab = presentationProfile != null
            ? presentationProfile.ImpactVfxPrefab
            : null;

        if (requestedPrefab == pooledPrefab) return;

        ReturnAllActiveInstances();

        while (available.Count > 0)
        {
            ImpactInstance instance = available.Pop();
            if (instance != null && instance.Root != null)
            {
                Destroy(instance.Root);
            }
        }

        pooledPrefab = requestedPrefab;
        totalCreatedCount = 0;
        invalidPrefabLogged = false;
        poolExhaustionLogged = false;
    }

    private void EnsurePoolRoot()
    {
        if (poolRoot == null) poolRoot = transform;
    }

    private void ResolveReferences()
    {
        if (lifecycleEventSystem == null)
        {
            lifecycleEventSystem =
                FindFirstObjectByType<PowerupLifecycleEventSystem>();
        }
    }

    private void EnsureSubscriptions()
    {
        if (subscribedLifecycleEventSystem == lifecycleEventSystem) return;

        Unsubscribe();
        subscribedLifecycleEventSystem = lifecycleEventSystem;

        if (subscribedLifecycleEventSystem != null)
        {
            subscribedLifecycleEventSystem.OnProjectileImpacted +=
                HandleProjectileImpacted;
        }
    }

    private void Unsubscribe()
    {
        if (subscribedLifecycleEventSystem != null)
        {
            subscribedLifecycleEventSystem.OnProjectileImpacted -=
                HandleProjectileImpacted;
        }

        subscribedLifecycleEventSystem = null;
    }
}
