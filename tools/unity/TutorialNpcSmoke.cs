using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.SceneManagement;
using System.Reflection;
using SignalLost.Combat;
using SignalLost.Enemies;
using SignalLost.NPC;
using SignalLost.Player;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

[InitializeOnLoad]
public static class TutorialNpcSmoke
{
    private const string Key = "SignalLost.TutorialNpcSmoke";
    private static IEnumerator run;
    private static int frame = -1;
    private static float resume;
    private static readonly List<string> report = new List<string>();
    private static TutorialPlayer player;
    private static CharacterController body;
    private static Camera view;
    private static Terrain terrain;
    private static FirstPersonController movement;

    static TutorialNpcSmoke()
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
        TutorialEnemiesSetup.RequireTemp();
        EditorSceneManager.OpenScene("Assets/Scenes/Tutorial.unity");
        Check(UnityEngine.Object.FindObjectsByType<TrainingDummy>().Length == 3, "Three original dummies authored.");
        var authoredRoots = SceneManager.GetActiveScene().GetRootGameObjects();
        SessionState.SetInt(Key + "Crabs", authoredRoots.SelectMany(r => r.GetComponentsInChildren<HermitCrab>(true)).Count(c => c.gameObject.activeInHierarchy));
        SessionState.SetInt(Key + "Frogs", authoredRoots.SelectMany(r => r.GetComponentsInChildren<MortarFrog>(true)).Count(c => c.gameObject.activeInHierarchy));
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + "Time", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Time", 0) > 160)
        { Fail(new Exception("NPC smoke timed out.")); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < resume || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            run ??= Run();
            if (run.MoveNext()) { resume = Time.time + (run.Current is float seconds ? seconds : 0); return; }
            SessionState.SetBool(Key, false);
            Directory.CreateDirectory("Logs"); File.WriteAllLines("Logs/TutorialNpcSmoke.txt", report);
            Debug.Log("TUTORIAL_NPC_PLAY_MODE_PASS\n" + string.Join("\n", report));
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Fail(ex); }
    }
    private static IEnumerator Run()
    {
        yield return 0.4f;
        player = UnityEngine.Object.FindFirstObjectByType<TutorialPlayer>();
        body = player.GetComponent<CharacterController>();
        movement = player.GetComponent<FirstPersonController>();
        view = player.GetComponentInChildren<Camera>();
        terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
        var quest = UnityEngine.Object.FindFirstObjectByType<TutorialQuestFlow>();
        var merchant = UnityEngine.Object.FindFirstObjectByType<MerchantCrab>();
        var cav = GameObject.Find("CapybaraAstronaut").GetComponent<NpcActor>();
        var drop = Get<TutorialPickup>(merchant, "flaskDrop");
        var brain = merchant.GetComponent<HermitCrab>();
        var health = merchant.GetComponent<Health>();
        var agent = merchant.GetComponent<NavMeshAgent>();
        var shop = merchant.GetComponent<MerchantShop>();
        var ui = UnityEngine.Object.FindFirstObjectByType<TutorialNpcUi>();
        Check(ui.GetComponent<Canvas>().renderMode == RenderMode.ScreenSpaceOverlay && !ui.transform.IsChildOf(merchant.transform), "Quest/shop windows are on independent screen-space canvas.");
        var spawn = player.transform.position;
        var merchantSpawn = merchant.transform.position;
        Check(!quest.Accepted && !player.HasFlask && !drop.gameObject.activeSelf, "Starts without quest/flask; no loose reward available.");
        Check(merchant.transform.localScale.x == 1.6f && health.Max == 500, "Merchant is larger than ordinary crab with authored prototype health.");
        Check(!brain.enabled && !health.TakeDamage(999) && !merchant.IsDefeated, "Cannot bypass Cavendish by killing quest crab early.");
        movement.enabled = false; // No physical focus/input in batch mode; exercise real interaction rays.
        Foot(cav.transform.position + Vector3.forward * 10);
        view.transform.LookAt(cav.GetComponent<Collider>().bounds.center);
        Check(!player.TryInteract() && !quest.Accepted, "Out-of-range talk cannot grant quest.");
        Aim(cav.GetComponent<Collider>(), cav.transform);
        Check(player.TryInteract() && ui.ConversationActive && !quest.Accepted && !brain.enabled, "E interaction opens quest popup without auto-accepting.");
        string instruction = ui.CurrentLine;
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-npcRender") >= 0) CaptureCamera(view, "QuestPopupPreview");
        yield return 7f;
        Check(ui.CurrentLine == instruction && !quest.Accepted, "Quest instructions do not advance or accept after timed wait.");
        ui.CloseAll(); Check(!quest.Accepted, "Closing unfinished quest popup does not start fight.");
        Check(player.TryInteract() && ui.CurrentLine == instruction, "E can reopen cancelled quest instructions.");
        Check(player.TryInteract() && ui.CurrentLine != instruction, "E advances quest instructions one line.");
        player.TryInteract();
        Check(player.TryInteract() && quest.Accepted && brain.enabled && !ui.HasOpenWindow, "Final E accepts quest and starts hostile crab.");
        yield return 0.15f;
        var tracker = Get<GameObject>(ui, "trackerPanel");
        Check(tracker.activeSelf && tracker.GetComponent<RectTransform>().anchorMax == Vector2.one, "Accepted quest appears in top-right HUD tracker.");
        Check(UnityEngine.Object.FindFirstObjectByType<HUDController>().objectiveText.text == "", "Legacy objective label is hidden while top-right tracker is active.");
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-npcRender") >= 0) CaptureCamera(view, "QuestTrackerPreview");
        Check(Time.timeScale == 1 && !player.IsDrinking && !player.IsRespawning, "World-space conversation does not pause simulation.");
        var speech = cav.GetComponentInChildren<NpcDialogue>();
        Check(player.TryInteract() && ui.ConversationActive && !speech.IsManual, "Cavendish quest reminders use an interacted popup, not overhead text.");
        player.TryInteract(); Check(!ui.HasOpenWindow && !merchant.IsDefeated && !drop.gameObject.activeSelf, "Repeated quest conversations do not duplicate quest/reward.");
        Check(!merchant.GetComponent<NpcActor>().CanInteract && merchant.GetComponent<NpcActor>().Prompt == "", "Hostile merchant has no E interaction prompt.");
        var combatSpeech = merchant.GetComponentInChildren<NpcDialogue>();
        var cadence = typeof(NpcDialogue).GetField("secondsPerLine", BindingFlags.Instance | BindingFlags.NonPublic);
        cadence.SetValue(combatSpeech, 0.1f); combatSpeech.Speak("Combat line one", "Combat line two");
        yield return 0.15f;
        Check(combatSpeech.CurrentLine == "Combat line two" && !combatSpeech.IsManual, "Combat chatter still advances automatically.");
        cadence.SetValue(combatSpeech, 6f);
        var laserPickup = GameObject.Find("BurntRockBasin").GetComponentInChildren<TutorialPickup>();
        Check(laserPickup.TryCollect(player) && player.HasLaser, "Existing mining-laser pickup remains functional.");
        // Real crab AI must move and deal damage before surrender.
        Foot(merchant.transform.position + merchant.transform.forward * 1.5f);
        float hp = player.Health.Current;
        for (int i = 0; i < 120 && player.Health.Current == hp; i++) yield return 0.1f;
        Check(player.Health.Current < hp && !player.Health.IsDead, "Merchant crab fights using existing AI and damages the player.");
        Check(!combatSpeech.IsManual && !string.IsNullOrEmpty(combatSpeech.CurrentLine) && !ui.HasOpenWindow,
            "First encounter/combat dialogue appears overhead without opening quest UI.");
        Check(Vector3.Distance(merchantSpawn, merchant.transform.position) > 0.05f || brain.IsAttacking, "Merchant moves/attacks before defeat.");
        // Test reset during unfinished quest.
        player.Health.TakeDamage(999);
        yield return 2f;
        movement.enabled = false;
        Check(quest.Accepted && !merchant.IsDefeated && brain.enabled && !health.Invulnerable && health.Current == health.Max,
            "Death during quest retains quest and restores hostile merchant health.");
        Check(Vector3.Distance(merchant.transform.position, merchantSpawn) < 1, "Unfinished merchant returns to original fight position after death.");
        Check(!drop.gameObject.activeSelf && player.HasLaser, "No early flask drop; collected laser retained after death.");
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-npcRender") >= 0) Capture(merchant);
        // Defeat via the existing damage backend; combat weapon implementation is unchanged.
        Check(health.TakeDamage(999, player.gameObject) && merchant.IsDefeated, "Defeat transitions dedicated crab to merchant exactly once.");
        Check(drop.gameObject.activeSelf && !Get<GameObject>(merchant, "carriedFlask").activeSelf, "Flask disappears from back and becomes one collectible drop.");
        Check(!health.TakeDamage(999) && !brain.enabled, "Friendly merchant cannot be killed or continue attacking.");
        for (int i = 0; i < 400 && (i < 20 || merchant.IsReturning); i++) yield return 0.1f;
        var stand = Get<Transform>(merchant, "returnPoint");
        Check(!merchant.IsReturning && Vector3.Distance(merchant.transform.position, stand.position) < 1.1f, "Merchant walks along NavMesh and stops beside Cavendish.");
        Check(health.Current == health.Max && health.Invulnerable, "Recovered merchant is healthy and protected.");
        Aim(drop.GetComponentInChildren<Collider>(), drop.transform);
        Check(player.TryInteract() && player.HasFlask && !drop.gameObject.activeSelf, "Only dropped flask can be collected with the actual interaction ray.");
        Check(!drop.TryCollect(player), "Flask cannot be duplicated.");
        yield return 0.2f;
        Check(quest.FlaskRecovered && quest.Objective.Contains("Flask recovered"), "Quest objective completes when flask is collected.");
        Aim(cav.GetComponent<Collider>(), cav.transform);
        Check(player.TryInteract() && ui.ConversationActive && !speech.IsManual, "Quest completion instructions open a popup on interaction.");
        string completionLine = ui.CurrentLine;
        yield return 7f;
        Check(ui.CurrentLine == completionLine, "Quest completion popup waits for E.");
        player.TryInteract(); player.TryInteract();
        Check(!ui.HasOpenWindow, "E completes the quest report conversation.");
        Check(player.TryInteract() && speech.IsManual && !ui.HasOpenWindow, "Non-quest small talk stays above Cavendish.");
        string regularLine = speech.CurrentLine;
        yield return 7f;
        Check(speech.CurrentLine == regularLine, "World-space instructional talk waits indefinitely for E.");
        player.TryInteract(); Check(speech.CurrentLine != regularLine, "E advances world-space Cavendish dialogue.");
        player.TryInteract(); Check(!speech.IsManual, "Final E ends regular conversation without restarting it.");
        Aim(merchant.GetComponent<Collider>(), merchant.transform);
        Check(player.TryInteract() && !ui.HasOpenWindow && Get<NpcDialogue>(merchant, "dialogue").IsManual,
            "First friendly crab interaction gives an overhead greeting.");
        Check(player.TryInteract() && ui.ConversationActive && !shop.IsOpen, "Next E opens manual shop/quest instructions in a popup.");
        string shopLine = ui.CurrentLine;
        yield return 7f;
        Check(ui.CurrentLine == shopLine && !shop.IsOpen, "Merchant shop dialogue never auto-advances.");
        Check(player.TryInteract() && ui.CurrentLine != shopLine, "E advances merchant instructions.");
        Check(player.TryInteract() && shop.IsOpen, "Final E opens independent shop window.");
        var panel = Get<GameObject>(ui, "shopPanel");
        var buttons = panel.GetComponentsInChildren<Button>();
        Check(buttons.Length == 3 && Array.TrueForAll(buttons, b => !b.interactable), "All placeholder purchase controls are disabled.");
        Check(!player.IsRespawning && Time.timeScale == 1, "Shop does not pause simulation.");
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-npcRender") >= 0)
        { yield return 0.2f; CaptureCamera(view, "MerchantShopPreview"); }
        shop.Toggle(); Check(!shop.IsOpen, "Shop can close without changing cursor/input state.");
        shop.Toggle(); Foot(spawn); yield return 0.15f; Check(!shop.IsOpen, "Walking away closes shop preview.");
        // Death after completion must preserve friendly merchant and collected flask.
        player.Health.TakeDamage(999); yield return 2f;
        Check(merchant.IsDefeated && !brain.enabled && quest.FlaskRecovered && player.HasFlask && !drop.gameObject.activeSelf,
            "Completed quest, friendly merchant, and collected flask survive player death without duplicates.");
        // Same reset path must also preserve NPC state after the ninth-drink blackout.
        for (int sip = 0; sip < 9; sip++)
        { Check(player.TryDrink(), "Post-quest flask sip " + (sip + 1) + " accepted."); yield return 1.3f; }
        yield return 2f;
        Check(!player.IsRespawning && player.DrunkStacks == 0 && quest.FlaskRecovered && merchant.IsDefeated && !brain.enabled,
            "Ninth-drink blackout retains quest completion and friendly merchant, and restores sober player.");
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        int dummies = roots.SelectMany(r => r.GetComponentsInChildren<TrainingDummy>(true)).Count(c => c.gameObject.activeInHierarchy);
        int frogs = roots.SelectMany(r => r.GetComponentsInChildren<MortarFrog>(true)).Count(c => c.gameObject.activeInHierarchy);
        int crabs = roots.SelectMany(r => r.GetComponentsInChildren<HermitCrab>(true)).Count(c => c.gameObject.activeInHierarchy);
        foreach (var c in roots.SelectMany(r => r.GetComponentsInChildren<HermitCrab>(true)).Where(c => c.gameObject.activeInHierarchy)) report.Add("Crab path=" + PathOf(c.transform));
        foreach (var c in roots.SelectMany(r => r.GetComponentsInChildren<MortarFrog>(true)).Where(c => c.gameObject.activeInHierarchy)) report.Add("Frog path=" + PathOf(c.transform));
        Check(dummies == 3 && frogs == SessionState.GetInt(Key + "Frogs", -1) && crabs == SessionState.GetInt(Key + "Crabs", -1), "Dummies, ordinary crab, and frog remain in tutorial. Counts=" + dummies + "/" + frogs + "/" + crabs);
    }
    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
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
    private static void Capture(MerchantCrab merchant)
    {
        ShaderUtil.allowAsyncCompilation = false;
        var obj = new GameObject("NPC Preview Camera");
        var camera = obj.AddComponent<Camera>();
        camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        camera.enabled = false;
        Vector3 focus = merchant.transform.position + Vector3.up * 1.15f;
        camera.transform.position = focus + merchant.transform.forward * 5.5f + merchant.transform.right * 3.8f + Vector3.up * 2.4f;
        camera.transform.LookAt(focus); camera.fieldOfView = 42;
        camera.backgroundColor = new Color(0.08f, 0.1f, 0.13f); camera.clearFlags = CameraClearFlags.SolidColor;
        var light = new GameObject("Preview Fill").AddComponent<Light>(); light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(40, 210, 0); light.intensity = 2;
        var texture = new RenderTexture(1280, 900, 24); texture.Create();
        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = texture });
        var old = RenderTexture.active; RenderTexture.active = texture;
        var image = new Texture2D(1280, 900, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1280, 900), 0, 0); image.Apply();
        Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/MerchantCrabPreview.png", image.EncodeToPNG());
        RenderTexture.active = old; UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(image);
        UnityEngine.Object.Destroy(light.gameObject); UnityEngine.Object.Destroy(obj);
        report.Add("Captured merchant prefab in actual Unity rendering.");
    }
    private static void CaptureCamera(Camera camera, string name)
    {
        ShaderUtil.allowAsyncCompilation = false;
        var overlays = UnityEngine.Object.FindObjectsByType<Canvas>().Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        foreach (var canvas in overlays) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 0.4f; }
        Canvas.ForceUpdateCanvases();
        var texture = new RenderTexture(1280, 900, 24); texture.Create();
        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = texture });
        var old = RenderTexture.active; RenderTexture.active = texture;
        var image = new Texture2D(1280, 900, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 900), 0, 0); image.Apply();
        Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/" + name + ".png", image.EncodeToPNG());
        RenderTexture.active = old; UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(image);
        foreach (var canvas in overlays) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
    }
    private static T Get<T>(object o, string field) => (T)o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); report.Add(message); }
    private static void Fail(Exception ex)
    {
        SessionState.SetBool(Key, false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/TutorialNpcSmokeFailure.txt", ex + "\n" + string.Join("\n", report));
        Debug.LogError(ex); EditorApplication.Exit(1);
    }
}
