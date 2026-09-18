using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ArenaZoneState
{
    Idle,
    Warning,
    Active
}

/// <summary>
/// One macro resource zone.
///
/// A zone may contain any number of child ZoneLootSpawners, usually one per
/// themed section. ArenaZone owns the SHARED event budget and distributes each
/// requested loot spawn across those section spawners.
///
/// Example:
/// Zone A budget = 12
/// Produce + Meat + Seafood spawners participate
/// TOTAL spawned across all three = at most 12, not 12 each.
/// </summary>
[DisallowMultipleComponent]
public class ArenaZone : MonoBehaviour
{
    #region Identity / Sources

    [Header("Identity")]
    [SerializeField] private ArenaZoneId zoneId = ArenaZoneId.ZoneA;
    [SerializeField] private string displayName = "Zone A";

    [Header("Section Loot Sources")]
    [Tooltip("All section-level ZoneLootSpawners belonging to this zone. Empty = auto-find children.")]
    [SerializeField] private ZoneLootSpawner[] sectionLootSpawners;

    [Header("Power-up Spawners")]
    [Tooltip(
        "When enabled, this zone enables each PowerupSpawner GameObject and " +
        "starts it only while the zone is Active. Warning and Idle fully " +
        "deactivate and disable the complete spawner GameObjects."
    )]
    [SerializeField] private bool activatePowerupsHere;

    [Tooltip(
        "PowerupSpawners controlled by this zone. Empty = auto-find all " +
        "children, including inactive children."
    )]
    [SerializeField] private PowerupSpawner[] powerupSpawners;

    #endregion

    #region Runtime

    [Header("Runtime - Read Only")]
    [SerializeField] private ArenaZoneState state = ArenaZoneState.Idle;
    [SerializeField] private bool isSpawningLoot;
    [SerializeField] private int lastRequestedBudget;
    [SerializeField] private int lastSpawnedCount;
    [SerializeField] private int lastUnreleasedBudget;
    [SerializeField] private int powerupSpawnerCount;
    [SerializeField] private bool powerupSpawnersActivated;

    private readonly List<ZoneLootSpawner> sectionAttemptBuffer = new List<ZoneLootSpawner>();

    public ArenaZoneId ZoneId => zoneId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? zoneId.ToString() : displayName;
    public ArenaZoneState State => state;
    public bool IsSpawningLoot => isSpawningLoot;
    public int LastRequestedBudget => lastRequestedBudget;
    public int LastSpawnedCount => lastSpawnedCount;
    public int LastUnreleasedBudget => lastUnreleasedBudget;
    public bool ActivatesPowerupsHere => activatePowerupsHere;
    public bool PowerupSpawnersActivated => powerupSpawnersActivated;
    public int PowerupSpawnerCount => powerupSpawnerCount;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (sectionLootSpawners == null || sectionLootSpawners.Length == 0)
        {
            sectionLootSpawners = GetComponentsInChildren<ZoneLootSpawner>(true);
        }

        ResolvePowerupSpawners();

        SetIdle();
    }

    private void OnDisable()
    {
        SetPowerupSpawnersActive(false);
    }

    #endregion

    #region Zone State

    public void SetIdle()
    {
        state = ArenaZoneState.Idle;

        SetPowerupSpawnersActive(false);
    }

    public void SetWarning()
    {
        state = ArenaZoneState.Warning;

        SetPowerupSpawnersActive(false);
    }

    public void SetActive()
    {
        state = ArenaZoneState.Active;

        SetPowerupSpawnersActive(activatePowerupsHere);
    }

    #endregion

    #region Zone Power-up Spawners

    private void ResolvePowerupSpawners()
    {
        if (powerupSpawners != null && powerupSpawners.Length > 0)
        {
            powerupSpawnerCount = CountUsablePowerupSpawners();
            return;
        }

        powerupSpawners = GetComponentsInChildren<PowerupSpawner>(true);
        powerupSpawnerCount = CountUsablePowerupSpawners();
    }

    private int CountUsablePowerupSpawners()
    {
        if (powerupSpawners == null) return 0;

        int count = 0;

        for (int i = 0; i < powerupSpawners.Length; i++)
        {
            if (powerupSpawners[i] != null) count++;
        }

        return count;
    }

    private void SetPowerupSpawnersActive(bool active)
    {
        ResolvePowerupSpawners();

        powerupSpawnersActivated = false;

        if (powerupSpawners == null) return;

        for (int i = 0; i < powerupSpawners.Length; i++)
        {
            PowerupSpawner spawner = powerupSpawners[i];
            if (spawner == null) continue;

            if (spawner.gameObject == gameObject)
            {
                spawner.SetSpawnerActive(active);

                if (active)
                {
                    Debug.LogError(
                        $"[ArenaZone] {DisplayName} cannot toggle the whole " +
                        "PowerupSpawner GameObject because it is on the same " +
                        "GameObject as ArenaZone. Move the spawner to a child.",
                        this
                    );
                }

                continue;
            }

            if (active)
            {
                // Arm the spawner before enabling its root. Its OnEnable then
                // consumes the request and begins a fresh countdown.
                spawner.SetSpawnerActive(true);
                spawner.gameObject.SetActive(true);
            }
            else
            {
                // Clear all runtime state before disabling the complete root,
                // including VFX that may live outside Spawner Visual Root.
                spawner.SetSpawnerActive(false);
                spawner.gameObject.SetActive(false);
            }

            if (active &&
                spawner.gameObject.activeInHierarchy &&
                spawner.IsActivated)
            {
                powerupSpawnersActivated = true;
            }
        }
    }

    #endregion

    #region Shared Zone Loot Budget

    public IEnumerator SpawnLootBudget(int totalCount, float activeDuration, int batchSize)
    {
        if (isSpawningLoot)
        {
            Debug.LogWarning($"[ArenaZone] {DisplayName} already has a loot event running.", this);
            yield return WaitForDuration(activeDuration);
            yield break;
        }

        lastRequestedBudget = Mathf.Max(0, totalCount);
        lastSpawnedCount = 0;
        lastUnreleasedBudget = lastRequestedBudget;

        if (lastRequestedBudget <= 0)
        {
            yield return WaitForDuration(activeDuration);
            yield break;
        }

        if (!HasAnyUsableSectionSpawner())
        {
            Debug.LogError($"[ArenaZone] {DisplayName} has no usable section ZoneLootSpawner.", this);
            yield return WaitForDuration(activeDuration);
            yield break;
        }

        batchSize = Mathf.Max(1, batchSize);

        int pulseCount = Mathf.CeilToInt((float)lastRequestedBudget / batchSize);
        float pulseInterval = activeDuration > 0f ? activeDuration / pulseCount : 0f;
        int backlog = 0;

        isSpawningLoot = true;

        for (int pulse = 0; pulse < pulseCount; pulse++)
        {
            int newlyDue = Mathf.Min(batchSize, lastRequestedBudget - pulse * batchSize);
            backlog += newlyDue;

            int successfulThisPulse = TrySpawnZoneBacklog(backlog);
            backlog -= successfulThisPulse;

            if (pulseInterval > 0f) yield return new WaitForSeconds(pulseInterval);
        }

        lastUnreleasedBudget = Mathf.Max(0, lastRequestedBudget - lastSpawnedCount);
        isSpawningLoot = false;

        if (lastUnreleasedBudget > 0)
        {
            Debug.LogWarning(
                $"[ArenaZone] {DisplayName} ended with {lastUnreleasedBudget}/{lastRequestedBudget} loot unreleased because all section areas were blocked.",
                this
            );
        }
    }

    private int TrySpawnZoneBacklog(int requestedCount)
    {
        int successful = 0;

        for (int i = 0; i < requestedCount; i++)
        {
            if (!TrySpawnOneFromSections()) continue;

            successful++;
            lastSpawnedCount++;
        }

        return successful;
    }

    private bool TrySpawnOneFromSections()
    {
        BuildSectionAttemptBuffer();
        if (sectionAttemptBuffer.Count == 0) return false;

        // Weighted random first choice. If that section cannot currently find a
        // legal position, remove it and try another section rather than losing
        // the zone-wide spawn immediately.
        while (sectionAttemptBuffer.Count > 0)
        {
            int index = PickWeightedSectionIndex(sectionAttemptBuffer);
            ZoneLootSpawner spawner = sectionAttemptBuffer[index];
            sectionAttemptBuffer.RemoveAt(index);

            if (spawner != null && spawner.TrySpawnOneLoot()) return true;
        }

        return false;
    }

    private void BuildSectionAttemptBuffer()
    {
        sectionAttemptBuffer.Clear();

        if (sectionLootSpawners == null) return;

        for (int i = 0; i < sectionLootSpawners.Length; i++)
        {
            ZoneLootSpawner spawner = sectionLootSpawners[i];
            if (spawner != null && spawner.CanSpawn && spawner.SelectionWeight > 0f)
            {
                sectionAttemptBuffer.Add(spawner);
            }
        }
    }

    private int PickWeightedSectionIndex(List<ZoneLootSpawner> spawners)
    {
        if (spawners == null || spawners.Count <= 1) return 0;

        float totalWeight = 0f;

        for (int i = 0; i < spawners.Count; i++)
        {
            totalWeight += Mathf.Max(0f, spawners[i].SelectionWeight);
        }

        if (totalWeight <= 0.0001f) return Random.Range(0, spawners.Count);

        float roll = Random.value * totalWeight;
        float accumulated = 0f;

        for (int i = 0; i < spawners.Count; i++)
        {
            accumulated += Mathf.Max(0f, spawners[i].SelectionWeight);
            if (roll <= accumulated) return i;
        }

        return spawners.Count - 1;
    }

    private bool HasAnyUsableSectionSpawner()
    {
        if (sectionLootSpawners == null) return false;

        for (int i = 0; i < sectionLootSpawners.Length; i++)
        {
            ZoneLootSpawner spawner = sectionLootSpawners[i];
            if (spawner != null && spawner.CanSpawn && spawner.SelectionWeight > 0f) return true;
        }

        return false;
    }

    public void CancelLootSpawning()
    {
        isSpawningLoot = false;
    }

    private IEnumerator WaitForDuration(float duration)
    {
        if (duration > 0f) yield return new WaitForSeconds(duration);
    }

    #endregion
}
