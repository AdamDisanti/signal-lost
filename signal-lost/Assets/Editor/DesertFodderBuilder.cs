using System;
using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Enemies;
using SignalLost.Enemies.Desert;
using SignalLost.Player;
using SignalLost.Scanner;
using SignalLost.UI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static SignalLost.Editor.EnemyTestBuilder;
using Object = UnityEngine.Object;

namespace SignalLost.Editor
{
    /// <summary>
    /// Generates the five desert fodder enemies (Spark Tick, Snorkel Eel, Jerry-Can Beetle, Hubcap Armadillo,
    /// Dish Meerkat) as low-poly placeholder prefabs with their meshes and materials, and a separate test scene,
    /// DesertFodderTest.unity, with one FodderSpawner "station" per enemy around a sandy arena.
    ///
    /// It only creates new files under Assets/Prefabs/Enemies/Desert, Assets/Materials/Enemies/Desert and
    /// Assets/Scenes/DesertFodderTest.unity (plus the five single-enemy test scenes, see CreateSingleEnemyScenes). It does not touch the tutorial, the Desert Dunes scene, the shared player
    /// or HUD prefabs, or any existing enemy. Like the other builders it refuses to overwrite the test scene;
    /// "Regenerate Prefabs" rebuilds the five prefabs and their meshes in place (same GUIDs).
    /// </summary>
    public static class DesertFodderBuilder
    {
        public const string ScenePath = "Assets/Scenes/DesertFodderTest.unity";
        private const string Root = "Assets/Prefabs/Enemies/Desert";
        private const string DesertMeshFolder = Root + "/Meshes";
        private const string DesertMaterialFolder = "Assets/Materials/Enemies/Desert";
        internal const string ScannerRigPath = "Assets/Prefabs/Player/Scanner/ScannerRig.prefab";
        internal const string HudPrefabPath = "Assets/Prefabs/UI/PlayerHUD.prefab";

        public const string TickPath = Root + "/SparkTick.prefab";
        public const string EelPath = Root + "/SnorkelEel.prefab";
        public const string BeetlePath = Root + "/JerryCanBeetle.prefab";
        public const string ArmadilloPath = Root + "/HubcapArmadillo.prefab";
        public const string MeerkatPath = Root + "/DishMeerkat.prefab";

        // ---------- Menus ----------

        [MenuItem("Signal Lost/Desert Fodder/Create Test Scene")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("Desert fodder test scene already exists. Edit it in Unity instead.");
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            var beam = AssetDatabase.LoadAssetAtPath<Material>(BeamMaterialPath);
            var scannerRig = AssetDatabase.LoadAssetAtPath<GameObject>(ScannerRigPath);
            var hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            if (playerPrefab == null || input == null || beam == null || scannerRig == null || hudPrefab == null)
                throw new InvalidOperationException("Player, input actions, laser beam, ScannerRig or PlayerHUD prefab missing. Is your branch up to date with main?");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureDesertFolders();
            GameObject tick = Load(TickPath) ?? BuildTick();
            GameObject eel = Load(EelPath) ?? BuildEel();
            GameObject beetle = Load(BeetlePath) ?? BuildBeetle();
            GameObject armadillo = Load(ArmadilloPath) ?? BuildArmadillo();
            GameObject meerkat = Load(MeerkatPath) ?? BuildMeerkat();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildTestArena();
            BuildStations(tick, eel, beetle, armadillo, meerkat);
            BuildTestPlayer(playerPrefab, input, beam, scannerRig, hudPrefab);
            new GameObject("Fodder Status Overlay").AddComponent<FodderStatusOverlay>();
            BuildDesertLighting();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("DESERT_FODDER_TEST_SCENE_CREATED_AND_VALIDATED");
        }

        [MenuItem("Signal Lost/Desert Fodder/Regenerate Prefabs")]
        public static void Regenerate()
        {
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Regenerate desert fodder prefabs",
                    "This rebuilds the five desert fodder prefabs and their meshes from the builder script, replacing any " +
                    "edits made to them. Materials are kept. Continue?", "Regenerate", "Cancel"))
                return;
            EnsureDesertFolders();
            BuildTick();
            BuildEel();
            BuildBeetle();
            BuildArmadillo();
            BuildMeerkat();
            AssetDatabase.SaveAssets();
            Debug.Log("DESERT_FODDER_PREFABS_REGENERATED");
        }

        [MenuItem("Signal Lost/Desert Fodder/Validate Test Scene")]
        public static void Validate()
        {
            ValidatePrefab<SparkTick>(TickPath, "legs", "sparkMaterial");
            ValidatePrefab<SnorkelEel>(EelPath, "pipeHealth", "head", "jaw", "wake", "bodyCollider", "sandMaterial");
            ValidatePrefab<JerryCanBeetle>(BeetlePath, "can", "explosionMaterial", "canRenderers", "legs");
            ValidatePrefab<HubcapArmadillo>(ArmadilloPath, "shell", "limbs", "legs", "sparkMaterial", "dustMaterial");
            ValidatePrefab<DishMeerkat>(MeerkatPath, "dish", "head", "burrow", "shriekMaterial", "dustMaterial");
            Require(Load(EelPath).GetComponent<EnemySignalSource>() != null, "Snorkel Eel needs an EnemySignalSource for the scanner.");

            EditorSceneManager.OpenScene(ScenePath);
            Require(Count<FirstPersonController>() == 1, "Expected one player.");
            Require(Count<MiningLaser>() == 1, "Expected one mining laser.");
            Require(Count<ScannerController>() == 1, "Expected one scanner.");
            Require(Count<PlayerHud>() == 1, "Expected one PlayerHud.");
            Require(Count<FodderSpawner>() == 5, "Expected five fodder spawner stations.");
            foreach (FodderSpawner spawner in All<FodderSpawner>())
            {
                SerializedProperty entries = new SerializedObject(spawner).FindProperty("entries");
                Require(entries.arraySize > 0, spawner.name + " has no entries.");
                for (int i = 0; i < entries.arraySize; i++)
                    Require(entries.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue != null,
                        spawner.name + " has an empty prefab entry.");
            }
            Require(Count<DesertFodder>() == 0, "Fodder should be spawned at runtime, not placed in the scene.");
            Require(Count<NavMeshSurface>() == 1 && Count<NavMeshBaker>() == 1, "Expected one NavMeshSurface with a NavMeshBaker.");
            Require(Count<Camera>() == 1 && Count<AudioListener>() == 1, "Expected one camera and listener.");
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0, "Missing script: " + item.name);
            Debug.Log("DESERT_FODDER_TEST_SCENE_VALIDATED");
        }

        private static void ValidatePrefab<T>(string path, params string[] fields) where T : DesertFodder
        {
            GameObject prefab = Load(path);
            Require(prefab != null, "Missing prefab " + path);
            var brain = prefab.GetComponent<T>();
            Require(brain != null, path + " has no " + typeof(T).Name);
            Require(prefab.GetComponent<NavMeshAgent>() != null && prefab.GetComponent<Health>() != null, path + " needs a NavMeshAgent and Health.");
            Require(prefab.GetComponentInChildren<Collider>(true) != null, path + " has no collider.");
            var data = new SerializedObject(brain);
            Require(data.FindProperty("visual").objectReferenceValue != null, path + " has no Visual reference.");
            foreach (string field in fields)
            {
                SerializedProperty property = data.FindProperty(field);
                Require(property != null, path + ": unknown field " + field);
                if (property.isArray) Require(property.arraySize > 0, path + ": " + field + " is empty.");
                else Require(property.objectReferenceValue != null, path + ": " + field + " is not assigned.");
            }
        }

        /// <summary>Builds any of the five fodder prefabs that don't exist yet (for scenes that spawn them).</summary>
        internal static void EnsureFodderPrefabs()
        {
            EnsureDesertFolders();
            if (Load(TickPath) == null) BuildTick();
            if (Load(EelPath) == null) BuildEel();
            if (Load(BeetlePath) == null) BuildBeetle();
            if (Load(ArmadilloPath) == null) BuildArmadillo();
            if (Load(MeerkatPath) == null) BuildMeerkat();
        }

        // ---------- Single-enemy test scenes (combat test map) ----------

        private readonly struct SingleTest
        {
            public readonly string Scene, PrefabPath, Label;
            public readonly int Group, MaxAlive;
            public SingleTest(string scene, string prefabPath, string label, int group, int maxAlive)
            {
                Scene = scene; PrefabPath = prefabPath; Label = label; Group = group; MaxAlive = maxAlive;
            }
        }

        // One scene per enemy. Ticks come as a pack of four (to test latching and swatting several); beetles in pairs
        // (to test chain explosions); the others one at a time.
        private static readonly SingleTest[] SingleTests =
        {
            new SingleTest("Assets/Scenes/SparkTickTest.unity", TickPath, "Spark Ticks", 4, 4),
            new SingleTest("Assets/Scenes/SnorkelEelTest.unity", EelPath, "Snorkel Eel", 1, 1),
            new SingleTest("Assets/Scenes/JerryCanBeetleTest.unity", BeetlePath, "Jerry-Can Beetles", 2, 2),
            new SingleTest("Assets/Scenes/HubcapArmadilloTest.unity", ArmadilloPath, "Hubcap Armadillo", 1, 1),
            new SingleTest("Assets/Scenes/DishMeerkatTest.unity", MeerkatPath, "Dish Meerkat", 1, 1),
        };

        /// <summary>
        /// Creates one test scene per fodder enemy on the combat test's map (the same 20 x 50 m walled range and cover
        /// block, reusing its materials), with a NavMesh, the desert player setup, and a spawner that keeps exactly that
        /// enemy coming back a few seconds after it dies. Scenes that already exist are skipped, never overwritten.
        /// </summary>
        [MenuItem("Signal Lost/Desert Fodder/Create Single-Enemy Test Scenes")]
        public static void CreateSingleEnemyScenes()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            var beam = AssetDatabase.LoadAssetAtPath<Material>(BeamMaterialPath);
            var scannerRig = AssetDatabase.LoadAssetAtPath<GameObject>(ScannerRigPath);
            var hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            if (playerPrefab == null || input == null || beam == null || scannerRig == null || hudPrefab == null)
                throw new InvalidOperationException("Player, input actions, laser beam, ScannerRig or PlayerHUD prefab missing. Is your branch up to date with main?");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureDesertFolders();
            if (Load(TickPath) == null) BuildTick();
            if (Load(EelPath) == null) BuildEel();
            if (Load(BeetlePath) == null) BuildBeetle();
            if (Load(ArmadilloPath) == null) BuildArmadillo();
            if (Load(MeerkatPath) == null) BuildMeerkat();

            int created = 0;
            foreach (SingleTest test in SingleTests)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(test.Scene) != null)
                {
                    Debug.Log(test.Scene + " already exists; skipped (edit it in Unity instead).");
                    continue;
                }
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                BuildCombatTestMap();
                BuildSingleSpawner(test);
                BuildTestPlayer(playerPrefab, input, beam, scannerRig, hudPrefab);
                GameObject player = Object.FindAnyObjectByType<MiningLaser>().gameObject;
                player.transform.SetPositionAndRotation(new Vector3(0f, 0.1f, -20f), Quaternion.identity); // combat test start
                SetObjective("Test: " + test.Label + " (respawns a few seconds after dying)");
                new GameObject("Fodder Status Overlay").AddComponent<FodderStatusOverlay>();
                BuildCombatLighting();
                EditorSceneManager.SaveScene(scene, test.Scene);
                created++;
            }
            AssetDatabase.SaveAssets();
            ValidateSingleEnemyScenes();
            Debug.Log($"DESERT_FODDER_SINGLE_TESTS_CREATED_AND_VALIDATED ({created} new)");
        }

        [MenuItem("Signal Lost/Desert Fodder/Validate Single-Enemy Test Scenes")]
        public static void ValidateSingleEnemyScenes()
        {
            foreach (SingleTest test in SingleTests)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(test.Scene) == null) continue;
                EditorSceneManager.OpenScene(test.Scene);
                Require(Count<FirstPersonController>() == 1 && Count<MiningLaser>() == 1, test.Scene + ": expected one player with a laser.");
                Require(Count<ScannerController>() == 1 && Count<PlayerHud>() == 1, test.Scene + ": expected the scanner and HUD.");
                Require(Count<NavMeshSurface>() == 1 && Count<NavMeshBaker>() == 1, test.Scene + ": expected one NavMeshSurface with a NavMeshBaker.");
                Require(Count<Camera>() == 1 && Count<AudioListener>() == 1, test.Scene + ": expected one camera and listener.");
                Require(Count<DesertFodder>() == 0, test.Scene + ": fodder should be spawned at runtime, not placed.");
                List<FodderSpawner> spawners = All<FodderSpawner>();
                Require(spawners.Count == 1, test.Scene + ": expected one spawner.");
                SerializedProperty entries = new SerializedObject(spawners[0]).FindProperty("entries");
                Require(entries.arraySize == 1 && entries.GetArrayElementAtIndex(0).FindPropertyRelative("prefab").objectReferenceValue == Load(test.PrefabPath),
                    test.Scene + ": the spawner should spawn only " + test.Label + ".");
                foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                    foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                        Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0, test.Scene + ": missing script on " + item.name);
            }
            Debug.Log("DESERT_FODDER_SINGLE_TESTS_VALIDATED");
        }

        /// <summary>The combat test's map: same floor, walls and cover block, plus a NavMesh built from them at runtime.</summary>
        private static void BuildCombatTestMap()
        {
            Material floor = ExistingOrLit("Assets/Materials/Combat/CombatFloor.mat", "ArenaFloor", new Color(0.32f, 0.36f, 0.4f));
            Material wall = ExistingOrLit("Assets/Materials/Combat/CombatWall.mat", "ArenaWall", new Color(0.52f, 0.55f, 0.58f));
            Material cover = ExistingOrLit("Assets/Materials/Combat/CombatCover.mat", "ArenaCover", new Color(0.78f, 0.49f, 0.23f));

            var area = new GameObject("Combat Test Area");
            var surface = area.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            area.AddComponent<NavMeshBaker>();
            Transform a = area.transform;
            Block("Floor", new Vector3(0, -0.5f, 0), new Vector3(20, 1, 50), floor, a);
            Block("North wall", new Vector3(0, 1.5f, 25), new Vector3(21, 3, 1), wall, a);
            Block("South wall", new Vector3(0, 1.5f, -25), new Vector3(21, 3, 1), wall, a);
            Block("East wall", new Vector3(10, 1.5f, 0), new Vector3(1, 3, 50), wall, a);
            Block("West wall", new Vector3(-10, 1.5f, 0), new Vector3(1, 3, 50), wall, a);
            Block("Cover block", new Vector3(5, 1.25f, -12), new Vector3(3, 2.5f, 1), cover, a);
        }

        /// <summary>A spawner in the middle of the range that keeps the scene's one enemy type coming, in front of the player.</summary>
        private static void BuildSingleSpawner(SingleTest test)
        {
            var station = new GameObject(test.Label + " Spawner");
            station.transform.position = new Vector3(0f, 0f, 2f);
            var spawner = station.AddComponent<FodderSpawner>();
            var data = new SerializedObject(spawner);
            SerializedProperty entries = data.FindProperty("entries");
            entries.arraySize = 1;
            SerializedProperty entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("prefab").objectReferenceValue = Load(test.PrefabPath);
            entry.FindPropertyRelative("weight").floatValue = 1f;
            entry.FindPropertyRelative("minGroup").intValue = test.Group;
            entry.FindPropertyRelative("maxGroup").intValue = test.Group;
            data.FindProperty("maxAlive").intValue = test.MaxAlive;
            data.FindProperty("spawnRadiusMin").floatValue = 2f;
            data.FindProperty("spawnRadiusMax").floatValue = 6f;
            data.FindProperty("minPlayerDistance").floatValue = 10f;
            data.FindProperty("preferOutOfView").boolValue = false; // spawn where you can watch them arrive
            data.FindProperty("activationRange").floatValue = 0f;   // always on
            data.FindProperty("firstWaveDelay").floatValue = 1.5f;
            data.FindProperty("waveInterval").floatValue = 4f;      // a dead enemy is replaced about 4 s later
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetObjective(string text)
        {
            PlayerHud hud = Object.FindAnyObjectByType<PlayerHud>();
            Text objective = hud != null ? hud.transform.Find("Objective/Text")?.GetComponent<Text>() : null;
            if (objective != null) objective.text = text;
        }

        private static void BuildCombatLighting()
        {
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            light.gameObject.AddComponent<UniversalAdditionalLightData>();
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.65f);
        }

        // ---------- Shared parts ----------

        private static Mesh blob, gem, limb, box;

        private static void LoadSharedMeshes()
        {
            blob = SaveDesertMesh(LowPolyMesh.Icosphere("DesertBlob", 1));
            gem = SaveDesertMesh(LowPolyMesh.Icosphere("DesertGem", 0));
            limb = SaveDesertMesh(LowPolyMesh.TaperedPrism("DesertLimb", 5, 0.6f));
            box = SaveDesertMesh(LowPolyMesh.TaperedPrism("DesertBox", 4, 1f));
        }

        private static GameObject NewRoot(string name, out Transform visual)
        {
            LoadSharedMeshes();
            var root = new GameObject(name);
            visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            return root;
        }

        /// <summary>Adds the brain (which brings its NavMeshAgent and Health) and sets the agent up like the other enemies.</summary>
        private static T AddBrain<T>(GameObject root, float maxHealth, float radius, float height, float speed, float acceleration)
            where T : DesertFodder
        {
            T brain = root.AddComponent<T>();
            var agent = root.GetComponent<NavMeshAgent>();
            agent.enabled = false; // switched on in DesertFodder.Start, once the NavMesh has been built
            agent.radius = radius;
            agent.height = height;
            agent.speed = speed;
            agent.acceleration = acceleration;
            agent.angularSpeed = 540f;
            agent.stoppingDistance = 0.2f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            var health = new SerializedObject(root.GetComponent<Health>());
            health.FindProperty("maxHealth").floatValue = maxHealth;
            health.ApplyModifiedPropertiesWithoutUndo();
            return brain;
        }

        private static Transform Eye(Transform parent, Vector3 position, float size, Material eye, Material pupil)
        {
            Transform socket = Part("Eye", parent, gem, eye, position, Quaternion.identity, Vector3.one * size);
            Part("Pupil", socket, gem, pupil, new Vector3(0f, 0.1f, 0.75f), Quaternion.identity, Vector3.one * 0.55f);
            return socket;
        }

        // ---------- Spark Tick ----------

        // A palm-sized tick (about 0.3 m): a flat body, a little head, six legs, and stripped copper wires sprouting
        // from its back.
        internal static GameObject BuildTick()
        {
            GameObject root = NewRoot("Spark Tick", out Transform visual);
            Material shell = Lit("TickShell", new Color(0.42f, 0.28f, 0.2f), 0.1f, 0.55f);
            Material legMat = Lit("TickLeg", new Color(0.25f, 0.17f, 0.12f), 0f, 0.3f);
            Material copper = Lit("TickCopper", new Color(0.86f, 0.46f, 0.2f), 0.9f, 0.6f);
            Material eye = Lit("TickEye", new Color(0.95f, 0.15f, 0.1f), 0f, 0.8f);
            Material pupil = Lit("FodderPupil", new Color(0.03f, 0.03f, 0.04f), 0f, 0.8f);
            Material spark = Unlit("TickSpark", new Color(0.55f, 0.9f, 1f));
            Mesh wire = SaveDesertMesh(LowPolyMesh.Tube("TickWire", new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(0f, 0.07f, 0.01f), new Vector3(0.02f, 0.12f, -0.02f), new Vector3(0.05f, 0.15f, -0.06f),
            }, 0.008f, 4));

            Part("Body", visual, blob, shell, new Vector3(0f, 0.1f, -0.02f), Quaternion.identity, new Vector3(0.13f, 0.075f, 0.16f));
            Part("Back Plate", visual, blob, copper, new Vector3(0f, 0.15f, -0.03f), Quaternion.identity, new Vector3(0.08f, 0.03f, 0.1f));
            Transform head = Part("Head", visual, blob, shell, new Vector3(0f, 0.09f, 0.14f), Quaternion.identity, new Vector3(0.065f, 0.05f, 0.06f));
            Eye(head, new Vector3(-0.45f, 0.4f, 0.6f), 0.35f, eye, pupil);
            Eye(head, new Vector3(0.45f, 0.4f, 0.6f), 0.35f, eye, pupil);
            Segment("Fang L", visual, limb, legMat, new Vector3(-0.02f, 0.07f, 0.18f), new Vector3(-0.015f, 0.03f, 0.22f), 0.01f);
            Segment("Fang R", visual, limb, legMat, new Vector3(0.02f, 0.07f, 0.18f), new Vector3(0.015f, 0.03f, 0.22f), 0.01f);

            float[] wireAngles = { -50f, -20f, 10f, 40f, 75f };
            for (int i = 0; i < wireAngles.Length; i++)
                Part("Wire " + (i + 1), visual, wire, copper, new Vector3((i - 2) * 0.025f, 0.16f, -0.04f + (i % 2) * 0.03f),
                    Quaternion.Euler(0f, wireAngles[i] + 180f, (i - 2) * 12f), Vector3.one);

            var legs = new Transform[6];
            for (int i = 0; i < 6; i++)
            {
                float side = i < 3 ? -1f : 1f;
                float z = 0.08f - (i % 3) * 0.08f;
                Transform hip = Empty("Leg " + (i + 1), visual, new Vector3(0.09f * side, 0.1f, z));
                Segment("Upper", hip, limb, legMat, Vector3.zero, new Vector3(0.09f * side, 0.04f, (i % 3 - 1) * -0.03f), 0.012f);
                Segment("Lower", hip, limb, legMat, new Vector3(0.09f * side, 0.04f, (i % 3 - 1) * -0.03f), new Vector3(0.14f * side, -0.1f, (i % 3 - 1) * -0.05f), 0.01f);
                legs[i] = hip;
            }
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                renderer.shadowCastingMode = ShadowCastingMode.Off; // tiny, and they cling to the camera

            var collider = root.AddComponent<SphereCollider>();
            collider.center = new Vector3(0f, 0.12f, 0.02f);
            collider.radius = 0.2f;

            var brain = AddBrain<SparkTick>(root, 8f, 0.2f, 0.25f, 6.2f, 40f);
            root.GetComponent<NavMeshAgent>().autoBraking = false;
            var data = new SerializedObject(brain);
            data.FindProperty("visual").objectReferenceValue = visual;
            data.FindProperty("sparkMaterial").objectReferenceValue = spark;
            data.FindProperty("detectRange").floatValue = 16f;
            data.FindProperty("corpseTime").floatValue = 0.8f;
            SetArray(data.FindProperty("legs"), legs);
            data.ApplyModifiedPropertiesWithoutUndo();
            return SaveDesertPrefab(root, TickPath);
        }

        // ---------- Snorkel Eel ----------

        // A sand eel about 1.8 m long. The head is at the front (+Z), the body tapers back in four segments, and a
        // bent exhaust pipe rises from the top of the head: long enough to stick out of the sand when it is buried.
        internal static GameObject BuildEel()
        {
            GameObject root = NewRoot("Snorkel Eel", out Transform visual);
            Material skin = Lit("EelSkin", new Color(0.66f, 0.55f, 0.3f), 0f, 0.35f);
            Material belly = Lit("EelBelly", new Color(0.9f, 0.82f, 0.6f), 0f, 0.3f);
            Material spots = Lit("EelSpots", new Color(0.42f, 0.33f, 0.18f), 0f, 0.3f);
            Material mouth = Lit("EelMouth", new Color(0.35f, 0.08f, 0.1f), 0f, 0.2f);
            Material eye = Lit("EelEye", new Color(0.98f, 0.93f, 0.4f), 0f, 0.7f);
            Material pupil = Lit("FodderPupil", new Color(0.03f, 0.03f, 0.04f), 0f, 0.8f);
            Material pipe = Lit("EelPipe", new Color(0.5f, 0.42f, 0.38f), 0.75f, 0.35f);
            Material rust = Lit("EelPipeRust", new Color(0.55f, 0.27f, 0.12f), 0.4f, 0.2f);
            Material sandLit = Lit("DesertSandMound", new Color(0.86f, 0.71f, 0.48f), 0f, 0.1f);
            Material sand = Unlit("DesertDust", new Color(0.88f, 0.75f, 0.54f));
            Mesh pipeMesh = SaveDesertMesh(LowPolyMesh.Tube("EelSnorkel", new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(0f, 0.45f, -0.02f), new Vector3(0f, 0.75f, -0.06f),
                new Vector3(0f, 0.92f, -0.16f), new Vector3(0f, 0.98f, -0.3f),
            }, 0.055f, 6));
            Mesh rim = SaveDesertMesh(LowPolyMesh.Lathe("EelSnorkelRim", new[]
            {
                new Vector2(0.05f, 0f), new Vector2(0.08f, 0f), new Vector2(0.08f, 0.05f), new Vector2(0.05f, 0.05f),
            }, 6));

            Transform head = Empty("Head", visual, new Vector3(0f, 0.35f, 0.45f));
            Part("Skull", head, blob, skin, Vector3.zero, Quaternion.identity, new Vector3(0.27f, 0.22f, 0.34f));
            Part("Spot", head, blob, spots, new Vector3(0f, 0.15f, -0.08f), Quaternion.identity, new Vector3(0.14f, 0.08f, 0.14f));
            Eye(head, new Vector3(-0.18f, 0.12f, 0.18f), 0.07f, eye, pupil);
            Eye(head, new Vector3(0.18f, 0.12f, 0.18f), 0.07f, eye, pupil);
            Part("Upper Lip", head, blob, mouth, new Vector3(0f, -0.06f, 0.26f), Quaternion.identity, new Vector3(0.18f, 0.04f, 0.1f));
            Transform jaw = Empty("Jaw", head, new Vector3(0f, -0.1f, 0.05f));
            Part("Lower Jaw", jaw, blob, belly, new Vector3(0f, -0.04f, 0.15f), Quaternion.identity, new Vector3(0.2f, 0.07f, 0.24f));
            for (int i = 0; i < 4; i++)
                Part("Tooth", jaw, limb, belly, new Vector3(-0.09f + i * 0.06f, 0.0f, 0.3f), Quaternion.Euler(-90f, 0f, 0f), new Vector3(0.015f, 0.015f, 0.06f));

            // The snorkel: a bent, rusty exhaust pipe. Its own collider and Health make it the weak spot.
            Transform snorkel = Empty("Snorkel", head, new Vector3(0f, 0.12f, -0.05f));
            Part("Pipe", snorkel, pipeMesh, pipe, Vector3.zero, Quaternion.identity, Vector3.one);
            Part("Rust Band", snorkel, rim, rust, new Vector3(0f, 0.3f, -0.01f), Quaternion.identity, Vector3.one);
            Part("Mouth Rim", snorkel, rim, rust, new Vector3(0f, 0.98f, -0.3f), Quaternion.Euler(-90f, 0f, 0f), Vector3.one);
            var hitbox = new GameObject("Snorkel Eel (snorkel)");
            hitbox.transform.SetParent(snorkel, false);
            var pipeCollider = hitbox.AddComponent<BoxCollider>();
            pipeCollider.center = new Vector3(0f, 0.55f, -0.1f);
            pipeCollider.size = new Vector3(0.24f, 1.05f, 0.42f);
            var pipeHealth = hitbox.AddComponent<Health>();
            var pipeData = new SerializedObject(pipeHealth);
            pipeData.FindProperty("maxHealth").floatValue = 100000f; // Damaged reports applied damage, capped at current HP
            pipeData.ApplyModifiedPropertiesWithoutUndo();

            var segments = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                float z = 0.12f - i * 0.36f;
                float scale = 1f - i * 0.17f;
                Transform segment = Empty("Segment " + (i + 1), visual, new Vector3(0f, 0.3f - i * 0.03f, z));
                Part("Coil", segment, blob, skin, Vector3.zero, Quaternion.identity, new Vector3(0.24f, 0.2f, 0.26f) * scale);
                Part("Belly", segment, blob, belly, new Vector3(0f, -0.08f * scale, 0.02f), Quaternion.identity, new Vector3(0.18f, 0.1f, 0.22f) * scale);
                if (i % 2 == 0) Part("Spot", segment, blob, spots, new Vector3(0.06f, 0.14f * scale, 0f), Quaternion.identity, new Vector3(0.08f, 0.05f, 0.1f) * scale);
                if (i == 3) Part("Tail Fin", segment, limb, spots, new Vector3(0f, 0f, -0.15f), Quaternion.Euler(0f, 180f, 0f), new Vector3(0.02f, 0.14f, 0.3f));
                segments[i] = segment;
            }

            // The moving sand mound; a root child so it stays on the ground while the body is buried.
            Transform wake = Part("Sand Wake", root.transform, blob, sandLit, new Vector3(0f, 0.02f, 0.3f), Quaternion.identity, new Vector3(0.65f, 0.16f, 0.95f));

            var body = root.AddComponent<CapsuleCollider>();
            body.direction = 2;
            body.center = new Vector3(0f, 0.32f, -0.15f);
            body.radius = 0.32f;
            body.height = 1.7f;

            var brain = AddBrain<SnorkelEel>(root, 50f, 0.45f, 0.5f, 4.4f, 20f);
            root.AddComponent<EnemySignalSource>(); // the scanner can track it while it is buried
            var data = new SerializedObject(brain);
            data.FindProperty("visual").objectReferenceValue = visual;
            data.FindProperty("pipeHealth").objectReferenceValue = pipeHealth;
            data.FindProperty("head").objectReferenceValue = head;
            data.FindProperty("jaw").objectReferenceValue = jaw;
            data.FindProperty("wake").objectReferenceValue = wake;
            data.FindProperty("bodyCollider").objectReferenceValue = body;
            data.FindProperty("sandMaterial").objectReferenceValue = sand;
            data.FindProperty("detectRange").floatValue = 18f;
            SetArray(data.FindProperty("segments"), segments);
            data.ApplyModifiedPropertiesWithoutUndo();
            return SaveDesertPrefab(root, EelPath);
        }

        // ---------- Jerry-Can Beetle ----------

        // A fat beetle about 0.9 m long with a red jerry can strapped across its back, spout dripping fuel.
        internal static GameObject BuildBeetle()
        {
            GameObject root = NewRoot("Jerry-Can Beetle", out Transform visual);
            Material carapace = Lit("BeetleShell", new Color(0.16f, 0.2f, 0.17f), 0.35f, 0.75f);
            Material underside = Lit("BeetleUnderside", new Color(0.3f, 0.24f, 0.18f), 0f, 0.3f);
            Material legMat = Lit("BeetleLeg", new Color(0.1f, 0.1f, 0.09f), 0.2f, 0.4f);
            Material canMat = Lit("JerryCanRed", new Color(0.78f, 0.12f, 0.08f), 0.55f, 0.45f);
            Material capMat = Lit("JerryCanCap", new Color(0.82f, 0.8f, 0.72f), 0.8f, 0.5f);
            Material fuel = Lit("BeetleFuelDrip", new Color(0.15f, 0.1f, 0.05f), 0.2f, 0.95f);
            Material strapMat = Lit("BeetleStrap", new Color(0.3f, 0.24f, 0.18f), 0.1f, 0.2f);
            Material eye = Lit("BeetleEye", new Color(0.95f, 0.85f, 0.3f), 0f, 0.8f);
            Material pupil = Lit("FodderPupil", new Color(0.03f, 0.03f, 0.04f), 0f, 0.8f);
            Material blast = Unlit("BeetleBlast", new Color(1f, 0.55f, 0.15f));
            Mesh handle = SaveDesertMesh(LowPolyMesh.Tube("JerryCanHandle", new[]
            {
                new Vector3(-0.1f, 0f, 0f), new Vector3(-0.08f, 0.06f, 0f), new Vector3(0.08f, 0.06f, 0f), new Vector3(0.1f, 0f, 0f),
            }, 0.018f, 4));
            var strapPath = new Vector3[13];
            for (int i = 0; i < strapPath.Length; i++)
            {
                float t = i * Mathf.PI * 2f / (strapPath.Length - 1);
                strapPath[i] = new Vector3(0.44f * Mathf.Cos(t), 0.35f + 0.3f * Mathf.Sin(t), 0f);
            }
            Mesh strap = SaveDesertMesh(LowPolyMesh.Tube("BeetleStrap", strapPath, 0.02f, 4));

            Part("Carapace", visual, blob, carapace, new Vector3(0f, 0.36f, -0.04f), Quaternion.identity, new Vector3(0.42f, 0.27f, 0.48f));
            Part("Underside", visual, blob, underside, new Vector3(0f, 0.24f, 0f), Quaternion.identity, new Vector3(0.36f, 0.14f, 0.42f));
            Part("Wing Seam", visual, box, legMat, new Vector3(0f, 0.6f, -0.05f), Quaternion.Euler(0f, 0f, 45f), new Vector3(0.01f, 0.01f, 0.4f));
            Transform head = Part("Head", visual, blob, carapace, new Vector3(0f, 0.3f, 0.45f), Quaternion.identity, new Vector3(0.2f, 0.15f, 0.17f));
            Eye(head, new Vector3(-0.55f, 0.35f, 0.55f), 0.32f, eye, pupil);
            Eye(head, new Vector3(0.55f, 0.35f, 0.55f), 0.32f, eye, pupil);
            Segment("Mandible L", visual, limb, legMat, new Vector3(-0.08f, 0.26f, 0.58f), new Vector3(-0.02f, 0.24f, 0.7f), 0.025f);
            Segment("Mandible R", visual, limb, legMat, new Vector3(0.08f, 0.26f, 0.58f), new Vector3(0.02f, 0.24f, 0.7f), 0.025f);
            Segment("Antenna L", visual, limb, legMat, new Vector3(-0.07f, 0.4f, 0.55f), new Vector3(-0.2f, 0.62f, 0.75f), 0.012f);
            Segment("Antenna R", visual, limb, legMat, new Vector3(0.07f, 0.4f, 0.55f), new Vector3(0.2f, 0.62f, 0.75f), 0.012f);

            // The jerry can, lying across the beetle's back, strapped on.
            Transform can = Empty("Jerry Can", visual, new Vector3(0f, 0.68f, -0.06f));
            var canRenderers = new System.Collections.Generic.List<Renderer>();
            Transform canBody = Part("Can Body", can, box, canMat, new Vector3(-0.2f, 0f, 0f), Quaternion.Euler(0f, 90f, 45f), new Vector3(0.17f, 0.17f, 0.4f));
            canRenderers.Add(canBody.GetComponent<Renderer>());
            canRenderers.Add(Part("Ridge", can, box, canMat, new Vector3(0f, 0.12f, 0f), Quaternion.Euler(0f, 90f, 45f), new Vector3(0.04f, 0.04f, 0.36f)).GetComponent<Renderer>());
            Part("Handle", can, handle, capMat, new Vector3(-0.05f, 0.12f, 0.08f), Quaternion.identity, Vector3.one);
            Segment("Spout", can, limb, capMat, new Vector3(0.18f, 0.08f, 0f), new Vector3(0.3f, 0.16f, 0f), 0.03f);
            Part("Cap", can, blob, capMat, new Vector3(0.3f, 0.16f, 0f), Quaternion.identity, Vector3.one * 0.035f);
            Part("Drip", can, blob, fuel, new Vector3(0.31f, 0.08f, 0f), Quaternion.identity, new Vector3(0.025f, 0.05f, 0.025f));
            Part("Strap Front", visual, strap, strapMat, new Vector3(0f, 0.02f, 0.06f), Quaternion.identity, Vector3.one);
            Part("Strap Back", visual, strap, strapMat, new Vector3(0f, 0.02f, -0.18f), Quaternion.identity, Vector3.one);

            var legs = new Transform[6];
            for (int i = 0; i < 6; i++)
            {
                float side = i < 3 ? -1f : 1f;
                float z = 0.2f - (i % 3) * 0.22f;
                Transform hip = Empty("Leg " + (i + 1), visual, new Vector3(0.3f * side, 0.25f, z));
                Segment("Upper", hip, limb, legMat, Vector3.zero, new Vector3(0.15f * side, 0.06f, (1 - i % 3) * 0.06f), 0.03f);
                Segment("Lower", hip, limb, legMat, new Vector3(0.15f * side, 0.06f, (1 - i % 3) * 0.06f), new Vector3(0.24f * side, -0.25f, (1 - i % 3) * 0.1f), 0.022f);
                legs[i] = hip;
            }

            var collider = root.AddComponent<CapsuleCollider>();
            collider.direction = 2;
            collider.center = new Vector3(0f, 0.42f, 0.05f);
            collider.radius = 0.42f;
            collider.height = 1.2f;

            var brain = AddBrain<JerryCanBeetle>(root, 30f, 0.45f, 0.8f, 2.2f, 10f);
            var data = new SerializedObject(brain);
            data.FindProperty("visual").objectReferenceValue = visual;
            data.FindProperty("can").objectReferenceValue = can;
            data.FindProperty("explosionMaterial").objectReferenceValue = blast;
            data.FindProperty("canColor").colorValue = new Color(0.78f, 0.12f, 0.08f);
            SerializedProperty renderers = data.FindProperty("canRenderers");
            renderers.arraySize = canRenderers.Count;
            for (int i = 0; i < canRenderers.Count; i++) renderers.GetArrayElementAtIndex(i).objectReferenceValue = canRenderers[i];
            SetArray(data.FindProperty("legs"), legs);
            data.ApplyModifiedPropertiesWithoutUndo();
            return SaveDesertPrefab(root, BeetlePath);
        }

        // ---------- Hubcap Armadillo ----------

        // An armadillo about 1.1 m long. Its banded shell is a ball-ish shape that spins when it rolls, with a chrome
        // ship hubcap fused into each flank so it rolls like a wheel. Head, tail and legs tuck away when it curls.
        internal static GameObject BuildArmadillo()
        {
            GameObject root = NewRoot("Hubcap Armadillo", out Transform visual);
            Material shellMat = Lit("ArmadilloShell", new Color(0.6f, 0.47f, 0.34f), 0.1f, 0.4f);
            Material bandMat = Lit("ArmadilloBand", new Color(0.42f, 0.31f, 0.22f), 0.1f, 0.35f);
            Material skin = Lit("ArmadilloSkin", new Color(0.86f, 0.64f, 0.55f), 0f, 0.3f);
            Material chrome = Lit("HubcapChrome", new Color(0.82f, 0.84f, 0.88f), 0.95f, 0.85f);
            Material rivet = Lit("HubcapRivet", new Color(0.35f, 0.35f, 0.38f), 0.9f, 0.5f);
            Material eye = Lit("ArmadilloEye", new Color(0.08f, 0.06f, 0.05f), 0f, 0.9f);
            Material spark = Unlit("ArmadilloSpark", new Color(1f, 0.9f, 0.5f));
            Material dust = Unlit("DesertDust", new Color(0.88f, 0.75f, 0.54f));
            Mesh hubcap = SaveDesertMesh(LowPolyMesh.Lathe("Hubcap", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.27f, 0f), new Vector2(0.29f, 0.02f), new Vector2(0.27f, 0.045f),
                new Vector2(0.17f, 0.06f), new Vector2(0.1f, 0.085f), new Vector2(0.05f, 0.1f), new Vector2(0f, 0.1f),
            }, 10));

            Transform shell = Empty("Shell", visual, new Vector3(0f, 0.45f, 0f));
            Part("Dome", shell, blob, shellMat, Vector3.zero, Quaternion.identity, new Vector3(0.44f, 0.42f, 0.5f));
            for (int i = 0; i < 4; i++)
            {
                float a = (i - 1.5f) * 28f;
                Part("Band " + (i + 1), shell, blob, bandMat, Quaternion.Euler(a, 0f, 0f) * new Vector3(0f, 0.39f, 0f),
                    Quaternion.Euler(a, 0f, 0f), new Vector3(0.42f, 0.05f, 0.09f));
            }
            for (int side = 0; side < 2; side++)
            {
                float s = side == 0 ? -1f : 1f;
                Transform cap = Part(side == 0 ? "Hubcap L" : "Hubcap R", shell, hubcap, chrome, new Vector3(0.4f * s, 0f, 0f),
                    Quaternion.Euler(0f, 0f, -90f * s), Vector3.one);
                for (int r = 0; r < 5; r++)
                {
                    float a = r * Mathf.PI * 2f / 5f;
                    Part("Rivet", cap, gem, rivet, new Vector3(Mathf.Cos(a) * 0.21f, 0.05f, Mathf.Sin(a) * 0.21f), Quaternion.identity, Vector3.one * 0.025f);
                }
            }

            Transform head = Empty("Head", visual, new Vector3(0f, 0.32f, 0.45f));
            Segment("Snout", head, limb, skin, Vector3.zero, new Vector3(0f, -0.06f, 0.32f), 0.11f);
            Part("Nose", head, blob, eye, new Vector3(0f, -0.06f, 0.33f), Quaternion.identity, Vector3.one * 0.035f);
            Part("Ear L", head, limb, skin, new Vector3(-0.07f, 0.1f, 0.03f), Quaternion.Euler(-70f, -20f, 0f), new Vector3(0.04f, 0.02f, 0.14f));
            Part("Ear R", head, limb, skin, new Vector3(0.07f, 0.1f, 0.03f), Quaternion.Euler(-70f, 20f, 0f), new Vector3(0.04f, 0.02f, 0.14f));
            Part("Eye L", head, gem, eye, new Vector3(-0.08f, 0.04f, 0.12f), Quaternion.identity, Vector3.one * 0.025f);
            Part("Eye R", head, gem, eye, new Vector3(0.08f, 0.04f, 0.12f), Quaternion.identity, Vector3.one * 0.025f);

            Transform tail = Empty("Tail", visual, new Vector3(0f, 0.3f, -0.45f));
            Segment("Tail Cone", tail, limb, bandMat, Vector3.zero, new Vector3(0f, -0.2f, -0.35f), 0.07f);

            var legs = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                float s = i % 2 == 0 ? -1f : 1f;
                float z = i < 2 ? 0.25f : -0.25f;
                Transform hip = Empty("Leg " + (i + 1), visual, new Vector3(0.24f * s, 0.25f, z));
                Segment("Leg", hip, limb, skin, Vector3.zero, new Vector3(0.04f * s, -0.24f, 0.03f), 0.06f);
                Part("Claw", hip, gem, eye, new Vector3(0.04f * s, -0.24f, 0.07f), Quaternion.identity, new Vector3(0.05f, 0.02f, 0.05f));
                legs[i] = hip;
            }

            var collider = root.AddComponent<SphereCollider>();
            collider.center = new Vector3(0f, 0.45f, 0f);
            collider.radius = 0.5f;

            var brain = AddBrain<HubcapArmadillo>(root, 70f, 0.5f, 0.9f, 3f, 20f);
            var data = new SerializedObject(brain);
            data.FindProperty("visual").objectReferenceValue = visual;
            data.FindProperty("shell").objectReferenceValue = shell;
            data.FindProperty("sparkMaterial").objectReferenceValue = spark;
            data.FindProperty("dustMaterial").objectReferenceValue = dust;
            data.FindProperty("detectRange").floatValue = 18f;
            SetArray(data.FindProperty("limbs"), new[] { head, tail, legs[0], legs[1], legs[2], legs[3] });
            SetArray(data.FindProperty("legs"), legs);
            data.ApplyModifiedPropertiesWithoutUndo();
            return SaveDesertPrefab(root, ArmadilloPath);
        }

        // ---------- Dish Meerkat ----------

        // A meerkat about 1.15 m tall standing up, with a tiny radar dish on an antenna on its head, sitting in a
        // dirt-ringed burrow. The burrow is a root child so it stays on the ground while the meerkat ducks in.
        internal static GameObject BuildMeerkat()
        {
            GameObject root = NewRoot("Dish Meerkat", out Transform visual);
            Material fur = Lit("MeerkatFur", new Color(0.8f, 0.64f, 0.44f), 0f, 0.3f);
            Material belly = Lit("MeerkatBelly", new Color(0.94f, 0.85f, 0.68f), 0f, 0.3f);
            Material mask = Lit("MeerkatMask", new Color(0.3f, 0.2f, 0.14f), 0f, 0.4f);
            Material eye = Lit("MeerkatEye", new Color(0.05f, 0.04f, 0.04f), 0f, 0.95f);
            Material dishMat = Lit("RadarDish", new Color(0.86f, 0.88f, 0.9f), 0.85f, 0.7f);
            Material mast = Lit("RadarMast", new Color(0.35f, 0.37f, 0.4f), 0.8f, 0.4f);
            Material blink = Lit("RadarLight", new Color(1f, 0.25f, 0.2f), 0f, 0.9f);
            Material dirt = Lit("BurrowDirt", new Color(0.6f, 0.44f, 0.3f), 0f, 0.1f);
            Material hole = Lit("BurrowHole", new Color(0.12f, 0.08f, 0.05f), 0f, 0f);
            Material shriek = Unlit("MeerkatShriek", new Color(1f, 0.35f, 0.25f));
            Material dust = Unlit("DesertDust", new Color(0.88f, 0.75f, 0.54f));
            Mesh dish = SaveDesertMesh(LowPolyMesh.Lathe("RadarDishBowl", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.16f, 0.06f), new Vector2(0.175f, 0.075f), new Vector2(0.16f, 0.08f), new Vector2(0f, 0.02f),
            }, 10));
            Mesh ring = SaveDesertMesh(LowPolyMesh.Lathe("BurrowRing", new[]
            {
                new Vector2(0.24f, 0f), new Vector2(0.5f, 0f), new Vector2(0.44f, 0.07f), new Vector2(0.3f, 0.09f), new Vector2(0.24f, 0.02f),
            }, 9));

            Part("Body", visual, blob, fur, new Vector3(0f, 0.48f, 0f), Quaternion.identity, new Vector3(0.19f, 0.36f, 0.17f));
            Part("Belly", visual, blob, belly, new Vector3(0f, 0.46f, 0.07f), Quaternion.identity, new Vector3(0.14f, 0.3f, 0.12f));
            Part("Haunches", visual, blob, fur, new Vector3(0f, 0.16f, -0.04f), Quaternion.identity, new Vector3(0.21f, 0.14f, 0.2f));
            Segment("Arm L", visual, limb, fur, new Vector3(-0.12f, 0.66f, 0.08f), new Vector3(-0.07f, 0.52f, 0.17f), 0.035f);
            Segment("Arm R", visual, limb, fur, new Vector3(0.12f, 0.66f, 0.08f), new Vector3(0.07f, 0.52f, 0.17f), 0.035f);
            Segment("Tail", visual, limb, fur, new Vector3(0f, 0.08f, -0.16f), new Vector3(0f, 0.02f, -0.6f), 0.05f);

            Transform head = Empty("Head", visual, new Vector3(0f, 0.92f, 0.02f));
            Part("Skull", head, blob, fur, Vector3.zero, Quaternion.identity, new Vector3(0.13f, 0.12f, 0.14f));
            Segment("Snout", head, limb, belly, new Vector3(0f, -0.02f, 0.08f), new Vector3(0f, -0.04f, 0.2f), 0.06f);
            Part("Nose", head, gem, eye, new Vector3(0f, -0.04f, 0.2f), Quaternion.identity, Vector3.one * 0.02f);
            Part("Mask L", head, blob, mask, new Vector3(-0.06f, 0.03f, 0.08f), Quaternion.identity, new Vector3(0.045f, 0.035f, 0.03f));
            Part("Mask R", head, blob, mask, new Vector3(0.06f, 0.03f, 0.08f), Quaternion.identity, new Vector3(0.045f, 0.035f, 0.03f));
            Part("Eye L", head, gem, eye, new Vector3(-0.06f, 0.035f, 0.1f), Quaternion.identity, Vector3.one * 0.02f);
            Part("Eye R", head, gem, eye, new Vector3(0.06f, 0.035f, 0.1f), Quaternion.identity, Vector3.one * 0.02f);
            Part("Ear L", head, blob, mask, new Vector3(-0.11f, 0.04f, -0.02f), Quaternion.identity, Vector3.one * 0.03f);
            Part("Ear R", head, blob, mask, new Vector3(0.11f, 0.04f, -0.02f), Quaternion.identity, Vector3.one * 0.03f);

            // Antenna mast and the spinning radar dish.
            Segment("Mast", head, limb, mast, new Vector3(0f, 0.1f, -0.02f), new Vector3(0f, 0.32f, -0.02f), 0.015f);
            Transform dishPivot = Empty("Dish Pivot", head, new Vector3(0f, 0.32f, -0.02f));
            Transform bowl = Part("Dish", dishPivot, dish, dishMat, new Vector3(0f, 0f, 0.03f), Quaternion.Euler(-60f, 0f, 0f), Vector3.one);
            Segment("Feed Horn", bowl, limb, mast, Vector3.zero, new Vector3(0f, 0.12f, 0f), 0.01f);
            Part("Blinker", dishPivot, gem, blink, new Vector3(0f, 0.02f, -0.03f), Quaternion.identity, Vector3.one * 0.022f);

            Transform burrow = Empty("Burrow", root.transform, Vector3.zero);
            Part("Dirt Ring", burrow, ring, dirt, Vector3.zero, Quaternion.identity, Vector3.one);
            Part("Hole", burrow, LowPolyMeshDisc(), hole, new Vector3(0f, 0.012f, 0f), Quaternion.identity, Vector3.one);

            var collider = root.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.62f, 0f);
            collider.radius = 0.24f;
            collider.height = 1.25f;

            var brain = AddBrain<DishMeerkat>(root, 20f, 0.3f, 1.2f, 0f, 8f);
            var data = new SerializedObject(brain);
            data.FindProperty("visual").objectReferenceValue = visual;
            data.FindProperty("dish").objectReferenceValue = dishPivot;
            data.FindProperty("head").objectReferenceValue = head;
            data.FindProperty("burrow").objectReferenceValue = burrow;
            data.FindProperty("shriekMaterial").objectReferenceValue = shriek;
            data.FindProperty("dustMaterial").objectReferenceValue = dust;
            data.FindProperty("hideDepth").floatValue = 1.3f;
            data.FindProperty("hitKnockback").floatValue = 0f;
            data.ApplyModifiedPropertiesWithoutUndo();
            return SaveDesertPrefab(root, MeerkatPath);
        }

        private static Mesh LowPolyMeshDisc() => SaveDesertMesh(LowPolyMesh.Lathe("BurrowHole", new[]
        {
            new Vector2(0.27f, 0f), new Vector2(0f, 0f),
        }, 9));

        // ---------- Test scene ----------

        private static readonly Vector3 PlayerStart = new Vector3(0f, 0.1f, 0f);
        private const float StationRadius = 34f;

        private static void BuildTestArena()
        {
            Material sand = Lit("TestSand", new Color(0.86f, 0.71f, 0.48f), 0f, 0.1f);
            Material wall = Lit("TestSandstone", new Color(0.69f, 0.42f, 0.29f), 0f, 0.15f);
            Material rock = Lit("TestRock", new Color(0.55f, 0.36f, 0.27f), 0f, 0.15f);
            Material hull = Lit("TestHull", new Color(0.62f, 0.64f, 0.66f), 0.7f, 0.4f);

            var arena = new GameObject("Desert Fodder Arena");
            var surface = arena.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            arena.AddComponent<NavMeshBaker>();
            Transform a = arena.transform;

            Block("Sand Floor", new Vector3(0f, -0.5f, 0f), new Vector3(100f, 1f, 100f), sand, a);
            Block("North Wall", new Vector3(0f, 2f, 50f), new Vector3(101f, 4f, 1f), wall, a);
            Block("South Wall", new Vector3(0f, 2f, -50f), new Vector3(101f, 4f, 1f), wall, a);
            Block("East Wall", new Vector3(50f, 2f, 0f), new Vector3(1f, 4f, 100f), wall, a);
            Block("West Wall", new Vector3(-50f, 2f, 0f), new Vector3(1f, 4f, 100f), wall, a);

            // Rocks and hull plates for cover, and for the armadillo to bounce off. Fixed layout.
            var rng = new System.Random(18);
            for (int i = 0; i < 26; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float distance = 10f + (float)rng.NextDouble() * 34f;
                var p = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
                if (InStation(p, 6f)) continue; // keep station centres open
                bool plate = i % 3 == 0;
                Vector3 size = plate
                    ? new Vector3(2.5f + (float)rng.NextDouble() * 2f, 1.6f, 0.25f)
                    : new Vector3(1.2f + (float)rng.NextDouble() * 2f, 0.8f + (float)rng.NextDouble() * 1.6f, 1.2f + (float)rng.NextDouble() * 2f);
                GameObject block = Block(plate ? "Hull Plate" : "Rock", p + Vector3.up * size.y * 0.5f, size, plate ? hull : rock, a);
                block.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 180f, plate ? 8f : 0f);
            }
        }

        private static bool InStation(Vector3 point, float radius)
        {
            for (int i = 0; i < 5; i++)
                if (Vector3.Distance(point, StationCentre(i)) < radius) return true;
            return false;
        }

        private static Vector3 StationCentre(int index)
        {
            float angle = (90f - index * 72f) * Mathf.Deg2Rad; // north first, then clockwise
            return new Vector3(Mathf.Cos(angle) * StationRadius, 0f, Mathf.Sin(angle) * StationRadius);
        }

        private static void BuildStations(GameObject tick, GameObject eel, GameObject beetle, GameObject armadillo, GameObject meerkat)
        {
            var group = new GameObject("Fodder Stations").transform;
            Station(group, 0, "Station N - Spark Ticks", new Color(0.86f, 0.46f, 0.2f), 10, 6f,
                new Spawn(tick, 1f, 4, 6));
            Station(group, 1, "Station E - Snorkel Eels", new Color(0.66f, 0.55f, 0.3f), 3, 5f,
                new Spawn(eel, 1f, 1, 1));
            Station(group, 2, "Station SE - Jerry-Can Beetles", new Color(0.78f, 0.12f, 0.08f), 5, 4f,
                new Spawn(beetle, 1f, 1, 2));
            Station(group, 3, "Station SW - Hubcap Armadillos", new Color(0.82f, 0.84f, 0.88f), 2, 6f,
                new Spawn(armadillo, 1f, 1, 1));
            // Meerkats don't attack, so a few ticks come along to show what being spotted does.
            Station(group, 4, "Station W - Dish Meerkats (with ticks)", new Color(1f, 0.35f, 0.25f), 7, 4f,
                new Spawn(meerkat, 2f, 1, 1), new Spawn(tick, 1f, 3, 3));
        }

        private readonly struct Spawn
        {
            public readonly GameObject Prefab;
            public readonly float Weight;
            public readonly int Min, Max;
            public Spawn(GameObject prefab, float weight, int min, int max) { Prefab = prefab; Weight = weight; Min = min; Max = max; }
        }

        private static void Station(Transform parent, int index, string name, Color color, int maxAlive, float interval, params Spawn[] spawns)
        {
            Vector3 centre = StationCentre(index);
            var station = new GameObject(name);
            station.transform.SetParent(parent, false);
            station.transform.position = centre;

            // A coloured marker post so each station is easy to find (no collider, so it doesn't change the NavMesh).
            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Marker Post";
            Object.DestroyImmediate(post.GetComponent<Collider>());
            post.transform.SetParent(station.transform, false);
            post.transform.localPosition = new Vector3(0f, 2f, 0f);
            post.transform.localScale = new Vector3(0.3f, 2f, 0.3f);
            post.GetComponent<Renderer>().sharedMaterial = Lit("TestMarker" + index, color, 0.2f, 0.5f);

            var spawner = station.AddComponent<FodderSpawner>();
            var data = new SerializedObject(spawner);
            SerializedProperty entries = data.FindProperty("entries");
            entries.arraySize = spawns.Length;
            for (int i = 0; i < spawns.Length; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("prefab").objectReferenceValue = spawns[i].Prefab;
                entry.FindPropertyRelative("weight").floatValue = spawns[i].Weight;
                entry.FindPropertyRelative("minGroup").intValue = spawns[i].Min;
                entry.FindPropertyRelative("maxGroup").intValue = spawns[i].Max;
            }
            data.FindProperty("maxAlive").intValue = maxAlive;
            data.FindProperty("waveInterval").floatValue = interval;
            data.FindProperty("spawnRadiusMin").floatValue = 4f;
            data.FindProperty("spawnRadiusMax").floatValue = 10f;
            data.FindProperty("activationRange").floatValue = 17f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>The same player setup as the Desert Dunes level: laser, health, respawn, scanner, laser/scanner swap and HUD.</summary>
        internal static void BuildTestPlayer(GameObject playerPrefab, InputActionAsset input, Material beam, GameObject scannerRig, GameObject hudPrefab)
        {
            BuildPlayer(playerPrefab, input, beam);
            MiningLaser laser = Object.FindAnyObjectByType<MiningLaser>();
            GameObject player = laser.gameObject;
            player.name = "Player";
            player.transform.SetPositionAndRotation(PlayerStart, Quaternion.identity);

            var debugHud = player.GetComponent<CombatDebugHud>();
            if (debugHud != null) Object.DestroyImmediate(debugHud);

            Camera camera = player.GetComponentInChildren<Camera>();
            PrefabUtility.InstantiatePrefab(scannerRig, camera.transform);
            player.AddComponent<ScannerLaserSwap>();

            var hud = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab);
            var hudData = new SerializedObject(hud.GetComponent<PlayerHud>());
            hudData.FindProperty("playerHealth").objectReferenceValue = player.GetComponent<Health>();
            hudData.FindProperty("laser").objectReferenceValue = laser;
            hudData.FindProperty("viewpoint").objectReferenceValue = camera.transform;
            hudData.ApplyModifiedPropertiesWithoutUndo();
            Transform hudScanner = hud.transform.Find("Scanner");
            if (hudScanner != null) hudScanner.gameObject.SetActive(false); // the scanner device shows its own display (Q)
            Text objective = hud.transform.Find("Objective/Text")?.GetComponent<Text>();
            if (objective != null) objective.text = "Visit the five marker posts to test each fodder enemy";
        }

        internal static void BuildDesertLighting()
        {
            var light = new GameObject("Desert Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.9f, 0.74f);
            light.intensity = 1.5f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            light.gameObject.AddComponent<UniversalAdditionalLightData>();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.7f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.72f, 0.58f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.46f, 0.35f, 0.27f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.91f, 0.79f, 0.63f);
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 140f;
        }

        // ---------- Asset helpers (own folders, so no other feature's assets are touched) ----------

        internal static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        internal static void EnsureDesertFolders()
        {
            foreach (string folder in new[] { "Assets/Prefabs", "Assets/Prefabs/Enemies", Root, DesertMeshFolder,
                         "Assets/Materials", "Assets/Materials/Enemies", DesertMaterialFolder })
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                int slash = folder.LastIndexOf('/');
                AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
            }
        }

        /// <summary>Saves a mesh asset, replacing an existing one's contents so references and GUIDs survive.</summary>
        internal static Mesh SaveDesertMesh(Mesh mesh)
        {
            string path = DesertMeshFolder + "/" + mesh.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        internal static GameObject SaveDesertPrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            if (prefab == null) throw new InvalidOperationException("Could not save prefab " + path);
            return prefab;
        }

        /// <summary>A URP Lit material in the desert enemy folder; an existing one is reused unchanged.</summary>
        internal static Material Lit(string name, Color color, float metallic, float smoothness)
        {
            string path = DesertMaterialFolder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader missing.");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>A flat-colour URP Unlit material for effects (sparks, dust, blasts, the shriek).</summary>
        internal static Material Unlit(string name, Color color)
        {
            string path = DesertMaterialFolder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("URP Unlit shader missing.");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
