using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One SECTION-level loot source inside an ArenaZone.
///
/// Example:
/// Fresh Zone
/// - Produce ZoneLootSpawner
/// - Meat ZoneLootSpawner
/// - Seafood ZoneLootSpawner
///
/// This component does NOT own event budget or event timing.
/// ArenaZone owns the shared zone-wide budget.
///
/// Each prefab in Loot Prefabs must already contain GroceryLootPickup with its
/// intended GroceryLootDefinition assigned. This spawner does not override
/// definitions at runtime.
/// </summary>
[DisallowMultipleComponent]
public class ZoneLootSpawner : MonoBehaviour
{
    [Header("Section Identity")]
    [SerializeField] private string sectionLabel = "Section";

    [Tooltip("Relative chance that ArenaZone chooses this section. 1 = normal. 2 = twice the weight of a section set to 1.")]
    [Min(0f)]
    [SerializeField] private float selectionWeight = 1f;

    [Header("Loot Prefabs")]
    [Tooltip("World loot prefabs available in this section. Each prefab owns its GroceryLootDefinition through GroceryLootPickup.")]
    [SerializeField] private GameObject[] lootPrefabs;

    [Header("Section Drop Area")]
    [SerializeField] private RandomGroundSpawnArea spawnArea;

    [Header("Spawn Rotation")]
    [SerializeField] private bool randomizeYaw = true;

    [Header("Runtime - Read Only")]
    [SerializeField] private int validPrefabCount;
    [SerializeField] private int totalSpawned;

    private readonly List<GameObject> validLootPrefabs = new List<GameObject>();

    public string SectionLabel => string.IsNullOrWhiteSpace(sectionLabel) ? gameObject.name : sectionLabel;
    public float SelectionWeight => Mathf.Max(0f, selectionWeight);
    public bool CanSpawn
    {
        get
        {
            ResolveSpawnArea();
            return spawnArea != null && validLootPrefabs.Count > 0;
        }
    }
    public int ValidPrefabCount => validLootPrefabs.Count;
    public int TotalSpawned => totalSpawned;
    public RandomGroundSpawnArea SpawnArea
    {
        get
        {
            ResolveSpawnArea();
            return spawnArea;
        }
    }

    private void Awake()
    {
        ResolveSpawnArea();
        RebuildPrefabCache();
    }

    private void OnValidate()
    {
        selectionWeight = Mathf.Max(0f, selectionWeight);
    }

    public bool TrySpawnOneLoot()
    {
        ResolveSpawnArea();

        if (!CanSpawn) return false;
        if (!spawnArea.TryGetValidDropPosition(out Vector3 dropPosition)) return false;

        GameObject prefab = validLootPrefabs[Random.Range(0, validLootPrefabs.Count)];
        Quaternion rotation = randomizeYaw ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) : prefab.transform.rotation;

        Instantiate(prefab, dropPosition, rotation);

        // Make this pickup's InvisibleTop trigger available to the next spawn
        // request in the same event pulse.
        Physics.SyncTransforms();

        totalSpawned++;
        return true;
    }

    [ContextMenu("Rebuild Loot Prefab Cache")]
    public void RebuildPrefabCache()
    {
        validLootPrefabs.Clear();

        if (lootPrefabs == null)
        {
            validPrefabCount = 0;
            return;
        }

        for (int i = 0; i < lootPrefabs.Length; i++)
        {
            GameObject prefab = lootPrefabs[i];
            if (prefab == null) continue;

            GroceryLootPickup pickup = prefab.GetComponent<GroceryLootPickup>();

            if (pickup == null)
            {
                Debug.LogWarning($"[ZoneLootSpawner] '{prefab.name}' skipped: GroceryLootPickup must be on the prefab root.", prefab);
                continue;
            }

            if (pickup.LootDefinition == null)
            {
                Debug.LogWarning($"[ZoneLootSpawner] '{prefab.name}' skipped: GroceryLootPickup has no GroceryLootDefinition assigned.", prefab);
                continue;
            }

            validLootPrefabs.Add(prefab);
        }

        validPrefabCount = validLootPrefabs.Count;
    }

    private void ResolveSpawnArea()
    {
        if (spawnArea == null)
        {
            spawnArea = GetComponentInChildren<RandomGroundSpawnArea>(true);
        }
    }
}
