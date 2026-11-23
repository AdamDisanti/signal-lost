using System.Collections.Generic;
using System.Linq;
using SignalLost.NPC;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Editor
{
    /// <summary>
    /// Prefabs for the desert revamp's locals (docs/desert-revamp.md, 11-22): Doris at Mirage Springs, Marshal Mo at the
    /// Dustbowl Speedway and Old Gus in Rattlesnake Canyon. Their models come from tools/blender/desert_npcs.py (built in
    /// Blender through the Blender MCP), split into the parts that move; like Cavendish's builder, this turns each to face
    /// +Z, stands it on its feet, gives it the project's own URP materials by name, and adds <see cref="NpcActor"/>
    /// (<see cref="NpcActor.Role.Local"/>), its speech bubble and <see cref="LocalIdle"/>.
    /// </summary>
    public static class DesertNpcBuilder
    {
        public const string Folder = "Assets/Prefabs/NPC/Desert";
        private const string MaterialFolder = Folder + "/Materials";

        public sealed class Spec
        {
            public string Key, Name;
            public string[] SmallTalk;
            public bool Wings;
        }

        public static readonly Spec Doris = new Spec
        {
            Key = "Doris", Name = "Doris", Wings = true,
            SmallTalk = new[] { "Welcome to Mirage Springs, dear. Towels are complimentary.", "Ninety years at this spring. Nothing ever happens. Nothing nice." }
        };
        public static readonly Spec Mo = new Spec
        {
            Key = "Mo", Name = "Marshal Mo",
            SmallTalk = new[] { "Track's closed. Form 27-B. In triplicate.", "That's a four out of ten, that is. Your stance. Very four." }
        };
        public static readonly Spec Gus = new Spec
        {
            Key = "Gus", Name = "Old Gus",
            SmallTalk = new[] { "EH? SPEAK UP, SONNY.", "Eighty years I've been about to strike it rich. Any day now." }
        };

        public static string PrefabPath(Spec spec) => $"{Folder}/Desert{spec.Key}.prefab";

        private static readonly Dictionary<string, (Color colour, float smooth, float metal, float glow)> Looks = new Dictionary<string, (Color, float, float, float)>
        {
            ["DorisFeather"] = (Rgb(150, 98, 214), 0.2f, 0f, 0f), ["DorisFeatherDark"] = (Rgb(98, 62, 162), 0.2f, 0f, 0f), // (saturated: the warm desert light greys lavender)
            ["DorisPlume"] = (Rgb(246, 240, 236), 0.2f, 0f, 0f), ["DorisLeg"] = (Rgb(232, 150, 120), 0.25f, 0f, 0f),
            ["DorisBeak"] = (Rgb(246, 168, 62), 0.4f, 0f, 0f), ["DorisTowel"] = (Rgb(246, 246, 240), 0.1f, 0f, 0f),
            ["DorisStripe"] = (Rgb(62, 172, 162), 0.1f, 0f, 0f), ["DorisLens"] = (Rgb(30, 30, 44), 0.85f, 0.2f, 0f),
            ["DorisFrame"] = (Rgb(240, 92, 152), 0.5f, 0f, 0f),
            ["MoFur"] = (Rgb(206, 160, 102), 0.15f, 0f, 0f), ["MoFurLight"] = (Rgb(236, 206, 152), 0.15f, 0f, 0f),
            ["MoFurDark"] = (Rgb(120, 86, 56), 0.15f, 0f, 0f), ["MoPatch"] = (Rgb(70, 50, 40), 0.2f, 0f, 0f),
            ["MoNose"] = (Rgb(40, 30, 30), 0.5f, 0f, 0f), ["MoVest"] = (Rgb(255, 122, 22), 0.3f, 0f, 0f),
            ["MoStripe"] = (Rgb(232, 232, 222), 0.6f, 0f, 0.5f), ["MoCapBrim"] = (Rgb(40, 40, 48), 0.3f, 0f, 0f),
            ["MoHorn"] = (Rgb(242, 202, 42), 0.6f, 0.2f, 0f), ["MoPole"] = (Rgb(92, 92, 98), 0.6f, 0.5f, 0f),
            ["MoFlagWhite"] = (Rgb(240, 240, 236), 0.2f, 0f, 0f), ["MoFlagBlack"] = (Rgb(26, 26, 30), 0.2f, 0f, 0f),
            ["GusSkin"] = (Rgb(150, 160, 108), 0.2f, 0f, 0f), ["GusSkinDark"] = (Rgb(108, 118, 78), 0.2f, 0f, 0f),
            ["GusShell"] = (Rgb(120, 90, 56), 0.35f, 0f, 0f), ["GusPlate"] = (Rgb(166, 126, 76), 0.35f, 0f, 0f),
            ["GusRim"] = (Rgb(88, 64, 40), 0.3f, 0f, 0f), ["GusPlastron"] = (Rgb(212, 192, 132), 0.25f, 0f, 0f),
            ["GusBeard"] = (Rgb(242, 242, 236), 0.1f, 0f, 0f), ["GusHat"] = (Rgb(152, 106, 62), 0.15f, 0f, 0f),
            ["GusHatBand"] = (Rgb(90, 60, 36), 0.2f, 0f, 0f), ["GusBrass"] = (Rgb(204, 162, 62), 0.65f, 0.6f, 0f),
            ["GusRod"] = (Rgb(132, 96, 60), 0.15f, 0f, 0f),
            ["NpcEye"] = (Rgb(16, 14, 14), 0.85f, 0f, 0f), ["NpcShine"] = (Rgb(255, 255, 255), 0.9f, 0f, 1.2f),
        };

        private static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

        [MenuItem("Signal Lost/NPC/Build Desert Locals")]
        public static void BuildAll()
        {
            foreach (Spec spec in new[] { Doris, Mo, Gus }) Build(spec);
            AssetDatabase.SaveAssets();
        }

        public static GameObject Build(Spec spec)
        {
            string modelPath = $"{Folder}/Models/Desert{spec.Key}.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            if (importer == null) throw new System.Exception("Missing " + modelPath + ": run tools/blender/desert_npcs.py in Blender first.");
            if (importer.importAnimation || importer.animationType != ModelImporterAnimationType.None)
            {
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.SaveAndReimport();
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var root = new GameObject(spec.Name);
            var body = new GameObject("Model").transform;
            body.SetParent(root.transform, false);
            foreach (MeshRenderer source in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                var part = new GameObject(source.name).transform;
                part.SetParent(body, false);
                part.localPosition = source.transform.localPosition;
                part.localRotation = source.transform.localRotation;
                part.localScale = source.transform.localScale;
                part.gameObject.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
                var renderer = part.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = source.sharedMaterials.Select(m => Material(m != null ? m.name : "NpcEye")).ToArray();
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }
            Transform Part(string suffix) => body.Find(spec.Key + suffix);

            // Face +Z (the head is the front), stand on the ground, centred.
            Transform head = Part("Head") ?? throw new System.Exception(spec.Key + " has no head part.");
            Bounds bounds = Bounds(body);
            Vector3 forward = Vector3.ProjectOnPlane(head.GetComponent<MeshRenderer>().bounds.center - bounds.center, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            body.localRotation = Quaternion.FromToRotation(forward.normalized, Vector3.forward) * body.localRotation;
            bounds = Bounds(body);
            body.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            bounds = Bounds(body);

            var idle = new SerializedObject(root.AddComponent<LocalIdle>());
            idle.FindProperty("head").objectReferenceValue = head;
            idle.FindProperty("body").objectReferenceValue = Part("Body");
            idle.FindProperty("wings").boolValue = spec.Wings;
            var limbs = new List<Transform>();
            foreach (string limb in spec.Wings ? new[] { "WingL", "WingR" } : new[] { "ArmL", "ArmR" })
                if (Part(limb) != null) limbs.Add(Part(limb));
            SerializedProperty limbList = idle.FindProperty("limbs");
            limbList.arraySize = limbs.Count;
            for (int i = 0; i < limbs.Count; i++) limbList.GetArrayElementAtIndex(i).objectReferenceValue = limbs[i];
            idle.ApplyModifiedPropertiesWithoutUndo();

            var collider = root.AddComponent<CapsuleCollider>();
            collider.center = bounds.center;
            collider.height = bounds.size.y;
            collider.radius = Mathf.Max(0.3f, Mathf.Min(bounds.size.x, bounds.size.z) * 0.45f);

            var actor = new SerializedObject(root.AddComponent<NpcActor>());
            actor.FindProperty("role").enumValueIndex = (int)NpcActor.Role.Local;
            actor.FindProperty("displayName").stringValue = spec.Name;
            SerializedProperty talk = actor.FindProperty("smallTalk");
            talk.arraySize = spec.SmallTalk.Length;
            for (int i = 0; i < spec.SmallTalk.Length; i++) talk.GetArrayElementAtIndex(i).stringValue = spec.SmallTalk[i];
            actor.ApplyModifiedPropertiesWithoutUndo();

            var voice = new GameObject(spec.Name + " Dialogue");
            voice.transform.SetParent(root.transform, false);
            voice.transform.localPosition = new Vector3(0f, bounds.max.y + 0.4f, 0f);
            var dialogue = new SerializedObject(voice.AddComponent<NpcDialogue>());
            dialogue.FindProperty("speaker").stringValue = spec.Name;
            dialogue.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<Scanner.QuestSignalSource>().enabled = false;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(spec));
            Object.DestroyImmediate(root);
            Debug.Log($"DESERT_NPC built {PrefabPath(spec)}: {bounds.size.x:0.00} x {bounds.size.y:0.00} x {bounds.size.z:0.00} m");
            return saved;
        }

        private static Bounds Bounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            Bounds b = renderers[0].bounds;
            foreach (MeshRenderer r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        private static Material Material(string name)
        {
            if (!Looks.TryGetValue(name, out var look)) look = (Color.magenta, 0f, 0f, 0f);
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
            if (look.glow > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", look.colour * look.glow);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
