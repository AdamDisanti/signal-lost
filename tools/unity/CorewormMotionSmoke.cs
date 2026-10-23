// Install only in a disposable project's Assets/Editor; never in the real project.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using SignalLost.Combat;
using SignalLost.Enemies.Desert;
using SignalLost.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CorewormMotionSmoke
{
    private const string Key = "SignalLost.CorewormMotionSmoke";
    private static bool spawned;
    private static string error;
    internal static readonly List<string> Report = new List<string>();
    internal static readonly List<string> Failures = new List<string>();

    static CorewormMotionSmoke()
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

    public static void Start()
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
        string temporary = Path.GetFullPath(Path.GetTempPath()).Replace('\\', '/').TrimEnd('/') + "/";
        if (!Application.isBatchMode || !project.StartsWith(temporary, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Disposable temporary batch project required.");
        EditorSceneManager.OpenScene("Assets/Scenes/Bosses/CorewormTest.unity");
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + "Deadline", (float)EditorApplication.timeSinceStartup + 240f);
        EditorApplication.EnterPlaymode();
    }

    private static void Update()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (error != null || EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + "Deadline", 0))
        { Failures.Add(error ?? "Motion smoke timed out."); Finish(); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < 1.5f || spawned) return;
        spawned = true;
        new GameObject("Coreworm Motion Probe").AddComponent<CorewormMotionProbe>();
    }

    internal static object Call(object obj, string method, params object[] args) =>
        obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args);
    internal static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    internal static void Finish()
    {
        SessionState.SetBool(Key, false);
        Time.captureDeltaTime = 0f;
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/CorewormMotion.txt", Report);
        if (Failures.Count > 0) Debug.LogError("COREWORM_MOTION_FAIL: " + string.Join("; ", Failures));
        else Debug.Log("COREWORM_MOTION_PASS: 15 dive regressions and all four fight phases.");
        EditorApplication.Exit(Failures.Count == 0 ? 0 : 1);
    }
}

public sealed class CorewormMotionProbe : MonoBehaviour
{
    private IEnumerator Start()
    {
        var worm = Object.FindAnyObjectByType<Coreworm>();
        var arena = Object.FindAnyObjectByType<CorewormArena>();
        var player = Object.FindAnyObjectByType<FirstPersonController>();
        CorewormMotionSmoke.Require(worm != null && arena != null && player != null, "Missing worm/arena/player.");
        foreach (var exit in Object.FindObjectsByType<DesertStageExit>(FindObjectsSortMode.None)) { exit.StopAllCoroutines(); exit.enabled = false; }
        worm.StopAllCoroutines();
        player.enabled = false;
        player.GetComponent<Health>().Invulnerable = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        UnityEngine.Random.InitState(23142);
        Vector3 ground = arena.Centre;
        ground.y = arena.GroundY;
        float[] jitter = { 0.007f, 0.013f, 0.019f, 0.011f };

        foreach (int fps in new[] { 30, 60, 144, 240, 0 })
        foreach (float duration in new[] { 0.6f, 0.7f, 0.8f })
        {
            Time.captureDeltaTime = fps == 0 ? jitter[0] : 1f / fps;
            yield return null;
            worm.HideAt(ground);
            worm.Head.position = ground + new Vector3(-6f, 9f, 0f);
            Vector3 previous = worm.Head.position;
            var routine = (IEnumerator)CorewormMotionSmoke.Call(worm, "DiveInto", ground, duration);
            var speeds = new List<float>();
            int frame = 0;
            while (routine.MoveNext())
            {
                float speed = Vector3.Distance(previous, worm.Head.position) / Time.deltaTime;
                speeds.Add(speed);
                previous = worm.Head.position;
                if (fps == 0) Time.captureDeltaTime = jitter[++frame % jitter.Length];
                yield return null;
            }
            float worst = 1f;
            for (int i = Mathf.Max(1, speeds.Count - 3); i < speeds.Count; i++)
                worst = Mathf.Min(worst, speeds[i] / Mathf.Max(0.001f, speeds[i - 1]));
            string label = "fps=" + (fps == 0 ? "jitter" : fps.ToString()) + "; duration=" + duration;
            string line = "DIVE_TAIL: " + label + "; lastSpeed=" + speeds[speeds.Count - 1].ToString("F3") + "; worstRetained=" + worst.ToString("F4");
            CorewormMotionSmoke.Report.Add(line); Debug.Log(line);
            if (worst < 0.8f) CorewormMotionSmoke.Failures.Add(label + ": final dive frame lost " + ((1f - worst) * 100f).ToString("F1") + "% speed.");
        }
        if (CorewormMotionSmoke.Failures.Count > 0) { CorewormMotionSmoke.Finish(); yield break; }

        // Exercise the real fight and arena changes, not only the isolated movement coroutine.
        Time.captureDeltaTime = 1f / 60f;
        yield return null;
        var controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = arena.Centre + new Vector3(0f, 0.1f, -15f);
        controller.enabled = true;
        worm.HideAt(ground);
        worm.BeginFight();
        float[] seconds = { 18f, 18f, 26f, 42f };
        var attacks = new HashSet<string>();
        for (int phase = 1; phase <= 4; phase++)
        {
            while (worm.Phase < phase || worm.Invulnerable) yield return null;
            float until = Time.time + seconds[phase - 1];
            attacks.Clear();
            while (Time.time < until)
            {
                Vector3 p = worm.Head.position;
                CorewormMotionSmoke.Require(float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z), "Non-finite head pose.");
                CorewormMotionSmoke.Require(worm.Body != null && worm.TrailCount > 1, "Body/trail disappeared.");
                if (!string.IsNullOrEmpty(worm.AttackName) && worm.AttackName != "PhaseChange") attacks.Add(worm.AttackName);
                yield return null;
            }
            CorewormMotionSmoke.Require(attacks.Count > 0, "No attacks in phase " + phase);
            string line = "FIGHT_PHASE_PASS: " + phase + "; attacks=" + string.Join(",", attacks);
            CorewormMotionSmoke.Report.Add(line); Debug.Log(line);
            if (phase < 4)
            {
                var health = worm.GetComponent<Health>();
                health.TakeDamage(health.Current - health.Max * (0.99f - phase * 0.25f), player.gameObject);
            }
        }
        CorewormMotionSmoke.Finish();
    }
}
