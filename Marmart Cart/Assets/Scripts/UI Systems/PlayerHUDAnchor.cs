using UnityEngine;

/// <summary>
/// The point a player's world HUD follows. Lives on the HUDWorldAnchor object of
/// the leading cart and registers with PlayerWorldHUDSystem while enabled.
///
/// The player number comes from the parent SnakeCartManager, so a leading cart
/// outside a player's chain (results screen, props) never registers.
/// </summary>
[DisallowMultipleComponent]
public class PlayerHUDAnchor : MonoBehaviour
{
    private PlayerWorldHUDSystem registeredSystem;
    private int registeredPlayerIndex;

    private void OnEnable()
    {
        TryRegister();
    }

    private void Start()
    {
        // Covers a cart that was enabled before the HUD system's Awake.
        TryRegister();
    }

    private void OnDisable()
    {
        // Unity null check: the system may already be destroyed during scene unload.
        if (registeredSystem != null)
        {
            registeredSystem.UnregisterPlayer(registeredPlayerIndex, transform);
        }

        registeredSystem = null;
        registeredPlayerIndex = 0;
    }

    private void TryRegister()
    {
        if (registeredSystem != null) return;

        PlayerWorldHUDSystem system = PlayerWorldHUDSystem.Instance;
        if (system == null) return;

        SnakeCartManager owner = GetComponentInParent<SnakeCartManager>();
        if (owner == null) return;

        GameObject cartRoot = FindCartRoot(owner.transform);

        if (cartRoot == null)
        {
            Debug.LogWarning("[UI] PlayerHUDAnchor is under a SnakeCartManager but not inside one of its carts.", this);
            return;
        }

        int playerIndex = owner.GetPlayerId();
        if (!system.RegisterPlayer(playerIndex, cartRoot, transform)) return;

        registeredSystem = system;
        registeredPlayerIndex = playerIndex;
    }

    // SnakeCartManager spawns the leading cart as its direct child.
    private GameObject FindCartRoot(Transform ownerTransform)
    {
        Transform current = transform;

        while (current.parent != null && current.parent != ownerTransform)
        {
            current = current.parent;
        }

        return current.parent == ownerTransform ? current.gameObject : null;
    }
}
