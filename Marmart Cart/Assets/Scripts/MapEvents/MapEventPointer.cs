using UnityEngine;

/// <summary>
/// Per-player pointer toward the currently active Map Event section.
///
/// Built to match the existing PitIndictor setup:
/// - Each pointer owns a Player Index (1-4).
/// - It finds Player1 / Player2 / Player3 / Player4 by tag after startup.
/// - It stays offset from that player's leading cart.
/// - MapEventManager only assigns/clears the current section target.
/// - Optional blinking is done by enabling/disabling the MeshRenderer.
/// </summary>
[DisallowMultipleComponent]
public class MapEventPointer : MonoBehaviour
{
    #region Identity

    [Header("Identity")]
    [Range(1, 4)]
    [SerializeField] private int playerIndex = 1;

    #endregion

    #region Placement Settings

    [Header("Settings")]
    [SerializeField] private float offsetDistance = 2f;
    [SerializeField] private float yOffset = -1f;

    #endregion

    #region References

    [Header("Refs (Auto)")]
    [SerializeField] private Transform player;

    [Header("Runtime Target")]
    [SerializeField] private Transform target;

    #endregion

    #region Rotation

    [Header("Rotation")]
    [Tooltip("Extra rotation around Y to fine-tune arrow facing.")]
    [SerializeField] private float yRotationOffset = 0f;

    [Tooltip("Base X rotation needed for the arrow mesh to face correctly.")]
    [SerializeField] private float baseXRotation = -90f;

    #endregion

    #region Blink

    [Header("Blink")]
    [SerializeField] private bool blink = true;

    [Tooltip("Seconds between visible / hidden toggles while an event is active.")]
    [Min(0.05f)]
    [SerializeField] private float blinkInterval = 0.25f;

    #endregion

    #region Runtime

    private MeshRenderer meshRenderer;
    private float nextBlinkTime;
    private bool blinkVisible = true;

    public bool HasTarget => target != null;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();

        if (meshRenderer == null)
        {
            Debug.LogError("[MapEventPointer] MeshRenderer is missing.", this);
        }

        SetVisible(false);
    }

    private void Start()
    {
        Invoke(nameof(RegisterPlayer), 0.1f);
    }

    private void Update()
    {
        if (target == null || player == null)
        {
            SetVisible(false);
            return;
        }

        UpdatePointerTransform();
        UpdateBlink();
    }

    #endregion

    #region Player Registration

    public void RegisterPlayer()
    {
        GameObject playerRef = GameObject.FindGameObjectWithTag($"Player{playerIndex}");
        player = playerRef != null ? playerRef.transform : null;

        if (player == null)
        {
            Debug.LogWarning($"[MapEventPointer] Player{playerIndex} not found.", this);
        }
    }

    public void SetPlayerIndex(int idx)
    {
        playerIndex = Mathf.Clamp(idx, 1, 4);
        RegisterPlayer();
    }

    #endregion

    #region Target API

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;

        if (target == null)
        {
            ClearTarget();
            return;
        }

        blinkVisible = true;
        nextBlinkTime = Time.time + blinkInterval;

        SetVisible(true);
    }

    public void ClearTarget()
    {
        target = null;
        blinkVisible = false;

        SetVisible(false);
    }

    #endregion

    #region Pointer Update

    private void UpdatePointerTransform()
    {
        Vector3 direction = target.position - player.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f) return;

        direction.Normalize();

        transform.position = player.position + direction * offsetDistance + Vector3.up * yOffset;

        float angleY = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        angleY += yRotationOffset;

        transform.rotation = Quaternion.Euler(baseXRotation, angleY, 0f);
    }

    private void UpdateBlink()
    {
        if (!blink)
        {
            blinkVisible = true;
            SetVisible(true);
            return;
        }

        if (Time.time < nextBlinkTime) return;

        blinkVisible = !blinkVisible;
        nextBlinkTime = Time.time + blinkInterval;

        SetVisible(blinkVisible);
    }

    private void SetVisible(bool visible)
    {
        if (meshRenderer != null) meshRenderer.enabled = visible;
    }

    #endregion
}
