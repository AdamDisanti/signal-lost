using System.Collections.Generic;
using UnityEngine;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// The Core Worm's body as one continuous, bending mesh. Every frame it is rebuilt along the path the head has
    /// travelled: a dark desert-coloured worm, ringed with soft annular ridges, that swells behind the head and tapers to
    /// a fine tail, with glowing veins of core energy winding over it and pulses of light racing along them from head to
    /// tail.
    ///
    /// The raw path is resampled evenly and smoothed before the body is laid along it, so tight turns bend into curves
    /// instead of corners. <see cref="PointAt"/> gives positions on that same smoothed curve, so anything placed along the
    /// body (the hit boxes and weak spots) sits on it; <see cref="RadiusAt"/> gives its thickness there.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class CorewormBody : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [Tooltip("Body length in metres, from the head pivot to the tail tip.")]
        [SerializeField, Min(2f)] private float length = 29f;
        [SerializeField, Range(6, 24)] private int sides = 14;
        [Tooltip("Metres between rings of the mesh along the body.")]
        [SerializeField, Min(0.2f)] private float ringSpacing = 0.4f;
        [Tooltip("Metres between the worm's annular ridges.")]
        [SerializeField, Min(0.2f)] private float ridgeSpacing = 0.85f;
        [Tooltip("How many smoothing passes the path gets (more: rounder turns).")]
        [SerializeField, Range(0, 12)] private int smoothing = 6;
        [Tooltip("The glowing energy veins (unlit).")]
        [SerializeField] private MeshFilter veins;
        [SerializeField, Range(0, 8)] private int veinCount = 6;
        [SerializeField, Min(0.01f)] private float veinRadius = 0.09f;
        [Tooltip("Speed of the pulses running down the veins, in metres per second.")]
        [SerializeField, Min(0f)] private float pulseSpeed = 9f;

        private const float Step = 0.3f; // spacing of the smoothed path
        private const int VeinSides = 4;

        private Mesh bodyMesh, veinMesh;
        private Vector3[] bodyVerts, bodyNormals, veinVerts;
        private readonly List<Vector3> even = new List<Vector3>();
        private readonly List<Vector3> scratch = new List<Vector3>();
        private Vector3[] centres = new Vector3[0], tangents = new Vector3[0], normals = new Vector3[0], binormals = new Vector3[0];
        private int rings;
        private Renderer veinRenderer;
        private Color veinColor = new Color(1f, 0.5f, 0.15f);
        private MaterialPropertyBlock block;
        private float pulse = 1f;

        public float Length => length;

        /// <summary>
        /// Radius of the body at a fraction of its length (0 at the head pivot, 1 at the tail tip): a neck that swells
        /// to the thickest part a fifth of the way back, then a long taper to a fine tail.
        /// </summary>
        public static float RadiusAt(float t)
        {
            t = Mathf.Clamp01(t);
            if (t < 0.18f) return Mathf.Lerp(1.3f, 1.6f, Mathf.SmoothStep(0f, 1f, t / 0.18f));
            return Mathf.Lerp(1.6f, 0.12f, Mathf.Pow((t - 0.18f) / 0.82f, 1.15f));
        }

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            rings = Mathf.Max(4, Mathf.CeilToInt(length / ringSpacing) + 1);
            centres = new Vector3[rings];
            tangents = new Vector3[rings];
            normals = new Vector3[rings];
            binormals = new Vector3[rings];

            bodyMesh = NewMesh("Coreworm Body", GetComponent<MeshFilter>());
            bodyVerts = new Vector3[rings * sides + 1];
            bodyNormals = new Vector3[bodyVerts.Length];
            var triangles = new List<int>();
            TubeTriangles(triangles, 0, rings, sides);
            int tip = rings * sides;
            for (int j = 0; j < sides; j++) // close the tail
                triangles.AddRange(new[] { (rings - 1) * sides + j, (rings - 1) * sides + (j + 1) % sides, tip });
            bodyMesh.vertices = bodyVerts;
            bodyMesh.SetTriangles(triangles, 0);

            if (veins != null && veinCount > 0)
            {
                veinMesh = NewMesh("Coreworm Veins", veins);
                veinVerts = new Vector3[veinCount * rings * VeinSides];
                var veinTriangles = new List<int>();
                for (int v = 0; v < veinCount; v++) TubeTriangles(veinTriangles, v * rings * VeinSides, rings, VeinSides);
                veinMesh.vertices = veinVerts;
                veinMesh.SetTriangles(veinTriangles, 0);
                veinRenderer = veins.GetComponent<Renderer>();
                if (veinRenderer != null && veinRenderer.sharedMaterial != null && veinRenderer.sharedMaterial.HasProperty(BaseColor))
                    veinColor = veinRenderer.sharedMaterial.GetColor(BaseColor);
            }
        }

        private static Mesh NewMesh(string name, MeshFilter filter)
        {
            var mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
            return mesh;
        }

        // Rings run toward the tail and sides run from the frame's up toward Cross(tangent, up):
        // (a, b, c) is then clockwise seen from outside, Unity's front face.
        private static void TubeTriangles(List<int> triangles, int start, int ringCount, int around)
        {
            for (int i = 0; i < ringCount - 1; i++)
                for (int j = 0; j < around; j++)
                {
                    int a = start + i * around + j, b = start + i * around + (j + 1) % around;
                    int c = start + (i + 1) * around + j, d = start + (i + 1) * around + (j + 1) % around;
                    triangles.AddRange(new[] { a, b, c, b, d, c });
                }
        }

        /// <summary>How brightly the veins burn (0 dark, 1 normal, above 1 white-hot).</summary>
        public void SetGlow(float glow)
        {
            pulse = Mathf.Max(0.2f, glow);
            if (veinRenderer == null) return;
            veinRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColor, Color.Lerp(veinColor, Color.white, Mathf.Clamp01(glow - 1f) * 0.6f) * Mathf.Max(0.25f, glow));
            veinRenderer.SetPropertyBlock(block);
        }

        /// <summary>A point on the smoothed body curve, a distance back from the head pivot, and the direction toward the tail there.</summary>
        public Vector3 PointAt(float distance, out Vector3 towardTail)
        {
            if (even.Count < 2)
            {
                towardTail = -transform.forward;
                return transform.position;
            }
            float f = Mathf.Max(0f, distance) / Step;
            int i = Mathf.Min(Mathf.FloorToInt(f), even.Count - 2);
            towardTail = (even[i + 1] - even[i]).normalized;
            return Vector3.Lerp(even[i], even[i + 1], f - i);
        }

        /// <summary>
        /// Rebuilds the body along a path in world space that starts at the head pivot and runs back along where the
        /// head has been (it is carried straight on if it is shorter than the body).
        /// </summary>
        public void Rebuild(IReadOnlyList<Vector3> path, Vector3 upHint)
        {
            if (bodyMesh == null || path == null || path.Count < 2) return;
            Resample(path);
            Smooth();

            // Ring centres along the smoothed curve; frames carried down the body so it never twists.
            for (int r = 0; r < rings; r++)
            {
                float d = length * r / (rings - 1);
                centres[r] = PointAt(d, out Vector3 direction);
                tangents[r] = direction;
            }
            for (int r = 1; r < rings - 1; r++)
            {
                Vector3 across = centres[r + 1] - centres[r - 1];
                if (across.sqrMagnitude > 0.0001f) tangents[r] = across.normalized;
            }
            Vector3 up = Vector3.ProjectOnPlane(upHint, tangents[0]);
            if (up.sqrMagnitude < 0.0001f) up = Vector3.ProjectOnPlane(Vector3.up, tangents[0]);
            if (up.sqrMagnitude < 0.0001f) up = Vector3.ProjectOnPlane(Vector3.forward, tangents[0]);
            normals[0] = up.normalized;
            binormals[0] = Vector3.Cross(tangents[0], normals[0]);
            for (int r = 1; r < rings; r++)
            {
                Vector3 n = Vector3.ProjectOnPlane(normals[r - 1], tangents[r]);
                normals[r] = n.sqrMagnitude > 0.0001f ? n.normalized : normals[r - 1];
                binormals[r] = Vector3.Cross(tangents[r], normals[r]);
            }

            // The body: soft annular ridges like an earthworm's, a slightly flattened belly.
            for (int r = 0; r < rings; r++)
            {
                float t = r / (float)(rings - 1);
                float d = length * t;
                float ridge = 1f + 0.055f * Mathf.Pow(0.5f + 0.5f * Mathf.Cos(d / ridgeSpacing * Mathf.PI * 2f), 2f);
                float radius = RadiusAt(t) * ridge;
                for (int j = 0; j < sides; j++)
                {
                    float angle = j * Mathf.PI * 2f / sides;
                    float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
                    float belly = c < 0f ? 1f + c * 0.14f : 1f;
                    Vector3 outward = normals[r] * c + binormals[r] * s;
                    int index = r * sides + j;
                    bodyVerts[index] = transform.InverseTransformPoint(centres[r] + outward * radius * belly);
                    bodyNormals[index] = transform.InverseTransformDirection(outward);
                }
            }
            bodyVerts[rings * sides] = transform.InverseTransformPoint(centres[rings - 1] + tangents[rings - 1] * 0.3f);
            bodyNormals[rings * sides] = transform.InverseTransformDirection(tangents[rings - 1]);
            bodyMesh.vertices = bodyVerts;
            bodyMesh.normals = bodyNormals;
            bodyMesh.RecalculateBounds();

            if (veinMesh != null) BuildVeins();
        }

        /// <summary>Evenly spaced points along the path, carried straight on past its end to cover the whole body.</summary>
        private void Resample(IReadOnlyList<Vector3> path)
        {
            even.Clear();
            even.Add(path[0]);
            int needed = Mathf.CeilToInt(length / Step) + 2;
            float carried = 0f;
            Vector3 direction = (path[1] - path[0]).normalized;
            for (int i = 1; i < path.Count && even.Count < needed; i++)
            {
                Vector3 a = path[i - 1], b = path[i];
                float span = Vector3.Distance(a, b);
                if (span < 0.0001f) continue;
                direction = (b - a) / span;
                float at = Step - carried;
                while (at <= span && even.Count < needed)
                {
                    even.Add(a + direction * at);
                    at += Step;
                }
                carried = span - (at - Step);
            }
            while (even.Count < needed) even.Add(even[even.Count - 1] + direction * Step);
        }

        /// <summary>Rounds off corners: repeated neighbour averaging, keeping the first points pinned to the head.</summary>
        private void Smooth()
        {
            for (int pass = 0; pass < smoothing; pass++)
            {
                scratch.Clear();
                scratch.AddRange(even);
                for (int i = 3; i < even.Count - 1; i++)
                    even[i] = (scratch[i - 1] + scratch[i] * 2f + scratch[i + 1]) * 0.25f;
            }
        }

        // Energy veins winding over the body, half sunk into the hide, thinning toward the tail; bright swellings of
        // energy pulse down them from the head.
        private void BuildVeins()
        {
            float time = Time.time;
            for (int v = 0; v < veinCount; v++)
            {
                float baseAngle = v * Mathf.PI * 2f / veinCount + 0.3f;
                float twist = (v % 2 == 0 ? 1f : -1f) * 0.9f; // neighbouring veins spiral opposite ways, crossing now and then
                for (int r = 0; r < rings; r++)
                {
                    float t = r / (float)(rings - 1);
                    float d = length * t;
                    float angle = baseAngle + twist * t * 2.2f + 0.45f * Mathf.Sin(d * 0.55f + v * 1.9f) + 0.15f * Mathf.Sin(d * 1.7f + v);
                    Vector3 outward = normals[r] * Mathf.Cos(angle) + binormals[r] * Mathf.Sin(angle);
                    Vector3 side = Vector3.Cross(tangents[r], outward);
                    Vector3 spine = centres[r] + outward * RadiusAt(t) * 0.99f;
                    float wave = Mathf.Sin(d * 0.7f - time * pulseSpeed * 0.7f + v * 1.3f);
                    float swell = 1f + 1.1f * Mathf.Pow(Mathf.Max(0f, wave), 6f) * Mathf.Min(1.5f, pulse);
                    float thickness = veinRadius * Mathf.Lerp(1.4f, 0.35f, t) * (1f + 0.25f * Mathf.Sin(d * 2.3f + v)) * swell;
                    for (int j = 0; j < VeinSides; j++)
                    {
                        float a = j * Mathf.PI * 2f / VeinSides;
                        veinVerts[(v * rings + r) * VeinSides + j] =
                            transform.InverseTransformPoint(spine + (outward * Mathf.Cos(a) + side * Mathf.Sin(a)) * thickness);
                    }
                }
            }
            veinMesh.vertices = veinVerts;
            veinMesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            if (bodyMesh != null) Destroy(bodyMesh);
            if (veinMesh != null) Destroy(veinMesh);
        }
    }
}
