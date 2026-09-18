using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-level pooled presentation for ColaMentosBoost.
///
/// Each active object follows the transform that owns CartControlScript. All
/// ParticleSystems are explicitly cleared/replayed on rent and cleared on
/// return, making looping VFX deterministic across pool reuse regardless of
/// the prefab's Play On Awake setting.
/// </summary>
[DisallowMultipleComponent]
public class ColaMentosCartVisualPresenter : MonoBehaviour
{
    private sealed class VisualInstance
    {
        public GameObject Root;
        public ParticleSystem[] ParticleSystems;
        public Vector3 AuthoredLocalScale;
        public Transform CartTransform;
        public int PlayerIndex;
        public uint EffectInstanceId;
    }

    [Header("References")]
    [SerializeField] private PowerupLifecycleEventSystem lifecycleEventSystem;
    [SerializeField] private ColaMentosEffectSystem effectSystem;
    [SerializeField] private ColaMentosPresentationProfile presentationProfile;

    [Tooltip("Optional hierarchy parent for pooled Cola attachment objects.")]
    [SerializeField] private Transform poolRoot;

    [Header("Diagnostics")]
    [SerializeField] private bool logPoolExhaustion = true;

    [Header("Runtime - Read Only")]
    [SerializeField] private int totalCreatedCount;
    [SerializeField] private int activeVisualCount;
    [SerializeField] private int droppedVisualCount;
    [SerializeField] private PowerupEffectEvent lastEffectEvent;

    private readonly Stack<VisualInstance> available =
        new Stack<VisualInstance>();
    private readonly List<VisualInstance> active =
        new List<VisualInstance>();

    private PowerupLifecycleEventSystem subscribedLifecycleEventSystem;
    private GameObject pooledPrefab;
    private bool invalidPrefabLogged;
    private bool poolExhaustionLogged;

    public int TotalCreatedCount => totalCreatedCount;
    public int ActiveVisualCount => activeVisualCount;
    public int DroppedVisualCount => droppedVisualCount;
    public PowerupEffectEvent LastEffectEvent => lastEffectEvent;

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
        if (lifecycleEventSystem != null) return;

        ResolveReferences();
        EnsureSubscriptions();
    }

    private void LateUpdate()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            VisualInstance instance = active[i];

            if (instance == null ||
                instance.Root == null ||
                instance.CartTransform == null)
            {
                active.RemoveAt(i);
                ReturnInstance(instance);
                continue;
            }

            UpdatePlacement(instance);
        }

        activeVisualCount = active.Count;
    }

    private void OnDisable()
    {
        Unsubscribe();
        ReturnAllActiveInstances();
    }

    private void HandleEffectApplied(PowerupEffectEvent effectEvent)
    {
        if (!IsColaEffect(effectEvent)) return;

        lastEffectEvent = effectEvent;
        EnsurePoolMatchesProfile();

        if (!TryResolveCartTransform(
                effectEvent,
                out Transform cartTransform))
        {
            return;
        }

        VisualInstance instance = FindByPlayer(
            effectEvent.TargetPlayerIndex
        );
        bool newlyRented = instance == null;

        if (newlyRented && !TryRent(out instance))
        {
            droppedVisualCount++;

            if (logPoolExhaustion && !poolExhaustionLogged)
            {
                poolExhaustionLogged = true;
                Debug.LogWarning(
                    "[ColaMentosCartVisualPresenter] The Cola cart-visual " +
                    "pool is exhausted. Increase Maximum Instances, use 0 " +
                    "for no explicit cap, or enable growth in " +
                    "ColaMentosPresentationProfile.",
                    this
                );
            }

            return;
        }

        poolExhaustionLogged = false;
        instance.CartTransform = cartTransform;
        instance.PlayerIndex = effectEvent.TargetPlayerIndex;
        instance.EffectInstanceId = effectEvent.EffectInstanceId;

        UpdatePlacement(instance);
        instance.Root.SetActive(true);

        if (newlyRented)
        {
            active.Add(instance);
        }

        if (newlyRented ||
            presentationProfile.RestartParticlesOnRefresh)
        {
            RestartParticleSystems(instance);
        }

        activeVisualCount = active.Count;
    }

    private void HandleEffectEnded(PowerupEffectEvent effectEvent)
    {
        if (!IsColaEffect(effectEvent)) return;

        lastEffectEvent = effectEvent;
        VisualInstance instance = FindByEffectInstance(
            effectEvent.EffectInstanceId
        );

        if (instance == null)
        {
            instance = FindByPlayer(effectEvent.TargetPlayerIndex);
        }

        if (instance == null) return;

        active.Remove(instance);
        ReturnInstance(instance);
        activeVisualCount = active.Count;
    }

    private void UpdatePlacement(VisualInstance instance)
    {
        if (instance == null ||
            instance.Root == null ||
            instance.CartTransform == null ||
            presentationProfile == null)
        {
            return;
        }

        Transform cart = instance.CartTransform;
        Transform visual = instance.Root.transform;

        visual.SetPositionAndRotation(
            cart.TransformPoint(presentationProfile.LocalPosition),
            cart.rotation * Quaternion.Euler(
                presentationProfile.LocalEulerAngles
            )
        );

        Vector3 desiredWorldScale = Vector3.Scale(
            instance.AuthoredLocalScale,
            presentationProfile.ScaleMultiplier
        );

        if (presentationProfile.InheritCartWorldScale)
        {
            Vector3 cartScale = cart.lossyScale;
            desiredWorldScale = Vector3.Scale(
                desiredWorldScale,
                new Vector3(
                    Mathf.Abs(cartScale.x),
                    Mathf.Abs(cartScale.y),
                    Mathf.Abs(cartScale.z)
                )
            );
        }

        visual.localScale = DivideByParentScale(
            desiredWorldScale,
            visual.parent
        );
    }

    private static Vector3 DivideByParentScale(
        Vector3 desiredWorldScale,
        Transform parent)
    {
        if (parent == null) return desiredWorldScale;

        Vector3 parentScale = parent.lossyScale;
        return new Vector3(
            desiredWorldScale.x /
                Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            desiredWorldScale.y /
                Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
            desiredWorldScale.z /
                Mathf.Max(0.0001f, Mathf.Abs(parentScale.z))
        );
    }

    private bool TryResolveCartTransform(
        PowerupEffectEvent effectEvent,
        out Transform cartTransform)
    {
        cartTransform = null;
        PlayerPowerupController controller =
            effectEvent.TargetController != null
                ? effectEvent.TargetController
                : effectEvent.OwnerController;

        CartControlScript cartControl = controller != null
            ? controller.CartControlInput
            : null;

        if (cartControl == null &&
            effectSystem != null &&
            effectSystem.TryGetState(
                effectEvent.TargetPlayerIndex,
                out ColaMentosBoostState state))
        {
            cartControl = state.CartControl;
        }

        if (cartControl == null) return false;

        cartTransform = cartControl.transform;
        return cartTransform != null;
    }

    private static bool IsColaEffect(PowerupEffectEvent effectEvent)
    {
        return effectEvent.SourcePowerupId == PowerupId.ColaMentos &&
               effectEvent.EffectId == PowerupEffectId.ColaMentosBoost;
    }

    private bool TryRent(out VisualInstance instance)
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

        if (!presentationProfile.AllowPoolGrowth)
        {
            return false;
        }

        int maximumCount = presentationProfile.MaximumInstances;

        if (maximumCount > 0 && totalCreatedCount >= maximumCount)
        {
            return false;
        }

        instance = CreateInstance();
        return instance != null;
    }

    private VisualInstance CreateInstance()
    {
        if (!HasUsablePrefab()) return null;

        EnsurePoolRoot();

        GameObject root = Instantiate(
            presentationProfile.CartVisualPrefab,
            poolRoot
        );

        if (root == null) return null;

        root.name =
            $"{presentationProfile.CartVisualPrefab.name} " +
            "(Cola Mentos Cart Visual Pooled)";

        ParticleSystem[] particleSystems =
            root.GetComponentsInChildren<ParticleSystem>(true);

        for (int i = 0; i < particleSystems.Length; i++)
        {
            if (particleSystems[i] == null) continue;

            particleSystems[i].Stop(
                false,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }

        Vector3 authoredScale = root.transform.localScale;
        root.SetActive(false);
        totalCreatedCount++;

        return new VisualInstance
        {
            Root = root,
            ParticleSystems = particleSystems,
            AuthoredLocalScale = authoredScale
        };
    }

    private static void RestartParticleSystems(VisualInstance instance)
    {
        if (instance == null || instance.ParticleSystems == null) return;

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
    }

    private void ReturnInstance(VisualInstance instance)
    {
        if (instance == null || instance.Root == null) return;

        if (instance.ParticleSystems != null)
        {
            for (int i = 0; i < instance.ParticleSystems.Length; i++)
            {
                ParticleSystem particleSystem =
                    instance.ParticleSystems[i];

                if (particleSystem == null) continue;

                particleSystem.Stop(
                    false,
                    ParticleSystemStopBehavior.StopEmittingAndClear
                );
            }
        }

        instance.CartTransform = null;
        instance.PlayerIndex = 0;
        instance.EffectInstanceId = 0u;
        instance.Root.transform.SetParent(poolRoot, false);
        instance.Root.transform.localScale = instance.AuthoredLocalScale;
        instance.Root.SetActive(false);
        available.Push(instance);
    }

    private void ReturnAllActiveInstances()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            ReturnInstance(active[i]);
        }

        active.Clear();
        activeVisualCount = 0;
    }

    private void Prewarm()
    {
        if (!HasUsablePrefab()) return;

        int targetCount = presentationProfile.PrewarmCount;
        int maximumCount = presentationProfile.MaximumInstances;

        if (maximumCount > 0)
        {
            targetCount = Mathf.Min(targetCount, maximumCount);
        }

        while (totalCreatedCount < targetCount)
        {
            VisualInstance instance = CreateInstance();
            if (instance == null) break;
            available.Push(instance);
        }
    }

    private bool HasUsablePrefab()
    {
        if (presentationProfile != null &&
            presentationProfile.CartVisualPrefab != null &&
            !invalidPrefabLogged)
        {
            return true;
        }

        if (!invalidPrefabLogged)
        {
            invalidPrefabLogged = true;
            Debug.LogWarning(
                "[ColaMentosCartVisualPresenter] Assign a standalone cart " +
                "mesh/VFX prefab in ColaMentosPresentationProfile.",
                this
            );
        }

        return false;
    }

    private void EnsurePoolMatchesProfile()
    {
        GameObject requestedPrefab = presentationProfile != null
            ? presentationProfile.CartVisualPrefab
            : null;

        if (requestedPrefab == pooledPrefab) return;

        ReturnAllActiveInstances();

        while (available.Count > 0)
        {
            VisualInstance instance = available.Pop();

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

    private VisualInstance FindByPlayer(int playerIndex)
    {
        for (int i = 0; i < active.Count; i++)
        {
            VisualInstance instance = active[i];

            if (instance != null && instance.PlayerIndex == playerIndex)
            {
                return instance;
            }
        }

        return null;
    }

    private VisualInstance FindByEffectInstance(uint effectInstanceId)
    {
        for (int i = 0; i < active.Count; i++)
        {
            VisualInstance instance = active[i];

            if (instance != null &&
                instance.EffectInstanceId == effectInstanceId)
            {
                return instance;
            }
        }

        return null;
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

        if (effectSystem == null)
        {
            effectSystem = FindFirstObjectByType<ColaMentosEffectSystem>();
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
        subscribedLifecycleEventSystem.OnEffectEnded +=
            HandleEffectEnded;
    }

    private void Unsubscribe()
    {
        if (subscribedLifecycleEventSystem != null)
        {
            subscribedLifecycleEventSystem.OnEffectStarted -=
                HandleEffectApplied;
            subscribedLifecycleEventSystem.OnEffectRefreshed -=
                HandleEffectApplied;
            subscribedLifecycleEventSystem.OnEffectEnded -=
                HandleEffectEnded;
        }

        subscribedLifecycleEventSystem = null;
    }
}
