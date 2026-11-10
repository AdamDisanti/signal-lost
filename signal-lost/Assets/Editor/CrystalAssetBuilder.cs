using System;
using SignalLost.Effects;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Editor
{
    /// <summary>
    /// Builds the Crystal Caves art kit in Prefabs/Enviornment objects/CrystalCaves:
    ///  * three glowing crystal shapes (Crystals/CrystalSpire, CrystalCluster, CrystalShard), two giant ones
    ///    (Crystals/GiantCrystal, GiantCrystalWide, 10 m long) and six crystal materials (Materials/Crystal*.mat:
    ///    dark blue, green, orange, purple, pink, cyan);
    ///  * Crystals/CrystalBatch.prefab, a ready-made group of crystals with its glow light: drop it anywhere and pick
    ///    a material on its <see cref="CrystalBatch"/> to recolour the crystals and the light together;
    ///  * cave rock (Rocks/Stalagmite, StalagmiteBlunt, CaveColumn, CaveBoulder, CaveBoulderRound; stalactites are
    ///    stalagmites turned upside down) and coast rock (Rocks/CoastColumn, CoastBlock) for the sea cliffs.
    /// Every piece is a flat-shaded generated mesh, like the rest of the game's art. Shapes stand on y = 0 and point
    /// up (+Y); scale them freely. Menu: Signal Lost > Crystal Caves > Build Crystal and Rock Prefabs.
    /// </summary>
    internal static class CrystalAssetBuilder
    {
        public const string Folder = "Assets/Prefabs/Enviornment objects/CrystalCaves";
        public const string MaterialFolder = Folder + "/Materials";
        private const string MeshFolder = Folder + "/Meshes";
        private const string CrystalFolder = Folder + "/Crystals";
        private const string RockFolder = Folder + "/Rocks";

        public enum CrystalColor { DarkBlue, Green, Orange, Purple, Pink, Cyan }

        public static readonly string[] CrystalShapes = { "CrystalSpire", "CrystalCluster", "CrystalShard" };
        public static readonly string[] GiantCrystalShapes = { "GiantCrystal", "GiantCrystalWide" };
        public static string CrystalPath(string shape) => CrystalFolder + "/" + shape + ".prefab";
        public static string BatchPath => CrystalFolder + "/CrystalBatch.prefab";
        public static string RockPath(string name) => RockFolder + "/" + name + ".prefab";

        private enum Rock { Cave, CaveDark, Coast, CoastDark }
        private enum One { Crystal }

        public static Color Glow(CrystalColor color) => color switch
        {
            CrystalColor.DarkBlue => new Color(0.16f, 0.3f, 1f),
            CrystalColor.Green => new Color(0.25f, 0.95f, 0.42f),
            CrystalColor.Orange => new Color(1f, 0.55f, 0.14f),
            CrystalColor.Purple => new Color(0.62f, 0.26f, 1f),
            CrystalColor.Pink => new Color(1f, 0.36f, 0.74f),
            _ => new Color(0.2f, 0.9f, 1f)
        };

        /// <summary>Blue and purple look dimmer than the rest at the same strength, so their lights get a boost.</summary>
        public static float LightBoost(CrystalColor color) => color == CrystalColor.DarkBlue ? 1.45f : color == CrystalColor.Purple ? 1.25f : 1f;

        public static Material CrystalMaterial(CrystalColor color) => AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/Crystal" + color + ".mat");

        [MenuItem("Signal Lost/Crystal Caves/Build Crystal and Rock Prefabs")]
        public static void Build()
        {
            foreach (string folder in new[] { MaterialFolder, MeshFolder, CrystalFolder, RockFolder }) EnsureFolder(folder);
            foreach (CrystalColor color in Enum.GetValues(typeof(CrystalColor))) BuildCrystalMaterial(color);
            Material cyan = CrystalMaterial(CrystalColor.Cyan);

            // Crystals: hexagonal prisms with pointed tips, lathed so the facets catch the light.
            Prefab(CrystalPath("CrystalSpire"), Crystal(Prism(0.26f, 2.2f, 6), Matrix4x4.identity), cyan, true, false);
            var cluster = new FlatMeshKit<One>();
            cluster.Add(Prism(0.3f, 2.1f, 6), Matrix4x4.identity, One.Crystal);
            (float yaw, float tilt, float size)[] arms = { (20f, 28f, 0.75f), (130f, 34f, 0.6f), (235f, 24f, 0.68f), (300f, 40f, 0.45f), (80f, 46f, 0.38f) };
            foreach ((float yaw, float tilt, float size) in arms)
                cluster.Add(Prism(0.3f, 2.1f, 6), Matrix4x4.TRS(Quaternion.Euler(0f, yaw, 0f) * new Vector3(0.18f, 0f, 0f), Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, -tilt), Vector3.one * size), One.Crystal);
            Prefab(CrystalPath("CrystalCluster"), Crystal(cluster), cyan, true, false);
            Prefab(CrystalPath("CrystalShard"), Crystal(Lathe("Shard", new[] { new Vector2(0f, 0f), new Vector2(0.52f, 0f), new Vector2(0.58f, 0.32f), new Vector2(0.36f, 0.95f), new Vector2(0f, 1.2f) }, 5), Matrix4x4.Scale(new Vector3(1f, 1f, 0.75f))), cyan, true, false);
            BuildBatchPrefab(cyan);
            // Giant crystals, like the reference's long glowing rods: a slim one and a wide, flattened one, 10 m long.
            Prefab(CrystalPath("GiantCrystal"), Crystal(Prism(0.42f, 10f, 6), Matrix4x4.identity), cyan, true, false);
            Prefab(CrystalPath("GiantCrystalWide"), Crystal(Lathe("GiantCrystalWide", new[] { new Vector2(0f, 0f), new Vector2(0.8f, 0f), new Vector2(0.84f, 0.5f), new Vector2(0.7f, 7f), new Vector2(0f, 9f) }, 6),
                Matrix4x4.Scale(new Vector3(1f, 1f, 0.72f))), cyan, true, false);

            // Cave and coast rock.
            Prefab(RockPath("Stalagmite"), RockMesh("Stalagmite", Upright(LowPolyMesh.TaperedPrism("Stalagmite", 6, 0.06f))), RockMaterial(Rock.Cave), true, true);
            Prefab(RockPath("StalagmiteBlunt"), RockMesh("StalagmiteBlunt", Upright(LowPolyMesh.TaperedPrism("StalagmiteBlunt", 5, 0.28f))), RockMaterial(Rock.CaveDark), true, true);
            // An hourglass pillar, flared into the floor and the ceiling; scale its height from floor to ceiling.
            Prefab(RockPath("CaveColumn"), RockMesh("CaveColumn", Lathe("CaveColumn", new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.72f, 0.12f), new Vector2(0.48f, 0.38f), new Vector2(0.44f, 0.55f),
                new Vector2(0.56f, 0.78f), new Vector2(0.8f, 0.92f), new Vector2(1f, 1f), new Vector2(0f, 1f)
            }, 8)), RockMaterial(Rock.Cave), true, true);
            Prefab(RockPath("CaveBoulder"), RockMesh("CaveBoulder", LowPolyMesh.Icosphere("CaveBoulder", 0)), RockMaterial(Rock.CaveDark), true, true);
            Prefab(RockPath("CaveBoulderRound"), RockMesh("CaveBoulderRound", LowPolyMesh.Icosphere("CaveBoulderRound", 1)), RockMaterial(Rock.Cave), true, true);
            // Blocky, slightly tapered columns like the reference's stacked sea cliffs, and tumbled blocks for the surf.
            Prefab(RockPath("CoastColumn"), RockMesh("CoastColumn", Upright(LowPolyMesh.TaperedPrism("CoastColumn", 6, 0.9f))), RockMaterial(Rock.Coast), true, true);
            Prefab(RockPath("CoastBlock"), RockMesh("CoastBlock", Upright(LowPolyMesh.TaperedPrism("CoastBlock", 4, 0.82f))), RockMaterial(Rock.CoastDark), true, true);

            AssetDatabase.SaveAssets();
            Debug.Log("CRYSTAL_ASSETS_BUILT");
        }

        // ---------- Crystals ----------

        private static void BuildCrystalMaterial(CrystalColor color)
        {
            string path = MaterialFolder + "/Crystal" + color + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            Color glow = Glow(color);
            material.SetColor("_BaseColor", new Color(glow.r * 0.5f, glow.g * 0.5f, glow.b * 0.5f, 1f));
            material.SetFloat("_Smoothness", 0.78f);
            material.SetColor("_EmissionColor", glow * (1.25f * LightBoost(color))); // brighter washes the colour out to white under bloom
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; // with None, URP's material check drops _EMISSION on import
            EditorUtility.SetDirty(material);
        }

        // A sample batch: a big spire, a cluster and shards around it, and its glow light.
        private static void BuildBatchPrefab(Material material)
        {
            var root = new GameObject("CrystalBatch");
            (string shape, Vector3 at, Vector3 euler, float scale)[] parts =
            {
                ("CrystalSpire", Vector3.zero, new Vector3(4f, 0f, -6f), 1.3f), ("CrystalCluster", new Vector3(0.9f, 0f, 0.4f), new Vector3(0f, 40f, 18f), 0.8f),
                ("CrystalShard", new Vector3(-0.8f, 0f, 0.5f), new Vector3(-12f, 110f, 22f), 0.9f), ("CrystalSpire", new Vector3(-0.4f, 0f, -0.9f), new Vector3(-28f, 200f, 10f), 0.7f),
                ("CrystalShard", new Vector3(0.6f, 0f, -0.8f), new Vector3(20f, 300f, -15f), 0.55f)
            };
            foreach ((string shape, Vector3 at, Vector3 euler, float scale) in parts)
            {
                var crystal = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CrystalPath(shape)), root.transform);
                crystal.transform.SetLocalPositionAndRotation(at + Vector3.down * 0.1f, Quaternion.Euler(euler));
                crystal.transform.localScale = Vector3.one * scale;
            }
            Light glow = AddGlow(root.transform, Vector3.up * 3.8f, 24f, 16f);
            var batch = new SerializedObject(root.AddComponent<CrystalBatch>());
            batch.FindProperty("crystalMaterial").objectReferenceValue = material;
            batch.FindProperty("glow").objectReferenceValue = glow;
            batch.ApplyModifiedPropertiesWithoutUndo();
            root.GetComponent<CrystalBatch>().Apply();
            PrefabUtility.SaveAsPrefabAsset(root, BatchPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        /// <summary>A crystal batch's glow: a point light without shadows (it shines through rock, so keep its range modest).
        /// Keep it above the crystals: inside one, it would only light that crystal from within.</summary>
        public static Light AddGlow(Transform batch, Vector3 localPosition, float range, float intensity)
        {
            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(batch, false);
            glow.transform.localPosition = localPosition;
            glow.type = LightType.Point;
            glow.range = range;
            glow.intensity = intensity;
            glow.shadows = LightShadows.None;
            return glow;
        }

        // A hexagonal crystal standing on y = 0: straight sides, then a pointed tip.
        private static Mesh Prism(float radius, float height, int sides) =>
            Lathe("Crystal", new[] { new Vector2(0f, 0f), new Vector2(radius, 0f), new Vector2(radius * 1.06f, height * 0.08f), new Vector2(radius * 0.86f, height * 0.74f), new Vector2(0f, height) }, sides);

        private static Mesh Lathe(string name, Vector2[] profile, int sides) => LowPolyMesh.Lathe(name, profile, sides);

        private static Mesh Crystal(Mesh mesh, Matrix4x4 pose)
        {
            var kit = new FlatMeshKit<One>();
            kit.Add(mesh, pose, One.Crystal);
            return Crystal(kit);
        }

        private static Mesh Crystal(FlatMeshKit<One> kit) => kit.Build("Crystal");

        // ---------- Rock ----------

        // LowPolyMesh prisms run along +Z; rock stands up along +Y.
        private static Mesh Upright(Mesh prism)
        {
            string name = prism.name; // Add destroys the source mesh
            var kit = new FlatMeshKit<One>();
            kit.Add(prism, Matrix4x4.Rotate(Quaternion.Euler(-90f, 0f, 0f)), One.Crystal);
            return kit.Build(name);
        }

        private static Mesh RockMesh(string name, Mesh mesh)
        {
            mesh.name = name;
            return mesh;
        }

        private static Material RockMaterial(Rock key)
        {
            Color color = key switch
            {
                Rock.Cave => new Color(0.36f, 0.38f, 0.45f),
                Rock.CaveDark => new Color(0.26f, 0.27f, 0.33f),
                Rock.Coast => new Color(0.6f, 0.46f, 0.33f),
                _ => new Color(0.46f, 0.35f, 0.27f)
            };
            return SimpleMaterial("Rock" + key, color, 0.06f);
        }

        /// <summary>A plain URP Lit material in the CrystalCaves materials folder, created once and recoloured on rebuild.</summary>
        public static Material SimpleMaterial(string name, Color color, float smoothness)
        {
            EnsureFolder(MaterialFolder);
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- Prefabs ----------

        private static void Prefab(string path, Mesh mesh, Material material, bool collider, bool castShadows)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            mesh = SaveMesh(mesh, MeshFolder + "/" + name + ".asset");
            var root = new GameObject(name);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (collider)
            {
                var hull = root.AddComponent<MeshCollider>();
                hull.sharedMesh = mesh;
                hull.convex = true;
            }
            GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            mesh.name = System.IO.Path.GetFileNameWithoutExtension(path);
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

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
