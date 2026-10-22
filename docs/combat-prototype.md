# Combat prototype: health and mining laser

Sequence step 2 from the [work guidelines](WORK_GUIDELINES_README.md): the mining laser damages a target. Enemy attacks on the player come with the first enemy task.

## Create the test scene (once)

1. Open the project in Unity **6000.6.2f1** and wait for scripts to compile. The Console should have no red errors.
2. Choose **Signal Lost > Create Combat Test Scene** from the top menu bar.
3. The Console should print `COMBAT_TEST_SCENE_CREATED_AND_VALIDATED`, and `Assets/Scenes/Initial Test Scenes/CombatTest.unity` opens.

The menu refuses to run again once the scene exists. Commit the generated scene, materials in `Assets/Materials/Combat/`, and their `.meta` files with this feature. The shared `FirstPersonPlayer` prefab is not changed: the laser, muzzle, and debug HUD are added to the player in this scene only. Moving the laser onto the prefab is an integration decision for the lead.

The scene was generated before the melee redesign. It does not need regenerating: the `MiningLaser` component keeps the same script and references.

## Held model

The laser in your hand is Lawson's mining-laser model from the `tutorial-zone` branch (`Prefabs/Enviornment objects/TutorialCrashSite/Upgrades/MiningLaser.prefab`; attribution is in his `docs/assets.md` row). The prefab, its FBX, its six `Laser*` materials, and their folder `.meta` files were copied unchanged onto this branch so the laser can be tested before both branches merge. They are identical to Lawson's files, so merging both branches does not conflict.

`CombatTest.unity` assigns the prefab to **Held Model** on the player's **MiningLaser** component. At runtime the laser makes a copy in the player's hand, removes its colliders and shadows, holds it by its `GripAnchor`, and starts the charge electricity and bolts at its `MuzzleAnchor`. If **Held Model** is empty or the prefab is missing, a placeholder box is used instead.

## Run it

Open `Assets/Scenes/Initial Test Scenes/CombatTest.unity`, press Play, and click the Game view. Movement controls are unchanged (see [movement prototype](movement-prototype.md)).

| Input | Action |
| --- | --- |
| Left mouse (tap) | Melee strike; keep tapping for the three-hit combo |
| Right mouse (hold, then release) | Charge, then fire a bolt; longer holds make stronger bolts |
| Gamepad west button | Melee strike (needs hardware check) |
| Gamepad right trigger (hold, then release) | Charge and fire (needs hardware check) |

The scene has five training dummies at 5 m, 12 m, 20 m, and 32 m from the start, plus one behind an orange cover block. A temporary debug HUD shows a crosshair that flashes yellow on hits, the combo count, a charge bar while charging, a controls hint, and the health of the dummy under the crosshair.

## How the laser works (provisional)

The design document asks for a charge/cooldown laser with no magazines but does not define the rules. This prototype uses one interpretation so the team can playtest it: the laser is a melee tool that can also charge up and shoot.

**Melee combo**

- Each left click strikes. Clicking again during a strike, or shortly after one ends, continues the combo.
- The combo is a **jab** (a quick thrust with the muzzle), a **side bash** (a right-to-left hook with the body of the laser), then an **overhead slam** that hits harder. The combo then starts over.
- Each strike winds up, accelerates into the hit, and holds for a moment before recovering. On contact it freezes briefly (hit-stop), the camera kicks, and an orange flash marks the impact point.
- A strike hits every damageable target within reach inside an arc centred on the crosshair, but not through walls.
- You cannot swing while charging.

**Charged shot**

- Hold right click to charge. Electricity crackles at the laser's muzzle: a small flickering core plus jagged arcs that crawl back along the barrel and spark outward. More arcs appear, and they reach further, as the charge builds, and the HUD bar fills. Reaching full charge is a distinct moment: a shockwave ring blasts out from the muzzle, the core flashes, arcs burst outward, the laser jolts forward and the view kicks. While it stays full, a crackling ring hovers around the muzzle, the arcs turn white-hot and run the whole barrel, the core pulses, and the laser shakes slightly.
- Release to fire a bolt toward the crosshair. Damage, speed, and size scale with how long you held, from a quick tap (weak) to full charge (strong).
- The bolt flies straight, damages the first target it touches, and stops on walls and cover.
- A short cooldown follows each shot. Keeping the button held starts the next charge once the cooldown ends.
- Charging cannot start mid-swing; it starts once the swing finishes if the button is still held.

All values are placeholders, tunable on the player's **MiningLaser** component in the Inspector:

| Setting | Default | Meaning |
| --- | --- | --- |
| Combo Damage | 10, 10, 20 | Damage of each swing; the number of entries sets the combo length |
| Combo Duration | 0.4, 0.4, 0.65 s | Length of each swing including recovery |
| Melee Range | 2.1 m | Reach of a swing |
| Combo Arc | 30°, 70°, 45° | Width of each strike (jab, bash, slam), centred on the crosshair |
| Combo Window | 0.4 s | Time after a swing to continue the combo |
| Hit Stop | 0.06 s | How long a strike freezes when it connects |
| View Kick | 1 | Strength of the camera kick on strikes and shots; 0 turns it off |
| Sway Strength | 1 | How much the laser lags behind when you look around; 0 turns it off |
| Bob Strength | 1 | Walking/sprinting bob, landing dip and idle breathing; 0 turns it off |
| Full Charge Time | 1.2 s | Hold time for a full-strength shot |
| Min / Max Shot Damage | 5 / 50 | Tap shot to full-charge shot |
| Min / Max Shot Speed | 25 / 45 m/s | Tap shot to full-charge shot |
| Min / Max Shot Radius | 0.06 / 0.2 m | Bolt size, which also widens what it can clip |
| Shot Range | 30 m | Bolt travel distance; the 32 m dummy is deliberately out of range |
| Shot Cooldown | 0.5 s | Pause after firing before the next charge |
| Energy Color | cyan | Colour of the charge electricity, bolts, and bolt impact flashes |
| Held Model | MiningLaser prefab | Model shown in the hand; empty uses a placeholder box |
| Held Model Scale | 0.55 | Size of the held model; the prefab is 0.82 m long at scale 1 |
| Grip Position | (-0.02, -0.08, -0.22) | Where the grip sits relative to the Laser Muzzle point |
| Held Model Rotation | (0, -3, 0) | Angle of the held model in degrees |

With these values (Christian's tuning from playtesting) a 100 HP dummy takes two full combos plus a jab (10 + 10 + 20, twice, + 10), or two full-charge bolts.

Training dummies have 100 HP (on their **Health** component). They redden as they lose health, flash when hit, vanish at zero, and return at full health after 3 s.

## Held laser motion

The laser in your hand moves with you: it lags slightly behind when you turn and catches up (sway), bobs in step while you walk and more when you sprint, dips when you land from a jump, and drifts gently when you stand still. Bob is reduced while charging so aiming stays steady, and all motion is reduced during strikes so the jab, bash and slam stay crisp.

## Scripts

- `Assets/Scripts/Combat/Health.cs`: shared health for the player, enemies, bosses, and dummies. `TakeDamage`, `Heal` (capped at max; flask overheal not implemented), `Restore` for respawns, plus `Changed`, `Damaged`, and `Died` events and an optional Inspector `died` event.
- `Assets/Scripts/Combat/MiningLaser.cs`: reads the **Attack** and **ChargeShot** actions, runs the combo and charge, places the held model (or a placeholder) and the muzzle point, animates the jab, bash and slam, applies the camera kick only while the camera renders (so aim is never affected), and exposes `IsSwinging`, `ComboStep`, `IsCharging`, `Charge01`, `IsCoolingDown`, `LastHitTime`, and `CurrentTarget` for a future HUD. It must sit on the player root.
- `Assets/Scripts/Combat/LaserBolt.cs`: the charged-shot projectile. It sphere-casts along its path each frame, so fast bolts cannot pass through thin walls. Anything implementing `IBoltReflector` (`Assets/Scripts/Combat/IBoltReflector.cs`), such as a hiding hermit crab, can bounce it back; a bounced bolt can hit the player.
- `Assets/Scripts/Combat/ChargeEffect.cs`: the charging electricity (flickering core and redrawn LineRenderer arcs), built at runtime on the muzzle point. Placeholder until real particles are made.
- `Assets/Scripts/Combat/ImpactFlash.cs`: placeholder burst where strikes and bolts land.

The strike poses (wind-up and impact positions and angles, and when the hit lands) are defined at the top of `MiningLaser.cs` as `Jab`, `Bash` and `Slam`. Edit them there to change the animation; they are not in the Inspector.
- `Assets/Scripts/Combat/TrainingDummy.cs`: test target behavior.
- `Assets/Scripts/Combat/CombatDebugHud.cs`: temporary IMGUI readout; replace with the real HUD later.
- `Assets/Editor/CombatTestBuilder.cs`: one-time scene generator and validator.
- `Assets/InputSystem_Actions.inputactions`: adds the **ChargeShot** action to the Player map (right mouse, gamepad right trigger).

## Manual acceptance checks

1. The scene opens and enters Play Mode with no Console errors or missing scripts. Movement still works.
2. A crosshair and the controls hint are visible. Lawson's orange and graphite mining laser sits at the lower right of the view, pointing forward, not clipping into the camera. The Console shows no `GripAnchor` or `MuzzleAnchor` warnings. If you see a dark grey box instead, the model files are missing (see Held model).
3. Tap left click near the 5 m dummy: the laser pulls back and jabs forward. An orange flash appears on the dummy, the laser freezes for an instant, the view kicks slightly, the crosshair flashes yellow, and the dummy's health drops by 10.
4. Tap three times quickly: jab, side bash from right to left, then the laser rises and slams down with a bigger view kick. The HUD shows `SWING 1 / 3` to `3 / 3`, and the slam deals 20.
5. Wait a moment between taps: the combo restarts at swing 1.
6. Strike at a dummy more than about 2.5 m away: it takes no damage. Jab with the dummy just off the edge of the screen's centre third: the jab misses, but the wider side bash still hits it.
7. Hold right click: cyan electricity crackles at the muzzle, starting with a single arc and building to several that crawl along the barrel. It stays close to the gun and never covers it. After about 1.2 s, as the bar reaches `FULL CHARGE`, a ring blasts out from the muzzle, the laser jolts and the view kicks. Then a white-hot crackling ring stays around the muzzle until you release. Full charge should be obvious without looking at the bar.
8. Release at full charge on the 12 m dummy: a large bolt flies to it and deals 50.
9. Tap right click quickly: a small, slower bolt deals about 5. Firing again immediately shows `COOLING` for half a second first.
10. Bolts reach the 20 m dummy, fade out before the 32 m dummy, and stop on the orange cover block.
11. Hold right click, then press Escape: the charge cancels without firing. Click the Game view to resume. That click must **not** swing; swinging needs a fresh press.
12. Swinging or firing at walls, the floor, or the sky never damages the player or causes errors. Bolts leave a cyan flash where they hit. Strikes that miss still kick the view, but more softly, and do not freeze.
13. Walk the laser into a wall and the cover block: it never blocks or pushes the player.
14. Turn the view quickly left and right: the laser lags a little behind, then settles. It never swings off screen.
15. Walk, then sprint: the laser bobs in step, more when sprinting. Stand still: it stops bobbing and drifts very slightly.
16. Jump and land: the laser dips on landing and springs back. Walking down the gentle edge of an object does not cause dips.
17. Hold right click while walking: the bob is noticeably calmer.
18. Exit and re-enter Play Mode, then repeat a quick swing and shot.

## Validation performed

The scripts were written without a Unity editor. They compile cleanly against hand-written stand-ins for the Unity APIs they use, which catches typos and type errors but is not a real Unity compile. They have **not** been compiled or run in Unity. Compilation and every check above remain for the first person to open this branch in Unity.
