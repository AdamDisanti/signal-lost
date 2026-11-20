# Crystal Caves: level flow

The Crystal Caves version of [desert-dunes-flow.md](desert-dunes-flow.md). **Built on `feature/crystal-caves` (11-15)**, apart from the route fodder (skipped for now). See [How it was built](#how-it-was-built) at the end. The world itself is in [crystal-caves.md](crystal-caves.md). Positions are map metres (480 x 480 m, +Z north, centre 0,0).

## At a glance

1. Arrive by boat at the dock and meet Cavendish at camp.
2. Do three jobs in a fixed order. Each is: three tasks, a mini-boss duel, a trophy on the rack, then a talk with Cavendish, who takes the boss's ship part.
3. Buy the **Crystal Disruptor** from Eugene whenever you can afford it.
4. When Cavendish holds all three ship parts, and you own the Disruptor, enter the deep chamber and fight the Cave Axolotl.
5. It drops the **Flight Control Module**, which clears the stage.
6. Walk out of the cave on your own with the module and the Axolotl's gill frond, and set the frond on the rack. Then Cavendish at camp says you are done with the caves and can depart on the boat.

## The map

| Place | Position | Role |
| --- | --- | --- |
| Camp, cove and dock | South-east ledge. Camp (150, -166), dock about (140, -216) | Spawn and respawn point (the camp). Cavendish gives every job. Four trophy stands. The boat arrives and departs at this one dock |
| Cave mouth | (118, -128), facing south | Only way in |
| Upper level | Front of the cave (south of z -55) and a lobe round the Beetles | Floor about 18 m above sea level |
| Beetles zone | (-150, -42), r 24 m, upper level | Job 1 |
| The ramp | North edge of the Beetles lobe, about (-150, 20) down to (-150, 94) | The only way up from the deep level. The deep level can be dropped into from anywhere on the upper level |
| Shard Mole zone | (15, 12), r 24 m, deep level | Job 2 |
| Geode Bear zone | (134, 64), r 24 m, deep level | Job 3 |
| Cave Axolotl chamber | (0, 138), 152 x 68 m ellipse, deep level | Finale. Nothing overlooks it |

- The order follows the geography: the Beetles first, then the ramp, then the deep level.
- Deep water costs 15 health and puts you back on land.
- Nothing wanders the cave. Every enemy belongs to a fight zone, a route group or the Axolotl chamber.
- Job 1's zone is about 270 m from camp. There is no shortcut, and every hand-in is at camp.

## Quests

The model is the desert's `DesertStory` and `QuestTask`.

- **Order:** fixed. Each boss stays hidden and inactive until its job's tasks are done.
- **Tasks:** all three of a job's tasks are active at once, in any order, with a count on the HUD. Each is an E press. The scanner tracks the nearest.
- **Boss:** walk into the zone and fight. It drops its ship part (walk over it) and a trophy (E to pick up).
- **Rack:** set the trophy on a stand at camp.
- **Hand-in:** talk to Cavendish. The player gives him the ship part, and he keeps it.
- **Trophies:** each boss drops one, a part of the boss: the three mini-bosses and the Axolotl (its Gill Frond), so the camp has four caves stands.

| Job | Tasks | Boss | Ship part | Trophy |
| --- | --- | --- | --- | --- |
| 1. **Ticket to Ride** | Collect concert tickets | The Beetles (Bug Lennon, Paul Arthropodney, George Harrison, Scarab Starr) | Signal amplifier | Elytra |
| 2. **Set in Stone** | Re-seat the crystals the Mole's digging knocked loose | Shard Mole | Antenna array | Mole Hide |
| 3. **Bear with Me** | Restore power nodes | Geode Bear | Power cell | Bear Claw |

Each job's tasks lead the player to its boss. Job 1's tickets are all on the upper level. Jobs 2 and 3 are on the deep level.

### Task positions (rough, check in the editor)

| Job | Positions | Notes |
| --- | --- | --- |
| 1. Tickets | (30, -80), (-55, -72), (-105, -62) | All inside the cave, none in the tunnel. The last is about 49 m from the Beetles zone |
| 2. Crystals | (-110, 72), (-65, 45), (-20, 28) | Trail from the ramp foot east to the Mole. The last is about 38 m from its zone centre |
| 3. Nodes | (55, 0), (90, 30), (105, 88) | Trail east to the Bear. The last is about 38 m from its zone centre |

Every task is at least 38 m from its boss's zone centre, and clear of every other zone and the Axolotl chamber.

**Task text and Cavendish's job dialogue:** drafted, see [How it was built](#how-it-was-built).

## The scanner

- **Raise it with Q.** This holsters the laser. It has a proximity bar and beeps, and **no directional arrow**. This may change if the scanner is upgraded later.
- **Quest signals win at any distance.** While one is on, the scanner ignores creatures.
- **Range:** 200 m.

### Crystal Disruptor

Sold by Eugene for scrap only, at the price already set in his store. The shop keeps the items that still belong here and drops the desert-specific ones. Which stay is decided later.

- With it, hidden crystal enemies are scanner targets and beep.
- To make one emerge, raise the scanner and be near its fake crystal.
- Without it they stay hidden from the scanner.
- The Axolotl fight does not start without it.

### Chameleon override

With the Disruptor, a hidden **route** Chameleon takes the scanner's signal even while a quest signal is on.

- **Radius:** 20 m. Outside it, the quest signal wins. Inside it, the Chameleon's replaces the quest's. If playtests show players losing it, raise it to 25 to 30 m.
- **No tell:** no different tone, colour or message. The player follows it without knowing it leads to an enemy.
- **Ignorable:** the signal stays on the Chameleon until the player leaves its radius.
- **After it emerges:** the scanner returns to the quest it was on.
- **Only the route Chameleons.** Not other enemies, not other levels, and not the Axolotl chamber's Chameleons. Widening it later needs Amanda's approval.
- **Code:** a change to the scanner's priority rule in `ScannerController.ScanForNearestTarget`. It must be opt-in, enabled only by the Crystal Caves upgrade and only for sources marked as Chameleons.

## Enemies

### Mini-bosses

All three are pure duels, as in the desert, with no fodder during the fight. Difficulty ranking: Desert < Crystal Caves < Mudlands.

| Boss | Notes |
| --- | --- |
| Beetles (upper lobe) | Four beetles, 220 health each, one shared fight with no boss bars. Their box-in charge is beaten by jumping as the ring closes. They get angrier as members fall |
| Shard Mole | 3 phases. Claw swipe, tail swoop, burrow with a floor marker, listening after 3 s of silence. Phase 2 adds a claw flurry, phase 3 ceiling shards |
| Geode Bear | 1850 health, poise 200, a step above Pitchclaw. Its yawn slows and blurs for about 2 s |

### Route fodder (Amanda's maps, 11-20)

47 hand-placed enemies from Amanda's second marked map, under *Crystal Caves Story > Route Fodder*: 18 Quartz Bats, 5 Gem Rams, 14 Quill Porcupines, 10 Prism Snails (13 on the upper level, 34 on the deep level). The spots are `FodderSpots` in `Editor/CrystalCavesStory.cs`. *Signal Lost > Crystal Caves > Install Route Fodder* (batch: `CrystalCavesBuilder.InstallRouteFodderBatch`) replaces only that group, and the full story install places it too.

- **Clear of the duels:** every one is at least 45 m from a mini-boss zone's centre (24 m zone plus the 14 m fodder territory, with a margin) and 21 m outside the Axolotl chamber. A spot drawn nearer is pushed straight out from the zone's centre.
- **Toward the paths:** each is pulled toward its route (upper or deep): half the way, at most 15 m, stopping 6 m short of the path and 8 m from task spots. Enemies drawn in different groups (25 m or more apart) stay about 25 m apart, so groups don't merge. If the pulled spot is blocked, it backs off toward the drawn spot.
- **Inside nothing:** a spot that overlaps a collider, a crystal or rock model (decorative crystals have no collider, so their bounds count), a wall, a slope over 20°, or low ceiling moves to the nearest clear ground on the same level, up to 12 m. If none is found, the enemy is left out (a warning in the log). The 11-20 install placed all 47; the largest group is 3.
- **Fodder stays dead after the player dies**, as in the desert.

### Route Chameleons

- **Count:** 3 to 5 on the upper level and 4 to 8 on the deep level, as many as reasonably fit.
- **Near routes**, so the player passes within 20 m of some. Never needed to progress.
- **At least 60 m apart.**
- **Not on or near** quest spots, the rack, boss drops or fight zones.
- **Some on walls** (wall chameleons), but none on the walls near the Axolotl chamber.
- Each stays among real crystal batches.

### Axolotl chamber Chameleons

The Axolotl fight's fodder. They are separate from the route Chameleons.

- **Count:** 6 to 8, spread around the fight zone, set in the ground beforehand as extra crystals.
- **Not on any crystal batch the Axolotl can hide at.** It picks batches at random.
- **Rise automatically when phase 2 starts,** a couple at a time. All of them must emerge, even if the player pushes the fight to phase 3 straight away.
- **Never triggered by the scanner,** and they don't affect it. During the fight it points only at the Axolotl.
- **Not cleared at phase 3.** The player defeats them on their own.

## The finale

1. The Axolotl's chamber is entered only when Cavendish holds all three ship parts (parts the player is carrying don't count) and the player owns the Disruptor. Otherwise it refuses, and the message says which is missing, pointing to Eugene for the Disruptor.
2. **Cutscene entrance, then four phases:**
   1. **Still Pool:** tail swipe and a telegraphed roll.
   2. **Crystal Bloom:** floor crystal spires and a ring of shards to jump. The Axolotl hides, and the chamber's Chameleons rise.
   3. **Cave-In:** marked falling ceiling crystals and double rolls.
   4. **Frenzy:** faster, tighter combos. No flood.
3. It hides as a fake crystal at every phase change, and about every 25 s in phases 2 to 4. Each hide recolours its crystals to the nearest batch's colour. The Disruptor reveals it.
4. Its gills are the weak spot. A roll into a wall dazes it and exposes them.
5. It drops the **Flight Control Module** and scrap.
6. The player picks up the module and the gill frond, walks out on their own (there is no collapse) and sets the frond on the rack. Only then will Cavendish wrap up, and the boat sails only after that. At camp, Cavendish says they have collected everything, are done with the caves, and can depart on the boat to keep searching for the remaining ship parts. The next stage isn't built, so the boat stops at a message.

## How it was built

**Install:** *Signal Lost > Crystal Caves > Install Crystal Caves Story* (batch: `CrystalCavesBuilder.InstallStoryBatch`). It rebuilds the camp prefab and the placeholder trophies, then replaces the *Crystal Caves Story* object in `CrystalCaves.unity`. It is safe to run again, and needs no NavMesh re-bake. *Validate Crystal Caves Story* checks the result.

| Piece | Where |
| --- | --- |
| The story (jobs, hand-ins, finale, homecoming) | `Scripts/Story/CavesStory.cs` |
| Placement, task props, Chameleon placement, validation | `Editor/CrystalCavesStory.cs` (part of `CrystalCavesBuilder`) |
| Axolotl gate (parts handed in) and chamber Chameleons rising | `AxolotlLair.PartCount`, `chamberChameleons` |
| Chameleon override | `ScannerController.ScanForNearestTarget`, `CrystalChameleon.questOverrideRadius` (20 m on route Chameleons only) |
| Chamber Chameleons hidden from the scanner | `CrystalChameleon.scannerIgnores` |
| One dock that also departs | `IslandDock.alsoDeparts` (opens once `CrystalCaves` is cleared; there is no destination, so it shows "the next island hasn't been built yet") |
| Play Mode check | `tools/unity/CrystalCavesStorySmoke.cs` (disposable temp project only). The camp save is shared with the real project (same company and product name), so the test backs it up first and restores it afterwards |

**Decisions made while building (check these):**
- **Stands (Amanda, 11-15):** the caves' three new stands (save slots 6-8) stand in front, east of the fire. Every older stand sits behind them in staggered rows, keeping its trophy: the desert's three (slots 3-5), then the camp's shared three (0-2). The desert camp itself is unchanged (6 stands since Lawson's desert story, 11-09).
- **Hand-in needs both** the trophy on the rack and the part picked up.
- **Trophy models (11-15):** low-poly kits in the desert trophies' style, about 0.7 m on a cave-rock base: the Beetles' red wing cases with crystal studs, the Mole's pelt with shards and claws, and the Bear's paw with three curved claws and a split geode. They are rebuilt on every install, like the desert's. Each boss drops its trophy when beaten, as in the desert. Only a trophy already on a stand in the camp save is skipped.
- **The Axolotl's arena radius is 30 m** (its prefab has 20), so the crystals ringing the chamber's long sides are in its hiding range. Its lair starts the fight 30 m from the chamber's centre.
- **Chamber Chameleons:** 8, at 78% of the chamber's ellipse and at least 48 m from its centre, so they are past every batch the Axolotl hides at. They rise two at a time, 4 s apart.
- **Route Chameleons:** placed beside real batches up to 30 m from the routes. At least three are within 20 m, so the override can trigger. The current layout has 5 upper (one on a wall) and 6 deep (one on a wall). Walls only count when they are the cave's own rock, not the invisible barriers.
- **Spawn and respawn (Amanda, 11-15):** the first spawn is on the dock by the boat, where a voyage lands you too. After the first talk with Cavendish, the respawn moves to just west of the camp fire.
- **Silent until Cavendish (Amanda, 11-16):** before the first talk with Cavendish the scanner has no signals at all (`ScannerController.Silenced`, set by the story). Raised, it doesn't beep and its bar stays empty. Everything works as normal once he gives the first job.
- **Enough scrap for the Disruptor (Amanda, 11-16):** the Geode Bear now drops 4-5 wiring (was 1-3) and 1 fuel cell. The three mini-bosses together always drop at least 8 wiring, 4 circuit boards and 2 fuel cells, against the Disruptor's 8, 4 and 1.
- **Scanner range (Amanda, 11-15):** 450 m in the caves (was 200), so the far chamber's signal still beeps and fills the bar a little from the camp or the dock. Creatures are picked up that far too, when no quest signal is on.
- **Task props:** big concert tickets (11-16) that float upright, bob and slowly turn like a pickup, at 1.2 m. Each is tan card with notched ends, an ink frame round blocky "ADMIT ONE" letters and number dashes, printed on both faces. The player walks through their E box, and it goes with the ticket. Also toppled crystals that stand back up and light, and metal power nodes whose cores light.
- **The task text and Cavendish's lines** are drafted in `CavesStory.cs` (the welcome, reports, wrap-up and reminders) and in `CrystalCavesStory.cs` (job titles, objectives and task prompts).
