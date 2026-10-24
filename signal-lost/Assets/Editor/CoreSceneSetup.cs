using System;
using System.Collections.Generic;
using System.Linq;
using SignalLost.Combat;
using SignalLost.Inventory;
using SignalLost.NPC;
using SignalLost.Player;
using SignalLost.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SignalLost.Editor
{
    /// <summary>
    /// Moves scenes onto the shared core prefabs (Prefabs/Core) and sets up the title screen.
    /// <see cref="UpgradeToCorePlayer"/> works on any scene: it swaps the old player (and its HUDs) for the Player
    /// prefab at the same spot, adds GameSystems and a PlayerSpawnPoint, and optionally a StageReset.
    /// Menu: Signal Lost > Scenes.
    /// </summary>
    internal static class CoreSceneSetup
    {
        public const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        public const string SandboxPath = "Assets/Scenes/Testing Sandbox.unity";
        public const string DesertPath = "Assets/Scenes/Large Areas/DesertDunes.unity";
        private static readonly PlayerItem[] AllGear = { PlayerItem.MiningLaser, PlayerItem.Scanner, PlayerItem.Flask };

        [MenuItem("Signal Lost/Scenes/Upgrade Testing Sandbox (free-play)")]
        public static void UpgradeSandbox()
        {
            Scene scene = EditorSceneManager.OpenScene(SandboxPath, OpenSceneMode.Single);
            Transform targets = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "Tutorial Targets");
            UpgradeToCorePlayer(scene, AllGear, targets != null ? targets.gameObject : null);

            // Free play: no story, the merchant is already friendly (its cousins wait despawned for the angry-merchant
            // ambush), test props stay.
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject); // the old quest flow
            foreach (MerchantCrab merchant in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MerchantCrab>(true)))
            {
                var data = new SerializedObject(merchant);
                data.FindProperty("startFriendly").boolValue = true;
                AddCousins(merchant, data.FindProperty("cousins"));
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            Save(scene, "SANDBOX_UPGRADED");
        }

        // The merchant's ten cousins (CrabCousinBuilder), placed beside it; they only appear when it calls them.
        private static void AddCousins(MerchantCrab merchant, SerializedProperty list)
        {
            if (list.arraySize > 0) return;
            CrabCousinBuilder.Build();
            var group = new GameObject("Bucket Brothers").transform;
            group.SetParent(merchant.transform.parent, false);
            group.position = merchant.transform.position;
            string[] paths = CrabCousinBuilder.PrefabPaths.ToArray();
            list.arraySize = paths.Length;
            for (int i = 0; i < paths.Length; i++)
            {
                var cousin = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]), group);
                float angle = i * Mathf.PI * 2f / paths.Length;
                cousin.transform.position = merchant.transform.position + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * 5f;
                list.GetArrayElementAtIndex(i).objectReferenceValue = cousin.GetComponent<SignalLost.Enemies.CrabCousin>();
                cousin.SetActive(false); // despawned until called (and kept out of the runtime NavMesh bake)
            }
        }

        [MenuItem("Signal Lost/Scenes/Upgrade Desert Dunes")]
        public static void UpgradeDesert()
        {
            Scene scene = EditorSceneManager.OpenScene(DesertPath, OpenSceneMode.Single);
            UpgradeToCorePlayer(scene, AllGear, null);
            Save(scene, "DESERT_UPGRADED");
        }

        /// <summary>
        /// Replaces whatever player the scene has with the Player prefab at the same position and facing, removes the
        /// old HUD canvases (TutorialHUD, PlayerHUD) and adds GameSystems, a PlayerSpawnPoint where the player stood
        /// and, if <paramref name="stageGroup"/> is given, a StageReset that restores it on respawn.
        /// </summary>
        public static GameObject UpgradeToCorePlayer(Scene scene, PlayerItem[] startingItems, GameObject stageGroup)
        {
            var roots = scene.GetRootGameObjects();
            FirstPersonController old = roots.SelectMany(r => r.GetComponentsInChildren<FirstPersonController>(true))
                .FirstOrDefault(c => c.GetComponent<PlayerGear>() == null);
            GameObject player = roots.SelectMany(r => r.GetComponentsInChildren<PlayerGear>(true)).Select(g => g.gameObject).FirstOrDefault();
            if (player == null)
            {
                Vector3 position = old != null ? old.transform.position : Vector3.up;
                Quaternion rotation = old != null ? Quaternion.Euler(0f, old.transform.eulerAngles.y, 0f) : Quaternion.identity;
                if (old != null)
                {
                    GameObject oldRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(old.gameObject);
                    UnityEngine.Object.DestroyImmediate(oldRoot != null ? oldRoot : old.gameObject);
                }
                player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CorePrefabBuilder.PlayerPath), scene);
                player.transform.SetPositionAndRotation(position, rotation);

                var spawn = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CorePrefabBuilder.SpawnPath), scene);
                spawn.transform.SetPositionAndRotation(position, rotation);
            }
            var gear = new SerializedObject(player.GetComponent<PlayerGear>());
            SerializedProperty items = gear.FindProperty("startingItems");
            items.arraySize = startingItems.Length;
            for (int i = 0; i < startingItems.Length; i++) items.GetArrayElementAtIndex(i).enumValueIndex = (int)startingItems[i];
            gear.ApplyModifiedPropertiesWithoutUndo();

            // Old HUDs (the tutorial's HUD canvas, the desert's PlayerHUD) and stray scene cameras: the player's
            // camera is the only one, which also leaves a single audio listener.
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponent<HUDController>() != null || root.GetComponent<PlayerHud>() != null ||
                    (root.GetComponent<Camera>() != null && root.GetComponentsInChildren<Component>().Length <= 5))
                    UnityEngine.Object.DestroyImmediate(root);

            if (!scene.GetRootGameObjects().Any(r => r.GetComponent<SignalLost.Core.GameSystems>() != null))
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CorePrefabBuilder.SystemsPath), scene);
            if (stageGroup != null && !scene.GetRootGameObjects().Any(r => r.GetComponent<StageReset>() != null))
            {
                var reset = new GameObject("Stage Reset");
                SceneManager.MoveGameObjectToScene(reset, scene);
                var data = new SerializedObject(reset.AddComponent<StageReset>());
                data.FindProperty("group").objectReferenceValue = stageGroup;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            return player;
        }

        [MenuItem("Signal Lost/Scenes/Create Main Menu Scene")]
        public static void CreateMainMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuPath) == null)
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                camera.tag = "MainCamera";
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.15f, 0.2f, 0.4f);
                new GameObject("Main Menu").AddComponent<MainMenuScreen>();
                EditorSceneManager.SaveScene(scene, MainMenuPath);
            }
            SetBuildScenes();
            Debug.Log("MAIN_MENU_CREATED");
        }

        /// <summary>Title screen first, then the playable scenes (New Game loads "Tutorial").</summary>
        public static void SetBuildScenes()
        {
            var paths = new List<string> { MainMenuPath, "Assets/Scenes/Tutorial/Tutorial Demo.unity", SandboxPath, DesertPath, "Assets/Scenes/Initial Test Scenes/Prototype.unity" };
            EditorBuildSettings.scenes = paths.Where(p => AssetDatabase.LoadAssetAtPath<SceneAsset>(p) != null)
                .Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
        }

        private static void Save(Scene scene, string message)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(message);
        }

        /// <summary>Batch entry: rebuild the core prefabs, then set up the title screen, sandbox and desert.</summary>
        public static void SetUpAllBatch()
        {
            try
            {
                CorePrefabBuilder.Build();
                CreateMainMenu();
                UpgradeSandbox();
                UpgradeDesert();
                SetBuildScenes();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
