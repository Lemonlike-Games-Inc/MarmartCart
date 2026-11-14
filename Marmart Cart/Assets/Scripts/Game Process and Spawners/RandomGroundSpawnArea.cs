using UnityEngine;

/// <summary>
/// Random spawn-position sampler for Marmart Carts.
///
/// The GameObject Transform defines the ACTUAL DROP PLANE:
/// - transform X/Z = center of the rectangular random area;
/// - transform Y   = height where spawned carts/loot are released.
///
/// Raycast detection is separate:
/// - each random X/Z candidate starts from Transform Y + Raycast Start Height;
/// - the ray checks only Ground + InvisibleTop;
/// - first hit Ground       -> valid;
/// - first hit InvisibleTop -> reject and reroll.
///
/// Therefore objects can visibly drop from high above the arena while the
/// detection ray starts even higher than all InvisibleTop rejection volumes.
/// </summary>
[DisallowMultipleComponent]
public class RandomGroundSpawnArea : MonoBehaviour
{
    #region Area

    [Header("Drop Area")]
    [Min(0.1f)]
    [SerializeField] private float areaWidth = 20f;

    [Min(0.1f)]
    [SerializeField] private float areaLength = 20f;

    [Tooltip("If enabled, the rectangular area follows this Transform's Y rotation.")]
    [SerializeField] private bool useTransformYaw = true;

    #endregion

    #region Detection

    [Header("Spawn Validation Layers")]
    [Tooltip("Legal floor surfaces. First hit Ground = candidate accepted.")]
    [SerializeField] private LayerMask groundLayers;

    [Tooltip("Spawn-rejection trigger volumes. First hit InvisibleTop = candidate rejected.")]
    [SerializeField] private LayerMask invisibleTopLayers;

    [Header("Raycast Start")]
    [Tooltip(
        "Extra height ABOVE this component's Transform Y where validation rays begin. " +
        "The Transform Y itself is the object DROP height. This offset is detection-only."
    )]
    [Min(0.1f)]
    [SerializeField] private float raycastStartHeight = 10f;

    [Tooltip("Maximum downward validation distance from the raycast-start plane.")]
    [Min(0.1f)]
    [SerializeField] private float raycastDistance = 100f;

    #endregion

    #region Attempts / Debug

    [Header("Placement Attempts")]
    [Min(1)]
    [SerializeField] private int maxPlacementAttempts = 40;

    [Header("Runtime - Read Only")]
    [SerializeField] private Vector3 lastRaycastOrigin;
    [SerializeField] private Vector3 lastGroundHitPoint;
    [SerializeField] private Vector3 lastDropPosition;
    [SerializeField] private bool lastCandidateWasValid;

    [Header("Debug")]
    [SerializeField] private bool logFailedPlacement;

    #endregion

    #region Public API

    public float AreaWidth => areaWidth;
    public float AreaLength => areaLength;
    public bool UsesTransformYaw => useTransformYaw;

    /// <summary>
    /// Tests the same authored horizontal rectangle used by spawn sampling.
    /// World Y is deliberately ignored. Positive padding expands the rectangle.
    /// </summary>
    public bool ContainsHorizontalPoint(Vector3 worldPoint, float padding = 0f)
    {
        GetHorizontalAxes(out Vector3 right, out Vector3 forward);

        Vector3 fromCenter = worldPoint - transform.position;
        float localX = Vector3.Dot(fromCenter, right);
        float localZ = Vector3.Dot(fromCenter, forward);
        float safePadding = Mathf.Max(0f, padding);

        return
            Mathf.Abs(localX) <= areaWidth * 0.5f + safePadding &&
            Mathf.Abs(localZ) <= areaLength * 0.5f + safePadding;
    }

    /// <summary>
    /// Finds a legal X/Z using a downward Ground/InvisibleTop ray, then returns
    /// that X/Z at THIS COMPONENT'S Transform Y so the prefab is released from
    /// the authored drop plane rather than appearing on the ground.
    /// </summary>
    public bool TryGetValidDropPosition(out Vector3 dropPosition)
    {
        int raycastMask = groundLayers.value | invisibleTopLayers.value;

        if (raycastMask == 0)
        {
            dropPosition = Vector3.zero;

            if (logFailedPlacement)
            {
                Debug.LogWarning("[RandomGroundSpawnArea] Ground Layers + Invisible Top Layers are both empty.", this);
            }

            return false;
        }

        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            Vector3 candidateOnDropPlane = GetRandomPointOnDropPlane();
            Vector3 rayOrigin = candidateOnDropPlane + Vector3.up * raycastStartHeight;

            lastRaycastOrigin = rayOrigin;

            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDistance, raycastMask, QueryTriggerInteraction.Collide))
            {
                lastCandidateWasValid = false;
                continue;
            }

            int hitLayer = hit.collider.gameObject.layer;

            if (LayerMaskContains(invisibleTopLayers, hitLayer))
            {
                lastCandidateWasValid = false;
                continue;
            }

            if (!LayerMaskContains(groundLayers, hitLayer))
            {
                lastCandidateWasValid = false;
                continue;
            }

            lastCandidateWasValid = true;
            lastGroundHitPoint = hit.point;

            dropPosition = new Vector3(hit.point.x, transform.position.y, hit.point.z);
            lastDropPosition = dropPosition;
            return true;
        }

        dropPosition = Vector3.zero;

        if (logFailedPlacement)
        {
            Debug.LogWarning($"[RandomGroundSpawnArea] Failed to find a valid drop position after {maxPlacementAttempts} attempts.", this);
        }

        return false;
    }

    #endregion

    #region Sampling

    private Vector3 GetRandomPointOnDropPlane()
    {
        float x = Random.Range(-areaWidth * 0.5f, areaWidth * 0.5f);
        float z = Random.Range(-areaLength * 0.5f, areaLength * 0.5f);

        GetHorizontalAxes(out Vector3 right, out Vector3 forward);

        return transform.position + right * x + forward * z;
    }

    private void GetHorizontalAxes(out Vector3 right, out Vector3 forward)
    {
        if (!useTransformYaw)
        {
            right = Vector3.right;
            forward = Vector3.forward;
            return;
        }

        right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        if (right.sqrMagnitude < 0.001f) right = Vector3.right;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
    }

    private bool LayerMaskContains(LayerMask mask, int layer)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    #endregion

    #region Validation

    private void OnValidate()
    {
        areaWidth = Mathf.Max(0.1f, areaWidth);
        areaLength = Mathf.Max(0.1f, areaLength);
        raycastStartHeight = Mathf.Max(0.1f, raycastStartHeight);
        raycastDistance = Mathf.Max(0.1f, raycastDistance);
        maxPlacementAttempts = Mathf.Max(1, maxPlacementAttempts);
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        GetHorizontalAxes(out Vector3 right, out Vector3 forward);

        Vector3 dropCenter = transform.position;
        Vector3 rayCenter = dropCenter + Vector3.up * raycastStartHeight;

        Vector3 halfRight = right * (areaWidth * 0.5f);
        Vector3 halfForward = forward * (areaLength * 0.5f);

        Vector3 dropA = dropCenter - halfRight - halfForward;
        Vector3 dropB = dropCenter + halfRight - halfForward;
        Vector3 dropC = dropCenter + halfRight + halfForward;
        Vector3 dropD = dropCenter - halfRight + halfForward;

        Vector3 rayA = dropA + Vector3.up * raycastStartHeight;
        Vector3 rayB = dropB + Vector3.up * raycastStartHeight;
        Vector3 rayC = dropC + Vector3.up * raycastStartHeight;
        Vector3 rayD = dropD + Vector3.up * raycastStartHeight;

        // Green = ACTUAL prefab release/drop plane.
        Gizmos.color = Color.green;
        DrawRectangle(dropA, dropB, dropC, dropD);
        Gizmos.DrawWireSphere(dropCenter, 0.2f);

        // Cyan = validation-ray start plane.
        Gizmos.color = Color.cyan;
        DrawRectangle(rayA, rayB, rayC, rayD);
        Gizmos.DrawWireSphere(rayCenter, 0.2f);

        Gizmos.DrawLine(dropA, rayA);
        Gizmos.DrawLine(dropB, rayB);
        Gizmos.DrawLine(dropC, rayC);
        Gizmos.DrawLine(dropD, rayD);

        // Yellow = representative validation ray.
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(rayCenter, rayCenter + Vector3.down * raycastDistance);

        // Forward marker.
        Gizmos.color = Color.white;
        Gizmos.DrawLine(dropCenter, dropCenter + forward * Mathf.Min(2f, areaLength * 0.25f));
    }

    private void DrawRectangle(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d);
        Gizmos.DrawLine(d, a);
    }

    #endregion
}
