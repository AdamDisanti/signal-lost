# Desert / Dunes level (prototype)

First biome after the tutorial, built from the design document's level description: **"open terrain with scattered wreckage"**, mechanical focus on **open exploration**, three mini encounters before the boss, the **Technology-Enhanced Hermit Crab** boss and the **Engine Core** reward. Owner: Nicole (environment art). Everything is low-poly placeholder art made from generated meshes, with no imported asset packs.

## Create the level

Open the `signal-lost/` project, then choose **Signal Lost → Create Desert Dunes Level**. It creates:

- `Assets/Prefabs/Enviornment objects/DesertDunes/DesertEnvironment.prefab`: terrain, perimeter dunes, mesas, rocks, alien plants, wreckage, ribcage and boss-arena spires. Its meshes and materials are in `Meshes/` and `Materials/` beside it.
- `Assets/Scenes/DesertDunes.unity`: the environment, desert lighting/fog/sky, the player, five hermit crabs at three encounter sites, and placeholder markers.

Like the other builders, it refuses to overwrite the scene. Commit the scene, the `DesertDunes` folder, the builder script and all `.meta` files.

After that, edit the scene and prefab in Unity: move rocks, add props, change colors. **Signal Lost → Rebuild Desert Environment Prefab** regenerates the environment from the script (same seed, same layout) and throws away edits made to the prefab.

## Layout

The map is 320 × 320 m, the same footprint as the tutorial crash site, made of 4 m flat-shaded facets. +Z is north.

| Area | Where | What's there |
| --- | --- | --- |
| Biome Entrance | South canyon, around (0, −130) | Player spawn and respawn point. Cavendish stands nearby; an empty Merchant Spot marker is next to him |
| Crash streak | South-west to north-east across the middle | About 48 hull plates and chips scattered along the path the ship's debris flew, leading the eye toward the boss arena |
| Encounter 1: Wreck Field | West, (−62, −62) | Flat ground ringed by hull plates, 1 hermit crab |
| Encounter 2: Mesa Pass | East, (78, −18) | 2 hermit crabs among wreckage |
| Encounter 3: Buried Hull | North-west, (−58, 62) | 2 hermit crabs among wreckage |
| Giant Ribcage | Center-south, (22, −48) | Bleached ribs arching out of the sand. A landmark only |
| Boss Arena | North, (38, 92) | Sand bowl ringed by tall spires, open to the south and west, with a half-buried ship engine. Boss Spawn and Engine Core Drop markers, no boss yet |
| Perimeter | All round | Tall, steep dunes the player can't climb, so no invisible walls are needed |

Scattered across the dunes are 12 flat-topped mesas (long-range landmarks), 70 boulders (cover) and 34 goofy alien cacti (teal stems, pink bulbs). Lighting is a warm, high sun with a procedural sky and linear dust haze (45–260 m) so far areas fade out. This is the biome's own lighting and fog, as the design doc's Artistic Differentiation asks.

## Player setup

The player matches the tested prototypes: the shared player prefab with the mining laser, 100 HP, Christian's placeholder respawn (back to the biome entrance), Amanda's **ScannerRig** (press **Q** to raise it), `ScannerLaserSwap`, and the **PlayerHUD**. Raising the scanner holsters the laser so the scanner display can be seen, and lowering it draws the laser again. You can't attack while scanning. Each crab has an `EnemySignalSource`, so the scanner finds it within its 30 m range. The HUD's own scanner widget is hidden in this scene, because the scanner shows its arrow and meter on the device. The HUD objective reads "Scan for creatures (press Q)".

The flask and drinking are **not** in this level. They live in `TutorialPlayer`, which is tied to the tutorial's own HUD and pickups.

## Test checklist

1. Open `DesertDunes.unity` and press Play. No Console errors. There may be a short pause at start while the crabs' NavMesh is built.
2. You start in the south canyon facing north, with Cavendish beside you. The HUD shows 100 / 100 and the objective.
3. Walk out into the dunes. The slopes are walkable, the facets read as low-poly, and the far edges fade into warm haze.
4. Try to climb the perimeter dunes. You can't get out anywhere, including at the canyon's dead end.
5. Press **Q**: the laser disappears and the scanner comes up, fully visible. Walk toward the Wreck Field (west). Within about 30 m the arrow points at the crab and the beeps speed up. Press **Q** again: the scanner goes down and the laser comes back. Clicking while scanning does nothing.
   - Start charging a shot (hold right click), then press **Q**: the charge is cancelled and nothing fires.
6. Fight the crabs at all three sites. They chase, attack, and stay dead when defeated.
7. Die to a crab: you return to the entrance with full health.
8. Walk into the Boss Arena through the south or west gap: the bowl, spires and buried engine are there, with no boss.
9. Check that big rocks, mesas, cacti and large hull plates block you, and that small debris chips don't.

## Open questions

- **Ownership:** Lawson owns level design in the team roster. Confirm with him that Nicole builds the Desert, or whether this should be his layout with her art.
- **Encounters:** what counts as one of the three mini encounters (a site, a creature, a quest?). The crabs here are ordinary hermit crabs, and nothing counts kills or unlocks the boss signal yet.
- **Death reset:** Lawson's tutorial rule resets stage enemies on death. These crabs stay dead after the player respawns, because the placeholder respawn doesn't reset them.
- **Boss, merchant, quests, exit:** the Tech-Enhanced Hermit Crab, the merchant crab, Cavendish's quest and the exit to the Fog Forest don't exist yet, so they're empty markers only.
- **Scrap:** the design says enemies drop scrap. There's no scrap system yet.
- **Scanner display:** HUD widget or in-hand device (see [Player HUD](hud.md)).
- **Laser/scanner swap:** holstering the laser while scanning is a proposal; confirm it with Amanda (scanner) and Christian (laser).
- **Art pass:** swap the placeholder meshes for real low-poly desert assets once the team picks a pack (record sources in [assets.md](assets.md)).
