# Scanner prototype

## Scope

This is a first-pass scanner prototype in the separate [`ScannerTest` scene](../signal-lost/Assets/Scenes/ScannerTest.unity). It is not integrated into the shared player prefab or the main `Prototype` scene. The scanner currently detects above-ground enemy signal sources; subterranean discovery and emergence behavior are future work.

The design reference calls for a directional indicator, signal strength, and audio feedback. Exact tuning values here are provisional and should be adjusted through playtesting. See the [design summary](design.md) and [work guidelines](WORK_GUIDELINES_README.md).

## Controls

| Input | Action |
| --- | --- |
| Q | Toggle scanner up/down |
| Gamepad left shoulder (LB/L1) | Toggle scanner up/down |

The `ToggleScanner` action is in the existing Player action map. The toggle script uses a private copy of the Input Actions asset, following the existing player input pattern. Input is ignored while the application is unfocused or the cursor is unlocked.

## Scene setup

Open `signal-lost/Assets/Scenes/ScannerTest.unity` in Unity 6000.6.2f1 and enter Play Mode. The scene contains a player prefab instance with scanner components added as scene overrides, a `ScannerVisualRoot` under the camera, a cube placeholder, a world-space Canvas for the scanner display, and a `ScannerTarget_AboveGround` test object.

Keep `ScannerVisualRoot` as the toggle target. The placeholder and scanner display are children of that root, so they show and hide together. When a scanner model is added, it can replace the placeholder under the same root; the Canvas can then be positioned on the model's display. Do not apply the scene overrides to the shared player prefab without coordinating integration.

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

- The scanner toggle was reported working in Play Mode: Q raises and lowers the scanner visual.
- `dotnet build signal-lost/Assembly-CSharp.csproj --no-restore` completed successfully. This checks the generated C# project; it is not a Unity Editor compile, Play Mode test, or player build.
- Still to verify in Unity: nearest-target selection with multiple targets; arrow orientation while turning; meter response with distance; beep cadence, pitch, and volume; no acquisition for buried/under-ground targets or targets beyond range; and the one-shot reached event. Check the Console and assigned Inspector references as part of those tests.

Record Unity Play Mode results and any known limitations in the pull request, following the [workflow](workflow.md). If the arrow texture is from an external or third-party source, add its attribution and license to [assets.md](assets.md).
