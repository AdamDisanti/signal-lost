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
using SignalLost.Scanner;
using SignalLost.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Play Mode smoke for the tutorial core loop (meeting notes 2026-10-20). Install only in a disposable copy of the
// project under the system temp directory, in Assets/Editor, then run:
//   Unity -batchmode -projectPath <temp-project> -executeMethod TutorialCoreLoopSmoke.Start -logFile <log>
// Omit -quit; it exits with 0 on pass and 1 on failure. Add -coreLoopRender (without -nographics) to save UI
// captures to Logs/CoreLoop_*.png. Calls public methods directly: not a physical keyboard/mouse test.
[InitializeOnLoad]
public static class TutorialCoreLoopSmoke
{
    private const string Key = "SignalLost.TutorialCoreLoopSmoke";
    private static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    private static bool started;
    private static int frame = -1;
    private static float resume;
    private static readonly List<string> report = new List<string>();
    private static TutorialPlayer player;
    private static CharacterController body;
    private static Camera view;
    private static Terrain terrain;
    private static FirstPersonController movement;

    static TutorialCoreLoopSmoke()
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
        RequireTemp();
        EditorSceneManager.OpenScene("Assets/Scenes/Tutorial.unity");
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + "Time", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Time", 0) > 300)
        { Fail(new Exception("Core loop smoke timed out.")); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < resume || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (!started) { started = true; steps.Push(Run()); }
            // Nested IEnumerators (captures) run inline; a float waits that many seconds; null waits a frame.
            while (steps.Count > 0)
            {
                IEnumerator top = steps.Peek();
                if (!top.MoveNext()) { steps.Pop(); continue; }
                if (top.Current is IEnumerator nested) { steps.Push(nested); continue; }
                resume = Time.time + (top.Current is float seconds ? seconds : 0);
                return;
            }
            SessionState.SetBool(Key, false);
            Directory.CreateDirectory("Logs"); File.WriteAllLines("Logs/TutorialCoreLoopSmoke.txt", report);
            Debug.Log("TUTORIAL_CORE_LOOP_SMOKE_PASS\n" + string.Join("\n", report));
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static IEnumerator Run()
    {
        yield return 0.5f;
        player = UnityEngine.Object.FindFirstObjectByType<TutorialPlayer>();
        body = player.GetComponent<CharacterController>();
        movement = player.GetComponent<FirstPersonController>();
        view = player.GetComponentInChildren<Camera>();
        terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
        var cav = GameObject.Find("CapybaraAstronaut").GetComponent<NpcActor>();
        var cavSpeech = cav.GetComponentInChildren<NpcDialogue>();
        var merchant = UnityEngine.Object.FindFirstObjectByType<MerchantCrab>();
        var crabSpeech = merchant.GetComponentInChildren<NpcDialogue>();
        var ui = DialogueUi.Instance;
        movement.enabled = false; // No physical focus/input in batch mode; drive the real interaction ray instead.

        // ---------- Package 1: dialogue UI ----------
        Check(GameObject.Find("Tutorial NPC Windows") == null && ui != null && ui.GetComponent<Canvas>().renderMode == RenderMode.ScreenSpaceOverlay,
            "Code-built dialogue canvas replaces the legacy NPC windows.");
        Check(cavSpeech.GetComponent<TextMeshPro>() == null || !cavSpeech.GetComponent<TextMeshPro>().enabled,
            "Legacy world-space NPC text is hidden in favour of speech bubbles.");

        Aim(cav.GetComponent<Collider>(), cav.transform);
        cavSpeech.Speak(2f, "Bubble line one, mechanic!", "Bubble line two.");
        yield return 0.4f;
        SpeechBubble bubble = ui.BubbleLayer.GetComponentsInChildren<SpeechBubble>(true).FirstOrDefault(b => b.HasText);
        Check(bubble != null && bubble.GetComponent<CanvasGroup>().alpha > 0.5f, "Overhead chatter shows a visible speech bubble.");
        var subtitle = Get<TextMeshProUGUI>(ui, "subtitleText");
        Check(subtitle.text.Contains("Bubble line one") && subtitle.text.Contains("Cavendish") &&
              Get<CanvasGroup>(ui, "subtitleGroup").alpha > 0.5f, "The same line appears as a bottom-middle subtitle with the speaker's name.");
        yield return Capture("BubbleAndSubtitle");
        yield return 2.2f;
        Check(cavSpeech.CurrentLine == "Bubble line two." && subtitle.text.Contains("Bubble line two"), "Chatter auto-advances in bubble and subtitle.");

        // Walk up first: getting close triggers the crab's own greeting, which would replace the warning.
        Foot(merchant.transform.position + merchant.transform.forward * 4f);
        view.transform.LookAt(crabSpeech.transform.position);
        yield return 0.3f;
        crabSpeech.Warn("Hey! That's not nice!");
        yield return 0.3f;
        Check(subtitle.text.Contains("not nice") && Get<bool>(ui, "subtitleWarning") && !UiMenus.AnyOpen,
            "Warnings show a red subtitle and bubble without opening any menu.");
        yield return Capture("WarningBubble");

        Aim(cav.GetComponent<Collider>(), cav.transform);
        Check(player.TryInteract() && ui.PopupOpen && UiMenus.AnyOpen, "E on Cavendish opens the focused quest popup and frees the cursor.");
        string first = ui.CurrentLine;
        yield return 0.1f;
        Check(Get<TextMeshProUGUI>(ui, "bodyText").maxVisibleCharacters < first.Length, "Popup text types out.");
        Check(ui.Advance() && ui.CurrentLine == first, "First press finishes the typing instead of skipping the line.");
        yield return Capture("QuestPopup");
        ui.CloseAll();
        Check(!ui.PopupOpen && !UiMenus.AnyOpen, "Escape/cancel closes the popup and returns the cursor.");
        yield return null;
        Check(player.TryInteract() && ui.CurrentLine == first, "E reopens the popup from the start.");
        for (int i = 0; i < 12 && ui.PopupOpen; i++) { ui.Advance(); ui.Advance(); }
        Check(!ui.PopupOpen && !UiMenus.AnyOpen, "Reading to the end closes the popup.");
        yield return 0.5f;
        var tracker = Get<CanvasGroup>(GameplayOverlay.Instance, "trackerGroup");
        Check(tracker.alpha > 0.5f, "Accepted quest shows the themed top-right tracker.");
        yield return Capture("QuestTracker");
        yield return PlayerItems(merchant);
    }

    // ---------- Package 2: player items, cooldowns, stun, healing ----------
    private static IEnumerator PlayerItems(MerchantCrab merchant)
    {
        merchant.GetComponent<HermitCrab>().enabled = false; // keep the quest crab out of these checks
        var laser = player.Laser;
        var scanner = player.Scanner;
        var pickups = UnityEngine.Object.FindObjectsByType<TutorialPickup>(FindObjectsInactive.Include);
        var scannerPickup = pickups.Single(p => p.Kind == TutorialPickup.Item.Scanner);
        var laserPickup = pickups.Single(p => p.Kind == TutorialPickup.Item.Laser);
        Check(scanner != null && !scanner.enabled && !player.HasScanner && !player.ScannerRaised,
            "Scanner is mounted but unusable until it is found.");
        Check(scannerPickup.gameObject.activeInHierarchy && scannerPickup.GetComponentInChildren<Light>() != null,
            "A glowing scanner pickup lies in the crash site.");
        Check(laser.ShotCooldown == 10f && laser.ComboCooldown == 3f, "Tutorial laser uses the 10 s shot and 3 s combo cooldowns.");

        Aim(scannerPickup.GetComponent<Collider>(), scannerPickup.transform);
        yield return Capture("ScannerPickup");
        Check(player.TryInteract() && player.HasScanner && scanner.enabled && !scannerPickup.gameObject.activeSelf,
            "E collects the scanner through the real interaction ray.");
        yield return 0.5f;
        var card = Get<TextMeshProUGUI>(GameplayOverlay.Instance, "cardTitle");
        Check(card.text == "Scanner" && Get<CanvasGroup>(GameplayOverlay.Instance, "cardGroup").alpha > 0.5f, "Collecting the scanner shows its item card.");
        yield return Capture("ScannerCard");

        Aim(laserPickup.GetComponent<Collider>(), laserPickup.transform);
        Check(player.TryInteract() && player.HasLaser && laser.enabled, "E collects the laser, which is drawn.");
        yield return 0.3f;
        Check(card.text == "Mining Laser", "Collecting the laser shows its item card.");

        SetRaised(scanner, true);
        yield return null;
        yield return null;
        Check(player.ScannerRaised && !laser.enabled, "Raising the scanner (Q) puts the laser away.");
        yield return Capture("ScannerRaised");
        SetRaised(scanner, false);
        yield return null;
        yield return null;
        Check(!player.ScannerRaised && laser.enabled, "Lowering the scanner (Q again) draws the laser.");

        // Charged shot cooldown.
        Invoke(laser, "FireBolt", 1f);
        Check(laser.ShotCooldownRemaining > 9.5f && laser.IsCoolingDown, "A bolt starts the 10 s cooldown.");

        // Combo finisher: stuns a small enemy, then the combo needs 3 s.
        var armadillo = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Desert/HubcapArmadillo.prefab"),
            player.transform.position + player.transform.forward * 1.6f, Quaternion.identity).GetComponent<HubcapArmadillo>();
        yield return 0.3f;
        // Freeze it in reach: armadillos back away from a close player, and only the stun call is under test.
        armadillo.enabled = false;
        armadillo.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
        armadillo.transform.position = player.transform.position + player.transform.forward * 1.5f;
        Physics.SyncTransforms();
        view.transform.LookAt(armadillo.GetComponent<Collider>().bounds.center);
        Set(laser, "comboStep", 1);
        Set(laser, "lastSwingEnd", Time.time);
        Invoke(laser, "StartSwing");
        yield return 0.9f;
        report.Add($"  armadillo hp={armadillo.GetComponent<Health>().Current} dist={Vector3.Distance(armadillo.transform.position, player.transform.position):0.00} swinging={laser.IsSwinging} step={laser.ComboStep} cd={laser.ComboCooldownRemaining:0.00}");
        Check(armadillo.IsStunned && laser.ComboCooldownRemaining > 1.5f, "The third combo hit stuns desert fodder and starts the 3 s combo cooldown.");
        yield return 3.2f;
        Check(!armadillo.IsStunned && laser.ComboCooldownRemaining <= 0f, "The stun and combo cooldown both wear off.");
        UnityEngine.Object.Destroy(armadillo.gameObject);
        var crabStun = merchant.GetComponentInParent<IStunnable>();
        Check(crabStun == null, "Hermit crabs are not stunnable (fodder only).");

        // Healing: each sip heals half as much while drunk; the sixth sip in a row blacks out.
        player.GetComponent<SignalLost.Inventory.PlayerInventory>().Add(SignalLost.Inventory.PlayerItem.Flask);
        player.Health.TakeDamage(95f);
        float[] expected = { 35f, 17.5f, 8.75f, 4.375f, 2.1875f };
        for (int sip = 0; sip < expected.Length; sip++)
        {
            float before = player.Health.Current;
            Check(player.TryDrink(), $"Sip {sip + 1} accepted.");
            yield return 1.3f;
            Check(Mathf.Abs(player.Health.Current - before - expected[sip]) < 0.01f, $"Sip {sip + 1} heals {expected[sip]} HP.");
        }
        Check(player.DrunkStacks == 5 && !player.IsRespawning, "Five stacks allowed without blackout.");
        Check(player.TryDrink(), "Sixth sip accepted.");
        yield return 0.7f;
        Check(player.IsRespawning, "The sixth sip in a row blacks the player out.");
        yield return 2.5f;
        Check(!player.IsRespawning && player.DrunkStacks == 0 && Mathf.Approximately(player.NextSipHeal, 35f) && player.HasScanner && player.HasLaser,
            "After blackout the player is sober, heals fully again and keeps their gear.");
        movement.enabled = false;
        merchant.GetComponent<HermitCrab>().enabled = true;
    }

    // ---------- Helpers ----------

    private static void SetRaised(ScannerToggle toggle, bool raised) => Invoke(toggle, "SetRaised", raised);

    private static object Invoke(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, args);

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(target, value);

    private static void Foot(Vector3 p)
    {
        p.y = terrain.SampleHeight(p) + terrain.transform.position.y + 0.06f;
        body.enabled = false; player.transform.position = p; body.enabled = true; Physics.SyncTransforms();
    }

    private static void Aim(Collider target, Transform root)
    {
        for (int i = 0; i < 40; i++)
        {
            float angle = i * Mathf.PI / 20;
            Foot(target.bounds.center + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * 2.1f);
            view.transform.LookAt(target.bounds.center);
            if (Physics.Raycast(view.transform.position, view.transform.forward, out var hit, 3, ~0, QueryTriggerInteraction.Ignore) && hit.collider.transform.IsChildOf(root)) return;
        }
        throw new Exception("No interaction ray to " + root.name);
    }

    // Renders the player camera with every overlay canvas temporarily bound to it, so captures include the UI.
    private static IEnumerator Capture(string name)
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-coreLoopRender") < 0) yield break;
        ShaderUtil.allowAsyncCompilation = false;
        const int width = 1600, height = 900;
        var texture = new RenderTexture(width, height, 24); texture.Create();
        var overlays = UnityEngine.Object.FindObjectsByType<Canvas>().Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        view.targetTexture = texture;
        foreach (var canvas in overlays) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = view; canvas.planeDistance = 0.4f; }
        yield return null;
        yield return null;
        Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(view, new RenderPipeline.StandardRequest { destination = texture });
        var old = RenderTexture.active; RenderTexture.active = texture;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/CoreLoop_" + name + ".png", image.EncodeToPNG());
        RenderTexture.active = old;
        foreach (var canvas in overlays) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
        view.targetTexture = null;
        UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(image);
        report.Add("Captured Logs/CoreLoop_" + name + ".png");
        report.Add($"  subtitle until={Get<float>(DialogueUi.Instance, "subtitleUntil"):0.0} now={Time.unscaledTime:0.0} time={Time.time:0.0} alpha={Get<CanvasGroup>(DialogueUi.Instance, "subtitleGroup").alpha:0.00}");
        foreach (var b in UnityEngine.Object.FindObjectsByType<SpeechBubble>())
            report.Add($"  bubble {b.name} text={b.HasText} alpha={b.GetComponent<CanvasGroup>().alpha:0.00} pos={((RectTransform)b.transform).anchoredPosition}");
    }

    private static T Get<T>(object o, string field) =>
        (T)o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(o);

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); report.Add(message); }

    private static void RequireTemp()
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Application.isBatchMode || !(project.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
            project.StartsWith("/private/tmp/", StringComparison.Ordinal) || project.StartsWith("/tmp/", StringComparison.Ordinal)))
            throw new InvalidOperationException("Run in a disposable batch-mode project under the system temporary directory.");
    }

    private static void Fail(Exception ex)
    {
        SessionState.SetBool(Key, false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/TutorialCoreLoopSmokeFailure.txt", ex + "\n" + string.Join("\n", report));
        Debug.LogError(ex); EditorApplication.Exit(1);
    }
}
