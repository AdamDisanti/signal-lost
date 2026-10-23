using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Editor
{
    /// <summary>
    /// Builds flat-shaded (faceted, low-poly) meshes with one submesh per material, keyed by an enum of the
    /// builder's materials. Every triangle gets its own vertices so lighting shows each facet. Shared by the
    /// procedural art builders (shipwreck, meadow vegetation, tutorial terrain).
    /// </summary>
    internal sealed class FlatMeshKit<TKey> where TKey : struct, Enum
    {
        private static readonly Dictionary<Mesh, List<TKey>> SubmeshMaterials = new Dictionary<Mesh, List<TKey>>();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly Dictionary<TKey, List<int>> triangles = new Dictionary<TKey, List<int>>();

        public static TKey MaterialOf(Mesh mesh, int submesh) => SubmeshMaterials[mesh][submesh];
        public static string Order(Mesh mesh) => string.Join(",", SubmeshMaterials[mesh]);
        public static void Remember(Mesh mesh, string order)
        {
            var list = new List<TKey>();
            foreach (string part in order.Split(',')) list.Add((TKey)Enum.Parse(typeof(TKey), part));
            SubmeshMaterials[mesh] = list;
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, TKey material)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < 1e-12f) return;
            if (Vector3.Dot(normal, outward) < 0f) (b, c) = (c, b);
            if (!triangles.TryGetValue(material, out List<int> list)) triangles[material] = list = new List<int>();
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            list.Add(start);
            list.Add(start + 1);
            list.Add(start + 2);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, TKey material)
        {
            Tri(a, b, c, outward, material);
            Tri(a, c, d, outward, material);
        }

        public void Box(Vector3 centre, Vector3 size, Quaternion rotation, TKey material)
        {
            Vector3 h = size * 0.5f;
            Vector3 P(float x, float y, float z) => centre + rotation * new Vector3(x * h.x, y * h.y, z * h.z);
            Vector3 R(Vector3 v) => rotation * v;
            Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), R(Vector3.forward), material);
            Quad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), R(Vector3.back), material);
            Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), R(Vector3.up), material);
            Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), R(Vector3.down), material);
            Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), R(Vector3.right), material);
            Quad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), R(Vector3.left), material);
        }

        // Copies another (LowPolyMesh) mesh's triangles in, transformed, keeping its flat facets.
        public void Add(Mesh mesh, Matrix4x4 pose, TKey material)
        {
            Vector3[] source = mesh.vertices;
            int[] tris = mesh.triangles;
            Vector3 centre = pose.MultiplyPoint3x4(mesh.bounds.center);
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = pose.MultiplyPoint3x4(source[tris[i]]), b = pose.MultiplyPoint3x4(source[tris[i + 1]]), c = pose.MultiplyPoint3x4(source[tris[i + 2]]);
                Vector3 normal = Vector3.Cross(b - a, c - a);
                Tri(a, b, c, normal.sqrMagnitude > 0f ? normal : (a + b + c) / 3f - centre, material);
            }
            UnityEngine.Object.DestroyImmediate(mesh);
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices);
            var order = new List<TKey>(triangles.Keys);
            mesh.subMeshCount = order.Count;
            for (int i = 0; i < order.Count; i++) mesh.SetTriangles(triangles[order[i]], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            SubmeshMaterials[mesh] = order;
            return mesh;
        }
    }
}
