# Enemy and combat rules

These are the game-wide baseline from the 10-22-26 meeting notes. Every enemy follows them unless its own doc says otherwise.

## Fighting radius

Every enemy AI owns a `Territory` (a serialized field named `territory`).

- **Attack:** it attacks only while the player is inside its fight radius. Outside the radius it ignores them, even in plain sight. Being hit still provokes it.
- **Chase:** once fighting, it chases until the player is **2×** the radius from the centre (`Territory.LeashMultiplier`).
- **Give up:** it then walks home and **heals to full**, so the player cannot drag it across the map or chip it down with hit-and-run.
- **Centre:** where the enemy started, or a shared `CombatZone`. A group in one zone, such as the merchant crab and its cousins, engages and gives up together. Spawners hand their zone to what they spawn.

| Enemy | Radius |
| --- | --- |
| Hermit crab | 12 m |
| Desert fodder | 14 m |
| Mortar frog | 18 m |
| Merchant hollow (shared zone) | 14 m |

Desert mini-bosses use their arena radius. They reset (full health, phase 1, back home) once the player is twice that far from home.

Gizmos: the selected enemy or zone draws its fight radius and a fainter ring at the leash distance.

## Stuns

- Only the **third hit of the laser combo** stuns (`IStunnable`), for **1 s**. Ordinary hits knock back but never stagger, which ended the crab stun-lock.
- Everything except bosses can be stunned. Desert mini-bosses and the merchant crab cannot.
- Enemies' own stuns (frog backfire, eel yanked out, Burnout crashing) are separate and unchanged.

## Mining laser

- **Combo:** three hits, then a 2 s recharge. The HUD's three combo pips drain right to left as its timer.
- **Charged bolt:** 10-70 damage. The cooldown scales with the charge, from 1 s for a tap (10% or less) to 6 s at full charge, eased so part charges stay quick (about 2.8 s at half charge). Tuned 10-23.
- **Full-charge burst:** a full charge bursts on impact (`AreaBlast`). It hits everything within 4 m, at full damage in the centre and half at the edge, with a small shove. It is meant for groups. A shot reflected off a crab's shell loses the burst.

These values are set on the shared Player prefab (`Prefabs/Core/Player.prefab`, built by `CorePrefabBuilder`).

## Verification

- [CombatRulesSmoke.cs](../tools/unity/CombatRulesSmoke.cs) passes. It covers:
  - the radius, the leash and the heal on return;
  - the stun rule;
  - the mini-boss arena and reset;
  - the bolt cooldown curve;
  - the burst (pack damage, half at the edge, never the player).
- [TutorialStorySmoke.cs](../tools/unity/TutorialStorySmoke.cs) covers the shared-zone engagement.
