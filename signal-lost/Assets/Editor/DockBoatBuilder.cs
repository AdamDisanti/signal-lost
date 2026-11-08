using System;
using UnityEditor;
using UnityEngine;

namespace SignalLost.Editor
{
    /// <summary>
    /// Builds the dock and the boat that every level's coast uses (the ocean links the levels; travelling between
    /// them is not built yet, so both are scenery with colliders). They reuse the meadow and shipwreck materials.
    ///
    /// Dock: its origin is the top of the deck at the landward end. Place it on a cliff edge
    /// <see cref="DeckHeight"/> m above the sea, facing out to sea (+Z). A short deck hangs off the cliff on
    /// brackets, a railed stairway on pilings runs down to a jetty at the waterline, and the boat moors beside the
    /// jetty at <see cref="BoatMooring"/> (local to the dock).
    /// Boat: a small wooden motor boat; its origin is on the waterline, bow toward +Z.
    /// Menu: Signal Lost > Core > Build Dock and Boat.
    /// </summary>
    internal static class DockBoatBuilder
    {
        public const string DockPath = "Assets/Prefabs/Core/Dock.prefab";
        public const string BoatPath = "Assets/Prefabs/Core/Boat.prefab";
        private const string MeshFolder = "Assets/Prefabs/Core/Meshes";

        /// <summary>How far the dock's top deck is above the water.</summary>
        public const float DeckHeight = 10f;
        /// <summary>Where the boat's origin goes, in the dock's local space (beside the jetty, on the water).</summary>
        public static readonly Vector3 BoatMooring = new Vector3(4.4f, -DeckHeight, 29f);

        private const float DeckEnd = 6f, StairEnd = 22.3f, JettyEnd = 34f, JettyTop = -DeckHeight + 0.6f;

        private enum Mat { Wood, DarkWood, Paint, Trim, Canvas, Metal, Rope, Glass }

        [MenuItem("Signal Lost/Core/Build Dock and Boat")]
        public static void Build()
        {
            EnsureFolder(MeshFolder);
            BuildDock();
            BuildBoat();
            AssetDatabase.SaveAssets();
            Debug.Log("DOCK_AND_BOAT_BUILT");
        }

        // ---------- Dock ----------

        private static void BuildDock()
        {
            var kit = new FlatMeshKit<Mat>();
            var root = new GameObject("Dock");

            // Top deck on the cliff edge (it starts 1.5 m back on the rock), planks across two stringers.
            Planks(kit, -1.5f, DeckEnd, 0f, 4.4f);
            Beam(kit, new Vector3(-1.8f, -0.45f, (DeckEnd - 1.5f) * 0.5f), new Vector3(0.3f, 0.3f, DeckEnd + 1.5f));
            Beam(kit, new Vector3(1.8f, -0.45f, (DeckEnd - 1.5f) * 0.5f), new Vector3(0.3f, 0.3f, DeckEnd + 1.5f));
            // Brackets from under the deck's far end back into the cliff face.
            foreach (float x in new[] { -1.8f, 1.8f })
            {
                Strut(kit, new Vector3(x, -0.5f, DeckEnd - 0.4f), new Vector3(x, -5.5f, -0.6f), 0.28f, Mat.DarkWood);
                Strut(kit, new Vector3(x, -0.5f, 2.5f), new Vector3(x, -3f, -0.6f), 0.22f, Mat.DarkWood);
            }
            // Deck railings, open where the stairs go down.
            Rail(kit, new Vector3(-2.15f, 0f, -1.2f), new Vector3(-2.15f, 0f, DeckEnd));
            Rail(kit, new Vector3(2.15f, 0f, -1.2f), new Vector3(2.15f, 0f, DeckEnd));
            Rail(kit, new Vector3(-2.15f, 0f, DeckEnd), new Vector3(-1.5f, 0f, DeckEnd));
            Rail(kit, new Vector3(1.5f, 0f, DeckEnd), new Vector3(2.15f, 0f, DeckEnd));

            // Stairway down to the jetty, on pilings, railed both sides.
            const int steps = 24;
            float rise = (0f - JettyTop) / steps, run = (StairEnd - DeckEnd) / steps;
            for (int i = 0; i < steps; i++)
            {
                float top = -(i + 1) * rise, z = DeckEnd + (i + 0.5f) * run;
                kit.Box(new Vector3(0f, top - 0.06f, z), new Vector3(2.9f, 0.12f, run * 1.05f), Quaternion.identity, Mat.Wood);
            }
            Vector3 high = new Vector3(0f, -0.25f, DeckEnd), low = new Vector3(0f, JettyTop - 0.25f, StairEnd);
            foreach (float x in new[] { -1.55f, 1.55f })
            {
                Strut(kit, high + Vector3.right * x, low + Vector3.right * x, 0.24f, Mat.DarkWood);
                Rail(kit, new Vector3(x, 0f, DeckEnd), new Vector3(x, JettyTop, StairEnd));
            }
            for (float z = DeckEnd + 3f; z < StairEnd; z += 4.5f)
            {
                float y = Mathf.Lerp(0f, JettyTop, (z - DeckEnd) / (StairEnd - DeckEnd)) - 0.3f;
                Piling(kit, new Vector3(-1.55f, y, z), -DeckHeight - 4f);
                Piling(kit, new Vector3(1.55f, y, z), -DeckHeight - 4f);
            }

            // Jetty at the waterline, with mooring posts and a coil of rope on the boat side and a lamp post at the end.
            Planks(kit, StairEnd, JettyEnd, JettyTop, 4.4f);
            for (float z = StairEnd + 0.6f; z <= JettyEnd; z += 3.8f)
            {
                Piling(kit, new Vector3(-2.05f, JettyTop + 0.9f, z), -DeckHeight - 4f);
                Piling(kit, new Vector3(2.05f, JettyTop + 0.9f, z), -DeckHeight - 4f);
            }
            kit.Box(new Vector3(1.4f, JettyTop + 0.12f, 27.5f), new Vector3(0.9f, 0.24f, 0.9f), Quaternion.Euler(0f, 30f, 0f), Mat.Rope);
            Strut(kit, new Vector3(2.05f, JettyTop + 0.7f, 26.9f), BoatMooring + new Vector3(-0.9f, 1f, -2.2f), 0.05f, Mat.Rope);
            Strut(kit, new Vector3(2.05f, JettyTop + 0.7f, 30.7f), BoatMooring + new Vector3(-0.7f, 1f, 2.2f), 0.05f, Mat.Rope);
            kit.Box(new Vector3(-1.9f, JettyTop + 1.4f, JettyEnd - 0.4f), new Vector3(0.16f, 2.8f, 0.16f), Quaternion.identity, Mat.DarkWood);
            kit.Box(new Vector3(-1.9f, JettyTop + 2.9f, JettyEnd - 0.4f), new Vector3(0.4f, 0.5f, 0.4f), Quaternion.identity, Mat.Glass);
            kit.Box(new Vector3(-1.9f, JettyTop + 3.2f, JettyEnd - 0.4f), new Vector3(0.5f, 0.1f, 0.5f), Quaternion.identity, Mat.Metal);

            Attach(root, kit, "DockMesh");

            // Colliders: the decks, the stairs as one smooth ramp, and the railings.
            var deck = root.AddComponent<BoxCollider>();
            deck.center = new Vector3(0f, -0.15f, (DeckEnd - 1.5f) * 0.5f);
            deck.size = new Vector3(4.4f, 0.3f, DeckEnd + 1.5f);
            var jetty = root.AddComponent<BoxCollider>();
            jetty.center = new Vector3(0f, JettyTop - 0.15f, (StairEnd + JettyEnd) * 0.5f);
            jetty.size = new Vector3(4.4f, 0.3f, JettyEnd - StairEnd);
            Vector3 along = new Vector3(0f, JettyTop, StairEnd) - new Vector3(0f, 0f, DeckEnd);
            var ramp = new GameObject("Stair Ramp").transform;
            ramp.SetParent(root.transform, false);
            ramp.localRotation = Quaternion.LookRotation(along.normalized, Vector3.up);
            ramp.localPosition = new Vector3(0f, 0f, DeckEnd) + along * 0.5f - ramp.localRotation * Vector3.up * 0.1f;
            var slope = ramp.gameObject.AddComponent<BoxCollider>();
            slope.size = new Vector3(3f, 0.2f, along.magnitude + 0.4f);
            foreach (float x in new[] { -1.55f, 1.55f })
            {
                var side = new GameObject("Stair Railing").transform;
                side.SetParent(ramp, false);
                side.localPosition = new Vector3(x, 0.6f, 0f);
                side.gameObject.AddComponent<BoxCollider>().size = new Vector3(0.12f, 1.2f, along.magnitude);
            }
            AddRailCollider(root, new Vector3(-2.15f, 0.6f, (DeckEnd - 1.2f) * 0.5f), new Vector3(0.12f, 1.2f, DeckEnd + 1.2f));
            AddRailCollider(root, new Vector3(2.15f, 0.6f, (DeckEnd - 1.2f) * 0.5f), new Vector3(0.12f, 1.2f, DeckEnd + 1.2f));

            Save(root, DockPath);
        }

        // ---------- Boat ----------

        private static void BuildBoat()
        {
            var kit = new FlatMeshKit<Mat>();
            var root = new GameObject("Boat");

            // Hull stations from the stern (-Z) to the bow (+Z): (z, half-width at the gunwale, gunwale height, keel depth).
            Vector4[] stations =
            {
                new Vector4(-3.3f, 1.15f, 0.85f, 0.45f), new Vector4(-1.6f, 1.35f, 0.85f, 0.6f), new Vector4(0.4f, 1.35f, 0.9f, 0.62f),
                new Vector4(2.1f, 1.0f, 1.05f, 0.5f), new Vector4(3.5f, 0.06f, 1.3f, 0.2f)
            };
            Vector3 Gunwale(Vector4 s, float side) => new Vector3(side * s.y, s.z, s.x);
            Vector3 Chine(Vector4 s, float side) => new Vector3(side * s.y * 0.78f, -0.05f, s.x);
            Vector3 Keel(Vector4 s) => new Vector3(0f, -s.w, s.x);
            for (int i = 0; i < stations.Length - 1; i++)
            {
                Vector4 a = stations[i], b = stations[i + 1];
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 outward = new Vector3(side, 0f, 0f);
                    kit.Quad(Gunwale(a, side), Gunwale(b, side), Chine(b, side), Chine(a, side), outward, Mat.Paint);
                    kit.Quad(Chine(a, side), Chine(b, side), Keel(b), Keel(a), outward + Vector3.down, Mat.DarkWood);
                }
            }
            // Flat transom at the stern, an inner floor and seats.
            Vector4 stern = stations[0];
            kit.Quad(Gunwale(stern, -1f), Gunwale(stern, 1f), Chine(stern, 1f), Chine(stern, -1f), Vector3.back, Mat.Paint);
            kit.Tri(Chine(stern, -1f), Chine(stern, 1f), Keel(stern), Vector3.back, Mat.DarkWood);
            kit.Box(new Vector3(0f, 0.12f, 0f), new Vector3(2.1f, 0.08f, 5.6f), Quaternion.identity, Mat.Wood);
            kit.Box(new Vector3(0f, 0.5f, 2.0f), new Vector3(1.7f, 0.1f, 0.5f), Quaternion.identity, Mat.Wood);
            kit.Box(new Vector3(0f, 0.5f, -2.6f), new Vector3(2.0f, 0.1f, 0.6f), Quaternion.identity, Mat.Wood);
            // Rub rail along both gunwales.
            for (int i = 0; i < stations.Length - 1; i++)
                foreach (float side in new[] { -1f, 1f })
                    Strut(kit, Gunwale(stations[i], side), Gunwale(stations[i + 1], side), 0.07f, Mat.Trim);

            // Little wheelhouse amidships: walls, windows, a canvas roof.
            kit.Box(new Vector3(0f, 0.95f, -0.6f), new Vector3(1.7f, 1.6f, 1.8f), Quaternion.identity, Mat.Wood);
            kit.Box(new Vector3(0f, 1.25f, 0.31f), new Vector3(1.3f, 0.55f, 0.04f), Quaternion.identity, Mat.Glass);
            kit.Box(new Vector3(0.86f, 1.25f, -0.6f), new Vector3(0.04f, 0.5f, 1.2f), Quaternion.identity, Mat.Glass);
            kit.Box(new Vector3(-0.86f, 1.25f, -0.6f), new Vector3(0.04f, 0.5f, 1.2f), Quaternion.identity, Mat.Glass);
            kit.Box(new Vector3(0f, 1.82f, -0.6f), new Vector3(2.0f, 0.12f, 2.2f), Quaternion.Euler(4f, 0f, 0f), Mat.Canvas);
            // Patched outboard motor on the transom, and a lamp pole at the bow.
            kit.Box(new Vector3(0f, 0.95f, -3.6f), new Vector3(0.55f, 0.7f, 0.6f), Quaternion.identity, Mat.Metal);
            kit.Box(new Vector3(0f, 0.25f, -3.65f), new Vector3(0.18f, 0.9f, 0.22f), Quaternion.identity, Mat.Metal);
            kit.Box(new Vector3(0f, 1.32f, -3.6f), new Vector3(0.6f, 0.08f, 0.66f), Quaternion.identity, Mat.Trim);
            kit.Box(new Vector3(0f, 1.6f, 2.9f), new Vector3(0.08f, 1.2f, 0.08f), Quaternion.identity, Mat.DarkWood);
            kit.Box(new Vector3(0f, 2.25f, 2.9f), new Vector3(0.22f, 0.3f, 0.22f), Quaternion.identity, Mat.Glass);
            kit.Box(new Vector3(0.75f, 0.25f, 1.2f), new Vector3(0.5f, 0.5f, 0.5f), Quaternion.Euler(0f, 20f, 0f), Mat.Rope);

            Attach(root, kit, "BoatMesh");
            var body = root.AddComponent<BoxCollider>();
            body.center = new Vector3(0f, 0.6f, 0f);
            body.size = new Vector3(2.6f, 2.2f, 7f);
            Save(root, BoatPath);
        }

        // ---------- Pieces ----------

        // Deck planks across the width, with thin gaps between them, from z0 to z1 with their tops at y.
        private static void Planks(FlatMeshKit<Mat> kit, float z0, float z1, float y, float width)
        {
            const float plank = 0.42f, gap = 0.05f;
            int n = 0;
            for (float z = z0; z + plank <= z1 + 0.01f; z += plank + gap, n++)
                kit.Box(new Vector3(0f, y - 0.07f, z + plank * 0.5f), new Vector3(width + (n % 3 - 1) * 0.08f, 0.14f, plank), Quaternion.identity, n % 4 == 1 ? Mat.DarkWood : Mat.Wood);
            Beam(kit, new Vector3(-width * 0.38f, y - 0.24f, (z0 + z1) * 0.5f), new Vector3(0.24f, 0.2f, z1 - z0));
            Beam(kit, new Vector3(width * 0.38f, y - 0.24f, (z0 + z1) * 0.5f), new Vector3(0.24f, 0.2f, z1 - z0));
        }

        private static void Beam(FlatMeshKit<Mat> kit, Vector3 centre, Vector3 size) => kit.Box(centre, size, Quaternion.identity, Mat.DarkWood);

        // A square post from the top down to the bottom height.
        private static void Piling(FlatMeshKit<Mat> kit, Vector3 top, float bottom)
        {
            float height = top.y - bottom;
            kit.Box(new Vector3(top.x, top.y - height * 0.5f, top.z), new Vector3(0.32f, height, 0.32f), Quaternion.identity, Mat.DarkWood);
        }

        // A square beam from a to b.
        private static void Strut(FlatMeshKit<Mat> kit, Vector3 a, Vector3 b, float thickness, Mat material)
        {
            Vector3 along = b - a;
            kit.Box((a + b) * 0.5f, new Vector3(thickness, thickness, along.magnitude), Quaternion.LookRotation(along.normalized, Vector3.up), material);
        }

        // A railing from a to b (both at deck level): posts about every 2 m and a hand rail 1 m up.
        private static void Rail(FlatMeshKit<Mat> kit, Vector3 a, Vector3 b)
        {
            int posts = Mathf.Max(1, Mathf.CeilToInt(new Vector2(b.x - a.x, b.z - a.z).magnitude / 2f));
            for (int i = 0; i <= posts; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / (float)posts);
                kit.Box(p + Vector3.up * 0.5f, new Vector3(0.12f, 1f, 0.12f), Quaternion.identity, Mat.DarkWood);
            }
            Strut(kit, a + Vector3.up * 1f, b + Vector3.up * 1f, 0.09f, Mat.Wood);
            Strut(kit, a + Vector3.up * 0.55f, b + Vector3.up * 0.55f, 0.05f, Mat.Rope);
        }

        private static void AddRailCollider(GameObject root, Vector3 centre, Vector3 size)
        {
            var rail = root.AddComponent<BoxCollider>();
            rail.center = centre;
            rail.size = size;
        }

        // ---------- Assets ----------

        private static void Attach(GameObject root, FlatMeshKit<Mat> kit, string name)
        {
            Mesh mesh = kit.Build(name);
            var materials = new Material[mesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = MaterialFor(FlatMeshKit<Mat>.MaterialOf(mesh, i));
            root.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, MeshFolder + "/" + name + ".asset");
            root.AddComponent<MeshRenderer>().sharedMaterials = materials;
            GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic);
        }

        private static Material MaterialFor(Mat key)
        {
            string path = key switch
            {
                Mat.Wood => MeadowAssetBuilder.Folder + "/Materials/MeadowLog.mat",
                Mat.DarkWood => MeadowAssetBuilder.Folder + "/Materials/MeadowBark.mat",
                Mat.Paint => MeadowAssetBuilder.Folder + "/Materials/MeadowTentTeal.mat",
                Mat.Trim => MeadowAssetBuilder.Folder + "/Materials/MeadowTentOrange.mat",
                Mat.Canvas => MeadowAssetBuilder.Folder + "/Materials/MeadowCanvas.mat",
                Mat.Rope => MeadowAssetBuilder.Folder + "/Materials/MeadowGrassDry.mat",
                Mat.Metal => CrashSiteWreckBuilder.Folder + "/Materials/WreckMetal.mat",
                _ => CrashSiteWreckBuilder.Folder + "/Materials/WreckCanopyGlass.mat"
            };
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) throw new InvalidOperationException("Missing material " + path + ". Build the meadow and shipwreck assets first.");
            return material;
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
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

        private static void Save(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
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
