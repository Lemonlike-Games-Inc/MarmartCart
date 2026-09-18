using Shapes;
using UnityEngine;

public enum ArenaDirectionalIndicatorPreviewMode
{
    RuntimeState = 0,
    ZoneGuidance = 1,
    CheckoutAvailability = 2
}

/// <summary>
/// Draws shared world-space arena guidance using Shapes.
///
/// Runtime visibility is event-driven by MatchFlowDirector. Geometry is
/// generated procedurally from one World Center transform and one profile;
/// no individual arrow GameObjects or per-arrow Transform references exist.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class ArenaDirectionalIndicatorRenderer : ImmediateModeShapeDrawer
{
    #region References

    [Header("References")]
    [SerializeField] private MatchFlowDirector matchFlowDirector;

    [Tooltip(
        "Position is the arena center. Forward is logical North, Right is " +
        "logical East, and Up lifts the arrows above the floor."
    )]
    [SerializeField] private Transform worldCenter;

    [SerializeField]
    private ArenaDirectionalIndicatorProfile profile;

    #endregion

    #region Camera / Drawing

    [Header("Drawing")]
    [SerializeField] private bool drawingEnabled = true;
    [SerializeField] private bool drawInGameCameras = true;
    [SerializeField] private bool drawInSceneView = true;

    [Tooltip(
        "When enabled, a Game camera draws the arrows only if its Culling " +
        "Mask includes this renderer GameObject's layer."
    )]
    [SerializeField] private bool respectRendererGameObjectLayer = true;

    #endregion

    #region Preview Override

    [Header("Editor / Debug Preview Override")]
    [Tooltip(
        "Runtime State follows MatchFlowDirector. The other two options " +
        "override match state so the layout can be tuned directly. Return " +
        "this to Runtime State before final gameplay testing."
    )]
    [SerializeField]
    private ArenaDirectionalIndicatorPreviewMode previewMode =
        ArenaDirectionalIndicatorPreviewMode.RuntimeState;

    [SerializeField] private ArenaZoneId previewZone = ArenaZoneId.ZoneA;

    [SerializeField]
    private CheckoutStationMask previewCheckoutStations =
        CheckoutStationMask.All;

    #endregion

    #region Runtime Diagnostics

    [Header("Runtime - Read Only")]
    [SerializeField] private MatchFlowSessionPhase currentPhase;
    [SerializeField] private MatchFlowSessionType currentSessionType;
    [SerializeField] private ArenaZoneId currentZone;
    [SerializeField] private CheckoutStationMask currentCheckoutStations;
    [SerializeField] private bool guidanceVisible;
    [SerializeField] private bool checkoutVisible;
    [SerializeField] private int lastDrawnArrowCount;
    [SerializeField] private string lastDrawCameraName;

    private MatchFlowDirector subscribedDirector;

    public MatchFlowSessionPhase CurrentPhase => currentPhase;
    public bool GuidanceVisible => guidanceVisible;
    public bool CheckoutVisible => checkoutVisible;
    public int LastDrawnArrowCount => lastDrawnArrowCount;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        worldCenter = transform;
        ResolveDirectorReference();
    }

    private void Awake()
    {
        ResolveDirectorReference();
    }

    private void Start()
    {
        if (!Application.isPlaying) return;

        ResolveDirectorReference();
        SubscribeToDirector();
        SynchronizeFromDirector();
        ValidateRuntimeSetup();
    }

    private void OnDestroy()
    {
        UnsubscribeFromDirector();
    }

    #endregion

    #region Match Flow Subscription

    private void ResolveDirectorReference()
    {
        if (matchFlowDirector == null)
        {
            matchFlowDirector = FindFirstObjectByType<MatchFlowDirector>();
        }
    }

    private void SubscribeToDirector()
    {
        if (matchFlowDirector == null) return;

        if (subscribedDirector == matchFlowDirector) return;

        UnsubscribeFromDirector();

        subscribedDirector = matchFlowDirector;
        subscribedDirector.OnSessionPhaseChanged += HandleSessionPhaseChanged;
    }

    private void UnsubscribeFromDirector()
    {
        if (subscribedDirector != null)
        {
            subscribedDirector.OnSessionPhaseChanged -=
                HandleSessionPhaseChanged;
        }

        subscribedDirector = null;
    }

    private void SynchronizeFromDirector()
    {
        if (matchFlowDirector == null)
        {
            ClearRuntimeState();
            return;
        }

        ApplyRuntimeState(
            matchFlowDirector.CurrentSession,
            matchFlowDirector.CurrentSessionPhase
        );
    }

    private void HandleSessionPhaseChanged(
        int sessionIndex,
        MatchFlowSession session,
        MatchFlowSessionPhase phase
    )
    {
        ApplyRuntimeState(session, phase);
    }

    private void ApplyRuntimeState(
        MatchFlowSession session,
        MatchFlowSessionPhase phase
    )
    {
        currentPhase = phase;

        if (session == null || phase == MatchFlowSessionPhase.None)
        {
            currentSessionType = session != null
                ? session.type
                : MatchFlowSessionType.FreePlay;

            currentZone = session != null
                ? session.zone
                : ArenaZoneId.ZoneA;

            currentCheckoutStations = CheckoutStationMask.None;
            RefreshRuntimeVisibility();
            return;
        }

        currentSessionType = session.type;
        currentZone = session.zone;
        currentCheckoutStations = session.checkoutStations;

        RefreshRuntimeVisibility();
    }

    private void ClearRuntimeState()
    {
        currentPhase = MatchFlowSessionPhase.None;
        currentSessionType = MatchFlowSessionType.FreePlay;
        currentZone = ArenaZoneId.ZoneA;
        currentCheckoutStations = CheckoutStationMask.None;
        guidanceVisible = false;
        checkoutVisible = false;
        lastDrawnArrowCount = 0;
        lastDrawCameraName = string.Empty;
    }

    private void RefreshRuntimeVisibility()
    {
        bool remainDuringActive =
            profile != null && profile.RemainGuidanceDuringActive;

        guidanceVisible =
            currentSessionType == MatchFlowSessionType.ZoneLoot &&
            (
                currentPhase == MatchFlowSessionPhase.Warning ||
                (
                    currentPhase == MatchFlowSessionPhase.Active &&
                    remainDuringActive
                )
            );

        checkoutVisible =
            currentSessionType == MatchFlowSessionType.CheckoutWindow &&
            currentPhase == MatchFlowSessionPhase.Active &&
            currentCheckoutStations != CheckoutStationMask.None;
    }

    #endregion

    #region Shapes Rendering

    public override void DrawShapes(Camera cam)
    {
        if (!drawingEnabled ||
            cam == null ||
            profile == null ||
            worldCenter == null ||
            !ShouldDrawForCamera(cam))
        {
            return;
        }

        ResolveDrawState(
            out bool drawGuidance,
            out ArenaZoneId guidanceZone,
            out bool drawCheckout,
            out CheckoutStationMask checkoutMask
        );

        if (Application.isPlaying)
        {
            guidanceVisible = drawGuidance;
            checkoutVisible = drawCheckout;
        }

        if (!drawGuidance && !drawCheckout)
        {
            if (Application.isPlaying)
            {
                lastDrawnArrowCount = 0;
                lastDrawCameraName = cam.name;
            }

            return;
        }

        BuildWorldBasis(
            out Vector3 up,
            out Vector3 north,
            out Vector3 east
        );

        Vector3 origin =
            worldCenter.position + up * profile.HeightOffset;

        int arrowCount = 0;

        using (Draw.Command(cam))
        {
            Draw.ResetAllDrawStates();
            Draw.BlendMode = ShapesBlendMode.Transparent;

            if (drawGuidance)
            {
                arrowCount += DrawGuidanceArrows(
                    origin,
                    up,
                    north,
                    east,
                    guidanceZone
                );
            }

            if (drawCheckout)
            {
                arrowCount += DrawCheckoutArrows(
                    origin,
                    up,
                    north,
                    east,
                    checkoutMask
                );
            }
        }

        if (Application.isPlaying)
        {
            lastDrawnArrowCount = arrowCount;
            lastDrawCameraName = cam.name;
        }
    }

    private bool ShouldDrawForCamera(Camera cam)
    {
        if (cam.cameraType == CameraType.SceneView)
        {
            return drawInSceneView;
        }

        if (cam.cameraType != CameraType.Game || !drawInGameCameras)
        {
            return false;
        }

        if (!respectRendererGameObjectLayer) return true;

        int rendererLayerMask = 1 << gameObject.layer;
        return (cam.cullingMask & rendererLayerMask) != 0;
    }

    private void ResolveDrawState(
        out bool drawGuidance,
        out ArenaZoneId guidanceZone,
        out bool drawCheckout,
        out CheckoutStationMask checkoutMask
    )
    {
        switch (previewMode)
        {
            case ArenaDirectionalIndicatorPreviewMode.ZoneGuidance:
                drawGuidance = true;
                guidanceZone = previewZone;
                drawCheckout = false;
                checkoutMask = CheckoutStationMask.None;
                return;

            case ArenaDirectionalIndicatorPreviewMode.CheckoutAvailability:
                drawGuidance = false;
                guidanceZone = previewZone;
                drawCheckout =
                    previewCheckoutStations != CheckoutStationMask.None;
                checkoutMask = previewCheckoutStations;
                return;

            default:
                RefreshRuntimeVisibility();
                drawGuidance = guidanceVisible;
                guidanceZone = currentZone;
                drawCheckout = checkoutVisible;
                checkoutMask = currentCheckoutStations;
                return;
        }
    }

    #endregion

    #region Procedural Layout

    private int DrawGuidanceArrows(
        Vector3 origin,
        Vector3 up,
        Vector3 north,
        Vector3 east,
        ArenaZoneId zoneId
    )
    {
        ArenaEightWayDirection zoneDirection =
            profile.GetZoneDirection(zoneId);

        Vector3 targetDirection = GetEightWayVector(
            zoneDirection,
            north,
            east
        );

        Vector3 targetPosition =
            origin + targetDirection * profile.ZoneTargetDistance;

        int drawnCount = 0;

        for (int anchorIndex = 0; anchorIndex < 4; anchorIndex++)
        {
            ArenaEightWayDirection anchorDirection =
                profile.GetGuidanceAnchorDirection(anchorIndex);

            Vector3 anchorVector = GetEightWayVector(
                anchorDirection,
                north,
                east
            );

            Vector3 anchorPosition =
                origin + anchorVector * profile.GuidanceAnchorDistance;

            Vector3 snappedFacing = SnapToEightWayDirection(
                targetPosition - anchorPosition,
                north,
                east
            );

            DrawWorldArrow(
                anchorPosition,
                snappedFacing,
                up,
                profile.GuidanceArrowStyle
            );

            drawnCount++;
        }

        Vector3 centerFacing = SnapToEightWayDirection(
            targetPosition - origin,
            north,
            east
        );

        DrawWorldArrow(
            origin,
            centerFacing,
            up,
            profile.GuidanceArrowStyle
        );

        return drawnCount + 1;
    }

    private int DrawCheckoutArrows(
        Vector3 origin,
        Vector3 up,
        Vector3 north,
        Vector3 east,
        CheckoutStationMask mask
    )
    {
        int drawnCount = 0;

        drawnCount += DrawCheckoutArrowIfSelected(
            origin,
            up,
            north,
            CheckoutStationMask.North,
            mask
        );

        drawnCount += DrawCheckoutArrowIfSelected(
            origin,
            up,
            east,
            CheckoutStationMask.East,
            mask
        );

        drawnCount += DrawCheckoutArrowIfSelected(
            origin,
            up,
            -north,
            CheckoutStationMask.South,
            mask
        );

        drawnCount += DrawCheckoutArrowIfSelected(
            origin,
            up,
            -east,
            CheckoutStationMask.West,
            mask
        );

        return drawnCount;
    }

    private int DrawCheckoutArrowIfSelected(
        Vector3 origin,
        Vector3 up,
        Vector3 outwardDirection,
        CheckoutStationMask station,
        CheckoutStationMask activeMask
    )
    {
        if ((activeMask & station) == 0) return 0;

        Vector3 arrowCenter =
            origin +
            outwardDirection.normalized * profile.CheckoutArrowDistance;

        DrawWorldArrow(
            arrowCenter,
            outwardDirection,
            up,
            profile.CheckoutArrowStyle
        );

        return 1;
    }

    private void BuildWorldBasis(
        out Vector3 up,
        out Vector3 north,
        out Vector3 east
    )
    {
        up = worldCenter.up.normalized;
        north = Vector3.ProjectOnPlane(worldCenter.forward, up).normalized;

        if (north.sqrMagnitude <= 0.0001f)
        {
            north = Vector3.forward;
        }

        east = Vector3.Cross(up, north).normalized;

        if (east.sqrMagnitude <= 0.0001f)
        {
            east = Vector3.right;
        }
    }

    private Vector3 GetEightWayVector(
        ArenaEightWayDirection direction,
        Vector3 north,
        Vector3 east
    )
    {
        float angleRadians =
            (int)direction * 45f * Mathf.Deg2Rad;

        return (
            north * Mathf.Cos(angleRadians) +
            east * Mathf.Sin(angleRadians)
        ).normalized;
    }

    private Vector3 SnapToEightWayDirection(
        Vector3 worldDirection,
        Vector3 north,
        Vector3 east
    )
    {
        float northAmount = Vector3.Dot(worldDirection, north);
        float eastAmount = Vector3.Dot(worldDirection, east);

        if (Mathf.Abs(northAmount) <= 0.0001f &&
            Mathf.Abs(eastAmount) <= 0.0001f)
        {
            return north;
        }

        float rawAngle = Mathf.Atan2(eastAmount, northAmount) * Mathf.Rad2Deg;
        int snappedStep = Mathf.RoundToInt(rawAngle / 45f);
        int normalizedStep = ((snappedStep % 8) + 8) % 8;

        return GetEightWayVector(
            (ArenaEightWayDirection)normalizedStep,
            north,
            east
        );
    }

    #endregion

    #region Arrow Geometry

    private void DrawWorldArrow(
        Vector3 center,
        Vector3 forward,
        Vector3 up,
        ArenaDirectionalArrowStyle style
    )
    {
        if (style == null) return;

        float scale = profile.MasterScale;
        float shaftLength = style.ShaftLength * scale;
        float shaftWidth = style.ShaftWidth * scale;
        float headLength = style.HeadLength * scale;
        float headWidth = style.HeadWidth * scale;

        if (style.DrawUnderlay)
        {
            float expansion = style.UnderlayExpansion * scale;

            DrawArrowPass(
                center,
                forward,
                up,
                shaftLength + expansion * 2f,
                shaftWidth + expansion * 2f,
                headLength + expansion * 2f,
                headWidth + expansion * 2f,
                style.UnderlayColor
            );
        }

        DrawArrowPass(
            center,
            forward,
            up,
            shaftLength,
            shaftWidth,
            headLength,
            headWidth,
            style.Color
        );
    }

    private void DrawArrowPass(
        Vector3 center,
        Vector3 forward,
        Vector3 up,
        float shaftLength,
        float shaftWidth,
        float headLength,
        float headWidth,
        Color color
    )
    {
        forward = Vector3.ProjectOnPlane(forward, up).normalized;
        if (forward.sqrMagnitude <= 0.0001f) return;

        Vector3 side = Vector3.Cross(up, forward).normalized;
        float totalLength = shaftLength + headLength;

        Vector3 tailCenter =
            center - forward * (totalLength * 0.5f);

        Vector3 tip =
            center + forward * (totalLength * 0.5f);

        Vector3 headBaseCenter = tip - forward * headLength;

        Vector3 tailA = tailCenter + side * (shaftWidth * 0.5f);
        Vector3 tailB = tailCenter - side * (shaftWidth * 0.5f);
        Vector3 shaftA = headBaseCenter + side * (shaftWidth * 0.5f);
        Vector3 shaftB = headBaseCenter - side * (shaftWidth * 0.5f);

        Vector3 headA = headBaseCenter + side * (headWidth * 0.5f);
        Vector3 headB = headBaseCenter - side * (headWidth * 0.5f);

        Draw.Triangle(tailA, shaftB, shaftA, color);
        Draw.Triangle(tailA, tailB, shaftB, color);
        Draw.Triangle(tip, headA, headB, color);
    }

    #endregion

    #region Validation

    private void ValidateRuntimeSetup()
    {
        if (matchFlowDirector == null)
        {
            Debug.LogError(
                "[ArenaDirectionalIndicatorRenderer] MatchFlowDirector is " +
                "missing. Runtime State preview cannot follow match phases.",
                this
            );
        }

        if (worldCenter == null)
        {
            Debug.LogError(
                "[ArenaDirectionalIndicatorRenderer] World Center is missing.",
                this
            );
        }

        if (profile == null)
        {
            Debug.LogError(
                "[ArenaDirectionalIndicatorRenderer] Indicator Profile is missing.",
                this
            );
        }
    }

    #endregion
}
