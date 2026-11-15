using System;
using SignalLost.Enemies.Meadow;
using SignalLost.Progression;
using SignalLost.Scanner;
using UnityEditor;
using UnityEngine;

namespace SignalLost.Editor
{
    /// <summary>
    /// Builds the base camp as one prefab (meeting notes 10-23: the same camp appears in every level): Cavendish's
    /// fire pit and campfire, the tent, two log seats, his supplies, and a row of three trophy stands by the tent where
    /// beaten bosses' trophies go (<see cref="TrophyStand"/>, remembered by <see cref="CampSave"/>). Also builds the
    /// trophy models into Resources (the Chief Toad's crown, copied from his prefab). The camp's origin is the fire,
    /// +Z north. Menu: Signal Lost > Camp > Build Base Camp.
    /// </summary>
    internal static class BaseCampBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Core/BaseCamp.prefab";
        private const string TrophyFolder = "Assets/Resources/SignalLost/Trophies";
        private const string ChiefToadPath = "Assets/Prefabs/Enemies/Meadow/ChiefToad.prefab";
        private const string MeadowMaterials = MeadowAssetBuilder.Folder + "/Materials/";

        /// <summary>Where the trophy stands are, relative to the fire (x east, y north).</summary>
        public static readonly Vector2[] StandSpots = { new Vector2(1.2f, 5.6f), new Vector2(2.9f, 5.1f), new Vector2(4.4f, 4.1f) };

        private enum Mat { Wood, Dark, Brass }

        [MenuItem("Signal Lost/Camp/Build Base Camp")]
        public static void Build()
        {
            BuildTrophies();
            var root = new GameObject("Campsite");
            Place(MeadowAssetBuilder.PrefabPath("FirePit"), root.transform, Vector3.zero, 0f, 1f).name = "Fire Pit";
            GameObject fire = Place(MeadowAssetBuilder.EffectPath("Campfire"), root.transform, Vector3.up * 0.05f, 0f, 1f);
            fire.name = "Campfire";
            Place(MeadowAssetBuilder.PrefabPath("Tent"), root.transform, new Vector3(-4f, 0f, 6f), 160f, 1f);
            Place(MeadowAssetBuilder.PrefabPath("LogSeat"), root.transform, new Vector3(3f, 0f, 0.6f), 90f, 1f).name = "Cavendish's Log";
            Place(MeadowAssetBuilder.PrefabPath("LogSeat"), root.transform, new Vector3(0.3f, 0f, -3.4f), 10f, 1f);
            Place(CrashSiteWreckBuilder.DebrisPath("Crate"), root.transform, new Vector3(-2.5f, 0f, 4.6f), 25f, 0.8f).name = "Cavendish's Supplies";
            Place(CrashSiteWreckBuilder.DebrisPath("Barrel"), root.transform, new Vector3(-1.2f, 0f, 5.2f), 0f, 0.8f);

            Mesh stand = StandMesh(out Material[] materials);
            var shelf = new GameObject("Trophy Stands").transform;
            shelf.SetParent(root.transform, false);
            for (int i = 0; i < StandSpots.Length; i++)
            {
                var spot = new GameObject("Trophy Stand " + (i + 1));
                spot.transform.SetParent(shelf, false);
                spot.transform.localPosition = new Vector3(StandSpots[i].x, 0f, StandSpots[i].y);
                spot.transform.localRotation = Quaternion.LookRotation(-new Vector3(StandSpots[i].x, 0f, StandSpots[i].y)); // facing the fire
                spot.AddComponent<MeshFilter>().sharedMesh = stand;
                spot.AddComponent<MeshRenderer>().sharedMaterials = materials;
                var box = spot.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.55f, 0f);
                box.size = new Vector3(0.8f, 1.1f, 0.8f);
                var mount = new GameObject("Mount").transform;
                mount.SetParent(spot.transform, false);
                mount.localPosition = new Vector3(0f, 1.02f, 0f);
                var trophyStand = new SerializedObject(spot.AddComponent<TrophyStand>());
                trophyStand.FindProperty("index").intValue = i;
                trophyStand.FindProperty("mount").objectReferenceValue = mount;
                trophyStand.ApplyModifiedPropertiesWithoutUndo();
                spot.AddComponent<QuestSignalSource>().enabled = false;
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("BASE_CAMP_BUILT");
        }

        // ---------- The desert camp (11-08) ----------

        public const string DesertPrefabPath = "Assets/Prefabs/Core/DesertCamp.prefab";
        private const string DesertFolder = "Assets/Prefabs/Core/DesertCamp";
        public const string RackName = "Chop Shop Rack";
        /// <summary>The desert camp's extra rack for the Chop Shop's trophies, relative to the fire (x east, y north).</summary>
        public static readonly Vector2[] RackSpots = { new Vector2(6.3f, -2.1f), new Vector2(7.0f, -0.4f), new Vector2(7.2f, 1.4f) };

        private enum DesertPart { Driftwood, DriftwoodDark, Canvas, Indigo, Terracotta, Clay, Rope }

        /// <summary>
        /// The base camp as it is pitched on the Desert Dunes (11-08): the same fire, tent, two logs, supplies and three
        /// trophy stands in the same places, restyled for the desert. Sandstone ring round the fire, sun-bleached
        /// driftwood logs and stands, a terracotta and indigo tent. It adds a canvas sun shade over Cavendish's log, a
        /// woven rug and clay water jars. Carries an <see cref="SignalLost.World.IslandCamp"/>, which packs it away when
        /// the crew arrives by boat and puts it up again piece by piece; the scene installer links Cavendish and Eugene.
        /// Menu: Signal Lost > Camp > Build Desert Camp.
        /// </summary>
        [MenuItem("Signal Lost/Camp/Build Desert Camp")]
        public static void BuildDesert()
        {
            EnsureFolder(DesertFolder + "/Materials");
            var remap = new System.Collections.Generic.Dictionary<string, Material>
            {
                ["MeadowStone"] = DesertMaterial("DesertCampSandstone", new Color(0.76f, 0.52f, 0.36f)),
                ["MeadowLog"] = DesertMaterial("DesertCampDriftwood", new Color(0.8f, 0.72f, 0.6f)),
                ["MeadowBark"] = DesertMaterial("DesertCampDriftwoodDark", new Color(0.56f, 0.46f, 0.36f)),
                ["MeadowTentOrange"] = DesertMaterial("DesertCampTerracotta", new Color(0.7f, 0.37f, 0.23f)),
                ["MeadowCanvas"] = DesertMaterial("DesertCampCanvas", new Color(0.91f, 0.83f, 0.67f)),
                ["MeadowTentTeal"] = DesertMaterial("DesertCampIndigo", new Color(0.24f, 0.29f, 0.5f)),
            };

            var root = new GameObject("Desert Camp");
            Transform Piece(string path, Vector3 at, float yaw, float scale, string name = null)
            {
                GameObject piece = Place(path, root.transform, at, yaw, scale);
                if (name != null) piece.name = name;
                Restyle(piece, remap);
                return piece.transform;
            }
            Transform pit = Piece(MeadowAssetBuilder.PrefabPath("FirePit"), Vector3.zero, 0f, 1f, "Fire Pit");
            Transform fire = Piece(MeadowAssetBuilder.EffectPath("Campfire"), Vector3.up * 0.05f, 0f, 1f, "Campfire");
            Transform tent = Piece(MeadowAssetBuilder.PrefabPath("Tent"), new Vector3(-4f, 0f, 6f), 160f, 1f);
            Transform cavendishLog = Piece(MeadowAssetBuilder.PrefabPath("LogSeat"), new Vector3(3f, 0f, 0.6f), 90f, 1f, "Cavendish's Log");
            Transform otherLog = Piece(MeadowAssetBuilder.PrefabPath("LogSeat"), new Vector3(0.3f, 0f, -3.4f), 10f, 1f);
            Transform supplies = Piece(CrashSiteWreckBuilder.DebrisPath("Crate"), new Vector3(-2.5f, 0f, 4.6f), 25f, 0.8f, "Cavendish's Supplies");
            Transform barrel = Piece(CrashSiteWreckBuilder.DebrisPath("Barrel"), new Vector3(-1.2f, 0f, 5.2f), 0f, 0.8f);

            Transform shade = DesertProp(root.transform, "Sun Shade", ShadeKit(), new Vector3(3.1f, 0f, 0.7f));
            Transform rug = DesertProp(root.transform, "Rug", RugKit(), new Vector3(-1.7f, 0f, 2.5f));
            rug.localRotation = Quaternion.Euler(0f, 18f, 0f);
            Transform jars = DesertProp(root.transform, "Water Jars", JarKit(), new Vector3(-0.2f, 0f, 4.3f));
            var jarBox = jars.gameObject.AddComponent<BoxCollider>();
            jarBox.center = new Vector3(0.2f, 0.35f, 0f);
            jarBox.size = new Vector3(1.1f, 0.7f, 0.6f);

            Mesh stand = StandMesh(out Material[] standMaterials);
            for (int i = 0; i < standMaterials.Length; i++)
                if (standMaterials[i] != null && remap.TryGetValue(standMaterials[i].name, out Material desert)) standMaterials[i] = desert;
            var shelf = new GameObject("Trophy Stands").transform;
            shelf.SetParent(root.transform, false);
            for (int i = 0; i < StandSpots.Length; i++)
            {
                var spot = new GameObject("Trophy Stand " + (i + 1));
                spot.transform.SetParent(shelf, false);
                spot.transform.localPosition = new Vector3(StandSpots[i].x, 0f, StandSpots[i].y);
                spot.transform.localRotation = Quaternion.LookRotation(-new Vector3(StandSpots[i].x, 0f, StandSpots[i].y));
                spot.AddComponent<MeshFilter>().sharedMesh = stand;
                spot.AddComponent<MeshRenderer>().sharedMaterials = standMaterials;
                var box = spot.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.55f, 0f);
                box.size = new Vector3(0.8f, 1.1f, 0.8f);
                var mount = new GameObject("Mount").transform;
                mount.SetParent(spot.transform, false);
                mount.localPosition = new Vector3(0f, 1.02f, 0f);
                var trophyStand = new SerializedObject(spot.AddComponent<TrophyStand>());
                trophyStand.FindProperty("index").intValue = i;
                trophyStand.FindProperty("mount").objectReferenceValue = mount;
                trophyStand.ApplyModifiedPropertiesWithoutUndo();
                spot.AddComponent<QuestSignalSource>().enabled = false;
            }

            // The Chop Shop rack (11-08): three more stands east of the fire for the desert mini-bosses' trophies, under a
            // driftwood sign. They are stands 4-6 (the camp save remembers them like the first three).
            var rack = new GameObject(RackName).transform;
            rack.SetParent(root.transform, false);
            for (int i = 0; i < RackSpots.Length; i++)
            {
                var spot = new GameObject("Rack Stand " + (i + 1));
                spot.transform.SetParent(rack, false);
                spot.transform.localPosition = new Vector3(RackSpots[i].x, 0f, RackSpots[i].y);
                spot.transform.localRotation = Quaternion.LookRotation(-new Vector3(RackSpots[i].x, 0f, RackSpots[i].y));
                spot.AddComponent<MeshFilter>().sharedMesh = stand;
                spot.AddComponent<MeshRenderer>().sharedMaterials = standMaterials;
                var box = spot.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.55f, 0f);
                box.size = new Vector3(0.8f, 1.1f, 0.8f);
                var mount = new GameObject("Mount").transform;
                mount.SetParent(spot.transform, false);
                mount.localPosition = new Vector3(0f, 1.02f, 0f);
                var trophyStand = new SerializedObject(spot.AddComponent<TrophyStand>());
                trophyStand.FindProperty("index").intValue = StandSpots.Length + i;
                trophyStand.FindProperty("mount").objectReferenceValue = mount;
                trophyStand.ApplyModifiedPropertiesWithoutUndo();
                spot.AddComponent<QuestSignalSource>().enabled = false;
            }
            Transform sign = DesertProp(rack, "Rack Sign", RackSignKit(), new Vector3(8.2f, 0f, -0.3f));
            sign.localRotation = Quaternion.LookRotation(new Vector3(-8.2f, 0f, 0.3f));

            // Where Cavendish and Eugene sit (the log tops, facing the fire) and stand while pitching camp.
            Transform Mark(string name, Vector3 at)
            {
                var mark = new GameObject(name).transform;
                mark.SetParent(root.transform, false);
                mark.localPosition = at;
                Vector3 toFire = -at;
                toFire.y = 0f;
                mark.localRotation = toFire.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toFire) : Quaternion.identity;
                return mark;
            }
            Transform cavendishSeat = Mark("Cavendish Seat", root.transform.InverseTransformPoint(LogTop(cavendishLog)));
            Transform merchantSeat = Mark("Merchant Seat", root.transform.InverseTransformPoint(LogTop(otherLog)));
            Transform cavendishStand = Mark("Cavendish Stand", new Vector3(1.9f, 0f, -1.3f));
            Transform merchantStand = Mark("Merchant Stand", new Vector3(2.6f, 0f, -2.6f));

            var camp = new SerializedObject(root.AddComponent<SignalLost.World.IslandCamp>());
            camp.FindProperty("cavendishSeat").objectReferenceValue = cavendishSeat;
            camp.FindProperty("merchantSeat").objectReferenceValue = merchantSeat;
            camp.FindProperty("cavendishStand").objectReferenceValue = cavendishStand;
            camp.FindProperty("merchantStand").objectReferenceValue = merchantStand;
            Transform[] order = { pit, rug, cavendishLog, otherLog, tent, shade, supplies, barrel, jars, shelf, rack, fire };
            SerializedProperty list = camp.FindProperty("pieces");
            list.arraySize = order.Length;
            for (int i = 0; i < order.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = order[i];
            camp.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, DesertPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("DESERT_CAMP_BUILT");
        }

        // The middle of a log's top surface.
        private static Vector3 LogTop(Transform log)
        {
            var bounds = new Bounds(log.position, Vector3.zero);
            foreach (Renderer part in log.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(part.bounds);
            return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        }

        private static void Restyle(GameObject piece, System.Collections.Generic.Dictionary<string, Material> remap)
        {
            foreach (Renderer renderer in piece.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer) continue;
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] != null && remap.TryGetValue(materials[i].name, out Material desert)) { materials[i] = desert; changed = true; }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        // A canvas lean-to shading Cavendish's log: four driftwood poles, the cloth sloping down to the east with two
        // indigo bands, and guy ropes to stakes in the sand. Centred on the log.
        private static FlatMeshKit<DesertPart> ShadeKit()
        {
            var kit = new FlatMeshKit<DesertPart>();
            float west = -1.1f, east = 1.1f, south = -1.6f, north = 1.6f, high = 2.9f, low = 2.4f;
            foreach (float z in new[] { south, north })
            {
                kit.Add(LowPolyMesh.TaperedPrism("pole", 6, 0.85f), Matrix4x4.TRS(new Vector3(west, 0f, z), Quaternion.Euler(-90f, 0f, 0f), new Vector3(0.06f, 0.06f, high)), DesertPart.Driftwood);
                kit.Add(LowPolyMesh.TaperedPrism("pole", 6, 0.85f), Matrix4x4.TRS(new Vector3(east, 0f, z), Quaternion.Euler(-90f, 0f, 0f), new Vector3(0.06f, 0.06f, low)), DesertPart.Driftwood);
                kit.Add(LowPolyMesh.Tube("guy", new[] { new Vector3(east, low - 0.05f, z), new Vector3(east + 1.2f, 0.02f, z + Mathf.Sign(z) * 0.4f) }, 0.012f, 4), Matrix4x4.identity, DesertPart.Rope);
                kit.Box(new Vector3(east + 1.2f, 0.06f, z + Mathf.Sign(z) * 0.4f), new Vector3(0.05f, 0.14f, 0.05f), Quaternion.identity, DesertPart.DriftwoodDark);
            }
            float slope = Mathf.Atan2(high - low, east - west) * Mathf.Rad2Deg;
            Quaternion tilt = Quaternion.Euler(0f, 0f, -slope);
            Vector3 middle = new Vector3(0f, (high + low) * 0.5f + 0.03f, 0f);
            kit.Box(middle, new Vector3(east - west + 0.5f, 0.03f, north - south + 0.4f), tilt, DesertPart.Canvas);
            foreach (float z in new[] { -0.75f, 0.75f })
                kit.Box(middle + tilt * new Vector3(0f, 0.02f, z), new Vector3(east - west + 0.52f, 0.02f, 0.3f), tilt, DesertPart.Indigo);
            return kit;
        }

        // A woven rug on the sand: terracotta with a cream border and indigo bands.
        private static FlatMeshKit<DesertPart> RugKit()
        {
            var kit = new FlatMeshKit<DesertPart>();
            kit.Box(new Vector3(0f, 0.01f, 0f), new Vector3(2.4f, 0.02f, 1.5f), Quaternion.identity, DesertPart.Canvas);
            kit.Box(new Vector3(0f, 0.022f, 0f), new Vector3(2.2f, 0.02f, 1.3f), Quaternion.identity, DesertPart.Terracotta);
            foreach (float x in new[] { -0.6f, 0f, 0.6f })
                kit.Box(new Vector3(x, 0.034f, 0f), new Vector3(0.14f, 0.02f, 1.3f), Quaternion.identity, DesertPart.Indigo);
            return kit;
        }

        // A driftwood sign behind the rack: two posts and a plank with indigo and terracotta stripes (the camp's colours).
        private static FlatMeshKit<DesertPart> RackSignKit()
        {
            var kit = new FlatMeshKit<DesertPart>();
            foreach (float x in new[] { -1.4f, 1.4f })
                kit.Add(LowPolyMesh.TaperedPrism("post", 6, 0.85f), Matrix4x4.TRS(new Vector3(x, 0f, 0f), Quaternion.Euler(-90f, 0f, 0f), new Vector3(0.07f, 0.07f, 1.9f)), DesertPart.DriftwoodDark);
            kit.Box(new Vector3(0f, 1.55f, 0f), new Vector3(3.2f, 0.5f, 0.07f), Quaternion.Euler(0f, 0f, 2f), DesertPart.Driftwood);
            kit.Box(new Vector3(0f, 1.66f, 0.04f), new Vector3(2.9f, 0.09f, 0.02f), Quaternion.Euler(0f, 0f, 2f), DesertPart.Indigo);
            kit.Box(new Vector3(0f, 1.44f, 0.04f), new Vector3(2.9f, 0.09f, 0.02f), Quaternion.Euler(0f, 0f, 2f), DesertPart.Terracotta);
            return kit;
        }

        // Two clay water jars and a smaller one, by the supplies.
        private static FlatMeshKit<DesertPart> JarKit()
        {
            var kit = new FlatMeshKit<DesertPart>();
            Vector2[] jar = { new Vector2(0f, 0f), new Vector2(0.16f, 0f), new Vector2(0.27f, 0.22f), new Vector2(0.25f, 0.42f), new Vector2(0.12f, 0.56f), new Vector2(0.13f, 0.64f), new Vector2(0f, 0.64f) };
            // (The kit takes each mesh it is given, so every jar gets its own.)
            kit.Add(LowPolyMesh.Lathe("jar", jar, 9), Matrix4x4.TRS(new Vector3(-0.15f, 0f, 0f), Quaternion.identity, Vector3.one), DesertPart.Clay);
            kit.Add(LowPolyMesh.Lathe("jar", jar, 9), Matrix4x4.TRS(new Vector3(0.4f, 0f, 0.08f), Quaternion.Euler(0f, 40f, 0f), Vector3.one * 0.8f), DesertPart.Clay);
            kit.Add(LowPolyMesh.Lathe("jar", jar, 9), Matrix4x4.TRS(new Vector3(0.62f, 0f, -0.22f), Quaternion.Euler(0f, 80f, 0f), Vector3.one * 0.55f), DesertPart.Clay);
            kit.Add(LowPolyMesh.Lathe("band", new[] { new Vector2(0.255f, 0.3f), new Vector2(0.262f, 0.33f), new Vector2(0.255f, 0.36f) }, 9),
                Matrix4x4.Translate(new Vector3(-0.15f, 0f, 0f)), DesertPart.Indigo);
            return kit;
        }

        private static Transform DesertProp(Transform parent, string name, FlatMeshKit<DesertPart> kit, Vector3 at)
        {
            Mesh mesh = SaveMesh<DesertPart>(kit.Build("DesertCamp" + name.Replace(" ", "")), DesertFolder + "/DesertCamp" + name.Replace(" ", "") + ".asset");
            var prop = new GameObject(name);
            prop.transform.SetParent(parent, false);
            prop.transform.localPosition = at;
            prop.AddComponent<MeshFilter>().sharedMesh = mesh;
            var materials = new Material[mesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++)
            {
                switch (FlatMeshKit<DesertPart>.MaterialOf(mesh, i))
                {
                    case DesertPart.Driftwood: materials[i] = DesertMaterial("DesertCampDriftwood", new Color(0.8f, 0.72f, 0.6f)); break;
                    case DesertPart.DriftwoodDark: materials[i] = DesertMaterial("DesertCampDriftwoodDark", new Color(0.56f, 0.46f, 0.36f)); break;
                    case DesertPart.Canvas: materials[i] = DesertMaterial("DesertCampCanvas", new Color(0.91f, 0.83f, 0.67f)); break;
                    case DesertPart.Indigo: materials[i] = DesertMaterial("DesertCampIndigo", new Color(0.24f, 0.29f, 0.5f)); break;
                    case DesertPart.Terracotta: materials[i] = DesertMaterial("DesertCampTerracotta", new Color(0.7f, 0.37f, 0.23f)); break;
                    case DesertPart.Clay: materials[i] = DesertMaterial("DesertCampClay", new Color(0.74f, 0.45f, 0.3f)); break;
                    default: materials[i] = DesertMaterial("DesertCampRope", new Color(0.82f, 0.72f, 0.52f)); break;
                }
            }
            prop.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return prop.transform;
        }

        private static Material DesertMaterial(string name, Color color, float smoothness = 0.08f)
        {
            string path = DesertFolder + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Saves a generated mesh, keeping the asset (and its GUID) when it already exists.
        private static Mesh SaveMesh<T>(Mesh mesh, string path) where T : struct, Enum
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            string order = FlatMeshKit<T>.Order(mesh);
            EditorUtility.CopySerialized(mesh, existing);
            FlatMeshKit<T>.Remember(existing, order);
            UnityEngine.Object.DestroyImmediate(mesh);
            return existing;
        }

        // ---------- Trophies ----------

        // The Chief Toad's feathered crown, copied (looks only) from his prefab, resting on y = 0.
        private static void BuildTrophies()
        {
            EnsureFolder(TrophyFolder);
            var toadAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ChiefToadPath);
            if (toadAsset == null) throw new InvalidOperationException("Missing " + ChiefToadPath);
            var toad = (GameObject)PrefabUtility.InstantiatePrefab(toadAsset);
            Transform source = toad.GetComponent<ChiefToad>() != null ? CrownOf(toad) : null;
            if (source == null) { UnityEngine.Object.DestroyImmediate(toad); throw new InvalidOperationException("The Chief Toad prefab has no crown."); }
            GameObject crown = UnityEngine.Object.Instantiate(source.gameObject);
            crown.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            crown.transform.localScale = source.lossyScale;
            UnityEngine.Object.DestroyImmediate(toad);
            foreach (Component part in crown.GetComponentsInChildren<Component>(true))
                if (!(part is Transform || part is MeshFilter || part is MeshRenderer)) UnityEngine.Object.DestroyImmediate(part);
            // Stand it on its base and centre it.
            var root = new GameObject("Chief Toad's Crown");
            Bounds bounds = new Bounds(crown.transform.position, Vector3.zero);
            foreach (Renderer part in crown.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(part.bounds);
            crown.transform.SetParent(root.transform, true);
            crown.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            float size = Mathf.Max(bounds.size.x, bounds.size.z);
            if (size > 0.01f) root.transform.localScale = Vector3.one * Mathf.Clamp(0.7f / size, 0.2f, 3f); // about 0.7 m across on the stand
            var holder = new GameObject("chief_toad_crown");
            root.transform.SetParent(holder.transform, false);
            PrefabUtility.SaveAsPrefabAsset(holder, TrophyFolder + "/chief_toad_crown.prefab");
            UnityEngine.Object.DestroyImmediate(holder);
        }

        private static Transform CrownOf(GameObject toad)
        {
            var data = new SerializedObject(toad.GetComponent<ChiefToad>());
            return data.FindProperty("crown").objectReferenceValue as Transform;
        }

        // ---------- The stand ----------

        // A carved wooden plinth with a dark inset and a brass name plate on the front (+Z).
        private static Mesh StandMesh(out Material[] materials)
        {
            var kit = new FlatMeshKit<Mat>();
            kit.Box(new Vector3(0f, 0.08f, 0f), new Vector3(0.75f, 0.16f, 0.75f), Quaternion.identity, Mat.Wood);
            kit.Box(new Vector3(0f, 0.55f, 0f), new Vector3(0.45f, 0.8f, 0.45f), Quaternion.identity, Mat.Wood);
            kit.Box(new Vector3(0f, 0.55f, 0.226f), new Vector3(0.32f, 0.6f, 0.01f), Quaternion.identity, Mat.Dark);
            kit.Box(new Vector3(0f, 0.98f, 0f), new Vector3(0.68f, 0.08f, 0.68f), Quaternion.identity, Mat.Wood);
            kit.Box(new Vector3(0f, 0.72f, 0.234f), new Vector3(0.24f, 0.08f, 0.01f), Quaternion.identity, Mat.Brass);
            Mesh mesh = kit.Build("TrophyStand");
            string path = "Assets/Prefabs/Core/TrophyStand.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            string order = FlatMeshKit<Mat>.Order(mesh);
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else
            {
                EditorUtility.CopySerialized(mesh, existing);
                FlatMeshKit<Mat>.Remember(existing, order);
                UnityEngine.Object.DestroyImmediate(mesh);
                mesh = existing;
            }
            materials = new Material[mesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++)
            {
                Mat key = FlatMeshKit<Mat>.MaterialOf(mesh, i);
                materials[i] = AssetDatabase.LoadAssetAtPath<Material>(MeadowMaterials + (key == Mat.Wood ? "MeadowLog.mat" : key == Mat.Dark ? "MeadowBark.mat" : "MeadowFlowerYellow.mat"));
            }
            return mesh;
        }

        // ---------- Helpers ----------

        private static GameObject Place(string path, Transform parent, Vector3 position, float yaw, float scale)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new InvalidOperationException("Missing prefab " + path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            instance.transform.localScale = Vector3.one * scale;
            return instance;
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
