using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Owns split-screen camera activation, viewport rectangles, and Cinemachine
/// focus targets for the gameplay scene.
///
/// MatchSceneModeController supplies the active player count and presentation
/// profile. This manager does not independently decide whether the match is 2P
/// or 4P.
///
/// Runtime-instantiated leading carts are supported: cameras/viewports are
/// configured immediately, while Cinemachine focus targets are bound as soon
/// as each leading cart appears under its chain root.
/// </summary>
[DisallowMultipleComponent]
public class CameraManager : MonoBehaviour
{
    public const int MaxPlayers = 4;

    #region Player / Focus References

    [Header("Player Chain Roots / Focus Sources")]
    [SerializeField] private GameObject chainedCartsP1;
    [SerializeField] private GameObject chainedCartsP2;
    [SerializeField] private GameObject chainedCartsP3;
    [SerializeField] private GameObject chainedCartsP4;

    #endregion

    #region Output Cameras

    [Header("Physical Unity Cameras")]
    [Tooltip("The real Unity Camera rendering Player 1's viewport. Not a CinemachineCamera.")]
    [SerializeField] private Camera outputCameraP1;

    [SerializeField] private Camera outputCameraP2;
    [SerializeField] private Camera outputCameraP3;
    [SerializeField] private Camera outputCameraP4;

    #endregion

    #region Cinemachine Cameras

    [Header("Cinemachine Cameras")]
    [SerializeField] private CinemachineCamera topDownCameraP1;
    [SerializeField] private CinemachineCamera topDownCameraP2;
    [SerializeField] private CinemachineCamera topDownCameraP3;
    [SerializeField] private CinemachineCamera topDownCameraP4;

    #endregion


    #region Lens Runtime

    [Header("Lens Runtime - Read Only")]
    [SerializeField] private float currentP1OrthographicSize;
    [SerializeField] private float currentP2OrthographicSize;
    [SerializeField] private float currentP3OrthographicSize;
    [SerializeField] private float currentP4OrthographicSize;

    private readonly int[] playerFollowerCartCounts = new int[MaxPlayers];
    private MatchSceneModeProfile activeProfile;

    #endregion

    #region Runtime Focus Binding

    [Header("Runtime Focus Binding")]
    [Tooltip(
        "How often to retry finding runtime-instantiated leading carts for camera focus. " +
        "This uses a coroutine rather than per-frame Update polling."
    )]
    [Min(0.01f)]
    [SerializeField] private float focusRetryIntervalSeconds = 0.05f;

    [Tooltip(
        "How long to wait for runtime leading carts before reporting an error. " +
        "Set to 0 to wait forever."
    )]
    [Min(0f)]
    [SerializeField] private float focusBindingTimeoutSeconds = 10f;

    [SerializeField] private bool logSuccessfulFocusBindings = false;

    #endregion

    #region Runtime

    [Header("Runtime - Read Only")]
    [SerializeField] private int configuredPlayerCount;
    [SerializeField] private int boundFocusCount;

    private readonly bool[] cameraSwitcherRegistered = new bool[MaxPlayers];
    private readonly bool[] focusBindingComplete = new bool[MaxPlayers];

    private Coroutine focusBindingRoutine;

    public int ConfiguredPlayerCount => configuredPlayerCount;
    public int BoundFocusCount => boundFocusCount;
    public bool AllActiveFocusTargetsBound => configuredPlayerCount > 0 && boundFocusCount >= configuredPlayerCount;

    #endregion

    #region Configuration

    public void ConfigureForPlayerCount(
        int requestedPlayerCount,
        MatchSceneModeProfile profile)
    {
        int playerCount = requestedPlayerCount <= 2 ? 2 : 4;

        if (profile == null)
        {
            Debug.LogError(
                "[CameraManager] MatchSceneModeProfile is missing. Cannot configure viewports.",
                this
            );
            return;
        }

        StopFocusBindingRoutine();

        // Make reconfiguration deterministic if this method is ever called again.
        UnregisterAllFromCameraSwitcher();
        ResetFocusBindingState();

        configuredPlayerCount = playerCount;
        activeProfile = profile;

        for (int playerIndex = 1; playerIndex <= MaxPlayers; playerIndex++)
        {
            bool active = playerIndex <= playerCount;

            Camera outputCamera = GetOutputCamera(playerIndex);
            CinemachineCamera cinemachineCamera = GetCinemachineCamera(playerIndex);

            if (outputCamera != null)
            {
                if (active)
                {
                    outputCamera.rect = profile.GetViewportRect(playerCount, playerIndex);
                }

                SetCameraComponentAndObjectActive(
                    outputCamera,
                    active,
                    playerIndex,
                    "physical output Camera"
                );
            }
            else if (active)
            {
                Debug.LogWarning(
                    $"[CameraManager] Physical output Camera for Player {playerIndex} is not assigned.",
                    this
                );
            }

            if (cinemachineCamera != null)
            {
                if (active)
                {
                    ApplyPlayerLens(playerIndex);
                }

                SetCameraComponentAndObjectActive(
                    cinemachineCamera,
                    active,
                    playerIndex,
                    "CinemachineCamera"
                );

                if (active)
                {
                    CameraSwitcher.Register(cinemachineCamera);
                    cameraSwitcherRegistered[playerIndex - 1] = true;
                }
            }
            else if (active)
            {
                Debug.LogWarning(
                    $"[CameraManager] CinemachineCamera for Player {playerIndex} is not assigned.",
                    this
                );
            }
        }

        // Focus targets may not exist yet. Bind them independently as the
        // runtime leading carts are spawned.
        StartFocusBindingRoutine();
    }

    /// <summary>
    /// Camera.enabled alone cannot reactivate a camera whose GameObject starts
    /// inactive. Keep both layers synchronized so 2P and 4P work regardless of
    /// how the scene was left in the Inspector before entering Play mode.
    /// </summary>
    private void SetCameraComponentAndObjectActive(
        Behaviour cameraComponent,
        bool active,
        int playerIndex,
        string cameraDescription)
    {
        if (cameraComponent == null) return;

        GameObject cameraObject =
            cameraComponent.gameObject;

        // Set the Behaviour first. When activating an inactive GameObject, its
        // OnEnable lifecycle then begins with the correct component state.
        cameraComponent.enabled = active;

        if (cameraObject == null ||
            cameraObject.activeSelf == active)
        {
            return;
        }

        // Misconfigured references should never allow a player camera slot to
        // deactivate CameraManager itself or one of its ancestors.
        if (!active &&
            (
                cameraObject == gameObject ||
                transform.IsChildOf(cameraObject.transform)
            ))
        {
            Debug.LogWarning(
                $"[CameraManager] Player {playerIndex} {cameraDescription} is on " +
                "CameraManager's own GameObject hierarchy. Its component was " +
                "disabled, but the GameObject was kept active to protect the manager.",
                cameraObject
            );

            return;
        }

        cameraObject.SetActive(active);
    }

    #endregion

    #region Camera Read API

    public Camera GetOutputCamera(int playerIndex)
    {
        return playerIndex switch
        {
            1 => outputCameraP1,
            2 => outputCameraP2,
            3 => outputCameraP3,
            4 => outputCameraP4,
            _ => null
        };
    }

    public CinemachineCamera GetCinemachineCamera(int playerIndex)
    {
        return playerIndex switch
        {
            1 => topDownCameraP1,
            2 => topDownCameraP2,
            3 => topDownCameraP3,
            4 => topDownCameraP4,
            _ => null
        };
    }

    #endregion


    #region Camera Lens

    /// <summary>
    /// Receives the player's current FOLLOWER cart count. The count is cached so
    /// a later 2P/4P reconfiguration can immediately re-evaluate the correct lens.
    /// </summary>
    public void SetPlayerCartCount(int playerIndex, int followerCartCount)
    {
        if (playerIndex < 1 || playerIndex > MaxPlayers) return;

        playerFollowerCartCounts[playerIndex - 1] = Mathf.Max(0, followerCartCount);

        if (playerIndex <= configuredPlayerCount)
        {
            ApplyPlayerLens(playerIndex);
        }
    }

    public void RefreshAllActiveLens()
    {
        for (int playerIndex = 1; playerIndex <= configuredPlayerCount; playerIndex++)
        {
            ApplyPlayerLens(playerIndex);
        }
    }

    private void ApplyPlayerLens(int playerIndex)
    {
        if (activeProfile == null) return;

        CinemachineCamera camera = GetCinemachineCamera(playerIndex);
        if (camera == null) return;

        int followerCartCount = playerFollowerCartCounts[playerIndex - 1];
        float size = activeProfile.GetOrthographicSizeForCartCount(
            configuredPlayerCount,
            followerCartCount
        );

        camera.Lens.OrthographicSize = size;
        SetRuntimeOrthographicSize(playerIndex, size);
    }

    private void SetRuntimeOrthographicSize(int playerIndex, float size)
    {
        switch (playerIndex)
        {
            case 1: currentP1OrthographicSize = size; break;
            case 2: currentP2OrthographicSize = size; break;
            case 3: currentP3OrthographicSize = size; break;
            case 4: currentP4OrthographicSize = size; break;
        }
    }

    #endregion

    #region Camera Focus

    /// <summary>
    /// Manual/compatibility refresh for one player. If the runtime leading cart
    /// does not exist yet, this simply leaves the slot unresolved; the retry
    /// routine can bind it later.
    /// </summary>
    public void UpdateCameraFocus(int playerIndex)
    {
        if (playerIndex < 1 || playerIndex > MaxPlayers) return;

        int slot = playerIndex - 1;

        if (TryUpdateCameraFocus(playerIndex))
        {
            MarkFocusBound(slot, playerIndex);
        }
    }

    public void RefreshAllActiveCameraFocus()
    {
        StopFocusBindingRoutine();

        for (int slot = 0; slot < MaxPlayers; slot++)
        {
            focusBindingComplete[slot] = slot >= configuredPlayerCount;
        }

        boundFocusCount = 0;

        for (int playerIndex = 1; playerIndex <= configuredPlayerCount; playerIndex++)
        {
            if (TryUpdateCameraFocus(playerIndex))
            {
                MarkFocusBound(playerIndex - 1, playerIndex);
            }
        }

        if (!AllActiveFocusTargetsBound)
        {
            StartFocusBindingRoutine();
        }
    }

    // Compatibility methods retained for existing inspector events / callers.
    public void SetCameraP1ToLookAtLeadingCart() => UpdateCameraFocus(1);
    public void SetCameraP2ToLookAtLeadingCart() => UpdateCameraFocus(2);
    public void SetCameraP3ToLookAtLeadingCart() => UpdateCameraFocus(3);
    public void SetCameraP4ToLookAtLeadingCart() => UpdateCameraFocus(4);

    private bool TryUpdateCameraFocus(int playerIndex)
    {
        CinemachineCamera targetCamera = GetCinemachineCamera(playerIndex);
        GameObject chainRoot = GetChainRoot(playerIndex);

        if (targetCamera == null || chainRoot == null)
        {
            return false;
        }

        if (!chainRoot.activeInHierarchy || chainRoot.transform.childCount <= 0)
        {
            return false;
        }

        Transform leadingCartTarget = chainRoot.transform.GetChild(0);

        if (leadingCartTarget == null)
        {
            return false;
        }

        CameraSwitcher.UpdateCameraFocus(
            targetCamera,
            leadingCartTarget
        );

        return true;
    }

    private GameObject GetChainRoot(int playerIndex)
    {
        return playerIndex switch
        {
            1 => chainedCartsP1,
            2 => chainedCartsP2,
            3 => chainedCartsP3,
            4 => chainedCartsP4,
            _ => null
        };
    }

    #endregion


    #region Runtime Focus Binding

    private void StartFocusBindingRoutine()
    {
        if (!isActiveAndEnabled || configuredPlayerCount <= 0) return;
        if (AllActiveFocusTargetsBound) return;

        focusBindingRoutine = StartCoroutine(BindRuntimeFocusTargetsRoutine());
    }

    private void StopFocusBindingRoutine()
    {
        if (focusBindingRoutine == null) return;

        StopCoroutine(focusBindingRoutine);
        focusBindingRoutine = null;
    }

    private IEnumerator BindRuntimeFocusTargetsRoutine()
    {
        float startedAt = Time.realtimeSinceStartup;
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(
            Mathf.Max(0.01f, focusRetryIntervalSeconds)
        );

        while (!AllActiveFocusTargetsBound)
        {
            for (int playerIndex = 1; playerIndex <= configuredPlayerCount; playerIndex++)
            {
                int slot = playerIndex - 1;
                if (focusBindingComplete[slot]) continue;

                if (TryUpdateCameraFocus(playerIndex))
                {
                    MarkFocusBound(slot, playerIndex);
                }
            }

            if (AllActiveFocusTargetsBound)
            {
                break;
            }

            if (focusBindingTimeoutSeconds > 0f &&
                Time.realtimeSinceStartup - startedAt >= focusBindingTimeoutSeconds)
            {
                ReportUnresolvedFocusTargets();
                focusBindingRoutine = null;
                yield break;
            }

            yield return wait;
        }

        focusBindingRoutine = null;
    }

    private void MarkFocusBound(int slot, int playerIndex)
    {
        if (slot < 0 || slot >= MaxPlayers) return;
        if (focusBindingComplete[slot]) return;

        focusBindingComplete[slot] = true;
        boundFocusCount++;

        if (logSuccessfulFocusBindings)
        {
            Debug.Log(
                $"[CameraManager] Player {playerIndex} runtime leading cart bound as camera focus target.",
                this
            );
        }
    }

    private void ReportUnresolvedFocusTargets()
    {
        for (int playerIndex = 1; playerIndex <= configuredPlayerCount; playerIndex++)
        {
            int slot = playerIndex - 1;
            if (focusBindingComplete[slot]) continue;

            GameObject chainRoot = GetChainRoot(playerIndex);

            Debug.LogError(
                $"[CameraManager] Timed out waiting for Player {playerIndex} runtime leading cart " +
                "for Cinemachine focus. Check the assigned chain root and its runtime spawn flow.",
                chainRoot != null ? (UnityEngine.Object)chainRoot : this
            );
        }
    }

    private void ResetFocusBindingState()
    {
        boundFocusCount = 0;

        for (int slot = 0; slot < MaxPlayers; slot++)
        {
            focusBindingComplete[slot] = false;
        }
    }

    #endregion

    #region CameraSwitcher Lifetime

    private void OnEnable()
    {
        if (configuredPlayerCount > 0 && !AllActiveFocusTargetsBound && focusBindingRoutine == null)
        {
            StartFocusBindingRoutine();
        }
    }

    private void OnDisable()
    {
        StopFocusBindingRoutine();
        UnregisterAllFromCameraSwitcher();
    }

    private void UnregisterAllFromCameraSwitcher()
    {
        for (int playerIndex = 1; playerIndex <= MaxPlayers; playerIndex++)
        {
            int slot = playerIndex - 1;
            if (!cameraSwitcherRegistered[slot]) continue;

            CinemachineCamera camera = GetCinemachineCamera(playerIndex);

            if (camera != null)
            {
                CameraSwitcher.Unregister(camera);
            }

            cameraSwitcherRegistered[slot] = false;
        }
    }

    private void OnValidate()
    {
        focusRetryIntervalSeconds = Mathf.Max(0.01f, focusRetryIntervalSeconds);
        focusBindingTimeoutSeconds = Mathf.Max(0f, focusBindingTimeoutSeconds);
    }

    #endregion
}
