# Desert stage boss: the Core Worm

The boss of the Desert / Dunes stage: a 29 m sandworm that swallowed the ship's **Engine Core**, one of the main parts needed to finish the game. It is much harder than the three [mini-bosses](desert-minibosses.md): it has four phases, each of which changes the arena, and its attacks get denser and overlap more as the fight goes on. Owner: Lawson. The model is a low-poly placeholder built by the builder script, and all numbers are placeholders to tune.

## Look

Lawson's direction: a desert-coloured worm, darker than the sand for contrast, with veins of energy pulsing across its body. It is biological rather than mechanical, keeps the petal mouth, and has one solid bending body rather than segments.

- **Body:** one continuous, bending body (`CorewormBody`) in dark dune brown, rebuilt every frame along the path the head has taken.
  - It swells behind the head and tapers to a fine tail, ringed with soft annular ridges like an earthworm's, with a slightly flatter belly.
  - The path is resampled and smoothed first, so tight turns curve instead of kinking. This fixes the jagged tail at the end of turns.
- **Energy veins:** six glowing veins wind and spiral over the body, crossing each other. Bright swellings of energy pulse down them from head to tail. They burn brighter each phase and whenever the core charges.
- **Head:** a dark sand-coloured skull, ringed like the body, with energy veins branching back from the mouth. It has a fleshy lip and throat, and two rings of bone teeth angled down the throat. The three petal jaws have flesh on their inner faces, a glowing vein and bone fangs, and open on the Engine Core, still in its metal brace.
- **No clipping:** the body leaves from the back of the skull, and the head can turn at most 55° from the line of its neck (**Max Neck Bend**). It no longer twists through its own body when it looks around or in the cutscene.
- **Weak spots:** swollen blisters of the core's energy bulging through the hide in fleshy rims, on both sides of four points along the body.

The body mesh only exists in Play mode (it is built at runtime), so outside Play the Scene view shows the head and the blisters on their own.

## How it plays out

1. **Leaving the stage:** with the three mini-bosses' ship parts, walk to the stage exit (a ship beacon) to take them back to the ship.
2. **The ambush (cutscene):** the ground rumbles and the Core Worm rockets out of the sand right in front of the exit. It roars, lunges and swallows you. Then: *"Dragged beneath the dunes..."*.
3. **Its arena:** you land at the edge of a sunken bowl. It bursts out in the middle, the name card shows, and the fight begins.
   - **The Scotch flask:** you have it for the fight. Press **F** (or d-pad up) to drink. The rules are the tutorial's: each sip heals 35, halved for every drunk stack, and a stack wears off every 15 s. Drinking makes you stagger, and the sip after 5 stacks is a blackout, which counts as going down. The laser is put away while you drink. This is the shared player's `PlayerFlask` ([core player](core-player.md)): the exit gives you the flask (`PlayerGear`) when the fight starts if you don't already have it.
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

The test scene uses the shared `Player` and `GameSystems` prefabs ([core player](core-player.md)), starting with the laser and scanner. A test scene made before the core player existed can be moved onto it with **Signal Lost > Desert Boss > Upgrade Test Scene to Core Player**, which keeps everything else in the scene. During the cutscenes the exit switches off movement, the flask and interaction, and holsters your tools.

In the test scene, **Assume Parts Collected** is ticked on the exit, so you can walk straight up and get ambushed. **Regenerate Prefab** rebuilds the worm (same GUID), then re-links the test scene's worm to its arena, exit and fodder spawner if needed. **Validate** re-runs the checks. Commit the generated files with their `.meta` files.

## Where you can hurt it

| Part | Damage taken |
| --- | --- |
| Hide and hull plating (head, body) | **15%** |
| Glowing energy **blisters** on its sides (4 places, both sides), only while above the sand | **100%** |
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
- **Core beam:** rears up, charges for about 1.4 s (core blazing, mouth flame swelling, fire and sparks pouring into its throat, the ground shaking), fires with a flash, then sweeps a beam 140° across the arena (9 every 0.2 s, hits within 2 m of the beam), leaving a wide line of fire. Pillars block it.
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
  - **Projectiles:** the beam has three layers (a white-hot core, a wide crackling orange beam and a broad glow) with orange lights at the mouth and the impact, a doubled flame jet at the mouth, and a fountain of fire, sparks and smoke where it hits; slag globs trail flame; shrapnel leaves glowing streaks.
  - **Bursts** (sand, smoke, sparks, fireballs) are emitted into one shared particle system per kind, built when the fight loads, instead of a new object per burst (which caused hitches when an attack set off many at once).
  - **Arena:** sandstorm particles and fog, swirling cyclones, collapsing tiles, fire lanes.
  - **Screen shake** for every eruption, slam and blast.
- **Body following:** the body is laid along the head's whole trail with nothing skipped, so it moves smoothly with the head. (Earlier it skipped trail points near the head, which made the body jump back and forth when the worm dived beside its own eruption.) The neck bends out of the back of the skull over the first 3.5 m (`neckLength` on CorewormBody). Before every eruption it lines up directly beneath the spot with its head turned up, while still hidden, then bursts straight up (it used to break the surface sideways up to a few metres off and lurch across). The neck limit only applies above the sand. The tail strictly follows the head's path. While the worm waits under the sand to erupt, its head keeps circling slowly just beneath the spot, so the body keeps sliding in after it instead of freezing with the tail in the air. The ambush swims into position under the sand instead of teleporting, which used to yank the whole body.
- **Movement:** every move continues from how the worm is actually travelling (direction and speed), so it never snaps round or jumps. Swims speed up smoothly and turn tighter near their target. It slides under its eruption spot instead of jumping there, steers along the arena edge instead of grinding into it, and trembles in place during the nova instead of drifting. Its hit boxes ride on a kinematic Rigidbody so moving them each frame is cheap.
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
  - `CorewormBody` (the continuous body mesh and its pulsing energy veins)
  - `CorewormArena` (the arena changes)
  - `DesertStageExit` (the exit, the ambush cutscene, retry and the return)
  - `CorewormHud` (bar, name card, fades, subtitles)
  - `CoreSlag`, `CoreShrapnel`, `SandCyclone`
  - `EngineCorePickup`
- `Scripts/Progression/StageProgress.cs`
- `Scripts/Effects/CameraShake.cs`
- `Scripts/Enemies/Common/EnemyAllegiance.cs` (no friendly fire between enemies, next to the shared `Territory` rules)
- `Editor/DesertBossBuilder.cs`

It reuses the fire effect and particle bursts, `FireZone`, `FuelShell.Blast` (the line-of-sight blast), `ScrapBomb`, `GroundMarker`, `ExpandingRing`, `MiniBossHud` banners and the desert fodder.

## Diagnostics

`CorewormDiagnostics` is a test recorder, Editor only. It is off unless the file `signal-lost/Logs/coreworm-diagnostics.on` exists. When it is on, pressing Play in CorewormTest:
- puts the player at the stage exit, starting the ambush;
- keeps the player alive;
- writes a line per frame to `signal-lost/Logs/coreworm-diagnostics.csv`: frame time, the worm's current attack and move, speed, head and body positions, body layout time and garbage collections;
- stops Play mode after 90 s.

Delete the `.on` file to turn it off. The Logs folder is git-ignored.

The stutter fix used it. In the first run, at about 200 fps, the head stopped dead for a frame at nearly every change of move, then re-accelerated from zero (for example 26 m/s → 0 → 4 m/s). Breach exits also snapped the head 4 m inward. There were three causes:
- Unity resumed the outer routine a frame after each nested move.
- The tracked speed dropped to 0 on that frame.
- The boundary clamp in `Circle` caused the inward snap.

The fixes were inline nested moves (`Inline`), easing the tracked speed, steering back inside the arena, carrying the dive's end speed, and rising during line-up. In that run the median speed kept across move changes went from 3% to about 100%, with no jumps. That transition statistic did not catch every last-frame dive stall.

### Dive end-frame follow-up

A further Play Mode regression reproduced the intermittent stall: `DiveInto` clamped its curve parameter to 1 on the final frame. If only a tiny fraction of the dive remained, the head barely moved (sometimes not at all) for a whole rendered frame. Restoring `currentSpeed` afterwards did not undo that visible pause. Six of 15 baseline cases lost 60–100% of their final-frame speed.

The dive now finishes a full movement step, continuing just beyond its nominal endpoint beneath the sand for at most that final frame. The overshoot is capped at 10% of the curve, so a long hitch frame (Unity allows up to 0.33 s) can't push the head 10–15 m past the end; normal frames never reach the cap. The next swim still inherits its momentum. The change is shared by every attack using `DiveInto`; attack timings, damage, health, deliberate pauses and the existing body-following path are unchanged. No prefab or scene regeneration is needed.

[CorewormMotionSmoke](../tools/unity/CorewormMotionSmoke.cs) is the regression harness. Install it only in a disposable project's `Assets/Editor`, then run `CorewormMotionSmoke.Start` in batch mode without `-quit` (it exits itself). It checks actual head displacement for the three dive durations at simulated 30, 60, 144 and 240 fps, plus variable frame steps, then runs all four fight phases.

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

The original check used the standalone C# compiler against Unity **6000.6.2f1** assemblies. The dive end-frame follow-up also ran actual Unity **6000.6.2f1**, D3D11 compilation and Play Mode checks in a disposable copy using the same gameplay package versions:

- Before the fix, 6 of 15 dive cases failed the end-frame speed-retention check; after it, all 15 passed without the near-zero final step.
- All four fight phases ran, including ambush, breach, slam, spit, beam, barrage, overlapping attacks and the core nova, with finite head positions and the body/trail present.
- The existing `UnityEditor.Search.SearchDatabase` startup exception was narrowly excluded; other Console errors/exceptions fail the harness.
- No scenes, prefabs, materials, meshes or settings were regenerated or delivered from the disposable copy.

This is not full manual acceptance: the cutscene, defeat/retry/progression loop, visual smoothness with physical input, balance and target-hardware performance still need playtesting. Reports and the pre-change snapshot are under the ignored `signal-lost/Logs/CorewormStutter/` folder.
