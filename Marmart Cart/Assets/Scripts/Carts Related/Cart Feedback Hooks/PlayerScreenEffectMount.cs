using UnityEngine;

/// <summary>
/// Puts one screen-space UI prefab (speed lines, hit flash, vignette...) into
/// this cart's player Effects layer of the ViewportCanvasSystem, so it is
/// clipped to that player's viewport only. Lives in the Cart Feedback prefab.
///
/// The instance starts hidden. Call Show/Hide (Inspector events work), or
/// drive Instance from a script. It is destroyed with the cart.
/// </summary>
[DisallowMultipleComponent]
public class PlayerScreenEffectMount : MonoBehaviour
{
    [Tooltip("A UI prefab with a RectTransform root. Usually stretched to fill the player's view.")]
    [SerializeField] private RectTransform screenEffectPrefab;

    private RectTransform instance;

    /// <summary>The spawned effect, or null before Start or outside a gameplay scene.</summary>
    public RectTransform Instance => instance;

    private void Start()
    {
        if (screenEffectPrefab == null) return;

        LeadingCartFeedbackRelay relay = GetComponentInParent<LeadingCartFeedbackRelay>();
        int playerIndex = relay != null ? relay.PlayerIndex : 0;

        // Player 0 = not a live player cart (for example the results podium).
        if (playerIndex <= 0) return;

        ViewportCanvasSystem canvasSystem = ViewportCanvasSystem.Instance;

        if (canvasSystem == null ||
            !canvasSystem.TryGetPlayerLayer(playerIndex, ViewportCanvasSystem.PlayerLayer.Effects, out RectTransform layerRoot))
        {
            Debug.LogWarning($"[CartFeedback] No Effects layer for P{playerIndex}; screen effect '{name}' not created.", this);
            return;
        }

        instance = Instantiate(screenEffectPrefab, layerRoot, false);
        instance.name = $"{screenEffectPrefab.name} (P{playerIndex})";
        instance.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        Hide();
    }

    private void OnDestroy()
    {
        if (instance != null) Destroy(instance.gameObject);
    }

    public void Show()
    {
        if (instance != null) instance.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (instance != null) instance.gameObject.SetActive(false);
    }
}
