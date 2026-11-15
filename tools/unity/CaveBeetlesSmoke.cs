// Install only in a disposable project's Assets/Editor; never in the real project.
// Batch: -executeMethod CaveBeetlesSmoke.Start (no -quit; it exits itself).
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
public static class CaveBeetlesSmoke
{
    private const string Key = "SignalLost.CaveBeetlesSmoke";
    private static float started = -1f, stageTime, mark;
    private static int stage, chargeHits;
    private static float maxFromCentre;
    private static string error, report = "";
    private static readonly List<float> hits = new List<float>(); // every hit a beetle landed on the player
    private static CaveBeetle subject;
    private static float subjectHealth;
    private static Vector3 boxCentre;

    static CaveBeetlesSmoke()
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
        CaveBeetleBuilder.Validate();
        EditorSceneManager.OpenScene(CaveBeetleBuilder.ScenePath);
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + "Deadline", (float)EditorApplication.timeSinceStartup + 420);
        EditorApplication.EnterPlaymode();
    }

    private static string Formation(BeetleBand band) =>
        typeof(BeetleBand).GetField("formation", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(band).ToString();

    private static IList Bars() =>
        (IList)typeof(MiniBossHud).GetField("Engaged", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

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
            var band = Object.FindAnyObjectByType<BeetleBand>();
            CaveBeetle[] beetles = Object.FindObjectsByType<CaveBeetle>(FindObjectsSortMode.InstanceID);
            Require(player != null && band != null && beetles.Length == 4, "Expected the player, the band and four beetles.");
            Health playerHealth = player.GetComponent<Health>();
            if (stage > 0) playerHealth.Restore(); // keep the player alive so the beetles don't reset mid-test
            foreach (CaveBeetle beetle in beetles)
            {
                if (beetle.IsDead) continue;
                Vector3 offset = beetle.transform.position - band.transform.position;
                offset.y = 0f;
                maxFromCentre = Mathf.Max(maxFromCentre, offset.magnitude);
            }

            stageTime += Time.deltaTime;
            switch (stage)
            {
                case 0:
                    if (t < 2.5f) return;
                    foreach (CaveBeetle beetle in beetles) Require(!beetle.Engaged, beetle.name + " should ignore the player outside its zone.");
                    Teleport(player, band.transform.position + new Vector3(0f, 0.05f, -8f));
                    playerHealth.Damaged += (_, amount, source) =>
                    {
                        if (source == null || source.GetComponent<CaveBeetle>() == null) return;
                        if (Formation(band) == "Charge") chargeHits++;
                        hits.Add(amount);
                    };
                    Next();
                    break;
                case 1: // the whole band engages under one shared bar
                    if (Array.TrueForAll(beetles, b => b.Engaged))
                    {
                        IList bars = Bars();
                        Require(bars.Contains(band), "The band's shared boss bar is not showing.");
                        foreach (CaveBeetle beetle in beetles) Require(!bars.Contains(beetle), beetle.name + " has its own boss bar; only the band's should show.");
                        Require(band.HealthFraction > 0.99f && band.DisplayPhase == 1, "The band should start at full health in phase 1.");
                        report += "engaged x4, one bar; ";
                        Next();
                    }
                    else Require(stageTime < 6f, "The band did not all engage.");
                    break;
                case 2: // first box-in: be in the air as they charge -> they slam into each other and flip
                    if (Formation(band) == "Charge")
                    {
                        CheckCross(band, beetles);
                        Teleport(player, player.transform.position + Vector3.up * 4f); // airborne for about as long as a real jump
                        Next();
                    }
                    else Require(stageTime < 25f, "No box-in charge within 25 s.");
                    break;
                case 3:
                    if (stageTime < 2.2f) return;
                {
                    int flipped = 0;
                    string why = "chargeHits=" + chargeHits;
                    foreach (CaveBeetle beetle in beetles)
                    {
                        why += " | " + beetle.name + " bumped=" + beetle.Bumped + " flipped=" + beetle.Flipped + " move=" + beetle.CurrentMove;
                        if (!beetle.Bumped) continue;
                        Require(beetle.HealthFraction > 0.999f, beetle.name + " was hurt by the crash; crashes should only flip them.");
                        if (beetle.Flipped) flipped++;
                    }
                    Require(flipped >= 2, "Dodged box-in: expected beetles to crash and flip onto their backs, got " + flipped + ": " + why);
                    Require(chargeHits == 0, "Dodged box-in should not hit the player.");
                    // The belly weak spot: a hit on it while flipped does the bonus damage.
                    subject = Array.Find(beetles, b => b.Flipped);
                    subjectHealth = subject.GetComponent<Health>().Current;
                    Require(Belly(subject).ReadoutAs == subject.GetComponent<Health>(), "Aiming at the belly should show the beetle's own health.");
                    Belly(subject).TakeDamage(10f, player.gameObject);
                    report += "dodge -> " + flipped + " flipped, no crash damage; ";
                    Next();
                    break;
                }
                case 4:
                {
                    float taken = subjectHealth - subject.GetComponent<Health>().Current;
                    Require(Mathf.Abs(taken - 25f) < 0.5f, "A 10-damage hit on a flipped belly should do 25, did " + taken.ToString("F1"));
                    // One belly hit: it rolls straight back over, while its unhit bandmates stay on their backs.
                    if (subject.Flipped)
                    {
                        Require(stageTime < 0.5f, "After one belly hit the beetle should start getting up at once.");
                        return;
                    }
                    Require(subject.CurrentMove == "Flipped", "The beetle should be rolling back over, not " + subject.CurrentMove);
                    Require(Array.Exists(beetles, b => b != subject && b.Flipped), "Unhit beetles should still be on their backs.");
                    subjectHealth = subject.GetComponent<Health>().Current;
                    Belly(subject).TakeDamage(10f, player.gameObject);
                    Next();
                    break;
                }
                case 5: // a second hit gets no belly bonus: it's already rolling back over
                {
                    float taken = subjectHealth - subject.GetComponent<Health>().Current;
                    Require(Mathf.Abs(taken - 10f) < 0.5f, "A second hit after the belly hit should do normal damage (10), did " + taken.ToString("F1"));
                    report += "belly x2.5 once, then it gets up; readout shows the beetle; ";
                    // The next box-in starts near the zone's edge, where slots used to be pulled in and bunch up.
                    Teleport(player, band.transform.position + new Vector3(0f, 0.05f, -21f));
                    Next();
                    break;
                }
                case 6: // second box-in, at the zone's edge: still an even cross; stay on the ground -> hit once, no crash
                    if (Formation(band) == "Charge") { CheckCross(band, beetles); report += "even cross at the zone edge; "; Next(); }
                    else Require(stageTime < 25f, "No second box-in within 25 s.");
                    break;
                case 7:
                    if (stageTime < 2f) return;
                    Require(chargeHits == 1, "Grounded box-in should hit the player exactly once, got " + chargeHits);
                    Require(hits.Contains(30f), "The box-in hit should do 30.");
                    foreach (CaveBeetle beetle in beetles) Require(!beetle.Bumped, beetle.name + " bumped although the player was hit.");
                    report += "grounded -> hit once for 30; ";
                {
                    // The hard shell (upright beetle): laser swings do nothing, a jump smash a quarter, a bolt full damage.
                    CaveBeetle upright = Array.Find(beetles, b => !b.Flipped && !b.IsDead);
                    Health health = upright.GetComponent<Health>();
                    var laser = player.GetComponent<MiningLaser>();
                    var smash = player.GetComponent<JumpSmash>();
                    float before = health.Current;
                    SetFlag(laser, "IsSwinging", true);
                    health.TakeDamage(10f, player.gameObject);
                    SetFlag(laser, "IsSwinging", false);
                    Require(Mathf.Approximately(health.Current, before), "A laser swing on the shell should do nothing, did " + (before - health.Current));
                    SetFlag(smash, "IsLanding", true);
                    health.TakeDamage(40f, player.gameObject);
                    SetFlag(smash, "IsLanding", false);
                    Require(Mathf.Abs(before - health.Current - 10f) < 0.01f, "A 40-damage jump smash on the shell should do 10, did " + (before - health.Current));
                    before = health.Current;
                    health.TakeDamage(10f, player.gameObject);
                    Require(Mathf.Abs(before - health.Current - 10f) < 0.01f, "A bolt on the shell should do full damage.");
                    report += "shell: swing 0, smash 25%, bolt full; ";
                }
                    Next();
                    break;
                case 8: // phase 2 follows the band's combined health
                    if (stageTime < 0.1f)
                    {
                        foreach (CaveBeetle beetle in beetles) HurtTo(beetle, 0.55f, player);
                        return;
                    }
                    if (Array.TrueForAll(beetles, b => b.DuelPhase == 2))
                    {
                        Require(band.DisplayPhase == 2 && band.PhaseTitle == "Performance", "The bar should show phase 2, Performance; got " + band.PhaseTitle);
                        report += "band phase 2 at " + band.HealthFraction.ToString("P0") + "; ";
                        Next();
                    }
                    else Require(stageTime < 12f, "Not every beetle reached phase 2 with the band at " + band.HealthFraction.ToString("P0"));
                    break;
                case 9: // drum stomp: the crystal shockwave hits a grounded player
                    if (stageTime < 0.1f)
                    {
                        subject = beetles[1];
                        Teleport(player, subject.transform.position + subject.transform.forward * 5.5f + Vector3.up * 0.05f);
                        hits.Clear();
                        Require(subject.DebugRunAttack("Stomp"), "No Stomp move.");
                        return;
                    }
                    if (hits.Contains(25f)) { report += "stomp shockwave 25; "; Next(); }
                    else Require(stageTime < 5f, "The stomp's shockwave did not hit the grounded player. Hits: " + string.Join(",", hits));
                    break;
                case 10: // phase 3
                    if (stageTime < 0.1f)
                    {
                        foreach (CaveBeetle beetle in beetles) HurtTo(beetle, 0.28f, player);
                        return;
                    }
                    if (Array.TrueForAll(beetles, b => b.DuelPhase == 3)) { report += "band phase 3; "; Next(); }
                    else Require(stageTime < 12f, "Not every beetle reached phase 3.");
                    break;
                case 11: // stage dive onto a player standing still: it lands on them
                    if (stageTime < 0.1f)
                    {
                        subject = beetles[2];
                        Teleport(player, band.transform.position + Vector3.up * 0.05f);
                        hits.Clear();
                        Require(subject.DebugRunAttack("StageDive"), "No StageDive move.");
                        return;
                    }
                    if (hits.Contains(28f))
                    {
                        Require(!subject.Flipped, "A stage dive that landed should not flip the beetle.");
                        report += "dive hit 28; ";
                        Next();
                    }
                    else Require(stageTime < 5f, "The stage dive missed a player standing still. Hits: " + string.Join(",", hits));
                    break;
                case 12: // stage dive, player runs once the shadow locks: it misses and flips
                    if (stageTime < 1f) return; // let the last dive finish
                    if (mark == 0f)
                    {
                        Teleport(player, band.transform.position + Vector3.up * 0.05f);
                        Require(subject.DebugRunAttack("StageDive"), "No StageDive move.");
                        mark = -1f;
                        return;
                    }
                    if (mark < 0f && subject.transform.position.y > band.transform.position.y + 1.5f) mark = Time.time; // in the air
                    if (mark > 0f && Time.time - mark > 0.5f && subject.CurrentMove == "StageDive" && boxCentre == Vector3.zero)
                    {
                        boxCentre = player.transform.position;
                        Teleport(player, band.transform.position + (band.transform.position - subject.transform.position).normalized * 6f + Vector3.up * 0.05f);
                    }
                    if (subject.Flipped) { report += "dive miss -> flipped; "; Next(); }
                    else Require(stageTime < 6f, "A missed stage dive should leave the beetle on its back.");
                    break;
                case 13: // defeat them one by one: the part and one scrap pile only from the last
                {
                    int scrapBefore = Object.FindObjectsByType<ScrapPickup>(FindObjectsSortMode.None).Length;
                    for (int i = 0; i < 3; i++)
                    {
                        float anger = band.SpeedScale;
                        beetles[i].GetComponent<Health>().TakeDamage(99999f, player.gameObject);
                        Require(band.SpeedScale > anger, "The band should get angrier when a member falls.");
                        Require(Object.FindObjectsByType<ShipPartDrop>(FindObjectsSortMode.None).Length == 0, "Only the last beetle should drop the signal amplifier.");
                        Require(Object.FindObjectsByType<ScrapPickup>(FindObjectsSortMode.None).Length == scrapBefore, "Only the last beetle should drop scrap.");
                    }
                    Require(Bars().Contains(band), "The bar should stay while a beetle is left.");
                    beetles[3].GetComponent<Health>().TakeDamage(99999f, player.gameObject);
                    Require(Object.FindObjectsByType<ShipPartDrop>(FindObjectsSortMode.None).Length == 1, "The last beetle should drop the signal amplifier.");
                    Require(!Bars().Contains(band), "The bar should go when the band is beaten.");
                    int scrap = Object.FindObjectsByType<ScrapPickup>(FindObjectsSortMode.None).Length - scrapBefore;
                    Require(scrap == 12, "The band's loot pile should be 12 scrap, got " + scrap);
                    Require(maxFromCentre <= 31.5f, "A beetle went beyond its zone plus a box-in place: " + maxFromCentre.ToString("F1") + " m from the centre.");
                    report += "defeats -> one loot pile from the last: 12 scrap + signal amplifier; max from centre " + maxFromCentre.ToString("F1") + " m";
                    Directory.CreateDirectory("Logs");
                    File.WriteAllText("Logs/CaveBeetlesPlayMode.txt", "CAVE_BEETLES_PLAY_MODE_PASS: " + report);
                    Debug.Log("CAVE_BEETLES_PLAY_MODE_PASS: " + report);
                    SessionState.SetBool(Key, false);
                    EditorApplication.Exit(0);
                    break;
                }
            }
        }
        catch (Exception exception)
        {
            SessionState.SetBool(Key, false);
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    // Just as the charge starts: every beetle on its place, Box Radius from the middle, evenly spaced (a cross with four).
    private static void CheckCross(BeetleBand band, CaveBeetle[] beetles)
    {
        var live = Array.FindAll(beetles, b => !b.IsDead);
        float radius = (float)typeof(BeetleBand).GetField("boxRadius", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(band);
        float spacing = 2f * radius * Mathf.Sin(Mathf.PI / live.Length); // neighbour distance on an even ring
        string where = "";
        foreach (CaveBeetle b in live)
        {
            Vector3 offset = b.transform.position - band.FormationCentre;
            offset.y = 0f;
            where += " " + b.name + "@" + offset.magnitude.ToString("F1");
            Require(Mathf.Abs(offset.magnitude - radius) < 1.6f, "Box-in: " + b.name + " is " + offset.magnitude.ToString("F1") + " m from the middle, not " + radius + "." + where);
        }
        for (int i = 0; i < live.Length; i++)
            for (int j = i + 1; j < live.Length; j++)
            {
                Vector3 between = live[i].transform.position - live[j].transform.position;
                between.y = 0f;
                Require(between.magnitude > spacing - 1.6f, "Box-in: " + live[i].name + " and " + live[j].name + " are only " + between.magnitude.ToString("F1") + " m apart (even spacing is " + spacing.ToString("F1") + ")." + where);
            }
    }

    private static void SetFlag(object target, string property, bool value) =>
        target.GetType().GetProperty(property).SetValue(target, value);

    private static Health Belly(CaveBeetle beetle) =>
        (Health)typeof(CaveBeetle).GetField("belly", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(beetle);

    private static void HurtTo(CaveBeetle beetle, float fraction, FirstPersonController player)
    {
        var health = beetle.GetComponent<Health>();
        float amount = health.Current - health.Max * fraction;
        if (amount > 0f) health.TakeDamage(amount, player.gameObject);
    }

    private static void Next()
    {
        stage++;
        stageTime = 0f;
    }

    private static void Teleport(FirstPersonController player, Vector3 point)
    {
        var body = player.GetComponent<CharacterController>();
        body.enabled = false;
        player.transform.position = point;
        body.enabled = true;
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
