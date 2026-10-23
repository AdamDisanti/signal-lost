using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SignalLost.Combat;
using SignalLost.Core;
using SignalLost.Enemies;
using SignalLost.Inventory;
using SignalLost.NPC;
using SignalLost.Player;
using SignalLost.Scanner;
using SignalLost.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Play Mode smoke for the shared player, hands, HUD and menus (tutorial overhaul phase 2), in the free-play
// Tutorial Testing sandbox, Desert Dunes and the title screen. Install only in a disposable project copy under the
// system temp directory (Assets/Editor), then run:
//   Unity -batchmode -projectPath <temp-project> -executeMethod CoreSystemsSmoke.Start -logFile <log>
// Omit -quit. Add -coreRender (without -nographics) to save captures to Logs/Core_*.png. Not a physical-input test.
[InitializeOnLoad]
public static class CoreSystemsSmoke
{
    private const string Key = "SignalLost.CoreSystemsSmoke";
    private static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    private static readonly List<string> report = new List<string>();
    private static bool started;
    private static int frame = -1;
    private static float resume;
    private static Camera view;

    static CoreSystemsSmoke()
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
        EditorSceneManager.OpenScene("Assets/Scenes/Tutorial Testing.unity");
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + "Time", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Time", 0) > 400) { Fail(new Exception("Timed out.")); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.unscaledTime < resume || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (!started) { started = true; steps.Push(Run()); }
            while (steps.Count > 0)
            {
                IEnumerator top = steps.Peek();
                if (!top.MoveNext()) { steps.Pop(); continue; }
                if (top.Current is IEnumerator nested) { steps.Push(nested); continue; }
                resume = Time.unscaledTime + (top.Current is float seconds ? seconds : 0);
                return;
            }
            SessionState.SetBool(Key, false);
            Directory.CreateDirectory("Logs"); File.WriteAllLines("Logs/CoreSystemsSmoke.txt", report);
            Debug.Log("CORE_SYSTEMS_SMOKE_PASS\n" + string.Join("\n", report));
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static IEnumerator Run()
    {
        yield return 1f;
        // ---------- Sandbox: the shared player ----------
        var life = UnityEngine.Object.FindAnyObjectByType<PlayerLife>();
        var gear = life.GetComponent<PlayerGear>();
        var flask = life.GetComponent<PlayerFlask>();
        var hands = life.GetComponent<FirstPersonHands>();
        var interactor = life.GetComponent<PlayerInteractor>();
        var movement = life.GetComponent<FirstPersonController>();
        view = life.GetComponentInChildren<Camera>();
        movement.enabled = false; // no physical focus/input in batch mode
        Check(UnityEngine.Object.FindObjectsByType<Camera>().Count(c => c.isActiveAndEnabled) == 1 &&
              UnityEngine.Object.FindObjectsByType<AudioListener>().Length == 1, "Exactly one camera and audio listener.");
        Check(UnityEngine.Object.FindObjectsByType<HUDController>().Length == 0 && UnityEngine.Object.FindObjectsByType<CombatDebugHud>().Length == 0 &&
              UnityEngine.Object.FindObjectsByType<PlayerHud>().Length == 0, "No old HUD remains (TutorialHUD, debug HUD, PlayerHUD).");
        Check(GameHud.Instance != null && UnityEngine.Object.FindAnyObjectByType<GameSystems>() != null, "GameSystems brought up the new HUD.");
        Check(gear.Has(PlayerItem.MiningLaser) && gear.Has(PlayerItem.Scanner) && gear.Has(PlayerItem.Flask), "The sandbox starts with all gear.");
        Check(gear.InHand == PlayerGear.Held.Laser && gear.Laser.enabled && hands.RightHand.parent == gear.Laser.HeldTool,
            "The right hand holds the laser.");
        yield return Capture("Hud");

        SetRaised(gear.Scanner, true);
        yield return null;
        yield return null;
        Check(gear.InHand == PlayerGear.Held.Scanner && !gear.Laser.enabled && hands.RightHand.parent == gear.Scanner.Visual.transform,
            "Q swaps to the scanner, which the right hand now holds.");
        yield return Capture("Scanner");
        SetRaised(gear.Scanner, false);
        yield return null;
        yield return null;
        Check(gear.InHand == PlayerGear.Held.Laser && hands.RightHand.parent == gear.Laser.HeldTool, "Q again swaps back to the laser.");

        Vector3 restLeft = hands.LeftHand.localPosition;
        life.Health.TakeDamage(50f);
        Check(flask.TryDrink(), "F starts a sip.");
        yield return 0.35f;
        Check(flask.IsDrinking && hands.LeftHand.localPosition != restLeft && hands.LeftHand.Find("Held Flask") != null && hands.LeftHand.Find("Held Flask").gameObject.activeSelf,
            "The left hand brings the flask up while the right keeps the laser.");
        Check(gear.Laser.enabled, "Drinking does not put the laser away.");
        yield return Capture("Drinking");
        yield return 0.6f;
        Check(!flask.IsDrinking && life.Health.Current > 50f && Vector3.Distance(hands.LeftHand.localPosition, restLeft) < 0.01f,
            "The sip heals and the flask is put away.");

        // ---------- Menus ----------
        PauseMenu.Instance.Open();
        Check(Time.timeScale == 0f && UiMenus.FullScreenOpen, "Pause stops time and owns the screen.");
        yield return Capture("Pause");
        Invoke(PauseMenu.Instance, "ShowSettings", SettingsPanel.Page.Sound);
        yield return Capture("PauseSettings");
        PauseMenu.Instance.Resume();
        Check(Time.timeScale == 1f && !UiMenus.AnyOpen, "Continue resumes the game.");
        GameSettings.LookSensitivity = 1.5f;
        Check(Mathf.Approximately(FirstPersonController.SensitivityScale, 1.5f), "Look sensitivity applies to the controller.");
        GameSettings.LookSensitivity = 1f;
        InventoryUi.Instance.Open();
        Check(InventoryUi.Instance.IsOpen && GetField<CanvasGroup>(GameHud.Instance, "barGroup") != null, "Inventory opens.");
        yield return 0.3f;
        yield return Capture("Inventory");
        InventoryUi.Instance.Close();

        // ---------- NPCs without a story ----------
        var cavendish = UnityEngine.Object.FindObjectsByType<NpcActor>().First(n => n.IsQuestGiver);
        var merchant = UnityEngine.Object.FindAnyObjectByType<MerchantCrab>();
        Check(merchant.IsDefeated && !merchant.GetComponent<HermitCrab>().enabled, "The sandbox merchant starts friendly.");
        Check(merchant.Cousins.Count == 10 && merchant.Cousins.All(c => c != null && !c.gameObject.activeSelf), "Its ten cousins wait despawned for the angry-merchant ambush.");
        Aim(life, cavendish.GetComponent<Collider>(), cavendish.transform);
        Check(interactor.Prompt.Contains("Cavendish") && interactor.TryInteract() && cavendish.Dialogue.IsManual, "Cavendish makes small talk.");
        yield return Capture("Prompt");
        interactor.TryInteract();
        Aim(life, merchant.GetComponent<Collider>(), merchant.transform);
        Check(interactor.TryInteract() && MerchantMenuUi.Instance.IsOpen, "The friendly merchant opens its shop.");
        MerchantMenuUi.Instance.Close();

        // ---------- Death, fade and stage reset ----------
        var dummy = UnityEngine.Object.FindObjectsByType<TrainingDummy>().First();
        string dummyName = dummy.name;
        dummy.GetComponent<Health>().TakeDamage(999f);
        life.Health.TakeDamage(999f);
        yield return 0.6f;
        Check(life.IsRespawning, "Dying starts the respawn fade.");
        yield return Capture("Death");
        for (int i = 0; i < 40 && life.IsRespawning; i++) yield return 0.1f;
        Check(!life.IsRespawning && Mathf.Approximately(life.Health.Current, life.Health.Max) &&
              Vector3.Distance(life.transform.position, PlayerSpawnPoint.Current.transform.position) < 0.5f, "The player respawns at the spawn point, healed.");
        Check(UnityEngine.Object.FindObjectsByType<TrainingDummy>().Any(d => d.name == dummyName && !d.GetComponent<Health>().IsDead), "StageReset restored the targets.");
        yield return 3f; // the short wake-up after respawning

        // ---------- Desert Dunes on the shared player ----------
        EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Large Areas/DesertDunes.unity", new LoadSceneParameters(LoadSceneMode.Single));
        yield return 2f;
        life = UnityEngine.Object.FindAnyObjectByType<PlayerLife>();
        gear = life.GetComponent<PlayerGear>();
        view = life.GetComponentInChildren<Camera>();
        life.GetComponent<FirstPersonController>().enabled = false;
        Check(UnityEngine.Object.FindObjectsByType<PlayerHud>().Length == 0 && UnityEngine.Object.FindObjectsByType<PlaceholderRespawn>().Length == 0 &&
              GameHud.Instance != null, "Desert Dunes runs on the shared player and HUD.");
        SetRaised(gear.Scanner, true);
        yield return 0.5f;
        Check(gear.Scanner.GetComponent<ScannerController>().enabled, "The desert scanner works.");
        yield return Capture("Desert");

        // ---------- Title screen ----------
        EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainMenu.unity", new LoadSceneParameters(LoadSceneMode.Single));
        yield return 1.5f;
        var menu = UnityEngine.Object.FindAnyObjectByType<MainMenuScreen>();
        var buttons = menu.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        Check(buttons.Any(b => b.name == "New Game" && b.interactable) && buttons.Any(b => b.name == "Continue" && !b.interactable) &&
              buttons.Any(b => b.name == "Settings") && buttons.Any(b => b.name == "Exit"), "The title screen offers New Game, Continue (no save yet), Settings and Exit.");
        view = Camera.main;
        yield return Capture("MainMenu");
        Invoke(menu, "OpenSettings");
        yield return Capture("MainMenuSettings");
    }

    // ---------- Helpers ----------

    private static void SetRaised(ScannerToggle toggle, bool raised) => Invoke(toggle, "SetRaised", raised);

    private static void Aim(PlayerLife player, Collider target, Transform root)
    {
        var body = player.GetComponent<CharacterController>();
        for (int i = 0; i < 40; i++)
        {
            float angle = i * Mathf.PI / 20;
            Vector3 p = target.bounds.center + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * 2.1f;
            if (Physics.Raycast(p + Vector3.up * 10f, Vector3.down, out RaycastHit ground, 30f)) p = ground.point;
            body.enabled = false; player.transform.position = p + Vector3.up * 0.05f; body.enabled = true; Physics.SyncTransforms();
            view.transform.LookAt(target.bounds.center);
            if (Physics.Raycast(view.transform.position, view.transform.forward, out var hit, 3, ~0, QueryTriggerInteraction.Ignore) && hit.collider.transform.IsChildOf(root)) return;
        }
        throw new Exception("No interaction ray to " + root.name);
    }

    private static IEnumerator Capture(string name)
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-coreRender") < 0) yield break;
        ShaderUtil.allowAsyncCompilation = false;
        const int width = 1600, height = 900;
        var texture = new RenderTexture(width, height, 24); texture.Create();
        var overlays = UnityEngine.Object.FindObjectsByType<Canvas>().Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        view.targetTexture = texture;
        foreach (var canvas in overlays) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = view; canvas.planeDistance = view.nearClipPlane + 0.02f; }
        yield return null;
        yield return null;
        Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(view, new RenderPipeline.StandardRequest { destination = texture });
        var old = RenderTexture.active; RenderTexture.active = texture;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/Core_" + name + ".png", image.EncodeToPNG());
        RenderTexture.active = old;
        foreach (var canvas in overlays) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
        view.targetTexture = null;
        UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(image);
        report.Add("Captured Logs/Core_" + name + ".png");
    }

    private static T GetField<T>(object o, string field) =>
        (T)o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(o);

    private static void Invoke(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, args);

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); report.Add(message); }

    private static void Fail(Exception ex)
    {
        SessionState.SetBool(Key, false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/CoreSystemsSmokeFailure.txt", ex + "\n" + string.Join("\n", report));
        Debug.LogError(ex); EditorApplication.Exit(1);
    }
}
