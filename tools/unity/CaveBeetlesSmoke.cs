// Install only in a disposable project's Assets/Editor; never in the real project.
// Batch: -executeMethod CaveBeetlesSmoke.Start (no -quit; it exits itself).
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
public static class CaveBeetlesSmoke
{
    private const string Key = "SignalLost.CaveBeetlesSmoke";
    private static float started = -1f, stageTime;
    private static int stage, chargeHits, headbutts;
    private static float maxFromCentre;
    private static string error, report = "";

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
        SessionState.SetFloat(Key + "Deadline", (float)EditorApplication.timeSinceStartup + 300);
        EditorApplication.EnterPlaymode();
    }

    private static string Formation(BeetleBand band) =>
        typeof(BeetleBand).GetField("formation", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(band).ToString();

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
            CaveBeetle[] beetles = Object.FindObjectsByType<CaveBeetle>(FindObjectsSortMode.None);
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
                        else headbutts++;
                    };
                    Next();
                    break;
                case 1: // the whole band engages
                    if (Array.TrueForAll(beetles, b => b.Engaged)) { report += "engaged x4; "; Next(); }
                    else Require(stageTime < 6f, "The band did not all engage.");
                    break;
                case 2: // first box-in: be in the air as they charge -> they bump each other
                    if (Formation(band) == "Charge" && stageTime > 0f)
                    {
                        Teleport(player, player.transform.position + Vector3.up * 4f); // airborne for about as long as a real jump (0.69 s)
                        Next();
                    }
                    else Require(stageTime < 20f, "No box-in charge within 20 s.");
                    break;
                case 3:
                    if (stageTime < 2.5f) return;
                    int bumped = 0;
                    foreach (CaveBeetle beetle in beetles) if (beetle.Bumped) bumped++;
                    string why = "chargeHits=" + chargeHits + " playerY=" + player.transform.position.y.ToString("F2");
                    foreach (CaveBeetle beetle in beetles)
                        why += " | " + beetle.name + " under=" + beetle.PassedUnder + " mode=" + typeof(CaveBeetle).GetField("mode", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(beetle) + " pos=" + beetle.transform.position.ToString("F1");
                    Require(bumped >= 2, "Dodged box-in: expected beetles to bump into each other, got " + bumped + ": " + why);
                    Require(chargeHits == 0, "Dodged box-in should not hit the player.");
                    foreach (CaveBeetle beetle in beetles)
                        if (beetle.Bumped) Require(beetle.HealthFraction < 1f, beetle.name + " bumped but took no damage.");
                    report += "dodge -> " + bumped + " bumped; ";
                    Next();
                    break;
                case 4: // second box-in: stay on the ground -> the player is hit, no bumps
                    if (Formation(band) == "Charge") Next();
                    else Require(stageTime < 20f, "No second box-in within 20 s.");
                    break;
                case 5:
                    if (stageTime < 2f) return;
                    Require(chargeHits == 1, "Grounded box-in should hit the player exactly once, got " + chargeHits);
                    foreach (CaveBeetle beetle in beetles) Require(!beetle.Bumped, beetle.name + " bumped although the player was hit.");
                    report += "grounded -> hit once; ";
                    Next();
                    break;
                case 6: // third box-in: step out of the box once they charge -> they don't turn after you, they collide
                    if (Formation(band) == "Charge")
                    {
                        boxCentre = band.FormationCentre;
                        Teleport(player, boxCentre + AwayFromWalls(band, boxCentre) * 14f + Vector3.up * 0.05f);
                        Next();
                    }
                    else Require(stageTime < 20f, "No third box-in within 20 s.");
                    break;
                case 7:
                    if (stageTime < 2.5f) return;
                {
                    int collided = 0;
                    foreach (CaveBeetle beetle in beetles)
                        if (beetle.Bumped)
                        {
                            collided++;
                            Vector3 offset = beetle.transform.position - boxCentre;
                            offset.y = 0f;
                            Require(offset.magnitude < 5f, beetle.name + " turned after the player instead of charging the box: " + offset.magnitude.ToString("F1") + " m from its middle.");
                        }
                    Require(collided >= 2, "Player left during the charge: the beetles should still collide, got " + collided);
                    Require(chargeHits == 1, "Player left during the charge: no charge hit expected.");
                    report += "left mid-charge -> " + collided + " collided in the box; ";
                    Next();
                    break;
                }
                case 8: // fourth box-in: leave the box before the charge -> called off, no attack during the calm
                    if (Formation(band) == "Brace")
                    {
                        float radius = (float)typeof(BeetleBand).GetField("boxRadius", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(band);
                        boxCentre = band.FormationCentre;
                        Teleport(player, boxCentre + AwayFromWalls(band, boxCentre) * (radius + 4f) + Vector3.up * 0.05f);
                        hitsBeforeEscape = chargeHits + headbutts;
                        Next();
                    }
                    else Require(stageTime < 25f, "No fourth box-in within 25 s.");
                    break;
                case 9:
                    Require(Formation(band) != "Charge", "The beetles charged although the player left the box before the charge.");
                    if (stageTime < 0.5f) return;
                    Require(Formation(band) == "None", "Leaving the box should call the box-in off.");
                    Require(chargeHits + headbutts == hitsBeforeEscape, "A beetle attacked during the calm after the player escaped.");
                    if (stageTime < 2.2f) return;
                    report += "left before the charge -> called off, no attack in the calm; headbutts=" + headbutts + "; ";
                    Next();
                    break;
                case 10: // defeat one: scrap, the band gets angrier
                {
                    CaveBeetle victim = beetles[0];
                    float speed = band.SpeedScale;
                    int before = Object.FindObjectsByType<ScrapPickup>(FindObjectsSortMode.None).Length;
                    victim.GetComponent<Health>().TakeDamage(99999f, player.gameObject);
                    int after = Object.FindObjectsByType<ScrapPickup>(FindObjectsSortMode.None).Length;
                    Require(after > before, "A defeated beetle should drop scrap.");
                    Require(band.Fallen == 1 && band.SpeedScale > speed, "The band should get angrier when a member falls.");
                    Require(maxFromCentre <= 24.5f, "A beetle left its zone: " + maxFromCentre.ToString("F1") + " m from the centre.");
                    report += "defeat -> scrap " + (after - before) + ", anger x" + band.SpeedScale.ToString("F2") + "; max from centre " + maxFromCentre.ToString("F1") + " m";
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

    private static Vector3 boxCentre;
    private static int hitsBeforeEscape;

    // Towards the middle of the zone (away from the test room's walls), on the flat.
    private static Vector3 AwayFromWalls(BeetleBand band, Vector3 from)
    {
        Vector3 direction = band.transform.position - from;
        direction.y = 0f;
        return direction.magnitude > 1f ? direction.normalized : Vector3.forward;
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
