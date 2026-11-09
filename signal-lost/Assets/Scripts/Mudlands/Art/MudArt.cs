using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// A flat-shaded low-poly mesh builder that works at runtime (the editor's FlatMeshKit is editor-only). Every face
    /// gets its own vertices and face normal, so shapes read as facets like the rest of the game. One submesh per
    /// colour; <see cref="MudArt.Paint"/> supplies the matching materials. Shapes: boxes, prisms and cones, balls
    /// (icospheres), lathes and tubes, each placed with a position, rotation and (for balls) per-axis radii.
    /// With <see cref="Smooth"/> on, round shapes (balls, prism and rod sides, lathes) are built finer and shaded
    /// smooth, for characters; boxes and caps stay crisp.
    /// </summary>
    public sealed class MeshKit
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<List<int>> triangles = new List<List<int>>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<float> glows = new List<float>();
        private readonly List<int> groups = new List<int>(); // per vertex: 0 = flat, else a smoothing group
        private int group, nextGroup = 1;

        /// <summary>Round shapes added while this is on are finer and smooth-shaded (characters); off: faceted.</summary>
        public bool Smooth { get; set; }

        private void BeginShape(bool round) => group = round && Smooth ? nextGroup++ : 0;
        private void EndShape() => group = 0;

        public int VertexCount => vertices.Count;
        public int SubmeshCount => colors.Count;
        /// <summary>The colour (and glow) of a submesh, for code that makes its own materials (the level builder).</summary>
        public (Color color, float glow) SubmeshLook(int index) => (colors[index], glows[index]);

        private int Sub(Color color, float glow)
        {
            for (int i = 0; i < colors.Count; i++)
                if (colors[i] == color && Mathf.Approximately(glows[i], glow)) return i;
            colors.Add(color);
            glows.Add(glow);
            triangles.Add(new List<int>());
            return colors.Count - 1;
        }

        /// <summary>One flat triangle, wound so its normal faces away from <paramref name="outward"/>'s opposite.</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color color, float glow = 0f)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < 1e-12f) return;
            normal.Normalize();
            List<int> list = triangles[Sub(color, glow)];
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            normals.Add(normal); normals.Add(normal); normals.Add(normal);
            groups.Add(group); groups.Add(group); groups.Add(group);
            list.Add(start); list.Add(start + 1); list.Add(start + 2);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, float glow = 0f)
        {
            Tri(a, b, c, color, glow);
            Tri(a, c, d, color, glow);
        }

        public void Box(Vector3 centre, Vector3 size, Quaternion rotation, Color color, float glow = 0f)
        {
            EndShape();
            Vector3 h = size * 0.5f;
            Vector3 P(float x, float y, float z) => centre + rotation * new Vector3(x * h.x, y * h.y, z * h.z);
            Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), color, glow);      // front +Z
            Quad(P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), color, glow);  // back
            Quad(P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), color, glow);      // right
            Quad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), color, glow);  // left
            Quad(P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), P(-1, 1, -1), color, glow);      // top
            Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), color, glow);  // bottom
        }

        public void Box(Vector3 centre, Vector3 size, Color color, float glow = 0f) => Box(centre, size, Quaternion.identity, color, glow);

        /// <summary>A prism or frustum along local +Y from <paramref name="baseCentre"/>, capped at both ends.</summary>
        public void Prism(Vector3 baseCentre, Quaternion rotation, float bottomRadius, float topRadius, float height, int sides, Color color,
            float glow = 0f, float twist = 0f)
        {
            sides = Mathf.Max(3, sides);
            if (Smooth && sides >= 6) sides = Mathf.Max(sides, 16);
            var bottom = new Vector3[sides];
            var top = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / sides + twist;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                bottom[i] = baseCentre + rotation * (dir * bottomRadius);
                top[i] = baseCentre + rotation * (dir * topRadius + Vector3.up * height);
            }
            Vector3 b0 = baseCentre, t0 = baseCentre + rotation * (Vector3.up * height);
            bool round = sides >= 6;
            BeginShape(round);
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                if (topRadius > 0.0001f) Quad(bottom[i], top[i], top[j], bottom[j], color, glow);
                else Tri(bottom[i], t0, bottom[j], color, glow);
            }
            EndShape(); // the caps stay flat
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                if (bottomRadius > 0.0001f) Tri(b0, bottom[i], bottom[j], color, glow);
                if (topRadius > 0.0001f) Tri(t0, top[j], top[i], color, glow);
            }
        }

        public void Cone(Vector3 baseCentre, Quaternion rotation, float radius, float height, int sides, Color color, float glow = 0f) =>
            Prism(baseCentre, rotation, radius, 0f, height, sides, color, glow);

        /// <summary>A cylinder between two points.</summary>
        public void Rod(Vector3 from, Vector3 to, float radius, int sides, Color color, float glow = 0f, float endRadius = -1f)
        {
            Vector3 along = to - from;
            if (along.sqrMagnitude < 1e-8f) return;
            Prism(from, Quaternion.FromToRotation(Vector3.up, along), radius, endRadius < 0f ? radius : endRadius, along.magnitude, sides, color, glow);
        }

        /// <summary>A faceted ball (icosphere, detail 0 = 20 faces, 1 = 80, 2 = 320, 3 = 1280) with per-axis radii.</summary>
        public void Ball(Vector3 centre, Vector3 radii, Quaternion rotation, Color color, int detail = 1, float glow = 0f)
        {
            if (Smooth) detail = Mathf.Max(detail + 1, 2);
            (Vector3[] points, int[] tris) = Icosphere(Mathf.Clamp(detail, 0, 3));
            BeginShape(true);
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = points[tris[i]], b = points[tris[i + 1]], c = points[tris[i + 2]];
                Tri(centre + rotation * Vector3.Scale(a, radii), centre + rotation * Vector3.Scale(b, radii),
                    centre + rotation * Vector3.Scale(c, radii), color, glow);
            }
            EndShape();
        }

        public void Ball(Vector3 centre, float radius, Color color, int detail = 1, float glow = 0f) =>
            Ball(centre, Vector3.one * radius, Quaternion.identity, color, detail, glow);

        /// <summary>A surface of revolution about local +Y; profile points are (radius, height), bottom to top.</summary>
        public void Lathe(Vector3 centre, Quaternion rotation, Vector2[] profile, int sides, Color color, float glow = 0f)
        {
            if (Smooth) sides = Mathf.Max(sides, 20);
            BeginShape(true);
            for (int k = 0; k < profile.Length - 1; k++)
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                    Vector3 P(Vector2 p, float a) => centre + rotation * new Vector3(Mathf.Cos(a) * p.x, p.y, Mathf.Sin(a) * p.x);
                    Vector3 p00 = P(profile[k], a0), p01 = P(profile[k], a1), p10 = P(profile[k + 1], a0), p11 = P(profile[k + 1], a1);
                    // A ring this small is the axis: close it with one triangle. A quad there is a sliver the GPU can't
                    // interpolate across; it writes NaN pixels that the bloom smears into a blinding white flash as the
                    // camera moves (the glowing mushrooms' caps).
                    if (profile[k].x < 0.002f) Tri(p00, p10, p11, color, glow);
                    else if (profile[k + 1].x < 0.002f) Tri(p00, p10, p01, color, glow);
                    else Quad(p00, p10, p11, p01, color, glow);
                }
            EndShape();
        }

        /// <summary>A tube following a path (open ends capped with fans).</summary>
        public void Tube(Vector3[] path, float radius, int sides, Color color, float glow = 0f)
        {
            for (int i = 0; i < path.Length - 1; i++) Rod(path[i], path[i + 1], radius, sides, color, glow);
            for (int i = 1; i < path.Length - 1; i++) Ball(path[i], radius, color, 0, glow);
        }

        /// <summary>A flat plank (box) between two points, of the given width and thickness.</summary>
        public void Plank(Vector3 from, Vector3 to, float width, float thickness, Color color, Vector3 up = default)
        {
            Vector3 along = to - from;
            if (along.sqrMagnitude < 1e-8f) return;
            Quaternion r = Quaternion.LookRotation(along, up == default ? Vector3.up : up);
            Box((from + to) * 0.5f, new Vector3(width, thickness, along.magnitude), r, color);
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(SmoothedNormals());
            mesh.subMeshCount = triangles.Count;
            for (int i = 0; i < triangles.Count; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Smooth groups: every vertex of a group at the same spot takes the average of its faces' normals.
        private List<Vector3> SmoothedNormals()
        {
            if (nextGroup == 1) return normals;
            var sums = new Dictionary<(int, int, int, int), Vector3>();
            (int, int, int, int) Key(int i)
            {
                Vector3 p = vertices[i] * 2000f;
                return (groups[i], Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
            }
            for (int i = 0; i < vertices.Count; i++)
            {
                if (groups[i] == 0) continue;
                var key = Key(i);
                sums[key] = sums.TryGetValue(key, out Vector3 sum) ? sum + normals[i] : normals[i];
            }
            var result = new List<Vector3>(normals);
            for (int i = 0; i < vertices.Count; i++)
                if (groups[i] != 0) result[i] = sums[Key(i)].normalized;
            return result;
        }

        public Material[] Materials()
        {
            var result = new Material[colors.Count];
            for (int i = 0; i < colors.Count; i++) result[i] = glows[i] > 0f ? MudArt.Glow(colors[i], glows[i]) : MudArt.Paint(colors[i]);
            return result;
        }

        // ---------- Icospheres ----------

        private static readonly (Vector3[], int[])[] Spheres = new (Vector3[], int[])[4];

        private static (Vector3[], int[]) Icosphere(int detail)
        {
            if (Spheres[detail].Item1 != null) return Spheres[detail];
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var points = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (int i = 0; i < points.Count; i++) points[i] = points[i].normalized;
            var tris = new List<int> { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            for (int d = 0; d < detail; d++)
            {
                var cache = new Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
                    if (cache.TryGetValue(key, out int found)) return found;
                    points.Add(((points[a] + points[b]) * 0.5f).normalized);
                    cache[key] = points.Count - 1;
                    return points.Count - 1;
                }
                var next = new List<int>();
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = next;
            }
            // This winding gives cross(b - a, c - a) pointing outward, which is Unity's front face (MeshKit.Tri).
            Spheres[detail] = (points.ToArray(), tris.ToArray());
            return Spheres[detail];
        }
    }

    /// <summary>
    /// Materials and model parts for the Mudlands' code-built art. Materials are cached by colour (URP Lit, low
    /// smoothness, the same look as the generated meadow and desert); meshes are cached by key so twenty Mudpuppies
    /// share one set. A part is a child GameObject with a mesh, placed under a parent, ready to animate.
    /// </summary>
    public static class MudArt
    {
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static readonly Dictionary<string, (Mesh mesh, Material[] materials)> Meshes = new Dictionary<string, (Mesh, Material[])>();
        private static Shader lit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Materials.Clear();
            Meshes.Clear();
        }

        private static Material NewLit(string template = "SignalLost/MudlandsLit")
        {
            // Lit materials in Resources (made by the Mudlands builder) keep the shader and its variants (plain, glowing,
            // see-through) in builds; Shader.Find is the fallback.
            var source = Resources.Load<Material>(template);
            if (source != null) return new Material(source);
            var template0 = Resources.Load<Material>("SignalLost/MudlandsLit");
            if (template0 != null) return new Material(template0);
            if (lit == null) lit = Shader.Find("Universal Render Pipeline/Lit");
            return new Material(lit != null ? lit : Shader.Find("Standard"));
        }

        /// <summary>A flat colour material (shared).</summary>
        public static Material Paint(Color color, float smoothness = 0.12f)
        {
            string key = "p" + ColorUtility.ToHtmlStringRGBA(color) + smoothness.ToString("0.00");
            if (Materials.TryGetValue(key, out Material found) && found != null) return found;
            Material material = NewLit();
            material.name = "Mud " + ColorUtility.ToHtmlStringRGB(color);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.hideFlags = HideFlags.DontSave;
            Materials[key] = material;
            return material;
        }

        /// <summary>A glowing material (lanterns, headlights, magic).</summary>
        public static Material Glow(Color color, float intensity = 2f)
        {
            string key = "g" + ColorUtility.ToHtmlStringRGBA(color) + intensity.ToString("0.0");
            if (Materials.TryGetValue(key, out Material found) && found != null) return found;
            Material material = NewLit("SignalLost/MudlandsGlow");
            material.name = "Mud Glow " + ColorUtility.ToHtmlStringRGB(color);
            material.SetColor("_BaseColor", color);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * intensity);
            // Matte, with no specular highlight. The template is URP Lit's default smoothness 0.5, whose sharp moon
            // highlight is many times brighter than the screen and goes through the project's unclamped bloom as a
            // blinding white flash that blinks on and off as the camera moves (the glowing mushrooms).
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.hideFlags = HideFlags.DontSave;
            Materials[key] = material;
            return material;
        }

        /// <summary>A see-through material (magic clones, spore clouds, water).</summary>
        public static Material Clear(Color color, float glow = 0f)
        {
            string key = "c" + ColorUtility.ToHtmlStringRGBA(color) + glow.ToString("0.0");
            if (Materials.TryGetValue(key, out Material found) && found != null) return found;
            Material material = NewLit("SignalLost/MudlandsClear");
            material.name = "Mud Clear " + ColorUtility.ToHtmlStringRGB(color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.15f); // the template's 0.5 throws blinding moon glints through the bloom
            if (glow > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(color.r, color.g, color.b) * glow);
            }
            material.hideFlags = HideFlags.DontSave;
            Materials[key] = material;
            return material;
        }

        /// <summary>The mesh built by <paramref name="build"/>, built once per session under <paramref name="key"/>.</summary>
        public static (Mesh mesh, Material[] materials) Cached(string key, Action<MeshKit> build)
        {
            if (Meshes.TryGetValue(key, out var found) && found.mesh != null) return found;
            var kit = new MeshKit();
            build(kit);
            found = (kit.Build(key), kit.Materials());
            found.mesh.hideFlags = HideFlags.DontSave;
            Meshes[key] = found;
            return found;
        }

        /// <summary>A child part with a cached mesh. Returns its transform.</summary>
        public static Transform Part(Transform parent, string name, Vector3 position, Quaternion rotation, string meshKey, Action<MeshKit> build,
            bool castShadows = true)
        {
            var part = new GameObject(name).transform;
            part.SetParent(parent, false);
            part.localPosition = position;
            part.localRotation = rotation;
            (Mesh mesh, Material[] materials) = Cached(meshKey, build);
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return part;
        }

        public static Transform Part(Transform parent, string name, Vector3 position, string meshKey, Action<MeshKit> build) =>
            Part(parent, name, position, Quaternion.identity, meshKey, build);

        /// <summary>An empty joint to animate other parts under.</summary>
        public static Transform Pivot(Transform parent, string name, Vector3 position, Quaternion rotation = default)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = position;
            pivot.localRotation = rotation == default ? Quaternion.identity : rotation;
            return pivot;
        }

        /// <summary>Recolours every renderer under <paramref name="root"/> with one see-through material (clones, ghosts).</summary>
        public static void Ghost(Transform root, Material material)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = material;
                renderer.sharedMaterials = mats;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        // ---------- Shared bits ----------

        /// <summary>A pair of cartoon eyes (white ball, dark pupil) on a head, facing +Z. Returns the two eye pivots.</summary>
        public static (Transform left, Transform right) Eyes(Transform head, Vector3 centre, float spacing, float size, Color white, Color pupil,
            string key)
        {
            Transform Eye(float side)
            {
                Transform pivot = Pivot(head, side < 0 ? "Eye L" : "Eye R", centre + new Vector3(spacing * side, 0f, 0f));
                Part(pivot, "White", Vector3.zero, Quaternion.identity, key + "_eyewhite", k => k.Ball(Vector3.zero, size, white, 1));
                Part(pivot, "Pupil", new Vector3(0f, 0f, size * 0.72f), Quaternion.identity, key + "_pupil",
                    k => k.Ball(Vector3.zero, new Vector3(size * 0.5f, size * 0.55f, size * 0.35f), Quaternion.identity, pupil, 1));
                return pivot;
            }
            return (Eye(-1f), Eye(1f));
        }
    }

    /// <summary>Colours for the Mudlands, picked to sit between the meadow's warmth and the references' teal dusk.</summary>
    public static class MudPalette
    {
        public static readonly Color Mud = new Color(0.33f, 0.24f, 0.16f);
        public static readonly Color WetMud = new Color(0.22f, 0.16f, 0.11f);
        public static readonly Color Road = new Color(0.47f, 0.35f, 0.22f);
        public static readonly Color Grass = new Color(0.29f, 0.42f, 0.22f);
        public static readonly Color DarkGrass = new Color(0.18f, 0.31f, 0.2f);
        public static readonly Color Moss = new Color(0.32f, 0.48f, 0.26f);
        public static readonly Color Wood = new Color(0.45f, 0.33f, 0.22f);
        public static readonly Color DarkWood = new Color(0.27f, 0.2f, 0.15f);
        public static readonly Color GreyWood = new Color(0.42f, 0.42f, 0.4f);
        public static readonly Color Bark = new Color(0.25f, 0.22f, 0.2f);
        public static readonly Color Stone = new Color(0.45f, 0.48f, 0.47f);
        public static readonly Color Rust = new Color(0.55f, 0.27f, 0.13f);
        public static readonly Color Metal = new Color(0.55f, 0.57f, 0.6f);
        public static readonly Color DarkMetal = new Color(0.2f, 0.21f, 0.23f);
        public static readonly Color Rubber = new Color(0.11f, 0.11f, 0.12f);
        public static readonly Color White = new Color(0.95f, 0.94f, 0.9f);
        public static readonly Color Black = new Color(0.06f, 0.06f, 0.07f);
        public static readonly Color Cream = new Color(0.93f, 0.86f, 0.68f);
        public static readonly Color Fire = new Color(1f, 0.62f, 0.2f);
        public static readonly Color Lantern = new Color(1f, 0.78f, 0.38f);
        public static readonly Color Curse = new Color(0.45f, 1f, 0.35f);
        public static readonly Color Magic = new Color(0.62f, 0.35f, 1f);
        public static readonly Color Lily = new Color(0.78f, 0.45f, 0.9f);
        public static readonly Color Scotch = new Color(0.78f, 0.45f, 0.12f);
        public static readonly Color Water = new Color(0.12f, 0.3f, 0.32f);
    }
}
