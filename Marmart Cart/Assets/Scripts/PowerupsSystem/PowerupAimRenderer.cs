using Shapes;
using UnityEngine;

/// <summary>
/// One Shapes drawer for every local viewport. Each camera receives only its
/// owning player's current projectile arc and impact envelope.
///
/// World samples are projected to screen and reconstructed near the camera by
/// default, matching the existing PlayerWorldHUD anti-occlusion approach.
/// Pixel sizes are 1920x1080 reference pixels times the UI scale.
/// </summary>
[DisallowMultipleComponent]
public class PowerupAimRenderer : ImmediateModeShapeDrawer
{
    [Header("References")]
    [SerializeField] private PlayerWorldHUDSystem hudSystem;
    [SerializeField] private PowerupAimStateSystem aimStateSystem;
    [SerializeField] private PowerupAimProfile aimProfile;

    [Header("Visibility")]
    [SerializeField] private bool drawingEnabled = true;

    // UI scale for the camera currently being drawn.
    private float drawScale = 1f;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    public override void DrawShapes(Camera cam)
    {
        if (!drawingEnabled || cam == null || aimProfile == null) return;

        if (hudSystem == null || aimStateSystem == null)
        {
            ResolveReferences();
        }

        if (hudSystem == null || aimStateSystem == null) return;

        // The renderable anchor lookup is intentional: targeting previews
        // disappear with the Player HUD during checkout, while the separate
        // match viewport overlay remains unaffected.
        if (!HudShapes.TryGetViewPlayer(cam, out int playerIndex) ||
            !hudSystem.TryGetRenderableAnchor(playerIndex, out _))
        {
            return;
        }

        if (!aimStateSystem.TryGetState(
                playerIndex,
                out PowerupAimState aimState
            ) ||
            !aimState.Visible ||
            aimState.ImpactPreviewRadius <= 0f)
        {
            return;
        }

        drawScale = HudShapes.Scale();

        using (Draw.Command(cam))
        {
            HudShapes.ApplyHudDrawState();

            Color mainColor = aimProfile.GetMainColor(
                aimState.TrajectoryObstructed
            );

            DrawLandingEnvelope(cam, aimState, mainColor);
            DrawLandingCenter(cam, aimState, mainColor);
            DrawTrajectory(cam, aimState, mainColor);
        }
    }

    private void DrawTrajectory(
        Camera cam,
        PowerupAimState aimState,
        Color mainColor)
    {
        float mainThickness = aimProfile.TrajectoryThicknessPixels * drawScale;
        float underlayThickness =
            mainThickness + aimProfile.TrajectoryUnderlayExtraPixels * drawScale;

        if (underlayThickness > mainThickness)
        {
            DrawTrajectoryPass(
                cam,
                aimState,
                underlayThickness,
                aimProfile.TrajectoryUnderlayColor
            );
        }

        DrawTrajectoryPass(
            cam,
            aimState,
            mainThickness,
            mainColor
        );
    }

    private void DrawTrajectoryPass(
        Camera cam,
        PowerupAimState aimState,
        float thicknessPixels,
        Color color)
    {
        int sampleCount = Mathf.Max(2, aimProfile.TrajectorySampleCount);
        float previewEndTime = aimState.TrajectoryObstructed
            ? Mathf.Clamp01(aimState.PreviewEndNormalizedTime)
            : 1f;

        bool hasPreviousPoint = false;
        Vector3 previousPoint = default;

        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            float sampleFraction = sampleIndex / (float)(sampleCount - 1);
            float normalizedTime = sampleFraction * previewEndTime;

            // End at the exact blocking-collider contact returned by the
            // semantic sweep. All earlier points remain on the original arc
            // toward the unchanged ground destination.
            Vector3 worldPoint = sampleIndex == sampleCount - 1
                ? aimState.PreviewEndPosition
                : PowerupTrajectory.Evaluate(
                    aimState.StartPosition,
                    aimState.LandingPosition,
                    aimState.ArcHeight,
                    normalizedTime
                );

            if (!TryProjectPoint(cam, worldPoint, out Vector3 renderPoint))
            {
                hasPreviousPoint = false;
                continue;
            }

            if (hasPreviousPoint)
            {
                Draw.Line(
                    previousPoint,
                    renderPoint,
                    thicknessPixels,
                    color
                );
            }

            previousPoint = renderPoint;
            hasPreviousPoint = true;
        }
    }

    private void DrawLandingEnvelope(
        Camera cam,
        PowerupAimState aimState,
        Color mainColor)
    {
        if (!TryBuildSurfaceBasis(
            aimState,
            out Vector3 basisRight,
            out Vector3 basisForward
        ))
        {
            return;
        }

        if (!TryProjectPoint(
            cam,
            aimState.PreviewEndPosition,
            out Vector3 renderCenter
        ))
        {
            return;
        }

        int sampleCount = Mathf.Max(12, aimProfile.LandingCircleSampleCount);
        Color fillColor = aimProfile.GetFillColor(
            aimState.TrajectoryObstructed
        );

        // Fill is triangulated from the same world-space circle used by the
        // outline, so power-up-specific impact radii remain visually honest.
        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            float angleA = sampleIndex * Mathf.PI * 2f / sampleCount;
            float angleB = (sampleIndex + 1) * Mathf.PI * 2f / sampleCount;

            Vector3 pointA = GetCirclePoint(
                aimState,
                basisRight,
                basisForward,
                angleA
            );

            Vector3 pointB = GetCirclePoint(
                aimState,
                basisRight,
                basisForward,
                angleB
            );

            if (!TryProjectPoint(cam, pointA, out Vector3 renderA) ||
                !TryProjectPoint(cam, pointB, out Vector3 renderB))
            {
                continue;
            }

            Draw.Triangle(renderCenter, renderA, renderB, fillColor);
        }

        float mainThickness = aimProfile.LandingOutlineThicknessPixels * drawScale;
        float underlayThickness =
            mainThickness + aimProfile.LandingUnderlayExtraPixels * drawScale;

        if (underlayThickness > mainThickness)
        {
            DrawLandingOutlinePass(
                cam,
                aimState,
                basisRight,
                basisForward,
                underlayThickness,
                aimProfile.TrajectoryUnderlayColor
            );
        }

        DrawLandingOutlinePass(
            cam,
            aimState,
            basisRight,
            basisForward,
            mainThickness,
            mainColor
        );
    }

    private void DrawLandingOutlinePass(
        Camera cam,
        PowerupAimState aimState,
        Vector3 basisRight,
        Vector3 basisForward,
        float thicknessPixels,
        Color color)
    {
        int sampleCount = Mathf.Max(12, aimProfile.LandingCircleSampleCount);

        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            float angleA = sampleIndex * Mathf.PI * 2f / sampleCount;
            float angleB = (sampleIndex + 1) * Mathf.PI * 2f / sampleCount;

            Vector3 pointA = GetCirclePoint(
                aimState,
                basisRight,
                basisForward,
                angleA
            );

            Vector3 pointB = GetCirclePoint(
                aimState,
                basisRight,
                basisForward,
                angleB
            );

            if (!TryProjectPoint(cam, pointA, out Vector3 renderA) ||
                !TryProjectPoint(cam, pointB, out Vector3 renderB))
            {
                continue;
            }

            Draw.Line(renderA, renderB, thicknessPixels, color);
        }
    }

    private void DrawLandingCenter(
        Camera cam,
        PowerupAimState aimState,
        Color mainColor)
    {
        if (!TryProjectPoint(
                cam,
                aimState.PreviewEndPosition,
                out Vector3 renderCenter
            ))
        {
            return;
        }

        Vector3 centerScreen = cam.WorldToScreenPoint(renderCenter);
        Vector2 center = centerScreen;
        float depth = centerScreen.z;
        float halfSize = aimProfile.CenterCrossSizePixels * drawScale * 0.5f;

        Vector3 lowerLeft = HudShapes.ScreenToWorld(cam, center + new Vector2(-halfSize, -halfSize), depth);
        Vector3 upperRight = HudShapes.ScreenToWorld(cam, center + new Vector2(halfSize, halfSize), depth);
        Vector3 upperLeft = HudShapes.ScreenToWorld(cam, center + new Vector2(-halfSize, halfSize), depth);
        Vector3 lowerRight = HudShapes.ScreenToWorld(cam, center + new Vector2(halfSize, -halfSize), depth);

        // A true screen-space X keeps both strokes exactly equal and prevents
        // Tomato/Ice impact-radius differences from resizing the marker.
        DrawCrossLine(lowerLeft, upperRight, mainColor);
        DrawCrossLine(upperLeft, lowerRight, mainColor);

        Draw.Disc(
            renderCenter,
            cam.transform.rotation,
            aimProfile.CenterDotRadiusPixels * drawScale,
            mainColor
        );
    }

    private void DrawCrossLine(Vector3 start, Vector3 end, Color mainColor)
    {
        float mainThickness = aimProfile.CenterCrossThicknessPixels * drawScale;
        float underlayThickness =
            mainThickness + aimProfile.LandingUnderlayExtraPixels * drawScale;

        if (underlayThickness > mainThickness)
        {
            Draw.Line(
                start,
                end,
                underlayThickness,
                aimProfile.TrajectoryUnderlayColor
            );
        }

        Draw.Line(start, end, mainThickness, mainColor);
    }

    private static Vector3 GetCirclePoint(
        PowerupAimState aimState,
        Vector3 basisRight,
        Vector3 basisForward,
        float radians)
    {
        Vector3 radialDirection =
            basisRight * Mathf.Cos(radians) +
            basisForward * Mathf.Sin(radians);

        return aimState.PreviewEndPosition +
               radialDirection * aimState.ImpactPreviewRadius;
    }

    private static bool TryBuildSurfaceBasis(
        PowerupAimState aimState,
        out Vector3 basisRight,
        out Vector3 basisForward)
    {
        Vector3 normal = aimState.PreviewEndNormal.sqrMagnitude > 0.000001f
            ? aimState.PreviewEndNormal.normalized
            : Vector3.up;

        basisForward = Vector3.ProjectOnPlane(
            aimState.AimDirection,
            normal
        );

        if (basisForward.sqrMagnitude <= 0.000001f)
        {
            basisForward = Vector3.ProjectOnPlane(Vector3.forward, normal);
        }

        if (basisForward.sqrMagnitude <= 0.000001f)
        {
            basisForward = Vector3.ProjectOnPlane(Vector3.right, normal);
        }

        if (basisForward.sqrMagnitude <= 0.000001f)
        {
            basisRight = default;
            return false;
        }

        basisForward.Normalize();
        basisRight = Vector3.Cross(normal, basisForward).normalized;
        return basisRight.sqrMagnitude > 0.000001f;
    }

    private bool TryProjectPoint(
        Camera cam,
        Vector3 worldPoint,
        out Vector3 renderPoint)
    {
        renderPoint = worldPoint;

        if (!aimProfile.UseNearCameraRenderPlane)
        {
            Vector3 directScreenPoint = cam.WorldToScreenPoint(worldPoint);
            return !aimProfile.SkipPointsBehindCamera ||
                   directScreenPoint.z > 0f;
        }

        Vector3 screenPoint = cam.WorldToScreenPoint(worldPoint);

        if (aimProfile.SkipPointsBehindCamera && screenPoint.z <= 0f)
        {
            return false;
        }

        screenPoint.z = HudShapes.RenderDepth(
            cam,
            aimProfile.NearCameraRenderDistance,
            aimProfile.NearClipSafetyPadding
        );

        renderPoint = cam.ScreenToWorldPoint(screenPoint);
        return true;
    }

    private void ResolveReferences()
    {
        if (hudSystem == null)
        {
            hudSystem = FindFirstObjectByType<PlayerWorldHUDSystem>();
        }

        if (aimStateSystem == null)
        {
            aimStateSystem = FindFirstObjectByType<PowerupAimStateSystem>();
        }
    }
}
