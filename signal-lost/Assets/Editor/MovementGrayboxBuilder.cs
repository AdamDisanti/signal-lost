using System;
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
    public static class MovementGrayboxBuilder
    {
        public const string ScenePath = "Assets/Scenes/MovementGraybox.unity";
        public const string PrefabPath = "Assets/Prefabs/Player/FirstPersonPlayer.prefab";

        [MenuItem("Signal Lost/Create Movement Graybox")]
        public static void Create()
        {
            // Re-running this helper must not overwrite a teammate's authored work.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null ||
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                throw new InvalidOperationException("Graybox assets already exist. Edit them in Unity instead.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Prefabs/Player");
            EnsureFolder("Assets/Materials");
            EnsureFolder("Assets/Materials/Graybox");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Material floor = Material("Floor", new Color(0.32f, 0.36f, 0.4f));
            Material wall = Material("Wall", new Color(0.52f, 0.55f, 0.58f));
            Material obstacle = Material("Obstacle", new Color(0.78f, 0.49f, 0.23f));
            Material platform = Material("Platform", new Color(0.24f, 0.58f, 0.64f));
            Transform area = new GameObject("Movement Test Area").transform;
            Block("Floor", new Vector3(0, -0.5f, 0), new Vector3(24, 1, 28), floor, area);
            Block("North wall", new Vector3(0, 1.5f, 14), new Vector3(25, 3, 1), wall, area);
            Block("South wall", new Vector3(0, 1.5f, -14), new Vector3(25, 3, 1), wall, area);
            Block("East wall", new Vector3(12, 1.5f, 0), new Vector3(1, 3, 28), wall, area);
            Block("West wall", new Vector3(-12, 1.5f, 0), new Vector3(1, 3, 28), wall, area);
            Block("Collision obstacle", new Vector3(0, 1, 0), new Vector3(3, 2, 3), obstacle, area);
            for (int i = 0; i < 4; i++)
                Block("Step " + (i + 1), new Vector3(-7, (i + 1) * 0.125f, 1 + i),
                    new Vector3(3, (i + 1) * 0.25f, 1), obstacle, area);
            GameObject ramp = Block("Ramp", new Vector3(6, 0.8f, 3), new Vector3(3, 0.4f, 6), platform, area);
            ramp.transform.rotation = Quaternion.Euler(-15, 0, 0);
            Block("Jump platform low", new Vector3(-3, 0.3f, 8), new Vector3(2, 0.6f, 2), platform, area);
            Block("Jump platform high", new Vector3(0, 0.6f, 9), new Vector3(2, 1.2f, 2), platform, area);

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            light.gameObject.AddComponent<UniversalAdditionalLightData>();
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.65f);

            GameObject player = new GameObject("FirstPersonPlayer");
            CharacterController capsule = player.AddComponent<CharacterController>();
            capsule.height = 1.8f;
            capsule.radius = 0.35f;
            capsule.center = new Vector3(0, 0.9f, 0);
            capsule.stepOffset = 0.3f;
            capsule.slopeLimit = 45;
            capsule.skinWidth = 0.035f;
            var camera = new GameObject("Player Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.SetParent(player.transform, false);
            camera.transform.localPosition = new Vector3(0, 1.6f, 0);
            camera.nearClipPlane = 0.05f;
            camera.fieldOfView = 75;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.17f, 0.23f);
            camera.gameObject.AddComponent<AudioListener>();
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var movement = player.AddComponent<FirstPersonController>();
            var serialized = new SerializedObject(movement);
            serialized.FindProperty("inputActions").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            serialized.FindProperty("cameraPivot").objectReferenceValue = camera.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(player, PrefabPath);
            UnityEngine.Object.DestroyImmediate(player);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = new Vector3(0, 0.1f, -10);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("MOVEMENT_GRAYBOX_CREATED_AND_VALIDATED");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        private static Material Material(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader missing.");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, "Assets/Materials/Graybox/" + name + ".mat");
            return material;
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

        public static void Validate()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var players = UnityEngine.Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None);
            if (players.Length != 1) throw new InvalidOperationException("Expected one player.");
            var data = new SerializedObject(players[0]);
            if (data.FindProperty("inputActions").objectReferenceValue == null ||
                data.FindProperty("cameraPivot").objectReferenceValue == null)
                throw new InvalidOperationException("Player references missing.");
            if (UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length != 1 ||
                UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length != 1)
                throw new InvalidOperationException("Expected one camera and listener.");
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) > 0)
                        throw new InvalidOperationException("Missing script: " + item.name);
            Physics.SyncTransforms();
            CharacterController controller = players[0].GetComponent<CharacterController>();
            for (int i = 0; i < 20; i++) controller.Move(Vector3.down * 0.1f);
            if (!controller.isGrounded) throw new InvalidOperationException("Player failed to ground.");
            for (int i = 0; i < 200; i++) controller.Move(new Vector3(0, -0.02f, 0.1f));
            if (controller.transform.position.z > -1.7f || controller.transform.position.z < -2.1f)
                throw new InvalidOperationException("Player did not stop at obstacle: " + controller.transform.position);
            Debug.Log("MOVEMENT_GRAYBOX_COLLISION_CHECK_PASSED");
        }
    }
}
