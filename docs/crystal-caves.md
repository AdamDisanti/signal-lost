# Crystal Caves level (world only)

The third biome, from Amanda's 11-08 design map and six reference images (sea cliffs, low-poly caves lit by crystals). The design document calls it **Crystal Caverns** (boss: Crystal-infused Cave Beast, reward: Flight Control Module). This pass builds **the world only**: landscape, camp, dock and lighting. Enemies, quests and level flow come later and have empty, marked places to go.

Everything is generated, low-poly art made in code, in the same style as the tutorial and the desert. No asset packs were imported.

## Create or rebuild it

Menu **Signal Lost → Crystal Caves**:

| Menu item | What it does |
| --- | --- |
| **Create Crystal Caves Level** | Builds anything missing, then creates `Assets/Scenes/Large Areas/CrystalCaves.unity`, bakes its NavMesh and validates it. It refuses to overwrite an existing scene. |
| **Rebuild Crystal Caves Environment** | Regenerates `CrystalCavesEnvironment.prefab` (same seed, same layout) and throws away edits made to the prefab. The scene keeps its instance. Re-bake the NavMesh afterwards: select **Navigation** and press **Bake**. |
| **Build Crystal and Rock Prefabs** | Rebuilds the crystal and cave-rock kit. |

**Signal Lost → Core → Build Dock and Boat** rebuilds the shared dock and boat.

This follows the Desert Dunes workflow (the project lead's choice): the landscape is a prefab you can rebuild from code, and the scene is created once and then edited by hand. Add enemies, quests and triggers in the scene. They survive environment rebuilds.

## Layout

The map is **480 × 480 m**, 1.5 times Desert Dunes' 320 m, with 4 m flat-shaded facets. +Z is north and sea level is y = 0.

| Area | Where (x, z) | What's there |
| --- | --- | --- |
| Ocean | All round | Stylized water (the tutorial pond's shader, retuned) out to the horizon, with foam along the cliffs. It is the sea that will link the levels. |
| Rocky shelf | The south coast, from WSW to ENE | Walkable coastal rock, stepped like stacked blocks, at 10–18 m. It is lined with columnar sea cliffs and tumbled blocks in the surf, plus boulders, a few pines, bushes and grass. At both ends the mountains drop straight into the sea, so the shelf is closed off without invisible walls. |
| South-east ledge | Around (130, −172) | The widest open space, holding the dock, the camp and the view of the cave mouth. |
| Dock and boat | Cliff edge at (124, −197), pointing south | A deck hanging off the cliff on brackets, then a railed stairway on pilings down to a jetty at the waterline. The boat is moored beside the jetty. The player spawns and respawns on the top deck, facing the cave mouth. |
| Campsite | (144, −164) | The shared **BaseCamp** prefab (fire, tent, logs, supplies, three trophy stands that remember trophies from earlier levels). Cavendish sits on his log and Eugene waits by the fire with his shop open. A 16 m clear radius is left around it for the stands of later levels. |
| Cave mouth | Portal at (118, −128), facing south | A dark arch in the foot of the mountains, visible from the dock with crystal glow inside. No path leads to it. |
| Mountains | Over the whole cave | Jagged peaks about 60 m high at the shelf, climbing to 150 m and more. They use the tutorial's mountain rock materials. |
| Cave, upper level | Ring around the deep level | Floor about 18 m above sea level, ceiling about 32 m above that. It is widest in the south. |
| Cave, deep level | Ellipse centred on (0, 70), 292 × 220 m | Floor about 16 m lower than the upper level, with a vaulted ceiling about 70 m up. Elsewhere its edge is a cliff. **Three ramps** lead down: by the entrance (300°), south-west (225°) and west (175°). |
| Beetles fight zone | (−150, −42), r 24 m, upper level | Flattened and empty, ringed by purple and pink crystals. |
| Mini Boss 2 fight zone | (80, 35), r 24 m, deep level | Flattened and empty, ringed by orange and pink crystals. |
| Final boss fight zone | (0, 138), 152 × 68 m ellipse, deep level | Flattened and empty, ringed by purple and blue crystals. |

Each fight zone has a **CombatZone** marker under *Level Markers (placeholders)*, so enemies placed later can use it, and its rings show in the Scene view. A *Boat Travel* marker sits on the boat.

To move things, edit the numbers at the top of `Editor/CrystalCavesTerrain.cs` (positions, sizes, ramp angles, heights) and rebuild the environment.

## Cave lighting

The glowing crystals are the only lights in the cave:
- **Crystal batches:** 183 groups, each with one point light in the crystals' colour. They line the tunnel (on alternating walls), mark the top and bottom of every ramp, ring the fight zones, and are scattered over the floors, walls and ceiling.
- **Shapes:** three crystal shapes (spire, cluster, shard), tilted and sized differently.
- **Colours:** six, namely dark blue, green, orange, purple, pink and cyan. Each area of the cave leans toward a few colours and about one batch in eight is any colour.
- **Rock:** stalagmites, stalactites, boulders and huge floor-to-ceiling columns break up the space.

**CaveLighting** (on the environment prefab) makes the cave dark inside a sunlit level:
- **Fade-in:** once the camera is under the cave roof, the sun, sky light, reflections and fog fade to the cave settings over the first 40 m from the mouth.
- **Fill light:** a faint ambient fill keeps rock shapes readable between the crystals' pools of light. Tune it in its Inspector.
- **Lights outside:** crystal lights more than 70 m from the mouth switch off while you are outside. They have no shadows, so they would otherwise shine through the mountain.

### Adding or changing crystals

- **Add:** drop `Prefabs/Enviornment objects/CrystalCaves/Crystals/CrystalBatch.prefab` anywhere.
- **Recolour:** choose a material from `CrystalCaves/Materials/Crystal*.mat` on its **CrystalBatch** component. Its crystals and its light change together.
- **Reshape:** add, move or delete the crystals under a batch freely.
- **Brightness:** set it on the batch's **Glow** light. Keep the light above the crystals, because inside one it only lights that crystal from within.
- **Light budget:** stay under about 220 crystal lights. Forward+ has a per-camera light budget, and validation checks this.

## Shared pieces, reused

- **Player and UI:** the shared **Player** (laser, scanner, flask; all three from the start) and **GameSystems** (status-bar HUD, dialogue, pause and settings menus) from `Prefabs/Core`. They are the same as in every level.
- **Scanner:** level range 200 m, like the tutorial.
- **Camp and NPCs:** **BaseCamp**, Cavendish (the capybara prefab, seated as in the tutorial, small talk only) and Eugene (the merchant crab, already friendly, with his ten cousins waiting despawned for the angry-merchant ambush).
- **Dock and boat (new, shared):** `Prefabs/Core/Dock.prefab` and `Boat.prefab`, built by `Editor/DockBoatBuilder.cs` from the meadow and shipwreck materials. To use them in another level, put the dock's origin on a cliff edge 10 m above the sea, facing out to sea, and the boat at `DockBoatBuilder.BoatMooring`. They are scenery: boat travel isn't built yet.
- **FallRespawn (new, reusable):** falling below y = −3 (off a sea cliff or the jetty) brings you back to the dock with the normal death fade.
- **Build list:** the scene is in the build list (MainMenu, Tutorial, Sandbox, Desert, **Crystal Caves**, Prototype). Nothing loads it yet.

## Files

| File | What it is |
| --- | --- |
| `Editor/CrystalCavesTerrain.cs` | Layout numbers and the landform: ground, cave ceiling, mountain top, ocean, and a self-check (roof thickness, headroom, ramp slopes, tunnel height, island inside the map) |
| `Editor/CrystalCavesBuilder.cs` | The environment prefab (props and crystal placement) and the scene (lighting, player, camp, NavMesh, markers, validation) |
| `Editor/CrystalAssetBuilder.cs` | Crystal shapes and materials, CrystalBatch prefab, cave and coast rock |
| `Editor/DockBoatBuilder.cs` | Shared dock and boat |
| `Scripts/Effects/CaveLighting.cs`, `CrystalBatch.cs` | Runtime cave darkness and crystal colour sync |
| `Scripts/Player/FallRespawn.cs` | Sea-fall respawn |
| `Prefabs/Enviornment objects/CrystalCaves/` | Environment prefab, meshes, materials, crystals, rocks |
| `Scenes/Large Areas/CrystalCaves.unity`, `CrystalCavesNavMesh.asset` | The scene and its baked NavMesh (Git LFS) |

## Verification (11-08, Unity 6000.6.2f1, batch mode)

- **Create Crystal Caves Level:** passed its validation.
  - It found one Player, GameSystems, spawn point, camera and listener.
  - It found CaveLighting, FallRespawn, three trophy stands, Cavendish, a friendly Eugene, the dock, the boat and the camp.
  - The NavMesh was baked, 183 crystal lights were found (in all six colours), and no scripts were missing.
  - NavMesh paths from the dock reach the camp, the inside of the cave mouth, the foot of all three ramps and all three fight zones.
- **Landform self-check:** passed.
- **Play Mode smoke run:** a temporary script, not committed. It passed with no Console errors.
  - Outside, the sun was on and the 154 deep crystal lights were off.
  - Inside the cave, the sun turned off, the cave fill and fog applied, and all deep lights came on.
  - Falling into the sea respawned the player on the dock, and the daylight came back.
- **Rendered review shots:** checked from the dock, the camp, the sea, the tunnel, both cave levels and the fight zones.

**Not checked:** physical keyboard and mouse play, the feel of the ramps and stairs on foot, frame rate on target hardware, and how the light levels look on different monitors.

## Open questions

- **Level flow:** how the player gets here (boat travel from the desert?), and where the exit is.
- **Enemies and quests:** the beetles, Mini Boss 2 (and any others), the final boss and Cavendish's quests are still to be designed. The zones are empty placeholders.
- **Respawn point:** set to the dock (where the player arrives). The camp is the alternative.
- **Trophy stands:** only three stands exist in BaseCamp. Space is left for more, but the stand count belongs to BaseCampBuilder.
- **Brightness:** the cave is meant to be dark but readable. Judge it by playing, then adjust CaveLighting's fill or the batch lights.
