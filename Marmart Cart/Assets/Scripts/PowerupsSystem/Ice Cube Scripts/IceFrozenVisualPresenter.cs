using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-level pooled presenter for the frozen mesh shown on a victim.
///
/// The visual is a standalone runtime object. It follows the GameObject that
/// owns the victim CartControlScript using TransformPoint/local rotation, so
/// no hidden mesh or extra presentation child is required on the leading-cart
/// prefab.
/// </summary>
[DisallowMultipleComponent]
public class IceFrozenVisualPresenter : MonoBehaviour
{
    private sealed class FrozenVisualInstance
    {
        public GameObject Root;
        public IceVisualFadeController FadeController;
        public Vector3 AuthoredLocalScale;
        public Transform VictimTransform;
        public int TargetPlayerIndex;
        public uint EffectInstanceId;
    }

    [Header("References")]
    [SerializeField] private PowerupLifecycleEventSystem lifecycleEventSystem;
    [SerializeField] private IceFreezeEffectSystem freezeEffectSystem;
    [SerializeField] private IcePresentationProfile presentationProfile;

    [Tooltip("Optional hierarchy parent for pooled victim visuals.")]
    [SerializeField] private Transform poolRoot;

    [Header("Diagnostics")]
    [SerializeField] private bool logPoolExhaustion = true;

    [Header("Runtime - Read Only")]
    [SerializeField] private int totalCreatedCount;
    [SerializeField] private int activeVisualCount;
    [SerializeField] private int fadingVisualCount;
    [SerializeField] private int droppedVisualCount;

    private readonly Stack<FrozenVisualInstance> available =
        new Stack<FrozenVisualInstance>();
    private readonly List<FrozenVisualInstance> active =
        new List<FrozenVisualInstance>();
    private readonly List<FrozenVisualInstance> fading =
        new List<FrozenVisualInstance>();

    private PowerupLifecycleEventSystem subscribedLifecycleEventSystem;
    private GameObject pooledPrefab;
    private bool invalidPrefabLogged;
    private bool invalidMaterialLogged;
    private bool poolExhaustionLogged;

    public int TotalCreatedCount => totalCreatedCount;
    public int ActiveVisualCount => activeVisualCount;
    public int FadingVisualCount => fadingVisualCount;
    public int DroppedVisualCount => droppedVisualCount;

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
        for (int i = 0; i < active.Count; i++)
        {
            UpdatePlacement(active[i]);
        }

        for (int i = 0; i < fading.Count; i++)
        {
            UpdatePlacement(fading[i]);
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
        ReturnAllInstances();
    }

    private void HandleEffectApplied(PowerupEffectEvent effectEvent)
    {
        if (!IsFrozenEffect(effectEvent)) return;

        EnsurePoolMatchesProfile();

        if (!TryResolveVictimTransform(
                effectEvent,
                out Transform victimTransform))
        {
            return;
        }

        FrozenVisualInstance instance =
            FindByTarget(active, effectEvent.TargetPlayerIndex);

        if (instance == null)
        {
            instance = FindByTarget(
                fading,
                effectEvent.TargetPlayerIndex
            );

            if (instance != null)
            {
                fading.Remove(instance);
            }
        }

        if (instance == null && !TryRent(out instance))
        {
            droppedVisualCount++;

            if (logPoolExhaustion && !poolExhaustionLogged)
            {
                poolExhaustionLogged = true;
                Debug.LogWarning(
                    "[IceFrozenVisualPresenter] The victim visual pool is " +
                    "exhausted. Increase Maximum Frozen Visual Instances, " +
                    "use 0 for no explicit cap, or enable growth in " +
                    "IcePresentationProfile.",
                    this
                );
            }

            return;
        }

        poolExhaustionLogged = false;
        instance.VictimTransform = victimTransform;
        instance.TargetPlayerIndex = effectEvent.TargetPlayerIndex;
        instance.EffectInstanceId = effectEvent.EffectInstanceId;

        if (!instance.FadeController.PrepareForUse(
                presentationProfile.VisualStartAlphaByte) &&
            !invalidMaterialLogged)
        {
            invalidMaterialLogged = true;
            Debug.LogWarning(
                "[IceFrozenVisualPresenter] The frozen victim prefab has " +
                "no resolvable Renderer material with _BaseColor or _Color. " +
                "It can still be positioned, but cannot use the authored " +
                "Ice fade.",
                instance.Root
            );
        }

        UpdatePlacement(instance);
        instance.Root.SetActive(true);

        if (!active.Contains(instance))
        {
            active.Add(instance);
        }

        UpdateRuntimeCounts();
    }

    private void HandleEffectEnded(PowerupEffectEvent effectEvent)
    {
        if (!IsFrozenEffect(effectEvent)) return;

        FrozenVisualInstance instance = FindByEffectInstance(
            active,
            effectEvent.EffectInstanceId
        );

        if (instance == null)
        {
            instance = FindByTarget(
                active,
                effectEvent.TargetPlayerIndex
            );
        }

        if (instance == null) return;

        active.Remove(instance);
        fading.Add(instance);
        UpdateRuntimeCounts();

        float fadeDuration = presentationProfile != null
            ? presentationProfile.VisualFadeOutSeconds
            : 0f;

        instance.FadeController.BeginFade(
            fadeDuration,
            () => CompleteFade(instance)
        );
    }

    private void CompleteFade(FrozenVisualInstance instance)
    {
        if (instance == null) return;

        fading.Remove(instance);
        active.Remove(instance);
        ReturnInstance(instance);
        UpdateRuntimeCounts();
    }

    private void UpdatePlacement(FrozenVisualInstance instance)
    {
        if (instance == null ||
            instance.Root == null ||
            instance.VictimTransform == null ||
            presentationProfile == null)
        {
            return;
        }

        Transform victim = instance.VictimTransform;
        Transform visual = instance.Root.transform;

        visual.SetPositionAndRotation(
            victim.TransformPoint(
                presentationProfile.FrozenVictimLocalPosition
            ),
            victim.rotation * Quaternion.Euler(
                presentationProfile.FrozenVictimLocalEulerAngles
            )
        );

        Vector3 desiredWorldScale = Vector3.Scale(
            instance.AuthoredLocalScale,
            presentationProfile.FrozenVictimScaleMultiplier
        );

        if (presentationProfile.InheritVictimWorldScale)
        {
            Vector3 victimScale = victim.lossyScale;
            desiredWorldScale = Vector3.Scale(
                desiredWorldScale,
                new Vector3(
                    Mathf.Abs(victimScale.x),
                    Mathf.Abs(victimScale.y),
                    Mathf.Abs(victimScale.z)
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
            desiredWorldScale.x / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            desiredWorldScale.y / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
            desiredWorldScale.z / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z))
        );
    }

    private bool TryResolveVictimTransform(
        PowerupEffectEvent effectEvent,
        out Transform victimTransform)
    {
        victimTransform = null;
        CartControlScript cartControl = null;

        if (effectEvent.TargetController != null)
        {
            cartControl = effectEvent.TargetController.CartControlInput;
        }

        if (cartControl == null &&
            freezeEffectSystem != null &&
            freezeEffectSystem.TryGetState(
                effectEvent.TargetPlayerIndex,
                out IceFreezeState state))
        {
            cartControl = state.TargetCartControl;
        }

        if (cartControl == null) return false;

        victimTransform = cartControl.transform;
        return victimTransform != null;
    }

    private static bool IsFrozenEffect(PowerupEffectEvent effectEvent)
    {
        return effectEvent.SourcePowerupId == PowerupId.IceCube &&
               effectEvent.EffectId == PowerupEffectId.IceFrozen;
    }

    private bool TryRent(out FrozenVisualInstance instance)
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

        if (!presentationProfile.AllowFrozenVisualPoolGrowth)
        {
            return false;
        }

        int maximumCount =
            presentationProfile.MaximumFrozenVisualInstances;

        if (maximumCount > 0 && totalCreatedCount >= maximumCount)
        {
            return false;
        }

        instance = CreateInstance();
        return instance != null;
    }

    private FrozenVisualInstance CreateInstance()
    {
        if (!HasUsablePrefab()) return null;

        EnsurePoolRoot();

        GameObject root = Instantiate(
            presentationProfile.FrozenVictimVisualPrefab,
            poolRoot
        );

        if (root == null) return null;

        root.name =
            $"{presentationProfile.FrozenVictimVisualPrefab.name} " +
            "(Ice Victim Pooled)";

        IceVisualFadeController fadeController =
            root.GetComponent<IceVisualFadeController>();

        if (fadeController == null)
        {
            fadeController = root.AddComponent<IceVisualFadeController>();
        }

        Vector3 authoredScale = root.transform.localScale;
        fadeController.ResetForPool(
            presentationProfile.VisualStartAlphaByte
        );
        root.SetActive(false);
        totalCreatedCount++;

        return new FrozenVisualInstance
        {
            Root = root,
            FadeController = fadeController,
            AuthoredLocalScale = authoredScale
        };
    }

    private void ReturnInstance(FrozenVisualInstance instance)
    {
        if (instance == null || instance.Root == null) return;

        int startAlpha = presentationProfile != null
            ? presentationProfile.VisualStartAlphaByte
            : 175;

        instance.FadeController.ResetForPool(startAlpha);
        instance.VictimTransform = null;
        instance.TargetPlayerIndex = 0;
        instance.EffectInstanceId = 0u;
        instance.Root.transform.SetParent(poolRoot, false);
        instance.Root.transform.localScale = instance.AuthoredLocalScale;
        instance.Root.SetActive(false);
        available.Push(instance);
    }

    private void ReturnAllInstances()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            ReturnInstance(active[i]);
        }

        for (int i = fading.Count - 1; i >= 0; i--)
        {
            ReturnInstance(fading[i]);
        }

        active.Clear();
        fading.Clear();
        UpdateRuntimeCounts();
    }

    private void Prewarm()
    {
        if (!HasUsablePrefab()) return;

        int targetCount = presentationProfile.FrozenVisualPrewarmCount;
        int maximumCount =
            presentationProfile.MaximumFrozenVisualInstances;

        if (maximumCount > 0)
        {
            targetCount = Mathf.Min(targetCount, maximumCount);
        }

        while (totalCreatedCount < targetCount)
        {
            FrozenVisualInstance instance = CreateInstance();
            if (instance == null) break;
            available.Push(instance);
        }
    }

    private bool HasUsablePrefab()
    {
        if (presentationProfile != null &&
            presentationProfile.FrozenVictimVisualPrefab != null &&
            !invalidPrefabLogged)
        {
            return true;
        }

        if (!invalidPrefabLogged)
        {
            invalidPrefabLogged = true;
            Debug.LogWarning(
                "[IceFrozenVisualPresenter] Assign a standalone frozen " +
                "victim prefab in IcePresentationProfile.",
                this
            );
        }

        return false;
    }

    private void EnsurePoolMatchesProfile()
    {
        GameObject requestedPrefab = presentationProfile != null
            ? presentationProfile.FrozenVictimVisualPrefab
            : null;

        if (requestedPrefab == pooledPrefab) return;

        ReturnAllInstances();

        while (available.Count > 0)
        {
            FrozenVisualInstance instance = available.Pop();
            if (instance != null && instance.Root != null)
            {
                Destroy(instance.Root);
            }
        }

        pooledPrefab = requestedPrefab;
        totalCreatedCount = 0;
        invalidPrefabLogged = false;
        invalidMaterialLogged = false;
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

        if (freezeEffectSystem == null)
        {
            freezeEffectSystem =
                FindFirstObjectByType<IceFreezeEffectSystem>();
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

    private static FrozenVisualInstance FindByTarget(
        List<FrozenVisualInstance> collection,
        int targetPlayerIndex)
    {
        for (int i = 0; i < collection.Count; i++)
        {
            FrozenVisualInstance instance = collection[i];
            if (instance != null &&
                instance.TargetPlayerIndex == targetPlayerIndex)
            {
                return instance;
            }
        }

        return null;
    }

    private static FrozenVisualInstance FindByEffectInstance(
        List<FrozenVisualInstance> collection,
        uint effectInstanceId)
    {
        for (int i = 0; i < collection.Count; i++)
        {
            FrozenVisualInstance instance = collection[i];
            if (instance != null &&
                instance.EffectInstanceId == effectInstanceId)
            {
                return instance;
            }
        }

        return null;
    }

    private void UpdateRuntimeCounts()
    {
        activeVisualCount = active.Count;
        fadingVisualCount = fading.Count;
    }
}
