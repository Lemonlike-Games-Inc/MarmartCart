using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

/// <summary>
/// Optional Inspector-facing bridge for precise lifecycle timing.
///
/// Code systems should normally subscribe to PowerupLifecycleEventSystem's
/// typed C# events. This relay is convenient for one-shot SFX, Animator
/// triggers, Timeline hooks, or other parameterless Inspector callbacks. The
/// complete typed payload remains available through the Last... properties.
/// </summary>
[DisallowMultipleComponent]
public class PowerupLifecycleUnityEventRelay : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private PowerupLifecycleEventSystem lifecycleEventSystem;

    [Header("Filters")]
    [FormerlySerializedAs("filterByPowerupId")]
    [SerializeField] private bool filterByItem = true;

    [Tooltip("Only events of this item and of the Also Accept Items.")]
    [SerializeField] private ItemDefinition item;

    [Tooltip(
        "More items that use these callbacks, such as a variant that should " +
        "sound like its base item. Give a variant its own relay instead when " +
        "it needs different callbacks."
    )]
    [SerializeField] private ItemDefinition[] alsoAcceptItems = new ItemDefinition[0];

    [SerializeField] private bool filterEffectsByEffectId;
    [SerializeField]
    private PowerupEffectId effectId =
        PowerupEffectId.TomatoBlind;

    [Header("Activation")]
    [SerializeField] private UnityEvent onActivated = new UnityEvent();

    [Header("Projectile Completion")]
    [SerializeField] private UnityEvent onProjectileImpacted = new UnityEvent();
    [SerializeField] private UnityEvent onProjectileCancelled = new UnityEvent();

    [Header("Gameplay Effect")]
    [SerializeField] private UnityEvent onEffectStarted = new UnityEvent();
    [SerializeField] private UnityEvent onEffectRefreshed = new UnityEvent();
    [SerializeField] private UnityEvent onEffectEnded = new UnityEvent();

    [Header("Runtime - Read Only")]
    [SerializeField] private PowerupActivationEvent lastActivation;
    [SerializeField] private PowerupProjectileEvent lastProjectileEvent;
    [SerializeField] private PowerupEffectEvent lastEffectEvent;

    private PowerupLifecycleEventSystem subscribedLifecycleEventSystem;

    public PowerupActivationEvent LastActivation => lastActivation;
    public PowerupProjectileEvent LastProjectileEvent => lastProjectileEvent;
    public PowerupEffectEvent LastEffectEvent => lastEffectEvent;

    private void Reset()
    {
        ResolveReference();
    }

    private void Awake()
    {
        ResolveReference();
    }

    private void OnEnable()
    {
        ResolveReference();
        EnsureSubscription();
    }

    private void Start()
    {
        ResolveReference();
        EnsureSubscription();
    }

    private void Update()
    {
        if (lifecycleEventSystem != null) return;

        ResolveReference();
        EnsureSubscription();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void HandleActivated(PowerupActivationEvent activationEvent)
    {
        if (!Accepts(activationEvent.Item)) return;

        lastActivation = activationEvent;
        onActivated?.Invoke();
    }

    private void HandleProjectileImpacted(
        PowerupProjectileEvent projectileEvent)
    {
        if (!Accepts(projectileEvent.Completion.Item)) return;

        lastProjectileEvent = projectileEvent;
        onProjectileImpacted?.Invoke();
    }

    private void HandleProjectileCancelled(
        PowerupProjectileEvent projectileEvent)
    {
        if (!Accepts(projectileEvent.Completion.Item)) return;

        lastProjectileEvent = projectileEvent;
        onProjectileCancelled?.Invoke();
    }

    private void HandleEffectStarted(PowerupEffectEvent effectEvent)
    {
        if (!Accepts(effectEvent)) return;

        lastEffectEvent = effectEvent;
        onEffectStarted?.Invoke();
    }

    private void HandleEffectRefreshed(PowerupEffectEvent effectEvent)
    {
        if (!Accepts(effectEvent)) return;

        lastEffectEvent = effectEvent;
        onEffectRefreshed?.Invoke();
    }

    private void HandleEffectEnded(PowerupEffectEvent effectEvent)
    {
        if (!Accepts(effectEvent)) return;

        lastEffectEvent = effectEvent;
        onEffectEnded?.Invoke();
    }

    private bool Accepts(ItemDefinition candidateItem)
    {
        if (!filterByItem) return true;
        if (candidateItem == null) return false;
        if (candidateItem == item) return true;

        if (alsoAcceptItems == null) return false;

        for (int i = 0; i < alsoAcceptItems.Length; i++)
        {
            if (alsoAcceptItems[i] == candidateItem) return true;
        }

        return false;
    }

    private bool Accepts(PowerupEffectEvent effectEvent)
    {
        if (!Accepts(effectEvent.SourceItem)) return false;

        return !filterEffectsByEffectId || effectEvent.EffectId == effectId;
    }

    private void ResolveReference()
    {
        if (lifecycleEventSystem == null)
        {
            lifecycleEventSystem =
                FindFirstObjectByType<PowerupLifecycleEventSystem>();
        }
    }

    private void EnsureSubscription()
    {
        if (subscribedLifecycleEventSystem == lifecycleEventSystem) return;

        Unsubscribe();
        subscribedLifecycleEventSystem = lifecycleEventSystem;

        if (subscribedLifecycleEventSystem == null) return;

        subscribedLifecycleEventSystem.OnActivated += HandleActivated;
        subscribedLifecycleEventSystem.OnProjectileImpacted +=
            HandleProjectileImpacted;
        subscribedLifecycleEventSystem.OnProjectileCancelled +=
            HandleProjectileCancelled;
        subscribedLifecycleEventSystem.OnEffectStarted += HandleEffectStarted;
        subscribedLifecycleEventSystem.OnEffectRefreshed +=
            HandleEffectRefreshed;
        subscribedLifecycleEventSystem.OnEffectEnded += HandleEffectEnded;
    }

    private void Unsubscribe()
    {
        if (subscribedLifecycleEventSystem == null) return;

        subscribedLifecycleEventSystem.OnActivated -= HandleActivated;
        subscribedLifecycleEventSystem.OnProjectileImpacted -=
            HandleProjectileImpacted;
        subscribedLifecycleEventSystem.OnProjectileCancelled -=
            HandleProjectileCancelled;
        subscribedLifecycleEventSystem.OnEffectStarted -= HandleEffectStarted;
        subscribedLifecycleEventSystem.OnEffectRefreshed -=
            HandleEffectRefreshed;
        subscribedLifecycleEventSystem.OnEffectEnded -= HandleEffectEnded;
        subscribedLifecycleEventSystem = null;
    }
}
