using System.Collections.Generic;
using UnityEngine;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// The Core Worm's body as one continuous, bending mesh instead of a chain of balls. Every frame it is rebuilt along
    /// the path the head has travelled: a thick obsidian body that swells behind the head and tapers to the tail, with
    /// molten core veins winding down its sides and two ship cables running along its back. The ship parts grown into
    /// it (plates, clamps, engine housings, exhausts) and the hit boxes ride on anchor points placed along the same path
    /// by <see cref="Coreworm"/>; <see cref="RadiusAt"/> keeps them sitting on its surface.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class CorewormBody : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [Tooltip("Body length in metres, from the head pivot to the tail tip.")]
        [SerializeField, Min(2f)] private float length = 29f;
        [SerializeField, Range(6, 24)] private int sides = 12;
        [Tooltip("Metres between rings of the mesh along the body.")]
        [SerializeField, Min(0.2f)] private float ringSpacing = 0.55f;
        [Tooltip("The glowing core veins (unlit).")]
        [SerializeField] private MeshFilter veins;
        [SerializeField, Range(0, 6)] private int veinCount = 4;
        [SerializeField, Min(0.01f)] private float veinRadius = 0.1f;
        [Tooltip("The ship cables along its back.")]
        [SerializeField] private MeshFilter cables;
        [SerializeField, Min(0.01f)] private float cableRadius = 0.13f;

        private Mesh bodyMesh, veinMesh, cableMesh;
        private Vector3[] bodyVerts, bodyNormals, veinVerts, cableVerts, cableNormals;
        private readonly List<float> distances = new List<float>();
        private Vector3[] centres = new Vector3[0], tangents = new Vector3[0], normals = new Vector3[0], binormals = new Vector3[0];
        private int rings;
        private Renderer veinRenderer;
        private Color veinColor = new Color(1f, 0.45f, 0.1f);
        private MaterialPropertyBlock block;

        private const int VeinSides = 4, CableSides = 5;

        public float Length => length;

        /// <summary>
        /// Radius of the body at a fraction of its length (0 at the head pivot, 1 at the tail tip): a neck that swells
        /// to the thickest part a fifth of the way back, then a long taper to a thin tail.
        /// </summary>
        public static float RadiusAt(float t)
        {
            t = Mathf.Clamp01(t);
            if (t < 0.18f) return Mathf.Lerp(1.3f, 1.6f, Mathf.SmoothStep(0f, 1f, t / 0.18f));
            return Mathf.Lerp(1.6f, 0.16f, Mathf.Pow((t - 0.18f) / 0.82f, 1.25f));
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
            TubeTriangles(triangles, 0, rings, sides, true);
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
                for (int v = 0; v < veinCount; v++) TubeTriangles(veinTriangles, v * rings * VeinSides, rings, VeinSides, true);
                veinMesh.vertices = veinVerts;
                veinMesh.SetTriangles(veinTriangles, 0);
                veinRenderer = veins.GetComponent<Renderer>();
                if (veinRenderer != null && veinRenderer.sharedMaterial != null && veinRenderer.sharedMaterial.HasProperty(BaseColor))
                    veinColor = veinRenderer.sharedMaterial.GetColor(BaseColor);
            }
            if (cables != null)
            {
                cableMesh = NewMesh("Coreworm Cables", cables);
                cableVerts = new Vector3[2 * rings * CableSides];
                cableNormals = new Vector3[cableVerts.Length];
                var cableTriangles = new List<int>();
                for (int c = 0; c < 2; c++) TubeTriangles(cableTriangles, c * rings * CableSides, rings, CableSides, true);
                cableMesh.vertices = cableVerts;
                cableMesh.SetTriangles(cableTriangles, 0);
            }
        }

        private static Mesh NewMesh(string name, MeshFilter filter)
        {
            var mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
            return mesh;
        }

        private static void TubeTriangles(List<int> triangles, int start, int ringCount, int around, bool outward)
        {
            for (int i = 0; i < ringCount - 1; i++)
                for (int j = 0; j < around; j++)
                {
                    int a = start + i * around + j, b = start + i * around + (j + 1) % around;
                    int c = start + (i + 1) * around + j, d = start + (i + 1) * around + (j + 1) % around;
                    // Rings run toward the tail and sides run from the frame's up toward Cross(tangent, up):
                    // (a, b, c) is then clockwise seen from outside, Unity's front face.
                    if (outward) triangles.AddRange(new[] { a, b, c, b, d, c });
                    else triangles.AddRange(new[] { a, c, b, b, c, d });
                }
        }

        /// <summary>How brightly the veins burn (0 dark, 1 normal, above 1 white-hot).</summary>
        public void SetGlow(float glow)
        {
            if (veinRenderer == null) return;
            veinRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColor, Color.Lerp(veinColor, Color.white, Mathf.Clamp01(glow - 1f) * 0.6f) * Mathf.Max(0.25f, glow));
            veinRenderer.SetPropertyBlock(block);
        }

        /// <summary>
        /// Rebuilds the body along a path in world space that starts at the head pivot and runs back along where the
        /// head has been (it is extended straight on if it is shorter than the body).
        /// </summary>
        public void Rebuild(IReadOnlyList<Vector3> path, Vector3 upHint)
        {
            if (bodyMesh == null || path == null || path.Count < 2) return;
            distances.Clear();
            distances.Add(0f);
            for (int i = 1; i < path.Count; i++) distances.Add(distances[i - 1] + Vector3.Distance(path[i - 1], path[i]));

            // Ring centres and tangents along the path; frames carried down the body so it never twists.
            int segment = 1;
            for (int r = 0; r < rings; r++)
            {
                float d = length * r / (rings - 1);
                while (segment < path.Count - 1 && distances[segment] < d) segment++;
                Vector3 a = path[segment - 1], b = path[segment];
                float span = distances[segment] - distances[segment - 1];
                Vector3 direction = span > 0.0001f ? (b - a) / span : (r > 0 ? tangents[r - 1] : Vector3.back);
                float along = d - distances[segment - 1];
                centres[r] = a + direction * along; // beyond the end: carries straight on
                tangents[r] = direction;
            }
            for (int r = 1; r < rings - 1; r++)
            {
                Vector3 smooth = (centres[r + 1] - centres[r - 1]);
                if (smooth.sqrMagnitude > 0.0001f) tangents[r] = smooth.normalized;
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

            // The body: slightly flattened belly, faint ridges.
            for (int r = 0; r < rings; r++)
            {
                float t = r / (float)(rings - 1);
                float radius = RadiusAt(t);
                for (int j = 0; j < sides; j++)
                {
                    float angle = j * Mathf.PI * 2f / sides;
                    float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
                    float bulge = 1f + 0.05f * Mathf.Sin(t * 47f + angle * 3f);
                    float belly = c < 0f ? 1f + c * 0.15f : 1f;
                    Vector3 outward = normals[r] * c + binormals[r] * s;
                    int index = r * sides + j;
                    bodyVerts[index] = transform.InverseTransformPoint(centres[r] + outward * radius * bulge * belly);
                    bodyNormals[index] = transform.InverseTransformDirection(outward);
                }
            }
            bodyVerts[rings * sides] = transform.InverseTransformPoint(centres[rings - 1] + tangents[rings - 1] * 0.4f);
            bodyNormals[rings * sides] = transform.InverseTransformDirection(tangents[rings - 1]);
            bodyMesh.vertices = bodyVerts;
            bodyMesh.normals = bodyNormals;
            bodyMesh.RecalculateBounds();

            if (veinMesh != null) BuildVeins();
            if (cableMesh != null) BuildCables();
        }

        // Molten veins winding down the sides and belly, half sunk into the hide, thinning toward the tail.
        private void BuildVeins()
        {
            float[] bases = { 1.25f, 2.6f, 3.7f, 5.05f, 0.6f, 5.7f };
            for (int v = 0; v < veinCount; v++)
                for (int r = 0; r < rings; r++)
                {
                    float t = r / (float)(rings - 1);
                    float angle = bases[v % bases.Length] + 0.35f * Mathf.Sin(t * 9f + v * 1.7f);
                    Vector3 outward = normals[r] * Mathf.Cos(angle) + binormals[r] * Mathf.Sin(angle);
                    Vector3 side = Vector3.Cross(tangents[r], outward);
                    Vector3 spine = centres[r] + outward * RadiusAt(t) * 0.98f;
                    float thickness = veinRadius * Mathf.Lerp(1.3f, 0.35f, t) * (1f + 0.4f * Mathf.Sin(t * 31f + v));
                    for (int j = 0; j < VeinSides; j++)
                    {
                        float a = j * Mathf.PI * 2f / VeinSides;
                        veinVerts[(v * rings + r) * VeinSides + j] =
                            transform.InverseTransformPoint(spine + (outward * Mathf.Cos(a) + side * Mathf.Sin(a)) * thickness);
                    }
                }
            veinMesh.vertices = veinVerts;
            veinMesh.RecalculateBounds();
        }

        // Two cables along its back, a little proud of the hide, stopping before the thin end of the tail.
        private void BuildCables()
        {
            for (int c = 0; c < 2; c++)
                for (int r = 0; r < rings; r++)
                {
                    float t = Mathf.Min(r / (float)(rings - 1), 0.82f);
                    int ring = Mathf.Min(r, Mathf.RoundToInt(0.82f * (rings - 1)));
                    float angle = (c == 0 ? -0.45f : 0.45f) + 0.08f * Mathf.Sin(t * 13f + c);
                    Vector3 outward = normals[ring] * Mathf.Cos(angle) + binormals[ring] * Mathf.Sin(angle);
                    Vector3 side = Vector3.Cross(tangents[ring], outward);
                    Vector3 spine = centres[ring] + outward * (RadiusAt(t) + cableRadius * 0.6f);
                    for (int j = 0; j < CableSides; j++)
                    {
                        float a = j * Mathf.PI * 2f / CableSides;
                        Vector3 around = outward * Mathf.Cos(a) + side * Mathf.Sin(a);
                        int index = (c * rings + r) * CableSides + j;
                        cableVerts[index] = transform.InverseTransformPoint(spine + around * cableRadius);
                        cableNormals[index] = transform.InverseTransformDirection(around);
                    }
                }
            cableMesh.vertices = cableVerts;
            cableMesh.normals = cableNormals;
            cableMesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            if (bodyMesh != null) Destroy(bodyMesh);
            if (veinMesh != null) Destroy(veinMesh);
            if (cableMesh != null) Destroy(cableMesh);
        }
    }
}
