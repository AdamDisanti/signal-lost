using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Editor
{
    /// <summary>
    /// Generates the tutorial's stripped shipwreck and its debris (meeting notes 2026-10-20, crash site items 1-2):
    /// a low-poly white-and-orange ship lying nose-down, torn open, with empty sockets where the four stolen ship
    /// parts used to be (engine core in the tail, reactor in the open mid bay, flight-control module under a
    /// dorsal hatch, navigation computer in the cockpit), plus a kit of junk prefabs to scatter around it.
    /// All geometry and materials are original and generated here; no imported assets.
    /// Re-running overwrites the generated assets in place, keeping their GUIDs.
    /// Menu: Signal Lost > Tutorial > Build Shipwreck Assets. The tutorial scene builder places them.
    /// </summary>
    internal static class CrashSiteWreckBuilder
    {
        public const string Folder = "Assets/Prefabs/Enviornment objects/TutorialCrashSite/Shipwreck";
        public const string ShipPath = Folder + "/StrippedShipwreck.prefab";
        public static readonly string[] DebrisNames =
        {
            "HullChunk", "HullPanel", "HullPanelSmall", "Crate", "Barrel", "Pipe", "Rib", "Seat",
            "Gear", "CableBundle", "Strut", "BrokenWing", "ScorchMark"
        };

        private enum Mat { Hull, Stripe, Interior, Metal, Scorch, Yellow, Black, Fault, Cable, Crate, NavGreen, Seam, Glass }

        private static readonly Dictionary<Mat, Material> Materials = new Dictionary<Mat, Material>();

        [MenuItem("Signal Lost/Tutorial/Build Shipwreck Assets")]
        public static void Build()
        {
            EnsureFolder(Folder);
            EnsureFolder(Folder + "/Meshes");
            EnsureFolder(Folder + "/Materials");
            EnsureFolder(Folder + "/Debris");
            BuildMaterials();
            BuildShip();
            BuildDebris();
            AssetDatabase.SaveAssets();
            Debug.Log("SHIPWRECK_ASSETS_BUILT");
        }

        public static string DebrisPath(string name) => Folder + "/Debris/" + name + ".prefab";

        // ---------- Materials ----------

        private static void BuildMaterials()
        {
            Material("WreckHull", Mat.Hull, new Color(0.86f, 0.84f, 0.79f), 0.35f, 0.1f);
            Material("WreckStripe", Mat.Stripe, new Color(0.95f, 0.47f, 0.13f), 0.4f, 0.05f);
            Material("WreckInterior", Mat.Interior, new Color(0.11f, 0.11f, 0.13f), 0.2f, 0.3f);
            Material("WreckMetal", Mat.Metal, new Color(0.46f, 0.48f, 0.52f), 0.55f, 0.7f);
            Material("WreckScorch", Mat.Scorch, new Color(0.07f, 0.065f, 0.06f), 0.05f, 0f);
            Material("WreckWarningYellow", Mat.Yellow, new Color(0.98f, 0.78f, 0.12f), 0.35f, 0f);
            Material("WreckWarningBlack", Mat.Black, new Color(0.05f, 0.05f, 0.06f), 0.3f, 0f);
            Material("WreckFaultLamp", Mat.Fault, new Color(1f, 0.12f, 0.08f), 0.6f, 0f, new Color(2.2f, 0.15f, 0.08f));
            Material("WreckCable", Mat.Cable, new Color(0.16f, 0.13f, 0.12f), 0.25f, 0f);
            Material("WreckCrate", Mat.Crate, new Color(0.23f, 0.42f, 0.46f), 0.3f, 0.2f);
            Material("WreckNavLight", Mat.NavGreen, new Color(0.2f, 1f, 0.45f), 0.6f, 0f, new Color(0.3f, 2.2f, 0.7f));
            Material("WreckPanelSeam", Mat.Seam, new Color(0.3f, 0.29f, 0.27f), 0.2f, 0.2f);
            GlassMaterial();
        }

        // The canopy glass: URP Lit set to transparent, smooth and faintly blue, drawn from both sides.
        private static void GlassMaterial()
        {
            string path = Folder + "/Materials/WreckCanopyGlass.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", new Color(0.55f, 0.74f, 0.82f, 0.3f));
            material.SetFloat("_Smoothness", 0.92f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Surface", 1f); // transparent
            material.SetFloat("_Blend", 0f);   // alpha
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(material);
            Materials[Mat.Glass] = material;
        }

        private static void Material(string name, Mat key, Color color, float smoothness, float metallic, Color? emission = null)
        {
            string path = Folder + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; // with None, URP's material check drops _EMISSION on import
            }
            EditorUtility.SetDirty(material);
            Materials[key] = material;
        }

        // ---------- The ship ----------

        private struct Station
        {
            public float Z, W, H, Y;
            public Station(float z, float w, float h, float y) { Z = z; W = w; H = h; Y = y; }
        }

        // Tail (open engine bay) to nose. Width, height and centre height of the boxy fuselage at each station.
        private static readonly Station[] Stations =
        {
            new Station(-11.5f, 5.0f, 3.6f, 2.3f), new Station(-9f, 6f, 4.3f, 2.5f), new Station(-5.5f, 7f, 4.9f, 2.7f),
            new Station(-2f, 7.3f, 5.1f, 2.8f), new Station(1.5f, 7.1f, 5f, 2.8f), new Station(5f, 6.4f, 4.6f, 2.7f),
            new Station(8f, 5f, 3.9f, 2.5f), new Station(10.5f, 3.2f, 2.8f, 2.2f), new Station(12.2f, 1.4f, 1.5f, 1.9f)
        };
        private const int Sides = 12;
        private static readonly Vector3 NoseTip = new Vector3(0f, 1.8f, 13.3f);

        private static void BuildShip()
        {
            var root = new GameObject("StrippedShipwreck");
            // The hull sits nose-down and rolled onto its right (+X) side, which shows the torn-open bay to anyone
            // standing on that side. The installer turns +X toward Cavendish and the player's wake-up spot.
            var hull = new GameObject("Hull");
            hull.transform.SetParent(root.transform, false);
            hull.transform.localRotation = Quaternion.Euler(7f, 0f, -15f);

            var outer = new FlatMeshKit<Mat>();
            var inner = new FlatMeshKit<Mat>();
            for (int s = 0; s < Stations.Length - 1; s++)
            for (int j = 0; j < Sides; j++)
            {
                Vector3 a = Ring(s, j), b = Ring(s, j + 1), c = Ring(s + 1, j + 1), d = Ring(s + 1, j);
                Vector3 centre = (a + b + c + d) * 0.25f;
                Vector3 axis = new Vector3(0f, (Stations[s].Y + Stations[s + 1].Y) * 0.5f, centre.z);
                Vector3 outward = centre - axis;
                // Holes go through both skins, so from inside you see out of the torn bay and the canopy.
                if (Missing(s, j) || Canopy(s, j)) continue;
                inner.Quad(Inset(a, s), Inset(b, s), Inset(c, s + 1), Inset(d, s + 1), -outward, Mat.Interior);
                outer.Quad(a, b, c, d, outward, FaceMaterial(s, j));
            }
            // Nose cone, scorched where it ploughed into the ground.
            int last = Stations.Length - 1;
            for (int j = 0; j < Sides; j++)
                outer.Tri(Ring(last, j), Ring(last, j + 1), NoseTip, Ring(last, j) - new Vector3(0f, Stations[last].Y, Stations[last].Z) + Vector3.forward,
                    j < Sides / 2 ? Mat.Hull : Mat.Scorch);
            AddPart(hull.transform, "Fuselage", SaveMesh("WreckFuselage", outer.Build("WreckFuselage")), true);
            // Both skins collide: mesh colliders only stop the player from the front, so the inner skin is what keeps
            // anyone who climbs in through the bay from walking out through the hull.
            AddPart(hull.transform, "Fuselage Interior", SaveMesh("WreckFuselageInterior", inner.Build("WreckFuselageInterior")), true);

            BuildBay(hull.transform);
            BuildEngineBay(hull.transform);
            BuildCockpit(hull.transform);
            BuildDorsalHatch(hull.transform);
            BuildWingsAndFin(hull.transform);
            BuildDetails(hull.transform);
            BuildMarkings(hull.transform);

            // Sink it: the lowest point ends up 0.8 m below the root (ground level).
            var bounds = new Bounds(hull.transform.position, Vector3.zero);
            foreach (Renderer part in hull.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(part.bounds);
            hull.transform.localPosition = new Vector3(0f, -bounds.min.y - 0.8f, 0f);

            SavePrefab(root, ShipPath);
        }

        private static Vector3 Ring(int s, int j) => CleanRing(s, j) + Noise(s * 31 + j % Sides) * Damage(s, j);

        // Dents: deterministic jitter, heavier around the torn bay on the starboard (+x) side.
        private static float Damage(int s, int j) => s >= 2 && s <= 4 && Mathf.Cos((j % Sides) * Mathf.PI * 2f / Sides) > 0f ? 0.28f : 0.12f;

        private static Vector3 CleanRing(int s, int j)
        {
            Station st = Stations[s];
            float angle = (j % Sides) * Mathf.PI * 2f / Sides;
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            // A boxy superellipse with a flatter belly.
            float x = st.W * 0.5f * Mathf.Sign(cos) * Mathf.Pow(Mathf.Abs(cos), 0.6f);
            float y = st.H * 0.5f * Mathf.Sign(sin) * Mathf.Pow(Mathf.Abs(sin), sin < 0f ? 0.35f : 0.7f);
            return new Vector3(x, st.Y + y, st.Z);
        }

        private static Vector3 Inset(Vector3 point, int s)
        {
            Vector3 axis = new Vector3(0f, Stations[s].Y, point.z);
            return axis + (point - axis) * 0.93f;
        }

        // Faces torn away: the big bay over the reactor (top and the right shoulder), plus a few punched-out panels.
        private static bool Missing(int s, int j)
        {
            if ((s == 2 || s == 3) && j >= 0 && j <= 5) return true;
            if (s == 4 && (j == 1 || j == 2)) return true;
            return (s == 1 && j == 8) || (s == 5 && j == 10) || (s == 6 && j == 7) || (s == 0 && j == 4);
        }

        // The cockpit canopy: the roof and starboard shoulder over the pilot's seat, framed and glazed (BuildCockpit).
        private static bool Canopy(int s, int j) => (s == 5 || s == 6) && j >= 1 && j <= 4;

        // The starboard side window, smashed: the mechanic is pulled out through it in the opening.
        private const int WindowStation = 5, WindowSide = 1;

        /// <summary>The smashed cockpit window in hull space: its centre, facing out of the ship.</summary>
        public static Pose CockpitWindow()
        {
            Vector3 a = Ring(WindowStation, WindowSide), b = Ring(WindowStation, WindowSide + 1);
            Vector3 c = Ring(WindowStation + 1, WindowSide + 1), d = Ring(WindowStation + 1, WindowSide);
            Vector3 centre = (a + b + c + d) * 0.25f;
            Vector3 normal = Vector3.Cross(c - a, b - d).normalized;
            if (Vector3.Dot(normal, centre - new Vector3(0f, Stations[WindowStation].Y, centre.z)) < 0f) normal = -normal;
            return new Pose(centre, Quaternion.LookRotation(normal, Vector3.up));
        }

        private static Mat FaceMaterial(int s, int j)
        {
            if (j == 0 || j == 6) return Mat.Stripe;               // racing stripe along both flanks
            if (j >= 8 && j <= 10 && s >= 5) return Mat.Scorch;     // belly scraped along the ground
            if ((s == 1 || s == 4) && (j == 2 || j == 11)) return Mat.Scorch; // burnt edges round the bay
            if (s == 0 && j == 3) return Mat.Stripe;                 // tail band
            return Mat.Hull;
        }

        // Reactor Core socket: the open bay with exposed ribs, a scorched floor and an empty clamped cradle.
        private static void BuildBay(Transform hull)
        {
            var kit = new FlatMeshKit<Mat>();
            float floorY = 1.3f;
            kit.Box(new Vector3(0f, floorY, -2f), new Vector3(5.6f, 0.15f, 7.2f), Quaternion.identity, Mat.Scorch);
            // Ribs: arcs spanning the hole, some snapped.
            foreach (float z in new[] { -4.6f, -2.6f, -0.6f, 1.2f })
            {
                var path = new List<Vector3>();
                int s = z < -2f ? 2 : 3;
                for (int j = 0; j <= 6; j++)
                {
                    if (z > 0f && j > 4) break; // broken rib
                    Vector3 p = Ring(s, j);
                    path.Add(new Vector3(p.x * 0.97f, p.y - 0.05f, z));
                }
                kit.Add(LowPolyMesh.Tube("Rib", path.ToArray(), 0.12f, 5), Matrix4x4.identity, Mat.Metal);
            }
            // The empty reactor cradle: a hollow ring with three clamps sprung open.
            Mesh ring = LowPolyMesh.Lathe("Cradle", new[]
            {
                new Vector2(1.5f, 0f), new Vector2(1.5f, 0.7f), new Vector2(1.25f, 0.7f), new Vector2(1.25f, 0.1f), new Vector2(0.4f, 0.1f), new Vector2(0.4f, 0f)
            }, 10);
            kit.Add(ring, Matrix4x4.TRS(new Vector3(0.4f, floorY + 0.08f, -2f), Quaternion.identity, Vector3.one), Mat.Metal);
            for (int i = 0; i < 3; i++)
            {
                float angle = i * 120f;
                Quaternion clamp = Quaternion.Euler(0f, angle, 0f) * Quaternion.Euler(0f, 0f, -55f);
                Vector3 at = new Vector3(0.4f, floorY + 0.8f, -2f) + Quaternion.Euler(0f, angle, 0f) * new Vector3(1.4f, 0f, 0f);
                kit.Box(at, new Vector3(0.9f, 0.2f, 0.3f), clamp, Mat.Yellow);
            }
            WarningFrame(kit, new Vector3(0.4f, floorY + 0.09f, -2f), 3.6f, 3.6f);
            FaultLamp(kit, new Vector3(-2.1f, floorY + 0.9f, -4.8f));
            FaultLamp(kit, new Vector3(2.4f, floorY + 0.6f, 0.9f));
            Cables(kit, new Vector3(-1.2f, 4.6f, -1f), 4, 2.6f, 11);
            AddPart(hull, "Reactor Bay (empty)", SaveMesh("WreckReactorBay", kit.Build("WreckReactorBay")), true);
        }

        // Engine Core socket: the open tail with an empty mounting ring and cables hanging out.
        private static void BuildEngineBay(Transform hull)
        {
            var kit = new FlatMeshKit<Mat>();
            Mesh frame = LowPolyMesh.Lathe("EngineRing", new[]
            {
                new Vector2(1.7f, -0.25f), new Vector2(1.7f, 0.25f), new Vector2(1.35f, 0.25f), new Vector2(1.35f, -0.25f), new Vector2(1.7f, -0.25f)
            }, 12);
            Matrix4x4 ringPose = Matrix4x4.TRS(new Vector3(0f, 2.3f, -10.6f), Quaternion.Euler(90f, 0f, 0f), Vector3.one);
            kit.Add(frame, ringPose, Mat.Metal);
            for (int i = 0; i < 4; i++)
            {
                Quaternion spoke = Quaternion.Euler(0f, 0f, i * 90f + 45f);
                kit.Box(new Vector3(0f, 2.3f, -10.3f) + spoke * new Vector3(1.05f, 0f, 0f), new Vector3(0.7f, 0.18f, 0.18f), spoke, Mat.Metal);
            }
            WarningFrameVertical(kit, new Vector3(0f, 2.3f, -11.45f), 3.8f, 3.2f);
            Cables(kit, new Vector3(0f, 3.6f, -11.2f), 6, 3.2f, 21);
            FaultLamp(kit, new Vector3(1.6f, 3.5f, -11.3f));
            AddPart(hull, "Engine Bay (empty)", SaveMesh("WreckEngineBay", kit.Build("WreckEngineBay")), false);
        }

        private static readonly Vector3 ConsoleCentre = new Vector3(0f, 3.75f, 8.6f), ConsoleSize = new Vector3(2.2f, 0.7f, 0.8f);
        private static readonly Quaternion ConsoleTilt = Quaternion.Euler(-25f, 0f, 0f); // its top leans toward the pilot

        // Navigation Computer socket and the pilot's seat: a framed, glazed canopy over the roof and starboard shoulder
        // (its side window smashed), a floor, a bulkhead behind the seat, side consoles and the main console with its
        // navigation slot standing empty. The pilot's eyes in the opening are at about (0.15, 3.7, 6.7).
        private static void BuildCockpit(Transform hull)
        {
            var kit = new FlatMeshKit<Mat>();
            // Canopy frame: arcs at each station and struts between them, along the edges of the opening.
            for (int s = 5; s <= 7; s++)
            {
                var arc = new List<Vector3>();
                for (int j = 1; j <= 5; j++) arc.Add(Proud(Ring(s, j), s, 0.03f));
                kit.Add(LowPolyMesh.Tube("CanopyArc", arc.ToArray(), s == 5 ? 0.09f : 0.07f, 4), Matrix4x4.identity, s == 5 ? Mat.Stripe : Mat.Metal);
            }
            for (int j = 1; j <= 5; j++)
                kit.Add(LowPolyMesh.Tube("CanopyStrut", new[] { Proud(Ring(5, j), 5, 0.03f), Proud(Ring(6, j), 6, 0.03f), Proud(Ring(7, j), 7, 0.03f) }, 0.06f, 4),
                    Matrix4x4.identity, Mat.Metal);

            // Floor, and a bulkhead sealing the cockpit off from the reactor bay behind the seat.
            kit.Box(new Vector3(0f, 2.25f, 7.2f), new Vector3(3.4f, 0.12f, 4.6f), Quaternion.identity, Mat.Interior);
            var hub = new Vector3(0f, Stations[5].Y, Stations[5].Z + 0.03f);
            for (int j = 0; j < Sides; j++)
            {
                Vector3 a = Inset(Ring(5, j), 5) + Vector3.forward * 0.03f, b = Inset(Ring(5, j + 1), 5) + Vector3.forward * 0.03f;
                kit.Tri(hub, a, b, Vector3.forward, Mat.Interior);
                kit.Tri(hub, a, b, Vector3.back, Mat.Interior);
            }

            // The pilot's seat, slightly askew from the impact.
            Quaternion askew = Quaternion.Euler(0f, 6f, 0f);
            kit.Box(new Vector3(0.15f, 2.6f, 6.2f), new Vector3(0.6f, 0.7f, 0.6f), askew, Mat.Metal);
            kit.Box(new Vector3(0.15f, 3.02f, 6.2f), new Vector3(0.8f, 0.16f, 0.75f), askew, Mat.Black);
            kit.Box(new Vector3(0.12f, 3.6f, 5.82f), new Vector3(0.8f, 1.1f, 0.14f), askew * Quaternion.Euler(-10f, 0f, 0f), Mat.Black);
            kit.Box(new Vector3(0.11f, 4.25f, 5.72f), new Vector3(0.45f, 0.3f, 0.14f), askew * Quaternion.Euler(-10f, 0f, 0f), Mat.Stripe);

            // Side consoles with a few lit buttons, and the main console with its navigation slot standing empty.
            foreach (float side in new[] { -1f, 1f })
            {
                kit.Box(new Vector3(1.35f * side, 2.75f, 7.6f), new Vector3(0.6f, 1f, 2.2f), Quaternion.Euler(0f, 0f, -8f * side), Mat.Black);
                for (int i = 0; i < 3; i++)
                    kit.Box(new Vector3(1.3f * side, 3.28f, 6.9f + i * 0.55f), new Vector3(0.12f, 0.05f, 0.12f), Quaternion.identity, i == 1 && side > 0f ? Mat.Fault : Mat.NavGreen);
            }
            kit.Box(ConsoleCentre, ConsoleSize, ConsoleTilt, Mat.Black);
            kit.Box(ConsoleCentre + ConsoleTilt * new Vector3(0f, 0.25f, -0.05f), new Vector3(0.9f, 0.35f, 0.5f), ConsoleTilt, Mat.Interior);
            Cables(kit, new Vector3(0f, 4.15f, 8.4f), 3, 0.7f, 33);
            FaultLamp(kit, new Vector3(0.75f, 4.05f, 8.35f));
            AddPart(hull, "Cockpit (navigation slot empty)", SaveMesh("WreckCockpit", kit.Build("WreckCockpit")), true);

            // The glass, with the smashed side window open apart from a few shards left in its frame.
            var glass = new FlatMeshKit<Mat>();
            for (int s = 5; s <= 6; s++)
            for (int j = 1; j <= 4; j++)
            {
                Vector3 a = Ring(s, j), b = Ring(s, j + 1), c = Ring(s + 1, j + 1), d = Ring(s + 1, j);
                Vector3 outward = (a + b + c + d) * 0.25f - new Vector3(0f, (Stations[s].Y + Stations[s + 1].Y) * 0.5f, (a.z + c.z) * 0.5f);
                if (s != WindowStation || j != WindowSide)
                {
                    glass.Quad(a, b, c, d, outward, Mat.Glass);
                    continue;
                }
                glass.Tri(a, Vector3.Lerp(a, b, 0.4f), Vector3.Lerp(a, d, 0.28f), outward, Mat.Glass);
                glass.Tri(c, Vector3.Lerp(c, b, 0.3f), Vector3.Lerp(c, d, 0.45f), outward, Mat.Glass);
                glass.Tri(d, Vector3.Lerp(d, a, 0.22f), Vector3.Lerp(d, c, 0.18f), outward, Mat.Glass);
            }
            AddPart(hull, "Canopy Glass", SaveMesh("WreckCanopyGlass", glass.Build("WreckCanopyGlass")), true);
        }

        // A hull point pushed out from the axis by a little, so frames sit on the skin rather than in it.
        private static Vector3 Proud(Vector3 point, int s, float by)
        {
            Vector3 axis = new Vector3(0f, Stations[s].Y, point.z);
            return point + (point - axis).normalized * by;
        }

        // Flight Control Module socket: a dorsal hatch flung open over an empty cradle.
        private static void BuildDorsalHatch(Transform hull)
        {
            var kit = new FlatMeshKit<Mat>();
            Vector3 socket = new Vector3(-1.2f, 5.12f, 3.6f);
            kit.Box(socket + Vector3.down * 0.1f, new Vector3(1.6f, 0.2f, 1.4f), Quaternion.identity, Mat.Interior);
            WarningFrame(kit, socket, 1.9f, 1.7f);
            kit.Box(socket + new Vector3(-1.45f, 0.5f, 0f), new Vector3(0.12f, 1.4f, 1.5f), Quaternion.Euler(0f, 0f, 25f), Mat.Hull);
            kit.Box(socket + new Vector3(-1.4f, 0.55f, 0f), new Vector3(0.14f, 0.3f, 1.52f), Quaternion.Euler(0f, 0f, 25f), Mat.Stripe);
            Cables(kit, socket + Vector3.up * 0.05f, 3, 0.5f, 47);
            FaultLamp(kit, socket + new Vector3(0.7f, 0.12f, 0.6f));
            AddPart(hull, "Dorsal Hatch (flight controls missing)", SaveMesh("WreckDorsalHatch", kit.Build("WreckDorsalHatch")), false);
        }

        private static void BuildWingsAndFin(Transform hull)
        {
            var kit = new FlatMeshKit<Mat>();
            // Left wing: intact, now raised in the air by the roll.
            Slab(kit, new[] { new Vector3(-3.3f, 1.6f, -6.5f), new Vector3(-3.3f, 1.6f, 2f), new Vector3(-10.5f, 1.1f, -2.5f), new Vector3(-10.5f, 1.1f, -5.5f) }, 0.35f, Mat.Hull);
            Slab(kit, new[] { new Vector3(-9.4f, 1.18f, -2.9f), new Vector3(-10.5f, 1.12f, -2.5f), new Vector3(-10.5f, 1.12f, -5.5f), new Vector3(-9.4f, 1.18f, -5.6f) }, 0.4f, Mat.Stripe);
            // Right wing: snapped off near the root (the rest lies in the debris field).
            Slab(kit, new[] { new Vector3(3.3f, 1.6f, -6.5f), new Vector3(3.3f, 1.6f, 2f), new Vector3(5.4f, 1.45f, 0.3f), new Vector3(6.1f, 1.4f, -5.9f) }, 0.35f, Mat.Hull);
            kit.Box(new Vector3(5.7f, 1.4f, -2.6f), new Vector3(0.5f, 0.3f, 5.6f), Quaternion.Euler(0f, 6f, 0f), Mat.Scorch);
            // Tail fin, bent sideways.
            Slab(kit, new[] { new Vector3(0f, 4.4f, -11f), new Vector3(0f, 4.6f, -7.5f), new Vector3(0.9f, 7.4f, -10.4f), new Vector3(1.1f, 7.5f, -11.8f) }, 0.3f, Mat.Hull);
            Slab(kit, new[] { new Vector3(0.75f, 6.9f, -10.5f), new Vector3(0.9f, 7.4f, -10.4f), new Vector3(1.1f, 7.5f, -11.8f), new Vector3(0.95f, 7.0f, -11.75f) }, 0.34f, Mat.Stripe);
            // A landing leg snapped and sticking out.
            kit.Box(new Vector3(2.6f, 0.6f, 6f), new Vector3(0.3f, 1.8f, 0.3f), Quaternion.Euler(30f, 0f, 40f), Mat.Metal);
            AddPart(hull, "Wings And Fin", SaveMesh("WreckWings", kit.Build("WreckWings")), true);
        }

        // Panel seams, vents, an antenna and nav lights so the hull reads as a real, well-worn ship.
        private static void BuildDetails(Transform hull)
        {
            var kit = new FlatMeshKit<Mat>();
            foreach (int s in new[] { 1, 4, 5, 6 })
            {
                var seam = new List<Vector3>();
                for (int j = s == 4 ? 3 : 0; j <= 12; j++)
                {
                    if (s == 4 && j > 5 && j < 8) continue;
                    Vector3 p = Ring(s, j);
                    Vector3 axis = new Vector3(0f, Stations[s].Y, p.z);
                    seam.Add(axis + (p - axis) * 1.012f);
                }
                kit.Add(LowPolyMesh.Tube("Seam", seam.ToArray(), 0.035f, 4), Matrix4x4.identity, Mat.Seam);
            }
            for (int i = 0; i < 4; i++) // vent grille on the tail
                kit.Box(new Vector3(-1.2f + i * 0.32f, 4.62f, -8.2f), new Vector3(0.14f, 0.12f, 1.4f), Quaternion.Euler(0f, 0f, 8f), Mat.Black);
            kit.Box(new Vector3(-0.6f, 4.55f, -8.2f), new Vector3(1.5f, 0.06f, 1.7f), Quaternion.Euler(0f, 0f, 8f), Mat.Metal);
            // Antenna mast with a small dish, bent from the crash (on the tail roof, clear of the canopy).
            kit.Box(new Vector3(-0.9f, 5.65f, -6.3f), new Vector3(0.09f, 1.5f, 0.09f), Quaternion.Euler(0f, 0f, 18f), Mat.Metal);
            kit.Add(LowPolyMesh.Lathe("Dish", new[] { new Vector2(0.02f, 0f), new Vector2(0.35f, 0.12f), new Vector2(0.38f, 0.16f), new Vector2(0.02f, 0.06f) }, 8),
                Matrix4x4.TRS(new Vector3(-1.15f, 6.35f, -6.3f), Quaternion.Euler(30f, 0f, 60f), Vector3.one), Mat.Hull);
            kit.Box(new Vector3(-10.4f, 1.15f, -4f), new Vector3(0.18f, 0.18f, 0.3f), Quaternion.identity, Mat.NavGreen);
            kit.Box(new Vector3(-0.9f, 5.0f, 4.6f), new Vector3(0.12f, 0.12f, 0.12f), Quaternion.Euler(45f, 45f, 0f), Mat.Fault);
            AddPart(hull, "Hull Details", SaveMesh("WreckDetails", kit.Build("WreckDetails")), false);
        }

        // Who the mechanic is: his ship's name and registry, the Gleebinglorb Republic Maintenance Corps band and
        // crest on the flank Cavendish can see, and his licence plaque in the cockpit.
        private static void BuildMarkings(Transform hull)
        {
            var markings = new GameObject("Markings").transform;
            markings.SetParent(hull, false);
            // Livery, each piece on one whole, present hull face and sized to it: name and corps band on the orange stripe
            // along the port flank, the corps band again on the starboard stripe over the tail, and the crest above it.
            // (Faces that are torn away, or that the roll turns toward the ground, are avoided.)
            const string corps = "GLEEBINGLORB REPUBLIC\n<size=70%>MAINTENANCE CORPS</size>";
            Color ink = new Color(0.13f, 0.12f, 0.14f);
            Label(markings, "Ship Name", FacePose(4, 6), "GRS RUSTY WRENCH\n<size=55%>REG. MC-0451</size>", 1.4f, ink);
            Label(markings, "Corps Band Port", FacePose(2, 6), corps, 1f, ink);
            Label(markings, "Corps Band", FacePose(1, 0), corps, 1f, ink);

            // Crest: a gear with a wrench through it, on a round plaque, scaled to fit its face.
            var kit = new FlatMeshKit<Mat>();
            HullSpot crest = FacePose(1, 1);
            float scale = Mathf.Min(1f, Mathf.Min(crest.Size.x, crest.Size.y) * 0.85f / 1.5f);
            Matrix4x4 pose = Matrix4x4.TRS(crest.Position, crest.Rotation, Vector3.one * scale);
            kit.Add(LowPolyMesh.Lathe("Plaque", new[] { new Vector2(0.01f, 0f), new Vector2(0.75f, 0f), new Vector2(0.75f, 0.05f), new Vector2(0.01f, 0.05f) }, 14),
                pose * Matrix4x4.Rotate(Quaternion.Euler(-90f, 0f, 0f)), Mat.Stripe);
            kit.Add(LowPolyMesh.Lathe("Gear", new[] { new Vector2(0.18f, 0f), new Vector2(0.48f, 0f), new Vector2(0.48f, 0.08f), new Vector2(0.18f, 0.08f), new Vector2(0.18f, 0f) }, 12),
                pose * Matrix4x4.Translate(new Vector3(0f, 0f, -0.06f)) * Matrix4x4.Rotate(Quaternion.Euler(-90f, 0f, 0f)), Mat.Hull);
            for (int i = 0; i < 8; i++)
            {
                Quaternion turn = Quaternion.Euler(0f, 0f, i * 45f);
                kit.Box(pose.MultiplyPoint3x4(turn * new Vector3(0.55f, 0f, -0.1f)), new Vector3(0.14f, 0.12f, 0.1f) * scale, crest.Rotation * turn, Mat.Hull);
            }
            Quaternion diagonal = crest.Rotation * Quaternion.Euler(0f, 0f, 45f);
            kit.Box(pose.MultiplyPoint3x4(new Vector3(0f, 0f, -0.16f)), new Vector3(1.25f, 0.13f, 0.06f) * scale, diagonal, Mat.Black);
            Vector3 wrenchHead = Quaternion.Euler(0f, 0f, 45f) * new Vector3(0.62f, 0f, 0f) + new Vector3(0f, 0f, -0.16f); // in crest space
            kit.Box(pose.MultiplyPoint3x4(wrenchHead), new Vector3(0.28f, 0.3f, 0.06f) * scale, diagonal, Mat.Black);
            AddPart(markings, "Corps Crest", SaveMesh("WreckCrest", kit.Build("WreckCrest")), false);

            // The licence plaque, flat on the front of the main console, facing the pilot's seat.
            Vector3 front = ConsoleCentre + ConsoleTilt * new Vector3(0f, 0.05f, -ConsoleSize.z * 0.5f - 0.012f);
            Label(markings, "Licence Plaque", new HullSpot(front, ConsoleTilt, new Vector2(1.7f, 0.42f)),
                "LICENSED SPACE MECHANIC\n<size=70%>Gleebinglorb Republic  -  Grade C (pending)</size>", 0.14f, new Color(1f, 0.85f, 0.3f), 1f);
        }

        /// <summary>A spot for a marking in hull space: where, facing into the hull (text reads from outside), and how
        /// much room there is (along the hull, round it).</summary>
        private readonly struct HullSpot
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly Vector2 Size;
            public HullSpot(Vector3 position, Quaternion rotation, Vector2 size) { Position = position; Rotation = rotation; Size = size; }
        }

        // Hull face (s, j) as built, dents included: its centre, lifted just clear of its highest corner so the
        // marking neither cuts into the skin nor floats off it, and its size.
        private static HullSpot FacePose(int s, int j)
        {
            Vector3 a = Ring(s, j), b = Ring(s, j + 1), c = Ring(s + 1, j + 1), d = Ring(s + 1, j);
            Vector3 centre = (a + b + c + d) * 0.25f;
            Vector3 normal = Vector3.Cross(c - a, b - d).normalized;
            Vector3 axis = new Vector3(0f, (Stations[s].Y + Stations[s + 1].Y) * 0.5f, centre.z);
            if (Vector3.Dot(normal, centre - axis) < 0f) normal = -normal;
            float lift = 0f;
            foreach (Vector3 corner in new[] { a, b, c, d }) lift = Mathf.Max(lift, Vector3.Dot(corner - centre, normal));
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, normal).normalized;
            var size = new Vector2((((c + d) - (a + b)) * 0.5f).magnitude, (((b + c) - (a + d)) * 0.5f).magnitude);
            return new HullSpot(centre + normal * (lift + 0.025f), Quaternion.LookRotation(-normal, up), size);
        }

        // Text on a spot, kept inside it (with a margin) so it never runs off its panel.
        private static Transform Label(Transform parent, string name, HullSpot spot, string text, float size, Color color, float fill = 0.85f)
        {
            Vector2 box = spot.Size * fill;
            var pose = (position: spot.Position, rotation: spot.Rotation);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(parent.TransformPoint(pose.position), parent.rotation * pose.rotation);
            var label = go.AddComponent<TextMeshPro>();
            label.text = text;
            label.fontSize = size * 10f;
            label.enableAutoSizing = true; // shrink to the box so the text never runs off its panel
            label.fontSizeMin = 1f;
            label.fontSizeMax = size * 10f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = box;
            return go.transform;
        }

        // ---------- Debris kit ----------

        private static void BuildDebris()
        {
            Debris("HullChunk", kit => CurvedPlate(kit, 4.2f, 2.8f, 1.1f, 0.12f, 5, Mat.Hull, Mat.Stripe), Collision.Mesh);
            Debris("HullPanel", kit => CurvedPlate(kit, 2.2f, 1.5f, 0.35f, 0.07f, 3, Mat.Hull, Mat.Scorch), Collision.Mesh);
            Debris("HullPanelSmall", kit => CurvedPlate(kit, 1.1f, 0.8f, 0.15f, 0.05f, 2, Mat.Stripe, Mat.Hull), Collision.Mesh);
            Debris("Crate", kit =>
            {
                kit.Box(new Vector3(0f, 0.45f, 0f), new Vector3(1.2f, 0.9f, 0.9f), Quaternion.identity, Mat.Crate);
                kit.Box(new Vector3(0f, 0.45f, 0f), new Vector3(1.22f, 0.18f, 0.92f), Quaternion.identity, Mat.Stripe);
                kit.Box(new Vector3(0.1f, 0.98f, -0.25f), new Vector3(1.2f, 0.08f, 0.9f), Quaternion.Euler(-28f, 0f, 4f), Mat.Crate);
            }, Collision.Box);
            Debris("Barrel", kit =>
            {
                kit.Add(LowPolyMesh.Lathe("Barrel", new[] { new Vector2(0.01f, 0f), new Vector2(0.38f, 0f), new Vector2(0.42f, 0.5f), new Vector2(0.38f, 1.1f), new Vector2(0.01f, 1.1f) }, 9), Matrix4x4.identity, Mat.Stripe);
                kit.Add(LowPolyMesh.Lathe("Band", new[] { new Vector2(0.4f, 0.3f), new Vector2(0.44f, 0.32f), new Vector2(0.44f, 0.42f), new Vector2(0.4f, 0.44f) }, 9), Matrix4x4.identity, Mat.Black);
            }, Collision.Box);
            Debris("Pipe", kit => kit.Add(LowPolyMesh.Tube("Pipe", new[] { new Vector3(0f, 0.2f, -1.6f), new Vector3(0f, 0.2f, 0.2f), new Vector3(0.5f, 0.25f, 1f), new Vector3(1.4f, 0.3f, 1.3f) }, 0.18f, 6), Matrix4x4.identity, Mat.Metal), Collision.Mesh);
            Debris("Rib", kit =>
            {
                var path = new List<Vector3>();
                for (int i = 0; i <= 6; i++) { float a = i / 6f * Mathf.PI * 0.8f; path.Add(new Vector3(Mathf.Cos(a) * 2.2f, Mathf.Sin(a) * 1.6f + 0.15f, 0f)); }
                kit.Add(LowPolyMesh.Tube("Rib", path.ToArray(), 0.12f, 5), Matrix4x4.identity, Mat.Metal);
            }, Collision.Mesh);
            Debris("Seat", kit =>
            {
                kit.Box(new Vector3(0f, 0.25f, 0f), new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity, Mat.Metal);
                kit.Box(new Vector3(0f, 0.6f, 0f), new Vector3(0.7f, 0.18f, 0.7f), Quaternion.identity, Mat.Stripe);
                kit.Box(new Vector3(0f, 1.05f, -0.32f), new Vector3(0.7f, 0.85f, 0.15f), Quaternion.Euler(-12f, 0f, 0f), Mat.Stripe);
            }, Collision.Box);
            Debris("Gear", kit =>
            {
                kit.Add(LowPolyMesh.Lathe("Gear", new[] { new Vector2(0.15f, 0f), new Vector2(0.55f, 0f), new Vector2(0.55f, 0.16f), new Vector2(0.15f, 0.16f), new Vector2(0.15f, 0f) }, 12), Matrix4x4.identity, Mat.Metal);
                for (int i = 0; i < 10; i++)
                {
                    Quaternion turn = Quaternion.Euler(0f, i * 36f, 0f);
                    kit.Box(turn * new Vector3(0.62f, 0.08f, 0f), new Vector3(0.16f, 0.16f, 0.14f), turn, Mat.Metal);
                }
            }, Collision.Mesh);
            Debris("CableBundle", kit => Cables(kit, new Vector3(0f, 0.05f, 0f), 4, 0f, 63, horizontal: true), Collision.None);
            Debris("Strut", kit =>
            {
                kit.Box(new Vector3(0f, 0.15f, 0f), new Vector3(0.22f, 0.22f, 2.4f), Quaternion.identity, Mat.Metal);
                kit.Box(new Vector3(0.35f, 0.25f, 1.6f), new Vector3(0.22f, 0.22f, 1.1f), Quaternion.Euler(-10f, 35f, 0f), Mat.Metal);
            }, Collision.Mesh);
            Debris("BrokenWing", kit =>
            {
                Slab(kit, new[] { new Vector3(0f, 0.2f, -3f), new Vector3(0.3f, 0.25f, 3f), new Vector3(5.5f, 0.15f, 0.8f), new Vector3(5.5f, 0.15f, -2.2f) }, 0.32f, Mat.Hull);
                Slab(kit, new[] { new Vector3(4.4f, 0.18f, 1.2f), new Vector3(5.5f, 0.15f, 0.8f), new Vector3(5.5f, 0.15f, -2.2f), new Vector3(4.4f, 0.18f, -2.4f) }, 0.36f, Mat.Stripe);
                kit.Box(new Vector3(0.1f, 0.22f, 0f), new Vector3(0.4f, 0.4f, 5.8f), Quaternion.identity, Mat.Scorch);
                Cables(kit, new Vector3(0.05f, 0.3f, 0.5f), 3, 0f, 71, horizontal: true);
            }, Collision.Mesh);
            Debris("ScorchMark", kit =>
            {
                const int points = 9;
                var rim = new Vector3[points];
                for (int i = 0; i < points; i++)
                {
                    float a = i * Mathf.PI * 2f / points, r = 1f + Noise(i * 7 + 3).x * 0.6f;
                    rim[i] = new Vector3(Mathf.Cos(a) * r * 1.8f, 0.03f, Mathf.Sin(a) * r);
                }
                for (int i = 0; i < points; i++) kit.Tri(new Vector3(0f, 0.03f, 0f), rim[i], rim[(i + 1) % points], Vector3.up, Mat.Scorch);
            }, Collision.None);
        }

        private enum Collision { None, Box, Mesh }

        private static void Debris(string name, Action<FlatMeshKit<Mat>> build, Collision collision)
        {
            var kit = new FlatMeshKit<Mat>();
            build(kit);
            Mesh mesh = SaveMesh("Debris" + name, kit.Build("Debris" + name));
            var root = new GameObject(name);
            AddPart(root.transform, "Visual", mesh, false);
            if (collision == Collision.Box)
            {
                var box = root.AddComponent<BoxCollider>();
                box.center = mesh.bounds.center;
                box.size = mesh.bounds.size;
            }
            else if (collision == Collision.Mesh)
            {
                var hull = root.AddComponent<MeshCollider>();
                hull.sharedMesh = mesh;
                hull.convex = true;
            }
            root.isStatic = true;
            SavePrefab(root, DebrisPath(name));
        }

        // ---------- Shape helpers ----------

        // A bent plate with thickness, made of a grid curved around Z; the leading row uses the accent material.
        private static void CurvedPlate(FlatMeshKit<Mat> kit, float width, float length, float bend, float thickness, int columns, Mat main, Mat accent)
        {
            int rows = 2;
            var top = new Vector3[columns + 1, rows + 1];
            for (int c = 0; c <= columns; c++)
            for (int r = 0; r <= rows; r++)
            {
                float u = c / (float)columns - 0.5f, v = r / (float)rows - 0.5f;
                Vector3 jitter = Noise(c * 13 + r * 5 + columns) * 0.06f;
                top[c, r] = new Vector3(u * width, bend * (1f - 4f * u * u) * 0.5f + 0.1f, v * length) + jitter;
            }
            for (int c = 0; c < columns; c++)
            for (int r = 0; r < rows; r++)
            {
                Vector3 a = top[c, r], b = top[c + 1, r], cc = top[c + 1, r + 1], d = top[c, r + 1];
                Mat mat = r == rows - 1 && c % 2 == 0 ? accent : main;
                Vector3 up = Vector3.Cross(d - a, b - a).normalized;
                if (up.y < 0f) up = -up;
                kit.Quad(a, b, cc, d, up, mat);
                kit.Quad(a - up * thickness, b - up * thickness, cc - up * thickness, d - up * thickness, -up, Mat.Interior);
            }
            // Rim around the edge so the plate looks solid from the side.
            for (int c = 0; c < columns; c++)
            {
                Rim(kit, top[c, 0], top[c + 1, 0], thickness, Vector3.back);
                Rim(kit, top[c, rows], top[c + 1, rows], thickness, Vector3.forward);
            }
            for (int r = 0; r < rows; r++)
            {
                Rim(kit, top[0, r], top[0, r + 1], thickness, Vector3.left);
                Rim(kit, top[columns, r], top[columns, r + 1], thickness, Vector3.right);
            }
        }

        private static void Rim(FlatMeshKit<Mat> kit, Vector3 a, Vector3 b, float thickness, Vector3 outward) =>
            kit.Quad(a, b, b + Vector3.down * thickness, a + Vector3.down * thickness, outward, Mat.Metal);

        // A flat four-cornered slab (wings, fins) with the given thickness.
        private static void Slab(FlatMeshKit<Mat> kit, Vector3[] corners, float thickness, Mat material)
        {
            Vector3 half = Vector3.up * thickness * 0.5f;
            Vector3 centre = (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;
            Vector3 normal = Vector3.Cross(corners[1] - corners[0], corners[3] - corners[0]).normalized;
            if (Vector3.Dot(normal, Vector3.up) < 0f) normal = -normal;
            if (Mathf.Abs(normal.y) < 0.3f) half = normal * thickness * 0.5f; // vertical fins
            kit.Quad(corners[0] + half, corners[1] + half, corners[2] + half, corners[3] + half, half, material);
            kit.Quad(corners[0] - half, corners[1] - half, corners[2] - half, corners[3] - half, -half, material);
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = corners[i], b = corners[(i + 1) % 4];
                Vector3 edgeOut = ((a + b) * 0.5f - centre).normalized;
                kit.Quad(a + half, b + half, b - half, a - half, edgeOut, material);
            }
        }

        // Yellow-and-black hazard frame lying flat around a socket.
        private static void WarningFrame(FlatMeshKit<Mat> kit, Vector3 centre, float width, float depth)
        {
            int index = 0;
            for (float x = -width * 0.5f; x < width * 0.5f - 0.01f; x += 0.3f, index++)
            {
                kit.Box(centre + new Vector3(x + 0.15f, 0.04f, depth * 0.5f), new Vector3(0.3f, 0.08f, 0.16f), Quaternion.identity, index % 2 == 0 ? Mat.Yellow : Mat.Black);
                kit.Box(centre + new Vector3(x + 0.15f, 0.04f, -depth * 0.5f), new Vector3(0.3f, 0.08f, 0.16f), Quaternion.identity, index % 2 == 1 ? Mat.Yellow : Mat.Black);
            }
            for (float z = -depth * 0.5f; z < depth * 0.5f - 0.01f; z += 0.3f, index++)
            {
                kit.Box(centre + new Vector3(width * 0.5f, 0.04f, z + 0.15f), new Vector3(0.16f, 0.08f, 0.3f), Quaternion.identity, index % 2 == 0 ? Mat.Yellow : Mat.Black);
                kit.Box(centre + new Vector3(-width * 0.5f, 0.04f, z + 0.15f), new Vector3(0.16f, 0.08f, 0.3f), Quaternion.identity, index % 2 == 1 ? Mat.Yellow : Mat.Black);
            }
        }

        // The same frame standing up, around the open tail.
        private static void WarningFrameVertical(FlatMeshKit<Mat> kit, Vector3 centre, float width, float height)
        {
            int index = 0;
            for (float x = -width * 0.5f; x < width * 0.5f - 0.01f; x += 0.35f, index++)
                kit.Box(centre + new Vector3(x + 0.175f, height * 0.5f, 0f), new Vector3(0.35f, 0.16f, 0.1f), Quaternion.identity, index % 2 == 0 ? Mat.Yellow : Mat.Black);
            for (float y = -height * 0.5f; y < height * 0.5f - 0.01f; y += 0.35f, index++)
            {
                kit.Box(centre + new Vector3(width * 0.5f, y + 0.175f, 0f), new Vector3(0.16f, 0.35f, 0.1f), Quaternion.identity, index % 2 == 0 ? Mat.Yellow : Mat.Black);
                kit.Box(centre + new Vector3(-width * 0.5f, y + 0.175f, 0f), new Vector3(0.16f, 0.35f, 0.1f), Quaternion.identity, index % 2 == 1 ? Mat.Yellow : Mat.Black);
            }
        }

        // Torn cables hanging down from a point (or lying along the ground when horizontal).
        private static void Cables(FlatMeshKit<Mat> kit, Vector3 from, int count, float drop, int seed, bool horizontal = false)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 n = Noise(seed + i * 11);
                var path = new Vector3[5];
                for (int k = 0; k < path.Length; k++)
                {
                    float t = k / (float)(path.Length - 1);
                    Vector3 sway = new Vector3(Mathf.Sin(t * 3f + i) * 0.25f, 0f, Mathf.Cos(t * 2f + i * 1.7f) * 0.25f);
                    path[k] = horizontal
                        ? from + new Vector3((t - 0.5f) * 2.4f + n.x * 0.3f, 0.06f + i * 0.02f, n.z * 0.6f + sway.z)
                        : from + new Vector3(n.x * 0.5f, -drop * t * (0.6f + 0.4f * Mathf.Abs(n.y)), n.z * 0.5f) + sway * t;
                }
                kit.Add(LowPolyMesh.Tube("Cable", path, 0.045f + 0.02f * Mathf.Abs(n.y), 4), Matrix4x4.identity, i % 3 == 2 ? Mat.Yellow : Mat.Cable);
            }
        }

        private static void FaultLamp(FlatMeshKit<Mat> kit, Vector3 at) =>
            kit.Box(at, new Vector3(0.16f, 0.16f, 0.16f), Quaternion.Euler(45f, 45f, 0f), Mat.Fault);

        // Deterministic pseudo-random vector in [-1, 1] per component, so rebuilding gives the same wreck.
        private static Vector3 Noise(int seed)
        {
            float Hash(int n)
            {
                n = (n << 13) ^ n;
                return 1f - ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824f;
            }
            return new Vector3(Hash(seed * 3 + 1), Hash(seed * 3 + 2), Hash(seed * 3 + 3));
        }

        // ---------- Assets ----------

        private static void AddPart(Transform parent, string name, Mesh mesh, bool collide)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            var shared = new Material[mesh.subMeshCount];
            for (int i = 0; i < shared.Length; i++) shared[i] = Materials[FlatMeshKit<Mat>.MaterialOf(mesh, i)];
            renderer.sharedMaterials = shared;
            if (collide) part.AddComponent<MeshCollider>().sharedMesh = mesh;
            part.isStatic = true;
        }

        // Overwrites an existing mesh asset in place (same GUID) or creates it.
        private static Mesh SaveMesh(string name, Mesh mesh)
        {
            string path = Folder + "/Meshes/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            string materialOrder = FlatMeshKit<Mat>.Order(mesh);
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = name;
            FlatMeshKit<Mat>.Remember(existing, materialOrder);
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            return existing;
        }

        private static void SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
