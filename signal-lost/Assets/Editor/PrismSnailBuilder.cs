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
    /// Generates the Prism Snail (Crystal Caves fodder) as a low-poly prefab and its test scene.
    ///
    /// A pale green cave snail (one body colour) with two stalked eyes and a calm face,
    /// carrying a faceted spiral shell in a dark glowing magenta crystal, with crystal spikes and a glow.
    /// About 1.4 m long and 1 m tall.
    ///
    /// Files: Prefabs/Enemies/Caves/PrismSnail.prefab (rebuilt in place, same GUID), meshes in
    /// Prefabs/Enemies/Caves/Meshes, materials in Materials/Enemies/Caves, and Scenes/Enemies/PrismSnailTest.unity.
    /// Menu: Signal Lost > Crystal Caves Enemies > Prism Snail.
    /// </summary>
    public static class PrismSnailBuilder
    {
        public const string PrefabPath = CaveEnemyKit.PrefabFolder + "/PrismSnail.prefab";
        public const string ScenePath = "Assets/Scenes/Enemies/PrismSnailTest.unity";

        // ---------- Menus ----------

        [MenuItem("Signal Lost/Crystal Caves Enemies/Prism Snail/Build Prefab")]
        public static void BuildPrefabMenu()
        {
            BuildPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("PRISM_SNAIL_PREFAB_BUILT");
        }

        [MenuItem("Signal Lost/Crystal Caves Enemies/Prism Snail/Create Test Scene")]
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
            Transform room = CaveEnemyKit.BuildCaveRoom("Snail Test Cave", 36f, 48f, 9f);

            (Vector3 at, float yaw, float scale, CrystalColor color)[] batches =
            {
                (new Vector3(-15f, 0f, 18f), 20f, 0.8f, CrystalColor.Purple), (new Vector3(15f, 0f, 19f), 160f, 0.9f, CrystalColor.Cyan),
                (new Vector3(-15.5f, 0f, 2f), 75f, 0.7f, CrystalColor.Pink), (new Vector3(15.5f, 0f, 4f), 230f, 0.75f, CrystalColor.Purple),
                (new Vector3(-15f, 0f, -14f), 300f, 0.7f, CrystalColor.Cyan), (new Vector3(15f, 0f, -16f), 120f, 0.65f, CrystalColor.Pink),
                (new Vector3(0f, 0f, 22.5f), 0f, 1f, CrystalColor.Green),
            };
            foreach ((Vector3 at, float yaw, float scale, CrystalColor color) in batches)
                CaveEnemyKit.PlaceCrystalBatch(room, at, yaw, scale, color);
            // A narrow gap between two boulders, so the slime trail can be seen blocking a passage.
            CaveEnemyKit.PlaceRock(room, "CaveBoulder", new Vector3(-9f, 0.4f, 0f), 30f, new Vector3(3.2f, 1.6f, 2f));
            CaveEnemyKit.PlaceRock(room, "CaveBoulderRound", new Vector3(-2.5f, 0.4f, 0f), 0f, new Vector3(2.4f, 1.4f, 2.2f));
            CaveEnemyKit.PlaceRock(room, "Stalagmite", new Vector3(5f, 0f, -4f), 0f, new Vector3(0.7f, 2.6f, 0.7f));
            CaveEnemyKit.PlaceRock(room, "Stalagmite", new Vector3(8f, 0f, 6f), 0f, new Vector3(0.6f, 2.2f, 0.6f));

            (Vector3 at, float yaw)[] snails = { (new Vector3(-6f, 0f, 12f), 160f), (new Vector3(7f, 0f, 14f), 200f), (new Vector3(0f, 0f, 18f), 180f) };
            for (int i = 0; i < snails.Length; i++)
            {
                var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                item.name = "Prism Snail " + (i + 1);
                item.transform.SetPositionAndRotation(snails[i].at, Quaternion.Euler(0f, snails[i].yaw, 0f));
            }

            CaveEnemyKit.AddCorePlayer(scene, new Vector3(0f, 0.05f, -20f), 0f);
            CaveEnemyKit.CaveLight();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("PRISM_SNAIL_TEST_SCENE_CREATED_AND_VALIDATED");
        }

        [MenuItem("Signal Lost/Crystal Caves Enemies/Prism Snail/Validate")]
        public static void Validate()
        {
            GameObject prefab = CaveEnemyKit.Load(PrefabPath);
            Require(prefab != null, PrefabPath + " is missing (Build Prefab).");
            var brain = prefab.GetComponent<PrismSnail>();
            Require(brain != null && prefab.GetComponent<EnemySignalSource>() != null, "The snail needs PrismSnail and EnemySignalSource.");
            Require(prefab.GetComponent<ScrapDropper>() != null, "The snail must drop scrap (ScrapDropper).");
            Require(Mathf.Approximately(new SerializedObject(prefab.GetComponent<Health>()).FindProperty("maxHealth").floatValue, 115f), "The snail should have 115 HP.");
            var data = new SerializedObject(brain);
            foreach (string field in new[] { "visual", "shell", "headRoot", "spentMaterial", "trailMaterial", "flareMaterial", "glow" })
                Require(data.FindProperty(field).objectReferenceValue != null, "PrismSnail." + field + " is not set.");
            Require(data.FindProperty("shellParts").arraySize > 0 && data.FindProperty("stalks").arraySize == 2, "PrismSnail needs its shell parts and two eyestalks.");
            Require(!prefab.GetComponent<NavMeshAgent>().enabled, "The NavMeshAgent must start disabled (DesertFodder enables it).");

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;
            EditorSceneManager.OpenScene(ScenePath);
            Require(Count<FirstPersonController>() == 1 && Count<PlayerGear>() == 1, ScenePath + ": expected one shared Player.");
            Require(Count<GameSystems>() == 1 && Count<PlayerSpawnPoint>() >= 1, ScenePath + ": expected GameSystems and a spawn point.");
            Require(Count<Camera>() == 1 && Count<AudioListener>() == 1, ScenePath + ": expected one camera and listener.");
            Require(Count<NavMeshSurface>() == 1 && Count<NavMeshBaker>() == 1, ScenePath + ": expected one NavMeshSurface with a NavMeshBaker.");
            Require(Count<PrismSnail>() == 3, ScenePath + ": expected three snails.");
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0, ScenePath + ": missing script on " + item.name);
            Debug.Log("PRISM_SNAIL_VALIDATED");
        }

        // ---------- Model ----------

        // Faces +Z. The foot is 1.4 m long; the shell rises to about 1 m.
        internal static GameObject BuildPrefab()
        {
            CaveEnemyKit.EnsureFolders();
            Mesh blob = CaveEnemyKit.SaveMesh(LowPolyMesh.Icosphere("CaveBlob", 1));
            Mesh facet = CaveEnemyKit.SaveMesh(LowPolyMesh.Icosphere("CaveGem", 0));
            Mesh spike = CaveEnemyKit.SaveMesh(LowPolyMesh.TaperedPrism("SnailShellSpike", 5, 0f));

            // One body colour for every snail.
            Material body = CaveEnemyKit.Lit("SnailBody", new Color(0.62f, 0.8f, 0.5f), 0f, 0.35f);
            Material eye = CaveEnemyKit.Lit("SnailEye", new Color(0.03f, 0.03f, 0.04f), 0f, 0.92f);
            Material spent = CaveEnemyKit.Lit("SnailShellSpent", new Color(0.24f, 0.22f, 0.26f), 0.1f, 0.5f);
            Material slime = CaveEnemyKit.Glowing("SnailSlime", new Color(0.75f, 1f, 0.6f), 1.1f);
            Material flare = CaveEnemyKit.Glowing("SnailFlare", new Color(0.8f, 0.08f, 0.55f), 1.3f);
            Material crystal = CaveEnemyKit.Glowing("SnailShell", new Color(0.8f, 0.08f, 0.55f), 1.3f); // dark magenta

            var root = new GameObject("Prism Snail");
            Transform visual = Empty("Visual", root.transform, Vector3.zero);

            // Foot: a long, low slab with a tapering tail and a raised neck.
            Part("Foot", visual, blob, body, new Vector3(0f, 0.11f, -0.02f), Quaternion.identity, new Vector3(0.36f, 0.11f, 0.7f));
            Part("Tail", visual, blob, body, new Vector3(0f, 0.08f, -0.62f), Quaternion.identity, new Vector3(0.2f, 0.07f, 0.28f));
            Part("Neck", visual, blob, body, new Vector3(0f, 0.24f, 0.42f), Quaternion.Euler(-25f, 0f, 0f), new Vector3(0.2f, 0.17f, 0.3f));

            // Head and eyestalks (they shrink into the body when it hides): calm, with two stalked eyes.
            Transform head = Empty("Head", visual, new Vector3(0f, 0.34f, 0.62f));
            Part("Face", head, blob, body, Vector3.zero, Quaternion.identity, new Vector3(0.18f, 0.15f, 0.18f));
            Part("Mouth", head, blob, eye, new Vector3(0f, -0.04f, 0.17f), Quaternion.identity, new Vector3(0.05f, 0.012f, 0.02f));
            var stalks = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Transform stalk = Empty(i == 0 ? "Eyestalk L" : "Eyestalk R", head, new Vector3(0.07f * side, 0.1f, 0.04f));
                stalk.localRotation = Quaternion.Euler(-12f, 14f * side, 0f);
                Segment("Stalk", stalk, spike, body, Vector3.zero, new Vector3(0f, 0f, 0.4f), 0.045f);
                Part("Eyeball", stalk, blob, body, new Vector3(0f, 0f, 0.4f), Quaternion.identity, Vector3.one * 0.065f);
                Part("Pupil", stalk, blob, eye, new Vector3(0f, 0f, 0.45f), Quaternion.identity, Vector3.one * 0.035f);
                stalks[i] = stalk;
            }

            // Shell: a faceted crystal spiral on the back, spiral axis sideways like the reference. A big whorl,
            // smaller whorls winding in on each side, and crystal spikes along the crown.
            Transform shell = Empty("Shell", visual, new Vector3(0f, 0.55f, -0.18f));
            var parts = new System.Collections.Generic.List<Renderer>();
            parts.Add(Part("Shell Body", shell, facet, crystal, Vector3.zero, Quaternion.Euler(0f, 0f, 20f), new Vector3(0.36f, 0.5f, 0.52f)).GetComponent<Renderer>());
            Vector2[] whorls = { new Vector2(0.12f, 0.02f), new Vector2(0.04f, 0.22f), new Vector2(-0.1f, 0.16f), new Vector2(-0.12f, -0.02f) };
            float[] whorlSize = { 0.3f, 0.24f, 0.17f, 0.1f };
            for (int s = -1; s <= 1; s += 2)
                for (int i = 0; i < whorls.Length; i++)
                    parts.Add(Part("Whorl", shell, facet, crystal, new Vector3(0.3f * s, whorls[i].y, whorls[i].x),
                        Quaternion.Euler(i * 30f, 0f, 0f), new Vector3(0.1f, 1f, 1f) * whorlSize[i]).GetComponent<Renderer>());
            for (int i = 0; i < 5; i++)
            {
                float a = Mathf.Lerp(-55f, 50f, i / 4f) * Mathf.Deg2Rad; // along the crown, over the top
                Vector3 from = new Vector3(0f, Mathf.Cos(a) * 0.46f, Mathf.Sin(a) * 0.5f);
                Vector3 direction = new Vector3(0.12f * (i % 2 == 0 ? 1f : -1f), Mathf.Cos(a), Mathf.Sin(a)).normalized;
                parts.Add(Segment("Spike", shell, spike, crystal, from, from + direction * (0.2f + 0.08f * (i % 3)), 0.07f).GetComponent<Renderer>());
            }
            Light glow = CrystalAssetBuilder.AddGlow(shell, Vector3.up * 0.2f, 6f, 2f);
            glow.color = SignalLost.Effects.CrystalBatch.GlowColor(crystal);

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 2;
            capsule.center = new Vector3(0f, 0.45f, 0f);
            capsule.radius = 0.45f;
            capsule.height = 1.4f;

            // Brain (brings NavMeshAgent and Health), set up like the other fodder.
            var brain = root.AddComponent<PrismSnail>();
            var agent = root.GetComponent<NavMeshAgent>();
            agent.enabled = false; // switched on in DesertFodder.Start, once the NavMesh has been built
            agent.radius = 0.55f;
            agent.height = 1f;
            agent.speed = 1.5f;
            agent.acceleration = 6f;
            agent.angularSpeed = 140f;
            agent.stoppingDistance = 0.2f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            var health = new SerializedObject(root.GetComponent<Health>());
            health.FindProperty("maxHealth").floatValue = 115f;
            health.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<EnemySignalSource>();
            CaveEnemyKit.SetDrops(root.AddComponent<ScrapDropper>(), (ScrapType.ScrapMetal, 1, 2), (ScrapType.Wiring, 0, 1));

            var data = new SerializedObject(brain);
            data.FindProperty("visual").objectReferenceValue = visual;
            data.FindProperty("shell").objectReferenceValue = shell;
            data.FindProperty("headRoot").objectReferenceValue = head;
            data.FindProperty("spentMaterial").objectReferenceValue = spent;
            data.FindProperty("trailMaterial").objectReferenceValue = slime;
            data.FindProperty("flareMaterial").objectReferenceValue = flare;
            data.FindProperty("glow").objectReferenceValue = glow;
            SerializedProperty shellParts = data.FindProperty("shellParts");
            shellParts.arraySize = parts.Count;
            for (int i = 0; i < parts.Count; i++) shellParts.GetArrayElementAtIndex(i).objectReferenceValue = parts[i];
            SetArray(data.FindProperty("stalks"), stalks);
            data.ApplyModifiedPropertiesWithoutUndo();
            return CaveEnemyKit.SavePrefab(root, PrefabPath);
        }
    }
}
