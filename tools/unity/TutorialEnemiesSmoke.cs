using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using SignalLost.Combat;
using SignalLost.Enemies;
using SignalLost.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Install only in Assets/Editor of a disposable temp project; never in the real project.
[InitializeOnLoad]
public static class TutorialEnemiesSmoke
{
    private const string Key = "SignalLost.TutorialEnemiesSmoke";
    private static IEnumerator run;
    private static float resume;
    private static int frame = -1;
    private static readonly List<string> report = new List<string>();
    private static TutorialPlayer player;
    private static CharacterController body;
    private static Terrain terrain;
    private static readonly Dictionary<string, Vector3> homes = new Dictionary<string, Vector3>();

    static TutorialEnemiesSmoke()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception) &&
                !trace.Contains("UnityEditor.Search.SearchDatabase")) Fail(new Exception(message + "\n" + trace));
        };
    }

    public static void Start()
    {
        TutorialEnemiesSetup.RequireTemp();
        EditorSceneManager.OpenScene(TutorialEnemiesSetup.ScenePath);
        foreach (var dummy in UnityEngine.Object.FindObjectsByType<TrainingDummy>())
            homes.Add(dummy.name, dummy.transform.position);
        Check(homes.Count == 3, "Three existing dummies in authored scene.");
        // Preserve home positions across the domain reload.
        foreach (var item in homes)
        { SessionState.SetString(Key + item.Key, JsonUtility.ToJson(new Position { value = item.Value })); }
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + "Start", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }

    [Serializable] private class Position { public Vector3 value; }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120)
        { Fail(new Exception("Enemy integration test timed out.")); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < resume || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            run ??= Run();
            if (run.MoveNext()) { resume = Time.time + (run.Current is float seconds ? seconds : 0); return; }
            SessionState.SetBool(Key, false);
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/TutorialEnemiesSmoke.txt", report);
            Debug.Log("TUTORIAL_ENEMIES_PLAY_MODE_PASS\n" + string.Join("\n", report));
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static IEnumerator Run()
    {
        yield return 0.5f;
        player = UnityEngine.Object.FindFirstObjectByType<TutorialPlayer>();
        body = player.GetComponent<CharacterController>();
        terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
        var movement = player.GetComponent<FirstPersonController>();
        movement.enabled = false; // Batch mode has no physical input/focus.
        var crab = UnityEngine.Object.FindFirstObjectByType<HermitCrab>();
        var frog = UnityEngine.Object.FindFirstObjectByType<MortarFrog>();
        Check(crab != null && frog != null, "One active crab and frog initialize in tutorial.");
        Check(crab.GetComponent<NavMeshAgent>().isOnNavMesh && frog.GetComponent<NavMeshAgent>().isOnNavMesh, "Both enemies bind to the authored NavMesh.");
        Check(Vector3.Distance(player.transform.position, crab.transform.position) > 25 && Vector3.Distance(player.transform.position, frog.transform.position) > 25,
            "Player starts outside both enemy detection ranges.");
        Check(!player.HasLaser && !player.HasFlask && player.Health.Current == 100, "Unarmed tutorial start and player health unchanged.");
        CheckDummies();
        var crabHome = Get<Vector3>(crab, "home"); var frogHome = Get<Vector3>(frog, "home");
        var spawn = Get<Vector3>(player, "spawnPosition");

        foreach (var pickup in UnityEngine.Object.FindObjectsByType<TutorialPickup>())
            Check(pickup.TryCollect(player), "Existing pickup collects: " + pickup.name);
        Check(player.HasLaser && player.HasFlask, "Existing laser/flask can be equipped.");
        // Exercise the real crab attack on the tutorial Health, with the frog kept idle.
        frog.enabled = false;
        PlacePlayer(crab.transform.position + crab.transform.forward * 1.15f);
        float before = player.Health.Current;
        for (int i = 0; i < 100 && player.Health.Current >= before; i++) yield return 0.1f;
        Check(player.Health.Current < before && !player.Health.IsDead, "Crab detects and damages tutorial player with its own AI.");
        crab.enabled = false; crab.GetComponent<NavMeshAgent>().isStopped = true;
        player.Health.Restore();
        frog.enabled = true;
        // Stand within mortar range and let the real frog aim/fire.
        PlacePlayer(frog.transform.position + frog.transform.forward * 9);
        before = player.Health.Current;
        bool sawBlob = false;
        for (int i = 0; i < 180 && player.Health.Current >= before; i++)
        {
            sawBlob |= UnityEngine.Object.FindObjectsByType<GooBlob>().Length > 0;
            yield return 0.1f;
        }
        Check(sawBlob && player.Health.Current < before, "Frog AI fires goo and damages tutorial player.");
        frog.enabled = false; frog.GetComponent<NavMeshAgent>().isStopped = true;
        CheckDummies();
        // Existing Health must accept player-sourced damage and invoke enemy death.
        crab.enabled = true; frog.enabled = true;
        crab.GetComponent<Health>().TakeDamage(999, player.gameObject);
        frog.GetComponent<Health>().TakeDamage(999, player.gameObject);
        Check(crab.IsDead && frog.IsDead, "Both enemy death paths accept player-sourced damage.");
        yield return 4f;
        Check(crab.IsDead && frog.IsDead, "Enemies stay defeated until stage reset, without test-arena timer respawn.");
        // Launch a long-lived projectile to check cleanup during reset.
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        GooBlob.Launch(mat, spawn + Vector3.up * 30, spawn, 12, 0.1f, 12, 1.5f, ~0, null);
        player.Health.TakeDamage(999);
        Check(player.IsRespawning, "Lethal damage triggers existing tutorial reset.");
        yield return 2f;
        crab = UnityEngine.Object.FindFirstObjectByType<HermitCrab>();
        frog = UnityEngine.Object.FindFirstObjectByType<MortarFrog>();
        Check(crab != null && frog != null && !crab.IsDead && !frog.IsDead, "Stage reset restores both enemy types.");
        Check(crab.GetComponent<Health>().Current == crab.GetComponent<Health>().Max && frog.GetComponent<Health>().Current == frog.GetComponent<Health>().Max, "Enemy health fully restored.");
        Check(Vector3.Distance(crab.transform.position, crabHome) < 2 && Vector3.Distance(frog.transform.position, frogHome) < 2, "Enemies restore near authored spawn positions.");
        Check(crab.GetComponent<NavMeshAgent>().isOnNavMesh && frog.GetComponent<NavMeshAgent>().isOnNavMesh, "Restored enemies rebind navigation.");
        Check(player.HasLaser && player.HasFlask && player.Health.Current == 100 && !player.IsRespawning, "Player retains pickups and full health after reset.");
        Check(UnityEngine.Object.FindObjectsByType<GooBlob>().Length == 0 && UnityEngine.Object.FindObjectsByType<GooPuddle>().Length == 0, "No goo from previous attempt survives reset.");
        CheckDummies();
        UnityEngine.Object.Destroy(mat);
    }

    private static void CheckDummies()
    {
        var dummies = UnityEngine.Object.FindObjectsByType<TrainingDummy>();
        Check(dummies.Length == 3, "All three tutorial dummies remain.");
        foreach (var dummy in dummies)
        {
            Vector3 original = JsonUtility.FromJson<Position>(SessionState.GetString(Key + dummy.name, "")).value;
            Check(Vector3.Distance(dummy.transform.position, original) < 0.01f && dummy.GetComponent<Health>().Current == dummy.GetComponent<Health>().Max,
                "Dummy unchanged and healthy: " + dummy.name);
        }
    }

    private static void PlacePlayer(Vector3 point)
    {
        point.y = terrain.SampleHeight(point) + terrain.transform.position.y + 0.05f;
        body.enabled = false; player.transform.position = point; body.enabled = true;
        Physics.SyncTransforms();
    }
    private static T Get<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj);
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); report.Add(message); }
    private static void Fail(Exception ex)
    {
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/TutorialEnemiesSmokeFailure.txt", ex.ToString() + "\n" + string.Join("\n", report));
        Debug.LogError(ex);
        EditorApplication.Exit(1);
    }
}
