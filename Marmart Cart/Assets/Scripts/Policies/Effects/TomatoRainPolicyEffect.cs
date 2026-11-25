using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tomato Rain: from time to time a red circle appears on the floor, fills from
/// its centre, then tomatoes rain into it fast for a short time. A leading cart
/// that is hit gets the normal Tomato effect; carts in a checkout are not hit.
///
/// The circle goes to a random floor point inside the area the players occupy
/// (a padded box around all leading carts), not onto a player. The drops are the
/// Rain Item (the normal Tomato) launched from the PowerupProjectilePool with no
/// owner (player 0), so the normal Tomato hit, splat and splash systems handle
/// them. All timing is in game time.
/// Cues: "rain-warning", "rain-start", "rain-end". TomatoRainZoneRenderer draws the circle.
/// </summary>
[DisallowMultipleComponent]
public sealed class TomatoRainPolicyEffect : PolicyEffect
{
    private const int MaxPlayers = PowerupRuntimeSystem.MaxPlayerSlots;

    // Rain shots count from here so they never share a number with a player's volley.
    private const uint FirstShotVersion = 0x80000000u;

    public enum Phase
    {
        Waiting,
        Warning,
        Raining
    }

    private struct LaunchedDrop
    {
        public FakeArcProjectile Projectile;
        public uint ShotVersion;
    }

    [Header("References")]
    [Tooltip("The thrown item that rains down. Its Throw use and effects apply (the normal Tomato).")]
    [SerializeField] private ItemDefinition rainItem;

    [Tooltip("Ground and blocking layers for placing the circle and the drops. Auto-found from a player's targeting controller if left empty.")]
    [SerializeField] private PowerupGameplayProfile gameplayProfile;

    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private PowerupProjectilePool projectilePool;

    [Tooltip("Auto-found if left empty. Gives each player's leading cart.")]
    [SerializeField] private PowerupRuntimeSystem runtimeSystem;

    [Header("Timing (game seconds, random between X and Y)")]
    [Tooltip("From the start of the Round to the first circle.")]
    [SerializeField] private Vector2 firstRainDelay = new Vector2(10f, 15f);

    [Tooltip("From the end of one rain to the next circle.")]
    [SerializeField] private Vector2 gapSeconds = new Vector2(12f, 20f);

    [Tooltip("How long the circle takes to fill before the rain.")]
    [Min(0f)]
    [SerializeField] private float warningSeconds = 3f;

    [Tooltip("How long the tomatoes keep falling.")]
    [Min(0f)]
    [SerializeField] private float rainSeconds = 2.5f;

    [Header("Area")]
    [Tooltip("Radius of the rain circle, in metres.")]
    [Min(0.5f)]
    [SerializeField] private float radius = 6f;

    [Tooltip("Margin around the box of all leading carts in which the circle may appear, in metres.")]
    [Min(0f)]
    [SerializeField] private float placementPadding = 6f;

    [Tooltip("A floor point higher than this above the carts' average height (a shelf top) is rejected.")]
    [Min(0f)]
    [SerializeField] private float maxHeightAboveCarts = 1.5f;

    [Tooltip("Radius of the check that rejects a centre inside a shelf or wall.")]
    [Min(0f)]
    [SerializeField] private float obstacleCheckRadius = 1f;

    [Min(1)]
    [SerializeField] private int placementTries = 10;

    [Tooltip("Wait before trying again when no floor point was found.")]
    [Min(0.1f)]
    [SerializeField] private float placementRetrySeconds = 2f;

    [Header("Drops")]
    [Min(0f)]
    [SerializeField] private float dropsPerSecond = 16f;

    [Tooltip("Height above the floor where each tomato starts falling.")]
    [Min(0.5f)]
    [SerializeField] private float dropHeight = 14f;

    [Tooltip("Seconds from the start height to the floor.")]
    [Min(0.05f)]
    [SerializeField] private float fallSeconds = 0.45f;

    [SerializeField] private float dropSpinDegreesPerSecond = 540f;

    [Header("Runtime - Read Only")]
    [SerializeField] private Phase phase = Phase.Waiting;
    [SerializeField] private float phaseTime;
    [SerializeField] private float phaseDuration;
    [SerializeField] private Vector3 zoneCentre;
    [SerializeField] private Vector3 zoneNormal = Vector3.up;

    private readonly List<LaunchedDrop> drops = new List<LaunchedDrop>(32);
    private ThrowItemUse throwUse;
    private float dropBudget;
    private uint nextShotVersion = FirstShotVersion;

    public Phase CurrentPhase => phase;

    /// <summary>True while a circle is on the floor (filling or raining).</summary>
    public bool HasZone => IsRunning && phase != Phase.Waiting;

    public Vector3 ZoneCentre => zoneCentre;
    public Vector3 ZoneNormal => zoneNormal;
    public float ZoneRadius => radius;

    /// <summary>How full the circle is: 0 to 1 while filling, 1 while raining.</summary>
    public float Fill01 => phase == Phase.Raining ? 1f : phase == Phase.Warning && phaseDuration > 0f ? Mathf.Clamp01(phaseTime / phaseDuration) : 0f;

    /// <summary>0 to 1 through the rain; 0 otherwise.</summary>
    public float Rain01 => phase == Phase.Raining && phaseDuration > 0f ? Mathf.Clamp01(phaseTime / phaseDuration) : 0f;

    protected override void OnPolicyStarted()
    {
        ResolveReferences();

        if (rainItem == null || !rainItem.TryGetUse(out throwUse) || projectilePool == null || gameplayProfile == null || runtimeSystem == null)
        {
            Debug.LogError("[Policy] Tomato Rain needs a Rain Item with a Throw use, a projectile pool, a gameplay profile and the powerup runtime system. It does nothing.", this);
            throwUse = null;
            return;
        }

        drops.Clear();
        dropBudget = 0f;
        EnterPhase(Phase.Waiting, RandomIn(firstRainDelay));
    }

    protected override void OnPolicyStopped(PolicyStopReason reason)
    {
        // During a load or teardown the pool may already be gone; the drops go with the scene.
        if (reason == PolicyStopReason.RoundEnded || reason == PolicyStopReason.MatchStartFailed)
            CancelDrops();

        drops.Clear();
        phase = Phase.Waiting;
        throwUse = null;
    }

    private void Update()
    {
        if (!IsRunning || throwUse == null) return;

        float deltaTime = Time.deltaTime;
        phaseTime += deltaTime;

        if (phase == Phase.Raining) Rain(deltaTime);
        if (phaseTime >= phaseDuration) AdvancePhase();

        PruneDrops();
    }

    private void AdvancePhase()
    {
        switch (phase)
        {
            case Phase.Waiting:
                if (!TryPickZone(out zoneCentre, out zoneNormal))
                {
                    EnterPhase(Phase.Waiting, placementRetrySeconds);
                    break;
                }

                EnterPhase(Phase.Warning, warningSeconds);
                Debug.Log($"[Policy] Tomato Rain: circle at ({zoneCentre.x:0.0}, {zoneCentre.z:0.0}), rain in {phaseDuration:0.0} s.", this);
                RaiseCue("rain-warning");
                break;

            case Phase.Warning:
                EnterPhase(Phase.Raining, rainSeconds);
                dropBudget = 0f;
                RaiseCue("rain-start");
                break;

            case Phase.Raining:
                EnterPhase(Phase.Waiting, RandomIn(gapSeconds));
                Debug.Log($"[Policy] Tomato Rain: rain over. Next circle in {phaseDuration:0.0} s.", this);
                RaiseCue("rain-end");
                break;
        }
    }

    private void Rain(float deltaTime)
    {
        dropBudget += dropsPerSecond * deltaTime;

        while (dropBudget >= 1f)
        {
            dropBudget -= 1f;
            LaunchDrop();
        }
    }

    private void LaunchDrop()
    {
        Vector2 offset = UnityEngine.Random.insideUnitCircle * radius;
        Vector3 point = zoneCentre + new Vector3(offset.x, 0f, offset.y);

        Vector3 landing = point;
        Vector3 normal = zoneNormal;

        if (Physics.Raycast(point + Vector3.up * gameplayProfile.GroundProbeHeight, Vector3.down, out RaycastHit hit,
                gameplayProfile.GroundProbeDistance, gameplayProfile.PowerupGroundMask, QueryTriggerInteraction.Ignore))
        {
            normal = hit.normal.sqrMagnitude > 0.000001f ? hit.normal.normalized : Vector3.up;
            landing = hit.point;
        }

        landing += normal * gameplayProfile.LandingClearance;

        float yaw = UnityEngine.Random.Range(0f, 360f);
        ProjectileShotPattern pattern = throwUse.Pattern;
        Quaternion castRotation = pattern != null ? Quaternion.Euler(pattern.CastEulerAngles) : Quaternion.identity;

        var launch = new ResolvedPowerupProjectileLaunch
        {
            ShotVersion = nextShotVersion,
            PatternEntryIndex = 0,
            Item = rainItem,
            OwnerPlayerIndex = 0,
            StartPosition = landing + Vector3.up * dropHeight,
            LandingPosition = landing,
            LandingNormal = normal,
            FlightDuration = fallSeconds,
            ArcHeight = 0f,
            CollisionRotation = Quaternion.Euler(0f, yaw, 0f) * castRotation,
            CartTargetMask = projectilePool.ProjectileProfile != null ? projectilePool.ProjectileProfile.CartTargetMask : default,
            EnvironmentBlockingMask = gameplayProfile.ProjectileBlockingMask,
            HasEffectOnLeadingCart = pattern == null || pattern.HasEffectOnLeadingCart,
            HasEffectOnChainedCarts = pattern != null && pattern.HasEffectOnChainedCarts,
            IgnoreOwnerInFlight = false,
            CanHitOwner = false,
            IgnoreCheckoutTargets = true,
            VisualSpinAxis = UnityEngine.Random.onUnitSphere,
            VisualSpinDegreesPerSecond = dropSpinDegreesPerSecond,
            VisualScaleMultiplier = 1f
        };

        if (!projectilePool.TryLaunch(throwUse, launch, out FakeArcProjectile projectile)) return;

        drops.Add(new LaunchedDrop { Projectile = projectile, ShotVersion = nextShotVersion });
        nextShotVersion = nextShotVersion == uint.MaxValue ? FirstShotVersion : nextShotVersion + 1u;
    }

    // A random floor point in the padded box around all leading carts, clear of shelves and walls.
    private bool TryPickZone(out Vector3 centre, out Vector3 normal)
    {
        centre = default;
        normal = Vector3.up;

        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        float heightSum = 0f;
        int carts = 0;

        for (int playerIndex = 1; playerIndex <= MaxPlayers; playerIndex++)
        {
            if (!runtimeSystem.TryGetPlayer(playerIndex, out PlayerItemHand hand) || hand == null) continue;

            CartControlScript cart = hand.CartControlInput;
            if (cart == null || !cart.isActiveAndEnabled) continue;

            Vector3 position = cart.transform.position;
            min = Vector2.Min(min, new Vector2(position.x, position.z));
            max = Vector2.Max(max, new Vector2(position.x, position.z));
            heightSum += position.y;
            carts++;
        }

        if (carts == 0) return false;

        min -= Vector2.one * placementPadding;
        max += Vector2.one * placementPadding;
        float cartHeight = heightSum / carts;

        for (int i = 0; i < placementTries; i++)
        {
            Vector3 probe = new Vector3(
                UnityEngine.Random.Range(min.x, max.x),
                cartHeight + gameplayProfile.GroundProbeHeight,
                UnityEngine.Random.Range(min.y, max.y));

            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, gameplayProfile.GroundProbeHeight + gameplayProfile.GroundProbeDistance,
                    gameplayProfile.PowerupGroundMask, QueryTriggerInteraction.Ignore))
                continue;

            if (hit.point.y > cartHeight + maxHeightAboveCarts) continue;

            if (obstacleCheckRadius > 0f && gameplayProfile.HasProjectileBlockingMask &&
                Physics.CheckSphere(hit.point + Vector3.up * (obstacleCheckRadius + 0.2f), obstacleCheckRadius,
                    gameplayProfile.ProjectileBlockingMask, QueryTriggerInteraction.Ignore))
                continue;

            centre = hit.point;
            normal = hit.normal.sqrMagnitude > 0.000001f ? hit.normal.normalized : Vector3.up;
            return true;
        }

        return false;
    }

    private void PruneDrops()
    {
        for (int i = drops.Count - 1; i >= 0; i--)
        {
            if (!IsStillFlying(drops[i])) drops.RemoveAt(i);
        }
    }

    private void CancelDrops()
    {
        for (int i = drops.Count - 1; i >= 0; i--)
        {
            LaunchedDrop drop = drops[i];
            if (IsStillFlying(drop)) drop.Projectile.Cancel();
        }
    }

    // A pooled actor may already fly again for a player, so check it is still this drop.
    private bool IsStillFlying(LaunchedDrop drop)
    {
        FakeArcProjectile projectile = drop.Projectile;
        return projectile != null && projectile.ActiveFlight && projectile.ActiveShotVersion == drop.ShotVersion && projectile.ActiveItem == rainItem;
    }

    private void ResolveReferences()
    {
        if (projectilePool == null) projectilePool = FindFirstObjectByType<PowerupProjectilePool>();
        if (runtimeSystem == null) runtimeSystem = FindFirstObjectByType<PowerupRuntimeSystem>();

        if (gameplayProfile == null)
        {
            PowerupTargetingController targeting = FindFirstObjectByType<PowerupTargetingController>();
            if (targeting != null) gameplayProfile = targeting.GameplayProfile;
        }
    }

    private void EnterPhase(Phase next, float duration)
    {
        phase = next;
        phaseTime = 0f;
        phaseDuration = Mathf.Max(0f, duration);
    }

    private static float RandomIn(Vector2 range)
    {
        return UnityEngine.Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
    }
}
