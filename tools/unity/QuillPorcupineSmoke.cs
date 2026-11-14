// Install only in a disposable project's Assets/Editor; never in the real project.
// Batch: -executeMethod QuillPorcupineSmoke.Start (no -quit; it exits itself).
using System;
using System.IO;
using System.Reflection;
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
public static class QuillPorcupineSmoke
{
    private const string Key = "SignalLost.QuillPorcupineSmoke";
    private static float started = -1f, stageAt;
    private static int stage, quillHits, thornHits;
    private static string error;

    static QuillPorcupineSmoke()
    {
        EditorApplication.update += Update;
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
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
        QuillPorcupineBuilder.Validate();
        EditorSceneManager.OpenScene(QuillPorcupineBuilder.ScenePath);
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + "Deadline", (float)EditorApplication.timeSinceStartup + 240);
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
            if (stage > 0) player.GetComponent<Health>().Restore(); // three porcupines firing at once would kill the test player
            QuillPorcupine[] all = Object.FindObjectsByType<QuillPorcupine>(FindObjectsSortMode.None);

            if (stage == 0 && t > 2.5f)
            {
                Require(all.Length == 3, "Expected three porcupines, found " + all.Length);
                // Stand 8 m in front of the middle porcupine, in the open.
                Teleport(player, new Vector3(0f, 0.05f, 9f));
                player.GetComponent<Health>().Damaged += (_, amount, source) =>
                {
                    if (source == null || source.GetComponent<QuillPorcupine>() == null) return;
                    if (Mathf.Approximately(amount, 8f)) quillHits++;
                    else if (Mathf.Approximately(amount, 5f)) thornHits++;
                };
                stage = 1;
            }
            else if (stage == 1 && quillHits > 0)
            {
                // Bristle: step right up to one.
                QuillPorcupine near = all[0];
                Teleport(player, near.transform.position + near.transform.forward * 0.9f);
                stageAt = t;
                stage = 2;
            }
            else if (stage == 1 && t > 30f) throw new InvalidOperationException("No quill hit the player within 27 s.");
            else if (stage == 2 && thornHits > 0)
            {
                // Curled: a full charged bolt takes half its health, the second kills it.
                QuillPorcupine victim = all[0];
                FieldInfo state = typeof(QuillPorcupine).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
                state.SetValue(victim, Enum.Parse(state.FieldType, "Curl"));
                Health health = victim.GetComponent<Health>();
                float fullCharge = player.GetComponentInChildren<MiningLaser>(true).MaxShotDamage;
                health.TakeDamage(fullCharge * 0.5f, player.gameObject); // a half charge: a quarter of its health
                Require(Mathf.Approximately(health.Current, health.Max * 0.75f), "Curled, a half-damage shot should take a quarter of its health, left " + health.Current);
                health.Restore();
                state.SetValue(victim, Enum.Parse(state.FieldType, "Curl"));
                health.TakeDamage(fullCharge, player.gameObject);
                Require(!health.IsDead && Mathf.Approximately(health.Current, health.Max * 0.5f), "Curled, a full charge should leave half health, left " + health.Current);

                int scrapBefore = Object.FindObjectsByType<ScrapPickup>(FindObjectsSortMode.None).Length;
                state.SetValue(victim, Enum.Parse(state.FieldType, "Curl"));
                health.TakeDamage(fullCharge, player.gameObject); // the second full charge finishes it
                Require(health.IsDead, "Curled, a second full charge should kill it, left " + health.Current);
                int scrapAfter = Object.FindObjectsByType<ScrapPickup>(FindObjectsSortMode.None).Length;
                Require(scrapAfter > scrapBefore, "A dead porcupine should drop scrap.");
                var quills = new SerializedObject(victim).FindProperty("quills");
                string quill = ((Renderer)quills.GetArrayElementAtIndex(0).objectReferenceValue).sharedMaterial.name;
                Require(quill.Contains("Spent"), "Its quills should go dull when it dies, got " + quill);
                Report("QUILL_PORCUPINE_PLAY_MODE_PASS: quill hits=" + quillHits + ", bristle hits=" + thornHits + ", curled: full charge -> half health, second kills, scrap " + (scrapAfter - scrapBefore) + ", quills dull on death");
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(0);
            }
            else if (stage == 2 && t - stageAt > 8f) throw new InvalidOperationException("No bristle thorn hit within 8 s of standing next to it.");
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
        File.WriteAllText("Logs/QuillPorcupinePlayMode.txt", line);
        Debug.Log(line);
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
