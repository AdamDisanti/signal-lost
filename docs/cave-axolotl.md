# The Cave Axolotl (Crystal Caves stage boss)

The Crystal Caves stage boss, from Amanda's 11-12 request: a huge axolotl crusted with the cave's own crystals, guarding the ship's **Flight Control Module** (the caves' main part in the design document). It plays like the Core Worm (an entrance, four phases that change the arena, a main part that clears the stage) but is built the way the Shard Mole is: a Blender model with animation clips on the `DuelBoss` base, with a boss bar, phases, poise, and the leash and reset.

It lives only in its own test scene for now. It is **not placed in the Crystal Caves level**; that waits until Amanda asks.

## Look

Adapted from Amanda's reference (a low-poly papercraft axolotl) in the flat-shaded style of the other bosses:

- A long, soft, pale peach-pink body with a paler belly, short sprawled legs and star-shaped toes.
- A wide, flat head with a little smile, black bead eyes and two nostrils.
- Three feathery coral **gills** on each side of the head, each with a fringe. They're its weak spot.
- A long, flattened tail with a fin near the tip.
- **Crystals** like the other cave enemies: a crest of hexagonal crystals down its back, a crown, a few on the flanks and hips, and a row along the tail. They use the cave's own crystal materials and glow like a batch (a light on the back and one on the tail).
- About 7.7 m long including the tail, and about 2.8 m tall at the crest. About 2,800 triangles, 26 bones and 14 clips.

## Open it

Open `Assets/Scenes/Bosses/CaveAxolotlTest.unity` and press Play.

The test cave is a 60 x 84 m walled, roofed room built from the level's cave materials. Its chamber is ringed with twelve crystal batches in all six cave colours, plus a stalagmite and three boulders. You start at the south end with the shared **Player** and **GameSystems** (HUD, dialogue, pause and settings menus). The player already has the **Crystal Disruptor** (a `StartingUpgrades` testing component), so nothing has to be bought. The lair has **Assume Parts Collected** ticked.

Walk north into the chamber to start the fight.

Menu **Signal Lost > Crystal Caves Enemies > Cave Axolotl**:

| Menu item | What it does |
| --- | --- |
| **Build Prefab** | Rebuilds `Prefabs/Enemies/Caves/Bosses/CaveAxolotl/CaveAxolotl.prefab` from the FBX and clip table, keeping the same GUID. Edits made to the prefab are lost. |
| **Create Test Scene** | Creates the test scene if it doesn't exist (it never overwrites one), then validates. |
| **Validate** | Checks the prefab's wiring, clips and materials, and the test scene. |

## How the fight runs (the lair)

`AxolotlLair` is the Crystal Caves version of the Core Worm's stage exit.

1. **Waiting:** the axolotl sleeps under a crystal in the middle of its chamber, switched off.
2. **The entrance:** walk within 6 m of the lair (the chamber's mouth) with both cave mini-boss parts (the Shard Mole's antenna array and the Beetles' signal amplifier) and the **Crystal Disruptor** scanner upgrade. Without the parts, a message tells you to find them first. Without the upgrade, a message says one of the crystals is alive and that Eugene sells the Crystal Disruptor. Amanda's call (11-12): the fight never starts without the upgrade, since you couldn't find the axolotl once it hides. The cutscene uses the shared `BossEntrance` from the Mudlands bosses (letterbox, HUD hidden, the player frozen and safe, **Space** skips). The cave rumbles and the crystal sheds dust as the camera creeps in ("One of the crystals is moving..."). It bursts out of its crystal and roars, and the name card shows: **THE CAVE AXOLOTL**, "It's guarding the ship's Flight Control Module".
3. **Retry:** if you die, or run more than twice the arena radius away, it resets (full health, phase 1) and sinks back under its crystal. Walk in again to replay the entrance.
4. **Defeat:** it drops the **Flight Control Module** as a floating pickup, plus scrap. Picking the module up records it as a main ship part and clears the Crystal Caves stage (`CrystalCaves`). The next stage isn't built yet.

## How it fights

- **Arena:** 20 m.
- **Health:** 1,200.
- **Hide:** its crystal-crusted hide takes **half** of each hit.
- **Gills:** they take **full** damage, and **double** while it's dazed.
- **Poise:** 140 damage in a short time staggers it out of the tail swipe's wind-up or a breather (not out of a roll, the shard ring or a hide).
- **No adds:** it fights alone.

### Moves

| Phase | Move | What happens | Counter |
| --- | --- | --- | --- |
| 1-4 | **Tail swipe** (melee) | Within 6.5 m. More likely when you're beside or behind it. It turns away and cocks its crystal tail with its crystals flaring (the warning), holds, then spins right round so the crystal tail scythes a full circle. 22 damage and a 3.5 m knockback. | Back off on the coil. |
| 1-4 | **Roll** (short range) | From 5 m away. It curls into a crystal-studded ball. A **line of glowing rings** on the floor shows its lane: the lane follows you for the first 60% of the warning (1.2 s), then locks. Then it rolls down the lane at 15 m/s. 26 damage and a sideways shove. **It can't be hurt while it rolls.** If it hits a wall, a rock or a spire, it crashes and lies **dazed** for about 2 s with its gills flared ("IT'S DAZED! Hit the gills!"). | Step off the lane once it locks. Lure it into a rock, then hit the gills. |
| 2-4 | **Shard ring** (long range) | It arches its back so its crystals stand up and glow brighter (the warning), then snaps down. 24 crystal shards fly out of its back in a ring at shin height, 12 m/s. 16 damage each. Shards that hit a wall stick there and sting. | Jump as the ring reaches you. Cover stops it. |
| 2-4 | **Hide** | It burrows and comes up somewhere else as a **crystal** (a real crystal batch) beside one of the chamber's batches, away from you. It takes that batch's colour: the fake crystal and every crystal on its body. It can't be hurt and does nothing until it's found. "THE AXOLOTL IS HIDING AMONG THE CRYSTALS. Raise the scanner [Q] and find it." | See below. |
| 3-4 | **Double roll** | Two rolls in a row, the second with a shorter warning (0.7 s). | As above. |
| 4 | **Frenzy combo** | A roll straight into a shard ring (unless the roll crashed). | As above. |

### Hiding and the Crystal Disruptor

It hides at **every phase change** (after the phase roar) and, from phase 2, after every **25 s** of fighting.

It works the same way as the crystal chameleons. The axolotl implements `IScannerDisruptable`:

- **Without the upgrade,** the scanner ignores it while it hides.
- **With the upgrade,** the raised scanner beeps toward it, so a player who missed it can still find it.
- It only comes up when the scanner is **raised** and within **4 m** of its fake crystal.
- **Coming up:** the crystal shatters and it bursts out. Until it lands it can't hurt you and you can't hurt it. A player within 5 m is pushed back 2.5 m, so you can't hit it straight away.
- **Testing without the scanner:** select it, then right-click the CaveAxolotl component header and choose **Disrupt**.

### Phases

| Phase | Health | Title | Arena change and moves |
| --- | --- | --- | --- |
| 1 | 100-75% | Still Pool | A clear chamber. Tail swipe and roll. |
| 2 | 75-50% | Crystal Bloom | Six crystal spires (the cave's spire mesh) grow out of the floor around the chamber. They're cover from the shards, and walls the rolls crash into. Adds the shard ring and timed hiding. |
| 3 | 50-25% | Cave-In | Every 5 s, three glowing rings appear near you: the first where you're heading, the rest within 6 m. A crystal falls from the ceiling onto each 1.3 s later (20 damage within 1.6 m). Adds double rolls. |
| 4 | 25-0% | Frenzy | Everything is faster (tempo 1.28) with shorter breathers. Adds the frenzy combo, and the shard ring fires two rings. The cave-in continues. |

The crystals glow brighter each phase. Each phase change shows a banner, and the boss bar has a notch for each phase. All numbers are serialized fields on the prefab, placeholders to tune.

### Drops

- The **Flight Control Module**, a main ship part. It rides hidden inside the boss until it drops.
- Scrap, more than the cave mini-bosses give: 5–8 scrap metal, 2–3 wiring and 2–3 circuit boards.

## How it's built

- **Model and clips:** `tools/blender/cave_axolotl.py`, built on the bosses' shared kit (`bosskit.py`). Run `blender --background --python tools/blender/cave_bosses.py -- axolotl`. It writes:
  - `CaveAxolotl.fbx` and `CaveAxolotlClips.json` to `Assets/Prefabs/Enemies/Caves/Bosses/CaveAxolotl/`;
  - `art/cave-bosses/CaveAxolotl.blend`;
  - `axolotl_sheet.png` to `art/cave-bosses/`.

  The clips are Idle, Walk, Run, Roar, TailSwipe, RollStart, Roll, RollEnd, RollCrash, ShardRing, BurrowIn, BurrowOut, Stagger and Death. The roll's spin is added in code, on top of the curled pose.
- **Unity:** `Assets/Editor/CaveAxolotlBuilder.cs` uses the same import steps as the desert and mole builders (`DesertDuelBossBuilder`). It then:
  - swaps the crystal material for the cave's `CrystalPink`;
  - adds the bone hitboxes and the gill weak spots;
  - adds the crystal lights, the scrap and the hidden module;
  - points the fake crystal at the level's own `CrystalBatch` prefab;
  - builds the test scene.
- **Scripts:**
  - `Assets/Scripts/Enemies/Caves/CaveAxolotl.cs` (a `DuelBoss` and `IScannerDisruptable`): the moves, the hiding and the arena changes.
  - `Assets/Scripts/Enemies/Caves/AxolotlLair.cs`: the entrance, the retry and the stage clear.
- **Reused, not rebuilt:**
  - `BossEntrance` (the cutscene camera);
  - `CorewormHud` (the name card and subtitles);
  - `MiniBossHud` (the boss bar and banners);
  - `EngineCorePickup` (the main part pickup and stage clear);
  - `ThornShot` (the shards);
  - `GroundMarker` (the warning rings);
  - the cave crystal batch, spire and materials;
  - `CaveEnemyKit` (the test cave, Player and GameSystems);
  - `StartingUpgrades`.

## Changes to shared code

- **`EngineCorePickup.Create`:** gets an optional last argument for the stage it clears. It defaults to the desert, so the Core Worm is unchanged.
- **`DesertDuelBossBuilder.WeakSpot`:** now `internal` (it was `private`), so the axolotl's builder can add the gills. This is the same change the Shard Mole made to the other helpers.
- **`tools/blender/cave_bosses.py`:** knows the `axolotl` key.

## Why it doesn't use BurrowEmerge

The scanner doc suggests the shared `BurrowEmerge` for disruptable enemies. That component only rises once. The axolotl burrows and rises many times in one fight, and it animates through its own clips like the Shard Mole's burrow, so it doesn't use `BurrowEmerge`.

## Placing it later

1. Drop the prefab in its deep chamber on cave floor with a NavMesh. The chamber should be at least about 44 m across, with crystal batches round its edge. It hides beside batches within its arena radius plus 6 m.
2. Add an empty object with `AxolotlLair` at the chamber's mouth, inside twice the arena radius from the boss. Set its **Axolotl** field.
3. Leave **Assume Parts Collected** unticked in the level. The Shard Mole and the Beetles both count their parts under the stage `CrystalCaves`. The player also needs the Crystal Disruptor, which Eugene sells in the caves.
4. The cave-in drops crystals from the ceiling above, or from 8 m up if there's no ceiling collider.

## Verification (11-12)

- **Blender 5.2:** the model and clips built with no unweighted vertices (2,766 triangles, 26 bones, 14 clips). The preview sheet was reviewed. After the first look, the tail crystals were moved out from inside the fin and the roll pose was curled tighter.
- **Unity 6000.6.2f1, in a disposable copy of the project:**
  - **Build Prefab**, **Create Test Scene** and **Validate** passed (`CAVE_AXOLOTL_TEST_SCENE_CREATED_AND_VALIDATED`).
  - The rendered Play Mode smoke [CaveAxolotlSmoke.cs](../tools/unity/CaveAxolotlSmoke.cs) passed (`CAVE_AXOLOTL_PLAY_MODE_PASS`). It checks:
    - it starts dormant under its crystal;
    - without the Crystal Disruptor, walking into the lair doesn't start the fight;
    - with the upgrade, walking in plays the entrance and starts the fight;
    - the tail swipe does 22;
    - the roll shows its ring lane, takes no damage while rolling, and does 26;
    - the shard ring does 16;
    - a roll into a boulder leaves it dazed: 10 damage on the gills does 20, and 10 on the hide does 5;
    - phase 2 grows the spires, and it hides beside a batch in that batch's colour;
    - the raised, upgraded scanner beeps at it from 9 m without revealing it, and brings it up at 2 m;
    - while it emerges it takes no damage and does none, and it pushes the player back;
    - phase 3 drops ceiling crystals;
    - dying resets it under its crystal at full health and clears the spires, and walking in again restarts the fight;
    - a kill drops scrap and the Flight Control Module, and picking the module up clears `CrystalCaves`.
  - The first run caught a bug: rolls slid along the NavMesh edge around a rock instead of crashing. Rolls now look ahead with a NavMesh raycast.
  - In-engine screenshots were reviewed: the dormant crystal, the ball and its lane, the shard ring arch, the crash, and the hiding crystal next to the real batch it copied.
- **To repeat it:** copy the harness into `Assets/Editor` of a disposable project under the system temp directory. Then run Unity with `-batchmode -projectPath <temp-project> -executeMethod CaveAxolotlSmoke.Start -logFile <log>`, without `-quit`.
- **Not checked by hand:**
  - how the fight feels with a real mouse and keyboard: the difficulty, how readable the tells are, and the timing of the shard-ring jump;
  - skipping the entrance with Space;
  - the Bloom spires' placement against real cave walls.

## Open questions

- **The Beetles' part stage:** the lair counts parts under `CrystalCaves`. The Shard Mole and the Beetles (on `feature/crystal-beetles`) both use it, so the gate works once both are merged.
- **Shot while hiding:** it can't be hit at all while hidden, like the chameleon.
- **Sounds:** it reuses existing effects (the roar, swishes, rock smashes). It has no sounds of its own.
- **Stage flow:** like the Core Worm, beating it only marks the stage cleared. There's no way out or next stage yet.
