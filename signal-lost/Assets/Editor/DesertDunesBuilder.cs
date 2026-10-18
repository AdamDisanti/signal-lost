using System;
using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Enemies;
using SignalLost.Player;
using SignalLost.Scanner;
using SignalLost.UI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace SignalLost.Editor
{
    /// <summary>
    /// Generates the first biome, Desert / Dunes (design doc: "open terrain with scattered wreckage", open exploration,
    /// three mini encounters before the boss, Technology-Enhanced Hermit Crab boss, Engine Core reward,
    /// respawn at the biome entrance, Cavendish as a passive NPC).
    ///
    /// Creates DesertEnvironment.prefab (terrain, perimeter dunes, rocks, mesas, alien plants, wreckage) and
    /// DesertDunes.unity (environment, lighting, player with laser/scanner/HUD, hermit crabs at three encounter sites,
    /// an empty boss arena). Like the other builders it refuses to overwrite the scene; edit it in Unity afterwards.
    /// Layout is generated from a fixed seed, so rebuilding gives the same level.
    /// </summary>
    public static class DesertDunesBuilder
    {
        public const string ScenePath = "Assets/Scenes/DesertDunes.unity";
        private const string Root = "Assets/Prefabs/Enviornment objects/DesertDunes";
        private const string MeshFolder = Root + "/Meshes";
        private const string MaterialFolder = Root + "/Materials";
        public const string EnvironmentPrefabPath = Root + "/DesertEnvironment.prefab";

        private const string ScannerRigPath = "Assets/Prefabs/Player/Scanner/ScannerRig.prefab";
        private const string HudPrefabPath = "Assets/Prefabs/UI/PlayerHUD.prefab";
        private const string CapybaraPath = "Assets/Prefabs/Enviornment objects/GrayboxProps/CapybaraAstronaut.prefab";
        private const string RocketEnginePath = "Assets/Prefabs/Enviornment objects/TutorialCrashSite/Upgrades/RocketEngine.prefab";
        private const string ShipPanelMeshPath = "Assets/Prefabs/Enviornment objects/TutorialCrashSite/Meshes/BentShipPanel.asset";
        private const string HullMaterialPath = "Assets/Prefabs/Enviornment objects/TutorialCrashSite/Materials/DamagedRocketHull.mat";
        private const string BurntMetalPath = "Assets/Prefabs/Enviornment objects/TutorialCrashSite/Materials/BurntMetal.mat";
        private const string ScorchedHullPath = "Assets/Prefabs/Enviornment objects/TutorialCrashSite/Materials/ScorchedHull.mat";

        // ---------- Layout (meters; +Z is north) ----------

        private const float MapSize = 320f;   // same footprint as the tutorial crash site
        private const int Cells = 80;         // 4 m facets
        private const float PlayableRadius = 128f;

        private static readonly Vector3 SpawnPoint = new Vector3(0f, 0f, -132f);

        private struct Site
        {
            public string Name;
            public Vector2 Center;
            public float Radius;
            public Site(string name, float x, float z, float radius) { Name = name; Center = new Vector2(x, z); Radius = radius; }
        }

        private static readonly Site Entrance = new Site("Biome Entrance", 0f, -122f, 14f);
        private static readonly Site[] Encounters =
        {
            new Site("Encounter 1 - Wreck Field", -62f, -62f, 16f),
            new Site("Encounter 2 - Mesa Pass", 78f, -18f, 16f),
            new Site("Encounter 3 - Buried Hull", -58f, 62f, 16f),
        };
        private static readonly Site BossArena = new Site("Boss Arena", 38f, 92f, 26f);

        // Crash streak: debris was flung from the south-west toward the boss arena.
        private static readonly Vector2 StreakStart = new Vector2(-30f, -105f);
        private static readonly Vector2 StreakEnd = new Vector2(38f, 92f);
        private static readonly Vector2 RibcageCenter = new Vector2(22f, -48f);

        // ---------- Colors ----------

        private static readonly Color Sand = new Color(0.87f, 0.71f, 0.48f);
        private static readonly Color DuneShade = new Color(0.78f, 0.58f, 0.38f);
        private static readonly Color Sandstone = new Color(0.69f, 0.42f, 0.29f);
        private static readonly Color RockLight = new Color(0.76f, 0.52f, 0.36f);
        private static readonly Color RockDark = new Color(0.52f, 0.33f, 0.25f);
        private static readonly Color CactusColor = new Color(0.27f, 0.66f, 0.56f);
        private static readonly Color BulbColor = new Color(0.96f, 0.46f, 0.62f);
        private static readonly Color BoneColor = new Color(0.93f, 0.9f, 0.82f);

        [MenuItem("Signal Lost/Create Desert Dunes Level")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("Desert Dunes scene already exists. Edit it in Unity instead.");
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyTestBuilder.PlayerPrefabPath);
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(EnemyTestBuilder.InputPath);
            var beam = AssetDatabase.LoadAssetAtPath<Material>(EnemyTestBuilder.BeamMaterialPath);
            var crab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyTestBuilder.CrabPrefabPath);
            var scannerRig = AssetDatabase.LoadAssetAtPath<GameObject>(ScannerRigPath);
            var hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            if (playerPrefab == null || input == null || beam == null || crab == null || scannerRig == null || hudPrefab == null)
                throw new InvalidOperationException("Player, input actions, laser beam, hermit crab, ScannerRig or PlayerHUD prefab missing. Is your branch up to date with main?");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject environmentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPrefabPath) ?? BuildEnvironmentPrefab();
            PrefabUtility.InstantiatePrefab(environmentPrefab);

            BuildLighting();
            BuildPlayer(playerPrefab, input, beam, scannerRig, hudPrefab);
            BuildEncounters(crab);
            BuildMarkers();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("DESERT_DUNES_CREATED_AND_VALIDATED");
        }

        [MenuItem("Signal Lost/Rebuild Desert Environment Prefab")]
        public static void RebuildEnvironment()
        {
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Rebuild desert environment",
                    "This regenerates DesertEnvironment.prefab, its terrain mesh and materials from the builder script, " +
                    "replacing any edits made to them in Unity. The scene keeps its instance. Continue?", "Rebuild", "Cancel"))
                return;
            BuildEnvironmentPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("DESERT_ENVIRONMENT_REBUILT");
        }

        /// <summary>
        /// Adds ScannerLaserSwap to the player of an already-created DesertDunes scene and saves it.
        /// Scenes built after this script change already have it.
        /// </summary>
        [MenuItem("Signal Lost/Add Scanner Laser Swap To Desert Scene")]
        public static void AddScannerLaserSwap()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Stop Play mode first", "Changes made in Play mode are thrown away. Press Stop, then run this again.", "OK");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("DesertDunes scene not found. Create it with Signal Lost > Create Desert Dunes Level.");
            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ScenePath);
            }

            MiningLaser laser = Object.FindAnyObjectByType<MiningLaser>();
            if (laser == null) throw new InvalidOperationException("No player with a MiningLaser in the DesertDunes scene.");
            GameObject player = laser.gameObject;
            if (player.GetComponent<ScannerLaserSwap>() == null) Undo.AddComponent<ScannerLaserSwap>(player);
            EditorSceneManager.MarkSceneDirty(player.scene);
            EditorSceneManager.SaveScene(player.scene);
            Selection.activeGameObject = player;
            Debug.Log("SCANNER_LASER_SWAP_ADDED to " + player.name + " in " + ScenePath);
        }

        public static void Validate()
        {
            EditorSceneManager.OpenScene(ScenePath);
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<FirstPersonController>() == 1, "Expected one player.");
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<MiningLaser>() == 1, "Expected one mining laser.");
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<PlayerHud>() == 1, "Expected one PlayerHud.");
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<CombatDebugHud>() == 0, "Debug HUD should be removed.");
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<ScannerController>() == 1, "Expected one scanner.");
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<ScannerLaserSwap>() == 1, "Expected one ScannerLaserSwap on the player.");
            var crabs = EnemyTestBuilder.All<HermitCrab>();
            EnemyTestBuilder.Require(crabs.Count == 5, "Expected five hermit crabs.");
            foreach (HermitCrab crab in crabs)
                EnemyTestBuilder.Require(crab.GetComponent<EnemySignalSource>() != null, crab.name + " has no EnemySignalSource.");
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<NavMeshSurface>() == 1 && EnemyTestBuilder.Count<NavMeshBaker>() == 1,
                "Expected one NavMeshSurface with a NavMeshBaker.");
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<Camera>() == 1 && EnemyTestBuilder.Count<AudioListener>() == 1,
                "Expected one camera and listener.");
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    EnemyTestBuilder.Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0, "Missing script: " + item.name);
            Debug.Log("DESERT_DUNES_VALIDATED");
        }

        // =====================================================================
        // Height field
        // =====================================================================

        private static float SmoothStep01(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static float Noise(float x, float z, float scale, float ox, float oz) =>
            (Mathf.PerlinNoise(x / scale + ox, z / scale + oz) - 0.5f) * 2f;

        /// <summary>Rolling dunes before sites and borders are applied.</summary>
        private static float DuneHeight(float x, float z)
        {
            // Crests run roughly north-west to south-east, wobbling so they don't look ruled.
            float u = x * 0.8f + z * 0.6f, v = -x * 0.6f + z * 0.8f;
            float wave = 0.5f + 0.5f * Mathf.Sin(u * (Mathf.PI * 2f / 44f) + 1.4f * Mathf.Sin(v / 31f));
            float dunes = 3.2f * Mathf.Pow(wave, 1.7f);
            float swell = 3.5f * Noise(x, z, 95f, 11.3f, 7.1f);
            float ripple = 0.7f * Noise(x, z, 17f, 3.7f, 5.9f);
            return dunes + swell + ripple;
        }

        private static float Height(float x, float z)
        {
            float h = DuneHeight(x, z);

            // Flatten gameplay spaces so fights and spawns happen on fair ground.
            foreach (Site site in AllSites())
            {
                float d = Vector2.Distance(new Vector2(x, z), site.Center);
                float flat = DuneHeight(site.Center.x, site.Center.y);
                h = Mathf.Lerp(h, flat, 1f - SmoothStep01(site.Radius, site.Radius + 14f, d));
            }

            // Boss arena: shallow sand bowl with a raised rim.
            float boss = Vector2.Distance(new Vector2(x, z), BossArena.Center);
            if (boss < BossArena.Radius) h -= 2.2f * (1f - (boss / BossArena.Radius) * (boss / BossArena.Radius));
            h += 1.6f * Mathf.Exp(-Mathf.Pow((boss - BossArena.Radius - 3f) / 4f, 2f));

            // Perimeter: towering dunes all round (a rounded square) that are far too steep to climb.
            float edge = Mathf.Pow(Mathf.Pow(Mathf.Abs(x), 4f) + Mathf.Pow(Mathf.Abs(z), 4f), 0.25f);
            float angle = Mathf.Atan2(z, x);
            float inner = PlayableRadius + 7f * Noise(Mathf.Cos(angle) * 40f, Mathf.Sin(angle) * 40f, 13f, 2.2f, 8.4f);
            // Entrance canyon: a narrow notch in the south wall the player arrives through.
            if (z < 0f) inner += 22f * (1f - SmoothStep01(4f, 11f, Mathf.Abs(x)));
            float wall = SmoothStep01(inner, inner + 13f, edge);
            h += wall * (24f + 5f * Noise(x, z, 23f, 9.1f, 1.7f));
            return h;
        }

        private static IEnumerable<Site> AllSites()
        {
            yield return Entrance;
            foreach (Site site in Encounters) yield return site;
            yield return BossArena;
        }

        private static float[,] heightGrid;

        private static void EnsureGrid()
        {
            if (heightGrid != null) return;
            float step = MapSize / Cells, origin = -MapSize * 0.5f;
            heightGrid = new float[Cells + 1, Cells + 1];
            for (int i = 0; i <= Cells; i++)
                for (int j = 0; j <= Cells; j++)
                    heightGrid[i, j] = Height(origin + i * step, origin + j * step);
        }

        private static bool FlipDiagonal(int i, int j) => ((i + j) & 1) == 0;

        /// <summary>Exact surface height of the generated terrain mesh (same triangles), for placing props.</summary>
        private static float Ground(float x, float z)
        {
            EnsureGrid();
            float step = MapSize / Cells, origin = -MapSize * 0.5f;
            float fx = Mathf.Clamp((x - origin) / step, 0f, Cells - 0.0001f), fz = Mathf.Clamp((z - origin) / step, 0f, Cells - 0.0001f);
            int i = (int)fx, j = (int)fz;
            float u = fx - i, v = fz - j;
            float h00 = heightGrid[i, j], h10 = heightGrid[i + 1, j], h01 = heightGrid[i, j + 1], h11 = heightGrid[i + 1, j + 1];
            if (FlipDiagonal(i, j))
                // Diagonal from (0,0) to (1,1).
                return u >= v ? h00 + u * (h10 - h00) + v * (h11 - h10) : h00 + v * (h01 - h00) + u * (h11 - h01);
            // Diagonal from (1,0) to (0,1).
            return u + v <= 1f ? h00 + u * (h10 - h00) + v * (h01 - h00) : h11 + (1f - u) * (h01 - h11) + (1f - v) * (h10 - h11);
        }

        private static Vector3 OnGround(float x, float z, float sink = 0f) => new Vector3(x, Ground(x, z) - sink, z);

        // =====================================================================
        // Environment prefab
        // =====================================================================

        private static GameObject BuildEnvironmentPrefab()
        {
            heightGrid = null;
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Prefabs/Enviornment objects");
            EnsureFolder(Root);
            EnsureFolder(MeshFolder);
            EnsureFolder(MaterialFolder);

            var mats = new Dictionary<string, Material>
            {
                ["Sand"] = MakeMaterial("DesertSand", Sand, 0.08f),
                ["Shade"] = MakeMaterial("DuneShade", DuneShade, 0.06f),
                ["Sandstone"] = MakeMaterial("Sandstone", Sandstone, 0.05f),
                ["RockLight"] = MakeMaterial("MesaRockLight", RockLight, 0.05f),
                ["RockDark"] = MakeMaterial("MesaRockDark", RockDark, 0.05f),
                ["Cactus"] = MakeMaterial("AlienCactus", CactusColor, 0.25f),
                ["Bulb"] = MakeMaterial("AlienBulb", BulbColor, 0.45f),
                ["Bone"] = MakeMaterial("BleachedBone", BoneColor, 0.2f),
            };
            Material[] wreckMats =
            {
                AssetDatabase.LoadAssetAtPath<Material>(HullMaterialPath) ?? mats["RockDark"],
                AssetDatabase.LoadAssetAtPath<Material>(BurntMetalPath) ?? mats["RockDark"],
                AssetDatabase.LoadAssetAtPath<Material>(ScorchedHullPath) ?? mats["RockDark"],
            };

            Mesh terrain = SaveMesh(BuildTerrainMesh());
            Mesh boulder = SaveMesh(LowPolyMesh.Icosphere("DesertBoulder", 0));
            Mesh pebble = SaveMesh(LowPolyMesh.Icosphere("DesertRoundRock", 1));
            Mesh mesa6 = SaveMesh(LowPolyMesh.TaperedPrism("DesertMesa6", 6, 0.78f));
            Mesh mesa7 = SaveMesh(LowPolyMesh.TaperedPrism("DesertMesa7", 7, 0.7f));
            Mesh spire = SaveMesh(LowPolyMesh.TaperedPrism("DesertSpire", 5, 0.35f));
            Mesh rib = SaveMesh(LowPolyMesh.TaperedPrism("DesertRib", 5, 0.15f));
            Mesh panel = AssetDatabase.LoadAssetAtPath<Mesh>(ShipPanelMeshPath);

            var rng = new Random(20261018);
            var root = new GameObject("Desert Environment");
            var surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.3f; // coarser than default so the big level bakes quickly at startup
            root.AddComponent<NavMeshBaker>();

            // Terrain
            var ground = new GameObject("Dune Terrain");
            ground.transform.SetParent(root.transform, false);
            ground.AddComponent<MeshFilter>().sharedMesh = terrain;
            ground.AddComponent<MeshRenderer>().sharedMaterials = new[] { mats["Sand"], mats["Shade"], mats["Sandstone"] };
            ground.AddComponent<MeshCollider>().sharedMesh = terrain;
            GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.BatchingStatic);

            var placed = new List<Vector3>();

            // Mesas: big flat-topped landmarks, visible from far away.
            Transform mesas = Group("Mesas", root.transform);
            for (int n = 0, tries = 0; n < 12 && tries < 500; tries++)
            {
                Vector2 p = RandomPoint(rng, 112f);
                if (!Clear(p, 22f, placed, 30f)) continue;
                float radius = Range(rng, 6f, 12f), height = Range(rng, 7f, 15f);
                Solid(n % 2 == 0 ? mesa6 : mesa7, "Mesa " + (n + 1), mesas, OnGround(p.x, p.y, 1.5f),
                    Quaternion.Euler(-90f, Range(rng, 0f, 360f), 0f), new Vector3(radius, radius * Range(rng, 0.7f, 1f), height),
                    rng.NextDouble() < 0.5 ? mats["RockLight"] : mats["RockDark"], false);
                placed.Add(new Vector3(p.x, 0f, p.y));
                n++;
            }

            // Boulders: cover and scatter.
            Transform rocks = Group("Rocks", root.transform);
            for (int n = 0, tries = 0; n < 70 && tries < 2000; tries++)
            {
                Vector2 p = RandomPoint(rng, 122f);
                if (!Clear(p, 6f, placed, 4f)) continue;
                float s = Range(rng, 0.6f, 2.8f);
                bool round = rng.NextDouble() < 0.4;
                Solid(round ? pebble : boulder, "Rock " + (n + 1), rocks, OnGround(p.x, p.y, s * 0.35f),
                    Quaternion.Euler(Range(rng, 0f, 360f), Range(rng, 0f, 360f), Range(rng, 0f, 360f)),
                    new Vector3(s * Range(rng, 0.9f, 1.5f), s * Range(rng, 0.5f, 0.9f), s * Range(rng, 0.9f, 1.4f)),
                    rng.NextDouble() < 0.6 ? mats["RockLight"] : mats["RockDark"], s > 1.1f);
                placed.Add(new Vector3(p.x, 0f, p.y));
                n++;
            }

            // Goofy alien cacti: teal stems with pink bulbs.
            Transform plants = Group("Alien Plants", root.transform);
            for (int n = 0, tries = 0; n < 34 && tries < 2000; tries++)
            {
                Vector2 p = RandomPoint(rng, 118f);
                if (!Clear(p, 5f, placed, 3f)) continue;
                Cactus(rng, "Alien Cactus " + (n + 1), plants, p, mats["Cactus"], mats["Bulb"]);
                placed.Add(new Vector3(p.x, 0f, p.y));
                n++;
            }

            // Wreckage: a streak of ship parts from the crash, plus piles at every encounter site.
            Transform wreckage = Group("Wreckage", root.transform);
            if (panel != null)
            {
                for (int n = 0; n < 48; n++)
                {
                    float t = (float)rng.NextDouble();
                    Vector2 along = Vector2.Lerp(StreakStart, StreakEnd, t);
                    Vector2 side = new Vector2(-(StreakEnd - StreakStart).y, (StreakEnd - StreakStart).x).normalized;
                    Vector2 p = along + side * Range(rng, -22f, 22f) * (0.4f + t);
                    if (InsideAnySite(p, 2f) || !InsidePlayable(p, 6f)) continue;
                    float s = Range(rng, 0.8f, 3.2f);
                    Panel(panel, "Debris " + (n + 1), wreckage, p, s, rng, wreckMats);
                }
                foreach (Site site in Encounters)
                {
                    Transform pile = Group(site.Name + " Wreck", wreckage);
                    for (int n = 0; n < 7; n++)
                    {
                        float a = Range(rng, 0f, Mathf.PI * 2f), r = Range(rng, site.Radius * 0.55f, site.Radius * 0.95f);
                        Vector2 p = site.Center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                        Panel(panel, "Hull Plate " + (n + 1), pile, p, Range(rng, 2.5f, 4.5f), rng, wreckMats);
                    }
                }
            }

            // Bones of something big: ribs arching out of the sand near the middle of the map.
            Transform bones = Group("Giant Ribcage", root.transform);
            Vector2 cage = RibcageCenter;
            for (int n = 0; n < 7; n++)
            {
                float offset = (n - 3) * 3.2f;
                for (int sideSign = -1; sideSign <= 1; sideSign += 2)
                {
                    Vector2 p = cage + new Vector2(offset, sideSign * 3.5f);
                    float height = 7.5f - Mathf.Abs(n - 3) * 1.1f;
                    Solid(rib, $"Rib {n + 1}{(sideSign < 0 ? "L" : "R")}", bones, OnGround(p.x, p.y, 0.8f),
                        Quaternion.Euler(-90f - sideSign * 28f, 0f, 0f), new Vector3(0.45f, 0.45f, height), mats["Bone"], true);
                }
            }

            // Boss arena ring: tall spires around the bowl, with openings to the south and west.
            Transform arena = Group("Boss Arena Spires", root.transform);
            for (int n = 0; n < 16; n++)
            {
                float a = n * Mathf.PI * 2f / 16f;
                float deg = a * Mathf.Rad2Deg;
                if (Mathf.Abs(Mathf.DeltaAngle(deg, 270f)) < 25f || Mathf.Abs(Mathf.DeltaAngle(deg, 180f)) < 20f) continue;
                Vector2 p = BossArena.Center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (BossArena.Radius + Range(rng, 3f, 6f));
                float r = Range(rng, 2.2f, 3.6f);
                Solid(spire, "Spire " + (n + 1), arena, OnGround(p.x, p.y, 1f),
                    Quaternion.Euler(-90f + Range(rng, -6f, 6f), Range(rng, 0f, 360f), Range(rng, -6f, 6f)),
                    new Vector3(r, r, Range(rng, 10f, 17f)), rng.NextDouble() < 0.5 ? mats["RockLight"] : mats["RockDark"], true);
            }

            // Engine wreck in the middle of the arena: a ship engine half-swallowed by sand.
            var enginePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RocketEnginePath);
            if (enginePrefab != null)
            {
                var engine = (GameObject)PrefabUtility.InstantiatePrefab(enginePrefab);
                engine.name = "Engine Wreck";
                engine.transform.SetParent(root.transform, false);
                engine.transform.SetPositionAndRotation(OnGround(BossArena.Center.x + 6f, BossArena.Center.y + 8f, 0.6f), Quaternion.Euler(28f, 140f, 12f));
                engine.transform.localScale = Vector3.one * 2.2f;
            }

            return EnemyTestBuilder.SavePrefab(root, EnvironmentPrefabPath);
        }

        private static Mesh BuildTerrainMesh()
        {
            EnsureGrid();
            float step = MapSize / Cells, origin = -MapSize * 0.5f;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var sub = new[] { new List<int>(), new List<int>(), new List<int>() }; // flat sand, slopes, cliffs

            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                if (n.y < 0f) { (b, c) = (c, b); n = -n; }
                float slope = Mathf.Acos(Mathf.Clamp01(n.y)) * Mathf.Rad2Deg;
                List<int> target = slope < 16f ? sub[0] : slope < 38f ? sub[1] : sub[2];
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                normals.Add(n); normals.Add(n); normals.Add(n);
                target.Add(start); target.Add(start + 1); target.Add(start + 2);
            }

            for (int i = 0; i < Cells; i++)
                for (int j = 0; j < Cells; j++)
                {
                    Vector3 p00 = new Vector3(origin + i * step, heightGrid[i, j], origin + j * step);
                    Vector3 p10 = new Vector3(origin + (i + 1) * step, heightGrid[i + 1, j], origin + j * step);
                    Vector3 p01 = new Vector3(origin + i * step, heightGrid[i, j + 1], origin + (j + 1) * step);
                    Vector3 p11 = new Vector3(origin + (i + 1) * step, heightGrid[i + 1, j + 1], origin + (j + 1) * step);
                    if (FlipDiagonal(i, j)) { Tri(p00, p11, p10); Tri(p00, p01, p11); }
                    else { Tri(p00, p01, p10); Tri(p10, p01, p11); }
                }

            var mesh = new Mesh { name = "DesertDunesTerrain" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.subMeshCount = 3;
            for (int s = 0; s < 3; s++) mesh.SetTriangles(sub[s], s);
            mesh.RecalculateBounds();
            return mesh;
        }

        // =====================================================================
        // Scene contents
        // =====================================================================

        private static void BuildLighting()
        {
            var light = new GameObject("Desert Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.9f, 0.74f);
            light.intensity = 1.6f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            light.gameObject.AddComponent<UniversalAdditionalLightData>();

            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader == null) throw new InvalidOperationException("Skybox/Procedural shader missing.");
            var sky = new Material(skyShader) { name = "DesertSky" };
            sky.SetFloat("_SunSize", 0.05f);
            sky.SetFloat("_AtmosphereThickness", 1.35f);
            sky.SetColor("_SkyTint", new Color(0.9f, 0.68f, 0.5f));
            sky.SetColor("_GroundColor", new Color(0.78f, 0.62f, 0.46f));
            sky.SetFloat("_Exposure", 1.25f);
            string skyPath = MaterialFolder + "/DesertSky.mat";
            var existingSky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (existingSky == null) AssetDatabase.CreateAsset(sky, skyPath); else { EditorUtility.CopySerialized(sky, existingSky); sky = existingSky; }
            RenderSettings.skybox = sky;
            RenderSettings.sun = light;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.7f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.72f, 0.58f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.46f, 0.35f, 0.27f);

            // Warm haze: the far edge of the map fades into dust.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.91f, 0.79f, 0.63f);
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 260f;
        }

        private static void BuildPlayer(GameObject playerPrefab, InputActionAsset input, Material beam, GameObject scannerRig, GameObject hudPrefab)
        {
            // Same laser/health/respawn setup as the enemy test, so the desert plays like the tested prototypes.
            EnemyTestBuilder.BuildPlayer(playerPrefab, input, beam);
            MiningLaser laser = Object.FindAnyObjectByType<MiningLaser>();
            GameObject player = laser.gameObject;
            player.name = "Player";
            // PlaceholderRespawn returns the player here: the biome entrance, as the design doc describes.
            player.transform.SetPositionAndRotation(OnGround(SpawnPoint.x, SpawnPoint.z) + Vector3.up * 0.1f, Quaternion.identity);

            var debugHud = player.GetComponent<CombatDebugHud>();
            if (debugHud != null) Object.DestroyImmediate(debugHud);

            Camera camera = player.GetComponentInChildren<Camera>();
            camera.farClipPlane = 600f;
            PrefabUtility.InstantiatePrefab(scannerRig, camera.transform);
            // Put the laser away while the scanner is raised so the scanner display can be seen.
            player.AddComponent<ScannerLaserSwap>();

            var hud = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab);
            var hudData = new SerializedObject(hud.GetComponent<PlayerHud>());
            hudData.FindProperty("playerHealth").objectReferenceValue = player.GetComponent<Health>();
            hudData.FindProperty("laser").objectReferenceValue = laser;
            hudData.FindProperty("viewpoint").objectReferenceValue = camera.transform;
            hudData.ApplyModifiedPropertiesWithoutUndo();

            // Amanda's scanner shows its own arrow and meter on the device (Q), so the HUD copy is hidden here
            // until the team decides where the scanner display lives.
            Transform hudScanner = hud.transform.Find("Scanner");
            if (hudScanner != null) hudScanner.gameObject.SetActive(false);
            Text objective = hud.transform.Find("Objective/Text")?.GetComponent<Text>();
            if (objective != null) objective.text = "Scan for creatures (press Q)";
        }

        private static void BuildEncounters(GameObject crabPrefab)
        {
            var group = new GameObject("Encounters").transform;
            int[] crabsPerSite = { 1, 2, 2 };
            for (int s = 0; s < Encounters.Length; s++)
            {
                Site site = Encounters[s];
                var siteRoot = new GameObject(site.Name).transform;
                siteRoot.SetParent(group, false);
                siteRoot.position = OnGround(site.Center.x, site.Center.y);
                for (int c = 0; c < crabsPerSite[s]; c++)
                {
                    var crab = (GameObject)PrefabUtility.InstantiatePrefab(crabPrefab, siteRoot);
                    crab.name = $"Hermit Crab {s + 1}{(char)('A' + c)}";
                    Vector2 p = site.Center + new Vector2(c == 0 ? -2.5f : 2.5f, crabsPerSite[s] == 1 ? 0f : 1.5f);
                    crab.transform.SetPositionAndRotation(OnGround(p.x, p.y), Quaternion.Euler(0f, 180f + c * 40f, 0f));
                    // Lets Amanda's scanner find this creature. Crabs keep their prefab default of staying dead.
                    crab.AddComponent<EnemySignalSource>();
                }
            }
        }

        private static void BuildMarkers()
        {
            var markers = new GameObject("Level Markers (placeholders)").transform;

            // Cavendish, the capybara quest giver, waits at the entrance (passive NPC; no quest logic yet).
            var capybaraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CapybaraPath);
            if (capybaraPrefab != null)
            {
                var capybara = (GameObject)PrefabUtility.InstantiatePrefab(capybaraPrefab, markers);
                capybara.name = "Cavendish (quest giver)";
                capybara.transform.SetPositionAndRotation(OnGround(5f, -122f), Quaternion.Euler(0f, 210f, 0f));
            }

            Marker("Merchant Spot (hermit crab merchant goes here)", markers, OnGround(-6f, -120f));
            Marker("Boss Spawn (Tech-Enhanced Hermit Crab goes here)", markers, OnGround(BossArena.Center.x, BossArena.Center.y));
            Marker("Engine Core Drop (boss reward)", markers, OnGround(BossArena.Center.x, BossArena.Center.y - 4f));
            Marker("Biome Exit (to Fog Forest, not built)", markers, OnGround(BossArena.Center.x - 40f, BossArena.Center.y + 18f));
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private static void Cactus(Random rng, string name, Transform parent, Vector2 p, Material stemMat, Material bulbMat)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = OnGround(p.x, p.y, 0.2f);
            root.rotation = Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f);
            float height = Range(rng, 1.6f, 3.4f);

            GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            stem.name = "Stem";
            stem.transform.SetParent(root, false);
            stem.transform.localPosition = Vector3.up * height * 0.5f;
            stem.transform.localScale = new Vector3(0.55f, height * 0.5f, 0.55f);
            stem.GetComponent<Renderer>().sharedMaterial = stemMat;

            int arms = rng.Next(0, 3);
            for (int a = 0; a < arms; a++)
            {
                GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                arm.name = "Arm " + (a + 1);
                Object.DestroyImmediate(arm.GetComponent<Collider>());
                arm.transform.SetParent(root, false);
                float side = a == 0 ? 1f : -1f;
                arm.transform.localPosition = new Vector3(side * 0.45f, height * Range(rng, 0.45f, 0.7f), 0f);
                arm.transform.localRotation = Quaternion.Euler(0f, 0f, -side * 35f);
                arm.transform.localScale = new Vector3(0.32f, 0.45f, 0.32f);
                arm.GetComponent<Renderer>().sharedMaterial = stemMat;
            }

            int bulbs = rng.Next(1, 4);
            for (int b = 0; b < bulbs; b++)
            {
                GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.name = "Bulb " + (b + 1);
                Object.DestroyImmediate(bulb.GetComponent<Collider>());
                bulb.transform.SetParent(root, false);
                float a = b * 2.1f;
                bulb.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.18f * b, height + 0.05f + 0.12f * b, Mathf.Sin(a) * 0.18f * b);
                bulb.transform.localScale = Vector3.one * Range(rng, 0.3f, 0.5f);
                bulb.GetComponent<Renderer>().sharedMaterial = bulbMat;
            }
        }

        private static void Panel(Mesh mesh, string name, Transform parent, Vector2 p, float scale, Random rng, Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(OnGround(p.x, p.y, 0.15f * scale),
                Quaternion.Euler(Range(rng, -35f, 35f), Range(rng, 0f, 360f), Range(rng, -35f, 35f)));
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = materials[rng.Next(materials.Length)];
            if (scale >= 1.5f) go.AddComponent<MeshCollider>().convex = true; // small chips are decoration only
        }

        private static GameObject Solid(Mesh mesh, string name, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale, Material material, bool convex)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = convex;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        private static void Marker(string name, Transform parent, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
        }

        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Vector2 RandomPoint(Random rng, float radius) =>
            new Vector2(Range(rng, -radius, radius), Range(rng, -radius, radius));

        private static float Range(Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        private static bool InsideAnySite(Vector2 p, float margin)
        {
            foreach (Site site in AllSites())
                if (Vector2.Distance(p, site.Center) < site.Radius + margin) return true;
            return false;
        }

        private static bool InsidePlayable(Vector2 p, float margin) =>
            Mathf.Pow(Mathf.Pow(Mathf.Abs(p.x), 4f) + Mathf.Pow(Mathf.Abs(p.y), 4f), 0.25f) < PlayableRadius - margin;

        /// <summary>Keeps scatter out of gameplay spaces, the entrance path and away from other big pieces.</summary>
        private static bool Clear(Vector2 p, float siteMargin, List<Vector3> placed, float spacing)
        {
            if (InsideAnySite(p, siteMargin)) return false;
            if (Mathf.Abs(p.x) < 8f && p.y < -100f) return false; // entrance canyon
            if (!InsidePlayable(p, 8f)) return false;              // not up on the perimeter wall
            if (Vector2.Distance(p, RibcageCenter) < 14f) return false;
            foreach (Vector3 other in placed)
                if ((new Vector2(other.x, other.z) - p).sqrMagnitude < spacing * spacing) return false;
            return true;
        }

        private static Material MakeMaterial(string name, Color color, float smoothness)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader missing.");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Mesh SaveMesh(Mesh mesh)
        {
            string path = MeshFolder + "/" + mesh.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
