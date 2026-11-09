# Crystal Chameleon (Crystal Caves fodder)

The small enemy of the [Crystal Caves](crystal-caves.md), from Amanda's 11-09 request. It's a crawling chameleon whose back is a cluster of the cave's own crystals. While camouflaged it lies buried with only the crystals showing, so it looks exactly like any other crystal batch. The scanner's future camouflage-disrupting upgrade will reveal it: its crystals turn red, it rises out of the ground and it attacks with its tongue. Its crystals stay red while it fights and go dark when it dies.

It lives only in its own test scene for now. It is **not placed in the Crystal Caves level**; that waits until Amanda asks.

The model is generated in code, in the same low-poly style as the other enemies. No assets were imported. All numbers are placeholders to tune.

## Open it

Open `Assets/Scenes/Enemies/CrystalChameleonTest.unity` and press Play.

The test cave is a walled, roofed room. It uses the level's own cave floor, wall and ceiling materials, real crystal batches along the walls, and the cave's inside lighting. You start at the south end with the shared **Player** (laser, scanner, flask) and **GameSystems** (HUD, dialogue, pause and settings menus), the same setup as the Crystal Caves level.

Three chameleons wait in the middle, each copying a nearby crystal colour: purple, cyan and green. Because no scanner disruption exists yet, each test instance has **Start Revealed** ticked. They turn red and rise out of the floor as soon as Play starts, which also shows the emerge. They don't respawn; restart Play to fight them again.

Menu **Signal Lost > Crystal Caves Enemies > Chameleon**:

| Menu item | What it does |
| --- | --- |
| **Build Prefab** | Rebuilds `Prefabs/Enemies/Caves/CrystalChameleon.prefab` and its meshes from the script (same GUID). Edits made to the prefab are lost. |
| **Create Test Scene** | Creates the test scene if it doesn't exist (it never overwrites one) and validates it. |
| **Validate** | Checks the prefab's wiring and the test scene. |

## How it behaves

| State | What happens |
| --- | --- |
| **Camouflaged** | Buried 0.95 m deep, so only its crystals stick out. It has no colliders (it can't be shot), its scanner signal is switched off (the ordinary scanner can't find it), and its AI does nothing. Its crystals wear a real cave crystal material and it has a glow light, like a batch. |
| **Disrupted** | `Disrupt()` turns its crystals red and the glow light red, switches the scanner signal on, and starts the shared [BurrowEmerge](burrow-emerge.md) rise (1 s). |
| **Grace** | Risen: it can move but not attack for 0.8 s (BurrowEmerge's grace). |
| **Stalking** | It keeps 3–5.5 m from the player, closes in when it can't see them, and backs off if they get within 3 m. It has a 14 m territory with the shared leash and heal rule, like all fodder. |
| **Tongue** | **Wind-up (0.65 s), the warning:** it stops, pulls its head back, opens its mouth, swells its throat and pulses its red crystals. The aim follows the player for the first 75%, then locks. **Lick:** the tongue shoots out in a straight line, up to 8 m, in 0.12 s, holds, and retracts. If it passes within 0.3 m of the player's body, it deals 10 damage. Walls stop it. Then a 1.6 s cooldown. |
| **Death** | Its crystals switch off (dull grey), the glow goes out, and it drops scrap: 1–2 scrap metal and 0–1 wiring. |

- **Health:** 40.
- **Movement:** 3.2 m/s.
- **Stun:** the laser's combo finisher stuns it, like all fodder ([enemy rules](enemy-rules.md)).
- **Eyes:** each eye turret swivels on its own, then both lock onto the player once it is revealed.

**Counterplay:** watch for the head pulling back and the crystals pulsing, then sidestep. Rocks block the tongue.

## Wiring the scanner upgrade later

The chameleon is ready for it:

1. **Find hidden chameleons.** They are `CrystalChameleon` components with `IsCamouflaged == true`. The ordinary scanner doesn't see them, because their `EnemySignalSource` stays disabled until they are disrupted. The upgrade should find them itself, for example with `FindObjectsByType<CrystalChameleon>`, or with a trigger or overlap check around the scanner's aim.
2. **Reveal one.** Call `chameleon.Disrupt()`. That does everything: red crystals, signal on, emerge, then fight. It's safe to call more than once.
3. **React.** `Revealed` is an event, raised once per chameleon, for sounds, HUD hints or counting.

To place chameleons in the level:
1. Drop the prefab where it should hide (on cave floor with a NavMesh, ideally next to real crystal batches).
2. Set its **Crystal Material** to the colour of the batches around it.
3. Leave **Start Revealed** unticked.

## Changes to shared code

- **[BurrowEmerge](burrow-emerge.md)** (merged in from `feature/enemy-burrow-emerge`) gets two additions:
  - **Emerge On Proximity**, on by default, so the crab and every existing use behave as before. Untick it for enemies that only rise when told.
  - A public **`Emerge()`** method, which starts the rise from code. The chameleon uses both.
- **New shared editor kit:** `Editor/CaveEnemyKit.cs` holds the cave enemy folders and materials, the cave test room, and the shared Player and GameSystems setup. The beetle mini-bosses use it too.

## Files

- `Scripts/Enemies/Caves/CrystalChameleon.cs`: the enemy. It builds on `DesertFodder`, like all fodder, and implements `IBurrowOffset`.
- `Editor/CrystalChameleonBuilder.cs`: the model, prefab, test scene and validation.
- `Editor/CaveEnemyKit.cs`: the shared cave enemy helpers.
- `Prefabs/Enemies/Caves/CrystalChameleon.prefab`, its meshes in `Prefabs/Enemies/Caves/Meshes/`, and its materials in `Materials/Enemies/Caves/`. The crystals reuse the cave's own crystal meshes and materials.
- `Scenes/Enemies/CrystalChameleonTest.unity`.
- [CrystalChameleonSmoke.cs](../tools/unity/CrystalChameleonSmoke.cs): the Play Mode smoke check. Install it only in a disposable copy of the project.

## Manual checks

1. Press Play. There are no Console errors (a short pause while the NavMesh builds is normal). The three chameleons' crystals turn red, and they rise out of the floor.
2. Walk in. A chameleon stops, pulls its head back, swells its throat and pulses its crystals, then flicks its tongue. Standing still: 10 damage and a HUD flash. Sidestepping after the aim locks: it misses.
3. Stand behind a rock: the tongue stops at the rock.
4. Get within 3 m: it backs away. Walk far away: it gives up, walks home and heals.
5. Kill one: its crystals go dull grey, its light goes out, scrap pops out, and the body flops and sinks.
6. **Camouflage check:**
   1. In the Scene view during Play, drag the prefab in with **Start Revealed** unticked. Only crystals show above the floor, it can't be shot, and the scanner (**Q**) doesn't pick it up.
   2. Select it, then right-click the CrystalChameleon component header and choose **Disrupt**. It turns red and rises.

## Open questions

- **Walls:** Amanda's brief says it emerges from the wall or the ground. Only ground is built. BurrowEmerge rises straight up, and the chameleon walks on the NavMesh. A wall chameleon needs a rise along the wall's normal and a drop to the floor, so decide on it together with the scanner work.
- **Shot while camouflaged:** for now it can't be hit at all while hidden. Should a laser hit reveal it?

## Validation performed

- **Unity 6000.6.2f1 batch run on this branch:** the project compiled with no errors. **Create Test Scene** built the prefab and scene, and **Validate** passed (`CRYSTAL_CHAMELEON_TEST_SCENE_CREATED_AND_VALIDATED`).
- **Play Mode smoke** ([CrystalChameleonSmoke.cs](../tools/unity/CrystalChameleonSmoke.cs)) passed in a disposable copy of the project (`CRYSTAL_CHAMELEON_PLAY_MODE_PASS`). It checks:
  - the three test chameleons reveal themselves;
  - a fresh camouflaged one is buried, unhittable and hidden from the scanner, and wears the cave crystal material;
  - `Disrupt()` turns it red and it emerges, with its collider and signal back on;
  - the tongue hits the player (3 hits);
  - a kill drops scrap and switches its crystals off.
- **Not checked:** how it looks and feels: animation, the camouflage next to real batches, tuning. Check those in the editor.
