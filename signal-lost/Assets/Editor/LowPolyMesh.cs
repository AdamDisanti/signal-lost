using System.Collections.Generic;
using UnityEngine;

namespace SignalLost.Editor
{
    /// <summary>
    /// Builds simple faceted (flat-shaded) low-poly meshes for placeholder models: lathed shapes such as a
    /// bucket, icospheres, tapered prisms for limbs, and tubes along a path. Every triangle gets its own
    /// vertices so lighting shows each facet, which is the low-poly look.
    /// </summary>
    internal static class LowPolyMesh
    {
        private sealed class Builder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int> triangles = new List<int>();

            /// <summary>Adds a triangle wound so its front faces <paramref name="outward"/>.</summary>
            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-12f) return; // degenerate (for example where a lathe meets its axis)
                if (Vector3.Dot(normal, outward) < 0f) (b, c) = (c, b);
                int start = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
            {
                Triangle(a, b, c, outward);
                Triangle(a, c, d, outward);
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        /// <summary>
        /// Spins a profile of (radius, height) points around the Y axis. Trace the profile so the solid is on
        /// the left as you walk along it (for example: out along the base, up the outside, in across the rim,
        /// down the inside); faces then point the right way.
        /// </summary>
        public static Mesh Lathe(string name, Vector2[] profile, int sides)
        {
            var builder = new Builder();
            for (int i = 0; i < profile.Length - 1; i++)
            {
                Vector2 p0 = profile[i], p1 = profile[i + 1];
                Vector2 edge = p1 - p0;
                var outward2D = new Vector2(edge.y, -edge.x); // right-hand side of the walk: outside the solid
                for (int j = 0; j < sides; j++)
                {
                    float a0 = j * Mathf.PI * 2f / sides, a1 = (j + 1) * Mathf.PI * 2f / sides, mid = (a0 + a1) * 0.5f;
                    var outward = new Vector3(outward2D.x * Mathf.Cos(mid), outward2D.y, outward2D.x * Mathf.Sin(mid));
                    builder.Quad(Around(p0, a0), Around(p0, a1), Around(p1, a1), Around(p1, a0), outward);
                }
            }
            return builder.Build(name);
        }

        private static Vector3 Around(Vector2 point, float angle) =>
            new Vector3(point.x * Mathf.Cos(angle), point.y, point.x * Mathf.Sin(angle));

        /// <summary>Unit-radius icosphere. 0 subdivisions gives 20 faces; 1 gives 80.</summary>
        public static Mesh Icosphere(string name, int subdivisions)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var points = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < points.Count; i++) points[i] = points[i].normalized;
            var faces = new List<int[]>
            {
                new[] {0, 11, 5}, new[] {0, 5, 1}, new[] {0, 1, 7}, new[] {0, 7, 10}, new[] {0, 10, 11},
                new[] {1, 5, 9}, new[] {5, 11, 4}, new[] {11, 10, 2}, new[] {10, 7, 6}, new[] {7, 1, 8},
                new[] {3, 9, 4}, new[] {3, 4, 2}, new[] {3, 2, 6}, new[] {3, 6, 8}, new[] {3, 8, 9},
                new[] {4, 9, 5}, new[] {2, 4, 11}, new[] {6, 2, 10}, new[] {8, 6, 7}, new[] {9, 8, 1},
            };

            for (int s = 0; s < subdivisions; s++)
            {
                var midpoints = new Dictionary<long, int>();
                var next = new List<int[]>();
                foreach (int[] f in faces)
                {
                    int a = Midpoint(points, midpoints, f[0], f[1]);
                    int b = Midpoint(points, midpoints, f[1], f[2]);
                    int c = Midpoint(points, midpoints, f[2], f[0]);
                    next.Add(new[] {f[0], a, c});
                    next.Add(new[] {f[1], b, a});
                    next.Add(new[] {f[2], c, b});
                    next.Add(new[] {a, b, c});
                }
                faces = next;
            }

            var builder = new Builder();
            foreach (int[] f in faces)
            {
                Vector3 a = points[f[0]], b = points[f[1]], c = points[f[2]];
                builder.Triangle(a, b, c, (a + b + c) / 3f);
            }
            return builder.Build(name);
        }

        private static int Midpoint(List<Vector3> points, Dictionary<long, int> cache, int i, int j)
        {
            long key = ((long)Mathf.Min(i, j) << 32) + Mathf.Max(i, j);
            if (cache.TryGetValue(key, out int index)) return index;
            points.Add(((points[i] + points[j]) * 0.5f).normalized);
            cache[key] = points.Count - 1;
            return points.Count - 1;
        }

        /// <summary>
        /// A prism along +Z from 0 to 1, radius 1 at the start and <paramref name="endRadius"/> at the end
        /// (0 makes a cone). Scale the object to set thickness and length.
        /// </summary>
        public static Mesh TaperedPrism(string name, int sides, float endRadius)
        {
            var builder = new Builder();
            for (int j = 0; j < sides; j++)
            {
                float a0 = j * Mathf.PI * 2f / sides, a1 = (j + 1) * Mathf.PI * 2f / sides, mid = (a0 + a1) * 0.5f;
                Vector3 r0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f), r1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);
                var outward = new Vector3(Mathf.Cos(mid), Mathf.Sin(mid), 0.2f);
                Vector3 end = Vector3.forward;
                builder.Quad(r0, r1, end + r1 * endRadius, end + r0 * endRadius, outward);
                builder.Triangle(Vector3.zero, r1, r0, Vector3.back);                                     // start cap
                if (endRadius > 0f) builder.Triangle(end, end + r0 * endRadius, end + r1 * endRadius, Vector3.forward); // end cap
            }
            return builder.Build(name);
        }

        /// <summary>A tube of the given radius following a path of points (no end caps).</summary>
        public static Mesh Tube(string name, Vector3[] path, float radius, int sides)
        {
            var builder = new Builder();
            var rings = new Vector3[path.Length][];
            for (int i = 0; i < path.Length; i++)
            {
                Vector3 forward = (path[Mathf.Min(i + 1, path.Length - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                Vector3 side = Vector3.Cross(forward, Vector3.up);
                if (side.sqrMagnitude < 0.001f) side = Vector3.Cross(forward, Vector3.right);
                side.Normalize();
                Vector3 up = Vector3.Cross(side, forward);
                rings[i] = new Vector3[sides];
                for (int j = 0; j < sides; j++)
                {
                    float angle = j * Mathf.PI * 2f / sides;
                    rings[i][j] = path[i] + (side * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
                }
            }
            for (int i = 0; i < path.Length - 1; i++)
            {
                for (int j = 0; j < sides; j++)
                {
                    int k = (j + 1) % sides;
                    Vector3 centre = (path[i] + path[i + 1]) * 0.5f;
                    Vector3 faceCentre = (rings[i][j] + rings[i][k] + rings[i + 1][j] + rings[i + 1][k]) * 0.25f;
                    builder.Quad(rings[i][j], rings[i][k], rings[i + 1][k], rings[i + 1][j], faceCentre - centre);
                }
            }
            return builder.Build(name);
        }
    }
}
