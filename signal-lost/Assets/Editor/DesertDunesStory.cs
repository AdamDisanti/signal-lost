using System;
using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Enemies;
using SignalLost.Enemies.Desert;
using SignalLost.NPC;
using SignalLost.Player;
using SignalLost.Progression;
using SignalLost.Scanner;
using SignalLost.Story;
using SignalLost.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace SignalLost.Editor
{
    /// <summary>
    /// The desert's story pass (11-08, Lawson): the three mini-boss arenas, the Core Worm's lair under the desert, the
    /// trophies and the quests that tie them together (<see cref="DesertStory"/>).
    /// <list type="bullet">
    /// <item><b>Arenas</b> (part of DesertEnvironment.prefab, so the NavMesh walks round them): Old Buzzard's Roost in the
    /// Wreck Field, Burnout's Scorch Raceway in the Mesa Pass and Sludge's Fuel Sump at the Buried Hull.</item>
    /// <item><b>The lair</b> (CorewormLair.prefab): a cavern 22 m under the worm's old sand bowl, with the arena floor that
    /// collapses in its phase 3, cave pillars and wreck slabs for cover, glowing crystals, a rocky dome whose ramp side caves
    /// in, a shaft up to a patch of the dunes that drops through (the environment's sinkhole lid) and the rubble ramp.</item>
    /// <item><b>Install</b> (Signal Lost > Desert > Install Desert Story): rebuilds the environment, the desert camp (with
    /// the Chop Shop rack), the trophies and the lair, then puts the bosses, tasks, worm, lair and story into
    /// DesertDunes.unity in place of the old hermit-crab encounters and boss placeholders.</item>
    /// </list>
    /// </summary>
    public static partial class DesertDunesBuilder
    {
        public const string LairPrefabPath = Root + "/CorewormLair.prefab";
        private const string SinkholeLidName = "Sinkhole Lid";
        private const string StoryRootName = "Desert Story";
        private const string TrophyFolder = "Assets/Resources/SignalLost/Trophies";

        // The lair sits under the boss bowl; the way out climbs toward the north dock.
        private const float LairDepth = 22f, DomeRadius = 44f, DomeHeight = 18f, LairArenaRadius = 34f;
        // The hole (and the heap of rubble under it) is over the middle of the lair's arena, in the bowl but clear of
        // its engine wreck; the climb comes out facing the north dock.
        // The collapse (11-09): a long gash in the roof on the north side, running from near the cavern wall toward the
        // middle. Its far end (the lip) is where the debris cone tops out and the climb steps onto the dunes.
        private const float ApexDist = 27f, GashLength = 17f, GashHalf = 5.5f, ShaftMargin = 3f;
        private const float ReposeTan = 0.65f; // the debris cone's slope: 33 degrees
        private static Vector2 LairCentre => BossArena.Center + new Vector2(-7f, -8f);
        // Toward a gently sloping part of the bowl's floor, north of its middle and clear of the engine wreck, so the
        // climb steps out onto easy ground (the bowl's rim is too steep to climb out onto).
        private static Vector2 LairExitDir => (BossArena.Center + new Vector2(-8f, 6f) - LairCentre).normalized;
        private static Vector2 LairAcross => new Vector2(-LairExitDir.y, LairExitDir.x);
        /// <summary>The middle of the gash, relative to the lair's centre and in the world (x, z).</summary>
        private static Vector2 HoleLocal => LairExitDir * (ApexDist - GashLength * 0.5f);
        private static Vector2 SinkholeCentre => LairCentre + HoleLocal;

        /// <summary>How far a point (relative to the lair) is from the gash's centre line.</summary>
        private static float GashDistance(Vector2 local)
        {
            Vector2 a = LairExitDir * (ApexDist - GashHalf), b = LairExitDir * (ApexDist - GashLength + GashHalf);
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(local - a, ab) / ab.sqrMagnitude);
            return (local - (a + ab * t)).magnitude;
        }

        /// <summary>Is a point of the dunes (world x, z) over the gash, i.e. sand that drops away?</summary>
        private static bool InSinkhole(Vector2 world) => GashDistance(world - LairCentre) < GashHalf;

        /// <summary>The outline of the gash grown by <paramref name="grow"/> metres: point <paramref name="i"/> of <paramref name="count"/>, relative to the lair.</summary>
        private static Vector2 GashOutline(int i, int count, float grow)
        {
            float angle = (i % count) * Mathf.PI * 2f / count;
            Vector2 dir = LairExitDir * Mathf.Cos(angle) + LairAcross * Mathf.Sin(angle);
            Vector2 end = LairExitDir * (Mathf.Cos(angle) >= 0f ? ApexDist - GashHalf : ApexDist - GashLength + GashHalf);
            return end + dir * (GashHalf + grow);
        }

        // ---------- Arena materials ----------

        private enum AM { Hull, Burnt, Scorch, Oil, Rust, Bone, Feather, FeatherTip, Stick, Egg, Rock, RockDark, Sandstone, Checker, CheckerDark,
            Red, Brass, Girder, CaveRock, CaveFloor, Crystal, Fang, Amber, Sand, RubbleDark, RubbleDust, Debris, DuneShade }

        private static Dictionary<AM, Material> arenaMats;

        private static void PrepareArenaMaterials(Dictionary<string, Material> mats, Material[] wreckMats)
        {
            arenaMats = new Dictionary<AM, Material>
            {
                [AM.Hull] = wreckMats[0],
                [AM.Burnt] = wreckMats[1],
                [AM.Scorch] = ArenaMaterial("ArenaScorch", new Color(0.34f, 0.24f, 0.17f), 0.05f),
                [AM.Oil] = ArenaMaterial("ArenaOil", new Color(0.07f, 0.055f, 0.045f), 0.55f),
                [AM.Rust] = ArenaMaterial("ArenaRust", new Color(0.55f, 0.27f, 0.13f), 0.25f),
                [AM.Bone] = mats["Bone"],
                [AM.Feather] = ArenaMaterial("ArenaFeather", new Color(0.4f, 0.35f, 0.33f), 0.15f),
                [AM.FeatherTip] = ArenaMaterial("ArenaFeatherTip", new Color(0.92f, 0.88f, 0.8f), 0.15f),
                [AM.Stick] = ArenaMaterial("ArenaStick", new Color(0.44f, 0.33f, 0.22f), 0.05f),
                [AM.Egg] = ArenaMaterial("ArenaEgg", new Color(0.93f, 0.89f, 0.76f), 0.3f),
                [AM.Rock] = mats["RockLight"],
                [AM.RockDark] = mats["RockDark"],
                [AM.Sandstone] = mats["Sandstone"],
                [AM.Checker] = ArenaMaterial("ArenaChecker", new Color(0.95f, 0.94f, 0.9f), 0.2f),
                [AM.CheckerDark] = ArenaMaterial("ArenaCheckerDark", new Color(0.1f, 0.1f, 0.12f), 0.2f),
                [AM.Red] = ArenaMaterial("ArenaDrumRed", new Color(0.72f, 0.18f, 0.12f), 0.35f),
                [AM.Brass] = ArenaMaterial("ArenaBrass", new Color(0.8f, 0.63f, 0.3f), 0.55f),
                [AM.Girder] = ArenaMaterial("ArenaGirder", new Color(0.36f, 0.36f, 0.38f), 0.4f),
                [AM.CaveRock] = ArenaMaterial("LairRock", new Color(0.52f, 0.37f, 0.27f), 0.05f),
                [AM.CaveFloor] = ArenaMaterial("LairSand", new Color(0.68f, 0.52f, 0.36f), 0.05f),
                [AM.Crystal] = DesertFodderBuilder.Unlit("LairCrystal", new Color(1f, 0.6f, 0.22f)),
                [AM.Fang] = ArenaMaterial("TrophyFangBone", new Color(0.9f, 0.84f, 0.68f), 0.4f),
                [AM.Amber] = ArenaMaterial("TrophyAmber", new Color(0.9f, 0.62f, 0.18f), 0.6f),
                [AM.Sand] = mats["Sand"],
                [AM.RubbleDark] = ArenaMaterial("LairRubble", new Color(0.44f, 0.31f, 0.23f), 0.05f),
                [AM.RubbleDust] = ArenaMaterial("LairRubbleDust", new Color(0.74f, 0.58f, 0.41f), 0.03f),
                [AM.Debris] = ArenaMaterial("LairDebris", new Color(0.56f, 0.42f, 0.3f), 0.03f),
                [AM.DuneShade] = mats["Shade"],
            };
        }

        // Like MakeMaterial, but the builder owns these: the colour is set every build.
        private static Material ArenaMaterial(string name, Color color, float smoothness)
        {
            Material material = MakeMaterial(name, color, smoothness);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static readonly Dictionary<string, Mesh> SavedKits = new Dictionary<string, Mesh>();

        // Saves a kit as a mesh asset (keeping the GUID of an existing one), once per name per build.
        private static Mesh SaveKit(FlatMeshKit<AM> kit, string name)
        {
            if (SavedKits.TryGetValue(name, out Mesh cached) && cached != null) return cached;
            Mesh mesh = kit.Build(name);
            string path = MeshFolder + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else
            {
                string order = FlatMeshKit<AM>.Order(mesh);
                EditorUtility.CopySerialized(mesh, existing);
                FlatMeshKit<AM>.Remember(existing, order);
                Object.DestroyImmediate(mesh);
                mesh = existing;
            }
            SavedKits[name] = mesh;
            return mesh;
        }

        /// <summary>A prop from a kit: renderer with its materials and, if asked, a mesh collider.</summary>
        private static GameObject KitProp(string meshName, Func<FlatMeshKit<AM>> build, string name, Transform parent, Vector3 position, Quaternion rotation,
            Vector3? scale = null, bool collide = false, bool convex = false, bool isStatic = true)
        {
            Mesh mesh = SavedKits.TryGetValue(meshName, out Mesh cached) && cached != null ? cached : SaveKit(build(), meshName);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale ?? Vector3.one;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var materials = new Material[mesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = arenaMats[FlatMeshKit<AM>.MaterialOf(mesh, i)];
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            if (collide)
            {
                var collider = go.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                collider.convex = convex;
            }
            if (isStatic) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        private static FlatMeshKit<AM> Single(Mesh mesh, AM material)
        {
            var kit = new FlatMeshKit<AM>();
            kit.Add(mesh, Matrix4x4.identity, material);
            return kit;
        }

        private static Vector3 AroundSite(Vector2 centre, float degrees, float radius, float sink = 0f)
        {
            Vector2 p = centre + new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad)) * radius;
            return OnGround(p.x, p.y, sink);
        }

        private static float AngleTo(Vector2 from, Vector2 to) => Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;

        private static bool InGap(float degrees, params (float centre, float half)[] gaps)
        {
            foreach ((float centre, float half) in gaps)
                if (Mathf.Abs(Mathf.DeltaAngle(degrees, centre)) < half) return true;
            return false;
        }

        // =====================================================================
        // The mini-boss arenas
        // =====================================================================

        private static void BuildMiniBossArenas(Transform root, Dictionary<string, Material> mats, Material[] wreckMats, Mesh panel)
        {
            SavedKits.Clear();
            PrepareArenaMaterials(mats, wreckMats);
            var rng = new Random(20261108);
            Transform group = Group("Chop Shop Arenas", root);
            BuildBuzzardRoost(Group("Old Buzzard's Roost", group), Encounters[0].Center, rng, panel, wreckMats);
            BuildScorchRaceway(Group("Scorch Raceway", group), Encounters[1].Center, rng);
            BuildFuelSump(Group("Fuel Sump", group), Encounters[2].Center, rng);
        }

        // ---------- Old Buzzard's Roost (the Wreck Field) ----------
        // Open sand under the sky for its dives, ringed by low hull plates to duck behind when it bombs; a broken ring of
        // boulders round the edge; bare dead trees it perches on; and its nest of sticks and ship scrap on a rock stump,
        // with three eggs. Bones and feathers everywhere.
        private static void BuildBuzzardRoost(Transform parent, Vector2 c, Random rng, Mesh panel, Material[] wreckMats)
        {
            float toCamp = AngleTo(c, CampOrigin);
            for (int i = 0; i < 16; i++)
            {
                float a = i * 22.5f + Range(rng, -5f, 5f);
                if (InGap(a, (toCamp, 26f), (90f, 16f))) continue;
                float s = Range(rng, 3f, 4.6f);
                KitProp("ArenaBoulder", () => Single(LowPolyMesh.Icosphere("b", 0), AM.Rock), "Boulder", parent, AroundSite(c, a, Range(rng, 27f, 30f), s * 0.3f),
                    Quaternion.Euler(Range(rng, 0f, 360f), Range(rng, 0f, 360f), Range(rng, 0f, 360f)), new Vector3(s, s * 0.75f, s * 1.1f), true, true);
            }
            if (panel != null)
            {
                Transform ring = Group("Hull Plate Ring", parent);
                for (int i = 0; i < 9; i++)
                {
                    float a = i * 40f + 12f;
                    Vector2 p = c + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * 14f;
                    Panel(panel, "Hull Plate " + (i + 1), ring, p, Range(rng, 2.6f, 3.4f), rng, wreckMats);
                }
            }

            // The roost: a stump of rock with the nest on top, across the arena from the way in.
            float roostAngle = toCamp + 180f;
            Vector3 stumpAt = AroundSite(c, roostAngle, 24f, 1f);
            float stumpHeight = 9f;
            KitProp("ArenaStump", () => Single(LowPolyMesh.TaperedPrism("s", 7, 0.72f), AM.RockDark), "Roost Stump", parent, stumpAt,
                Quaternion.Euler(-90f, Range(rng, 0f, 360f), 0f), new Vector3(5.8f, 5.4f, stumpHeight), true);
            KitProp("BuzzardNest", NestKit, "Old Buzzard's Nest", parent, stumpAt + Vector3.up * (stumpHeight - 0.6f), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), Vector3.one * 1.45f);

            // Dead trees to perch on.
            foreach (float a in new[] { roostAngle + 60f, roostAngle - 70f, roostAngle + 150f, roostAngle - 140f })
                KitProp("ArenaDeadTree", DeadTreeKit, "Dead Tree", parent, AroundSite(c, a, Range(rng, 21f, 25f), 0.3f),
                    Quaternion.Euler(0f, Range(rng, 0f, 360f), Range(rng, -6f, 6f)), Vector3.one * Range(rng, 0.9f, 1.2f), true);

            // Bones: ribs of its old meals poking out of the sand, and loose feathers.
            Transform bones = Group("Bones", parent);
            for (int i = 0; i < 7; i++)
            {
                float a = Range(rng, 0f, 360f), r = Range(rng, 5f, 20f);
                float length = Range(rng, 1.4f, 3.2f);
                KitProp("ArenaRib", () => Single(LowPolyMesh.TaperedPrism("r", 5, 0.2f), AM.Bone), "Rib", bones, AroundSite(c, a, r, 0.3f),
                    Quaternion.Euler(-90f + Range(rng, 20f, 50f), Range(rng, 0f, 360f), 0f), new Vector3(0.18f, 0.18f, length));
            }
            KitProp("ArenaSkull", SkullKit, "Big Skull", bones, AroundSite(c, toCamp + 40f, 9f, 0.4f), Quaternion.Euler(0f, toCamp + 200f, 8f), Vector3.one, true, true);
            Transform feathers = Group("Feathers", parent);
            for (int i = 0; i < 14; i++)
                KitProp("ArenaFeatherLoose", () => FeatherKit(1f), "Feather", feathers, AroundSite(c, Range(rng, 0f, 360f), Range(rng, 2f, 22f), -0.05f),
                    Quaternion.Euler(Range(rng, -6f, 6f), Range(rng, 0f, 360f), Range(rng, -10f, 10f)), Vector3.one * Range(rng, 0.8f, 1.3f));
        }

        // A nest about 7 m across: rings of sticks laid round, a few hull scraps woven in and three eggs. Origin at its base.
        private static FlatMeshKit<AM> NestKit()
        {
            var kit = new FlatMeshKit<AM>();
            var rng = new Random(77);
            for (int ring = 0; ring < 3; ring++)
            {
                int count = 14 + ring * 4;
                float radius = 2.2f + ring * 0.7f;
                for (int i = 0; i < count; i++)
                {
                    float a = (i + ring * 0.5f) * 360f / count;
                    Vector3 at = Quaternion.Euler(0f, a, 0f) * new Vector3(radius, 0.25f + ring * 0.35f, 0f);
                    Quaternion along = Quaternion.Euler(0f, a, 0f) * Quaternion.Euler((float)rng.NextDouble() * 24f - 12f, 0f, (float)rng.NextDouble() * 30f - 15f);
                    kit.Add(LowPolyMesh.TaperedPrism("stick", 5, 0.6f), Matrix4x4.TRS(at - along * Vector3.forward * 1.6f, along, new Vector3(0.11f, 0.11f, 3.2f)), AM.Stick);
                }
            }
            kit.Add(LowPolyMesh.Lathe("bowl", new[] { new Vector2(0f, 0.1f), new Vector2(2.4f, 0.1f), new Vector2(2.4f, 0.3f), new Vector2(0f, 0.3f) }, 9), Matrix4x4.identity, AM.Stick);
            kit.Box(new Vector3(0.9f, 0.7f, 2.3f), new Vector3(1.6f, 0.08f, 1f), Quaternion.Euler(20f, 30f, 10f), AM.Hull);
            kit.Box(new Vector3(-2.1f, 0.8f, -0.9f), new Vector3(1.2f, 0.08f, 1.4f), Quaternion.Euler(-15f, 70f, 25f), AM.Burnt);
            foreach (Vector3 egg in new[] { new Vector3(0.3f, 0.65f, 0.2f), new Vector3(-0.45f, 0.62f, -0.2f), new Vector3(0.1f, 0.6f, -0.65f) })
                kit.Add(LowPolyMesh.Icosphere("egg", 1), Matrix4x4.TRS(egg, Quaternion.Euler(10f, 0f, 15f), new Vector3(0.4f, 0.52f, 0.4f)), AM.Egg);
            for (int i = 0; i < 5; i++)
            {
                FlatMeshKit<AM> feather = FeatherKit(1.3f);
                Mesh mesh = feather.Build("f");
                kit.Add(mesh, Matrix4x4.TRS(Quaternion.Euler(0f, i * 72f + 15f, 0f) * new Vector3(2.4f, 0.9f, 0f), Quaternion.Euler(-30f, i * 72f + 15f, 0f), Vector3.one), AM.Feather);
            }
            return kit;
        }

        // A long grey feather, quill along +Z, lying flat, with a pale tip.
        private static FlatMeshKit<AM> FeatherKit(float length)
        {
            var kit = new FlatMeshKit<AM>();
            float l = 0.9f * length, w = 0.16f * length;
            Vector3 root = Vector3.zero, tip = new Vector3(0f, 0.02f, l);
            Vector3 left = new Vector3(-w, 0.015f, l * 0.45f), right = new Vector3(w * 0.8f, 0.015f, l * 0.5f);
            Vector3 pale = new Vector3(0f, 0.02f, l * 0.8f);
            kit.Tri(root + new Vector3(0f, 0f, 0.15f * length), left, pale, Vector3.up, AM.Feather);
            kit.Tri(root + new Vector3(0f, 0f, 0.15f * length), pale, right, Vector3.up, AM.Feather);
            kit.Tri(left, tip, pale, Vector3.up, AM.FeatherTip);
            kit.Tri(pale, tip, right, Vector3.up, AM.FeatherTip);
            kit.Tri(root + new Vector3(0f, 0f, 0.15f * length), pale, left, Vector3.down, AM.Feather);
            kit.Tri(root + new Vector3(0f, 0f, 0.15f * length), right, pale, Vector3.down, AM.Feather);
            kit.Add(LowPolyMesh.TaperedPrism("quill", 4, 0.3f), Matrix4x4.TRS(new Vector3(0f, 0.02f, 0f), Quaternion.identity, new Vector3(0.012f, 0.012f, l)), AM.FeatherTip);
            return kit;
        }

        // A bare, crooked dead tree about 6 m tall with three branches (perches).
        private static FlatMeshKit<AM> DeadTreeKit()
        {
            var kit = new FlatMeshKit<AM>();
            kit.Add(LowPolyMesh.TaperedPrism("trunk", 6, 0.45f), Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(-88f, 0f, 0f), new Vector3(0.42f, 0.42f, 6f)), AM.Stick);
            kit.Add(LowPolyMesh.TaperedPrism("b1", 5, 0.3f), Matrix4x4.TRS(new Vector3(0f, 3.6f, 0f), Quaternion.Euler(-40f, 30f, 0f), new Vector3(0.18f, 0.18f, 2.6f)), AM.Stick);
            kit.Add(LowPolyMesh.TaperedPrism("b2", 5, 0.3f), Matrix4x4.TRS(new Vector3(0f, 4.6f, 0f), Quaternion.Euler(-35f, 200f, 0f), new Vector3(0.15f, 0.15f, 2.2f)), AM.Stick);
            kit.Add(LowPolyMesh.TaperedPrism("b3", 5, 0.25f), Matrix4x4.TRS(new Vector3(0f, 5.3f, 0f), Quaternion.Euler(-55f, 110f, 0f), new Vector3(0.12f, 0.12f, 1.6f)), AM.Stick);
            return kit;
        }

        // A big horned skull half sunk in the sand.
        private static FlatMeshKit<AM> SkullKit()
        {
            var kit = new FlatMeshKit<AM>();
            kit.Add(LowPolyMesh.Icosphere("cranium", 1), Matrix4x4.TRS(new Vector3(0f, 0.7f, 0f), Quaternion.identity, new Vector3(1.2f, 0.9f, 1.4f)), AM.Bone);
            kit.Add(LowPolyMesh.TaperedPrism("snout", 6, 0.6f), Matrix4x4.TRS(new Vector3(0f, 0.5f, 0.9f), Quaternion.identity, new Vector3(0.7f, 0.5f, 1.4f)), AM.Bone);
            foreach (float side in new[] { -1f, 1f })
            {
                kit.Add(LowPolyMesh.Icosphere("socket", 0), Matrix4x4.TRS(new Vector3(side * 0.55f, 0.9f, 0.7f), Quaternion.identity, Vector3.one * 0.3f), AM.CheckerDark);
                kit.Add(LowPolyMesh.TaperedPrism("horn", 5, 0f), Matrix4x4.TRS(new Vector3(side * 0.9f, 1.1f, -0.2f), Quaternion.Euler(-30f, side * 70f, 0f), new Vector3(0.25f, 0.25f, 1.8f)), AM.Bone);
            }
            return kit;
        }

        // ---------- Burnout's Scorch Raceway (the Mesa Pass) ----------
        // An oval racetrack: a banked sandstone berm all round with red-and-white kerbs to bank off, four rock pillars in
        // the infield to crash into, scorched skid marks where it has been tearing round, a chequered start gate across the
        // way in, a pit canopy and stacks of fuel drums.
        private static void BuildScorchRaceway(Transform parent, Vector2 c, Random rng)
        {
            float toCamp = AngleTo(c, CampOrigin);
            const float rx = 26f, rz = 22f;
            Vector2 Oval(float degrees, float scale = 1f) =>
                c + new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad) * rx, Mathf.Sin(degrees * Mathf.Deg2Rad) * rz) * scale;

            Transform berm = Group("Banked Berm", parent);
            const int segments = 36;
            for (int i = 0; i < segments; i++)
            {
                float a = (i + 0.5f) * 360f / segments;
                if (InGap(a, (toCamp, 16f), (90f, 12f))) continue;
                Vector2 p = Oval(a), q = Oval(a + 360f / segments);
                Vector2 mid = (p + q) * 0.5f;
                Vector3 at = OnGround(mid.x, mid.y, 0.3f);
                Vector2 along = q - p;
                Quaternion facing = Quaternion.LookRotation(new Vector3(along.x, 0f, along.y)); // +Z along the track (anticlockwise), so -X faces the infield
                float length = along.magnitude + 0.35f;
                KitProp("RacewayBerm", BermKit, "Berm", berm, at, facing, new Vector3(1f, 1f, length / 4f), true, true);
                KitProp("RacewayKerb", KerbKit, "Kerb", berm, at + facing * new Vector3(-1.9f, 0f, 0f) + Vector3.up * 0.3f, facing, new Vector3(1f, 1f, length / 4f));
            }

            // Four rock pillars in the infield, with open dash lanes between.
            foreach (Vector2 o in new[] { new Vector2(-11f, 9f), new Vector2(11f, 9f), new Vector2(-11f, -9f), new Vector2(11f, -9f) })
                KitProp("RacewayPillar", () => Single(LowPolyMesh.TaperedPrism("p", 7, 0.82f), AM.Rock), "Crash Pillar", parent, OnGround(c.x + o.x, c.y + o.y, 0.6f),
                    Quaternion.Euler(-90f, Range(rng, 0f, 360f), 0f), new Vector3(2.6f, 2.4f, 6.5f), true);

            // Skid marks: three scorched laps and some burnt patches.
            Transform skids = Group("Skid Marks", parent);
            for (int lap = 0; lap < 3; lap++)
            {
                float scale = 0.55f + lap * 0.13f, start = Range(rng, 0f, 360f), sweep = Range(rng, 150f, 260f);
                for (float a = start; a < start + sweep; a += 6f)
                {
                    Vector2 p = Oval(a, scale), q = Oval(a + 6f, scale);
                    Vector2 mid = (p + q) * 0.5f, along = q - p;
                    KitProp("RacewaySkid", () => { var k = new FlatMeshKit<AM>(); k.Box(new Vector3(0f, 0.02f, 0f), new Vector3(0.7f, 0.04f, 1f), Quaternion.identity, AM.Scorch); return k; },
                        "Skid", skids, OnGround(mid.x, mid.y, -0.01f), Quaternion.LookRotation(new Vector3(along.x, 0f, along.y)), new Vector3(1f, 1f, along.magnitude + 0.2f));
                }
            }
            for (int i = 0; i < 6; i++)
                KitProp("ArenaScorchPatch", () => DiscKit(AM.Scorch), "Burnt Patch", skids, AroundSite(c, Range(rng, 0f, 360f), Range(rng, 3f, 17f), -0.03f),
                    Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), new Vector3(Range(rng, 1.5f, 3f), 1f, Range(rng, 1.2f, 2.4f)));

            // The start gate across the way in.
            // (The gate spans its local X, so it faces out of the infield and its beam runs along the track.)
            Vector2 gate = Oval(toCamp);
            KitProp("RacewayGate", GateKit, "Start Gate", parent, OnGround(gate.x, gate.y, 0.2f), Quaternion.LookRotation(new Vector3(gate.x - c.x, 0f, gate.y - c.y)), Vector3.one, true);

            // A pit canopy and fuel drums by the inside of the berm.
            KitProp("RacewayCanopy", CanopyKit, "Pit Canopy", parent, AroundSite(c, toCamp + 70f, 17f, 0.2f), Quaternion.Euler(0f, -(toCamp + 70f) + 90f, 0f), Vector3.one, true);
            Transform drums = Group("Fuel Drums", parent);
            for (int i = 0; i < 7; i++)
            {
                Vector3 at = AroundSite(c, toCamp + 70f + Range(rng, -14f, 14f), Range(rng, 13f, 15.5f), 0.05f);
                bool tipped = i % 3 == 2;
                KitProp("ArenaDrum", DrumKit, "Fuel Drum", drums, at + (tipped ? Vector3.up * 0.4f : Vector3.zero),
                    Quaternion.Euler(tipped ? 90f : 0f, Range(rng, 0f, 360f), 0f), Vector3.one, true, true);
            }
        }

        // A banked wall section 4 m long (scaled along Z): flat outside, a sloping inner face to bank off (inside is -X).
        private static FlatMeshKit<AM> BermKit()
        {
            var kit = new FlatMeshKit<AM>();
            float h = 1.7f, baseIn = -1.8f, topIn = -0.4f, outX = 0.9f, z = 2f;
            Vector3 A(float x, float y, float zz) => new Vector3(x, y, zz);
            kit.Quad(A(baseIn, 0f, -z), A(topIn, h, -z), A(topIn, h, z), A(baseIn, 0f, z), new Vector3(-1f, 0.6f, 0f), AM.Sandstone); // banked inner face
            kit.Quad(A(topIn, h, -z), A(outX, h, -z), A(outX, h, z), A(topIn, h, z), Vector3.up, AM.Rock);
            kit.Quad(A(outX, h, -z), A(outX, -0.3f, -z), A(outX, -0.3f, z), A(outX, h, z), Vector3.right, AM.Sandstone);
            kit.Quad(A(baseIn, 0f, -z), A(outX, -0.3f, -z), A(outX, h, -z), A(topIn, h, -z), Vector3.back, AM.Sandstone);
            kit.Quad(A(baseIn, 0f, z), A(topIn, h, z), A(outX, h, z), A(outX, -0.3f, z), Vector3.forward, AM.Sandstone);
            return kit;
        }

        // Red and white kerb blocks along the foot of the berm, 4 m (scaled along Z).
        private static FlatMeshKit<AM> KerbKit()
        {
            var kit = new FlatMeshKit<AM>();
            for (int i = 0; i < 4; i++)
                kit.Box(new Vector3(0f, 0f, -1.5f + i), new Vector3(0.6f, 0.12f, 1f), Quaternion.identity, i % 2 == 0 ? AM.Red : AM.Checker);
            return kit;
        }

        // Two hull posts, a crossbeam and a chequered banner; the way through runs along +Z... the gate spans X.
        private static FlatMeshKit<AM> GateKit()
        {
            var kit = new FlatMeshKit<AM>();
            foreach (float x in new[] { -5f, 5f })
            {
                kit.Box(new Vector3(x, 3f, 0f), new Vector3(0.8f, 6f, 0.8f), Quaternion.identity, AM.Hull);
                kit.Box(new Vector3(x, 0.4f, 0f), new Vector3(1.4f, 0.8f, 1.4f), Quaternion.identity, AM.Burnt);
            }
            kit.Box(new Vector3(0f, 6.2f, 0f), new Vector3(11f, 0.6f, 0.9f), Quaternion.identity, AM.Hull);
            for (int col = 0; col < 12; col++)
                for (int row = 0; row < 2; row++)
                    kit.Box(new Vector3(-4.125f + col * 0.75f, 5.55f - row * 0.7f, 0f), new Vector3(0.75f, 0.7f, 0.06f), Quaternion.identity,
                        (col + row) % 2 == 0 ? AM.Checker : AM.CheckerDark);
            return kit;
        }

        // A hull plate roof on four girder posts.
        private static FlatMeshKit<AM> CanopyKit()
        {
            var kit = new FlatMeshKit<AM>();
            foreach (Vector2 p in new[] { new Vector2(-2.2f, -1.6f), new Vector2(2.2f, -1.6f), new Vector2(-2.2f, 1.6f), new Vector2(2.2f, 1.6f) })
                kit.Box(new Vector3(p.x, 1.4f, p.y), new Vector3(0.25f, 2.8f, 0.25f), Quaternion.identity, AM.Girder);
            kit.Box(new Vector3(0f, 2.9f, 0f), new Vector3(5.4f, 0.12f, 4f), Quaternion.Euler(0f, 0f, 4f), AM.Hull);
            kit.Box(new Vector3(0f, 0.45f, 0.4f), new Vector3(1.8f, 0.9f, 0.9f), Quaternion.Euler(0f, 10f, 0f), AM.Burnt);
            return kit;
        }

        // A red fuel drum with rust bands, about 0.9 m tall.
        private static FlatMeshKit<AM> DrumKit()
        {
            var kit = new FlatMeshKit<AM>();
            kit.Add(LowPolyMesh.Lathe("drum", new[] { new Vector2(0f, 0f), new Vector2(0.4f, 0f), new Vector2(0.4f, 0.9f), new Vector2(0f, 0.9f) }, 10), Matrix4x4.identity, AM.Red);
            foreach (float y in new[] { 0.28f, 0.62f })
                kit.Add(LowPolyMesh.Lathe("band", new[] { new Vector2(0.405f, y - 0.03f), new Vector2(0.415f, y), new Vector2(0.405f, y + 0.03f) }, 10), Matrix4x4.identity, AM.Rust);
            return kit;
        }

        // A flat disc of radius 1 lying on the sand (scale it): oil pools, burnt patches.
        private static FlatMeshKit<AM> DiscKit(AM material)
        {
            var kit = new FlatMeshKit<AM>();
            kit.Add(LowPolyMesh.Lathe("disc", new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0.04f), new Vector2(0f, 0.05f) }, 11), Matrix4x4.identity, material);
            return kit;
        }

        // ---------- Sludge's Fuel Sump (the Buried Hull) ----------
        // A dip of oily sand round a huge buried fuel tank it has been draining. Pipes run from the tank across the sump
        // to three leaks (Cavendish's task), a rickety drilling derrick stands over it, and hull slabs and boulders give
        // cover from the fuel cannon. Pools of oil, drums, and a rubble ring with two ways in.
        private static void BuildFuelSump(Transform parent, Vector2 c, Random rng)
        {
            float toCamp = AngleTo(c, CampOrigin);
            float toBowl = AngleTo(c, BossArena.Center);
            for (int i = 0; i < 26; i++)
            {
                float a = i * (360f / 26f) + Range(rng, -4f, 4f);
                if (InGap(a, (toCamp, 22f), (toBowl, 16f))) continue;
                float s = Range(rng, 2.6f, 4f);
                KitProp("ArenaBoulder", () => Single(LowPolyMesh.Icosphere("b", 0), AM.Rock), "Rubble", parent, AroundSite(c, a, Range(rng, 26f, 29f), s * 0.3f),
                    Quaternion.Euler(Range(rng, 0f, 360f), Range(rng, 0f, 360f), Range(rng, 0f, 360f)), new Vector3(s * 1.2f, s * 0.7f, s), true, true);
            }

            // The buried tank, across the sump from the way in.
            float tankAngle = SumpTankAngle(c);
            Vector3 tankAt = AroundSite(c, tankAngle, 20.5f, 2.2f);
            KitProp("SumpTank", TankKit, "Buried Fuel Tank", parent, tankAt, Quaternion.Euler(0f, 90f - tankAngle, 12f), Vector3.one * 1.35f, true); // lying along the rim

            // The derrick, beside the tank.
            KitProp("SumpDerrick", DerrickKit, "Derrick", parent, AroundSite(c, tankAngle - 65f, 21f, 0.1f), Quaternion.Euler(0f, Range(rng, 0f, 90f), 0f), Vector3.one, true);

            // Cover from the fuel cannon: hull slabs and boulders spread round the sump.
            Transform cover = Group("Cover", parent);
            for (int i = 0; i < 5; i++)
            {
                float a = tankAngle + 35f + i * 72f, r = 9f + (i % 2) * 4f;
                if (i % 2 == 0)
                    KitProp("SumpSlab", () => { var k = new FlatMeshKit<AM>(); k.Box(new Vector3(0f, 1.2f, 0f), new Vector3(4.6f, 2.8f, 0.6f), Quaternion.identity, AM.Hull);
                            k.Box(new Vector3(0f, 2.62f, 0f), new Vector3(4.7f, 0.08f, 0.66f), Quaternion.identity, AM.Rust); return k; },
                        "Hull Slab", cover, AroundSite(c, a, r, 0.3f), Quaternion.Euler(0f, -a + Range(rng, -20f, 20f), Range(rng, -5f, 5f)), Vector3.one, true, true);
                else
                    KitProp("ArenaBoulder", () => Single(LowPolyMesh.Icosphere("b", 0), AM.Rock), "Boulder", cover, AroundSite(c, a, r, 0.6f),
                        Quaternion.Euler(Range(rng, 0f, 360f), Range(rng, 0f, 360f), 0f), new Vector3(2.3f, 1.9f, 2f), true, true);
            }

            // Oil pools and drums.
            Transform oil = Group("Oil", parent);
            for (int i = 0; i < 14; i++)
                KitProp("ArenaOilPool", () => DiscKit(AM.Oil), "Oil Pool", oil, AroundSite(c, Range(rng, 0f, 360f), Range(rng, 0f, 15f), -0.04f),
                    Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), new Vector3(Range(rng, 1.8f, 4f), 1f, Range(rng, 1.4f, 3.2f)));
            KitProp("ArenaOilPool", () => DiscKit(AM.Oil), "Tank Spill", oil, AroundSite(c, tankAngle, 14f, -0.04f), Quaternion.Euler(0f, -tankAngle, 0f), new Vector3(5f, 1f, 3f));
            for (int i = 0; i < 6; i++)
            {
                float a = tankAngle + Range(rng, -40f, 40f);
                bool tipped = i % 2 == 1;
                Vector3 at = AroundSite(c, a, Range(rng, 15f, 20f), 0.05f);
                KitProp("ArenaDrum", DrumKit, "Fuel Drum", oil, at + (tipped ? Vector3.up * 0.4f : Vector3.zero), Quaternion.Euler(tipped ? 90f : 0f, Range(rng, 0f, 360f), 0f), Vector3.one, true, true);
            }

            // Pipes from the tank to the three leaks.
            Transform pipes = Group("Pipes", parent);
            Vector3 tankMouth = AroundSite(c, tankAngle, 14.5f) + Vector3.up * 0.35f;
            foreach (float a in SumpLeakAngles(c))
            {
                Vector3 leak = AroundSite(c, a, SumpLeakRadius) + Vector3.up * 0.35f;
                var path = new List<Vector3>();
                for (int k = 0; k <= 8; k++)
                {
                    Vector3 p = Vector3.Lerp(tankMouth, leak, k / 8f);
                    p.y = Ground(p.x, p.z) + 0.3f;
                    path.Add(p);
                }
                Vector3 origin = path[0];
                for (int k = 0; k < path.Count; k++) path[k] -= origin;
                var kit = new FlatMeshKit<AM>();
                kit.Add(LowPolyMesh.Tube("pipe", path.ToArray(), 0.28f, 8), Matrix4x4.identity, AM.Rust);
                var go = new GameObject("Pipe");
                go.transform.SetParent(pipes, false);
                go.transform.position = origin;
                Mesh mesh = SaveKit(kit, "SumpPipe" + Mathf.RoundToInt(a));
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = arenaMats[AM.Rust];
            }
        }

        private const float SumpLeakRadius = 12f;
        private static float SumpTankAngle(Vector2 c) => AngleTo(c, CampOrigin) + 180f;
        private static float[] SumpLeakAngles(Vector2 c)
        {
            float tank = SumpTankAngle(c);
            return new[] { tank + 145f, tank + 215f, tank + 290f };
        }

        // A fuel tank 16 m long lying along X, half sunk, with rusty bands and a crack.
        private static FlatMeshKit<AM> TankKit()
        {
            var kit = new FlatMeshKit<AM>();
            Vector2[] profile = { new Vector2(0f, -8f), new Vector2(2.6f, -7.6f), new Vector2(3.6f, -6.6f), new Vector2(3.8f, -4f), new Vector2(3.8f, 4f),
                new Vector2(3.6f, 6.6f), new Vector2(2.6f, 7.6f), new Vector2(0f, 8f) };
            kit.Add(LowPolyMesh.Lathe("tank", profile, 12), Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 90f)), AM.Hull);
            foreach (float x in new[] { -4.5f, 0f, 4.5f })
                kit.Add(LowPolyMesh.Lathe("band", new[] { new Vector2(3.83f, -0.25f), new Vector2(3.9f, 0f), new Vector2(3.83f, 0.25f) }, 12),
                    Matrix4x4.TRS(new Vector3(x, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), Vector3.one), AM.Rust);
            kit.Box(new Vector3(1.5f, 1.2f, -3.7f), new Vector3(2.6f, 0.5f, 0.25f), Quaternion.Euler(0f, 0f, 25f), AM.Oil);
            kit.Add(LowPolyMesh.Lathe("hatch", new[] { new Vector2(0f, 3.7f), new Vector2(0.9f, 3.7f), new Vector2(0.9f, 4.1f), new Vector2(0f, 4.1f) }, 8), Matrix4x4.identity, AM.Brass);
            return kit;
        }

        // A rickety drilling derrick 10 m tall: four legs leaning in, cross braces and a platform.
        private static FlatMeshKit<AM> DerrickKit()
        {
            var kit = new FlatMeshKit<AM>();
            const float h = 10f, foot = 2.2f, top = 0.7f;
            Vector2[] corners = { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f) };
            for (int i = 0; i < 4; i++)
            {
                Vector3 bottom = new Vector3(corners[i].x * foot, 0f, corners[i].y * foot), up = new Vector3(corners[i].x * top, h, corners[i].y * top);
                kit.Add(LowPolyMesh.Tube("leg", new[] { bottom, up }, 0.14f, 4), Matrix4x4.identity, AM.Girder);
                Vector3 nextBottom = new Vector3(corners[(i + 1) % 4].x * foot, 0f, corners[(i + 1) % 4].y * foot);
                Vector3 nextUp = new Vector3(corners[(i + 1) % 4].x * top, h, corners[(i + 1) % 4].y * top);
                for (float k = 0.2f; k < 0.95f; k += 0.25f)
                {
                    kit.Add(LowPolyMesh.Tube("rung", new[] { Vector3.Lerp(bottom, up, k), Vector3.Lerp(nextBottom, nextUp, k) }, 0.07f, 4), Matrix4x4.identity, AM.Girder);
                    kit.Add(LowPolyMesh.Tube("brace", new[] { Vector3.Lerp(bottom, up, k), Vector3.Lerp(nextBottom, nextUp, k + 0.25f) }, 0.05f, 4), Matrix4x4.identity, AM.Rust);
                }
            }
            kit.Box(new Vector3(0f, h, 0f), new Vector3(2.2f, 0.2f, 2.2f), Quaternion.identity, AM.Hull);
            kit.Add(LowPolyMesh.Tube("drill", new[] { new Vector3(0f, h, 0f), new Vector3(0f, -1f, 0f) }, 0.12f, 6), Matrix4x4.identity, AM.Brass);
            return kit;
        }

        // =====================================================================
        // Trophies (Resources/SignalLost/Trophies, read by CampSave's Trophy list)
        // =====================================================================

        private static void BuildDesertTrophies()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/SignalLost")) AssetDatabase.CreateFolder("Assets/Resources", "SignalLost");
            if (!AssetDatabase.IsValidFolder(TrophyFolder)) AssetDatabase.CreateFolder("Assets/Resources/SignalLost", "Trophies");
            SaveTrophy("old_buzzard_feathers", "Old Buzzard's Tail Feathers", "TrophyBuzzardFeathers", FeatherFanKit);
            SaveTrophy("burnout_thorns", "Burnout's Thorn Crest", "TrophyBurnoutThorns", ThornCrestKit);
            SaveTrophy("sludge_stinger", "Sludge's Stinger", "TrophySludgeStinger", StingerKit);
            SaveTrophy("core_worm_fang", "Core Worm Fang", "TrophyCoreWormFang", FangKit);
        }

        // Each trophy rests on y = 0, centred, about 0.7 m across, like the Chief Toad's crown.
        private static void SaveTrophy(string id, string label, string meshName, Func<FlatMeshKit<AM>> build)
        {
            var holder = new GameObject(id);
            var model = KitProp(meshName, build, label, holder.transform, Vector3.zero, Quaternion.identity, null, false, false, false);
            Bounds bounds = model.GetComponent<MeshFilter>().sharedMesh.bounds;
            float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float scale = size > 0.01f ? 0.7f / size : 1f;
            model.transform.localScale = Vector3.one * scale;
            model.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z) * scale;
            PrefabUtility.SaveAsPrefabAsset(holder, TrophyFolder + "/" + id + ".prefab");
            Object.DestroyImmediate(holder);
        }

        // Old Buzzard's tail: seven long feathers fanned up from a bound knot.
        private static FlatMeshKit<AM> FeatherFanKit()
        {
            var kit = new FlatMeshKit<AM>();
            for (int i = 0; i < 7; i++)
            {
                float a = (i - 3) * 14f;
                Mesh feather = FeatherKit(1.6f).Build("f");
                kit.Add(feather, Matrix4x4.TRS(new Vector3(0f, 0.1f, 0f), Quaternion.Euler(-75f + Mathf.Abs(i - 3) * 6f, 0f, 0f) * Quaternion.Euler(0f, a, 0f), Vector3.one), AM.Feather);
            }
            kit.Add(LowPolyMesh.Icosphere("knot", 0), Matrix4x4.TRS(new Vector3(0f, 0.1f, 0f), Quaternion.identity, new Vector3(0.14f, 0.12f, 0.14f)), AM.Rust);
            kit.Add(LowPolyMesh.Lathe("base", new[] { new Vector2(0f, 0f), new Vector2(0.3f, 0f), new Vector2(0.25f, 0.08f), new Vector2(0f, 0.08f) }, 8), Matrix4x4.identity, AM.Stick);
            return kit;
        }

        // Burnout's crest: a curved scaled plate bristling with thorns, scorched at the tips.
        private static FlatMeshKit<AM> ThornCrestKit()
        {
            var kit = new FlatMeshKit<AM>();
            for (int i = 0; i < 5; i++)
            {
                float a = (i - 2) * 22f;
                Quaternion turn = Quaternion.Euler(0f, 0f, a);
                kit.Box(turn * new Vector3(0f, 0.6f, 0f), new Vector3(0.36f, 0.12f, 0.5f), turn, AM.Sandstone);
                float length = 0.5f - Mathf.Abs(i - 2) * 0.08f;
                kit.Add(LowPolyMesh.TaperedPrism("thorn", 5, 0f), Matrix4x4.TRS(turn * new Vector3(0f, 0.65f, 0f), turn * Quaternion.Euler(-90f, 0f, 0f), new Vector3(0.11f, 0.11f, length)), AM.Rock);
                kit.Add(LowPolyMesh.TaperedPrism("tip", 5, 0f), Matrix4x4.TRS(turn * new Vector3(0f, 0.65f + length * 0.62f, 0f), turn * Quaternion.Euler(-90f, 0f, 0f), new Vector3(0.045f, 0.045f, length * 0.4f)), AM.Scorch);
                kit.Add(LowPolyMesh.TaperedPrism("side", 5, 0f), Matrix4x4.TRS(turn * new Vector3(0f, 0.62f, 0.2f), turn * Quaternion.Euler(-60f, 0f, 0f), new Vector3(0.06f, 0.06f, 0.22f)), AM.Rock);
            }
            kit.Add(LowPolyMesh.Lathe("base", new[] { new Vector2(0f, 0f), new Vector2(0.32f, 0f), new Vector2(0.26f, 0.12f), new Vector2(0f, 0.12f) }, 8), Matrix4x4.identity, AM.Burnt);
            kit.Box(new Vector3(0f, 0.3f, 0f), new Vector3(0.14f, 0.4f, 0.14f), Quaternion.identity, AM.Burnt);
            return kit;
        }

        // Sludge's stinger: a curled amber tail tip ending in a black barb, on a little drum of fuel.
        private static FlatMeshKit<AM> StingerKit()
        {
            var kit = new FlatMeshKit<AM>();
            Vector3 previous = new Vector3(0f, 0.25f, 0f);
            for (int i = 0; i < 6; i++)
            {
                float k = i / 5f;
                Vector3 next = new Vector3(0f, 0.25f + Mathf.Sin(k * 2.2f) * 0.5f, Mathf.Sin(k * 1.6f) * 0.35f - k * k * 0.25f);
                float r = Mathf.Lerp(0.13f, 0.07f, k);
                kit.Add(LowPolyMesh.Icosphere("seg", 0), Matrix4x4.TRS(next, Quaternion.LookRotation(next - previous + Vector3.forward * 0.001f), new Vector3(r, r, r * 1.5f)), i % 2 == 0 ? AM.Amber : AM.Rust);
                previous = next;
            }
            kit.Add(LowPolyMesh.TaperedPrism("barb", 6, 0f), Matrix4x4.TRS(previous, Quaternion.Euler(110f, 0f, 0f), new Vector3(0.06f, 0.06f, 0.3f)), AM.CheckerDark);
            kit.Add(LowPolyMesh.Lathe("drum", new[] { new Vector2(0f, 0f), new Vector2(0.18f, 0f), new Vector2(0.18f, 0.24f), new Vector2(0f, 0.24f) }, 9), Matrix4x4.identity, AM.Red);
            return kit;
        }

        // A great curved fang with a vein of the core's orange glow, on a chunk of the worm's hide.
        private static FlatMeshKit<AM> FangKit()
        {
            var kit = new FlatMeshKit<AM>();
            Vector3 previous = new Vector3(0f, 0.12f, 0f);
            for (int i = 0; i < 6; i++)
            {
                float k = (i + 1) / 6f;
                Vector3 next = new Vector3(0f, 0.12f + k * 0.9f, k * k * 0.35f);
                float r = Mathf.Lerp(0.2f, 0.03f, k);
                kit.Add(LowPolyMesh.TaperedPrism("seg", 7, 0.8f), Matrix4x4.TRS(previous, Quaternion.LookRotation(next - previous), new Vector3(r, r * 0.8f, Vector3.Distance(previous, next) * 1.05f)), AM.Fang);
                if (i < 4) kit.Add(LowPolyMesh.TaperedPrism("vein", 4, 0.6f), Matrix4x4.TRS(previous + new Vector3(0f, 0f, -r * 0.55f), Quaternion.LookRotation(next - previous),
                    new Vector3(0.03f, 0.03f, Vector3.Distance(previous, next))), AM.Crystal);
                previous = next;
            }
            kit.Add(LowPolyMesh.Icosphere("hide", 0), Matrix4x4.TRS(new Vector3(0f, 0.08f, 0f), Quaternion.identity, new Vector3(0.36f, 0.16f, 0.3f)), AM.CaveRock);
            return kit;
        }

        // =====================================================================
        // The Core Worm's lair
        // =====================================================================

        private static float LairFloorY => Ground(LairCentre.x, LairCentre.y) - LairDepth;

        /// <summary>The cavern roof's height over a point (world x, z), rough with low-poly lumps.</summary>
        private static float DomeY(float localX, float localZ)
        {
            float r = new Vector2(localX, localZ).magnitude;
            float k = Mathf.Clamp01(r / DomeRadius);
            float lumps = Noise(localX, localZ, 9f, 4.1f, 7.7f) * 1.4f * Mathf.Sin(k * Mathf.PI);
            return DomeHeight * Mathf.Sqrt(Mathf.Max(0f, 1f - k * k)) + lumps;
        }

        private static GameObject BuildLairPrefab()
        {
            EnsureGrid();
            Vector2 c2 = LairCentre;
            float floorY = LairFloorY;
            Vector3 centre = new Vector3(c2.x, floorY, c2.y);
            var rng = new Random(20261112);

            var root = new GameObject("Core Worm Lair");
            root.transform.position = centre;
            var surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            root.AddComponent<NavMeshBaker>();
            Transform a = root.transform;
            Transform middle = new GameObject("Centre").transform;
            middle.SetParent(a, false);

            // The arena floor: 5 m tiles that fall away in phase 3, and a solid rim out to the cavern wall.
            Material floorMat = arenaMats[AM.CaveFloor];
            Transform floorGroup = Group("Floor", a);
            var tiles = new List<Transform>();
            for (float x = -32.5f; x <= 32.5f; x += 5f)
                for (float z = -32.5f; z <= 32.5f; z += 5f)
                    if (new Vector2(x, z).magnitude <= LairArenaRadius)
                    {
                        GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        tile.name = "Floor Tile";
                        tile.transform.SetParent(floorGroup, false);
                        tile.transform.localPosition = new Vector3(x, -0.5f, z);
                        tile.transform.localScale = new Vector3(5f, 1f, 5f);
                        tile.GetComponent<Renderer>().sharedMaterial = floorMat;
                        tiles.Add(tile.transform);
                    }
            KitProp("LairRim", RimKit, "Floor Rim", floorGroup, centre, Quaternion.identity, Vector3.one, true);

            // The wall: a ring of boulders, then the rocky dome over everything.
            Transform walls = Group("Walls", a);
            for (int i = 0; i < 30; i++)
            {
                float angle = i * 12f;
                Vector3 at = centre + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * Range(rng, 37f, 39f) + Vector3.up * 1.5f;
                KitProp("LairWallRock", () => Single(LowPolyMesh.Icosphere("w", 0), AM.CaveRock), "Wall Rock", walls, at,
                    Quaternion.Euler(Range(rng, -20f, 20f), angle + Range(rng, -15f, 15f), Range(rng, -20f, 20f)), new Vector3(5.5f, Range(rng, 4.5f, 6f), 3.5f), true, true);
            }
            BuildDome(a, centre);
            BuildShaft(a, centre);

            // Cover: cave pillars from floor to roof, and three slabs of the ship's hull it swallowed.
            Transform coverGroup = Group("Cover", a);
            var cover = new List<Transform>();
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f + 25f;
                Vector3 local = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 17f;
                float height = DomeY(local.x, local.z) + 1f;
                GameObject pillar = KitProp("LairPillar", PillarKit, "Cave Pillar", coverGroup, centre + local, Quaternion.Euler(0f, angle * 1.7f, 0f),
                    new Vector3(1f, height / 16f, 1f), true, true);
                cover.Add(pillar.transform);
            }
            for (int i = 0; i < 3; i++)
            {
                float angle = i * 120f + 45f;
                Vector3 at = centre + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 26f;
                GameObject slab = KitProp("LairHullSlab", () => { var k = new FlatMeshKit<AM>(); k.Box(new Vector3(0f, 1.75f, 0f), new Vector3(5f, 3.5f, 0.8f), Quaternion.identity, AM.Hull);
                        k.Box(new Vector3(-1.2f, 2.9f, 0.42f), new Vector3(1.6f, 0.4f, 0.05f), Quaternion.identity, AM.Rust); return k; },
                    "Hull Slab", coverGroup, at, Quaternion.Euler(0f, angle + 90f, 4f), Vector3.one, true, true);
                cover.Add(slab.transform);
            }

            // Glowing crystals of the core's energy round the walls and on the pillars, with warm lights.
            Transform glow = Group("Crystals", a);
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30f + Range(rng, -8f, 8f);
                Vector3 at = centre + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * Range(rng, 34.5f, 35.5f);
                KitProp("LairCrystals", CrystalKit, "Crystal Cluster", glow, at, Quaternion.Euler(Range(rng, -15f, 15f), Range(rng, 0f, 360f), Range(rng, -15f, 15f)),
                    Vector3.one * Range(rng, 0.9f, 1.5f));
            }
            foreach (Transform pillar in cover.GetRange(0, 6))
                KitProp("LairCrystals", CrystalKit, "Pillar Crystals", glow, pillar.position + (centre - pillar.position).normalized * 1.6f + Vector3.up * 0.1f,
                    Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), Vector3.one * 0.7f);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f + 22f;
                var light = new GameObject("Crystal Light").AddComponent<Light>();
                light.transform.SetParent(glow, false);
                light.transform.position = centre + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 31f + Vector3.up * 3.5f;
                light.type = LightType.Point;
                light.color = new Color(1f, 0.58f, 0.25f);
                light.range = 26f;
                light.intensity = 30f; // URP point lights fall off with the square of the distance
                light.shadows = LightShadows.None;
            }
            var heart = new GameObject("Cavern Glow").AddComponent<Light>();
            heart.transform.SetParent(glow, false);
            heart.transform.position = centre + Vector3.up * 12f;
            heart.type = LightType.Point;
            heart.color = new Color(1f, 0.72f, 0.5f);
            heart.range = 50f;
            heart.intensity = 60f;
            heart.shadows = LightShadows.None;

            // Old bones of things it ate, leaning on the walls.
            Transform bones = Group("Bones", a);
            for (int i = 0; i < 8; i++)
            {
                float angle = Range(rng, 0f, 360f);
                Vector3 at = centre + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * Range(rng, 33f, 35f);
                KitProp("ArenaRib", () => Single(LowPolyMesh.TaperedPrism("r", 5, 0.2f), AM.Bone), "Old Rib", bones, at,
                    Quaternion.Euler(-60f, angle + 180f + Range(rng, -20f, 20f), 0f), new Vector3(0.35f, 0.35f, Range(rng, 3f, 6f)));
            }

            // Where the player lands, the fodder, and the way out (hidden until the roof comes in).
            Transform drop = new GameObject("Player Drop").transform;
            drop.SetParent(a, false);
            drop.localPosition = new Vector3(0f, 0f, -24f);
            var spawnerObject = new GameObject("Fodder Spawner");
            spawnerObject.transform.SetParent(a, false);
            FodderSpawner spawner = LairSpawner(spawnerObject);

            Vector3 holeTop = OnGround(SinkholeCentre.x, SinkholeCentre.y);
            Vector2 lipWorld = LairCentre + LairExitDir * ApexDist;
            var hole = new GameObject("Hole").transform;
            hole.SetParent(a, false);
            hole.SetPositionAndRotation(holeTop, Quaternion.LookRotation(new Vector3(LairExitDir.x, 0f, LairExitDir.y)));
            CollapseParts collapse = BuildCollapse(root, centre);

            var arena = root.AddComponent<CorewormArena>();
            var data = new SerializedObject(arena);
            data.FindProperty("centre").objectReferenceValue = middle;
            data.FindProperty("radius").floatValue = LairArenaRadius;
            data.FindProperty("playerDrop").objectReferenceValue = drop;
            SetObjects(data.FindProperty("floorTiles"), tiles.ToArray());
            SetObjects(data.FindProperty("cover"), cover.ToArray());
            data.FindProperty("fodderSpawner").objectReferenceValue = spawner;
            data.FindProperty("warningMaterial").objectReferenceValue = DesertFodderBuilder.Unlit("BossWarning", new Color(1f, 0.3f, 0.15f));
            data.FindProperty("stormFog").colorValue = new Color(0.45f, 0.33f, 0.22f);
            data.ApplyModifiedPropertiesWithoutUndo();

            var lair = root.AddComponent<WormLair>();
            var lairData = new SerializedObject(lair);
            lairData.FindProperty("arena").objectReferenceValue = arena;
            lairData.FindProperty("surfaceY").floatValue = Ground(lipWorld.x, lipWorld.y);
            lairData.FindProperty("footprint").floatValue = DomeRadius + 2f;
            SetObjects(lairData.FindProperty("rocks"), collapse.Rocks.ToArray());
            lairData.FindProperty("bake").objectReferenceValue = collapse.Bake;
            lairData.FindProperty("mound").objectReferenceValue = collapse.Mound;
            lairData.FindProperty("pourPoint").vector3Value = collapse.PourPoint;
            lairData.FindProperty("reposeTan").floatValue = ReposeTan;
            lairData.FindProperty("holeSize").vector2Value = new Vector2(GashHalf * 2f, GashLength);
            lairData.FindProperty("ceilingCap").objectReferenceValue = collapse.Cap;
            lairData.FindProperty("chokeSand").objectReferenceValue = collapse.ChokeSand;
            lairData.FindProperty("crushZone").objectReferenceValue = collapse.Zone;
            lairData.FindProperty("crushOrigin").vector2Value = collapse.ZoneOrigin;
            lairData.FindProperty("crushCell").floatValue = ZoneCell;
            lairData.FindProperty("crushColumns").intValue = collapse.ZoneColumns;
            SerializedProperty cells = lairData.FindProperty("crushCells");
            cells.arraySize = collapse.ZoneCells.Length;
            for (int i = 0; i < collapse.ZoneCells.Length; i++) cells.GetArrayElementAtIndex(i).boolValue = collapse.ZoneCells[i];
            SerializedProperty climb = lairData.FindProperty("climbPath");
            climb.arraySize = collapse.Path.Count;
            for (int i = 0; i < collapse.Path.Count; i++) climb.GetArrayElementAtIndex(i).vector3Value = collapse.Path[i];
            lairData.FindProperty("exitPoint").objectReferenceValue = collapse.Exit;
            lairData.FindProperty("hole").objectReferenceValue = hole;
            lairData.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = EnemyTestBuilder.SavePrefab(root, LairPrefabPath);
            return saved;
        }

        private static void SetObjects(SerializedProperty property, Object[] items)
        {
            property.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        // The floor between the tiles and the cavern wall: a flat ring from 34.5 m to the dome's foot, local to the centre.
        private static FlatMeshKit<AM> RimKit()
        {
            var kit = new FlatMeshKit<AM>();
            const int sides = 48;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                kit.Quad(d0 * 35f, d1 * 35f, d1 * (DomeRadius + 2f), d0 * (DomeRadius + 2f), Vector3.up, AM.CaveFloor);
                kit.Quad(d0 * 35f + Vector3.down, d1 * 35f + Vector3.down, d1 * 35f, d0 * 35f, -d0, AM.CaveRock);
            }
            return kit;
        }

        private const int DomeSectors = 28;

        private static Vector3 DomePoint(float r, int sector)
        {
            float angle = sector * Mathf.PI * 2f / DomeSectors;
            float x = Mathf.Cos(angle) * r, z = Mathf.Sin(angle) * r;
            return new Vector3(x, DomeY(x, z), z);
        }

        // The cavern roof: rings of lumpy rock panels from the top down to the floor, seen from inside and out (the
        // gash looks down on it). It is open under the gash; a ragged collar of rock round the opening covers the seam
        // between the gash's walls and the roof's panels.
        private static void BuildDome(Transform parent, Vector3 centre)
        {
            float[] rings = { 0f, 5f, 9f, 13f, 17f, 21f, 25f, 29f, 33f, 36.5f, 40f, DomeRadius };
            float open = GashHalf + ShaftMargin + 0.3f;
            var kit = new FlatMeshKit<AM>();
            for (int ring = 0; ring < rings.Length - 1; ring++)
                for (int s = 0; s < DomeSectors; s++)
                {
                    Vector3 a = DomePoint(rings[ring], s), b = DomePoint(rings[ring], s + 1), cc = DomePoint(rings[ring + 1], s + 1), d = DomePoint(rings[ring + 1], s);
                    Vector3 mid = (a + b + cc + d) * 0.25f;
                    // Nothing may poke into the opening: any panel touching it goes (the collar closes what that leaves).
                    bool touches = false;
                    foreach (Vector3 corner in new[] { a, b, cc, d, mid, (a + b) * 0.5f, (b + cc) * 0.5f, (cc + d) * 0.5f, (d + a) * 0.5f })
                        if (GashDistance(new Vector2(corner.x, corner.z)) < open) touches = true;
                    if (touches) continue;
                    AddBothSides(kit, a, b, cc, d, mid);
                }
            const int sides = 32;
            var rng = new Random(31);
            float[] reach = new float[sides + 1];
            for (int i = 0; i < sides; i++) reach[i] = 7.5f + Range(rng, -0.8f, 1.5f);
            reach[sides] = reach[0];
            for (int i = 0; i < sides; i++)
            {
                Vector2 in0 = GashOutline(i, sides, ShaftMargin - 0.2f), in1 = GashOutline(i + 1, sides, ShaftMargin - 0.2f);
                Vector2 out0 = GashOutline(i, sides, ShaftMargin + reach[i]), out1 = GashOutline(i + 1, sides, ShaftMargin + reach[i + 1]);
                Vector3 P(Vector2 p, float drop) => new Vector3(p.x, DomeY(p.x, p.y) - drop, p.y);
                Vector3 A = P(in0, 0.9f), B = P(in1, 0.9f), C = P(out1, 0.15f), D = P(out0, 0.15f);
                AddBothSides(kit, A, B, C, D, (A + B + C + D) * 0.25f);
            }
            GameObject dome = KitProp("LairDome", () => kit, "Cavern Roof", parent, centre, Quaternion.identity);
            dome.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
        }

        private static void AddBothSides(FlatMeshKit<AM> kit, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 mid)
        {
            Vector3 inward = -new Vector3(mid.x, mid.y - DomeHeight * 0.2f, mid.z); // toward the cavern's middle
            kit.Quad(a, b, c, d, inward, AM.CaveRock);
            kit.Quad(a, b, c, d, -inward, AM.CaveRock);
        }

        // The gash's walls (11-10): from the hole's very edge in the dunes (the points the dunes are cut along, so there is
        // no gap between them) down to the opening in the cavern roof. At the top the sand rolls over a rounded brink in
        // the dunes' own colours, then turns to sandstone and, lower down, the cavern's rock, flaring out to meet the
        // roof. Rocks bounce off them and the player climbing the debris is kept inside them.
        private static void BuildShaft(Transform parent, Vector3 centre)
        {
            var kit = new FlatMeshKit<AM>();
            foreach ((Vector3 p, Vector3 q) in HoleEdge())
            {
                Vector3[] a = WallProfile(p - centre, centre), b = WallProfile(q - centre, centre);
                AM top = SandKind(p);
                for (int k = 0; k < a.Length - 1; k++)
                {
                    AM material = k == 0 ? top : k == 1 ? (top == AM.Sand ? AM.DuneShade : top) : k < 4 ? AM.Sandstone : AM.CaveRock;
                    Vector3 mid = (a[k] + b[k] + a[k + 1] + b[k + 1]) * 0.25f;
                    Vector2 axis = GashAxisPoint(new Vector2(mid.x, mid.z));
                    var inward = new Vector3(axis.x - mid.x, 0f, axis.y - mid.z);
                    kit.Quad(a[k], b[k], b[k + 1], a[k + 1], inward, material);
                    kit.Quad(a[k], b[k], b[k + 1], a[k + 1], -inward, material);
                }
            }
            GameObject shaft = KitProp("LairShaft", () => kit, "Shaft", parent, centre, Quaternion.identity, Vector3.one, true);
            shaft.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
        }

        /// <summary>The nearest point of the gash's centre line (relative to the lair).</summary>
        private static Vector2 GashAxisPoint(Vector2 local)
        {
            Vector2 a = LairExitDir * (ApexDist - GashHalf), b = LairExitDir * (ApexDist - GashLength + GashHalf);
            Vector2 ab = b - a;
            return a + ab * Mathf.Clamp01(Vector2.Dot(local - a, ab) / ab.sqrMagnitude);
        }

        // The wall's profile down from one point of the hole's edge (relative to the lair): the rounded brink, the
        // sandstone, and the rock flaring out to the roof's opening. Lumps come from the point's position, so neighbours
        // along the edge share them.
        private static Vector3[] WallProfile(Vector3 p, Vector3 centre)
        {
            Vector2 flat = new Vector2(p.x, p.z), axis = GashAxisPoint(flat);
            Vector2 inward = (axis - flat).normalized;
            Vector3 n = new Vector3(inward.x, 0f, inward.y);
            float lump = Noise(p.x, p.z, 4f, 1.7f, 3.3f), lump2 = Noise(p.x, p.z, 2.5f, 7.1f, 0.4f);
            Vector3 Out(float radius, float y)
            {
                Vector2 at = axis - inward * radius;
                return new Vector3(at.x, y, at.y);
            }
            float roof = DomeY(axis.x - inward.x * (GashHalf + ShaftMargin - 0.4f), axis.y - inward.y * (GashHalf + ShaftMargin - 0.4f)) - 1.2f;
            Vector3 low = p + n * (0.55f + lump * 0.2f) + Vector3.down * 2.8f;
            return new[]
            {
                p,
                p + n * 0.28f + Vector3.down * 0.2f,
                p + n * 0.5f + Vector3.down * 0.6f,
                p + n * (0.6f + lump2 * 0.15f) + Vector3.down * 1.4f,
                low,
                Out(GashHalf + 1.3f + lump * 0.6f, Mathf.Lerp(low.y, roof, 0.5f)),
                Out(GashHalf + ShaftMargin - 0.4f, roof),
            };
        }

        // The dunes' colour at a point of the hole's edge: flat sand, slope or rock, the way the terrain picks it.
        private static AM SandKind(Vector3 world)
        {
            const float d = 0.5f;
            var normal = new Vector3(Ground(world.x - d, world.z) - Ground(world.x + d, world.z), 2f * d, Ground(world.x, world.z - d) - Ground(world.x, world.z + d)).normalized;
            float slope = Mathf.Acos(Mathf.Clamp01(normal.y)) * Mathf.Rad2Deg;
            return slope < 16f ? AM.Sand : slope < 38f ? AM.DuneShade : AM.Sandstone;
        }

        /// <summary>How far from the gash's centre line its wall stands, at a height (relative to the lair) over a point.</summary>
        private static float WallRadius(Vector2 local, float y, Vector3 centre)
        {
            float ground = Ground(centre.x + local.x, centre.z + local.y) - centre.y;
            float top = ground - 2.8f, bottom = DomeY(local.x, local.y) - 1.2f;
            if (y >= top) return GashHalf;
            return Mathf.Lerp(GashHalf, GashHalf + ShaftMargin - 0.4f, Mathf.InverseLerp(top, bottom, y));
        }
        // A point on the gash's outline grown to `radius` from its centre line, `u` of the way round it (0..1, by
        // distance, so the straight sides are sampled as finely as the round ends). Relative to the lair.
        private static Vector2 StadiumPoint(float u, float radius)
        {
            Vector2 a = LairExitDir * (ApexDist - GashHalf), b = LairExitDir * (ApexDist - GashLength + GashHalf);
            float straight = (a - b).magnitude, arc = Mathf.PI * radius, total = 2f * straight + 2f * arc;
            float d = Mathf.Repeat(u, 1f) * total;
            Vector2 along = LairExitDir, across = LairAcross;
            if (d < straight) return b + along * d + across * radius;
            d -= straight;
            if (d < arc) { float t = d / radius; return a + across * (radius * Mathf.Cos(t)) + along * (radius * Mathf.Sin(t)); }
            d -= arc;
            if (d < straight) return a - along * d - across * radius;
            d -= straight;
            float s = d / radius;
            return b - across * (radius * Mathf.Cos(s)) - along * (radius * Mathf.Sin(s));
        }

        /// <summary>
        /// The sand packed in round the choked rocks (11-10). It fills the gash from the cracked face down to just below
        /// the lowest rocks, so from the cavern you see a sandy ceiling with the rocks' undersides bedded in it, not rocks
        /// hanging in the air. When the roof gives way it crumbles and falls piece by piece (WormLair), each patch just
        /// as the rocks in it come loose: the mesh's third UV holds every piece's middle, its fourth when (from the
        /// start of the fall) and how fast it goes.
        /// </summary>
        private static MeshFilter BuildChokeSand(Transform group, Vector3 centre, Random rng, List<Transform> rocks, CollapseBake bake)
        {
            const int around = 72, rings = 14;
            float outer = GashHalf + ShaftMargin + 0.3f;
            var bedded = new List<(Vector3 at, float radius)>();
            foreach (Transform rock in rocks) bedded.Add((rock.position - centre, rock.localScale.x * 0.46f));
            // The sand's underside follows the rocks: no higher than the middle of the lowest rock near, so every rock is
            // at least half bedded in it (none hangs in the air), and it rises gently away from them.
            Vector3 Point(int ring, int s)
            {
                float radius = Mathf.Lerp(0f, outer, ring / (float)rings);
                Vector2 q = StadiumPoint(s / (float)around, radius);
                float edge = GashDistance(q);
                float bulge = 4.2f * Mathf.Max(0f, 1f - (edge / (GashHalf + 2f)) * (edge / (GashHalf + 2f)));
                float floorOf = Mathf.Max(DomeY(q.x, q.y) - bulge, MoundHeight(q) + 0.4f) + 0.1f;
                float cap = Ground(centre.x + q.x, centre.z + q.y) - centre.y - 1.0f;
                float under = cap;
                foreach ((Vector3 at, float r) in bedded)
                {
                    float d = Mathf.Max(0f, new Vector2(at.x - q.x, at.z - q.y).magnitude - r * 0.6f);
                    under = Mathf.Min(under, at.y + d * d * 0.8f);
                }
                under += Noise(q.x, q.y, 1.6f, 4.4f, 9.1f) * 0.12f;
                return new Vector3(q.x, Mathf.Clamp(under, Mathf.Min(floorOf, cap), cap), q.y);
            }
            var starts = new List<(Vector2 at, float time)>();
            for (int i = 0; i < rocks.Count && i < bake.RockCount; i++)
            {
                Vector3 local = rocks[i].position - centre;
                starts.Add((new Vector2(local.x, local.z), bake.StartTime(i)));
            }
            float When(Vector3 middle)
            {
                var flat = new Vector2(middle.x, middle.z);
                float near = float.MaxValue, nearest = 0f, best = float.MaxValue;
                foreach ((Vector2 at, float time) in starts)
                {
                    float d = (at - flat).magnitude;
                    if (d < 2.5f) near = Mathf.Min(near, time);
                    if (d < best) { best = d; nearest = time; }
                }
                return near < float.MaxValue ? near : nearest;
            }
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var middles = new List<Vector3>();
            var timing = new List<Vector2>();
            var triangles = new List<int>();
            void Piece(Vector3 a, Vector3 b, Vector3 c)
            {
                if (Vector3.Cross(b - a, c - a).sqrMagnitude < 1e-8f) return;
                Vector3 middle = (a + b + c) / 3f;
                var when = new Vector2(Mathf.Max(0f, When(middle) - 0.6f + Range(rng, 0f, 0.9f)), Range(rng, 0.45f, 0.8f));
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                if (n.y > 0f) { (b, c) = (c, b); n = -n; }
                // Both faces: the underside the cavern sees, and the top, seen from above once the lid has gone.
                foreach ((Vector3 x, Vector3 y, Vector3 z, Vector3 normal) in new[] { (a, b, c, n), (a, c, b, -n) })
                {
                    int start = vertices.Count;
                    vertices.Add(x); vertices.Add(y); vertices.Add(z);
                    for (int k = 0; k < 3; k++) { normals.Add(normal); middles.Add(middle); timing.Add(when); }
                    triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                }
            }
            for (int ring = 0; ring < rings; ring++)
                for (int s = 0; s < around; s++)
                {
                    Vector3 a = Point(ring, s), b = Point(ring, s + 1), c = Point(ring + 1, s + 1), d = Point(ring + 1, s);
                    Piece(a, b, c);
                    Piece(a, c, d);
                }
            var mesh = new Mesh { name = "LairChokeSand" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(2, middles);
            mesh.SetUVs(3, timing);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh = SaveMesh(mesh);
            var sand = new GameObject("Choke Sand");
            sand.transform.SetParent(group, false);
            sand.transform.position = centre;
            var filter = sand.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = sand.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = arenaMats[AM.Sand];
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return filter;
        }

        // A tall rock column, narrower in the middle, 16 m (scaled to reach the roof).
        private static FlatMeshKit<AM> PillarKit()
        {
            var kit = new FlatMeshKit<AM>();
            kit.Add(LowPolyMesh.Lathe("pillar", new[] { new Vector2(0f, -0.5f), new Vector2(2.3f, -0.5f), new Vector2(1.9f, 2f), new Vector2(1.4f, 6f), new Vector2(1.3f, 10f),
                new Vector2(1.7f, 14f), new Vector2(2.6f, 16f), new Vector2(0f, 16f) }, 8), Matrix4x4.identity, AM.CaveRock);
            return kit;
        }

        // A cluster of glowing crystals sprouting from a rock.
        private static FlatMeshKit<AM> CrystalKit()
        {
            var kit = new FlatMeshKit<AM>();
            kit.Add(LowPolyMesh.Icosphere("base", 0), Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.9f, 0.5f, 0.8f)), AM.CaveRock);
            float[] tilts = { 0f, 25f, -30f, 40f, -15f };
            for (int i = 0; i < tilts.Length; i++)
                kit.Add(LowPolyMesh.TaperedPrism("crystal", 6, 0f), Matrix4x4.TRS(new Vector3(0f, 0.2f, 0f), Quaternion.Euler(-90f + tilts[i], i * 72f, 0f),
                    new Vector3(0.22f, 0.22f, 1.6f - Mathf.Abs(tilts[i]) * 0.015f)), AM.Crystal);
            return kit;
        }

        // ---------- The collapse ----------

        private sealed class CollapseParts
        {
            public readonly List<Transform> Rocks = new List<Transform>();
            public CollapseBake Bake;
            public MeshFilter Mound;
            public Vector3 PourPoint;
            public readonly List<Vector3> Path = new List<Vector3>();
            public GameObject Cap;
            public MeshFilter ChokeSand;
            public MeshFilter Zone;
            public Vector2 ZoneOrigin;
            public int ZoneColumns;
            public bool[] ZoneCells;
            public Transform Exit;
        }

        private const float MoundCell = 1.25f, ZoneCell = 1.5f, BakeFrame = 1f / 30f;
        // The red zone: debris deeper than this buries you; a rock's danger reaches this far past it; and nothing may
        // come further toward the far (south) side of the arena than this line (metres along the gash's direction from
        // the middle), so there is always plenty of floor to stand on.
        private const float BuryDepth = 4f, RockMargin = 0.5f, SafeLine = -10f;
        private const string BakePath = Root + "/CorewormCollapse.asset";
        private const string JamName = "Jammed Rocks", MoundName = "Debris Cone", CollapseName = "Collapse";

        private static Vector3 RandomUnit(Random rng)
        {
            for (int i = 0; i < 20; i++)
            {
                var v = new Vector3(Range(rng, -1f, 1f), Range(rng, -1f, 1f), Range(rng, -1f, 1f));
                if (v.sqrMagnitude > 0.05f && v.sqrMagnitude <= 1f) return v.normalized;
            }
            return Vector3.up;
        }

        // An angular chunk of broken rock: a ball squared off toward a block, a little lumpy, then sliced by a few random
        // flat cuts. Triangles come out three points each, with whether each lies on a cut (a fresh break, paler) or on
        // the old weathered skin. About <paramref name="size"/> across, centred on the origin.
        private static void RockSoup(Random rng, Vector3 size, List<Vector3> points, List<bool> fresh, int detail = 1)
        {
            Mesh sphere = LowPolyMesh.Icosphere("rock", detail);
            Vector3[] source = sphere.vertices;
            int[] triangles = sphere.triangles;
            Object.DestroyImmediate(sphere);
            int cuts = rng.Next(7, 12);
            var normals = new Vector3[cuts];
            var depths = new float[cuts];
            for (int c = 0; c < cuts; c++)
            {
                normals[c] = RandomUnit(rng);
                depths[c] = Range(rng, 0.38f, 0.74f);
            }
            float seed = Range(rng, 0f, 100f);
            Vector3 Shape(Vector3 v, out bool cut)
            {
                float m = Mathf.Max(Mathf.Abs(v.x), Mathf.Max(Mathf.Abs(v.y), Mathf.Abs(v.z)));
                v = Vector3.Lerp(v, v / m, 0.5f);
                v *= 0.9f + 0.2f * Mathf.PerlinNoise(v.x * 1.7f + seed, v.y * 1.7f + v.z * 1.3f + seed * 0.4f);
                cut = false;
                for (int c = 0; c < cuts; c++)
                {
                    float over = Vector3.Dot(v, normals[c]) - depths[c];
                    if (over <= 0f) continue;
                    v -= normals[c] * over;
                    cut = true;
                }
                return Vector3.Scale(v, size * 0.5f);
            }
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = Shape(source[triangles[i]], out bool ca), b = Shape(source[triangles[i + 1]], out bool cb), c = Shape(source[triangles[i + 2]], out bool cc);
                if (Vector3.Cross(b - a, c - a).sqrMagnitude < 1e-10f) continue;
                points.Add(a);
                points.Add(b);
                points.Add(c);
                fresh.Add(ca && cb && cc);
            }
        }

        private static readonly Vector3[] RockShapes =
        {
            new Vector3(1f, 0.7f, 0.85f),  // block
            new Vector3(1f, 0.32f, 0.75f), // slab
            new Vector3(0.95f, 0.9f, 1f),  // chunk
            new Vector3(1f, 0.45f, 0.5f),  // shard
            new Vector3(0.8f, 0.55f, 1f),  // wedge
        };

        // The sand the debris cone hangs from: the dunes' own surface where they are still there at the lip end of the
        // gash (x, z relative to the lair; y the sand's height above the lair's floor). Sand slumps away from it.
        private static List<Vector3> lipSet;
        private static float lipTop;

        private static void PrepareLip(Vector3 centre)
        {
            lipSet = new List<Vector3>();
            lipTop = float.MinValue;
            float half = GashHalf + ShaftMargin;
            for (float s = -ShaftMargin; s <= 3.5f; s += 0.5f)
                for (float v = -half; v <= half; v += 0.5f)
                {
                    Vector2 p = LairExitDir * (ApexDist - s) + LairAcross * v;
                    if (GashDistance(p) > half - 0.4f || InLid(centre.x + p.x, centre.z + p.y)) continue;
                    float y = Ground(centre.x + p.x, centre.z + p.y) - centre.y - 0.15f;
                    lipSet.Add(new Vector3(p.x, y, p.y));
                    lipTop = Mathf.Max(lipTop, y);
                }
            EnemyTestBuilder.Require(lipSet.Count > 20, "The gash has no lip for the debris to climb to.");
        }

        /// <summary>
        /// The debris cone's surface height at a point (x, z relative to the lair; height above its floor), or below zero
        /// where there is none. Sand and rubble slumped away from the lip at their natural slope, the way a talus cone
        /// leans against the wall it fell from: level with the dunes at the lip, falling away in every direction from
        /// there, lumpy, and held under the cavern roof outside the gash.
        /// </summary>
        private static float MoundHeight(Vector2 p)
        {
            float best = float.NegativeInfinity;
            foreach (Vector3 q in lipSet)
            {
                float dx = p.x - q.x, dz = p.y - q.z;
                float h = q.y - Mathf.Sqrt(dx * dx + dz * dz) * ReposeTan;
                if (h > best) best = h;
            }
            float rough = Mathf.Clamp01((lipTop - best) / 2.5f);
            best += (Noise(p.x, p.y, 5.5f, 3.1f, 8.2f) * 0.3f + Noise(p.x, p.y, 2.1f, 9.4f, 1.3f) * 0.1f) * rough;
            if (GashDistance(p) >= GashHalf + ShaftMargin - 0.4f)
            {
                if (p.magnitude > DomeRadius - 1.5f) return -1f;
                best = Mathf.Min(best, DomeY(p.x, p.y) - 0.8f);
            }
            return best;
        }

        /// <summary>
        /// Everything the collapse needs. The debris cone (hidden until the sand pours), the cracked face and the broken
        /// rock choking the gash are built in place; then the rockfall is simulated with real physics and recorded
        /// (<see cref="BakeCollapse"/>), which also finds the route up through the settled rocks. Last come the things
        /// that depend on where the rocks ended up: small half-buried stones dressing the cone (none on the route), and
        /// the red zone covering the cone and everywhere a rock lands or rolls.
        /// </summary>
        private static CollapseParts BuildCollapse(GameObject root, Vector3 centre)
        {
            var parts = new CollapseParts();
            var rng = new Random(20261115);
            Transform group = Group(CollapseName, root.transform);
            float GroundAt(Vector2 q) => Ground(centre.x + q.x, centre.z + q.y) - centre.y;
            PrepareLip(centre);
            Vector2 apex = LairExitDir * ApexDist;
            parts.PourPoint = new Vector3(HoleLocal.x, 0f, HoleLocal.y);

            // The cone as a grid of heights.
            float reach = (lipTop + 1f) / ReposeTan + 3f;
            Vector2 min = apex - Vector2.one * reach;
            int cols = Mathf.CeilToInt(reach * 2f / MoundCell), rows = cols;
            var heights = new float[cols + 1, rows + 1];
            for (int i = 0; i <= cols; i++)
                for (int j = 0; j <= rows; j++)
                    heights[i, j] = MoundHeight(min + new Vector2(i, j) * MoundCell);
            Vector3 V(int i, int j) => new Vector3(min.x + i * MoundCell, Mathf.Max(heights[i, j], -0.4f), min.y + j * MoundCell);
            bool Solid(int i, int j) => Mathf.Max(Mathf.Max(heights[i, j], heights[i + 1, j]), Mathf.Max(heights[i, j + 1], heights[i + 1, j + 1])) > 0.05f;

            // What you walk on (and what the rocks land on): the plain surface.
            var walkVertices = new List<Vector3>();
            var walkTriangles = new List<int>();
            for (int i = 0; i <= cols; i++)
                for (int j = 0; j <= rows; j++)
                    walkVertices.Add(V(i, j));
            int Index(int i, int j) => i * (rows + 1) + j;
            for (int i = 0; i < cols; i++)
                for (int j = 0; j < rows; j++)
                {
                    if (!Solid(i, j)) continue;
                    walkTriangles.AddRange(new[] { Index(i, j), Index(i, j + 1), Index(i + 1, j + 1), Index(i, j), Index(i + 1, j + 1), Index(i + 1, j) });
                }
            var walkMesh = new Mesh { name = "LairDebrisCollider", indexFormat = IndexFormat.UInt32 };
            walkMesh.SetVertices(walkVertices);
            walkMesh.SetTriangles(walkTriangles, 0);
            walkMesh.RecalculateNormals();
            walkMesh.RecalculateBounds();
            walkMesh = SaveMesh(walkMesh);

            var mound = new GameObject(MoundName);
            mound.transform.SetParent(group, false);
            mound.transform.position = centre;
            parts.Mound = mound.AddComponent<MeshFilter>();
            mound.AddComponent<MeshRenderer>().sharedMaterials = new[] { arenaMats[AM.Debris], arenaMats[AM.RubbleDark], arenaMats[AM.RubbleDust] };
            var moundCollider = mound.AddComponent<MeshCollider>();
            moundCollider.sharedMesh = walkMesh;
            moundCollider.sharedMaterial = RockPhysics();

            // The cracked rock face holding it all up, just under the sand lid.
            {
                var kit = new FlatMeshKit<AM>();
                const int sides = 32, rings = 3;
                Vector3 C(int ring, int s)
                {
                    Vector2 q = GashOutline(s, sides, (GashHalf + ShaftMargin + 0.5f) * ring / rings - GashHalf);
                    return new Vector3(q.x, GroundAt(q) - 0.9f - Mathf.Abs(Noise(q.x, q.y, 3f, 2.3f, 5.1f)) * 0.5f, q.y);
                }
                for (int ring = 0; ring < rings; ring++)
                    for (int s = 0; s < sides; s++)
                    {
                        Vector3 a = C(ring, s), b = C(ring, s + 1), c = C(ring + 1, s + 1), d = C(ring + 1, s);
                        kit.Quad(a, b, c, d, Vector3.down, AM.CaveRock);
                        kit.Quad(a, b, c, d, Vector3.up, AM.CaveRock);
                    }
                parts.Cap = KitProp("LairCollapseCap", () => kit, "Cracked Face", group, centre, Quaternion.identity, Vector3.one, false, false, false);
                parts.Cap.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
            }

            // The broken rock choking the gash: angular blocks, slabs and shards in a few shapes, scaled and turned, packed
            // (big ones first) so none touch. Each starts above where the cone's surface will be, so none ends up inside it.
            const int variants = 10;
            var meshes = new Mesh[variants];
            for (int v = 0; v < variants; v++)
            {
                var points = new List<Vector3>();
                var fresh = new List<bool>();
                RockSoup(rng, RockShapes[v % RockShapes.Length], points, fresh);
                var kit = new FlatMeshKit<AM>();
                for (int t = 0; t < fresh.Count; t++)
                {
                    Vector3 a = points[t * 3], b = points[t * 3 + 1], c = points[t * 3 + 2];
                    Vector3 n = Vector3.Cross(b - a, c - a);
                    if (Vector3.Dot(n, a + b + c) < 0f) n = -n;
                    kit.Tri(a, b, c, n, fresh[t] ? AM.RubbleDust : AM.RubbleDark);
                }
                meshes[v] = SaveKit(kit, "LairFallingRock" + (v + 1));
            }
            var sizes = new List<float>();
            for (int i = 0; i < 330; i++) sizes.Add(i < 14 ? Range(rng, 2.3f, 3.2f) : i < 100 ? Range(rng, 1.3f, 2.1f) : Range(rng, 0.6f, 1.2f));
            var placed = new List<(Vector3 at, float radius)>();
            var rockSizes = new List<float>();
            Transform jam = Group(JamName, group);
            PhysicsMaterial physics = RockPhysics();
            foreach (float size in sizes)
            {
                float radius = size * 0.46f;
                for (int attempt = 0; attempt < 400; attempt++)
                {
                    Vector2 q = HoleLocal + LairExitDir * Range(rng, -GashLength * 0.5f - 1.5f, GashLength * 0.5f + 1.5f) + LairAcross * Range(rng, -GashHalf - 1.5f, GashHalf + 1.5f);
                    float edge = GashDistance(q);
                    if (edge + radius > GashHalf + ShaftMargin - 1f) continue;
                    float bulge = 4.2f * Mathf.Max(0f, 1f - (edge / (GashHalf + 2f)) * (edge / (GashHalf + 2f)));
                    float low = Mathf.Max(DomeY(q.x, q.y) - bulge, MoundHeight(q) + 0.4f) + radius, high = GroundAt(q) - 1.5f - radius;
                    if (high < low) continue;
                    var at = new Vector3(q.x, Range(rng, low, high), q.y);
                    if (edge + radius > WallRadius(q, at.y + radius * 0.7f, centre) - 0.3f) continue; // inside the walls
                    bool clear = true;
                    foreach ((Vector3 other, float otherRadius) in placed)
                        if ((other - at).sqrMagnitude < (radius + otherRadius) * (radius + otherRadius)) { clear = false; break; }
                    if (!clear) continue;
                    placed.Add((at, radius));
                    var rock = new GameObject("Rock " + placed.Count);
                    rock.transform.SetParent(jam, false);
                    rock.transform.SetPositionAndRotation(centre + at, Quaternion.Euler(Range(rng, 0f, 360f), Range(rng, 0f, 360f), Range(rng, 0f, 360f)));
                    rock.transform.localScale = Vector3.one * size;
                    Mesh mesh = meshes[placed.Count % variants];
                    rock.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var materials = new Material[mesh.subMeshCount];
                    for (int m = 0; m < materials.Length; m++) materials[m] = arenaMats[FlatMeshKit<AM>.MaterialOf(mesh, m)];
                    rock.AddComponent<MeshRenderer>().sharedMaterials = materials;
                    var hull = rock.AddComponent<MeshCollider>();
                    hull.sharedMesh = mesh;
                    hull.convex = true;
                    hull.sharedMaterial = physics;
                    rockSizes.Add(size);
                    break;
                }
            }

            // The rockfall itself: simulated, checked and recorded.
            BakeResult baked = BakeCollapse(root, jam, centre, rockSizes);
            for (int i = jam.childCount - 1; i >= 0; i--)
                if (!baked.Alive[i]) Object.DestroyImmediate(jam.GetChild(i).gameObject);
            foreach (Transform rock in jam) parts.Rocks.Add(rock);
            parts.Bake = baked.Asset;
            parts.ChokeSand = BuildChokeSand(group, centre, rng, parts.Rocks, baked.Asset);
            parts.Path.AddRange(baked.Path);
            Vector2 beyond = apex + LairExitDir * 5f;
            parts.Path.Add(new Vector3(beyond.x, GroundAt(beyond) + 0.1f, beyond.y));

            // The cone's look: a field of rubble. The surface itself is rough, mottled broken ground. Packed over it lie low
            // angular stones (too low to trip on: what you walk on is the smooth surface underneath), and, away from the
            // route and the fallen rocks, bigger half-buried ones. Every vertex carries the point of the surface it rides
            // on while the cone grows (a stone rides on the ground under its middle).
            var look = new List<Vector3>();
            var anchors = new List<Vector3>();
            var lookTriangles = new[] { new List<int>(), new List<int>(), new List<int>() }; // rubble, dark rock, pale broken rock
            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 onA, Vector3 onB, Vector3 onC, Vector3 outward, int material)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f)
                {
                    (b, c) = (c, b);
                    (onB, onC) = (onC, onB);
                }
                int start = look.Count;
                look.Add(a); look.Add(b); look.Add(c);
                anchors.Add(onA); anchors.Add(onB); anchors.Add(onC);
                lookTriangles[material].Add(start); lookTriangles[material].Add(start + 1); lookTriangles[material].Add(start + 2);
            }
            Vector3 Rough(int i, int j)
            {
                Vector3 v = V(i, j);
                float along = ApexDist - Vector2.Dot(new Vector2(v.x, v.z), LairExitDir);
                if (heights[i, j] > 0.3f && along > 1.5f) v.y += (Mathf.PerlinNoise(i * 0.73f + 3.1f, j * 0.61f + 7.7f) - 0.5f) * 0.5f;
                return v;
            }
            for (int i = 0; i < cols; i++)
                for (int j = 0; j < rows; j++)
                {
                    if (!Solid(i, j)) continue;
                    Vector3 a = V(i, j), b = V(i + 1, j), c = V(i + 1, j + 1), d = V(i, j + 1);
                    Vector3 ra = Rough(i, j), rb = Rough(i + 1, j), rc = Rough(i + 1, j + 1), rd = Rough(i, j + 1);
                    foreach ((Vector3 x, Vector3 y, Vector3 z, Vector3 ox, Vector3 oy, Vector3 oz) in new[] { (ra, rd, rc, a, d, c), (ra, rc, rb, a, c, b) })
                    {
                        Face(x, y, z, ox, oy, oz, Vector3.up, 0);
                    }
                }
            void Stone(Vector3 at, Vector3 size, Quaternion turn, Vector3 anchor)
            {
                var points = new List<Vector3>();
                var fresh = new List<bool>();
                RockSoup(rng, size, points, fresh, 0);
                for (int t = 0; t < fresh.Count; t++)
                {
                    Vector3 a = at + turn * points[t * 3], b = at + turn * points[t * 3 + 1], c = at + turn * points[t * 3 + 2];
                    Face(a, b, c, anchor, anchor, anchor, (a + b + c) / 3f - at, fresh[t] ? 2 : 1);
                }
            }
            int lowStones = 0;
            for (int tries = 0; lowStones < 2200 && tries < 60000; tries++)
            {
                Vector2 q = min + new Vector2(Range(rng, 0f, cols * MoundCell), Range(rng, 0f, rows * MoundCell));
                float h = MoundHeight(q);
                if (h < 0.3f || ApexDist - Vector2.Dot(q, LairExitDir) < 1f) continue;
                float wide = Range(rng, 0.5f, 1.5f), tall = wide * Range(rng, 0.3f, 0.45f);
                // Mostly sunk: only its top shows, a hand or two above the surface.
                var at = new Vector3(q.x, h + Range(rng, 0.08f, 0.26f) - tall * 0.4f, q.y);
                Stone(at, new Vector3(wide, tall, wide * Range(rng, 0.6f, 1f)), Quaternion.Euler(Range(rng, -14f, 14f), Range(rng, 0f, 360f), Range(rng, -14f, 14f)), new Vector3(q.x, h, q.y));
                lowStones++;
            }
            int stones = 0;
            for (int tries = 0; stones < 260 && tries < 12000; tries++)
            {
                Vector2 q = min + new Vector2(Range(rng, 0f, cols * MoundCell), Range(rng, 0f, rows * MoundCell));
                float h = MoundHeight(q);
                if (h < 0.35f || ApexDist - Vector2.Dot(q, LairExitDir) < 3f) continue;
                float size = Range(rng, 0.8f, 1.9f);
                var at = new Vector3(q.x, h + size * 0.08f, q.y);
                bool crowded = false;
                for (int k = 0; k + 1 < parts.Path.Count && !crowded; k++)
                    if (SegmentDistance(new Vector2(at.x, at.z), new Vector2(parts.Path[k].x, parts.Path[k].z), new Vector2(parts.Path[k + 1].x, parts.Path[k + 1].z)) < 2f + size * 0.5f) crowded = true;
                for (int k = 0; k < baked.FinalPositions.Count && !crowded; k++)
                    if ((baked.FinalPositions[k] - at).magnitude < baked.FinalSizes[k] * 0.55f + size * 0.5f) crowded = true;
                if (crowded) continue;
                Stone(at, RockShapes[stones % RockShapes.Length] * size, Quaternion.Euler(Range(rng, -30f, 30f), Range(rng, 0f, 360f), Range(rng, -30f, 30f)), new Vector3(q.x, h, q.y));
                stones++;
            }
            var lookMesh = new Mesh { name = "LairDebrisCone", indexFormat = IndexFormat.UInt32 };
            lookMesh.SetVertices(look);
            lookMesh.SetUVs(1, anchors);
            lookMesh.subMeshCount = 3;
            for (int m = 0; m < 3; m++) lookMesh.SetTriangles(lookTriangles[m], m);
            lookMesh.RecalculateNormals();
            lookMesh.RecalculateBounds();
            parts.Mound.sharedMesh = SaveMesh(lookMesh);

            // The red zone: the floor under the cone, and everywhere a rock lands or rolls (the recording knows), with a
            // brighter edge.
            float span = DomeRadius;
            int zCols = Mathf.CeilToInt(span * 2f / ZoneCell);
            var zoneOrigin = new Vector2(-span, -span);
            var cells = new bool[zCols * zCols];
            for (int i = 0; i < zCols; i++)
                for (int j = 0; j < zCols; j++)
                {
                    Vector2 q = zoneOrigin + new Vector2(i + 0.5f, j + 0.5f) * ZoneCell;
                    // Only where the debris piles deep enough to bury someone; its thin skirt just washes round your feet.
                    cells[j * zCols + i] = q.magnitude < LairArenaRadius + 2f && MoundHeight(q) > BuryDepth;
                }
            foreach ((Vector3 at, float radius) in baked.Low)
            {
                int i0 = Mathf.FloorToInt((at.x - radius - zoneOrigin.x) / ZoneCell), i1 = Mathf.FloorToInt((at.x + radius - zoneOrigin.x) / ZoneCell);
                int j0 = Mathf.FloorToInt((at.z - radius - zoneOrigin.y) / ZoneCell), j1 = Mathf.FloorToInt((at.z + radius - zoneOrigin.y) / ZoneCell);
                for (int i = Mathf.Max(0, i0); i <= Mathf.Min(zCols - 1, i1); i++)
                    for (int j = Mathf.Max(0, j0); j <= Mathf.Min(zCols - 1, j1); j++)
                        cells[j * zCols + i] = true;
            }
            bool Covered(int i, int j) => i >= 0 && j >= 0 && i < zCols && j < zCols && cells[j * zCols + i];
            {
                // The red zone must leave real room to stand: the far side of the arena is kept clear by the recording
                // (BakeCollapse leaves out any rock that strays there), and this checks it.
                int floor = 0, red = 0, strayed = 0;
                for (int i = 0; i < zCols; i++)
                    for (int j = 0; j < zCols; j++)
                    {
                        Vector2 q = zoneOrigin + new Vector2(i + 0.5f, j + 0.5f) * ZoneCell;
                        if (q.magnitude > LairArenaRadius) continue;
                        floor++;
                        if (!cells[j * zCols + i]) continue;
                        red++;
                        if (Vector2.Dot(q, LairExitDir) < SafeLine - ZoneCell) strayed++;
                    }
                Debug.Log($"COLLAPSE_ZONE the red zone covers {red * 100 / Mathf.Max(1, floor)}% of the arena floor.");
                EnemyTestBuilder.Require(strayed == 0, "The collapse's red zone reaches into the arena's safe side.");
                EnemyTestBuilder.Require(red * 100 / Mathf.Max(1, floor) <= 55, "The collapse's red zone covers more than about half the arena.");
            }
            var zoneVertices = new List<Vector3>();
            var zoneColors = new List<Color>();
            var zoneTriangles = new List<int>();
            for (int i = 0; i < zCols; i++)
                for (int j = 0; j < zCols; j++)
                {
                    if (!Covered(i, j)) continue;
                    bool edge = !Covered(i - 1, j) || !Covered(i + 1, j) || !Covered(i, j - 1) || !Covered(i, j + 1);
                    int start = zoneVertices.Count;
                    foreach (Vector2 corner in new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) })
                    {
                        Vector2 q = zoneOrigin + (new Vector2(i, j) + corner) * ZoneCell;
                        zoneVertices.Add(new Vector3(q.x, 0.06f, q.y));
                        zoneColors.Add(new Color(1f, 1f, 1f, edge ? 1f : 0.3f));
                    }
                    zoneTriangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                }
            var zoneMesh = new Mesh { name = "LairCrushZone", indexFormat = IndexFormat.UInt32 };
            zoneMesh.SetVertices(zoneVertices);
            zoneMesh.SetColors(zoneColors);
            zoneMesh.SetTriangles(zoneTriangles, 0);
            zoneMesh.RecalculateNormals();
            zoneMesh.RecalculateBounds();
            zoneMesh = SaveMesh(zoneMesh);
            var zone = new GameObject("Crush Zone");
            zone.transform.SetParent(group, false);
            zone.transform.position = centre;
            parts.Zone = zone.AddComponent<MeshFilter>();
            parts.Zone.sharedMesh = zoneMesh;
            parts.ZoneOrigin = zoneOrigin;
            parts.ZoneColumns = zCols;
            parts.ZoneCells = cells;

            parts.Exit = new GameObject("Way Out").transform;
            parts.Exit.SetParent(root.transform, false);
            parts.Exit.position = OnGround(centre.x + beyond.x, centre.z + beyond.y) + Vector3.up * 0.5f;
            parts.Exit.gameObject.AddComponent<QuestSignalSource>().enabled = false;
            return parts;
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return (p - (a + ab * t)).magnitude;
        }

        // Rough rock on rock and rubble: grippy, barely bouncy, so things come to rest on the cone's slope.
        private static PhysicsMaterial RockPhysics()
        {
            const string path = MaterialFolder + "/LairRockPhysics.physicsMaterial";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (material == null)
            {
                material = new PhysicsMaterial("LairRockPhysics");
                AssetDatabase.CreateAsset(material, path);
            }
            material.dynamicFriction = 0.75f;
            material.staticFriction = 0.9f;
            material.bounciness = 0.1f;
            material.frictionCombine = PhysicsMaterialCombine.Average;
            material.bounceCombine = PhysicsMaterialCombine.Average;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- The recorded rockfall ----------

        private sealed class BakeResult
        {
            public bool[] Alive;
            public CollapseBake Asset;
            public readonly List<Vector3> Path = new List<Vector3>();
            public readonly List<Vector3> FinalPositions = new List<Vector3>();
            public readonly List<float> FinalSizes = new List<float>();
            /// <summary>Everywhere a rock is near the floor at any moment (relative to the lair), with its reach.</summary>
            public readonly List<(Vector3 at, float radius)> Low = new List<(Vector3, float)>();
        }

        /// <summary>
        /// Runs the rockfall with real physics and records it, so the game can play back the very same fall every time.
        /// A copy of the lair goes into a scene of its own with its own physics. The rocks choking the gash become rigid
        /// bodies, come loose one after another (from the gash's far end toward its lip, the lowest first), and fall onto
        /// the debris cone, the floor, the pillars and each other until every one has come to rest.
        ///
        /// Then the result is checked, and re-run without the offenders until it is clean:
        /// - no rock lost out of the cavern or sunk into the cone;
        /// - a walking route from the cavern floor up the cone and out over the lip, between the settled rocks (if rocks
        ///   block every way, the fewest that would open one are left out of the next run).
        /// The clean run's paths go into the CollapseBake asset; the route is returned for the lair (and its tests).
        /// </summary>
        private static BakeResult BakeCollapse(GameObject root, Transform jam, Vector3 centre, List<float> sizes)
        {
            int count = jam.childCount;
            var result = new BakeResult { Alive = new bool[count] };
            for (int i = 0; i < count; i++) result.Alive[i] = true;
            float lowest = float.MaxValue, highest = float.MinValue;
            foreach (Transform rock in jam)
            {
                lowest = Mathf.Min(lowest, rock.position.y);
                highest = Mathf.Max(highest, rock.position.y);
            }

            for (int pass = 1; pass <= 40; pass++)
            {
                Scene preview = EditorSceneManager.NewPreviewScene();
                try
                {
                    GameObject copy = Object.Instantiate(root, root.transform.position, root.transform.rotation);
                    SceneManager.MoveGameObjectToScene(copy, preview);
                    PhysicsScene physics = preview.GetPhysicsScene();
                    EnemyTestBuilder.Require(physics.IsValid() && physics != Physics.defaultPhysicsScene, "The collapse needs a physics scene of its own to simulate in.");
                    Transform copyJam = copy.transform.Find(CollapseName + "/" + JamName);
                    var cone = copy.transform.Find(CollapseName + "/" + MoundName).GetComponent<MeshCollider>();

                    var bodies = new Rigidbody[count];
                    var index = new Dictionary<Rigidbody, int>();
                    var release = new float[count];
                    var released = new bool[count];
                    var jitter = new Random(4242);
                    for (int i = 0; i < count; i++)
                    {
                        Transform rock = copyJam.GetChild(i);
                        float wobble = Range(jitter, 0f, 0.15f); // (drawn for every rock, so leaving one out doesn't shift the others')
                        if (!result.Alive[i])
                        {
                            rock.gameObject.SetActive(false);
                            continue;
                        }
                        var body = rock.gameObject.AddComponent<Rigidbody>();
                        body.isKinematic = true;
                        body.mass = Mathf.Clamp(sizes[i] * sizes[i] * sizes[i] * 400f, 20f, 8000f);
                        body.linearDamping = 0.12f;
                        body.angularDamping = 1.6f; // broken rock doesn't roll far
                        body.solverIterations = 12;
                        body.solverVelocityIterations = 4;
                        body.maxDepenetrationVelocity = 4f;
                        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                        bodies[i] = body;
                        index[body] = i;
                        Vector2 local = new Vector2(rock.position.x - centre.x, rock.position.z - centre.z);
                        float along = Mathf.Clamp01((ApexDist - Vector2.Dot(local, LairExitDir)) / GashLength); // 0 at the lip
                        release[i] = 0.5f + 2.8f * (1f - along) + 0.6f * Mathf.InverseLerp(lowest, highest, rock.position.y) + wobble;
                    }

                    // The fall.
                    const float dt = 1f / 60f;
                    float time = 0f, quiet = 0f, danger = 0f;
                    var trackPositions = new List<Vector3>[count];
                    var trackRotations = new List<Quaternion>[count];
                    var starts = new float[count];
                    var before = new Vector3[count];
                    var knocks = new List<(float time, Vector3 at, float size)>();
                    var spin = new Random(977);
                    for (int step = 1; time < 14f; step++)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            if (bodies[i] == null) continue;
                            if (!released[i] && time >= release[i])
                            {
                                released[i] = true;
                                bodies[i].isKinematic = false;
                                bodies[i].linearVelocity = Vector3.down * Range(spin, 0.3f, 1.5f) + new Vector3(Range(spin, -0.6f, 0.6f), 0f, Range(spin, -0.6f, 0.6f));
                                bodies[i].angularVelocity = RandomUnit(spin) * Range(spin, 0.3f, 2f);
                            }
                            before[i] = released[i] ? bodies[i].linearVelocity : Vector3.zero;
                        }
                        physics.Simulate(dt);
                        time += dt;
                        bool moving = false, waiting = false;
                        for (int i = 0; i < count; i++)
                        {
                            Rigidbody body = bodies[i];
                            if (body == null) continue;
                            if (!released[i]) { waiting = true; continue; }
                            Vector3 velocity = body.linearVelocity;
                            float knock = (velocity - before[i] - Physics.gravity * dt).magnitude;
                            if (knock > 3.5f) knocks.Add((time, body.position - centre, knock * sizes[i]));
                            if (velocity.sqrMagnitude > 4f) danger = time;
                            if (!body.IsSleeping() && (velocity.sqrMagnitude > 0.01f || body.angularVelocity.sqrMagnitude > 0.04f)) moving = true;
                            if (step % 2 != 0) continue;
                            if (trackPositions[i] == null)
                            {
                                trackPositions[i] = new List<Vector3>();
                                trackRotations[i] = new List<Quaternion>();
                                starts[i] = time;
                            }
                            trackPositions[i].Add(body.position - centre);
                            trackRotations[i].Add(body.rotation);
                        }
                        if (waiting || moving) quiet = 0f;
                        else if ((quiet += dt) > 0.8f) break;
                    }
                    foreach (Rigidbody body in bodies) if (body != null) body.isKinematic = true;
                    Physics.SyncTransforms();

                    // Lost out of the cavern, or sunk into the cone: leave them out and run it again.
                    int lost = 0, buried = 0;
                    for (int i = 0; i < count; i++)
                    {
                        if (bodies[i] == null) continue;
                        Vector3 local = bodies[i].position - centre;
                        bool gone = local.y < -1.5f || new Vector2(local.x, local.z).magnitude > DomeRadius + 2f || local.y > lipTop + 2.5f || trackPositions[i] == null;
                        bool sunk = !gone && cone.Raycast(new Ray(new Vector3(bodies[i].position.x, centre.y + 60f, bodies[i].position.z), Vector3.down), out RaycastHit top, 120f) &&
                                    top.point.y - bodies[i].position.y > 0.1f;
                        if (gone) lost++;
                        if (sunk) buried++;
                        if (gone || sunk) result.Alive[i] = false;
                    }
                    if (lost + buried > 0)
                    {
                        Debug.Log($"COLLAPSE_BAKE pass {pass}: {lost} rocks lost and {buried} sunk into the cone; running again without them.");
                        continue;
                    }

                    // The far side of the arena stays safe: a rock that rolls or bounces across the line is left out.
                    int strays = 0;
                    for (int i = 0; i < count; i++)
                    {
                        if (bodies[i] == null) continue;
                        float reach = sizes[i] * 0.5f + RockMargin;
                        foreach (Vector3 p in trackPositions[i])
                            if (p.y - sizes[i] * 0.5f < 2.5f && Vector2.Dot(new Vector2(p.x, p.z), LairExitDir) - reach < SafeLine)
                            {
                                result.Alive[i] = false;
                                strays++;
                                break;
                            }
                    }
                    if (strays > 0)
                    {
                        Debug.Log($"COLLAPSE_BAKE pass {pass}: {strays} rocks strayed into the arena's safe side; running again without them.");
                        continue;
                    }

                    // A way up between the settled rocks.
                    List<int> blockers = FindClimb(physics, cone, index, centre, result.Path);
                    if (blockers.Count > 0)
                    {
                        foreach (int i in blockers) result.Alive[i] = false;
                        Debug.Log($"COLLAPSE_BAKE pass {pass}: {blockers.Count} rocks blocked every way up; running again without them.");
                        continue;
                    }

                    // Clean: keep it.
                    var allStarts = new List<float>();
                    var counts = new List<int>();
                    var positions = new List<Vector3>();
                    var rotations = new List<Quaternion>();
                    for (int i = 0; i < count; i++)
                    {
                        if (bodies[i] == null) continue;
                        List<Vector3> p = trackPositions[i];
                        List<Quaternion> r = trackRotations[i];
                        int keep = p.Count;
                        while (keep > 2 && (p[keep - 1] - p[keep - 2]).sqrMagnitude < 4e-6f && Quaternion.Angle(r[keep - 1], r[keep - 2]) < 0.2f) keep--;
                        allStarts.Add(starts[i]);
                        counts.Add(keep);
                        for (int k = 0; k < keep; k++)
                        {
                            positions.Add(p[k]);
                            rotations.Add(r[k]);
                            if (p[k].y - sizes[i] * 0.5f < 2.5f) result.Low.Add((p[k], sizes[i] * 0.5f + RockMargin));
                        }
                        result.FinalPositions.Add(p[keep - 1]);
                        result.FinalSizes.Add(sizes[i]);
                    }
                    // The hardest knocks, a few per tenth of a second, for dust and tremors.
                    knocks.Sort((a, b) => a.time.CompareTo(b.time));
                    var kept = new List<(float time, Vector3 at, float size)>();
                    for (int k = 0; k < knocks.Count;)
                    {
                        int end = k;
                        while (end < knocks.Count && knocks[end].time < knocks[k].time + 0.1f) end++;
                        List<(float time, Vector3 at, float size)> bucket = knocks.GetRange(k, end - k);
                        bucket.Sort((a, b) => b.size.CompareTo(a.size));
                        kept.AddRange(bucket.GetRange(0, Mathf.Min(3, bucket.Count)));
                        k = end;
                    }
                    kept.Sort((a, b) => a.time.CompareTo(b.time));

                    var asset = AssetDatabase.LoadAssetAtPath<CollapseBake>(BakePath);
                    if (asset == null)
                    {
                        asset = ScriptableObject.CreateInstance<CollapseBake>();
                        AssetDatabase.CreateAsset(asset, BakePath);
                    }
                    asset.Set(BakeFrame, allStarts.ToArray(), counts.ToArray(), positions.ToArray(), rotations.ToArray(), danger + 0.3f,
                        kept.ConvertAll(k => k.time).ToArray(), kept.ConvertAll(k => k.at).ToArray(), kept.ConvertAll(k => k.size).ToArray());
                    EditorUtility.SetDirty(asset);
                    result.Asset = asset;
                    Debug.Log($"COLLAPSE_BAKED pass {pass}: {counts.Count} rocks, {positions.Count} keys, {asset.Duration:0.0} s, {kept.Count} knocks, route of {result.Path.Count} points.");
                    return result;
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(preview);
                }
            }
            throw new InvalidOperationException("The collapse never settled into something with a way out. Fewer or smaller rocks, or a gentler cone, would help.");
        }

        /// <summary>
        /// Looks for a walking route from the cavern floor up the debris cone and out over the lip, between the settled
        /// rocks (in the simulation's own physics scene). Fills <paramref name="path"/> with it (points relative to the
        /// lair) and returns nothing if rocks leave a way open; otherwise returns the fewest rocks whose removal opens one.
        /// A spot is walkable when it is on the cone, has headroom under the roof and the dunes' overhang, nothing solid
        /// stands in it, and the step to the next spot is no steeper than a person can climb.
        /// </summary>
        private static List<int> FindClimb(PhysicsScene physics, MeshCollider cone, Dictionary<Rigidbody, int> rocks, Vector3 centre, List<Vector3> path)
        {
            const float cell = 0.5f;
            Bounds bounds = cone.bounds;
            int cols = Mathf.CeilToInt(bounds.size.x / cell), rows = Mathf.CeilToInt(bounds.size.z / cell);
            int total = cols * rows;
            var height = new float[total];
            var state = new byte[total]; // 0 none, 1 free, 2 blocked by rocks only
            var blockedBy = new Dictionary<int, List<int>>();
            var found = new Collider[24];
            for (int i = 0; i < cols; i++)
                for (int j = 0; j < rows; j++)
                {
                    int n = j * cols + i;
                    var world = new Vector3(bounds.min.x + (i + 0.5f) * cell, centre.y + 60f, bounds.min.z + (j + 0.5f) * cell);
                    if (!cone.Raycast(new Ray(world, Vector3.down), out RaycastHit hit, 120f)) continue;
                    float h = hit.point.y - centre.y;
                    if (h < -0.2f) continue;
                    var local = new Vector2(world.x - centre.x, world.z - centre.z);
                    bool inShaft = GashDistance(local) < GashHalf + ShaftMargin - 0.4f;
                    if (!inShaft && local.magnitude > LairArenaRadius + 1.5f) continue; // behind the cavern's wall rocks
                    if (!inShaft && DomeY(local.x, local.y) - h < 2.3f) continue; // no headroom under the roof
                    float under = Ground(world.x, world.z) - centre.y - 0.15f - h; // how far below the dunes' sand
                    if (inShaft && !InLid(world.x, world.z) && under > 0.35f && under < 2.3f) continue; // no headroom under the dunes' overhang
                    height[n] = h;
                    Vector3 feet = hit.point;
                    int hits = physics.OverlapCapsule(feet + Vector3.up * 0.8f, feet + Vector3.up * 1.9f, 0.5f, found, ~0, QueryTriggerInteraction.Ignore);
                    bool wall = false;
                    List<int> stones = null;
                    for (int k = 0; k < hits; k++)
                    {
                        Collider other = found[k];
                        if (other == cone || other.name.StartsWith("Floor")) continue;
                        if (other.attachedRigidbody != null && rocks.TryGetValue(other.attachedRigidbody, out int rock)) (stones ??= new List<int>()).Add(rock);
                        else wall = true;
                    }
                    if (wall) continue;
                    state[n] = stones == null ? (byte)1 : (byte)2;
                    if (stones != null) blockedBy[n] = stones;
                }

            Vector2 CellLocal(int n) => new Vector2(bounds.min.x + (n % cols + 0.5f) * cell - centre.x, bounds.min.z + (n / cols + 0.5f) * cell - centre.z);

            // Cheapest way from the cone's foot to the lip: first through free spots only; failing that, counting each
            // rock-blocked spot as very costly, to find the fewest rocks in the way.
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var cost = new float[total];
                var from = new int[total];
                for (int n = 0; n < total; n++) { cost[n] = float.MaxValue; from[n] = -1; }
                var queue = new SortedSet<(float cost, int node)>();
                for (int n = 0; n < total; n++)
                    if (state[n] == 1 && height[n] < 0.6f && CellLocal(n).magnitude < LairArenaRadius - 2f)
                    {
                        cost[n] = 0f;
                        queue.Add((0f, n));
                    }
                int goal = -1;
                while (queue.Count > 0)
                {
                    (float c, int n) = queue.Min;
                    queue.Remove(queue.Min);
                    if (c > cost[n]) continue;
                    int i = n % cols, j = n / cols;
                    var local = new Vector2(bounds.min.x + (i + 0.5f) * cell - centre.x, bounds.min.z + (j + 0.5f) * cell - centre.z);
                    float along = ApexDist - Vector2.Dot(local, LairExitDir);
                    if (along < -0.75f && Mathf.Abs(Vector2.Dot(local, LairAcross)) < 3.5f && height[n] > lipTop - 1.5f) { goal = n; break; }
                    for (int di = -1; di <= 1; di++)
                        for (int dj = -1; dj <= 1; dj++)
                        {
                            if (di == 0 && dj == 0) continue;
                            int ni = i + di, nj = j + dj;
                            if (ni < 0 || nj < 0 || ni >= cols || nj >= rows) continue;
                            int m = nj * cols + ni;
                            if (state[m] == 0 || (attempt == 0 && state[m] == 2)) continue;
                            float run = cell * (di != 0 && dj != 0 ? 1.4142f : 1f);
                            if (Mathf.Abs(height[m] - height[n]) > run * 0.93f) continue; // steeper than 43 degrees
                            float next = c + run + (state[m] == 2 ? 300f : 0f);
                            if (next >= cost[m]) continue;
                            cost[m] = next;
                            from[m] = n;
                            queue.Add((next, m));
                        }
                }
                if (goal < 0) continue;
                var cellsOnPath = new List<int>();
                for (int n = goal; n >= 0; n = from[n]) cellsOnPath.Add(n);
                cellsOnPath.Reverse();
                if (attempt == 1)
                {
                    var blockers = new HashSet<int>();
                    foreach (int n in cellsOnPath)
                        if (state[n] == 2) foreach (int rock in blockedBy[n]) blockers.Add(rock);
                    return new List<int>(blockers);
                }
                path.Clear();
                for (int k = 0; k < cellsOnPath.Count; k++)
                {
                    if (k % 3 != 0 && k != cellsOnPath.Count - 1) continue;
                    int n = cellsOnPath[k];
                    path.Add(new Vector3(bounds.min.x + (n % cols + 0.5f) * cell - centre.x, height[n] + 0.1f, bounds.min.z + (n / cols + 0.5f) * cell - centre.z));
                }
                return new List<int>();
            }
            throw new InvalidOperationException("No way up the debris cone at all, even ignoring the rocks: the cone, the gash's walls or the roof are in the way.");
        }

        // The lair's fodder (phase 2's sandstorm): ticks, armadillos and beetles, as in the test arena.
        private static FodderSpawner LairSpawner(GameObject holder)
        {
            var spawner = holder.AddComponent<FodderSpawner>();
            var entries = new (GameObject prefab, float weight, int min, int max)[]
            {
                (DesertFodderBuilder.Load(DesertFodderBuilder.TickPath), 1.5f, 3, 4),
                (DesertFodderBuilder.Load(DesertFodderBuilder.ArmadilloPath), 1f, 1, 1),
                (DesertFodderBuilder.Load(DesertFodderBuilder.BeetlePath), 1f, 1, 2),
            };
            var data = new SerializedObject(spawner);
            SerializedProperty list = data.FindProperty("entries");
            list.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("prefab").objectReferenceValue = entries[i].prefab;
                entry.FindPropertyRelative("weight").floatValue = entries[i].weight;
                entry.FindPropertyRelative("minGroup").intValue = entries[i].min;
                entry.FindPropertyRelative("maxGroup").intValue = entries[i].max;
            }
            data.FindProperty("maxAlive").intValue = 7;
            data.FindProperty("waveInterval").floatValue = 7f;
            data.FindProperty("firstWaveDelay").floatValue = 2f;
            data.FindProperty("spawnRadiusMin").floatValue = 10f;
            data.FindProperty("spawnRadiusMax").floatValue = 26f;
            data.FindProperty("minPlayerDistance").floatValue = 7f;
            data.FindProperty("activationRange").floatValue = 0f;
            data.ApplyModifiedPropertiesWithoutUndo();
            spawner.enabled = false;
            return spawner;
        }

        // =====================================================================
        // Install into DesertDunes.unity
        // =====================================================================

        [MenuItem("Signal Lost/Desert/Install Desert Story")]
        public static void InstallStoryMenu()
        {
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Install the desert story",
                    "This regenerates DesertEnvironment.prefab (adding the mini-boss arenas and the sinkhole over the lair), the desert camp, " +
                    "the desert trophies and CorewormLair.prefab, then installs the mini-bosses, quest tasks, Core Worm, lair and story in " +
                    "DesertDunes.unity, replacing the old hermit-crab encounters and boss placeholders. Edits made to those prefabs are lost. Continue?",
                    "Install", "Cancel"))
                return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            InstallStory();
        }

        public static void InstallStory()
        {
            DesertFodderBuilder.EnsureFodderPrefabs();
            BuildEnvironmentPrefab();
            BuildDesertTrophies();
            BuildLairPrefab();
            BaseCampBuilder.BuildDesert();
            AssetDatabase.SaveAssets();

            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            InstallCamp();
            foreach (string old in new[] { StoryRootName, "Encounters", "Boss Spawn (Tech-Enhanced Hermit Crab goes here)", "Engine Core Drop (boss reward)", "Mini-Boss HUD", "Fodder Status Overlay" })
                for (GameObject found = GameObject.Find(old); found != null; found = GameObject.Find(old)) Object.DestroyImmediate(found);
            Physics.SyncTransforms();

            var story = new GameObject(StoryRootName);
            Transform chopShop = Group("Chop Shop", story.transform);
            Transform tasks = Group("Quest Tasks", story.transform);
            Transform wormGroup = Group("Core Worm", story.transform);
            new GameObject("Mini-Boss HUD").AddComponent<MiniBossHud>();
            new GameObject("Fodder Status Overlay").AddComponent<FodderStatusOverlay>();

            // The Chop Shop, each at home in its arena, hidden until its task is done.
            DesertMiniBoss buzzard = PlaceBoss(DesertMiniBossBuilder.BuzzardPath, "Old Buzzard", DesertMiniBossBuilder.Theme.WreckField, Encounters[0].Center, chopShop);
            DesertMiniBoss burnout = PlaceBoss(DesertMiniBossBuilder.BurnoutPath, "Burnout", DesertMiniBossBuilder.Theme.MesaPass, Encounters[1].Center, chopShop);
            DesertMiniBoss sludge = PlaceBoss(DesertMiniBossBuilder.SludgePath, "Sludge", DesertMiniBossBuilder.Theme.BuriedHull, Encounters[2].Center, chopShop);

            // The tasks along the way to each.
            var feathers = new[] { new Vector2(-12f, -104f), new Vector2(-30f, -92f), new Vector2(-45f, -79f) };
            var fires = new[] { new Vector2(30f, -100f), new Vector2(48f, -76f), new Vector2(62f, -50f) };
            var leaks = new List<Vector2>();
            foreach (float angle in SumpLeakAngles(Encounters[2].Center))
                leaks.Add(Encounters[2].Center + new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * SumpLeakRadius);
            QuestTask[] featherTasks = PlaceTasks(tasks, "Feather", feathers, FeatherTask);
            QuestTask[] fireTasks = PlaceTasks(tasks, "Fire", fires, FireTask);
            QuestTask[] leakTasks = PlaceTasks(tasks, "Leak", leaks.ToArray(), LeakTask, false);

            // The Core Worm: lurking under the camp's new rack, and its lair under its old sand bowl.
            var camp = Object.FindAnyObjectByType<IslandCamp>();
            Transform rackRoot = camp.transform.Find(BaseCampBuilder.RackName);
            var rack = rackRoot.GetComponentsInChildren<TrophyStand>();
            var lairObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LairPrefabPath), wormGroup);
            lairObject.name = "Core Worm Lair";
            var lair = lairObject.GetComponent<WormLair>();
            var arena = lairObject.GetComponent<CorewormArena>();
            Transform lid = null;
            foreach (GameObject top in scene.GetRootGameObjects())
                if (lid == null) lid = top.transform.Find(SinkholeLidName);
            var lairData = new SerializedObject(lair);
            lairData.FindProperty("surfaceLid").objectReferenceValue = lid != null ? lid.gameObject : null;
            lairData.ApplyModifiedPropertiesWithoutUndo();

            var worm = (GameObject)PrefabUtility.InstantiatePrefab(DesertFodderBuilder.Load(DesertBossBuilder.WormPath), wormGroup);
            worm.name = "Core Worm";
            worm.transform.position = arena.Centre + Vector3.down * 12f;
            var wormData = new SerializedObject(worm.GetComponent<Coreworm>());
            wormData.FindProperty("arena").objectReferenceValue = arena;
            wormData.ApplyModifiedPropertiesWithoutUndo();
            var spawnerData = new SerializedObject(lairObject.GetComponentInChildren<FodderSpawner>(true));
            spawnerData.FindProperty("stopWhenDead").objectReferenceValue = worm.GetComponent<Health>();
            spawnerData.ApplyModifiedPropertiesWithoutUndo();

            Vector3 rackMiddle = Vector3.zero;
            foreach (TrophyStand stand in rack) rackMiddle += stand.transform.position;
            rackMiddle /= Mathf.Max(1, rack.Length);
            var ambush = new GameObject("Core Worm Ambush (at the rack)");
            ambush.transform.SetParent(wormGroup, false);
            ambush.transform.position = rackMiddle;
            Vector2 eruptFlat = new Vector2(camp.transform.position.x + 15f, camp.transform.position.z - 3f);
            var erupt = new GameObject("Worm Erupts Here").transform;
            erupt.SetParent(ambush.transform, false);
            erupt.position = OnGround(eruptFlat.x, eruptFlat.y);
            var exit = ambush.AddComponent<DesertStageExit>();
            var exitData = new SerializedObject(exit);
            exitData.FindProperty("worm").objectReferenceValue = worm.GetComponent<Coreworm>();
            exitData.FindProperty("arena").objectReferenceValue = arena;
            exitData.FindProperty("eruptPoint").objectReferenceValue = erupt;
            exitData.FindProperty("returnPoint").objectReferenceValue = ambush.transform;
            exitData.FindProperty("triggerRadius").floatValue = 5f;
            exitData.FindProperty("requiredParts").intValue = 0;
            exitData.FindProperty("waitForArming").boolValue = true;
            exitData.FindProperty("returnAfterCore").boolValue = false;
            exitData.FindProperty("ambushLine").stringValue = "Hold on... why is the ground shaking?";
            exitData.FindProperty("waitingMessage").stringValue = "The Core Worm got you. It's lurking under the camp again...";
            exitData.ApplyModifiedPropertiesWithoutUndo();

            // The story.
            var flow = story.AddComponent<DesertStory>();
            var data = new SerializedObject(flow);
            data.FindProperty("cavendish").objectReferenceValue = camp.Cavendish;
            data.FindProperty("player").objectReferenceValue = Object.FindAnyObjectByType<PlayerLife>();
            SerializedProperty jobs = data.FindProperty("jobs");
            jobs.arraySize = 3;
            Job(jobs.GetArrayElementAtIndex(0), "Picked Clean", buzzard, "old_buzzard_feathers", "the Wreck Field, west of camp", featherTasks,
                "Old Buzzard has been moulting all over the dunes west of camp. Follow its trail and pick up the feathers.", "Feathers",
                "Old Buzzard roosts in the Wreck Field, west of camp. Go and find it.",
                "Defeat Old Buzzard. Step out of its shadow when it dives; knock it down with charged shots at its wings.");
            Job(jobs.GetArrayElementAtIndex(1), "Burning Rubber", burnout, "burnout_thorns", "the Scorch Raceway in the Mesa Pass, east of camp", fireTasks,
                "Burnout set the dunes alight on its way east. Smother its fires with sand.", "Fires out",
                "Burnout is tearing round the Scorch Raceway in the Mesa Pass, east of camp. Go and stop it.",
                "Defeat Burnout. Sidestep late on its dashes and crash it into the pillars.");
            Job(jobs.GetArrayElementAtIndex(2), "Oil and Trouble", sludge, "sludge_stinger", "its fuel sump, north-west of camp", leakTasks,
                "Seal the three leaking fuel lines round Sludge's sump, north-west of camp. That will bring it out.", "Leaks sealed",
                "Sludge has crawled out of the sump!",
                "Defeat Sludge. Get behind cover when it charges its cannon; hit its tank with a charged shot to backfire it.");
            SetObjects(data.FindProperty("rack"), rack);
            data.FindProperty("wormAmbush").objectReferenceValue = exit;
            data.FindProperty("lair").objectReferenceValue = lair;
            data.ApplyModifiedPropertiesWithoutUndo();

            // Bosses start hidden (the story shows each when its task is done).
            foreach (DesertMiniBoss boss in new[] { buzzard, burnout, sludge }) boss.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            ValidateStory();
            Debug.Log("DESERT_STORY_INSTALLED");
        }

        private static void Job(SerializedProperty job, string title, DesertMiniBoss boss, string trophy, string where, QuestTask[] tasks,
            string taskObjective, string taskLabel, string hunt, string fight)
        {
            job.FindPropertyRelative("title").stringValue = title;
            job.FindPropertyRelative("boss").objectReferenceValue = boss;
            job.FindPropertyRelative("trophyId").stringValue = trophy;
            job.FindPropertyRelative("where").stringValue = where;
            SetObjects(job.FindPropertyRelative("tasks"), tasks);
            job.FindPropertyRelative("taskObjective").stringValue = taskObjective;
            job.FindPropertyRelative("taskLabel").stringValue = taskLabel;
            job.FindPropertyRelative("huntObjective").stringValue = hunt;
            job.FindPropertyRelative("fightObjective").stringValue = fight;
        }

        private static DesertMiniBoss PlaceBoss(string prefabPath, string label, DesertMiniBossBuilder.Theme theme, Vector2 home, Transform parent)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(DesertFodderBuilder.Load(prefabPath), parent);
            go.name = label;
            Vector3 at = OnGround(home.x, home.y);
            float facing = AngleTo(home, CampOrigin);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 90f - facing, 0f));
            go.AddComponent<QuestSignalSource>().enabled = false;
            FodderSpawner spawner = DesertMiniBossBuilder.BuildSpawner(theme, go.GetComponent<Health>());
            spawner.name = label + " Fodder Spawner";
            spawner.transform.SetParent(parent, false);
            spawner.transform.position = at;
            var boss = go.GetComponent<DesertMiniBoss>();
            var data = new SerializedObject(boss);
            data.FindProperty("fodderSpawner").objectReferenceValue = spawner;
            data.ApplyModifiedPropertiesWithoutUndo();
            return boss;
        }

        // Each task where asked, nudged off any rock or prop it would sit in.
        private static QuestTask[] PlaceTasks(Transform parent, string label, Vector2[] spots, Func<Transform, QuestTask> build, bool nudge = true)
        {
            var result = new QuestTask[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                Vector2 p = nudge ? ClearSpot(spots[i]) : spots[i];
                var holder = new GameObject($"{label} {i + 1}").transform;
                holder.SetParent(parent, false);
                holder.position = OnGround(p.x, p.y);
                holder.rotation = Quaternion.Euler(0f, (i * 137f) % 360f, 0f);
                result[i] = build(holder);
            }
            return result;
        }

        private static Vector2 ClearSpot(Vector2 spot)
        {
            for (int i = 0; i < 40; i++)
            {
                float ring = i * 0.6f, angle = i * 2.39996f;
                Vector2 p = spot + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring;
                Vector3 at = OnGround(p.x, p.y) + Vector3.up * 1.2f;
                bool blocked = false;
                foreach (Collider hit in Physics.OverlapSphere(at, 1.6f, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.name != "Dune Terrain" && hit.name != SinkholeLidName && hit.name != "Seabed") blocked = true;
                if (!blocked) return p;
            }
            return spot;
        }

        private static QuestTask AddTask(GameObject holder, string prompt, string doneMessage, ParticleBurst.Kind puff, GameObject[] hide, GameObject[] show, Vector3 boxCentre, Vector3 boxSize)
        {
            var box = holder.AddComponent<BoxCollider>();
            box.center = boxCentre;
            box.size = boxSize;
            holder.AddComponent<QuestSignalSource>().enabled = false;
            var task = holder.AddComponent<QuestTask>();
            var data = new SerializedObject(task);
            data.FindProperty("prompt").stringValue = prompt;
            data.FindProperty("doneMessage").stringValue = doneMessage;
            data.FindProperty("puff").enumValueIndex = (int)puff;
            SetObjects(data.FindProperty("hideWhenDone"), hide);
            SetObjects(data.FindProperty("showWhenDone"), show);
            data.ApplyModifiedPropertiesWithoutUndo();
            return task;
        }

        // A big moulted feather stuck in the sand, glinting.
        private static QuestTask FeatherTask(Transform holder)
        {
            GameObject feather = KitProp("TaskFeather", () => FeatherKit(1.8f), "Feather", holder, holder.position + Vector3.up * 0.05f,
                holder.rotation * Quaternion.Euler(-35f, 0f, 0f), Vector3.one, false, false, false);
            var glint = new GameObject("Glint").AddComponent<Light>();
            glint.transform.SetParent(feather.transform, false);
            glint.transform.position = holder.position + Vector3.up * 0.8f;
            glint.type = LightType.Point;
            glint.color = new Color(1f, 0.9f, 0.7f);
            glint.range = 4f;
            glint.intensity = 1.5f;
            return AddTask(holder.gameObject, "Pick up the feather", "Feather", ParticleBurst.Kind.Sand, new[] { feather }, new GameObject[0],
                new Vector3(0f, 0.6f, 0f), new Vector3(1.4f, 1.2f, 1.6f));
        }

        // A patch of burning fuel in a scorched patch, put out with sand (a mound appears).
        private static QuestTask FireTask(Transform holder)
        {
            KitProp("ArenaScorchPatch", () => DiscKit(AM.Scorch), "Scorch", holder, holder.position + Vector3.up * 0.02f, holder.rotation, new Vector3(2.2f, 1f, 1.8f));
            var fire = new GameObject("Fire");
            fire.transform.SetParent(holder, false);
            fire.transform.localPosition = Vector3.up * 0.05f;
            var effect = fire.AddComponent<FireEffect>();
            var data = new SerializedObject(effect);
            data.FindProperty("radius").floatValue = 1.1f;
            data.FindProperty("height").floatValue = 1.8f;
            data.FindProperty("smokeAmount").floatValue = 0.7f;
            data.ApplyModifiedPropertiesWithoutUndo();
            GameObject mound = KitProp("TaskSandMound", () => Single(LowPolyMesh.Icosphere("m", 0), AM.Sand), "Sand Mound", holder, holder.position,
                holder.rotation, new Vector3(1.3f, 0.45f, 1.1f), false, false, false);
            return AddTask(holder.gameObject, "Smother the fire with sand", "Fire out", ParticleBurst.Kind.Sand, new GameObject[0], new[] { mound },
                new Vector3(0f, 0.7f, 0f), new Vector3(2.2f, 1.4f, 2.2f));
        }

        // The end of a pipe from the tank, spraying fuel into a pool; sealed with a brass clamp.
        private static QuestTask LeakTask(Transform holder)
        {
            Vector2 c = Encounters[2].Center;
            Vector3 toTank = AroundSite(c, SumpTankAngle(c), 14.5f) - holder.position;
            toTank.y = 0f;
            holder.rotation = Quaternion.LookRotation(-toTank.normalized); // facing away from the tank, along the pipe
            KitProp("ArenaOilPool", () => DiscKit(AM.Oil), "Oil Pool", holder, holder.position + Vector3.up * 0.03f, holder.rotation, new Vector3(1.6f, 1f, 2.2f));
            KitProp("TaskPipeEnd", () =>
            {
                var k = new FlatMeshKit<AM>();
                k.Add(LowPolyMesh.Tube("end", new[] { new Vector3(0f, 0.3f, -0.6f), new Vector3(0f, 0.32f, 0.1f), new Vector3(0f, 0.7f, 0.35f) }, 0.24f, 6), Matrix4x4.identity, AM.Rust);
                k.Add(LowPolyMesh.Lathe("flange", new[] { new Vector2(0.24f, 0f), new Vector2(0.34f, 0f), new Vector2(0.34f, 0.08f), new Vector2(0.24f, 0.08f) }, 6),
                    Matrix4x4.TRS(new Vector3(0f, 0.7f, 0.35f), Quaternion.Euler(30f, 0f, 0f), Vector3.one), AM.Burnt);
                return k;
            }, "Pipe End", holder, holder.position, holder.rotation);

            var spray = new GameObject("Fuel Spray");
            spray.transform.SetParent(holder, false);
            spray.transform.localPosition = new Vector3(0f, 0.75f, 0.38f);
            spray.transform.localRotation = Quaternion.Euler(-50f, 0f, 0f);
            var particles = spray.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.startLifetime = 0.7f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 3.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            main.startColor = new Color(0.08f, 0.07f, 0.06f, 1f);
            main.gravityModifier = 1f;
            main.maxParticles = 300;
            var emission = particles.emission;
            emission.rateOverTime = 140f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.08f;
            var renderer = spray.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = arenaMats[AM.Oil];
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            GameObject clamp = KitProp("TaskClamp", () =>
            {
                var k = new FlatMeshKit<AM>();
                k.Add(LowPolyMesh.Lathe("cap", new[] { new Vector2(0f, 0f), new Vector2(0.36f, 0f), new Vector2(0.36f, 0.16f), new Vector2(0f, 0.2f) }, 8),
                    Matrix4x4.TRS(new Vector3(0f, 0.72f, 0.37f), Quaternion.Euler(30f, 0f, 0f), Vector3.one), AM.Brass);
                k.Box(new Vector3(0f, 0.5f, 0.2f), new Vector3(0.6f, 0.08f, 0.12f), Quaternion.Euler(30f, 0f, 0f), AM.Brass);
                return k;
            }, "Clamp", holder, holder.position, holder.rotation, Vector3.one, false, false, false);
            return AddTask(holder.gameObject, "Seal the leak", "Leak sealed", ParticleBurst.Kind.Smoke, new[] { spray }, new[] { clamp },
                new Vector3(0f, 0.6f, 0f), new Vector3(1.4f, 1.2f, 1.8f));
        }

        [MenuItem("Signal Lost/Desert/Validate Desert Story")]
        public static void ValidateStory()
        {
            if (SceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<DesertStory>() == 1, "Expected one DesertStory.");
            var story = Object.FindAnyObjectByType<DesertStory>();
            var data = new SerializedObject(story);
            foreach (string field in new[] { "cavendish", "player", "wormAmbush", "lair" })
                EnemyTestBuilder.Require(data.FindProperty(field).objectReferenceValue != null, "The desert story has no " + field + ".");
            SerializedProperty jobs = data.FindProperty("jobs");
            EnemyTestBuilder.Require(jobs.arraySize == 3, "Expected three Chop Shop jobs.");
            for (int i = 0; i < jobs.arraySize; i++)
            {
                SerializedProperty job = jobs.GetArrayElementAtIndex(i);
                var boss = job.FindPropertyRelative("boss").objectReferenceValue as DesertMiniBoss;
                EnemyTestBuilder.Require(boss != null && !boss.gameObject.activeSelf, $"Job {i + 1}: its boss is missing or not hidden.");
                EnemyTestBuilder.Require(new SerializedObject(boss).FindProperty("fodderSpawner").objectReferenceValue != null, boss.name + " has no fodder spawner.");
                EnemyTestBuilder.Require(Trophy.Find(job.FindPropertyRelative("trophyId").stringValue) != null, $"Job {i + 1}: unknown trophy.");
                EnemyTestBuilder.Require(Trophy.Find(job.FindPropertyRelative("trophyId").stringValue).LoadModel() != null, $"Job {i + 1}: trophy model missing.");
                SerializedProperty taskList = job.FindPropertyRelative("tasks");
                EnemyTestBuilder.Require(taskList.arraySize == 3, $"Job {i + 1}: expected three tasks.");
                for (int t = 0; t < taskList.arraySize; t++)
                    EnemyTestBuilder.Require(taskList.GetArrayElementAtIndex(t).objectReferenceValue != null, $"Job {i + 1}: task {t + 1} is missing.");
            }
            EnemyTestBuilder.Require(data.FindProperty("rack").arraySize == 3, "Expected the three-stand Chop Shop rack.");
            EnemyTestBuilder.Require(Trophy.Find("core_worm_fang")?.LoadModel() != null, "The Core Worm fang trophy is missing.");
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<Coreworm>() == 1 && EnemyTestBuilder.Count<CorewormArena>() == 1 && EnemyTestBuilder.Count<WormLair>() == 1,
                "Expected the Core Worm, its arena and the lair.");
            var lair = new SerializedObject(Object.FindAnyObjectByType<WormLair>());
            foreach (string field in new[] { "arena", "surfaceLid", "ceilingCap", "exitPoint", "hole" })
                EnemyTestBuilder.Require(lair.FindProperty(field).objectReferenceValue != null, "The lair has no " + field + ".");
            EnemyTestBuilder.Require(lair.FindProperty("rocks").arraySize > 20 && lair.FindProperty("mound").objectReferenceValue != null &&
                lair.FindProperty("bake").objectReferenceValue != null &&
                lair.FindProperty("crushZone").objectReferenceValue != null && lair.FindProperty("crushCells").arraySize > 0,
                "The lair's collapse (rocks, scree, red zone) is missing.");
            EnemyTestBuilder.Require(lair.FindProperty("climbPath").arraySize > 10, "The lair has no climb path.");
            EnemyTestBuilder.Require(EnemyTestBuilder.All<HermitCrab>().TrueForAll(crab => crab.GetComponent<MerchantCrab>() != null),
                "The old hermit-crab encounters should be gone (only Eugene is left).");
            EnemyTestBuilder.Require(EnemyTestBuilder.Count<Camera>() == 1 && EnemyTestBuilder.Count<AudioListener>() == 1, "Expected one camera and listener.");
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    EnemyTestBuilder.Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0, "Missing script: " + item.name);
            Debug.Log("DESERT_STORY_VALIDATED");
        }
    }
}