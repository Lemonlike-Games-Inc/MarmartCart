using UnityEngine;

/// <summary>
/// Optional compatibility bridge for prototype objects that collect a power-up
/// explicitly in code. PowerupPickup no longer requires this component; it now
/// resolves the runtime leading cart exactly like the project's other pickup
/// systems resolve their owning player.
/// </summary>
[DisallowMultipleComponent]
public class PowerupCollector : MonoBehaviour
{
    [SerializeField] private PlayerPowerupController powerupController;

    public PlayerPowerupController PowerupController => powerupController;
    public bool CanAcceptPowerup => powerupController != null;

    private void Reset()
    {
        ResolveController();
    }

    private void Awake()
    {
        ResolveController();

        if (powerupController == null)
        {
            Debug.LogError(
                "[PowerupCollector] PlayerPowerupController was not found in this hierarchy.",
                this
            );
        }
    }

    public bool TryCollect(PowerupId powerupId)
    {
        return powerupController != null &&
               powerupController.TryStorePowerup(powerupId);
    }

    private void ResolveController()
    {
        if (powerupController != null) return;

        powerupController = GetComponentInParent<PlayerPowerupController>();

        if (powerupController == null)
        {
            powerupController = GetComponentInChildren<PlayerPowerupController>(true);
        }
    }
}
