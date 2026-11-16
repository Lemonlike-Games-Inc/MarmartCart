# Round spine: code plan

Drafted 2026-11-15. Liang accepted the plan and its four defaults on 2026-11-16 ("sounds solid"). No code is written yet; the work starts in a new session on its own branch. It implements `meta-round-flow.md` up to a playable 3-Round Match with placeholder text. No Abilities, Policies or Sections yet.

## Decisions (defaults accepted)

1. **No snapshot.** Match data changes at one moment only: when a Round is committed and the next one loads. Any reload before that moment is a Restart Round, so keyboard R stays safe. Abilities and the Policy are picked in the pre-round of the same load that uses them, so they do not need to survive a load in the demo. This is simpler than the snapshot in the flow doc. Default: yes, and I update the flow doc.
2. **Staff keys.** F1 Restart Match, F2 Restart Round, F3 Skip Round, F4 return to main menu. R keeps working as Restart Round. Default: yes.
3. **Placeholder banner.** One full-screen uGUI canvas with a TextMesh Pro text, not Shapes. It spans all viewports, so the per-camera `pixelRect` rule does not apply. It is replaced when the presentation is designed. Default: yes.
4. **New folder** `Assets/Scripts/Meta/` for the new scripts. Default: yes.

## What the code already gives us

- `GameTimeManager` runs PreGame, Playing, PostGame once per load. Turning off `Auto Begin After PreGame Pause` in the Inspector lets another script call `BeginMatch()`.
- `MatchResultsPresentationController` commits open checkouts at PostGame and raises `OnAllScoreRevealsCompleted` when the score reveal ends.
- `CashScoreManager.GetPlayerScore(playerIndex)` gives the banked value.
- `GameplayDebugger` reloads on keyboard R. Its pad return-to-menu is the Inspector toggle `Listen For Return To Menu`.
- `MainMenuSessionReset.Awake` is the place where a session ends.
- `MatchFlowDirector` has no public "end now", so there is no force-end key in this plan.

## Steps

Each step ends with a stop so Liang can test in Unity.

### Step 1: Match data and the Round loop

The cheapest proof that reload-per-Round works, with Console logs only.

- **New `MatchSession`** (static, plain data): Round number, Match Points per player, banked value per player per Round. Methods: clear, commit a Round, skip a Round.
- **New `MetaRoundController`** (scene-local): logs the Round on load. When the score reveal completes, it reads the banked values, works out the Round winners (ties all score, zero banked scores nothing), waits a set time, commits and reloads. After Round 3 it stops and waits.
- **Staff keys** F1, F2, F3 in the same controller.
- **Edit `MainMenuSessionReset`:** one line to clear Match data.
- **Inspector:** add `MetaRoundController` to one object in Gameplay Scene 2P and 4P.
- **Checks:** F3 walks Round 1, 2, 3. A full Round ends, reveals scores and loads Round 2 with the right Match Points in the Console. R during the reveal awards nothing. Pads keep their players after a load. Returning to the menu clears the data. Note the load time.

### Step 2: Pre-round countdown

- **New `RoundBanner`:** shows a text for a set time in unscaled time.
- **`MetaRoundController`:** shows "Round N", counts 3-2-1, then calls `BeginMatch()`. Logs an error if it cannot start the match.
- **Inspector:** untick `Auto Begin After PreGame Pause` on `GameTimeManager`. Create the canvas and text and assign them.
- **Checks:** every Round starts with the countdown in 2P and 4P. Nothing moves until it ends.

### Step 3: Post-round result and end of Match

- **`MetaRoundController`:** after the score reveal, the banner shows the Round winners and the Match Point totals. Rounds 1 and 2 fade to black and load the next Round. Round 3 shows the Match winners and waits for F4.
- The existing ceremony is not changed.
- **Checks:** a tied Round, a Round nobody banks in, a shared Match win, F4 to the menu and a fresh Match after it.

### Step 4: Booth Round length and settings

No code expected.

- **Round profile:** Liang duplicates the 2P and 4P profile assets in Unity and trims them to about 170 s: start, cart restock, two loot-then-checkout cycles, EndGameWrap last. Assign them in the gameplay scenes.
- **Booth setting:** untick `Listen For Return To Menu` on `GameplayDebugger`.
- **Checks:** time a full Match with the tutorial against the 12 to 15 minute budget.

## Not in this plan

Ability pick, Policy pop, Section opening, music across loads, a loading screen, the ceremony redesign, and the pad-order fix (audit B2).
