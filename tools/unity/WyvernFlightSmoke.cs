// Copy-only smoke (never in the real project; put it in Assets/Editor/Smoke of a disposable copy under %TEMP%).
// Batch: -executeMethod WyvernFlightSmoke.Start (no -quit; it exits itself). Output folder: env STAGE_SMOKE_OUT.
// Plays BriarwingTest, forces the dive bomb (and then the spinning dive) through the boss's own move runner, and
// records every frame of the flight: height, horizontal speed, bank, pitch and clip. Saves a frame strip of the dive
// bomb from a fixed camera and a CSV of the samples, and checks the flight follows the wingbeats (the climb is faster
// on downstrokes than upstrokes), banks in turns, never rises faster than a heavy flyer should, and lands.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using SignalLost.Enemies.Desert;
using SignalLost.Progression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class WyvernFlightSmoke
{
    private const string Key = "SignalLost.WyvernFlightSmoke";
    internal static bool done;
    internal static string report = "";
    private static string error;
    private static WyvernFlightRunner runner;
    internal static string Out => Environment.GetEnvironmentVariable("STAGE_SMOKE_OUT") ?? Path.Combine(Path.GetTempPath(), "StageOrderSmoke");

    static WyvernFlightSmoke()
    {
        EditorApplication.update += Update;
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (trace != null && trace.Contains("UnityEditor.Search")) return;
            error = message + "\n" + trace;
        };
    }

    public static void Start()
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
        string temporary = Path.GetFullPath(Path.GetTempPath()).Replace('\\', '/').TrimEnd('/') + "/";
        if (!Application.isBatchMode || !project.StartsWith(temporary, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use only a disposable batch-mode project under the system temporary directory.");
        EditorSceneManager.OpenScene("Assets/Scenes/Bosses/BriarwingTest.unity");
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
            Require(EditorApplication.timeSinceStartup < SessionState.GetFloat(Key + "Deadline", 0), "timed out. So far: " + report);
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (runner == null) runner = new GameObject("Wyvern Flight Smoke").AddComponent<WyvernFlightRunner>();
            if (!done) return;
            Finish(0, "WYVERN_FLIGHT_PASS:");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish(1, "WYVERN_FLIGHT_FAIL after:");
        }
    }

    private static void Finish(int code, string label)
    {
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory(Out);
        File.WriteAllText(Path.Combine(Out, "wyvern_flight.txt"), label + report.Replace(";", ";\n"));
        Debug.Log(label + report);
        EditorApplication.Exit(code);
    }

    internal static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}

public sealed class WyvernFlightRunner : MonoBehaviour
{
    private static void Note(string line) => WyvernFlightSmoke.report += " " + line + ";";
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

    private struct Sample { public float t, y, speed, roll, pitch, lift; public string clip; }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(3f);
        var boss = FindAnyObjectByType<Briarwing>();
        WyvernFlightSmoke.Require(boss != null, "no Briarwing");
        Transform player = FindAnyObjectByType<CharacterController>()?.transform;
        WyvernFlightSmoke.Require(player != null, "no player");
        // Stand the player 16 m in front of it so it is engaged and has a target.
        player.GetComponent<CharacterController>().enabled = false;
        player.position = boss.transform.position + boss.transform.forward * 16f + Vector3.up * 0.2f;
        player.GetComponent<CharacterController>().enabled = true;
        yield return new WaitForSeconds(2.5f);
        // Keep the player standing still and unhurt: this is about the boss's flight.
        var health = player.GetComponent<SignalLost.Combat.Health>();
        yield return Fly(boss, "DiveBomb", true, health);
        yield return new WaitForSeconds(1.5f);
        yield return Fly(boss, "SpinDive", false, health);
        WyvernFlightSmoke.done = true;
    }

    private IEnumerator Fly(Briarwing boss, string move, bool strip, SignalLost.Combat.Health health)
    {
        Type duel = typeof(DuelBoss);
        var attacks = (IList)duel.GetField("Attacks", Any).GetValue(boss);
        object attack = null;
        foreach (object a in attacks) if ((string)a.GetType().GetField("Name").GetValue(a) == move) attack = a;
        WyvernFlightSmoke.Require(attack != null, "no " + move + " attack");
        var routine = (IEnumerator)typeof(Briarwing).GetMethod(move, Any).Invoke(boss, null);
        duel.GetMethod("Run", Any).Invoke(boss, new object[] { routine, move, attack });
        PropertyInfo playing = duel.GetProperty("Playing", Any), current = duel.GetProperty("CurrentMove", Any) ?? typeof(DesertMiniBoss).GetProperty("CurrentMove", Any);
        MethodInfo beat = typeof(Briarwing).GetMethod("BeatLift", Any);
        Transform visual = boss.transform.Find("Visual");
        var samples = new List<Sample>();
        Camera camera = null;
        RenderTexture target = null;
        var frames = new List<Texture2D>();
        Vector3 start = boss.transform.position;
        if (strip)
        {
            camera = new GameObject("Flight Camera").AddComponent<Camera>();
            camera.fieldOfView = 55f;
            camera.farClipPlane = 800f;
            target = new RenderTexture(480, 320, 24) { antiAliasing = 4 };
            camera.targetTexture = target;
        }
        float t0 = Time.time, nextShot = 0f;
        Vector3 last = boss.transform.position;
        while (Time.time - t0 < 14f)
        {
            if (health != null) health.Heal(1000f);
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 p = boss.transform.position;
            Vector3 flat = p - last;
            flat.y = 0f;
            Vector3 euler = visual != null ? visual.localEulerAngles : Vector3.zero;
            samples.Add(new Sample
            {
                t = Time.time - t0, y = p.y - start.y, speed = flat.magnitude / dt, clip = (string)playing.GetValue(boss),
                roll = Mathf.DeltaAngle(0f, euler.z), pitch = Mathf.DeltaAngle(0f, euler.x), lift = (float)beat.Invoke(boss, null)
            });
            last = p;
            if (strip && Time.time - t0 >= nextShot && frames.Count < 24)
            {
                nextShot += 0.4f;
                // A fixed wide camera beside the arena, aimed at the boss.
                camera.transform.position = start + new Vector3(-34f, 9f, -6f);
                camera.transform.LookAt(p + Vector3.up * 3f);
                camera.Render();
                RenderTexture.active = target;
                var shot = new Texture2D(480, 320, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, 480, 320), 0, 0);
                shot.Apply();
                RenderTexture.active = null;
                frames.Add(shot);
            }
            string moving = current != null ? (string)current.GetValue(boss) : move;
            if (Time.time - t0 > 1f && string.IsNullOrEmpty(moving)) break;
            yield return null;
        }
        float took = Time.time - t0;
        Directory.CreateDirectory(WyvernFlightSmoke.Out);
        var csv = new StringBuilder("t,height,speed,roll,pitch,lift,clip\n");
        foreach (Sample s in samples) csv.AppendLine($"{s.t:0.000},{s.y:0.00},{s.speed:0.00},{s.roll:0.0},{s.pitch:0.0},{s.lift:0.00},{s.clip}");
        File.WriteAllText(Path.Combine(WyvernFlightSmoke.Out, $"wyvern_{move}.csv"), csv.ToString());
        float top = samples.Max(s => s.y);
        // Climb rate per frame, split by the wing's phase while it is going up.
        var climbing = new List<(float rate, float lift)>();
        for (int i = 1; i < samples.Count; i++)
        {
            float dt = samples[i].t - samples[i - 1].t;
            if (dt <= 0f || samples[i].clip == "Plunge" || samples[i].clip == "SpinDive") continue;
            climbing.Add(((samples[i].y - samples[i - 1].y) / dt, samples[i].lift));
        }
        var down = climbing.Where(c => c.lift > 1.2f && c.rate > 0.05f).Select(c => c.rate).ToList();
        var up = climbing.Where(c => c.lift < 0.6f && c.lift > 0.01f && c.rate > -50f).Select(c => c.rate).ToList();
        float downAvg = down.Count > 0 ? down.Average() : 0f, upAvg = up.Count > 0 ? up.Average() : 0f;
        // The fastest sustained rise, over 0.1 s windows (single frames in batch mode are about 1 ms, too noisy).
        float maxRise = 0f;
        for (int i = 0, j = 0; i < samples.Count; i++)
        {
            while (j < samples.Count && samples[j].t - samples[i].t < 0.1f) j++;
            if (j >= samples.Count) break;
            if (samples[j].clip == "Plunge" || samples[j].clip == "SpinDive" || samples[i].clip == "Impact") continue;
            maxRise = Mathf.Max(maxRise, (samples[j].y - samples[i].y) / (samples[j].t - samples[i].t));
        }
        float maxRoll = samples.Max(s => Mathf.Abs(s.roll));
        string clips = string.Join(" > ", samples.Select(s => s.clip).Where(c => c != null).Aggregate(new List<string>(), (l, c) => { if (l.Count == 0 || l[l.Count - 1] != c) l.Add(c); return l; }));
        Note($"{move}: {took:0.0} s, top {top:0.0} m, climb {downAvg:0.0} m/s on downstrokes vs {upAvg:0.0} m/s on upstrokes, fastest rise {maxRise:0.0} m/s, most bank {maxRoll:0} deg, clips {clips}");
        WyvernFlightSmoke.Require(samples.Last().y < 0.6f, move + ": didn't come back to the ground");
        WyvernFlightSmoke.Require(downAvg > upAvg + 0.5f, move + ": the climb doesn't follow the wingbeats");
        WyvernFlightSmoke.Require(maxRise < 16f, move + ": rises far too fast for a heavy flyer");
        if (move == "DiveBomb")
        {
            WyvernFlightSmoke.Require(top > 10f, "the dive bomb didn't climb high");
            WyvernFlightSmoke.Require(clips.Contains("Flap") && clips.Contains("Plunge"), "the dive bomb skipped the flap or the plunge");
        }
        if (strip && frames.Count > 0)
        {
            int cols = 6, rows = (frames.Count + cols - 1) / cols;
            var sheet = new Texture2D(480 * cols, 320 * rows, TextureFormat.RGB24, false);
            for (int i = 0; i < frames.Count; i++)
                sheet.SetPixels(i % cols * 480, (rows - 1 - i / cols) * 320, 480, 320, frames[i].GetPixels());
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(WyvernFlightSmoke.Out, "wyvern_divebomb_ingame.png"), sheet.EncodeToPNG());
            Destroy(camera.gameObject);
        }
    }
}
