using UnityEngine;

/// <summary>
/// Simple 2-player pointer that always points from its owning player
/// toward the other player.
///
/// Player Index = 1 -> points to Player2
/// Player Index = 2 -> points to Player1
///
/// Built to match the existing PitIndictor / MapEventPointer setup.
/// </summary>
[DisallowMultipleComponent]
public class OtherPlayerPointer : MonoBehaviour
{
    #region Identity

    [Header("Identity")]
    [Range(1, 2)]
    [SerializeField] private int playerIndex = 1;

    #endregion

    #region Settings

    [Header("Settings")]
    [SerializeField] private float offsetDistance = 2f;
    [SerializeField] private float yOffset = -1f;

    #endregion

    #region References

    [Header("Refs (Auto)")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform otherPlayer;

    #endregion

    #region Rotation

    [Header("Rotation")]
    [Tooltip("Extra rotation around Y to fine-tune arrow facing.")]
    [SerializeField] private float yRotationOffset = 0f;

    [Tooltip("Base X rotation needed for the arrow mesh to face correctly.")]
    [SerializeField] private float baseXRotation = -90f;

    #endregion

    #region Runtime

    private MeshRenderer meshRenderer;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();

        if (meshRenderer == null)
        {
            Debug.LogError("[OtherPlayerPointer] MeshRenderer is missing.", this);
        }
    }

    private void Start()
    {
        Invoke(nameof(RegisterPlayers), 0.1f);
    }

    private void Update()
    {
        if (player == null || otherPlayer == null)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);
        UpdatePointerTransform();
    }

    #endregion

    #region Player Registration

    public void RegisterPlayers()
    {
        int otherPlayerIndex = playerIndex == 1 ? 2 : 1;

        GameObject playerRef = GameObject.FindGameObjectWithTag($"Player{playerIndex}");
        GameObject otherPlayerRef = GameObject.FindGameObjectWithTag($"Player{otherPlayerIndex}");

        player = playerRef != null ? playerRef.transform : null;
        otherPlayer = otherPlayerRef != null ? otherPlayerRef.transform : null;

        if (player == null)
        {
            Debug.LogWarning($"[OtherPlayerPointer] Player{playerIndex} not found.", this);
        }

        if (otherPlayer == null)
        {
            Debug.LogWarning($"[OtherPlayerPointer] Player{otherPlayerIndex} not found.", this);
        }
    }

    public void SetPlayerIndex(int idx)
    {
        playerIndex = Mathf.Clamp(idx, 1, 2);
        RegisterPlayers();
    }

    #endregion

    #region Pointer Update

    private void UpdatePointerTransform()
    {
        Vector3 direction = otherPlayer.position - player.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f) return;

        direction.Normalize();

        transform.position = player.position + direction * offsetDistance + Vector3.up * yOffset;

        float angleY = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        angleY += yRotationOffset;

        transform.rotation = Quaternion.Euler(baseXRotation, angleY, 0f);
    }

    private void SetVisible(bool visible)
    {
        if (meshRenderer != null) meshRenderer.enabled = visible;
    }

    #endregion
}
