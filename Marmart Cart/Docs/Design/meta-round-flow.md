# Meta round flow

Agreed with Liang on 2026-11-15. Design only; no code exists yet. Class names below are proposals.

## Scene flow

1. **Main menu.** Title screen, and where staff choose 2P or 4P.
2. **Tutorial scene** (2P or 4P).
3. **Gameplay scene** (2P or 4P). The whole Match runs here: 3 Rounds.
4. **Main menu.**

## Round phases

One Round is one load of the gameplay scene. The three phases are the ones `GameTimeManager` already runs per load.

| Phase | Existing phase | Round 1 | Rounds 2 and 3 |
|---|---|---|---|
| Pre-round | PreGame, time paused | "Round 1" countdown, then start | Ability pick, Policy pop, "Round N" countdown, then start |
| Mid-round | Playing | Today's `MatchFlowDirector` timeline, unchanged | Same |
| Post-round | PostGame | Round winner showcase with total Match Points | Same. Round 3 then shows the final ranking |

An arena camera pan before Round 1 is wanted but very low priority.

## Clean start

- **Reload.** The next Round starts by loading the same gameplay scene again. Nothing is reset in place.
- **Match data.** A plain static class (proposed name `MatchSession`) survives the loads. A scene-local controller (proposed name `MetaRoundController`) reads it when the scene loads and writes the Round result at the end.
- **Rule.** Match data holds only plain values and asset references. It never holds a scene object or an event.
- **Empty Match data** means Round 1, so pressing Play in the gameplay scene works.
- **Cleared** on Restart Match and on return to the main menu.
- **Transition.** A fade for the demo. A loading screen or transition scene may come later.
- **Later.** If instant Rounds are ever needed, only "how the next Round starts" changes.

Match data holds:

- Round number.
- Match Points per player.
- Banked value per player, per Round and in total. For display only.

Match data changes at one moment only: when a Round is committed and the next one loads. Any reload before that moment replays the Round, so no snapshot is needed.

Not stored for the demo, because each is chosen in the pre-round of the same load that uses it:

- Each player's Ability. One slot; a new pick replaces the old one.
- The active Policy. Stored as a list later if Policies stack.
- Open Sections. They follow from the Round number.

## Staff controls

Keyboard only, included in the booth build. Pad debug shortcuts are off for the booth.

| Key | Command | Result |
|---|---|---|
| F1 | Restart Match | Round 1, Match data cleared, no tutorial |
| F2 | Restart Round | Back to this Round's pre-round: Match Points as they were, picks cleared. Keyboard R does the same |
| F3 | Skip Round | Next Round's pre-round, no Match Point awarded |
| F4 | Main menu | Leaves the Match and clears Match data |

There is no finer checkpoint. A fault during a showcase replays that Round.

## Scoring

- The highest banked value in a Round scores 1 Match Point.
- A tied Round gives every tied player a Match Point.
- A Round nobody banks in gives no point.
- Most Match Points after Round 3 wins. There is no tiebreaker: all players tied at the top are winners.

## Ability pick

- All players pick at the same time, each in their own viewport, from 2 or 3 offered Ability Samples.
- Two players may pick the same one.
- After about 15 s, a player who has not confirmed gets the option they are highlighting.

## Policy pop

Shown for about 5 s on a fixed timer, with no input, then the countdown.

## End of Match

The final ranking stays on screen with time paused, with the game title and a QR link. It leaves to the main menu on staff input only.

## Out of scope

The two-monitor booth setup (trailer loop, live game) is operated by staff outside the Unity build.

## Open

- Load time between Rounds: not measured. Press R in the gameplay scene.
- Music restarts on each load unless its source is made to survive.
- Pad order is read again on each load (audit B2).
- Which Sections open in which Round.
- The Ability Sample pool and how samples are offered.
- The Policy set beyond Blackout.
- Emergency Cart Delivery: Ability or automatic system.
- The showcase and final ranking presentation.
