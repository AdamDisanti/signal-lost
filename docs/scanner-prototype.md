# Scanner prototype

## Scope

The [`ScannerTest` scene](../signal-lost/Assets/Scenes/Initial%20Test%20Scenes/ScannerTest.unity) remains a standalone scanner test, but the `ScannerController`, `ScannerHud`, and `ScannerRig` are now used by the shared player in the Tutorial, Testing Sandbox, and Desert Dunes scenes. The active in-hand display has no directional arrow: it retains a proximity bar and audio feedback. Enemy signals can be tracked above or below ground, and defeated enemy signals with `Health` are excluded. The shared burrow/emergence behavior is used by one Hermit Crab instance in the separate [BurrowEmergeTest scene](../signal-lost/Assets/Scenes/Initial%20Test%20Scenes/BurrowEmergeTest.unity) and by the Crystal Chameleon, which the Crystal Disruptor upgrade reveals; see [burrow and emerge](burrow-emerge.md) and [Crystal Disruptor upgrade](#crystal-disruptor-upgrade).

The design reference calls for a directional indicator, signal strength, and audio feedback. Removing the arrow is a current prototype change, not a revision to that design reference. Exact tuning values are provisional and should be adjusted through playtesting. See the [design summary](design.md) and [work guidelines](WORK_GUIDELINES_README.md).

## Controls

| Input | Action |
| --- | --- |
| Q | Toggle scanner up/down |
| Gamepad left shoulder (LB/L1) | Toggle scanner up/down |

The `ToggleScanner` action is in the existing Player action map. The toggle script uses a private copy of the Input Actions asset, following the existing player input pattern. Input is ignored while the application is unfocused or the cursor is unlocked. The scanner deliberately starts **stowed** when Play begins: click the Game view to capture the cursor, then press **Q** to raise it (again to lower it). Its model disappearing at startup alone is not an error.

## Scene setup

Open `signal-lost/Assets/Scenes/Initial Test Scenes/ScannerTest.unity` in Unity 6000.6.2f1 and enter Play Mode. The scene contains a player prefab instance, a `ScannerRig` prefab under the camera with a `ScannerVisualRoot`, imported scanner model, world-space display, and a `ScannerTarget_AboveGround` test object.

Keep `ScannerVisualRoot` as the toggle target. The model and scanner display are children of that root, so they show and hide together. Keep the toggle/controller on the active `ScannerRig` parent, outside the hidden visual root. Do not apply the scene overrides to the shared player prefab without coordinating integration.

## Proximity-bar layout

The proximity bar uses matching rounded UI Images for `Slider/Background` and `Slider/Fill Area/Fill`. Both use the default UI material. The obsolete `Scanner_SliderTrack` and the entire `Handle Slide Area/Handle/Scanner_SliderThumb` subtree have been removed. `Slider.Handle Rect` and `Target Graphic` are unset; the slider is a read-only display with no interaction, selectable transition, or navigation. Do not add a second mesh bar/handle over the UI.

The `Arrow` display object has been removed from the shared `ScannerRig`; the in-hand display retains the read-only slider instead. In the current prefab its anchored position is `(0, 0)` and its size is `(90, 30)`. The scanner body, Canvas/CanvasScaler, toggle, detection, and audio remain. The source FBX/model assets are not the active directional UI.

Background and Fill Area have matching bounds. Fill has zero anchored-position and size-delta offsets, and local Z = 0, matching the background plane. The Slider drives its fill anchors as signal strength changes. Extra width produces overflow (and a visible cap even at zero); a fill-only depth offset produces perspective-dependent misalignment. Keep the Canvas scale nonzero on all axes and use UI sibling order for these two Images rather than separating their depths. Scanner model/overall display placement is unchanged.

The layout regression fails on the old oversized/depth-offset fill and passes at 0/25/50/100% after correction. Direct3D 11 captures at empty/half/full values from front and +/-20-degree views confirm the rounded fill stays aligned with the background. Captures are made only in a disposable temporary project; no scanner runtime code was changed.

## Components

- [`TrackableTarget`](../signal-lost/Assets/Scripts/Scanner/TrackableTarget.cs) provides the signal type, strength multiplier, active state, and detected state.
- [`EnemySignalSource`](../signal-lost/Assets/Scripts/Scanner/EnemySignalSource.cs) is the enemy-facing source component. Its `IsBuried` flag defaults to false. `SetBuried` updates both the flag and signal type; the scanner accepts buried sources marked `UnderGround`.
- [`ScannerConfig`](../signal-lost/Assets/Scripts/Scanner/ScannerConfig.cs) is a ScriptableObject for detection, audio, and upgrade tuning. The current asset is [`ScannerConfig.asset`](../signal-lost/Assets/Settings/ScannerConfig.asset).
- [`ScannerToggle`](../signal-lost/Assets/Scripts/Scanner/ScannerToggle.cs) handles the Q/LB toggle and visual root.
- [`ScannerController`](../signal-lost/Assets/Scripts/Scanner/ScannerController.cs) prioritizes quest signals over enemy signals, applies the scene's effective range to enemy acquisition and signal-strength calculation, and raises `OnTargetReached` once when entering the reach radius.
- [`ScannerHud`](../signal-lost/Assets/Scripts/Scanner/ScannerHud.cs) updates the retained strength slider on the scanner display; it no longer updates a directional arrow.
- [`ScannerAudioFeedback`](../signal-lost/Assets/Scripts/Scanner/ScannerAudioFeedback.cs) generates a short placeholder tone at runtime. Beeps become more frequent, louder, and higher-pitched as signal strength increases.

## Current tuning and behavior

The config asset currently uses a 30 m maximum range, a 0.2 s scan interval, far/close beep intervals of 1.0/0.15 s, pitch range 0.8-1.4, and volume range 0.2-0.8. Range, strength, and audio upgrade multipliers in this config are all 1.0 placeholders and are not connected to an upgrade system. Upgrades reach the scanner a different way: the shop's upgrades set properties on `ScannerController` through `PlayerUpgrades.Apply`. The Long-Nose Antenna sets `RangeBonus`, and the Crystal Disruptor (Crystal Caves, 11-12) sets `DisruptsCamouflage`; see [Crystal Disruptor upgrade](#crystal-disruptor-upgrade). A positive `levelRangeOverride` on the scene's `ScannerRig` replaces the config range before applying the range-upgrade multiplier; zero or a negative value falls back to the config's 30 m maximum.

Current shared-player scene overrides are:

| Scene | `levelRangeOverride` |
| --- | ---: |
| Tutorial | 200 m |
| Testing Sandbox | 300 m |
| Desert Dunes | 130 m |

These are authored tuning values, not guarantees of full-map coverage. Range is measured from the scanner's scan origin; level shape, player position, and distance to the signal still matter.
The standalone `ScannerTest` scene has no positive level override, so it uses the config's 30 m fallback. Scenes without a positive override behave the same way.

The controller scans active, eligible `EnemySignalSource` objects and selects the nearest one within effective range. Buried candidates must have the `UnderGround` signal type. Candidates with a parent `Health` component are skipped while dead; if the current enemy target dies, the controller immediately rescans and moves to the next eligible target. If none is available, it clears the target and signal strength. Sources without a parent `Health` component are not classified as defeated. Quest signals are checked first and remain selected at any distance, preserving their priority over enemies. The bar's signal strength still uses distance and the effective range, so a quest beyond that range can remain the selected target while showing zero strength. For an in-range target, strength uses the source/config multipliers and decreases linearly from full strength at the scan origin to zero at the effective range. The reached radius is currently 2.5 m.

Detection currently has no line-of-sight or occlusion filtering. Burrow/emergence and its combat gates are implemented only for the test Hermit Crab instance; other enemies and encounter progression have not been wired to the feature. `OnTargetReached` is an extension point and has no subscriber in this prototype.

## Checks

- Unity **6000.6.2f1** compilation and three consecutive isolated Play Mode entries passed with normal domain/scene reload. Synthetic Q/LB bindings, simultaneous movement/sprint input, repeated raising/lowering, imported model renderers, target acquisition, and disable/re-enable cleanup were checked using [ScannerPlayModeSmoke.cs](../tools/unity/ScannerPlayModeSmoke.cs).
- `dotnet build signal-lost/Assembly-CSharp.csproj --no-restore` completed successfully. This checks the generated C# project; it is not a Unity Editor compile, Play Mode test, or player build.
- Feature-author Play Mode checks reported successful: the buried test crab emerges, remains a scanner target while underground, and the scanner moves off a defeated enemy to another eligible target.
- Crystal Disruptor (11-12): a Play Mode smoke check in a disposable copy ([CrystalChameleonSmoke.cs](../tools/unity/CrystalChameleonSmoke.cs)) passed. Without the upgrade the raised scanner ignores a hidden chameleon. With it, the scanner selects it from 8 m, and at 2 m reveals it (red crystals, emerges, fights, drops scrap). Not checked by hand: the feel of the 4 m reveal distance, a reveal sound or hint (none exists), and the model swap (no upgraded model exists yet).
- Continue verifying in Unity: quest priority; meter response within and beyond effective range; beep cadence, pitch, and volume; buried target tracking; exclusion and handoff on enemy death; and the one-shot reached event. Check the Console and assigned Inspector references as part of those tests.

### Input-state errors when entering Play Mode

The reported Editor log contained `Map/Control/Binding index out of range` and `NullReferenceException` inside Input System state-change monitors, while both domain and scene reload were disabled. These are stale input-state errors, not missing scanner Inspector references. `ProjectSettings/EditorSettings.asset` now restores normal domain and scene reload, so each Play Mode entry starts with clean script/input state. Close/reopen Unity once to discard an already-corrupted session. Do not disable both reloads again without validating all gameplay/input lifecycles. No scanner runtime code, model transforms, scenes, package versions, or input bindings were changed for this fix.

A fresh temporary session also passed with the old fast-mode settings; the exact previously corrupted Editor state was not reproduced. This is a defensive Editor-configuration fix, not a demonstrated Input System package repair. The regression check uses the same project assets/settings and runs three Play Mode cycles. To repeat, copy the harness into `Assets/Editor` of a disposable project under the system temp directory and run `Unity.exe -batchmode -nographics -projectPath <temporary-project> -executeMethod ScannerPlayModeSmoke.Start -logFile <log-path>` (omit `-quit`). `-scannerFastPlayMode` compares the old settings in that temporary project only. Test-only background/focus settings allow synthetic input in batch mode; when the application is unfocused, the harness checks action bindings and invokes the existing visual toggle directly. These are not physical keyboard/controller tests; no runtime focus restrictions are removed. Run without `-nographics` and with `-force-d3d11` to additionally capture the bar at empty/half/full values from three angles under ignored `Logs/ScannerBar_*.png`.

## Crystal Disruptor upgrade

The Crystal Caves scanner upgrade (see [upgrades](upgrades.md#crystal-caves-upgrade)). Owning it sets `ScannerController.DisruptsCamouflage`, which `PlayerUpgrades.Apply` does.

- **Without it:** enemies disguised as crystals are skipped as scanner targets, even though their `EnemySignalSource` is on.
- **With it:** they're ordinary targets, so the scanner beeps toward them (quest signals still come first). While the scanner is raised, each scan also checks every disguised enemy, and any whose fake crystal is within its reveal radius of the scan origin gets `Disrupt()` called.

### Making an enemy disruptable (the cave axolotl)

1. Implement `SignalLost.Scanner.IScannerDisruptable` on the enemy:
   - `IsDisguised`: true while it's posing as a crystal. It can turn true again later; the axolotl re-burrows mid-fight.
   - `DisguisePosition`: the fake crystal the player has to walk up to.
   - `RevealRadius`: how close that is.
   - `Disrupt()`: drop the disguise and fight. It must do nothing if it's already revealed.
2. Give it an `EnemySignalSource`, on the same object or a child. The scanner finds disruptable enemies through their signal sources.
3. To rise out of the ground, use the shared [BurrowEmerge](burrow-emerge.md) with **Emerge On Proximity** unticked, and call `Emerge()` from `Disrupt()`. `CrystalChameleon` is the example to copy.

### Replacing the scanner model

Until the upgraded model exists, the scanner looks the same before and after the upgrade.

**In hand:**
1. Import the new model and drop it into `Prefabs/Player/Scanner/ScannerRig.prefab` as a child of `ScannerVisualRoot`, beside the current model, in the same position. Keeping it under the visual root means Q still shows and hides it.
2. On the rig's `ScannerController`, under **Crystal Disruptor upgrade**:
   - set **Upgraded Model** to the new object;
   - set **Standard Model** to the current model object.
3. Untick the new object, so it starts hidden. Once the upgrade is owned, the controller switches the two. If Upgraded Model is left empty, nothing switches.

**Gear tab (pockets menu):**
1. Add `["crystal_disruptor"] = YourBuildMethod` to `Models` in `Editor/UpgradeAssetBuilder.cs`. Then run **Signal Lost > Items > Build Upgrade Assets**, which also makes the shop icon. A model that wasn't made by that builder also works if it's added as an `ItemCatalog` entry with the Id `crystal_disruptor`.
2. `GearFigure.Refresh` already has a `crystal_disruptor` case that puts it at the centre of the scanner. Change its position there if needed.

Record Unity Play Mode results and any known limitations in the pull request, following the [workflow](workflow.md). If the arrow texture is from an external or third-party source, add its attribution and license to [assets.md](assets.md).
