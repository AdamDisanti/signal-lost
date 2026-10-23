# Desert stage boss: the Core Worm

The boss of the Desert / Dunes stage: a 29 m **ship-fused abomination** that swallowed the ship's **Engine Core**, one of the main parts needed to finish the game. It is much harder than the three [mini-bosses](desert-minibosses.md): it has four phases, each of which changes the arena, and its attacks get denser and overlap more as the fight goes on. Owner: Lawson. The model is a low-poly placeholder built by the builder script, and all numbers are placeholders to tune.

## Look

Lawson chose a ship-fused abomination with a petal mouth, glowing core veins, and dark obsidian with molten orange.

- **Body:** one continuous, bending obsidian body (`CorewormBody`), not a chain of segments. It is rebuilt every frame along the path the head has taken. It swells behind the head, then tapers to a thin tail, with faint ridges and a flatter belly.
- **Core veins:** four molten-orange veins wind down its sides and belly. They pulse, and burn brighter each phase and whenever the core charges.
- **The ship grown into it:**
  - hull plates along its back
  - metal pipe clamps around it
  - two ship cables running down its spine
  - two engine housings with glowing intakes
  - three exhaust stacks that smoke (and belch fire once the core runs hot in phase 4)
  - a rocket nozzle for a tail
- **Head:** an armored obsidian skull, 15% bigger than before. It has a glowing molten lip, molten cracks running back over it, a frill of bent hull fins and two smoking exhaust pipes behind it, an engine intake grown into its crown, and ship-metal teeth. The three petal jaws have molten veins inside and open on the glowing Engine Core in its throat.
- **Weak spots:** glowing intake vents, set in metal frames on both sides of four points along the body.

The body mesh only exists in Play mode (it is built at runtime), so in the Scene view outside Play you see the head and ship parts on their own.

## How it plays out

1. **Leaving the stage:** with the three mini-bosses' ship parts, walk to the stage exit (a ship beacon) to take them back to the ship.
2. **The ambush (cutscene):** the ground rumbles and the Core Worm rockets out of the sand right in front of the exit. It roars, lunges and swallows you. Then: *"Dragged beneath the dunes..."*.
3. **Its arena:** you land at the edge of a sunken bowl. It bursts out in the middle, the name card shows, and the fight begins.
4. **If you die,** you respawn at the stage start, the arena resets, and the worm waits under the exit again.
5. **Defeat:** its core overloads and blows apart, the body crashes down and sinks, and the **Engine Core** drops as a floating, glowing pickup.
6. **Pick it up:** it is recorded as a main ship part, the desert is marked cleared, and the next stage is unlocked. You are faded back to the exit, which now leaves the stage. The next stage doesn't exist yet: until **Next Stage Scene** is set on the exit (and the scene is in Build Settings), a banner says the stage is unlocked but not built.

## Generate it

1. Open the project in Unity **6000.6.2f1** on `feature/desert-stage-boss` and let scripts compile.
2. Choose **Signal Lost > Desert Boss > Create Test Scene**. The Console prints `DESERT_BOSS_TEST_SCENE_CREATED_AND_VALIDATED`.

It creates:

- `Assets/Prefabs/Enemies/Desert/Boss/Coreworm.prefab`, with its meshes and materials in the desert enemy folders.
- `Assets/Scenes/Bosses/CorewormTest.unity`, which has two areas:
  - an 80 m stretch of desert canyon. You start at the south end, and the exit beacon is at the north end.
  - far away, the worm's round arena: 68 m across with a floor of 5 m tiles (these collapse), a ring of dune walls, six rock pillars and three hull slabs for cover, a NavMesh and a fodder spawner.

In the test scene, **Assume Parts Collected** is ticked on the exit, so you can walk straight up and get ambushed. **Regenerate Prefab** rebuilds the worm (same GUID), then re-links the test scene's worm to its arena, exit and fodder spawner if needed. **Validate** re-runs the checks. Commit the generated files with their `.meta` files.

## Where you can hurt it

| Part | Damage taken |
| --- | --- |
| Hide and hull plating (head, body) | **15%** |
| Glowing **vents** on its sides (4 segments, both sides), only while above the sand | **100%** |
| The **Engine Core** in its throat, mouth closed | 40% |
| The **Engine Core**, mouth open (after charges and spits, during the beam, after the nova) | **160%** |

Sparks fly when you hit a vent or the core. It has **825 HP**, lowered from 2200 at Lawson's request. Between phases it is invulnerable for a moment while it roars and the arena changes.

## The phases

### Phase 1: The Hunt (100–75%)
Clear, open dunes. A sand ridge racing under the surface shows where it is between attacks.

| Attack | What happens | Counter |
| --- | --- | --- |
| **Burrow charge** | Swims at you, leading your movement. A ring appears for 0.6 s, then it erupts under you (28 and a shove) and rears up with its mouth open for about 1.5 s. | Leave the ring, then hit the open core. |
| **Breach arc** | A ring where it will come up and a big ring where it will land. It leaps out in a 14 m-high arc over you. You take damage if it hits you in the air, and the landing does 24 in 5 m. Debris rains down around you with warning rings. | Clear both rings, then watch for falling debris. |
| **Core spit** | Rears up 14 m away, opens its mouth and lobs 4 globs of molten slag, the first one at you, each marked by a ring. Each bursts (14) and leaves a fire. | Keep moving, then hit the core while the mouth hangs open. |
| **Body slam** | Surfaces beside you, rears up, and a line of rings shows where its body will land. It slams along the line (28). It then lies across the sand with its vents exposed. | Step off the line, then hit the vents. |

### Phase 2: Sandstorm (75–50%)
- **Arena:** a sandstorm rolls in. The fog gets thick, sand streams past, and a steady wind pushes you. Three **sand cyclones** wander the arena, dragging you toward them; their eye grinds you and blinds you with sand. **Fodder** (Spark Ticks, Hubcap Armadillos, Jerry-Can Beetles) pours in.
- **Ambush:** three or four ridges converge on you at once. All but one are fakes that end in sand geysers (10). The real one has an orange glint in its trail and erupts (28).
- **Also:** burrow charges, breach arcs, and spits of 6 globs.

### Phase 3: Collapse (50–25%)
- **Arena:** the storm clears and the fodder is cleared away. Floor tiles near you shake, darken and puff dust for 1.8 s, then fall away in waves until about half the floor is gone. The floor around the pillars, the hull slabs and the drop point never falls. Falling into a pit costs 20 and puts you back on the nearest solid floor. You can see the worm swimming in the pits.
- **Core beam:** rears up, charges for about 1.4 s (core brightening, sparks gathering), then sweeps a beam 140° across the arena (9 every 0.2 s), leaving a line of fire. Pillars block it.
- **Shrapnel barrage:** its plating fires white-hot shrapnel at chest height (jumping won't help) in fans, rings with a gap, and spirals. Cover stops it.
- **Beam and barrage together:** rings of shrapnel during the beam sweep.
- **Also:** breach arcs and burrow charges over the pits.

### Phase 4: Meltdown (25–0%)
- **Arena:** the core is melting down and the sand is on fire. Every 2.6 s a lane of fire is marked across the arena, through or near you, and then bursts into flame for 6 s. Safe spots keep moving.
- **Combos with almost no gaps:** beam and barrage, charge then slam, spit with a shrapnel spiral, breaches and barrages. Everything is faster.
- **Core nova:** every fourth attack. It rears up in the middle: "THE CORE IS OVERLOADING! GET BEHIND COVER!". The core charges for 3 s (white-hot, sparks, the screen shaking harder), then detonates: **55 damage across 50 m**, unless solid cover is between you and the core. Afterwards it collapses, **stunned for 3.5 s with its mouth open**: the big opening.

## Presentation

- **Cutscene:** a cutscene camera, the rumble, the eruption, the roar, the lunge into the camera, a fade to black, narration lines, and the reveal in the arena with a name card.
- **Boss bar:** a wide bar across the top of the screen, split into four parts by phase notches, showing the phase name. It changes colour each phase and says "(invulnerable)" between phases.
- **Effects:**
  - **Sand:** geysers and dust rings where it breaks the surface, and the racing sand ridge.
  - **Core:** the glow brightens as it charges, and its light flickers.
  - **Projectiles:** the beam is a glowing line with a flame jet at the mouth and sparks where it hits; slag globs trail flame; shrapnel leaves glowing streaks.
  - **Arena:** sandstorm particles and fog, swirling cyclones, collapsing tiles, fire lanes.
  - **Screen shake** for every eruption, slam and blast.
- **Death:** the core flickers out of control, then an explosion. The body crashes and sinks segment by segment, and the core floats free.

## Progression added

`StageProgress` is a **placeholder** for the current Play session only, since there is no save system yet.

- It records the stages cleared and unlocked, and the main ship parts (`RecoverMainPart`, `MainShipParts`).
- It counts the stage's mini-boss parts (`StagePartsRecovered`, read from `ShipPartDrop.Recovered`).
- Picking up the Engine Core calls `RecoverMainPart("Engine Core")`, then `ClearStage("Desert", "NextStage")`, which raises `StageUnlocked`.
- The exit loads **Next Stage Scene** once one exists.

## Files

- `Scripts/Enemies/Desert/Boss/`:
  - `Coreworm` (the boss)
  - `CorewormBody` (the continuous body mesh, veins and cables)
  - `CorewormArena` (the arena changes)
  - `DesertStageExit` (the exit, the ambush cutscene, retry and the return)
  - `CorewormHud` (bar, name card, fades, subtitles)
  - `CoreSlag`, `CoreShrapnel`, `SandCyclone`
  - `EngineCorePickup`
- `Scripts/Progression/StageProgress.cs`
- `Scripts/Effects/CameraShake.cs`
- `Editor/DesertBossBuilder.cs`

It reuses the fire effect and particle bursts, `FireZone`, `FuelShell.Blast` (the line-of-sight blast), `ScrapBomb`, `GroundMarker`, `ExpandingRing`, `MiniBossHud` banners and the desert fodder.

## Manual acceptance checks

1. Play `CorewormTest`. Walk north to the beacon: the ground shakes, the worm erupts in front of the exit, roars and lunges at you, and the screen goes black. *"Dragged beneath the dunes"* shows, then you are in the arena. The worm erupts, the name card shows, and you have control again with the boss bar showing.
2. **Phase 1:** each attack's ring or line warns before it hits. Hits on its hide barely move the bar; hits on a vent or the open core do.
3. **At 75%:** "PHASE 2: SANDSTORM". Fog, wind, three cyclones that pull you, and fodder. In ambushes, the ridge with orange sparks is the real one.
4. **At 50%:** the storm and fodder clear. Tiles shake, then drop. Falling in hurts and puts you back on solid floor. Beam sweeps are blocked by pillars, and shrapnel patterns can be weaved through or blocked by cover.
5. **At 25%:** fire lanes keep appearing. The core nova does a big hit in the open and nothing behind a pillar. Afterwards it lies stunned, mouth open.
6. **Die at any point:** you respawn at the canyon start, and the arena is back to normal. Walk to the exit again for the ambush.
7. **Defeat it:** it explodes and sinks, and the Engine Core floats up. Pick it up: "RECOVERED: ENGINE CORE". A moment later you are back at the exit. Walk into it: "DESERT CLEARED! NextStage unlocked (it hasn't been built yet)."
8. No Console errors. Frame rate stays reasonable in phase 4 with many fires (the light budget caps fire lights at 8).

## Open questions

- **Placement:** this boss and its arena belong at the end of Nicole's Desert Dunes scene. The exit and arena can move there as they are, but the change needs coordinating with her.
- **Parts gate:** the exit requires the three mini-boss parts, counted from `ShipPartDrop.Recovered`. Should it also require anything else?
- **Saving:** progression is per Play session only. How the Engine Core and the cleared stage persist across sessions, deaths and the next stage is undecided.
- **Next stage:** the exit calls it `NextStage` and loads nothing until a scene is named.
- **Crosshair target bar:** the shared HUD's target bar shows the hit box you are aiming at, which never drops (the same as with the mini-bosses' weak spots). The boss bar at the top is the real one.
- **Difficulty:** all damage, timings and HP are first guesses for "substantially harder", not tuned.

## Validation performed

Every script, including the whole project, was compiled with the C# compiler against the Unity **6000.6.2f1** assemblies, with the project's defines and C# 9: no errors and no new warnings. **Nothing has been run in Unity yet.** Generating the prefab and scene, the cutscene, the worm's motion, the arena changes, balance and performance all need a first run in the Editor.
