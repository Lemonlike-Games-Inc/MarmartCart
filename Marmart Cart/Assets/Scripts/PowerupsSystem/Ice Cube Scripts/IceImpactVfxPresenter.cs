using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-level pooled particle presenter for successful Ice Freeze
/// applications. It listens to IceFrozen start/refresh events rather than raw
/// projectile completion, so a ground, wall, loose cart, checkout cart, or any
/// other non-effect impact cannot accidentally play the success VFX.
/// </summary>
[DisallowMultipleComponent]
public class IceImpactVfxPresenter : MonoBehaviour
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
    [SerializeField] private IcePresentationProfile presentationProfile;

    [Tooltip("Optional hierarchy parent for pooled Ice impact objects.")]
    [SerializeField] private Transform poolRoot;

    [Header("Diagnostics")]
    [SerializeField] private bool logPoolExhaustion = true;

    [Header("Runtime - Read Only")]
    [SerializeField] private int totalCreatedCount;
    [SerializeField] private int activeCount;
    [SerializeField] private int successfulFreezeEventCount;
    [SerializeField] private int droppedImpactCount;
    [SerializeField] private PowerupEffectEvent lastFreezeEvent;

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
    public int SuccessfulFreezeEventCount => successfulFreezeEventCount;
    public int DroppedImpactCount => droppedImpactCount;
    public PowerupEffectEvent LastFreezeEvent => lastFreezeEvent;

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

    private void HandleEffectApplied(PowerupEffectEvent effectEvent)
    {
        if (effectEvent.SourcePowerupId != PowerupId.IceCube ||
            effectEvent.EffectId != PowerupEffectId.IceFrozen)
        {
            return;
        }

        successfulFreezeEventCount++;
        lastFreezeEvent = effectEvent;

        EnsurePoolMatchesProfile();

        if (!TryRent(out ImpactInstance instance))
        {
            droppedImpactCount++;

            if (logPoolExhaustion && !poolExhaustionLogged)
            {
                poolExhaustionLogged = true;
                Debug.LogWarning(
                    "[IceImpactVfxPresenter] The successful-freeze impact " +
                    "VFX pool is exhausted. Increase Maximum Impact VFX " +
                    "Instances, use 0 for no explicit cap, or enable growth " +
                    "in IcePresentationProfile.",
                    this
                );
            }

            return;
        }

        poolExhaustionLogged = false;

        instance.Root.transform.SetPositionAndRotation(
            effectEvent.Position +
                presentationProfile.ImpactVfxPositionOffset,
            BuildImpactRotation(effectEvent)
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

    private Quaternion BuildImpactRotation(PowerupEffectEvent effectEvent)
    {
        Quaternion baseRotation = Quaternion.identity;

        if (presentationProfile.OrientImpactVfxToVictim &&
            TryResolveVictimTransform(effectEvent, out Transform victim))
        {
            baseRotation = victim.rotation;
        }

        return baseRotation * Quaternion.Euler(
            presentationProfile.ImpactVfxEulerOffset
        );
    }

    private static bool TryResolveVictimTransform(
        PowerupEffectEvent effectEvent,
        out Transform victim)
    {
        victim = null;

        if (effectEvent.TargetController == null) return false;

        CartControlScript cartControl =
            effectEvent.TargetController.CartControlInput;

        if (cartControl == null) return false;

        victim = cartControl.transform;
        return victim != null;
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

        if (!presentationProfile.AllowImpactVfxPoolGrowth)
        {
            return false;
        }

        int maximumCount =
            presentationProfile.MaximumImpactVfxInstances;

        if (maximumCount > 0 && totalCreatedCount >= maximumCount)
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
            $"{presentationProfile.ImpactVfxPrefab.name} " +
            "(Ice Successful Freeze Pooled)";

        ParticleSystem[] particleSystems =
            root.GetComponentsInChildren<ParticleSystem>(true);

        if (particleSystems == null || particleSystems.Length == 0)
        {
            Debug.LogError(
                "[IceImpactVfxPresenter] Impact VFX prefab must contain " +
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

        int targetCount = presentationProfile.ImpactVfxPrewarmCount;
        int maximumCount =
            presentationProfile.MaximumImpactVfxInstances;

        if (maximumCount > 0)
        {
            targetCount = Mathf.Min(targetCount, maximumCount);
        }

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
                "[IceImpactVfxPresenter] Assign an impact VFX prefab in " +
                "IcePresentationProfile to enable successful-freeze " +
                "particles.",
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

        if (subscribedLifecycleEventSystem == null) return;

        subscribedLifecycleEventSystem.OnEffectStarted +=
            HandleEffectApplied;
        subscribedLifecycleEventSystem.OnEffectRefreshed +=
            HandleEffectApplied;
    }

    private void Unsubscribe()
    {
        if (subscribedLifecycleEventSystem != null)
        {
            subscribedLifecycleEventSystem.OnEffectStarted -=
                HandleEffectApplied;
            subscribedLifecycleEventSystem.OnEffectRefreshed -=
                HandleEffectApplied;
        }

        subscribedLifecycleEventSystem = null;
    }
}
