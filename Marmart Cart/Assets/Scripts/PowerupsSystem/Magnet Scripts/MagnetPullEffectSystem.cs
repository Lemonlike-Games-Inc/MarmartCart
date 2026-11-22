using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-level executor for items with a MagnetPullEffect and a ConeItemUse
/// (U-shape Magnet).
///
/// LT is accepted only while the cone preview is shown. On use it finds the
/// loose carts, and optionally the rival leading carts, inside the published
/// cone that have a clear line to the thrower, spends the item (also when
/// none are found), then pulls each one toward the thrower every physics step.
///
/// One pull method for both kinds (ComputePullAcceleration), with their own
/// amounts. Loose carts are pushed through their Rigidbody and stop being
/// pulled when any chain collects them (the normal trigger rule) or the pull
/// times out. Leading carts are pushed through LeadingCartExternalForces and
/// stop being pulled when it times out or the cart cannot be moved (ghost,
/// checkout, frozen). Every pull stops when the thrower enters ghost mode or
/// the match ends.
/// </summary>
[DefaultExecutionOrder(140)]
[DisallowMultipleComponent]
public class MagnetPullEffectSystem : MonoBehaviour
{
    private const int OverlapBufferCapacity = 128;

    private struct PullState
    {
        public Rigidbody Body;
        public ChainedCartManager LooseCart;
        public LeadingCartExternalForces LeadingCart;
        public int OwnerPlayerIndex;
        public Transform Target;
        public LeadingCartBattleController TargetBattle;
        public float Speed;
        public float Acceleration;
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
    private readonly List<ChainedCartManager> foundLooseCarts = new List<ChainedCartManager>(16);
    private readonly List<LeadingCartExternalForces> foundLeadingCarts = new List<LeadingCartExternalForces>(4);
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
            bool isLoose = pull.LooseCart != null;

            // A loose cart collected by any chain, or destroyed by its
            // disappear timer; a leading cart that went away.
            if (pull.Body == null ||
                (isLoose && !pull.LooseCart.isAvailable) ||
                (!isLoose && pull.LeadingCart == null))
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

            Vector3 acceleration = ComputePullAcceleration(
                pull.Body.linearVelocity,
                pull.Target.position - pull.Body.position,
                pull.Speed,
                pull.Acceleration,
                isLoose,
                step
            );

            if (isLoose)
            {
                pull.Body.AddForce(acceleration, ForceMode.Acceleration);
            }
            else if (!pull.LeadingCart.TryAddAcceleration(acceleration))
            {
                // Ghost, checkout or frozen: this cart is no longer pulled.
                activePulls.RemoveAt(i);
            }
        }

        activePullCount = activePulls.Count;
    }

    /// <summary>
    /// The one pull method: accelerate toward the target until the closing
    /// speed reaches the pull speed, never overshooting in one step. With
    /// cancelSideways the body also loses its sideways drift and is slowed
    /// near the target, so it homes in (loose carts). Without it only the
    /// toward-target part is added (leading carts, whose own grip and
    /// steering act on the rest).
    /// </summary>
    private static Vector3 ComputePullAcceleration(
        Vector3 velocity,
        Vector3 toTarget,
        float pullSpeed,
        float maximumAcceleration,
        bool cancelSideways,
        float step)
    {
        toTarget.y = 0f;
        Vector3 planarVelocity = new Vector3(velocity.x, 0f, velocity.z);
        float distance = toTarget.magnitude;
        Vector3 velocityChange;

        if (distance <= 0.0001f)
        {
            velocityChange = cancelSideways ? -planarVelocity : Vector3.zero;
        }
        else
        {
            Vector3 direction = toTarget / distance;
            float closingSpeed = Vector3.Dot(planarVelocity, direction);
            float desiredClosingSpeed = Mathf.Min(pullSpeed, distance / step);
            float alongChange = desiredClosingSpeed - closingSpeed;

            if (!cancelSideways) alongChange = Mathf.Max(0f, alongChange);

            velocityChange = direction * alongChange;

            if (cancelSideways)
            {
                velocityChange -= planarVelocity - direction * closingSpeed;
            }
        }

        return Vector3.ClampMagnitude(velocityChange / step, maximumAcceleration);
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

        CartControlScript cartControl = controller.CartControlInput;

        FindTargetsInCone(aim, pullEffect, cartControl);

        if (!controller.TryConsumeEquippedItem(out ItemDefinition consumedItem) ||
            consumedItem != requestedItem)
        {
            Debug.LogWarning(
                $"[Magnet] P{controller.PlayerIndex} could not spend {requestedItem.name}; nothing was pulled.",
                this
            );
            ClearFoundTargets();
            return;
        }

        LeadingCartBattleController battle =
            cartControl.GetComponentInChildren<LeadingCartBattleController>(true);
        float now = Time.time;

        for (int i = 0; i < foundLooseCarts.Count; i++)
        {
            ChainedCartManager cart = foundLooseCarts[i];
            Rigidbody body = cart.GetComponent<Rigidbody>();
            if (body == null) continue;

            StartPull(new PullState
            {
                Body = body,
                LooseCart = cart,
                OwnerPlayerIndex = controller.PlayerIndex,
                Target = cartControl.transform,
                TargetBattle = battle,
                Speed = pullEffect.PullSpeed,
                Acceleration = pullEffect.PullAcceleration,
                EndsAtTime = now + pullEffect.MaximumPullSeconds
            });
        }

        for (int i = 0; i < foundLeadingCarts.Count; i++)
        {
            LeadingCartExternalForces leadingCart = foundLeadingCarts[i];

            StartPull(new PullState
            {
                Body = leadingCart.Body,
                LeadingCart = leadingCart,
                OwnerPlayerIndex = controller.PlayerIndex,
                Target = cartControl.transform,
                TargetBattle = battle,
                Speed = pullEffect.LeadingCartPullSpeed,
                Acceleration = pullEffect.LeadingCartPullAcceleration,
                EndsAtTime = now + pullEffect.LeadingCartPullSeconds
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
                ProjectileCount = foundLooseCarts.Count + foundLeadingCarts.Count
            }
        );

        if (logPulls)
        {
            Debug.Log(
                $"[Magnet] P{controller.PlayerIndex} used {requestedItem.name}: pulling " +
                $"{foundLooseCarts.Count} loose cart(s) and {foundLeadingCarts.Count} leading cart(s).",
                this
            );
        }

        ClearFoundTargets();
    }

    // Fills the found lists with loose carts and rival leading carts inside
    // the cone that have a clear line to the cone origin.
    private void FindTargetsInCone(
        PowerupAimState aim,
        MagnetPullEffect pullEffect,
        CartControlScript throwerCart)
    {
        ClearFoundTargets();

        Vector3 origin = aim.RangeOrigin;
        Vector3 forward = aim.AimDirection;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.000001f) return;
        forward.Normalize();

        float length = aim.ConeLength;
        float cosHalfAngle = Mathf.Cos(aim.ConeAngleDegrees * 0.5f * Mathf.Deg2Rad);
        bool includeLeadingCarts = pullEffect.PullLeadingCarts;

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
            ChainedCartManager looseCart = body != null
                ? body.GetComponent<ChainedCartManager>()
                : hit.GetComponentInParent<ChainedCartManager>();

            if (looseCart != null)
            {
                if (looseCart.isAvailable &&
                    !foundLooseCarts.Contains(looseCart) &&
                    IsInConeWithClearLine(looseCart.transform.position, origin, forward, length, cosHalfAngle, pullEffect))
                {
                    foundLooseCarts.Add(looseCart);
                }

                continue;
            }

            if (!includeLeadingCarts || body == null) continue;

            LeadingCartExternalForces leadingCart = body.GetComponent<LeadingCartExternalForces>();

            if (leadingCart == null ||
                leadingCart.CartControl == throwerCart ||
                foundLeadingCarts.Contains(leadingCart) ||
                !leadingCart.CanBeMoved ||
                !IsInConeWithClearLine(body.position, origin, forward, length, cosHalfAngle, pullEffect))
            {
                continue;
            }

            foundLeadingCarts.Add(leadingCart);
        }
    }

    private static bool IsInConeWithClearLine(
        Vector3 position,
        Vector3 origin,
        Vector3 forward,
        float length,
        float cosHalfAngle,
        MagnetPullEffect pullEffect)
    {
        Vector3 toTarget = position - origin;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        if (distance > length) return false;

        if (distance > 0.0001f &&
            Vector3.Dot(toTarget / distance, forward) < cosHalfAngle)
        {
            return false;
        }

        LayerMask blockingLayers = pullEffect.LineOfSightBlockingLayers;
        if (blockingLayers.value == 0) return true;

        Vector3 sightOffset = Vector3.up * pullEffect.LineOfSightHeight;

        return !Physics.Linecast(
            origin + sightOffset,
            position + sightOffset,
            blockingLayers,
            QueryTriggerInteraction.Ignore
        );
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

    // The newest magnet takes over a cart another magnet is pulling.
    private void StartPull(PullState pull)
    {
        for (int i = activePulls.Count - 1; i >= 0; i--)
        {
            if (activePulls[i].Body == pull.Body) activePulls.RemoveAt(i);
        }

        activePulls.Add(pull);
    }

    private void ClearFoundTargets()
    {
        foundLooseCarts.Clear();
        foundLeadingCarts.Clear();
    }

    // Loose carts are let go with a little momentum instead of a dead stop.
    // Leading carts keep their speed; their own drive and grip take over.
    private static void ReleasePull(PullState pull)
    {
        if (pull.LooseCart == null || pull.Body == null || !pull.LooseCart.isAvailable) return;

        Vector3 velocity = pull.Body.linearVelocity;
        velocity.x *= 0.3f;
        velocity.z *= 0.3f;
        pull.Body.linearVelocity = velocity;
    }

    private void ReleaseAllPulls()
    {
        for (int i = 0; i < activePulls.Count; i++)
        {
            ReleasePull(activePulls[i]);
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
