using UnityEngine;

/// <summary>
/// Publishes one cart's backward-movement availability to its semantic Player
/// World HUD state. The renderer remains unaware of CartControlScript.
///
/// Attach once under the leading-cart hierarchy. The player number comes from
/// the parent SnakeCartManager; a cart outside a player's chain publishes nothing.
/// </summary>
[DisallowMultipleComponent]
public class CartWorldHUDMoveBackwardAdapter : MonoBehaviour
{
    #region References

    [Header("Cart")]
    [SerializeField] private CartControlScript cartController;

    [Header("HUD State")]
    [SerializeField] private PlayerWorldHUDStateSystem stateSystem;

    [Header("Runtime - Read Only")]
    [SerializeField] private int resolvedPlayerIndex;

    #endregion

    #region Unity

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeCartState();
        SubscribeStateReset();
        PushCurrentState();
    }

    private void OnDisable()
    {
        UnsubscribeCartState();
        UnsubscribeStateReset();
        ClearResolvedPlayerState();
    }

    #endregion

    #region State Reset

    private void SubscribeStateReset()
    {
        if (stateSystem == null)
        {
            return;
        }

        stateSystem.OnStateChanged -=
            HandleHUDStateChanged;

        stateSystem.OnStateChanged +=
            HandleHUDStateChanged;
    }

    private void UnsubscribeStateReset()
    {
        if (stateSystem == null)
        {
            return;
        }

        stateSystem.OnStateChanged -=
            HandleHUDStateChanged;
    }

    private void HandleHUDStateChanged(
        int playerIndex,
        PlayerWorldHUDStateChange change)
    {
        if (playerIndex != resolvedPlayerIndex ||
            change != PlayerWorldHUDStateChange.All)
        {
            return;
        }

        // A general HUD-state reset should not permanently hide a currently
        // valid gameplay prompt. Re-publish the authoritative cart state.
        PushCurrentState();
    }

    #endregion

    #region Cart State

    private void SubscribeCartState()
    {
        if (cartController == null)
        {
            return;
        }

        cartController.OnCanMoveBackwardChanged -=
            HandleCanMoveBackwardChanged;

        cartController.OnCanMoveBackwardChanged +=
            HandleCanMoveBackwardChanged;
    }

    private void UnsubscribeCartState()
    {
        if (cartController == null)
        {
            return;
        }

        cartController.OnCanMoveBackwardChanged -=
            HandleCanMoveBackwardChanged;
    }

    private void HandleCanMoveBackwardChanged(
        bool canMoveBackward)
    {
        PublishState(canMoveBackward);
    }

    private void PushCurrentState()
    {
        if (cartController == null)
        {
            return;
        }

        PublishState(
            cartController.CanMoveBackward
        );
    }

    private void PublishState(bool canMoveBackward)
    {
        if (stateSystem == null ||
            resolvedPlayerIndex <= 0)
        {
            return;
        }

        stateSystem.SetCanMoveBackward(
            resolvedPlayerIndex,
            canMoveBackward
        );
    }

    private void ClearResolvedPlayerState()
    {
        if (stateSystem == null ||
            resolvedPlayerIndex <= 0)
        {
            return;
        }

        stateSystem.SetCanMoveBackward(
            resolvedPlayerIndex,
            false
        );
    }

    #endregion

    #region References

    private void ResolveReferences()
    {
        if (cartController == null)
        {
            cartController =
                GetComponentInParent<CartControlScript>();
        }

        if (stateSystem == null)
        {
            stateSystem =
                FindFirstObjectByType<PlayerWorldHUDStateSystem>();
        }

        SnakeCartManager owner = GetComponentInParent<SnakeCartManager>();
        resolvedPlayerIndex = owner != null ? owner.GetPlayerId() : 0;
    }

    #endregion
}
