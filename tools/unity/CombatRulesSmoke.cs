using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SignalLost.Combat;
using SignalLost.Enemies;
using SignalLost.Enemies.Desert;
using SignalLost.NPC;
using SignalLost.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Play Mode smoke for the shared combat rules (tutorial overhaul phase 1): the fighting-radius leash, the
// finisher-only 1 s stun, the charge-scaled bolt cooldown and the full-charge area blast. Install only in a
// disposable project copy under the system temp directory (Assets/Editor), then run:
//   Unity -batchmode -nographics -projectPath <temp-project> -executeMethod CombatRulesSmoke.Start -logFile <log>
// Omit -quit; it exits with 0 on pass and 1 on failure. Drives the game through code, not physical input.
[InitializeOnLoad]
public static class CombatRulesSmoke
{
    private const string Key = "SignalLost.CombatRulesSmoke";
    private const string EnemyScene = "Assets/Scenes/Initial Test Scenes/EnemyTest.unity";
    private const string BossScene = "Assets/Scenes/Bosses/BurnoutTest.unity";
    private static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    private static readonly List<string> report = new List<string>();
    private static bool started;
    private static int frame = -1;
    private static float resume;

    static CombatRulesSmoke()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += (m, trace, type) =>
        {
            if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception) &&
                !trace.Contains("UnityEditor.Search.SearchDatabase")) Fail(new Exception(m + "\n" + trace));
        };
    }

    public static void Start()
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        if (!Application.isBatchMode || !(project.StartsWith(Path.GetFullPath(Path.GetTempPath())) || project.StartsWith("/private/tmp/")))
            throw new InvalidOperationException("Run in a disposable batch-mode project under the system temporary directory.");
        EditorSceneManager.OpenScene(EnemyScene);
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + "Time", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Time", 0) > 300) { Fail(new Exception("Timed out.")); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < resume || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (!started) { started = true; steps.Push(Run()); }
            while (steps.Count > 0)
            {
                IEnumerator top = steps.Peek();
                if (!top.MoveNext()) { steps.Pop(); continue; }
                if (top.Current is IEnumerator nested) { steps.Push(nested); continue; }
                resume = Time.time + (top.Current is float seconds ? seconds : 0);
                return;
            }
            SessionState.SetBool(Key, false);
            Directory.CreateDirectory("Logs"); File.WriteAllLines("Logs/CombatRulesSmoke.txt", report);
            Debug.Log("COMBAT_RULES_SMOKE_PASS\n" + string.Join("\n", report));
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static IEnumerator Run()
    {
        yield return 1f;
        var player = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
        var laser = player.GetComponent<MiningLaser>();
        var view = player.GetComponentInChildren<Camera>();
        player.enabled = false;
        var crabs = UnityEngine.Object.FindObjectsByType<HermitCrab>();
        var crab = crabs[0];
        foreach (var other in crabs.Skip(1)) other.gameObject.SetActive(false);
        Vector3 home = crab.Territory.Centre;

        // ---------- Fighting radius ----------
        Place(player, home + Vector3.forward * 14f);
        view.transform.LookAt(crab.transform.position);
        yield return 1.5f;
        Check(Get<object>(crab, "state").ToString() == "Idle", "A crab ignores a visible player outside its 12 m fight radius.");
        Place(player, home + Vector3.forward * 9f);
        yield return 1f;
        Check(Get<object>(crab, "state").ToString() != "Idle", "It attacks once the player steps inside the radius.");
        crab.GetComponent<Health>().TakeDamage(40f, player.gameObject);
        Place(player, home + Vector3.forward * 26f);
        for (int i = 0; i < 40 && !crab.IsReturningHome; i++) yield return 0.1f; // an attack in progress finishes first
        Check(crab.IsReturningHome, "It gives up once the player is more than twice the radius away.");
        for (int i = 0; i < 80 && crab.IsReturningHome; i++) yield return 0.1f;
        var health = crab.GetComponent<Health>();
        Check(!crab.IsReturningHome && Mathf.Approximately(health.Current, health.Max), "It walks home and heals to full.");

        // ---------- Stun only from the combo finisher ----------
        Place(player, home + Vector3.forward * 30f); // out of reach while the crab is hit by script
        yield return 0.3f;
        for (int i = 0; i < 4; i++)
        {
            health.TakeDamage(20f, player.gameObject);
            yield return 0.15f;
            Check(!crab.IsStunned, $"Plain 20-damage hit {i + 1} does not stun or stagger the crab (no stun-lock).");
        }
        crab.Stun(laser.FinisherStun);
        Check(crab.IsStunned && Mathf.Approximately(laser.FinisherStun, 1f), "The combo finisher stuns it for 1 s.");
        yield return 1.2f;
        Check(!crab.IsStunned, "The stun wears off after 1 s.");

        var frog = Spawn<MortarFrog>("Assets/Prefabs/Enemies/MortarFrog.prefab", home + Vector3.right * 8f);
        var armadillo = Spawn<HubcapArmadillo>("Assets/Prefabs/Enemies/Desert/HubcapArmadillo.prefab", home + Vector3.left * 8f);
        yield return 0.5f;
        frog.Stun(1f);
        armadillo.Stun(1f);
        Check(frog.IsStunned && armadillo.IsStunned, "Frogs and desert fodder are stunnable too.");
        yield return 1.3f;
        Check(!frog.IsStunned && !armadillo.IsStunned, "Their stuns also last 1 s.");
        Check(armadillo.Territory.Radius > 0f && frog.Territory.Radius > crab.Territory.Radius, "Every enemy type has its own fight radius.");
        var merchant = Spawn<MerchantCrab>("Assets/Prefabs/NPC/MerchantCrab.prefab", home + Vector3.back * 8f);
        yield return 0.3f;
        var merchantBrain = merchant.GetComponent<HermitCrab>();
        merchantBrain.enabled = true;
        merchantBrain.Stun(1f);
        Check(!merchantBrain.IsStunned && !merchantBrain.Stunnable, "The merchant crab (a mini-boss) is immune to the stun.");
        UnityEngine.Object.Destroy(merchant.gameObject);

        // ---------- Bolt cooldown scales with charge ----------
        Set(laser, "shotCooldown", 8f);
        Set(laser, "tapShotCooldown", 4f);
        Check(Mathf.Approximately(laser.CooldownFor(0.1f), 4f) && Mathf.Approximately(laser.CooldownFor(1f), 8f) &&
              Mathf.Approximately(laser.CooldownFor(0.55f), 6f) && Mathf.Approximately(laser.CooldownFor(0f), 4f),
            "Bolt cooldown: 4 s at 10% charge or less, 6 s at 55%, 8 s at full.");

        // ---------- Full-charge blast ----------
        foreach (var e in new Component[] { frog, armadillo }) UnityEngine.Object.Destroy(e.gameObject);
        crab.gameObject.SetActive(false);
        var pack = new List<Health>();
        Vector3 packCentre = home + Vector3.forward * 4f;
        for (int i = 0; i < 4; i++)
        {
            var c = Spawn<HermitCrab>("Assets/Prefabs/Enemies/HermitCrab.prefab", packCentre + Quaternion.Euler(0f, i * 90f, 0f) * Vector3.forward * 1.8f);
            c.Territory.Zone = null;
            pack.Add(c.GetComponent<Health>());
        }
        yield return 0.5f;
        foreach (var c in pack) c.GetComponent<HermitCrab>().enabled = false; // hold still for the shot
        Place(player, packCentre + Vector3.back * 10f);
        view.transform.LookAt(pack[0].transform.position + Vector3.up * 0.4f);
        Physics.SyncTransforms();
        float before = player.GetComponent<Health>() != null ? player.GetComponent<Health>().Current : 0f;
        Invoke(laser, "FireBolt", 1f);
        yield return 0.8f;
        int hurt = pack.Count(h => h.Current < h.Max);
        Check(hurt >= 3, $"A full-charge bolt bursts and hurts the pack ({hurt} of 4 crabs damaged).");
        Check(player.GetComponent<Health>() == null || player.GetComponent<Health>().Current == before, "The blast never hurts the player.");
        float weakest = pack.Where(h => h.Current < h.Max).Min(h => h.Max - h.Current);
        Check(weakest >= laser.MaxShotDamage * 0.5f - 0.01f, "Splash damage never drops below half at the edge.");
        Set(laser, "cooldownUntil", 0f);
        foreach (var h in pack) h.Restore();
        Invoke(laser, "FireBolt", 0.5f);
        yield return 0.8f;
        Check(pack.Count(h => h.Current < h.Max) <= 1, "Shots below full charge stay single-target.");

        // ---------- Mini-boss arena follows the same rule ----------
        EditorSceneManager.LoadSceneInPlayMode(BossScene, new LoadSceneParameters(LoadSceneMode.Single));
        yield return 2f;
        player = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
        player.enabled = false;
        var boss = UnityEngine.Object.FindAnyObjectByType<SignalLost.Enemies.Desert.DesertMiniBoss>();
        float arena = Get<float>(boss, "arenaRadius");
        Vector3 bossHome = Get<Vector3>(boss, "<Home>k__BackingField");
        Place(player, bossHome + Vector3.back * (arena + 3f));
        yield return 1f;
        Check(!boss.Engaged, "A mini-boss ignores the player just outside its arena.");
        Place(player, bossHome + Vector3.back * (arena - 4f));
        yield return 0.6f;
        Check(boss.Engaged, "It engages inside its arena.");
        Place(player, bossHome + Vector3.back * (arena * 2f + 3f));
        yield return 0.5f;
        Check(!boss.Engaged, "It resets once the player is twice the arena radius away.");
    }

    // ---------- Helpers ----------

    private static T Spawn<T>(string prefab, Vector3 at) where T : Component =>
        UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefab), at, Quaternion.identity).GetComponent<T>();

    private static void Place(FirstPersonController player, Vector3 at)
    {
        if (Physics.Raycast(at + Vector3.up * 30f, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore)) at = hit.point;
        var body = player.GetComponent<CharacterController>();
        body.enabled = false;
        player.transform.position = at + Vector3.up * 0.05f;
        body.enabled = true;
        Physics.SyncTransforms();
    }

    private static T Get<T>(object o, string field)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f != null) return (T)f.GetValue(o);
        }
        throw new MissingFieldException(o.GetType().Name, field);
    }

    private static void Set(object o, string field, object value) =>
        o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, value);

    private static void Invoke(object o, string method, params object[] args) =>
        o.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, args);

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); report.Add(message); }

    private static void Fail(Exception ex)
    {
        SessionState.SetBool(Key, false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/CombatRulesSmokeFailure.txt", ex + "\n" + string.Join("\n", report));
        Debug.LogError(ex); EditorApplication.Exit(1);
    }
}
