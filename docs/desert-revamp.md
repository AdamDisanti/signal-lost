# Desert revamp: terrain and quests (draft, 11-22)

Lawson, 11-22: "revamp the desert quest system and have the boss interactions feel more natural and have the quests not just feel like a boss spawn flag ... more terrain variety for the desert with things like ravines/canyons, large rock formations, oasis's, desert trees ... the quests should take advantage of environmental design." He liked the variety and goofiness of the Mudlands quests (the driving test), and gave three seed ideas: Cavendish's omelet and the wyvern snatch, waves at the track before a boss, and mining oil for a thumper that disturbs the scorpion.

Status: **approved 11-22, being built**: terrain first. This page replaces the job list in [desert-story.md](desert-story.md) once it is built; the boss fights in [desert-bosses.md](desert-bosses.md), the Core Worm fight and the lair stay as they are.

## Decisions (Lawson, 11-22)

| Question | Answer |
| --- | --- |
| Structure | **One chain**, one job at a time, like the Mudlands: the tracker shows only the current job, the scanner points only at its objectives, and every job ends with a report to Cavendish. |
| NPCs | **2-3 new NPCs** besides Cavendish and Eugene. |
| Map size | Grow from 320 m to **~440 m**. |
| Arenas | The three mini-boss arenas and the Core Worm's bowl and lair stay. They can move but must not be removed. |
| Models | All new models (NPCs, trees, rock formations, props) are made in Blender through the Blender MCP. |
| The chain | Approved as drafted below. |
| Rewards | Scrap, plus 3-4 desert upgrades in Eugene's shop, one unlocked per act. |
| Build order | Terrain first, with all new areas and models, so it can be walked; then the quests, act by act. |
| Doris | A comic scene only (the egg hunt, and a line after the snatch); no job of her own. |

## The new layout (440 m)

The map grows by 60 m on every side. To keep the risk low, the three arenas, the worm's bowl and the lair stay where they are. The new ground goes to the new outer ring and to the gaps between the arenas. Camp and the arrival dock move out to the new south shore.

| Area | Where (approx.) | What's there | Used by |
| --- | --- | --- | --- |
| **Camp and arrival dock** | South shore, (10, -170) | Cavendish, Eugene, the trophy rack, the camp fire (now a cooking spot) | Every report; the Omelet |
| **Mirage Springs** (oasis) | South-west, (-110, -120) | A spring-fed pond in a sandstone hollow, alien palms, reeds, a shaded hut. Home of **Doris** | Breakfast Run |
| **The Hoodoos** | West, (-150, -10) | Tall rock formations: a field of wind-carved hoodoos, balanced rocks and **the Big Arch**, a natural arch you can walk over. Nests sit on ledges and tops, reached by rock ramps | Breakfast Run |
| **Wreck Field / Briarwing's Roost** | (-62, -62), unchanged | Arena unchanged; the dunes round it get dead trees and thornwood | Briarwing |
| **Dustbowl Speedway** | East, salt flat round (110, -40) | The Scorch Raceway (arena, unchanged) becomes the infield of a bigger speedway: a cracked white salt flat, a ramshackle grandstand of hull plates, a pit garage, a flag tower. Home of **Marshal Mo** | Pit Crew Tryout, Main Event, Cindermaw |
| **Rattlesnake Canyon** | North-west, winding from the coast (-160, 110) down to the Buried Hull | A ravine 8-12 m deep, cut into a raised plateau: sheer layered walls, a narrow switchback, a rope bridge over its deepest part, oil seeps on its floor. **Old Gus's** claim and derrick at its mouth | Divining, Black Gold |
| **Buried Hull / Pitchclaw's Sump** | (-58, 62), unchanged | Arena unchanged; it now sits where the canyon opens out | Pitchclaw |
| **The Dry Wash** | From the canyon mouth south-east across the middle to the sea | A dried riverbed: a sunken, pebbled channel with dead trees and bleached driftwood, crossing the old crash streak | Travel, scenery |
| **Table Mesa** | North-east, (130, 110) | A big flat-topped mesa with a ramp path up; a lookout over the whole map | Scenery, short cut down to the Speedway |
| **Core Worm's bowl** | (38, 92), unchanged | Arena and lair unchanged. Gus's **thumper** is set up on the rim | The Thumper |
| **Departure dock** | North shore | Unchanged (the boat to the Mudlands) | The end |

**Desert trees** are scattered by area: alien palms at the oasis, spiky Joshua-like "spindle trees" on the plateau and the flats, dead thorn trees in the Wash and round the Roost. The existing cacti, mesas, boulders and wreckage stay.

**Rocks and the canyon** are separate cliff meshes, like the Crystal Caves' cliff blocks. They aren't carved out of the 4 m terrain facets, which are too coarse for sheer walls. The terrain gets 2 m cells near paths, like the Mudlands.

## The cast

| Who | Where | What they are | Running gag |
| --- | --- | --- | --- |
| **Cavendish** | Camp | The capybara astronaut. Gives every job and takes every report | Everything is "for safe keeping"; he's hungry |
| **Eugene** | Camp | The hermit crab merchant. Restocks after reports | Unchanged |
| **Doris** (new) | Mirage Springs | A tall, unflappable alien ostrich-like bird who keeps the oasis as a "spa" (a towel on her head, cucumber slices) | Knows exactly whose eggs those are and says nothing useful about it |
| **Marshal Mo** (new) | Dustbowl Speedway | A meerkat race marshal with a megaphone, a chequered flag and far too much paperwork. Cousin of the Dish Meerkats | Everything needs a form; he ranks everything out of ten |
| **Old Gus** (new) | Rattlesnake Canyon | A deaf old tortoise prospector with a dowsing rod and a thumper of his own invention | Mishears everything; has been "about to strike it rich" for 80 years |

## The quest chain (11 jobs)

Each job ends with a report to Cavendish. He pays scrap, and Eugene restocks if there are desert upgrades to unlock. Then he gives the next job in the same conversation. As in the Mudlands, a job finished before it's given is caught up. The boss is never a spawn flag: each one turns up because of something the player did.

### Act 1: Breakfast (Briarwing)

1. **Breakfast Run.** Cavendish is starving: "I've seen nests all over the place. Abandoned, I'm sure." Collect **4 big speckled eggs** from nests around the map:
   - one on a hoodoo top;
   - one on top of the Big Arch;
   - one on a ledge in the Mirage Springs hollow;
   - one in a dead tree in the Wash.

   Each is a short climb up rock ramps and ledges. Doris at the oasis watches you take one: "Oh, those aren't mine, dear. Mine are the small ones. Those are... well. Enjoy your breakfast." Eggs carried: 4/4. Report: Cavendish is delighted and gets the pan out.
2. **Omelet du Jour.** A cooking mini-game at the camp fire, with Cavendish as a fussy sous-chef. Three steps, each with a small on-screen widget:
   - **Crack**: hit E as a ring closes, 4 eggs.
   - **Whisk**: alternate A/D to fill a bar before it drains.
   - **Flip**: release on the sweet spot of a swinging pan meter.

   Each step is graded, and Cavendish scores the omelet out of ten ("A seven. The shell is a bold choice."). A bad omelet is still eaten. You sit down on a log and eat (first-person bites). Then a screech, a shadow, and **Briarwing snatches you off your seat**. A scripted flight carries you over the Hoodoos to its Roost. It drops you on the sand by its empty nest, and the fight starts. Those were its eggs.
3. **Briarwing.** The duel as built. Beaten, it leaves its wing panels and the trophy. Report: "Its eggs? I'd never have guessed. Ostrich, I'd have said. Doris did look at me funny."

### Act 2: The Speedway (Cindermaw)

4. **Ruckus at the Track.** Cavendish has seen smoke and heard engines to the east. At the Dustbowl Speedway, Marshal Mo won't let you past the gate without a **Pit Pass**, and a Pit Pass needs a **Pit Crew Tryout**. It's a tyre change against the clock on a wrecked hover-kart: each of 4 wheels needs its 4 nuts (follow the shown keys fast), then "lollipop" (E) to release. You can fail it and retry ("Three out of ten. My nan's quicker, and she's a meerkat."). The time is graded.
5. **The Main Event.** Mo needs the track cleared for "the Main Event". Three waves on the oval, announced over his megaphone, with the start lights counting each wave in:
   - Spark Ticks;
   - Hubcap Armadillos rolling the banking;
   - a mixed wave with Jerry-Can Beetles.

   The kerbs and pillars matter. After the third wave the start lights go red, a rocket roar comes from the tunnel... and **Cindermaw** tears onto the track: it *is* the Main Event. Mo hides in the flag tower. The fight starts on the raceway with no adds, as built.
6. **Cindermaw.** The duel. Beaten: thruster and trophy. Mo gives you a laminated "Track Champion" card and a 10/10. Report.

### Act 3: Black Gold (Pitchclaw)

7. **Divining.** Cavendish sends you to Old Gus, the prospector in Rattlesnake Canyon, who "knows where the worm sleeps". Gus mishears everything. He wants oil for his thumper and shows you his dowsing rod... then lends you the idea instead: **your scanner** finds the 3 oil seeps hidden along the canyon floor. It's hot/cold by signal strength with no arrow, the scanner's own rule. Walk the switchback and the rope bridge, and plant one of Gus's stakes on each seep.
8. **Black Gold.** Gus drills the three stakes. While each **pump jack** fills a barrel (about 30 s), the noise draws **Snorkel Eels** and Spark Ticks out of the sand. Keep them off the pump; it stops while hit. Each full barrel rolls down to Gus. On the third, the canyon floor cracks, oil sprays from the sump, and **Pitchclaw** heaves itself out of the Buried Hull: you've been drilling into its pool. Gus: "SPEAK UP, SONNY, I CAN'T HEAR THE SCORPION."
9. **Pitchclaw.** The duel. Beaten: fuel tank and trophy. Report: Cavendish is told about the thumper and goes quiet.

### Finale: the Core Worm

10. **The Thumper.** Gus's thumper stands on the rim of the worm's old sand bowl. It needs fuel:
    - Carry Gus's 3 barrels to it one at a time, from the canyon mouth to the bowl. You walk at 70% speed while carrying, and the Jerry-Can Beetles go for the barrel.
    - Fill it, then pull the cord on the beat: E as the piston drops, 3 times, like starting a mower.
    - It thumps. The bowl ripples. The worm comes up under the thumper and swallows you, and the lair fight runs as built.

    Dying sends you back up with the thumper still running, ready to call the worm again. This replaces "set the third trophy on the rack".
11. **Daylight.** The way out as built (collapse, climb), then home: the fang on the rack, Cavendish's wrap-up, and the boat to the Mudlands.

## How it's built (plan)

- **Terrain** (`DesertDunesBuilder`):
  - Grow `MapSize` to 440 and add the new areas as their own builders: `BuildOasis`, `BuildHoodoos`, `BuildCanyon`, `BuildWash`, `BuildSpeedway`, `BuildTableMesa`.
  - Canyon walls and big formations are cliff meshes.
  - The existing seeds and layout inside the old map stay where they can, so the arenas and lair don't move.
- **Models** (Blender MCP, exported to the project):
  - the three NPCs, rigged with a few idle and talk poses;
  - alien palms, spindle trees and dead thorn trees;
  - hoodoos, balanced rocks, the Big Arch and canyon wall pieces;
  - the egg and nests, the camp pan, the hover-kart, Gus's pump jack, stakes, barrels and the thumper;
  - the grandstand and flag tower.
- **Story** (`DesertStory` rewritten on the Mudlands pattern):
  - One `Step` enum and one job at a time, with reports, pay and catch-up.
  - New small components: `EggNest`, `CookingMinigame`, `WyvernSnatch` (the scripted flight), `PitCrewTryout`, `WaveEvent` (the track waves), `OilSeep` and `PumpJack`, `CarriedBarrel`, `Thumper` (starts the existing worm ambush).
  - Boss arrivals hook into the existing `DesertMiniBoss` (no fight changes).
- **Tests:** a Play mode smoke on the copy that runs the whole chain through `CompleteCurrentForTests`, like the Mudlands, plus pictures of every new area.

## Progress

**Terrain, 11-22 (built, checked by builder validation and rendered pictures, not yet walked in Play mode):**
- **Map:** 440 m, on the same 4 m grid as before, so the bowl, the lair and the collapse sit on identical ground. The three arenas are unchanged, apart from turning a few degrees to face the camp's new spot.
- **Camp and spawn:** moved to the new south beach (camp at (9, -176), spawn at (0, -192)). The north dock moved out to the new north beach.
- **New areas:** `DesertDunesLandforms.cs`.
  - **Rattlesnake Canyon:** carved through a 12 m plateau that has two ramps. The floor is about 14 m wide between layered wall slabs, and **the Big Arch** spans it as a natural bridge, level with the plateau.
  - **The Dry Wash:** runs down to **Mirage Springs**, a hollow with a clear pond and palms.
  - **The Hoodoos:** 16 hoodoos, balanced rocks and rock fins, round a lookout rock with one ramp.
  - **Table Mesa:** 16 m high, with one ramp.
  - **Salt flat:** a pale crust beside the Scorch Raceway.
  - **Trees:** spindle trees on the flats, the plateau and the mesa; dead thorn trees along the Wash and round the Roost.
- **New scatter:** 18 mesas, 110 boulders and 52 cacti, all kept off the landforms.
- **Menu:** **Signal Lost > Desert > Apply Desert Revamp Terrain**. It rebuilds everything and reinstalls the current (old) story, which the new chain replaces next.
- **Models:** the Blender scenery (`tools/blender/desert_scenery.py`) and the three NPC models (`tools/blender/desert_npcs.py`) are exported.

**Terrain pass 2, 11-22 (Lawson: "make the canyon deeper and make the terrain look more natural as currently its pretty blocky"):**
- **Deeper canyon:** the plateau stands 20 m (it was 12), so the canyon is about 19 m deep. The plateau moved inland so the west beach no longer flattens its edge.
- **Ramps:** they run out past the cliff line as long tongues, so they stay walkable on the taller plateau.
- **Cliffs:** the boxy wall slabs are gone. Every cliff is now one continuous eroded rock face (`DesertDunesCliffs.cs`): the canyon's two walls and the outer cliffs of the plateau, Table Mesa and the lookout rock.
  - The face is built in uneven sandstone strata that step in and out as ledges. It leans back as it rises, bulges and hollows along its length, is buried at its foot, and folds over the land at its top.
  - A face only stands where at least 5 m of rock really rises behind it, so it fades out at canyon mouths and across ramps.
- **The Big Arch:** stretched so its deck spans the whole gap and its legs reach the deeper floor.
- **Old mesas:** the hexagonal prisms are replaced by three eroded Blender buttes. Each has near-vertical banded walls, the odd ledge and a scree skirt.
- **Salt flat:** a smooth-edged crust with a honeycomb of crack lines, instead of square tiles.

**Act 1, Breakfast, 11-22 (built; Play mode smoke passes on the test copy):**
- **The cast:** prefabs for Doris, Marshal Mo and Old Gus from the Blender models (`DesertNpcBuilder`, **Signal Lost > NPC > Build Desert Locals**).
  - Each is a new `NpcActor` role, `Local`, talkable under its own name, with `LocalIdle`: the head tracks the player and nods while talking, the body breathes, the arms gesture, and Doris's wings flap now and then.
  - Doris stands by the pond at Mirage Springs, Mo between the Scorch Raceway and the salt flat, and Gus in the canyon by its east mouth. Mo and Gus have small talk only, until Acts 2 and 3.
- **Breakfast Run:** Cavendish's new welcome sends you for four eggs.
  - The nests (Blender nest and egg) are on the lookout rock in the Hoodoos, on the Big Arch's deck, on the rim of Mirage Springs and in a dead tree in the Dry Wash.
  - Each is a `QuestTask` with a scanner signal. Doris comments when you take the springs egg, and has her own lines before and after the snatch.
- **Omelet du Jour:** `CookingStation` on the camp fire, with a Blender frying pan. It is the three-step mini-game (crack with E in the green, whisk by alternating A/D, flip with Space at the sweet spot), scored out of ten.
  - The player is frozen and looks down at the pan while cooking. Cavendish rates the omelet, you eat (three bites, a burp)...
- **The snatch:** `WyvernSnatch`. A screech, then a stand-in Briarwing (the boss's own model and animator, stripped of colliders and scripts) swoops in from behind on its Hover flap. It grabs the player and carries them high over the Hoodoos to the Roost (the view looks down from its talons), drops them 9 m from the nest ("Those were ITS eggs!"), wheels round and lands. Then it swaps for the real Briarwing, which engages at once.
- **The fight:** Briarwing is job 1 with no tasks. After it, the report has new lines, and Acts 2 and 3 still run as the old jobs (fires, then Cindermaw; leaks, then Pitchclaw) until they are rebuilt.
- **Test hooks:** `DesertStory.SkipToBreakfastForTests(score)` and `SkipSnatchForTests()`, and `CookingStation.CookForTests(score)`.

**Checked on the test copy:** a Play mode smoke covers the eggs armed (4/4), breakfast, the snatch's real flight, landing at the Roost, Briarwing engaging, and the kill moving the story on. It also took pictures of the nests, the locals and the flight.

**Not checked:** the cooking mini-game's panel and its key timing by hand (the smoke uses the test hook), and the feel of the flight in the editor.

**Fixes after Lawson's play-test, 11-22:**
- **Skipping:** Space (Jump) skips the breakfast scene and the snatch. A skipped snatch drops the player by the nest with Briarwing home, as at the end of the flight. A hint shows when each starts.
- **See-through scenery:**
  - **Double-sided:** every desert scenery material (`SceneryMaterial`) renders both sides, so thin pieces (palm fronds, wings, beards) and cliff faces seen from above or behind never vanish.
  - **Matte:** the materials have environment reflections and specular highlights off, so shadowed faces seen edge-on don't go blue-grey.
- **Cliff tops:**
  - Each cliff's lip slopes down to tuck just under the real ground behind it, with a skirt below. It no longer floats as a band over a rim that rises gradually (the lookout rock), and never lies flat on the ground to flicker against it.
  - Lips are longer (8 m on the canyon walls, 6 m on the tables), so they always cover the canyon's cut even where the face bulges out. A row of sawtooth terrain triangles had shown along the rim.
  - Each column's reach is capped where it would cross its neighbours on the inside of a bend.
- **Buttes:** they were open between their layers, leaving thin slits all the way round. Each layer now starts on the ring its ledge ends on, so the butte is one closed shell (0 open edges).
- **Salt crust:** raised to 12 cm, with its cracks at 16 cm, so it doesn't flicker against the dead-flat salt pan far off.
- **Checked:**
  - Every exported model was checked for open edges. Only thin blades (fronds, wings, plumes, beards, the omelet) have them, by design, and they render double-sided.
  - A raycast census from the problem viewpoints found no face seen from behind.

## Open questions for Lawson

- **Rewards:** the Mudlands reports pay scrap and unlock Eugene's stock. The desert has no desert-only upgrades yet. Keep scrap only, or add 3-4 desert upgrades (one per act) to Eugene's shop?
- **Build order:** terrain first (with the new areas empty), then the quests, then the models? Or one act at a time, end to end?
- **Doris:** a quest giver only in passing (one comic scene), or should she get a small job of her own in the chain?
