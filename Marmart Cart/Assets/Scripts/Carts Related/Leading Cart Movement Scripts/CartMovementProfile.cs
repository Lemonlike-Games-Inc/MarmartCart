using UnityEngine;

/// <summary>
/// Shared movement tuning asset for the leading cart raycast-wheel motor.
///
/// Keep broad design numbers here:
/// - Base speed.
/// - Speedup additive bonus.
/// - Per-mode turn speed assist.
/// - Drift dip / drift sustained speed-loss behavior.
///
/// Important:
/// - Generic per-mode speed multipliers are intentionally removed.
/// - Normal Drive target speed = chain-adjusted base speed.
/// - Speedup target speed = chain-adjusted base speed + additive speedup bonus.
/// - Drift target speed = chain-adjusted base speed, modified only by drift feel rules
///   such as tight-drift dip and tight-drift fatigue.
/// </summary>
[CreateAssetMenu(
    fileName = "CartMovementProfile",
    menuName = "Scriptable Objects/Cart Movement Profile"
)]
public class CartMovementProfile : ScriptableObject
{
    #region Base Speed

    [Header("Base Speed")]
    [Tooltip("Base speed before chain-length penalty. Normal Drive uses this after chain adjustment.")]
    public float baseSpeed = 20f;

    [Tooltip("Lowest chain-adjusted base speed allowed.")]
    public float minimumBaseSpeed = 10f;

    [Tooltip("Additive speed bonus when the fuel speedup input is held. Final speedup target = adjusted base speed + this value.")]
    public float speedupAdditiveBonus = 5f;

    #endregion

    #region Mode Profiles

    [Header("Mode Profiles")]
    public CartMovementModeSettings normalDrive = CartMovementModeSettings.CreateNormalDrive();
    public CartMovementModeSettings drift = CartMovementModeSettings.CreateDrift();
    public CartMovementModeSettings speedup = CartMovementModeSettings.CreateSpeedup();

    #endregion

    #region Drift Feel

    [Header("Drift Feel")]
    public CartDriftFeelSettings driftFeel = CartDriftFeelSettings.CreateDefault();

    #endregion

    #region Mode / Powerup Interaction

    [Header("Mode / Powerup Interaction")]
    [Tooltip("If true, the separate powerup boost cannot be started while drifting.")]
    public bool blockPowerupBoostWhileDrifting = true;

    [Tooltip("If true, entering drift cancels the separate powerup boost coroutine.")]
    public bool cancelPowerupBoostWhenDriftStarts = true;

    [Tooltip("If true, SetSpeedToZero interrupts drift and forces the player to release drift before drifting again.")]
    public bool interruptDriftWhenSetSpeedToZero = true;

    #endregion

    public CartMovementModeSettings GetSettings(CartDriveMode mode)
    {
        switch (mode)
        {
            case CartDriveMode.Drift:
                return drift;

            case CartDriveMode.Speedup:
                return speedup;

            default:
                return normalDrive;
        }
    }

    private void OnValidate()
    {
        baseSpeed = Mathf.Max(0f, baseSpeed);
        minimumBaseSpeed = Mathf.Max(0f, minimumBaseSpeed);
        speedupAdditiveBonus = Mathf.Max(0f, speedupAdditiveBonus);

        normalDrive?.OnValidate();
        drift?.OnValidate();
        speedup?.OnValidate();
        driftFeel?.OnValidate();
    }
}

public enum CartDriveMode
{
    NormalDrive,
    Drift,
    Speedup
}

/// <summary>
/// Per-mode tuning values used by LeadingCartBehaviour.
///
/// Turn Speed Assist:
/// - This does not modify steering grip/counter-force.
/// - It only multiplies the forward engine torque cap during turns.
/// - Values above 1 help the cart recover speed faster during turns.
/// - Values below 1 intentionally make turns slower.
/// </summary>
[System.Serializable]
public class CartMovementModeSettings
{
    #region Turn Speed Assist

    [Header("Turn Speed Assist")]
    [Tooltip("Adds or removes engine authority while turning. This does not modify steering grip/counter-force.")]
    public bool enableTurnSpeedAssist = false;

    [Tooltip("Steer/requested angle treated as full turn intensity for this mode.")]
    public float turnAssistReferenceAngle = 45f;

    [Tooltip("0 means assist starts immediately. 0.2 means assist starts after 20% turn intensity.")]
    [Range(0f, 1f)]
    public float turnAssistStartIntensity = 0f;

    [Tooltip("1 = unchanged. 3 = up to 3x engine torque cap during intense turns. 0.5 = slower turns.")]
    public float maxTurnAssistTorqueMultiplier = 1f;

    #endregion

    public float GetTurnAssistTorqueMultiplier(float normalizedTurnIntensity)
    {
        if (!enableTurnSpeedAssist)
            return 1f;

        float assistIntensity = Mathf.InverseLerp(
            turnAssistStartIntensity,
            1f,
            Mathf.Clamp01(normalizedTurnIntensity)
        );

        return Mathf.Lerp(1f, maxTurnAssistTorqueMultiplier, assistIntensity);
    }

    public void OnValidate()
    {
        turnAssistReferenceAngle = Mathf.Max(1f, turnAssistReferenceAngle);
        maxTurnAssistTorqueMultiplier = Mathf.Max(0f, maxTurnAssistTorqueMultiplier);
    }

    public static CartMovementModeSettings CreateNormalDrive()
    {
        return new CartMovementModeSettings
        {
            enableTurnSpeedAssist = true,
            turnAssistReferenceAngle = 45f,
            turnAssistStartIntensity = 0f,
            maxTurnAssistTorqueMultiplier = 1.1f
        };
    }

    public static CartMovementModeSettings CreateDrift()
    {
        return new CartMovementModeSettings
        {
            enableTurnSpeedAssist = true,
            turnAssistReferenceAngle = 30f,
            turnAssistStartIntensity = 0f,
            maxTurnAssistTorqueMultiplier = 1.15f
        };
    }

    public static CartMovementModeSettings CreateSpeedup()
    {
        return new CartMovementModeSettings
        {
            enableTurnSpeedAssist = true,
            turnAssistReferenceAngle = 45f,
            turnAssistStartIntensity = 0f,
            maxTurnAssistTorqueMultiplier = 1.5f
        };
    }
}

/// <summary>
/// Drift-only speed-feel rules.
///
/// This replaces the old universal drift speed multiplier.
///
/// Design goals:
/// - Wide drift should still be allowed.
/// - Tight drift should have a clear moment of speed dip.
/// - If the player returns to wide drift, the speed loss should recover.
/// - Holding tight drift for a long time should slowly become less efficient.
/// </summary>
[System.Serializable]
public class CartDriftFeelSettings
{
    #region Tight Drift Dip

    [Header("Tight Drift Dip")]
    [Tooltip("If true, speed dips whenever drift tightness crosses into tight-drift range.")]
    public bool enableTightDriftDip = true;

    [Tooltip("Drift tightness value that triggers the dip. Example: 0.65 means dip starts when tightness reaches 65%.")]
    [Range(0f, 1f)]
    public float tightDriftDipTriggerTightness = 0.65f;

    [Tooltip("Dip can trigger again only after tightness drops below this value first.")]
    [Range(0f, 1f)]
    public float tightDriftDipRearmTightness = 0.35f;

    [Tooltip("How long the tight-drift dip lasts.")]
    public float tightDriftDipDuration = 0.18f;

    [Tooltip("Target speed multiplier while the tight-drift dip is active. Example: 0.9 = 90% of base speed.")]
    [Range(0.1f, 1f)]
    public float tightDriftDipMultiplier = 0.9f;

    #endregion

    #region Drift Fatigue / Sustained Speed Loss

    [Header("Drift Fatigue / Sustained Speed Loss")]
    [Tooltip("If true, sustained tight drifting gradually lowers target speed, but wide drifting can recover it.")]
    public bool enableDriftFatigueSpeedLoss = true;

    [Tooltip("Tightness where fatigue starts building. Below this, drift does not build speed fatigue.")]
    [Range(0f, 1f)]
    public float fatigueBuildStartTightness = 0.35f;

    [Tooltip("When tightness is below this value, drift fatigue recovers.")]
    [Range(0f, 1f)]
    public float fatigueRecoverBelowTightness = 0.25f;

    [Tooltip("Fatigue build rate per second at full tightness.")]
    public float fatigueBuildPerSecondAtFullTightness = 0.45f;

    [Tooltip("Fatigue recovery rate per second while wide drifting.")]
    public float fatigueRecoverPerSecondWhenWide = 0.75f;

    [Tooltip("X = fatigue 0..1. Y = target speed multiplier. Example: 0 fatigue = 1, full fatigue = 0.88.")]
    public AnimationCurve fatigueSpeedMultiplier = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.5f, 0.94f),
        new Keyframe(1f, 0.88f)
    );

    #endregion

    public float GetFatigueBuildAmount(float driftTightness, float deltaTime)
    {
        if (!enableDriftFatigueSpeedLoss)
            return 0f;

        float buildIntensity = Mathf.InverseLerp(
            fatigueBuildStartTightness,
            1f,
            Mathf.Clamp01(driftTightness)
        );

        return fatigueBuildPerSecondAtFullTightness * buildIntensity * deltaTime;
    }

    public float GetFatigueRecoverAmount(float driftTightness, float deltaTime)
    {
        if (!enableDriftFatigueSpeedLoss)
            return 0f;

        if (driftTightness > fatigueRecoverBelowTightness)
            return 0f;

        return fatigueRecoverPerSecondWhenWide * deltaTime;
    }

    public float GetFatigueSpeedMultiplier(float fatigue01)
    {
        if (!enableDriftFatigueSpeedLoss)
            return 1f;

        float value = fatigueSpeedMultiplier.Evaluate(Mathf.Clamp01(fatigue01));
        return Mathf.Clamp(value, 0.1f, 1f);
    }

    public void OnValidate()
    {
        tightDriftDipDuration = Mathf.Max(0f, tightDriftDipDuration);
        fatigueBuildPerSecondAtFullTightness = Mathf.Max(0f, fatigueBuildPerSecondAtFullTightness);
        fatigueRecoverPerSecondWhenWide = Mathf.Max(0f, fatigueRecoverPerSecondWhenWide);

        if (tightDriftDipRearmTightness > tightDriftDipTriggerTightness)
            tightDriftDipRearmTightness = tightDriftDipTriggerTightness;

        if (fatigueRecoverBelowTightness > fatigueBuildStartTightness)
            fatigueRecoverBelowTightness = fatigueBuildStartTightness;
    }

    public static CartDriftFeelSettings CreateDefault()
    {
        return new CartDriftFeelSettings
        {
            enableTightDriftDip = true,
            tightDriftDipTriggerTightness = 0.65f,
            tightDriftDipRearmTightness = 0.35f,
            tightDriftDipDuration = 0.18f,
            tightDriftDipMultiplier = 0.9f,

            enableDriftFatigueSpeedLoss = true,
            fatigueBuildStartTightness = 0.35f,
            fatigueRecoverBelowTightness = 0.25f,
            fatigueBuildPerSecondAtFullTightness = 0.45f,
            fatigueRecoverPerSecondWhenWide = 0.75f,
            fatigueSpeedMultiplier = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.5f, 0.94f),
                new Keyframe(1f, 0.88f)
            )
        };
    }
}