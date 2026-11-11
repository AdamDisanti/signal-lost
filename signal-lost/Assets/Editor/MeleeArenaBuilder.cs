using System;
using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Enemies;
using SignalLost.Finale;
using SignalLost.Inventory;
using SignalLost.Melee;
using SignalLost.Player;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SignalLost.Editor
{
    /// <summary>
    /// Builds the melee test arena (Adam, 11-10; docs/melee-rage.md): the early Enemy Test box (floor, low walls, cover,
    /// a pillar and a platform with a ramp) made about twice as big, with a tower and some steps to jump off for the
    /// Moon Shoes and the jump smash, the current player with end-game gear (<see cref="FinaleLoadout"/>: every knuckle
    /// and shoe tier), and <see cref="MeleeArena"/> spawning the finale's robot creatures. The NavMesh is built when the
    /// scene starts, as in the Enemy Test scene. Rebuilding replaces the scene.
    /// Menu: Signal Lost > Melee > Build Melee Arena. Batch: SignalLost.Editor.MeleeArenaBuilder.BuildBatch.
    /// </summary>
    public static class MeleeArenaBuilder
    {
        public const string ScenePath = "Assets/Scenes/Initial Test Scenes/MeleeArena.unity";
        private const float Half = 30f;


        [MenuItem("Signal Lost/Melee/Build Melee Arena")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                !EditorUtility.DisplayDialog("Rebuild the melee arena?", "This replaces " + ScenePath + ", including any hand edits.", "Rebuild", "Cancel"))
                return;
            Build();
        }

        public static void BuildBatch()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Transform arena = BuildBox();
            BuildLighting();
            BuildGameplay(arena);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("MELEE_ARENA_BUILT");
        }

        // The Enemy Test box at about twice the size: 60 x 60 m, walls the player can't jump, the same cover pieces
        // spread out, plus steps and a tower for big jumps.
        private static Transform BuildBox()
        {
            Material floor = EnemyTestBuilder.ExistingOrLit("Assets/Materials/Combat/CombatFloor.mat", "ArenaFloor", new Color(0.32f, 0.36f, 0.4f));
            Material wall = EnemyTestBuilder.ExistingOrLit("Assets/Materials/Combat/CombatWall.mat", "ArenaWall", new Color(0.52f, 0.55f, 0.58f));
            Material cover = EnemyTestBuilder.ExistingOrLit("Assets/Materials/Combat/CombatCover.mat", "ArenaCover", new Color(0.78f, 0.49f, 0.23f));

            var root = new GameObject("Melee Arena Box");
            var surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            root.AddComponent<NavMeshBaker>();
            Transform a = root.transform;

            float size = Half * 2f;
            EnemyTestBuilder.Block("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(size, 1f, size), floor, a);
            EnemyTestBuilder.Block("North wall", new Vector3(0f, 2f, Half), new Vector3(size + 1f, 4f, 1f), wall, a);
            EnemyTestBuilder.Block("South wall", new Vector3(0f, 2f, -Half), new Vector3(size + 1f, 4f, 1f), wall, a);
            EnemyTestBuilder.Block("East wall", new Vector3(Half, 2f, 0f), new Vector3(1f, 4f, size), wall, a);
            EnemyTestBuilder.Block("West wall", new Vector3(-Half, 2f, 0f), new Vector3(1f, 4f, size), wall, a);

            // The Enemy Test pieces, scaled out from the middle.
            EnemyTestBuilder.Block("Cover block", new Vector3(10f, 0.6f, -2f), new Vector3(4f, 1.2f, 1.2f), cover, a);
            EnemyTestBuilder.Block("Crate stack", new Vector3(-12f, 0.75f, 4f), new Vector3(2f, 1.5f, 2f), cover, a);
            EnemyTestBuilder.Block("Pillar", new Vector3(6f, 1.75f, 16f), new Vector3(1.4f, 3.5f, 1.4f), wall, a);
            EnemyTestBuilder.Block("Platform", new Vector3(20f, 0.75f, 18f), new Vector3(7f, 1.5f, 7f), wall, a);
            GameObject ramp = EnemyTestBuilder.Block("Ramp", new Vector3(20f, 0.72f, 11.2f), new Vector3(3f, 0.25f, 6.4f), cover, a);
            ramp.transform.rotation = Quaternion.Euler(-13.5f, 0f, 0f);
            EnemyTestBuilder.Block("Cover block 2", new Vector3(-8f, 0.6f, -14f), new Vector3(1.2f, 1.2f, 4f), cover, a);
            EnemyTestBuilder.Block("Pillar 2", new Vector3(-18f, 1.75f, -8f), new Vector3(1.4f, 3.5f, 1.4f), wall, a);

            // New: steps up to a tower (about 6 m) for jump smashes and the Moon Shoes.
            for (int i = 0; i < 4; i++)
                EnemyTestBuilder.Block("Step " + (i + 1), new Vector3(-20f + i * 2.4f, 0.6f + i * 0.6f, 18f), new Vector3(2.4f, 1.2f + i * 1.2f, 3f), wall, a);
            EnemyTestBuilder.Block("Tower", new Vector3(-10.6f, 3f, 18f), new Vector3(3f, 6f, 3f), wall, a);
            return a;
        }

        private static void BuildLighting()
        {
            var sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.65f);
        }

        private static void BuildGameplay(Transform box)
        {
            Vector3 start = new Vector3(0f, 0.05f, -20f);
            var spawn = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CorePrefabBuilder.SpawnPath));
            spawn.transform.SetPositionAndRotation(start, Quaternion.identity);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CorePrefabBuilder.PlayerPath));
            player.transform.SetPositionAndRotation(start, Quaternion.identity);
            var gear = new SerializedObject(player.GetComponent<PlayerGear>());
            SerializedProperty items = gear.FindProperty("startingItems");
            PlayerItem[] all = { PlayerItem.MiningLaser, PlayerItem.Scanner, PlayerItem.Flask };
            items.arraySize = all.Length;
            for (int i = 0; i < all.Length; i++) items.GetArrayElementAtIndex(i).enumValueIndex = (int)all[i];
            gear.FindProperty("introduceItems").boolValue = false;
            gear.ApplyModifiedPropertiesWithoutUndo();
            player.AddComponent<FinaleLoadout>(); // end-game gear: every upgrade tier, the knuckles and shoes included
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CorePrefabBuilder.SystemsPath));

            var arena = new GameObject("Melee Arena");
            arena.transform.position = Vector3.zero;
            var zone = arena.AddComponent<CombatZone>();
            zone.Radius = Half * 1.45f; // reaches the corners
            var melee = arena.AddComponent<MeleeArena>();
            var data = new SerializedObject(melee);
            var adds = new List<Object>();
            foreach (string path in FinaleBuilder.AddPaths) // the finale's horde, weighted the same way
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) adds.Add(prefab);
                else Debug.LogWarning("Melee arena: missing add prefab " + path);
            }
            SerializedProperty list = data.FindProperty("addPrefabs");
            list.arraySize = adds.Count;
            for (int i = 0; i < adds.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = adds[i];
            data.FindProperty("halfSize").floatValue = Half - 3f;
            data.FindProperty("robotSources.chiefToad").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Meadow/ChiefToad.prefab");
            data.FindProperty("robotSources.oldBuzzard").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Desert/MiniBosses/OldBuzzard.prefab");
            data.FindProperty("robotSources.burnout").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Desert/MiniBosses/Burnout.prefab");
            data.FindProperty("robotSources.coreworm").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(DesertBossBuilder.WormPath);
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
