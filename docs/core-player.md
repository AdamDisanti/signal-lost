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

- **Jump smash (C in mid-air, laser out):** raises the laser and dives straight down. The landing shockwave does 15 to 60 damage depending on how high you dived from (0.8 m to 8 m; capped above that). Its radius is 2.5 to 5.5 m and it shoves things back. It takes over the airborne C press, so you don't crouch. `MiningLaser` adds the `JumpSmash` component to the player at runtime; its tuning is there.

## Moving a scene to the shared player

Use **Signal Lost > Scenes**, or `CoreSceneSetup.UpgradeToCorePlayer(scene, startingItems, stageGroup)`. It does the following:

- swaps the old player for the prefab, in the same spot;
- removes old HUDs and stray cameras;
- adds GameSystems and a spawn point;
- optionally adds a `StageReset`, which restores a group of objects on respawn.

## Hands

The hands stay fixed at the bottom of the screen, just above the status bar.

- **Look:** pudgy gloved hands: a puffy palm, short thick two-jointed fingers and a chunky thumb, with the cream cuff and the orange suit sleeve.
- **Right hand:** holds the laser or the scanner (Q cycles), so laser swings carry it along. With nothing out it closes into a fist.
  - **Laser:** the fist is wrapped round the real pistol grip, index finger under the trigger guard. The laser is carried 6 cm higher and 16 cm further out than before (`gripPosition` on the Player prefab, set by `CorePrefabBuilder`), so the arm holding it shows above the status bar.
  - **Scanner:** the fist grips the scanner's right-hand side rail, wrist tipped so the arm comes up from below.
  - The grips are measured against the real models and kept in `MechanicModel.LaserHold` / `ScannerHold`; the fingers use `Hand.Wrap(radius)`, which bends each finger round a handle of that thickness. They assume the hand scale of 1.2.
- **Left hand: the swig.** On F it reaches down out of view for the flask, brings it up from the lower left to the mouth (the neck just below the screen, the label toward you), tips the bottom up for a big swig with two gulps, and the head tips back about 10° (`swigHeadTilt`). Then it lowers the flask out of view and comes back up empty. At rest the fingers are relaxed and half open.
  - The whole drink is posed by moving the flask (neck position and tip angle); the hand follows it.
  - **Hold-to-chug ready:** the pose comes from `PoseDrink(raise, gulps)` in `FirstPersonHands`. The timed sip runs raise 0→1, two gulps, then 1→0. A hold-to-chug would keep `raise` at 1 for as long as the button is held and keep counting `gulps` (each whole number is one swallow). PlayerFlask would need its own change to keep the drink going.
- **Footsteps:** a soft, low bass note per step (pitched per surface), replacing the old crunch. **Switched off for now** (lead's request, 10-23): set `PlayerAudio.FootstepsEnabled` to bring them back.

The hands and the pockets menu's mechanic are built by the same code (`MechanicModel.cs`), so they always match, with no art assets:

- The gloves, the suit (legs, belly and chest) and the arms are each **one seamless, smooth mesh** (`SoftShape.cs`): rounded pieces melted together like clay, so there are no seams or lumps where parts meet. The gloves are skinned, so the fingers bend smoothly at their joints.
- Small details (cuffs, sleeves, belt, eyes, nose, cap, badge, boots) are separate rounded shapes.
- **Baked meshes:** the seamless meshes take a moment to build, so they are baked into `Resources/SignalLost/MechanicMeshes/` (one asset per mesh) with **Signal Lost > Models > Bake Mechanic Meshes** and loaded from there. If you change a shape in `MechanicModel.cs`, bump `MechanicModel.MeshVersion` and bake again. Without an up-to-date bake the game still works: it builds them when the scene starts (a short hitch) and warns in the Console.
- Colours, size, poses, finger grip and curl speed are at the top of `FirstPersonHands.cs`; the shapes and the body's proportions are in `MechanicModel.cs`.

## Drunk Driving (Mudlands)

`Scripts/Mudlands/Driving/DrunkDriving.cs` adds itself at runtime to whichever object has the `PlayerFlask` (no prefab change). It does nothing until the pockets hold the **Learner's Permit** key item; after that, holding the drink button for 0.3 s chugs the flask dry (`FirstPersonHands` poses it with the flask's `PoseDrink` hook) and summons a car instead of sipping. While driving, the walking controller is switched off, the player is parented to the seat and hidden, made invulnerable (hits damage the car instead) and a chase camera takes over. `PlayerFlask` gained three small hooks for it: `Actions`, `CancelSip()` and `AddDrinks(n)`. Details in [The Mudlands](mudlands.md#3-drunk-driving).

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
- **Low health:** at 30 health or less the screen edges pulse red with a heartbeat (sound too) and "LOW HEALTH - drink [F]" appears; stronger the lower you go (`GameHud.LowHealth`).
- **Pause menu:** centred; Settings and Controls replace it in the middle (Back or Esc returns).
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
