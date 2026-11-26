using UnityEngine;

/// <summary>
/// TEMPORARY. Master switch for every CartFeedbackPlaceholder under this
/// Cart Feedback root. Each placeholder also has its own Show box; a
/// placeholder is visible only when both are ticked. Delete together with
/// the placeholders.
/// </summary>
[DisallowMultipleComponent]
public class CartFeedbackPlaceholderSwitch : MonoBehaviour
{
    [SerializeField] private bool showPlaceholders = true;

    public bool ShowPlaceholders => showPlaceholders;

#if UNITY_EDITOR
    // Lets the box be toggled live in Play Mode on a runtime cart.
    private void OnValidate()
    {
        if (!Application.isPlaying) return;

        CartFeedbackPlaceholder[] placeholders = GetComponentsInChildren<CartFeedbackPlaceholder>(true);

        for (int i = 0; i < placeholders.Length; i++) placeholders[i].RefreshVisual();
    }
#endif
}
