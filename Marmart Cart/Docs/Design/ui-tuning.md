# UI tuning guide

Where each in-game UI element's size, position and 2P/4P values live. Rules are in `ui-architecture.md`.

## Before you tune

- Set the Game view to **1920x1080**. Every size and offset is in 1920x1080 reference pixels; the game multiplies them by the UI scale (1 at 1080p, 2 at 4K).
- All four gameplay scenes (2P, 4P, Tutorial 2P, Tutorial 4P) use the same assets below, so one edit changes every scene.
- Asset (ScriptableObject) edits made in Play Mode stay after you stop. Scene component edits made in Play Mode are lost.
- Assets are under `Assets/SO_Variables/` unless a path says otherwise.

## Shapes HUD (drawn inside each player camera)

| Element | Asset | Size | Position | 2P / 4P |
|---|---|---|---|---|
| Cart HUD: hype ring, load meter, prompts, warning icons | `Player HUD/PlayerWorldHUDLayoutProfile` | Pair Placement > Master Scale; each group's own Master Scale (prompts, warnings) | Pair Placement > Horizontal Separation, Vertical Offset; each group's Offset | Pair Placement > 2P / 4P |
| Pointers to other players (orbit chevrons) | `Player HUD/OtherPlayerPointerProfile` | Pointer Scale, capsule points and thickness | Orbit Radius, Orbit Center Offset | 2P / 4P |
| Markers over visible players (raindrop) | `Player HUD/OtherPlayerVisibleMarkerProfile` | Marker Master Scale | Marker Tip Offset | Marker Placement > 2P / 4P |
| Power-up aim preview | `Powerup/PowerupAimProfile` | Trajectory, Landing Envelope | follows the aim | none (UI scale only) |
| Session guide: dialogue, compass, raindrop | `Player HUD/PlayerSessionGuideProfile` | General Scale / Placement > Master Scale | General Scale / Placement > Group Offset | General Scale / Placement > Two / Four Player Scale Multiplier |
| Timer, live leaderboard, split dividers | `Player HUD/MatchViewportOverlayProfile` | 2 / 4 Player Overlay Root > Master Scale; Leaderboard - Root > Leaderboard Master Scale | 2 / 4 Player Overlay Root > Root Offset | the two Overlay Root sections |
| Results-screen leaderboard | `Player HUD/MatchViewportOverlayProfile` | same as above | Leaderboard - Root > Two / Four Player Results Leaderboard Offset | those two fields |

## Canvas UI (drawn over the Shapes HUD)

| Element | Where | Notes |
|---|---|---|
| Power-up held icon and button prompts | `Powerup/PowerupHudProfile` | 2 Player Layout and 4 Player Layout hold all sizes and positions |
| Tomato splat | `Powerup/Tomato/TomatoPresentationProfile` | Blind Canvas section |
| Round banner ("ROUND 1", countdown, "SHOP!") | `Assets/Prefabs/UI/Round Banner Label.prefab` | Position: the RectTransform anchors. Size: UniText font size and auto size. Timing: `Meta Game Controller` in each gameplay scene (Round Title Duration, Countdown Step Duration, Go Duration) |
| Round and Match result banner | Texts: `RoundResultTextProfile` (every Round line, the rank lines, the Match Points line). Look: `Assets/Prefabs/UI/Round Result Title.prefab` and `Round Result Detail.prefab` | Shown by `Game UIs/Round Result`. Lines take `<color=#RRGGBB>` and `<size=55%>` tags; a line break starts a smaller second line. How long it shows: `Meta Game Controller` > Round Result Duration |
| Screen fade between Rounds | Timing: `Meta Game Controller` > Fade In / Fade Out Duration. Colour: `Game UIs/Screen Fade` > Fade Color | Gameplay scenes only |
| Tutorial control hints | `Assets/Prefabs/Tutorial/Control Hints.prefab` | Built on each player's HUD layer by `HintsOverlay/manager` |
| Ability pick (before Rounds 2 and 3) | Look: `Assets/Prefabs/UI/Ability Pick/Ability Pick.prefab` (backdrop, header, footer, texts, highlight scale) and `Ability Pick Card.prefab` (card back, face, labels, stamp). Placement: `Player HUD/AbilityPickProfile` > 2 Player Layout / 4 Player Layout | Card text comes from each Sample asset (Display Name, Description, Card Icon, Cooldown Seconds). Timing: `Meta Game Controller/Ability Sample Pick`. Shown by `Game UIs/Ability Pick Presenter`, with its sound hooks on the same object (`AbilityPickSfxRelay`). Flip and pop: the prefab root > Reveal Timings. Pick a Preview State on the prefab root in Prefab Mode |
| Pre-round shade (dark layer under the pick and later pre-round stages) | `Game UIs/Pre-Round Shade` > Shade Color (alpha = strength), Fade Seconds | Bottom of each player's Banner layer: over the world and HUD, under the stage UI. On while any Pre-Round Stage runs |
| Ability card colours and motion per tier | `Player HUD/Ability Tier Styles` | Shared by the Player Action Bar and the Ability pick |

## Shared

- Player main colours (pointers, markers): `Player HUD/PlayerIdentityProfile`.
- Leaderboard colours: `MatchViewportOverlayProfile`, the Leaderboard colour sections. Update them together with the identity colours.
- Canvas text style (bold, outline, shadow tags): `Assets/Fonts/UniText/Marmart Text Styles.asset`, set as the global preset in `UniTextSettings`.
- Results podium world text is 3D, not UI: `MatchResultsPresentationProfile` > World Text.

## Scene objects (for reference)

- `Match Load`: `PlayerViewService` (UI scale, player views).
- `Game UIs/Viewport Canvas System`: the runtime per-player canvas.
- `Game UIs/Player HUDs`: PlayerHUD System, PlayerHUD Renderer, Other Player Pointers, Other Player Markers.
- `Game UIs/Viewport Overlay UIs/Overlay UI Renderer`, `Game UIs/Player Session Guide HUD/Player Session Guide Renderer`, `Game UIs/Round Banner`.
- `Powerup Runtime System`: Aim System, Powerup HUD Presenter, Tomato Blind Canvas Presenter.
