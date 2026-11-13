# Crystal Chameleon (Crystal Caves fodder)

The small enemy of the [Crystal Caves](crystal-caves.md), from Amanda's 11-09 request. It's a crawling chameleon whose back is a cluster of the cave's own crystals. While camouflaged it lies buried with only the crystals showing, so it looks exactly like any other crystal batch. The scanner's **Crystal Disruptor** upgrade reveals it: its crystals turn red, it rises out of the ground and it attacks with its tongue. Its crystals stay red while it fights and go dark when it dies.

It lives only in its own test scene for now. It is **not placed in the Crystal Caves level**; that waits until Amanda asks.

The model is generated in code, in the same low-poly style as the other enemies. No assets were imported. All numbers are placeholders to tune.

## Open it

Open `Assets/Scenes/Enemies/CrystalChameleonTest.unity` and press Play.

The test cave is a walled, roofed room. It uses the level's own cave floor, wall and ceiling materials, real crystal batches along the walls, and the cave's inside lighting. You start at the south end with the shared **Player** (laser, scanner, flask) and **GameSystems** (HUD, dialogue, pause and settings menus), the same setup as the Crystal Caves level.

Five chameleons hide in the room, each copying a nearby crystal colour:
- three in the floor in the middle (purple, cyan, green);
- two in the walls: one high (2.2 m) on the west wall, and one low (0.6 m) on the east wall.

The player starts with the **Crystal Disruptor** already installed (a `StartingUpgrades` testing component on the Player), so no scrap or shop is needed. Raise the scanner (**Q**); it beeps toward the nearest hidden one. Walk within 4 m and it bursts out. They don't respawn; restart Play to try again.

The scene is generated. **Create Test Scene** only builds it when it's missing, so delete `CrystalChameleonTest.unity` (keep its `.meta`) to regenerate it.

Menu **Signal Lost > Crystal Caves Enemies > Chameleon**:

| Menu item | What it does |
| --- | --- |
| **Build Prefab** | Rebuilds `Prefabs/Enemies/Caves/CrystalChameleon.prefab` and its meshes from the script (same GUID). Edits made to the prefab are lost. |
| **Create Test Scene** | Creates the test scene if it doesn't exist (it never overwrites one) and validates it. |
| **Validate** | Checks the prefab's wiring and the test scene. |

## How it behaves

| State | What happens |
| --- | --- |
| **Camouflaged** | Buried 0.95 m deep, so only its crystals stick out. It has no colliders (it can't be shot), the scanner ignores it unless the player owns the Crystal Disruptor, and its AI does nothing. Its crystals wear a real cave crystal material and it has a glow light, like a batch. |
| **In a wall** | With **Hide In Wall** ticked, its body clings to the wall instead, belly to the wall and head up, sunk 0.95 m in so only the crystals stick out. Its root (NavMeshAgent, colliders, AI) waits on the floor 0.9 m out from the wall, below its hiding spot. |
| **Disrupted** | The raised, upgraded scanner comes within **Reveal Radius** (4 m) of it (for a wall one, of its spot on the wall). `Disrupt()` turns its crystals red and the glow light red, and starts the shared [BurrowEmerge](burrow-emerge.md) rise (1 s). |
| **Wall drop** | A wall one slides straight out of the wall (the same BurrowEmerge rise, along the wall's facing), then falls to its spot on the floor while turning upright (0.45 s). From then on it fights like a floor one. |
| **Grace** | Risen: it can move but not attack for 0.8 s (BurrowEmerge's grace). |
| **Stalking** | It keeps 3–5.5 m from the player, closes in when it can't see them, and backs off if they get within 3 m. It has a 14 m territory with the shared leash and heal rule, like all fodder. |
| **Tongue** | **Wind-up (0.65 s), the warning:** it stops, pulls its head back, opens its mouth, swells its throat and pulses its red crystals. The aim follows the player for the first 75%, then locks. **Lick:** the tongue shoots out in a straight line, up to 8 m, in 0.12 s, holds, and retracts. If it passes within 0.3 m of the player's body, it deals 10 damage. Walls stop it. Then a 1.6 s cooldown. |
| **Death** | Its crystals switch off (dull grey), the glow goes out, and it drops scrap: 1–2 scrap metal and 0–1 wiring. |

- **Health:** 40.
- **Movement:** 3.2 m/s.
- **Stun:** the laser's combo finisher stuns it, like all fodder ([enemy rules](enemy-rules.md)).
- **Eyes:** each eye turret swivels on its own, then both lock onto the player once it is revealed.

**Counterplay:** watch for the head pulling back and the crystals pulsing, then sidestep. Rocks block the tongue.

## The scanner upgrade

The chameleon implements `IScannerDisruptable`. The Crystal Disruptor finds it through its `EnemySignalSource` and calls `Disrupt()` when the raised scanner gets within **Reveal Radius**. How it works, and how to add another disruptable enemy, is in [scanner-prototype.md](scanner-prototype.md#crystal-disruptor-upgrade). The `Revealed` event still fires once per chameleon, for sounds, HUD hints or counting.

To place chameleons in the level:
1. Drop the prefab where it should hide, ideally next to real crystal batches.
   - **Floor:** on cave floor with a NavMesh.
   - **Wall:** tick **Hide In Wall**. Put it on the wall's surface at any height, rotated so its forward (the blue arrow) points out of the wall, into the room. There must be NavMesh floor below, about 0.9 m out from the wall (**Wall Landing Distance**).
2. Set its **Crystal Material** to the colour of the batches around it.
3. Leave **Start Revealed** unticked.

## Changes to shared code

- **[BurrowEmerge](burrow-emerge.md)** (merged in from `feature/enemy-burrow-emerge`) gets two additions:
  - **Emerge On Proximity**, on by default, so the crab and every existing use behave as before. Untick it for enemies that only rise when told.
  - A public **`Emerge()`** method, which starts the rise from code. The chameleon uses both.
- **New shared editor kit:** `Editor/CaveEnemyKit.cs` holds the cave enemy folders and materials, the cave test room, and the shared Player and GameSystems setup. The beetle mini-bosses use it too.

## Files

- `Scripts/Progression/StartingUpgrades.cs`: a testing component that installs upgrades on the player at start (here, the Crystal Disruptor).
- `Scripts/Enemies/Caves/CrystalChameleon.cs`: the enemy. It builds on `DesertFodder`, like all fodder, and implements `IBurrowOffset`.
- `Editor/CrystalChameleonBuilder.cs`: the model, prefab, test scene and validation.
- `Editor/CaveEnemyKit.cs`: the shared cave enemy helpers.
- `Prefabs/Enemies/Caves/CrystalChameleon.prefab`, its meshes in `Prefabs/Enemies/Caves/Meshes/`, and its materials in `Materials/Enemies/Caves/`. The crystals reuse the cave's own crystal meshes and materials.
- `Scenes/Enemies/CrystalChameleonTest.unity`.
- [CrystalChameleonSmoke.cs](../tools/unity/CrystalChameleonSmoke.cs): the Play Mode smoke check. Install it only in a disposable copy of the project.

## Manual checks

1. Press Play. There are no Console errors (a short pause while the NavMesh builds is normal). Only crystal clusters show: three on the floor, and two stuck to the side walls.
2. Raise the scanner (**Q**). It beeps toward the nearest hidden one. Walk up to a floor cluster: within 4 m its crystals turn red and it rises out of the floor.
3. Walk up to a wall cluster (west wall, high; east wall, low): it turns red, slides out of the wall, drops to the floor and turns to face you.
4. Lower the scanner and walk up to another one: it stays hidden.
5. Fight one. A chameleon stops, pulls its head back, swells its throat and pulses its crystals, then flicks its tongue. Standing still: 10 damage and a HUD flash. Sidestepping after the aim locks: it misses.
6. Stand behind a rock: the tongue stops at the rock.
7. Get within 3 m: it backs away. Walk far away: it gives up, walks home and heals.
8. Kill one: its crystals go dull grey, its light goes out, scrap pops out, and the body flops and sinks.
9. Without the scanner: select a hidden one, then right-click the CrystalChameleon component header and choose **Disrupt**. It turns red and rises (or drops, from a wall).

## Open questions

- **Wall chameleons in the level:** the cave's walls are uneven rock, not flat test walls. Each one needs its rotation checked by eye so its body sits flush.
- **Shot while camouflaged:** for now it can't be hit at all while hidden. Should a laser hit reveal it?

## Validation performed

- **Unity 6000.6.2f1 batch run on this branch:** the project compiled with no errors. **Create Test Scene** built the prefab and scene, and **Validate** passed (`CRYSTAL_CHAMELEON_TEST_SCENE_CREATED_AND_VALIDATED`).
- **Play Mode smoke** ([CrystalChameleonSmoke.cs](../tools/unity/CrystalChameleonSmoke.cs)) passed in a disposable copy of the project (`CRYSTAL_CHAMELEON_PLAY_MODE_PASS`). It checks:
  - (11-12, walls) all five test chameleons start hidden, and the player starts with the Crystal Disruptor. A wall one waits with its root on the floor and its body on the wall, facing out. The raised scanner in front of it reveals it, and it drops upright onto the NavMesh;
  - a fresh camouflaged one is buried and unhittable, and wears the cave crystal material;
  - (11-12, Crystal Disruptor) the raised scanner ignores it without the upgrade, and beeps at it from 8 m with the upgrade. At 2 m the upgrade reveals it: it turns red and emerges, with its collider back on. This passed in a disposable copy (`CRYSTAL_CHAMELEON_PLAY_MODE_PASS`, 2 tongue hits);
  - the tongue hits the player (3 hits);
  - a kill drops scrap and switches its crystals off.
- **Not checked:** how it looks and feels: animation, the camouflage next to real batches, tuning. Check those in the editor.
