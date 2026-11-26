# Cart feedback hooks

Agreed with Liang on 2026-11-25, except the items marked Proposed. Design only; no code exists yet. Class, prefab and folder names are proposals.

## Goal

Mercer, working with his own Claude, improves three feedbacks: the crash, the speedup and the drift. He should not need to edit the core cart scripts. We build the hooks and wired placeholders first; he designs the look on top of them.

## What exists today (read on 2026-11-25)

- **Crash:** `LeadingCartBattleController.LoseBattle()` raises `OnBattleLost` with no data. `OnVulnerableCartHit(cart, detachedCount)` is raised for the attacker who cuts a stalled chain.
- **Speedup:** `CartControlScript.OnSpeedupHeld(bool)` is raised every frame, not on change. `CartSpeedupVFXController` uses it to switch one object on and off. Speedup stops by itself when Hype runs out.
- **Drift:** `CartDriftController.IsDrifting` is polled. There is no start or stop event. `CartWheelTrackRenderer` draws the drift lines and stays.
- **Camera:** `CameraManager.ApplyPlayerLens` writes each player camera's orthographic size from the follower cart count. There is no shake.

## The relay

One component on `Leading Cart Base` (proposed name `LeadingCartFeedbackRelay`). It only reports; it never changes gameplay. It offers Inspector events for wiring without code, and C# events with data for scripts.

### Crash

- **Crashed**, for the player who lost. Data:
  - Cause: own chain, rival chain, rival leading cart, or other.
  - The other player's index, or 0 when there is none.
  - Hit point and hit direction.
  - Speed just before the hit.
  - Number of carts lost.
- **Inspector events per cause**, with no parameter: Crashed, Crashed Into Own Chain, Crashed Into Rival.
- **Hit point anchor:** before the events fire, the relay moves a child Transform to the hit point and turns it along the hit direction. An effect placed under it plays at the right spot with no code.
- Proposed: **Caused a crash**, for the rival whose cart or chain was hit, with the same data.
- Proposed: **Cut a chain** for the attacker and **Chain was cut** for the defender, with the number of carts.

### Speedup

- **Speedup started** and **Speedup stopped**, raised once each. There is no intensity.
- Open: whether the Cola with Mentos boost shows the same effect or its own.

### Drift

- **Drift started**, with the side, and **Drift stopped**.
- Anchors for the rear-left and rear-right wheels.
- Proposed: a drift tightness value from 0 to 1, read while drifting.

## Mercer's prefab

- A nested prefab (proposed `Cart Feedback`) sits once under `Leading Cart Base`. Mercer owns it and edits only it, so `Leading Cart Base.prefab` and the scenes stay out of his changes.
- It ships with empty placeholders already wired to the relay: Crash, Speedup, Drift Left, Drift Right.
- The old speedup object and `CartSpeedupVFXController` are retired once his speedup effect is in.

## Camera (Proposed)

The camera is simple, but two write points are shared, so we add those and Mercer builds on them:

- **Zoom offset:** `CameraManager` gets an add/remove handle for a per-player zoom offset, added to the cart-count size. Without it, a zoom effect and the cart-count zoom overwrite each other.
- **Shake:** one call that shakes one player's camera only. It needs a per-player setup on the Cinemachine cameras in the gameplay and tutorial scenes.
- **Screen-space effects** such as speed lines go in the player's Effects layer of the `ViewportCanvasSystem`, through one slot per player.
- **Post-processing effects** such as a lens stretch are not per player by default. Discuss before building one.

Mercer owns everything above these entries: curves, shake strength, timing, art.

## Rules for any feedback

- It works in 2P and 4P, and in the tutorial scenes.
- It affects only the player it belongs to. No global time scale change (hit-stop): it would freeze every player.
- It stops cleanly when the Round ends and starts no coroutine during teardown.

## Build order

Each step ends with a stop for Liang to test.

1. Read the code and propose. Find every caller of `LoseBattle` and every user of `OnSpeedupHeld`.
2. The relay with crash, speedup and drift events, and the `Cart Feedback` prefab with placeholders.
3. The camera entries.
4. The guide for Mercer with the real names. Merge to `cl_VGX_demo`; Mercer branches from there.

## Open

- Who builds the camera entries.
- Where Mercer's own scripts live.
- Controller rumble for a crash.
