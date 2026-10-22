using System;
using SignalLost.Combat;
using SignalLost.Enemies;
using SignalLost.Player;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using static SignalLost.Editor.EnemyTestBuilder;

namespace SignalLost.Editor
{
    /// <summary>
    /// Generates the mortar frog enemy and its test scene: low-poly frog and tin-can meshes and materials, a
    /// Mortar Frog prefab, and FrogTest.unity with the same arena as the enemy test, two frogs and
    /// the player with the mining laser. It refuses to overwrite the scene; an existing frog prefab is reused.
    /// "Regenerate Mortar Frog Prefab" rebuilds the frog and its meshes in place (same GUIDs).
    /// </summary>
    public static class FrogTestBuilder
    {
        public const string ScenePath = "Assets/Scenes/Enemies/FrogTest.unity";
        public const string FrogPrefabPath = EnemyFolder + "/MortarFrog.prefab";

        [MenuItem("Signal Lost/Create Frog Test Scene")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("Frog test scene already exists. Edit it in Unity instead.");
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            var beam = AssetDatabase.LoadAssetAtPath<Material>(BeamMaterialPath);
            if (playerPrefab == null || input == null || beam == null)
                throw new InvalidOperationException("Player prefab, input actions or LaserBeam material missing.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolders();
            GameObject frog = AssetDatabase.LoadAssetAtPath<GameObject>(FrogPrefabPath) ?? BuildFrogPrefab();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildArena();
            PlaceFrog(frog, "Mortar Frog A", new Vector3(0f, 0f, 7f), 180f);
            PlaceFrog(frog, "Mortar Frog B", new Vector3(8f, 0f, 11f), 210f);
            BuildPlayer(playerPrefab, input, beam);
            BuildLighting();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("FROG_TEST_SCENE_CREATED_AND_VALIDATED");
        }

        [MenuItem("Signal Lost/Regenerate Mortar Frog Prefab")]
        public static void Regenerate()
        {
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Regenerate mortar frog prefab",
                    "This rebuilds MortarFrog.prefab and its meshes from the builder script, replacing any edits made " +
                    "to them. Scenes keep their frogs. Continue?", "Regenerate", "Cancel"))
                return;
            EnsureFolders();
            BuildFrogPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("MORTAR_FROG_PREFAB_REGENERATED");
        }

        public static void Validate()
        {
            EditorSceneManager.OpenScene(ScenePath);
            Require(Count<FirstPersonController>() == 1, "Expected one player.");
            Require(Count<MiningLaser>() == 1, "Expected one mining laser.");
            var frogs = All<MortarFrog>();
            Require(frogs.Count == 2, "Expected two mortar frogs.");
            foreach (MortarFrog frog in frogs)
            {
                var brain = new SerializedObject(frog);
                Require(brain.FindProperty("canHealth").objectReferenceValue != null, frog.name + " has no tin can weak spot.");
                var rig = new SerializedObject(frog.GetComponent<MortarFrogRig>());
                foreach (string field in new[] { "visual", "body", "throat", "canMount", "canMouth" })
                    Require(rig.FindProperty(field).objectReferenceValue != null, frog.name + " rig reference missing: " + field);
                Require(rig.FindProperty("eyes").arraySize == 2, frog.name + " should have two eyes.");
                Require(rig.FindProperty("frontLegs").arraySize == 2 && rig.FindProperty("hindLegs").arraySize == 2,
                    frog.name + " should have four legs.");
                Require(frog.GetComponent<NavMeshAgent>() != null, frog.name + " has no NavMeshAgent.");
            }
            Require(Count<HermitCrab>() == 0, "The frog test should have no hermit crabs.");
            Require(Count<NavMeshSurface>() == 1 && Count<NavMeshBaker>() == 1, "Expected one NavMeshSurface with a NavMeshBaker.");
            Require(Count<Camera>() == 1 && Count<AudioListener>() == 1, "Expected one camera and listener.");
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0, "Missing script: " + item.name);
            Debug.Log("FROG_TEST_SCENE_VALIDATED");
        }

        // ---------- Mortar frog ----------

        // Layout (metres, frog facing +Z, ground at y = 0): a squat body with a big head at the front, bulging
        // eyes on top, a throat sac under the mouth, folded hind legs at the back, and a soup can strapped to its
        // back tilted forward like a mortar.
        private static GameObject BuildFrogPrefab()
        {
            Mesh blob = SaveMesh(LowPolyMesh.Icosphere("CrabBlob", 1)); // shared unit icosphere
            Mesh gem = GemMesh();
            Mesh limb = SaveMesh(LowPolyMesh.TaperedPrism("CrabLimb", 6, 0.65f)); // shared tapered limb
            Mesh can = SaveMesh(LowPolyMesh.Lathe("TinCan", new[]
            {
                // Traced with the metal on the left: base, ribbed side, rolled rim, inside, inner floor.
                new Vector2(0f, 0f), new Vector2(0.155f, 0f), new Vector2(0.16f, 0.01f), new Vector2(0.16f, 0.05f),
                new Vector2(0.166f, 0.06f), new Vector2(0.16f, 0.07f), new Vector2(0.16f, 0.35f), new Vector2(0.166f, 0.36f),
                new Vector2(0.16f, 0.37f), new Vector2(0.16f, 0.41f), new Vector2(0.168f, 0.42f), new Vector2(0.15f, 0.425f),
                new Vector2(0.146f, 0.41f), new Vector2(0.146f, 0.012f), new Vector2(0f, 0.012f),
            }, 10));
            Mesh label = SaveMesh(LowPolyMesh.Lathe("TinCanLabel", new[] { new Vector2(0.167f, 0.09f), new Vector2(0.167f, 0.33f) }, 10));
            var strapPath = new Vector3[17];
            for (int i = 0; i < strapPath.Length; i++)
            {
                float t = i * Mathf.PI * 2f / (strapPath.Length - 1);
                strapPath[i] = new Vector3(0.44f * Mathf.Cos(t), 0.3f + 0.3f * Mathf.Sin(t), 0f);
            }
            Mesh strap = SaveMesh(LowPolyMesh.Tube("FrogStrap", strapPath, 0.022f, 4));

            Material skin = LitMaterial("FrogSkin", new Color(0.52f, 0.3f, 0.72f), 0f, 0.35f);
            Material belly = LitMaterial("FrogBelly", new Color(0.95f, 0.82f, 0.45f), 0f, 0.3f);
            Material throatMat = LitMaterial("FrogThroat", new Color(0.98f, 0.9f, 0.62f), 0f, 0.5f);
            Material dark = LitMaterial("FrogMouth", new Color(0.18f, 0.06f, 0.22f), 0f, 0.2f);
            Material eye = LitMaterial("FrogEye", new Color(0.97f, 0.97f, 0.9f), 0f, 0.6f);
            Material pupil = LitMaterial("FrogPupil", new Color(0.04f, 0.04f, 0.05f), 0f, 0.8f);
            Material tin = LitMaterial("CanTin", new Color(0.72f, 0.72f, 0.7f), 0.85f, 0.55f);
            Material red = LitMaterial("CanLabel", new Color(0.82f, 0.15f, 0.12f), 0f, 0.35f);
            Material strapMat = LitMaterial("FrogStrap", new Color(0.26f, 0.22f, 0.2f), 0.3f, 0.3f);

            var root = new GameObject("Mortar Frog");
            Transform visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            Transform body = Part("Body", visual, blob, skin, new Vector3(0f, 0.3f, -0.02f), Quaternion.identity, new Vector3(0.42f, 0.26f, 0.45f));
            Part("Belly", visual, blob, belly, new Vector3(0f, 0.22f, 0.06f), Quaternion.identity, new Vector3(0.36f, 0.17f, 0.38f));
            Part("Head", visual, blob, skin, new Vector3(0f, 0.38f, 0.27f), Quaternion.identity, new Vector3(0.34f, 0.2f, 0.27f));
            Part("Mouth", visual, blob, dark, new Vector3(0f, 0.33f, 0.49f), Quaternion.identity, new Vector3(0.28f, 0.022f, 0.06f));
            Transform throat = Part("Throat", visual, blob, throatMat, new Vector3(0f, 0.25f, 0.42f), Quaternion.identity, new Vector3(0.18f, 0.1f, 0.13f));
            Part("Strap", visual, strap, strapMat, new Vector3(0f, 0f, -0.12f), Quaternion.identity, Vector3.one);

            // Big bulging eyes on top of the head.
            var eyes = new Transform[2];
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -0.15f : 0.15f;
                Transform pivot = Empty(side == 0 ? "Eye L" : "Eye R", visual, new Vector3(x, 0.55f, 0.3f));
                Part("Socket", pivot, blob, skin, new Vector3(0f, -0.03f, -0.02f), Quaternion.identity, Vector3.one * 0.12f);
                Part("Eyeball", pivot, gem, eye, Vector3.zero, Quaternion.identity, Vector3.one * 0.11f);
                Part("Pupil", pivot, gem, pupil, new Vector3(x * 0.15f, 0.01f, 0.085f), Quaternion.identity, Vector3.one * 0.055f);
                eyes[side] = pivot;
            }

            // Front legs: short and splayed. Hind legs: big and folded, ready to spring.
            var front = new Transform[2];
            var hind = new Transform[2];
            for (int side = 0; side < 2; side++)
            {
                float s = side == 0 ? -1f : 1f;
                Transform shoulder = Empty(side == 0 ? "Front Leg L" : "Front Leg R", visual, new Vector3(0.2f * s, 0.24f, 0.3f));
                Segment("Arm", shoulder, limb, skin, Vector3.zero, new Vector3(0.06f * s, -0.22f, 0.1f), 0.045f);
                Part("Hand", shoulder, blob, skin, new Vector3(0.07f * s, -0.23f, 0.15f), Quaternion.identity, new Vector3(0.07f, 0.02f, 0.09f));
                front[side] = shoulder;

                Transform hip = Empty(side == 0 ? "Hind Leg L" : "Hind Leg R", visual, new Vector3(0.3f * s, 0.26f, -0.2f));
                Part("Thigh", hip, blob, skin, new Vector3(0.05f * s, -0.04f, 0.02f), Quaternion.identity, new Vector3(0.13f, 0.11f, 0.2f));
                Segment("Shin", hip, limb, skin, new Vector3(0.09f * s, -0.1f, 0.1f), new Vector3(0.13f * s, -0.24f, -0.12f), 0.05f);
                Part("Foot", hip, blob, skin, new Vector3(0.13f * s, -0.25f, -0.05f), Quaternion.identity, new Vector3(0.1f, 0.025f, 0.18f));
                hind[side] = hip;
            }

            // The soup-can mortar, strapped on and tilted forward so it lobs goo toward whatever the frog faces.
            Transform mount = Empty("Can Mount", visual, new Vector3(0f, 0.5f, -0.12f));
            mount.localRotation = Quaternion.Euler(40f, 0f, 0f);
            Transform canBody = Part("Tin Can", mount, can, tin, Vector3.zero, Quaternion.identity, Vector3.one);
            Part("Label", canBody, label, red, Vector3.zero, Quaternion.identity, Vector3.one);
            Transform mouth = Empty("Can Mouth", mount, new Vector3(0f, 0.43f, 0f));
            // The can is its own hit target: its Health tells the frog when it is struck (to backfire while loading).
            var canCollider = canBody.gameObject.AddComponent<BoxCollider>();
            canCollider.center = new Vector3(0f, 0.21f, 0f);
            canCollider.size = new Vector3(0.34f, 0.44f, 0.34f);
            var canHealth = canBody.gameObject.AddComponent<Health>();
            var canData = new SerializedObject(canHealth);
            canData.FindProperty("maxHealth").floatValue = 1f;
            canData.ApplyModifiedPropertiesWithoutUndo();

            // Components: AddComponent<MortarFrog> also adds the NavMeshAgent, Health and rig it requires.
            root.AddComponent<MortarFrog>();
            root.AddComponent<SignalLost.Inventory.ScrapDropper>(); // defeated enemies drop scrap (tutorial core loop)
            var agent = root.GetComponent<NavMeshAgent>();
            agent.enabled = false; // switched on in MortarFrog.Start, once the NavMesh has been built
            agent.radius = 0.45f;
            agent.height = 0.7f;
            agent.speed = 0f;
            agent.angularSpeed = 0f;
            agent.acceleration = 50f;

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.32f, 0.02f);
            capsule.radius = 0.42f;
            capsule.height = 0.84f;

            var health = new SerializedObject(root.GetComponent<Health>());
            health.FindProperty("maxHealth").floatValue = 60f;
            health.ApplyModifiedPropertiesWithoutUndo();

            var brain = new SerializedObject(root.GetComponent<MortarFrog>());
            brain.FindProperty("canHealth").objectReferenceValue = canHealth;
            brain.ApplyModifiedPropertiesWithoutUndo();

            var rig = new SerializedObject(root.GetComponent<MortarFrogRig>());
            rig.FindProperty("visual").objectReferenceValue = visual;
            rig.FindProperty("body").objectReferenceValue = body;
            rig.FindProperty("throat").objectReferenceValue = throat;
            rig.FindProperty("canMount").objectReferenceValue = mount;
            rig.FindProperty("canMouth").objectReferenceValue = mouth;
            SetArray(rig.FindProperty("eyes"), eyes);
            SetArray(rig.FindProperty("frontLegs"), front);
            SetArray(rig.FindProperty("hindLegs"), hind);
            rig.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(root, FrogPrefabPath);
        }

        private static void PlaceFrog(GameObject prefab, string name, Vector3 position, float yaw)
        {
            var frog = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            frog.name = name;
            frog.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            // Test-scene only: dead frogs come back so you can keep testing.
            var data = new SerializedObject(frog.GetComponent<MortarFrog>());
            data.FindProperty("respawnDelay").floatValue = 5f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
