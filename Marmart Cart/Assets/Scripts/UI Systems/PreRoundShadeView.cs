using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A dark shade over each player's view while a Pre-Round Stage runs (the
/// Ability pick, later the Policy pop and the Section reveal), so the stage's
/// UI does not sit straight on the frozen world. It is the bottom child of each
/// player's Banner layer: over the world and the HUD, under the stage's UI.
/// Fades in and out in real time. Presentation only: MetaRoundController says
/// when, with PreRoundStagesStarted and PreRoundStagesEnded.
/// </summary>
[DisallowMultipleComponent]
public sealed class PreRoundShadeView : MonoBehaviour
{
    private const int MaxViews = PlayerViewService.MaxViews;

    [Header("References")]
    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private MetaRoundController roundController;

    [Tooltip("Auto-found if left empty.")]
    [SerializeField] private ViewportCanvasSystem viewportCanvas;

    [Header("Look")]
    [Tooltip("Colour and strength of the shade at full fade.")]
    [SerializeField] private Color shadeColor = new Color(0f, 0f, 0f, 0.7f);

    [Tooltip("Real-time seconds to fade in or out.")]
    [Min(0f)]
    [SerializeField] private float fadeSeconds = 0.25f;

    private readonly Image[] shades = new Image[MaxViews];

    private bool shown;
    private bool fading;
    private float fade01;

    private void Awake()
    {
        if (roundController == null)
            roundController = FindFirstObjectByType<MetaRoundController>();

        if (viewportCanvas == null)
            viewportCanvas = ViewportCanvasSystem.Instance != null
                ? ViewportCanvasSystem.Instance
                : FindFirstObjectByType<ViewportCanvasSystem>();
    }

    private void OnEnable()
    {
        if (roundController != null)
        {
            roundController.PreRoundStagesStarted += HandleStagesStarted;
            roundController.PreRoundStagesEnded += HandleStagesEnded;
        }

        if (viewportCanvas != null) viewportCanvas.LayoutChanged += Refresh;

        shown = roundController != null && roundController.PreRoundStagesRunning;
        fade01 = shown ? 1f : 0f;
        fading = false;
        Refresh();
    }

    private void OnDisable()
    {
        if (roundController != null)
        {
            roundController.PreRoundStagesStarted -= HandleStagesStarted;
            roundController.PreRoundStagesEnded -= HandleStagesEnded;
        }

        if (viewportCanvas != null) viewportCanvas.LayoutChanged -= Refresh;

        shown = false;
        fading = false;
        fade01 = 0f;
        Refresh();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < shades.Length; i++)
        {
            if (shades[i] != null) Destroy(shades[i].gameObject);
        }
    }

    private void Update()
    {
        if (!fading) return;

        float step = fadeSeconds > 0f ? Time.unscaledDeltaTime / fadeSeconds : 1f;
        fade01 = Mathf.MoveTowards(fade01, shown ? 1f : 0f, step);
        fading = fade01 != (shown ? 1f : 0f);
        Refresh();
    }

    private void HandleStagesStarted()
    {
        shown = true;
        fading = true;
        Debug.Log("[UI] Pre-round shade on.", this);
        Refresh();
    }

    private void HandleStagesEnded()
    {
        shown = false;
        fading = true;
        Debug.Log("[UI] Pre-round shade off.", this);
    }

    private void Refresh()
    {
        if (fade01 > 0f) EnsureShades();

        Color color = shadeColor;
        color.a *= fade01;

        for (int i = 0; i < shades.Length; i++)
        {
            Image shade = shades[i];
            if (shade == null) continue;

            bool visible = fade01 > 0f && viewportCanvas != null && viewportCanvas.IsPlayerViewActive(i + 1);

            if (shade.gameObject.activeSelf != visible) shade.gameObject.SetActive(visible);
            if (!visible) continue;

            shade.color = color;

            // Stage UI added to the Banner layer later stays on top.
            if (shade.transform.GetSiblingIndex() != 0) shade.transform.SetAsFirstSibling();
        }
    }

    private void EnsureShades()
    {
        if (viewportCanvas == null) return;

        for (int i = 0; i < MaxViews; i++)
        {
            if (shades[i] != null) continue;

            if (!viewportCanvas.TryGetPlayerLayer(i + 1, ViewportCanvasSystem.PlayerLayer.Banner, out RectTransform layer))
                continue;

            GameObject shadeObject = new GameObject($"P{i + 1} Pre-Round Shade", typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)shadeObject.transform;
            rect.SetParent(layer, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();

            Image image = shadeObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = Color.clear;

            shadeObject.SetActive(false);
            shades[i] = image;
        }
    }
}
