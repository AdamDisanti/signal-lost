using System;
using System.Collections.Generic;
using SignalLost.Inventory;
using UnityEditor;
using UnityEngine;

namespace SignalLost.Editor
{
    /// <summary>
    /// Builds the four scrap models (meeting notes 10-23, from Adam's reference images) and their icons, and the
    /// ScrapCatalog that runtime code reads them from. All flat-shaded low-poly, like the rest of Signal Lost:
    /// <list type="bullet">
    /// <item>Scrap Metal: a bundle of bent gunmetal pipes and drilled brackets with hex nuts and rust.</item>
    /// <item>Wiring: a spool of black cable between rusty-orange flanges, a loose end trailing off.</item>
    /// <item>Circuit Boards: a green board with gold traces, chips, teal capacitors, heat sinks and a glowing core chip.</item>
    /// <item>Fuel Cells: a glass tube with ribbed metal caps and a jagged cyan charge glowing inside.</item>
    /// </list>
    /// Models rest upright on y = 0 and are about 0.4 m across. Rebuilding replaces the assets in place (same GUIDs).
    /// Menu: Signal Lost > Items > Build Scrap Assets.
    /// </summary>
    internal static class ScrapAssetBuilder
    {
        public const string Folder = "Assets/Prefabs/Items/Scrap";
        public const string CatalogPath = "Assets/Resources/SignalLost/ScrapCatalog.asset";
        /// <summary>TextMeshPro sprite asset with the four icons, so text can show "4 &lt;sprite name="ScrapMetal"&gt;".</summary>
        public const string InlineIconsPath = "Assets/Resources/SignalLost/ScrapIcons.asset";
        private const int InlineSize = 128;

        private enum Mat { Metal, Rust, DarkMetal, Cable, Flange, Board, Trace, Chip, Capacitor, Silver, Led, Glass, Charge, Band }

        private static readonly Dictionary<Mat, Material> Materials = new Dictionary<Mat, Material>();

        [MenuItem("Signal Lost/Items/Build Scrap Assets")]
        public static void Build()
        {
            EnsureFolder(Folder + "/Meshes");
            EnsureFolder(Folder + "/Materials");
            EnsureFolder(Folder + "/Icons");
            EnsureFolder("Assets/Resources/SignalLost");
            Materials.Clear();
            BuildMaterials();

            var entries = new List<ScrapCatalog.Entry>();
            foreach (ScrapType type in ScrapInfo.All)
            {
                GameObject prefab = BuildModel(type);
                Sprite icon = ItemIconRenderer.Render(prefab, Folder + "/Icons/" + type + ".png", IconAngles(type));
                entries.Add(new ScrapCatalog.Entry { type = type, model = prefab, icon = icon });
            }

            var catalog = AssetDatabase.LoadAssetAtPath<ScrapCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ScrapCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var data = new SerializedObject(catalog);
            SerializedProperty list = data.FindProperty("entries");
            list.arraySize = entries.Count;
            for (int i = 0; i < entries.Count; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("type").enumValueIndex = (int)entries[i].type;
                entry.FindPropertyRelative("model").objectReferenceValue = entries[i].model;
                entry.FindPropertyRelative("icon").objectReferenceValue = entries[i].icon;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            BuildInlineIcons();
            AssetDatabase.SaveAssets();
            Debug.Log("SCRAP_ASSETS_BUILT");
        }

        public static string PrefabPath(ScrapType type) => Folder + "/" + type + ".prefab";

        // ---------- Inline icons for text ----------

        // The four icons side by side in one atlas, and a TMP sprite asset naming each after its ScrapType.
        private static void BuildInlineIcons()
        {
            int count = ScrapInfo.All.Length;
            var atlas = new Texture2D(InlineSize * count, InlineSize, TextureFormat.RGBA32, false);
            atlas.SetPixels32(new Color32[atlas.width * atlas.height]);
            for (int i = 0; i < count; i++)
            {
                var icon = new Texture2D(2, 2);
                icon.LoadImage(System.IO.File.ReadAllBytes(Folder + "/Icons/" + ScrapInfo.All[i] + ".png"));
                // Crop to the visible pixels (a square around them), so the icon fills its slot in the text.
                int minX = icon.width, minY = icon.height, maxX = 0, maxY = 0;
                Color32[] pixels = icon.GetPixels32();
                for (int y = 0; y < icon.height; y++)
                for (int x = 0; x < icon.width; x++)
                    if (pixels[y * icon.width + x].a > 20) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
                float side = Mathf.Max(maxX - minX, maxY - minY) + 4f;
                float cx = (minX + maxX) * 0.5f, cy = (minY + maxY) * 0.5f;
                for (int y = 0; y < InlineSize; y++)
                for (int x = 0; x < InlineSize; x++)
                {
                    float u = (cx - side * 0.5f + (x + 0.5f) / InlineSize * side) / icon.width;
                    float v = (cy - side * 0.5f + (y + 0.5f) / InlineSize * side) / icon.height;
                    atlas.SetPixel(i * InlineSize + x, y, u < 0f || v < 0f || u > 1f || v > 1f ? Color.clear : icon.GetPixelBilinear(u, v));
                }
                UnityEngine.Object.DestroyImmediate(icon);
            }
            atlas.Apply();
            string atlasPath = Folder + "/Icons/ScrapIconAtlas.png";
            System.IO.File.WriteAllBytes(atlasPath, atlas.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(atlas);
            AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(atlasPath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);

            AssetDatabase.DeleteAsset(InlineIconsPath);
            var asset = ScriptableObject.CreateInstance<TMPro.TMP_SpriteAsset>();
            asset.name = "ScrapIcons";
            asset.spriteSheet = sheet;
            // Sized like a font of the icon's height, so an icon matches the size of the text around it.
            asset.faceInfo = new UnityEngine.TextCore.FaceInfo
            {
                pointSize = InlineSize, scale = 1f, lineHeight = InlineSize,
                ascentLine = InlineSize * 0.8f, descentLine = -InlineSize * 0.2f, baseline = 0f
            };
            for (int i = 0; i < count; i++)
            {
                var glyph = new TMPro.TMP_SpriteGlyph((uint)i,
                    new UnityEngine.TextCore.GlyphMetrics(InlineSize, InlineSize, 0f, InlineSize * 0.72f, InlineSize),
                    new UnityEngine.TextCore.GlyphRect(i * InlineSize, 0, InlineSize, InlineSize), 1.6f, 0);
                asset.spriteGlyphTable.Add(glyph);
                asset.spriteCharacterTable.Add(new TMPro.TMP_SpriteCharacter(0xE000u + (uint)i, glyph) { name = ScrapInfo.All[i].ToString() });
            }
            AssetDatabase.CreateAsset(asset, InlineIconsPath);
            var material = new Material(Shader.Find("TextMeshPro/Sprite")) { name = "ScrapIcons Material" };
            material.mainTexture = sheet;
            AssetDatabase.AddObjectToAsset(material, asset);
            asset.material = material;
            // A version stamp, or TMP treats it as a legacy asset and "upgrades" (empties) its tables on load.
            var data = new SerializedObject(asset);
            data.FindProperty("m_Version").stringValue = "1.1.0";
            data.ApplyModifiedPropertiesWithoutUndo();
            asset.UpdateLookupTables();
            EditorUtility.SetDirty(asset);
        }

        private static Vector3 IconAngles(ScrapType type) => type switch
        {
            ScrapType.CircuitBoards => new Vector3(55f, 20f, 0f),
            ScrapType.FuelCells => new Vector3(0f, 25f, -35f),
            ScrapType.Wiring => new Vector3(20f, 30f, 0f),
            _ => new Vector3(30f, 35f, 0f)
        };

        // ---------- Models ----------

        private static GameObject BuildModel(ScrapType type)
        {
            var kit = new FlatMeshKit<Mat>();
            switch (type)
            {
                case ScrapType.Wiring: Spool(kit); break;
                case ScrapType.CircuitBoards: Board(kit); break;
                case ScrapType.FuelCells: FuelCell(kit); break;
                default: MetalBundle(kit); break;
            }
            Mesh mesh = SaveMesh(kit.Build("Scrap" + type));
            var root = new GameObject(ScrapInfo.Name(type));
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var materials = new Material[mesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = Materials[FlatMeshKit<Mat>.MaterialOf(mesh, i)];
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(type));
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        // Bent pipes, a drilled bracket and a couple of hex nuts, piled together.
        private static void MetalBundle(FlatMeshKit<Mat> kit)
        {
            kit.Add(LowPolyMesh.Tube("Pipe", new[] { new Vector3(-0.2f, 0.04f, -0.05f), new Vector3(0.02f, 0.05f, -0.03f), new Vector3(0.1f, 0.06f, 0.06f), new Vector3(0.12f, 0.16f, 0.1f) }, 0.028f, 6), Matrix4x4.identity, Mat.Metal);
            kit.Add(LowPolyMesh.Tube("Pipe", new[] { new Vector3(-0.15f, 0.07f, 0.08f), new Vector3(0.05f, 0.09f, 0.0f), new Vector3(0.2f, 0.07f, -0.09f) }, 0.022f, 6), Matrix4x4.identity, Mat.DarkMetal);
            kit.Add(LowPolyMesh.Tube("Pipe", new[] { new Vector3(0.18f, 0.03f, 0.1f), new Vector3(0.0f, 0.04f, 0.12f), new Vector3(-0.08f, 0.1f, 0.15f) }, 0.018f, 5), Matrix4x4.identity, Mat.Rust);
            // A flat bracket with two "holes" (darker squares) and a folded lip.
            Quaternion lie = Quaternion.Euler(0f, 25f, 8f);
            kit.Box(new Vector3(-0.02f, 0.12f, -0.02f), new Vector3(0.26f, 0.018f, 0.06f), lie, Mat.Metal);
            kit.Box(lie * new Vector3(0.12f, 0.03f, 0f) + new Vector3(-0.02f, 0.12f, -0.02f), new Vector3(0.02f, 0.06f, 0.06f), lie, Mat.Metal);
            foreach (float x in new[] { -0.08f, 0.04f })
                kit.Box(lie * new Vector3(x, 0.01f, 0f) + new Vector3(-0.02f, 0.12f, -0.02f), new Vector3(0.025f, 0.004f, 0.025f), lie, Mat.DarkMetal);
            foreach (Vector3 at in new[] { new Vector3(0.12f, 0.03f, -0.1f), new Vector3(-0.17f, 0.03f, 0.12f), new Vector3(0.05f, 0.17f, 0.11f) })
                kit.Add(LowPolyMesh.Lathe("Nut", new[] { new Vector2(0.012f, 0f), new Vector2(0.035f, 0f), new Vector2(0.035f, 0.025f), new Vector2(0.012f, 0.025f) }, 6),
                    Matrix4x4.TRS(at, Quaternion.Euler(70f, at.x * 300f, 0f), Vector3.one), Mat.Rust);
        }

        // A cable spool standing on one flange, with the loose end of the cable trailing off.
        private static void Spool(FlatMeshKit<Mat> kit)
        {
            Mesh Flange() => LowPolyMesh.Lathe("Flange", new[] { new Vector2(0.025f, 0f), new Vector2(0.15f, 0f), new Vector2(0.15f, 0.025f), new Vector2(0.025f, 0.025f) }, 12);
            kit.Add(Flange(), Matrix4x4.identity, Mat.Flange);
            kit.Add(Flange(), Matrix4x4.Translate(new Vector3(0f, 0.185f, 0f)), Mat.Flange);
            // Wound cable: ribbed layers between the flanges.
            var wound = new List<Vector2> { new Vector2(0.025f, 0.025f) };
            for (int i = 0; i <= 8; i++)
            {
                float y = 0.025f + i * 0.02f;
                wound.Add(new Vector2(i % 2 == 0 ? 0.118f : 0.124f, y));
            }
            wound.Add(new Vector2(0.025f, 0.185f));
            kit.Add(LowPolyMesh.Lathe("Cable", wound.ToArray(), 12), Matrix4x4.identity, Mat.Cable);
            // The hub hole, dark, and the loose end.
            kit.Add(LowPolyMesh.Lathe("Hub", new[] { new Vector2(0.01f, 0.208f), new Vector2(0.026f, 0.208f), new Vector2(0.026f, 0.212f), new Vector2(0.01f, 0.212f) }, 8), Matrix4x4.identity, Mat.DarkMetal);
            kit.Add(LowPolyMesh.Tube("Loose End", new[] { new Vector3(0.12f, 0.15f, 0f), new Vector3(0.17f, 0.12f, 0.04f), new Vector3(0.2f, 0.04f, 0.12f), new Vector3(0.26f, 0.015f, 0.16f) }, 0.012f, 5), Matrix4x4.identity, Mat.Cable);
            kit.Add(LowPolyMesh.Tube("Copper", new[] { new Vector3(0.26f, 0.015f, 0.16f), new Vector3(0.29f, 0.012f, 0.18f) }, 0.006f, 4), Matrix4x4.identity, Mat.Band);
        }

        // A green board with gold traces, chips, capacitors, heat sinks and a glowing core chip.
        private static void Board(FlatMeshKit<Mat> kit)
        {
            const float w = 0.34f, d = 0.26f, top = 0.02f;
            kit.Box(new Vector3(0f, top * 0.5f, 0f), new Vector3(w, top, d), Quaternion.identity, Mat.Board);
            var random = new System.Random(4);
            // Gold traces in parallel runs with a kink, like the reference.
            for (int run = 0; run < 7; run++)
            {
                float z = -0.1f + run * 0.032f;
                float x0 = -0.15f + (float)random.NextDouble() * 0.05f, x1 = 0.02f + (float)random.NextDouble() * 0.12f;
                for (int line = 0; line < 3; line++)
                {
                    float lz = z + line * 0.007f;
                    kit.Box(new Vector3((x0 + x1) * 0.5f, top + 0.001f, lz), new Vector3(x1 - x0, 0.002f, 0.003f), Quaternion.identity, Mat.Trace);
                }
            }
            for (int col = 0; col < 6; col++)
                kit.Box(new Vector3(0.1f + col * 0.008f, top + 0.001f, 0.04f), new Vector3(0.003f, 0.002f, 0.14f), Quaternion.identity, Mat.Trace);
            // Chips.
            foreach ((Vector3 at, Vector2 size) in new[] { (new Vector3(0.09f, 0f, -0.07f), new Vector2(0.05f, 0.05f)), (new Vector3(-0.1f, 0f, 0.08f), new Vector2(0.06f, 0.035f)), (new Vector3(0.12f, 0f, 0.09f), new Vector2(0.04f, 0.04f)) })
                kit.Box(at + Vector3.up * (top + 0.007f), new Vector3(size.x, 0.012f, size.y), Quaternion.identity, Mat.Chip);
            // The core chip: a silver frame with a glowing green die.
            kit.Box(new Vector3(-0.03f, top + 0.006f, -0.01f), new Vector3(0.06f, 0.01f, 0.06f), Quaternion.identity, Mat.Silver);
            kit.Box(new Vector3(-0.03f, top + 0.012f, -0.01f), new Vector3(0.035f, 0.004f, 0.035f), Quaternion.identity, Mat.Led);
            // Capacitors.
            foreach (Vector3 at in new[] { new Vector3(0.05f, top, 0.06f), new Vector3(-0.13f, top, -0.08f), new Vector3(0.15f, top, -0.02f), new Vector3(-0.05f, top, 0.1f) })
                kit.Add(LowPolyMesh.Lathe("Cap", new[] { new Vector2(0.001f, 0f), new Vector2(0.014f, 0f), new Vector2(0.014f, 0.03f), new Vector2(0.001f, 0.032f) }, 8), Matrix4x4.Translate(at), Mat.Capacitor);
            // Two finned heat sinks along the left edge.
            foreach (float z in new[] { -0.08f, 0.08f })
                for (int fin = 0; fin < 5; fin++)
                    kit.Box(new Vector3(-0.15f, top + 0.015f, z - 0.024f + fin * 0.012f), new Vector3(0.035f, 0.03f, 0.004f), Quaternion.identity, Mat.Silver);
        }

        // A glass tube with ribbed metal end caps and a jagged cyan charge inside.
        private static void FuelCell(FlatMeshKit<Mat> kit)
        {
            const float h = 0.36f, r = 0.045f;
            kit.Add(LowPolyMesh.Lathe("Glass", new[] { new Vector2(r, 0.05f), new Vector2(r, h - 0.05f), new Vector2(r - 0.004f, h - 0.05f), new Vector2(r - 0.004f, 0.05f) }, 12), Matrix4x4.identity, Mat.Glass);
            foreach (bool upper in new[] { false, true })
            {
                var cap = new[]
                {
                    new Vector2(0.001f, 0f), new Vector2(0.04f, 0f), new Vector2(0.055f, 0.012f), new Vector2(0.055f, 0.022f), new Vector2(0.05f, 0.026f),
                    new Vector2(0.056f, 0.032f), new Vector2(0.056f, 0.042f), new Vector2(0.05f, 0.046f), new Vector2(0.052f, 0.06f), new Vector2(0.001f, 0.06f)
                };
                Matrix4x4 pose = upper ? Matrix4x4.TRS(new Vector3(0f, h, 0f), Quaternion.Euler(180f, 0f, 0f), Vector3.one) : Matrix4x4.identity;
                kit.Add(LowPolyMesh.Lathe("Cap", cap, 10), pose, Mat.DarkMetal);
                kit.Add(LowPolyMesh.Lathe("Band", new[] { new Vector2(0.05f, 0.062f), new Vector2(0.057f, 0.062f), new Vector2(0.057f, 0.07f), new Vector2(0.05f, 0.07f) }, 10), pose, Mat.Band);
            }
            // The charge: a zigzag bolt up the middle, plus a faint core.
            var bolt = new List<Vector3>();
            for (int i = 0; i <= 9; i++) bolt.Add(new Vector3(i % 2 == 0 ? -0.018f : 0.018f, 0.06f + i * (h - 0.12f) / 9f, i % 3 == 0 ? 0.01f : -0.01f));
            kit.Add(LowPolyMesh.Tube("Charge", bolt.ToArray(), 0.007f, 4), Matrix4x4.identity, Mat.Charge);
            kit.Add(LowPolyMesh.Lathe("Core", new[] { new Vector2(0.001f, 0.06f), new Vector2(0.012f, 0.07f), new Vector2(0.012f, h - 0.07f), new Vector2(0.001f, h - 0.06f) }, 6), Matrix4x4.identity, Mat.Charge);
        }

        // ---------- Materials ----------

        private static void BuildMaterials()
        {
            Lit(Mat.Metal, new Color(0.42f, 0.42f, 0.42f), 0.8f, 0.45f);
            Lit(Mat.DarkMetal, new Color(0.24f, 0.24f, 0.26f), 0.85f, 0.5f);
            Lit(Mat.Rust, new Color(0.5f, 0.3f, 0.18f), 0.4f, 0.25f);
            Lit(Mat.Cable, new Color(0.06f, 0.06f, 0.07f), 0f, 0.65f);
            Lit(Mat.Flange, new Color(0.72f, 0.32f, 0.12f), 0f, 0.4f);
            Lit(Mat.Board, new Color(0.13f, 0.32f, 0.16f), 0f, 0.35f);
            Lit(Mat.Trace, new Color(0.85f, 0.68f, 0.25f), 0.9f, 0.6f);
            Lit(Mat.Chip, new Color(0.08f, 0.08f, 0.09f), 0f, 0.5f);
            Lit(Mat.Capacitor, new Color(0.15f, 0.55f, 0.7f), 0.2f, 0.6f);
            Lit(Mat.Silver, new Color(0.8f, 0.82f, 0.84f), 0.9f, 0.6f);
            Lit(Mat.Band, new Color(0.9f, 0.48f, 0.18f), 0.7f, 0.5f);
            Lit(Mat.Led, new Color(0.2f, 0.8f, 0.2f), 0f, 0.6f, new Color(0.04f, 0.4f, 0.06f));
            Lit(Mat.Charge, new Color(0.2f, 0.85f, 1f), 0f, 0.6f, new Color(0.05f, 0.65f, 0.95f) * 1.3f);
            Material glass = Lit(Mat.Glass, new Color(0.55f, 0.8f, 1f, 0.16f), 0f, 0.95f);
            MakeTransparent(glass);
        }

        private static Material Lit(Mat key, Color color, float metallic, float smoothness, Color? emission = null)
        {
            string path = Folder + "/Materials/Scrap" + key + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color); // keep the legacy colour in step, or Unity rewrites it on the next save
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                // Without this flag Unity treats the emission as black and strips the keyword when it re-saves.
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(material);
            Materials[key] = material;
            return material;
        }

        private static void MakeTransparent(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
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
