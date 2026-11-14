using System;
using UnityEngine;

/// <summary>
/// Inspector sandbox for PlayerWorldHUDStateSystem.
///
/// Lets UI design testing simulate:
/// - Hype
/// - drift success reward preview
/// - drift failure penalty preview
/// - Load
/// - speed consequence
/// - streak
/// </summary>
[DisallowMultipleComponent]
public class PlayerWorldHUDDebugStateController : MonoBehaviour
{
    [Serializable]
    private sealed class DebugPlayerState
    {
        [Header("Debug Slot")]
        public bool enabled = true;

        [Header("Hype")]
        [Min(0f)] public float currentHype = 65f;
        [Min(0f)] public float maxHype = 100f;

        [Tooltip("1 = normal burn. 1.5 = 50% higher than baseline.")]
        [Min(0f)]
        public float hypeBurnMultiplier = 1f;

        [Min(0f)]
        public float hypeBurnPerSecond = 100f;

        public bool isSpeedingUp;

        [Header("Potential Drift Success")]
        public bool driftRewardPreviewActive = true;

        [Tooltip("Potential Hype earned if the current drift succeeds.")]
        [Min(0f)]
        public float potentialDriftHypeReward = 15f;

        [Header("Potential Drift Failure")]
        public bool driftPenaltyPreviewActive = true;

        [Tooltip(
            "If true, the debug penalty automatically equals Success Reward × Penalty Fraction."
        )]
        public bool autoCalculatePenaltyFromReward = true;

        [Range(0f, 1f)]
        public float driftPenaltyFraction = 0.5f;

        [Tooltip(
            "Used only when Auto Calculate Penalty From Reward is false."
        )]
        [Min(0f)]
        public float potentialDriftHypePenalty = 7.5f;

        [Header("Load")]
        [Min(0f)] public float currentLoad = 7f;
        [Min(0f)] public float safeCapacity = 10f;
        [Min(0f)] public float overloadAmount;

        [Header("Speed Consequence")]
        [Tooltip("1 = no max-speed penalty. 0.7 = max speed is 70% of normal.")]
        [Min(0f)]
        public float maxSpeedMultiplier = 1f;

        [Header("Checkout / Streak")]
        public bool checkoutStreakEligible;
        [Min(0)] public int checkoutStreakLevel;
        [Range(0f, 1f)] public float checkoutStreakProgressNormalized;

        public float GetMeaningfulDebugRewardAmount()
        {
            if (!driftRewardPreviewActive)
            {
                return 0f;
            }

            float availableHypeRoom =
                Mathf.Max(
                    0f,
                    maxHype - currentHype
                );

            return Mathf.Min(
                Mathf.Max(
                    0f,
                    potentialDriftHypeReward
                ),
                availableHypeRoom
            );
        }

        public float GetDebugPenaltyAmount()
        {
            if (!driftPenaltyPreviewActive)
            {
                return 0f;
            }

            if (autoCalculatePenaltyFromReward)
            {
                return Mathf.Max(
                    0f,
                    GetMeaningfulDebugRewardAmount() *
                    driftPenaltyFraction
                );
            }

            return Mathf.Max(
                0f,
                potentialDriftHypePenalty
            );
        }
    }

    [Header("Target")]
    [SerializeField]
    private PlayerWorldHUDStateSystem stateSystem;

    [Header("Debug Update")]
    [SerializeField]
    private bool applyContinuously = true;

    [Tooltip("Debug-only update interval using unscaled time.")]
    [Min(0.02f)]
    [SerializeField]
    private float applyInterval = 0.1f;

    [Header("Player Debug States")]
    [SerializeField]
    private DebugPlayerState[] players =
        new DebugPlayerState[
            PlayerWorldHUDStateSystem.MaxPlayerSlots
        ];

    private float nextApplyTime;

    private void Reset()
    {
        EnsureFourDebugStates();
    }

    private void Awake()
    {
        EnsureFourDebugStates();

        if (stateSystem == null)
        {
            stateSystem =
                FindFirstObjectByType<
                    PlayerWorldHUDStateSystem
                >();
        }
    }

    private void Start()
    {
        ApplyAllDebugStates();
    }

    private void Update()
    {
        if (!applyContinuously)
        {
            return;
        }

        if (Time.unscaledTime < nextApplyTime)
        {
            return;
        }

        nextApplyTime =
            Time.unscaledTime +
            applyInterval;

        ApplyAllDebugStates();
    }

    private void OnValidate()
    {
        applyInterval =
            Mathf.Max(
                0.02f,
                applyInterval
            );

        EnsureFourDebugStates();

        for (int i = 0; i < players.Length; i++)
        {
            DebugPlayerState state =
                players[i];

            if (state == null)
            {
                continue;
            }

            state.currentHype =
                Mathf.Max(
                    0f,
                    state.currentHype
                );

            state.maxHype =
                Mathf.Max(
                    0f,
                    state.maxHype
                );

            state.hypeBurnMultiplier =
                Mathf.Max(
                    0f,
                    state.hypeBurnMultiplier
                );

            state.hypeBurnPerSecond =
                Mathf.Max(
                    0f,
                    state.hypeBurnPerSecond
                );

            state.potentialDriftHypeReward =
                Mathf.Max(
                    0f,
                    state.potentialDriftHypeReward
                );

            state.driftPenaltyFraction =
                Mathf.Clamp01(
                    state.driftPenaltyFraction
                );

            state.potentialDriftHypePenalty =
                Mathf.Max(
                    0f,
                    state.potentialDriftHypePenalty
                );

            state.currentLoad =
                Mathf.Max(
                    0f,
                    state.currentLoad
                );

            state.safeCapacity =
                Mathf.Max(
                    0f,
                    state.safeCapacity
                );

            state.overloadAmount =
                Mathf.Max(
                    0f,
                    state.overloadAmount
                );

            state.maxSpeedMultiplier =
                Mathf.Max(
                    0f,
                    state.maxSpeedMultiplier
                );

            state.checkoutStreakLevel =
                Mathf.Max(
                    0,
                    state.checkoutStreakLevel
                );

            state.checkoutStreakProgressNormalized =
                Mathf.Clamp01(
                    state.checkoutStreakProgressNormalized
                );
        }
    }

    [ContextMenu("Apply All Debug HUD States Now")]
    public void ApplyAllDebugStates()
    {
        if (stateSystem == null)
        {
            return;
        }

        EnsureFourDebugStates();

        for (
            int i = 0;
            i < PlayerWorldHUDStateSystem.MaxPlayerSlots;
            i++)
        {
            DebugPlayerState debug =
                players[i];

            if (debug == null ||
                !debug.enabled)
            {
                continue;
            }

            int playerIndex = i + 1;

            stateSystem.SetHype(
                playerIndex,
                debug.currentHype,
                debug.maxHype,
                debug.hypeBurnMultiplier,
                debug.hypeBurnPerSecond,
                debug.isSpeedingUp
            );

            stateSystem.SetDriftRewardPreview(
                playerIndex,
                debug.driftRewardPreviewActive,
                debug.GetMeaningfulDebugRewardAmount()
            );

            stateSystem.SetDriftPenaltyPreview(
                playerIndex,
                debug.driftPenaltyPreviewActive,
                debug.GetDebugPenaltyAmount()
            );

            stateSystem.SetLoad(
                playerIndex,
                debug.currentLoad,
                debug.safeCapacity,
                debug.overloadAmount
            );

            stateSystem.SetMaxSpeedMultiplier(
                playerIndex,
                debug.maxSpeedMultiplier
            );

            stateSystem.SetCheckoutStreak(
                playerIndex,
                debug.checkoutStreakEligible,
                debug.checkoutStreakLevel,
                debug.checkoutStreakProgressNormalized
            );
        }
    }

    private void EnsureFourDebugStates()
    {
        int count =
            PlayerWorldHUDStateSystem.MaxPlayerSlots;

        if (players == null ||
            players.Length != count)
        {
            DebugPlayerState[] old =
                players;

            players =
                new DebugPlayerState[count];

            if (old != null)
            {
                int copyCount =
                    Mathf.Min(
                        old.Length,
                        count
                    );

                for (int i = 0; i < copyCount; i++)
                {
                    players[i] =
                        old[i];
                }
            }
        }

        for (int i = 0; i < count; i++)
        {
            if (players[i] == null)
            {
                players[i] =
                    new DebugPlayerState();
            }
        }
    }
}
