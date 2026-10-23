// Install only in a disposable project's Assets/Editor; never in the real project.
// Batch Play Mode smoke for the Chief Toad test arena: the fight starts, he attacks in all three phases, the phase
// changes happen, nothing throws, he stays on the ground and the NavMesh, and his cooling system drops and counts for
// the tutorial (not the desert). Run:
//   Unity -batchmode -projectPath <temp-project> -executeMethod ChiefToadSmoke.Start -logFile <log>
// Omit -quit (it exits itself). Not a physical-input or balance test.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SignalLost.Combat;
using SignalLost.Enemies.Desert;
using SignalLost.Enemies.Meadow;
using SignalLost.Player;
using SignalLost.Progression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ChiefToadSmoke
{
    private const string Key = "SignalLost.ChiefToadSmoke";
    private static bool spawned;
    private static string error;
    internal static readonly List<string> Report = new List<string>();
    internal static readonly List<string> Failures = new List<string>();

    static ChiefToadSmoke()
    {
        EditorApplication.update += Update;
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (trace.Contains("UnityEditor.Search.SearchDatabase")) return; // known editor start-up noise
            error = message + "\n" + trace;
        };
    }

    public static void Start()
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
        string temporary = Path.GetFullPath(Path.GetTempPath()).Replace('\\', '/').TrimEnd('/') + "/";
        if (!Application.isBatchMode || !project.StartsWith(temporary, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Disposable temporary batch project required.");
        EditorSceneManager.OpenScene("Assets/Scenes/Bosses/ChiefToadTest.unity");
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + "Deadline", (float)EditorApplication.timeSinceStartup + 300f);
        EditorApplication.EnterPlaymode();
    }

    private static void Update()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (error != null || EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + "Deadline", 0))
        {
            Failures.Add(error ?? "Smoke timed out.");
            Finish();
            return;
        }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < 2f || spawned) return;
        spawned = true;
        new GameObject("Chief Toad Probe").AddComponent<ChiefToadProbe>();
    }

    internal static void Check(bool ok, string message)
    {
        Report.Add((ok ? "PASS: " : "FAIL: ") + message);
        if (!ok) Failures.Add(message);
    }

    internal static void Finish()
    {
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/ChiefToadSmoke.txt", Report);
        string summary = string.Join("\n", Report);
        if (Failures.Count > 0) Debug.LogError("CHIEF_TOAD_SMOKE_FAIL: " + string.Join("; ", Failures) + "\n" + summary);
        else Debug.Log("CHIEF_TOAD_SMOKE_PASS\n" + summary);
        EditorApplication.Exit(Failures.Count == 0 ? 0 : 1);
    }
}

public sealed class ChiefToadProbe : MonoBehaviour
{
    private ChiefToad toad;
    private Health toadHealth, playerHealth;
    private FirstPersonController player;
    private CharacterController body;
    private readonly HashSet<string> states = new HashSet<string>();
    private readonly HashSet<SplashWave> waves = new HashSet<SplashWave>();
    private int bladesSeen, wavesSeen, stunsSeen;
    private bool wasStunned, leftGround;

    private IEnumerator Start()
    {
        toad = Object.FindAnyObjectByType<ChiefToad>();
        player = Object.FindAnyObjectByType<FirstPersonController>();
        ChiefToadSmoke.Check(toad != null && player != null, "The arena has the Chief Toad and the player.");
        if (toad == null || player == null) { ChiefToadSmoke.Finish(); yield break; }
        toadHealth = toad.GetComponent<Health>();
        playerHealth = player.GetComponent<Health>();
        body = player.GetComponent<CharacterController>();
        playerHealth.Invulnerable = true; // the fight must not end with the player's death

        MovePlayer(9f, 180f);
        float until = Time.time + 6f;
        while (!toad.Engaged && Time.time < until) yield return null;
        ChiefToadSmoke.Check(toad.Engaged, "Walking in starts the fight.");

        yield return Watch(1, 28f);
        toadHealth.TakeDamage(toadHealth.Current - toadHealth.Max * 0.6f, player.gameObject);
        until = Time.time + 4f;
        while (toad.Stage < 2 && Time.time < until) yield return null;
        ChiefToadSmoke.Check(toad.Stage == 2, "Phase 2 (coolant surge) starts below 66%.");
        yield return Watch(2, 22f);
        toadHealth.TakeDamage(toadHealth.Current - toadHealth.Max * 0.3f, player.gameObject);
        until = Time.time + 4f;
        while (toad.Stage < 3 && Time.time < until) yield return null;
        ChiefToadSmoke.Check(toad.Stage == 3, "Phase 3 (coolant overflow) starts below 33%.");
        yield return Watch(3, 26f);

        ChiefToadSmoke.Check(states.Count >= 4, "It used several attacks: " + string.Join(", ", states));
        ChiefToadSmoke.Report.Add($"INFO: blades seen {bladesSeen}, splash waves seen {wavesSeen}, stuns {stunsSeen}, left the ground {leftGround}.");

        // Defeat: the cooling system drops and counts for the tutorial, not the desert.
        toadHealth.TakeDamage(toadHealth.Current + 1f, player.gameObject);
        yield return new WaitForSeconds(2.5f);
        ShipPartDrop drop = Object.FindAnyObjectByType<ShipPartDrop>();
        ChiefToadSmoke.Check(drop != null, "Defeated, it drops the cooling system.");
        if (drop != null)
        {
            Teleport(drop.transform.position + Vector3.back * 1f);
            yield return new WaitForSeconds(1.5f);
            ChiefToadSmoke.Check(ShipPartDrop.CountFor(StageProgress.Tutorial) == 1 && ShipPartDrop.CountFor(StageProgress.Desert) == 0,
                "The cooling system counts for the tutorial stage, not the desert's three parts.");
        }
        ChiefToadSmoke.Finish();
    }

    // Moves the player round the toad every few seconds (in and out of scream and slam range) and records what it does.
    private IEnumerator Watch(int stage, float seconds)
    {
        var seen = new HashSet<string>();
        float end = Time.time + seconds, nextMove = 0f;
        int step = 0;
        while (Time.time < end)
        {
            if (toad == null || toadHealth.IsDead) break;
            if (Time.time >= nextMove)
            {
                nextMove = Time.time + 4f;
                MovePlayer(step % 2 == 0 ? 8f : 15f, step * 70f);
                step++;
            }
            string state = toad.StateName;
            if (state != "Approach" && state != "Recover") { seen.Add(state); states.Add(state); }
            Vector3 p = toad.transform.position;
            if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z)) { ChiefToadSmoke.Check(false, "Its position stays finite."); break; }
            if (state == "SlamAir") leftGround = true;
            bladesSeen = Mathf.Max(bladesSeen, Object.FindObjectsByType<WaterBlade>().Length);
            foreach (SplashWave wave in Object.FindObjectsByType<SplashWave>()) waves.Add(wave);
            wavesSeen = waves.Count;
            bool stunned = player.IsStunned;
            if (stunned && !wasStunned) stunsSeen++;
            wasStunned = stunned;
            yield return null;
        }
        ChiefToadSmoke.Report.Add($"INFO: phase {stage} attacks: {string.Join(", ", seen)}");
        var agent = toad.GetComponent<UnityEngine.AI.NavMeshAgent>();
        ChiefToadSmoke.Check(toadHealth.IsDead || toad.StateName == "SlamAir" || (agent != null && agent.enabled && agent.isOnNavMesh),
            $"After phase {stage} it is back on the NavMesh.");
    }

    private void MovePlayer(float distance, float angle)
    {
        Vector3 around = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
        Vector3 at = toad.transform.position + around * distance;
        if (Physics.Raycast(at + Vector3.up * 30f, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore)) at = hit.point;
        Teleport(at + Vector3.up * 0.1f);
        player.transform.rotation = Quaternion.LookRotation(Flat(toad.transform.position - at));
    }

    private void Teleport(Vector3 at)
    {
        body.enabled = false;
        player.transform.position = at;
        body.enabled = true;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.001f ? v : Vector3.forward;
    }
}
