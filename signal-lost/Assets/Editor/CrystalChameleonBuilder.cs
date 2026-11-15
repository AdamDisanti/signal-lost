using System;
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
using Object = UnityEngine.Object;

namespace SignalLost.Editor
{
    /// <summary>
    /// Generates the Crystal Chameleon (Crystal Caves fodder) as a low-poly prefab and its test scene.
    ///
    /// The chameleon's back is a cluster of the cave's own crystal meshes in a cave crystal material, so when it lies
    /// buried (BurrowEmerge) only a crystal batch shows. Its body is slate rock-coloured, with the chameleon's casque,
    /// turret eyes, curled tail and a long tongue.
    ///
    /// Files: Prefabs/Enemies/Caves/CrystalChameleon.prefab (rebuilt in place, same GUID), meshes in
    /// Prefabs/Enemies/Caves/Meshes, materials in Materials/Enemies/Caves, and Scenes/Enemies/CrystalChameleonTest.unity.
    /// Menu: Signal Lost > Crystal Caves Enemies > Chameleon.
    /// </summary>
    public static class CrystalChameleonBuilder
    {
        public const string PrefabPath = CaveEnemyKit.PrefabFolder + "/CrystalChameleon.prefab";
        public const string ScenePath = "Assets/Scenes/Enemies/CrystalChameleonTest.unity";

        // ---------- Menus ----------

        [MenuItem("Signal Lost/Crystal Caves Enemies/Chameleon/Build Prefab")]
        public static void BuildPrefabMenu()
        {
            BuildPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("CRYSTAL_CHAMELEON_PREFAB_BUILT");
        }

        [MenuItem("Signal Lost/Crystal Caves Enemies/Chameleon/Create Test Scene")]
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
            Transform room = CaveEnemyKit.BuildCaveRoom("Chameleon Test Cave", 36f, 48f, 9f);

            // Real crystal batches along the walls, so the chameleons' crystals can be compared with the real thing.
            (Vector3 at, float yaw, float scale, CrystalColor color)[] batches =
            {
                (new Vector3(-15f, 0f, 18f), 20f, 0.8f, CrystalColor.Purple), (new Vector3(15f, 0f, 19f), 160f, 0.9f, CrystalColor.Cyan),
                (new Vector3(-15.5f, 0f, 2f), 75f, 0.7f, CrystalColor.Green), (new Vector3(15.5f, 0f, 4f), 230f, 0.75f, CrystalColor.Purple),
                (new Vector3(-15f, 0f, -14f), 300f, 0.7f, CrystalColor.Cyan), (new Vector3(15f, 0f, -16f), 120f, 0.65f, CrystalColor.Green),
                (new Vector3(0f, 0f, 22.5f), 0f, 1f, CrystalColor.Pink),
            };
            foreach ((Vector3 at, float yaw, float scale, CrystalColor color) in batches)
                CaveEnemyKit.PlaceCrystalBatch(room, at, yaw, scale, color);
            // Cover to break line of sight with the tongue.
            CaveEnemyKit.PlaceRock(room, "CaveBoulder", new Vector3(-5f, 0.4f, -2f), 30f, new Vector3(1.6f, 1.2f, 1.4f));
            CaveEnemyKit.PlaceRock(room, "CaveBoulderRound", new Vector3(6f, 0.4f, -6f), 0f, new Vector3(1.3f, 1.1f, 1.3f));
            CaveEnemyKit.PlaceRock(room, "Stalagmite", new Vector3(3f, 0f, 9f), 0f, new Vector3(0.7f, 2.6f, 0.7f));

            // Three chameleons, already revealed (there is no scanner disruption yet), each copying a nearby colour.
            (Vector3 at, float yaw, CrystalColor color)[] chameleons =
            {
                (new Vector3(-7f, 0f, 12f), 160f, CrystalColor.Purple), (new Vector3(7f, 0f, 14f), 200f, CrystalColor.Cyan),
                (new Vector3(0f, 0f, 6f), 180f, CrystalColor.Green),
            };
            for (int i = 0; i < chameleons.Length; i++)
            {
                var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                item.name = "Crystal Chameleon " + (i + 1);
                item.transform.SetPositionAndRotation(chameleons[i].at, Quaternion.Euler(0f, chameleons[i].yaw, 0f));
                var data = new SerializedObject(item.GetComponent<CrystalChameleon>());
                data.FindProperty("crystalMaterial").objectReferenceValue = CrystalAssetBuilder.CrystalMaterial(chameleons[i].color);
                data.FindProperty("startRevealed").boolValue = true;
                data.ApplyModifiedPropertiesWithoutUndo();
            }

            CaveEnemyKit.AddCorePlayer(scene, new Vector3(0f, 0.05f, -20f), 0f);
            CaveEnemyKit.CaveLight();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("CRYSTAL_CHAMELEON_TEST_SCENE_CREATED_AND_VALIDATED");
        }

        [MenuItem("Signal Lost/Crystal Caves Enemies/Chameleon/Validate")]
        public static void Validate()
        {
            GameObject prefab = CaveEnemyKit.Load(PrefabPath);
            Require(prefab != null, PrefabPath + " is missing (Build Prefab).");
            var brain = prefab.GetComponent<CrystalChameleon>();
            Require(brain != null && prefab.GetComponent<BurrowEmerge>() != null && prefab.GetComponent<EnemySignalSource>() != null,
                "The chameleon needs CrystalChameleon, BurrowEmerge and EnemySignalSource.");
            Require(prefab.GetComponent<ScrapDropper>() != null, "The chameleon must drop scrap (ScrapDropper).");
            var data = new SerializedObject(brain);
            foreach (string field in new[] { "crystalMaterial", "disruptedMaterial", "spentMaterial", "glow", "head", "jaw", "throat", "mouth", "tongue", "tongueTip", "tail" })
                Require(data.FindProperty(field).objectReferenceValue != null, "CrystalChameleon." + field + " is not set.");
            Require(data.FindProperty("crystals").arraySize > 0 && data.FindProperty("legs").arraySize == 4 && data.FindProperty("eyes").arraySize == 2,
                "CrystalChameleon needs its crystals, four legs and two eyes.");
            Require(!data.FindProperty("startRevealed").boolValue, "The prefab must start camouflaged; only test instances start revealed.");
            var burrow = new SerializedObject(prefab.GetComponent<BurrowEmerge>());
            Require(!burrow.FindProperty("emergeOnProximity").boolValue, "The chameleon must only emerge when disrupted, not by proximity.");
            Require(burrow.FindProperty("visualRoot").objectReferenceValue != null && burrow.FindProperty("bodyColliders").arraySize > 0,
                "BurrowEmerge needs its visual root and body colliders.");
            Require(!prefab.GetComponent<NavMeshAgent>().enabled, "The NavMeshAgent must start disabled (DesertFodder enables it).");

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;
            EditorSceneManager.OpenScene(ScenePath);
            Require(Count<FirstPersonController>() == 1 && Count<PlayerGear>() == 1, ScenePath + ": expected one shared Player.");
            Require(Count<GameSystems>() == 1 && Count<PlayerSpawnPoint>() >= 1, ScenePath + ": expected GameSystems and a spawn point.");
            Require(Count<Camera>() == 1 && Count<AudioListener>() == 1, ScenePath + ": expected one camera and listener.");
            Require(Count<NavMeshSurface>() == 1 && Count<NavMeshBaker>() == 1, ScenePath + ": expected one NavMeshSurface with a NavMeshBaker.");
            Require(Count<CrystalChameleon>() == 3, ScenePath + ": expected three chameleons.");
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0, ScenePath + ": missing script on " + item.name);
            Debug.Log("CRYSTAL_CHAMELEON_VALIDATED");
        }

        // ---------- Model ----------

        // About 1.3 m from snout to tail base, standing 0.7 m at the back. The crystals on its back rise to about
        // 1.6 m, so with the body buried 0.95 m deep, a crystal cluster about 0.65 m tall is all that shows.
        internal static GameObject BuildPrefab()
        {
            CaveEnemyKit.EnsureFolders();
            Mesh blob = CaveEnemyKit.SaveMesh(LowPolyMesh.Icosphere("CaveBlob", 1));
            Mesh gem = CaveEnemyKit.SaveMesh(LowPolyMesh.Icosphere("CaveGem", 0));
            Mesh limb = CaveEnemyKit.SaveMesh(LowPolyMesh.TaperedPrism("CaveLimb", 5, 0.6f));
            Mesh tongueMesh = CaveEnemyKit.SaveMesh(LowPolyMesh.TaperedPrism("ChameleonTongue", 5, 0.7f));
            Mesh casqueMesh = CaveEnemyKit.SaveMesh(LowPolyMesh.TaperedPrism("ChameleonCasque", 4, 0.15f));

            Material skin = CaveEnemyKit.Lit("ChameleonSkin", new Color(0.21f, 0.28f, 0.33f), 0f, 0.35f);
            Material belly = CaveEnemyKit.Lit("ChameleonBelly", new Color(0.44f, 0.49f, 0.55f), 0f, 0.3f);
            Material ridge = CaveEnemyKit.Lit("ChameleonRidge", new Color(0.3f, 0.36f, 0.42f), 0f, 0.4f);
            Material eyeMat = CaveEnemyKit.Lit("ChameleonEye", new Color(0.33f, 0.41f, 0.44f), 0f, 0.45f);
            Material pupil = CaveEnemyKit.Lit("ChameleonPupil", new Color(0.03f, 0.03f, 0.04f), 0f, 0.85f);
            Material tongueMat = CaveEnemyKit.Lit("ChameleonTongue", new Color(0.86f, 0.4f, 0.52f), 0f, 0.7f);
            Material tipMat = CaveEnemyKit.Lit("ChameleonTongueTip", new Color(0.72f, 0.22f, 0.36f), 0f, 0.85f);
            Material red = CaveEnemyKit.Glowing("ChameleonCrystalRed", new Color(1f, 0.14f, 0.1f), 1.6f);
            Material spent = CaveEnemyKit.Lit("ChameleonCrystalSpent", new Color(0.2f, 0.2f, 0.24f), 0.1f, 0.55f);
            Material disguise = CrystalAssetBuilder.CrystalMaterial(CrystalColor.Purple);
            if (disguise == null)
            {
                CrystalAssetBuilder.Build();
                disguise = CrystalAssetBuilder.CrystalMaterial(CrystalColor.Purple);
            }

            var root = new GameObject("Crystal Chameleon");
            Transform visual = Empty("Visual", root.transform, Vector3.zero);

            // Body: a deep, narrow chameleon body and a pale belly.
            Part("Body", visual, blob, skin, new Vector3(0f, 0.44f, 0f), Quaternion.Euler(-4f, 0f, 0f), new Vector3(0.3f, 0.3f, 0.6f));
            Part("Belly", visual, blob, belly, new Vector3(0f, 0.33f, 0.03f), Quaternion.identity, new Vector3(0.24f, 0.17f, 0.5f));
            Part("Neck", visual, blob, skin, new Vector3(0f, 0.47f, 0.48f), Quaternion.identity, new Vector3(0.2f, 0.2f, 0.2f));

            // The crystal back: the cave's own crystal meshes, as one small batch.
            var crystals = new System.Collections.Generic.List<Renderer>();
            Transform back = Empty("Crystals", visual, new Vector3(0f, 0.6f, -0.02f));
            (string shape, Vector3 at, Vector3 euler, float scale)[] spines =
            {
                ("CrystalCluster", new Vector3(0f, 0f, 0f), new Vector3(0f, 25f, 0f), 0.42f),
                ("CrystalSpire", new Vector3(0.05f, 0.02f, -0.3f), new Vector3(-22f, 0f, -8f), 0.4f),
                ("CrystalShard", new Vector3(-0.06f, 0.02f, 0.3f), new Vector3(18f, 70f, 10f), 0.36f),
                ("CrystalShard", new Vector3(0.08f, -0.02f, 0.12f), new Vector3(8f, 200f, -24f), 0.26f),
                ("CrystalSpire", new Vector3(-0.07f, -0.04f, -0.5f), new Vector3(-38f, 40f, 12f), 0.24f),
            };
            foreach ((string shape, Vector3 at, Vector3 euler, float scale) in spines)
                crystals.Add(Part(shape, back, CaveEnemyKit.CrystalMesh(shape), disguise, at, Quaternion.Euler(euler), Vector3.one * scale).GetComponent<Renderer>());
            Light glow = CrystalAssetBuilder.AddGlow(back, Vector3.up * 1.2f, 6f, 2.5f);
            glow.color = SignalLost.Effects.CrystalBatch.GlowColor(disguise);

            // Head: a pivot so it can pull back for the tongue; casque, snout, jaw, throat sac and turret eyes.
            Transform head = Empty("Head", visual, new Vector3(0f, 0.48f, 0.6f));
            Part("Skull", head, blob, skin, new Vector3(0f, 0f, 0.1f), Quaternion.identity, new Vector3(0.18f, 0.17f, 0.22f));
            Part("Snout", head, blob, skin, new Vector3(0f, -0.03f, 0.28f), Quaternion.identity, new Vector3(0.12f, 0.1f, 0.13f));
            Segment("Casque", head, casqueMesh, ridge, new Vector3(0f, 0.08f, 0.2f), new Vector3(0f, 0.3f, -0.12f), 0.08f);
            Transform jaw = Empty("Jaw", head, new Vector3(0f, -0.07f, 0.06f));
            Part("Lower Jaw", jaw, blob, belly, new Vector3(0f, -0.03f, 0.15f), Quaternion.identity, new Vector3(0.13f, 0.06f, 0.17f));
            Transform throat = Part("Throat", jaw, blob, belly, new Vector3(0f, -0.07f, 0.03f), Quaternion.identity, new Vector3(0.1f, 0.07f, 0.1f));
            Transform mouth = Empty("Mouth", head, new Vector3(0f, -0.05f, 0.34f));
            var eyes = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Transform eye = Empty(i == 0 ? "Eye L" : "Eye R", head, new Vector3(0.13f * side, 0.05f, 0.14f));
                eye.localRotation = Quaternion.Euler(0f, 70f * side, 0f);
                Part("Turret", eye, blob, eyeMat, Vector3.zero, Quaternion.identity, new Vector3(0.085f, 0.085f, 0.085f));
                Part("Pupil", eye, gem, pupil, new Vector3(0f, 0f, 0.075f), Quaternion.identity, Vector3.one * 0.03f);
                eyes[i] = eye;
            }

            // Tongue: hidden until it licks; the brain stretches it toward the player.
            Transform tongue = Part("Tongue", visual, tongueMesh, tongueMat, new Vector3(0f, 0.43f, 0.94f), Quaternion.identity, new Vector3(0.035f, 0.035f, 1f));
            Transform tip = Part("Tongue Tip", visual, blob, tipMat, new Vector3(0f, 0.43f, 0.94f), Quaternion.identity, Vector3.one * 0.07f);

            // Four legs in diagonal pairs (front left + back right swing together), gripping toes.
            var legs = new Transform[4];
            Vector3[] hips = { new Vector3(-0.2f, 0.38f, 0.3f), new Vector3(0.2f, 0.38f, 0.3f), new Vector3(-0.2f, 0.38f, -0.3f), new Vector3(0.2f, 0.38f, -0.3f) };
            for (int i = 0; i < 4; i++)
            {
                float side = hips[i].x < 0f ? -1f : 1f;
                Transform hip = Empty("Leg " + (i + 1), visual, hips[i]);
                Vector3 knee = new Vector3(0.14f * side, -0.06f, 0.04f), foot = new Vector3(0.17f * side, -0.37f, 0.07f);
                Segment("Upper", hip, limb, skin, Vector3.zero, knee, 0.05f);
                Segment("Lower", hip, limb, skin, knee, foot, 0.04f);
                Part("Foot", hip, blob, skin, foot + new Vector3(0f, 0.02f, 0.02f), Quaternion.identity, new Vector3(0.06f, 0.03f, 0.07f));
                legs[i] = hip;
            }

            // The chameleon's curled tail: straight back, then a shrinking spiral downwards.
            var path = new System.Collections.Generic.List<Vector3> { Vector3.zero, new Vector3(0f, -0.04f, -0.2f) };
            Vector3 centre = new Vector3(0f, -0.2f, -0.38f);
            for (int i = 0; i <= 18; i++)
            {
                float t = i / 18f;
                float angle = Mathf.Lerp(135f, -270f, t) * Mathf.Deg2Rad;
                float radius = Mathf.Lerp(0.24f, 0.06f, t);
                path.Add(centre + new Vector3(0f, radius * Mathf.Sin(angle), -radius * Mathf.Cos(angle)));
            }
            Mesh tailMesh = CaveEnemyKit.SaveMesh(LowPolyMesh.Tube("ChameleonTail", path.ToArray(), 0.05f, 5));
            Transform tail = Empty("Tail", visual, new Vector3(0f, 0.42f, -0.55f));
            Part("Tail Curl", tail, tailMesh, skin, Vector3.zero, Quaternion.identity, Vector3.one);

            var body = root.AddComponent<CapsuleCollider>();
            body.direction = 2;
            body.center = new Vector3(0f, 0.45f, 0.1f);
            body.radius = 0.32f;
            body.height = 1.5f;

            // Brain (brings NavMeshAgent and Health), set up like the other fodder.
            var brain = root.AddComponent<CrystalChameleon>();
            var agent = root.GetComponent<NavMeshAgent>();
            agent.enabled = false; // switched on in DesertFodder.Start, once the NavMesh has been built
            agent.radius = 0.4f;
            agent.height = 0.8f;
            agent.speed = 3.2f;
            agent.acceleration = 12f;
            agent.angularSpeed = 360f;
            agent.stoppingDistance = 0.2f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            var health = new SerializedObject(root.GetComponent<Health>());
            health.FindProperty("maxHealth").floatValue = 40f;
            health.ApplyModifiedPropertiesWithoutUndo();

            var signal = root.AddComponent<EnemySignalSource>();
            var burrow = root.AddComponent<BurrowEmerge>();
            var burrowData = new SerializedObject(burrow);
            burrowData.FindProperty("visualRoot").objectReferenceValue = visual;
            burrowData.FindProperty("signalSource").objectReferenceValue = signal;
            burrowData.FindProperty("bodyColliders").arraySize = 1;
            burrowData.FindProperty("bodyColliders").GetArrayElementAtIndex(0).objectReferenceValue = body;
            burrowData.FindProperty("emergeOnProximity").boolValue = false; // only the scanner's disruption reveals it
            burrowData.FindProperty("burrowDepth").floatValue = 0.95f;
            burrowData.FindProperty("emergenceDuration").floatValue = 1f;
            burrowData.FindProperty("postEmergenceGrace").floatValue = 0.8f;
            burrowData.ApplyModifiedPropertiesWithoutUndo();

            CaveEnemyKit.SetDrops(root.AddComponent<ScrapDropper>(), (ScrapType.ScrapMetal, 1, 2), (ScrapType.Wiring, 0, 1));

            var data = new SerializedObject(brain);
            data.FindProperty("visual").objectReferenceValue = visual;
            data.FindProperty("crystalMaterial").objectReferenceValue = disguise;
            data.FindProperty("disruptedMaterial").objectReferenceValue = red;
            data.FindProperty("spentMaterial").objectReferenceValue = spent;
            data.FindProperty("glow").objectReferenceValue = glow;
            data.FindProperty("head").objectReferenceValue = head;
            data.FindProperty("jaw").objectReferenceValue = jaw;
            data.FindProperty("throat").objectReferenceValue = throat;
            data.FindProperty("mouth").objectReferenceValue = mouth;
            data.FindProperty("tongue").objectReferenceValue = tongue;
            data.FindProperty("tongueTip").objectReferenceValue = tip;
            data.FindProperty("tail").objectReferenceValue = tail;
            SerializedProperty crystalList = data.FindProperty("crystals");
            crystalList.arraySize = crystals.Count;
            for (int i = 0; i < crystals.Count; i++) crystalList.GetArrayElementAtIndex(i).objectReferenceValue = crystals[i];
            SetArray(data.FindProperty("eyes"), eyes);
            SetArray(data.FindProperty("legs"), legs);
            data.ApplyModifiedPropertiesWithoutUndo();
            return CaveEnemyKit.SavePrefab(root, PrefabPath);
        }
    }
}
