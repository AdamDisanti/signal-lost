# The Shard Mole (Crystal Caves mini-boss)

The Crystal Caves mini-boss, from Amanda's 11-11 request: a blind cave mole with glowing crystal claws and a crystal-bladed tail that hunts by sound. It's built the same way as the desert mini-bosses ([desert bosses](desert-bosses.md)): a Blender model with animation clips, the `DuelBoss` base, a boss bar with phases, poise and staggers, the leash and reset, and a ship part dropped on defeat.

It lives only in its own test scene for now. It is **not placed in the Crystal Caves level**; that waits until Amanda asks.

## Look

Adapted from Amanda's reference image (a cartoon mole), without its clothes, necklace and sunglasses, in the low-poly, flat-shaded style of the desert bosses:

- A fat, pear-shaped body of dark slate fur with rough tufts. A paler belly.
- A long snout with a pink nose and whiskers. Its eyes are closed slits, because it's blind.
- Short, thick arms with huge pink palm-out hands. Each hand has five long **crystal claws** in the cave's own cyan crystal material, so they glow exactly like the cave crystals, plus a cyan light on each hand.
- A short pink tail ending in a **crystal blade**.
- The ship's **antenna array** wired into its back: a dish and three rods with red lights. It hears through it. The antenna drops on defeat.
- About 4.5 m long including the tail, and about 2.3 m tall at the back (3.4 m to the antenna tips). About 1,900 triangles, 23 bones and 15 clips.

## Open it

Open `Assets/Scenes/Bosses/ShardMoleTest.unity` and press Play.

The test cave is a 52 x 70 m walled, roofed room built from the level's cave materials, ringed with cyan and purple crystal batches, with a stalagmite and two boulders. You start at the south end with the shared **Player** and **GameSystems**. The mole waits 34 m away, outside its arena. Walk north to fight it.

Menu **Signal Lost > Crystal Caves Enemies > Shard Mole**:

| Menu item | What it does |
| --- | --- |
| **Build Prefab** | Rebuilds `Prefabs/Enemies/Caves/Bosses/ShardMole/ShardMole.prefab` from the FBX and clip table, keeping the same GUID. Edits made to the prefab are lost. |
| **Create Test Scene** | Creates the test scene if it doesn't exist (it never overwrites one), then validates. |
| **Validate** | Checks the prefab's wiring, clips and materials, and the test scene. |

## How it fights

- **Engage:** walking into its **18 m** arena, or hitting it, starts the fight. It roars, and the boss bar appears with a notch for each phase. Cave chambers are smaller than the desert arenas, which use 24 m.
- **Leash and reset:** if you go past twice the arena radius, or die, it goes home at full health and phase 1, like every mini-boss.
- **No adds:** it fights alone, like the desert duels.
- **Poise:** if it takes 120 damage in a short time, it staggers out of an attack (not the tail swoop or the burrow).

### Hearing (it's blind)

- **Noise** is the player moving (walking, sliding or jumping) or using the laser (swinging, charging, or just fired).
- **Silence:** if the player stands still with the laser quiet for **3 s**, it stops wherever it is and **listens**. It crouches low, cocks its head one way and then the other, sniffs, and sweeps the antenna dish. It doesn't attack while it listens.
- **Heard again:** the moment the player makes noise, or hits it, its head snaps toward the sound and it resumes the fight.
- **Burrowing:** underground it heads for where it last heard you. If you stand still, it surfaces under that spot.

### Moves

| Phase | Move | What happens | Counter |
| --- | --- | --- | --- |
| 1-3 | **Claw swipe** | Close in front. It draws the nearer hand out and back (the claws flare), then rakes it across the front. 18 damage and a small shove. | Back off on the draw. |
| 1-3 | **Tail swoop** | Up to 6 m, and more likely when you're beside or behind it. It crouches and coils with the blade raised (the warning), then spins right round so the crystal blade scythes a full circle low. 22 damage and a **5 m knockback**. Afterwards it's dizzy and takes 1.2x damage. | Jump it, or get out of reach on the coil. |
| 1-3 | **Burrow ambush** | When you're 7 m or more away. It digs in snout-first and disappears (it can't be hit). A **dirt mound with its crystal blade poking out** travels along the floor toward where it heard you, trailing dust. When it gets there, a glowing cyan **ring** (3 m) marks the spot for 1 s, then it bursts out: 30 damage and a shove to anyone in the ring. It lands dazed and takes 1.3x damage until it recovers. | Watch the mound. Step out of the ring when it appears, then punish. |
| 2-3 | **Claw flurry** | Right swipe, left swipe (16 each), then both hands raised high and **held**, which catches an early dodge, then a two-handed rake down the middle (26, a 4 m shove). | Dodge the rake late. |
| 3 | **Ceiling shards** | It rears up and slams both claws into the floor. Five cyan rings appear: the first where you're heading, the rest within 6 m of you. Each ring gets a crystal falling from the ceiling 1.3 s later (22 damage within 1.6 m). They land in quick succession. The aimed shard lands wherever you are; the others stay inside the arena. | Keep moving out of the rings. |

| Phase | Health | Title | What changes |
| --- | --- | --- | --- |
| 1 | 100-66% | Listening | The swipe, swoop and burrow. |
| 2 | 66-33% | Restless | Adds the claw flurry. The claws glow brighter. A little faster (the shared phase tempo). |
| 3 | 33-0% | Shattering | Adds the ceiling shards. The claws glow brightest. Faster again. |

- **Health:** 1,100. All numbers are serialized fields on the prefab, placeholders to tune.
- **Defeat:** the antenna array drops as a glowing pickup ("RECOVERED: Antenna array"). It counts under the stage `CrystalCaves`, since the Crystal Caves has no stage constant yet. It also drops scrap like the cave beetles, the other cave mini-boss: 3–5 scrap metal, 1–2 wiring and 1–2 circuit boards.

## How it's built

- **Model and clips:** `tools/blender/cave_shard_mole.py`, on the desert bosses' shared kit (`bosskit.py`). Run `blender --background --python tools/blender/cave_bosses.py -- mole`. It writes:
  - `ShardMole.fbx` and `ShardMoleClips.json` to `Assets/Prefabs/Enemies/Caves/Bosses/ShardMole/`;
  - `art/cave-bosses/ShardMole.blend`;
  - preview renders and `mole_sheet.png` to `art/cave-bosses/`.
  The clips are Idle, Walk, Run, Roar, Listen, Startle, SwipeL, SwipeR, ClawFlurry, TailSwoop, BurrowIn, BurrowOut, Quake, Stagger and Death.
- **Unity:** `Assets/Editor/ShardMoleBuilder.cs` uses the desert builder's import steps: the FBX import, the URP materials from the clip table, the Animator Controller with a state per clip, bone hitboxes and the hidden part. Then it swaps the claw material for the cave's `CrystalCyan`, adds the claw lights, the scrap and the antenna pickup, and builds the test scene.
- **Script:** `Assets/Scripts/Enemies/Caves/ShardMole.cs` (a `DuelBoss`). The hearing, the listen state and the five moves are all here.

## Changes to shared code

- `DesertDuelBossBuilder`: a few private helpers are now `internal` so the mole's builder can reuse them: `Begin`, `Common`, `Hitboxes`, `HiddenPart`, `AddMesh`, `Save` and `Hitbox`. `Begin` and `Common` take an optional asset folder, which defaults to the desert one. `Begin` takes an optional hook to adjust the materials. The desert bosses build exactly as before.

- **Fix (11-11): the floating antenna.** The antenna array that drops on defeat rides hidden inside the boss. Showing the body again after a burrow (`DesertFodder.SetRenderersVisible`) used to switch it on too, leaving a frozen copy hanging in the air while the mole moved. A new `KeepHidden` hook, which `DesertMiniBoss` points at its ship part, now keeps it hidden. This also fixes the same bug on Pitchclaw's fuel tank after its burrow.

## Placing it later

1. Drop the prefab on cave floor with a NavMesh, in a chamber at least about 36 m across and high enough to rear in (about 4 m). The ceiling shards drop from the ceiling above, or from 8 m up if there's no ceiling collider.
2. Nothing else to set up. It finds the player itself.

## Verification (11-11)

- **Blender 5.2:** the model and clips built with no unweighted vertices. The preview sheet was reviewed, and the fur tufts and the roar pose were adjusted after the first look.
- **Unity 6000.6.2f1, in a disposable copy of the project:**
  - **Build Prefab**, **Create Test Scene** and **Validate** all passed. All 15 clips imported as Animator states.
  - The Play Mode smoke [ShardMoleSmoke.cs](../tools/unity/ShardMoleSmoke.cs) passed, both without graphics and rendered. It checks:
    - it engages when the player walks in;
    - it starts listening about 4 s after the player goes quiet (3 s of silence plus the end of the move it was in) and doesn't attack while listening;
    - footsteps end the listening;
    - every move damages a player standing in it: swipe 18, tail swoop 22, flurry 58 (all three hits), ceiling shards 22–44 (more than one shard can land on a player who stands still), burrow 30;
    - the burrow goes underground, and the hidden antenna stays hidden when it comes back up;
    - phase 3 starts;
    - it drops scrap and the antenna array.
  - In-engine screenshots of each move in the cave lighting were reviewed: the claws match the cave crystals, and the mound, blade and ring read clearly from above.
- **To repeat it:** copy the harness into `Assets/Editor` of a disposable project under the system temp directory, then run Unity with `-batchmode -projectPath <temp-project> -executeMethod ShardMoleSmoke.Start -logFile <log>`, without `-quit`.
- **Not checked by hand:** how the fight feels with real mouse and keyboard, including the difficulty, how readable the tells are, the 3 s listening window and footstep sensitivity (0.6 m/s).

## Open questions

- **Weak spot:** the desert bosses each have one. The mole has none, because none was asked for. The antenna array would be the natural candidate.
- **Crouch-walking:** it currently counts as noise like any other movement. Should sneaking be quieter?
- **Sounds:** it reuses existing effects (the roar, swishes, rock smashes). It has no sounds of its own yet.
