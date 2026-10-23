# Demo build

The playable demo (meeting notes 10-23): it starts on the title screen, and New Game plays the **Tutorial Demo** (`Scenes/Tutorial/Tutorial Demo.unity`) from the crash to the Chief Toad and the "Thanks for playing" card. Only those two scenes are in the build.

## Build it

In Unity 6000.6.2f1, **Signal Lost > Build**:

| Menu item | Output (in `signal-lost/Builds/`, git-ignored) | Needs |
| --- | --- | --- |
| **Mac Demo** | `Mac/Signal Lost Demo.app` and `SignalLostDemo-Mac.zip` | Mac Build Support (installed with the Mac editor) |
| **Windows Demo (.exe)** | `Windows/Signal Lost Demo.exe` (with its `_Data` folder) and `SignalLostDemo-Windows.zip` | **Windows Build Support (Mono)** for 6000.6.2f1: Unity Hub > Installs > 6000.6.2f1 > Add modules |

Batch builds: `Unity -batchmode -quit -projectPath signal-lost -executeMethod SignalLost.Editor.DemoBuild.WindowsBatch` (or `MacBatch`). Without the module, the Windows build stops with a message saying which module to add.

Share the zip. Testers unzip it and run `Signal Lost Demo.exe` (Windows) or `Signal Lost Demo.app` (Mac). Builds are unsigned, so Windows SmartScreen asks to "Run anyway", and on a Mac the first launch needs right-click > Open.

The build script (`Editor/DemoBuild.cs`) also sets the product name ("Signal Lost"), version (0.1.0-demo), app id (com.signallost.demo) and a resizable full-screen window.

## Mac: Apple silicon or universal

Mac builds are universal (Apple silicon and Intel) when Burst's `llvm-lipo` tool can run. On this machine's Unity install it shipped without its executable bit, so builds are **Apple silicon only** (the Console says so). To get universal builds, run once:

```
chmod +x /Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app/Contents/Resources/Burst/Client/bcl/hostmac/llvm-lipo
```

## What a playthrough looks like

Title screen (looping menu music) > New Game > the full [tutorial](tutorial.md), ending with the Chief Toad, his crown on the camp's trophy stand, Cavendish's wrap-up and the end card (Keep Exploring / Main Menu). New Game clears the camp's saved trophies.
