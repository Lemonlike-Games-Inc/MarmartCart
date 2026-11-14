using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class TutorialOneSlotSpawner : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private GameObject prefab;

    [Tooltip("Uses the same legal drop-position system as the current production spawners. Empty = auto-find in children.")]
    [SerializeField] private RandomGroundSpawnArea spawnArea;

    [SerializeField] private Transform spawnedParent;

    [Header("Spawn Rotation")]
    [SerializeField] private bool randomizeYaw = true;

    [Header("Repickable Slot")]
    [Tooltip("Delay after the final player leaves the trigger before the empty slot may refill.")]
    [Min(0f)]
    [SerializeField] private float respawnDelay = 1.5f;

    [Tooltip("Horizontal X/Z distance from the original drop position required before a movable resource is considered taken.")]
    [Min(0.01f)]
    [SerializeField] private float takenDistance = 1f;

    [Tooltip("Preserves the old tutorial anti-overlap rule before refilling.")]
    [SerializeField] private bool requireZoneClearToRespawn = true;

    [Tooltip("Layers that block this tutorial slot from refilling.")]
    [SerializeField] private LayerMask zoneBlockMask;

    [Tooltip("How often to retry while the area or RandomGroundSpawnArea is blocked.")]
    [Min(0.02f)]
    [SerializeField] private float retryInterval = 0.15f;

    [Header("Runtime - Read Only")]
    [SerializeField] private GameObject currentItem;
    [SerializeField] private Vector3 currentDropPosition;
    [SerializeField] private int playersInside;
    [SerializeField] private bool waitingToRespawn;
    [SerializeField] private int totalSpawned;

    private BoxCollider zoneTrigger;
    private Coroutine respawnRoutine;
    private ISpawnerHoldable currentHoldable;

    public GameObject CurrentItem => currentItem;
    public bool HasAvailableItem => currentItem != null;
    public bool WaitingToRespawn => waitingToRespawn;
    public int TotalSpawned => totalSpawned;

    private void Reset()
    {
        ResolveReferences();

        if (zoneTrigger != null)
        {
            zoneTrigger.isTrigger = true;
        }
    }

    private void Awake()
    {
        ResolveReferences();

        if (zoneTrigger != null)
        {
            zoneTrigger.isTrigger = true;
        }
    }

    private void Start()
    {
        SpawnNow();
    }

    private void Update()
    {
        if (currentItem == null)
        {
            ReleaseCurrentReference();

            if (playersInside == 0)
            {
                TryStartRespawn();
            }

            return;
        }

        if (!currentItem.activeInHierarchy)
        {
            MarkCurrentItemTaken();
            return;
        }

        // Ignore vertical drop/fall distance. Only horizontal movement means
        // a movable cart/resource has actually left this one-slot source.
        Vector3 offset = currentItem.transform.position - currentDropPosition;
        offset.y = 0f;

        if (offset.sqrMagnitude >= takenDistance * takenDistance)
        {
            MarkCurrentItemTaken();
        }
    }

    private void OnDisable()
    {
        if (respawnRoutine != null)
        {
            StopCoroutine(respawnRoutine);
            respawnRoutine = null;
        }

        waitingToRespawn = false;
        playersInside = 0;
    }

    private void OnValidate()
    {
        respawnDelay = Mathf.Max(0f, respawnDelay);
        takenDistance = Mathf.Max(0.01f, takenDistance);
        retryInterval = Mathf.Max(0.02f, retryInterval);

        if (zoneTrigger == null)
        {
            zoneTrigger = GetComponent<BoxCollider>();
        }

        if (zoneTrigger != null)
        {
            zoneTrigger.isTrigger = true;
        }

        if (spawnArea == null)
        {
            spawnArea = GetComponentInChildren<RandomGroundSpawnArea>(true);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;

        playersInside++;

        if (respawnRoutine != null)
        {
            StopCoroutine(respawnRoutine);
            respawnRoutine = null;
            waitingToRespawn = false;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;

        playersInside = Mathf.Max(0, playersInside - 1);

        if (playersInside == 0)
        {
            TryStartRespawn();
        }
    }

    private static bool IsPlayer(Collider other)
    {
        if (other == null) return false;

        return
            other.CompareTag("Player1") ||
            other.CompareTag("Player2") ||
            other.CompareTag("Player3") ||
            other.CompareTag("Player4");
    }

    private void MarkCurrentItemTaken()
    {
        ReleaseCurrentReference();

        if (playersInside == 0)
        {
            TryStartRespawn();
        }
    }

    private void ReleaseCurrentReference()
    {
        currentHoldable?.OnSpawnerHoldEnd();
        currentHoldable = null;
        currentItem = null;
    }

    private void TryStartRespawn()
    {
        if (currentItem != null) return;
        if (playersInside > 0) return;
        if (respawnRoutine != null) return;
        if (!isActiveAndEnabled) return;

        respawnRoutine = StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        waitingToRespawn = true;

        float elapsed = 0f;

        while (elapsed < respawnDelay)
        {
            if (playersInside > 0)
            {
                FinishRespawnRoutine();
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Unlike normal match spawning, this tutorial source has no budget.
        // Keep retrying until its single available item can be restored.
        while (currentItem == null)
        {
            if (playersInside > 0)
            {
                FinishRespawnRoutine();
                yield break;
            }

            if (requireZoneClearToRespawn && !IsZoneClear())
            {
                yield return new WaitForSeconds(retryInterval);
                continue;
            }

            if (TrySpawnNow())
            {
                FinishRespawnRoutine();
                yield break;
            }

            yield return new WaitForSeconds(retryInterval);
        }

        FinishRespawnRoutine();
    }

    private void FinishRespawnRoutine()
    {
        respawnRoutine = null;
        waitingToRespawn = false;
    }

    public void SpawnNow()
    {
        TrySpawnNow();
    }

    public bool TrySpawnNow()
    {
        ResolveReferences();

        if (prefab == null)
        {
            Debug.LogError("[TutorialOneSlotSpawner] Prefab is missing.", this);
            return false;
        }

        if (spawnArea == null)
        {
            Debug.LogError("[TutorialOneSlotSpawner] RandomGroundSpawnArea is missing.", this);
            return false;
        }

        if (currentItem != null)
        {
            return false;
        }

        if (!spawnArea.TryGetValidDropPosition(out Vector3 dropPosition))
        {
            return false;
        }

        Quaternion rotation =
            randomizeYaw
                ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
                : prefab.transform.rotation;

        currentItem = Instantiate(
            prefab,
            dropPosition,
            rotation,
            spawnedParent
        );

        currentDropPosition = dropPosition;

        // Backwards compatibility only. New cart / GroceryLootPickup prefabs
        // do not need to implement ISpawnerHoldable.
        currentHoldable =
            currentItem.GetComponentInChildren<ISpawnerHoldable>();

        currentHoldable?.OnSpawnerHoldStart();

        Physics.SyncTransforms();

        totalSpawned++;
        return true;
    }

    private bool IsZoneClear()
    {
        if (zoneTrigger == null) return true;

        Vector3 center = zoneTrigger.bounds.center;
        Vector3 halfExtents = zoneTrigger.bounds.extents;

        Collider[] hits = Physics.OverlapBox(
            center,
            halfExtents,
            Quaternion.identity,
            zoneBlockMask,
            QueryTriggerInteraction.Ignore
        );

        return hits == null || hits.Length == 0;
    }

    private void ResolveReferences()
    {
        if (zoneTrigger == null)
        {
            zoneTrigger = GetComponent<BoxCollider>();
        }

        if (spawnArea == null)
        {
            spawnArea =
                GetComponentInChildren<RandomGroundSpawnArea>(true);
        }
    }

    [ContextMenu("Force Clear Slot")]
    public void ForceClearSlot()
    {
        if (respawnRoutine != null)
        {
            StopCoroutine(respawnRoutine);
            respawnRoutine = null;
        }

        waitingToRespawn = false;

        currentHoldable?.OnSpawnerHoldEnd();
        currentHoldable = null;

        if (currentItem != null)
        {
            Destroy(currentItem);
        }

        currentItem = null;
    }

    [ContextMenu("Force Refill Slot")]
    public void ForceRefillSlot()
    {
        ForceClearSlot();
        SpawnNow();
    }
}
