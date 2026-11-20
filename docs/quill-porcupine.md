# Quill Porcupine (Crystal Caves fodder)

A ranged small enemy for the [Crystal Caves](crystal-caves.md), from Amanda's 11-13 request. Built on `feature/quill-porcupine`. It follows the same fodder rules as the other cave enemies ([enemy rules](enemy-rules.md)): a 14 m territory with the shared leash and heal rule, and the laser's combo finisher stuns it.

It lives only in its own test scene for now. It is **not placed in the Crystal Caves level**; that waits until Amanda asks.

The model is generated in code, in the same low-poly style as the other enemies. No assets were imported. All numbers are placeholders to tune.

## Concept

A stout, grumpy porcupine whose quills are glowing crystal spikes. It stays in the open and fires volleys. It is the second ranged type in the caves, next to the [Crystal Chameleon](crystal-chameleon.md).

## Look

- A round, low-slung body in dusky brown fur with a paler belly, a round face, a blunt snout with a pink nose, small glossy black eyes and round ears.
- A short fringe of brown fur spikes behind the face.
- A back of 42 crystal quills, short at the neck and longest on top, swept up and back. They alternate the cave's own **cyan** and **purple** crystal materials (fixed, Amanda's 11-13 choice), with a glow light like a batch.
- About 1.1 m long and 0.6 m tall at the back.

## Open it

Open `Assets/Scenes/Enemies/QuillPorcupineTest.unity` and press Play.

The test cave is the same walled, roofed room as the chameleon's (`CaveEnemyKit`): the level's cave floor, wall and ceiling materials, real crystal batches, boulders and stalagmites for cover, and the cave's inside lighting. You start at the south end with the shared **Player** (laser, scanner, flask) and **GameSystems** (HUD, dialogue, pause and settings menus), the same setup as the Crystal Caves level. Three porcupines wait at the north end. They don't respawn; restart Play to fight them again.

Menu **Signal Lost > Crystal Caves Enemies > Quill Porcupine**:

| Menu item | What it does |
| --- | --- |
| **Build Prefab** | Rebuilds `Prefabs/Enemies/Caves/QuillPorcupine.prefab` and its meshes from the script (same GUID). Edits made to the prefab are lost. |
| **Create Test Scene** | Creates the test scene if it doesn't exist (it never overwrites one) and validates it. |
| **Validate** | Checks the prefab's wiring and the test scene. |

## How it behaves

| State | What happens |
| --- | --- |
| **Idle** | Waddles about within 2 m of its spot until you enter its territory. A hit draws it in. |
| **Keep distance** | Closes in if you're farther than 10 m or out of sight, holds between 4 and 10 m, and backs off if you get closer than 4 m. |
| **Curl** | While you hold a laser charge and look right at it (the Jug Possum's check), it curls into a quill ball in place. Curled, **a full charged bolt takes exactly half its health** (16 of 32), so it takes two full charges to kill. Every other hit scales the same way (a half-damage charge takes a quarter of its health). It reads the full-charge damage from the laser, so laser upgrades keep it at two shots. It stays curled 0.6 s after you stop aiming, so the bolt you just released still hits the ball. Then it pops up and fires almost straight away. It curls from keeping distance, its short pause after a fan, or a bristle, and in the first 40% of its wind-up. |
| **Wind-up (0.7 s), the warning** | It stops, raises its back, the quills swell and glow brighter. The aim follows you for the first 75%, then locks. |
| **Quill fan** | Fires 3 quills in a fan, 14° apart, at 18 m/s. 8 damage each. A quill that misses sticks in the wall or floor for 4 s and stings for 40% (about 3) if you brush it. Then a 3 s cooldown. |
| **Bristle** | If you come within 2 m, it stops and bristles for 1.5 s: the quills flare and flicker. Touching it then deals 5 thorn damage (at most every 0.5 s). Then a 3 s cooldown before it can bristle again, so melee works between bristles. |
| **Death** | The quills go dull grey and the glow goes out. Drops 1-2 scrap metal and 0-1 wiring, like the other cave fodder. |

- **Health:** 32. More than the desert's Dish Meerkat (20) and less than the Mudlands' Jug Possum (45).
- **Movement:** 2.6 m/s.
- **Stun:** the laser's combo finisher stuns it, like all fodder.

**Counterplay:** sidestep after the aim locks, use rocks as cover, and shoot it from range. Don't melee it while it bristles. Charging a shot at it makes it curl, and curled it takes two full charges, so catch it uncurled or mid-fan to kill it in one.

## How it differs from the chameleon

The chameleon hides until revealed, then fires one tongue at a time. The porcupine is in the open from the start and fires a fan of three.

## Phobia check

A furry mammal with no crawlies, no many-legged shape and nothing skittering.

## Amanda's decisions (11-13)

- Stuck quills **sting**. They reuse Briarwing's `ThornShot` and `GroundThorn` as they are.
- Bristle is **kept, with a short window** (1.5 s on, 3 s cooldown).
- Cover is a **curl into a quill ball** in place, not a run to a stalagmite. Curled, **one full charge takes half its health, the second kills it**, and smaller hits scale with it (changed 11-13). It stays curled 0.6 s after the aim stops, because the released bolt used to land after it had already uncurled.
- Quill colours are **fixed cyan and purple**.

## Shared code reused (nothing changed)

- `DesertFodder`: territory, leash and heal, hit flash, combo stun, death.
- `ThornShot` / `GroundThorn` (`BossFx.cs`): the flying quill and the stuck quill. ThornShot scales its mesh for a boss thorn, so the porcupine's quill mesh is baked smaller.
- `Health.DamageModifier`: the curl's scaled damage.
- `CaveEnemyKit`: folders, materials, the cave test room, and the shared Player and GameSystems.

## Files

- `Scripts/Enemies/Caves/QuillPorcupine.cs`: the enemy.
- `Editor/QuillPorcupineBuilder.cs`: the model, prefab, test scene and validation.
- `Prefabs/Enemies/Caves/QuillPorcupine.prefab`, its meshes (`PorcupineQuill`, `PorcupineQuillShot`) in `Prefabs/Enemies/Caves/Meshes/`, and its materials (`Porcupine*`) in `Materials/Enemies/Caves/`. The quills use the cave's own crystal materials.
- `Scenes/Enemies/QuillPorcupineTest.unity`.
- [QuillPorcupineSmoke.cs](../tools/unity/QuillPorcupineSmoke.cs): the Play Mode smoke check. Install it only in a disposable copy of the project.

## Manual checks

1. Press Play. There are no Console errors (a short pause while the NavMesh builds is normal).
2. Walk in. A porcupine stops, raises its back and its quills glow brighter, then fires three quills. Standing still: 8 damage and a HUD flash. Sidestepping after the aim locks: they miss and stick in the wall or floor, then fade.
3. Brush a stuck quill: a small sting.
4. Stand behind a rock: the quills stick in the rock.
5. Hold a laser charge aimed at one: it curls into a ball. Release: it uncurls and fires soon after.
6. Walk right up to one: it bristles and touching it hurts. Wait about 3 s and it can be meleed safely until the next bristle.
7. Walk far away: it gives up, walks home and heals.
8. Kill one: its quills go dull grey, its light goes out, scrap pops out, and the body flops and sinks.

## Validation performed

- **Unity 6000.6.2f1 batch run on this branch:** the project compiled with no errors. **Create Test Scene** built the prefab and scene, and **Validate** passed (`QUILL_PORCUPINE_TEST_SCENE_CREATED_AND_VALIDATED`, then `QUILL_PORCUPINE_VALIDATED` after the final prefab rebuild).
- **Play Mode smoke** ([QuillPorcupineSmoke.cs](../tools/unity/QuillPorcupineSmoke.cs)) passed in a disposable copy of the project (`QUILL_PORCUPINE_PLAY_MODE_PASS`). It checks:
  - three porcupines in the test scene;
  - a quill hits the player (8 damage);
  - standing next to one, a bristle thorn hits (5 damage);
  - curled, a half-damage charge takes a quarter of its health, a full charge leaves exactly half, and a second full charge kills it;
  - a kill drops scrap and dulls its quills.
- **Not checked:** how it looks and feels in the editor: true colours under the cave lighting, animation, tuning. A batch preview render confirmed the shape (quills swept up and back) but not the colours. Check those in Unity.
