using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player/chain-level coordinator for cargo capacity and loot allocation.
///
/// Authoritative cargo data remains on each ChainCartCargo.
/// This controller:
/// - maintains the ordered set of CURRENTLY OWNED follower carts;
/// - aggregates TotalCapacity / TotalLoad / TotalScore / GlobalOverload;
/// - chooses which physical cart receives newly collected GroceryLoot;
/// - follows the front-to-tail safe-slot rule for NEW loot;
/// - distributes overload in configurable rounds;
/// - when owned capacity increases, transfers existing LOCAL overload into
///   newly available safe capacity, checking overloaded donors tail-to-front;
/// - exposes checkout-facing cargo totals;
/// - can compact all owned cargo into a canonical front-to-tail checkout layout.
///
/// It does NOT:
/// - continuously rebalance safe cargo during normal play;
/// - apply Hype burn yet;
/// - own score/reward rules.
/// </summary>
[DisallowMultipleComponent]
public class CargoCapacityController : MonoBehaviour
{
    #region References

    [Header("References")]
    [SerializeField] private SnakeCartManager snakeCartManager;
    [SerializeField] private CargoSystemSettings settings;

    #endregion

    #region Prototype Test Data

    [Header("Prototype Test Data")]
    [Tooltip("Temporary GroceryLootDefinition assets used by Context Menu allocation tests.")]
    [SerializeField] private GroceryLootDefinition[] debugTestLootDefinitions;

    #endregion

    #region Runtime Debug

    [Header("Owned Cargo Carts - Read Only")]
    [SerializeField] private int ownedFollowerCount;
    [SerializeField] private int registeredCargoCartCount;
    [SerializeField] private int missingCargoComponentCount;

    [Header("Global Cargo State - Read Only")]
    [SerializeField] private int totalCapacity;
    [SerializeField] private int totalLoad;
    [SerializeField] private int totalScoreValue;
    [SerializeField] private int totalCargoEntryCount;
    [SerializeField] private int loadedCargoCartCount;
    [SerializeField] private int overloadAmount;
    [SerializeField] private bool isOverloaded;
    [SerializeField] private float capacityNormalized;
    [SerializeField] private float movementSpeedMultiplier = 1f;

    [Header("Allocation Debug - Read Only")]
    [SerializeField] private string lastAssignedCartName;
    [SerializeField] private int lastAssignedOwnedIndex = -1;
    [SerializeField] private bool lastAssignmentWasOverflow;
    [SerializeField] private int lastSelectedOverflowRound;

    [Header("Overload Transfer Debug - Read Only")]
    [SerializeField] private int lastNormalizationTransferCount;
    [SerializeField] private bool lastNormalizationWasCapacityIncrease;

    [Header("Checkout Compaction Debug - Read Only")]
    [SerializeField] private bool lastCheckoutCompactionNeeded;
    [SerializeField] private int lastCheckoutCompactionMovedEntries;

    #endregion

    #region Runtime Collections

    private readonly List<GameObject> ownedFollowerObjects = new List<GameObject>(16);
    private readonly List<ChainCartCargo> orderedCargoCarts = new List<ChainCartCargo>(16);

    private readonly List<CargoEntry> checkoutCargoBuffer = new List<CargoEntry>(64);
    private readonly List<CargoEntry> checkoutOverflowBuffer = new List<CargoEntry>(32);
    private readonly List<List<CargoEntry>> checkoutDistributionBuffers = new List<List<CargoEntry>>(16);
    private readonly List<int> checkoutDistributionLoads = new List<int>(16);
    private readonly List<int> checkoutOverflowEntryCounts = new List<int>(16);
    private readonly Dictionary<CargoEntry, int> checkoutOriginalOwnerIndex = new Dictionary<CargoEntry, int>(64);

    private bool isNormalizingCargoTransfers;
    private bool isRebuildingCargoLayout;

    #endregion

    #region Public State

    public int OwnedFollowerCount => ownedFollowerCount;
    public int RegisteredCargoCartCount => registeredCargoCartCount;
    public int MissingCargoComponentCount => missingCargoComponentCount;

    public int TotalCapacity => totalCapacity;
    public int TotalLoad => totalLoad;
    public int TotalScoreValue => totalScoreValue;
    public int TotalCargoEntryCount => totalCargoEntryCount;
    public int LoadedCargoCartCount => loadedCargoCartCount;
    public bool HasCheckoutCargo => totalCargoEntryCount > 0;
    public int OverloadAmount => overloadAmount;
    public bool IsOverloaded => isOverloaded;
    public float CapacityNormalized => capacityNormalized;

    /// <summary>
    /// Cached movement multiplier derived from GLOBAL overload.
    /// It refreshes whenever owned carts or cargo state changes.
    /// </summary>
    public float MovementSpeedMultiplier => movementSpeedMultiplier;

    public IReadOnlyList<ChainCartCargo> OrderedCargoCarts => orderedCargoCarts;

    #endregion

    #region Events

    public event Action<int> OnCartCountChanged;
    public event Action<int> OnCapacityChanged;
    public event Action<int> OnLoadChanged;
    public event Action<int> OnScoreValueChanged;
    public event Action<int> OnCargoEntryCountChanged;
    public event Action<int> OnLoadedCargoCartCountChanged;
    public event Action<int> OnOverloadChanged;
    public event Action<float> OnMovementSpeedMultiplierChanged;

    public event Action<CargoEntry, ChainCartCargo> OnCargoAdded;
    public event Action<CargoEntry, ChainCartCargo> OnCargoRemoved;

    /// <summary>
    /// Fired for automatic overload normalization transfers.
    /// This is NOT a new loot pickup and does not change score/load totals.
    /// </summary>
    public event Action<CargoEntry, ChainCartCargo, ChainCartCargo> OnCargoTransferred;

    /// <summary>
    /// Fired after a successful checkout layout rebuild.
    /// Argument = number of CargoEntry instances whose owning cart changed.
    /// </summary>
    public event Action<int> OnCheckoutCargoCompacted;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (snakeCartManager == null) snakeCartManager = GetComponent<SnakeCartManager>();
        if (settings == null) Debug.LogError("[CargoCapacityController] CargoSystemSettings is not assigned.", this);
        if (snakeCartManager == null) Debug.LogError("[CargoCapacityController] SnakeCartManager is missing.", this);
    }

    private void OnEnable()
    {
        if (snakeCartManager != null)
        {
            snakeCartManager.OnOwnedFollowersChanged -= HandleOwnedFollowersChanged;
            snakeCartManager.OnOwnedFollowersChanged += HandleOwnedFollowersChanged;
        }
    }

    private void Start()
    {
        RefreshOwnedCargoCarts();
    }

    private void OnDisable()
    {
        if (snakeCartManager != null) snakeCartManager.OnOwnedFollowersChanged -= HandleOwnedFollowersChanged;
        UnsubscribeFromCargoCarts();
    }

    #endregion

    #region Owned Cart Registration

    private void HandleOwnedFollowersChanged()
    {
        RefreshOwnedCargoCarts();
    }

    public void RefreshOwnedCargoCarts()
    {
        int previousOwnedFollowerCount = ownedFollowerCount;
        int previousCapacity = totalCapacity;

        UnsubscribeFromCargoCarts();

        ownedFollowerObjects.Clear();
        orderedCargoCarts.Clear();

        missingCargoComponentCount = 0;

        if (snakeCartManager != null)
        {
            snakeCartManager.FillOwnedFollowerCarts(ownedFollowerObjects);
        }

        ownedFollowerCount = ownedFollowerObjects.Count;

        for (int i = 0; i < ownedFollowerObjects.Count; i++)
        {
            GameObject cartObject = ownedFollowerObjects[i];
            if (cartObject == null) continue;

            ChainCartCargo cargo = cartObject.GetComponentInChildren<ChainCartCargo>(true);

            if (cargo == null)
            {
                missingCargoComponentCount++;

                Debug.LogError(
                    $"[CargoCapacityController] Owned follower '{cartObject.name}' is missing ChainCartCargo.",
                    cartObject
                );

                continue;
            }

            if (settings != null && cargo.Settings != settings)
            {
                Debug.LogWarning(
                    $"[CargoCapacityController] '{cartObject.name}' uses a different CargoSystemSettings asset than the chain controller.",
                    cargo
                );
            }

            orderedCargoCarts.Add(cargo);
            SubscribeToCargoCart(cargo);
        }

        if (ownedFollowerCount != previousOwnedFollowerCount) OnCartCountChanged?.Invoke(ownedFollowerCount);

        int rebuiltCapacity = CalculateRegisteredCapacity();
        bool capacityIncreased = rebuiltCapacity > previousCapacity;

        lastNormalizationWasCapacityIncrease = capacityIncreased;
        lastNormalizationTransferCount = capacityIncreased
            ? NormalizeLocalOverloadIntoSafeCapacity()
            : 0;

        RecalculateGlobalState();
    }

    private int CalculateRegisteredCapacity()
    {
        int capacity = 0;

        for (int i = 0; i < orderedCargoCarts.Count; i++)
        {
            ChainCartCargo cargo = orderedCargoCarts[i];
            if (cargo != null) capacity += Mathf.Max(0, cargo.SafeCapacity);
        }

        return capacity;
    }

    private void SubscribeToCargoCart(ChainCartCargo cargo)
    {
        if (cargo == null) return;

        cargo.OnCargoAdded -= HandleCartCargoAdded;
        cargo.OnCargoRemoved -= HandleCartCargoRemoved;
        cargo.OnCargoChanged -= HandleCartCargoChanged;

        cargo.OnCargoAdded += HandleCartCargoAdded;
        cargo.OnCargoRemoved += HandleCartCargoRemoved;
        cargo.OnCargoChanged += HandleCartCargoChanged;
    }

    private void UnsubscribeFromCargoCarts()
    {
        for (int i = 0; i < orderedCargoCarts.Count; i++)
        {
            ChainCartCargo cargo = orderedCargoCarts[i];
            if (cargo == null) continue;

            cargo.OnCargoAdded -= HandleCartCargoAdded;
            cargo.OnCargoRemoved -= HandleCartCargoRemoved;
            cargo.OnCargoChanged -= HandleCartCargoChanged;
        }
    }

    #endregion

    #region Global State

    public void RecalculateGlobalState()
    {
        int previousCapacity = totalCapacity;
        int previousLoad = totalLoad;
        int previousScore = totalScoreValue;
        int previousEntryCount = totalCargoEntryCount;
        int previousLoadedCartCount = loadedCargoCartCount;
        int previousOverload = overloadAmount;
        float previousMovementSpeedMultiplier = movementSpeedMultiplier;

        registeredCargoCartCount = 0;
        totalCapacity = 0;
        totalLoad = 0;
        totalScoreValue = 0;
        totalCargoEntryCount = 0;
        loadedCargoCartCount = 0;

        for (int i = 0; i < orderedCargoCarts.Count; i++)
        {
            ChainCartCargo cargo = orderedCargoCarts[i];
            if (cargo == null) continue;

            registeredCargoCartCount++;
            totalCapacity += Mathf.Max(0, cargo.SafeCapacity);
            totalLoad += Mathf.Max(0, cargo.LocalLoad);
            totalScoreValue += Mathf.Max(0, cargo.LocalScoreValue);
            totalCargoEntryCount += Mathf.Max(0, cargo.CargoEntryCount);

            if (cargo.CargoEntryCount > 0) loadedCargoCartCount++;
        }

        overloadAmount = Mathf.Max(0, totalLoad - totalCapacity);
        isOverloaded = overloadAmount > 0;

        capacityNormalized = totalCapacity > 0
            ? Mathf.Clamp01(totalLoad / (float)totalCapacity)
            : 0f;

        movementSpeedMultiplier = settings != null ? settings.GetOverloadSpeedMultiplier(overloadAmount) : 1f;

        if (totalCapacity != previousCapacity) OnCapacityChanged?.Invoke(totalCapacity);
        if (totalLoad != previousLoad) OnLoadChanged?.Invoke(totalLoad);
        if (totalScoreValue != previousScore) OnScoreValueChanged?.Invoke(totalScoreValue);
        if (totalCargoEntryCount != previousEntryCount) OnCargoEntryCountChanged?.Invoke(totalCargoEntryCount);
        if (loadedCargoCartCount != previousLoadedCartCount) OnLoadedCargoCartCountChanged?.Invoke(loadedCargoCartCount);
        if (overloadAmount != previousOverload) OnOverloadChanged?.Invoke(overloadAmount);
        if (!Mathf.Approximately(movementSpeedMultiplier, previousMovementSpeedMultiplier)) OnMovementSpeedMultiplierChanged?.Invoke(movementSpeedMultiplier);
    }

    private void HandleCartCargoAdded(ChainCartCargo cart, CargoEntry entry)
    {
        if (isNormalizingCargoTransfers || isRebuildingCargoLayout) return;
        OnCargoAdded?.Invoke(entry, cart);
    }

    private void HandleCartCargoRemoved(ChainCartCargo cart, CargoEntry entry)
    {
        if (isNormalizingCargoTransfers || isRebuildingCargoLayout) return;
        OnCargoRemoved?.Invoke(entry, cart);
    }

    private void HandleCartCargoChanged(ChainCartCargo cart)
    {
        if (isNormalizingCargoTransfers || isRebuildingCargoLayout) return;
        RecalculateGlobalState();
    }

    #endregion

    #region Capacity-Increase Overload Normalization

    /// <summary>
    /// Moves only LOCAL overload cargo into currently unused SAFE capacity.
    ///
    /// Donor priority is TAIL -> FRONT so limited new capacity clears later
    /// carts first and leaves any unavoidable overload concentrated toward C1.
    ///
    /// Receiver priority is FRONT -> TAIL so transferred overload fills the
    /// earliest available safe capacity in normal chain order.
    ///
    /// Safe cargo is never generally rebalanced.
    /// </summary>
    public int NormalizeLocalOverloadIntoSafeCapacity()
    {
        if (orderedCargoCarts.Count <= 1) return 0;

        int transferCount = 0;
        isNormalizingCargoTransfers = true;

        try
        {
            for (int donorIndex = orderedCargoCarts.Count - 1; donorIndex >= 0; donorIndex--)
            {
                ChainCartCargo donor = orderedCargoCarts[donorIndex];
                if (donor == null) continue;

                while (donor.IsOverloaded)
                {
                    if (!TryFindTransferDestinationAndEntry(
                            donorIndex,
                            donor,
                            out ChainCartCargo destination,
                            out CargoEntry entry))
                    {
                        break;
                    }

                    if (!donor.RemoveCargo(entry)) break;

                    if (!destination.TryAddCargoEntry(entry))
                    {
                        // Defensive rollback. Under normal setup this should not
                        // fail because destination safe capacity and technical
                        // capacity were checked before removing from the donor.
                        donor.TryAddCargoEntry(entry);

                        Debug.LogError(
                            $"[CargoCapacityController] Failed to transfer cargo from '{donor.name}' to '{destination.name}'. Entry was restored to the donor.",
                            this
                        );

                        break;
                    }

                    transferCount++;
                    OnCargoTransferred?.Invoke(entry, donor, destination);
                }
            }
        }
        finally
        {
            isNormalizingCargoTransfers = false;
        }

        return transferCount;
    }

    private bool TryFindTransferDestinationAndEntry(
        int donorIndex,
        ChainCartCargo donor,
        out ChainCartCargo destination,
        out CargoEntry entry)
    {
        destination = null;
        entry = null;

        if (donor == null || !donor.IsOverloaded) return false;

        // Front -> tail receiver search. Donor priority and destination
        // priority are intentionally different:
        // - choose overloaded donors tail -> front;
        // - place transferred cargo into safe capacity front -> tail.
        for (int destinationIndex = 0; destinationIndex < orderedCargoCarts.Count; destinationIndex++)
        {
            if (destinationIndex == donorIndex) continue;

            ChainCartCargo candidateDestination = orderedCargoCarts[destinationIndex];
            if (!CanTechnicallyAcceptCargo(candidateDestination)) continue;

            int remainingSafeCapacity = candidateDestination.GetRemainingSafeCapacity();
            if (remainingSafeCapacity <= 0) continue;

            if (!donor.TryGetTransferableOverloadEntry(remainingSafeCapacity, out CargoEntry candidateEntry)) continue;

            destination = candidateDestination;
            entry = candidateEntry;
            return true;
        }

        return false;
    }

    #endregion

    #region Checkout Cargo Compaction

    /// <summary>
    /// Returns the first owned follower cart containing at least one CargoEntry,
    /// using the normal active-front-to-tail then pending order.
    /// </summary>
    public ChainCartCargo GetFirstLoadedCargoCart()
    {
        for (int i = 0; i < orderedCargoCarts.Count; i++)
        {
            ChainCartCargo cargo = orderedCargoCarts[i];
            if (cargo != null && cargo.CargoEntryCount > 0) return cargo;
        }

        return null;
    }

    /// <summary>
    /// Returns true when the current cargo assignment differs from the canonical
    /// checkout layout.
    ///
    /// Canonical checkout layout:
    /// 1) Gather existing CargoEntry instances in current front-to-tail order.
    /// 2) Fill gameplay-safe capacity front-to-tail.
    /// 3) If globally overloaded, distribute remaining entries using the normal
    ///    front-to-tail OverflowRoundSize rule.
    ///
    /// This guarantees loaded carts are packed toward the front and empty carts
    /// are pushed toward the tail whenever the current LoadCost configuration
    /// allows it.
    /// </summary>
    public bool NeedsCheckoutCompaction()
    {
        if (orderedCargoCarts.Count <= 1 || totalCargoEntryCount <= 0) return false;
        if (!TryBuildCheckoutDistribution(out _)) return false;

        return !DoesCurrentLayoutMatchCheckoutDistribution();
    }

    /// <summary>
    /// Rebuilds cargo ownership across the CURRENTLY OWNED follower carts into
    /// canonical checkout order while preserving every CargoEntry instance.
    ///
    /// Returns true when a valid checkout distribution was built/applied.
    /// movedEntryCount reports how many entries changed owning carts.
    /// </summary>
    public bool CompactCargoForCheckout(out int movedEntryCount)
    {
        movedEntryCount = 0;
        lastCheckoutCompactionMovedEntries = 0;

        if (orderedCargoCarts.Count == 0)
        {
            lastCheckoutCompactionNeeded = false;
            return true;
        }

        if (!TryBuildCheckoutDistribution(out movedEntryCount))
        {
            lastCheckoutCompactionNeeded = false;
            return false;
        }

        bool layoutChanged = !DoesCurrentLayoutMatchCheckoutDistribution();

        lastCheckoutCompactionNeeded = layoutChanged;
        lastCheckoutCompactionMovedEntries = movedEntryCount;

        if (!layoutChanged)
        {
            movedEntryCount = 0;
            lastCheckoutCompactionMovedEntries = 0;
            return true;
        }

        isRebuildingCargoLayout = true;

        bool success = true;

        try
        {
            for (int i = 0; i < orderedCargoCarts.Count; i++)
            {
                ChainCartCargo cargo = orderedCargoCarts[i];
                if (cargo == null) continue;

                if (!cargo.ReplaceCargoEntries(checkoutDistributionBuffers[i]))
                {
                    success = false;
                    Debug.LogError(
                        $"[CargoCapacityController] Checkout compaction failed while rebuilding '{cargo.name}'.",
                        cargo
                    );

                    break;
                }
            }
        }
        finally
        {
            isRebuildingCargoLayout = false;
        }

        RecalculateGlobalState();

        if (!success) return false;

        OnCheckoutCargoCompacted?.Invoke(movedEntryCount);
        return true;
    }

    private bool TryBuildCheckoutDistribution(out int movedEntryCount)
    {
        movedEntryCount = 0;

        PrepareCheckoutDistributionBuffers();
        checkoutCargoBuffer.Clear();
        checkoutOverflowBuffer.Clear();
        checkoutOriginalOwnerIndex.Clear();

        // Preserve global CargoEntry ordering as much as possible:
        // current front cart first, then toward the tail.
        for (int cartIndex = 0; cartIndex < orderedCargoCarts.Count; cartIndex++)
        {
            ChainCartCargo cargo = orderedCargoCarts[cartIndex];
            if (cargo == null) continue;

            IReadOnlyList<CargoEntry> entries = cargo.CargoEntries;

            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                CargoEntry entry = entries[entryIndex];
                if (entry == null) continue;

                checkoutCargoBuffer.Add(entry);
                checkoutOriginalOwnerIndex[entry] = cartIndex;
            }
        }

        // PASS 1: safe gameplay capacity, front -> tail.
        for (int entryIndex = 0; entryIndex < checkoutCargoBuffer.Count; entryIndex++)
        {
            CargoEntry entry = checkoutCargoBuffer[entryIndex];
            if (entry == null) continue;

            bool placedSafely = false;

            for (int cartIndex = 0; cartIndex < orderedCargoCarts.Count; cartIndex++)
            {
                ChainCartCargo cart = orderedCargoCarts[cartIndex];
                if (!CanCheckoutDistributionAcceptAnotherEntry(cartIndex, cart)) continue;

                int safeCapacity = Mathf.Max(0, cart.SafeCapacity);
                int newLoad = checkoutDistributionLoads[cartIndex] + Mathf.Max(0, entry.LoadCost);

                if (newLoad > safeCapacity) continue;

                checkoutDistributionBuffers[cartIndex].Add(entry);
                checkoutDistributionLoads[cartIndex] = newLoad;
                placedSafely = true;
                break;
            }

            if (!placedSafely) checkoutOverflowBuffer.Add(entry);
        }

        // PASS 2: true global overflow. Same entry-round policy as normal pickup,
        // but based on checkout's rebuilt layout instead of existing visuals.
        int overflowRoundSize = settings != null ? Mathf.Max(1, settings.OverflowRoundSize) : 1;

        for (int entryIndex = 0; entryIndex < checkoutOverflowBuffer.Count; entryIndex++)
        {
            CargoEntry entry = checkoutOverflowBuffer[entryIndex];
            if (entry == null) continue;

            int lowestRound = int.MaxValue;

            for (int cartIndex = 0; cartIndex < orderedCargoCarts.Count; cartIndex++)
            {
                ChainCartCargo cart = orderedCargoCarts[cartIndex];
                if (!CanCheckoutDistributionAcceptAnotherEntry(cartIndex, cart)) continue;

                int cartRound = checkoutOverflowEntryCounts[cartIndex] / overflowRoundSize;
                if (cartRound < lowestRound) lowestRound = cartRound;
            }

            if (lowestRound == int.MaxValue)
            {
                Debug.LogError(
                    "[CargoCapacityController] Checkout compaction could not place every CargoEntry before hitting technical safety limits. No layout changes were applied.",
                    this
                );

                return false;
            }

            bool placed = false;

            for (int cartIndex = 0; cartIndex < orderedCargoCarts.Count; cartIndex++)
            {
                ChainCartCargo cart = orderedCargoCarts[cartIndex];
                if (!CanCheckoutDistributionAcceptAnotherEntry(cartIndex, cart)) continue;

                int cartRound = checkoutOverflowEntryCounts[cartIndex] / overflowRoundSize;
                if (cartRound != lowestRound) continue;

                checkoutDistributionBuffers[cartIndex].Add(entry);
                checkoutDistributionLoads[cartIndex] += Mathf.Max(0, entry.LoadCost);
                checkoutOverflowEntryCounts[cartIndex]++;
                placed = true;
                break;
            }

            if (!placed)
            {
                Debug.LogError(
                    "[CargoCapacityController] Checkout compaction failed to choose an overflow destination. No layout changes were applied.",
                    this
                );

                return false;
            }
        }

        for (int cartIndex = 0; cartIndex < checkoutDistributionBuffers.Count; cartIndex++)
        {
            List<CargoEntry> targetEntries = checkoutDistributionBuffers[cartIndex];

            for (int entryIndex = 0; entryIndex < targetEntries.Count; entryIndex++)
            {
                CargoEntry entry = targetEntries[entryIndex];

                if (entry != null &&
                    checkoutOriginalOwnerIndex.TryGetValue(entry, out int oldCartIndex) &&
                    oldCartIndex != cartIndex)
                {
                    movedEntryCount++;
                }
            }
        }

        return true;
    }

    private void PrepareCheckoutDistributionBuffers()
    {
        while (checkoutDistributionBuffers.Count < orderedCargoCarts.Count)
        {
            checkoutDistributionBuffers.Add(new List<CargoEntry>(16));
            checkoutDistributionLoads.Add(0);
            checkoutOverflowEntryCounts.Add(0);
        }

        for (int i = 0; i < checkoutDistributionBuffers.Count; i++)
        {
            checkoutDistributionBuffers[i].Clear();
            checkoutDistributionLoads[i] = 0;
            checkoutOverflowEntryCounts[i] = 0;
        }
    }

    private bool CanCheckoutDistributionAcceptAnotherEntry(int cartIndex, ChainCartCargo cart)
    {
        if (cart == null || cart.Settings == null) return false;
        if (cartIndex < 0 || cartIndex >= checkoutDistributionBuffers.Count) return false;

        return checkoutDistributionBuffers[cartIndex].Count < cart.Settings.TechnicalMaxCargoPerCart;
    }

    private bool DoesCurrentLayoutMatchCheckoutDistribution()
    {
        if (checkoutDistributionBuffers.Count < orderedCargoCarts.Count) return false;

        for (int cartIndex = 0; cartIndex < orderedCargoCarts.Count; cartIndex++)
        {
            ChainCartCargo cargo = orderedCargoCarts[cartIndex];
            if (cargo == null) continue;

            IReadOnlyList<CargoEntry> current = cargo.CargoEntries;
            List<CargoEntry> target = checkoutDistributionBuffers[cartIndex];

            if (current.Count != target.Count) return false;

            for (int entryIndex = 0; entryIndex < current.Count; entryIndex++)
            {
                if (!ReferenceEquals(current[entryIndex], target[entryIndex])) return false;
            }
        }

        return true;
    }

    #endregion

    #region Loot Allocation

    /// <summary>
    /// Adds one collected GroceryLootDefinition to the player's owned carts.
    ///
    /// Allocation rule:
    /// 1) Scan front -> tail for gameplay-safe capacity.
    /// 2) Only when EVERY cart is safe-full, distribute overflow in rounds.
    /// 3) A cart receives OverflowRoundSize overflow ENTRIES before allocation
    ///    moves to the next cart.
    /// 4) After every cart completes that overflow round, begin the next round
    ///    again from the front.
    ///
    /// Existing SAFE cargo is never generally rebalanced. Existing LOCAL
    /// overload may be transferred only when owned capacity increases.
    /// </summary>
    public bool TryAddLoot(GroceryLootDefinition lootDefinition)
    {
        return TryAddLoot(lootDefinition, out _);
    }

    public bool TryAddLoot(GroceryLootDefinition lootDefinition, out ChainCartCargo assignedCart)
    {
        assignedCart = null;

        if (lootDefinition == null) return false;
        if (orderedCargoCarts.Count == 0) return false;

        ResetAllocationDebug();

        // PASS 1: Always consume safe capacity first, front -> tail.
        for (int i = 0; i < orderedCargoCarts.Count; i++)
        {
            ChainCartCargo cargo = orderedCargoCarts[i];
            if (!CanTechnicallyAcceptCargo(cargo)) continue;
            if (!cargo.HasSafeCapacityFor(lootDefinition)) continue;

            if (!cargo.TryAddLoot(lootDefinition)) continue;

            assignedCart = cargo;
            RecordAllocationDebug(cargo, i, false, -1);
            return true;
        }

        // PASS 2: Every usable cart is safe-full. Distribute visual overflow in
        // front-to-tail rounds. This rule is currently entry-based because the
        // prototype milestone uses 1 Loot = 1 CargoEntry = 1 Load.
        int overflowRoundSize = settings != null ? Mathf.Max(1, settings.OverflowRoundSize) : 1;
        int lowestOverflowRound = int.MaxValue;

        for (int i = 0; i < orderedCargoCarts.Count; i++)
        {
            ChainCartCargo cargo = orderedCargoCarts[i];
            if (!CanTechnicallyAcceptCargo(cargo)) continue;

            int cartRound = cargo.VisualOverflowEntryCount / overflowRoundSize;
            if (cartRound < lowestOverflowRound) lowestOverflowRound = cartRound;
        }

        if (lowestOverflowRound == int.MaxValue) return false;

        for (int i = 0; i < orderedCargoCarts.Count; i++)
        {
            ChainCartCargo cargo = orderedCargoCarts[i];
            if (!CanTechnicallyAcceptCargo(cargo)) continue;

            int cartRound = cargo.VisualOverflowEntryCount / overflowRoundSize;
            if (cartRound != lowestOverflowRound) continue;

            if (!cargo.TryAddLoot(lootDefinition)) continue;

            assignedCart = cargo;
            RecordAllocationDebug(cargo, i, true, lowestOverflowRound);
            return true;
        }

        return false;
    }

    public bool CanAcceptAnyLoot()
    {
        for (int i = 0; i < orderedCargoCarts.Count; i++)
        {
            if (CanTechnicallyAcceptCargo(orderedCargoCarts[i])) return true;
        }

        return false;
    }

    private bool CanTechnicallyAcceptCargo(ChainCartCargo cargo)
    {
        if (cargo == null || cargo.Settings == null) return false;
        return cargo.CargoEntryCount < cargo.Settings.TechnicalMaxCargoPerCart;
    }

    private void RecordAllocationDebug(ChainCartCargo cargo, int ownedIndex, bool overflow, int overflowRound)
    {
        lastAssignedCartName = cargo != null ? cargo.gameObject.name : string.Empty;
        lastAssignedOwnedIndex = ownedIndex;
        lastAssignmentWasOverflow = overflow;
        lastSelectedOverflowRound = overflowRound;
    }

    private void ResetAllocationDebug()
    {
        lastAssignedCartName = string.Empty;
        lastAssignedOwnedIndex = -1;
        lastAssignmentWasOverflow = false;
        lastSelectedOverflowRound = -1;
    }

    #endregion

    #region Prototype Context Menu Tests

    [ContextMenu("TEST - Add First Loot")]
    private void DebugAddFirstLoot()
    {
        if (!RequirePlayModeForTest()) return;

        GroceryLootDefinition loot = GetFirstValidDebugLoot();

        if (loot == null)
        {
            Debug.LogWarning("[CargoCapacityController] Assign at least one Debug Test Loot Definition first.", this);
            return;
        }

        if (!TryAddLoot(loot))
        {
            Debug.LogWarning("[CargoCapacityController] TEST Add Loot failed. Check owned carts / ChainCartCargo setup.", this);
        }
    }

    [ContextMenu("TEST - Add Random Loot")]
    private void DebugAddRandomLoot()
    {
        if (!RequirePlayModeForTest()) return;

        GroceryLootDefinition loot = GetRandomValidDebugLoot();

        if (loot == null)
        {
            Debug.LogWarning("[CargoCapacityController] Assign at least one Debug Test Loot Definition first.", this);
            return;
        }

        if (!TryAddLoot(loot))
        {
            Debug.LogWarning("[CargoCapacityController] TEST Add Loot failed. Check owned carts / ChainCartCargo setup.", this);
        }
    }

    [ContextMenu("TEST - Add 10 Random Loot")]
    private void DebugAddTenRandomLoot()
    {
        if (!RequirePlayModeForTest()) return;

        for (int i = 0; i < 10; i++)
        {
            GroceryLootDefinition loot = GetRandomValidDebugLoot();

            if (loot == null || !TryAddLoot(loot))
            {
                Debug.LogWarning($"[CargoCapacityController] TEST stopped after {i} successful additions.", this);
                return;
            }
        }
    }

    [ContextMenu("TEST - Recalculate / Refresh Owned Carts")]
    private void DebugRefreshOwnedCarts()
    {
        if (!RequirePlayModeForTest()) return;
        RefreshOwnedCargoCarts();
    }

    [ContextMenu("TEST - Normalize Local Overload")]
    private void DebugNormalizeLocalOverload()
    {
        if (!RequirePlayModeForTest()) return;

        lastNormalizationWasCapacityIncrease = false;
        lastNormalizationTransferCount = NormalizeLocalOverloadIntoSafeCapacity();
        RecalculateGlobalState();

        Debug.Log(
            $"[CargoCapacityController] TEST normalization moved {lastNormalizationTransferCount} cargo entries.",
            this
        );
    }

    [ContextMenu("TEST - Needs Checkout Compaction")]
    private void DebugNeedsCheckoutCompaction()
    {
        if (!RequirePlayModeForTest()) return;

        bool needsCompaction = NeedsCheckoutCompaction();

        Debug.Log(
            $"[CargoCapacityController] Checkout compaction needed:{needsCompaction} | Entries:{TotalCargoEntryCount} | LoadedCarts:{LoadedCargoCartCount}",
            this
        );
    }

    [ContextMenu("TEST - Compact Cargo For Checkout")]
    private void DebugCompactCargoForCheckout()
    {
        if (!RequirePlayModeForTest()) return;

        if (!CompactCargoForCheckout(out int movedEntries))
        {
            Debug.LogError("[CargoCapacityController] Checkout compaction TEST failed.", this);
            return;
        }

        Debug.Log(
            $"[CargoCapacityController] Checkout compaction complete | MovedEntries:{movedEntries} | Entries:{TotalCargoEntryCount} | LoadedCarts:{LoadedCargoCartCount}",
            this
        );
    }

    [ContextMenu("TEST - Log Cargo Distribution")]
    private void DebugLogDistribution()
    {
        if (!RequirePlayModeForTest()) return;

        string message =
            $"[CargoCapacityController] Distribution | Owned:{ownedFollowerCount} | Registered:{registeredCargoCartCount} | " +
            $"Entries:{totalCargoEntryCount} | LoadedCarts:{loadedCargoCartCount} | " +
            $"Load:{totalLoad}/{totalCapacity} | Overload:{overloadAmount}\n";

        for (int i = 0; i < orderedCargoCarts.Count; i++)
        {
            ChainCartCargo cargo = orderedCargoCarts[i];

            if (cargo == null)
            {
                message += $"[{i}] NULL\n";
                continue;
            }

            message +=
                $"[{i}] {cargo.gameObject.name} | Entries:{cargo.CargoEntryCount} | " +
                $"Load:{cargo.LocalLoad}/{cargo.SafeCapacity} | LocalOverload:{cargo.LocalOverload} | " +
                $"VisualOverflow:{cargo.VisualOverflowEntryCount}\n";
        }

        Debug.Log(message, this);
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

        Debug.LogWarning("[CargoCapacityController] Prototype Context Menu tests are Play Mode only.", this);
        return false;
    }

    #endregion
}
