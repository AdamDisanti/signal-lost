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
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static SignalLost.Editor.EnemyTestBuilder;
using CrystalColor = SignalLost.Editor.CrystalAssetBuilder.CrystalColor;

namespace SignalLost.Editor
{
    /// <summary>
    /// Generates the Quartz Bat (Crystal Caves fodder) as a low-poly prefab and its test scene.
    ///
    /// A dusky violet cave bat with big pointed ears, an ochre belly and a glowing pink quartz crystal set in its
    /// chest. Its wings are dark finger bones with translucent crystal panes between them.
    ///
    /// Files: Prefabs/Enemies/Caves/QuartzBat.prefab (rebuilt in place, same GUID), meshes in
    /// Prefabs/Enemies/Caves/Meshes, materials in Materials/Enemies/Caves, and Scenes/Enemies/QuartzBatTest.unity.
    /// Menu: Signal Lost > Crystal Caves Enemies > Quartz Bat.
    /// </summary>
    public static class QuartzBatBuilder
    {
        public const string PrefabPath = CaveEnemyKit.PrefabFolder + "/QuartzBat.prefab";
        public const string ScenePath = "Assets/Scenes/Enemies/QuartzBatTest.unity";

        // ---------- Menus ----------

        [MenuItem("Signal Lost/Crystal Caves Enemies/Quartz Bat/Build Prefab")]
        public static void BuildPrefabMenu()
        {
            BuildPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("QUARTZ_BAT_PREFAB_BUILT");
        }

        [MenuItem("Signal Lost/Crystal Caves Enemies/Quartz Bat/Create Test Scene")]
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
            Transform room = CaveEnemyKit.BuildCaveRoom("Quartz Bat Test Cave", 36f, 48f, 9f);

            // Crystal batches along the walls for the cave's light, and a few rocks to walk around.
            (Vector3 at, float yaw, float scale, CrystalColor color)[] batches =
            {
                (new Vector3(-15f, 0f, 18f), 20f, 0.8f, CrystalColor.Purple), (new Vector3(15f, 0f, 19f), 160f, 0.9f, CrystalColor.Pink),
                (new Vector3(-15.5f, 0f, 2f), 75f, 0.7f, CrystalColor.Cyan), (new Vector3(15.5f, 0f, 4f), 230f, 0.75f, CrystalColor.Purple),
                (new Vector3(-15f, 0f, -14f), 300f, 0.7f, CrystalColor.Pink), (new Vector3(15f, 0f, -16f), 120f, 0.65f, CrystalColor.Cyan),
            };
            foreach ((Vector3 at, float yaw, float scale, CrystalColor color) in batches)
                CaveEnemyKit.PlaceCrystalBatch(room, at, yaw, scale, color);
            CaveEnemyKit.PlaceRock(room, "CaveBoulder", new Vector3(-6f, 0.4f, -4f), 30f, new Vector3(1.6f, 1.2f, 1.4f));
            CaveEnemyKit.PlaceRock(room, "Stalagmite", new Vector3(5f, 0f, 2f), 0f, new Vector3(0.7f, 2.6f, 0.7f));

            // Three bats, spread out so they can be fought one at a time or together.
            Vector3[] bats = { new Vector3(-7f, 0f, 10f), new Vector3(7f, 0f, 12f), new Vector3(0f, 0f, 18f) };
            for (int i = 0; i < bats.Length; i++)
            {
                var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                item.name = "Quartz Bat " + (i + 1);
                item.transform.SetPositionAndRotation(bats[i], Quaternion.Euler(0f, 180f, 0f));
            }

            CaveEnemyKit.AddCorePlayer(scene, new Vector3(0f, 0.05f, -20f), 0f);
            CaveEnemyKit.CaveLight();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("QUARTZ_BAT_TEST_SCENE_CREATED_AND_VALIDATED");
        }

        [MenuItem("Signal Lost/Crystal Caves Enemies/Quartz Bat/Validate")]
        public static void Validate()
        {
            GameObject prefab = CaveEnemyKit.Load(PrefabPath);
            Require(prefab != null, PrefabPath + " is missing (Build Prefab).");
            var brain = prefab.GetComponent<QuartzBat>();
            Require(brain != null && prefab.GetComponent<EnemySignalSource>() != null, "The bat needs QuartzBat and EnemySignalSource.");
            Require(prefab.GetComponent<ScrapDropper>() != null, "The bat must drop scrap (ScrapDropper).");
            var data = new SerializedObject(brain);
            foreach (string field in new[] { "crystalCollider", "crystal", "spentMaterial", "glow", "jaw", "mouth", "visual" })
                Require(data.FindProperty(field).objectReferenceValue != null, "QuartzBat." + field + " is not set.");
            Require(data.FindProperty("wings").arraySize == 2, "QuartzBat needs two wings.");
            Require(!prefab.GetComponent<NavMeshAgent>().enabled, "The NavMeshAgent must start disabled (DesertFodder enables it).");

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;
            EditorSceneManager.OpenScene(ScenePath);
            Require(Count<FirstPersonController>() == 1 && Count<PlayerGear>() == 1, ScenePath + ": expected one shared Player.");
            Require(Count<GameSystems>() == 1 && Count<PlayerSpawnPoint>() >= 1, ScenePath + ": expected GameSystems and a spawn point.");
            Require(Count<Camera>() == 1 && Count<AudioListener>() == 1, ScenePath + ": expected one camera and listener.");
            Require(Count<NavMeshSurface>() == 1 && Count<NavMeshBaker>() == 1, ScenePath + ": expected one NavMeshSurface with a NavMeshBaker.");
            Require(Count<QuartzBat>() == 3, ScenePath + ": expected three quartz bats.");
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0, ScenePath + ": missing script on " + item.name);
            Debug.Log("QUARTZ_BAT_VALIDATED");
        }

        // ---------- Model ----------

        // Facing +Z. The body is about 0.75 m from feet to ear tips with a 1.8 m wingspan. The brain lifts the whole
        // Visual to its flying height, so the model is built standing on y = 0.
        internal static GameObject BuildPrefab()
        {
            CaveEnemyKit.EnsureFolders();
            Mesh blob = CaveEnemyKit.SaveMesh(LowPolyMesh.Icosphere("CaveBlob", 1));
            Mesh gem = CaveEnemyKit.SaveMesh(LowPolyMesh.Icosphere("CaveGem", 0));
            Mesh limb = CaveEnemyKit.SaveMesh(LowPolyMesh.TaperedPrism("CaveLimb", 5, 0.6f));
            Mesh spike = CaveEnemyKit.SaveMesh(LowPolyMesh.TaperedPrism("BatSpike", 4, 0.05f));
            Mesh shard = CaveEnemyKit.CrystalMesh("CrystalShard");

            Material fur = CaveEnemyKit.Lit("BatFur", new Color(0.23f, 0.2f, 0.31f), 0f, 0.3f);
            Material belly = CaveEnemyKit.Lit("BatBelly", new Color(0.72f, 0.58f, 0.3f), 0f, 0.25f);
            Material innerEar = CaveEnemyKit.Lit("BatInnerEar", new Color(0.1f, 0.08f, 0.12f), 0f, 0.3f);
            Material bone = CaveEnemyKit.Lit("BatWingBone", new Color(0.17f, 0.15f, 0.23f), 0f, 0.35f);
            Material nose = CaveEnemyKit.Lit("BatNose", new Color(0.62f, 0.32f, 0.42f), 0f, 0.5f);
            Material eye = CaveEnemyKit.Lit("BatEye", new Color(0.03f, 0.03f, 0.04f), 0f, 0.85f);
            Material fang = CaveEnemyKit.Lit("BatFang", new Color(0.9f, 0.88f, 0.82f), 0f, 0.5f);
            Material heart = CaveEnemyKit.Glowing("QuartzBatHeart", new Color(1f, 0.45f, 0.85f), 0.9f);
            Material spent = CaveEnemyKit.Lit("QuartzBatHeartSpent", new Color(0.2f, 0.2f, 0.24f), 0.1f, 0.55f);
            Material pane = Translucent("QuartzBatWing", new Color(0.7f, 0.6f, 1f, 0.55f));

            var root = new GameObject("Quartz Bat");
            Transform visual = Empty("Visual", root.transform, Vector3.zero);

            // Body: a plump furry torso with the ochre belly in front.
            Part("Body", visual, blob, fur, new Vector3(0f, 0.32f, 0f), Quaternion.identity, new Vector3(0.2f, 0.26f, 0.17f));
            Part("Belly", visual, blob, belly, new Vector3(0f, 0.29f, 0.06f), Quaternion.identity, new Vector3(0.15f, 0.2f, 0.12f));

            // The chest crystal: a faceted gem with two small cave shards, its glow, and its own hit sphere.
            Transform heartRoot = Empty("Crystal", visual, new Vector3(0f, 0.31f, 0.16f));
            Renderer crystal = Part("Gem", heartRoot, gem, heart, Vector3.zero, Quaternion.Euler(0f, 45f, 0f), new Vector3(0.08f, 0.11f, 0.06f)).GetComponent<Renderer>();
            Part("Shard L", heartRoot, shard, heart, new Vector3(-0.045f, 0.04f, -0.01f), Quaternion.Euler(10f, 0f, 30f), Vector3.one * 0.12f);
            Part("Shard R", heartRoot, shard, heart, new Vector3(0.05f, -0.03f, -0.01f), Quaternion.Euler(-5f, 90f, -35f), Vector3.one * 0.1f);
            var crystalHit = heartRoot.gameObject.AddComponent<SphereCollider>();
            crystalHit.radius = 0.12f; // sticks out past the body capsule, so frontal shots reach it first
            Light glow = CrystalAssetBuilder.AddGlow(heartRoot, new Vector3(0f, 0f, 0.5f), 3f, 0.5f); // in front, so it lights the player, not its own fur
            glow.color = new Color(1f, 0.55f, 0.85f);

            // Head: skull, short snout with a pink nose, big black eyes, fangs, a jaw that opens to bite, tall ears.
            Transform head = Empty("Head", visual, new Vector3(0f, 0.62f, 0.02f));
            Part("Skull", head, blob, fur, Vector3.zero, Quaternion.identity, new Vector3(0.17f, 0.15f, 0.15f));
            Part("Snout", head, blob, fur, new Vector3(0f, -0.03f, 0.11f), Quaternion.identity, new Vector3(0.09f, 0.07f, 0.08f));
            Part("Nose", head, gem, nose, new Vector3(0f, -0.005f, 0.18f), Quaternion.identity, new Vector3(0.045f, 0.025f, 0.02f));
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                Part(i == 0 ? "Eye L" : "Eye R", head, blob, eye, new Vector3(0.1f * s, 0.03f, 0.07f), Quaternion.identity, Vector3.one * 0.045f);
                Part(i == 0 ? "Fang L" : "Fang R", head, spike, fang, new Vector3(0.03f * s, -0.07f, 0.15f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.012f, 0.012f, 0.05f));
                Transform ear = Empty(i == 0 ? "Ear L" : "Ear R", head, new Vector3(0.07f * s, 0.07f, -0.01f));
                Vector3 tip = new Vector3(0.13f * s, 0.27f, -0.03f);
                // Flat ear blades facing forward (thin along the mesh's local Y, which points forward here).
                Quaternion along = Quaternion.LookRotation(tip, Vector3.forward);
                Part("Outer", ear, spike, fur, Vector3.zero, along, new Vector3(0.09f, 0.03f, tip.magnitude));
                Part("Inner", ear, spike, innerEar, new Vector3(0f, 0.01f, 0.03f), along, new Vector3(0.06f, 0.015f, tip.magnitude * 0.82f));
            }
            Transform jaw = Empty("Jaw", head, new Vector3(0f, -0.06f, 0.05f));
            Part("Lower Jaw", jaw, blob, fur, new Vector3(0f, -0.01f, 0.06f), Quaternion.identity, new Vector3(0.07f, 0.03f, 0.07f));
            Transform mouth = Empty("Mouth", head, new Vector3(0f, -0.06f, 0.17f));

            // Little legs dangling under it.
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                Vector3 hip = new Vector3(0.07f * s, 0.12f, -0.01f), foot = new Vector3(0.08f * s, 0.01f, -0.03f);
                Segment(i == 0 ? "Leg L" : "Leg R", visual, limb, fur, hip, foot, 0.035f);
                Part(i == 0 ? "Foot L" : "Foot R", visual, blob, fur, foot, Quaternion.identity, new Vector3(0.04f, 0.025f, 0.05f));
            }

            // Wings: an arm, a forearm and three fingers in dark bone, with translucent crystal panes between them.
            var wings = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                Transform wing = Empty(i == 0 ? "Wing L" : "Wing R", visual, new Vector3(0.14f * s, 0.43f, -0.01f));
                Vector3 elbow = new Vector3(0.32f * s, 0.03f, -0.04f), wrist = new Vector3(0.58f * s, 0.05f, 0.03f);
                Segment("Arm", wing, limb, bone, Vector3.zero, elbow, 0.032f);
                Segment("Forearm", wing, limb, bone, elbow, wrist, 0.026f);
                Part("Thumb", wing, spike, bone, wrist, Quaternion.LookRotation(new Vector3(0.1f * s, 0.05f, 1f)), new Vector3(0.015f, 0.015f, 0.07f));
                Vector3[] tips = { new Vector3(0.9f * s, 0f, -0.1f), new Vector3(0.78f * s, -0.02f, -0.36f), new Vector3(0.46f * s, -0.03f, -0.44f) };
                foreach (Vector3 fingerTip in tips) Segment("Finger", wing, limb, bone, wrist, fingerTip, 0.016f);
                // Panes: flattened, pointed crystal blades fanning out from the arm to each finger, plus one back to the body.
                (Vector3 from, Vector3 to, float width)[] panes =
                {
                    (new Vector3(0.36f * s, 0.02f, -0.02f), tips[0], 0.11f),
                    (new Vector3(0.3f * s, 0.01f, -0.08f), tips[1], 0.15f),
                    (new Vector3(0.18f * s, 0f, -0.06f), tips[2], 0.15f),
                    (new Vector3(0.02f * s, -0.02f, -0.04f), new Vector3(0.3f * s, -0.04f, -0.36f), 0.11f),
                };
                foreach ((Vector3 from, Vector3 to, float width) in panes)
                {
                    Transform blade = Segment("Crystal Pane", wing, spike, pane, from, to, width);
                    blade.localScale = new Vector3(blade.localScale.x, blade.localScale.y * 0.1f, blade.localScale.z);
                }
                var box = wing.gameObject.AddComponent<BoxCollider>(); // bolts glance off the wings
                box.center = new Vector3(0.48f * s, 0f, -0.16f);
                box.size = new Vector3(0.85f, 0.08f, 0.5f);
                wings[i] = wing;
            }

            var body = visual.gameObject.AddComponent<CapsuleCollider>(); // on Visual so it flies with the model
            body.center = new Vector3(0f, 0.42f, 0f);
            body.radius = 0.17f;
            body.height = 0.78f;

            // Brain (brings NavMeshAgent and Health), set up like the other fodder.
            var brain = root.AddComponent<QuartzBat>();
            var agent = root.GetComponent<NavMeshAgent>();
            agent.enabled = false; // switched on in DesertFodder.Start, once the NavMesh has been built
            agent.radius = 0.3f;
            agent.height = 0.6f;
            agent.speed = 5f;
            agent.acceleration = 20f;
            agent.angularSpeed = 360f;
            agent.stoppingDistance = 0.1f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            var health = new SerializedObject(root.GetComponent<Health>());
            health.FindProperty("maxHealth").floatValue = 30f;
            health.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<EnemySignalSource>(); // the scanner can find it
            CaveEnemyKit.SetDrops(root.AddComponent<ScrapDropper>(), (ScrapType.ScrapMetal, 1, 2), (ScrapType.Wiring, 0, 1));

            var data = new SerializedObject(brain);
            data.FindProperty("visual").objectReferenceValue = visual;
            data.FindProperty("corpseTime").floatValue = 2.2f; // time to fall out of the air before the usual flop
            data.FindProperty("crystalCollider").objectReferenceValue = crystalHit;
            data.FindProperty("crystal").objectReferenceValue = crystal;
            data.FindProperty("spentMaterial").objectReferenceValue = spent;
            data.FindProperty("glow").objectReferenceValue = glow;
            data.FindProperty("jaw").objectReferenceValue = jaw;
            data.FindProperty("mouth").objectReferenceValue = mouth;
            SetArray(data.FindProperty("wings"), wings);
            data.ApplyModifiedPropertiesWithoutUndo();
            return CaveEnemyKit.SavePrefab(root, PrefabPath);
        }

        /// <summary>A see-through URP Lit material (the crystal wing panes); an existing one is reused unchanged.</summary>
        private static Material Translucent(string name, Color color)
        {
            string path = CaveEnemyKit.MaterialFolder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.85f);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", 0f); // panes are seen from above and below
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
