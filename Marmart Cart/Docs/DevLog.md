# Dev Log

Newest entries at the top. Keep entries brief; never rewrite old ones.
Entry header: `## YYYY-MM-DD HH:MM (UTC offset): title`, then `Model` and `Summary` lines.

## 2026-11-25 16:48 (UTC-7): Marmart Policies Step 6: stacking check, sound and announcer hooks
- Model: Claude Opus 5.5 (`claude-opus-5-5`), effort xhigh
- Summary: The last Policies step. Liang tested Round 3 with Blackout and Tomato Rain together in 2P and 4P; they work side by side. The reveal and each Policy now have empty Inspector hooks for sound, VFX and announcer lines.
- Changed (f1c0d686):
  - `PolicyRevealUnityEventRelay` on `Meta Game Controller/Policy Reveal`: hooks for reveal started, card revealed, collapse started and reveal finished, plus an optional Announcer Source that plays each Policy's Announcer Clip as its card flips.
  - `PolicyUnityEventRelay` on each Policy object under `Meta Game Controller/Marmart Policies` (Cart Over-stock, Blackout, Tomato Rain): its card revealed, Round started, Round ended, and named cue hooks. Blackout cues: `lights-warning`, `lights-out`, `lights-on`. Tomato Rain cues: `rain-warning`, `rain-start`, `rain-end`.
  - `PolicyRevealStage.WasCancelled`: "reveal finished" is not called after a skip, load or teardown.
  - `Docs/Design/marmart-policies.md`: Hooks section.
- Why:
  - Liang wires sounds in the Inspector, the same way as other SFX (`SfxManager.PlaySFX`). The planned announcer system can subscribe to `MarmartPolicyDirector` and `PolicyRevealStage` in code instead.
- Decisions:
  - In-Round reminder: the compact banners stay through the Round and fade out at its end.
  - The hooks fire only on a real Round end or a finished reveal, so teardown never calls sound targets that may be gone.
  - Editor writes to `Camera P1` (and `Camera P2`) in Gameplay Scene 2P are discarded on sight.
- Open questions:
  - The random Policy pool is not built; "Use Fixed Schedule" stays on.
  - More Policies (Private Checkout, Hot Potato, Golden Burger, Absolute Chaos) are held for later.

## 2026-11-25 11:25 (UTC-7): Marmart Policies Steps 4-5: Blackout and Tomato Rain
- Model: Claude Opus 5.5 (`claude-opus-5-5`), effort xhigh
- Summary: Round 3's two Policies work. Blackout darkens each view in short windows, except a lit circle around the player's own cart, with a setting per HUD part. Tomato Rain marks a red circle near the players and rains Tomatoes into it. Liang tested both in 2P and 4P.
- Changed:
  - Step 4 (c4815cbf):
    - `BlackoutPolicyEffect`: random windows with a three-blink warning. Liang's tuning: first window 15 to 25 s after "GO", windows 7 to 15 s, gaps 20 to 50 s, lit radius 7 m.
    - `PlayerViewDarkness` on `Game UIs/Player View Darkness`: per-player handles, each with a strength, a lit radius and `DarknessHudRules`.
    - `PlayerDarknessView`: a Shapes drawer in each player camera.
    - `HudShapes` has three passes: normal HUD, darkness, and On Top HUD.
    - HUD While Dark: Covered, Hidden or On Top for each part (rival pointers, timer and leaderboard, rings, action bar, Policy banners). The action bar and banners fade through `PlayerViewDarkness.CanvasAlpha`. Both scenes: pointers Hidden, everything else On Top.
  - Step 5 (36359e42):
    - `TomatoRainPolicyEffect` launches the normal Tomato item through the new `PowerupProjectilePool.TryLaunch`, with owner 0.
    - Liang's tuning: 8 m radius, 3 s fill, 2.5 s rain at 10 drops per second, gaps 12 to 20 s.
    - `TomatoRainZoneRenderer` draws the circle.
    - Both Policies are on `Meta Game Controller/Marmart Policies` in Gameplay Scene 2P and 4P.
- Why:
  - HUD on top of the darkness needs the darkness drawn in the camera; a canvas overlay always draws over Shapes.
  - Reusing the Tomato item gives the normal hit, splat and splash with no copies.
- Decisions:
  - The darkness moved from a canvas overlay to the camera. The first canvas version and its shader were dropped before commit.
  - One dropdown per HUD part replaced the two toggle groups first asked for, so "hide" and "draw on top" cannot both be on.
  - An E.M.P. Sample later adds darkness handles for everyone but its user; per HUD part the stricter setting wins.
  - The Tomato Rain circle draws before transparent effects. The Tomato splash materials `GlowExplosion_TomatoVFX` and `Drops_TomatoVFX` write depth and cut it into squares otherwise.
  - Editor writes to `Camera P2` in Gameplay Scene 2P are discarded on sight, with no need to ask.
- Open questions:
  - Next: Step 6. Check Round 3 with both Policies together, add sound and announcer hooks, and decide the in-Round reminder (compact banners stay on trial).
  - The projectile pool and lifecycle logs write one line per landed drop (about 25 per rain).
  - Should the two Tomato splash materials stop writing depth? That is art's call.

## 2026-11-24 01:35 (UTC-7): Marmart Policies Steps 1-3, pre-round HUD hide, card polish
- Model: Claude Opus 5.5 (`claude-opus-5-5`), effort xhigh
- Summary: Policies have a core, a fixed schedule and their first effect (Cart Over-stock in Round 2). Each Round with Policies opens with a reveal in every view, and the Policies then stay as top-left banner cards. The player HUD is hidden until "GO". Prismatic cards got a colour-band rim. Liang tested each step in Play Mode.
- Changed:
  - Step 2 (5eeda4cd):
    - `PolicyDefinition` (name, tier, description, icon and announcer slots) and `PolicyRoundSource`, with `PolicySchedule` as the fixed source.
    - `MarmartPolicyDirector` on `Meta Game Controller/Marmart Policies`, with one `PolicyEffect` child per Policy.
    - `CartOverstockPolicyEffect` (restocks x1.5) through a budget handle on `CartRestockSpawner`.
    - New events `GameTimeManager.OnMatchStarting` and `MetaRoundController.LoadStarted`.
    - Assets in `SO_Variables/Policies`.
  - Step 3 (4366f165):
    - `PolicyRevealStage`, the first pre-round stage: the cards flip one by one, then collapse into the banners.
    - `PolicyRevealPresenter`/`View`, `PolicyCardView` (its own copy of the pick card's flip), `PolicyBannerPresenter`/`View`/`CardView`, `PolicyUIProfile`; prefabs in `Prefabs/UI/Policies`.
    - Banners: compact (icon slot and a one-line scrolling name) during the pick and during the Round, full during the title and countdown.
    - `MetaRoundController.PreRoundStageBegan`.
    - `PlayerHudVisibility` (per-player hide handles) hides the player HUD from load until the match starts; a check in the six Shapes drawers and the Player Action Bar.
  - Card polish (3383f7ce):
    - `UIRimGradient` gives the Prismatic rim, glow and ready ring a pastel colour band that flows while the card is selected.
    - `AbilityTierStyleProfile` Prismatic entry is now `PrismaticCardStyle` (Rim Colors, Body, Inner Line, Select Glow).
    - Gold select glow is golden (`ActionBarCardStyle.selectGlow`).
    - Shine: width 20, sweep 0.3 s.
    - Liang's tuning: U-shape Magnet is Prismatic; Sample Pool weights R2 Silver 50 / Gold 50, R3 Silver 10 / Gold 50 / Prismatic 40.
- Why: Policies are the next meta feature: shared rules that change where players go. The reveal comes before the pick so players choose knowing the Round. A HUD drawn through the pre-round cluttered the reveal, pick and countdown. A flat purple Prismatic read as weaker than Gold.
- Decisions:
  - The fixed schedule for now (Round 2 Cart Over-stock; Round 3 Blackout and Tomato Rain). A "Use Fixed Schedule" toggle leaves room for a random pool by tier, like the Sample Pool.
  - Policies have a tier (the Sample tiers) and no kind.
  - Policies start right before the match flow starts, so the first restock already counts.
  - Every exit stops them: Round end, a failed start, any load (F1 to F4, next Round) and teardown.
  - The tutorial gets no Policies.
  - Compact banners during the pick, because full ones do not fit beside the pick header. Keeping them compact through the Round is on trial ("Keep Compact During Round").
  - The audit session routed the card polish here, as its own commit.
  - The Prismatic profile entry changed type at Liang's request; Body and Inner Line carried over by name.
- Open questions:
  - Next: Step 4 Blackout (warning before a window? the gap between windows), Step 5 Tomato Rain, Step 6 stacking check, hooks and the in-Round reminder decision.
  - Policy icons do not exist yet.
  - Saving Gameplay Scene 2P keeps writing Cinemachine `Camera P2` values; they were left out of the commits each time.

## 2026-11-23 05:43 (UTC-7): Sample pool rule, Item folders, Upgrade kind; Ability Samples paused
- Model: Claude Opus 5.5 (`claude-opus-5-5`), effort xhigh
- Summary: Housekeeping after the pick screen. The Ability Samples session pauses here; it can return for more Samples before the demo.
- Changed:
  - Pool rule (cd73a639): "No Baseline With Its Upgrade", on. A hand never holds a supply and its own upgrade (Tomato supply with Rolling Tomato).
  - Folders (09c18cc9): `SO_Variables/Items` has one folder per item with its use, effects and variants; the base effects moved out of `SO_Variables/Powerup`. Liang renamed the Sample files by kind (`Supply_`, `Upgrade_`, `Gadget_`), Cola to "Cola with Mentos", and the card names and descriptions.
  - Kind rename (73852c41): `AbilitySampleKind.Adjective` is now `Upgrade`.
- Why: A supply next to its upgrade wastes a card. Item parts were spread over two folders.
- Decisions: every item folder keeps its own use asset, also Instant Use.
- Open questions: before the booth, see the 04:46 entry (Magnet timings, Debug Starting Samples, 4P Match Flow test profile); Gameplay Scene 2P has the pick's Debug Mode on (uncommitted).

## 2026-11-23 04:46 (UTC-7): Ability Samples Steps 9-10, card polish, pick screen, pre-round shade
- Model: Claude Opus 5.5 (`claude-opus-5-5`), effort xhigh
- Summary: The first Ability Sample set is complete. Before Rounds 2 and 3 every player gets three Sample cards that flip over, picks one, and plays the Round with it. The action bar cards got their polish and system sound hooks. Liang tested each step in 2P and 4P.
- Changed:
  - Card polish (19373a93): rim, body, outline and glow, radial cooldown wipe, tier motion (Gold shine, Prismatic colours, Unique pulse); new `UIVertexGradient`; placeholder card sprites in `2D Arts/Player Action Bar/`.
  - Step 9 (c386325d): `PlayerItemSfxRelay` on `Chain of Carts Base`: powerup collected, item equipped, swapped, swap failed, Ability ready. `PlayerItemHand.OnSwapPressed`.
  - Step 10a (14d4a5be): `MetaRoundController` runs Pre-Round Stages (`PreRoundStage`) before the Round title. `AbilitySamplePickController` on `Meta Game Controller/Ability Sample Pick` deals, reveals, takes input, times and sets the Sample. `AbilitySamplePool` asset with tier weights per Round. Menu input from the UI map through `CartControlScript` (`OnMenuHorizontalStep`, `OnMenuSubmitPressed`).
  - Tier styles (7337e718): shared `Ability Tier Styles` asset for the bar and the pick.
  - Step 10b-c (ae1e5288): `AbilityPickPresenter`, `AbilityPickView`, `AbilityPickCardView`, `AbilityPickProfile`; prefabs `Ability Pick` and `Ability Pick Card`; flip, pop and one tier motion on reveal; tier motion only on the highlighted card; `AbilityPickSfxRelay`; Sample Description and Card Icon fields.
  - Pre-round shade (df873be4): `PreRoundShadeView` darkens every view while any pre-round stage runs (`PreRoundStagesStarted` / `PreRoundStagesEnded`).
- Why: The demo needs a visible, exciting moment between Rounds where each player chooses their Sample. The stage list and the shade let the Policy pop and Section reveal plug in later without reopening the Round controller.
- Decisions:
  - Each card rolls its tier with that Round's weights (Round 2 Silver 60 / Gold 40, Round 3 Silver 30 / Gold 70). Three different Samples per hand; players may get the same ones. The "at most one Baseline" pity rule is a toggle, off.
  - 20 s to pick after the last reveal; the highlighted card is picked at 0. Once all have picked: hold 3 s and skip the rest. The Sample is set when the pick ends.
  - Pick Debug Mode: 99999 s and F5 (or "Deal New Cards") deals new cards.
  - Header "FREE SAMPLES!" / "One per customer". Cooldown shown as text.
  - One generic reveal sound, fired per flip in each view (stacks in 4P). Try Reveal Once Per Flip if it sounds bad.
  - Input reads the UI map, so the input asset did not change.
- Open questions:
  - Before the booth: U-shape Magnet back to 45 s / 10 s (its card reads "Cooldown 1s"); clear the Debug Starting Samples (Base Tomato Supply, P1 U-shape Magnet, P2 Ice Cube Supply); reset 4P Match Flow Round Profiles[0] (MatchFlowProfile_ShortForTest).
  - Uncommitted: Liang's PlayerActionBarProfile 2P offset/scale; Cinemachine camera values in 2P.
  - The Magnet icon is the Fan placeholder and has no own sound. Unique-once-per-Match is not built.
  - The GDD Policy table still uses the old tier names.

## 2026-11-22 19:24 (UTC-7): Ability Samples Steps 4-8, cart outside forces, Silver/Gold tiers
- Model: Claude Opus 5.5 (`claude-opus-5-5`), effort xhigh
- Summary: The first Ability Sample set is playable and shown in a new Player Action Bar: Tomato, Ice Cube and Cola supply, Really Really Cold Ice Cube, Rolling Tomato and the U-shape Magnet. A side quest gave leading carts one entry point for outside forces. The tiers are now Silver, Gold, Prismatic, Unique. Liang tested each step.
- Changed:
  - Step 4 (9ead82be): Ice Cube supply and Cola supply Samples (Baseline). No code.
  - Step 5 (f8465bf3): Really Really Cold Ice Cube and the Adjective passive. `IceCubeEffect` can stay until triggered, with an optional per-player cap; `AbilitySampleDefinition.Upgrades Map Item` swaps the player's map pickups through a hand handle.
  - Step 6 (ff4d4d92): Rolling Tomato. `ProjectileRollEffect` rolls a landed item until it hits a cart or a wall. Tomato self-hit (`HitsThrower`, Blinds Thrower on).
  - Step 7 (75e7e5b3): U-shape Magnet. `ConeItemUse` (aimed cone) and `MagnetPullEffect` + `MagnetPullEffectSystem`, which pull loose carts with a clear line to the player.
  - Side quest (0c2e6ac0): overspeed settle (`LeadingCartBehaviour`, `CartMovementProfile`); new `LeadingCartExternalForces` on Leading Cart Base; the Magnet also pulls rival leading carts. New `cart-movement.md`.
  - Step 8 (6c8e7360): Player Action Bar. The held-powerup HUD became `PlayerActionBarPresenter` + `PlayerActionBarProfile` (renamed, same GUIDs), with new `PlayerActionBarView` and `ActionBarCardView`, prefabs `Player Action Bar Base` + P1-P4 variants, and the profile asset in `SO_Variables/Player HUD`. LB and the swap arrow imported as Sprites.
  - Tiers (b6bc5b9e): `AbilitySampleTier` is Silver, Gold, Prismatic, Unique; the Sample assets moved with it (supplies Silver; Rolling Tomato, Really Really Cold Ice Cube, Magnet Gold). `ability-samples.md` and `GDD_v4.md` updated.
  - Magnet cone (f51bd45d): 45° instead of 70°.
- Why: The first set covers all three kinds (Baseline, Adjective, Gadget) for the demo. Outside forces need one place that respects the cart's speed rules. The bar must show both slots and the Ability cooldown. A flat purple frame did not read as prismatic.
- Decisions:
  - A pulled leading cart is never dragged backward, only slowed to 3 m/s and slid sideways; backward pulls folded the chain into a self-crash. Kept as the current Magnet design.
  - Every outside force on a leading cart goes through `LeadingCartExternalForces`.
  - Action bar: fixed slots; the equipped card is larger and alone shows prompts; the LB swap hint shows only when both are loaded; the Ability card shows whenever a Sample is held; an empty Powerup slot is a faint frame; the bar hides with neither. Base prefab plus per-player variants for later player colours.
  - Silver = baselines and stat-only adjectives, Gold = behaviour changes, Prismatic unused until it has a moving colourful look, Unique dark red.
- Open questions:
  - Before the booth: restore the U-shape Magnet Sample to 45 s / 10 s (test values 1 / 1) and clear the Debug Starting Samples (Base Tomato Supply, P1 U-shape Magnet, P2 Ice Cube Supply).
  - The Magnet icon is the Fan placeholder.
  - The GDD Policy table still uses the old tier names.
  - Next: card polish, then Step 9 (pick screen).

## 2026-11-21 16:03 (UTC-7): Item library and hand (R1-R3); Ability core (Step 3)
- Model: Claude Opus 5.5 (`claude-opus-5-5`), effort xhigh
- Summary: Map powerups now run on a shared Item library and a per-player hand, and `PowerupId` is gone. On top of that, the Ability core: two hand slots, LB swap, Sample charge and cooldown. Liang tested each step; map powerups behave as before.
- Changed:
  - R1 (0531f369): `PlayerPowerupController` became `PlayerItemHand` in `Scripts/Items/` (same GUID). The blockers and the use check live on the hand.
  - R2a (8919918a): Item types (`ItemDefinition`, `ThrowItemUse`, `InstantItemUse`, `ItemEffectDefinition`, `ItemLibrary`) and Item assets in `SO_Variables/Items`. The Tomato, Ice and Cola profiles became effect parts (`TomatoBlindEffect`, `IceCubeEffect`, `ColaBoostEffect`).
  - R2b (8cd263d3): aim, launcher, projectile pool and the three effect systems read the equipped Item and its parts. The scene effect profiles were removed.
  - R3 (47a1b0c7): pickups, spawner, random pool, visual catalog, HUD profile and event relays use Items. Data migrated in the four build scenes and the pickup prefab variants. `PowerupId.cs` deleted with its `.meta`. The scene saves also kept Camera P2 edit-mode values in Gameplay Scene 2P and three empty Round Profile slots in the other three scenes.
  - Liang's test setup (af354d3b): Gameplay Scene 2P values for easier Ability testing (a random map slot cooldown of 1 s, the Round profile).
  - Step 3 (df7a3316): `SwapItem` input action (LB, keyboard R) and `CartControlScript.OnSwapItemPressed`; map and ability slots and swap on the hand; new `AbilitySampleDefinition`, `PlayerAbilityController` and the `Tomato Supply` asset. The controller sits on `Chain of Carts Base` with Debug Starting Sample = Tomato Supply.
- Why: Powerups and Abilities give the same things from different sources. One library and one hand keep the Ability code out of the map powerup code and let variants reuse parts.
- Decisions:
  - The term is "Item".
  - Each duration effect decides whether it blocks the other slot (4f861d8d). Cola does not.
  - The slot loaded last is equipped; when it empties, the other one is. LB swaps when both are loaded, also while use is blocked.
  - A charge counts only while the match is playing, and the cooldown starts when the charge is used. Tomato supply: first charge 10 s, cooldown 20 s.
  - The held-item HUD shows the map slot only until the Ability HUD step.
- Open questions:
  - Clear the Debug Starting Sample on `Chain of Carts Base` before the booth build.
  - Deferred renames (such as the serialized `playerPowerupController` field) are listed in `item-library.md`.

## 2026-11-21 05:14 (UTC-7): Ability Samples plan; Item library and hand before the Abilities
- Model: Claude Opus 5.5 (`claude-opus-5-5`), effort xhigh. The first code read was done with Claude Fable 5.2 (`claude-fable-5-2`).
- Summary: Read the live powerup system (code, the 2P/4P/tutorial scene setup and the cart prefabs) for Ability Samples Step 1. Agreed to refactor it into a shared Item library and a per-player hand before any Ability code. No project code changed.
- Changed:
  - New `Docs/Design/item-library.md`: terms, layers, the Item asset shape, variants, the passive, the hand, refactor steps R1-R3 and risks.
  - `Docs/Design/ability-samples.md`: Item/Powerup terms, swap on LB, revised 9-step build order, Open list updated.
- Why: `PowerupId` means both "what the player holds" and "what the map hands out", per-item data is spread over several profiles keyed by it, and the blockers sit on the map-slot controller although they describe the player. Abilities should not reach into the map powerup code; both sources should point at one library.
- Decisions:
  - Terms: Item = what a player holds and uses; Powerup = a map pickup that gives an Item; Ability Sample = gives an Item on a charge. "Product" is not a code term.
  - New per-player hand (`PlayerItemHand`, proposed): equipped Item, swap, LT, aim permission and the blockers.
  - Item assets are built from one use part (throw, instant, cone) and a list of effect parts; each scene system handles only Items with its effect. A variant (Rolling Tomato, Really Really Cold Ice Cube) is its own asset that reuses the base Item's parts.
  - Adjective passive = an Item replacement on that player's map pickups.
  - Swap on LB, as a new action in the input asset. LT fires both slots; un-aimed Items fire at once.
  - R1 (hand), R2 (Item library), R3 (map side on Items, `PowerupId` retired) all come before the Ability steps; each keeps today's behaviour and is tested with map Items.
  - The Ability HUD and the powerup icon changes are their own later step, on Liang's instructions.
- Open questions:
  - Rolling Tomato: the whole volley rolls, or one big tomato?
  - R1-R3 plus six Samples, the HUD and the pick screen before the content freeze (about 2026-12-02) is tight.
  - Gameplay Scene Tutorial 4P setup not checked yet; check in R1.

## 2026-11-20 05:22 (UTC-7): Per-Round flow profiles; Round spine code complete
- Model: Claude Opus 5.5 (`claude-opus-5-5`), effort xhigh
- Summary: Each Round can now have its own length. With that, the Round spine (Match data and Round loop) is code-complete: Steps 1 to 3 plus per-Round profiles. Step 4 and the look polish come later.
- Changed:
  - `MatchFlowDirector` (fbc3ec40): new Round Profiles slots (Round 1, 2, 3). In Awake the director swaps in the Round's profile before anything reads it, so the timer, session guide and debug HUD follow. An empty slot uses Profile; with no slots assigned nothing changes. It logs which profile each Round uses. Tested in 4P (short, fallback 400 s, short); the scene was restored afterwards.
  - `round-spine-plan.md`: Step 4 rewritten for per-Round lengths; new "Later: look and polish" list.
  - Liang's test setup in Gameplay Scene 2P (82c725ee): Profile = `MatchFlowProfile_ShortForTest`, Round 3 slot = `MatchFlowProfile_2Players`.
- Why: Liang wants Rounds to grow longer (about 2:00, 3:00, 5:00) rather than all being the same length.
- Decisions:
  - Per-Round profiles live on `MatchFlowDirector`, since a flow profile is a Round's level design (its loot sessions already name the zones).
  - Step 4 (Round profile design, booth toggle, timing) is deferred until the more important features are done.
  - This work session stays open: Liang will come back to it to finish the look of the Round banners, the final screen and the 4P retune.
- Open questions:
  - At 2:00 + 3:00 + 5:00, plus about 20 s per Round outside play, the Match is about 11 min. With the tutorial and the menu that is close to the top of the 12 to 15 minute budget.

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
