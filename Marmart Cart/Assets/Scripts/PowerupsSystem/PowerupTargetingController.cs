using System;
using UnityEngine;

/// <summary>
/// Per-player deterministic projectile targeting.
///
/// The left stick selects direction and analog range. The requested endpoint
/// is resolved onto an explicit ground mask, then a semantic state is published
/// for the shared Shapes renderer. This component does not spawn or consume a
/// projectile in the targeting-preview milestone.
/// </summary>
[DisallowMultipleComponent]
public class PowerupTargetingController : MonoBehaviour
{
    #region References

    [Header("References")]
    [SerializeField] private PlayerPowerupController playerPowerupController;
    [SerializeField] private PowerupAimStateSystem aimStateSystem;
    [SerializeField] private PowerupGameplayProfile gameplayProfile;

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
    private bool missingGroundMaskLogged;

    #endregion

    #region Public State

    public PowerupAimState CurrentAimState => currentAimState;
    public PowerupAimState LastAcceptedAimSnapshot => lastAcceptedAimSnapshot;
    public uint AcceptedAimSnapshotVersion => acceptedAimSnapshotVersion;

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

        if (!gameplayProfile.HasGroundMask)
        {
            if (!missingGroundMaskLogged)
            {
                missingGroundMaskLogged = true;
                Debug.LogError(
                    "[PowerupTargetingController] Powerup Ground Mask is empty. " +
                    "Assign only the explicit walkable PowerupGround layer(s).",
                    gameplayProfile
                );
            }
        }
        else
        {
            missingGroundMaskLogged = false;
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

        bool targetValid = TryResolveLandingPoint(
            rangeOrigin,
            aimDirection,
            requestedRange,
            out Vector3 landingPosition,
            out Vector3 landingNormal
        );

        if (!targetValid)
        {
            // Keep a visible invalid endpoint at the requested X/Z so setup
            // errors and off-map aim are immediately understandable.
            landingPosition = rangeOrigin + aimDirection * requestedRange;
            landingNormal = Vector3.up;
        }

        float resolvedDistance =
            PowerupTrajectory.PlanarDistance(rangeOrigin, landingPosition);

        currentAimState = new PowerupAimState
        {
            PlayerIndex = playerPowerupController.PlayerIndex,
            Visible = true,
            TargetValid = targetValid,
            PowerupId = playerPowerupController.StoredPowerup,
            RawAimInput = rawAimInput,
            RawAimMagnitude = rawMagnitude,
            AdjustedAimMagnitude = adjustedMagnitude,
            AimDirection = aimDirection,
            RangeOrigin = rangeOrigin,
            StartPosition = startPosition,
            LandingPosition = landingPosition,
            LandingNormal = landingNormal,
            RequestedRange = requestedRange,
            ResolvedPlanarDistance = resolvedDistance,
            FlightDuration = gameplayProfile.EvaluateFlightTime(
                resolvedDistance
            ),
            ArcHeight = gameplayProfile.EvaluateArcHeight(
                resolvedDistance
            ),
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

        return targetValid;
    }

    private bool TryResolveLandingPoint(
        Vector3 rangeOrigin,
        Vector3 aimDirection,
        float requestedRange,
        out Vector3 landingPosition,
        out Vector3 landingNormal)
    {
        landingPosition = default;
        landingNormal = Vector3.up;

        if (gameplayProfile == null || !gameplayProfile.HasGroundMask)
        {
            return false;
        }

        int fallbackSteps = gameplayProfile.FallbackStepCount;
        int sampleCount = Mathf.Max(1, fallbackSteps + 1);

        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            float rangeFraction = fallbackSteps <= 0
                ? 1f
                : 1f - sampleIndex / (float)fallbackSteps;

            float sampleRange = Mathf.Max(0f, requestedRange * rangeFraction);
            Vector3 requestedPoint =
                rangeOrigin + aimDirection * sampleRange;

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
                continue;
            }

            Vector3 normal = hit.normal.sqrMagnitude > 0.000001f
                ? hit.normal.normalized
                : Vector3.up;

            landingPosition =
                hit.point + normal * gameplayProfile.LandingClearance;
            landingNormal = normal;
            return true;
        }

        return false;
    }

    private void ClearAimState()
    {
        int playerIndex = playerPowerupController != null
            ? playerPowerupController.PlayerIndex
            : currentAimState.PlayerIndex;

        bool stateWasVisible =
            currentAimState.Visible || currentAimState.TargetValid;

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

        return valid &&
               currentAimState.Visible &&
               currentAimState.TargetValid &&
               currentAimState.PowerupId == requestedPowerup;
    }

    private void HandleAcceptedUse(
        PlayerPowerupController controller,
        PowerupId requestedPowerup)
    {
        if (controller != playerPowerupController ||
            !PowerupIdRules.RequiresProjectileAim(requestedPowerup) ||
            !currentAimState.Visible ||
            !currentAimState.TargetValid ||
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
                $"{lastAcceptedAimSnapshot.LandingPosition}. " +
                "Projectile spawning and inventory consumption are intentionally deferred.",
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

        ResolveRuntimeCartReferences();
    }

    private void ResolveMissingReferences()
    {
        if (playerPowerupController == null || aimStateSystem == null)
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
