using System;
using System.Collections;
using System.IO;
using SignalLost.Combat;
using SignalLost.Player;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Copy only into Assets/Editor of a disposable project under /tmp.
[InitializeOnLoad]
public static class TutorialTerrainSmoke
{
    private const string Key = "SignalLost.TutorialTerrainSmoke";
    private static IEnumerator run;
    private static double deadline;
    private static int frame = -1;
    static TutorialTerrainSmoke() { EditorApplication.update += Tick; }

    public static void Start()
    {
        TutorialEnemiesSetup.RequireTemp();
        EditorSceneManager.OpenScene("Assets/Scenes/Tutorial.unity");
        var surface = GameObject.Find("Tutorial Encounter Navigation").GetComponent<NavMeshSurface>();
        var terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
        var collider = terrain.GetComponent<TerrainCollider>();
        Check(collider.enabled && collider.terrainData == terrain.terrainData, "Terrain collision matches visible ground.");
        Check(surface.size.x >= terrain.terrainData.size.x && surface.size.z >= terrain.terrainData.size.z,
            "Navigation covers the expanded terrain, not the old encounter volume.");
        Physics.SyncTransforms();
        int samples = 0;
        for (float x = -170; x <= 170; x += 5) for (float z = -170; z <= 170; z += 5)
        {
            Vector3 p = new Vector3(x, 0, z);
            p.y = Ground(terrain, p);
            if (!NavMesh.SamplePosition(p, out var hit, 0.8f, NavMesh.AllAreas)) continue;
            // Ignore prop tops: the regression is a stale surface beneath terrain.
            Check(hit.position.y >= Ground(terrain, hit.position) - 0.3f, "No walkable mesh buried beneath terrain at " + hit.position);
            samples++;
        }
        Check(samples > 2000, "Thousands of current-terrain navigation samples are available.");
        Debug.Log("TUTORIAL_TERRAIN_EDIT_CHECKS_PASS samples=" + samples);
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 120;
        if (EditorApplication.timeSinceStartup > deadline) { Fail(new Exception("Terrain test timed out.")); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            run ??= Run();
            if (run.MoveNext()) return;
            SessionState.SetBool(Key, false);
            Debug.Log("TUTORIAL_TERRAIN_PLAY_MODE_PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static IEnumerator Run()
    {
        yield return null;
        var terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
        var player = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
        player.enabled = false;
        var agents = UnityEngine.Object.FindObjectsByType<NavMeshAgent>();
        Check(agents.Length >= 3, "Crab, frog and merchant exist.");
        foreach (var agent in agents)
        {
            // The protected merchant's combat brain/agent stay off until the quest starts.
            if (!agent.enabled)
            {
                agent.enabled = true;
                Check(agent.Warp(agent.transform.position), agent.name + " binds when activated.");
            }
            Check(agent.isOnNavMesh, agent.name + " starts bound to navigation.");
            Check(agent.transform.position.y >= Ground(terrain, agent.transform.position) - 0.3f, agent.name + " starts above ground.");
            // Remove AI decisions, then exercise actual agent movement across the reshaped terrain.
            foreach (var brain in agent.GetComponents<MonoBehaviour>())
                if (!(brain is Health)) brain.enabled = false;
            agent.isStopped = false;
            agent.speed = 15;
            Vector3 goal = new Vector3(-65, 0, 65);
            goal.y = Ground(terrain, goal);
            Check(NavMesh.SamplePosition(goal, out var target, 3, NavMesh.AllAreas), "Outer lunar destination exists.");
            var path = new NavMeshPath();
            Check(agent.CalculatePath(target.position, path) && path.status == NavMeshPathStatus.PathComplete,
                agent.name + " can reach outer lunar terrain.");
            agent.SetPath(path);
        }
        float until = Time.time + 25f;
        while (Time.time < until)
        {
            foreach (var agent in agents)
                Check(agent.transform.position.y >= Ground(terrain, agent.transform.position) - 0.3f,
                    agent.name + " stays above terrain while walking: " + agent.transform.position);
            yield return null;
        }
        foreach (var agent in agents)
            Check(!agent.pathPending && agent.remainingDistance < 2, agent.name + " arrived on new terrain.");
    }
    private static float Ground(Terrain t, Vector3 p) => t.transform.position.y + t.SampleHeight(p);
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Fail(Exception ex)
    {
        SessionState.SetBool(Key, false);
        Debug.LogError(ex);
        EditorApplication.Exit(1);
    }
}
