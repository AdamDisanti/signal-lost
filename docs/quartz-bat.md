# Quartz Bat (Crystal Caves fodder)

A small flying enemy of the [Crystal Caves](crystal-caves.md), from Amanda's 11-11 request. It's a cave bat with translucent crystal wings and a glowing quartz crystal in its chest. It circles the player's head and swoops down to bite. It comes in packs: each bat circles on its own ring, and only two can dive at a time, so a pack takes turns. Shots only hurt it when they hit the chest crystal. Melee hurts it anywhere, but it's only low enough to reach while it swoops.

It lives only in its own test scene for now. It is **not placed in the Crystal Caves level**; that waits until Amanda asks.

The model is generated in code, in the same low-poly style as the other enemies, adapted from Amanda's reference image: a dusky violet body, big pointed ears, black eyes, a pink nose and an ochre belly. The chest crystal and the wing panes are new. No assets were imported. All numbers are placeholders to tune.

## Open it

Open `Assets/Scenes/Enemies/QuartzBatTest.unity` and press Play.

The test cave is the same walled, roofed room as the chameleon's. It uses the level's cave materials, crystal batches along the walls and the cave's inside lighting. You start at the south end with the shared **Player** and **GameSystems**. Three bats wait further in. Walk toward them; they engage once you're inside their 14 m territory. To see the turn-taking, select a bat in Play Mode and set **Max Divers** to 1: only one bat then dives at a time. At 4, all three can dive together. They don't respawn, so restart Play to fight them again.

Menu **Signal Lost > Crystal Caves Enemies > Quartz Bat**:

| Menu item | What it does |
| --- | --- |
| **Build Prefab** | Rebuilds `Prefabs/Enemies/Caves/QuartzBat.prefab` from the script (same GUID). Edits made to the prefab are lost. |
| **Create Test Scene** | Creates the test scene if it doesn't exist (it never overwrites one) and validates it. |
| **Validate** | Checks the prefab's wiring and the test scene. |

## How it behaves

| State | What happens |
| --- | --- |
| **Hover** | Flaps in place 3.3 m up until the player enters its territory. A hit also draws it in. Once it's fighting it never goes back to hovering mid-fight: it keeps attacking until the player is past the 28 m leash, then flies home and heals. |
| **Circle** | Circles the player about 3.6 m out and 3.3 m up, at a steady 65°/s, always facing them. Each bat gets its own ring at spawn (up to 0.6 m nearer or farther and 0.4 m higher or lower), so a pack doesn't stack. It picks a direction (clockwise or anticlockwise) at spawn and keeps it. Up there it's out of melee reach. |
| **Wind-up (0.6 s), the warning** | Every 3.5 s it stops, raises its wings, opens its mouth and its crystal pulses brighter. **Taking turns:** at most 2 bats can be winding up, swooping or hanging low at once (**Max Divers**). The others keep circling until one finishes. |
| **Swoop** | Dives at the player's head (7.5 m/s), dropping to head height as it closes. If its mouth gets within 0.9 m of the head, it bites for 8 damage. It gives up after 1.4 s. |
| **Hang (1 s), the melee window** | Hovers low (about 1.2 m up), drifting back a little. This is the time to swing at it. |
| **Climb** | Rises back to 3.3 m and starts circling again. |
| **Death** | It drops out of the air, tumbling, then flops over. The crystal goes dark and the glow goes out. Drops 1–2 scrap metal and 0–1 wiring, like the other cave fodder. |

- **Health:** 14. Lowered from 30 so the Caves' fodder sits between the desert's and the Mudlands' by rank: the swarm slot, between the Spark Tick (8) and the Mudpuppy (20).
- **Pack rules (swarm):** each bat still runs its own AI, with no flocking. Two things keep a pack from stacking:
  - **Separate rings:** at spawn each bat picks its own circle radius (**Radius Spread**, 0.6 m) and flying height (**Height Spread**, 0.4 m) within that much of the defaults. The lowest ring is 2.9 m up, still above the laser's 2.1 m reach.
  - **Taking turns:** a shared count allows at most **Max Divers** (default 2, range 1-4) bats to be winding up, swooping or hanging low at once. A bat whose swoop timer is up keeps circling until a turn frees. A dead or disabled bat frees its turn at once. `QuartzBat.DivingCount` exposes the count.
- **Shooting:** only bolts that hit the chest crystal do damage. Bolts that hit its wings or body glance off. A full-charge burst nearby doesn't hurt it either.
- **Melee:** swings hurt it anywhere, but only reach it while it swoops or hangs low.
- **Stun:** the laser's combo finisher stuns it, like all fodder ([enemy rules](enemy-rules.md)). It spins in place in the air.
- **Leash:** a 14 m territory with the shared leash and heal rule, like all fodder. Once it engages, it follows the player out of that 14 m area and only gives up past 28 m (fixed 11-11: it used to freeze in the air 4 s after the player left the 14 m area).
- **Movement:** its NavMeshAgent walks the ground under it, which keeps it inside cave walls; the model flies above that point. It needs a NavMesh like the other fodder.

**Counterplay:** shoot the crystal while it circles, since its chest always faces you. Or wait for the wind-up, step aside or stand your ground, and swing while it hangs low after the swoop.

## Placing it later

1. Drop the prefab on cave floor with a NavMesh, away from low ceilings (it flies 3.3 m up, and as low as 2.9 m for a bat with a low ring).
2. Place two to four together for a pack. Bats in the level share one dive count, so only two dive at a time wherever they are.
3. Nothing else to set up. It finds the player itself.
4. **Max Divers**, **Radius Spread** and **Height Spread** aren't saved in the prefab yet, so bats use the defaults from the code. They are saved the next time the prefab is rebuilt or saved in Unity.

## Verification

- **Build and validate:** `QuartzBatBuilder.CreateTestScene` was run in batch mode in a disposable copy of the project, then **Validate**. Both passed.
- **Play Mode:** the scratch-only [QuartzBatSmoke.cs](../tools/unity/QuartzBatSmoke.cs) passed. It checks:
  - the three bats fly;
  - they circle high and swoop low (1.2–3.3 m);
  - a swoop bites the player;
  - with the player moved outside its 14 m home area (but inside the leash), it keeps swooping (twice in about 11 s). The old version failed this check;
  - the player's non-melee, non-crystal damage is refused;
  - a bolt into a wing glances off, and a bolt into the crystal hurts;
  - a dead bat drops scrap;
  - no more than two bats are ever diving at once (added with the pack rules).
- **Not re-run after the 11-13 changes:** the 14 HP change and the pack rules (separate rings, Max Divers) were made without opening Unity, so the smoke test, **Validate** and a hand playtest still need to be run. One check could fail by chance: it expects some bat to circle above 3 m, and all three bats rolling a ring below 3 m (about a 0.2% chance) would trip it.
- To repeat it, copy the harness into `Assets/Editor` of a disposable project under the system temp directory and run Unity with `-batchmode -nographics -projectPath <temp-project> -executeMethod QuartzBatSmoke.Start -logFile <log>`, without `-quit`.
- **Not checked by hand:** how it feels to fight with real mouse and keyboard input. The swoop timing, circle speed and crystal size all need a playtest.

## Changes to shared code

None. It reuses `DesertFodder`, `IBoltReflector`, `ScrapDropper`, `EnemySignalSource` and `CaveEnemyKit` as they are.
