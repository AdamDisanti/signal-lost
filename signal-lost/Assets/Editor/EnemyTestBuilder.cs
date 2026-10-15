using System;
using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Enemies;
using SignalLost.Player;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SignalLost.Editor
{
    /// <summary>
    /// Generates the hermit crab enemy and its test scene:
    /// low-poly meshes and materials, a Metal Bucket shell prefab, a Hermit Crab prefab wearing it, and
    /// EnemyTest.unity with an arena, two crabs and the player with the mining laser.
    /// Like the other builders it refuses to overwrite the scene; existing prefabs are reused.
    /// "Regenerate Hermit Crab Prefabs" rebuilds the crab, bucket and their meshes in place (same GUIDs).
    /// Its arena, player and mesh helpers are shared with FrogTestBuilder.
    /// </summary>
    public static class EnemyTestBuilder
    {
        public const string ScenePath = "Assets/Scenes/EnemyTest.unity";
        public const string CrabPrefabPath = EnemyFolder + "/HermitCrab.prefab";
        public const string BucketPrefabPath = ShellFolder + "/MetalBucket.prefab";

        internal const string EnemyFolder = "Assets/Prefabs/Enemies";
        private const string ShellFolder = EnemyFolder + "/Shells";
        internal const string MeshFolder = EnemyFolder + "/Meshes";
        internal const string MaterialFolder = "Assets/Materials/Enemies";
        internal const string PlayerPrefabPath = "Assets/Prefabs/Player/FirstPersonPlayer.prefab";
        internal const string InputPath = "Assets/InputSystem_Actions.inputactions";
        private const string LaserModelPath = "Assets/Prefabs/Enviornment objects/TutorialCrashSite/Upgrades/MiningLaser.prefab";
        internal const string BeamMaterialPath = "Assets/Materials/Combat/LaserBeam.mat";

        [MenuItem("Signal Lost/Create Enemy Test Scene")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("Enemy test scene already exists. Edit it in Unity instead.");
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            var beam = AssetDatabase.LoadAssetAtPath<Material>(BeamMaterialPath);
            if (playerPrefab == null || input == null || beam == null)
                throw new InvalidOperationException("Player prefab, input actions or LaserBeam material missing. Is main up to date?");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolders();
            GameObject bucket = AssetDatabase.LoadAssetAtPath<GameObject>(BucketPrefabPath) ?? BuildBucketPrefab();
            GameObject crab = AssetDatabase.LoadAssetAtPath<GameObject>(CrabPrefabPath) ?? BuildCrabPrefab(bucket);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildArena();
            PlaceCrab(crab, "Hermit Crab A", new Vector3(0f, 0f, 5f), 180f);
            PlaceCrab(crab, "Hermit Crab B", new Vector3(-9f, 0f, 11f), 150f);
            BuildPlayer(playerPrefab, input, beam);
            BuildLighting();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("ENEMY_TEST_SCENE_CREATED_AND_VALIDATED");
        }

        [MenuItem("Signal Lost/Regenerate Hermit Crab Prefabs")]
        public static void Regenerate()
        {
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Regenerate hermit crab prefabs",
                    "This rebuilds HermitCrab.prefab, MetalBucket.prefab and their meshes from the builder script, " +
                    "replacing any edits made to them. Scenes keep their crabs. Continue?", "Regenerate", "Cancel"))
                return;
            EnsureFolders();
            GameObject bucket = BuildBucketPrefab();
            BuildCrabPrefab(bucket);
            AssetDatabase.SaveAssets();
            Debug.Log("HERMIT_CRAB_PREFABS_REGENERATED");
        }

        public static void Validate()
        {
            EditorSceneManager.OpenScene(ScenePath);
            Require(Count<FirstPersonController>() == 1, "Expected one player.");
            var lasers = All<MiningLaser>();
            Require(lasers.Count == 1, "Expected one mining laser.");
            var laserData = new SerializedObject(lasers[0]);
            foreach (string field in new[] { "inputActions", "aimOrigin", "muzzle", "beam" })
                Require(laserData.FindProperty(field).objectReferenceValue != null, "Mining laser reference missing: " + field);
            GameObject player = lasers[0].gameObject;
            Require(player.GetComponent<Health>() != null, "Player has no Health.");
            Require(player.GetComponent<PlaceholderRespawn>() != null, "Player has no PlaceholderRespawn.");
            Require(player.GetComponent<CombatDebugHud>() != null, "Player has no CombatDebugHud.");

            var crabs = All<HermitCrab>();
            Require(crabs.Count == 2, "Expected two hermit crabs.");
            foreach (HermitCrab crab in crabs)
            {
                var rig = new SerializedObject(crab.GetComponent<HermitCrabRig>());
                foreach (string field in new[] { "shellPrefab", "visual", "body", "shellSocket", "bigClaw", "smallClaw", "bigPincer", "smallPincer" })
                    Require(rig.FindProperty(field).objectReferenceValue != null, crab.name + " rig reference missing: " + field);
                Require(rig.FindProperty("legs").arraySize == 6, crab.name + " should have six legs.");
                Require(rig.FindProperty("eyeStalks").arraySize == 2, crab.name + " should have two eyes.");
                Require(crab.GetComponent<NavMeshAgent>() != null && crab.GetComponent<Collider>() != null, crab.name + " is missing its agent or collider.");
            }

            Require(Count<NavMeshSurface>() == 1 && Count<NavMeshBaker>() == 1, "Expected one NavMeshSurface with a NavMeshBaker.");
            Require(Count<Camera>() == 1 && Count<AudioListener>() == 1, "Expected one camera and listener.");
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0, "Missing script: " + item.name);
            Debug.Log("ENEMY_TEST_SCENE_VALIDATED");
        }

        // ---------- Metal bucket shell ----------

        /// <summary>
        /// Shell prefab convention: origin at the centre of the opening, +Z out of the opening, +Y up.
        /// The bucket lies on its side with its mouth facing forward and its handle on top.
        /// </summary>
        internal static GameObject BuildBucketPrefab()
        {
            // Profile traced with the metal on the left: base, outside wall with two raised bands, rolled rim,
            // inside wall, inside floor.
            var profile = new[]
            {
                new Vector2(0f, 0f), new Vector2(0.27f, 0f), new Vector2(0.283f, 0.14f), new Vector2(0.294f, 0.15f),
                new Vector2(0.294f, 0.17f), new Vector2(0.288f, 0.18f), new Vector2(0.318f, 0.49f), new Vector2(0.33f, 0.5f),
                new Vector2(0.33f, 0.52f), new Vector2(0.324f, 0.53f), new Vector2(0.334f, 0.6f), new Vector2(0.346f, 0.612f),
                new Vector2(0.346f, 0.625f), new Vector2(0.322f, 0.625f), new Vector2(0.312f, 0.61f), new Vector2(0.257f, 0.022f),
                new Vector2(0f, 0.022f),
            };
            Mesh bucketMesh = SaveMesh(LowPolyMesh.Lathe("MetalBucket", profile, 12));

            // Handle drapes down one side (local -Z, which ends up on top), hinged on two lugs below the rim.
            var handlePath = new Vector3[11];
            for (int i = 0; i < handlePath.Length; i++)
            {
                float t = i * Mathf.PI / (handlePath.Length - 1);
                handlePath[i] = new Vector3(0.345f * Mathf.Cos(t), 0.55f - 0.17f * Mathf.Sin(t), -0.385f * Mathf.Sin(t));
            }
            Mesh handleMesh = SaveMesh(LowPolyMesh.Tube("MetalBucketHandle", handlePath, 0.012f, 5));
            Mesh gem = GemMesh();

            Material metal = LitMaterial("BucketMetal", new Color(0.6f, 0.62f, 0.65f), 0.85f, 0.45f);
            Material darkMetal = LitMaterial("BucketHandle", new Color(0.34f, 0.35f, 0.37f), 0.9f, 0.5f);

            var root = new GameObject("Metal Bucket");
            // Bucket axis tilted 15 degrees up from forward; its base sits 0.625 m behind the opening.
            Quaternion lieDown = Quaternion.Euler(75f, 0f, 0f);
            Transform bucket = Part("Bucket", root.transform, bucketMesh, metal, lieDown * new Vector3(0f, -0.625f, 0f), lieDown, Vector3.one);
            Part("Handle", bucket, handleMesh, darkMetal, Vector3.zero, Quaternion.identity, Vector3.one);
            Part("Lug L", bucket, gem, darkMetal, new Vector3(-0.345f, 0.55f, 0f), Quaternion.identity, Vector3.one * 0.022f);
            Part("Lug R", bucket, gem, darkMetal, new Vector3(0.345f, 0.55f, 0f), Quaternion.identity, Vector3.one * 0.022f);
            var collider = bucket.gameObject.AddComponent<BoxCollider>(); // for use as a prop; removed when worn
            collider.center = new Vector3(0f, 0.31f, 0f);
            collider.size = new Vector3(0.69f, 0.63f, 0.69f);

            return SavePrefab(root, BucketPrefabPath);
        }

        // ---------- Hermit crab ----------

        // Layout (metres, crab facing +Z, ground at y = 0):
        //   bucket opening at (0, 0.42, 0.28), bucket extends back to z = -0.33
        //   body just inside the opening; eyes, claws and six legs at the front
        internal static GameObject BuildCrabPrefab(GameObject shell)
        {
            Mesh blob = SaveMesh(LowPolyMesh.Icosphere("CrabBlob", 1));
            Mesh gem = GemMesh();
            Mesh limb = SaveMesh(LowPolyMesh.TaperedPrism("CrabLimb", 6, 0.65f));
            Mesh stalk = SaveMesh(LowPolyMesh.TaperedPrism("CrabStalk", 5, 0.7f));
            Mesh finger = SaveMesh(LowPolyMesh.TaperedPrism("CrabFinger", 5, 0f));

            Material bodyMat = LitMaterial("CrabBody", new Color(0.86f, 0.34f, 0.18f), 0f, 0.25f);
            Material limbMat = LitMaterial("CrabLimb", new Color(0.72f, 0.26f, 0.14f), 0f, 0.25f);
            Material clawMat = LitMaterial("CrabClaw", new Color(0.93f, 0.46f, 0.22f), 0f, 0.3f);
            Material eyeMat = LitMaterial("CrabEye", new Color(0.95f, 0.95f, 0.9f), 0f, 0.6f);
            Material pupilMat = LitMaterial("CrabPupil", new Color(0.05f, 0.05f, 0.06f), 0f, 0.8f);

            var root = new GameObject("Hermit Crab");
            Transform visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            Transform body = Part("Body", visual, blob, bodyMat, new Vector3(0f, 0.33f, 0.34f), Quaternion.identity, new Vector3(0.27f, 0.19f, 0.25f));
            Transform socket = Empty("Shell Socket", visual, new Vector3(0f, 0.42f, 0.28f));

            // Eyes on wobbly stalks.
            var eyes = new Transform[2];
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -0.07f : 0.07f;
                Transform pivot = Empty(side == 0 ? "Eye Stalk L" : "Eye Stalk R", visual, new Vector3(x, 0.42f, 0.48f));
                Segment("Stalk", pivot, stalk, limbMat, Vector3.zero, new Vector3(x * 0.3f, 0.17f, 0.05f), 0.018f);
                Part("Eye", pivot, gem, eyeMat, new Vector3(x * 0.3f, 0.19f, 0.06f), Quaternion.identity, Vector3.one * 0.065f);
                Part("Pupil", pivot, gem, pupilMat, new Vector3(x * 0.3f, 0.2f, 0.115f), Quaternion.identity, Vector3.one * 0.032f);
                eyes[side] = pivot;
            }

            // A big claw on the right (it attacks) and a small one on the left, as real hermit crabs have.
            Transform bigClaw = Claw("Big Claw", visual, new Vector3(0.19f, 0.3f, 0.5f), 1f, 1.4f, limb, blob, finger, limbMat, clawMat, out Transform bigPincer);
            Transform smallClaw = Claw("Small Claw", visual, new Vector3(-0.17f, 0.28f, 0.49f), -1f, 0.75f, limb, blob, finger, limbMat, clawMat, out Transform smallPincer);

            // Six legs, three per side, fanned forward to back. Each leg points along its pivot's +X.
            var legs = new Transform[6];
            float[] legZ = { 0.42f, 0.3f, 0.18f };
            float[] legSpread = { 0.55f, 0.05f, -0.45f };
            for (int i = 0; i < 6; i++)
            {
                float sideSign = i < 3 ? 1f : -1f;
                int k = i % 3;
                Transform hip = Empty((sideSign > 0 ? "Leg R" : "Leg L") + (k + 1), visual, new Vector3(0.17f * sideSign, 0.25f, legZ[k]));
                hip.localRotation = Quaternion.FromToRotation(Vector3.right, new Vector3(sideSign, 0f, legSpread[k]).normalized);
                Segment("Upper", hip, limb, limbMat, Vector3.zero, new Vector3(0.17f, 0.13f, 0f), 0.038f);
                Segment("Lower", hip, limb, limbMat, new Vector3(0.17f, 0.13f, 0f), new Vector3(0.34f, -0.25f, 0f), 0.032f);
                legs[i] = hip;
            }

            // Components: AddComponent<HermitCrab> also adds the NavMeshAgent, Health and rig it requires.
            root.AddComponent<HermitCrab>();
            var agent = root.GetComponent<NavMeshAgent>();
            agent.enabled = false; // switched on in HermitCrab.Start, once the NavMesh has been built
            agent.radius = 0.5f;
            agent.height = 0.9f;
            agent.speed = 3.2f;
            agent.angularSpeed = 300f;
            agent.acceleration = 14f;
            agent.stoppingDistance = 1.2f;

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.45f, 0.05f);
            capsule.radius = 0.48f;
            capsule.height = 0.95f;

            var health = new SerializedObject(root.GetComponent<Health>());
            health.FindProperty("maxHealth").floatValue = 120f;
            health.ApplyModifiedPropertiesWithoutUndo();

            var rig = new SerializedObject(root.GetComponent<HermitCrabRig>());
            rig.FindProperty("shellPrefab").objectReferenceValue = shell;
            rig.FindProperty("visual").objectReferenceValue = visual;
            rig.FindProperty("body").objectReferenceValue = body;
            rig.FindProperty("shellSocket").objectReferenceValue = socket;
            rig.FindProperty("bigClaw").objectReferenceValue = bigClaw;
            rig.FindProperty("smallClaw").objectReferenceValue = smallClaw;
            rig.FindProperty("bigPincer").objectReferenceValue = bigPincer;
            rig.FindProperty("smallPincer").objectReferenceValue = smallPincer;
            SetArray(rig.FindProperty("eyeStalks"), eyes);
            SetArray(rig.FindProperty("legs"), legs);
            rig.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(root, CrabPrefabPath);
        }

        /// <summary>Arm, palm, fixed finger and a hinged moving finger. side = +1 for the right, -1 for the left.</summary>
        private static Transform Claw(string name, Transform parent, Vector3 shoulder, float side, float scale,
            Mesh limb, Mesh blob, Mesh finger, Material limbMat, Material clawMat, out Transform pincer)
        {
            Transform pivot = Empty(name, parent, shoulder);
            pivot.localScale = Vector3.one * scale;
            Segment("Arm", pivot, limb, limbMat, Vector3.zero, new Vector3(0.05f * side, -0.03f, 0.13f), 0.045f);
            Part("Palm", pivot, blob, clawMat, new Vector3(0.07f * side, -0.03f, 0.22f), Quaternion.identity, new Vector3(0.085f, 0.07f, 0.11f));
            Segment("Fixed Finger", pivot, finger, clawMat, new Vector3(0.06f * side, -0.06f, 0.3f), new Vector3(0.05f * side, -0.075f, 0.43f), 0.035f);
            pincer = Empty(name.Replace("Claw", "Pincer"), pivot, new Vector3(0.08f * side, 0f, 0.3f));
            Segment("Moving Finger", pincer, finger, clawMat, Vector3.zero, new Vector3(-0.01f * side, -0.03f, 0.13f), 0.03f);
            return pivot;
        }

        // ---------- Scene ----------

        internal static void BuildArena()
        {
            Material floor = ExistingOrLit("Assets/Materials/Combat/CombatFloor.mat", "ArenaFloor", new Color(0.32f, 0.36f, 0.4f));
            Material wall = ExistingOrLit("Assets/Materials/Combat/CombatWall.mat", "ArenaWall", new Color(0.52f, 0.55f, 0.58f));
            Material cover = ExistingOrLit("Assets/Materials/Combat/CombatCover.mat", "ArenaCover", new Color(0.78f, 0.49f, 0.23f));

            // The NavMesh is built at runtime from this object's children (their colliders).
            var arena = new GameObject("Enemy Test Arena");
            var surface = arena.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            arena.AddComponent<NavMeshBaker>();

            Transform a = arena.transform;
            Block("Floor", new Vector3(0, -0.5f, 0), new Vector3(36, 1, 36), floor, a);
            Block("North wall", new Vector3(0, 1.25f, 18), new Vector3(37, 2.5f, 1), wall, a);
            Block("South wall", new Vector3(0, 1.25f, -18), new Vector3(37, 2.5f, 1), wall, a);
            Block("East wall", new Vector3(18, 1.25f, 0), new Vector3(1, 2.5f, 36), wall, a);
            Block("West wall", new Vector3(-18, 1.25f, 0), new Vector3(1, 2.5f, 36), wall, a);
            Block("Cover block", new Vector3(5, 0.6f, -1), new Vector3(4, 1.2f, 1.2f), cover, a);
            Block("Crate stack", new Vector3(-6, 0.75f, 2), new Vector3(2, 1.5f, 2), cover, a);
            Block("Pillar", new Vector3(3, 1.25f, 9), new Vector3(1.2f, 2.5f, 1.2f), wall, a);
            Block("Platform", new Vector3(11, 0.5f, 10), new Vector3(5, 1, 5), wall, a);
            GameObject ramp = Block("Ramp", new Vector3(11, 0.48f, 5.6f), new Vector3(2.5f, 0.2f, 4.2f), cover, a);
            ramp.transform.rotation = Quaternion.Euler(-14f, 0f, 0f);
        }

        internal static void PlaceCrab(GameObject prefab, string name, Vector3 position, float yaw)
        {
            var crab = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            crab.name = name;
            crab.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            // Test-scene only: dead crabs come back so you can keep testing.
            var data = new SerializedObject(crab.GetComponent<HermitCrab>());
            data.FindProperty("respawnDelay").floatValue = 5f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Player with the mining laser (same setup as the combat test), health, HUD and placeholder respawn.</summary>
        internal static void BuildPlayer(GameObject prefab, InputActionAsset input, Material beam)
        {
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.transform.position = new Vector3(0f, 0.1f, -12f);
            Camera camera = player.GetComponentInChildren<Camera>();
            if (camera == null) throw new InvalidOperationException("Player prefab has no camera.");

            var muzzle = new GameObject("Laser Muzzle").transform;
            muzzle.SetParent(camera.transform, false);
            muzzle.localPosition = new Vector3(0.25f, -0.2f, 0.5f);
            var line = muzzle.gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = beam;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;

            var laser = player.AddComponent<MiningLaser>();
            var laserData = new SerializedObject(laser);
            laserData.FindProperty("inputActions").objectReferenceValue = input;
            laserData.FindProperty("aimOrigin").objectReferenceValue = camera.transform;
            laserData.FindProperty("muzzle").objectReferenceValue = muzzle;
            laserData.FindProperty("beam").objectReferenceValue = line;
            laserData.FindProperty("heldModel").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(LaserModelPath);
            laserData.ApplyModifiedPropertiesWithoutUndo();

            var health = new SerializedObject(player.AddComponent<Health>());
            health.FindProperty("maxHealth").floatValue = 100f;
            health.ApplyModifiedPropertiesWithoutUndo();
            player.AddComponent<PlaceholderRespawn>();

            var hud = new SerializedObject(player.AddComponent<CombatDebugHud>());
            hud.FindProperty("laser").objectReferenceValue = laser;
            hud.FindProperty("showPlayerHealth").boolValue = true;
            hud.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void BuildLighting()
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

        // ---------- Helpers ----------

        private static Mesh gemMesh;
        internal static Mesh GemMesh() => gemMesh != null ? gemMesh : gemMesh = SaveMesh(LowPolyMesh.Icosphere("LowPolyGem", 0));

        internal static Transform Empty(string name, Transform parent, Vector3 position)
        {
            var item = new GameObject(name).transform;
            item.SetParent(parent, false);
            item.localPosition = position;
            return item;
        }

        internal static Transform Part(string name, Transform parent, Mesh mesh, Material material,
            Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localRotation = rotation;
            item.transform.localScale = scale;
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            item.AddComponent<MeshRenderer>().sharedMaterial = material;
            return item.transform;
        }

        /// <summary>A unit-length +Z mesh stretched from one point to another with the given start radius.</summary>
        internal static Transform Segment(string name, Transform parent, Mesh mesh, Material material, Vector3 from, Vector3 to, float radius)
        {
            Vector3 direction = to - from;
            Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            return Part(name, parent, mesh, material, from, Quaternion.LookRotation(direction, up),
                new Vector3(radius, radius, direction.magnitude));
        }

        internal static GameObject Block(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent);
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
            return block;
        }

        internal static void SetArray(SerializedProperty property, Transform[] items)
        {
            property.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        /// <summary>Saves a mesh asset, replacing the contents of an existing one so references and GUIDs survive.</summary>
        internal static Mesh SaveMesh(Mesh mesh)
        {
            string path = MeshFolder + "/" + mesh.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        internal static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            if (prefab == null) throw new InvalidOperationException("Could not save prefab " + path);
            return prefab;
        }

        internal static Material LitMaterial(string name, Color color, float metallic, float smoothness)
        {
            string path = MaterialFolder + "/" + name + ".mat";
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

        internal static Material ExistingOrLit(string path, string fallbackName, Color color) =>
            AssetDatabase.LoadAssetAtPath<Material>(path) ?? LitMaterial(fallbackName, color, 0f, 0.2f);

        internal static void EnsureFolders()
        {
            foreach (string folder in new[] { "Assets/Prefabs", EnemyFolder, ShellFolder, MeshFolder, "Assets/Materials", MaterialFolder })
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                int slash = folder.LastIndexOf('/');
                AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
            }
        }

        internal static List<T> All<T>() where T : Component
        {
            var found = new List<T>();
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                found.AddRange(root.GetComponentsInChildren<T>(true));
            return found;
        }

        internal static int Count<T>() where T : Component => All<T>().Count;

        internal static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
