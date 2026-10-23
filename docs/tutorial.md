# Tutorial

The playable tutorial from the 10-22-26 meeting notes ("Major tutorial rework"), finished by the 10-23-26 notes ("Final tutorial tweaking"). It replaces the lunar crash-site tutorial; that scene is now **Testing Sandbox**, a free-play sandbox. New Game on the title screen loads it.

- Scene: `signal-lost/Assets/Scenes/Tutorial/Tutorial Demo.unity`
- Build order: MainMenu, Tutorial, Testing Sandbox, Desert Dunes, Prototype

## How it plays

1. **Crash.** The mechanic comes to in the burning cockpit. Cavendish's face appears at the smashed side window and he hauls them out through it, onto the grass; they black out, then catch two glimpses of Cavendish dragging them across the meadow, then wake by his campfire with Cavendish sitting across from them. **Space** skips the opening.
2. **A Rude Awakening.** Cavendish introduces himself, notices the Gleebinglorb Republic Maintenance Corps patch, and sends the mechanic to search the crash site.
3. **Salvage Run.** Two items are hidden in the wreck, and each glints only within about 8 m:
   - the **mining laser** is wedged under a rib in the open reactor bay;
   - the **scanner** is under a fallen hull panel by the tail, on the port side of the trench.

   Once the scanner is found, it points at the laser. Coming back to Cavendish early gets a "keep looking" line. The quest tracker ticks both items off.
4. **The Big Crab.** With both items found, Cavendish describes a big crab that scuttled off with a massive bottle. The scanner now points at it. It waits in a hollow north-east of camp, ringed by boulders, bushes and pines. The trail from the crash fades out before the hollow, and the only gap in the ring faces away from the crash site.
5. **The fight.**
   - The merchant crab and its ten cousins, the **Bucket Brothers**, share one fighting radius (14 m).
   - Defeated cousins despawn.
   - If the crab gives up first, it drops the bottle (the Scotch flask, glowing amber like the hidden gear, visible from any distance) and waits for the brawl to end.
   - Once the whole family is down, it leads the mechanic back to camp.
6. **Bucket & Barter.** At camp the crab introduces itself as **Eugene** and its shop opens. The fight leaves enough scrap for a first upgrade (each cousin drops a guaranteed wire). See [upgrades](upgrades.md).
7. **Kitted Out.** Cavendish won't give his next job until an upgrade is bought; then the scanner points back to him.
8. **Pond Life.** Cavendish sends the mechanic to the pond due north of camp. **20 mortar frogs** come in three growing waves (5, then 7, then 8), each hopping in from the bank once the last is beaten. The scanner points at the nearest frog; the tracker counts them. Frogs drop scrap metal and fuel cells, about two more upgrades' worth.
9. **The Chief Toad.** Back at camp, Cavendish hears a deep croak: the frogs have annoyed the Chief Toad (Lawson's boss, see [chief toad](chief-toad.md)). He is not in the world until this quest is given.
10. **The fight.** Coming within his arena of the pond (40 m, widened for our pond) cuts to a short scene from the bank: the toad rises out of the middle of the pond in a burst of water and his boss bar appears (Space skips). Then the fight, with 3-5 frogs always hopping in (they drop no scrap).
11. **The trophy.** Beaten, he leaves a loot pile: his cooling system, scrap, and **his crown**. Pick up the crown, take it home and set it on one of the camp's **trophy stands** by the tent. It is saved to disk, so it stays in the camp in later sessions and later levels (New Game clears it).
12. **The end.** Cavendish wraps up the demo, and a "Thanks for playing" card offers Keep Exploring or Main Menu.

After that Eugene is an ordinary friendly shop. Its sixth hit within the anger window brings back **the same ten cousins** around it (the angry-merchant ambush); called-back cousins drop no scrap, so the ambush cannot be farmed.

## The Bucket Brothers

Each cousin is a Hermit Crab prefab variant with the basic crab's health and moves. Its own name shows in the HUD's target readout. Each also has its own junk shell, colours and size, and a bark plus last words shown as subtitles.

| Cousin | Shell | Notes |
| --- | --- | --- |
| Moe | Paint can with drips | Bossy |
| Larry | Ribbed tin can | "He's hitting me!" |
| Curly | Traffic cone | Nyuk nyuk |
| Shemp | Teapot | |
| Clawdius | Centurion helmet with a red crest | "Et tu, mechanic?" |
| Sir Pinchington | Top hat | Duellist |
| Barnacle Bob | Barnacled diving helmet | Pirate |
| Crabigail | Flower pot with daisies | Protective of her petunias |
| Little Steve | Coffee mug | 0.72× size |
| Big Steve | Oil drum | 1.32× size |

Each cousin drops 2-3 scrap metal, 1 wiring and 0-1 circuit boards.

## The level

The level is generated, so a rebuild gives the same result:

- a golden late-afternoon meadow, about 320 m across;
- a jagged mountain ring and a dense forest belt;
- inner hills and groves, so no spot sees from one end to the other;
- a reed-fringed pond in its own basin under the north-west hill, with stylized water (`Shaders/StylizedWater.shader`), lily pads, bank rocks and a ring of trees open toward the meadow. It is shallow enough to wade;
- grass and flowers, scattered at runtime.

Landmarks:

| Landmark | Position (x, z) | Contents |
| --- | --- | --- |
| Crash site | (12, 4) | The stripped shipwreck, nose-first in a dirt berm at the end of a scorched trench (the ground is dug out under the hull, so it never pokes inside), with a glazed cockpit (seat, consoles, one smashed side window), with electrical fires, smoke and a red cockpit emergency light. Livery: "GRS Rusty Wrench", the Maintenance Corps band and crest, and a "Licensed Space Mechanic" plaque on the console. |
| Cavendish's camp | (-60, -64) | About 100 m south-west of the wreck with no trail between them, so the player follows the smoke. A pine-ringed clearing with the fire pit, tent, two log seats, supplies and the spawn point. |
| Merchant hollow | (66, 58) | A dip with a shared CombatZone. |
| Pond | (-60, 62) | About 50 × 34 m, under the north-west hill. |

The NavMesh is baked when the level is built and saved as `Scenes/Tutorial/TutorialNavMesh.asset`. It is baked before any creature is placed.

## Building and editing

Run **Signal Lost > Tutorial > Rebuild Tutorial Level**. It rebuilds the shipwreck, meadow assets, core prefabs and cousins, then **replaces** the scene. Do hand edits in the builders, not in the scene.

| Builder | What it makes |
| --- | --- |
| `Editor/TutorialLevelBuilder.cs` | Terrain, landmarks, scatter, lighting and the NavMesh |
| `Editor/TutorialStoryInstaller.cs` | The cast, hidden items, opening camera marks and the story object |
| `Editor/MeadowAssetBuilder.cs` | Trees, rocks, bushes, grass, camp props, and fire and smoke effects (`Prefabs/Enviornment objects/TutorialMeadow/`) |
| `Editor/CrabCousinBuilder.cs` | Cousin shells and prefabs (`Prefabs/Enemies/Cousins/`) |
| `Editor/CrashSiteWreckBuilder.cs` | The ship, its details and livery |

Runtime code:

- `Scripts/Story/TutorialStory.cs`: the stages, opening, conversations, quest tracker and scanner targets. Turn off **Play Opening** to start by the fire.
- `Scripts/Enemies/CrabCousin.cs`
- `MerchantCrab` (cousins field)
- `PickupGlint` (visible distance)
- `WakeUpSequence` (scripted eyelids and skip hint)

## Test checklist

1. Press Play in the Tutorial scene. The cockpit, Cavendish pulling you out through the side window, two drag glimpses and the campfire wake play, and Space skips them.
2. Cavendish greets you across the fire. Walk off and come back: he says to keep looking.
3. Find the wreck by its smoke (there is no trail from camp). Neither item glints until you are close. The wreck, its debris and the rocks around it are solid.
   - Find the scanner under the panel; it points at the laser.
   - Find the laser in the reactor bay.
4. Return to Cavendish for the big-crab quest. Q raises the scanner, and it points north-east.
5. Enter the hollow: the whole family attacks.
   - Run more than 28 m from its centre: they walk home and heal.
   - Beat the crab and all ten cousins; cousins vanish when beaten.
   - Pick up the flask and the scrap.
6. Follow the crab to camp and hear its introduction; the shop opens. Buy an upgrade and check it in the pockets (Tab, Gear).
7. Cavendish sends you to the pond: clear three waves of frogs (5, 7, 8) with the scanner's help, then report back for the Chief Toad quest.
8. Take the Chief Toad quest, walk to the pond: the toad rises (cutscene), his bar appears, 3-5 frogs keep coming. Beat him, take the crown from the loot, set it on a trophy stand at camp, talk to Cavendish: the end card appears.
9. Quit to the title screen and start a New Game: the trophy stands are empty again.
10. Hit the friendly crab six times: the same ten cousins return around it, and drop nothing when beaten.

## Verification

> **Re-verified 10-23 (smoke only):** after the camp move, trail removal, pond, cockpit rebuild, window pull-out, collision and livery fixes and the Eugene rename, the level was rebuilt in Unity 6000.6.2f1 and TutorialStorySmoke passed again on a disposable copy, opening included. It does not look at the pull-out's framing, the pond's appearance or collisions on foot; those still need a play-test.

[TutorialStorySmoke.cs](../tools/unity/TutorialStorySmoke.cs) passed in a batch-mode Play Mode run of the generated scene. It covers:

- the opening (all of it, unskipped);
- the greeting and the keep-looking line;
- both items found from a nearby spot through the real interaction ray;
- the glint distance;
- the scanner targets;
- the shared fighting radius;
- the ten distinct names and shells;
- surrender, waiting, despawning, the flask and enough scrap;
- the path to camp, the walk home and the introduction;
- the ambush bringing the same ten back, with no scrap;
- (10-23) the shop opening after the introduction, the first-upgrade gate, the three frog waves (5, 7, 8) and the Chief Toad hand-off.

It does not cover physical input, timing feel or a full on-foot playthrough.

## Open questions

These are recorded in [design](design.md#tutorial-overhaul-meeting-notes-10-22-26):

- The opening is first-person only. Cavendish slides and bobs because he has no rig.
- Balance of the Chief Toad in the tutorial (750 HP plus frogs) needs play-testing.
