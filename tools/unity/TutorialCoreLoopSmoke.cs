using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SignalLost.NPC;
using SignalLost.Player;
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
    }

    // ---------- Helpers ----------

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
