using System;
using System.IO;
using SignalLost.Combat;
using SignalLost.Enemies;
using SignalLost.Player;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Copy into Assets/Editor of a disposable project to author this integration using Unity serialization.
public static class TutorialEnemiesSetup
{
    public const string ScenePath = "Assets/Scenes/Tutorial Testing.unity";
    public const string NavPath = "Assets/Prefabs/Enviornment objects/TutorialCrashSite/Gameplay/TutorialEncounterNavMesh.asset";

    public static void Create()
    {
        RequireTemp();
        EditorSceneManager.OpenScene(ScenePath);
        if (GameObject.Find("Tutorial Enemies") != null) throw new InvalidOperationException("Tutorial enemies already installed.");
        var player = UnityEngine.Object.FindFirstObjectByType<TutorialPlayer>();
        var terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
        var basin = GameObject.Find("BurntRockBasin");
        var targets = new SerializedObject(player).FindProperty("enemies").objectReferenceValue as GameObject;
        if (terrain == null || basin == null || targets == null) throw new InvalidOperationException("Tutorial setup missing.");
        Vector3 forward = Vector3.ProjectOnPlane(basin.transform.position - player.transform.position, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        Vector3 centre = basin.transform.position + forward * 36;
        centre.y = terrain.SampleHeight(centre) + terrain.transform.position.y;
        var navigation = new GameObject("Tutorial Encounter Navigation");
        navigation.transform.SetParent(targets.transform.parent, true);
        navigation.transform.position = centre;
        var surface = navigation.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Volume;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.size = new Vector3(100, 40, 100);
        surface.center = Vector3.zero;
        surface.overrideVoxelSize = true;
        surface.voxelSize = 0.15f;
        surface.BuildNavMesh();
        if (surface.navMeshData == null || NavMesh.CalculateTriangulation().vertices.Length == 0)
            throw new InvalidOperationException("NavMesh bake failed.");
        AssetDatabase.CreateAsset(surface.navMeshData, NavPath);
        var group = new GameObject("Tutorial Enemies").transform;
        group.SetParent(targets.transform, false);
        Place("Assets/Prefabs/Enemies/HermitCrab.prefab", "Tutorial Hermit Crab", centre - side * 6 - forward * 3, terrain, group, player.transform.position);
        Place("Assets/Prefabs/Enemies/MortarFrog.prefab", "Tutorial Mortar Frog", centre + side * 6 + forward * 8, terrain, group, player.transform.position);
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(player.gameObject.scene);
        Debug.Log("TUTORIAL_ENEMIES_CREATED");
    }

    private static void Place(string path, string name, Vector3 desired, Terrain terrain, Transform parent, Vector3 spawn)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("Missing prefab: " + path);
        for (int i = 0; i < 120; i++)
        {
            Vector3 position = desired + new Vector3(Mathf.Cos(i * 2.4f), 0, Mathf.Sin(i * 2.4f)) * Mathf.Sqrt(i) * 0.6f;
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
            if (Vector3.Distance(position, spawn) < 25) continue;
            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) continue;
            if (Mathf.Abs(hit.position.y - position.y) > 0.5f) continue;
            Physics.SyncTransforms();
            bool blocked = false;
            foreach (var col in Physics.OverlapCapsule(hit.position + Vector3.up * 0.8f, hit.position + Vector3.up * 1.5f, 0.7f, ~0, QueryTriggerInteraction.Ignore))
                if (!(col is TerrainCollider)) blocked = true;
            if (blocked) continue;
            // The encounter must connect to the pickup area on the same NavMesh.
            Vector3 approach = GameObject.Find("BurntRockBasin").transform.position;
            approach.y = terrain.SampleHeight(approach) + terrain.transform.position.y;
            if (!NavMesh.SamplePosition(approach, out NavMeshHit start, 6, NavMesh.AllAreas)) continue;
            var route = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, hit.position, NavMesh.AllAreas, route) || route.status != NavMeshPathStatus.PathComplete) continue;
            var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            enemy.name = name;
            enemy.transform.SetPositionAndRotation(hit.position, Quaternion.LookRotation(Vector3.ProjectOnPlane(spawn - hit.position, Vector3.up)));
            var brain = (UnityEngine.Object)enemy.GetComponent<HermitCrab>() ?? enemy.GetComponent<MortarFrog>();
            var data = new SerializedObject(brain);
            data.FindProperty("respawnDelay").floatValue = 0;
            data.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log(name + " placed at " + hit.position + "; complete route from pickup area.");
            return;
        }
        throw new InvalidOperationException("No reachable clear ground for " + name);
    }

    public static void RequireTemp()
    {
        var project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        if (!Application.isBatchMode || !(project.StartsWith(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || project.StartsWith("/private/tmp/", StringComparison.Ordinal) || project.StartsWith("/tmp/", StringComparison.Ordinal)))
            throw new InvalidOperationException("Run in a disposable batch-mode project under the system temporary directory.");
    }
}
