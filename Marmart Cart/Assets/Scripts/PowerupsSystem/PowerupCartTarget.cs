using System;
using System.Collections.Generic;
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
    private static readonly Dictionary<Collider, PowerupCartTarget>
        RegisteredColliderTargets =
            new Dictionary<Collider, PowerupCartTarget>();

    [Header("Auto-Resolved References")]
    [SerializeField] private CartControlScript leadingCartControl;
    [SerializeField] private ChainedCartManager chainedCartManager;
    [SerializeField] private PowerupRuntimeSystem runtimeSystem;

    [Header("Optional Leading Cart Collider Bridge")]
    [Tooltip(
        "For a leading cart whose physical colliders are on a sibling hierarchy, " +
        "assign the common Physics Colliders root here. Every Collider below " +
        "that root is registered to this target. Leave empty on chained/loose carts."
    )]
    [SerializeField] private Transform leadingCartCollisionRoot;

    [Header("Runtime - Read Only")]
    [SerializeField] private PowerupCartTargetKind currentKind;
    [SerializeField] private int playerIndex;
    [SerializeField] private bool inCheckout;
    [SerializeField] private PlayerPowerupController ownerController;
    [SerializeField] private int registeredColliderCount;

    private bool localReferencesResolved;
    private ChainedCartManager subscribedChainedCartManager;
    private PowerupRuntimeSystem subscribedRuntimeSystem;
    private readonly List<Collider> registeredColliders =
        new List<Collider>(4);

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
        ResolveLocalReferences();
        ResolveRuntimeSystem();
        EnsureSubscriptions();
        RefreshRuntimeState();
        RefreshColliderBindings();
    }

    private void OnDisable()
    {
        UnregisterColliders();
        UnsubscribeFromChainedCartManager();
        UnsubscribeFromRuntimeSystem();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetColliderRegistry()
    {
        RegisteredColliderTargets.Clear();
    }

    /// <summary>
    /// Immediately refreshes the Inspector diagnostics and gameplay snapshot.
    /// ChainedCartManager ownership events call this automatically; it is also
    /// safe for another cart-system transition to call explicitly if needed.
    /// </summary>
    public void RefreshFromCartState()
    {
        RefreshRuntimeState();
    }

    /// <summary>
    /// Resolves both supported cart layouts:
    /// - target marker on the hit collider or one of its parents;
    /// - leading-cart colliders explicitly registered from a sibling root.
    /// </summary>
    public static bool TryResolveFromCollider(
        Collider hitCollider,
        out PowerupCartTarget target)
    {
        target = null;
        if (hitCollider == null) return false;

        target = hitCollider.GetComponentInParent<PowerupCartTarget>();

        if (target != null)
        {
            return true;
        }

        if (!RegisteredColliderTargets.TryGetValue(
                hitCollider,
                out target))
        {
            return false;
        }

        if (target != null && target.isActiveAndEnabled)
        {
            return true;
        }

        RegisteredColliderTargets.Remove(hitCollider);
        target = null;
        return false;
    }

    /// <summary>
    /// Rebuilds the optional sibling-collider mapping. Normally called once by
    /// OnEnable; exposed so a dynamically rebuilt leading cart can refresh it.
    /// </summary>
    [ContextMenu("Refresh Power-up Collider Bindings")]
    public void RefreshColliderBindings()
    {
        UnregisterColliders();

        if (leadingCartCollisionRoot == null ||
            chainedCartManager != null)
        {
            return;
        }

        Collider[] colliders =
            leadingCartCollisionRoot.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider candidate = colliders[i];
            if (candidate == null) continue;

            if (RegisteredColliderTargets.TryGetValue(
                    candidate,
                    out PowerupCartTarget existingTarget) &&
                existingTarget != null &&
                existingTarget != this)
            {
                Debug.LogError(
                    $"[PowerupCartTarget] Collider '{candidate.name}' is already " +
                    $"registered to target '{existingTarget.name}'.",
                    candidate
                );
                continue;
            }

            RegisteredColliderTargets[candidate] = this;
            registeredColliders.Add(candidate);
        }

        registeredColliderCount = registeredColliders.Count;

        if (registeredColliderCount == 0)
        {
            Debug.LogWarning(
                "[PowerupCartTarget] Leading Cart Collision Root contains no " +
                "Collider components, so sibling collision resolution is unavailable.",
                this
            );
        }
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

        if (isActiveAndEnabled)
        {
            EnsureSubscriptions();
        }

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
        }
        else if (leadingCartControl != null)
        {
            currentKind = PowerupCartTargetKind.LeadingCart;
        }
        else
        {
            currentKind = PowerupCartTargetKind.None;
            return;
        }

        ownerController = ResolveOwnerControllerFromHierarchy();

        if (runtimeSystem != null)
        {
            if (ownerController == null &&
                currentKind == PowerupCartTargetKind.LeadingCart &&
                leadingCartControl != null)
            {
                runtimeSystem.TryGetPlayerForCartControl(
                    leadingCartControl,
                    out ownerController
                );
            }

            playerIndex = ownerController != null
                ? ownerController.PlayerIndex
                : ResolvePlayerIndexFromHierarchy();

            if (ownerController == null &&
                IsValidPlayerIndex(playerIndex))
            {
                runtimeSystem.TryGetPlayer(
                    playerIndex,
                    out ownerController
                );
            }
        }
        else
        {
            playerIndex = ownerController != null
                ? ownerController.PlayerIndex
                : ResolvePlayerIndexFromHierarchy();
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
        if (localReferencesResolved &&
            (chainedCartManager != null || leadingCartControl != null))
        {
            return;
        }

        localReferencesResolved = false;

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
            leadingCartControl =
                GetComponent<CartControlScript>() ??
                GetComponentInParent<CartControlScript>() ??
                GetComponentInChildren<CartControlScript>(true);

            // Supports a common leading-cart layout where this marker and the
            // CartControlScript live on sibling children of the same prefab.
            if (leadingCartControl == null && transform.parent != null)
            {
                leadingCartControl =
                    transform.parent.GetComponentInChildren<
                        CartControlScript>(true);
            }
        }

        localReferencesResolved = leadingCartControl != null;
    }

    private void ResolveRuntimeSystem()
    {
        if (runtimeSystem == null && Application.isPlaying)
        {
            runtimeSystem = FindFirstObjectByType<PowerupRuntimeSystem>();
        }
    }

    private void EnsureSubscriptions()
    {
        if (subscribedChainedCartManager != chainedCartManager)
        {
            UnsubscribeFromChainedCartManager();
            subscribedChainedCartManager = chainedCartManager;

            if (subscribedChainedCartManager != null)
            {
                subscribedChainedCartManager.OnCollectionStateChanged +=
                    HandleCollectionStateChanged;
            }
        }

        if (subscribedRuntimeSystem != runtimeSystem)
        {
            UnsubscribeFromRuntimeSystem();
            subscribedRuntimeSystem = runtimeSystem;

            if (subscribedRuntimeSystem != null)
            {
                subscribedRuntimeSystem.OnPlayerRegistered +=
                    HandlePlayerRegistered;
                subscribedRuntimeSystem.OnPlayerUnregistered +=
                    HandlePlayerUnregistered;
            }
        }
    }

    private void UnsubscribeFromChainedCartManager()
    {
        if (subscribedChainedCartManager != null)
        {
            subscribedChainedCartManager.OnCollectionStateChanged -=
                HandleCollectionStateChanged;
        }

        subscribedChainedCartManager = null;
    }

    private void UnsubscribeFromRuntimeSystem()
    {
        if (subscribedRuntimeSystem != null)
        {
            subscribedRuntimeSystem.OnPlayerRegistered -=
                HandlePlayerRegistered;
            subscribedRuntimeSystem.OnPlayerUnregistered -=
                HandlePlayerUnregistered;
        }

        subscribedRuntimeSystem = null;
    }

    private void UnregisterColliders()
    {
        for (int i = 0; i < registeredColliders.Count; i++)
        {
            Collider registeredCollider = registeredColliders[i];

            if (registeredCollider == null) continue;

            if (RegisteredColliderTargets.TryGetValue(
                    registeredCollider,
                    out PowerupCartTarget registeredTarget) &&
                registeredTarget == this)
            {
                RegisteredColliderTargets.Remove(registeredCollider);
            }
        }

        registeredColliders.Clear();
        registeredColliderCount = 0;
    }

    private void HandleCollectionStateChanged(ChainedCartManager source)
    {
        if (source == chainedCartManager)
        {
            RefreshRuntimeState();
        }
    }

    private void HandlePlayerRegistered(
        int registeredPlayerIndex,
        PlayerPowerupController registeredController)
    {
        RefreshRuntimeState();
    }

    private void HandlePlayerUnregistered(int unregisteredPlayerIndex)
    {
        RefreshRuntimeState();
    }

    private PlayerPowerupController ResolveOwnerControllerFromHierarchy()
    {
        PlayerPowerupController resolved = null;

        if (chainedCartManager != null)
        {
            resolved = chainedCartManager.GetComponentInParent<
                PlayerPowerupController>();
        }
        else if (leadingCartControl != null)
        {
            resolved = leadingCartControl.GetComponentInParent<
                PlayerPowerupController>();
        }

        return resolved != null
            ? resolved
            : GetComponentInParent<PlayerPowerupController>();
    }

    private int ResolvePlayerIndexFromHierarchy()
    {
        SnakeCartManager snakeCartManager = null;

        if (chainedCartManager != null)
        {
            snakeCartManager =
                chainedCartManager.GetComponentInParent<SnakeCartManager>();
        }
        else if (leadingCartControl != null)
        {
            snakeCartManager =
                leadingCartControl.GetComponentInParent<SnakeCartManager>();
        }

        if (snakeCartManager == null)
        {
            snakeCartManager = GetComponentInParent<SnakeCartManager>();
        }

        if (snakeCartManager != null)
        {
            int snakePlayerIndex = snakeCartManager.GetPlayerId();

            if (IsValidPlayerIndex(snakePlayerIndex))
            {
                return snakePlayerIndex;
            }
        }

        int resolvedIndex = FindPlayerIndexInParents(transform);

        if (!IsValidPlayerIndex(resolvedIndex) &&
            chainedCartManager != null)
        {
            resolvedIndex = FindPlayerIndexInParents(
                chainedCartManager.transform
            );
        }

        if (!IsValidPlayerIndex(resolvedIndex) &&
            leadingCartControl != null)
        {
            resolvedIndex = FindPlayerIndexInParents(
                leadingCartControl.transform
            );
        }

        return resolvedIndex;
    }

    private static int FindPlayerIndexInParents(Transform start)
    {
        Transform current = start;

        while (current != null)
        {
            int resolvedIndex = TagToPlayerIndex(current.gameObject.tag);

            if (IsValidPlayerIndex(resolvedIndex))
            {
                return resolvedIndex;
            }

            current = current.parent;
        }

        return 0;
    }

    private static bool IsValidPlayerIndex(int candidate)
    {
        return candidate >= 1 &&
               candidate <= PowerupRuntimeSystem.MaxPlayerSlots;
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
