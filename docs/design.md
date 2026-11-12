# Design reference

Source: [Game Design Document.docx](reference/Game%20Design%20Document.docx), supplied by the project lead. This is a summary of design intent, not a record of implemented features. Read the original before gameplay decisions. The current user specifies six team members and Unity 6000.6.2f1 with Universal 3D/URP.

## Core experience

A low-poly first-person action-adventure shooter with exploration, goofy alien creatures, and light suspense. The stranded mechanic uses a directional scanner with signal strength and audio feedback to find ship scraps and encounters. The mining laser is the sole planned story weapon, using charge/cooldown rather than magazines. Scrap funds equipment/stat upgrades and merchant purchases. Enemy AI uses idle, chase, attack, and dead states, with expanded boss behaviors.

The flask heals without a fixed use limit; excessive drinking affects visibility and can cause blackout and checkpoint respawn. Cavendish the capybara gives optional quests; the hermit crab becomes a merchant. Per-biome progression includes three mini encounters before the boss becomes scannable; the document allows an early boss fight at much higher difficulty. Previous ship parts gate later biomes.

**Current scanner prototype:** the active in-hand scanner no longer has a directional arrow. It retains its proximity bar and audio feedback. This is an implementation change; the supplied design document's directional-scanner intent above remains unchanged.

**Buried-enemy prototype:** the scanner can track an underground `EnemySignalSource`; the current burrow/emergence test is limited to one Hermit Crab scene instance. The scanner also excludes defeated enemy sources with `Health` and advances to another eligible target. See [burrow and emerge](burrow-emerge.md) and the [scanner prototype](scanner-prototype.md).

| Area | Planned boss / reward |
| --- | --- |
| Crash site tutorial | Recover laser/flask; learn movement, scanner, combat, resources, and healing |
| Desert / Dunes | Technology-enhanced hermit crab / Engine Core |
| Fog Forest | Nest Guardian / Navigation Computer |
| Crystal Caverns | Crystal-infused Cave Beast / Flight Control Module |
| Return to crash site | Cavendish in a mech / Reactor Core |

Win by recovering all four components, defeating the bosses, repairing the ship, and escaping. Levels are handcrafted. HUD includes health, scanner direction/strength, scrap, and objectives. Planned options include control adjustments, audio, save, and quit. Mouse/keyboard is planned; controller support is conditional. Playtesting metrics include play time, deaths, biome times, boss attempts, and resource collection.

## Scope as written

**Prototype:** tutorial, healing, one biome and boss, basic weapon skill tree, HUD/settings, and basic merchant/quest systems.

**MVP:** tutorial plus four playable levels, four bosses, scanner, mining laser upgrades, enemy state machines, quests/merchant, repair ending, settings/UI, and saves.

**Stretch:** enemy variants, extra upgrades/weapons, collectibles, boss phases, storytelling, and alternate endings including the 100% casino ending. Extra weapons are a stretch idea that conflicts with the sole-story-weapon statement; do not treat them as core scope.

The source proposes an eight-week sequence: foundation/prototype (weeks 1–3), biome/progression work (3–6), finale and save integration (6–7), then testing/polish/submission (7–8). Calendar dates are not supplied.

## Clarifications before implementation

- Source roster names five people; current team size is six. Sixth member, responsibilities, and all GitHub usernames are pending.
- Respawn location is described both as biome entrance and last checkpoint. Confirm checkpoint placement and what progress/resources persist after death or blackout.
- Clarify whether the tutorial crab, merchant, and desert boss are the same creature or separate characters.
- Christian chose a low-poly hermit crab as the basic enemy (see [enemy prototype](enemy-prototype.md)). It wears a swappable shell; the first shell is a metal bucket. Whether this enemy is related to the tutorial crab, merchant, or desert boss is still the open question above. It uses the document's four states (idle, chase, attack, dead); its health, damage, speeds and stagger rules are placeholders.
- Christian's second enemy is a mortar frog with a tin can on its back that lobs arcing goo (see [frog enemy](frog-enemy.md)). Shooting the can while it loads makes the shot backfire. Its numbers are placeholders.
- Confirm what counts toward the three encounters and how optional quests assist scanner/boss discovery.
- Laser values remain the combat-demo prototype values. The tutorial flask now uses provisional tuning of 35 HP per sip, one sober stack per 15 seconds without another drink, and bounded distortion/steering/speed penalties. Confirm balance and save triggers with the lead; healing still caps at max HP, with no overheal.
- The mining laser is provisionally a melee tool with a charged shot (see [combat prototype](combat-prototype.md)): left click is a three-swing combo, holding right click charges a bolt whose damage scales with hold time, and a short cooldown follows each shot. This replaces the earlier hold-to-fire beam with an overheat meter. The design document's "charge/cooldown rather than magazines" wording is read here as charge-to-fire plus a shot cooldown. Confirm the rule and values with the lead after playtesting.
- Clarify how the finale's stolen components affect inventory/progression and the ship repair ending.
- Lawson requested a burnt-rock basin for laser placement and an exit gate as [tutorial graybox props](graybox-props.md). The gate's unlock conditions and destination are unspecified; the delivered gate is an open visual frame, not a progression or scene-transition system.
- Lawson requested replacing the box-shaped tutorial environment with a dark rocky [impact site](tutorial-impact-site.md): a broad crater, scattered ship debris, modest hills, and the existing rocket partially buried at its center. The existing terrain slopes were explicitly approved and retained; this environment adds no checkpoint, quest-order, collectible-debris or exit-unlock rules.

- The earlier tutorial art request doubled terrain width/length to 320 x 320 meters, preserves ship/debris sizes, adds a laser visual in the basin and requires impassable natural perimeter cliffs. The closed cliff ring is scenery/collision, not an exit-unlock mechanic. Confirm how the existing gate will eventually transition to the next area; no cliff opening, teleport or progression rule was invented. The later [tutorial gameplay integration](tutorial-gameplay.md) makes the placed laser an E-to-pick-up weapon and adds the flask, player health/HUD and a damage-test pad.

## Tutorial gameplay decisions

- Lawson requested the complete existing combat-demo laser in `Tutorial.unity`, with the player initially unarmed. E picks up the basin laser; its original combo, charged shot, recoil, sway and effects are reused, not reimplemented. E also picks up the existing flask, then F drinks it with unlimited uses.
- Lawson confirmed gradual intoxication recovery and blackout **above 8 stacks** (the ninth drink). Blackout returns the player to the starting location sober. Prototype timing/effect strengths are documented in [tutorial gameplay](tutorial-gameplay.md), rather than treated as final balance.
- Lawson requested death retain basic flask/weapon pickups and gear owned before entering the stage, while every stage-start enemy returns to its original position at full health. The current tutorial retains its collected flask/laser and resets its entire stage-start target group on death/blackout. No broader equipment inventory or inter-stage persistence system exists yet; their representation remains an integration question. Other checkpoint placements, save behavior, progression resources and gear acquired during later stages are not invented here.
- The prototype-demo tutorial now also contains one existing hermit crab and one existing mortar frog in a separate encounter, without changing the training dummies. Both use the stage-start reset; no new encounter rewards, quests, or boss progression are defined.
- Three existing training-dummy actors provide combat targets, not new enemy AI. In the tutorial they stay defeated until the stage resets; the original combat demo retains its automatic three-second dummy respawn.

## Tutorial traversal decisions

- On the current main-based tutorial, Lawson explicitly selected **480 x 480 meters** (1.5x width/length, 2.25x ground area), **natural formations plus wreckage**, and **required platforming on some routes**. The new geography supersedes the earlier map-wide height invariance; it does not authorize scaling the player, rocket or props.
- Lawson subsequently rejected the extreme mesa/canyon and mine-like interpretation. He selected **natural terrain plus sparse wreckage** and supplied a moon reference showing craters, rounded hills and scattered stones. The current [lunar layout](tutorial-impact-site.md#lunar-terrain-direction) retains 480 meters but replaces the six industrial/traversal landmarks with 57 multi-scale impact bowls and continuous rolling relief. Grey regolith, pale ejecta, a dark airless sky and neutral low sunlight guide appearance; the reference does not imply literal Earth scenery, low gravity, oxygen mechanics or a change from the fictional setting.
- The central crash floor and full saved ship/core-actor transforms remain unchanged; original rock/debris counts/scales/rotations and working pickup/pad/target/gate roles are retained. Existing movement, health, combat, scanner/HUD and healing/intoxication systems are not changed. The earlier mandatory eight-jump installation course is superseded by natural exploration with eight tested walk-only circuits; no replacement movement, reward, checkpoint or fall-damage rule was invented.
- The surrounding rim remains closed for safety, lower than the extreme pass and blended into terrain. Final art acceptance, physical-input comfort, target-hardware performance and later exploration rewards/progression remain playtest/design questions. The earlier traversal assets remain inactive with GUIDs preserved; the separately stashed foliage/pond pass was not restored.

## Desert / Dunes decisions

- The first biome prototype is [Desert / Dunes](desert-dunes.md): open low-poly dunes with a crash-debris streak, three encounter sites with hermit crabs that the scanner can detect, and an empty boss arena with a half-buried ship engine, all closed in by unclimbable perimeter dunes. The player respawns at the biome entrance, as the source document says. No encounter counting, boss unlock, boss, merchant, quest, scrap or biome exit was invented; those are marked as placeholders. Confirm level-design ownership with Lawson.
- Superseded for the encounters, boss, quests and exit by [the desert story (11-08)](#the-desert-story-11-08): the three encounter sites are now the mini-bosses' arenas, the hermit crabs there are gone, and the boss is placed in the level.

## Desert fodder decisions

- Lawson requested desert-specific fodder enemies, each built around ship junk, to keep spawning around the planned desert mini-bosses (a "Chop Shop" gang: thruster lizard, winged vulture, fuel scorpion; not built yet). Five are implemented as placeholders in [desert fodder](desert-fodder.md): Spark Tick (swarm that latches on), Snorkel Eel (burrower with a shootable snorkel), Jerry-Can Beetle (walking bomb that chain-reacts), Hubcap Armadillo (rolling charger that deflects bolts while curled) and Dish Meerkat (spotter that marks the player and jams the scanner).
- New mechanics they need are scoped to the new enemies: a wave `FodderSpawner` with a cap, the meerkat's "spotted" alert, scanner jamming applied from outside the scanner's own scripts, latching, burrowing, chain explosions and bolt deflection. Existing enemies, the scanner, HUD and tutorial are unchanged.
- No scrap drops, encounter counting, boss unlock or mini-bosses were invented. Whether being spotted should also affect crabs and frogs, and where fodder goes in the Desert Dunes scene, need the owners' agreement (Christian, Nicole).

## Desert mini-boss decisions

- Lawson requested the three desert mini-bosses as a themed gang, each with two phases and with its stolen ship part built into its design and moves. They are implemented as placeholders in [desert mini-bosses](desert-minibosses.md): Burnout (thorny-devil lizard, rocket-thruster dashes), Old Buzzard (vulture with ship wing panels: flying, then grounded after a wing is shot off) and Sludge (scorpion with the fuel tank: oil slicks it ignites, and a tank weak spot that backfires).
- At Lawson's request, each one patrols a set area while fodder keeps spawning around it during the fight. Phase 2 starts at half health. Leaving the arena or dying resets the mini-boss. On defeat the stolen part drops as a placeholder pickup.
- Not decided, so not invented: whether the parts count toward progression, the boss becoming scannable after three defeats, placement in the Desert Dunes scene, scanner priority, and death persistence.
  - Placement, and what follows the three defeats, are now set by [the desert story (11-08)](#the-desert-story-11-08). Scanner priority and death persistence are still open.

### Mini-boss polish pass

Lawson requested these changes after playing the mini-bosses. See [desert mini-bosses](desert-minibosses.md#polish-pass-fire-animation-new-moves).

- **Fire:** the bosses' fire should look like proper fire, not spheres in a puddle, and the effect should be reusable elsewhere, for example in stage design. It is now the general [fire effect](fire-effect.md), a code-built particle fire with prefabs and a GameObject menu. It does no damage on its own.
- **Old Buzzard:**
  - Its animation was too stiff. Now every pose eases in, the wings have elbows, flapping has a proper cycle, and the head tracks the player.
  - The sand attack should reach to the ground and fling sand rather than raise a wing. It now scoops with the wing down in the sand, then flings it forward.
- **Burnout:**
  - Follow-up dashes should target the player. Each follow-up now re-aims at the player with a short lead, curves toward them mid-dash and banks off walls toward them. This resolves the "targeted multi-dash" item in the meeting notes below.
  - At its last 15% of health it stays in a rapid dashing state until defeated (the redline).
- **Sludge:**
  - It felt uninteresting, so it now fires more oil puddles in one attack: 6, or 9 in phase 2.
  - New charged explosive shot that the player must take cover from. The blast checks line of sight, so cover fully protects; creatures and puddles are not cover. A sight line shows whether you are covered, and a heavy hit on the tank while it charges backfires it (consistent with its existing weak spot).
  - It was weak up close, so it now has a claw combo, a tail sting that reaches any side, and a short flamethrower, and it no longer backs away.
  - Its body was too stiff. It is now a segmented scorpion: five bending body plates, jointed legs in an alternating gait, claw elbows and a five-joint whipping tail.
- **Numbers:** timings and damage values for the new moves are placeholders, not agreed balance.
- **Sludge pressure follow-up:** Lawson requested faster projectiles, wider oil coverage, longer flamethrower reach, quicker movement/turning and more frequent shooting. Combat speed is now 4.5 m/s, oil slick radius 2.6 m, flamethrower reach 10 m and cannon speed 60 m/s, with shorter ranged wind-ups/cooldowns in both phases. Health and damage per hit are unchanged; these are provisional tuning values, not a new progression rule. See the [Sludge tuning](desert-minibosses.md#sludge-fuel-tank).

## Desert stage boss decisions

Lawson requested a main boss for the desert stage. See [desert stage boss](desert-boss.md).

- **Scope:** it is a stage boss, not the game's final boss. It is substantially harder than the mini-bosses, with four main phases, grander effects and more attack variety.
- **Design (from Lawson's quiz):** a giant sandworm with the **Engine Core** built in. It has its own moveset rather than echoing the mini-bosses. Each phase changes the arena (sandstorm, collapsing ground, fire field), and the attacks are dense and overlap, needing precise dodging.
- **Story beat (Lawson's direction):** when the player leaves the stage to put the parts on the ship, the boss rockets out of the ground where they were about to exit, grabs them and drags them to its arena, in a cutscene.
- **Progression (Lawson's direction):** the Engine Core is one of the main ship parts needed to complete the game. Beating the boss unlocks the next stage, which doesn't exist yet.
- **Implemented as placeholders:**
  - A session-only `StageProgress`; no save system was invented.
  - The exit requires the three mini-boss parts.
  - A configurable next-stage scene name, with a message until that stage exists.
  - The name "Core Worm".
- **Not decided, so not invented:** placement in Nicole's Desert Dunes scene; saving and death persistence of parts and cleared stages; what the next stage is; balance numbers.
- **Look (Lawson's direction, superseding the earlier ship-fused version):** a biological, desert-coloured worm, darker than the sand for contrast, with veins of energy pulsing across one solid bending body (no segments), keeping the petal mouth. Machine parts were removed apart from the Engine Core's brace. Its health is 825, lowered at Lawson's request.
- **No friendly fire (Lawson's request):** enemies are never hurt by their own attacks or each other's: fire, blasts, the cannon shell, the worm's shrapnel and the mortar frog's goo all skip enemies (`EnemyAllegiance`). This supersedes the earlier "fire and blasts hurt fodder" notes. Hazards the player sets off still count as the player's: shooting an oil slick, a fire the player started, or a Jerry-Can Beetle the player shot or ignited.
- **Flask in the fight (Lawson's request):** the player has the Scotch flask during the Core Worm fight, with the tutorial's healing and intoxication rules. The tutorial's blackout sends you back to the start, so here a blackout counts as going down. Whether the flask comes from the inventory once stages are linked is still open.

## Crystal Caves world (11-08)

Amanda's request: a new Crystal Caves level built from her design map and reference images, world only. See [Crystal Caves](crystal-caves.md).

- **Size:** 480 × 480 m, which is 1.5 times Desert Dunes' 320 m.
- **Layout (Amanda's answers):**
  - The cave fills the island under high mountains.
  - A walkable rocky shelf wraps along the south coast. Amanda chose this over the drawing's small ledge.
  - The dock hangs off a cliff, with the cave mouth visible from it and no path to it.
  - Two cave levels, the deep one about 16 m lower, joined by three ramps.
- **Dock and boat:** no earlier level had them. Amanda chose reusable static prefabs; travel between levels is not built.
- **Workflow:** the Desert Dunes pattern (an environment prefab that can be rebuilt, and a scene created once then edited by hand), at Amanda's choice.
- **Look:** a bright coastal day outside. Inside, the crystals (six colours) are the only light; a scripted fade turns off the sun and sky light under the cave roof.
- **Chosen while building (open to change):**
  - the player spawns on the dock with all gear;
  - Eugene is already friendly and Cavendish has small talk only;
  - falling into the sea respawns you on the dock;
  - a faint ambient fill in the cave so rock shapes read.
- **Not invented:** enemies, quests, boss, level flow or travel. The fight zones are empty CombatZone markers.

## Crystal Caves revisions (11-09)

Amanda's fixes and changes after reviewing the world. See [Crystal Caves](crystal-caves.md).

- **Roof:** part of the cave, left of the entrance, was open to the sky. The roof only covered ground north of the portal line. It now covers the whole cavern, and the terrain self-check refuses any cave ground left uncovered.
- **Smaller outside:** the mountains now rise straight from the sea everywhere except the south-east ledge, so the only open ground is the ledge with the camp, the cove and the cave mouth.
- **Dock:** no more staircase. A cove of low rocks steps down from the ledge to the water, and the dock starts at its foot. The cove points at the cave mouth, which stays visible from the dock (the self-check covers this).
- **Brighter cave:** the cave fill is now bright enough that every part of the cave is easy to see. No crystals were added.
- **Upper level only at the front and the beetles:** the back of the cave is all deep level. Nothing up there overlooks mini boss 2's or the final boss's zone: the nearest upper ground is about 90 m from mini boss 2's centre. The beetles' zone sits in a 62 m lobe, about 38 m from any drop. There are three ramps, two from the front and one from the lobe.
- **Teammate's islands update:** the teammate's pushes (islands, docks and voyages on main; the crew sailing along, island camps and outfits on `claude/crew-voyage-outfits`) were merged into `feature/crystal-caves`. The level now uses them:
  - the island kit's sea, deep-water rescue, arrival dock and sailboat, and a not-walkable sea floor;
  - Cavendish and Eugene at an island camp (a base camp variant with an `IslandCamp`), set up as in the desert.
  - These replace the level's own dock, boat, ocean and `FallRespawn`, which were removed. Eugene no longer has ambush cousins here, matching the desert camp.
- **Nothing climbable (later on 11-09):**
  - **The bug:** the player could reach the top of the mountains by pressing Jump over and over.
  - **The cause:** the shared player can jump on any slope that isn't vertical.
  - **First attempt, withdrawn:** a footing and sliding rule in the shared `FirstPersonController`. Amanda found it made ordinary ground and the ramp slip, and wanted every level to keep its original movement. The player controller is unchanged.
  - **The fix:** vertical rock walls instead, wherever the player must not climb:
    - the mountains' foot round the ledge;
    - both sides of the cut to the mouth;
    - the upper level's cliff in the cave, with a flat top back to the edge and full height where it meets the cave wall.
  - **Getting back up:** you may jump down from the upper level, but the only way back up is one ramp, moved from near the entrance to the far side of the beetles' zone, so you find it after that fight.
  - **Getting stuck:** Amanda got stuck on the way up to the cave. The ledge had steep spots: hard 2 m terraces, a short pad blend at the top of the cove, and a steep rise at the mountains' foot. The ledge is now soft-terraced and gentler, and the walk from the dock to the cave stays under 35°.
  - **The check:** the terrain self-check verifies all of this.
- **Polish (later on 11-09):**
  - **Ramp:** the ramp still made the player slip, so it is now a long, even incline of about 12° (it was an S-curve, steepest in the middle at about 38°), and wider. The self-check holds it to 20°.
  - **Walls:** the barrier walls looked broken (thin sheets with gaps behind them and seams at the mouth). They are now solid blocks that run back into the hillside, and the mountains' and the cut's walls are one piece.
  - **Camp on a slope:** the camp sat on sloping ground, with some pieces floating and others sunk. The widened ease at the top of the cove had reached its pad. The camp's pad now always wins, and validation checks every camp piece stands on the ground.
  - **Cave mouth:** looking out of the cave there was a block on one side and a wall with a hole on the other, not visible from outside. Two things caused it: the cut's walls ran 8 m into the tunnel, and the mountain wall's solid top reached out over the cut. The walls now stop at the mouth and turn into a flat face beside the opening, no top reaches over the cut, and tall props are kept out of the view in front of the mouth.
  - **Corner right of the mouth:** a slab stuck out of the mountain wall where it turns from the cut onto the mountain foot. The wall's top had run 15 m straight back over open ledge, because the high ground there is diagonally behind the wall. Tops now reach only the nearest hillside within 8 m, in any direction behind the face, or are left off.
  - **Camp overrides (this level only):** the supplies crate and barrel are moved clear of the tent, and Eugene sits on the ground in front of his log instead of floating on top of it.
- **Not decided, so not invented:** Crystal Caves outfits and camp styling (no `StageTheme` yet, so the crew wears its Crash Meadow look), and a departure dock and voyage route (waiting on the Fog Forest).

The remaining clarifications above are unresolved questions, not permission to silently choose new progression rules.

## Crystal Chameleon (11-09)

Amanda's request: the Crystal Caves fodder enemy, a chameleon with crystal scales that hides among the cave's crystals. See [Crystal Chameleon](crystal-chameleon.md).

- **Camouflage:** it lies buried with only its crystals showing (the cave's own crystal meshes and materials). Hidden, it can't be shot and the ordinary scanner doesn't see it.
- **Reveal:** the scanner's camouflage-disrupting upgrade isn't built yet, so `CrystalChameleon.Disrupt()` is the hook for it. Disrupting turns its crystals red, and it rises through the shared `BurrowEmerge`.
  - The burrow-emerge branch was merged in, as Amanda chose.
  - `BurrowEmerge` gained an opt-out of its proximity trigger and a public `Emerge()`.
- **Fighting:** a ranged enemy. A telegraphed tongue lick, a straight line that walls stop. Its crystals stay red while it fights and go dark when it dies.
- **Drops:** scrap, like the other fodder.
- **Test scene only:** its own test scene, with instances set to **Start Revealed**. It is not placed in the level yet.
- **Not decided, so not invented:** emerging from walls (only ground is built), and whether a hit while it is camouflaged should reveal it.

## Shard Mole (11-11)

Amanda's request: the Crystal Caves mini-boss, a blind mole with glowing crystal claws and a crystal-tipped tail that hunts by sound. It's built like the desert mini-bosses (not the beetles, at her direction). See [Shard Mole](shard-mole.md).

- **Hearing:** after 3 s of player silence (standing still, laser quiet) it stops and listens until it hears them again.
- **Her moves:** the burrow ambush (a floor marker tracks it and marks where it bursts out), the tail swoop (damage and knockback) and the claw swipe.
- **Her picks (11-11):** three phases. Phase 2 adds the claw flurry and phase 3 the ceiling shards. It drops the **antenna array** ship part plus scrap. About 4.5 m long.
- **Test scene only:** it is not placed in the level yet.
- **Not decided, so not invented:** a weak spot, its own sounds, and whether crouch-walking is quieter.

## Chief Toad decisions (10-23-26)

Lawson's request: a chief toad boss for the tutorial pond, built in its own test arena first. See [Chief Toad](chief-toad.md).

- **Design:** from Lawson's concept art. He wears the ship's cooling system (pump, glowing coolant tubes, chest core).
- **Attacks (Lawson):** a high-pressure water beam, water blades, a jump slam whose splash radiates out from the impact, and a stunning scream.
  - He may leave the pond to fight.
  - His water effects work out of the water too.
- **Answers to the questions asked before building:**
  - **Phases:** three. A new pattern at 66% and at 33%.
  - **Scream:** dodgeable, a 1.5 s stun inside a telegraphed 12 m ring, blocked by line-of-sight cover.
  - **Reward:** the ship's cooling system, as a recoverable part.
  - **Branch:** a separate `feature/chief-toad-boss` branch.
- **Chosen while building (open to change):**
  - 750 HP.
  - Phase 3 adds the "tidal spin" (a low spinning beam to jump).
  - The chest core is a ×1.5 weak spot.
  - The splash wave only hits a grounded player.
  - Parts now count per stage, so the cooling system does not count toward the desert exit's three parts.
- **Open:**
  - his placement and arena radius on the tutorial map;
  - whether the cooling system gates tutorial progress;
  - balance against the rest of the tutorial;
  - his own sounds (placeholders reused for now; see [sound effects](sound-effects.md)).

## Cavendish and merchant tutorial integration

The project lead confirmed world-space text with movement active, E interactions, and a dedicated larger merchant crab carrying the flask. Cavendish assigns the first quest; the existing basin laser is used for the fight. The loose flask becomes a one-time drop on the merchant's defeat. The lead's final clarification supersedes the earlier fight-location option: after surrender the crab walks back and stands beside Cavendish. Both NPCs stay in the tutorial for this step. The shop is a disabled purchase preview with no scrap economy. See [tutorial NPCs](tutorial-npcs.md).

The accepted quest and friendly merchant persist through tutorial death/blackout within the current Play session. They do not persist across reloads/saves. Existing dummies and regular enemies are preserved. The merchant's size, health and attack reach are provisional prototype tuning, not a biome-boss or new economy rule.

### NPC dialogue and HUD refinement

Instructional dialogue now advances only on E. Cavendish's first quest uses a screen-space popup; finishing its instructions accepts the quest and reveals a top-right HUD tracker. Regular Cavendish reminders remain world-space text, also E-driven. Friendly merchant introductions are E-driven and lead to a separate screen-space shop window. Combat/surrender chatter may auto-cycle. The merchant has no E interaction prompt until defeated. Movement remains active; closing/cancelling an unfinished conversation does not grant a quest. No shop economy or additional quest content is added.

### Overhead flavour versus quest information

The project lead clarified that first-encounter and combat crab chatter should appear overhead, while all quest-related instructions must be interacted with and shown in popups. The hostile crab keeps its no-E-prompt rule; its first approach greeting is proximity-triggered. After defeat its first friendly E greeting appears overhead, then the next E opens the reward/trading popup. Cavendish's quest reminders and completion instructions also use popups; only non-quest small talk remains overhead.

## Tutorial core loop (meeting notes 10-20-26)

Adam and Lawson's meeting notes asked for a complete tutorial before the demo. The project lead chose the scope and resolved the open rules below. See [tutorial core loop](tutorial-core-loop.md) for how it plays.

- **Quest order:** wake up beside Cavendish, then "Gear Up" (find the laser in the basin and the scanner in the wreckage), return, "Hair of the Crab" (beat the flask crab, collect the flask), follow the crab home, hear its scrap and shop rundown, then report back. This supersedes the earlier single "Recover Your Flask" quest.
- **Dialogue:** subtitles at the bottom middle plus speech bubbles for chatter. Quest information goes in a rounded, transparent popup that advances with a click or E. Earlier rules still hold: walking stays active and cancelling does not accept.
- **Items:** Q swaps between scanner and laser, and F drinks. The scanner is found in the tutorial. The two-handed, both-items-at-once idea from the UI notes was not built.
- **Scanner:** quest signals (the current objective or an NPC with a quest waiting) remain selected at any distance and outrank creature signals. In the current implementation, the in-hand display has no directional arrow: its bar strength is calculated using the active scene's range, so a distant quest can remain selected while showing zero strength. This resolves the mini-boss doc's open question for quest targets only; boss priority is still undecided.
- **Healing (supersedes 35 HP flat, blackout above 8):** each sip heals half as much per drunk stack and returns to full once sober. Blackout comes on the sip after 5 stacks.
- **Laser (the lead wants the starting weapon to feel weak):**
  - a 10 s cooldown on the charged shot only;
  - the third combo hit stuns desert fodder only (not crabs, frogs or bosses) for 2 s;
  - melee then needs 3 s to recharge. The meeting notes' "3 second cooldown for the stun combo" is read here as a pause before the next swing.
  - These apply in the tutorial; code defaults leave other scenes unchanged until their owners opt in.
- **Scrap:** four placeholder types (Scrap Metal, Wiring, Circuit Boards, Fuel Cells). Enemies drop them, and walking over them collects and sorts them. A placeholder inventory opens on Tab, and the merchant menu is a priced, disabled preview. No trading, upgrades or save.
- **Angry merchant:** hits on the friendly crab bring non-blocking red warnings. One hit is forgotten for every 10 s without another. The 6th hit calls 5-10 hermit crabs. Beating them or dying makes the merchant surrender again, and the shop stays open.
- **Crash site:** a new stripped shipwreck replaces the rocket, and the rocket's assets are kept. The empty sockets for the engine core, reactor core, flight-control module and navigation computer show what the bosses took. The site is much more cluttered. The player now wakes up beside Cavendish instead of at the old spawn.
- **Not done:**
  - Moving the tutorial enemies out from under the terrain was not selected, so the encounter NavMesh was not rebaked around the wreck.
  - The next-stage transition (heal and black out after a stage) and the other UI ideas (Doom-style HUD, on-screen hands, skyboxes) are still open.
  - The "screeb" spotter behaviour is still open. Burnout's targeted multi-dash was done in the [mini-boss polish pass](#mini-boss-polish-pass).


## Tutorial overhaul (meeting notes 10-22-26)

The "Major tutorial rework" prompt, with the project lead's answers. See [tutorial](tutorial.md), [enemy rules](enemy-rules.md) and [shared player, HUD and menus](core-player.md). Where it conflicts with the 10-20 decisions above, this section wins.

- **New scene:**
  - The tutorial is a new generated scene. Tutorial Testing is reference only and becomes a free-play sandbox (renamed Testing Sandbox); none of its dummies, rock basin, gate or damage pad carry over.
  - New Game loads it.
  - All art is generated in-project, with no asset packs.
- **Look:**
  - a green meadow in golden late-afternoon light, with trees, rocks and foliage;
  - hills, groves and a forest belt break every end-to-end sightline;
  - the wreck sits dug into a berm, with electrical fires and smoke;
  - the livery shows the mechanic served the Gleebinglorb Republic Maintenance Corps.
- **Story:**
  - Cockpit, then dragged to camp, then wake by the fire across from Cavendish.
  - Search the wreck for the hidden laser and scanner; returning early gets "keep looking".
  - The big crab with the bottle, then the merchant and its ten cousins, then the walk to camp, then the shop introduction.
  - The tutorial stops before upgrades and the skill tree, which are the next prompt.
- **Ten cousins (supersedes "5-10 hermit crabs"):**
  - always the same ten, each uniquely named and dressed;
  - defeated cousins despawn;
  - the angry merchant's sixth hit brings back those same ten.
- **Fighting radius for all enemies:** attack inside the radius, chase to 2× the radius, then go home and heal to full.
- **Stuns (supersedes "fodder only, 2 s"):**
  - Everything except bosses is stunned for 1 s, only by the combo finisher.
  - Plain hits no longer stagger; this fixes the crab stun-lock caused by its 20-damage stagger threshold matching the 20-damage swing.
- **Laser:**
  - The charged-shot cooldown scales from 4 s (10% charge) to 8 s (full).
  - A full charge bursts for full-to-half damage within 4 m, which helps the hack-and-slash feel against groups.
  - Health values are unchanged.
- **Hands and UI:**
  - The right hand holds the laser or the scanner (Q), and the left hand drinks the flask (F). The hands stay fixed on screen.
  - A bottom status bar and a pause menu (Save is a placeholder).
  - A Terraria-style title screen with New Game, Continue, Settings and Exit.
- **Defaults chosen without a specific instruction (confirm or change):**
  - The opening is first-person, and Cavendish slides and bobs rather than animating.
  - The ship is named "GRS Rusty Wrench".
  - The cousins' names and lines.
  - Hiding spots: the laser in the reactor bay and the scanner under a hull panel by the tail, each with an 8 m glint.
  - Space skips the opening.
  - Cousins drop one guaranteed wiring each, so the fight pays for a first upgrade.
- **Open:** audio; a real save; upgrades and the skill tree; whether bosses should share the 2× leash or keep their own reset rules (they now use the 2× rule on their arena radius).

## Final tutorial tweaking (meeting notes 10-23-26)

Adam's "Final tutorial tweaking" notes, with his answers. See [tutorial](tutorial.md), [upgrades](upgrades.md) and [shared player](core-player.md).

- **Movement:** tap C to crouch or stand; hold C while sprinting to slide; jumping out of a slide keeps its speed (slide cancel), so slide-jump chains are the fast way around.
- **Pop-ups:** tutorial and item cards are dismissed by the player (E or Enter), never on a timer. **This applies to every pop-up from now on.**
- **Angry merchant:** called-back cousins drop no scrap (no farming).
- **Laser:** combo recharge 2 s, shown by the three combo pips draining; bolt cooldown 1 s (tap) to 6 s (full charge), eased between (about 2.8 s at half charge).
- **Flask:** heals 35 / 28 / 22 / 17 / 13 (gentler than halving); the top drunk pip drains as a drink wears off; the sip is 30% slower and reaches down for the flask before snapping it up; burps from the 3rd sip; overdrinking reads "SIGNAL LOST - BLACKOUT".
- **Merchant:** renamed **Eugene** (from Eugine).
- **Scrap and upgrades:** four scrap models from Adam's references; seven tutorial upgrades, two supplies and three trades, all fixed recipes (no crafting); key-item recipes supported for mini-boss drops.
- **Pockets:** four tabs (the notes said three but listed four): Materials, Gear (3D mechanic wearing upgrades, tool close-ups, supplies), Upgrades, Stats.
- **Tutorial ending:** buy a first upgrade (required before Cavendish's next job), clear 20 pond frogs in three growing waves (5, 7, 8), then take the Chief Toad quest; the tutorial stops there until the mini-boss is merged.
- **Open:** the Chief Toad itself and its reward (ideas in [upgrades](upgrades.md#ideas-for-the-chief-toad-reward-not-built)); costs and frog drop rates need a play-test; crouch and slide feel can only be judged by playing.

## Demo round (meeting notes 10-23-26, second list)

- **Music:** the title screen plays "Signal lost main menu music" on a seamless loop (imported uncompressed so the loop has no gap); the Music slider controls it.
- **Low health:** at 30 health or less, a pulsing red vignette, a heartbeat and a "drink [F]" reminder.
- **Chief Toad in the tutorial:** spawned only after Cavendish's quest; a short rise-from-the-pond cutscene with his boss bar; 3-5 frogs kept alive during the fight (no scrap from them); beaten, he drops a loot pile including his crown.
- **Base camp and trophies:** the camp is one prefab (`Prefabs/Core/BaseCamp.prefab`) with three trophy stands, to be placed in every later level. Boss trophies are picked up from the loot and set on a stand; placed trophies are saved to disk (`CampSave`), and New Game clears them. This is the game's first real save data.
- **Demo:** the tutorial scene is now **Tutorial Demo**; the demo build is the title screen plus that scene. Adam asked for a Mac build now and Windows-ready setup for teammates (the Windows module is not installed on his Mac). See [demo build](demo-build.md).
- **End of the demo:** Cavendish's wrap-up, then a "Thanks for playing" card (Keep Exploring / Main Menu).

## Demo polish (10-23-26, third list)

- NPCs sit on the camp logs when home: Cavendish on his log (his unrigged model sinks onto it), Eugene hops onto the other log after walking home (now 50% faster).
- The flask pips drain right to left like the combo pips; the dropped flask floats and spins.
- Footsteps are a soft low bass note.
- Lore: bosses' main ship parts go to Cavendish "for safe keeping"; the tutorial asks for the cooling system to be handed over.
- The Chief Toad's beam tilts to the player's real height and distance, and both the jet and its hitbox are wider.
- Pause menu centred.
- Shop: type filters, a Purchased filter, scrap amounts as icons, and a preview-then-confirm purchase. The pockets' Upgrades tab shows only what is installed.
- The merchant crab and its cousins only appear once Cavendish gives the big-crab quest.

## Islands instead of cliffs (11-07)

Lawson asked for an ocean-island system in place of the cliff boundaries; see [islands](islands.md).

- **Islands.** The tutorial's mountain ring and the desert's perimeter dunes and entrance canyon are now beaches into the sea.
- **Docks.** There is a departure dock at the end of each stage and an arrival dock where the previous stage's boat lands. Interacting with a departure boat plays the voyage cutscene: sailing out, a wide aerial shot, a fade while the next scene loads, then sailing in and zooming back into the player's eyes.
- **No swimming.** Feet more than 1.3 m under the surface fade to black, cost 15 health and put the player back on the nearest dry land.
- **Water.** The water shader was reworked to be clearer, higher resolution and low-poly, in the style of the reference picture.
- **Placement and gates (my calls, not specified):**
  - The tutorial boat is behind Cavendish's camp and unlocks when the tutorial is finished; the demo end card still shows.
  - The desert arrival dock is at the player's start, and its departure dock is past the biome exit marker. That boat unlocks with the Engine Core and says the Fog Forest isn't built yet.
- **Open:**
  - Whether the tutorial's "Thanks for playing" card should stay now that the boat continues to the desert.
  - Inventory, health and gear do not carry between scenes yet. That is the save system's job, so the desert still starts with its own player setup.
  - Whether wading should slow the player.
  - Whether the voyage needs sound (sea, sail, gulls).
  - Whether 15 health is the right cost for deep water.
  - Whether the far islands should match the real next stages more closely.
  - The desert's Core Worm exit (in its test scene) and the departure dock are separate; when the boss goes into the desert scene, the worm's ambush should probably happen on the way to the dock.

## The crew sails too, camps and island outfits (11-08)

Lawson asked for three things (see [islands](islands.md)):
- The player physically boards the boat.
- Cavendish and Eugene come along, and set up camp near the arrival dock on the next island. The camp keeps the same structure with island theming.
- The player, Cavendish and Eugene change clothes for each island.

- **On the boat:** the voyage shows the mechanic's own body (the pockets figure's rounded model, from the player model revamp) walking aboard and standing at the tiller.
  - Cavendish and Eugene come down the dock and climb in.
  - The boat is now twice the size (about 10 m), with a raised boom and three thwarts, so the three of them fit.
- **Who comes:** Cavendish always does. Eugene comes once he is a friendly trader, which he always is by the time the tutorial boat unlocks.
- **Arrival:**
  - Everyone steps ashore. A short cut shows Cavendish and Eugene at the camp site while the camp goes up piece by piece; then they sit down by the fire.
  - The camp sits just inland of the desert's arrival dock, on the biome entrance's flat ground. It replaces the old "Cavendish (quest giver)" and merchant-spot placeholders there.
- **Desert camp:** the same pieces in the same layout as the base camp, restyled for the desert:
  - sandstone fire ring, driftwood logs and stands, and a terracotta-and-indigo tent;
  - additions: a sun shade over Cavendish's log, a rug and water jars.
  - The trophy stands show the same saved trophies.
- **Outfits (my calls on the details):**
  - Crash Meadow is everyone's current look.
  - In the desert:
    - **The mechanic:** khaki suit with rust trim, a bush hat with goggles, a neckerchief and suede gloves. This shows on the first-person sleeves too.
    - **Cavendish:** a sand-and-terracotta suit, his helmet frame and lamp off, a straw sun hat, sunglasses and a neckerchief.
    - **Eugene:** a beach parasol in his bucket, goggles and a bandana.
  - Each island's look is set by a `StageTheme` in its scene.
- **Desert residents are the desert scene's own:** the Cavendish and Eugene who step off the boat belong to the desert scene, so they are there (seated by the fire) however the desert is loaded. The tutorial's NPCs are not carried across scenes.
- **Open:**
  - Should the desert camp be missing anyone who didn't actually board? For example, sailing with `IgnoreRequirements` before Eugene was met. It always has both today.
  - Cavendish has small talk in the desert but no quests there yet.
  - Eugene's shop works, but inventory and upgrades still don't carry between scenes (see above).
  - Nicole owns the Desert Dunes scene: the camp install removed the two placeholders and added the camp, the residents and a `StageTheme`.

## The desert story (11-08)

Lawson asked for the desert to be set up with a short series of quests. See [desert story](desert-story.md).
- Each quest has a task, then one of the three mini-bosses in its own themed arena, which works as a proper arena.
- Placing the final trophy on its stand springs the Core Worm. It captures the player and takes them to a large underground arena.
- After the worm is beaten and its trophy collected, the ceiling falls in and the player emerges above ground.

- **Built:**
  - **Cavendish's three jobs, in order:**
    - Old Buzzard: pick up its feathers, then its Roost in the Wreck Field.
    - Burnout: put out its fires, then the Scorch Raceway in the Mesa Pass.
    - Sludge: seal the sump's leaks, then its Fuel Sump at the Buried Hull.
  - **Each mini-boss** appears only once its task is done. It drops its part and a new trophy, which goes on a new three-stand Chop Shop rack at the desert camp.
  - **The Core Worm:**
    - The third trophy on the rack springs the worm at camp, and it drags the player into its lair, a cavern 22 m under its old sand bowl.
    - The Engine Core plus a new fang trophy bring the roof down.
    - Redesigned three times at Lawson's request. His last brief (11-09): it should always play out the same, with physics-faithful interactions between the rubble, look natural, and leave an opening to climb out.
      - A gash in the roof, choked with broken rock you can see from the start, gives way.
      - The floor where it lands flashes red first; staying there gets you crushed.
      - Sand pours in as a debris cone, and about 220 angular rocks tumble down it.
      - The fall is a physics simulation run once when the level is built, and recorded. The game plays the recording, so it is identical every time.
      - The build checks the settled rocks leave a walking route out, and re-runs the fall without any that block it.
      - The gash stays open to the sky and the player climbs the rubble out. It all stays, so both areas connect.
    - The fang goes home on a stand, and Cavendish wraps up.
- **This settles, for the desert:**
  - **Placement:** the mini-bosses and the boss are now placed in the Desert Dunes level.
  - **What counts as the three encounters:** the three mini-bosses.
  - **When the boss comes:** after all three, via their trophies.
  - **The parts:** the mini-boss parts are recovered as before, and handed to Cavendish "for safe keeping" as in the tutorial.
- **My calls (open to change):**
  - the fixed job order;
  - the tasks;
  - an extra rack rather than sharing the three boss stands;
  - the fang as the worm's trophy;
  - the ambush at the camp rather than at the stage exit;
  - removing the old hermit-crab encounters.
- **Open:**
  - Nicole owns the Desert Dunes scene and environment; this regenerated both. The site radii grew from 16 m to 24 m, which reshuffled where some rocks and cacti landed.
  - Saving the story's progress: only the trophies persist today.
  - Balance of each fight in its new arena.
  - Whether the tutorial camp should also show stands 4-6.

## Testing tools (11-08)

Lawson asked to mark the End-key cheat as a testing tool, and for a second tool: the left and right arrows go to the next or previous stage. All quests of the stages before the one you land in count as done. See [testing tools](core-player.md#testing-tools).
- **What "done" means here (my call):** the stage is cleared, its ship parts and main part are recovered, and its boss trophies are on the stands. The stage you land in starts fresh.
- **Arrow keys:** removed from strafing (`Player/Move`) so that walking with them can't skip a stage.
- **Open:** whether the tools should be compiled out of release builds.

## Upgrade tiers, spawn safety, key bindings and title cycle (11-07)

- **Upgrade tiers.** Each upgrade is a line of small tiers (I, II, III), each dearer than the last; see [upgrades](upgrades.md). Totals at max tier match or slightly beat the old one-off upgrades: bolts +30%, health +30, two extra sips. Open: final costs need play-testing against how much scrap the tutorial actually hands out.
- **Spawn safety.** `Enemies/Common/SpawnSafety.cs` refuses an enemy spot that is off the NavMesh, has something solid within 7 m overhead, is below a terrain, overlaps solid geometry, or has no complete NavMesh path to the player (or the respawn point). Frog waves, desert fodder waves and the crab-cousin ambush pick only safe spots, and hand-placed crabs, frogs and fodder move to the nearest safe spot (within 8 m) when they start. Open: spots under tree canopies lower than 7 m count as "under" something and are skipped; frog waves fall back to the nearest NavMesh (with a warning) if a whole bank has no safe spot, so the quest can't stall.
- **Key bindings.** Keyboard and mouse controls can be rebound on the Controls page; gamepad stays fixed for now. The data is ready for the save system (see [core player](core-player.md)). Open: whether gamepad rebinding is wanted, and whether the number keys used by menu tabs should be rebindable.
- **Text padding.** One padding value per box size, the same on all four sides (`UiTheme.Pad`, `PadSmall`, `PadLarge`).
- **Title screen.** A Terraria-style day-night cycle, 150 s per day, opening at dusk.

## Combat and crash-site polish (11-07)

Lawson's list, as built. Numbers are provisional, not agreed balance.

- **Crash-site debris:** the random scatter of 140 pieces is replaced by a crash layout. A scorched skid trail runs down the trench, with wreckage dug in and pointing the way the ship slid. It is bunched where it struck and bounced (at the trench fires): a torn wing section on the trench lip, cargo spilled out of one side, light junk thrown past the nose, and ploughed dirt along the trench edges. About half as many pieces. Trees that grew in the trench are gone; the rest of the forest, the hollow and the pond are unchanged. See `CrashDebrisLayout`.
- **Merchant spawn flag:** the merchant crab has a **Spawn After Quest** setting. With it on, the crab and its cousins stay out of the world until the quest that sends you after them calls `SpawnForQuest`. In the tutorial that is Cavendish's big-crab quest (this was already the rule; it is now a setting on the crab instead of only the saved scene state).
- **Jump smash (player):** in mid-air with the laser out, press C (gamepad B) to raise the laser and dive. Landing sends a shockwave that hurts and shoves everything around you.
  - It is the player's version of the Chief Toad's jump slam.
  - Damage scales with the height you dived from: 15 at 0.8 m up to 60 from 8 m or higher (capped there). The radius grows from 2.5 m to 5.5 m.
  - Heavy Slam adds to it like it does to the combo finisher.
  - It does not stun: the 10-22 rule keeps stuns to the combo finisher. 1.2 s cooldown.
  - No fall damage was added. **Open:** whether it should stun, its numbers, and whether C is the right button.
- **Collectables on the ground:** gear, the flask, scrap, trophies and ship parts all show at one size (0.55 m on the longest side). They float with their middle 0.8 m above the ground (lower under a low ceiling, so hidden items stay hidden), bob, and spin. The scanner and laser now float in their hiding places instead of lying there. **Open:** whether big main ship parts should be larger than scrap.
- **Chief Toad beam:** both variants are used in every phase and take turns.
  - The **tracking jet** has a long windup (2.3 s, never under 1.6 s) that makes it obvious it is coming and that you need cover, without on-screen text. A water orb swells in his mouth, a sighting line locks onto you, a target ring follows you, and lock-on pings speed up into a sharp lock tone. It then turns after you faster than you can run.
  - The **torrent sweep** paints a wedge on the ground over the arc it will cover, then sweeps a much bigger, wider torrent slowly across it, so it stays on you longer.
  - Details in [Chief Toad](chief-toad.md).

## Player model revamp (11-07-26)

Christian's direction, as built. Still built in code, with no art assets.

- **First-person hands:** inspired by chunky, pudgy cartoon hands: a puffy palm, short thick fingers with round tips and a chunky thumb. The fingers now close: a fist with nothing out, wrapped round the laser or scanner, round the flask while drinking. They keep the tan work gloves, cream cuffs and orange sleeves.
- **The mechanic's build:** short and stocky, with a round belly, stubby legs, short arms and a big head with almost no neck. The outfit (orange suit, cream stripe, belt, teal Maintenance Corps patch, orange cap with cream badge, gloves, boots) and face (dark eyes, nose, stubble) are unchanged.
- **Where it shows:** the first-person hands and the pockets menu's Gear-tab figure. The player still has no body in the world (no shadow or legs when looking down), and the HUD face is unchanged.
- **Drinking pose:** the flask is now held in the fist with its neck to the mouth and tipped up for the swig. Before, it poked out of the back of the hand.
- **Smoothness pass (same day, Christian's request: "more smooth and not so blocky, especially the hands"):** the gloves, suit and arms are now each one seamless mesh built from rounded pieces melted together, so no seams or lumps show. The gloves are skinned so fingers bend smoothly; fingers are fatter, and in a fist the thumb wraps across the front. The head, cap, boots, eyes, patch and badge are rounder. The meshes are baked into an asset in the editor so scenes don't hitch building them.
- **Holding and drinking pass (same day, Christian's list):**
  - "The gun, flask and sensor don't seat well in the hands": each is now held by its real grip, measured against the actual models. The laser's pistol grip sits under its body, below the screen, so Christian chose to carry the laser **6 cm higher and 16 cm further out** so the holding arm shows (Player prefab `gripPosition` -0.02,-0.08,-0.22 → -0.03,-0.02,-0.06). The scanner is gripped by its side rail; the pockets figure holds both the same way.
  - "Small stubble with his mouth showing": the stubble band is replaced by a flat mouth and small stubble dots on the chin and jaw, as on the HUD portrait.
  - "More natural drinking, a big swig, easily turned into hold to chug": the flask comes up from below to the mouth with its bottom tipped up and the head tipped back, with two gulps; the pose is driven by `raise` and `gulps` so a hold-to-chug can stretch the swig. **Open:** the hold-to-chug mechanic itself (not built).
- **Open:** whether the gloves should stay or become bare hands like the reference (`FirstPersonHands` glove colour), and whether the player should get a world body later.

## The Mudlands and Drunk Driving (11-08-26)

Christian's request, built on `feature/mudlands`; the full design is in [The Mudlands](mudlands.md).

- **A new biome after the desert**, not in the original document's list. Same loop as before: camp, Cavendish's quest lines (three, in any order), a mini-boss at the end of each, then a stage boss. Built by a code builder like the tutorial and desert.
- **Drunk Driving** is the area's ability, unlocked by the Learner's Permit key item from the first quest line and kept after. It reuses the flask button: holding it chugs the scotch flask dry (2 drinks, no healing) and summons a car for 14 s. Small creatures and wooden things get run over; anything big or solid head-on makes it explode (area damage and stun) and you are thrown clear. 30 s cooldown; refused when too drunk, at camp, or with no room.
- **Exception to the 10-22 stun rule:** the car's explosion stuns, including on mini-bosses, because the boss designs ask for it (Kart Kong's kart breaks, Silt walls it off). **Open.**
- **New upgrade slot "Car"** with seven car upgrades, plus three general Mudlands upgrades; three use the mini-bosses' key items.
- **Stage boss getaway:** Clutch McCheddar flees at 15% and must be rammed with the car; the chase hands out a free car, so the ability can't be on cooldown there.
- **Open:** where the Mudlands sits in the stage order; all numbers; whether the driving HUD should move to the game's HUD style.

## The Beetles mini-bosses (11-09)

Amanda's request: the Crystal Caves mini-boss is four beetles who want to be a band. See [The Beetles](cave-beetles.md).

- **Model:** the Jerry-Can Beetle model, without the can and the explosion, made mini-boss sized.
  - Crystal growths on the shell.
  - Small, stubby legs, and no antennae or mandibles, so it doesn't trigger bug phobia.
  - A microphone pack on the back and a headset mic.
- **Who they are:** identical apart from colour. Bug Lennon is red, Paul Arthropodney yellow, George Harrison blue and Scarab Starr green.
- **Talking:** each talks about the band (rock and roll, underground pop and other cave genres) when hit, through the dialogue subtitles, like the merchant crab's cousins.
- **Box-in:** they surround the player and charge together.
  - **Warning:** they rear up, their mic lights blink amber, and a closing ring marks when to jump.
  - **Jumped:** they slam into each other and hurt themselves.
  - **Grounded:** only the player is hurt.
- **Amanda's choices (11-09):**
  - No boss bars (revised the same day, after four stacked bars covered the screen): like the crab cousins, a beetle's name and health show in the crosshair target readout while you aim at it.
  - No on-screen text for the box-in at all: the beetles' moves, mic lights and the closing ring are the only warning, and the player works out the jump themselves.
  - **Committed charge (Amanda's fix):** the beetles charged after a player who was outside the circle. Now:
    - Once the ring appears, the box is fixed.
    - The charge goes straight at the box's middle and never turns after the player.
    - Unless a grounded player in the box takes the hit, the beetles collide, whether the player jumped or got out during the charge.
    - Leaving the box before the charge calls the box-in off. The beetles chase again but hold their attacks for 2.5 s.
  - Scrap drops only, no ship part.
  - No per-beetle phase 2. The band gets angrier as members fall: faster, with more frequent box-ins and a shorter warning.
- **Shared code:** `DesertMiniBoss` gained opt-outs of phase 2 (`HasPhaseTwo`) and of the boss bar (`ShowsBossBar`), and a protected `Engage()`. The defeat banner no longer mentions a part when there is none.
- **Test scene only:** it is not placed in the level yet.

## Final level and boss (planning 11-06 and 11-09; first slice 11-09)

Adam's planning sessions; see [final boss](final-boss.md).

- **Supersedes, for the finale:** the design document's "Return to crash site: Cavendish in a mech / Reactor Core" stays. These are added:
  - a wasteland theme;
  - raid-style shield tasks;
  - Eugene's crew call-in;
  - an escape off the island, then the space casino.
- **The parts:** each island's main part becomes one of the mech's weapons, and the mini-boss parts are its armour. This includes the Mudlands' Landing Gear, which wasn't in the design document's list.
- **Built in the first slice:** see the final boss page. The rest is placeholders, listed there.
- **Shared code touched:**
  - a Finale stage, last in the stage-skip tool, so the right arrow in the desert now goes to the Wasteland;
  - a `call_crew` key binding (G);
  - `PlayerSlowReceiver.ExtraSlow`;
  - the scene added to the build list.
- **Open:** balance, the mech's final look, the approach and cutscenes, and the island order in between.

## Melee, rage and Moon Shoes (11-10)

Adam's request and answers; see [melee, rage and Moon Shoes](melee-rage.md).

- **Where:** only the final boss level and the melee test arena (Adam, same day: kept out of the rest of the game so it doesn't conflict with the team's work). Those two scenes hand out Knuckles III and Moon Shoes III; nothing is sold, and the shop, pockets, upgrade catalog and main HUD are unchanged.
- **Fists:** bare fists join the Q cycle (laser → scanner → fists) there.
- **Gaussian Brass Knuckles:** the tricks unlock by tier:
  - I: the magnet lunge;
  - II: overcharge marks, chain lightning and the magnet pull;
  - III: finishers on V, with a shockwave.
- **The spreading effects (Adam's wording):** punches mark; melee kills and punches on marked enemies throw chain lightning; finishers slam a shockwave plus lightning and set off nearby marks; higher rage reaches further.
- **Rage:** any kill +1, melee +2, finisher +3, up to 10. Each stack adds 6% to every hit the player deals (bosses included) and 3% speed. It drains after 6 s without a kill.
- **Moon Shoes:** three tiers of run speed and jump height. Tier III adds a double jump in the direction the keys point.
- **Design document:** it calls the laser the sole story weapon and extra weapons a stretch goal. The fists are treated as gear for the hands, at the lead's request.
- **Test arena:** `MeleeArena.unity`, built on the Enemy Test box, with the finale's robot horde and testing keys.
- **In the final boss (Adam, same day):**
  - the repair robots can be finished below a quarter of their health (`Finishable`; never the mech);
  - the horde is weighted to melee rushers and spawns closer;
  - no rebalance until playtesting.
- **Open:** balance, whether finishers heal, a gamepad finisher button, the jump smash with fists, and whether and where the gear is sold later.

## Mudlands alien reskin (11-10-26)

- Feedback to Christian: the Mudlands enemies and bosses read as Earth animals, and their metal as new human gear, while the rest of the game uses goofy alien creatures built around ship junk. Decision: make the creatures alien and turn their metal into scrap from the crashed ship, changing as little as possible (colours and a few parts; no behaviour, numbers or sizes).
- Done for the five Mudlands creatures and the world (see [Mudlands](mudlands.md), Status). Ship scrap is shown one way everywhere: off-white hull (`MudPalette.Hull`), the orange stripe, rivets (`MeshKit.ScrapPanel`).
- Kept: clothes and props that are jokes (overalls, straw hat, vest and bow tie, moonshine barrels), and the words on signs, since the quest uses them.
- Not done yet: the biker mice and the four bosses (suggested: salvage kart for Kart Kong, gill frills for Silt, ship running light on the witch's broom, salvage chopper for Clutch).
- Second pass (Christian, same day): "give the world a more alien feel especially the houses, avoid the overuse of metal, ensure the collision of all buildings works". Houses became the swamp folk's own architecture (pod husk, ribs, mushroom caps, leaf roofs, root stilts, mint-glowing round windows) on their old footprints; ship salvage is kept to the test kiosk and gas pumps; every building's collision follows its shape, with roofs kept out of the NavMesh. Details in [Mudlands](mudlands.md), Status.

## Desert boss revamp (11-10)

Lawson asked for the three desert mini-bosses to be rebuilt as harder, Souls-leaning fights with 3-4 phases each. The design and his answers are in [desert-bosses.md](desert-bosses.md). Briarwing, Cindermaw and Pitchclaw replace Old Buzzard, Burnout and Sludge in the desert story; the old prefabs and scripts stay in the project, unused by the story.

- **Decided:** a dodge now (Left Alt, i-frames); heavy hits (a big attack takes about 30% of the player's health); pure duels with no fodder; the stolen-ship-part frame kept with new names; a mostly grounded wyvern; smoke clouds with a glow tell; fuel that ignites; large bosses.
- **Open, until the rewards pass:** the trophies (Old Buzzard's Tail Feathers, Burnout's Thorn Crest, Sludge's Stinger) and Cavendish's lines about them keep their old names; the post-boss upgrades and items are not designed yet.
- **Open, until the quest pass:** the tasks before each fight (pick up what Briarwing dropped, put out Cindermaw's fires, seal the leaks round Pitchclaw's sump) are the old ones with new names. The pickups along Briarwing's trail are still the old feather props.
- **Open:** whether the old Chop Shop prefabs (and their test scenes) should be deleted once the new fights are approved.
