using System;
using UnityEngine;

/// <summary>
/// Optional non-tier random source for mystery pickups.
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
        [SerializeField] private PowerupId powerup;
        [Min(0f)]
        [SerializeField] private float weight;

        public PowerupId Powerup => powerup;
        public float Weight => Mathf.Max(0f, weight);
    }

    [SerializeField] private WeightedEntry[] entries = Array.Empty<WeightedEntry>();

    public bool TryRoll(out PowerupId powerupId)
    {
        float totalWeight = 0f;

        if (entries != null)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                WeightedEntry entry = entries[i];
                if (!PowerupIdRules.IsDefined(entry.Powerup)) continue;
                totalWeight += entry.Weight;
            }
        }

        if (totalWeight <= 0f)
        {
            powerupId = default;
            return false;
        }

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float accumulatedWeight = 0f;

        for (int i = 0; i < entries.Length; i++)
        {
            WeightedEntry entry = entries[i];
            if (!PowerupIdRules.IsDefined(entry.Powerup) || entry.Weight <= 0f) continue;

            accumulatedWeight += entry.Weight;

            if (roll <= accumulatedWeight)
            {
                powerupId = entry.Powerup;
                return true;
            }
        }

        // Floating-point fallback: return the final positive valid entry.
        for (int i = entries.Length - 1; i >= 0; i--)
        {
            WeightedEntry entry = entries[i];

            if (PowerupIdRules.IsDefined(entry.Powerup) && entry.Weight > 0f)
            {
                powerupId = entry.Powerup;
                return true;
            }
        }

        powerupId = default;
        return false;
    }
}
