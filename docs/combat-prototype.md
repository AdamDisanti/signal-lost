# Combat prototype: health and mining laser

Sequence step 2 from the [work guidelines](WORK_GUIDELINES_README.md): the mining laser damages a target. Enemy attacks on the player come with the first enemy task.

## Create the test scene (once)

1. Open the project in Unity **6000.6.2f1** and wait for scripts to compile. The Console should have no red errors.
2. Choose **Signal Lost > Create Combat Test Scene** from the top menu bar.
3. The Console should print `COMBAT_TEST_SCENE_CREATED_AND_VALIDATED`, and `Assets/Scenes/CombatTest.unity` opens.

The menu refuses to run again once the scene exists. Commit the generated scene, materials in `Assets/Materials/Combat/`, and their `.meta` files with this feature. The shared `FirstPersonPlayer` prefab is not changed: the laser, muzzle, and debug HUD are added to the player in this scene only. Moving the laser onto the prefab is an integration decision for the lead.

## Run it

Open `Assets/Scenes/CombatTest.unity`, press Play, and click the Game view. Movement controls are unchanged (see [movement prototype](movement-prototype.md)).

| Input | Action |
| --- | --- |
| Left mouse (hold) | Fire the mining laser |
| Gamepad west button (hold) | Fire the mining laser (needs hardware check) |

The scene has five training dummies at 5 m, 12 m, 20 m, and 32 m from the start, plus one behind an orange cover block. A temporary debug HUD shows a crosshair, the laser charge bar, and the health of the dummy under the beam.

## How the laser works (provisional)

The design document asks for a charge/cooldown laser with no magazines but does not define the rules. This prototype uses one interpretation so the team can playtest it:

- Hold to fire a continuous hitscan beam. It damages the first thing it hits within range.
- Firing drains charge. After you release, charge refills following a short delay.
- Emptying the charge **overheats** the laser. It cannot fire until the charge refills completely.

All values are placeholders, tunable on the player's **MiningLaser** component in the Inspector:

| Setting | Default | Meaning |
| --- | --- | --- |
| Damage Per Second | 30 | A 100 HP dummy dies in about 3.3 s of beam time |
| Range | 25 m | Beam length; the 32 m dummy is deliberately out of range |
| Max Charge | 100 | Charge capacity |
| Drain Per Second | 25 | 4 s of continuous fire from full |
| Recharge Per Second | 40 | 2.5 s to refill from empty after the delay |
| Recharge Delay | 0.5 s | Pause after releasing before refilling starts |
| Overheat Duration | 2 s | Lockout after emptying; charge refills to full during it |

Training dummies have 100 HP (on their **Health** component). They redden as they lose health, flash while hit, vanish at zero, and return at full health after 3 s.

## Scripts

- `Assets/Scripts/Combat/Health.cs`: shared health for the player, enemies, bosses, and dummies. `TakeDamage`, `Heal` (capped at max; flask overheal not implemented), `Restore` for respawns, plus `Changed`, `Damaged`, and `Died` events and an optional Inspector `died` event.
- `Assets/Scripts/Combat/MiningLaser.cs`: reads the existing **Attack** action, raycasts from the camera, ignores the player's own colliders, and exposes `Charge01`, `IsFiring`, `IsOverheated`, and `CurrentTarget` for a future HUD. It must sit on the player root.
- `Assets/Scripts/Combat/TrainingDummy.cs`: test target behavior.
- `Assets/Scripts/Combat/CombatDebugHud.cs`: temporary IMGUI readout; replace with the real HUD later.
- `Assets/Editor/CombatTestBuilder.cs`: one-time scene generator and validator.

## Manual acceptance checks

1. The scene opens and enters Play Mode with no Console errors or missing scripts. Movement still works.
2. A crosshair and a full `LASER 100%` bar are visible.
3. Holding left click on the 5 m dummy draws a beam from the lower right of the view to the dummy. The dummy reddens, flashes, and its health counts down under the crosshair.
4. Keep firing until the dummy vanishes. It returns at full health about 3 s later.
5. From the start position, the beam reaches the 12 m and 20 m dummies but stops short of the 32 m dummy, which takes no damage.
6. Aim at the dummy behind the orange cover block from the start position. The beam stops on the block, and the dummy takes no damage.
7. Fire into empty space: the bar drains, and after about 4 s it shows `OVERHEATED` in red. Holding fire does nothing until the bar refills (about 2 s), then firing works again.
8. Fire briefly, release, and watch the bar refill after a short pause.
9. Press Escape: the beam stops. Click the Game view to resume. That click must **not** fire the laser; firing needs a fresh press.
10. Firing at walls, the floor, or the sky never damages the player or causes errors.
11. Exit and re-enter Play Mode, then repeat a quick shot.

## Validation performed

The scripts were written and reviewed without a Unity editor. They have **not** been compiled or run in Unity. Compilation, scene generation, and every check above remain for the first person to open this branch in Unity.
