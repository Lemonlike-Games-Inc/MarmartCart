using System;
using UnityEngine;

[Flags]
public enum PlayerWorldHUDStateChange
{
    None = 0,
    Hype = 1 << 0,
    DriftReward = 1 << 1,
    Load = 1 << 2,
    Speed = 1 << 3,
    Streak = 1 << 4,
    DriftPenalty = 1 << 5,
    MoveBackward = 1 << 6,

    All =
        Hype |
        DriftReward |
        DriftPenalty |
        Load |
        Speed |
        Streak |
        MoveBackward
}

/// <summary>
/// NEW SCRIPT.
///
/// Stores semantic HUD state for Player 1-4.
///
/// This is intentionally separate from PlayerWorldHUDSystem:
///
/// PlayerWorldHUDSystem
///     = runtime player/camera/HUDWorldAnchor binding.
///
/// PlayerWorldHUDStateSystem
///     = gameplay values the HUD should display.
///
/// The renderer reads BOTH systems:
///     HUDSystem tells it WHO/WHERE to draw.
///     HUDStateSystem tells it WHAT to draw.
/// </summary>
[DisallowMultipleComponent]
public class PlayerWorldHUDStateSystem : MonoBehaviour
{
    public const int MaxPlayerSlots = 4;

    [Header("Runtime Player HUD State")]
    [SerializeField]
    private PlayerWorldHUDState[] playerStates =
        new PlayerWorldHUDState[MaxPlayerSlots];

    public event Action<int, PlayerWorldHUDStateChange> OnStateChanged;

    #region Unity

    private void Reset()
    {
        EnsureFourStates();
        ResetAllStates();
    }

    private void Awake()
    {
        EnsureFourStates();
    }

    private void OnValidate()
    {
        EnsureFourStates();
    }

    #endregion

    #region Read API

    public PlayerWorldHUDState GetState(int playerIndex)
    {
        if (!TryGetStateIndex(playerIndex, out int index))
        {
            return null;
        }

        return playerStates[index];
    }

    public bool TryGetState(
        int playerIndex,
        out PlayerWorldHUDState state)
    {
        state = null;

        if (!TryGetStateIndex(playerIndex, out int index))
        {
            return false;
        }

        state = playerStates[index];
        return state != null;
    }

    #endregion

    #region Hype

    public void SetHype(
        int playerIndex,
        float currentHype,
        float maxHype,
        float burnMultiplier,
        float burnPerSecond,
        bool isSpeedingUp)
    {
        if (!TryGetStateIndex(playerIndex, out int index))
        {
            return;
        }

        playerStates[index].SetHype(
            currentHype,
            maxHype,
            burnMultiplier,
            burnPerSecond,
            isSpeedingUp
        );

        OnStateChanged?.Invoke(
            playerIndex,
            PlayerWorldHUDStateChange.Hype
        );
    }

    #endregion

    #region Drift Reward

    public void SetDriftRewardPreview(
        int playerIndex,
        bool active,
        float potentialReward)
    {
        if (!TryGetStateIndex(playerIndex, out int index))
        {
            return;
        }

        playerStates[index].SetDriftRewardPreview(
            active,
            potentialReward
        );

        OnStateChanged?.Invoke(
            playerIndex,
            PlayerWorldHUDStateChange.DriftReward
        );
    }

    public void ClearDriftRewardPreview(
        int playerIndex)
    {
        if (!TryGetStateIndex(playerIndex, out int index))
        {
            return;
        }

        playerStates[index].ClearDriftRewardPreview();

        OnStateChanged?.Invoke(
            playerIndex,
            PlayerWorldHUDStateChange.DriftReward
        );
    }

    #endregion

    #region Drift Penalty

    public void SetDriftPenaltyPreview(
        int playerIndex,
        bool active,
        float potentialPenalty)
    {
        if (!TryGetStateIndex(playerIndex, out int index))
        {
            return;
        }

        OnStateChanged?.Invoke(
            playerIndex,
            PlayerWorldHUDStateChange.DriftPenalty
        );
    }

    public void ClearDriftPenaltyPreview(
        int playerIndex)
    {
        if (!TryGetStateIndex(playerIndex, out int index))
        {
            return;
        }

        OnStateChanged?.Invoke(
            playerIndex,
            PlayerWorldHUDStateChange.DriftPenalty
        );
    }

    public void ClearAllDriftPreviews(
        int playerIndex)
    {
        if (!TryGetStateIndex(playerIndex, out int index))
        {
            return;
        }

        OnStateChanged?.Invoke(
            playerIndex,
            PlayerWorldHUDStateChange.DriftReward |
            PlayerWorldHUDStateChange.DriftPenalty
        );
    }

    #endregion

    #region Load

    public void SetLoad(
        int playerIndex,
        float currentLoad,
        float safeCapacity,
        float overloadAmount)
    {
        if (!TryGetStateIndex(playerIndex, out int index))
        {
            return;
        }

        playerStates[index].SetLoad(
            currentLoad,
            safeCapacity,
            overloadAmount
        );

        OnStateChanged?.Invoke(
            playerIndex,
            PlayerWorldHUDStateChange.Load
        );
    }

    #endregion

    #region Speed

    public void SetMaxSpeedMultiplier(
        int playerIndex,
        float multiplier)
    {
        if (!TryGetStateIndex(playerIndex, out int index))
        {
            return;
        }

        playerStates[index].SetMaxSpeedMultiplier(
            multiplier
        );

        OnStateChanged?.Invoke(
            playerIndex,
            PlayerWorldHUDStateChange.Speed
        );
    }

    #endregion

    #region Control Prompts

    public void SetCanMoveBackward(
        int playerIndex,
        bool canMoveBackward)
    {
        if (!TryGetStateIndex(playerIndex, out int index))
        {
            return;
        }

        PlayerWorldHUDState state = playerStates[index];

        if (state.CanMoveBackward == canMoveBackward)
        {
            return;
        }

        state.SetCanMoveBackward(canMoveBackward);

        OnStateChanged?.Invoke(
            playerIndex,
            PlayerWorldHUDStateChange.MoveBackward
        );
    }

    #endregion

    #region Checkout / Streak

    public void SetCheckoutStreak(
        int playerIndex,
        bool eligible,
        int streakLevel,
        float progressNormalized)
    {
        if (!TryGetStateIndex(playerIndex, out int index))
        {
            return;
        }

        playerStates[index].SetCheckoutStreak(
            eligible,
            streakLevel,
            progressNormalized
        );

        OnStateChanged?.Invoke(
            playerIndex,
            PlayerWorldHUDStateChange.Streak
        );
    }

    #endregion

    #region Reset

    public void ResetPlayerState(
        int playerIndex)
    {
        if (!TryGetStateIndex(playerIndex, out int index))
        {
            return;
        }

        playerStates[index].ResetState();

        OnStateChanged?.Invoke(
            playerIndex,
            PlayerWorldHUDStateChange.All
        );
    }

    [ContextMenu("Reset All HUD States")]
    public void ResetAllStates()
    {
        EnsureFourStates();

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            playerStates[i].ResetState();

            OnStateChanged?.Invoke(
                i + 1,
                PlayerWorldHUDStateChange.All
            );
        }
    }

    #endregion

    #region Internal

    private bool TryGetStateIndex(
        int playerIndex,
        out int index)
    {
        index = playerIndex - 1;

        if (index >= 0 &&
            index < MaxPlayerSlots)
        {
            return true;
        }

        Debug.LogError(
            $"[PlayerWorldHUDStateSystem] Player index {playerIndex} is invalid. Expected 1..{MaxPlayerSlots}.",
            this
        );

        index = -1;
        return false;
    }

    private void EnsureFourStates()
    {
        if (playerStates == null ||
            playerStates.Length != MaxPlayerSlots)
        {
            PlayerWorldHUDState[] oldStates =
                playerStates;

            playerStates =
                new PlayerWorldHUDState[
                    MaxPlayerSlots
                ];

            if (oldStates != null)
            {
                int copyCount =
                    Mathf.Min(
                        oldStates.Length,
                        MaxPlayerSlots
                    );

                for (int i = 0; i < copyCount; i++)
                {
                    playerStates[i] =
                        oldStates[i];
                }
            }
        }

        for (int i = 0; i < MaxPlayerSlots; i++)
        {
            if (playerStates[i] == null)
            {
                playerStates[i] =
                    new PlayerWorldHUDState();
            }
        }
    }

    #endregion
}
