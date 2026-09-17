using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pooled, Rigidbody-free Ice ground hazard. Its authored BoxCollider is used
/// only as the designer-visible source of overlap shape; the physical collider
/// is disabled at runtime and Physics.OverlapBoxNonAlloc performs detection.
/// </summary>
[DisallowMultipleComponent]
public class IceGroundHazard : MonoBehaviour
{
    [Header("Designer-Authored Detection Shape")]
    [Tooltip(
        "BoxCollider fitted in Prefab Mode. It may live on this object or a " +
        "child. Runtime disables it and copies its exact center, rotation, " +
        "size, and scale into non-allocating overlap checks."
    )]
    [SerializeField] private BoxCollider authoredHitbox;

    [Header("Diagnostics")]
    [SerializeField] private bool drawRuntimeHitbox = true;
    [SerializeField]
    private Color runtimeHitboxColor =
        new Color(0.2f, 0.85f, 1f, 0.75f);

    [Header("Runtime - Read Only")]
    [SerializeField] private bool activeHazard;
    [SerializeField] private bool presentationFading;
    [SerializeField] private uint activationVersion;
    [SerializeField] private uint effectInstanceId;
    [SerializeField] private uint effectVersion;
    [SerializeField] private int sourcePatternEntryIndex;
    [SerializeField] private int ownerPlayerIndex;
    [SerializeField] private float durationSeconds;
    [SerializeField] private float startedAtTime;
    [SerializeField] private float endsAtTime;

    private readonly Collider[] overlapBuffer = new Collider[32];
    private readonly HashSet<PowerupCartTarget> evaluatedTargets =
        new HashSet<PowerupCartTarget>();

    private IceGroundHazardPool owningPool;
    private IceFreezeEffectSystem freezeEffectSystem;
    private IceVisualFadeController visualFadeController;
    private PlayerPowerupController ownerController;
    private LayerMask cartTargetMask;
    private Vector3 authoredLocalScale = Vector3.one;
    private bool authoredScaleCaptured;
    private bool canAffectLeadingCarts;
    private bool overlapOverflowLogged;
    private int visualStartAlphaByte = 175;

    public bool IsActiveHazard => activeHazard;
    public bool IsPresentationFading => presentationFading;
    public bool HasValidAuthoredHitbox
    {
        get
        {
            ResolveAuthoredHitbox();
            return authoredHitbox != null;
        }
    }

    public uint ActivationVersion => activationVersion;
    public uint EffectInstanceId => effectInstanceId;
    public uint EffectVersion => effectVersion;
    public int SourcePatternEntryIndex => sourcePatternEntryIndex;
    public int OwnerPlayerIndex => ownerPlayerIndex;
    public PlayerPowerupController OwnerController => ownerController;
    public float DurationSeconds => durationSeconds;
    public float StartedAtTime => startedAtTime;
    public float EndsAtTime => endsAtTime;

    private void Awake()
    {
        CaptureAuthoredScale();
        ResolveAuthoredHitbox();
        ResolveVisualFadeController();
        DisablePhysicalHitbox();
    }

    private void Update()
    {
        if (!activeHazard) return;

        if (Time.time >= endsAtTime)
        {
            owningPool?.ReturnExpired(this);
        }
    }

    private void FixedUpdate()
    {
        if (!activeHazard ||
            !canAffectLeadingCarts ||
            authoredHitbox == null ||
            freezeEffectSystem == null)
        {
            return;
        }

        EvaluateLeadingCartOverlaps();
    }

    internal void InitializeForPool(IceGroundHazardPool pool)
    {
        owningPool = pool;
        CaptureAuthoredScale();
        ResolveAuthoredHitbox();
        ResolveVisualFadeController();
        DisablePhysicalHitbox();
        ResetForPool();
    }

    internal bool Activate(
        IceFreezeEffectSystem freezeSystem,
        LayerMask targetMask,
        bool affectLeadingCarts,
        uint sourceActivationVersion,
        uint hazardEffectInstanceId,
        int patternEntryIndex,
        int sourcePlayerIndex,
        PlayerPowerupController sourceController,
        Vector3 position,
        Quaternion rotation,
        Vector3 scaleMultiplier,
        float lifetimeSeconds,
        int startAlphaByte)
    {
        ResolveAuthoredHitbox();
        ResolveVisualFadeController();

        if (owningPool == null ||
            freezeSystem == null ||
            authoredHitbox == null ||
            targetMask.value == 0)
        {
            return false;
        }

        freezeEffectSystem = freezeSystem;
        cartTargetMask = targetMask;
        canAffectLeadingCarts = affectLeadingCarts;
        activationVersion = sourceActivationVersion;
        effectInstanceId = hazardEffectInstanceId;
        effectVersion = 1u;
        sourcePatternEntryIndex = patternEntryIndex;
        ownerPlayerIndex = sourcePlayerIndex;
        ownerController = sourceController;
        durationSeconds = Mathf.Max(0.05f, lifetimeSeconds);
        startedAtTime = Time.time;
        endsAtTime = startedAtTime + durationSeconds;
        overlapOverflowLogged = false;
        presentationFading = false;
        visualStartAlphaByte = Mathf.Clamp(startAlphaByte, 0, 255);

        transform.SetPositionAndRotation(position, rotation);
        transform.localScale = Vector3.Scale(
            authoredLocalScale,
            new Vector3(
                Mathf.Max(0.0001f, scaleMultiplier.x),
                Mathf.Max(0.0001f, scaleMultiplier.y),
                Mathf.Max(0.0001f, scaleMultiplier.z)
            )
        );

        DisablePhysicalHitbox();
        activeHazard = true;
        gameObject.SetActive(true);
        visualFadeController?.PrepareForUse(visualStartAlphaByte);
        return true;
    }

    internal void IncrementEffectVersion()
    {
        effectVersion = NextRevision(effectVersion);
    }

    internal void MarkGameplayEndingNow()
    {
        activeHazard = false;
        endsAtTime = Mathf.Min(endsAtTime, Time.time);
    }

    internal void BeginPresentationFade(
        float durationSeconds,
        Action<IceGroundHazard> onCompleted)
    {
        presentationFading = true;
        ResolveVisualFadeController();

        if (visualFadeController == null)
        {
            presentationFading = false;
            onCompleted?.Invoke(this);
            return;
        }

        visualFadeController.BeginFade(
            durationSeconds,
            () =>
            {
                presentationFading = false;
                onCompleted?.Invoke(this);
            }
        );
    }

    internal void ResetForPool()
    {
        activeHazard = false;
        presentationFading = false;
        freezeEffectSystem = null;
        ownerController = null;
        cartTargetMask = default;
        canAffectLeadingCarts = false;
        activationVersion = 0u;
        effectInstanceId = 0u;
        effectVersion = 0u;
        sourcePatternEntryIndex = 0;
        ownerPlayerIndex = 0;
        durationSeconds = 0f;
        startedAtTime = 0f;
        endsAtTime = 0f;
        overlapOverflowLogged = false;
        evaluatedTargets.Clear();

        transform.localScale = authoredLocalScale;
        visualFadeController?.ResetForPool(visualStartAlphaByte);
        visualStartAlphaByte = 175;
        DisablePhysicalHitbox();
        gameObject.SetActive(false);
    }

    private void EvaluateLeadingCartOverlaps()
    {
        GetWorldBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation);

        int hitCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            overlapBuffer,
            rotation,
            cartTargetMask,
            QueryTriggerInteraction.Collide
        );

        if (hitCount >= overlapBuffer.Length && !overlapOverflowLogged)
        {
            overlapOverflowLogged = true;
            Debug.LogWarning(
                "[IceGroundHazard] Overlap buffer filled completely. " +
                "Reduce colliders in the Cart mask or increase the buffer size.",
                this
            );
        }

        evaluatedTargets.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = overlapBuffer[i];
            overlapBuffer[i] = null;

            if (!PowerupCartTarget.TryResolveFromCollider(
                    hitCollider,
                    out PowerupCartTarget target) ||
                !evaluatedTargets.Add(target) ||
                !target.TryGetGameplayTarget(
                    out PowerupCartTargetSnapshot snapshot) ||
                snapshot.Kind != PowerupCartTargetKind.LeadingCart ||
                snapshot.IsInCheckout ||
                snapshot.LeadingCartControl == null)
            {
                continue;
            }

            if (freezeEffectSystem.TryApplyFreeze(
                    new IceFreezeRequest
                    {
                        ActivationVersion = activationVersion,
                        SourcePatternEntryIndex =
                            sourcePatternEntryIndex,
                        SourcePlayerIndex = ownerPlayerIndex,
                        SourceController = ownerController,
                        TargetPlayerIndex = snapshot.PlayerIndex,
                        TargetController = snapshot.OwnerController,
                        TargetCartControl = snapshot.LeadingCartControl,
                        FromGroundHazard = true,
                        ImpactPosition = ClosestPointOrCenter(
                            hitCollider,
                            center
                        )
                    }))
            {
                owningPool?.ReturnTriggered(
                    this,
                    snapshot,
                    hitCollider,
                    ClosestPointOrCenter(hitCollider, center)
                );
                return;
            }
        }
    }

    private void ResolveAuthoredHitbox()
    {
        if (authoredHitbox != null) return;

        authoredHitbox =
            GetComponent<BoxCollider>() ??
            GetComponentInChildren<BoxCollider>(true);
    }

    private void ResolveVisualFadeController()
    {
        if (visualFadeController != null) return;

        visualFadeController = GetComponent<IceVisualFadeController>();

        if (visualFadeController == null)
        {
            visualFadeController =
                gameObject.AddComponent<IceVisualFadeController>();
        }
    }

    private void CaptureAuthoredScale()
    {
        if (authoredScaleCaptured) return;

        authoredLocalScale = transform.localScale;
        authoredScaleCaptured = true;
    }

    private void DisablePhysicalHitbox()
    {
        if (authoredHitbox != null)
        {
            authoredHitbox.enabled = false;
        }
    }

    private void GetWorldBox(
        out Vector3 center,
        out Vector3 halfExtents,
        out Quaternion rotation)
    {
        Transform hitboxTransform = authoredHitbox.transform;
        Vector3 lossyScale = hitboxTransform.lossyScale;

        center = hitboxTransform.TransformPoint(authoredHitbox.center);
        halfExtents = Vector3.Scale(
            authoredHitbox.size * 0.5f,
            new Vector3(
                Mathf.Abs(lossyScale.x),
                Mathf.Abs(lossyScale.y),
                Mathf.Abs(lossyScale.z)
            )
        );
        halfExtents.x = Mathf.Max(0.0001f, halfExtents.x);
        halfExtents.y = Mathf.Max(0.0001f, halfExtents.y);
        halfExtents.z = Mathf.Max(0.0001f, halfExtents.z);
        rotation = hitboxTransform.rotation;
    }

    private static Vector3 ClosestPointOrCenter(
        Collider collider,
        Vector3 center)
    {
        return collider != null ? collider.ClosestPoint(center) : center;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawRuntimeHitbox) return;

        ResolveAuthoredHitbox();
        if (authoredHitbox == null) return;

        GetWorldBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation);

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
        Gizmos.color = runtimeHitboxColor;
        Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    private static uint NextRevision(uint currentRevision)
    {
        uint nextRevision = unchecked(currentRevision + 1u);
        return nextRevision == 0u ? 1u : nextRevision;
    }
}
