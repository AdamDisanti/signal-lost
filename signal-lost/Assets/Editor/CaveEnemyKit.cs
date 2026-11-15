using System;
using SignalLost.Effects;
using SignalLost.Enemies;
using SignalLost.Inventory;
using SignalLost.Player;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static SignalLost.Editor.EnemyTestBuilder;
using Object = UnityEngine.Object;

namespace SignalLost.Editor
{
    /// <summary>
    /// Shared pieces for the Crystal Caves enemy builders (the crystal chameleon and the cave beetles): their asset
    /// folders, materials, meshes and prefabs, and a small cave test arena made from the level's own floor, wall,
    /// crystal and rock assets, lit like the inside of the cave, with the shared Player and GameSystems.
    /// </summary>
    internal static class CaveEnemyKit
    {
        public const string PrefabFolder = "Assets/Prefabs/Enemies/Caves";
        public const string MeshFolder = PrefabFolder + "/Meshes";
        public const string MaterialFolder = "Assets/Materials/Enemies/Caves";
        private const string CaveMaterials = CrystalAssetBuilder.MaterialFolder;

        public static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        public static void EnsureFolders()
        {
            foreach (string folder in new[] { "Assets/Prefabs", "Assets/Prefabs/Enemies", PrefabFolder, MeshFolder,
                         "Assets/Materials", "Assets/Materials/Enemies", MaterialFolder })
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                int slash = folder.LastIndexOf('/');
                AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
            }
        }

        /// <summary>Saves a mesh asset, replacing an existing one's contents so references and GUIDs survive.</summary>
        public static Mesh SaveMesh(Mesh mesh)
        {
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

        public static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            if (prefab == null) throw new InvalidOperationException("Could not save prefab " + path);
            return prefab;
        }

        /// <summary>A URP Lit material in the cave enemy folder; an existing one is reused unchanged.</summary>
        public static Material Lit(string name, Color color, float metallic, float smoothness)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>A glowing Lit material, set up the same way as the cave's crystal materials.</summary>
        public static Material Glowing(string name, Color color, float brightness)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, 1f));
            material.SetFloat("_Smoothness", 0.78f);
            material.SetColor("_EmissionColor", color * brightness);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; // with None, URP's material check drops _EMISSION on import
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>One of the cave's own crystal meshes (CrystalSpire, CrystalCluster, CrystalShard).</summary>
        public static Mesh CrystalMesh(string shape)
        {
            if (Load(CrystalAssetBuilder.CrystalPath(shape)) == null) CrystalAssetBuilder.Build();
            return Load(CrystalAssetBuilder.CrystalPath(shape)).GetComponent<MeshFilter>().sharedMesh;
        }

        public static void SetDrops(Component dropper, params (ScrapType type, int min, int max)[] drops)
        {
            var data = new SerializedObject(dropper);
            SerializedProperty list = data.FindProperty("drops");
            list.arraySize = drops.Length;
            for (int i = 0; i < drops.Length; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("type").enumValueIndex = (int)drops[i].type;
                entry.FindPropertyRelative("min").intValue = drops[i].min;
                entry.FindPropertyRelative("max").intValue = drops[i].max;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Test arena ----------

        /// <summary>
        /// A walled, roofed cave room (width x length, centred on the origin, +Z north) with the level's cave floor and
        /// wall materials and a NavMesh built at runtime. Returns the arena root; put crystals and rocks under it.
        /// </summary>
        public static Transform BuildCaveRoom(string name, float width, float length, float height)
        {
            Material floor = MaterialOr(CaveMaterials + "/GroundCaveFloor.mat", new Color(0.36f, 0.38f, 0.45f));
            Material wall = MaterialOr(CaveMaterials + "/GroundCaveWall.mat", new Color(0.3f, 0.32f, 0.4f));
            Material roof = MaterialOr(CaveMaterials + "/GroundCaveCeiling.mat", new Color(0.26f, 0.27f, 0.33f));

            var area = new GameObject(name);
            var surface = area.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            area.AddComponent<NavMeshBaker>();
            Transform a = area.transform;
            Block("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(width, 1f, length), floor, a);
            Block("North wall", new Vector3(0f, height * 0.5f, length * 0.5f + 0.5f), new Vector3(width + 2f, height, 1f), wall, a);
            Block("South wall", new Vector3(0f, height * 0.5f, -length * 0.5f - 0.5f), new Vector3(width + 2f, height, 1f), wall, a);
            Block("East wall", new Vector3(width * 0.5f + 0.5f, height * 0.5f, 0f), new Vector3(1f, height, length), wall, a);
            Block("West wall", new Vector3(-width * 0.5f - 0.5f, height * 0.5f, 0f), new Vector3(1f, height, length), wall, a);
            GameObject ceiling = Block("Ceiling", new Vector3(0f, height + 0.5f, 0f), new Vector3(width + 2f, 1f, length + 2f), roof, a);
            Object.DestroyImmediate(ceiling.GetComponent<Collider>()); // out of reach; keeps the NavMesh off its top
            return a;
        }

        private static Material MaterialOr(string path, Color fallback)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            return material != null ? material : Lit(System.IO.Path.GetFileNameWithoutExtension(path), fallback, 0f, 0.1f);
        }

        /// <summary>A real crystal batch from the level kit, in one of the six cave colours.</summary>
        public static GameObject PlaceCrystalBatch(Transform parent, Vector3 position, float yaw, float scale, CrystalAssetBuilder.CrystalColor color)
        {
            if (Load(CrystalAssetBuilder.BatchPath) == null) CrystalAssetBuilder.Build();
            var batch = (GameObject)PrefabUtility.InstantiatePrefab(Load(CrystalAssetBuilder.BatchPath), parent);
            batch.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            batch.transform.localScale = Vector3.one * scale;
            var data = new SerializedObject(batch.GetComponent<CrystalBatch>());
            data.FindProperty("crystalMaterial").objectReferenceValue = CrystalAssetBuilder.CrystalMaterial(color);
            data.ApplyModifiedPropertiesWithoutUndo();
            batch.GetComponent<CrystalBatch>().Apply();
            Light glow = batch.GetComponentInChildren<Light>();
            if (glow != null)
            {
                // A small room: the level's 24 m, 16-intensity glow would flood it.
                glow.range = 10f;
                glow.intensity = 4f;
            }
            return batch;
        }

        /// <summary>A cave rock from the level kit (CaveBoulder, Stalagmite, CaveColumn...), with its collider.</summary>
        public static GameObject PlaceRock(Transform parent, string rock, Vector3 position, float yaw, Vector3 scale)
        {
            if (Load(CrystalAssetBuilder.RockPath(rock)) == null) CrystalAssetBuilder.Build();
            var item = (GameObject)PrefabUtility.InstantiatePrefab(Load(CrystalAssetBuilder.RockPath(rock)), parent);
            item.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            item.transform.localScale = scale;
            return item;
        }

        /// <summary>The shared Player (laser, scanner and flask, as in the Crystal Caves level) and GameSystems.</summary>
        public static GameObject AddCorePlayer(Scene scene, Vector3 start, float yaw)
        {
            if (Load(CorePrefabBuilder.PlayerPath) == null) CorePrefabBuilder.Build();
            GameObject player = CoreSceneSetup.UpgradeToCorePlayer(scene, new[] { PlayerItem.MiningLaser, PlayerItem.Scanner, PlayerItem.Flask }, null);
            Quaternion facing = Quaternion.Euler(0f, yaw, 0f);
            player.transform.SetPositionAndRotation(start, facing);
            foreach (PlayerSpawnPoint spawn in All<PlayerSpawnPoint>()) spawn.transform.SetPositionAndRotation(start, facing);
            Camera view = player.GetComponentInChildren<Camera>(true);
            if (view != null)
            {
                view.clearFlags = CameraClearFlags.SolidColor;
                view.backgroundColor = new Color(0.15f, 0.17f, 0.24f);
            }
            return player;
        }

        /// <summary>The inside-the-cave light from CaveLighting's defaults: an even fill, cave fog, no sun.</summary>
        public static void CaveLight()
        {
            RenderSettings.sun = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.72f, 0.76f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.56f, 0.6f, 0.74f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.44f, 0.55f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.15f, 0.17f, 0.24f);
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 420f;
            RenderSettings.skybox = null;
        }
    }
}
