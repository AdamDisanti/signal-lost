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
using UnityEngine.SceneManagement;
using static SignalLost.Editor.EnemyTestBuilder;
using Object = UnityEngine.Object;

namespace SignalLost.Editor
{
    /// <summary>
    /// Generates the three desert mini-bosses (Burnout, Old Buzzard, Sludge) as low-poly placeholder prefabs and one
    /// test scene for each. Burnout wears the crash site's existing RocketEngine prefab (unpacked copy, the original is
    /// untouched) and Old Buzzard's wings reuse the crash site's bent ship-panel mesh and hull material.
    ///
    /// It only creates new files: prefabs in Assets/Prefabs/Enemies/Desert/MiniBosses, meshes and materials in the
    /// desert enemy folders, and scenes in Assets/Scenes (BurnoutTest, OldBuzzardTest, SludgeTest). It refuses to
    /// overwrite existing scenes. "Regenerate Prefabs" rebuilds the three prefabs in place (same GUIDs).
    /// </summary>
    public static class DesertMiniBossBuilder
    {
        private const string Root = "Assets/Prefabs/Enemies/Desert/MiniBosses";
        public const string BurnoutPath = Root + "/Burnout.prefab";
        public const string BuzzardPath = Root + "/OldBuzzard.prefab";
        public const string SludgePath = Root + "/Sludge.prefab";

        private const string RocketEnginePath = "Assets/Prefabs/Enviornment objects/TutorialCrashSite/Upgrades/RocketEngine.prefab";
        private const string ShipPanelMeshPath = "Assets/Prefabs/Enviornment objects/TutorialCrashSite/Meshes/BentShipPanel.asset";
        private const string HullMaterialPath = "Assets/Prefabs/Enviornment objects/TutorialCrashSite/Materials/DamagedRocketHull.mat";

        private enum Theme { MesaPass, WreckField, BuriedHull }

        private readonly struct BossTest
        {
            public readonly string Scene, PrefabPath, Label;
            public readonly Theme Theme;
            public BossTest(string scene, string prefab, string label, Theme theme) { Scene = scene; PrefabPath = prefab; Label = label; Theme = theme; }
        }

        private static readonly BossTest[] Tests =
        {
            new BossTest("Assets/Scenes/Bosses/BurnoutTest.unity", BurnoutPath, "Burnout", Theme.MesaPass),
            new BossTest("Assets/Scenes/Bosses/OldBuzzardTest.unity", BuzzardPath, "Old Buzzard", Theme.WreckField),
            new BossTest("Assets/Scenes/Bosses/SludgeTest.unity", SludgePath, "Sludge", Theme.BuriedHull),
        };

        // ---------- Menus ----------

        [MenuItem("Signal Lost/Desert Mini-Bosses/Create Test Scenes")]
        public static void CreateTestScenes()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            var beam = AssetDatabase.LoadAssetAtPath<Material>(BeamMaterialPath);
            var scannerRig = AssetDatabase.LoadAssetAtPath<GameObject>(DesertFodderBuilder.ScannerRigPath);
            var hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DesertFodderBuilder.HudPrefabPath);
            if (playerPrefab == null || input == null || beam == null || scannerRig == null || hudPrefab == null)
                throw new InvalidOperationException("Player, input actions, laser beam, ScannerRig or PlayerHUD prefab missing. Is your branch up to date with main?");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            DesertFodderBuilder.EnsureFodderPrefabs();
            EnsureFolder();
            if (DesertFodderBuilder.Load(BurnoutPath) == null) BuildBurnout();
            if (DesertFodderBuilder.Load(BuzzardPath) == null) BuildBuzzard();
            if (DesertFodderBuilder.Load(SludgePath) == null) BuildSludge();

            int created = 0;
            foreach (BossTest test in Tests)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(test.Scene) != null)
                {
                    Debug.Log(test.Scene + " already exists; skipped (edit it in Unity instead).");
                    continue;
                }
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                BuildArena(test.Theme);
                var boss = (GameObject)PrefabUtility.InstantiatePrefab(DesertFodderBuilder.Load(test.PrefabPath));
                boss.name = test.Label;
                boss.transform.SetPositionAndRotation(new Vector3(0f, 0f, 6f), Quaternion.Euler(0f, 180f, 0f));
                FodderSpawner spawner = BuildSpawner(test.Theme, boss.GetComponent<Health>());
                var bossData = new SerializedObject(boss.GetComponent<DesertMiniBoss>());
                bossData.FindProperty("fodderSpawner").objectReferenceValue = spawner;
                bossData.ApplyModifiedPropertiesWithoutUndo();

                DesertFodderBuilder.BuildTestPlayer(playerPrefab, input, beam, scannerRig, hudPrefab);
                GameObject player = Object.FindAnyObjectByType<MiningLaser>().gameObject;
                player.transform.SetPositionAndRotation(new Vector3(0f, 0.1f, -27f), Quaternion.identity);
                DesertFodderBuilder.SetObjective("Walk into the arena to fight " + test.Label);
                new GameObject("Fodder Status Overlay").AddComponent<FodderStatusOverlay>();
                new GameObject("Mini-Boss HUD").AddComponent<MiniBossHud>();
                DesertFodderBuilder.BuildDesertLighting();
                EditorSceneManager.SaveScene(scene, test.Scene);
                created++;
            }
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log($"DESERT_MINIBOSS_TEST_SCENES_CREATED_AND_VALIDATED ({created} new)");
        }

        [MenuItem("Signal Lost/Desert Mini-Bosses/Regenerate Prefabs")]
        public static void Regenerate()
        {
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Regenerate desert mini-boss prefabs",
                    "This rebuilds Burnout, Old Buzzard and Sludge from the builder script, replacing any edits made to the " +
                    "prefabs. Materials are kept. The three boss test scenes are then checked and their bosses re-linked " +
                    "to their fodder spawners if needed. Continue?", "Regenerate", "Cancel"))
                return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previousScene = SceneManager.GetActiveScene().path;
            DesertFodderBuilder.EnsureFodderPrefabs();
            EnsureFolder();
            BuildBurnout();
            BuildBuzzard();
            BuildSludge();
            AssetDatabase.SaveAssets();
            RepairTestScenes();
            if (!string.IsNullOrEmpty(previousScene) && AssetDatabase.LoadAssetAtPath<SceneAsset>(previousScene) != null)
                EditorSceneManager.OpenScene(previousScene);
            Debug.Log("DESERT_MINIBOSS_PREFABS_REGENERATED");
        }

        /// <summary>
        /// After regenerating, makes sure each existing boss test scene still has its boss at the start position and
        /// linked to the scene's fodder spawner (prefab overrides can come loose when a prefab is rebuilt from scratch).
        /// </summary>
        private static void RepairTestScenes()
        {
            foreach (BossTest test in Tests)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(test.Scene) == null) continue;
                Scene scene = EditorSceneManager.OpenScene(test.Scene);
                bool changed = false;
                List<DesertMiniBoss> bosses = All<DesertMiniBoss>();
                List<FodderSpawner> spawners = All<FodderSpawner>();
                if (bosses.Count == 0)
                {
                    var boss = (GameObject)PrefabUtility.InstantiatePrefab(DesertFodderBuilder.Load(test.PrefabPath), scene);
                    boss.name = test.Label;
                    bosses.Add(boss.GetComponent<DesertMiniBoss>());
                    changed = true;
                }
                DesertMiniBoss brain = bosses[0];
                Transform t = brain.transform;
                if ((t.position - new Vector3(0f, 0f, 6f)).sqrMagnitude > 0.01f || Quaternion.Angle(t.rotation, Quaternion.Euler(0f, 180f, 0f)) > 1f)
                {
                    t.SetPositionAndRotation(new Vector3(0f, 0f, 6f), Quaternion.Euler(0f, 180f, 0f));
                    changed = true;
                }
                var data = new SerializedObject(brain);
                SerializedProperty link = data.FindProperty("fodderSpawner");
                if (link.objectReferenceValue == null && spawners.Count > 0)
                {
                    link.objectReferenceValue = spawners[0];
                    data.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
                if (!changed) continue;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log(test.Scene + ": boss re-linked after regenerating.");
            }
        }

        /// <summary>
        /// The rigs gained joints (Old Buzzard's elbows, Sludge's body plates): saved prefabs made before that still work,
        /// but stiffly. Says so once per script reload, so nobody tests the old rigs by mistake.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void WarnIfPrefabsOutdated()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode) return;
                if (Outdated<Sludge>(SludgePath, "bodySegments") || Outdated<OldBuzzard>(BuzzardPath, "wingTips"))
                    Debug.LogWarning("The desert mini-boss prefabs were made by an older version of their builder (no body " +
                                     "plates / wing elbows yet). Run Signal Lost > Desert Mini-Bosses > Regenerate Prefabs to update them.");
            };
        }

        private static bool Outdated<T>(string path, string rigField) where T : DesertMiniBoss
        {
            GameObject prefab = DesertFodderBuilder.Load(path);
            T brain = prefab != null ? prefab.GetComponent<T>() : null;
            if (brain == null) return false;
            SerializedProperty property = new SerializedObject(brain).FindProperty(rigField);
            return property != null && property.isArray && property.arraySize == 0;
        }

        [MenuItem("Signal Lost/Desert Mini-Bosses/Validate")]
        public static void Validate()
        {
            ValidatePrefab<Burnout>(BurnoutPath, "body", "head", "tail", "legs", "thruster", "plume", "nozzleRenderers", "sparkMaterial", "smokeMaterial", "shipPart");
            ValidatePrefab<OldBuzzard>(BuzzardPath, "wings", "wingTips", "head", "tail", "wingHitboxes", "neck", "legs", "shadowMaterial", "warningMaterial", "scrapMaterial", "blastMaterial", "dustMaterial", "sparkMaterial");
            ValidatePrefab<Sludge>(SludgePath, "bodySegments", "tail", "hoseTip", "igniter", "claws", "clawElbows", "clawFingers", "legs", "legKnees",
                "mouthparts", "tank", "leak", "tankHealth", "oilMaterial", "warningMaterial", "sparkMaterial", "blastMaterial", "shipPart");

            foreach (BossTest test in Tests)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(test.Scene) == null) continue;
                EditorSceneManager.OpenScene(test.Scene);
                Require(Count<FirstPersonController>() == 1 && Count<MiningLaser>() == 1, test.Scene + ": expected one player with a laser.");
                Require(Count<ScannerController>() == 1 && Count<PlayerHud>() == 1, test.Scene + ": expected the scanner and HUD.");
                Require(Count<NavMeshSurface>() == 1 && Count<NavMeshBaker>() == 1, test.Scene + ": expected one NavMeshSurface with a NavMeshBaker.");
                Require(Count<Camera>() == 1 && Count<AudioListener>() == 1, test.Scene + ": expected one camera and listener.");
                List<DesertMiniBoss> bosses = All<DesertMiniBoss>();
                Require(bosses.Count == 1, test.Scene + ": expected one mini-boss.");
                var spawner = new SerializedObject(bosses[0]).FindProperty("fodderSpawner").objectReferenceValue as FodderSpawner;
                Require(spawner != null, test.Scene + ": the mini-boss has no fodder spawner.");
                Require(new SerializedObject(spawner).FindProperty("entries").arraySize > 0, test.Scene + ": the fodder spawner has no entries.");
                Require(Count<DesertFodder>() == 1, test.Scene + ": fodder should be spawned at runtime, not placed.");
                foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                    foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                        Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0, test.Scene + ": missing script on " + item.name);
            }
            Debug.Log("DESERT_MINIBOSSES_VALIDATED");
        }

        private static void ValidatePrefab<T>(string path, params string[] fields) where T : DesertMiniBoss
        {
            GameObject prefab = DesertFodderBuilder.Load(path);
            Require(prefab != null, "Missing prefab " + path);
            T brain = prefab.GetComponent<T>();
            Require(brain != null, path + " has no " + typeof(T).Name);
            Require(prefab.GetComponent<NavMeshAgent>() != null && prefab.GetComponent<Health>() != null, path + " needs a NavMeshAgent and Health.");
            Require(prefab.GetComponent<EnemySignalSource>() != null, path + " needs an EnemySignalSource for the scanner.");
            var data = new SerializedObject(brain);
            Require(data.FindProperty("visual").objectReferenceValue != null, path + " has no Visual reference.");
            foreach (string field in fields)
            {
                SerializedProperty property = data.FindProperty(field);
                Require(property != null, path + ": unknown field " + field);
                if (property.isArray)
                {
                    Require(property.arraySize > 0, path + ": " + field + " is empty.");
                    for (int i = 0; i < property.arraySize; i++)
                        Require(property.GetArrayElementAtIndex(i).objectReferenceValue != null, path + ": " + field + "[" + i + "] is empty.");
                }
                else Require(property.objectReferenceValue != null, path + ": " + field + " is not assigned.");
            }
        }

        // ---------- Shared modelling ----------

        private static Mesh blob, gem, limb, spike;
        private static Material pupil;

        private static GameObject NewRoot(string name, out Transform visual)
        {
            blob = DesertFodderBuilder.SaveDesertMesh(LowPolyMesh.Icosphere("DesertBlob", 1));
            gem = DesertFodderBuilder.SaveDesertMesh(LowPolyMesh.Icosphere("DesertGem", 0));
            limb = DesertFodderBuilder.SaveDesertMesh(LowPolyMesh.TaperedPrism("DesertLimb", 5, 0.6f));
            spike = DesertFodderBuilder.SaveDesertMesh(LowPolyMesh.TaperedPrism("DesertSpike", 5, 0f));
            pupil = DesertFodderBuilder.Lit("FodderPupil", new Color(0.03f, 0.03f, 0.04f), 0f, 0.8f);
            var root = new GameObject(name);
            visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            return root;
        }

        private static void Eye(Transform parent, Vector3 position, float size, Material eye)
        {
            Transform socket = Part("Eye", parent, gem, eye, position, Quaternion.identity, Vector3.one * size);
            Part("Pupil", socket, gem, pupil, new Vector3(0f, 0.1f, 0.75f), Quaternion.identity, Vector3.one * 0.55f);
        }

        /// <summary>A cone pointing up (+Y), leaning outward by <paramref name="lean"/> degrees toward +X.</summary>
        private static void Spike(string name, Transform parent, Material material, Vector3 position, float lean, float size, float length) =>
            Part(name, parent, spike, material, position, Quaternion.Euler(0f, 0f, -lean) * Quaternion.Euler(-90f, 0f, 0f),
                new Vector3(size, size, length));

        /// <summary>Adds the brain (which brings its NavMeshAgent and Health) and sets up the agent like the other enemies.</summary>
        private static T AddBrain<T>(GameObject root, float maxHealth, float radius, float height, float speed) where T : DesertMiniBoss
        {
            T brain = root.AddComponent<T>();
            var agent = root.GetComponent<NavMeshAgent>();
            agent.enabled = false; // switched on in Start, once the NavMesh has been built
            agent.radius = radius;
            agent.height = height;
            agent.speed = speed;
            agent.acceleration = 30f;
            agent.angularSpeed = 360f;
            agent.stoppingDistance = 0.3f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            var health = new SerializedObject(root.GetComponent<Health>());
            health.FindProperty("maxHealth").floatValue = maxHealth;
            health.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<EnemySignalSource>(); // the scanner can find it
            return brain;
        }

        /// <summary>The shared mini-boss settings on the serialized brain.</summary>
        private static void BossBasics(SerializedObject data, Transform visual, string displayName, string partName)
        {
            data.FindProperty("visual").objectReferenceValue = visual;
            data.FindProperty("displayName").stringValue = displayName;
            data.FindProperty("partName").stringValue = partName;
            data.FindProperty("hitKnockback").floatValue = 0f;
            data.FindProperty("corpseTime").floatValue = 6f;
            data.FindProperty("flashTime").floatValue = 0.05f;
        }

        /// <summary>The look of a boss's fire (the FireEffect particles): a colour tint and how smoky it is.</summary>
        private static void SetFire(SerializedObject data, string field, Color tint, float smoke, float fierceness)
        {
            SerializedProperty fire = data.FindProperty(field);
            fire.FindPropertyRelative("tint").colorValue = tint;
            fire.FindPropertyRelative("smoke").floatValue = smoke;
            fire.FindPropertyRelative("fierceness").floatValue = fierceness;
        }

        private static Health HitboxHealth(GameObject holder)
        {
            var health = holder.AddComponent<Health>();
            var data = new SerializedObject(health);
            // Never runs out (restored after every hit). Large because Damaged reports damage capped at current HP.
            data.FindProperty("maxHealth").floatValue = 100000f;
            data.ApplyModifiedPropertiesWithoutUndo();
            return health;
        }

        private static Material Flame() => DesertFodderBuilder.Unlit("BossFlame", new Color(1f, 0.55f, 0.12f));
        private static Material Spark() => DesertFodderBuilder.Unlit("BossSpark", new Color(1f, 0.9f, 0.5f));
        private static Material Smoke() => DesertFodderBuilder.Unlit("BossSmoke", new Color(0.26f, 0.25f, 0.24f));
        private static Material Dust() => DesertFodderBuilder.Unlit("DesertDust", new Color(0.88f, 0.75f, 0.54f));
        private static Material Warning() => DesertFodderBuilder.Unlit("BossWarning", new Color(1f, 0.3f, 0.15f));
        private static Material Blast() => DesertFodderBuilder.Unlit("BeetleBlast", new Color(1f, 0.55f, 0.15f));

        // ---------- Burnout ----------

        // A thorny-devil lizard about 3.2 m long including the tail, facing +Z, with the crash site's rocket engine
        // strapped along its spine, nozzle pointing backwards, and an exhaust plume behind it.
        private static GameObject BuildBurnout()
        {
            GameObject root = NewRoot("Burnout", out Transform visual);
            Material skin = DesertFodderBuilder.Lit("BurnoutSkin", new Color(0.78f, 0.45f, 0.22f), 0f, 0.35f);
            Material spikes = DesertFodderBuilder.Lit("BurnoutSpikes", new Color(0.45f, 0.22f, 0.12f), 0f, 0.4f);
            Material belly = DesertFodderBuilder.Lit("BurnoutBelly", new Color(0.93f, 0.75f, 0.5f), 0f, 0.3f);
            Material eye = DesertFodderBuilder.Lit("BurnoutEye", new Color(1f, 0.85f, 0.2f), 0f, 0.8f);
            Material strapMat = DesertFodderBuilder.Lit("BurnoutStrap", new Color(0.25f, 0.2f, 0.16f), 0.1f, 0.2f);
            Material flame = Flame();

            Transform body = Empty("Body", visual, new Vector3(0f, 0.62f, 0f));
            Part("Torso", body, blob, skin, Vector3.zero, Quaternion.identity, new Vector3(0.72f, 0.42f, 1.05f));
            Part("Belly", body, blob, belly, new Vector3(0f, -0.16f, 0.05f), Quaternion.identity, new Vector3(0.6f, 0.25f, 0.9f));
            foreach (float x in new[] { -0.4f, 0f, 0.4f })
                foreach (float z in new[] { -0.8f, -0.45f, -0.1f, 0.25f, 0.6f })
                {
                    if (Mathf.Approximately(x, 0f) && z > -0.6f && z < 0.4f) continue; // the engine sits there
                    float y = 0.42f * Mathf.Sqrt(Mathf.Max(0.05f, 1f - (x / 0.72f) * (x / 0.72f) - (z / 1.05f) * (z / 1.05f))) - 0.03f;
                    Spike("Spike", body, spikes, new Vector3(x, y, z), x * 70f, 0.07f, 0.26f);
                }

            Transform head = Empty("Head", body, new Vector3(0f, 0.05f, 1f));
            Part("Skull", head, blob, skin, new Vector3(0f, 0f, 0.15f), Quaternion.identity, new Vector3(0.42f, 0.3f, 0.48f));
            Part("Snout", head, blob, belly, new Vector3(0f, -0.06f, 0.45f), Quaternion.identity, new Vector3(0.3f, 0.17f, 0.28f));
            Eye(head, new Vector3(-0.2f, 0.1f, 0.32f), 0.09f, eye);
            Eye(head, new Vector3(0.2f, 0.1f, 0.32f), 0.09f, eye);
            Part("Horn L", head, spike, spikes, new Vector3(-0.17f, 0.2f, 0.1f), Quaternion.Euler(-135f, -15f, 0f), new Vector3(0.06f, 0.06f, 0.32f));
            Part("Horn R", head, spike, spikes, new Vector3(0.17f, 0.2f, 0.1f), Quaternion.Euler(-135f, 15f, 0f), new Vector3(0.06f, 0.06f, 0.32f));

            var legs = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                float s = i % 2 == 0 ? -1f : 1f;
                float z = i < 2 ? 0.5f : -0.5f;
                Transform hip = Empty("Leg " + (i + 1), visual, new Vector3(0.55f * s, 0.5f, z));
                Segment("Upper", hip, limb, skin, Vector3.zero, new Vector3(0.35f * s, -0.15f, 0.05f), 0.11f);
                Segment("Lower", hip, limb, skin, new Vector3(0.35f * s, -0.15f, 0.05f), new Vector3(0.42f * s, -0.48f, 0.1f), 0.08f);
                Part("Foot", hip, blob, belly, new Vector3(0.42f * s, -0.48f, 0.16f), Quaternion.identity, new Vector3(0.13f, 0.05f, 0.19f));
                legs[i] = hip;
            }

            Transform tail1 = Empty("Tail 1", body, new Vector3(0f, -0.05f, -0.95f));
            Segment("Tail", tail1, limb, skin, Vector3.zero, new Vector3(0f, -0.1f, -0.6f), 0.22f);
            Transform tail2 = Empty("Tail 2", tail1, new Vector3(0f, -0.1f, -0.6f));
            Segment("Tail", tail2, limb, skin, Vector3.zero, new Vector3(0f, -0.06f, -0.55f), 0.14f);
            Transform tail3 = Empty("Tail 3", tail2, new Vector3(0f, -0.06f, -0.55f));
            Segment("Tail", tail3, limb, skin, Vector3.zero, new Vector3(0f, -0.02f, -0.5f), 0.08f);
            Spike("Tail Spike", tail1, spikes, new Vector3(0f, 0.15f, -0.25f), 0f, 0.06f, 0.2f);
            Spike("Tail Spike", tail2, spikes, new Vector3(0f, 0.09f, -0.25f), 0f, 0.05f, 0.16f);

            // The stolen rocket engine: an unpacked copy of the crash site's RocketEngine prefab.
            Transform mount = Empty("Rocket Thruster", body, new Vector3(0f, 0.42f, -0.05f));
            Transform thruster;
            var enginePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RocketEnginePath);
            if (enginePrefab != null)
            {
                var engine = (GameObject)PrefabUtility.InstantiatePrefab(enginePrefab);
                PrefabUtility.UnpackPrefabInstance(engine, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                engine.name = "Engine";
                thruster = engine.transform;
                thruster.SetParent(mount, false);
                thruster.localPosition = new Vector3(0f, 0.14f, 0.3f);
                thruster.localRotation = Quaternion.Euler(90f, 0f, 0f); // nozzle (-Y) points backwards (-Z)
                thruster.localScale = Vector3.one * 0.5f;
                foreach (Collider engineCollider in engine.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(engineCollider);
            }
            else
            {
                Debug.LogWarning("RocketEngine prefab not found; Burnout gets a stand-in nozzle.");
                Mesh bell = DesertFodderBuilder.SaveDesertMesh(LowPolyMesh.Lathe("BurnoutNozzle", new[]
                {
                    new Vector2(0.18f, 0f), new Vector2(0.24f, -0.15f), new Vector2(0.42f, -0.6f), new Vector2(0.38f, -0.6f), new Vector2(0.2f, -0.15f), new Vector2(0.14f, 0f),
                }, 10));
                thruster = Part("Engine", mount, bell, DesertFodderBuilder.Lit("BurnoutNozzle", new Color(0.4f, 0.4f, 0.42f), 0.8f, 0.4f),
                    new Vector3(0f, 0.14f, 0.3f), Quaternion.Euler(90f, 0f, 0f), Vector3.one);
            }
            Transform plume = Part("Exhaust Plume", mount, blob, flame, new Vector3(0f, 0.14f, -0.85f), Quaternion.identity, new Vector3(0.26f, 0.26f, 0.6f));
            plume.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var strapPath = new Vector3[13];
            for (int i = 0; i < strapPath.Length; i++)
            {
                float t = i * Mathf.PI * 2f / (strapPath.Length - 1);
                strapPath[i] = new Vector3(0.74f * Mathf.Cos(t), 0.44f * Mathf.Sin(t), 0f);
            }
            Mesh strap = DesertFodderBuilder.SaveDesertMesh(LowPolyMesh.Tube("BurnoutStrap", strapPath, 0.03f, 4));
            Part("Strap Front", body, strap, strapMat, new Vector3(0f, 0.02f, 0.18f), Quaternion.identity, Vector3.one);
            Part("Strap Back", body, strap, strapMat, new Vector3(0f, 0.02f, -0.3f), Quaternion.identity, Vector3.one);

            var collider = root.AddComponent<CapsuleCollider>();
            collider.direction = 2;
            collider.center = new Vector3(0f, 0.62f, -0.15f);
            collider.radius = 0.6f;
            collider.height = 2.6f;

            var brain = AddBrain<Burnout>(root, 420f, 0.85f, 1.3f, 3.6f);
            var data = new SerializedObject(brain);
            BossBasics(data, visual, "Burnout", "Rocket thruster");
            data.FindProperty("body").objectReferenceValue = body;
            data.FindProperty("head").objectReferenceValue = head;
            SetArray(data.FindProperty("tail"), new[] { tail1, tail2, tail3 });
            SetArray(data.FindProperty("legs"), legs);
            data.FindProperty("thruster").objectReferenceValue = thruster;
            data.FindProperty("plume").objectReferenceValue = plume;
            Renderer[] nozzle = thruster.GetComponentsInChildren<Renderer>(true);
            SerializedProperty renderers = data.FindProperty("nozzleRenderers");
            renderers.arraySize = nozzle.Length;
            for (int i = 0; i < nozzle.Length; i++) renderers.GetArrayElementAtIndex(i).objectReferenceValue = nozzle[i];
            data.FindProperty("sparkMaterial").objectReferenceValue = Spark();
            data.FindProperty("smokeMaterial").objectReferenceValue = Smoke();
            data.FindProperty("shipPart").objectReferenceValue = mount;
            SetFire(data, "fire", Color.white, 0.3f, 0.8f); // a streak of many small fires: keep each one light
            data.ApplyModifiedPropertiesWithoutUndo();
            return DesertFodderBuilder.SaveDesertPrefab(root, BurnoutPath);
        }

        // ---------- Old Buzzard ----------

        // A ragged vulture about 1.9 m tall with a 4.6 m wingspan, facing +Z. Each wing is a feathered arm with two of the
        // crash site's bent hull panels riveted over it, and its own hit box so the wings can be targeted.
        private static GameObject BuildBuzzard()
        {
            GameObject root = NewRoot("Old Buzzard", out Transform visual);
            Material feathers = DesertFodderBuilder.Lit("BuzzardFeathers", new Color(0.22f, 0.17f, 0.14f), 0f, 0.25f);
            Material ruff = DesertFodderBuilder.Lit("BuzzardRuff", new Color(0.88f, 0.84f, 0.76f), 0f, 0.2f);
            Material skin = DesertFodderBuilder.Lit("BuzzardSkin", new Color(0.85f, 0.45f, 0.42f), 0f, 0.35f);
            Material beak = DesertFodderBuilder.Lit("BuzzardBeak", new Color(0.9f, 0.78f, 0.45f), 0f, 0.5f);
            Material eye = DesertFodderBuilder.Lit("BuzzardEye", new Color(0.95f, 0.75f, 0.2f), 0f, 0.8f);
            Material legMat = DesertFodderBuilder.Lit("BuzzardLeg", new Color(0.7f, 0.6f, 0.45f), 0f, 0.3f);
            Material rivet = DesertFodderBuilder.Lit("HubcapRivet", new Color(0.35f, 0.35f, 0.38f), 0.9f, 0.5f);
            Material hull = AssetDatabase.LoadAssetAtPath<Material>(HullMaterialPath) ??
                            DesertFodderBuilder.Lit("BuzzardHull", new Color(0.62f, 0.64f, 0.66f), 0.7f, 0.4f);
            Mesh panel = AssetDatabase.LoadAssetAtPath<Mesh>(ShipPanelMeshPath) ??
                         DesertFodderBuilder.SaveDesertMesh(LowPolyMesh.TaperedPrism("DesertBox", 4, 1f));

            Part("Body", visual, blob, feathers, new Vector3(0f, 1.05f, 0f), Quaternion.identity, new Vector3(0.55f, 0.62f, 0.75f));
            Part("Ruff", visual, blob, ruff, new Vector3(0f, 1.45f, 0.35f), Quaternion.identity, new Vector3(0.45f, 0.24f, 0.42f));
            Transform tail = Empty("Tail", visual, new Vector3(0f, 0.95f, -0.55f));
            for (int i = -1; i <= 1; i++)
                Segment("Tail Feather", tail, limb, feathers, new Vector3(i * 0.1f, 0f, -0.05f), new Vector3(i * 0.22f, -0.23f, -0.7f), 0.09f);

            Transform neck = Empty("Neck", visual, new Vector3(0f, 1.45f, 0.45f));
            Segment("Neck", neck, limb, skin, Vector3.zero, new Vector3(0f, 0.35f, 0.22f), 0.09f);
            Transform head = Empty("Head", neck, new Vector3(0f, 0.38f, 0.25f));
            Part("Skull", head, blob, skin, Vector3.zero, Quaternion.identity, new Vector3(0.19f, 0.17f, 0.23f));
            Segment("Beak", head, limb, beak, new Vector3(0f, -0.02f, 0.18f), new Vector3(0f, -0.1f, 0.42f), 0.06f);
            Part("Beak Hook", head, spike, beak, new Vector3(0f, -0.1f, 0.42f), Quaternion.Euler(150f, 0f, 0f), new Vector3(0.04f, 0.04f, 0.1f));
            Eye(head, new Vector3(-0.12f, 0.05f, 0.1f), 0.06f, eye);
            Eye(head, new Vector3(0.12f, 0.05f, 0.1f), 0.06f, eye);

            var legs = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                Transform hip = Empty(i == 0 ? "Leg L" : "Leg R", visual, new Vector3(0.22f * s, 0.78f, 0.05f));
                Part("Thigh", hip, blob, feathers, Vector3.zero, Quaternion.identity, new Vector3(0.16f, 0.2f, 0.18f));
                Segment("Shank", hip, limb, legMat, new Vector3(0.03f * s, -0.2f, 0.02f), new Vector3(0.04f * s, -0.76f, 0.08f), 0.05f);
                for (int t = -1; t <= 1; t++)
                    Part("Toe", hip, spike, legMat, new Vector3(0.04f * s, -0.76f, 0.1f), Quaternion.Euler(80f, t * 30f, 0f), new Vector3(0.03f, 0.03f, 0.22f));
                legs[i] = hip;
            }

            var wings = new Transform[2];
            var tips = new Transform[2];
            var hitboxes = new Health[2];
            for (int side = 0; side < 2; side++)
            {
                float s = side == 0 ? -1f : 1f;
                Transform wing = Empty(side == 0 ? "Wing L" : "Wing R", visual, new Vector3(0.5f * s, 1.25f, 0f));
                // The outer half hangs off an elbow joint so the wing can fold, trail and scoop.
                Vector3 elbowAt = new Vector3(1.1f * s, 0.12f, -0.05f);
                Segment("Arm", wing, limb, feathers, Vector3.zero, elbowAt, 0.09f);
                Transform elbow = Empty(side == 0 ? "Elbow L" : "Elbow R", wing, elbowAt);
                Segment("Forearm", elbow, limb, feathers, Vector3.zero, new Vector3(2.1f * s, 0.05f, -0.25f) - elbowAt, 0.06f);
                foreach (float x in new[] { 0.6f, 1f, 1.4f, 1.8f, 2.2f })
                {
                    Vector3 from = new Vector3(x * s, 0.05f, -0.15f), to = new Vector3(x * s * 1.05f, 0f, -0.8f);
                    if (x < 1.1f) Segment("Feather", wing, limb, feathers, from, to, 0.07f);
                    else Segment("Feather", elbow, limb, feathers, from - elbowAt, to - elbowAt, 0.07f);
                }
                Transform p1 = Part("Ship Panel", wing, panel, hull, new Vector3(0.7f * s, 0.14f, -0.28f), Quaternion.Euler(0f, 0f, -5f * s), new Vector3(0.55f, 0.5f, 0.55f));
                Transform p2 = Part("Ship Panel", elbow, panel, hull, new Vector3(1.62f * s, 0.1f, -0.32f) - elbowAt, Quaternion.Euler(0f, 8f * s, -3f * s), new Vector3(0.5f, 0.5f, 0.5f));
                tips[side] = elbow;
                foreach (Transform p in new[] { p1, p2 })
                    for (int r = 0; r < 4; r++)
                        Part("Rivet", p, gem, rivet, new Vector3((r % 2 == 0 ? -0.8f : 0.8f), 0.32f, r < 2 ? -0.45f : 0.45f), Quaternion.identity, Vector3.one * 0.06f);

                var box = new GameObject(side == 0 ? "Old Buzzard (left wing)" : "Old Buzzard (right wing)");
                box.transform.SetParent(wing, false);
                var wingBox = box.AddComponent<BoxCollider>();
                wingBox.center = new Vector3(1.15f * s, 0.1f, -0.3f);
                wingBox.size = new Vector3(2.2f, 0.4f, 1.1f);
                hitboxes[side] = HitboxHealth(box);
                wings[side] = wing;
            }

            var body = root.AddComponent<CapsuleCollider>();
            body.center = new Vector3(0f, 1.05f, 0.05f);
            body.radius = 0.5f;
            body.height = 1.9f;

            var brain = AddBrain<OldBuzzard>(root, 380f, 0.7f, 1.8f, 2.6f);
            var data = new SerializedObject(brain);
            BossBasics(data, visual, "Old Buzzard", "Wing panels");
            data.FindProperty("patrolRadius").floatValue = 0f; // it circles in the sky instead of walking a route
            SetArray(data.FindProperty("wings"), wings);
            SetArray(data.FindProperty("wingTips"), tips);
            data.FindProperty("head").objectReferenceValue = head;
            data.FindProperty("tail").objectReferenceValue = tail;
            SerializedProperty boxes = data.FindProperty("wingHitboxes");
            boxes.arraySize = 2;
            for (int i = 0; i < 2; i++) boxes.GetArrayElementAtIndex(i).objectReferenceValue = hitboxes[i];
            data.FindProperty("neck").objectReferenceValue = neck;
            SetArray(data.FindProperty("legs"), legs);
            data.FindProperty("shadowMaterial").objectReferenceValue = DesertFodderBuilder.Unlit("BuzzardShadow", new Color(0.13f, 0.1f, 0.08f));
            data.FindProperty("warningMaterial").objectReferenceValue = Warning();
            data.FindProperty("scrapMaterial").objectReferenceValue = hull;
            data.FindProperty("blastMaterial").objectReferenceValue = Blast();
            data.FindProperty("dustMaterial").objectReferenceValue = Dust();
            data.FindProperty("sparkMaterial").objectReferenceValue = Spark();
            data.ApplyModifiedPropertiesWithoutUndo();
            return DesertFodderBuilder.SaveDesertPrefab(root, BuzzardPath);
        }

        // ---------- Sludge ----------

        // A giant scorpion about 2.8 m long, facing +Z. The body is a chain of five plates (the head plate, then four
        // back plates), each parented to the one in front so the body can bend and ripple. Two big pincers with an
        // elbow, eight two-jointed legs in a wide stance, a five-joint tail that curls up and over its back ending in a
        // fuel hose with a blue pilot-light igniter, and a yellow fuel tank strapped along its back (its own hit box:
        // the weak spot).
        private static GameObject BuildSludge()
        {
            GameObject root = NewRoot("Sludge", out Transform visual);
            Material shell = DesertFodderBuilder.Lit("SludgeShell", new Color(0.18f, 0.22f, 0.24f), 0.2f, 0.7f);
            Material under = DesertFodderBuilder.Lit("SludgeUnder", new Color(0.35f, 0.3f, 0.25f), 0f, 0.3f);
            Material joint = DesertFodderBuilder.Lit("SludgeJoint", new Color(0.45f, 0.15f, 0.1f), 0f, 0.4f);
            Material eye = DesertFodderBuilder.Lit("SludgeEye", new Color(0.9f, 0.2f, 0.1f), 0f, 0.9f);
            Material tankMat = DesertFodderBuilder.Lit("SludgeTank", new Color(0.85f, 0.78f, 0.2f), 0.6f, 0.5f);
            Material hazard = DesertFodderBuilder.Lit("SludgeHazard", new Color(0.08f, 0.08f, 0.08f), 0.2f, 0.4f);
            Material hose = DesertFodderBuilder.Lit("SludgeHose", new Color(0.15f, 0.15f, 0.15f), 0.1f, 0.3f);
            Material metal = DesertFodderBuilder.Lit("SludgeMetal", new Color(0.6f, 0.6f, 0.62f), 0.8f, 0.5f);
            Material pilot = DesertFodderBuilder.Unlit("SludgePilot", new Color(0.3f, 0.6f, 1f));
            Material oil = DesertFodderBuilder.Lit("SludgeOil", new Color(0.06f, 0.05f, 0.04f), 0.3f, 0.95f);

            // Body plates: pivot position (relative to the plate in front), plate size.
            Vector3[] pivots =
            {
                new Vector3(0f, 0.6f, 0.45f), new Vector3(0f, 0.01f, -0.42f), new Vector3(0f, 0f, -0.32f),
                new Vector3(0f, -0.01f, -0.3f), new Vector3(0f, -0.01f, -0.28f),
            };
            Vector3[] plates =
            {
                new Vector3(0.62f, 0.3f, 0.52f), new Vector3(0.6f, 0.3f, 0.26f), new Vector3(0.57f, 0.29f, 0.25f),
                new Vector3(0.5f, 0.26f, 0.23f), new Vector3(0.42f, 0.23f, 0.2f),
            };
            var segments = new Transform[pivots.Length];
            Transform parent = visual;
            for (int i = 0; i < pivots.Length; i++)
            {
                Transform plate = Empty(i == 0 ? "Head Plate" : "Back Plate " + i, parent, pivots[i]);
                Vector3 size = plates[i];
                Vector3 centre = i == 0 ? Vector3.zero : new Vector3(0f, 0f, -0.08f);
                Part("Carapace", plate, blob, shell, centre, Quaternion.identity, size);
                Part("Underside", plate, blob, under, centre + new Vector3(0f, -size.y * 0.55f, 0f), Quaternion.identity,
                    new Vector3(size.x * 0.82f, size.y * 0.45f, size.z * 0.88f));
                if (i > 0) Part("Seam", plate, blob, joint, new Vector3(0f, -0.02f, 0.05f), Quaternion.identity,
                    new Vector3(size.x * 0.9f, size.y * 0.8f, 0.07f));
                segments[i] = plate;
                parent = plate;
            }
            Transform head = segments[0];
            for (int i = 0; i < 3; i++)
                Eye(head, new Vector3((i - 1) * 0.12f, 0.27f, 0.42f - Mathf.Abs(i - 1) * 0.04f), 0.06f, eye);

            // Mouthparts that chew.
            var mouth = new Transform[2];
            for (int side = 0; side < 2; side++)
            {
                float s = side == 0 ? -1f : 1f;
                mouth[side] = Empty(side == 0 ? "Mouthpart L" : "Mouthpart R", head, new Vector3(0.09f * s, -0.08f, 0.48f));
                Segment("Fang", mouth[side], limb, joint, Vector3.zero, new Vector3(0.02f * s, -0.04f, 0.15f), 0.04f);
            }

            // Pincers: shoulder, arm, elbow, forearm, then the claw with one fixed and one moving finger.
            var claws = new Transform[2];
            var elbows = new Transform[2];
            var fingers = new Transform[2];
            for (int side = 0; side < 2; side++)
            {
                float s = side == 0 ? -1f : 1f;
                Transform shoulder = Empty(side == 0 ? "Claw L" : "Claw R", head, new Vector3(0.38f * s, -0.04f, 0.32f));
                Vector3 elbowAt = new Vector3(0.32f * s, 0.06f, 0.3f);
                Segment("Arm", shoulder, limb, shell, Vector3.zero, elbowAt, 0.12f);
                Part("Joint", shoulder, blob, joint, elbowAt, Quaternion.identity, Vector3.one * 0.11f);
                Transform elbow = Empty("Elbow", shoulder, elbowAt);
                Segment("Forearm", elbow, limb, shell, Vector3.zero, new Vector3(0.06f * s, 0.02f, 0.36f), 0.1f);
                Part("Palm", elbow, blob, shell, new Vector3(0.08f * s, 0.02f, 0.5f), Quaternion.identity, new Vector3(0.22f, 0.17f, 0.28f));
                Segment("Fixed Finger", elbow, limb, shell, new Vector3(0.1f * s, -0.04f, 0.68f), new Vector3(0.06f * s, -0.06f, 1.05f), 0.065f);
                Transform finger = Empty("Finger", elbow, new Vector3(0.1f * s, 0.07f, 0.68f));
                Segment("Moving Finger", finger, limb, shell, Vector3.zero, new Vector3(-0.04f * s, 0f, 0.36f), 0.065f);
                claws[side] = shoulder;
                elbows[side] = elbow;
                fingers[side] = finger;
            }

            // Legs: four a side on the first four plates, splayed from forward to back. Hip, knee, foot on the sand.
            var legs = new Transform[8];
            var knees = new Transform[8];
            int[] legPlate = { 0, 0, 1, 2 };
            float[] legZ = { 0.05f, -0.22f, -0.06f, -0.05f };
            float[] legYaw = { 38f, 12f, -14f, -38f };
            for (int i = 0; i < 8; i++)
            {
                bool left = i < 4;
                float s = left ? -1f : 1f;
                int pair = i % 4;
                Transform hip = Empty("Leg " + (i + 1), segments[legPlate[pair]], new Vector3(0.36f * s, -0.06f, legZ[pair]));
                hip.localRotation = Quaternion.Euler(0f, -s * legYaw[pair], 0f);
                Vector3 kneeAt = new Vector3(0.42f * s, 0.2f, 0f);
                Segment("Upper", hip, limb, shell, Vector3.zero, kneeAt, 0.06f);
                Transform knee = Empty("Knee", hip, kneeAt);
                Segment("Lower", knee, limb, joint, Vector3.zero, new Vector3(0.32f * s, -0.74f, 0f), 0.045f);
                legs[i] = hip;
                knees[i] = knee;
            }

            // Tail: five joints from the last plate, each turning a further 30 degrees up and over the back; +Z runs
            // along the tail. A fuel hose is clamped along its top, ending in a nozzle with the igniter.
            var tail = new Transform[5];
            Transform joint0 = Empty("Tail 1", segments[4], new Vector3(0f, 0.06f, -0.24f));
            joint0.localRotation = Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(-30f, 0f, 0f);
            parent = joint0;
            for (int i = 0; i < tail.Length; i++)
            {
                Transform t = i == 0 ? joint0 : Empty("Tail " + (i + 1), parent, new Vector3(0f, 0f, 0.36f));
                if (i > 0) t.localRotation = Quaternion.Euler(-30f, 0f, 0f);
                float size = 0.2f - i * 0.02f;
                Part("Segment", t, blob, shell, new Vector3(0f, 0f, 0.18f), Quaternion.identity, new Vector3(size, size * 0.9f, 0.22f));
                Segment("Hose", t, limb, hose, new Vector3(0f, size * 0.85f, 0.02f), new Vector3(0f, size * 0.8f, 0.34f), 0.035f);
                tail[i] = t;
                parent = t;
            }
            Transform last = tail[tail.Length - 1];
            Transform tip = Empty("Hose Tip", last, new Vector3(0f, 0f, 0.56f));
            Segment("Hose Nozzle", last, limb, metal, new Vector3(0f, 0f, 0.3f), new Vector3(0f, 0f, 0.52f), 0.06f);
            Transform igniter = Part("Igniter", tip, gem, pilot, new Vector3(0f, 0.06f, 0f), Quaternion.identity, Vector3.one * 0.06f);
            igniter.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            // The fuel tank, lying along its back on the first back plate (lathe Y turned to Z), held on by two straps.
            Mesh tankMesh = DesertFodderBuilder.SaveDesertMesh(LowPolyMesh.Lathe("SludgeTankShell", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.22f, 0f), new Vector2(0.3f, 0.08f), new Vector2(0.32f, 0.2f),
                new Vector2(0.32f, 0.8f), new Vector2(0.3f, 0.92f), new Vector2(0.22f, 1f), new Vector2(0f, 1f),
            }, 10));
            Mesh band = DesertFodderBuilder.SaveDesertMesh(LowPolyMesh.Lathe("SludgeTankBand", new[]
            {
                new Vector2(0.325f, 0f), new Vector2(0.34f, 0f), new Vector2(0.34f, 0.07f), new Vector2(0.325f, 0.07f),
            }, 10));
            Transform tank = Empty("Fuel Tank", segments[1], new Vector3(0f, 0.41f, -0.63f));
            tank.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part("Tank Shell", tank, tankMesh, tankMat, Vector3.zero, Quaternion.identity, Vector3.one);
            Part("Hazard Band", tank, band, hazard, new Vector3(0f, 0.27f, 0f), Quaternion.identity, Vector3.one);
            Part("Hazard Band", tank, band, hazard, new Vector3(0f, 0.66f, 0f), Quaternion.identity, Vector3.one);
            Part("Valve", tank, blob, metal, new Vector3(0f, 1.02f, 0f), Quaternion.identity, Vector3.one * 0.08f);
            GameObject leak = Part("Fuel Leak", tank, blob, oil, new Vector3(0.3f, 0.5f, 0.05f), Quaternion.identity, new Vector3(0.1f, 0.3f, 0.1f)).gameObject;
            var tankBox = new GameObject("Sludge (fuel tank)");
            tankBox.transform.SetParent(tank, false);
            var tankCollider = tankBox.AddComponent<BoxCollider>();
            tankCollider.center = new Vector3(0f, 0.5f, 0f);
            tankCollider.size = new Vector3(0.72f, 1.02f, 0.72f);
            Health tankHealth = HitboxHealth(tankBox);
            var strapPath = new Vector3[13];
            for (int i = 0; i < strapPath.Length; i++)
            {
                float t = i * Mathf.PI * 2f / (strapPath.Length - 1);
                strapPath[i] = new Vector3(0.62f * Mathf.Cos(t), 0.2f + 0.55f * Mathf.Sin(t), 0f);
            }
            Mesh strap = DesertFodderBuilder.SaveDesertMesh(LowPolyMesh.Tube("SludgeStrap", strapPath, 0.03f, 4));
            Part("Strap Front", segments[1], strap, hose, new Vector3(0f, 0f, 0.1f), Quaternion.identity, Vector3.one);
            Part("Strap Back", segments[1], strap, hose, new Vector3(0f, 0f, -0.3f), Quaternion.identity, Vector3.one);
            // The hose from the back of the tank down into the tail.
            Mesh hosePipe = DesertFodderBuilder.SaveDesertMesh(LowPolyMesh.Tube("SludgeFuelHose", new[]
            {
                new Vector3(0f, 0.42f, 0f), new Vector3(0f, 0.5f, -0.2f), new Vector3(0f, 0.35f, -0.45f), new Vector3(0f, 0.12f, -0.55f),
            }, 0.05f, 5));
            Part("Fuel Hose", segments[3], hosePipe, hose, Vector3.zero, Quaternion.identity, Vector3.one);

            var body = root.AddComponent<CapsuleCollider>();
            body.direction = 2;
            body.center = new Vector3(0f, 0.6f, -0.1f);
            body.radius = 0.62f;
            body.height = 2.4f;

            var brain = AddBrain<Sludge>(root, 440f, 0.95f, 1.2f, 4.5f);
            root.GetComponent<NavMeshAgent>().angularSpeed = 540f;
            var data = new SerializedObject(brain);
            BossBasics(data, visual, "Sludge", "Fuel tank");
            SetArray(data.FindProperty("bodySegments"), segments);
            SetArray(data.FindProperty("tail"), tail);
            data.FindProperty("hoseTip").objectReferenceValue = tip;
            data.FindProperty("igniter").objectReferenceValue = igniter.GetComponent<Renderer>();
            SetArray(data.FindProperty("claws"), claws);
            SetArray(data.FindProperty("clawElbows"), elbows);
            SetArray(data.FindProperty("clawFingers"), fingers);
            SetArray(data.FindProperty("legs"), legs);
            SetArray(data.FindProperty("legKnees"), knees);
            SetArray(data.FindProperty("mouthparts"), mouth);
            data.FindProperty("tank").objectReferenceValue = tank;
            data.FindProperty("leak").objectReferenceValue = leak;
            data.FindProperty("tankHealth").objectReferenceValue = tankHealth;
            data.FindProperty("oilMaterial").objectReferenceValue = oil;
            data.FindProperty("warningMaterial").objectReferenceValue = Warning();
            data.FindProperty("sparkMaterial").objectReferenceValue = Spark();
            data.FindProperty("blastMaterial").objectReferenceValue = Blast();
            data.FindProperty("shipPart").objectReferenceValue = tank;
            SetFire(data, "fire", new Color(1f, 0.9f, 0.8f), 0.9f, 1f); // burning fuel: thick black smoke
            data.ApplyModifiedPropertiesWithoutUndo();
            return DesertFodderBuilder.SaveDesertPrefab(root, SludgePath);
        }

        // ---------- Test scenes ----------

        // A 64 x 64 m walled sand arena themed on the boss's encounter site in the Desert Dunes level. The boss's home is
        // near the middle; the player starts at the south edge, outside its 20 m arena, and walks in.
        private static void BuildArena(Theme theme)
        {
            Material sand = DesertFodderBuilder.Lit("TestSand", new Color(0.86f, 0.71f, 0.48f), 0f, 0.1f);
            Material wall = DesertFodderBuilder.Lit("TestSandstone", new Color(0.69f, 0.42f, 0.29f), 0f, 0.15f);
            Material rock = DesertFodderBuilder.Lit("TestRock", new Color(0.55f, 0.36f, 0.27f), 0f, 0.15f);
            Material hull = DesertFodderBuilder.Lit("TestHull", new Color(0.62f, 0.64f, 0.66f), 0.7f, 0.4f);

            var arena = new GameObject("Mini-Boss Arena");
            var surface = arena.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            arena.AddComponent<NavMeshBaker>();
            Transform a = arena.transform;

            Block("Sand Floor", new Vector3(0f, -0.5f, 0f), new Vector3(64f, 1f, 64f), sand, a);
            Block("North Wall", new Vector3(0f, 2f, 32f), new Vector3(65f, 4f, 1f), wall, a);
            Block("South Wall", new Vector3(0f, 2f, -32f), new Vector3(65f, 4f, 1f), wall, a);
            Block("East Wall", new Vector3(32f, 2f, 0f), new Vector3(1f, 4f, 64f), wall, a);
            Block("West Wall", new Vector3(-32f, 2f, 0f), new Vector3(1f, 4f, 64f), wall, a);

            switch (theme)
            {
                case Theme.MesaPass:
                    // Four mesas for Burnout to crash into (phase 1) and bank off (phase 2), with open dash lanes between.
                    foreach (Vector2 p in new[] { new Vector2(-13f, 11f), new Vector2(13f, 11f), new Vector2(-13f, -9f), new Vector2(13f, -9f) })
                        Block("Mesa", new Vector3(p.x, 3f, p.y), new Vector3(5f, 6f, 5f), rock, a).transform.rotation = Quaternion.Euler(0f, p.x * p.y, 0f);
                    Plate(a, hull, new Vector3(0f, 0.7f, 19f), 20f);
                    Plate(a, hull, new Vector3(-21f, 0.7f, 2f), 75f);
                    Plate(a, hull, new Vector3(21f, 0.7f, 0f), -70f);
                    break;
                case Theme.WreckField:
                    // A ring of low hull plates: cover from scrap bombs, but nothing tall to block a dive.
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * Mathf.PI * 2f / 8f + 0.2f;
                        Plate(a, hull, new Vector3(Mathf.Cos(angle) * 15f, 0.7f, Mathf.Sin(angle) * 15f + 3f), -angle * Mathf.Rad2Deg + 90f);
                    }
                    Block("Rock", new Vector3(-6f, 0.5f, 10f), new Vector3(2f, 1f, 1.6f), rock, a);
                    Block("Rock", new Vector3(7f, 0.6f, -2f), new Vector3(1.8f, 1.2f, 2.2f), rock, a);
                    break;
                case Theme.BuriedHull:
                    // A half-buried chunk of hull to the north, with panels and rocks to lure Sludge around.
                    GameObject wreck = Block("Buried Hull", new Vector3(0f, 1f, 17f), new Vector3(9f, 3.2f, 4f), hull, a);
                    wreck.transform.rotation = Quaternion.Euler(0f, 18f, 11f);
                    Plate(a, hull, new Vector3(-9f, 0.7f, 10f), 40f);
                    Plate(a, hull, new Vector3(10f, 0.7f, 8f), -30f);
                    Block("Rock", new Vector3(-12f, 0.8f, -4f), new Vector3(2.4f, 1.6f, 2f), rock, a);
                    Block("Rock", new Vector3(11f, 0.7f, -6f), new Vector3(2f, 1.4f, 2.6f), rock, a);
                    break;
            }
        }

        private static void Plate(Transform parent, Material hull, Vector3 position, float yaw)
        {
            GameObject plate = Block("Hull Plate", position, new Vector3(3.2f, 1.4f, 0.25f), hull, parent);
            plate.transform.rotation = Quaternion.Euler(0f, yaw, 6f);
        }

        /// <summary>The boss's fodder: off until the boss engages (the boss switches it on), stops when the boss dies.</summary>
        private static FodderSpawner BuildSpawner(Theme theme, Health boss)
        {
            var go = new GameObject("Fodder Spawner");
            var spawner = go.AddComponent<FodderSpawner>();
            var entries = new List<(GameObject prefab, float weight, int min, int max)>();
            int maxAlive;
            float interval;
            switch (theme)
            {
                case Theme.MesaPass:
                    entries.Add((DesertFodderBuilder.Load(DesertFodderBuilder.ArmadilloPath), 1f, 1, 1));
                    GameObject frog = AssetDatabase.LoadAssetAtPath<GameObject>(FrogTestBuilder.FrogPrefabPath);
                    if (frog != null) entries.Add((frog, 1f, 1, 1));
                    else Debug.LogWarning("MortarFrog prefab not found; Burnout's arena spawns armadillos only.");
                    maxAlive = 5;
                    interval = 7f;
                    break;
                case Theme.WreckField:
                    entries.Add((DesertFodderBuilder.Load(DesertFodderBuilder.MeerkatPath), 1f, 1, 1));
                    entries.Add((DesertFodderBuilder.Load(DesertFodderBuilder.TickPath), 1.5f, 3, 4));
                    maxAlive = 7;
                    interval = 6f;
                    break;
                default:
                    entries.Add((DesertFodderBuilder.Load(DesertFodderBuilder.BeetlePath), 1.5f, 1, 2));
                    entries.Add((DesertFodderBuilder.Load(DesertFodderBuilder.EelPath), 1f, 1, 1));
                    maxAlive = 5;
                    interval = 7f;
                    break;
            }

            var data = new SerializedObject(spawner);
            SerializedProperty list = data.FindProperty("entries");
            list.arraySize = entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("prefab").objectReferenceValue = entries[i].prefab;
                entry.FindPropertyRelative("weight").floatValue = entries[i].weight;
                entry.FindPropertyRelative("minGroup").intValue = entries[i].min;
                entry.FindPropertyRelative("maxGroup").intValue = entries[i].max;
            }
            data.FindProperty("maxAlive").intValue = maxAlive;
            data.FindProperty("waveInterval").floatValue = interval;
            data.FindProperty("firstWaveDelay").floatValue = 2f;
            data.FindProperty("spawnRadiusMin").floatValue = 10f;
            data.FindProperty("spawnRadiusMax").floatValue = 18f;
            data.FindProperty("minPlayerDistance").floatValue = 7f;
            data.FindProperty("activationRange").floatValue = 0f; // the boss decides when it runs
            data.FindProperty("stopWhenDead").objectReferenceValue = boss;
            data.ApplyModifiedPropertiesWithoutUndo();
            spawner.enabled = false;
            return spawner;
        }

        private static void EnsureFolder()
        {
            DesertFodderBuilder.EnsureDesertFolders();
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets/Prefabs/Enemies/Desert", "MiniBosses");
        }
    }
}
