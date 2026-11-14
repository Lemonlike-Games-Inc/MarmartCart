using System.Collections;
using UnityEngine;

public enum SaleLootType
{
    Normal,
    Expensive
}

[DisallowMultipleComponent]
public class EventItemGenerator : MonoBehaviour
{
    [Header("Sale Loot")]
    [SerializeField] private SaleLootType lootType = SaleLootType.Normal;
    [SerializeField] private GameObject normalItemPrefab;
    [SerializeField] private GameObject expensiveItemPrefab;

    [Header("Items Per Drop")]
    [Min(1)][SerializeField] private int minItemsPerDrop = 1;
    [Min(1)][SerializeField] private int maxItemsPerDrop = 3;

    [Header("Time Between Drops")]
    [Min(0.05f)][SerializeField] private float minDropInterval = 0.6f;
    [Min(0.05f)][SerializeField] private float maxDropInterval = 1.2f;

    [Header("Spawn Area")]
    [Min(0.1f)][SerializeField] private float boxWidth = 40f;
    [Min(0.1f)][SerializeField] private float boxLength = 30f;
    [Min(0.1f)][SerializeField] private float raycastHeight = 30f;
    [Min(0f)][SerializeField] private float spawnHeightAboveGround = 20f;
    [SerializeField] private LayerMask groundLayer;
    [Min(1)][SerializeField] private int maxSpawnPositionAttempts = 30;

    [Header("Runtime - Read Only")]
    [SerializeField] private bool isRunning;

    private Coroutine spawnRoutine;

    public SaleLootType LootType => lootType;
    public bool IsRunning => isRunning;

    public void StartSaleEvent(float duration)
    {
        StopEvent();

        if (duration <= 0f) return;

        GameObject prefab = GetSelectedLootPrefab();

        if (prefab == null)
        {
            Debug.LogWarning($"[EventItemGenerator] {name} has no prefab assigned for {lootType} loot.", this);
            return;
        }

        spawnRoutine = StartCoroutine(SpawnSaleRoutine(prefab, duration));
    }

    public void StopEvent()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        isRunning = false;
    }

    private IEnumerator SpawnSaleRoutine(GameObject prefab, float duration)
    {
        isRunning = true;
        float endTime = Time.time + duration;

        while (Time.time < endTime)
        {
            int dropAmount = Random.Range(minItemsPerDrop, maxItemsPerDrop + 1);
            SpawnLoot(prefab, dropAmount);

            float interval = Random.Range(minDropInterval, maxDropInterval);
            yield return new WaitForSeconds(interval);
        }

        isRunning = false;
        spawnRoutine = null;
    }

    private void SpawnLoot(GameObject prefab, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (!TryGetValidSpawnPosition(out Vector3 groundPosition)) continue;

            Vector3 spawnPosition = groundPosition + Vector3.up * spawnHeightAboveGround;
            Instantiate(prefab, spawnPosition, prefab.transform.rotation);
        }
    }

    private GameObject GetSelectedLootPrefab()
    {
        return lootType == SaleLootType.Expensive ? expensiveItemPrefab : normalItemPrefab;
    }

    private bool TryGetValidSpawnPosition(out Vector3 groundPosition)
    {
        groundPosition = Vector3.zero;

        float halfWidth = boxWidth * 0.5f;
        float halfLength = boxLength * 0.5f;

        for (int attempt = 0; attempt < maxSpawnPositionAttempts; attempt++)
        {
            float xOffset = Random.Range(-halfWidth, halfWidth);
            float zOffset = Random.Range(-halfLength, halfLength);
            Vector3 rayOrigin = transform.position + new Vector3(xOffset, raycastHeight, zOffset);

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, Mathf.Infinity, groundLayer, QueryTriggerInteraction.Ignore))
            {
                groundPosition = hit.point;
                return true;
            }
        }

        return false;
    }

    private void OnValidate()
    {
        minItemsPerDrop = Mathf.Max(1, minItemsPerDrop);
        maxItemsPerDrop = Mathf.Max(minItemsPerDrop, maxItemsPerDrop);

        minDropInterval = Mathf.Max(0.05f, minDropInterval);
        maxDropInterval = Mathf.Max(minDropInterval, maxDropInterval);

        boxWidth = Mathf.Max(0.1f, boxWidth);
        boxLength = Mathf.Max(0.1f, boxLength);
        raycastHeight = Mathf.Max(0.1f, raycastHeight);
        spawnHeightAboveGround = Mathf.Max(0f, spawnHeightAboveGround);
        maxSpawnPositionAttempts = Mathf.Max(1, maxSpawnPositionAttempts);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = lootType == SaleLootType.Expensive ? Color.yellow : Color.cyan;

        Vector3 center = transform.position + Vector3.up * (raycastHeight * 0.5f);
        Vector3 size = new Vector3(boxWidth, raycastHeight, boxLength);

        Gizmos.DrawWireCube(center, size);
    }
}
