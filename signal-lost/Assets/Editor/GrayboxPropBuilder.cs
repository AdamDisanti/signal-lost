using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SignalLost.Editor
{
    /// <summary>Builds missing prop prefabs in isolation. Never edits the open scene or existing prefabs.</summary>
    public static class GrayboxPropBuilder
    {
        public const string Root = "Assets/Prefabs/Enviornment objects/GrayboxProps";
        private static readonly string[] Names =
            { "CapybaraAstronaut", "BurntRockBasin", "AlcoholFlask", "GrayboxExitGate" };

        [Serializable] private class Palette { public Entry[] materials; }
        [Serializable] private class Entry
        {
            public string name;
            public string hex;
            public float metallic;
            public float smoothness;
        }

        [MenuItem("Signal Lost/Graybox Props/Create Missing Prefabs")]
        public static void CreateMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Leave Play Mode before building prop assets.");
            EnsureFolder(Root + "/Materials");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader not available.");
            TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Models/Palette.json");
            if (json == null) throw new InvalidOperationException("Graybox prop palette is missing.");
            Palette palette = JsonUtility.FromJson<Palette>(json.text);
            var materials = new Dictionary<string, Material>();
            foreach (Entry entry in palette.materials)
            {
                string path = Root + "/Materials/" + entry.name + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    if (!ColorUtility.TryParseHtmlString("#" + entry.hex, out Color color))
                        throw new InvalidOperationException("Invalid palette color: " + entry.hex);
                    material = new Material(shader) { name = entry.name };
                    material.SetColor("_BaseColor", color);
                    material.SetFloat("_Metallic", entry.metallic);
                    material.SetFloat("_Smoothness", entry.smoothness);
                    AssetDatabase.CreateAsset(material, path);
                }
                materials.Add(entry.name, material);
            }

            foreach (string name in Names)
            {
                string prefabPath = Root + "/" + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) continue;
                string modelPath = Root + "/Models/" + name + ".fbx";
                var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
                if (importer == null) throw new InvalidOperationException("Model missing: " + modelPath);
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.addCollider = false;
                importer.globalScale = 1;
                importer.useFileScale = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                Scene preview = EditorSceneManager.NewPreviewScene();
                try
                {
                    GameObject root = new GameObject(name);
                    SceneManager.MoveGameObjectToScene(root, preview);
                    var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
                    visual.name = "Visual";
                    visual.transform.SetParent(root.transform, false);
                    foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>())
                    {
                        Material[] mapped = renderer.sharedMaterials;
                        for (int i = 0; i < mapped.Length; i++)
                        {
                            if (mapped[i] == null || !materials.TryGetValue(mapped[i].name, out Material mat))
                                throw new InvalidOperationException("Unknown material on " + renderer.name);
                            mapped[i] = mat;
                        }
                        renderer.sharedMaterials = mapped;
                    }
                    AddCollidersAndMarkers(name, root);
                    GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    if (saved == null) throw new InvalidOperationException("Could not save " + prefabPath);
                }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("GRAYBOX_PROPS_CREATED_AND_VALIDATED");
        }

        private static void AddCollidersAndMarkers(string name, GameObject root)
        {
            switch (name)
            {
                case "CapybaraAstronaut":
                    var capsule = root.AddComponent<CapsuleCollider>();
                    capsule.center = new Vector3(0, 1.075f, 0);
                    capsule.height = 2.15f;
                    capsule.radius = 0.50f;
                    Marker(root, "DialogueAnchor", new Vector3(0, 2.45f, 0));
                    break;
                case "BurntRockBasin":
                    // Convex hulls or a single box would close the recess. Use static concave meshes.
                    foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
                    {
                        var meshCollider = filter.gameObject.AddComponent<MeshCollider>();
                        meshCollider.sharedMesh = filter.sharedMesh;
                        meshCollider.convex = false;
                    }
                    Marker(root, "MiningLaserAnchor", new Vector3(0, 0.67f, 0));
                    break;
                case "AlcoholFlask":
                    Box(root, "FlaskCollider", new Vector3(0, 0.16f, 0), new Vector3(0.19f, 0.32f, 0.085f));
                    Marker(root, "GripAnchor", new Vector3(0, 0.13f, 0));
                    break;
                case "GrayboxExitGate":
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Box(root, side < 0 ? "LeftPostCollider" : "RightPostCollider",
                            new Vector3(side * 1.72f, 1.40f, 0), new Vector3(0.70f, 2.80f, 0.78f));
                        Box(root, side < 0 ? "LeftFootCollider" : "RightFootCollider",
                            new Vector3(side * 1.70f, 0.12f, 0), new Vector3(0.86f, 0.24f, 0.90f));
                    }
                    Box(root, "LintelCollider", new Vector3(0, 3.00f, 0), new Vector3(4.10f, 0.40f, 0.80f));
                    Marker(root, "ExitAnchor", new Vector3(0, 0, 1));
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(name));
            }
        }

        private static void Marker(GameObject root, string name, Vector3 position)
        {
            var marker = new GameObject(name);
            marker.transform.SetParent(root.transform, false);
            marker.transform.localPosition = position;
        }

        private static void Box(GameObject root, string name, Vector3 center, Vector3 size)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(root.transform, false);
            var collider = obj.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        [MenuItem("Signal Lost/Graybox Props/Validate Prefabs")]
        public static void Validate()
        {
            var report = new List<string>();
            foreach (string name in Names)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + name + ".prefab");
                if (prefab == null) throw new InvalidOperationException("Missing prefab: " + name);
                GameObject root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab));
                try
                {
                    Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
                    if (renderers.Length == 0) throw new InvalidOperationException("No renderers: " + name);
                    Bounds bounds = renderers[0].bounds;
                    foreach (Renderer renderer in renderers)
                    {
                        bounds.Encapsulate(renderer.bounds);
                        foreach (Material mat in renderer.sharedMaterials)
                            if (mat == null || mat.shader.name != "Universal Render Pipeline/Lit")
                                throw new InvalidOperationException("Missing or non-URP material: " + renderer.name);
                    }
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                            throw new InvalidOperationException("Missing script: " + child.name);
                    foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
                        if (filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0)
                            throw new InvalidOperationException("Missing mesh: " + filter.name);
                    if (Mathf.Abs(bounds.min.y) > 0.025f)
                        throw new InvalidOperationException("Ground pivot/axis error: " + name + " " + bounds);
                    float minHeight = name == "CapybaraAstronaut" ? 2.1f : name == "BurntRockBasin" ? 0.9f :
                        name == "AlcoholFlask" ? 0.30f : 3.1f;
                    float maxHeight = name == "CapybaraAstronaut" ? 2.4f : name == "BurntRockBasin" ? 1.15f :
                        name == "AlcoholFlask" ? 0.34f : 3.3f;
                    if (bounds.size.y < minHeight || bounds.size.y > maxHeight)
                        throw new InvalidOperationException("Incorrect import scale/axis: " + name + " " + bounds);
                    Collider[] colliders = root.GetComponentsInChildren<Collider>();
                    if (colliders.Length == 0) throw new InvalidOperationException("No colliders: " + name);
                    Physics.SyncTransforms();
                    if (name == "BurntRockBasin")
                    {
                        float nearest = float.PositiveInfinity;
                        foreach (Collider collider in colliders)
                            if (collider.Raycast(new Ray(new Vector3(0, 2, 0), Vector3.down), out RaycastHit hit, 3))
                                nearest = Mathf.Min(nearest, hit.distance);
                        if (Mathf.Abs(nearest - 1.36f) > 0.025f)
                            throw new InvalidOperationException("Basin not recessed: ray distance " + nearest);
                        if (root.transform.Find("MiningLaserAnchor") == null)
                            throw new InvalidOperationException("Missing laser anchor.");
                    }
                    if (name == "GrayboxExitGate")
                    {
                        foreach (float x in new[] { -0.85f, 0f, 0.85f })
                            foreach (float y in new[] { 0.05f, 1f, 2.1f })
                                foreach (Collider collider in colliders)
                                    if (collider.Raycast(new Ray(new Vector3(x, y, -2), Vector3.forward), out _, 4))
                                        throw new InvalidOperationException("Gate passage is blocked.");
                        bool postHit = false;
                        foreach (Collider collider in colliders)
                            postHit |= collider.Raycast(new Ray(new Vector3(1.7f, 1, -2), Vector3.forward), out _, 4);
                        if (!postHit) throw new InvalidOperationException("Gate post collision missing.");
                    }
                    report.Add(name + ": bounds=" + bounds + ", renderers=" + renderers.Length +
                        ", colliders=" + colliders.Length + ", references/URP/scale OK");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            report.Add("Basin floor raycast and clear gate passage/post collision checks passed.");
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/GrayboxPropsValidation.txt", report);
            Debug.Log("GRAYBOX_PROPS_VALIDATED\n" + string.Join("\n", report));
        }
    }
}
