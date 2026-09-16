using System;
using UnityEngine;

public enum PowerupCartTargetKind
{
    None,
    LooseCart,
    LeadingCart,
    ChainedCart
}

/// <summary>
/// Stable semantic data captured when a projectile evaluates a cart collider.
/// The projectile/effect layer does not need to rediscover cart ownership.
/// </summary>
[Serializable]
public struct PowerupCartTargetSnapshot
{
    public PowerupCartTarget Target;
    public PowerupCartTargetKind Kind;
    public int PlayerIndex;
    public bool IsInCheckout;
    public PlayerPowerupController OwnerController;
    public CartControlScript LeadingCartControl;
    public ChainedCartManager ChainedCartManager;
}

/// <summary>
/// Semantic marker shared by every object on the existing Cart layer.
///
/// - A cart with ChainedCartManager is Loose or Chained according to its
///   authoritative isCollectedByPlayer state.
/// - A cart without ChainedCartManager but with CartControlScript is Leading.
///
/// Add this once to the leading-cart prefab and once to the follower/loose-cart
/// prefab. The latter changes classification automatically at runtime.
/// </summary>
[DisallowMultipleComponent]
public class PowerupCartTarget : MonoBehaviour
{
    [Header("Auto-Resolved References")]
    [SerializeField] private CartControlScript leadingCartControl;
    [SerializeField] private ChainedCartManager chainedCartManager;
    [SerializeField] private PowerupRuntimeSystem runtimeSystem;

    [Header("Runtime - Read Only")]
    [SerializeField] private PowerupCartTargetKind currentKind;
    [SerializeField] private int playerIndex;
    [SerializeField] private bool inCheckout;
    [SerializeField] private PlayerPowerupController ownerController;

    private bool localReferencesResolved;

    public PowerupCartTargetKind CurrentKind
    {
        get
        {
            RefreshRuntimeState();
            return currentKind;
        }
    }

    public int PlayerIndex
    {
        get
        {
            RefreshRuntimeState();
            return playerIndex;
        }
    }

    public bool IsInCheckout
    {
        get
        {
            RefreshRuntimeState();
            return inCheckout;
        }
    }

    private void Reset()
    {
        localReferencesResolved = false;
        ResolveLocalReferences();
    }

    private void Awake()
    {
        ResolveLocalReferences();
        ResolveRuntimeSystem();
        RefreshRuntimeState();
    }

    private void OnEnable()
    {
        RefreshRuntimeState();
    }

    public bool TryGetGameplayTarget(
        out PowerupCartTargetSnapshot snapshot)
    {
        RefreshRuntimeState();

        snapshot = new PowerupCartTargetSnapshot
        {
            Target = this,
            Kind = currentKind,
            PlayerIndex = playerIndex,
            IsInCheckout = inCheckout,
            OwnerController = ownerController,
            LeadingCartControl = currentKind ==
                PowerupCartTargetKind.LeadingCart
                ? leadingCartControl
                : null,
            ChainedCartManager = currentKind ==
                PowerupCartTargetKind.ChainedCart
                ? chainedCartManager
                : null
        };

        return (currentKind == PowerupCartTargetKind.LeadingCart ||
                currentKind == PowerupCartTargetKind.ChainedCart) &&
               playerIndex >= 1 &&
               playerIndex <= PowerupRuntimeSystem.MaxPlayerSlots;
    }

    private void RefreshRuntimeState()
    {
        ResolveLocalReferences();
        ResolveRuntimeSystem();

        ownerController = null;
        playerIndex = 0;
        inCheckout = false;

        if (chainedCartManager != null)
        {
            currentKind = chainedCartManager.isCollectedByPlayer
                ? PowerupCartTargetKind.ChainedCart
                : PowerupCartTargetKind.LooseCart;

            if (currentKind == PowerupCartTargetKind.LooseCart)
            {
                return;
            }

            playerIndex = TagToPlayerIndex(
                chainedCartManager.gameObject.tag
            );
        }
        else if (leadingCartControl != null)
        {
            currentKind = PowerupCartTargetKind.LeadingCart;
            playerIndex = TagToPlayerIndex(
                leadingCartControl.gameObject.tag
            );
        }
        else
        {
            currentKind = PowerupCartTargetKind.None;
            return;
        }

        if (runtimeSystem != null)
        {
            if (currentKind == PowerupCartTargetKind.LeadingCart &&
                leadingCartControl != null)
            {
                runtimeSystem.TryGetPlayerForCartControl(
                    leadingCartControl,
                    out ownerController
                );
            }

            if (ownerController == null &&
                playerIndex >= 1 &&
                playerIndex <= PowerupRuntimeSystem.MaxPlayerSlots)
            {
                runtimeSystem.TryGetPlayer(
                    playerIndex,
                    out ownerController
                );
            }
        }

        if (ownerController != null)
        {
            playerIndex = ownerController.PlayerIndex;
        }

        CartControlScript ownerCartControl = currentKind ==
            PowerupCartTargetKind.LeadingCart
            ? leadingCartControl
            : ownerController != null
                ? ownerController.CartControlInput
                : null;

        inCheckout =
            (ownerCartControl != null && ownerCartControl.GetIsInPit()) ||
            (ownerController != null &&
             ownerController.IsUseBlocked(PowerupUseBlockReason.Checkout));
    }

    private void ResolveLocalReferences()
    {
        if (localReferencesResolved) return;

        if (chainedCartManager == null)
        {
            chainedCartManager =
                GetComponent<ChainedCartManager>() ??
                GetComponentInParent<ChainedCartManager>();
        }

        // ChainedCartManager is the authoritative distinction for follower
        // and loose carts. Never search children for it from a leading cart,
        // because a whole snake hierarchy may contain follower descendants.
        if (chainedCartManager != null)
        {
            leadingCartControl = null;
            localReferencesResolved = true;
            return;
        }

        if (leadingCartControl == null)
        {
            leadingCartControl = this.transform.parent.GetComponentInChildren<CartControlScript>(true);
        }

        localReferencesResolved = true;
    }

    private void ResolveRuntimeSystem()
    {
        if (runtimeSystem == null && Application.isPlaying)
        {
            runtimeSystem = FindFirstObjectByType<PowerupRuntimeSystem>();
        }
    }

    private static int TagToPlayerIndex(string objectTag)
    {
        switch (objectTag)
        {
            case "Player1": return 1;
            case "Player2": return 2;
            case "Player3": return 3;
            case "Player4": return 4;
            default: return 0;
        }
    }
}
