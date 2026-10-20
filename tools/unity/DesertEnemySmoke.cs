// Install only in a disposable project's Assets/Editor; never in the real project.
using System;
using System.Collections.Generic;
using System.IO;
using SignalLost.Editor;
using SignalLost.Enemies.Desert;
using SignalLost.Player;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class DesertEnemySmoke
{
    private const string Key = "SignalLost.DesertEnemySmoke";
    private static readonly string[] Scenes = { "DesertFodderTest", "SparkTickTest", "SnorkelEelTest", "JerryCanBeetleTest", "HubcapArmadilloTest", "DishMeerkatTest", "BurnoutTest", "OldBuzzardTest", "SludgeTest" };
    private static readonly List<string> Report = new List<string>();
    private static float started;
    private static int peakAlive;
    private static bool initialized, moved, engaged;
    private static string error;
    static DesertEnemySmoke()
    {
        EditorApplication.update += Update;
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (message.StartsWith("ArgumentOutOfRangeException: Index was out of range.") && trace.Contains("UnityEditor.Search.SearchDatabase")) return;
            error = message + "\n" + trace;
        };
    }
    private static void Guard()
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
        string temporary = Path.GetFullPath(Path.GetTempPath()).Replace('\\', '/').TrimEnd('/') + "/";
        if (!Application.isBatchMode || !project.StartsWith(temporary, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use only a disposable batch-mode project under the system temporary directory.");
    }
    public static void Generate()
    {
        Guard();
        if (!File.Exists("Assets/Scenes/DesertFodderTest.unity")) DesertFodderBuilder.Create();
        DesertFodderBuilder.CreateSingleEnemyScenes();
        DesertMiniBossBuilder.CreateTestScenes();
        Validate();
        Debug.Log("DESERT_ENEMY_ASSETS_GENERATED: eight enemy prefabs and nine test scenes.");
    }
    public static void Validate()
    {
        Guard();
        foreach (string name in Scenes) Require(File.Exists("Assets/Scenes/" + name + ".unity"), "Missing test scene: " + name);
        DesertFodderBuilder.Validate();
        DesertFodderBuilder.ValidateSingleEnemyScenes();
        DesertMiniBossBuilder.Validate();
        Debug.Log("DESERT_ENEMY_ALL_ASSETS_VALIDATED");
    }
    public static void Start()
    {
        Guard(); Validate();
        EditorSceneManager.OpenScene("Assets/Scenes/" + Scenes[0] + ".unity");
        SessionState.SetInt(Key + "Index", 0);
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + "Deadline", (float)EditorApplication.timeSinceStartup + 210);
        EditorApplication.EnterPlaymode();
    }
    private static void Update()
    {
        if (!SessionState.GetBool(Key, false)) return;
        try
        {
            Require(error == null, error);
            Require(EditorApplication.timeSinceStartup < SessionState.GetFloat(Key + "Deadline", 0), "Play Mode smoke timed out.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            int index = SessionState.GetInt(Key + "Index", 0);
            if (!initialized) { started = Time.time; initialized = true; }
            var boss = Object.FindAnyObjectByType<DesertMiniBoss>();
            if (boss != null && boss.Engaged) engaged = true;
            int alive = 0;
            foreach (var station in Object.FindObjectsByType<FodderSpawner>(FindObjectsSortMode.None)) alive += station.AliveCount;
            peakAlive = Mathf.Max(peakAlive, alive);
            if (!moved && Time.time - started > 2)
            {
                var player = Object.FindAnyObjectByType<FirstPersonController>();
                var station = Object.FindAnyObjectByType<FodderSpawner>();
                Require(player != null && station != null, "Player/spawner missing.");
                var cc = player.GetComponent<CharacterController>();
                Vector3 point = boss != null ? new Vector3(0, 0.1f, -7) : station.transform.position + Vector3.back * 4;
                point.y = 0.1f;
                cc.enabled = false; player.transform.position = point; cc.enabled = true;
                moved = true;
            }
            if (Time.time - started < 8) return;
            var surface = Object.FindAnyObjectByType<NavMeshSurface>();
            Require(surface != null && surface.navMeshData != null, "NavMesh failed to build.");
            Require(peakAlive > 0, "No enemies spawned during observation in " + Scenes[index]);
            if (index >= 6) Require(boss != null && engaged, "Mini-boss did not engage in " + Scenes[index]);
            Require(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1, "Expected one active camera.");
            string line = "DESERT_ENEMY_SCENE_PLAY_PASS: " + Scenes[index] + "; peakSpawned=" + peakAlive + "; aliveAtEnd=" + alive + "; engaged=" + engaged;
            Report.Add(line); Debug.Log(line);
            index++;
            if (index == Scenes.Length)
            {
                Directory.CreateDirectory("Logs"); File.WriteAllLines("Logs/DesertEnemyPlayMode.txt", Report);
                SessionState.SetBool(Key, false); Debug.Log("DESERT_ENEMY_PLAY_MODE_PASS: nine scenes."); EditorApplication.Exit(0); return;
            }
            SessionState.SetInt(Key + "Index", index);
            initialized = moved = engaged = false;
            peakAlive = 0;
            EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/" + Scenes[index] + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
        }
        catch (Exception exception)
        {
            SessionState.SetBool(Key, false); Debug.LogException(exception); EditorApplication.Exit(1);
        }
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
