using System;
using System.Collections.Generic;
using System.Linq;
using SignalLost.Effects;
using SignalLost.Enemies;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SignalLost.Editor
{
    /// <summary>
    /// Builds the tutorial level (Scenes/Tutorial/Tutorial Demo.unity, meeting notes 10-22): a green meadow at golden late
    /// afternoon ringed by rocky mountains, with a dense forest belt and inner hills so the player never sees end to
    /// end. The stripped shipwreck lies nose-first in a dirt berm at the end of a gouged trench, burning and smoking
    /// (the ground is dug out under the hull so it never pokes through); Cavendish's campsite sits in a pine-ringed
    /// clearing far to the south-west, with no trail to the wreck, so the player follows its smoke; a reed-fringed pond
    /// lies under the hill to the north-west; the merchant crab's hollow hides behind rocks and bushes far to the
    /// north-east. It also places the Player, GameSystems, spawn point and runtime NavMesh.
    /// The NavMesh is baked here (before any creature is placed) and saved next to the scene. The story (NPCs,
    /// quests, items, enemies) is then added by TutorialStoryInstaller.
    /// Everything is generated from fixed seeds, so rebuilding gives the same level. Rebuilding REPLACES the scene.
    /// Menu: Signal Lost > Tutorial > Rebuild Tutorial Level.
    /// </summary>
    internal static class TutorialLevelBuilder
    {
        public const string ScenePath = "Assets/Scenes/Tutorial/Tutorial Demo.unity";
        private const string TerrainFolder = MeadowAssetBuilder.Folder + "/Terrain";

        private const float MapSize = 320f;
        private const int Cells = 106;              // about 3 m facets
        public const float PlayRadius = 112f;      // the mountains begin beyond this

        // Landmarks (x, z). +Z is north.
        // The camp is about 100 m from the wreck, out of sight behind groves and a rise: the smoke shows the way.
        public static readonly Vector2 Camp = new Vector2(-60f, -64f);
        public static readonly Vector2 Ship = new Vector2(12f, 4f);
        public static readonly Vector2 ShipForward = new Vector2(0.91f, 0.41f).normalized; // the nose points north-east
        public static readonly Vector2 Hollow = new Vector2(66f, 58f);
        public static readonly Vector2 Pond = new Vector2(-60f, 62f);
        public static readonly Vector2 PondRadii = new Vector2(25f, 17f); // half-widths along x and z (longest east-west)
        private const float PondBank = 1.45f;  // where the bank meets the meadow, in pond radii
        private const float PondDepth = 1.1f;  // shallow enough to wade

        /// <summary>Where the opening's two glimpses of being dragged are seen: just behind the wreck, then most of the
        /// way to camp. Kept clear of trees.</summary>
        public static Vector2[] DragPoints => new[] { Ship - ShipForward * 21f, Vector2.Lerp(Ship, Camp, 0.62f) };

        private static readonly Vector4[] Hills =
        {
            // x, z, radius, height: they break the sightlines between the camp, the crash and the hollow.
            new Vector4(-20f, 42f, 22f, 7f), new Vector4(40f, 34f, 15f, 5.5f), new Vector4(-82f, 40f, 24f, 9f),
            new Vector4(22f, -58f, 26f, 8f), new Vector4(-40f, -74f, 22f, 7f), new Vector4(88f, -30f, 20f, 8f),
            new Vector4(-6f, -30f, 14f, 3.5f), new Vector4(48f, 82f, 18f, 7f), new Vector4(95f, 40f, 14f, 6f)
        };

        private static readonly Vector2[] HollowPath = { Ship + ShipForward * 16f, new Vector2(32f, 22f), new Vector2(42f, 37f) }; // fades out short of the hollow

        private enum T { Grass, GrassDark, GoldGrass, Dirt, Scorched, Rock, Cliff }

        [MenuItem("Signal Lost/Tutorial/Rebuild Tutorial Level")]
        public static void BuildFromMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                !EditorUtility.DisplayDialog("Rebuild tutorial level?", "This replaces Scenes/Tutorial/Tutorial Demo.unity, including any hand edits.", "Rebuild", "Cancel"))
                return;
            Build();
        }

        public static void Build()
        {
            CrashSiteWreckBuilder.Build();
            MeadowAssetBuilder.Build();
            CorePrefabBuilder.Build();
            CrabCousinBuilder.Build();
            BaseCampBuilder.Build();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var random = new System.Random(20261022);

            var level = new GameObject("Level").transform;
            try
            {
                // The wreck goes down first, so the ground can be dug out under it (Height consults it from then on).
                Transform ship = PlaceShip(level);
                shipBed = (x, z) => BedUnder(ship, x, z);
                GameObject ground = BuildTerrain(level);
                Physics.SyncTransforms();
                var clear = new List<Vector3>(); // x, z, radius
                Transform crash = BuildCrashSite(ship, clear, random);
                Transform camp = BuildCamp(level, clear);
                Transform hollow = BuildHollow(level, clear, random);
                BuildPond(level, clear, random);
                foreach (Vector2 glimpse in DragPoints) clear.Add(new Vector3(glimpse.x, glimpse.y, 7f));
                Physics.SyncTransforms();
                ScatterNature(level, clear, random);
                BakeNavigation(level);
                BuildFoliage(level, ground, clear);
                BuildLighting();
                Transform gameplay = BuildGameplay(camp);
                TutorialStoryInstaller.Install(level, gameplay, crash, hollow);
            }
            finally
            {
                shipBed = null;
            }

            System.IO.Directory.CreateDirectory("Assets/Scenes/Tutorial");
            EditorSceneManager.SaveScene(scene, ScenePath);
            CoreSceneSetup.SetBuildScenes();
            Debug.Log("TUTORIAL_LEVEL_BUILT");
        }

        // ---------- Height and surface ----------

        private static float Noise(float x, float z, float scale, float seed = 0f) => (Mathf.PerlinNoise(x / scale + 100f + seed, z / scale + 37f + seed) - 0.5f) * 2f;

        /// <summary>Ground height at a point: rolling meadow, hills, the mountain ring, the crash features and the pond,
        /// dug out under the wreck while the level is being built.</summary>
        public static float Height(float x, float z)
        {
            float h = PondBasin(x, z, NaturalHeight(x, z));
            return shipBed != null ? Mathf.Min(h, shipBed(x, z)) : h;
        }

        private static Func<float, float, float> shipBed;

        // Just under the lowest part of the wreck above or beside (x, z), or +infinity away from it. Rays go up from
        // below so they meet the hull's underside; a ring of extra rays catches the edges, since each terrain facet is
        // 3 m across and would otherwise slice through the hull between its corners.
        private static float BedUnder(Transform ship, float x, float z)
        {
            if ((new Vector2(x, z) - Ship).sqrMagnitude > 24f * 24f) return float.PositiveInfinity;
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

        private static float NaturalHeight(float x, float z)
        {
            float h = 1.6f * Noise(x, z, 38f) + 0.7f * Noise(x, z, 14f, 3f);
            foreach (Vector4 hill in Hills)
            {
                float d = new Vector2(x - hill.x, z - hill.y).magnitude / hill.z;
                h += hill.w * Mathf.Exp(-d * d * 2.2f) * (1f + 0.15f * Noise(x, z, 6f, 9f));
            }
            // Mountains: steep and jagged beyond the playable circle.
            float r = new Vector2(x, z).magnitude + 7f * Noise(x, z, 30f, 5f);
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PlayRadius + 4f, PlayRadius + 36f, r));
            h += rise * rise * (34f + 18f * Noise(x, z, 40f, 11f)) + rise * 5f * Noise(x, z, 8f, 13f);

            // The crash: local coordinates along (u) and across (v) the ship.
            Vector2 local = new Vector2(x, z) - Ship;
            float u = Vector2.Dot(local, ShipForward), v = Vector2.Dot(local, new Vector2(ShipForward.y, -ShipForward.x));
            h += 3.4f * Mathf.Exp(-Sq((u - 13f) / 5.5f) - Sq(v / 7f));                                   // dirt piled up at the nose
            h += 1.5f * Mathf.Exp(-Sq((Mathf.Abs(v) - 7f) / 2f)) * Bell(u, -16f, 14f);                     // berms along the flanks
            float trench = Bell(u, -40f, -12f) * Mathf.Clamp01(1f - Mathf.Abs(v) / 4.5f);
            h += -1.4f * trench + 0.6f * Mathf.Exp(-Sq((Mathf.Abs(v) - 5.2f) / 1.2f)) * Bell(u, -40f, -12f); // gouge with lips
            h += -0.6f * Mathf.Exp(-Sq(u / 13f) - Sq(v / 5f));                                             // the bed it lies in

            // Flat clearings: the camp and the hollow (which also dips).
            float hollow = (new Vector2(x, z) - Hollow).magnitude;
            h += -1.8f * Mathf.Exp(-Sq(hollow / 12f));
            float camp = (new Vector2(x, z) - Camp).magnitude;
            h = Mathf.Lerp(h, CampHeight, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(13f, 7f, camp)));
            return h;
        }

        private static float CampHeight => 1.6f * Noise(Camp.x, Camp.y, 38f) + 0.3f;

        // ---------- The pond ----------

        // 0 at the pond's centre, 1 at the waterline, with a slightly wavy shore.
        private static float PondDistance(float x, float z)
        {
            Vector2 local = new Vector2((x - Pond.x) / PondRadii.x, (z - Pond.y) / PondRadii.y);
            return local.magnitude * (1f + 0.07f * Noise(x, z, 9f, 23f));
        }

        // The water sits a little below the lowest point of the ground ringing the basin, so it never spills over.
        private static float? pondWater;
        public static float PondWater
        {
            get
            {
                if (pondWater.HasValue) return pondWater.Value;
                float lowest = float.PositiveInfinity;
                for (int i = 0; i < 32; i++)
                {
                    float angle = i * Mathf.PI * 2f / 32f;
                    lowest = Mathf.Min(lowest, NaturalHeight(Pond.x + Mathf.Cos(angle) * PondRadii.x * 1.3f, Pond.y + Mathf.Sin(angle) * PondRadii.y * 1.3f));
                }
                pondWater = lowest - 0.35f;
                return pondWater.Value;
            }
        }

        // A bowl under the water, then a bank that rises from just above the waterline back to the meadow.
        private static float PondBasin(float x, float z, float h)
        {
            float e = PondDistance(x, z);
            if (e >= PondBank) return h;
            if (e <= 1f) return PondWater - 0.25f - PondDepth * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 0.35f, e));
            float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, PondBank, e));
            return Mathf.Lerp(PondWater + 0.25f, Mathf.Max(h, PondWater + 0.25f), t);
        }
        private static float Sq(float value) => value * value;
        private static float Bell(float value, float from, float to) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from - 4f, from, value)) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(to + 4f, to, value));

        private static float DistanceToPath(Vector2 p, Vector2[] path)
        {
            float best = float.MaxValue;
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector2 a = path[i], b = path[i + 1], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, (p - (a + ab * t)).magnitude);
            }
            return best;
        }

        private static T Surface(Vector3 centre, Vector3 normal)
        {
            Vector2 p = new Vector2(centre.x, centre.z);
            Vector2 local = p - Ship;
            float u = Vector2.Dot(local, ShipForward), v = Vector2.Dot(local, new Vector2(ShipForward.y, -ShipForward.x));
            if (normal.y < 0.6f) return T.Cliff;
            if (normal.y < 0.82f) return T.Rock;
            if (p.magnitude > PlayRadius + 14f) return centre.y > 14f ? T.Rock : T.GrassDark;
            bool nearShip = Mathf.Abs(u) < 18f && Mathf.Abs(v) < 9f + Noise(p.x, p.y, 3f) * 2f;
            bool inTrench = u > -42f && u < -10f && Mathf.Abs(v) < 5.5f + Noise(p.x, p.y, 3f) * 1.5f;
            if (inTrench || nearShip) return Mathf.Abs(v) < 3.5f || (nearShip && Mathf.Abs(u) < 12f) ? T.Scorched : T.Dirt;
            if (PondDistance(p.x, p.y) < 1.12f + Noise(p.x, p.y, 4f, 29f) * 0.06f) return T.Dirt; // the bed and a muddy shore
            if ((p - Camp).magnitude < 6.5f || DistanceToPath(p, HollowPath) < 1.4f) return T.Dirt;
            if ((p - Hollow).magnitude < 7f) return T.Dirt;
            float patch = Noise(p.x, p.y, 22f, 17f);
            return patch > 0.25f ? T.GoldGrass : patch < -0.2f ? T.GrassDark : T.Grass;
        }

        private static GameObject BuildTerrain(Transform level)
        {
            float step = MapSize / Cells, origin = -MapSize * 0.5f;
            var heights = new float[Cells + 1, Cells + 1];
            for (int i = 0; i <= Cells; i++)
            for (int j = 0; j <= Cells; j++)
                heights[i, j] = Height(origin + i * step, origin + j * step);

            var kit = new FlatMeshKit<T>();
            for (int i = 0; i < Cells; i++)
            for (int j = 0; j < Cells; j++)
            {
                Vector3 a = new Vector3(origin + i * step, heights[i, j], origin + j * step);
                Vector3 b = new Vector3(origin + (i + 1) * step, heights[i + 1, j], origin + j * step);
                Vector3 c = new Vector3(origin + (i + 1) * step, heights[i + 1, j + 1], origin + (j + 1) * step);
                Vector3 d = new Vector3(origin + i * step, heights[i, j + 1], origin + (j + 1) * step);
                // Alternate the diagonal for a less regular facet pattern.
                bool flip = (i + j) % 2 == 0;
                AddFacet(kit, a, flip ? b : d, c);
                AddFacet(kit, a, c, flip ? d : b);
            }
            Mesh mesh = SaveTerrainMesh(kit.Build("TutorialMeadowTerrain"));

            var ground = new GameObject("Terrain");
            ground.transform.SetParent(level, false);
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = ground.AddComponent<MeshRenderer>();
            var materials = new Material[mesh.subMeshCount];
            for (int s = 0; s < materials.Length; s++) materials[s] = TerrainMaterial(FlatMeshKit<T>.MaterialOf(mesh, s));
            renderer.sharedMaterials = materials;
            ground.AddComponent<MeshCollider>().sharedMesh = mesh;
            ground.isStatic = true;
            return ground;
        }

        private static void AddFacet(FlatMeshKit<T> kit, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            if (normal.y < 0f) normal = -normal;
            kit.Tri(a, b, c, normal, Surface((a + b + c) / 3f, normal));
        }

        private static Material TerrainMaterial(T surface)
        {
            Color color = surface switch
            {
                T.Grass => new Color(0.45f, 0.64f, 0.24f),
                T.GrassDark => new Color(0.29f, 0.48f, 0.2f),
                T.GoldGrass => new Color(0.7f, 0.67f, 0.28f),
                T.Dirt => new Color(0.55f, 0.4f, 0.25f),
                T.Scorched => new Color(0.2f, 0.15f, 0.12f),
                T.Rock => new Color(0.55f, 0.51f, 0.47f),
                _ => new Color(0.66f, 0.61f, 0.56f)
            };
            System.IO.Directory.CreateDirectory(TerrainFolder);
            AssetDatabase.Refresh();
            string path = TerrainFolder + "/Ground" + surface + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.08f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mesh SaveTerrainMesh(Mesh mesh)
        {
            System.IO.Directory.CreateDirectory(TerrainFolder);
            AssetDatabase.Refresh();
            string path = TerrainFolder + "/TutorialMeadowTerrain.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            string order = FlatMeshKit<T>.Order(mesh);
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = "TutorialMeadowTerrain";
            FlatMeshKit<T>.Remember(existing, order);
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            return existing;
        }

        // ---------- Landmarks ----------

        // The wreck, on the undug ground; BuildTerrain then digs its bed out (see BedUnder).
        private static Transform PlaceShip(Transform level)
        {
            var site = new GameObject("Crash Site").transform;
            site.SetParent(level, false);
            Quaternion facing = Quaternion.LookRotation(new Vector3(ShipForward.x, 0f, ShipForward.y));
            var ship = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CrashSiteWreckBuilder.ShipPath), site);
            ship.transform.SetPositionAndRotation(Ground(Ship) + Vector3.down * 0.4f, facing);
            Physics.SyncTransforms();
            return ship.transform;
        }

        private static Transform BuildCrashSite(Transform ship, List<Vector3> clear, System.Random random)
        {
            Transform site = ship.parent;
            Transform hull = ship.Find("Hull");

            // Fire and smoke on and around the wreck.
            Effect("ElectricalFire", site, hull.TransformPoint(new Vector3(0.4f, 2.2f, -2f)), 1f, "Reactor Bay Fire");
            Effect("ElectricalFire", site, hull.TransformPoint(new Vector3(0f, 2.4f, -11f)), 1f, "Engine Bay Fire");
            Effect("ElectricalFire", site, hull.TransformPoint(new Vector3(0f, 4.3f, 8.4f)), 0.7f, "Cockpit Sparks");
            Effect("SmokePlume", site, hull.TransformPoint(new Vector3(0.4f, 3.5f, -2f)), 1f, "Smoke Plume");
            // A failing red emergency lamp lights the cockpit, where the mechanic comes to.
            var lamp = new GameObject("Cockpit Emergency Light").AddComponent<Light>();
            lamp.transform.SetParent(site, false);
            lamp.transform.position = hull.TransformPoint(new Vector3(0f, 4.5f, 7.3f));
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.25f, 0.15f);
            lamp.intensity = 3f;
            lamp.range = 5f;
            lamp.shadows = LightShadows.None;
            var flicker = new SerializedObject(lamp.gameObject.AddComponent<FlickerLight>());
            flicker.FindProperty("electrical").boolValue = true;
            flicker.FindProperty("strength").floatValue = 0.6f;
            flicker.ApplyModifiedPropertiesWithoutUndo();
            Effect("SmokePlume", site, hull.TransformPoint(new Vector3(0f, 3.5f, -11.5f)), 0.6f, "Engine Smoke");
            Vector2 back = -ShipForward, side = new Vector2(ShipForward.y, -ShipForward.x);
            foreach ((float along, float across) in new[] { (17f, 2.5f), (25f, -1.5f), (33f, 1f) })
                Effect("ElectricalFire", site, Ground(Ship + back * along + side * across), 0.6f, "Trench Fire");

            clear.Add(new Vector3(Ship.x, Ship.y, 15f));
            for (float along = 14f; along <= 40f; along += 6f) clear.Add(new Vector3(Ship.x + back.x * along, Ship.y + back.y * along, 6f));

            // Junk thrown along the trench and around the ship, and clods of dirt on the berm.
            string[] junk = { "HullPanel", "HullPanelSmall", "HullPanelSmall", "Crate", "Barrel", "Pipe", "Rib", "Seat", "Gear", "Gear", "CableBundle", "Strut", "HullChunk", "ScorchMark", "ScorchMark" };
            var pieces = new GameObject("Wreckage").transform;
            pieces.SetParent(site, false);
            for (int n = 0, tries = 0; n < 140 && tries < 2000; tries++)
            {
                float along = Range(random, -46f, 22f), across = Range(random, -16f, 16f) * (along < -14f ? 0.6f : 1f);
                if (Mathf.Abs(along) < 14f && Mathf.Abs(across) < 6f) continue; // not inside the hull
                string name = junk[random.Next(junk.Length)];
                Vector2 p = Ship + ShipForward * along + side * across;
                Place(CrashSiteWreckBuilder.DebrisPath(name), pieces, Ground(p), Quaternion.Euler(Range(random, -8f, 8f), Range(random, 0f, 360f), Range(random, -8f, 8f)), Range(random, 0.8f, 1.25f), true);
                n++;
            }
            for (int n = 0; n < 28; n++)
            {
                float angle = Range(random, -1.4f, 1.4f);
                Vector2 p = Ship + ShipForward * (13f + Mathf.Cos(angle) * Range(random, 4f, 8f)) + side * Mathf.Sin(angle) * Range(random, 5f, 9f);
                Place(MeadowAssetBuilder.PrefabPath(n % 3 == 0 ? "RockB" : "RockC"), pieces, Ground(p) + Vector3.down * 0.2f, Quaternion.Euler(0f, Range(random, 0f, 360f), 0f), Range(random, 0.8f, 1.6f), true);
            }
            return site;
        }

        private static Transform BuildCamp(Transform level, List<Vector3> clear)
        {
            // The base camp is one prefab, the same in every level (BaseCampBuilder); its trophy stands remember trophies.
            Transform camp = Place(BaseCampBuilder.PrefabPath, level, Ground(Camp), Quaternion.identity, 1f, false).transform;
            camp.name = "Campsite";
            clear.Add(new Vector3(Camp.x, Camp.y, 12f));
            return camp;
        }

        private static Transform BuildHollow(Transform level, List<Vector3> clear, System.Random random)
        {
            var hollow = new GameObject("Merchant Hollow").transform;
            hollow.SetParent(level, false);
            hollow.position = Ground(Hollow);
            var zone = hollow.gameObject.AddComponent<CombatZone>();
            zone.Radius = 14f;
            // A tight ring of tall rocks, bushes and pines hides it. The trail from the crash site fades out short of it,
            // and the only gap faces away from the crash, so the player has to search around (or follow the scanner).
            Vector2 entry = new Vector2(1f, -0.3f).normalized;
            for (int i = 0; i < 22; i++)
            {
                float angle = i * Mathf.PI * 2f / 22f;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                if (Vector2.Dot(dir, entry) > 0.93f) continue;
                Vector2 p = Hollow + dir * Range(random, 15f, 17f);
                string rock = i % 2 == 0 ? "BoulderTall" : "Boulder";
                Place(MeadowAssetBuilder.PrefabPath(rock), hollow, Ground(p) + Vector3.down * 0.3f, Quaternion.Euler(0f, Range(random, 0f, 360f), 0f), Range(random, 1.2f, 1.7f), false);
                Place(MeadowAssetBuilder.PrefabPath(i % 3 == 0 ? "BushB" : "BushA"), hollow, Ground(Hollow + dir * Range(random, 17.5f, 19.5f)), Quaternion.Euler(0f, Range(random, 0f, 360f), 0f), Range(random, 1.4f, 2f), false);
                if (i % 2 == 1 && Vector2.Dot(dir, entry) < 0.8f)
                    Place(MeadowAssetBuilder.PrefabPath(MeadowAssetBuilder.Trees[random.Next(3)]), hollow, Ground(Hollow + dir * Range(random, 20f, 23f)) + Vector3.down * 0.15f, Quaternion.Euler(0f, Range(random, 0f, 360f), 0f), Range(random, 1f, 1.3f), false);
            }
            clear.Add(new Vector3(Hollow.x, Hollow.y, 22f));
            return hollow;
        }

        // A pond in its own basin under the north-west hill: stylized water (Shaders/StylizedWater.shader), reeds and
        // cattails along the shore, lily pads, rocks on the banks, and bushes and trees ringing it, open toward the
        // meadow so it can be seen from afar. Shallow enough to wade.
        private static void BuildPond(Transform level, List<Vector3> clear, System.Random random)
        {
            var pond = new GameObject("Pond").transform;
            pond.SetParent(level, false);
            pond.position = new Vector3(Pond.x, PondWater, Pond.y);

            var water = new GameObject("Pond Water");
            water.transform.SetParent(pond, false);
            water.AddComponent<MeshFilter>().sharedMesh = SavePondMesh(BuildWaterMesh());
            var renderer = water.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = WaterMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Points around the shore at a given distance (in pond radii), following the same wavy outline as the basin.
            Vector2 Around(float angle, float e)
            {
                Vector2 nominal = Pond + new Vector2(Mathf.Cos(angle) * PondRadii.x, Mathf.Sin(angle) * PondRadii.y);
                float wobble = 1f + 0.07f * Noise(nominal.x, nominal.y, 9f, 23f);
                return Pond + new Vector2(Mathf.Cos(angle) * PondRadii.x, Mathf.Sin(angle) * PondRadii.y) * (e / wobble);
            }
            Vector2 meadow = (-Pond).normalized; // the side facing the middle of the map stays open

            var plants = new GameObject("Reeds And Lilies").transform;
            plants.SetParent(pond, false);
            for (float angle = 0f; angle < Mathf.PI * 2f; angle += 0.11f)
            {
                Vector2 nominal = Around(angle, 1f);
                if (Noise(nominal.x, nominal.y, 7f, 31f) > 0.3f) continue; // gaps between the reed beds
                Vector2 at = Around(angle + Range(random, -0.04f, 0.04f), Range(random, 0.94f, 1.06f));
                Place(MeadowAssetBuilder.PrefabPath(MeadowAssetBuilder.ReedClumps[random.Next(2)]), plants, Ground(at), Quaternion.Euler(0f, Range(random, 0f, 360f), 0f), Range(random, 0.8f, 1.3f), false);
            }
            for (int i = 0; i < 10; i++)
            {
                Vector2 at = Around(Range(random, 0f, Mathf.PI * 2f), Range(random, 0.3f, 0.8f));
                Place(MeadowAssetBuilder.PrefabPath("LilyPads"), plants, new Vector3(at.x, PondWater + 0.04f, at.y), Quaternion.Euler(0f, Range(random, 0f, 360f), 0f), Range(random, 0.8f, 1.4f), false);
            }

            var banks = new GameObject("Banks").transform;
            banks.SetParent(pond, false);
            for (int i = 0; i < 14; i++)
            {
                float angle = Range(random, 0f, Mathf.PI * 2f);
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                bool big = Vector2.Dot(dir, meadow) < -0.3f && i % 3 == 0; // boulders on the hill side
                Vector2 at = Around(angle, Range(random, 1.04f, 1.3f));
                string rock = big ? MeadowAssetBuilder.Rocks[3 + random.Next(2)] : MeadowAssetBuilder.Rocks[random.Next(3)];
                Place(MeadowAssetBuilder.PrefabPath(rock), banks, Ground(at) + Vector3.down * (big ? 0.4f : 0.12f), Quaternion.Euler(0f, Range(random, 0f, 360f), 0f), Range(random, 0.9f, big ? 1.4f : 2f), false);
            }
            for (int i = 0; i < 18; i++)
            {
                float angle = Range(random, 0f, Mathf.PI * 2f);
                Vector2 at = Around(angle, Range(random, 1.3f, 1.65f));
                Place(MeadowAssetBuilder.PrefabPath(MeadowAssetBuilder.Bushes[random.Next(2)]), banks, Ground(at), Quaternion.Euler(0f, Range(random, 0f, 360f), 0f), Range(random, 0.9f, 1.6f), false);
            }
            var trees = new GameObject("Pond Trees").transform;
            trees.SetParent(pond, false);
            for (int i = 0; i < 26; i++)
            {
                float angle = i * Mathf.PI * 2f / 26f + Range(random, -0.08f, 0.08f);
                if (Vector2.Dot(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), meadow) > 0.55f) continue;
                Vector2 at = Around(angle, Range(random, 1.6f, 2.1f));
                string kind = MeadowAssetBuilder.Trees[random.NextDouble() < 0.55 ? random.Next(3) : 3 + random.Next(3)];
                Place(MeadowAssetBuilder.PrefabPath(kind), trees, Ground(at) + Vector3.down * 0.15f, Quaternion.Euler(0f, Range(random, 0f, 360f), 0f), Range(random, 0.9f, 1.3f), false);
                clear.Add(new Vector3(at.x, at.y, 2.5f)); // the scattered forest keeps off these
            }
        }

        // The water surface: a flat grid clipped to just past the waterline (the bank hides its edge). Each vertex's
        // red colour is how close it is to the shore, which the water shader uses for depth colour, calm and foam.
        private static Mesh BuildWaterMesh()
        {
            const float cell = 1.5f;
            Vector2 extent = PondRadii * 1.3f;
            int nx = Mathf.CeilToInt(extent.x * 2f / cell), nz = Mathf.CeilToInt(extent.y * 2f / cell);
            var vertices = new List<Vector3>();
            var colours = new List<Color>();
            var index = new int[nx + 1, nz + 1];
            var e = new float[nx + 1, nz + 1];
            for (int i = 0; i <= nx; i++)
            for (int k = 0; k <= nz; k++)
            {
                float x = -extent.x + i * cell, z = -extent.y + k * cell;
                e[i, k] = PondDistance(Pond.x + x, Pond.y + z);
                index[i, k] = vertices.Count;
                vertices.Add(new Vector3(x, 0f, z));
                colours.Add(new Color(Mathf.InverseLerp(0.55f, 1f, e[i, k]), 0f, 0f, 1f));
            }
            var triangles = new List<int>();
            for (int i = 0; i < nx; i++)
            for (int k = 0; k < nz; k++)
            {
                if (e[i, k] > 1.25f || e[i + 1, k] > 1.25f || e[i + 1, k + 1] > 1.25f || e[i, k + 1] > 1.25f) continue;
                triangles.AddRange(new[] { index[i, k], index[i, k + 1], index[i + 1, k + 1], index[i, k], index[i + 1, k + 1], index[i + 1, k] });
            }
            var mesh = new Mesh { name = "PondWater" };
            mesh.SetVertices(vertices);
            mesh.SetColors(colours);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh SavePondMesh(Mesh mesh)
        {
            System.IO.Directory.CreateDirectory(TerrainFolder);
            AssetDatabase.Refresh();
            string path = TerrainFolder + "/PondWater.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = "PondWater";
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            return existing;
        }

        // The stylized water shader, tuned toward the reference's clear turquoise; plain transparent Lit if it is missing.
        private static Material WaterMaterial()
        {
            string path = MeadowAssetBuilder.Folder + "/Materials/PondWater.mat";
            Shader shader = Shader.Find("Signal Lost/Stylized Water");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || (shader != null && material.shader != shader))
            {
                if (material != null) AssetDatabase.DeleteAsset(path);
                material = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            if (shader != null)
            {
                material.SetColor("_DeepColor", new Color(0.05f, 0.4f, 0.6f, 0.93f));
                material.SetColor("_ShallowColor", new Color(0.24f, 0.72f, 0.74f, 0.7f));
                material.SetColor("_HorizonColor", new Color(0.86f, 0.8f, 0.68f, 1f)); // the golden-hour sky
            }
            else
            {
                Debug.LogWarning("Signal Lost/Stylized Water shader not found; the pond uses plain transparent Lit.");
                material.SetColor("_BaseColor", new Color(0.1f, 0.5f, 0.65f, 0.75f));
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

        // ---------- Trees, rocks, bushes ----------

        private static void ScatterNature(Transform level, List<Vector3> clear, System.Random random)
        {
            var forest = new GameObject("Forest").transform;
            forest.SetParent(level, false);
            var rocks = new GameObject("Rocks").transform;
            rocks.SetParent(level, false);
            var bushes = new GameObject("Bushes").transform;
            bushes.SetParent(level, false);
            var trunks = new List<Vector2>();

            bool Blocked(Vector2 p, float pad)
            {
                foreach (Vector3 c in clear) if ((p - new Vector2(c.x, c.y)).magnitude < c.z + pad) return true;
                return DistanceToPath(p, HollowPath) < 3f + pad || PondDistance(p.x, p.y) < PondBank + pad / PondRadii.y;
            }

            void Tree(Vector2 p, bool pinesOnly)
            {
                if (Blocked(p, 1f) || trunks.Any(t => (t - p).sqrMagnitude < 9f)) return;
                Vector3 at = Ground(p);
                if (at.y > 18f) return; // not up on the mountains
                double roll = random.NextDouble();
                string kind = pinesOnly || roll < 0.6 ? MeadowAssetBuilder.Trees[random.Next(3)] : MeadowAssetBuilder.Trees[3 + random.Next(3)];
                Place(MeadowAssetBuilder.PrefabPath(kind), forest, at + Vector3.down * 0.15f, Quaternion.Euler(0f, Range(random, 0f, 360f), 0f), Range(random, 0.85f, 1.35f), false);
                trunks.Add(p);
            }

            // A thick belt of forest in front of the mountains: it walls the meadow in and hides how small it is.
            for (int i = 0; i < 1400; i++)
            {
                float angle = Range(random, 0f, Mathf.PI * 2f), r = Range(random, 90f, PlayRadius + 16f);
                Tree(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r, r > PlayRadius - 4f);
            }
            // Groves inside the meadow, where the noise says so.
            for (int i = 0; i < 2600; i++)
            {
                Vector2 p = new Vector2(Range(random, -88f, 88f), Range(random, -88f, 88f));
                if (p.magnitude > 90f || Noise(p.x, p.y, 26f, 21f) < 0.15f) continue;
                Tree(p, false);
            }
            // A ring of pines around the camp, open toward the crash site.
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2f / 16f;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                if (Vector2.Dot(dir, (Ship - Camp).normalized) > 0.75f) continue;
                Vector2 p = Camp + dir * Range(random, 14f, 19f);
                if (trunks.Any(t => (t - p).sqrMagnitude < 9f)) continue;
                Place(MeadowAssetBuilder.PrefabPath(MeadowAssetBuilder.Trees[random.Next(3)]), forest, Ground(p) + Vector3.down * 0.15f, Quaternion.Euler(0f, Range(random, 0f, 360f), 0f), Range(random, 1f, 1.3f), false);
                trunks.Add(p);
            }

            for (int i = 0, placed = 0; i < 1200 && placed < 300; i++)
            {
                Vector2 p = new Vector2(Range(random, -105f, 105f), Range(random, -105f, 105f));
                if (p.magnitude > PlayRadius || Blocked(p, 0f)) continue;
                Place(MeadowAssetBuilder.PrefabPath(MeadowAssetBuilder.Bushes[random.Next(2)]), bushes, Ground(p), Quaternion.Euler(0f, Range(random, 0f, 360f), 0f), Range(random, 0.8f, 1.6f), false);
                placed++;
            }
            for (int i = 0, placed = 0; i < 1500 && placed < 320; i++)
            {
                Vector2 p = new Vector2(Range(random, -110f, 110f), Range(random, -110f, 110f));
                if (p.magnitude > PlayRadius + 6f || Blocked(p, -2f)) continue;
                bool big = random.NextDouble() < 0.15;
                string kind = big ? MeadowAssetBuilder.Rocks[3 + random.Next(2)] : MeadowAssetBuilder.Rocks[random.Next(3)];
                Place(MeadowAssetBuilder.PrefabPath(kind), rocks, Ground(p) + Vector3.down * (big ? 0.4f : 0.1f), Quaternion.Euler(0f, Range(random, 0f, 360f), 0f), Range(random, 0.7f, big ? 1.6f : 1.8f), false);
                placed++;
            }
        }

        private static void BuildFoliage(Transform level, GameObject ground, List<Vector3> clear)
        {
            var foliage = new GameObject("Grass And Flowers");
            foliage.transform.SetParent(level, false);
            foreach (Vector3 c in clear)
            {
                // Keep the camp floor, the ship bed and the trench bare; the hollow and the pond's banks can stay grassy.
                if ((new Vector2(c.x, c.y) - Hollow).magnitude < 1f || PondDistance(c.x, c.y) < PondBank + 0.6f) continue;
                var marker = new GameObject("Clear");
                marker.transform.SetParent(foliage.transform, false);
                marker.transform.position = new Vector3(c.x, 0f, c.y);
                marker.AddComponent<SphereCollider>().radius = (new Vector2(c.x, c.y) - Camp).magnitude < 1f ? 5f : c.z * 0.8f;
            }
            // The pond's water stays bare (its banks keep their grass).
            for (float x = Pond.x - PondRadii.x; x <= Pond.x + PondRadii.x; x += 4f)
                for (float z = Pond.y - PondRadii.y; z <= Pond.y + PondRadii.y; z += 4f)
                {
                    if (PondDistance(x, z) > 1.05f) continue;
                    var marker = new GameObject("Clear Pond");
                    marker.transform.SetParent(foliage.transform, false);
                    marker.transform.position = new Vector3(x, 0f, z);
                    marker.AddComponent<SphereCollider>().radius = 3.2f;
                }
            // The worn trail toward the hollow stays bare too (there is none between camp and the wreck).
            foreach (Vector2[] path in new[] { HollowPath })
                for (int i = 0; i < path.Length - 1; i++)
                    for (float d = 0f, length = (path[i + 1] - path[i]).magnitude; d < length; d += 2.5f)
                    {
                        Vector2 p = Vector2.Lerp(path[i], path[i + 1], d / length);
                        var marker = new GameObject("Clear Path");
                        marker.transform.SetParent(foliage.transform, false);
                        marker.transform.position = new Vector3(p.x, 0f, p.y);
                        marker.AddComponent<SphereCollider>().radius = 1.5f;
                    }
            var scatter = foliage.AddComponent<FoliageScatter>();
            var data = new SerializedObject(scatter);
            data.FindProperty("ground").objectReferenceValue = ground.GetComponent<Collider>();
            (string name, int count, Vector2 scale)[] items =
            {
                ("GrassTuft", 5200, new Vector2(0.8f, 1.6f)), ("GrassTuftDry", 2000, new Vector2(0.8f, 1.5f)),
                ("FlowersYellow", 650, new Vector2(0.8f, 1.2f)), ("FlowersWhite", 500, new Vector2(0.8f, 1.2f)), ("FlowersPink", 420, new Vector2(0.8f, 1.2f))
            };
            SerializedProperty list = data.FindProperty("items");
            list.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MeadowAssetBuilder.PrefabPath(items[i].name));
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("mesh").objectReferenceValue = prefab.GetComponent<MeshFilter>().sharedMesh;
                Material[] materials = prefab.GetComponent<MeshRenderer>().sharedMaterials;
                SerializedProperty mats = entry.FindPropertyRelative("materials");
                mats.arraySize = materials.Length;
                for (int m = 0; m < materials.Length; m++) mats.GetArrayElementAtIndex(m).objectReferenceValue = materials[m];
                entry.FindPropertyRelative("count").intValue = items[i].count;
                entry.FindPropertyRelative("scale").vector2Value = items[i].scale;
            }
            data.FindProperty("radius").floatValue = PlayRadius + 4f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Lighting and gameplay ----------

        // Golden late afternoon: a warm low sun, a soft procedural sky and haze that swallows the far forest.
        private static void BuildLighting()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.8f, 0.55f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(24f, 70f, 0f);
            RenderSettings.sun = sun;

            string skyPath = MeadowAssetBuilder.Folder + "/Materials/GoldenHourSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            sky.SetFloat("_SunSize", 0.06f);
            sky.SetFloat("_SunSizeConvergence", 6f);
            sky.SetFloat("_AtmosphereThickness", 1.35f);
            sky.SetColor("_SkyTint", new Color(0.6f, 0.62f, 0.75f));
            sky.SetColor("_GroundColor", new Color(0.42f, 0.4f, 0.32f));
            sky.SetFloat("_Exposure", 1.25f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.66f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.78f, 0.64f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.32f, 0.2f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.86f, 0.73f, 0.56f);
            RenderSettings.fogStartDistance = 35f;
            RenderSettings.fogEndDistance = 210f;
        }

        private static Transform BuildGameplay(Transform camp)
        {
            var gameplay = new GameObject("Gameplay").transform;
            // The player wakes beside the fire, facing it; Cavendish sits across from them (story installer).
            Vector3 spawnAt = Ground(Camp + new Vector2(-2.6f, -0.4f));
            Quaternion spawnFacing = Quaternion.Euler(0f, 90f, 0f);
            var spawn = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CorePrefabBuilder.SpawnPath));
            spawn.transform.SetParent(gameplay, false);
            spawn.transform.SetPositionAndRotation(spawnAt, spawnFacing);
            spawn.name = "Spawn At Camp";
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CorePrefabBuilder.PlayerPath));
            player.transform.SetPositionAndRotation(spawnAt + Vector3.up * 0.05f, spawnFacing);
            player.GetComponentInChildren<Camera>(true).clearFlags = CameraClearFlags.Skybox; // the shared player clears to a flat colour
            // Amanda's scanner rework: the tutorial's scene-wide scanner range (signal strength and creature pickup).
            var scanner = new SerializedObject(player.GetComponentInChildren<SignalLost.Scanner.ScannerController>(true));
            scanner.FindProperty("levelRangeOverride").floatValue = 200f;
            scanner.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CorePrefabBuilder.SystemsPath));
            return gameplay;
        }

        // Baked in the editor from the ground, rocks, trees and wreck only, so creatures and props never punch
        // holes in it. NavMeshBaker would rebuild it at runtime if the data went missing.
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
            System.IO.Directory.CreateDirectory("Assets/Scenes/Tutorial");
            const string path = "Assets/Scenes/Tutorial/TutorialNavMesh.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(surface.navMeshData, path);
        }

        // ---------- Helpers ----------

        public static Vector3 Ground(Vector2 p) => new Vector3(p.x, Height(p.x, p.y), p.y);

        private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        private static GameObject Place(string prefabPath, Transform parent, Vector3 position, Quaternion rotation, float scale, bool alignToGround)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (asset == null) throw new InvalidOperationException("Missing prefab " + prefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            if (alignToGround && Physics.Raycast(position + Vector3.up * 30f, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore) && hit.collider is MeshCollider)
                rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * rotation;
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = Vector3.one * scale;
            return instance;
        }

        private static void Effect(string name, Transform parent, Vector3 position, float scale, string label)
        {
            GameObject effect = Place(MeadowAssetBuilder.EffectPath(name), parent, position, Quaternion.identity, scale, false);
            effect.name = label;
            foreach (ParticleSystem system in effect.GetComponentsInChildren<ParticleSystem>())
            {
                var main = system.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
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
