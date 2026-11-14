using UnityEngine;

/// <summary>
/// Pushes REAL cargo/load + overload movement-speed consequence into
/// PlayerWorldHUDStateSystem.
///
/// Attach once under the leading-cart/player hierarchy.
///
/// This adapter owns ONLY:
/// - Load / safe capacity / overload
/// - Max-speed multiplier caused by overload
///
/// Hype + drift previews remain owned by CartWorldHUDHypeAdapter.
/// </summary>
[DisallowMultipleComponent]
public class CartWorldHUDCargoAdapter : MonoBehaviour
{
    #region References

    [Header("Cargo")]
    [SerializeField] private CargoCapacityController cargoCapacityController;

    [Header("HUD Systems")]
    [SerializeField] private PlayerWorldHUDSystem hudSystem;
    [SerializeField] private PlayerWorldHUDStateSystem stateSystem;

    #endregion

    #region Player Resolution

    [Header("Player Slot")]
    [Tooltip(
        "0 = automatically resolve from PlayerWorldHUDSystem binding. " +
        "1..4 = force a specific player slot."
    )]
    [Range(0, PlayerWorldHUDStateSystem.MaxPlayerSlots)]
    [SerializeField] private int playerIndexOverride = 0;

    [Header("Runtime - Read Only")]
    [SerializeField] private int resolvedPlayerIndex;

    #endregion

    #region Unity

    private void Awake()
    {
        if (cargoCapacityController == null)
        {
            cargoCapacityController = GetComponentInParent<CargoCapacityController>();
        }

        if (hudSystem == null)
        {
            hudSystem = FindFirstObjectByType<PlayerWorldHUDSystem>();
        }

        if (stateSystem == null)
        {
            stateSystem = FindFirstObjectByType<PlayerWorldHUDStateSystem>();
        }
    }

    private void OnEnable()
    {
        SubscribeHUDBinding();
        SubscribeCargo();
        ResolvePlayerIndex();
    }

    private void Start()
    {
        // CargoCapacityController refreshes its real totals in Start().
        // Pushing here ensures we capture its initialized state even when no
        // value-change event happened after this adapter subscribed.
        PushAllCargoState();
    }

    private void OnDisable()
    {
        UnsubscribeHUDBinding();
        UnsubscribeCargo();
    }

    private void OnValidate()
    {
        playerIndexOverride = Mathf.Clamp(
            playerIndexOverride,
            0,
            PlayerWorldHUDStateSystem.MaxPlayerSlots
        );
    }

    #endregion

    #region HUD Binding

    private void SubscribeHUDBinding()
    {
        if (hudSystem == null) return;

        hudSystem.OnPlayerHUDBound -= HandlePlayerHUDBound;
        hudSystem.OnPlayerHUDUnbound -= HandlePlayerHUDUnbound;

        hudSystem.OnPlayerHUDBound += HandlePlayerHUDBound;
        hudSystem.OnPlayerHUDUnbound += HandlePlayerHUDUnbound;
    }

    private void UnsubscribeHUDBinding()
    {
        if (hudSystem == null) return;

        hudSystem.OnPlayerHUDBound -= HandlePlayerHUDBound;
        hudSystem.OnPlayerHUDUnbound -= HandlePlayerHUDUnbound;
    }

    private void HandlePlayerHUDBound(
        int playerIndex,
        GameObject playerRoot,
        Transform hudAnchor)
    {
        if (playerIndexOverride > 0)
        {
            resolvedPlayerIndex = playerIndexOverride;
            PushAllCargoState();
            return;
        }

        if (!IsInsidePlayerRoot(playerRoot)) return;

        resolvedPlayerIndex = playerIndex;
        PushAllCargoState();
    }

    private void HandlePlayerHUDUnbound(int playerIndex)
    {
        if (playerIndexOverride > 0) return;
        if (resolvedPlayerIndex != playerIndex) return;

        resolvedPlayerIndex = 0;
    }

    private void ResolvePlayerIndex()
    {
        if (playerIndexOverride > 0)
        {
            resolvedPlayerIndex = playerIndexOverride;
            return;
        }

        resolvedPlayerIndex = 0;

        if (hudSystem == null) return;

        for (int playerIndex = 1;
             playerIndex <= PlayerWorldHUDSystem.MaxPlayerSlots;
             playerIndex++)
        {
            GameObject playerRoot = hudSystem.GetPlayerRoot(playerIndex);

            if (!IsInsidePlayerRoot(playerRoot)) continue;

            resolvedPlayerIndex = playerIndex;
            return;
        }
    }

    private bool IsInsidePlayerRoot(GameObject playerRoot)
    {
        if (playerRoot == null) return false;

        Transform rootTransform = playerRoot.transform;

        return transform == rootTransform || transform.IsChildOf(rootTransform);
    }

    #endregion

    #region Cargo Events

    private void SubscribeCargo()
    {
        if (cargoCapacityController == null) return;

        cargoCapacityController.OnCapacityChanged -= HandleCapacityChanged;
        cargoCapacityController.OnLoadChanged -= HandleLoadChanged;
        cargoCapacityController.OnOverloadChanged -= HandleOverloadChanged;
        cargoCapacityController.OnMovementSpeedMultiplierChanged -= HandleMovementSpeedMultiplierChanged;

        cargoCapacityController.OnCapacityChanged += HandleCapacityChanged;
        cargoCapacityController.OnLoadChanged += HandleLoadChanged;
        cargoCapacityController.OnOverloadChanged += HandleOverloadChanged;
        cargoCapacityController.OnMovementSpeedMultiplierChanged += HandleMovementSpeedMultiplierChanged;
    }

    private void UnsubscribeCargo()
    {
        if (cargoCapacityController == null) return;

        cargoCapacityController.OnCapacityChanged -= HandleCapacityChanged;
        cargoCapacityController.OnLoadChanged -= HandleLoadChanged;
        cargoCapacityController.OnOverloadChanged -= HandleOverloadChanged;
        cargoCapacityController.OnMovementSpeedMultiplierChanged -= HandleMovementSpeedMultiplierChanged;
    }

    private void HandleCapacityChanged(int value)
    {
        PushAllCargoState();
    }

    private void HandleLoadChanged(int value)
    {
        PushAllCargoState();
    }

    private void HandleOverloadChanged(int value)
    {
        PushAllCargoState();
    }

    private void HandleMovementSpeedMultiplierChanged(float value)
    {
        PushAllCargoState();
    }

    #endregion

    #region State Push

    private void PushAllCargoState()
    {
        if (cargoCapacityController == null || stateSystem == null) return;

        if (resolvedPlayerIndex <= 0)
        {
            ResolvePlayerIndex();
            if (resolvedPlayerIndex <= 0) return;
        }

        stateSystem.SetLoad(
            resolvedPlayerIndex,
            cargoCapacityController.TotalLoad,
            cargoCapacityController.TotalCapacity,
            cargoCapacityController.OverloadAmount
        );

        stateSystem.SetMaxSpeedMultiplier(
            resolvedPlayerIndex,
            cargoCapacityController.MovementSpeedMultiplier
        );
    }

    #endregion
}
