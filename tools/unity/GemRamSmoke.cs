// Install only in a disposable project's Assets/Editor; never in the real project.
// Batch: -executeMethod GemRamSmoke.Start (no -quit; it exits itself).
using System;
using System.IO;
using SignalLost.Combat;
using SignalLost.Editor;
using SignalLost.Enemies.Caves;
using SignalLost.Inventory;
using SignalLost.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class GemRamSmoke
{
    private const string Key = "SignalLost.GemRamSmoke";
    private static float started = -1f, stageStart;
    private static int stage, playerHits;
    private static bool sawRicochet, sawDaze;
    private static string error;

    static GemRamSmoke()
    {
        EditorApplication.update += Update;
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return; // import-time package errors are not ours
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

    public static void Start()
    {
        Guard();
        GemRamBuilder.Validate();
        EditorSceneManager.OpenScene(GemRamBuilder.ScenePath);
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + "Deadline", (float)EditorApplication.timeSinceStartup + 300);
        EditorApplication.EnterPlaymode();
    }

    private static void Update()
    {
        if (!SessionState.GetBool(Key, false)) return;
        try
        {
            Require(error == null, error);
            Require(EditorApplication.timeSinceStartup < SessionState.GetFloat(Key + "Deadline", 0), "Play Mode smoke timed out at stage " + stage);
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (started < 0f) started = Time.time;
            float t = Time.time - started;
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            Require(player != null, "No player.");
            GemRam[] rams = Object.FindObjectsByType<GemRam>(FindObjectsSortMode.None);
            foreach (GemRam ram in rams)
            {
                sawRicochet |= ram.IsRicocheting;
                sawDaze |= ram.IsDazed;
            }

            if (stage == 0 && t > 2.5f)
            {
                Require(rams.Length == 3, "Expected three rams, found " + rams.Length);
                player.GetComponent<Health>().Damaged += (_, amount, source) => { if (source != null && source.GetComponent<GemRam>() != null) playerHits++; };
                // Close to the middle ram: hop-and-butt range.
                Teleport(player, new Vector3(0f, 0.05f, 3f));
                stage = 1;
                stageStart = t;
            }
            else if (stage == 1 && playerHits > 0)
            {
                // Mid range from the rams, near the pillars: ricochet range. Keep the player alive.
                player.GetComponent<Health>().Restore();
                Teleport(player, new Vector3(-3f, 0.05f, -6f));
                stage = 2;
                stageStart = t;
            }
            else if (stage == 1 && t - stageStart > 25f) throw new InvalidOperationException("No ram hit the player at close range within 25 s.");
            else if (stage == 2)
            {
                player.GetComponent<Health>().Restore();
                if (!sawRicochet && t - stageStart < 30f) return;
                Require(sawRicochet, "No ram used the ricochet within 30 s.");
                GemRam victim = rams[0];
                int before = Object.FindObjectsByType<ScrapPickup>(FindObjectsSortMode.None).Length;
                victim.GetComponent<Health>().TakeDamage(9999f, player.gameObject);
                int after = Object.FindObjectsByType<ScrapPickup>(FindObjectsSortMode.None).Length;
                Require(after > before, "A dead ram should drop scrap.");
                var crystal = (Renderer)new SerializedObject(victim).FindProperty("hornCrystals").GetArrayElementAtIndex(0).objectReferenceValue;
                Require(crystal.sharedMaterial.name.Contains("Spent"), "Horn crystals should go dull on death, got " + crystal.sharedMaterial.name);
                Report("GEM_RAM_PLAY_MODE_PASS: hits=" + playerHits + ", ricochet seen, daze seen=" + sawDaze + ", scrap " + (after - before) + ", crystals dull on death");
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(0);
            }
        }
        catch (Exception exception)
        {
            SessionState.SetBool(Key, false);
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void Teleport(FirstPersonController player, Vector3 at)
    {
        var body = player.GetComponent<CharacterController>();
        body.enabled = false;
        player.transform.SetPositionAndRotation(at, Quaternion.identity);
        body.enabled = true;
    }

    private static void Report(string line)
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/GemRamPlayMode.txt", line);
        Debug.Log(line);
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
