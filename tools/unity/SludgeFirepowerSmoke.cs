// Install only in a disposable project's Assets/Editor; never in the real project.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using SignalLost.Combat;
using SignalLost.Enemies.Desert;
using SignalLost.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SludgeFirepowerSmoke
{
    private const string Key = "SignalLost.SludgeFirepowerSmoke";
    private const string Prefab = "Assets/Prefabs/Enemies/Desert/MiniBosses/Sludge.prefab";
    private static readonly Dictionary<string, float> Tuning = new Dictionary<string, float>
    {
        { "walkSpeed", 4.5f }, { "sprayWindup", 0.6f }, { "phaseTwoSprayWindup", 0.45f },
        { "sprayTime", 0.4f }, { "sprayRange", 20f }, { "sprayScatter", 5f }, { "slickRadius", 2.6f },
        { "sprayCooldown", 3f }, { "phaseTwoSprayCooldown", 2f }, { "igniteWindup", 0.45f }, { "igniteCooldown", 2f },
        { "torchRange", 10f }, { "torchWindup", 0.4f }, { "torchCooldown", 4.5f },
        { "cannonCharge", 1.6f }, { "phaseTwoCannonCharge", 1.2f }, { "cannonShellSpeed", 60f },
        { "cannonRecover", 0.8f }, { "firstCannonDelay", 5f }, { "cannonCooldown", 9f }, { "phaseTwoCannonCooldown", 6f },
    };
    private static Sludge boss;
    private static NavMeshAgent agent;
    private static FirstPersonController player;
    private static float since;
    private static Vector3 beforeMove;
    private static int stage;
    private static string error;
    private static readonly List<string> Report = new List<string>();

    static SludgeFirepowerSmoke()
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
        try
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            string temporary = Path.GetFullPath(Path.GetTempPath()).Replace('\\', '/').TrimEnd('/') + "/";
            Require(Application.isBatchMode && project.StartsWith(temporary, StringComparison.OrdinalIgnoreCase), "Disposable temporary batch project required.");
            ValidateTuning(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab).GetComponent<Sludge>());
            var fresh = new GameObject("Default tuning check");
            fresh.SetActive(false);
            try { ValidateTuning(fresh.AddComponent<Sludge>()); }
            finally { Object.DestroyImmediate(fresh); }
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            Near(saved.GetComponent<NavMeshAgent>().speed, 4.5f, "Saved movement");
            Near(saved.GetComponent<NavMeshAgent>().angularSpeed, 540f, "Saved turning");
            Near(saved.GetComponent<Health>().Max, 440f, "Health unchanged");
            var data = new SerializedObject(saved.GetComponent<Sludge>());
            Near(data.FindProperty("cannonDamage").floatValue, 45f, "Cannon damage unchanged");
            Near(data.FindProperty("torchDamage").floatValue, 6f, "Torch damage unchanged");
            Near(data.FindProperty("torchTick").floatValue, 0.25f, "Torch damage interval unchanged");
            Near(data.FindProperty("fire.damage").floatValue, 9f, "Ground fire damage unchanged");
            EditorSceneManager.OpenScene("Assets/Scenes/Bosses/SludgeTest.unity");
            SessionState.SetBool(Key, true);
            SessionState.SetFloat(Key + "Deadline", (float)EditorApplication.timeSinceStartup + 90f);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception exception) { Fail(exception); }
    }

    private static void ValidateTuning(Sludge sludge)
    {
        var data = new SerializedObject(sludge);
        foreach (var setting in Tuning) Near(data.FindProperty(setting.Key).floatValue, setting.Value, setting.Key);
        Require(data.FindProperty("globsPerSpray").intValue == 6 && data.FindProperty("phaseTwoGlobsPerSpray").intValue == 9, "Volley counts unchanged.");
    }

    private static void Update()
    {
        if (!SessionState.GetBool(Key, false)) return;
        try
        {
            Require(error == null, error);
            Require(EditorApplication.timeSinceStartup < SessionState.GetFloat(Key + "Deadline", 0), "Sludge smoke timed out.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < 2f) return;
            if (stage == 0)
            {
                boss = Object.FindAnyObjectByType<Sludge>();
                player = Object.FindAnyObjectByType<FirstPersonController>();
                Require(boss != null && player != null, "Sludge/player missing.");
                ValidateTuning(boss); // Includes any saved scene overrides.
                foreach (var spawner in Object.FindObjectsByType<FodderSpawner>(FindObjectsSortMode.None)) { spawner.enabled = false; spawner.Clear(); }
                boss.enabled = false;
                player.enabled = false;
                Set(boss, "target", player.transform);
                agent = boss.GetComponent<NavMeshAgent>();
                Require(agent.enabled && agent.isOnNavMesh, "Sludge NavMesh unavailable.");
                beforeMove = boss.transform.position;
                Call(boss, "Chase", beforeMove + Vector3.right * 8f, 4.5f);
                Near(agent.speed, 4.5f, "Runtime movement speed");
                since = Time.time; stage = 1;
            }
            else if (stage == 1 && Time.time - since >= 0.6f)
            {
                Require(Vector3.Distance(beforeMove, boss.transform.position) > 1f, "Sludge failed to move on the NavMesh.");
                agent.isStopped = true;
                boss.transform.rotation = Quaternion.identity;
                MovePlayer(boss.transform.position + Vector3.forward * 12f);
                Spray(1, 6, 3f);
                since = Time.time; stage = 2;
            }
            else if (stage == 2 && Time.time - since >= 1.5f)
            {
                Require(OilSlick.All.Count == 6, "Phase 1 oil did not land.");
                foreach (var slick in OilSlick.All) Near(slick.Radius, 2.6f, "Landed oil radius");
                foreach (var slick in new List<OilSlick>(OilSlick.All)) Object.DestroyImmediate(slick.gameObject);
                Spray(2, 9, 2f);
                since = Time.time; stage = 3;
            }
            else if (stage == 3 && Time.time - since >= 1.5f)
            {
                Require(OilSlick.All.Count == 9, "Phase 2 oil did not land.");
                foreach (var slick in new List<OilSlick>(OilSlick.All)) Object.DestroyImmediate(slick.gameObject);
                // Fire through the actual Sludge attack path, then advance the projectile a known timestep.
                foreach (int phase in new[] { 1, 2 })
                {
                    typeof(DesertMiniBoss).GetProperty("Phase").SetValue(boss, phase);
                    Set(boss, "cannonAim", boss.transform.position + Vector3.up * 8f + Vector3.forward * 20f);
                    Call(boss, "FireCannon");
                    var shell = Object.FindAnyObjectByType<FuelShell>();
                    Require(shell != null, "Cannon did not launch a shell.");
                    Vector3 from = shell.transform.position;
                    Call(shell, "Update");
                    Near(Vector3.Distance(from, shell.transform.position), 60f * Time.deltaTime, "Cannon flight distance");
                    Near((float)Get(boss, "nextCannon") - Time.time, phase == 1 ? 9f : 6f, "Cannon cooldown");
                    Object.DestroyImmediate(shell.gameObject);
                }
                boss.transform.rotation = Quaternion.identity;
                Vector3 nozzle = ((Transform)Get(boss, "hoseTip")).position;
                MovePlayer(new Vector3(nozzle.x, boss.transform.position.y, nozzle.z + 9f));
                var health = player.GetComponent<Health>();
                health.Restore();
                Call(boss, "BeginTorch");
                Set(boss, "nextTorchTick", Time.time - 1f);
                float before = health.Current;
                Physics.SyncTransforms();
                Call(boss, "UpdateTorch", 0f);
                Vector3 toPlayer = player.transform.position + Vector3.up * 0.9f - nozzle;
                Require(toPlayer.magnitude > 6.5f && toPlayer.magnitude < 10f, "Torch probe must be beyond the old range and inside the new range.");
                Require(Vector3.Angle((Vector3)Get(boss, "torchDirection"), toPlayer) < 16f, "Torch probe is outside the flame cone.");
                Near(before - health.Current, 6f, "Flamethrower reaches player 9 m ahead of nozzle");
                Report.Add("SLUDGE_FIREPOWER_PASS: source defaults, saved prefab and scene; NavMesh movement; 6/9 faster oil globs and 2.6 m slicks; 60 m/s cannon and 9/6 s cooldowns; 9 m nozzle-relative torch damage; unchanged health/damage settings.");
                Directory.CreateDirectory("Logs"); File.WriteAllLines("Logs/SludgeFirepower.txt", Report);
                Debug.Log(Report[0]); SessionState.SetBool(Key, false); EditorApplication.Exit(0);
            }
        }
        catch (Exception exception) { Fail(exception); }
    }

    private static void Spray(int phase, int count, float cooldown)
    {
        typeof(DesertMiniBoss).GetProperty("Phase").SetValue(boss, phase);
        Type state = typeof(Sludge).GetNestedType("State", BindingFlags.NonPublic);
        Call(boss, "Enter", Enum.Parse(state, "Spray"));
        Set(boss, "stateTime", 0.4f);
        Call(boss, "UpdateSpray");
        var globs = Object.FindObjectsByType<OilGlob>(FindObjectsSortMode.None);
        Require(globs.Length == count, "Wrong oil volley count.");
        foreach (var glob in globs)
        {
            Vector3 point = (Vector3)Get(glob, "to") - boss.transform.position; point.y = 0f;
            Near((float)Get(glob, "flight"), 0.45f + point.magnitude * 0.02f, "Faster oil flight");
            Near((float)Get(glob, "slickRadius"), 2.6f, "Oil payload radius");
        }
        Near((float)Get(boss, "nextSpray") - Time.time, cooldown, "Oil cooldown");
    }

    private static void MovePlayer(Vector3 point)
    { var cc = player.GetComponent<CharacterController>(); cc.enabled = false; player.transform.position = point; cc.enabled = true; }
    private static FieldInfo Field(object obj, string name)
    {
        for (Type type = obj.GetType(); type != null; type = type.BaseType)
        { var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly); if (field != null) return field; }
        throw new MissingFieldException(name);
    }
    private static object Get(object obj, string name) => Field(obj, name).GetValue(obj);
    private static void Set(object obj, string name, object value) => Field(obj, name).SetValue(obj, value);
    private static void Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args);
    private static void Near(float actual, float expected, string label) => Require(Mathf.Abs(actual - expected) < 0.02f, label + ": expected " + expected + ", got " + actual);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Fail(Exception exception)
    { SessionState.SetBool(Key, false); Debug.LogException(exception); EditorApplication.Exit(1); }
}
