using UnityEngine;

/// <summary>
/// Scene-level coordinator for mode-dependent setup.
///
/// Startup rule:
/// - Awake only resolves scene references.
/// - Start reads GMode and configures the scene.
///
/// Reading GMode in Start is intentional: it guarantees that every scene object's
/// Awake has already run, so a GMode already placed in the Inspector can establish
/// its singleton before this controller reads it.
/// </summary>
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public class MatchSceneModeController : MonoBehaviour
{
    #region References

    [Header("Mode Source")]
    [Tooltip(
        "Optional explicit GMode reference. Normally this resolves automatically. " +
        "Keeping this visible also makes direct-scene Inspector testing unambiguous."
    )]
    [SerializeField] private GMode gMode;

    [Header("Mode Presentation")]
    [SerializeField] private MatchSceneModeProfile modeProfile;

    [Header("Scene Systems")]
    [SerializeField] private PlayerInputManager playerInputManager;
    [SerializeField] private CameraManager cameraManager;

    [Tooltip(
        "Optional but recommended. When assigned, inactive P3/P4 HUD slots are disabled " +
        "and each slot is given the same physical Camera used by CameraManager."
    )]
    [SerializeField] private PlayerWorldHUDSystem playerWorldHUDSystem;

    [Header("Adaptive Shapes Presentation")]
    [Tooltip("Optional. Receives the 2P/4P runtime scale multiplier without changing the tuned layout asset values.")]
    [SerializeField] private PlayerWorldHUDRenderer playerWorldHUDRenderer;

    [Tooltip("Optional. Receives the 2P/4P runtime scale multiplier for the orbit pointers.")]
    [SerializeField] private OtherPlayerPointerRenderer otherPlayerPointerRenderer;

    [Tooltip("Optional. Receives the 2P/4P runtime scale multiplier for visible-player raindrop markers.")]
    [SerializeField] private OtherPlayerVisibleMarkerRenderer otherPlayerVisibleMarkerRenderer;

    [Tooltip(
        "Optional. Selects the 2P/4P master scale and root offset authored " +
        "inside MatchViewportOverlayProfile.")]
    [SerializeField] private MatchViewportOverlayRenderer matchViewportOverlayRenderer;

    #endregion

    #region Runtime

    [Header("Runtime - Read Only")]
    [SerializeField] private GameMode configuredMode = GameMode.duel2P;
    [SerializeField] private int configuredPlayerCount;
    [SerializeField] private bool configured;

    private GMode subscribedGMode;

    public GameMode ConfiguredMode => configuredMode;
    public int ConfiguredPlayerCount => configuredPlayerCount;
    public bool IsConfigured => configured;

    #endregion

    #region Unity

    private void Awake()
    {
        // Do not configure here. GMode or other startup systems may still be in Awake.
        ResolveReferences();
        ResolveGModeReference();
    }

    private void OnEnable()
    {
        // Subscription may succeed immediately if GMode is already initialized.
        // Start will resolve/subscribe again before the initial configuration.
        ResolveGModeReference();
        SubscribeToGMode();
    }

    private void Start()
    {
        // All active scene objects have completed Awake by this point.
        ResolveReferences();
        ResolveGModeReference();
        SubscribeToGMode();
        ConfigureSceneFromGMode();
    }

    private void OnDisable()
    {
        UnsubscribeFromGMode();
    }

    #endregion

    #region GMode Listening

    private void ResolveGModeReference()
    {
        // The singleton is authoritative once it exists (important when coming
        // from a previous lobby/bootstrap scene via DontDestroyOnLoad).
        if (GMode.Instance != null)
        {
            gMode = GMode.Instance;
            return;
        }

        // For direct gameplay-scene testing, read the GMode component already
        // placed in this scene. Its Inspector CurrentMode value is serialized and
        // can therefore be read before/independently of the singleton assignment.
        if (gMode == null)
        {
            gMode = FindFirstObjectByType<GMode>();
        }
    }

    private void SubscribeToGMode()
    {
        ResolveGModeReference();

        if (subscribedGMode == gMode) return;

        UnsubscribeFromGMode();

        subscribedGMode = gMode;
        if (subscribedGMode != null)
        {
            subscribedGMode.OnModeChanged += HandleGameModeChanged;
        }
    }

    private void UnsubscribeFromGMode()
    {
        if (subscribedGMode == null) return;

        subscribedGMode.OnModeChanged -= HandleGameModeChanged;
        subscribedGMode = null;
    }

    private void HandleGameModeChanged(GameMode newMode)
    {
        if (!isActiveAndEnabled) return;

        Debug.Log(
            $"[MatchSceneModeController] GMode changed to {newMode}. Reconfiguring scene.",
            this
        );

        ConfigureSceneFromGMode();
    }

    #endregion

    #region Configuration

    [ContextMenu("Configure Scene From GMode")]
    public void ConfigureSceneFromGMode()
    {
        ResolveReferences();
        ResolveGModeReference();
        SubscribeToGMode();

        if (gMode != null)
        {
            configuredMode = gMode.CurrentMode;
            configuredPlayerCount = gMode.PlayerCount();
        }
        else
        {
            configuredMode = GameMode.duel2P;
            configuredPlayerCount = 2;

            Debug.LogWarning(
                "[MatchSceneModeController] No GMode was found. " +
                "Falling back to duel2P / 2 players for direct-scene testing.",
                this
            );
        }

        if (playerInputManager != null)
        {
            playerInputManager.ConfigureForPlayerCount(configuredPlayerCount);
        }
        else
        {
            Debug.LogError("[MatchSceneModeController] PlayerInputManager is missing.", this);
        }

        if (cameraManager != null)
        {
            cameraManager.ConfigureForPlayerCount(configuredPlayerCount, modeProfile);
        }
        else
        {
            Debug.LogError("[MatchSceneModeController] CameraManager is missing.", this);
        }

        ConfigureWorldHUDSlots();
        ConfigureAdaptiveShapesPresentation();

        configured = true;

        Debug.Log(
            $"[MatchSceneModeController] Scene configured from GMode '{configuredMode}' " +
            $"({configuredPlayerCount} players).",
            this
        );
    }

    private void ConfigureWorldHUDSlots()
    {
        if (playerWorldHUDSystem == null || cameraManager == null) return;

        for (int playerIndex = 1; playerIndex <= PlayerWorldHUDSystem.MaxPlayerSlots; playerIndex++)
        {
            bool active = playerIndex <= configuredPlayerCount;

            playerWorldHUDSystem.SetSlotEnabled(playerIndex, active);
            playerWorldHUDSystem.SetGameplayCamera(
                playerIndex,
                cameraManager.GetOutputCamera(playerIndex)
            );

            if (!active)
            {
                playerWorldHUDSystem.UnregisterPlayer(playerIndex);
            }
        }

        playerWorldHUDSystem.RetryAllBindingsNow();
    }

    private void ConfigureAdaptiveShapesPresentation()
    {
        float worldHUDScale = modeProfile != null
            ? modeProfile.GetWorldHUDScaleMultiplier(configuredPlayerCount)
            : 1f;

        float pointerScale = modeProfile != null
            ? modeProfile.GetOtherPlayerPointerScaleMultiplier(configuredPlayerCount)
            : 1f;

        float visibleMarkerScale = modeProfile != null
            ? modeProfile.GetVisiblePlayerMarkerScaleMultiplier(configuredPlayerCount)
            : 1f;

        if (playerWorldHUDRenderer != null)
        {
            playerWorldHUDRenderer.SetModeScaleMultiplier(worldHUDScale);
        }

        if (otherPlayerPointerRenderer != null)
        {
            otherPlayerPointerRenderer.SetModeScaleMultiplier(pointerScale);
        }

        if (otherPlayerVisibleMarkerRenderer != null)
        {
            otherPlayerVisibleMarkerRenderer.SetModeScaleMultiplier(visibleMarkerScale);
        }

        if (matchViewportOverlayRenderer != null)
        {
            matchViewportOverlayRenderer.SetModePlayerCount(
                configuredPlayerCount
            );
        }

    }

    #endregion

    #region Reference Resolution

    private void ResolveReferences()
    {
        if (playerInputManager == null)
        {
            playerInputManager = FindFirstObjectByType<PlayerInputManager>();
        }

        if (cameraManager == null)
        {
            cameraManager = FindFirstObjectByType<CameraManager>();
        }

        if (playerWorldHUDSystem == null)
        {
            playerWorldHUDSystem = FindFirstObjectByType<PlayerWorldHUDSystem>();
        }

        if (playerWorldHUDRenderer == null)
        {
            playerWorldHUDRenderer = FindFirstObjectByType<PlayerWorldHUDRenderer>();
        }

        if (otherPlayerPointerRenderer == null)
        {
            otherPlayerPointerRenderer = FindFirstObjectByType<OtherPlayerPointerRenderer>();
        }

        if (otherPlayerVisibleMarkerRenderer == null)
        {
            otherPlayerVisibleMarkerRenderer = FindFirstObjectByType<OtherPlayerVisibleMarkerRenderer>();
        }

        if (matchViewportOverlayRenderer == null)
        {
            matchViewportOverlayRenderer = FindFirstObjectByType<MatchViewportOverlayRenderer>();
        }

    }

    #endregion
}
