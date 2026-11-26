# Cart feedback: quick guide for Mercer

Short version for you. Give your Claude the detailed version: `cart-feedback-guide-for-mercer-claude.md` (save its block as `CLAUDE.local.md` in the project root).

## Your job
- **Crash:** hit splash, plus a screen effect such as camera shake.
- **Speedup:** a new speedup effect, plus zoom or speed lines. The old ice trail and its sound are gone; speedup is silent until you add one.
- **Drift:** keep the existing lines, add sparkles on the rear tyres.

## Where you work
- Open `Assets/Prefabs/Cart Feedback/Cart Feedback.prefab`. Every effect goes in here.
- It sits on every player's cart, so one change covers P1 to P4.
- Your scripts go in `Assets/Scripts/CartFeedback/`. Your art goes in the folder you agree with Liang.

## What is already in the prefab
- **Hit Anchor:** jumps to the hit point and faces the crash direction, just before a crash event.
- **Rear Left Anchor / Rear Right Anchor:** follow the rear tyres all the time.
- **Screen Effects:** a slot for a screen-space effect (UI) that shows only in that player's view.
- **Coloured placeholders:** proof that each event fires. Untick **Show Placeholders** on the root to hide all of them, or **Show** on one to hide just that one. Delete a placeholder when your effect replaces it.

## How to trigger an effect without code
- Select the `Cart Feedback` root, find **Cart Feedback Events**, and add your effect to an event.
- Crash: On Crashed, On Crashed Into Own Chain, On Crashed Into Rival.
- Hit someone else: On Caused Crash (the player whose cart or chain was hit).
- Speedup: On Speedup Started / Stopped. Cola item boost: On Cola Boost Started / Stopped.
- Drift: On Drift Started / Stopped, or per side: On Drift Left / Right Started / Stopped.
- In a left drift the back of the cart slides out to the right, so the outer tyre is the **rear right**. The placeholders already work this way.

## Camera and screen
- Shake and zoom go through the cart's feedback relay, and only affect that player's view. Your Claude knows the calls.
- Never move or resize a camera directly.

## Rules
- Test in 2P and 4P, and in a tutorial scene.
- Affect only your own player's view. No slow motion or freeze (it would freeze everyone).
- Effects never change gameplay: no forces, speed, colliders or input.
- Effects must switch off cleanly when the cart or match ends.
- Do not edit: `Leading Cart Base`, the `Chain of Carts` prefabs, the scenes, or any existing script.

## Missing something?
- Tell Liang which moment and which data you need. He adds the hook; you don't.

## Git
- Your own branch from `cl_VGX_demo`. Commit when you decide. Liang reviews before it merges.
