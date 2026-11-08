using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Editor
{
    /// <summary>
    /// The layout and landform of the Crystal Caves level (design map, 11-08). Metres, +Z north, sea level y = 0.
    /// Two height fields on one 4 m grid make the whole island:
    ///  * <see cref="Ground"/>: the sea floor, the rocky shelf along the south coast, the mountains and both cave floors.
    ///  * The roof: the cave ceiling (<see cref="Ceiling"/>, facing down) and the mountain top over it
    ///    (<see cref="RoofTop"/>, facing up), closed by a wall along its edge. That wall is the rock face over the cave
    ///    mouth; everywhere else it is buried inside the mountain.
    /// To reshape the level, change the numbers below and rebuild the environment
    /// (Signal Lost > Crystal Caves > Rebuild Crystal Caves Environment).
    /// </summary>
    internal static class CrystalCavesTerrain
    {
        // ---------- Size: 1.5x the 320 m Desert Dunes ----------

        public const float MapSize = 480f;
        public const int Cells = 120; // 4 m facets, like the desert
        public const float Step = MapSize / Cells;
        public const float Origin = -MapSize * 0.5f;
        public const float SeaLevel = 0f;
        private const float SeaFloor = -8f;

        // ---------- The island ----------

        public static readonly Vector2 CaveCentre = new Vector2(-5f, 22f);
        private const float CaveRadius = 184f;   // the cave fills the island under the mountains
        private const float MassifRadius = 206f; // where the mountains rise
        private const float CoastRadius = 240f;  // the shelf's cliff edge along the south coast
        // The shelf runs from west-south-west through south to east-north-east (degrees anticlockwise from east);
        // everywhere else the mountains drop straight into the sea, so they close the shelf off at both ends.
        private const float ShelfArcStart = 205f, ShelfArcLength = 165f;

        // The wide ledge at the south-east corner, with the dock, the camp and the cave mouth.
        public static readonly Vector2 LedgeCentre = new Vector2(130f, -172f);
        private static readonly Vector2 LedgeHalfSize = new Vector2(38f, 28f);
        public static readonly Vector2 Camp = new Vector2(144f, -164f);
        public const float CampHeight = 11f;
        public const float CampClearRadius = 16f; // kept open around the camp for the trophy stands of later levels
        public static readonly Vector2 DockRoot = new Vector2(124f, -197f); // the cliff edge the dock hangs off; it points south

        // ---------- The cave ----------

        public const float UpperFloor = 18f, DeepFloor = 2f;
        public static readonly Vector2 DeepCentre = new Vector2(0f, 70f);
        public static readonly Vector2 DeepRadii = new Vector2(146f, 110f);
        // Ramps down to the deep level, as directions (degrees) from its centre: by the entrance, south-west and west.
        // Everywhere else the deep level's edge is a cliff.
        public static readonly float[] RampAngles = { 300f, 225f, 175f };
        private const float RampHalfAngle = 7f;

        // The mouth: a tunnel from the shelf, through the mountains' foot, into the upper level. Its outer stretch runs
        // due north, so the portal lies on a grid line (z = PortalZ) and the roof ends cleanly there.
        public static readonly Vector2[] MouthPath = { new Vector2(118f, -148f), new Vector2(118f, -128f), new Vector2(112f, -108f), new Vector2(96f, -86f) };
        public const float PortalZ = -128f;
        private const float MouthHalfWidth = 9f, MouthHeight = 13f, NotchHalfWidth = 11f;

        // Fight zones from the design map, flattened and left empty (enemies come later).
        public static readonly Vector2 BeetleZone = new Vector2(-150f, -42f);
        public static readonly Vector2 MiniBossZone = new Vector2(80f, 35f);
        public const float ZoneRadius = 24f;
        public static readonly Vector2 FinalBossZone = new Vector2(0f, 138f);
        public static readonly Vector2 FinalBossRadii = new Vector2(76f, 34f);

        // Rises on the cave floors (x, z, radius, height) that break up the long sightlines.
        private static readonly Vector4[] Mounds =
        {
            new Vector4(-60f, 60f, 18f, 7f), new Vector4(45f, 85f, 16f, 6f), new Vector4(-100f, 110f, 14f, 8f), new Vector4(100f, 90f, 16f, 7f),
            new Vector4(-30f, 20f, 14f, 5f), new Vector4(-60f, -110f, 20f, 6f), new Vector4(20f, -120f, 16f, 5f), new Vector4(-160f, 30f, 14f, 6f),
            new Vector4(150f, -20f, 14f, 5f)
        };

        public enum Surface { ShelfRock, ShelfMoss, CoastCliff, SeaFloor, MountainRock, MountainCliff, CaveFloor, CaveDeep, CaveSand, CaveWall, CaveCeiling }

        // ---------- Maths ----------

        private static float Noise(float x, float z, float scale, float seed) =>
            (Mathf.PerlinNoise(x / scale + 37.1f + seed * 13.7f, z / scale + 71.3f + seed * 7.9f) - 0.5f) * 2f;

        // Noise around a circle, so outlines wobble without a seam.
        private static float AngleNoise(float angle, float scale, float seed) => Noise(Mathf.Cos(angle) * 100f, Mathf.Sin(angle) * 100f, scale, seed);

        // Smoothstep from a to b (b may be below a).
        private static float Smooth(float a, float b, float v)
        {
            float t = Mathf.Clamp01((v - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        private static void Polar(float x, float z, out float r, out float angle)
        {
            float dx = x - CaveCentre.x, dz = z - CaveCentre.y;
            r = Mathf.Sqrt(dx * dx + dz * dz);
            angle = Mathf.Atan2(dz, dx);
        }

        // Distance to the polyline from path[first] to path[last]; along is 0..1 along it.
        private static float DistanceToPath(Vector2 p, Vector2[] path, int first, int last, out float along)
        {
            float best = float.MaxValue, total = 0f, bestAt = 0f;
            for (int i = first; i < last; i++)
            {
                Vector2 a = path[i], ab = path[i + 1] - a;
                float length = ab.magnitude, t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / (length * length));
                float d = (p - (a + ab * t)).magnitude;
                if (d < best)
                {
                    best = d;
                    bestAt = total + t * length;
                }
                total += length;
            }
            along = total > 0f ? bestAt / total : 0f;
            return best;
        }

        /// <summary>Distance from the cave mouth's centre line, from the shelf in front of it to the cave.</summary>
        public static float DistanceToMouth(Vector2 p) => DistanceToPath(p, MouthPath, 0, MouthPath.Length - 1, out _);

        // ---------- Coast and mountains ----------

        private static float ShelfAmount(float angle)
        {
            float a = Mathf.Repeat(angle * Mathf.Rad2Deg - ShelfArcStart, 360f);
            return Smooth(0f, 18f, a) * Smooth(ShelfArcLength, ShelfArcLength - 18f, a);
        }

        private static float CoastAt(float angle) => Mathf.Lerp(MassifRadius + 3f, CoastRadius + 5f * AngleNoise(angle, 9f, 2f), ShelfAmount(angle));
        private static float MassifAt(float angle) => MassifRadius + 4f * AngleNoise(angle, 11f, 3f);

        // Signed distance to the ledge's box (negative inside).
        private static float LedgeDistance(float x, float z)
        {
            float qx = Mathf.Abs(x - LedgeCentre.x) - LedgeHalfSize.x, qz = Mathf.Abs(z - LedgeCentre.y) - LedgeHalfSize.y;
            return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qz, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qz), 0f);
        }

        /// <summary>How much of a point is dry land (0 sea, 1 land) and how far it is up the mountains (0 shelf, 1 mountain).</summary>
        public static void Coast(float x, float z, out float land, out float mountain)
        {
            Polar(x, z, out float r, out float angle);
            float coast = CoastAt(angle);
            land = Mathf.Max(Smooth(coast + 3f, coast - 3f, r + 1.5f * Noise(x, z, 7f, 4f)), Smooth(3f, -3f, LedgeDistance(x, z) + 1.2f * Noise(x, z, 6f, 9f)));
            float massif = MassifAt(angle);
            mountain = Smooth(massif + 10f, massif - 14f, r + 4f * Noise(x, z, 18f, 6f));
        }

        // The shelf: rocky ground rising from about 10 m at the sea cliffs to 18 m at the mountains' foot, in steps
        // like stacked rock blocks, with flat pads for the camp and behind the dock.
        private static float ShelfHeight(float x, float z)
        {
            Polar(x, z, out float r, out float angle);
            float inland = Smooth(CoastAt(angle), MassifAt(angle) + 10f, r);
            float h = 9.5f + 8.5f * inland + 2.2f * Noise(x, z, 26f, 7f) + Noise(x, z, 9f, 8f);
            h = Mathf.Lerp(h, Mathf.Round(h / 2f) * 2f, 0.55f);
            h = Pad(h, x, z, Camp, CampClearRadius - 2f, CampClearRadius + 8f, CampHeight);
            h = Pad(h, x, z, DockRoot + new Vector2(0f, 5f), 6f, 13f, DockBoatBuilder.DeckHeight - 0.15f);
            return h;
        }

        private static float Pad(float h, float x, float z, Vector2 centre, float inner, float outer, float height) =>
            Mathf.Lerp(h, height, Smooth(outer, inner, (new Vector2(x, z) - centre).magnitude));

        // Jagged mountains: about 60 m high where they rise from the shelf, climbing to peaks of 150 m and more toward
        // the middle of the island (the roof keeps at least 18 m of rock over the cave wherever they would dip lower).
        private static float MountainHeight(float x, float z)
        {
            Polar(x, z, out float r, out float angle);
            float inward = MassifAt(angle) - r;
            float ridge = 1f - Mathf.Abs(Noise(x, z, 46f, 21f));
            return 58f + 95f * Smooth(-10f, 150f, inward) + 22f * ridge * ridge + 16f * Noise(x, z, 85f, 22f) + 6f * Noise(x, z, 9f, 23f);
        }

        // The ground without the cave: sea floor, shelf and mountains, with the open cut in front of the mouth if asked.
        private static float Outdoor(float x, float z, bool notch)
        {
            Coast(x, z, out float land, out float mountain);
            float h = Mathf.Lerp(SeaFloor + 1.2f * Noise(x, z, 20f, 5f), ShelfHeight(x, z), land);
            h = Mathf.Lerp(h, MountainHeight(x, z), mountain);
            h += 40f * mountain * (1f - mountain) * Noise(x, z, 7f, 24f); // crags on the mountains' faces
            if (notch && z <= PortalZ + 0.01f)
            {
                float d = DistanceToPath(new Vector2(x, z), MouthPath, 0, 1, out _);
                h = Mathf.Lerp(h, MouthFloor(new Vector2(x, z)), Smooth(0f, 5f, NotchHalfWidth - d));
            }
            return h;
        }

        // ---------- The cave ----------

        /// <summary>How far a point is inside the cavern's outer wall (negative outside it).</summary>
        public static float CavernDepth(float x, float z)
        {
            Polar(x, z, out float r, out float angle);
            return CaveRadius + 5f * AngleNoise(angle, 9f, 41f) - r;
        }

        // How far a point is inside the mouth tunnel's side walls (negative outside); with the notch, the open cut in
        // front of the portal counts too.
        private static float MouthDepth(Vector2 p, bool withNotch) => MouthHalfWidth - DistanceToPath(p, MouthPath, withNotch ? 0 : 1, MouthPath.Length - 1, out _);

        // The mouth's floor slopes evenly from the shelf in front of it to the cave floor at its inner end.
        private static float MouthFloor(Vector2 p)
        {
            DistanceToPath(p, MouthPath, 0, MouthPath.Length - 1, out float along);
            return Mathf.Lerp(ShelfHeight(MouthPath[0].x, MouthPath[0].y), CavernFloor(MouthPath[3].x, MouthPath[3].y), along);
        }

        /// <summary>0 at the deep level's centre, 1 at its rim; angle is the direction from its centre in degrees.</summary>
        public static float DeepAmount(float x, float z, out float angle)
        {
            float qx = (x - DeepCentre.x) / DeepRadii.x, qz = (z - DeepCentre.y) / DeepRadii.y;
            angle = Mathf.Repeat(Mathf.Atan2(qz, qx) * Mathf.Rad2Deg, 360f);
            return Mathf.Sqrt(qx * qx + qz * qz) * (1f + 0.05f * Noise(x, z, 30f, 15f));
        }

        /// <summary>1 on a ramp's centre line between the levels, fading to 0 at its sides.</summary>
        public static float RampWeight(float angle)
        {
            float weight = 0f;
            foreach (float ramp in RampAngles) weight = Mathf.Max(weight, Smooth(RampHalfAngle + 6f, RampHalfAngle, Mathf.Abs(Mathf.DeltaAngle(angle, ramp))));
            return weight;
        }

        // The two levels: a 16 m cliff between them, eased into a long walkable slope at each ramp.
        private static float BaseFloor(float x, float z)
        {
            float upper = UpperFloor + 2f * Noise(x, z, 45f, 11f) + 0.8f * Noise(x, z, 12f, 12f);
            float deep = DeepFloor + 2.5f * Noise(x, z, 38f, 13f) + 0.8f * Noise(x, z, 10f, 14f);
            float e = DeepAmount(x, z, out float angle);
            float ramp = RampWeight(angle);
            return Mathf.Lerp(deep, upper, Smooth(Mathf.Lerp(0.95f, 0.7f, ramp), Mathf.Lerp(1f, 1.03f, ramp), e));
        }

        /// <summary>The cave floor (both levels), with mounds and the fight zones flattened.</summary>
        public static float CavernFloor(float x, float z)
        {
            float h = BaseFloor(x, z);
            foreach (Vector4 mound in Mounds)
            {
                float d = new Vector2(x - mound.x, z - mound.y).magnitude / mound.z;
                h += mound.w * Mathf.Exp(-d * d * 2.2f) * (1f + 0.15f * Noise(x, z, 6f, 17f));
            }
            var p = new Vector2(x, z);
            h = Mathf.Lerp(h, BaseFloor(BeetleZone.x, BeetleZone.y), Smooth(ZoneRadius + 12f, ZoneRadius, Vector2.Distance(p, BeetleZone)));
            h = Mathf.Lerp(h, BaseFloor(MiniBossZone.x, MiniBossZone.y), Smooth(ZoneRadius + 12f, ZoneRadius, Vector2.Distance(p, MiniBossZone)));
            float boss = new Vector2((x - FinalBossZone.x) / FinalBossRadii.x, (z - FinalBossZone.y) / FinalBossRadii.y).magnitude;
            return Mathf.Lerp(h, BaseFloor(FinalBossZone.x, FinalBossZone.y), Smooth(1.25f, 1f, boss));
        }

        private static float CaveFloor(float x, float z, float cavern, float mouth) =>
            Mathf.Lerp(CavernFloor(x, z), MouthFloor(new Vector2(x, z)), Smooth(-4f, 4f, mouth - cavern));

        /// <summary>The single ground surface: sea floor, shelf, mountains, and the cave floor with its steep walls.</summary>
        public static float Ground(float x, float z)
        {
            var p = new Vector2(x, z);
            float outside = Outdoor(x, z, true);
            float cavern = CavernDepth(x, z), mouth = MouthDepth(p, true);
            float inside = Mathf.Max(Smooth(0f, 10f, cavern), Smooth(0f, 4f, mouth));
            return inside <= 0f ? outside : Mathf.Lerp(outside, CaveFloor(x, z, cavern, mouth), inside);
        }

        /// <summary>The cave's ceiling: about 32 m over the upper level, vaulting to about 70 m over the deep level, curving
        /// down into the walls; an arch over the mouth tunnel.</summary>
        public static float Ceiling(float x, float z)
        {
            var p = new Vector2(x, z);
            float cavern = CavernDepth(x, z);
            float floor = CaveFloor(x, z, cavern, MouthDepth(p, true));
            float e = DeepAmount(x, z, out _);
            float vault = 50f + 22f * (1f - Smooth(0.6f, 1.15f, e)) + 5f * Noise(x, z, 34f, 31f) + 3f * Noise(x, z, 11f, 32f);
            float ceiling = Mathf.Lerp(floor, vault, Smooth(-2f, 24f, cavern));
            float tunnel = MouthDepth(p, false);
            if (tunnel > -2f) ceiling = Mathf.Max(ceiling, MouthFloor(p) + MouthHeight * Mathf.Sqrt(Mathf.Clamp01((tunnel + 1f) / (MouthHalfWidth + 1f))));
            return ceiling;
        }

        /// <summary>The mountain surface over the cave (always at least 18 m of rock above the ceiling).</summary>
        public static float RoofTop(float x, float z) => Mathf.Max(Outdoor(x, z, false), Ceiling(x, z) + 18f) - 0.6f;

        /// <summary>Whether the roof covers the cell around a point: the cave plus a 4 m margin buried in the mountain,
        /// north of the portal.</summary>
        public static bool UnderRoof(float x, float z) => z > PortalZ && Mathf.Max(CavernDepth(x, z), MouthDepth(new Vector2(x, z), false)) > -4f;

        // ---------- Grids and meshes ----------

        private static float[,] groundGrid, ceilingGrid, roofGrid;
        private static bool[,] roofed;

        /// <summary>Samples both height fields on the grid; call before the meshes or the exact samplers.</summary>
        public static void Generate()
        {
            groundGrid = new float[Cells + 1, Cells + 1];
            ceilingGrid = new float[Cells + 1, Cells + 1];
            roofGrid = new float[Cells + 1, Cells + 1];
            for (int i = 0; i <= Cells; i++)
                for (int j = 0; j <= Cells; j++)
                {
                    float x = Origin + i * Step, z = Origin + j * Step;
                    groundGrid[i, j] = Ground(x, z);
                    ceilingGrid[i, j] = Ceiling(x, z);
                    roofGrid[i, j] = RoofTop(x, z);
                }
            roofed = new bool[Cells, Cells];
            for (int i = 0; i < Cells; i++)
                for (int j = 0; j < Cells; j++)
                    roofed[i, j] = UnderRoof(Origin + (i + 0.5f) * Step, Origin + (j + 0.5f) * Step);
        }

        /// <summary>Exact height of the generated ground mesh (same triangles), for standing props on it.</summary>
        public static float GroundAt(float x, float z) => Sample(groundGrid, x, z);

        /// <summary>Exact height of the generated cave ceiling, for hanging props from it.</summary>
        public static float CeilingAt(float x, float z) => Sample(ceilingGrid, x, z);

        /// <summary>Whether the generated roof covers this point.</summary>
        public static bool Roofed(float x, float z)
        {
            int i = Mathf.FloorToInt((x - Origin) / Step), j = Mathf.FloorToInt((z - Origin) / Step);
            return i >= 0 && j >= 0 && i < Cells && j < Cells && roofed[i, j];
        }

        private static bool Flip(int i, int j) => ((i + j) & 1) == 0;

        private static float Sample(float[,] grid, float x, float z)
        {
            float fx = Mathf.Clamp((x - Origin) / Step, 0f, Cells - 0.0001f), fz = Mathf.Clamp((z - Origin) / Step, 0f, Cells - 0.0001f);
            int i = (int)fx, j = (int)fz;
            float u = fx - i, v = fz - j;
            float h00 = grid[i, j], h10 = grid[i + 1, j], h01 = grid[i, j + 1], h11 = grid[i + 1, j + 1];
            if (Flip(i, j)) return u >= v ? h00 + u * (h10 - h00) + v * (h11 - h10) : h00 + v * (h01 - h00) + u * (h11 - h01);
            return u + v <= 1f ? h00 + u * (h10 - h00) + v * (h01 - h00) : h11 + (1f - u) * (h01 - h11) + (1f - v) * (h10 - h11);
        }

        private static Vector3 Corner(float[,] grid, int i, int j) => new Vector3(Origin + i * Step, grid[i, j], Origin + j * Step);

        // Two triangles per cell, the diagonal alternating like the sampler above.
        private static void Cell(float[,] grid, int i, int j, System.Action<Vector3, Vector3, Vector3> tri)
        {
            Vector3 p00 = Corner(grid, i, j), p10 = Corner(grid, i + 1, j), p01 = Corner(grid, i, j + 1), p11 = Corner(grid, i + 1, j + 1);
            if (Flip(i, j)) { tri(p00, p11, p10); tri(p00, p01, p11); }
            else { tri(p00, p01, p10); tri(p10, p01, p11); }
        }

        /// <summary>The ground mesh, one submesh per surface.</summary>
        public static Mesh BuildGroundMesh(out Surface[] surfaces)
        {
            var kit = new FlatMeshKit<Surface>();
            for (int i = 0; i < Cells; i++)
                for (int j = 0; j < Cells; j++)
                {
                    bool covered = roofed[i, j];
                    Cell(groundGrid, i, j, (a, b, c) =>
                    {
                        Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                        if (n.y < 0f) n = -n;
                        kit.Tri(a, b, c, n, GroundSurface((a + b + c) / 3f, n, covered));
                    });
                }
            return Build(kit, "CrystalCavesGround", out surfaces);
        }

        private static Surface GroundSurface(Vector3 c, Vector3 n, bool covered)
        {
            if (covered && c.y < Ceiling(c.x, c.z) - 0.5f)
            {
                if (n.y < 0.62f) return Surface.CaveWall;
                if (Noise(c.x, c.z, 16f, 51f) > 0.42f) return Surface.CaveSand;
                return DeepAmount(c.x, c.z, out _) < 0.97f ? Surface.CaveDeep : Surface.CaveFloor;
            }
            if (c.y < SeaLevel + 0.4f) return Surface.SeaFloor;
            Coast(c.x, c.z, out _, out float mountain);
            if (n.y < 0.62f) return mountain > 0.35f ? Surface.MountainCliff : Surface.CoastCliff;
            if (mountain > 0.5f) return n.y < 0.82f ? Surface.MountainCliff : Surface.MountainRock;
            if (n.y < 0.86f) return Surface.ShelfRock;
            return Noise(c.x, c.z, 18f, 52f) > 0.1f ? Surface.ShelfMoss : Surface.ShelfRock;
        }

        /// <summary>The roof mesh: ceiling (facing down), mountain top (facing up) and the wall joining them along the edge.</summary>
        public static Mesh BuildRoofMesh(out Surface[] surfaces)
        {
            var kit = new FlatMeshKit<Surface>();
            for (int i = 0; i < Cells; i++)
                for (int j = 0; j < Cells; j++)
                {
                    if (!roofed[i, j]) continue;
                    Cell(ceilingGrid, i, j, (a, b, c) => kit.Tri(a, b, c, Vector3.down, Surface.CaveCeiling));
                    Cell(roofGrid, i, j, (a, b, c) =>
                    {
                        Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                        kit.Tri(a, b, c, Vector3.up, Mathf.Abs(n.y) < 0.62f ? Surface.MountainCliff : Surface.MountainRock);
                    });
                    if (i == 0 || !roofed[i - 1, j]) Skirt(kit, i, j, i, j + 1, Vector3.left);
                    if (i == Cells - 1 || !roofed[i + 1, j]) Skirt(kit, i + 1, j, i + 1, j + 1, Vector3.right);
                    if (j == 0 || !roofed[i, j - 1]) Skirt(kit, i, j, i + 1, j, Vector3.back);
                    if (j == Cells - 1 || !roofed[i, j + 1]) Skirt(kit, i, j + 1, i + 1, j + 1, Vector3.forward);
                }
            return Build(kit, "CrystalCavesRoof", out surfaces);
        }

        // A wall from the ceiling up to the mountain top along one roof edge, in rough bands so the rock face over the
        // cave mouth is craggy rather than one flat slab. Its top and bottom stay on the roof's edges; the bands
        // between are nudged by noise of their position, so neighbouring walls still meet without cracks.
        private static void Skirt(FlatMeshKit<Surface> kit, int i0, int j0, int i1, int j1, Vector3 outward)
        {
            Vector3 b0 = Corner(ceilingGrid, i0, j0), b1 = Corner(ceilingGrid, i1, j1), t0 = Corner(roofGrid, i0, j0), t1 = Corner(roofGrid, i1, j1);
            const int bands = 8; // the same for every wall, so walls sharing an edge split it alike
            Vector3 Point(Vector3 bottom, Vector3 top, int band)
            {
                Vector3 p = Vector3.Lerp(bottom, top, band / (float)bands);
                if (band == 0 || band == bands) return p;
                return p + new Vector3(Noise(p.x + p.y, p.z, 3f, 61f), 0f, Noise(p.x, p.z - p.y, 3f, 62f)) * 1.6f;
            }
            for (int band = 0; band < bands; band++)
                kit.Quad(Point(b0, t0, band), Point(b1, t1, band), Point(b1, t1, band + 1), Point(b0, t0, band + 1), outward, Surface.MountainCliff);
        }

        private static Mesh Build(FlatMeshKit<Surface> kit, string name, out Surface[] surfaces)
        {
            Mesh mesh = kit.Build(name);
            surfaces = new Surface[mesh.subMeshCount];
            for (int s = 0; s < surfaces.Length; s++) surfaces[s] = FlatMeshKit<Surface>.MaterialOf(mesh, s);
            return mesh;
        }

        /// <summary>
        /// The sea: a grid over the map that widens out to the horizon (one grid, so its waves never crack apart). Each
        /// vertex's red colour is how close it is to the shore, which the stylized water shader turns into shallow
        /// colour and foam along the cliffs.
        /// </summary>
        public static Mesh BuildOceanMesh()
        {
            var coords = new List<float>();
            for (float c = Origin; c <= -Origin + 0.01f; c += 8f) coords.Add(c);
            for (float c = -Origin, cell = 8f; c < 4000f; ) { cell *= 1.35f; c += cell; coords.Add(c); coords.Insert(0, -c); }
            int n = coords.Count;
            var vertices = new List<Vector3>(n * n);
            var colours = new List<Color>(n * n);
            var wet = new bool[n, n];
            for (int i = 0; i < n; i++)
                for (int k = 0; k < n; k++)
                {
                    float x = coords[i], z = coords[k];
                    bool onMap = Mathf.Abs(x) <= -Origin && Mathf.Abs(z) <= -Origin;
                    float ground = onMap ? GroundAt(x, z) : SeaFloor;
                    wet[i, k] = ground < SeaLevel + 0.5f && !(onMap && Roofed(x, z)); // the deep cave floor dips below sea level, but stays dry
                    vertices.Add(new Vector3(x, 0f, z));
                    colours.Add(new Color(Mathf.Clamp01(1f - (SeaLevel - ground) / 5f), 0f, 0f, 1f));
                }
            var triangles = new List<int>();
            for (int i = 0; i < n - 1; i++)
                for (int k = 0; k < n - 1; k++)
                {
                    if (!wet[i, k] && !wet[i + 1, k] && !wet[i, k + 1] && !wet[i + 1, k + 1]) continue;
                    int a = i * n + k, b = (i + 1) * n + k, c = (i + 1) * n + k + 1, d = i * n + k + 1;
                    triangles.AddRange(new[] { a, d, c, a, c, b });
                }
            var mesh = new Mesh { name = "CrystalCavesOcean", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetColors(colours);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------- Self-check ----------

        /// <summary>Checks the generated shape (call after <see cref="Generate"/>); returns the problems found.</summary>
        public static List<string> Check()
        {
            var problems = new List<string>();
            float thinnest = float.MaxValue, lowest = float.MaxValue;
            for (int i = 0; i <= Cells; i++)
                for (int j = 0; j <= Cells; j++)
                {
                    float x = Origin + i * Step, z = Origin + j * Step;
                    if (UnderRoof(x, z)) thinnest = Mathf.Min(thinnest, roofGrid[i, j] - ceilingGrid[i, j]);
                    if (CavernDepth(x, z) > 30f) lowest = Mathf.Min(lowest, ceilingGrid[i, j] - groundGrid[i, j]);
                }
            if (thinnest < 12f) problems.Add($"Roof rock is only {thinnest:0.0} m thick somewhere.");
            if (lowest < 15f) problems.Add($"Cave headroom drops to {lowest:0.0} m somewhere.");

            // Each ramp, walked down its centre line, stays gentle enough to walk (and for the NavMesh's 45 degrees).
            foreach (float ramp in RampAngles)
            {
                Vector2 dir = new Vector2(Mathf.Cos(ramp * Mathf.Deg2Rad) * DeepRadii.x, Mathf.Sin(ramp * Mathf.Deg2Rad) * DeepRadii.y);
                Vector2 previous = DeepCentre + dir * 0.6f;
                float steepest = 0f;
                for (float e = 0.61f; e <= 1.12f; e += 0.01f)
                {
                    Vector2 p = DeepCentre + dir * e;
                    float rise = Mathf.Abs(GroundAt(p.x, p.y) - GroundAt(previous.x, previous.y)), run = (p - previous).magnitude;
                    steepest = Mathf.Max(steepest, Mathf.Atan2(rise, run) * Mathf.Rad2Deg);
                    previous = p;
                }
                if (steepest > 38f) problems.Add($"Ramp at {ramp} degrees is {steepest:0} degrees steep.");
            }
            // Walking headroom all the way through the mouth tunnel.
            for (float t = 0f; t <= 1f; t += 0.05f)
            {
                Vector2 p = PointOnPath(t);
                if (p.y > PortalZ + 2f && CeilingAt(p.x, p.y) - GroundAt(p.x, p.y) < 8f) problems.Add($"Mouth tunnel too low at ({p.x:0}, {p.y:0}).");
            }
            // The island stays inside the map, with sea all round.
            foreach (Vector2 edge in new[] { new Vector2(0f, Origin + 2f), new Vector2(0f, -Origin - 2f), new Vector2(Origin + 2f, 0f), new Vector2(-Origin - 2f, 0f) })
                if (GroundAt(edge.x, edge.y) > SeaLevel - 2f) problems.Add($"Land reaches the map edge at ({edge.x:0}, {edge.y:0}).");
            return problems;
        }

        /// <summary>A point along the mouth's centre line, 0 on the shelf in front of it, 1 at its inner end.</summary>
        public static Vector2 PointOnPath(float t)
        {
            float total = 0f;
            for (int i = 0; i < MouthPath.Length - 1; i++) total += (MouthPath[i + 1] - MouthPath[i]).magnitude;
            float target = t * total;
            for (int i = 0; i < MouthPath.Length - 1; i++)
            {
                float length = (MouthPath[i + 1] - MouthPath[i]).magnitude;
                if (target <= length) return Vector2.Lerp(MouthPath[i], MouthPath[i + 1], target / length);
                target -= length;
            }
            return MouthPath[MouthPath.Length - 1];
        }
    }
}
