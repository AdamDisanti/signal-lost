using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Core;
using SignalLost.Effects;
using SignalLost.Enemies;
using SignalLost.Enemies.Caves;
using SignalLost.Enemies.Desert;
using SignalLost.Inventory;
using SignalLost.Player;
using SignalLost.Progression;
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
    /// Builds the Cave Axolotl (Crystal Caves stage boss, docs/cave-axolotl.md) the way the Shard Mole is built: from the
    /// Blender model and clips made by tools/blender/cave_bosses.py in Assets/Prefabs/Enemies/Caves/Bosses/CaveAxolotl/,
    /// with the cave's own crystal material on its crystals, bone hitboxes, gill weak spots, crystal lights, scrap and
    /// the hidden Flight Control Module. The test scene has its chamber, its lair (the entrance) and the player with
    /// the Crystal Disruptor already installed.
    /// Menu: Signal Lost > Crystal Caves Enemies > Cave Axolotl. Its test scene is Scenes/Bosses/CaveAxolotlTest.unity.
    /// </summary>
    public static class CaveAxolotlBuilder
    {
        public const string Root = ShardMoleBuilder.Root;
        public const string PrefabPath = Root + "/CaveAxolotl/CaveAxolotl.prefab";
        public const string ScenePath = "Assets/Scenes/Bosses/CaveAxolotlTest.unity";
        public const float ArenaRadius = 20f;
        private static readonly Vector3 Home = new Vector3(0f, 0f, 10f);

        [MenuItem("Signal Lost/Crystal Caves Enemies/Cave Axolotl/Build Prefab")]
        public static void BuildPrefabMenu()
        {
            BuildPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("CAVE_AXOLOTL_PREFAB_BUILT");
        }

        [MenuItem("Signal Lost/Crystal Caves Enemies/Cave Axolotl/Create Test Scene")]
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
            Transform room = CaveEnemyKit.BuildCaveRoom("Cave Axolotl Test Cave", 60f, 84f, 14f);
            // Its chamber: a ring of crystal batches in all six cave colours (it hides beside them and copies their
            // colour), with a few rocks for cover.
            var colours = new[] { CrystalColor.Pink, CrystalColor.Cyan, CrystalColor.Purple, CrystalColor.Green, CrystalColor.Orange, CrystalColor.DarkBlue };
            for (int i = 0; i < 12; i++)
            {
                float angle = i * 30f + 15f;
                Vector3 at = Home + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * (ArenaRadius + 3f);
                CaveEnemyKit.PlaceCrystalBatch(room, at, angle + 180f, 0.85f + 0.12f * (i % 3), colours[i % colours.Length]);
            }
            CaveEnemyKit.PlaceRock(room, "Stalagmite", Home + new Vector3(-11f, 0f, 3f), 0f, new Vector3(0.8f, 3f, 0.8f));
            CaveEnemyKit.PlaceRock(room, "CaveBoulder", Home + new Vector3(12f, 0.4f, -4f), 40f, new Vector3(1.6f, 1.2f, 1.4f));
            CaveEnemyKit.PlaceRock(room, "CaveBoulderRound", Home + new Vector3(-5f, 0.4f, -13f), 0f, new Vector3(1.3f, 1.1f, 1.3f));
            CaveEnemyKit.PlaceRock(room, "CaveBoulder", Home + new Vector3(6f, 0.4f, 13f), 110f, new Vector3(1.4f, 1.1f, 1.3f));

            var axolotl = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            axolotl.name = "Cave Axolotl";
            axolotl.transform.SetPositionAndRotation(Home, Quaternion.Euler(0f, 180f, 0f));

            // The lair: walking in here starts the entrance. The test scene assumes the mini-boss parts are collected.
            var lair = new GameObject("Axolotl Lair").AddComponent<AxolotlLair>();
            lair.transform.position = Home + new Vector3(0f, 0f, -18f);
            var lairData = new SerializedObject(lair);
            lairData.FindProperty("axolotl").objectReferenceValue = axolotl.GetComponent<CaveAxolotl>();
            lairData.FindProperty("assumePartsCollected").boolValue = true;
            lairData.ApplyModifiedPropertiesWithoutUndo();

            GameObject player = CaveEnemyKit.AddCorePlayer(scene, new Vector3(0f, 0.05f, -34f), 0f);
            var loadout = new SerializedObject(player.AddComponent<StartingUpgrades>());
            SerializedProperty ids = loadout.FindProperty("upgradeIds");
            ids.arraySize = 1;
            ids.GetArrayElementAtIndex(0).stringValue = "crystal_disruptor";
            loadout.ApplyModifiedPropertiesWithoutUndo();
            new GameObject("Mini-Boss HUD").AddComponent<MiniBossHud>();
            CaveEnemyKit.CaveLight();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("CAVE_AXOLOTL_TEST_SCENE_CREATED_AND_VALIDATED");
        }

        [MenuItem("Signal Lost/Crystal Caves Enemies/Cave Axolotl/Validate")]
        public static void Validate()
        {
            GameObject prefab = CaveEnemyKit.Load(PrefabPath);
            Require(prefab != null, PrefabPath + " is missing (Build Prefab).");
            var boss = prefab.GetComponent<CaveAxolotl>();
            Require(boss != null && prefab.GetComponent<EnemySignalSource>() != null, "The axolotl needs CaveAxolotl and an EnemySignalSource.");
            Require(prefab.GetComponent<ScrapDropper>() != null, "The axolotl must drop scrap (ScrapDropper).");
            Require(!prefab.GetComponent<NavMeshAgent>().enabled, "The NavMeshAgent must start disabled (DesertFodder enables it).");
            var data = new SerializedObject(boss);
            foreach (string field in new[] { "visual", "clipTable", "animator", "crystalMaterial", "body", "disguisePrefab", "mainPart",
                         "spireMesh", "markerMaterial", "puffMesh", "spikeMesh" })
                Require(data.FindProperty(field).objectReferenceValue != null, "CaveAxolotl." + field + " is not set.");
            Require(data.FindProperty("shipPart").objectReferenceValue == null, "The Flight Control Module is a main part (mainPart), not a mini-boss ship part.");
            Require(data.FindProperty("phaseAt").arraySize == 3, "The axolotl has four phases.");
            Require(data.FindProperty("gills").arraySize == 2, "The axolotl needs a gill weak spot on each side.");
            Require(data.FindProperty("crystalLights").arraySize >= 1, "The axolotl needs its crystal lights.");
            Require(!data.FindProperty("fodderWhileEngaged").boolValue, "The axolotl fights alone.");
            var table = BossClipTable.Parse((TextAsset)data.FindProperty("clipTable").objectReferenceValue);
            foreach (string clip in new[] { "Idle", "Walk", "Run", "Roar", "TailSwipe", "RollStart", "Roll", "RollEnd", "RollCrash", "ShardRing",
                         "BurrowIn", "BurrowOut", "Stagger", "Death" })
                Require(table.Has(clip), "CaveAxolotlClips.json has no " + clip + " clip.");
            var body = (SkinnedMeshRenderer)data.FindProperty("body").objectReferenceValue;
            Require(System.Array.IndexOf(body.sharedMaterials, (Material)data.FindProperty("crystalMaterial").objectReferenceValue) >= 0,
                "The body must wear the crystal material (the slot it recolours).");
            Require(prefab.GetComponentsInChildren<SphereCollider>(true).Length >= 10, "The axolotl needs its bone hitboxes.");
            foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                    Require(m != null, "The axolotl has a missing material on " + r.name + ".");

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;
            EditorSceneManager.OpenScene(ScenePath);
            Require(Count<FirstPersonController>() == 1 && Count<PlayerGear>() == 1, ScenePath + ": expected one shared Player.");
            Require(Count<GameSystems>() == 1 && Count<PlayerSpawnPoint>() >= 1, ScenePath + ": expected GameSystems and a spawn point.");
            Require(Count<Camera>() == 1 && Count<AudioListener>() == 1, ScenePath + ": expected one camera and listener.");
            Require(Count<NavMeshSurface>() == 1 && Count<NavMeshBaker>() == 1, ScenePath + ": expected one NavMeshSurface with a NavMeshBaker.");
            Require(Count<CaveAxolotl>() == 1 && Count<AxolotlLair>() == 1 && Count<MiniBossHud>() == 1,
                ScenePath + ": expected one Cave Axolotl, its lair and the mini-boss HUD.");
            Require(Count<CrystalBatch>() >= 6, ScenePath + ": the chamber needs crystal batches to hide beside.");
            StartingUpgrades loadout = All<StartingUpgrades>().Count == 1 ? All<StartingUpgrades>()[0] : null;
            SerializedProperty ids = loadout != null ? new SerializedObject(loadout).FindProperty("upgradeIds") : null;
            Require(ids != null && ids.arraySize == 1 && ids.GetArrayElementAtIndex(0).stringValue == "crystal_disruptor",
                ScenePath + ": the player should start with the Crystal Disruptor (StartingUpgrades).");
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0, ScenePath + ": missing script on " + item.name);
            Debug.Log("CAVE_AXOLOTL_VALIDATED");
        }

        internal static GameObject BuildPrefab()
        {
            CaveEnemyKit.EnsureFolders();
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder(CaveEnemyKit.PrefabFolder, "Bosses");
            Material crystal = CrystalAssetBuilder.CrystalMaterial(CrystalColor.Pink);
            if (crystal == null)
            {
                CrystalAssetBuilder.Build();
                crystal = CrystalAssetBuilder.CrystalMaterial(CrystalColor.Pink);
            }

            GameObject root = DesertDuelBossBuilder.Begin("CaveAxolotl", out Transform model, out BossClipTable table, out _, Root,
                mats => mats["CaveAxolotlCrystal"] = crystal); // its crystals are the cave's own (it recolours them as it hides)
            var boss = root.AddComponent<CaveAxolotl>();

            Transform part = DesertDuelBossBuilder.HiddenPart(root, model, "Spine", "Flight Control Module", go =>
            {
                Material casing = CaveEnemyKit.Lit("AxolotlModuleCasing", new Color(0.62f, 0.64f, 0.68f), 0.75f, 0.55f);
                Material dark = CaveEnemyKit.Lit("AxolotlModuleDark", new Color(0.22f, 0.23f, 0.27f), 0.6f, 0.4f);
                Material screen = CaveEnemyKit.Glowing("AxolotlModuleScreen", new Color(0.35f, 0.85f, 1f), 2.2f);
                Mesh puck = CaveEnemyKit.SaveMesh(LowPolyMesh.Lathe("AxolotlModuleBody", new[]
                {
                    new Vector2(0f, 0f), new Vector2(0.42f, 0f), new Vector2(0.48f, 0.12f), new Vector2(0.44f, 0.3f), new Vector2(0.3f, 0.36f), new Vector2(0f, 0.36f),
                }, 8));
                Mesh fin = CaveEnemyKit.SaveMesh(LowPolyMesh.TaperedPrism("AxolotlModuleFin", 4, 0.3f));
                Mesh gem = CaveEnemyKit.SaveMesh(LowPolyMesh.Icosphere("AxolotlModuleGem", 0));
                DesertDuelBossBuilder.AddMesh(go, puck, casing, Vector3.zero, Quaternion.identity, Vector3.one);
                DesertDuelBossBuilder.AddMesh(go, gem, screen, Vector3.up * 0.38f, Quaternion.identity, new Vector3(0.22f, 0.08f, 0.22f));
                for (int i = 0; i < 4; i++)
                    DesertDuelBossBuilder.AddMesh(go, fin, dark, Quaternion.Euler(0f, 45f + i * 90f, 0f) * Vector3.forward * 0.42f + Vector3.up * 0.16f,
                        Quaternion.Euler(0f, 45f + i * 90f, 0f), new Vector3(0.05f, 0.22f, 0.28f));
            });

            Transform head = DesertDuelBossBuilder.FindDeep(model, "Head");
            DesertDuelBossBuilder.Hitboxes(model, new[]
            {
                new Hitbox("Hips", 1.0f), new Hitbox("Spine", 1.05f), new Hitbox("Chest", 1.0f), new Hitbox("Head", 0.8f),
                new Hitbox("Jaw", 0.6f), new Hitbox("Tail1", 0.5f), new Hitbox("Tail2", 0.42f), new Hitbox("Tail3", 0.35f),
                new Hitbox("Tail4", 0.28f), new Hitbox("ForearmL", 0.3f), new Hitbox("ForearmR", 0.3f), new Hitbox("ShinL", 0.3f),
                new Hitbox("ShinR", 0.3f),
            });
            // The bone pivots sit at the back of the wide head: one more sphere for the front of it.
            var snout = new GameObject("Hitbox Snout");
            snout.transform.SetParent(head, true);
            snout.transform.position = head.position + root.transform.forward * 1.0f + Vector3.down * 0.1f;
            snout.AddComponent<SphereCollider>().radius = 0.7f / Mathf.Max(0.0001f, snout.transform.lossyScale.x);

            // The gills: three feathery stalks a side, the weak spot.
            var gills = new List<Health>();
            foreach (string side in new[] { "L", "R" })
            {
                Transform gill = DesertDuelBossBuilder.FindDeep(model, "Gill" + side);
                Vector3 outward = gill.position - head.position;
                outward.y = 0f;
                gills.Add(DesertDuelBossBuilder.WeakSpot(model, "Gill" + side, null, 0f, 0.6f, "Cave Axolotl (gills " + side + ")",
                    outward.normalized * 0.45f + Vector3.up * 0.15f));
            }

            // Its crystals glow like a batch: a light on its back and one on its tail.
            Color glow = CrystalAssetBuilder.Glow(CrystalColor.Pink);
            var lights = new List<Light>();
            foreach ((string bone, float range, float intensity) in new[] { ("Spine", 9f, 2.2f), ("Tail2", 6f, 1.4f) })
            {
                Light light = CrystalAssetBuilder.AddGlow(DesertDuelBossBuilder.FindDeep(model, bone), Vector3.up * 1f, range, intensity);
                light.color = glow;
                lights.Add(light);
            }

            var data = DesertDuelBossBuilder.Common(boss, root, model, table, "The Cave Axolotl", CaveAxolotl.MainPart, null, 1200f, 2.6f, 6f,
                new[] { 0.75f, 0.5f, 0.25f }, new[] { "Still Pool", "Crystal Bloom", "Cave-In", "Frenzy" }, new[] { "Chest", "Head" }, 1.4f, Root);
            data.FindProperty("arenaRadius").floatValue = ArenaRadius;
            data.FindProperty("patrolRadius").floatValue = 0f; // it waits at home under its crystal
            data.FindProperty("poise").floatValue = 140f;
            data.FindProperty("stalkDistance").floatValue = 6f;
            SetFloats(data.FindProperty("phaseTempo"), 1f, 1.08f, 1.16f, 1.28f);
            SetFloats(data.FindProperty("phaseBreather"), 1.1f, 0.85f, 0.6f, 0.35f);
            data.FindProperty("markerMaterial").objectReferenceValue = CaveEnemyKit.Glowing("AxolotlWarning", glow, 2.2f);
            data.FindProperty("crystalMaterial").objectReferenceValue = crystal;
            data.FindProperty("body").objectReferenceValue = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            data.FindProperty("disguisePrefab").objectReferenceValue = CaveEnemyKit.Load(CrystalAssetBuilder.BatchPath);
            data.FindProperty("mainPart").objectReferenceValue = part;
            data.FindProperty("spireMesh").objectReferenceValue = CaveEnemyKit.CrystalMesh("CrystalSpire");
            SetObjects(data.FindProperty("gills"), gills.ToArray());
            SetObjects(data.FindProperty("crystalLights"), lights.ToArray());
            data.ApplyModifiedPropertiesWithoutUndo();

            // A stage boss: more scrap than the cave mini-bosses.
            CaveEnemyKit.SetDrops(root.AddComponent<ScrapDropper>(), (ScrapType.ScrapMetal, 5, 8), (ScrapType.Wiring, 2, 3), (ScrapType.CircuitBoards, 2, 3));
            return DesertDuelBossBuilder.Save(root, PrefabPath);
        }

        private static void SetFloats(SerializedProperty list, params float[] values)
        {
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).floatValue = values[i];
        }

        private static void SetObjects(SerializedProperty list, Object[] values)
        {
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
