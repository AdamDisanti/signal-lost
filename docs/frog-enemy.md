# Mortar frog prototype

Christian's second enemy type. It is a low-poly frog with a **tin can strapped to its back**. The can works as a mortar that lobs arcing globs of goo. The crab rushes in for melee; the frog keeps its distance and makes the player move.

## How it fights

| State | What it does |
| --- | --- |
| Idle | Sits still until the player comes within 18 m. It forgets the player if they go beyond 26 m for 4 s. |
| Engage | Hops to stay between 6 m and 15 m from the player: away when too close, closer when too far, and a side hop about every 3 s. Each hop is about 5 m, with a high arc, and checked against the NavMesh, so it never hops off the arena. |
| Load | The can rattles and glows for 1 s, then fires a glob in a high arc. A pulsing green ring shows where it will land. The ring pulses faster as the glob comes down. |
| Stunned | After a backfire (below), the frog is dazed for 2 s. |
| Dead | Flops over, then respawns in test scenes. |

**Goo:** 12 damage in a 1.5 m splash, and it leaves a puddle that fades over 3 s. Each shot aims slightly off (up to 0.4 m of scatter). Flight time grows with distance, 0.8 to 1.4 s, so long shots are easier to dodge. The cooldown between shots is 2.2 s.

**Counterplay, the tin can:** the can is its own small target. Hit it with a bolt or a melee swing **while it is loading** (rattling and glowing), and the shot backfires: goo bursts out over the frog, it takes 30 damage and is stunned for 2 s. That is the window to run in and finish it. Hitting the can at any other time does nothing to the can, and the frog body takes the normal hit.

Frog health is 60 (placeholder).

## Files

- `Scripts/Enemies/MortarFrog.cs`: the brain (states, hopping, aiming, backfire).
- `Scripts/Enemies/MortarFrogRig.cs`: procedural animation:
  - hops: arc, squash and stretch, leg kicks
  - loading: it rears back and aims the can up, squats, quivers, its throat swells, and the can rattles and glows
  - firing: the can kicks, and the frog hops off the ground with its legs flung out
  - backfire stun: goo splatters out, the frog slips onto its side dripping goo with its legs flailing and eyes spinning, then scrambles back up
  - hits on the can clang with sparks and make it wobble
  - hits make the frog lean away from the shooter and squeeze its eyes shut
  - also: blinking, throat breathing, and the death flop
- `Scripts/Enemies/GooBlob.cs`: the projectile and its landing ring.
- `Scripts/Enemies/GooPuddle.cs`: the fading puddle left behind.
- `Editor/FrogTestBuilder.cs`: the **Signal Lost > Create Frog Test Scene** and **Signal Lost > Regenerate Mortar Frog Prefab** menus. The test scene has two frogs.

## Tuning

All numbers are on the **Mortar Frog** component of `Prefabs/Enemies/MortarFrog.prefab`. If you change a default in code, regenerate the prefab or edit the prefab, because values already saved in the prefab win.
