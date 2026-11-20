# Dev Log

Newest entries at the top. Keep entries brief; never rewrite old ones.
Entry header: `## YYYY-MM-DD HH:MM (UTC offset): title`, then `Model` and `Summary` lines.

## 2026-11-20 04:27 (UTC-7): Round spine Step 3, Round results and end of Match
- Model: Claude Opus 5.5 (`claude-opus-5-5`), effort xhigh
- Summary: A Match now plays end to end on screen. Rounds fade between loads, every player sees who won each Round and the Match Points, and after Round 3 each player gets their own rank line. F4 leaves to the main menu. Liang tested both parts.
- Changed:
  - 3a (b31d987c):
    - `MetaRoundController`: every gameplay-scene load fades in; every load it starts (next Round, F1/F2/F3) fades out first. Fades advance at most 1/30 s per frame so the slow first frames after a load can't skip them.
    - New F4: fade out, clear Match data, end `GMode`, load MainMenu.
    - New `ScreenFadeView` on `Game UIs/Screen Fade` (2P and 4P scenes), drawing on the full-screen canvas root.
  - 3b (c5dbfb20):
    - `MetaRoundController`: `RoundResultShown` and `MatchResultShown` events; Round winners, Match Points after the Round (stored once, so the fade can't double-count), Match winners and dense ranks.
    - New `RoundResultView` on `Game UIs/Round Result`: per-player UniText title and detail labels on the Banner layer, built like the Round start banner.
    - New `RoundResultTextProfile` asset with every result text and rank line, editable in the Inspector; new `Round Result Title` and `Round Result Detail` label prefabs.
  - Liang added `MatchFlowProfile_ShortForTest` (0ae557ae): about 2 s per Round, a full Match in under 30 s.
  - `ui-tuning.md` lists the result banner and fade settings; `round-spine-plan.md` marks Step 3 done.
- Why: the booth needs a complete 3-Round Match with clear results and a clean exit for staff.
- Decisions:
  - Result banner per player view, in the lower part of the view; winners see "YOU WIN" or "YOU SHARE", others see the winners' names in player colours.
  - After Round 3 each player gets a rank line (winner, tied 1st, 2nd, 3rd, 4th, no winner); ties share a rank (1, 1, 2), like the podium. Lines are playful and shop-themed, and Liang can edit them in one asset.
  - Plain numbers for Match Points for now; game title and QR link on the final screen wait for art.
  - F4 works any time in a gameplay scene, keyboard only.
- Open questions:
  - With tall score towers, the result title can sit over them; move it in the prefab if needed.
  - QuickOutline logs errors on two meshes ('Box299', 'Object124') without Read/Write enabled (unrelated, seen in a long test run).
  - Still open from Step 2: the deferred 4P retune and a 4K check in a real booth build.

## 2026-11-20 03:12 (UTC-7): UI foundation 2e-2j; Round spine Step 2 closed
- Model: Claude Opus 5.5 (`claude-opus-5-5`), effort xhigh
- Summary: Finished the UI foundation. The Round banner and pre-round countdown were added. Every Shapes HUD drawer now uses one toolkit, the UI scale and the view service. HUD player binding lives in one place, and 2P/4P values live in each widget's profile. Checked at 1080p and 4K in all four gameplay scenes. Liang tested each sub-step.
- Changed:
  - 2e: UniText global style preset `Assets/Fonts/UniText/Marmart Text Styles.asset`, set in `UniTextSettings`. No custom font yet (OS font).
  - 2f:
    - `MetaRoundController` pre-round: "ROUND N" ("FINAL ROUND"), 3-2-1, "SHOP!", in unscaled time, with events.
    - `GameTimeManager.TakeOverMatchStart` and `PrepareForSceneLoad`.
    - New `RoundBannerView` and `Round Banner Label.prefab` on each player's banner layer.
    - Player cameras ignore time scale (Liang).
  - 2g: `HudShapes` toolkit and `PlayerIdentityProfile` (player colours and labels). The pointer, marker and aim drawers were migrated.
  - 2h: The session guide and viewport overlay were migrated. 4P dividers fixed (one draw command per strip) and `Bar_tempfix` removed. Liang retuned the overlay's master scale and root offset.
  - 2i:
    - World HUD drawer migrated: UI scale, toolkit, no runtime state on its profile. P4's hype fill now animates.
    - New `PlayerHUDAnchor` on `Leading Cart Base/HUDWorldAnchor` registers each cart with `PlayerWorldHUDSystem`. Tag polling and the HUD camera map were removed.
    - The three HUD adapters read the player from `SnakeCartManager`.
  - 2j:
    - The world HUD, pointer and marker 2P/4P factors moved from `MatchSceneModeProfile` into their own profiles. `MatchSceneModeProfile` keeps cameras only.
    - Stale serialized fields were removed from the build scenes and chain prefabs.
    - New `Docs/Design/ui-tuning.md` (where each UI value lives).
- Why: one consistent UI base (scale, views, binding, profiles) before Step 3's result banner and the meta features.
- Decisions:
  - The leaderboard keeps its own colour palette; other single-colour UI uses `PlayerIdentityProfile`.
  - Each widget's profile holds its 2P/4P values; drawers get the view count from `PlayerViewService`.
  - `SnakeCartManager` owns the player number; UI never searches tags or object names.
  - Shapes pitfall: back-to-back flat rectangles in one draw command don't render. Give each its own command.
  - The final retune was deferred by Liang.
- Open questions:
  - Deferred 4P layout items: the guide arrow over the cart ring, the power-up icon over the "B to Reverse" prompt, timer and leaderboard contrast over the dark area outside the map, the results leaderboard against the top edge.
  - A 4K check in a real exclusive-fullscreen build on the booth monitor.
  - The UniText display font is still not chosen.
  - Older: `LevelGreyBoxScene` missing script; Bakery missing-script warning.

## 2026-11-19 03:00 (UTC-7): UI foundation 2a-2d: views service, dead UI removed, one canvas
- Model: Claude Opus 5.5 (`claude-opus-5-5`), effort xhigh
- Summary: Built the base of the new UI system: one service that knows each player's view and the UI scale, and one canvas with a clipped box per player. The power-up HUD, Tomato splat and tutorial hints now use it. Dead UI was removed. Liang tested each step in the editor.
- Changed:
  - 2a: `Docs/Design/ui-architecture.md` (rules and migration table); `round-spine-plan.md` Step 2 split into 2a-2j; CLAUDE.md points to the doc.
  - 2b: new `PlayerViewService` (cameras, viewport rects, `UiScale`, player count, `LayoutChanged`) on `Match Load` in all four gameplay scenes; `MatchSceneModeController` feeds it.
  - 2c:
    - `Leading Cart Base.prefab` lost its inactive old UI Canvas (CartUISwitcher, 2 CartControlPromptManager, 2 old PowerupUIController) and the inactive Grabber and Bowling Ball children.
    - Unused overrides were removed from Leading Cart P1/P2 and the scenes.
    - Dead HUD code was removed from `GameTimeManager`, and `WorldAnchorUIFollower` was deleted.
  - 2d:
    - New `ViewportCanvasSystem` on `Game UIs/Viewport Canvas System`: one overlay canvas scaled from 1920x1080 (Expand), a clipped root per player with Effects/HUD/Banner layers, and a full-screen root.
    - Power-up and Tomato presenters use it; `PowerupViewportCanvasSystem` was deleted.
    - Tutorial hints build on each player's HUD layer (hard-coded rects and the `HintsOverlay` canvas components removed).
    - `PowerupHudProfile`: 2P held icon lowered (300 to 150), 4P layout scaled to about 56%.
- Why: a stable UI base before the Round banner, Ability pick and Policy UI; editor and build now match for canvas UI.
- Decisions:
  - Never commit or push without Liang's OK for that commit. Before a CLI edit, the target files must have no uncommitted changes. Reports name every editor change (scene, GameObject, component, fields, assets). CLAUDE.md and memory updated.
  - Scene and prefab cleanups run as one-off CLI editor scripts with a read-only report first.
  - Canvas structure is built at runtime; presenters own their widgets; profiles hold look and layout.
  - The scripts used by old prefabs and test scenes (CartControlPromptManager, CartUISwitcher, PowerupUIController) were kept.
- Open questions:
  - `LevelGreyBoxScene` (test scene) now has a missing `PowerupViewportCanvasSystem` script.
  - `!ftraceLightmaps` (Bakery) has a missing script in the gameplay scenes (older issue).
  - Possible leftovers on the cart: inactive Drill, Tomato, MushRoom and SimpleCrown.
  - Display font for UniText (2e).

## 2026-11-18 02:25 (UTC-7): UI architecture review; Step 2 expanded into a UI foundation
- Model: Claude Opus 5.5 (`claude-opus-5-5`), effort xhigh
- Summary: Reviewed every live UI/HUD system, Shapes 4.6, UniText 3.5 and the render setup, and ran Gameplay Scene 2P once in Play Mode. Agreed a long-term UI direction and expanded Step 2 to rebuild the HUD on it. No project code changed.
- Findings:
  - All six live Shapes drawers (world HUD, viewport overlay, pointers, markers, session guide, aim) size everything in fixed screen pixels. Builds run at native resolution, so the HUD shrinks on bigger screens: the cart ring is 36% of viewport height in the 1348x759 editor view, 25% at 1080p, 12.5% at 4K.
  - The power-up canvas keeps its CanvasScaler at Constant Pixel Size.
  - Helper code is copied across the drawers; "2P or 4P" is decided in about eight places; player colours live in three profiles; four HUD profiles hold runtime values on shared assets; 4P scenes draw the divider twice (`Bar_tempfix`).
  - UniText needs a Canvas (or its own world-text batcher), has no console support, and is not used in any scene yet. It comes from the Asset Store.
- Decisions:
  - Two layers. Shapes for procedural HUD (meters, rings, bars, pointers, aim, timer, leaderboard), treated as prototype UI until the art direction is settled. A per-player canvas with UniText for art, real text, cards, results and menus. A widget lives in one layer.
  - Draw order: world, Shapes HUD inside each camera, per-player canvas, full-screen canvas.
  - One reference resolution, 1920x1080; scale = min(width/1920, height/1080) for Shapes and every CanvasScaler. Tune with a fixed 1920x1080 Game view.
  - One `PlayerViewService` for cameras, viewport rects, UI scale and player count, with views separate from players.
  - Profiles hold style only. One player-identity profile. Presentation animations use real time. `GameTimeManager` owns game time.
  - The Round banner is canvas + UniText.
  - Step 2 now runs as 2a-2j: docs, view service, dead-UI removal, viewport canvas, UniText setup, Round banner, Shapes toolkit and the three small drawers, session guide and overlay, player world HUD, final retune. Each sub-step stops for testing.
  - Standing OK for this work: Play Mode for before/after HUD captures, and scene/prefab edits through the CLI after a commit.
- Out of scope: spawn-time player tags, results cameras, keyboard aim from screen centre, a one-view layout for online.
- Open questions: display font for UniText (CarterOne or Kids Magazine); the schedule impact on the remaining meta features (about 6-9 days for Step 2).

## 2026-11-17 02:46 (UTC-7): Round spine Step 1, Match data and Round loop
- Model: Claude Opus 5.5 (`claude-opus-5-5`), effort xhigh
- Summary: A Match now runs as 3 Rounds, one gameplay-scene reload each, with Match Points kept across loads. Console logs only, no UI. Liang tested it in Play Mode.
- Changed:
  - New `Assets/Scripts/Meta/MatchSession.cs` (static Match data: Round number, Match Points, banked value per Round) and `MetaRoundController.cs` (scene-local Round loop; staff keys F1 Restart Match, F2 Restart Round, F3 Skip Round).
  - `MainMenuSessionReset`: clears Match data.
  - `GameplayDebugger`: keyboard R removed; F2 is the only reload key.
  - `PlayerPowerupController.OnDisable`: Unity null check instead of `?.`. Fixes the `PowerupCartTarget` MissingReferenceException seen on most scene loads (an older teardown-order bug).
  - Gameplay Scene 2P and 4P: a Meta Game Controller object with `MetaRoundController` (set up by Liang).
  - `meta-round-flow.md`: R removed, load time filled in.
- Why: Step 1 of `round-spine-plan.md`; proves reload-per-Round before any UI.
- Decisions:
  - Round 3 is never saved to Match data (no next load); F2 after the Match result replays Round 3.
  - F3 does nothing in Round 3.
  - Match winners: ties share; nobody wins at 0 Match Points (confirm in Step 3).
  - Return to menu stays the gamepad hold Y (3 s) until F4 in Step 3.
  - Branch `cl_feature_MetaGameRoundSpine`. The architecture audit session now coordinates the meta-game work; Step 2 continues in this session.
- Tested by Liang: F1, F2, F3, a Round commit with the right Match Points, pad hold Y to the menu. Load time 0.5-0.8 s (Editor, 2P).
- Open questions: the Console log shows 2P runs only, so 4P and pad order after loads are not confirmed there.

## 2026-11-16 16:50 (UTC-7): Unity CLI installed and smoke-tested
- Model: Claude Fable 5.2 (`claude-fable-5-2`), effort high
- Summary: Added the Unity CLI and Pipeline package so Claude Code can read the open Editor; added usage rules to `CLAUDE.md`.
- Changed: `Packages/manifest.json` and `packages-lock.json` (`com.unity.pipeline` 1.0.0, pushed in `056a9f01`); `CLAUDE.md` (one Tech line, one usage rule). Unity CLI 1.0.1 and Unity's skills are installed on Liang's machine, outside the repo.
- Why: Unity deprecated its in-Editor MCP server; the CLI is the current official route for agents.
- Smoke test (read-only) passed on 6000.0.84f1: command list, Console (0 errors, 27 warnings, compile OK), hierarchy of Gameplay Scene 4P. Play Mode and editing commands were not tested.
- Decisions: reading is free; Play Mode and any edit need approval, with a commit first; no `eval` changes without approval; never read a whole scene hierarchy (about 1 MB).
- Also: `round-spine-plan.md` and its four defaults were accepted; the flow doc now says "no snapshot".
- Open questions: does the CLI behave in Play Mode and for edits; the 14 CLAUDE.md corrections from the audit are still not applied.

## 2026-11-15 20:10 (UTC-7): Architecture audit, Nov plan review, meta Round flow
- Model: Claude Fable 5.2 (`claude-fable-5-2`), effort high
- Summary: Read-only audit and plan review for the Dec 5 demo; agreed the 3-Round flow; drafted the Round spine code plan.
- Changed: new `Docs/Design/architecture-audit.md`, `nov-plan-review.md` (decisions in section 8), `meta-round-flow.md`, `round-spine-plan.md` (draft, not approved). New project skills in `.claude/skills/`: `grill-me`, `grilling`, `simple-english` (typed by hand to turn on). No code, scene or prefab changes. Nothing committed.
- Why: the code assumes one match per scene load and has no reset path, so the Round model had to be chosen before any meta feature.
- Decisions:
  - Demo: Dec 5, attended booth at Vancouver Game Expo, 12 to 15 minutes per group including the tutorial.
  - One Round is one reload of the gameplay scene. Plain static Match data survives the loads. Nothing else carries over.
  - Phases per Round map to the existing PreGame, Playing, PostGame.
  - 3 fixed Rounds. Most Match Points wins, no tiebreaker, ties share the win. Banked value is tracked for display only.
  - Round 1 has no Ability and no Policy. One Ability slot, replaced each Round. One Policy for the demo; Policies may stack later.
  - Ability pick is simultaneous with a timeout; Policy pop is on a fixed timer.
  - Staff controls are keyboard only: Restart Match, Restart Round, Skip Round. The final screen leaves on staff input.
  - Core loop rules (self-collision, own-cart cut, vulnerable rule, 15 s expiry) are intended and stay.
  - Tutorial rebuild is wanted, after the meta game is playable.
- Open questions:
  - Approval of `round-spine-plan.md` and its four decisions.
  - Load time between Rounds (not measured), music across loads, pad order across loads.
  - Which Sections open in which Round, the Ability Sample pool, Policies beyond Blackout.
  - Emergency Cart Delivery: Ability or automatic system.
  - The 14 CLAUDE.md corrections listed in the audit are not applied.

## 2026-11-14 16:16 (UTC-7): DevLog format
- Model: Claude Opus 5.5 (`claude-opus-5-5`), effort high
- Summary: Added timestamp, model/effort and summary lines to every entry; newest entries now go on top.
- Rewrote the two earlier entries once, with approval, to add the new header fields, and reversed the entry order. Updated the DevLog rule in `CLAUDE.md`.

## 2026-11-14 16:13 (UTC-7): Follow-up on open questions
- Model: Claude Opus 5.5 (`claude-opus-5-5`), effort high
- Summary: Answered the MercerMade and debugger questions.
- `ImportedAssests/MercerMade` is team-made art; treat it like imported assets (do not edit). Added to the CLAUDE.md plugin list.
- Debug helpers (`Debuggers.cs` vs `GameplayDebugger`) will be updated later; no change now.

## 2026-11-14 16:04 (UTC-7): Codebase audit and CLAUDE.md correction
- Model: Claude Opus 5.5 (`claude-opus-5-5`), effort high
- Summary: Audited scripts and build scenes; rewrote CLAUDE.md to match the real code.
- Changed: `CLAUDE.md` (architecture map, tech notes, new Project layout and Legacy code sections). Created this log. No code, scene or prefab changes.
- Why: CLAUDE.md was written from older notes. Read-only audit of `Assets/Scripts/` plus a reference scan of the 5 build scenes: 140 of 247 project scripts are reachable from them.
- Decisions (confirmed in review):
  - `MapEventManager` / `MapEvents/` is obsolete; its design now lives in MatchFlowDirector + MatchFlowProfile.
  - Team battle is not wanted; only 2P and 4P. `teamBattle4P` references to be removed later.
  - `MatchBalanceManager` cart assist is abandoned; there is no comeback system now.
  - Live leaderboard is the Shapes `MatchViewportOverlay*`; `LeaderboardUIs/` is obsolete.
  - Old power-up system is unused. New system sits on the `Chain of Carts P1-P4` prefab roots; the leading cart and followers are spawned at runtime.
  - Most scripts unreachable from the build scenes are obsolete leftovers.
- Open questions:
  - Cleanup pass: remove `teamBattle4P`, the old `PowerupUIController` components and inactive old power-up children on `Leading Cart Base.prefab`, and obsolete scripts. Needs a plan first.
  - Is `ImportedAssests/MercerMade` team-made art or a store asset?
  - Legacy `Debuggers.cs` is still in the tutorial scenes and Gameplay 4P (not 2P), next to `GameplayDebugger`. Keep or remove?
