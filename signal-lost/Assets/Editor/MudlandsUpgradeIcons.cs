using System;
using System.Collections.Generic;
using SignalLost.Inventory;
using SignalLost.Mudlands;
using UnityEditor;
using UnityEngine;

namespace SignalLost.Editor
{
    /// <summary>
    /// Shop and pockets icons for the ten Mudlands upgrades (docs/mudlands.md, 7). Each gets a small model built with
    /// the Mudlands mesh kit, saved as a prefab, and rendered to an icon by <see cref="ItemIconRenderer"/>, the same
    /// way <see cref="UpgradeAssetBuilder"/> does the tutorial's; that builder adds these entries to the ItemCatalog.
    /// The models are only for icons: the pockets menu's mechanic doesn't wear them (GearFigure skips unknown ids).
    /// </summary>
    internal static class MudlandsUpgradeIcons
    {
        private const string Folder = UpgradeAssetBuilder.Folder + "/Mudlands";

        private static readonly Color Chrome = new Color(0.78f, 0.8f, 0.82f);
        private static readonly Color Copper = new Color(0.78f, 0.45f, 0.25f);
        private static readonly Color Glass = new Color(0.62f, 0.34f, 0.1f);
        private static readonly Color BootGreen = new Color(0.22f, 0.36f, 0.2f);
        private static readonly Color Dynamite = new Color(0.78f, 0.16f, 0.12f);
        private static readonly Color Spring = new Color(0.95f, 0.75f, 0.2f);
        private static readonly Color Spark = new Color(0.55f, 0.9f, 1f);

        private static readonly Dictionary<string, Action<MeshKit>> Models = new Dictionary<string, Action<MeshKit>>
        {
            ["swamp_waders"] = Waders,
            ["bootleg_capacitor"] = Capacitor,
            ["moonshine_distiller"] = Distiller,
            ["reinforced_bumper"] = Bumper,
            ["bigger_boom"] = BiggerBoom,
            ["extra_proof"] = ExtraProof,
            ["shockwave_shocks"] = Shocks,
            ["mud_tires"] = MudTire,
            ["quick_chug"] = Funnel,
            ["spare_keys"] = Keys
        };

        /// <summary>Builds every model and icon; returns the catalog entries.</summary>
        public static List<ItemCatalog.Entry> Build()
        {
            EnsureFolder(Folder + "/Materials");
            var entries = new List<ItemCatalog.Entry>();
            foreach (KeyValuePair<string, Action<MeshKit>> model in Models)
            {
                if (UpgradeCatalog.Find(model.Key) == null) continue;
                var kit = new MeshKit();
                model.Value(kit);
                Mesh mesh = kit.Build("Upgrade_" + model.Key);
                string meshPath = Folder + "/" + model.Key + "_mesh.asset";
                AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(mesh, meshPath);
                var materials = new Material[kit.SubmeshCount];
                for (int i = 0; i < materials.Length; i++)
                {
                    (Color color, float glow) = kit.SubmeshLook(i);
                    materials[i] = SavedMaterial(color, glow);
                }
                var root = new GameObject(model.Key);
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterials = materials;
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + model.Key + ".prefab");
                UnityEngine.Object.DestroyImmediate(root);
                Sprite icon = ItemIconRenderer.Render(prefab, Folder + "/" + model.Key + ".png", new Vector3(-25f, 35f, 0f), 256, 1.25f);
                entries.Add(new ItemCatalog.Entry { id = model.Key, model = prefab, icon = icon });
            }
            return entries;
        }

        /// <summary>Builds the icons and puts them into the existing ItemCatalog (replacing any with the same id).</summary>
        public static void MergeIntoCatalog()
        {
            List<ItemCatalog.Entry> built = Build();
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(UpgradeAssetBuilder.CatalogPath);
            if (catalog == null)
            {
                Debug.LogWarning("No ItemCatalog yet: run Signal Lost > Items > Build Upgrade Assets (it includes the Mudlands icons).");
                return;
            }
            var data = new SerializedObject(catalog);
            SerializedProperty list = data.FindProperty("entries");
            foreach (ItemCatalog.Entry entry in built)
            {
                int index = -1;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == entry.id) { index = i; break; }
                if (index < 0) { index = list.arraySize; list.arraySize++; }
                SerializedProperty row = list.GetArrayElementAtIndex(index);
                row.FindPropertyRelative("id").stringValue = entry.id;
                row.FindPropertyRelative("model").objectReferenceValue = entry.model;
                row.FindPropertyRelative("icon").objectReferenceValue = entry.icon;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        private static Material SavedMaterial(Color color, float glow)
        {
            string path = Folder + "/Materials/" + ColorUtility.ToHtmlStringRGB(color) + (glow > 0f ? "_glow" : "") + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.35f);
            if (glow > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * glow);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- Models ----------

        // A tall green rubber wader with a strap and a chunky sole.
        private static void Waders(MeshKit k)
        {
            k.Prism(new Vector3(0f, 0.08f, 0f), Quaternion.identity, 0.075f, 0.085f, 0.42f, 10, BootGreen);
            k.Box(new Vector3(0f, 0.05f, 0.07f), new Vector3(0.15f, 0.1f, 0.26f), BootGreen);
            k.Box(new Vector3(0f, 0f, 0.07f), new Vector3(0.16f, 0.03f, 0.28f), MudPalette.Rubber);
            k.Prism(new Vector3(0f, 0.5f, 0f), Quaternion.identity, 0.088f, 0.088f, 0.03f, 10, MudPalette.DarkMetal);
            k.Box(new Vector3(0f, 0.515f, 0.09f), new Vector3(0.05f, 0.04f, 0.02f), Chrome);
            k.Box(new Vector3(0f, 0.12f, 0.2f), new Vector3(0.16f, 0.06f, 0.03f), MudPalette.Mud);
        }

        // A bottle-green glass capacitor jar wound with copper, two terminals and a spark between them.
        private static void Capacitor(MeshKit k)
        {
            k.Prism(Vector3.zero, Quaternion.identity, 0.11f, 0.11f, 0.28f, 12, new Color(0.25f, 0.5f, 0.35f));
            for (int i = 0; i < 4; i++) k.Prism(new Vector3(0f, 0.05f + i * 0.055f, 0f), Quaternion.identity, 0.118f, 0.118f, 0.025f, 12, Copper);
            k.Prism(new Vector3(0f, 0.28f, 0f), Quaternion.identity, 0.1f, 0.1f, 0.03f, 12, MudPalette.DarkMetal);
            k.Rod(new Vector3(-0.05f, 0.31f, 0f), new Vector3(-0.05f, 0.4f, 0f), 0.015f, 6, Chrome);
            k.Rod(new Vector3(0.05f, 0.31f, 0f), new Vector3(0.05f, 0.4f, 0f), 0.015f, 6, Chrome);
            k.Ball(new Vector3(-0.05f, 0.41f, 0f), 0.025f, Copper);
            k.Ball(new Vector3(0.05f, 0.41f, 0f), 0.025f, Copper);
            k.Tube(new[] { new Vector3(-0.04f, 0.43f, 0f), new Vector3(-0.01f, 0.47f, 0.01f), new Vector3(0.01f, 0.44f, -0.01f), new Vector3(0.04f, 0.43f, 0f) }, 0.008f, 4, Spark, 3f);
        }

        // A little copper pot still with its coil.
        private static void Distiller(MeshKit k)
        {
            k.Ball(new Vector3(0f, 0.14f, 0f), new Vector3(0.14f, 0.13f, 0.14f), Quaternion.identity, Copper, 1);
            k.Prism(new Vector3(0f, 0.24f, 0f), Quaternion.identity, 0.05f, 0.03f, 0.08f, 10, Copper);
            k.Tube(new[] { new Vector3(0f, 0.32f, 0f), new Vector3(0.08f, 0.35f, 0f), new Vector3(0.18f, 0.28f, 0f) }, 0.014f, 6, Copper);
            for (int i = 0; i < 3; i++)
                k.Tube(new[] { new Vector3(0.14f, 0.24f - i * 0.05f, -0.04f), new Vector3(0.22f, 0.24f - i * 0.05f - 0.012f, 0f), new Vector3(0.14f, 0.24f - i * 0.05f - 0.025f, 0.04f), new Vector3(0.18f, 0.24f - i * 0.05f - 0.04f, 0f) }, 0.012f, 5, Copper);
            k.Prism(new Vector3(0f, 0f, 0f), Quaternion.identity, 0.12f, 0.12f, 0.03f, 10, MudPalette.DarkMetal);
            k.Box(new Vector3(0.24f, 0.05f, 0f), new Vector3(0.05f, 0.1f, 0.05f), MudPalette.Wood);
        }

        // A chunky chrome bumper with a welded cow-catcher and big bolts.
        private static void Bumper(MeshKit k)
        {
            k.Box(new Vector3(0f, 0.12f, 0f), new Vector3(0.6f, 0.14f, 0.1f), Chrome);
            k.Box(new Vector3(-0.22f, 0.12f, -0.1f), new Vector3(0.06f, 0.08f, 0.14f), MudPalette.DarkMetal);
            k.Box(new Vector3(0.22f, 0.12f, -0.1f), new Vector3(0.06f, 0.08f, 0.14f), MudPalette.DarkMetal);
            for (int i = -3; i <= 3; i++)
                k.Rod(new Vector3(i * 0.08f, 0.06f, 0.05f), new Vector3(i * 0.025f, -0.06f, 0.24f), 0.018f, 6, MudPalette.DarkMetal);
            k.Box(new Vector3(0f, -0.06f, 0.22f), new Vector3(0.22f, 0.03f, 0.05f), MudPalette.DarkMetal);
            for (int i = -2; i <= 2; i++) k.Ball(new Vector3(i * 0.12f, 0.12f, 0.055f), 0.02f, MudPalette.DarkMetal, 0);
        }

        // Three sticks of dynamite taped together, the fuse fizzing.
        private static void BiggerBoom(MeshKit k)
        {
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 0.055f;
                float z = i == 1 ? 0.04f : 0f;
                k.Prism(new Vector3(x, 0f, z), Quaternion.identity, 0.028f, 0.028f, 0.3f, 8, Dynamite);
            }
            k.Prism(new Vector3(0f, 0.08f, 0.01f), Quaternion.identity, 0.095f, 0.095f, 0.04f, 10, new Color(0.85f, 0.8f, 0.65f));
            k.Prism(new Vector3(0f, 0.2f, 0.01f), Quaternion.identity, 0.095f, 0.095f, 0.04f, 10, new Color(0.85f, 0.8f, 0.65f));
            k.Tube(new[] { new Vector3(0f, 0.3f, 0.04f), new Vector3(0.03f, 0.36f, 0.04f), new Vector3(-0.02f, 0.4f, 0.04f) }, 0.008f, 4, MudPalette.Black);
            k.Ball(new Vector3(-0.02f, 0.41f, 0.04f), 0.025f, new Color(1f, 0.75f, 0.3f), 0, 3f);
        }

        // A stoneware moonshine jug marked XXX.
        private static void ExtraProof(MeshKit k)
        {
            Color jug = new Color(0.82f, 0.74f, 0.6f);
            k.Lathe(Vector3.zero, Quaternion.identity, new[] { new Vector2(0.0001f, 0f), new Vector2(0.12f, 0f), new Vector2(0.14f, 0.1f), new Vector2(0.12f, 0.2f),
                new Vector2(0.05f, 0.26f), new Vector2(0.035f, 0.3f), new Vector2(0.0001f, 0.3f) }, 12, jug);
            k.Prism(new Vector3(0f, 0.12f, 0f), Quaternion.identity, 0.142f, 0.135f, 0.06f, 12, new Color(0.45f, 0.28f, 0.15f));
            k.Prism(new Vector3(0f, 0.3f, 0f), Quaternion.identity, 0.03f, 0.028f, 0.04f, 8, MudPalette.Wood);
            k.Tube(new[] { new Vector3(0.04f, 0.26f, 0f), new Vector3(0.12f, 0.27f, 0f), new Vector3(0.13f, 0.18f, 0f) }, 0.012f, 5, jug);
            for (int i = -1; i <= 1; i++)
            {
                Vector3 c = new Vector3(i * 0.045f, 0.15f, 0.145f);
                k.Box(c, new Vector3(0.012f, 0.05f, 0.01f), Quaternion.Euler(0f, 0f, 35f), MudPalette.Black);
                k.Box(c, new Vector3(0.012f, 0.05f, 0.01f), Quaternion.Euler(0f, 0f, -35f), MudPalette.Black);
            }
        }

        // A coil-over shock absorber: a yellow spring round a chrome rod.
        private static void Shocks(MeshKit k)
        {
            k.Rod(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.42f, 0f), 0.025f, 8, Chrome);
            k.Prism(new Vector3(0f, 0f, 0f), Quaternion.identity, 0.04f, 0.04f, 0.18f, 10, MudPalette.DarkMetal);
            var coil = new List<Vector3>();
            for (int i = 0; i <= 64; i++)
            {
                float t = i / 64f, a = t * Mathf.PI * 2f * 6f;
                coil.Add(new Vector3(Mathf.Cos(a) * 0.065f, 0.06f + t * 0.3f, Mathf.Sin(a) * 0.065f));
            }
            k.Tube(coil.ToArray(), 0.012f, 5, Spring);
            k.Prism(new Vector3(0f, 0.05f, 0f), Quaternion.identity, 0.08f, 0.08f, 0.015f, 10, MudPalette.DarkMetal);
            k.Prism(new Vector3(0f, 0.37f, 0f), Quaternion.identity, 0.08f, 0.08f, 0.015f, 10, MudPalette.DarkMetal);
            k.Ball(new Vector3(0f, 0.45f, 0f), 0.035f, MudPalette.DarkMetal, 0);
            k.Ball(new Vector3(0f, -0.02f, 0f), 0.035f, MudPalette.DarkMetal, 0);
        }

        // A fat off-road tyre with deep tread blocks, caked in mud.
        private static void MudTire(MeshKit k)
        {
            Quaternion side = Quaternion.Euler(0f, 0f, 90f);
            k.Prism(new Vector3(-0.09f, 0f, 0f), side, 0.2f, 0.2f, 0.18f, 16, MudPalette.Rubber);
            k.Prism(new Vector3(-0.095f, 0f, 0f), side, 0.1f, 0.1f, 0.19f, 10, Chrome);
            k.Prism(new Vector3(-0.1f, 0f, 0f), side, 0.04f, 0.04f, 0.2f, 6, MudPalette.DarkMetal);
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f;
                var at = new Vector3(i % 2 == 0 ? -0.04f : 0.04f, Mathf.Cos(a) * 0.21f, Mathf.Sin(a) * 0.21f);
                k.Box(at, new Vector3(0.09f, 0.04f, 0.06f), Quaternion.Euler(-a * Mathf.Rad2Deg, 0f, 0f), MudPalette.Rubber);
            }
            k.Ball(new Vector3(0.05f, -0.17f, 0.08f), new Vector3(0.08f, 0.05f, 0.07f), Quaternion.identity, MudPalette.WetMud, 1);
            k.Ball(new Vector3(-0.04f, -0.12f, -0.15f), new Vector3(0.07f, 0.05f, 0.06f), Quaternion.identity, MudPalette.WetMud, 1);
        }

        // A beer-bong funnel with its hose.
        private static void Funnel(MeshKit k)
        {
            Color plastic = new Color(0.95f, 0.55f, 0.15f);
            k.Lathe(new Vector3(0f, 0.2f, 0f), Quaternion.identity, new[] { new Vector2(0.03f, 0f), new Vector2(0.16f, 0.16f), new Vector2(0.15f, 0.17f), new Vector2(0.025f, 0.01f) }, 14, plastic);
            k.Tube(new[] { new Vector3(0f, 0.2f, 0f), new Vector3(0f, 0.08f, 0.02f), new Vector3(0.06f, 0.0f, 0.08f), new Vector3(0.16f, 0.02f, 0.1f) }, 0.022f, 7, new Color(0.85f, 0.9f, 0.85f));
            k.Prism(new Vector3(0.16f, 0.0f, 0.1f), Quaternion.Euler(0f, 0f, 80f), 0.026f, 0.026f, 0.03f, 7, MudPalette.DarkMetal);
        }

        // A ring of spare car keys on a fuzzy-dice fob.
        private static void Keys(MeshKit k)
        {
            var ring = new List<Vector3>();
            for (int i = 0; i <= 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2f;
                ring.Add(new Vector3(Mathf.Cos(a) * 0.07f, 0.3f + Mathf.Sin(a) * 0.07f, 0f));
            }
            k.Tube(ring.ToArray(), 0.008f, 5, Chrome);
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.05f : 0.06f;
                Quaternion tilt = Quaternion.Euler(0f, 0f, i == 0 ? 18f : -14f);
                k.Box(new Vector3(x, 0.19f, 0f), new Vector3(0.06f, 0.07f, 0.015f), tilt, i == 0 ? MudPalette.Black : Copper);
                k.Box(new Vector3(x * 1.2f, 0.1f, 0f), new Vector3(0.022f, 0.12f, 0.008f), tilt, Chrome);
                k.Box(new Vector3(x * 1.3f + 0.012f, 0.06f, 0f), new Vector3(0.014f, 0.02f, 0.008f), tilt, Chrome);
            }
            k.Box(new Vector3(0f, 0.38f, 0f), new Vector3(0.08f, 0.08f, 0.08f), Quaternion.Euler(20f, 30f, 10f), MudPalette.White);
            k.Box(new Vector3(0.07f, 0.42f, 0.02f), new Vector3(0.07f, 0.07f, 0.07f), Quaternion.Euler(-15f, 50f, 30f), MudPalette.White);
            foreach (Vector3 pip in new[] { new Vector3(0f, 0.38f, 0.042f), new Vector3(0.02f, 0.4f, 0.042f), new Vector3(-0.02f, 0.36f, 0.042f) })
                k.Ball(pip, 0.008f, MudPalette.Black, 0);
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
