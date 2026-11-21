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

## Variants

A variant is its own full Item asset that reuses the base Item's parts by pointing to the same assets. No inheritance or override machinery.

- Rolling Tomato: the Tomato throw and Tomato blind assets, plus a roll effect.
- Really Really Cold Ice Cube: the Ice Cube throw and freeze assets, plus its own ice cube effect (lasts the Round, capped).

## Passive

An adjective Sample lists an Item replacement, for example Tomato -> Rolling Tomato. The hand applies it to this player's map pickups.

## The hand

- Two slots: map (filled by Powerups) and ability (filled by the Ability charge).
- The Item that became ready last is equipped. When one slot is empty, the other is equipped. LB swaps when both are loaded.
- LT uses the equipped Item. An Item that needs no aim fires at once on LT.
- Blockers live here. Their writers (`PowerupRuntimeSystem`, `CartPitZone`, `IceFreezeEffectSystem`, `MatchResultsPresentationController`) and readers (`PowerupCartTarget`, `ColaMentosEffectSystem`) move to it.
- Consumption rule stays: the system that runs the Item spends it only after its work succeeded.

## Refactor steps (each keeps today's behaviour)

Each step is tested with map Items in Gameplay Scene 2P, 4P and the tutorial scenes before the next one starts.

1. **R1, the hand.** `PlayerItemHand` takes over LT, aim permission, the blockers and the use check. The map slot stays in `PlayerPowerupController`.
2. **R2, the Item library.** Item, use and effect assets for Tomato, Ice Cube and Cola, built from today's profiles (values copied by an editor script, with Liang's OK first). Projectiles carry the Item; effect systems check their own effect. The map side still uses `PowerupId` through a lookup asset.
3. **R3, map side on Items.** Pickups, random pools, the pickup visual catalog and spawners use Items; `PowerupId` is retired.

## Risks

- R2 and R3 touch most tested powerup code and serialized assets. Missing a field rename can null a reference; use `[FormerlySerializedAs]` and check every build scene after each step.
- The content freeze is around 2026-12-02. R1-R3 come before the Ability steps.
