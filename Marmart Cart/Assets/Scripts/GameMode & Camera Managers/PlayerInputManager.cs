using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns local player-root activation and input-device assignment.
///
/// IMPORTANT:
/// This script does not decide whether the match is 2P or 4P.
/// MatchSceneModeController supplies the required player count from GMode.
///
/// Runtime-instantiated leading carts are supported: after the player roots are
/// activated, this manager waits until each CartControlScript actually exists
/// before assigning that player's input device.
/// </summary>
[DisallowMultipleComponent]
public class PlayerInputManager : MonoBehaviour
{
    public const int MaxPlayers = 4;

    #region Player References

    [Header("Player Roots")]
    [SerializeField] private GameObject player1;
    [SerializeField] private GameObject player2;
    [SerializeField] private GameObject player3;
    [SerializeField] private GameObject player4;

    #endregion

    #region Input Fallback

    [Header("Input Fallback")]
    [Tooltip(
        "If there are fewer gamepads than active players, assign keyboard input " +
        "to the remaining players. Current project behavior allows multiple players " +
        "to share that keyboard fallback."
    )]
    [SerializeField] private bool enableKeyboardInput = true;

    #endregion

    #region Runtime Binding

    [Header("Runtime Cart Binding")]
    [Tooltip(
        "How often to retry finding CartControlScript on runtime-instantiated leading carts. " +
        "This is a coroutine retry, not per-frame Update polling."
    )]
    [Min(0.01f)]
    [SerializeField] private float bindingRetryIntervalSeconds = 0.05f;

    [Tooltip(
        "How long to wait for runtime CartControlScripts before reporting an error. " +
        "Set to 0 to wait forever."
    )]
    [Min(0f)]
    [SerializeField] private float bindingTimeoutSeconds = 10f;

    [SerializeField] private bool logSuccessfulBindings = false;

    #endregion

    #region Runtime

    [Header("Runtime - Read Only")]
    [SerializeField] private int configuredPlayerCount;
    [SerializeField] private int boundPlayerCount;

    private readonly CartControlScript[] initializedControls = new CartControlScript[MaxPlayers];
    private readonly bool[] bindingComplete = new bool[MaxPlayers];

    private Coroutine bindingRoutine;

    public int ConfiguredPlayerCount => configuredPlayerCount;
    public int BoundPlayerCount => boundPlayerCount;
    public bool AllConfiguredPlayersBound => configuredPlayerCount > 0 && boundPlayerCount >= configuredPlayerCount;

    #endregion

    #region Configuration

    /// <summary>
    /// Called by MatchSceneModeController after reading the authoritative GMode.
    ///
    /// Immediate work:
    /// - activates only the player roots required by this mode;
    /// - disables unused player roots.
    ///
    /// Deferred work:
    /// - waits for runtime-instantiated CartControlScripts;
    /// - assigns the correct input device when each one appears.
    /// </summary>
    public void ConfigureForPlayerCount(int requestedPlayerCount)
    {
        int neededPlayers = requestedPlayerCount <= 2 ? 2 : 4;

        StopBindingRoutine();
        CleanupInitializedInputs();
        ResetBindingState();

        configuredPlayerCount = neededPlayers;

        GameObject[] players = GetPlayerArray();

        // Establish the correct scene population immediately. This needs to
        // happen before runtime player/cart spawners create their leading carts.
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null)
            {
                if (i < neededPlayers)
                {
                    Debug.LogError(
                        $"[PlayerInputManager] Player {i + 1} root reference is missing.",
                        this
                    );
                }

                continue;
            }

            players[i].SetActive(i < neededPlayers);
        }

        StartBindingRoutine();
    }

    #endregion

    #region Runtime Binding

    private void StartBindingRoutine()
    {
        if (!isActiveAndEnabled || configuredPlayerCount <= 0) return;

        bindingRoutine = StartCoroutine(BindRuntimeCartControlsRoutine());
    }

    private void StopBindingRoutine()
    {
        if (bindingRoutine == null) return;

        StopCoroutine(bindingRoutine);
        bindingRoutine = null;
    }

    private IEnumerator BindRuntimeCartControlsRoutine()
    {
        float startedAt = Time.realtimeSinceStartup;
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(
            Mathf.Max(0.01f, bindingRetryIntervalSeconds)
        );

        while (boundPlayerCount < configuredPlayerCount)
        {
            for (int slot = 0; slot < configuredPlayerCount; slot++)
            {
                if (bindingComplete[slot]) continue;

                TryBindPlayerSlot(slot);
            }

            if (boundPlayerCount >= configuredPlayerCount)
            {
                break;
            }

            if (bindingTimeoutSeconds > 0f &&
                Time.realtimeSinceStartup - startedAt >= bindingTimeoutSeconds)
            {
                ReportUnresolvedBindings();
                bindingRoutine = null;
                yield break;
            }

            yield return wait;
        }

        bindingRoutine = null;
    }

    private void TryBindPlayerSlot(int slot)
    {
        int playerIndex = slot + 1;
        GameObject playerRoot = GetPlayerRoot(playerIndex);

        if (playerRoot == null || !playerRoot.activeInHierarchy)
        {
            return;
        }

        CartControlScript cartControl = GetCartControl(playerRoot);

        // This is expected during startup because the leading cart can be
        // instantiated after MatchSceneModeController has already configured
        // the player roots. Just retry quietly.
        if (cartControl == null)
        {
            return;
        }

        var gamepads = Gamepad.all;

        if (slot < gamepads.Count)
        {
            cartControl.InitializeWithDevice(gamepads[slot]);
            CompleteBinding(slot, cartControl, $"gamepad {slot + 1}");
            return;
        }

        if (enableKeyboardInput)
        {
            cartControl.InitializeWithKeyboard();
            CompleteBinding(slot, cartControl, "keyboard fallback");
            return;
        }

        Debug.LogWarning(
            $"[PlayerInputManager] No input device available for Player {playerIndex}, " +
            "and keyboard fallback is disabled. Disabling that player root.",
            playerRoot
        );

        playerRoot.SetActive(false);
        bindingComplete[slot] = true;
        boundPlayerCount++;
    }

    private void CompleteBinding(
        int slot,
        CartControlScript cartControl,
        string inputDescription)
    {
        initializedControls[slot] = cartControl;
        bindingComplete[slot] = true;
        boundPlayerCount++;

        if (logSuccessfulBindings)
        {
            Debug.Log(
                $"[PlayerInputManager] Player {slot + 1} runtime cart bound to {inputDescription}.",
                cartControl
            );
        }
    }

    private void ReportUnresolvedBindings()
    {
        for (int slot = 0; slot < configuredPlayerCount; slot++)
        {
            if (bindingComplete[slot]) continue;

            GameObject playerRoot = GetPlayerRoot(slot + 1);

            Debug.LogError(
                $"[PlayerInputManager] Timed out waiting for Player {slot + 1} " +
                "runtime CartControlScript. Check that the leading cart is spawned " +
                "under the assigned player root.",
                playerRoot != null ? (UnityEngine.Object)playerRoot : this
            );
        }
    }

    #endregion

    #region Public Read API

    public GameObject GetPlayerRoot(int playerIndex)
    {
        return playerIndex switch
        {
            1 => player1,
            2 => player2,
            3 => player3,
            4 => player4,
            _ => null
        };
    }

    public CartControlScript GetBoundCartControl(int playerIndex)
    {
        if (playerIndex < 1 || playerIndex > MaxPlayers) return null;
        return initializedControls[playerIndex - 1];
    }

    #endregion

    #region Helpers

    private GameObject[] GetPlayerArray()
    {
        return new[]
        {
            player1,
            player2,
            player3,
            player4
        };
    }

    private CartControlScript GetCartControl(GameObject playerRoot)
    {
        if (playerRoot == null) return null;

        // Startup-only hierarchy search. The CartControlScript lives on the
        // leading cart, which may not exist yet when scene configuration begins.
        return playerRoot.GetComponentInChildren<CartControlScript>(true);
    }

    private void ResetBindingState()
    {
        boundPlayerCount = 0;

        for (int i = 0; i < MaxPlayers; i++)
        {
            bindingComplete[i] = false;
        }
    }

    private void CleanupInitializedInputs()
    {
        for (int i = 0; i < initializedControls.Length; i++)
        {
            if (initializedControls[i] == null) continue;

            initializedControls[i].CleanupInput();
            initializedControls[i] = null;
        }
    }

    #endregion

    #region Unity Lifetime

    private void OnEnable()
    {
        // Handles the uncommon case where this component is enabled after the
        // scene mode was already configured.
        if (configuredPlayerCount > 0 && bindingRoutine == null && !AllConfiguredPlayersBound)
        {
            StartBindingRoutine();
        }
    }

    private void OnDisable()
    {
        StopBindingRoutine();
    }

    private void OnDestroy()
    {
        StopBindingRoutine();
        CleanupInitializedInputs();
    }

    private void OnValidate()
    {
        bindingRetryIntervalSeconds = Mathf.Max(0.01f, bindingRetryIntervalSeconds);
        bindingTimeoutSeconds = Mathf.Max(0f, bindingTimeoutSeconds);
    }

    #endregion
}
