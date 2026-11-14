using System.Collections;
using UnityEngine;

/// <summary>
/// Exact-budget empty-cart restock spawner.
///
/// RandomGroundSpawnArea Transform Y is the actual cart release/drop height.
/// The cart prefab should own its normal Rigidbody/gravity behavior.
/// </summary>
[DisallowMultipleComponent]
public class CartRestockSpawner : MonoBehaviour
{
    [Header("Cart")]
    [SerializeField] private GameObject emptyCartPrefab;
    [SerializeField] private Transform spawnedCartParent;

    [Header("Random Center Drop Area")]
    [SerializeField] private RandomGroundSpawnArea spawnArea;

    [Header("Spawn Rotation")]
    [SerializeField] private bool randomizeYaw = true;

    [Header("Runtime - Read Only")]
    [SerializeField] private bool isSpawning;
    [SerializeField] private int lastRequestedBudget;
    [SerializeField] private int lastSpawnedCount;
    [SerializeField] private int lastUnreleasedBudget;

    public bool IsSpawning => isSpawning;
    public int LastRequestedBudget => lastRequestedBudget;
    public int LastSpawnedCount => lastSpawnedCount;
    public int LastUnreleasedBudget => lastUnreleasedBudget;

    private void Awake()
    {
        if (spawnArea == null) spawnArea = GetComponentInChildren<RandomGroundSpawnArea>(true);
    }

    public IEnumerator SpawnExactBudget(int totalCount, float distributionDuration, int batchSize)
    {
        if (isSpawning)
        {
            Debug.LogWarning("[CartRestockSpawner] A restock is already running.", this);
            yield return WaitForDuration(distributionDuration);
            yield break;
        }

        lastRequestedBudget = Mathf.Max(0, totalCount);
        lastSpawnedCount = 0;
        lastUnreleasedBudget = lastRequestedBudget;

        if (lastRequestedBudget <= 0)
        {
            yield return WaitForDuration(distributionDuration);
            yield break;
        }

        if (emptyCartPrefab == null || spawnArea == null)
        {
            Debug.LogError("[CartRestockSpawner] Empty Cart Prefab or RandomGroundSpawnArea is missing.", this);
            yield return WaitForDuration(distributionDuration);
            yield break;
        }

        batchSize = Mathf.Max(1, batchSize);

        int pulseCount = Mathf.CeilToInt((float)lastRequestedBudget / batchSize);
        float pulseInterval = distributionDuration > 0f ? distributionDuration / pulseCount : 0f;
        int backlog = 0;

        isSpawning = true;

        for (int pulse = 0; pulse < pulseCount; pulse++)
        {
            int newlyDue = Mathf.Min(batchSize, lastRequestedBudget - pulse * batchSize);
            backlog += newlyDue;

            int successfulThisPulse = TrySpawnBacklog(backlog);
            backlog -= successfulThisPulse;

            if (pulseInterval > 0f) yield return new WaitForSeconds(pulseInterval);
        }

        lastUnreleasedBudget = Mathf.Max(0, lastRequestedBudget - lastSpawnedCount);
        isSpawning = false;

        if (lastUnreleasedBudget > 0)
        {
            Debug.LogWarning($"[CartRestockSpawner] Restock ended with {lastUnreleasedBudget}/{lastRequestedBudget} carts unreleased.", this);
        }
    }

    private int TrySpawnBacklog(int requestedCount)
    {
        int successful = 0;

        for (int i = 0; i < requestedCount; i++)
        {
            if (!spawnArea.TryGetValidDropPosition(out Vector3 dropPosition)) continue;

            Quaternion rotation = randomizeYaw ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) : emptyCartPrefab.transform.rotation;
            Instantiate(emptyCartPrefab, dropPosition, rotation, spawnedCartParent);

            Physics.SyncTransforms();

            lastSpawnedCount++;
            successful++;
        }

        return successful;
    }

    public void CancelSpawning()
    {
        isSpawning = false;
    }

    private IEnumerator WaitForDuration(float duration)
    {
        if (duration > 0f) yield return new WaitForSeconds(duration);
    }
}
