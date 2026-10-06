# Movement prototype

## Run it

Open the existing Unity project in **6000.6.2f1**. Let scripts compile/import, open `Assets/Scenes/MovementGraybox.unity`, and press Play. Click the Game view to capture the cursor if needed.

| Input | Action |
| --- | --- |
| WASD / arrow keys | Move |
| Mouse | Look |
| Left Shift (hold) | Sprint |
| Space | Jump while grounded |
| Escape | Release cursor and stop player input |
| Left click in Game view | Capture cursor again |

The existing input asset also provides gamepad move/look, left-stick-click sprint, and south-button jump bindings. Gamepad behavior requires hardware verification. Escape/click cursor handling is intended for desktop testing. This is not a pause menu: gravity continues while input is released.

## Included assets

- `Assets/Prefabs/Player/FirstPersonPlayer.prefab`: CharacterController, movement script, child camera, and audio listener. Place its root at foot level; do not add a Rigidbody. Use only one active player/camera/listener in the test scene.
- `Assets/Scripts/Player/FirstPersonController.cs`: reads an instance of the existing Player action map and handles movement, gravity, grounded jump, yaw/pitch limits, ceiling collision, and cursor focus.
- `Assets/Scenes/MovementGraybox.unity`: enclosed floor, collision obstacle, four steps, ramp, and jump platforms; player starts at the south end facing the obstacle.
- `Assets/Materials/Graybox/`: simple URP materials, no imported asset packs.
- `Assets/Editor/MovementGrayboxBuilder.cs`: asset creation/validation helper. The menu `Signal Lost > Create Movement Graybox` is for initial generation only and refuses to overwrite existing scene/prefab assets. Edit the generated assets normally in Unity.

Defaults are provisional: walk 4 m/s, sprint 7 m/s, jump height 1.2 m, gravity 20 m/s², camera height 1.6 m, and FOV 75°. Tune on the prefab's FirstPersonController component. Mouse sensitivity is degrees per pixel; gamepad sensitivity is degrees per second. Diagonal movement is capped to the same speed as forward movement.

## Manual acceptance checks

1. Spawn above the floor and settle without falling through it. Verify there are no Console errors or missing scripts.
2. Walk forward/backward/sideways and diagonally; diagonal movement should not be faster. Sprint should increase speed.
3. Look around, including straight up/down: pitch should stop at its limits without rolling the view.
4. Walk into walls and the central obstacle; the capsule should stop. Climb the small steps and ramp without jumping.
5. Jump onto the low platform and toward the higher platform. Jump again only after landing; holding Space should not auto-repeat jumps.
6. Press Escape: mouse releases and movement input stops. Click to resume. Switching focus away from the editor should release control too.
7. Exit and re-enter Play Mode, then repeat basic movement. Test a gamepad if available.

The shared `Prototype.unity` and build list remain the integration starting point; the movement scene is a separate development scene. Combat, scanner, health, saves, and checkpoint respawn are not part of this movement task. Validation results will be recorded below.

## Validation performed

Unity 6000.6.2f1 batch-mode compilation and asset generation passed in an isolated temporary project. Automated checks confirmed one player/camera/audio listener, assigned input and camera references, no missing scripts, grounding on the floor, and collision stopping the character at the central obstacle. The generated scene and prefab were copied back with their Unity metadata. No package or editor version changes were made.

Interactive Play Mode, mouse/cursor behavior, movement feel, jump/step/ramp traversal, rendering, and gamepad hardware checks remain for the manual checklist above.
