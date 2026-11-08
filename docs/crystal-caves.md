# Crystal Caves level (world only)

The third biome, from Amanda's 11-08 design map and reference images (sea cliffs, low-poly caves lit by crystals), revised on 11-09. The design document calls it **Crystal Caverns** (boss: Crystal-infused Cave Beast, reward: Flight Control Module).

This is the **world only**: landscape, camp, dock and lighting. Enemies, quests and level flow come later, and the places for them are marked but empty.

It is one of the [islands](islands.md): it uses the shared island kit for the sea, the dock, the boat and the deep-water rescue, and Cavendish's island camp. All art is generated in code, in the same style as the tutorial and the desert; no asset packs were imported.

## Create or rebuild it

Menu **Signal Lost → Crystal Caves**:

| Menu item | What it does |
| --- | --- |
| **Create Crystal Caves Level** | Builds anything missing, then creates `Assets/Scenes/Large Areas/CrystalCaves.unity`, bakes its NavMesh and validates it. It refuses to overwrite an existing scene. |
| **Rebuild Crystal Caves Environment** | Regenerates `CrystalCavesEnvironment.prefab` (same seed, same layout), throwing away edits made to the prefab, and rebuilds the camp prefab. The scene keeps its instance. Re-bake the NavMesh afterwards: select **Navigation** and press **Bake**. |
| **Build Crystal and Rock Prefabs** | Rebuilds the crystal and cave-rock kit. |

This follows the Desert Dunes workflow (the project lead's choice): the landscape is a prefab you can rebuild from code, and the scene is created once and then edited by hand. Add enemies, quests and triggers in the scene; they survive environment rebuilds.

## Layout

The map is **480 × 480 m**, 1.5 times Desert Dunes' 320 m, with 4 m flat-shaded facets. +Z is north and sea level is y = 0.

| Area | Where (x, z) | What's there |
| --- | --- | --- |
| Ocean | All round | The island kit's sea. Falling or wading into deep water fades to "TOO DEEP", costs 15 health and puts you back on the nearest dry land. The sea floor is not walkable, so enemies and spawns stay on land. |
| Mountains | All round the island | Jagged peaks rising straight from the sea everywhere except the south-east ledge, so they close the outside in. They climb from about 60 m at the ledge to 150 m and more, and use the tutorial's mountain rock. |
| South-east ledge | Around (130, −172) | The only open ground outside: a small rocky ledge about 10–18 m above the sea, stepped like stacked blocks. Columnar sea cliffs line its edge, with tumbled blocks in the surf, a few pines, bushes, grass and rocks. |
| Cove and dock | Cove from (127, −180) down to (140, −216); dock straight on from it | Low rocks step down from the ledge to the water, with rocks along both sides. At the bottom the island kit's arrival dock (a timber jetty with a T-shaped end) runs out to sea, with the sailboat moored alongside. The cove points at the cave mouth, so the mouth can be seen from the dock. Loaded directly, the player starts at the foot of the cove, facing the mouth; arriving by boat, the voyage lands them on the dock. |
| Cavendish's camp | (150, −166) | The base camp (fire, tent, logs, supplies, three trophy stands that remember trophies from earlier levels) as an island camp. Cavendish sits on his log; Eugene, a friendly trader, sits on the other. Arriving by boat, they pitch it in the voyage cutscene. A 16 m clear radius is left round it for the stands of later levels. |
| Cave mouth | Portal at (118, −128), facing south | A dark arch at the foot of the mountains, with crystal glow inside. No path leads to it. |
| Cave, upper level | The front of the cave (south of z ≈ −55) and a lobe round the beetles' zone | Floor about 18 m above sea level, with the ceiling about 32 m above that. The back of the cave has no upper level, so nothing up there overlooks mini boss 2 or the final boss. |
| Cave, deep level | Everything else, out to the cave walls | Floor about 16 m below the upper level, with a vaulted ceiling about 70 m up. The upper level's edge is a cliff, except at **three ramps**: two from the front, at x = 40 and x = −45, and one from the beetles' lobe. |
| Beetles fight zone | (−150, −42), r 24 m, upper level | Flattened, empty, ringed by purple and pink crystals. It sits in the middle of a lobe 62 m in radius, so its edge is about 38 m from any drop. |
| Mini Boss 2 fight zone | (80, 35), r 24 m, deep level | Flattened, empty, ringed by orange and pink crystals. It is about 90 m from the nearest upper ground. |
| Final boss fight zone | (0, 138), 152 × 68 m ellipse, deep level | Flattened, empty, ringed by purple and blue crystals, at the back of the cave. |

Each fight zone has a **CombatZone** marker under *Level Markers (placeholders)*, so enemies placed later can use it. Its rings show in the Scene view.

To move things, edit the numbers at the top of `Editor/CrystalCavesTerrain.cs` (positions, sizes, ramps, the cove, heights) and rebuild the environment.

The terrain self-check runs on every build. It refuses a landform that has:
- any cave ground open to the sky;
- a ramp that is too steep or doesn't reach the deep level;
- the beetles' zone within 10 m of a drop;
- upper ground near the boss zones;
- a low tunnel;
- a cove that doesn't reach the water;
- the cave mouth hidden from the dock;
- land at the map edge.

## Cave lighting

**CaveLighting** (on the environment prefab) gives the cave its own light inside the sunlit level:
- **Fade-in:** once the camera is under the cave roof, the sun, sky light, reflections and fog fade over the first 40 m from the mouth to the cave settings.
- **Fill light:** a bright, even fill keeps every part of the cave easy to see and navigate.

**Crystal batches** (185) tint the fill with colour:
- **Placement:** they line the tunnel (on alternating walls), mark the top and foot of every ramp, ring the fight zones, and are scattered over the floors, walls and ceiling.
- **Shapes:** three crystal shapes (spire, cluster, shard), tilted and sized differently.
- **Colours:** six, namely dark blue, green, orange, purple, pink and cyan. Each area leans toward a few colours, and about one batch in eight is any colour.
- **Rock:** stalagmites, stalactites, boulders and huge floor-to-ceiling columns break up the space.

Crystal lights more than 70 m from the mouth switch off while you are outside. They have no shadows, so they would otherwise shine through the mountain.

### Adding or changing crystals

- **Add:** drop `Prefabs/Enviornment objects/CrystalCaves/Crystals/CrystalBatch.prefab` anywhere.
- **Recolour:** choose a material from `CrystalCaves/Materials/Crystal*.mat` on its **CrystalBatch** component. Its crystals and its light change together.
- **Reshape:** add, move or delete the crystals under a batch freely.
- **Brightness:** set it on the batch's **Glow** light. Keep the light above the crystals, because inside one it only lights that crystal from within.
- **Light budget:** stay under about 220 crystal lights. Forward+ has a per-camera light budget, and validation checks this.

## Inspector settings

**CrystalBatch** (`Scripts/Effects/CrystalBatch.cs`, on each crystal batch)

| Field | What it does |
| --- | --- |
| Crystal Material | One of the six `Crystal*.mat`. Changing it recolours every crystal under the batch and sets the glow light to the matching colour. |
| Glow | The batch's point light. Its own Range and Intensity set how far and how bright it shines. |

**CaveLighting** (`Scripts/Effects/CaveLighting.cs`, on *Cave Lighting* in the environment prefab)

| Field | What it does |
| --- | --- |
| Roof | The cave roof collider. The camera counts as inside the cave whenever this collider is directly above it. |
| Entrance | The cave mouth. The outdoor light fades out over Blend Distance from here. |
| Blend Distance | How far into the cave (in metres) the fade takes. |
| Sun | Leave empty to use the scene's sun from the Lighting settings. |
| Crystals | Parent of the crystal batches whose lights it switches. |
| Always Lit Radius | Crystal lights within this distance of the entrance stay on outside, so the mouth glows from the dock. Lights farther in turn on only when you enter. |
| Inside the cave: Sky / Equator / Ground Light | The cave's fill light. Sky lights floors, Equator walls and Ground ceilings. Raise them to brighten the cave, lower them to darken it. |
| Inside the cave: Fog Colour / Start / End | The fog inside the cave. |

The outdoor look is whatever the scene's Lighting settings are; CaveLighting reads them when the scene starts.

## Shared pieces, reused

- **Player and UI:** the shared **Player** (laser, scanner, flask; all three from the start) and **GameSystems** (status-bar HUD, dialogue, pause and settings menus) from `Prefabs/Core`, the same as in every level.
- **Scanner:** level range 200 m, like the tutorial.
- **Island kit** (`Editor/IslandKit.cs`; see [islands](islands.md)):
  - the sea, with the `Ocean` component and its sea mask;
  - the deep-water rescue, which the sea adds to the player at runtime;
  - the arrival dock (`IslandDock`, role Arrival, island name "The Crystal Caves") with its sailboat;
  - the not-walkable sea floor.
- **Cavendish's camp** (`Prefabs/Core/CrystalCavesCamp.prefab`):
  - It is a **prefab variant of BaseCamp** with an `IslandCamp`, so changes to the base camp carry over.
  - It is set up the same way as the desert camp: seats, pitching spots, and the order the pieces go up in.
  - Cavendish (small talk only) and Eugene (friendly from the start, with no ambush cousins at island camps) are linked to it, as in the desert.
- **No island theme yet:** there is no `StageTheme`, so the crew wears its Crash Meadow look here. Crystal Caves outfits and camp styling are still to be designed.
- **Build list:** the scene is in the build list (MainMenu, Tutorial, Sandbox, Desert, **Crystal Caves**, Prototype). No voyage sails here yet, because the Fog Forest before it isn't built.

## Reusing the pieces in other levels

### A cave in another level

1. Give the level a roof collider over the cave, a mouth Transform and a parent for its crystals.
2. Add a **CaveLighting** component and point Roof, Entrance and Crystals at them.
3. Fill the cave with `CrystalBatch.prefab` copies in a mix of colours.

### Starting a new level from this one

`CrystalCavesBuilder.Create` is the template. The steps every level needs:
1. Instance the shared **Player**, **GameSystems** and a **PlayerSpawnPoint**.
2. Build the island with `IslandKit`:
   - the sea (`BuildOcean`);
   - the sea floor as its own object, marked `NoWalk`;
   - the arrival and departure docks (`BuildDock`), placed with `ShoreStart`.
3. Place an island camp (a camp prefab with an `IslandCamp`), with Cavendish on his seat (`IslandCamp.SitCavendish`) and a friendly Eugene linked to it.
4. Bake a **NavMeshSurface** (with **NavMeshBaker**) and store its data with Git LFS.
5. Add the scene to `CoreSceneSetup.SetBuildScenes`.

## Editing tips

- **Always edit in the scene.** Hand edits there (enemies, quests, moved camp props) are kept.
- **Don't edit the environment prefab by hand.** **Rebuild Crystal Caves Environment** overwrites it, so change `CrystalCavesTerrain.cs` or `CrystalCavesBuilder.cs` instead.
  - After a rebuild, re-bake the NavMesh: select *Navigation* and press **Bake**.
  - Objects inside the prefab may also get new IDs, so re-check any scene references to them.
- **Starting over:** delete `CrystalCaves.unity` and run **Create Crystal Caves Level** again.
- **Validation:** **Create** ends by validating the scene. It checks:
  - the shared pieces, the sea, the dock and boat, the camp and its residents, and the crystals;
  - that the end of the dock, the camp, the cave mouth, the foot of every ramp and all three fight zones can be reached on foot from the spawn.
  - **Batch builds:** run `SignalLost.Editor.CrystalCavesBuilder.CreateBatch` with `-executeMethod`. To validate an existing scene, run `CrystalCavesBuilder.Validate` with `-executeMethod` and `-quit`.
- **The NavMesh file:** `CrystalCavesNavMesh.asset` is stored in Git LFS. Run `git lfs install` before you pull.

## Files

| File | What it is |
| --- | --- |
| `Editor/CrystalCavesTerrain.cs` | Layout numbers and the landform: ground, sea floor, cave ceiling and mountain top, the cove, the two cave levels and ramps, and the self-check |
| `Editor/CrystalCavesBuilder.cs` | The environment prefab (island kit pieces, props and crystal placement), the camp prefab, and the scene (lighting, player, camp and residents, NavMesh, markers, validation) |
| `Editor/CrystalAssetBuilder.cs` | Crystal shapes and materials, CrystalBatch prefab, cave and coast rock |
| `Scripts/Effects/CaveLighting.cs`, `CrystalBatch.cs` | Runtime cave lighting and crystal colour sync |
| `Prefabs/Enviornment objects/CrystalCaves/` | Environment prefab, meshes, materials, crystals, rocks |
| `Prefabs/Core/CrystalCavesCamp.prefab` | Cavendish's camp here (base camp variant with an `IslandCamp`) |
| `Prefabs/Enviornment objects/Islands/Meshes/CrystalCavesDock.asset`, `CrystalCavesOcean.asset` | This island's dock and sea meshes (from the island kit) |
| `Scenes/Large Areas/CrystalCaves.unity`, `CrystalCavesNavMesh.asset` | The scene and its baked NavMesh (Git LFS) |

## Verification (11-09, Unity 6000.6.2f1, batch mode)

- **Create Crystal Caves Level:** passed its validation, and the terrain self-check passed.
  - It found one Player, GameSystems, spawn point, camera and listener.
  - It found the sea, the arrival dock with its boat, CaveLighting, three trophy stands, and the island camp with Cavendish, a friendly Eugene and their seats.
  - The NavMesh was baked, and 185 crystal lights were found in all six colours.
  - NavMesh paths from the spawn reach the end of the dock, the camp, the inside of the cave mouth, the foot of all three ramps and all three fight zones.
- **Play Mode smoke run:** a temporary script, not committed. It passed with no Console errors.
  - Outside, the sun was on and the 152 deep crystal lights were off.
  - Inside, the sun turned off, the cave fill and fog applied, and all deep lights came on.
  - Stepping into deep water off the dock rescued the player onto the dock (100 → 85 health), and the daylight came back.
- **Rendered review shots:** checked from the cove, the dock, the sea, the camp, the area left of the entrance (now roofed), the tunnel, both cave levels, the ramps and the fight zones.

**Not checked:**
- physical keyboard and mouse play;
- the boat voyage arriving here (no stage sails here yet);
- frame rate on target hardware;
- how the light levels look on different monitors.

## Open questions

- **Level flow:** where the boat comes from (the Fog Forest isn't built), whether this island needs a departure dock, and where the exit is.
- **Enemies and quests:** the beetles, Mini Boss 2 (and any others), the final boss and Cavendish's quests are still to be designed. The zones are empty placeholders.
- **Island look:** Crystal Caves outfits for the crew and camp styling (the desert has both). The crew wears the Crash Meadow look here for now.
- **Respawn point:** set to the foot of the cove by the dock. The camp is the alternative.
- **Trophy stands:** only three stands exist in the base camp. Space is left for more, but the stand count belongs to BaseCampBuilder.
