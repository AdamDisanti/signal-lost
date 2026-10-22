# Player HUD

Sequence step 5 from the [work guidelines](WORK_GUIDELINES_README.md) (HUD part; owner Nicole). Covers the HUD list in the [design summary](design.md): health, scanner direction/strength and objectives, plus the crosshair and laser feedback that used to live in the temporary `CombatDebugHud`.

## Create the scene

Open the `signal-lost/` project, then choose **Signal Lost → Create HUD Test Scene**. It creates:

- `Assets/Prefabs/UI/PlayerHUD.prefab`: the HUD itself (a Canvas built with Unity UI, so it can be edited in the Scene/Prefab view).
- `Assets/Scenes/Initial Test Scenes/HudTest.unity`: the enemy test arena with two hermit crabs, the player with the mining laser and health, the real HUD instead of the debug HUD, a **Signal Beacon (test)** sphere on the platform, and a **HUD Test Driver**.

Like the other builders, it refuses to overwrite the scene. Commit the scene, prefab, new scripts and all their `.meta` files.

After that, edit `PlayerHUD.prefab` directly in Unity (layout, colors, sprites). **Signal Lost → Rebuild Player HUD Prefab** recreates the prefab from the script and throws those edits away.

## Layout

| Where | What |
| --- | --- |
| Top left | Health bar with value, a pale trail showing damage just taken, pulsing red under 25%, gold overheal strip (inactive until `Health` supports overheal) |
| Top center | Scanner: needle points toward the signal (straight up = ahead), five signal-strength bars, `SIGNAL` / `NO SIGNAL` |
| Top right | Objective tracker |
| Center | Crosshair that grows and turns yellow on hits; enemy name and health above it; charge bar below it while charging or cooling |
| Full screen | Red flash when hurt; `SIGNAL LOST` while respawning; `Click to resume` when the cursor is free |

## Connecting other systems

`PlayerHud` finds the player's `Health`, `MiningLaser` and camera automatically, so the prefab can be dropped into any scene with the player. Systems that don't exist yet push values in:

```csharp
using SignalLost.UI;

PlayerHud.Current?.SetObjective("Defeat creatures (1/3)");   // null or "" hides the tracker
PlayerHud.Current?.SetScannerSignal(target.position, strength01);
PlayerHud.Current?.ClearScannerSignal();
```

`HudTestDriver` is a test-scene stand-in for the scanner and quests. Remove it once those systems call `PlayerHud` themselves. `CombatDebugHud` stays in the older test scenes until the integration owner swaps it out.

## Test checklist

1. Open `HudTest.unity`, press Play. No Console errors.
2. Health reads `100 / 100`. Objective reads `Defeat creatures (0/3)`. The scanner needle points toward the orange sphere on the platform; turning your view turns the needle.
3. Walk toward the sphere: more signal bars light up. Walk away: fewer.
4. Aim at a crab: its name and health bar appear above the crosshair.
5. Hit a crab: the crosshair flashes yellow and grows. Hold right click: the charge bar fills cyan, turns orange at full charge, and shows `COOLING` after firing.
6. Let a crab hit you: red screen flash, the bar drops, and a pale trail catches up. Under 25 health the bar pulses red.
7. Kill crabs (they come back several seconds after dying): the objective counts to 3, then reads `Follow the signal`. Stand next to the sphere: `Signal found!`.
8. Die: `SIGNAL LOST / Respawning...` appears, then the HUD returns with full health.
9. Press Esc: `Click to resume` appears and the crosshair hides.
10. Change the Game view to a different aspect ratio (e.g. 16:10, 4:3): everything stays in its corner and on screen.

## Open questions

- Flask overheal and drunk level: confirm with Amanda how they should show (the overheal strip is ready; drunk level is not built).
- Scanner: confirm whether direction should be a needle (current) or a compass strip, and whether strength also drives audio.
- Scrap counter: removed for now. Add it back (a `SetScrap` call plus a counter under the objective) once the economy rules are settled with Christian.
- Text uses Unity's built-in legacy font to avoid importing TextMeshPro resources without the lead's OK; switch to TextMeshPro and a styled font during the art pass if the team agrees.
