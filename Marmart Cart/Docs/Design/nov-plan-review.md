# November plan review

Date: 2026-11-15. Reviews `Old Plan for Nov before Dec Public Demo` and the roadmap parts of `Marmart Cart Business Strategy` against GDD v4 and the code. Finding IDs such as A2 or B1 point to `architecture-audit.md`. Market analysis is skipped as asked. Nothing was run in Unity.

The earlier planning notes that propose `MatchMetaDirector` and `AbilitySampleManager` were not shared, so section 4 judges the shape those names imply, not their detail.

> **Updated 2026-11-15 after Liang's answers.** Section 8 records the decisions and wins where it differs from sections 3 to 7. The main change: Rounds start clean, so the continuous-Rounds recommendation (R1, H1) no longer applies.

## 1. Bottom line

- **The plan's calendar is gone.** Its rows from Oct 20 to Nov 7 are marked lost, and the repository has no gameplay commit after 2026-09-23. About three weeks remain for what the plan spread over eight.
- **The plan's "Required" list does not fit three weeks.** It is the full GDD between-Round loop, Tutorial V2, a UI hierarchy pass, a localization foundation and an art reboot. A 3-Round match with a loud notification layer, two or three Policies, three or four Abilities built on existing powerups, and zone unlocks does fit.
- **The cheapest proof needs almost no code.** The 400 s match is already four loot-then-checkout cycles that visit Zone A to D in order (A3). Pausing that match at cycle boundaries gives Rounds with no reset work at all.
- **Three things the plan assumes do not exist:** an announcer system, any use of UniText, and a way to leave a finished match (G1, G2, A5).

## 2. GDD v4 against the build

| GDD v4 item | In the build today | Call for the demo |
|---|---|---|
| Drive, drift, Hype, chain, overload, block, checkout | Yes | Keep as is |
| Rounds, Match Points, first-to-X | No | Build: fixed 3 Rounds |
| Opening Policies (player count + 1) | No | Cut |
| Policy Update, one per Round | No | Build: 2 or 3 Policies including Business as Usual |
| Ability Samples (2 options, pick 1, reveal) | No | Build small: 3 or 4 Abilities on existing powerups |
| Rarity tiers, Mystery, Unique, stacking | No | Cut |
| Section expansion, center + 8 slots | Four fixed zones, all open | Unlock the existing zones Round by Round |
| Sale Events | Yes (ZoneLoot sessions, guide, area lights) | Keep; add a loud banner |
| Emergency Cart Delivery | No | Build after the loop works; automatic |
| Gold Cart | No | Cut |
| NPC shoppers, Hype from hits | Legacy code only | Cut |
| Simultaneous-checkout bonus (5.4) | No; cargo-count milestones instead | Leave |
| Powerups | Tomato, Ice Cube, Cola + Mentos. Strong Fan is a stub | Keep three |
| Announcer | No | Text banner and a sound sting |
| Localization | No | New text in one keyed table, English only |

## 3. Design risks in the 3-Round demo

**R1 · High · Whether Rounds start clean is undecided, and it sets the whole cost**
- Evidence: the GDD does not say whether chains and cargo carry over. Nothing in code can reset a player (A2). Checkout already destroys the carts it banks and each cycle restocks carts, so the current match partly resets itself every cycle.
- Recommendation: start with continuous Rounds (section 4, H1). Decide on clean starts after the first playtest, not before.
- Ask: do chains and unbanked cargo carry into the next Round?

**R2 · High · Match length**
- Evidence: 2P and 4P profiles run 400 s. Three clean-start Rounds at that length is 20 minutes plus a 150 s tutorial. Three of the existing cycles in one timeline is about 4.5 minutes plus two intermissions.
- Recommendation: target about 5 to 6 minutes of match. Test it on day one by copying a profile asset and cutting it to three cycles.
- Ask: how long may one group hold the station at the event?

**R3 · High · Reading load between Rounds**
- Evidence: the GDD intermission is a result, a Section opening, a Policy, two samples per player and a reveal. Opening Policies add three to five rule cards before a new player has driven. No UI host exists for any of it (G1), and there is no per-player menu input (B4).
- Recommendation: Round 1 is the plain game with no Ability and no Policy. Keep the intermission under about 20 seconds. One Policy at a time. Show the same banner in every viewport.

**R4 · High · Who wins is easy to get wrong**
- Evidence: there is no Round score, winner or tie rule (D2). With four players, three Rounds can end 1-1-1. A Round can end with nobody banking. The ceremony ranks by banked value, which may disagree with Match Points (A4).
- Recommendation: fixed 3 Rounds, most Match Points wins, ties broken by total banked value. Show Match Points on the Round result every time.
- Ask: is that tie-break acceptable, and should a Round with no banked value award no point?

**R5 · Med · Abilities are limited by what the powerup code can express**
- Evidence: only three powerups work. An Ability that aims needs new aim arbitration (B1). An Ability cannot be a profile swap or a new `PowerupId` (E3).
- Recommendation: for the demo an Ability is a signature powerup. The player is restocked with an upgraded version on a cooldown through `PlayerPowerupController.TryStorePowerup`, with a per-player override for size, count or duration. No new button and no aim conflict. Candidates: Tomato King, XL Cola, a larger ice patch. A fourth can be a non-aimed action on LB.
- Ask: is "an upgraded powerup that restocks" visible and funny enough for the demo?

**R6 · Med · Some GDD Policies touch the most fragile code**
- Evidence: Private Checkout changes who a station scores for (`CartPitZone`, `CheckOutManager`). Hot Potato changes battle resolution. Speed-changing rules have no modifier stack (C1).
- Recommendation: pick Policies that sit on existing hooks. In rising cost: Business as Usual; a checkout rule such as "one lane only" (the profiles already open all four stations, so the Policy closes three on `OnSessionPhaseChanged`); Blackout, drawn on the per-viewport overlay canvas the tomato blind uses; Tomato Rain, which needs the world launcher (E4). A Policy must never write to a shared profile asset at runtime.

**R7 · Med · Section expansion collides with scene structure**
- Evidence: zones are fixed at four (E1) and the level is copied into four scenes (E2).
- Recommendation: a Section opening is a shutter lifting on an existing zone, timed with the cycle that first uses it. Build it in the 4P gameplay scene only until it is proven.

**R8 · Med · Event robustness**
- Evidence: no exit from a finished match (A5); pad mapping by list order (B2); the checkout capture question (D1); debug keys in the build; Y means both skip-tutorial and quit-to-menu.
- Recommendation: these are not optional. Budget them in the last phase.

**R9 · Med · The dead state inside a Round**
- Evidence: a crash gives 4 s of ghost in which carts cannot be collected, and loose carts expire with their cargo after 15 s (C2). There is no comeback system.
- Recommendation: automatic Emergency Cart Delivery, no button: drop two or three loose carts near a player who has had no followers for a few seconds, with a cooldown, after the ghost ends (C3).

## 4. Challenge to the handoff's architecture

**H1 · A meta director with in-scene phases, built first**
- The handoff: "set meta-game architecture" and a "Policy framework" in the first two weeks; a `MatchMetaDirector` is implied.
- The problem: it builds a framework before one Ability or Policy has been fun, and in-scene clean Rounds need reset code in about ten systems (A2).
- Alternatives:

| Model | What it costs | What you get |
|---|---|---|
| **Continuous Rounds.** One timeline; a new component pauses at chosen `OnSessionEnded` boundaries, runs the intermission, resumes | One component. No change to the Director, profiles or ceremony. Needs a mid-match pause check | Chains persist, the mess grows, Round score is the value banked in that Round. No level start |
| **Clean-start Rounds by reload.** Small persistent session object; intermission in the PreGame window | Session object, re-cut profiles, `SetProfile()`, ceremony gating, pad ids (B2) | Every Round starts level. A short load between Rounds |
| **In-scene reset** | Reset APIs across chain, control, battle, powerups, effects, loot, score, lifecycle | Clean start with no load. Highest regression risk |

- Recommendation: continuous Rounds now. Keep the Round data in one plain object from the start, so a move to reload later only changes where that object lives and when the intermission runs. No in-scene reset before the demo.

**H2 · New lifecycle states for the between-Round flow**
- Alternative: none are needed. Continuous Rounds pause inside Playing. Clean-start Rounds use the PreGame hook that `GameTimeManager` already documents: turn off `autoBeginAfterPreGamePause` (`:53`) and call `BeginMatch()` (`:190`) when the intermission ends.

**H3 · A Policy framework with categories, durations and stacking**
- Alternative: a Policy is a GameObject under a `Policies` root, enabled for its Round. `OnEnable` and `OnDisable` do the work. A small definition asset carries only name, text key and icon. Compatibility rules are needed only once Policies stack, which the demo does not do.

**H4 · An Ability system with its own activation and aim**
- Alternative: see R5. Abilities ride the powerup pipeline through a per-player override. The separate aimed pipeline waits until the right-stick question (B1) is designed.

**H5 · Tutorial V2 that also teaches the between-Round systems**
- The problem: the current tutorial is timed, not taught, so V2 is a rebuild of its sequencing (F1).
- Alternative: keep the current tutorial and patch its worst confusion points. Round 1 has no Ability or Policy by design, so it teaches the base game, and each intermission card explains itself.

**H6 · Localization foundation now, routed through UniText**
- The problem: nothing uses UniText, and about 50 old strings plus baked menu text would need retrofitting (G2).
- Alternative: one keyed string table for new meta text, with keys as the plan suggests. No retrofit. The Simplified Chinese smoke test moves after the demo unless a concrete need appears.

**H7 · "Put the announcer system into real use"**
- The problem: there is no announcer system (G1).
- Alternative: build the Level-1 banner first, as one announcement state and one draw pass in `MatchViewportOverlayRenderer`. Text plus a sting. Voice later. The plan's three-level hierarchy is right and is the most valuable UI work in it.

**H8 · Three gates and parallel tracks**
- The problem: Gate 1 (external test) never happened, and Gates 1 and 2 now share a week. Parallel tracks assume more than one person on code and level.
- Alternative: one cold playtest at the end of Phase 3 replaces Gate 1. Art lands in one scene only until the freeze (E2).
- Ask: who besides you is producing for this build, and how many hours?

**From the strategy document**, two points bear on engineering. Gate A prefers a later reveal over a prototype-looking one, so cutting scope serves it better than compressing scope. The Remote Play Together test depends on pads that appear after scene load being assigned a player (B2).

## 5. Recommended sequence

Dates are indicative for one person. Each phase ends with a Unity test before the next starts.

**Phase 0 · Nov 16-17 · Decide, and prove with no code**
- Copy `MatchFlowProfile_4Players`, cut the copy to three cycles, play it in 4P and 2P.
- During Play Mode, set Time Scale to 0 (Project Settings, Time) at the end of a checkout session, then back to 1. Note what keeps moving or breaks.
- Run the audit's Verify checks: checkout capture (D1), pad replug (B2), Strong Fan (E5).
- Answer decisions 1 to 6 in section 7.
- Exit: a match length that feels complete, and a Round model.

**Phase 1 · Nov 18-20 · Match spine**
- Round boundaries, pause, a rough Round result with Match Points, resume countdown, Match winner, a real exit to menu or rematch.
- No Abilities, Policies or Sections.
- Exit: from the menu, three Rounds to a Match winner and back to the menu, twice in a row, in 2P and 4P.

**Phase 2 · Nov 21-24 · One of each, hard-wired**
- The Level-1 banner, used for ROUND, CHECKOUT TIME and POLICY.
- One Policy as a toggled object. Two Abilities as powerup overrides, assigned at random with no choice UI. One zone shutter in one scene.
- Exit: an internal playtest answers the plan's Test B questions. Do players notice the Policy? Do Abilities change what they do?
- Decision gate: do Rounds need a clean start? If yes, the reload model takes the first two or three days of Phase 3 and pushes Emergency Cart Delivery out.

**Phase 3 · Nov 25-29 · Choice, safety net, cold test**
- Sample Time pick in each viewport (left stick left or right, hold to confirm) and a one-line reveal.
- Automatic Emergency Cart Delivery.
- Cold playtest with people who have not played, observed before explaining.
- Exit: a ranked list of the top five confusions.

**Phase 4 · Nov 30-Dec 2 · Fill and fix; feature freeze on Dec 2**
- Content up to the minimum: 3 or 4 Abilities, 2 or 3 Policies.
- Tutorial patch for the top confusions. Copy level changes to the other scenes.

**Phase 5 · Dec 3-5 · Hardening**
- Clean-install build, ten matches back to back, pad disconnects, long idle, backup build. No features.

**Rule if late.** If Phase 1 is not passing by Nov 22, drop the zone shutters and the Sample Time choice (keep random Abilities with a reveal). The spine, the banner and the exit are the demo.

## 6. What to cut

Cut from the demo, not from the game.

| Cut now | Why |
|---|---|
| Opening Policies | Too much reading before first play (R3) |
| Rarity tiers, Mystery, Unique, Absolute Chaos, stacking | Content and rules with no demo payoff |
| Aimed Abilities, release-to-fire | Needs the aim redesign (B1) |
| Eight Section slots, random placement, shuffle bag | Zones are fixed at four (E1) |
| Private Checkout, Hot Potato, speed-changing rules | Touch fragile core code (R6) |
| Gold Cart, NPC shoppers, simultaneous-checkout bonus | Not in code; not needed to show the loop |
| Tutorial V2 rebuild | Patch V1 instead (H5) |
| Localization retrofit and Chinese smoke test | Nothing to build on yet (H6) |
| Announcer voice | Banner and sting first (H7) |
| Legacy cleanup, `teamBattle4P` removal, scene merge | No refactors inside a deadline, unless E2 blocks level work |

| Cut if late, in this order | Fallback |
|---|---|
| Sample Time choice | Random Ability with a reveal |
| Zone shutters | All zones open from Round 1 |
| Emergency Cart Delivery | Rounds are short |
| A third Policy, a fourth Ability | Two and three |

**Do not cut:** the Level-1 banner, the Round result with Match Points, the end-of-match exit, pad robustness, the checkout capture check.

## 7. Decisions needed from you

Each has my default. Items 1 to 6 block Phase 1. These were answered on 2026-11-15; see section 8.

1. **Date and setting.** Exact build date; attended booth or unattended loop; time per group. Default: Dec 5, attended, about 8 minutes including tutorial.
2. **Round model.** Continuous, reload, or in-scene reset. Default: continuous, with reload decided at the Phase 2 gate.
3. **Carry-over.** Default: chains and unbanked cargo carry over; only the Round score restarts.
4. **Winning.** Default: 3 fixed Rounds, most Match Points, tie broken by total banked value, no point for a Round nobody banks in.
5. **Final screen.** Default: a Match Point result first, then the existing cargo ceremony unchanged.
6. **Round 1.** Default: no Ability and no Policy.
7. **Ability model.** Default: signature powerup that restocks, upgraded; at most one non-aimed LB action.
8. **Policy set.** Default: Business as Usual, one lane only, Blackout. Tomato Rain only if time allows.
9. **Sections.** Default: shutters on existing zones, one more opening each Round, built in the 4P scene first. How many are open in Round 1 is your level-design call.
10. **Tutorial.** Default: patch, do not rebuild.
11. **Emergency Cart Delivery.** Default: automatic, loose carts that rivals can also take.
12. **Team.** Who else produces art, UI or audio for this build?
13. **Rule checks.** Are the own-cart cut and the 15 s cargo expiry intended (C2)?

## 8. Decisions recorded 2026-11-15

| # | Topic | Decision |
|---|---|---|
| 1 | Date and setting | Build on Dec 5 for an attended booth at Vancouver Game Expo. 12 to 15 minutes per group including the tutorial, excluding feedback time. |
| 2 | Round model | One gameplay scene hosts the whole Match. Scene flow stays: main menu, pick 2P or 4P, that mode's tutorial scene, gameplay scene, main menu. Each Round is a reload of that scene, with plain Match data surviving the loads. Staff controls: Restart Match, Restart Round, Skip Round. Detail in `meta-round-flow.md`. |
| 3 | Carry-over | Nothing carries over. Every Round starts the same. Only Match Points, each player's Ability, the open Sections and the active Policy differ. |
| 4 | Winning | 3 fixed Rounds, most Match Points wins. No tiebreaker: all players tied at the top win, and a tied Round gives each tied player a point. No point for a Round nobody banks in. Banked value is tracked per Round and in total for display only. |
| 5 | Final screen | Match Point result, then a final ranking with the title and a QR link. It leaves to the main menu on staff input only. The presentation will be redesigned. |
| 6 | Round 1 | No Ability and no Policy. |
| 7 | Abilities | Follows item 6. The model (upgraded restocking powerup, or a button) is settled when Abilities are built. |
| 8 | Policies | Build the Policy slot and Blackout. One Policy at a time for the demo; Policies may stack later, Abilities never do. The rest of the set is designed when that work starts. "One lane only" is dropped; two lanes at a time is a candidate but may frustrate more than it adds. |
| 9 | Sections | Reuse the existing arena with small edits. A system opens more Sections after each Round. Keep the work small. |
| 10 | Tutorial | A rebuild is wanted and is treated as important for a public event. It can only be designed after the meta game is built and tested. |
| 11 | Emergency Cart Delivery | Undecided between Ability and system. Recommendation: an automatic system outside the Ability slot, so Round 1 needs no Ability system and no player has to give it up for a fun Ability. |
| 12 | Team | Liang: design and code. JP: Art Director, defining character and prop direction; nothing lands in the engine for this demo. Mercer: technical artist; shaders, post-processing, possibly specific VFX. |
| 13 | Core rules | Self-collision, the own-cart cut, the vulnerable rule, and the 15 s expiry of loose carts and cargo are all intended. The core loop is tested and stays as it is. The meta features are content added on top. |

**What this changes in the plan**

- **Time budget.** About 3 minutes of tutorial leaves 9 to 12 minutes of Match. Three Rounds of roughly 170 s fit: start, cart restock, then two of the existing loot-then-checkout cycles.
- **Clean starts.** Decisions 2 and 3 together need each Round to begin from a clean scene. Two ways give that inside the one gameplay scene:
  - **Reload the gameplay scene between Rounds.** A small session object keeps Match Points, Abilities, Sections and the Policy. The core loop code is not touched. "Reset Round" and "skip Round" are the same reload with a different Round number. Cost: a short load, and the pad mapping risk (audit B2).
  - **Reset in place.** No load, and the Hypermarket can change in front of the players. It needs new reset code in the chain, control, battle, checkout lane, powerup and spawner code, which is the core that decision 13 wants left alone (audit A2).
  - **Decided: reload.** The agreed flow is in `meta-round-flow.md`. The load time is still to be tested with keyboard R in the gameplay scene.
- **Tutorial.** The rebuild moves into Phase 4 with a go or no-go on Nov 27, once the meta game has been played. The fallback is the patched current tutorial. Room for it comes from the smaller Section and Policy work.
- **Sequence.** Phases 0 to 3 keep their order. Phase 1 builds the clean-start spine and the debug controls instead of continuous Rounds.
