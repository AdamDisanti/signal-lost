# The Beetles (Crystal Caves mini-bosses)

The Crystal Caves mini-boss fight, from Amanda's 11-09 request. Four beetles want to be a band:

| Beetle | Colour |
| --- | --- |
| **Bug Lennon** | red |
| **Paul Arthropodney** | yellow |
| **George Harrison** | blue |
| **Scarab Starr** | green |

They look identical apart from their colour. Each wears a microphone pack on its back and a little headset mic by its mouth. They fight together, and while you hit them they talk about their dream of being a band. Their signature move is the **box-in**: they surround you and charge at once. Jump just as they arrive and they slam into each other instead.

It lives only in its own test scene for now. It is **not placed in the Crystal Caves level** (the level has an empty beetle fight zone waiting at (−150, −42)); that waits until Amanda asks.

All art is generated in code, in the same low-poly style as the other enemies. No assets were imported. All numbers are placeholders to tune.

## Open it

Open `Assets/Scenes/Bosses/CaveBeetlesTest.unity` and press Play.

The test cave is a walled, roofed room. It uses the level's cave floor, wall and ceiling materials and the cave's inside lighting. Like the level's beetle zone, the 24 m fight zone is flat and empty, ringed by purple and pink crystal batches. You start at the south end, outside the zone, with the shared **Player** (laser, scanner, flask) and **GameSystems** (HUD, dialogue subtitles, pause and settings menus), the same setup as the Crystal Caves level. Walk north into the zone to start the fight.

Menu **Signal Lost > Crystal Caves Enemies > Beetles**:

| Menu item | What it does |
| --- | --- |
| **Build Prefabs** | Rebuilds the four prefabs and their meshes from the script (same GUIDs). Edits made to them are lost. |
| **Create Test Scene** | Creates the test scene if it doesn't exist (it never overwrites one) and validates it. |
| **Validate** | Checks the prefabs and the test scene. |

## The look

The model starts from the desert's Jerry-Can Beetle: the same round shell, head and big eyes. Amanda asked to keep its look but not its combat, and to make it read as a Crystal Caves mini-boss. The changes:

- **Removed:** the fuel can, its straps and the explosion.
- **Mini-boss sized:** about 1.9 times bigger, roughly 2.3 m long, with a glossier shell.
- **Crystal growths:** small glowing crystals on its shoulders and the back of its shell, in its own colour.
- **Nothing creepy:** six short, stubby legs mostly hidden under the shell, and no antennae or mandibles, so no bug-phobia triggers.
- **Band kit:** a microphone pack strapped to its back, with a status light and a stubby aerial, and a headset (band, earpieces, boom and foam mic) by its mouth.

## How the fight works

### Together

- **Zone:** they patrol in their zone, like a band rehearsing. The `BeetleBand` object at its centre keeps all four inside its 24 m radius.
- **One fight:** walking into the zone, or hitting any one of them, pulls in the whole band. There are no boss bars at the top of the screen. Like the crab cousins, aim at a beetle and the HUD's crosshair readout shows its name and health.
- **Taking turns:** only one beetle head-butts at a time. The rest circle you, waiting for their solo.
- **Head-butt:** it rears back for 0.55 s (the warning), then lunges, dealing 12 damage and a shove.

### The box-in

| Step | What happens |
| --- | --- |
| **Gather** | No on-screen text warns of it or says how to dodge; the player reads the beetles and works out the jump. A beetle calls it ("From the top, lads!"). They run to evenly spaced places 6.5 m around you: a square with four, a triangle with three, a pincer with two. Their mic lights turn amber. |
| **Brace (the warning, 1.2 s)** | They stop, face the middle of the box and rear up on their back legs, rocking. Their mic lights blink amber, faster and faster, and their shell crystals flare. An amber ring appears around you and closes in, reaching the middle just as the beetles will: **that's when to jump**. The box itself (6.5 m around where the ring appeared) stays put; it doesn't follow you. |
| **Escape (before the charge)** | Get more than 6.5 m from the box's middle (where the ring appeared) before they charge and the box-in is called off. The ring vanishes and the beetles go back to chasing you, but they don't attack for 2.5 s (**Calm After Escape** on the band), so nothing hits you out of nowhere. |
| **Charge** | The lights go solid red and they all charge at once, about 14 m/s, straight at the middle of the box. They're committed: they never turn after you. Each sets its speed so the whole band reaches the middle together, exactly as the ring closes. |
| **You were in the box, on the ground** | The first beetle to reach you hits you once (22 damage and a shove). The rest stop short, nobody bumps, and they gloat ("Encore!"). |
| **You jumped, or left during the charge** | They meet in the middle and slam into each other. Each takes 30 damage (credited to you), is knocked back and is dazed for 2 s, with a wobble and a blinking blue light. They complain ("Who was on tempo?!"). That's your opening. A lone survivor has no one to hit, so it skids past the middle and is dazed instead. |

Their bodies don't block you during a charge, so you can't land on a beetle's back.

### The band gets angrier

There is no phase 2 per beetle. Instead, each fallen member makes the rest:
- 15% faster, with quicker head-butts;
- box in 1.8 s sooner (10 s apart with the full band, 4.6 s apart with one left);
- give a shorter warning (1.2 s with the full band, down to 0.84 s).

A survivor reacts when a bandmate falls ("The show must go on!").

### Talking

Lines are subtitled through the shared dialogue UI, the same way the merchant crab's cousins bark during the hermit crab fight.

- **When you hit one:** if no one else is talking, it says its next line. Each beetle works through its own lines in order, so their story carries on through the fight.
- **Genres they talk about:** rock and roll, underground pop, stone-age rock, heavy metal ("it's in the walls") and crystal-clear vocals.
- **Band calls:** an opening line, box-in calls, misses, gloating and losing a member.
- **Last words when one falls:** Paul: "Let it... beetle...". George: "All things must... pass...".

### Numbers

| | Each beetle |
| --- | --- |
| Health | 220 (880 for the band) |
| Move speed | 3.6 m/s, plus the anger bonus |
| Head-butt | 12 damage, 0.55 s wind-up, 1.4 s cooldown |
| Box-in hit | 22 (once per box-in) |
| Bump after a dodge | 30 to each beetle, 2 s daze |
| Drops | 3–5 scrap metal, 1–2 wiring, 1–2 circuit boards. No ship part (Amanda's choice for now). |

The other shared mini-boss rules apply, from `DesertMiniBoss` and the [enemy rules](enemy-rules.md):
- **Leash:** get more than 48 m from its home, or die, and a beetle resets: full health, back home. Defeated beetles stay defeated.
- **No combo stun:** the laser's combo finisher doesn't stun mini-bosses.

## Placing them in the level later

1. Put a `BeetleBand` at the centre of the level's beetle zone and set its **Zone Radius** (24 m there). Assign its **Ring Material** (`Materials/Enemies/Caves/BeetleBoxInRing.mat`) and its lines. The easiest way is to copy the band object from the test scene.
2. Place the four prefabs a few metres from the centre, and list them in the band's **Members**, Bug Lennon first, because he gets the opening line.
3. Re-bake the level's NavMesh if anything there changed.

## Changes to shared code

`DesertMiniBoss` and `MiniBossHud`, all opt-in, with no change for the existing mini-bosses:

- **`HasPhaseTwo`:** a virtual property, true by default. The beetles return false, so they never enter phase 2.
- **`ShowsBossBar`:** a virtual property, true by default. The beetles return false, so they get no top-of-screen bar, and their health shows only in the crosshair target readout, like the crab cousins.
- **`Engage()`:** now `protected` instead of `private`, so the band can pull every member into the fight together.
- **Defeat banner:** it says "… DEFEATED!" without "The ship part dropped" when the boss has no part to drop.
- **New shared editor kit:** `Editor/CaveEnemyKit.cs` holds the cave enemy folders and materials, the cave test room, and the shared Player and GameSystems setup. The Crystal Chameleon uses it too, with an identical copy on its branch.

## Files

- `Scripts/Enemies/Caves/CaveBeetle.cs`: one beetle. It builds on `DesertMiniBoss`.
- `Scripts/Enemies/Caves/BeetleBand.cs`: the band. It handles the zone, turns, the box-in, the anger and the talking.
- `Editor/CaveBeetleBuilder.cs`: the models, prefabs, lines, test scene and validation.
- `Editor/CaveEnemyKit.cs`: the shared cave enemy helpers.
- `Prefabs/Enemies/Caves/BugLennon`, `PaulArthropodney`, `GeorgeHarrison` and `ScarabStarr` prefabs, with their meshes in `Prefabs/Enemies/Caves/Meshes/` and their materials in `Materials/Enemies/Caves/`.
- `Scenes/Bosses/CaveBeetlesTest.unity`.
- [CaveBeetlesSmoke.cs](../tools/unity/CaveBeetlesSmoke.cs): the Play Mode smoke check. Install it only in a disposable copy of the project.

## Manual checks

1. Press Play. There are no Console errors. The beetles patrol in the middle and ignore you.
2. Walk north into the zone. No boss bars appear; aim at a beetle and its name and health show under the crosshair. Bug Lennon calls out, and all four come for you. Only one at a time rears back and head-butts; the others circle.
3. Hit each beetle a few times. Each speaks its own lines, one at a time, without talking over each other.
4. About 6 s in, the box-in starts:
   1. No text appears on screen.
   2. They surround you, rear up with blinking amber lights and flaring crystals, and the amber ring closes in.
   3. **Stand still:** you take one hit, and no beetle loses health.
   4. **Next box-in, jump as the ring closes:** they pass under you and crash together. Their health drops (aim at one to see), they wobble dazed, and one complains.
   5. **Next box-in, once they charge, run out of the box:** they don't turn after you. They crash together in the middle.
   6. **Next box-in, walk well away from where the ring appeared before the charge:** the box-in is called off. They chase you again, but nobody attacks for a couple of seconds.
5. Kill one:
   1. It says its last words, and a survivor reacts.
   2. Scrap pops out, and the "… DEFEATED!" banner doesn't mention a part.
   3. The rest get faster, and the next box-in is a triangle, coming sooner.
6. Get down to the last beetle: its box-in is a solo charge. Jump it, and it skids and is dazed.
7. Die mid-fight: the beetles still alive reset to full health, back home.
8. Throughout the fight, no beetle leaves the crystal ring.

## Open questions

- **Ship part:** none for now, as Amanda chose. Add one when the Crystal Caves progression is designed.

## Validation performed

- **Unity 6000.6.2f1 batch run on this branch:** the project compiled with no errors. **Create Test Scene** built the four prefabs and the scene, and **Validate** passed.
- **Play Mode smoke** ([CaveBeetlesSmoke.cs](../tools/unity/CaveBeetlesSmoke.cs)) passed twice in a disposable copy of the project (`CAVE_BEETLES_PLAY_MODE_PASS`). It checks:
  - the band ignores you outside the zone, then all four engage;
  - **airborne at the charge:** all four bump and are hurt, and you aren't hit (the smoke lifts the player into the air for about a real jump's airtime, because batch mode can't press Jump);
  - **grounded:** you are hit exactly once, and nobody bumps;
  - **left during the charge:** all four still collide in the middle of the box; none turns after you;
  - **left the box before the charge:** the box-in is called off, and no beetle attacks during the calm;
  - head-butts land;
  - a defeat drops scrap and raises the band's anger;
  - no beetle leaves the 24 m zone (furthest 23.9 m).
- **Found and fixed while testing:** if the gather timed out before every beetle was in place, the far ones arrived after the ring closed, so a jump on the cue landed too early. Each beetle now sets its charge speed so the whole band arrives exactly as the ring closes.
- **Not checked:** how it looks and feels: models, animation, how readable the warning is, the subtitles on screen, tuning. Check those in the editor.
