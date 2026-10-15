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
- Confirm what counts toward the three encounters and how optional quests assist scanner/boss discovery.
- Laser values remain the combat-demo prototype values. The tutorial flask now uses provisional tuning of 35 HP per sip, one sober stack per 15 seconds without another drink, and bounded distortion/steering/speed penalties. Confirm balance and save triggers with the lead; healing still caps at max HP, with no overheal.
- The mining laser is provisionally a melee tool with a charged shot (see [combat prototype](combat-prototype.md)): left click is a three-swing combo, holding right click charges a bolt whose damage scales with hold time, and a short cooldown follows each shot. This replaces the earlier hold-to-fire beam with an overheat meter. The design document's "charge/cooldown rather than magazines" wording is read here as charge-to-fire plus a shot cooldown. Confirm the rule and values with the lead after playtesting.
- Clarify how the finale's stolen components affect inventory/progression and the ship repair ending.
- Lawson requested a burnt-rock basin for laser placement and an exit gate as [tutorial graybox props](graybox-props.md). The gate's unlock conditions and destination are unspecified; the delivered gate is an open visual frame, not a progression or scene-transition system.
- Lawson requested replacing the box-shaped tutorial environment with a dark rocky [impact site](tutorial-impact-site.md): a broad crater, scattered ship debris, modest hills, and the existing rocket partially buried at its center. The existing terrain slopes were explicitly approved and retained; this environment adds no checkpoint, quest-order, collectible-debris or exit-unlock rules.

- The latest tutorial art request doubles terrain width/length to 320 x 320 meters, preserves ship/debris sizes, adds a laser visual in the basin and requires impassable natural perimeter cliffs. The closed cliff ring is scenery/collision, not an exit-unlock mechanic. Confirm how the existing gate will eventually transition to the next area; no cliff opening, teleport or progression rule was invented. The later [tutorial gameplay integration](tutorial-gameplay.md) makes the placed laser an E-to-pick-up weapon and adds the flask, player health/HUD and a damage-test pad.

## Tutorial gameplay decisions

- Lawson requested the complete existing combat-demo laser in `Tutorial.unity`, with the player initially unarmed. E picks up the basin laser; its original combo, charged shot, recoil, sway and effects are reused, not reimplemented. E also picks up the existing flask, then F drinks it with unlimited uses.
- Lawson confirmed gradual intoxication recovery and blackout **above 8 stacks** (the ninth drink). Blackout returns the player to the starting location sober. Prototype timing/effect strengths are documented in [tutorial gameplay](tutorial-gameplay.md), rather than treated as final balance.
- Lawson requested death retain basic flask/weapon pickups and gear owned before entering the stage, while every stage-start enemy returns to its original position at full health. The current tutorial retains its collected flask/laser and resets its entire stage-start target group on death/blackout. No broader equipment inventory or inter-stage persistence system exists yet; their representation remains an integration question. Other checkpoint placements, save behavior, progression resources and gear acquired during later stages are not invented here.
- Three existing training-dummy actors provide combat targets, not new enemy AI. In the tutorial they stay defeated until the stage resets; the original combat demo retains its automatic three-second dummy respawn.

The remaining clarifications above are unresolved questions, not permission to silently choose new progression rules.
