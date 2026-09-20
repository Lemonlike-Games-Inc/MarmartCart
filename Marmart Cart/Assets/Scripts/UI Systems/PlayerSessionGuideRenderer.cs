using Shapes;
using UnityEngine;

/// <summary>
/// Draws one cart-anchored session forecast for each registered gameplay
/// camera. Gameplay/timeline meaning lives in PlayerSessionGuideSystem; this
/// component owns presentation only.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerSessionGuideRenderer : ImmediateModeShapeDrawer
{
    #region References

    [Header("References")]
    [SerializeField] private PlayerWorldHUDSystem playerWorldHUDSystem;
    [SerializeField] private PlayerSessionGuideSystem sessionGuideSystem;
    [SerializeField] private PlayerSessionGuideProfile presentationProfile;

    #endregion

    #region Visibility

    [Header("Visibility")]
    [SerializeField] private bool drawingEnabled = true;
    [SerializeField] private bool skipWhenBehindCamera = true;

    #endregion

    #region Runtime Diagnostics

    [Header("Runtime - Read Only")]
    [SerializeField] private int activePlayerCount;
    [SerializeField] private int lastRenderedPlayerIndex;
    [SerializeField] private bool lastArrowWasVisible;
    [SerializeField] private bool lastRaindropWasVisible;
    [SerializeField] private bool lastGuideWasSuppressedInsideTarget;
    [SerializeField] private Vector2 lastArrowScreenDirection;

    private PlayerSessionGuideTargetKind cachedMessageKind =
        PlayerSessionGuideTargetKind.None;
    private PlayerSessionGuideIndicationPhase cachedMessagePhase =
        PlayerSessionGuideIndicationPhase.None;
    private string cachedMessageZone = string.Empty;
    private int cachedMessageCountdown = -1;
    private string cachedMessageTemplate = string.Empty;
    private string cachedMessage = string.Empty;

    // One immutable circle mesh is reused for the two missing raindrop fills.
    // Camera position, pixel radius, and color are applied per draw.
    private const int RaindropCircleSegments = 64;
    private PolygonPath raindropCirclePath;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        InvalidateMessageCache();
    }

    private void OnDestroy()
    {
        raindropCirclePath?.Dispose();
        raindropCirclePath = null;
    }

    public override void DrawShapes(Camera cam)
    {
        if (!drawingEnabled || cam == null) return;

        ResolveReferences();

        if (playerWorldHUDSystem == null ||
            sessionGuideSystem == null ||
            presentationProfile == null ||
            !presentationProfile.ShowSessionGuide)
        {
            return;
        }

        if (!sessionGuideSystem.TryGetActiveState(
                out PlayerSessionGuideState guideState
            ) ||
            guideState == null)
        {
            return;
        }

        if (!playerWorldHUDSystem.TryGetRenderableSlotForCameraForOverlay(
                cam,
                out int playerIndex,
                out Transform hudAnchor
            ) ||
            hudAnchor == null)
        {
            return;
        }

        bool playerIsInsideTarget =
            sessionGuideSystem.IsPointInsideCurrentTarget(
                hudAnchor.position
            );

        lastRenderedPlayerIndex = playerIndex;
        lastArrowWasVisible = false;
        lastRaindropWasVisible = false;
        lastGuideWasSuppressedInsideTarget =
            guideState.IndicationPhase ==
                PlayerSessionGuideIndicationPhase.Active &&
            playerIsInsideTarget;
        lastArrowScreenDirection = Vector2.zero;

        // Active guidance is only a remote-location reminder. Once
        // this player arrives, every part of their guide is hidden.
        if (lastGuideWasSuppressedInsideTarget) return;

        Vector3 anchorScreen = cam.WorldToScreenPoint(hudAnchor.position);
        if (skipWhenBehindCamera && anchorScreen.z <= 0f) return;

        float screenDepth = anchorScreen.z;

        if (presentationProfile.UseNearCameraRenderPlane)
        {
            screenDepth = Mathf.Max(
                presentationProfile.NearCameraRenderDistance,
                cam.nearClipPlane + presentationProfile.NearClipSafetyPadding
            );
        }

        activePlayerCount = CountActivePlayerSlots();

        float scale = presentationProfile.GetEffectiveScale(
            activePlayerCount
        );

        Vector2 anchorScreenPoint = new Vector2(
            anchorScreen.x,
            anchorScreen.y
        );

        Vector2 groupCenter =
            anchorScreenPoint + presentationProfile.GroupOffsetPixels * scale;

        string message = ResolveMessage(guideState);

        // Keep different Shapes primitive types in isolated command scopes.
        // This isolates the suspected mixed-batch rendering issue without
        // changing the camera, visibility conditions, or primitive geometry.
        DrawDialogue(
            cam,
            screenDepth,
            groupCenter,
            guideState,
            message,
            scale
        );

        bool showYouAreHere =
            guideState.IndicationPhase ==
                PlayerSessionGuideIndicationPhase.Countdown &&
            playerIsInsideTarget;

        if (showYouAreHere)
        {
            DrawCompassRaindrop(
                cam,
                screenDepth,
                groupCenter,
                scale
            );

            lastRaindropWasVisible = true;
        }
        else if (!playerIsInsideTarget &&
                 guideState.TargetAnchor != null &&
                 TryGetArrowScreenDirection(
                     cam,
                     hudAnchor.position,
                     guideState.TargetAnchor.position,
                     out Vector2 direction
                 ))
        {
            DrawCompassArrow(
                cam,
                screenDepth,
                groupCenter,
                direction,
                scale
            );

            lastArrowWasVisible = true;
            lastArrowScreenDirection = direction;
        }
    }

    #endregion

    #region Dialogue

    private void DrawDialogue(
        Camera cam,
        float screenDepth,
        Vector2 groupCenter,
        PlayerSessionGuideState guideState,
        string message,
        float scale)
    {
        Vector2 backgroundSize =
            presentationProfile.GetDialogueBackgroundSize(
                guideState.TargetKind
            ) * scale;

        // Rectangle and text use separate command scopes so neither relies on
        // a mixed primitive batch or on draw state left by the other.
        using (Draw.Command(cam))
        {
            ConfigureDrawState();

            DrawRoundedScreenRectangle(
                cam,
                screenDepth,
                groupCenter +
                    presentationProfile.DialogueBackgroundOffsetPixels * scale,
                backgroundSize,
                presentationProfile.DialogueCornerRadiusPixels * scale,
                presentationProfile.DialogueBackgroundColor
            );
        }

        using (Draw.Command(cam))
        {
            ConfigureDrawState();

            DrawCenteredScreenText(
                cam,
                screenDepth,
                groupCenter +
                    presentationProfile.DialogueTextOffsetPixels * scale,
                message,
                presentationProfile.DialogueFontSizePixels * scale,
                presentationProfile.DialogueTextColor
            );
        }
    }

    private string ResolveMessage(PlayerSessionGuideState guideState)
    {
        string template =
            presentationProfile.GetMessageTemplate(
                guideState.IndicationPhase,
                guideState.TargetKind
            );

        string zoneName = guideState.TargetDisplayName ?? string.Empty;

        if (cachedMessageKind == guideState.TargetKind &&
            cachedMessagePhase == guideState.IndicationPhase &&
            cachedMessageZone == zoneName &&
            cachedMessageCountdown == guideState.CountdownSeconds &&
            cachedMessageTemplate == template)
        {
            return cachedMessage;
        }

        cachedMessageKind = guideState.TargetKind;
        cachedMessagePhase = guideState.IndicationPhase;
        cachedMessageZone = zoneName;
        cachedMessageCountdown = guideState.CountdownSeconds;
        cachedMessageTemplate = template;
        cachedMessage = presentationProfile.BuildMessage(
            guideState.IndicationPhase,
            guideState.TargetKind,
            zoneName,
            guideState.CountdownSeconds
        );

        return cachedMessage;
    }

    private void InvalidateMessageCache()
    {
        cachedMessageKind = PlayerSessionGuideTargetKind.None;
        cachedMessagePhase = PlayerSessionGuideIndicationPhase.None;
        cachedMessageZone = string.Empty;
        cachedMessageCountdown = -1;
        cachedMessageTemplate = string.Empty;
        cachedMessage = string.Empty;
    }

    #endregion

    #region Compass Arrow

    private static bool TryGetArrowScreenDirection(
        Camera cam,
        Vector3 playerWorldPosition,
        Vector3 targetWorldPosition,
        out Vector2 screenDirection)
    {
        targetWorldPosition.y = playerWorldPosition.y;

        Vector3 horizontalDirection =
            targetWorldPosition - playerWorldPosition;

        if (horizontalDirection.sqrMagnitude <= 0.0001f)
        {
            screenDirection = Vector2.zero;
            return false;
        }

        Vector3 playerScreen = cam.WorldToScreenPoint(playerWorldPosition);
        Vector3 targetScreen = cam.WorldToScreenPoint(targetWorldPosition);

        Vector2 delta = new Vector2(
            targetScreen.x - playerScreen.x,
            targetScreen.y - playerScreen.y
        );

        if (delta.sqrMagnitude <= 0.0001f)
        {
            screenDirection = Vector2.zero;
            return false;
        }

        screenDirection = delta.normalized;
        return true;
    }

    private void DrawCompassArrow(
        Camera cam,
        float screenDepth,
        Vector2 groupCenter,
        Vector2 direction,
        float scale)
    {
        Vector2 compassCenter =
            groupCenter + presentationProfile.CompassOffsetPixels * scale;

        using (Draw.Command(cam))
        {
            ConfigureDrawState();

            DrawCompassBackground(
                cam,
                screenDepth,
                compassCenter,
                scale
            );
        }

        float arrowLength = presentationProfile.ArrowLengthPixels * scale;
        float shaftThickness =
            presentationProfile.ArrowShaftThicknessPixels * scale;
        float headLength = Mathf.Min(
            presentationProfile.ArrowHeadLengthPixels * scale,
            arrowLength
        );
        float halfHeadWidth =
            presentationProfile.ArrowHeadWidthPixels * scale * 0.5f;

        if (arrowLength <= 0f) return;

        Vector2 arrowCenter =
            compassCenter + presentationProfile.ArrowOffsetPixels * scale;

        Vector2 tail = arrowCenter - direction * (arrowLength * 0.5f);
        Vector2 tip = arrowCenter + direction * (arrowLength * 0.5f);
        Vector2 headBase = tip - direction * headLength;

        if (shaftThickness > 0f)
        {
            using (Draw.Command(cam))
            {
                ConfigureDrawState();

                Draw.Line(
                    ScreenPointToWorld(cam, tail, screenDepth),
                    ScreenPointToWorld(cam, headBase, screenDepth),
                    shaftThickness,
                    presentationProfile.ArrowColor
                );
            }
        }

        if (headLength <= 0f || halfHeadWidth <= 0f) return;

        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        Vector2 headLeft = headBase + perpendicular * halfHeadWidth;
        Vector2 headRight = headBase - perpendicular * halfHeadWidth;

        using (Draw.Command(cam))
        {
            ConfigureDrawState();

            Draw.Triangle(
                ScreenPointToWorld(cam, headLeft, screenDepth),
                ScreenPointToWorld(cam, headRight, screenDepth),
                ScreenPointToWorld(cam, tip, screenDepth),
                presentationProfile.ArrowColor
            );
        }
    }

    private void DrawCompassRaindrop(
        Camera cam,
        float screenDepth,
        Vector2 groupCenter,
        float scale)
    {
        Vector2 compassCenter =
            groupCenter + presentationProfile.CompassOffsetPixels * scale;

        using (Draw.Command(cam))
        {
            ConfigureDrawState();

            DrawCompassBackground(
                cam,
                screenDepth,
                compassCenter,
                scale
            );
        }

        float width = presentationProfile.RaindropWidthPixels * scale;
        float height = presentationProfile.RaindropHeightPixels * scale;

        if (width <= 0f || height <= 0f) return;

        Vector2 dropCenter =
            compassCenter + presentationProfile.RaindropOffsetPixels * scale;

        float bulbRadius = Mathf.Min(width * 0.5f, height * 0.4f);
        Vector2 bulbCenter =
            dropCenter + Vector2.up * (height * 0.5f - bulbRadius);

        Vector2 tip = dropCenter + Vector2.down * (height * 0.5f);
        Vector2 triangleBaseCenter =
            bulbCenter + Vector2.down * (bulbRadius * 0.35f);
        float halfBaseWidth = bulbRadius * 0.72f;

        Vector2 baseLeft =
            triangleBaseCenter + Vector2.left * halfBaseWidth;
        Vector2 baseRight =
            triangleBaseCenter + Vector2.right * halfBaseWidth;

        float centerCircleRadius =
            presentationProfile.RaindropCenterCircleRadiusPixels * scale;

        // Preserve the working tail and the original painter order:
        // background -> Triangle tail -> polygon bulb/center.
        using (Draw.Command(cam))
        {
            ConfigureDrawState();

            Draw.Triangle(
                ScreenPointToWorld(cam, baseLeft, screenDepth),
                ScreenPointToWorld(cam, baseRight, screenDepth),
                ScreenPointToWorld(cam, tip, screenDepth),
                presentationProfile.RaindropColor
            );
        }

        using (Draw.Command(cam))
        {
            ConfigureDrawState();

            DrawRaindropCirclePolygon(
                cam,
                screenDepth,
                bulbCenter,
                bulbRadius,
                presentationProfile.RaindropColor
            );

            if (centerCircleRadius > 0f)
            {
                DrawRaindropCirclePolygon(
                    cam,
                    screenDepth,
                    bulbCenter,
                    centerCircleRadius,
                    presentationProfile.RaindropCenterCircleColor
                );
            }
        }
    }

    private void DrawCompassBackground(
        Camera cam,
        float screenDepth,
        Vector2 compassCenter,
        float scale)
    {
        float circleRadius =
            presentationProfile.CompassCircleRadiusPixels * scale;

        if (circleRadius <= 0f) return;

        Draw.Disc(
            ScreenPointToWorld(cam, compassCenter, screenDepth),
            cam.transform.rotation,
            circleRadius,
            presentationProfile.CompassCircleColor
        );
    }

    #endregion

    #region Screen-Space Shape Helpers

    private void DrawRaindropCirclePolygon(
        Camera cam,
        float screenDepth,
        Vector2 centerPixels,
        float radiusPixels,
        Color color)
    {
        if (radiusPixels <= 0f) return;

        if (raindropCirclePath == null)
        {
            raindropCirclePath = new PolygonPath();
            for (int i = 0; i < RaindropCircleSegments; i++)
            {
                // Clockwise unit-circle vertices. The path stays unchanged
                // when another player camera or a different radius draws it.
                float angle = -Mathf.PI * 2f * i / RaindropCircleSegments;
                raindropCirclePath.AddPoint(Mathf.Cos(angle), Mathf.Sin(angle));
            }
        }

        Vector3 origin = ScreenPointToWorld(cam, centerPixels, screenDepth);
        Vector3 xAxis = ScreenPointToWorld(
            cam, centerPixels + Vector2.right * radiusPixels, screenDepth
        ) - origin;
        Vector3 yAxis = ScreenPointToWorld(
            cam, centerPixels + Vector2.up * radiusPixels, screenDepth
        ) - origin;
        Vector3 forward = cam.transform.forward;

        Matrix4x4 matrix = Matrix4x4.identity;
        matrix.SetColumn(0, new Vector4(xAxis.x, xAxis.y, xAxis.z, 0f));
        matrix.SetColumn(1, new Vector4(yAxis.x, yAxis.y, yAxis.z, 0f));
        matrix.SetColumn(2, new Vector4(forward.x, forward.y, forward.z, 0f));
        matrix.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1f));

        Matrix4x4 previousMatrix = Draw.Matrix;
        try
        {
            Draw.Matrix = matrix;
            Draw.Polygon(raindropCirclePath, color);
        }
        finally
        {
            Draw.Matrix = previousMatrix;
        }
    }

    private static void ConfigureDrawState()
    {
        Draw.ResetAllDrawStates();
        Draw.BlendMode = ShapesBlendMode.Transparent;
        Draw.RadiusSpace = ThicknessSpace.Pixels;
        Draw.ThicknessSpace = ThicknessSpace.Pixels;
        Draw.LineGeometry = LineGeometry.Billboard;
        Draw.LineEndCaps = LineEndCap.Round;
    }

    private static void DrawRoundedScreenRectangle(
        Camera cam,
        float screenDepth,
        Vector2 centerScreen,
        Vector2 sizePixels,
        float cornerRadiusPixels,
        Color color)
    {
        float widthPixels = Mathf.Max(0.001f, sizePixels.x);
        float heightPixels = Mathf.Max(0.001f, sizePixels.y);

        float safeCornerPixels = Mathf.Clamp(
            cornerRadiusPixels,
            0f,
            Mathf.Min(widthPixels, heightPixels) * 0.5f
        );

        Vector3 centerWorld = ScreenPointToWorld(
            cam,
            centerScreen,
            screenDepth
        );

        float widthWorld = PixelsToWorldSizeAtDepth(
            cam,
            centerWorld,
            widthPixels
        );

        float heightWorld = PixelsToWorldSizeAtDepth(
            cam,
            centerWorld,
            heightPixels
        );

        float cornerRadiusWorld = PixelsToWorldSizeAtDepth(
            cam,
            centerWorld,
            safeCornerPixels
        );

        Color previousColor = Draw.Color;

        Draw.Matrix = Matrix4x4.TRS(
            centerWorld,
            cam.transform.rotation,
            Vector3.one
        );

        Draw.Color = color;
        Draw.Rectangle(
            Vector3.zero,
            widthWorld,
            heightWorld,
            cornerRadiusWorld
        );

        Draw.Color = previousColor;
        Draw.Matrix = Matrix4x4.identity;
    }

    private static void DrawCenteredScreenText(
        Camera cam,
        float screenDepth,
        Vector2 centerScreen,
        string text,
        float fontSizePixels,
        Color color)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        Vector3 textWorld = ScreenPointToWorld(
            cam,
            centerScreen,
            screenDepth
        );

        Draw.FontSize = PixelsToWorldSizeAtDepth(
            cam,
            textWorld,
            fontSizePixels
        );

        Color previousColor = Draw.Color;
        Draw.Color = color;

        Draw.Text(
            textWorld,
            cam.transform.rotation,
            text,
            TextAlign.Center
        );

        Draw.Color = previousColor;
    }

    private static Vector3 ScreenPointToWorld(
        Camera cam,
        Vector2 screenPoint,
        float screenDepth)
    {
        return cam.ScreenToWorldPoint(
            new Vector3(screenPoint.x, screenPoint.y, screenDepth)
        );
    }

    private static float PixelsToWorldSizeAtDepth(
        Camera cam,
        Vector3 worldPosition,
        float pixelSize)
    {
        Vector3 screenA = cam.WorldToScreenPoint(worldPosition);
        Vector3 screenB = screenA;
        screenB.y += pixelSize;

        Vector3 worldA = cam.ScreenToWorldPoint(screenA);
        Vector3 worldB = cam.ScreenToWorldPoint(screenB);

        return Vector3.Distance(worldA, worldB);
    }

    #endregion

    #region Helpers

    private void ResolveReferences()
    {
        if (playerWorldHUDSystem == null)
        {
            playerWorldHUDSystem = FindFirstObjectByType<PlayerWorldHUDSystem>();
        }

        if (sessionGuideSystem == null)
        {
            sessionGuideSystem = FindFirstObjectByType<PlayerSessionGuideSystem>();
        }
    }

    private int CountActivePlayerSlots()
    {
        if (playerWorldHUDSystem == null) return 0;

        int count = 0;

        for (int playerIndex = 1; playerIndex <= 4; playerIndex++)
        {
            if (playerWorldHUDSystem.IsSlotEnabled(playerIndex) &&
                playerWorldHUDSystem.IsPlayerHUDBound(playerIndex))
            {
                count++;
            }
        }

        return count;
    }

    #endregion
}
