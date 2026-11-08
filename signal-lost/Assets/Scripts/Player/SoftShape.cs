using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Player
{
    /// <summary>
    /// Builds one smooth, seamless mesh from simple rounded pieces (tapered capsules, ellipsoids and rounded boxes)
    /// that melt into each other, like clay: a whole gloved hand, the mechanic's body, an arm. Used by
    /// <see cref="MechanicModel"/>, so the character needs no art assets.
    /// <para>
    /// The pieces are described as a distance field and meshed with surface nets; vertices are pulled onto the true
    /// surface and get normals from the field, so the result shades smoothly with no faceting or seams. Each piece
    /// rides on a bone; with several bones the mesh is skinned (<see cref="SkinnedMeshRenderer"/>) so, for example,
    /// fingers bend smoothly at their joints.
    /// </para>
    /// <para>
    /// Pieces in group 0 (the core, e.g. the palm) blend together. Pieces in another group (e.g. one finger) blend with
    /// each other at the joints and with the core where they attach, but not with other groups, so neighbouring
    /// fingers stay separate instead of fusing into a mitten.
    /// </para>
    /// </summary>
    public sealed class SoftShape
    {
        private enum Kind { Cone, Ellipsoid, RoundBox }

        private struct Piece
        {
            public Kind Kind;
            public int Group, Bone;
            public Vector3 A, B; // cone: the two ends; ellipsoid: centre, radii; rounded box: centre, half size
            public float Ra, Rb; // cone: the radii at A and B; rounded box: Ra is the rounding
        }

        private const float Far = 1e9f;
        private readonly List<Piece> pieces = new List<Piece>();
        private int groups = 1;
        private float[] groupDistance;

        /// <summary>How softly the core pieces melt into each other (metres).</summary>
        public float CoreBlend = 0.01f;
        /// <summary>How softly each group (finger, leg...) melts into the core.</summary>
        public float AttachBlend = 0.01f;
        /// <summary>How softly the pieces within a group melt together (a finger's joints).</summary>
        public float JointBlend = 0.003f;

        /// <summary>Starts a new group (a finger, a leg...) that blends into the core but not into other groups.</summary>
        public int NewGroup() => groups++;

        /// <summary>A capsule from <paramref name="a"/> (radius <paramref name="ra"/>) to <paramref name="b"/> (radius <paramref name="rb"/>).</summary>
        public void Cone(int group, int bone, Vector3 a, float ra, Vector3 b, float rb) =>
            pieces.Add(new Piece { Kind = Kind.Cone, Group = group, Bone = bone, A = a, B = b, Ra = ra, Rb = rb });

        public void Ellipsoid(int group, int bone, Vector3 centre, Vector3 radii) =>
            pieces.Add(new Piece { Kind = Kind.Ellipsoid, Group = group, Bone = bone, A = centre, B = radii });

        /// <summary>A box of the given half size whose edges are rounded by <paramref name="rounding"/> (added outside).</summary>
        public void RoundBox(int group, int bone, Vector3 centre, Vector3 halfSize, float rounding) =>
            pieces.Add(new Piece { Kind = Kind.RoundBox, Group = group, Bone = bone, A = centre, B = halfSize, Ra = rounding });

        /// <summary>
        /// Meshes the shape with cells of <paramref name="cell"/> metres. <paramref name="bones"/> gives each bone's pose
        /// in mesh space (one identity pose for an unskinned mesh). With more than one bone the mesh gets bone weights
        /// (blended over <paramref name="skinSoftness"/> metres between pieces) and bind poses for a
        /// <see cref="SkinnedMeshRenderer"/> whose bones are in the same order.
        /// </summary>
        public Mesh Build(string name, float cell, Pose[] bones, float skinSoftness = 0.002f)
        {
            Prepare(bones);
            Vector3 min, max;
            Extent(bones, out min, out max);
            min -= Vector3.one * cell * 2f;
            max += Vector3.one * cell * 2f;
            int nx = Mathf.CeilToInt((max.x - min.x) / cell) + 1;
            int ny = Mathf.CeilToInt((max.y - min.y) / cell) + 1;
            int nz = Mathf.CeilToInt((max.z - min.z) / cell) + 1;

            // Sample the field: first on a coarse grid, then finely only near the surface. Far from it, the coarse
            // value is already the right sign (the field changes no faster than the distance moved).
            const int Coarse = 4;
            int kx = (nx - 1) / Coarse + 2, ky = (ny - 1) / Coarse + 2, kz = (nz - 1) / Coarse + 2;
            var coarse = new float[kx * ky * kz];
            for (int z = 0, i = 0; z < kz; z++)
                for (int y = 0; y < ky; y++)
                    for (int x = 0; x < kx; x++, i++)
                        coarse[i] = Distance(min + new Vector3(x, y, z) * (cell * Coarse), null);
            float band = cell * Coarse * 1.25f;
            var field = new float[nx * ny * nz];
            for (int z = 0, i = 0; z < nz; z++)
                for (int y = 0; y < ny; y++)
                    for (int x = 0; x < nx; x++, i++)
                    {
                        int ix = (x + Coarse / 2) / Coarse, iy = (y + Coarse / 2) / Coarse, iz = (z + Coarse / 2) / Coarse;
                        float near = coarse[ix + kx * (iy + ky * iz)];
                        field[i] = Mathf.Abs(near) > band ? near : Distance(min + new Vector3(x, y, z) * cell, null);
                    }

            // Surface nets: one vertex per cell that the surface passes through, at the mean of its edge crossings.
            int cx = nx - 1, cy = ny - 1, cz = nz - 1;
            var cellVertex = new int[cx * cy * cz];
            var vertices = new List<Vector3>();
            var corner = new float[8];
            for (int z = 0, c = 0; z < cz; z++)
                for (int y = 0; y < cy; y++)
                    for (int x = 0; x < cx; x++, c++)
                    {
                        cellVertex[c] = -1;
                        int inside = 0;
                        for (int k = 0; k < 8; k++)
                        {
                            corner[k] = field[(x + (k & 1)) + nx * ((y + ((k >> 1) & 1)) + ny * (z + ((k >> 2) & 1)))];
                            if (corner[k] < 0f) inside++;
                        }
                        if (inside == 0 || inside == 8) continue;
                        Vector3 sum = Vector3.zero;
                        int crossings = 0;
                        for (int k = 0; k < 8; k++)
                            for (int bit = 1; bit <= 4; bit <<= 1)
                            {
                                if ((k & bit) != 0) continue;
                                int other = k | bit;
                                float a = corner[k], b = corner[other];
                                if ((a < 0f) == (b < 0f)) continue;
                                float t = a / (a - b);
                                sum += Vector3.Lerp(Offset(k), Offset(other), t);
                                crossings++;
                            }
                        cellVertex[c] = vertices.Count;
                        vertices.Add(min + (new Vector3(x, y, z) + sum / crossings) * cell);
                    }

            // A quad across every grid edge the surface crosses, joining the four cells around that edge.
            var triangles = new List<int>();
            var q = new int[4];
            for (int z = 0; z < nz; z++)
                for (int y = 0; y < ny; y++)
                    for (int x = 0; x < nx; x++)
                    {
                        float here = field[x + nx * (y + ny * z)];
                        for (int axis = 0; axis < 3; axis++)
                        {
                            int x1 = x + (axis == 0 ? 1 : 0), y1 = y + (axis == 1 ? 1 : 0), z1 = z + (axis == 2 ? 1 : 0);
                            if (x1 >= nx || y1 >= ny || z1 >= nz) continue;
                            float there = field[x1 + nx * (y1 + ny * z1)];
                            if ((here < 0f) == (there < 0f)) continue;
                            // The four cells sharing this edge: step back along the other two axes.
                            int ua = axis == 0 ? 1 : 0, va = axis == 2 ? 1 : 2;
                            bool ok = true;
                            for (int n = 0; n < 4 && ok; n++)
                            {
                                int du = (n == 1 || n == 2) ? 0 : -1, dv = n >= 2 ? 0 : -1;
                                int px = x, py = y, pz = z;
                                Step(ref px, ref py, ref pz, ua, du);
                                Step(ref px, ref py, ref pz, va, dv);
                                if (px < 0 || py < 0 || pz < 0 || px >= cx || py >= cy || pz >= cz) { ok = false; break; }
                                q[n] = cellVertex[px + cx * (py + cy * pz)];
                                if (q[n] < 0) ok = false;
                            }
                            if (!ok) continue;
                            Vector3 outward = Vector3.zero;
                            outward[axis] = here < 0f ? 1f : -1f;
                            Quad(vertices, triangles, q, outward);
                        }
                    }

            // Pull every vertex onto the true surface and take its normal from the field.
            var normals = new Vector3[vertices.Count];
            float eps = cell * 0.25f;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 p = vertices[i];
                {
                    float d = Distance(p, null);
                    Vector3 g = Gradient(p, eps);
                    float g2 = g.sqrMagnitude;
                    if (g2 > 1e-8f)
                    {
                        Vector3 move = g * (d / g2);
                        if (move.magnitude > cell) move = move.normalized * cell;
                        p -= move;
                    }
                }
                vertices[i] = p;
                Vector3 n = Gradient(p, eps);
                normals[i] = n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
            }

            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontUnloadUnusedAsset };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices.ToArray();
            mesh.normals = normals;
            mesh.triangles = triangles.ToArray();
            if (bones.Length > 1) Skin(mesh, vertices, bones, skinSoftness);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------- Skinning ----------

        // Each vertex follows the bones of the pieces nearest to it, blending smoothly where pieces meet.
        private void Skin(Mesh mesh, List<Vector3> vertices, Pose[] bones, float softness)
        {
            var each = new float[pieces.Count];
            var perBone = new float[bones.Length];
            var weights = new BoneWeight[vertices.Count];
            for (int v = 0; v < vertices.Count; v++)
            {
                Distance(vertices[v], each);
                float nearest = Far;
                for (int i = 0; i < each.Length; i++) nearest = Mathf.Min(nearest, each[i]);
                for (int b = 0; b < perBone.Length; b++) perBone[b] = 0f;
                for (int i = 0; i < each.Length; i++)
                    perBone[pieces[i].Bone] += Mathf.Exp(-(each[i] - nearest) / Mathf.Max(1e-5f, softness));
                weights[v] = TopFour(perBone);
            }
            var bindPoses = new Matrix4x4[bones.Length];
            for (int b = 0; b < bones.Length; b++)
                bindPoses[b] = Matrix4x4.TRS(bones[b].position, bones[b].rotation, Vector3.one).inverse;
            mesh.boneWeights = weights;
            mesh.bindposes = bindPoses;
        }

        private static BoneWeight TopFour(float[] perBone)
        {
            int[] index = { 0, 0, 0, 0 };
            float[] weight = { 0f, 0f, 0f, 0f };
            for (int b = 0; b < perBone.Length; b++)
            {
                float w = perBone[b];
                for (int slot = 0; slot < 4; slot++)
                {
                    if (w <= weight[slot]) continue;
                    for (int move = 3; move > slot; move--) { weight[move] = weight[move - 1]; index[move] = index[move - 1]; }
                    weight[slot] = w;
                    index[slot] = b;
                    break;
                }
            }
            float total = weight[0] + weight[1] + weight[2] + weight[3];
            if (total <= 0f) { total = 1f; weight[0] = 1f; }
            return new BoneWeight
            {
                boneIndex0 = index[0], weight0 = weight[0] / total,
                boneIndex1 = index[1], weight1 = weight[1] / total,
                boneIndex2 = index[2], weight2 = weight[2] / total,
                boneIndex3 = index[3], weight3 = weight[3] / total,
            };
        }

        // ---------- The field ----------

        // Each piece moved into mesh space once, with what its distance needs precomputed, plus a bounding sphere.
        private struct Ready
        {
            public Kind Kind;
            public int Group;
            public Vector3 Centre, AxisX, AxisY, AxisZ; // ellipsoid / box frame (cone: A)
            public Vector3 Size;                         // ellipsoid radii / box half size (cone: B - A)
            public float Ra, Rb, L2, Rr, A2, Il2;         // cone terms (box: Ra = rounding)
            public Vector3 Sphere;                       // bounding sphere centre
            public float Reach;                          // bounding sphere radius
        }

        private Ready[] ready;
        private float widestBlend;

        private void Prepare(Pose[] bones)
        {
            ready = new Ready[pieces.Count];
            for (int i = 0; i < pieces.Count; i++)
            {
                Piece piece = pieces[i];
                Pose bone = bones[piece.Bone];
                var r = new Ready { Kind = piece.Kind, Group = piece.Group, Ra = piece.Ra, Rb = piece.Rb };
                if (piece.Kind == Kind.Cone)
                {
                    Vector3 a = bone.position + bone.rotation * piece.A, b = bone.position + bone.rotation * piece.B;
                    r.Centre = a;
                    r.Size = b - a;
                    r.L2 = Vector3.Dot(r.Size, r.Size);
                    r.Rr = piece.Ra - piece.Rb;
                    r.A2 = r.L2 - r.Rr * r.Rr;
                    r.Il2 = r.L2 > 1e-10f ? 1f / r.L2 : 0f;
                    r.Sphere = (a + b) * 0.5f;
                    r.Reach = Mathf.Sqrt(r.L2) * 0.5f + Mathf.Max(piece.Ra, piece.Rb);
                }
                else
                {
                    r.Centre = bone.position + bone.rotation * piece.A;
                    Quaternion back = Quaternion.Inverse(bone.rotation);
                    // Rows of the inverse rotation: dotting with them takes a mesh-space offset into the piece's frame.
                    Vector3 cx = back * new Vector3(1f, 0f, 0f), cy = back * new Vector3(0f, 1f, 0f), cz = back * new Vector3(0f, 0f, 1f);
                    r.AxisX = new Vector3(cx.x, cy.x, cz.x);
                    r.AxisY = new Vector3(cx.y, cy.y, cz.y);
                    r.AxisZ = new Vector3(cx.z, cy.z, cz.z);
                    r.Size = piece.B;
                    r.Sphere = r.Centre;
                    r.Reach = piece.Kind == Kind.Ellipsoid ? Mathf.Max(piece.B.x, Mathf.Max(piece.B.y, piece.B.z)) : piece.B.magnitude + piece.Ra;
                }
                ready[i] = r;
            }
            groupDistance = new float[groups];
            widestBlend = Mathf.Max(CoreBlend, Mathf.Max(AttachBlend, JointBlend));
        }

        // The signed distance to the whole shape (negative inside). With each, every piece's own distance is filled in
        // exactly (for skinning); without, pieces too far away to matter are only bounded.
        private float Distance(Vector3 p, float[] each)
        {
            float cutoff = Far;
            if (each == null)
            {
                for (int i = 0; i < ready.Length; i++)
                    cutoff = Mathf.Min(cutoff, (p - ready[i].Sphere).magnitude + ready[i].Reach);
                cutoff += widestBlend;
            }
            float core = Far;
            for (int g = 1; g < groups; g++) groupDistance[g] = Far;
            for (int i = 0; i < ready.Length; i++)
            {
                float d = (p - ready[i].Sphere).magnitude - ready[i].Reach;
                if (d <= cutoff)
                {
                    Vector3 o = p - ready[i].Centre;
                    switch (ready[i].Kind)
                    {
                        case Kind.Cone: d = RoundCone(o, ref ready[i]); break;
                        case Kind.Ellipsoid:
                            d = EllipsoidDistance(new Vector3(Vector3.Dot(ready[i].AxisX, o), Vector3.Dot(ready[i].AxisY, o), Vector3.Dot(ready[i].AxisZ, o)), ready[i].Size);
                            break;
                        default:
                            d = RoundBoxDistance(new Vector3(Vector3.Dot(ready[i].AxisX, o), Vector3.Dot(ready[i].AxisY, o), Vector3.Dot(ready[i].AxisZ, o)), ready[i].Size, ready[i].Ra);
                            break;
                    }
                }
                if (each != null) each[i] = d;
                int group = ready[i].Group;
                if (group == 0) core = SmoothMin(core, d, CoreBlend);
                else groupDistance[group] = SmoothMin(groupDistance[group], d, JointBlend);
            }
            if (groups == 1) return core;
            float best = core;
            for (int g = 1; g < groups; g++) best = Mathf.Min(best, SmoothMin(core, groupDistance[g], AttachBlend));
            return best;
        }

        // The field's slope, from four samples around p (a tetrahedron).
        private Vector3 Gradient(Vector3 p, float e)
        {
            var a = new Vector3(1f, -1f, -1f);
            var b = new Vector3(-1f, -1f, 1f);
            var c = new Vector3(-1f, 1f, -1f);
            var d = new Vector3(1f, 1f, 1f);
            return (a * Distance(p + a * e, null) + b * Distance(p + b * e, null) + c * Distance(p + c * e, null) +
                    d * Distance(p + d * e, null)) / (4f * e);
        }

        // Polynomial smooth minimum: a fillet of about k between two surfaces.
        private static float SmoothMin(float a, float b, float k)
        {
            if (k <= 0f) return Mathf.Min(a, b);
            float h = Mathf.Max(k - Mathf.Abs(a - b), 0f) / k;
            return Mathf.Min(a, b) - h * h * k * 0.25f;
        }

        // Exact distance to a capsule whose radius changes from Ra to Rb along it (Inigo Quilez's round cone).
        // o is the point relative to the first end.
        private static float RoundCone(Vector3 o, ref Ready c)
        {
            if (c.L2 < 1e-10f) return o.magnitude - Mathf.Max(c.Ra, c.Rb);
            Vector3 ba = c.Size;
            float y = Vector3.Dot(o, ba), z = y - c.L2;
            float x2 = (o * c.L2 - ba * y).sqrMagnitude, y2 = y * y * c.L2, z2 = z * z * c.L2;
            float k = (c.Rr > 0f ? 1f : c.Rr < 0f ? -1f : 0f) * c.Rr * c.Rr * x2;
            if ((z > 0f ? 1f : z < 0f ? -1f : 0f) * c.A2 * z2 > k) return Mathf.Sqrt(x2 + z2) * c.Il2 - c.Rb;
            if ((y > 0f ? 1f : y < 0f ? -1f : 0f) * c.A2 * y2 < k) return Mathf.Sqrt(x2 + y2) * c.Il2 - c.Ra;
            return (Mathf.Sqrt(x2 * c.A2 * c.Il2) + y * c.Rr) * c.Il2 - c.Ra;
        }

        private static float EllipsoidDistance(Vector3 p, Vector3 r)
        {
            float k0 = new Vector3(p.x / r.x, p.y / r.y, p.z / r.z).magnitude;
            float k1 = new Vector3(p.x / (r.x * r.x), p.y / (r.y * r.y), p.z / (r.z * r.z)).magnitude;
            if (k1 < 1e-8f) return -Mathf.Min(r.x, Mathf.Min(r.y, r.z));
            return k0 * (k0 - 1f) / k1;
        }

        private static float RoundBoxDistance(Vector3 p, Vector3 half, float rounding)
        {
            Vector3 q = new Vector3(Mathf.Abs(p.x) - half.x, Mathf.Abs(p.y) - half.y, Mathf.Abs(p.z) - half.z);
            Vector3 outside = new Vector3(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f), Mathf.Max(q.z, 0f));
            return outside.magnitude + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f) - rounding;
        }

        // ---------- Mesh helpers ----------

        private void Extent(Pose[] bones, out Vector3 min, out Vector3 max)
        {
            min = Vector3.one * Far;
            max = -Vector3.one * Far;
            foreach (Piece piece in pieces)
            {
                Pose bone = bones[piece.Bone];
                switch (piece.Kind)
                {
                    case Kind.Cone:
                        Grow(ref min, ref max, bone.position + bone.rotation * piece.A, piece.Ra);
                        Grow(ref min, ref max, bone.position + bone.rotation * piece.B, piece.Rb);
                        break;
                    case Kind.Ellipsoid:
                        Grow(ref min, ref max, bone.position + bone.rotation * piece.A, Mathf.Max(piece.B.x, Mathf.Max(piece.B.y, piece.B.z)));
                        break;
                    default:
                        Grow(ref min, ref max, bone.position + bone.rotation * piece.A, piece.B.magnitude + piece.Ra);
                        break;
                }
            }
        }

        private static void Grow(ref Vector3 min, ref Vector3 max, Vector3 centre, float radius)
        {
            min = new Vector3(Mathf.Min(min.x, centre.x - radius), Mathf.Min(min.y, centre.y - radius), Mathf.Min(min.z, centre.z - radius));
            max = new Vector3(Mathf.Max(max.x, centre.x + radius), Mathf.Max(max.y, centre.y + radius), Mathf.Max(max.z, centre.z + radius));
        }

        private static Vector3 Offset(int corner) => new Vector3(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1);

        private static void Step(ref int x, ref int y, ref int z, int axis, int by)
        {
            if (axis == 0) x += by;
            else if (axis == 1) y += by;
            else z += by;
        }

        // Two triangles facing outward, split along the shorter diagonal.
        private static void Quad(List<Vector3> v, List<int> triangles, int[] q, Vector3 outward)
        {
            Vector3 a = v[q[0]], b = v[q[1]], c = v[q[2]], d = v[q[3]];
            Vector3 facing = Vector3.Cross(b - a, c - a) + Vector3.Cross(c - a, d - a);
            if (Vector3.Dot(facing, outward) < 0f)
            {
                int swap = q[1]; q[1] = q[3]; q[3] = swap;
                b = v[q[1]]; d = v[q[3]];
            }
            if ((a - c).sqrMagnitude <= (b - d).sqrMagnitude)
            {
                triangles.Add(q[0]); triangles.Add(q[1]); triangles.Add(q[2]);
                triangles.Add(q[0]); triangles.Add(q[2]); triangles.Add(q[3]);
            }
            else
            {
                triangles.Add(q[0]); triangles.Add(q[1]); triangles.Add(q[3]);
                triangles.Add(q[1]); triangles.Add(q[2]); triangles.Add(q[3]);
            }
        }
    }
}
