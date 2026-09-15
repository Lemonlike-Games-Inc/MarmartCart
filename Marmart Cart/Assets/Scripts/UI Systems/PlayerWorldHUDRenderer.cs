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
///
/// CHECKOUT HUD:
/// - checkout suppression replaces the normal meters with the checkout prompt;
/// - each successfully registered cart flashes the session's cumulative base score;
/// - an earned streak bonus flashes once as the final cumulative checkout total.
/// </summary>
[DisallowMultipleComponent]
public class PlayerWorldHUDRenderer : ImmediateModeShapeDrawer
{
    #region References

    [Header("References")]
    [SerializeField] private PlayerWorldHUDSystem hudSystem;
    [SerializeField] private PlayerWorldHUDStateSystem stateSystem;
    [SerializeField] private PlayerWorldHUDLayoutProfile layoutProfile;
    [SerializeField] private CashScoreManager cashScoreManager;

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

    #endregion


    #region Mode Adaptation

    public void SetModeScaleMultiplier(float multiplier)
    {
        if (layoutProfile != null)
        {
            layoutProfile.SetRuntimeModeScaleMultiplier(multiplier);
        }
    }

    #endregion

    #region Unity

    private void Awake()
    {
        if (hudSystem == null) hudSystem = FindFirstObjectByType<PlayerWorldHUDSystem>();
        if (stateSystem == null) stateSystem = FindFirstObjectByType<PlayerWorldHUDStateSystem>();
        if (cashScoreManager == null) cashScoreManager = FindFirstObjectByType<CashScoreManager>();
    }

    public override void DrawShapes(Camera cam)
    {
        if (!drawingEnabled) return;
        if (cam == null || hudSystem == null || layoutProfile == null) return;

        bool checkoutPromptOnly =
            hudSystem.TryGetCheckoutSuppressedSlotForCamera(
                cam,
                out int playerIndex,
                out Transform hudAnchor
            );

        if (!checkoutPromptOnly &&
            !hudSystem.TryGetRenderableSlotForCamera(
                cam,
                out playerIndex,
                out hudAnchor
            ))
        {
            return;
        }

        if (hudAnchor == null) return;

        // The REAL cart anchor determines only where the HUD belongs on-screen.
        Vector3 anchorScreen = cam.WorldToScreenPoint(hudAnchor.position);
        if (skipWhenBehindCamera && anchorScreen.z <= 0f) return;

        // Optionally reconstruct that same screen X/Y on a fixed plane close to
        // this gameplay camera. The HUD still tracks the cart visually, but it
        // no longer physically intersects shelves, the cart, or the ground.
        Vector3 renderAnchorWorld = hudAnchor.position;

        if (layoutProfile.UseNearCameraRenderPlane)
        {
            float renderDepth = Mathf.Max(
                layoutProfile.NearCameraRenderDistance,
                cam.nearClipPlane + layoutProfile.NearClipSafetyPadding
            );

            anchorScreen.z = renderDepth;
            renderAnchorWorld = cam.ScreenToWorldPoint(anchorScreen);
        }

        if (checkoutPromptOnly)
        {
            using (Draw.Command(cam))
            {
                Draw.ResetAllDrawStates();
                Draw.BlendMode = ShapesBlendMode.Transparent;
                Draw.RadiusSpace = ThicknessSpace.Pixels;
                Draw.ThicknessSpace = ThicknessSpace.Pixels;
                Draw.LineGeometry = LineGeometry.Billboard;
                Draw.LineEndCaps = LineEndCap.Round;

                DrawCheckoutPrompt(
                    cam,
                    renderAnchorWorld
                );

                DrawCheckoutRegisteredScore(
                    cam,
                    renderAnchorWorld,
                    playerIndex
                );
            }

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
            -layoutProfile.HorizontalSeparationPixels,
            layoutProfile.VerticalOffsetPixels
        );

        Vector2 hypeCenterOffset = new Vector2(
            layoutProfile.HorizontalSeparationPixels,
            layoutProfile.VerticalOffsetPixels
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
            Draw.ResetAllDrawStates();

            Draw.BlendMode = ShapesBlendMode.Transparent;

            // No custom depth test override in this architecture.
            // The HUD wins normal depth testing because all of it is rendered
            // physically close to this gameplay camera.
            Draw.RadiusSpace = ThicknessSpace.Pixels;
            Draw.ThicknessSpace = ThicknessSpace.Pixels;
            Draw.LineGeometry = LineGeometry.Billboard;
            Draw.LineEndCaps = LineEndCap.Round;

            // Draw Hype first. Load is drawn second so its ticks/text are the
            // final HUD elements submitted by this drawer.
            DrawHypeMeter(
                cam,
                hypeCenterWorld,
                screenPlaneRotation,
                hypeNormalized,
                hudState,
                playerIndex
            );

            DrawLoadMeter(
                cam,
                loadCenterWorld,
                screenPlaneRotation,
                safeCapacityCount,
                currentLoadCount,
                overloadCount
            );

            // Warning placeholders are intentionally drawn after the two meters
            // so they are easy to inspect during this design pass.
            DrawHypeBurnWarningIcon(
                cam,
                renderAnchorWorld,
                hudState
            );

            DrawLowSpeedWarningIcon(
                cam,
                renderAnchorWorld,
                hudState
            );

            DrawMoveBackwardPrompt(
                cam,
                renderAnchorWorld,
                hudState
            );
        }
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
            layoutProfile.CheckoutPromptOffsetPixels;

        DrawRoundedScreenRectangle(
            cam,
            anchorScreen.z,
            groupCenter +
            layoutProfile.CheckoutPromptBackgroundOffsetPixels,
            layoutProfile.CheckoutPromptBackgroundSizePixels,
            layoutProfile.CheckoutPromptCornerRadiusPixels,
            layoutProfile.CheckoutPromptBackgroundColor
        );

        DrawPromptCircle(
            cam,
            anchorScreen.z,
            groupCenter +
            layoutProfile.CheckoutPromptCircleOffsetPixels,
            layoutProfile.CheckoutPromptCircleRadiusPixels,
            layoutProfile.CheckoutPromptCircleColor
        );

        DrawCenteredScreenText(
            cam,
            anchorScreen.z,
            groupCenter +
            layoutProfile.CheckoutPromptTextOffsetPixels,
            layoutProfile.CheckoutPromptText,
            layoutProfile.CheckoutPromptFontSizePixels,
            layoutProfile.CheckoutPromptTextColor
        );
    }

    private void DrawCheckoutRegisteredScore(
        Camera cam,
        Vector3 renderAnchorWorld,
        int playerIndex)
    {
        if (!layoutProfile.ShowCheckoutRegisteredScore)
        {
            return;
        }

        if (cashScoreManager == null)
        {
            cashScoreManager =
                FindFirstObjectByType<CashScoreManager>();
        }

        if (cashScoreManager == null ||
            !cashScoreManager.TryGetCheckoutScorePulse(
                playerIndex,
                out int cumulativeScore,
                out float registeredAtUnscaledTime
            ))
        {
            return;
        }

        float pulseAge =
            Mathf.Max(
                0f,
                Time.unscaledTime -
                registeredAtUnscaledTime
            );

        if (pulseAge >=
            layoutProfile.CheckoutRegisteredScoreLifetimeSeconds)
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
            layoutProfile.CheckoutPromptOffsetPixels;

        DrawCenteredScreenText(
            cam,
            anchorScreen.z,
            groupCenter +
            layoutProfile.CheckoutRegisteredScoreOffsetPixels,
            cumulativeScore.ToString(),
            layoutProfile.CheckoutRegisteredScoreFontSizePixels,
            layoutProfile.CheckoutRegisteredScoreColor
        );
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
            layoutProfile.MoveBackwardPromptOffsetPixels;

        DrawRoundedScreenRectangle(
            cam,
            anchorScreen.z,
            groupCenter +
            layoutProfile.MoveBackwardPromptBackgroundOffsetPixels,
            layoutProfile.MoveBackwardPromptBackgroundSizePixels,
            layoutProfile.MoveBackwardPromptCornerRadiusPixels,
            layoutProfile.MoveBackwardPromptBackgroundColor
        );

        DrawPromptCircle(
            cam,
            anchorScreen.z,
            groupCenter +
            layoutProfile.MoveBackwardPromptCircleOffsetPixels,
            layoutProfile.MoveBackwardPromptCircleRadiusPixels,
            layoutProfile.MoveBackwardPromptCircleColor
        );

        DrawCenteredScreenText(
            cam,
            anchorScreen.z,
            groupCenter +
            layoutProfile.MoveBackwardPromptTextOffsetPixels,
            layoutProfile.MoveBackwardPromptText,
            layoutProfile.MoveBackwardPromptFontSizePixels,
            layoutProfile.MoveBackwardPromptTextColor
        );
    }

    private void DrawRoundedScreenRectangle(
        Camera cam,
        float screenDepth,
        Vector2 centerScreen,
        Vector2 sizePixels,
        float cornerRadiusPixels,
        Color color)
    {
        float widthPixels = Mathf.Max(0.001f, sizePixels.x);
        float heightPixels = Mathf.Max(0.001f, sizePixels.y);

        float safeCornerPixels =
            Mathf.Clamp(
                cornerRadiusPixels,
                0f,
                Mathf.Min(widthPixels, heightPixels) * 0.5f
            );

        Vector3 centerWorld =
            ScreenPointToWorld(
                cam,
                centerScreen,
                screenDepth
            );

        float widthWorld =
            PixelsToWorldSizeAtDepth(
                cam,
                centerWorld,
                widthPixels
            );

        float heightWorld =
            PixelsToWorldSizeAtDepth(
                cam,
                centerWorld,
                heightPixels
            );

        float cornerRadiusWorld =
            PixelsToWorldSizeAtDepth(
                cam,
                centerWorld,
                safeCornerPixels
            );

        Color previousColor = Draw.Color;

        Draw.Matrix =
            Matrix4x4.TRS(
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

    private void DrawPromptCircle(
        Camera cam,
        float screenDepth,
        Vector2 centerScreen,
        float radiusPixels,
        Color color)
    {
        if (radiusPixels <= 0f) return;

        Draw.Disc(
            ScreenPointToWorld(
                cam,
                centerScreen,
                screenDepth
            ),
            cam.transform.rotation,
            radiusPixels,
            color
        );
    }

    private void DrawCenteredScreenText(
        Camera cam,
        float screenDepth,
        Vector2 centerScreen,
        string text,
        float fontSizePixels,
        Color color)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Vector3 textWorld =
            ScreenPointToWorld(
                cam,
                centerScreen,
                screenDepth
            );

        Draw.FontSize =
            PixelsToWorldSizeAtDepth(
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
        DrawFullRoundedArc(
            cam,
            centerWorld,
            rotation,
            layoutProfile.LoadRadiusPixels,
            layoutProfile.LoadTrackThicknessPixels,
            layoutProfile.LoadCenterAngleDegrees,
            layoutProfile.LoadSpanDegrees,
            layoutProfile.LoadTrackColor
        );

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
        );

        float capsuleLength = layoutProfile.LoadCapsuleLengthPixels;
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
                layoutProfile.LoadRadiusPixels,
                capsuleLength,
                capsuleThickness,
                capsuleColor
            );
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
                layoutProfile.LoadMilestoneTickLengthPixels,
                layoutProfile.LoadMilestoneTickThicknessPixels,
                layoutProfile.LoadMilestoneTickColor,
                layoutProfile.LoadMilestoneTextColor,
                layoutProfile.LoadMilestoneFontSizePixels,
                layoutProfile.LoadMilestoneLabelOffsetPixels,
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
            layoutProfile.CurrentLoadTickLengthPixels,
            layoutProfile.CurrentLoadTickThicknessPixels,
            tickColor,
            layoutProfile.CurrentLoadTextColor,
            layoutProfile.CurrentLoadFontSizePixels,
            layoutProfile.CurrentLoadLabelOffsetPixels,
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
            layoutProfile.LoadRadiusPixels +
            layoutProfile.LoadTrackThicknessPixels * 0.5f;

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

        Draw.Line(
            cam.ScreenToWorldPoint(startScreen3),
            cam.ScreenToWorldPoint(endScreen3),
            tickThicknessPixels,
            tickColor
        );

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

        // Shapes text lives on the same near-camera plane as the rest of the
        // HUD now, so it needs no separate depth workaround.
        Draw.FontSize =
            PixelsToWorldSizeAtDepth(
                cam,
                labelWorld,
                fontSizePixels
            );

        Color previousColor = Draw.Color;
        Draw.Color = textColor;

        Draw.Text(
            labelWorld,
            rotation,
            label,
            TextAlign.Right
        );

        Draw.Color = previousColor;
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
            layoutProfile.HypeRadiusPixels,
            layoutProfile.HypeTrackThicknessPixels,
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
                    layoutProfile.HypeRadiusPixels +
                    layoutProfile.HypeRewardPreviewRadiusOffsetPixels,
                    layoutProfile.HypeRewardPreviewThicknessPixels,
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
                layoutProfile.HypeRadiusPixels,
                layoutProfile.HypeFillThicknessPixels,
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
                    layoutProfile.HypeRadiusPixels +
                    layoutProfile.HypePenaltyPreviewRadiusOffsetPixels,
                    layoutProfile.HypePenaltyPreviewThicknessPixels,
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

        if (playerIndex < 0 ||
            playerIndex >= displayedHypeNormalized.Length)
        {
            return targetNormalized;
        }

        if (!layoutProfile.AnimateHypeGainFill)
        {
            displayedHypeNormalized[playerIndex] = targetNormalized;
            hypeFillInitialized[playerIndex] = true;
            return targetNormalized;
        }

        if (!hypeFillInitialized[playerIndex])
        {
            displayedHypeNormalized[playerIndex] = targetNormalized;
            hypeFillInitialized[playerIndex] = true;
            return targetNormalized;
        }

        float displayed = displayedHypeNormalized[playerIndex];

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
                layoutProfile.HypeGainFillSpeedNormalizedPerSecond * Time.deltaTime
            );
        }

        displayedHypeNormalized[playerIndex] = displayed;
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
            layoutProfile.HypeBurnWarningOffsetPixels;

        // One severity-mapped color drives the ENTIRE icon so ring, arrow,
        // and bolt always read as one visual warning unit.
        Color warningColor =
            layoutProfile.GetHypeBurnWarningColor(
                state.HypeBurnMultiplier
            );

        DrawWarningBackgroundCircle(
            cam,
            anchorScreen.z,
            iconCenter + layoutProfile.HypeBurnWarningBackgroundOffsetPixels,
            layoutProfile.HypeBurnWarningBackgroundRadiusPixels,
            layoutProfile.HypeBurnWarningBackgroundColor
        );

        DrawHypeBurnWarningRing(
            cam,
            anchorScreen.z,
            iconCenter,
            warningColor
        );

        DrawHypeBurnWarningArrow(
            cam,
            anchorScreen.z,
            iconCenter,
            warningColor
        );

        DrawHypeBurnWarningBolt(
            cam,
            anchorScreen.z,
            iconCenter,
            warningColor
        );
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
            ScreenPointToWorld(
                cam,
                iconCenter,
                screenDepth
            );

        Draw.Arc(
            centerWorld,
            cam.transform.rotation,
            layoutProfile.HypeBurnWarningRingRadiusPixels,
            layoutProfile.HypeBurnWarningRingThicknessPixels,
            arcStartDegrees * Mathf.Deg2Rad,
            arcEndDegrees * Mathf.Deg2Rad,
            ArcEndCap.Round,
            color
        );
    }

    private void DrawHypeBurnWarningArrow(
        Camera cam,
        float screenDepth,
        Vector2 iconCenter,
        Color color)
    {
        Vector2 arrowCenter =
            iconCenter +
            layoutProfile.HypeBurnWarningArrowOffsetPixels;

        float shaftLength =
            layoutProfile.HypeBurnWarningArrowShaftLengthPixels;

        float shaftHalf =
            shaftLength * 0.5f;

        Vector2 shaftTop =
            arrowCenter +
            new Vector2(0f, shaftHalf);

        Vector2 shaftBottom =
            arrowCenter +
            new Vector2(0f, -shaftHalf);

        Draw.Line(
            ScreenPointToWorld(cam, shaftTop, screenDepth),
            ScreenPointToWorld(cam, shaftBottom, screenDepth),
            layoutProfile.HypeBurnWarningArrowShaftThicknessPixels,
            color
        );

        float headWidthHalf =
            layoutProfile.HypeBurnWarningArrowHeadWidthPixels * 0.5f;

        float headHeight =
            layoutProfile.HypeBurnWarningArrowHeadHeightPixels;

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

        Draw.Triangle(
            ScreenPointToWorld(cam, a, screenDepth),
            ScreenPointToWorld(cam, b, screenDepth),
            ScreenPointToWorld(cam, c, screenDepth),
            color
        );
    }

    private void DrawHypeBurnWarningBolt(
        Camera cam,
        float screenDepth,
        Vector2 iconCenter,
        Color color)
    {
        Vector2 boltCenter =
            iconCenter +
            layoutProfile.HypeBurnWarningBoltOffsetPixels;

        DrawObtuseBoltTriangle(
            cam,
            screenDepth,
            boltCenter + layoutProfile.HypeBurnWarningBoltTriangleAOffsetPixels,
            layoutProfile.HypeBurnWarningBoltTriangleAWidthPixels,
            layoutProfile.HypeBurnWarningBoltTriangleAHeightPixels,
            layoutProfile.HypeBurnWarningBoltTriangleASkewPixels,
            layoutProfile.HypeBurnWarningBoltTriangleARotationDegrees,
            color
        );

        DrawObtuseBoltTriangle(
            cam,
            screenDepth,
            boltCenter + layoutProfile.HypeBurnWarningBoltTriangleBOffsetPixels,
            layoutProfile.HypeBurnWarningBoltTriangleBWidthPixels,
            layoutProfile.HypeBurnWarningBoltTriangleBHeightPixels,
            layoutProfile.HypeBurnWarningBoltTriangleBSkewPixels,
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

        Draw.Triangle(
            ScreenPointToWorld(cam, a, screenDepth),
            ScreenPointToWorld(cam, b, screenDepth),
            ScreenPointToWorld(cam, c, screenDepth),
            color
        );
    }

    private Vector3 ScreenPointToWorld(
        Camera cam,
        Vector2 screenPoint,
        float screenDepth)
    {
        return cam.ScreenToWorldPoint(
            new Vector3(
                screenPoint.x,
                screenPoint.y,
                screenDepth
            )
        );
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

        Vector2 iconCenter = new Vector2(anchorScreen.x, anchorScreen.y) + layoutProfile.LowSpeedWarningOffsetPixels;

        Color warningColor = layoutProfile.GetLowSpeedWarningColor(state.MaxSpeedPenaltyNormalized);

        DrawWarningBackgroundCircle(
            cam,
            anchorScreen.z,
            iconCenter + layoutProfile.LowSpeedWarningBackgroundOffsetPixels,
            layoutProfile.LowSpeedWarningBackgroundRadiusPixels,
            layoutProfile.LowSpeedWarningBackgroundColor
        );

        DrawLowSpeedWarningCart(
            cam,
            anchorScreen.z,
            iconCenter + layoutProfile.LowSpeedWarningCartOffsetPixels,
            warningColor
        );

        DrawLowSpeedWarningWeight(
            cam,
            anchorScreen.z,
            iconCenter + layoutProfile.LowSpeedWarningWeightOffsetPixels,
            warningColor
        );

        DrawLowSpeedWarningArrow(
            cam,
            anchorScreen.z,
            iconCenter + layoutProfile.LowSpeedWarningArrowOffsetPixels,
            warningColor
        );
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
        float thickness = layoutProfile.LowSpeedWarningCartStrokeThicknessPixels;

        Vector2 basketTopLeft = iconCenter + layoutProfile.LowSpeedWarningCartBasketTopLeftOffsetPixels;
        Vector2 basketTopRight = iconCenter + layoutProfile.LowSpeedWarningCartBasketTopRightOffsetPixels;
        Vector2 basketBottomRight = iconCenter + layoutProfile.LowSpeedWarningCartBasketBottomRightOffsetPixels;
        Vector2 basketBottomLeft = iconCenter + layoutProfile.LowSpeedWarningCartBasketBottomLeftOffsetPixels;

        // 1) Basket trapezoid.
        DrawScreenLine(cam, screenDepth, basketTopLeft, basketTopRight, thickness, color);
        DrawScreenLine(cam, screenDepth, basketTopRight, basketBottomRight, thickness, color);
        DrawScreenLine(cam, screenDepth, basketBottomRight, basketBottomLeft, thickness, color);
        DrawScreenLine(cam, screenDepth, basketBottomLeft, basketTopLeft, thickness, color);

        // 2) + 3) Two-segment handle.
        Vector2 handleJoint = iconCenter + layoutProfile.LowSpeedWarningCartHandleJointOffsetPixels;
        Vector2 handleEnd = iconCenter + layoutProfile.LowSpeedWarningCartHandleEndOffsetPixels;
        DrawScreenLine(cam, screenDepth, basketTopLeft, handleJoint, thickness, color);
        DrawScreenLine(cam, screenDepth, handleJoint, handleEnd, thickness, color);

        // 4) Two wheels only.
        Vector2 leftWheelCenter = iconCenter + layoutProfile.LowSpeedWarningCartLeftWheelOffsetPixels;
        Vector2 rightWheelCenter = iconCenter + layoutProfile.LowSpeedWarningCartRightWheelOffsetPixels;
        DrawScreenCircleStroke(cam, screenDepth, leftWheelCenter, layoutProfile.LowSpeedWarningCartWheelRadiusPixels, thickness, color);
        DrawScreenCircleStroke(cam, screenDepth, rightWheelCenter, layoutProfile.LowSpeedWarningCartWheelRadiusPixels, thickness, color);

        // 5) Two base segments sharing one middle point.
        Vector2 baseLeftEnd = iconCenter + layoutProfile.LowSpeedWarningCartBaseLeftEndOffsetPixels;
        Vector2 baseMid = iconCenter + layoutProfile.LowSpeedWarningCartBaseMidOffsetPixels;
        Vector2 baseRightEnd = iconCenter + layoutProfile.LowSpeedWarningCartBaseRightEndOffsetPixels;
        DrawScreenLine(cam, screenDepth, baseLeftEnd, baseMid, thickness, color);
        DrawScreenLine(cam, screenDepth, baseMid, baseRightEnd, thickness, color);
    }

    private void DrawLowSpeedWarningWeight(
        Camera cam,
        float screenDepth,
        Vector2 center,
        Color color)
    {
        float widthTop = layoutProfile.LowSpeedWarningWeightTopWidthPixels;
        float widthBottom = layoutProfile.LowSpeedWarningWeightBottomWidthPixels;
        float height = layoutProfile.LowSpeedWarningWeightBodyHeightPixels;
        float halfHeight = height * 0.5f;

        Vector2 topLeft = center + new Vector2(-widthTop * 0.5f, halfHeight);
        Vector2 topRight = center + new Vector2(widthTop * 0.5f, halfHeight);
        Vector2 bottomRight = center + new Vector2(widthBottom * 0.5f, -halfHeight);
        Vector2 bottomLeft = center + new Vector2(-widthBottom * 0.5f, -halfHeight);

        // Solid trapezoid body.
        DrawScreenTriangle(cam, screenDepth, topLeft, topRight, bottomRight, color);
        DrawScreenTriangle(cam, screenDepth, topLeft, bottomRight, bottomLeft, color);

        // Ring handle.
        Vector2 ringCenter = center + layoutProfile.LowSpeedWarningWeightRingOffsetPixels;
        DrawScreenCircleStroke(
            cam,
            screenDepth,
            ringCenter,
            layoutProfile.LowSpeedWarningWeightRingRadiusPixels,
            layoutProfile.LowSpeedWarningWeightRingThicknessPixels,
            color
        );
    }

    private void DrawLowSpeedWarningArrow(
        Camera cam,
        float screenDepth,
        Vector2 center,
        Color color)
    {
        float shaftLength = layoutProfile.LowSpeedWarningArrowShaftLengthPixels;
        float shaftHalf = shaftLength * 0.5f;

        Vector2 top = center + new Vector2(0f, shaftHalf);
        Vector2 bottom = center + new Vector2(0f, -shaftHalf);

        DrawScreenLine(
            cam,
            screenDepth,
            top,
            bottom,
            layoutProfile.LowSpeedWarningArrowShaftThicknessPixels,
            color
        );

        float headWidthHalf = layoutProfile.LowSpeedWarningArrowHeadWidthPixels * 0.5f;
        float headHeight = layoutProfile.LowSpeedWarningArrowHeadHeightPixels;

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
            ScreenPointToWorld(cam, a, screenDepth),
            ScreenPointToWorld(cam, b, screenDepth),
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
            ScreenPointToWorld(cam, a, screenDepth),
            ScreenPointToWorld(cam, b, screenDepth),
            ScreenPointToWorld(cam, c, screenDepth),
            color
        );
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
            ScreenPointToWorld(cam, center, screenDepth),
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
            ScreenPointToWorld(cam, center, screenDepth),
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

    private float PixelsToWorldSizeAtDepth(
        Camera cam,
        Vector3 worldPosition,
        float pixelSize)
    {
        Vector3 screenA =
            cam.WorldToScreenPoint(worldPosition);

        Vector3 screenB =
            screenA;

        screenB.y += pixelSize;

        Vector3 worldA =
            cam.ScreenToWorldPoint(screenA);

        Vector3 worldB =
            cam.ScreenToWorldPoint(screenB);

        return Vector3.Distance(
            worldA,
            worldB
        );
    }

    #endregion
}
