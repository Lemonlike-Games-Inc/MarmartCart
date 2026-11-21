# Ability Samples

Agreed with Liang on 2026-11-20 and 2026-11-21. Design only; no code exists yet. Values are starting points to tune. Build-risk notes are estimates and were not checked against the code.

The goal is long-run design value, not only the event demo: the demo is a playtest of ideas that may or may not stay.

## What an Ability Sample is

- **Item:** what a player holds and uses (Tomato, Ice Cube, Cola, Rolling Tomato, U-shape Magnet...). Items live in one shared library; see `item-library.md`.
- **Powerup:** a map pickup that gives an Item. It is tied to a Section.
- **Ability Sample:** picked between Rounds, lasts one Round, and is replaced at the next pick. It never stacks. It gives an Item on a charge.

There are three kinds of Sample:

| Kind | What it gives | Example |
|---|---|---|
| Baseline | A slow personal charge of a normal powerup. It works even when that product's Section is closed. | Tomato supply |
| Adjective | An upgraded product: its own slow charge, plus a passive that upgrades every pickup of that product for this player. | Rolling Tomato |
| Gadget | An effect with no product on the map. Charge only, no passive. | U-shape Magnet |

Adjectives point players at a Section ("P2 has Rolling Tomato, so P2 wants produce"). That creates conflict between players at a known place, which is why the pick comes after the Section reveal.

An adjective should change a rule or a behaviour. A pure stat change ("bigger", "stronger") is the weak kind and sits in the Gold tier.

## Tiers

- **Gold:** baselines and stat-only adjectives.
- **Prismatic:** adjectives and gadgets that change behaviour.
- **Unique:** a specific Unique appears once per Match.
- Mystery Samples are cut.

## The offer

- Each player is offered 3 Samples and picks 1.
- The offer is random per player, with tiers mixed. Two players may pick the same Sample.
- Proposed, not yet agreed: at most one baseline in a hand, so no player gets three baselines.
- Later, much later: a one-by-one reveal with more show.

## The pick

- Pre-round order for Rounds 2 and 3: Policy pop, Section reveal, Ability pick, countdown. Round 1 has no pick.
- All players pick at the same time, each in their own viewport.
- Left stick or D-pad highlights; A confirms.
- After about 15 s, a player who has not confirmed gets the highlighted Sample.
- A locked pick is shown larger, so the other players can read it. There is no separate reveal step.

## Using it in a Round

- A player can hold a map powerup and an Ability charge at the same time.
- One aim (right stick) and one fire button (LT), as today.
- The item that became ready last is equipped. When one slot is empty, the other is equipped.
- LB swaps the equipped item when both are loaded (decided 2026-11-21). It is a new action in the input asset.
- Samples that are not aimed fire at once on LT.
- The cooldown starts when the charge is used. The first charge is ready about 10 s after "SHOP!".

This is the first version to test. It replaces the open aim-input question in `CLAUDE.md` until a playtest says otherwise.

## Powerups

| Powerup | Section | State | What it does |
|---|---|---|---|
| Tomato | Produce & Meat | Built | Aim and throw, high arc. Blocks the vision of a leading cart it lands on. |
| Ice Cube | Seafood & Frozen | Built | Aim and throw, high arc. Freezes a leading cart it lands on. The cube stays for a short time. |
| Cola + Mentos | Toys & Snacks | Built | Activate. Faster than a Hype boost while active. |
| Tomahawk Steak | Produce & Meat | Concept | Aim and throw, low arc. Makes a chained cart vulnerable; breaks a cart that is already vulnerable. On a leading cart, makes its nearest chained carts vulnerable (start at 3, the idea was 5). Vulnerable carts recover after a time. |
| Customer in Training | Toys & Snacks | Concept | Activate. Shrinks the player's chain (kids' carts with a flag) and gives a small speed gain. Normal combat rules. Keep the speed gain small so it differs from Cola. |
| Fan | Household & Bread | Concept | Activate, then aim. Pushes back carts that drive toward it and speeds up carts that drive away. A safer first version blows loose carts and thrown items only. |
| French Baguette | Household & Bread | Concept | Activate. A swing around the leading cart; a hit makes the rival's screen dizzy. A hit that spills cargo was suggested as a more distinct effect. |
| Squid | Seafood & Frozen | Concept | Activate, then aim. Shoots straight ink streams, about 5, from a stamina bar. Ink covers a hit rival's screen. It needs an effect of its own, for example ink that stays on the floor. |

## Sample pool

Cooldowns are starting values.

### First set (this loop)

| Sample | Kind | Tier | Cooldown | Notes |
|---|---|---|---|---|
| Tomato supply | Baseline | Gold | 20 s | |
| Ice Cube supply | Baseline | Gold | 20 s | |
| Cola supply | Baseline | Gold | 20 s | |
| Really Really Cold Ice Cube | Adjective | Prismatic | 35 s | The cube stays for the whole Round. Cap the number of cubes; the oldest melts. A longer freeze is the secondary effect: keep it small. |
| Rolling Tomato | Adjective | Prismatic | 30 s | After it lands, the tomato rolls on until it hits an obstacle or a chained cart. A leading cart it hits gets the normal Tomato effect. |
| U-shape Magnet | Gadget | Prismatic | 45 s | Shows a cone; on fire, pulls all loose carts in the cone to the player fast. No force on leading carts or chained carts. |

### Second wave (after more features are done)

| Sample | Kind | Tier | Cooldown | Notes |
|---|---|---|---|---|
| Piercing Tomahawk Steak | Adjective | Prismatic | 40 s | Passes through carts, one hit per cart. A throw along a chain can make the whole chain vulnerable. Needs the Tomahawk powerup. |
| Customer in Training | Baseline | Gold | 25 s, lasts 8 s | Needs the powerup. |
| Running in the Aisles | Adjective | Prismatic | 40 s, lasts 5 s | While small, the player passes through rival chained carts without crashing, and still loses to a leading cart. Other ideas: Sugar Rush (each grocery picked up extends the effect), Under the Shelves (low shortcuts; needs level support). |
| E.M.P. Device + Night-Vision Goggles | Gadget | Unique | 60 s, lasts 4 s | The store goes dark and players see only nearby. The user sees the map in green night vision. Reuses the Blackout Policy work. |

### Later

| Sample | Kind | Tier | Notes |
|---|---|---|---|
| Rotten Tomato | Adjective | Gold | Longer vision block; a small part stays for the Round. Make the tomato look rotten. |
| Leaking Cola | Adjective | Prismatic | Leaves a sticky trail that slows leading carts. First check whether a slow needs a force or only a speed cap. |
| Overloaded Fan | Adjective | Prismatic | "Times stronger" is a stat change; it needs a real adjective. Needs the Fan. |
| Looooong Baguette | Adjective | Gold | Much longer swing range. Needs the Baguette. |
| Big Squid | Adjective | Prismatic | More stamina and a larger effect. Needs the Squid. |

Dropped: Protein Powder (replaced by Customer in Training).

Anything that puts a force on a leading cart (Fan, perhaps the Cola trail) waits until other work is done.

## Build order for the first set

Revised 2026-11-21. Each step stops for Liang to test in Unity.

1. Read the powerup code and agree the plan (done).
2. R1, R2, R3: the hand and the Item library, keeping today's behaviour (`item-library.md`).
3. Ability core: the swap action, the Sample definition, the charge and cooldown, the two slots and the swap, tested with Tomato supply assigned without a pick screen. No HUD yet; logs and the Inspector.
4. Ice Cube supply and Cola supply.
5. Really Really Cold Ice Cube, with the passive on map pickups.
6. Rolling Tomato.
7. U-shape Magnet.
8. Ability HUD and the changes to the powerup icons, on Liang's detailed instructions. Can move to any point after step 3.
9. The pick screen, as the last pre-round stage in Rounds 2 and 3, with room for the Policy pop and Section reveal before it.

## Open

- Rolling Tomato: does the whole volley roll, or one big tomato?
- The rule of at most one baseline in a hand.
- Tier weights in the random offer.
- Whether three cards stay readable in a 4P view. Check early.
- The cap on permanent ice cubes.
- Whether loose carts pulled by the Magnet attach by the normal collect rule.
- A HUD icon for the Ability and its cooldown.
- Emergency Cart Delivery is parked. The last idea was a cooldown action on its own button, always available.
