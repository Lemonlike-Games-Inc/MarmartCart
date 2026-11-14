using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class AimDirectionVisualizer : MonoBehaviour
{
    [Header("Identity")]
    [Range(1, 4)]
    [SerializeField] private int playerIndex = 1; // 1..4

    [Header("Settings")]
    [SerializeField] private float lineLength = 5f;
    [SerializeField] private Vector3 startOffset = new Vector3(0f, 0.2f, 0f);

    [Header("Runtime Refs")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private CartControlScript cartController;

    private LineRenderer lineRenderer;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.enabled = false;
    }

    private void Start()
    {
        Invoke(nameof(RegisterPlayer), 0.1f);
    }

    public void RegisterPlayer()
    {
        var playerRef = GameObject.FindGameObjectWithTag($"Player{playerIndex}");
        if (!playerRef)
        {
            Debug.LogError($"AimDirectionVisualizer: Player{playerIndex} not found.");
            return;
        }

        playerTransform = playerRef.transform;
        cartController = playerRef.GetComponentInChildren<CartControlScript>(true);

        if (!cartController)
            Debug.LogError($"AimDirectionVisualizer: CartControlScript not found under Player{playerIndex}.");
    }

    private void Update()
    {
        if (!cartController || !cartController.enabled || !playerTransform)
        {
            lineRenderer.enabled = false;
            return;
        }

        if (!cartController.GetCanAim())
        {
            lineRenderer.enabled = false;
            return;
        }

        Vector3 dir = cartController.AimDirection;
        if (dir.sqrMagnitude < 0.0001f)
            dir = new Vector3(0f, 0f, 0f);

        dir.y = 0f;

        dir.Normalize();

        Vector3 start = playerTransform.position + startOffset;
        Vector3 end = start + dir * lineLength;

        lineRenderer.enabled = true;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
    }

    public void SetPlayerIndex(int idx)
    {
        playerIndex = Mathf.Clamp(idx, 1, 4);
        RegisterPlayer();
    }
}