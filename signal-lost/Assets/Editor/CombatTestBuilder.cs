using System;
using SignalLost.Combat;
using SignalLost.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SignalLost.Editor
{
    /// <summary>
    /// Generates the combat test scene once. Like the movement graybox builder, it refuses to
    /// overwrite an existing scene; edit the generated scene normally afterwards.
    /// The shared player prefab is not modified: the laser is added to the scene's player instance only.
    /// </summary>
    public static class CombatTestBuilder
    {
        public const string ScenePath = "Assets/Scenes/CombatTest.unity";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/FirstPersonPlayer.prefab";
        private const string InputPath = "Assets/InputSystem_Actions.inputactions";
        private const string MaterialFolder = "Assets/Materials/Combat";

        [MenuItem("Signal Lost/Create Combat Test Scene")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("Combat test scene already exists. Edit it in Unity instead.");
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            if (playerPrefab == null || input == null)
                throw new InvalidOperationException("Player prefab or input actions missing; create the movement graybox first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder("Assets/Materials");
            EnsureFolder(MaterialFolder);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material floor = LitMaterial("CombatFloor", new Color(0.32f, 0.36f, 0.4f));
            Material wall = LitMaterial("CombatWall", new Color(0.52f, 0.55f, 0.58f));
            Material cover = LitMaterial("CombatCover", new Color(0.78f, 0.49f, 0.23f));
            Material dummy = LitMaterial("TrainingDummy", new Color(0.85f, 0.85f, 0.8f));
            Material beam = UnlitMaterial("LaserBeam", new Color(1f, 0.35f, 0.15f));

            // Long range: the player starts at the south end; distances below are measured from the start.
            Transform area = new GameObject("Combat Test Area").transform;
            Block("Floor", new Vector3(0, -0.5f, 0), new Vector3(20, 1, 50), floor, area);
            Block("North wall", new Vector3(0, 1.5f, 25), new Vector3(21, 3, 1), wall, area);
            Block("South wall", new Vector3(0, 1.5f, -25), new Vector3(21, 3, 1), wall, area);
            Block("East wall", new Vector3(10, 1.5f, 0), new Vector3(1, 3, 50), wall, area);
            Block("West wall", new Vector3(-10, 1.5f, 0), new Vector3(1, 3, 50), wall, area);
            Block("Cover block", new Vector3(5, 1.25f, -12), new Vector3(3, 2.5f, 1), cover, area);

            Transform targets = new GameObject("Targets").transform;
            Dummy("Dummy 5m (close)", new Vector3(-3, 0, -15), dummy, targets);
            Dummy("Dummy 12m (mid)", new Vector3(-3, 0, -8), dummy, targets);
            Dummy("Dummy 20m (far)", new Vector3(-3, 0, 0), dummy, targets);
            Dummy("Dummy 32m (out of range)", new Vector3(-3, 0, 12), dummy, targets);
            Dummy("Dummy behind cover", new Vector3(5, 0, -9), dummy, targets);

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            light.gameObject.AddComponent<UniversalAdditionalLightData>();
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.65f);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.position = new Vector3(0, 0.1f, -20);
            Camera camera = player.GetComponentInChildren<Camera>();
            if (camera == null) throw new InvalidOperationException("Player prefab has no camera.");

            var muzzle = new GameObject("Laser Muzzle").transform;
            muzzle.SetParent(camera.transform, false);
            muzzle.localPosition = new Vector3(0.25f, -0.2f, 0.5f);
            var line = muzzle.gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = beam;
            line.startWidth = 0.05f;
            line.endWidth = 0.03f;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;

            var laser = player.AddComponent<MiningLaser>();
            var serialized = new SerializedObject(laser);
            serialized.FindProperty("inputActions").objectReferenceValue = input;
            serialized.FindProperty("aimOrigin").objectReferenceValue = camera.transform;
            serialized.FindProperty("muzzle").objectReferenceValue = muzzle;
            serialized.FindProperty("beam").objectReferenceValue = line;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var hud = player.AddComponent<CombatDebugHud>();
            var hudData = new SerializedObject(hud);
            hudData.FindProperty("laser").objectReferenceValue = laser;
            hudData.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("COMBAT_TEST_SCENE_CREATED_AND_VALIDATED");
        }

        public static void Validate()
        {
            EditorSceneManager.OpenScene(ScenePath);
            if (UnityEngine.Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None).Length != 1)
                throw new InvalidOperationException("Expected one player.");
            var lasers = UnityEngine.Object.FindObjectsByType<MiningLaser>(FindObjectsSortMode.None);
            if (lasers.Length != 1) throw new InvalidOperationException("Expected one mining laser.");
            var data = new SerializedObject(lasers[0]);
            foreach (string field in new[] { "inputActions", "aimOrigin", "muzzle", "beam" })
                if (data.FindProperty(field).objectReferenceValue == null)
                    throw new InvalidOperationException("Mining laser reference missing: " + field);
            if (UnityEngine.Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None).Length != 5)
                throw new InvalidOperationException("Expected five training dummies.");
            if (UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length != 1 ||
                UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length != 1)
                throw new InvalidOperationException("Expected one camera and listener.");
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) > 0)
                        throw new InvalidOperationException("Missing script: " + item.name);
            Debug.Log("COMBAT_TEST_SCENE_VALIDATED");
        }

        private static void Dummy(string name, Vector3 feet, Material material, Transform parent)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule); // 2 m tall, pivot at center
            body.name = name;
            body.transform.SetParent(parent);
            body.transform.position = feet + Vector3.up;
            body.GetComponent<Renderer>().sharedMaterial = material;
            body.AddComponent<TrainingDummy>(); // RequireComponent adds Health first
        }

        private static GameObject Block(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent);
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
            return block;
        }

        private static Material LitMaterial(string name, Color color) =>
            CreateMaterial(name, "Universal Render Pipeline/Lit", color);

        private static Material UnlitMaterial(string name, Color color) =>
            CreateMaterial(name, "Universal Render Pipeline/Unlit", color);

        private static Material CreateMaterial(string name, string shaderName, Color color)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException(shaderName + " shader missing.");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
