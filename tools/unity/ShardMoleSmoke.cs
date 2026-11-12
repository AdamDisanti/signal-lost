// Install only in a disposable project's Assets/Editor; never in the real project.
// Batch: -executeMethod ShardMoleSmoke.Start (no -quit; it exits itself).
using System;
using System.Collections;
using System.IO;
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
public static class ShardMoleSmoke
{
    private const string Key = "SignalLost.ShardMoleSmoke";
    private static ShardMoleSmokeRunner runner;
    internal static bool done;
    private static string error, report = "";

    static ShardMoleSmoke()
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
        ShardMoleBuilder.BuildPrefab();
        AssetDatabase.SaveAssets();
        ShardMoleBuilder.CreateTestScene();
        ShardMoleBuilder.Validate();
        EditorSceneManager.OpenScene(ShardMoleBuilder.ScenePath);
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
            // The script runs as a coroutine in the game loop, on the game's own clock.
            if (runner == null) runner = new GameObject("Shard Mole Smoke").AddComponent<ShardMoleSmokeRunner>();
            if (done)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/ShardMolePlayMode.txt", "SHARD_MOLE_PLAY_MODE_PASS:" + report);
                Debug.Log("SHARD_MOLE_PLAY_MODE_PASS:" + report);
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(0);
            }
        }
        catch (Exception exception)
        {
            SessionState.SetBool(Key, false);
            Debug.LogException(exception);
            Debug.Log("SHARD_MOLE_PLAY_MODE_FAIL after:" + report);
            EditorApplication.Exit(1);
        }
    }

    private static FirstPersonController player;
    private static CharacterController body;
    private static Health hp;
    private static ShardMole mole;
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

    /// <summary>Footsteps: shuffles back and forth fast enough to be heard.</summary>
    private static void Shuffle() => body.Move(new Vector3(Mathf.Sin(Time.time * 20f) * 2f * Time.deltaTime, -0.5f * Time.deltaTime, 0f));

    internal static IEnumerator Run()
    {
        yield return Wait(2.5f);
        player = Object.FindAnyObjectByType<FirstPersonController>();
        body = player.GetComponent<CharacterController>();
        hp = player.GetComponent<Health>();
        mole = Object.FindAnyObjectByType<ShardMole>();
        Require(mole != null && mole.GetComponentInChildren<Animator>() != null, "No Shard Mole with an Animator.");
        hp.Damaged += (_, amount, source) => { if (source == mole.gameObject) damage += amount; };
        Vector3 home = mole.transform.position;

        // Walk in: it engages (boss bar), roars, then hunts.
        Place(home + new Vector3(0f, 0.05f, -10f));
        yield return Wait(1.5f, Shuffle);
        Require(mole.Engaged, "It should engage when the player walks into its arena.");
        report += " engaged;";

        // Stand still and quiet: after ~3 s it stops and listens, and doesn't attack while it does.
        Place(mole.transform.position + mole.transform.forward * 9f + Vector3.up * 0.05f);
        float listenedAt = -1f, quietSince = Time.time;
        yield return Wait(14f, () => { if (listenedAt < 0f && mole.IsListening) listenedAt = Time.time; });
        Require(listenedAt > 0f, "It never listened while the player stood silent (move=" + mole.CurrentMove + ") " + Diag());
        float before = damage;
        yield return Wait(2.5f);
        Require(mole.IsListening && damage == before, "While listening it must stay put and not attack.");
        report += " listens " + (listenedAt - quietSince).ToString("0.0") + "s after the player went quiet;";
        Shot("listen");
        // Noise: it hears and stops listening.
        yield return Wait(0.6f, Shuffle);
        yield return Wait(1.2f, Shuffle); // the startle (head snapping toward the sound) finishes the listen
        Require(!mole.IsListening, "Footsteps should end its listening.");
        report += " footsteps wake it;";

        foreach (string attack in new[] { "Swipe", "TailSwoop", "ClawFlurry", "CeilingShards", "Burrow" })
        {
            yield return Wait(0.2f, () => { });
            while (mole.CurrentMove != null && !mole.IsListening) yield return null;
            hp.Restore();
            float range = attack == "Burrow" ? 10f : attack == "CeilingShards" ? 6f : 2.6f;
            Vector3 front = mole.transform.position + mole.transform.forward * range;
            Place(new Vector3(front.x, home.y + 0.05f, front.z));
            yield return Wait(0.3f, Shuffle); // heard where it now stands
            before = damage;
            Require(mole.DebugRunAttack(attack), "No attack " + attack);
            bool buried = false, shot = false;
            float started = Time.time;
            float end = Time.time + 12f;
            while (mole.CurrentMove == attack && Time.time < end)
            {
                buried |= mole.IsBurrowed;
                if (!shot && Time.time - started > (attack == "Burrow" ? 2.6f : 0.5f)) { shot = true; Shot(attack); }
                yield return null;
            }
            Require(damage > before, attack + " did no damage to a player standing in it (player " +
                (player.transform.position - home).magnitude.ToString("0.0") + " m from home).");
            if (attack == "Burrow")
            {
                Require(buried, "Burrow never went underground.");
                // The drop copy of the antenna rides hidden in the mole; coming back up must not show it (it would hang frozen in the air).
                foreach (Renderer r in mole.transform.Find("Antenna array").GetComponentsInChildren<Renderer>(true))
                    Require(!r.enabled, "The hidden antenna array showed after the burrow.");
            }
            report += $" {attack} {damage - before:0};";
        }

        // Phase change (the player stays silent so it isn't attacking meanwhile).
        hp.Restore();
        mole.DebugEnterPhase(3);
        yield return Wait(4f);
        Require(mole.DuelPhase == 3, "Phase 3 should start (got " + mole.DuelPhase + ").");
        report += " phase3;";

        int scrap = Object.FindObjectsByType<ScrapPickup>(FindObjectsSortMode.None).Length;
        int parts = Object.FindObjectsByType<ShipPartDrop>(FindObjectsSortMode.None).Length;
        mole.GetComponent<Health>().TakeDamage(99999f, player.gameObject);
        yield return Wait(0.5f);
        Require(Object.FindObjectsByType<ScrapPickup>(FindObjectsSortMode.None).Length > scrap, "It should drop scrap.");
        Require(Object.FindObjectsByType<ShipPartDrop>(FindObjectsSortMode.None).Length > parts, "It should drop the antenna array.");
        report += " scrap + antenna array dropped";
    }

    /// <summary>A picture from a camera beside the mole (rendered runs only), for checking the look.</summary>
    private static void Shot(string name)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var go = new GameObject("Shot Camera");
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.15f, 0.17f, 0.24f);
        Vector3 at = mole.transform.position;
        go.transform.position = at + mole.transform.forward * 7f + mole.transform.right * 5f + Vector3.up * 3.5f;
        go.transform.LookAt(at + Vector3.up * 1.2f);
        var rt = new RenderTexture(960, 640, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(960, 640, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 960, 640), 0, 0);
        RenderTexture.active = null;
        Directory.CreateDirectory("Logs/ShardMoleShots");
        File.WriteAllBytes("Logs/ShardMoleShots/" + name + ".png", tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.Destroy(rt);
        Object.Destroy(go);
    }

    private static string Diag()
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var t = typeof(DuelBoss);
        var anim = mole.GetComponentInChildren<Animator>();
        return $"t={Time.time:0.00} clipStart={t.GetField("clipStart", flags).GetValue(mole)} playing={t.GetField("playing", flags).GetValue(mole)} " +
               $"animSpeed={(anim != null ? anim.speed : -1f)} engaged={mole.Engaged} dead={mole.IsDead} silent={mole.SilentFor:0.0} timeScale={Time.timeScale}";
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}

public sealed class ShardMoleSmokeRunner : MonoBehaviour
{
    private IEnumerator Start()
    {
        yield return ShardMoleSmoke.Run();
        ShardMoleSmoke.done = true;
    }
}
