# Tutorial core loop

> **Superseded (10-22-26):** the tutorial is now a new scene, `Scenes/Tutorial/Tutorial Demo.unity`; see [tutorial](tutorial.md). Testing Sandbox is a free-play sandbox on the [shared player](core-player.md). The tutorial-only player, quest flow and installers named below (`TutorialPlayer`, `TutorialQuestFlow`, `TutorialGameplayBuilder`, `TutorialCoreLoopInstaller`) and their smoke harnesses were removed. Their behaviour lives on in the shared components.

The playable tutorial built from the "Remaining core features to implement" section of the 10-20-26 meeting notes (Adam and Lawson). Open `signal-lost/Assets/Scenes/Testing Sandbox.unity` in Unity **6000.6.2f1** and press Play. Everything is already installed in the scene; you do not need to run a tool.

This supersedes parts of [tutorial gameplay](tutorial-gameplay.md), [tutorial NPCs](tutorial-npcs.md) and [the impact site](tutorial-impact-site.md): the quest order, healing numbers, NPC windows and the crashed rocket changed. Those pages note what is out of date.

## Play it

| Control | Action |
| --- | --- |
| WASD, mouse, Shift, Space | Move, look, sprint, jump (unchanged) |
| E | Talk / pick up. In a popup: finish the line, then next. Click also works |
| Escape | Close a popup or menu without finishing it |
| Q | Raise the scanner (puts the laser away); Q again swaps back |
| Left click | Laser combo: jab, bash, slam |
| Hold / release right click | Charge and fire a bolt |
| F | Take a sip from the flask |
| Tab | Inventory ("Pockets") |
| 1 / 2 / 3 | Shop tabs while the merchant menu is open |

The walkthrough:

1. **Wake up.** You blink awake on your back beside Cavendish, through a hangover blur, mumbling. He greets you in speech bubbles and subtitles.
2. **Crash Landing.** Press E on Cavendish. His popup gives the first quest.
3. **Gear Up.** Pick up the mining laser from the scorched rock basin beside him, and the scanner, which glows teal beside the wreck's empty engine bay. Each item shows a card explaining what it does. Once you have the scanner, it selects the quest target you still need.
4. **Return to Cavendish** for the second quest; he becomes the selected quest signal.
5. **Hair of the Crab.** Raise the scanner and use the quest text and level landmarks to find the big crab with the copper bucket. The scanner keeps the crab selected but has no directional arrow. Beat it. It surrenders and drops your flask and a first haul of scrap. Walk over the scrap to suck it in, then pick up the flask with E.
6. **Follow the crab** back to the crash site. It waits and waves you on if you fall behind.
7. **Talk to the merchant crab.** Its popup explains scrap and the shop, then opens the Bucket & Barter menu.
8. **Report back to Cavendish.** The tracker reads Tutorial Complete. From here the scanner hunts critters, and they drop scrap.

The top-right tracker always shows the current objective. The bottom-left hint shows the flask's next heal and the Q swap.

## What each feature does

### Dialogue

- **Subtitles** appear at the bottom middle for quick chatter: "Speaker: line", in the speaker's colour. Warnings use a red version that shakes.
- **Speech bubbles** pop up over the speaker's head, bob, and shrink with distance. They are hidden beyond 14 m or behind the camera. Warnings use a red bubble.
- **Quest popups** are transparent, rounded and slightly tilted, with a name tag and typewriter text. Click or E finishes the line, then moves to the next. Escape or walking more than 6 m away cancels without accepting anything. The cursor is freed and mouse look pauses, but walking keeps working, as the lead decided earlier.
- Quest information always uses a popup that the player opens with E. Idle chatter, combat taunts and warnings use bubbles and subtitles.

### Items

- The player owns three items: the mining laser, the scanner and the flask. Ownership survives death and blackout for the Play session; there is no save yet.
- **Q** raises the scanner and holsters the laser; Q again swaps back. Drinking, death and blackout lower the scanner.
- **Mining laser:** a bolt starts a **10 s cooldown**. The third combo hit **stuns small enemies for 2 s** (desert fodder only; crabs, frogs and mini-bosses are not stunned), then melee needs **3 s** to recharge. The debug HUD shows both countdowns.
- **Flask:** a sip heals 35 HP while sober, and **half as much for every drunk stack** (35, 17.5, 8.75...). It heals fully again once sober (one stack wears off every 15 s). **Blackout on the sip after 5 stacks.** After a death or blackout you come round with a short wake-up.

### Scanner guidance

- Quest signals stay selected at any distance and outrank creature signals: Cavendish when he has a quest waiting, the missing laser, the flask crab, the dropped flask, and the crab again on the walk home. The active in-hand display has no directional arrow; use quest text and level landmarks to navigate.
- The proximity bar's strength uses the effective range set for the current scene. The Tutorial override is 200 m; a quest can remain selected beyond that distance while its bar shows zero strength. See the [scanner reference](scanner-prototype.md) for all current scene values and fallback behavior.
- Once the tutorial ends there are no quest objectives, so it goes back to the nearest eligible creature within the current scene's effective range.

### Scrap, inventory and shop

- **Scrap types:** Scrap Metal, Wiring, Circuit Boards and Fuel Cells.
- **Drops:** hermit crabs drop 2-3 metal, 0-1 wiring and 0-1 boards. Frogs drop 1-2 metal and 1-2 fuel cells. The merchant's defeat drops 4 metal, 2 wiring, 2 boards and 1 fuel cell.
- **Collection:** chunks pop out, land and bob. Within 3.5 m they fly into your pockets and are sorted automatically. A counter and a "+3 Scrap Metal" toast appear under the health bar.
- **Inventory (Tab):** sorted scrap, the three items with live stats, and upgrades ("none yet").
- **Bucket & Barter:** tabs for Upgrades, Supplies and Trade Scrap. Prices are in scrap and every purchase is disabled ("Coming soon"). No scrap is spent.

### Angry merchant

- Hitting the friendly merchant does no damage, but it notices.
- Hits 1-5 bring escalating warnings in a red bubble and subtitle, which never block the player. One hit is forgotten for every 10 s without another.
- The **6th hit** calls **5-10 hermit crabs** ("Bucket Brothers").
- When they are all beaten, or the player dies, the merchant surrenders again. The hit count resets and the shop stays open.

### The crash site

- A new **stripped shipwreck** replaces the old crashed rocket. The rocket instance is switched off; its assets and GUIDs are untouched.
- **Missing parts:** the ship lies nose-down on its torn-open side, facing Cavendish. Each part the bosses took has an empty socket with hanging cables and red fault lamps:
  - engine core: an empty mounting ring in the open tail;
  - reactor core: an empty clamped cradle in the open mid bay, which you can walk into;
  - flight-control module: under a dorsal hatch flung open;
  - navigation computer: an empty console slot under a glassless canopy.
- **Clutter:** about 180 pieces of junk plus a scorched skid trench trail behind it. The area around Cavendish, the spawn, the basin, the gate and the merchant's walk home stays clear.

## Where things live

| What | Where |
| --- | --- |
| Dialogue, tracker, item cards, scrap counter, inventory, shop | `Assets/Scripts/UI/` (`DialogueUi`, `SpeechBubble`, `GameplayOverlay`, `InventoryUi`, `MerchantMenuUi`, `UiTheme`, `UiMenus`) |
| Quest steps and lines | `Assets/Scripts/NPC/TutorialQuestFlow.cs` |
| Merchant leading, ambush, warnings | `Assets/Scripts/NPC/MerchantCrab.cs` |
| Items, healing, Q swap, item cards | `Assets/Scripts/Player/TutorialPlayer.cs`, `TutorialPickup.cs`, `PickupGlint.cs` |
| Wake-up | `Assets/Scripts/Player/WakeUpSequence.cs` |
| Inventory and scrap | `Assets/Scripts/Inventory/` (`PlayerInventory`, `ScrapType`, `ScrapPickup`, `ScrapDropper`) |
| Scanner objectives | `Assets/Scripts/Scanner/QuestSignalSource.cs` and a small change to `ScannerController` |
| Shipwreck and debris | `Assets/Prefabs/Enviornment objects/TutorialCrashSite/Shipwreck/`, generated by `Assets/Editor/CrashSiteWreckBuilder.cs` |
| Scene installer | `Assets/Editor/TutorialCoreLoopInstaller.cs` |

The UI is built in code, so there are no UI prefabs to edit. Restyle it in one place: colours, panel shapes and fonts are in `UiTheme`. The rounded panel sprite is generated at runtime, and the font is the existing TextMesh Pro default.

**Signal Lost > Tutorial > Install Core Loop** re-applies the scene changes; it skips anything already installed. **Signal Lost > Tutorial > Build Shipwreck Assets** regenerates the wreck and debris in place (same GUIDs), throwing away hand edits to those generated assets. Moving the wreck or debris in the scene is fine.

Changes to other people's code are small and additive:
- `Health.Blocked`: hits on invulnerable targets.
- `HermitCrab.Provoke()`.
- `DesertFodder` stun, with an opt-out for `DesertMiniBoss`.
- `MiningLaser`: combo cooldown, finisher stun, read-only stats.
- `ScannerController`: quest priority and a `Range` property.
- The crab and frog builders keep the `ScrapDropper`.

## Tuning (all placeholders)

| Setting | Where | Value |
| --- | --- | --- |
| Bolt cooldown / combo cooldown / finisher stun | Tutorial player's **MiningLaser** | 10 s / 3 s / 2 s. Code defaults leave other scenes unchanged (0.5 s, off) |
| Heal per sip / blackout after | **TutorialPlayer** | 35 HP halved per stack / 5 stacks |
| Lead-wait distance | **MerchantCrab** | 12 m |
| Hits before ambush / cool-off / ambush size | **MerchantCrab** | 6 / 10 s / 5-10 crabs |
| Scrap drop tables | **ScrapDropper** on the crab, frog and merchant prefabs | See above |
| Wake-up length / mumbled lines | **WakeUpSequence** | 7 s (2.2 s after respawns) |

## Known limitations and open questions

- **Tutorial enemies are below the terrain.** The tutorial's regular crabs and frogs sit below the lunar terrain (the "enemies go under the new terrain" item) and only match the old encounter NavMesh. Because of that, the NavMesh was **not** rebaked around the wreck. The wreck is west of every NPC and enemy route, so nothing walks through it today. Fix the enemies first, then rebake **Tutorial Encounter Navigation**.
- **No persistence.** Scrap, upgrades and quest progress last only for the Play session. Nothing can be bought.
- **Other scenes keep the old laser tuning** (0.5 s bolt cooldown, no combo cooldown) until their owners opt in.
- The Desert Dunes scene still uses `PlaceholderRespawn`, with no flask, scrap or quest beacons. These systems are tutorial-wired for now.
- **Farming:** enemies reset on death, so scrap can be farmed. The ambush crabs also drop scrap.
- **Untested by hand:** voice lines are text only. Physical mouse, keyboard and gamepad feel, readability on real screens, and performance are untested (see validation).

## Validation

- Unity **6000.6.2f1** compiled the project in batch mode with no errors.
- [TutorialCoreLoopSmoke.cs](../tools/unity/TutorialCoreLoopSmoke.cs) passed in Play Mode in a disposable copy of the project. It plays the whole tutorial in order:
  - wake-up and greeting;
  - popups (typing, skip, cancel, accept);
  - bubbles, subtitles and red warnings;
  - both item pickups through the real interaction ray, with their cards;
  - the Q swap, the bolt cooldown, and the combo stun on a Hubcap Armadillo;
  - the scanner target at every step, including beyond 30 m;
  - the crab fight, scrap drop and collection, and the crab leading the player home;
  - the rundown, shop tabs, inventory and report;
  - the angry merchant (warnings, cool-off, ambush, surrender, death clean-up);
  - the halving heals and blackout on the 6th sip.
- To repeat: copy the harness into `Assets/Editor` of a disposable project under the system temp directory, then run Unity with `-batchmode -projectPath <temp-project> -executeMethod TutorialCoreLoopSmoke.Start -logFile <log>`, without `-quit`. Add `-coreLoopRender` to save captures.
- **These are not physical-input tests.** The harness calls the public methods. For screenshots it binds overlay canvases to the camera, so captures can show post-processing on UI and the held laser in front of a popup, which doesn't happen in real play.
- Renders: [UI previews](../art/tutorial-core-loop/Previews/) and [shipwreck previews](../art/tutorial-shipwreck/Previews/).
- **Still to check by hand in the editor:** the full run with keyboard and mouse, the gamepad, the feel of the wake-up and dialogue pacing, the ambush difficulty, and readability at other resolutions.
