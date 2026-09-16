using Shapes;
using UnityEngine;

/// <summary>
/// One Shapes drawer for every local viewport. Each camera receives only its
/// owning player's current projectile arc and impact envelope.
///
/// World samples are projected to screen and reconstructed near the camera by
/// default, matching the existing PlayerWorldHUD anti-occlusion approach.
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

        // The ordinary renderable lookup is intentional: targeting previews
        // disappear with the Player HUD during checkout, while the separate
        // match viewport overlay remains unaffected.
        if (!hudSystem.TryGetRenderableSlotForCamera(
            cam,
            out int playerIndex,
            out Transform unusedHudAnchor
        ))
        {
            return;
        }

        if (unusedHudAnchor == null ||
            !aimStateSystem.TryGetState(
                playerIndex,
                out PowerupAimState aimState
            ) ||
            !aimState.Visible ||
            aimState.ImpactPreviewRadius <= 0f)
        {
            return;
        }

        using (Draw.Command(cam))
        {
            Draw.ResetAllDrawStates();
            Draw.BlendMode = ShapesBlendMode.Transparent;
            Draw.RadiusSpace = ThicknessSpace.Pixels;
            Draw.ThicknessSpace = ThicknessSpace.Pixels;
            Draw.LineGeometry = LineGeometry.Billboard;
            Draw.LineEndCaps = LineEndCap.Round;

            Color mainColor = aimProfile.GetMainColor(
                aimState.PowerupId,
                aimState.TargetValid
            );

            DrawLandingEnvelope(cam, aimState, mainColor);
            DrawTrajectory(cam, aimState, mainColor);
            DrawLandingCenter(cam, aimState, mainColor);
        }
    }

    private void DrawTrajectory(
        Camera cam,
        PowerupAimState aimState,
        Color mainColor)
    {
        float mainThickness = aimProfile.TrajectoryThicknessPixels;
        float underlayThickness =
            mainThickness + aimProfile.TrajectoryUnderlayExtraPixels;

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
        bool hasPreviousPoint = false;
        Vector3 previousPoint = default;

        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            float normalizedTime = sampleIndex / (float)(sampleCount - 1);
            Vector3 worldPoint = PowerupTrajectory.Evaluate(
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
            aimState.LandingPosition,
            out Vector3 renderCenter
        ))
        {
            return;
        }

        int sampleCount = Mathf.Max(12, aimProfile.LandingCircleSampleCount);
        Color fillColor = aimProfile.GetFillColor(
            aimState.PowerupId,
            aimState.TargetValid
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

        float mainThickness = aimProfile.LandingOutlineThicknessPixels;
        float underlayThickness =
            mainThickness + aimProfile.LandingUnderlayExtraPixels;

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
        if (!TryBuildSurfaceBasis(
            aimState,
            out Vector3 basisRight,
            out Vector3 basisForward
        ) ||
            !TryProjectPoint(
                cam,
                aimState.LandingPosition,
                out Vector3 renderCenter
            ))
        {
            return;
        }

        float crossRadius =
            aimState.ImpactPreviewRadius *
            aimProfile.CenterCrossRadiusFraction;

        Vector3 rightA = aimState.LandingPosition - basisRight * crossRadius;
        Vector3 rightB = aimState.LandingPosition + basisRight * crossRadius;
        Vector3 forwardA =
            aimState.LandingPosition - basisForward * crossRadius;
        Vector3 forwardB =
            aimState.LandingPosition + basisForward * crossRadius;

        if (TryProjectPoint(cam, rightA, out Vector3 renderRightA) &&
            TryProjectPoint(cam, rightB, out Vector3 renderRightB))
        {
            DrawCrossLine(renderRightA, renderRightB, mainColor);
        }

        if (TryProjectPoint(cam, forwardA, out Vector3 renderForwardA) &&
            TryProjectPoint(cam, forwardB, out Vector3 renderForwardB))
        {
            DrawCrossLine(renderForwardA, renderForwardB, mainColor);
        }

        Draw.Disc(
            renderCenter,
            cam.transform.rotation,
            aimProfile.CenterDotRadiusPixels,
            mainColor
        );
    }

    private void DrawCrossLine(Vector3 start, Vector3 end, Color mainColor)
    {
        float mainThickness = aimProfile.CenterCrossThicknessPixels;
        float underlayThickness =
            mainThickness + aimProfile.LandingUnderlayExtraPixels;

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

        return aimState.LandingPosition +
               radialDirection * aimState.ImpactPreviewRadius;
    }

    private static bool TryBuildSurfaceBasis(
        PowerupAimState aimState,
        out Vector3 basisRight,
        out Vector3 basisForward)
    {
        Vector3 normal = aimState.LandingNormal.sqrMagnitude > 0.000001f
            ? aimState.LandingNormal.normalized
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

        screenPoint.z = Mathf.Max(
            aimProfile.NearCameraRenderDistance,
            cam.nearClipPlane + aimProfile.NearClipSafetyPadding
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
