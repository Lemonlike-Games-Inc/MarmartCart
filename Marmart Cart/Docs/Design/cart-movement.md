# Leading cart movement

Notes from reading the code on 2026-11-21, plus the outside-force rules agreed with Liang that day. The code is the source of truth.

## How it moves

- One Rigidbody on the `Leading Cart Base` root: 25 kg, no drag, no rotation locks, centre of mass computed from the low box collider. Physics runs at 50 steps a second.
- Four virtual wheels (`Virtual Wheels/VirtualWheel_FL/FR/BL/BR`, each a `LeadingCartBehaviour`). Every physics step each wheel raycasts down and applies, at its own position:
  - **Suspension:** spring 1000, damping 150, rest length 1.
  - **Grip:** cancels part of the wheel's sideways speed each step. The grip curve goes from 0.75 (no slide) to 0.5 (20 m/s slide); wheel mass 1 against the 25 kg body. A slide halves in about 0.1-0.15 s. There is no cap.
  - **Drive:** pushes only while forward speed is below the target speed. Up to 100 N per wheel times the torque curve (0.8 at rest, 1 from half speed), about 16 m/s² for the cart.
- **Steering:** `CartSteering` turns the two front wheel objects (±30°; while drifting, 5-30° from `CartDriftController`). Grip and drive use each wheel's own axes, so turned front wheels yaw the cart. Drift changes only that angle and the target speed, not grip.
- **Target speed:** base 20, +5 with Hype Speed Up, Cola's fixed speed, drift dip and fatigue, times the cargo overload multiplier (`CartMovementProfile`, `LeadingCartBehaviour.UpdateDriveModeAndTargetSpeed`).
- **Overspeed settle** (added 2026-11-21): above the target speed, the cart slows at the mode's `Overspeed Settle Deceleration` (Normal Drive 8 m/s², Speed Up 8, Drift 0 so drift feel is unchanged). Before this the drive never braked, so a cart kept Hype or Cola speed on a straight until it turned. It also makes overload slow the cart at once.
- Other code that sets the velocity directly:
  - A crash zeroes it, applies one knockback impulse, and stops the drive for the stop time (`LeadingCartBattleController`).
  - MoveBackward drives the body backward (`SnakeMoveBackwardController`).
  - Ice freezes the body completely (FreezeAll, position pinned).
- `LeadingCartBehaviour.Brake()` is never called. `WheelSuspensionScript` and the two drift Experiment scripts are not on the leading cart prefab.

## Outside forces

`LeadingCartExternalForces` on the leading cart root is the one entry point. A source calls `TryAddAcceleration` from FixedUpdate for each step it acts. The component:

- flattens the acceleration and caps the total per step from all sources (60 m/s²);
- removes any forward part that would push the cart past its current target speed, so normal top speed stays the same for everyone;
- never pushes the cart backward: the backward part only slows it down to `Minimum Forward Speed` (3 m/s; 0 allows a stop but still no reverse). Added 2026-11-22, see "Why never backward";
- applies it at the centre of mass (mass independent), so it slides the cart without rolling or spinning it;
- refuses carts in ghost mode, in a checkout pit, or frozen.

What a force does to a driving cart:

- **Sideways:** a slide of about a/6 m/s at 50 Hz (30 m/s² gives about 5 m/s). Grip straightens it once the force stops.
- **Backward:** slows the cart down to the minimum forward speed, never further. Slowing a cart for a while (a slow effect) still belongs on target speed (a multiplier with add/remove handles in `LeadingCartBehaviour`), not on forces. Not built yet.
- **Forward:** limited to the target speed, as above.

### Why never backward

The chain follows a path drawn by a trailing arm, not by the leader itself:

- `PhysicalChainJointProbe`: the leader's rear hitch drags a free hinge (no swing limit) with a 1.5 m arm. The probe at its tip is the path source.
- `SnakePathHistory`: the tip adds a point every 0.1 m it moves, in any direction. Path progress only grows.
- `SnakeCartManager`: each follower is placed kinematically at a fixed distance behind the tip along that path (1 m, then 1.5 m apart).
- `LeadingCartBattleController`: the leader's front trigger touching any of its own non-vulnerable chain carts is a crash.

The engine only drives forward, so normal driving keeps the arm behind the cart. MoveBackward rewinds the path instead and turns off battle detection. An outside force strong enough to drive the cart backward pushed the hitch into its own arm. The hinge jackknifed, the tip's swing was recorded as new path around the cart, and the first followers ran through the leader's front trigger. Seen with the Magnet at 100 m/s² on 2026-11-21.

## Magnet pull (current design, playtested 2026-11-22)

Loose carts and rival leading carts use the same pull (`MagnetPullEffectSystem.ComputePullAcceleration`): accelerate toward the thrower until the closing speed reaches the pull speed, never overshooting in one step.

- Loose carts: 35 m/s, 200 m/s², up to 1.5 s. Their sideways drift is also cancelled so they home in.
- Leading carts: Liang's values 16 m/s, 100 m/s² (the 60 m/s² step cap in `LeadingCartExternalForces` applies), 0.5 s. Their own grip and steering keep working, so a player can fight the pull. A pull from behind only slows them to the minimum forward speed.
- A pulled rival that hits the thrower's chain or leading cart crashes by the normal rule.

## Open

- Whether players can react to the leading-cart pull, and how much driving against it helps. Playtest, then tune on `Magnet Pull Effect`.
- Whether drift should also settle above its target (drift settle is 0 now).
- Whether 3 m/s is the right crawl for a cart pulled from behind, and whether strong sideways slides can still fold the chain. If self-crashes remain, the fallback is a short own-chain crash grace while a cart is being pushed.
