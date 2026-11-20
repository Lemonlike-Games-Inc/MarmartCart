using System;
using UnityEngine;

/// <summary>
/// Scene-local registry of each player's world HUD anchor: the point on the
/// leading cart that the world HUD, pointers, markers and aim preview follow.
///
/// - Carts register themselves through PlayerHUDAnchor (player number from
///   their SnakeCartManager). Nothing here searches tags or names.
/// - A player's camera comes from PlayerViewService, not from this system.
/// - CartPitZone suppresses a player's normal world HUD during checkout.
/// </summary>
[DisallowMultipleComponent]
public class PlayerWorldHUDSystem : MonoBehaviour
{
    public const int MaxPlayerSlots = 4;

    [Serializable]
    private sealed class PlayerSlot
    {
        [SerializeField] private GameObject boundPlayerRoot;
        [SerializeField] private Transform hudWorldAnchor;

        [NonSerialized] public bool checkoutSuppressed;

        public GameObject BoundPlayerRoot { get => boundPlayerRoot; set => boundPlayerRoot = value; }
        public Transform HUDWorldAnchor { get => hudWorldAnchor; set => hudWorldAnchor = value; }
        public bool IsBound => boundPlayerRoot != null && hudWorldAnchor != null;
    }

    [Header("Runtime - Read Only")]
    [Tooltip("P1-P4. Filled at runtime when each leading cart registers its PlayerHUDAnchor.")]
    [SerializeField] private PlayerSlot[] slots = new PlayerSlot[MaxPlayerSlots];

    [Header("Diagnostics")]
    [SerializeField] private bool logSuccessfulBindings = true;

    public static PlayerWorldHUDSystem Instance { get; private set; }

    public event Action<int, GameObject, Transform> OnPlayerHUDBound;
    public event Action<int> OnPlayerHUDUnbound;

    #region Unity

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[UI] A second PlayerWorldHUDSystem was found in the scene. The newest one is used.", this);
        }

        Instance = this;

        EnsureFourSlots();

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            slots[i].BoundPlayerRoot = null;
            slots[i].HUDWorldAnchor = null;
            slots[i].checkoutSuppressed = false;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnValidate()
    {
        EnsureFourSlots();
    }

    #endregion

    #region Registration

    /// <summary>Called by PlayerHUDAnchor when a player's leading cart is enabled.</summary>
    public bool RegisterPlayer(int playerIndex, GameObject playerRoot, Transform hudAnchor)
    {
        if (!TryGetSlotArrayIndex(playerIndex, out int slotIndex)) return false;

        if (playerRoot == null || hudAnchor == null)
        {
            Debug.LogWarning($"[UI] World HUD P{playerIndex} registration needs a cart root and an anchor.", this);
            return false;
        }

        PlayerSlot slot = slots[slotIndex];

        if (slot.IsBound && slot.HUDWorldAnchor != hudAnchor)
        {
            Debug.LogWarning(
                $"[UI] World HUD P{playerIndex} was bound to '{slot.BoundPlayerRoot.name}' and is now replaced by " +
                $"'{playerRoot.name}'. Two carts claim the same player number.",
                playerRoot
            );
        }

        slot.BoundPlayerRoot = playerRoot;
        slot.HUDWorldAnchor = hudAnchor;

        if (logSuccessfulBindings)
        {
            Debug.Log($"[UI] World HUD P{playerIndex} bound to '{playerRoot.name}'.", hudAnchor);
        }

        OnPlayerHUDBound?.Invoke(playerIndex, playerRoot, hudAnchor);
        return true;
    }

    /// <summary>Called by PlayerHUDAnchor when its cart is disabled or destroyed. Ignored if a newer anchor owns the slot.</summary>
    public void UnregisterPlayer(int playerIndex, Transform hudAnchor)
    {
        if (!TryGetSlotArrayIndex(playerIndex, out int slotIndex)) return;

        PlayerSlot slot = slots[slotIndex];
        if (slot.HUDWorldAnchor != hudAnchor) return;

        slot.BoundPlayerRoot = null;
        slot.HUDWorldAnchor = null;

        OnPlayerHUDUnbound?.Invoke(playerIndex);
    }

    #endregion

    #region Checkout Suppression

    /// <summary>
    /// Temporarily hides the player's normal world HUD (PlayerWorldHUDRenderer then
    /// draws only the checkout prompt) without touching the binding.
    /// </summary>
    public void SetCheckoutSuppressed(int playerIndex, bool suppressed)
    {
        if (!TryGetSlotArrayIndex(playerIndex, out int slotIndex)) return;
        slots[slotIndex].checkoutSuppressed = suppressed;
    }

    public bool IsCheckoutSuppressed(int playerIndex)
    {
        return TryGetSlotArrayIndex(playerIndex, out int slotIndex) &&
               slots[slotIndex].checkoutSuppressed;
    }

    #endregion

    #region Read API

    public bool IsPlayerHUDBound(int playerIndex)
    {
        return TryGetSlotArrayIndex(playerIndex, out int slotIndex) && slots[slotIndex].IsBound;
    }

    /// <summary>The registered leading cart's root GameObject, or null.</summary>
    public GameObject GetPlayerRoot(int playerIndex)
    {
        if (!TryGetSlotArrayIndex(playerIndex, out int slotIndex)) return null;
        return slots[slotIndex].BoundPlayerRoot;
    }

    public Transform GetHUDWorldAnchor(int playerIndex)
    {
        if (!TryGetSlotArrayIndex(playerIndex, out int slotIndex)) return null;
        return slots[slotIndex].HUDWorldAnchor;
    }

    /// <summary>
    /// The player's HUD anchor when their world HUD should draw: bound and not
    /// checkout-suppressed. Drawers find the player for a camera through
    /// PlayerViewService, then ask for the anchor here.
    /// </summary>
    public bool TryGetRenderableAnchor(int playerIndex, out Transform hudAnchor)
    {
        return TryGetAnchor(playerIndex, false, false, out hudAnchor);
    }

    /// <summary>The player's HUD anchor for overlay-style UI that stays visible during checkout.</summary>
    public bool TryGetOverlayAnchor(int playerIndex, out Transform hudAnchor)
    {
        return TryGetAnchor(playerIndex, true, false, out hudAnchor);
    }

    /// <summary>
    /// The player's HUD anchor while checkout suppresses their normal world HUD.
    /// PlayerWorldHUDRenderer then draws only the checkout prompt.
    /// </summary>
    public bool TryGetCheckoutAnchor(int playerIndex, out Transform hudAnchor)
    {
        return TryGetAnchor(playerIndex, true, true, out hudAnchor);
    }

    #endregion

    #region Internal

    private bool TryGetAnchor(int playerIndex, bool allowSuppressed, bool requireSuppressed, out Transform hudAnchor)
    {
        hudAnchor = null;

        if (!TryGetSlotArrayIndex(playerIndex, out int slotIndex)) return false;

        PlayerSlot slot = slots[slotIndex];
        if (!slot.IsBound) return false;
        if (slot.checkoutSuppressed && !allowSuppressed) return false;
        if (!slot.checkoutSuppressed && requireSuppressed) return false;

        hudAnchor = slot.HUDWorldAnchor;
        return true;
    }

    private bool TryGetSlotArrayIndex(int playerIndex, out int slotIndex)
    {
        slotIndex = playerIndex - 1;

        if (slotIndex >= 0 && slotIndex < MaxPlayerSlots) return true;

        Debug.LogError(
            $"[PlayerWorldHUDSystem] Player index {playerIndex} is invalid. Expected 1..{MaxPlayerSlots}.",
            this
        );

        slotIndex = -1;
        return false;
    }

    private void EnsureFourSlots()
    {
        if (slots == null || slots.Length != MaxPlayerSlots)
        {
            PlayerSlot[] oldSlots = slots;
            slots = new PlayerSlot[MaxPlayerSlots];

            if (oldSlots != null)
            {
                Array.Copy(oldSlots, slots, Mathf.Min(oldSlots.Length, MaxPlayerSlots));
            }
        }

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            if (slots[i] == null) slots[i] = new PlayerSlot();
        }
    }

    #endregion
}
