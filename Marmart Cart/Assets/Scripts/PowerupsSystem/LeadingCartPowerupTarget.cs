using UnityEngine;

/// <summary>
/// Explicit marker for the only cart hierarchy that flying power-ups may hit.
/// Follower carts must not carry this component or use its dedicated layer.
/// </summary>
[DisallowMultipleComponent]
public class LeadingCartPowerupTarget : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CartControlScript cartControlInput;
    [SerializeField] private PowerupRuntimeSystem runtimeSystem;

    [Header("Runtime - Read Only")]
    [SerializeField] private int playerIndex;
    [SerializeField] private PlayerPowerupController ownerController;

    public CartControlScript CartControlInput => cartControlInput;

    public int PlayerIndex
    {
        get
        {
            TryResolveOwner();
            return playerIndex;
        }
    }

    public PlayerPowerupController OwnerController
    {
        get
        {
            TryResolveOwner();
            return ownerController;
        }
    }

    public bool IsInCheckout
    {
        get
        {
            TryResolveOwner();

            if (cartControlInput != null && cartControlInput.GetIsInPit())
            {
                return true;
            }

            return ownerController != null &&
                   ownerController.IsUseBlocked(
                       PowerupUseBlockReason.Checkout
                   );
        }
    }

    private void Awake()
    {
        ResolveReferences();
        TryResolveOwner();
    }

    private void OnEnable()
    {
        TryResolveOwner();
    }

    public bool TryResolveOwner()
    {
        ResolveReferences();

        if (ownerController != null)
        {
            playerIndex = ownerController.PlayerIndex;
            return playerIndex >= 1 &&
                   playerIndex <= PowerupRuntimeSystem.MaxPlayerSlots;
        }

        if (runtimeSystem != null &&
            cartControlInput != null &&
            runtimeSystem.TryGetPlayerForCartControl(
                cartControlInput,
                out PlayerPowerupController resolvedController
            ))
        {
            ownerController = resolvedController;
            playerIndex = ownerController.PlayerIndex;
            return true;
        }

        playerIndex = ResolvePlayerIndexFromTag();
        return playerIndex >= 1 &&
               playerIndex <= PowerupRuntimeSystem.MaxPlayerSlots;
    }

    private void ResolveReferences()
    {
        if (cartControlInput == null)
        {
            cartControlInput =
                GetComponent<CartControlScript>() ??
                GetComponentInParent<CartControlScript>() ??
                GetComponentInChildren<CartControlScript>(true);
        }

        if (runtimeSystem == null)
        {
            runtimeSystem = FindFirstObjectByType<PowerupRuntimeSystem>();
        }
    }

    private int ResolvePlayerIndexFromTag()
    {
        GameObject taggedObject = cartControlInput != null
            ? cartControlInput.gameObject
            : gameObject;

        string objectTag = taggedObject.tag;

        if (objectTag == "Player1") return 1;
        if (objectTag == "Player2") return 2;
        if (objectTag == "Player3") return 3;
        if (objectTag == "Player4") return 4;
        return 0;
    }
}
