using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SignalLost.Editor
{
    /// <summary>
    /// Builds the playable demo (meeting notes 10-23): the title screen first, then the tutorial demo, and nothing
    /// else. Output goes to <c>signal-lost/Builds/</c> (git-ignored) and is zipped, ready to hand to testers:
    /// <list type="bullet">
    /// <item><b>Signal Lost > Build > Mac Demo</b>: <c>Builds/Mac/Signal Lost Demo.app</c> and <c>Builds/SignalLostDemo-Mac.zip</c>.</item>
    /// <item><b>Signal Lost > Build > Windows Demo (.exe)</b>: <c>Builds/Windows/Signal Lost Demo.exe</c> and
    /// <c>Builds/SignalLostDemo-Windows.zip</c>. Needs Unity Hub's "Windows Build Support (Mono)" module for 6000.6.2f1.</item>
    /// </list>
    /// Batch: <c>-executeMethod SignalLost.Editor.DemoBuild.MacBatch</c> or <c>.WindowsBatch</c>.
    /// </summary>
    internal static class DemoBuild
    {
        public const string ProductName = "Signal Lost";
        public const string Version = "0.1.0-demo";
        private const string Output = "Builds";

        /// <summary>The demo's scenes, in order: the game starts on the first.</summary>
        public static string[] Scenes => new[] { CoreSceneSetup.MainMenuPath, TutorialLevelBuilder.ScenePath };

        [MenuItem("Signal Lost/Build/Mac Demo")]
        public static void Mac() => Build(BuildTarget.StandaloneOSX, "Mac/Signal Lost Demo.app", "SignalLostDemo-Mac.zip");

        [MenuItem("Signal Lost/Build/Windows Demo (.exe)")]
        public static void Windows() => Build(BuildTarget.StandaloneWindows64, "Windows/Signal Lost Demo.exe", "SignalLostDemo-Windows.zip");

        public static void MacBatch() => Batch(Mac);
        public static void WindowsBatch() => Batch(Windows);

        private static void Batch(Action build)
        {
            try
            {
                build();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>The player settings every demo build uses (also saved to the project, so the editor shows them).</summary>
        public static void Configure()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = "Signal Lost";
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.signallost.demo");
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
#if UNITY_EDITOR_OSX
            // Universal (Apple silicon and Intel Macs) when Burst's lipo tool can run. Some Unity 6000.6.2f1 installs ship
            // it without its executable bit; then the build is Apple silicon only until someone runs:
            //   chmod +x /Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app/Contents/Resources/Burst/Client/bcl/hostmac/llvm-lipo
            bool universal = CanRun(Path.Combine(EditorApplication.applicationContentsPath, "Resources/Burst/Client/bcl/hostmac/llvm-lipo"));
            UnityEditor.OSXStandalone.UserBuildSettings.architecture = universal ? OSArchitecture.x64ARM64 : OSArchitecture.ARM64;
            if (!universal) Debug.LogWarning("Mac demo: building for Apple silicon only (Burst's llvm-lipo is not executable; see DemoBuild.cs).");
#endif
            AssetDatabase.SaveAssets();
        }

        private static void Build(BuildTarget target, string relativePath, string zipName)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new InvalidOperationException($"{target} builds need its module: Unity Hub > Installs > 6000.6.2f1 > Add modules > " +
                                                    (target == BuildTarget.StandaloneOSX ? "Mac Build Support (Mono)." : "Windows Build Support (Mono)."));
            foreach (string scene in Scenes)
                if (!File.Exists(scene)) throw new InvalidOperationException("Missing demo scene " + scene);
            Configure();

            string path = Path.Combine(Output, relativePath);
            string folder = Path.GetDirectoryName(path);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = path,
                target = target,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"{target} build {report.summary.result} with {report.summary.totalErrors} error(s).");

            string zip = Path.Combine(Output, zipName);
            if (File.Exists(zip)) File.Delete(zip);
            if (target == BuildTarget.StandaloneOSX && Application.platform == RuntimePlatform.OSXEditor)
                Run("/usr/bin/ditto", $"-c -k --sequesterRsrc --keepParent \"{path}\" \"{zip}\""); // keeps the .app's permissions and links
            else
                ZipFile.CreateFromDirectory(folder, zip, System.IO.Compression.CompressionLevel.Optimal, false);
            Debug.Log($"DEMO_BUILD_DONE {target}: {Path.GetFullPath(path)} ({report.summary.totalSize / (1024 * 1024)} MB), zipped to {Path.GetFullPath(zip)}");
        }

        private static bool CanRun(string file)
        {
            if (!File.Exists(file)) return false;
            using Process test = Process.Start(new ProcessStartInfo("/bin/test", $"-x \"{file}\"") { UseShellExecute = false, CreateNoWindow = true });
            test.WaitForExit();
            return test.ExitCode == 0;
        }

        private static void Run(string file, string arguments)
        {
            using Process process = Process.Start(new ProcessStartInfo(file, arguments) { UseShellExecute = false, CreateNoWindow = true });
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException($"{file} failed ({process.ExitCode}).");
        }
    }
}
