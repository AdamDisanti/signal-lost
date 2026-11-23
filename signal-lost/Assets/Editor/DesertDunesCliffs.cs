using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Random = System.Random;

namespace SignalLost.Editor
{
    /// <summary>
    /// The desert's cliff faces (11-22, Lawson: "make the terrain look more natural as currently its pretty blocky"): one
    /// continuous eroded rock face along each edge instead of separate wall slabs. The face is built in sandstone strata, each
    /// stepping in or out a little so the cliff reads as ledged layers; it leans back as it rises, bulges and hollows along
    /// its length, is buried at its foot, and folds back over the land above at its top, so it hides the terrain's steep 4 m
    /// triangles behind it. Used for both sides of Rattlesnake Canyon and the outer cliffs of the plateau, Table Mesa and
    /// the lookout rock (with gaps for their ramps).
    /// </summary>
    public static partial class DesertDunesBuilder
    {
        private struct CliffColumn
        {
            public Vector2 Foot;  // where the face meets the open ground
            public Vector2 Into;  // unit, from the open side into the rock
            public float FootY, TopY;
        }

        private static readonly string[] StrataKeys = { "DS_RockMid", "DS_RockLight", "DS_RockDark", "DS_RockBand" };
        private const int Strata = 7;

        /// <summary>Builds one cliff face (a strip, or a closed loop) as its own mesh and collider.</summary>
        private static void CliffFace(Transform parent, string name, List<CliffColumn> cols, bool closed, int seed, float batter, float lip)
        {
            int n = cols.Count;
            if (n < 3) return;
            var rng = new Random(seed);
            var bounds = new float[Strata + 1];
            for (int k = 0; k <= Strata; k++) bounds[k] = k / (float)Strata + (k > 0 && k < Strata ? Range(rng, -0.04f, 0.04f) : 0f);
            var key = new string[Strata];
            int shift = rng.Next(4);
            for (int k = 0; k < Strata; k++) key[k] = StrataKeys[(k + shift) % StrataKeys.Length];

            // Open strips taper down at their ends, so they die into the slope instead of ending in a sheer edge.
            if (!closed)
                for (int i = 0; i < n; i++)
                {
                    int end = Mathf.Min(i, n - 1 - i);
                    if (end >= 4) continue;
                    CliffColumn c = cols[i];
                    c.TopY = c.FootY + (c.TopY - c.FootY) * (0.15f + 0.85f * end / 4f);
                    cols[i] = c;
                }

            var along = new float[n];
            for (int i = 1; i < n; i++) along[i] = along[i - 1] + Vector2.Distance(cols[i - 1].Foot, cols[i].Foot);

            // How far back each column may reach before it would cross its neighbours (on the inside of a bend the columns
            // converge; past that point the lip would fold over itself into fins).
            var reach = new float[n];
            for (int i = 0; i < n; i++)
            {
                reach[i] = 999f;
                foreach (int j in new[] { i - 1, i + 1 })
                {
                    int k = closed ? (j + n) % n : j;
                    if (k < 0 || k >= n) continue;
                    Vector2 di = cols[i].Into - cols[k].Into;
                    if (di.sqrMagnitude < 1e-6f) continue;
                    float meet = Vector2.Dot(cols[k].Foot - cols[i].Foot, di) / di.sqrMagnitude;
                    if (meet > 0f) reach[i] = Mathf.Min(reach[i], meet * 0.8f);
                }
            }
            for (int pass = 0; pass < 3; pass++) // a little smoothing, so the cap doesn't jump column to column
                for (int i = 0; i < n; i++)
                {
                    int a = closed ? (i + n - 1) % n : Mathf.Max(0, i - 1), b = closed ? (i + 1) % n : Mathf.Min(n - 1, i + 1);
                    reach[i] = Mathf.Min(reach[i], (reach[a] + reach[b]) * 0.5f + 1f);
                }

            int rows = 3 + Strata * 2;
            var p = new Vector3[n, rows];
            float salt = seed * 0.113f;
            for (int i = 0; i < n; i++)
            {
                CliffColumn c = cols[i];
                float h = Mathf.Max(0.5f, c.TopY - c.FootY), s = along[i];
                // Big buttresses and bays common to every stratum, then each stratum's own ledges and recesses.
                float bulge = 1.3f * Noise(s, salt, 23f, 1.7f, 0.3f) + 0.5f * Noise(s, salt, 7f, 5.3f, 0.9f);
                float Offset(int k) => batter * ((bounds[k] + bounds[k + 1]) * 0.5f) + bulge + 0.7f * Noise(s, k * 7.1f + salt, 9f, 3.1f, 0f) + (k % 2 == 1 ? 0.45f : 0f);
                float cap = reach[i];
                Vector3 At(float offset, float y) { offset = Mathf.Min(offset, cap); return new Vector3(c.Foot.x + c.Into.x * offset, y, c.Foot.y + c.Into.y * offset); }
                int r = 0;
                p[i, r++] = At(Offset(0), c.FootY - 1.6f);
                for (int k = 0; k < Strata; k++)
                {
                    float o = Offset(k);
                    p[i, r++] = At(o, c.FootY + h * bounds[k]);
                    p[i, r++] = At(o, c.FootY + h * bounds[k + 1] + (k == Strata - 1 ? 0.25f : 0f));
                }
                // The lip slopes back down into the ground behind (so it never lies flat on the surface and flickers against
                // it), then a skirt goes down under the ground, so no edge of the face floats.
                // Where the lip ends it tucks just under the real ground there, so it follows a rim that rises gradually
                // (the lookout rock, a table's edge) instead of hanging over it as a band.
                Vector3 back = At(Offset(Strata - 1) + lip, 0f);
                float landY = Mathf.Min(c.TopY - 0.6f, Ground(back.x, back.z) - 0.3f);
                p[i, r++] = At(Offset(Strata - 1) + lip, landY);
                p[i, r] = At(Offset(Strata - 1) + lip + 0.6f, landY - 3.5f);
            }

            // Flat-shaded quads, one submesh per material.
            var vertices = new List<Vector3>();
            var byKey = new Dictionary<string, List<int>>();
            string RowKey(int r)
            {
                if (r == 0) return key[0];
                if (r >= rows - 3) return "DS_RockLight";       // the lip over the top and its skirt
                int k = (r - 1) / 2;
                return (r - 1) % 2 == 0 ? key[k] : "DS_RockLight"; // a stratum's face, or the ledge on top of it
            }
            int segments = closed ? n : n - 1;
            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % n;
                Vector3 t = p[j, 1] - p[i, 1];
                Vector2 into = (cols[i].Into + cols[j].Into).normalized;
                bool flip = Vector3.Dot(Vector3.Cross(t, Vector3.up), new Vector3(-into.x, 0f, -into.y)) < 0f;
                for (int r = 0; r < rows - 1; r++)
                {
                    Vector3 a = p[i, r], b = p[j, r], c = p[j, r + 1], d = p[i, r + 1];
                    if (!byKey.TryGetValue(RowKey(r), out List<int> list)) byKey[RowKey(r)] = list = new List<int>();
                    void Tri(Vector3 x, Vector3 y, Vector3 z)
                    {
                        if (Vector3.Cross(y - x, z - x).sqrMagnitude < 1e-8f) return;
                        int start = vertices.Count;
                        vertices.Add(x);
                        if (flip) { vertices.Add(z); vertices.Add(y); } else { vertices.Add(y); vertices.Add(z); }
                        list.Add(start); list.Add(start + 1); list.Add(start + 2);
                    }
                    Tri(a, b, c);
                    Tri(a, c, d);
                }
            }

            var mesh = new Mesh { name = name.Replace(" ", ""), indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            var keys = new List<string>(byKey.Keys);
            mesh.subMeshCount = keys.Count;
            for (int k = 0; k < keys.Count; k++) mesh.SetTriangles(byKey[keys[k]], k);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh = SaveMesh(mesh);

            var materials = new Material[keys.Count];
            for (int k = 0; k < keys.Count; k++) materials[k] = SceneryMaterial(keys[k]);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        /// <summary>Splits columns into runs where <paramref name="keep"/> holds, and builds a face for each.</summary>
        private static void CliffRuns(Transform parent, string label, List<CliffColumn> cols, List<bool> keep, bool loop, int seed, float batter, float lip)
        {
            int n = cols.Count;
            if (loop && keep.TrueForAll(k => k))
            {
                CliffFace(parent, label, cols, true, seed, batter, lip);
                return;
            }
            // Start just after a gap, so a run that wraps round the loop's seam stays whole.
            int start = 0;
            if (loop) for (int i = 0; i < n; i++) if (!keep[i]) { start = (i + 1) % n; break; }
            var run = new List<CliffColumn>();
            int runs = 0;
            for (int m = 0; m <= n; m++)
            {
                int i = (start + m) % n;
                bool on = m < n && keep[i];
                if (on) { run.Add(cols[i]); continue; }
                if (run.Count >= 3) CliffFace(parent, $"{label} {++runs}", run, false, seed + runs * 31, batter, lip);
                run = new List<CliffColumn>();
                if (!loop && m >= n) break;
            }
        }

        /// <summary>Catmull-Rom points every ~<paramref name="step"/> metres along a polyline, with unit tangents.</summary>
        private static List<(Vector2 point, Vector2 tangent)> Resample(Vector2[] path, float step)
        {
            var result = new List<(Vector2, Vector2)>();
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector2 p0 = path[Mathf.Max(i - 1, 0)], p1 = path[i], p2 = path[i + 1], p3 = path[Mathf.Min(i + 2, path.Length - 1)];
                int m = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(p1, p2) / step));
                for (int j = 0; j < m; j++)
                {
                    float u = j / (float)m;
                    result.Add((CatmullRom(p0, p1, p2, p3, u), (CatmullRom(p0, p1, p2, p3, u + 0.01f) - CatmullRom(p0, p1, p2, p3, u)).normalized));
                }
            }
            Vector2 last = path[path.Length - 1];
            result.Add((last, (last - path[path.Length - 2]).normalized));
            return result;
        }

        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        /// <summary>The canyon's two walls: faces 7.5 m either side of its centre line, wherever it runs through the plateau.</summary>
        private static void CanyonCliffs(Transform parent)
        {
            var line = Resample(Canyon, 2.5f);
            for (int side = -1; side <= 1; side += 2)
            {
                var cols = new List<CliffColumn>();
                var keep = new List<bool>();
                foreach ((Vector2 point, Vector2 tangent) in line)
                {
                    Vector2 into = new Vector2(-tangent.y, tangent.x) * side;
                    Vector2 foot = point + into * CanyonWallFoot;
                    // The face reaches the ground actually standing behind it (just past the carve), and only stands where
                    // at least 5 m of rock does: it fades out at the canyon's mouths and wherever the plateau runs thin.
                    Vector2 behind = point + into * (CanyonHalf + CanyonWallWidth + 1f);
                    var column = new CliffColumn
                    {
                        Foot = foot, Into = into,
                        FootY = Ground(point.x + into.x * (CanyonWallFoot - 2.5f), point.y + into.y * (CanyonWallFoot - 2.5f)),
                        TopY = Ground(behind.x, behind.y) + 0.1f
                    };
                    cols.Add(column);
                    keep.Add(column.TopY - column.FootY > 5f);
                }
                CliffRuns(parent, side < 0 ? "Canyon Wall South" : "Canyon Wall North", cols, keep, false, side < 0 ? 811 : 823, 3f, 8f); // (an 8 m lip always reaches past the cut, even where the face bulges out)
            }
        }

        /// <summary>
        /// The cliff round a raised table: walk its outline by bearing, put the face's foot 4.5 m outside the rim and its top
        /// on the rim, and leave gaps where <paramref name="skip"/> says (its ramps, a canyon mouth).
        /// </summary>
        private static void TableCliffs(Transform parent, string label, Func<Vector2, float> inside, Vector2 centre, float top, Func<Vector2, bool> skip, int seed)
        {
            float radius = 10f;
            for (float r = 1f; r < 140f; r += 1f) if (inside(centre + Vector2.right * r) < 0f) { radius = r; break; }
            int count = Mathf.Max(24, Mathf.CeilToInt(2f * Mathf.PI * radius / 2.5f));
            var cols = new List<CliffColumn>();
            var keep = new List<bool>();
            for (int k = 0; k < count; k++)
            {
                float a = k * Mathf.PI * 2f / count;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float edge = 0f;
                for (float r = 0f; r < 160f; r += 0.25f) if (inside(centre + dir * r) < 0f) { edge = r; break; }
                Vector2 rim = centre + dir * edge;
                Vector2 foot = rim + dir * 4.5f;
                // Top on the ground just inside the rim, so it follows the table down its ramps; it only stands where that is
                // at least 5 m above the foot (so not across a ramp, nor where the beach or an arena levels the edge).
                Vector2 behind = rim - dir * 5f;
                var column = new CliffColumn { Foot = foot, Into = -dir, FootY = Ground(foot.x + dir.x, foot.y + dir.y), TopY = Ground(behind.x, behind.y) + 0.1f };
                cols.Add(column);
                keep.Add(!skip(rim) && column.TopY - column.FootY > 5f);
            }
            CliffRuns(parent, label, cols, keep, true, seed, 4.5f, 6f);
        }
    }
}
