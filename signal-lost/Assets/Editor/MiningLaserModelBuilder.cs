using System.Collections.Generic;
using SignalLost.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Editor
{
    /// <summary>
    /// Rebuilds the mining laser model (Upgrades/MiningLaser.prefab) in place from code: a machined tool with a
    /// see-through charge chamber in the middle. Glass side windows show a core rod and a row of coil rings that light
    /// up as a shot charges (<see cref="LaserChargeVisual"/>, driven by <see cref="MiningLaser"/>).
    ///
    /// The prefab's root object, GripAnchor and MuzzleAnchor are kept (their positions are unchanged), so the player
    /// prefab, test scenes and the tutorial pickup keep pointing at it; only the visual children are replaced. Meshes
    /// (bevelled boxes, lathed barrels, rings) go into Upgrades/Meshes/MiningLaserParts.asset; materials reuse the old
    /// Laser* colours and add LaserGlass and LaserEnergy. Model space: +Z forward, Y up, barrel centre-line at y 0.32.
    /// </summary>
    public static class MiningLaserModelBuilder
    {
        private const string Folder = "Assets/Prefabs/Enviornment objects/TutorialCrashSite/Upgrades";
        private const string PrefabPath = Folder + "/MiningLaser.prefab";
        private const string MeshPath = Folder + "/Meshes/MiningLaserParts.asset";
        private const float Axis = 0.32f; // barrel centre-line height

        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        private static Material graphite, steel, orange, teal, rubber, glass, energy, dark;

        [MenuItem("Signal Lost/Models/Rebuild Mining Laser")]
        public static void Build()
        {
            Meshes.Clear();
            LoadMaterials();

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                // Keep the root (other assets reference it) and the anchors; replace everything else.
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = root.transform.GetChild(i);
                    if (child.name != "GripAnchor" && child.name != "MuzzleAnchor") Object.DestroyImmediate(child.gameObject);
                }
                foreach (LaserChargeVisual old in root.GetComponents<LaserChargeVisual>()) Object.DestroyImmediate(old);

                var visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
                visual.SetSiblingIndex(0);
                BuildModel(visual, root);

                var collider = root.GetComponent<BoxCollider>();
                if (collider == null) collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, 0.25f, 0.08f);
                collider.size = new Vector3(0.28f, 0.46f, 0.86f);

                SaveMeshes();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("MINING_LASER_MODEL_BUILT " + Meshes.Count + " meshes");
        }

        /// <summary>Batch entry: Unity -batchmode -executeMethod SignalLost.Editor.MiningLaserModelBuilder.BuildBatch -quit</summary>
        public static void BuildBatch() => Build();

        // ---------- The model ----------

        private static void BuildModel(Transform visual, GameObject root)
        {
            // Charge chamber: an open frame between two bulkheads, glazed on both sides.
            const float back = -0.13f, front = 0.20f, mid = (back + front) * 0.5f, length = front - back;
            Box(visual, "Top Spine", new Vector3(0f, 0.405f, mid), new Vector3(0.21f, 0.035f, length + 0.04f), orange, 0.012f);
            Box(visual, "Top Rail", new Vector3(0f, 0.432f, mid + 0.02f), new Vector3(0.12f, 0.022f, length - 0.04f), graphite, 0.006f);
            Box(visual, "Keel", new Vector3(0f, 0.232f, mid), new Vector3(0.21f, 0.035f, length + 0.04f), orange, 0.012f);
            Box(visual, "Rear Bulkhead", new Vector3(0f, Axis, back - 0.025f), new Vector3(0.235f, 0.21f, 0.05f), graphite, 0.016f);
            Box(visual, "Front Bulkhead", new Vector3(0f, Axis, front + 0.022f), new Vector3(0.235f, 0.21f, 0.045f), graphite, 0.016f);
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * 0.104f;
                Box(visual, "Window Frame Top", new Vector3(x, 0.382f, mid), new Vector3(0.018f, 0.016f, length), steel, 0.004f);
                Box(visual, "Window Frame Bottom", new Vector3(x, 0.258f, mid), new Vector3(0.018f, 0.016f, length), steel, 0.004f);
                foreach (float z in new[] { back + 0.008f, mid, front - 0.008f })
                    Box(visual, "Window Post", new Vector3(x, Axis, z), new Vector3(0.02f, 0.11f, 0.016f), steel, 0.004f);
                Box(visual, "Window Glass", new Vector3(side * 0.1f, Axis, mid), new Vector3(0.004f, 0.11f, length - 0.01f), glass, 0f);
                foreach (float y in new[] { 0.36f, 0.28f })
                    foreach (float z in new[] { back - 0.025f, front + 0.022f })
                        Cylinder(visual, "Bolt", new Vector3(side * 0.119f, y, z), Quaternion.Euler(0f, 90f, 0f), 0.009f, 0.008f, steel, 6);
            }

            // Inside the chamber: rails, a core rod in a dark sleeve, and coil rings around it.
            Box(visual, "Chamber Floor", new Vector3(0f, 0.255f, mid), new Vector3(0.18f, 0.012f, length), dark, 0.003f);
            foreach (float side in new[] { -1f, 1f })
                Box(visual, "Core Rail", new Vector3(side * 0.045f, 0.27f, mid), new Vector3(0.012f, 0.02f, length), graphite, 0.003f);
            MeshRenderer core = Lathe(visual, "Charge Core", new Vector3(0f, Axis, back), new[]
            {
                new Vector2(0f, 0f), new Vector2(0.018f, 0f), new Vector2(0.024f, 0.02f), new Vector2(0.024f, length - 0.02f),
                new Vector2(0.018f, length), new Vector2(0f, length),
            }, energy, 20);
            foreach (float z in new[] { back + 0.005f, front - 0.005f })
                Lathe(visual, "Core Socket", new Vector3(0f, Axis, z - 0.012f), new[]
                {
                    new Vector2(0.026f, 0f), new Vector2(0.042f, 0f), new Vector2(0.042f, 0.024f), new Vector2(0.026f, 0.024f), new Vector2(0.026f, 0f),
                }, steel, 20);
            const int coilCount = 6;
            var coils = new Renderer[coilCount];
            for (int i = 0; i < coilCount; i++)
            {
                float z = Mathf.Lerp(back + 0.04f, front - 0.04f, i / (coilCount - 1f));
                coils[i] = Ring(visual, "Charge Coil " + (i + 1), new Vector3(0f, Axis, z), 0.05f, 0.0075f, energy);
                Box(visual, "Coil Clamp", new Vector3(0f, 0.274f, z), new Vector3(0.11f, 0.012f, 0.012f), graphite, 0.002f);
            }

            // Rear: power pack, cartridge, shoulder pad and vents.
            Box(visual, "Power Pack", new Vector3(0f, 0.305f, -0.235f), new Vector3(0.21f, 0.175f, 0.13f), graphite, 0.02f);
            Box(visual, "Power Pack Cap", new Vector3(0f, 0.398f, -0.235f), new Vector3(0.17f, 0.018f, 0.1f), orange, 0.006f);
            Box(visual, "Power Cartridge", new Vector3(0f, 0.2f, -0.245f), new Vector3(0.15f, 0.075f, 0.1f), teal, 0.01f);
            Box(visual, "Shoulder Pad", new Vector3(0f, 0.305f, -0.315f), new Vector3(0.2f, 0.15f, 0.03f), rubber, 0.01f);
            foreach (float side in new[] { -1f, 1f })
                for (int i = 0; i < 4; i++)
                    Box(visual, "Vent", new Vector3(side * 0.106f, 0.305f, -0.28f + i * 0.028f), new Vector3(0.006f, 0.1f, 0.012f), steel, 0.002f);

            // Charge lights on top of the power pack, back to front.
            var indicators = new Renderer[3];
            Box(visual, "Indicator Housing", new Vector3(0f, 0.414f, -0.235f), new Vector3(0.07f, 0.012f, 0.09f), dark, 0.003f);
            for (int i = 0; i < 3; i++)
                indicators[i] = Box(visual, "Charge Light " + (i + 1), new Vector3(0f, 0.422f, -0.265f + i * 0.03f), new Vector3(0.05f, 0.008f, 0.018f), energy, 0.002f);

            // Grip and trigger.
            Box(visual, "Grip", new Vector3(0f, 0.12f, -0.115f), new Vector3(0.085f, 0.2f, 0.1f), rubber, 0.018f, Quaternion.Euler(14f, 0f, 0f));
            for (int i = 0; i < 4; i++) // on the grip's tilted front face
                Box(visual, "Grip Rib", new Vector3(0f, 0.06f + i * 0.035f, -0.063f + (i * 0.035f - 0.06f) * 0.25f),
                    new Vector3(0.09f, 0.012f, 0.014f), graphite, 0.003f, Quaternion.Euler(14f, 0f, 0f));
            Box(visual, "Grip Cap", new Vector3(0f, 0.02f, -0.14f), new Vector3(0.095f, 0.02f, 0.115f), graphite, 0.006f, Quaternion.Euler(14f, 0f, 0f));
            Box(visual, "Trigger Guard", new Vector3(0f, 0.15f, -0.005f), new Vector3(0.06f, 0.018f, 0.12f), steel, 0.005f);
            Box(visual, "Trigger Guard Front", new Vector3(0f, 0.185f, 0.05f), new Vector3(0.06f, 0.07f, 0.018f), steel, 0.005f);
            Box(visual, "Trigger", new Vector3(0f, 0.19f, -0.02f), new Vector3(0.022f, 0.06f, 0.02f), teal, 0.004f, Quaternion.Euler(-10f, 0f, 0f));

            // Emitter: a stepped steel neck, a flared graphite shroud with cooling fins, and the lens.
            float e = front + 0.045f;
            Lathe(visual, "Emitter Neck", new Vector3(0f, Axis, e), new[]
            {
                new Vector2(0f, 0f), new Vector2(0.075f, 0f), new Vector2(0.075f, 0.03f), new Vector2(0.06f, 0.04f),
                new Vector2(0.06f, 0.1f), new Vector2(0.068f, 0.11f), new Vector2(0.068f, 0.12f),
            }, steel, 24);
            Lathe(visual, "Emitter Shroud", new Vector3(0f, Axis, e + 0.12f), new[]
            {
                new Vector2(0.05f, 0f), new Vector2(0.088f, 0f), new Vector2(0.1f, 0.03f), new Vector2(0.1f, 0.1f),
                new Vector2(0.092f, 0.115f), new Vector2(0.07f, 0.115f), new Vector2(0.062f, 0.1f), new Vector2(0.05f, 0.1f),
                new Vector2(0.05f, 0f),
            }, graphite, 28);
            // The lens sits in the shroud's bore, its dome facing forward.
            MeshRenderer lens = Lathe(visual, "Lens", new Vector3(0f, Axis, e + 0.22f), new[]
            {
                new Vector2(0f, 0.018f), new Vector2(0.03f, 0.015f), new Vector2(0.048f, 0.006f), new Vector2(0.05f, 0f), new Vector2(0f, 0f),
            }, energy, 24);
            Ring(visual, "Lens Ring", new Vector3(0f, Axis, e + 0.222f), 0.054f, 0.005f, steel);
            for (int i = 0; i < 6; i++)
            {
                Quaternion around = Quaternion.Euler(0f, 0f, i * 60f + 30f);
                Box(visual, "Cooling Fin", new Vector3(0f, Axis, e + 0.07f) + around * new Vector3(0f, 0.075f, 0f),
                    new Vector3(0.008f, 0.04f, 0.08f), teal, 0.003f, around);
            }
            for (int i = 0; i < 3; i++)
                Ring(visual, "Neck Band", new Vector3(0f, Axis, e + 0.05f + i * 0.022f), 0.061f, 0.004f, graphite);

            // A cable from the power pack along the keel to the emitter.
            Cylinder(visual, "Feed Cable", new Vector3(0.07f, 0.212f, mid - 0.02f), Quaternion.identity, 0.011f, length + 0.17f, rubber, 10);

            // No point light inside the chamber: a few centimetres from the coils and walls its inverse-square
            // falloff blew the whole held gun out to white. The emissive parts carry the glow on their own.
            var chargeVisual = root.AddComponent<LaserChargeVisual>();
            chargeVisual.Setup(core, coils, lens, indicators);
        }

        // ---------- Parts ----------

        private static MeshRenderer Part(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(position, rotation);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = material == glass ? ShadowCastingMode.Off : ShadowCastingMode.On;
            return renderer;
        }

        private static MeshRenderer Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, float bevel, Quaternion? rotation = null)
        {
            string key = $"Box {size.x:0.###}x{size.y:0.###}x{size.z:0.###} b{bevel:0.###}";
            if (!Meshes.TryGetValue(key, out Mesh mesh)) Meshes[key] = mesh = BevelBox(key, size, bevel);
            return Part(parent, name, mesh, material, position, rotation ?? Quaternion.identity);
        }

        private static MeshRenderer Cylinder(Transform parent, string name, Vector3 position, Quaternion rotation, float radius, float length, Material material, int sides)
        {
            float h = length * 0.5f;
            return Lathe(parent, name, position, new[] { new Vector2(0f, -h), new Vector2(radius, -h), new Vector2(radius, h), new Vector2(0f, h) },
                material, sides, rotation);
        }

        private static MeshRenderer Lathe(Transform parent, string name, Vector3 position, Vector2[] profile, Material material, int sides, Quaternion? rotation = null)
        {
            string key = "Lathe " + sides;
            foreach (Vector2 p in profile) key += $" {p.x:0.####},{p.y:0.####}";
            if (!Meshes.TryGetValue(key, out Mesh mesh)) Meshes[key] = mesh = LatheMesh(name, profile, sides);
            return Part(parent, name, mesh, material, position, rotation ?? Quaternion.identity);
        }

        private static MeshRenderer Ring(Transform parent, string name, Vector3 position, float radius, float thickness, Material material)
        {
            string key = $"Ring {radius:0.####} {thickness:0.####}";
            if (!Meshes.TryGetValue(key, out Mesh mesh)) Meshes[key] = mesh = TorusMesh(name, radius, thickness, 28, 8);
            return Part(parent, name, mesh, material, position, Quaternion.identity);
        }

        // ---------- Meshes ----------

        /// <summary>A box with chamfered edges and corners, flat shaded. Centred on the origin.</summary>
        private static Mesh BevelBox(string name, Vector3 size, float bevel)
        {
            Vector3 h = size * 0.5f;
            float b = Mathf.Min(bevel, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.45f);
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            // The vertex of corner s that belongs to the face across axis a: full out along a, inset by the bevel on the others.
            Vector3 V(Vector3Int s, int a)
            {
                var v = new Vector3(s.x * (h.x - b), s.y * (h.y - b), s.z * (h.z - b));
                v[a] = s[a] * h[a];
                return v;
            }

            void Polygon(params Vector3[] points)
            {
                Vector3 centre = Vector3.zero;
                foreach (Vector3 p in points) centre += p;
                centre /= points.Length;
                // Not Vector3.normalized: it returns zero below a length of 1e-5, which the millimetre chamfers on
                // the small parts fall under. A zero normal turns into NaN in the lit shader, and bloom spreads the
                // NaN pixels into a white blowout over the held gun.
                Vector3 cross = Vector3.Cross(points[1] - points[0], points[2] - points[0]);
                Vector3 normal = cross / Mathf.Max(cross.magnitude, 1e-12f);
                if (Vector3.Dot(normal, centre) < 0f)
                {
                    System.Array.Reverse(points);
                    normal = -normal;
                }
                int start = vertices.Count;
                foreach (Vector3 p in points)
                {
                    vertices.Add(p);
                    normals.Add(normal);
                }
                for (int i = 1; i < points.Length - 1; i++)
                {
                    // Cross(p1 - p0, p2 - p0) now points outward, which is Unity's front face.
                    triangles.Add(start);
                    triangles.Add(start + i);
                    triangles.Add(start + i + 1);
                }
            }

            Vector3Int S(int x, int y, int z) => new Vector3Int(x, y, z);
            for (int a = 0; a < 3; a++)
                foreach (int sign in new[] { -1, 1 })
                {
                    // Main face: the four corners on this side, in order round the face.
                    int u = (a + 1) % 3, w = (a + 2) % 3;
                    var quad = new Vector3[4];
                    int[,] order = { { -1, -1 }, { 1, -1 }, { 1, 1 }, { -1, 1 } };
                    for (int k = 0; k < 4; k++)
                    {
                        var s = new Vector3Int();
                        s[a] = sign; s[u] = order[k, 0]; s[w] = order[k, 1];
                        quad[k] = V(s, a);
                    }
                    Polygon(quad);
                }
            if (b > 0.0001f)
            {
                // Edge chamfers: between the faces across axes a and c, running along axis e.
                for (int e = 0; e < 3; e++)
                {
                    int a = (e + 1) % 3, c = (e + 2) % 3;
                    foreach (int sa in new[] { -1, 1 })
                        foreach (int sc in new[] { -1, 1 })
                        {
                            var s1 = new Vector3Int(); s1[a] = sa; s1[c] = sc; s1[e] = -1;
                            var s2 = s1; s2[e] = 1;
                            Polygon(V(s1, a), V(s2, a), V(s2, c), V(s1, c));
                        }
                }
                // Corner triangles.
                foreach (int x in new[] { -1, 1 })
                    foreach (int y in new[] { -1, 1 })
                        foreach (int z in new[] { -1, 1 })
                        {
                            Vector3Int s = S(x, y, z);
                            Polygon(V(s, 0), V(s, 1), V(s, 2));
                        }
            }
            return Finish(name, vertices, normals, triangles);
        }

        /// <summary>
        /// Spins a profile (x = radius, y = distance along the axis) round the local Z axis. Smooth around, creased at
        /// each profile corner.
        /// </summary>
        private static Mesh LatheMesh(string name, Vector2[] profile, int sides)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            // A profile drawn anticlockwise in (radius, along) space has its outward side on the right of each segment.
            float area = 0f;
            for (int i = 0; i < profile.Length; i++)
            {
                Vector2 p = profile[i], q = profile[(i + 1) % profile.Length];
                area += p.x * q.y - q.x * p.y;
            }
            float outward = area >= 0f ? 1f : -1f;
            for (int i = 0; i < profile.Length - 1; i++)
            {
                Vector2 p0 = profile[i], p1 = profile[i + 1];
                Vector2 d = p1 - p0;
                if (d.sqrMagnitude < 1e-10f) continue;
                var n = new Vector2(d.y, -d.x).normalized * outward;
                int start = vertices.Count;
                for (int j = 0; j <= sides; j++)
                {
                    float angle = j * Mathf.PI * 2f / sides;
                    float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                    vertices.Add(new Vector3(cos * p0.x, sin * p0.x, p0.y));
                    vertices.Add(new Vector3(cos * p1.x, sin * p1.x, p1.y));
                    Vector3 normal = new Vector3(cos * n.x, sin * n.x, n.y);
                    normals.Add(normal);
                    normals.Add(normal);
                }
                for (int j = 0; j < sides; j++)
                {
                    int a = start + j * 2, b = a + 1, c = a + 2, e = a + 3;
                    // Wind so each face points along its normal (Unity: Cross(v1 - v0, v2 - v0) is the front).
                    // A segment that starts or ends on the axis has one collapsed triangle; test the other one.
                    Vector3 faceNormal = Vector3.Cross(vertices[c] - vertices[a], vertices[b] - vertices[a]);
                    if (faceNormal.sqrMagnitude < 1e-14f) faceNormal = Vector3.Cross(vertices[c] - vertices[b], vertices[e] - vertices[b]);
                    if (Vector3.Dot(faceNormal, normals[a] + normals[c]) >= 0f) triangles.AddRange(new[] { a, c, b, b, c, e });
                    else triangles.AddRange(new[] { a, b, c, b, e, c });
                }
            }
            return Finish(name, vertices, normals, triangles);
        }

        /// <summary>A ring lying across the local Z axis (round the barrel).</summary>
        private static Mesh TorusMesh(string name, float radius, float thickness, int segments, int sides)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float u = i * Mathf.PI * 2f / segments;
                var dir = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0f);
                for (int j = 0; j <= sides; j++)
                {
                    float v = j * Mathf.PI * 2f / sides;
                    Vector3 normal = dir * Mathf.Cos(v) + Vector3.forward * Mathf.Sin(v);
                    vertices.Add(dir * radius + normal * thickness);
                    normals.Add(normal);
                }
            }
            int row = sides + 1;
            for (int i = 0; i < segments; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a = i * row + j, b = a + row, c = a + 1, d = b + 1;
                    Vector3 face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                    if (Vector3.Dot(face, normals[a]) > 0f) triangles.AddRange(new[] { a, b, c, c, b, d });
                    else triangles.AddRange(new[] { a, c, b, c, d, b });
                }
            return Finish(name, vertices, normals, triangles);
        }

        private static Mesh Finish(string name, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            foreach (Vector3 normal in normals)
                if (!(Mathf.Abs(normal.sqrMagnitude - 1f) < 0.01f))
                    throw new System.InvalidOperationException($"Mesh '{name}' has a bad normal {normal}; it would render as NaN.");
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void SaveMeshes()
        {
            // Replace the whole mesh file; the prefab is saved straight after with the new meshes.
            AssetDatabase.DeleteAsset(MeshPath);
            bool first = true;
            foreach (KeyValuePair<string, Mesh> pair in Meshes)
            {
                pair.Value.name = pair.Key;
                if (first) AssetDatabase.CreateAsset(pair.Value, MeshPath);
                else AssetDatabase.AddObjectToAsset(pair.Value, MeshPath);
                first = false;
            }
            AssetDatabase.ImportAsset(MeshPath);
        }

        // ---------- Materials ----------

        private static void LoadMaterials()
        {
            graphite = Load("LaserGraphite");
            steel = Load("LaserSteel");
            orange = Load("LaserOrange");
            teal = Load("LaserTeal");
            rubber = Load("LaserRubber");
            dark = Lit("LaserChamber", new Color(0.06f, 0.08f, 0.09f), 0.4f, 0.6f);
            energy = Lit("LaserEnergy", new Color(0.3f, 0.9f, 0.95f), 0.85f, 0f, new Color(0.3f, 0.95f, 1f) * 0.6f);
            glass = Glass();
        }

        private static Material Load(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/" + name + ".mat");

        private static Material Lit(string name, Color color, float smoothness, float metallic, Color? emission = null)
        {
            string path = Folder + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; // with None, URP drops _EMISSION on import
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        // Clear, faintly blue glass, smooth enough to catch highlights; drawn from both sides.
        private static Material Glass()
        {
            Material material = Lit("LaserGlass", new Color(0.7f, 0.9f, 1f, 0.1f), 0.78f, 0f);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
