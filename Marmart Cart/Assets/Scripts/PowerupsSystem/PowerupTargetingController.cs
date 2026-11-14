using System;
using UnityEngine;

/// <summary>
/// Per-player deterministic projectile targeting.
///
/// The left stick selects direction and analog range. The requested endpoint
/// is resolved onto an explicit ground mask, then a semantic state is published
/// for the shared Shapes renderer. This component captures accepted snapshots;
/// a separate launcher owns spawning, pooling, and inventory consumption.
/// </summary>
[DisallowMultipleComponent]
public class PowerupTargetingController : MonoBehaviour
{
    #region References

    [Header("References")]
    [SerializeField] private PlayerPowerupController playerPowerupController;
    [SerializeField] private PowerupAimStateSystem aimStateSystem;
    [SerializeField] private PowerupGameplayProfile gameplayProfile;

    [Tooltip(
        "Shared projectile asset that owns Tomato/Ice flight-time tuning. " +
        "When left empty, the scene projectile pool is used to resolve it."
    )]
    [SerializeField] private PowerupProjectileProfile projectileProfile;

    #endregion

    #region Diagnostics

    [Header("Diagnostics")]
    [SerializeField] private bool logAcceptedAimSnapshots = true;

    [Header("Runtime - Read Only")]
    [SerializeField] private CartControlScript boundCartControl;
    [SerializeField] private PowerupMounts boundMounts;
    [SerializeField] private PowerupAimState currentAimState;
    [SerializeField] private PowerupAimState lastAcceptedAimSnapshot;
    [SerializeField] private uint acceptedAimSnapshotVersion;

    private PlayerPowerupController subscribedPowerupController;
    private bool missingControllerLogged;
    private bool missingStateSystemLogged;
    private bool missingGameplayProfileLogged;
    private bool missingProjectileProfileLogged;

    #endregion

    #region Public State

    public PowerupAimState CurrentAimState => currentAimState;
    public PowerupAimState LastAcceptedAimSnapshot => lastAcceptedAimSnapshot;
    public uint AcceptedAimSnapshotVersion => acceptedAimSnapshotVersion;
    public PlayerPowerupController PlayerPowerupController =>
        playerPowerupController;
    public PowerupGameplayProfile GameplayProfile => gameplayProfile;
    public PowerupProjectileProfile ProjectileProfile => projectileProfile;

    /// <summary>
    /// Raised with the exact valid state captured on the accepted input frame.
    /// A later projectile executor can consume this event without re-reading a
    /// moving stick or a moving cart.
    /// </summary>
    public event Action<PowerupTargetingController, PowerupAimState>
        OnTargetedPowerupUseAccepted;

    #endregion

    #region Unity Lifecycle

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
        EnsureControllerSubscription();
    }

    private void LateUpdate()
    {
        ResolveMissingReferences();
        EnsureControllerSubscription();
        RebuildAimState(true);
    }

    private void OnDisable()
    {
        UnsubscribeFromController();
        ClearAimState();
    }

    #endregion

    #region Target Calculation

    private bool RebuildAimState(bool publish)
    {
        if (playerPowerupController == null)
        {
            LogMissingControllerOnce();
            ClearAimState();
            return false;
        }

        missingControllerLogged = false;

        if (gameplayProfile == null)
        {
            if (!missingGameplayProfileLogged)
            {
                missingGameplayProfileLogged = true;
                Debug.LogError(
                    "[PowerupTargetingController] Assign a PowerupGameplayProfile. " +
                    "Projectile targeting fails closed without authoritative tuning.",
                    this
                );
            }

            ClearAimState();
            return false;
        }

        missingGameplayProfileLogged = false;

        if (projectileProfile == null)
        {
            if (!missingProjectileProfileLogged)
            {
                missingProjectileProfileLogged = true;
                Debug.LogError(
                    "[PowerupTargetingController] Assign the shared " +
                    "PowerupProjectileProfile. Projectile-specific flight " +
                    "time cannot be calculated without it.",
                    this
                );
            }

            ClearAimState();
            return false;
        }

        missingProjectileProfileLogged = false;

        if (!playerPowerupController.CanUseStoredPowerup ||
            !PowerupIdRules.RequiresProjectileAim(
                playerPowerupController.StoredPowerup
            ))
        {
            ClearAimState();
            return false;
        }

        ResolveRuntimeCartReferences();

        if (boundCartControl == null)
        {
            ClearAimState();
            return false;
        }

        Vector2 rawAimInput = boundCartControl.RawAimInput;
        float rawMagnitude = Mathf.Clamp01(rawAimInput.magnitude);
        float adjustedMagnitude =
            gameplayProfile.RemapAimMagnitude(rawMagnitude);

        // Neutral input is intentionally not a guessed direction. It clears
        // the preview and makes a targeted activation fail while retaining the
        // stored item.
        if (adjustedMagnitude <= 0f)
        {
            ClearAimState();
            return false;
        }

        Vector3 aimDirection = boundCartControl.AimDirection;
        aimDirection.y = 0f;

        if (aimDirection.sqrMagnitude <= 0.000001f)
        {
            ClearAimState();
            return false;
        }

        aimDirection.Normalize();

        Transform cartTransform = boundCartControl.transform;
        Vector3 rangeOrigin = boundMounts != null
            ? boundMounts.GetRangeOriginPosition(cartTransform)
            : cartTransform.position;

        Vector3 startPosition = boundMounts != null
            ? boundMounts.GetThrowOriginPosition(
                cartTransform,
                gameplayProfile.FallbackThrowOriginOffset
            )
            : cartTransform.TransformPoint(
                gameplayProfile.FallbackThrowOriginOffset
            );

        float requestedRange =
            gameplayProfile.EvaluateThrowRange(adjustedMagnitude);

        ResolveGroundLandingPoint(
            rangeOrigin,
            aimDirection,
            requestedRange,
            out Vector3 landingPosition,
            out Vector3 landingNormal
        );

        float resolvedDistance =
            PowerupTrajectory.PlanarDistance(rangeOrigin, landingPosition);

        float flightDuration = projectileProfile.EvaluateFlightTime(
            playerPowerupController.StoredPowerup,
            resolvedDistance,
            gameplayProfile.MinimumThrowRange,
            gameplayProfile.MaximumThrowRange
        );

        float arcHeight = gameplayProfile.EvaluateArcHeight(
            resolvedDistance
        );

        ResolvePreviewEndpoint(
            startPosition,
            landingPosition,
            landingNormal,
            arcHeight,
            out bool trajectoryObstructed,
            out float previewEndNormalizedTime,
            out Vector3 previewEndPosition,
            out Vector3 previewEndNormal
        );

        currentAimState = new PowerupAimState
        {
            PlayerIndex = playerPowerupController.PlayerIndex,
            Visible = true,
            PowerupId = playerPowerupController.StoredPowerup,
            RawAimInput = rawAimInput,
            RawAimMagnitude = rawMagnitude,
            AdjustedAimMagnitude = adjustedMagnitude,
            AimDirection = aimDirection,
            RangeOrigin = rangeOrigin,
            StartPosition = startPosition,
            LandingPosition = landingPosition,
            LandingNormal = landingNormal,
            TrajectoryObstructed = trajectoryObstructed,
            PreviewEndNormalizedTime = previewEndNormalizedTime,
            PreviewEndPosition = previewEndPosition,
            PreviewEndNormal = previewEndNormal,
            RequestedRange = requestedRange,
            ResolvedPlanarDistance = resolvedDistance,
            FlightDuration = flightDuration,
            ArcHeight = arcHeight,
            ImpactPreviewRadius = gameplayProfile.GetImpactPreviewRadius(
                playerPowerupController.StoredPowerup
            )
        };

        if (publish && aimStateSystem != null)
        {
            currentAimState = aimStateSystem.PublishState(
                playerPowerupController.PlayerIndex,
                currentAimState
            );
        }

        return true;
    }

    private void ResolveGroundLandingPoint(
        Vector3 rangeOrigin,
        Vector3 aimDirection,
        float requestedRange,
        out Vector3 landingPosition,
        out Vector3 landingNormal)
    {
        Vector3 requestedPoint =
            rangeOrigin + aimDirection * requestedRange;

        // Ground is expected throughout the authored map. Missing mask/hit is
        // therefore a simple height fallback, never an invalid target and
        // never a reason to alter the player's requested range.
        landingPosition = requestedPoint;
        landingNormal = Vector3.up;

        if (gameplayProfile == null || !gameplayProfile.HasGroundMask)
        {
            return;
        }

        Vector3 rayOrigin =
            requestedPoint + Vector3.up * gameplayProfile.GroundProbeHeight;

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

        Vector3 normal = hit.normal.sqrMagnitude > 0.000001f
            ? hit.normal.normalized
            : Vector3.up;

        landingPosition =
            hit.point + normal * gameplayProfile.LandingClearance;
        landingNormal = normal;
    }

    private void ResolvePreviewEndpoint(
        Vector3 startPosition,
        Vector3 landingPosition,
        Vector3 landingNormal,
        float arcHeight,
        out bool trajectoryObstructed,
        out float previewEndNormalizedTime,
        out Vector3 previewEndPosition,
        out Vector3 previewEndNormal)
    {
        trajectoryObstructed = false;
        previewEndNormalizedTime = 1f;
        previewEndPosition = landingPosition;
        previewEndNormal = landingNormal;

        if (gameplayProfile == null ||
            !gameplayProfile.HasProjectileBlockingMask)
        {
            return;
        }

        int sampleCount = Mathf.Max(
            4,
            gameplayProfile.ProjectileBlockingSampleCount
        );

        float apexTime =
            PowerupTrajectory.CalculateApexNormalizedTime(
                startPosition,
                landingPosition,
                arcHeight
            );

        if (apexTime >= 1f) return;

        // Environment/lifting layers are intentionally ignored while the
        // projectile is rising. Begin the preview blocker query at the true
        // world-space apex and sample only the descending portion.
        float previousTime = apexTime;
        Vector3 previousPoint = PowerupTrajectory.Evaluate(
            startPosition,
            landingPosition,
            arcHeight,
            previousTime
        );

        for (int sampleIndex = 1; sampleIndex < sampleCount; sampleIndex++)
        {
            float sampleFraction =
                sampleIndex / (float)(sampleCount - 1);

            float currentTime = Mathf.Lerp(
                apexTime,
                1f,
                sampleFraction
            );

            Vector3 currentPoint = PowerupTrajectory.Evaluate(
                startPosition,
                landingPosition,
                arcHeight,
                currentTime
            );

            if (Physics.Linecast(
                previousPoint,
                currentPoint,
                out RaycastHit hit,
                gameplayProfile.ProjectileBlockingMask,
                QueryTriggerInteraction.Ignore
            ))
            {
                float segmentLength =
                    Vector3.Distance(previousPoint, currentPoint);

                float segmentProgress = segmentLength > 0.000001f
                    ? Mathf.Clamp01(hit.distance / segmentLength)
                    : 0f;

                trajectoryObstructed = true;
                previewEndNormalizedTime = Mathf.Lerp(
                    previousTime,
                    currentTime,
                    segmentProgress
                );
                previewEndPosition = hit.point;
                previewEndNormal = hit.normal.sqrMagnitude > 0.000001f
                    ? hit.normal.normalized
                    : Vector3.up;
                return;
            }

            previousTime = currentTime;
            previousPoint = currentPoint;
        }
    }

    private void ClearAimState()
    {
        int playerIndex = playerPowerupController != null
            ? playerPowerupController.PlayerIndex
            : currentAimState.PlayerIndex;

        bool stateWasVisible = currentAimState.Visible;

        currentAimState = new PowerupAimState
        {
            PlayerIndex = playerIndex
        };

        if (stateWasVisible && aimStateSystem != null && playerIndex > 0)
        {
            aimStateSystem.ClearState(playerIndex);
        }
    }

    #endregion

    #region Activation Validation / Snapshot

    private bool ValidatePowerupUse(
        PlayerPowerupController controller,
        PowerupId requestedPowerup)
    {
        if (!PowerupIdRules.RequiresProjectileAim(requestedPowerup))
        {
            return true;
        }

        if (controller != playerPowerupController) return false;

        bool valid = RebuildAimState(true);

        if (!valid ||
            !currentAimState.Visible ||
            currentAimState.PowerupId != requestedPowerup)
        {
            return false;
        }

        if (gameplayProfile.BlockActivationWhenTrajectoryObstructed &&
            currentAimState.TrajectoryObstructed)
        {
            return false;
        }

        return true;
    }

    private void HandleAcceptedUse(
        PlayerPowerupController controller,
        PowerupId requestedPowerup)
    {
        if (controller != playerPowerupController ||
            !PowerupIdRules.RequiresProjectileAim(requestedPowerup) ||
            !currentAimState.Visible ||
            currentAimState.PowerupId != requestedPowerup)
        {
            return;
        }

        lastAcceptedAimSnapshot = currentAimState;
        acceptedAimSnapshotVersion++;

        if (logAcceptedAimSnapshots)
        {
            Debug.Log(
                $"[PowerupTargetingController] P{controller.PlayerIndex} " +
                $"captured {requestedPowerup} aim snapshot " +
                $"#{acceptedAimSnapshotVersion} at " +
                $"{lastAcceptedAimSnapshot.LandingPosition}.",
                this
            );
        }

        OnTargetedPowerupUseAccepted?.Invoke(
            this,
            lastAcceptedAimSnapshot
        );
    }

    #endregion

    #region Binding

    private void ResolveReferences()
    {
        if (playerPowerupController == null)
        {
            playerPowerupController =
                GetComponent<PlayerPowerupController>() ??
                GetComponentInParent<PlayerPowerupController>() ??
                GetComponentInChildren<PlayerPowerupController>(true);
        }

        if (aimStateSystem == null)
        {
            aimStateSystem = FindFirstObjectByType<PowerupAimStateSystem>();
        }

        if (projectileProfile == null)
        {
            PowerupProjectilePool projectilePool =
                FindFirstObjectByType<PowerupProjectilePool>();

            if (projectilePool != null)
            {
                projectileProfile = projectilePool.ProjectileProfile;
            }
        }

        ResolveRuntimeCartReferences();
    }

    private void ResolveMissingReferences()
    {
        if (playerPowerupController == null ||
            aimStateSystem == null ||
            projectileProfile == null)
        {
            ResolveReferences();
        }

        if (aimStateSystem == null)
        {
            if (!missingStateSystemLogged)
            {
                missingStateSystemLogged = true;
                Debug.LogWarning(
                    "[PowerupTargetingController] No PowerupAimStateSystem was found. " +
                    "Use validation still works, but no shared aim preview can render.",
                    this
                );
            }
        }
        else
        {
            missingStateSystemLogged = false;
        }
    }

    private void ResolveRuntimeCartReferences()
    {
        CartControlScript resolvedCart = playerPowerupController != null
            ? playerPowerupController.CartControlInput
            : null;

        if (resolvedCart == boundCartControl) return;

        boundCartControl = resolvedCart;
        boundMounts = null;

        if (boundCartControl == null) return;

        boundMounts =
            boundCartControl.GetComponent<PowerupMounts>() ??
            boundCartControl.GetComponentInParent<PowerupMounts>() ??
            boundCartControl.GetComponentInChildren<PowerupMounts>(true);
    }

    private void EnsureControllerSubscription()
    {
        if (playerPowerupController == null) return;
        if (subscribedPowerupController == playerPowerupController) return;

        UnsubscribeFromController();

        subscribedPowerupController = playerPowerupController;
        subscribedPowerupController.OnPowerupUseValidationRequested +=
            ValidatePowerupUse;
        subscribedPowerupController.OnPowerupUseRequested +=
            HandleAcceptedUse;
    }

    private void UnsubscribeFromController()
    {
        if (subscribedPowerupController == null) return;

        subscribedPowerupController.OnPowerupUseValidationRequested -=
            ValidatePowerupUse;
        subscribedPowerupController.OnPowerupUseRequested -=
            HandleAcceptedUse;
        subscribedPowerupController = null;
    }

    private void LogMissingControllerOnce()
    {
        if (missingControllerLogged) return;

        missingControllerLogged = true;
        Debug.LogError(
            "[PowerupTargetingController] No PlayerPowerupController could be resolved.",
            this
        );
    }

    #endregion
}
