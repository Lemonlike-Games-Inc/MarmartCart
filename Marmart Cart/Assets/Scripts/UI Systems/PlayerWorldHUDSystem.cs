using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerWorldHUDSystem : MonoBehaviour
{
    public const int MaxPlayerSlots = 4;

    public enum SlotBindingStatus
    {
        Disabled,
        WaitingForPlayer,
        WaitingForAnchor,
        Bound,
        TagUnavailable,
        AmbiguousTaggedPlayers
    }

    [Serializable]
    private sealed class PlayerSlot
    {
        [Header("Slot Setup")]
        [Tooltip("When disabled, this slot will not perform fallback binding and later renderers should ignore it.")]
        public bool enabled = true;

        [Tooltip("The REAL Unity Camera rendering this player's viewport. Do not assign a Cinemachine virtual camera.")]
        public Camera gameplayCamera;

        [Header("Runtime - Read Only")]
        [SerializeField] private SlotBindingStatus bindingStatus = SlotBindingStatus.WaitingForPlayer;
        [SerializeField] private GameObject boundPlayerRoot;
        [SerializeField] private Transform hudWorldAnchor;
        [SerializeField] private bool wasDirectlyRegistered;

        [NonSerialized] public float nextRetryTime;
        [NonSerialized] public bool invalidTagLogged;
        [NonSerialized] public bool ambiguousTagLogged;
        [NonSerialized] public bool missingAnchorLoggedForCurrentRoot;
        [NonSerialized] public bool checkoutSuppressed;

        public SlotBindingStatus BindingStatus { get => bindingStatus; set => bindingStatus = value; }
        public GameObject BoundPlayerRoot { get => boundPlayerRoot; set => boundPlayerRoot = value; }
        public Transform HUDWorldAnchor { get => hudWorldAnchor; set => hudWorldAnchor = value; }
        public bool WasDirectlyRegistered { get => wasDirectlyRegistered; set => wasDirectlyRegistered = value; }
        public bool IsBound => boundPlayerRoot != null && hudWorldAnchor != null;
    }

    [Header("Player Slots")]
    [Tooltip("Exactly four fixed local-player slots. Assign the PHYSICAL gameplay Camera for each slot.")]
    [SerializeField] private PlayerSlot[] slots = new PlayerSlot[MaxPlayerSlots];

    [Header("Fallback Runtime Binding")]
    [Tooltip("Compatibility fallback only. Unbound slots look for Player1..Player4 tags.")]
    [SerializeField] private bool enableTagBindingFallback = true;

    [Tooltip("Exact descendant Transform name expected under each runtime player/leading-cart hierarchy.")]
    [SerializeField] private string hudAnchorName = "HUDWorldAnchor";

    [Tooltip("Seconds between fallback binding attempts. Uses unscaled time.")]
    [Min(0.05f)]
    [SerializeField] private float bindingRetryInterval = 0.5f;

    [Header("Diagnostics")]
    [SerializeField] private bool logSuccessfulBindings = true;

    private readonly Dictionary<Camera, int> cameraToPlayerIndex = new Dictionary<Camera, int>(MaxPlayerSlots);

    public event Action<int, GameObject, Transform> OnPlayerHUDBound;
    public event Action<int> OnPlayerHUDUnbound;

    private void Reset()
    {
        EnsureFourSlots();
        slots[0].enabled = true;
        slots[1].enabled = true;
        slots[2].enabled = false;
        slots[3].enabled = false;
    }

    private void Awake()
    {
        EnsureFourSlots();
        RebuildCameraMap();

        float now = Time.unscaledTime;

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            PlayerSlot slot = slots[i];
            slot.checkoutSuppressed = false;
            slot.nextRetryTime = now;
            slot.BindingStatus = !slot.enabled
                ? SlotBindingStatus.Disabled
                : slot.IsBound ? SlotBindingStatus.Bound : SlotBindingStatus.WaitingForPlayer;
        }
    }

    private void Start()
    {
        ValidateCameraAssignments();
    }

    private void Update()
    {
        float now = Time.unscaledTime;

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            PlayerSlot slot = slots[i];

            if (!slot.enabled)
            {
                slot.BindingStatus = SlotBindingStatus.Disabled;
                continue;
            }

            if (slot.BoundPlayerRoot == null && slot.HUDWorldAnchor != null)
            {
                ClearRuntimeBinding(i, true);
            }
            else if (slot.BoundPlayerRoot != null && slot.HUDWorldAnchor == null && slot.BindingStatus == SlotBindingStatus.Bound)
            {
                slot.BindingStatus = SlotBindingStatus.WaitingForAnchor;
                slot.nextRetryTime = now;
                OnPlayerHUDUnbound?.Invoke(i + 1);
            }

            if (slot.IsBound)
            {
                slot.BindingStatus = SlotBindingStatus.Bound;
                continue;
            }

            if (now < slot.nextRetryTime) continue;

            slot.nextRetryTime = now + bindingRetryInterval;
            TryBindSlot(i);
        }
    }

    private void OnValidate()
    {
        bindingRetryInterval = Mathf.Max(0.05f, bindingRetryInterval);
        if (string.IsNullOrWhiteSpace(hudAnchorName)) hudAnchorName = "HUDWorldAnchor";

        EnsureFourSlots();

        if (Application.isPlaying) RebuildCameraMap();
    }

    public bool RegisterPlayer(int playerIndex, GameObject playerRoot)
    {
        return RegisterPlayer(playerIndex, playerRoot, null);
    }

    public bool RegisterPlayer(int playerIndex, GameObject playerRoot, Transform hudAnchor)
    {
        if (!TryGetSlotArrayIndex(playerIndex, out int slotIndex)) return false;

        PlayerSlot slot = slots[slotIndex];

        if (playerRoot == null)
        {
            Debug.LogWarning($"[PlayerWorldHUDSystem] RegisterPlayer({playerIndex}) received a null player root.", this);
            return false;
        }

        bool wasPreviouslyBound = slot.IsBound;

        slot.BoundPlayerRoot = playerRoot;
        slot.HUDWorldAnchor = null;
        slot.WasDirectlyRegistered = true;
        slot.missingAnchorLoggedForCurrentRoot = false;
        slot.nextRetryTime = Time.unscaledTime;

        if (hudAnchor != null)
        {
            CompleteBinding(slotIndex, playerRoot, hudAnchor, "direct registration");
            return true;
        }

        Transform foundAnchor = FindDescendantByName(playerRoot.transform, hudAnchorName);

        if (foundAnchor != null)
        {
            CompleteBinding(slotIndex, playerRoot, foundAnchor, "direct player registration + anchor lookup");
            return true;
        }

        slot.BindingStatus = SlotBindingStatus.WaitingForAnchor;

        if (wasPreviouslyBound) OnPlayerHUDUnbound?.Invoke(playerIndex);
        return true;
    }

    public void UnregisterPlayer(int playerIndex)
    {
        if (!TryGetSlotArrayIndex(playerIndex, out int slotIndex)) return;
        ClearRuntimeBinding(slotIndex, true);
    }

    public void SetSlotEnabled(int playerIndex, bool enabled)
    {
        if (!TryGetSlotArrayIndex(playerIndex, out int slotIndex)) return;

        PlayerSlot slot = slots[slotIndex];
        slot.enabled = enabled;

        if (enabled)
        {
            slot.BindingStatus = slot.IsBound ? SlotBindingStatus.Bound : SlotBindingStatus.WaitingForPlayer;
            slot.nextRetryTime = Time.unscaledTime;
        }
        else
        {
            slot.BindingStatus = SlotBindingStatus.Disabled;
        }
    }

    public void SetGameplayCamera(int playerIndex, Camera gameplayCamera)
    {
        if (!TryGetSlotArrayIndex(playerIndex, out int slotIndex)) return;
        slots[slotIndex].gameplayCamera = gameplayCamera;
        RebuildCameraMap();
    }

    [ContextMenu("Retry All HUD Bindings Now")]
    public void RetryAllBindingsNow()
    {
        float now = Time.unscaledTime;

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            PlayerSlot slot = slots[i];
            slot.invalidTagLogged = false;
            slot.ambiguousTagLogged = false;
            slot.missingAnchorLoggedForCurrentRoot = false;
            slot.nextRetryTime = now;

            if (slot.enabled && !slot.IsBound)
            {
                slot.BindingStatus = slot.BoundPlayerRoot != null
                    ? SlotBindingStatus.WaitingForAnchor
                    : SlotBindingStatus.WaitingForPlayer;
            }
        }
    }

    public bool IsSlotEnabled(int playerIndex)
    {
        return TryGetSlotArrayIndex(playerIndex, out int slotIndex) && slots[slotIndex].enabled;
    }

    /// <summary>
    /// Temporarily hides every PlayerWorldHUDRenderer element for one player
    /// without disabling the slot or disturbing its runtime player/anchor binding.
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

    public bool IsPlayerHUDBound(int playerIndex)
    {
        return TryGetSlotArrayIndex(playerIndex, out int slotIndex) && slots[slotIndex].IsBound;
    }

    public SlotBindingStatus GetBindingStatus(int playerIndex)
    {
        if (!TryGetSlotArrayIndex(playerIndex, out int slotIndex)) return SlotBindingStatus.Disabled;
        return slots[slotIndex].BindingStatus;
    }

    public Camera GetGameplayCamera(int playerIndex)
    {
        if (!TryGetSlotArrayIndex(playerIndex, out int slotIndex)) return null;
        return slots[slotIndex].gameplayCamera;
    }

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

    public bool TryGetPlayerIndexForCamera(Camera gameplayCamera, out int playerIndex)
    {
        playerIndex = 0;
        if (gameplayCamera == null) return false;
        return cameraToPlayerIndex.TryGetValue(gameplayCamera, out playerIndex);
    }

    public bool TryGetRenderableSlotForCamera(Camera gameplayCamera, out int playerIndex, out Transform hudAnchor)
    {
        playerIndex = 0;
        hudAnchor = null;

        if (!TryGetPlayerIndexForCamera(gameplayCamera, out playerIndex)) return false;
        if (!TryGetSlotArrayIndex(playerIndex, out int slotIndex)) return false;

        PlayerSlot slot = slots[slotIndex];
        if (!slot.enabled || slot.checkoutSuppressed || !slot.IsBound) return false;

        hudAnchor = slot.HUDWorldAnchor;
        return hudAnchor != null;
    }
    public bool TryGetRenderableSlotForCameraForOverlay(Camera gameplayCamera, out int playerIndex, out Transform hudAnchor)
    {
        playerIndex = 0;
        hudAnchor = null;

        if (!TryGetPlayerIndexForCamera(gameplayCamera, out playerIndex)) return false;
        if (!TryGetSlotArrayIndex(playerIndex, out int slotIndex)) return false;

        PlayerSlot slot = slots[slotIndex];
        if (!slot.enabled || !slot.IsBound) return false;

        hudAnchor = slot.HUDWorldAnchor;
        return hudAnchor != null;
    }

    /// <summary>
    /// Resolves only a bound player slot whose normal world HUD is currently
    /// checkout-suppressed. This lets PlayerWorldHUDRenderer replace the normal
    /// meters with checkout-only presentation without affecting the dedicated
    /// viewport-overlay lookup above.
    /// </summary>
    public bool TryGetCheckoutSuppressedSlotForCamera(
        Camera gameplayCamera,
        out int playerIndex,
        out Transform hudAnchor)
    {
        playerIndex = 0;
        hudAnchor = null;

        if (!TryGetPlayerIndexForCamera(gameplayCamera, out playerIndex)) return false;
        if (!TryGetSlotArrayIndex(playerIndex, out int slotIndex)) return false;

        PlayerSlot slot = slots[slotIndex];
        if (!slot.enabled || !slot.checkoutSuppressed || !slot.IsBound) return false;

        hudAnchor = slot.HUDWorldAnchor;
        return hudAnchor != null;
    }

    private void TryBindSlot(int slotIndex)
    {
        PlayerSlot slot = slots[slotIndex];

        if (!slot.enabled || slot.IsBound) return;

        if (slot.BoundPlayerRoot != null)
        {
            TryBindAnchorOnKnownRoot(slotIndex);
            return;
        }

        if (!enableTagBindingFallback)
        {
            slot.BindingStatus = SlotBindingStatus.WaitingForPlayer;
            return;
        }

        TryBindFromPlayerTag(slotIndex);
    }

    private void TryBindFromPlayerTag(int slotIndex)
    {
        PlayerSlot slot = slots[slotIndex];
        int playerIndex = slotIndex + 1;
        string playerTag = GetFallbackPlayerTag(playerIndex);

        GameObject[] taggedObjects;

        try
        {
            taggedObjects = GameObject.FindGameObjectsWithTag(playerTag);
        }
        catch (UnityException)
        {
            slot.BindingStatus = SlotBindingStatus.TagUnavailable;

            if (!slot.invalidTagLogged)
            {
                slot.invalidTagLogged = true;

                Debug.LogError(
                    $"[PlayerWorldHUDSystem] Fallback tag '{playerTag}' does not exist. " +
                    $"Create the tag, disable fallback, or register P{playerIndex} directly.",
                    this
                );
            }

            return;
        }

        if (taggedObjects == null || taggedObjects.Length == 0)
        {
            // Missing players during startup are expected.
            slot.BindingStatus = SlotBindingStatus.WaitingForPlayer;
            return;
        }

        GameObject resolvedRoot = null;
        Transform resolvedAnchor = null;

        for (int i = 0; i < taggedObjects.Length; i++)
        {
            GameObject candidate = taggedObjects[i];
            if (candidate == null) continue;

            // Important difference from the old MapEventPointer:
            // we do NOT accept an arbitrary PlayerX-tagged object.
            //
            // The tagged object must actually own the HUD hierarchy we need.
            Transform candidateAnchor = FindDescendantByName(candidate.transform, hudAnchorName);
            if (candidateAnchor == null) continue;

            if (resolvedAnchor == null)
            {
                resolvedRoot = candidate;
                resolvedAnchor = candidateAnchor;
                continue;
            }

            // Multiple tagged GameObjects can sometimes resolve to the same
            // anchor (for example, if project hierarchy/tagging later changes).
            // That is still one unique HUD owner and is safe.
            if (candidateAnchor == resolvedAnchor) continue;

            slot.BindingStatus = SlotBindingStatus.AmbiguousTaggedPlayers;

            if (!slot.ambiguousTagLogged)
            {
                slot.ambiguousTagLogged = true;

                Debug.LogError(
                    $"[PlayerWorldHUDSystem] P{playerIndex} found multiple '{playerTag}' objects that each contain " +
                    $"a different '{hudAnchorName}'. The HUD system will not guess which player owns the slot. " +
                    $"Prefer direct RegisterPlayer(), or make only the intended Player{playerIndex} hierarchy contain the HUD anchor.",
                    this
                );
            }

            return;
        }

        if (resolvedRoot == null || resolvedAnchor == null)
        {
            // Tagged objects exist, but none of them is the HUD-owning player
            // hierarchy yet. This is normal during staged runtime spawning.
            slot.BindingStatus = SlotBindingStatus.WaitingForAnchor;
            return;
        }

        slot.ambiguousTagLogged = false;
        slot.BoundPlayerRoot = resolvedRoot;
        slot.HUDWorldAnchor = resolvedAnchor;
        slot.WasDirectlyRegistered = false;
        slot.missingAnchorLoggedForCurrentRoot = false;

        CompleteBinding(slotIndex, resolvedRoot, resolvedAnchor, "multi-object tag fallback");
    }

    private void TryBindAnchorOnKnownRoot(int slotIndex)
    {
        PlayerSlot slot = slots[slotIndex];

        if (slot.BoundPlayerRoot == null)
        {
            slot.BindingStatus = SlotBindingStatus.WaitingForPlayer;
            return;
        }

        Transform anchor = FindDescendantByName(slot.BoundPlayerRoot.transform, hudAnchorName);

        if (anchor == null)
        {
            slot.BindingStatus = SlotBindingStatus.WaitingForAnchor;

            if (!slot.missingAnchorLoggedForCurrentRoot)
            {
                slot.missingAnchorLoggedForCurrentRoot = true;
                Debug.LogWarning(
                    $"[PlayerWorldHUDSystem] P{slotIndex + 1} root '{slot.BoundPlayerRoot.name}' was found, but no descendant named '{hudAnchorName}' exists yet. Retrying every {bindingRetryInterval:0.##}s.",
                    slot.BoundPlayerRoot
                );
            }

            return;
        }

        CompleteBinding(
            slotIndex,
            slot.BoundPlayerRoot,
            anchor,
            slot.WasDirectlyRegistered ? "direct registration retry" : "tag fallback"
        );
    }

    private void CompleteBinding(int slotIndex, GameObject playerRoot, Transform anchor, string source)
    {
        PlayerSlot slot = slots[slotIndex];

        slot.BoundPlayerRoot = playerRoot;
        slot.HUDWorldAnchor = anchor;
        slot.BindingStatus = SlotBindingStatus.Bound;
        slot.missingAnchorLoggedForCurrentRoot = false;

        int playerIndex = slotIndex + 1;

        if (logSuccessfulBindings)
        {
            Debug.Log($"[PlayerWorldHUDSystem] Bound P{playerIndex} HUD -> '{anchor.name}' using {source}.", anchor);
        }

        OnPlayerHUDBound?.Invoke(playerIndex, playerRoot, anchor);
    }

    private void ClearRuntimeBinding(int slotIndex, bool notify)
    {
        PlayerSlot slot = slots[slotIndex];
        bool wasBound = slot.IsBound;

        slot.BoundPlayerRoot = null;
        slot.HUDWorldAnchor = null;
        slot.WasDirectlyRegistered = false;
        slot.missingAnchorLoggedForCurrentRoot = false;
        slot.nextRetryTime = Time.unscaledTime;
        slot.BindingStatus = slot.enabled ? SlotBindingStatus.WaitingForPlayer : SlotBindingStatus.Disabled;

        if (notify && wasBound) OnPlayerHUDUnbound?.Invoke(slotIndex + 1);
    }

    [ContextMenu("Rebuild HUD Camera Map")]
    public void RebuildCameraMap()
    {
        cameraToPlayerIndex.Clear();
        EnsureFourSlots();

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            Camera camera = slots[i].gameplayCamera;
            if (camera == null) continue;

            int playerIndex = i + 1;

            if (cameraToPlayerIndex.ContainsKey(camera))
            {
                Debug.LogError(
                    $"[PlayerWorldHUDSystem] Camera '{camera.name}' is assigned to more than one player slot.",
                    camera
                );
                continue;
            }

            cameraToPlayerIndex.Add(camera, playerIndex);
        }
    }

    private void ValidateCameraAssignments()
    {
        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            PlayerSlot slot = slots[i];

            if (!slot.enabled) continue;

            if (slot.gameplayCamera == null)
            {
                Debug.LogWarning(
                    $"[PlayerWorldHUDSystem] P{i + 1} is enabled but has no physical gameplay Camera assigned.",
                    this
                );
            }
        }
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

    private string GetFallbackPlayerTag(int playerIndex)
    {
        switch (playerIndex)
        {
            case 1: return "Player1";
            case 2: return "Player2";
            case 3: return "Player3";
            case 4: return "Player4";
            default: return string.Empty;
        }
    }

    private Transform FindDescendantByName(Transform root, string targetName)
    {
        if (root == null) return null;
        if (root.name == targetName) return root;

        int childCount = root.childCount;

        for (int i = 0; i < childCount; i++)
        {
            Transform found = FindDescendantByName(root.GetChild(i), targetName);
            if (found != null) return found;
        }

        return null;
    }

    private void EnsureFourSlots()
    {
        if (slots == null || slots.Length != MaxPlayerSlots)
        {
            PlayerSlot[] oldSlots = slots;
            slots = new PlayerSlot[MaxPlayerSlots];

            if (oldSlots != null)
            {
                int copyCount = Mathf.Min(oldSlots.Length, MaxPlayerSlots);

                for (int i = 0; i < copyCount; i++)
                {
                    slots[i] = oldSlots[i];
                }
            }
        }

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            if (slots[i] == null) slots[i] = new PlayerSlot();
        }
    }
}
