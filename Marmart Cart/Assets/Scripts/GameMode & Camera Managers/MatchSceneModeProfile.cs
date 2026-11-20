using UnityEngine;

/// <summary>
/// Camera settings that differ between 2-player and 4-player modes: viewports
/// and lens. UI widgets keep their own 2P/4P values in their own profiles.
///
/// This profile intentionally owns PRESENTATION only. GMode still owns which
/// game mode is active, and CameraManager owns applying these rectangles to
/// the physical Unity Cameras.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Match/Scene Mode Profile",
    fileName = "MatchSceneModeProfile"
)]
public class MatchSceneModeProfile : ScriptableObject
{
    #region Two Player Viewports

    [Header("2 Player Camera Viewports")]
    [Tooltip("Unity Camera.rect for Player 1 in 2P mode. Default: left half of the screen.")]
    [SerializeField] private Rect twoPlayerP1Viewport = new Rect(0f, 0f, 0.5f, 1f);

    [Tooltip("Unity Camera.rect for Player 2 in 2P mode. Default: right half of the screen.")]
    [SerializeField] private Rect twoPlayerP2Viewport = new Rect(0.5f, 0f, 0.5f, 1f);

    #endregion

    #region Four Player Viewports

    [Header("4 Player Camera Viewports")]
    [SerializeField] private Rect fourPlayerP1Viewport = new Rect(0f, 0.5f, 0.5f, 0.5f);
    [SerializeField] private Rect fourPlayerP2Viewport = new Rect(0.5f, 0.5f, 0.5f, 0.5f);
    [SerializeField] private Rect fourPlayerP3Viewport = new Rect(0f, 0f, 0.5f, 0.5f);
    [SerializeField] private Rect fourPlayerP4Viewport = new Rect(0.5f, 0f, 0.5f, 0.5f);

    #endregion



    #region Camera Lens Presentation

    [Header("2 Player Camera Lens")]
    [Tooltip("Base Cinemachine orthographic size used by active player cameras in 2P mode.")]
    [Min(0.01f)]
    [SerializeField] private float twoPlayerBaseOrthographicSize = 30f;

    [Header("4 Player Camera Lens")]
    [Tooltip("Base Cinemachine orthographic size used by active player cameras in 4P mode.")]
    [Min(0.01f)]
    [SerializeField] private float fourPlayerBaseOrthographicSize = 30f;

    [Header("Dynamic Camera Zoom By Cart Count")]
    [Tooltip("How many follower carts are required before one zoom-out step is applied.")]
    [Min(1)]
    [SerializeField] private int cartsPerZoomIncrement = 5;

    [Tooltip("Orthographic-size increase applied for each completed cart-count step.")]
    [Min(0f)]
    [SerializeField] private float orthographicSizeIncrement = 0.5f;

    [Tooltip(
        "Maximum amount that cart-count zoom may add on top of the current mode's base orthographic size. " +
        "For example, Base 30 + Max Additional 4 gives a maximum size of 34."
    )]
    [Min(0f)]
    [SerializeField] private float maxAdditionalOrthographicSize = 4f;

    #endregion


    public Rect GetViewportRect(int playerCount, int playerIndex)
    {
        if (playerCount <= 2)
        {
            return playerIndex switch
            {
                1 => twoPlayerP1Viewport,
                2 => twoPlayerP2Viewport,
                _ => new Rect(0f, 0f, 1f, 1f)
            };
        }

        return playerIndex switch
        {
            1 => fourPlayerP1Viewport,
            2 => fourPlayerP2Viewport,
            3 => fourPlayerP3Viewport,
            4 => fourPlayerP4Viewport,
            _ => new Rect(0f, 0f, 1f, 1f)
        };
    }



    public float GetBaseOrthographicSize(int playerCount)
    {
        return playerCount <= 2
            ? twoPlayerBaseOrthographicSize
            : fourPlayerBaseOrthographicSize;
    }

    public float GetOrthographicSizeForCartCount(int playerCount, int followerCartCount)
    {
        float baseSize = GetBaseOrthographicSize(playerCount);
        int safeCartCount = Mathf.Max(0, followerCartCount);
        int stepSize = Mathf.Max(1, cartsPerZoomIncrement);
        int zoomSteps = Mathf.FloorToInt((float)safeCartCount / stepSize);

        float targetSize = baseSize + zoomSteps * Mathf.Max(0f, orthographicSizeIncrement);
        float maxSize = baseSize + Mathf.Max(0f, maxAdditionalOrthographicSize);

        return Mathf.Clamp(targetSize, baseSize, maxSize);
    }

    [ContextMenu("Reset 2P Viewports To Left / Right")]
    private void ResetTwoPlayerViewportsToLeftRight()
    {
        twoPlayerP1Viewport = new Rect(0f, 0f, 0.5f, 1f);
        twoPlayerP2Viewport = new Rect(0.5f, 0f, 0.5f, 1f);
    }


    [ContextMenu("Reset Camera Lens Defaults")]
    private void ResetCameraLensDefaults()
    {
        twoPlayerBaseOrthographicSize = 30f;
        fourPlayerBaseOrthographicSize = 30f;
        cartsPerZoomIncrement = 5;
        orthographicSizeIncrement = 0.5f;
        maxAdditionalOrthographicSize = 4f;
    }

    private void OnValidate()
    {
        twoPlayerBaseOrthographicSize = Mathf.Max(0.01f, twoPlayerBaseOrthographicSize);
        fourPlayerBaseOrthographicSize = Mathf.Max(0.01f, fourPlayerBaseOrthographicSize);
        cartsPerZoomIncrement = Mathf.Max(1, cartsPerZoomIncrement);
        orthographicSizeIncrement = Mathf.Max(0f, orthographicSizeIncrement);
        maxAdditionalOrthographicSize = Mathf.Max(0f, maxAdditionalOrthographicSize);
    }

}
