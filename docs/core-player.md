# Shared player, HUD and menus

The game-wide player and UI from the 10-22-26 meeting notes. Every playable scene uses them: Tutorial, Testing Sandbox and Desert Dunes. They supersede the old [Player HUD](hud.md) and the tutorial-only player.

## Prefabs

All prefabs are in `signal-lost/Assets/Prefabs/Core/` and are built by `Editor/CorePrefabBuilder.cs` (**Signal Lost > Core > Build Player & Game Systems Prefabs**).

| Prefab | What it is |
| --- | --- |
| `Player` | The first-person player with the tuned mining laser, scanner rig and drunk-vision volume. It carries single-purpose components (below). |
| `GameSystems` | Brings up the HUD, dialogue, overlay, pause menu and EventSystem, and applies saved settings. Put one in every playable scene. |
| `PlayerSpawnPoint` | Where the player respawns. The active one is `PlayerSpawnPoint.Current`. |

Player components:

| Component | Job |
| --- | --- |
| `PlayerGear` | Items (laser, scanner, flask), Q to swap, holstering, starting items, "new gear" cards |
| `PlayerFlask` | F to sip (1 s): heals 35 / 28 / 22 / 17 / 13 as you get drunker; one drink wears off every 15 s; the 6th sip blacks out ("SIGNAL LOST - BLACKOUT"). From the 3rd sip on, each one ends in a burp |
| `PlayerLife` | Death ("SIGNAL LOST") and blackout, fade, respawn at the spawn point, short wake-up |
| `PlayerStun` | Added when needed: a short stun (the Chief Toad's scream) with no moving, jumping, tools or flask, and a screen effect (`FirstPersonController.Stun`) |
| `PlayerInteractor` | E on whatever is under the crosshair (`IInteractable`) |
| `FirstPersonHands` | On-screen gloves |
| `WakeUpSequence` | Eyelid wake-ups (tutorial intro, respawns) |

## Movement

- WASD, Shift to sprint, Space to jump.
- **C** (tap) crouches or stands (only where there is headroom).
- **C held while sprinting** slides: a burst at 1.5× sprint speed that bleeds off; let go of C to stand up.
- **Slide cancel:** jump during a slide to keep its speed in the air. Landing with C still held slides again, so slide-jump chains outrun sprinting.

Tuning is on `FirstPersonController` ("Crouch and slide"). Upgrades can lengthen slides (`SlideDistanceScale`).

## Moving a scene to the shared player

Use **Signal Lost > Scenes**, or `CoreSceneSetup.UpgradeToCorePlayer(scene, startingItems, stageGroup)`. It does the following:

- swaps the old player for the prefab, in the same spot;
- removes old HUDs and stray cameras;
- adds GameSystems and a spawn point;
- optionally adds a `StageReset`, which restores a group of objects on respawn.

## Hands

The hands stay fixed at the bottom of the screen, just above the status bar.

- **Right hand:** holds the laser or the scanner (Q cycles), so laser swings carry it along. With nothing out it rests as a fist.
- **Left hand:** on F it reaches down out of view for the flask, snaps it up to the mouth, swigs, and stows it again.

Everything is built from boxes at runtime. Colours, size and poses are at the top of `FirstPersonHands.cs`.

## HUD and menus

All UI is drawn in code on singleton canvases (`Scripts/UI/`), so there are no UI assets to import.

| Canvas | Sort order |
| --- | --- |
| Main menu | 10 |
| GameHud | 30 |
| GameplayOverlay (quest tracker, item cards) | 40 |
| Wake-up eyelids | 45 |
| DialogueUi | 50 |
| Pockets / Bucket & Barter (see [upgrades](upgrades.md)) | 60 |
| Pause | 70 |
| Screen fade | 80 |

- **GameHud:** a Signal Lost status bar along the bottom (Doom style) shows:
  - health;
  - flask and drunkenness;
  - the mechanic's reacting face;
  - what is in hand, with bolt and combo readiness;
  - sorted scrap.

  Around the crosshair: charge meter, hit marker and the target's name and health. Everything else keeps clear of the bar (`GameHud.BottomReserved`).
- **Pause menu (Esc):**
  - Continue, Save Game, Controls, Settings, Exit to Main Menu;
  - time and audio stop while it is open;
  - Save is a placeholder.
- **Settings:**
  - pages: Controls, Sound, Video;
  - stored in PlayerPrefs (`GameSettings`);
  - partly placeholder.
- **Title screen** (`Scenes/MainMenu.unity`, `MainMenuScreen`):
  - a Terraria-style title over a code-drawn golden-hour scene;
  - New Game loads the Tutorial (`SceneNames.FirstLevel`);
  - Continue is a placeholder; Settings and Exit work.
- **Item cards** ("NEW GEAR"): stay up until the player presses **E** or Enter; several queue up. **Rule for all future tutorial pop-ups: never on a timer.**
- **Pips:** the three combo pips drain right to left during the 2 s combo recharge; the top flask pip drains as the current drink wears off, a timer for the next sip.
- **Menus:** `UiMenus` tracks open menus, so full-screen ones hide the HUD and Esc closes the top one first.

## Verification

[CoreSystemsSmoke.cs](../tools/unity/CoreSystemsSmoke.cs) passes. It covers:

- the sandbox, desert and title screen;
- one camera, no old HUDs, starting gear and hands;
- drinking, pause, settings, the inventory and the merchant shop;
- death and respawn;
- the sandbox merchant's ten cousins waiting despawned.

Add `-coreRender` to save captures. These checks do not prove the feel of physical input.
