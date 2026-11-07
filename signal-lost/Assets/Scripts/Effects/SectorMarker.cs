using SignalLost.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Effects
{
    /// <summary>
    /// A wedge painted over the ground warning where a sweeping attack will go (the Chief Toad's torrent sweep): a
    /// see-through fill that darkens as the attack charges, a bright outline, and a thick pulsing line on the edge the
    /// sweep starts from, with chevrons pointing the way it will turn. It drapes over the ground, so hills don't hide
    /// it. The owner sets <see cref="Progress"/>, calls <see cref="Fire"/> when the attack starts and destroys it after.
    /// </summary>
    public sealed class SectorMarker : MonoBehaviour
    {
        private const int Arcs = 24, Rings = 9;

        private Mesh mesh;
        private Color[] baseColors;
        // Copies of the effect materials drawn after other see-through things, so a wedge over a pond shows on top of
        // the water instead of under it on the pond bed. Shared by every wedge.
        private static Material fillMaterial, lineMaterial;

        /// <summary>The render queue for ground warnings: after see-through water (3000) and before UI-ish overlays.</summary>
        public const int OverWaterQueue = 3100;

        public static Material OverWater(Material source, ref Material copy)
        {
            if (copy != null) return copy;
            copy = new Material(source) { name = source.name + " (over water)", renderQueue = OverWaterQueue, hideFlags = HideFlags.DontSave };
            return copy;
        }
        private LineRenderer outline, startEdge;
        private LineRenderer[] chevrons;
        private Color color;
        private bool fired;
        private float firedAt;

        /// <summary>0 when the warning starts, 1 when the attack begins.</summary>
        public float Progress { get; set; }

        /// <param name="apex">Where the attacker stands.</param>
        /// <param name="centreYaw">World yaw (degrees) of the middle of the arc.</param>
        /// <param name="halfArc">Degrees either side of the middle.</param>
        /// <param name="direction">+1 if the sweep turns clockwise seen from above (yaw increasing), -1 if not.</param>
        public static SectorMarker Spawn(Vector3 apex, float centreYaw, float halfArc, float radius, float direction, Color color)
        {
            var go = new GameObject("Sweep Warning");
            go.transform.position = apex;
            var marker = go.AddComponent<SectorMarker>();
            marker.color = color;
            marker.Build(apex, centreYaw, halfArc, radius, direction);
            return marker;
        }

        public void Fire()
        {
            fired = true;
            firedAt = Time.time;
        }

        private void Build(Vector3 apex, float centreYaw, float halfArc, float radius, float direction)
        {
            // A fan of draped vertices: ring r, arc step a.
            var vertices = new Vector3[(Rings + 1) * (Arcs + 1)];
            var uv = new Vector2[vertices.Length];
            baseColors = new Color[vertices.Length];
            for (int r = 0; r <= Rings; r++)
                for (int a = 0; a <= Arcs; a++)
                {
                    float yaw = centreYaw + Mathf.Lerp(-halfArc, halfArc, a / (float)Arcs);
                    float distance = radius * r / Rings;
                    int i = r * (Arcs + 1) + a;
                    vertices[i] = Drape(apex + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * distance) - apex;
                    uv[i] = new Vector2(0.5f, 0.5f); // the middle of the band texture: solid
                    // Faint near him, strongest toward the far edge where it is hardest to get out of.
                    baseColors[i] = new Color(color.r, color.g, color.b, Mathf.Lerp(0.6f, 1f, r / (float)Rings));
                }
            var triangles = new int[Rings * Arcs * 6];
            int t = 0;
            for (int r = 0; r < Rings; r++)
                for (int a = 0; a < Arcs; a++)
                {
                    int i = r * (Arcs + 1) + a, j = i + Arcs + 1;
                    triangles[t++] = i; triangles[t++] = j; triangles[t++] = i + 1;
                    triangles[t++] = i + 1; triangles[t++] = j; triangles[t++] = j + 1;
                }
            mesh = new Mesh { name = "Sweep Warning" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.colors = baseColors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = OverWater(EffectMaterials.AlphaBlended(EffectMaterials.Look.Beam), ref fillMaterial);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Outline: one radial edge, round the far arc, back along the other edge.
            var points = new Vector3[Arcs + 1 + 2 * Rings];
            int p = 0;
            for (int r = 0; r < Rings; r++) points[p++] = vertices[r * (Arcs + 1)];
            for (int a = 0; a <= Arcs; a++) points[p++] = vertices[Rings * (Arcs + 1) + a];
            for (int r = Rings - 1; r >= 0; r--) points[p++] = vertices[r * (Arcs + 1) + Arcs];
            outline = Line("Outline", points, 0.3f);

            // The edge the sweep starts from: thick and pulsing.
            int startColumn = direction > 0f ? 0 : Arcs;
            var edge = new Vector3[Rings + 1];
            for (int r = 0; r <= Rings; r++) edge[r] = vertices[r * (Arcs + 1) + startColumn];
            startEdge = Line("Start Edge", edge, 0.7f);

            // Chevrons across the middle of the wedge, pointing the way it will turn.
            chevrons = new LineRenderer[3];
            for (int c = 0; c < chevrons.Length; c++)
            {
                int ring = Mathf.RoundToInt(Rings * (0.45f + 0.2f * c));
                int middle = Arcs / 2 + (int)direction * (c - 1) * 3;
                Vector3 tip = vertices[ring * (Arcs + 1) + Mathf.Clamp(middle + (int)direction * 2, 0, Arcs)];
                Vector3 inner = vertices[(ring - 1) * (Arcs + 1) + Mathf.Clamp(middle, 0, Arcs)];
                Vector3 outer = vertices[Mathf.Min(Rings, ring + 1) * (Arcs + 1) + Mathf.Clamp(middle, 0, Arcs)];
                chevrons[c] = Line("Chevron", new[] { inner, tip, outer }, 0.45f);
            }
        }

        private LineRenderer Line(string name, Vector3[] points, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = OverWater(EffectMaterials.Additive(EffectMaterials.Look.Beam), ref lineMaterial);
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            for (int i = 0; i < points.Length; i++) line.SetPosition(i, points[i] + Vector3.up * 0.03f);
            line.startWidth = line.endWidth = width;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void Update()
        {
            float p = Mathf.Clamp01(Progress);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(6f, 22f, p));
            float fade = fired ? Mathf.Clamp01(1f - (Time.time - firedAt) / 2.5f) : 1f;

            // The fill darkens in as the attack charges; once it fires it fades while the torrent crosses it.
            float fill = Mathf.Lerp(0.4f, 0.75f, p) * (0.85f + 0.15f * pulse) * (fired ? 0.6f * fade + 0.15f : 1f);
            var colors = new Color[baseColors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                Color c = baseColors[i];
                colors[i] = new Color(c.r, c.g, c.b, c.a * fill);
            }
            mesh.colors = colors;

            Color bright = Color.Lerp(color, Color.white, 0.25f);
            outline.startColor = outline.endColor = new Color(bright.r, bright.g, bright.b, (0.45f + 0.4f * p) * fade);
            startEdge.startColor = startEdge.endColor = new Color(1f, 0.95f, 0.85f, (0.35f + 0.65f * pulse) * fade);
            startEdge.widthMultiplier = Mathf.Lerp(0.8f, 1.3f, pulse);
            foreach (LineRenderer chevron in chevrons)
                chevron.startColor = chevron.endColor = new Color(bright.r, bright.g, bright.b, (0.3f + 0.6f * pulse) * fade * (fired ? 0f : 1f));
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }

        // The ground (not creatures or the player) under a point, a few centimetres up.
        private static Vector3 Drape(Vector3 point)
        {
            float best = float.MaxValue;
            Vector3 ground = point;
            foreach (RaycastHit hit in Physics.RaycastAll(point + Vector3.up * 12f, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider is CharacterController || hit.collider.GetComponentInParent<Health>() != null) continue;
                if (hit.distance < best) { best = hit.distance; ground = hit.point; }
            }
            return ground + Vector3.up * 0.08f;
        }
    }
}
