using UnityEngine;

/// <summary>
/// TEMPORARY. Shows a coloured primitive so each Cart Feedback event can be
/// seen in Play Mode. Mercer replaces these with real effects; delete the
/// placeholder objects (and this script once none are left).
///
/// Visible only when its own Show box and the CartFeedbackPlaceholderSwitch
/// on the Cart Feedback root are both ticked. Events still arrive when hidden.
/// </summary>
public class CartFeedbackPlaceholder : MonoBehaviour
{
    [SerializeField] private GameObject visual;
    [SerializeField] private Color color = Color.white;
    [Min(0.05f)]
    [SerializeField] private float flashDuration = 0.5f;

    [Tooltip("Untick to hide this placeholder only. The master switch is on the Cart Feedback root.")]
    [SerializeField] private bool show = true;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private CartFeedbackPlaceholderSwitch masterSwitch;
    private bool active;
    private float hideAtTime = -1f;

    private bool IsAllowed => show && (masterSwitch == null || masterSwitch.ShowPlaceholders);

    private void Awake()
    {
        masterSwitch = GetComponentInParent<CartFeedbackPlaceholderSwitch>(true);

        if (visual == null) return;

        Renderer visualRenderer = visual.GetComponent<Renderer>();

        if (visualRenderer != null)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            visualRenderer.SetPropertyBlock(block);
        }

        visual.SetActive(false);
    }

    private void OnDisable()
    {
        Hide();
    }

    private void Update()
    {
        if (hideAtTime >= 0f && Time.time >= hideAtTime) Hide();
    }

    public void Flash()
    {
        Show();
        hideAtTime = Time.time + flashDuration;
    }

    public void Show()
    {
        hideAtTime = -1f;
        active = true;
        RefreshVisual();
    }

    public void Hide()
    {
        hideAtTime = -1f;
        active = false;
        RefreshVisual();
    }

    public void RefreshVisual()
    {
        if (visual != null) visual.SetActive(active && IsAllowed);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) RefreshVisual();
    }
#endif
}
