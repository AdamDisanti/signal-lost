using SignalLost.Combat;
using SignalLost.Core;
using SignalLost.Enemies;
using SignalLost.Enemies.Caves;
using SignalLost.Enemies.Desert;
using SignalLost.Inventory;
using SignalLost.Player;
using SignalLost.Scanner;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using static SignalLost.Editor.EnemyTestBuilder;
using CrystalColor = SignalLost.Editor.CrystalAssetBuilder.CrystalColor;
using Hitbox = SignalLost.Editor.DesertDuelBossBuilder.Hitbox;

namespace SignalLost.Editor
{
    /// <summary>
    /// Builds Grumble, the Geode Bear (Crystal Caves mini-boss, docs/geode-bear.md) the same way as the Shard Mole
    /// (<see cref="ShardMoleBuilder"/>): from the Blender model and clips made by tools/blender/cave_bosses.py in
    /// Assets/Prefabs/Enemies/Caves/Bosses/GeodeBear/, with the cave's own cyan and purple crystal materials on its
    /// crest, bone hitboxes, the geode-back and power-cell weak spots, crest lights, scrap drops and the hidden power
    /// cell that drops on defeat. Menu: Signal Lost > Crystal Caves Enemies > Geode Bear. Test scene:
    /// Scenes/Bosses/GeodeBearTest.unity.
    /// </summary>
    public static class GeodeBearBuilder
    {
        public const string PrefabPath = ShardMoleBuilder.Root + "/GeodeBear/GeodeBear.prefab";
        public const string ScenePath = "Assets/Scenes/Bosses/GeodeBearTest.unity";
        private static readonly Vector3 Home = new Vector3(0f, 0f, 8f);

        [MenuItem("Signal Lost/Crystal Caves Enemies/Geode Bear/Build Prefab")]
        public static void BuildPrefabMenu()
        {
            BuildPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("GEODE_BEAR_PREFAB_BUILT");
        }

        [MenuItem("Signal Lost/Crystal Caves Enemies/Geode Bear/Create Test Scene")]
        public static void CreateTestScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Debug.Log(ScenePath + " already exists; skipped (edit it in Unity, or delete it to rebuild).");
                return;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            GameObject prefab = CaveEnemyKit.Load(PrefabPath) ?? BuildPrefab();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Transform room = CaveEnemyKit.BuildCaveRoom("Geode Bear Test Cave", 52f, 70f, 12f);
            // A den ringed with the bear's own cyan and purple crystals, rocks to break it up.
            for (int i = 0; i < 10; i++)
            {
                float angle = i * 36f + 12f;
                Vector3 at = Home + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * (ShardMoleBuilder.ArenaRadius + 3f);
                if (Mathf.Abs(at.x) > 24f || at.z > 33f || at.z < -33f) continue;
                CaveEnemyKit.PlaceCrystalBatch(room, at, angle + 180f, 0.8f + 0.15f * (i % 3), i % 2 == 0 ? CrystalColor.Purple : CrystalColor.Cyan);
            }
            CaveEnemyKit.PlaceRock(room, "Stalagmite", Home + new Vector3(9f, 0f, 5f), 0f, new Vector3(0.8f, 3f, 0.8f));
            CaveEnemyKit.PlaceRock(room, "CaveBoulder", Home + new Vector3(-10f, 0.4f, -2f), 40f, new Vector3(1.6f, 1.2f, 1.4f));
            CaveEnemyKit.PlaceRock(room, "CaveBoulderRound", Home + new Vector3(6f, 0.4f, -11f), 0f, new Vector3(1.3f, 1.1f, 1.3f));

            var bear = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            bear.name = "Geode Bear";
            bear.transform.SetPositionAndRotation(Home, Quaternion.Euler(0f, 180f, 0f));

            CaveEnemyKit.AddCorePlayer(scene, new Vector3(0f, 0.05f, -26f), 0f);
            new GameObject("Mini-Boss HUD").AddComponent<MiniBossHud>();
            CaveEnemyKit.CaveLight();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("GEODE_BEAR_TEST_SCENE_CREATED_AND_VALIDATED");
        }

        [MenuItem("Signal Lost/Crystal Caves Enemies/Geode Bear/Validate")]
        public static void Validate()
        {
            GameObject prefab = CaveEnemyKit.Load(PrefabPath);
            Require(prefab != null, PrefabPath + " is missing (Build Prefab).");
            var bear = prefab.GetComponent<GeodeBear>();
            Require(bear != null && prefab.GetComponent<EnemySignalSource>() != null, "The bear needs GeodeBear and an EnemySignalSource.");
            Require(prefab.GetComponent<ScrapDropper>() != null, "The bear must drop scrap (ScrapDropper).");
            Require(!prefab.GetComponent<NavMeshAgent>().enabled, "The NavMeshAgent must start disabled (DesertFodder enables it).");
            var data = new SerializedObject(bear);
            foreach (string field in new[] { "visual", "clipTable", "animator", "shipPart", "crystalMaterial", "rockMaterial", "powerCell",
                         "geodeBack", "cellLight", "markerMaterial", "shadowMaterial", "puffMesh", "spikeMesh" })
                Require(data.FindProperty(field).objectReferenceValue != null, "GeodeBear." + field + " is not set.");
            Require(data.FindProperty("partName").stringValue == "Power cell", "The bear's part is the power cell.");
            Require(data.FindProperty("phaseAt").arraySize == 2, "The bear has three phases.");
            Require(data.FindProperty("crestLights").arraySize == 2, "The bear needs its two crest lights.");
            Require(!data.FindProperty("fodderWhileEngaged").boolValue, "The bear fights alone (no adds).");
            var table = BossClipTable.Parse((TextAsset)data.FindProperty("clipTable").objectReferenceValue);
            foreach (string clip in new[] { "Idle", "Walk", "Run", "Roar", "SwipeL", "SwipeR", "Lob", "Yawn", "BellyFlop", "HugLunge",
                         "HugSqueeze", "CurlUp", "Hibernate", "WakeUp", "Stagger", "Death" })
                Require(table.Has(clip), "GeodeBearClips.json has no " + clip + " clip.");
            Require(prefab.GetComponentsInChildren<SphereCollider>(true).Length >= 10, "The bear needs its bone hitboxes.");
            foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                    Require(m != null, "The bear has a missing material on " + r.name + ".");

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;
            EditorSceneManager.OpenScene(ScenePath);
            Require(Count<FirstPersonController>() == 1 && Count<PlayerGear>() == 1, ScenePath + ": expected one shared Player.");
            Require(Count<GameSystems>() == 1 && Count<PlayerSpawnPoint>() >= 1, ScenePath + ": expected GameSystems and a spawn point.");
            Require(Count<Camera>() == 1 && Count<AudioListener>() == 1, ScenePath + ": expected one camera and listener.");
            Require(Count<NavMeshSurface>() == 1 && Count<NavMeshBaker>() == 1, ScenePath + ": expected one NavMeshSurface with a NavMeshBaker.");
            Require(Count<GeodeBear>() == 1 && Count<MiniBossHud>() == 1, ScenePath + ": expected one Geode Bear and the mini-boss HUD.");
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0, ScenePath + ": missing script on " + item.name);
            Debug.Log("GEODE_BEAR_VALIDATED");
        }

        internal static GameObject BuildPrefab()
        {
            CaveEnemyKit.EnsureFolders();
            if (!AssetDatabase.IsValidFolder(ShardMoleBuilder.Root)) AssetDatabase.CreateFolder(CaveEnemyKit.PrefabFolder, "Bosses");
            if (CrystalAssetBuilder.CrystalMaterial(CrystalColor.Cyan) == null) CrystalAssetBuilder.Build();
            Material cyan = CrystalAssetBuilder.CrystalMaterial(CrystalColor.Cyan), purple = CrystalAssetBuilder.CrystalMaterial(CrystalColor.Purple);

            GameObject root = DesertDuelBossBuilder.Begin("GeodeBear", out Transform model, out BossClipTable table, out _, ShardMoleBuilder.Root, mats =>
            {
                // The crest glows exactly like the cave's own crystals.
                mats["GeodeBearCrystalCyan"] = cyan;
                mats["GeodeBearCrystalPurple"] = purple;
            });
            var boss = root.AddComponent<GeodeBear>();

            Material metal = CaveEnemyKit.Lit("GeodeBearCellMetal", new Color(0.3f, 0.31f, 0.34f), 0.75f, 0.5f);
            Material cellGlow = CaveEnemyKit.Glowing("GeodeBearCellGlow", new Color(0.45f, 1f, 0.55f), 2.2f);
            Transform part = DesertDuelBossBuilder.HiddenPart(root, model, "Cell", "Power cell", go =>
            {
                Mesh shell = CaveEnemyKit.SaveMesh(LowPolyMesh.Lathe("GeodeBearCellShell", new[]
                {
                    new Vector2(0f, -0.42f), new Vector2(0.25f, -0.42f), new Vector2(0.25f, 0.42f), new Vector2(0f, 0.42f),
                }, 8));
                Mesh core = CaveEnemyKit.SaveMesh(LowPolyMesh.Lathe("GeodeBearCellCore", new[]
                {
                    new Vector2(0f, -0.27f), new Vector2(0.26f, -0.27f), new Vector2(0.26f, 0.27f), new Vector2(0f, 0.27f),
                }, 8));
                Quaternion across = Quaternion.Euler(0f, 0f, 90f);
                DesertDuelBossBuilder.AddMesh(go, shell, metal, Vector3.zero, across, Vector3.one);
                DesertDuelBossBuilder.AddMesh(go, core, cellGlow, Vector3.zero, across, Vector3.one);
            });

            DesertDuelBossBuilder.Hitboxes(model, new[]
            {
                new Hitbox("Hips", 1.0f), new Hitbox("Spine", 1.05f), new Hitbox("Chest", 1.0f), new Hitbox("Neck", 0.7f),
                new Hitbox("Head", 0.65f), new Hitbox("Jaw", 0.35f), new Hitbox("ForearmL", 0.38f), new Hitbox("ForearmR", 0.38f),
                new Hitbox("HandL", 0.35f), new Hitbox("HandR", 0.35f), new Hitbox("ShinL", 0.38f), new Hitbox("ShinR", 0.38f),
            });
            Health cell = DesertDuelBossBuilder.WeakSpot(model, "Cell", null, 0f, 0.5f, "Geode Bear (power cell)");
            Health back = DesertDuelBossBuilder.WeakSpot(model, "Crest", null, 0f, 0.85f, "Geode Bear (geode back)", Vector3.up * 0.15f);

            // The crest glows like a crystal batch (one cyan light, one purple); the cell glows green.
            Transform crest = DesertDuelBossBuilder.FindDeep(model, "Crest");
            Light lightCyan = CrystalAssetBuilder.AddGlow(crest, Vector3.zero, 6f, 1.4f);
            lightCyan.color = CrystalAssetBuilder.Glow(CrystalColor.Cyan);
            Light lightPurple = CrystalAssetBuilder.AddGlow(crest, Vector3.zero, 6f, 1.4f);
            lightPurple.color = CrystalAssetBuilder.Glow(CrystalColor.Purple);
            Light cellLight = CrystalAssetBuilder.AddGlow(DesertDuelBossBuilder.FindDeep(model, "Cell"), Vector3.zero, 4f, 1.2f);
            cellLight.color = new Color(0.45f, 1f, 0.55f);

            var data = DesertDuelBossBuilder.Common(boss, root, model, table, "Grumble, the Geode Bear", "Power cell", part, 1850f, 2.6f, 6.8f,
                new[] { 0.66f, 0.33f }, new[] { "Groggy", "Cranky", "Sleepwalker" }, new[] { "Neck", "Head" }, 1.5f, ShardMoleBuilder.Root);
            data.FindProperty("arenaRadius").floatValue = ShardMoleBuilder.ArenaRadius; // a cave chamber, like the mole's
            data.FindProperty("poise").floatValue = 200f; // a step above Pitchclaw's 190 (Amanda, 11-14)
            data.FindProperty("stalkDistance").floatValue = 5.5f;
            data.FindProperty("markerMaterial").objectReferenceValue = CaveEnemyKit.Glowing("GeodeBearWarning", CrystalAssetBuilder.Glow(CrystalColor.Purple), 2.2f);
            data.FindProperty("crystalMaterial").objectReferenceValue = purple;
            data.FindProperty("rockMaterial").objectReferenceValue = CaveEnemyKit.Lit("GeodeBearRock", new Color(0.42f, 0.41f, 0.46f), 0f, 0.1f);
            data.FindProperty("powerCell").objectReferenceValue = cell;
            data.FindProperty("geodeBack").objectReferenceValue = back;
            data.FindProperty("cellLight").objectReferenceValue = cellLight;
            SerializedProperty lightList = data.FindProperty("crestLights");
            lightList.arraySize = 2;
            lightList.GetArrayElementAtIndex(0).objectReferenceValue = lightCyan;
            lightList.GetArrayElementAtIndex(1).objectReferenceValue = lightPurple;
            data.ApplyModifiedPropertiesWithoutUndo();

            // Between the Shard Mole (3-5 / 1-2 / 1-2) and the Cave Axolotl stage boss (5-8 / 2-3 / 2-3).
            CaveEnemyKit.SetDrops(root.AddComponent<ScrapDropper>(), (ScrapType.ScrapMetal, 4, 6), (ScrapType.Wiring, 4, 5), (ScrapType.CircuitBoards, 1, 2),
                (ScrapType.FuelCells, 1, 1)); // with the Beetles' and the Mole's, always enough for the Crystal Disruptor (8 wiring, 4 boards, 1 fuel cell)
            return DesertDuelBossBuilder.Save(root, PrefabPath);
        }
    }
}
