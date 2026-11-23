using System;
using System.Collections.Generic;
using SignalLost.Enemies.Desert;
using SignalLost.Progression;
using SignalLost.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SignalLost.Editor
{
    /// <summary>
    /// The order the islands are sailed (11-22, the user: "make the marsh stage the second stage so that the player has
    /// access to the car on the following stages"): Crash Meadow (the tutorial), the Mudlands, the Desert Dunes, the
    /// Crystal Caves, then the Wasteland (the finale, reached by the stage-skip tool until the Fog Forest exists).
    ///
    /// The level builders put these links in when they build. This links the scenes and prefabs as they stand without
    /// rebuilding them: each boat's destination, the stage it waits for, and the desert's stage exit. Only values that
    /// differ are written, so running it again changes nothing. Menu: Signal Lost &gt; Islands &gt; Link Stage Order, and
    /// Validate Stage Order to check them.
    /// </summary>
    internal static class StageOrderLinker
    {
        public const string MudlandsScene = "Mudlands", DesertScene = "DesertDunes", CavesScene = "CrystalCaves";
        public const string DesertName = "the Desert Dunes", CavesName = "the Crystal Caves";
        public const string MudlandsLocked = "Cavendish won't sail until the Mudlands gives back the landing gear.";
        /// <summary>The island on the tutorial's horizon, past its dock (the Mudlands now; it was the desert).</summary>
        public const string TutorialHorizonIsland = "MudlandsIslandSilhouette";
        private const string OldTutorialHorizonIsland = "DesertIslandSilhouette";

        private struct Link
        {
            public string Destination, Name, Required, Locked;
            public bool AlsoDeparts;
        }

        private static readonly Link TutorialBoat = new Link
        {
            Destination = MudlandsScene, Name = "the Mudlands", Required = StageProgress.Tutorial,
            Locked = "Cavendish still needs you here before you can sail."
        };
        private static readonly Link MudlandsBoat = new Link
        {
            Destination = DesertScene, Name = DesertName, Required = StageProgress.Mudlands, Locked = MudlandsLocked, AlsoDeparts = true
        };
        private static readonly Link DesertBoat = new Link
        {
            Destination = CavesScene, Name = CavesName, Required = StageProgress.Desert, Locked = "Recover the Engine Core before you set sail."
        };

        [MenuItem("Signal Lost/Islands/Link Stage Order")]
        public static void LinkAll()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var log = new List<string>();

            // The desert's departure dock lives in its environment prefab; set it there first so the scene keeps no override.
            GameObject environment = PrefabUtility.LoadPrefabContents(DesertDunesBuilder.EnvironmentPrefabPath);
            try
            {
                int changed = 0;
                foreach (IslandDock dock in environment.GetComponentsInChildren<IslandDock>(true))
                    if (dock.DockRole == IslandDock.Role.Departure) changed += Apply(dock, DesertBoat);
                if (changed > 0) PrefabUtility.SaveAsPrefabAsset(environment, DesertDunesBuilder.EnvironmentPrefabPath);
                log.Add($"desert environment prefab: {changed} change(s)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(environment);
            }

            log.Add(LinkScene(TutorialLevelBuilder.ScenePath, () =>
            {
                int changed = 0;
                foreach (IslandDock dock in Docks()) if (dock.DockRole == IslandDock.Role.Departure) changed += Apply(dock, TutorialBoat);
                GameObject horizon = GameObject.Find(OldTutorialHorizonIsland);
                if (horizon != null)
                {
                    horizon.name = TutorialHorizonIsland;
                    changed++;
                }
                return changed;
            }));
            log.Add(LinkScene(MudlandsBuilder.ScenePath, () =>
            {
                int changed = 0;
                foreach (IslandDock dock in Docks()) changed += Apply(dock, MudlandsBoat);
                return changed;
            }));
            log.Add(LinkScene(CoreSceneSetup.DesertPath, () =>
            {
                int changed = 0;
                foreach (IslandDock dock in Docks()) if (dock.DockRole == IslandDock.Role.Departure) changed += Apply(dock, DesertBoat);
                foreach (DesertStageExit exit in Object.FindObjectsByType<DesertStageExit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var data = new SerializedObject(exit);
                    changed += Set(data, "nextStageScene", CavesScene) + Set(data, "nextStageName", CavesName);
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                return changed;
            }));
            Debug.Log("STAGE_ORDER_LINKED " + string.Join("; ", log));
        }

        [MenuItem("Signal Lost/Islands/Validate Stage Order")]
        public static void ValidateAll()
        {
            var problems = new List<string>();
            void Check(string where, IslandDock dock, Link link)
            {
                if (dock == null) { problems.Add(where + ": no dock"); return; }
                var data = new SerializedObject(dock);
                if (data.FindProperty("destinationScene").stringValue != link.Destination) problems.Add($"{where}: sails to '{data.FindProperty("destinationScene").stringValue}', expected '{link.Destination}'");
                if (data.FindProperty("requiredStage").stringValue != link.Required) problems.Add($"{where}: waits for '{data.FindProperty("requiredStage").stringValue}', expected '{link.Required}'");
                if (data.FindProperty("destinationName").stringValue != link.Name) problems.Add($"{where}: named '{data.FindProperty("destinationName").stringValue}', expected '{link.Name}'");
                if (!dock.Departs) problems.Add(where + ": its boat doesn't depart");
            }
            IslandDock Find(IslandDock.Role role)
            {
                foreach (IslandDock dock in Docks()) if (dock.DockRole == role) return dock;
                return null;
            }

            EditorSceneManager.OpenScene(TutorialLevelBuilder.ScenePath, OpenSceneMode.Single);
            Check("Crash Meadow", Find(IslandDock.Role.Departure), TutorialBoat);
            if (GameObject.Find(TutorialHorizonIsland) == null) problems.Add("Crash Meadow: no " + TutorialHorizonIsland);

            EditorSceneManager.OpenScene(MudlandsBuilder.ScenePath, OpenSceneMode.Single);
            Check("the Mudlands", Find(IslandDock.Role.Arrival), MudlandsBoat);

            EditorSceneManager.OpenScene(CoreSceneSetup.DesertPath, OpenSceneMode.Single);
            if (Find(IslandDock.Role.Arrival) == null) problems.Add("the Desert Dunes: no arrival dock");
            Check("the Desert Dunes", Find(IslandDock.Role.Departure), DesertBoat);
            foreach (DesertStageExit exit in Object.FindObjectsByType<DesertStageExit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (new SerializedObject(exit).FindProperty("nextStageScene").stringValue != CavesScene) problems.Add("the Desert Dunes: the stage exit doesn't lead to the Crystal Caves");

            EditorSceneManager.OpenScene(CrystalCavesBuilder.ScenePath, OpenSceneMode.Single);
            if (Find(IslandDock.Role.Arrival) == null) problems.Add("the Crystal Caves: no arrival dock");

            foreach (string scene in new[] { MudlandsScene, DesertScene, CavesScene })
            {
                bool listed = false;
                foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
                    if (entry.enabled && System.IO.Path.GetFileNameWithoutExtension(entry.path) == scene) listed = true;
                if (!listed) problems.Add(scene + " is not in the build's scene list");
            }

            if (problems.Count > 0) throw new InvalidOperationException("Stage order problems:\n" + string.Join("\n", problems));
            Debug.Log("STAGE_ORDER_VALID tutorial -> Mudlands -> desert -> caves");
        }

        public static void LinkBatch() => Batch(LinkAll);
        public static void ValidateBatch() => Batch(ValidateAll);

        private static void Batch(Action action)
        {
            try
            {
                action();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        // ---------- Helpers ----------

        private static IEnumerable<IslandDock> Docks() =>
            Object.FindObjectsByType<IslandDock>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        private static string LinkScene(string path, Func<int> link)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int changed = link();
            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            return $"{scene.name}: {changed} change(s)";
        }

        private static int Apply(IslandDock dock, Link link)
        {
            var data = new SerializedObject(dock);
            int changed = Set(data, "destinationScene", link.Destination) + Set(data, "destinationName", link.Name)
                + Set(data, "requiredStage", link.Required) + Set(data, "lockedMessage", link.Locked);
            if (dock.DockRole == IslandDock.Role.Arrival && data.FindProperty("alsoDeparts").boolValue != link.AlsoDeparts)
            {
                data.FindProperty("alsoDeparts").boolValue = link.AlsoDeparts;
                changed++;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        private static int Set(SerializedObject data, string property, string value)
        {
            SerializedProperty field = data.FindProperty(property);
            if (field.stringValue == value) return 0;
            field.stringValue = value;
            return 1;
        }
    }
}
