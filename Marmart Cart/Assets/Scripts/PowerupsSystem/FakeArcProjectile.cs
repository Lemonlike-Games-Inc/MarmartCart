using UnityEngine;

/// <summary>
/// Pooled, transform-driven projectile actor. Its root SphereCollider or
/// BoxCollider is the designer-authored hitbox source; runtime detection still
/// uses swept non-alloc casts so no Rigidbody is required.
/// </summary>
[DisallowMultipleComponent]
public class FakeArcProjectile : MonoBehaviour
{
    // A volley may pass through many ignored owner/follower colliders because
    // all carts intentionally share one layer. Keep this fixed buffer generous
    // enough that ignored carts do not hide a valid farther hit.
    private const int HitBufferCapacity = 64;

    [Header("Swappable Presentation")]
    [Tooltip(
        "Assign the mesh/model child that may spin and scale. Keep it separate " +
        "from the actor root so the root hitbox remains deterministic."
    )]
    [SerializeField] private Transform visualRoot;

    [Header("Designer-Authored Hitbox")]
    [Tooltip(
        "Assign one SphereCollider or BoxCollider on this same actor root. " +
        "It is an authoring/gizmo source and is disabled during play; swept " +
        "queries use its exact center and dimensions."
    )]
    [SerializeField] private Collider hitboxSource;

    [SerializeField] private bool drawHitboxGizmo = true;
    [SerializeField] private bool drawHitboxOnlyWhenSelected = true;
    [SerializeField]
    private Color hitboxGizmoColor =
        new Color(1f, 0.25f, 0.1f, 0.9f);

    [Header("Runtime - Read Only")]
    [SerializeField] private bool rentedFromPool;
    [SerializeField] private bool preparedLaunch;
    [SerializeField] private bool activeFlight;
    [SerializeField] private ItemDefinition activeItem;
    [SerializeField] private uint activeShotVersion;
    [SerializeField] private int activePatternEntryIndex = -1;
    [SerializeField] private float elapsedSeconds;
    [SerializeField] private float normalizedFlightTime;
    [SerializeField] private float runtimeApexNormalizedTime;
    [SerializeField] private PowerupProjectileSweepShape runtimeSweepShape;
    [SerializeField] private float runtimeSphereRadius;
    [SerializeField] private Vector3 runtimeBoxHalfExtents;
    [SerializeField] private bool rolling;
    [SerializeField] private float rolledDistance;

    private Vector3 rollDirection;

    private readonly RaycastHit[] hitBuffer =
        new RaycastHit[HitBufferCapacity];

    private PowerupProjectilePool owningPool;
    private ResolvedPowerupProjectileLaunch launch;

    private Quaternion authoredVisualLocalRotation;
    private Vector3 authoredVisualLocalScale;
    private Quaternion runtimeVisualBaseRotation;
    private bool visualDefaultsCaptured;

    private Vector3 authoredHitboxCenter;
    private float authoredSphereRadius;
    private Vector3 authoredBoxSize;
    private bool hitboxDefaultsCaptured;
    private bool runtimeHitboxValid;
    private Vector3 runtimeHitboxCenterOffsetWorld;
    private Quaternion runtimeHitboxRotation;

    public bool IsRentedFromPool => rentedFromPool;
    public bool ActiveFlight => activeFlight;
    public ItemDefinition ActiveItem => activeItem;
    public uint ActiveShotVersion => activeShotVersion;
    public bool HasValidAuthoredHitbox => ValidateAuthoredHitbox();

    /// <summary>In flight or rolling, with a roll after landing.</summary>
    public bool ActiveRollingItem => activeFlight && launch.RollSpeed > 0f;

    private void Awake()
    {
        CaptureVisualDefaults();
        CaptureHitboxDefaults();
        DisablePhysicalHitbox();
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;

        visualDefaultsCaptured = false;
        hitboxDefaultsCaptured = false;

        if (TryResolveHitboxSource())
        {
            // This collider exists for clear prefab authoring. Runtime sweep
            // queries remain authoritative and the collider is disabled in play.
            hitboxSource.isTrigger = true;
        }
    }

    private void Update()
    {
        if (!activeFlight) return;

        if (rolling)
        {
            UpdateRoll();
            return;
        }

        float duration = Mathf.Max(0.0001f, launch.FlightDuration);
        float nextElapsed = Mathf.Min(
            duration,
            elapsedSeconds + Time.deltaTime
        );

        float nextNormalizedTime = Mathf.Clamp01(nextElapsed / duration);
        Vector3 previousPosition = transform.position;
        Vector3 nextPosition = PowerupTrajectory.Evaluate(
            launch.StartPosition,
            launch.LandingPosition,
            launch.ArcHeight,
            nextNormalizedTime
        );

        if (TrySweepSegment(
            previousPosition,
            nextPosition,
            normalizedFlightTime,
            nextNormalizedTime,
            out RaycastHit acceptedHit,
            out PowerupCartTargetSnapshot hitCart,
            out PowerupProjectileCompletionReason completionReason,
            out Vector3 projectileCenterAtHit
        ))
        {
            elapsedSeconds = nextElapsed;
            normalizedFlightTime = nextNormalizedTime;
            transform.position = projectileCenterAtHit;
            UpdateVisualSpin();

            Complete(
                completionReason,
                acceptedHit.point,
                acceptedHit.normal,
                hitCart,
                acceptedHit.collider
            );
            return;
        }

        elapsedSeconds = nextElapsed;
        normalizedFlightTime = nextNormalizedTime;
        transform.position = nextPosition;
        UpdateVisualSpin();

        if (nextNormalizedTime >= 1f)
        {
            if (launch.RollSpeed > 0f)
            {
                BeginRoll();
                return;
            }

            Complete(
                PowerupProjectileCompletionReason.Landed,
                launch.LandingPosition,
                launch.LandingNormal,
                default,
                null
            );
        }
    }

    internal void InitializeForPool(PowerupProjectilePool pool)
    {
        owningPool = pool;
        CaptureVisualDefaults();
        CaptureHitboxDefaults();
        ResetForPool();
    }

    internal void MarkRented()
    {
        rentedFromPool = true;
    }

    /// <summary>
    /// Configures the complete actor while it remains inactive. The launcher
    /// prepares every actor before inventory is consumed, preserving the
    /// all-or-nothing launch transaction.
    /// </summary>
    internal bool TryPrepareLaunch(
        ResolvedPowerupProjectileLaunch resolvedLaunch)
    {
        if (!rentedFromPool) return false;

        CaptureVisualDefaults();

        if (!CaptureHitboxDefaults()) return false;

        launch = resolvedLaunch;

        // A rolling projectile ends its arc at roll height, so it does not
        // pop up when the roll starts.
        if (launch.RollSpeed > 0f)
        {
            launch.LandingPosition += Vector3.up * launch.RollHeight;
        }

        rolling = false;
        rolledDistance = 0f;
        activeItem = launch.Item;
        activeShotVersion = launch.ShotVersion;
        activePatternEntryIndex = launch.PatternEntryIndex;
        elapsedSeconds = 0f;
        normalizedFlightTime = 0f;
        runtimeApexNormalizedTime =
            PowerupTrajectory.CalculateApexNormalizedTime(
                launch.StartPosition,
                launch.LandingPosition,
                launch.ArcHeight
            );
        activeFlight = false;
        preparedLaunch = false;

        transform.SetPositionAndRotation(
            launch.StartPosition,
            launch.CollisionRotation
        );

        ApplyPresentationScale(launch.VisualScaleMultiplier);

        if (!ConfigureRuntimeHitbox(launch.VisualScaleMultiplier))
        {
            RestoreAuthoredPresentation();
            return false;
        }

        preparedLaunch = true;
        return true;
    }

    internal bool BeginPreparedLaunch()
    {
        if (!rentedFromPool || !preparedLaunch || !runtimeHitboxValid)
        {
            return false;
        }

        preparedLaunch = false;
        activeFlight = true;
        gameObject.SetActive(true);
        return true;
    }

    public void Cancel()
    {
        if (!rentedFromPool) return;

        if (activeFlight)
        {
            Complete(
                PowerupProjectileCompletionReason.Cancelled,
                transform.position,
                Vector3.up,
                default,
                null
            );
        }
        else
        {
            owningPool?.ReturnUnlaunched(this);
        }
    }

    internal void ResetForPool()
    {
        activeFlight = false;
        preparedLaunch = false;
        rentedFromPool = false;
        rolling = false;
        rolledDistance = 0f;
        rollDirection = Vector3.zero;
        activeItem = null;
        activeShotVersion = 0u;
        activePatternEntryIndex = -1;
        elapsedSeconds = 0f;
        normalizedFlightTime = 0f;
        runtimeApexNormalizedTime = 0f;
        runtimeHitboxValid = false;
        runtimeSphereRadius = 0f;
        runtimeBoxHalfExtents = Vector3.zero;
        runtimeHitboxCenterOffsetWorld = Vector3.zero;
        runtimeHitboxRotation = Quaternion.identity;
        launch = default;

        RestoreAuthoredPresentation();
        RestoreAuthoredHitbox();
        DisablePhysicalHitbox();
        gameObject.SetActive(false);
    }

    public bool TryGetAuthoredHitboxPlanarRadius(
        out float planarRadius)
    {
        planarRadius = 0f;

        if (!ValidateAuthoredHitbox()) return false;

        Vector3 absoluteScale = Abs(transform.lossyScale);

        if (hitboxSource is SphereCollider sphere)
        {
            Vector3 scaledCenter = Vector3.Scale(
                sphere.center,
                absoluteScale
            );

            float radius = sphere.radius * MaxComponent(absoluteScale);
            planarRadius =
                new Vector2(scaledCenter.x, scaledCenter.z).magnitude +
                radius;
            return true;
        }

        if (hitboxSource is BoxCollider box)
        {
            Vector3 scaledCenter = Vector3.Scale(
                box.center,
                absoluteScale
            );

            Vector3 halfExtents = Vector3.Scale(
                box.size * 0.5f,
                absoluteScale
            );

            planarRadius =
                new Vector2(scaledCenter.x, scaledCenter.z).magnitude +
                new Vector2(halfExtents.x, halfExtents.z).magnitude;
            return true;
        }

        return false;
    }

    private bool TrySweepSegment(
        Vector3 start,
        Vector3 end,
        float segmentStartNormalizedTime,
        float segmentEndNormalizedTime,
        out RaycastHit acceptedHit,
        out PowerupCartTargetSnapshot hitCart,
        out PowerupProjectileCompletionReason completionReason,
        out Vector3 projectileCenterAtHit)
    {
        acceptedHit = default;
        hitCart = default;
        completionReason = default;
        projectileCenterAtHit = end;

        if (!runtimeHitboxValid) return false;

        float nearestHitNormalizedTime = float.PositiveInfinity;
        bool foundAcceptedHit = false;
        PowerupCartTargetSnapshot nearestHitCart = default;
        Vector3 nearestProjectileCenter = end;

        // Cart targets remain hittable for the complete flight, including the
        // ascent. Only environmental blockers are gated by the arc apex.
        if (launch.CartTargetMask.value != 0 &&
            TrySweepHitbox(
                start,
                end,
                launch.CartTargetMask,
                out int cartHitCount,
                out float cartSweepDistance
            ))
        {
            for (int i = 0; i < cartHitCount; i++)
            {
                RaycastHit candidateHit = hitBuffer[i];
                Collider candidateCollider = candidateHit.collider;
                if (candidateCollider == null) continue;

                PowerupCartTargetSnapshot candidateHitCart;

                if (!PowerupCartTarget.TryResolveFromCollider(
                        candidateCollider,
                        out PowerupCartTarget candidateTarget) ||
                    !candidateTarget.TryGetGameplayTarget(
                        out candidateHitCart
                    ))
                {
                    continue;
                }

                bool targetTypeAllowed =
                    (candidateHitCart.Kind ==
                        PowerupCartTargetKind.LeadingCart &&
                     launch.HasEffectOnLeadingCart) ||
                    (candidateHitCart.Kind ==
                        PowerupCartTargetKind.ChainedCart &&
                     launch.HasEffectOnChainedCarts);

                if (!targetTypeAllowed) continue;

                int targetPlayerIndex = candidateHitCart.PlayerIndex;

                if (targetPlayerIndex < 1 ||
                    targetPlayerIndex > PowerupRuntimeSystem.MaxPlayerSlots)
                {
                    continue;
                }

                float segmentProgress = Mathf.Clamp01(
                    candidateHit.distance / cartSweepDistance
                );

                float candidateHitTime = Mathf.Lerp(
                    segmentStartNormalizedTime,
                    segmentEndNormalizedTime,
                    segmentProgress
                );

                // An item that hits its thrower may land on the thrower's
                // leading cart, but only on the way down.
                if (launch.IgnoreOwnerInFlight &&
                    targetPlayerIndex == launch.OwnerPlayerIndex &&
                    !(launch.CanHitOwner &&
                      candidateHitCart.Kind == PowerupCartTargetKind.LeadingCart &&
                      candidateHitTime >= runtimeApexNormalizedTime))
                {
                    continue;
                }

                if (launch.IgnoreCheckoutTargets &&
                    candidateHitCart.IsInCheckout)
                {
                    continue;
                }

                PowerupProjectileCompletionReason candidateReason =
                    candidateHitCart.Kind ==
                    PowerupCartTargetKind.LeadingCart
                    ? PowerupProjectileCompletionReason.LeadingCartHit
                    : PowerupProjectileCompletionReason.ChainedCartHit;

                if (candidateHitTime >= nearestHitNormalizedTime) continue;

                nearestHitNormalizedTime = candidateHitTime;
                acceptedHit = candidateHit;
                nearestHitCart = candidateHitCart;
                completionReason = candidateReason;
                nearestProjectileCenter = Vector3.Lerp(
                    start,
                    end,
                    segmentProgress
                );
                foundAcceptedHit = true;
            }
        }

        // If this frame crosses the apex, begin the environment sweep at the
        // exact apex rather than skipping the entire frame. This prevents a
        // thin post-peak obstacle from being tunneled through while still
        // guaranteeing that ascent-side walls never block the projectile.
        if (launch.EnvironmentBlockingMask.value != 0 &&
            segmentEndNormalizedTime > runtimeApexNormalizedTime)
        {
            float environmentStartTime = Mathf.Max(
                segmentStartNormalizedTime,
                runtimeApexNormalizedTime
            );

            Vector3 environmentStart =
                environmentStartTime <= segmentStartNormalizedTime
                    ? start
                    : PowerupTrajectory.Evaluate(
                        launch.StartPosition,
                        launch.LandingPosition,
                        launch.ArcHeight,
                        environmentStartTime
                    );

            if (TrySweepHitbox(
                environmentStart,
                end,
                launch.EnvironmentBlockingMask,
                out int environmentHitCount,
                out float environmentSweepDistance
            ))
            {
                for (int i = 0; i < environmentHitCount; i++)
                {
                    RaycastHit candidateHit = hitBuffer[i];
                    Collider candidateCollider = candidateHit.collider;

                    if (candidateCollider == null ||
                        candidateCollider.isTrigger)
                    {
                        continue;
                    }

                    float segmentProgress = Mathf.Clamp01(
                        candidateHit.distance / environmentSweepDistance
                    );

                    float candidateHitTime = Mathf.Lerp(
                        environmentStartTime,
                        segmentEndNormalizedTime,
                        segmentProgress
                    );

                    if (candidateHitTime >= nearestHitNormalizedTime) continue;

                    nearestHitNormalizedTime = candidateHitTime;
                    acceptedHit = candidateHit;
                    nearestHitCart = default;
                    completionReason =
                        PowerupProjectileCompletionReason.EnvironmentBlocked;
                    nearestProjectileCenter = Vector3.Lerp(
                        environmentStart,
                        end,
                        segmentProgress
                    );
                    foundAcceptedHit = true;
                }
            }
        }

        if (!foundAcceptedHit) return false;

        projectileCenterAtHit = nearestProjectileCenter;
        hitCart = nearestHitCart;
        return true;
    }

    #region Roll

    // After landing, a rolling projectile moves flat along the launcher's roll
    // direction and completes on the first cart or blocking collider, or after
    // its roll distance (infinite when the item rolls until it hits).
    private void BeginRoll()
    {
        rollDirection = launch.RollDirection;
        rollDirection.y = 0f;

        rollDirection = rollDirection.sqrMagnitude > 0.000001f
            ? rollDirection.normalized
            : Vector3.forward;

        rolledDistance = 0f;
        rolling = true;
    }

    private void UpdateRoll()
    {
        float remaining = Mathf.Max(0f, launch.RollMaxDistance - rolledDistance);
        float step = Mathf.Min(remaining, launch.RollSpeed * Time.deltaTime);

        Vector3 previousPosition = transform.position;
        Vector3 nextPosition = previousPosition + rollDirection * step;

        elapsedSeconds += Time.deltaTime;

        if (TrySweepRoll(
                previousPosition,
                nextPosition,
                out RaycastHit acceptedHit,
                out PowerupCartTargetSnapshot hitCart,
                out PowerupProjectileCompletionReason completionReason,
                out Vector3 projectileCenterAtHit))
        {
            transform.position = projectileCenterAtHit;
            UpdateVisualSpin();

            // A hit that starts inside a collider reports no point.
            Vector3 hitPoint = acceptedHit.distance > 0f
                ? acceptedHit.point
                : projectileCenterAtHit;

            Complete(
                completionReason,
                hitPoint,
                acceptedHit.normal,
                hitCart,
                acceptedHit.collider
            );
            return;
        }

        transform.position = nextPosition;
        rolledDistance += step;
        UpdateVisualSpin();

        if (rolledDistance >= launch.RollMaxDistance - 0.0001f)
        {
            Complete(
                PowerupProjectileCompletionReason.Landed,
                nextPosition - Vector3.up * launch.RollHeight,
                launch.LandingNormal,
                default,
                null
            );
        }
    }

    // Any cart or obstacle stops the roll, except the thrower's own leading
    // cart when the item cannot hit its thrower. Only a valid leading cart
    // gets the item's effect.
    private bool TrySweepRoll(
        Vector3 start,
        Vector3 end,
        out RaycastHit acceptedHit,
        out PowerupCartTargetSnapshot hitCart,
        out PowerupProjectileCompletionReason completionReason,
        out Vector3 projectileCenterAtHit)
    {
        acceptedHit = default;
        hitCart = default;
        completionReason = default;
        projectileCenterAtHit = end;

        if (!runtimeHitboxValid) return false;

        float nearestDistance = float.PositiveInfinity;
        float nearestSweepDistance = 1f;
        bool found = false;

        if (TrySweepRollShape(
                start,
                end,
                launch.RollCartMask,
                out int cartHitCount,
                out float cartSweepDistance))
        {
            for (int i = 0; i < cartHitCount; i++)
            {
                RaycastHit candidateHit = hitBuffer[i];
                Collider candidateCollider = candidateHit.collider;
                if (candidateCollider == null) continue;
                if (candidateHit.distance >= nearestDistance) continue;

                PowerupCartTargetSnapshot snapshot = default;
                bool resolved =
                    PowerupCartTarget.TryResolveFromCollider(
                        candidateCollider,
                        out PowerupCartTarget candidateTarget);

                if (resolved)
                {
                    // The snapshot is filled even for loose carts, which are
                    // not gameplay targets.
                    candidateTarget.TryGetGameplayTarget(out snapshot);
                }
                else if (candidateCollider.isTrigger)
                {
                    continue;
                }

                PowerupProjectileCompletionReason candidateReason;
                PowerupCartTargetSnapshot candidateCart = default;

                if (snapshot.Kind == PowerupCartTargetKind.LeadingCart)
                {
                    bool isOwner =
                        snapshot.PlayerIndex == launch.OwnerPlayerIndex;

                    if (isOwner &&
                        launch.IgnoreOwnerInFlight &&
                        !launch.CanHitOwner)
                    {
                        continue;
                    }

                    bool validTarget =
                        launch.HasEffectOnLeadingCart &&
                        snapshot.PlayerIndex >= 1 &&
                        snapshot.PlayerIndex <=
                            PowerupRuntimeSystem.MaxPlayerSlots &&
                        !(launch.IgnoreCheckoutTargets && snapshot.IsInCheckout);

                    if (validTarget)
                    {
                        candidateReason =
                            PowerupProjectileCompletionReason.LeadingCartHit;
                        candidateCart = snapshot;
                    }
                    else
                    {
                        candidateReason =
                            PowerupProjectileCompletionReason.EnvironmentBlocked;
                    }
                }
                else if (snapshot.Kind == PowerupCartTargetKind.ChainedCart)
                {
                    candidateReason =
                        PowerupProjectileCompletionReason.ChainedCartHit;
                    candidateCart = snapshot;
                }
                else
                {
                    candidateReason =
                        PowerupProjectileCompletionReason.EnvironmentBlocked;
                }

                nearestDistance = candidateHit.distance;
                nearestSweepDistance = cartSweepDistance;
                acceptedHit = candidateHit;
                hitCart = candidateCart;
                completionReason = candidateReason;
                found = true;
            }
        }

        if (TrySweepRollShape(
                start,
                end,
                launch.RollBlockingMask,
                out int environmentHitCount,
                out float environmentSweepDistance))
        {
            for (int i = 0; i < environmentHitCount; i++)
            {
                RaycastHit candidateHit = hitBuffer[i];
                Collider candidateCollider = candidateHit.collider;

                if (candidateCollider == null ||
                    candidateCollider.isTrigger ||
                    candidateHit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = candidateHit.distance;
                nearestSweepDistance = environmentSweepDistance;
                acceptedHit = candidateHit;
                hitCart = default;
                completionReason =
                    PowerupProjectileCompletionReason.EnvironmentBlocked;
                found = true;
            }
        }

        if (!found) return false;

        projectileCenterAtHit = Vector3.Lerp(
            start,
            end,
            Mathf.Clamp01(nearestDistance / nearestSweepDistance)
        );
        return true;
    }

    // The roll's own hit sphere, centred on the pivot; 0 falls back to the
    // authored hitbox.
    private bool TrySweepRollShape(
        Vector3 rootStart,
        Vector3 rootEnd,
        LayerMask collisionMask,
        out int hitCount,
        out float sweepDistance)
    {
        if (launch.RollHitRadius <= 0f)
        {
            return TrySweepHitbox(
                rootStart,
                rootEnd,
                collisionMask,
                out hitCount,
                out sweepDistance);
        }

        hitCount = 0;
        Vector3 segment = rootEnd - rootStart;
        sweepDistance = segment.magnitude;

        if (collisionMask.value == 0 || sweepDistance <= 0.000001f)
        {
            return false;
        }

        hitCount = Physics.SphereCastNonAlloc(
            rootStart,
            launch.RollHitRadius,
            segment / sweepDistance,
            hitBuffer,
            sweepDistance,
            collisionMask,
            QueryTriggerInteraction.Collide
        );

        return hitCount > 0;
    }

    #endregion

    private bool TrySweepHitbox(
        Vector3 rootStart,
        Vector3 rootEnd,
        LayerMask collisionMask,
        out int hitCount,
        out float sweepDistance)
    {
        hitCount = 0;
        sweepDistance = 0f;

        if (collisionMask.value == 0) return false;

        Vector3 hitboxStart = rootStart + runtimeHitboxCenterOffsetWorld;
        Vector3 hitboxEnd = rootEnd + runtimeHitboxCenterOffsetWorld;
        Vector3 segment = hitboxEnd - hitboxStart;
        sweepDistance = segment.magnitude;

        if (sweepDistance <= 0.000001f) return false;

        Vector3 direction = segment / sweepDistance;

        if (runtimeSweepShape == PowerupProjectileSweepShape.Box)
        {
            hitCount = Physics.BoxCastNonAlloc(
                hitboxStart,
                runtimeBoxHalfExtents,
                direction,
                hitBuffer,
                runtimeHitboxRotation,
                sweepDistance,
                collisionMask,
                QueryTriggerInteraction.Collide
            );
        }
        else
        {
            hitCount = Physics.SphereCastNonAlloc(
                hitboxStart,
                Mathf.Max(0.0001f, runtimeSphereRadius),
                direction,
                hitBuffer,
                sweepDistance,
                collisionMask,
                QueryTriggerInteraction.Collide
            );
        }

        return hitCount > 0;
    }

    private bool ConfigureRuntimeHitbox(float visualScaleMultiplier)
    {
        if (!CaptureHitboxDefaults()) return false;

        RestoreAuthoredHitbox();

        float scaleMultiplier = Mathf.Max(0.01f, visualScaleMultiplier);

        // When the visual is the actor root, its transform scale already
        // includes the entry multiplier. The recommended setup uses a child.
        float componentScale = visualRoot == transform
            ? 1f
            : scaleMultiplier;

        if (hitboxSource is SphereCollider sphere)
        {
            sphere.center = authoredHitboxCenter * componentScale;
            sphere.radius = authoredSphereRadius * componentScale;

            Vector3 absoluteScale = Abs(transform.lossyScale);
            runtimeSweepShape = PowerupProjectileSweepShape.Sphere;
            runtimeSphereRadius =
                sphere.radius * MaxComponent(absoluteScale);
            runtimeBoxHalfExtents = Vector3.zero;
            runtimeHitboxCenterOffsetWorld =
                transform.TransformVector(sphere.center);
        }
        else if (hitboxSource is BoxCollider box)
        {
            box.center = authoredHitboxCenter * componentScale;
            box.size = authoredBoxSize * componentScale;

            runtimeSweepShape = PowerupProjectileSweepShape.Box;
            runtimeSphereRadius = 0f;
            runtimeBoxHalfExtents = Vector3.Scale(
                box.size * 0.5f,
                Abs(transform.lossyScale)
            );
            runtimeHitboxCenterOffsetWorld =
                transform.TransformVector(box.center);
        }
        else
        {
            return false;
        }

        runtimeHitboxRotation = transform.rotation;
        runtimeHitboxValid = true;
        DisablePhysicalHitbox();
        return true;
    }

    private bool CaptureHitboxDefaults()
    {
        if (hitboxDefaultsCaptured) return true;
        if (!ValidateAuthoredHitbox()) return false;

        // Keep the authored component harmless even before the pooled actor is
        // disabled. Runtime queries never depend on trigger callbacks.
        hitboxSource.isTrigger = true;

        if (hitboxSource is SphereCollider sphere)
        {
            authoredHitboxCenter = sphere.center;
            authoredSphereRadius = sphere.radius;
            authoredBoxSize = Vector3.zero;
        }
        else if (hitboxSource is BoxCollider box)
        {
            authoredHitboxCenter = box.center;
            authoredSphereRadius = 0f;
            authoredBoxSize = box.size;
        }
        else
        {
            return false;
        }

        hitboxDefaultsCaptured = true;
        return true;
    }

    private bool TryResolveHitboxSource()
    {
        if (hitboxSource == null)
        {
            hitboxSource = GetComponent<SphereCollider>();

            if (hitboxSource == null)
            {
                hitboxSource = GetComponent<BoxCollider>();
            }
        }

        return hitboxSource != null &&
               hitboxSource.gameObject == gameObject &&
               (hitboxSource is SphereCollider ||
                hitboxSource is BoxCollider);
    }

    private bool ValidateAuthoredHitbox()
    {
        if (!TryResolveHitboxSource()) return false;

        if (hitboxSource is SphereCollider sphere)
        {
            return sphere.radius > 0.0001f;
        }

        if (hitboxSource is BoxCollider box)
        {
            Vector3 size = box.size;
            return size.x > 0.0001f &&
                   size.y > 0.0001f &&
                   size.z > 0.0001f;
        }

        return false;
    }

    private void RestoreAuthoredHitbox()
    {
        if (!hitboxDefaultsCaptured || hitboxSource == null) return;

        if (hitboxSource is SphereCollider sphere)
        {
            sphere.center = authoredHitboxCenter;
            sphere.radius = authoredSphereRadius;
        }
        else if (hitboxSource is BoxCollider box)
        {
            box.center = authoredHitboxCenter;
            box.size = authoredBoxSize;
        }
    }

    private void DisablePhysicalHitbox()
    {
        if (hitboxSource != null && Application.isPlaying)
        {
            hitboxSource.enabled = false;
        }
    }

    private void ApplyPresentationScale(float visualScaleMultiplier)
    {
        if (visualRoot == null) return;

        if (visualRoot != transform)
        {
            visualRoot.localRotation = authoredVisualLocalRotation;
        }

        visualRoot.localScale = Vector3.Scale(
            authoredVisualLocalScale,
            Vector3.one * Mathf.Max(0.01f, visualScaleMultiplier)
        );

        runtimeVisualBaseRotation = visualRoot.localRotation;
    }

    private void RestoreAuthoredPresentation()
    {
        if (visualRoot == null || !visualDefaultsCaptured) return;

        visualRoot.localRotation = authoredVisualLocalRotation;
        visualRoot.localScale = authoredVisualLocalScale;
    }

    private void UpdateVisualSpin()
    {
        // Spinning the actor root would also spin the deterministic BoxCast.
        // Use a visual child when spin is desired.
        if (visualRoot == null || visualRoot == transform) return;

        Vector3 spinAxis = launch.VisualSpinAxis.sqrMagnitude > 0.000001f
            ? launch.VisualSpinAxis.normalized
            : Vector3.up;

        visualRoot.localRotation =
            runtimeVisualBaseRotation *
            Quaternion.AngleAxis(
                elapsedSeconds * launch.VisualSpinDegreesPerSecond,
                spinAxis
            );
    }

    private void Complete(
        PowerupProjectileCompletionReason reason,
        Vector3 position,
        Vector3 surfaceNormal,
        PowerupCartTargetSnapshot hitCart,
        Collider hitCollider)
    {
        if (!activeFlight) return;

        activeFlight = false;
        rolling = false;

        PowerupProjectileCompletion completion =
            new PowerupProjectileCompletion
            {
                ShotVersion = launch.ShotVersion,
                PatternEntryIndex = launch.PatternEntryIndex,
                Item = launch.Item,
                OwnerPlayerIndex = launch.OwnerPlayerIndex,
                Reason = reason,
                Position = position,
                SurfaceNormal = surfaceNormal.sqrMagnitude > 0.000001f
                    ? surfaceNormal.normalized
                    : Vector3.up,
                HitTarget = hitCart.Target,
                HitCartKind = hitCart.Kind,
                HitPlayerIndex = hitCart.PlayerIndex,
                HitCollider = hitCollider,
                HitOwnerController = hitCart.OwnerController,
                HitLeadingCartControl = hitCart.LeadingCartControl,
                HitChainedCartManager = hitCart.ChainedCartManager
            };

        if (owningPool != null)
        {
            owningPool.ReturnCompleted(this, completion);
        }
        else
        {
            ResetForPool();
        }
    }

    private void CaptureVisualDefaults()
    {
        if (visualDefaultsCaptured) return;

        if (visualRoot == null) visualRoot = transform;

        authoredVisualLocalRotation = visualRoot.localRotation;
        authoredVisualLocalScale = visualRoot.localScale;
        runtimeVisualBaseRotation = authoredVisualLocalRotation;
        visualDefaultsCaptured = true;
    }

    private void OnDrawGizmos()
    {
        if (!drawHitboxGizmo || drawHitboxOnlyWhenSelected) return;
        DrawAuthoredHitboxGizmo();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawHitboxGizmo || !drawHitboxOnlyWhenSelected) return;
        DrawAuthoredHitboxGizmo();
    }

    private void DrawAuthoredHitboxGizmo()
    {
        if (!TryResolveHitboxSource()) return;

        Color previousColor = Gizmos.color;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.color = hitboxGizmoColor;

        if (hitboxSource is SphereCollider sphere)
        {
            Vector3 worldCenter = transform.TransformPoint(sphere.center);
            float worldRadius =
                sphere.radius * MaxComponent(Abs(transform.lossyScale));
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawWireSphere(worldCenter, worldRadius);
        }
        else if (hitboxSource is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(
            Mathf.Abs(value.x),
            Mathf.Abs(value.y),
            Mathf.Abs(value.z)
        );
    }

    private static float MaxComponent(Vector3 value)
    {
        return Mathf.Max(value.x, Mathf.Max(value.y, value.z));
    }

}
