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
