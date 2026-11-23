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

An adjective should change a rule or a behaviour. A pure stat change ("bigger", "stronger") is the weak kind and sits in the Silver tier.

## Tiers

- **Silver:** baselines and stat-only adjectives. Silver-grey frame.
- **Gold:** adjectives and gadgets that change behaviour. Gold frame.
- **Prismatic:** rarer and more spectacular than Gold. No Sample uses it yet: a plain purple frame did not read as prismatic, so it waits for a colourful, moving look.
- **Unique:** a specific Unique appears once per Match. Dark red frame.
- Changed 2026-11-22: the old Gold tier is now Silver and the old Prismatic is now Gold. The assets moved with it, since the tier is saved as a number.
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
- LB swaps the equipped item when both are loaded (decided 2026-11-21). It is the `SwapItem` action in the input asset: LB on the pad, R on the keyboard. Swapping is allowed while use is blocked (frozen, checkout); it fires nothing.
- Samples that are not aimed fire at once on LT.
- A duration effect decides per effect whether it blocks using the other slot while it runs (see `item-library.md`). Cola does not block.
- The cooldown starts when the charge is used. The first charge is ready about 10 s after "SHOP!".

This is the first version to test. It replaces the open aim-input question in `CLAUDE.md` until a playtest says otherwise.

## Powerups

| Powerup | Section | State | What it does |
|---|---|---|---|
| Tomato | Produce & Meat | Built | Aim and throw, high arc. Blocks the vision of a leading cart it lands on, the thrower's own included, but never on the way up (decided 2026-11-21; toggle Blinds Thrower on `TomatoPowerupProfile`, shared by every Tomato item). |
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
| Tomato supply | Baseline | Silver | 20 s | |
| Ice Cube supply | Baseline | Silver | 20 s | |
| Cola supply | Baseline | Silver | 30 s | A Cola lasts 10 s, so 20 s gave half the Round at Cola speed (changed 2026-11-21). A Cola used while one runs restarts the 10 s; it does not add up. |
| Really Really Cold Ice Cube | Adjective | Gold | 35 s | The cube stays until a leading cart drives into it (it still melts then) or the Round ends. Per-player cap where the oldest melts; 0 = no cap, and there is no cap for now (decided 2026-11-21). Same freeze time (3 s) and look as Ice Cube for now. |
| Rolling Tomato | Adjective | Gold | 30 s | After it lands, the tomato rolls on until it hits an obstacle or a chained cart. A leading cart it hits gets the normal Tomato effect. The whole volley of 12 rolls (decided 2026-11-21; cut the count if it gets laggy or messy). It rolls until it hits (Liang, 2026-11-21): any cart, or an Obstacles or Walls collider, stops a tomato; the thrower's own leading cart is blinded like any other while Blinds Thrower is on. The match end cancels it. The volley rolls side by side in the aim direction. The tomatoes spin forward in flight and while rolling. |
| U-shape Magnet | Gadget | Gold | 45 s | Hold the right stick to show a cone (20 m, 45°; was 70° until 2026-11-22); LT pulls the loose carts in it toward the player at 35 m/s, for up to 1.5 s. No force on chained carts. Decided 2026-11-21: pulled carts join by the normal collect rule (a rival chain in the way can collect them; nothing joins in ghost mode); only carts with a clear line to the player are pulled (shelves and walls shield); an empty cone still spends the charge. Tried 2026-11-21, kept 2026-11-22 as the current design: it also pulls rival leading carts in the cone (16 m/s, 100 m/s², 0.5 s) with the same pull method; ghost, checkout and frozen carts are never pulled, and a pulled rival that hits the thrower crashes by the normal rule. Decided 2026-11-22: a pulled leading cart is never dragged backward, only slowed to a crawl (3 m/s) and slid sideways, so it cannot fold its own chain and self-crash. See `cart-movement.md`. |

### Second wave (after more features are done)

| Sample | Kind | Tier | Cooldown | Notes |
|---|---|---|---|---|
| Piercing Tomahawk Steak | Adjective | Gold | 40 s | Passes through carts, one hit per cart. A throw along a chain can make the whole chain vulnerable. Needs the Tomahawk powerup. |
| Customer in Training | Baseline | Silver | 25 s, lasts 8 s | Needs the powerup. |
| Running in the Aisles | Adjective | Gold | 40 s, lasts 5 s | While small, the player passes through rival chained carts without crashing, and still loses to a leading cart. Other ideas: Sugar Rush (each grocery picked up extends the effect), Under the Shelves (low shortcuts; needs level support). |
| E.M.P. Device + Night-Vision Goggles | Gadget | Unique | 60 s, lasts 4 s | The store goes dark and players see only nearby. The user sees the map in green night vision. Reuses the Blackout Policy work. |

### Later

| Sample | Kind | Tier | Notes |
|---|---|---|---|
| Rotten Tomato | Adjective | Silver | Longer vision block; a small part stays for the Round. Make the tomato look rotten. |
| Leaking Cola | Adjective | Gold | Leaves a sticky trail that slows leading carts. First check whether a slow needs a force or only a speed cap. |
| Overloaded Fan | Adjective | Gold | "Times stronger" is a stat change; it needs a real adjective. Needs the Fan. |
| Looooong Baguette | Adjective | Silver | Much longer swing range. Needs the Baguette. |
| Big Squid | Adjective | Gold | More stamina and a larger effect. Needs the Squid. |

Dropped: Protein Powder (replaced by Customer in Training).

Anything that puts a force on a leading cart (Fan, perhaps the Cola trail) waits until other work is done.

## Build order for the first set

Revised 2026-11-21. Each step stops for Liang to test in Unity.

1. Read the powerup code and agree the plan (done).
2. R1, R2, R3: the hand and the Item library, keeping today's behaviour (`item-library.md`). Done 2026-11-21.
3. Ability core: the swap action, the Sample definition, the charge and cooldown, the two slots and the swap, tested with Tomato supply assigned without a pick screen. No HUD yet; logs and the Inspector. Done 2026-11-21: `AbilitySampleDefinition` assets in `SO_Variables/AbilitySamples`, `PlayerAbilityController` on `Chain of Carts Base` with a Debug Starting Sample (Tomato Supply) that must be cleared before the booth build.
4. Ice Cube supply and Cola supply. Assets done 2026-11-21, no code needed. Debug Starting Sample overrides for testing: `Chain of Carts P1` = Cola Supply, `P2` = Ice Cube Supply (P3/P4 inherit Tomato Supply). Clear them with the Base before the booth build.
5. Really Really Cold Ice Cube, with the passive on map pickups. Done 2026-11-21: Item, effect and Sample assets; the passive (`Upgrades Map Item` on the Sample); the Ice Cube SFX relay also accepts the variant. The Sample under test goes on `Chain of Carts P1` (Liang, 2026-11-21): P1 = Really Really Cold Ice Cube, P2 = Ice Cube Supply, P3/P4 = Tomato Supply from the Base.
6. Rolling Tomato. Done 2026-11-21: `ProjectileRollEffect` read by the launcher, a roll phase in `FakeArcProjectile`, and Item, throw, effect and Sample assets. The Tomato SFX relay also accepts it. `Chain of Carts P1` = Rolling Tomato. All roll tuning is on `Tomato Roll Effect`: Roll Until Hit (on; off uses Maximum Roll Distance), speed 15 m/s, roll height 0.9 m, Roll Along Aim (on), a 0.8 m roll hit radius (the prefab hitbox is about 1.7 m and caught shelves early), and the Cart Layers and Blocking Layers that stop it. The projectile pool cancels rolling items at match end.
7. U-shape Magnet. Built 2026-11-21: `ConeItemUse` (cone length and angle), `MagnetPullEffect` (pull speed, give-up time, cart and line-of-sight layers) and `MagnetPullEffectSystem` (in the four build scenes under `Powerup Runtime System/Magnet Scripts`). The aim preview draws the cone, and the system uses the published cone. Item, effect, use and Sample assets; the held-item HUD uses the unused Fan icon until step 8. `Chain of Carts P1` = U-shape Magnet.
8. Ability HUD and the changes to the powerup icons, on Liang's detailed instructions. Can move to any point after step 3. Built 2026-11-22 (8a + 8b in one go), the **Player Action Bar**: a row of plain cards at the bottom of each view, canvas UI in each player's HUD layer of `ViewportCanvasSystem`.
   - Cards in fixed slots: Powerup left, Ability middle, Emergency right (reserved, hidden until that feature exists). A lone Powerup card (no Ability) moves to the middle.
   - The equipped card is larger and is the only one showing its prompts. The LB + arrow swap hint shows only when both are loaded.
   - The Ability card always shows while a Sample is held: dimmed icon, a dark wipe over the charge still to fill, and the seconds left; a pop when it is ready. Its frame takes the Sample's tier colour.
   - An empty Powerup slot shows a faint frame while the player has an Ability; with neither, the bar hides.
   - Code: `PlayerActionBarPresenter` (was `PowerupHeldItemHudPresenter`), `PlayerActionBarProfile` (was `PowerupHudProfile`: bar prefab per player, item icons and prompts, 2P/4P anchor, offset and scale), `PlayerActionBarView` (bar root, with a Prefab Mode preview), `ActionBarCardView` (one card).
   - Prefabs: `Prefabs/UI/Player Action Bar/` Base plus P1-P4 variants (no overrides yet), so each player can get its own colours later.
   - Card polish (2026-11-22), all placeholder art made in code, no shaders:
     - Each card is layered: drop shadow, rim with a metallic gradient (`UIVertexGradient`), body, top highlight, rounded corners. The Powerup card has a light cream body; Ability cards have a dark body in their tier colour.
     - The equipped card has a white outline and glow, which flash when it becomes equipped.
     - The cooldown is a radial wipe that clears clockwise from the top.
     - Ready motion per tier: Silver still, Gold a shine sweep, Prismatic a colour-cycling rim and glow plus the shine, Unique a red glow pulse and a gold inner line. A ring bursts out when the Ability becomes ready.
     - Styles (colours and motion per tier) and timings sit on the bar root; the placeholder sprites are in `2D Arts/Player Action Bar/`.
9. The pick screen, as the last pre-round stage in Rounds 2 and 3, with room for the Policy pop and Section reveal before it.

## Open

- Rolling Tomato: the aim preview shows only the landing area, not the roll path.
- Rolling Tomato: the roll is flat and ignores the floor, so a tomato that finds a gap in the walls rolls on until the match ends.
- Effects that change the same thing at once, such as two speed effects (Cola with Customer in Training). Watch in playtests; design later.
- The rule of at most one baseline in a hand.
- Tier weights in the random offer.
- Whether three cards stay readable in a 4P view. Check early.
- Permanent ice cubes have no cap. If Rounds get too messy, set Maximum Cubes Per Player on the Really Really Cold Ice Cube Effect.
- Idea (Liang, 2026-11-21, not approved): the Magnet also becomes a map powerup with a narrower cone (about 45°), and the Sample version gets a much wider one (up to about 360°).
- U-shape Magnet: a cart pulled into a thrower in ghost mode is let go next to them; it joins only when it touches the chain again. It needs its own icon and sound (no SFX relay yet; it publishes an activation event).
- A HUD icon for the Ability and its cooldown.
- Before the booth build: the U-shape Magnet Sample asset has test timings (cooldown 1 s, first charge 1 s). Restore 45 / 10.
- Emergency Cart Delivery is parked. The last idea was a cooldown action on its own button, always available.
