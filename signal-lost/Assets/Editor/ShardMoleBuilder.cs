using System.Collections.Generic;
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
    /// Builds the Shard Mole (Crystal Caves mini-boss, docs/shard-mole.md) the same way the desert bosses are built
    /// (<see cref="DesertDuelBossBuilder"/>): from the Blender model and clips made by tools/blender/cave_bosses.py in
    /// Assets/Prefabs/Enemies/Caves/Bosses/ShardMole/, with the cave's own cyan crystal material on its claws and
    /// blade, bone hitboxes, claw lights, scrap drops and the hidden antenna array that drops on defeat.
    /// Menu: Signal Lost > Crystal Caves Enemies > Shard Mole. Its test scene is Scenes/Bosses/ShardMoleTest.unity.
    /// </summary>
    public static class ShardMoleBuilder
    {
        public const string Root = CaveEnemyKit.PrefabFolder + "/Bosses";
        public const string PrefabPath = Root + "/ShardMole/ShardMole.prefab";
        public const string ScenePath = "Assets/Scenes/Bosses/ShardMoleTest.unity";
        public const float ArenaRadius = 18f;
        private static readonly Vector3 Home = new Vector3(0f, 0f, 8f);

        [MenuItem("Signal Lost/Crystal Caves Enemies/Shard Mole/Build Prefab")]
        public static void BuildPrefabMenu()
        {
            BuildPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("SHARD_MOLE_PREFAB_BUILT");
        }

        [MenuItem("Signal Lost/Crystal Caves Enemies/Shard Mole/Create Test Scene")]
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
            Transform room = CaveEnemyKit.BuildCaveRoom("Shard Mole Test Cave", 52f, 70f, 12f);
            // A cave chamber ringed with cyan crystals (the mole's own colour) and a few others, rocks to break it up.
            for (int i = 0; i < 10; i++)
            {
                float angle = i * 36f + 12f;
                Vector3 at = Home + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * (ArenaRadius + 3f);
                if (Mathf.Abs(at.x) > 24f || at.z > 33f || at.z < -33f) continue;
                CaveEnemyKit.PlaceCrystalBatch(room, at, angle + 180f, 0.8f + 0.15f * (i % 3), i % 3 == 0 ? CrystalColor.Purple : CrystalColor.Cyan);
            }
            CaveEnemyKit.PlaceRock(room, "Stalagmite", Home + new Vector3(-9f, 0f, 4f), 0f, new Vector3(0.8f, 3f, 0.8f));
            CaveEnemyKit.PlaceRock(room, "CaveBoulder", Home + new Vector3(10f, 0.4f, -3f), 40f, new Vector3(1.6f, 1.2f, 1.4f));
            CaveEnemyKit.PlaceRock(room, "CaveBoulderRound", Home + new Vector3(-6f, 0.4f, -11f), 0f, new Vector3(1.3f, 1.1f, 1.3f));

            var mole = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            mole.name = "Shard Mole";
            mole.transform.SetPositionAndRotation(Home, Quaternion.Euler(0f, 180f, 0f));

            CaveEnemyKit.AddCorePlayer(scene, new Vector3(0f, 0.05f, -26f), 0f);
            new GameObject("Mini-Boss HUD").AddComponent<MiniBossHud>();
            CaveEnemyKit.CaveLight();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("SHARD_MOLE_TEST_SCENE_CREATED_AND_VALIDATED");
        }

        [MenuItem("Signal Lost/Crystal Caves Enemies/Shard Mole/Validate")]
        public static void Validate()
        {
            GameObject prefab = CaveEnemyKit.Load(PrefabPath);
            Require(prefab != null, PrefabPath + " is missing (Build Prefab).");
            var mole = prefab.GetComponent<ShardMole>();
            Require(mole != null && prefab.GetComponent<EnemySignalSource>() != null, "The mole needs ShardMole and an EnemySignalSource.");
            Require(prefab.GetComponent<ScrapDropper>() != null, "The mole must drop scrap (ScrapDropper).");
            Require(!prefab.GetComponent<NavMeshAgent>().enabled, "The NavMeshAgent must start disabled (DesertFodder enables it).");
            var data = new SerializedObject(mole);
            foreach (string field in new[] { "visual", "clipTable", "animator", "shipPart", "crystalMaterial", "moundMaterial", "markerMaterial", "puffMesh", "spikeMesh" })
                Require(data.FindProperty(field).objectReferenceValue != null, "ShardMole." + field + " is not set.");
            Require(data.FindProperty("partName").stringValue == "Antenna array", "The mole's part is the antenna array.");
            Require(data.FindProperty("phaseAt").arraySize == 2, "The mole has three phases.");
            Require(data.FindProperty("clawLights").arraySize == 2, "The mole needs a light on each claw.");
            Require(!data.FindProperty("fodderWhileEngaged").boolValue, "The mole fights alone (no adds), like the desert bosses.");
            var table = BossClipTable.Parse((TextAsset)data.FindProperty("clipTable").objectReferenceValue);
            foreach (string clip in new[] { "Idle", "Walk", "Run", "Roar", "Listen", "Startle", "SwipeL", "SwipeR", "ClawFlurry", "TailSwoop",
                         "BurrowIn", "BurrowOut", "Quake", "Stagger", "Death" })
                Require(table.Has(clip), "ShardMoleClips.json has no " + clip + " clip.");
            Require(prefab.GetComponentsInChildren<SphereCollider>(true).Length >= 8, "The mole needs its bone hitboxes.");
            foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                    Require(m != null, "The mole has a missing material on " + r.name + ".");

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;
            EditorSceneManager.OpenScene(ScenePath);
            Require(Count<FirstPersonController>() == 1 && Count<PlayerGear>() == 1, ScenePath + ": expected one shared Player.");
            Require(Count<GameSystems>() == 1 && Count<PlayerSpawnPoint>() >= 1, ScenePath + ": expected GameSystems and a spawn point.");
            Require(Count<Camera>() == 1 && Count<AudioListener>() == 1, ScenePath + ": expected one camera and listener.");
            Require(Count<NavMeshSurface>() == 1 && Count<NavMeshBaker>() == 1, ScenePath + ": expected one NavMeshSurface with a NavMeshBaker.");
            Require(Count<ShardMole>() == 1 && Count<MiniBossHud>() == 1, ScenePath + ": expected one Shard Mole and the mini-boss HUD.");
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0, ScenePath + ": missing script on " + item.name);
            Debug.Log("SHARD_MOLE_VALIDATED");
        }

        internal static GameObject BuildPrefab()
        {
            CaveEnemyKit.EnsureFolders();
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder(CaveEnemyKit.PrefabFolder, "Bosses");
            Material crystal = CrystalAssetBuilder.CrystalMaterial(CrystalColor.Cyan);
            if (crystal == null)
            {
                CrystalAssetBuilder.Build();
                crystal = CrystalAssetBuilder.CrystalMaterial(CrystalColor.Cyan);
            }

            GameObject root = DesertDuelBossBuilder.Begin("ShardMole", out Transform model, out BossClipTable table, out _, Root,
                mats => mats["ShardMoleCrystal"] = crystal); // claws and blade glow exactly like the cave's crystals
            var boss = root.AddComponent<ShardMole>();

            Transform part = DesertDuelBossBuilder.HiddenPart(root, model, "Antenna", "Antenna array", go =>
            {
                Material metal = CaveEnemyKit.Lit("ShardMoleAntennaMetal", new Color(0.6f, 0.6f, 0.62f), 0.75f, 0.55f);
                Material signal = CaveEnemyKit.Glowing("ShardMoleAntennaLight", new Color(1f, 0.32f, 0.22f), 2f);
                Mesh dish = CaveEnemyKit.SaveMesh(LowPolyMesh.Lathe("ShardMoleDish", new[]
                {
                    new Vector2(0f, 0f), new Vector2(0.2f, 0.08f), new Vector2(0.42f, 0.2f), new Vector2(0.4f, 0.22f), new Vector2(0f, 0.04f),
                }, 10));
                Mesh rod = CaveEnemyKit.SaveMesh(LowPolyMesh.TaperedPrism("ShardMoleRod", 4, 0.5f));
                Mesh gem = CaveEnemyKit.SaveMesh(LowPolyMesh.Icosphere("CaveGem", 0));
                DesertDuelBossBuilder.AddMesh(go, dish, metal, Vector3.zero, Quaternion.Euler(-20f, 0f, 0f), Vector3.one);
                for (int i = 0; i < 3; i++)
                {
                    var at = new Vector3((i - 1) * 0.18f, 0f, (i % 2) * 0.12f);
                    DesertDuelBossBuilder.AddMesh(go, rod, metal, at, Quaternion.Euler(-90f, 0f, 0f), new Vector3(0.04f, 0.04f, 0.8f + 0.15f * i));
                    DesertDuelBossBuilder.AddMesh(go, gem, signal, at + Vector3.up * (0.8f + 0.15f * i), Quaternion.identity, Vector3.one * 0.05f);
                }
            });

            DesertDuelBossBuilder.Hitboxes(model, new[]
            {
                new Hitbox("Hips", 1.0f), new Hitbox("Spine", 1.15f), new Hitbox("Chest", 1.0f), new Hitbox("Head", 0.7f),
                new Hitbox("Snout", 0.4f), new Hitbox("ForearmL", 0.4f), new Hitbox("ForearmR", 0.4f), new Hitbox("HandL", 0.55f),
                new Hitbox("HandR", 0.55f), new Hitbox("Tail2", 0.25f), new Hitbox("TailTip", 0.3f),
            });

            // The claws glow like a crystal batch: a cyan light on each hand.
            Color glow = CrystalAssetBuilder.Glow(CrystalColor.Cyan);
            var lights = new List<Light>();
            foreach (string hand in new[] { "HandL", "HandR" })
            {
                Light light = CrystalAssetBuilder.AddGlow(DesertDuelBossBuilder.FindDeep(model, hand), Vector3.zero, 5f, 1.4f);
                light.color = glow;
                lights.Add(light);
            }

            var data = DesertDuelBossBuilder.Common(boss, root, model, table, "The Shard Mole", "Antenna array", part, 1100f, 2.8f, 6.5f,
                new[] { 0.66f, 0.33f }, new[] { "Listening", "Restless", "Shattering" }, new[] { "Chest", "Head" }, 1.4f, Root);
            data.FindProperty("arenaRadius").floatValue = ArenaRadius; // cave chambers are smaller than the desert arenas
            data.FindProperty("poise").floatValue = 120f;
            data.FindProperty("stalkDistance").floatValue = 5f;
            data.FindProperty("markerMaterial").objectReferenceValue = CaveEnemyKit.Glowing("ShardMoleWarning", glow, 2.2f);
            data.FindProperty("crystalMaterial").objectReferenceValue = crystal;
            data.FindProperty("moundMaterial").objectReferenceValue = CaveEnemyKit.Lit("ShardMoleDirt", new Color(0.3f, 0.31f, 0.37f), 0f, 0.08f);
            SerializedProperty lightList = data.FindProperty("clawLights");
            lightList.arraySize = lights.Count;
            for (int i = 0; i < lights.Count; i++) lightList.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
            data.ApplyModifiedPropertiesWithoutUndo();

            // Scrap like the cave beetles, the other Crystal Caves mini-boss.
            CaveEnemyKit.SetDrops(root.AddComponent<ScrapDropper>(), (ScrapType.ScrapMetal, 3, 5), (ScrapType.Wiring, 1, 2), (ScrapType.CircuitBoards, 1, 2));
            return DesertDuelBossBuilder.Save(root, PrefabPath);
        }
    }
}
