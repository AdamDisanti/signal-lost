# Grumble, the Geode Bear (Crystal Caves mini-boss)

A Crystal Caves mini-boss, from Amanda's 11-13 idea, built 11-14. It's made the same way as the [Shard Mole](shard-mole.md): a Blender model with animation clips, the `DuelBoss` base, a boss bar with phases, poise and staggers, the leash and reset, and a ship part dropped on defeat.

It lives only in its own test scene for now. It is **not placed in the Crystal Caves level**. That waits until Amanda asks.

## Concept

A cave bear woken early by the crash. Years of sleeping grew geode crystals on its back. It dragged the ship's **power cell** home as a hand-warmer and night-light, and now it is grumpy and won't give it back.

- **Fits the caves:** the cave bear is the classic cave animal, and its crystal back matches the other cave enemies.
- **No phobia triggers:** a fluffy, four-legged mammal with no crawlies, and nothing with many legs.
- **Tone:** comic, like the band beetles and the sunglasses mole.

Bear is not used by any other enemy (checked against every doc and script). Rejected alternatives: dragon (Cindermaw), owl (too close to the bat and the mole's hearing), pangolin (too close to the armadillo and the axolotl's roll).

## Amanda's picks (11-14)

- **Branch:** `feature/geode-bear`, branched from `feature/shard-mole` so it reuses the cave-boss pipeline (it isn't on main yet).
- **Ship part:** the power cell.
- **Yawn:** slows *and* clouds the screen for about 2 s.
- **Difficulty:** ranked against the desert duel bosses, which use the same `DuelBoss` system: a step above Pitchclaw (1,700 HP, poise 190). The Mudlands mini-bosses use the older `DesertMiniBoss` base (420–520 HP, no poise), so their numbers aren't comparable.
- **Colours:** from this doc (cool grey-brown), not the warm brown of her reference image, like the Gem Ram.

## Look

Adapted from Amanda's reference (a low-poly brown bear) in the flat-shaded style of the other bosses:

- A big, round bear with a shoulder hump, in cool grey-brown fur. Darker on the back and the legs, with a paler belly and muzzle. Tufts on the flanks and a neck ruff make it read fluffy.
- Small sleepy eyes under heavy lids. Round ears, a dark nose, two little fangs, cream claws.
- A crest of **geode crystals** on its hump: grey geode rocks sprouting cyan and purple crystals in the cave's own `CrystalCyan` and `CrystalPurple` materials, so they glow exactly like the cave crystals. A cyan light and a purple light sit in the crest.
- The ship's **power cell** strapped to its lower back with scrap straps: a metal cylinder with a glowing green core, ribs, a red status light and loose wires. A green light glows on it. It is the weak spot, and it drops on defeat.
- About 4.4 m long and 2.6 m tall on all fours (3.1 m to the crystal tips). About 2,570 triangles, 23 bones and 16 clips.

## Open it

Open `Assets/Scenes/Bosses/GeodeBearTest.unity` and press Play.

The test cave is the same 52 x 70 m walled, roofed room as the Shard Mole's, ringed with cyan and purple crystal batches, with a stalagmite and two boulders. You start at the south end with the shared **Player** and **GameSystems** (HUD, menus, scrap). The bear waits 34 m away, outside its arena. Walk north to fight it.

Menu **Signal Lost > Crystal Caves Enemies > Geode Bear**:

| Menu item | What it does |
| --- | --- |
| **Build Prefab** | Rebuilds `Prefabs/Enemies/Caves/Bosses/GeodeBear/GeodeBear.prefab` from the FBX and clip table, keeping the same GUID. Edits made to the prefab are lost. |
| **Create Test Scene** | Creates the test scene if it doesn't exist (it never overwrites one), then validates. |
| **Validate** | Checks the prefab's wiring, clips and materials, and the test scene. |

## How it fights

- **Arena:** 18 m, like the Shard Mole. Walking in or hitting it starts the fight: it roars, and the boss bar appears with a notch for each phase.
- **Leash and reset:** if you go past twice the arena radius, or die, it goes home at full health and phase 1, like every mini-boss. Its hibernate count resets too.
- **No adds:** it fights alone.
- **Hide:** the geode back takes **half** damage.
- **Weak spot:** the power cell takes **full** damage. From phase 3 it sparks.
- **Poise:** 200. If it takes that much damage in a short time it staggers out of an attack, but not out of the belly flop or a hibernate.

### Phases

| Phase | Health | Title | What changes |
| --- | --- | --- | --- |
| 1 | 100-66% | Groggy | Paw swipe, boulder lob, big yawn. |
| 2 | 66-33% | Cranky | Adds the belly flop and bear-hug lunge. The crest glows brighter. A little faster (the shared phase tempo). |
| 3 | 33-0% | Sleepwalker | Adds the hibernate attempt. Lids half-shut, wider and sloppier swipes (3 m reach instead of 2.4 m, a bigger lurch), the crest glows brightest, the cell sparks. Faster again. |

### Moves

| Phase | Move | What happens | Counter |
| --- | --- | --- | --- |
| 1-3 | **Paw swipe** | Close in front. It rocks back and raises one paw high (the warning), then rakes it across. 24 damage and a small shove. | Back off on the raise. |
| 1-3 | **Boulder lob** | 6–18 m. It digs its paws under a rock and rips it up (a dust burst), rears with it overhead, and hurls it in an arc. A glowing purple ring marks where it lands, a little ahead of a running player. 28 damage within 2.6 m, and the rock bursts into crystal shards. | Step out of the ring. |
| 1-3 | **Big yawn** | Up to 8 m in front. It sits back, stretches a paw and yawns a cone of cave dust (9 m long, 32° each side). A player caught in it moves at 55% speed with a dusty haze over the screen for 2 s. No damage. Afterwards it sways, dizzy, and takes 1.3x damage. | Sidestep the cone, then punish. |
| 2-3 | **Belly flop** | 5–16 m. It crouches and leaps. A shadow marks the landing. 34 damage within 3.6 m on impact, then a ring of crystal spikes spreads out to 11 m (18 damage, plus a sting from the lingering spikes). It lies flat afterwards and takes 1.25x damage. | Get out of the shadow, jump the ring. |
| 2-3 | **Bear-hug lunge** | 3.5–9 m, in front. It rears with its arms wide (the warning), charges upright about 5 m and wraps its arms shut. A caught player takes 10, is stunned and squeezed three times (12 each), then thrown (12 and a shove): 58 in all. A miss leaves it open (1.25x damage). | Jump, or slide under the arms, as it arrives. |
| 3 | **Hibernate attempt** | At most twice a fight, when it's hurt. It curls up and snores ("Grumble is dozing off... hit the power cell!"), its eyes shut, and crystals grow out of its crest while it heals. Over 5 s it regains up to 15% of its health. A hit on the sparking power cell jolts it awake: it stops healing, the grown crystals shatter, and it's dazed (1.3x damage). If nobody wakes it, it gets the full heal ("Grumble feels refreshed."). | Hit the power cell fast. |

- **Health:** 1,850. All numbers are serialized fields on the prefab, placeholders to tune.
- **Defeat:** the power cell drops as a glowing pickup ("RECOVERED: Power cell"). It counts under the stage `CrystalCaves`, like the Shard Mole's antenna array. It also drops scrap, between the Shard Mole and the Cave Axolotl stage boss: 4–6 scrap metal, 1–3 wiring and 1–2 circuit boards.

### Against the desert duels

| | Briarwing | Cindermaw | Pitchclaw | **Geode Bear** |
| --- | --- | --- | --- | --- |
| Health | 1,300 | 1,500 | 1,700 | **1,850** |
| Poise | 150 | 170 | 190 | **200** |
| Biggest grab | | | 58 (tail snare) | **58 (bear hug)** |
| Biggest slam | | 32 | 32 (leap) | **34 + 18 ring** |

## How it's built

- **Model and clips:** `tools/blender/cave_geode_bear.py`, on the shared boss kit (`bosskit.py`). Run `blender --background --python tools/blender/cave_bosses.py -- bear`. It writes:
  - `GeodeBear.fbx` and `GeodeBearClips.json` to `Assets/Prefabs/Enemies/Caves/Bosses/GeodeBear/`;
  - `art/cave-bosses/GeodeBear.blend`;
  - preview renders and `bear_sheet.png` to `art/cave-bosses/` (set `BOSS_PREVIEWS` to put the single renders elsewhere).
  The clips are Idle, Walk, Run, Roar, SwipeL, SwipeR, Lob, Yawn, BellyFlop, HugLunge, HugSqueeze, CurlUp, Hibernate, WakeUp, Stagger and Death.
- **Unity:** `Assets/Editor/GeodeBearBuilder.cs` uses the desert builder's import steps: the FBX import, the URP materials from the clip table, the Animator Controller, bone hitboxes, the weak spots and the hidden part. Then it swaps the crest materials for the cave's `CrystalCyan` and `CrystalPurple`, adds the crest and cell lights, the scrap and the power cell pickup, and builds the test scene.
- **Script:** `Assets/Scripts/Enemies/Caves/GeodeBear.cs` (a `DuelBoss`). The six moves, the hide and weak spot, and the lids and glow are all here. The yawn's slow uses the player's shared `SetSpeedBonus`, and the dust uses `MiniBossHud.Haze`, the screen cloud from Cindermaw's smoke. The slow is always removed on reset, death or disable.

## Changes to shared code

- `DesertDuelBossBuilder.WeakSpot` is now `internal` (it was `private`), so the bear's builder can reuse it, as the Shard Mole did with the other helpers. The desert bosses build exactly as before.
- `tools/blender/cave_bosses.py` lists the bear (`-- bear`).

## Placing it later

1. Drop the prefab on cave floor with a NavMesh, in a chamber at least about 36 m across and about 4 m high (it rears up and leaps).
2. Nothing else to set up. It finds the player itself.

## Verification (11-14)

- **Blender 5.2:** the model and clips built with no unweighted vertices. The preview sheet was reviewed, then the fur was warmed toward grey-brown and the lob's backward lean reduced.
- **Unity 6000.6.2f1, in a disposable copy of the project:**
  - **Build Prefab**, **Create Test Scene** and **Validate** all passed.
  - The Play Mode smoke [GeodeBearSmoke.cs](../tools/unity/GeodeBearSmoke.cs) passed without graphics. It checks:
    - it engages when the player walks in;
    - a 100-damage hit on the geode back takes 50, and on the power cell 100;
    - every move damages a player standing in it: swipe 24, lob 28, belly flop 52 (slam and ring), bear hug 58;
    - the yawn slows the player and the slow wears off;
    - phase 3 starts; left alone, a hibernate heals about 15% (277); a hit on the power cell wakes it;
    - it drops scrap and the power cell, and no slow outlives it.
- **To repeat it:** copy the harness into `Assets/Editor` of a disposable project under the system temp directory, then run Unity with `-batchmode -projectPath <temp-project> -executeMethod GeodeBearSmoke.Start -logFile <log>`, without `-quit`.
- **Not checked by hand:** how the fight feels with mouse and keyboard, including the difficulty, how readable the tells are, and the look in the cave lighting in the editor (only the Blender previews were reviewed).

## Open questions

- **Axolotl gate:** `AxolotlLair` currently needs two parts (the Shard Mole's antenna array and the Beetles' signal amplifier). A third mini-boss means a third part. Should the gate need all three, like the desert's three parts? That's in the level, so it isn't touched here.
- **Sounds:** it reuses existing effects (the roar, pitched down for snores and the yawn, swishes, rock smashes). It has no sounds of its own yet.
