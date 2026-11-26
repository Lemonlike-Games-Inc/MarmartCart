# Cart feedback work (Mercer) — guide for Claude

Copy this whole file to `CLAUDE.local.md` in the Unity project root (next to `CLAUDE.md`). It adds to the project `CLAUDE.md`; it does not replace it. Everything named here exists in code on `cl_VGX_demo` as of 2026-11-26. If something here disagrees with the code, the code wins; tell Mercer.

## The task
You help Mercer (technical artist) build three feedbacks on each player's leading cart:
1. **Crash:** a hit splash VFX at the hit point, plus a screen effect such as camera shake.
2. **Speedup (Hype speedup, right trigger):** a new speedup VFX, plus screen effects such as zoom or speed lines. The old effect (`SpeedUp_Vfx` with Vefects ice trails, and its trail sound) was removed on purpose; speedup currently has no effect and no sound.
3. **Drift:** keep the drift lines (`CartWheelTrackRenderer` on `Leading Cart Base/VFXs/Drift_Vfx_TrailRenderer`); add sparkles on the rear tyres.

You build presentation only. The hooks already exist; you never edit them.

## Where you may work
- `Assets/Prefabs/Cart Feedback/Cart Feedback.prefab`: the only prefab you edit. It is nested once under the root of `Leading Cart Base`, so it is on every player's leading cart (P1 to P4), the tutorial scenes, and the results podium.
- New scripts: only in `Assets/Scripts/CartFeedback/` (create it). Small, presentation-only MonoBehaviours.
- New art (particles, materials, textures, shaders, Feel feedbacks, UI prefabs): in the art folder Mercer agreed with Liang. Not under `Assets/ImportedAssests/` (third-party and team-made imported art are treated as read-only).

## Do not edit
- Any existing script under `Assets/Scripts/`, including everything in `Assets/Scripts/Carts Related/Cart Feedback Hooks/` (the relay, `CartFeedbackEvents`, `PlayerScreenEffectMount`, the placeholder scripts), `LeadingCartBattleController`, `CartControlScript`, `CartDriftController`, `CameraManager`, `PlayerCameraShake`, `CameraZoomHandle`.
- `Leading Cart Base.prefab`, `Leading Cart P1-P4.prefab`, the `Chain of Carts` prefabs, and every scene.
- Third-party plugins (see project `CLAUDE.md`). You may use them (Feel, VFX All In 1, Shapes, Vefects...), not change them.
- Do not hand-edit prefab or scene YAML.

If a hook is missing, stop and tell Mercer the moment and the data you need. Liang adds it. Do not work around a missing hook by polling gameplay scripts, `Find` calls, or reflection.

## Structure

```
Chain of Carts P# (runtime player root, SnakeCartManager)
└─ Leading Cart P#(Clone)  (Leading Cart Base variant, spawned at runtime, tagged Player#)
   ├─ [LeadingCartFeedbackRelay]  on this root: C# events + camera calls
   ├─ Cart Model                  visual root; yaws and leans while drifting (Outline, CartMaterialManager)
   │  └─ Wheel Models/wheel_BL, wheel_BR   rear tyre models the anchors follow
   ├─ VFXs/Drift_Vfx_TrailRenderer          drift lines (keep)
   └─ Cart Feedback  (YOUR nested prefab)
      ├─ [CartFeedbackEvents]              Inspector events + anchors
      ├─ [CartFeedbackPlaceholderSwitch]   TEMPORARY master "Show Placeholders"
      ├─ Hit Anchor                        moved to the hit point before crash events
      │  ├─ Crash Placeholder              (white pillar, any crash)
      │  ├─ Crash Own Chain Placeholder    (orange)
      │  └─ Crash Rival Placeholder        (red)
      ├─ Rear Left Anchor                  follows wheel_BL
      │  └─ Drift Right Placeholder        (magenta; right drift = outer tyre is rear left)
      ├─ Rear Right Anchor                 follows wheel_BR
      │  └─ Drift Left Placeholder         (cyan; left drift = outer tyre is rear right)
      ├─ Caused Crash Placeholder          (green, above the cart that was hit)
      ├─ Speedup Placeholder               (yellow disc)
      ├─ Cola Boost Placeholder            (purple disc)
      └─ Screen Effects  [PlayerScreenEffectMount]  empty slot for a screen-space UI prefab
```

Never put renderers or particle systems under `Cart Model`: QuickOutline collects every child renderer there, and `CartMaterialManager` restyles the cart (ghost mode). Keep effects under `Cart Feedback`.

## Placeholders (temporary)
- `CartFeedbackPlaceholder` shows a coloured primitive when its event fires. Each has a **Show** box; the root's `CartFeedbackPlaceholderSwitch` has **Show Placeholders** (master). Visible only when both are ticked. Events still fire when hidden.
- When Mercer's effect for a moment is in, delete that placeholder object from `Cart Feedback`. Do not delete the placeholder scripts; Liang removes them when none are left.
- Toggling in Play Mode: the carts are runtime copies, so change the boxes on the runtime cart in the Hierarchy (`Chain of Carts P# / Leading Cart P#(Clone) / Cart Feedback`), not on the prefab asset.

## Path 1: no code (Inspector events)
`CartFeedbackEvents` on the `Cart Feedback` root has parameterless UnityEvents. Wire `ParticleSystem.Play`, `GameObject.SetActive`, a Feel `MMF_Player.PlayFeedbacks`, `PlayerScreenEffectMount.Show/Hide`, etc.

| Inspector event | Fires when |
|---|---|
| On Crashed | this player lost a battle (any cause) |
| On Crashed Into Own Chain | ...by hitting their own chain |
| On Crashed Into Rival | ...by hitting a rival's chain or leading cart |
| On Caused Crash | a rival crashed into this player's leading cart or chain |
| On Speedup Started / Stopped | Hype speedup begins / ends (once each) |
| On Cola Boost Started / Stopped | the Cola with Mentos item boost begins / ends |
| On Drift Started / Stopped | a drift begins / ends (any side) |
| On Drift Left Started / Stopped | left drift begins / ends; a side switch stops one side and starts the other |
| On Drift Right Started / Stopped | same for right |

Anchors (Transforms you can parent effects under):
- **Hit Anchor**: before the crash events fire, it is moved to `HitPoint` and rotated to look along `HitDirection` (forward = crash travel direction, up = world up). It is a child of the cart, so it moves with the knockback afterwards: use **world-space simulation** for hit particles.
- **Rear Left Anchor / Rear Right Anchor**: every LateUpdate, positioned on `wheel_BL` / `wheel_BR` and rotated like `Cart Model` (so they follow the drift yaw). Not drift-only; they are always there.
- Outer tyre in a drift: left drift → **Rear Right Anchor**; right drift → **Rear Left Anchor**.

## Path 2: scripts (data)
Get the relay once, in `Awake`, and subscribe in `OnEnable` / unsubscribe in `OnDisable`:

```csharp
relay = GetComponentInParent<LeadingCartFeedbackRelay>();
```

Use `GetComponentInParent` (the relay is on the cart root, so this works in any order). Do not read `CartFeedbackEvents.Relay` from another script's `Awake`/`OnEnable`; it may not be set yet.

### `LeadingCartFeedbackRelay` (on the leading cart root)
C# events (all raised once per change, never per frame):

| Event | Signature | Notes |
|---|---|---|
| `Crashed` | `Action<CartCrashInfo>` | this player crashed |
| `CausedCrash` | `Action<CartCrashInfo>` | a rival crashed into this player; `info.CrashedPlayerIndex` is the rival |
| `SpeedupStarted` / `SpeedupStopped` | `Action` | Hype speedup |
| `ColaBoostStarted` / `ColaBoostStopped` | `Action` | Cola with Mentos item boost (separate from Hype speedup; speedup is blocked while it runs) |
| `DriftStarted` | `Action<CartDriftSide>` | `Left` or `Right` |
| `DriftSideChanged` | `Action<CartDriftSide>` | new side, mid-drift |
| `DriftStopped` | `Action<CartDriftSide, bool>` | side that stopped, `interrupted` |

Read-only state: `PlayerIndex` (1-4; 0 = not a live player cart, e.g. the results podium), `IsSpeedingUp`, `IsColaBoostActive`, `IsDrifting`, `DriftSide` (`CartDriftSide.None/Left/Right`), `DriftTightness` (0 wide to 1 tight, 0 when not drifting; fine to read every frame while drifting), `CartVisualRoot`, `RearLeftWheel`, `RearRightWheel`.

Camera calls (always this cart's own player):
- `ShakeCamera(float amplitude, float duration)`
- `CameraZoomHandle AddCameraZoom(float sizeOffset)` (null outside a gameplay scene)
- `RemoveCameraZoom(CameraZoomHandle handle)`

### `CartCrashInfo` (struct)
| Field | Meaning |
|---|---|
| `Cause` | `CartCrashCause.OwnChain`, `RivalChain`, `RivalLeadingCart`, or `Other` (code-triggered, no contact data; unused today) |
| `CrashedPlayerIndex` | player who crashed (1-4) |
| `OtherPlayerIndex` | player whose cart or chain was hit; 0 for own chain / other |
| `HitPoint` | closest point on the hit collider to the crashing cart (approximate contact) |
| `HitDirection` | planar travel direction just before the hit (normalized) |
| `SpeedBeforeHit` | planar speed just before the hit; use it to scale the effect |
| `CartsLost` | follower carts the crasher lost (0 is common) |
| `IsIntoRival` | `RivalChain` or `RivalLeadingCart` |

### Timing facts
- A crash is resolved in one physics step: ghost mode starts, the whole chain detaches, the cart's velocity is zeroed and a knockback impulse is applied, then `Crashed` fires, then `CausedCrash` fires on the rival. Read speed from `SpeedBeforeHit`, not from the Rigidbody.
- If the cart was drifting, `DriftStopped(side, interrupted: true)` fires just **before** `Crashed` (the crash stops the wheels).
- After a crash the cart is a ghost for a few seconds and cannot crash again in that time.
- A head-on hit between two leading carts: both crash, and each also gets `CausedCrash` from the other.
- Hitting a rival's **stalled** (vulnerable) cart is not a crash: no event fires (it cuts the rival's chain; no hook for it yet).
- Speedup stops on: release, Hype empty, drift pressed (drift overrides speedup), Ice freeze, entering a checkout pit, Cola boost starting, cart disabled.
- `DriftStopped interrupted`: `false` = button released or drift cancelled by another system (stall, speedup, Cola boost, Ice freeze); `true` = interrupted by a crash, the final results, or the cart being disabled.
- On cart disable or teardown, the relay raises the Stopped events for anything still running, so looping effects always get a stop.

### Example: crash splash scaled by speed
```csharp
using UnityEngine;

public class CartCrashSplash : MonoBehaviour
{
    [SerializeField] private ParticleSystem splash;   // world-space simulation, under Hit Anchor
    [SerializeField] private float speedForFullSize = 25f;

    private LeadingCartFeedbackRelay relay;

    private void Awake() => relay = GetComponentInParent<LeadingCartFeedbackRelay>();

    private void OnEnable()  { if (relay != null) relay.Crashed += HandleCrashed; }
    private void OnDisable() { if (relay != null) relay.Crashed -= HandleCrashed; }

    private void HandleCrashed(CartCrashInfo info)
    {
        float strength = Mathf.Clamp01(info.SpeedBeforeHit / speedForFullSize);
        splash.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, strength);
        splash.Play();
        relay.ShakeCamera(Mathf.Lerp(0.3f, 0.8f, strength), 0.35f);
    }
}
```

## Camera
- Each player has one Cinemachine follow camera (orthographic, top-down) and one output Unity Camera with its own viewport. `CameraManager` owns them. Never write a camera's size, position or rotation, and never add Cinemachine noise or impulse components yourself.
- **Shake:** `relay.ShakeCamera(amplitude, duration)`. Amplitude is in world units, offsets the camera in its screen plane, and fades out quadratically over `duration`. A weaker shake never cuts a stronger one short; a stronger one replaces it. Only that player's view shakes. Tested values: 0.6 for 0.35 s.
- **Zoom:** `CameraZoomHandle h = relay.AddCameraZoom(0f);` then set `h.SizeOffset` (orthographic size; positive = zoom out) as often as you like, e.g. every frame from a curve; finish with `relay.RemoveCameraZoom(h)`. Several offsets add up, on top of the base size. Always remove the handle in `OnDisable`. `h.IsActive` turns false if the camera system was torn down.
- **Scale:** the base orthographic size is 30 in 2P and 20 in 4P (`MatchSceneModeProfile`). The same amplitude or offset looks about 1.5x stronger in 4P. To compensate, scale by `CameraManager.Instance.ConfiguredPlayerCount` (2 or 4) when `CameraManager.Instance` is not null. The cart-count zoom exists in the profile but is not driven today, so the base size is constant.
- Shake and zoom act on the follow camera only; while a checkout or results camera is live they are not visible.
- No post-processing effects yet: Volumes are not per player by default. Ask Liang before using one.

### Example: eased speedup zoom
```csharp
using UnityEngine;

public class CartSpeedupZoom : MonoBehaviour
{
    [SerializeField] private float zoomOut = 1.5f;
    [SerializeField] private float blendTime = 0.25f;

    private LeadingCartFeedbackRelay relay;
    private CameraZoomHandle zoom;
    private float target;

    private void Awake() => relay = GetComponentInParent<LeadingCartFeedbackRelay>();

    private void OnEnable()
    {
        if (relay == null) return;
        relay.SpeedupStarted += HandleStarted;
        relay.SpeedupStopped += HandleStopped;
    }

    private void OnDisable()
    {
        if (relay != null)
        {
            relay.SpeedupStarted -= HandleStarted;
            relay.SpeedupStopped -= HandleStopped;
            relay.RemoveCameraZoom(zoom);
        }
        zoom = null;
    }

    private void HandleStarted()
    {
        if (zoom == null) zoom = relay.AddCameraZoom(0f);
        target = zoomOut;
    }

    private void HandleStopped() => target = 0f;

    private void Update()
    {
        if (zoom == null) return;
        if (!zoom.IsActive) { zoom = null; return; }

        zoom.SizeOffset = Mathf.MoveTowards(zoom.SizeOffset, target, zoomOut / blendTime * Time.deltaTime);

        if (target == 0f && zoom.SizeOffset == 0f)
        {
            relay.RemoveCameraZoom(zoom);
            zoom = null;
        }
    }
}
```

## Screen-space effects (speed lines, hit flash, vignette)
- Read `Docs/Design/ui-architecture.md` first.
- Use `PlayerScreenEffectMount` (on `Cart Feedback/Screen Effects`): set **Screen Effect Prefab** to a uGUI prefab with a `RectTransform` root (usually stretched to fill). In `Start` the mount instantiates it into that player's **Effects** layer of the `ViewportCanvasSystem` (one Screen Space Overlay canvas, one clipped root per player, under the HUD). It starts hidden. Show it with `Show()` / `Hide()` (Inspector events work) or drive `Instance` (the spawned `RectTransform`) from a script. It is destroyed with the cart.
- Need more than one screen effect? Duplicate the `Screen Effects` child (one mount per effect).
- Layout: the canvas scales from 1920x1080 (CanvasScaler Expand). A 2P view is half the screen, a 4P view a quarter: anchor and stretch, never assume full screen.
- Text, if any, uses UniText per the UI doc. No Screen Space Camera canvases, no new canvases.

## Rules for every effect
- Works in 2P and 4P split-screen and in the tutorial scenes (`Assets/Scenes/Liang/Full Indie Summit 2026/`: Gameplay Scene 2P/4P, Gameplay Scene Tutorial 2P/4P).
- Affects only its own player: no global time scale (no hit-stop), no shared post-processing, no other player's view.
- Never changes gameplay: no forces, speed, colliders, triggers, layers on the cart, or input. No colliders on effect objects.
- Stops cleanly: stop loops on the Stopped events and in `OnDisable`. Start no coroutine in `OnDisable` or while the scene unloads.
- Performance: cache references; no per-frame `Find`, `GetComponent` or allocations. Reuse particle systems (`Play`/`Stop`, `Emit`) instead of instantiating per event. Up to 4 carts run every effect at once.
- Sound: there is no speedup sound now. If an effect plays audio, keep it on the effect and stop it with the effect.
- Use `UnityEngine.Random` explicitly.

## Testing
- Play Gameplay Scene 2P and 4P directly, or start from MainMenu. The Console logs every relay event as `[CartFeedback] P# ...` (Liang can switch this off).
- Check each event on the right player only, both drift sides, a side switch mid-drift, crash into own chain and into a rival, and that nothing stays on at match end or when returning to the menu.

## Git and reporting
- Work on your own branch created from `cl_VGX_demo`. Commit only when Mercer says so. Liang reviews the branch before it merges.
- After each change, list the files, prefabs and GameObjects you touched and what changed in each (component added/removed, fields changed and to what, assets created).
