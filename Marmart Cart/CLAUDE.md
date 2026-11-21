# Marmart Carts: project guide for Claude Code

Unity 6 local-multiplayer party game by LemonLike Games Inc. This file loads every session, so it stays short.
Details live in `Docs/` (and in subfolder `CLAUDE.md` files once we add any). The code is the source of truth, not any document.

## Game in brief
- 2-4 player competitive local split-screen party game (2P and 4P free-for-all). PC first, Steam intended. Controllers (Xbox layout) are the primary input.
- Players drive and drift shopping carts around a supermarket "Hypermarket", 3D isometric/top-down view.
- Core loop: drive/drift -> collect empty carts (grow a snake-like chain) -> collect groceries -> block/fight rivals -> checkout (banks the value).
- Combat rule: a leading cart that hits a non-vulnerable chain cart (its own or a rival's) or a rival's leading cart loses: short ghost mode, knockback, and its whole chain detaches into loose carts anyone can collect. Loose carts keep their cargo; only overload spills. Exception: hitting a rival's vulnerable (stalled) cart cuts the rival's chain from that cart back, and the attacker does not lose.
- Highest banked checkout value wins the Round (1 Match Point). Long-term match format is first-to-X Match Points.
- Essence: a simple shopping-cart snake battle that escalates into a ridiculous mess as the Hypermarket, player abilities and rules change each Round.
- Terminology: say "rob", not "steal", in player-facing text.

## Tech
- Unity 6000.0.50f1, C#, Cinemachine, URP. Packages: `Packages/manifest.json`.
- Input: gameplay uses the Unity Input System, and new code should too. Active Input Handling is set to Both because `MainMenuTutorialLauncher` and a few debug scripts still use the legacy `Input` API.
- Gamepad bindings (Player map): right stick Aim, LT ActivatePowerUp, RT Speedup (spends Hype), RB Drift, B MoveBackward, X CheckOut.
- Third-party plugins live under `Assets/ImportedAssests/` (the misspelling is the real folder name), except Bakery (`Assets/Bakery/`) and TextMesh Pro. Do not edit them. Key ones: Shapes (all custom gameplay UI, leaderboards, aim previews), Feel (includes MoreMountains Tools and Nice Vibrations), UniText Folio (`ImportedAssests/UniText`, resources in `Assets/LightSide/`), VFX All In 1 (`AllIn1VfxToolkit`), Bakery. Also there: QuickOutline, vFolders, Synty, Kenney packs, Hovl Studio, Vefects, Ciathyza, Feel_URPfix, and `MercerMade` (team-made art, treated as imported assets).
- Do not hand-edit the generated `InputSystem_Actions` class.
- Unity CLI: the `unity` command plus the `com.unity.pipeline` package let Claude Code talk to the open Editor. Usage rules are under "How to work with me".
- Git is used. Never commit or push without my explicit OK for that commit. I review every change in git and test it in the editor before it is committed.

## Project layout
- Gameplay code: `Assets/Scripts/`.
- Build scenes (the only ones enabled in Build Settings): `Assets/Scenes/Liang/Full Indie Summit 2026/` with MainMenu, Gameplay Scene Tutorial 2P/4P, Gameplay Scene 2P/4P. All other scenes are personal test scenes.

## Architecture map (checked against the build scenes on 2026-11-14; verify details in code)
- Mode/scene: `GMode` (DontDestroyOnLoad: game mode and player count). Its mode is set in the Inspector of each tutorial scene and carries into gameplay; the menu only chooses which tutorial scene to load. `MatchSceneModeController` (scene-local: configures input, cameras, HUD from GMode). `PlayerInputManager` (ours, not Unity's class of the same name: activates player roots, assigns devices). `MainMenuSessionReset` destroys GMode and resets `Time.timeScale`.
- Match flow: `GameTimeManager` owns PreGame and PostGame. `MatchFlowDirector` + `MatchFlowProfile` own the Playing timeline (session types: FreePlay, CartRestock, ZoneLoot, CheckoutWindow, CheckoutRestock, TutorialZoneLoot, EndGameWrap; EndGameWrap must be last). Areas are `ArenaZone` (`ArenaZoneId` ZoneA-D) fed by `ZoneLootSpawner`; carts come from `CartRestockSpawner`.
- Checkout: `CheckoutStationFlowController` opens/closes one station (North/East/South/West). `CheckOutManager` + `CartPitZone` run the checkout. `CashScoreManager` banks the score.
- Player chain: `Prefabs/Chain of Carts/Chain of Carts P1-P4` (variants of `Chain of Carts Base`). The root holds the per-player controllers (`SnakeCartManager`, `CargoCapacityController`, `PlayerPowerupController`, `PowerupTargetingController`, `PowerupProjectileLauncher`, ...). At runtime `SnakeCartManager` spawns the leading cart as the first child (tagged Player1-4) and builds follower carts behind it.
- Leading cart: `CartControlScript` (input, control state, Hype), `LeadingCartBehaviour` (per-wheel drive, 4 per cart), `CartDriftController`, `CartSteering`, `CartMovementProfile` (SO), `CartDriftHypeReward` (drifting earns Hype), `LeadingCartBattleController` (crash rules).
- Follower carts and cargo: `ChainedCartManager` (one cart: owned/loose/vulnerable), `ChainCartCargo` (authoritative cargo), `GroceryLootPickup`.
- Powerups (`Scripts/PowerupsSystem/`, not its `Old/` folder): `PlayerPowerupController`, `PowerupTargetingController`, `PowerupAimRenderer`, plus the Tomato, Ice Cube and Cola Mentos effect systems.
- UI (rules in `Docs/Design/ui-architecture.md`): `PlayerViewService` (on `Match Load`: player cameras, viewports, UI scale) and `ViewportCanvasSystem` (`Game UIs/Viewport Canvas System`: the one canvas, a clipped root per player with Effects/HUD/Banner layers, plus a full-screen root). Shapes HUD: `PlayerWorldHUD*` + `CartWorldHUD*Adapter` (player-attached), `MatchViewportOverlay*` (per-camera timer and live leaderboard), `PlayerSessionGuide*`, other-player pointers and markers. Canvas UI: power-up held item, Tomato splat, tutorial control hints.
- Tutorial: separate 2P and 4P scenes, `TutorialSceneManager` and `Scripts/Tutorial/NewForSummit/`. Debug: `GameplayDebugger`.
- Map areas: code says Zone (`ArenaZoneId`), design docs say Section. Section definitions will be reworked for Hypermarket expansion.

## Legacy code (not in the build; do not build on it)
- Confirmed obsolete: `MapEventManager` and `Scripts/MapEvents/` (design moved into MatchFlowDirector/Profile), `MatchBalanceManager` and `Scripts/BalancingCartPickupEntry/` (abandoned with the new level design), `Scripts/LeaderboardUIs/` (replaced by the Shapes overlay), `Scripts/Powerups/Old/` and `Scripts/PowerupsSystem/Old/`.
- `teamBattle4P` is not wanted; only 2P and 4P free-for-all. Its references in `GMode`, `CashScoreManager` and the old leaderboards are to be removed later.
- Many other scripts are leftovers from older scenes (for example `Protoprototype/`, `AI Shoppers/`, `ComboDealSystem/`, `Assets/Cart/`, the old tutorial task scripts). Before relying on or extending a script, check that a build scene or prefab uses it.
- Possible leftovers on the live cart: `Prefabs/Leading Carts/Leading Cart Base.prefab` has inactive nested prefabs Drill, Tomato and MushRoom under `Cart Model/Attached Models/In Cart Models` (from the legacy `Assets/Cart/Prefab/`), and an inactive SimpleCrown elsewhere (a `CrownSpin` script exists). Check before removing. Its old UI canvas and the Bowling Ball and Grabber children were removed on 2026-11-19.

## Design rules (full detail in Docs/GDD_v4.md)
- Driving, drifting, blocking and positioning stay the main skill expression. New systems must not make driving irrelevant.
- Growing a long chain must stay desirable. Capacity overload and Hype burn are separate balancing levers.
- Player-to-player interaction first. Prefer visible, physical, social effects over hidden stat modifiers.
- The meta layer changes the context around the cart battle; it never replaces it with a minigame.
- Normal top speed stays consistent between players. Comeback systems restore participation, not victory.
- There is no comeback system today (the old `MatchBalanceManager` cart assist was abandoned). Idea, not implemented: Emergency Cart Delivery, also called emergency restock. A few carts dropped near a player with zero or very few carts, with eligibility and cooldown. Whether it is an always-available ability or a separate system is undecided.
- A discussed idea is not an approved feature.

## Current milestone
Event demo build: a short fixed 3-Round match with new meta systems: Ability Samples (temporary per-player abilities chosen between Rounds), Marmart Policies (temporary global rules), Hypermarket Section expansion, and new onboarding. The long-term format is first-to-X.
- None of this exists in code yet: no Rounds, Match Points, Abilities or Policies. Today a match is a single MatchFlowProfile timeline.
- Earlier ChatGPT-era planning notes exist outside this repo. I will share them when we design a feature. They are reference only, not instructions. Class names like `MatchMetaDirector` or `AbilitySampleManager` were proposals and may not exist.
- Open design problem: Ability and Powerup may both need the right stick to aim. Today it is hold right stick to aim, press LT to fire; the aim-preview owner must be decided, possibly via release-to-fire. Discuss before coding.

## Code goals
- Clean, efficient, performance-conscious code. Comments only where they add necessary information.
- Scope: local multiplayer only; always support 2P and 4P, no Player 1 assumptions, runtime-spawned players must work. No networking code.
- Prefer small, additive changes; keep working behaviour, public APIs, serialized field names and prefab/scene references unless we agree otherwise. If a serialized field must be renamed, use `[FormerlySerializedAs]`.
- ScriptableObjects hold definitions/config; mutable runtime state lives in runtime instances, never in shared SO assets.
- Scene-specific logic stays scene-local. Cross-scene state persists only when necessary, with explicit reset behaviour.
- Keep gameplay separate from presentation; expose events/hooks for the art team (VFX, audio, UI).
- Temporary modifiers use add/remove handles, not `speed += x` / `speed -= x`.
- Cache references; no per-frame `Find`/`GetComponent`/allocations. Use `UnityEngine.Random` explicitly.
- No enterprise architecture, god managers or giant switch statements for abilities/policies.
- Explicit cleanup on Round end, Match end, menu return, scene load and tutorial skip.
- Log key state transitions with a prefix (`[Meta]`, `[Ability]`, ...), never per-frame.
- These conventions are defaults. Challenge any of them, or propose refactors, when it clearly improves the project.

## Known fragile areas
- Making `MatchSceneModeController` persistent broke input init in new scenes.
- Changing `MatchFlowProfile` serialization can null the profile and stall matches at PreGame 00:00.
- Match teardown must not start coroutines (`OnDisable`). Reset `Time.timeScale` on tutorial/scene transitions.
- Shapes UI is not Screen Space Overlay; it must respect each camera's `pixelRect`.
- Each gameplay scene also has its own GMode, which is destroyed as a duplicate when one arrives from the tutorial. Its Inspector mode only matters when you press Play in that scene directly.

## How to work with me
- Be concise. Do not over-explain unless I ask.
- Work in small steps. Explain a plan before changing architecture and wait for my approval. If you disagree with a plan or earlier decision, argue it with reasoning and let us discuss before acting.
- After each step, stop so I can test in Unity. After any change, list the files touched and what/why in plain language, plus Inspector setup and Play Mode checks. I must always understand my own project. Do not claim something works unless it was run.
- Report every editor change exactly: which scene or prefab, which GameObject, which component was added or removed, which fields changed and to what, and any asset created. A script's file path alone is not enough.
- Before editing a system, read the nearest subfolder `CLAUDE.md` (if any) and the scripts involved, and check dependents before changing shared APIs. Verify existing APIs by reading the code; new classes and APIs are welcome when justified.
- If you need context beyond the files/folders for the task, pause and ask my permission instead of scanning widely.
- Do not touch or search generated/local folders and files: `Library/`, `Temp/`, `Logs/`, `obj/`, `.vs/`, `UserSettings/`, `*.csproj`, `*.sln`. Do not hand-edit scene/prefab YAML unless asked. Never delete or regenerate `.meta` files; move scripts together with their `.meta`.
- Unity CLI use. Free: reading the Console, reading scenes and assets, checking compilation. Ask first: entering Play Mode, and any command that edits a scene, prefab, asset or setting. Such edits skip Undo, so first check that the target files have no uncommitted changes (then git can restore them); if they do, ask me. Never commit to make room for an edit. Never use `eval` to change the project without my approval. Never read a whole scene hierarchy (about 1 MB); query one object or a filtered part.
- DevLog entries are written only when I ask. After several big changes without an entry, remind me once: "We made several big updates; should we add a DevLog entry?" Each entry goes at the top of `Docs/DevLog.md` (newest first): local date and time with UTC offset, Claude model and effort level, a one-line summary, what changed (files/systems), why, decisions made, open questions. Get the time from the system clock, never guess it. Keep entries brief; never rewrite old ones.
- No unrelated cleanup or refactors without discussing them first.

## Docs (read only when relevant)
- `Docs/GDD_v4.md`: design vision, pillars, resource flow, meta-game direction.
- `Docs/Design/ui-architecture.md`: UI rules (Shapes HUD + per-player canvas with UniText, 1920x1080 scale, PlayerViewService). Read before any UI work.
- `Docs/DevLog.md`: running log of changes and decisions, newest first. Skim the top few entries once at the start of a new session; after that, read it only when I ask or when you need it (say so first).
- Design decisions for specific features go in `Docs/Design/<feature>.md` once we agree on them.
