using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SignalLost.Editor
{
    /// <summary>
    /// Cavendish redesigned as a normal four-legged capybara in a space suit with a clear bubble helmet. The model is
    /// made in Blender (tools/blender/cavendish_capybara.py, exported to Prefabs/NPC/Cavendish/Models); this builds its
    /// URP materials and the prefab, and puts him in the Testing Sandbox to look at. The story scenes still use
    /// GrayboxProps/CapybaraAstronaut. Menu: Signal Lost > NPC.
    /// </summary>
    internal static class CavendishCapybaraBuilder
    {
        private const string Folder = "Assets/Prefabs/NPC/Cavendish";
        private const string ModelPath = Folder + "/Models/CavendishCapybara.fbx";
        private const string MaterialFolder = Folder + "/Materials";
        public const string PrefabPath = Folder + "/CavendishCapybara.prefab";
        private const string InstanceName = "Cavendish (Capybara)";

        // Blender material name -> colour (sRGB), smoothness, metallic.
        private static readonly Dictionary<string, (Color colour, float smooth, float metal)> Looks = new Dictionary<string, (Color, float, float)>
        {
            { "CavFur", (Rgb(158, 98, 52), 0.15f, 0f) },
            { "CavFurDark", (Rgb(96, 70, 52), 0.15f, 0f) },
            { "CavMuzzle", (Rgb(124, 88, 62), 0.15f, 0f) },
            { "CavNose", (Rgb(38, 34, 34), 0.45f, 0f) },
            { "CavEye", (Rgb(14, 12, 12), 0.9f, 0f) },
            { "CavSuit", (Rgb(232, 232, 226), 0.3f, 0f) },
            { "CavAccent", (Rgb(240, 118, 28), 0.35f, 0f) },
            { "CavMetal", (Rgb(122, 128, 136), 0.6f, 0.6f) },
            { "CavBoot", (Rgb(52, 52, 58), 0.25f, 0f) },
            { "CavPatch", (Rgb(48, 92, 170), 0.3f, 0f) },
            { "CavLight", (Rgb(255, 70, 50), 0.5f, 0f) },
            { "CavShine", (Rgb(255, 255, 255), 0.9f, 0f) },
            { "CavHelmetGlass", (new Color(0.82f, 0.92f, 0.98f, 0.12f), 0.85f, 0f) },
        };

        private static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

        [MenuItem("Signal Lost/NPC/Build Cavendish Capybara")]
        public static void Build()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            if (importer == null) throw new System.Exception("Missing " + ModelPath + ": run tools/blender/cavendish_capybara.py in Blender first.");
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.SaveAndReimport();

            // Which material each submesh had in Blender, read off the imported model.
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var root = new GameObject("CavendishCapybara");
            var body = new GameObject("Model").transform;
            body.SetParent(root.transform, false);
            foreach (MeshRenderer source in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                Mesh mesh = source.GetComponent<MeshFilter>().sharedMesh;
                var part = new GameObject(source.name).transform;
                part.SetParent(body, false);
                part.localPosition = source.transform.localPosition;
                part.localRotation = source.transform.localRotation;
                part.localScale = source.transform.localScale;
                part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = part.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = source.sharedMaterials.Select(m => Material(m != null ? m.name : "CavSuit")).ToArray();
                bool glass = source.sharedMaterials.Any(m => m != null && m.name == "CavHelmetGlass");
                renderer.shadowCastingMode = glass ? ShadowCastingMode.Off : ShadowCastingMode.On;
            }

            // Face +Z.
            Bounds bounds = Bounds(body);
            Vector3 nose = body.Find("CavendishHead").GetComponent<MeshRenderer>().bounds.center; // the head is the front
            Vector3 forward = Vector3.ProjectOnPlane(nose - bounds.center, Vector3.up).normalized;
            body.localRotation = Quaternion.FromToRotation(forward, Vector3.forward) * body.localRotation;
            bounds = Bounds(body);
            body.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            bounds = Bounds(body);

            // The moving parts: ears and eyes ride on the head, which turns at the neck.
            Transform Part(string partName) => body.Find(partName) ?? throw new System.Exception("The Cavendish model has no " + partName + ".");
            Transform head = Part("CavendishHead");
            foreach (string partName in new[] { "CavendishEarL", "CavendishEarR", "CavendishEyeL", "CavendishEyeR" })
                Part(partName).SetParent(head, true);
            var idle = new SerializedObject(root.AddComponent<SignalLost.NPC.CavendishIdle>());
            idle.FindProperty("head").objectReferenceValue = head;
            idle.FindProperty("body").objectReferenceValue = Part("CavendishBody");
            idle.FindProperty("antennaLight").objectReferenceValue = Part("CavendishAntennaLight");
            SerializedProperty ears = idle.FindProperty("ears"), eyes = idle.FindProperty("eyes");
            ears.arraySize = eyes.arraySize = 2;
            ears.GetArrayElementAtIndex(0).objectReferenceValue = head.Find("CavendishEarL");
            ears.GetArrayElementAtIndex(1).objectReferenceValue = head.Find("CavendishEarR");
            eyes.GetArrayElementAtIndex(0).objectReferenceValue = head.Find("CavendishEyeL");
            eyes.GetArrayElementAtIndex(1).objectReferenceValue = head.Find("CavendishEyeR");
            idle.ApplyModifiedPropertiesWithoutUndo();

            var collider = root.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size;
            var anchor = new GameObject("DialogueAnchor").transform;
            anchor.SetParent(root.transform, false);
            anchor.localPosition = new Vector3(0f, bounds.max.y + 0.35f, Mathf.Min(bounds.max.z - 0.4f, 0.4f));

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"CAVENDISH_PREFAB built {PrefabPath}: {bounds.size.x:0.00} x {bounds.size.y:0.00} x {bounds.size.z:0.00} m");
        }

        [MenuItem("Signal Lost/NPC/Place Cavendish Capybara in Testing Sandbox")]
        public static void PlaceInSandbox()
        {
            Scene scene = EditorSceneManager.OpenScene(CoreSceneSetup.SandboxPath, OpenSceneMode.Single);
            foreach (GameObject old in scene.GetRootGameObjects().Where(g => g.name == InstanceName).ToArray())
                Object.DestroyImmediate(old);

            // Beside the old Cavendish, in front of where the player starts.
            Transform player = scene.GetRootGameObjects().Select(g => g.transform).FirstOrDefault(t => t.name == "Player");
            Transform oldCavendish = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == "CapybaraAstronaut");
            Vector3 start = player != null ? player.position : Vector3.zero;
            Vector3 spot = oldCavendish != null ? oldCavendish.position + Vector3.right * 3.5f : start + new Vector3(3f, 0f, -3f);
            Physics.SyncTransforms();
            if (Physics.Raycast(spot + Vector3.up * 30f, Vector3.down, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore))
                spot.y = hit.point.y;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            instance.name = InstanceName;
            Vector3 look = Vector3.ProjectOnPlane(start - spot, Vector3.up);
            // Turned three-quarters from the player so his profile (and the head in the helmet) reads from the start.
            instance.transform.SetPositionAndRotation(spot, (look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity) * Quaternion.Euler(0f, 50f, 0f));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"CAVENDISH_PLACED at {spot} facing the player at {start}");
        }

        public static void BuildAndPlaceBatch()
        {
            Build();
            PlaceInSandbox();
            Debug.Log("CAVENDISH_BUILD_PASS");
        }

        private static Material Material(string name)
        {
            if (!Looks.TryGetValue(name, out var look)) look = Looks["CavSuit"];
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder(Folder, "Materials");
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", look.colour);
            material.SetFloat("_Smoothness", look.smooth);
            material.SetFloat("_Metallic", look.metal);
            if (name == "CavLight" || name == "CavShine")
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", look.colour * (name == "CavShine" ? 1.2f : 2.5f));
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            if (name == "CavHelmetGlass")
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_Cull", (float)CullMode.Back);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                // Clear, not mirrored: reflections fade with the glass's alpha and only a soft highlight shows.
                material.SetFloat("_BlendModePreserveSpecular", 0f);
                material.DisableKeyword("_ALPHAMODULATE_ON");
                material.SetFloat("_EnvironmentReflections", 0f);
                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
                material.SetShaderPassEnabled("DepthOnly", false);
                material.SetShaderPassEnabled("ShadowCaster", false);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Bounds Bounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<MeshFilter>();
            Bounds? total = null;
            Transform top = root.parent != null ? root.parent : root;
            foreach (MeshFilter filter in renderers)
                foreach (Vector3 v in filter.sharedMesh.vertices)
                {
                    Vector3 p = top.InverseTransformPoint(filter.transform.TransformPoint(v));
                    if (total == null) total = new Bounds(p, Vector3.zero);
                    else { Bounds b = total.Value; b.Encapsulate(p); total = b; }
                }
            return total ?? new Bounds();
        }
    }
}
