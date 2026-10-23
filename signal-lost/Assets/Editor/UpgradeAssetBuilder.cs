using System;
using System.Collections.Generic;
using SignalLost.Inventory;
using UnityEditor;
using UnityEngine;

namespace SignalLost.Editor
{
    /// <summary>
    /// Builds a small model and an icon for every upgrade and supply in <see cref="UpgradeCatalog"/>, and the
    /// ItemCatalog the shop and pockets menu read them from. Upgrade models are the parts the pockets menu's mechanic
    /// wears (a coil on the laser, fins, an antenna, padding...); each is centred on its own origin and GearFigure
    /// places it on the right tool or body part. Supplies get a prop (pill bottle, snack bag).
    /// Menu: Signal Lost > Items > Build Upgrade Assets.
    /// </summary>
    internal static class UpgradeAssetBuilder
    {
        public const string Folder = "Assets/Prefabs/Items/Upgrades";
        public const string CatalogPath = "Assets/Resources/SignalLost/ItemCatalog.asset";

        private enum Mat { Copper, Silver, Iron, Red, Pad, PadStitch, Rubber, PillOrange, PillCap, Label, Foil, FoilDark }

        private static readonly Dictionary<Mat, Material> Materials = new Dictionary<Mat, Material>();

        private static readonly Dictionary<string, Action<FlatMeshKit<Mat>>> Models = new Dictionary<string, Action<FlatMeshKit<Mat>>>
        {
            ["overclocked_coil"] = Coil,
            ["quickcool_fins"] = Fins,
            ["heavy_slam"] = StrikePlate,
            ["longnose_antenna"] = Antenna,
            ["castiron_liver"] = FlaskBands,
            ["padded_overalls"] = Padding,
            ["slick_kneepads"] = Kneepad,
            ["hangover_pills"] = PillBottle,
            ["mystery_snack"] = SnackBag
        };

        [MenuItem("Signal Lost/Items/Build Upgrade Assets")]
        public static void Build()
        {
            EnsureFolder(Folder + "/Meshes");
            EnsureFolder(Folder + "/Materials");
            EnsureFolder(Folder + "/Icons");
            EnsureFolder("Assets/Resources/SignalLost");
            Materials.Clear();
            BuildMaterials();

            var entries = new List<ItemCatalog.Entry>();
            foreach (Upgrade upgrade in UpgradeCatalog.All)
            {
                if (!Models.TryGetValue(upgrade.Id, out Action<FlatMeshKit<Mat>> build)) continue;
                var kit = new FlatMeshKit<Mat>();
                build(kit);
                Mesh mesh = SaveMesh(kit.Build("Upgrade_" + upgrade.Id));
                var root = new GameObject(upgrade.Name);
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                var materials = new Material[mesh.subMeshCount];
                for (int i = 0; i < materials.Length; i++) materials[i] = Materials[FlatMeshKit<Mat>.MaterialOf(mesh, i)];
                root.AddComponent<MeshRenderer>().sharedMaterials = materials;
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + upgrade.Id + ".prefab");
                UnityEngine.Object.DestroyImmediate(root);
                Sprite icon = ItemIconRenderer.Render(prefab, Folder + "/Icons/" + upgrade.Id + ".png", new Vector3(-25f, 35f, 0f), 256, 1.25f);
                entries.Add(new ItemCatalog.Entry { id = upgrade.Id, model = prefab, icon = icon });
            }

            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ItemCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var data = new SerializedObject(catalog);
            SerializedProperty list = data.FindProperty("entries");
            list.arraySize = entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("id").stringValue = entries[i].id;
                entry.FindPropertyRelative("model").objectReferenceValue = entries[i].model;
                entry.FindPropertyRelative("icon").objectReferenceValue = entries[i].icon;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("UPGRADE_ASSETS_BUILT");
        }

        // ---------- Models (centred on the origin; lengths along +Z for tool parts) ----------

        // Three copper windings round a barrel 0.09 m across, with the leads.
        private static void Coil(FlatMeshKit<Mat> kit)
        {
            for (int i = 0; i < 3; i++)
                kit.Add(Ring(0.065f, 0.02f, 10), Matrix4x4.TRS(new Vector3(0f, 0f, -0.06f + i * 0.06f), Quaternion.Euler(90f, 0f, 0f), Vector3.one), Mat.Copper);
            kit.Add(LowPolyMesh.Tube("Lead", new[] { new Vector3(0.06f, 0.03f, -0.06f), new Vector3(0.09f, 0.08f, -0.1f), new Vector3(0.05f, 0.1f, -0.16f) }, 0.008f, 4), Matrix4x4.identity, Mat.Copper);
        }

        // A row of cooling fins on a base rail.
        private static void Fins(FlatMeshKit<Mat> kit)
        {
            kit.Box(Vector3.zero, new Vector3(0.08f, 0.02f, 0.3f), Quaternion.identity, Mat.Iron);
            for (int i = 0; i < 7; i++)
                kit.Box(new Vector3(0f, 0.05f, -0.135f + i * 0.045f), new Vector3(0.14f, 0.08f, 0.012f), Quaternion.identity, Mat.Silver);
        }

        // An iron striking plate with bolts, facing +Z.
        private static void StrikePlate(FlatMeshKit<Mat> kit)
        {
            kit.Box(Vector3.zero, new Vector3(0.22f, 0.2f, 0.05f), Quaternion.identity, Mat.Iron);
            kit.Box(new Vector3(0f, 0f, 0.035f), new Vector3(0.17f, 0.15f, 0.03f), Quaternion.identity, Mat.Iron);
            foreach (Vector2 c in new[] { new Vector2(-0.085f, -0.075f), new Vector2(0.085f, -0.075f), new Vector2(-0.085f, 0.075f), new Vector2(0.085f, 0.075f) })
                kit.Add(LowPolyMesh.Icosphere("Bolt", 0), Matrix4x4.TRS(new Vector3(c.x, c.y, 0.03f), Quaternion.identity, Vector3.one * 0.02f), Mat.Silver);
            kit.Box(new Vector3(0f, 0f, 0.052f), new Vector3(0.1f, 0.02f, 0.006f), Quaternion.identity, Mat.Red);
        }

        // A telescoping antenna with a red tip, base at the origin, pointing up.
        private static void Antenna(FlatMeshKit<Mat> kit)
        {
            kit.Add(LowPolyMesh.Lathe("Base", new[] { new Vector2(0.001f, 0f), new Vector2(0.03f, 0f), new Vector2(0.025f, 0.03f), new Vector2(0.001f, 0.03f) }, 8), Matrix4x4.identity, Mat.Iron);
            float y = 0.03f, r = 0.012f;
            for (int i = 0; i < 3; i++)
            {
                kit.Add(LowPolyMesh.Lathe("Section", new[] { new Vector2(0.001f, y), new Vector2(r, y), new Vector2(r, y + 0.12f), new Vector2(0.001f, y + 0.12f) }, 6), Matrix4x4.identity, Mat.Silver);
                y += 0.12f;
                r *= 0.72f;
            }
            kit.Add(LowPolyMesh.Icosphere("Tip", 1), Matrix4x4.TRS(new Vector3(0f, y + 0.015f, 0f), Quaternion.identity, Vector3.one * 0.035f), Mat.Red);
        }

        // Two riveted iron bands for a flask about 0.2 x 0.09 m.
        private static void FlaskBands(FlatMeshKit<Mat> kit)
        {
            foreach (float y in new[] { -0.06f, 0.06f })
            {
                kit.Box(new Vector3(0f, y, 0f), new Vector3(0.21f, 0.03f, 0.1f), Quaternion.identity, Mat.Iron);
                foreach (float x in new[] { -0.07f, 0f, 0.07f })
                    kit.Add(LowPolyMesh.Icosphere("Rivet", 0), Matrix4x4.TRS(new Vector3(x, y, 0.052f), Quaternion.identity, Vector3.one * 0.012f), Mat.Silver);
            }
        }

        // Quilted padding: a chest plate and two shoulder pads, for a body about 0.5 m wide.
        private static void Padding(FlatMeshKit<Mat> kit)
        {
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
                kit.Box(new Vector3(-0.1f + col * 0.1f, 0.1f - row * 0.1f, 0f), new Vector3(0.095f, 0.095f, 0.04f), Quaternion.identity, Mat.Pad);
            kit.Box(new Vector3(0f, 0f, -0.012f), new Vector3(0.31f, 0.31f, 0.02f), Quaternion.identity, Mat.PadStitch);
            foreach (float x in new[] { -0.27f, 0.27f })
            {
                kit.Box(new Vector3(x, 0.19f, -0.05f), new Vector3(0.14f, 0.07f, 0.2f), Quaternion.Euler(0f, 0f, x < 0f ? 18f : -18f), Mat.Pad);
                kit.Box(new Vector3(x, 0.15f, -0.05f), new Vector3(0.15f, 0.02f, 0.21f), Quaternion.Euler(0f, 0f, x < 0f ? 18f : -18f), Mat.PadStitch);
            }
        }

        // One knee pad: a rubber cup with a polished slide plate.
        private static void Kneepad(FlatMeshKit<Mat> kit)
        {
            kit.Box(Vector3.zero, new Vector3(0.14f, 0.16f, 0.05f), Quaternion.identity, Mat.Rubber);
            kit.Box(new Vector3(0f, 0f, 0.03f), new Vector3(0.11f, 0.12f, 0.015f), Quaternion.identity, Mat.Silver);
            kit.Box(new Vector3(0f, 0.09f, -0.01f), new Vector3(0.15f, 0.025f, 0.06f), Quaternion.identity, Mat.Rubber);
            kit.Box(new Vector3(0f, -0.09f, -0.01f), new Vector3(0.15f, 0.025f, 0.06f), Quaternion.identity, Mat.Rubber);
        }

        // An orange pill bottle with a white cap and a label.
        private static void PillBottle(FlatMeshKit<Mat> kit)
        {
            kit.Add(LowPolyMesh.Lathe("Bottle", new[] { new Vector2(0.001f, 0f), new Vector2(0.045f, 0f), new Vector2(0.048f, 0.01f), new Vector2(0.048f, 0.12f), new Vector2(0.04f, 0.13f), new Vector2(0.001f, 0.13f) }, 10), Matrix4x4.identity, Mat.PillOrange);
            kit.Add(LowPolyMesh.Lathe("Cap", new[] { new Vector2(0.001f, 0.125f), new Vector2(0.052f, 0.125f), new Vector2(0.052f, 0.16f), new Vector2(0.001f, 0.16f) }, 10), Matrix4x4.identity, Mat.PillCap);
            kit.Add(LowPolyMesh.Lathe("Label", new[] { new Vector2(0.049f, 0.035f), new Vector2(0.05f, 0.035f), new Vector2(0.05f, 0.095f), new Vector2(0.049f, 0.095f) }, 10), Matrix4x4.identity, Mat.Label);
        }

        // A crinkled foil snack bag with crimped ends and a question-mark stripe.
        private static void SnackBag(FlatMeshKit<Mat> kit)
        {
            kit.Box(new Vector3(0f, 0.09f, 0f), new Vector3(0.14f, 0.15f, 0.05f), Quaternion.Euler(0f, 0f, 3f), Mat.Foil);
            kit.Box(new Vector3(0f, 0.175f, 0f), new Vector3(0.15f, 0.025f, 0.012f), Quaternion.identity, Mat.FoilDark);
            kit.Box(new Vector3(0f, 0.005f, 0f), new Vector3(0.15f, 0.025f, 0.012f), Quaternion.identity, Mat.FoilDark);
            kit.Box(new Vector3(0f, 0.1f, 0.027f), new Vector3(0.03f, 0.05f, 0.004f), Quaternion.identity, Mat.Label);
            kit.Box(new Vector3(0f, 0.065f, 0.027f), new Vector3(0.012f, 0.012f, 0.004f), Quaternion.identity, Mat.Label);
        }

        private static Mesh Ring(float radius, float thickness, int sides) => LowPolyMesh.Lathe("Ring", new[]
        {
            new Vector2(radius - thickness, -thickness * 0.5f), new Vector2(radius, -thickness * 0.5f),
            new Vector2(radius, thickness * 0.5f), new Vector2(radius - thickness, thickness * 0.5f), new Vector2(radius - thickness, -thickness * 0.5f)
        }, sides);

        // ---------- Materials and assets ----------

        private static void BuildMaterials()
        {
            Lit(Mat.Copper, new Color(0.85f, 0.45f, 0.2f), 0.9f, 0.6f);
            Lit(Mat.Silver, new Color(0.82f, 0.84f, 0.86f), 0.9f, 0.6f);
            Lit(Mat.Iron, new Color(0.3f, 0.3f, 0.32f), 0.8f, 0.4f);
            Lit(Mat.Red, new Color(0.85f, 0.15f, 0.12f), 0f, 0.5f);
            Lit(Mat.Pad, new Color(0.22f, 0.24f, 0.3f), 0f, 0.2f);
            Lit(Mat.PadStitch, new Color(0.93f, 0.48f, 0.15f), 0f, 0.3f);
            Lit(Mat.Rubber, new Color(0.1f, 0.1f, 0.11f), 0f, 0.3f);
            Lit(Mat.PillOrange, new Color(0.95f, 0.55f, 0.15f), 0f, 0.7f);
            Lit(Mat.PillCap, new Color(0.95f, 0.95f, 0.93f), 0f, 0.4f);
            Lit(Mat.Label, new Color(0.96f, 0.94f, 0.85f), 0f, 0.2f);
            Lit(Mat.Foil, new Color(0.55f, 0.3f, 0.75f), 0.7f, 0.65f);
            Lit(Mat.FoilDark, new Color(0.35f, 0.18f, 0.5f), 0.7f, 0.5f);
        }

        private static void Lit(Mat key, Color color, float metallic, float smoothness)
        {
            string path = Folder + "/Materials/Upgrade" + key + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            Materials[key] = material;
        }

        private static Mesh SaveMesh(Mesh mesh)
        {
            string path = Folder + "/Meshes/" + mesh.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            string order = FlatMeshKit<Mat>.Order(mesh);
            EditorUtility.CopySerialized(mesh, existing);
            FlatMeshKit<Mat>.Remember(existing, order);
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            return existing;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
