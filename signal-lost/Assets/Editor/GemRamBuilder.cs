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
    /// Generates the Gem Ram (Crystal Caves pressure fodder) as a low-poly prefab and its test scene.
    ///
    /// A stocky bighorn in cream-grey wool with a dark face and big curled horns, each horn capped with one of the
    /// cave's own crystal shards (cyan). About 1.6 m long and 1.1 m at the shoulder.
    ///
    /// Files: Prefabs/Enemies/Caves/GemRam.prefab (rebuilt in place, same GUID), meshes in Prefabs/Enemies/Caves/Meshes,
    /// materials in Materials/Enemies/Caves, and Scenes/Enemies/GemRamTest.unity.
    /// Menu: Signal Lost > Crystal Caves Enemies > Gem Ram.
    /// </summary>
    public static class GemRamBuilder
    {
        public const string PrefabPath = CaveEnemyKit.PrefabFolder + "/GemRam.prefab";
        public const string ScenePath = "Assets/Scenes/Enemies/GemRamTest.unity";

        // ---------- Menus ----------

        [MenuItem("Signal Lost/Crystal Caves Enemies/Gem Ram/Build Prefab")]
        public static void BuildPrefabMenu()
        {
            BuildPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("GEM_RAM_PREFAB_BUILT");
        }

        [MenuItem("Signal Lost/Crystal Caves Enemies/Gem Ram/Create Test Scene")]
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
            Transform room = CaveEnemyKit.BuildCaveRoom("Gem Ram Test Cave", 36f, 48f, 9f);

            (Vector3 at, float yaw, float scale, CrystalColor color)[] batches =
            {
                (new Vector3(-15f, 0f, 18f), 20f, 0.8f, CrystalColor.Purple), (new Vector3(15f, 0f, 19f), 160f, 0.9f, CrystalColor.Cyan),
                (new Vector3(-15.5f, 0f, -6f), 75f, 0.7f, CrystalColor.Green), (new Vector3(15.5f, 0f, -8f), 230f, 0.75f, CrystalColor.Pink),
                (new Vector3(0f, 0f, 22.5f), 0f, 1f, CrystalColor.Cyan),
            };
            foreach ((Vector3 at, float yaw, float scale, CrystalColor color) in batches)
                CaveEnemyKit.PlaceCrystalBatch(room, at, yaw, scale, color);

            // Pillars for the ricochet: the rams bounce off these as well as the walls.
            CaveEnemyKit.PlaceRock(room, "CaveColumn", new Vector3(-6f, 0f, 2f), 0f, new Vector3(1.4f, 1f, 1.4f));
            CaveEnemyKit.PlaceRock(room, "CaveColumn", new Vector3(7f, 0f, -4f), 40f, new Vector3(1.2f, 1f, 1.2f));
            CaveEnemyKit.PlaceRock(room, "Stalagmite", new Vector3(2f, 0f, 12f), 0f, new Vector3(0.9f, 2.6f, 0.9f));
            CaveEnemyKit.PlaceRock(room, "StalagmiteBlunt", new Vector3(-9f, 0f, -10f), 0f, new Vector3(1.1f, 1.8f, 1.1f));
            CaveEnemyKit.PlaceRock(room, "CaveBoulder", new Vector3(10f, 0.4f, 8f), 30f, new Vector3(1.6f, 1.2f, 1.4f));

            (Vector3 at, float yaw)[] rams = { (new Vector3(-8f, 0f, 12f), 160f), (new Vector3(9f, 0f, 14f), 200f), (new Vector3(0f, 0f, 6f), 180f) };
            for (int i = 0; i < rams.Length; i++)
            {
                var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                item.name = "Gem Ram " + (i + 1);
                item.transform.SetPositionAndRotation(rams[i].at, Quaternion.Euler(0f, rams[i].yaw, 0f));
            }

            CaveEnemyKit.AddCorePlayer(scene, new Vector3(0f, 0.05f, -20f), 0f);
            CaveEnemyKit.CaveLight();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("GEM_RAM_TEST_SCENE_CREATED_AND_VALIDATED");
        }

        [MenuItem("Signal Lost/Crystal Caves Enemies/Gem Ram/Validate")]
        public static void Validate()
        {
            GameObject prefab = CaveEnemyKit.Load(PrefabPath);
            Require(prefab != null, PrefabPath + " is missing (Build Prefab).");
            var brain = prefab.GetComponent<GemRam>();
            Require(brain != null, "The ram needs GemRam.");
            Require(prefab.GetComponent<ScrapDropper>() != null, "The ram must drop scrap (ScrapDropper).");
            Require(prefab.GetComponent<EnemySignalSource>() != null, "The ram needs an EnemySignalSource for the scanner.");
            Require(Mathf.Approximately(prefab.GetComponent<Health>().Max, 80f), "The ram should have 80 HP.");
            var data = new SerializedObject(brain);
            foreach (string field in new[] { "visual", "head", "glow", "spentMaterial", "ringMaterial", "dustMaterial" })
                Require(data.FindProperty(field).objectReferenceValue != null, "GemRam." + field + " is not set.");
            Require(data.FindProperty("legs").arraySize == 4 && data.FindProperty("hornCrystals").arraySize == 2,
                "GemRam needs four legs and two horn crystals.");
            Require(!prefab.GetComponent<NavMeshAgent>().enabled, "The NavMeshAgent must start disabled (DesertFodder enables it).");

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;
            EditorSceneManager.OpenScene(ScenePath);
            Require(Count<FirstPersonController>() == 1 && Count<PlayerGear>() == 1, ScenePath + ": expected one shared Player.");
            Require(Count<GameSystems>() == 1 && Count<PlayerSpawnPoint>() >= 1, ScenePath + ": expected GameSystems and a spawn point.");
            Require(Count<Camera>() == 1 && Count<AudioListener>() == 1, ScenePath + ": expected one camera and listener.");
            Require(Count<NavMeshSurface>() == 1 && Count<NavMeshBaker>() == 1, ScenePath + ": expected one NavMeshSurface with a NavMeshBaker.");
            Require(Count<GemRam>() == 3, ScenePath + ": expected three rams.");
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0, ScenePath + ": missing script on " + item.name);
            Debug.Log("GEM_RAM_VALIDATED");
        }

        // ---------- Model ----------

        internal static GameObject BuildPrefab()
        {
            CaveEnemyKit.EnsureFolders();
            Mesh blob = CaveEnemyKit.SaveMesh(LowPolyMesh.Icosphere("CaveBlob", 1));
            Mesh gem = CaveEnemyKit.SaveMesh(LowPolyMesh.Icosphere("CaveGem", 0));
            Mesh limb = CaveEnemyKit.SaveMesh(LowPolyMesh.TaperedPrism("CaveLimb", 5, 0.6f));
            Mesh hornMesh = CaveEnemyKit.SaveMesh(LowPolyMesh.TaperedPrism("RamHorn", 5, 0.85f));
            Mesh shard = CaveEnemyKit.CrystalMesh("CrystalShard");

            Material wool = CaveEnemyKit.Lit("RamWool", new Color(0.74f, 0.72f, 0.66f), 0f, 0.15f);
            Material ruff = CaveEnemyKit.Lit("RamWoolDark", new Color(0.6f, 0.58f, 0.53f), 0f, 0.15f);
            Material face = CaveEnemyKit.Lit("RamFace", new Color(0.22f, 0.2f, 0.2f), 0f, 0.3f);
            Material muzzle = CaveEnemyKit.Lit("RamMuzzle", new Color(0.4f, 0.38f, 0.36f), 0f, 0.3f);
            Material leg = CaveEnemyKit.Lit("RamLeg", new Color(0.3f, 0.28f, 0.27f), 0f, 0.3f);
            Material hoof = CaveEnemyKit.Lit("RamHoof", new Color(0.1f, 0.09f, 0.09f), 0f, 0.5f);
            Material horn = CaveEnemyKit.Lit("RamHorn", new Color(0.86f, 0.77f, 0.58f), 0f, 0.35f);
            Material eye = CaveEnemyKit.Lit("RamEye", new Color(0.04f, 0.035f, 0.03f), 0f, 0.85f);
            Material spent = CaveEnemyKit.Lit("RamCrystalSpent", new Color(0.2f, 0.2f, 0.24f), 0.1f, 0.55f);
            Material crystal = CrystalAssetBuilder.CrystalMaterial(CrystalColor.Cyan);
            if (crystal == null)
            {
                CrystalAssetBuilder.Build();
                crystal = CrystalAssetBuilder.CrystalMaterial(CrystalColor.Cyan);
            }

            var root = new GameObject("Gem Ram");
            Transform visual = Empty("Visual", root.transform, Vector3.zero);

            // Wool body: barrel, shaggy chest ruff and rump; 1.1 m at the shoulder.
            Part("Body", visual, blob, wool, new Vector3(0f, 0.8f, -0.02f), Quaternion.identity, new Vector3(0.36f, 0.33f, 0.6f));
            Part("Chest Ruff", visual, blob, ruff, new Vector3(0f, 0.76f, 0.4f), Quaternion.Euler(12f, 0f, 0f), new Vector3(0.34f, 0.36f, 0.3f));
            Part("Rump", visual, blob, wool, new Vector3(0f, 0.83f, -0.46f), Quaternion.identity, new Vector3(0.33f, 0.3f, 0.26f));
            Part("Neck", visual, blob, wool, new Vector3(0f, 0.98f, 0.48f), Quaternion.Euler(-30f, 0f, 0f), new Vector3(0.17f, 0.2f, 0.2f));
            Part("Tail", visual, blob, ruff, new Vector3(0f, 0.9f, -0.72f), Quaternion.identity, new Vector3(0.07f, 0.1f, 0.06f));

            // Head on a pivot (grazing, lowering for the charge, the headbutt): dark face, lighter muzzle, curled horns.
            Transform head = Empty("Head", visual, new Vector3(0f, 1.05f, 0.55f));
            Part("Skull", head, blob, face, new Vector3(0f, 0.04f, 0.14f), Quaternion.identity, new Vector3(0.16f, 0.17f, 0.22f));
            Part("Muzzle", head, blob, muzzle, new Vector3(0f, -0.06f, 0.32f), Quaternion.identity, new Vector3(0.11f, 0.1f, 0.13f));
            var crystals = new Renderer[2];
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Part(i == 0 ? "Eye L" : "Eye R", head, gem, eye, new Vector3(0.13f * side, 0.08f, 0.22f), Quaternion.identity, Vector3.one * 0.03f);
                Part(i == 0 ? "Ear L" : "Ear R", head, blob, face, new Vector3(0.15f * side, 0.07f, 0.02f), Quaternion.Euler(0f, 0f, -20f * side), new Vector3(0.08f, 0.03f, 0.045f));
                crystals[i] = BuildHorn(head, side, hornMesh, gem, shard, horn, crystal);
            }
            Light glow = CrystalAssetBuilder.AddGlow(head, new Vector3(0f, 0.1f, -0.1f), 4f, 1.5f);
            glow.color = SignalLost.Effects.CrystalBatch.GlowColor(crystal);

            // Four legs: woolly thigh, dark shank, hoof. Front left, front right, back left, back right.
            var legs = new Transform[4];
            Vector3[] hips = { new Vector3(-0.2f, 0.68f, 0.36f), new Vector3(0.2f, 0.68f, 0.36f), new Vector3(-0.2f, 0.68f, -0.4f), new Vector3(0.2f, 0.68f, -0.4f) };
            for (int i = 0; i < 4; i++)
            {
                Transform hip = Empty("Leg " + (i + 1), visual, hips[i]);
                Part("Thigh", hip, blob, i < 2 ? ruff : wool, new Vector3(0f, -0.08f, 0f), Quaternion.identity, new Vector3(0.1f, 0.16f, 0.12f));
                Segment("Shank", hip, limb, leg, new Vector3(0f, -0.18f, 0f), new Vector3(0f, -0.6f, 0.02f), 0.05f);
                Part("Hoof", hip, blob, hoof, new Vector3(0f, -0.63f, 0.03f), Quaternion.identity, new Vector3(0.055f, 0.04f, 0.07f));
                legs[i] = hip;
            }

            var body = root.AddComponent<CapsuleCollider>();
            body.direction = 2;
            body.center = new Vector3(0f, 0.8f, 0.1f);
            body.radius = 0.38f;
            body.height = 1.8f;

            // Brain (brings NavMeshAgent and Health), set up like the other fodder.
            var brain = root.AddComponent<GemRam>();
            var agent = root.GetComponent<NavMeshAgent>();
            agent.enabled = false; // switched on in DesertFodder.Start, once the NavMesh has been built
            agent.radius = 0.5f;
            agent.height = 1.2f;
            agent.speed = 4f;
            agent.acceleration = 14f;
            agent.angularSpeed = 360f;
            agent.stoppingDistance = 0.2f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            var health = new SerializedObject(root.GetComponent<Health>());
            health.FindProperty("maxHealth").floatValue = 80f;
            health.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<EnemySignalSource>();
            CaveEnemyKit.SetDrops(root.AddComponent<ScrapDropper>(), (ScrapType.ScrapMetal, 1, 2), (ScrapType.Wiring, 0, 1));

            var data = new SerializedObject(brain);
            data.FindProperty("visual").objectReferenceValue = visual;
            data.FindProperty("head").objectReferenceValue = head;
            data.FindProperty("glow").objectReferenceValue = glow;
            data.FindProperty("spentMaterial").objectReferenceValue = spent;
            data.FindProperty("ringMaterial").objectReferenceValue = Unlit("RamWarningRing", new Color(1f, 0.45f, 0.15f));
            data.FindProperty("dustMaterial").objectReferenceValue = Unlit("CaveDust", new Color(0.55f, 0.57f, 0.66f));
            SetArray(data.FindProperty("legs"), legs);
            SerializedProperty list = data.FindProperty("hornCrystals");
            list.arraySize = crystals.Length;
            for (int i = 0; i < crystals.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = crystals[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            return CaveEnemyKit.SavePrefab(root, PrefabPath);
        }

        // A bighorn curl beside the head: up and back, down behind the ear, forward under it, tip pointing up-forward,
        // drifting outward as it goes. Tapered segments with knuckle gems at the bends, and a crystal shard on the tip.
        private static Renderer BuildHorn(Transform head, float side, Mesh segment, Mesh knuckle, Mesh shard, Material horn, Material crystal)
        {
            Transform root = Empty(side < 0f ? "Horn L" : "Horn R", head, Vector3.zero);
            var path = new List<Vector3> { new Vector3(0.07f * side, 0.15f, 0.1f) };
            const int steps = 12;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float a = Mathf.Lerp(90f, 400f, t) * Mathf.Deg2Rad;
                float r = Mathf.Lerp(0.17f, 0.09f, t);
                path.Add(new Vector3(Mathf.Lerp(0.11f, 0.27f, t) * side, 0.03f + r * Mathf.Sin(a), -0.06f + r * Mathf.Cos(a)));
            }
            for (int i = 0; i < path.Count - 1; i++)
            {
                float radius = Mathf.Lerp(0.065f, 0.03f, i / (float)(path.Count - 1));
                Segment("Horn " + i, root, segment, horn, path[i], path[i + 1], radius);
                if (i > 0) Part("Knuckle " + i, root, knuckle, horn, path[i], Quaternion.identity, Vector3.one * radius * 0.95f);
            }
            Vector3 tip = path[path.Count - 1];
            Vector3 along = (tip - path[path.Count - 2]).normalized;
            return Part("Horn Crystal", root, shard, crystal, tip, Quaternion.FromToRotation(Vector3.up, along), Vector3.one * 0.2f).GetComponent<Renderer>();
        }

        private static Material Unlit(string name, Color color)
        {
            string path = CaveEnemyKit.MaterialFolder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
