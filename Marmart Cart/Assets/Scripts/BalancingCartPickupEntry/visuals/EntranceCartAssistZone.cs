using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class EntranceCartAssistZone : MonoBehaviour
{
    public enum EntryFace
    {
        Forward,
        Back,
        Right,
        Left
    }

    private const int MaxSupportedPlayers = 4;

    [SerializeField] private float claimLockoutDurationAtMatchStart = 30f;
    private float zoneActiveTime;

    [Header("References")]
    [SerializeField] private MatchBalanceManager matchBalanceManager;
    [SerializeField] private EntranceCartStockVisualizer stockVisualizer;

    [Header("Related Events")]
    [SerializeField] private GameEvent[] collectEmptyCartEvent = new GameEvent[MaxSupportedPlayers];

    [Header("Entry Rules")]
    [SerializeField] private EntryFace validEntryFace = EntryFace.Back;
    [SerializeField] private float entryVelocityDotThreshold = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool logEvents = true;
    [SerializeField] private Color zoneColor = new Color(0f, 1f, 1f, 0.15f);
    [SerializeField] private Color entryFaceColor = new Color(0f, 1f, 0f, 0.35f);
    [SerializeField] private Color arrowColor = Color.green;

    private BoxCollider boxCollider;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;

        zoneActiveTime = Time.time;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsClaimLockedByStartTimer())
        {
            if (logEvents)
                Debug.Log("[AssistZone] Claim blocked by start lockout.");
            return;
        }

        if (other.GetComponent<LeadingCartRaycaster>() == null)
            return;

        int playerId = other.GetComponentInParent<SnakeCartManager>().GetPlayerId();
        int playerIdx = playerId - 1;

        if (playerIdx < 0 || playerIdx >= MaxSupportedPlayers)
            return;

        if (!IsValidEntry(other))
        {
            if (logEvents)
                Debug.Log($"[AssistZone] Player {playerId} failed one-way entry check.");
            return;
        }
        Debug.Log($"[AssistZone] Player {playerId} passed one-way entry check.");
        if (matchBalanceManager == null || stockVisualizer == null)
            return;

        AssistEvaluationResult result = matchBalanceManager.EvaluatePlayerForAssist(
            playerId,
            stockVisualizer.CurrentStock
        );

        if (logEvents)
            Debug.Log($"[AssistZone] Eval Player {playerId}: {result}");

        if (!result.eligible)
            return;

        int consumed = stockVisualizer.ConsumeCarts(result.claimAmount);
        if (consumed <= 0)
            return;

        for (int i = 0; i < consumed; i++)
        {
            collectEmptyCartEvent[playerIdx]?.Raise();
        }

        matchBalanceManager.MarkPlayerClaimedAssist(playerId);

        if (logEvents)
            Debug.Log($"[AssistZone] Player {playerId} claimed {consumed} carts.");
    }

    private bool IsValidEntry(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if (rb == null)
            return true;

        Vector3 velocity = rb.linearVelocity;
        if (velocity.sqrMagnitude < 0.0001f)
            return true;

        Vector3 allowedDir = GetAllowedEntryDirectionWorld();
        float dot = Vector3.Dot(velocity.normalized, allowedDir);

        return dot >= entryVelocityDotThreshold;
    }

    private bool IsEnteringFromValidSide(Vector3 worldPosition)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition) - boxCollider.center;
        Vector3 half = boxCollider.size * 0.5f;
        float tolerance = 0.35f;

        switch (validEntryFace)
        {
            case EntryFace.Forward:
                return local.z >= half.z - tolerance;

            case EntryFace.Back:
                return local.z <= -half.z + tolerance;

            case EntryFace.Right:
                return local.x >= half.x - tolerance;

            case EntryFace.Left:
                return local.x <= -half.x + tolerance;
        }

        return false;
    }

    private Vector3 GetAllowedEntryDirectionWorld()
    {
        switch (validEntryFace)
        {
            case EntryFace.Forward: return transform.forward;
            case EntryFace.Back: return -transform.forward;
            case EntryFace.Right: return transform.right;
            case EntryFace.Left: return -transform.right;
        }

        return transform.forward;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
            return;

        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc == null)
            return;

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        Vector3 center = bc.center;
        Vector3 size = bc.size;
        Vector3 half = size * 0.5f;

        Gizmos.color = zoneColor;
        Gizmos.DrawCube(center, size);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(center, size);

        Gizmos.color = entryFaceColor;
        Vector3 faceCenter = center + GetEntryFaceLocalOffset(half);
        Vector3 faceSize = GetEntryFacePlaneSize(size);
        Gizmos.DrawCube(faceCenter, faceSize);

        Gizmos.matrix = oldMatrix;

        Vector3 worldFaceCenter = transform.TransformPoint(faceCenter);
        Vector3 worldDir = GetAllowedEntryDirectionWorld();

        Gizmos.color = arrowColor;
        Gizmos.DrawLine(worldFaceCenter, worldFaceCenter + worldDir * 2f);
        Gizmos.DrawSphere(worldFaceCenter + worldDir * 2f, 0.15f);
    }

    private Vector3 GetEntryFaceLocalOffset(Vector3 half)
    {
        switch (validEntryFace)
        {
            case EntryFace.Forward: return new Vector3(0f, 0f, half.z);
            case EntryFace.Back: return new Vector3(0f, 0f, -half.z);
            case EntryFace.Right: return new Vector3(half.x, 0f, 0f);
            case EntryFace.Left: return new Vector3(-half.x, 0f, 0f);
        }

        return new Vector3(0f, 0f, -half.z);
    }

    private Vector3 GetEntryFacePlaneSize(Vector3 size)
    {
        const float thickness = 0.05f;

        switch (validEntryFace)
        {
            case EntryFace.Forward:
            case EntryFace.Back:
                return new Vector3(size.x, size.y, thickness);

            case EntryFace.Right:
            case EntryFace.Left:
                return new Vector3(thickness, size.y, size.z);
        }

        return new Vector3(size.x, size.y, thickness);
    }
    private bool IsClaimLockedByStartTimer()
    {
        return Time.time - zoneActiveTime < claimLockoutDurationAtMatchStart;
    }
}