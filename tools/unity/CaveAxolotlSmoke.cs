// Install only in a disposable project's Assets/Editor; never in the real project.
// Batch: -executeMethod CaveAxolotlSmoke.Start (no -quit; it exits itself).
using System;
using System.Collections;
using System.IO;
using System.Linq;
using SignalLost.Combat;
using SignalLost.Editor;
using SignalLost.Effects;
using SignalLost.Enemies;
using SignalLost.Enemies.Caves;
using SignalLost.Enemies.Desert;
using SignalLost.Inventory;
using SignalLost.Player;
using SignalLost.Progression;
using SignalLost.Scanner;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CaveAxolotlSmoke
{
    private const string Key = "SignalLost.CaveAxolotlSmoke";
    private static CaveAxolotlSmokeRunner runner;
    internal static bool done;
    private static string error, report = "";

    static CaveAxolotlSmoke()
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
        CaveAxolotlBuilder.BuildPrefab();
        AssetDatabase.SaveAssets();
        CaveAxolotlBuilder.CreateTestScene();
        CaveAxolotlBuilder.Validate();
        EditorSceneManager.OpenScene(CaveAxolotlBuilder.ScenePath);
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + "Deadline", (float)EditorApplication.timeSinceStartup + 420);
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
            if (runner == null) runner = new GameObject("Cave Axolotl Smoke").AddComponent<CaveAxolotlSmokeRunner>();
            if (done)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/CaveAxolotlPlayMode.txt", "CAVE_AXOLOTL_PLAY_MODE_PASS:" + report);
                Debug.Log("CAVE_AXOLOTL_PLAY_MODE_PASS:" + report);
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(0);
            }
        }
        catch (Exception exception)
        {
            SessionState.SetBool(Key, false);
            Debug.LogException(exception);
            Debug.Log("CAVE_AXOLOTL_PLAY_MODE_FAIL after:" + report);
            EditorApplication.Exit(1);
        }
    }

    private static FirstPersonController player;
    private static CharacterController body;
    private static Health hp;
    private static CaveAxolotl axo;
    private static Health axoHp;
    private static AxolotlLair lair;
    private static float damage;

    private static IEnumerator Wait(float seconds, Action each = null)
    {
        float end = Time.time + seconds;
        while (Time.time < end) { each?.Invoke(); yield return null; }
    }

    private static IEnumerator WaitFor(Func<bool> condition, float seconds, string failure)
    {
        float end = Time.time + seconds;
        while (!condition())
        {
            Require(Time.time < end, failure);
            yield return null;
        }
    }

    private static void Place(Vector3 at)
    {
        body.enabled = false;
        player.transform.position = at;
        body.enabled = true;
    }

    private static bool BodyVisible => axo.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r => r.enabled);

    internal static IEnumerator Run()
    {
        yield return Wait(2.5f);
        player = Object.FindAnyObjectByType<FirstPersonController>();
        body = player.GetComponent<CharacterController>();
        hp = player.GetComponent<Health>();
        axo = Object.FindAnyObjectByType<CaveAxolotl>(FindObjectsInactive.Include);
        lair = Object.FindAnyObjectByType<AxolotlLair>();
        Require(axo != null && lair != null, "No axolotl or lair.");
        axoHp = axo.GetComponent<Health>();
        hp.Damaged += (_, amount, source) => { if (source == axo.gameObject) damage += amount; };
        Vector3 home = axo.transform.position;
        var scanner = player.GetComponentInChildren<ScannerController>(true);
        Require(scanner != null && scanner.DisruptsCamouflage, "The test player should start with the Crystal Disruptor.");

        // Before the fight it waits under a crystal, switched off.
        Require(axo.Dormant && !BodyVisible && !axo.IsDisguised, "It should start dormant under its crystal.");
        Shot("dormant", home);
        report += " dormant;";

        // Without the Crystal Disruptor the fight won't start.
        scanner.DisruptsCamouflage = false;
        Place(lair.transform.position + Vector3.up * 0.05f);
        yield return Wait(2f);
        Require(axo.Dormant && !lair.InCutsceneOrFight, "The fight must not start without the Crystal Disruptor.");
        scanner.DisruptsCamouflage = true;
        Place(lair.transform.position + Vector3.back * 14f + Vector3.up * 0.05f);
        yield return null;
        report += " refuses without the upgrade;";

        // Walk into the lair: the entrance plays and the fight starts.
        yield return EnterLair();
        report += " entrance -> fight;";

        // The moves.
        yield return Attack("TailSwipe", 2.6f);
        yield return RollThroughPlayer();
        yield return Attack("ShardRing", 6f);
        yield return RollCrash();

        // Phase 2: the spires grow, then it hides beside a batch, recoloured.
        hp.Restore();
        Place(home + new Vector3(0f, 0.05f, -12f));
        axo.DebugEnterPhase(2);
        yield return WaitFor(() => axo.IsHiding, 12f, "It should hide after the phase 2 roar (move " + axo.CurrentMove + ").");
        Require(GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t => t.name == "Bloom Spire"), "Crystal Bloom spires should grow in phase 2.");
        Require(!BodyVisible && axo.IsDisguised, "While hiding its body is gone and it's disguised.");
        var batches = Object.FindObjectsByType<CrystalBatch>(FindObjectsSortMode.None);
        Require(batches.Any(b => b.CrystalMaterial == axo.CurrentCrystal && b.name != "Cave Axolotl (hiding)"),
            "It should take the colour of a batch (" + axo.CurrentCrystal.name + ").");
        CrystalBatch nearest = batches.Where(b => b.name != "Cave Axolotl (hiding)").OrderBy(b => (b.transform.position - axo.DisguisePosition).sqrMagnitude).First();
        Require(nearest.CrystalMaterial == axo.CurrentCrystal, "It should copy the batch it hides beside.");
        Shot("hiding", axo.DisguisePosition);
        report += $" hides as {axo.CurrentCrystal.name};";

        // The raised scanner beeps at it from range but doesn't reveal it; up close it does.
        player.GetComponentInChildren<ScannerToggle>(true).SetRaised(true);
        Vector3 toMiddle = home - axo.DisguisePosition;
        toMiddle.y = 0f;
        toMiddle.Normalize();
        Place(axo.DisguisePosition + toMiddle * 9f + Vector3.up * 0.05f);
        yield return Wait(1.5f);
        Require(axo.IsHiding, "Outside the reveal radius it should stay hidden.");
        Require(scanner.CurrentTarget == axo.GetComponent<EnemySignalSource>(), "With the upgrade the scanner should beep at it.");
        hp.Restore();
        float before = damage;
        Place(axo.DisguisePosition + toMiddle * 2f + Vector3.up * 0.05f);
        yield return WaitFor(() => !axo.IsHiding, 3f, "The raised scanner up close should bring it up.");
        float startGap = (player.transform.position - axo.transform.position).magnitude;
        bool blocked = false;
        yield return Wait(0.6f, () => { if (axoHp.Invulnerable && !axoHp.TakeDamage(10f, player.gameObject)) blocked = true; });
        Require(blocked, "It must not take damage while it emerges.");
        Require(damage == before, "It must not hurt the player while it emerges.");
        Require((player.transform.position - axo.transform.position).magnitude > startGap + 0.5f, "It should push the player back a little.");
        report += " scanner reveals it, emerge harmless + shove;";
        player.GetComponentInChildren<ScannerToggle>(true).SetRaised(false);

        // Phase 3: ceiling crystals fall near the player.
        yield return WaitFor(() => axo.CurrentMove == null, 6f, "Emerge never finished.");
        axo.DebugEnterPhase(3);
        yield return WaitFor(() => axo.IsHiding, 12f, "It should hide again at phase 3.");
        axo.Disrupt();
        yield return WaitFor(() => !axo.IsHiding, 2f, "Disrupt() should bring it up.");
        bool fell = false;
        yield return Wait(10f, () => fell |= GameObject.FindObjectsByType<GroundMarker>(FindObjectsSortMode.None).Length > 0
            && GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t => t.name == "Falling Crystal"));
        Require(fell, "Phase 3 should drop ceiling crystals.");
        report += " cave-in;";

        // Dying resets it under its crystal; walking in again starts the fight again.
        hp.TakeDamage(99999f, axo.gameObject);
        yield return WaitFor(() => axo.Dormant, 10f, "After the player dies it should go back under its crystal.");
        Require(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).All(t => t.name != "Bloom Spire"), "The spires should clear on reset.");
        yield return Wait(3f);
        hp.Restore();
        yield return EnterLair();
        Require(axo.DuelPhase == 1 && axoHp.Normalized > 0.99f, "A retry starts at full health, phase 1.");
        report += " death resets + retry;";

        // Defeat: scrap and the Flight Control Module, which clears the stage.
        int scrap = Object.FindObjectsByType<ScrapPickup>(FindObjectsSortMode.None).Length;
        yield return WaitFor(() => axo.CurrentMove == null, 8f, "Intro never finished.");
        axoHp.TakeDamage(999999f, player.gameObject);
        yield return Wait(0.5f);
        Require(Object.FindObjectsByType<ScrapPickup>(FindObjectsSortMode.None).Length > scrap, "It should drop scrap.");
        EngineCorePickup module = Object.FindAnyObjectByType<EngineCorePickup>();
        Require(module != null, "It should drop the Flight Control Module.");
        yield return Wait(1.5f);
        Place(module.transform.position + Vector3.down * 0.7f);
        yield return Wait(0.5f);
        Require(StageProgress.HasMainPart(CaveAxolotl.MainPart) && StageProgress.IsCleared(CaveAxolotl.Stage),
            "Picking up the module should recover it and clear the Crystal Caves.");
        Require(lair.Cleared, "The lair should be cleared.");
        report += " scrap + Flight Control Module, stage cleared";
    }

    private static IEnumerator EnterLair()
    {
        Place(lair.transform.position + Vector3.up * 0.05f);
        yield return WaitFor(() => axo.Engaged && !axo.Dormant, 8f, "Walking into the lair should start the fight.");
        Shot("emerge", axo.transform.position);
        yield return WaitFor(() => lair.InCutsceneOrFight && axo.CurrentMove != "Intro", 12f, "The entrance never finished.");
    }

    private static IEnumerator Attack(string attack, float range)
    {
        yield return WaitFor(() => axo.CurrentMove == null, 8f, "Still busy with " + axo.CurrentMove);
        hp.Restore();
        Vector3 front = axo.transform.position + axo.transform.forward * range;
        Place(new Vector3(front.x, axo.transform.position.y + 0.05f, front.z));
        yield return null;
        float before = damage;
        Require(axo.DebugRunAttack(attack), "No attack " + attack);
        bool shot = false;
        float started = Time.time;
        while (axo.CurrentMove == attack && Time.time < started + 10f)
        {
            if (!shot && Time.time - started > 0.6f) { shot = true; Shot(attack, axo.transform.position); }
            yield return null;
        }
        Require(damage > before, attack + " did no damage to a player standing in it.");
        report += $" {attack} {damage - before:0};";
    }

    private static IEnumerator RollThroughPlayer()
    {
        yield return WaitFor(() => axo.CurrentMove == null, 8f, "Still busy with " + axo.CurrentMove);
        hp.Restore();
        Vector3 front = axo.transform.position + axo.transform.forward * 9f;
        Place(new Vector3(front.x, axo.transform.position.y + 0.05f, front.z));
        yield return null;
        float before = damage, healthBefore = axoHp.Current;
        bool rolled = false, lane = false;
        Require(axo.DebugRunAttack("Roll"), "No attack Roll");
        while (axo.CurrentMove == "Roll")
        {
            lane |= Object.FindObjectsByType<GroundMarker>(FindObjectsSortMode.None).Length >= 3;
            if (axo.IsRolling)
            {
                if (!rolled) Shot("roll", axo.transform.position);
                rolled = true;
                axoHp.TakeDamage(25f, player.gameObject); // hits during the roll do nothing
            }
            yield return null;
        }
        Require(lane, "The roll should show its lane as a line of rings first.");
        Require(rolled, "It never rolled.");
        Require(axoHp.Current >= healthBefore, "It must take no damage while rolling.");
        Require(damage > before, "The roll should hit a player standing in its lane.");
        report += $" Roll {damage - before:0} (lane shown, untouchable);";
    }

    // A roll into the boulder: it crashes, dazed, and its gills take double.
    private static IEnumerator RollCrash()
    {
        yield return WaitFor(() => axo.CurrentMove == null, 8f, "Still busy with " + axo.CurrentMove);
        GameObject boulder = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).First(t => t.name.StartsWith("CaveBoulder") && t.name != "CaveBoulderRound").gameObject;
        Vector3 past = boulder.transform.position + (boulder.transform.position - axo.transform.position).normalized * 3f;
        past.y = axo.transform.position.y + 0.05f;
        Place(past);
        hp.Restore();
        yield return null;
        Require(axo.DebugRunAttack("Roll"), "No attack Roll");
        yield return WaitFor(() => axo.IsDazed || axo.CurrentMove != "Roll", 8f, "Roll never ended.");
        Require(axo.IsDazed, "A roll into a boulder should leave it dazed.");
        Shot("crash", axo.transform.position);
        yield return Wait(0.3f);
        Health gill = (Health)new SerializedObject(axo).FindProperty("gills").GetArrayElementAtIndex(0).objectReferenceValue;
        float before = axoHp.Current;
        gill.TakeDamage(10f, player.gameObject);
        yield return null;
        yield return null;
        float gillHit = before - axoHp.Current;
        before = axoHp.Current;
        axoHp.TakeDamage(10f, player.gameObject);
        float bodyHit = before - axoHp.Current;
        Require(Mathf.Abs(gillHit - 20f) < 0.5f && Mathf.Abs(bodyHit - 5f) < 0.5f,
            $"Dazed: 10 on the gills should do 20 and 10 on the hide 5 (got {gillHit:0.0} and {bodyHit:0.0}).");
        report += " crash -> dazed, gills x2, hide x0.5;";
        axoHp.Restore();
    }

    private static void Shot(string name, Vector3 at)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var go = new GameObject("Shot Camera");
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.15f, 0.17f, 0.24f);
        go.transform.position = at + new Vector3(7f, 4.5f, -9f);
        go.transform.LookAt(at + Vector3.up * 1.2f);
        var rt = new RenderTexture(960, 640, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(960, 640, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 960, 640), 0, 0);
        RenderTexture.active = null;
        Directory.CreateDirectory("Logs/CaveAxolotlShots");
        File.WriteAllBytes("Logs/CaveAxolotlShots/" + name + ".png", tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.Destroy(rt);
        Object.Destroy(go);
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}

public sealed class CaveAxolotlSmokeRunner : MonoBehaviour
{
    private IEnumerator Start()
    {
        yield return CaveAxolotlSmoke.Run();
        CaveAxolotlSmoke.done = true;
    }
}
