using UnityEngine;

public class OtherPlayerIndictor : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool isForPlayer1 = true;
    [SerializeField] private float offsetDistance = 2f;

    [SerializeField] private Transform thisPlayer;
    [SerializeField] private Transform otherPlayer;
    [Header("Rotation")]
    [Tooltip("Extra rotation around Y to fine-tune arrow facing")]
    [SerializeField] private float yRotationOffset = 0f;

    [Tooltip("Base X rotation needed for the mesh (e.g. -90)")]
    [SerializeField] private float baseXRotation = -90f;
    private void Start()
    {
        Invoke(nameof(RegisterPlayer), 2f);
    }
    public void RegisterPlayer()
    {
        if (isForPlayer1)
        {
            thisPlayer = GameObject.FindGameObjectWithTag("Player1").transform;
            otherPlayer = GameObject.FindGameObjectWithTag("Player2").transform;
        }
        else
        {
            thisPlayer = GameObject.FindGameObjectWithTag("Player2").transform;
            otherPlayer = GameObject.FindGameObjectWithTag("Player1").transform;
        }
    }
    private void Update()
    {
        if (thisPlayer == null || otherPlayer == null)
            return;

        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null)
            mr.enabled = true;

        // --- POSITION ---+
        Vector3 direction = otherPlayer.position - thisPlayer.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        direction.Normalize();
        transform.position = thisPlayer.position + direction * offsetDistance + Vector3.up * -1f;

        // --- ROTATION (same as PitIndicator) ---
        float angleY = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        angleY += yRotationOffset;

        transform.rotation = Quaternion.Euler(baseXRotation, angleY, 0f);
    }
}
