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
| `PlayerFlask` | F to sip: heals 35, halved per drunk stack; blackout after 5 stacks |
| `PlayerLife` | Death ("SIGNAL LOST") and blackout, fade, respawn at the spawn point, short wake-up |
| `PlayerInteractor` | E on whatever is under the crosshair (`IInteractable`) |
| `FirstPersonHands` | On-screen gloves |
| `WakeUpSequence` | Eyelid wake-ups (tutorial intro, respawns) |

## Moving a scene to the shared player

Use **Signal Lost > Scenes**, or `CoreSceneSetup.UpgradeToCorePlayer(scene, startingItems, stageGroup)`. It does the following:

- swaps the old player for the prefab, in the same spot;
- removes old HUDs and stray cameras;
- adds GameSystems and a spawn point;
- optionally adds a `StageReset`, which restores a group of objects on respawn.

## Hands

The hands stay fixed at the bottom of the screen, just above the status bar.

- **Right hand:** holds the laser or the scanner (Q cycles), so laser swings carry it along. With nothing out it rests as a fist.
- **Left hand:** on F it grabs the flask, takes a quick sip and puts the flask away.

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
| Inventory / merchant menu | 60 |
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
- **Menus:** `UiMenus` tracks open menus, so full-screen ones hide the HUD and Esc closes the top one first.

## Verification

[CoreSystemsSmoke.cs](../tools/unity/CoreSystemsSmoke.cs) passes. It covers:

- the sandbox, desert and title screen;
- one camera, no old HUDs, starting gear and hands;
- drinking, pause, settings, the inventory and the merchant shop;
- death and respawn;
- the sandbox merchant's ten cousins waiting despawned.

Add `-coreRender` to save captures. These checks do not prove the feel of physical input.
