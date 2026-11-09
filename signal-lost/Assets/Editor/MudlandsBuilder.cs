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

        private const float MapSize = 400f;
        private const int Cells = 100;
        private const float PlayRadius = 178f;
        public const float WaterY = -0.55f;

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
            // The Gator Causeway: out of the lot's west side to the County Line gate.
            new[] { Lot + new Vector2(-22f, 6f), new Vector2(-84f, 134f), new Vector2(-120f, 112f), new Vector2(-144f, 80f), new Vector2(-152f, 44f), new Vector2(-156f, 18f) }
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

        private enum Ground { Grass, DarkGrass, Moss, Road, Mud, Bed, Rock, Cliff }

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
            EnsureFolder(Folder + "/Materials");
            EnsureFolder(Folder + "/Meshes");
            EnsureFolder(EnemyFolder);
            MakeTemplateMaterials();
            Dictionary<Type, GameObject> creatures = BuildCreaturePrefabs();
            BuildTrophy();
            MudlandsUpgradeIcons.MergeIntoCatalog();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var random = new System.Random(20261108);
            var level = new GameObject("Level").transform;
            GameObject ground = BuildTerrain(level);
            BuildWater(level);
            Physics.SyncTransforms();

            var clear = new List<Vector3>();
            Transform camp = BuildCamp(level, clear);
            var world = new World { Level = level, Random = random, Clear = clear };
            BuildSolidProps(world);
            BuildScenery(world);
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
            public RoadhouseGate Gate;
            public Transform Ramp;
            public Transform[] Pumps, Doors, Route;
            public Transform Gameplay;
        }

        // ---------- Height ----------

        private static float Noise(float x, float z, float scale, float seed = 0f) => (Mathf.PerlinNoise(x / scale + 300f + seed, z / scale + 71f + seed) - 0.5f) * 2f;

        private static float RoadLevel(float x, float z) => 0.45f + 0.5f * Noise(x, z, 60f);

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

            // The creek and the bog are carved out after the roads, so bridges span the gaps.
            float creek = DistanceToPath(p, Creek);
            if (creek < 7f) h = Mathf.Min(h, Mathf.Lerp(-1.3f, h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3.5f, 7f, creek))));
            float bog = Ellipse(p, Bog, BogRadii);
            if (bog < 1.35f) h = Mathf.Min(h, Mathf.Lerp(-1.3f, h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1.35f, bog))));
            float shallows = Vector2.Distance(p, WitchClearing + new Vector2(0f, -9f));
            if (shallows < 9f) h = Mathf.Min(h, Mathf.Lerp(-0.9f, h, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(5f, 9f, shallows))));

            // The ridge ring: tall and too steep to climb.
            float r = p.magnitude;
            if (r > PlayRadius) h += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PlayRadius, PlayRadius + 12f, r)) * 22f + Mathf.Max(0f, Noise(x, z, 9f, 3f)) * 4f;
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

        public static Vector3 GroundAt(Vector2 p) => new Vector3(p.x, Height(p.x, p.y), p.y);

        private static Ground Surface(Vector3 centre, Vector3 normal)
        {
            var p = new Vector2(centre.x, centre.z);
            if (normal.y < 0.6f) return Ground.Cliff;
            if (normal.y < 0.8f) return Ground.Rock;
            if (centre.y < WaterY - 0.2f) return Ground.Bed;
            if (DistanceToRoads(p) < 3.4f + Noise(p.x, p.y, 3f) * 0.6f) return Ground.Road;
            float e = Ellipse(p, Speedway, new Vector2(22f, 13f));
            if (e > 0.78f && e < 1.22f) return Ground.Road; // the go-kart track
            if (Vector2.Distance(p, Lot) < 30f || Vector2.Distance(p, Camp) < 9f) return Ground.Road;
            foreach ((Vector2 c, Vector2 radii) in MudPatches) if (Ellipse(p, c, radii) < 1.05f) return Ground.Mud;
            if (centre.y < WaterY + 0.35f) return Ground.Mud; // muddy shores
            if (p.magnitude > PlayRadius - 6f) return Ground.DarkGrass;
            float patch = Noise(p.x, p.y, 18f, 23f);
            return patch > 0.3f ? Ground.Moss : patch < -0.25f ? Ground.DarkGrass : Ground.Grass;
        }

        private static Color GroundColor(Ground ground) => ground switch
        {
            Ground.Grass => new Color(0.27f, 0.4f, 0.22f),
            Ground.DarkGrass => new Color(0.18f, 0.3f, 0.2f),
            Ground.Moss => new Color(0.34f, 0.44f, 0.24f),
            Ground.Road => new Color(0.45f, 0.34f, 0.22f),
            Ground.Mud => new Color(0.24f, 0.17f, 0.11f),
            Ground.Bed => new Color(0.16f, 0.13f, 0.1f),
            Ground.Rock => new Color(0.38f, 0.4f, 0.38f),
            _ => new Color(0.3f, 0.31f, 0.3f)
        };

        // ---------- Terrain and water ----------

        private static GameObject BuildTerrain(Transform level)
        {
            float step = MapSize / Cells, origin = -MapSize * 0.5f;
            var heights = new float[Cells + 1, Cells + 1];
            for (int i = 0; i <= Cells; i++)
                for (int j = 0; j <= Cells; j++)
                    heights[i, j] = Height(origin + i * step, origin + j * step);
            var kit = new MeshKit();
            for (int i = 0; i < Cells; i++)
                for (int j = 0; j < Cells; j++)
                {
                    var a = new Vector3(origin + i * step, heights[i, j], origin + j * step);
                    var b = new Vector3(origin + (i + 1) * step, heights[i + 1, j], origin + j * step);
                    var c = new Vector3(origin + (i + 1) * step, heights[i + 1, j + 1], origin + (j + 1) * step);
                    var d = new Vector3(origin + i * step, heights[i, j + 1], origin + (j + 1) * step);
                    bool flip = (i + j) % 2 == 0;
                    Facet(kit, a, flip ? d : b, c);
                    Facet(kit, a, c, flip ? b : d);
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
            kit.Tri(a, b, c, GroundColor(Surface((a + b + c) / 3f, normal)));
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
                material.SetColor("_DeepColor", new Color(0.05f, 0.16f, 0.15f, 0.95f));
                material.SetColor("_ShallowColor", new Color(0.18f, 0.3f, 0.24f, 0.8f));
                material.SetColor("_HorizonColor", new Color(0.32f, 0.45f, 0.45f, 1f));
                // Still, murky swamp water: barely any swell, few sparkles.
                material.SetFloat("_WaveHeight", 0.015f);
                material.SetFloat("_WaveScale", 0.18f);
                material.SetFloat("_WaveSpeed", 0.35f);
                material.SetFloat("_Sparkle", 0.1f);
                material.SetFloat("_Smoothness", 0.2f); // low: the moon sits in front of most views, and a glossy surface turns into a white sheet
                material.SetFloat("_FoamWidth", 0.05f);
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

        // ---------- The camp ----------

        private static Transform BuildCamp(Transform level, List<Vector3> clear)
        {
            Transform camp = Instance(BaseCampBuilder.PrefabPath, level).transform;
            camp.position = GroundAt(Camp);
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

        private static void AddColliders(GameObject go, MudModels.Kind kind, float size)
        {
            foreach ((Vector3 centre, Vector3 boxSize) in MudModels.Colliders(kind, size))
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = centre;
                box.size = boxSize;
            }
        }

        private static float Yaw(Vector2 from, Vector2 to) => Mathf.Atan2(to.x - from.x, to.y - from.y) * Mathf.Rad2Deg;

        private static void BuildSolidProps(World world)
        {
            var props = new GameObject("Buildings").transform;
            props.SetParent(world.Level, false);

            // Camp: boardwalk north to the road, torches.
            for (int i = 0; i < 2; i++) Prop(world, MudModels.Kind.Boardwalk, Camp + new Vector2(0f, 10f + i * 6f), 0f, "Camp Boardwalk", parent: props, yOffset: 0.2f);
            foreach (Vector2 t in new[] { new Vector2(-5f, 9f), new Vector2(5f, 9f), new Vector2(-9f, -2f), new Vector2(9f, -3f) })
                Prop(world, MudModels.Kind.TikiTorch, Camp + t, 0f, "Camp Torch", parent: props);
            Prop(world, MudModels.Kind.StiltShack, Camp + new Vector2(-13f, 6f), 120f, "Camp Shack", parent: props);
            Prop(world, MudModels.Kind.RoadSign, Camp + new Vector2(3f, 22f), 180f, "Sign", sign: "MUDLANDS  pop. ???", parent: props);

            // Gumbo Flats: an old windmill, fence lines (breakable, below), hay.
            Prop(world, MudModels.Kind.Windmill, Flats + new Vector2(28f, 10f), 30f, "Broken Windmill", parent: props);
            Prop(world, MudModels.Kind.Cabin, Flats + new Vector2(-30f, 14f), 160f, "Flats Cabin", parent: props);

            // Rotwood: stilt shacks, the sawmill, the driving school.
            Prop(world, MudModels.Kind.DrivingSchool, School, Yaw(School, Rotwood), "Bayou Driving School", sign: "BAYOU DRIVING SCHOOL", parent: props);
            foreach ((Vector2 at, float yaw) in new[] { (Rotwood + new Vector2(-12f, 10f), 140f), (Rotwood + new Vector2(10f, 14f), 200f), (Rotwood + new Vector2(-18f, -8f), 80f), (Rotwood + new Vector2(14f, -14f), 300f) })
                Prop(world, MudModels.Kind.StiltShack, at, yaw, "Rotwood Shack", parent: props);
            Prop(world, MudModels.Kind.Sawmill, Rotwood + new Vector2(-6f, -22f), 20f, "Sawmill", parent: props);
            Prop(world, MudModels.Kind.RoadSign, Rotwood + new Vector2(6f, -4f), 230f, "Sign", sign: "HAUL ROAD", parent: props);
            Prop(world, MudModels.Kind.RoadSign, HaulBarricades[0] + new Vector2(4f, 2f), 200f, "Sign", sign: "PROPERTY OF KART KONG RACEWAY", parent: props);

            // Bridges over the creek, on the roads (one per crossing, even where a crossing lands on a bend).
            var bridges = new List<Vector2>();
            foreach (Vector2[] road in Roads)
                for (int i = 0; i < road.Length - 1; i++)
                    for (int c = 0; c < Creek.Length - 1; c++)
                        if (SegmentsCross(road[i], road[i + 1], Creek[c], Creek[c + 1], out Vector2 hit) && bridges.TrueForAll(b => Vector2.Distance(b, hit) > 8f) && Add(bridges, hit))
                            Prop(world, MudModels.Kind.Bridge, hit, Yaw(road[i], road[i + 1]), "Bridge", 1.4f, parent: props, yOffset: Mathf.Max(0f, RoadLevel(hit.x, hit.y) - Height(hit.x, hit.y)) + 0.05f);

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

            // The Greasy Spoon: the roadhouse, gas pumps, a ramp, the gate, the county line.
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
            Prop(world, MudModels.Kind.CountyLine, Roads[8][Roads[8].Length - 1], Yaw(Roads[8][Roads[8].Length - 2], Roads[8][Roads[8].Length - 1]), "County Line", sign: "COUNTY LINE", parent: props);
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
                    Vector2.Distance(p, Dojo) < 18f || Vector2.Distance(p, Camp) < 14f) return true;
                foreach (Vector3 c in world.Clear) if (Vector2.Distance(p, new Vector2(c.x, c.y)) < c.z + margin) return true;
                return false;
            }

            void Tree(MudModels.Kind kind, Vector2 p, float scale)
            {
                Vector3 at = GroundAt(p) + Vector3.down * 0.2f;
                scenery.Place(kind, random.Next(4), at, R(0f, 360f), scale);
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
            for (int i = 0; i < 700; i++)
            {
                Vector2 p = Random(random, PlayRadius - 4f);
                float h = Height(p.x, p.y);
                if (DistanceToRoads(p) < 4.5f) continue;
                if (h < WaterY - 0.25f)
                {
                    if (random.NextDouble() < 0.5f) scenery.Place(MudModels.Kind.LilyPads, random.Next(4), new Vector3(p.x, WaterY, p.y), R(0f, 360f), R(0.8f, 1.4f));
                }
                else if (h < WaterY + 0.6f) scenery.Place(MudModels.Kind.Cattails, random.Next(4), GroundAt(p), R(0f, 360f), R(0.8f, 1.3f));
                else if (random.NextDouble() < 0.2f && !Blocked(p, -3f)) scenery.Place(MudModels.Kind.Mushrooms, random.Next(4), GroundAt(p), R(0f, 360f), R(0.8f, 1.6f));
                else if (random.NextDouble() < 0.25f && !Blocked(p, -3f)) scenery.Place(MudModels.Kind.Reeds, random.Next(4), GroundAt(p), R(0f, 360f), R(0.8f, 1.3f));
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
            kiosk.gameObject.AddComponent<QuestSignalSource>().enabled = false;
            world.Barricades = HaulBarricades.Select((b, i) =>
            {
                Vector2 dir = i + 1 < HaulBarricades.Length ? HaulBarricades[i + 1] - b : b - HaulBarricades[i - 1];
                MudProp prop = Prop(world, MudModels.Kind.Barricade, b, Yaw(Vector2.zero, dir), "Haul Road Barricade", sign: "ROAD CLOSED", parent: gameplay);
                return Breakable(prop.gameObject, Destructible.Substance.Wood, 45f, false, true);
            }).ToArray();
            MudProp still = Prop(world, MudModels.Kind.MoonshineStill, HaulEnd, Yaw(HaulEnd, Rotwood), "Moonshine Still", parent: gameplay);
            world.Still = Breakable(still.gameObject, Destructible.Substance.Metal, 140f, true, true, 6f, 40f);

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
            foreach (Vector2 g in GongSpots)
            {
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
                gong.Configure(guards, prop.Moving);
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
            world.Dam = Breakable(dam.gameObject, Destructible.Substance.Wood, 120f, false, true);
            for (int i = 0; i < 4; i++)
                Breakable(Prop(world, MudModels.Kind.MoonshineBarrel, damAt + new Vector2(-3f + i * 1.1f, -3.2f + (i % 2) * 0.6f), 0f, "Dam Barrel", parent: gameplay).gameObject,
                    Destructible.Substance.Metal, 10f, true, false, 5f, 45f);
            var flood = new GameObject("Chapel Flood");
            flood.transform.SetParent(gameplay, false);
            flood.transform.position = new Vector3(Chapel.x, RoadLevel(Chapel.x, Chapel.y) + 0.5f, Chapel.y);
            AddWaterDisc(flood.transform, "Flood Water", flood.transform.position, new Vector2(14f, 10f), WaterMaterial());
            AddZone(flood.transform, "Flood Zone", flood.transform.position, new Vector2(12f, 8.5f), MudZone.Kind.BogWater, 0.7f);
            world.Flood = flood;

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
            return gameplay;
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
            Spawn(creatures, typeof(RoadblockTurtle), enemies, HaulBarricades[1] + (HaulBarricades[2] - HaulBarricades[1]).normalized * 4f, Yaw(HaulBarricades[2], HaulBarricades[1]), haul);
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
            neon.position = GroundAt(Lot + new Vector2(0f, 24f)) + new Vector3(0f, 6.6f, -3.6f);
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
            story.Configure(cavendish, gameplay.GetComponentInChildren<MerchantCrab>(true), world.Kiosk, world.Barricades, world.Still, kong,
                world.Gongs, world.Dam, world.Flood, otter, world.Lanterns, witch, world.Gate, clutch);
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
            EditorUtility.CopySerialized(mesh, existing);
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
