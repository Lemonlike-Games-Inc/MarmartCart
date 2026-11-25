# Marmart Policies

Agreed with Liang on 2026-11-23. Design only; no code exists yet. Class and asset names are proposals. Values are starting points to tune. Nothing here was checked against the code.

## What a Policy is

- An Ability Sample is a personal tool. A Policy is a shared problem: a global rule for the Round that changes where players go or what they fight over.
- Round 1 has no Policy.
- A Round can have more than one Policy. They run at the same time.
- There is no "Business as Usual" Policy.

## Schedule

Fixed by design for now, not random:

| Round | Policies |
|---|---|
| 1 | None |
| 2 | Cart Over-stock |
| 3 | Blackout and Tomato Rain |

Round 3 is the first test of stacking. A random pool, tiers and compatibility groups come later; the rest of the system only sees "the list of Policies for this Round", so they can replace the fixed table without other changes.

## The three Policies

### Cart Over-stock

- This Round restocks more carts.
- It acts on the cart restock (the match flow or the cart restock spawners), through an add/remove handle.
- The numbers wait for the review of the whole resource loop.

### Blackout

- The store goes dark in short windows, not for the whole Round.
- Each window lasts 5 to 10 s, chosen at random each time. The gap between windows is also random; the range is to be tuned.
- During a window each player sees only a small lit area around their own leading cart. Everything else is blocked to start with, the HUD included.
- This is the extreme version on purpose. Test first, then decide what comes back (rivals as lights, the timer, rival pointers).
- Build it per viewport and per player, with the lit radius as a per-player value. That leaves room for "see rivals a bit" and for the E.M.P. Sample, which is a personal Blackout that excludes its user. The E.M.P. Sample is not part of this work.
- Built (2026-11-24), tuned by Liang in both scenes: first window 15 to 25 s after "GO", windows 7 to 15 s, gaps 20 to 50 s, lit radius 7 m. Each window starts with a 1 s warning of three blinks. After testing nothing comes back into view, and the rival pointers around the player's own cart (inside the lit circle) fade out with the darkness.
- How it works: `PlayerViewDarkness` (on `Game UIs`) takes per-player handles, each with a strength (0 to 1), a lit radius in metres and HUD rules. Per player the strongest request and the smallest radius win. `PlayerDarknessView` draws the darkness in each player camera with Shapes, in its own pass after the normal HUD (`HudShapes.DarknessPass`), with a soft lit circle around the leading cart.
- HUD While Dark (2026-11-24), on the Blackout component, one setting per part: Covered (under the darkness), Hidden (gone, also inside the lit circle) or On Top (drawn in the pass after the darkness). Parts: rival pointers (default Hidden), timer and leaderboard, rings (hype and cargo, with their icons and prompts), action bar, Policy banners (default Covered). Both scenes use Hidden for the rival pointers and On Top for every other part (Liang, after testing). Canvas UI always draws above the darkness, so the action bar and banners fade themselves; for them Covered and Hidden both fade out. The Tomato splat and the split-screen dividers always stay on top.
- E.M.P. later: the Sample adds a darkness handle with its own HUD rules for every player except its user, so the user keeps full sight and their HUD. It stacks with Blackout with no extra code; per part the stricter setting wins. If it should flicker and fade like Blackout, move the window timing out of `BlackoutPolicyEffect` into a shared helper at that point.

### Tomato Rain

- From time to time a small to medium area gets a red circle on the ground.
- The centre of the circle fills slowly. When it is full, tomatoes rain down fast on that area for a short time.
- A leading cart that is hit gets the normal Tomato splash effect.
- The target is biased toward where the players are, because the map is large. It is not a player's current position: there is a delay between the warning and the rain. It is a random point inside the overall "heat" area that the players occupy.
- A cart in a checkout is not affected.

## Reveal

- The reveal is the first pre-round stage, before the Section reveal and the Ability pick, so the pick is informed.
- It is shown in each player's view, like the Ability Sample pick, and in a similar style: full vertical cards, revealed one by one.
- It runs on a fixed timer with no input.
- After the reveal, each card becomes a small horizontal banner card in the top-left of each view, with an icon, the name and the description. A header sits above them: "THIS ROUND'S POLICIES".
- The banner cards stay through the rest of the pre-round, so a player can check them while picking a Sample.
- There are no icon assets yet. The cards have an icon slot.
- Built (2026-11-24): while a later stage such as the Ability pick runs, the banners are compact (icon slot and a one-line name that scrolls when too long), so they fit beside the pick header. They are full during the Round title and countdown.
- The player HUD (timer, leaderboard, hype ring, cargo bars, pointers, session guide, arena arrows, action bar) is hidden from scene load until "GO" (`PlayerHudVisibility`).

In-Round reminder: being tried (2026-11-24): the compact banners stay through the Round and fade out at its end ("Keep Compact During Round" on the banner presenter). Off: they fade out at "GO".

## Announcer

A store announcer is planned as its own system, for the pre-round ("Attention shoppers...": Policies, Section updates, Sample time) and during the Round. It is not part of this work. This work leaves empty hooks for it: events on the reveal and on each Policy (for example "rain incoming", "lights out"), and a line slot on each Policy definition.

## System shape

- **Policy definition asset:** name, description, icon slot, announcer line slot, and a tier (the Ability Sample tiers). Policies have no kind.
- **Policy schedule asset:** the list of Policies per Round.
- **Random pool later** (2026-11-23): the director has a "Use Fixed Schedule" toggle, on for now. Off reads a random pool asset instead, rolled by tier weights per Round like the Ability Sample Pool. The pool is not built yet.
- **Scene-local director:** reads the schedule on load, turns each Policy's effect on when the Round starts and off when it ends. Gameplay scenes only; the tutorial has no Policies.
- **One effect component per Policy.** Each owns its own state and never assumes it is the only Policy.
- **No switch statement over Policies.**
- **Changes to other systems use add/remove handles,** and never write to a shared profile asset at runtime.
- **Explicit cleanup** on Round end, Match end, menu return and F1 to F4, with no coroutine started during teardown.
- **The reveal is a `PreRoundStage`** in `MetaRoundController`'s list, so it gets the pre-round shade and the staff-key cancel.
- Because the schedule follows the Round number, nothing new has to survive a scene load.

## Build order

Each step ends with a stop for Liang to test.

1. Read the code and propose. No code until Liang approves.
2. The Policy core with Cart Over-stock, no UI.
3. The reveal and the top-left banner cards.
4. Blackout.
5. Tomato Rain.
6. The stacking check in Round 3, the hooks, and the in-Round reminder.

If time runs short, Tomato Rain is dropped first; Round 3 then runs Blackout alone.

## Open

- The gap between rains.
- The size of the Tomato Rain area, the fill time and the rain time.
- The in-Round reminder.
- More Policies. Ideas held for later: Private Checkout, Hot Potato, Golden Burger, Absolute Chaos.
