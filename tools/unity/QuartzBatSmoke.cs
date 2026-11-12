// Install only in a disposable project's Assets/Editor; never in the real project.
// Batch: -executeMethod QuartzBatSmoke.Start (no -quit; it exits itself).
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
public static class QuartzBatSmoke
{
    private const string Key = "SignalLost.QuartzBatSmoke";
    private static float started = -1f, maxHeight, minHeight = 99f;
    private static int stage, bites;
    private static bool sawSwoop;
    private static QuartzBat bat;
    private static string error;

    static QuartzBatSmoke()
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
        QuartzBatBuilder.Validate();
        EditorSceneManager.OpenScene(QuartzBatBuilder.ScenePath);
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
            foreach (QuartzBat item in Object.FindObjectsByType<QuartzBat>(FindObjectsSortMode.None))
            {
                if (item.IsDead) continue;
                maxHeight = Mathf.Max(maxHeight, item.Height);
                minHeight = Mathf.Min(minHeight, item.Height);
                sawSwoop |= item.IsSwooping;
            }

            if (stage == 0 && t > 2f)
            {
                QuartzBat[] all = Object.FindObjectsByType<QuartzBat>(FindObjectsSortMode.None);
                Require(all.Length == 3, "Expected three bats, found " + all.Length);
                foreach (QuartzBat item in all) Require(item.Height > 2.5f, item.name + " should be flying, height " + item.Height);
                // Walk the player up to the bats.
                var body = player.GetComponent<CharacterController>();
                body.enabled = false;
                player.transform.SetPositionAndRotation(all[0].transform.position + new Vector3(0f, 0.05f, -4f), Quaternion.identity);
                body.enabled = true;
                player.GetComponent<Health>().Damaged += (_, amount, source) => { if (source != null && source.GetComponent<QuartzBat>() != null) { bites++; if (bat == null) bat = source.GetComponent<QuartzBat>(); } };
                stage = 1;
            }
            else if (stage == 1 && bites > 0)
            {
                stage = 10; // let it settle into its low hang (the melee window) before checking heights
                stageTime = t;
            }
            else if (stage == 10 && t > stageTime + 0.8f)
            {
                Require(sawSwoop, "It bit without swooping.");
                Require(maxHeight > 3f && minHeight < 1.6f, "It should circle high and swoop low; heights " + minHeight + ".." + maxHeight);
                // Damage rule, tested with the player's own damage: not swinging and not a crystal shot = refused.
                Health health = bat.GetComponent<Health>();
                GameObject laser = player.GetComponentInChildren<MiningLaser>().gameObject;
                Require(!health.TakeDamage(10f, laser), "Non-melee, non-crystal player damage (blast splash) should be refused.");
                Require(health.Current == health.Max, "Refused damage still hurt it.");
                // A bolt into the crystal hurts; a bolt into a wing glances off.
                var data = new SerializedObject(bat);
                var crystal = (Collider)data.FindProperty("crystalCollider").objectReferenceValue;
                var wing = ((Transform)data.FindProperty("wings").GetArrayElementAtIndex(0).objectReferenceValue).GetComponent<Collider>();
                bat.enabled = false; // hold it still for the test bolts (batch-mode frames are long); its damage rule still applies
                FireAt(wing.bounds.center, Vector3.down, player.transform);
                FireAt(crystal.bounds.center, -bat.transform.forward, player.transform);
                stage = 2;
                stageTime = t;
            }
            else if (stage == 1 && t > 40f) throw new InvalidOperationException("No bat bit the player within 38 s.");
            else if (stage == 2 && t > stageTime + 0.6f)
            {
                Health health = bat.GetComponent<Health>();
                Require(health.Current < health.Max, "A bolt into the crystal should hurt it.");
                Require(Mathf.Approximately(health.Max - health.Current, 5f), "Only the crystal bolt (5) should land, took " + (health.Max - health.Current));
                bat.enabled = true;
                // Regression: outside its 14 m home area but inside the 28 m leash, it must keep attacking, not freeze in the air.
                var body = player.GetComponent<CharacterController>();
                body.enabled = false;
                player.transform.position = new Vector3(0f, 0.05f, -8f); // 19-26 m from every bat's home
                body.enabled = true;
                stage = 3;
                stageTime = t;
            }
            else if (stage == 3 && t > stageTime + 25f) throw new InvalidOperationException("Outside its home area the bat swooped " + swoopsAway + " times in 25 s; expected 2 (it froze in the air).");
            else if (stage == 3 && swoopsAway < 2)
            {
                if (bat.IsSwooping && !wasSwooping) swoopsAway++; // two swoops outlast the old 4 s give-up timer
                wasSwooping = bat.IsSwooping;
            }
            else if (stage == 3)
            {
                Health health = bat.GetComponent<Health>();
                int before = Object.FindObjectsByType<ScrapPickup>(FindObjectsSortMode.None).Length;
                health.TakeDamage(9999f, null);
                int after = Object.FindObjectsByType<ScrapPickup>(FindObjectsSortMode.None).Length;
                Require(after > before, "A dead bat should drop scrap.");
                Report("QUARTZ_BAT_PLAY_MODE_PASS: flies " + minHeight.ToString("0.0") + "-" + maxHeight.ToString("0.0") + " m, swoop bites=" + bites +
                       ", swooped twice in " + (t - stageTime).ToString("0.0") + " s outside its home area" +
                       ", blast refused, wing bolt glanced, crystal bolt hit, scrap " + (after - before));
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

    private static float stageTime;
    private static int swoopsAway;
    private static bool wasSwooping;

    // A small 5-damage bolt owned by the player, fired from 0.3 m away (close, so other bats crowding the player can't get in the way).
    private static void FireAt(Vector3 point, Vector3 direction, Transform owner)
    {
        Vector3 from = point - direction * 0.3f;
        var look = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        LaserBolt.Spawn(look, from, direction, from, 5f, 60f, 0.03f, 5f, ~0, owner, null);
    }

    private static void Report(string line)
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/QuartzBatPlayMode.txt", line);
        Debug.Log(line);
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
