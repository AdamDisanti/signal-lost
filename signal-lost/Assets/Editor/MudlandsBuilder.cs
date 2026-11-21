using System;
using System.Collections.Generic;
using System.Linq;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Enemies;
using SignalLost.Enemies.Desert;
using SignalLost.Inventory;
using SignalLost.Mudlands;
using SignalLost.NPC;
using SignalLost.Player;
using SignalLost.Progression;
using SignalLost.Scanner;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SignalLost.Editor
{
    /// <summary>
    /// Builds the Mudlands (docs/mudlands.md): the creature prefabs, the trophy, and the whole level scene. The ground
    /// is a generated low-poly heightfield (roads flattened in, a creek, the bog, raised arenas, an unclimbable ridge
    /// ring); scenery and buildings are <see cref="MudProp"/> and <see cref="MudScenery"/> objects whose models are
    /// built from code; the NavMesh is baked before any creature or breakable thing is placed. Then the base camp,
    /// Cavendish and Eugene, the quest objects, the encounters, the four bosses and the story are put in.
    /// Outside the ridge ring the land drops to a beach and the open sea (11-20): the Mudlands is an island, and a pass
    /// south of camp leads down to the swamp dock where the boat from the Desert Dunes comes in.
    /// Like the tutorial builder it replaces the scene: make edits here, not in the scene.
    /// Menu: Signal Lost > Mudlands > Build Mudlands Level.
    /// </summary>
    internal static class MudlandsBuilder
    {
        public const string ScenePath = "Assets/Scenes/Large Areas/Mudlands.unity";
        private const string Folder = "Assets/Prefabs/Enviornment objects/Mudlands";
        private const string EnemyFolder = "Assets/Prefabs/Enemies/Mudlands";
        private const string NavMeshPath = "Assets/Scenes/Large Areas/MudlandsNavMesh.asset";
        private const string CavendishPath = "Assets/Prefabs/Enviornment objects/GrayboxProps/CapybaraAstronaut.prefab";
        private const string MerchantPath = "Assets/Prefabs/NPC/MerchantCrab.prefab";
        private const string TrophyFolder = "Assets/Resources/SignalLost/Trophies";

        // 520 m (was 400 m before the sea, 11-20): the same 4 m cells, and room for the ridge's outer slope, the beach
        // and the shallows round the island.
        private const float MapSize = 520f;
        private const int Cells = 130;
        private const float PlayRadius = 178f;
        public const float WaterY = -0.55f;

        // ---------- The sea and the landing (11-20) ----------
        // The sea is level with the swamp water. The ridge ring's outer side comes down (from 196 m to 214 m out) to a
        // beach all round the island (IslandKit.Shore, its coastline at Coast). South of camp the ridge opens into a pass
        // (Pass) down to a small bay, whose beach is closer in (BayCoast, along -z), and the swamp dock runs out from it.
        // The pass floor stays well above the sea, so the open sea can't flood into the swamp through it; everything
        // inside the ridge (r under 174 m) is exactly as before.
        private const float SeaLevel = WaterY;
        private const float Coast = 222f, BayCoast = 198f, PassFloor = 1.4f;
        private const float PassStart = -174.5f; // the pass only changes ground south of this (outside every inland scatter radius)

        // ---------- The map (x east, y north) ----------

        public static readonly Vector2 Camp = new Vector2(0f, -145f);
        private static readonly Vector2 Flats = new Vector2(0f, -45f);
        private static readonly Vector2 Rotwood = new Vector2(-105f, -35f);
        private static readonly Vector2 School = new Vector2(-88f, -55f);
        private static readonly Vector2 HaulEnd = new Vector2(-132f, -100f);
        private static readonly Vector2 Speedway = new Vector2(-108f, 80f);
        private static readonly Vector2 Lot = new Vector2(-40f, 120f);
        private static readonly Vector2 WitchClearing = new Vector2(75f, 120f);
        private static readonly Vector2 Dojo = new Vector2(128f, 55f);
        private static readonly Vector2 Bog = new Vector2(100f, -50f);
        private static readonly Vector2 BogRadii = new Vector2(34f, 24f);
        private static readonly Vector2 Chapel = new Vector2(126f, 14f);
        // The chapel flood's water sheet (it drains when the dam breaks).
        private static readonly Vector2 FloodRadii = new Vector2(14f, 10f);

        private static readonly Vector2[][] Roads =
        {
            new[] { Camp + new Vector2(0f, 14f), new Vector2(4f, -110f), new Vector2(-4f, -80f), Flats },
            new[] { Flats, new Vector2(-45f, -52f), Rotwood, new Vector2(-122f, 0f), new Vector2(-128f, 30f), new Vector2(-112f, 52f) },
            new[] { new Vector2(-112f, 52f), new Vector2(-82f, 98f), new Vector2(-58f, 104f), new Vector2(-40f, 98f) },
            new[] { Flats, new Vector2(6f, -10f), new Vector2(12f, 22f), new Vector2(24f, 50f), new Vector2(42f, 78f), new Vector2(60f, 102f), WitchClearing + new Vector2(-12f, -6f) },
            new[] { new Vector2(-40f, 98f), new Vector2(-10f, 92f), new Vector2(24f, 50f) },
            new[] { Flats, new Vector2(45f, -60f), new Vector2(64f, -78f), new Vector2(100f, -82f), new Vector2(138f, -62f), new Vector2(146f, -20f), Chapel, new Vector2(124f, 34f), Dojo + new Vector2(-14f, 0f) },
            new[] { Dojo + new Vector2(-14f, 0f), new Vector2(110f, 82f), new Vector2(92f, 104f), WitchClearing + new Vector2(8f, -14f) },
            new[] { Rotwood, new Vector2(-114f, -58f), new Vector2(-125f, -80f), HaulEnd },
            // The Gator Causeway: out of the lot's west side to the county line (Clutch's escape ramp).
            new[] { Lot + new Vector2(-22f, 6f), new Vector2(-84f, 134f), new Vector2(-120f, 112f), new Vector2(-144f, 80f), new Vector2(-152f, 44f), new Vector2(-156f, 18f) }
        };
        // Quest rework (11-20): two short cuts off the camp road, as narrower dirt tracks. They are kept out of Roads, which
        // also drives the scatter (trees, rocks, plants, road lights), so everything else keeps its place; the scatter
        // skips anything that would land on them (QuestBlocked) after drawing its numbers as before.
        private static readonly Vector2[][] Shortcuts =
        {
            // The Logging Chute: north-west to the driving school. A log jam blocks it until the car shifts it.
            new[] { new Vector2(2.9f, -116f), new Vector2(-30f, -100f), new Vector2(-62f, -78f), new Vector2(-79f, -63f) },
            // The Spirit Trail: north-east to the bog road, where the Spirit Road starts across the bog.
            new[] { new Vector2(3.6f, -113f), new Vector2(30f, -96f), new Vector2(56f, -70.4f) },
            // From the Spirit Road's far end up to the mud dam (drive it and you crash into the dam).
            new[] { new Vector2(124f, -20f), new Vector2(124.5f, -11f) }
        };
        // The Spirit Road: a causeway straight across the bog, from the bog road to the far shore below the dam.
        private static readonly Vector2 SpiritA = new Vector2(62f, -75f), SpiritB = new Vector2(124f, -20f);
        private const float SpiritWidth = 5.6f;
        private static readonly Vector2 LogJamAt = new Vector2(-11.5f, -109f);
        // The stilt shack by the Lantern Road bridge (a hex lantern on its roof).
        private static readonly Vector2 BridgeShack = new Vector2(27f, 31f);
        // North-east of the fire, between the tutorial's stand (north) and the desert's (east).
        private static readonly Vector2 SafeKeepingAt = Camp + new Vector2(4f, 4.5f);
        // Things placed for the quests that the scatter should keep clear of: (x, z, radius).
        private static readonly Vector2 SawmillAt = Rotwood + new Vector2(-17f, -20f);
        private static readonly Vector3[] QuestClear =
        {
            new Vector3(BridgeShack.x, BridgeShack.y, 6f), new Vector3(SafeKeepingAt.x, SafeKeepingAt.y, 2.5f), new Vector3(SawmillAt.x, SawmillAt.y, 7f)
        };

        private static readonly Vector2[] Creek = { new Vector2(-190f, -4f), new Vector2(-122f, 2f), new Vector2(-62f, 14f), new Vector2(-12f, 24f), new Vector2(30f, 18f), new Vector2(62f, -8f), new Vector2(78f, -34f) };
        private static readonly Vector2[] HaulBarricades = { new Vector2(-109f, -45f), new Vector2(-116f, -61f), new Vector2(-122f, -74f), new Vector2(-127f, -87f) };
        // Where each gong's guards come out of the reeds when it rings (on the bog's shore, off the roads).
        private static readonly Vector2[] GuardSpots = { new Vector2(62f, -42f), new Vector2(128f, -76f), new Vector2(134f, -30f) };
        private static readonly Vector2[] GongSpots = { new Vector2(84f, -34f), new Vector2(112f, -62f), new Vector2(118f, -36f) };
        private static readonly Vector2[] LanternSpots = { new Vector2(9f, -2f), new Vector2(14f, 32f), new Vector2(34f, 64f), new Vector2(50f, 90f), new Vector2(64f, 108f) };
        private static readonly (Vector2 centre, Vector2 radii)[] MudPatches =
        {
            (new Vector2(-12f, -40f), new Vector2(7f, 5f)), (new Vector2(14f, -52f), new Vector2(6f, 8f)), (new Vector2(26f, -32f), new Vector2(5f, 4f)),
            (new Vector2(-20f, -62f), new Vector2(5f, 6f)), (new Vector2(-96f, -30f), new Vector2(5f, 4f)), (new Vector2(-118f, -66f), new Vector2(4f, 6f)),
            (Speedway + new Vector2(18f, 4f), new Vector2(4f, 6f)), (Speedway + new Vector2(-16f, -6f), new Vector2(5f, 4f)),
            (Lot + new Vector2(10f, -8f), new Vector2(6f, 5f)), (Lot + new Vector2(-12f, 4f), new Vector2(4f, 5f)), (new Vector2(52f, 96f), new Vector2(4f, 3f)),
            (Dojo + new Vector2(0f, 19f), new Vector2(7f, 3f)), (Dojo + new Vector2(0f, -19f), new Vector2(7f, 3f)), (Dojo + new Vector2(19f, 0f), new Vector2(3f, 7f))
        };

        private enum Ground { Grass, DarkGrass, Moss, Road, Mud, Bed, Rock, Cliff, Sand, SeaFloor }

        [MenuItem("Signal Lost/Mudlands/Build Mudlands Level")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                !EditorUtility.DisplayDialog("Rebuild the Mudlands?", "This replaces Scenes/Large Areas/Mudlands.unity, including any hand edits.", "Rebuild", "Cancel"))
                return;
            Build();
        }

        public static void Build()
        {
            fineGrid = null;
            detailed = null;
            BridgeSpans.Clear();
            EnsureFolder(Folder + "/Materials");
            EnsureFolder(Folder + "/Meshes");
            EnsureFolder(EnemyFolder);
            MakeTemplateMaterials();
            Dictionary<Type, GameObject> creatures = BuildCreaturePrefabs();
            BuildTrophy();
            MudlandsUpgradeIcons.MergeIntoCatalog();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var random = new System.Random(20261108);
            questRandom = new System.Random(20261120);
            var level = new GameObject("Level").transform;
            GameObject ground = BuildTerrain(level);
            BuildWater(level);
            Physics.SyncTransforms();

            var clear = new List<Vector3>();
            Transform camp = BuildCamp(level, clear);
            var world = new World { Level = level, Random = random, Clear = clear };
            BuildSolidProps(world);
            BuildScenery(world);
            BuildLanding(world);
            BuildRoadSurfaces(world);
            Physics.SyncTransforms();
            BakeNavigation(level);

            // After the bake: breakable things (they carve the NavMesh while standing) and everything that moves.
            BuildLighting();
            BuildFoliage(level, ground, clear);
            Transform gameplay = BuildGameplay(world, camp, creatures);
            BuildEncounters(world, gameplay, creatures);

            EditorSceneManager.SaveScene(scene, ScenePath);
            CoreSceneSetup.SetBuildScenes();
            Debug.Log("MUDLANDS_LEVEL_BUILT");
        }

        private sealed class World
        {
            public Transform Level;
            public System.Random Random;
            public List<Vector3> Clear;
            // Filled while building, used by the story.
            public DrivingTestKiosk Kiosk;
            public Destructible[] Barricades;
            public Destructible Still, Dam;
            public SpiritGong[] Gongs;
            public CursedLantern[] Lanterns;
            public GameObject Flood;
            // The quest rework (11-20).
            public Destructible LogJam;
            public SpiritRoad SpiritRoad;
            public CursedLantern[] HexLanterns;
            public GameObject[] Relit;
            public Transform Pile;
            public RoadhouseGate Gate;
            public Transform Ramp;
            public Transform[] Pumps, Doors, Route;
            public Transform Gameplay;
        }

        // ---------- Height ----------

        private static float Noise(float x, float z, float scale, float seed = 0f) => (Mathf.PerlinNoise(x / scale + 300f + seed, z / scale + 71f + seed) - 0.5f) * 2f;

        private static float RoadLevel(float x, float z) => 0.45f + 0.5f * Noise(x, z, 60f);

        private static float FloodLevel => RoadLevel(Chapel.x, Chapel.y) + 0.5f;

        public static float Height(float x, float z)
        {
            var p = new Vector2(x, z);
            float h = RoadLevel(x, z) + 0.6f * Noise(x, z, 22f, 5f) + 0.25f * Noise(x, z, 7f, 11f);

            // Arenas and the camp are flat.
            h = Flatten(h, p, Camp, 18f, 1.2f);
            h = FlattenEllipse(h, p, Speedway, new Vector2(36f, 25f), RoadLevel(Speedway.x, Speedway.y));
            h = Flatten(h, p, Lot, 34f, RoadLevel(Lot.x, Lot.y));
            h = Flatten(h, p, WitchClearing, 24f, RoadLevel(WitchClearing.x, WitchClearing.y));
            h = Flatten(h, p, Dojo, 15f, RoadLevel(Dojo.x, Dojo.y) + 1.2f);
            h = Flatten(h, p, School, 12f, RoadLevel(School.x, School.y));
            h = Flatten(h, p, HaulEnd, 10f, RoadLevel(HaulEnd.x, HaulEnd.y));

            // Roads are smoothed in.
            float road = DistanceToRoads(p);
            if (road < 6f) h = Mathf.Lerp(RoadLevel(x, z), h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3.2f, 6f, road)));
            float track = DistanceToShortcuts(p);
            if (track < 4.8f) h = Mathf.Lerp(RoadLevel(x, z), h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.6f, 4.8f, track)));

            // The creek and the bog are carved out after the roads, so bridges span the gaps.
            float creek = DistanceToPath(p, Creek);
            if (creek < 7f) h = Mathf.Min(h, Mathf.Lerp(-1.3f, h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3.5f, 7f, creek))));
            float bog = Ellipse(p, Bog, BogRadii);
            if (bog < 1.35f) h = Mathf.Min(h, Mathf.Lerp(-1.3f, h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1.35f, bog))));
            float shallows = Vector2.Distance(p, WitchClearing + new Vector2(0f, -9f));
            if (shallows < 9f) h = Mathf.Min(h, Mathf.Lerp(-0.9f, h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(5f, 9f, shallows))));
            // The chapel flood sits in a hollow (0.6 m under its water) with a low bank round its rim, so the water's
            // edge always meets the ground instead of hanging in the air where the land dips. The road dips into it
            // (the water blocks the way to the dojo until it drains).
            float flood = Ellipse(p, Chapel, FloodRadii);
            if (flood < 1.3f)
            {
                float floor = FloodLevel - 0.6f, bank = FloodLevel + 0.25f;
                if (flood <= 0.85f) h = Mathf.Min(h, floor);
                else if (flood <= 1f) h = Mathf.Lerp(floor, bank, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1f, flood)));
                else h = Mathf.Lerp(bank, h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 1.3f, flood)));
            }

            // The ridge ring: tall and too steep to climb. Its outer side drops to the beach (11-20).
            float r = p.magnitude;
            if (r > PlayRadius)
            {
                float down = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(196f, 214f, r));
                h += (Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PlayRadius, PlayRadius + 12f, r)) * 22f + Mathf.Max(0f, Noise(x, z, 9f, 3f)) * 4f) * down;
            }

            // The pass south of camp: the ridge opens into a level trail down to the bay.
            float pass = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(20f, 7f, Mathf.Abs(x))) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PassStart, PassStart - 7.5f, z));
            if (pass > 0f) h = Mathf.Lerp(h, PassFloor, pass);

            // The beach and the sea floor, all round the island and closer in at the bay.
            if (r > 180f)
            {
                float coast = r - Coast;
                float bay = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(26f, 10f, Mathf.Abs(x))) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-180f, -190f, z));
                coast = Mathf.Lerp(coast, -z - BayCoast, bay);
                h = IslandKit.Shore(h, coast, SeaLevel);
            }
            return h;
        }

        private static float Flatten(float h, Vector2 p, Vector2 centre, float radius, float level)
        {
            float d = Vector2.Distance(p, centre);
            return d >= radius + 6f ? h : Mathf.Lerp(level, h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(radius, radius + 6f, d)));
        }

        private static float FlattenEllipse(float h, Vector2 p, Vector2 centre, Vector2 radii, float level)
        {
            float e = Ellipse(p, centre, radii);
            return e >= 1.25f ? h : Mathf.Lerp(level, h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 1.25f, e)));
        }

        private static float Ellipse(Vector2 p, Vector2 centre, Vector2 radii)
        {
            Vector2 d = p - centre;
            return Mathf.Sqrt(d.x * d.x / (radii.x * radii.x) + d.y * d.y / (radii.y * radii.y));
        }

        private static float DistanceToRoads(Vector2 p)
        {
            float best = float.MaxValue;
            foreach (Vector2[] road in Roads) best = Mathf.Min(best, DistanceToPath(p, road));
            return best;
        }

        private static float DistanceToShortcuts(Vector2 p)
        {
            float best = float.MaxValue;
            foreach (Vector2[] track in Shortcuts) best = Mathf.Min(best, DistanceToPath(p, track));
            return best;
        }

        // Roads and the short cut tracks (for the ground; the scatter keeps using DistanceToRoads, see Shortcuts).
        private static float DistanceToAnyRoad(Vector2 p) => Mathf.Min(DistanceToRoads(p), DistanceToShortcuts(p));

        // Somewhere a quest piece goes: the short cuts, the Spirit Road, the quest buildings.
        private static bool QuestBlocked(Vector2 p)
        {
            if (DistanceToShortcuts(p) < 5f || DistanceToPath(p, new[] { SpiritA, SpiritB }) < SpiritWidth * 0.5f + 2f) return true;
            foreach (Vector3 c in QuestClear) if (Vector2.Distance(p, new Vector2(c.x, c.y)) < c.z) return true;
            return false;
        }

        private static float DistanceToPath(Vector2 p, Vector2[] path)
        {
            float best = float.MaxValue;
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector2 a = path[i], b = path[i + 1], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        // On the terrain as it is drawn and collides (11-20): the 4 m triangles, not the smooth Height() they were sampled
        // from. Everything set on the ground (props, trees, rocks, plants) uses this, so nothing floats or sinks on slopes.
        public static Vector3 GroundAt(Vector2 p) => new Vector3(p.x, MeshHeight(p.x, p.y), p.y);

        // ---------- The terrain grid (11-20: finer where you spend your time) ----------
        // The ground is 4 m cells, each split into two triangles along its a-c diagonal. Cells near camp, the trail to the
        // dock and the roads are split again into four 2 m cells. Heights live on a 2 m grid (fineGrid): the 4 m corners,
        // and the midpoints and centres of the fine cells. A midpoint on an edge shared with a coarse cell is the average of
        // the edge's two ends, so the fine and coarse triangles meet without cracks.
        private static float[,] fineGrid;
        private static bool[,] detailed;

        private static bool Detailed(Vector2 c) =>
            DistanceToAnyRoad(c) < 11f || Vector2.Distance(c, Camp) < 32f || (Mathf.Abs(c.x) < 14f && c.y < Camp.y && c.y > -214f);

        private static void EnsureGrid()
        {
            if (fineGrid != null) return;
            float step = MapSize / Cells, half = step * 0.5f, origin = -MapSize * 0.5f;
            detailed = new bool[Cells, Cells];
            for (int i = 0; i < Cells; i++)
                for (int j = 0; j < Cells; j++)
                    detailed[i, j] = Detailed(new Vector2(origin + (i + 0.5f) * step, origin + (j + 0.5f) * step));
            // Every cell on the map that shares the edge is a fine cell.
            bool AllFine(int i1, int j1, int i2, int j2)
            {
                bool any = false;
                foreach ((int i, int j) in new[] { (i1, j1), (i2, j2) })
                {
                    if (i < 0 || j < 0 || i >= Cells || j >= Cells) continue;
                    if (!detailed[i, j]) return false;
                    any = true;
                }
                return any;
            }
            int n = Cells * 2;
            fineGrid = new float[n + 1, n + 1];
            for (int I = 0; I <= n; I++)
                for (int J = 0; J <= n; J++)
                {
                    float x = origin + I * half, z = origin + J * half;
                    bool oddI = I % 2 == 1, oddJ = J % 2 == 1;
                    if (!oddI && !oddJ || oddI && oddJ) { fineGrid[I, J] = Height(x, z); continue; }
                    // An edge midpoint: fine only if every cell sharing the edge is fine.
                    bool fine;
                    if (oddI) fine = AllFine((I - 1) / 2, J / 2, (I - 1) / 2, J / 2 - 1);   // a midpoint on an x-edge: the cells above and below
                    else fine = AllFine(I / 2, (J - 1) / 2, I / 2 - 1, (J - 1) / 2);        // on a z-edge: the cells right and left
                    fineGrid[I, J] = fine ? Height(x, z) : oddI ? (Height(x - half, z) + Height(x + half, z)) * 0.5f : (Height(x, z - half) + Height(x, z + half)) * 0.5f;
                }
        }

        /// <summary>The height of the terrain mesh at (x, z): the same cells, fine cells and diagonals as BuildTerrain.</summary>
        public static float MeshHeight(float x, float z)
        {
            EnsureGrid();
            float step = MapSize / Cells, origin = -MapSize * 0.5f;
            float gx = Mathf.Clamp((x - origin) / step, 0f, Cells - 0.0001f), gz = Mathf.Clamp((z - origin) / step, 0f, Cells - 0.0001f);
            int ci = (int)gx, cj = (int)gz;
            int I, J, span;
            float fx, fz;
            if (detailed[ci, cj])
            {
                float hx = gx * 2f, hz = gz * 2f;
                I = (int)hx; J = (int)hz; fx = hx - I; fz = hz - J; span = 1;
            }
            else { I = ci * 2; J = cj * 2; fx = gx - ci; fz = gz - cj; span = 2; }
            float a = fineGrid[I, J], b = fineGrid[I + span, J], c = fineGrid[I + span, J + span], d = fineGrid[I, J + span];
            return fx >= fz ? a + (b - a) * fx + (c - b) * fz : a + (c - d) * fx + (d - a) * fz;
        }

        // The terrain's slope normal round p (over a few metres, so one odd triangle doesn't decide it).
        private static Vector3 SlopeNormal(Vector2 p, float reach = 1.5f)
        {
            float hx = (MeshHeight(p.x + reach, p.y) - MeshHeight(p.x - reach, p.y)) / (2f * reach);
            float hz = (MeshHeight(p.x, p.y + reach) - MeshHeight(p.x, p.y - reach)) / (2f * reach);
            return new Vector3(-hx, 1f, -hz).normalized;
        }

        /// <summary>
        /// Where a tree, rock, stump or log stands (11-20): on the terrain as drawn, a little sunk, with the ground's slope
        /// under it so MudScenery drapes the bottom of the model (a tree's flared base and knees, a whole rock or log)
        /// onto the bank. Trunks stay upright, so nothing leans out over air.
        /// </summary>
        private static (Vector3 at, Vector2 slope) Footing(MudModels.Kind kind, Vector2 p, float scale)
        {
            Vector3 normal = SlopeNormal(p, 1.5f);
            var slope = new Vector2(-normal.x / normal.y, -normal.z / normal.y);
            float sink = 0.2f + Mathf.Min(0.4f, slope.magnitude * 0.25f);
            return (new Vector3(p.x, MeshHeight(p.x, p.y) - sink, p.y), slope);
        }

        private static Ground Surface(Vector3 centre, Vector3 normal)
        {
            var p = new Vector2(centre.x, centre.z);
            if (normal.y < 0.6f) return Ground.Cliff;
            if (normal.y < 0.8f) return Ground.Rock;
            // The beach and the sea floor outside the ridge, and the trail from camp down through the pass (11-20).
            if (p.magnitude > PlayRadius + 8f || (p.y < PassStart - 1f && Mathf.Abs(p.x) < 24f))
            {
                if (centre.y < SeaLevel - 0.1f) return Ground.SeaFloor;
                if (centre.y < SeaLevel + 1.7f) return Ground.Sand;
            }
            if (centre.y < WaterY - 0.2f) return Ground.Bed;
            // Roads, the trail, the go-kart track, the lot and the camp yard are drawn over the terrain as smooth-edged
            // overlays (BuildRoadSurfaces, 11-20); colouring the 4 m triangles gave them sawtooth edges.
            // Mud patches, the flood's bed and the muddy shores are overlays too (BuildRoadSurfaces, 11-20).
            if (p.magnitude > PlayRadius - 6f) return Ground.DarkGrass;
            float patch = Noise(p.x, p.y, 18f, 23f);
            return patch > 0.3f ? Ground.Moss : patch < -0.25f ? Ground.DarkGrass : Ground.Grass;
        }

        private static Color GroundColor(Ground ground) => ground switch
        {
            // Alien ground cover: teal grass and violet moss (matches MudPalette).
            Ground.Grass => new Color(0.19f, 0.4f, 0.38f),
            Ground.DarkGrass => new Color(0.13f, 0.26f, 0.3f),
            Ground.Moss => new Color(0.38f, 0.31f, 0.5f),
            Ground.Road => new Color(0.45f, 0.34f, 0.22f),
            Ground.Mud => new Color(0.24f, 0.17f, 0.11f),
            Ground.Bed => new Color(0.16f, 0.13f, 0.1f),
            Ground.Rock => new Color(0.38f, 0.4f, 0.38f),
            Ground.Sand => new Color(0.4f, 0.35f, 0.27f),      // grey-brown swamp sand
            Ground.SeaFloor => new Color(0.46f, 0.41f, 0.3f),  // under the clear sea
            _ => new Color(0.3f, 0.31f, 0.3f)
        };

        // ---------- Terrain and water ----------

        private static GameObject BuildTerrain(Transform level)
        {
            EnsureGrid();
            float half = MapSize / Cells * 0.5f, origin = -MapSize * 0.5f;
            Vector3 At(int I, int J) => new Vector3(origin + I * half, fineGrid[I, J], origin + J * half);
            var kit = new MeshKit();
            void Quad(int I, int J, int span)
            {
                Vector3 a = At(I, J), b = At(I + span, J), c = At(I + span, J + span), d = At(I, J + span);
                Facet(kit, a, b, c); // split along a-c (MeshHeight assumes it)
                Facet(kit, a, c, d);
            }
            for (int i = 0; i < Cells; i++)
                for (int j = 0; j < Cells; j++)
                {
                    if (!detailed[i, j]) { Quad(i * 2, j * 2, 2); continue; }
                    for (int u = 0; u < 2; u++)
                        for (int v = 0; v < 2; v++) Quad(i * 2 + u, j * 2 + v, 1);
                }
            Mesh mesh = SaveMesh(kit.Build("MudlandsTerrain"), Folder + "/Meshes/MudlandsTerrain.asset");
            var terrain = new GameObject("Terrain");
            terrain.transform.SetParent(level, false);
            terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
            var materials = new Material[kit.SubmeshCount];
            for (int s = 0; s < materials.Length; s++) materials[s] = GroundMaterial(kit.SubmeshLook(s).color);
            terrain.AddComponent<MeshRenderer>().sharedMaterials = materials;
            terrain.AddComponent<MeshCollider>().sharedMesh = mesh;
            terrain.isStatic = true;
            return terrain;
        }

        // A triangle wound to face up, coloured by its surface.
        private static void Facet(MeshKit kit, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            if (normal.y < 0f) { (b, c) = (c, b); normal = -normal; }
            Vector3 centre = (a + b + c) / 3f;
            kit.Tri(a, b, c, Shade(GroundColor(Surface(centre, normal)), centre));
        }

        // A slightly lighter or darker take on a ground colour for each facet (three steps, by position), so the low-poly
        // ground reads as facets rather than flat colour fields.
        private static Color Shade(Color color, Vector3 at)
        {
            float hash = Mathf.Abs(Mathf.Sin(at.x * 12.9898f + at.z * 78.233f) * 43758.547f) % 1f;
            float shade = hash < 0.33f ? 0.93f : hash < 0.66f ? 1f : 1.06f;
            return new Color(Mathf.Round(color.r * shade * 255f) / 255f, Mathf.Round(color.g * shade * 255f) / 255f, Mathf.Round(color.b * shade * 255f) / 255f, 1f);
        }

        private static readonly Dictionary<Color, Material> GroundMaterials = new Dictionary<Color, Material>();

        private static Material GroundMaterial(Color color)
        {
            if (GroundMaterials.TryGetValue(color, out Material found) && found != null) return found;
            string path = Folder + "/Materials/Ground_" + ColorUtility.ToHtmlStringRGB(color) + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.06f);
            EditorUtility.SetDirty(material);
            GroundMaterials[color] = material;
            return material;
        }

        private static void BuildWater(Transform level)
        {
            var water = new GameObject("Water").transform;
            water.SetParent(level, false);
            Material material = WaterMaterial();
            // The bog, the creek, and the shallows in the witch's clearing: still, murky water you can wade.
            AddWaterDisc(water, "Bog Water", new Vector3(Bog.x, WaterY, Bog.y), BogRadii * 1.3f, material);
            AddWaterDisc(water, "Clearing Shallows", new Vector3(WitchClearing.x, WaterY, WitchClearing.y - 9f), new Vector2(8f, 8f), material);
            Mesh creek = SaveMesh(OpenWater(CreekMesh()), Folder + "/Meshes/CreekWater.asset");
            var creekGo = new GameObject("Creek Water");
            creekGo.transform.SetParent(water, false);
            creekGo.AddComponent<MeshFilter>().sharedMesh = creek;
            creekGo.AddComponent<MeshRenderer>().sharedMaterial = material;

            // Bog-water zones: slow on foot, stall the car.
            AddZone(water, "Bog Zone", new Vector3(Bog.x, WaterY, Bog.y), BogRadii * 0.95f, MudZone.Kind.BogWater, 0.55f);
            AddZone(water, "Shallows Zone", new Vector3(WitchClearing.x, WaterY, WitchClearing.y - 9f), new Vector2(6f, 6f), MudZone.Kind.BogWater, 0.45f);
            for (int i = 0; i < Creek.Length - 1; i++)
            {
                float length = Vector2.Distance(Creek[i], Creek[i + 1]);
                for (float d = 0f; d < length; d += 6f)
                {
                    Vector2 p = Vector2.Lerp(Creek[i], Creek[i + 1], d / length);
                    if (DistanceToRoads(p) < 6f) continue; // the bridges stay dry
                    AddZone(water, "Creek Zone", new Vector3(p.x, WaterY, p.y), new Vector2(3.2f, 3.2f), MudZone.Kind.BogWater, 0.5f);
                }
            }
        }

        private static void AddWaterDisc(Transform parent, string name, Vector3 centre, Vector2 radii, Material material)
        {
            var kit = new MeshKit();
            const int sides = 40;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                kit.Tri(Vector3.zero, new Vector3(Mathf.Cos(a1) * radii.x, 0f, Mathf.Sin(a1) * radii.y), new Vector3(Mathf.Cos(a0) * radii.x, 0f, Mathf.Sin(a0) * radii.y), Color.white);
            }
            Mesh mesh = SaveMesh(OpenWater(kit.Build(name.Replace(" ", ""))), Folder + "/Meshes/" + name.Replace(" ", "") + ".asset");
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // The water shader reads vertex colour red as "how close to the shore" (white = foam everywhere). These
        // water sheets run under the banks, so they are all open water: a little shallow tint, no foam.
        private static Mesh OpenWater(Mesh mesh)
        {
            var colors = new Color[mesh.vertexCount];
            for (int i = 0; i < colors.Length; i++) colors[i] = new Color(0.12f, 0f, 0f, 1f);
            mesh.colors = colors;
            return mesh;
        }

        private static Mesh CreekMesh()
        {
            var kit = new MeshKit();
            for (int i = 0; i < Creek.Length - 1; i++)
            {
                Vector2 a = Creek[i], b = Creek[i + 1];
                Vector2 side = new Vector2(-(b - a).y, (b - a).x).normalized * 5f;
                Vector3 A(Vector2 v) => new Vector3(v.x, WaterY, v.y);
                kit.Quad(A(a + side), A(b + side), A(b - side), A(a - side), Color.white);
                kit.Quad(A(a - side), A(b - side), A(b + side), A(a + side), Color.white);
            }
            return kit.Build("CreekWater");
        }

        private static void AddZone(Transform parent, string name, Vector3 at, Vector2 radii, MudZone.Kind kind, float slow)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.AddComponent<MudZone>().Configure(kind, radii, slow);
        }

        private static Material WaterMaterial()
        {
            string path = Folder + "/Materials/SwampWater.mat";
            Shader shader = Shader.Find("Signal Lost/Stylized Water");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            if (shader != null)
            {
                material.shader = shader;
                // Dark, murky swamp water (11-20, Christian: the sea outside stays clear, the water in the level is dark):
                // olive-brown and nearly opaque a hand's depth down, no caustics or shore wash lines, a dull greenish
                // scum where it meets the banks instead of white foam, barely any swell and few sparkles.
                // 18:31: "too murky ... more of a blue grey brown water, rn it pure black": lighter, a blue-grey murk
                // over brown shallows, still not clear.
                material.SetColor("_DeepColor", new Color(0.2f, 0.25f, 0.28f, 0.94f));
                material.SetColor("_ShallowColor", new Color(0.36f, 0.34f, 0.27f, 0.82f));
                material.SetColor("_HorizonColor", new Color(0.42f, 0.5f, 0.52f, 1f));
                material.SetColor("_FoamColor", new Color(0.5f, 0.52f, 0.46f, 1f));
                material.SetFloat("_WaveHeight", 0.012f);
                material.SetFloat("_WaveScale", 0.18f);
                material.SetFloat("_WaveSpeed", 0.3f);
                material.SetFloat("_Sparkle", 0.08f);
                material.SetFloat("_Smoothness", 0.2f); // low: the moon sits in front of most views, and a glossy surface turns into a white sheet
                material.SetFloat("_FoamWidth", 0.04f);
                material.SetFloat("_UseDepth", 1f);
                material.SetFloat("_DepthFade", 1.2f);
                material.SetFloat("_Clarity", 0.35f);
                material.SetFloat("_EdgeFoam", 0.12f);
                material.SetFloat("_WashLines", 0f);
                material.SetFloat("_CausticStrength", 0f);
                material.SetFloat("_Facets", 0.4f);
            }
            else
            {
                material.SetColor("_BaseColor", new Color(0.1f, 0.22f, 0.2f, 0.85f));
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- The landing: the sea, the swamp dock and the pass (11-20) ----------

        // Where the trail from camp comes down to the bay, and the line it follows (straight south, x = 0).
        private static readonly Vector2 BayTop = new Vector2(0f, -186f);

        /// <summary>
        /// The sea round the island (clear, the same as every island's), the arrival dock in the bay below camp (the boat
        /// from the Desert Dunes comes in here) dressed for the swamp, lanterns down the pass, and the dressing round the
        /// bay. Runs after the scenery and before the NavMesh bake, with its own random numbers, so nothing inland moves.
        /// </summary>
        private static void BuildLanding(World world)
        {
            var root = new GameObject("Landing").transform;
            root.SetParent(world.Level, false);
            IslandKit.BuildOcean(root, SeaLevel, Vector2.zero, MapSize * 0.5f, 2f, Height, Vector3.zero, "MudlandsOcean");

            Vector2 start = IslandKit.ShoreStart(BayTop, Vector2.down, Height, SeaLevel);
            global::SignalLost.World.IslandDock dock = IslandKit.BuildDock(root, new IslandKit.DockSpec
            {
                Name = "MudlandsDock", Start = start, Seaward = Vector2.down, Length = 16f,
                Role = global::SignalLost.World.IslandDock.Role.Arrival, IslandName = "The Mudlands", Look = IslandKit.DockLook.Swamp
            }, SeaLevel, Height, 20261120);
            DockMoss(dock.transform);
            var noParking = new GameObject("No Parking (dock)");
            noParking.transform.SetParent(dock.transform, false);
            noParking.transform.localPosition = new Vector3(0f, 0f, 9f);
            noParking.AddComponent<NoParkingZone>().Configure(14f, "No parking on the dock. It's barely holding itself up.");

            // Nothing walks the sea floor: the ground under the water outside the ridge is left out of the NavMesh (it never
            // reaches inland, where the creek and bog beds stay walkable).
            float floor = SeaLevel - 0.5f, depth = 30f, far = MapSize * 0.5f + 10f, edge = 185f;
            foreach ((Vector2 centre, Vector2 size) in new[]
            {
                (new Vector2(0f, -(edge + far) * 0.5f), new Vector2(far * 2f, far - edge)), (new Vector2(0f, (edge + far) * 0.5f), new Vector2(far * 2f, far - edge)),
                (new Vector2(-(edge + far) * 0.5f, 0f), new Vector2(far - edge, far * 2f)), (new Vector2((edge + far) * 0.5f, 0f), new Vector2(far - edge, far * 2f))
            })
            {
                var volume = new GameObject("Sea Floor (not walkable)").AddComponent<NavMeshModifierVolume>();
                volume.transform.SetParent(root, false);
                volume.transform.position = new Vector3(centre.x, floor - depth * 0.5f, centre.y);
                volume.size = new Vector3(size.x, depth, size.y);
                volume.center = Vector3.zero;
                volume.area = NavMesh.GetAreaFromName("Not Walkable");
            }

            // Props: a sign at camp, lanterns down the trail, torches at the foot of the dock, a rowboat pulled up on
            // the sand. Their own random numbers and clear list, so the inland props keep their looks.
            var props = new World { Level = root, Random = new System.Random(20261121), Clear = new List<Vector3>() };
            Prop(props, MudModels.Kind.RoadSign, Camp + new Vector2(3.6f, -15f), 180f, "Sign", sign: "DOCK", parent: root);
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                Vector2 at = new Vector2(side * 3.4f, Camp.y - 22f - i * 6.5f);
                Prop(props, MudModels.Kind.LanternPost, at, Yaw(at, new Vector2(0f, at.y)), "Trail Lantern", parent: root);
            }
            foreach (float side in new[] { -1f, 1f })
                Prop(props, MudModels.Kind.TikiTorch, start + new Vector2(side * 2.4f, 1.2f), 0f, "Dock Torch", parent: root);
            Prop(props, MudModels.Kind.RoadSign, start + new Vector2(4.2f, 3.5f), 0f, "Sign", sign: "NO SWIMMING", parent: root);
            Prop(props, MudModels.Kind.Rowboat, start + new Vector2(9f, 4.5f), 28f, "Beached Rowboat", parent: root, yOffset: -0.25f);

            // Swamp dressing round the bay: mossy cypresses on the slopes either side, reeds and cattails along the
            // waterline, a few glowing mushrooms, lily pads off to the sides (clear of the boat's way in).
            var scenery = new GameObject("Landing Scenery").AddComponent<MudScenery>();
            scenery.transform.SetParent(root, false);
            var colliders = new GameObject("Landing Scenery Colliders").transform;
            colliders.SetParent(root, false);
            var random = new System.Random(20261122);
            float R(float a, float b) => a + (float)random.NextDouble() * (b - a);
            void Tree(MudModels.Kind kind, Vector2 p, float scale)
            {
                (Vector3 at, Vector2 slope) = Footing(kind, p, scale);
                scenery.Place(kind, random.Next(4), at, R(0f, 360f), scale, slope);
                foreach ((Vector3 centre, Vector3 size) in MudModels.Colliders(kind))
                {
                    var go = new GameObject(kind + " Collider");
                    go.transform.SetParent(colliders, false);
                    go.transform.position = at;
                    go.transform.localScale = Vector3.one * scale;
                    var box = go.AddComponent<BoxCollider>();
                    box.center = centre;
                    box.size = size;
                }
            }
            foreach (Vector2 at in new[] { new Vector2(-11f, -190f), new Vector2(12f, -193f), new Vector2(-15f, -199f), new Vector2(16f, -201f), new Vector2(-9f, -181f), new Vector2(10f, -183f) })
                Tree(MudModels.Kind.Cypress, at + new Vector2(R(-1f, 1f), R(-1f, 1f)), R(0.95f, 1.2f));
            Tree(MudModels.Kind.DeadTree, new Vector2(-19f, -206f), 1.1f);
            Tree(MudModels.Kind.Willow, new Vector2(20f, -197f), 1f);
            for (int i = 0; i < 260; i++)
            {
                Vector2 p = new Vector2(R(-30f, 30f), R(-232f, -176f));
                if (Mathf.Abs(p.x) < 4f && p.y > start.y - 2f) continue; // the trail
                if (p.x > -12f && p.x < 4f && p.y < start.y + 1f) continue; // the dock and the boat's way in
                float h = Height(p.x, p.y);
                if (h < SeaLevel - 0.6f)
                {
                    if (random.NextDouble() < 0.25f) scenery.Place(MudModels.Kind.LilyPads, random.Next(4), new Vector3(p.x, SeaLevel, p.y), R(0f, 360f), R(0.8f, 1.3f));
                }
                else if (h < SeaLevel + 0.15f) scenery.Place(MudModels.Kind.Cattails, random.Next(4), GroundAt(p), R(0f, 360f), R(0.8f, 1.3f));
                else if (h < SeaLevel + 1.3f && random.NextDouble() < 0.35f) scenery.Place(MudModels.Kind.Reeds, random.Next(4), GroundAt(p), R(0f, 360f), R(0.8f, 1.2f));
                else if (h < PassFloor + 4f && Mathf.Abs(p.x) > 6f && random.NextDouble() < 0.12f) scenery.Place(MudModels.Kind.Mushrooms, random.Next(4), GroundAt(p), R(0f, 360f), R(0.8f, 1.4f));
            }
            scenery.Generate();

            // No grass on the trail.
            for (float z = Camp.y - 10f; z > PassStart; z -= 3f) world.Clear.Add(new Vector3(0f, z, 3f));
        }

        // Violet hanging moss on the dock's standing posts (the same strips as on the swamp trees), in the dock's space.
        // The posts are where IslandKit.BuildDock puts them: every other pair along the walk, and the far corners.
        private static void DockMoss(Transform dock)
        {
            var kit = new MeshKit();
            var moss = new Color(0.52f, 0.4f, 0.64f);
            void Strands(Vector3 top, float length, float side)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector3 t = top + new Vector3(side * 0.12f, 0f, i * 0.13f - 0.13f);
                    kit.Plank(t, t + new Vector3(side * (0.04f + 0.03f * i), -length + i * 0.25f, 0.03f * i), 0.11f, 0.02f, moss);
                }
            }
            for (int row = 1; row * 3f + 0.4f < 16f; row += 2)
                foreach (float side in new[] { -1f, 1f })
                    Strands(new Vector3(side * 1.5f, 0.55f, row * 3f + 0.4f), 1.3f, side);
            foreach (float side in new[] { -1f, 1f }) Strands(new Vector3(side * 3.3f, 0.85f, 20.9f), 1.6f, side);
            Mesh mesh = SaveMesh(kit.Build("DockMoss"), Folder + "/Meshes/DockMoss.asset");
            var go = new GameObject("Hanging Moss");
            go.transform.SetParent(dock, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var materials = new Material[kit.SubmeshCount];
            for (int s = 0; s < materials.Length; s++) materials[s] = GroundMaterial(kit.SubmeshLook(s).color);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ---------- Road surfaces (11-20) ----------

        /// <summary>
        /// The roads, the trail down to the dock, the go-kart track, the lot and the camp yard, drawn as thin overlays
        /// following the terrain 8 cm above it, with smooth slightly wobbly edges. (They used to be the terrain's own 4 m
        /// triangles coloured brown, which gave every road a sawtooth edge.) No collider: you walk and drive on the
        /// terrain just under them. They stop at the bridges and wherever the ground dips under the road (the fords).
        /// </summary>
        private static void BuildRoadSurfaces(World world)
        {
            var kit = new MeshKit();
            Color dirt = GroundColor(Ground.Road), verge = new Color(0.29f, 0.23f, 0.17f), mud = GroundColor(Ground.Mud);
            foreach (Vector2[] road in Roads) Ribbon(kit, road, 3.3f, 1.6f, dirt, verge, true);
            foreach (Vector2[] track in Shortcuts) Ribbon(kit, track, 2.4f, 1.2f, dirt, verge, true);
            Vector2 trailEnd = IslandKit.ShoreStart(BayTop, Vector2.down, Height, SeaLevel);
            Ribbon(kit, new[] { Camp + new Vector2(0f, -8.6f), new Vector2(0f, PassStart), BayTop, trailEnd }, 1.9f, 1f, dirt, verge, false);
            Pad(kit, Camp, new Vector2(9f, 9f), 0f, 1f, dirt);
            Pad(kit, Lot, new Vector2(30f, 30f), 0f, 1f, dirt);
            Pad(kit, Speedway, new Vector2(22f, 13f), 0.78f, 1.22f, dirt);
            // Soft mud (11-20): the mud patches and the chapel flood's bed as smooth-edged pads, and a wet, blotchy band of
            // mud along every shore of the swamp water (the bog, the creek, the shallows), clear of the roads' verges.
            foreach ((Vector2 c, Vector2 radii) in MudPatches) Pad(kit, c, radii * 1.05f, 0f, 1f, mud, true);
            Pad(kit, Chapel, FloodRadii * 0.97f, 0f, 1f, mud, true);
            Shores(kit, mud);
            Mesh mesh = SaveMesh(kit.Build("RoadSurfaces"), Folder + "/Meshes/RoadSurfaces.asset");
            var go = new GameObject("Road Surfaces");
            go.transform.SetParent(world.Level, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var materials = new Material[kit.SubmeshCount];
            for (int s = 0; s < materials.Length; s++) materials[s] = GroundMaterial(kit.SubmeshLook(s).color);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            go.isStatic = true;
        }

        private const float SurfaceLift = 0.08f;

        private static Vector3 OnSurface(Vector2 p) => new Vector3(p.x, MeshHeight(p.x, p.y) + SurfaceLift, p.y);

        private static void UpTri(MeshKit kit, Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            if (Vector3.Cross(b - a, c - a).y < 0f) (b, c) = (c, b);
            kit.Tri(a, b, c, color);
        }

        private static bool OnBridge(Vector2 p)
        {
            foreach ((Vector2 a, Vector2 b) in BridgeSpans)
            {
                Vector2 ab = b - a;
                float length = ab.magnitude;
                if (length < 0.01f) continue;
                Vector2 dir = ab / length;
                float t = Vector2.Dot(p - a, dir), side = Mathf.Abs(dir.x * (p - a).y - dir.y * (p - a).x);
                if (t > -0.2f && t < length + 0.2f && side < 4f) return true;
            }
            return false;
        }

        // A strip along a path, 1 m steps, five lanes across (so it follows the ground's bends), edges wobbling a little,
        // with a band of darker trodden mud (the verge) along each side whose outer edge wobbles more.
        private static void Ribbon(MeshKit kit, Vector2[] path, float halfWidth, float vergeWidth, Color color, Color vergeColor, bool atRoadLevel)
        {
            var points = new List<Vector2>();
            for (int i = 0; i < path.Length - 1; i++)
            {
                float length = Vector2.Distance(path[i], path[i + 1]);
                int steps = Mathf.Max(1, Mathf.CeilToInt(length));
                for (int k = 0; k < steps; k++) points.Add(Vector2.Lerp(path[i], path[i + 1], k / (float)steps));
            }
            points.Add(path[path.Length - 1]);
            float[] lanes = { -1f, -1f, -0.5f, 0f, 0.5f, 1f, 1f }; // the first and last are the verges' outer edges
            var rows = new Vector3[points.Count][];
            var keep = new bool[points.Count];
            for (int k = 0; k < points.Count; k++)
            {
                Vector2 dir = (points[Mathf.Min(k + 1, points.Count - 1)] - points[Mathf.Max(k - 1, 0)]).normalized;
                var side = new Vector2(-dir.y, dir.x);
                Vector2 p = points[k];
                float ground = MeshHeight(p.x, p.y);
                keep[k] = !OnBridge(p) && (atRoadLevel ? ground >= RoadLevel(p.x, p.y) - 0.6f : ground >= SeaLevel + 0.3f);
                rows[k] = new Vector3[lanes.Length];
                for (int m = 0; m < lanes.Length; m++)
                {
                    float offset = lanes[m] * halfWidth;
                    if (m == 1 || m == lanes.Length - 2) offset *= 1f + 0.07f * Noise(p.x, p.y, 4f, 31f + m); // the road's edge
                    if (m == 0 || m == lanes.Length - 1)
                    {
                        float edge = halfWidth * (1f + 0.07f * Noise(p.x, p.y, 4f, 31f + (m == 0 ? 1 : lanes.Length - 2)));
                        offset = Mathf.Sign(lanes[m]) * (edge + vergeWidth * (1f + 0.45f * Noise(p.x, p.y, 3f, 71f + m)));
                    }
                    Vector2 at = p + side * offset;
                    // The verge's outer edge sits a little lower, so where it runs under another road that road wins.
                    rows[k][m] = m == 0 || m == lanes.Length - 1 ? new Vector3(at.x, MeshHeight(at.x, at.y) + 0.05f, at.y) : OnSurface(at);
                }
            }
            for (int k = 0; k < points.Count - 1; k++)
            {
                if (!keep[k] || !keep[k + 1]) continue;
                for (int m = 0; m < lanes.Length - 1; m++)
                {
                    bool isVerge = m == 0 || m == lanes.Length - 2;
                    if (isVerge && InYard((points[k] + points[k + 1]) * 0.5f)) continue; // no verge across the camp yard or the lot
                    Color c = isVerge ? vergeColor : color;
                    UpTri(kit, rows[k][m], rows[k + 1][m], rows[k + 1][m + 1], c);
                    UpTri(kit, rows[k][m], rows[k + 1][m + 1], rows[k][m + 1], c);
                }
            }
        }

        // Wet mud along the swamp's shores: 1 m squares following the ground, wherever it is within a hand or two of the
        // water's level (with a little noise, so the band's edge is blotchy rather than a contour line). Under the water
        // it shows through as muddy shallows. Kept off the roads and their verges.
        private static void Shores(MeshKit kit, Color mud)
        {
            const float cell = 1f;
            int extent = Mathf.CeilToInt(PlayRadius);
            for (int ix = -extent; ix < extent; ix++)
                for (int iz = -extent; iz < extent; iz++)
                {
                    var p = new Vector2((ix + 0.5f) * cell, (iz + 0.5f) * cell);
                    if (p.magnitude > PlayRadius) continue;
                    float band = MeshHeight(p.x, p.y) - WaterY + 0.18f * Noise(p.x, p.y, 3f, 61f);
                    if (band < -0.35f || band > 0.5f) continue;
                    if (DistanceToAnyRoad(p) < 6f || OnBridge(p)) continue;
                    Vector2 o = p - Vector2.one * (cell * 0.5f);
                    Vector3 a = OnShore(o), b = OnShore(o + new Vector2(cell, 0f)), c = OnShore(o + new Vector2(cell, cell)), d = OnShore(o + new Vector2(0f, cell));
                    UpTri(kit, a, b, c, mud);
                    UpTri(kit, a, c, d, mud);
                }
        }

        private static bool InYard(Vector2 p) => Vector2.Distance(p, Camp) < 10f || Vector2.Distance(p, Lot) < 31f;

        private static Vector3 OnShore(Vector2 p) => new Vector3(p.x, MeshHeight(p.x, p.y) + 0.06f, p.y);

        // An (elliptical) disc, or a ring between inner and outer (fractions of the radii), with a wobbling rim.
        private static void Pad(MeshKit kit, Vector2 centre, Vector2 radii, float inner, float outer, Color color, bool offRoads = false)
        {
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 mid = (a + b + c) / 3f;
                if (offRoads && DistanceToAnyRoad(new Vector2(mid.x, mid.z)) < 6f) return; // a road and its verge go over it
                UpTri(kit, a, b, c, color);
            }
            const int segments = 72, rings = 5;
            var grid = new Vector3[segments, rings + 1];
            for (int s = 0; s < segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                var unit = new Vector2(Mathf.Cos(a) * radii.x, Mathf.Sin(a) * radii.y);
                for (int r = 0; r <= rings; r++)
                {
                    float f = Mathf.Lerp(inner, outer, r / (float)rings);
                    if (r == rings || (r == 0 && inner > 0f)) f *= 1f + 0.03f * Noise(unit.x * 3f, unit.y * 3f, 4f, 47f + r);
                    grid[s, r] = OnSurface(centre + unit * f);
                }
            }
            for (int s = 0; s < segments; s++)
            {
                int n = (s + 1) % segments;
                for (int r = 0; r < rings; r++)
                {
                    if (inner <= 0f && r == 0) { Tri(OnSurface(centre), grid[s, 1], grid[n, 1]); continue; }
                    Tri(grid[s, r], grid[n, r], grid[n, r + 1]);
                    Tri(grid[s, r], grid[n, r + 1], grid[s, r + 1]);
                }
            }
        }

        // ---------- The camp ----------

        private static Transform BuildCamp(Transform level, List<Vector3> clear)
        {
            Transform camp = Instance(BaseCampBuilder.PrefabPath, level).transform;
            camp.position = GroundAt(Camp);
            // The last island so far: every earlier level's stands join the tutorial's, plus Clutch's.
            foreach (var set in new[] { BaseCampBuilder.StandSet.Desert, BaseCampBuilder.StandSet.Caves, BaseCampBuilder.StandSet.Mudlands })
                BaseCampBuilder.AddStands(camp, set);
            clear.Add(new Vector3(Camp.x, Camp.y, 12f));
            var noParking = new GameObject("No Parking (camp)");
            noParking.transform.SetParent(camp, false);
            noParking.AddComponent<NoParkingZone>().Configure(16f, "No parking at camp. Everyone's sleeping.");
            return camp;
        }

        // ---------- Buildings and solid props (before the NavMesh bake) ----------

        private static MudProp Prop(World world, MudModels.Kind kind, Vector2 at, float yaw, string name = null, float size = 1f, string sign = "",
            Transform parent = null, float yOffset = 0f, bool solid = true)
        {
            var go = new GameObject(name ?? kind.ToString());
            go.transform.SetParent(parent != null ? parent : world.Level, false);
            go.transform.SetPositionAndRotation(GroundAt(at) + Vector3.up * yOffset, Quaternion.Euler(0f, yaw, 0f));
            var prop = go.AddComponent<MudProp>();
            prop.Configure(kind, world.Random.Next(4), size, sign);
            if (solid) AddColliders(go, kind, size);
            world.Clear.Add(new Vector3(at.x, at.y, 3f + size * 2f));
            return prop;
        }

        // Quest pieces (11-20) draw their variants from their own random numbers and add no clearings, so adding them
        // changes no other prop's variant and no tree's place.
        private static System.Random questRandom = new System.Random(20261120);

        private static MudProp QuestProp(World world, MudModels.Kind kind, Vector2 at, float yaw, string name, string sign = "", Transform parent = null,
            float yOffset = 0f, bool solid = true, float size = 1f)
        {
            System.Random saved = world.Random;
            List<Vector3> clear = world.Clear;
            world.Random = questRandom;
            world.Clear = new List<Vector3>();
            try { return Prop(world, kind, at, yaw, name, size, sign, parent, yOffset, solid); }
            finally
            {
                world.Random = saved;
                world.Clear = clear;
            }
        }

        // Boxes shaped to the model (MudModels.Solids). Rotated boxes (sloped roofs) and roof pieces go on child
        // objects; roof pieces are left out of the NavMesh so nothing paths, spawns or drops onto a roof.
        private static void AddColliders(GameObject go, MudModels.Kind kind, float size)
        {
            foreach (MudModels.Solid solid in MudModels.Solids(kind, size))
            {
                if (solid.angles == Vector3.zero && !solid.roof)
                {
                    var box = go.AddComponent<BoxCollider>();
                    box.center = solid.centre;
                    box.size = solid.size;
                    continue;
                }
                var part = new GameObject(solid.roof ? "Roof Collider" : "Collider");
                part.layer = go.layer;
                part.transform.SetParent(go.transform, false);
                part.transform.localPosition = solid.centre;
                part.transform.localRotation = Quaternion.Euler(solid.angles);
                part.AddComponent<BoxCollider>().size = solid.size;
                if (solid.roof) part.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            }
        }

        private static float Yaw(Vector2 from, Vector2 to) => Mathf.Atan2(to.x - from.x, to.y - from.y) * Mathf.Rad2Deg;

        private static void BuildSolidProps(World world)
        {
            var props = new GameObject("Buildings").transform;
            props.SetParent(world.Level, false);

            // Camp: boardwalk north to the road, torches.
            // The boardwalk north to the road: one straight run (two pieces end to end) from the camp yard onto the road,
            // tilted to sit on the ground at both ends (11-20: it was two level pieces with a step between them).
            {
                Vector2 from = Camp + new Vector2(0f, 7f), to = Camp + new Vector2(0f, 19.5f);
                var a = new Vector3(from.x, MeshHeight(from.x, from.y) + SurfaceLift + 0.04f, from.y);
                var b = new Vector3(to.x, MeshHeight(to.x, to.y) + SurfaceLift + 0.04f, to.y);
                Quaternion rotation = Quaternion.LookRotation(b - a, Vector3.up);
                float piece = Vector3.Distance(a, b) * 0.5f;
                for (int i = 0; i < 2; i++)
                {
                    int clearIndex = world.Clear.Count;
                    MudProp walk = Prop(world, MudModels.Kind.Boardwalk, Camp + new Vector2(0f, 10f + i * 6f), 0f, "Camp Boardwalk", piece / 6f, parent: props);
                    world.Clear[clearIndex] = new Vector3(Camp.x, Camp.y + 10f + i * 6f, 5f); // as before, so the scenery doesn't move
                    walk.transform.SetPositionAndRotation(Vector3.Lerp(a, b, 0.25f + 0.5f * i) - rotation * Vector3.up * 0.05f, rotation); // deck top 0.05 above its pivot
                }
            }
            foreach (Vector2 t in new[] { new Vector2(-5f, 9f), new Vector2(5f, 9f), new Vector2(-9f, -2f), new Vector2(9f, -3f) })
                Prop(world, MudModels.Kind.TikiTorch, Camp + t, 0f, "Camp Torch", parent: props);
            Prop(world, MudModels.Kind.StiltShack, Camp + new Vector2(-13f, 6f), 120f, "Camp Shack", parent: props);
            Prop(world, MudModels.Kind.RoadSign, Camp + new Vector2(3f, 22f), 180f, "Sign", sign: "MUDLANDS  pop. ???", parent: props);

            // Gumbo Flats: an old windmill, fence lines (breakable, below), hay.
            Prop(world, MudModels.Kind.Windmill, Flats + new Vector2(28f, 10f), 30f, "Broken Windmill", parent: props);
            Prop(world, MudModels.Kind.Cabin, Flats + new Vector2(-30f, 14f), 160f, "Flats Cabin", parent: props);

            // Quest rework (11-20): the stilt shack by the Lantern Road bridge (a hex lantern goes on its roof), signs where
            // the short cuts leave the camp road, lanterns along them, and the safe-keeping sign by Cavendish.
            QuestProp(world, MudModels.Kind.StiltShack, BridgeShack, Yaw(BridgeShack, new Vector2(18f, 36f)), "Bridge Shack", parent: props);
            QuestProp(world, MudModels.Kind.RoadSign, new Vector2(-3f, -118.5f), 0f, "Sign", sign: "SHORT CUT: ROTWOOD", parent: props);
            QuestProp(world, MudModels.Kind.RoadSign, new Vector2(10f, -117f), 0f, "Sign", sign: "SHORT CUT: THE BOG", parent: props);
            Vector2 keepSign = SafeKeepingAt + (SafeKeepingAt - Camp).normalized * 1.5f;
            QuestProp(world, MudModels.Kind.RoadSign, keepSign, Yaw(keepSign, Camp), "Sign", sign: "SAFE KEEPING", parent: props);
            int trackLamp = 0;
            foreach (Vector2[] track in Shortcuts)
                for (int i = 0; i < track.Length - 1; i++)
                {
                    float length = Vector2.Distance(track[i], track[i + 1]);
                    for (float d = 14f; d < length - 6f; d += 24f)
                    {
                        Vector2 p = Vector2.Lerp(track[i], track[i + 1], d / length);
                        Vector2 side = new Vector2(-(track[i + 1] - track[i]).y, (track[i + 1] - track[i]).x).normalized * 3.8f * (trackLamp++ % 2 == 0 ? 1f : -1f);
                        if (Ellipse(p + side, Bog, BogRadii) < 1.3f || DistanceToRoads(p + side) < 5f) continue;
                        QuestProp(world, MudModels.Kind.LanternPost, p + side, Yaw(p + side, p), "Track Light", parent: props);
                    }
                }

            // Rotwood: stilt shacks, the sawmill, the driving school.
            Prop(world, MudModels.Kind.DrivingSchool, School, Yaw(School, Rotwood), "Bayou Driving School", sign: "BAYOU DRIVING SCHOOL", parent: props);
            foreach ((Vector2 at, float yaw) in new[] { (Rotwood + new Vector2(-12f, 10f), 140f), (Rotwood + new Vector2(10f, 14f), 200f), (Rotwood + new Vector2(-18f, -8f), 80f), (Rotwood + new Vector2(14f, -14f), 300f) })
                Prop(world, MudModels.Kind.StiltShack, at, yaw, "Rotwood Shack", parent: props);
            // The sawmill (11-20): moved 11 m west, off the haul road; its posts stood on the road and wrecked the car on the
            // Practical. Its scatter clearing stays where it was, so no tree moves; QuestClear keeps its new spot clear.
            int sawClear = world.Clear.Count;
            Prop(world, MudModels.Kind.Sawmill, SawmillAt, 20f, "Sawmill", parent: props);
            world.Clear[sawClear] = new Vector3(Rotwood.x - 6f, Rotwood.y - 22f, world.Clear[sawClear].z);
            Prop(world, MudModels.Kind.RoadSign, Rotwood + new Vector2(6f, -4f), 230f, "Sign", sign: "HAUL ROAD", parent: props);
            Prop(world, MudModels.Kind.RoadSign, HaulBarricades[0] + new Vector2(4f, 2f), 200f, "Sign", sign: "PROPERTY OF KART KONG RACEWAY", parent: props);

            // Bridges over the creek, on the roads (one per crossing, even where a crossing lands on a bend).
            var bridges = new List<Vector2>();
            foreach (Vector2[] road in Roads)
                for (int i = 0; i < road.Length - 1; i++)
                    for (int c = 0; c < Creek.Length - 1; c++)
                        if (SegmentsCross(road[i], road[i + 1], Creek[c], Creek[c + 1], out Vector2 hit) && bridges.TrueForAll(b => Vector2.Distance(b, hit) > 8f) && Add(bridges, hit))
                            Bridge(world, props, hit, (road[i + 1] - road[i]).normalized);

            // The Banana Speedway: tyre walls round the oval, a grandstand, the banana stand, a barrel pile.
            for (int i = 0; i < 22; i++)
            {
                float a = i / 22f * Mathf.PI * 2f;
                Vector2 outer = Speedway + new Vector2(Mathf.Cos(a) * 30f, Mathf.Sin(a) * 20f);
                if (Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, -60f)) < 14f) continue; // the way in, toward the road
                Prop(world, MudModels.Kind.TyreWall, outer, -a * Mathf.Rad2Deg, "Tyre Wall", parent: props);
            }
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                Prop(world, MudModels.Kind.TyreWall, Speedway + new Vector2(Mathf.Cos(a) * 11f, Mathf.Sin(a) * 4f), -a * Mathf.Rad2Deg + 90f, "Infield Tyres", parent: props);
            }
            Prop(world, MudModels.Kind.Grandstand, Speedway + new Vector2(0f, 26f), 180f, "Grandstand", parent: props);
            Prop(world, MudModels.Kind.BananaStand, Speedway + new Vector2(-34f, 4f), 90f, "Banana Stand", parent: props);

            // The bog: islands for the gongs, a sunken chapel and the dam.
            foreach (Vector2 g in GongSpots) Prop(world, MudModels.Kind.Island, g, 0f, "Gong Island", 1f, parent: props, yOffset: WaterY - Height(g.x, g.y) + 0.2f, solid: false).gameObject.AddComponent<BoxCollider>().size = new Vector3(7f, 1.5f, 7f);
            Prop(world, MudModels.Kind.Cabin, Chapel + new Vector2(8f, 6f), 250f, "Sunken Chapel", 1.2f, parent: props, yOffset: -0.6f);

            // The dojo: the gate, stone lanterns, and the tea rock.
            Prop(world, MudModels.Kind.DojoGate, Dojo + new Vector2(-14f, 0f), 90f, "Dojo Gate", parent: props);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                Prop(world, MudModels.Kind.StoneLantern, Dojo + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 13f, -a * Mathf.Rad2Deg, "Stone Lantern", parent: props);
            }

            // The witch's clearing: her hut and the cauldron.
            Prop(world, MudModels.Kind.WitchHut, WitchClearing + new Vector2(4f, 20f), 190f, "Witch Hut", parent: props);
            Prop(world, MudModels.Kind.Cauldron, WitchClearing, 0f, "Cauldron", parent: props);

            // The Greasy Spoon: the roadhouse, gas pumps, a ramp, the gate.
            Prop(world, MudModels.Kind.Roadhouse, Lot + new Vector2(0f, 24f), 180f, "The Greasy Spoon", sign: "THE GREASY SPOON", parent: props);
            var pumps = new List<Transform>();
            foreach (float x in new[] { -16f, -11f })
                pumps.Add(Prop(world, MudModels.Kind.GasPump, Lot + new Vector2(x, 12f), 180f, "Gas Pump", parent: props).transform);
            world.Pumps = pumps.ToArray();
            MudProp ramp = Prop(world, MudModels.Kind.Ramp, Lot + new Vector2(14f, -6f), -35f, "Jump Ramp", parent: props, solid: false);
            // The driving surface is a thin tilted box along the planks (18 degrees: 2.6 m over 8 m); a block under the
            // high end stops anything driving through it sideways.
            var rampSlope = new GameObject("Ramp Surface");
            rampSlope.transform.SetParent(ramp.transform, false);
            rampSlope.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            rampSlope.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            rampSlope.AddComponent<BoxCollider>().size = new Vector3(4f, 0.2f, 8.4f);
            var rampBlock = ramp.gameObject.AddComponent<BoxCollider>();
            rampBlock.center = new Vector3(0f, 0.5f, 2.5f);
            rampBlock.size = new Vector3(4f, 1f, 3f);
            var lip = new GameObject("Ramp Lip").transform;
            lip.SetParent(ramp.transform, false);
            lip.localPosition = new Vector3(0f, 2.6f, 4f);
            world.Ramp = lip;
            Vector2 gateAt = Lot + new Vector2(0f, -32f);
            var gate = new GameObject("Roadhouse Gate");
            gate.transform.SetParent(props, false);
            gate.transform.SetPositionAndRotation(GroundAt(gateAt), Quaternion.identity);
            Prop(world, MudModels.Kind.GatePost, gateAt + new Vector2(-4.3f, 0f), 0f, "Gate Post", parent: gate.transform);
            Prop(world, MudModels.Kind.GatePost, gateAt + new Vector2(4.3f, 0f), 0f, "Gate Post", parent: gate.transform);
            Transform left = GateLeaf(gate.transform, new Vector3(-4f, 0f, 0f), 1f);
            Transform right = GateLeaf(gate.transform, new Vector3(4f, 0f, 0f), -1f);
            world.Gate = gate.AddComponent<RoadhouseGate>();
            world.Gate.Configure(left, right);
            // (No County Line gate any more (11-20): the causeway just ends where Clutch's escape ramp throws him off the map.)
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2f;
                Vector2 at = Lot + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 33f;
                if (Vector2.Distance(at, gateAt) < 10f || DistanceToPath(at, Roads[8]) < 8f) continue;
                Prop(world, MudModels.Kind.HayBale, at, -a * Mathf.Rad2Deg, "Lot Hay", parent: props);
            }

            // Lanterns and torches along the roads: the reference's warm lights in the gloom.
            int lamp = 0;
            foreach (Vector2[] road in Roads)
                for (int i = 0; i < road.Length - 1; i++)
                {
                    float length = Vector2.Distance(road[i], road[i + 1]);
                    for (float d = 12f; d < length - 6f; d += 30f)
                    {
                        Vector2 p = Vector2.Lerp(road[i], road[i + 1], d / length);
                        Vector2 side = new Vector2(-(road[i + 1] - road[i]).y, (road[i + 1] - road[i]).x).normalized * 5.2f * (lamp % 2 == 0 ? 1f : -1f);
                        if (DistanceToPath(p + side, Creek) < 7f || Ellipse(p + side, Bog, BogRadii) < 1.3f) continue;
                        Prop(world, lamp++ % 3 == 0 ? MudModels.Kind.TikiTorch : MudModels.Kind.LanternPost, p + side, Yaw(p + side, p), "Road Light", parent: props);
                    }
                }
        }

        private static Transform GateLeaf(Transform gate, Vector3 hinge, float side)
        {
            var pivot = new GameObject(side > 0 ? "Leaf L" : "Leaf R").transform;
            pivot.SetParent(gate, false);
            pivot.localPosition = hinge;
            var leaf = new GameObject("Planks");
            leaf.transform.SetParent(pivot, false);
            leaf.transform.localPosition = new Vector3(2f * side, 1.1f, 0f);
            var prop = leaf.AddComponent<MudProp>();
            prop.Configure(MudModels.Kind.Fence, 1, 1f);
            leaf.transform.localScale = new Vector3(1f, 1.6f, 1f);
            leaf.transform.localPosition = new Vector3(2f * side, 0f, 0f);
            var box = pivot.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(2f * side, 1f, 0f);
            box.size = new Vector3(4f, 2f, 0.3f);
            return pivot;
        }

        private static bool Add(List<Vector2> list, Vector2 item)
        {
            list.Add(item);
            return true;
        }

        // Where the bridges span (each end where the deck leaves the road), for the road overlays to stop at.
        private static readonly List<(Vector2 a, Vector2 b)> BridgeSpans = new List<(Vector2 a, Vector2 b)>();

        /// <summary>
        /// A bridge sized to its gap and flush with the road at both ends (11-20): along the road from the crossing, each
        /// end is where the drawn ground comes back up to the road; the deck runs 0.9 m onto the road past it, tilted so
        /// its top sits on the road surface at both ends.
        /// </summary>
        private static void Bridge(World world, Transform parent, Vector2 hit, Vector2 along)
        {
            Vector2 Edge(Vector2 dir)
            {
                for (float d = 1f; d < 18f; d += 0.25f)
                {
                    Vector2 p = hit + dir * d;
                    if (MeshHeight(p.x, p.y) >= RoadLevel(p.x, p.y) - 0.1f) return p;
                }
                return hit + dir * 8f;
            }
            Vector2 edgeA = Edge(-along), edgeB = Edge(along);
            BridgeSpans.Add((edgeA, edgeB));
            Vector2 endA = edgeA - along * 0.9f, endB = edgeB + along * 0.9f;
            var a = new Vector3(endA.x, MeshHeight(endA.x, endA.y) + SurfaceLift + 0.03f, endA.y); // on top of the road overlay
            var b = new Vector3(endB.x, MeshHeight(endB.x, endB.y) + SurfaceLift + 0.03f, endB.y);
            float length = Vector3.Distance(a, b);
            int clearIndex = world.Clear.Count;
            MudProp bridge = Prop(world, MudModels.Kind.Bridge, (endA + endB) * 0.5f, Yaw(endA, endB), "Bridge", length / 10f, parent: parent);
            // Keep the clear radius the old fixed-size bridge had, so the scenery placed after it doesn't move.
            world.Clear[clearIndex] = new Vector3(hit.x, hit.y, 3f + 1.4f * 2f);
            Quaternion rotation = Quaternion.LookRotation(b - a, Vector3.up);
            bridge.transform.SetPositionAndRotation((a + b) * 0.5f - rotation * Vector3.up * 0.06f, rotation); // deck top 0.06 above its pivot
        }

        private static bool SegmentsCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 hit)
        {
            hit = default;
            Vector2 r = b - a, s = d - c;
            float denom = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(denom) < 1e-5f) return false;
            float t = ((c.x - a.x) * s.y - (c.y - a.y) * s.x) / denom;
            float u = ((c.x - a.x) * r.y - (c.y - a.y) * r.x) / denom;
            if (t < 0f || t > 1f || u < 0f || u > 1f) return false;
            hit = a + r * t;
            return true;
        }

        // ---------- Scenery (trees, rocks, plants) ----------

        private static void BuildScenery(World world)
        {
            var root = new GameObject("Scenery");
            root.transform.SetParent(world.Level, false);
            var scenery = root.AddComponent<MudScenery>();
            var colliders = new GameObject("Scenery Colliders").transform;
            colliders.SetParent(root.transform, false);
            System.Random random = world.Random;
            float R(float a, float b) => a + (float)random.NextDouble() * (b - a);

            bool Blocked(Vector2 p, float margin)
            {
                if (p.magnitude > PlayRadius + 8f) return false;
                if (DistanceToRoads(p) < 6f + margin) return true;
                if (Ellipse(p, Speedway, new Vector2(32f, 22f)) < 1f || Vector2.Distance(p, Lot) < 36f || Vector2.Distance(p, WitchClearing) < 20f ||
                    Vector2.Distance(p, Dojo) < 18f || Vector2.Distance(p, Camp) < 14f || Ellipse(p, Chapel, FloodRadii) < 1.15f) return true;
                foreach (Vector3 c in world.Clear) if (Vector2.Distance(p, new Vector2(c.x, c.y)) < c.z + margin) return true;
                return false;
            }

            void Tree(MudModels.Kind kind, Vector2 p, float scale)
            {
                (Vector3 at, Vector2 slope) = Footing(kind, p, scale);
                int variant = random.Next(4);
                float yaw = R(0f, 360f);
                if (QuestBlocked(p)) return; // drawn as before, so everything after is unchanged
                scenery.Place(kind, variant, at, yaw, scale, slope);
                foreach ((Vector3 centre, Vector3 size) in MudModels.Colliders(kind))
                {
                    var go = new GameObject(kind + " Collider");
                    go.transform.SetParent(colliders, false);
                    go.transform.position = at;
                    go.transform.localScale = Vector3.one * scale;
                    var box = go.AddComponent<BoxCollider>();
                    box.center = centre;
                    box.size = size;
                }
            }

            // The ridge ring: a wall of big cypresses.
            for (int i = 0; i < 160; i++)
            {
                float a = i / 160f * Mathf.PI * 2f + R(-0.01f, 0.01f);
                Vector2 p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R(PlayRadius - 4f, PlayRadius + 10f);
                if (DistanceToRoads(p) < 7f) continue;
                if (p.y < -168f && Mathf.Abs(p.x) < 14f)
                {
                    // The pass to the landing stays open. Draw what the tree would have, so everything after is unchanged.
                    R(1.1f, 1.5f);
                    random.Next(4);
                    R(0f, 360f);
                    continue;
                }
                Tree(MudModels.Kind.Cypress, p, R(1.1f, 1.5f));
            }
            // The swamp forest.
            for (int i = 0; i < 520; i++)
            {
                Vector2 p = Random(random, PlayRadius - 6f);
                if (Blocked(p, 0f) || Ellipse(p, Bog, BogRadii) < 0.9f) continue;
                float roll = (float)random.NextDouble();
                Tree(roll < 0.55f ? MudModels.Kind.Cypress : roll < 0.75f ? MudModels.Kind.Willow : MudModels.Kind.DeadTree, p, R(0.8f, 1.25f));
            }
            // Dead trees round the witch's clearing.
            for (int i = 0; i < 18; i++)
            {
                float a = i / 18f * Mathf.PI * 2f;
                Tree(MudModels.Kind.DeadTree, WitchClearing + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R(22f, 26f), R(1f, 1.4f));
            }
            // Rocks, stumps, logs.
            for (int i = 0; i < 160; i++)
            {
                Vector2 p = Random(random, PlayRadius - 4f);
                if (Blocked(p, -2f)) continue;
                float roll = (float)random.NextDouble();
                Tree(roll < 0.45f ? MudModels.Kind.Rock : roll < 0.65f ? MudModels.Kind.Boulder : roll < 0.85f ? MudModels.Kind.Stump : MudModels.Kind.Log, p, R(0.8f, 1.3f));
            }
            // Water plants: cattails on the shores, lily pads on the water, glowing mushrooms in the gloom.
            void Plant(MudModels.Kind kind, int variant, Vector3 at, float yaw, float scale)
            {
                if (!QuestBlocked(new Vector2(at.x, at.z))) scenery.Place(kind, variant, at, yaw, scale);
            }
            for (int i = 0; i < 700; i++)
            {
                Vector2 p = Random(random, PlayRadius - 4f);
                float h = Height(p.x, p.y);
                if (DistanceToRoads(p) < 4.5f) continue;
                if (h < WaterY - 0.25f)
                {
                    if (random.NextDouble() < 0.5f) Plant(MudModels.Kind.LilyPads, random.Next(4), new Vector3(p.x, WaterY, p.y), R(0f, 360f), R(0.8f, 1.4f));
                }
                else if (h < WaterY + 0.6f) Plant(MudModels.Kind.Cattails, random.Next(4), GroundAt(p), R(0f, 360f), R(0.8f, 1.3f));
                else if (random.NextDouble() < 0.2f && !Blocked(p, -3f)) Plant(MudModels.Kind.Mushrooms, random.Next(4), GroundAt(p), R(0f, 360f), R(0.8f, 1.6f));
                else if (random.NextDouble() < 0.25f && !Blocked(p, -3f)) Plant(MudModels.Kind.Reeds, random.Next(4), GroundAt(p), R(0f, 360f), R(0.8f, 1.3f));
            }
            scenery.Generate(); // show it in the editor now (it was enabled before any entries existed)
        }

        private static Vector2 Random(System.Random random, float radius)
        {
            float a = (float)random.NextDouble() * Mathf.PI * 2f, d = Mathf.Sqrt((float)random.NextDouble()) * radius;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
        }

        // ---------- Lighting, foliage, navigation ----------

        // Teal dusk under a low moon, fog in the trees, warm lights as the accent (the references).
        private static void BuildLighting()
        {
            var moon = new GameObject("Moon").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.62f, 0.78f, 0.88f);
            moon.intensity = 0.75f;
            moon.shadows = LightShadows.Soft;
            moon.transform.rotation = Quaternion.Euler(38f, -35f, 0f);
            RenderSettings.sun = moon;
            string skyPath = Folder + "/Materials/MudlandsSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            sky.SetFloat("_SunSize", 0.03f);
            sky.SetFloat("_AtmosphereThickness", 0.6f);
            sky.SetColor("_SkyTint", new Color(0.25f, 0.45f, 0.5f));
            sky.SetColor("_GroundColor", new Color(0.12f, 0.18f, 0.17f));
            sky.SetFloat("_Exposure", 0.55f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.32f, 0.46f, 0.5f);
            RenderSettings.ambientEquatorColor = new Color(0.24f, 0.34f, 0.32f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.14f, 0.11f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.2f, 0.32f, 0.33f);
            RenderSettings.fogStartDistance = 22f;
            RenderSettings.fogEndDistance = 170f;
        }

        private static void BuildFoliage(Transform level, GameObject ground, List<Vector3> clear)
        {
            var foliage = new GameObject("Grass");
            foliage.transform.SetParent(level, false);
            foreach (Vector3 c in clear)
            {
                var marker = new GameObject("Clear");
                marker.transform.SetParent(foliage.transform, false);
                marker.transform.position = new Vector3(c.x, 0f, c.y);
                marker.AddComponent<SphereCollider>().radius = c.z;
            }
            foreach (Vector2[] road in Roads)
                for (int i = 0; i < road.Length - 1; i++)
                    for (float d = 0f, length = Vector2.Distance(road[i], road[i + 1]); d < length; d += 3f)
                    {
                        Vector2 p = Vector2.Lerp(road[i], road[i + 1], d / length);
                        var marker = new GameObject("Clear Road");
                        marker.transform.SetParent(foliage.transform, false);
                        marker.transform.position = new Vector3(p.x, 0f, p.y);
                        marker.AddComponent<SphereCollider>().radius = 3.6f;
                    }
            foreach (Vector2[] track in Shortcuts)
                for (int i = 0; i < track.Length - 1; i++)
                    for (float d = 0f, length = Vector2.Distance(track[i], track[i + 1]); d < length; d += 2.5f)
                    {
                        Vector2 p = Vector2.Lerp(track[i], track[i + 1], d / length);
                        var marker = new GameObject("Clear Track");
                        marker.transform.SetParent(foliage.transform, false);
                        marker.transform.position = new Vector3(p.x, 0f, p.y);
                        marker.AddComponent<SphereCollider>().radius = 2.8f;
                    }
            // No grass under the water.
            void Marker(Vector2 p, float radius)
            {
                var marker = new GameObject("Clear Water");
                marker.transform.SetParent(foliage.transform, false);
                marker.transform.position = new Vector3(p.x, 0f, p.y);
                marker.AddComponent<SphereCollider>().radius = radius;
            }
            for (float x = -1f; x <= 1f; x += 0.25f)
                for (float y = -1f; y <= 1f; y += 0.25f)
                    if (x * x + y * y <= 1.3f) Marker(Bog + new Vector2(x * BogRadii.x, y * BogRadii.y), 9f);
            for (int i = 0; i < Creek.Length - 1; i++)
                for (float d = 0f, length = Vector2.Distance(Creek[i], Creek[i + 1]); d < length; d += 3f)
                    Marker(Vector2.Lerp(Creek[i], Creek[i + 1], d / length), 4.5f);
            Marker(WitchClearing + new Vector2(0f, -9f), 8f);
            foreach (SphereCollider sphere in foliage.GetComponentsInChildren<SphereCollider>()) sphere.enabled = false; // markers only
            var scatter = foliage.AddComponent<FoliageScatter>();
            var data = new SerializedObject(scatter);
            data.FindProperty("ground").objectReferenceValue = ground.GetComponent<Collider>();
            (string name, int count, Vector2 scale)[] items = { ("GrassTuft", 6500, new Vector2(0.9f, 1.8f)), ("GrassTuftDry", 2500, new Vector2(0.9f, 1.6f)), ("FlowersPink", 300, new Vector2(0.8f, 1.1f)) };
            SerializedProperty list = data.FindProperty("items");
            list.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MeadowAssetBuilder.PrefabPath(items[i].name));
                if (prefab == null) continue;
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("mesh").objectReferenceValue = prefab.GetComponent<MeshFilter>().sharedMesh;
                Material[] materials = prefab.GetComponent<MeshRenderer>().sharedMaterials;
                SerializedProperty mats = entry.FindPropertyRelative("materials");
                mats.arraySize = materials.Length;
                for (int m = 0; m < materials.Length; m++) mats.GetArrayElementAtIndex(m).objectReferenceValue = materials[m];
                entry.FindPropertyRelative("count").intValue = items[i].count;
                entry.FindPropertyRelative("scale").vector2Value = items[i].scale;
            }
            data.FindProperty("radius").floatValue = PlayRadius;
            data.FindProperty("seed").intValue = 1108;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BakeNavigation(Transform level)
        {
            var navigation = new GameObject("Navigation");
            navigation.transform.SetParent(level, false);
            var surface = navigation.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            navigation.AddComponent<NavMeshBaker>();
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
        }

        // ---------- Gameplay: player, cast, quest objects ----------

        private static Transform BuildGameplay(World world, Transform camp, Dictionary<Type, GameObject> creatures)
        {
            var gameplay = new GameObject("Gameplay").transform;
            world.Gameplay = gameplay;
            Vector3 spawnAt = GroundAt(Camp + new Vector2(-2.6f, -0.4f));
            Quaternion facing = Quaternion.Euler(0f, 90f, 0f);
            var spawn = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CorePrefabBuilder.SpawnPath));
            spawn.transform.SetParent(gameplay, false);
            spawn.transform.SetPositionAndRotation(spawnAt, facing);
            spawn.name = "Spawn At Camp";
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CorePrefabBuilder.PlayerPath));
            player.transform.SetPositionAndRotation(spawnAt + Vector3.up * 0.05f, facing);
            player.GetComponentInChildren<Camera>(true).clearFlags = CameraClearFlags.Skybox;
            var gear = new SerializedObject(player.GetComponent<PlayerGear>());
            SerializedProperty items = gear.FindProperty("startingItems");
            PlayerItem[] all = { PlayerItem.MiningLaser, PlayerItem.Scanner, PlayerItem.Flask };
            items.arraySize = all.Length;
            for (int i = 0; i < all.Length; i++) items.GetArrayElementAtIndex(i).enumValueIndex = (int)all[i];
            gear.FindProperty("introduceItems").boolValue = false;
            gear.ApplyModifiedPropertiesWithoutUndo();
            var scanner = new SerializedObject(player.GetComponentInChildren<ScannerController>(true));
            scanner.FindProperty("levelRangeOverride").floatValue = 220f;
            scanner.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CorePrefabBuilder.SystemsPath));

            // Cavendish on his log, Eugene on the other.
            GameObject cavendish = Instance(CavendishPath, gameplay);
            cavendish.name = "Cavendish";
            cavendish.transform.SetPositionAndRotation(GroundAt(Camp + new Vector2(3.4f, 0.5f)), Quaternion.Euler(0f, -90f, 0f));
            cavendish.AddComponent<NpcActor>();
            cavendish.AddComponent<QuestSignalSource>();
            var voice = new GameObject("Cavendish Dialogue");
            voice.transform.SetParent(cavendish.transform, false);
            voice.transform.localPosition = new Vector3(0f, 2.45f, 0f);
            var dialogue = new SerializedObject(voice.AddComponent<NpcDialogue>());
            dialogue.FindProperty("speaker").stringValue = "Cavendish";
            dialogue.ApplyModifiedPropertiesWithoutUndo();

            GameObject merchant = Instance(MerchantPath, gameplay);
            merchant.name = "Eugene";
            Vector3 merchantAt = GroundAt(Camp + new Vector2(1.8f, -3.2f));
            merchant.transform.SetPositionAndRotation(merchantAt, Quaternion.Euler(0f, -20f, 0f));
            var spot = new GameObject("Merchant Spot").transform;
            spot.SetParent(gameplay, false);
            spot.SetPositionAndRotation(merchantAt, merchant.transform.rotation);
            var crab = new SerializedObject(merchant.GetComponent<MerchantCrab>());
            crab.FindProperty("returnPoint").objectReferenceValue = spot;
            crab.FindProperty("seat").objectReferenceValue = spot;
            crab.FindProperty("startFriendly").boolValue = true;
            crab.FindProperty("spawnAfterQuest").boolValue = false;
            crab.ApplyModifiedPropertiesWithoutUndo();

            // Line 1: the kiosk, the haul road's barricades, the still.
            MudProp kiosk = Prop(world, MudModels.Kind.Kiosk, School + new Vector2(0f, 7f), Yaw(School, Rotwood), "Driving Test Kiosk", sign: "TEST", parent: gameplay);
            world.Kiosk = kiosk.gameObject.AddComponent<DrivingTestKiosk>();
            world.Kiosk.Configure(creatures[typeof(Mudpuppy)]);
            kiosk.gameObject.AddComponent<QuestSignalSource>().enabled = false;
            world.Barricades = HaulBarricades.Select((b, i) =>
            {
                Vector2 dir = i + 1 < HaulBarricades.Length ? HaulBarricades[i + 1] - b : b - HaulBarricades[i - 1];
                MudProp prop = Prop(world, MudModels.Kind.Barricade, b, Yaw(Vector2.zero, dir), "Haul Road Barricade", sign: "ROAD CLOSED", parent: gameplay);
                Destructible barricade = Breakable(prop.gameObject, Destructible.Substance.Wood, 45f, false, true);
                barricade.SetToughness(Destructible.Toughness.CarOnly, PlatedHint);
                return barricade;
            }).ToArray();
            MudProp still = Prop(world, MudModels.Kind.MoonshineStill, HaulEnd, Yaw(HaulEnd, Rotwood), "Moonshine Still", parent: gameplay);
            world.Still = Breakable(still.gameObject, Destructible.Substance.Metal, 60f, true, true, 6f, 40f);
            world.Still.SetToughness(Destructible.Toughness.CarOnly, PlatedHint);

            // The log jam on the Logging Chute (a short cut to Rotwood once the car shifts it).
            {
                var jam = new GameObject("Log Jam");
                jam.transform.SetParent(gameplay, false);
                float yaw = Yaw(Shortcuts[0][0], Shortcuts[0][1]);
                jam.transform.SetPositionAndRotation(GroundAt(LogJamAt), Quaternion.Euler(0f, yaw, 0f));
                foreach ((Vector3 offset, float turn) in new[] { (new Vector3(0f, 0f, -0.55f), 4f), (new Vector3(0.3f, 0f, 0.55f), -6f), (new Vector3(-0.2f, 0.72f, 0f), 2f) })
                {
                    MudProp log = QuestProp(world, MudModels.Kind.Log, LogJamAt, yaw + turn, "Jammed Log", parent: jam.transform, solid: false, size: 1.25f);
                    log.transform.localPosition = offset;
                    log.transform.localRotation = Quaternion.Euler(0f, turn, offset.y > 0f ? 3f : 0f);
                }
                var block = jam.AddComponent<BoxCollider>();
                block.center = new Vector3(0f, 0.75f, 0f);
                block.size = new Vector3(5.6f, 1.5f, 2.2f);
                world.LogJam = Breakable(jam, Destructible.Substance.Wood, 60f, false, false);
                world.LogJam.SetToughness(Destructible.Toughness.CarOnly, "A proper log jam. Your laser won't shift it: a car would.");
            }

            // Breakable fences and hay on the Flats, crates and barrels around Rotwood: things to smash.
            for (int i = 0; i < 14; i++)
            {
                Vector2 at = Flats + new Vector2(-38f + i * 5.4f, 18f + Mathf.Sin(i) * 2f);
                if (DistanceToRoads(at) < 4f) continue;
                Breakable(Prop(world, MudModels.Kind.Fence, at, 90f + Mathf.Sin(i) * 8f, "Fence", parent: gameplay).gameObject, Destructible.Substance.Wood, 20f, false, false);
            }
            foreach (Vector2 at in new[] { Flats + new Vector2(18f, -14f), Flats + new Vector2(22f, -12f), Flats + new Vector2(-20f, 8f), Rotwood + new Vector2(4f, 6f) })
                Breakable(Prop(world, MudModels.Kind.HayBale, at, 30f, "Hay Bale", parent: gameplay).gameObject, Destructible.Substance.Hay, 25f, false, false);
            foreach (Vector2 at in new[] { Rotwood + new Vector2(-4f, 8f), Rotwood + new Vector2(-3f, 9.2f), School + new Vector2(5f, 4f), Speedway + new Vector2(-32f, 9f), Speedway + new Vector2(-31f, 10.2f) })
                Breakable(Prop(world, MudModels.Kind.Crate, at, 15f, "Crate", parent: gameplay).gameObject, Destructible.Substance.Wood, 15f, false, false);
            foreach (Vector2 at in new[] { HaulBarricades[2] + new Vector2(3f, 2f), Flats + new Vector2(30f, -20f), Rotwood + new Vector2(8f, -3f) })
                Breakable(Prop(world, MudModels.Kind.MoonshineBarrel, at, 0f, "Moonshine Barrel", parent: gameplay).gameObject, Destructible.Substance.Metal, 10f, true, false);
            foreach (Vector2 at in new[] { Rotwood + new Vector2(20f, 4f), Flats + new Vector2(-14f, -18f) })
                Breakable(Prop(world, MudModels.Kind.Outhouse, at, 90f, "Outhouse", parent: gameplay).gameObject, Destructible.Substance.Wood, 30f, false, false);

            // Line 2: gongs on the islands, stepping stones, the dam with its barrels, the flooded chapel road.
            var gongs = new List<SpiritGong>();
            for (int gi = 0; gi < GongSpots.Length; gi++)
            {
                Vector2 g = GongSpots[gi];
                var root = new GameObject("Spirit Gong");
                root.transform.SetParent(gameplay, false);
                root.transform.SetPositionAndRotation(new Vector3(g.x, WaterY + 0.92f, g.y), Quaternion.Euler(0f, Yaw(g, Bog), 0f));
                var prop = root.AddComponent<MudProp>();
                prop.Configure(MudModels.Kind.Gong, 0);
                AddColliders(root, MudModels.Kind.Gong, 1f);
                root.AddComponent<QuestSignalSource>().enabled = false;
                var guards = new GameObject("Gong Guards");
                guards.transform.SetParent(gameplay, false);
                guards.transform.position = root.transform.position;
                var gong = root.AddComponent<SpiritGong>();
                gong.Configure(guards, prop.Moving, gi + 1); // rung in order: NW island, south island, east island
                gongs.Add(gong);
                // Stepping stones from the nearest shore.
                Vector2 toShore = (g - Bog).normalized;
                Vector2 shore = Bog + new Vector2(toShore.x * BogRadii.x, toShore.y * BogRadii.y) * 1.05f;
                float span = Vector2.Distance(g, shore) - 4f;
                for (float d = 2.2f; d < span; d += 2.3f)
                {
                    Vector2 at = Vector2.Lerp(g + (shore - g).normalized * 4f, shore, d / span);
                    var stone = new GameObject("Stepping Stone");
                    stone.transform.SetParent(gameplay, false);
                    stone.transform.position = new Vector3(at.x, WaterY + 0.15f, at.y);
                    stone.AddComponent<MudProp>().Configure(MudModels.Kind.StepStone, (int)d);
                    AddColliders(stone, MudModels.Kind.StepStone, 1f);
                    stone.AddComponent<SinkingStone>();
                }
            }
            world.Gongs = gongs.ToArray();
            Vector2 damAt = new Vector2(Chapel.x - 2f, Chapel.y - 16f);
            MudProp dam = Prop(world, MudModels.Kind.Dam, damAt, 70f, "Mud Dam", parent: gameplay);
            world.Dam = Breakable(dam.gameObject, Destructible.Substance.Wood, 30f, false, true);
            world.Dam.SetToughness(Destructible.Toughness.BlastOnly, "The mud soaks up bolts. It needs an explosion: the barrels at its foot, or crash the car into it.");
            for (int i = 0; i < 4; i++)
                Breakable(Prop(world, MudModels.Kind.MoonshineBarrel, damAt + new Vector2(-3f + i * 1.1f, -3.2f + (i % 2) * 0.6f), 0f, "Dam Barrel", parent: gameplay).gameObject,
                    Destructible.Substance.Metal, 10f, true, false, 5f, 45f);
            var flood = new GameObject("Chapel Flood");
            flood.transform.SetParent(gameplay, false);
            flood.transform.position = new Vector3(Chapel.x, FloodLevel, Chapel.y);
            AddWaterDisc(flood.transform, "Flood Water", flood.transform.position, FloodRadii, WaterMaterial());
            AddZone(flood.transform, "Flood Zone", flood.transform.position, new Vector2(12f, 8.5f), MudZone.Kind.BogWater, 0.7f);
            world.Flood = flood;
            world.SpiritRoad = BuildSpiritRoad(gameplay);

            // Line 3: the cursed lanterns.
            world.Lanterns = LanternSpots.Select((p, i) =>
            {
                MudProp pole = Prop(world, MudModels.Kind.CursedLantern, p + new Vector2(5f, 0f), 270f, "Cursed Lantern", parent: gameplay);
                Breakable(pole.gameObject, Destructible.Substance.Wood, 25f, false, true, smash: 3f);
                var group = new GameObject("Released by Lantern " + (i + 1));
                group.transform.SetParent(gameplay, false);
                var lantern = pole.gameObject.AddComponent<CursedLantern>();
                PlaceLanternGroup(group.transform, i, p, creatures);
                lantern.Configure(group);
                return lantern;
            }).ToArray();
            // Warm lanterns that go up where the cursed ones stood, once the witch is beaten (inactive until then).
            world.Relit = LanternSpots.Select(p =>
            {
                MudProp post = QuestProp(world, MudModels.Kind.LanternPost, p + new Vector2(5f, 0f), 270f, "Relit Lantern", parent: gameplay);
                post.gameObject.SetActive(false); // lit once the witch is beaten (MudlandsStory)
                return post.gameObject;
            }).ToArray();
            world.HexLanterns = BuildHexLanterns(world, gameplay, creatures);

            // The safe-keeping pile: where Cavendish keeps the ship parts you bring him.
            var pile = new GameObject("Safe Keeping").transform;
            pile.SetParent(gameplay, false);
            pile.SetPositionAndRotation(GroundAt(SafeKeepingAt), Quaternion.Euler(0f, Yaw(SafeKeepingAt, Camp), 0f));
            world.Pile = pile;
            return gameplay;
        }

        private const string PlatedHint = "Plated with ship scrap: bolts bounce off. A car would go straight through.";

        // The Spirit Road (11-20): slabs from the bog road straight across the bog to the far shore below the dam, each
        // tilted so its top meets the next and the banks at both ends; split into three sections that the three gongs raise.
        private static SpiritRoad BuildSpiritRoad(Transform gameplay)
        {
            var root = new GameObject("Spirit Road");
            root.transform.SetParent(gameplay, false);
            float total = Vector2.Distance(SpiritA, SpiritB);
            Vector2 dir = (SpiritB - SpiritA) / total;
            float Top(Vector2 p) => Mathf.Max(WaterY + 0.38f, MeshHeight(p.x, p.y) + 0.06f);
            bool Wet(Vector2 p) => MeshHeight(p.x, p.y) < WaterY + 0.3f;
            // The wet stretch, with a slab's length of bank at each end.
            float first = -1f, last = -1f;
            for (float d = 0f; d <= total; d += 0.5f)
                if (Wet(SpiritA + dir * d)) { if (first < 0f) first = d; last = d; }
            if (first < 0f) { Debug.LogWarning("Spirit Road: no water between its ends."); first = 0f; last = total; }
            first = Mathf.Max(0f, first - 5f);
            last = Mathf.Min(total, last + 5f);
            int count = Mathf.Max(3, Mathf.CeilToInt((last - first) / 6f));
            float step = (last - first) / count;
            var sections = new Transform[3];
            for (int s = 0; s < 3; s++)
            {
                sections[s] = new GameObject("Section " + (s + 1)).transform;
                sections[s].SetParent(root.transform, false);
            }
            const float thickness = 0.7f;
            for (int i = 0; i < count; i++)
            {
                Vector2 a2 = SpiritA + dir * (first + i * step), b2 = SpiritA + dir * (first + (i + 1) * step);
                var a = new Vector3(a2.x, Top(a2), a2.y);
                var b = new Vector3(b2.x, Top(b2), b2.y);
                Quaternion rotation = Quaternion.LookRotation(b - a, Vector3.up);
                var slab = new GameObject("Slab");
                slab.transform.SetParent(sections[Mathf.Min(2, i * 3 / count)], false);
                slab.transform.SetPositionAndRotation((a + b) * 0.5f - rotation * Vector3.up * (thickness * 0.5f), rotation);
                slab.transform.localScale = new Vector3(SpiritWidth, thickness, Vector3.Distance(a, b) + 0.25f);
                slab.AddComponent<BoxCollider>();
            }
            var road = root.AddComponent<SpiritRoad>();
            road.Configure(sections);
            return road;
        }

        // The hex lanterns (11-20, quest "Hex Lanterns"): on top of the windmill and the Flats cabin, and on the roof of the
        // shack by the Lantern Road bridge. Hidden until the curse jumps to them; each with a few guards that turn up then.
        private static CursedLantern[] BuildHexLanterns(World world, Transform gameplay, Dictionary<Type, GameObject> creatures)
        {
            var spots = new (Vector2 at, float roof, float yaw, string name)[]
            {
                (Flats + new Vector2(28f, 10f), 9f, 30f, "Hex Lantern (Windmill)"),
                (Flats + new Vector2(-30f, 14f), 3.9f, 160f, "Hex Lantern (Flats Cabin)"),
                (BridgeShack, 5.5f, Yaw(BridgeShack, new Vector2(18f, 36f)), "Hex Lantern (Bridge Shack)")
            };
            var result = new List<CursedLantern>();
            for (int i = 0; i < spots.Length; i++)
            {
                (Vector2 at, float roof, float yaw, string name) = spots[i];
                MudProp pole = QuestProp(world, MudModels.Kind.CursedLantern, at, yaw, name, parent: gameplay, yOffset: roof, size: 1.25f);
                Destructible piece = Breakable(pole.gameObject, Destructible.Substance.Wood, 30f, false, true, smash: 99f);
                Object.DestroyImmediate(pole.GetComponent<NavMeshObstacle>()); // up on a roof: nothing to carve
                var guards = new GameObject("Hex Guards " + (i + 1));
                guards.transform.SetParent(gameplay, false);
                guards.transform.position = GroundAt(at);
                CombatZone zone = Zone(guards.transform, "Hex Zone", at, 18f);
                Vector2 front = at + new Vector2(Mathf.Sin(yaw * Mathf.Deg2Rad), Mathf.Cos(yaw * Mathf.Deg2Rad)) * 7f;
                if (i == 0)
                {
                    Spawn(creatures, typeof(BanjoToad), guards.transform, front, -1f, zone);
                    for (int p = 0; p < 3; p++) Spawn(creatures, typeof(Mudpuppy), guards.transform, front + new Vector2(p * 1.5f - 1.5f, 2.5f), -1f, zone);
                }
                else
                {
                    Possum(creatures, guards.transform, GroundAt(front + new Vector2(-4f, 1f)), false);
                    Possum(creatures, guards.transform, GroundAt(front + new Vector2(4f, -1f)), false);
                    if (i == 1) for (int p = 0; p < 2; p++) Spawn(creatures, typeof(Mudpuppy), guards.transform, front + new Vector2(p * 1.5f, -3f), -1f, zone);
                }
                var lantern = pole.gameObject.AddComponent<CursedLantern>();
                lantern.Configure(guards, true, 3.8f * 1.25f); // the flame of a size-1.25 lantern
                pole.gameObject.SetActive(false); // hidden until the curse jumps to it (MudlandsStory)
                result.Add(lantern);
            }
            return result.ToArray();
        }

        private static Destructible Breakable(GameObject go, Destructible.Substance substance, float hp, bool explosive, bool signal, float radius = 4f, float damage = 30f, float smash = 4f)
        {
            var destructible = go.AddComponent<Destructible>();
            destructible.Configure(substance, hp, explosive, radius, damage);
            var data = new SerializedObject(destructible);
            data.FindProperty("carSmashSpeed").floatValue = smash;
            data.ApplyModifiedPropertiesWithoutUndo();
            var health = new SerializedObject(go.GetComponent<Health>());
            health.FindProperty("maxHealth").floatValue = hp;
            health.ApplyModifiedPropertiesWithoutUndo();
            var obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (BoxCollider box in go.GetComponents<BoxCollider>())
            {
                if (first) { bounds = new Bounds(box.center, box.size); first = false; }
                else bounds.Encapsulate(new Bounds(box.center, box.size));
            }
            obstacle.center = bounds.center;
            obstacle.size = bounds.size;
            if (signal) go.AddComponent<QuestSignalSource>().enabled = false;
            return destructible;
        }

        // ---------- Creatures ----------

        private static GameObject Spawn(Dictionary<Type, GameObject> creatures, Type type, Transform parent, Vector2 at, float yaw = -1f, CombatZone zone = null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(creatures[type], parent);
            go.transform.SetPositionAndRotation(GroundAt(at), Quaternion.Euler(0f, yaw < 0f ? UnityEngine.Random.Range(0f, 360f) : yaw, 0f));
            if (zone != null)
            {
                var data = new SerializedObject(go.GetComponent<MudCritter>());
                data.FindProperty("territory.zone").objectReferenceValue = zone;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            return go;
        }

        private static CombatZone Zone(Transform parent, string name, Vector2 at, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = GroundAt(at);
            var zone = go.AddComponent<CombatZone>();
            zone.Radius = radius;
            return zone;
        }

        private static void Den(Dictionary<Type, GameObject> creatures, Transform parent, Vector2 at, int count, CombatZone zone = null)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                Spawn(creatures, typeof(Mudpuppy), parent, at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.2f, -1f, zone);
            }
            var mud = new GameObject("Den Mud");
            mud.transform.SetParent(parent, false);
            mud.transform.position = GroundAt(at);
            mud.AddComponent<MudZone>().Configure(MudZone.Kind.Mud, new Vector2(3.5f, 3.5f), 0.45f);
        }

        private static GameObject Possum(Dictionary<Type, GameObject> creatures, Transform parent, Vector3 at, bool perch)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(creatures[typeof(JugPossum)], parent);
            go.transform.position = at;
            if (perch)
            {
                var data = new SerializedObject(go.GetComponent<JugPossum>());
                data.FindProperty("perch").boolValue = true;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            return go;
        }

        private static void PlaceLanternGroup(Transform group, int index, Vector2 at, Dictionary<Type, GameObject> creatures)
        {
            CombatZone zone = Zone(group, "Lantern Zone", at, 16f);
            switch (index)
            {
                case 0: for (int i = 0; i < 3; i++) Spawn(creatures, typeof(Mudpuppy), group, at + new Vector2(i * 1.5f - 1.5f, 4f), -1f, zone); break;
                case 1: Spawn(creatures, typeof(HogBrute), group, at + new Vector2(0f, 5f), 180f, zone); break;
                case 2:
                    Spawn(creatures, typeof(BanjoToad), group, at + new Vector2(-4f, 6f), 180f, zone);
                    for (int i = 0; i < 3; i++) Spawn(creatures, typeof(Mudpuppy), group, at + new Vector2(i * 1.5f, 3f), -1f, zone);
                    break;
                case 3:
                    Possum(creatures, group, GroundAt(at + new Vector2(-6f, 8f)), false);
                    Possum(creatures, group, GroundAt(at + new Vector2(6f, 8f)), false);
                    break;
                default:
                    Spawn(creatures, typeof(RoadblockTurtle), group, at + new Vector2(0f, 6f), 180f, zone);
                    Spawn(creatures, typeof(BanjoToad), group, at + new Vector2(5f, 10f), 180f, zone);
                    break;
            }
        }

        private static void BuildEncounters(World world, Transform gameplay, Dictionary<Type, GameObject> creatures)
        {
            var enemies = new GameObject("Creatures").transform;
            enemies.SetParent(gameplay, false);

            // Gumbo Flats: two mudpuppy dens and a brute: the car's first playground.
            Den(creatures, enemies, Flats + new Vector2(-12f, 0f), 4);
            Den(creatures, enemies, Flats + new Vector2(16f, -8f), 5);
            Spawn(creatures, typeof(HogBrute), enemies, Flats + new Vector2(4f, 6f), 180f);
            // The driving school: possums on the roof over a den.
            Possum(creatures, enemies, GroundAt(School) + new Vector3(0f, 4.6f, 1.2f), true);
            Possum(creatures, enemies, GroundAt(School) + new Vector3(2.4f, 4.6f, -1f), true);
            Den(creatures, enemies, School + new Vector2(9f, 8f), 5);
            // The haul road: a turtle behind barricade 2, possums on a shack porch, two brutes arguing by the still.
            CombatZone haul = Zone(enemies, "Haul Road Zone", Vector2.Lerp(HaulBarricades[1], HaulBarricades[3], 0.5f), 18f);
            // The turtle (11-20): beside the road between barricades 2 and 3, where you see it coming, instead of 4 m behind
            // barricade 2, where the car smashed the barricade straight into its shell on the Practical's timed run.
            Vector2 haulDir = (HaulBarricades[2] - HaulBarricades[1]).normalized;
            Vector2 turtleAt = Vector2.Lerp(HaulBarricades[1], HaulBarricades[2], 0.5f) + new Vector2(haulDir.y, -haulDir.x) * 5.5f;
            Spawn(creatures, typeof(RoadblockTurtle), enemies, turtleAt, Yaw(turtleAt, Vector2.Lerp(HaulBarricades[1], HaulBarricades[2], 0.5f)), haul);
            Possum(creatures, enemies, GroundAt(HaulBarricades[2] + new Vector2(8f, 3f)), false);
            Possum(creatures, enemies, GroundAt(HaulBarricades[3] + new Vector2(-7f, 2f)), false);
            Spawn(creatures, typeof(HogBrute), enemies, HaulEnd + new Vector2(5f, 6f), 0f);
            Spawn(creatures, typeof(HogBrute), enemies, HaulEnd + new Vector2(7f, 4f), 270f);
            // A brute pair arguing at the Lantern Road bridge, a mixed band near the witch's road.
            Spawn(creatures, typeof(HogBrute), enemies, new Vector2(16f, 30f), 90f);
            Spawn(creatures, typeof(HogBrute), enemies, new Vector2(19f, 32f), 270f);
            CombatZone band = Zone(enemies, "Roadside Band", new Vector2(100f, 96f), 16f);
            Spawn(creatures, typeof(BanjoToad), enemies, new Vector2(104f, 100f), 200f, band);
            Spawn(creatures, typeof(HogBrute), enemies, new Vector2(98f, 92f), 200f, band);
            for (int i = 0; i < 3; i++) Spawn(creatures, typeof(Mudpuppy), enemies, new Vector2(96f + i * 1.5f, 98f), -1f, band);

            // Gong guards (woken when each gong rings).
            for (int g = 0; g < world.Gongs.Length; g++)
            {
                Transform guards = FindGuards(gameplay, g);
                Vector2 shore = GuardSpots[g];
                CombatZone zone = Zone(guards, "Gong Zone", shore, 16f);
                if (g == 0) { Spawn(creatures, typeof(HogBrute), guards, shore, -1f, zone); for (int i = 0; i < 3; i++) Spawn(creatures, typeof(Mudpuppy), guards, shore + new Vector2(i * 1.5f, 3f), -1f, zone); }
                else if (g == 1) { Spawn(creatures, typeof(BanjoToad), guards, shore, -1f, zone); for (int i = 0; i < 4; i++) Spawn(creatures, typeof(Mudpuppy), guards, shore + new Vector2(i * 1.5f, -3f), -1f, zone); }
                else { Spawn(creatures, typeof(RoadblockTurtle), guards, shore, -1f, zone); Possum(creatures, guards, GroundAt(shore + new Vector2(4f, 4f)), false); }
            }

            // ---------- The bosses ----------
            MudBossEncounter kong = Boss<KartKong>(gameplay, "Kart Kong", Speedway, 34f, 520f, 1.6f, 3.4f, "Auxiliary Starter Motor", KeyItems.KartWheel, 0.5f, 28f);
            AddSpawner(kong, creatures[typeof(Mudpuppy)], 4, 7f);
            MudBossEncounter otter = Boss<MudbenderOtter>(gameplay, "Silt the Mudbender", Dojo, 22f, 480f, 0.55f, 1.8f, "Gyro Stabilizer", KeyItems.MudstoneBead, 0.5f, 17f);
            MudBossEncounter witch = Boss<SlothWitch>(gameplay, "Granny Drowse", WitchClearing, 28f, 420f, 0.6f, 2.2f, "Antigrav Coil", KeyItems.WitchEmber, 0.5f, 22f);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                var post = new GameObject("Hag Lantern Post");
                post.transform.SetParent(witch.transform, false);
                post.transform.position = GroundAt(WitchClearing + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 17.6f);
                post.transform.rotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 180f, 0f);
                post.AddComponent<MudProp>().Configure(MudModels.Kind.LanternPost, i);
            }
            MudBossEncounter clutch = Boss<ClutchMcCheddar>(gameplay, "Clutch McCheddar", Lot, 36f, 1100f, 1.1f, 2.6f, "Landing Gear", "", 0.66f, 30f);
            var neon = new GameObject("Neon").transform;
            neon.SetParent(clutch.transform, false);
            neon.position = GroundAt(Lot + new Vector2(0f, 24f)) + new Vector3(0f, 6.6f, -5.95f); // the sign on the roadhouse's front gable
            foreach (float x in new[] { -2.5f, 0f, 2.5f })
            {
                var light = new GameObject("Neon Light").AddComponent<Light>();
                light.transform.SetParent(neon, false);
                light.transform.localPosition = new Vector3(x, 0f, -0.5f);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.35f, 0.6f);
                light.range = 12f;
                light.intensity = 3f;
            }
            var doors = new List<Transform>();
            foreach (float x in new[] { -3f, 3f })
            {
                var door = new GameObject("Gang Door").transform;
                door.SetParent(clutch.transform, false);
                door.position = GroundAt(Lot + new Vector2(x, 15f));
                door.rotation = Quaternion.Euler(0f, 180f, 0f);
                doors.Add(door);
            }
            var route = new List<Transform>();
            foreach (Vector2 p in Roads[8])
            {
                var point = new GameObject("Getaway " + route.Count).transform;
                point.SetParent(clutch.transform, false);
                point.position = GroundAt(p);
                route.Add(point);
            }
            clutch.Boss.GetComponent<ClutchMcCheddar>().Configure(creatures[typeof(BikerMouse)], doors.ToArray(), world.Ramp, route.ToArray(), world.Pumps);
            EditorUtility.SetDirty(clutch.Boss);

            // ---------- The story ----------
            var storyRoot = new GameObject("Mudlands Story");
            storyRoot.transform.SetParent(gameplay, false);
            var story = storyRoot.AddComponent<MudlandsStory>();
            NpcActor cavendish = gameplay.GetComponentsInChildren<NpcActor>(true).First(a => a.IsQuestGiver);
            var actor = new SerializedObject(cavendish);
            actor.FindProperty("quest").objectReferenceValue = story;
            actor.ApplyModifiedPropertiesWithoutUndo();
            story.Configure(cavendish, gameplay.GetComponentInChildren<MerchantCrab>(true), world.Pile, world.Kiosk, world.Barricades, world.Still,
                world.LogJam, kong, world.Gongs, world.SpiritRoad, world.Dam, world.Flood, otter, world.Lanterns, world.HexLanterns, world.Relit,
                witch, world.Gate, clutch);
            EditorUtility.SetDirty(story);
        }

        private static Transform FindGuards(Transform gameplay, int index) =>
            gameplay.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Gong Guards").ElementAt(index);

        // The entrance triggers inside the arena (trigger < arena radius), so the fight engages as soon as it ends.
        private static MudBossEncounter Boss<T>(Transform gameplay, string name, Vector2 at, float arena, float hp, float radius, float height,
            string part, string keyItem, float phaseTwo, float trigger) where T : MudBoss
        {
            var encounter = new GameObject(name + " Encounter");
            encounter.transform.SetParent(gameplay, false);
            encounter.transform.position = GroundAt(at);
            encounter.AddComponent<QuestSignalSource>().enabled = false;
            var bossGo = new GameObject(name);
            bossGo.transform.SetParent(encounter.transform, false);
            bossGo.transform.localPosition = Vector3.zero;
            var agent = bossGo.AddComponent<NavMeshAgent>();
            agent.radius = radius;
            agent.height = height;
            agent.speed = 4f;
            agent.angularSpeed = 360f;
            agent.acceleration = 20f;
            agent.enabled = false;
            var health = bossGo.AddComponent<Health>();
            var capsule = bossGo.AddComponent<CapsuleCollider>();
            capsule.radius = radius;
            capsule.height = height;
            capsule.center = new Vector3(0f, height * 0.5f, 0f);
            T boss = bossGo.AddComponent<T>();
            var data = new SerializedObject(boss);
            data.FindProperty("displayName").stringValue = name;
            data.FindProperty("partName").stringValue = part;
            data.FindProperty("arenaRadius").floatValue = arena;
            data.FindProperty("phaseTwoAt").floatValue = phaseTwo;
            data.FindProperty("keyItemDrop").stringValue = keyItem;
            data.FindProperty("fodderWhileEngaged").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
            var healthData = new SerializedObject(health);
            healthData.FindProperty("maxHealth").floatValue = hp;
            healthData.ApplyModifiedPropertiesWithoutUndo();
            var fight = encounter.AddComponent<MudBossEncounter>();
            fight.Configure(boss, trigger);
            bossGo.SetActive(false);
            return fight;
        }

        private static void AddSpawner(MudBossEncounter fight, GameObject prefab, int cap, float interval)
        {
            var go = new GameObject("Pit Crew Spawner");
            go.transform.SetParent(fight.transform, false);
            var spawner = go.AddComponent<FodderSpawner>();
            var data = new SerializedObject(spawner);
            SerializedProperty entries = data.FindProperty("entries");
            entries.arraySize = 1;
            SerializedProperty entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("weight").floatValue = 1f;
            entry.FindPropertyRelative("minGroup").intValue = 2;
            entry.FindPropertyRelative("maxGroup").intValue = 3;
            data.FindProperty("spawnRadiusMin").floatValue = 16f;
            data.FindProperty("spawnRadiusMax").floatValue = 26f;
            data.FindProperty("activationRange").floatValue = 40f;
            data.FindProperty("maxAlive").intValue = cap;
            data.FindProperty("waveInterval").floatValue = interval;
            data.FindProperty("firstWaveDelay").floatValue = 6f;
            data.FindProperty("stopWhenDead").objectReferenceValue = fight.Boss.GetComponent<Health>();
            data.ApplyModifiedPropertiesWithoutUndo();
            var boss = new SerializedObject(fight.Boss);
            boss.FindProperty("fodderSpawner").objectReferenceValue = spawner;
            boss.FindProperty("fodderWhileEngaged").boolValue = true;
            boss.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Prefabs ----------

        private static Dictionary<Type, GameObject> BuildCreaturePrefabs()
        {
            var specs = new (Type type, CritterSpec spec)[]
            {
                (typeof(Mudpuppy), Mudpuppy.Defaults), (typeof(HogBrute), HogBrute.Defaults), (typeof(JugPossum), JugPossum.Defaults),
                (typeof(BanjoToad), BanjoToad.Defaults), (typeof(RoadblockTurtle), RoadblockTurtle.Defaults), (typeof(BikerMouse), BikerMouse.Defaults)
            };
            var prefabs = new Dictionary<Type, GameObject>();
            foreach ((Type type, CritterSpec spec) in specs)
            {
                var root = new GameObject(spec.Name);
                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = spec.Radius;
                agent.height = spec.Height;
                agent.speed = spec.Speed;
                agent.angularSpeed = 540f;
                agent.acceleration = 24f;
                agent.stoppingDistance = 0.2f;
                agent.enabled = false;
                root.AddComponent<Health>();
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.radius = spec.Radius;
                capsule.height = Mathf.Max(spec.Height, spec.Radius * 2f);
                capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                var critter = (MudCritter)root.AddComponent(type);
                var data = new SerializedObject(critter);
                data.FindProperty("territory.fightRadius").floatValue = spec.Territory;
                data.FindProperty("displayName").stringValue = spec.Name;
                data.ApplyModifiedPropertiesWithoutUndo();
                var hp = new SerializedObject(root.GetComponent<Health>());
                hp.FindProperty("maxHealth").floatValue = spec.Health;
                hp.ApplyModifiedPropertiesWithoutUndo();
                var dropper = new SerializedObject(root.AddComponent<ScrapDropper>());
                SerializedProperty drops = dropper.FindProperty("drops");
                drops.arraySize = spec.Drops.Length;
                for (int i = 0; i < spec.Drops.Length; i++)
                {
                    SerializedProperty drop = drops.GetArrayElementAtIndex(i);
                    drop.FindPropertyRelative("type").enumValueIndex = (int)spec.Drops[i].type;
                    drop.FindPropertyRelative("min").intValue = spec.Drops[i].min;
                    drop.FindPropertyRelative("max").intValue = spec.Drops[i].max;
                }
                dropper.ApplyModifiedPropertiesWithoutUndo();
                prefabs[type] = PrefabUtility.SaveAsPrefabAsset(root, EnemyFolder + "/" + type.Name + ".prefab");
                Object.DestroyImmediate(root);
            }
            return prefabs;
        }

        // Clutch's aviators on a little stand, for the camp's trophy stands (Resources/SignalLost/Trophies).
        private static void BuildTrophy()
        {
            EnsureFolder(TrophyFolder);
            var kit = new MeshKit();
            ClutchMcCheddar.AviatorsMesh(kit);
            Mesh mesh = SaveMesh(kit.Build("ClutchAviators"), Folder + "/Meshes/ClutchAviators.asset");
            var materials = new Material[kit.SubmeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = GroundMaterial(kit.SubmeshLook(i).color);
            var holder = new GameObject(ClutchMcCheddar.TrophyId);
            var model = new GameObject("Clutch's Aviators");
            model.transform.SetParent(holder.transform, false);
            model.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            model.transform.localScale = Vector3.one * 2.2f;
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            model.AddComponent<MeshRenderer>().sharedMaterials = materials;
            PrefabUtility.SaveAsPrefabAsset(holder, TrophyFolder + "/" + ClutchMcCheddar.TrophyId + ".prefab");
            Object.DestroyImmediate(holder);
        }

        // Lit materials in Resources, so code-built art keeps its shader variants in builds (MudArt).
        private static void MakeTemplateMaterials()
        {
            EnsureFolder("Assets/Resources/SignalLost");
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Material Make(string name)
            {
                string path = "Assets/Resources/SignalLost/" + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(lit);
                    AssetDatabase.CreateAsset(material, path);
                }
                return material;
            }
            Material plain = Make("MudlandsLit");
            plain.SetFloat("_Smoothness", 0.12f);
            Material glow = Make("MudlandsGlow");
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", Color.white);
            glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            Material clear = Make("MudlandsClear");
            clear.SetFloat("_Surface", 1f);
            clear.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            clear.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            clear.SetFloat("_ZWrite", 0f);
            clear.SetOverrideTag("RenderType", "Transparent");
            clear.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            clear.EnableKeyword("_EMISSION");
            clear.renderQueue = (int)RenderQueue.Transparent;
            foreach (Material material in new[] { plain, glow, clear }) EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
        }

        // ---------- Helpers ----------

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            mesh.hideFlags = HideFlags.None;
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            // Copy through the Mesh API, not EditorUtility.CopySerialized: CopySerialized updates the asset's data (and the
            // collider cooks from it) but the editor kept drawing the old geometry when the vertex count / index format
            // changed (11-20: the terrain grew from 60k to 101k vertices, and the old 400 m terrain stayed on screen,
            // see-through past its edge and with a cliff where the new pass is). Setting it through the API refreshes the
            // renderers as well.
            existing.Clear();
            existing.indexFormat = mesh.indexFormat;
            existing.vertices = mesh.vertices;
            existing.normals = mesh.normals;
            if (mesh.colors.Length == mesh.vertexCount) existing.colors = mesh.colors;
            if (mesh.uv.Length == mesh.vertexCount) existing.uv = mesh.uv;
            if (mesh.tangents.Length == mesh.vertexCount) existing.tangents = mesh.tangents;
            existing.subMeshCount = mesh.subMeshCount;
            for (int s = 0; s < mesh.subMeshCount; s++) existing.SetTriangles(mesh.GetTriangles(s), s, false);
            existing.RecalculateBounds();
            existing.name = mesh.name;
            existing.MarkModified();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        private static GameObject Instance(string path, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new InvalidOperationException("Missing prefab " + path);
            return (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        /// <summary>
        /// Points the desert's stage exit at the Mudlands, so beating the Coreworm and taking off lands you here instead
        /// of on the demo end card. A separate, optional step because it edits the desert scene (close it first).
        /// </summary>
        [MenuItem("Signal Lost/Mudlands/Link Desert Exit To Mudlands")]
        public static void LinkDesertExit()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene desert = EditorSceneManager.OpenScene(CoreSceneSetup.DesertPath, OpenSceneMode.Single);
            int linked = 0;
            foreach (DesertStageExit exit in Object.FindObjectsByType<DesertStageExit>(FindObjectsInactive.Include))
            {
                var data = new SerializedObject(exit);
                data.FindProperty("nextStageScene").stringValue = "Mudlands";
                data.ApplyModifiedPropertiesWithoutUndo();
                linked++;
            }
            EditorSceneManager.MarkSceneDirty(desert);
            EditorSceneManager.SaveScene(desert);
            Debug.Log($"Linked {linked} desert stage exit(s) to the Mudlands.");
        }

        /// <summary>Batch entry: builds everything and saves the scene.</summary>
        public static void BuildBatch()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
