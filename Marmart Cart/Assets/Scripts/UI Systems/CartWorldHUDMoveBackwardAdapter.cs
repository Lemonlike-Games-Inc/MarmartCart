using UnityEngine;

/// <summary>
/// Publishes one cart's backward-movement availability to its semantic Player
/// World HUD state. The renderer remains unaware of CartControlScript.
///
/// Attach once under the leading-cart/player hierarchy.
/// </summary>
[DisallowMultipleComponent]
public class CartWorldHUDMoveBackwardAdapter : MonoBehaviour
{
    #region References

    [Header("Cart")]
    [SerializeField] private CartControlScript cartController;

    [Header("HUD Systems")]
    [SerializeField] private PlayerWorldHUDSystem hudSystem;
    [SerializeField] private PlayerWorldHUDStateSystem stateSystem;

    #endregion

    #region Player Resolution

    [Header("Player Slot")]
    [Tooltip(
        "0 = automatically resolve from PlayerWorldHUDSystem binding. " +
        "1..4 = force a specific player slot.")]
    [Range(0, PlayerWorldHUDStateSystem.MaxPlayerSlots)]
    [SerializeField] private int playerIndexOverride;

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
        SubscribeHUDBinding();
        SubscribeStateReset();
        ResolvePlayerIndex();
        PushCurrentState();
    }

    private void OnDisable()
    {
        UnsubscribeCartState();
        UnsubscribeHUDBinding();
        UnsubscribeStateReset();
        ClearResolvedPlayerState();
    }

    private void OnValidate()
    {
        playerIndexOverride =
            Mathf.Clamp(
                playerIndexOverride,
                0,
                PlayerWorldHUDStateSystem.MaxPlayerSlots
            );
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

    #endregion

    #region HUD Binding

    private void SubscribeHUDBinding()
    {
        if (hudSystem == null)
        {
            return;
        }

        hudSystem.OnPlayerHUDBound +=
            HandlePlayerHUDBound;

        hudSystem.OnPlayerHUDUnbound +=
            HandlePlayerHUDUnbound;
    }

    private void UnsubscribeHUDBinding()
    {
        if (hudSystem == null)
        {
            return;
        }

        hudSystem.OnPlayerHUDBound -=
            HandlePlayerHUDBound;

        hudSystem.OnPlayerHUDUnbound -=
            HandlePlayerHUDUnbound;
    }

    private void HandlePlayerHUDBound(
        int playerIndex,
        GameObject playerRoot,
        Transform hudAnchor)
    {
        if (playerIndexOverride > 0)
        {
            resolvedPlayerIndex = playerIndexOverride;
            PushCurrentState();
            return;
        }

        if (!IsInsidePlayerRoot(playerRoot))
        {
            return;
        }

        resolvedPlayerIndex = playerIndex;
        PushCurrentState();
    }

    private void HandlePlayerHUDUnbound(int playerIndex)
    {
        if (playerIndexOverride > 0 ||
            resolvedPlayerIndex != playerIndex)
        {
            return;
        }

        ClearResolvedPlayerState();
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

        if (hudSystem == null)
        {
            return;
        }

        for (
            int playerIndex = 1;
            playerIndex <= PlayerWorldHUDSystem.MaxPlayerSlots;
            playerIndex++)
        {
            GameObject playerRoot =
                hudSystem.GetPlayerRoot(
                    playerIndex
                );

            if (!IsInsidePlayerRoot(playerRoot))
            {
                continue;
            }

            resolvedPlayerIndex = playerIndex;
            return;
        }
    }

    private bool IsInsidePlayerRoot(GameObject playerRoot)
    {
        if (playerRoot == null)
        {
            return false;
        }

        Transform rootTransform = playerRoot.transform;

        return transform == rootTransform ||
               transform.IsChildOf(rootTransform);
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

        if (hudSystem == null)
        {
            hudSystem =
                FindFirstObjectByType<PlayerWorldHUDSystem>();
        }

        if (stateSystem == null)
        {
            stateSystem =
                FindFirstObjectByType<PlayerWorldHUDStateSystem>();
        }
    }

    #endregion
}
