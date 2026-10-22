# Scanner prototype

## Scope

This is a first-pass scanner prototype in the separate [`ScannerTest` scene](../signal-lost/Assets/Scenes/Initial%20Test%20Scenes/ScannerTest.unity). It is not integrated into the shared player prefab or the main `Prototype` scene. The scanner currently detects above-ground enemy signal sources; subterranean discovery and emergence behavior are future work.

The design reference calls for a directional indicator, signal strength, and audio feedback. Exact tuning values here are provisional and should be adjusted through playtesting. See the [design summary](design.md) and [work guidelines](WORK_GUIDELINES_README.md).

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

The compass keeps its `Arrow` RectTransform and working `Scanner_FancyArrow` mesh. Its unused disabled Image/CanvasRenderer and the display's unused GraphicRaycaster have been removed. The scanner body, Canvas/CanvasScaler, HUD bindings, toggle, detection and audio remain intact. Original FBX/source files are retained, including the shared mesh file used by the working compass. `ScannerTest.unity` no longer stores an override for the deleted handle.

Background and Fill Area have matching bounds. Fill has zero anchored-position and size-delta offsets, and local Z = 0, matching the background plane. The Slider drives its fill anchors as signal strength changes. Extra width produces overflow (and a visible cap even at zero); a fill-only depth offset produces perspective-dependent misalignment. Keep the Canvas scale nonzero on all axes and use UI sibling order for these two Images rather than separating their depths. Scanner model/overall display placement is unchanged.

The layout regression fails on the old oversized/depth-offset fill and passes at 0/25/50/100% after correction. Direct3D 11 captures at empty/half/full values from front and +/-20-degree views confirm the rounded fill stays aligned with the background. Captures are made only in a disposable temporary project; no scanner runtime code was changed.

## Components

- [`TrackableTarget`](../signal-lost/Assets/Scripts/Scanner/TrackableTarget.cs) provides the signal type, strength multiplier, active state, and detected state.
- [`EnemySignalSource`](../signal-lost/Assets/Scripts/Scanner/EnemySignalSource.cs) is the enemy-facing source component. Its `IsBuried` flag defaults to false. The signal type currently includes above-ground and under-ground values, but only above-ground sources are scanned.
- [`ScannerConfig`](../signal-lost/Assets/Scripts/Scanner/ScannerConfig.cs) is a ScriptableObject for detection, audio, and upgrade tuning. The current asset is [`ScannerConfig.asset`](../signal-lost/Assets/Settings/ScannerConfig.asset).
- [`ScannerToggle`](../signal-lost/Assets/Scripts/Scanner/ScannerToggle.cs) handles the Q/LB toggle and visual root.
- [`ScannerController`](../signal-lost/Assets/Scripts/Scanner/ScannerController.cs) selects the nearest eligible target, updates direction and normalized signal strength, and raises `OnTargetReached` once when entering the reach radius. The event can later be used by encounter or emergence systems.
- [`ScannerHud`](../signal-lost/Assets/Scripts/Scanner/ScannerHud.cs) updates the compass arrow and strength slider on the scanner display.
- [`ScannerAudioFeedback`](../signal-lost/Assets/Scripts/Scanner/ScannerAudioFeedback.cs) generates a short placeholder tone at runtime. Beeps become more frequent, louder, and higher-pitched as signal strength increases.

## Current tuning and behavior

The config asset currently uses a 30 m maximum range, a 0.2 s scan interval, far/close beep intervals of 1.0/0.15 s, pitch range 0.8-1.4, and volume range 0.2-0.8. Range, strength, and audio upgrade multipliers are all 1.0 placeholders; they are not connected to an upgrade system.

The controller scans active `EnemySignalSource` objects and selects the nearest one within range when it is above ground and not buried. Signal strength uses distance and the source/config multipliers; with the current default multipliers it decreases linearly from full strength at the scan origin to zero at maximum range. The reached radius is currently 2.5 m.

Detection currently has no line-of-sight or occlusion filtering. Subterranean signals, emergence events, combat engagement, and encounter progression are not implemented. `OnTargetReached` is an extension point and has no subscriber in this prototype.

## Checks

- Unity **6000.6.2f1** compilation and three consecutive isolated Play Mode entries passed with normal domain/scene reload. Synthetic Q/LB bindings, simultaneous movement/sprint input, repeated raising/lowering, imported model renderers, target acquisition, and disable/re-enable cleanup were checked using [ScannerPlayModeSmoke.cs](../tools/unity/ScannerPlayModeSmoke.cs).
- `dotnet build signal-lost/Assembly-CSharp.csproj --no-restore` completed successfully. This checks the generated C# project; it is not a Unity Editor compile, Play Mode test, or player build.
- Still to verify in Unity: nearest-target selection with multiple targets; arrow orientation while turning; meter response with distance; beep cadence, pitch, and volume; no acquisition for buried/under-ground targets or targets beyond range; and the one-shot reached event. Check the Console and assigned Inspector references as part of those tests.

### Input-state errors when entering Play Mode

The reported Editor log contained `Map/Control/Binding index out of range` and `NullReferenceException` inside Input System state-change monitors, while both domain and scene reload were disabled. These are stale input-state errors, not missing scanner Inspector references. `ProjectSettings/EditorSettings.asset` now restores normal domain and scene reload, so each Play Mode entry starts with clean script/input state. Close/reopen Unity once to discard an already-corrupted session. Do not disable both reloads again without validating all gameplay/input lifecycles. No scanner runtime code, model transforms, scenes, package versions, or input bindings were changed for this fix.

A fresh temporary session also passed with the old fast-mode settings; the exact previously corrupted Editor state was not reproduced. This is a defensive Editor-configuration fix, not a demonstrated Input System package repair. The regression check uses the same project assets/settings and runs three Play Mode cycles. To repeat, copy the harness into `Assets/Editor` of a disposable project under the system temp directory and run `Unity.exe -batchmode -nographics -projectPath <temporary-project> -executeMethod ScannerPlayModeSmoke.Start -logFile <log-path>` (omit `-quit`). `-scannerFastPlayMode` compares the old settings in that temporary project only. Test-only background/focus settings allow synthetic input in batch mode; when the application is unfocused, the harness checks action bindings and invokes the existing visual toggle directly. These are not physical keyboard/controller tests; no runtime focus restrictions are removed. Run without `-nographics` and with `-force-d3d11` to additionally capture the bar at empty/half/full values from three angles under ignored `Logs/ScannerBar_*.png`.

Record Unity Play Mode results and any known limitations in the pull request, following the [workflow](workflow.md). If the arrow texture is from an external or third-party source, add its attribution and license to [assets.md](assets.md).
