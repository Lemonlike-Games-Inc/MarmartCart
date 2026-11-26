# Cart feedback: guide for Mercer's Claude (draft)

Draft of 2026-11-25. The names marked (proposed) are replaced with the real ones once the hooks are built. Paste the block below into your Claude session, or save it as `CLAUDE.local.md` in the project root. It adds to the project `CLAUDE.md`; it does not replace it.

```markdown
# Cart feedback work (Mercer)

You are helping Mercer, the technical artist, improve three feedbacks on the leading cart:
1. Crash: hit splash VFX and a screen effect such as camera shake.
2. Speedup: a new speedup VFX, plus screen effects such as zoom or speed lines.
3. Drift: keep the line effect (`CartWheelTrackRenderer`), add sparkles on the rear tires.

## Where you work
- The `Cart Feedback` prefab (proposed), nested under `Leading Cart Base`. Put every effect here.
- New particle systems, materials, textures, shaders and Feel feedbacks, in Mercer's own folders.
- New scripts only in the cart feedback script folder (proposed). Keep them small and presentation only.

## How effects are triggered
- `LeadingCartFeedbackRelay` (proposed) on `Leading Cart Base` raises every moment you need: crashed (with cause, hit point, direction, other player), speedup started and stopped, drift started and stopped (with side and rear-wheel anchors).
- Wire effects to its Inspector events. For data, subscribe to its C# events from your own script.
- Camera zoom and shake go through the per-player camera calls (proposed). Never write a camera's size or position directly.
- Screen-space effects such as speed lines go in the player's Effects layer of the Viewport Canvas System.

## Do not edit
- Any existing script under `Assets/Scripts/`. This includes `LeadingCartBattleController`, `CartControlScript`, `CartDriftController`, `LeadingCartBehaviour`, `SnakeCartManager`, `ChainedCartManager`, `CameraManager` and the relay itself.
- `Leading Cart Base.prefab`, the `Chain of Carts` prefabs, and the gameplay and tutorial scenes.
- Third-party plugins, as the project `CLAUDE.md` says.

## If a hook is missing
Stop and tell Mercer what you need: the moment, and the data. Mercer asks Liang, and the hook is added on Liang's side. Do not add it yourself, and do not work around it by polling or by `Find` calls.

## Rules for every effect
- It works in 2P and 4P split-screen, and in the tutorial scenes.
- It affects only the player it belongs to. No global time scale change (hit-stop), no effect on another player's view.
- It never changes gameplay: no forces, no speed, no colliders, no input.
- It stops cleanly when the Round ends. Start no coroutine while the scene unloads.
- No per-frame `Find`, `GetComponent` or allocation. Pool or reuse particle objects.

## Git
- Work on your own branch, created from `cl_VGX_demo`.
- Commit only when Mercer says so. Liang reviews the branch before it merges.
- After each change, list the files and prefabs you touched and what changed in each.
```
