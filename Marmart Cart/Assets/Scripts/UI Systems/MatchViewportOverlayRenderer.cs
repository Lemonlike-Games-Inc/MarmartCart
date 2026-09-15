using Shapes;
using UnityEngine;

/// <summary>
/// Scene-level Shapes renderer for viewport-attached match UI.
///
/// STEP 4:
/// - timer at top-center of each active player viewport;
/// - leaderboard below timer;
/// - every viewport sees all ACTIVE players;
/// - local player's P# label becomes YOU;
/// - official rank uses BANKED score only;
/// - projected checkout score extends the same bar as a per-player second color;
/// - potential score uses a fixed-pixel repeating chevron pattern;
/// - projected streak reward recolors the exact right-end chevron interval;
/// - larger synchronized rear fills create outlines behind the foreground fills;
/// - one shared tiered score maximum is used by every row / viewport;
/// - leaderboard bars are slightly-rounded rectangles, not capsules;
/// - bar endpoints animate in unscaled time with profile-authored easing;
/// - player/rank text each render as a movable, scalable background group;
/// - rank changes animate complete entries between rows;
/// - upward-moving entries render above overlapping entries;
/// - mode-aware full-screen split dividers share this same Shapes pass;
/// - no score numbers or crowns.
/// </summary>
[DisallowMultipleComponent]
public class MatchViewportOverlayRenderer : ImmediateModeShapeDrawer
{
    #region References

    [Header("References")]
    [SerializeField] private PlayerWorldHUDSystem hudSystem;
    [SerializeField] private MatchViewportOverlayStateSystem stateSystem;
    [SerializeField] private MatchViewportOverlayProfile profile;

    #endregion

    #region Mode Presentation

    [Header("Mode Presentation - Runtime Read Only")]
    [SerializeField] private int configuredPlayerCount = 2;

    public int ConfiguredPlayerCount => configuredPlayerCount;

    /// <summary>
    /// Selects the 2P or 4P root offset and master scale authored in the
    /// viewport-overlay profile. MatchSceneModeController owns this choice.
    /// </summary>
    public void SetModePlayerCount(int playerCount)
    {
        configuredPlayerCount =
            playerCount <= 2
                ? 2
                : 4;

        ApplyModeProfileSelection();
    }

    #endregion

    #region Visibility

    [Header("Visibility")]
    [SerializeField] private bool drawingEnabled = true;

    #endregion

    #region Runtime Cache

    private int cachedDisplaySeconds = int.MinValue;
    private string cachedTimerText = "00:00";

    private const int MaxSupportedPlayers = 4;
    private const float AnimationTargetEpsilon = 0.0001f;

    private sealed class AnimatedEndpointState
    {
        public bool Initialized;
        public float DisplayedValue;
        public float StartValue;
        public float TargetValue;
        public float ElapsedSeconds;

        public void Reset()
        {
            Initialized = false;
            DisplayedValue = 0f;
            StartValue = 0f;
            TargetValue = 0f;
            ElapsedSeconds = 0f;
        }
    }

    private sealed class LeaderboardBarPresentationState
    {
        public readonly AnimatedEndpointState Banked =
            new AnimatedEndpointState();

        public readonly AnimatedEndpointState Combined =
            new AnimatedEndpointState();

        public readonly AnimatedEndpointState StreakRewardStart =
            new AnimatedEndpointState();

        public readonly AnimatedEndpointState DisplayedRow =
            new AnimatedEndpointState();

        public void Reset()
        {
            Banked.Reset();
            Combined.Reset();
            StreakRewardStart.Reset();
            DisplayedRow.Reset();
        }
    }

    private readonly LeaderboardBarPresentationState[]
        leaderboardBarPresentationStates =
            new LeaderboardBarPresentationState[
                MaxSupportedPlayers + 1
            ];

    private int lastLeaderboardPresentationUpdateFrame = -1;

    private static readonly string[] PlayerLabels =
    {
        "",
        "P1",
        "P2",
        "P3",
        "P4"
    };

    private static readonly string[] RankLabels =
    {
        "",
        "1st",
        "2nd",
        "3rd",
        "4th"
    };

    private const string LocalPlayerLabel = "YOU";

    #endregion

    #region Unity

    private void Awake()
    {
        ResolveReferences();
        ApplyModeProfileSelection();
    }

    public override void DrawShapes(Camera cam)
    {
        if (!drawingEnabled) return;

        if (cam == null ||
            hudSystem == null ||
            stateSystem == null ||
            profile == null)
        {
            return;
        }

        // Reassert the renderer's selected layout before reading any scaled
        // profile values. This remains safe if the same profile asset is ever
        // referenced by another runtime overlay renderer.
        ApplyModeProfileSelection();

        // This gives us both:
        // 1. a hard filter to active gameplay cameras;
        // 2. the local player index for the YOU label.
        if (!hudSystem.TryGetRenderableSlotForCameraForOverlay(
                cam,
                out int localPlayerIndex,
                out _))
        {
            return;
        }

        float renderDepth =
            Mathf.Max(
                profile.NearCameraRenderDistance,
                cam.nearClipPlane +
                profile.NearClipSafetyPadding
            );

        Vector2 rootScreen =
            GetViewportAnchorScreenPosition(
                cam
            );

        UpdateCachedTimerText();
        UpdateLeaderboardPresentation();

        using (Draw.Command(cam))
        {
            Draw.ResetAllDrawStates();
            Draw.BlendMode =
                ShapesBlendMode.Transparent;

            Draw.RadiusSpace =
                ThicknessSpace.Pixels;

            Draw.ThicknessSpace =
                ThicknessSpace.Pixels;

            Draw.LineGeometry =
                LineGeometry.Billboard;

            Draw.LineEndCaps =
                LineEndCap.Round;

            DrawSplitScreenDividers(
                cam,
                renderDepth
            );

            DrawTimer(
                cam,
                renderDepth,
                rootScreen
            );

            DrawLeaderboard(
                cam,
                renderDepth,
                rootScreen,
                localPlayerIndex
            );
        }
    }

    #endregion

    #region Split-Screen Dividers

    /// <summary>
    /// Every gameplay camera draws the portion of the same screen-space
    /// divider rectangles that falls inside its pixelRect. Camera viewport
    /// clipping joins those pieces into one continuous full-screen cross.
    /// </summary>
    private void DrawSplitScreenDividers(
        Camera cam,
        float renderDepth)
    {
        if (!profile.SplitScreenDividersEnabled)
        {
            return;
        }

        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        if (screenWidth <= 0f || screenHeight <= 0f)
        {
            return;
        }

        float centerX = screenWidth * 0.5f;
        float centerY = screenHeight * 0.5f;
        float verticalWidth = profile.VerticalDividerWidthPixels;

        if (verticalWidth > 0f)
        {
            DrawRoundedScreenRectangle(
                cam,
                renderDepth,
                centerX - verticalWidth * 0.5f,
                centerX + verticalWidth * 0.5f,
                centerY,
                screenHeight,
                0f,
                profile.VerticalDividerColor
            );
        }

        if (configuredPlayerCount >= 4)
        {
            float horizontalHeight =
                profile.HorizontalDividerHeightPixels;

            if (horizontalHeight > 0f)
            {
                DrawRoundedScreenRectangle(
                    cam,
                    renderDepth,
                    0f,
                    screenWidth,
                    centerY,
                    horizontalHeight,
                    0f,
                    profile.HorizontalDividerColor
                );
            }
        }
    }

    #endregion

    #region Timer

    private void UpdateCachedTimerText()
    {
        MatchViewportOverlayStateSystem.TimerState timer =
            stateSystem.Timer;

        if (timer.DisplaySeconds ==
            cachedDisplaySeconds)
        {
            return;
        }

        cachedDisplaySeconds =
            timer.DisplaySeconds;

        cachedTimerText =
            $"{timer.Minutes:D2}:{timer.Seconds:D2}";
    }

    private void DrawTimer(
        Camera cam,
        float renderDepth,
        Vector2 rootScreen)
    {
        Vector2 timerCenterScreen =
            rootScreen +
            profile.TimerOffsetPixels;

        DrawTimerRoundedBackground(
            cam,
            renderDepth,
            timerCenterScreen
        );

        Vector2 textScreen =
            timerCenterScreen +
            profile.TimerTextOffsetPixels;

        DrawCenteredText(
            cam,
            renderDepth,
            textScreen,
            cachedTimerText,
            profile.TimerFontSizePixels,
            profile.TimerTextColor
        );
    }

    private void DrawTimerRoundedBackground(
        Camera cam,
        float renderDepth,
        Vector2 centerScreen)
    {
        float width =
            profile.TimerBackgroundWidthPixels;

        float height =
            profile.TimerBackgroundHeightPixels;

        float halfInnerLength =
            Mathf.Max(
                0f,
                width - height
            ) * 0.5f;

        Vector2 left =
            centerScreen +
            profile.TimerBackgroundOffsetPixels +
            Vector2.left *
            halfInnerLength;

        Vector2 right =
            centerScreen +
            profile.TimerBackgroundOffsetPixels +
            Vector2.right *
            halfInnerLength;

        Draw.Line(
            ScreenPointToWorld(
                cam,
                left,
                renderDepth
            ),
            ScreenPointToWorld(
                cam,
                right,
                renderDepth
            ),
            height,
            profile.TimerBackgroundColor
        );
    }

    #endregion

    #region Leaderboard

    private void UpdateLeaderboardPresentation()
    {
        // DrawShapes runs once for every active gameplay camera. Advance the
        // shared presentation state only once so split-screen player count
        // cannot change the animation speed.
        if (lastLeaderboardPresentationUpdateFrame ==
            Time.frameCount)
        {
            return;
        }

        lastLeaderboardPresentationUpdateFrame =
            Time.frameCount;

        float unscaledDeltaTime =
            Mathf.Max(
                0f,
                Time.unscaledDeltaTime
            );

        for (int playerIndex = 1;
             playerIndex <= MaxSupportedPlayers;
             playerIndex++)
        {
            LeaderboardBarPresentationState presentationState =
                GetOrCreateBarPresentationState(
                    playerIndex
                );

            if (!stateSystem
                    .TryGetLeaderboardPlayer(
                        playerIndex,
                        out MatchViewportOverlayStateSystem
                            .LeaderboardPlayerState
                            playerState) ||
                !playerState.Active)
            {
                presentationState.Reset();
                continue;
            }

            UpdateAnimatedEndpoint(
                presentationState.Banked,
                playerState.BankedNormalized,
                unscaledDeltaTime
            );

            UpdateAnimatedEndpoint(
                presentationState.Combined,
                playerState.CombinedNormalized,
                unscaledDeltaTime
            );

            UpdateAnimatedEndpoint(
                presentationState.StreakRewardStart,
                playerState.StreakRewardStartNormalized,
                unscaledDeltaTime
            );
        }

        int rowCount =
            stateSystem
                .LeaderboardActivePlayerCount;

        for (int targetRow = 0;
             targetRow < rowCount;
             targetRow++)
        {
            int playerIndex =
                stateSystem
                    .GetLeaderboardDisplayPlayerIndex(
                        targetRow
                    );

            if (playerIndex < 1 ||
                playerIndex > MaxSupportedPlayers)
            {
                continue;
            }

            LeaderboardBarPresentationState presentationState =
                GetOrCreateBarPresentationState(
                    playerIndex
                );

            UpdateAnimatedRowPosition(
                presentationState.DisplayedRow,
                targetRow,
                unscaledDeltaTime
            );
        }
    }

    private LeaderboardBarPresentationState
        GetOrCreateBarPresentationState(
            int playerIndex)
    {
        LeaderboardBarPresentationState presentationState =
            leaderboardBarPresentationStates[playerIndex];

        if (presentationState != null)
        {
            return presentationState;
        }

        presentationState =
            new LeaderboardBarPresentationState();

        leaderboardBarPresentationStates[playerIndex] =
            presentationState;

        return presentationState;
    }

    private void UpdateAnimatedEndpoint(
        AnimatedEndpointState endpointState,
        float exactTargetValue,
        float unscaledDeltaTime)
    {
        float safeTargetValue =
            Mathf.Clamp01(
                exactTargetValue
            );

        float durationSeconds =
            profile
                .LeaderboardBarAnimationDurationSeconds;

        if (!endpointState.Initialized ||
            !profile.LeaderboardBarAnimationEnabled ||
            durationSeconds <= AnimationTargetEpsilon)
        {
            SnapAnimatedEndpoint(
                endpointState,
                safeTargetValue
            );

            return;
        }

        if (Mathf.Abs(
                safeTargetValue -
                endpointState.TargetValue
            ) > AnimationTargetEpsilon)
        {
            // Retarget from the currently displayed position. Repeated score
            // changes therefore remain continuous instead of snapping back to
            // an earlier start point.
            endpointState.StartValue =
                endpointState.DisplayedValue;

            endpointState.TargetValue =
                safeTargetValue;

            endpointState.ElapsedSeconds =
                0f;
        }

        endpointState.ElapsedSeconds +=
            unscaledDeltaTime;

        float normalizedTime =
            Mathf.Clamp01(
                endpointState.ElapsedSeconds /
                durationSeconds
            );

        float easedTime =
            profile
                .EvaluateLeaderboardBarAnimationEasing(
                    normalizedTime
                );

        endpointState.DisplayedValue =
            Mathf.LerpUnclamped(
                endpointState.StartValue,
                endpointState.TargetValue,
                easedTime
            );

        if (normalizedTime >= 1f)
        {
            endpointState.DisplayedValue =
                endpointState.TargetValue;
        }
    }

    private static void SnapAnimatedEndpoint(
        AnimatedEndpointState endpointState,
        float targetValue)
    {
        endpointState.Initialized = true;
        endpointState.DisplayedValue = targetValue;
        endpointState.StartValue = targetValue;
        endpointState.TargetValue = targetValue;
        endpointState.ElapsedSeconds = 0f;
    }

    private void UpdateAnimatedRowPosition(
        AnimatedEndpointState rowState,
        int exactTargetRow,
        float unscaledDeltaTime)
    {
        float safeTargetRow =
            Mathf.Clamp(
                exactTargetRow,
                0,
                MaxSupportedPlayers - 1
            );

        float durationSeconds =
            profile
                .LeaderboardEntryReorderAnimationDurationSeconds;

        if (!rowState.Initialized ||
            !profile.LeaderboardEntryReorderAnimationEnabled ||
            durationSeconds <= AnimationTargetEpsilon)
        {
            SnapAnimatedEndpoint(
                rowState,
                safeTargetRow
            );

            return;
        }

        if (Mathf.Abs(
                safeTargetRow -
                rowState.TargetValue
            ) > AnimationTargetEpsilon)
        {
            // Start every reorder from the entry's current visual position.
            // A second rank change during movement therefore retargets without
            // a snap or discontinuity.
            rowState.StartValue =
                rowState.DisplayedValue;

            rowState.TargetValue =
                safeTargetRow;

            rowState.ElapsedSeconds =
                0f;
        }

        rowState.ElapsedSeconds +=
            unscaledDeltaTime;

        float normalizedTime =
            Mathf.Clamp01(
                rowState.ElapsedSeconds /
                durationSeconds
            );

        float easedTime =
            profile
                .EvaluateLeaderboardEntryReorderAnimationEasing(
                    normalizedTime
                );

        rowState.DisplayedValue =
            Mathf.LerpUnclamped(
                rowState.StartValue,
                rowState.TargetValue,
                easedTime
            );

        if (normalizedTime >= 1f)
        {
            rowState.DisplayedValue =
                rowState.TargetValue;
        }
    }

    private float GetDisplayedLeaderboardRow(
        int playerIndex,
        int exactTargetRow)
    {
        if (playerIndex < 1 ||
            playerIndex > MaxSupportedPlayers)
        {
            return exactTargetRow;
        }

        LeaderboardBarPresentationState presentationState =
            GetOrCreateBarPresentationState(
                playerIndex
            );

        return
            presentationState.DisplayedRow.Initialized
                ? Mathf.Clamp(
                    presentationState
                        .DisplayedRow
                        .DisplayedValue,
                    0f,
                    MaxSupportedPlayers - 1f
                )
                : exactTargetRow;
    }

    private void GetDisplayedBarValues(
        MatchViewportOverlayStateSystem
            .LeaderboardPlayerState playerState,
        out float displayedBankedNormalized,
        out float displayedCombinedNormalized,
        out float displayedStreakRewardStartNormalized)
    {
        int playerIndex =
            playerState.PlayerIndex;

        if (playerIndex < 1 ||
            playerIndex > MaxSupportedPlayers)
        {
            displayedBankedNormalized =
                Mathf.Clamp01(
                    playerState.BankedNormalized
                );

            displayedCombinedNormalized =
                Mathf.Max(
                    displayedBankedNormalized,
                    Mathf.Clamp01(
                        playerState.CombinedNormalized
                    )
                );

            displayedStreakRewardStartNormalized =
                Mathf.Clamp(
                    playerState.StreakRewardStartNormalized,
                    displayedBankedNormalized,
                    displayedCombinedNormalized
                );

            return;
        }

        LeaderboardBarPresentationState presentationState =
            GetOrCreateBarPresentationState(
                playerIndex
            );

        displayedBankedNormalized =
            presentationState.Banked.Initialized
                ? Mathf.Clamp01(
                    presentationState
                        .Banked
                        .DisplayedValue
                )
                : Mathf.Clamp01(
                    playerState.BankedNormalized
                );

        float rawDisplayedCombined =
            presentationState.Combined.Initialized
                ? Mathf.Clamp01(
                    presentationState
                        .Combined
                        .DisplayedValue
                )
                : Mathf.Clamp01(
                    playerState.CombinedNormalized
                );

        // The combined endpoint must never render left of the banked endpoint,
        // even while both values are independently retargeting.
        displayedCombinedNormalized =
            Mathf.Max(
                displayedBankedNormalized,
                rawDisplayedCombined
            );

        float rawDisplayedStreakRewardStart =
            presentationState.StreakRewardStart.Initialized
                ? Mathf.Clamp01(
                    presentationState
                        .StreakRewardStart
                        .DisplayedValue
                )
                : Mathf.Clamp01(
                    playerState.StreakRewardStartNormalized
                );

        displayedStreakRewardStartNormalized =
            Mathf.Clamp(
                rawDisplayedStreakRewardStart,
                displayedBankedNormalized,
                displayedCombinedNormalized
            );
    }

    private void DrawLeaderboard(
        Camera cam,
        float renderDepth,
        Vector2 rootScreen,
        int localPlayerIndex)
    {
        int rowCount =
            stateSystem
                .LeaderboardActivePlayerCount;

        if (rowCount <= 0) return;

        Vector2 timerCenter =
            rootScreen +
            profile.TimerOffsetPixels;

        Vector2 timerBackgroundCenter =
            timerCenter +
            profile.TimerBackgroundOffsetPixels;

        float timerBottomY =
            timerBackgroundCenter.y -
            profile.TimerBackgroundHeightPixels *
            0.5f;

        // The first row is automatically placed below the timer.
        // LeaderboardOffsetPixels then moves the complete leaderboard as one unit.
        Vector2 firstRowCenter =
            new Vector2(
                timerCenter.x,
                timerBottomY -
                profile.TimerToLeaderboardPaddingPixels -
                profile.LeaderboardRowHeightPixels *
                0.5f
            ) +
            profile.LeaderboardOffsetPixels;

        float rowStep =
            profile.LeaderboardRowHeightPixels +
            profile.LeaderboardRowSpacingPixels;

        // Draw stationary and downward-moving entries first.
        for (int targetRow = 0;
             targetRow < rowCount;
             targetRow++)
        {
            DrawLeaderboardEntryForTargetRow(
                cam,
                renderDepth,
                firstRowCenter,
                rowStep,
                localPlayerIndex,
                targetRow,
                false
            );
        }

        // Draw upward-moving entries last so rank improvements remain on top
        // while rows overlap. Iterate bottom-to-top so the entry targeting the
        // highest row is the final draw within this priority group.
        for (int targetRow = rowCount - 1;
             targetRow >= 0;
             targetRow--)
        {
            DrawLeaderboardEntryForTargetRow(
                cam,
                renderDepth,
                firstRowCenter,
                rowStep,
                localPlayerIndex,
                targetRow,
                true
            );
        }
    }

    private void DrawLeaderboardEntryForTargetRow(
        Camera cam,
        float renderDepth,
        Vector2 firstRowCenter,
        float rowStep,
        int localPlayerIndex,
        int targetRow,
        bool drawUpwardMovingEntries)
    {
        int playerIndex =
            stateSystem
                .GetLeaderboardDisplayPlayerIndex(
                    targetRow
                );

        if (!stateSystem
                .TryGetLeaderboardPlayer(
                    playerIndex,
                    out MatchViewportOverlayStateSystem
                        .LeaderboardPlayerState
                        playerState) ||
            !playerState.Active)
        {
            return;
        }

        float displayedRow =
            GetDisplayedLeaderboardRow(
                playerIndex,
                targetRow
            );

        // Row zero is the top. A displayed row greater than its new target is
        // therefore traveling upward toward a better leaderboard position.
        bool isMovingUp =
            displayedRow >
            targetRow +
            AnimationTargetEpsilon;

        if (isMovingUp !=
            drawUpwardMovingEntries)
        {
            return;
        }

        Vector2 rowCenter =
            firstRowCenter +
            Vector2.down *
            (
                rowStep *
                displayedRow
            );

        DrawLeaderboardRow(
            cam,
            renderDepth,
            rowCenter,
            localPlayerIndex,
            playerState
        );
    }

    private void DrawLeaderboardRow(
        Camera cam,
        float renderDepth,
        Vector2 rowCenter,
        int localPlayerIndex,
        MatchViewportOverlayStateSystem
            .LeaderboardPlayerState playerState)
    {
        bool isLocalPlayer =
            playerState.PlayerIndex ==
            localPlayerIndex;

        Vector2 barCenter =
            rowCenter +
            profile.LeaderboardBarOffsetPixels;

        DrawLeaderboardBar(
            cam,
            renderDepth,
            barCenter,
            playerState,
            isLocalPlayer
        );

        string playerLabel =
            isLocalPlayer
                ? LocalPlayerLabel
                : GetPlayerLabel(
                    playerState.PlayerIndex
                );

        Vector2 playerLabelBackgroundSize =
            profile
                .LeaderboardPlayerLabelBackgroundSizePixels;

        Vector2 playerLabelGroupCenter =
            barCenter +
            Vector2.left *
            (
                profile.LeaderboardBarWidthPixels *
                0.5f +
                playerLabelBackgroundSize.x *
                0.5f +
                profile.LeaderboardPlayerLabelGapPixels
            ) +
            profile
                .LeaderboardPlayerLabelGroupOffsetPixels;

        DrawRoundedTextGroup(
            cam,
            renderDepth,
            playerLabelGroupCenter,
            playerLabelBackgroundSize,
            profile
                .LeaderboardPlayerLabelBackgroundCornerRadiusPixels,
            profile
                .GetLeaderboardPlayerLabelBackgroundColor(
                    playerState.PlayerIndex,
                    isLocalPlayer
                ),
            playerLabel,
            profile
                .LeaderboardPlayerLabelFontSizePixels,
            profile.GetLeaderboardTextColor(
                isLocalPlayer
            )
        );

        Vector2 rankBackgroundSize =
            profile
                .LeaderboardRankBackgroundSizePixels;

        Vector2 rankGroupCenter =
            barCenter +
            Vector2.right *
            (
                profile.LeaderboardBarWidthPixels *
                0.5f +
                rankBackgroundSize.x *
                0.5f +
                profile.LeaderboardRankTextGapPixels
            ) +
            profile
                .LeaderboardRankGroupOffsetPixels;

        DrawRoundedTextGroup(
            cam,
            renderDepth,
            rankGroupCenter,
            rankBackgroundSize,
            profile
                .LeaderboardRankBackgroundCornerRadiusPixels,
            profile.GetLeaderboardRankBackgroundColor(
                isLocalPlayer
            ),
            GetRankLabel(playerState.Rank),
            profile.LeaderboardRankFontSizePixels,
            profile.GetLeaderboardTextColor(
                isLocalPlayer
            )
        );
    }

    private void DrawLeaderboardBar(
        Camera cam,
        float renderDepth,
        Vector2 center,
        MatchViewportOverlayStateSystem
            .LeaderboardPlayerState playerState,
        bool isLocalPlayer)
    {
        GetDisplayedBarValues(
            playerState,
            out float displayedBankedNormalized,
            out float displayedCombinedNormalized,
            out float displayedStreakRewardStartNormalized
        );

        float width =
            profile.LeaderboardBarWidthPixels;

        float backgroundHeight =
            profile
                .LeaderboardBarBackgroundHeightPixels;

        float fillHeight =
            profile
                .LeaderboardBarFillHeightPixels;

        float cornerRadius =
            profile
                .LeaderboardBarCornerRadiusPixels;

        float horizontalPadding =
            profile
                .LeaderboardBarHorizontalPaddingPixels;

        float fillOutlineThickness =
            profile
                .LeaderboardBarFillOutlineThicknessPixels;

        float fullLeftX =
            center.x -
            width * 0.5f;

        float fullRightX =
            center.x +
            width * 0.5f;

        float fillLeftX =
            fullLeftX +
            horizontalPadding;

        float fillRightX =
            fullRightX -
            horizontalPadding;

        // 1. Background: mostly rectangular, with only a small tunable corner radius.
        DrawRoundedScreenRectangle(
            cam,
            renderDepth,
            fullLeftX,
            fullRightX,
            center.y,
            backgroundHeight,
            cornerRadius,
            profile.GetLeaderboardBarBackgroundColor(
                isLocalPlayer
            )
        );

        float bankedRightX =
            Mathf.Lerp(
                fillLeftX,
                fillRightX,
                displayedBankedNormalized
            );

        float combinedRightX =
            Mathf.Lerp(
                fillLeftX,
                fillRightX,
                displayedCombinedNormalized
            );

        float foregroundFillInset =
            Mathf.Clamp(
                fillOutlineThickness,
                0f,
                fillHeight * 0.5f
            );

        float foregroundFillLeftX =
            fillLeftX +
            foregroundFillInset;

        float foregroundBankedRightX =
            bankedRightX -
            foregroundFillInset;

        float foregroundCombinedRightX =
            combinedRightX -
            foregroundFillInset;

        float foregroundFillHeight =
            Mathf.Max(
                0.001f,
                fillHeight -
                foregroundFillInset *
                2f
            );

        float foregroundCornerRadius =
            Mathf.Max(
                0f,
                cornerRadius -
                foregroundFillInset
            );

        // 2. Larger potential/combined rear layer. It uses the same animated
        // endpoint as the foreground, so both pieces grow and shrink together.
        if (foregroundFillInset > 0.001f &&
            combinedRightX >
            fillLeftX + 0.001f)
        {
            DrawRoundedScreenRectangle(
                cam,
                renderDepth,
                fillLeftX,
                combinedRightX,
                center.y,
                fillHeight,
                cornerRadius,
                profile.LeaderboardBarFillOutlineColor
            );
        }

        // 3. Smaller potential/combined foreground layer. The exposed rear
        // layer around it becomes the visual outline.
        if (foregroundCombinedRightX >
            foregroundFillLeftX + 0.001f)
        {
            DrawRoundedScreenRectangle(
                cam,
                renderDepth,
                foregroundFillLeftX,
                foregroundCombinedRightX,
                center.y,
                foregroundFillHeight,
                foregroundCornerRadius,
                profile
                    .GetLeaderboardPlayerPotentialBaseColor(
                        playerState.PlayerIndex
                    )
            );
        }

        // 4. The banked section receives its own synchronized rear layer. Its
        // right edge naturally separates banked score from potential score.
        if (foregroundFillInset > 0.001f &&
            bankedRightX >
            fillLeftX + 0.001f)
        {
            DrawRoundedScreenRectangle(
                cam,
                renderDepth,
                fillLeftX,
                bankedRightX,
                center.y,
                fillHeight,
                cornerRadius,
                profile.LeaderboardBarFillOutlineColor
            );
        }

        // 5. Smaller banked foreground layer.
        if (foregroundBankedRightX >
            foregroundFillLeftX + 0.001f)
        {
            DrawRoundedScreenRectangle(
                cam,
                renderDepth,
                foregroundFillLeftX,
                foregroundBankedRightX,
                center.y,
                foregroundFillHeight,
                foregroundCornerRadius,
                profile.GetLeaderboardPlayerFillColor(
                    playerState.PlayerIndex
                )
            );
        }

        // 6. Repeating potential-score chevrons stay inside the smaller
        // foreground layer. Pattern phase remains anchored to the bar's left
        // side rather than squeezing or reflowing with score changes.
        // Banked growth therefore reveals/hides the same fixed pattern instead
        // of squeezing or reflowing it.
        float patternVisibleLeftX =
            Mathf.Max(
                foregroundFillLeftX,
                bankedRightX +
                foregroundFillInset
            );

        float displayedPotentialNormalized =
            Mathf.Max(
                0f,
                displayedCombinedNormalized -
                displayedBankedNormalized
            );

        float displayedStreakRewardNormalized =
            Mathf.Max(
                0f,
                displayedCombinedNormalized -
                displayedStreakRewardStartNormalized
            );

        float displayedStreakRewardFraction =
            displayedPotentialNormalized > AnimationTargetEpsilon
                ? Mathf.Clamp01(
                    displayedStreakRewardNormalized /
                    displayedPotentialNormalized
                )
                : 0f;

        // Apply the score fraction within the actually visible foreground
        // interval. This compensates for outline insets at both ends, so a
        // 10-point reward inside 30 potential points visibly occupies exactly
        // the final third of the chevrons.
        float streakRewardStartX =
            Mathf.Lerp(
                foregroundCombinedRightX,
                patternVisibleLeftX,
                displayedStreakRewardFraction
            );

        DrawPotentialChevronPattern(
            cam,
            renderDepth,
            foregroundFillLeftX,
            patternVisibleLeftX,
            foregroundCombinedRightX,
            streakRewardStartX,
            center.y,
            foregroundFillHeight,
            playerState.PlayerIndex
        );
    }

    private void DrawPotentialChevronPattern(
        Camera cam,
        float renderDepth,
        float patternOriginX,
        float visibleLeftX,
        float visibleRightX,
        float streakRewardStartX,
        float centerY,
        float fillHeightPixels,
        int playerIndex)
    {
        float lineThickness =
            profile
                .LeaderboardPotentialPatternLineThicknessPixels;

        float chevronWidth =
            profile
                .LeaderboardPotentialPatternWidthPixels;

        float chevronSpacing =
            profile
                .LeaderboardPotentialPatternSpacingPixels;

        float verticalPadding =
            profile
                .LeaderboardPotentialPatternVerticalPaddingPixels;

        // Round line caps extend by roughly half the stroke thickness. Keep
        // their endpoints inside the visible potential interval so even a
        // partially revealed chevron cannot bleed into banked/background space.
        float capSafety =
            lineThickness * 0.5f;

        float clippedLeftX =
            visibleLeftX +
            capSafety;

        float clippedRightX =
            visibleRightX -
            capSafety;

        float halfPatternHeight =
            fillHeightPixels * 0.5f -
            verticalPadding -
            capSafety;

        if (clippedRightX <= clippedLeftX + 0.001f ||
            halfPatternHeight <= 0.001f ||
            chevronWidth <= 0.001f ||
            chevronSpacing <= 0.001f)
        {
            return;
        }

        int firstChevronIndex =
            Mathf.Max(
                0,
                Mathf.FloorToInt(
                    (
                        clippedLeftX -
                        patternOriginX -
                        chevronWidth
                    ) /
                    chevronSpacing
                )
            );

        int lastChevronIndex =
            Mathf.CeilToInt(
                (
                    clippedRightX -
                    patternOriginX
                ) /
                chevronSpacing
            );

        for (int chevronIndex = firstChevronIndex;
             chevronIndex <= lastChevronIndex;
             chevronIndex++)
        {
            float originX =
                patternOriginX +
                chevronIndex *
                chevronSpacing;

            Vector2 top =
                new Vector2(
                    originX,
                    centerY +
                    halfPatternHeight
                );

            Vector2 middle =
                new Vector2(
                    originX +
                    chevronWidth,
                    centerY
                );

            Vector2 bottom =
                new Vector2(
                    originX,
                    centerY -
                    halfPatternHeight
                );

            Color chevronColor =
                profile
                    .GetLeaderboardPlayerPotentialPatternColor(
                        playerIndex,
                        (chevronIndex & 1) == 1
                    );

            Color streakRewardColor =
                profile
                    .GetLeaderboardStreakRewardPatternColor(
                        (chevronIndex & 1) == 1
                    );

            DrawPotentialChevronLineWithHighlight(
                cam,
                renderDepth,
                top,
                middle,
                clippedLeftX,
                clippedRightX,
                streakRewardStartX,
                lineThickness,
                chevronColor,
                streakRewardColor
            );

            DrawPotentialChevronLineWithHighlight(
                cam,
                renderDepth,
                middle,
                bottom,
                clippedLeftX,
                clippedRightX,
                streakRewardStartX,
                lineThickness,
                chevronColor,
                streakRewardColor
            );
        }
    }

    private void DrawPotentialChevronLineWithHighlight(
        Camera cam,
        float renderDepth,
        Vector2 start,
        Vector2 end,
        float clipLeftX,
        float clipRightX,
        float streakRewardStartX,
        float thicknessPixels,
        Color potentialColor,
        Color streakRewardColor)
    {
        float baseRightX =
            Mathf.Clamp(
                streakRewardStartX,
                clipLeftX,
                clipRightX
            );

        if (baseRightX > clipLeftX + 0.001f)
        {
            DrawHorizontallyClippedScreenLine(
                cam,
                renderDepth,
                start,
                end,
                clipLeftX,
                baseRightX,
                thicknessPixels,
                potentialColor
            );
        }

        float rewardLeftX =
            Mathf.Clamp(
                streakRewardStartX,
                clipLeftX,
                clipRightX
            );

        if (clipRightX > rewardLeftX + 0.001f)
        {
            // Draw the reward interval second so its global highlight remains
            // visually dominant where round line caps meet at the boundary.
            DrawHorizontallyClippedScreenLine(
                cam,
                renderDepth,
                start,
                end,
                rewardLeftX,
                clipRightX,
                thicknessPixels,
                streakRewardColor
            );
        }
    }

    private void DrawHorizontallyClippedScreenLine(
        Camera cam,
        float renderDepth,
        Vector2 start,
        Vector2 end,
        float clipLeftX,
        float clipRightX,
        float thicknessPixels,
        Color color)
    {
        if (!TryClipLineToHorizontalRange(
                start,
                end,
                clipLeftX,
                clipRightX,
                out Vector2 clippedStart,
                out Vector2 clippedEnd))
        {
            return;
        }

        Draw.Line(
            ScreenPointToWorld(
                cam,
                clippedStart,
                renderDepth
            ),
            ScreenPointToWorld(
                cam,
                clippedEnd,
                renderDepth
            ),
            thicknessPixels,
            color
        );
    }

    private static bool TryClipLineToHorizontalRange(
        Vector2 start,
        Vector2 end,
        float clipLeftX,
        float clipRightX,
        out Vector2 clippedStart,
        out Vector2 clippedEnd)
    {
        clippedStart = start;
        clippedEnd = end;

        float deltaX =
            end.x -
            start.x;

        if (Mathf.Abs(deltaX) <= 0.0001f)
        {
            return
                start.x >= clipLeftX &&
                start.x <= clipRightX;
        }

        float tAtLeft =
            (clipLeftX - start.x) /
            deltaX;

        float tAtRight =
            (clipRightX - start.x) /
            deltaX;

        float enterT =
            Mathf.Max(
                0f,
                Mathf.Min(
                    tAtLeft,
                    tAtRight
                )
            );

        float exitT =
            Mathf.Min(
                1f,
                Mathf.Max(
                    tAtLeft,
                    tAtRight
                )
            );

        if (exitT <= enterT + 0.0001f)
        {
            return false;
        }

        clippedStart =
            Vector2.LerpUnclamped(
                start,
                end,
                enterT
            );

        clippedEnd =
            Vector2.LerpUnclamped(
                start,
                end,
                exitT
            );

        return true;
    }

    /// <summary>
    /// Draws a filled Shapes rectangle in screen-pixel authored dimensions.
    ///
    /// Rectangle width/height/corner radius are converted from pixels to
    /// world units at the fixed near-camera render depth. The rectangle is
    /// rotated into the camera plane so it remains a viewport HUD element.
    /// </summary>
    private void DrawRoundedScreenRectangle(
        Camera cam,
        float renderDepth,
        float visibleLeftX,
        float visibleRightX,
        float centerY,
        float heightPixels,
        float cornerRadiusPixels,
        Color color)
    {
        float widthPixels =
            visibleRightX -
            visibleLeftX;

        if (widthPixels <= 0.001f ||
            heightPixels <= 0.001f)
        {
            return;
        }

        float safeHeightPixels =
            Mathf.Max(
                0.001f,
                heightPixels
            );

        float safeCornerPixels =
            Mathf.Clamp(
                cornerRadiusPixels,
                0f,
                Mathf.Min(
                    widthPixels,
                    safeHeightPixels
                ) * 0.5f
            );

        Vector2 centerScreen =
            new Vector2(
                (visibleLeftX + visibleRightX) *
                0.5f,
                centerY
            );

        Vector3 centerWorld =
            ScreenPointToWorld(
                cam,
                centerScreen,
                renderDepth
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
                safeHeightPixels
            );

        float cornerRadiusWorld =
            PixelsToWorldSizeAtDepth(
                cam,
                centerWorld,
                safeCornerPixels
            );

        Color previousColor =
            Draw.Color;

        // Shapes rectangles live in their local XY plane. Put that plane on the
        // current camera's near plane, draw, then restore the default matrix.
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

        Draw.Color =
            previousColor;

        Draw.Matrix =
            Matrix4x4.identity;
    }

    private static string GetPlayerLabel(
        int playerIndex)
    {
        if (playerIndex >= 1 &&
            playerIndex < PlayerLabels.Length)
        {
            return PlayerLabels[playerIndex];
        }

        return "P?";
    }

    private static string GetRankLabel(
        int rank)
    {
        if (rank >= 1 &&
            rank < RankLabels.Length)
        {
            return RankLabels[rank];
        }

        return "--";
    }

    #endregion

    #region Viewport Anchor

    private Vector2 GetViewportAnchorScreenPosition(
        Camera cam)
    {
        Rect pixelRect =
            cam.pixelRect;

        Vector2 normalized =
            profile.ViewportAnchorNormalized;

        Vector2 anchor =
            new Vector2(
                pixelRect.xMin +
                pixelRect.width *
                normalized.x,
                pixelRect.yMin +
                pixelRect.height *
                normalized.y
            );

        return
            anchor +
            profile.RootOffsetPixels;
    }

    #endregion

    #region Helpers

    private void ResolveReferences()
    {
        if (hudSystem == null)
        {
            hudSystem =
                FindFirstObjectByType<
                    PlayerWorldHUDSystem
                >();
        }

        if (stateSystem == null)
        {
            stateSystem =
                MatchViewportOverlayStateSystem
                    .Instance;
        }

        if (stateSystem == null)
        {
            stateSystem =
                FindFirstObjectByType<
                    MatchViewportOverlayStateSystem
                >();
        }
    }

    private void ApplyModeProfileSelection()
    {
        if (profile == null) return;

        profile.SetRuntimePlayerCount(
            configuredPlayerCount
        );
    }

    private void DrawCenteredText(
        Camera cam,
        float renderDepth,
        Vector2 screenPosition,
        string text,
        float fontSizePixels,
        Color color)
    {
        Vector3 textWorld =
            ScreenPointToWorld(
                cam,
                screenPosition,
                renderDepth
            );

        Draw.FontSize =
            PixelsToWorldSizeAtDepth(
                cam,
                textWorld,
                fontSizePixels
            );

        Color previousColor =
            Draw.Color;

        Draw.Color = color;

        Draw.Text(
            textWorld,
            cam.transform.rotation,
            text,
            TextAlign.Center
        );

        Draw.Color =
            previousColor;
    }

    private void DrawRoundedTextGroup(
        Camera cam,
        float renderDepth,
        Vector2 groupCenterScreen,
        Vector2 backgroundSizePixels,
        float backgroundCornerRadiusPixels,
        Color backgroundColor,
        string text,
        float fontSizePixels,
        Color textColor)
    {
        float halfBackgroundWidth =
            backgroundSizePixels.x *
            0.5f;

        DrawRoundedScreenRectangle(
            cam,
            renderDepth,
            groupCenterScreen.x -
            halfBackgroundWidth,
            groupCenterScreen.x +
            halfBackgroundWidth,
            groupCenterScreen.y,
            backgroundSizePixels.y,
            backgroundCornerRadiusPixels,
            backgroundColor
        );

        DrawCenteredText(
            cam,
            renderDepth,
            groupCenterScreen,
            text,
            fontSizePixels,
            textColor
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

    private float PixelsToWorldSizeAtDepth(
        Camera cam,
        Vector3 worldPosition,
        float pixelSize)
    {
        Vector3 screenA =
            cam.WorldToScreenPoint(
                worldPosition
            );

        Vector3 screenB =
            screenA;

        screenB.y +=
            pixelSize;

        Vector3 worldA =
            cam.ScreenToWorldPoint(
                screenA
            );

        Vector3 worldB =
            cam.ScreenToWorldPoint(
                screenB
            );

        return Vector3.Distance(
            worldA,
            worldB
        );
    }

    #endregion
}
