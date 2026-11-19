using System;
using UnityEngine;

/// <summary>
/// Scene-local source of truth for player views and UI scale.
/// See Docs/Design/ui-architecture.md.
///
/// - Maps each local player to the Camera that renders their viewport.
/// - Holds UiScale: min(screen width / 1920, screen height / 1080).
/// - Knows the active player count.
/// - Raises LayoutChanged when the player count, the views or the screen size change.
///
/// CameraManager stays the authority for viewport rects; this service reads its
/// cameras after MatchSceneModeController has configured them.
/// Player indices are 1-based.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerViewService : MonoBehaviour
{
    public const int MaxViews = CameraManager.MaxPlayers;
    public const float ReferenceWidth = 1920f;
    public const float ReferenceHeight = 1080f;

    [Header("References")]
    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private CameraManager cameraManager;

    [Header("Runtime - Read Only")]
    [SerializeField] private bool configured;
    [SerializeField] private int activePlayerCount;
    [SerializeField] private float uiScale = 1f;
    [SerializeField] private Vector2Int screenSize;

    private readonly Camera[] viewCameras = new Camera[MaxViews];

    public static PlayerViewService Instance { get; private set; }

    public event Action LayoutChanged;

    /// <summary>True once the scene has configured its player views.</summary>
    public bool IsConfigured => configured;
    public int ActivePlayerCount => activePlayerCount;
    public float UiScale => uiScale;
    public Vector2Int ScreenSize => screenSize;

    #region Unity

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[UI] A second PlayerViewService was found in the scene. The newest one is used.", this);
        }

        Instance = this;

        if (cameraManager == null)
            cameraManager = FindFirstObjectByType<CameraManager>();

        RefreshScreen();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (Screen.width == screenSize.x && Screen.height == screenSize.y) return;

        RefreshScreen();
        Debug.Log($"[UI] Screen {screenSize.x}x{screenSize.y} | UI scale {uiScale:0.###}", this);
        LayoutChanged?.Invoke();
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Called by MatchSceneModeController after CameraManager has applied the viewports.
    /// </summary>
    public void Configure(int playerCount)
    {
        if (cameraManager == null)
            cameraManager = FindFirstObjectByType<CameraManager>();

        activePlayerCount = Mathf.Clamp(playerCount, 0, MaxViews);

        for (int i = 0; i < MaxViews; i++)
        {
            viewCameras[i] = i < activePlayerCount && cameraManager != null
                ? cameraManager.GetOutputCamera(i + 1)
                : null;
        }

        configured = true;
        RefreshScreen();

        if (cameraManager == null)
            Debug.LogError("[UI] CameraManager not found. No player views are available.", this);

        Debug.Log(
            $"[UI] Views configured | {activePlayerCount} players | Screen {screenSize.x}x{screenSize.y} | " +
            $"UI scale {uiScale:0.###} | {DescribeViews()}",
            this
        );

        LayoutChanged?.Invoke();
    }

    private void RefreshScreen()
    {
        screenSize = new Vector2Int(Screen.width, Screen.height);

        uiScale = screenSize.x > 0 && screenSize.y > 0
            ? Mathf.Min(screenSize.x / ReferenceWidth, screenSize.y / ReferenceHeight)
            : 1f;
    }

    #endregion

    #region Queries

    public bool IsViewActive(int playerIndex)
    {
        return TryGetCamera(playerIndex, out _);
    }

    public bool TryGetCamera(int playerIndex, out Camera viewCamera)
    {
        viewCamera = IsValidPlayer(playerIndex) ? viewCameras[playerIndex - 1] : null;
        return viewCamera != null;
    }

    /// <summary>The view's normalized viewport rect (Camera.rect).</summary>
    public bool TryGetViewportRect(int playerIndex, out Rect normalizedRect)
    {
        bool found = TryGetCamera(playerIndex, out Camera viewCamera);
        normalizedRect = found ? viewCamera.rect : default;
        return found;
    }

    /// <summary>The view's rect in screen pixels (Camera.pixelRect).</summary>
    public bool TryGetPixelRect(int playerIndex, out Rect pixelRect)
    {
        bool found = TryGetCamera(playerIndex, out Camera viewCamera);
        pixelRect = found ? viewCamera.pixelRect : default;
        return found;
    }

    /// <summary>Which player's view this camera renders. False for any other camera.</summary>
    public bool TryGetPlayerIndex(Camera viewCamera, out int playerIndex)
    {
        if (viewCamera != null)
        {
            for (int i = 0; i < activePlayerCount; i++)
            {
                if (viewCameras[i] != viewCamera) continue;

                playerIndex = i + 1;
                return true;
            }
        }

        playerIndex = 0;
        return false;
    }

    private static bool IsValidPlayer(int playerIndex)
    {
        return playerIndex >= 1 && playerIndex <= MaxViews;
    }

    #endregion

    #region Logging

    private string DescribeViews()
    {
        var text = new System.Text.StringBuilder(96);

        for (int i = 0; i < activePlayerCount; i++)
        {
            if (i > 0) text.Append(" | ");

            Camera viewCamera = viewCameras[i];
            text.Append('P').Append(i + 1).Append(' ');

            if (viewCamera == null)
            {
                text.Append("no camera");
                continue;
            }

            Rect rect = viewCamera.rect;
            text.Append(viewCamera.name)
                .Append(" (")
                .Append(rect.x.ToString("0.##")).Append(", ")
                .Append(rect.y.ToString("0.##")).Append(", ")
                .Append(rect.width.ToString("0.##")).Append(" x ")
                .Append(rect.height.ToString("0.##")).Append(')');
        }

        return text.ToString();
    }

    #endregion
}
