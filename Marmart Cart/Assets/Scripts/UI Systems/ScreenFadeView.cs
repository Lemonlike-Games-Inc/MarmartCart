using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen fade between Rounds. See Docs/Design/ui-architecture.md.
///
/// Presentation only: MetaRoundController owns the timing and exposes
/// ScreenFadeAlpha; this view copies it onto one image in the full-screen
/// canvas root, which draws over every player view.
/// </summary>
[DisallowMultipleComponent]
public sealed class ScreenFadeView : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private MetaRoundController roundController;

    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private ViewportCanvasSystem viewportCanvas;

    [Header("Look")]
    [SerializeField] private Color fadeColor = Color.black;

    private Image fadeImage;
    private float shownAlpha = -1f;

    private void Awake()
    {
        if (roundController == null)
            roundController = FindFirstObjectByType<MetaRoundController>();

        if (viewportCanvas == null)
            viewportCanvas = ViewportCanvasSystem.Instance != null
                ? ViewportCanvasSystem.Instance
                : FindFirstObjectByType<ViewportCanvasSystem>();

        if (roundController == null)
            Debug.LogWarning("[UI] ScreenFadeView has no MetaRoundController. The screen will not fade.", this);
    }

    // LateUpdate runs before rendering, so the first frame after a load is already black.
    private void LateUpdate()
    {
        float alpha = roundController != null ? roundController.ScreenFadeAlpha : 0f;
        if (alpha == shownAlpha) return;

        if (fadeImage == null && !TryCreateImage()) return;

        shownAlpha = alpha;
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, fadeColor.a * alpha);
        fadeImage.enabled = alpha > 0f;
    }

    private bool TryCreateImage()
    {
        if (viewportCanvas == null || viewportCanvas.FullscreenRoot == null) return false;

        var imageObject = new GameObject("Fade Image", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)imageObject.transform;

        rect.SetParent(viewportCanvas.FullscreenRoot, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        fadeImage = imageObject.GetComponent<Image>();
        fadeImage.raycastTarget = false;
        return true;
    }
}
