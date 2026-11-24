using System.Collections;
using System.Collections.Generic;
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
    [Tooltip("The budget the match flow asked for, before budget modifiers (for example Cart Over-stock).")]
    [SerializeField] private int lastAuthoredBudget;
    [SerializeField] private int lastRequestedBudget;
    [SerializeField] private int lastSpawnedCount;
    [SerializeField] private int lastUnreleasedBudget;

    public bool IsSpawning => isSpawning;
    public int LastRequestedBudget => lastRequestedBudget;
    public int LastSpawnedCount => lastSpawnedCount;
    public int LastUnreleasedBudget => lastUnreleasedBudget;

    private struct BudgetModifier
    {
        public int Handle;
        public float Multiplier;
        public int ExtraCarts;
    }

    private readonly List<BudgetModifier> budgetModifiers = new List<BudgetModifier>(2);
    private int lastBudgetModifierHandle;

    private void Awake()
    {
        if (spawnArea == null) spawnArea = GetComponentInChildren<RandomGroundSpawnArea>(true);
    }

    /// <summary>
    /// Until removed, every restock that starts uses a changed budget: the
    /// authored budget times every modifier's multiplier, plus every modifier's
    /// extra carts. A restock with an authored budget of 0 stays 0. A running
    /// restock keeps its budget. Returns a handle for RemoveBudgetModifier;
    /// 0 means nothing was added.
    /// </summary>
    public int AddBudgetModifier(float multiplier, int extraCarts)
    {
        lastBudgetModifierHandle++;
        if (lastBudgetModifierHandle <= 0) lastBudgetModifierHandle = 1;

        budgetModifiers.Add(new BudgetModifier
        {
            Handle = lastBudgetModifierHandle,
            Multiplier = Mathf.Max(0f, multiplier),
            ExtraCarts = extraCarts
        });

        return lastBudgetModifierHandle;
    }

    public void RemoveBudgetModifier(int handle)
    {
        if (handle == 0) return;

        for (int i = budgetModifiers.Count - 1; i >= 0; i--)
        {
            if (budgetModifiers[i].Handle != handle) continue;

            budgetModifiers.RemoveAt(i);
            return;
        }
    }

    /// <summary>The budget a restock with this authored budget would spawn now.</summary>
    public int ResolveBudget(int authoredBudget)
    {
        authoredBudget = Mathf.Max(0, authoredBudget);
        if (authoredBudget == 0 || budgetModifiers.Count == 0) return authoredBudget;

        float multiplier = 1f;
        int extraCarts = 0;

        for (int i = 0; i < budgetModifiers.Count; i++)
        {
            multiplier *= budgetModifiers[i].Multiplier;
            extraCarts += budgetModifiers[i].ExtraCarts;
        }

        return Mathf.Max(0, Mathf.RoundToInt(authoredBudget * multiplier) + extraCarts);
    }

    public IEnumerator SpawnExactBudget(int totalCount, float distributionDuration, int batchSize)
    {
        if (isSpawning)
        {
            Debug.LogWarning("[CartRestockSpawner] A restock is already running.", this);
            yield return WaitForDuration(distributionDuration);
            yield break;
        }

        lastAuthoredBudget = Mathf.Max(0, totalCount);
        lastRequestedBudget = ResolveBudget(lastAuthoredBudget);
        lastSpawnedCount = 0;

        if (lastRequestedBudget != lastAuthoredBudget)
            Debug.Log($"[CartRestockSpawner] Restock budget {lastAuthoredBudget} -> {lastRequestedBudget} (budget modifiers).", this);
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
