using System;
using UnityEngine;

/// <summary>
/// Every item in the game, in one asset.
///
/// Transitional: map pickups, the held-item HUD icons and the SFX relay still
/// use PowerupId. The mappings say which item each PowerupId gives, until those
/// read items directly.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Items/Item Library",
    fileName = "Item Library"
)]
public sealed class ItemLibrary : ScriptableObject
{
    [Serializable]
    private struct PowerupMapping
    {
        public PowerupId powerup;
        public ItemDefinition item;
    }

    [SerializeField] private ItemDefinition[] items = Array.Empty<ItemDefinition>();

    [Tooltip("Which item each map PowerupId gives.")]
    [SerializeField] private PowerupMapping[] powerupMappings = Array.Empty<PowerupMapping>();

    public int ItemCount => items != null ? items.Length : 0;

    public ItemDefinition GetItem(int index)
    {
        return items != null && index >= 0 && index < items.Length
            ? items[index]
            : null;
    }

    public bool TryGetItemForPowerup(PowerupId powerup, out ItemDefinition item)
    {
        if (powerupMappings != null)
        {
            for (int i = 0; i < powerupMappings.Length; i++)
            {
                if (powerupMappings[i].powerup != powerup ||
                    powerupMappings[i].item == null)
                {
                    continue;
                }

                item = powerupMappings[i].item;
                return true;
            }
        }

        item = null;
        return false;
    }

    public bool TryGetPowerupForItem(ItemDefinition item, out PowerupId powerup)
    {
        if (item != null && powerupMappings != null)
        {
            for (int i = 0; i < powerupMappings.Length; i++)
            {
                if (powerupMappings[i].item != item) continue;

                powerup = powerupMappings[i].powerup;
                return true;
            }
        }

        powerup = default;
        return false;
    }

    private void OnValidate()
    {
        if (items == null) items = Array.Empty<ItemDefinition>();
        if (powerupMappings == null) powerupMappings = Array.Empty<PowerupMapping>();
    }
}
