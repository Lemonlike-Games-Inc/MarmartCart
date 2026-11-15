# Dev Log

Newest entries at the top. Keep entries brief; never rewrite old ones.
Entry header: `## YYYY-MM-DD HH:MM (UTC offset): title`, then `Model` and `Summary` lines.

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
