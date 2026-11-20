# Round spine: code plan

Drafted 2026-11-15. Liang accepted the plan and its four defaults on 2026-11-16 ("sounds solid"). It implements `meta-round-flow.md` up to a playable 3-Round Match. No Abilities, Policies or Sections yet.

Status: Step 1 done (2026-11-17, commit f3086217, branch `cl_feature_MetaGameRoundSpine`). On 2026-11-18 Step 2 was expanded into a UI foundation step; the rules are in `ui-architecture.md`. Step 2 done 2026-11-20 (commits aa687990 to 0a8c0fbe); its final retune was deferred by Liang. Step 3 done 2026-11-20 (commits b31d987c, c5dbfb20). Per-Round flow profiles added (fbc3ec40). The code is complete; Step 4 (Liang, no code) and the look polish come later.

## Decisions (defaults accepted)

1. **No snapshot.** Match data changes at one moment only: when a Round is committed and the next one loads. Any reload before that moment is a Restart Round, so keyboard R stays safe. Abilities and the Policy are picked in the pre-round of the same load that uses them, so they do not need to survive a load in the demo. This is simpler than the snapshot in the flow doc. Default: yes, and I update the flow doc.
2. **Staff keys.** F1 Restart Match, F2 Restart Round, F3 Skip Round, F4 return to main menu. Default: yes. Changed 2026-11-17: keyboard R was removed; F2 is the only reload key.
3. **Banner.** Replaced 2026-11-18: the banner is shown per player, on the per-player canvas with UniText text (see `ui-architecture.md`), not one full-screen TMP canvas.
4. **New folder** `Assets/Scripts/Meta/` for the new scripts. Default: yes.

## What the code already gives us

- `GameTimeManager` runs PreGame, Playing, PostGame once per load. Turning off `Auto Begin After PreGame Pause` in the Inspector lets another script call `BeginMatch()`.
- `MatchResultsPresentationController` commits open checkouts at PostGame and raises `OnAllScoreRevealsCompleted` when the score reveal ends.
- `CashScoreManager.GetPlayerScore(playerIndex)` gives the banked value.
- `GameplayDebugger`'s pad return-to-menu (hold Y) is the Inspector toggle `Listen For Return To Menu`. Its keyboard R reload was removed in Step 1.
- `MainMenuSessionReset.Awake` is the place where a session ends.
- `MatchFlowDirector` has no public "end now", so there is no force-end key in this plan.

## Steps

Each step ends with a stop so Liang can test in Unity.

### Step 1: Match data and the Round loop (done 2026-11-17)

The cheapest proof that reload-per-Round works, with Console logs only.

- **New `MatchSession`** (static, plain data): Round number, Match Points per player, banked value per player per Round. Methods: clear, commit a Round, skip a Round.
- **New `MetaRoundController`** (scene-local): logs the Round on load. When the score reveal completes, it reads the banked values, works out the Round winners (ties all score, zero banked scores nothing), waits a set time, commits and reloads. After Round 3 it stops and waits.
- **Staff keys** F1, F2, F3 in the same controller.
- **Edit `MainMenuSessionReset`:** one line to clear Match data.
- **Inspector:** add `MetaRoundController` to one object in Gameplay Scene 2P and 4P.
- **Checks:** F3 walks Round 1, 2, 3. A full Round ends, reveals scores and loads Round 2 with the right Match Points in the Console. R during the reveal awards nothing. Pads keep their players after a load. Returning to the menu clears the data. Note the load time.

### Step 2: UI foundation and pre-round countdown (done 2026-11-20)

Expanded on 2026-11-18 so the existing HUD is rebuilt on the rules in `ui-architecture.md` before new UI is added. Each sub-step stops for Liang to test. Detail questions are asked at the start of each sub-step. Standing OK for this step: Play Mode for checks and before/after HUD captures, and scene/prefab edits through the CLI when the target files have no uncommitted changes. No commit without Liang's OK for that commit (rule changed 2026-11-19).

- **2a. Docs:** `ui-architecture.md` and this update.
- **2b. `PlayerViewService`:** per-player camera, viewport rect and active flag; `UiScale`; the active player count and layout; a `LayoutChanged` event. `MatchSceneModeController` feeds it. Nothing uses it yet.
  - Checks: `[UI]` logs are right in 2P and 4P; resizing the Game view updates the scale.
- **2c. Remove dead UI:**
  - The inactive canvas on `Leading Cart Base`, with CartControlPromptManager, CartUISwitcher and the 2 old PowerupUIController.
  - The unused WorldAnchorUIFollower.
  - The empty HUD stubs in GameTimeManager.
  - Checks: carts and HUD behave as before.
- **2d. `ViewportCanvasSystem`:**
  - One overlay canvas scaled from 1920x1080, with one root per player clipped to its viewport (effects, HUD, banner) and a full-screen root.
  - The power-up held item and Tomato splat move onto it; `PowerupViewportCanvasSystem` keeps its public methods.
  - The tutorial control hints move onto it, dropping their hard-coded rects.
  - Checks: power-up icon, Tomato splat and tutorial hints in 2P and 4P; nothing spills into a neighbour's viewport.
- **2e. UniText setup:** display font asset, font stack, global style preset.
  - Checks: a test label renders crisply in both modes.
- **2f. Round banner:**
  - `MetaRoundController` gets a pre-round stage: "ROUND N" (Round 3: "FINAL ROUND"), 3-2-1, "SHOP!", in unscaled time, with events.
  - `GameTimeManager` keeps game time: the controller asks it to begin the match, and the time-scale reset before a load moves into a `GameTimeManager` method.
  - New `RoundBannerView` (UniText) on each player's banner root only displays.
  - Checks: every Round in 2P and 4P plays the sequence; nothing moves before "SHOP!"; F1, F2 and F3 during the countdown load cleanly.
- **2g. Shapes toolkit and player-identity profile;** migrate OtherPlayerPointer, OtherPlayerVisibleMarker and PowerupAim.
  - Checks: same look at 1920x1080 (before/after captures).
- **2h. Migrate PlayerSessionGuide and MatchViewportOverlay;** dividers come from the viewport rects; `Bar_tempfix` is removed.
  - Checks: same look at 1920x1080; one divider in 4P.
- **2i. Migrate PlayerWorldHUD,** its system and its three adapters, onto the view service and toolkit; player binding in one place.
  - Checks: same look at 1920x1080; HUD follows the right cart in 2P and 4P.
- **2j. Final pass:** retune at 1920x1080.
  - Checks: same proportions at 1080p and 4K in all four gameplay scenes (both tutorials too), 2P and 4P.
  - Done: 2j-1 moved every 2P/4P factor into its widget's profile and removed stale serialized data; 2j-2 added `ui-tuning.md`. The 1080p/4K check passed in all four scenes and on the results screen.
  - Deferred by Liang (the retune), all 4P:
    - The session guide's arrow circle covers the top of the cart HUD ring.
    - The power-up held icon covers the "B to Reverse" prompt.
    - Over the dark area outside the map, the timer pill and leaderboard bars lose contrast.
    - The results leaderboard sits against the top edge of each view.
  - Not yet done: a check in a real 4K exclusive-fullscreen build on the booth monitor.

### Step 3: Post-round result and end of Match (done 2026-11-20)

- **`MetaRoundController`:** after the score reveal, the banner shows the Round winners and the Match Point totals. Rounds 1 and 2 fade to black (full-screen canvas root) and load the next Round. Round 3 shows the Match winners and waits for F4.
- The existing ceremony is not changed.
- **Checks:** a tied Round, a Round nobody banks in, a shared Match win, F4 to the menu and a fresh Match after it.
- Done:
  - 3a (b31d987c): every gameplay-scene load fades in; every load the controller starts fades out first (`ScreenFadeView` on the full-screen root). F4 fades out, clears Match data and loads MainMenu.
  - 3b (c5dbfb20): `RoundResultView` shows, per player view, who won the Round and the Match Points ("YOU" for the view's player). After Round 3 each player gets a rank line; ties share a rank (1, 1, 2), like the podium.
  - All result texts and rank lines live in `RoundResultTextProfile`, editable in the Inspector (added at Liang's request).
  - Liang's `MatchFlowProfile_ShortForTest` (about 2 s per Round) makes a full Match testable in under 30 s.

### Step 4: Booth Round lengths and settings

No code expected. Liang does this later, after the more important features (agreed 2026-11-20).

- **Round profiles:** Rounds no longer share one length. `MatchFlowDirector` has a Round Profiles slot per Round (commit fbc3ec40); an empty slot uses the scene's Profile.
  - Liang designs one flow profile per Round for 2P and for 4P, starting around 2:00 (Round 1), 3:00 (Round 2) and 5:00 (Round 3), each with EndGameWrap last.
  - Assign them in Gameplay Scene 2P and 4P (`Match Flow Director` > Round Profiles).
  - Gameplay Scene 2P currently holds Liang's test setup (Profile = `MatchFlowProfile_ShortForTest`, Round 3 slot = `MatchFlowProfile_2Players`). Reset it when the real profiles go in.
- **Booth setting:** untick `Listen For Return To Menu` on `GameplayDebugger` (on `Game Time & Score Manager` in both gameplay scenes).
- **Checks:** time a full Match with the tutorial against the 12 to 15 minute budget.
  - Each Round adds about 20 s outside play (title and countdown, podium, result, fades, load).
  - At 2:00 + 3:00 + 5:00 that is about 11 min for the Match; with the tutorial (150 s today) and the menu, close to the top of the budget.

## Later: look and polish

The Round spine code is complete. Liang will come back to this work session to finish the look of these once the more important features are done:

- Round start banner and result banner: final look and animation (label prefabs, `RoundResultTextProfile` lines).
- Final screen: game title and QR link.
- The deferred 4P retune (see 2j) and a 4K check in a real booth build.

## Not in this plan

Ability pick, Policy pop, Section opening, music across loads, a loading screen, the ceremony redesign, and the pad-order fix (audit B2).
