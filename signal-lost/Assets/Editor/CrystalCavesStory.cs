using System;
using System.Collections.Generic;
using System.Linq;
using SignalLost.Effects;
using SignalLost.Enemies.Caves;
using SignalLost.Enemies.Desert;
using SignalLost.Inventory;
using SignalLost.Player;
using SignalLost.Progression;
using SignalLost.Scanner;
using SignalLost.Story;
using SignalLost.UI;
using SignalLost.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using T = SignalLost.Editor.CrystalCavesTerrain;

namespace SignalLost.Editor
{
    /// <summary>
    /// The Crystal Caves' story pass (docs/crystal-caves-flow.md): Cavendish's three jobs, their tasks and mini-bosses,
    /// the caves' trophy rack, the route Chameleons, and the Cave Axolotl's chamber with its own Chameleons.
    /// Install (Signal Lost > Crystal Caves > Install Crystal Caves Story) rebuilds the camp prefab (adding the rack) and
    /// the placeholder trophies, then replaces the story in CrystalCaves.unity. It is safe to run again. No NavMesh
    /// re-bake is needed: nothing it places is baked in.
    /// </summary>
    internal static partial class CrystalCavesBuilder
    {
        private const string StoryRootName = "Crystal Caves Story";
        private const string SpawnName = "Spawn At The Dock";
        // The scanner's range here: from the camp or the dock, the far chamber's signal still beeps and fills the bar a little.
        private const float ScannerRange = 450f;
        private static string RackName => BaseCampBuilder.StandGroupName(BaseCampBuilder.StandSet.Caves);
        private static int CavesRackFirstIndex => BaseCampBuilder.FirstStandIndex(BaseCampBuilder.StandSet.Caves); // after the tutorial's and the desert's
        private const string TrophyFolder = "Assets/Resources/SignalLost/Trophies";
        private const float RouteOverrideRadius = 20f;
        private const float AxolotlArenaRadius = 30f; // reaches the crystals ringing the chamber's long sides (34 m out)
        private const float ChamberTrigger = 30f;

        private static readonly Vector2[] TicketSpots = { new Vector2(30f, -80f), new Vector2(-55f, -72f), new Vector2(-105f, -62f) };
        private static readonly Vector2[] CrystalSpots = { new Vector2(-110f, 72f), new Vector2(-65f, 45f), new Vector2(-20f, 28f) };
        private static readonly Vector2[] NodeSpots = { new Vector2(55f, 0f), new Vector2(90f, 30f), new Vector2(105f, 88f) };
        private static IEnumerable<Vector2> TaskSpots => TicketSpots.Concat(CrystalSpots).Concat(NodeSpots);

        // The routes the player walks, for placing the route Chameleons near them.
        private static readonly Vector2[] UpperRoute = { T.MouthPath[3], TicketSpots[0], TicketSpots[1], TicketSpots[2], T.BeetleZone, new Vector2(-150f, 20f) };
        private static readonly Vector2[] DeepRoute =
        {
            new Vector2(-150f, 94f), CrystalSpots[0], CrystalSpots[1], CrystalSpots[2], T.MoleZone, NodeSpots[0], NodeSpots[1], NodeSpots[2], T.BearZone,
            new Vector2(60f, 100f), new Vector2(0f, 100f)
        };

        // =====================================================================
        // Spawn, respawn and the camp's stands
        // =====================================================================

        // The player starts on the dock by the boat (where a voyage lands them too), looking up the cove toward camp.
        private static void DockSpawn(out Vector3 at, out Quaternion facing)
        {
            Transform landing = Object.FindAnyObjectByType<IslandDock>().Landing;
            at = landing.position + Vector3.up * 0.1f;
            Vector3 toCamp = Ground(T.Camp) - landing.position;
            facing = Quaternion.LookRotation(new Vector3(toCamp.x, 0f, toCamp.z));
        }

        // After the first talk with Cavendish, the player respawns just west of the fire, looking toward the cave mouth.
        private static void CampRespawn(out Vector3 at, out Quaternion facing)
        {
            Vector2 spot = T.Camp + new Vector2(-3.2f, -0.8f);
            at = Ground(spot) + Vector3.up * 0.1f;
            Vector2 toMouth = T.MouthPath[1] - spot;
            facing = Quaternion.LookRotation(new Vector3(toMouth.x, 0f, toMouth.y));
        }

        // =====================================================================
        // Trophies and the ticket (low-poly kits in the desert trophies' style)
        // =====================================================================

        private enum CM
        {
            Rock, Shell, ShellDark, ShellCrystal, Fur, FurDark, MoleCrystal, MoleNail, BearFur, Claw, Geode, GeodeCrystal, Paper, Ink,
            AxolotlSkin, Gill, GillFringe, DarkBlue, Green, Orange, Purple, Pink, Cyan // the last six: the cave's crystal colours
        }

        private static Dictionary<CM, Material> caveMats;

        private static Dictionary<CM, Material> CaveMats()
        {
            const string mole = CaveEnemyKit.PrefabFolder + "/Bosses/ShardMole/Materials/", bear = CaveEnemyKit.PrefabFolder + "/Bosses/GeodeBear/Materials/";
            const string axolotl = CaveEnemyKit.PrefabFolder + "/Bosses/CaveAxolotl/Materials/";
            const string kit = CaveEnemyKit.MaterialFolder + "/";
            return caveMats ??= new Dictionary<CM, Material>
            {
                [CM.Rock] = Mat(CrystalAssetBuilder.MaterialFolder + "/RockCave.mat"),
                [CM.Shell] = TicketMaterial("TrophyBeetleShell", new Color(0.78f, 0.14f, 0.12f), 0f), // matte, like the desert trophies
                [CM.ShellDark] = Mat(kit + "BeetleBugLennonUnderside.mat"),
                [CM.ShellCrystal] = CrystalAssetBuilder.CrystalMaterial(CrystalAssetBuilder.CrystalColor.Pink),
                [CM.Fur] = Mat(mole + "ShardMoleFur.mat"),
                [CM.FurDark] = Mat(mole + "ShardMoleFurDark.mat"),
                [CM.MoleCrystal] = Mat(mole + "ShardMoleCrystal.mat"),
                [CM.MoleNail] = Mat(mole + "ShardMoleNail.mat"),
                [CM.BearFur] = Mat(bear + "GeodeBearFurDark.mat"),
                [CM.Claw] = Mat(bear + "GeodeBearNail.mat"),
                [CM.Geode] = Mat(bear + "GeodeBearGeode.mat"),
                [CM.GeodeCrystal] = Mat(bear + "GeodeBearCrystalPurple.mat"),
                [CM.Paper] = TicketMaterial("TicketPaper", new Color(0.8f, 0.55f, 0.3f), 0.35f),
                [CM.Ink] = TicketMaterial("TicketInk", new Color(0.1f, 0.07f, 0.06f), 0f),
                [CM.AxolotlSkin] = Mat(axolotl + "CaveAxolotlSkin.mat"),
                [CM.Gill] = Mat(axolotl + "CaveAxolotlGill.mat"),
                [CM.GillFringe] = Mat(axolotl + "CaveAxolotlGillFringe.mat"),
                [CM.DarkBlue] = CrystalAssetBuilder.CrystalMaterial(CrystalAssetBuilder.CrystalColor.DarkBlue),
                [CM.Green] = CrystalAssetBuilder.CrystalMaterial(CrystalAssetBuilder.CrystalColor.Green),
                [CM.Orange] = CrystalAssetBuilder.CrystalMaterial(CrystalAssetBuilder.CrystalColor.Orange),
                [CM.Purple] = CrystalAssetBuilder.CrystalMaterial(CrystalAssetBuilder.CrystalColor.Purple),
                [CM.Pink] = CrystalAssetBuilder.CrystalMaterial(CrystalAssetBuilder.CrystalColor.Pink),
                [CM.Cyan] = CrystalAssetBuilder.CrystalMaterial(CrystalAssetBuilder.CrystalColor.Cyan),
            };
        }

        // A matte trophy or ticket material, set every install. The ticket's glow faintly in their own colours, so the
        // cave's coloured light doesn't tint them.
        private static Material TicketMaterial(string name, Color color, float glow)
        {
            Material material = CaveEnemyKit.Lit(name, color, 0f, 0.2f);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.35f);
            material.SetColor("_EmissionColor", color * glow);
            if (glow > 0f) material.EnableKeyword("_EMISSION");
            else material.DisableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(material);
            return material;
        }

        // A kit saved as a mesh asset (keeping an existing asset's GUID) on a new object with its materials.
        private static GameObject CaveKitProp(string meshName, FlatMeshKit<CM> kit, string name, Transform parent)
        {
            Mesh mesh = kit.Build(meshName);
            var materials = new Material[mesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = CaveMats()[FlatMeshKit<CM>.MaterialOf(mesh, i)];
            EnsureFolder(MeshFolder);
            string path = MeshFolder + "/" + meshName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                mesh = existing;
            }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }

        private static void BuildCaveTrophies()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/SignalLost")) AssetDatabase.CreateFolder("Assets/Resources", "SignalLost");
            if (!AssetDatabase.IsValidFolder(TrophyFolder)) AssetDatabase.CreateFolder("Assets/Resources/SignalLost", "Trophies");
            SaveCaveTrophy("beetles_elytra", "The Beetles' Elytra", "TrophyBeetlesElytra", ElytraKit());
            SaveCaveTrophy("shard_mole_hide", "Shard Mole Hide", "TrophyShardMoleHide", MoleHideKit());
            SaveCaveTrophy("geode_bear_claw", "Geode Bear Claw", "TrophyGeodeBearClaw", BearClawKit());
            SaveCaveTrophy(CavesStory.AxolotlTrophyId, "Cave Axolotl Gill Frond", "TrophyCaveAxolotlGill", GillFrondKit());
        }

        // Like the desert's: resting on y = 0, centred, about 0.7 m across.
        private static void SaveCaveTrophy(string id, string label, string meshName, FlatMeshKit<CM> kit)
        {
            var holder = new GameObject(id);
            GameObject model = CaveKitProp(meshName, kit, label, holder.transform);
            Bounds bounds = model.GetComponent<MeshFilter>().sharedMesh.bounds;
            float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float scale = size > 0.01f ? 0.7f / size : 1f;
            model.transform.localScale = Vector3.one * scale;
            model.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z) * scale;
            PrefabUtility.SaveAsPrefabAsset(holder, TrophyFolder + "/" + id + ".prefab");
            Object.DestroyImmediate(holder);
        }

        private static FlatMeshKit<CM> RockBase(float radius)
        {
            var kit = new FlatMeshKit<CM>();
            kit.Add(LowPolyMesh.Lathe("base", new[] { new Vector2(0f, 0f), new Vector2(radius, 0f), new Vector2(radius * 0.85f, 0.1f), new Vector2(0f, 0.12f) }, 7), Matrix4x4.identity, CM.Rock);
            return kit;
        }

        // The Beetles' elytra: a pair of glossy red wing cases standing up from a dark thorax knob, spread in a V like a
        // beetle about to fly, studded with their crystal on the front, on a cave rock.
        private static FlatMeshKit<CM> ElytraKit()
        {
            FlatMeshKit<CM> kit = RockBase(0.32f);
            kit.Box(new Vector3(0f, 0.26f, 0f), new Vector3(0.07f, 0.28f, 0.07f), Quaternion.identity, CM.ShellDark);
            kit.Add(LowPolyMesh.Icosphere("thorax", 1), Matrix4x4.TRS(new Vector3(0f, 0.42f, 0f), Quaternion.identity, new Vector3(0.1f, 0.07f, 0.08f)), CM.ShellDark);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 along = new Vector3(side * 0.3f, 1f, 0.15f).normalized; // up, spread outward, leaning to the front
                Vector3 front = (Vector3.forward - Vector3.Dot(Vector3.forward, along) * along).normalized;
                Quaternion turn = Quaternion.LookRotation(along, front);
                Vector3 centre = new Vector3(side * 0.08f, 0.42f, 0f) + along * 0.28f;
                kit.Add(LowPolyMesh.Icosphere("case", 1), Matrix4x4.TRS(centre, turn, new Vector3(0.1f, 0.05f, 0.3f)), CM.Shell);
                for (int i = 0; i < 3; i++)
                    kit.Add(LowPolyMesh.TaperedPrism("stud", 5, 0f), Matrix4x4.TRS(centre + front * 0.04f + along * ((i - 1) * 0.12f), Quaternion.LookRotation(front),
                        new Vector3(0.028f, 0.028f, 0.07f)), CM.ShellCrystal);
            }
            return kit;
        }

        // The Shard Mole's hide: a folded pelt, dark stripe down its back and crystal shards still in it, a pair of claws
        // hanging over the front, draped on a cave rock.
        private static FlatMeshKit<CM> MoleHideKit()
        {
            FlatMeshKit<CM> kit = RockBase(0.34f);
            kit.Add(LowPolyMesh.Icosphere("rock", 0), Matrix4x4.TRS(new Vector3(0f, 0.2f, 0f), Quaternion.identity, new Vector3(0.22f, 0.14f, 0.2f)), CM.Rock);
            kit.Add(LowPolyMesh.Icosphere("pelt", 1), Matrix4x4.TRS(new Vector3(0f, 0.32f, 0f), Quaternion.Euler(8f, 0f, 0f), new Vector3(0.34f, 0.08f, 0.3f)), CM.Fur);
            kit.Add(LowPolyMesh.Icosphere("stripe", 0), Matrix4x4.TRS(new Vector3(0f, 0.37f, -0.02f), Quaternion.Euler(8f, 0f, 0f), new Vector3(0.09f, 0.05f, 0.28f)), CM.FurDark);
            for (int i = 0; i < 4; i++)
            {
                Vector3 at = new Vector3((i % 2 == 0 ? -1f : 1f) * 0.08f, 0.39f, (i - 1.5f) * 0.1f);
                kit.Add(LowPolyMesh.TaperedPrism("shard", 5, 0f), Matrix4x4.TRS(at, Quaternion.Euler(-90f + (i - 1.5f) * 12f, i * 40f, 0f), new Vector3(0.04f, 0.04f, 0.16f + (i % 2) * 0.06f)),
                    CM.MoleCrystal);
            }
            foreach (float side in new[] { -1f, 1f })
                for (int c = 0; c < 3; c++)
                    kit.Add(LowPolyMesh.TaperedPrism("claw", 4, 0f), Matrix4x4.TRS(new Vector3(side * 0.12f + (c - 1) * 0.025f, 0.3f, 0.27f), Quaternion.Euler(70f, 0f, 0f),
                        new Vector3(0.018f, 0.018f, 0.12f)), CM.MoleNail);
            return kit;
        }

        // The Geode Bear's claw: a furred paw with three great curved claws, a split geode glittering behind it.
        private static FlatMeshKit<CM> BearClawKit()
        {
            FlatMeshKit<CM> kit = RockBase(0.34f);
            kit.Add(LowPolyMesh.Icosphere("paw", 1), Matrix4x4.TRS(new Vector3(0f, 0.2f, 0.04f), Quaternion.identity, new Vector3(0.22f, 0.12f, 0.18f)), CM.BearFur);
            for (int c = 0; c < 3; c++)
            {
                Vector3 previous = new Vector3((c - 1) * 0.1f, 0.22f, 0.18f);
                for (int i = 0; i < 4; i++)
                {
                    float k = (i + 1) / 4f;
                    Vector3 next = new Vector3((c - 1) * 0.1f * (1f + k * 0.2f), 0.22f + Mathf.Sin(k * 1.4f) * 0.26f, 0.18f + k * 0.12f - k * k * 0.14f);
                    float r = Mathf.Lerp(0.045f, 0.01f, k);
                    kit.Add(LowPolyMesh.TaperedPrism("claw", 6, 0.7f), Matrix4x4.TRS(previous, Quaternion.LookRotation(next - previous), new Vector3(r, r, Vector3.Distance(previous, next) * 1.05f)), CM.Claw);
                    previous = next;
                }
            }
            kit.Add(LowPolyMesh.Icosphere("geode", 0), Matrix4x4.TRS(new Vector3(0f, 0.22f, -0.16f), Quaternion.Euler(-30f, 0f, 0f), new Vector3(0.16f, 0.14f, 0.07f)), CM.Geode);
            for (int i = 0; i < 4; i++)
                kit.Add(LowPolyMesh.TaperedPrism("gem", 5, 0f), Matrix4x4.TRS(new Vector3((i - 1.5f) * 0.05f, 0.24f, -0.12f), Quaternion.Euler(-30f + (i % 2) * 15f, 0f, 0f),
                    new Vector3(0.025f, 0.025f, 0.09f)), CM.GeodeCrystal);
            return kit;
        }

        // The Cave Axolotl's gill frond: one coral gill stalk curving up out of a scrap of its pale skin, feathery fringe
        // down its back, ringed by six small crystals in the cave's six colours (it wore them all, hiding), on a cave rock.
        private static FlatMeshKit<CM> GillFrondKit()
        {
            FlatMeshKit<CM> kit = RockBase(0.34f);
            kit.Add(LowPolyMesh.Icosphere("skin", 1), Matrix4x4.TRS(new Vector3(0f, 0.16f, 0f), Quaternion.identity, new Vector3(0.2f, 0.09f, 0.18f)), CM.AxolotlSkin);
            Vector3 previous = new Vector3(0f, 0.2f, 0f);
            for (int i = 0; i < 5; i++)
            {
                float k = (i + 1) / 5f;
                Vector3 next = new Vector3(0f, 0.2f + k * 0.5f, k * k * 0.18f); // up, bending forward like the boss's gills
                float r = Mathf.Lerp(0.05f, 0.012f, k);
                Quaternion along = Quaternion.LookRotation(next - previous);
                kit.Add(LowPolyMesh.TaperedPrism("gill", 5, 0.75f), Matrix4x4.TRS(previous, along, new Vector3(r, r, Vector3.Distance(previous, next) * 1.05f)), CM.Gill);
                if (i < 4)
                    foreach (float side in new[] { -1f, 1f }) // a pair of fringe spikes per segment, swept back
                        kit.Add(LowPolyMesh.TaperedPrism("fringe", 3, 0f), Matrix4x4.TRS(next, Quaternion.LookRotation(new Vector3(side * 0.5f, -0.2f, -1f)),
                            new Vector3(0.015f, 0.015f, 0.12f - i * 0.02f)), CM.GillFringe);
                previous = next;
            }
            CM[] colours = { CM.DarkBlue, CM.Green, CM.Orange, CM.Purple, CM.Pink, CM.Cyan };
            for (int i = 0; i < colours.Length; i++)
            {
                float a = i * Mathf.PI * 2f / colours.Length;
                var at = new Vector3(Mathf.Cos(a) * 0.17f, 0.12f, Mathf.Sin(a) * 0.15f);
                kit.Add(LowPolyMesh.TaperedPrism("crystal", 6, 0f), Matrix4x4.TRS(at, Quaternion.LookRotation(at.normalized * 0.5f + Vector3.up),
                    new Vector3(0.028f, 0.028f, 0.11f + (i % 2) * 0.04f)), colours[i]);
            }
            return kit;
        }

        // A big concert ticket, 1.2 x 0.6 m, standing upright (its faces along Z) to float like a pickup: tan card with
        // notched ends, an ink frame round "ADMIT ONE" in blocky low-poly letters and a column of number dashes at each
        // end, printed on both faces (mirrored on the back, so it reads right as it turns).
        private static FlatMeshKit<CM> TicketKit()
        {
            var kit = new FlatMeshKit<CM>();
            const float w = 0.6f, h = 0.3f, t = 0.015f;
            List<Vector2> outline = TicketOutline(w, h);
            foreach (float side in new[] { -1f, 1f })
                for (int i = 0; i < outline.Count; i++)
                {
                    Vector2 a = outline[i], b = outline[(i + 1) % outline.Count];
                    kit.Tri(new Vector3(0f, 0f, side * t), new Vector3(a.x, a.y, side * t), new Vector3(b.x, b.y, side * t), Vector3.forward * side, CM.Paper);
                }
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Count];
                Vector2 edge = b - a, outward = new Vector2(edge.y, -edge.x); // the outline runs anticlockwise
                kit.Quad(new Vector3(a.x, a.y, -t), new Vector3(b.x, b.y, -t), new Vector3(b.x, b.y, t), new Vector3(a.x, a.y, t), new Vector3(outward.x, outward.y, 0f), CM.Paper);
            }

            foreach (float side in new[] { -1f, 1f })
            {
                float z = side * (t + 0.002f);
                void Stroke(Vector2 from, Vector2 to, float width)
                {
                    Vector2 mid = (from + to) * 0.5f, d = to - from;
                    float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                    kit.Box(new Vector3(mid.x * -side, mid.y, z), new Vector3(d.magnitude + width, width, 0.004f), Quaternion.Euler(0f, 0f, angle * -side), CM.Ink);
                }
                // The frame.
                Stroke(new Vector2(-0.36f, -0.2f), new Vector2(0.36f, -0.2f), 0.025f);
                Stroke(new Vector2(-0.36f, 0.2f), new Vector2(0.36f, 0.2f), 0.025f);
                Stroke(new Vector2(-0.36f, -0.2f), new Vector2(-0.36f, 0.2f), 0.025f);
                Stroke(new Vector2(0.36f, -0.2f), new Vector2(0.36f, 0.2f), 0.025f);
                // ADMIT over ONE.
                Word("ADMIT", new Vector2(0f, 0.075f), Stroke);
                Word("ONE", new Vector2(0f, -0.085f), Stroke);
                // The ticket number, a column of dashes inside each notched end.
                foreach (float x in new[] { -0.47f, 0.47f })
                    for (int i = 0; i < 6; i++)
                        Stroke(new Vector2(x - 0.018f, -0.15f + i * 0.06f), new Vector2(x + 0.018f, -0.15f + i * 0.06f), 0.02f);
            }
            return kit;
        }

        // The ticket's outline, anticlockwise from the bottom-left corner: straight long sides, and short ends with a big
        // round notch in the middle and two small scallops either side of it.
        private static List<Vector2> TicketOutline(float w, float h)
        {
            var points = new List<Vector2> { new Vector2(-w, -h), new Vector2(w, -h) };
            (float y, float r)[] notches = { (-0.21f, 0.022f), (-0.13f, 0.022f), (0f, 0.07f), (0.13f, 0.022f), (0.21f, 0.022f) };
            void End(float x, float inward, bool up)
            {
                foreach ((float y, float r) in up ? notches : notches.Reverse().ToArray())
                    for (int k = 0; k <= 6; k++)
                    {
                        float a = (k / 6f - 0.5f) * Mathf.PI; // -90 to 90 degrees round the notch
                        points.Add(new Vector2(x + inward * r * Mathf.Cos(a), y + (up ? 1f : -1f) * r * Mathf.Sin(a)));
                    }
            }
            End(w, -1f, true);
            points.Add(new Vector2(w, h));
            points.Add(new Vector2(-w, h));
            End(-w, 1f, false);
            return points;
        }

        // Blocky capitals from straight strokes, about 9 x 11 cm each, centred on the given point.
        private static void Word(string text, Vector2 centre, Action<Vector2, Vector2, float> stroke)
        {
            const float cellW = 0.085f, cellH = 0.11f, gap = 0.03f, width = 0.022f;
            var shapes = new Dictionary<char, float[]>
            {
                ['A'] = new[] { 0f, 0f, 0.5f, 1f, 0.5f, 1f, 1f, 0f, 0.22f, 0.42f, 0.78f, 0.42f },
                ['D'] = new[] { 0f, 0f, 0f, 1f, 0f, 1f, 0.6f, 1f, 0.6f, 1f, 1f, 0.7f, 1f, 0.7f, 1f, 0.3f, 1f, 0.3f, 0.6f, 0f, 0.6f, 0f, 0f, 0f },
                ['M'] = new[] { 0f, 0f, 0f, 1f, 0f, 1f, 0.5f, 0.45f, 0.5f, 0.45f, 1f, 1f, 1f, 1f, 1f, 0f },
                ['I'] = new[] { 0.5f, 0f, 0.5f, 1f, 0.15f, 1f, 0.85f, 1f, 0.15f, 0f, 0.85f, 0f },
                ['T'] = new[] { 0f, 1f, 1f, 1f, 0.5f, 1f, 0.5f, 0f },
                ['O'] = new[] { 0.3f, 0f, 0.7f, 0f, 0.7f, 0f, 1f, 0.3f, 1f, 0.3f, 1f, 0.7f, 1f, 0.7f, 0.7f, 1f, 0.7f, 1f, 0.3f, 1f, 0.3f, 1f, 0f, 0.7f, 0f, 0.7f, 0f, 0.3f, 0f, 0.3f, 0.3f, 0f },
                ['N'] = new[] { 0f, 0f, 0f, 1f, 0f, 1f, 1f, 0f, 1f, 0f, 1f, 1f },
                ['E'] = new[] { 0f, 0f, 0f, 1f, 0f, 1f, 1f, 1f, 0f, 0.5f, 0.75f, 0.5f, 0f, 0f, 1f, 0f },
            };
            float total = text.Length * cellW + (text.Length - 1) * gap;
            for (int c = 0; c < text.Length; c++)
            {
                float[] s = shapes[text[c]];
                var origin = new Vector2(centre.x - total * 0.5f + c * (cellW + gap), centre.y - cellH * 0.5f);
                for (int i = 0; i + 3 < s.Length; i += 4)
                    stroke(origin + new Vector2(s[i] * cellW, s[i + 1] * cellH), origin + new Vector2(s[i + 2] * cellW, s[i + 3] * cellH), width);
            }
        }

        private static Material Mat(string path) =>
            AssetDatabase.LoadAssetAtPath<Material>(path) ?? throw new InvalidOperationException("Missing material " + path);

        // A primitive with no collider (props and placeholders; interaction uses the holder's box).
        private static GameObject Shape(Transform parent, PrimitiveType type, Vector3 localPosition, Quaternion localRotation, Vector3 scale, Material material)
        {
            GameObject shape = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(shape.GetComponent<Collider>());
            shape.transform.SetParent(parent, false);
            shape.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            shape.transform.localScale = scale;
            shape.GetComponent<Renderer>().sharedMaterial = material;
            return shape;
        }

        // =====================================================================
        // Install into CrystalCaves.unity
        // =====================================================================

        [MenuItem("Signal Lost/Crystal Caves/Install Crystal Caves Story")]
        public static void InstallStoryMenu()
        {
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Install the Crystal Caves story",
                    "This rebuilds CrystalCavesCamp.prefab (adding the caves' trophy rack) and installs the jobs, quest tasks, mini-bosses, " +
                    "Chameleons, the Cave Axolotl's chamber and the story in CrystalCaves.unity, replacing any earlier install. Continue?", "Install", "Cancel"))
                return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            InstallStory();
        }

        /// <summary>Batch entry: installs the story and validates the level.</summary>
        public static void InstallStoryBatch()
        {
            try
            {
                InstallStory();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void InstallStory()
        {
            foreach (string path in new[] { ShardMoleBuilder.PrefabPath, GeodeBearBuilder.PrefabPath, CaveAxolotlBuilder.PrefabPath, CrystalChameleonBuilder.PrefabPath }
                         .Concat(CaveBeetleBuilder.MemberPrefabs.Select(m => m.PrefabPath)))
                if (CaveEnemyKit.Load(path) == null) throw new InvalidOperationException("Missing " + path + ". Build the cave enemies first.");
            BuildCaveTrophies();
            BuildCampPrefab();
            AssetDatabase.SaveAssets();

            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            T.Generate();
            foreach (string old in new[] { StoryRootName, "Mini-Boss HUD" })
                for (GameObject found = GameObject.Find(old); found != null; found = GameObject.Find(old)) Object.DestroyImmediate(found);
            Physics.SyncTransforms();
            InstallGameplayFixes();

            var story = new GameObject(StoryRootName);
            Transform jobsGroup = Group("Mini-Bosses", story.transform);
            Transform tasks = Group("Quest Tasks", story.transform);
            Transform route = Group("Route Chameleons", story.transform);
            Transform chamber = Group("Cave Axolotl Chamber", story.transform);
            new GameObject("Mini-Boss HUD").AddComponent<MiniBossHud>();

            // Job 1: the Beetles, a band of four in their zone on the upper level.
            var band = new GameObject("The Beetles (band)");
            band.transform.SetParent(jobsGroup, false);
            band.transform.position = Ground(T.BeetleZone);
            (string Name, string PrefabPath)[] members = CaveBeetleBuilder.MemberPrefabs;
            var beetles = new CaveBeetle[members.Length];
            Vector2[] spots = { new Vector2(-4f, 4f), new Vector2(4f, 4f), new Vector2(-4f, -4f), new Vector2(4f, -4f) };
            for (int i = 0; i < members.Length; i++)
                beetles[i] = (CaveBeetle)PlaceBoss(members[i].PrefabPath, members[i].Name, T.BeetleZone + spots[i], TicketSpots[2], band.transform);
            CaveBeetleBuilder.ConfigureBand(band.AddComponent<BeetleBand>(), beetles, T.ZoneRadius);
            // Job 2 and 3: the Shard Mole in the middle of the deep level, the Geode Bear by its east wall.
            DesertMiniBoss mole = PlaceBoss(ShardMoleBuilder.PrefabPath, "Shard Mole", T.MoleZone, CrystalSpots[2], jobsGroup);
            DesertMiniBoss bear = PlaceBoss(GeodeBearBuilder.PrefabPath, "Geode Bear", T.BearZone, NodeSpots[2], jobsGroup);

            QuestTask[] tickets = PlaceTasks(tasks, "Concert Ticket", TicketSpots, TicketTask);
            QuestTask[] crystals = PlaceTasks(tasks, "Loose Crystal", CrystalSpots, CrystalTask);
            QuestTask[] nodes = PlaceTasks(tasks, "Power Node", NodeSpots, NodeTask);

            PlaceRouteChameleons(route);
            PlaceRouteFodder(Group(FodderGroupName, story.transform));
            (AxolotlLair lair, QuestSignalSource chamberSignal) = BuildChamber(chamber);

            var camp = Object.FindAnyObjectByType<IslandCamp>();
            TrophyStand[] rack = camp.transform.Find(RackName).GetComponentsInChildren<TrophyStand>();
            var data = new SerializedObject(story.AddComponent<CavesStory>());
            data.FindProperty("cavendish").objectReferenceValue = camp.Cavendish;
            CampRespawn(out Vector3 respawnAt, out Quaternion respawnFacing);
            var respawn = new GameObject("Camp Respawn (after the first talk with Cavendish)").transform;
            respawn.SetParent(story.transform, false);
            respawn.SetPositionAndRotation(respawnAt, respawnFacing);
            data.FindProperty("campRespawn").objectReferenceValue = respawn;
            data.FindProperty("lair").objectReferenceValue = lair;
            data.FindProperty("chamberSignal").objectReferenceValue = chamberSignal;
            SetObjects(data.FindProperty("rack"), rack);
            SerializedProperty jobs = data.FindProperty("jobs");
            jobs.arraySize = 3;
            Job(jobs.GetArrayElementAtIndex(0), "Ticket to Ride", band, beetles, "The Beetles", "Signal Amplifier", "beetles_elytra", tickets,
                "The Beetles have dropped their concert tickets all through the front of the cave. Pick them up.", "Tickets",
                "The Beetles are rehearsing on the ledge at the far west of the upper level. Go and catch the show.",
                "Beat the Beetles. Jump as the ring closes on a box-in; a beetle that lands on its back shows its belly.",
                "West through the front of the cave, mechanic. Follow the tickets.", "The tickets lead to the Beetles. Bands always leave a mess.");
            Job(jobs.GetArrayElementAtIndex(1), "Set in Stone", mole.gameObject, new[] { mole }, "The Shard Mole", "Antenna Array", "shard_mole_hide", crystals,
                "The Shard Mole's digging knocked crystals loose on the deep level. Take the ramp down past the Beetles' stage, then head east and re-seat them.",
                "Crystals re-seated",
                "The Shard Mole is digging in the middle of the deep level. Go and find it.",
                "Defeat the Shard Mole. Keep quiet and it listens; keep moving when it burrows, and watch the floor for its mound.",
                "Down the ramp past the Beetles' stage, then east. Put those crystals back.", "The loose crystals lead straight to the Mole.");
            Job(jobs.GetArrayElementAtIndex(2), "Bear with Me", bear.gameObject, new[] { bear }, "The Geode Bear", "Power Cell", "geode_bear_claw", nodes,
                "The Geode Bear drained the old power nodes on the deep level, east of the Mole's hollow. Restore them.", "Nodes restored",
                "The Geode Bear is up by the east wall of the deep level. Go and wake it properly.",
                "Defeat the Geode Bear. Look away when it yawns and stay out of reach of its swipes.",
                "East of the Mole's hollow. Get those power nodes humming again.", "Whatever you do, don't watch it yawn.");
            data.ApplyModifiedPropertiesWithoutUndo();
            band.SetActive(false);
            mole.gameObject.SetActive(false);
            bear.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            ValidateStory();
            Validate();
            Debug.Log("CRYSTAL_CAVES_STORY_INSTALLED");
        }

        // The spawn moves to the camp, Eugene's shop sells the caves' upgrades, and the one dock's boat departs too.
        private static void InstallGameplayFixes()
        {
            DockSpawn(out Vector3 at, out Quaternion facing);
            PlayerSpawnPoint spawn = Object.FindAnyObjectByType<PlayerSpawnPoint>();
            spawn.name = SpawnName;
            spawn.transform.SetPositionAndRotation(at, facing);
            Object.FindAnyObjectByType<PlayerGear>().transform.SetPositionAndRotation(at + Vector3.up * 0.05f, facing);
            var range = new SerializedObject(Object.FindAnyObjectByType<PlayerGear>().GetComponentInChildren<ScannerController>(true));
            range.FindProperty("levelRangeOverride").floatValue = ScannerRange;
            range.ApplyModifiedPropertiesWithoutUndo();

            if (Object.FindAnyObjectByType<ShopRegion>() == null)
            {
                var shop = new SerializedObject(GameObject.Find("Gameplay").AddComponent<ShopRegion>());
                shop.FindProperty("region").enumValueIndex = (int)UpgradeRegion.CrystalCaves;
                shop.ApplyModifiedPropertiesWithoutUndo();
            }

            var dock = new SerializedObject(Object.FindAnyObjectByType<IslandDock>());
            dock.FindProperty("alsoDeparts").boolValue = true;
            dock.FindProperty("requiredStage").stringValue = CavesStory.HomeStage; // sails once the axolotl's trophy is home and Cavendish has wrapped up
            dock.FindProperty("destinationName").stringValue = "the next island";
            dock.FindProperty("lockedMessage").stringValue = "Cavendish won't sail until the caves give back what they took.";
            dock.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Job(SerializedProperty job, string title, GameObject group, DesertMiniBoss[] bosses, string bossName, string partName, string trophy,
            QuestTask[] tasks, string taskObjective, string taskLabel, string hunt, string fight, params string[] reminders)
        {
            job.FindPropertyRelative("title").stringValue = title;
            job.FindPropertyRelative("group").objectReferenceValue = group;
            SetObjects(job.FindPropertyRelative("bosses"), bosses);
            job.FindPropertyRelative("bossName").stringValue = bossName;
            job.FindPropertyRelative("partName").stringValue = partName;
            job.FindPropertyRelative("trophyId").stringValue = trophy;
            SetObjects(job.FindPropertyRelative("tasks"), tasks);
            job.FindPropertyRelative("taskObjective").stringValue = taskObjective;
            job.FindPropertyRelative("taskLabel").stringValue = taskLabel;
            job.FindPropertyRelative("huntObjective").stringValue = hunt;
            job.FindPropertyRelative("fightObjective").stringValue = fight;
            SerializedProperty lines = job.FindPropertyRelative("taskReminders");
            lines.arraySize = reminders.Length;
            for (int i = 0; i < reminders.Length; i++) lines.GetArrayElementAtIndex(i).stringValue = reminders[i];
        }

        private static void SetObjects(SerializedProperty list, Object[] items)
        {
            list.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        // A pure duel (no fodder spawner), facing the way the player comes in, with its zone as its arena.
        private static DesertMiniBoss PlaceBoss(string prefabPath, string label, Vector2 home, Vector2 facing, Transform parent)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(CaveEnemyKit.Load(prefabPath), parent);
            go.name = label;
            Vector2 look = facing - home;
            go.transform.SetPositionAndRotation(Ground(home), Quaternion.LookRotation(new Vector3(look.x, 0f, look.y)));
            go.AddComponent<QuestSignalSource>().enabled = false;
            var boss = go.GetComponent<DesertMiniBoss>();
            var data = new SerializedObject(boss);
            data.FindProperty("arenaRadius").floatValue = T.ZoneRadius;
            data.ApplyModifiedPropertiesWithoutUndo();
            return boss;
        }

        // =====================================================================
        // Quest tasks
        // =====================================================================

        private static QuestTask[] PlaceTasks(Transform parent, string label, Vector2[] spots, Func<Transform, QuestTask> build)
        {
            var result = new QuestTask[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                Vector2 p = ClearSpot(spots[i]);
                var holder = new GameObject($"{label} {i + 1}").transform;
                holder.SetParent(parent, false);
                holder.position = Ground(p);
                holder.rotation = Quaternion.Euler(0f, (i * 137f) % 360f, 0f);
                result[i] = build(holder);
            }
            return result;
        }

        // The nearest spot to the plan with nothing (a rock, a crystal) standing on it.
        private static Vector2 ClearSpot(Vector2 spot)
        {
            for (int i = 0; i < 60; i++)
            {
                float ring = i * 0.6f, angle = i * 2.39996f;
                Vector2 p = spot + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring;
                if (Slope(p) < 20f && !Physics.CheckSphere(Ground(p) + Vector3.up * 1.4f, 1.1f, ~0, QueryTriggerInteraction.Ignore)) return p;
            }
            throw new InvalidOperationException($"No clear ground near the task spot ({spot.x:0}, {spot.y:0}).");
        }

        // The E box goes on the holder, or on colliderOn (a child hidden when done, so nothing is left in the way).
        private static QuestTask AddTask(GameObject holder, string prompt, string doneMessage, GameObject[] hide, GameObject[] show, Vector3 boxCentre, Vector3 boxSize,
            GameObject colliderOn = null)
        {
            if (boxSize != Vector3.zero) // zero: the task's prop brings its own (a floating pickup's)
            {
                var box = (colliderOn != null ? colliderOn : holder).AddComponent<BoxCollider>();
                box.center = boxCentre;
                box.size = boxSize;
            }
            holder.AddComponent<QuestSignalSource>().enabled = false;
            var task = holder.AddComponent<QuestTask>();
            var data = new SerializedObject(task);
            data.FindProperty("prompt").stringValue = prompt;
            data.FindProperty("doneMessage").stringValue = doneMessage;
            data.FindProperty("puff").enumValueIndex = (int)ParticleBurst.Kind.Smoke;
            SetObjects(data.FindProperty("hideWhenDone"), hide);
            SetObjects(data.FindProperty("showWhenDone"), show);
            data.ApplyModifiedPropertiesWithoutUndo();
            return task;
        }

        private static Light Glint(Transform parent, Vector3 localPosition, Color color, float range, float intensity)
        {
            var light = new GameObject("Glint").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = localPosition;
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            return light;
        }

        // A big concert ticket floating upright over the floor, bobbing and slowly turning like a pickup but at its full
        // 1.2 m. Its E box is the floating pickup's (the player walks through it), and it goes with the ticket.
        private static QuestTask TicketTask(Transform holder)
        {
            var ticket = new GameObject("Ticket").transform;
            ticket.SetParent(holder, false);
            GameObject model = CaveKitProp("TaskConcertTicket", TicketKit(), "Ticket Model", ticket);
            Glint(model.transform, Vector3.zero, new Color(1f, 0.85f, 0.55f), 5f, 2f);
            var floating = new SerializedObject(ticket.gameObject.AddComponent<FloatingPickup>());
            floating.FindProperty("model").objectReferenceValue = model.transform;
            floating.FindProperty("size").floatValue = 1.2f;
            floating.ApplyModifiedPropertiesWithoutUndo();
            return AddTask(holder.gameObject, "Pick up the concert ticket", "Ticket", new[] { ticket.gameObject }, new GameObject[0], Vector3.zero, Vector3.zero);
        }

        // A crystal knocked over on its side; re-seated, it stands up and glows.
        private static QuestTask CrystalTask(Transform holder)
        {
            Material colour = NearestBatchMaterial(holder.position);
            var spire = CaveEnemyKit.Load(CrystalAssetBuilder.CrystalPath("CrystalSpire"));
            var fallen = (GameObject)PrefabUtility.InstantiatePrefab(spire, holder);
            fallen.name = "Fallen Crystal";
            fallen.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.25f, 0f), Quaternion.Euler(0f, 0f, 82f));
            fallen.transform.localScale = Vector3.one * 0.7f;
            Color glow = colour.HasProperty("_EmissionColor") ? colour.GetColor("_EmissionColor") : Color.white;
            GameObject fallenGlint = Glint(holder, Vector3.up * 0.8f, glow, 4f, 0.8f).gameObject;
            var upright = (GameObject)PrefabUtility.InstantiatePrefab(spire, holder);
            upright.name = "Re-seated Crystal";
            upright.transform.SetLocalPositionAndRotation(Vector3.down * 0.1f, Quaternion.identity);
            upright.transform.localScale = Vector3.one * 0.8f;
            Glint(upright.transform, Vector3.up * 2f, glow, 8f, 3f);
            foreach (GameObject crystal in new[] { fallen, upright })
            {
                foreach (Renderer part in crystal.GetComponentsInChildren<Renderer>()) part.sharedMaterial = colour;
                foreach (Collider part in crystal.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(part);
            }
            return AddTask(holder.gameObject, "Re-seat the crystal", "Crystal re-seated", new[] { fallen, fallenGlint }, new[] { upright },
                new Vector3(0f, 0.6f, 0f), new Vector3(1.6f, 1.2f, 1.6f));
        }

        // An old power node, a metal post with a dead core; restored, its core glows.
        private static QuestTask NodeTask(Transform holder)
        {
            Material metal = Mat(CaveEnemyKit.MaterialFolder + "/GeodeBearCellMetal.mat");
            Shape(holder, PrimitiveType.Cylinder, new Vector3(0f, 0.55f, 0f), Quaternion.identity, new Vector3(0.5f, 0.55f, 0.5f), metal).name = "Node Post";
            Shape(holder, PrimitiveType.Cylinder, new Vector3(0f, 0.05f, 0f), Quaternion.identity, new Vector3(0.9f, 0.05f, 0.9f), metal).name = "Node Base";
            GameObject dead = Shape(holder, PrimitiveType.Sphere, new Vector3(0f, 1.3f, 0f), Quaternion.identity, Vector3.one * 0.45f, Mat(CaveEnemyKit.MaterialFolder + "/GeodeBearRock.mat"));
            dead.name = "Dead Core";
            GameObject live = Shape(holder, PrimitiveType.Sphere, new Vector3(0f, 1.3f, 0f), Quaternion.identity, Vector3.one * 0.45f, Mat(CaveEnemyKit.MaterialFolder + "/GeodeBearCellGlow.mat"));
            live.name = "Live Core";
            Glint(live.transform, Vector3.zero, new Color(0.4f, 0.95f, 1f), 9f, 3f);
            return AddTask(holder.gameObject, "Restore the power node", "Node restored", new[] { dead }, new[] { live }, new Vector3(0f, 0.8f, 0f), new Vector3(1.2f, 1.6f, 1.2f));
        }

        private static Material NearestBatchMaterial(Vector3 at) =>
            Object.FindObjectsByType<CrystalBatch>().Where(b => b.CrystalMaterial != null)
                .OrderBy(b => (b.transform.position - at).sqrMagnitude).First().CrystalMaterial;

        // =====================================================================
        // Route Chameleons
        // =====================================================================

        private sealed class Spot
        {
            public Vector3 At;
            public Quaternion Facing;
            public Material Colour;
            public bool Wall, Upper;
            public float Score;
            public Vector2 Flat => new Vector2(At.x, At.z);
        }

        // Beside real crystal batches near the routes (the player passes within 20 m of them), at least 60 m apart, off
        // the tasks, the fight zones, the rack and the chamber; some on walls, none on walls near the chamber.
        private static void PlaceRouteChameleons(Transform parent)
        {
            var candidates = new List<Spot>();
            foreach (CrystalBatch batch in Object.FindObjectsByType<CrystalBatch>())
            {
                if (batch.CrystalMaterial == null || batch.name.StartsWith("Giant")) continue;
                Vector3 up = batch.transform.up;
                var spots = new List<Spot>();
                if (Mathf.Abs(up.y) < 0.5f) spots.Add(WallSpot(batch, up));
                else if (up.y > 0f)
                {
                    spots.Add(FloorSpot(batch));
                    spots.Add(WallNear(batch));
                }
                foreach (Spot spot in spots) if (spot != null && RouteCandidate(spot)) candidates.Add(spot);
            }
            Debug.Log($"Route chameleon candidates: upper {candidates.Count(c => c.Upper && !c.Wall)} floor / {candidates.Count(c => c.Upper && c.Wall)} wall, " +
                      $"deep {candidates.Count(c => !c.Upper && !c.Wall)} floor / {candidates.Count(c => !c.Upper && c.Wall)} wall");
            var chosen = new List<Spot>();
            void Pick(bool upper, bool wall, int upTo)
            {
                foreach (Spot spot in candidates.Where(s => s.Upper == upper && s.Wall == wall).OrderBy(s => s.Score))
                {
                    if (chosen.Count(s => s.Upper == upper && (!wall || s.Wall)) >= upTo) return;
                    if (chosen.All(s => Vector2.Distance(s.Flat, spot.Flat) >= 60f)) chosen.Add(spot);
                }
            }
            Pick(true, true, 1);
            Pick(false, true, 2);
            Pick(true, false, 5);
            Pick(false, false, 8);

            int number = 0;
            foreach (Spot spot in chosen)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(CaveEnemyKit.Load(CrystalChameleonBuilder.PrefabPath), parent);
                go.name = $"{(spot.Wall ? "Wall " : "")}Route Chameleon {++number} ({(spot.Upper ? "upper" : "deep")})";
                go.transform.SetPositionAndRotation(spot.At, spot.Facing);
                var data = new SerializedObject(go.GetComponent<CrystalChameleon>());
                data.FindProperty("crystalMaterial").objectReferenceValue = spot.Colour;
                data.FindProperty("hideInWall").boolValue = spot.Wall;
                data.FindProperty("questOverrideRadius").floatValue = RouteOverrideRadius;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // =====================================================================
        // Route fodder (Amanda's map, 11-20)
        // =====================================================================

        private const string FodderGroupName = "Route Fodder";
        private const float FodderZoneGap = 45f;  // zone radius 24 + the 14 m territory + margin, so a duel never wakes one
        private const float FodderChamberPad = 21f;
        private const float FodderMaxShift = 12f; // how far a spot may move to get clear of rock and crystals
        // Each spot is pulled toward its route: half the way, at most 15 m, stopping 6 m short of it.
        private const float FodderMaxPull = 15f, FodderRouteKeep = 6f;
        private const float FodderTaskGap = 8f;
        private const float FodderGroupGap = 25f; // enemies this far apart don't join one fight; the pull never merges groups

        // B Quartz Bat, Q Quill Porcupine, R Gem Ram, S Prism Snail, read off Amanda's map (map metres, second map 11-20).
        private static readonly (char Kind, Vector2 At)[] FodderSpots =
        {
            // Upper level.
            ('S', new Vector2(-168f, 5f)), ('B', new Vector2(-125f, 0f)), ('B', new Vector2(-114f, -9f)),
            ('Q', new Vector2(-101f, -85f)), ('Q', new Vector2(-66f, -79f)),
            ('B', new Vector2(-9f, -84f)), ('B', new Vector2(-23f, -93f)), ('B', new Vector2(-9f, -107f)), ('Q', new Vector2(65f, -75f)),
            ('B', new Vector2(108f, -75f)), ('B', new Vector2(122f, -81f)), ('B', new Vector2(89f, -107f)), ('S', new Vector2(-45f, -138f)),
            // Deep level, west and round the Mole.
            ('S', new Vector2(-115f, 148f)), ('S', new Vector2(-121f, 136f)), ('Q', new Vector2(-150f, 112f)), ('Q', new Vector2(-121f, 107f)),
            ('R', new Vector2(-121f, 89f)), ('R', new Vector2(-44f, 85f)), ('B', new Vector2(-112f, 61f)), ('B', new Vector2(-98f, 56f)), ('B', new Vector2(-112f, 48f)),
            ('Q', new Vector2(-37f, 57f)), ('Q', new Vector2(-37f, 41f)), ('B', new Vector2(-15f, 55f)), ('B', new Vector2(-2f, 63f)),
            ('S', new Vector2(-75f, 29f)), ('S', new Vector2(-61f, 31f)),
            ('Q', new Vector2(37f, 65f)), ('Q', new Vector2(46f, 54f)), ('R', new Vector2(28f, 42f)),
            ('B', new Vector2(-28f, -7f)), ('B', new Vector2(-35f, -23f)), ('B', new Vector2(-18f, -19f)), ('S', new Vector2(42f, -24f)), ('S', new Vector2(35f, -37f)),
            // Deep level, east to the Bear.
            ('S', new Vector2(76f, 64f)), ('S', new Vector2(89f, 56f)), ('Q', new Vector2(93f, 93f)), ('Q', new Vector2(65f, 33f)), ('R', new Vector2(68f, 19f)),
            ('Q', new Vector2(107f, 24f)), ('Q', new Vector2(116f, 14f)), ('Q', new Vector2(47f, -6f)), ('R', new Vector2(85f, -11f)),
            ('B', new Vector2(124f, 121f)), ('B', new Vector2(136f, 117f)),
        };

        private static Vector2 NearestOnRoute(Vector2 p, Vector2[] route)
        {
            Vector2 best = route[0];
            for (int i = 0; i + 1 < route.Length; i++)
            {
                Vector2 a = route[i], ab = route[i + 1] - a;
                Vector2 q = a + ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                if ((q - p).sqrMagnitude < (best - p).sqrMagnitude) best = q;
            }
            return best;
        }

        [MenuItem("Signal Lost/Crystal Caves/Install Route Fodder")]
        public static void InstallRouteFodderMenu()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            InstallRouteFodder();
        }

        public static void InstallRouteFodderBatch()
        {
            try
            {
                InstallRouteFodder();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Replaces only the story's Route Fodder group (the full install places it too).</summary>
        public static void InstallRouteFodder()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            T.Generate();
            Transform story = GameObject.Find(StoryRootName)?.transform;
            if (story == null) throw new InvalidOperationException("Install the Crystal Caves story first.");
            for (Transform old = story.Find(FodderGroupName); old != null; old = story.Find(FodderGroupName)) Object.DestroyImmediate(old.gameObject);
            Physics.SyncTransforms();
            PlaceRouteFodder(Group(FodderGroupName, story));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            ValidateStory();
        }

        // Each spot moves to the nearest clear ground within 12 m on the same level, or is left out.
        private static void PlaceRouteFodder(Transform parent)
        {
            Physics.SyncTransforms();
            Renderer[] scenery = Object.FindObjectsByType<Renderer>().Where(r => r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer)
                && r.name != "Ground" && r.name != "Cave Roof" && r.bounds.extents.magnitude < 40f).ToArray();
            var placed = new List<(Vector2 At, float Radius, Vector2 Mark)>();
            var counts = new Dictionary<char, int>();
            Vector2[] zones = { T.BeetleZone, T.MoleZone, T.BearZone };
            foreach ((char kind, Vector2 drawn) in FodderSpots)
            {
                // Too near a mini-boss zone: straight out from its centre to just past the gap, still in the same area.
                Vector2 mark = drawn;
                foreach (Vector2 z in zones)
                    if (Vector2.Distance(z, mark) < FodderZoneGap) mark = z + (mark - z).normalized * (FodderZoneGap + 1f);
                (string path, string label) = kind switch
                {
                    'B' => (QuartzBatBuilder.PrefabPath, "Quartz Bat"),
                    'Q' => (QuillPorcupineBuilder.PrefabPath, "Quill Porcupine"),
                    'R' => (GemRamBuilder.PrefabPath, "Gem Ram"),
                    _ => (PrismSnailBuilder.PrefabPath, "Prism Snail"),
                };
                var go = (GameObject)PrefabUtility.InstantiatePrefab(CaveEnemyKit.Load(path), parent);
                counts[kind] = counts.TryGetValue(kind, out int n) ? n + 1 : 1;
                go.name = $"{label} {counts[kind]}";
                // Footprint from the model at the origin; a bat's model flies at about 3.3 m, so its column goes higher.
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Bounds body = new Bounds(Vector3.zero, Vector3.zero);
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) body.Encapsulate(r.bounds);
                float radius = Mathf.Max(body.extents.x, body.extents.z, 0.5f) + 0.3f;
                float height = Mathf.Max(body.max.y, kind == 'B' ? 5f : 0f) + 0.5f;
                float level = T.GroundAt(mark.x, mark.y);

                bool Clear(Vector2 p)
                {
                    if (Mathf.Abs(T.GroundAt(p.x, p.y) - level) > 3f || !T.Roofed(p.x, p.y) || T.DistanceToMouth(p) < 12f) return false;
                    if (zones.Any(z => Vector2.Distance(z, p) < FodderZoneGap) || ChamberDistance(p, FodderChamberPad) < 1f) return false;
                    if (TaskSpots.Any(t => Vector2.Distance(t, p) < FodderTaskGap) || placed.Any(o => Vector2.Distance(o.At, p) < o.Radius + radius + 1f)) return false;
                    // Keep Amanda's groups apart: enemies drawn in different groups stay near 25 m apart (give or take 3 m).
                    if (placed.Any(o => Vector2.Distance(o.Mark, mark) >= FodderGroupGap && Vector2.Distance(o.At, p) < FodderGroupGap - 3f)) return false;
                    // Flat ground under the whole footprint (no wall or rock face of the terrain inside it), with headroom.
                    float h = T.GroundAt(p.x, p.y);
                    if (Slope(p) > 20f || T.CeilingAt(p.x, p.y) - h < height + 2f) return false;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector2 edge = p + new Vector2(Mathf.Cos(i * Mathf.PI / 4f), Mathf.Sin(i * Mathf.PI / 4f)) * radius;
                        if (Mathf.Abs(T.GroundAt(edge.x, edge.y) - h) > 0.8f) return false;
                    }
                    Vector3 at = new Vector3(p.x, h, p.y);
                    if (!NavMesh.SamplePosition(at, out NavMeshHit nav, 0.6f, NavMesh.AllAreas)) return false;
                    if (!NavMesh.FindClosestEdge(nav.position, out NavMeshHit edgeHit, NavMesh.AllAreas) || edgeHit.distance < radius + 0.5f) return false;
                    // Nothing solid in its space (terrain aside, checked above), and no crystal or rock model either: small
                    // decorative crystals have no collider, so their bounds count too.
                    Vector3 low = at + Vector3.up * (radius + 0.25f), high = at + Vector3.up * Mathf.Max(height - radius, radius + 0.3f);
                    if (Physics.OverlapCapsule(low, high, radius, ~0, QueryTriggerInteraction.Ignore).Any(c => !VisibleRock(c) && !c.transform.IsChildOf(go.transform))) return false;
                    var space = new Bounds(at + Vector3.up * height * 0.5f, new Vector3(radius * 2f + 0.6f, height, radius * 2f + 0.6f));
                    return !scenery.Any(r => r != null && !r.transform.IsChildOf(go.transform) && r.bounds.Intersects(space));
                }

                // Pulled toward the route as far as allowed, backing off toward the mark until a spot is clear.
                Vector2 toward = NearestOnRoute(mark, level > 10f ? UpperRoute : DeepRoute) - mark;
                float pull = Mathf.Min(toward.magnitude * 0.5f, FodderMaxPull, Mathf.Max(0f, toward.magnitude - FodderRouteKeep));
                Vector2? spot = null;
                for (float step = pull; step > 0f && spot == null; step -= 1f)
                    for (int i = 0; i < 40 && spot == null; i++)
                    {
                        float ring = Mathf.Sqrt(i) * 2f / Mathf.Sqrt(40f), angle = i * 2.39996f;
                        Vector2 p = mark + toward.normalized * step + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring;
                        if (Clear(p)) spot = p;
                    }
                for (int i = 0; i < 1200 && spot == null; i++)
                {
                    float ring = Mathf.Sqrt(i) * FodderMaxShift / Mathf.Sqrt(1200f), angle = i * 2.39996f; // golden-angle spiral outward
                    Vector2 p = mark + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring;
                    if (Clear(p)) spot = p;
                }
                if (spot == null)
                {
                    Debug.LogWarning($"Route fodder: no clear ground within {FodderMaxShift} m of {label} at ({mark.x:0}, {mark.y:0}); left out.");
                    Object.DestroyImmediate(go);
                    counts[kind]--;
                    continue;
                }
                Vector2 s = spot.Value;
                Vector2[] route = level > 10f ? UpperRoute : DeepRoute;
                Debug.Log($"Route fodder: {go.name} drawn ({drawn.x:0}, {drawn.y:0}) -> ({s.x:0}, {s.y:0}), moved {Vector2.Distance(s, drawn):0.0} m, " +
                          $"route {DistanceToRoute(drawn, route):0} -> {DistanceToRoute(s, route):0} m");
                go.transform.SetPositionAndRotation(Ground(s), Quaternion.Euler(0f, (placed.Count * 137f) % 360f, 0f));
                placed.Add((s, radius, mark));
                Physics.SyncTransforms();
            }
            Debug.Log($"ROUTE_FODDER_PLACED {placed.Count} of {FodderSpots.Length}");
        }

        // Off the tunnel, ramp, tasks, fight zones and chamber, and near enough a route that players pass close to some.
        private static bool RouteCandidate(Spot spot)
        {
            Vector2 flat = spot.Flat;
            if (!T.Roofed(flat.x, flat.y) || T.DistanceToMouth(flat) < 15f || T.RampWeight(flat.x, flat.y) > 0.01f) return false;
            if (TaskSpots.Any(t => Vector2.Distance(t, flat) < 20f)) return false;
            if (new[] { T.BeetleZone, T.MoleZone, T.BearZone }.Any(z => Vector2.Distance(z, flat) < T.ZoneRadius + 12f)) return false;
            if (ChamberDistance(flat, spot.Wall ? 60f : 20f) < 1f) return false;
            Vector3 forward = spot.Facing * Vector3.forward;
            Vector2 landing = spot.Wall ? flat + new Vector2(forward.x, forward.z).normalized * 1.5f : flat;
            if (!NavMesh.SamplePosition(Ground(landing), out _, 3f, NavMesh.AllAreas)) return false; // somewhere it can walk once out
            spot.Upper = T.GroundAt(landing.x, landing.y) > 10f;
            float toRoute = DistanceToRoute(landing, spot.Upper ? UpperRoute : DeepRoute);
            if (toRoute < 2f || toRoute > 30f) return false;
            spot.Score = Mathf.Max(0f, toRoute - 12f); // within 12 m is as good as it gets
            return true;
        }

        // A wall face within 12 m of a floor batch (so it stays among real crystals), low on the wall, facing out.
        private static Spot WallNear(CrystalBatch batch)
        {
            Vector3 p = batch.transform.position;
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30f * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                if (!Physics.Raycast(p + Vector3.up * 2.2f, dir, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(hit.normal.y) > 0.4f || !VisibleRock(hit.collider)) continue;
                float floor = T.GroundAt(hit.point.x + hit.normal.x * 1.5f, hit.point.z + hit.normal.z * 1.5f);
                if (hit.point.y - floor < 1f || hit.point.y - floor > 5f) continue;
                return new Spot { At = hit.point, Facing = Quaternion.LookRotation(new Vector3(hit.normal.x, 0f, hit.normal.z)), Colour = batch.CrystalMaterial, Wall = true };
            }
            return null;
        }

        // A couple of metres from a floor batch, on flat clear ground, facing out from it.
        private static Spot FloorSpot(CrystalBatch batch)
        {
            Vector3 p = batch.transform.position;
            for (int i = 0; i < 24; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                Vector2 flat = new Vector2(p.x, p.z) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (2.8f + (i / 8) * 0.8f);
                if (Slope(flat) > 18f || Mathf.Abs(T.GroundAt(flat.x, flat.y) - p.y) > 1.5f) continue;
                if (Physics.CheckSphere(Ground(flat) + Vector3.up * 1.3f, 0.9f, ~0, QueryTriggerInteraction.Ignore)) continue;
                Vector3 out2 = new Vector3(flat.x - p.x, 0f, flat.y - p.z);
                return new Spot { At = Ground(flat), Facing = Quaternion.LookRotation(out2), Colour = batch.CrystalMaterial };
            }
            return null;
        }

        // Beside a wall batch, on the wall itself (found with a ray), facing out of it, low enough to drop from.
        private static Spot WallSpot(CrystalBatch batch, Vector3 normal)
        {
            Vector3 p = batch.transform.position;
            Vector3 flatNormal = new Vector3(normal.x, 0f, normal.z).normalized;
            Vector3 along = Vector3.Cross(Vector3.up, flatNormal);
            foreach (float side in new[] { 1.6f, -1.6f })
            {
                Vector3 from = p + flatNormal * 2f + along * side;
                if (!Physics.Raycast(from, -flatNormal, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.normal.y) > 0.6f || !VisibleRock(hit.collider)) continue;
                float floor = T.GroundAt(hit.point.x + hit.normal.x * 1.5f, hit.point.z + hit.normal.z * 1.5f);
                if (hit.point.y - floor < 0.4f || hit.point.y - floor > 6f) continue;
                Vector3 facing = new Vector3(hit.normal.x, 0f, hit.normal.z);
                return new Spot { At = hit.point, Facing = Quaternion.LookRotation(facing), Colour = batch.CrystalMaterial, Wall = true };
            }
            return null;
        }

        // The cave's own walls (the ground mesh, with the upper level's cliff faces, and the roof), not the invisible
        // barriers, rocks or crystals.
        private static bool VisibleRock(Collider collider) => collider.name == "Ground" || collider.name == "Cave Roof";

        private static float DistanceToRoute(Vector2 p, Vector2[] route)
        {
            float best = float.MaxValue;
            for (int i = 0; i + 1 < route.Length; i++) best = Mathf.Min(best, DistanceToSegment(p, route[i], route[i + 1]));
            return best;
        }

        // Under 1 inside the chamber's ellipse grown by pad metres.
        private static float ChamberDistance(Vector2 p, float pad) =>
            new Vector2((p.x - T.FinalBossZone.x) / (T.FinalBossRadii.x + pad), (p.y - T.FinalBossZone.y) / (T.FinalBossRadii.y + pad)).magnitude;

        // =====================================================================
        // The Cave Axolotl's chamber
        // =====================================================================

        private static (AxolotlLair, QuestSignalSource) BuildChamber(Transform parent)
        {
            Vector3 home = Ground(T.FinalBossZone);
            var axolotl = (GameObject)PrefabUtility.InstantiatePrefab(CaveEnemyKit.Load(CaveAxolotlBuilder.PrefabPath), parent);
            axolotl.name = "Cave Axolotl";
            axolotl.transform.SetPositionAndRotation(home, Quaternion.Euler(0f, 180f, 0f)); // facing the way in
            var boss = new SerializedObject(axolotl.GetComponent<CaveAxolotl>());
            boss.FindProperty("arenaRadius").floatValue = AxolotlArenaRadius;
            boss.ApplyModifiedPropertiesWithoutUndo();

            // Its fodder: 6-8 hidden in the chamber's floor, out past the crystals it can hide at (Arena Radius + 6 m from
            // its home), each copying the batch nearest it. The scanner never sees them; phase 2 raises them.
            float[] angles = { 0f, 25f, -25f, 45f, 180f, 155f, 205f, 135f };
            var chameleons = new List<CrystalChameleon>();
            foreach (float a in angles)
            {
                float r = a * Mathf.Deg2Rad;
                Vector2 flat = T.FinalBossZone + new Vector2(Mathf.Cos(r) * T.FinalBossRadii.x, Mathf.Sin(r) * T.FinalBossRadii.y) * 0.78f;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(CaveEnemyKit.Load(CrystalChameleonBuilder.PrefabPath), parent);
                go.name = $"Chamber Chameleon {chameleons.Count + 1}";
                Vector2 look = T.FinalBossZone - flat;
                go.transform.SetPositionAndRotation(Ground(flat), Quaternion.LookRotation(new Vector3(look.x, 0f, look.y)));
                var data = new SerializedObject(go.GetComponent<CrystalChameleon>());
                data.FindProperty("crystalMaterial").objectReferenceValue = NearestBatchMaterial(go.transform.position);
                data.FindProperty("scannerIgnores").boolValue = true;
                data.ApplyModifiedPropertiesWithoutUndo();
                chameleons.Add(go.GetComponent<CrystalChameleon>());
            }

            var lair = new GameObject("Axolotl Lair").AddComponent<AxolotlLair>();
            lair.transform.SetParent(parent, false);
            lair.transform.position = home;
            var lairData = new SerializedObject(lair);
            lairData.FindProperty("axolotl").objectReferenceValue = axolotl.GetComponent<CaveAxolotl>();
            lairData.FindProperty("triggerRadius").floatValue = ChamberTrigger;
            lairData.FindProperty("requiredParts").intValue = 3;
            SetObjects(lairData.FindProperty("chamberChameleons"), chameleons.ToArray());
            lairData.ApplyModifiedPropertiesWithoutUndo();

            var signal = new GameObject("Deep Chamber (scanner target)");
            signal.transform.SetParent(parent, false);
            signal.transform.position = Ground(T.FinalBossZone + new Vector2(0f, -T.FinalBossRadii.y + 6f));
            var source = signal.AddComponent<QuestSignalSource>();
            source.enabled = false;
            return (lair, source);
        }

        // =====================================================================
        // Validation
        // =====================================================================

        [MenuItem("Signal Lost/Crystal Caves/Validate Crystal Caves Story")]
        public static void ValidateStory()
        {
            if (SceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
            T.Generate();
            void Require(bool ok, string message) => EnemyTestBuilder.Require(ok, message);
            Require(EnemyTestBuilder.Count<CavesStory>() == 1, "Expected one CavesStory.");
            var data = new SerializedObject(Object.FindAnyObjectByType<CavesStory>());
            foreach (string field in new[] { "cavendish", "lair", "chamberSignal" })
                Require(data.FindProperty(field).objectReferenceValue != null, "The caves story has no " + field + ".");

            // The jobs: three tasks each, a hidden boss group, a known trophy.
            SerializedProperty jobs = data.FindProperty("jobs");
            Require(jobs.arraySize == 3, "Expected three jobs.");
            Vector2[] zones = { T.BeetleZone, T.MoleZone, T.BearZone };
            int[] bossCounts = { 4, 1, 1 };
            var reach = new List<(string name, Vector3 at)>();
            for (int i = 0; i < 3; i++)
            {
                SerializedProperty job = jobs.GetArrayElementAtIndex(i);
                var group = job.FindPropertyRelative("group").objectReferenceValue as GameObject;
                Require(group != null && !group.activeSelf, $"Job {i + 1}: its boss group is missing or not hidden.");
                SerializedProperty bosses = job.FindPropertyRelative("bosses");
                Require(bosses.arraySize == bossCounts[i], $"Job {i + 1}: expected {bossCounts[i]} boss(es).");
                for (int b = 0; b < bosses.arraySize; b++)
                {
                    var boss = bosses.GetArrayElementAtIndex(b).objectReferenceValue as DesertMiniBoss;
                    Require(boss != null && boss.GetComponent<QuestSignalSource>() != null, $"Job {i + 1}: boss {b + 1} is missing or has no quest signal.");
                    Require(new SerializedObject(boss).FindProperty("fodderSpawner").objectReferenceValue == null, boss.name + " should be a pure duel (no fodder).");
                    Require(Vector2.Distance(new Vector2(boss.transform.position.x, boss.transform.position.z), zones[i]) < 8f, boss.name + " is not in its zone.");
                }
                string trophy = job.FindPropertyRelative("trophyId").stringValue;
                Require(Trophy.Find(trophy)?.LoadModel() != null, $"Job {i + 1}: trophy {trophy} is unknown or has no model.");
                SerializedProperty taskList = job.FindPropertyRelative("tasks");
                Require(taskList.arraySize == 3, $"Job {i + 1}: expected three tasks.");
                for (int t = 0; t < taskList.arraySize; t++)
                {
                    var task = taskList.GetArrayElementAtIndex(t).objectReferenceValue as QuestTask;
                    Require(task != null && task.GetComponent<QuestSignalSource>() != null, $"Job {i + 1}: task {t + 1} is missing or has no quest signal.");
                    Vector2 flat = new Vector2(task.transform.position.x, task.transform.position.z);
                    Require(Vector2.Distance(flat, zones[i]) >= 38f - 1f, $"{task.name} is closer than 38 m to its boss's zone.");
                    Require(zones.All(z => Vector2.Distance(flat, z) > T.ZoneRadius) && ChamberDistance(flat, 0f) > 1f, $"{task.name} is inside a fight zone.");
                    reach.Add((task.name, task.transform.position));
                }
                reach.Add(($"job {i + 1}'s zone", Ground(zones[i])));
            }
            SerializedProperty rack = data.FindProperty("rack");
            int rackSize = BaseCampBuilder.StandCount(BaseCampBuilder.StandSet.Caves);
            Require(rack.arraySize == rackSize, $"Expected the caves' {rackSize}-stand rack.");
            for (int i = 0; i < rackSize; i++)
                Require(((TrophyStand)rack.GetArrayElementAtIndex(i).objectReferenceValue).Index == CavesRackFirstIndex + i, $"The caves' rack stands must be {CavesRackFirstIndex}-{CavesRackFirstIndex + rackSize - 1}.");
            Require(Trophy.Find(CavesStory.AxolotlTrophyId)?.LoadModel() != null, "The Cave Axolotl's trophy is unknown or has no model.");

            // The chamber.
            Require(EnemyTestBuilder.Count<CaveAxolotl>() == 1 && EnemyTestBuilder.Count<AxolotlLair>() == 1, "Expected one Cave Axolotl and its lair.");
            var lair = new SerializedObject(Object.FindAnyObjectByType<AxolotlLair>());
            Require(lair.FindProperty("axolotl").objectReferenceValue != null && !lair.FindProperty("assumePartsCollected").boolValue, "The lair needs its axolotl and the real gate.");
            SerializedProperty chamberList = lair.FindProperty("chamberChameleons");
            Require(chamberList.arraySize >= 6 && chamberList.arraySize <= 8, "Expected 6-8 chamber chameleons.");
            Vector2 home = T.FinalBossZone;
            for (int i = 0; i < chamberList.arraySize; i++)
            {
                var chameleon = (CrystalChameleon)chamberList.GetArrayElementAtIndex(i).objectReferenceValue;
                var c = new SerializedObject(chameleon);
                Vector2 flat = new Vector2(chameleon.transform.position.x, chameleon.transform.position.z);
                Require(c.FindProperty("scannerIgnores").boolValue && c.FindProperty("questOverrideRadius").floatValue == 0f, chameleon.name + " must be ignored by the scanner.");
                Require(Vector2.Distance(flat, home) > AxolotlArenaRadius + 6f + 3f, chameleon.name + " is among the crystals the axolotl hides at.");
                Require(ChamberDistance(flat, 0f) < 1f, chameleon.name + " is outside the chamber.");
                reach.Add((chameleon.name, chameleon.transform.position));
            }
            reach.Add(("the chamber", Ground(home)));

            // The route Chameleons.
            List<CrystalChameleon> route = EnemyTestBuilder.All<CrystalChameleon>().Where(ch => new SerializedObject(ch).FindProperty("questOverrideRadius").floatValue > 0f).ToList();
            int upper = route.Count(ch => ch.name.Contains("(upper)")), deep = route.Count - upper;
            Require(upper >= 3 && upper <= 5, $"{upper} upper route chameleons; expected 3-5.");
            Require(deep >= 4 && deep <= 8, $"{deep} deep route chameleons; expected 4-8.");
            Require(route.Any(ch => new SerializedObject(ch).FindProperty("hideInWall").boolValue), "Expected some wall chameleons on the routes.");
            Require(route.Count(ch => DistanceToRoute(new Vector2(ch.transform.position.x, ch.transform.position.z), ch.name.Contains("(upper)") ? UpperRoute : DeepRoute) <= RouteOverrideRadius) >= 3,
                "Expected at least three route chameleons the player passes within 20 m of.");
            foreach (CrystalChameleon chameleon in route)
            {
                Vector2 flat = new Vector2(chameleon.transform.position.x, chameleon.transform.position.z);
                bool wall = new SerializedObject(chameleon).FindProperty("hideInWall").boolValue;
                Require(route.All(o => o == chameleon || Vector2.Distance(flat, new Vector2(o.transform.position.x, o.transform.position.z)) >= 60f), chameleon.name + " is within 60 m of another.");
                Require(TaskSpots.All(t => Vector2.Distance(t, flat) >= 18f), chameleon.name + " is near a quest task.");
                Require(zones.All(z => Vector2.Distance(z, flat) >= T.ZoneRadius + 10f), chameleon.name + " is near a fight zone.");
                Require(ChamberDistance(flat, wall ? 60f : 20f) >= 1f, chameleon.name + " is too near the Axolotl's chamber.");
                Vector3 landing = wall ? chameleon.transform.position + chameleon.transform.forward * 1.5f : chameleon.transform.position;
                reach.Add((chameleon.name, Ground(new Vector2(landing.x, landing.z))));
            }

            // The route fodder: clear of the duels and the chamber, and inside nothing.
            List<DesertFodder> fodder = EnemyTestBuilder.All<DesertFodder>().Where(f => f.transform.parent != null && f.transform.parent.name == FodderGroupName).ToList();
            Require(fodder.Count > 0, "No route fodder.");
            foreach (DesertFodder f in fodder)
            {
                Vector2 flat = new Vector2(f.transform.position.x, f.transform.position.z);
                Require(zones.All(z => Vector2.Distance(z, flat) >= FodderZoneGap - 0.5f) && ChamberDistance(flat, FodderChamberPad) >= 1f, f.name + " is near a fight zone.");
                reach.Add((f.name, f.transform.position));
            }

            // Everything above can be walked to from the camp.
            PlayerSpawnPoint spawn = Object.FindAnyObjectByType<PlayerSpawnPoint>();
            Require(Vector3.Distance(spawn.transform.position, Object.FindAnyObjectByType<IslandDock>().Landing.position) < 2f, "The first spawn should be on the dock by the boat.");
            var campRespawn = data.FindProperty("campRespawn").objectReferenceValue as Transform;
            Require(campRespawn != null && Vector2.Distance(new Vector2(campRespawn.position.x, campRespawn.position.z), T.Camp) < 5f, "The camp respawn should be by the fire.");
            Require(!Physics.CheckSphere(campRespawn.position + Vector3.up * 1f, 0.45f, ~0, QueryTriggerInteraction.Ignore), "The camp respawn is inside something.");
            reach.Add(("the camp respawn", campRespawn.position));
            Require(Mathf.Approximately(new SerializedObject(Object.FindAnyObjectByType<PlayerGear>().GetComponentInChildren<ScannerController>(true))
                .FindProperty("levelRangeOverride").floatValue, ScannerRange), "The scanner's level range should be " + ScannerRange + " m.");
            // Every level sailed up to the caves (11-22: tutorial, Mudlands, desert, caves), by save slot.
            int[] expected = new[] { BaseCampBuilder.StandSet.Tutorial }.Concat(BaseCampBuilder.SetsUpTo(BaseCampBuilder.StandSet.Caves))
                .SelectMany(set => Enumerable.Range(BaseCampBuilder.FirstStandIndex(set), BaseCampBuilder.StandCount(set))).OrderBy(i => i).ToArray();
            Require(EnemyTestBuilder.All<TrophyStand>().Select(t => t.Index).OrderBy(i => i).SequenceEqual(expected), $"Expected stands {string.Join(", ", expected)} at the caves' camp.");
            Require(NavMesh.SamplePosition(spawn.transform.position, out NavMeshHit from, 3f, NavMesh.AllAreas), "The spawn is off the NavMesh.");
            foreach ((string name, Vector3 at) in reach)
            {
                Require(NavMesh.SamplePosition(at, out NavMeshHit to, 5f, NavMesh.AllAreas), name + " is off the NavMesh.");
                var path = new NavMeshPath();
                Require(NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, "No walkable path from camp to " + name + ".");
            }
            var dock = new SerializedObject(Object.FindAnyObjectByType<IslandDock>());
            Require(dock.FindProperty("alsoDeparts").boolValue && dock.FindProperty("requiredStage").stringValue == CavesStory.HomeStage, "The dock's boat should depart once Cavendish has wrapped up the caves.");
            Debug.Log($"CRYSTAL_CAVES_STORY_VALIDATED ({upper} upper and {deep} deep route chameleons, {chamberList.arraySize} in the chamber, {fodder.Count} route fodder)");
        }
    }
}
