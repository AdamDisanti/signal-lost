# Design reference

Source: [Game Design Document.docx](reference/Game%20Design%20Document.docx), supplied by the project lead. This is a summary of design intent, not a record of implemented features. Read the original before gameplay decisions. The current user specifies six team members and Unity 6000.6.2f1 with Universal 3D/URP.

## Core experience

A low-poly first-person action-adventure shooter with exploration, goofy alien creatures, and light suspense. The stranded mechanic uses a directional scanner with signal strength and audio feedback to find ship scraps and encounters. The mining laser is the sole planned story weapon, using charge/cooldown rather than magazines. Scrap funds equipment/stat upgrades and merchant purchases. Enemy AI uses idle, chase, attack, and dead states, with expanded boss behaviors.

The flask heals without a fixed use limit; excessive drinking affects visibility and can cause blackout and checkpoint respawn. Cavendish the capybara gives optional quests; the hermit crab becomes a merchant. Per-biome progression includes three mini encounters before the boss becomes scannable; the document allows an early boss fight at much higher difficulty. Previous ship parts gate later biomes.

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

## Desert fodder decisions

- Lawson requested desert-specific fodder enemies, each built around ship junk, to keep spawning around the planned desert mini-bosses (a "Chop Shop" gang: thruster lizard, winged vulture, fuel scorpion; not built yet). Five are implemented as placeholders in [desert fodder](desert-fodder.md): Spark Tick (swarm that latches on), Snorkel Eel (burrower with a shootable snorkel), Jerry-Can Beetle (walking bomb that chain-reacts), Hubcap Armadillo (rolling charger that deflects bolts while curled) and Dish Meerkat (spotter that marks the player and jams the scanner).
- New mechanics they need are scoped to the new enemies: a wave `FodderSpawner` with a cap, the meerkat's "spotted" alert, scanner jamming applied from outside the scanner's own scripts, latching, burrowing, chain explosions and bolt deflection. Existing enemies, the scanner, HUD and tutorial are unchanged.
- No scrap drops, encounter counting, boss unlock or mini-bosses were invented. Whether being spotted should also affect crabs and frogs, and where fodder goes in the Desert Dunes scene, need the owners' agreement (Christian, Nicole).

## Desert mini-boss decisions

- Lawson requested the three desert mini-bosses as a themed gang, each with two phases and with its stolen ship part built into its design and moves. They are implemented as placeholders in [desert mini-bosses](desert-minibosses.md): Burnout (thorny-devil lizard, rocket-thruster dashes), Old Buzzard (vulture with ship wing panels: flying, then grounded after a wing is shot off) and Sludge (scorpion with the fuel tank: oil slicks it ignites, and a tank weak spot that backfires).
- At Lawson's request, each one patrols a set area while fodder keeps spawning around it during the fight. Phase 2 starts at half health. Leaving the arena or dying resets the mini-boss. On defeat the stolen part drops as a placeholder pickup.
- Not decided, so not invented: whether the parts count toward progression, the boss becoming scannable after three defeats, placement in the Desert Dunes scene, scanner priority, and death persistence.

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

The remaining clarifications above are unresolved questions, not permission to silently choose new progression rules.

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
- **Scanner:** it points at the current quest objective, or at the NPC with a quest waiting, at any distance. Quest objectives outrank creatures. This resolves the mini-boss doc's open question for quest targets only; boss priority is still undecided.
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

