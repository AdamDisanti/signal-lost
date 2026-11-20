// Install only in a disposable project's Assets/Editor; never in the real project.
// Batch: -executeMethod GeodeBearSmoke.Start (no -quit; it exits itself).
using System;
using System.Collections;
using System.IO;
using System.Linq;
using SignalLost.Combat;
using SignalLost.Editor;
using SignalLost.Enemies.Caves;
using SignalLost.Enemies.Desert;
using SignalLost.Inventory;
using SignalLost.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class GeodeBearSmoke
{
    private const string Key = "SignalLost.GeodeBearSmoke";
    private static GeodeBearSmokeRunner runner;
    internal static bool done;
    private static string error, report = "";

    static GeodeBearSmoke()
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

    public static void Start()
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
        string temporary = Path.GetFullPath(Path.GetTempPath()).Replace('\\', '/').TrimEnd('/') + "/";
        if (!Application.isBatchMode || !project.StartsWith(temporary, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use only a disposable batch-mode project under the system temporary directory.");
        GeodeBearBuilder.BuildPrefab();
        AssetDatabase.SaveAssets();
        GeodeBearBuilder.CreateTestScene();
        GeodeBearBuilder.Validate();
        EditorSceneManager.OpenScene(GeodeBearBuilder.ScenePath);
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
            Require(EditorApplication.timeSinceStartup < SessionState.GetFloat(Key + "Deadline", 0), "Smoke timed out. So far: " + report);
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (runner == null) runner = new GameObject("Geode Bear Smoke").AddComponent<GeodeBearSmokeRunner>();
            if (done)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/GeodeBearPlayMode.txt", "GEODE_BEAR_PLAY_MODE_PASS:" + report);
                Debug.Log("GEODE_BEAR_PLAY_MODE_PASS:" + report);
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(0);
            }
        }
        catch (Exception exception)
        {
            SessionState.SetBool(Key, false);
            Debug.LogException(exception);
            Debug.Log("GEODE_BEAR_PLAY_MODE_FAIL after:" + report);
            EditorApplication.Exit(1);
        }
    }

    private static FirstPersonController player;
    private static CharacterController body;
    private static Health hp, bearHp;
    private static GeodeBear bear;
    private static float damage;

    private static IEnumerator Wait(float seconds, Action each = null)
    {
        float end = Time.time + seconds;
        while (Time.time < end) { each?.Invoke(); yield return null; }
    }

    private static void Place(Vector3 at)
    {
        body.enabled = false;
        player.transform.position = at;
        body.enabled = true;
    }

    private static Health Part(string name) => bear.GetComponentsInChildren<Health>(true).First(h => h.name.Contains(name));

    internal static IEnumerator Run()
    {
        yield return Wait(2.5f);
        player = Object.FindAnyObjectByType<FirstPersonController>();
        body = player.GetComponent<CharacterController>();
        hp = player.GetComponent<Health>();
        bear = Object.FindAnyObjectByType<GeodeBear>();
        Require(bear != null && bear.GetComponentInChildren<Animator>() != null, "No Geode Bear with an Animator.");
        bearHp = bear.GetComponent<Health>();
        hp.Damaged += (_, amount, source) => { if (source == bear.gameObject) damage += amount; };
        Vector3 home = bear.transform.position;

        // Walk in: it engages and roars.
        Place(home + new Vector3(0f, 0.05f, -10f));
        yield return Wait(0.6f);
        Require(bear.Engaged, "It should engage when the player walks into its arena.");
        report += " engaged;";

        // During the intro roar (no daze): the geode back takes half, the power cell full.
        float h0 = bearHp.Current;
        Part("geode back").TakeDamage(100f, player.gameObject);
        yield return null; yield return null;
        float backTaken = h0 - bearHp.Current;
        h0 = bearHp.Current;
        Part("power cell").TakeDamage(100f, player.gameObject);
        yield return null; yield return null;
        float cellTaken = h0 - bearHp.Current;
        Require(Mathf.Abs(backTaken - 50f) < 1f && Mathf.Abs(cellTaken - 100f) < 1f, $"Back should take 50 and cell 100 (got {backTaken}, {cellTaken}).");
        report += $" back {backTaken:0} / cell {cellTaken:0};";
        bearHp.Restore();

        bear.DebugEnterPhase(2);
        yield return Wait(4f);
        foreach (string attack in new[] { "Swipe", "BoulderLob", "Yawn", "BellyFlop", "BearHug" })
        {
            yield return Wait(0.2f);
            while (bear.CurrentMove != null) yield return null;
            hp.Restore();
            float range = attack == "Swipe" ? 2.8f : attack == "Yawn" ? 4f : attack == "BearHug" ? 6f : 10f;
            Vector3 front = bear.transform.position + bear.transform.forward * range;
            Place(new Vector3(front.x, home.y + 0.05f, front.z));
            yield return Wait(0.2f);
            float before = damage;
            bool slowed = false;
            Require(bear.DebugRunAttack(attack), "No attack " + attack);
            float end = Time.time + 12f;
            while (bear.CurrentMove == attack && Time.time < end)
            {
                slowed |= bear.PlayerSlowed && player.SpeedBonus < 0.9f;
                yield return null;
            }
            if (attack == "Yawn")
            {
                Require(slowed, "The yawn's dust should slow the player standing in it.");
                // It may yawn again on its own (a debug run skips the cooldown), so wait for the slow to clear.
                float until = Time.time + 12f;
                while (bear.PlayerSlowed && Time.time < until) yield return null;
                Require(!bear.PlayerSlowed && player.SpeedBonus > 0.99f, "The yawn's slow should wear off.");
                report += " Yawn slowed;";
                continue;
            }
            Require(damage > before, attack + " did no damage to a player standing in it.");
            report += $" {attack} {damage - before:0};";
        }

        // Phase 3: hibernate. Left alone it heals; hit on the cell it wakes.
        bear.DebugEnterPhase(3);
        yield return Wait(4f);
        Require(bear.DuelPhase == 3, "Phase 3 should start (got " + bear.DuelPhase + ").");
        while (bear.CurrentMove != null) yield return null;
        bearHp.TakeDamage(bearHp.Max * 0.5f, player.gameObject);
        Place(home + new Vector3(0f, 0.05f, -14f));
        Require(bear.DebugRunAttack("Hibernate"), "No Hibernate attack.");
        while (!bear.IsHibernating) yield return null;
        float slept = bearHp.Current;
        while (bear.IsHibernating) yield return null;
        float healed = bearHp.Current - slept;
        Require(healed > bearHp.Max * 0.12f, $"Sleeping the whole window should heal ~15% (healed {healed:0}).");
        report += $" hibernate healed {healed:0};";
        while (bear.CurrentMove != null) yield return null;
        Require(bear.DebugRunAttack("Hibernate"), "No Hibernate attack.");
        while (!bear.IsHibernating) yield return null;
        yield return Wait(1f);
        Part("power cell").TakeDamage(10f, player.gameObject);
        yield return Wait(0.3f);
        Require(!bear.IsHibernating, "A hit on the power cell should wake it.");
        report += " cell hit woke it;";

        int scrap = Object.FindObjectsByType<ScrapPickup>(FindObjectsSortMode.None).Length;
        int parts = Object.FindObjectsByType<ShipPartDrop>(FindObjectsSortMode.None).Length;
        bearHp.TakeDamage(99999f, player.gameObject);
        yield return Wait(0.5f);
        Require(Object.FindObjectsByType<ScrapPickup>(FindObjectsSortMode.None).Length > scrap, "It should drop scrap.");
        Require(Object.FindObjectsByType<ShipPartDrop>(FindObjectsSortMode.None).Length > parts, "It should drop the power cell.");
        Require(player.SpeedBonus > 0.99f, "No slow may outlive the bear.");
        report += " scrap + power cell dropped";
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}

public sealed class GeodeBearSmokeRunner : MonoBehaviour
{
    private IEnumerator Start()
    {
        yield return GeodeBearSmoke.Run();
        GeodeBearSmoke.done = true;
    }
}
