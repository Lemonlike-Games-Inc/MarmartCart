using System;
using System.Collections.Generic;
using UnityEngine;

public enum PowerupProjectileLaunchFailureReason
{
    MissingReference,
    UnsupportedPowerup,
    InvalidShotPattern,
    PoolUnavailable,
    SnapshotMismatch,
    InventoryChanged,
    PoolRentFailed,
    InventoryConsumeFailed,
    ProjectileProfileMismatch,
    InvalidProjectileHitbox,
    ProjectilePreparationFailed
}

/// <summary>
/// Per-player bridge from an accepted immutable aim snapshot to a complete
/// deterministic projectile volley. It reserves every actor before consuming
/// inventory, so a setup or pool failure always leaves the stored item intact.
/// </summary>
[DisallowMultipleComponent]
public class PowerupProjectileLauncher : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerPowerupController playerPowerupController;
    [SerializeField] private PowerupTargetingController targetingController;

    [Tooltip("Optional explicit scene reference. Automatically resolved when left empty.")]
    [SerializeField] private PowerupProjectilePool projectilePool;

    [Tooltip(
        "Optional explicit reference. Automatically resolved when left empty."
    )]
    [SerializeField] private PowerupLifecycleEventSystem lifecycleEventSystem;

    [Header("Diagnostics")]
    [SerializeField] private bool logSuccessfulLaunches = true;
    [SerializeField] private bool logLaunchFailures = true;
    [SerializeField] private bool warnWhenPatternExceedsPreview = true;

    [Header("Runtime - Read Only")]
    [SerializeField] private uint launchedShotVersion;
    [SerializeField] private PowerupId lastLaunchedPowerup;
    [SerializeField] private int lastLaunchedActorCount;
    [SerializeField] private PowerupProjectileLaunchFailureReason lastFailureReason;

    private readonly List<ResolvedPowerupProjectileLaunch> resolvedLaunches =
        new List<ResolvedPowerupProjectileLaunch>(8);

    private readonly List<FakeArcProjectile> rentedProjectiles =
        new List<FakeArcProjectile>(8);

    private PlayerPowerupController subscribedPowerupController;
    private PowerupTargetingController subscribedTargetingController;
    private bool patternPreviewWarningLogged;

    public uint LaunchedShotVersion => launchedShotVersion;

    public event Action<
        PowerupProjectileLauncher,
        PowerupId,
        uint,
        int> OnShotLaunched;

    public event Action<
        PowerupProjectileLauncher,
        PowerupId,
        PowerupProjectileLaunchFailureReason> OnShotLaunchFailed;

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
        EnsureSubscriptions();
    }

    private void Update()
    {
        if (playerPowerupController == null ||
            targetingController == null ||
            projectilePool == null)
        {
            ResolveReferences();
        }

        EnsureSubscriptions();
    }

    private void OnDisable()
    {
        Unsubscribe();
        RollBackRentedProjectiles();
        resolvedLaunches.Clear();
    }

    private bool ValidateProjectileExecution(
        PlayerPowerupController controller,
        PowerupId requestedPowerup)
    {
        if (!PowerupIdRules.RequiresProjectileAim(requestedPowerup))
        {
            return true;
        }

        if (controller != playerPowerupController ||
            targetingController == null ||
            projectilePool == null)
        {
            RecordFailure(
                requestedPowerup,
                PowerupProjectileLaunchFailureReason.MissingReference
            );
            return false;
        }

        PowerupProjectileProfile projectileProfile =
            projectilePool.ProjectileProfile;

        if (projectileProfile == null ||
            targetingController.ProjectileProfile == null)
        {
            RecordFailure(
                requestedPowerup,
                PowerupProjectileLaunchFailureReason.MissingReference
            );
            return false;
        }

        if (targetingController.ProjectileProfile != projectileProfile)
        {
            RecordFailure(
                requestedPowerup,
                PowerupProjectileLaunchFailureReason.ProjectileProfileMismatch
            );
            return false;
        }

        ProjectileShotPattern pattern =
            projectileProfile.GetPattern(requestedPowerup);

        if (!IsUsablePattern(pattern))
        {
            RecordFailure(
                requestedPowerup,
                PowerupProjectileLaunchFailureReason.InvalidShotPattern
            );
            return false;
        }

        FakeArcProjectile prefabActor =
            projectileProfile.GetPrefab(requestedPowerup);

        if (prefabActor == null || !prefabActor.HasValidAuthoredHitbox)
        {
            RecordFailure(
                requestedPowerup,
                PowerupProjectileLaunchFailureReason.InvalidProjectileHitbox
            );
            return false;
        }

        if (!projectileProfile.HasCartTargetMask ||
            !projectilePool.CanProvide(
                requestedPowerup,
                pattern.EntryCount
            ))
        {
            RecordFailure(
                requestedPowerup,
                PowerupProjectileLaunchFailureReason.PoolUnavailable
            );
            return false;
        }

        WarnIfPatternExceedsAdvertisedPreview(
            requestedPowerup,
            pattern,
            projectileProfile,
            targetingController.GameplayProfile
        );

        return true;
    }

    private void HandleAcceptedTargetedUse(
        PowerupTargetingController source,
        PowerupAimState acceptedSnapshot)
    {
        if (source != targetingController ||
            playerPowerupController == null)
        {
            return;
        }

        TryLaunchVolley(acceptedSnapshot);
    }

    private bool TryLaunchVolley(PowerupAimState snapshot)
    {
        resolvedLaunches.Clear();
        RollBackRentedProjectiles();

        if (!snapshot.Visible ||
            snapshot.PlayerIndex != playerPowerupController.PlayerIndex ||
            !PowerupIdRules.RequiresProjectileAim(snapshot.PowerupId))
        {
            RecordFailure(
                snapshot.PowerupId,
                PowerupProjectileLaunchFailureReason.SnapshotMismatch
            );
            return false;
        }

        if (!playerPowerupController.HasStoredPowerup ||
            playerPowerupController.StoredPowerup != snapshot.PowerupId)
        {
            RecordFailure(
                snapshot.PowerupId,
                PowerupProjectileLaunchFailureReason.InventoryChanged
            );
            return false;
        }

        if (projectilePool == null ||
            projectilePool.ProjectileProfile == null ||
            targetingController == null ||
            targetingController.GameplayProfile == null ||
            targetingController.ProjectileProfile == null)
        {
            RecordFailure(
                snapshot.PowerupId,
                PowerupProjectileLaunchFailureReason.MissingReference
            );
            return false;
        }

        PowerupProjectileProfile projectileProfile =
            projectilePool.ProjectileProfile;
        PowerupGameplayProfile gameplayProfile =
            targetingController.GameplayProfile;

        if (targetingController.ProjectileProfile != projectileProfile)
        {
            RecordFailure(
                snapshot.PowerupId,
                PowerupProjectileLaunchFailureReason.ProjectileProfileMismatch
            );
            return false;
        }

        if (!projectileProfile.HasCartTargetMask)
        {
            RecordFailure(
                snapshot.PowerupId,
                PowerupProjectileLaunchFailureReason.PoolUnavailable
            );
            return false;
        }

        ProjectileShotPattern pattern =
            projectileProfile.GetPattern(snapshot.PowerupId);

        if (!IsUsablePattern(pattern))
        {
            RecordFailure(
                snapshot.PowerupId,
                PowerupProjectileLaunchFailureReason.InvalidShotPattern
            );
            return false;
        }

        FakeArcProjectile prefabActor =
            projectileProfile.GetPrefab(snapshot.PowerupId);

        if (prefabActor == null || !prefabActor.HasValidAuthoredHitbox)
        {
            RecordFailure(
                snapshot.PowerupId,
                PowerupProjectileLaunchFailureReason.InvalidProjectileHitbox
            );
            return false;
        }

        BuildResolvedLaunches(
            snapshot,
            pattern,
            projectileProfile,
            gameplayProfile
        );

        for (int i = 0; i < resolvedLaunches.Count; i++)
        {
            if (!projectilePool.TryRent(
                snapshot.PowerupId,
                out FakeArcProjectile projectile
            ))
            {
                RollBackRentedProjectiles();
                resolvedLaunches.Clear();
                RecordFailure(
                    snapshot.PowerupId,
                    PowerupProjectileLaunchFailureReason.PoolRentFailed
                );
                return false;
            }

            rentedProjectiles.Add(projectile);
        }

        // Prepare every inactive actor before consuming inventory. This also
        // validates that every pooled copy can derive its runtime sweep from
        // the collider authored on the prefab.
        uint nextShotVersion = unchecked(launchedShotVersion + 1u);

        for (int i = 0; i < rentedProjectiles.Count; i++)
        {
            ResolvedPowerupProjectileLaunch launch = resolvedLaunches[i];
            launch.ShotVersion = nextShotVersion;
            resolvedLaunches[i] = launch;

            if (!rentedProjectiles[i].TryPrepareLaunch(launch))
            {
                RollBackRentedProjectiles();
                resolvedLaunches.Clear();
                RecordFailure(
                    snapshot.PowerupId,
                    PowerupProjectileLaunchFailureReason.ProjectilePreparationFailed
                );
                return false;
            }
        }

        // Nothing capable of invalidating the volley remains after this point.
        // Consume only after the complete volley has been resolved, rented,
        // and prepared.
        if (!playerPowerupController.TryConsumeStoredPowerup(
            out PowerupId consumedPowerup
        ) || consumedPowerup != snapshot.PowerupId)
        {
            RollBackRentedProjectiles();
            resolvedLaunches.Clear();
            RecordFailure(
                snapshot.PowerupId,
                PowerupProjectileLaunchFailureReason.InventoryConsumeFailed
            );
            return false;
        }

        launchedShotVersion = nextShotVersion;
        lastLaunchedPowerup = snapshot.PowerupId;
        lastLaunchedActorCount = 0;

        for (int i = 0; i < rentedProjectiles.Count; i++)
        {
            FakeArcProjectile projectile = rentedProjectiles[i];

            if (projectile.BeginPreparedLaunch())
            {
                lastLaunchedActorCount++;
            }
            else
            {
                Debug.LogError(
                    "[PowerupProjectileLauncher] A fully prepared projectile " +
                    "could not begin flight after inventory was consumed. " +
                    "Check for external changes to pooled actors.",
                    projectile
                );

                projectilePool.ReturnUnlaunched(projectile);
            }
        }

        int launchedActorCount = lastLaunchedActorCount;
        rentedProjectiles.Clear();
        resolvedLaunches.Clear();

        if (logSuccessfulLaunches)
        {
            Debug.Log(
                $"[PowerupProjectileLauncher] P{snapshot.PlayerIndex} launched " +
                $"{snapshot.PowerupId} shot #{launchedShotVersion} with " +
                $"{launchedActorCount} deterministic actor(s).",
                this
            );
        }

        ResolveLifecycleEventSystem();

        lifecycleEventSystem?.PublishActivated(
            new PowerupActivationEvent
            {
                PowerupId = snapshot.PowerupId,
                Mode = PowerupActivationMode.ProjectileVolley,
                ActivationVersion = launchedShotVersion,
                OwnerPlayerIndex = snapshot.PlayerIndex,
                OwnerController = playerPowerupController,
                Position = snapshot.StartPosition,
                Direction = snapshot.AimDirection,
                ProjectileCount = launchedActorCount
            }
        );

        OnShotLaunched?.Invoke(
            this,
            snapshot.PowerupId,
            launchedShotVersion,
            launchedActorCount
        );

        return true;
    }

    private void BuildResolvedLaunches(
        PowerupAimState snapshot,
        ProjectileShotPattern pattern,
        PowerupProjectileProfile projectileProfile,
        PowerupGameplayProfile gameplayProfile)
    {
        Vector3 landingNormal = snapshot.LandingNormal.sqrMagnitude > 0.000001f
            ? snapshot.LandingNormal.normalized
            : Vector3.up;

        Vector3 patternForward = Vector3.ProjectOnPlane(
            snapshot.AimDirection,
            landingNormal
        );

        if (patternForward.sqrMagnitude <= 0.000001f)
        {
            patternForward = Vector3.ProjectOnPlane(
                Vector3.forward,
                landingNormal
            );
        }

        if (patternForward.sqrMagnitude <= 0.000001f)
        {
            patternForward = Vector3.ProjectOnPlane(
                Vector3.right,
                landingNormal
            );
        }

        patternForward.Normalize();
        Vector3 patternRight = Vector3.Cross(
            landingNormal,
            patternForward
        ).normalized;

        Vector3 collisionForward = snapshot.AimDirection;
        collisionForward.y = 0f;

        if (collisionForward.sqrMagnitude <= 0.000001f)
        {
            collisionForward = Vector3.forward;
        }

        collisionForward.Normalize();
        Quaternion collisionRotation =
            Quaternion.LookRotation(collisionForward, Vector3.up) *
            Quaternion.Euler(pattern.CastEulerAngles);

        for (int i = 0; i < pattern.EntryCount; i++)
        {
            ProjectilePatternEntry entry = pattern.GetEntry(i);
            Vector2 normalizedOffset = entry.NormalizedLandingOffset;

            Vector3 requestedLanding =
                snapshot.LandingPosition +
                patternRight * normalizedOffset.x * pattern.SpreadRadius +
                patternForward * normalizedOffset.y * pattern.SpreadRadius;

            ResolveEntryGroundLanding(
                requestedLanding,
                snapshot.LandingPosition,
                landingNormal,
                gameplayProfile,
                out Vector3 resolvedLanding,
                out Vector3 resolvedNormal
            );

            resolvedLaunches.Add(
                new ResolvedPowerupProjectileLaunch
                {
                    ShotVersion = 0u,
                    PatternEntryIndex = i,
                    PowerupId = snapshot.PowerupId,
                    OwnerPlayerIndex = snapshot.PlayerIndex,
                    StartPosition = snapshot.StartPosition,
                    LandingPosition = resolvedLanding,
                    LandingNormal = resolvedNormal,
                    FlightDuration = Mathf.Max(
                        projectileProfile.MinimumProjectileDuration,
                        snapshot.FlightDuration +
                        entry.ArrivalTimeOffsetSeconds
                    ),
                    ArcHeight = Mathf.Max(
                        projectileProfile.MinimumProjectileArcHeight,
                        snapshot.ArcHeight + entry.ArcHeightOffset
                    ),
                    CollisionRotation = collisionRotation,
                    CartTargetMask =
                        projectileProfile.CartTargetMask,
                    EnvironmentBlockingMask =
                        gameplayProfile.ProjectileBlockingMask,
                    HasEffectOnLeadingCart =
                        pattern.HasEffectOnLeadingCart,
                    HasEffectOnChainedCarts =
                        pattern.HasEffectOnChainedCarts,
                    IgnoreOwnerInFlight = pattern.IgnoreOwnerInFlight,
                    IgnoreCheckoutTargets = pattern.IgnoreCheckoutTargets,
                    VisualSpinAxis = entry.VisualSpinAxis,
                    VisualSpinDegreesPerSecond =
                        entry.VisualSpinDegreesPerSecond,
                    VisualScaleMultiplier = entry.VisualScaleMultiplier
                }
            );
        }
    }

    private static void ResolveEntryGroundLanding(
        Vector3 requestedLanding,
        Vector3 centerLanding,
        Vector3 centerNormal,
        PowerupGameplayProfile gameplayProfile,
        out Vector3 resolvedLanding,
        out Vector3 resolvedNormal)
    {
        resolvedLanding = requestedLanding;
        resolvedLanding.y = centerLanding.y;
        resolvedNormal = centerNormal;

        if (gameplayProfile == null || !gameplayProfile.HasGroundMask)
        {
            return;
        }

        Vector3 rayOrigin =
            requestedLanding +
            Vector3.up * gameplayProfile.GroundProbeHeight;

        if (!Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            gameplayProfile.GroundProbeDistance,
            gameplayProfile.PowerupGroundMask,
            QueryTriggerInteraction.Ignore
        ))
        {
            return;
        }

        resolvedNormal = hit.normal.sqrMagnitude > 0.000001f
            ? hit.normal.normalized
            : Vector3.up;

        resolvedLanding =
            hit.point +
            resolvedNormal * gameplayProfile.LandingClearance;
    }

    private static bool IsUsablePattern(ProjectileShotPattern pattern)
    {
        if (pattern == null || pattern.EntryCount <= 0) return false;

        for (int i = 0; i < pattern.EntryCount; i++)
        {
            if (pattern.GetEntry(i) == null) return false;
        }

        return true;
    }

    private void WarnIfPatternExceedsAdvertisedPreview(
        PowerupId powerupId,
        ProjectileShotPattern pattern,
        PowerupProjectileProfile projectileProfile,
        PowerupGameplayProfile gameplayProfile)
    {
        if (!warnWhenPatternExceedsPreview ||
            patternPreviewWarningLogged ||
            projectileProfile == null ||
            gameplayProfile == null)
        {
            return;
        }

        if (!projectileProfile.TryGetAuthoredHitboxPlanarRadius(
            powerupId,
            out float authoredFootprint
        ))
        {
            return;
        }

        float maximumPatternRadius = 0f;

        for (int i = 0; i < pattern.EntryCount; i++)
        {
            ProjectilePatternEntry entry = pattern.GetEntry(i);
            maximumPatternRadius = Mathf.Max(
                maximumPatternRadius,
                entry.NormalizedLandingOffset.magnitude *
                pattern.SpreadRadius +
                authoredFootprint * entry.VisualScaleMultiplier
            );
        }

        float advertisedRadius =
            gameplayProfile.GetImpactPreviewRadius(powerupId);

        if (maximumPatternRadius <= advertisedRadius + 0.0001f) return;

        patternPreviewWarningLogged = true;
        Debug.LogWarning(
            $"[PowerupProjectileLauncher] {powerupId} runtime footprint " +
            $"({maximumPatternRadius:0.##}) exceeds its advertised aim-preview " +
            $"radius ({advertisedRadius:0.##}). Increase the preview radius or " +
            "reduce the shot pattern.",
            this
        );
    }

    private void ResolveReferences()
    {
        if (playerPowerupController == null)
        {
            playerPowerupController =
                GetComponent<PlayerPowerupController>() ??
                GetComponentInParent<PlayerPowerupController>() ??
                GetComponentInChildren<PlayerPowerupController>(true);
        }

        if (targetingController == null)
        {
            targetingController =
                GetComponent<PowerupTargetingController>() ??
                GetComponentInParent<PowerupTargetingController>() ??
                GetComponentInChildren<PowerupTargetingController>(true);
        }

        if (projectilePool == null)
        {
            projectilePool = FindFirstObjectByType<PowerupProjectilePool>();
        }

        ResolveLifecycleEventSystem();
    }

    private void ResolveLifecycleEventSystem()
    {
        if (lifecycleEventSystem == null)
        {
            lifecycleEventSystem =
                FindFirstObjectByType<PowerupLifecycleEventSystem>();
        }
    }

    private void EnsureSubscriptions()
    {
        if (playerPowerupController != subscribedPowerupController)
        {
            if (subscribedPowerupController != null)
            {
                subscribedPowerupController.OnPowerupUseValidationRequested -=
                    ValidateProjectileExecution;
            }

            subscribedPowerupController = playerPowerupController;

            if (subscribedPowerupController != null)
            {
                subscribedPowerupController.OnPowerupUseValidationRequested +=
                    ValidateProjectileExecution;
            }
        }

        if (targetingController != subscribedTargetingController)
        {
            if (subscribedTargetingController != null)
            {
                subscribedTargetingController.OnTargetedPowerupUseAccepted -=
                    HandleAcceptedTargetedUse;
            }

            subscribedTargetingController = targetingController;

            if (subscribedTargetingController != null)
            {
                subscribedTargetingController.OnTargetedPowerupUseAccepted +=
                    HandleAcceptedTargetedUse;
            }
        }
    }

    private void Unsubscribe()
    {
        if (subscribedPowerupController != null)
        {
            subscribedPowerupController.OnPowerupUseValidationRequested -=
                ValidateProjectileExecution;
        }

        if (subscribedTargetingController != null)
        {
            subscribedTargetingController.OnTargetedPowerupUseAccepted -=
                HandleAcceptedTargetedUse;
        }

        subscribedPowerupController = null;
        subscribedTargetingController = null;
    }

    private void RollBackRentedProjectiles()
    {
        if (projectilePool != null)
        {
            for (int i = 0; i < rentedProjectiles.Count; i++)
            {
                projectilePool.ReturnUnlaunched(rentedProjectiles[i]);
            }
        }

        rentedProjectiles.Clear();
    }

    private void RecordFailure(
        PowerupId powerupId,
        PowerupProjectileLaunchFailureReason reason)
    {
        lastFailureReason = reason;

        if (logLaunchFailures)
        {
            Debug.LogWarning(
                $"[PowerupProjectileLauncher] P" +
                $"{(playerPowerupController != null ? playerPowerupController.PlayerIndex : 0)} " +
                $"could not launch {powerupId}: {reason}. " +
                "The stored power-up was retained whenever inventory was still available.",
                this
            );
        }

        OnShotLaunchFailed?.Invoke(this, powerupId, reason);
    }
}
