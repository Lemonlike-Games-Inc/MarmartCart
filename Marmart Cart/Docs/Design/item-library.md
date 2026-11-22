# Item library and the hand

Agreed with Liang on 2026-11-21. Code plan only; nothing is built yet. Names below are proposals until the code exists; the code is the source of truth.

## Why

Today one id (`PowerupId`) means both "what the player holds" and "what the map hands out", and the data for each item is spread across lookups keyed by it (prefab, shot pattern and pool size in `PowerupProjectileProfile`, preview radius in `PowerupGameplayProfile`, icons in `PowerupHudProfile`, and an id check in every effect system). The blockers (frozen, checkout, match inactive, results) sit on `PlayerPowerupController`, although they describe the player, not the map slot.

Ability Samples are a second way to get something to use. They must not reach into the map powerup code. Both sources should point at one shared library instead.

## Terms

- **Item:** what a player holds and uses: Tomato, Ice Cube, Cola, Rolling Tomato, Really Really Cold Ice Cube, U-shape Magnet.
- **Powerup:** a map pickup that gives an Item. Map side only: spawner, pickup, random pool.
- **Ability Sample:** picked between Rounds; gives an Item on a charge. Meta side only: offer, pick, charge, passive.
- **Hand:** per player. Which Item is equipped, the swap, LT, aim permission and the blockers.

"Product" is not a code term.

## Layers

```
Item library (shared)   what a player can hold and use
  Item assets:          Tomato, Ice Cube, Cola, Rolling Tomato, Really Really Cold Ice Cube, U-shape Magnet
  Item systems (scene): throw and projectile pool, Tomato blind, Ice freeze, ice cubes, Cola boost, roll, magnet

Sources                 how a player gets an Item
  Powerups (map):       spawner, pickup, random pool  -> map slot
  Ability Samples:      offer, pick, charge           -> ability slot; passive = Item replacement

Hand (per player)       equipped Item, swap, LT, aim permission, blockers
```

The two sources never talk to each other.

## Item asset

```csharp
public class ItemDefinition : ScriptableObject
{
    public string displayName;
    public ItemUseDefinition use;               // how it is used: Throw (arc aim), Instant (Cola), Cone (Magnet)
    public List<ItemEffectDefinition> effects;  // what it does: blind on hit, freeze on hit, ice cube on land, roll on land...
}
```

- Use and effect assets hold definitions only, never runtime state.
- Each scene system handles only the Items that carry its effect (`item.TryGetEffect(out TomatoBlindEffect blind)`). No switch over Items.
- A new behaviour is a new effect type plus its system; nothing shared is edited.
- Cone use (`ConeItemUse`, U-shape Magnet): the targeting controller publishes a cone aim state (Cone Length > 0) that the aim renderer draws as a flat cone; the effect system validates and acts on that published state, so the cone shown is the cone used.
- Self-hit: an effect can say it may land on its thrower (`ItemEffectDefinition.HitsThrower`; the Tomato blind's Blinds Thrower toggle). The projectile then ignores the thrower on the way up only, and can hit them on the way down and while rolling. Ice cubes on the ground already hit everyone.

## Variants

A variant is its own full Item asset that reuses the base Item's parts by pointing to the same assets. No inheritance or override machinery.

- Rolling Tomato: the Tomato blind asset and a roll effect (`ProjectileRollEffect`), plus its own throw: a copy of the Tomato throw whose tomatoes spin forward. The launcher passes the roll values (distance, direction, hit radius and the layers that stop it) to the projectile, which rolls after landing; the blind, splat and sound run where it stops. The projectile pool cancels rolling items at match end.
- Really Really Cold Ice Cube: the Ice Cube throw, plus its own Ice Cube effect (a copy with Stays Until Triggered on and Maximum Cubes Per Player 0 = no cap). It uses the same cube prefab; a second look needs the ice cube pool keyed by prefab.

## Passive

An adjective Sample lists an Item replacement, for example Tomato -> Rolling Tomato. The hand applies it to this player's map pickups.

- Built in Ability Step 5: the Sample's `Upgrades Map Item` names the base item. `PlayerAbilityController` adds the replacement to the hand (`AddMapItemReplacement`, which returns a handle) and removes it when the Sample changes or the controller is disabled.
- The pickup still reports the base item; the hand loads the variant, so the held-item HUD needs an entry for the variant.
- SFX relays filter by item. A variant that should sound like its base item goes in the base relay's Also Accept Items; one that needs other sounds gets its own relay.

## The hand

- `PlayerItemHand` is the former `PlayerPowerupController`, renamed in R1 (same script GUID, so the component on `Chain of Carts Base` kept its values). It was already the per-player handle that every effect, blocker and the player registry used.
- The hand owns both slots (`HandSlot`); sources only load them.
- Two slots: map (filled by Powerups) and ability (filled by the Ability charge).
- The Item that became ready last is equipped. When one slot is empty, the other is equipped. LB swaps when both are loaded.
- LT uses the equipped Item. An Item that needs no aim fires at once on LT.
- Blockers live here. Their writers (`PowerupRuntimeSystem`, `CartPitZone`, `IceFreezeEffectSystem`, `MatchResultsPresentationController`) and readers (`PowerupCartTarget`, `ColaMentosEffectSystem`) move to it.
- Consumption rule stays: the system that runs the Item spends it only after its work succeeded.

## Duration effects (noted 2026-11-21)

With two slots a player can use a second item while a duration effect is still running, so each duration effect decides whether it blocks that.

- Every duration effect has its own toggle: "blocks other item use while active". It lives on the effect asset, so a variant can differ from its base item.
- Cola Boost already has it (`Block Other Powerup Use While Active` on `ColaMentosPowerupProfile.asset`, now `ColaBoostEffect`). It is on today; Liang wants Cola not to block, so it is turned off in R2b.
- Today the toggle sets the single `ActiveEffectLock` blocker. Once a second duration effect can lock, the lock needs one handle per running effect, so the end of one effect never clears another's lock.
- Watch, design later: effects that change the same thing at once, such as two speed effects (Cola with Customer in Training).

## Refactor steps (each keeps today's behaviour)

Each step is tested with map Items in Gameplay Scene 2P, 4P and the tutorial scenes before the next one starts.

1. **R1, the hand.** `PlayerPowerupController` becomes `PlayerItemHand` (LT, aim permission, blockers, use check, player registration). Its stored item becomes the hand's map slot; pickups load it, and item systems read and consume the equipped item. `PowerupId`, `PowerupUseBlockReason`, `PowerupRuntimeSystem`, the use event names and serialized field names stay until R2/R3.
2. **R2, the Item library.**
   - R2a (done): Item types; the three effect profiles renamed `TomatoBlindEffect`, `IceCubeEffect`, `ColaBoostEffect`; Item assets in `SO_Variables/Items` with values copied from today's profiles and checked identical.
   - R2b (done): the hand, targeting, launcher, projectile pool, projectiles and effect systems use Items; each effect system checks its own effect and takes its tuning from it. `PowerupRuntimeSystem` holds the Item Library (assigned in the four build scenes). Cola's block toggle is off. The map side, held-item HUD icons and the SFX relay still use `PowerupId` through the Item Library. The ice cube pool keeps its own `IceCubeEffect` reference for pool sizes and the cube prefab; a second cube look (Really Really Cold) will need the pool to key by prefab.
3. **R3, map side on Items.** Pickups, random pools, the pickup visual catalog and spawners use Items; `PowerupId` is retired.
   - Done: the spawner prefab and its Tomato/Cola variants hold their Fixed Item; the random pool, the pickup visual catalog (one visual per item plus a Random Pickup Visual), the held-item HUD profile (icon and prompts per item) and the SFX relays (filter by exact item) use Items. The Item Library has no `PowerupId` mapping any more. StrongFan had no item; its HUD icon and prompts were dropped. `LevelGreyBoxScene` (test scene) was not migrated.
   - Not done, for after the event: renaming `PowerupRuntimeSystem` and the shared throw profiles, and moving the effect assets into `SO_Variables/Items`.

## Risks

- R2 and R3 touch most tested powerup code and serialized assets. Missing a field rename can null a reference; use `[FormerlySerializedAs]` and check every build scene after each step.
- The content freeze is around 2026-12-02. R1-R3 come before the Ability steps.
