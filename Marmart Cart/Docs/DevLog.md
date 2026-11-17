# Dev Log

Newest entries at the top. Keep entries brief; never rewrite old ones.
Entry header: `## YYYY-MM-DD HH:MM (UTC offset): title`, then `Model` and `Summary` lines.

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
