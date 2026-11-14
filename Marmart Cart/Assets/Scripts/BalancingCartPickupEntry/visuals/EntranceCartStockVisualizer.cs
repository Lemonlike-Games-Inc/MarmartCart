using System.Collections.Generic;
using UnityEngine;

public class EntranceCartStockVisualizer : MonoBehaviour
{
    public enum LaneFillMode
    {
        SequentialByLane,
        AlternateByLane
    }

    public enum LaneConsumeMode
    {
        SequentialFrontByLane,
        AlternateReverseByLane
    }

    [Header("Cart Visual")]
    [SerializeField] private GameObject cartVisualPrefab;
    [SerializeField] private Vector3 spawnedCartEulerOffset;

    [Header("Layout")]
    [SerializeField] private int laneCount = 2;
    [SerializeField] private int cartsPerLane = 10;
    [SerializeField] private float cartGap = 2.0f;
    [SerializeField] private float laneSpacing = 3.0f;
    [SerializeField] private bool centerLanesAroundOrigin = true;

    [Header("Stock Rules")]
    [SerializeField] private LaneFillMode fillMode = LaneFillMode.AlternateByLane;
    [SerializeField] private LaneConsumeMode consumeMode = LaneConsumeMode.AlternateReverseByLane;
    [SerializeField] private int maxTotalStock = 20;

    [Header("Spawn Over Time")]
    [SerializeField] private bool autoSpawnOverTime = true;
    [SerializeField] private float spawnInterval = 2.0f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool logOperations = false;

    private float spawnTimer = 0f;

    private readonly List<CartLaneData> lanes = new List<CartLaneData>();

    public int LaneCount => laneCount;
    public int CartsPerLane => cartsPerLane;
    public int MaxCapacity => laneCount * cartsPerLane;
    public int CurrentStock { get; private set; }

    private void Awake()
    {
        RebuildLaneData();
        RebuildVisualsFromCurrentStock();
    }

    private void OnValidate()
    {
        laneCount = Mathf.Max(1, laneCount);
        cartsPerLane = Mathf.Max(1, cartsPerLane);
        cartGap = Mathf.Max(0.01f, cartGap);
        laneSpacing = Mathf.Max(0.01f, laneSpacing);
        spawnInterval = Mathf.Max(0.01f, spawnInterval);

        maxTotalStock = Mathf.Clamp(maxTotalStock, 0, MaxCapacity);

        if (!Application.isPlaying)
        {
            RebuildLaneData();
        }
    }

    private void Update()
    {
        if (!autoSpawnOverTime)
            return;

        if (CurrentStock >= Mathf.Min(maxTotalStock, MaxCapacity))
            return;

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;
            TryAddOneCart();
        }
    }

    public void RebuildLaneData()
    {
        lanes.Clear();

        for (int laneIndex = 0; laneIndex < laneCount; laneIndex++)
        {
            lanes.Add(new CartLaneData(cartsPerLane));
        }

        CurrentStock = Mathf.Clamp(CurrentStock, 0, Mathf.Min(maxTotalStock, MaxCapacity));
    }

    public void RebuildVisualsFromCurrentStock()
    {
        ClearAllSpawnedVisuals();

        int targetStock = Mathf.Clamp(CurrentStock, 0, Mathf.Min(maxTotalStock, MaxCapacity));
        CurrentStock = 0;

        for (int i = 0; i < targetStock; i++)
        {
            TryAddOneCart();
        }
    }

    public bool TryAddOneCart()
    {
        if (CurrentStock >= Mathf.Min(maxTotalStock, MaxCapacity))
            return false;

        if (TryGetNextFillSlot(out int laneIndex, out int slotIndex))
        {
            SpawnCartAtSlot(laneIndex, slotIndex);
            CurrentStock++;

            if (logOperations)
            {
                Debug.Log($"[CartStock] Added cart at Lane {laneIndex}, Slot {slotIndex}. CurrentStock = {CurrentStock}");
            }

            return true;
        }

        return false;
    }

    public int ConsumeCarts(int amount)
    {
        if (amount <= 0 || CurrentStock <= 0)
            return 0;

        int removed = 0;

        while (removed < amount && CurrentStock > 0)
        {
            if (!TryGetNextConsumeSlot(out int laneIndex, out int slotIndex))
                break;

            RemoveCartAtSlot(laneIndex, slotIndex);
            removed++;
            CurrentStock--;

            if (logOperations)
            {
                Debug.Log($"[CartStock] Removed cart at Lane {laneIndex}, Slot {slotIndex}. CurrentStock = {CurrentStock}");
            }
        }

        return removed;
    }

    public bool HasAnyStock()
    {
        return CurrentStock > 0;
    }

    public bool IsFull()
    {
        return CurrentStock >= Mathf.Min(maxTotalStock, MaxCapacity);
    }

    public void SetCurrentStockImmediate(int newStock)
    {
        CurrentStock = Mathf.Clamp(newStock, 0, Mathf.Min(maxTotalStock, MaxCapacity));
        RebuildLaneData();
        RebuildVisualsFromCurrentStock();
    }

    public Vector3 GetLaneStartWorldPosition(int laneIndex)
    {
        return transform.position + GetLaneOffset(laneIndex);
    }

    public Vector3 GetSlotWorldPosition(int laneIndex, int slotIndex)
    {
        return GetLaneStartWorldPosition(laneIndex) + transform.forward * (slotIndex * cartGap);
    }

    public Quaternion GetCartWorldRotation()
    {
        return transform.rotation;
    }

    private bool TryGetNextFillSlot(out int laneIndex, out int slotIndex)
    {
        switch (fillMode)
        {
            case LaneFillMode.SequentialByLane:
                return TryGetNextFillSlot_SequentialByLane(out laneIndex, out slotIndex);

            case LaneFillMode.AlternateByLane:
                return TryGetNextFillSlot_AlternateByLane(out laneIndex, out slotIndex);
        }

        laneIndex = -1;
        slotIndex = -1;
        return false;
    }

    private bool TryGetNextConsumeSlot(out int laneIndex, out int slotIndex)
    {
        switch (consumeMode)
        {
            case LaneConsumeMode.SequentialFrontByLane:
                return TryGetNextConsumeSlot_SequentialFrontByLane(out laneIndex, out slotIndex);

            case LaneConsumeMode.AlternateReverseByLane:
                return TryGetNextConsumeSlot_AlternateReverseByLane(out laneIndex, out slotIndex);
        }

        laneIndex = -1;
        slotIndex = -1;
        return false;
    }

    private bool TryGetNextFillSlot_SequentialByLane(out int laneIndex, out int slotIndex)
    {
        for (laneIndex = 0; laneIndex < laneCount; laneIndex++)
        {
            for (slotIndex = 0; slotIndex < cartsPerLane; slotIndex++)
            {
                if (!lanes[laneIndex].HasCartAt(slotIndex))
                    return true;
            }
        }

        laneIndex = -1;
        slotIndex = -1;
        return false;
    }

    private bool TryGetNextFillSlot_AlternateByLane(out int laneIndex, out int slotIndex)
    {
        for (slotIndex = 0; slotIndex < cartsPerLane; slotIndex++)
        {
            for (laneIndex = 0; laneIndex < laneCount; laneIndex++)
            {
                if (!lanes[laneIndex].HasCartAt(slotIndex))
                    return true;
            }
        }

        laneIndex = -1;
        slotIndex = -1;
        return false;
    }

    private bool TryGetNextConsumeSlot_SequentialFrontByLane(out int laneIndex, out int slotIndex)
    {
        for (laneIndex = 0; laneIndex < laneCount; laneIndex++)
        {
            for (slotIndex = 0; slotIndex < cartsPerLane; slotIndex++)
            {
                if (lanes[laneIndex].HasCartAt(slotIndex))
                    return true;
            }
        }

        laneIndex = -1;
        slotIndex = -1;
        return false;
    }

    private bool TryGetNextConsumeSlot_AlternateReverseByLane(out int laneIndex, out int slotIndex)
    {
        for (slotIndex = cartsPerLane - 1; slotIndex >= 0; slotIndex--)
        {
            for (laneIndex = 0; laneIndex < laneCount; laneIndex++)
            {
                if (lanes[laneIndex].HasCartAt(slotIndex))
                    return true;
            }
        }

        laneIndex = -1;
        slotIndex = -1;
        return false;
    }

    private void SpawnCartAtSlot(int laneIndex, int slotIndex)
    {
        if (cartVisualPrefab == null)
            return;

        if (lanes[laneIndex].HasCartAt(slotIndex))
            return;

        GameObject instance = Instantiate(
            cartVisualPrefab,
            GetSlotWorldPosition(laneIndex, slotIndex),
            transform.rotation,
            transform
        );

        instance.transform.rotation *= Quaternion.Euler(spawnedCartEulerOffset);

        instance.name = $"EntranceCart_L{laneIndex}_S{slotIndex}";
        lanes[laneIndex].SetCartAt(slotIndex, instance);
    }

    private void RemoveCartAtSlot(int laneIndex, int slotIndex)
    {
        GameObject cart = lanes[laneIndex].GetCartAt(slotIndex);
        if (cart != null)
        {
            if (Application.isPlaying)
                Destroy(cart);
            else
                DestroyImmediate(cart);
        }

        lanes[laneIndex].SetCartAt(slotIndex, null);
    }

    private void ClearAllSpawnedVisuals()
    {
        for (int laneIndex = 0; laneIndex < lanes.Count; laneIndex++)
        {
            for (int slotIndex = 0; slotIndex < lanes[laneIndex].SlotCount; slotIndex++)
            {
                GameObject cart = lanes[laneIndex].GetCartAt(slotIndex);
                if (cart != null)
                {
                    if (Application.isPlaying)
                        Destroy(cart);
                    else
                        DestroyImmediate(cart);
                }

                lanes[laneIndex].SetCartAt(slotIndex, null);
            }
        }
    }

    private Vector3 GetLaneOffset(int laneIndex)
    {
        if (centerLanesAroundOrigin)
        {
            float totalWidth = (laneCount - 1) * laneSpacing;
            float centeredOffset = -totalWidth * 0.5f + laneIndex * laneSpacing;
            return transform.right * centeredOffset;
        }

        return transform.right * (laneIndex * laneSpacing);
    }

    private Vector3 GetPreviewLaneStart(int laneIndex)
    {
        return transform.position + GetLaneOffset(laneIndex);
    }

    [ContextMenu("Rebuild Lane Data")]
    private void DebugRebuildLaneData()
    {
        RebuildLaneData();
        RebuildVisualsFromCurrentStock();
    }

    [ContextMenu("Add One Cart")]
    private void DebugAddOneCart()
    {
        TryAddOneCart();
    }

    [ContextMenu("Consume One Cart")]
    private void DebugConsumeOneCart()
    {
        ConsumeCarts(1);
    }

    [ContextMenu("Fill To Max")]
    private void DebugFillToMax()
    {
        SetCurrentStockImmediate(Mathf.Min(maxTotalStock, MaxCapacity));
    }

    [ContextMenu("Clear All")]
    private void DebugClearAll()
    {
        SetCurrentStockImmediate(0);
    }

    [ContextMenu("Log Slot Occupancy")]
    private void DebugLogSlotOccupancy()
    {
        for (int slotIndex = 0; slotIndex < cartsPerLane; slotIndex++)
        {
            string row = $"Slot {slotIndex}: ";

            for (int laneIndex = 0; laneIndex < laneCount; laneIndex++)
            {
                row += lanes[laneIndex].HasCartAt(slotIndex) ? $"[L{laneIndex}:X] " : $"[L{laneIndex}: ] ";
            }

            Debug.Log(row);
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
            return;

        int previewLaneCount = Mathf.Max(1, laneCount);
        int previewSlots = Mathf.Max(1, cartsPerLane);

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.2f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 3f);

        for (int laneIndex = 0; laneIndex < previewLaneCount; laneIndex++)
        {
            Vector3 laneStart = GetPreviewLaneStart(laneIndex);
            Vector3 laneEnd = laneStart + transform.forward * ((previewSlots - 1) * cartGap);

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(laneStart, 0.15f);
            Gizmos.DrawLine(laneStart, laneEnd);

            for (int slotIndex = 0; slotIndex < previewSlots; slotIndex++)
            {
                Vector3 slotPos = laneStart + transform.forward * (slotIndex * cartGap);
                Gizmos.color = Color.white;
                Gizmos.DrawWireCube(slotPos, new Vector3(1.2f, 1.0f, 2.0f));
            }
        }
    }

    [System.Serializable]
    private class CartLaneData
    {
        [SerializeField] private List<GameObject> slotCarts = new List<GameObject>();

        public int SlotCount => slotCarts.Count;

        public CartLaneData(int slotCount)
        {
            for (int i = 0; i < slotCount; i++)
            {
                slotCarts.Add(null);
            }
        }

        public bool HasCartAt(int slotIndex)
        {
            return slotCarts[slotIndex] != null;
        }

        public GameObject GetCartAt(int slotIndex)
        {
            return slotCarts[slotIndex];
        }

        public void SetCartAt(int slotIndex, GameObject cart)
        {
            slotCarts[slotIndex] = cart;
        }
    }
}