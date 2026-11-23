using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace SignalLost.Editor
{
    /// <summary>
    /// The desert revamp's landforms (docs/desert-revamp.md, 11-22): Rattlesnake Canyon cut through a raised plateau in
    /// the north-west, spanned by the Big Arch; the Dry Wash running from the canyon mouth down to Mirage Springs, an
    /// oasis in a sandstone hollow; the Hoodoos with their lookout rock in the west; Table Mesa in the north-east; and the
    /// salt flat round the Scorch Raceway (the Dustbowl Speedway). The three arenas, the worm's bowl and the lair are
    /// untouched. The shapes are part of the height field; their sheer faces, the rock formations and the trees are
    /// Blender models (tools/blender/desert_scenery.py) placed on top.
    /// </summary>
    public static partial class DesertDunesBuilder
    {
        private const string ModelFolder = Root + "/Models";

        // ---------- Layout (metres; +Z is north) ----------

        // Rattlesnake plateau: a raised table in the north-west, a cliff all round except two long ramps.
        private static readonly Vector2 PlateauCentre = new Vector2(-118f, 108f); // (clear of the west beach, which flattened its edge)
        private static readonly Vector2 PlateauRadii = new Vector2(54f, 38f);
        private const float PlateauAngle = 22f;
        /// <summary>The plateau's top, above sea level.</summary>
        public const float PlateauTop = 20f; // (11-22, "make the canyon deeper": was 12) the canyon is about 19 m deep
        private const float CliffWidth = 3f, RampLength = 62f;
        private static readonly float[] PlateauRamps = { -10f, 238f }; // bearings (degrees, 0 = +X, counter-clockwise)

        // The canyon: a winding floor 14 m wide from the west beach through the plateau to the Buried Hull.
        public static readonly Vector2[] Canyon =
        {
            new Vector2(-206f, 128f), new Vector2(-176f, 124f), new Vector2(-156f, 106f), new Vector2(-136f, 112f),
            new Vector2(-114f, 100f), new Vector2(-96f, 82f), new Vector2(-80f, 72f)
        };
        /// <summary>Half the carved floor's width; the wall slabs stand on its edge, leaving about 14 m between their faces.</summary>
        public const float CanyonHalf = 11.5f; // the floor is carved well behind the wall faces, so no slope shows in front of them
        private const float CanyonWallWidth = 3f;
        /// <summary>Where each canyon wall's face meets the floor, from the centre line (about 15 m of floor between them).</summary>
        private const float CanyonWallFoot = 7.5f;
        /// <summary>Where the Big Arch spans the canyon (on the leg from Canyon[2] to Canyon[3]).</summary>
        public static Vector2 ArchSpot => Vector2.Lerp(Canyon[2], Canyon[3], 0.55f);

        // The Dry Wash: an old riverbed from the canyon mouth down to the oasis.
        public static readonly Vector2[] Wash =
        {
            new Vector2(-80f, 44f), new Vector2(-102f, 14f), new Vector2(-116f, -24f), new Vector2(-120f, -62f), new Vector2(-114f, -100f)
        };
        private const float WashHalf = 5f, WashBank = 5f, WashDepth = 2.2f;

        // Mirage Springs: a sandstone hollow with a spring-fed pond.
        public static readonly Vector2 OasisCentre = new Vector2(-112f, -124f);
        private const float OasisRadius = 26f, OasisDepth = 4.5f, PondRadius = 11f;

        // The Hoodoos, and the lookout rock among them (a small table with one ramp).
        public static readonly Vector2 HoodooCentre = new Vector2(-154f, -8f);
        private const float HoodooRadius = 32f;
        public static readonly Vector2 LookoutCentre = new Vector2(-146f, -2f);
        private const float LookoutRadius = 9f, LookoutRise = 7f, LookoutRamp = 50f;

        // Table Mesa: a big flat-topped mesa in the north-east with one ramp up its south-west side.
        public static readonly Vector2 TableMesaCentre = new Vector2(126f, 112f);
        private static readonly Vector2 TableMesaRadii = new Vector2(30f, 26f);
        private const float TableMesaTop = 16f, TableMesaRamp = 225f;

        // The salt flat east of the Scorch Raceway: the Dustbowl Speedway's grounds.
        public static readonly Vector2 SaltCentre = new Vector2(116f, -52f);
        private static readonly Vector2 SaltRadii = new Vector2(44f, 34f);

        /// <summary>
        /// Puts the revamped terrain into DesertDunes.unity: regenerates the environment, camp, trophies and lair and
        /// reinstalls the story (all from the builders, as Install Desert Story does), then moves the player to the new
        /// spawn on the south beach (they respawn where they start).
        /// </summary>
        [MenuItem("Signal Lost/Desert/Apply Desert Revamp Terrain")]
        public static void ApplyRevampTerrain()
        {
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Apply the desert revamp terrain",
                    "This regenerates DesertEnvironment.prefab, the desert camp, trophies and CorewormLair.prefab, reinstalls the desert " +
                    "story in DesertDunes.unity and moves the player to the new spawn. Edits made to those prefabs are lost. Continue?",
                    "Apply", "Cancel"))
                return;
            if (!Application.isBatchMode && !UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            InstallStory();
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
            foreach (SignalLost.Player.FirstPersonController player in Object.FindObjectsByType<SignalLost.Player.FirstPersonController>())
                player.transform.SetPositionAndRotation(OnGround(SpawnPoint.x, SpawnPoint.z) + Vector3.up * 0.1f, Quaternion.identity);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            ValidateStory();
            Debug.Log("DESERT_REVAMP_TERRAIN_APPLIED");
        }

        // ---------- Height field ----------

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>Metres inside a wobbly ellipse's outline (negative outside), and the bearing of the point from its centre.</summary>
        private static float Inside(Vector2 p, Vector2 centre, Vector2 radii, float angle, float wobble, float seed, out float bearing)
        {
            Vector2 d = Rotate(p - centre, -angle);
            Vector2 n = new Vector2(d.x / radii.x, d.y / radii.y);
            float a = Mathf.Atan2(n.y, n.x);
            bearing = Mathf.Repeat(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + angle, 360f);
            float edge = 1f + wobble * Noise(Mathf.Cos(a) * 30f, Mathf.Sin(a) * 30f, 9f, seed, seed * 0.7f);
            return (edge - n.magnitude) * (radii.x + radii.y) * 0.5f;
        }

        private static float PlateauInside(Vector2 p, out float bearing) => Inside(p, PlateauCentre, PlateauRadii, PlateauAngle, 0.08f, 4.1f, out bearing);
        private static float PlateauInside(Vector2 p) => PlateauInside(p, out _);
        private static float TableMesaInside(Vector2 p, out float bearing) => Inside(p, TableMesaCentre, TableMesaRadii, 10f, 0.07f, 6.3f, out bearing);
        private static float LookoutInside(Vector2 p, out float bearing) => Inside(p, LookoutCentre, new Vector2(LookoutRadius, LookoutRadius), 0f, 0.1f, 8.8f, out bearing);
        private static float SaltInside(Vector2 p) => Inside(p, SaltCentre, SaltRadii, -15f, 0.06f, 2.9f, out _);

        /// <summary>How much of a ramp this bearing is on (1 in the middle of one, fading out over 12°).</summary>
        private static float RampMask(float bearing, IEnumerable<float> ramps, float halfWidth)
        {
            float best = 0f;
            foreach (float ramp in ramps)
                best = Mathf.Max(best, 1f - SmoothStep01(halfWidth, halfWidth + 12f, Mathf.Abs(Mathf.DeltaAngle(bearing, ramp))));
            return best;
        }

        /// <summary>Metres from a polyline (and the index of the nearest segment).</summary>
        public static float PathDistance(Vector2 p, Vector2[] path, out int segment, out float along)
        {
            float best = float.MaxValue;
            segment = 0;
            along = 0f;
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector2 a = path[i], ab = path[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                float d = Vector2.Distance(p, a + ab * t);
                if (d < best) { best = d; segment = i; along = t; }
            }
            return best;
        }

        private static float PathDistance(Vector2 p, Vector2[] path) => PathDistance(p, path, out _, out _);

        /// <summary>The broad swell of the dunes without their crests, for floors that should be smooth.</summary>
        private static float Swell(float x, float z) => 1.6f + 3.5f * Noise(x, z, 95f, 11.3f, 7.1f);

        /// <summary>A raised table: <paramref name="top"/> over the inside, a cliff round the edge, long slopes where it has ramps.</summary>
        // A ramp runs out past the cliff line as a tongue (60% of it outside, 40% cut into the table), so even a narrow table
        // can have a long, walkable ramp.
        private static float Table(float h, float inside, float bearing, float top, IEnumerable<float> ramps, float rampHalfWidth, float rampLength, float x, float z)
        {
            float ramp = RampMask(bearing, ramps, rampHalfWidth);
            float start = -0.6f * rampLength * ramp;
            if (inside < start - 0.5f) return h;
            float s = SmoothStep01(start, Mathf.Lerp(CliffWidth, 0.4f * rampLength, ramp), inside);
            float surface = top + 0.45f * Noise(x, z, 17f, 3.7f, 5.9f);
            return Mathf.Max(h, Mathf.Lerp(h, surface, s));
        }

        /// <summary>The landforms, applied to the dunes before the arenas and camp are levelled.</summary>
        private static float Landforms(float x, float z, float h)
        {
            var p = new Vector2(x, z);

            // The Dry Wash: a sunken channel with soft banks.
            float wash = PathDistance(p, Wash);
            if (wash < WashHalf + WashBank)
            {
                float floor = Mathf.Max(Mathf.Min(h, Swell(x, z)) - WashDepth, SeaLevel + 1.2f);
                h = Mathf.Lerp(h, floor, 1f - SmoothStep01(WashHalf, WashHalf + WashBank, wash));
            }

            // Mirage Springs: a hollow with a pond in its floor. Inland, so the sea mask leaves it alone.
            float oasis = Vector2.Distance(p, OasisCentre);
            if (oasis < OasisRadius + 6f)
            {
                float floor = OasisFloor;
                h = Mathf.Lerp(h, floor, 1f - SmoothStep01(PondRadius + 2f, OasisRadius, oasis));
                if (oasis < PondRadius) h -= 1.1f * (1f - (oasis / PondRadius) * (oasis / PondRadius));
            }

            // Rattlesnake plateau, with the canyon cut through it.
            float plateau = PlateauInside(p, out float pb);
            if (plateau > -0.6f * RampLength - 1f)
            {
                float raised = Table(h, plateau, pb, PlateauTop, PlateauRamps, 16f, RampLength, x, z);
                float canyon = PathDistance(p, Canyon);
                float cut = 1f - SmoothStep01(CanyonHalf, CanyonHalf + CanyonWallWidth, canyon);
                float floor = Mathf.Max(0.3f * Swell(x, z) + 0.3f, SeaLevel + 0.8f);
                h = Mathf.Lerp(raised, Mathf.Min(h, floor), cut);
            }

            // The lookout rock in the Hoodoos.
            float lookout = LookoutInside(p, out float lb);
            if (lookout > -0.6f * LookoutRamp * 0.4f - 1f)
                h = Table(h, lookout, lb, LookoutTop, new[] { 20f }, 14f, LookoutRamp * 0.4f, x, z);

            // Table Mesa.
            float mesa = TableMesaInside(p, out float mb);
            if (mesa > -0.6f * 42f - 1f)
                h = Table(h, mesa, mb, TableMesaTop, new[] { TableMesaRamp }, 14f, 42f, x, z);

            // The salt flat: dead level with the raceway, fading back into the dunes.
            float salt = SaltInside(p);
            if (salt > -14f)
                h = Mathf.Lerp(h, SaltLevel, SmoothStep01(-14f, 0f, salt));
            return h;
        }

        private static float OasisFloor => Mathf.Max(DuneHeight(OasisCentre.x, OasisCentre.y) - OasisDepth, SeaLevel + 1.6f);
        /// <summary>The oasis pond's surface.</summary>
        public static float PondLevel => OasisFloor - 0.45f;
        private static float LookoutTop => DuneHeight(LookoutCentre.x, LookoutCentre.y) + LookoutRise;
        private static float SaltLevel => DuneHeight(Encounters[1].Center.x, Encounters[1].Center.y);

        /// <summary>Keeps scatter off the landforms (their faces, the canyon, the pond, the salt flat).</summary>
        private static bool InLandform(Vector2 p, float margin)
        {
            if (PlateauInside(p) > -margin - 6f) return true;
            if (TableMesaInside(p, out _) > -margin - 6f) return true;
            if (LookoutInside(p, out _) > -margin - 4f) return true;
            if (SaltInside(p) > -margin) return true;
            if (Vector2.Distance(p, OasisCentre) < OasisRadius + margin) return true;
            if (PathDistance(p, Wash) < WashHalf + WashBank + margin) return true;
            if (PathDistance(p, Canyon) < CanyonHalf + CanyonWallWidth + margin + 4f) return true;
            return false;
        }

        // ---------- Models ----------

        private static readonly Dictionary<string, (Color color, float smoothness)> SceneryColours = new Dictionary<string, (Color, float)>
        {
            ["DS_RockLight"] = (new Color(0.77f, 0.52f, 0.36f), 0.05f),
            ["DS_RockMid"] = (new Color(0.69f, 0.42f, 0.29f), 0.05f),
            ["DS_RockDark"] = (new Color(0.52f, 0.33f, 0.25f), 0.05f),
            ["DS_RockBand"] = (new Color(0.84f, 0.66f, 0.47f), 0.05f),
            ["DS_Bark"] = (new Color(0.48f, 0.36f, 0.26f), 0.1f),
            ["DS_BarkPale"] = (new Color(0.67f, 0.59f, 0.55f), 0.1f),
            ["DS_Frond"] = (new Color(0.24f, 0.63f, 0.52f), 0.25f),
            ["DS_FrondDark"] = (new Color(0.16f, 0.46f, 0.41f), 0.2f),
            ["DS_Pod"] = (new Color(0.96f, 0.46f, 0.62f), 0.45f),
            ["DS_DeadWood"] = (new Color(0.66f, 0.58f, 0.48f), 0.08f),
            ["DS_Spike"] = (new Color(0.43f, 0.67f, 0.59f), 0.2f),
            ["DS_Twig"] = (new Color(0.44f, 0.34f, 0.24f), 0.08f),
            ["DS_Egg"] = (new Color(0.93f, 0.89f, 0.78f), 0.35f),
            ["DS_Speckle"] = (new Color(0.47f, 0.38f, 0.27f), 0.2f),
            ["DS_PanIron"] = (new Color(0.2f, 0.2f, 0.21f), 0.55f),
            ["DS_Omelet"] = (new Color(0.96f, 0.81f, 0.35f), 0.3f),
        };

        private static readonly Dictionary<string, (Mesh mesh, Material[] materials)> modelCache = new Dictionary<string, (Mesh, Material[])>();

        /// <summary>A Blender scenery model's mesh and its materials (the project's own, matched by name).</summary>
        public static (Mesh mesh, Material[] materials) SceneryModel(string name)
        {
            if (modelCache.TryGetValue(name, out var cached) && cached.mesh != null) return cached;
            string path = ModelFolder + "/" + name + ".fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new InvalidOperationException($"{path} is missing. Run tools/blender/desert_scenery.py in Blender.");
            var filter = asset.GetComponentInChildren<MeshFilter>();
            var renderer = asset.GetComponentInChildren<MeshRenderer>();
            Material[] source = renderer.sharedMaterials;
            var materials = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                materials[i] = SceneryMaterial(source[i] != null ? source[i].name : "DS_RockMid");
            }
            modelCache[name] = (filter.sharedMesh, materials);
            return modelCache[name];
        }

        /// <summary>
        /// A scenery material, rendered on both sides (11-22, Lawson: "incomplete and see through parts"): thin pieces (palm
        /// fronds, cliff faces seen from above or behind) and any face that ends up turned the wrong way still show.
        /// </summary>
        private static Material SceneryMaterial(string key)
        {
            (Color color, float smoothness) look = SceneryColours.TryGetValue(key, out var found) ? found : (Color.magenta, 0f);
            Material material = MakeMaterial(key.Replace("DS_", "DesertScenery"), look.color, look.smoothness);
            if (material.HasProperty("_Cull") && material.GetFloat("_Cull") != 0f)
            {
                material.SetFloat("_Cull", 0f); // Render Face: Both
                material.doubleSidedGI = true;
                EditorUtility.SetDirty(material);
            }
            // Flat, matte rock and wood: no sky reflections, which turned shadowed faces seen edge-on a cold blue-grey.
            if (material.HasProperty("_EnvironmentReflections") && material.GetFloat("_EnvironmentReflections") != 0f)
            {
                material.SetFloat("_EnvironmentReflections", 0f);
                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                material.SetFloat("_SpecularHighlights", 0f);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        public enum Collision { None, Mesh, Trunk }

        /// <summary>Places a scenery model: its origin is the middle of its footprint at ground level.</summary>
        private static GameObject Scenery(string model, string name, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale, Collision collision)
        {
            (Mesh mesh, Material[] materials) = SceneryModel(model);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            if (collision == Collision.Mesh) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            else if (collision == Collision.Trunk)
            {
                var trunk = go.AddComponent<CapsuleCollider>();
                trunk.radius = 0.45f;
                trunk.height = 3.2f;
                trunk.center = new Vector3(0f, 1.6f, 0f);
            }
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        // ---------- Building the landforms ----------

        private static void BuildLandforms(Transform root)
        {
            var rng = new Random(20261122);
            BuildCanyon(Group("Rattlesnake Canyon", root), rng);
            BuildTableMesa(Group("Table Mesa", root), rng);
            BuildHoodoos(Group("The Hoodoos", root), rng);
            BuildOasis(Group("Mirage Springs", root), rng);
            BuildWash(Group("Dry Wash", root), rng);
            BuildSaltFlat(Group("Salt Flat", root), rng);
            BuildTrees(Group("Desert Trees", root), rng);
        }

        private static void BuildCanyon(Transform group, Random rng)
        {
            // Continuous eroded faces (DesertDunesCliffs.cs): both canyon walls, and the plateau's outer cliffs with gaps for
            // its ramps and the two canyon mouths.
            CanyonCliffs(Group("Canyon Walls", group));
            TableCliffs(Group("Plateau Cliffs", group), "Plateau Cliff", p => PlateauInside(p), PlateauCentre, PlateauTop,
                rim => PathDistance(rim, Canyon) < CanyonWallFoot + 3f, 701);

            // The Big Arch: a natural bridge over the canyon, its deck level with the plateau either side. Stretched along its
            // span so the nearly flat middle of the deck covers the whole gap, and up so its legs reach the floor.
            Vector2 spot = ArchSpot, along = (Canyon[3] - Canyon[2]).normalized;
            const float deck = 11.3f, legs = 2f; // the model's deck height above its origin, and how far its legs go below it
            float deckY = PlateauTop + 0.6f, floorY = Ground(spot.x, spot.y);
            float tall = (deckY - (floorY - 1f)) / (deck + legs);
            Scenery("DesertBigArch", "The Big Arch", group, new Vector3(spot.x, deckY - deck * tall, spot.y),
                Quaternion.LookRotation(new Vector3(along.x, 0f, along.y), Vector3.up), new Vector3(1.6f, tall, 1f), Collision.Mesh);

            // Fallen blocks on the canyon floor.
            Transform floor = Group("Canyon Floor", group);
            for (int k = 0, tries = 0; k < 18 && tries < 500; tries++)
            {
                int seg = rng.Next(Canyon.Length - 1);
                Vector2 c = Vector2.Lerp(Canyon[seg], Canyon[seg + 1], (float)rng.NextDouble());
                Vector2 dir = (Canyon[seg + 1] - Canyon[seg]).normalized;
                Vector2 p = c + new Vector2(-dir.y, dir.x) * Range(rng, -CanyonWallFoot + 1f, CanyonWallFoot - 1f);
                if (PlateauInside(p) < 4f) continue;
                float s = Range(rng, 0.7f, 2.2f);
                Solid(SaveMesh(LowPolyMesh.Icosphere("DesertBoulder", 0)), "Fallen Block " + (++k), floor, OnGround(p.x, p.y, s * 0.3f),
                    Quaternion.Euler(Range(rng, 0f, 360f), Range(rng, 0f, 360f), Range(rng, 0f, 360f)),
                    new Vector3(s * 1.3f, s * 0.8f, s * 1.1f), MakeMaterial("MesaRockDark", RockDark, 0.05f), s > 1.1f);
            }
        }

        private static void BuildTableMesa(Transform group, Random rng)
        {
            TableCliffs(group, "Table Mesa Cliff", p => TableMesaInside(p, out _), TableMesaCentre, TableMesaTop,
                rim => false, 733);
        }

        private static void BuildHoodoos(Transform group, Random rng)
        {
            string[] hoodoos = { "DesertHoodooA", "DesertHoodooB", "DesertHoodooC" };
            var placed = new List<Vector2>();
            for (int n = 0, tries = 0; n < 16 && tries < 800; tries++)
            {
                Vector2 p = HoodooCentre + Rotate(Vector2.right, Range(rng, 0f, 360f)) * Mathf.Sqrt((float)rng.NextDouble()) * HoodooRadius;
                if (LookoutInside(p, out _) > -6f || PathDistance(p, Wash) < WashHalf + WashBank + 3f) continue;
                if (placed.Exists(q => Vector2.Distance(q, p) < 8f)) continue;
                placed.Add(p);
                float s = Range(rng, 0.8f, 1.25f);
                Scenery(hoodoos[rng.Next(hoodoos.Length)], "Hoodoo " + (++n), group, OnGround(p.x, p.y, 0.3f),
                    Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), new Vector3(s, s * Range(rng, 0.9f, 1.15f), s), Collision.Mesh);
            }
            for (int n = 0, tries = 0; n < 4 && tries < 400; tries++)
            {
                Vector2 p = HoodooCentre + Rotate(Vector2.right, Range(rng, 0f, 360f)) * Range(rng, 12f, HoodooRadius + 8f);
                if (LookoutInside(p, out _) > -6f || placed.Exists(q => Vector2.Distance(q, p) < 9f)) continue;
                placed.Add(p);
                Scenery("DesertBalancedRock", "Balanced Rock " + (++n), group, OnGround(p.x, p.y, 0.3f),
                    Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), Vector3.one * Range(rng, 0.9f, 1.2f), Collision.Mesh);
            }
            for (int n = 0, tries = 0; n < 4 && tries < 400; tries++)
            {
                Vector2 p = HoodooCentre + Rotate(Vector2.right, Range(rng, 0f, 360f)) * Range(rng, HoodooRadius - 4f, HoodooRadius + 14f);
                if (placed.Exists(q => Vector2.Distance(q, p) < 12f) || PathDistance(p, Wash) < WashHalf + WashBank + 4f) continue;
                placed.Add(p);
                Scenery("DesertRockFin", "Rock Fin " + (++n), group, OnGround(p.x, p.y, 0.5f),
                    Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), new Vector3(1f, Range(rng, 0.8f, 1.2f), 1f), Collision.Mesh);
            }
            TableCliffs(Group("Lookout Rock", group), "Lookout Cliff", p => LookoutInside(p, out _), LookoutCentre, LookoutTop,
                rim => false, 757);
        }

        private static void BuildOasis(Transform group, Random rng)
        {
            // The pond: the shared stylized water, clear and turquoise here.
            const int sides = 40;
            var mesh = new Mesh { name = "MirageSpringsPond" };
            var vertices = new List<Vector3> { Vector3.zero };
            var triangles = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                vertices.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (PondRadius + 1.5f));
            }
            for (int i = 0; i < sides; i++) { triangles.Add(0); triangles.Add(1 + (i + 1) % sides); triangles.Add(1 + i); }
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            var colors = new Color[vertices.Count];
            for (int i = 0; i < colors.Length; i++) colors[i] = new Color(0.12f, 0f, 0f, 1f);
            mesh.colors = colors;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh = SaveMesh(mesh);
            var pond = new GameObject("Spring Pond");
            pond.transform.SetParent(group, false);
            pond.transform.position = new Vector3(OasisCentre.x, PondLevel, OasisCentre.y);
            pond.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = pond.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = OasisWater();
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            // Palms round the water, on the hollow's slopes.
            for (int n = 0, tries = 0; n < 14 && tries < 600; tries++)
            {
                float a = Range(rng, 0f, 360f);
                Vector2 p = OasisCentre + Rotate(Vector2.right, a) * Range(rng, PondRadius + 2.5f, OasisRadius - 3f);
                Scenery(rng.Next(2) == 0 ? "DesertPalmA" : "DesertPalmB", "Palm " + (++n), group, OnGround(p.x, p.y, 0.3f),
                    Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), Vector3.one * Range(rng, 0.85f, 1.2f), Collision.Trunk);
            }
            // Sandstone fins round the rim, leaving the way in from the east and the wash's mouth to the north open.
            int f = 0;
            foreach (float a in new[] { 120f, 165f, 205f, 250f, 300f })
            {
                Vector2 p = OasisCentre + Rotate(Vector2.right, a) * (OasisRadius + 4f);
                Vector2 tangent = Rotate(Vector2.up, a);
                Scenery("DesertRockFin", "Rim Fin " + (++f), group, OnGround(p.x, p.y, 0.6f),
                    Quaternion.LookRotation(new Vector3(-tangent.y, 0f, tangent.x), Vector3.up), new Vector3(1.1f, Range(rng, 0.7f, 1f), 1f), Collision.Mesh);
            }
        }

        private static Material OasisWater()
        {
            string path = MaterialFolder + "/OasisWater.mat";
            Shader shader = Shader.Find("Signal Lost/Stylized Water");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Lit")) { name = "OasisWater" };
                AssetDatabase.CreateAsset(material, path);
            }
            if (shader != null)
            {
                material.shader = shader;
                // A clear spring: turquoise over pale sand, a little sparkle, barely a ripple.
                material.SetColor("_DeepColor", new Color(0.05f, 0.45f, 0.5f, 0.85f));
                material.SetColor("_ShallowColor", new Color(0.35f, 0.8f, 0.72f, 0.55f));
                material.SetColor("_HorizonColor", new Color(0.55f, 0.85f, 0.85f, 1f));
                material.SetColor("_FoamColor", new Color(0.95f, 0.95f, 0.88f, 1f));
                material.SetFloat("_WaveHeight", 0.01f);
                material.SetFloat("_WaveScale", 0.25f);
                material.SetFloat("_WaveSpeed", 0.35f);
                material.SetFloat("_Sparkle", 0.35f);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildWash(Transform group, Random rng)
        {
            // Dead trees and bleached driftwood logs along the banks.
            int n = 0;
            for (int i = 0; i < Wash.Length - 1; i++)
            {
                Vector2 a = Wash[i], b = Wash[i + 1], dir = (b - a).normalized, side = new Vector2(-dir.y, dir.x);
                float length = Vector2.Distance(a, b);
                for (float t = Range(rng, 4f, 10f); t < length; t += Range(rng, 12f, 20f))
                {
                    Vector2 p = a + dir * t + side * (rng.Next(2) == 0 ? -1f : 1f) * Range(rng, WashHalf + 0.5f, WashHalf + 4f);
                    if (InsideAnySite(p, 3f)) continue;
                    Scenery(rng.Next(2) == 0 ? "DesertDeadTreeA" : "DesertDeadTreeB", "Dead Tree " + (++n), group, OnGround(p.x, p.y, 0.2f),
                        Quaternion.Euler(Range(rng, -6f, 6f), Range(rng, 0f, 360f), Range(rng, -6f, 6f)), Vector3.one * Range(rng, 0.8f, 1.3f), Collision.Trunk);
                }
            }
            // Pebbles on the bed.
            Mesh pebble = SaveMesh(LowPolyMesh.Icosphere("DesertRoundRock", 1));
            Material dark = MakeMaterial("MesaRockDark", RockDark, 0.05f), light = MakeMaterial("MesaRockLight", RockLight, 0.05f);
            for (int k = 0; k < 60; k++)
            {
                int seg = rng.Next(Wash.Length - 1);
                Vector2 c = Vector2.Lerp(Wash[seg], Wash[seg + 1], (float)rng.NextDouble());
                Vector2 dir = (Wash[seg + 1] - Wash[seg]).normalized;
                Vector2 p = c + new Vector2(-dir.y, dir.x) * Range(rng, -WashHalf, WashHalf);
                float s = Range(rng, 0.25f, 0.7f);
                var go = new GameObject("Pebble " + (k + 1));
                go.transform.SetParent(group, false);
                go.transform.SetPositionAndRotation(OnGround(p.x, p.y, s * 0.3f), Quaternion.Euler(Range(rng, 0f, 360f), Range(rng, 0f, 360f), 0f));
                go.transform.localScale = new Vector3(s * 1.3f, s * 0.6f, s);
                go.AddComponent<MeshFilter>().sharedMesh = pebble;
                go.AddComponent<MeshRenderer>().sharedMaterial = rng.Next(2) == 0 ? dark : light;
            }
        }

        // The salt flat's crust (11-22, rebuilt: it was square tiles): a smooth-edged sheet a hand above the sand following its
        // wobbly outline, with a honeycomb of dark crack lines, the way a dried salt pan splits into polygons. No collider.
        private static void BuildSaltFlat(Transform group, Random rng)
        {
            const int spokes = 96, rings = 16;
            var vertices = new List<Vector3>();
            var crust = new List<int>();
            var edge = new float[spokes];
            for (int k = 0; k < spokes; k++)
            {
                Vector2 dir = Rotate(Vector2.right, k * 360f / spokes);
                for (float r = 0f; r < 80f; r += 0.25f) if (SaltInside(SaltCentre + dir * r) < 2f) { edge[k] = r; break; }
            }
            Vector3 V(Vector2 q) => new Vector3(q.x, Ground(q.x, q.y) + 0.12f, q.y); // (high enough not to flicker against the flat ground far off)
            Vector2 Q(int k, int j) => SaltCentre + Rotate(Vector2.right, (k % spokes) * 360f / spokes) * (edge[k % spokes] * j / rings);
            for (int k = 0; k < spokes; k++)
                for (int j = 0; j < rings; j++)
                {
                    Vector2 a = Q(k, j), b = Q(k + 1, j), c = Q(k + 1, j + 1), d = Q(k, j + 1);
                    if (InsideAnySite((a + c) * 0.5f, 0.5f)) continue;
                    int start = vertices.Count;
                    vertices.Add(V(a)); vertices.Add(V(d)); vertices.Add(V(c)); vertices.Add(V(b));
                    crust.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                }

            // Honeycomb cracks: hexagons on a jittered lattice; each corner's jitter comes from its position, so the three
            // hexagons sharing a corner agree on it.
            var cracks = new List<int>();
            const float size = 3.4f;
            Vector2 Corner(Vector2 c) => c + new Vector2(Noise(c.x, c.y, 2.3f, 4.1f, 0.7f), Noise(c.x, c.y, 2.3f, 9.4f, 3.3f)) * 0.9f;
            void Crack(Vector2 a, Vector2 b, float width)
            {
                Vector2 side = new Vector2(-(b - a).y, (b - a).x).normalized * width * 0.5f;
                int start = vertices.Count;
                Vector3 W(Vector2 q) => new Vector3(q.x, Ground(q.x, q.y) + 0.16f, q.y);
                vertices.Add(W(a - side)); vertices.Add(W(a + side)); vertices.Add(W(b + side)); vertices.Add(W(b - side));
                cracks.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            float w = size * Mathf.Sqrt(3f);
            for (float z = SaltCentre.y - 50f; z < SaltCentre.y + 50f; z += size * 1.5f)
            {
                int row = Mathf.RoundToInt((z - SaltCentre.y) / (size * 1.5f));
                for (float x = SaltCentre.x - 56f + (row & 1) * w * 0.5f; x < SaltCentre.x + 56f; x += w)
                {
                    var centre = new Vector2(x, z);
                    for (int k = 0; k < 3; k++)
                    {
                        Vector2 a = Corner(Round(centre + Rotate(Vector2.right, 30f + 60f * k) * size));
                        Vector2 b = Corner(Round(centre + Rotate(Vector2.right, 90f + 60f * k) * size));
                        if (SaltInside(a) < 3f || SaltInside(b) < 3f || InsideAnySite((a + b) * 0.5f, 1f)) continue;
                        if (rng.NextDouble() < 0.12) continue; // a few edges never cracked
                        Crack(a, b, Range(rng, 0.12f, 0.22f));
                    }
                }
            }

            var mesh = new Mesh { name = "DesertSaltFlat", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(crust, 0);
            mesh.SetTriangles(cracks, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh = SaveMesh(mesh);
            var go = new GameObject("Salt Crust");
            go.transform.SetParent(group, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            Material crack = MakeMaterial("DesertSaltCrack", new Color(0.72f, 0.66f, 0.56f), 0.1f);
            crack.SetColor("_BaseColor", new Color(0.72f, 0.66f, 0.56f)); // (darker than the first tiled version)
            renderer.sharedMaterials = new[] { MakeMaterial("DesertSaltCrust", new Color(0.95f, 0.93f, 0.87f), 0.15f), crack };
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // Rounds a lattice corner so neighbouring hexagons compute exactly the same point for it.
        private static Vector2 Round(Vector2 v) => new Vector2(Mathf.Round(v.x * 100f) / 100f, Mathf.Round(v.y * 100f) / 100f);

        private static void BuildTrees(Transform group, Random rng)
        {
            // Spindle trees out on the flats and up on the plateau and the mesa.
            int n = 0;
            for (int tries = 0; n < 30 && tries < 3000; tries++)
            {
                Vector2 p = RandomPoint(rng, 182f);
                if (!InsidePlayable(p, 12f) || InsideAnySite(p, 6f) || InLandform(p, 2f)) continue;
                if (Mathf.Abs(p.x - CampOrigin.x) < 16f && p.y < CampOrigin.y + 18f) continue;
                Spindle(group, ref n, rng, p);
            }
            for (int tries = 0, up = 0; up < 10 && tries < 2000; tries++)
            {
                Vector2 p = PlateauCentre + Rotate(Vector2.right, Range(rng, 0f, 360f)) * Range(rng, 0f, 60f);
                if (PlateauInside(p) < 8f || PathDistance(p, Canyon) < CanyonHalf + CanyonWallWidth + 6f) continue;
                Spindle(group, ref n, rng, p);
                up++;
            }
            for (int tries = 0, up = 0; up < 4 && tries < 800; tries++)
            {
                Vector2 p = TableMesaCentre + Rotate(Vector2.right, Range(rng, 0f, 360f)) * Range(rng, 0f, 24f);
                if (TableMesaInside(p, out _) < 6f) continue;
                Spindle(group, ref n, rng, p);
                up++;
            }
            // Dead thorn trees round Briarwing's Roost.
            int d = 0;
            for (int tries = 0; d < 6 && tries < 600; tries++)
            {
                Vector2 p = Encounters[0].Center + Rotate(Vector2.right, Range(rng, 0f, 360f)) * Range(rng, ArenaSiteRadius + 4f, ArenaSiteRadius + 14f);
                if (InLandform(p, 1f)) continue;
                Scenery(rng.Next(2) == 0 ? "DesertDeadTreeA" : "DesertDeadTreeB", "Roost Thorn Tree " + (++d), group, OnGround(p.x, p.y, 0.2f),
                    Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), Vector3.one * Range(rng, 0.9f, 1.4f), Collision.Trunk);
            }
        }

        private static void Spindle(Transform group, ref int n, Random rng, Vector2 p)
        {
            Scenery(rng.Next(2) == 0 ? "DesertSpindleTreeA" : "DesertSpindleTreeB", "Spindle Tree " + (++n), group, OnGround(p.x, p.y, 0.2f),
                Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), Vector3.one * Range(rng, 0.8f, 1.35f), Collision.Trunk);
        }
    }
}
