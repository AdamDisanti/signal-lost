using System;
using System.Collections.Generic;
using System.Linq;
using SignalLost.Core;
using SignalLost.Effects;
using SignalLost.Enemies;
using SignalLost.Inventory;
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
using CrystalColor = SignalLost.Editor.CrystalAssetBuilder.CrystalColor;
using Object = UnityEngine.Object;
using Random = System.Random;
using T = SignalLost.Editor.CrystalCavesTerrain;

namespace SignalLost.Editor
{
    /// <summary>
    /// Creates the Crystal Caves level (design map 11-08; see docs/crystal-caves.md). Like Desert Dunes, it is two parts:
    ///  * CrystalCavesEnvironment.prefab (Prefabs/Enviornment objects/CrystalCaves): the island's ground and cave roof, the
    ///    ocean, the coast (cliff columns, rocks, plants, the shared Dock and Boat), the cave (columns, stalagmites,
    ///    stalactites, boulders, glowing crystal batches) and the CaveLighting that darkens it. Rebuilding it from this
    ///    script (same seed, same layout) replaces edits made to the prefab.
    ///  * Scenes/Large Areas/CrystalCaves.unity, created once and then edited by hand: the coastal lighting, the shared
    ///    Player and GameSystems (HUD, menus, pause), the spawn on the dock, the base camp with Cavendish and Eugene, the
    ///    baked NavMesh, and empty fight-zone markers for enemies to come.
    /// The landform itself is in <see cref="CrystalCavesTerrain"/>; the art kit in <see cref="CrystalAssetBuilder"/>.
    /// </summary>
    internal static class CrystalCavesBuilder
    {
        public const string ScenePath = "Assets/Scenes/Large Areas/CrystalCaves.unity";
        private const string NavMeshPath = "Assets/Scenes/Large Areas/CrystalCavesNavMesh.asset";
        public const string EnvironmentPrefabPath = CrystalAssetBuilder.Folder + "/CrystalCavesEnvironment.prefab";
        private const string MeshFolder = CrystalAssetBuilder.Folder + "/Meshes";
        private const string CavendishPath = "Assets/Prefabs/Enviornment objects/GrayboxProps/CapybaraAstronaut.prefab";
        private const string MerchantPath = "Assets/Prefabs/NPC/MerchantCrab.prefab";
        private const float SitDrop = 0.55f; // how far Cavendish's unrigged model sinks to sit on his log (as in the tutorial)
        private const float NearMouth = 70f; // crystal lights this close to the mouth stay on when you are outside

        // Crystal colours by area: each batch takes one of its nearest area's colours, sometimes any colour at all.
        private static readonly (Vector2 at, CrystalColor[] colours)[] Palette =
        {
            (new Vector2(110f, -100f), new[] { CrystalColor.Cyan, CrystalColor.Cyan, CrystalColor.DarkBlue }),
            (new Vector2(-20f, -110f), new[] { CrystalColor.Cyan, CrystalColor.Green, CrystalColor.DarkBlue }),
            (T.BeetleZone, new[] { CrystalColor.Purple, CrystalColor.Pink, CrystalColor.DarkBlue }),
            (new Vector2(-150f, 110f), new[] { CrystalColor.DarkBlue, CrystalColor.Purple, CrystalColor.Cyan }),
            (new Vector2(165f, 40f), new[] { CrystalColor.Green, CrystalColor.Cyan, CrystalColor.Orange }),
            (new Vector2(0f, 60f), new[] { CrystalColor.Green, CrystalColor.Cyan, CrystalColor.Orange }),
            (T.MiniBossZone, new[] { CrystalColor.Orange, CrystalColor.Pink, CrystalColor.Orange }),
            (T.FinalBossZone, new[] { CrystalColor.Purple, CrystalColor.DarkBlue, CrystalColor.Pink })
        };

        [MenuItem("Signal Lost/Crystal Caves/Create Crystal Caves Level")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("The Crystal Caves scene already exists. Edit it in Unity (delete it first to start over).");
            foreach (string path in new[] { CorePrefabBuilder.PlayerPath, CorePrefabBuilder.SystemsPath, CorePrefabBuilder.SpawnPath, BaseCampBuilder.PrefabPath, CavendishPath, MerchantPath })
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) throw new InvalidOperationException("Missing " + path + ". Is your branch up to date with main?");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject environment = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPrefabPath) ?? BuildEnvironmentPrefab();
            T.Generate();
            PrefabUtility.InstantiatePrefab(environment);
            BuildLighting();
            Transform camp = Place(BaseCampBuilder.PrefabPath, null, Ground(T.Camp), Quaternion.identity, Vector3.one).transform;
            camp.name = "Campsite";
            BakeNavigation(); // before anyone who walks is placed, so they never punch holes in it
            BuildGameplay();
            BuildResidents(camp);
            BuildMarkers();

            EditorSceneManager.SaveScene(scene, ScenePath);
            CoreSceneSetup.SetBuildScenes();
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("CRYSTAL_CAVES_CREATED_AND_VALIDATED");
        }

        [MenuItem("Signal Lost/Crystal Caves/Rebuild Crystal Caves Environment")]
        public static void RebuildEnvironment()
        {
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Rebuild Crystal Caves environment",
                    "This regenerates CrystalCavesEnvironment.prefab, its meshes, crystals and materials from the builder scripts, replacing edits " +
                    "made to them in Unity. The scene keeps its instance; re-bake its NavMesh afterwards (select Navigation, press Bake). Continue?", "Rebuild", "Cancel"))
                return;
            CrystalAssetBuilder.Build();
            DockBoatBuilder.Build();
            BuildEnvironmentPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("CRYSTAL_CAVES_ENVIRONMENT_REBUILT");
        }

        /// <summary>Batch entry: creates the level (and its art) and validates it.</summary>
        public static void CreateBatch()
        {
            try
            {
                Create();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        // =====================================================================
        // Environment prefab
        // =====================================================================

        private static GameObject BuildEnvironmentPrefab()
        {
            // The art kits, if they are missing (checked by the last piece each builder makes).
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CrystalAssetBuilder.RockPath("CoastBlock")) == null) CrystalAssetBuilder.Build();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DockBoatBuilder.BoatPath) == null) DockBoatBuilder.Build();
            T.Generate();
            List<string> problems = T.Check();
            if (problems.Count > 0) throw new InvalidOperationException("Crystal Caves landform check failed:\n" + string.Join("\n", problems));

            var rng = new Random(20261108);
            var root = new GameObject("Crystal Caves Environment");
            Mesh groundMesh = T.BuildGroundMesh(out T.Surface[] groundSurfaces);
            MeshCollider ground = Solid("Ground", root.transform, SaveMesh(groundMesh), groundSurfaces);
            Mesh roofMesh = T.BuildRoofMesh(out T.Surface[] roofSurfaces);
            MeshCollider roof = Solid("Cave Roof", root.transform, SaveMesh(roofMesh), roofSurfaces);
            roof.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true; // the mountain top is out of reach

            var ocean = new GameObject("Ocean");
            ocean.transform.SetParent(root.transform, false);
            ocean.transform.position = new Vector3(0f, T.SeaLevel, 0f);
            ocean.AddComponent<MeshFilter>().sharedMesh = SaveMesh(T.BuildOceanMesh());
            var water = ocean.AddComponent<MeshRenderer>();
            water.sharedMaterial = OceanMaterial();
            water.shadowCastingMode = ShadowCastingMode.Off;
            water.receiveShadows = false;
            Physics.SyncTransforms();

            BuildCoast(Group("Coast", root.transform), rng);
            Transform cave = Group("Cave", root.transform);
            var mouth = new GameObject("Cave Mouth").transform;
            mouth.SetParent(cave, false);
            Vector2 portal = T.MouthPath[1];
            mouth.SetPositionAndRotation(Ground(portal) + Vector3.up * 2f, Quaternion.LookRotation(Vector3.back));
            BuildCaveRock(cave, rng);
            Transform crystals = BuildCrystals(cave, ground, roof, rng);

            var lighting = new GameObject("Cave Lighting");
            lighting.transform.SetParent(root.transform, false);
            var rig = new SerializedObject(lighting.AddComponent<CaveLighting>());
            rig.FindProperty("roof").objectReferenceValue = roof;
            rig.FindProperty("entrance").objectReferenceValue = mouth;
            rig.FindProperty("crystals").objectReferenceValue = crystals;
            rig.FindProperty("alwaysLitRadius").floatValue = NearMouth;
            rig.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, EnvironmentPrefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static MeshCollider Solid(string name, Transform parent, Mesh mesh, T.Surface[] surfaces)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = surfaces.Select(SurfaceMaterial).ToArray();
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return collider;
        }

        // ---------- The coast ----------

        private static void BuildCoast(Transform coast, Random rng)
        {
            // The dock hangs off the ledge's south cliff; the boat is moored at its jetty.
            Vector3 deck = new Vector3(T.DockRoot.x, DockBoatBuilder.DeckHeight, T.DockRoot.y);
            Transform dock = Place(DockBoatBuilder.DockPath, coast, deck, Quaternion.Euler(0f, 180f, 0f), Vector3.one).transform;
            Vector3 mooring = dock.TransformPoint(DockBoatBuilder.BoatMooring);
            Place(DockBoatBuilder.BoatPath, coast, new Vector3(mooring.x, T.SeaLevel, mooring.z), Quaternion.Euler(0f, 183f, 0f), Vector3.one);

            // Where the shelf meets the sea, found by walking out from the mountains at each angle along the south coast.
            var edge = new List<(Vector2 point, Vector2 outward)>();
            for (float angle = 212f; angle <= 363f; angle += 1.2f)
            {
                var outward = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                for (float r = 200f; r < 300f; r += 0.5f)
                {
                    Vector2 p = T.CaveCentre + outward * r;
                    T.Coast(p.x, p.y, out float land, out _);
                    if (land >= 0.5f) continue;
                    edge.Add((p, outward));
                    break;
                }
            }

            // Stacked rock columns lining the sea cliffs, and tumbled blocks in the surf, like the reference coast.
            Transform columns = Group("Cliff Columns", coast);
            foreach ((Vector2 point, Vector2 outward) in edge)
            {
                if (Vector2.Distance(point, T.DockRoot) < 14f) continue; // keep the dock's cliff clear
                Vector2 inland = point - outward * 4f;
                float top = T.GroundAt(inland.x, inland.y);
                int count = rng.NextDouble() < 0.5 ? 1 : 2;
                for (int i = 0; i < count; i++)
                {
                    Vector2 tangent = new Vector2(-outward.y, outward.x);
                    Vector2 p = point + outward * Range(rng, -1.5f, 2.5f) + tangent * Range(rng, -1.5f, 1.5f);
                    float radius = Range(rng, 1.6f, 3f), height = top + Range(rng, -3.5f, 1.2f) + 9f;
                    Place(CrystalAssetBuilder.RockPath("CoastColumn"), columns, new Vector3(p.x, -9f, p.y), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), new Vector3(radius, height, radius));
                }
            }
            Transform surf = Group("Surf Blocks", coast);
            for (int n = 0; n < 110; n++)
            {
                (Vector2 point, Vector2 outward) = edge[rng.Next(edge.Count)];
                Vector2 p = point + outward * Range(rng, 2f, 12f) + new Vector2(-outward.y, outward.x) * Range(rng, -3f, 3f);
                if (Vector2.Distance(p, T.DockRoot + new Vector2(0f, -20f)) < 22f) continue; // keep the water by the dock open
                float size = Range(rng, 1.4f, 3.8f);
                Place(CrystalAssetBuilder.RockPath("CoastBlock"), surf, new Vector3(p.x, Range(rng, -2.5f, -0.2f), p.y), RandomRotation(rng),
                    new Vector3(size * Range(rng, 0.8f, 1.3f), size * Range(rng, 0.7f, 1.3f), size * Range(rng, 0.8f, 1.3f)));
            }

            // Rock formations, boulders, wind-bent pines, bushes and grass on the shelf; none of them on the camp, the
            // dock, the mouth or the line of sight from the dock to the mouth.
            Transform formations = Group("Rock Formations", coast);
            for (int n = 0; n < 16; n++)
            {
                if (!ShelfPoint(rng, 5f, true, out Vector2 centre)) continue;
                int count = rng.Next(3, 7);
                for (int i = 0; i < count; i++)
                {
                    Vector2 p = centre + RandomDisc(rng, 3.5f);
                    float radius = Range(rng, 0.8f, 1.8f);
                    Place(CrystalAssetBuilder.RockPath("CoastColumn"), formations, Ground(p) + Vector3.down * 1f, Quaternion.Euler(Range(rng, -4f, 4f), Range(rng, 0f, 360f), Range(rng, -4f, 4f)),
                        new Vector3(radius, Range(rng, 2f, 7f), radius));
                }
            }
            Transform rocks = Group("Rocks", coast);
            for (int n = 0; n < 60; n++)
            {
                if (!ShelfPoint(rng, 2f, true, out Vector2 p)) continue;
                bool big = rng.NextDouble() < 0.25;
                string kind = big ? MeadowAssetBuilder.Rocks[3 + rng.Next(2)] : MeadowAssetBuilder.Rocks[rng.Next(3)];
                Place(MeadowAssetBuilder.PrefabPath(kind), rocks, Ground(p) + Vector3.down * (big ? 0.4f : 0.1f), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), Vector3.one * Range(rng, 0.8f, big ? 1.5f : 2f));
            }
            Transform pines = Group("Pines", coast);
            for (int n = 0, tries = 0; n < 20 && tries < 4000; tries++)
            {
                if (!ShelfPoint(rng, 2f, true, out Vector2 p)) continue;
                T.Coast(p.x, p.y, out _, out float mountain);
                if (mountain < 0.05f && rng.NextDouble() > 0.15) continue; // mostly along the mountains' foot
                Place(MeadowAssetBuilder.PrefabPath(MeadowAssetBuilder.Trees[rng.Next(3)]), pines, Ground(p) + Vector3.down * 0.15f, Quaternion.Euler(Range(rng, -5f, 5f), Range(rng, 0f, 360f), 0f), Vector3.one * Range(rng, 0.8f, 1.2f));
                n++;
            }
            Transform plants = Group("Bushes And Grass", coast);
            for (int n = 0; n < 40; n++)
                if (ShelfPoint(rng, 1f, false, out Vector2 p))
                    Place(MeadowAssetBuilder.PrefabPath(MeadowAssetBuilder.Bushes[rng.Next(2)]), plants, Ground(p), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), Vector3.one * Range(rng, 0.7f, 1.3f));
            for (int clump = 0; clump < 45; clump++)
            {
                if (!ShelfPoint(rng, 0f, false, out Vector2 centre)) continue;
                for (int i = rng.Next(3, 9); i > 0; i--)
                {
                    Vector2 p = centre + RandomDisc(rng, 3f);
                    string kind = rng.NextDouble() < 0.6 ? "GrassTuftDry" : "GrassTuft";
                    Place(MeadowAssetBuilder.PrefabPath(kind), plants, Ground(p), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), Vector3.one * Range(rng, 0.8f, 1.5f));
                }
            }
        }

        // A random spot on the open shelf (dry, below the mountains, not too steep), away from the camp, the dock and
        // the mouth, and (for anything tall) off the sight line from the dock to the cave mouth.
        private static bool ShelfPoint(Random rng, float pad, bool tall, out Vector2 p)
        {
            for (int tries = 0; tries < 400; tries++)
            {
                p = new Vector2(Range(rng, T.Origin, -T.Origin), Range(rng, T.Origin, 40f));
                T.Coast(p.x, p.y, out float land, out float mountain);
                if (land < 0.98f || mountain > 0.3f || T.Roofed(p.x, p.y) || T.GroundAt(p.x, p.y) < 4f) continue;
                if (Slope(p) > 26f) continue;
                if (Vector2.Distance(p, T.Camp) < T.CampClearRadius + pad) continue;
                if (DistanceToSegment(p, T.DockRoot + new Vector2(0f, 10f), T.DockRoot + new Vector2(0f, -40f)) < 8f + pad) continue;
                if (T.DistanceToMouth(p) < 15f + pad) continue;
                if (tall && DistanceToSegment(p, T.DockRoot, T.MouthPath[1]) < 7f + pad) continue;
                return true;
            }
            p = default;
            return false;
        }

        // ---------- The cave: rock ----------

        private static void BuildCaveRock(Transform cave, Random rng)
        {
            // Massive pillars from floor to ceiling break up the big cavern; thinner ones fill in.
            Transform pillars = Group("Columns", cave);
            var placed = new List<Vector2>();
            for (int big = 0, tries = 0; placed.Count < 22 && tries < 6000; tries++)
            {
                bool massive = big < 12;
                if (!CavePoint(rng, massive ? 28f : 14f, massive ? 10f : 5f, out Vector2 p)) continue;
                if (placed.Any(q => Vector2.Distance(p, q) < (massive ? 45f : 25f))) continue;
                float radius = massive ? Range(rng, 5f, 9f) : Range(rng, 1.8f, 3.5f);
                float floor = T.GroundAt(p.x, p.y), ceiling = T.CeilingAt(p.x, p.y);
                if (ceiling - floor < 20f || FloorSpread(p, radius * 0.9f) > 2.5f) continue;
                float y0 = floor - 1.5f;
                Place(CrystalAssetBuilder.RockPath("CaveColumn"), pillars, new Vector3(p.x, y0, p.y), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f),
                    new Vector3(radius, ceiling + 1.5f - y0, radius * Range(rng, 0.8f, 1.2f)));
                placed.Add(p);
                if (massive) big++;
            }

            // Stalagmites in clusters, most of them toward the walls, and stalactites hanging over them.
            Transform stalagmites = Group("Stalagmites", cave);
            for (int cluster = 0; cluster < 46; cluster++)
            {
                if (!CavePoint(rng, 5f, 2f, out Vector2 centre)) continue;
                if (rng.NextDouble() < 0.6 && T.CavernDepth(centre.x, centre.y) > 40f) continue;
                for (int i = rng.Next(2, 7); i > 0; i--)
                {
                    Vector2 p = centre + RandomDisc(rng, 4f);
                    float floor = T.GroundAt(p.x, p.y), room = T.CeilingAt(p.x, p.y) - floor;
                    float radius = rng.NextDouble() < 0.12 ? Range(rng, 2f, 3f) : Range(rng, 0.5f, 1.6f);
                    float height = Mathf.Min(radius * Range(rng, 2.4f, 4.5f), room - 3f);
                    if (height < 1f) continue;
                    Place(CrystalAssetBuilder.RockPath(rng.NextDouble() < 0.6 ? "Stalagmite" : "StalagmiteBlunt"), stalagmites, new Vector3(p.x, floor - 0.3f * radius, p.y),
                        Quaternion.Euler(Range(rng, -6f, 6f), Range(rng, 0f, 360f), Range(rng, -6f, 6f)), new Vector3(radius, height, radius));
                }
            }
            Transform stalactites = Group("Stalactites", cave);
            for (int cluster = 0, tries = 0; cluster < 50 && tries < 2000; tries++)
            {
                if (!CavePoint(rng, 8f, 0f, out Vector2 centre)) continue;
                cluster++;
                for (int i = rng.Next(2, 7); i > 0; i--)
                {
                    Vector2 p = centre + RandomDisc(rng, 5f);
                    float ceiling = T.CeilingAt(p.x, p.y), room = ceiling - T.GroundAt(p.x, p.y);
                    float radius = Range(rng, 0.6f, 2.2f), length = Mathf.Min(radius * Range(rng, 3f, 6f), room * 0.45f);
                    if (length < 1.5f) continue;
                    Place(CrystalAssetBuilder.RockPath("Stalagmite"), stalactites, new Vector3(p.x, ceiling + 0.3f * radius, p.y),
                        Quaternion.Euler(180f + Range(rng, -6f, 6f), Range(rng, 0f, 360f), Range(rng, -6f, 6f)), new Vector3(radius, length, radius));
                }
            }
            Transform boulders = Group("Boulders", cave);
            for (int n = 0; n < 90; n++)
            {
                if (!CavePoint(rng, 4f, 1f, out Vector2 p)) continue;
                float size = Range(rng, 0.7f, 3.2f);
                Place(CrystalAssetBuilder.RockPath(rng.NextDouble() < 0.5 ? "CaveBoulder" : "CaveBoulderRound"), boulders, Ground(p) + Vector3.down * size * 0.3f, RandomRotation(rng),
                    new Vector3(size * Range(rng, 0.9f, 1.4f), size * Range(rng, 0.6f, 1f), size * Range(rng, 0.9f, 1.3f)));
            }
        }

        // A random spot on the cave floor at least minDepth inside the walls, under the roof, and off the tunnel, the
        // ramps and the fight zones (by pad metres).
        private static bool CavePoint(Random rng, float minDepth, float pad, out Vector2 p)
        {
            for (int tries = 0; tries < 400; tries++)
            {
                p = T.CaveCentre + RandomDisc(rng, 190f);
                if (T.CavernDepth(p.x, p.y) < minDepth || !T.Roofed(p.x, p.y)) continue;
                if (CaveClear(p, pad)) return true;
            }
            p = default;
            return false;
        }

        private static bool CaveClear(Vector2 p, float pad)
        {
            if (T.DistanceToMouth(p) < 12f + pad) return false;
            float e = T.DeepAmount(p.x, p.y, out float angle);
            if (T.RampWeight(angle) > 0.01f && e > 0.6f && e < 1.14f) return false;
            if (Vector2.Distance(p, T.BeetleZone) < T.ZoneRadius + pad || Vector2.Distance(p, T.MiniBossZone) < T.ZoneRadius + pad) return false;
            Vector2 boss = new Vector2((p.x - T.FinalBossZone.x) / (T.FinalBossRadii.x + pad), (p.y - T.FinalBossZone.y) / (T.FinalBossRadii.y + pad));
            return boss.magnitude >= 1f;
        }

        // ---------- The cave: crystals ----------

        // Crystal batches are the cave's only light: signposts along the tunnel, at the top and bottom of each ramp and
        // around the fight zones, then scattered over the floors, the walls and the ceiling.
        private static Transform BuildCrystals(Transform cave, Collider ground, Collider roof, Random rng)
        {
            Transform crystals = Group("Crystals", cave);
            var groups = new Dictionary<string, Transform>();
            var spots = new List<Vector3>();

            void Add(Vector3 at, Vector3 normal, float size, float reach = 1f)
            {
                string area = Vector3.Distance(at, Ground(T.MouthPath[1])) < NearMouth ? "Near The Mouth" : T.DeepAmount(at.x, at.z, out _) < 1f ? "Deep Level" : "Upper Level";
                if (!groups.TryGetValue(area, out Transform group)) groups[area] = group = Group(area, crystals);
                Batch(group, at, normal, ColourAt(new Vector2(at.x, at.z), rng), size, reach, rng);
                spots.Add(at);
            }
            bool Spaced(Vector3 at, float spacing) => spots.All(s => (s - at).sqrMagnitude > spacing * spacing);
            bool Cast(Ray ray, float distance, out RaycastHit hit)
            {
                bool onGround = ground.Raycast(ray, out RaycastHit a, distance), onRoof = roof.Raycast(ray, out RaycastHit b, distance);
                hit = onGround && (!onRoof || a.distance <= b.distance) ? a : b;
                return onGround || onRoof;
            }

            // Along the tunnel, on alternate walls: two of them glow out of the mouth, a hint visible from the dock.
            for (int i = 0; i < 6; i++)
            {
                float t = 0.3f + i * 0.13f;
                Vector2 p = T.PointOnPath(t), ahead = T.PointOnPath(t + 0.02f) - p;
                Vector2 side = new Vector2(-ahead.y, ahead.x).normalized * (i % 2 == 0 ? 1f : -1f);
                var ray = new Ray(new Vector3(p.x, T.GroundAt(p.x, p.y) + Range(rng, 1.5f, 4f), p.y), new Vector3(side.x, -0.15f, side.y));
                if (Cast(ray, 20f, out RaycastHit hit)) Add(hit.point, hit.normal, Range(rng, 0.9f, 1.3f));
            }
            // Either side of each ramp, at the top and the bottom.
            foreach (float ramp in T.RampAngles)
            {
                Vector2 dir = new Vector2(Mathf.Cos(ramp * Mathf.Deg2Rad) * T.DeepRadii.x, Mathf.Sin(ramp * Mathf.Deg2Rad) * T.DeepRadii.y);
                Vector2 across = new Vector2(-dir.y, dir.x).normalized;
                foreach (float e in new[] { 0.6f, 1.1f })
                    foreach (float sideways in new[] { -18f, 18f })
                    {
                        Vector2 p = T.DeepCentre + dir * e + across * sideways;
                        if (T.Roofed(p.x, p.y) && T.CavernDepth(p.x, p.y) > 4f) Add(Ground(p), Vector3.up, Range(rng, 1f, 1.6f));
                    }
            }
            // Rings around the fight zones so each reads as a lit arena.
            foreach ((Vector2 centre, Vector2 radii, int count) in new[] { (T.BeetleZone, Vector2.one * (T.ZoneRadius + 3f), 6), (T.MiniBossZone, Vector2.one * (T.ZoneRadius + 3f), 6), (T.FinalBossZone, T.FinalBossRadii * 1.1f, 10) })
                for (int i = 0; i < count; i++)
                {
                    float angle = (i + Range(rng, -0.2f, 0.2f)) * Mathf.PI * 2f / count;
                    Vector2 p = centre + new Vector2(Mathf.Cos(angle) * radii.x, Mathf.Sin(angle) * radii.y);
                    if (T.Roofed(p.x, p.y) && T.CavernDepth(p.x, p.y) > 4f) Add(Ground(p), Vector3.up, Range(rng, 1.1f, 2f));
                }
            // Scattered over the floors, with an occasional giant.
            for (int n = 0, tries = 0; n < 90 && tries < 4000; tries++)
            {
                if (!CavePoint(rng, 4f, 0f, out Vector2 p) || !Spaced(Ground(p), 16f)) continue;
                Add(Ground(p), Vector3.up, rng.NextDouble() < 0.12 ? Range(rng, 2.5f, 4f) : Range(rng, 0.8f, 1.8f));
                n++;
            }
            // On the walls: look sideways from inside the cave until a steep face is hit.
            for (int n = 0, tries = 0; n < 32 && tries < 4000; tries++)
            {
                if (!CavePoint(rng, 10f, 0f, out Vector2 p)) continue;
                Vector2 outward = (p - T.CaveCentre).normalized;
                float turn = Range(rng, -70f, 70f) * Mathf.Deg2Rad;
                Vector2 dir = rng.NextDouble() < 0.7 ? new Vector2(outward.x * Mathf.Cos(turn) - outward.y * Mathf.Sin(turn), outward.x * Mathf.Sin(turn) + outward.y * Mathf.Cos(turn)) : RandomDisc(rng, 1f).normalized;
                var ray = new Ray(new Vector3(p.x, T.GroundAt(p.x, p.y) + Range(rng, 2f, 14f), p.y), new Vector3(dir.x, 0f, dir.y));
                if (!Cast(ray, 60f, out RaycastHit hit) || Mathf.Abs(hit.normal.y) > 0.7f || !T.Roofed(hit.point.x, hit.point.z)) continue;
                if (hit.point.y > T.CeilingAt(hit.point.x, hit.point.z) - 1f || !Spaced(hit.point, 16f)) continue;
                Add(hit.point, hit.normal, Range(rng, 0.7f, 1.5f));
                n++;
            }
            // Hanging from the ceiling, so the vault is never a black void.
            for (int n = 0, tries = 0; n < 24 && tries < 4000; tries++)
            {
                if (!CavePoint(rng, 20f, 0f, out Vector2 p)) continue;
                float floor = T.GroundAt(p.x, p.y);
                if (!roof.Raycast(new Ray(new Vector3(p.x, floor + 2f, p.y), Vector3.up), out RaycastHit hit, 200f) || hit.point.y - floor < 14f || !Spaced(hit.point, 24f)) continue;
                Add(hit.point, hit.normal, Range(rng, 0.8f, 1.6f), 1.8f); // high up: they light the floor far below
                n++;
            }
            return crystals;
        }

        // One batch: 3 to 7 crystals of the three shapes, tilted and sized differently, around one glow light.
        private static void Batch(Transform parent, Vector3 at, Vector3 normal, CrystalColor colour, float size, float reach, Random rng)
        {
            var root = new GameObject("Crystal Batch (" + colour + ")").transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(at, Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
            int count = rng.Next(3, 8);
            for (int i = 0; i < count; i++)
            {
                string shape = CrystalAssetBuilder.CrystalShapes[i == 0 ? (size > 2.4f ? 0 : rng.Next(2)) : rng.Next(3)];
                Vector2 offset = i == 0 ? Vector2.zero : RandomDisc(rng, size * 1.1f);
                float scale = size * (i == 0 ? 1f : Range(rng, 0.35f, 0.85f));
                Vector3 lean = new Vector3(offset.x, 0f, offset.y).normalized * Range(rng, 8f, 35f);
                var crystal = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CrystalAssetBuilder.CrystalPath(shape)), root);
                crystal.transform.SetLocalPositionAndRotation(new Vector3(offset.x, -0.12f * scale, offset.y), Quaternion.Euler(lean.z, Range(rng, 0f, 360f), -lean.x));
                crystal.transform.localScale = new Vector3(scale * Range(rng, 0.85f, 1.15f), scale * Range(rng, 0.85f, 1.35f), scale * Range(rng, 0.85f, 1.15f));
                if (scale < 0.9f) crystal.GetComponent<Collider>().enabled = false; // small ones are decoration only
            }
            // Light falls off with the square of distance, so it takes a strong light to reach across a cavern this size.
            Light glow = CrystalAssetBuilder.AddGlow(root, Vector3.up * (1f + size * 2.8f), (14f + size * 9f + Range(rng, 0f, 4f)) * reach,
                (8f + 6f * size) * reach * reach * CrystalAssetBuilder.LightBoost(colour));
            var batch = new SerializedObject(root.gameObject.AddComponent<CrystalBatch>());
            batch.FindProperty("crystalMaterial").objectReferenceValue = CrystalAssetBuilder.CrystalMaterial(colour);
            batch.FindProperty("glow").objectReferenceValue = glow;
            batch.ApplyModifiedPropertiesWithoutUndo();
            root.GetComponent<CrystalBatch>().Apply();
        }

        private static CrystalColor ColourAt(Vector2 p, Random rng)
        {
            if (rng.NextDouble() < 0.12) return (CrystalColor)rng.Next(6);
            CrystalColor[] colours = Palette.OrderBy(area => Vector2.Distance(area.at, p + RandomDisc(rng, 25f))).First().colours;
            return colours[rng.Next(colours.Length)];
        }

        // =====================================================================
        // Scene contents
        // =====================================================================

        // A bright coastal day: high sun from the south-east (it lights the mountain face and the cave mouth seen from
        // the dock), blue sky and a sea haze that swallows the horizon. CaveLighting darkens all of it inside the cave.
        private static void BuildLighting()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.sun = sun;

            string skyPath = CrystalAssetBuilder.MaterialFolder + "/CoastalSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            sky.SetFloat("_SunSize", 0.045f);
            sky.SetFloat("_SunSizeConvergence", 5f);
            sky.SetFloat("_AtmosphereThickness", 0.9f);
            sky.SetColor("_SkyTint", new Color(0.42f, 0.6f, 0.86f));
            sky.SetColor("_GroundColor", new Color(0.38f, 0.44f, 0.5f));
            sky.SetFloat("_Exposure", 1.3f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.6f, 0.7f, 0.84f);
            RenderSettings.ambientEquatorColor = new Color(0.6f, 0.63f, 0.64f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.3f, 0.27f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.7f, 0.8f, 0.88f);
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 560f;
        }

        // Baked in the editor from the ground, rocks, crystals and camp only, and saved beside the scene.
        // NavMeshBaker rebuilds it at runtime if the data goes missing.
        private static void BakeNavigation()
        {
            var navigation = new GameObject("Navigation");
            var surface = navigation.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.25f; // coarser than default so the big level bakes in reasonable time
            navigation.AddComponent<NavMeshBaker>();
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
        }

        // The player arrives by boat: they start (and respawn) on the dock's top deck, looking toward the cave mouth,
        // with all their gear. GameSystems brings up the HUD, menus and pause, as in every level.
        private static void BuildGameplay()
        {
            var gameplay = new GameObject("Gameplay").transform;
            Vector3 spawnAt = new Vector3(T.DockRoot.x, DockBoatBuilder.DeckHeight, T.DockRoot.y - 2.5f);
            Vector2 toMouth = T.MouthPath[1] - new Vector2(spawnAt.x, spawnAt.z);
            Quaternion facing = Quaternion.LookRotation(new Vector3(toMouth.x, 0f, toMouth.y));
            GameObject spawn = Place(CorePrefabBuilder.SpawnPath, gameplay, spawnAt, facing, Vector3.one);
            spawn.name = "Spawn At Dock";

            GameObject player = Place(CorePrefabBuilder.PlayerPath, null, spawnAt + Vector3.up * 0.05f, facing, Vector3.one);
            player.GetComponentInChildren<Camera>(true).clearFlags = CameraClearFlags.Skybox; // the shared player clears to a flat colour
            var gear = new SerializedObject(player.GetComponent<PlayerGear>());
            SerializedProperty items = gear.FindProperty("startingItems");
            PlayerItem[] all = { PlayerItem.MiningLaser, PlayerItem.Scanner, PlayerItem.Flask };
            items.arraySize = all.Length;
            for (int i = 0; i < all.Length; i++) items.GetArrayElementAtIndex(i).enumValueIndex = (int)all[i];
            gear.ApplyModifiedPropertiesWithoutUndo();
            // A big level, like the tutorial: the scanner's scene-wide range (signal strength and creature pickup).
            var scanner = new SerializedObject(player.GetComponentInChildren<ScannerController>(true));
            scanner.FindProperty("levelRangeOverride").floatValue = 200f;
            scanner.ApplyModifiedPropertiesWithoutUndo();
            Place(CorePrefabBuilder.SystemsPath, null, Vector3.zero, Quaternion.identity, Vector3.one);

            // Falling off a sea cliff or the dock lands you back on the dock.
            new GameObject("Fall Respawn", typeof(FallRespawn)).transform.SetParent(gameplay, false);
        }

        // Cavendish sits on his log and Eugene waits by the fire with his shop open, as at the end of the tutorial.
        // Neither has quests here yet; Eugene's ten cousins wait despawned for the angry-merchant ambush.
        private static void BuildResidents(Transform camp)
        {
            var residents = new GameObject("Camp Residents").transform;
            Vector3 fire = camp.position;

            GameObject cavendish = Place(CavendishPath, residents, fire, Quaternion.identity, Vector3.one);
            cavendish.name = "Cavendish";
            Transform log = camp.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Cavendish's Log");
            Vector3 seat = log != null ? LogTop(log) : Ground(T.Camp + new Vector2(3.4f, 0.5f));
            cavendish.transform.SetPositionAndRotation(seat, Facing(seat, fire));
            Transform visual = cavendish.transform.Find("Visual");
            if (log != null && visual != null) visual.localPosition += Vector3.down * SitDrop;
            cavendish.AddComponent<NpcActor>();
            var voice = new GameObject("Cavendish Dialogue");
            voice.transform.SetParent(cavendish.transform, false);
            voice.transform.localPosition = new Vector3(0f, 2.45f, 0f);
            var dialogue = new SerializedObject(voice.AddComponent<NpcDialogue>());
            dialogue.FindProperty("speaker").stringValue = "Cavendish";
            dialogue.ApplyModifiedPropertiesWithoutUndo();

            Vector3 stand = Ground(T.Camp + new Vector2(1.8f, -3f));
            var spot = new GameObject("Merchant Spot At Camp").transform;
            spot.SetParent(residents, false);
            spot.SetPositionAndRotation(stand, Facing(stand, fire));
            var perch = new GameObject("Merchant Seat").transform;
            perch.SetParent(residents, false);
            Transform otherLog = camp.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "LogSeat");
            Vector3 top = otherLog != null ? LogTop(otherLog) : stand;
            perch.SetPositionAndRotation(top, Facing(top, fire));
            GameObject merchant = Place(MerchantPath, residents, stand, spot.rotation, Vector3.one);
            merchant.name = "Eugene"; // also its name in the HUD target readout
            var data = new SerializedObject(merchant.GetComponent<MerchantCrab>());
            data.FindProperty("returnPoint").objectReferenceValue = spot;
            data.FindProperty("seat").objectReferenceValue = perch;
            data.FindProperty("startFriendly").boolValue = true;
            CoreSceneSetup.AddCousins(merchant.GetComponent<MerchantCrab>(), data.FindProperty("cousins"));
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        // Empty fight zones from the design map, each a CombatZone (enemies placed later attack inside its radius and
        // its rings show in the Scene view), and where boat travel will go.
        private static void BuildMarkers()
        {
            var markers = new GameObject("Level Markers (placeholders)").transform;
            Zone(markers, "Beetles Fight Zone", T.BeetleZone, T.ZoneRadius);
            Zone(markers, "Mini Boss 2 Fight Zone", T.MiniBossZone, T.ZoneRadius);
            Zone(markers, "Final Boss Fight Zone", T.FinalBossZone, T.FinalBossRadii.y * 1.4f);
            Transform boat = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).First(t => t.name == "Boat");
            var travel = new GameObject("Boat Travel (level-to-level travel is not built yet)").transform;
            travel.SetParent(markers, false);
            travel.position = boat.position;
        }

        private static void Zone(Transform parent, string name, Vector2 centre, float radius)
        {
            var zone = new GameObject(name).transform;
            zone.SetParent(parent, false);
            zone.position = Ground(centre);
            zone.gameObject.AddComponent<CombatZone>().Radius = radius;
        }

        // =====================================================================
        // Validation
        // =====================================================================

        public static void Validate()
        {
            EditorSceneManager.OpenScene(ScenePath);
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<PlayerGear>() == 1, "Expected one shared Player.");
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<GameSystems>() == 1, "Expected one GameSystems (HUD, menus, pause).");
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<PlayerSpawnPoint>() == 1, "Expected one PlayerSpawnPoint.");
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<Camera>() == 1 && EnemyTestBuilder.Count<AudioListener>() == 1, "Expected one camera and listener.");
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<FallRespawn>() == 1, "Expected one FallRespawn.");
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<CaveLighting>() == 1, "Expected one CaveLighting.");
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<TrophyStand>() == 3, "Expected the base camp's three trophy stands.");
            EnemyTestBuilder.Require(EnemyTestBuilder.All<NpcActor>().Any(a => a.Kind == NpcActor.Role.Cavendish), "Cavendish is missing.");
            MerchantCrab eugene = EnemyTestBuilder.All<MerchantCrab>().SingleOrDefault();
            EnemyTestBuilder.Require(eugene != null && new SerializedObject(eugene).FindProperty("startFriendly").boolValue, "Expected one friendly Eugene.");
            foreach (string piece in new[] { "Dock", "Boat", "Campsite" })
                EnemyTestBuilder.Require(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Any(t => t.name == piece), piece + " is missing.");
            NavMeshSurface surface = EnemyTestBuilder.All<NavMeshSurface>().Single();
            EnemyTestBuilder.Require(surface.navMeshData != null, "The NavMesh is not baked.");

            List<CrystalBatch> batches = EnemyTestBuilder.All<CrystalBatch>();
            int lights = batches.Count(b => b.GetComponentInChildren<Light>() != null);
            EnemyTestBuilder.Require(batches.All(b => b.CrystalMaterial != null), "A crystal batch has no material.");
            EnemyTestBuilder.Require(lights >= 100 && lights <= 220, $"{lights} crystal lights; expected 100-220 (enough to see by, within Forward+'s light budget).");
            EnemyTestBuilder.Require(batches.Select(b => b.CrystalMaterial).Distinct().Count() == 6, "Expected all six crystal colours.");

            // Everything that matters is reachable on foot from the dock.
            Vector3 start = Object.FindAnyObjectByType<PlayerSpawnPoint>().transform.position;
            var goals = new List<(string name, Vector2 at)>
            {
                ("the camp", T.Camp + new Vector2(0f, -9f)), ("inside the cave mouth", T.PointOnPath(0.85f)),
                ("the beetles' zone", T.BeetleZone), ("mini boss 2's zone", T.MiniBossZone), ("the final boss's zone", T.FinalBossZone)
            };
            foreach (float ramp in T.RampAngles) // the foot of every ramp, on the deep level
                goals.Add(($"the foot of the {ramp} degree ramp", T.DeepCentre + new Vector2(Mathf.Cos(ramp * Mathf.Deg2Rad) * T.DeepRadii.x, Mathf.Sin(ramp * Mathf.Deg2Rad) * T.DeepRadii.y) * 0.55f));
            EnemyTestBuilder.Require(NavMesh.SamplePosition(start, out NavMeshHit from, 3f, NavMesh.AllAreas), "The spawn is off the NavMesh.");
            foreach ((string name, Vector2 at) in goals)
            {
                EnemyTestBuilder.Require(NavMesh.SamplePosition(Ground(at), out NavMeshHit to, 6f, NavMesh.AllAreas), name + " is off the NavMesh.");
                var path = new NavMeshPath();
                EnemyTestBuilder.Require(NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                    "No walkable path from the dock to " + name + ".");
            }
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    EnemyTestBuilder.Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0, "Missing script: " + item.name);
            Debug.Log($"CRYSTAL_CAVES_VALIDATED ({batches.Count} crystal batches, {lights} lights)");
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private static Vector3 Ground(Vector2 p) => new Vector3(p.x, T.GroundAt(p.x, p.y), p.y);

        private static GameObject Place(string path, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new InvalidOperationException("Missing prefab " + path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            if (parent != null) instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = scale;
            return instance;
        }

        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static float Range(Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        private static Vector2 RandomDisc(Random rng, float radius)
        {
            float angle = Range(rng, 0f, Mathf.PI * 2f), r = Mathf.Sqrt((float)rng.NextDouble()) * radius;
            return new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
        }

        private static Quaternion RandomRotation(Random rng) => Quaternion.Euler(Range(rng, 0f, 360f), Range(rng, 0f, 360f), Range(rng, 0f, 360f));

        private static Quaternion Facing(Vector3 from, Vector3 to) => Quaternion.LookRotation(new Vector3(to.x - from.x, 0f, to.z - from.z));

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        // Steepest ground slope around a point, in degrees.
        private static float Slope(Vector2 p)
        {
            const float d = 1.5f;
            float dx = T.GroundAt(p.x + d, p.y) - T.GroundAt(p.x - d, p.y), dz = T.GroundAt(p.x, p.y + d) - T.GroundAt(p.x, p.y - d);
            return Mathf.Atan(new Vector2(dx, dz).magnitude / (2f * d)) * Mathf.Rad2Deg;
        }

        // How uneven the floor is under a pillar's base.
        private static float FloorSpread(Vector2 p, float radius)
        {
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < 6; i++)
            {
                float h = T.GroundAt(p.x + Mathf.Cos(i * 1.047f) * radius, p.y + Mathf.Sin(i * 1.047f) * radius);
                min = Mathf.Min(min, h);
                max = Mathf.Max(max, h);
            }
            return max - min;
        }

        // The middle of a log's top surface.
        private static Vector3 LogTop(Transform log)
        {
            var bounds = new Bounds(log.position, Vector3.zero);
            foreach (Renderer part in log.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(part.bounds);
            return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        }

        private static Material SurfaceMaterial(T.Surface surface)
        {
            // The mountains use the tutorial's mountain rock, so the ranges around both levels match.
            if (surface == T.Surface.MountainRock || surface == T.Surface.MountainCliff)
            {
                var shared = AssetDatabase.LoadAssetAtPath<Material>(MeadowAssetBuilder.Folder + "/Terrain/Ground" + (surface == T.Surface.MountainRock ? "Rock" : "Cliff") + ".mat");
                if (shared != null) return shared;
            }
            Color color = surface switch
            {
                T.Surface.ShelfRock => new Color(0.62f, 0.5f, 0.38f),
                T.Surface.ShelfMoss => new Color(0.43f, 0.53f, 0.3f),
                T.Surface.CoastCliff => new Color(0.52f, 0.4f, 0.3f),
                T.Surface.SeaFloor => new Color(0.5f, 0.46f, 0.36f),
                T.Surface.MountainRock => new Color(0.55f, 0.51f, 0.47f),
                T.Surface.MountainCliff => new Color(0.66f, 0.61f, 0.56f),
                T.Surface.CaveFloor => new Color(0.36f, 0.38f, 0.44f),
                T.Surface.CaveDeep => new Color(0.3f, 0.33f, 0.4f),
                T.Surface.CaveSand => new Color(0.52f, 0.46f, 0.34f),
                T.Surface.CaveWall => new Color(0.3f, 0.31f, 0.37f),
                _ => new Color(0.26f, 0.27f, 0.32f)
            };
            return CrystalAssetBuilder.SimpleMaterial("Ground" + surface, color, 0.06f);
        }

        // The tutorial pond's stylized water, retuned for open sea: deeper blue, turquoise shallows, bigger slower swells.
        private static Material OceanMaterial()
        {
            Shader shader = Shader.Find("Signal Lost/Stylized Water");
            if (shader == null) throw new InvalidOperationException("Signal Lost/Stylized Water shader not found.");
            string path = CrystalAssetBuilder.MaterialFolder + "/OceanWater.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_DeepColor", new Color(0.03f, 0.28f, 0.42f, 0.95f));
            material.SetColor("_ShallowColor", new Color(0.1f, 0.58f, 0.6f, 0.85f));
            material.SetColor("_HorizonColor", new Color(0.72f, 0.84f, 0.93f, 1f));
            material.SetFloat("_FoamWidth", 0.22f);
            material.SetFloat("_WaveHeight", 0.22f);
            material.SetFloat("_WaveScale", 0.06f);
            material.SetFloat("_WaveSpeed", 0.6f);
            material.SetFloat("_Sparkle", 1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mesh SaveMesh(Mesh mesh)
        {
            EnsureFolder(MeshFolder);
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
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
