# Tutorial gameplay

> **Superseded (10-22-26):** the tutorial is now a new scene, `Scenes/Tutorial/Tutorial.unity`; see [tutorial](tutorial.md). Testing Sandbox is a free-play sandbox on the [shared player](core-player.md). The tutorial-only player, quest flow and installers named below (`TutorialPlayer`, `TutorialQuestFlow`, `TutorialGameplayBuilder`, `TutorialCoreLoopInstaller`) and their smoke harnesses were removed. Their behaviour lives on in the shared components.

> **Partly superseded (10-20-26):** the tutorial now starts with a wake-up beside Cavendish, the scanner is a third pickup, healing halves per drunk stack with blackout after 5 stacks, and the tutorial laser has 10 s bolt and 3 s combo cooldowns. See [tutorial core loop](tutorial-core-loop.md) for the current behaviour.

Open `signal-lost/Assets/Scenes/Testing Sandbox.unity` in Unity **6000.6.2f1** and press Play. Gameplay is already installed; **do not rerun the creation tools** over the scene.

The rocket, original source models, shared player prefab and working gameplay are preserved. The current [480-meter lunar revision](tutorial-impact-site.md) replaces the extreme industrial terrain/landmarks with rolling crater fields, grey regolith and a lower blended perimeter, while retaining the core tutorial floor/routes. Gameplay components remain on the tutorial player/prop instances; the original combat test scene remains separate and unchanged.
**Current quest sequence:** talk to Cavendish with E, collect the laser, defeat the dedicated merchant crab, and collect its dropped flask. See [tutorial NPCs](tutorial-npcs.md) for the current walkthrough. The flask now stays hidden until the quest crab is defeated; the existing combat and healing mechanics below are reused.


## Controls and test loop

| Control | Action |
| --- | --- |
| WASD / left stick | Walk; Shift / left-stick press sprints; Space / south button jumps |
| E / north button | Look at a pickup within 3 meters and collect it |
| Left mouse / west button | Mining-laser melee: jab, side bash, overhead slam |
| Hold/release right mouse / right trigger | Charge/fire a laser bolt; original charge effects, recoil, sway, bob and cooldown |
| F / D-pad up | Drink Scotch after collecting the flask |
| Escape / left click | Release / recapture the mouse, following the existing controller behavior |

1. Start unarmed. Find the basin near the capybara, look at the orange/graphite laser and press **E**. Its world copy disappears and the combat-demo model is equipped. No attacks or held weapon are available before collection.
2. Talk to Cavendish with **E**, defeat the larger flask-carrying merchant crab, then pick up its dropped flask with **E**. It has unlimited uses, not unlimited simultaneous sips.
3. Walk onto the marked **red Damage Test Pad**, near the starting area, to lose **20 HP immediately and once per second** while remaining inside it. Step off to stop taking damage. It only damages the player, not props/targets.
4. Press **F** to lift/tilt the existing flask model and drink. Each sip heals **35 HP**, capped at the existing **100 HP** maximum. Healing and the drunk stack apply once halfway through the 0.9-second animation. Drinks have a 1.2-second cooldown. The laser is hidden and attacks/charging are cancelled during drinking, then restored.
5. Test the original three-hit combo and charged bolts against the three capsule practice targets near the basin. These use the existing `Health` and `TrainingDummy` classes, not new enemy AI. Tutorial targets stay defeated until the stage resets; combat-demo dummies still respawn automatically after three seconds.

The upper-left HUD shows the health bar and numeric HP; below it are drunkenness and the current tutorial objective. Pickup prompts appear near the center; the original combat HUD provides crosshair/hit feedback and combo/charge information once equipped.

Gamepad bindings are supplied but require a physical hardware check. Optional exploration now follows natural lunar hills and crater fields; the former industrial causeway/stepping-fin jump course is no longer in the scene. Eight exploration circuits and the normal pickup/target/gate loop are tested without jumping. Existing walking/sprinting/jumping, intoxication penalties and respawn rules are unchanged; no low-gravity or fall-damage system was added.

## Drunkenness, blackout and death

- Every completed sip adds **one stack**, including drinking at full HP. Stack count is visible.
- Each stack increases URP lens distortion/color separation, reduces walk/sprint speed, and adds oscillating steering deviation **only while moving**. At eight stacks movement is 63.33% of sober speed (the original 55% slowdown reduced by one third to 36.67%); inputs are never inverted and standing still does not push the player sideways. Jump height is unchanged. Visual intensity is stronger than the initial pass: at eight stacks lens distortion pulses from -0.24 to -0.56 and chromatic aberration reaches 1.0; lower stacks blend these effects proportionally. Pulse frequency and recovery/blackout rules are unchanged; the later movement tuning reduces only the speed penalty, not steering wobble or visuals.
- The first stack wears off **15 seconds after the last sip**; another wears off every 15 seconds while no new drink is taken. Sober movement and vision return to their defaults at zero stacks. The UI remains readable; distortion is applied to the camera, not the screen-overlay HUD.
- **Eight stacks are allowed. Drinking past eight (the ninth stack) causes blackout.** Input and combat stop, the screen fades to black, and after 1.5 seconds the player returns to the starting position, sober and at full health.
- Reaching zero health follows the same return/reset flow with a death message. The player retains the laser/flask once collected, and collected world copies remain removed. Gear is not duplicated.
- On death/blackout the whole **Tutorial Gameplay > Tutorial Targets** group is recreated from an inactive stage-start snapshot: all targets, including destroyed ones, return to their original transforms and full HP. Put tutorial enemy actors in this group when replacing the practice targets. Live laser bolts are cleared so they cannot immediately hit fresh targets.

These numbers are prototype tuning, exposed on the tutorial player's `TutorialPlayer` component. No overheal, save/checkpoint network, general equipment inventory, inter-stage gear persistence, quests, scanner or gate transitions are introduced. Existing crab/frog AI is now present in a separate tutorial encounter, described below. Lawson's rule to retain pre-stage equipment remains a requirement for the future inventory/stage-transition integration; the current level only has the two basic pickups.

## Authoring and reusable assets

New assets are in the existing prefab section, preserving its spelling:

`signal-lost/Assets/Prefabs/Enviornment objects/TutorialCrashSite/Gameplay/`

- `TutorialHUD.prefab`: derived from the existing UI Canvas/framework, with upper-left health and tutorial status.
- `DamageTestPad.prefab` / `DamagePadRed.mat`: visible red pad, damage instructions, trigger and kinematic Rigidbody.
- `TrainingTarget.prefab`: the existing health/dummy behavior on a capsule, with timer respawn disabled for this level.
- `TutorialDrunkProfile.asset`: URP distortion and chromatic aberration; the player uses a private runtime profile, leaving the asset unchanged.

Runtime scripts:

- [TutorialPlayer.cs](../signal-lost/Assets/Scripts/Player/TutorialPlayer.cs): pickup ownership, health/HUD events, drinking, intoxication, stage snapshot and respawn.
- [TutorialPickup.cs](../signal-lost/Assets/Scripts/Player/TutorialPickup.cs): scene-only laser/flask pickups.
- [PlayerDamageZone.cs](../signal-lost/Assets/Scripts/Player/PlayerDamageZone.cs): player-only timed damage.
- Existing `MiningLaser`, `Health`, `FirstPersonController`, `CombatDebugHud`, `TrainingDummy` and `HUDController` remain the shared implementations. Laser visibility follows component enable state; sober movement and the combat demo retain their defaults.

[The one-shot builder](../signal-lost/Assets/Editor/TutorialGameplayBuilder.cs) adds gameplay to a saved tutorial only when it is absent. It backs up the pre-gameplay scene under ignored `Logs/TutorialBeforeGameplay.unity` and refuses to overwrite an already-integrated scene. Edit the delivered scene through Unity normally, keeping all `.meta` files/GUIDs.

## Validation

Unity **6000.6.2f1** compilation and isolated Play Mode checks passed using [TutorialGameplaySmoke.cs](../tools/unity/TutorialGameplaySmoke.cs), including a full rendered rerun against the final 480-meter lunar scene:

- Unarmed start; distance/interaction-ray pickup gating; no duplicate collection; held weapon visibility and collision.
- Actual pad trigger damage and health bar events; flask animation, one-time healing, max-HP cap, cooldown and unlimited drinks.
- Stack recovery, bounded speed penalty and restored sober defaults.
- Three original melee strikes (20/20/40 damage), charged-bolt travel/70 damage and no player self-damage.
- Correct ninth-stack blackout, real lethal-pad death, retained pickups, starting-location respawn and complete stage-target restoration, including an actor destroyed during the test.
- Original combat demo still starts equipped and retains its three-second dummy respawn.
- A Direct3D 11 render pass captured the flask/health UI and verified actual intoxication image changes across more than 1% of pixels. The capture uses a standard URP render request so normal camera-stack volume/post-processing updates run. The off-screen capture temporarily camera-binds the UI for screenshots only; the delivered HUD remains screen-overlay UI.

The harness drives the public interaction/drink methods and, when batch-mode focus prevents input, invokes the original combat actions directly. **These are not physical mouse/keyboard/gamepad tests.** Its short stack-decay check temporarily accelerates the runtime recovery interval after asserting the authored value is 15 seconds; saved tuning is unchanged. A known Unity Editor-only empty SearchDatabase startup exception is excluded in the minimal temporary project; gameplay exceptions still fail the run.

To repeat, copy the matching project assets/settings/packages into a disposable project **under the system temporary directory**, then copy the harness into its `Assets/Editor` folder. Never install test copies/baselines in the real project. Run:

```text
Unity.exe -batchmode -nographics -projectPath <temporary-project> -executeMethod TutorialGameplaySmoke.Start -logFile <log-path>
```

Omit `-quit`; the harness exits with a pass/fail code. Omit `-nographics` and use `-force-d3d11` for rendered distortion checks. Logs and captures are written to that project's ignored `Logs/` directory. No dependencies or Unity/package versions were changed in the delivered project.

## Prototype-demo enemy encounter

Branch: `feature/playable-tutorial-demo`. The existing tutorial dummies are preserved, with their original transforms, prefab references, health and respawn settings.

One existing **HermitCrab** prefab and one **MortarFrog** prefab are placed beyond the basin/target area, away from the unarmed starting location:

| Actor | Approximate world position |
| --- | --- |
| Tutorial Hermit Crab | (62.50, 0.04, 22.14) |
| Tutorial Mortar Frog | (77.23, 0.72, 15.21) |

Find them under **Tutorial Gameplay > Tutorial Targets > Tutorial Enemies** in the Hierarchy; select an actor and press **F in the Scene view** to frame it. Collect the laser/flask first, then approach the encounter. Controls and enemy behaviors are unchanged; see [crab](enemy-prototype.md) and [frog](frog-enemy.md) guides. No new boss, quest, reward or progression rules are added.

**Tutorial Encounter Navigation**, next to the target group, holds a baked NavMeshSurface for a 100 x 40 x 100 m volume around the encounter. The bake uses physics colliders and excludes NavMesh agents via the surface's existing defaults. This is local encounter navigation, not a promise that enemies can traverse the entire 320 m map. After changing encounter terrain or collision, rebake that surface in Unity and include its navigation asset; the exact binary `.asset` path has an LFS rule. Do not rerun the original scene-creation tools.

Both enemies stay defeated until the player's next death/blackout. They participate in the existing stage-start snapshot/reset together with the unchanged dummies. Frog projectiles and puddles from the previous attempt are removed alongside player bolts during reset. Shared enemy prefabs and separate enemy test scenes are unchanged.

### Integration checks

Unity **6000.6.2f1** compilation, prefab placement, navigation bake, and isolated Play Mode checks passed. The checks confirmed both enemies on the NavMesh, complete routes from the pickup area, actual crab AI damage, actual frog projectile damage, enemy death and stage-reset restoration, player pickup retention, removal of old goo, and all three dummies remaining at their original positions/full health. The authored scene change contains only added data; existing dummy serialized data is unchanged.

The harness uses automatic enemy AI plus direct player relocation, pickup collection and health damage for batch-mode setup/death checks; it is not a physical-input test or a full visual playthrough. The existing editor-only SearchDatabase startup exception is excluded, as in the prior tutorial harness; gameplay exceptions fail the run. Manually verify encounter visibility, movement/combat feel, enemy special attacks, and both death and ninth-drink blackout in the editor.

Reproduction tools: [TutorialEnemiesSetup.cs](../tools/unity/TutorialEnemiesSetup.cs) and [TutorialEnemiesSmoke.cs](../tools/unity/TutorialEnemiesSmoke.cs). Copy the smoke harness and setup helper into `Assets/Editor` of a disposable project under the system temporary directory. With the already-integrated scene, run Unity with `-batchmode -nographics -projectPath <temporary-project> -executeMethod TutorialEnemiesSmoke.Start -logFile <log-path>`; omit `-quit`. `TutorialEnemiesSetup.Create` is only for adding this integration to a pre-enemy copy of the tutorial; it refuses an already-integrated scene. Test scripts remain outside the real project's Assets.
