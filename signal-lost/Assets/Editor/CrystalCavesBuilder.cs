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
using SignalLost.World;
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
    /// Creates the Crystal Caves level (design map 11-08, revised 11-09; see docs/crystal-caves.md). Like Desert Dunes, it
    /// is two parts:
    ///  * CrystalCavesEnvironment.prefab (Prefabs/Enviornment objects/CrystalCaves): the island's ground, sea floor and cave
    ///    roof; the shared island pieces (<see cref="IslandKit"/>: the sea, and the arrival dock with its sailboat at the
    ///    foot of the cove); the ledge's rock, plants and sea-cliff columns; the cave's columns, stalagmites,
    ///    stalactites, boulders and glowing crystal batches; and the CaveLighting that darkens it. Rebuilding it from this
    ///    script (same seed, same layout) replaces edits made to the prefab.
    ///  * Scenes/Large Areas/CrystalCaves.unity, created once and then edited by hand: the coastal lighting, the shared
    ///    Player and GameSystems (HUD, menus, pause), the spawn at the dock, Cavendish's island camp with Cavendish and
    ///    Eugene, the baked NavMesh, and empty fight-zone markers for enemies to come.
    /// The landform itself is in <see cref="CrystalCavesTerrain"/>; the art kit in <see cref="CrystalAssetBuilder"/>.
    /// </summary>
    internal static class CrystalCavesBuilder
    {
        public const string ScenePath = "Assets/Scenes/Large Areas/CrystalCaves.unity";
        private const string NavMeshPath = "Assets/Scenes/Large Areas/CrystalCavesNavMesh.asset";
        public const string EnvironmentPrefabPath = CrystalAssetBuilder.Folder + "/CrystalCavesEnvironment.prefab";
        /// <summary>Cavendish's camp as pitched here: the base camp with an <see cref="IslandCamp"/> (a prefab variant).</summary>
        public const string CampPrefabPath = "Assets/Prefabs/Core/CrystalCavesCamp.prefab";
        public const string IslandName = "The Crystal Caves";
        private const string MeshFolder = CrystalAssetBuilder.Folder + "/Meshes";
        private const string CavendishPath = "Assets/Prefabs/Enviornment objects/GrayboxProps/CapybaraAstronaut.prefab";
        private const string MerchantPath = "Assets/Prefabs/NPC/MerchantCrab.prefab";
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
            GameObject camp = AssetDatabase.LoadAssetAtPath<GameObject>(CampPrefabPath) ?? BuildCampPrefab();
            T.Generate();
            PrefabUtility.InstantiatePrefab(environment);
            BuildLighting();
            IslandCamp island = Place(CampPrefabPath, null, Ground(T.Camp), Quaternion.identity, Vector3.one).GetComponent<IslandCamp>();
            island.name = "Cavendish's Camp";
            BakeNavigation(); // before anyone who walks is placed, so they never punch holes in it
            BuildGameplay();
            BuildResidents(island);
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
            BuildEnvironmentPrefab();
            BuildCampPrefab();
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
            // The art kit, if it is missing (checked by the last piece it makes).
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CrystalAssetBuilder.RockPath("CoastBlock")) == null) CrystalAssetBuilder.Build();
            T.Generate();
            List<string> problems = T.Check();
            if (problems.Count > 0) throw new InvalidOperationException("Crystal Caves landform check failed:\n" + string.Join("\n", problems));

            var rng = new Random(20261108);
            var root = new GameObject("Crystal Caves Environment");
            Mesh groundMesh = T.BuildGroundMesh(out T.Surface[] groundSurfaces, out Mesh seabedMesh);
            MeshCollider ground = Solid("Ground", root.transform, SaveMesh(groundMesh), groundSurfaces);
            // The sea floor beyond the coast is its own object, not walkable, as on the other islands: the NavMesh,
            // enemies and spawns stay on land.
            MeshCollider seabed = Solid("Seabed", root.transform, SaveMesh(seabedMesh), new[] { T.Surface.SeaFloor });
            IslandKit.NoWalk(seabed.gameObject);
            Mesh roofMesh = T.BuildRoofMesh(out T.Surface[] roofSurfaces);
            MeshCollider roof = Solid("Cave Roof", root.transform, SaveMesh(roofMesh), roofSurfaces);
            roof.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true; // the mountain top is out of reach
            // Vertical rock wherever the player must not climb: the mountains' foot, the cut to the mouth, the cave's cliffs.
            Mesh barrierMesh = T.BuildBarrierMesh(T.Barriers(), out T.Surface[] barrierSurfaces);
            Solid("Climbing Barriers", root.transform, SaveMesh(barrierMesh), barrierSurfaces);
            // The sea cliffs' faces: stone blocks in tiers with narrow ledges, like the reference (11-09).
            Mesh cliffMesh = T.BuildCliffBlockMesh(T.CliffBlocks(), out T.Surface[] cliffSurfaces);
            Solid("Cliff Blocks", root.transform, SaveMesh(cliffMesh), cliffSurfaces);
            // The cave mouth: a rocky arch in the cliff face, with a closed passage back into the tunnel.
            Mesh mouthMesh = T.BuildMouthMesh(out T.Surface[] mouthSurfaces);
            Solid("Cave Mouth Arch", root.transform, SaveMesh(mouthMesh), mouthSurfaces);
            Physics.SyncTransforms();

            // The shared island pieces: the sea (with its deep-water rescue) and the arrival dock at the foot of the cove.
            Transform shore = Group("Shore", root.transform);
            IslandKit.BuildOcean(shore, T.SeaLevel, Vector2.zero, T.MapSize * 0.5f, 2f, T.Ground, new Vector3(T.CaveCentre.x, 0f, T.CaveCentre.y), "CrystalCavesOcean");
            Vector2 dockStart = IslandKit.ShoreStart(T.CoveTop, T.DockSeaward, T.Ground, T.SeaLevel);
            IslandKit.BuildDock(shore, new IslandKit.DockSpec
            {
                Name = "CrystalCavesDock", Start = dockStart, Seaward = T.DockSeaward, Length = 12f,
                Role = IslandDock.Role.Arrival, IslandName = IslandName
            }, T.SeaLevel, T.Ground, 20261109);

            BuildCoast(Group("Coast", root.transform), rng);
            BuildCliffPlants(Group("Cliff Plants", root.transform), new Random(20261113)); // its own seed, so nothing else moves
            Transform cave = Group("Cave", root.transform);
            var mouth = new GameObject("Cave Mouth").transform;
            mouth.SetParent(cave, false);
            mouth.SetPositionAndRotation(Ground(T.MouthPath[0]) + Vector3.up * 2f, Quaternion.LookRotation(Vector3.back)); // the opening in the cliff face
            // The cave has its own random numbers, starting where the coast's left off when the cave was laid out
            // (11-09), so reshaping the outside never moves its rock or crystals.
            var caveRng = new Random(20261108);
            for (int i = 0; i < 4254; i++) caveRng.NextDouble();
            BuildCaveRock(cave, caveRng);
            Transform crystals = BuildCrystals(cave, ground, roof, caveRng);
            BuildGiantCrystals(crystals, new Random(20261110)); // its own seed, so everything placed before stays put

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
            // Where the ledge meets the sea, found by walking out from the mountains at each angle round the south-east.
            var edge = new List<(Vector2 point, Vector2 outward)>();
            for (float angle = 280f; angle <= 336f; angle += 1.2f)
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

            // Stacked rock columns lining the sea cliffs, and tumbled blocks in the surf, like the reference coast; the
            // cove and the water off the dock stay clear.
            Transform columns = Group("Cliff Columns", coast);
            foreach ((Vector2 point, Vector2 outward) in edge)
            {
                if (T.DistanceToCove(point) < 14f) continue;
                Vector2 inland = point - outward * 4f;
                float top = Mathf.Min(T.GroundAt(inland.x, inland.y), 16f); // the ledge's height, even where the sea cliffs rise behind
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
            for (int n = 0; n < 60; n++)
            {
                (Vector2 point, Vector2 outward) = edge[rng.Next(edge.Count)];
                Vector2 p = point + outward * Range(rng, 2f, 12f) + new Vector2(-outward.y, outward.x) * Range(rng, -3f, 3f);
                if (T.DistanceToCove(p) < 18f || DistanceToSegment(p, T.CoveBottom, T.CoveBottom + T.DockSeaward * 30f) < 16f) continue;
                float size = Range(rng, 1.4f, 3.8f);
                Place(CrystalAssetBuilder.RockPath("CoastBlock"), surf, new Vector3(p.x, Range(rng, -2.5f, -0.2f), p.y), RandomRotation(rng),
                    new Vector3(size * Range(rng, 0.8f, 1.3f), size * Range(rng, 0.7f, 1.3f), size * Range(rng, 0.8f, 1.3f)));
            }
            // Low rocks along both sides of the cove, stepping down to the water with it.
            Transform cove = Group("Cove Rocks", coast);
            Vector2 down = T.DockSeaward, across = new Vector2(-down.y, down.x);
            for (float d = 2f; d < (T.CoveBottom - T.CoveTop).magnitude; d += Range(rng, 2.5f, 4f))
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector2 p = T.CoveTop + down * d + across * side * Range(rng, 7f, 9.5f);
                    float size = Range(rng, 1.2f, 2.6f);
                    Place(CrystalAssetBuilder.RockPath("CoastBlock"), cove, Ground(p) + Vector3.down * size * 0.4f,
                        Quaternion.Euler(Range(rng, -8f, 8f), Range(rng, 0f, 360f), Range(rng, -8f, 8f)), new Vector3(size * Range(rng, 1f, 1.5f), size * Range(rng, 0.5f, 0.9f), size));
                }

            // Rock formations, boulders, wind-bent pines, bushes and grass on the ledge; none of them on the camp, the
            // cove or the line of sight from the dock to the mouth.
            Transform formations = Group("Rock Formations", coast);
            for (int n = 0; n < 7; n++)
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
            for (int n = 0; n < 30; n++)
            {
                if (!ShelfPoint(rng, 2f, true, out Vector2 p)) continue;
                bool big = rng.NextDouble() < 0.25;
                string kind = big ? MeadowAssetBuilder.Rocks[3 + rng.Next(2)] : MeadowAssetBuilder.Rocks[rng.Next(3)];
                Place(MeadowAssetBuilder.PrefabPath(kind), rocks, Ground(p) + Vector3.down * (big ? 0.4f : 0.1f), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), Vector3.one * Range(rng, 0.8f, big ? 1.5f : 2f));
            }
            Transform pines = Group("Pines", coast);
            for (int n = 0, tries = 0; n < 10 && tries < 4000; tries++)
            {
                if (!ShelfPoint(rng, 2f, true, out Vector2 p)) continue;
                Place(MeadowAssetBuilder.PrefabPath(MeadowAssetBuilder.Trees[rng.Next(3)]), pines, Ground(p) + Vector3.down * 0.15f, Quaternion.Euler(Range(rng, -5f, 5f), Range(rng, 0f, 360f), 0f), Vector3.one * Range(rng, 0.8f, 1.2f));
                n++;
            }
            Transform plants = Group("Bushes And Grass", coast);
            for (int n = 0; n < 20; n++)
                if (ShelfPoint(rng, 1f, false, out Vector2 p))
                    Place(MeadowAssetBuilder.PrefabPath(MeadowAssetBuilder.Bushes[rng.Next(2)]), plants, Ground(p), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), Vector3.one * Range(rng, 0.7f, 1.3f));
            for (int clump = 0; clump < 24; clump++)
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

        // Pines, bushes, grass and rocks on the sea cliffs' terraces and along the cliff top, like the reference (11-10), so
        // they show from below. They stand back from each ledge's edge, and none of it can be reached: the lowest ledge
        // is at least 20 m over the ground. (Blocks whose sides turn far off straight back are skipped: their ledges are not simple strips.)
        private static void BuildCliffPlants(Transform plants, Random rng)
        {
            foreach (T.CliffBlock block in T.CliffBlocks())
            {
                if (Vector2.Dot(block.BackA.normalized, -block.Out) < 0.8f || Vector2.Dot(block.BackB.normalized, -block.Out) < 0.8f) continue;
                Vector2 along = (block.B - block.A).normalized;
                float width = (block.B - block.A).magnitude, depth = Mathf.Min(block.BackA.magnitude, block.BackB.magnitude);
                for (int k = 0; k < block.Tiers.Count; k++)
                {
                    T.CliffTier tier = block.Tiers[k];
                    if (!tier.Terrace) continue;
                    // The ledge: from this tier's edge back to the next tier's face (or the back of the block).
                    float near = tier.Back + 1.2f, far = (k + 1 < block.Tiers.Count ? block.Tiers[k + 1].Back : depth) - 1.2f;
                    float left = tier.Inset + 1f, right = width - tier.Inset - 1f;
                    if (far - near < 1.5f || right - left < 2f) continue;
                    Vector3 Spot(float toEdge) // toward the edge, where it shows from below
                    {
                        Vector2 p = block.A + along * Range(rng, left, right) - block.Out * Mathf.Lerp(near, far, toEdge * (float)rng.NextDouble());
                        return new Vector3(p.x, tier.Top, p.y);
                    }
                    Quaternion Turn() => Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f);
                    if (rng.NextDouble() < (k == block.Tiers.Count - 1 ? 0.5 : 0.4))
                        Place(MeadowAssetBuilder.PrefabPath(MeadowAssetBuilder.Trees[rng.Next(3)]), plants, Spot(0.7f) + Vector3.down * 0.15f, Turn(), Vector3.one * Range(rng, 0.9f, 1.4f));
                    for (int n = rng.Next(1, 3); n > 0; n--)
                        Place(MeadowAssetBuilder.PrefabPath(MeadowAssetBuilder.Bushes[rng.Next(2)]), plants, Spot(0.6f), Turn(), Vector3.one * Range(rng, 0.8f, 1.4f));
                    for (int n = rng.Next(0, 3); n > 0; n--)
                        Place(MeadowAssetBuilder.PrefabPath(rng.NextDouble() < 0.5 ? "GrassTuft" : "GrassTuftDry"), plants, Spot(0.8f), Turn(), Vector3.one * Range(rng, 1f, 1.6f));
                    if (rng.NextDouble() < 0.25)
                        Place(MeadowAssetBuilder.PrefabPath(MeadowAssetBuilder.Rocks[rng.Next(3)]), plants, Spot(1f) + Vector3.down * 0.1f, Turn(), Vector3.one * Range(rng, 0.8f, 1.6f));
                }
            }
        }

        // A random spot on the open ledge (dry, below the mountains, not too steep), away from the camp, the cove and the
        // mouth, and (for anything tall) off the sight line from the dock to the cave mouth.
        private static bool ShelfPoint(Random rng, float pad, bool tall, out Vector2 p)
        {
            for (int tries = 0; tries < 400; tries++)
            {
                p = T.LedgeCentre + new Vector2(Range(rng, -70f, 70f), Range(rng, -60f, 60f));
                T.Coast(p.x, p.y, out float land, out float mountain);
                if (land < 0.98f || mountain > 0.02f || T.Roofed(p.x, p.y) || T.GroundAt(p.x, p.y) < 4f) continue; // in front of the mountains' wall
                Vector2 behind = p + (T.CaveCentre - p).normalized * (4f + pad);
                T.Coast(behind.x, behind.y, out _, out float cliff);
                if (cliff > 0.02f) continue; // clear of the cliff blocks standing in front of the wall
                if (Slope(p) > 26f) continue;
                if (Vector2.Distance(p, T.Camp) < T.CampClearRadius + pad) continue;
                if (T.DistanceToCove(p) < 11f + pad) continue;
                if (T.DistanceToMouth(p) < 15f + pad) continue;
                if (tall && DistanceToSegment(p, T.CoveBottom, T.MouthPath[1]) < 7f + pad) continue;
                // Nor anything tall in front of the mouth, so the view out of the cave is clear.
                if (tall && DistanceToSegment(p, T.MouthPath[0], T.MouthPath[0] + Vector2.down * 60f) < 18f + pad) continue;
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
            if (T.RampWeight(p.x, p.y) > 0.01f) return false;
            float edge = T.UpperDistance(p.x, p.y);
            if (edge > -8f - pad && edge < 4f + pad) return false; // the cliff and its barrier wall
            if (Vector2.Distance(p, T.BeetleZone) < T.ZoneRadius + pad || Vector2.Distance(p, T.MiniBossZone) < T.ZoneRadius + pad) return false;
            Vector2 boss = new Vector2((p.x - T.FinalBossZone.x) / (T.FinalBossRadii.x + pad), (p.y - T.FinalBossZone.y) / (T.FinalBossRadii.y + pad));
            return boss.magnitude >= 1f;
        }

        // ---------- The cave: crystals ----------

        // Crystal batches light the cave: signposts along the tunnel, at the top and foot of each ramp and around the
        // fight zones, then scattered over the floors, the walls and the ceiling.
        private static Transform BuildCrystals(Transform cave, Collider ground, Collider roof, Random rng)
        {
            Transform crystals = Group("Crystals", cave);
            var groups = new Dictionary<string, Transform>();
            var spots = new List<Vector3>();

            void Add(Vector3 at, Vector3 normal, float size, float reach = 1f)
            {
                string area = Vector3.Distance(at, Ground(T.MouthPath[1])) < NearMouth ? "Near The Mouth" : T.UpperDistance(at.x, at.z) < 0f ? "Deep Level" : "Upper Level";
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
            // Either side of each ramp, at the top and the foot.
            foreach (T.Ramp ramp in T.Ramps)
            {
                Vector2 across = new Vector2(-ramp.Down.y, ramp.Down.x);
                foreach (Vector2 end in new[] { ramp.Top, ramp.Foot })
                    foreach (float sideways in new[] { -1f, 1f })
                    {
                        Vector2 p = end + across * sideways * (T.RampHalfWidth + 7f);
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

        // Structural giant crystals, like the reference, each in its own colour from the area's palette, with its own size,
        // tilt and turn, and glowing like the small ones:
        //  * at the foot of the cave walls, one to three long rods per spot, leaning back into the wall (the first one
        //    of each spot carries the spot's light);
        //  * near the walls, crystal beams from the floor right up into the ceiling, like angled supports, each with a light.
        // They keep off the tunnel, the ramps, the cliffs and the fight zones, so they never stand in the way. Their lights
        // are counted into the cave's budget (validation allows 220): at most 30 spots and 14 beams on top of 175.
        private static void BuildGiantCrystals(Transform crystals, Random rng)
        {
            Transform group = Group("Giant Crystals", crystals);
            var spots = new List<Vector2>();
            for (int tries = 0; spots.Count < 30 && tries < 8000; tries++)
            {
                if (!CavePoint(rng, 9f, 14f, out Vector2 p) || T.CavernDepth(p.x, p.y) > 15f) continue;
                if (spots.Any(s => Vector2.Distance(s, p) < 26f) || Slope(p) > 25f || T.CeilingAt(p.x, p.y) - T.GroundAt(p.x, p.y) < 14f) continue;
                spots.Add(p);
                bool lit = false;
                Vector2 wall = (p - T.CaveCentre).normalized;
                for (int k = 0, count = rng.Next(1, 4); k < count; k++)
                {
                    Vector2 at = p + (k == 0 ? Vector2.zero : RandomDisc(rng, 3.5f));
                    float yaw = Range(rng, k == 0 ? -35f : -65f, k == 0 ? 35f : 65f) * Mathf.Deg2Rad, tilt = Range(rng, 18f, k == 0 ? 48f : 68f) * Mathf.Deg2Rad;
                    Vector2 lean = new Vector2(wall.x * Mathf.Cos(yaw) - wall.y * Mathf.Sin(yaw), wall.x * Mathf.Sin(yaw) + wall.y * Mathf.Cos(yaw));
                    var dir = new Vector3(lean.x * Mathf.Sin(tilt), Mathf.Cos(tilt), lean.y * Mathf.Sin(tilt));
                    float size = k == 0 ? 1f : Range(rng, 0.45f, 0.8f);
                    int shape = rng.Next(CrystalAssetBuilder.GiantCrystalShapes.Length);
                    float length = Range(rng, 12f, 24f) * size, thickness = (shape == 0 ? Range(rng, 2f, 3.4f) : Range(rng, 1.2f, 2f)) * Mathf.Sqrt(size);
                    Vector3 foot = Ground(at) - dir * 1.5f; // sunk into the floor
                    // The tip ends in the rock over the ceiling (never out through the mountain top), toward the wall.
                    while (length > 6f && foot.y + dir.y * length > T.CeilingAt(foot.x + dir.x * length, foot.z + dir.z * length) + 10f) length -= 1f;
                    var tip = new Vector2(foot.x + dir.x * length, foot.z + dir.z * length);
                    if (T.CavernDepth(tip.x, tip.y) > T.CavernDepth(at.x, at.y) + 2f || !CaveClear(tip, 6f) || !CaveClear(at, 12f)) continue;
                    // Its light shines from in front of its middle, out toward the open cave.
                    Vector3 middle = foot + dir * (length * 0.45f);
                    Vector3? light = lit ? (Vector3?)null : middle - new Vector3(wall.x, 0f, wall.y) * (thickness + 2.5f);
                    GiantCrystal(group, foot, dir, length, shape, thickness, ColourAt(at, rng), light, rng);
                    lit = true;
                }
            }

            // Beams from floor to ceiling, angled, near the walls and well apart from the columns and the other giants.
            List<Vector2> columns = crystals.parent.Find("Columns").Cast<Transform>().Select(c => new Vector2(c.position.x, c.position.z)).ToList();
            var beams = new List<Vector2>();
            for (int tries = 0; beams.Count < 14 && tries < 8000; tries++)
            {
                if (!CavePoint(rng, 12f, 14f, out Vector2 a) || T.CavernDepth(a.x, a.y) > 40f) continue;
                if (beams.Any(s => Vector2.Distance(s, a) < 40f) || spots.Any(s => Vector2.Distance(s, a) < 20f) || columns.Any(c => Vector2.Distance(c, a) < 18f) || Slope(a) > 25f) continue;
                // Leaning up to 14 m off upright, any way round.
                Vector2 b = a + RandomDisc(rng, 1f).normalized * Range(rng, 3f, 14f);
                float floor = T.GroundAt(a.x, a.y), ceiling = T.CeilingAt(b.x, b.y);
                if (ceiling - floor < 18f || !T.Roofed(b.x, b.y) || !CaveClear(b, 8f)) continue;
                var bottom = new Vector3(a.x, floor - 2f, a.y);
                // The tip goes well up into the rock (never through the mountain top, at least 17 m above), so only the
                // straight part shows, meeting the ceiling like a support.
                var top = new Vector3(b.x, ceiling + Mathf.Min(0.22f * (ceiling - floor), 14f), b.y);
                bool open = true; // all the way up in the open cave, and clear of the cliffs, the ramps and the tunnel
                for (float t = 0.1f; open && t < 0.95f; t += 0.05f)
                {
                    Vector3 q = Vector3.Lerp(bottom, top, t);
                    if (q.y > T.CeilingAt(q.x, q.z) - 0.5f) break; // into the ceiling: the rest is in the rock
                    open = T.Roofed(q.x, q.z) && q.y > T.GroundAt(q.x, q.z) + 0.5f && CaveClear(new Vector2(q.x, q.z), 8f);
                }
                if (!open) continue;
                beams.Add(a);
                int shape = rng.Next(CrystalAssetBuilder.GiantCrystalShapes.Length);
                float thickness = shape == 0 ? Range(rng, 4.5f, 6.5f) : Range(rng, 2.8f, 4f); // stout, like supports
                Vector3 axis = top - bottom, middle = bottom + axis * 0.35f;
                Vector2 inward = (T.CaveCentre - a).normalized;
                GiantCrystal(group, bottom, axis.normalized, axis.magnitude, shape, thickness, ColourAt(a, rng), middle + new Vector3(inward.x, 0f, inward.y) * (thickness + 2.5f), rng);
            }
        }

        // One giant crystal as its own CrystalBatch (pick another material on it to recolour it and its light together),
        // standing on foot along dir, with a glow light at the given place if any.
        private static void GiantCrystal(Transform group, Vector3 foot, Vector3 dir, float length, int shape, float thickness, CrystalColor colour, Vector3? light, Random rng)
        {
            var root = new GameObject("Giant Crystal (" + colour + ")").transform;
            root.SetParent(group, false);
            root.SetPositionAndRotation(foot, Quaternion.FromToRotation(Vector3.up, dir) * Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
            var crystal = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CrystalAssetBuilder.CrystalPath(CrystalAssetBuilder.GiantCrystalShapes[shape])), root);
            crystal.transform.localScale = new Vector3(thickness, length / (shape == 0 ? 10f : 9f), thickness);
            Light glow = null;
            if (light != null)
            {
                Vector3 at = light.Value; // in the open cave: inside the rock it would light nothing
                at.y = Mathf.Min(Mathf.Max(at.y, T.GroundAt(at.x, at.z) + 2f), T.CeilingAt(at.x, at.z) - 2f);
                glow = CrystalAssetBuilder.AddGlow(root, root.InverseTransformPoint(at), 26f, 22f * CrystalAssetBuilder.LightBoost(colour));
            }
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
        // Cavendish's camp
        // =====================================================================

        /// <summary>
        /// Cavendish's camp for this island: a prefab variant of the shared base camp (same fire, tent, logs, supplies and
        /// trophy stands, so later changes to BaseCamp carry over) with an <see cref="IslandCamp"/>, so the crew can arrive
        /// by boat and pitch it, like the desert camp. It has no island theme of its own yet.
        /// </summary>
        private static GameObject BuildCampPrefab()
        {
            var camp = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BaseCampBuilder.PrefabPath));
            camp.name = "Crystal Caves Camp";
            Transform Child(string name) => camp.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
            Transform cavendishLog = Child("Cavendish's Log"), otherLog = Child("LogSeat");
            // Here only (overrides in this variant): the supplies and barrel stand clear of the tent instead of in it.
            Child("Cavendish's Supplies").localPosition = new Vector3(-1.5f, 0f, 3.5f);
            Child("Barrel").localPosition = new Vector3(-0.4f, 0f, 4.4f);
            Transform Mark(string name, Vector3 at)
            {
                var mark = new GameObject(name).transform;
                mark.SetParent(camp.transform, false);
                mark.localPosition = at;
                Vector3 toFire = -at;
                toFire.y = 0f;
                mark.localRotation = toFire.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toFire) : Quaternion.identity;
                return mark;
            }
            // Where Cavendish and Eugene sit (the log tops, facing the fire) and stand while pitching camp, as at the desert camp.
            var island = new SerializedObject(camp.AddComponent<IslandCamp>());
            island.FindProperty("cavendishSeat").objectReferenceValue = Mark("Cavendish Seat", camp.transform.InverseTransformPoint(LogTop(cavendishLog)));
            // Eugene sits on the ground in front of his log, facing the fire: perched on the log, his legs hang in the air.
            Vector3 log = otherLog.localPosition;
            island.FindProperty("merchantSeat").objectReferenceValue = Mark("Merchant Seat", new Vector3(log.x, 0f, log.z) * (1f - 1.3f / new Vector2(log.x, log.z).magnitude));
            island.FindProperty("cavendishStand").objectReferenceValue = Mark("Cavendish Stand", new Vector3(1.9f, 0f, -1.3f));
            island.FindProperty("merchantStand").objectReferenceValue = Mark("Merchant Stand", new Vector3(2.6f, 0f, -2.6f));
            Transform[] order = { Child("Fire Pit"), cavendishLog, otherLog, Child("Tent"), Child("Cavendish's Supplies"), Child("Barrel"), Child("Trophy Stands"), Child("Campfire") };
            SerializedProperty pieces = island.FindProperty("pieces");
            pieces.arraySize = order.Length;
            for (int i = 0; i < order.Length; i++) pieces.GetArrayElementAtIndex(i).objectReferenceValue = order[i];
            island.ApplyModifiedPropertiesWithoutUndo();
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(camp, CampPrefabPath);
            Object.DestroyImmediate(camp);
            return saved;
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

        // Baked in the editor from the ground, rocks, crystals and camp only (not the sea floor or the mountain top),
        // and saved beside the scene. NavMeshBaker rebuilds it at runtime if the data goes missing.
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

        // Loaded directly (not by boat), the player starts at the foot of the cove by the dock, looking up it toward
        // the cave mouth, with all their gear. Arriving by boat, the voyage puts them on the dock instead.
        // GameSystems brings up the HUD, menus and pause; the sea adds the deep-water rescue to the player.
        private static void BuildGameplay()
        {
            var gameplay = new GameObject("Gameplay").transform;
            Vector2 foot = T.CoveBottom - T.DockSeaward * 3f;
            Vector3 spawnAt = Ground(foot) + Vector3.up * 0.1f;
            Vector2 toMouth = T.MouthPath[1] - foot;
            Quaternion facing = Quaternion.LookRotation(new Vector3(toMouth.x, 0f, toMouth.y));
            GameObject spawn = Place(CorePrefabBuilder.SpawnPath, gameplay, spawnAt, facing, Vector3.one);
            spawn.name = "Spawn At The Cove";

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
        }

        // Cavendish sits on his log and Eugene on the other, a friendly trader, as at the desert camp. Cavendish has small
        // talk only; no quests here yet. Without a StageTheme the crew wears its Crash Meadow look.
        private static void BuildResidents(IslandCamp island)
        {
            var residents = new GameObject("Residents").transform;
            residents.SetParent(island.transform, false);

            var cavendish = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CavendishPath), residents);
            cavendish.name = "Cavendish";
            IslandCamp.SitCavendish(cavendish.transform, island.CavendishSeat);
            var actor = new SerializedObject(cavendish.AddComponent<NpcActor>());
            actor.FindProperty("role").enumValueIndex = (int)NpcActor.Role.Cavendish;
            SerializedProperty talk = actor.FindProperty("smallTalk");
            string[] lines =
            {
                "That cave goes a long way down. Mind the drop past the front ledge.",
                "Glowing rocks. Lovely. Don't lick them.",
                "The Flight Control Module is in there somewhere. Your scanner will find it.",
                "Sea air. Much better than sand. Much, much better than sand."
            };
            talk.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++) talk.GetArrayElementAtIndex(i).stringValue = lines[i];
            actor.ApplyModifiedPropertiesWithoutUndo();
            var voice = new GameObject("Cavendish Dialogue");
            voice.transform.SetParent(cavendish.transform, false);
            voice.transform.localPosition = new Vector3(0f, 2.45f, 0f);
            var dialogue = new SerializedObject(voice.AddComponent<NpcDialogue>());
            dialogue.FindProperty("speaker").stringValue = "Cavendish";
            dialogue.ApplyModifiedPropertiesWithoutUndo();

            var eugene = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MerchantPath), residents);
            eugene.name = "Eugene"; // also its name in the HUD target readout
            eugene.transform.SetPositionAndRotation(island.MerchantSeat.position, island.MerchantSeat.rotation);
            var merchant = new SerializedObject(eugene.GetComponent<MerchantCrab>());
            merchant.FindProperty("startFriendly").boolValue = true;
            merchant.FindProperty("spawnAfterQuest").boolValue = false;
            merchant.FindProperty("seat").objectReferenceValue = island.MerchantSeat;
            merchant.FindProperty("returnPoint").objectReferenceValue = island.MerchantStand;
            merchant.FindProperty("cousins").arraySize = 0;
            merchant.ApplyModifiedPropertiesWithoutUndo();

            var links = new SerializedObject(island);
            links.FindProperty("cavendish").objectReferenceValue = cavendish.GetComponent<NpcActor>();
            links.FindProperty("merchant").objectReferenceValue = eugene.GetComponent<NpcActor>();
            links.ApplyModifiedPropertiesWithoutUndo();
        }

        // Empty fight zones from the design map, each a CombatZone (enemies placed later attack inside its radius and
        // its rings show in the Scene view).
        private static void BuildMarkers()
        {
            var markers = new GameObject("Level Markers (placeholders)").transform;
            Zone(markers, "Beetles Fight Zone", T.BeetleZone, T.ZoneRadius);
            Zone(markers, "Mini Boss 2 Fight Zone", T.MiniBossZone, T.ZoneRadius);
            Zone(markers, "Final Boss Fight Zone", T.FinalBossZone, T.FinalBossRadii.y * 1.4f);
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
            T.Generate(); // the ground samples the reachability checks use
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<PlayerGear>() == 1, "Expected one shared Player.");
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<GameSystems>() == 1, "Expected one GameSystems (HUD, menus, pause).");
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<PlayerSpawnPoint>() == 1, "Expected one PlayerSpawnPoint.");
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<Camera>() == 1 && EnemyTestBuilder.Count<AudioListener>() == 1, "Expected one camera and listener.");
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<Ocean>() == 1, "Expected the sea.");
            IslandDock dock = EnemyTestBuilder.All<IslandDock>().SingleOrDefault();
            EnemyTestBuilder.Require(dock != null && dock.DockRole == IslandDock.Role.Arrival && dock.Boat != null, "Expected one arrival dock with its boat.");
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<CaveLighting>() == 1, "Expected one CaveLighting.");
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<TrophyStand>() == 3, "Expected the camp's three trophy stands.");
            IslandCamp camp = EnemyTestBuilder.All<IslandCamp>().SingleOrDefault();
            EnemyTestBuilder.Require(camp != null && camp.Cavendish != null && camp.Merchant != null && camp.CavendishSeat != null && camp.MerchantSeat != null,
                "Expected Cavendish's camp with Cavendish, Eugene and their seats.");
            // Every piece of the camp stands on the ground: none floating, none sunk into it.
            foreach (Transform piece in camp.transform)
            {
                if (piece.GetComponentInChildren<Renderer>() == null || piece.name == "Residents") continue;
                float ground = T.GroundAt(piece.position.x, piece.position.z);
                EnemyTestBuilder.Require(Mathf.Abs(piece.position.y - ground) < 0.08f, $"The camp's {piece.name} is {piece.position.y - ground:+0.00;-0.00} m off the ground.");
            }
            MerchantCrab eugene = EnemyTestBuilder.All<MerchantCrab>().SingleOrDefault();
            EnemyTestBuilder.Require(eugene != null && new SerializedObject(eugene).FindProperty("startFriendly").boolValue, "Expected one friendly Eugene.");
            NavMeshSurface surface = EnemyTestBuilder.All<NavMeshSurface>().Single();
            EnemyTestBuilder.Require(surface.navMeshData != null, "The NavMesh is not baked.");

            List<CrystalBatch> batches = EnemyTestBuilder.All<CrystalBatch>();
            int lights = batches.Count(b => b.GetComponentInChildren<Light>() != null);
            EnemyTestBuilder.Require(batches.All(b => b.CrystalMaterial != null), "A crystal batch has no material.");
            EnemyTestBuilder.Require(lights >= 100 && lights <= 220, $"{lights} crystal lights; expected 100-220 (enough to see by, within Forward+'s light budget).");
            EnemyTestBuilder.Require(batches.Select(b => b.CrystalMaterial).Distinct().Count() == 6, "Expected all six crystal colours.");

            // Everything that matters is reachable on foot from the spawn, including the dock's end.
            Vector3 start = Object.FindAnyObjectByType<PlayerSpawnPoint>().transform.position;
            var goals = new List<(string name, Vector3 at)>
            {
                ("the end of the dock", dock.Landing.position), ("the camp", Ground(T.Camp + new Vector2(0f, -9f))), ("inside the cave mouth", Ground(T.PointOnPath(0.85f))),
                ("the beetles' zone", Ground(T.BeetleZone)), ("mini boss 2's zone", Ground(T.MiniBossZone)), ("the final boss's zone", Ground(T.FinalBossZone))
            };
            foreach (T.Ramp ramp in T.Ramps) goals.Add(($"the foot of the ramp at ({ramp.Edge.x:0}, {ramp.Edge.y:0})", Ground(ramp.Foot)));
            EnemyTestBuilder.Require(NavMesh.SamplePosition(start, out NavMeshHit from, 3f, NavMesh.AllAreas), "The spawn is off the NavMesh.");
            foreach ((string name, Vector3 at) in goals)
            {
                EnemyTestBuilder.Require(NavMesh.SamplePosition(at, out NavMeshHit to, 6f, NavMesh.AllAreas), name + " is off the NavMesh.");
                var path = new NavMeshPath();
                EnemyTestBuilder.Require(NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                    "No walkable path from the spawn to " + name + ".");
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
            // The sea cliffs are grey stone (their own materials, so the tutorial's mountains are untouched).
            Color color = surface switch
            {
                T.Surface.ShelfRock => new Color(0.62f, 0.5f, 0.38f),
                T.Surface.ShelfMoss => new Color(0.43f, 0.53f, 0.3f),
                T.Surface.CoastCliff => new Color(0.52f, 0.4f, 0.3f),
                T.Surface.SeaFloor => new Color(0.5f, 0.46f, 0.36f),
                T.Surface.MountainRock => new Color(0.45f, 0.46f, 0.48f),
                T.Surface.MountainCliff => new Color(0.6f, 0.6f, 0.62f),
                T.Surface.CaveFloor => new Color(0.36f, 0.38f, 0.44f),
                T.Surface.CaveDeep => new Color(0.3f, 0.33f, 0.4f),
                T.Surface.CaveSand => new Color(0.52f, 0.46f, 0.34f),
                T.Surface.CaveWall => new Color(0.3f, 0.31f, 0.37f),
                _ => new Color(0.26f, 0.27f, 0.32f)
            };
            return CrystalAssetBuilder.SimpleMaterial("Ground" + surface, color, 0.06f);
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
