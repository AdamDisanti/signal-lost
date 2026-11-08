# Islands, docks and the sea

Lawson, 11-07: each stage is its own island in the sea, instead of being walled in by cliffs and dunes. A dock at the end of a stage has a boat to the next island. Using the boat plays a short voyage cutscene that zooms back in on the new island.

Lawson, 11-08:
- The player boards the boat in person, and Cavendish and Eugene sail along.
- They pitch Cavendish's camp beside the dock on the next island.
- Everyone dresses for the island they are on.

## What the player sees

- **Beach and sea.** The land eases down onto a sandy beach and into clear turquoise shallows. Past the shallows the bottom drops off into deep blue water. The next island sits on the horizon.
- **No swimming.**
  - The player can wade out across the shallows, which run about 13 m before they get too deep.
  - Once their feet are more than **1.3 m** under the surface, the screen fades to black ("TOO DEEP"), and they lose **15 health**.
  - They then reappear on the nearest dry land, facing inland.
  - If that damage kills them, the normal death and respawn takes over.
  - Low ground inland, such as the camp or the merchant's hollow, never counts as sea, even where it lies below sea level.
- **Docks.**
  - Each dock is a timber jetty on posts with a T-shaped end, bollards, a lantern, barrels, a crate and a coil of rope.
  - A small sailboat is tied up alongside the end.
  - At a departure dock, look at the boat and press **E** to "Sail to …".
  - Until the stage is cleared, it says why it can't sail.
- **The voyage**, about 35 s in all (press **Jump** to skip ahead):
  1. **Boarding.**
     - The view leaves the player's eyes for a shot back down the dock from the water.
     - The mechanic's own body steps aboard and takes the tiller. It is the mechanic figure from the pockets menu, in this island's outfit. Its suit is one seamless piece, so he waddles rather than strides.
     - Cavendish and Eugene come down the dock from camp and climb in. Cavendish sits on the stern thwart, Eugene on the middle one.
     - Eugene comes only once he is a friendly trader.
  2. **Casting off.** The boat sails out with all of them aboard, with a wake behind it. The camera cranes up into a wide aerial shot, with the island behind and the next one ahead.
  3. **The crossing.** The screen fades to "Sailing to …" while the next stage loads.
  4. **Arrival.**
     - On the new island it fades in on a wide shot of the boat sailing up to the arrival dock, with the crew now dressed for this island. The camera follows the boat in, and everyone steps ashore.
     - A cut to the camp site: Cavendish and Eugene pitch camp. Each piece pops up out of a puff of sand, and then they sit down by the fire.
     - A cut back to the mechanic on the dock, then down into the player's eyes, and control returns.
- **Cavendish's camp on each island.**
  - The same base camp (fire, tent, two logs, supplies, three trophy stands, all in the same places), themed for the island.
  - It stands just inland of the arrival dock.
  - Arriving by boat, it starts packed away and goes up in the cutscene. Loaded any other way, it is already up, with Cavendish and Eugene sitting by the fire.
  - Cavendish makes small talk. Eugene runs his shop.
  - The stands show your saved trophies.
- **Island outfits.**
  - **Crash Meadow** is the look everyone starts in.
  - **Desert Dunes:**
    - **The mechanic:** a khaki suit with rust trim, a bush hat with goggles on the band, a neckerchief and suede gloves. This shows on the first-person sleeves, the pockets figure and the boat.
    - **Cavendish:** a sand-and-terracotta suit, his helmet frame and lamp off, a straw sun hat (ears through the brim), sunglasses and a neckerchief.
    - **Eugene:** a striped beach parasol planted in his bucket, goggles on his eye stalks and a bandana.
- **Water look.** The water is low-poly to match the rest of the game: faceted swells, crisp toon foam and glints. It is see-through over sand in the shallows and deep blue further out. It shows moving caustics on the sea floor and foam wherever it meets the beach, rocks or dock posts, with wash lines rolling in to the shore. The pond uses the same shader.

## The stages

| Stage | Departure dock | Arrival dock | Boat needs |
|---|---|---|---|
| Tutorial (Crash Meadow) | Straight out behind Cavendish's camp (south-west), at the end of a cleared dirt trail | None: the game starts at the crash | The tutorial finished (`StageProgress.Tutorial` cleared, set when the demo-complete step runs) |
| Desert Dunes | North beach past the biome exit marker; it sails to the Fog Forest, which isn't built yet | South beach at the player's start | The desert cleared (the Engine Core) |
| Crystal Caves ([page](crystal-caves.md)) | None yet (the level's flow is still to be designed) | South-east, at the foot of a rocky cove below the camp | n/a |

The tutorial's mountain ring and the desert's perimeter dunes and entrance canyon are gone. The forest belt in the tutorial now stands behind the beach.

## How it is built

All the art is generated by code; no asset packs were imported.

- `Editor/IslandKit.cs` holds the shared pieces:
  - the beach profile (`Shore`);
  - the sea mesh and its sea mask (`BuildOcean`);
  - the dock and boat (`BuildDock`, `BoatPrefab`);
  - the far islands (`DistantIsland`);
  - the not-walkable sea floor (`NoWalk`).
- Meshes and materials go in `Prefabs/Enviornment objects/Islands/`.
- **Tutorial:** `Signal Lost > Tutorial > Rebuild Tutorial Level` (it replaces the scene, as before).
- **Desert:** `Signal Lost > Make Desert An Island`.
  - It regenerates `DesertEnvironment.prefab`, which now holds the sea, the docks and the far islands.
  - It moves the scene's player and the placeholder markers onto the new ground.
  - It sets the player's camera to show the sky.
  - It installs Cavendish's camp: see the desert camp below.
  - The rest of the scene is untouched.
- **Crystal Caves:** `Signal Lost > Crystal Caves > Create Crystal Caves Level` builds the island with this kit: the sea, the sea floor (not walkable) and the arrival dock with its boat. It has rocky sea cliffs, not a beach; a cove of low rocks steps down from the ledge to the dock. Its camp, `Prefabs/Core/CrystalCavesCamp.prefab`, is a prefab variant of the base camp with an `IslandCamp`, so the crew can arrive and pitch it. It has no island theme or outfits yet. See [Crystal Caves](crystal-caves.md).
- **Desert camp:** `Signal Lost > Desert > Install Cavendish's Camp`.
  - It rebuilds `Prefabs/Core/DesertCamp.prefab` (`Signal Lost > Camp > Build Desert Camp`; its meshes and materials go in `Prefabs/Core/DesertCamp/`).
  - It places the camp by the arrival dock, with Cavendish and Eugene seated.
  - It adds the scene's `StageTheme` (Desert).
  - It removes the old Cavendish and merchant-spot placeholders.
- **The boat** (`IslandKit.BoatPrefab`):
  - Modelled at 5 m and drawn at `BoatScale` (2x) in a "Hull" child, so the root, and anyone seated aboard, stays unscaled.
  - Seat markers: Helm, Cavendish Seat, Merchant Seat.
  - Its boom rides high, over the crew.
  - Docks moor it further out and have a "Boarding" marker on the edge beside it.
- **Runtime** (`Scripts/World/`):
  - `Ocean`: the sea level, wade depth, drown damage and sea mask.
  - `DeepWaterGuard`: the rescue; added to the player automatically.
  - `IslandDock` and `DockBoat`.
  - `IslandVoyage`: the cutscene, including walking the crew aboard and ashore.
  - `IslandCamp`: a camp that packs away while the crew is at sea and sets itself up.
  - `StageTheme` and `Outfits`: each island's look, and the clothes for it. NPCs dress themselves on Start (`NpcActor`), and the first-person hands read the look in Awake.
  - `Player/MechanicModel`: the mechanic's body and hands (from the player model revamp), shared by the hands, the pockets figure and the voyage. `BuildFigure` takes an island outfit: colours, and a bush hat and neckerchief instead of the cap.
  - `UI/ScreenFade`: the fade that carries across the scene load.
- The sea floor is its own object, marked **Not Walkable**, so the NavMesh, enemies and spawns stay on land. `SpawnSafety` also refuses spots under the sea.
  - Only ground past the coastline counts as sea floor. Inland dips below sea level, such as the merchant's hollow and desert troughs, stay walkable. An earlier build split by height alone and left the hollow's floor with no NavMesh, so the crabs there could not path.
- **Shader:** `Shaders/StylizedWater.shader` uses the camera depth texture, which is on in the PC pipeline asset. With **Use Depth** off, or on the mobile asset, it falls back to the shore value the builders bake into the mesh.

## Test checklist

1. Tutorial: walk from the camp down the trail to the beach and onto the dock. The sand meets the water with foam, caustics show in the shallows, and dock posts have foam rings.
2. Wade out past the dock: the screen goes black ("TOO DEEP"), you lose 15 health and reappear on dry land facing inland. Do the same anywhere else around the coast.
3. Look at the boat before finishing the tutorial: "Boat (not ready to sail)", and E says Cavendish still needs you.
4. Finish the tutorial (or set `IslandDock.IgnoreRequirements`): E on the boat plays the voyage and lands you on the desert's arrival dock with control back.
   - The mechanic, Cavendish and Eugene board, sail, and step ashore.
   - The desert camp goes up and they sit by the fire.
   - Try Jump to skip.
5. In the desert, check the outfits:
   - The sleeves are khaki.
   - Cavendish wears his sun hat and sunglasses; Eugene has his parasol and goggles.
   - Open the pockets menu: the figure wears the bush hat.
   - Back in the tutorial, everything is as before.
6. Desert: walk the beach; the old dune wall is gone. The north boat says to recover the Engine Core.
7. Enemies never appear in or under the water.

Checked on a disposable copy of the project in batch mode:
- An island probe covered the shots above, the deep-water rescue (15 health, back on dry land), sea-mask checks for the camp and hollow, refused sea spawns, and the whole voyage from tutorial to desert.
- The core-systems, tutorial-story, Chief Toad and combat-rules smokes passed.
- Not yet hand-played in the editor.
