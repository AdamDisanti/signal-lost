using System.Collections.Generic;
using UnityEngine;

namespace SignalLost.Editor
{
    /// <summary>
    /// The layout and landform of the Crystal Caves level (design map, 11-08; revised 11-09). Metres, +Z north, sea level y = 0.
    /// Two height fields on one 4 m grid make the whole island:
    ///  * <see cref="Ground"/>: the sea floor, the rocky ledge at the south-east corner, the mountains and both cave floors.
    ///    It is built as two meshes: the land and the sea floor beyond the coast (which is not walkable, like the other
    ///    islands').
    ///  * The roof: the cave ceiling (<see cref="Ceiling"/>, facing down) and the mountain top over it
    ///    (<see cref="RoofTop"/>, facing up), closed by a wall along its edge. That wall is the rock face over the cave
    ///    mouth; everywhere else it is buried inside the mountain.
    /// The sea, the dock and the boat come from the shared <see cref="IslandKit"/>.
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
        private const float CoastRadius = 232f;  // the cliff edge where there is ground between the mountains and the sea
        // The only open ground is around the south-east ledge; everywhere else the mountains drop straight into the sea,
        // so they close the outside off (degrees anticlockwise from east, around the cave's centre).
        private const float ShelfArcStart = 278f, ShelfArcLength = 60f;

        // The ledge at the south-east corner, with the camp, the cave mouth and the cove down to the dock.
        public static readonly Vector2 LedgeCentre = new Vector2(130f, -172f);
        private static readonly Vector2 LedgeHalfSize = new Vector2(38f, 28f);
        public static readonly Vector2 Camp = new Vector2(150f, -166f);
        public const float CampHeight = 11f;
        public const float CampClearRadius = 16f; // kept open around the camp for the trophy stands of later levels

        // The cove: rocks stepping down from the ledge to the water, where the dock starts. It points at the cave mouth,
        // so the mouth can be seen from the dock up the cove.
        public static readonly Vector2 CoveTop = new Vector2(126.7f, -180.4f), CoveBottom = new Vector2(140f, -216f);
        private const float CoveHalfWidth = 9f, CoveTopHeight = 8f;
        /// <summary>The way the dock runs out to sea (straight on from the cove).</summary>
        public static Vector2 DockSeaward => (CoveBottom - CoveTop).normalized;

        // ---------- The cave ----------

        public const float UpperFloor = 18f, DeepFloor = 2f;
        // The upper level is only the front of the cave (south of FrontZ) and a lobe round the beetles' fight zone. The
        // back is all deep level, so nothing up there overlooks mini boss 2's or the final boss's zone.
        public const float FrontZ = -55f;
        public const float BeetleLobeRadius = 62f;
        /// <summary>A ramp down from the upper level: where it leaves the upper edge, and the way down.</summary>
        public struct Ramp
        {
            public Vector2 Edge, Down;
            public Ramp(Vector2 edge, Vector2 down) { Edge = edge; Down = down.normalized; }
            public Vector2 Top => Edge - Down * 4f;
            public Vector2 Foot => Edge + Down * (RampLength + 2f);
        }
        // The only way down that leads back up: on the far (north) side of the beetles' zone, so it is found after the
        // beetle fight. Everywhere else the upper level's edge is a 16 m cliff, too steep to climb; jumping down is fine.
        // (Written out: static fields set up in order, and BeetleZone, at (-150, -42), is declared further down.)
        public static readonly Ramp[] Ramps = { new Ramp(new Vector2(-150f, -42f + BeetleLobeRadius), Vector2.up) };
        public const float RampHalfWidth = 14f, RampLength = 72f; // a long, even incline of about 12 degrees

        // The mouth: a tunnel from the ledge, through the mountains' foot, into the upper level. It is roofed right out to
        // the cliff face (z = PortalZ, a grid line, as far as the face runs across the mouth at a slant), where a lintel
        // slab spans the opening (see CliffBlocks).
        public static readonly Vector2[] MouthPath = { new Vector2(118f, -148f), new Vector2(118f, -128f), new Vector2(112f, -108f), new Vector2(96f, -86f) };
        public const float PortalZ = -148f;
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
            new Vector4(-30f, 20f, 14f, 5f), new Vector4(-60f, -110f, 20f, 6f), new Vector4(20f, -120f, 16f, 5f), new Vector4(-92f, 78f, 14f, 6f),
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

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b, out float t)
        {
            Vector2 ab = b - a;
            t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>Distance from the cave mouth's centre line, from the ledge in front of it to the cave.</summary>
        public static float DistanceToMouth(Vector2 p) => DistanceToPath(p, MouthPath, 0, MouthPath.Length - 1, out _);

        /// <summary>Distance from the cove's centre line, from the top of the rocks to the water.</summary>
        public static float DistanceToCove(Vector2 p) => DistanceToSegment(p, CoveTop, CoveBottom, out _);

        // ---------- Coast and mountains ----------

        private static float ShelfAmount(float angle)
        {
            float a = Mathf.Repeat(angle * Mathf.Rad2Deg - ShelfArcStart, 360f);
            return Smooth(0f, 12f, a) * Smooth(ShelfArcLength, ShelfArcLength - 12f, a);
        }

        private static float CoastAt(float angle) => Mathf.Lerp(MassifRadius + 3f, CoastRadius + 4f * AngleNoise(angle, 9f, 2f), ShelfAmount(angle));
        private static float MassifAt(float angle) => MassifRadius + 4f * AngleNoise(angle, 11f, 3f);

        // Signed distance to the ledge's box (negative inside).
        private static float LedgeDistance(float x, float z)
        {
            float qx = Mathf.Abs(x - LedgeCentre.x) - LedgeHalfSize.x, qz = Mathf.Abs(z - LedgeCentre.y) - LedgeHalfSize.y;
            return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qz, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qz), 0f);
        }

        // How much of a point is the cove (1 on its rocks, 0 off them), and how far down it it is (0 top, 1 bottom).
        private static float CoveAmount(float x, float z, out float down)
        {
            float d = DistanceToSegment(new Vector2(x, z), CoveTop, CoveBottom + DockSeaward * 4f, out down);
            down *= ((CoveBottom + DockSeaward * 4f) - CoveTop).magnitude / (CoveBottom - CoveTop).magnitude;
            return Smooth(CoveHalfWidth + 2f, CoveHalfWidth - 2f, d + 1.2f * Noise(x, z, 5f, 33f));
        }

        /// <summary>How much of a point is dry land (0 sea, 1 land) and how far it is up the mountains (0 ledge, 1 mountain).</summary>
        public static void Coast(float x, float z, out float land, out float mountain)
        {
            CliffTop(x, z, out land, out mountain);
            land = Mathf.Max(land, CoveAmount(x, z, out _));
        }

        // The same, leaving out the cove: the ledge and its sea cliffs.
        private static void CliffTop(float x, float z, out float land, out float mountain)
        {
            Polar(x, z, out float r, out float angle);
            float coast = CoastAt(angle);
            land = Mathf.Max(Smooth(coast + 3f, coast - 3f, r + 1.5f * Noise(x, z, 7f, 4f)), Smooth(3f, -3f, LedgeDistance(x, z) + 1.2f * Noise(x, z, 6f, 9f)));
            // The mountains rise as a steep bluff over about 10 m, too steep to stand on (or climb by jumping), from
            // just above the ledge.
            float massif = MassifAt(angle);
            mountain = Smooth(massif + 6f, massif - 4f, r + 4f * Noise(x, z, 18f, 6f));
        }

        // The ledge: rocky ground rising gently from about 10 m at the sea cliffs to 13.5 m at the mountains' foot, in soft steps like
        // stacked rock blocks, with a flat pad for the camp and lower ground at the top of the cove.
        private static float ShelfHeight(float x, float z)
        {
            Polar(x, z, out float r, out float angle);
            float inland = Smooth(CoastAt(angle), MassifAt(angle) + 10f, r);
            float h = 9.5f + 4f * inland + 2.2f * Noise(x, z, 26f, 7f) + Noise(x, z, 9f, 8f);
            // Soft terraces, 2 m apart: each step is a rounded rise, gentle enough to walk up anywhere.
            float level = Mathf.Floor(h / 2f);
            h = Mathf.Lerp(h, (level + Smooth(0.25f, 0.75f, h / 2f - level)) * 2f, 0.35f);
            h = Pad(h, x, z, CoveTop, 8f, 36f, CoveTopHeight); // eased over a wide circle, so the ledge rises gently from the cove
            // The camp's pad goes last, so nothing else tilts it: the whole camp stands on level ground.
            return Pad(h, x, z, Camp, CampClearRadius - 2f, CampClearRadius + 8f, CampHeight);
        }

        // The cove's rocks: from the top of the cove (0) down to the water's edge (1), then on under the water, in low
        // uneven steps.
        private static float CoveHeight(float x, float z, float down)
        {
            float length = (CoveBottom - CoveTop).magnitude;
            float h = Mathf.Lerp(CoveTopHeight, 0.55f, Mathf.Clamp01(down)) - Mathf.Max(0f, down - 1f) * length * 0.6f;
            h += 0.35f * Noise(x, z, 4f, 34f);
            return Mathf.Lerp(h, Mathf.Round(h / 0.8f) * 0.8f, 0.35f);
        }

        private static float Pad(float h, float x, float z, Vector2 centre, float inner, float outer, float height) =>
            Mathf.Lerp(h, height, Smooth(outer, inner, (new Vector2(x, z) - centre).magnitude));

        // The rock the cave was carved from: the jagged peaks the island had before its sea cliffs (11-09). Only the
        // cave walls use it, so they stay exactly as they were; outside, the sea cliffs below replace it.
        private static float CarvedRockHeight(float x, float z)
        {
            Polar(x, z, out float r, out float angle);
            float inward = MassifAt(angle) - r;
            float ridge = 1f - Mathf.Abs(Noise(x, z, 46f, 21f));
            return 58f + 95f * Smooth(-10f, 150f, inward) + 22f * ridge * ridge + 16f * Noise(x, z, 85f, 22f) + 6f * Noise(x, z, 9f, 23f);
        }

        // The sea cliffs' flat, grassy tops: about 100 m up, stepping up in great blocks 12 m apart toward the middle of the
        // island. Always above the roof the cave needs, so the tops stay flat. Their faces are the stone blocks of
        // CliffBlocks, standing in front.
        private static float CliffHeight(float x, float z)
        {
            Polar(x, z, out float r, out float angle);
            float inward = MassifAt(angle) - r;
            float tiers = inward / 70f + 0.6f * Noise(x, z, 95f, 21f);
            float level = Mathf.Floor(tiers);
            float block = Mathf.Clamp(level + Smooth(0.88f, 1f, tiers - level), 0f, 2f);
            return 102f + 12f * block + 1.5f * Noise(x, z, 28f, 22f) + 0.6f * Noise(x, z, 8f, 23f);
        }

        // The cave mouth as seen from the foot of the cove: a sight line the ledge in between is kept under.
        private static readonly Vector3 SightFrom = new Vector3(CoveBottom.x, 2.6f, CoveBottom.y), SightTo = new Vector3(118f, 25f, PortalZ);

        // The ground without the cave: sea floor, ledge, cove and mountains, with the open cut in front of the mouth if asked.
        // carved: the old jagged rock in place of the sea cliffs (for the cave walls only).
        private static float Outdoor(float x, float z, bool notch, bool carved = false)
        {
            CliffTop(x, z, out float land, out float mountain);
            float cove = CoveAmount(x, z, out float down);
            float h = Mathf.Lerp(SeaFloor + 1.2f * Noise(x, z, 20f, 5f), ShelfHeight(x, z), land);
            h = Mathf.Lerp(h, CoveHeight(x, z, down), cove); // the cove cuts down through the ledge and out into the sea
            // Keep the ledge below the line from the dock to the cave mouth, so the mouth is in view from the dock.
            float off = DistanceToSegment(new Vector2(x, z), new Vector2(SightFrom.x, SightFrom.z), new Vector2(SightTo.x, SightTo.z), out float t);
            float sight = Mathf.Lerp(SightFrom.y, SightTo.y, t) - 1.5f;
            // (Faded out toward the mouth, so it never leaves a step in the ground.)
            h = Mathf.Lerp(h, Mathf.Min(h, sight), Smooth(10f, 5f, off) * Smooth(0.85f, 0.55f, t) * (1f - mountain));
            if (carved)
            {
                h = Mathf.Lerp(h, CarvedRockHeight(x, z), mountain);
                h += 12f * mountain * (1f - mountain) * Noise(x, z, 7f, 24f); // crags on the faces (small, so they make no ledges)
            }
            else
            {
                // The ground only rises behind the cliff blocks' deepest terrace (about 15-20 m behind the foot), so it
                // stays hidden inside them; the blocks make the face.
                Polar(x, z, out float r, out float angle);
                float massif = MassifAt(angle);
                h = Mathf.Lerp(h, CliffHeight(x, z), Smooth(massif - 9f, massif - 14f, r + 4f * Noise(x, z, 18f, 6f)));
            }
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

        // The mouth's floor slopes evenly from the ledge in front of it to the cave floor at its inner end.
        private static float MouthFloor(Vector2 p)
        {
            DistanceToPath(p, MouthPath, 0, MouthPath.Length - 1, out float along);
            return Mathf.Lerp(ShelfHeight(MouthPath[0].x, MouthPath[0].y), CavernFloor(MouthPath[3].x, MouthPath[3].y), along);
        }

        /// <summary>How far a point is inside the upper level's edge (negative: out over the deep level).</summary>
        public static float UpperDistance(float x, float z) => Mathf.Max(FrontEdge(x, z), LobeEdge(x, z));

        // The two parts of the upper level: the front of the cave, and the lobe round the beetles' zone.
        private static float FrontEdge(float x, float z) => FrontZ + 4f * Noise(x, 0f, 35f, 15f) - z;

        private static float LobeEdge(float x, float z)
        {
            Vector2 fromLobe = new Vector2(x, z) - BeetleZone;
            return BeetleLobeRadius + 4f * AngleNoise(Mathf.Atan2(fromLobe.y, fromLobe.x), 9f, 16f) - fromLobe.magnitude;
        }

        /// <summary>1 on a ramp's centre line between the levels, fading to 0 at its sides and ends.</summary>
        public static float RampWeight(float x, float z)
        {
            float weight = 0f;
            foreach (Ramp ramp in Ramps)
            {
                Vector2 d = new Vector2(x, z) - ramp.Edge;
                float along = Vector2.Dot(d, ramp.Down), across = Mathf.Abs(d.x * ramp.Down.y - d.y * ramp.Down.x);
                weight = Mathf.Max(weight, Smooth(RampHalfWidth + 6f, RampHalfWidth, across) * Smooth(-18f, -10f, along) * Smooth(RampLength + 12f, RampLength + 4f, along));
            }
            return weight;
        }

        // The two levels: a 16 m cliff between them, eased into a long walkable slope at each ramp.
        private static float BaseFloor(float x, float z)
        {
            float upper = UpperFloor + 2f * Noise(x, z, 45f, 11f) + 0.8f * Noise(x, z, 12f, 12f);
            float deep = DeepFloor + 2.5f * Noise(x, z, 38f, 13f) + 0.8f * Noise(x, z, 10f, 14f);
            float ramp = RampWeight(x, z);
            // The cliff drops within about 3 m, so even on the 4 m terrain grid every face of it is steeper than the
            // player's 55 degree footing limit.
            // On the ramp the floor falls evenly (a straight incline, so no part of it is steeper than the rest).
            float edge = UpperDistance(x, z);
            float cliff = Smooth(-1.5f, 1.5f, edge), incline = Mathf.Clamp01((edge + RampLength) / (RampLength + 4f));
            return Mathf.Lerp(deep, upper, Mathf.Lerp(cliff, incline, ramp));
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

        /// <summary>The single ground surface: sea floor, ledge, mountains, and the cave floor with its steep walls.</summary>
        public static float Ground(float x, float z)
        {
            var p = new Vector2(x, z);
            float outside = Outdoor(x, z, true);
            float cavern = CavernDepth(x, z), mouth = MouthDepth(p, true);
            float inside = Mathf.Max(Smooth(0f, 10f, cavern), Smooth(0f, 4f, mouth));
            if (!UnderRoof(x, z)) return inside <= 0f ? outside : Mathf.Lerp(outside, CaveFloor(x, z, cavern, mouth), inside);
            // Under the roof the walls are carved from the old rock, so the cave stays as it was; above the ceiling
            // they are kept inside the roof (the roof is always 17 m or more over the ceiling, so no visible wall moves).
            return Mathf.Min(Mathf.Lerp(Outdoor(x, z, true, true), CaveFloor(x, z, cavern, mouth), inside), Mathf.Max(RoofTop(x, z) - 1f, Ceiling(x, z))); // (never down below the ceiling: solid rock stays solid)
        }

        /// <summary>The cave's ceiling: about 32 m over the upper level, vaulting to about 70 m over the deep level, curving
        /// down into the walls; an arch over the mouth tunnel.</summary>
        public static float Ceiling(float x, float z)
        {
            var p = new Vector2(x, z);
            float cavern = CavernDepth(x, z);
            float floor = CaveFloor(x, z, cavern, MouthDepth(p, true));
            float deep = 1f - Smooth(-25f, 10f, UpperDistance(x, z));
            float vault = 50f + 22f * deep + 5f * Noise(x, z, 34f, 31f) + 3f * Noise(x, z, 11f, 32f);
            float ceiling = Mathf.Lerp(floor, vault, Smooth(-2f, 24f, cavern));
            float tunnel = MouthDepth(p, true);
            if (tunnel > -2f) ceiling = Mathf.Max(ceiling, MouthFloor(p) + MouthHeight * Mathf.Sqrt(Mathf.Clamp01((tunnel + 1f) / (MouthHalfWidth + 1f))));
            return ceiling;
        }

        /// <summary>The mountain surface over the cave (always at least 18 m of rock above the ceiling).</summary>
        public static float RoofTop(float x, float z) => Mathf.Max(Outdoor(x, z, false), Ceiling(x, z) + 18f) - 0.6f;

        /// <summary>Whether the roof covers the cell around a point: the whole cavern plus a 4 m margin buried in the
        /// mountain, and the mouth tunnel out to the cliff face.</summary>
        public static bool UnderRoof(float x, float z) => CavernDepth(x, z) > -4f || InTunnel(x, z, -4f);

        // The tunnel's roofed stretch, with a margin past its side walls: out to the portal line, but only behind the
        // cliffs' foot, so the roof's edge never stands out in front of the cliff face.
        private static bool InTunnel(float x, float z, float margin)
        {
            if (z <= PortalZ || MouthDepth(new Vector2(x, z), true) <= margin) return false;
            CliffTop(x, z, out _, out float mountain);
            return mountain >= 0.3f; // a cell's edge then stays behind the foot, so its skirt is hidden by the slabs
        }

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

        // Sea floor: a cell out past the coast whose every corner is well under the water.
        private static bool IsSeabed(int i, int j) =>
            !roofed[i, j] && Mathf.Max(Mathf.Max(groundGrid[i, j], groundGrid[i + 1, j]), Mathf.Max(groundGrid[i, j + 1], groundGrid[i + 1, j + 1])) < SeaLevel - 0.6f;

        /// <summary>The land mesh (one submesh per surface) and the sea floor mesh beyond the coast.</summary>
        public static Mesh BuildGroundMesh(out Surface[] surfaces, out Mesh seabed)
        {
            var kit = new FlatMeshKit<Surface>();
            var floor = new FlatMeshKit<Surface>();
            for (int i = 0; i < Cells; i++)
                for (int j = 0; j < Cells; j++)
                {
                    bool covered = roofed[i, j], sea = IsSeabed(i, j);
                    Cell(groundGrid, i, j, (a, b, c) =>
                    {
                        Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                        if (n.y < 0f) n = -n;
                        if (sea) floor.Tri(a, b, c, n, Surface.SeaFloor);
                        else kit.Tri(a, b, c, n, GroundSurface((a + b + c) / 3f, n, covered));
                    });
                }
            seabed = floor.Build("CrystalCavesSeabed");
            return Build(kit, "CrystalCavesGround", out surfaces);
        }

        private static Surface GroundSurface(Vector3 c, Vector3 n, bool covered)
        {
            if (covered && c.y < Ceiling(c.x, c.z) - 0.5f)
            {
                if (n.y < 0.62f) return Surface.CaveWall;
                if (Noise(c.x, c.z, 16f, 51f) > 0.42f) return Surface.CaveSand;
                return UpperDistance(c.x, c.z) < -2f ? Surface.CaveDeep : Surface.CaveFloor;
            }
            if (c.y < SeaLevel + 0.4f) return Surface.SeaFloor;
            Coast(c.x, c.z, out _, out float mountain);
            if (n.y < 0.62f) return mountain > 0.08f ? Surface.MountainCliff : Surface.CoastCliff; // the sea cliffs' rock right down to their foot
            if (mountain > 0.08f) return CliffSurface(c, n);
            if (n.y < 0.86f) return Surface.ShelfRock;
            return Noise(c.x, c.z, 18f, 52f) > 0.1f ? Surface.ShelfMoss : Surface.ShelfRock;
        }

        // The sea cliffs: rock faces, and grass over most of their flat tops, like the reference.
        private static Surface CliffSurface(Vector3 c, Vector3 n)
        {
            if (Mathf.Abs(n.y) < 0.82f) return Surface.MountainCliff;
            return Mathf.Abs(n.y) > 0.93f && Noise(c.x, c.z, 22f, 53f) > -0.35f ? Surface.ShelfMoss : Surface.MountainRock;
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
                        kit.Tri(a, b, c, Vector3.up, CliffSurface((a + b + c) / 3f, Vector3.Cross(b - a, c - a).normalized));
                    });
                    if (i == 0 || !roofed[i - 1, j]) Skirt(kit, i, j, i, j + 1, Vector3.left);
                    if (i == Cells - 1 || !roofed[i + 1, j]) Skirt(kit, i + 1, j, i + 1, j + 1, Vector3.right);
                    if (j == 0 || !roofed[i, j - 1]) Skirt(kit, i, j, i + 1, j, Vector3.back);
                    if (j == Cells - 1 || !roofed[i, j + 1]) Skirt(kit, i, j + 1, i + 1, j + 1, Vector3.forward);
                }
            return Build(kit, "CrystalCavesRoof", out surfaces);
        }

        // A flat wall from the ceiling up to the mountain top along one roof edge, closing the roof. It is always hidden:
        // buried in the mountain round the cavern, and behind the cliff slabs and the mouth's arch at the cave mouth.
        private static void Skirt(FlatMeshKit<Surface> kit, int i0, int j0, int i1, int j1, Vector3 outward) =>
            kit.Quad(Corner(ceilingGrid, i0, j0), Corner(ceilingGrid, i1, j1), Corner(roofGrid, i1, j1), Corner(roofGrid, i0, j0), outward, Surface.MountainCliff);

        private static Mesh Build(FlatMeshKit<Surface> kit, string name, out Surface[] surfaces)
        {
            Mesh mesh = kit.Build(name);
            surfaces = new Surface[mesh.subMeshCount];
            for (int s = 0; s < surfaces.Length; s++) surfaces[s] = FlatMeshKit<Surface>.MaterialOf(mesh, s);
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

            // The roof covers every bit of the cave: no cell inside the cavern's walls (or the tunnel, north of the
            // portal) may be open to the sky.
            int open = 0;
            for (int i = 0; i < Cells; i++)
                for (int j = 0; j < Cells; j++)
                {
                    float x = Origin + (i + 0.5f) * Step, z = Origin + (j + 0.5f) * Step;
                    bool cave = CavernDepth(x, z) > -2f || InTunnel(x, z, -2f);
                    if (cave && !roofed[i, j]) open++;
                }
            if (open > 0) problems.Add($"{open} cave cells are open to the sky.");

            // Each ramp, walked from its top to its foot, stays gentle enough to walk (and for the NavMesh's 45 degrees).
            foreach (Ramp ramp in Ramps)
            {
                float steepest = 0f;
                for (float d = 0f; d < (ramp.Foot - ramp.Top).magnitude; d += 1f)
                {
                    Vector2 a = ramp.Top + ramp.Down * d, b = a + ramp.Down;
                    steepest = Mathf.Max(steepest, Mathf.Atan2(Mathf.Abs(GroundAt(b.x, b.y) - GroundAt(a.x, a.y)), 1f) * Mathf.Rad2Deg);
                }
                if (steepest > 20f) problems.Add($"The ramp at ({ramp.Edge.x:0}, {ramp.Edge.y:0}) is {steepest:0} degrees steep.");
                if (GroundAt(ramp.Top.x, ramp.Top.y) - GroundAt(ramp.Foot.x, ramp.Foot.y) < 10f) problems.Add($"The ramp at ({ramp.Edge.x:0}, {ramp.Edge.y:0}) doesn't lead down to the deep level.");
            }
            // The beetles' zone is on the upper level, well back from its edge, so nobody falls (or is knocked) off.
            for (int a = 0; a < 36; a++)
            {
                Vector2 p = BeetleZone + new Vector2(Mathf.Cos(a * 10f * Mathf.Deg2Rad), Mathf.Sin(a * 10f * Mathf.Deg2Rad)) * (ZoneRadius + 10f);
                if (CavernDepth(p.x, p.y) > 10f && UpperDistance(p.x, p.y) < 6f) { problems.Add("The beetles' zone comes within 10 m of the upper level's edge."); break; }
            }
            // No upper level overlooks mini boss 2's zone or the final boss's zone.
            if (UpperDistance(MiniBossZone.x, MiniBossZone.y) > -(ZoneRadius + 40f)) problems.Add("The upper level comes too close to mini boss 2's zone.");
            for (int a = 0; a < 36; a++)
            {
                Vector2 p = FinalBossZone + new Vector2(Mathf.Cos(a * 10f * Mathf.Deg2Rad) * FinalBossRadii.x, Mathf.Sin(a * 10f * Mathf.Deg2Rad) * FinalBossRadii.y);
                if (UpperDistance(p.x, p.y) > -40f) { problems.Add("The upper level comes too close to the final boss's zone."); break; }
            }
            // Nothing can be climbed. The player can jump up any slope that isn't vertical, so only the vertical barrier
            // walls stop them. Treating all ground as climbable and the walls as solid: from the foot of the cove no one
            // gets up the mountains, and from the deep level no one gets back up except by the ramp.
            var blocked = new bool[Cells, Cells];
            List<Barrier> walls = Barriers();
            // That needs every outside wall to rise well clear of the ground in front of it (the player reaches about
            // 3.5 m up); the cell flood below can't see a slope leaning on a wall, so check it here.
            string lowWall = null;
            foreach (Barrier wall in walls)
                foreach (BarrierPoint p in wall.Points)
                    for (float d = 0.5f; !wall.InCave && lowWall == null && d <= 4f; d += 0.5f)
                        if (GroundAt(p.Foot.x + p.Out.x * d, p.Foot.y + p.Out.y * d) > p.Top - 5f) lowWall = $"({p.Foot.x:0}, {p.Foot.y:0})";
            // The same for the cliff blocks' lowest ledges: out of reach of the ground (or the shallows) in front.
            foreach (CliffBlock block in CliffBlocks())
                for (float s = 0f; lowWall == null && s <= 1f; s += 0.25f)
                    for (float d = 0.5f; lowWall == null && d <= 4f; d += 0.5f)
                    {
                        Vector2 q = Vector2.Lerp(block.A, block.B, s) + block.Out * d;
                        if (Mathf.Max(GroundAt(q.x, q.y), SeaLevel - WadeDepth) > block.Tiers[0].Top - 5f) lowWall = $"({q.x:0}, {q.y:0}) (a cliff block)";
                    }
            if (lowWall != null) problems.Add($"The ground in front of the wall at {lowWall} comes within 5 m of its top.");
            foreach (Barrier wall in walls)
                for (int k = 0; k < wall.Points.Count - 1; k++)
                {
                    BarrierPoint a = wall.Points[k], b = wall.Points[k + 1];
                    for (int i = 0; i < Cells; i++)
                        for (int j = 0; j < Cells; j++)
                        {
                            if (blocked[i, j]) continue;
                            var c = new Vector2(Origin + (i + 0.5f) * Step, Origin + (j + 0.5f) * Step);
                            if (Mathf.Abs(c.x - a.Foot.x) > 12f || Mathf.Abs(c.y - a.Foot.y) > 12f) continue;
                            // A cell whose centre is within 2.9 m of the wall, or under a cliff's rock top, is wall. (2.9 m
                            // is wider than half a cell's diagonal, so the cells along a wall leave no diagonal gaps.)
                            blocked[i, j] = DistanceToSegment(c, a.Foot, b.Foot, out _) < 2.9f || (wall.Capped && InQuad(c, a.Foot, b.Foot, b.Back, a.Back));
                        }
                }
            // The cliff slabs are solid too (they close the mountains' wall at the cave mouth); the lintel over the
            // doorway is the way in, so it doesn't count.
            foreach (CliffBlock block in CliffBlocks())
            {
                if (block.Lintel) continue;
                Vector2 aBack = block.A + block.BackA, bBack = block.B + block.BackB;
                for (int i = 0; i < Cells; i++)
                    for (int j = 0; j < Cells; j++)
                    {
                        var c = new Vector2(Origin + (i + 0.5f) * Step, Origin + (j + 0.5f) * Step);
                        if (!blocked[i, j] && Mathf.Abs(c.x - block.A.x) < 24f && Mathf.Abs(c.y - block.A.y) < 24f)
                            blocked[i, j] = DistanceToSegment(c, block.A, block.B, out _) < 2.9f || InQuad(c, block.A, block.B, bBack, aBack);
                    }
            }
            // Inside the cave only the open space under the ceiling counts (no walking through the roof).
            System.Func<int, int, bool> passable = (i, j) =>
            {
                if (blocked[i, j]) return false;
                // The player wades until their feet are WadeDepth under: only a cell deeper than that all over stops them.
                if (Mathf.Max(Mathf.Max(groundGrid[i, j], groundGrid[i + 1, j]), Mathf.Max(groundGrid[i, j + 1], groundGrid[i + 1, j + 1])) < SeaLevel - WadeDepth) return false;
                if (!roofed[i, j]) return true;
                float x = Origin + (i + 0.5f) * Step, z = Origin + (j + 0.5f) * Step;
                return GroundAt(x, z) < CeilingAt(x, z) - 1.5f;
            };
            string Where((int, int) c) => $"({Origin + (c.Item1 + 0.5f) * Step:0}, {Origin + (c.Item2 + 0.5f) * Step:0})";
            // The last few cells of the way there, so a failure shows where a wall is missing.
            string Route((int, int) end, Dictionary<(int, int), (int, int)> from)
            {
                var steps = new List<string>();
                for ((int, int) c = end; steps.Count < 8 && from.ContainsKey(c); c = from[c]) steps.Add(Where(c));
                steps.Reverse();
                return string.Join(" <- ", steps);
            }
            // The ledge tops out near 20 m and the mountains rise past 60 m: standing above this outside means climbing.
            const float ClimbedHeight = 32f;
            var mountainRoute = new Dictionary<(int, int), (int, int)>();
            foreach ((int i, int j) in Flood(CoveBottom - DockSeaward * 3f, passable, mountainRoute))
                if (!roofed[i, j] && GroundAt(Origin + (i + 0.5f) * Step, Origin + (j + 0.5f) * Step) > ClimbedHeight) { problems.Add($"The mountains can be climbed: {Route((i, j), mountainRoute)}."); break; }
            var caveRoute = new Dictionary<(int, int), (int, int)>();
            foreach ((int i, int j) in Flood(FinalBossZone, (i, j) => passable(i, j) && roofed[i, j] && RampWeight(Origin + (i + 0.5f) * Step, Origin + (j + 0.5f) * Step) < 0.02f, caveRoute))
                if (UpperDistance(Origin + (i + 0.5f) * Step, Origin + (j + 0.5f) * Step) > 4f) { problems.Add($"The upper level can be climbed to from below, not by the ramp: {Route((i, j), caveRoute)}."); break; }

            // The walk from the dock up to the cave mouth and through the tunnel stays gentle (the player walks up 45
            // degrees at most), so nothing on the way catches them.
            float steepestWalk = 0f;
            Vector2 steepAt = default;
            var route = new List<Vector2>();
            for (float t = 0f; t <= 1f; t += 0.01f) route.Add(Vector2.Lerp(CoveBottom - DockSeaward * 3f, MouthPath[0], t));
            for (float t = 0f; t <= 1f; t += 0.01f) route.Add(PointOnPath(t));
            for (int k = 1; k < route.Count; k++)
            {
                float run = (route[k] - route[k - 1]).magnitude;
                if (run < 0.01f) continue;
                float slope = Mathf.Atan2(Mathf.Abs(GroundAt(route[k].x, route[k].y) - GroundAt(route[k - 1].x, route[k - 1].y)), run) * Mathf.Rad2Deg;
                if (slope > steepestWalk) { steepestWalk = slope; steepAt = route[k]; }
            }
            if (steepestWalk > 35f) problems.Add($"The way from the dock to the cave is {steepestWalk:0} degrees steep at ({steepAt.x:0}, {steepAt.y:0}).");

            // Walking headroom all the way through the mouth tunnel.
            for (float t = 0f; t <= 1f; t += 0.05f)
            {
                Vector2 p = PointOnPath(t);
                if (p.y > PortalZ + 2f && CeilingAt(p.x, p.y) - GroundAt(p.x, p.y) < 8f) problems.Add($"Mouth tunnel too low at ({p.x:0}, {p.y:0}).");
            }
            // The cove comes down to the water, and the dock's line of sight up it to the cave mouth is clear.
            if (GroundAt(CoveBottom.x, CoveBottom.y) > SeaLevel + 1.2f) problems.Add("The cove doesn't reach the water.");
            for (float t = 0.05f; t < 0.75f; t += 0.01f)
            {
                Vector3 p = Vector3.Lerp(SightFrom, SightTo, t);
                if (GroundAt(p.x, p.z) > p.y - 0.5f) { problems.Add($"The cave mouth is hidden from the dock by ground at ({p.x:0}, {p.z:0})."); break; }
            }
            // The island stays inside the map, with sea all round.
            foreach (Vector2 edge in new[] { new Vector2(0f, Origin + 2f), new Vector2(0f, -Origin - 2f), new Vector2(Origin + 2f, 0f), new Vector2(-Origin - 2f, 0f) })
                if (GroundAt(edge.x, edge.y) > SeaLevel - 2f) problems.Add($"Land reaches the map edge at ({edge.x:0}, {edge.y:0}).");
            return problems;
        }

        // The cells reachable from a point, stepping between neighbouring allowed cells; cameFrom records the way.
        private static List<(int, int)> Flood(Vector2 from, System.Func<int, int, bool> allowed, Dictionary<(int, int), (int, int)> cameFrom)
        {
            var reached = new List<(int, int)>();
            var seen = new bool[Cells, Cells];
            var open = new Queue<(int, int)>();
            int si = Mathf.FloorToInt((from.x - Origin) / Step), sj = Mathf.FloorToInt((from.y - Origin) / Step);
            if (!allowed(si, sj)) return reached;
            seen[si, sj] = true;
            open.Enqueue((si, sj));
            while (open.Count > 0)
            {
                (int i, int j) = open.Dequeue();
                reached.Add((i, j));
                foreach ((int ni, int nj) in new[] { (i + 1, j), (i - 1, j), (i, j + 1), (i, j - 1) })
                {
                    if (ni < 0 || nj < 0 || ni >= Cells || nj >= Cells || seen[ni, nj] || !allowed(ni, nj)) continue;
                    seen[ni, nj] = true;
                    cameFrom[(ni, nj)] = (i, j);
                    open.Enqueue((ni, nj));
                }
            }
            return reached;
        }

        private static bool InQuad(Vector2 p, Vector2 a, Vector2 b, Vector2 c, Vector2 d) => InTriangle(p, a, b, c) || InTriangle(p, a, c, d);

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float Side(Vector2 u, Vector2 v, Vector2 w) => (u.x - w.x) * (v.y - w.y) - (v.x - w.x) * (u.y - w.y);
            float d1 = Side(p, a, b), d2 = Side(p, b, c), d3 = Side(p, c, a);
            return !((d1 < 0f || d2 < 0f || d3 < 0f) && (d1 > 0f || d2 > 0f || d3 > 0f));
        }

        // ---------- Climbing barriers ----------

        // The player can jump up any slope that isn't vertical, so the edges they must not climb are lined with vertical
        // rock: along the mountains' foot round the ledge (open only at the cave mouth, where the cliff slabs frame the
        // doorway and stand in for it), and down the face of the upper level's cliff in the cave. Each wall is a solid block with closed ends: outside, its flat top runs
        // back into the hillside, so there is no gap behind the face; a cave cliff's top runs back to the upper level's
        // edge, so a player who jumps down can't fall in behind it, and where it meets the cave wall its rock goes up to
        // the ceiling, so no one climbs round it along the wall.
        private const float MountainWallHeight = 18f;   // above the ground at the mountains' foot: the clean base of the sea cliffs
        private const float NotchWallOffset = 5.5f;     // the doorway's sides, either side of the mouth's centre line
        private const float CliffFoot = 5f, CliffBack = 2f; // a cliff wall stands this far out over the deep level; its top reaches this far back

        /// <summary>One sample along a barrier: the vertical face stands on Foot, from Bottom to Top; a cliff's rock top
        /// runs from the face back to Back, at Cap.</summary>
        public struct BarrierPoint
        {
            public Vector2 Foot, Back, Out;
            public float Bottom, Top, Cap;
        }

        /// <summary>A continuous barrier wall.</summary>
        public sealed class Barrier
        {
            public readonly List<BarrierPoint> Points = new List<BarrierPoint>();
            public bool Capped, InCave;
        }

        /// <summary>All the barrier walls (call after <see cref="Generate"/>).</summary>
        public static List<Barrier> Barriers()
        {
            var walls = new List<Barrier>();
            float cutX = MouthPath[0].x;
            // A point on the mountains' wall: its face looks out toward the ledge, away from the mountains.
            BarrierPoint Foot(Vector2 p)
            {
                float g = GroundAt(p.x, p.y);
                return new BarrierPoint
                {
                    Foot = p, Back = p, Out = (p - CaveCentre).normalized,
                    Bottom = g < SeaLevel ? SeaFloor - 3f : g - 2f, Top = Mathf.Max(g, SeaLevel) + MountainWallHeight
                };
            }
            // The mountains' foot, all the way round the ledge and on into the sea at both ends, in two pieces: it stops
            // either side of the cave mouth, inside the cliff slabs that frame the doorway (a little past its sides, so
            // the slanting face never shows a wall end in the opening).
            Barrier current = null;
            for (float angle = 268f; angle <= 348f; angle += 0.8f)
            {
                var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                Vector2? foot = null;
                for (float r = 280f; r > 180f; r -= 0.5f)
                {
                    Vector2 q = CaveCentre + dir * r;
                    CliffTop(q.x, q.y, out _, out float mountain);
                    if (mountain >= 0.04f) { foot = q; break; }
                }
                if (foot == null) { current = null; continue; }
                Vector2 p = foot.Value;
                if (p.y < PortalZ + 10f && Mathf.Abs(p.x - cutX) < NotchWallOffset + 2.5f)
                {
                    current = null;
                    continue;
                }
                if (current == null) walls.Add(current = new Barrier { Capped = true });
                current.Points.Add(Foot(p));
            }

            // The outside walls are solid rock: a flat top runs back from the face to the nearest hillside behind it,
            // so there is no gap behind the face to see into.
            foreach (Barrier wall in walls)
                for (int k = 0; k < wall.Points.Count; k++)
                {
                    BarrierPoint point = wall.Points[k];
                    // The nearest hillside behind the face, looking straight back and up to 60 degrees either side
                    // (at a corner the high ground is often diagonally behind). None within reach: no top at all.
                    Vector2 back = point.Foot;
                    float nearest = 8f;
                    for (int turn = -60; turn <= 60; turn += 15)
                    {
                        float r = turn * Mathf.Deg2Rad;
                        Vector2 dir = new Vector2(-point.Out.x * Mathf.Cos(r) + point.Out.y * Mathf.Sin(r), -point.Out.x * Mathf.Sin(r) - point.Out.y * Mathf.Cos(r));
                        for (float d = 0.5f; d < nearest; d += 0.5f)
                        {
                            Vector2 q = point.Foot + dir * d;
                            // Never out over the mouth: a top there would stand in the doorway as a block.
                            if (DistanceToMouth(q) < NotchWallOffset + 0.5f) break;
                            if (GroundAt(q.x, q.y) >= point.Top) { back = q; nearest = d; break; }
                        }
                    }
                    point.Back = back;
                    point.Cap = point.Top;
                    wall.Points[k] = point;
                }

            // The upper level's cliff in the cave: along the front's edge, then round the beetles' lobe, leaving out the ramp.
            void CliffWall(System.Func<float, (Vector2 edge, Vector2 outward, bool mine)> sample, float from, float to, float step)
            {
                Barrier wall = null;
                for (float s = from; s <= to; s += step)
                {
                    (Vector2 edge, Vector2 outward, bool mine) = sample(s);
                    if (!mine || CavernDepth(edge.x, edge.y) < -3f || RampWeight(edge.x, edge.y) > 0.01f) { wall = null; continue; }
                    if (wall == null) walls.Add(wall = new Barrier { Capped = true, InCave = true });
                    Vector2 foot = edge + outward * CliffFoot, back = edge - outward * CliffBack;
                    float cap = GroundAt(back.x, back.y) + 0.03f;
                    float top = CavernDepth(foot.x, foot.y) < 15f ? Mathf.Max(cap, CeilingAt(foot.x, foot.y) + 1f) : cap;
                    wall.Points.Add(new BarrierPoint
                    {
                        Foot = foot, Back = back, Out = outward, Cap = cap, Top = top,
                        Bottom = Mathf.Min(GroundAt(foot.x, foot.y), GroundAt(edge.x, edge.y)) - 2f
                    });
                }
            }
            // (Each part runs 4 m past where the other takes over, so the two overlap at the corner they share.)
            CliffWall(x =>
            {
                var edge = new Vector2(x, FrontZ + 4f * Noise(x, 0f, 35f, 15f));
                return (edge, Vector2.up, FrontEdge(edge.x, edge.y) - LobeEdge(edge.x, edge.y) > -4f);
            }, Origin, -Origin, 1f);
            CliffWall(a =>
            {
                var outward = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                var edge = BeetleZone + outward * (BeetleLobeRadius + 4f * AngleNoise(a * Mathf.Deg2Rad, 9f, 16f));
                return (edge, outward, LobeEdge(edge.x, edge.y) - FrontEdge(edge.x, edge.y) > -4f);
            }, -180f, 180f, 1.5f);
            return walls;
        }

        private static bool TopBehind(BarrierPoint a, BarrierPoint b)
        {
            bool Behind(BarrierPoint p) => (p.Back - p.Foot).magnitude > 0.4f && Vector2.Dot(p.Back - p.Foot, p.Out) < 0f;
            return Behind(a) && Behind(b) && Vector2.Dot(a.Out, b.Out) > 0.8f;
        }

        /// <summary>The barrier walls as a mesh: rough vertical rock faces (never leaning back, so they can't be climbed),
        /// cliff tops and the closed ends of the cliff walls.</summary>
        public static Mesh BuildBarrierMesh(List<Barrier> walls, out Surface[] surfaces)
        {
            var kit = new FlatMeshKit<Surface>();
            const int bands = 3;
            foreach (Barrier wall in walls)
            {
                Surface rock = wall.InCave ? Surface.CaveWall : Surface.MountainCliff; // outside: the foot of the sea cliffs, in their rock
                // A face point: the bands between bottom and top bulge out a little (by position, so neighbours agree).
                Vector3 Face(BarrierPoint p, int band)
                {
                    float bulge = band == 0 || band == bands ? 0f : 0.3f + 0.35f * (Noise(p.Foot.x * 3f, p.Foot.y * 3f + band * 17f, 2f, 71f) + 1f);
                    return new Vector3(p.Foot.x + p.Out.x * bulge, Mathf.Lerp(p.Bottom, p.Top, band / (float)bands), p.Foot.y + p.Out.y * bulge);
                }
                for (int k = 0; k < wall.Points.Count - 1; k++)
                {
                    BarrierPoint a = wall.Points[k], b = wall.Points[k + 1];
                    var outward = new Vector3(a.Out.x + b.Out.x, 0f, a.Out.y + b.Out.y);
                    for (int band = 0; band < bands; band++)
                        kit.Quad(Face(a, band), Face(b, band), Face(b, band + 1), Face(a, band + 1), outward, rock);
                    if (!wall.Capped) continue;
                    // Outside, a top only where it lies flat behind the face: not at a corner (where the two ends face
                    // different ways), not where an end has no top, and never reaching out in front of the face.
                    if (!wall.InCave && !TopBehind(a, b)) continue;
                    var aCap = new Vector3(a.Foot.x, a.Cap, a.Foot.y);
                    var bCap = new Vector3(b.Foot.x, b.Cap, b.Foot.y);
                    kit.Quad(aCap, bCap, new Vector3(b.Back.x, b.Cap, b.Back.y), new Vector3(a.Back.x, a.Cap, a.Back.y), Vector3.up, wall.InCave ? Surface.CaveFloor : Surface.MountainRock);
                    // Outside, the back of the block, down into the hillside (hidden wherever the hill is higher).
                    if (!wall.InCave)
                        kit.Quad(new Vector3(a.Back.x, a.Cap, a.Back.y), new Vector3(b.Back.x, b.Cap, b.Back.y),
                            new Vector3(b.Back.x, GroundAt(b.Back.x, b.Back.y) - 2f, b.Back.y), new Vector3(a.Back.x, GroundAt(a.Back.x, a.Back.y) - 2f, a.Back.y), -outward, rock);
                    // Rock above the cliff top, up to the ceiling, near the cave walls.
                    if (a.Top > a.Cap + 0.1f || b.Top > b.Cap + 0.1f)
                        kit.Quad(new Vector3(a.Back.x, a.Cap, a.Back.y), new Vector3(b.Back.x, b.Cap, b.Back.y), new Vector3(b.Back.x, b.Top, b.Back.y), new Vector3(a.Back.x, a.Top, a.Back.y), -outward, rock);
                }
                if (!wall.Capped || wall.Points.Count < 2) continue;
                // Close both ends of the block, so it reads as solid rock and nothing gets in under its top.
                foreach ((BarrierPoint end, BarrierPoint next) in new[] { (wall.Points[0], wall.Points[1]), (wall.Points[wall.Points.Count - 1], wall.Points[wall.Points.Count - 2]) })
                {
                    var away = new Vector3(end.Foot.x - next.Foot.x, 0f, end.Foot.y - next.Foot.y);
                    float low = Mathf.Min(end.Bottom, GroundAt(end.Back.x, end.Back.y) - 2f);
                    kit.Quad(new Vector3(end.Foot.x, low, end.Foot.y), new Vector3(end.Foot.x, end.Top, end.Foot.y),
                        new Vector3(end.Back.x, end.Top, end.Back.y), new Vector3(end.Back.x, low, end.Back.y), away, rock);
                }
            }
            return Build(kit, "CrystalCavesBarriers", out surfaces);
        }

        // ---------- Sea-cliff blocks ----------

        // The Ocean's wade depth (Scripts/World/Ocean.cs): any deeper and the player is rescued from the water.
        private const float WadeDepth = 1.3f;
        private const float BlockDepth = 22f;          // how far each block reaches back into the cliff (short of the cave)
        private const float BlockProud = 1.1f;         // the least a block stands in front of the mountains' wall (whose bulges reach 1 m)

        /// <summary>One tier of a cliff block: its top, how far its face stands behind the block's front, how far in from
        /// each side, and what its top (a ledge, or the cliff top) is.</summary>
        public struct CliffTier
        {
            public float Top, Back, Inset;
            public Surface Ledge;
            public bool Terrace; // a terrace's grassy ledge (or the cliff top), where plants grow
        }

        /// <summary>A stone block in the sea cliffs' face: its front from A to B, facing Out, reaching back from A by BackA
        /// and from B by BackB, in tiers from Bottom up. A lintel spans the cave mouth, so its Bottom is over the opening.</summary>
        public sealed class CliffBlock
        {
            public Vector2 A, B, Out, BackA, BackB;
            public float Bottom;
            public bool Lintel;
            public readonly List<CliffTier> Tiers = new List<CliffTier>();
        }

        /// <summary>The sea cliffs' faces, like the reference (11-09): tall stone slabs standing side by side all round the
        /// island and up both sides of the cut to the cave mouth, each in a few tiers that step back a little, leaving
        /// narrow ledges, with a grassy top just over the cliff top behind. Every face is vertical and the lowest ledge is
        /// at least 20 m over the ground or the shallows in front, so they can't be climbed. Call after <see cref="Generate"/>.</summary>
        public static List<CliffBlock> CliffBlocks()
        {
            var rng = new System.Random(20261111);
            float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var blocks = new List<CliffBlock>();
            float cutX = MouthPath[0].x;

            var north = new Vector2(0f, BlockDepth); // the mouth runs due north into the cliff
            void Add(Vector2 a, Vector2 b, Vector2 outward)
            {
                // At the doorway a block's side runs back along the tunnel, not straight into the cliff, so it frames the
                // opening instead of cutting across the tunnel; the block over the opening is a lintel, starting above it.
                // Likewise wherever reaching straight back would cross into the tunnel (beside the mouth the face slants).
                bool AtDoor(Vector2 p) => p.y < PortalZ + 10f && Mathf.Abs(p.x - cutX) < NotchWallOffset + 0.01f;
                Vector2 BackFrom(Vector2 p)
                {
                    if (AtDoor(p)) return north;
                    for (float t = 0f; t <= 1f; t += 0.1f)
                        if (DistanceToMouth(p - outward * BlockDepth * t) < NotchWallOffset) return north;
                    // Never back into the cave: the blocks stop short of its outer wall.
                    float depth = BlockDepth;
                    while (depth > 12f && CavernDepth(p.x - outward.x * depth, p.y - outward.y * depth) > -2f) depth -= 1f;
                    return -outward * depth;
                }
                var block = new CliffBlock { A = a, B = b, Out = outward, BackA = BackFrom(a), BackB = BackFrom(b), Lintel = AtDoor((a + b) * 0.5f) };
                // The ground or shallows along its front, its lowest footing, and the cliff top over its back.
                float front = SeaLevel, low = float.MaxValue, top = float.MinValue;
                for (float s = 0f; s <= 1f; s += 0.25f)
                {
                    Vector2 p = Vector2.Lerp(a, b, s);
                    for (float d = 0.5f; d <= 4f; d += 0.5f) front = Mathf.Max(front, GroundAt(p.x + outward.x * d, p.y + outward.y * d));
                    low = Mathf.Min(low, GroundAt(p.x, p.y));
                    for (float d = 6f; d <= BlockDepth; d += 2f) top = Mathf.Max(top, GroundAt(p.x - outward.x * d, p.y - outward.y * d));
                }
                block.Bottom = low - 3f;
                if (block.Lintel)
                {
                    // Clear of the mouth's arch all the way back.
                    block.Bottom = float.MinValue;
                    foreach (Vector2 p in new[] { a, b, a + block.BackA, b + block.BackB, (a + b) * 0.5f + north * 0.5f })
                        block.Bottom = Mathf.Max(block.Bottom, MouthFloor(p) + MouthHeight + 1.5f);
                }
                // Terraces, like the reference (11-10): two or three levels stepping back into the cliff, the top one at
                // the full cliff top, the lower ones' grassy ledges wide enough for trees. The lowest ledge is at least
                // 20 m over the ground in front (over the cave mouth, over the tunnel's roof too), so none can be reached.
                float depth = Mathf.Min(block.BackA.magnitude, block.BackB.magnitude);
                float first = front + MountainWallHeight + Range(2f, 10f), last = top + Range(0.5f, 3f);
                if (block.Lintel) first = Mathf.Max(first, block.Bottom + 4f); // the lintel's first step stands clear above the arch
                int levels = rng.Next(2, 4);
                float[] tops = levels == 3
                    ? new[] { Mathf.Lerp(first, last, Range(0.15f, 0.4f)), Mathf.Lerp(first, last, Range(0.5f, 0.75f)), last }
                    : new[] { Mathf.Lerp(first, last, Range(0.25f, 0.6f)), last };
                float[] faces = levels == 3
                    ? new[] { 0f, depth * Range(0.28f, 0.4f), depth * Range(0.58f, 0.7f) }
                    : new[] { 0f, depth * Range(0.4f, 0.65f) };
                float inset = 0f, width = (b - a).magnitude, below = first; // (cracks only from the first ledge up: nothing in reach)
                for (int level = 0; level < levels; level++)
                {
                    bool cliffTop = level == levels - 1;
                    float narrower = Range(0f, 0.6f);
                    if (level > 0) inset = Mathf.Min(inset + narrower, width * 0.25f);
                    // Each terrace's face in one to three bands that step back a little, or jut out a little (never over
                    // the arch's plate), now and then with a crack across, the last band's top the terrace's ledge.
                    int bands = rng.Next(1, 4);
                    float bandFrom = below, back = faces[level];
                    if (level == 0 && bands > 1) block.Tiers.Add(new CliffTier { Top = first, Back = 0f, Inset = 0f, Ledge = Surface.ShelfRock });
                    for (int band = level == 0 && bands > 1 ? 1 : 0; band < bands; band++)
                    {
                        bool ledge = band == bands - 1;
                        float bandTop = ledge ? tops[level] : Mathf.Lerp(bandFrom, tops[level], (band + 1f) / bands);
                        if (band > 0) back = Mathf.Clamp(back + Range(-0.5f, 1f), faces[level] - 0.6f, depth - 3f);
                        if (bandTop - below > 9f && rng.NextDouble() < 0.5)
                        {
                            float crack = Range(below + 3f, bandTop - 4f);
                            block.Tiers.Add(new CliffTier { Top = crack, Back = back, Inset = inset, Ledge = Surface.ShelfRock });
                            block.Tiers.Add(new CliffTier { Top = crack + Range(0.6f, 1.2f), Back = back + Range(0.4f, 0.8f), Inset = inset, Ledge = Surface.MountainRock });
                        }
                        block.Tiers.Add(new CliffTier
                        {
                            Top = bandTop, Back = back, Inset = inset, Terrace = ledge,
                            // Grass on the terraces and most of the cliff top, tan rock on the narrow steps between.
                            Ledge = ledge ? (cliffTop && rng.NextDouble() < 0.2 ? Surface.MountainRock : Surface.ShelfMoss) : Surface.ShelfRock
                        });
                        below = bandTop;
                    }
                }
                blocks.Add(block);
            }

            // All round the island, standing on the cliffs' foot (where the mountains' wall stands) a little in front of
            // it, so they hide it. Each block's front is straight, 6-11 m wide, and stands out by its own amount.
            // Across the cave mouth the face is one straight run (DoorFront), so the slabs either side of the doorway
            // and the lintel over it line up, and the mouth's arch sits flush in it.
            float doorStart = FootAngleAtX(cutX - DoorSpan), doorEnd = FootAngleAtX(cutX + DoorSpan);
            Vector2 start = RingFoot(0f), from = start;
            for (float angle = 0f; angle < Mathf.PI * 2f - 0.001f;)
            {
                bool door = Mathf.Abs(angle - doorStart) < 1e-5f;
                float next = door ? doorEnd : Mathf.Min(angle + Range(6f, 11f) / (from - CaveCentre).magnitude, Mathf.PI * 2f);
                if (!door && angle < doorStart && next > doorStart) next = doorStart;
                Vector2 to = next >= Mathf.PI * 2f ? start : RingFoot(next), mid = (from + to) * 0.5f;
                angle = next;
                Vector2 along = (to - from).normalized, outward = new Vector2(along.y, -along.x);
                if (Vector2.Dot(outward, mid - CaveCentre) < 0f) outward = -outward;
                float proud = DoorProud; // every front on one line, the cave mouth's
                Vector2 a = from + outward * proud, b = to + outward * proud;
                from = to;
                // Across the cave mouth, split at the doorway's sides: a lintel over the opening, slabs either side.
                var cuts = new List<float> { 0f, 1f };
                if (door)
                    foreach (float edge in new[] { cutX - NotchWallOffset, cutX + NotchWallOffset })
                    {
                        float s = (edge - a.x) / (b.x - a.x);
                        if (s > 0.02f && s < 0.98f) cuts.Add(s);
                    }
                cuts.Sort();
                for (int k = 0; k < cuts.Count - 1; k++) Add(Vector2.Lerp(a, b, cuts[k]), Vector2.Lerp(a, b, cuts[k + 1]), outward);
            }
            return blocks;
        }

        private const float DoorSpan = 16f;                    // the face runs straight this far either side of the mouth's centre line
        private const float DoorProud = BlockProud + 0.5f;     // how far that run stands in front of the cliffs' foot

        // Where the cliffs' foot is (the mountains' wall stands there), looking out from the cave's centre at this angle.
        private static Vector2 RingFoot(float angle)
        {
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            for (float r = 280f; r > 170f; r -= 0.5f)
            {
                Vector2 q = CaveCentre + dir * r;
                CliffTop(q.x, q.y, out _, out float mountain);
                if (mountain >= 0.04f) return q;
            }
            return CaveCentre + dir * 170f;
        }

        // The angle round the island at which the foot reaches x, by the cave mouth (there x grows with the angle).
        private static float FootAngleAtX(float x)
        {
            float lo = 5.0f, hi = 5.7f;
            for (int i = 0; i < 30; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (RingFoot(mid).x < x) lo = mid;
                else hi = mid;
            }
            return (lo + hi) * 0.5f;
        }

        /// <summary>The straight cliff front across the cave mouth, from a (west) to b, facing outward.</summary>
        public static void DoorFront(out Vector2 a, out Vector2 b, out Vector2 outward)
        {
            float cutX = MouthPath[0].x;
            a = RingFoot(FootAngleAtX(cutX - DoorSpan));
            b = RingFoot(FootAngleAtX(cutX + DoorSpan));
            Vector2 along = (b - a).normalized;
            outward = new Vector2(along.y, -along.x);
            if (Vector2.Dot(outward, (a + b) * 0.5f - CaveCentre) < 0f) outward = -outward;
            a += outward * DoorProud;
            b += outward * DoorProud;
        }

        /// <summary>The cave mouth's arch: a rocky, rounded opening (11 m wide) in a plate flush with the cliff face, and
        /// a closed arched passage from it back into the roofed tunnel, so the doorway is solid rock all round and its
        /// walls are vertical (nothing to climb). The passage stays just inside the tunnel's own arch.</summary>
        public static Mesh BuildMouthMesh(out Surface[] surfaces)
        {
            var kit = new FlatMeshKit<Surface>();
            var rng = new System.Random(20261112);
            float cutX = MouthPath[0].x;
            const float halfWidth = 5.3f, buried = -1.5f, backZ = -136f, rim = 3.5f; // just enough rock round the opening to reach the lintel; the slabs make the rest of the face
            DoorFront(out Vector2 a, out Vector2 b, out Vector2 outward);
            // Over the opening the plate reaches 1 m up past the lintel's underside, so no gap shows between them.
            CliffBlock lintel = CliffBlocks().Find(k => k.Lintel);
            float lintelHalf = lintel == null ? 0f : (lintel.B - lintel.A).magnitude * 0.5f + 1f;
            Vector3 Reach(Vector3 v) => lintel != null && Mathf.Abs(v.x - cutX) < lintelHalf && v.y > FloorAt(v.x, v.z) + 4f ? new Vector3(v.x, Mathf.Max(v.y, lintel.Bottom + 1f), v.z) : v;
            float FloorAt(float x, float z) => MouthFloor(new Vector2(x, z));

            // The opening's outline (across, up): straight sides, then an arch under the tunnel's own (13 m at the
            // middle, 13 * sqrt((10 - d) / 10) at d from it), its rocks nudged inward a little so the rim is rough.
            var outline = new List<Vector2> { new Vector2(-halfWidth, buried) };
            const int steps = 12;
            for (int i = 0; i <= steps; i++)
            {
                float u = -halfWidth + 2f * halfWidth * i / steps;
                var p = new Vector2(u, 12.2f * Mathf.Sqrt((10f - Mathf.Abs(u)) / 10f));
                if (i > 0 && i < steps) p += (new Vector2(0f, 4f) - p).normalized * (float)rng.NextDouble() * 0.6f;
                outline.Add(p);
            }
            outline.Add(new Vector2(halfWidth, buried));

            Vector3 face = At(outward, 0f);
            // On the cliff front at p's x (0.15 m proud of the slabs, so they never show through), and on the tunnel's
            // line at the passage's far end.
            Vector3 Front(Vector2 p)
            {
                float x = cutX + p.x, z = Mathf.Lerp(a.y, b.y, (x - a.x) / (b.x - a.x));
                return new Vector3(x, FloorAt(x, z) + p.y, z) + face * 0.15f;
            }
            Vector3 Back(Vector2 p) { float x = cutX + p.x; return new Vector3(x, FloorAt(x, backZ) + p.y, backZ); }
            for (int i = 0; i < outline.Count - 1; i++)
            {
                Vector2 p = outline[i], q = outline[i + 1];
                // The passage, facing in.
                Vector3 inward = -At(new Vector2(p.x + q.x, 0f), 0f) - Vector3.up * ((p.y + q.y) * 0.5f - 4f);
                kit.Quad(Front(p), Front(q), Back(q), Back(p), inward, Surface.CaveWall);
                // The plate round the opening, in the cliff face, out to a rough ring 6 m past it.
                Vector2 np = (p - new Vector2(0f, 4f)).normalized, nq = (q - new Vector2(0f, 4f)).normalized;
                if (i == 0) np = Vector2.left;
                if (i == outline.Count - 2) nq = Vector2.right;
                kit.Quad(Front(p), Front(q), Reach(Front(q + nq * rim)), Reach(Front(p + np * rim)), face, Surface.MountainCliff);
            }
            return Build(kit, "CrystalCavesMouth", out surfaces);
        }

        private static Vector3 At(Vector2 p, float y) => new Vector3(p.x, y, p.y);

        /// <summary>The cliff blocks as one mesh: vertical faces and flat ledges.</summary>
        public static Mesh BuildCliffBlockMesh(List<CliffBlock> blocks, out Surface[] surfaces)
        {
            var kit = new FlatMeshKit<Surface>();
            foreach (CliffBlock block in blocks)
            {
                Vector2 along = (block.B - block.A).normalized;
                Vector3 o = At(block.Out, 0f), s = At(along, 0f);
                float bottom = block.Bottom;
                foreach (CliffTier tier in block.Tiers)
                {
                    Vector2 a = block.A - block.Out * tier.Back + along * tier.Inset, b = block.B - block.Out * tier.Back - along * tier.Inset;
                    Vector2 aBack = block.A + block.BackA + along * tier.Inset, bBack = block.B + block.BackB - along * tier.Inset;
                    kit.Quad(At(a, bottom), At(b, bottom), At(b, tier.Top), At(a, tier.Top), o, Surface.MountainCliff);
                    kit.Quad(At(aBack, bottom), At(a, bottom), At(a, tier.Top), At(aBack, tier.Top), -s, Surface.MountainRock);
                    kit.Quad(At(b, bottom), At(bBack, bottom), At(bBack, tier.Top), At(b, tier.Top), s, Surface.MountainRock);
                    kit.Quad(At(bBack, bottom), At(aBack, bottom), At(aBack, tier.Top), At(bBack, tier.Top), -o, Surface.MountainRock);
                    kit.Quad(At(a, tier.Top), At(b, tier.Top), At(bBack, tier.Top), At(aBack, tier.Top), Vector3.up, tier.Ledge);
                    // Its underside, seen where it juts out over the tier below, or over the cave mouth for a lintel.
                    if (bottom > block.Bottom || block.Lintel) kit.Quad(At(a, bottom), At(aBack, bottom), At(bBack, bottom), At(b, bottom), Vector3.down, Surface.MountainRock);
                    bottom = tier.Top;
                }
            }
            return Build(kit, "CrystalCavesCliffBlocks", out surfaces);
        }

        /// <summary>A point along the mouth's centre line, 0 on the ledge in front of it, 1 at its inner end.</summary>
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
