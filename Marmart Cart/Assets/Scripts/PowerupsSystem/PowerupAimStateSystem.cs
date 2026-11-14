using System;
using UnityEngine;

/// <summary>
/// Four-slot semantic store shared by per-player targeting controllers and the
/// single Shapes renderer. It contains no input or drawing logic.
/// </summary>
[DisallowMultipleComponent]
public class PowerupAimStateSystem : MonoBehaviour
{
    [SerializeField]
    private PowerupAimState[] states =
        new PowerupAimState[PowerupRuntimeSystem.MaxPlayerSlots];

    public event Action<int, PowerupAimState> OnAimStateChanged;

    private void Awake()
    {
        EnsureFourStates();
    }

    private void OnValidate()
    {
        EnsureFourStates();
    }

    public PowerupAimState PublishState(int playerIndex, PowerupAimState state)
    {
        if (!TryGetArrayIndex(playerIndex, out int stateIndex)) return state;

        EnsureFourStates();

        state.PlayerIndex = playerIndex;
        state.Revision = states[stateIndex].Revision + 1u;
        states[stateIndex] = state;

        OnAimStateChanged?.Invoke(playerIndex, state);
        return state;
    }

    public void ClearState(int playerIndex)
    {
        if (!TryGetArrayIndex(playerIndex, out int stateIndex)) return;

        EnsureFourStates();
        PowerupAimState previous = states[stateIndex];

        if (!previous.Visible && previous.PlayerIndex == playerIndex)
        {
            return;
        }

        PowerupAimState cleared = new PowerupAimState
        {
            PlayerIndex = playerIndex,
            Revision = previous.Revision + 1u
        };

        states[stateIndex] = cleared;
        OnAimStateChanged?.Invoke(playerIndex, cleared);
    }

    public bool TryGetState(int playerIndex, out PowerupAimState state)
    {
        state = default;
        if (!TryGetArrayIndex(playerIndex, out int stateIndex)) return false;

        EnsureFourStates();
        state = states[stateIndex];
        return true;
    }

    private void EnsureFourStates()
    {
        int requiredLength = PowerupRuntimeSystem.MaxPlayerSlots;
        if (states != null && states.Length == requiredLength) return;

        PowerupAimState[] resized = new PowerupAimState[requiredLength];

        if (states != null)
        {
            Array.Copy(states, resized, Mathf.Min(states.Length, resized.Length));
        }

        states = resized;
    }

    private static bool TryGetArrayIndex(int playerIndex, out int stateIndex)
    {
        stateIndex = playerIndex - 1;
        return stateIndex >= 0 &&
               stateIndex < PowerupRuntimeSystem.MaxPlayerSlots;
    }
}
