using UnityEngine;

/// <summary>
/// Pushes REAL cargo/load + overload movement-speed consequence into
/// PlayerWorldHUDStateSystem.
///
/// Attach once under the player's chain hierarchy. The player number comes from
/// the parent SnakeCartManager.
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

    [Header("HUD State")]
    [SerializeField] private PlayerWorldHUDStateSystem stateSystem;

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

        if (stateSystem == null)
        {
            stateSystem = FindFirstObjectByType<PlayerWorldHUDStateSystem>();
        }

        SnakeCartManager owner = GetComponentInParent<SnakeCartManager>();
        resolvedPlayerIndex = owner != null ? owner.GetPlayerId() : 0;
    }

    private void OnEnable()
    {
        SubscribeCargo();
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
        UnsubscribeCargo();
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
        if (cargoCapacityController == null || stateSystem == null || resolvedPlayerIndex <= 0) return;

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
