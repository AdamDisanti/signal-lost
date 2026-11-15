# Desert bosses: the revamp (11-10)

Lawson asked for the three desert mini-bosses to be rebuilt as memorable, Souls-leaning fights: 3-4 phases each, much harder than before, with punishing but clearly choreographed and readable attacks, detailed low-poly models with smooth animation, and more fantasy and sci-fi in the designs. This page is the design they are built to. It replaces the fights in [desert-minibosses.md](desert-minibosses.md); the arenas, quest chain and trophies in [desert-story.md](desert-story.md) stay.

Out of scope until Lawson has tested the bosses and gives the go-ahead: the locomotion overhaul (beyond the dodge below), the unique post-boss upgrades and items, and the desert main quest and side-quest overhaul.

## Decisions (Lawson, 11-10)

| Question | Answer |
| --- | --- |
| Dodge | Add one now, as part of the boss work. The full locomotion overhaul waits. |
| Difficulty | Heavy hits: a big attack takes about 30% of the player's health, so 3-4 kill. Each fight runs about 3-5 minutes. Souls-leaning, not Souls-hard. |
| Adds | None. Pure duels: the fodder spawners stay off while a boss is fought. |
| Lore | Keep the stolen-ship-part frame, with new names. |
| Thorn wyvern | Mostly grounded; it takes off only for its signature dives and barrages. |
| Ash drake's smoke | Smoke clouds it attacks out of, given away by its thruster glow and the ignition roar. |
| Scorpion's fuel | Fuel pools ignite: its pincer sparks light them, and so can the player's shots. |
| Scale | Large: wyvern wingspan about 14 m, drake about 10 m long, scorpion about 9 m long with a 6 m tail arch. |

## Rules every boss follows

- **Readable:** every attack has a wind-up pose held long enough to see (0.5-1.2 s), a distinct sound, and for area attacks a ground marker. Fast attacks have long wind-ups; slow ones may have delayed or feinted wind-ups, but never no tell.
- **Punishable:** every big attack ends in a recovery the player can hit. The bigger the attack, the longer the opening.
- **Damage:** light hits 12-18, heavy hits 25-35, grabs and finishers up to 40, against the player's 100. Lingering hazards (fire, thorns) tick 5-8.
- **Phases:** each change is a short, safe set piece (roar, the part breaking, a new look). The bar shows a notch for each phase.
- **No adds**, no leash change: leaving twice the arena radius, or dying, resets the boss to full health.
- **Ship part:** each wears a stolen ship part in its design, dropped on defeat as before.
- **Animation:** rigged and animated in Blender (one armature per boss, one action per move), played through an Animator with blended transitions. Small procedural layers on top: the head tracks the player, and the body leans into turns.

## The player's dodge

- **Left Alt** (rebindable as "Dodge"): a quick dash in the direction you're moving (backwards if standing still), about 4.5 m in 0.3 s.
- The first 0.22 s are invulnerable (i-frames), so you can dodge *through* an attack if timed to it.
- 0.6 s cooldown. It works on the ground and once in the air.

## Briarwing, the Thorn Wyvern (the Wreck Field)

*A wyvern overgrown with the thornwood that survives under the dunes. It tore the ship's wing panels off and wove them into its own wings with briars.* Ship part: **wing panels**.

**Look:** a long-necked, two-legged wyvern on its folded wing-arms. Dark bark-brown hide plated with grey-green thorny scutes. Rows of long thorns down the neck, back and tail, and thorns at every wing knuckle. Torn, ochre-brown wing membranes with the silver hull panels lashed into them. Glowing green eyes and a green glow deep in its throat. Wingspan about 14 m.

| Phase | Health | What changes |
| --- | --- | --- |
| 1: Briar | 100-65% | Ground melee and the thorn barrage. |
| 2: Bristling | 65-30% | Its thorns grow out and glow. Adds the spinning dive and a delayed third swipe. |
| 3: Thornstorm | 30-0% | Adds the spike dive bomb and longer combos. Thorns left behind linger longer. |

Moves:
- **Wing-claw swipes:** a left then right swipe with the wing claws, stepping forward each time. In phase 2+ there's a third, delayed overhead slam, which catches early dodges.
- **Tail lash:** when you're behind or beside it, it plants and whips its thorned tail in a 220° arc. Jump it or dodge through it.
- **Briar lunge:** it rears its head back, then lunges forward 8 m and bites. Sidestep it.
- **Thorn barrage:** it rears up and spreads its wings, the thorns glow, then it fires three volleys of five thorns in a fan. In phase 2+ the volleys track you. Thorns that miss stick in the ground for a few seconds as small hazards.
- **Spinning dive** (phase 2+): it hops into the air, hangs there and corkscrews down along a line at you, leaving a trail of thorns. A long recovery while it untangles is your big opening.
- **Spike dive bomb** (phase 3): it takes off high, and its shadow follows you on the ground. It drops, and spikes burst out of the ground in two expanding rings. Get out of the shadow, then jump or dodge the rings. The spikes stay a few seconds as a briar wall, then it is stuck in the ground for a moment.

Openings: after the third swipe, the lunge, the spinning dive and the dive bomb. Its throat glow is a weak spot (extra damage) while it barrages.

## Cindermaw, the Ash Drake (the Mesa Pass)

*A wingless ground dragon from the mesas that ate the ship's rocket thruster and grew round it. The thruster now sits fused into its spine, fed by the fire in its gut.* Ship part: **rocket thruster**.

**Look:** a long, low, heavy four-legged drake. Charcoal-black hide in faceted plates, with molten seams that flow (the flowing-oil shader in lava colours). A jagged crest of horns, a heavy jaw and a club tail. The ship's thruster bell juts from its back between the shoulders, ringed with scorched metal and heat-glow. About 10 m long.

**Reworked 11-10** after Lawson's first look ("very basic and similar to the original"): a more acrobatic, thruster-driven moveset, and real particle smoke in place of the solid grey puffs.

| Phase | Health | What changes |
| --- | --- | --- |
| 1: Smoulder | 100-75% | Raking claws, tail whip, pounce, drift charge, flame breath, fire lob. |
| 2: Smokescreen | 75-45% | Smoke clouds it hides in and attacks out of; afterburner sidesteps; its charges and pounces leave smoke. |
| 3: Afterburner | 45-20% | Charges come in pairs, and landings and the claw slam send out fire rings. Adds the rear slam. |
| 4: Meltdown | 20-0% | Faster. Adds the Inferno Circuit and the Supernova. It overheats after big moves. |

Moves:
- **Raking claws:** a right claw, a left claw, then it rears up with both claws high, holds them there (the delay that catches an early dodge) and crashes them down. It steps in during each wind-up.
- **Tail whip** (when you're beside or behind it): it lashes the club tail one way, then whips it back the other. Two hits; jump them.
- **Thruster pounce:** it crouches with the thruster lit and rocket-leaps for where you're heading (a shadow marks the landing), crashing down with both claws.
- **Drift charge:** the glowing, revving crouch (it aims, then locks), then an 18 m charge that keeps steering after you slowly, leaving fire. It ends in a skidding tail sweep, then shakes its head: the opening. Hitting a wall stuns it longer.
- **Flame breath:** a sweeping cone of fire that leaves burning ground.
- **Fire lob:** it spits three molten globs in high arcs at marked spots, the first at you and the others where you're likely to dodge.
- **Afterburner sidestep** (phase 2+): a thruster-kicked dash out of your line, straight into a follow-up: claws if you're close, breath at mid range, a pounce from afar.
- **Smokescreen** (phase 2+): it vents smoke clouds round the arena and slips into one. The smoke greys out your view while you're in it. It comes out with a pounce or a charge, given away about 0.8 s ahead by the thruster flaring through the smoke and its roar.
- **Rear slam** (phase 3+): it rears, thruster-hops and slams down; a ring of fire rolls outward. Jump it or dodge through it.
- **Inferno Circuit** (phase 4): a lap of the arena leaving a ring of fire, then two charges across the middle, then it overheats.
- **Supernova** (phase 4): it rears as the thruster goes white-hot for 3 s (a marked 10 m circle), then explodes round itself. Get out or dodge the blast. It overheats for 5 s afterwards.

**The smoke** is real particle smoke (soft puffs, the same smoke texture as the game's fires). It rolls over the ground, piles against walls (the particles collide with the world) and swirls aside as the drake or the player moves through it.

Weak spot: the thruster bell, which takes double damage, and triple while it overheats.

## Pitchclaw, the Oilstone Scorpion (the Buried Hull)

*A scorpion of living rock that nests in the buried hull. It burrowed into the ship's fuel tank and grew around it, and now fuel runs through the cracks in its stone like blood.* Ship part: **fuel tank**.

**Reworked 11-10** after Lawson's first look, from his reference image of a creature armoured in rock with glowing cracks:
- **Covered in rock:** angular, faceted rock slabs cover the body, legs, claws and tail, with thin cracks between them.
- **Oil in the cracks:** the cracks are black, glossy fuel with pulses of light flowing outward from the tank, swelling the cracks as they pass, like the energy on the Core Worm (the `Signal Lost/Flowing Oil` shader). The oil glows brighter each phase and blazes once it burns.
- **A far longer tail:** twelve segments and about 10 m long. It moves like a third arm: it whips, stabs over its head, reaches out to snare you, and points straight up to rain fuel.
- **Real fluid:** see the fuel below.
- About 9 m long, plus the tail.

| Phase | Health | What changes |
| --- | --- | --- |
| 1: Stone | 100-70% | Snaps, the pincer combo, tail lash, stab, fuel spew. |
| 2: Spark | 70-40% | Adds the stinger barrage, the tail snare, the spark scrape and the burrow. |
| 3: Cracked | 40-15% | Rock breaks away. Adds the leaping crush and the fuel rain; the pincer combo ends in a stab. |
| 4: Pyre | 15-0% | Its fuel burns: the spew is a flamethrower, every pool it makes is on fire, standing close burns. Adds the inferno spin. |

Moves:
- **Snap:** the claw nearer you draws back, opens and snaps.
- **Pincer combo:** left, right, a held-back left (the delay), then a scissor of both claws across the front.
- **Tail lash** (when you're beside or behind it): the tail sweeps low and wide round its side. Jump it.
- **Stab:** the tail cocks, holds, then throws the stinger over its head into the sand about 5 m ahead; it shuffles to put you there first. The stinger sticks: your opening.
- **Fuel spew:** a stream of fuel from its mouth, swept across the ground.
- **Stinger barrage** (phase 2+): three fast stabs over its head, tracking you.
- **Tail snare** (phase 2+): it draws the tail back, then flings it out about 10 m to hook you. If it catches you, it holds you up while both claws crush you twice, then flings you away (about 46 damage in all). If it misses, its tail is out and it's open.
- **Spark scrape** (phase 2+): drags both claws along the ground throwing sparks, lighting any fuel near them.
- **Burrow** (phase 2+): it digs in and sinks out of sight. A trail of churning sand hunts you, stops, marks a circle, and it erupts claws-first under it.
- **Leaping crush** (phase 3+): it springs across the arena onto where you're heading (a shadow marks the landing) and lands with both claws; fuel sloshes out of its cracks round it.
- **Fuel rain** (phase 3+): the tail points straight up and sprays fuel high into the air. Half of it rains down round a marked spot on you, the rest anywhere near it.
- **Inferno spin** (phase 4): two full turns with the tail stretched out low (jump it), flinging burning fuel.

**The fuel** (`FuelDrop`, `FuelPool`):
- **Drops:** every attack's fuel is a spray of drops that fly under gravity, stretched along their motion.
- **On the ground:** drops pour into a pool where they land, or join the pool already there.
- **On walls:** drops hitting a wall run down it and pool at the bottom.
- **On the player:** drops splash, a small sting of damage, and pool at their feet.
- **Pools:** each pool grows with the fuel poured in, spreads out thin, drapes over the terrain, creeps downhill on slopes and runs together with pools it touches. Pools slow you, and slowly soak into the sand.
- **Fire:** any flame lights a pool: its sparks, a burning drop, or a bolt you fire into it. The fire burns across the whole pool and spreads to pools touching it. Fire the player lit burns the scorpion too.

Weak spot: the fuel tank on its back. A charged bolt to the tank makes it backfire, staggering it for 2.5 s (kept from Sludge).

## How it's built

- **Models and animation** (Blender 5.x, `tools/blender/`):
  - `bosskit.py` is the shared kit. It builds low-poly parts, one skinned mesh on a simple skeleton, and key-pose animation clips. It exports the FBX and a clip table. `shell()` covers a limb in faceted rock slabs with cracks between them.
  - `desert_wyvern.py`, `desert_drake.py` and `desert_scorpion.py` are the three bosses.
  - Run `desert_bosses.py`, either through the Blender MCP (`runpy.run_path(..., init_globals={"BOSSES": [...], "NO_PREVIEWS": True})`) or with `blender --background --python tools/blender/desert_bosses.py -- wyvern drake scorpion`.
  - It writes `Assets/Prefabs/Enemies/Desert/Bosses/<Boss>/<Boss>.fbx` and `<Boss>Clips.json`, plus `art/desert-bosses/<Boss>.blend`.
- **The clip table** (`<Boss>Clips.json`) lists every clip's length and its named moments ("hit", "move", "volley" and so on). The boss scripts wait for those moments, so a hit lands on the frame the animation strikes. Retime a move in Blender, re-export, and the fight follows.
- **Unity:** `Assets/Editor/DesertDuelBossBuilder.cs` has two commands:
  - **Signal Lost > Desert Bosses > Build Briarwing, Cindermaw and Pitchclaw** sets up the FBX import, recreates the materials in URP and makes an Animator Controller with a state per clip. It then builds the prefab: bone hitboxes, the weak spot, the hidden ship part that drops on defeat, and the effect meshes and materials.
  - **Create Test Scenes** makes `Assets/Scenes/Bosses/BriarwingTest`, `CindermawTest` and `PitchclawTest`.
- **Scripts** (`Assets/Scripts/Enemies/Desert/Bosses/`):
  - `DuelBoss` is the shared base: phases, poise and staggers, the attack chooser, the breather between attacks, hit windows tied to the clip table, and the head tracking the player.
  - `Briarwing`, `Cindermaw` and `Pitchclaw` hold each boss's moves.
  - `BossFx` holds the hazards: thorns, ground thorns, spike rings, fire rings, particle smoke clouds and effect puffs.
  - `FuelFluid` holds Pitchclaw's fuel drops and pools.
- **Shader:** `Assets/Shaders/FlowingOil.shader` (Signal Lost/Flowing Oil): Pitchclaw's oil cracks and fuel pools, and Cindermaw's molten seams. Pulses run along the mesh's UV.x, which the Blender kit lays out as metres along each limb.
- **The dodge:** `Assets/Scripts/Player/PlayerDodge.cs`, added to the player by `FirstPersonController`. Its key is "Dodge" in the key bindings.
- **The story:** `DesertDunesStory` places the new prefabs in the three arenas. The quest text names them.

| Boss | Health | Phases start at | Poise |
| --- | --- | --- | --- |
| Briarwing | 1300 | 65%, 30% | 150 |
| Cindermaw | 1500 | 75%, 45%, 20% | 170 |
| Pitchclaw | 1700 | 70%, 40%, 15% | 190 |

Damage per hit, as tuned: swipes, claws and snaps 18-22; tail strikes, lunges and stabs 24-28; slams, charges, pounces, crushes and leaps 30-35; dive bomb 35; Supernova 45. Lingering hazards and fuel splashes do 4-9 per tick, and the scorpion's tail snare totals 46. Every number is a serialized field on the prefab.

## Checked (11-10)

- **Models and animation:** built in Blender 5.3 through the Blender MCP (and the same scripts from Blender's command line for the preview sheets in `art/desert-bosses/`). Every vertex is skinned. Briarwing has about 2,600 triangles and 19 clips, Cindermaw 2,660 and 26, Pitchclaw 8,200 (the rock shell) and 21.
- **Batch Play Mode on a disposable copy of the project** (Unity 6000.6.2f1), one run per boss:
  - The prefab builds: every clip has an Animator state, and every material is URP or the flowing-oil shader.
  - The flowing-oil shader compiles. Fuel drops, pools and particle smoke appear in the in-engine shots.
  - In the test arena each boss stands on the NavMesh, faces forward with its feet on the ground, and engages with a roar when the player walks in.
  - Every attack ran to the end against a stand-in player standing still in front (or behind, for tail moves). Combos connect in full, and the snare, burrow, leap and Supernova land.
  - Some moves dealt little or nothing to a player standing still: the fuel spew (one splash), the spark scrape (it mostly lights fuel), and the fuel rain and fire lob, whose drops and globs land after the move ends. The flame breath does up to 55 to a player who stands in the whole sweep.
  - Each phase change fires at its threshold with its title.
  - The dodge travels 4.5 m and its i-frames refuse a 20-damage hit.
  - In-engine contact sheets of each boss mid-attack were reviewed.
- **Regression runs, all passing:** the desert story rebuilt with the new bosses (`StoryBatch`), the full desert story probe, the stage-skip probe and the desert enemy Play Mode smoke.
- **Not done:** nobody has played the fights with a real player in the editor, so the difficulty, the readability of the tells and how the dodge feels are unproven. That is the test Lawson asked to do before the rest of the work goes ahead.

## Open questions

- The trophies (Burnout's Thorn Crest, Sludge's Stinger, Old Buzzard's) keep their old names and models until the rewards pass. That pass also brings the post-boss upgrades.
- The quest-chain text that names the old bosses is updated to the new names. The side-quest overhaul comes later.
