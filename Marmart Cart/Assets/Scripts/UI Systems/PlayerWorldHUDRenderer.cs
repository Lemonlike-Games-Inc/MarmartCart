using Shapes;
using UnityEngine;

/// <summary>
/// Step 5G.1.2 renderer - simplified low-speed warning cart/weight icon.
///
///
/// LOAD normal mode:
/// - 1..20 uses the original growing bar;
/// - 21 starts a new 21..40 rolling window;
/// - 41 starts 41..60, etc.;
/// - Pre-Slots represent future capacity inside the current window;
/// - decade milestone ticks show 10/20/30/40/...;
/// - current tick always shows the true Current Load.
///
/// LOAD overload mode:
/// - overload never creates extra visible slots;
/// - overload first consumes ANY unused Pre-Slots in the current visual window;
/// - this works for the 10-slot baseline AND rolling windows such as 21..40;
/// - after Pre-Slots are exhausted, additional overload replaces loaded safe
///   capsules from the top downward;
/// - once the whole visible window is red, the pointer loops top -> bottom.
///
/// HYPE:
/// - positive Hype gains animate the active fill toward the new value;
/// - Hype decreases remain immediate for responsive spending/loss feedback.
/// - the current fill pulses between two profile colors while Speed Up is active.
///
/// CONTROL PROMPTS:
/// - backward-movement availability draws a profile-authored rounded prompt;
/// - visibility comes only from the local player's semantic HUD state.
/// </summary>
[DisallowMultipleComponent]
public class PlayerWorldHUDRenderer : ImmediateModeShapeDrawer
{
    #region References

    [Header("References")]
    [SerializeField] private PlayerWorldHUDSystem hudSystem;
    [SerializeField] private PlayerWorldHUDStateSystem stateSystem;
    [SerializeField] private PlayerWorldHUDLayoutProfile layoutProfile;

    [Tooltip(
        "Optional semantic feature toggles. If left unassigned, supported HUD channels default to visible."
    )]
    [SerializeField] private PlayerWorldHUDFeatureProfile featureProfile;

    #endregion

    #region Visibility

    [Header("Visibility")]
    [SerializeField] private bool drawingEnabled = true;
    [SerializeField] private bool skipWhenBehindCamera = true;

    #endregion

    #region Visual Runtime

    private readonly float[] displayedHypeNormalized = new float[4];
    private readonly bool[] hypeFillInitialized = new bool[4];

    // Reuse immutable local-space meshes across player cameras. Camera motion
    // changes Draw.Matrix, never the path vertices already queued for rendering.
    private PolygonPath filledTrianglePath;
    private PolygonPath weightBodyPath;
    private float weightTopWidthRatio;

    #endregion


    #region Mode Adaptation

    // UiScale times the profile's 2P/4P factor for the camera being drawn.
    // Every profile pixel value is multiplied by it.
    private float drawScale = 1f;

    #endregion

    #region Unity

    private void Awake()
    {
        if (hudSystem == null) hudSystem = FindFirstObjectByType<PlayerWorldHUDSystem>();
        if (stateSystem == null) stateSystem = FindFirstObjectByType<PlayerWorldHUDStateSystem>();
    }

    private void OnDestroy()
    {
        filledTrianglePath?.Dispose();
        weightBodyPath?.Dispose();
        filledTrianglePath = null;
        weightBodyPath = null;
    }

    public override void DrawShapes(Camera cam)
    {
        if (!drawingEnabled) return;
        if (cam == null || hudSystem == null || layoutProfile == null) return;

        if (!HudShapes.TryGetViewPlayer(cam, out int playerIndex)) return;
        if (!PlayerHudVisibility.IsShown(playerIndex)) return;

        // During checkout the normal meters are suppressed and only the checkout prompt draws.
        bool checkoutPromptOnly = hudSystem.TryGetCheckoutAnchor(playerIndex, out Transform hudAnchor);

        if (!checkoutPromptOnly && !hudSystem.TryGetRenderableAnchor(playerIndex, out hudAnchor)) return;

        // The REAL cart anchor determines only where the HUD belongs on-screen.
        Vector3 anchorScreen = cam.WorldToScreenPoint(hudAnchor.position);
        if (skipWhenBehindCamera && anchorScreen.z <= 0f) return;

        drawScale = HudShapes.Scale(layoutProfile.GetModeScale(HudShapes.ViewCount()));

        // Optionally reconstruct that same screen X/Y on a fixed plane close to
        // this gameplay camera. The HUD still tracks the cart visually, but it
        // no longer physically intersects shelves, the cart, or the ground.
        Vector3 renderAnchorWorld = hudAnchor.position;

        if (layoutProfile.UseNearCameraRenderPlane)
        {
            anchorScreen.z = HudShapes.RenderDepth(
                cam,
                layoutProfile.NearCameraRenderDistance,
                layoutProfile.NearClipSafetyPadding
            );

            renderAnchorWorld = cam.ScreenToWorldPoint(anchorScreen);
        }

        if (checkoutPromptOnly)
        {
            DrawCheckoutPrompt(cam, renderAnchorWorld);
            return;
        }

        PlayerWorldHUDState hudState = null;

        float hypeNormalized = 0f;
        int safeCapacityCount = 0;
        int currentLoadCount = 0;
        int overloadCount = 0;

        if (stateSystem != null &&
            stateSystem.TryGetState(playerIndex, out hudState) &&
            hudState != null)
        {
            hypeNormalized = hudState.HypeNormalized;
            safeCapacityCount = Mathf.Max(0, Mathf.RoundToInt(hudState.SafeCapacity));
            currentLoadCount = Mathf.Max(0, Mathf.RoundToInt(hudState.CurrentLoad));

            overloadCount = Mathf.Max(
                0,
                Mathf.Max(
                    Mathf.RoundToInt(hudState.OverloadAmount),
                    currentLoadCount - safeCapacityCount
                )
            );
        }

        Vector2 loadCenterOffset = new Vector2(
            -(layoutProfile.HorizontalSeparationPixels * drawScale),
            (layoutProfile.VerticalOffsetPixels * drawScale)
        );

        Vector2 hypeCenterOffset = new Vector2(
            (layoutProfile.HorizontalSeparationPixels * drawScale),
            (layoutProfile.VerticalOffsetPixels * drawScale)
        );

        Vector3 loadCenterWorld = ScreenOffsetToWorld(
            cam,
            renderAnchorWorld,
            loadCenterOffset
        );

        Vector3 hypeCenterWorld = ScreenOffsetToWorld(
            cam,
            renderAnchorWorld,
            hypeCenterOffset
        );

        Quaternion screenPlaneRotation = cam.transform.rotation;

        using (Draw.Command(cam))
        {
            HudShapes.ApplyHudDrawState();

            // This command contains only the Hype arc primitives.
            DrawHypeMeter(
                cam,
                hypeCenterWorld,
                screenPlaneRotation,
                hypeNormalized,
                hudState,
                playerIndex
            );
        }

        // These groups own their command scopes. Do not wrap them in another
        // command: each pass isolates a primitive type and resets Shapes state.
        DrawLoadMeter(
            cam,
            loadCenterWorld,
            screenPlaneRotation,
            safeCapacityCount,
            currentLoadCount,
            overloadCount
        );

        DrawHypeBurnWarningIcon(cam, renderAnchorWorld, hudState);
        DrawLowSpeedWarningIcon(cam, renderAnchorWorld, hudState);
        DrawMoveBackwardPrompt(cam, renderAnchorWorld, hudState);
    }

    #endregion

    #region Checkout Prompt

    private void DrawCheckoutPrompt(
        Camera cam,
        Vector3 renderAnchorWorld)
    {
        if (!layoutProfile.ShowCheckoutPrompt) return;

        Vector3 anchorScreen =
            cam.WorldToScreenPoint(
                renderAnchorWorld
            );

        Vector2 groupCenter =
            new Vector2(
                anchorScreen.x,
                anchorScreen.y
            ) +
            (layoutProfile.CheckoutPromptOffsetPixels * drawScale);

        using (Draw.Command(cam))
        {
            HudShapes.ApplyHudDrawState();
            HudShapes.DrawRoundedScreenRectangle(
                cam,
                anchorScreen.z,
                groupCenter + (layoutProfile.CheckoutPromptBackgroundOffsetPixels * drawScale),
                (layoutProfile.CheckoutPromptBackgroundSizePixels * drawScale),
                (layoutProfile.CheckoutPromptCornerRadiusPixels * drawScale),
                layoutProfile.CheckoutPromptBackgroundColor
            );
        }

        using (Draw.Command(cam))
        {
            HudShapes.ApplyHudDrawState();
            DrawPromptCircle(
                cam,
                anchorScreen.z,
                groupCenter + (layoutProfile.CheckoutPromptCircleOffsetPixels * drawScale),
                (layoutProfile.CheckoutPromptCircleRadiusPixels * drawScale),
                layoutProfile.CheckoutPromptCircleColor
            );
        }

        using (Draw.Command(cam))
        {
            HudShapes.ApplyHudDrawState();
            HudShapes.DrawCenteredScreenText(
                cam,
                anchorScreen.z,
                groupCenter + (layoutProfile.CheckoutPromptTextOffsetPixels * drawScale),
                layoutProfile.CheckoutPromptText,
                (layoutProfile.CheckoutPromptFontSizePixels * drawScale),
                layoutProfile.CheckoutPromptTextColor
            );
        }
    }

    #endregion

    #region Move Backward Prompt

    private void DrawMoveBackwardPrompt(
        Camera cam,
        Vector3 renderAnchorWorld,
        PlayerWorldHUDState state)
    {
        if (state == null ||
            !state.CanMoveBackward ||
            !layoutProfile.ShowMoveBackwardPrompt)
        {
            return;
        }

        Vector3 anchorScreen =
            cam.WorldToScreenPoint(
                renderAnchorWorld
            );

        Vector2 groupCenter =
            new Vector2(
                anchorScreen.x,
                anchorScreen.y
            ) +
            (layoutProfile.MoveBackwardPromptOffsetPixels * drawScale);

        using (Draw.Command(cam))
        {
            HudShapes.ApplyHudDrawState();
            HudShapes.DrawRoundedScreenRectangle(
                cam,
                anchorScreen.z,
                groupCenter + (layoutProfile.MoveBackwardPromptBackgroundOffsetPixels * drawScale),
                (layoutProfile.MoveBackwardPromptBackgroundSizePixels * drawScale),
                (layoutProfile.MoveBackwardPromptCornerRadiusPixels * drawScale),
                layoutProfile.MoveBackwardPromptBackgroundColor
            );
        }

        using (Draw.Command(cam))
        {
            HudShapes.ApplyHudDrawState();
            DrawPromptCircle(
                cam,
                anchorScreen.z,
                groupCenter + (layoutProfile.MoveBackwardPromptCircleOffsetPixels * drawScale),
                (layoutProfile.MoveBackwardPromptCircleRadiusPixels * drawScale),
                layoutProfile.MoveBackwardPromptCircleColor
            );
        }

        using (Draw.Command(cam))
        {
            HudShapes.ApplyHudDrawState();
            HudShapes.DrawCenteredScreenText(
                cam,
                anchorScreen.z,
                groupCenter + (layoutProfile.MoveBackwardPromptTextOffsetPixels * drawScale),
                layoutProfile.MoveBackwardPromptText,
                (layoutProfile.MoveBackwardPromptFontSizePixels * drawScale),
                layoutProfile.MoveBackwardPromptTextColor
            );
        }
    }

    private void DrawPromptCircle(
        Camera cam,
        float screenDepth,
        Vector2 centerScreen,
        float radiusPixels,
        Color color)
    {
        if (radiusPixels <= 0f) return;

        Draw.Disc(
            HudShapes.ScreenToWorld(
                cam,
                centerScreen,
                screenDepth
            ),
            cam.transform.rotation,
            radiusPixels,
            color
        );
    }

    #endregion

    #region Load Meter

    private void DrawLoadMeter(
        Camera cam,
        Vector3 centerWorld,
        Quaternion rotation,
        int safeCapacityCount,
        int currentLoadCount,
        int overloadCount)
    {
        using (Draw.Command(cam))
        {
            HudShapes.ApplyHudDrawState();
            DrawFullRoundedArc(
                cam,
                centerWorld,
                rotation,
                (layoutProfile.LoadRadiusPixels * drawScale),
                (layoutProfile.LoadTrackThicknessPixels * drawScale),
                layoutProfile.LoadCenterAngleDegrees,
                layoutProfile.LoadSpanDegrees,
                layoutProfile.LoadTrackColor
            );
        }

        bool overloaded = overloadCount > 0;

        // IMPORTANT:
        // The visual window is ALWAYS chosen from Safe Capacity exactly as it
        // would be immediately before overload begins. Entering overload must
        // never change the page/window by itself.
        GetLoadWindow(
            safeCapacityCount,
            out int windowBase,
            out int visibleSlotCount,
            out int safeSlotsInWindow
        );

        float capsuleThickness = layoutProfile.GetLoadCapsuleThicknessPixels(
            visibleSlotCount
        ) * drawScale;

        float capsuleLength = (layoutProfile.LoadCapsuleLengthPixels * drawScale);
        float halfSpan = layoutProfile.LoadSpanDegrees * 0.5f;

        float bottomDegrees =
            layoutProfile.LoadCenterAngleDegrees + halfSpan;

        float topDegrees =
            layoutProfile.LoadCenterAngleDegrees - halfSpan;

        Vector3 centerScreen =
            cam.WorldToScreenPoint(centerWorld);

        Color overloadColor =
            layoutProfile.GetLoadOverloadColor(overloadCount);

        int safeLoadedTotal =
            Mathf.Min(currentLoadCount, safeCapacityCount);

        int safeLoadedInWindow = Mathf.Clamp(
            safeLoadedTotal - windowBase,
            0,
            safeSlotsInWindow
        );

        int preSlotsInWindow = Mathf.Max(
            0,
            visibleSlotCount - safeSlotsInWindow
        );

        // UNIVERSAL overload rule:
        //
        // 1) Red overload capsules first append into whatever Pre-Slots still
        //    exist in the CURRENT visual window.
        //
        //    Examples:
        //      Safe=4  in baseline 1..10  -> overload starts at slot 5.
        //      Safe=33 in window 21..40   -> overload starts at slot 34.
        //
        // 2) Once those Pre-Slots are exhausted, further overload starts
        //    replacing already-loaded safe capsules from the top downward.
        //
        // 3) Once every visible capsule is red, only the pointer loops.
        int appendedOverloadCount = overloaded
            ? Mathf.Min(overloadCount, preSlotsInWindow)
            : 0;

        int replacementOverloadCount = overloaded
            ? Mathf.Min(
                Mathf.Max(0, overloadCount - preSlotsInWindow),
                safeLoadedInWindow
            )
            : 0;

        int remainingFilledSafeCount = Mathf.Max(
            0,
            safeLoadedInWindow - replacementOverloadCount
        );

        // Capsule lines stay together, before the separate tick/text passes.
        using (Draw.Command(cam))
        {
            HudShapes.ApplyHudDrawState();
            for (int i = 0; i < visibleSlotCount; i++)
            {
                float angleDegrees = GetSlotAngleDegrees(
                    i,
                    visibleSlotCount,
                    bottomDegrees,
                    topDegrees
                );

                Color capsuleColor;

                if (!overloaded)
                {
                    if (i >= safeSlotsInWindow)
                    {
                        capsuleColor = layoutProfile.LoadPreSlotColor;
                    }
                    else if (i < safeLoadedInWindow)
                    {
                        capsuleColor = layoutProfile.LoadFilledColor;
                    }
                    else
                    {
                        capsuleColor = layoutProfile.LoadEmptyColor;
                    }
                }
                else if (i < safeSlotsInWindow)
                {
                    // Safe-capacity region.
                    if (i < remainingFilledSafeCount)
                    {
                        capsuleColor = layoutProfile.LoadFilledColor;
                    }
                    else if (i < safeLoadedInWindow)
                    {
                        capsuleColor = overloadColor;
                    }
                    else
                    {
                        capsuleColor = layoutProfile.LoadEmptyColor;
                    }
                }
                else
                {
                    // Pre-Slot region. Overload occupies these from bottom->top,
                    // exactly like the next load capsules would have appeared.
                    int preSlotIndex = i - safeSlotsInWindow;

                    capsuleColor = preSlotIndex < appendedOverloadCount
                        ? overloadColor
                        : layoutProfile.LoadPreSlotColor;
                }

                DrawRadialCapsule(
                    cam,
                    centerScreen,
                    angleDegrees * Mathf.Deg2Rad,
                    (layoutProfile.LoadRadiusPixels * drawScale),
                    capsuleLength,
                    capsuleThickness,
                    capsuleColor
                );
            }
        }

        DrawLoadMilestoneTicks(
            cam,
            centerWorld,
            rotation,
            centerScreen,
            windowBase,
            visibleSlotCount,
            bottomDegrees,
            topDegrees,
            currentLoadCount,
            overloaded
        );

        DrawCurrentLoadTick(
            cam,
            centerWorld,
            rotation,
            centerScreen,
            windowBase,
            visibleSlotCount,
            safeSlotsInWindow,
            safeLoadedInWindow,
            preSlotsInWindow,
            bottomDegrees,
            topDegrees,
            currentLoadCount,
            overloadCount,
            overloadColor
        );
    }

    /// <summary>
    /// The load window is based ONLY on Safe Capacity.
    /// Overload never changes pages by itself.
    ///
    /// Safe 0..10:
    ///     base 0, visible 10
    ///
    /// Safe 11..20:
    ///     base 0, visible = Safe
    ///
    /// Safe 21:
    ///     base 20, visible 20, safe-in-window 1, Pre-Slots 22..40
    ///
    /// Safe 33:
    ///     base 20, visible 20, safe-in-window 13, Pre-Slots 34..40
    ///
    /// Safe 40:
    ///     base 20, visible 20, safe-in-window 20
    ///
    /// Safe 41:
    ///     base 40, visible 20, safe-in-window 1, Pre-Slots 42..60
    /// </summary>
    private void GetLoadWindow(
        int safeCapacityCount,
        out int windowBase,
        out int visibleSlotCount,
        out int safeSlotsInWindow)
    {
        int minimum = layoutProfile.MinimumVisualSlotCount;
        int windowSize = layoutProfile.LoadWindowSize;

        if (safeCapacityCount <= windowSize)
        {
            windowBase = 0;

            visibleSlotCount = Mathf.Clamp(
                Mathf.Max(safeCapacityCount, minimum),
                minimum,
                windowSize
            );

            safeSlotsInWindow = Mathf.Clamp(
                safeCapacityCount,
                0,
                visibleSlotCount
            );

            return;
        }

        windowBase =
            ((safeCapacityCount - 1) / windowSize) * windowSize;

        visibleSlotCount = windowSize;

        safeSlotsInWindow = Mathf.Clamp(
            safeCapacityCount - windowBase,
            0,
            windowSize
        );
    }

    private void DrawRadialCapsule(
        Camera cam,
        Vector3 centerScreen,
        float angleRadians,
        float radiusPixels,
        float lengthPixels,
        float thicknessPixels,
        Color color)
    {
        Vector2 radial =
            ShapesMath.AngToDir(angleRadians);

        Vector2 capsuleCenter =
            new Vector2(
                centerScreen.x + radial.x * radiusPixels,
                centerScreen.y + radial.y * radiusPixels
            );

        Vector2 halfRadial =
            radial * (lengthPixels * 0.5f);

        Vector3 screenA =
            new Vector3(
                capsuleCenter.x - halfRadial.x,
                capsuleCenter.y - halfRadial.y,
                centerScreen.z
            );

        Vector3 screenB =
            new Vector3(
                capsuleCenter.x + halfRadial.x,
                capsuleCenter.y + halfRadial.y,
                centerScreen.z
            );

        Draw.Line(
            cam.ScreenToWorldPoint(screenA),
            cam.ScreenToWorldPoint(screenB),
            thicknessPixels,
            color
        );
    }

    #endregion

    #region Load Ticks

    private void DrawLoadMilestoneTicks(
        Camera cam,
        Vector3 centerWorld,
        Quaternion rotation,
        Vector3 centerScreen,
        int windowBase,
        int visibleSlotCount,
        float bottomDegrees,
        float topDegrees,
        int currentLoadCount,
        bool overloaded)
    {
        if (!layoutProfile.ShowLoadMilestoneTicks) return;

        int firstValue = windowBase + 1;
        int lastValue = windowBase + visibleSlotCount;
        int interval = layoutProfile.LoadMilestoneInterval;

        int firstMilestone =
            Mathf.CeilToInt(firstValue / (float)interval) * interval;

        for (
            int milestone = firstMilestone;
            milestone <= lastValue;
            milestone += interval)
        {
            // If current pointer already sits at exactly this non-overload
            // value, let the brighter current tick own that location.
            if (!overloaded && milestone == currentLoadCount)
            {
                continue;
            }

            int localIndex =
                milestone - windowBase - 1;

            if (localIndex < 0 ||
                localIndex >= visibleSlotCount)
            {
                continue;
            }

            float angleDegrees =
                GetSlotAngleDegrees(
                    localIndex,
                    visibleSlotCount,
                    bottomDegrees,
                    topDegrees
                );

            DrawLoadTickAndLabel(
                cam,
                centerWorld,
                rotation,
                centerScreen,
                angleDegrees,
                (layoutProfile.LoadMilestoneTickLengthPixels * drawScale),
                (layoutProfile.LoadMilestoneTickThicknessPixels * drawScale),
                layoutProfile.LoadMilestoneTickColor,
                layoutProfile.LoadMilestoneTextColor,
                (layoutProfile.LoadMilestoneFontSizePixels * drawScale),
                (layoutProfile.LoadMilestoneLabelOffsetPixels * drawScale),
                milestone.ToString()
            );
        }
    }

    private void DrawCurrentLoadTick(
        Camera cam,
        Vector3 centerWorld,
        Quaternion rotation,
        Vector3 centerScreen,
        int windowBase,
        int visibleSlotCount,
        int safeSlotsInWindow,
        int safeLoadedInWindow,
        int preSlotsInWindow,
        float bottomDegrees,
        float topDegrees,
        int currentLoadCount,
        int overloadCount,
        Color overloadColor)
    {
        if (!layoutProfile.ShowCurrentLoadTick) return;

        int pointerIndex;

        if (overloadCount > 0)
        {
            if (overloadCount <= preSlotsInWindow && preSlotsInWindow > 0)
            {
                // Overload is still filling available Pre-Slots.
                // Follow the newest appended overload capsule.
                pointerIndex =
                    safeSlotsInWindow +
                    overloadCount -
                    1;
            }
            else
            {
                int replacementStep =
                    overloadCount - preSlotsInWindow;

                if (replacementStep > 0 &&
                    replacementStep <= safeLoadedInWindow)
                {
                    // Pre-Slots are full. Continue by walking backward through
                    // the loaded safe capsules from top -> bottom.
                    pointerIndex =
                        safeLoadedInWindow -
                        replacementStep;
                }
                else
                {
                    // The whole meaningful visible load region is already red.
                    // Preserve the existing repeating warning behavior.
                    int coveredVisibleCount =
                        preSlotsInWindow +
                        safeLoadedInWindow;

                    int beyondCovered = Mathf.Max(
                        1,
                        overloadCount - coveredVisibleCount
                    );

                    int loopPosition =
                        (beyondCovered - 1) % visibleSlotCount;

                    pointerIndex =
                        visibleSlotCount - 1 - loopPosition;
                }
            }
        }
        else if (currentLoadCount <= 0)
        {
            pointerIndex = 0;
        }
        else
        {
            int localValue =
                currentLoadCount - windowBase;

            pointerIndex = Mathf.Clamp(
                localValue - 1,
                0,
                visibleSlotCount - 1
            );
        }

        pointerIndex = Mathf.Clamp(
            pointerIndex,
            0,
            visibleSlotCount - 1
        );

        float angleDegrees =
            GetSlotAngleDegrees(
                pointerIndex,
                visibleSlotCount,
                bottomDegrees,
                topDegrees
            );

        Color tickColor =
            overloadCount > 0
                ? overloadColor
                : layoutProfile.CurrentLoadTickColor;

        DrawLoadTickAndLabel(
            cam,
            centerWorld,
            rotation,
            centerScreen,
            angleDegrees,
            (layoutProfile.CurrentLoadTickLengthPixels * drawScale),
            (layoutProfile.CurrentLoadTickThicknessPixels * drawScale),
            tickColor,
            layoutProfile.CurrentLoadTextColor,
            (layoutProfile.CurrentLoadFontSizePixels * drawScale),
            (layoutProfile.CurrentLoadLabelOffsetPixels * drawScale),
            currentLoadCount.ToString()
        );
    }

    private void DrawLoadTickAndLabel(
        Camera cam,
        Vector3 centerWorld,
        Quaternion rotation,
        Vector3 centerScreen,
        float angleDegrees,
        float tickLengthPixels,
        float tickThicknessPixels,
        Color tickColor,
        Color textColor,
        float fontSizePixels,
        float labelOffsetPixels,
        string label)
    {
        float angleRadians =
            angleDegrees * Mathf.Deg2Rad;

        Vector2 radial =
            ShapesMath.AngToDir(angleRadians);

        float outerRadius =
            (layoutProfile.LoadRadiusPixels * drawScale) +
            (layoutProfile.LoadTrackThicknessPixels * drawScale) * 0.5f;

        Vector2 tickStartScreen =
            new Vector2(
                centerScreen.x + radial.x * outerRadius,
                centerScreen.y + radial.y * outerRadius
            );

        Vector2 tickEndScreen =
            tickStartScreen +
            radial * tickLengthPixels;

        Vector3 startScreen3 =
            new Vector3(
                tickStartScreen.x,
                tickStartScreen.y,
                centerScreen.z
            );

        Vector3 endScreen3 =
            new Vector3(
                tickEndScreen.x,
                tickEndScreen.y,
                centerScreen.z
            );

        using (Draw.Command(cam))
        {
            HudShapes.ApplyHudDrawState();
            Draw.Line(
                cam.ScreenToWorldPoint(startScreen3),
                cam.ScreenToWorldPoint(endScreen3),
                tickThicknessPixels,
                tickColor
            );
        }

        Vector2 labelScreen =
            tickEndScreen +
            radial * labelOffsetPixels;

        Vector3 labelScreen3 =
            new Vector3(
                labelScreen.x,
                labelScreen.y,
                centerScreen.z
            );

        Vector3 labelWorld =
            cam.ScreenToWorldPoint(labelScreen3);

        using (Draw.Command(cam))
        {
            HudShapes.ApplyHudDrawState();
            Draw.FontSize = HudShapes.PixelsToWorldSize(
                cam,
                labelWorld,
                fontSizePixels
            );
            Draw.Color = textColor;
            Draw.Text(labelWorld, rotation, label, TextAlign.Right);
        }
    }

    #endregion

    #region Hype Meter

    private void DrawHypeMeter(
        Camera cam,
        Vector3 centerWorld,
        Quaternion rotation,
        float normalizedValue,
        PlayerWorldHUDState state,
        int playerIndex)
    {
        normalizedValue = GetDisplayedHypeNormalized(
            playerIndex,
            normalizedValue
        );
        // 1. Background track.
        DrawFullRoundedArc(
            cam,
            centerWorld,
            rotation,
            (layoutProfile.HypeRadiusPixels * drawScale),
            (layoutProfile.HypeTrackThicknessPixels * drawScale),
            layoutProfile.HypeCenterAngleDegrees,
            layoutProfile.HypeSpanDegrees,
            layoutProfile.HypeTrackColor
        );

        float currentValue =
            Mathf.Clamp01(
                normalizedValue
            );

        float halfSpan =
            layoutProfile.HypeSpanDegrees * 0.5f;

        float bottomDegrees =
            layoutProfile.HypeCenterAngleDegrees -
            halfSpan;

        float topDegrees =
            layoutProfile.HypeCenterAngleDegrees +
            halfSpan;

        float currentDegrees =
            Mathf.Lerp(
                bottomDegrees,
                topDegrees,
                currentValue
            );

        bool showReward =
            state != null &&
            (featureProfile == null ||
             featureProfile.ShowDriftRewardPreview);

        bool showPenalty =
            state != null &&
            (featureProfile == null ||
             featureProfile.ShowDriftPenaltyPreview);

        // 2. SUCCESS REWARD PREVIEW FIRST.
        //
        // It intentionally sits UNDER the active Hype meter and penalty.
        // This hides the reward segment's inner rounded start cap under
        // the current Hype fill, giving us a cleaner continuous junction.
        if (showReward &&
            state.DriftRewardPreviewActive)
        {
            float projectedSuccess =
                Mathf.Clamp01(
                    state.ProjectedHypeAfterDriftRewardNormalized
                );

            if (projectedSuccess > currentValue)
            {
                float rewardEndDegrees =
                    Mathf.Lerp(
                        bottomDegrees,
                        topDegrees,
                        projectedSuccess
                    );

                DrawRoundedArcSection(
                    cam,
                    centerWorld,
                    rotation,
                    (layoutProfile.HypeRadiusPixels * drawScale) +
                    (layoutProfile.HypeRewardPreviewRadiusOffsetPixels * drawScale),
                    (layoutProfile.HypeRewardPreviewThicknessPixels * drawScale),
                    currentDegrees,
                    rewardEndDegrees,
                    layoutProfile.HypeRewardPreviewColor
                );
            }
        }

        // 3. CURRENT HYPE OVER THE REWARD PREVIEW.
        if (currentValue > 0f)
        {
            Color currentFillColor =
                GetCurrentHypeFillColor(
                    state
                );

            DrawRoundedArcSection(
                cam,
                centerWorld,
                rotation,
                (layoutProfile.HypeRadiusPixels * drawScale),
                (layoutProfile.HypeFillThicknessPixels * drawScale),
                bottomDegrees,
                currentDegrees,
                currentFillColor
            );
        }

        // 4. FAILURE PENALTY LAST.
        //
        // It overlays the portion of CURRENT Hype that would actually be
        // lost if the drift fails.
        if (showPenalty &&
            state.DriftPenaltyPreviewActive)
        {
            float projectedFailure =
                Mathf.Clamp01(
                    state.ProjectedHypeAfterDriftPenaltyNormalized
                );

            if (projectedFailure < currentValue)
            {
                float penaltyStartDegrees =
                    Mathf.Lerp(
                        bottomDegrees,
                        topDegrees,
                        projectedFailure
                    );

                DrawRoundedArcSection(
                    cam,
                    centerWorld,
                    rotation,
                    (layoutProfile.HypeRadiusPixels * drawScale) +
                    (layoutProfile.HypePenaltyPreviewRadiusOffsetPixels * drawScale),
                    (layoutProfile.HypePenaltyPreviewThicknessPixels * drawScale),
                    penaltyStartDegrees,
                    currentDegrees,
                    layoutProfile.HypePenaltyPreviewColor
                );
            }
        }
    }

    private Color GetCurrentHypeFillColor(
        PlayerWorldHUDState state)
    {
        Color normalColor =
            layoutProfile.HypeFillColor;

        if (state == null ||
            !state.IsSpeedingUp ||
            !layoutProfile.FlashHypeFillWhileSpeedingUp)
        {
            return normalColor;
        }

        float phaseRadians =
            Time.unscaledTime *
            layoutProfile.HypeSpeedupFlashFrequencyHz *
            Mathf.PI *
            2f;

        // Smooth cosine pulse: 0 = normal color, 1 = flash color.
        float flashBlend =
            0.5f -
            0.5f * Mathf.Cos(phaseRadians);

        return Color.Lerp(
            normalColor,
            layoutProfile.HypeSpeedupFlashColor,
            flashBlend
        );
    }

    private float GetDisplayedHypeNormalized(
        int playerIndex,
        float targetNormalized)
    {
        targetNormalized = Mathf.Clamp01(targetNormalized);

        // playerIndex is 1..4; the arrays are 0-based.
        int slot = playerIndex - 1;

        if (slot < 0 ||
            slot >= displayedHypeNormalized.Length)
        {
            return targetNormalized;
        }

        if (!layoutProfile.AnimateHypeGainFill)
        {
            displayedHypeNormalized[slot] = targetNormalized;
            hypeFillInitialized[slot] = true;
            return targetNormalized;
        }

        if (!hypeFillInitialized[slot])
        {
            displayedHypeNormalized[slot] = targetNormalized;
            hypeFillInitialized[slot] = true;
            return targetNormalized;
        }

        float displayed = displayedHypeNormalized[slot];

        // Loss/spending is intentionally immediate. This keeps Hype burn,
        // failed actions, and other reductions responsive and truthful.
        if (targetNormalized <= displayed)
        {
            displayed = targetNormalized;
        }
        else
        {
            displayed = Mathf.MoveTowards(
                displayed,
                targetNormalized,
                layoutProfile.HypeGainFillSpeedNormalizedPerSecond * Time.unscaledDeltaTime
            );
        }

        displayedHypeNormalized[slot] = displayed;
        return displayed;
    }

    #endregion

    #region Hype Burn Warning Icon

    private void DrawHypeBurnWarningIcon(
        Camera cam,
        Vector3 renderAnchorWorld,
        PlayerWorldHUDState state)
    {
        if (!ShouldDrawHypeBurnWarning(state))
        {
            return;
        }

        Vector3 anchorScreen =
            cam.WorldToScreenPoint(
                renderAnchorWorld
            );

        Vector2 iconCenter =
            new Vector2(
                anchorScreen.x,
                anchorScreen.y
            ) +
            (layoutProfile.HypeBurnWarningOffsetPixels * drawScale);

        // One severity-mapped color drives the ENTIRE icon so ring, arrow,
        // and bolt always read as one visual warning unit.
        Color warningColor =
            layoutProfile.GetHypeBurnWarningColor(
                state.HypeBurnMultiplier
            );

        using (Draw.Command(cam))
        {
            HudShapes.ApplyHudDrawState();
            DrawWarningBackgroundCircle(
                cam,
                anchorScreen.z,
                iconCenter + (layoutProfile.HypeBurnWarningBackgroundOffsetPixels * drawScale),
                (layoutProfile.HypeBurnWarningBackgroundRadiusPixels * drawScale),
                layoutProfile.HypeBurnWarningBackgroundColor
            );
        }

        using (Draw.Command(cam))
        {
            HudShapes.ApplyHudDrawState();
            DrawHypeBurnWarningRing(cam, anchorScreen.z, iconCenter, warningColor);
        }

        using (Draw.Command(cam))
        {
            HudShapes.ApplyHudDrawState();
            DrawHypeBurnWarningArrowShaft(cam, anchorScreen.z, iconCenter, warningColor);
        }

        // Mesh polygons bypass Draw.Triangle for the arrowhead and both bolt
        // pieces while preserving their original profile-authored vertices.
        using (Draw.Command(cam))
        {
            HudShapes.ApplyHudDrawState();
            DrawHypeBurnWarningArrowHead(cam, anchorScreen.z, iconCenter, warningColor);
            DrawHypeBurnWarningBolt(cam, anchorScreen.z, iconCenter, warningColor);
        }
    }

    private bool ShouldDrawHypeBurnWarning(
        PlayerWorldHUDState state)
    {
        if (state == null)
        {
            return false;
        }

        if (featureProfile != null &&
            !featureProfile.ShowHypeBurnFeedback)
        {
            return false;
        }

        return state.HypeBurnAboveBaseline > 0.0001f;
    }

    private void DrawHypeBurnWarningRing(
        Camera cam,
        float screenDepth,
        Vector2 iconCenter,
        Color color)
    {
        float gapCenterDegrees =
            layoutProfile.HypeBurnWarningGapCenterDegrees;

        float gapHalfDegrees =
            layoutProfile.HypeBurnWarningGapDegrees * 0.5f;

        float arcStartDegrees =
            gapCenterDegrees + gapHalfDegrees;

        float arcEndDegrees =
            gapCenterDegrees + 360f - gapHalfDegrees;

        Vector3 centerWorld =
            HudShapes.ScreenToWorld(
                cam,
                iconCenter,
                screenDepth
            );

        Draw.Arc(
            centerWorld,
            cam.transform.rotation,
            (layoutProfile.HypeBurnWarningRingRadiusPixels * drawScale),
            (layoutProfile.HypeBurnWarningRingThicknessPixels * drawScale),
            arcStartDegrees * Mathf.Deg2Rad,
            arcEndDegrees * Mathf.Deg2Rad,
            ArcEndCap.Round,
            color
        );
    }

    private void DrawHypeBurnWarningArrowShaft(
        Camera cam,
        float screenDepth,
        Vector2 iconCenter,
        Color color)
    {
        Vector2 arrowCenter =
            iconCenter +
            (layoutProfile.HypeBurnWarningArrowOffsetPixels * drawScale);

        float shaftLength =
            (layoutProfile.HypeBurnWarningArrowShaftLengthPixels * drawScale);

        float shaftHalf =
            shaftLength * 0.5f;

        Vector2 shaftTop =
            arrowCenter +
            new Vector2(0f, shaftHalf);

        Vector2 shaftBottom =
            arrowCenter +
            new Vector2(0f, -shaftHalf);

        Draw.Line(
            HudShapes.ScreenToWorld(cam, shaftTop, screenDepth),
            HudShapes.ScreenToWorld(cam, shaftBottom, screenDepth),
            (layoutProfile.HypeBurnWarningArrowShaftThicknessPixels * drawScale),
            color
        );
    }

    private void DrawHypeBurnWarningArrowHead(
        Camera cam,
        float screenDepth,
        Vector2 iconCenter,
        Color color)
    {
        Vector2 arrowCenter = iconCenter + (layoutProfile.HypeBurnWarningArrowOffsetPixels * drawScale);
        Vector2 shaftBottom = arrowCenter + new Vector2(
            0f,
            -(layoutProfile.HypeBurnWarningArrowShaftLengthPixels * drawScale) * 0.5f
        );

        float headWidthHalf =
            (layoutProfile.HypeBurnWarningArrowHeadWidthPixels * drawScale) * 0.5f;

        float headHeight =
            (layoutProfile.HypeBurnWarningArrowHeadHeightPixels * drawScale);

        Vector2 headBaseCenter = shaftBottom;

        Vector2 a =
            headBaseCenter +
            new Vector2(-headWidthHalf, 0f);

        Vector2 b =
            headBaseCenter +
            new Vector2(headWidthHalf, 0f);

        Vector2 c =
            headBaseCenter +
            new Vector2(0f, -headHeight);

        DrawScreenTrianglePolygon(cam, screenDepth, a, b, c, color);
    }

    private void DrawHypeBurnWarningBolt(
        Camera cam,
        float screenDepth,
        Vector2 iconCenter,
        Color color)
    {
        Vector2 boltCenter =
            iconCenter +
            (layoutProfile.HypeBurnWarningBoltOffsetPixels * drawScale);

        DrawObtuseBoltTriangle(
            cam,
            screenDepth,
            boltCenter + (layoutProfile.HypeBurnWarningBoltTriangleAOffsetPixels * drawScale),
            (layoutProfile.HypeBurnWarningBoltTriangleAWidthPixels * drawScale),
            (layoutProfile.HypeBurnWarningBoltTriangleAHeightPixels * drawScale),
            (layoutProfile.HypeBurnWarningBoltTriangleASkewPixels * drawScale),
            layoutProfile.HypeBurnWarningBoltTriangleARotationDegrees,
            color
        );

        DrawObtuseBoltTriangle(
            cam,
            screenDepth,
            boltCenter + (layoutProfile.HypeBurnWarningBoltTriangleBOffsetPixels * drawScale),
            (layoutProfile.HypeBurnWarningBoltTriangleBWidthPixels * drawScale),
            (layoutProfile.HypeBurnWarningBoltTriangleBHeightPixels * drawScale),
            (layoutProfile.HypeBurnWarningBoltTriangleBSkewPixels * drawScale),
            layoutProfile.HypeBurnWarningBoltTriangleBRotationDegrees,
            color
        );
    }

    private void DrawObtuseBoltTriangle(
        Camera cam,
        float screenDepth,
        Vector2 center,
        float width,
        float height,
        float skewPixels,
        float rotationDegrees,
        Color color)
    {
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;

        Vector2 localA = new Vector2(-halfWidth, halfHeight);
        Vector2 localB = new Vector2(halfWidth, halfHeight);
        Vector2 localC = new Vector2(-halfWidth + skewPixels, -halfHeight);

        Quaternion rotation = Quaternion.Euler(0f, 0f, rotationDegrees);

        Vector2 a = center + (Vector2)(rotation * localA);
        Vector2 b = center + (Vector2)(rotation * localB);
        Vector2 c = center + (Vector2)(rotation * localC);

        DrawScreenTrianglePolygon(cam, screenDepth, a, b, c, color);
    }

    #endregion

    #region Low Speed Warning Icon

    private void DrawLowSpeedWarningIcon(
        Camera cam,
        Vector3 renderAnchorWorld,
        PlayerWorldHUDState state)
    {
        if (!ShouldDrawLowSpeedWarning(state))
        {
            return;
        }

        Vector3 anchorScreen = cam.WorldToScreenPoint(renderAnchorWorld);

        Vector2 iconCenter = new Vector2(anchorScreen.x, anchorScreen.y) + (layoutProfile.LowSpeedWarningOffsetPixels * drawScale);

        Color warningColor = layoutProfile.GetLowSpeedWarningColor(state.MaxSpeedPenaltyNormalized);

        Vector2 cartCenter = iconCenter + (layoutProfile.LowSpeedWarningCartOffsetPixels * drawScale);
        Vector2 weightCenter = iconCenter + (layoutProfile.LowSpeedWarningWeightOffsetPixels * drawScale);
        Vector2 arrowCenter = iconCenter + (layoutProfile.LowSpeedWarningArrowOffsetPixels * drawScale);

        using (Draw.Command(cam))
        {
            HudShapes.ApplyHudDrawState();
            DrawWarningBackgroundCircle(
                cam,
                anchorScreen.z,
                iconCenter + (layoutProfile.LowSpeedWarningBackgroundOffsetPixels * drawScale),
                (layoutProfile.LowSpeedWarningBackgroundRadiusPixels * drawScale),
                layoutProfile.LowSpeedWarningBackgroundColor
            );
        }

        using (Draw.Command(cam))
        {
            HudShapes.ApplyHudDrawState();
            DrawLowSpeedWarningCart(cam, anchorScreen.z, cartCenter, warningColor);
            DrawLowSpeedWarningArrowShaft(cam, anchorScreen.z, arrowCenter, warningColor);
        }

        using (Draw.Command(cam))
        {
            HudShapes.ApplyHudDrawState();
            DrawLowSpeedWarningWheels(cam, anchorScreen.z, cartCenter, warningColor);
        }

        // Only the missing weight body switches to a polygon. Keep the
        // working low-speed arrowhead on its original Triangle draw path.
        using (Draw.Command(cam))
        {
            HudShapes.ApplyHudDrawState();
            DrawLowSpeedWarningWeight(cam, anchorScreen.z, weightCenter, warningColor);
        }

        using (Draw.Command(cam))
        {
            HudShapes.ApplyHudDrawState();
            DrawLowSpeedWarningArrowHead(cam, anchorScreen.z, arrowCenter, warningColor);
        }

        // Keep the handle above the filled weight body if they overlap.
        using (Draw.Command(cam))
        {
            HudShapes.ApplyHudDrawState();
            DrawLowSpeedWarningWeightHandle(cam, anchorScreen.z, weightCenter, warningColor);
        }
    }

    private bool ShouldDrawLowSpeedWarning(PlayerWorldHUDState state)
    {
        if (state == null)
        {
            return false;
        }

        if (featureProfile != null &&
            (!featureProfile.ShowOverloadSpeedWarning || !featureProfile.ShowSpeedConsequence))
        {
            return false;
        }

        return state.HasMaxSpeedPenalty;
    }

    private void DrawLowSpeedWarningCart(
        Camera cam,
        float screenDepth,
        Vector2 iconCenter,
        Color color)
    {
        float thickness = (layoutProfile.LowSpeedWarningCartStrokeThicknessPixels * drawScale);

        Vector2 basketTopLeft = iconCenter + (layoutProfile.LowSpeedWarningCartBasketTopLeftOffsetPixels * drawScale);
        Vector2 basketTopRight = iconCenter + (layoutProfile.LowSpeedWarningCartBasketTopRightOffsetPixels * drawScale);
        Vector2 basketBottomRight = iconCenter + (layoutProfile.LowSpeedWarningCartBasketBottomRightOffsetPixels * drawScale);
        Vector2 basketBottomLeft = iconCenter + (layoutProfile.LowSpeedWarningCartBasketBottomLeftOffsetPixels * drawScale);

        // 1) Basket trapezoid.
        DrawScreenLine(cam, screenDepth, basketTopLeft, basketTopRight, thickness, color);
        DrawScreenLine(cam, screenDepth, basketTopRight, basketBottomRight, thickness, color);
        DrawScreenLine(cam, screenDepth, basketBottomRight, basketBottomLeft, thickness, color);
        DrawScreenLine(cam, screenDepth, basketBottomLeft, basketTopLeft, thickness, color);

        // 2) + 3) Two-segment handle.
        Vector2 handleJoint = iconCenter + (layoutProfile.LowSpeedWarningCartHandleJointOffsetPixels * drawScale);
        Vector2 handleEnd = iconCenter + (layoutProfile.LowSpeedWarningCartHandleEndOffsetPixels * drawScale);
        DrawScreenLine(cam, screenDepth, basketTopLeft, handleJoint, thickness, color);
        DrawScreenLine(cam, screenDepth, handleJoint, handleEnd, thickness, color);

        // Two base segments sharing one middle point. The wheel arcs have
        // their own pass so this method only emits Lines.
        Vector2 baseLeftEnd = iconCenter + (layoutProfile.LowSpeedWarningCartBaseLeftEndOffsetPixels * drawScale);
        Vector2 baseMid = iconCenter + (layoutProfile.LowSpeedWarningCartBaseMidOffsetPixels * drawScale);
        Vector2 baseRightEnd = iconCenter + (layoutProfile.LowSpeedWarningCartBaseRightEndOffsetPixels * drawScale);
        DrawScreenLine(cam, screenDepth, baseLeftEnd, baseMid, thickness, color);
        DrawScreenLine(cam, screenDepth, baseMid, baseRightEnd, thickness, color);
    }

    private void DrawLowSpeedWarningWheels(
        Camera cam,
        float screenDepth,
        Vector2 iconCenter,
        Color color)
    {
        float thickness = (layoutProfile.LowSpeedWarningCartStrokeThicknessPixels * drawScale);
        Vector2 leftWheelCenter = iconCenter + (layoutProfile.LowSpeedWarningCartLeftWheelOffsetPixels * drawScale);
        Vector2 rightWheelCenter = iconCenter + (layoutProfile.LowSpeedWarningCartRightWheelOffsetPixels * drawScale);
        DrawScreenCircleStroke(cam, screenDepth, leftWheelCenter, (layoutProfile.LowSpeedWarningCartWheelRadiusPixels * drawScale), thickness, color);
        DrawScreenCircleStroke(cam, screenDepth, rightWheelCenter, (layoutProfile.LowSpeedWarningCartWheelRadiusPixels * drawScale), thickness, color);
    }

    private void DrawLowSpeedWarningWeight(
        Camera cam,
        float screenDepth,
        Vector2 center,
        Color color)
    {
        float widthTop = (layoutProfile.LowSpeedWarningWeightTopWidthPixels * drawScale);
        float widthBottom = (layoutProfile.LowSpeedWarningWeightBottomWidthPixels * drawScale);
        float height = (layoutProfile.LowSpeedWarningWeightBodyHeightPixels * drawScale);
        if (widthTop <= 0f || widthBottom <= 0f || height <= 0f) return;

        float topWidthRatio = widthTop / widthBottom;
        if (weightBodyPath == null || topWidthRatio != weightTopWidthRatio)
        {
            // Rebuild only when the authored top/bottom proportions change.
            // Uniform HUD scaling is applied by the drawing matrix below.
            weightBodyPath?.Dispose();
            weightBodyPath = new PolygonPath();
            weightBodyPath.AddPoint(-topWidthRatio * 0.5f, 0.5f);
            weightBodyPath.AddPoint(topWidthRatio * 0.5f, 0.5f);
            weightBodyPath.AddPoint(0.5f, -0.5f);
            weightBodyPath.AddPoint(-0.5f, -0.5f);
            weightTopWidthRatio = topWidthRatio;
        }

        DrawScreenPolygon(
            cam,
            screenDepth,
            weightBodyPath,
            center,
            new Vector2(widthBottom, 0f),
            new Vector2(0f, height),
            color
        );
    }

    private void DrawLowSpeedWarningWeightHandle(
        Camera cam,
        float screenDepth,
        Vector2 center,
        Color color)
    {
        Vector2 ringCenter = center + (layoutProfile.LowSpeedWarningWeightRingOffsetPixels * drawScale);
        DrawScreenCircleStroke(
            cam,
            screenDepth,
            ringCenter,
            (layoutProfile.LowSpeedWarningWeightRingRadiusPixels * drawScale),
            (layoutProfile.LowSpeedWarningWeightRingThicknessPixels * drawScale),
            color
        );
    }

    private void DrawLowSpeedWarningArrowShaft(
        Camera cam,
        float screenDepth,
        Vector2 center,
        Color color)
    {
        float shaftLength = (layoutProfile.LowSpeedWarningArrowShaftLengthPixels * drawScale);
        float shaftHalf = shaftLength * 0.5f;

        Vector2 top = center + new Vector2(0f, shaftHalf);
        Vector2 bottom = center + new Vector2(0f, -shaftHalf);

        DrawScreenLine(
            cam,
            screenDepth,
            top,
            bottom,
            (layoutProfile.LowSpeedWarningArrowShaftThicknessPixels * drawScale),
            color
        );
    }

    private void DrawLowSpeedWarningArrowHead(
        Camera cam,
        float screenDepth,
        Vector2 center,
        Color color)
    {
        Vector2 bottom = center + new Vector2(
            0f,
            -(layoutProfile.LowSpeedWarningArrowShaftLengthPixels * drawScale) * 0.5f
        );

        float headWidthHalf = (layoutProfile.LowSpeedWarningArrowHeadWidthPixels * drawScale) * 0.5f;
        float headHeight = (layoutProfile.LowSpeedWarningArrowHeadHeightPixels * drawScale);

        Vector2 a = bottom + new Vector2(-headWidthHalf, 0f);
        Vector2 b = bottom + new Vector2(headWidthHalf, 0f);
        Vector2 c = bottom + new Vector2(0f, -headHeight);

        DrawScreenTriangle(cam, screenDepth, a, b, c, color);
    }

    private void DrawScreenLine(
        Camera cam,
        float screenDepth,
        Vector2 a,
        Vector2 b,
        float thicknessPixels,
        Color color)
    {
        Draw.Line(
            HudShapes.ScreenToWorld(cam, a, screenDepth),
            HudShapes.ScreenToWorld(cam, b, screenDepth),
            thicknessPixels,
            color
        );
    }

    private void DrawScreenTriangle(
        Camera cam,
        float screenDepth,
        Vector2 a,
        Vector2 b,
        Vector2 c,
        Color color)
    {
        Draw.Triangle(
            HudShapes.ScreenToWorld(cam, a, screenDepth),
            HudShapes.ScreenToWorld(cam, b, screenDepth),
            HudShapes.ScreenToWorld(cam, c, screenDepth),
            color
        );
    }

    private void DrawScreenTrianglePolygon(
        Camera cam,
        float screenDepth,
        Vector2 a,
        Vector2 b,
        Vector2 c,
        Color color)
    {
        float signedArea =
            (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        if (Mathf.Abs(signedArea) <= 0.0001f) return;

        // Keep clockwise winding and a non-reflected transform for every
        // arrow direction and either orientation of the bolt pieces.
        if (signedArea > 0f)
        {
            Vector2 swap = b;
            b = c;
            c = swap;
        }

        if (filledTrianglePath == null)
        {
            filledTrianglePath = new PolygonPath();
            filledTrianglePath.AddPoint(0f, 0f);
            filledTrianglePath.AddPoint(1f, 0f);
            filledTrianglePath.AddPoint(0f, -1f);
        }

        DrawScreenPolygon(
            cam,
            screenDepth,
            filledTrianglePath,
            a,
            b - a,
            a - c,
            color
        );
    }

    private void DrawScreenPolygon(
        Camera cam,
        float screenDepth,
        PolygonPath path,
        Vector2 originPixels,
        Vector2 xAxisPixels,
        Vector2 yAxisPixels,
        Color color)
    {
        // Polygon vertices use local XY coordinates. Project the pixel basis
        // onto this camera's render plane so size/offset stay correct in 2P/4P.
        Vector3 origin = HudShapes.ScreenToWorld(cam, originPixels, screenDepth);
        Vector3 xAxis = HudShapes.ScreenToWorld(cam, originPixels + xAxisPixels, screenDepth) - origin;
        Vector3 yAxis = HudShapes.ScreenToWorld(cam, originPixels + yAxisPixels, screenDepth) - origin;
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
            Draw.Polygon(path, color);
        }
        finally
        {
            Draw.Matrix = previousMatrix;
        }
    }

    private void DrawScreenCircleStroke(
        Camera cam,
        float screenDepth,
        Vector2 center,
        float radiusPixels,
        float thicknessPixels,
        Color color)
    {
        Draw.Arc(
            HudShapes.ScreenToWorld(cam, center, screenDepth),
            cam.transform.rotation,
            radiusPixels,
            thicknessPixels,
            0f,
            Mathf.PI * 2f,
            ArcEndCap.Round,
            color
        );
    }

    #endregion

    #region Warning Background Helper

    private void DrawWarningBackgroundCircle(
        Camera cam,
        float screenDepth,
        Vector2 center,
        float radiusPixels,
        Color color)
    {
        Draw.Disc(
            HudShapes.ScreenToWorld(cam, center, screenDepth),
            cam.transform.rotation,
            radiusPixels,
            color
        );
    }

    #endregion

    #region Rounded Arc Drawing

    private void DrawFullRoundedArc(
        Camera cam,
        Vector3 centerWorld,
        Quaternion rotation,
        float radiusPixels,
        float thicknessPixels,
        float centerAngleDegrees,
        float spanDegrees,
        Color color)
    {
        float halfSpan =
            spanDegrees * 0.5f;

        DrawRoundedArcSection(
            cam,
            centerWorld,
            rotation,
            radiusPixels,
            thicknessPixels,
            centerAngleDegrees - halfSpan,
            centerAngleDegrees + halfSpan,
            color
        );
    }

    private void DrawRoundedArcSection(
        Camera cam,
        Vector3 centerWorld,
        Quaternion rotation,
        float radiusPixels,
        float thicknessPixels,
        float startDegrees,
        float endDegrees,
        Color color)
    {
        if (thicknessPixels <= 0f) return;
        if (Mathf.Approximately(startDegrees, endDegrees)) return;

        float startRadians =
            startDegrees * Mathf.Deg2Rad;

        float endRadians =
            endDegrees * Mathf.Deg2Rad;

        // Use Shapes' NATIVE rounded arc end caps.
        //
        // Previously we drew:
        //     Arc + separate half-disc at each endpoint
        //
        // Even though the half-disc only covered the outward half, its
        // anti-aliased edge still touched the arc's anti-aliased edge.
        // With transparent blending that could produce a faint seam.
        //
        // Native ArcEndCap.Round keeps the body + rounded cap inside ONE
        // Shapes arc primitive, so there is no transparent primitive overlap.
        Draw.Arc(
            centerWorld,
            rotation,
            radiusPixels,
            thicknessPixels,
            startRadians,
            endRadians,
            ArcEndCap.Round,
            color
        );
    }

    #endregion

    #region Helpers

    private float GetSlotAngleDegrees(
        int slotIndex,
        int visibleSlotCount,
        float bottomDegrees,
        float topDegrees)
    {
        if (visibleSlotCount <= 1)
        {
            return Mathf.Lerp(
                bottomDegrees,
                topDegrees,
                0.5f
            );
        }

        float t =
            slotIndex /
            (visibleSlotCount - 1f);

        return Mathf.Lerp(
            bottomDegrees,
            topDegrees,
            t
        );
    }

    private Vector3 ScreenOffsetToWorld(
        Camera cam,
        Vector3 anchorWorld,
        Vector2 pixelOffset)
    {
        Vector3 screenPoint =
            cam.WorldToScreenPoint(anchorWorld);

        screenPoint.x += pixelOffset.x;
        screenPoint.y += pixelOffset.y;

        return cam.ScreenToWorldPoint(
            screenPoint
        );
    }

    #endregion
}
