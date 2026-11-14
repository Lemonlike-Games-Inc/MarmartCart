using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Gameplay-scene debug shortcuts.
///
/// Keyboard only:
/// - R    : reload current gameplay scene.
/// - 1..4 : teleport/recover that player to the assigned debug point.
///
/// Controller only:
/// - Hold Y / Button North on ANY single gamepad for the configured duration
///   to return to Main Menu.
/// - Hold timers are per-device. Two controllers cannot combine progress.
///
/// This is intentionally a scene-local debug helper.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameplayDebugger : MonoBehaviour
{
    private const int MaxPlayers = 4;

    #region Feature Toggles

    [Header("Listeners")]
    [SerializeField] private bool listenForSceneReload = true;
    [SerializeField] private bool listenForPlayerTeleport = true;
    [SerializeField] private bool listenForReturnToMenu = true;

    #endregion

    #region Scene Reload

    [Header("Reload Current Scene")]
    [Tooltip("Keyboard R only.")]
    [SerializeField] private bool restoreTimeScaleBeforeSceneLoad = true;

    #endregion

    #region Player Teleport

    [Header("Runtime Player Source")]
    [Tooltip("Optional. Auto-found if left empty.")]
    [SerializeField] private PlayerInputManager playerInputManager;

    [Header("Teleport Targets")]
    [SerializeField] private Transform p1TeleportPoint;
    [SerializeField] private Transform p2TeleportPoint;
    [SerializeField] private Transform p3TeleportPoint;
    [SerializeField] private Transform p4TeleportPoint;

    [Header("Teleport Recovery")]
    [Tooltip("Zero every Rigidbody under the player's runtime root after teleport.")]
    [SerializeField] private bool clearRigidBodyVelocity = true;

    [Tooltip("Reset all LeadingCartBehaviour wheel speeds after teleport.")]
    [SerializeField] private bool restoreWheelMovement = true;

    [Tooltip(
        "Restore the common CartControlScript permissions that can leave a debug-teleported " +
        "player unable to move/use normal gameplay actions."
    )]
    [SerializeField] private bool restoreCartControlState = true;

    [Tooltip("Clear Ice freeze and Cola+Mentos temporary movement overrides.")]
    [SerializeField] private bool clearTemporaryPowerupEffects = true;

    [Tooltip("Refill the teleported player's Hype to maximum.")]
    [SerializeField] private bool refillHype = true;

    [Tooltip(
        "Clear a latched stall and give stall detection its normal post-recovery grace."
    )]
    [SerializeField] private bool clearStallState = true;

    [Tooltip(
        "After moving a cart chain, rebuild its path history from the teleported chain pose " +
        "so followers do not chase the pre-teleport world-space trail."
    )]
    [SerializeField] private bool rebuildChainPath = true;

    #endregion

    #region Return To Menu

    [Header("Return To Main Menu")]
    [Tooltip("Scene loaded after any ONE controller holds Y / Button North long enough.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Min(0.1f)]
    [SerializeField] private float returnToMenuHoldSeconds = 5f;

    #endregion

    #region Runtime Diagnostics

    [Header("Runtime - Read Only")]
    [SerializeField] private int lastTeleportedPlayer;
    [SerializeField] private int returnHoldingGamepadDeviceId = -1;
    [SerializeField] private float returnHoldingSeconds;
    [SerializeField] private bool sceneLoadRequested;

    private readonly Dictionary<int, float> returnHoldTimesByDevice =
        new Dictionary<int, float>();

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (sceneLoadRequested) return;

        HandleKeyboardDebug();

        if (sceneLoadRequested) return;

        HandleControllerReturnToMenu();
    }

    private void OnValidate()
    {
        returnToMenuHoldSeconds =
            Mathf.Max(0.1f, returnToMenuHoldSeconds);
    }

    #endregion

    #region Keyboard Debug

    private void HandleKeyboardDebug()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (listenForSceneReload &&
            keyboard.rKey.wasPressedThisFrame)
        {
            ReloadCurrentScene();
            return;
        }

        if (!listenForPlayerTeleport) return;

        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            TeleportAndRecoverPlayer(1);
        }
        else if (keyboard.digit2Key.wasPressedThisFrame)
        {
            TeleportAndRecoverPlayer(2);
        }
        else if (keyboard.digit3Key.wasPressedThisFrame)
        {
            TeleportAndRecoverPlayer(3);
        }
        else if (keyboard.digit4Key.wasPressedThisFrame)
        {
            TeleportAndRecoverPlayer(4);
        }
    }

    private void ReloadCurrentScene()
    {
        Scene currentScene = SceneManager.GetActiveScene();

        if (!currentScene.IsValid())
        {
            Debug.LogError(
                "[GameplayDebugger] Active scene is invalid.",
                this
            );
            return;
        }

        sceneLoadRequested = true;

        if (restoreTimeScaleBeforeSceneLoad)
        {
            Time.timeScale = 1f;
        }

        Debug.Log(
            $"[GameplayDebugger] Reloading scene '{currentScene.name}'.",
            this
        );

        SceneManager.LoadScene(
            currentScene.buildIndex,
            LoadSceneMode.Single
        );
    }

    #endregion

    #region Teleport / Recovery

    private void TeleportAndRecoverPlayer(int playerIndex)
    {
        ResolveReferences();

        if (playerInputManager == null)
        {
            Debug.LogError(
                "[GameplayDebugger] PlayerInputManager was not found.",
                this
            );
            return;
        }

        GameObject playerRoot =
            playerInputManager.GetPlayerRoot(playerIndex);

        Transform target =
            GetTeleportTarget(playerIndex);

        if (playerRoot == null ||
            !playerRoot.activeInHierarchy)
        {
            Debug.LogWarning(
                $"[GameplayDebugger] Player {playerIndex} is not active in this scene/mode.",
                this
            );
            return;
        }

        if (target == null)
        {
            Debug.LogWarning(
                $"[GameplayDebugger] P{playerIndex} Teleport Point is not assigned.",
                this
            );
            return;
        }

        CartControlScript control =
            playerInputManager.GetBoundCartControl(playerIndex);

        if (control == null)
        {
            control =
                playerRoot.GetComponentInChildren<CartControlScript>(true);
        }

        SnakeCartManager snake =
            playerRoot.GetComponentInChildren<SnakeCartManager>(true);

        // Clear active physical motion before changing the hierarchy pose.
        if (clearRigidBodyVelocity)
        {
            ClearRigidbodies(playerRoot);
        }

        playerRoot.transform.SetPositionAndRotation(
            target.position,
            target.rotation
        );

        Physics.SyncTransforms();

        if (clearRigidBodyVelocity)
        {
            ClearRigidbodies(playerRoot);
        }

        if (control != null)
        {
            RecoverCartControl(control);
        }
        else
        {
            Debug.LogWarning(
                $"[GameplayDebugger] P{playerIndex} CartControlScript was not found.",
                playerRoot
            );
        }

        if (restoreWheelMovement)
        {
            LeadingCartBehaviour[] wheels =
                playerRoot.GetComponentsInChildren<LeadingCartBehaviour>(true);

            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] != null)
                {
                    wheels[i].ResetSpeed();
                }
            }
        }

        if (clearStallState)
        {
            LeadingCartStallController[] stalls =
                playerRoot.GetComponentsInChildren<LeadingCartStallController>(true);

            for (int i = 0; i < stalls.Length; i++)
            {
                LeadingCartStallController stall = stalls[i];
                if (stall == null) continue;

                // Suppressing clears a currently latched stall. Releasing the
                // suppression immediately gives the controller its authored
                // post-recovery observation grace.
                stall.SetStallDetectionSuppressed(true);
                stall.SetStallDetectionSuppressed(false);
            }
        }

        if (rebuildChainPath &&
            snake != null &&
            snake.GetSnakeBody() != null &&
            snake.GetSnakeBody().Count >= 2)
        {
            if (!snake.CompleteMoveBackwardRecovery())
            {
                Debug.LogWarning(
                    $"[GameplayDebugger] P{playerIndex} teleported, but chain path recovery failed.",
                    snake
                );
            }
        }

        Physics.SyncTransforms();

        lastTeleportedPlayer = playerIndex;

        Debug.Log(
            $"[GameplayDebugger] Teleported and recovered Player {playerIndex}.",
            playerRoot
        );
    }

    private void RecoverCartControl(CartControlScript control)
    {
        if (control == null) return;

        // Remove any checkout/pit-side player state.
        control.SetOutPit();
        control.SetActiveCheckoutHandler(null);

        if (restoreCartControlState)
        {
            control.EnableControl();

            // Clear any held/latched drift input, then restore permission.
            control.DisallowDrift();
            control.AllowDrift();

            control.AllowSpeedingUp();
            control.AllowAim();
            control.AllowActivatePowerUp();

            // Reverse is a stall-owned recovery permission. Do not leave a
            // stale debug/checkout grant behind after teleport.
            control.DisallowMoveBackward();
        }

        if (clearTemporaryPowerupEffects)
        {
            control.SetPowerupFrozen(false);
            control.SetColaMentosBoost(false, 0f);
        }

        if (refillHype)
        {
            control.AddHype(control.MaxHype);
        }
    }

    private static void ClearRigidbodies(GameObject root)
    {
        if (root == null) return;

        Rigidbody[] bodies =
            root.GetComponentsInChildren<Rigidbody>(true);

        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody body = bodies[i];
            if (body == null) continue;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    private Transform GetTeleportTarget(int playerIndex)
    {
        return playerIndex switch
        {
            1 => p1TeleportPoint,
            2 => p2TeleportPoint,
            3 => p3TeleportPoint,
            4 => p4TeleportPoint,
            _ => null
        };
    }

    #endregion

    #region Controller Return To Menu

    private void HandleControllerReturnToMenu()
    {
        if (!listenForReturnToMenu)
        {
            ResetReturnToMenuProgress();
            return;
        }

        var gamepads = Gamepad.all;

        int strongestDeviceId = -1;
        float strongestHold = 0f;

        for (int i = 0; i < gamepads.Count; i++)
        {
            Gamepad gamepad = gamepads[i];
            if (gamepad == null) continue;

            int deviceId = gamepad.deviceId;

            if (!returnHoldTimesByDevice.ContainsKey(deviceId))
            {
                returnHoldTimesByDevice.Add(deviceId, 0f);
            }

            if (gamepad.buttonNorth.isPressed)
            {
                returnHoldTimesByDevice[deviceId] +=
                    Time.unscaledDeltaTime;

                float held =
                    returnHoldTimesByDevice[deviceId];

                if (held > strongestHold)
                {
                    strongestHold = held;
                    strongestDeviceId = deviceId;
                }

                if (held >= returnToMenuHoldSeconds)
                {
                    returnHoldingGamepadDeviceId = deviceId;
                    returnHoldingSeconds = held;

                    LoadMainMenu();
                    return;
                }
            }
            else
            {
                // Continuous hold from one controller is required.
                returnHoldTimesByDevice[deviceId] = 0f;
            }
        }

        returnHoldingGamepadDeviceId =
            strongestDeviceId;

        returnHoldingSeconds =
            strongestHold;
    }

    private void LoadMainMenu()
    {
        if (sceneLoadRequested) return;

        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            Debug.LogError(
                "[GameplayDebugger] Main Menu Scene Name is empty.",
                this
            );
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            Debug.LogError(
                $"[GameplayDebugger] Main menu scene '{mainMenuSceneName}' cannot be loaded. " +
                "Check Build Settings / Build Profile.",
                this
            );
            return;
        }

        sceneLoadRequested = true;
        Time.timeScale = 1f;

        // Returning to Main Menu ends the current match session.
        // GMode should persist across Tutorial -> Gameplay, but not across
        // Gameplay -> Main Menu -> a new mode selection.
        GMode.DestroyPersistentInstance();

        Debug.Log(
            $"[GameplayDebugger] Controller held Y for {returnToMenuHoldSeconds:0.##}s. " +
            $"Loading '{mainMenuSceneName}'.",
            this
        );

        SceneManager.LoadScene(
            mainMenuSceneName,
            LoadSceneMode.Single
        );
    }

    private void ResetReturnToMenuProgress()
    {
        returnHoldTimesByDevice.Clear();
        returnHoldingGamepadDeviceId = -1;
        returnHoldingSeconds = 0f;
    }

    #endregion

    #region References

    private void ResolveReferences()
    {
        if (playerInputManager == null)
        {
            playerInputManager =
                FindFirstObjectByType<PlayerInputManager>();
        }
    }

    #endregion
}
