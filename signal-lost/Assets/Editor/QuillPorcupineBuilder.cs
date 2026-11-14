using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Core;
using SignalLost.Enemies;
using SignalLost.Enemies.Caves;
using SignalLost.Inventory;
using SignalLost.Player;
using SignalLost.Scanner;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using static SignalLost.Editor.EnemyTestBuilder;
using CrystalColor = SignalLost.Editor.CrystalAssetBuilder.CrystalColor;

namespace SignalLost.Editor
{
    /// <summary>
    /// Generates the Quill Porcupine (Crystal Caves fodder) as a low-poly prefab and its test scene.
    ///
    /// A round, low-slung porcupine in dusky brown fur with a blunt snout, pink nose and small glossy black eyes. Its
    /// back is a fan of crystal quills in the cave's own cyan and purple crystal materials, with a glow like a batch.
    ///
    /// Files: Prefabs/Enemies/Caves/QuillPorcupine.prefab (rebuilt in place, same GUID), meshes in
    /// Prefabs/Enemies/Caves/Meshes, materials in Materials/Enemies/Caves, and Scenes/Enemies/QuillPorcupineTest.unity.
    /// Menu: Signal Lost > Crystal Caves Enemies > Quill Porcupine.
    /// </summary>
    public static class QuillPorcupineBuilder
    {
        public const string PrefabPath = CaveEnemyKit.PrefabFolder + "/QuillPorcupine.prefab";
        public const string ScenePath = "Assets/Scenes/Enemies/QuillPorcupineTest.unity";

        // ---------- Menus ----------

        [MenuItem("Signal Lost/Crystal Caves Enemies/Quill Porcupine/Build Prefab")]
        public static void BuildPrefabMenu()
        {
            BuildPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("QUILL_PORCUPINE_PREFAB_BUILT");
        }

        [MenuItem("Signal Lost/Crystal Caves Enemies/Quill Porcupine/Create Test Scene")]
        public static void CreateTestScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Debug.Log(ScenePath + " already exists; skipped (edit it in Unity, or delete it to rebuild).");
                return;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            GameObject prefab = CaveEnemyKit.Load(PrefabPath) ?? BuildPrefab();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Transform room = CaveEnemyKit.BuildCaveRoom("Porcupine Test Cave", 36f, 48f, 9f);

            (Vector3 at, float yaw, float scale, CrystalColor color)[] batches =
            {
                (new Vector3(-15f, 0f, 18f), 20f, 0.8f, CrystalColor.Purple), (new Vector3(15f, 0f, 19f), 160f, 0.9f, CrystalColor.Cyan),
                (new Vector3(-15.5f, 0f, 2f), 75f, 0.7f, CrystalColor.Cyan), (new Vector3(15.5f, 0f, 4f), 230f, 0.75f, CrystalColor.Purple),
                (new Vector3(-15f, 0f, -14f), 300f, 0.7f, CrystalColor.Purple), (new Vector3(15f, 0f, -16f), 120f, 0.65f, CrystalColor.Cyan),
                (new Vector3(0f, 0f, 22.5f), 0f, 1f, CrystalColor.Pink),
            };
            foreach ((Vector3 at, float yaw, float scale, CrystalColor color) in batches)
                CaveEnemyKit.PlaceCrystalBatch(room, at, yaw, scale, color);
            // Rocks to hide behind from the quill fans; missed quills stick in them.
            CaveEnemyKit.PlaceRock(room, "CaveBoulder", new Vector3(-5f, 0.4f, -4f), 30f, new Vector3(1.6f, 1.2f, 1.4f));
            CaveEnemyKit.PlaceRock(room, "CaveBoulderRound", new Vector3(6f, 0.4f, -2f), 0f, new Vector3(1.3f, 1.1f, 1.3f));
            CaveEnemyKit.PlaceRock(room, "Stalagmite", new Vector3(-2f, 0f, 4f), 0f, new Vector3(0.7f, 2.6f, 0.7f));
            CaveEnemyKit.PlaceRock(room, "Stalagmite", new Vector3(4f, 0f, 8f), 0f, new Vector3(0.6f, 2.2f, 0.6f));

            (Vector3 at, float yaw)[] porcupines = { (new Vector3(-7f, 0f, 12f), 160f), (new Vector3(7f, 0f, 14f), 200f), (new Vector3(0f, 0f, 17f), 180f) };
            for (int i = 0; i < porcupines.Length; i++)
            {
                var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                item.name = "Quill Porcupine " + (i + 1);
                item.transform.SetPositionAndRotation(porcupines[i].at, Quaternion.Euler(0f, porcupines[i].yaw, 0f));
            }

            CaveEnemyKit.AddCorePlayer(scene, new Vector3(0f, 0.05f, -20f), 0f);
            CaveEnemyKit.CaveLight();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("QUILL_PORCUPINE_TEST_SCENE_CREATED_AND_VALIDATED");
        }

        [MenuItem("Signal Lost/Crystal Caves Enemies/Quill Porcupine/Validate")]
        public static void Validate()
        {
            GameObject prefab = CaveEnemyKit.Load(PrefabPath);
            Require(prefab != null, PrefabPath + " is missing (Build Prefab).");
            var brain = prefab.GetComponent<QuillPorcupine>();
            Require(brain != null && prefab.GetComponent<EnemySignalSource>() != null, "The porcupine needs QuillPorcupine and EnemySignalSource.");
            Require(prefab.GetComponent<ScrapDropper>() != null, "The porcupine must drop scrap (ScrapDropper).");
            Require(Mathf.Approximately(new SerializedObject(prefab.GetComponent<Health>()).FindProperty("maxHealth").floatValue, 32f), "The porcupine should have 32 HP.");
            var data = new SerializedObject(brain);
            foreach (string field in new[] { "visual", "quillRoot", "launch", "quillShotMesh", "spentMaterial", "glow", "head" })
                Require(data.FindProperty(field).objectReferenceValue != null, "QuillPorcupine." + field + " is not set.");
            Require(data.FindProperty("quills").arraySize > 0 && data.FindProperty("quillMaterials").arraySize == 2 && data.FindProperty("legs").arraySize == 4,
                "QuillPorcupine needs its quills, two quill materials (cyan, purple) and four legs.");
            Require(Mathf.Approximately(data.FindProperty("curledFullChargeShare").floatValue, 0.5f), "Curled, a full charge should take half the porcupine's health (Build Prefab).");
            Require(!prefab.GetComponent<NavMeshAgent>().enabled, "The NavMeshAgent must start disabled (DesertFodder enables it).");

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;
            EditorSceneManager.OpenScene(ScenePath);
            Require(Count<FirstPersonController>() == 1 && Count<PlayerGear>() == 1, ScenePath + ": expected one shared Player.");
            Require(Count<GameSystems>() == 1 && Count<PlayerSpawnPoint>() >= 1, ScenePath + ": expected GameSystems and a spawn point.");
            Require(Count<Camera>() == 1 && Count<AudioListener>() == 1, ScenePath + ": expected one camera and listener.");
            Require(Count<NavMeshSurface>() == 1 && Count<NavMeshBaker>() == 1, ScenePath + ": expected one NavMeshSurface with a NavMeshBaker.");
            Require(Count<QuillPorcupine>() == 3, ScenePath + ": expected three porcupines.");
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0, ScenePath + ": missing script on " + item.name);
            Debug.Log("QUILL_PORCUPINE_VALIDATED");
        }

        // ---------- Model ----------

        // About 1.1 m long and 0.6 m tall at the back; the quills rise to about 1.1 m.
        internal static GameObject BuildPrefab()
        {
            CaveEnemyKit.EnsureFolders();
            Mesh blob = CaveEnemyKit.SaveMesh(LowPolyMesh.Icosphere("CaveBlob", 1));
            Mesh gem = CaveEnemyKit.SaveMesh(LowPolyMesh.Icosphere("CaveGem", 0));
            Mesh spike = CaveEnemyKit.SaveMesh(LowPolyMesh.TaperedPrism("PorcupineQuill", 4, 0f));
            // ThornShot scales its mesh to 0.18 x 0.18 x 1.1, sized for a boss; this one comes out about 6 cm thick and 0.6 m long.
            Mesh shot = LowPolyMesh.TaperedPrism("PorcupineQuillShot", 4, 0f);
            var points = shot.vertices;
            for (int i = 0; i < points.Length; i++) points[i] = Vector3.Scale(points[i], new Vector3(0.35f, 0.35f, 0.55f));
            shot.vertices = points;
            shot.RecalculateBounds();
            shot = CaveEnemyKit.SaveMesh(shot);

            Material fur = CaveEnemyKit.Lit("PorcupineFur", new Color(0.42f, 0.27f, 0.21f), 0f, 0.25f);
            Material face = CaveEnemyKit.Lit("PorcupineFace", new Color(0.58f, 0.39f, 0.31f), 0f, 0.3f);
            Material fringe = CaveEnemyKit.Lit("PorcupineFringe", new Color(0.32f, 0.2f, 0.15f), 0f, 0.3f);
            Material nose = CaveEnemyKit.Lit("PorcupineNose", new Color(0.9f, 0.5f, 0.58f), 0f, 0.6f);
            Material eye = CaveEnemyKit.Lit("PorcupineEye", new Color(0.03f, 0.03f, 0.04f), 0f, 0.92f);
            Material spent = CaveEnemyKit.Lit("PorcupineQuillSpent", new Color(0.2f, 0.2f, 0.24f), 0.1f, 0.55f);
            Material cyan = CrystalAssetBuilder.CrystalMaterial(CrystalColor.Cyan);
            if (cyan == null)
            {
                CrystalAssetBuilder.Build();
                cyan = CrystalAssetBuilder.CrystalMaterial(CrystalColor.Cyan);
            }
            Material purple = CrystalAssetBuilder.CrystalMaterial(CrystalColor.Purple);

            var root = new GameObject("Quill Porcupine");
            Transform visual = Empty("Visual", root.transform, Vector3.zero);

            // Body: round and low-slung, with a paler belly.
            Vector3 centre = new Vector3(0f, 0.36f, -0.02f);
            Vector3 radii = new Vector3(0.4f, 0.28f, 0.5f);
            Part("Body", visual, blob, fur, centre, Quaternion.identity, radii);
            Part("Belly", visual, blob, face, new Vector3(0f, 0.24f, 0.05f), Quaternion.identity, new Vector3(0.3f, 0.16f, 0.4f));

            // Head: a round face with a blunt snout, pink nose, small glossy eyes and round ears.
            Transform head = Empty("Head", visual, new Vector3(0f, 0.36f, 0.42f));
            Part("Face", head, blob, face, Vector3.zero, Quaternion.identity, new Vector3(0.24f, 0.22f, 0.2f));
            Part("Snout", head, blob, face, new Vector3(0f, -0.05f, 0.17f), Quaternion.identity, new Vector3(0.12f, 0.1f, 0.13f));
            Part("Nose", head, blob, nose, new Vector3(0f, -0.03f, 0.3f), Quaternion.identity, new Vector3(0.055f, 0.045f, 0.04f));
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Part(i == 0 ? "Eye L" : "Eye R", head, blob, eye, new Vector3(0.09f * side, 0.06f, 0.17f), Quaternion.identity, Vector3.one * 0.042f);
                Part(i == 0 ? "Ear L" : "Ear R", head, blob, face, new Vector3(0.17f * side, 0.15f, -0.02f), Quaternion.Euler(0f, 20f * side, 0f), new Vector3(0.07f, 0.07f, 0.03f));
            }
            // A short fur fringe of brown spikes behind the face, before the crystal quills start.
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Lerp(-75f, 75f, i / 8f) * Mathf.Deg2Rad;
                Vector3 from = new Vector3(Mathf.Sin(a) * 0.2f, Mathf.Cos(a) * 0.18f, -0.06f);
                Vector3 direction = new Vector3(Mathf.Sin(a), Mathf.Cos(a), -0.9f).normalized;
                Segment("Fringe", head, spike, fringe, from, from + direction * 0.16f, 0.035f);
            }

            // Four stubby legs (front left + back right swing together).
            var legs = new Transform[4];
            Vector3[] hips = { new Vector3(-0.22f, 0.2f, 0.26f), new Vector3(0.22f, 0.2f, 0.26f), new Vector3(-0.22f, 0.2f, -0.26f), new Vector3(0.22f, 0.2f, -0.26f) };
            for (int i = 0; i < 4; i++)
            {
                Transform hip = Empty("Leg " + (i + 1), visual, hips[i]);
                Part("Leg", hip, blob, fur, new Vector3(0f, -0.07f, 0f), Quaternion.identity, new Vector3(0.08f, 0.12f, 0.08f));
                Part("Paw", hip, blob, face, new Vector3(0f, -0.18f, 0.03f), Quaternion.identity, new Vector3(0.08f, 0.035f, 0.1f));
                legs[i] = hip;
            }

            // The crystal quills: rows over the back, short at the neck and longest on top, alternating cyan and purple.
            Transform quillRoot = Empty("Quills", visual, centre);
            var quills = new List<Renderer>();
            float[] rows = { 0.32f, 0.16f, 0f, -0.16f, -0.32f, -0.44f };
            for (int r = 0; r < rows.Length; r++)
            {
                float z = rows[r], ring = Mathf.Sqrt(1f - (z / radii.z) * (z / radii.z));
                float length = Mathf.Lerp(0.28f, 0.55f, Mathf.Sin((r + 1f) / (rows.Length + 1f) * Mathf.PI)) + (r == 0 ? 0f : 0.08f);
                for (int c = 0; c < 7; c++)
                {
                    float a = Mathf.Lerp(-68f, 68f, c / 6f) * Mathf.Deg2Rad + (r % 2) * 0.1f;
                    Vector3 from = new Vector3(Mathf.Sin(a) * radii.x * ring, Mathf.Cos(a) * radii.y * ring, z) * 0.92f;
                    Vector3 direction = new Vector3(Mathf.Sin(a) * 0.7f, Mathf.Cos(a) * 1.3f, -0.85f).normalized; // up and swept back
                    float l = length * (0.75f + 0.25f * Mathf.Cos(a));
                    Material material = (r + c) % 2 == 0 ? cyan : purple;
                    quills.Add(Segment("Quill", quillRoot, spike, material, from, from + direction * l, 0.035f).GetComponent<Renderer>());
                }
            }
            Transform launch = Empty("Quill Launch", quillRoot, new Vector3(0f, 0.42f, 0.05f));
            Light glow = CrystalAssetBuilder.AddGlow(quillRoot, Vector3.up * 0.6f, 5f, 2f);
            glow.color = Color.Lerp(SignalLost.Effects.CrystalBatch.GlowColor(cyan), SignalLost.Effects.CrystalBatch.GlowColor(purple), 0.5f);

            var body = root.AddComponent<CapsuleCollider>();
            body.direction = 2;
            body.center = new Vector3(0f, 0.38f, 0f);
            body.radius = 0.36f;
            body.height = 1.15f;

            // Brain (brings NavMeshAgent and Health), set up like the other fodder.
            var brain = root.AddComponent<QuillPorcupine>();
            var agent = root.GetComponent<NavMeshAgent>();
            agent.enabled = false; // switched on in DesertFodder.Start, once the NavMesh has been built
            agent.radius = 0.45f;
            agent.height = 0.7f;
            agent.speed = 2.6f;
            agent.acceleration = 10f;
            agent.angularSpeed = 300f;
            agent.stoppingDistance = 0.2f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            var health = new SerializedObject(root.GetComponent<Health>());
            health.FindProperty("maxHealth").floatValue = 32f;
            health.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<EnemySignalSource>();
            CaveEnemyKit.SetDrops(root.AddComponent<ScrapDropper>(), (ScrapType.ScrapMetal, 1, 2), (ScrapType.Wiring, 0, 1));

            var data = new SerializedObject(brain);
            data.FindProperty("visual").objectReferenceValue = visual;
            data.FindProperty("quillRoot").objectReferenceValue = quillRoot;
            data.FindProperty("launch").objectReferenceValue = launch;
            data.FindProperty("quillShotMesh").objectReferenceValue = shot;
            data.FindProperty("spentMaterial").objectReferenceValue = spent;
            data.FindProperty("glow").objectReferenceValue = glow;
            data.FindProperty("head").objectReferenceValue = head;
            SerializedProperty quillList = data.FindProperty("quills");
            quillList.arraySize = quills.Count;
            for (int i = 0; i < quills.Count; i++) quillList.GetArrayElementAtIndex(i).objectReferenceValue = quills[i];
            SerializedProperty materials = data.FindProperty("quillMaterials");
            materials.arraySize = 2;
            materials.GetArrayElementAtIndex(0).objectReferenceValue = cyan;
            materials.GetArrayElementAtIndex(1).objectReferenceValue = purple;
            SetArray(data.FindProperty("legs"), legs);
            data.ApplyModifiedPropertiesWithoutUndo();
            return CaveEnemyKit.SavePrefab(root, PrefabPath);
        }
    }
}
