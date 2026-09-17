using System;
using UnityEngine;

/// <summary>
/// Scene-level registry and match-lifecycle gate for the replacement power-up
/// system. It owns no inventory and implements no individual power-up effect.
/// Runtime player controllers register themselves by player index.
/// </summary>
[DefaultExecutionOrder(-400)]
[DisallowMultipleComponent]
public class PowerupRuntimeSystem : MonoBehaviour
{
    public const int MaxPlayerSlots = 4;

    #region References

    [Header("References")]
    [Tooltip("Optional explicit reference. Automatically resolved when left empty.")]
    [SerializeField] private GameTimeManager gameTimeManager;

    #endregion

    #region Isolated Scene Testing

    [Header("Isolated Scene Testing")]
    [Tooltip(
        "When no GameTimeManager exists, allow power-up use so isolated prefab/test scenes remain usable. " +
        "Normal gameplay scenes should always contain GameTimeManager."
    )]
    [SerializeField] private bool allowUseWhenGameTimeManagerIsMissing = true;

    #endregion

    #region Runtime

    [Header("Runtime - Read Only")]
    [SerializeField] private bool matchPlaying;
    [SerializeField] private int registeredPlayerCount;

    private readonly PlayerPowerupController[] registeredPlayers =
        new PlayerPowerupController[MaxPlayerSlots];

    private GameTimeManager subscribedGameTimeManager;
    private bool missingGameTimeManagerLogged;

    public bool MatchPlaying => matchPlaying;
    public int RegisteredPlayerCount => registeredPlayerCount;

    public event Action<int, PlayerPowerupController> OnPlayerRegistered;
    public event Action<int> OnPlayerUnregistered;
    public event Action<bool> OnMatchPlayingChanged;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveGameTimeManager();
        RefreshMatchStateFromSource();
    }

    private void OnEnable()
    {
        ResolveGameTimeManager();
        SubscribeToGameTimeManager();
        RefreshMatchStateFromSource();
    }

    private void Start()
    {
        // GameTimeManager establishes its initial PreGame state in Start.
        // Reading again here makes the initial blocker deterministic regardless
        // of scene object execution order.
        ResolveGameTimeManager();
        SubscribeToGameTimeManager();
        RefreshMatchStateFromSource();
    }

    private void OnDisable()
    {
        UnsubscribeFromGameTimeManager();
        SetMatchPlaying(false);
    }

    #endregion

    #region Player Registry

    public bool RegisterPlayer(PlayerPowerupController controller)
    {
        if (controller == null) return false;

        int playerIndex = controller.PlayerIndex;
        int slotIndex = playerIndex - 1;

        if (slotIndex < 0 || slotIndex >= MaxPlayerSlots)
        {
            Debug.LogError(
                $"[PowerupRuntimeSystem] Cannot register invalid player index {playerIndex}. Expected 1..{MaxPlayerSlots}.",
                controller
            );
            return false;
        }

        PlayerPowerupController existing = registeredPlayers[slotIndex];

        if (existing != null && existing != controller)
        {
            Debug.LogError(
                $"[PowerupRuntimeSystem] Player {playerIndex} already has a different registered controller '{existing.name}'.",
                this
            );
            return false;
        }

        // Runtime player identity may settle after this controller first
        // enables. Move the same controller out of any earlier slot before
        // completing the new registration.
        for (int i = 0; i < registeredPlayers.Length; i++)
        {
            if (i == slotIndex || registeredPlayers[i] != controller) continue;

            registeredPlayers[i] = null;
            OnPlayerUnregistered?.Invoke(i + 1);
        }

        bool newlyRegistered = existing == null;
        registeredPlayers[slotIndex] = controller;

        if (newlyRegistered)
        {
            OnPlayerRegistered?.Invoke(playerIndex, controller);
        }

        RecalculateRegisteredPlayerCount();

        controller.SetUseBlocked(
            PowerupUseBlockReason.MatchInactive,
            !matchPlaying
        );

        return true;
    }

    public void UnregisterPlayer(PlayerPowerupController controller)
    {
        if (controller == null) return;

        bool removedAny = false;

        for (int i = 0; i < registeredPlayers.Length; i++)
        {
            if (registeredPlayers[i] != controller) continue;

            registeredPlayers[i] = null;
            removedAny = true;
            OnPlayerUnregistered?.Invoke(i + 1);
        }

        if (!removedAny) return;
        RecalculateRegisteredPlayerCount();
    }

    public bool TryGetPlayer(
        int playerIndex,
        out PlayerPowerupController controller)
    {
        int slotIndex = playerIndex - 1;

        if (slotIndex < 0 || slotIndex >= MaxPlayerSlots)
        {
            controller = null;
            return false;
        }

        controller = registeredPlayers[slotIndex];
        return controller != null;
    }

    public bool TryGetPlayerForCartControl(
        CartControlScript cartControlInput,
        out PlayerPowerupController controller)
    {
        if (cartControlInput == null)
        {
            controller = null;
            return false;
        }

        for (int i = 0; i < registeredPlayers.Length; i++)
        {
            PlayerPowerupController candidate = registeredPlayers[i];

            if (candidate != null &&
                candidate.CartControlInput == cartControlInput)
            {
                controller = candidate;
                return true;
            }
        }

        controller = null;
        return false;
    }

    private void RecalculateRegisteredPlayerCount()
    {
        int count = 0;

        for (int i = 0; i < registeredPlayers.Length; i++)
        {
            if (registeredPlayers[i] != null) count++;
        }

        registeredPlayerCount = count;
    }

    #endregion

    #region Match Lifecycle

    [ContextMenu("Refresh Match State")]
    public void RefreshMatchStateFromSource()
    {
        ResolveGameTimeManager();

        if (gameTimeManager == null)
        {
            SetMatchPlaying(allowUseWhenGameTimeManagerIsMissing);

            if (!missingGameTimeManagerLogged)
            {
                missingGameTimeManagerLogged = true;
                Debug.LogWarning(
                    "[PowerupRuntimeSystem] GameTimeManager was not found. " +
                    $"Power-up use is {(allowUseWhenGameTimeManagerIsMissing ? "allowed" : "blocked")} for isolated-scene testing.",
                    this
                );
            }

            return;
        }

        missingGameTimeManagerLogged = false;
        SetMatchPlaying(gameTimeManager.IsPlaying);
    }

    private void SetMatchPlaying(bool playing)
    {
        bool stateChanged = matchPlaying != playing;
        matchPlaying = playing;

        for (int i = 0; i < registeredPlayers.Length; i++)
        {
            PlayerPowerupController controller = registeredPlayers[i];
            if (controller == null) continue;

            controller.SetUseBlocked(
                PowerupUseBlockReason.MatchInactive,
                !matchPlaying
            );
        }

        if (stateChanged)
        {
            OnMatchPlayingChanged?.Invoke(matchPlaying);
        }
    }

    private void HandlePreGameEntered()
    {
        SetMatchPlaying(false);
    }

    private void HandleMatchStarted()
    {
        SetMatchPlaying(true);
    }

    private void HandlePostGameEntered()
    {
        SetMatchPlaying(false);
    }

    #endregion

    #region GameTimeManager Binding

    private void ResolveGameTimeManager()
    {
        if (gameTimeManager == null)
        {
            gameTimeManager = FindFirstObjectByType<GameTimeManager>();
        }
    }

    private void SubscribeToGameTimeManager()
    {
        if (subscribedGameTimeManager == gameTimeManager) return;

        UnsubscribeFromGameTimeManager();
        subscribedGameTimeManager = gameTimeManager;

        if (subscribedGameTimeManager == null) return;

        subscribedGameTimeManager.OnPreGameEntered += HandlePreGameEntered;
        subscribedGameTimeManager.OnMatchStarted += HandleMatchStarted;
        subscribedGameTimeManager.OnPostGameEntered += HandlePostGameEntered;
    }

    private void UnsubscribeFromGameTimeManager()
    {
        if (subscribedGameTimeManager == null) return;

        subscribedGameTimeManager.OnPreGameEntered -= HandlePreGameEntered;
        subscribedGameTimeManager.OnMatchStarted -= HandleMatchStarted;
        subscribedGameTimeManager.OnPostGameEntered -= HandlePostGameEntered;
        subscribedGameTimeManager = null;
    }

    #endregion
}
