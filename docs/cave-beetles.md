# The Beetles (Crystal Caves mini-boss)

The Crystal Caves mini-boss fight. Amanda asked for it on 11-09: four beetles who want to be a band. On 11-11 she had it reworked to work like the game's other mini-bosses ([desert bosses](desert-bosses.md), [Shard Mole](shard-mole.md)):

- a Blender model with animation clips, on the `DuelBoss` base;
- a boss bar with phases;
- poise and staggers;
- a weak spot;
- a ship part dropped on defeat.

| Beetle | Colour |
| --- | --- |
| **Bug Lennon** | red |
| **Paul Arthropodney** | yellow |
| **George Harrison** | blue |
| **Scarab Starr** | green |

It lives only in its own test scene for now. It is **not placed in the Crystal Caves level** (the level has an empty beetle fight zone waiting at (−150, −42)); that waits until Amanda asks. All numbers are placeholders to tune.

## Open it

Open `Assets/Scenes/Bosses/CaveBeetlesTest.unity` and press Play.

The test cave is a walled, roofed room built from the level's cave materials and lighting. The 24 m fight zone is flat and empty, ringed by purple and pink crystal batches. You start at the south end, outside the zone, with the shared **Player** and **GameSystems**. Walk north into the zone to start the fight.

Menu **Signal Lost > Crystal Caves Enemies > Beetles**:

| Menu item | What it does |
| --- | --- |
| **Build Prefabs** | Rebuilds the four prefabs from the FBX and clip table (same GUIDs). Edits made to them are lost. |
| **Create Test Scene** | Creates the test scene if it doesn't exist (it never overwrites one) and validates it. |
| **Validate** | Checks the prefabs and the test scene. |

## The look

The same look as before, now a rigged Blender model shared by all four. Only the shell, belly and crystal colours change between beetles.

- A round, glossy shell with a dark seam down the middle, and small glowing crystal growths on the shoulders and back in the beetle's own colour.
- Big friendly eyes. No antennae or mandibles, and six short, stubby legs tucked under the shell, so nothing triggers bug phobia.
- A microphone pack strapped to its back, with a status light and a stubby aerial, and a headset with a boom mic.
- About 2.4 m long and 1.3 m to the top of the shell. About 1,400 triangles, 11 bones and 14 clips.

## How the fight works

### Shared mini-boss rules

- **Engage:** walking into the zone, or hitting any beetle, pulls in the whole band. All four roar, and a banner says "THE BEETLES" with the dodge key.
- **Roar push:** all four roar together (at the start and at each phase change), but only the beetle nearest you pushes you back, by 3 m. Four pushes used to stack and throw you up to 16 m (Amanda, 11-12).
- **One boss bar:** "The Beetles" shows the band's combined health (fallen members count as empty), with a notch for each phase and the phase's name.
- **No adds:** they fight alone, like the other duels.
- **Poise:** if a beetle takes 100 damage in a short time, it staggers out of a head-butt (but not out of the stomp, the dive, the box-in or a flip).
- **Leash and reset:** if you go more than 48 m from a beetle's home, or die, it goes home at full health. Defeated beetles stay defeated.
- **Zone:** the beetles never leave their 24 m zone, except to take their places in a box-in, which may be up to 6.5 m past its edge.
- **Taking turns:** only one beetle attacks at a time. The others stalk you. Nobody starts an attack once a box-in is due.

### Phases (the band's combined health)

| Phase | Health | Title | What changes |
| --- | --- | --- | --- |
| 1 | 100–66% | Rehearsal | Head-butt and the box-in. |
| 2 | 66–33% | Performance | Adds the drum stomp. The shell crystals glow brighter. Everything is a little faster (the shared phase tempo). |
| 3 | 33–0% | Encore | Adds the stage dive. The crystals glow brightest. Faster again. |

Each phase change is a band-wide set piece: all four roar together under a banner, "THE BEETLES: PERFORMANCE". On top of that, each fallen member makes the rest **15% faster** (moves and animation), box in **1.8 s sooner** and give a **shorter warning**.

### Moves

| Phase | Move | What happens | Counter |
| --- | --- | --- | --- |
| 1–3 | **Head-butt** | Close in front: it rears back (0.55 s, the warning), then lunges head first. 15 damage and a shove. | Back off or dodge on the rear-back. |
| 1–3 | **Box-in** | See below. 30 damage if it lands. | Jump as the ring closes. |
| 2–3 | **Drum stomp** | It rears up high on its back legs (the warning) and holds there, then slams down. Within 2.5 m of the slam: 25 damage. Then a **ring of crystals** bursts out of the floor and rolls out to 11 m. It hits a player on the ground for 25. | Jump or dodge through the ring. |
| 3 | **Stage dive** | 4–16 m away: it crouches and leaps high. A **shadow follows you** for the first 60% of the flight, then locks. It belly-flops onto the spot: 28 damage within 2.6 m. **If it misses, it rolls onto its back.** | Run once the shadow stops following you, then hit the belly. |

### The box-in

| Step | What happens |
| --- | --- |
| **Gather** | No on-screen text. A beetle calls it ("From the top, lads!"). They run to evenly spaced places 6.5 m around you: the four ends of a cross (a triangle with three, a pincer with two). Their places are never squeezed in by the zone's edge. Their mic lights turn amber. They have up to 3.5 s to get there, and keep running to their place during the brace. |
| **Brace (the warning, 1.2 s)** | They rear up on their back legs, rocking, with their mic lights blinking faster and faster and their crystals flaring. An amber ring closes in, reaching the middle just as the beetles will: **that's when to jump**. The box stays where the ring appeared. |
| **Escape (before the charge)** | Get more than 6.5 m from the box's middle and the box-in is called off. The beetles hold their attacks for 2.5 s. |
| **Charge** | Lights go red and they all charge the middle of the box, committed, timed to arrive together as the ring closes. |
| **On the ground in the box** | The first beetle to reach you hits you once (30 damage and a shove). The rest stop short and gloat. |
| **Jumped, or left during the charge** | They slam into each other and **flip onto their backs**. The crash does no damage: all their damage comes from you. A lone survivor skids past and flips over too. |

### Flipped: the weak spot

A beetle on its back kicks its legs, its mic light blinking blue. It rolls back over after **3 s**, or straight away once you land **one hit on its belly** (Amanda, 11-12: more hits made it too easy).

- Its **belly** is the weak spot: the first attack on it (a swing, a punch, the jump smash or a bolt) does **2.5x damage**. Then the belly closes and it rolls back over, so each flip is worth one big hit.

### Hard shell (11-12)

The shell is tough, so they last (Amanda's request). While a beetle is upright:

| Attack | Damage to the shell |
| --- | --- |
| Laser swings | none (it glances off) |
| Fists | none |
| Jump smash | 25% |
| Charged bolts, and anything else | full |

Flip a beetle to open it up: then everything gets through, and one belly hit takes 2.5x.

A swing or punch that glances off the shell rings out with a loud, low clank and a spray of white sparks off the side facing you, so it never feels like a miss.
- While it's flipped, the shell's hitbox is off, so hits from above land on the belly.
- Aiming at the belly shows the beetle's own name and health under the crosshair, and the bar drops with each hit (11-12 fix: it used to show the weak spot's own bar, which never moved). The desert bosses' weak spots got the same fix through `Health.ReadoutAs`.

It flips after a box-in crash or a missed stage dive.

### Fight length (11-12)

Amanda found the band went down too fast and asked for it to last as long as the other mini-bosses. Those take 1,100–1,700 damage as solo duels; the band takes damage much faster:

- a melee swing hits every beetle in its arc, and they crowd round you;
- there is always a beetle in reach, where a duel boss has gaps between openings;
- a dodged box-in hurts all four at once;
- and the belly bonus comes on top.

So each beetle got 600 health. Later the same day, Amanda made the crash do no damage at all and gave the shell its melee immunity (above). Each beetle is now **400** (1,600 for the band, close to the desert bosses' 1,300–1,700). This is an estimate from the numbers. Check the real fight length in play and tune **Max Health** in the builder if needed.

### Talking

The lines are subtitled through the shared dialogue UI.

- **Hit lines:** a beetle says its next line when hit, if nobody else is talking. Each beetle has its own lines and goes through them in order, so the story of the band carries on through the fight.
- **True to their parts (Amanda, 11-12):** all four sing, and each one's lines fit their instrument:

  | Beetle | Plays | Talks about |
  | --- | --- | --- |
  | Bug Lennon | rhythm guitar | writing the songs and keeping the rhythm |
  | Paul Arthropodney | bass | holding down the low end |
  | George Harrison | lead guitar | his solos and his crystal sitar |
  | Scarab Starr | drums | hitting things |

- **The Cavern Club:** where the band started. Each beetle mentions it once, and so does the band's opening line ("An audience! Just like the Cavern Club! Places, everyone!").
- **Band calls:**
  - an opening line;
  - box-in calls;
  - complaints when they crash;
  - gloating when the box-in lands;
  - a line when a bandmate falls.
- **Last words:** each beetle has its own.

### Numbers

| | Each beetle |
| --- | --- |
| Health | 400 (1,600 for the band) |
| Poise | 100 |
| Head-butt | 15 |
| Box-in hit | 30 (once per box-in) |
| Drum stomp, shockwave | 25, 25 |
| Stage dive | 28 |
| Crash after a dodge | no damage; flipped for 3 s, or until one belly hit |
| Shell (upright) | swings and fists 0, jump smash 25%, bolts full |
| Belly while flipped | one hit at 2.5x damage (from anything), then it gets up |
| Drops | Only the **last beetle standing** drops the band's loot: one pile of **12 scrap** (mostly scrap metal, with wiring, circuit boards and a fuel cell) and the **signal amplifier** (counts under `CrystalCaves`, like the Shard Mole's antenna). The others drop nothing. Like the Mudlands mini-bosses' 12-scrap loot piles (Amanda, 11-12); before, each beetle dropped 5–9 scrap. |

## How it's built

- **Model and clips:** `tools/blender/cave_beetle.py`, built with the desert bosses' shared kit. Run:

  ```
  blender --background --python tools/blender/cave_bosses.py -- beetle
  ```

  It writes `CaveBeetle.fbx` and `CaveBeetleClips.json` to `Assets/Prefabs/Enemies/Caves/Bosses/CaveBeetle/`, and `art/cave-bosses/CaveBeetle.blend`.
- **Clips:** Idle, Walk, Run, Roar, Headbutt, Brace, Charge, FlipOver, OnBack, FlipUp, Stomp, Dive, Stagger and Death.
- **Unity:** `Assets/Editor/CaveBeetleBuilder.cs` reuses the desert builder's import steps, then for each beetle:
  - swaps in its colours;
  - adds the bone hitboxes and the belly weak spot;
  - adds the hidden signal amplifier (the scrap pile is spawned by the script).
- **Scripts:**
  - `Scripts/Enemies/Caves/CaveBeetle.cs` (a `DuelBoss`): the moves, the flip and the box-in steps.
  - `Scripts/Enemies/Caves/BeetleBand.cs`: the shared bar, the zone, the turns, the box-in, the anger and the talking.

## Changes to shared code

All opt-in. The desert bosses and the Shard Mole behave exactly as before.

- **`DuelBoss`:**
  - `PhaseHealth`: the health its phases follow. The beetles use the band's.
  - `RequestPhase()`: asks for a phase change.
  - `BannerName`: the name on its engage and phase banners.
  - `TempoBonus`: extra speed. The band's anger uses it.
- **`MiniBossHud`:** bars now take any `IBossBar` (which `DesertMiniBoss` implements), so the band can show one bar for four bosses.
- **`DesertDuelBossBuilder.WeakSpot`:** now `internal`, for the belly.
- **Removed:** the old code-built beetle meshes and materials (headband, mic boom, box, eyes, legs and mic parts). The per-beetle shell, underside and crystal materials and the box-in ring are kept.

## Placing them in the level later

1. Copy the band object from the test scene to the centre of the level's beetle zone, and set its **Zone Radius** (24 m there).
2. Place the four prefabs a few metres from the centre. List them in the band's **Members**, with Bug Lennon first, because he gets the opening line.
3. Re-bake the level's NavMesh if anything there changed.

## Manual checks

1. Press Play. There are no Console errors, and the beetles patrol and ignore you.
2. Walk in. All four roar, and the banner and **one** boss bar ("The Beetles", two notches) appear. Only one beetle attacks at a time.
3. Box-in: the four stand at the ends of an even cross round you, even near the zone's edge. **Jump as the ring closes**: they crash and flip onto their backs. Hit a belly: big damage. One belly hit and that beetle rolls straight back over; a second hit does normal damage. Unhit ones get up after about 3 s.
4. Box-in, stand still: you take one hit of 30.
5. Box-in, walk well out before the charge: it's called off, with a short calm.
6. Get the bar below the first notch: all four roar, and the banner says "PERFORMANCE". Stomps start: jump the crystal ring.
7. Below the second notch: "ENCORE", and stage dives start. Stand still and it lands on you. Run once the shadow stops following you and it misses and flips over.
8. Kill them: the first three drop nothing; the last drops a pile of 12 scrap and the signal amplifier ("… DEFEATED! The signal amplifier dropped."). The bar goes.
9. Die mid-fight: the living beetles reset.

## Verification (11-11)

- **Blender 5.2:** the model and clips built with no unweighted vertices. The preview sheet (`art/cave-bosses/beetle_sheet.png`) was reviewed, and the legs were tucked under the shell after the first look.
- **Unity 6000.6.2f1:** **Build Prefabs** and **Create Test Scene** ran in batch mode, and **Validate** passed. All 14 clips imported as Animator states.
- **Play Mode smoke:** see [CaveBeetlesSmoke.cs](../tools/unity/CaveBeetlesSmoke.cs). Install it only in a disposable copy of the project.
- **Not checked by hand:** how it looks and feels in the editor with real mouse and keyboard, including tuning, the readability of the tells, the subtitles on screen, and the flipped pose in the cave lighting.
