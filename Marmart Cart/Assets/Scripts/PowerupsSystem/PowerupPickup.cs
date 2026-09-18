using System;
using UnityEngine;

public enum PowerupPickupGrantMode
{
    Fixed,
    RandomPool
}

/// <summary>
/// Non-tier pickup entry point.
///
/// The pickup owns collection and inventory replacement. A PowerupSpawner may
/// separately own when it is prepared, visible, and collectible. RandomPool
/// results are locked during preparation so the visible item and granted item
/// can never disagree.
/// </summary>
[DisallowMultipleComponent]
public class PowerupPickup : MonoBehaviour
{
    #region Setup

    [Header("Grant")]
    [SerializeField]
    private PowerupPickupGrantMode grantMode = PowerupPickupGrantMode.Fixed;

    [SerializeField] private PowerupId fixedPowerup = PowerupId.Tomato;
    [SerializeField] private PowerupRandomPool randomPool;

    [Header("Pickup Presentation / Lifetime")]
    [Tooltip(
        "Optional visual container. A spawner can place its selected visual " +
        "inside this root; the pickup hides the complete root when unavailable."
    )]
    [SerializeField] private GameObject visualRoot;

    [Tooltip(
        "Optional explicit trigger. Automatically resolved from this " +
        "GameObject when left empty."
    )]
    [SerializeField] private Collider pickupTrigger;

    [Tooltip(
        "Disable the complete pickup after collection. This is ignored while " +
        "a PowerupSpawner manages the pickup."
    )]
    [SerializeField] private bool deactivateGameObjectOnCollect = true;

    [Tooltip(
        "Optional scene reference. Automatically resolved when a hierarchy " +
        "lookup is insufficient."
    )]
    [SerializeField] private PowerupRuntimeSystem runtimeSystem;

    #endregion

    #region Runtime

    [Header("Runtime - Read Only")]
    [SerializeField] private bool managedBySpawner;
    [SerializeField] private bool available;
    [SerializeField] private bool collected;
    [SerializeField] private bool hasPreparedPowerup;
    [SerializeField] private PowerupId preparedPowerup;
    [SerializeField] private PowerupId lastGrantedPowerup;
    [SerializeField] private int lastCollectorPlayerIndex;

    public PowerupPickupGrantMode GrantMode => grantMode;
    public GameObject VisualRoot => visualRoot;
    public bool IsManagedBySpawner => managedBySpawner;
    public bool IsAvailable => available;
    public bool IsCollected => collected;
    public bool HasPreparedPowerup => hasPreparedPowerup;
    public PowerupId PreparedPowerup => preparedPowerup;
    public PowerupId LastGrantedPowerup => lastGrantedPowerup;
    public int LastCollectorPlayerIndex => lastCollectorPlayerIndex;

    public event Action<
        PowerupPickup,
        PlayerPowerupController,
        PowerupId> OnCollected;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        pickupTrigger = GetComponent<Collider>();
    }

    private void Awake()
    {
        ResolveTrigger();
        ResolveRuntimeSystem();

        if (pickupTrigger == null)
        {
            Debug.LogError(
                "[PowerupPickup] No Collider was found. Put the pickup " +
                "trigger Collider on the same GameObject or assign it " +
                "explicitly.",
                this
            );
        }

        if (visualRoot == gameObject)
        {
            Debug.LogError(
                "[PowerupPickup] Visual Root must be a child GameObject. " +
                "Assigning the pickup root would disable the collection " +
                "component together with its presentation.",
                this
            );
        }
    }

    private void OnEnable()
    {
        if (managedBySpawner)
        {
            SetAvailability(false);
            return;
        }

        ResetPickupState();
    }

    private void OnDisable()
    {
        available = false;

        if (pickupTrigger != null)
        {
            pickupTrigger.enabled = false;
        }

        if (visualRoot != null && visualRoot != gameObject)
        {
            visualRoot.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!available || collected || other == null) return;

        if (!TryResolvePlayerController(
                other,
                out PlayerPowerupController controller))
        {
            return;
        }

        TryCollect(controller);
    }

    private void OnValidate()
    {
        if (pickupTrigger == null)
        {
            pickupTrigger = GetComponent<Collider>();
        }
    }

    #endregion

    #region Spawn Lifecycle

    /// <summary>
    /// Gives a PowerupSpawner ownership of availability without changing the
    /// pickup's authored fixed/random grant configuration.
    /// </summary>
    public void SetManagedBySpawner(bool managed)
    {
        if (managedBySpawner == managed) return;

        managedBySpawner = managed;

        if (managedBySpawner)
        {
            CancelPreparedSpawn();
        }
        else if (isActiveAndEnabled)
        {
            ResetPickupState();
        }
    }

    /// <summary>
    /// Resolves and locks the next grant while keeping the trigger and visual
    /// hidden. Random pools roll exactly once here.
    /// </summary>
    public bool TryPrepareSpawn(out PowerupId powerupId)
    {
        SetAvailability(false);
        hasPreparedPowerup = false;
        preparedPowerup = default;
        collected = false;
        lastCollectorPlayerIndex = 0;

        if (!TryResolveGrantedPowerup(out powerupId))
        {
            return false;
        }

        preparedPowerup = powerupId;
        hasPreparedPowerup = true;
        return true;
    }

    /// <summary>
    /// Makes an already-prepared pickup visible and collectible.
    /// </summary>
    public bool RevealPreparedSpawn()
    {
        if (!hasPreparedPowerup ||
            !PowerupIdRules.IsDefined(preparedPowerup))
        {
            return false;
        }

        collected = false;
        SetAvailability(true);
        return true;
    }

    /// <summary>
    /// Convenience path for legacy/standalone pickups.
    /// </summary>
    public bool TrySpawnImmediately(out PowerupId powerupId)
    {
        if (!TryPrepareSpawn(out powerupId)) return false;

        if (RevealPreparedSpawn()) return true;

        CancelPreparedSpawn();
        powerupId = default;
        return false;
    }

    /// <summary>
    /// Hides the pickup and forgets any uncollected prepared grant.
    /// </summary>
    public void CancelPreparedSpawn()
    {
        SetAvailability(false);
        hasPreparedPowerup = false;
        preparedPowerup = default;
    }

    /// <summary>
    /// Backward-compatible reset for standalone pickups. Spawner-managed
    /// pickups should use TryPrepareSpawn and RevealPreparedSpawn instead.
    /// </summary>
    public void ResetPickupState()
    {
        TrySpawnImmediately(out _);
    }

    private void SetAvailability(bool isAvailable)
    {
        available = isAvailable;
        ResolveTrigger();

        if (pickupTrigger != null)
        {
            pickupTrigger.enabled = isAvailable;
        }

        if (visualRoot != null && visualRoot != gameObject)
        {
            visualRoot.SetActive(isAvailable);
        }
    }

    #endregion

    #region Collection

    public bool TryCollect(PlayerPowerupController controller)
    {
        if (!available ||
            collected ||
            controller == null ||
            !controller.isActiveAndEnabled)
        {
            return false;
        }

        if (!hasPreparedPowerup)
        {
            if (!TryPrepareSpawn(out _) || !RevealPreparedSpawn())
            {
                return false;
            }
        }

        PowerupId grantedPowerup = preparedPowerup;

        // TryStorePowerup intentionally replaces an existing unused item.
        if (!controller.TryStorePowerup(grantedPowerup)) return false;

        CompleteCollection(controller, grantedPowerup);
        return true;
    }

    private bool TryResolveGrantedPowerup(out PowerupId powerupId)
    {
        if (grantMode == PowerupPickupGrantMode.Fixed)
        {
            powerupId = fixedPowerup;
            return PowerupIdRules.IsDefined(powerupId);
        }

        if (randomPool != null && randomPool.TryRoll(out powerupId))
        {
            return true;
        }

        powerupId = default;

        Debug.LogWarning(
            "[PowerupPickup] RandomPool mode has no valid positive-weight " +
            "entry.",
            this
        );

        return false;
    }

    private void CompleteCollection(
        PlayerPowerupController controller,
        PowerupId grantedPowerup)
    {
        collected = true;
        lastGrantedPowerup = grantedPowerup;
        lastCollectorPlayerIndex = controller != null
            ? controller.PlayerIndex
            : 0;

        SetAvailability(false);
        hasPreparedPowerup = false;
        preparedPowerup = default;

        // Publish only after trigger/presentation state is final. A managing
        // spawner may now safely begin its next countdown inside this callback.
        OnCollected?.Invoke(this, controller, grantedPowerup);

        if (deactivateGameObjectOnCollect && !managedBySpawner)
        {
            gameObject.SetActive(false);
        }
    }

    private bool TryResolvePlayerController(
        Collider other,
        out PlayerPowerupController controller)
    {
        controller = null;
        if (other == null) return false;

        // Multiple physical collider children can belong to the same runtime
        // leading cart. Resolve through their CartControlScript/attached body
        // instead of requiring another marker on every collider.
        CartControlScript cartControl =
            other.GetComponentInParent<CartControlScript>();

        if (cartControl == null && other.attachedRigidbody != null)
        {
            cartControl =
                other.attachedRigidbody.GetComponent<CartControlScript>() ??
                other.attachedRigidbody
                    .GetComponentInChildren<CartControlScript>(true) ??
                other.attachedRigidbody
                    .GetComponentInParent<CartControlScript>();
        }

        // No CartControlScript means this was not a leading-cart pickup hit.
        if (cartControl == null) return false;

        controller =
            cartControl.GetComponentInParent<PlayerPowerupController>();

        if (controller != null) return true;

        ResolveRuntimeSystem();

        return runtimeSystem != null &&
               runtimeSystem.TryGetPlayerForCartControl(
                   cartControl,
                   out controller
               );
    }

    private void ResolveRuntimeSystem()
    {
        if (runtimeSystem == null)
        {
            runtimeSystem = FindFirstObjectByType<PowerupRuntimeSystem>();
        }
    }

    private void ResolveTrigger()
    {
        if (pickupTrigger == null)
        {
            pickupTrigger = GetComponent<Collider>();
        }

        if (pickupTrigger != null && !pickupTrigger.isTrigger)
        {
            Debug.LogWarning(
                "[PowerupPickup] Assigned pickup Collider is not marked " +
                "Is Trigger.",
                pickupTrigger
            );
        }
    }

    #endregion
}
