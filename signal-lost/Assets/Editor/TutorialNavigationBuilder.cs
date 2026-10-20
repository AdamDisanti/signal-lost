using System;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.Editor
{
    public static class TutorialNavigationBuilder
    {
        [MenuItem("Signal Lost/Tutorial Crash Site/Rebake Tutorial Navigation")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before baking navigation.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != TutorialCrashSiteBuilder.ScenePath)
                throw new InvalidOperationException("Open Tutorial.unity before rebaking its navigation.");
            var terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
            var surface = GameObject.Find("Tutorial Encounter Navigation")?.GetComponent<NavMeshSurface>();
            if (terrain == null || surface == null || surface.navMeshData == null)
                throw new InvalidOperationException("Tutorial terrain or saved navigation is missing.");
            var collider = terrain.GetComponent<TerrainCollider>();
            if (collider == null || !collider.enabled || collider.terrainData != terrain.terrainData)
                throw new InvalidOperationException("Terrain collider must be enabled and match the visible terrain.");

            // Agents follow the baked surface, not physics gravity: rebake whenever the terrain changes.
            NavMeshData saved = surface.navMeshData;
            surface.RemoveData();
            surface.transform.position = terrain.transform.position + terrain.terrainData.size * 0.5f;
            surface.center = Vector3.zero;
            surface.size = terrain.terrainData.size + Vector3.up * 10f;
            surface.collectObjects = CollectObjects.Volume;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.buildHeightMesh = true;
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            NavMeshData rebuilt = surface.navMeshData;
            if (rebuilt == null || NavMesh.CalculateTriangulation().vertices.Length == 0)
                throw new InvalidOperationException("Tutorial navigation bake produced no walkable surface.");
            surface.RemoveData();
            EditorUtility.CopySerialized(rebuilt, saved);
            surface.navMeshData = saved;
            surface.AddData();
            UnityEngine.Object.DestroyImmediate(rebuilt);
            EditorUtility.SetDirty(saved);
            EditorUtility.SetDirty(surface);
            foreach (var agent in UnityEngine.Object.FindObjectsByType<NavMeshAgent>())
            {
                Vector3 ground = agent.transform.position;
                ground.y = terrain.SampleHeight(ground) + terrain.transform.position.y;
                var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                if (!NavMesh.SamplePosition(ground, out var hit, 3f, filter) ||
                    hit.position.y < terrain.SampleHeight(hit.position) + terrain.transform.position.y - 0.3f)
                    throw new InvalidOperationException("No current-terrain spawn for " + agent.name);
                Undo.RecordObject(agent.transform, "Ground tutorial enemy");
                agent.transform.position = hit.position;
                PrefabUtility.RecordPrefabInstancePropertyModifications(agent.transform);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save tutorial navigation.");
            Debug.Log("TUTORIAL_NAVIGATION_REBUILT");
        }
    }
}
