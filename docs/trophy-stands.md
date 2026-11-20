# Trophy stands: one set per level

Branch: `feature/trophy-stand`

## What changed

Each level's camp now has only as many trophy stands as the player will have earned by the end of that level. Stands from earlier levels carry over, in the style of the level they came from. The caves got a fourth stand on 11-20 for the Cave Axolotl's trophy. Before this, the tutorial had 3 stands, the desert 6, the caves 9, and the Mudlands only the 3 from the base camp.

| Level | Stands in camp | Save slots (new) | Trophies earned here | Stand style |
|---|---|---|---|---|
| Tutorial | 1 | 0 | Chief Toad's Crown | wood |
| Desert Dunes | 5 | 1-4 | Old Buzzard, Burnout, Sludge, Core Worm Fang | sun-bleached driftwood, brass plate |
| Crystal Caves | 9 | 5-8 | Beetles' Elytra, Shard Mole Hide, Geode Bear Claw, Cave Axolotl Gill Frond | dark cave rock, cyan crystal inset, brass plate |
| Mudlands | 10 | 9 | Clutch's Aviators | dark bog planks, wet mud inset, rusty plate |

The Finale has no trophies.

A level's camp shows every earlier set. The caves camp, for example, shows 1 wooden, 4 driftwood and 4 cave-rock stands.

## Layout

The stands form a full ring 7 m from the fire. They are not evenly spaced: each level's stands sit 11 degrees apart, with wide gaps between levels, so the ring shows when each trophy was earned. Angles are degrees from east, counter-clockwise (90 = north):

| Level | Angles |
|---|---|
| Tutorial | 75 (north) |
| Desert | -11, 0, 11, 22 (east) |
| Caves | 285, 296, 307, 318 (south-east) |
| Mudlands | 250 (south-south-west) |

The ring keeps clear of the tent, the caves' cave-mouth and dock paths, and the desert dock.

## Where the code is

All of it is in `BaseCampBuilder.AddStands(camp, StandSet)` in `Assets/Editor/BaseCampBuilder.cs`.

- `StandAngles` holds each level's angles, so the stand count per level is the length of its array.
- `FirstStandIndex(set)` gives a set's first save slot. Slots carry on from the levels before.
- Each level's builder adds its sets:
  - Tutorial: `BaseCampBuilder.Build()` (the base camp prefab).
  - Desert: `BaseCampBuilder.BuildDesert()`.
  - Caves: `CrystalCavesBuilder.BuildCampPrefab()`.
  - Mudlands: `MudlandsBuilder.BuildCamp()`.
- Other edits:
  - `DesertStory.cs`: the Core Worm Fang highlight now only lights the desert's own stands.
  - `StageSkip.cs`: skipped trophies go on stands 0-4 (the testing tool), falling back to any free one of the ten.
  - `CrystalCavesStory.cs`, `CrystalCavesBuilder.cs`, `DesertDunesStory.cs`: the validate checks now expect 9 stands in the caves and 4 in the desert's rack.

## Each level's stands take only its own trophies

A trophy has a `Level` (`CampLevel` in `CampSave.cs`), and a stand's level comes from its slot (`CampSave.LevelOfStand`: slot 0 tutorial, 1-4 desert, 5-8 caves, 9 Mudlands). A stand only takes a trophy from its own level, so the desert's stands never take a caves trophy. The stand's own level is fixed by its slot, so this needs no scene rebuild. `StageSkip` uses the same rule when it has to fall back to a free stand.

## The camp save changed meaning

`CampSave` stores each trophy against a stand slot in `signal_lost_camp.json`. The slot numbers were reassigned, so an old save puts trophies on the wrong stands, or on stands that look out of place.

Everyone should clear their camp save once after pulling:

1. Close the game and Unity's Play mode.
2. Delete `%USERPROFILE%\AppData\LocalLow\Signal Lost\Signal Lost\signal_lost_camp.json`.
   Starting a New Game from the main menu also clears it.

Each boss drops its trophy again when beaten, as long as that trophy isn't already on a stand. Boss wins themselves aren't saved.

## Getting it on your own game file

1. Switch to the branch: `git fetch` then `git switch feature/trophy-stand` (or merge it into your branch).
2. Open the project in Unity and wait for it to finish compiling. There should be no red errors in the Console.
3. Check whether the rebuilt scenes and prefabs came with the branch. If `Assets/Scenes/Tutorial/Tutorial Demo.unity`, `Assets/Prefabs/Core/BaseCamp.prefab`, `DesertCamp.prefab` and `CrystalCavesCamp.prefab` changed in the pull, skip to step 5. If not, run step 4.
4. Rebuild, in this order. Back up any scene you've hand-edited first; the tutorial and Mudlands builds replace their scenes.
   1. **Signal Lost > Tutorial > Rebuild Tutorial Level** (rebuilds the base camp prefab too; or just **Signal Lost > Camp > Build Base Camp** if you only need the prefab).
   2. **Signal Lost > Desert > Install Desert Story**, then **Validate Desert Story**.
   3. **Signal Lost > Crystal Caves > Create Crystal Caves Level**, then **Install Crystal Caves Story**, then **Validate Crystal Caves Story**.
   4. **Signal Lost > Mudlands > Build Mudlands Level**. Run this after the caves step: it needs the Crystal Caves materials.
5. Clear your camp save (see above).

## Checking it worked

- Walk to the camp in each level and count the stands: 1 in the tutorial, 5 in the desert, 9 in the caves, 10 in the Mudlands. All should be empty after clearing the save.
- Each level's stands should match their style, and the groups should be clearly apart round the ring.
- Beat a boss, pick up its trophy and place it. It should sit on a stand from that level's group.
- Both Validate menu items should pass with no errors.

## Adding a trophy later

1. Append it to `Trophy.All` in `Assets/Scripts/Progression/CampSave.cs`. Never reuse an Id: the save file stores them.
2. Add its angle to that level's array in `StandAngles` in `BaseCampBuilder.cs`. Later levels' slot numbers shift, so clear saves after.
3. Set its `Level` in `Trophy.All`, and make `LevelOfStand` match its stand set.
4. Rebuild the camps for that level and every level after it.
