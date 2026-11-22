using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-level executor for items with a MagnetPullEffect and a ConeItemUse
/// (U-shape Magnet).
///
/// LT is accepted only while the cone preview is shown. On use it finds the
/// loose carts inside the published cone that have a clear line to the
/// leading cart, spends the item (also when none are found), then moves each
/// cart toward the thrower every physics step. A cart stops being pulled when
/// any chain collects it (the normal trigger rule), when the pull times out,
/// when the thrower enters ghost mode, or when the match ends.
/// </summary>
[DefaultExecutionOrder(140)]
[DisallowMultipleComponent]
public class MagnetPullEffectSystem : MonoBehaviour
{
    private const int OverlapBufferCapacity = 128;

    private struct PullState
    {
        public ChainedCartManager Cart;
        public Rigidbody Body;
        public int OwnerPlayerIndex;
        public Transform Target;
        public LeadingCartBattleController TargetBattle;
        public float Speed;
        public float EndsAtTime;
    }

    [Header("References")]
    [SerializeField] private PowerupRuntimeSystem runtimeSystem;
    [SerializeField] private PowerupLifecycleEventSystem lifecycleEventSystem;
    [SerializeField] private PowerupAimStateSystem aimStateSystem;

    [Header("Diagnostics")]
    [SerializeField] private bool logPulls = true;

    [Header("Runtime - Read Only")]
    [SerializeField] private int activePullCount;

    private readonly PlayerItemHand[] subscribedPlayers =
        new PlayerItemHand[PowerupRuntimeSystem.MaxPlayerSlots];

    private readonly Collider[] overlapBuffer = new Collider[OverlapBufferCapacity];
    private readonly List<ChainedCartManager> foundCarts = new List<ChainedCartManager>(16);
    private readonly List<PullState> activePulls = new List<PullState>(16);

    private PowerupRuntimeSystem subscribedRuntimeSystem;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureRuntimeSubscription();
    }

    private void Start()
    {
        ResolveReferences();
        EnsureRuntimeSubscription();
    }

    private void OnDisable()
    {
        ReleaseAllPulls();
        UnsubscribeRuntimeAndPlayers();
    }

    private void FixedUpdate()
    {
        if (activePulls.Count == 0) return;

        float now = Time.time;
        float step = Mathf.Max(0.0001f, Time.fixedDeltaTime);

        for (int i = activePulls.Count - 1; i >= 0; i--)
        {
            PullState pull = activePulls[i];

            // Collected by any chain, or destroyed by its disappear timer.
            if (pull.Cart == null || pull.Body == null || !pull.Cart.isAvailable)
            {
                activePulls.RemoveAt(i);
                continue;
            }

            if (pull.Target == null ||
                now >= pull.EndsAtTime ||
                (pull.TargetBattle != null && pull.TargetBattle.IsInGhostMode))
            {
                ReleasePull(pull);
                activePulls.RemoveAt(i);
                continue;
            }

            Vector3 toTarget = pull.Target.position - pull.Body.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            Vector3 velocity = pull.Body.linearVelocity;

            if (distance > 0.0001f)
            {
                // Never overshoot the leading cart in one step.
                float speed = Mathf.Min(pull.Speed, distance / step);
                Vector3 planar = toTarget / distance * speed;
                velocity.x = planar.x;
                velocity.z = planar.z;
            }
            else
            {
                velocity.x = 0f;
                velocity.z = 0f;
            }

            pull.Body.linearVelocity = velocity;
        }

        activePullCount = activePulls.Count;
    }

    private bool ValidateItemUse(
        PlayerItemHand controller,
        ItemDefinition requestedItem)
    {
        if (!IsMagnetItem(requestedItem, out _, out _)) return true;

        return controller != null &&
               controller.CartControlInput != null &&
               TryGetConeAim(controller, requestedItem, out _);
    }

    private void HandleItemUseRequested(
        PlayerItemHand controller,
        ItemDefinition requestedItem)
    {
        if (!IsMagnetItem(requestedItem, out MagnetPullEffect pullEffect, out _)) return;

        if (controller == null ||
            controller.CartControlInput == null ||
            !controller.HasEquippedItem ||
            controller.EquippedItem != requestedItem ||
            !TryGetConeAim(controller, requestedItem, out PowerupAimState aim))
        {
            return;
        }

        FindLooseCartsInCone(aim, pullEffect);

        if (!controller.TryConsumeEquippedItem(out ItemDefinition consumedItem) ||
            consumedItem != requestedItem)
        {
            Debug.LogWarning(
                $"[Magnet] P{controller.PlayerIndex} could not spend {requestedItem.name}; nothing was pulled.",
                this
            );
            foundCarts.Clear();
            return;
        }

        CartControlScript cartControl = controller.CartControlInput;
        LeadingCartBattleController battle =
            cartControl.GetComponentInChildren<LeadingCartBattleController>(true);
        float endsAt = Time.time + pullEffect.MaximumPullSeconds;

        for (int i = 0; i < foundCarts.Count; i++)
        {
            ChainedCartManager cart = foundCarts[i];
            Rigidbody body = cart.GetComponent<Rigidbody>();
            if (body == null) continue;

            // The newest magnet takes over a cart another magnet is pulling.
            RemovePullForCart(cart);

            activePulls.Add(new PullState
            {
                Cart = cart,
                Body = body,
                OwnerPlayerIndex = controller.PlayerIndex,
                Target = cartControl.transform,
                TargetBattle = battle,
                Speed = pullEffect.PullSpeed,
                EndsAtTime = endsAt
            });
        }

        activePullCount = activePulls.Count;

        lifecycleEventSystem?.PublishActivated(
            new PowerupActivationEvent
            {
                Item = requestedItem,
                Mode = PowerupActivationMode.Instant,
                ActivationVersion = controller.AcceptedUseRequestVersion,
                OwnerPlayerIndex = controller.PlayerIndex,
                OwnerController = controller,
                Position = aim.RangeOrigin,
                Direction = aim.AimDirection,
                ProjectileCount = foundCarts.Count
            }
        );

        if (logPulls)
        {
            Debug.Log(
                $"[Magnet] P{controller.PlayerIndex} used {requestedItem.name}: pulling {foundCarts.Count} loose cart(s).",
                this
            );
        }

        foundCarts.Clear();
    }

    // Fills foundCarts with loose carts inside the cone that have a clear line
    // to the cone origin.
    private void FindLooseCartsInCone(PowerupAimState aim, MagnetPullEffect pullEffect)
    {
        foundCarts.Clear();

        Vector3 origin = aim.RangeOrigin;
        Vector3 forward = aim.AimDirection;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.000001f) return;
        forward.Normalize();

        float length = aim.ConeLength;
        float cosHalfAngle = Mathf.Cos(aim.ConeAngleDegrees * 0.5f * Mathf.Deg2Rad);
        Vector3 sightOffset = Vector3.up * pullEffect.LineOfSightHeight;
        LayerMask blockingLayers = pullEffect.LineOfSightBlockingLayers;

        int hitCount = Physics.OverlapSphereNonAlloc(
            origin,
            length,
            overlapBuffer,
            pullEffect.CartLayers,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapBuffer[i];
            if (hit == null) continue;

            Rigidbody body = hit.attachedRigidbody;
            ChainedCartManager cart = body != null
                ? body.GetComponent<ChainedCartManager>()
                : hit.GetComponentInParent<ChainedCartManager>();

            if (cart == null || !cart.isAvailable || foundCarts.Contains(cart)) continue;

            Vector3 cartPosition = cart.transform.position;
            Vector3 toCart = cartPosition - origin;
            toCart.y = 0f;
            float distance = toCart.magnitude;

            if (distance > length) continue;

            if (distance > 0.0001f &&
                Vector3.Dot(toCart / distance, forward) < cosHalfAngle)
            {
                continue;
            }

            if (blockingLayers.value != 0 &&
                Physics.Linecast(
                    origin + sightOffset,
                    cartPosition + sightOffset,
                    blockingLayers,
                    QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            foundCarts.Add(cart);
        }
    }

    // The cone the player sees is the cone that is used.
    private bool TryGetConeAim(
        PlayerItemHand controller,
        ItemDefinition item,
        out PowerupAimState aim)
    {
        aim = default;

        if (aimStateSystem == null) ResolveReferences();

        return aimStateSystem != null &&
               aimStateSystem.TryGetState(controller.PlayerIndex, out aim) &&
               aim.Visible &&
               aim.Item == item &&
               aim.ConeLength > 0f;
    }

    private static bool IsMagnetItem(
        ItemDefinition item,
        out MagnetPullEffect pullEffect,
        out ConeItemUse coneUse)
    {
        coneUse = null;
        pullEffect = null;

        return item != null &&
               item.TryGetEffect(out pullEffect) &&
               item.TryGetUse(out coneUse);
    }

    private void RemovePullForCart(ChainedCartManager cart)
    {
        for (int i = activePulls.Count - 1; i >= 0; i--)
        {
            if (activePulls[i].Cart == cart) activePulls.RemoveAt(i);
        }
    }

    private static void ReleasePull(PullState pull)
    {
        if (pull.Body == null) return;

        // Let go with a little momentum instead of a dead stop.
        Vector3 velocity = pull.Body.linearVelocity;
        velocity.x *= 0.3f;
        velocity.z *= 0.3f;
        pull.Body.linearVelocity = velocity;
    }

    private void ReleaseAllPulls()
    {
        for (int i = 0; i < activePulls.Count; i++)
        {
            PullState pull = activePulls[i];
            if (pull.Cart != null && pull.Cart.isAvailable) ReleasePull(pull);
        }

        activePulls.Clear();
        activePullCount = 0;
    }

    private void ReleasePlayerPulls(int playerIndex)
    {
        for (int i = activePulls.Count - 1; i >= 0; i--)
        {
            if (activePulls[i].OwnerPlayerIndex != playerIndex) continue;

            ReleasePull(activePulls[i]);
            activePulls.RemoveAt(i);
        }

        activePullCount = activePulls.Count;
    }

    private void HandleMatchPlayingChanged(bool matchPlaying)
    {
        if (matchPlaying || activePulls.Count == 0) return;

        if (logPulls)
        {
            Debug.Log($"[Magnet] Match ended: released {activePulls.Count} pulled cart(s).", this);
        }

        ReleaseAllPulls();
    }

    private void HandlePlayerRegistered(int playerIndex, PlayerItemHand controller)
    {
        SubscribePlayer(playerIndex, controller);
    }

    private void HandlePlayerUnregistered(int playerIndex)
    {
        ReleasePlayerPulls(playerIndex);
        UnsubscribePlayer(playerIndex - 1);
    }

    private void ResolveReferences()
    {
        if (runtimeSystem == null)
        {
            runtimeSystem = FindFirstObjectByType<PowerupRuntimeSystem>();
        }

        if (lifecycleEventSystem == null)
        {
            lifecycleEventSystem = FindFirstObjectByType<PowerupLifecycleEventSystem>();
        }

        if (aimStateSystem == null)
        {
            aimStateSystem = FindFirstObjectByType<PowerupAimStateSystem>();
        }
    }

    private void EnsureRuntimeSubscription()
    {
        if (subscribedRuntimeSystem == runtimeSystem)
        {
            SynchronizeRegisteredPlayers();
            return;
        }

        UnsubscribeRuntimeAndPlayers();
        subscribedRuntimeSystem = runtimeSystem;

        if (subscribedRuntimeSystem == null) return;

        subscribedRuntimeSystem.OnPlayerRegistered += HandlePlayerRegistered;
        subscribedRuntimeSystem.OnPlayerUnregistered += HandlePlayerUnregistered;
        subscribedRuntimeSystem.OnMatchPlayingChanged += HandleMatchPlayingChanged;

        SynchronizeRegisteredPlayers();
    }

    private void SynchronizeRegisteredPlayers()
    {
        if (subscribedRuntimeSystem == null) return;

        for (int playerIndex = 1; playerIndex <= PowerupRuntimeSystem.MaxPlayerSlots; playerIndex++)
        {
            if (subscribedRuntimeSystem.TryGetPlayer(playerIndex, out PlayerItemHand controller))
            {
                SubscribePlayer(playerIndex, controller);
            }
            else
            {
                UnsubscribePlayer(playerIndex - 1);
            }
        }
    }

    private void SubscribePlayer(int playerIndex, PlayerItemHand controller)
    {
        int slotIndex = playerIndex - 1;

        if (slotIndex < 0 || slotIndex >= subscribedPlayers.Length || controller == null) return;
        if (subscribedPlayers[slotIndex] == controller) return;

        UnsubscribePlayer(slotIndex);
        subscribedPlayers[slotIndex] = controller;
        controller.OnItemUseValidationRequested += ValidateItemUse;
        controller.OnItemUseRequested += HandleItemUseRequested;
    }

    private void UnsubscribePlayer(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= subscribedPlayers.Length) return;

        PlayerItemHand controller = subscribedPlayers[slotIndex];

        if (controller != null)
        {
            controller.OnItemUseValidationRequested -= ValidateItemUse;
            controller.OnItemUseRequested -= HandleItemUseRequested;
        }

        subscribedPlayers[slotIndex] = null;
    }

    private void UnsubscribeRuntimeAndPlayers()
    {
        if (subscribedRuntimeSystem != null)
        {
            subscribedRuntimeSystem.OnPlayerRegistered -= HandlePlayerRegistered;
            subscribedRuntimeSystem.OnPlayerUnregistered -= HandlePlayerUnregistered;
            subscribedRuntimeSystem.OnMatchPlayingChanged -= HandleMatchPlayingChanged;
        }

        for (int i = 0; i < subscribedPlayers.Length; i++)
        {
            UnsubscribePlayer(i);
        }

        subscribedRuntimeSystem = null;
    }
}
