using UnityEngine;

/// <summary>
/// Pooled, transform-driven projectile actor. It evaluates the exact same
/// PowerupTrajectory function as the aim preview and performs swept non-alloc
/// hit queries between frames. It never owns a Rigidbody.
/// </summary>
[DisallowMultipleComponent]
public class FakeArcProjectile : MonoBehaviour
{
    private const int HitBufferCapacity = 16;

    [Header("Swappable Presentation")]
    [Tooltip("Assign the mesh/model child that may spin and scale. The actor root remains the deterministic path owner.")]
    [SerializeField] private Transform visualRoot;

    [Header("Runtime - Read Only")]
    [SerializeField] private bool rentedFromPool;
    [SerializeField] private bool activeFlight;
    [SerializeField] private PowerupId activePowerupId;
    [SerializeField] private uint activeShotVersion;
    [SerializeField] private int activePatternEntryIndex = -1;
    [SerializeField] private float elapsedSeconds;
    [SerializeField] private float normalizedFlightTime;

    private readonly RaycastHit[] hitBuffer =
        new RaycastHit[HitBufferCapacity];

    private PowerupProjectilePool owningPool;
    private ResolvedPowerupProjectileLaunch launch;
    private Quaternion authoredVisualLocalRotation;
    private Vector3 authoredVisualLocalScale;
    private Quaternion runtimeVisualBaseRotation;
    private bool visualDefaultsCaptured;

    public bool IsRentedFromPool => rentedFromPool;
    public bool ActiveFlight => activeFlight;
    public PowerupId ActivePowerupId => activePowerupId;
    public uint ActiveShotVersion => activeShotVersion;

    private void Awake()
    {
        CaptureVisualDefaults();
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
            nextNormalizedTime
        );

        if (TrySweepSegment(
            previousPosition,
            nextPosition,
            out RaycastHit acceptedHit,
            out LeadingCartPowerupTarget hitTarget,
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
                hitTarget
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
                null
            );
        }
    }

    internal void InitializeForPool(PowerupProjectilePool pool)
    {
        owningPool = pool;
        CaptureVisualDefaults();
        ResetForPool();
    }

    internal void MarkRented()
    {
        rentedFromPool = true;
    }

    public void Launch(ResolvedPowerupProjectileLaunch resolvedLaunch)
    {
        CaptureVisualDefaults();

        launch = resolvedLaunch;
        activePowerupId = launch.PowerupId;
        activeShotVersion = launch.ShotVersion;
        activePatternEntryIndex = launch.PatternEntryIndex;
        elapsedSeconds = 0f;
        normalizedFlightTime = 0f;
        activeFlight = true;

        transform.SetPositionAndRotation(
            launch.StartPosition,
            launch.CollisionRotation
        );

        if (visualRoot != null)
        {
            if (visualRoot != transform)
            {
                visualRoot.localRotation = authoredVisualLocalRotation;
            }

            visualRoot.localScale = Vector3.Scale(
                authoredVisualLocalScale,
                Vector3.one * Mathf.Max(0.01f, launch.VisualScaleMultiplier)
            );

            runtimeVisualBaseRotation = visualRoot.localRotation;
        }

        gameObject.SetActive(true);
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
        rentedFromPool = false;
        activePowerupId = default;
        activeShotVersion = 0u;
        activePatternEntryIndex = -1;
        elapsedSeconds = 0f;
        normalizedFlightTime = 0f;
        launch = default;

        if (visualRoot != null && visualDefaultsCaptured)
        {
            visualRoot.localRotation = authoredVisualLocalRotation;
            visualRoot.localScale = authoredVisualLocalScale;
        }

        gameObject.SetActive(false);
    }

    private bool TrySweepSegment(
        Vector3 start,
        Vector3 end,
        out RaycastHit acceptedHit,
        out LeadingCartPowerupTarget hitTarget,
        out PowerupProjectileCompletionReason completionReason,
        out Vector3 projectileCenterAtHit)
    {
        acceptedHit = default;
        hitTarget = null;
        completionReason = default;
        projectileCenterAtHit = end;

        Vector3 segment = end - start;
        float distance = segment.magnitude;

        if (distance <= 0.000001f) return false;

        int combinedMask =
            launch.LeadingCartTargetMask.value |
            launch.EnvironmentBlockingMask.value;

        if (combinedMask == 0) return false;

        Vector3 direction = segment / distance;
        int hitCount;

        if (launch.SweepShape == PowerupProjectileSweepShape.Box)
        {
            hitCount = Physics.BoxCastNonAlloc(
                start,
                launch.BoxHalfExtents,
                direction,
                hitBuffer,
                launch.CollisionRotation,
                distance,
                combinedMask,
                QueryTriggerInteraction.Collide
            );
        }
        else
        {
            hitCount = Physics.SphereCastNonAlloc(
                start,
                Mathf.Max(0.01f, launch.SphereRadius),
                direction,
                hitBuffer,
                distance,
                combinedMask,
                QueryTriggerInteraction.Collide
            );
        }

        float nearestDistance = float.PositiveInfinity;
        bool foundAcceptedHit = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit candidateHit = hitBuffer[i];
            Collider candidateCollider = candidateHit.collider;
            if (candidateCollider == null) continue;

            LeadingCartPowerupTarget candidateTarget =
                candidateCollider.GetComponentInParent<LeadingCartPowerupTarget>();

            PowerupProjectileCompletionReason candidateReason;

            if (candidateTarget != null)
            {
                int targetPlayerIndex = candidateTarget.PlayerIndex;

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
                    candidateTarget.IsInCheckout)
                {
                    continue;
                }

                candidateReason =
                    PowerupProjectileCompletionReason.LeadingCartHit;
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
                // A collider placed on the target mask without the explicit
                // target marker is ignored rather than guessed to be a player.
                continue;
            }

            if (candidateHit.distance >= nearestDistance) continue;

            nearestDistance = candidateHit.distance;
            acceptedHit = candidateHit;
            hitTarget = candidateTarget;
            completionReason = candidateReason;
            foundAcceptedHit = true;
        }

        if (!foundAcceptedHit) return false;

        projectileCenterAtHit =
            start + direction * Mathf.Clamp(nearestDistance, 0f, distance);
        return true;
    }

    private void UpdateVisualSpin()
    {
        if (visualRoot == null) return;

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
        LeadingCartPowerupTarget hitTarget)
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
                HitTarget = hitTarget
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

    private static bool LayerIsInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}
