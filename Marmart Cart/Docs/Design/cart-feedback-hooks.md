# Cart feedback hooks

Planned with Liang on 2026-11-25, built 2026-11-25/26 on `cl_feature_CartFeedbackHooks` (Steps 2-3: `acebeabf`, `3f3aaf9d`). This describes what exists. Guides: `cart-feedback-guide-for-mercer.md` (short, for Mercer) and `cart-feedback-guide-for-mercer-claude.md` (detailed, for his Claude).

## Goal

Mercer, working with his own Claude, improves three feedbacks: the crash, the speedup and the drift, without editing core cart scripts, `Leading Cart Base` or scenes. He edits only the `Cart Feedback` prefab and his own scripts in `Assets/Scripts/CartFeedback/`.

## Gameplay additions (additive only)

- `LeadingCartBattleController`: `CartCrashInfo` (`CartCrashInfo.cs`: `Cause` OwnChain/RivalChain/RivalLeadingCart/Other, `CrashedPlayerIndex`, `OtherPlayerIndex`, `HitPoint`, `HitDirection`, `SpeedBeforeHit`, `CartsLost`). New events `OnCrashed` (the crasher, right after `OnBattleLost`) and `OnCausedCrash` (the rival whose cart or chain was hit), and `PlayerIndex`. `OnBattleLost` and the public `LoseBattle()` (cause Other) are unchanged. `OnCausedCrash` is gameplay-side on purpose, so a later reward (for example Hype for a block) can listen to it.
- `CartDriftController`: `OnDriftSideChanged(newSign)`, because a drift can switch side mid-drift.
- `CameraManager`: per-player zoom offsets (`AddZoomOffset` / `RemoveZoomOffset`, returning a `CameraZoomHandle` whose `SizeOffset` can change every frame), added on top of the cart-count size; `ShakePlayerCamera(player, amplitude, duration)` and `StopAllCameraShakes()`; scene-local `Instance`. All cleared when `CameraManager` is disabled.
- `PlayerCameraShake`: Cinemachine extension, added at runtime by `CameraManager` to each active player camera. No scene setup.

## Presentation hooks (`Assets/Scripts/Carts Related/Cart Feedback Hooks/`)

- `LeadingCartFeedbackRelay` on the `Leading Cart Base` root. C# events, once per change: `Crashed`, `CausedCrash`, `SpeedupStarted/Stopped` (edge-detected from the per-frame `OnSpeedupHeld`), `ColaBoostStarted/Stopped`, `DriftStarted(side)`, `DriftSideChanged(side)`, `DriftStopped(side, interrupted)`. State: `PlayerIndex`, `IsSpeedingUp`, `IsColaBoostActive`, `IsDrifting`, `DriftSide`, `DriftTightness`, rear wheel and visual root Transforms. Camera calls for its own player: `ShakeCamera`, `AddCameraZoom`, `RemoveCameraZoom`. On disable it raises Stopped for anything running. Logs `[CartFeedback]` per event (`Log Events` box).
- `CartFeedbackEvents` on the `Cart Feedback` root: the Inspector events (kept in Mercer's prefab so wiring never touches `Leading Cart Base`), `Hit Anchor` (moved to the hit point and turned along the hit direction before crash events), `Rear Left/Right Anchor` (follow `wheel_BL`/`wheel_BR` every LateUpdate with the `Cart Model` rotation).
- `PlayerScreenEffectMount` on `Cart Feedback/Screen Effects`: instantiates a UI prefab into the player's Effects layer of `ViewportCanvasSystem`; `Show/Hide/Instance`. Empty for now.
- Temporary: `CartFeedbackPlaceholder` (8 coloured primitives, one per event group) and `CartFeedbackPlaceholderSwitch` (master Show Placeholders; each placeholder also has Show). Drift placeholders sit on the outer tyre (left drift on the rear right).

## Prefabs

- `Assets/Prefabs/Cart Feedback/Cart Feedback.prefab`, nested once under the `Leading Cart Base` root (not under `Cart Model`: QuickOutline grabs child renderers there and the model yaws).
- `Leading Cart Base`: relay added; the old `VFXs/SpeedUp_Vfx` (Vefects ice trails, which also played the trail sound) and `CartSpeedupVFXController` removed so the speedup starts fresh. `CartSpeedupVFXController.cs` stays in the project, unused. Drift lines (`CartWheelTrackRenderer`) kept.

## Decisions

- Kept: Caused a crash (simple version), drift tightness, Cola boost events, zoom offset, single-player shake (runtime-added), screen-effects mount.
- Cut: Cut a chain / Chain was cut (stalled-chain hits fire no feedback event), post-processing effects (not per player; discuss first), controller rumble, hit-stop.
- Mercer's scripts: `Assets/Scripts/CartFeedback/`.

## Rules for any feedback

- It works in 2P and 4P, and in the tutorial scenes.
- It affects only the player it belongs to. No global time scale change.
- It stops cleanly when the cart or Round ends and starts no coroutine during teardown.

## Known

- Nothing calls `CameraManager.SetPlayerCartCount`, so the cart-count zoom is not driven; the base size (30 in 2P, 20 in 4P) stays constant.
- Shake and zoom act on the follow cameras only; not visible while a checkout or results camera is live.
- Mercer's art folder is still to agree with Liang.
