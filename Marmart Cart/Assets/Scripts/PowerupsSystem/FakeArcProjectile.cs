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
    [SerializeField] private PowerupId activePowerupId;
    [SerializeField] private uint activeShotVersion;
    [SerializeField] private int activePatternEntryIndex = -1;
    [SerializeField] private float elapsedSeconds;
    [SerializeField] private float normalizedFlightTime;
    [SerializeField] private PowerupProjectileSweepShape runtimeSweepShape;
    [SerializeField] private float runtimeSphereRadius;
    [SerializeField] private Vector3 runtimeBoxHalfExtents;

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
    public PowerupId ActivePowerupId => activePowerupId;
    public uint ActiveShotVersion => activeShotVersion;
    public bool HasValidAuthoredHitbox => ValidateAuthoredHitbox();

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
            nextNormalizedTime,
            launch.TrajectoryTiming
        );

        if (TrySweepSegment(
            previousPosition,
            nextPosition,
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
        activePowerupId = launch.PowerupId;
        activeShotVersion = launch.ShotVersion;
        activePatternEntryIndex = launch.PatternEntryIndex;
        elapsedSeconds = 0f;
        normalizedFlightTime = 0f;
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
        activePowerupId = default;
        activeShotVersion = 0u;
        activePatternEntryIndex = -1;
        elapsedSeconds = 0f;
        normalizedFlightTime = 0f;
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

        Vector3 hitboxStart = start + runtimeHitboxCenterOffsetWorld;
        Vector3 hitboxEnd = end + runtimeHitboxCenterOffsetWorld;
        Vector3 segment = hitboxEnd - hitboxStart;
        float distance = segment.magnitude;

        if (distance <= 0.000001f) return false;

        int combinedMask =
            launch.CartTargetMask.value |
            launch.EnvironmentBlockingMask.value;

        if (combinedMask == 0) return false;

        Vector3 direction = segment / distance;
        int hitCount;

        if (runtimeSweepShape == PowerupProjectileSweepShape.Box)
        {
            hitCount = Physics.BoxCastNonAlloc(
                hitboxStart,
                runtimeBoxHalfExtents,
                direction,
                hitBuffer,
                runtimeHitboxRotation,
                distance,
                combinedMask,
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
                distance,
                combinedMask,
                QueryTriggerInteraction.Collide
            );
        }

        float nearestDistance = float.PositiveInfinity;
        bool foundAcceptedHit = false;
        PowerupCartTargetSnapshot nearestHitCart = default;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit candidateHit = hitBuffer[i];
            Collider candidateCollider = candidateHit.collider;
            if (candidateCollider == null) continue;

            PowerupProjectileCompletionReason candidateReason;
            PowerupCartTargetSnapshot candidateHitCart = default;

            if (LayerIsInMask(
                candidateCollider.gameObject.layer,
                launch.CartTargetMask
            ))
            {
                PowerupCartTarget candidateTarget =
                    candidateCollider.GetComponentInParent<PowerupCartTarget>();

                if (candidateTarget == null ||
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

                if (launch.IgnoreOwnerInFlight &&
                    targetPlayerIndex == launch.OwnerPlayerIndex)
                {
                    continue;
                }

                if (launch.IgnoreCheckoutTargets &&
                    candidateHitCart.IsInCheckout)
                {
                    continue;
                }

                candidateReason = candidateHitCart.Kind ==
                    PowerupCartTargetKind.LeadingCart
                    ? PowerupProjectileCompletionReason.LeadingCartHit
                    : PowerupProjectileCompletionReason.ChainedCartHit;
            }
            else if (LayerIsInMask(
                candidateCollider.gameObject.layer,
                launch.EnvironmentBlockingMask
            ) && !candidateCollider.isTrigger)
            {
                candidateReason =
                    PowerupProjectileCompletionReason.EnvironmentBlocked;
            }
            else
            {
                continue;
            }

            if (candidateHit.distance >= nearestDistance) continue;

            nearestDistance = candidateHit.distance;
            acceptedHit = candidateHit;
            nearestHitCart = candidateHitCart;
            completionReason = candidateReason;
            foundAcceptedHit = true;
        }

        if (!foundAcceptedHit) return false;

        Vector3 rootSegment = end - start;
        Vector3 rootDirection = rootSegment.sqrMagnitude > 0.000001f
            ? rootSegment.normalized
            : direction;

        projectileCenterAtHit =
            start +
            rootDirection * Mathf.Clamp(nearestDistance, 0f, distance);
        hitCart = nearestHitCart;
        return true;
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

        PowerupProjectileCompletion completion =
            new PowerupProjectileCompletion
            {
                ShotVersion = launch.ShotVersion,
                PatternEntryIndex = launch.PatternEntryIndex,
                PowerupId = launch.PowerupId,
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

    private static bool LayerIsInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}
