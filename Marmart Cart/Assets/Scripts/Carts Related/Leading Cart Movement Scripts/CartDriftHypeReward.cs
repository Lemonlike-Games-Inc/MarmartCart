using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Builds a pending Hype reward while the player maintains a valid drift.
///
/// CLEAN SUCCESS:
///     Pays the full pending Hype reward.
///
/// FAILED / INTERRUPTED DRIFT:
///     Optionally removes a configurable percentage of that pending reward
///     from the player's EXISTING Hype.
///
/// Example:
///     pending reward = 20
///     failure penalty fraction = 0.50
///     failed drift = lose up to 10 existing Hype.
///
/// Intentional clean release/cancel does NOT use the failure penalty path.
/// </summary>
public class CartDriftHypeReward : MonoBehaviour
{
    #region References

    [Header("References")]
    [SerializeField] private CartDriftController driftController;
    [SerializeField] private CartControlScript cartController;

    #endregion

    #region Reward Rules

    [Header("Reward Toggle")]
    [FormerlySerializedAs("enableDriftFuelReward")]
    [SerializeField] private bool enableDriftHypeReward = true;

    [Header("Success Rules")]
    [SerializeField] private float minimumSuccessDuration = 0.6f;

    [Header("Base Reward")]
    [FormerlySerializedAs("baseFuelGainPerSecond")]
    [Tooltip("Base Hype gained per second before tightness and duration multipliers.")]
    [SerializeField] private float baseHypeGainPerSecond = 5f;

    [FormerlySerializedAs("maxFuelRewardPerDrift")]
    [Tooltip("Maximum Hype that can be earned from one drift.")]
    [SerializeField] private float maxHypeRewardPerDrift = 25f;

    [Header("Tightness Reward")]
    [Tooltip("No pending Hype is gained below this drift tightness.")]
    [SerializeField, Range(0f, 1f)] private float minimumRewardTightness = 0.2f;

    [Tooltip("Multiplier when tightness is at Minimum Reward Tightness.")]
    [SerializeField] private float minTightnessRewardMultiplier = 0.5f;

    [Tooltip("Multiplier when tightness is 1.")]
    [SerializeField] private float maxTightnessRewardMultiplier = 1.3f;

    [Header("Duration Reward Ramp")]
    [Tooltip("How many seconds it takes for duration reward rate to reach maximum.")]
    [SerializeField] private float durationToReachMaxRewardRate = 3f;

    [Tooltip("Reward multiplier at the beginning of the drift.")]
    [SerializeField] private float startingDurationRewardMultiplier = 0.35f;

    [Tooltip("Reward multiplier after Duration To Reach Max Reward Rate.")]
    [SerializeField] private float maxDurationRewardMultiplier = 2f;

    [Tooltip("Higher values make long drifts ramp reward faster near the end.")]
    [SerializeField]
    private AnimationCurve durationRewardCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.4f, 0.25f),
        new Keyframe(0.75f, 0.7f),
        new Keyframe(1f, 1f)
    );

    [Header("Clean End Rules")]
    [Tooltip("If true, pressing Speedup while drifting still pays out pending drift Hype.")]
    [SerializeField] private bool payoutWhenCancelledBySpeedup = true;

    #endregion

    #region Failure Penalty

    [Header("Failed Drift Hype Penalty")]
    [Tooltip(
        "If enabled, an interrupted/failed drift removes some EXISTING Hype. " +
        "Clean release/cancel does not trigger this."
    )]
    [SerializeField] private bool enableFailedDriftHypePenalty = true;

    [Tooltip(
        "Penalty as a fraction of the current pending SUCCESS reward. " +
        "0.5 means: pending reward 20 -> failed drift risks losing 10 existing Hype."
    )]
    [Range(0f, 1f)]
    [SerializeField] private float failedDriftPenaltyFraction = 0.5f;

    #endregion

    #region Runtime

    [Header("Debug")]
    [SerializeField] private bool debugReward = true;

    private bool isTrackingDrift;
    private float currentDriftDuration;
    private float pendingHypeReward;

    private float currentDurationRewardMultiplier = 1f;
    private float currentTightnessRewardMultiplier = 1f;

    public bool DriftHypeRewardEnabled => enableDriftHypeReward;

    public bool IsTrackingDrift => isTrackingDrift;
    public float CurrentDriftDuration => currentDriftDuration;
    /// <summary>
    /// Raw reward accumulated from drift performance.
    /// This value is still capped by Max Hype Reward Per Drift.
    /// </summary>
    public float PendingHypeReward => pendingHypeReward;

    /// <summary>
    /// Portion of PendingHypeReward that could ACTUALLY fit into the
    /// player's Hype meter right now.
    ///
    /// Example:
    /// Current Hype = 90
    /// Max Hype = 100
    /// Raw Pending = 25
    /// Effective Pending = 10
    ///
    /// UI reward preview, failure risk, and payout use this value.
    /// </summary>
    public float EffectivePendingHypeReward => GetEffectivePendingHypeReward();

    public float PreviewHypeAmount => GetPreviewHypeAmount01();
    public bool HasPendingReward => EffectivePendingHypeReward > 0.01f;

    public float CurrentDurationRewardMultiplier => currentDurationRewardMultiplier;
    public float CurrentTightnessRewardMultiplier => currentTightnessRewardMultiplier;
    public float CurrentTotalRewardMultiplier => currentDurationRewardMultiplier * currentTightnessRewardMultiplier;

    public bool FailedDriftHypePenaltyEnabled => enableDriftHypeReward && enableFailedDriftHypePenalty;
    public float FailedDriftPenaltyFraction => failedDriftPenaltyFraction;
    public float PotentialFailedDriftHypePenalty => GetPotentialFailedDriftHypePenalty();
    public float ProjectedHypeAfterFailure => GetProjectedHypeAfterFailure();
    public float ProjectedHypeAfterFailureNormalized => GetProjectedHypeAfterFailureNormalized();

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (driftController == null) driftController = GetComponentInParent<CartDriftController>();
        if (cartController == null) cartController = GetComponentInParent<CartControlScript>();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ClearStateOnly();
    }

    private void OnValidate()
    {
        minimumSuccessDuration = Mathf.Max(0f, minimumSuccessDuration);
        baseHypeGainPerSecond = Mathf.Max(0f, baseHypeGainPerSecond);
        maxHypeRewardPerDrift = Mathf.Max(0f, maxHypeRewardPerDrift);
        minTightnessRewardMultiplier = Mathf.Max(0f, minTightnessRewardMultiplier);
        maxTightnessRewardMultiplier = Mathf.Max(0f, maxTightnessRewardMultiplier);
        durationToReachMaxRewardRate = Mathf.Max(0f, durationToReachMaxRewardRate);
        startingDurationRewardMultiplier = Mathf.Max(0f, startingDurationRewardMultiplier);
        maxDurationRewardMultiplier = Mathf.Max(0f, maxDurationRewardMultiplier);
        failedDriftPenaltyFraction = Mathf.Clamp01(failedDriftPenaltyFraction);
    }

    private void Update()
    {
        if (!enableDriftHypeReward || !isTrackingDrift) return;
        if (driftController == null || cartController == null) return;
        if (!driftController.IsDrifting) return;

        UpdatePendingHype();
    }

    #endregion

    #region Drift Events

    private void Subscribe()
    {
        if (driftController == null) return;

        driftController.OnDriftStarted += HandleDriftStarted;
        driftController.OnDriftEndedClean += HandleDriftEndedClean;
        driftController.OnDriftInterrupted += HandleDriftInterrupted;
    }

    private void Unsubscribe()
    {
        if (driftController == null) return;

        driftController.OnDriftStarted -= HandleDriftStarted;
        driftController.OnDriftEndedClean -= HandleDriftEndedClean;
        driftController.OnDriftInterrupted -= HandleDriftInterrupted;
    }

    private void HandleDriftStarted()
    {
        if (!enableDriftHypeReward) return;

        isTrackingDrift = true;
        currentDriftDuration = 0f;
        pendingHypeReward = 0f;

        currentDurationRewardMultiplier = startingDurationRewardMultiplier;
        currentTightnessRewardMultiplier = 1f;

        if (debugReward)
        {
            Debug.Log(
                $"[Drift Hype] Started tracking | failurePenalty:{FailedDriftHypePenaltyEnabled} | " +
                $"penaltyFraction:{failedDriftPenaltyFraction:P0}",
                this
            );
        }
    }

    private void HandleDriftEndedClean(string reason)
    {
        if (!isTrackingDrift) return;

        bool isSpeedupCancel =
            !string.IsNullOrEmpty(reason) &&
            reason.ToLowerInvariant().Contains("speedup");

        if (isSpeedupCancel && !payoutWhenCancelledBySpeedup)
        {
            ClearPendingReward("Clean Speedup cancel, payout disabled");
            return;
        }

        if (currentDriftDuration < minimumSuccessDuration)
        {
            ClearPendingReward("Too short");
            return;
        }

        float effectiveReward = GetEffectivePendingHypeReward();

        if (effectiveReward <= 0.01f)
        {
            ClearPendingReward("No meaningful pending reward / Hype already full");
            return;
        }

        cartController.AddHype(effectiveReward);

        if (debugReward)
        {
            Debug.Log(
                $"[Drift Hype] SUCCESS | +{effectiveReward:F1} Hype | rawPending:{pendingHypeReward:F1} | " +
                $"duration:{currentDriftDuration:F2}s | " +
                $"durationMult:{currentDurationRewardMultiplier:F2} | " +
                $"tightnessMult:{currentTightnessRewardMultiplier:F2} | " +
                $"reason:{reason}",
                this
            );
        }

        ClearStateOnly();
    }

    private void HandleDriftInterrupted(string reason)
    {
        if (!isTrackingDrift) return;

        float pendingAtFailure = pendingHypeReward;
        float meaningfulRewardAtFailure = GetEffectivePendingHypeReward();
        float requestedPenalty = GetPotentialFailedDriftHypePenalty();

        float hypeBeforePenalty =
            cartController != null
                ? cartController.CurrentHype
                : 0f;

        if (cartController != null &&
            FailedDriftHypePenaltyEnabled &&
            requestedPenalty > 0.01f)
        {
            cartController.RemoveHype(requestedPenalty);
        }

        float hypeAfterPenalty =
            cartController != null
                ? cartController.CurrentHype
                : hypeBeforePenalty;

        float actualPenalty =
            Mathf.Max(
                0f,
                hypeBeforePenalty - hypeAfterPenalty
            );

        if (debugReward)
        {
            Debug.Log(
                $"[Drift Hype] FAILED | raw pending lost:{pendingAtFailure:F1} | " +
                $"meaningful reward at risk:{meaningfulRewardAtFailure:F1} | " +
                $"requested penalty:{requestedPenalty:F1} | actual Hype lost:{actualPenalty:F1} | " +
                $"duration:{currentDriftDuration:F2}s | reason:{reason}",
                this
            );
        }

        ClearStateOnly();
    }

    #endregion

    #region Reward Accumulation

    private void UpdatePendingHype()
    {
        currentDriftDuration += Time.deltaTime;

        float tightness =
            Mathf.Clamp01(
                driftController.CurrentTightness
            );

        if (tightness < minimumRewardTightness)
        {
            return;
        }

        currentTightnessRewardMultiplier =
            GetTightnessRewardMultiplier(
                tightness
            );

        currentDurationRewardMultiplier =
            GetDurationRewardMultiplier();

        float gain =
            baseHypeGainPerSecond *
            currentTightnessRewardMultiplier *
            currentDurationRewardMultiplier *
            Time.deltaTime;

        // IMPORTANT:
        // Do not create additional risk for reward that can no longer fit
        // into the player's Hype meter.
        //
        // Example:
        // Current = 80, Max = 100
        // Raw pending is allowed to grow only to 20.
        //
        // Once projected Hype reaches 100:
        //     reward stops increasing
        //     failure penalty stops increasing
        float maximumMeaningfulPending =
            GetMaximumMeaningfulPendingReward();

        if (pendingHypeReward >= maximumMeaningfulPending - 0.0001f)
        {
            return;
        }

        pendingHypeReward =
            Mathf.Min(
                pendingHypeReward + gain,
                maxHypeRewardPerDrift,
                maximumMeaningfulPending
            );
    }

    private float GetTightnessRewardMultiplier(float tightness)
    {
        float tightness01 =
            Mathf.InverseLerp(
                minimumRewardTightness,
                1f,
                tightness
            );

        return Mathf.Lerp(
            minTightnessRewardMultiplier,
            maxTightnessRewardMultiplier,
            tightness01
        );
    }

    private float GetDurationRewardMultiplier()
    {
        if (durationToReachMaxRewardRate <= 0.01f)
        {
            return maxDurationRewardMultiplier;
        }

        float duration01 =
            Mathf.Clamp01(
                currentDriftDuration /
                durationToReachMaxRewardRate
            );

        float curveValue =
            durationRewardCurve != null
                ? Mathf.Clamp01(
                    durationRewardCurve.Evaluate(
                        duration01
                    )
                )
                : duration01;

        return Mathf.Lerp(
            startingDurationRewardMultiplier,
            maxDurationRewardMultiplier,
            curveValue
        );
    }

    #endregion

    #region State / Preview

    private void ClearPendingReward(string reason)
    {
        if (debugReward)
        {
            Debug.Log(
                $"[Drift Hype] No payout | pending:{pendingHypeReward:F1} | " +
                $"duration:{currentDriftDuration:F2}s | reason:{reason}",
                this
            );
        }

        ClearStateOnly();
    }

    private void ClearStateOnly()
    {
        isTrackingDrift = false;
        currentDriftDuration = 0f;
        pendingHypeReward = 0f;

        currentDurationRewardMultiplier = 1f;
        currentTightnessRewardMultiplier = 1f;
    }

    private float GetPreviewHypeAmount01()
    {
        if (cartController == null ||
            cartController.MaxHype <= 0.01f)
        {
            return 0f;
        }

        float previewHype =
            cartController.CurrentHype +
            GetEffectivePendingHypeReward();

        return Mathf.Clamp01(
            previewHype /
            cartController.MaxHype
        );
    }

    /// <summary>
    /// Maximum pending reward that is meaningful at the player's CURRENT Hype.
    ///
    /// The limit is whichever is smaller:
    /// - this drift's authored Max Hype Reward Per Drift;
    /// - the empty room remaining in the Hype meter.
    /// </summary>
    private float GetMaximumMeaningfulPendingReward()
    {
        if (cartController == null)
        {
            return 0f;
        }

        float hypeRoom =
            Mathf.Max(
                0f,
                cartController.MaxHype -
                cartController.CurrentHype
            );

        return Mathf.Min(
            maxHypeRewardPerDrift,
            hypeRoom
        );
    }

    private float GetEffectivePendingHypeReward()
    {
        return Mathf.Clamp(
            pendingHypeReward,
            0f,
            GetMaximumMeaningfulPendingReward()
        );
    }

    private float GetPotentialFailedDriftHypePenalty()
    {
        if (!FailedDriftHypePenaltyEnabled)
        {
            return 0f;
        }

        // Risk is based ONLY on reward the player could actually receive.
        // Wasted reward beyond Max Hype creates no additional punishment.
        return Mathf.Max(
            0f,
            GetEffectivePendingHypeReward() *
            failedDriftPenaltyFraction
        );
    }

    private float GetProjectedHypeAfterFailure()
    {
        if (cartController == null)
        {
            return 0f;
        }

        return Mathf.Max(
            0f,
            cartController.CurrentHype -
            GetPotentialFailedDriftHypePenalty()
        );
    }

    private float GetProjectedHypeAfterFailureNormalized()
    {
        if (cartController == null ||
            cartController.MaxHype <= 0.01f)
        {
            return 0f;
        }

        return Mathf.Clamp01(
            GetProjectedHypeAfterFailure() /
            cartController.MaxHype
        );
    }

    #endregion
}
