using UnityEngine;

public class WorldAnchorUIFollower : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera worldCamera;          // your gameplay camera
    [SerializeField] private Transform worldAnchor;       // cart anchor (above cart)
    [SerializeField] private RectTransform uiRect;        // this UI element rect
    [SerializeField] private Canvas canvas;               // parent canvas

    [Header("Tuning")]
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 30f);
    [SerializeField] private bool hideWhenOffscreen = true;

    void Reset()
    {
        uiRect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        worldCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (!worldCamera || !worldAnchor || !uiRect || !canvas) return;

        Vector3 screenPos = worldCamera.WorldToScreenPoint(worldAnchor.position);

        // behind camera
        if (screenPos.z < 0f)
        {
            if (hideWhenOffscreen) uiRect.gameObject.SetActive(false);
            return;
        }

        bool onScreen = screenPos.x >= 0 && screenPos.x <= Screen.width &&
                        screenPos.y >= 0 && screenPos.y <= Screen.height;

        if (hideWhenOffscreen) uiRect.gameObject.SetActive(onScreen);
        if (!onScreen && hideWhenOffscreen) return;

        // Overlay canvas: screen pixels -> anchoredPosition
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            uiRect.position = (Vector2)screenPos + screenOffset;
        }
        else
        {
            // Screen Space - Camera or World Space canvas:
            RectTransform canvasRect = canvas.transform as RectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPos,
                canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : worldCamera,
                out Vector2 localPoint
            );
            uiRect.anchoredPosition = localPoint + screenOffset;
        }
    }

    public void Bind(Transform anchor, Camera cam)
    {
        worldAnchor = anchor;
        worldCamera = cam;
    }
}
