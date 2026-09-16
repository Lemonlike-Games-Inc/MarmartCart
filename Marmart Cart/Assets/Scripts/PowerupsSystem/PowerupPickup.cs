using System;
using UnityEngine;

public enum PowerupPickupGrantMode
{
    Fixed,
    RandomPool
}

/// <summary>
/// New non-tier pickup entry point.
///
/// Fixed is the primary map-section design. RandomPool is available only for
/// explicitly authored mystery pickups. Collection follows the same pattern as
/// GroceryLootPickup: this pickup owns the trigger, receives any leading-cart
/// child collider, then resolves the player's controller through the runtime
/// cart hierarchy/registry. A newly collected item replaces any stored item.
/// </summary>
[DisallowMultipleComponent]
public class PowerupPickup : MonoBehaviour
{
    #region Setup

    [Header("Grant")]
    [SerializeField] private PowerupPickupGrantMode grantMode = PowerupPickupGrantMode.Fixed;
    [SerializeField] private PowerupId fixedPowerup = PowerupId.Tomato;
    [SerializeField] private PowerupRandomPool randomPool;

    [Header("Pickup Presentation / Lifetime")]
    [Tooltip("Optional visual child. It is hidden when this pickup is collected without deactivating the whole object.")]
    [SerializeField] private GameObject visualRoot;

    [Tooltip("Optional explicit trigger. Automatically resolved from this GameObject when left empty.")]
    [SerializeField] private Collider pickupTrigger;

    [Tooltip(
        "Disable the complete pickup after collection. Leave off when an external respawn system wants the root to remain active."
    )]
    [SerializeField] private bool deactivateGameObjectOnCollect = true;

    [Tooltip("Optional scene reference. Automatically resolved when a hierarchy lookup is insufficient.")]
    [SerializeField] private PowerupRuntimeSystem runtimeSystem;

    #endregion

    #region Runtime

    [Header("Runtime - Read Only")]
    [SerializeField] private bool collected;
    [SerializeField] private PowerupId lastGrantedPowerup;
    [SerializeField] private int lastCollectorPlayerIndex;

    public bool IsCollected => collected;
    public event Action<PowerupPickup, PlayerPowerupController, PowerupId> OnCollected;

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
                "[PowerupPickup] No Collider was found. Put the pickup trigger Collider on the same GameObject or assign it explicitly.",
                this
            );
        }
    }

    private void OnEnable()
    {
        ResetPickupState();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected || other == null) return;

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

    #region Collection

    public bool TryCollect(PlayerPowerupController controller)
    {
        if (collected || controller == null || !controller.isActiveAndEnabled) return false;
        if (!TryResolveGrantedPowerup(out PowerupId grantedPowerup)) return false;

        // TryStorePowerup intentionally replaces an existing unused item.
        if (!controller.TryStorePowerup(grantedPowerup)) return false;

        CompleteCollection(controller, grantedPowerup);
        return true;
    }

    public void ResetPickupState()
    {
        collected = false;
        lastCollectorPlayerIndex = 0;

        ResolveTrigger();

        if (pickupTrigger != null)
        {
            pickupTrigger.enabled = true;
        }

        if (visualRoot != null)
        {
            visualRoot.SetActive(true);
        }
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
            "[PowerupPickup] RandomPool mode has no valid positive-weight entry.",
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

        OnCollected?.Invoke(this, controller, grantedPowerup);

        if (pickupTrigger != null)
        {
            pickupTrigger.enabled = false;
        }

        if (visualRoot != null)
        {
            visualRoot.SetActive(false);
        }

        if (deactivateGameObjectOnCollect)
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
                other.attachedRigidbody.GetComponentInChildren<CartControlScript>(true) ??
                other.attachedRigidbody.GetComponentInParent<CartControlScript>();
        }

        // No CartControlScript means this was not a leading-cart pickup hit.
        if (cartControl == null) return false;

        controller = cartControl.GetComponentInParent<PlayerPowerupController>();
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
                "[PowerupPickup] Assigned pickup Collider is not marked Is Trigger.",
                pickupTrigger
            );
        }
    }

    #endregion
}
