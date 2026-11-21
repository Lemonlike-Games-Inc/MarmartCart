using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum PowerupSpawnerState
{
    Inactive,
    CountingDown,
    PickupAvailable
}

/// <summary>
/// Externally controlled world spawner for one PowerupPickup.
///
/// Inactive -> CountingDown -> PickupAvailable -> CountingDown.
/// Deactivation may interrupt either active state and always discards progress,
/// so the next activation begins from a fresh countdown.
/// </summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public class PowerupSpawner : MonoBehaviour
{
    private sealed class CachedVisual
    {
        public GameObject SourcePrefab;
        public GameObject Root;
        public ParticleSystem[] ParticleSystems;
    }

    #region Setup

    [Header("Core References")]
    [SerializeField] private PowerupPickup pickup;

    [Tooltip(
        "Child root containing the persistent ring/circle VFX. It remains " +
        "visible during both countdown and pickup-available states. Do not " +
        "assign the GameObject that owns this PowerupSpawner."
    )]
    [SerializeField] private GameObject spawnerVisualRoot;

    [Tooltip(
        "World-space TextMeshPro component used for integer countdown values. " +
        "Author its transform/rotation for the isometric camera in the prefab."
    )]
    [SerializeField] private TMP_Text countdownText;

    [Tooltip(
        "Parent for cached power-up visual prefabs. When empty, the pickup's " +
        "Visual Root is used, then this spawner transform as a final fallback."
    )]
    [SerializeField] private Transform pickupVisualSocket;

    [SerializeField]
    private PowerupPickupVisualCatalog pickupVisualCatalog;

    [Header("Countdown")]
    [Tooltip(
        "Begin a fresh countdown whenever this component/GameObject becomes " +
        "enabled. Disable this when an outer event manager will call Activate."
    )]
    [SerializeField] private bool activateOnEnable = true;

    [Min(0f)]
    [SerializeField] private float countdownSeconds = 10f;

    [Tooltip(
        "Normally leave disabled so pause/timeScale also pauses spawning."
    )]
    [SerializeField] private bool useUnscaledTime;

    [Header("Pickup Visual Reuse")]
    [Tooltip(
        "Clear and replay ParticleSystems whenever a cached pickup visual is " +
        "shown again. Particle Stop Action should be None."
    )]
    [SerializeField] private bool restartPickupParticlesOnReveal = true;

    [Header("Diagnostics")]
    [SerializeField] private bool logConfigurationFailures = true;

    #endregion

    #region Runtime

    [Header("Runtime - Read Only")]
    [SerializeField] private PowerupSpawnerState state;
    [SerializeField] private bool activationRequested;
    [SerializeField] private float remainingCountdownSeconds;
    [SerializeField] private int displayedCountdownValue;
    [SerializeField] private bool hasCurrentItem;
    [SerializeField] private ItemDefinition currentItem;
    [SerializeField] private int completedSpawnCount;
    [SerializeField] private int completedCollectionCount;

    // Keyed by visual prefab: one cached instance per look.
    private readonly Dictionary<GameObject, CachedVisual>
        cachedVisuals =
            new Dictionary<GameObject, CachedVisual>();

    private CachedVisual currentVisual;
    private PowerupPickup subscribedPickup;
    private Coroutine countdownRoutine;
    private uint countdownGeneration;
    private bool configurationFailureLogged;

    public PowerupSpawnerState State => state;
    public bool IsActivated => activationRequested;
    public bool IsCountingDown =>
        state == PowerupSpawnerState.CountingDown;
    public bool IsPickupAvailable =>
        state == PowerupSpawnerState.PickupAvailable;
    public float RemainingCountdownSeconds => remainingCountdownSeconds;
    public int DisplayedCountdownValue => displayedCountdownValue;
    public bool HasCurrentItem => hasCurrentItem;
    public ItemDefinition CurrentItem => currentItem;

    public event Action<PowerupSpawner> OnActivated;
    public event Action<PowerupSpawner> OnDeactivated;
    public event Action<PowerupSpawner> OnCountdownStarted;
    public event Action<PowerupSpawner, int> OnCountdownValueChanged;
    public event Action<PowerupSpawner, ItemDefinition> OnPickupSpawned;
    public event Action<
        PowerupSpawner,
        PlayerItemHand,
        ItemDefinition> OnPickupCollected;
    public event Action<
        PowerupSpawner,
        PowerupSpawnerState> OnStateChanged;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        ConfigureManagedPickup();
        ApplyInactivePresentation();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ConfigureManagedPickup();
        EnsurePickupSubscription();

        if (activateOnEnable || activationRequested)
        {
            activationRequested = false;
            ActivateSpawner();
        }
        else
        {
            ApplyInactivePresentation();
            SetState(PowerupSpawnerState.Inactive);
        }
    }

    private void OnDisable()
    {
        activationRequested = false;
        ApplyInactivePresentation();
        SetState(PowerupSpawnerState.Inactive);
        UnsubscribePickup();
    }

    private void OnDestroy()
    {
        UnsubscribePickup();

        if (pickup != null)
        {
            pickup.SetManagedBySpawner(false);
        }
    }

    private void OnValidate()
    {
        countdownSeconds = Mathf.Max(0f, countdownSeconds);
        ResolveReferences();
    }

    #endregion

    #region External Control

    /// <summary>
    /// Idempotently activates the spawner. A transition from inactive always
    /// starts a complete new countdown.
    /// </summary>
    public void ActivateSpawner()
    {
        if (activationRequested &&
            state != PowerupSpawnerState.Inactive)
        {
            return;
        }

        activationRequested = true;
        configurationFailureLogged = false;

        if (!isActiveAndEnabled) return;

        if (!CanRunSpawner())
        {
            StopAfterConfigurationFailure();
            return;
        }

        OnActivated?.Invoke(this);
        BeginFreshCountdown();
    }

    /// <summary>
    /// Immediately interrupts countdown/availability and discards all progress.
    /// </summary>
    public void DeactivateSpawner()
    {
        bool wasActive =
            activationRequested ||
            state != PowerupSpawnerState.Inactive;

        activationRequested = false;
        ApplyInactivePresentation();
        SetState(PowerupSpawnerState.Inactive);

        if (wasActive)
        {
            OnDeactivated?.Invoke(this);
        }
    }

    public void SetSpawnerActive(bool active)
    {
        if (active)
        {
            ActivateSpawner();
        }
        else
        {
            DeactivateSpawner();
        }
    }

    /// <summary>
    /// Explicitly discards an available pickup or partial countdown and starts
    /// again. It does nothing while the spawner is externally inactive.
    /// </summary>
    public void RestartCountdown()
    {
        if (!activationRequested || !isActiveAndEnabled) return;
        BeginFreshCountdown();
    }

    #endregion

    #region State Machine

    private void BeginFreshCountdown()
    {
        if (!activationRequested) return;

        if (!CanRunSpawner())
        {
            StopAfterConfigurationFailure(true);
            return;
        }

        HideCurrentPickupVisual();
        pickup.CancelPreparedSpawn();

        StopCountdownRoutine();
        uint generation = AdvanceCountdownGeneration();

        hasCurrentItem = false;
        currentItem = null;
        remainingCountdownSeconds = countdownSeconds;
        displayedCountdownValue = 0;

        SetSpawnerVisualActive(true);
        SetCountdownVisible(countdownSeconds > 0f);
        SetState(PowerupSpawnerState.CountingDown);

        if (!IsActiveCountdown(generation)) return;

        OnCountdownStarted?.Invoke(this);

        if (!IsActiveCountdown(generation)) return;

        if (countdownSeconds <= 0f)
        {
            CompleteCountdown();
            return;
        }

        RefreshCountdownText(true);

        if (!IsActiveCountdown(generation)) return;

        countdownRoutine = StartCoroutine(RunCountdown(generation));
    }

    private IEnumerator RunCountdown(uint generation)
    {
        while (IsActiveCountdown(generation) &&
               remainingCountdownSeconds > 0f)
        {
            yield return null;

            if (!IsActiveCountdown(generation))
            {
                break;
            }

            float deltaTime = useUnscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;

            remainingCountdownSeconds = Mathf.Max(
                0f,
                remainingCountdownSeconds - Mathf.Max(0f, deltaTime)
            );

            if (remainingCountdownSeconds > 0f)
            {
                RefreshCountdownText();
            }
        }

        // A callback may have restarted the countdown while this older
        // coroutine was running. Never clear or complete the newer cycle.
        if (generation != countdownGeneration) yield break;

        countdownRoutine = null;

        if (IsActiveCountdown(generation) &&
            remainingCountdownSeconds <= 0f)
        {
            CompleteCountdown();
        }
    }

    private void CompleteCountdown()
    {
        if (!activationRequested ||
            state != PowerupSpawnerState.CountingDown)
        {
            return;
        }

        remainingCountdownSeconds = 0f;
        SetCountdownVisible(false);

        if (!pickup.TryPrepareSpawn(out ItemDefinition selectedItem))
        {
            LogConfigurationFailure(
                "The pickup could not resolve a valid Fixed/RandomPool grant."
            );
            StopAfterConfigurationFailure(true);
            return;
        }

        if (!TryResolveVisualEntry(
                selectedItem,
                out PowerupPickupVisualCatalog.Entry visualEntry) ||
            !TryResolveCachedVisual(
                visualEntry,
                out CachedVisual visual))
        {
            pickup.CancelPreparedSpawn();
            LogConfigurationFailure(
                $"No usable pickup visual is assigned for " +
                $"{GetRequiredVisualName(selectedItem)}."
            );
            StopAfterConfigurationFailure(true);
            return;
        }

        currentVisual = visual;

        if (!pickup.RevealPreparedSpawn())
        {
            HideCurrentPickupVisual();
            pickup.CancelPreparedSpawn();
            LogConfigurationFailure(
                "The prepared pickup could not enter its available state."
            );
            StopAfterConfigurationFailure(true);
            return;
        }

        ShowCurrentPickupVisual();
        currentItem = selectedItem;
        hasCurrentItem = true;
        completedSpawnCount++;
        SetState(PowerupSpawnerState.PickupAvailable);

        if (!activationRequested ||
            state != PowerupSpawnerState.PickupAvailable ||
            !pickup.IsAvailable)
        {
            return;
        }

        OnPickupSpawned?.Invoke(this, selectedItem);
    }

    private void HandlePickupCollected(
        PowerupPickup collectedPickup,
        PlayerItemHand collector,
        ItemDefinition collectedItem)
    {
        if (collectedPickup != pickup) return;

        HideCurrentPickupVisual();
        hasCurrentItem = false;
        currentItem = null;
        completedCollectionCount++;

        OnPickupCollected?.Invoke(this, collector, collectedItem);

        // A collection subscriber may deliberately deactivate the spawner.
        if (activationRequested && isActiveAndEnabled)
        {
            BeginFreshCountdown();
        }
        else
        {
            ApplyInactivePresentation();
            SetState(PowerupSpawnerState.Inactive);
        }
    }

    private void ApplyInactivePresentation()
    {
        StopCountdownRoutine();
        AdvanceCountdownGeneration();
        remainingCountdownSeconds = 0f;
        displayedCountdownValue = 0;
        hasCurrentItem = false;
        currentItem = null;

        HideCurrentPickupVisual();

        if (pickup != null)
        {
            pickup.CancelPreparedSpawn();
        }

        SetCountdownVisible(false);
        SetSpawnerVisualActive(false);
    }

    private void SetState(PowerupSpawnerState nextState)
    {
        if (state == nextState) return;

        state = nextState;
        OnStateChanged?.Invoke(this, state);
    }

    private void StopCountdownRoutine()
    {
        if (countdownRoutine == null) return;

        StopCoroutine(countdownRoutine);
        countdownRoutine = null;
    }

    private bool IsActiveCountdown(uint generation)
    {
        return activationRequested &&
               state == PowerupSpawnerState.CountingDown &&
               generation == countdownGeneration;
    }

    private uint AdvanceCountdownGeneration()
    {
        countdownGeneration = unchecked(countdownGeneration + 1u);

        if (countdownGeneration == 0u)
        {
            countdownGeneration = 1u;
        }

        return countdownGeneration;
    }

    #endregion

    #region Countdown Presentation

    private void RefreshCountdownText(bool force = false)
    {
        if (countdownText == null || remainingCountdownSeconds <= 0f) return;

        int nextValue = Mathf.Max(
            1,
            Mathf.CeilToInt(remainingCountdownSeconds)
        );

        if (!force && nextValue == displayedCountdownValue) return;

        displayedCountdownValue = nextValue;
        countdownText.text = displayedCountdownValue.ToString();
        OnCountdownValueChanged?.Invoke(this, displayedCountdownValue);
    }

    private void SetCountdownVisible(bool visible)
    {
        if (countdownText == null) return;

        GameObject textObject = countdownText.gameObject;

        if (textObject == gameObject)
        {
            LogConfigurationFailure(
                "Countdown Text must be on a child GameObject, not the " +
                "PowerupSpawner root."
            );
            return;
        }

        textObject.SetActive(visible);
    }

    private void SetSpawnerVisualActive(bool active)
    {
        if (spawnerVisualRoot == null) return;

        if (spawnerVisualRoot == gameObject)
        {
            LogConfigurationFailure(
                "Spawner Visual Root must be a child, not the GameObject " +
                "holding PowerupSpawner."
            );
            return;
        }

        spawnerVisualRoot.SetActive(active);
    }

    #endregion

    #region Pickup Visual Cache

    // Random Pool pickups hide which item they give behind one shared visual.
    private bool TryResolveVisualEntry(
        ItemDefinition selectedItem,
        out PowerupPickupVisualCatalog.Entry entry)
    {
        if (pickupVisualCatalog == null)
        {
            entry = default;
            return false;
        }

        if (pickup != null &&
            pickup.GrantMode == PowerupPickupGrantMode.RandomPool)
        {
            return pickupVisualCatalog.TryGetRandomVisual(out entry);
        }

        return pickupVisualCatalog.TryGetItemVisual(selectedItem, out entry);
    }

    private string GetRequiredVisualName(ItemDefinition selectedItem)
    {
        if (pickup != null &&
            pickup.GrantMode == PowerupPickupGrantMode.RandomPool)
        {
            return "the Random Pickup Visual";
        }

        return selectedItem != null ? selectedItem.name : "no item";
    }

    private bool TryResolveCachedVisual(
        PowerupPickupVisualCatalog.Entry entry,
        out CachedVisual visual)
    {
        GameObject visualPrefab = entry.VisualPrefab;

        if (visualPrefab == null)
        {
            visual = null;
            return false;
        }

        if (cachedVisuals.TryGetValue(visualPrefab, out visual))
        {
            if (visual != null && visual.Root != null)
            {
                ApplyVisualTransform(visual.Root.transform, entry);
                return true;
            }

            cachedVisuals.Remove(visualPrefab);
            visual = null;
        }

        ResolveVisualSocket();
        if (pickupVisualSocket == null) return false;

        GameObject root = Instantiate(
            entry.VisualPrefab,
            pickupVisualSocket
        );

        if (root == null) return false;

        root.name =
            $"{entry.VisualPrefab.name} (Pickup Visual Cached)";

        ApplyVisualTransform(root.transform, entry);

        ParticleSystem[] particleSystems =
            root.GetComponentsInChildren<ParticleSystem>(true);

        StopAndClearParticleSystems(particleSystems);
        root.SetActive(false);

        visual = new CachedVisual
        {
            SourcePrefab = entry.VisualPrefab,
            Root = root,
            ParticleSystems = particleSystems
        };

        cachedVisuals.Add(visualPrefab, visual);
        return true;
    }

    private static void ApplyVisualTransform(
        Transform visualTransform,
        PowerupPickupVisualCatalog.Entry entry)
    {
        if (visualTransform == null) return;

        visualTransform.localPosition = entry.LocalPosition;
        visualTransform.localRotation =
            Quaternion.Euler(entry.LocalEulerAngles);
        visualTransform.localScale = entry.LocalScale;
    }

    private void ShowCurrentPickupVisual()
    {
        if (currentVisual == null || currentVisual.Root == null) return;

        currentVisual.Root.SetActive(true);

        if (restartPickupParticlesOnReveal)
        {
            RestartParticleSystems(currentVisual.ParticleSystems);
        }
    }

    private void HideCurrentPickupVisual()
    {
        if (currentVisual == null) return;

        StopAndClearParticleSystems(currentVisual.ParticleSystems);

        if (currentVisual.Root != null)
        {
            currentVisual.Root.SetActive(false);
        }

        currentVisual = null;
    }

    private static void RestartParticleSystems(
        ParticleSystem[] particleSystems)
    {
        if (particleSystems == null) return;

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null) continue;

            particleSystem.Stop(
                false,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
            particleSystem.Play(false);
        }
    }

    private static void StopAndClearParticleSystems(
        ParticleSystem[] particleSystems)
    {
        if (particleSystems == null) return;

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null) continue;

            particleSystem.Stop(
                false,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }
    }

    #endregion

    #region Reference / Failure Handling

    private bool CanRunSpawner()
    {
        ResolveReferences();
        ConfigureManagedPickup();
        EnsurePickupSubscription();

        if (pickup == null)
        {
            LogConfigurationFailure("Assign a PowerupPickup.");
            return false;
        }

        if (spawnerVisualRoot == null)
        {
            LogConfigurationFailure("Assign a child Spawner Visual Root.");
            return false;
        }

        if (spawnerVisualRoot == gameObject)
        {
            LogConfigurationFailure(
                "Spawner Visual Root must be a child, not the GameObject " +
                "holding PowerupSpawner."
            );
            return false;
        }

        if (countdownText == null)
        {
            LogConfigurationFailure("Assign a world-space TMP countdown text.");
            return false;
        }

        if (countdownText.gameObject == gameObject)
        {
            LogConfigurationFailure(
                "Countdown Text must be on a child GameObject, not the " +
                "PowerupSpawner root."
            );
            return false;
        }

        if (pickupVisualCatalog == null)
        {
            LogConfigurationFailure(
                "Assign a PowerupPickupVisualCatalog."
            );
            return false;
        }

        ResolveVisualSocket();

        if (pickupVisualSocket == null)
        {
            LogConfigurationFailure("Assign a Pickup Visual Socket.");
            return false;
        }

        if (pickup.VisualRoot == pickup.gameObject)
        {
            LogConfigurationFailure(
                "PowerupPickup Visual Root must be a child, not the pickup's " +
                "own GameObject."
            );
            return false;
        }

        return true;
    }

    private void StopAfterConfigurationFailure(
        bool notifyDeactivated = false)
    {
        bool wasActive = activationRequested;
        activationRequested = false;
        ApplyInactivePresentation();
        SetState(PowerupSpawnerState.Inactive);

        if (notifyDeactivated && wasActive)
        {
            OnDeactivated?.Invoke(this);
        }
    }

    private void LogConfigurationFailure(string message)
    {
        if (!logConfigurationFailures || configurationFailureLogged) return;

        configurationFailureLogged = true;
        Debug.LogError(
            $"[PowerupSpawner] {message} The spawner was stopped and will " +
            "start a fresh countdown after its next activation.",
            this
        );
    }

    private void ResolveReferences()
    {
        if (pickup == null)
        {
            pickup =
                GetComponent<PowerupPickup>() ??
                GetComponentInChildren<PowerupPickup>(true);
        }

        if (countdownText == null)
        {
            countdownText = GetComponentInChildren<TMP_Text>(true);
        }

        ResolveVisualSocket();
    }

    private void ResolveVisualSocket()
    {
        if (pickupVisualSocket != null) return;

        if (pickup != null && pickup.VisualRoot != null)
        {
            pickupVisualSocket = pickup.VisualRoot.transform;
        }
        else
        {
            pickupVisualSocket = transform;
        }
    }

    private void ConfigureManagedPickup()
    {
        if (pickup != null)
        {
            pickup.SetManagedBySpawner(true);
        }
    }

    private void EnsurePickupSubscription()
    {
        if (subscribedPickup == pickup) return;

        UnsubscribePickup();
        subscribedPickup = pickup;

        if (subscribedPickup != null)
        {
            subscribedPickup.OnCollected += HandlePickupCollected;
        }
    }

    private void UnsubscribePickup()
    {
        if (subscribedPickup != null)
        {
            subscribedPickup.OnCollected -= HandlePickupCollected;
        }

        subscribedPickup = null;
    }

    #endregion
}
