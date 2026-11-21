using System;
using UnityEngine;

/// <summary>
/// Weighted random items for mystery map pickups.
/// Fixed map-section pickups do not need this asset.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Powerups/Random Pool",
    fileName = "PowerupRandomPool"
)]
public class PowerupRandomPool : ScriptableObject
{
    [Serializable]
    private struct WeightedEntry
    {
        [SerializeField] private ItemDefinition item;

        [Min(0f)]
        [SerializeField] private float weight;

        public ItemDefinition Item => item;
        public float Weight => Mathf.Max(0f, weight);
        public bool IsUsable => item != null && Weight > 0f;
    }

    [SerializeField] private WeightedEntry[] entries = Array.Empty<WeightedEntry>();

    public bool TryRoll(out ItemDefinition item)
    {
        float totalWeight = 0f;

        if (entries != null)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].IsUsable) totalWeight += entries[i].Weight;
            }
        }

        if (totalWeight <= 0f)
        {
            item = null;
            return false;
        }

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float accumulatedWeight = 0f;

        for (int i = 0; i < entries.Length; i++)
        {
            WeightedEntry entry = entries[i];
            if (!entry.IsUsable) continue;

            accumulatedWeight += entry.Weight;

            if (roll <= accumulatedWeight)
            {
                item = entry.Item;
                return true;
            }
        }

        // Floating-point fallback: return the final usable entry.
        for (int i = entries.Length - 1; i >= 0; i--)
        {
            if (entries[i].IsUsable)
            {
                item = entries[i].Item;
                return true;
            }
        }

        item = null;
        return false;
    }
}
