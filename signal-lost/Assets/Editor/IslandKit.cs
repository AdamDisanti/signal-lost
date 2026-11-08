using System;
using System.Collections.Generic;
using SignalLost.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SignalLost.Editor
{
    /// <summary>
    /// Shared pieces for turning a stage into an island (Lawson, 11-07: no more cliff walls; each stage is an island in
    /// the sea, with docks at its ends and a boat to the next one). Used by the tutorial and desert builders:
    /// <list type="bullet">
    /// <item><see cref="Shore"/>: the height profile from the land down a sandy beach, across wadeable turquoise
    /// shallows and over a drop-off into deep water;</item>
    /// <item><see cref="BuildOcean"/>: the sea, a fine water mesh around the island (2 m cells, so the swell and the
    /// low-poly facets read up close) and a coarse skirt out to the horizon, with the <see cref="Ocean"/> component;</item>
    /// <item><see cref="BuildDock"/>: a timber dock on posts with a T-shaped end, bollards, a lantern, barrels and a
    /// coil of rope, the moored <see cref="BoatPrefab"/> sailboat and the <see cref="IslandDock"/> that makes it work;</item>
    /// <item><see cref="DistantIsland"/>: a low-poly island silhouette far out at sea, the next stage seen from the boat;</item>
    /// <item><see cref="NoWalk"/>: marks the sea floor so the NavMesh, and so enemies and spawns, stay on land.</item>
    /// </list>
    /// All art is generated here (no imported assets); meshes and materials are saved under <see cref="Folder"/>.
    /// </summary>
    internal static class IslandKit
    {
        public const string Folder = "Assets/Prefabs/Enviornment objects/Islands";
        public const string BoatPath = Folder + "/SailBoat.prefab";
        private const string MeshFolder = Folder + "/Meshes", MaterialFolder = Folder + "/Materials";

        /// <summary>Where the beach meets the water, in metres past the coastline (see <see cref="Shore"/>).</summary>
        public const float Waterline = 12f;

        // ---------- The beach ----------

        /// <summary>
        /// Ground height <paramref name="s"/> metres past the coastline (negative inland), given the land's own height
        /// there. Inland of -14 m the land is untouched; it then eases onto a gentle sandy beach that reaches the water
        /// about 12 m past the coastline, runs out under about 13 m of wadeable shallows (deeper than the 1.3 m wade
        /// depth from about 25 m) and drops off to around 10 m deep by 40 m.
        /// </summary>
        public static float Shore(float land, float s, float seaLevel)
        {
            float beach = seaLevel + 1.1f - s * (1.1f / Waterline);
            if (s > Waterline) beach = seaLevel - (s - Waterline) * 0.1f - 7f * Smooth(24f, 40f, s);
            return Mathf.Lerp(land, beach, Smooth(-14f, 0f, s));
        }

        private static float Smooth(float from, float to, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, x));

        /// <summary>
        /// Walks from <paramref name="inland"/> along <paramref name="direction"/> to where the ground first drops to
        /// about 0.75 m above the sea (the wet sand at the top of the waterline), where a dock starts.
        /// </summary>
        public static Vector2 ShoreStart(Vector2 inland, Vector2 direction, Func<float, float, float> ground, float seaLevel)
        {
            direction.Normalize();
            for (float d = 0f; d < 200f; d += 0.5f)
            {
                Vector2 p = inland + direction * d;
                if (ground(p.x, p.y) < seaLevel + 0.75f) return p;
            }
            return inland;
        }

        /// <summary>Makes a collider's ground not walkable for the NavMesh (the sea floor).</summary>
        public static void NoWalk(GameObject seabed)
        {
            var modifier = seabed.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true;
            modifier.area = NavMesh.GetAreaFromName("Not Walkable");
        }

        // ---------- The sea ----------

        /// <summary>
        /// The sea: a grid of <paramref name="step"/> m cells over the square <paramref name="half"/> m round
        /// <paramref name="centre"/> (skipping cells well above the water), then a coarse skirt out past 2.5 km. Vertex
        /// red is how close the ground is to the surface (the shader's no-depth-texture fallback, and it calms the swell
        /// at the very edge) and rises to 1 at the grid's border, where the skirt starts flat, so the two never crack.
        /// </summary>
        public static Ocean BuildOcean(Transform parent, float seaLevel, Vector2 centre, float half, float step,
            Func<float, float, float> ground, Vector3 islandCentre, string meshName)
        {
            var vertices = new List<Vector3>();
            var colours = new List<Color>();
            var triangles = new List<int>();
            int n = Mathf.RoundToInt(half * 2f / step);
            var index = new int[n + 1, n + 1];
            var height = new float[n + 1, n + 1];
            for (int i = 0; i <= n; i++)
            for (int k = 0; k <= n; k++)
            {
                index[i, k] = -1;
                height[i, k] = ground(centre.x - half + i * step, centre.y - half + k * step);
            }

            int Vertex(int i, int k)
            {
                if (index[i, k] >= 0) return index[i, k];
                float x = centre.x - half + i * step, z = centre.y - half + k * step;
                float shore = 1f - Mathf.Clamp01((seaLevel - height[i, k]) / 1.2f);
                float border = half - Mathf.Max(Mathf.Abs(x - centre.x), Mathf.Abs(z - centre.y));
                float calm = 1f - Smooth(0f, 24f, border);
                index[i, k] = vertices.Count;
                vertices.Add(new Vector3(x, 0f, z));
                colours.Add(new Color(Mathf.Max(shore, calm), 0f, 0f, 1f));
                return index[i, k];
            }

            // Water only where it joins the open sea: flood in from the grid's edge through cells that dip below the
            // surface, so low ground inland (a camp, a hollow, a dune trough) stays dry.
            var sea = new bool[n, n];
            var open = new Queue<(int, int)>();
            for (int i = 0; i < n; i++)
            for (int k = 0; k < n; k++)
                if (i == 0 || k == 0 || i == n - 1 || k == n - 1) Flood(i, k);
            while (open.Count > 0)
            {
                (int i, int k) = open.Dequeue();
                Flood(i - 1, k); Flood(i + 1, k); Flood(i, k - 1); Flood(i, k + 1);
            }
            void Flood(int i, int k)
            {
                if (i < 0 || k < 0 || i >= n || k >= n || sea[i, k]) return;
                float lowest = Mathf.Min(Mathf.Min(height[i, k], height[i + 1, k]), Mathf.Min(height[i, k + 1], height[i + 1, k + 1]));
                if (lowest > seaLevel + 0.4f) return; // dry land
                sea[i, k] = true;
                open.Enqueue((i, k));
            }

            for (int i = 0; i < n; i++)
            for (int k = 0; k < n; k++)
            {
                if (!sea[i, k]) continue;
                int a = Vertex(i, k), b = Vertex(i, k + 1), c = Vertex(i + 1, k + 1), d = Vertex(i + 1, k);
                // Clockwise seen from above, so the faces point up.
                triangles.AddRange(new[] { a, b, c, a, c, d });
            }

            // The skirt: big flat cells out to the horizon, leaving a hole exactly over the fine grid.
            float big = half * 2f / 4f;
            int rings = Mathf.CeilToInt((2500f - half) / big);
            float extent = half + rings * big;
            int cells = Mathf.RoundToInt(extent * 2f / big);
            var skirt = new Dictionary<(int, int), int>();
            int Skirt(int i, int k)
            {
                if (skirt.TryGetValue((i, k), out int existing)) return existing;
                skirt[(i, k)] = vertices.Count;
                vertices.Add(new Vector3(centre.x - extent + i * big, 0f, centre.y - extent + k * big));
                colours.Add(new Color(1f, 0f, 0f, 1f));
                return skirt[(i, k)];
            }
            for (int i = 0; i < cells; i++)
            for (int k = 0; k < cells; k++)
            {
                float x = centre.x - extent + (i + 0.5f) * big, z = centre.y - extent + (k + 0.5f) * big;
                if (Mathf.Abs(x - centre.x) < half && Mathf.Abs(z - centre.y) < half) continue;
                int a = Skirt(i, k), b = Skirt(i, k + 1), c = Skirt(i + 1, k + 1), d = Skirt(i + 1, k);
                triangles.AddRange(new[] { a, b, c, a, c, d });
            }

            var mesh = new Mesh { name = meshName, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetColors(colours);
            mesh.SetTriangles(triangles, 0);
            var normals = new Vector3[vertices.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.normals = normals;
            mesh.bounds = new Bounds(new Vector3(centre.x, 0f, centre.y), new Vector3(extent * 2f, 2f, extent * 2f));

            var water = new GameObject("Ocean");
            water.transform.SetParent(parent, false);
            water.transform.localPosition = new Vector3(0f, seaLevel, 0f);
            water.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh);
            var renderer = water.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = OceanMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var ocean = water.AddComponent<Ocean>();
            var data = new SerializedObject(ocean);
            data.FindProperty("islandCentre").vector3Value = islandCentre;
            data.ApplyModifiedPropertiesWithoutUndo();
            // The sea mask, at twice the water grid's cell size: sea wherever any of its water cells is.
            int size = (n + 1) / 2;
            var mask = new bool[size * size];
            for (int i = 0; i < n; i++)
            for (int k = 0; k < n; k++)
                if (sea[i, k]) mask[(k / 2) * size + i / 2] = true;
            ocean.SetSeaMask(new Vector2(centre.x - half, centre.y - half), step * 2f, size, mask);
            EditorUtility.SetDirty(ocean);
            return ocean;
        }

        /// <summary>The sea's material: the stylized water shader tuned for open water (longer swells, turquoise shallows).</summary>
        public static Material OceanMaterial()
        {
            Material material = LoadOrCreate("OceanWater", Shader.Find("Signal Lost/Stylized Water") ?? Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_DeepColor", new Color(0.04f, 0.32f, 0.55f, 1f));
            material.SetColor("_ShallowColor", new Color(0.34f, 0.87f, 0.83f, 0.26f));
            material.SetColor("_HorizonColor", new Color(0.8f, 0.9f, 0.95f, 1f));
            material.SetColor("_FoamColor", new Color(0.97f, 0.99f, 0.97f, 1f));
            material.SetFloat("_WaveHeight", 0.16f);
            material.SetFloat("_WaveScale", 0.07f);
            material.SetFloat("_WaveSpeed", 0.9f);
            material.SetFloat("_Sparkle", 0.9f);
            material.SetFloat("_Smoothness", 0.85f);
            material.SetFloat("_UseDepth", 1f);
            material.SetFloat("_DepthFade", 3.2f);
            material.SetFloat("_Clarity", 2.4f);
            material.SetFloat("_EdgeFoam", 0.28f);
            material.SetFloat("_WashLines", 0.75f);
            material.SetFloat("_CausticStrength", 0.8f);
            material.SetFloat("_CausticScale", 0.4f);
            material.SetFloat("_Facets", 0.6f);
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- Docks ----------

        private enum DockPart { Plank, PlankDark, Beam, Post, Rope, Iron, Lamp, Barrel, Crate }

        /// <summary>What a dock is for and where it goes.</summary>
        public sealed class DockSpec
        {
            public string Name;                   // also names its mesh, e.g. "TutorialDock"
            public Vector2 Start;                 // where it leaves the beach (x, z)
            public Vector2 Seaward;               // the way it runs out to sea
            public float Length = 20f;
            public IslandDock.Role Role = IslandDock.Role.Departure;
            public string IslandName = "";
            public string DestinationScene = "", DestinationName = "the next island", RequiredStage = "";
            public string LockedMessage = "The boat isn't ready to sail yet.";
        }

        private const float DeckWidth = 2.8f, EndWidth = 6.4f, EndLength = 5f;

        /// <summary>
        /// Builds a dock running from the beach out to sea, with its boat tied up alongside the T-shaped end, and returns
        /// its <see cref="IslandDock"/>. The deck is level, a hand's height above the sand where it starts; its posts go
        /// down into the sea floor. Build it before baking the NavMesh, so the deck is walkable.
        /// </summary>
        public static IslandDock BuildDock(Transform parent, DockSpec spec, float seaLevel, Func<float, float, float> ground, int seed)
        {
            var random = new System.Random(seed);
            float R(float min, float max) => min + (float)random.NextDouble() * (max - min);
            Vector2 sea = spec.Seaward.normalized, right = new Vector2(sea.y, -sea.x);
            float deckY = Mathf.Max(ground(spec.Start.x, spec.Start.y) + 0.12f, seaLevel + 0.85f);
            float length = spec.Length, total = length + EndLength;
            float GroundAt(float x, float z) { Vector2 p = spec.Start + sea * z + right * x; return ground(p.x, p.y) - deckY; }

            var kit = new FlatMeshKit<DockPart>();
            // Planks across the deck, each a little crooked; the T at the end is wider.
            for (float z = 0f; z < total - 0.1f; z += 0.36f)
            {
                float width = z < length ? DeckWidth : EndWidth;
                kit.Box(new Vector3(R(-0.04f, 0.04f), -0.05f + R(-0.012f, 0.012f), z + 0.17f), new Vector3(width + R(-0.06f, 0.1f), 0.09f, 0.31f),
                    Quaternion.Euler(0f, R(-1.2f, 1.2f), R(-0.6f, 0.6f)), random.NextDouble() < 0.3 ? DockPart.PlankDark : DockPart.Plank);
            }
            // Beams under the planks.
            foreach (float x in new[] { -DeckWidth * 0.5f + 0.3f, DeckWidth * 0.5f - 0.3f })
                kit.Box(new Vector3(x, -0.22f, length * 0.5f), new Vector3(0.22f, 0.24f, length + 0.4f), Quaternion.identity, DockPart.Beam);
            foreach (float x in new[] { -EndWidth * 0.5f + 0.3f, 0f, EndWidth * 0.5f - 0.3f })
                kit.Box(new Vector3(x, -0.22f, length + EndLength * 0.5f), new Vector3(0.22f, 0.24f, EndLength), Quaternion.identity, DockPart.Beam);

            var colliders = new List<(Vector3 centre, Vector3 size)>
            {
                (new Vector3(0f, -0.11f, length * 0.5f), new Vector3(DeckWidth, 0.22f, length)),
                (new Vector3(0f, -0.11f, length + EndLength * 0.5f), new Vector3(EndWidth, 0.22f, EndLength))
            };

            // Posts: down into the sea floor, every other pair standing up above the deck like the reference's.
            void Post(float x, float z, float radius, float above)
            {
                float bottom = Mathf.Min(GroundAt(x, z) - 0.6f, -0.4f);
                float top = above > 0f ? above : -0.12f;
                kit.Add(LowPolyMesh.TaperedPrism("post", 7, 0.9f), Matrix4x4.TRS(new Vector3(x, bottom, z), Quaternion.Euler(-90f, R(0f, 360f), 0f),
                    new Vector3(radius, radius, top - bottom)), DockPart.Post);
                if (above > 0f) colliders.Add((new Vector3(x, above * 0.5f, z), new Vector3(radius * 2f, above, radius * 2f)));
            }
            int row = 0;
            for (float z = 0.4f; z < length; z += 3f, row++)
            {
                foreach (float side in new[] { -1f, 1f }) Post(side * (DeckWidth * 0.5f + 0.1f), z, 0.15f, row % 2 == 1 ? R(0.6f, 0.85f) : 0f);
                if (GroundAt(0f, z) < -1.2f) kit.Box(new Vector3(0f, -0.65f, z), new Vector3(DeckWidth + 0.3f, 0.14f, 0.16f), Quaternion.identity, DockPart.Beam);
            }
            foreach (Vector2 corner in new[] { new Vector2(-1f, 0f), new Vector2(1f, 0f), new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f) })
                Post(corner.x * (EndWidth * 0.5f + 0.1f), length + corner.y * (EndLength - 0.2f) + 0.1f, 0.17f, corner.y > 0f && corner.x != 0f ? 0.9f : 0f);

            // Bollards to tie the boat to, on the boat's side of the end.
            foreach (float z in new[] { length + 0.8f, length + EndLength - 0.8f })
            {
                Vector3 at = new Vector3(EndWidth * 0.5f - 0.35f, 0f, z);
                kit.Add(LowPolyMesh.TaperedPrism("bollard", 8, 0.85f), Matrix4x4.TRS(at, Quaternion.Euler(-90f, 0f, 0f), new Vector3(0.17f, 0.17f, 0.42f)), DockPart.Iron);
                kit.Add(LowPolyMesh.TaperedPrism("cap", 8, 0.7f), Matrix4x4.TRS(at + Vector3.up * 0.42f, Quaternion.Euler(-90f, 0f, 0f), new Vector3(0.22f, 0.22f, 0.08f)), DockPart.Iron);
                colliders.Add((at + Vector3.up * 0.25f, new Vector3(0.35f, 0.5f, 0.35f)));
            }

            // A lantern on a post at the far corner, away from the boat.
            Vector3 lamp = new Vector3(-EndWidth * 0.5f + 0.35f, 0f, length + EndLength - 0.35f);
            kit.Add(LowPolyMesh.TaperedPrism("lamp post", 6, 0.8f), Matrix4x4.TRS(lamp, Quaternion.Euler(-90f, 0f, 0f), new Vector3(0.1f, 0.1f, 2.3f)), DockPart.Post);
            kit.Box(lamp + new Vector3(0.25f, 2.2f, 0f), new Vector3(0.55f, 0.07f, 0.07f), Quaternion.identity, DockPart.Iron);
            kit.Box(lamp + new Vector3(0.48f, 1.95f, 0f), new Vector3(0.22f, 0.3f, 0.22f), Quaternion.identity, DockPart.Lamp);
            kit.Box(lamp + new Vector3(0.48f, 2.13f, 0f), new Vector3(0.3f, 0.06f, 0.3f), Quaternion.identity, DockPart.Iron);
            colliders.Add((lamp + Vector3.up * 1.15f, new Vector3(0.2f, 2.3f, 0.2f)));

            // Barrels, a crate and a coil of rope where the dock leaves the beach.
            foreach (Vector3 at in new[] { new Vector3(DeckWidth * 0.5f - 0.4f, 0f, 1.4f), new Vector3(DeckWidth * 0.5f - 0.45f, 0f, 2.1f) })
            {
                kit.Add(LowPolyMesh.Lathe("barrel", new[] { new Vector2(0f, 0f), new Vector2(0.27f, 0f), new Vector2(0.31f, 0.3f), new Vector2(0.27f, 0.62f), new Vector2(0f, 0.62f) }, 9),
                    Matrix4x4.TRS(at, Quaternion.Euler(0f, R(0f, 360f), 0f), Vector3.one), DockPart.Barrel);
                foreach (float y in new[] { 0.1f, 0.52f })
                    kit.Add(LowPolyMesh.Lathe("hoop", new[] { new Vector2(0.28f, y - 0.03f), new Vector2(0.305f, y - 0.03f), new Vector2(0.305f, y + 0.03f), new Vector2(0.28f, y + 0.03f) }, 9),
                        Matrix4x4.Translate(at), DockPart.Iron);
                colliders.Add((at + Vector3.up * 0.31f, new Vector3(0.6f, 0.62f, 0.6f)));
            }
            Vector3 crate = new Vector3(-DeckWidth * 0.5f + 0.45f, 0.3f, 2.6f);
            kit.Box(crate, new Vector3(0.6f, 0.6f, 0.6f), Quaternion.Euler(0f, 12f, 0f), DockPart.Crate);
            kit.Box(crate + Vector3.up * 0.3f, new Vector3(0.64f, 0.06f, 0.64f), Quaternion.Euler(0f, 12f, 0f), DockPart.PlankDark);
            colliders.Add((crate, new Vector3(0.65f, 0.6f, 0.65f)));
            var coil = new List<Vector3>();
            for (int i = 0; i <= 54; i++)
            {
                float a = i * Mathf.PI * 2f / 18f, r = 0.26f - i * 0.0012f;
                coil.Add(new Vector3(EndWidth * 0.5f - 1.1f + Mathf.Cos(a) * r, 0.04f + i * 0.0022f, length + 1.7f + Mathf.Sin(a) * r));
            }
            kit.Add(LowPolyMesh.Tube("rope", coil.ToArray(), 0.035f, 5), Matrix4x4.identity, DockPart.Rope);

            Mesh mesh = SaveKitMesh<DockPart>(kit.Build(spec.Name));
            var root = new GameObject(spec.Role == IslandDock.Role.Departure ? $"Dock (boat to {spec.DestinationName})" : "Dock (arrival)");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(new Vector3(spec.Start.x, deckY, spec.Start.y), Quaternion.LookRotation(new Vector3(sea.x, 0f, sea.y)));
            var deck = new GameObject("Deck");
            deck.transform.SetParent(root.transform, false);
            deck.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = deck.AddComponent<MeshRenderer>();
            var materials = new Material[mesh.subMeshCount];
            for (int s = 0; s < materials.Length; s++) materials[s] = DockMaterial(FlatMeshKit<DockPart>.MaterialOf(mesh, s));
            renderer.sharedMaterials = materials;
            foreach ((Vector3 centre, Vector3 size) in colliders)
            {
                var box = deck.AddComponent<BoxCollider>();
                box.center = centre;
                box.size = size;
            }
            deck.isStatic = true;

            var light = new GameObject("Lantern Light").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.transform.localPosition = lamp + new Vector3(0.48f, 1.85f, 0f);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.74f, 0.42f);
            light.range = 8f;
            light.intensity = 1.6f;

            Transform Marker(string name, Vector3 local)
            {
                var marker = new GameObject(name).transform;
                marker.SetParent(root.transform, false);
                marker.localPosition = local;
                return marker;
            }
            // The boat lies alongside the end of the dock, bow out to sea, its waterline on the surface.
            Transform mooring = Marker("Mooring", new Vector3(EndWidth * 0.5f + 1.45f, seaLevel - deckY, length + EndLength * 0.5f));
            Transform landing = Marker("Landing", new Vector3(0.6f, 0.02f, length + 0.9f));
            var boat = (GameObject)PrefabUtility.InstantiatePrefab(BoatPrefab(), root.transform);
            boat.transform.localPosition = mooring.localPosition;
            boat.transform.localRotation = Quaternion.identity;
            boat.name = "Boat";

            var dock = root.AddComponent<IslandDock>();
            var data = new SerializedObject(dock);
            data.FindProperty("role").enumValueIndex = (int)spec.Role;
            data.FindProperty("islandName").stringValue = spec.IslandName;
            data.FindProperty("boat").objectReferenceValue = boat.transform;
            data.FindProperty("mooring").objectReferenceValue = mooring;
            data.FindProperty("landing").objectReferenceValue = landing;
            data.FindProperty("destinationScene").stringValue = spec.DestinationScene;
            data.FindProperty("destinationName").stringValue = spec.DestinationName;
            data.FindProperty("requiredStage").stringValue = spec.RequiredStage;
            data.FindProperty("lockedMessage").stringValue = spec.LockedMessage;
            data.ApplyModifiedPropertiesWithoutUndo();
            var boatData = new SerializedObject(boat.GetComponent<DockBoat>());
            boatData.FindProperty("dock").objectReferenceValue = dock;
            boatData.ApplyModifiedPropertiesWithoutUndo();
            return dock;
        }

        private static Material DockMaterial(DockPart part)
        {
            switch (part)
            {
                case DockPart.Plank: return Lit("DockPlank", new Color(0.64f, 0.45f, 0.28f), 0.08f);
                case DockPart.PlankDark: return Lit("DockPlankDark", new Color(0.5f, 0.34f, 0.21f), 0.08f);
                case DockPart.Beam: return Lit("DockBeam", new Color(0.36f, 0.24f, 0.15f), 0.05f);
                case DockPart.Post: return Lit("DockPost", new Color(0.44f, 0.3f, 0.19f), 0.05f);
                case DockPart.Rope: return Lit("DockRope", new Color(0.82f, 0.72f, 0.5f), 0.05f);
                case DockPart.Iron: return Lit("DockIron", new Color(0.24f, 0.24f, 0.26f), 0.4f);
                case DockPart.Barrel: return Lit("DockBarrel", new Color(0.56f, 0.36f, 0.2f), 0.1f);
                case DockPart.Crate: return Lit("DockCrate", new Color(0.7f, 0.55f, 0.34f), 0.08f);
                default:
                    Material lamp = Lit("DockLamp", new Color(1f, 0.82f, 0.5f), 0.3f);
                    lamp.EnableKeyword("_EMISSION");
                    lamp.SetColor("_EmissionColor", new Color(1f, 0.7f, 0.35f) * 2.2f);
                    lamp.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    return lamp;
            }
        }

        // ---------- The boat ----------

        private enum BoatPart { Hull, Inside, Trim, Mast, Sail, Flag, Rope }

        /// <summary>
        /// A small wooden sailboat (about 5 m), its waterline at its origin and its bow along +Z: a planked hull, two
        /// thwarts, a mast with a mainsail, jib and pennant, and a wake trail at the stern. Rebuilt in place each time,
        /// so its GUID is kept.
        /// </summary>
        public static GameObject BoatPrefab()
        {
            var kit = new FlatMeshKit<BoatPart>();
            // Hull sections from stern to bow: z, half-width, gunwale height, keel depth.
            Vector4[] sections =
            {
                new Vector4(-2.3f, 0.8f, 0.62f, -0.18f), new Vector4(-1.6f, 0.98f, 0.58f, -0.34f), new Vector4(-0.8f, 1.08f, 0.56f, -0.42f),
                new Vector4(0f, 1.1f, 0.56f, -0.45f), new Vector4(0.8f, 1.02f, 0.6f, -0.43f), new Vector4(1.6f, 0.8f, 0.68f, -0.36f),
                new Vector4(2.2f, 0.48f, 0.78f, -0.22f), new Vector4(2.75f, 0.02f, 0.92f, 0.05f)
            };
            Vector3[] Section(Vector4 s, float inset)
            {
                float w = s.y * inset, top = s.z - (1f - inset) * 0.3f, keel = s.w + (1f - inset) * 1.6f;
                return new[]
                {
                    new Vector3(-w, top, s.x), new Vector3(-w * 0.86f, Mathf.Lerp(top, keel, 0.55f), s.x), new Vector3(0f, keel, s.x),
                    new Vector3(w * 0.86f, Mathf.Lerp(top, keel, 0.55f), s.x), new Vector3(w, top, s.x)
                };
            }
            for (int i = 0; i < sections.Length - 1; i++)
            {
                Vector3[] a = Section(sections[i], 1f), b = Section(sections[i + 1], 1f);
                Vector3[] ai = Section(sections[i], 0.9f), bi = Section(sections[i + 1], 0.9f);
                Vector3 axis = new Vector3(0f, 0.2f, (sections[i].x + sections[i + 1].x) * 0.5f);
                for (int j = 0; j < 4; j++)
                {
                    Vector3 mid = (a[j] + a[j + 1] + b[j] + b[j + 1]) * 0.25f;
                    kit.Quad(a[j], a[j + 1], b[j + 1], b[j], mid - axis, BoatPart.Hull);
                    Vector3 midIn = (ai[j] + ai[j + 1] + bi[j] + bi[j + 1]) * 0.25f;
                    kit.Quad(ai[j], ai[j + 1], bi[j + 1], bi[j], axis - midIn, BoatPart.Inside);
                }
                // The gunwale: a dark rail joining the outside and inside along the top.
                foreach (int j in new[] { 0, 4 })
                    kit.Quad(a[j], b[j], bi[j], ai[j], Vector3.up, BoatPart.Trim);
            }
            // The transom across the stern, outside and inside.
            Vector3[] stern = Section(sections[0], 1f), sternIn = Section(sections[0], 0.9f);
            for (int j = 0; j < 4; j++)
            {
                kit.Tri(stern[j], stern[j + 1], new Vector3(0f, stern[0].y, stern[0].z), Vector3.back, BoatPart.Hull);
                kit.Tri(sternIn[j], sternIn[j + 1], new Vector3(0f, sternIn[0].y, sternIn[0].z), Vector3.forward, BoatPart.Inside);
            }
            for (int j = 0; j < 5; j++) kit.Quad(stern[j], sternIn[j], sternIn[Mathf.Min(j + 1, 4)], stern[Mathf.Min(j + 1, 4)], Vector3.back, BoatPart.Trim);
            // A stripe along the hull below the gunwale.
            for (int i = 0; i < sections.Length - 2; i++)
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector4 s0 = sections[i], s1 = sections[i + 1];
                    Vector3 p0 = new Vector3(side * s0.y * 1.005f, s0.z - 0.08f, s0.x), p1 = new Vector3(side * s1.y * 1.005f, s1.z - 0.08f, s1.x);
                    kit.Quad(p0, p1, p1 + Vector3.down * 0.09f, p0 + Vector3.down * 0.09f, new Vector3(side, 0f, 0f), BoatPart.Trim);
                }

            // Floorboards and two thwarts (seats).
            kit.Box(new Vector3(0f, -0.2f, -0.1f), new Vector3(0.9f, 0.06f, 3.6f), Quaternion.identity, BoatPart.Trim);
            kit.Box(new Vector3(0f, 0.2f, -1.3f), new Vector3(1.8f, 0.07f, 0.34f), Quaternion.identity, BoatPart.Trim);
            kit.Box(new Vector3(0f, 0.22f, 0.9f), new Vector3(1.85f, 0.07f, 0.34f), Quaternion.identity, BoatPart.Trim);

            // Mast, boom, sails and pennant.
            Vector3 foot = new Vector3(0f, -0.2f, 0.9f), top = new Vector3(0f, 4.6f, 0.9f);
            kit.Add(LowPolyMesh.TaperedPrism("mast", 6, 0.6f), Matrix4x4.TRS(foot, Quaternion.Euler(-90f, 0f, 0f), new Vector3(0.08f, 0.08f, top.y - foot.y)), BoatPart.Mast);
            kit.Add(LowPolyMesh.Tube("boom", new[] { new Vector3(0f, 1.05f, 0.85f), new Vector3(0.12f, 1.0f, -2.05f) }, 0.05f, 5), Matrix4x4.identity, BoatPart.Mast);
            void Sail(Vector3 a, Vector3 b, Vector3 c, Vector3 belly)
            {
                Vector3 m = (a + b + c) / 3f + belly;
                foreach (Vector3 face in new[] { belly, -belly })
                {
                    kit.Tri(a, b, m, face, BoatPart.Sail);
                    kit.Tri(b, c, m, face, BoatPart.Sail);
                    kit.Tri(c, a, m, face, BoatPart.Sail);
                }
            }
            Sail(new Vector3(0f, 1.12f, 0.8f), new Vector3(0f, 4.4f, 0.82f), new Vector3(0.12f, 1.08f, -2.0f), new Vector3(0.38f, 0f, 0f));
            Sail(new Vector3(0f, 3.7f, 0.98f), new Vector3(0f, 0.95f, 2.6f), new Vector3(0.05f, 1.0f, 1.25f), new Vector3(0.26f, 0f, 0f));
            foreach (Vector3 face in new[] { Vector3.right, Vector3.left })
                kit.Tri(new Vector3(0f, 4.58f, 0.92f), new Vector3(0f, 4.3f, 0.92f), new Vector3(0.05f, 4.48f, 0.3f), face, BoatPart.Flag);
            // Stays from the masthead to bow and stern.
            kit.Add(LowPolyMesh.Tube("forestay", new[] { top, new Vector3(0f, 0.9f, 2.7f) }, 0.015f, 4), Matrix4x4.identity, BoatPart.Rope);
            kit.Add(LowPolyMesh.Tube("backstay", new[] { top, new Vector3(0f, 0.65f, -2.25f) }, 0.015f, 4), Matrix4x4.identity, BoatPart.Rope);

            Mesh mesh = SaveKitMesh<BoatPart>(kit.Build("SailBoat"));
            var root = new GameObject("Sail Boat");
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            var materials = new Material[mesh.subMeshCount];
            for (int s = 0; s < materials.Length; s++) materials[s] = BoatMaterial(FlatMeshKit<BoatPart>.MaterialOf(mesh, s));
            renderer.sharedMaterials = materials;
            var hull = root.AddComponent<BoxCollider>();
            hull.center = new Vector3(0f, 0.2f, 0.2f);
            hull.size = new Vector3(2.2f, 1.3f, 5.1f);
            var sail = root.AddComponent<BoxCollider>();
            sail.center = new Vector3(0.15f, 2.6f, -0.3f);
            sail.size = new Vector3(0.5f, 3f, 3f);
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var wakeObject = new GameObject("Wake");
            wakeObject.transform.SetParent(root.transform, false);
            wakeObject.transform.localPosition = new Vector3(0f, 0.04f, -2.3f);
            wakeObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // lies flat on the water
            var wake = wakeObject.AddComponent<TrailRenderer>();
            wake.time = 3.5f;
            wake.minVertexDistance = 0.6f;
            wake.alignment = LineAlignment.TransformZ;
            wake.widthCurve = new AnimationCurve(new Keyframe(0f, 1.5f), new Keyframe(1f, 4f));
            wake.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0.75f, 0f), new GradientAlphaKey(0f, 1f) }
            };
            wake.shadowCastingMode = ShadowCastingMode.Off;
            wake.receiveShadows = false;
            wake.emitting = false;

            var boat = root.AddComponent<DockBoat>();
            var data = new SerializedObject(boat);
            data.FindProperty("wake").objectReferenceValue = wake;
            data.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolders();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BoatPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Material BoatMaterial(BoatPart part)
        {
            switch (part)
            {
                case BoatPart.Hull: return Lit("BoatHull", new Color(0.52f, 0.32f, 0.19f), 0.12f);
                case BoatPart.Inside: return Lit("BoatInside", new Color(0.7f, 0.5f, 0.31f), 0.08f);
                case BoatPart.Trim: return Lit("BoatTrim", new Color(0.34f, 0.21f, 0.13f), 0.1f);
                case BoatPart.Mast: return Lit("BoatMast", new Color(0.46f, 0.31f, 0.19f), 0.1f);
                case BoatPart.Sail: return Lit("BoatSail", new Color(0.94f, 0.87f, 0.73f), 0.05f);
                case BoatPart.Flag: return Lit("BoatFlag", new Color(0.86f, 0.28f, 0.18f), 0.1f);
                default: return Lit("BoatRope", new Color(0.8f, 0.7f, 0.5f), 0.05f);
            }
        }

        // ---------- Islands on the horizon ----------

        private enum IslePart { Sand, Low, High, Rock, Tree }

        /// <summary>
        /// A low-poly island far out at sea: a sandy rim, slopes and a peak, with a few trees if it is green. It is only
        /// scenery for the voyage (no collider). <paramref name="green"/> picks a meadow-like island, otherwise a dry one
        /// of sand and red rock like the desert.
        /// </summary>
        public static GameObject DistantIsland(Transform parent, string name, Vector3 position, float radius, float height, bool green, int seed)
        {
            var random = new System.Random(seed);
            float R(float min, float max) => min + (float)random.NextDouble() * (max - min);
            const int segments = 28, rings = 7;
            float offset = R(0f, 100f);
            float Height(float r, float angle)
            {
                float edge = 1f + 0.18f * (Mathf.PerlinNoise(Mathf.Cos(angle) * 2f + offset, Mathf.Sin(angle) * 2f + offset) - 0.5f) * 2f;
                float t = r / (radius * edge);
                if (t > 1f) return Mathf.Lerp(1.2f, -3f, (t - 1f) / 0.2f);
                float peak = height * Mathf.Pow(1f - t, 1.5f) * (0.8f + 0.4f * Mathf.PerlinNoise(r * 0.03f + offset, angle * 1.3f));
                return 1.2f + peak;
            }
            var points = new Vector3[rings + 1, segments];
            for (int ring = 0; ring <= rings; ring++)
            for (int s = 0; s < segments; s++)
            {
                float angle = s * Mathf.PI * 2f / segments, r = ring == 0 ? 0f : radius * 1.2f * Mathf.Pow(ring / (float)rings, 0.8f);
                float jitter = ring == 0 || ring == rings ? 0f : R(-0.04f, 0.04f) * radius;
                points[ring, s] = new Vector3(Mathf.Cos(angle) * (r + jitter), Height(r, angle), Mathf.Sin(angle) * (r + jitter));
            }
            var kit = new FlatMeshKit<IslePart>();
            IslePart Surface(float y) => y < 2.6f ? IslePart.Sand : y < height * 0.45f ? IslePart.Low : y < height * 0.75f ? IslePart.High : IslePart.Rock;
            for (int ring = 0; ring < rings; ring++)
            for (int s = 0; s < segments; s++)
            {
                int t = (s + 1) % segments;
                Vector3 a = points[ring, s], b = points[ring, t], c = points[ring + 1, t], d = points[ring + 1, s];
                kit.Tri(a, b, c, Vector3.up, Surface((a.y + b.y + c.y) / 3f));
                kit.Tri(a, c, d, Vector3.up, Surface((a.y + c.y + d.y) / 3f));
            }
            int trees = green ? 26 : 6;
            for (int i = 0; i < trees; i++)
            {
                float angle = R(0f, Mathf.PI * 2f), r = R(0.25f, 0.8f) * radius, y = Height(r, angle);
                Vector3 at = new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
                float size = R(4f, 7f);
                kit.Add(LowPolyMesh.TaperedPrism("tree", 5, 0f), Matrix4x4.TRS(at, Quaternion.Euler(-90f, R(0f, 360f), 0f), new Vector3(size * 0.45f, size * 0.45f, size * 1.6f)), IslePart.Tree);
            }
            Mesh mesh = SaveKitMesh<IslePart>(kit.Build(name));
            var island = new GameObject(name);
            island.transform.SetParent(parent, false);
            island.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, R(0f, 360f), 0f));
            island.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = island.AddComponent<MeshRenderer>();
            var materials = new Material[mesh.subMeshCount];
            for (int s = 0; s < materials.Length; s++) materials[s] = IsleMaterial(FlatMeshKit<IslePart>.MaterialOf(mesh, s), green);
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            island.isStatic = true;
            return island;
        }

        private static Material IsleMaterial(IslePart part, bool green)
        {
            string kind = green ? "Green" : "Dry";
            switch (part)
            {
                case IslePart.Sand: return Lit("IsleSand", new Color(0.88f, 0.78f, 0.56f), 0.05f);
                case IslePart.Low: return green ? Lit("IsleGrass", new Color(0.4f, 0.6f, 0.26f), 0.05f) : Lit("IsleDune", new Color(0.86f, 0.66f, 0.44f), 0.05f);
                case IslePart.High: return green ? Lit("IsleForest", new Color(0.26f, 0.44f, 0.22f), 0.05f) : Lit("IsleMesa", new Color(0.72f, 0.45f, 0.31f), 0.05f);
                case IslePart.Rock: return Lit("IsleRock" + kind, green ? new Color(0.55f, 0.53f, 0.5f) : new Color(0.55f, 0.34f, 0.25f), 0.05f);
                default: return green ? Lit("IsleTree", new Color(0.2f, 0.38f, 0.2f), 0.05f) : Lit("IsleCactus", new Color(0.27f, 0.6f, 0.5f), 0.1f);
            }
        }

        // ---------- Assets ----------

        public static Material Lit(string name, Color color, float smoothness)
        {
            Material material = LoadOrCreate(name, Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreate(string name, Shader shader)
        {
            EnsureFolders();
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader) material.shader = shader;
            return material;
        }

        /// <summary>Saves a mesh under the kit's folder, replacing the contents of an existing one so its GUID is kept.</summary>
        public static Mesh SaveMesh(Mesh mesh)
        {
            EnsureFolders();
            string path = MeshFolder + "/" + mesh.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        // A FlatMeshKit mesh remembers which material each submesh is for; keep that when the asset is reused.
        private static Mesh SaveKitMesh<T>(Mesh mesh) where T : struct, Enum
        {
            string order = FlatMeshKit<T>.Order(mesh);
            Mesh saved = SaveMesh(mesh);
            FlatMeshKit<T>.Remember(saved, order);
            return saved;
        }

        private static void EnsureFolders()
        {
            foreach (string path in new[] { "Assets/Prefabs/Enviornment objects", Folder, MeshFolder, MaterialFolder })
            {
                if (AssetDatabase.IsValidFolder(path)) continue;
                int slash = path.LastIndexOf('/');
                AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
            }
        }
    }
}
