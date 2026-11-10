using System;
using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Enemies;
using SignalLost.Finale;
using SignalLost.Inventory;
using SignalLost.Mudlands;
using SignalLost.Player;
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
    /// Builds the final level's first playable slice (docs/final-boss.md): the crash-site island laid waste, with the
    /// arena south of the wreck where Cavendish's mech waits. The island keeps the tutorial's shape (its ground height
    /// is the tutorial's, with the pond drained and the arena levelled), so players recognise it; everything on it is
    /// ruined: ash and cracked earth, dead trees and stumps, scattered wreckage, smoke on the horizon, and the old camp
    /// knocked flat. The arena has cover (boulders and hull chunks) and seven hiding spots for the shield conduits.
    /// The approach, its enemies and the cutscenes aren't built yet: the player starts at the arena's south edge.
    /// Like the other level builders it replaces the scene: make edits here, not in the scene.
    /// Menu: Signal Lost > Finale > Build Wasteland Arena.
    /// </summary>
    internal static class FinaleBuilder
    {
        public const string ScenePath = "Assets/Scenes/Large Areas/Wasteland.unity";
        private const string Folder = "Assets/Prefabs/Enviornment objects/Wasteland";
        private const string NavMeshPath = "Assets/Scenes/Large Areas/WastelandNavMesh.asset";
        private const string CavendishPath = "Assets/Prefabs/Enviornment objects/GrayboxProps/CapybaraAstronaut.prefab";
        private const string EugenePath = "Assets/Prefabs/NPC/MerchantCrab.prefab";
        private static readonly string[] CousinPaths = { "Assets/Prefabs/Enemies/Cousins/Moe.prefab", "Assets/Prefabs/Enemies/Cousins/Larry.prefab", "Assets/Prefabs/Enemies/Cousins/Curly.prefab" };
        /// <summary>Stand-ins for the adds until each island's own enemies are built (Adam, 11-09).</summary>
        private static readonly string[] AddPaths =
        {
            "Assets/Prefabs/Enemies/HermitCrab.prefab", "Assets/Prefabs/Enemies/Desert/SparkTick.prefab",
            "Assets/Prefabs/Enemies/Desert/HubcapArmadillo.prefab", "Assets/Prefabs/Enemies/Mudlands/Mudpuppy.prefab"
        };

        private const float MapSize = 320f;
        private const int Cells = 106;
        private const float SeaLevel = TutorialLevelBuilder.SeaLevel;
        public static readonly Vector2 ArenaCentre = new Vector2(8f, -30f);
        public const float ArenaRadius = 40f;
        /// <summary>Where the mech kneels at the start (and goes back to on a retry): by the wreck, facing the arena.</summary>
        public static readonly Vector2 MechHome = new Vector2(9f, -13f);
        public static readonly Vector2 PlayerStart = ArenaCentre + new Vector2(0f, -39f);

        private static Func<float, float, float> shipBed;
        private static float arenaLevel;

        [MenuItem("Signal Lost/Finale/Build Wasteland Arena")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                !EditorUtility.DisplayDialog("Rebuild the Wasteland?", "This replaces Scenes/Large Areas/Wasteland.unity, including any hand edits.", "Rebuild", "Cancel"))
                return;
            Build();
        }

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

        public static void Build()
        {
            EnsureFolder(Folder + "/Materials");
            EnsureFolder(Folder + "/Meshes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var random = new System.Random(20261109);
            arenaLevel = TutorialLevelBuilder.Height(ArenaCentre.x, ArenaCentre.y);
            var level = new GameObject("Level").transform;
            var clear = new List<Vector3>(); // x, z, radius: kept free of scenery and grass
            try
            {
                Transform ship = PlaceShip(level);
                shipBed = (x, z) => BedUnder(ship, x, z);
                GameObject ground = BuildTerrain(level);
                Physics.SyncTransforms();
                clear.Add(new Vector3(ArenaCentre.x, ArenaCentre.y, ArenaRadius + 3f));
                clear.Add(new Vector3(TutorialLevelBuilder.Ship.x, TutorialLevelBuilder.Ship.y, 20f));
                BuildShore(level, clear);
                BuildRuinedCamp(level, clear);
                Transform arena = BuildArena(level, ship, random, out List<Vector3> hideSpots);
                BuildScenery(level, clear, random);
                Physics.SyncTransforms();
                BakeNavigation(level);
                BuildLighting();
                BuildFoliage(level, ground, clear);
                BuildGameplay(arena, hideSpots);
            }
            finally
            {
                shipBed = null;
            }
            EditorSceneManager.SaveScene(scene, ScenePath);
            CoreSceneSetup.SetBuildScenes();
            Debug.Log("WASTELAND_ARENA_BUILT");
        }

        // ---------- Height ----------

        private static float Noise(float x, float z, float scale, float seed = 0f) => (Mathf.PerlinNoise(x / scale + 410f + seed, z / scale + 93f + seed) - 0.5f) * 2f;

        /// <summary>The tutorial island's ground, with the arena levelled and the ground dug out under the wreck.</summary>
        public static float Height(float x, float z)
        {
            float h = TutorialLevelBuilder.Height(x, z);
            float d = Vector2.Distance(new Vector2(x, z), ArenaCentre);
            if (d < ArenaRadius + 12f)
                h = Mathf.Lerp(arenaLevel + 0.15f * Noise(x, z, 6f), h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ArenaRadius - 2f, ArenaRadius + 12f, d)));
            return shipBed != null ? Mathf.Min(h, shipBed(x, z)) : h;
        }

        public static Vector3 GroundAt(Vector2 p) => new Vector3(p.x, Height(p.x, p.y), p.y);

        private static float BedUnder(Transform ship, float x, float z)
        {
            if ((new Vector2(x, z) - TutorialLevelBuilder.Ship).sqrMagnitude > 24f * 24f) return float.PositiveInfinity;
            float lowest = float.PositiveInfinity;
            const float reach = 1.8f;
            for (int i = -1; i <= 1; i++)
            for (int k = -1; k <= 1; k++)
            {
                var from = new Vector3(x + i * reach, -100f, z + k * reach);
                foreach (RaycastHit hit in Physics.RaycastAll(from, Vector3.up, 300f, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.collider.transform.IsChildOf(ship)) lowest = Mathf.Min(lowest, hit.point.y);
            }
            return lowest - 0.12f;
        }

        // ---------- Terrain ----------

        private static readonly Color Ash = new Color(0.43f, 0.41f, 0.38f), AshDark = new Color(0.33f, 0.31f, 0.29f),
            Cracked = new Color(0.55f, 0.46f, 0.34f), DeadGrass = new Color(0.52f, 0.48f, 0.3f), Scorched = new Color(0.17f, 0.15f, 0.14f),
            Rock = new Color(0.45f, 0.43f, 0.41f), Sand = new Color(0.7f, 0.63f, 0.48f), Seabed = new Color(0.44f, 0.42f, 0.37f);

        private static Color Surface(Vector3 centre, Vector3 normal)
        {
            float r = new Vector2(centre.x, centre.z).magnitude;
            if (centre.y < SeaLevel + 1.4f && r > TutorialLevelBuilder.PlayRadius - 10f) return centre.y < SeaLevel - 0.05f ? Seabed : Sand;
            if (normal.y < 0.8f) return Rock;
            float arena = Vector2.Distance(new Vector2(centre.x, centre.z), ArenaCentre);
            float wreck = Vector2.Distance(new Vector2(centre.x, centre.z), TutorialLevelBuilder.Ship);
            float n = Noise(centre.x, centre.z, 16f), m = Noise(centre.x, centre.z, 5f, 7f);
            if (wreck < 18f + n * 4f || (arena < ArenaRadius && m > 0.45f)) return Scorched;
            if (arena < ArenaRadius) return n > 0.1f ? Cracked : (m > 0f ? Ash : AshDark);
            if (n > 0.35f) return DeadGrass;
            if (n < -0.3f) return AshDark;
            return m > 0.2f ? Cracked : Ash;
        }

        private static GameObject BuildTerrain(Transform level)
        {
            float step = MapSize / Cells, origin = -MapSize * 0.5f;
            var heights = new float[Cells + 1, Cells + 1];
            for (int i = 0; i <= Cells; i++)
                for (int j = 0; j <= Cells; j++)
                    heights[i, j] = Height(origin + i * step, origin + j * step);
            var kit = new MeshKit();
            var seabedKit = new MeshKit();
            for (int i = 0; i < Cells; i++)
                for (int j = 0; j < Cells; j++)
                {
                    var a = new Vector3(origin + i * step, heights[i, j], origin + j * step);
                    var b = new Vector3(origin + (i + 1) * step, heights[i + 1, j], origin + j * step);
                    var c = new Vector3(origin + (i + 1) * step, heights[i + 1, j + 1], origin + (j + 1) * step);
                    var d = new Vector3(origin + i * step, heights[i, j + 1], origin + (j + 1) * step);
                    bool flip = (i + j) % 2 == 0;
                    // The sea floor is its own object, marked not walkable, so the NavMesh and creatures stay on land.
                    bool under = Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y)) < SeaLevel - 0.6f &&
                                 new Vector2(origin + (i + 0.5f) * step, origin + (j + 0.5f) * step).magnitude > TutorialLevelBuilder.PlayRadius - 4f;
                    MeshKit target = under ? seabedKit : kit;
                    Facet(target, a, flip ? d : b, c);
                    Facet(target, a, c, flip ? b : d);
                }
            GameObject seabed = MeshObject(level, "Seabed", seabedKit, "WastelandSeabed");
            IslandKit.NoWalk(seabed);
            return MeshObject(level, "Terrain", kit, "WastelandTerrain");
        }

        private static void Facet(MeshKit kit, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            if (normal.y < 0f) { (b, c) = (c, b); normal = -normal; }
            kit.Tri(a, b, c, Surface((a + b + c) / 3f, normal));
        }

        private static GameObject MeshObject(Transform level, string name, MeshKit kit, string meshName)
        {
            Mesh mesh = SaveMesh(kit.Build(meshName), Folder + "/Meshes/" + meshName + ".asset");
            var go = new GameObject(name);
            go.transform.SetParent(level, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var materials = new Material[kit.SubmeshCount];
            for (int s = 0; s < materials.Length; s++) materials[s] = GroundMaterial(kit.SubmeshLook(s).color);
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.isStatic = true;
            return go;
        }

        private static Material GroundMaterial(Color color)
        {
            string path = Folder + "/Materials/Ground_" + ColorUtility.ToHtmlStringRGB(color) + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.05f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        // ---------- The wreck, the sea, the old camp ----------

        private static Transform PlaceShip(Transform level)
        {
            var site = new GameObject("Crash Site").transform;
            site.SetParent(level, false);
            Vector2 forward = TutorialLevelBuilder.ShipForward;
            var ship = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CrashSiteWreckBuilder.ShipPath), site);
            ship.transform.SetPositionAndRotation(GroundAt(TutorialLevelBuilder.Ship) + Vector3.down * 0.4f, Quaternion.LookRotation(new Vector3(forward.x, 0f, forward.y)));
            Physics.SyncTransforms();
            // Still smouldering: smoke from the stripped engine bays.
            Transform hull = ship.transform.Find("Hull");
            if (hull != null)
            {
                Effect("ElectricalFire", site, hull.TransformPoint(new Vector3(0.4f, 2.2f, -2f)), 0.8f, "Reactor Bay Embers");
                Effect("SmokePlume", site, hull.TransformPoint(new Vector3(0f, 3f, -6f)), 1.3f, "Wreck Smoke");
            }
            return ship.transform;
        }

        private static void BuildShore(Transform level, List<Vector3> clear)
        {
            var shore = new GameObject("Shore").transform;
            shore.SetParent(level, false);
            IslandKit.BuildOcean(shore, SeaLevel, Vector2.zero, MapSize * 0.5f, 2f, Height, Vector3.zero, "WastelandOcean");
            Vector2 start = TutorialLevelBuilder.DockStart;
            IslandKit.BuildDock(shore, new IslandKit.DockSpec
            {
                Name = "WastelandDock", Start = start, Seaward = TutorialLevelBuilder.Camp.normalized, Length = 18f,
                Role = SignalLost.World.IslandDock.Role.Arrival, IslandName = "the Wasteland"
            }, SeaLevel, Height, 20261109);
            clear.Add(new Vector3(start.x, start.y, 10f));
        }

        // The old camp, knocked flat: the tent down, the fire cold, the logs thrown about, the stands smashed.
        private static void BuildRuinedCamp(Transform level, List<Vector3> clear)
        {
            Vector2 camp = TutorialLevelBuilder.Camp;
            var root = new GameObject("Ruined Camp").transform;
            root.SetParent(level, false);
            clear.Add(new Vector3(camp.x, camp.y, 9f));
            Place(MeadowAssetBuilder.PrefabPath("FirePit"), root, camp, Quaternion.identity, 1f);
            Place(MeadowAssetBuilder.PrefabPath("Tent"), root, camp + new Vector2(-4f, 6f), Quaternion.Euler(78f, 160f, 0f), 1f, -0.6f);
            Place(MeadowAssetBuilder.PrefabPath("LogSeat"), root, camp + new Vector2(4.5f, 1.5f), Quaternion.Euler(0f, 55f, 14f), 1f);
            Place(MeadowAssetBuilder.PrefabPath("LogSeat"), root, camp + new Vector2(-1.5f, -5f), Quaternion.Euler(0f, -30f, 0f), 1f);
            Place(CrashSiteWreckBuilder.DebrisPath("Crate"), root, camp + new Vector2(-2.8f, 3.9f), Quaternion.Euler(0f, 40f, 90f), 0.8f);
            Place(CrashSiteWreckBuilder.DebrisPath("Barrel"), root, camp + new Vector2(1.5f, 6f), Quaternion.Euler(90f, 20f, 0f), 0.8f);
            for (int i = 0; i < 3; i++) // the trophy stands, toppled
                Place(MeadowAssetBuilder.PrefabPath("LogSeat"), root, camp + new Vector2(1.2f + i * 1.6f, 5.4f - i * 0.6f), Quaternion.Euler(0f, i * 50f, 90f), 0.35f, 0.2f);
            Effect("SmokePlume", root, GroundAt(camp) + Vector3.up * 0.2f, 0.6f, "Cold Fire Smoke");
        }

        // ---------- The arena ----------

        private static Transform BuildArena(Transform level, Transform ship, System.Random random, out List<Vector3> hideSpots)
        {
            var arena = new GameObject("Arena").transform;
            arena.SetParent(level, false);
            arena.position = GroundAt(ArenaCentre);
            // Cover from the beam and the charges: boulder clusters and hull chunks at mid range.
            for (int i = 0; i < 8; i++)
            {
                float a = (i + 0.35f) * Mathf.PI * 2f / 8f;
                float r = i % 2 == 0 ? 19f : 27f;
                Vector2 at = ArenaCentre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (Vector2.Distance(at, MechHome) < 9f || Vector2.Distance(at, PlayerStart) < 8f) continue;
                if (i % 2 == 0)
                {
                    Prop(arena, MudModels.Kind.Boulder, at, Range(random, 0f, 360f), random, 1.1f);
                    Prop(arena, MudModels.Kind.Rock, at + new Vector2(2.6f, 1.2f), Range(random, 0f, 360f), random, 1f);
                }
                else
                {
                    Place(CrashSiteWreckBuilder.DebrisPath("HullChunk"), arena, at, Quaternion.Euler(Range(random, -10f, 10f), Range(random, 0f, 360f), Range(random, 60f, 80f)), 1.2f, -0.4f);
                    Place(CrashSiteWreckBuilder.DebrisPath("Strut"), arena, at + new Vector2(1.8f, -1.4f), Quaternion.Euler(0f, Range(random, 0f, 360f), 0f), 1f);
                }
            }
            // Hiding spots for the shield conduits, round the edge, each tucked into a heap of scrap.
            hideSpots = new List<Vector3>();
            string[] heap = { "HullPanel", "Crate", "Barrel", "Pipe", "CableBundle", "Gear", "BrokenWing" };
            for (int i = 0; i < 7; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / 7f + 0.2f;
                Vector2 at = ArenaCentre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Range(random, 31f, 36f);
                if (Vector2.Distance(at, TutorialLevelBuilder.Ship) < 14f) at = ArenaCentre + (at - ArenaCentre).normalized * 26f;
                Vector2 inward = (ArenaCentre - at).normalized;
                Vector2 side = new Vector2(-inward.y, inward.x);
                for (int k = 0; k < 3; k++)
                {
                    Vector2 p = at + inward * Range(random, 1.6f, 2.4f) + side * ((k - 1) * 1.8f);
                    Place(CrashSiteWreckBuilder.DebrisPath(heap[(i + k) % heap.Length]), arena, p, Quaternion.Euler(Range(random, -20f, 20f), Range(random, 0f, 360f), Range(random, -30f, 30f)), Range(random, 1f, 1.4f));
                }
                Vector3 spot = GroundAt(at);
                hideSpots.Add(new Vector3(spot.x, spot.y, spot.z));
            }
            return arena;
        }

        // ---------- Scenery ----------

        private static void BuildScenery(Transform level, List<Vector3> clear, System.Random random)
        {
            var scenery = new GameObject("Scenery").transform;
            scenery.SetParent(level, false);
            (MudModels.Kind kind, int count, float min, float max)[] spread =
            {
                (MudModels.Kind.DeadTree, 150, 0.8f, 1.35f), (MudModels.Kind.Stump, 70, 0.8f, 1.3f), (MudModels.Kind.Log, 30, 0.8f, 1.2f),
                (MudModels.Kind.Rock, 90, 0.7f, 1.6f), (MudModels.Kind.Boulder, 26, 0.8f, 1.4f)
            };
            foreach ((MudModels.Kind kind, int count, float min, float max) in spread)
                for (int n = 0, tries = 0; n < count && tries < count * 20; tries++)
                {
                    float angle = Range(random, 0f, Mathf.PI * 2f), r = Mathf.Sqrt(Range(random, 0.02f, 1f)) * (TutorialLevelBuilder.PlayRadius - 6f);
                    Vector2 p = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                    if (Blocked(p, clear) || Height(p.x, p.y) < SeaLevel + 1.5f) continue;
                    MudProp prop = Prop(scenery, kind, p, Range(random, 0f, 360f), random, Range(random, min, max));
                    if (kind == MudModels.Kind.DeadTree) prop.transform.rotation *= Quaternion.Euler(Range(random, -8f, 8f), 0f, Range(random, -8f, 8f)); // leaning
                    n++;
                }
            // Wreckage strewn from the crash, thickest around the wreck and the arena.
            for (int n = 0, tries = 0; n < 60 && tries < 1200; tries++)
            {
                Vector2 centre = n % 2 == 0 ? TutorialLevelBuilder.Ship : ArenaCentre;
                float angle = Range(random, 0f, Mathf.PI * 2f), r = Range(random, n % 2 == 0 ? 16f : ArenaRadius + 4f, n % 2 == 0 ? 60f : 80f);
                Vector2 p = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                if (p.magnitude > TutorialLevelBuilder.PlayRadius - 8f || Vector2.Distance(p, ArenaCentre) < ArenaRadius + 2f) continue;
                string piece = CrashSiteWreckBuilder.DebrisNames[random.Next(CrashSiteWreckBuilder.DebrisNames.Length)];
                Place(CrashSiteWreckBuilder.DebrisPath(piece), scenery, p, Quaternion.Euler(Range(random, -15f, 15f), Range(random, 0f, 360f), Range(random, -15f, 15f)), Range(random, 0.8f, 1.3f));
                n++;
            }
            // Smoke columns on the horizon: the island is still burning.
            for (int i = 0; i < 7; i++)
            {
                float angle = i * Mathf.PI * 2f / 7f + 0.4f;
                Vector2 p = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Range(random, 70f, 100f);
                if (Height(p.x, p.y) < SeaLevel + 1.5f) continue;
                Effect("SmokePlume", scenery, GroundAt(p), Range(random, 1.2f, 1.8f), "Distant Smoke");
                if (i % 2 == 0) Effect("ElectricalFire", scenery, GroundAt(p), 1f, "Burning Wreckage");
            }
        }

        private static bool Blocked(Vector2 p, List<Vector3> clear)
        {
            foreach (Vector3 c in clear)
                if (Vector2.Distance(p, new Vector2(c.x, c.y)) < c.z) return true;
            return false;
        }

        private static MudProp Prop(Transform parent, MudModels.Kind kind, Vector2 at, float yaw, System.Random random, float size)
        {
            var go = new GameObject(kind.ToString());
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(GroundAt(at), Quaternion.Euler(0f, yaw, 0f));
            var prop = go.AddComponent<MudProp>();
            prop.Configure(kind, random.Next(4), size);
            foreach ((Vector3 centre, Vector3 boxSize) in MudModels.Colliders(kind, size))
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = centre;
                box.size = boxSize;
            }
            go.isStatic = true;
            return prop;
        }

        private static GameObject Place(string path, Transform parent, Vector2 at, Quaternion rotation, float scale, float sink = 0f)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new InvalidOperationException("Missing prefab " + path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.SetPositionAndRotation(GroundAt(at) + Vector3.up * sink, rotation);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        private static void Effect(string name, Transform parent, Vector3 at, float scale, string label)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(MeadowAssetBuilder.EffectPath(name));
            if (asset == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.name = label;
            go.transform.position = at;
            go.transform.localScale = Vector3.one * scale;
        }

        private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        // ---------- Navigation, light, grass ----------

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

        // A low, dusty sun through smoke: warm light, brown haze, a washed-out sky.
        private static void BuildLighting()
        {
            var sun = new GameObject("Smoky Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.74f, 0.52f);
            sun.intensity = 1.05f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(26f, 58f, 0f);
            RenderSettings.sun = sun;
            string skyPath = Folder + "/Materials/WastelandSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            sky.SetFloat("_SunSize", 0.06f);
            sky.SetFloat("_AtmosphereThickness", 1.9f);
            sky.SetColor("_SkyTint", new Color(0.62f, 0.47f, 0.37f));
            sky.SetColor("_GroundColor", new Color(0.3f, 0.24f, 0.2f));
            sky.SetFloat("_Exposure", 1.05f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.55f, 0.5f);
            RenderSettings.ambientEquatorColor = new Color(0.48f, 0.4f, 0.34f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.19f, 0.17f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.58f, 0.47f, 0.38f);
            RenderSettings.fogStartDistance = 35f;
            RenderSettings.fogEndDistance = 230f;
        }

        // Sparse dry grass, none in the arena.
        private static void BuildFoliage(Transform level, GameObject ground, List<Vector3> clear)
        {
            var foliage = new GameObject("Dry Grass");
            foliage.transform.SetParent(level, false);
            foreach (Vector3 c in clear)
            {
                var marker = new GameObject("Clear");
                marker.transform.SetParent(foliage.transform, false);
                marker.transform.position = new Vector3(c.x, 0f, c.y);
                var sphere = marker.AddComponent<SphereCollider>();
                sphere.radius = c.z;
                sphere.enabled = false; // a marker only
            }
            var scatter = foliage.AddComponent<FoliageScatter>();
            var data = new SerializedObject(scatter);
            data.FindProperty("ground").objectReferenceValue = ground.GetComponent<Collider>();
            (string name, int count, Vector2 scale)[] items = { ("GrassTuftDry", 2600, new Vector2(0.8f, 1.5f)) };
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
            data.FindProperty("radius").floatValue = TutorialLevelBuilder.PlayRadius - 6f;
            data.FindProperty("seed").intValue = 1109;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Gameplay: the player, the mech, the encounter ----------

        private static void BuildGameplay(Transform arena, List<Vector3> hideSpots)
        {
            var gameplay = new GameObject("Gameplay").transform;
            Vector3 spawnAt = GroundAt(PlayerStart);
            Quaternion facing = Quaternion.LookRotation(new Vector3(ArenaCentre.x - PlayerStart.x, 0f, ArenaCentre.y - PlayerStart.y));
            var spawn = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CorePrefabBuilder.SpawnPath));
            spawn.transform.SetParent(gameplay, false);
            spawn.transform.SetPositionAndRotation(spawnAt, facing);
            spawn.name = "Spawn At Arena Edge";
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
            scanner.FindProperty("levelRangeOverride").floatValue = 130f;
            scanner.ApplyModifiedPropertiesWithoutUndo();
            player.AddComponent<FinaleLoadout>();
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CorePrefabBuilder.SystemsPath));

            // The mech, kneeling by the wreck, facing the arena.
            var mech = new GameObject("Cavendish's Mech");
            mech.transform.SetParent(gameplay, false);
            Vector2 look = ArenaCentre - MechHome;
            mech.transform.SetPositionAndRotation(GroundAt(MechHome), Quaternion.LookRotation(new Vector3(look.x, 0f, look.y)));
            var health = mech.AddComponent<Health>();
            var hp = new SerializedObject(health);
            hp.FindProperty("maxHealth").floatValue = 4500f;
            hp.ApplyModifiedPropertiesWithoutUndo();
            var agent = mech.AddComponent<NavMeshAgent>();
            agent.radius = 3f;
            agent.height = 12f;
            agent.speed = 3.4f;
            agent.acceleration = 8f;
            agent.angularSpeed = 0f;
            agent.stoppingDistance = 1f;
            agent.enabled = false;
            var boss = mech.AddComponent<CavendishMech>();
            var data = new SerializedObject(boss);
            data.FindProperty("pilotPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(CavendishPath);
            data.ApplyModifiedPropertiesWithoutUndo();

            // The conduits: three for phase 1 every 120 degrees or so, four for the phase 2 placeholder.
            var conduitRoot = new GameObject("Shield Conduits").transform;
            conduitRoot.SetParent(gameplay, false);
            var phaseOne = new List<ShieldConduit>();
            var phaseTwo = new List<ShieldConduit>();
            int[] firstSet = { 0, 2, 5 };
            for (int i = 0; i < hideSpots.Count; i++)
            {
                var go = new GameObject("Conduit " + (i + 1));
                go.transform.SetParent(conduitRoot, false);
                go.transform.SetPositionAndRotation(hideSpots[i], Quaternion.Euler(0f, i * 51f, 0f));
                go.AddComponent<Health>();
                go.AddComponent<QuestSignalSource>().enabled = false;
                ShieldConduit conduit = go.AddComponent<ShieldConduit>();
                (Array.IndexOf(firstSet, i) >= 0 ? phaseOne : phaseTwo).Add(conduit);
            }

            // The encounter: the arena's fight zone, the adds, Eugene's crew.
            var encounter = new GameObject("Finale Encounter");
            encounter.transform.SetParent(gameplay, false);
            encounter.transform.position = GroundAt(ArenaCentre);
            var zone = encounter.AddComponent<CombatZone>();
            zone.Radius = ArenaRadius + 4f;
            var fight = encounter.AddComponent<FinaleEncounter>();
            var e = new SerializedObject(fight);
            e.FindProperty("mech").objectReferenceValue = boss;
            e.FindProperty("arena").objectReferenceValue = zone;
            Fill(e.FindProperty("conduits"), phaseOne.ToArray());
            Fill(e.FindProperty("phaseTwoConduits"), phaseTwo.ToArray());
            var adds = new List<Object>();
            foreach (string path in AddPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) adds.Add(prefab);
            }
            Fill(e.FindProperty("addPrefabs"), adds.ToArray());
            var crew = new List<Object> { AssetDatabase.LoadAssetAtPath<GameObject>(EugenePath) };
            foreach (string path in CousinPaths) crew.Add(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            Fill(e.FindProperty("crewPrefabs"), crew.ToArray());
            e.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Fill(SerializedProperty list, Object[] values)
        {
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
