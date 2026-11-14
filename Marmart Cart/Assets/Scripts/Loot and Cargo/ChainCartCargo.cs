using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ChainCartCargo : MonoBehaviour
{
    [Header("Shared Cargo Settings")]
    [SerializeField] private CargoSystemSettings settings;

    [Header("Cargo Visual Parent")]
    [SerializeField] private Transform cargoVisualAnchor;
    [SerializeField] private Material prototypeSphereMaterial;

    [Header("Predesigned Cargo Slots")]
    [Tooltip("OFF = use the procedural prototype layout. ON = use authored safe-slot Transforms.")]
    [SerializeField] private bool usePredesignedSlotPositions = false;

    [Tooltip("Safe cargo slots in fill order. List them from lower/earlier slots toward the highest/topmost slots.")]
    [SerializeField] private List<Transform> predesignedSafeSlots = new List<Transform>();

    [Tooltip("How many of the LAST authored safe slots form the top pattern repeated upward for overload.")]
    [Min(1)]
    [SerializeField] private int predesignedOverflowTemplateSlotCount = 3;

    [Tooltip("Local-space offset added for each generated overflow layer above the authored top pattern.")]
    [SerializeField] private Vector3 predesignedOverflowLayerOffset = new Vector3(0f, 0.24f, 0f);

    [Tooltip("Keep the authored slot rotations for safe and generated overflow cargo.")]
    [SerializeField] private bool usePredesignedSlotRotations = true;

    [Header("Persistent Cargo - Runtime")]
    [SerializeField] private List<CargoEntry> cargoEntries = new List<CargoEntry>();

    [Header("Local Cargo Runtime - Read Only")]
    [SerializeField] private int localLoad;
    [SerializeField] private int localScoreValue;
    [SerializeField] private int localOverload;
    [SerializeField] private int visualOverflowEntryCount;

    [Header("Prototype Test Data")]
    [SerializeField] private GroceryLootDefinition[] debugTestLootDefinitions;

    private const string RuntimeVisualRootName = "__RuntimeCargoVisuals";
    private Transform runtimeVisualRoot;
    private MaterialPropertyBlock materialPropertyBlock;

    public CargoSystemSettings Settings => settings;
    public IReadOnlyList<CargoEntry> CargoEntries => cargoEntries;
    public int CargoEntryCount => cargoEntries.Count;
    public int SafeCapacity => settings != null ? settings.CapacityPerCart : 0;
    public int LocalLoad => localLoad;
    public int LocalScoreValue => localScoreValue;
    public int LocalOverload => localOverload;
    public bool IsOverloaded => localOverload > 0;
    public int VisualOverflowEntryCount => visualOverflowEntryCount;
    public bool UsePredesignedSlotPositions => usePredesignedSlotPositions;
    public int VisualSafeSlotCount => GetVisualSafeSlotCount();

    public event Action<ChainCartCargo, CargoEntry> OnCargoAdded;
    public event Action<ChainCartCargo, CargoEntry> OnCargoRemoved;
    public event Action<ChainCartCargo> OnCargoChanged;

    private void Awake()
    {
        materialPropertyBlock = new MaterialPropertyBlock();

        if (settings == null) Debug.LogError("[ChainCartCargo] CargoSystemSettings is not assigned.", this);

        ValidatePredesignedSlotSetup();
        EnsureRuntimeVisualRoot();
        RecalculateLocalState();
    }

    private void Start()
    {
        RebuildCargoVisuals();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        EnsureRuntimeVisualRoot();
        RecalculateLocalState();
    }

    private void OnValidate()
    {
        predesignedOverflowTemplateSlotCount = Mathf.Max(1, predesignedOverflowTemplateSlotCount);

        if (predesignedSafeSlots != null && predesignedSafeSlots.Count > 0)
        {
            predesignedOverflowTemplateSlotCount = Mathf.Min(predesignedOverflowTemplateSlotCount, predesignedSafeSlots.Count);
        }

        if (!Application.isPlaying) RecalculateLocalState();
    }

    public bool TryAddLoot(GroceryLootDefinition lootDefinition)
    {
        if (lootDefinition == null) return false;
        return TryAddCargoEntry(new CargoEntry(lootDefinition));
    }

    public bool TryAddCargoEntry(CargoEntry entry)
    {
        if (entry == null) return false;

        if (settings == null)
        {
            Debug.LogError("[ChainCartCargo] Cannot add cargo because CargoSystemSettings is missing.", this);
            return false;
        }

        if (cargoEntries.Count >= settings.TechnicalMaxCargoPerCart)
        {
            Debug.LogError($"[ChainCartCargo] Technical cargo safety limit reached ({settings.TechnicalMaxCargoPerCart}).", this);
            return false;
        }

        cargoEntries.Add(entry);
        RecalculateLocalState();

        if (Application.isPlaying)
        {
            EnsureRuntimeVisualRoot();
            SpawnVisualForEntry(entry, cargoEntries.Count - 1);
        }

        OnCargoAdded?.Invoke(this, entry);
        OnCargoChanged?.Invoke(this);
        return true;
    }

    public bool RemoveCargo(CargoEntry entry)
    {
        if (entry == null) return false;
        int index = cargoEntries.IndexOf(entry);
        return index >= 0 && RemoveCargoAt(index);
    }

    public bool RemoveCargoAt(int index)
    {
        if (index < 0 || index >= cargoEntries.Count) return false;

        CargoEntry removedEntry = cargoEntries[index];
        DestroyEntryVisual(removedEntry);
        cargoEntries.RemoveAt(index);
        RecalculateLocalState();

        if (Application.isPlaying && index < cargoEntries.Count) RebuildCargoVisuals();

        OnCargoRemoved?.Invoke(this, removedEntry);
        OnCargoChanged?.Invoke(this);
        return true;
    }

    public CargoEntry RemoveLastCargo()
    {
        if (cargoEntries.Count == 0) return null;

        int lastIndex = cargoEntries.Count - 1;
        CargoEntry removedEntry = cargoEntries[lastIndex];

        DestroyEntryVisual(removedEntry);
        cargoEntries.RemoveAt(lastIndex);
        RecalculateLocalState();

        OnCargoRemoved?.Invoke(this, removedEntry);
        OnCargoChanged?.Invoke(this);
        return removedEntry;
    }

    public int RemoveOverloadCargo(List<CargoEntry> removedEntries)
    {
        if (settings == null || cargoEntries.Count == 0) return 0;

        int removedCount = 0;

        while (cargoEntries.Count > 0 && localLoad > SafeCapacity)
        {
            CargoEntry removedEntry = RemoveLastCargo();
            if (removedEntry == null) break;

            removedEntries?.Add(removedEntry);
            removedCount++;
        }

        return removedCount;
    }

    /// <summary>
    /// Replaces this cart's cargo layout with an existing set of CargoEntry
    /// instances without treating the operation as pickup/removal gameplay.
    ///
    /// Intended for authoritative layout rebuilds such as checkout compaction.
    /// CargoEntry identity, SourceLoot, SelectedCargoVisual, LoadCost and
    /// ScoreValue are all preserved.
    ///
    /// This fires OnCargoChanged once after the replacement. It deliberately
    /// does NOT fire OnCargoAdded / OnCargoRemoved for every moved entry.
    /// </summary>
    public bool ReplaceCargoEntries(IReadOnlyList<CargoEntry> entries)
    {
        if (settings == null)
        {
            Debug.LogError("[ChainCartCargo] Cannot replace cargo because CargoSystemSettings is missing.", this);
            return false;
        }

        int newCount = entries != null ? entries.Count : 0;

        if (newCount > settings.TechnicalMaxCargoPerCart)
        {
            Debug.LogError(
                $"[ChainCartCargo] Cannot replace cargo: requested {newCount} entries exceeds technical safety limit {settings.TechnicalMaxCargoPerCart}.",
                this
            );

            return false;
        }

        // Clear runtime visuals for the OLD layout first.
        for (int i = 0; i < cargoEntries.Count; i++)
        {
            cargoEntries[i]?.ClearVisualRuntime();
        }

        ClearSpawnedVisuals();
        cargoEntries.Clear();

        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                CargoEntry entry = entries[i];
                if (entry != null) cargoEntries.Add(entry);
            }
        }

        RecalculateLocalState();

        if (Application.isPlaying) RebuildCargoVisuals();

        OnCargoChanged?.Invoke(this);
        return true;
    }

    public void ClearCargo()
    {
        if (cargoEntries.Count == 0)
        {
            ClearSpawnedVisuals();
            RecalculateLocalState();
            return;
        }

        for (int i = 0; i < cargoEntries.Count; i++)
        {
            CargoEntry entry = cargoEntries[i];

            if (entry != null)
            {
                DestroyEntryVisual(entry);
                OnCargoRemoved?.Invoke(this, entry);
            }
        }

        cargoEntries.Clear();
        RecalculateLocalState();
        ClearSpawnedVisuals();
        OnCargoChanged?.Invoke(this);
    }

    public bool HasSafeCapacityFor(GroceryLootDefinition lootDefinition)
    {
        return settings != null && lootDefinition != null && localLoad + lootDefinition.LoadCost <= SafeCapacity;
    }

    public bool HasSafeCapacityFor(CargoEntry entry)
    {
        return settings != null && entry != null && localLoad + entry.LoadCost <= SafeCapacity;
    }

    public int GetRemainingSafeCapacity()
    {
        return Mathf.Max(0, SafeCapacity - localLoad);
    }

    /// <summary>
    /// Finds one cargo entry that can be moved away while this cart is locally
    /// overloaded and that fits within the supplied destination safe capacity.
    ///
    /// Search is newest/highest slot -> oldest so prototype/production visuals
    /// remove overflow from the top/end first.
    ///
    /// This only SELECTS the entry. The caller must remove it explicitly with
    /// RemoveCargo(entry) once a destination has been chosen.
    /// </summary>
    public bool TryGetTransferableOverloadEntry(int maxLoadCost, out CargoEntry entry)
    {
        entry = null;

        if (!IsOverloaded || maxLoadCost <= 0) return false;

        for (int i = cargoEntries.Count - 1; i >= 0; i--)
        {
            CargoEntry candidate = cargoEntries[i];
            if (candidate == null) continue;
            if (candidate.LoadCost <= 0 || candidate.LoadCost > maxLoadCost) continue;

            entry = candidate;
            return true;
        }

        return false;
    }

    private void RecalculateLocalState()
    {
        localLoad = 0;
        localScoreValue = 0;

        for (int i = 0; i < cargoEntries.Count; i++)
        {
            CargoEntry entry = cargoEntries[i];
            if (entry == null) continue;

            localLoad += Mathf.Max(0, entry.LoadCost);
            localScoreValue += Mathf.Max(0, entry.ScoreValue);
        }

        int safeCapacity = settings != null ? settings.CapacityPerCart : 0;
        int safeVisualSlots = GetVisualSafeSlotCount();

        localOverload = Mathf.Max(0, localLoad - safeCapacity);
        visualOverflowEntryCount = Mathf.Max(0, cargoEntries.Count - safeVisualSlots);
    }

    public void RebuildCargoVisuals()
    {
        if (!Application.isPlaying) return;

        EnsureRuntimeVisualRoot();
        ClearSpawnedVisuals();

        for (int i = 0; i < cargoEntries.Count; i++)
        {
            CargoEntry entry = cargoEntries[i];
            if (entry != null) SpawnVisualForEntry(entry, i);
        }
    }

    private void SpawnVisualForEntry(CargoEntry entry, int slotIndex)
    {
        if (entry == null || settings == null) return;

        EnsureRuntimeVisualRoot();
        GetCargoSlotLocalPose(slotIndex, out Vector3 localPosition, out Quaternion localRotation);

        CargoVisualDefinition visualDefinition = entry.SelectedCargoVisual;
        GameObject visualInstance;

        if (visualDefinition != null && visualDefinition.CargoVisualPrefab != null)
        {
            visualInstance = Instantiate(visualDefinition.CargoVisualPrefab, runtimeVisualRoot);
            visualInstance.name = $"Cargo_{slotIndex}_{visualDefinition.DisplayName}";
            visualInstance.transform.localPosition = localPosition;
            visualInstance.transform.localRotation = localRotation;
        }
        else
        {
            visualInstance = CreatePrototypeSphere(entry, slotIndex, localPosition, localRotation);
        }

        entry.SetVisualRuntime(slotIndex, visualInstance);
    }

    private GameObject CreatePrototypeSphere(CargoEntry entry, int slotIndex, Vector3 localPosition, Quaternion localRotation)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = $"DEBUG_Cargo_{slotIndex}_{entry.Category}";
        sphere.transform.SetParent(runtimeVisualRoot, false);
        sphere.transform.localPosition = localPosition;
        sphere.transform.localRotation = localRotation;

        CargoVisualDefinition visualDefinition = entry.SelectedCargoVisual;
        float multiplier = visualDefinition != null ? visualDefinition.DebugScaleMultiplier : 1f;
        sphere.transform.localScale = Vector3.one * settings.PrototypeCargoSphereScale * multiplier;

        Collider sphereCollider = sphere.GetComponent<Collider>();
        if (sphereCollider != null)
        {
            sphereCollider.enabled = false;
            Destroy(sphereCollider);
        }

        Renderer sphereRenderer = sphere.GetComponent<Renderer>();
        if (sphereRenderer != null)
        {
            if (prototypeSphereMaterial != null) sphereRenderer.sharedMaterial = prototypeSphereMaterial;

            if (materialPropertyBlock == null) materialPropertyBlock = new MaterialPropertyBlock();

            Color debugColor = GetDebugColor(entry);
            sphereRenderer.GetPropertyBlock(materialPropertyBlock);
            materialPropertyBlock.SetColor("_BaseColor", debugColor);
            materialPropertyBlock.SetColor("_Color", debugColor);
            sphereRenderer.SetPropertyBlock(materialPropertyBlock);
        }

        return sphere;
    }

    private Color GetDebugColor(CargoEntry entry)
    {
        if (entry != null && entry.SelectedCargoVisual != null) return entry.SelectedCargoVisual.DebugColor;
        if (entry != null && entry.SourceLoot != null) return entry.SourceLoot.WorldDebugColor;
        return Color.white;
    }

    public void GetCargoSlotLocalPose(int slotIndex, out Vector3 localPosition, out Quaternion localRotation)
    {
        if (slotIndex < 0)
        {
            localPosition = Vector3.zero;
            localRotation = Quaternion.identity;
            return;
        }

        if (usePredesignedSlotPositions && HasUsablePredesignedSlots())
        {
            GetPredesignedSlotLocalPose(slotIndex, out localPosition, out localRotation);
            return;
        }

        localPosition = CalculatePrototypeSlotLocalPosition(slotIndex);
        localRotation = Quaternion.identity;
    }

    private void GetPredesignedSlotLocalPose(int slotIndex, out Vector3 localPosition, out Quaternion localRotation)
    {
        EnsureRuntimeVisualRoot();

        int safeSlotCount = predesignedSafeSlots.Count;

        if (slotIndex < safeSlotCount)
        {
            GetAuthoredTransformLocalPose(predesignedSafeSlots[slotIndex], out localPosition, out localRotation);
            return;
        }

        int templateCount = Mathf.Clamp(predesignedOverflowTemplateSlotCount, 1, safeSlotCount);
        int templateStartIndex = safeSlotCount - templateCount;

        int overflowIndex = slotIndex - safeSlotCount;
        int indexInOverflowLayer = overflowIndex % templateCount;
        int overflowLayerIndex = overflowIndex / templateCount + 1;

        Transform templateSlot = predesignedSafeSlots[templateStartIndex + indexInOverflowLayer];

        GetAuthoredTransformLocalPose(templateSlot, out localPosition, out localRotation);
        localPosition += predesignedOverflowLayerOffset * overflowLayerIndex;
    }

    private void GetAuthoredTransformLocalPose(Transform slot, out Vector3 localPosition, out Quaternion localRotation)
    {
        if (runtimeVisualRoot == null || slot == null)
        {
            localPosition = Vector3.zero;
            localRotation = Quaternion.identity;
            return;
        }

        localPosition = runtimeVisualRoot.InverseTransformPoint(slot.position);

        localRotation = usePredesignedSlotRotations
            ? Quaternion.Inverse(runtimeVisualRoot.rotation) * slot.rotation
            : Quaternion.identity;
    }

    private int GetVisualSafeSlotCount()
    {
        if (usePredesignedSlotPositions && HasUsablePredesignedSlots()) return predesignedSafeSlots.Count;
        return settings != null ? settings.PrototypeSafeVisualSlotCount : 0;
    }

    private bool HasUsablePredesignedSlots()
    {
        if (predesignedSafeSlots == null || predesignedSafeSlots.Count == 0) return false;

        for (int i = 0; i < predesignedSafeSlots.Count; i++)
        {
            if (predesignedSafeSlots[i] == null) return false;
        }

        return true;
    }

    private void ValidatePredesignedSlotSetup()
    {
        if (!usePredesignedSlotPositions) return;

        if (!HasUsablePredesignedSlots())
        {
            Debug.LogWarning(
                "[ChainCartCargo] Predesigned slot mode is enabled, but the slot list is empty or contains null entries. Falling back to the procedural prototype layout.",
                this
            );

            return;
        }

        if (settings != null && predesignedSafeSlots.Count != settings.CapacityPerCart)
        {
            Debug.LogWarning(
                $"[ChainCartCargo] Predesigned safe slot count ({predesignedSafeSlots.Count}) does not match gameplay Capacity Per Cart ({settings.CapacityPerCart}). " +
                "This is allowed, but visual overflow and gameplay overload will begin at different points.",
                this
            );
        }
    }

    public Vector3 CalculatePrototypeSlotLocalPosition(int slotIndex)
    {
        if (settings == null || slotIndex < 0) return Vector3.zero;

        int slotsPerLayer = Mathf.Max(1, settings.PrototypeSlotsPerLayer);
        int layerIndex = slotIndex / slotsPerLayer;
        int indexInLayer = slotIndex % slotsPerLayer;

        float centeredIndex = indexInLayer - (slotsPerLayer - 1) * 0.5f;
        float x = centeredIndex * settings.PrototypeHorizontalSpacing;
        float y = layerIndex * settings.PrototypeLayerHeight;
        float z = 0f;

        if (layerIndex > 0 && settings.PrototypeLayerDepthStagger > 0f)
        {
            z = (layerIndex % 2 == 1 ? 1f : -1f) * settings.PrototypeLayerDepthStagger;
        }

        return settings.PrototypeCargoCenter + new Vector3(x, y, z);
    }

    private void DestroyEntryVisual(CargoEntry entry)
    {
        if (entry == null) return;

        if (entry.VisualInstance != null) Destroy(entry.VisualInstance);
        entry.ClearVisualRuntime();
    }

    private void EnsureRuntimeVisualRoot()
    {
        if (runtimeVisualRoot != null) return;

        Transform parent = cargoVisualAnchor != null ? cargoVisualAnchor : transform;
        Transform existing = parent.Find(RuntimeVisualRootName);

        if (existing != null)
        {
            runtimeVisualRoot = existing;
            return;
        }

        GameObject rootObject = new GameObject(RuntimeVisualRootName);
        runtimeVisualRoot = rootObject.transform;
        runtimeVisualRoot.SetParent(parent, false);
        runtimeVisualRoot.localPosition = Vector3.zero;
        runtimeVisualRoot.localRotation = Quaternion.identity;
        runtimeVisualRoot.localScale = Vector3.one;
    }

    private void ClearSpawnedVisuals()
    {
        if (runtimeVisualRoot == null) return;

        for (int i = runtimeVisualRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = runtimeVisualRoot.GetChild(i);
            if (child != null) Destroy(child.gameObject);
        }

        for (int i = 0; i < cargoEntries.Count; i++)
        {
            cargoEntries[i]?.ClearVisualRuntime();
        }
    }

    [ContextMenu("TEST - Add First Loot")]
    private void DebugAddFirstLoot()
    {
        if (!RequirePlayModeForTest()) return;
        GroceryLootDefinition loot = GetFirstValidDebugLoot();

        if (loot == null)
        {
            Debug.LogWarning("[ChainCartCargo] Assign at least one Debug Test Loot Definition first.", this);
            return;
        }

        TryAddLoot(loot);
    }

    [ContextMenu("TEST - Add Random Loot")]
    private void DebugAddRandomLoot()
    {
        if (!RequirePlayModeForTest()) return;
        GroceryLootDefinition loot = GetRandomValidDebugLoot();

        if (loot == null)
        {
            Debug.LogWarning("[ChainCartCargo] Assign at least one Debug Test Loot Definition first.", this);
            return;
        }

        TryAddLoot(loot);
    }

    [ContextMenu("TEST - Fill To Safe Capacity")]
    private void DebugFillToSafeCapacity()
    {
        if (!RequirePlayModeForTest()) return;
        GroceryLootDefinition loot = GetFirstValidDebugLoot();

        if (loot == null)
        {
            Debug.LogWarning("[ChainCartCargo] Assign at least one Debug Test Loot Definition first.", this);
            return;
        }

        int safety = 0;
        while (localLoad < SafeCapacity && safety < 1000)
        {
            if (!TryAddLoot(loot)) break;
            safety++;
        }
    }

    [ContextMenu("TEST - Add One Overflow Round")]
    private void DebugAddOneOverflowRound()
    {
        if (!RequirePlayModeForTest() || settings == null) return;
        GroceryLootDefinition loot = GetFirstValidDebugLoot();

        if (loot == null)
        {
            Debug.LogWarning("[ChainCartCargo] Assign at least one Debug Test Loot Definition first.", this);
            return;
        }

        for (int i = 0; i < settings.OverflowRoundSize; i++)
        {
            if (!TryAddLoot(loot)) break;
        }
    }

    [ContextMenu("TEST - Remove Last Cargo")]
    private void DebugRemoveLastCargo()
    {
        if (!RequirePlayModeForTest()) return;
        RemoveLastCargo();
    }

    [ContextMenu("TEST - Spill Local Overload")]
    private void DebugSpillLocalOverload()
    {
        if (!RequirePlayModeForTest()) return;

        List<CargoEntry> removed = new List<CargoEntry>();
        int count = RemoveOverloadCargo(removed);

        Debug.Log($"[ChainCartCargo] TEST spill removed {count}. Remaining Load:{LocalLoad} / Safe Capacity:{SafeCapacity}", this);
    }

    [ContextMenu("TEST - Clear Cargo")]
    private void DebugClearCargo()
    {
        if (!RequirePlayModeForTest()) return;
        ClearCargo();
    }

    [ContextMenu("TEST - Rebuild Visuals")]
    private void DebugRebuildVisuals()
    {
        if (!RequirePlayModeForTest()) return;
        RebuildCargoVisuals();
    }

    private GroceryLootDefinition GetFirstValidDebugLoot()
    {
        if (debugTestLootDefinitions == null) return null;

        for (int i = 0; i < debugTestLootDefinitions.Length; i++)
        {
            if (debugTestLootDefinitions[i] != null) return debugTestLootDefinitions[i];
        }

        return null;
    }

    private GroceryLootDefinition GetRandomValidDebugLoot()
    {
        if (debugTestLootDefinitions == null || debugTestLootDefinitions.Length == 0) return null;

        int startIndex = UnityEngine.Random.Range(0, debugTestLootDefinitions.Length);

        for (int offset = 0; offset < debugTestLootDefinitions.Length; offset++)
        {
            int index = (startIndex + offset) % debugTestLootDefinitions.Length;
            if (debugTestLootDefinitions[index] != null) return debugTestLootDefinitions[index];
        }

        return null;
    }

    private bool RequirePlayModeForTest()
    {
        if (Application.isPlaying) return true;

        Debug.LogWarning("[ChainCartCargo] Prototype Context Menu tests are Play Mode only.", this);
        return false;
    }
}
