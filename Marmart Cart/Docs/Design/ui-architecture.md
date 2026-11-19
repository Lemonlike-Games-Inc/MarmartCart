# UI architecture

Agreed with Liang on 2026-11-18, after a review of every live UI system. These are the rules for all in-game UI. The code is the source of truth; this doc records the rules, the target and the migration status.

## Why

The review found:
- The Shapes HUD is sized in fixed screen pixels, and builds run at native resolution, so it looks different in the editor and in a build. The cart ring is 36% of the viewport height in a 1348x759 Game view, 25% at 1080p and 12.5% at 4K.
- Helper code copied between drawers.
- "2P or 4P" decided in about eight places.
- Player colours defined in three profiles.
- Runtime values written onto shared profile assets.

The coming meta UI (Round banner, Ability pick, Policy pop, results) needs icons, real text and layout, which Shapes does not do well.

## Two layers

| Layer | Technology | Use it for |
|---|---|---|
| Shapes HUD | Shapes immediate mode, drawn inside each player camera | Procedural, data-driven HUD: meters, rings, bars, pointers, aim, markers, timer, leaderboard |
| Canvas UI | uGUI + UniText | Anything with art or real text: icons, banners, cards, results, menus |

- A widget lives in one layer only.
- The Shapes HUD is **prototype UI**. It stays clean and easy to iterate until the art direction is settled. Then we decide which parts get custom art; those move to the canvas.

## Draw order, bottom to top

1. World: 3D scene and world-space TMP signs.
2. Shapes HUD, inside each player camera.
3. Per-player canvas: one root per player, clipped to that player's viewport. Inside it: effects, then HUD, then banner.
4. Full-screen canvas: fades, transitions, staff and debug UI.

Canvas UI always draws over the Shapes HUD. A text label over a Shapes shape is fine. A Shapes shape over canvas UI is not possible, so do not design one.

## Scale

- Reference resolution: **1920x1080**.
- `UiScale = min(screenWidth / 1920, screenHeight / 1080)`.
  - Every Shapes pixel value is multiplied by it.
  - Every CanvasScaler uses Scale With Screen Size, 1920x1080, Expand, which is the same rule.
- 2P and 4P size differences are authored multipliers on top of `UiScale`, kept in one place.
- Tune with the Game view fixed at 1920x1080.
- Booth: a 4K 16:9 monitor, exclusive fullscreen at 4K or 1080p, so `UiScale` is exactly 2 or 1. Check both. Other aspect ratios are not needed for the demo.

## Player views

- `PlayerViewService` (scene-local) is the only UI code that:
  - maps a player to a camera and a viewport rect;
  - holds `UiScale`;
  - knows the active player count and layout.
- It raises `LayoutChanged` when the count, a rect or the screen size changes.
- `CameraManager` and `MatchSceneModeProfile` stay the authority for the viewport rects; the service reads them.
- Views are separate from players. A player can exist without a view (future online).
- No other UI code finds cameras, reads the screen size or decides 2P vs 4P.

## Code structure

- **State, presenter, profile.**
  - Gameplay publishes state and events.
  - Presenters (a Shapes drawer or a canvas view) only display them.
  - ScriptableObject profiles hold style and layout only, never runtime values.
- **One player-identity profile** (`PlayerIdentityProfile`) holds each player's main colour and label, used by single-colour UI (pointers, markers, later ability or banner tints).
  - Exception, on purpose: the leaderboard keeps its own designed per-player palette in `MatchViewportOverlayProfile` (bar fill, name tag, two potential-pattern colours). When a player's colour changes, update both.
- **Shapes drawers** use one shared toolkit:
  - What it does: projects onto the near-camera plane, converts pixels to world units with `UiScale`, draws rounded rectangles and centred text, and resets draw state.
  - Each drawer checks the camera before opening its one draw command for that camera.
- **Canvas UI** goes only through `ViewportCanvasSystem`. No other runtime canvases.
- **Time:**
  - Presentation animations use unscaled time; gameplay timers use game time.
  - `GameTimeManager` owns `Time.timeScale`. The only exceptions are the main-menu session reset and staff/debug tools.
- **Logs:** key UI state changes use the `[UI]` prefix, never per frame.

## Text

- **Canvas:** UniText (from the Asset Store). It needs a font asset, a font stack and a global style preset; without the preset, tags like `<b>` print as literal text.
- **Shapes HUD:** `Draw.Text` (TMP), for short labels only.
- **World signs:** TMP 3D text.
- **One display font** across all three. It is picked in step 2e.
- UniText has no console support. Consoles are far off; revisit before any console port.

## Migration (Round spine Step 2, see `round-spine-plan.md`)

| System | Today | Step |
|---|---|---|
| Player views | Camera lookups in PlayerWorldHUDSystem, PowerupViewportCanvasSystem, the results controller and tutorial hints | 2b: `PlayerViewService` |
| Dead UI | Inactive canvas on `Leading Cart Base` (CartControlPromptManager, CartUISwitcher, 2 old PowerupUIController), unused WorldAnchorUIFollower, empty HUD stubs in GameTimeManager | 2c: removed |
| Power-up held item, Tomato splat | Runtime overlay canvas at Constant Pixel Size | 2d: `ViewportCanvasSystem` |
| Tutorial control hints | Own overlay canvas, hard-coded rects, waits for exactly 2 or 4 players | 2d: `ViewportCanvasSystem` |
| UniText | Imported, not used | 2e: font, stack, style preset |
| Round banner | New | 2f: canvas + UniText |
| OtherPlayerPointer, OtherPlayerVisibleMarker, PowerupAim | Shapes, fixed pixels | 2g: toolkit + identity profile |
| PlayerSessionGuide, MatchViewportOverlay | Shapes, fixed pixels; dividers at screen centre; `Bar_tempfix` draws the 4P divider again | 2h: dividers from viewport rects; `Bar_tempfix` removed |
| PlayerWorldHUD (+ system and adapters) | Shapes, fixed pixels; tag polling; adapters re-find their player | 2i |
| All of it | Tuned in a Free Aspect Game view | 2j: retuned at 1920x1080, checked at 1080p and 4K |

## Done when

1. Every UI size comes from the 1920x1080 reference.
2. Only `PlayerViewService` finds a player's camera or decides 2P vs 4P for UI.
3. Every Shapes drawer uses the toolkit.
4. Profiles hold style only; nothing writes to a shared profile at runtime.
5. Player main colours live in `PlayerIdentityProfile`; only the leaderboard keeps its own matching palette.
6. Canvas UI goes only through `ViewportCanvasSystem`.
7. No dead UI is left.

## Out of scope for now

- How carts get their `Player1`-`Player4` tags at spawn (core loop).
- Results cameras.
- Keyboard aim measured from the screen centre (input).
- A one-view layout for online. The service supports any number of views, but there is no 1P mode.
