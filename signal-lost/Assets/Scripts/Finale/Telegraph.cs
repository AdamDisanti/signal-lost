using System.Collections.Generic;
using SignalLost.Mudlands;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Finale
{
    /// <summary>
    /// A warning painted on the ground before one of the mech's attacks lands: a wedge (the freezing sweep), a strip
    /// (the thruster charge) or a disc (the stomp). A faint outline shows the whole area at once, and a brighter copy
    /// grows to fill it as <see cref="Progress"/> goes from 0 to 1, so the player can read when it fires. It lies flat
    /// at the height it was spawned at (the arena is levelled), and removes itself with <see cref="Clear"/>.
    /// </summary>
    public sealed class Telegraph : MonoBehaviour
    {
        public enum Shape { Wedge, Strip, Disc }

        private Transform fill;
        private Shape shape;
        private float progress;

        public float Progress
        {
            get => progress;
            set
            {
                progress = Mathf.Clamp01(value);
                float p = Mathf.Max(0.001f, progress);
                fill.localScale = shape == Shape.Strip ? new Vector3(1f, 1f, p) : new Vector3(p, 1f, p);
            }
        }

        /// <param name="size">Wedge: (radius, half-angle in degrees). Strip: (width, length). Disc: (radius, unused).</param>
        public static Telegraph Spawn(Shape shape, Vector3 origin, Quaternion facing, Vector2 size, Color color)
        {
            var root = new GameObject("Telegraph " + shape).AddComponent<Telegraph>();
            root.shape = shape;
            root.transform.SetPositionAndRotation(origin + Vector3.up * 0.07f, Quaternion.Euler(0f, facing.eulerAngles.y, 0f));
            Mesh mesh = Build(shape, size);
            Add(root.transform, "Outline", mesh, MudArt.Clear(new Color(color.r, color.g, color.b, color.a * 0.45f), 1f));
            root.fill = Add(root.transform, "Fill", mesh, MudArt.Clear(color, 1.6f));
            root.fill.localPosition = Vector3.up * 0.01f;
            root.Progress = 0f;
            return root;
        }

        public void Clear() => Destroy(gameObject);

        private static Transform Add(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go.transform;
        }

        private static readonly Dictionary<(Shape, Vector2), Mesh> Meshes = new Dictionary<(Shape, Vector2), Mesh>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Meshes.Clear();

        // Flat, facing up, with the origin where the attack starts (the wedge's apex, the strip's near end, the disc's centre).
        private static Mesh Build(Shape shape, Vector2 size)
        {
            if (Meshes.TryGetValue((shape, size), out Mesh found) && found != null) return found;
            var vertices = new List<Vector3> { Vector3.zero };
            var triangles = new List<int>();
            switch (shape)
            {
                case Shape.Wedge:
                {
                    int steps = 24;
                    for (int i = 0; i <= steps; i++)
                    {
                        float a = Mathf.Lerp(-size.y, size.y, i / (float)steps) * Mathf.Deg2Rad;
                        vertices.Add(new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * size.x);
                        if (i > 0) triangles.AddRange(new[] { 0, i, i + 1 });
                    }
                    break;
                }
                case Shape.Disc:
                {
                    int steps = 32;
                    for (int i = 0; i <= steps; i++)
                    {
                        float a = i / (float)steps * Mathf.PI * 2f;
                        vertices.Add(new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * size.x);
                        if (i > 0) triangles.AddRange(new[] { 0, i, i + 1 });
                    }
                    break;
                }
                default:
                {
                    float w = size.x * 0.5f;
                    vertices.Clear();
                    vertices.AddRange(new[] { new Vector3(-w, 0f, 0f), new Vector3(-w, 0f, size.y), new Vector3(w, 0f, size.y), new Vector3(w, 0f, 0f) });
                    triangles.AddRange(new[] { 0, 1, 2, 0, 2, 3 });
                    break;
                }
            }
            var mesh = new Mesh { name = "Telegraph " + shape, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            var normals = new Vector3[vertices.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            Meshes[(shape, size)] = mesh;
            return mesh;
        }
    }
}
