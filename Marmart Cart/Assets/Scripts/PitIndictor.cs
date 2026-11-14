using UnityEngine;

public class PitIndictor : MonoBehaviour
{
    [Header("Identity")]
    [Range(1, 4)]
    [SerializeField] private int playerIndex = 1; // 1..4

    [Header("Settings")]
    [SerializeField] private float offsetDistance = 2f;
    [SerializeField] private float yOffset = -1f; // your old Vector3.up * -1f

    [Header("Refs (auto)")]
    [SerializeField] private Transform player;
    [SerializeField] private GameObject pit;

    [Header("Rotation")]
    [Tooltip("Extra rotation around Y to fine-tune arrow facing")]
    [SerializeField] private float yRotationOffset = 0f;

    [Tooltip("Base X rotation needed for the mesh to look correct (your -90)")]
    [SerializeField] private float baseXRotation = -90f;

    private MeshRenderer _mr;

    private void Awake()
    {
        _mr = GetComponent<MeshRenderer>();
    }

    private void Start()
    {
        pit = GameObject.FindGameObjectWithTag("CheckOutStation");
        Invoke(nameof(RegisterPlayer), 0.1f);
    }

    public void RegisterPlayer()
    {
        var playerRef = GameObject.FindGameObjectWithTag($"Player{playerIndex}");
        player = playerRef ? playerRef.transform : null;

        if (!player)
            Debug.LogWarning($"PitIndictor: Player{playerIndex} not found.");
    }

    private void Update()
    {
        if (pit == null || player == null)
        {
            if (_mr) _mr.enabled = false;
            return;
        }

        if (_mr) _mr.enabled = true;

        Vector3 pitPos = pit.transform.position;

        // Direction from player to pit (XZ only)
        Vector3 direction = pitPos - player.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        direction.Normalize();

        // Position: offset toward pit from player
        transform.position = player.position + direction * offsetDistance + Vector3.up * yOffset;

        // Rotation: yaw toward pit + optional tweak
        float angleY = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        angleY += yRotationOffset;

        transform.rotation = Quaternion.Euler(baseXRotation, angleY, 0f);
    }

    public void SetPlayerIndex(int idx)
    {
        playerIndex = Mathf.Clamp(idx, 1, 4);
        RegisterPlayer();
    }
}
