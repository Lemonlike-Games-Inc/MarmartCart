# Architecture audit

Date: 2026-11-15. Read-only static review of branch `c_setup` (last gameplay commit 2026-09-23). Nothing was run in Unity. Items marked **Verify** are inferred from code and need a Play Mode check.

**Coverage.** Read in full: match flow, zones and spawners, checkout, score, GMode and scene mode, input, cart control. Read by three parallel passes and spot-checked: chain, cargo and battle; powerups and aiming; tutorial, the five build scenes, match UI and results. Not read: renderers, layout profiles, VFX and audio presenters, plugins.

**Severity.** High: blocks or breaks the 3-Round demo unless handled. Med: costs days or causes bugs once the meta systems land. Low: debt that can wait.

The plan and sequence built on these findings are in `nov-plan-review.md`.

## Top findings

1. **Everything assumes one match per scene load.** There is no Round, and no reset path for chains, cargo, Hype, powerups, loot or score (A1, A2). Two Round models fit the code as it is: pause the running match at a session boundary (nothing resets), or reload the scene per Round (everything resets). An in-scene reset needs new code in about ten systems.
2. **The results ceremony is one-way, and a finished match has no exit** except a debug hold-Y (A4, A5).
3. **Aim has one owner: the stored powerup.** An Ability that aims needs new arbitration. An Ability that modifies a powerup, or uses a free button without aiming, does not (B1).
4. **No announcer, no big-notification layer, no shared full-screen UI, no per-player menu input** (G1, B4).
5. **Zones are fixed at four in code, UI and lights, and the level is copied into four scenes** (E1, E2).
6. **Good news.** The 400 s match profile is already four loot-then-checkout cycles that visit Zone A to D in order (A3). Event hooks for a meta layer exist (C4, E3). Emergency Cart Delivery is cheap (C3).

## How it works today

- **Lifecycle.** `GameTimeManager` runs PreGame, Playing, PostGame once per scene load. PreGame pauses with `Time.timeScale = 0` for a fixed real-time delay, then calls `BeginMatch()`. `MatchFlowDirector` runs the `MatchFlowProfile` sessions in one coroutine; the final `EndGameWrap` raises `OnMatchEndRequested`, which enters PostGame and starts the results ceremony.
- **Mode and input.** `MatchSceneModeController.Start` reads `GMode`, then `PlayerInputManager` activates 2 or 4 player roots and polls for each runtime-spawned `CartControlScript`, pairing it with `Gamepad.all[slot]` or the keyboard. `CartControlScript` owns one `InputSystem_Actions` instance per player and exposes state and events; it does not move the cart.
- **Aim and fire.** Right stick is read only while a Tomato or Ice Cube is stored. Stick magnitude sets range. LT fires; neutral stick plus LT is rejected. Cola fires on LT with no aim.
- **Chain and battle.** The leader spawns at the chain root. A leader that touches any owned, non-vulnerable cart (its own included) or another leader loses: 4 s ghost, 2 s stop, knockback, full detach. Touching a vulnerable cart cuts the chain from that cart back, with no loss.
- **Checkout.** `CartPitZone` captures a leader that carries cargo, auto-drives it into the lane, and `CheckOutManager` banks one follower cart per X press, or automatically every 0.55 s after 1.5 s. `CashScoreManager` adds base value plus a cargo-count milestone bonus when the cart exits. One player per station at a time.
- **Zones.** `ArenaZone` (ZoneA to ZoneD) shares one loot budget across its child `ZoneLootSpawner`s and switches its child `PowerupSpawner`s on while Active.
- **Tutorial.** A copy of the gameplay scene running a 150 s timed profile, with walls and spawners toggled by a separate clock. PostGame loads the gameplay scene; `GMode` carries the player count.

## Findings

### A. Round flow and lifecycle

**A1 · High · One match per scene load; nothing is Round-aware**
- Evidence: `GameTimeManager.EnterPreGame` is private (`:149`), `BeginMatch` only runs from PreGame (`:192`), PostGame is terminal (`:232`). `CashScoreManager.ResetAllScores` is private and runs once in `Start` (`:198`, `:503`). No Round, Match Point, Ability, Policy or Section class exists in `Assets/Scripts`.
- Recommendation: treat today's lifecycle as one Round and add the Match above it, not inside it.

**A2 · High · No reset path for players or the world**
- Evidence, by system:
  - Chain: only `SnakeCartManager.DetachAllFollowers()` (`:1204`), which throws carts loose with impulse and SFX. No silent clear, no teleport, no spawn points (the leader spawns at the chain root, `:372`).
  - Hype is set only in `CartControlScript.Start` (`:505`). Ghost time can only be extended (`LeadingCartBattleController.cs:305`). MoveBackward has no public cancel.
  - Powerups: `PlayerPowerupController.ClearStoredPowerup` (`:253`) has no callers; in-flight projectiles have no cancel-all.
  - World: loot is instantiated untracked (`ZoneLootSpawner.cs:88`) and only expires on timers.
  - Results: see A4.
- All live state is scene-local. `GMode` is the only `DontDestroyOnLoad` object.
- Recommendation: do not build an in-scene reset before the demo. Two cheap routes avoid it:
  1. **Continuous Rounds.** Nothing resets. A new component listens to `MatchFlowDirector.OnSessionEnded` and pauses the match at chosen session boundaries for the intermission. The Director waits in scaled time, so `timeScale = 0` holds it with no change to it. The next session starts its Warning in the same frame and then holds at its first wait.
  2. **Clean-start Rounds.** Reload the scene per Round and keep a small persistent session object. This reuses the tutorial-to-gameplay path, and `MatchResultsPresentationController.cs:175` already assumes a rematch is a reload.
- **Verify** for route 1: a mid-match pause is untested. Today `timeScale = 0` is used only in PreGame, before anything moves.
- Ask: do chains and cargo carry across Rounds, or does everyone restart at zero? That picks the route.

**A3 · Med · A per-Round timeline needs one small seam**
- Evidence: `MatchFlowDirector.profile` is a private serialized field with no setter (`:32`). `MatchFlowProfile_2Players` and `_4Players` (`Assets/SO_Variables/`) are 14 sessions, 400 s: start, cart restock, then four cycles of ZoneLoot (50 s) and Checkout + Restock (25 s) in Zone A, B, C, D, then two short loot bursts and a final checkout.
- Recommendation: continuous Rounds need no Director change; the new component holds the session indices where a Round ends, and each existing cycle is already a Round-sized unit. Clean-start Rounds need re-cut profile assets and a `SetProfile()` on the Director that only works while stopped. Either way, do not change `MatchFlowProfile` or `MatchFlowSession` fields (known fragile).

**A4 · High · The results ceremony is one-way and fires on every PostGame**
- Evidence: `MatchResultsPresentationController.LockAndStagePlayer` (`:375-487`) disables the chain, drift, wheels and stall, makes bodies kinematic, hides followers and teleports leaders; `:528-547` disables every `CheckOutManager` and `CartPitZone`. There is no restore. It subscribes to `OnPostGameEntered` (`:746`) and is the only caller of `CashScoreManager.CommitAllActiveCheckoutSessions()` (`:220`).
- Recommendation: keep the ceremony for Match end and show a light Round result between Rounds. With continuous Rounds the ceremony runs once, unchanged. With clean-start Rounds, disable the component on non-final Rounds and have the Round flow call `CommitAllActiveCheckoutSessions()` itself.
- Ask: the ceremony ranks players by banked value, but the Match winner is decided by Match Points. What should the final screen show?

**A5 · High for an event · A finished match has no exit except debug input**
- Evidence: `onAllScoreRevealsCompleted` has no listeners in the Gameplay 4P scene. Return to menu exists only as `GameplayDebugger` hold Y for 5 s on any pad (`:13-15`, `:473-514`); keyboard R reloads the scene. The same Y button, held 3 s, skips the tutorial (`TutorialSceneManager`).
- Recommendation: add a real end-of-match step (rematch or menu, or auto-return) and decide which debug inputs ship in the event build.
- Ask: attended booth, or an unattended loop?

**A6 · Low · Stopping the flow mid tutorial loot leaks coroutines**
- Evidence: `MatchFlowDirector.StopFlowInternal` (`:172-206`) never calls `StopTutorialZoneLootCoroutines` (`:527`). `ArenaZone.CancelLootSpawning` (`:446`) only clears a flag that the spawn loop (`:334-343`) never reads.
- Recommendation: harmless today because sessions run to the end. Fix it only if in-scene restart is ever used.

**A7 · Low · `GameTimeManager` carries dead HUD code**
- Evidence: `UpdateTimerDisplay` builds a string every frame and discards it (`:254-268`, called from `Update` `:141`). `ConfigurePlayerHudReferences`, `ResetCartHud`, `UpdateCartHud` are empty (`:289-301`).
- **Verify:** sessions wait with chained `WaitForSeconds` pulses while the timer adds `Time.deltaTime`, so the HUD can reach 00:00 a moment before the match ends.
- Recommendation: delete in a cleanup pass after the demo.

### B. Input and aiming

**B1 · High · Aim has a single owner**
- Evidence: `CartControlScript.canAim` is one bool (`:70`). `PlayerPowerupController.RefreshInputPermissions` (`:302-315`) sets it true only while an unblocked Tomato or Ice Cube is stored, and overwrites any other writer. `PowerupTargetingController` reads `StoredPowerup` only (`:153-156`). Only `.performed` is hooked for LT (`CartControlScript.cs:332`).
- Free pad buttons (`InputSystem_Actions.inputactions`): A, LB, D-pad up and down, stick clicks, Start, Select. Y is taken (Reset).
- Options, cheapest first:
  1. The Ability modifies the player's powerup. No new input. About four touch points (`PowerupProjectileLauncher` `:151`, `:273`; `PowerupTargetingController` `:223`, `:266`).
  2. A non-aimed Ability on LB or A. One input-asset edit and regenerate, plus one callback in each of the pad and keyboard blocks.
  3. An aimed Ability on the same stick. `canAim` must become reason flags, targeting needs a second source, and a rule must say which item is armed.
  4. Release-to-fire. Changes the current feel and the tutorial prompts.
- Recommendation: options 1 and 2 for the demo. Options 3 and 4 after it.

**B2 · Med · Pad-to-player mapping is decided by list order at each scene load**
- Evidence: `PlayerInputManager.TryBindPlayerSlot` uses `Gamepad.all[slot]` (`:203-209`). Nothing in the project handles `InputSystem.onDeviceChange` or `InputUser.onChange`. `CartControlScript` callbacks filter on the captured device (`:286` onward).
- **Verify:** unplug and replug a pad mid-match. Then do it again and reload the scene: if the list order changed, players swap carts. Reload per Round raises the exposure. It also matters for Remote Play Together tests, where remote pads appear after load.
- Recommendation: store each slot's device id in the session object and re-pair by id on load.

**B3 · Med · Control state is plain bools with many writers**
- Evidence: `controllable`, `allowDrift`, `canSpeedup`, `canAim`, `canActivatePowerUp` in `CartControlScript` are written by `CartPitZone` (`:336-338`, `:601-617`), `LeadingCartStallController` (saves and restores raw bools, `:355-386`), `GameplayDebugger`, `TutorialInfoPanel` and `LeadingCartRaycaster`. The wheel stop `LeadingCartBehaviour.isStopping` (`:459-472`) is set by crash, checkout, MoveBackward and the ceremony; the first `ResetSpeed()` wins. Each effect adds its own named gate (`powerupFrozen`, `colaMentosBoostActive`) wired into `CanDrift` and `CanSpeedingUp` (`:772`, `:918`). Only powerup use has reason flags (`PowerupUseBlockReason`).
- Recommendation: no refactor now. Freeze the intermission with a `timeScale` pause, as PreGame does, not with the wheel stop. Before the first Ability or Policy that locks control, add one reason-keyed lock that copies the `SetUseBlocked` pattern.

**B4 · Med · No per-player menu input exists**
- Evidence: the only menu is `MainMenuTutorialLauncher`, legacy `Input`, any joystick, A for 2P and B for 4P. Nothing routes the UI action map per player.
- Recommendation: Sample Time can read state that is already per player and ungated: `CartControlScript.MoveInput`, or `OnTutorialPrev` / `OnTutorialNext` (D-pad left and right, `:375-383`), through `PlayerInputManager.GetBoundCartControl()`. No input-asset work. `Update` and input callbacks still run at `timeScale = 0`.

**B5 · Low · Duplication and per-frame noise**
- Evidence: pad and keyboard bindings are two copied lambda blocks (`CartControlScript.cs:284-384`, `:386-497`), so every new action is added twice. `UpdateSpeedup` and `UpdateHeldEvents` invoke four events every frame per player (`:684-709`). Keyboard fallback is shared by all players without a pad.

### C. Cart, chain and cargo

**C1 · Med · Speed has no modifier stack**
- Evidence: `LeadingCartBehaviour.UpdateDriveModeAndTargetSpeed` (`:183-216`) is a priority ladder evaluated by each of the four wheels: pit, stop or freeze gives 0; Cola is a single fixed override that bypasses overload, drift and speedup; otherwise base speed times the overload multiplier, plus the speedup bonus or the drift multiplier. `CartMovementProfile` fields are public and shared by all players.
- Recommendation: avoid speed-changing Abilities and Policies in the demo. If one is needed, add a per-player multiplier in `ComputeCargoAdjustedBaseSpeed()` (`:244`), read the same way as `CargoCapacityController.MovementSpeedMultiplier`.

**C2 · Med · The real combat rules differ from CLAUDE.md and the GDD**
- Evidence:
  - Vulnerable means the first two followers behind a stalled leader (`SnakeCartManager.cs:1010-1054`). The hit check has no ownership test (`LeadingCartBattleController.cs:147-153`), so cutting your own vulnerable cart costs nothing.
  - A ghosted leader cannot collect loose carts (`ChainedCartManager.cs:218`) or cut vulnerable ones.
  - Loose carts destroy themselves with their cargo after about 15 s (`ChainedCartManager.cs:547-608`); loose loot after about 20 s.
  - Checkout destroys the follower carts it banks (`SnakeCartManager.cs:1710`).
  - Gold Cart is not in code.
- Ask: are the own-cart cut and the 15 s cargo expiry intended? The GDD says potential value stays physically at risk until Checkout.

**C3 · Low · Emergency Cart Delivery is cheap**
- Evidence: `SnakeCartManager.GetOwnedFollowerCount()` (`:1613`) and `OnOwnedFollowersChanged` (`:169`) give eligibility. Dropping carts is the `Instantiate` that `CartRestockSpawner.cs:102` already does; normal trigger collection takes over.
- Missing: cooldown, a safe drop position near a player, a prefab reference.
- Trap: carts dropped during the 4 s ghost cannot be collected, and they expire at 15 s.

**C4 · Low · Battle hooks are overwritable and carry no attacker**
- Evidence: `OnBattleLost`, `OnVulnerableCartHit`, stall and drift hooks are public `System.Action` fields, not events; the first two have no subscribers. `OnBattleLost` carries no attacker and no cart count, so "P2 broke P3's chain" cannot be announced today. Cargo, follower-count and Hype events are proper events.
- Recommendation: add attacker data when the announcer needs it. Convert fields to events in the cleanup pass.

**C5 · Low · Per-frame costs and compile links to legacy code**
- Evidence: two `GetComponent<Rigidbody>()` calls per follower per FixedUpdate (`SnakeCartManager.cs:304-305`). `ChainedCartBehaviour` raycasts every FixedUpdate on kinematic carts. `renderer.materials` on each ownership change (`ChainedCartManager.cs:618`). Followers share the leader's `Player{n}` tag, so tag lookups are ambiguous. `SnakeCartManager` implements the legacy `IAssistPlayerDataSource` (`:10`).

### D. Checkout and score

**D1 · Med · Verify: a station captures on trigger enter only**
- Evidence: `CartPitZone.OnTriggerEnter` (`:204`) returns when the station is closed or occupied (`:206-207`). There is no `OnTriggerStay` and no re-check on `CheckoutStationFlowController.OnStationOpened` (`:52`; only audio listens). A cart already inside the trigger when the station opens, or when the previous player leaves, is not captured until it drives out and back in. The 5 s telegraph invites exactly that waiting.
- Recommendation: test it in Play Mode. If real, re-test occupants when the station opens or frees. It decides final-seconds banking, so it decides Rounds.

**D2 · Med · Score has no Round snapshot, winner or tie rule**
- Evidence: `CashScoreManager` holds one total per player (`:86`). The bonus is six cargo-count thresholds per checkout session (`:56-68`, `:484-496`); the code defaults set thresholds 3 to 6 all to 30. The GDD 5.4 idea of rewarding simultaneous checkouts is not in code.
- Recommendation: the session object records each Round's totals at PostGame. Decide the tie rules first (see the plan review).

**D3 · Low · Dead and leftover code**
- Evidence: `PitsManager.cs` is fully commented out. Team mapping and `OnTeamScoreGained` remain in `CashScoreManager` (`:72-81`, `:287-296`). Each `CartPitZone` holds four `PlayerCameraManager` references (`:91`), wired per station per scene.

### E. Sections, zones and powerups

**E1 · High for Section expansion · Zones are fixed at four**
- Evidence: `ArenaZoneId` has four values (`MatchFlowSession.cs:19-25`). `MatchFlowDirector.GetAllFourTutorialZones` hard-codes them (`:469-487`). `PlayerSessionGuideSystem` has four serialized zone fields and switches (`:34-45`, `:566-587`, `:657-678`). `MatchFlowAreaIndicatorController` has five light roots (`:5-14`, `:46-50`). "Section" exists only as a label string on `ZoneLootSpawner` (`:24`); no open or closed state anywhere.
- Recommendation: for the demo, a Section opening is one of the four existing zones being unlocked. No enum change. Design the Section definition and slots after the demo.

**E2 · High for level work · The level is copied into four scenes**
- Evidence: `Game Level` is loose prefab instances in each scene (219 per gameplay scene, 311 per tutorial scene). The four scenes share fileIDs for the director, zones and stations, so they are copies. Gameplay 2P and 4P differ only in `GMode.CurrentMode`, the profile asset, a `Bar_tempfix` canvas and `Debuggers`. Each scene appears to carry its own Bakery bake (inferred).
- Recommendation: build any Section gate in one scene first. Before wider level work, either make `Game Level` one prefab or merge 2P and 4P into one scene.
- Ask: is merging 2P and 4P acceptable before the demo, or after?

**E3 · Med · A new powerup type is wide; an Ability must not be one**
- Evidence: a new `PowerupId` touches about six files (`PowerupId`, `PowerupProjectileProfile` `:299-350`, `PowerupProjectilePool` `:57`, `PowerupGameplayProfile` `:181`, `PowerupHudProfile`, `PowerupPickupVisualCatalog`) plus an effect system and scene objects in every gameplay scene. A profile swap is not possible: the launcher rejects a profile that differs from the pool's (`:142`, `:255`), blind duration comes from one scene `tomatoProfile`, and Cola needs a stored Cola to activate.
- Useful hooks already exist: `PlayerPowerupController.TryStorePowerup` (`:199`), the `OnPowerupUseValidationRequested` veto, `SetUseBlocked`, and `PowerupLifecycleEventSystem` (activated, impacted, effect started and ended, with owner and target).
- Recommendation: give Abilities a small per-player override (pattern, duration, size) and a public Cola `TryApplyBoost`. Do not add `PowerupId`s for Abilities.

**E4 · Med · Ownerless projectiles need a small world launcher**
- Evidence: `PowerupProjectileLauncher` is per player and consumes inventory (`:227`, `:349`). The lower layers accept owner 0: `PowerupProjectilePool.TryRent` is public, effect systems tolerate a null source, and `IceFreezeEffectSystem.TryApplyFreeze` (`:174`) is public. Missing: about 100 lines that build a launch directly, and a public Tomato blind apply. Not checked: whether VFX and HUD presenters assume owner 1 to 4.
- Recommendation: build it only if Tomato Rain is in the demo Policy set.

**E5 · Low · Small items**
- `PowerupId.StrongFan` is an enum, HUD and catalog stub with no executor. The one pool asset (`Assets/SO_Variables/Powerup/PowerupRandomPool.asset`) holds only Tomato, Ice Cube and Cola. **Verify** that no fixed pickup grants it; LT would be accepted forever without consuming.
- Session types `CartRestock` and `CheckoutWindow` are used by none of the three profiles, and the session guide does not support them.
- Section-owned powerup pools (GDD 5.6) are data work only: each `PowerupPickup` can take its own `PowerupRandomPool`.

### F. Tutorial and scenes

**F1 · Med · The tutorial is timed, not taught**
- Evidence: `MatchFlowProfile_Tutorial` runs 150 s (FreePlay 3, cart restock 20, all-zone loot 30, checkout and battle 95, wrap 2). `tutorialGatesController` runs its own `Time.deltaTime` clock and toggles walls and spawners at 25, 50 and 90 s (`:69-73`), hand-synced to the profile. Nothing checks that a player did anything. 13 TMP "Step" texts are baked per scene.
- Reusable: `TutorialSceneManager` (hand-off, skip), `TutorialControlHintsManager` with `ControlsOverlayUI` and `ControlHints`, `TutorialOneSlotSpawner`. Throwaway: the wall-clock gates and baked texts.
- Recommendation: a Tutorial V2 that gates on completion is a rebuild of the sequencing. Budget it as such, or keep V1 and patch it.

**F2 · Low · Scene inconsistencies**
- `Debuggers` (legacy `Input`) is in both tutorial scenes and Gameplay 4P, not Gameplay 2P. `GameplayDebugger` is only in the gameplay scenes.
- Both tutorial scenes hold a live `MatchResultsPresentationController` that fires on PostGame in the same frame as the scene load. Harmless only while `loadDelay` is 0.
- `TutorialControlHintsManager.GetHardcodedViewport` (`:197-216`) duplicates the viewport rects in `MatchSceneModeProfile`.

### G. Notifications, UI and text

**G1 · High · No announcer, banner or shared full-screen UI exists**
- Evidence: "Announc" appears only in comments. `PlayerSessionGuide` is a small cart-anchored prompt and arrow for ZoneLoot and Checkout + Restock, and hides once the player is inside the target (`PlayerSessionGuideRenderer.cs:121-123`). `AudioManager` is one SFX source with string lookup; no voice queue, priority or subtitles.
- Two hosts exist: `MatchViewportOverlayRenderer` draws per camera on `cam.pixelRect` in unscaled time (`:1755-1777`); `PowerupViewportCanvasSystem` builds one Screen Space Overlay canvas with a root per viewport (`:323-333`).
- Recommendation: add a match-announcement state and one draw pass to the viewport overlay, and use it for every Level-1 message. Do not stretch `PlayerSessionGuide`.

**G2 · Med · Localization readiness is zero**
- Evidence: no script references UniText. Live text is about 45 to 50 strings in Shapes `Draw.Text`, TMP and ScriptableObject fields, with button names baked in ("X to Checkout"). The menu text is baked into images (inferred).
- Recommendation: put new meta-game text in one keyed string table. Do not retrofit old strings before the demo.

### H. Legacy that is still compile-linked

- `CartControlScript` keeps a `PowerupsManager` fallback from `PowerupsSystem/Old/` (`:199`, `:829-836`) and `IsCharing()`.
- `SnakeCartManager` implements `IAssistPlayerDataSource` from `BalancingCartPickupEntry/`.
- `LeadingCartStallSensor`, `LeadingCartRaycaster`, `CartFreezeUtil` and the two drift experiments appear only on the old leading-cart prefabs.
- `teamBattle4P` has 10 references in 5 files.
- Recommendation: these folders cannot simply be deleted. Plan the cleanup after the demo.

## Where CLAUDE.md is wrong or incomplete

1. **"UI (all Shapes)"** is wrong. The tutorial hints, the powerup HUD and tomato blind (`PowerupViewportCanvasSystem`) and `Bar_tempfix` are uGUI Screen Space Overlay; the results tower uses TMP. The "not Overlay" rule holds for Shapes only.
2. **UniText** is listed as a key plugin, but no project script uses it.
3. **Bindings** are incomplete: Y is Reset (hold 3 s skips the tutorial, hold 5 s returns to the menu), D-pad left and right are TutorialPrev and TutorialNext. Free: A, LB, D-pad up and down, stick clicks.
4. **Combat rule.** Vulnerable means the first two followers behind a stalled leader, and your own vulnerable carts can be cut too. "Loose carts keep their cargo" is true for about 15 s, then both are destroyed. A ghosted leader cannot collect carts.
5. **Player tags.** `Player1-4` is on the leader and every owned follower.
6. **Chain root** also holds `SnakePathHistory`, `SnakeMoveBackwardController`, `PhysicalChainJointProbe`. The leader also has `LeadingCartStallController`.
7. **Powerups.** Add the scene-level systems every gameplay scene needs (`PowerupRuntimeSystem`, `PowerupLifecycleEventSystem`, `PowerupAimStateSystem`, `PowerupProjectilePool`, `IceGroundHazardPool`, `PowerupViewportCanvasSystem`). Strong Fan is not implemented. Aim is read only while a projectile powerup is stored.
8. **Tutorial.** Live code also includes `Tutorial/ControlsOverlayUI`, `ControlHints` and `TutorialOneSlotSpawner`, not only `NewForSummit/`.
9. **Debug.** `GameplayDebugger` is in the gameplay scenes only and is the only return-to-menu path.
10. **Scenes.** The four gameplay and tutorial scenes are copies; the level is not one prefab. Profiles live in `Assets/SO_Variables/` (tutorial 150 s, 2P and 4P 400 s).
11. **Legacy list.** `PowerupsSystem/Old/PowerupsManager` and `BalancingCartPickupEntry/IAssistPlayerDataSource` are still referenced by live scripts. `PitsManager.cs` is fully commented out.
12. **Score.** Worth stating: base value plus a cargo-count milestone bonus per checkout session.
13. **Hype.** Only drifting earns Hype in live code.
14. **"Temporary modifiers use add/remove handles"** is a goal, not what the existing effects do (B3, C1).

## Open questions

1. Do chains and cargo carry across Rounds, or does everyone restart at zero? (A2)
2. What does the final screen show when the Match Point winner is not the top banked value? (A4)
3. Attended booth or unattended loop, and which debug inputs ship? (A5)
4. Is the checkout trigger-enter behaviour known? (D1)
5. Are the own-cart cut and the 15 s cargo expiry intended? (C2)
6. Merge the 2P and 4P scenes before or after the demo? (E2)
7. Wired or wireless pads at the event? (B2)
8. Is `SnakeCartManager.AddBodyParts` (`:1574`, no code callers) wired through a UnityEvent anywhere? Scenes were not scanned for it.
