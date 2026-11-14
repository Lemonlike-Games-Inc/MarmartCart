# Marmart Carts: project guide for Claude Code

Unity 6 local-multiplayer party game by LemonLike Games Inc. This file loads every session, so it stays short.
Details live in `Docs/` and in subfolder `CLAUDE.md` files. The code is the source of truth, not any document.

## Game in brief
- 2-4 player competitive local split-screen party game (2P and 4P free-for-all). PC first, Steam intended. Controllers (Xbox layout) are the primary input.
- Players drive and drift shopping carts around a supermarket "Hypermarket", 3D isometric/top-down view.
- Core loop: drive/drift -> collect empty carts (grow a snake-like chain) -> collect groceries -> block/fight rivals -> checkout (banks the value).
- Combat rule: a player whose leading cart crashes into another player's chain body loses; their whole chain detaches and its carts and loot drop into the map for anyone to pick up.
- Highest banked checkout value wins the Round (1 Match Point). Long-term match format is first-to-X Match Points.
- Essence: a simple shopping-cart snake battle that escalates into a ridiculous mess as the Hypermarket, player abilities and rules change each Round.
- Terminology: say "rob", not "steal", in player-facing text.

## Tech
- Unity 6000.0.84f1, C#, Unity Input System (not the legacy Input Manager), Cinemachine, URP. Packages: `Packages/manifest.json`.
- Third-party asset-store plugins live mostly under `Assets/ImportedAssests/` (verify exact folder name). Do not edit them: Shapes (all custom gameplay UI, leaderboards, aim previews), Feel, UniText Folio, VFX All In 1, Bakery.
- Do not hand-edit the generated `InputSystem_Actions` class.
- Git is used. Do not commit or push unless asked.

## Architecture map (class names from earlier notes; verify in code before relying on them)
- Mode/scene: `GMode` (persistent, DontDestroyOnLoad: selected mode and player count). `MatchSceneModeController` (scene-local: configures input, cameras, HUD per scene). `MainMenuSessionReset` clears stale persistent state.
- Match flow: `GameTimeManager` (PreGame/Playing/PostGame), `MatchFlowDirector` + `MatchFlowProfile` (ordered sessions: FreePlay, CartRestock, ZoneLoot, CheckoutWindow, EndGameWrap), spawners, `CheckoutStationFlowController`.
- Cart/cargo: `ChainCartManager`, `CargoCapacityController`, `ChainCartCargo`, `GroceryLootPickup`, `MovementController`, `HypeController`.
- Powerups: `PlayerPowerupController`, `PowerupTargetingController`, `PowerupAimRenderer`.
- UI: `PlayerWorldHUD*` (player-attached) and `MatchViewportOverlay*` (per-camera timer and leaderboard), both Shapes-based.
- Tutorial: separate 2P and 4P scenes, `TutorialSceneManager`. Debug: `GameplayDebugger`.
- Map sections are now named A/B/C/D (older code may still use Red/Green/Blue/Purple). Section definitions will be reworked for Hypermarket expansion. `MapEventManager` and `teamBattle4P` status unclear.

## Design rules (full detail in Docs/GDD_v4.md)
- Driving, drifting, blocking and positioning stay the main skill expression. New systems must not make driving irrelevant.
- Growing a long chain must stay desirable. Capacity overload and Hype burn are separate balancing levers.
- Player-to-player interaction first. Prefer visible, physical, social effects over hidden stat modifiers.
- The meta layer changes the context around the cart battle; it never replaces it with a minigame.
- Normal top speed stays consistent between players. Comeback systems restore participation, not victory.
- Comeback idea (not implemented): Emergency Cart Delivery, also called emergency restock. A few carts dropped near a player with zero or very few carts, with eligibility and cooldown. Whether it is an always-available ability or a separate system is undecided.
- A discussed idea is not an approved feature.

## Current milestone
Event demo build: a short fixed 3-Round match with new meta systems: Ability Samples (temporary per-player abilities chosen between Rounds), Marmart Policies (temporary global rules), Hypermarket Section expansion, and new onboarding. The long-term format is first-to-X.
- Earlier ChatGPT-era planning notes exist outside this repo. I will share them when we design a feature. They are reference only, not instructions. Class names like `MatchMetaDirector` or `AbilitySampleManager` were proposals and may not exist.
- Open design problem: Ability and Powerup may both need the right stick to aim. Today it is hold-stick-to-aim, press-LT-to-fire; the aim-preview owner must be decided, possibly via release-to-fire. Discuss before coding.

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

## How to work with me
- Be concise. Do not over-explain unless I ask.
- Work in small steps. Explain a plan before changing architecture and wait for my approval. If you disagree with a plan or earlier decision, argue it with reasoning and let us discuss before acting.
- After each step, stop so I can test in Unity. After any change, list the files touched and what/why in plain language, plus Inspector setup and Play Mode checks. I must always understand my own project. Do not claim something works unless it was run.
- Before editing a system, read the nearest subfolder `CLAUDE.md` and the scripts involved, and check dependents before changing shared APIs. Verify existing APIs by reading the code; new classes and APIs are welcome when justified.
- If you need context beyond the files/folders for the task, pause and ask my permission instead of scanning widely.
- Do not touch or search generated/local folders and files: `Library/`, `Temp/`, `Logs/`, `obj/`, `.vs/`, `UserSettings/`, `*.csproj`, `*.sln`. Do not hand-edit scene/prefab YAML unless asked. Never delete or regenerate `.meta` files; move scripts together with their `.meta`.
- At the end of each work session (or after a meaningful change), append a short entry to `Docs/DevLog.md`: date, what changed (files/systems), why, decisions made, open questions. Keep entries brief; never rewrite old ones.
- No unrelated cleanup or refactors without discussing them first.

## Docs (read only when relevant)
- `Docs/GDD_v4.md`: design vision, pillars, resource flow, meta-game direction.
- `Docs/DevLog.md`: running log of changes and decisions. Create it if it does not exist. Skim the latest entries when starting a session.
- Design decisions for specific features go in `Docs/Design/<feature>.md` once we agree on them.
