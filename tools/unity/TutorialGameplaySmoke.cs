using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using SignalLost.Combat;
using SignalLost.Editor;
using SignalLost.Player;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Copy into a disposable temp project's Assets/Editor; never run against the user's open project.
[InitializeOnLoad]
public static class TutorialGameplaySmoke
{
    private const string SessionKey = "SignalLost.TutorialGameplaySmoke";
    private static IEnumerator tests;
    private static float resumeAt;
    private static int lastFrame = -1;
    private static readonly List<string> report = new List<string>();
    private static TutorialPlayer player;
    private static FirstPersonController movement;
    private static CharacterController body;
    private static Camera view;
    private static Terrain terrain;
    private static Mouse mouse;
    private static Keyboard keyboard;

    static TutorialGameplaySmoke()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, trace, type) =>
        {
            // shortcut: minimal temp projects can hit Unity's empty SearchDatabase startup bug; remove when Unity fixes it.
            if (trace.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (SessionState.GetInt(SessionKey, 0) == 1 && (type == LogType.Error || type == LogType.Exception))
                Fail(new Exception(message + "\n" + trace));
        };
    }

    public static void Start()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Application.isBatchMode || !root.StartsWith(temp, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use a disposable batch-mode project under the system temp directory.");
        EditorSceneManager.OpenScene(TutorialCrashSiteBuilder.ScenePath);
        TutorialGameplayBuilder.Validate();
        SessionState.SetInt(SessionKey, 1);
        SessionState.SetFloat(SessionKey + "Start", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (SessionState.GetInt(SessionKey, 0) != 1) return;
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(SessionKey + "Start", 0) > 150)
        { Fail(new Exception("Gameplay test timed out.")); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < resumeAt || lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        try
        {
            tests ??= Run();
            if (tests.MoveNext()) { resumeAt = Time.time + (tests.Current is float seconds ? seconds : 0f); return; }
            SessionState.SetInt(SessionKey, 2);
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/TutorialGameplayPlayMode.txt", report);
            Debug.Log("TUTORIAL_GAMEPLAY_PLAY_MODE_PASS\n" + string.Join("\n", report));
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Fail(error); }
    }

    private static IEnumerator Run()
    {
        yield return 0.25f;
        player = UnityEngine.Object.FindFirstObjectByType<TutorialPlayer>();
        Check(player != null && player.enabled, "Player initialized.");
        movement = player.GetComponent<FirstPersonController>(); body = player.GetComponent<CharacterController>();
        view = player.GetComponentInChildren<Camera>(); terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
        MiningLaser laser = player.GetComponent<MiningLaser>();
        HUDController hud = UnityEngine.Object.FindFirstObjectByType<HUDController>();
        Transform tool = Get<Transform>(laser, "toolPivot");
        Check(!player.HasLaser && !player.HasFlask && !laser.enabled && !tool.gameObject.activeInHierarchy, "Starts unarmed with both pickups still in the world.");
        Check(!player.TryDrink() && !player.TryInteract(), "Cannot drink without the flask or pick up distant items.");
        Check(player.Health.Current == 100f && hud.healthSlider.value == 100f, "Existing Health backend starts the player at 100 HP and initializes the HUD.");
        Check(hud.healthSlider.GetComponent<RectTransform>().anchorMin == new Vector2(0, 1), "Health bar is anchored upper left.");
        var initialTargets = new Dictionary<string, Vector3>();
        foreach (TrainingDummy target in UnityEngine.Object.FindObjectsByType<TrainingDummy>()) initialTargets.Add(target.name, target.transform.position);
        Check(initialTargets.Count == 3, "Three stage-start practice targets are present.");
        Vector3 spawn = Get<Vector3>(player, "spawnPosition");
        mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>();
        movement.enabled = false; Cursor.lockState = CursorLockMode.Locked;
        bool input = Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
        report.Add("Synthetic mouse/keyboard action path available: " + input + ". No physical-controller check.");

        TutorialPickup worldLaser = GameObject.Find("BurntRockBasin").transform.Find("MiningLaserAnchor/Mining Laser").GetComponent<TutorialPickup>();
        AimAtPickup(worldLaser);
        if (input) { KeyDown(Key.E); yield return 0.08f; KeyDown(); }
        else Check(player.TryInteract(), "Laser interaction ray succeeds.");
        Check(player.HasLaser && laser.enabled && tool.gameObject.activeInHierarchy && !worldLaser.gameObject.activeSelf, "Laser pickup equips the combat-demo model once and removes the world pickup.");
        Check(!worldLaser.TryCollect(player), "An already collected pickup cannot be collected twice.");
        foreach (Collider item in tool.GetComponentsInChildren<Collider>()) Check(!item.enabled, "Held weapon does not collide with the player.");
        TutorialPickup worldFlask = GameObject.Find("AlcoholFlask").GetComponent<TutorialPickup>();
        AimAtPickup(worldFlask);
        if (input) { KeyDown(Key.E); yield return 0.08f; KeyDown(); }
        else Check(player.TryInteract(), "Flask interaction ray succeeds.");
        Check(player.HasFlask && !worldFlask.gameObject.activeSelf, "Existing flask can be picked up and retained.");

        PlayerDamageZone zone = UnityEngine.Object.FindFirstObjectByType<PlayerDamageZone>();
        Foot(zone.transform.position + Vector3.up * 0.2f); movement.enabled = true;
        yield return 0.35f;
        Check(player.Health.Current == 80f && hud.healthSlider.value == 80f, "Walking onto the red pad applies one 20-HP damage tick and updates the HUD.");
        movement.enabled = false; Foot(spawn); Cursor.lockState = CursorLockMode.Locked;
        player.Health.TakeDamage(30f);
        Check(player.TryDrink(), "Flask begins a sip after damage.");
        Transform flask = Get<Transform>(player, "heldFlask"); Vector3 flaskRest = flask.localPosition;
        Check(flask.gameObject.activeInHierarchy && !laser.enabled && !tool.gameObject.activeInHierarchy, "Drinking displays the flask and suspends combat/held laser.");
        Check(!player.TryDrink(), "A sip cannot be spammed or overlapped.");
        yield return 0.25f;
        Check(Vector3.Distance(flask.localPosition, flaskRest) > 0.02f, "Existing flask mesh animates toward the mouth.");
        if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            UnityEngine.Object.Destroy(CaptureView("TutorialFlaskDrink", hud.GetComponent<Canvas>()));
        yield return 0.3f;
        Check(player.Health.Current == 85f && player.DrunkStacks == 1 && hud.healthSlider.value == 85f, "A completed sip heals 35 HP, adds exactly one stack and updates health UI.");
        yield return 0.75f;
        Check(!player.IsDrinking && laser.enabled && !flask.gameObject.activeSelf, "Sip finishes and restores the equipped weapon.");
        Check(player.TryDrink(), "Flask has no use-count limit."); yield return 1.3f;
        Check(player.Health.Current == 100f && player.DrunkStacks == 2, "Healing is capped at max HP while drunkenness still stacks.");
        Volume volume = Get<Volume>(player, "drunkVolume");
        Check(movement.MovementScale < 1f, "Stacks reduce movement speed: " + movement.MovementScale);
        Check(volume.weight > 0, "Stacks drive drunk volume weight: " + volume.weight);
        Check(view.GetUniversalAdditionalCameraData().renderPostProcessing, "Player camera enables URP post-processing.");
        Check(volume.profile.TryGet<LensDistortion>(out var lens) && lens.active && lens.intensity.value < 0, "Screen distortion is configured and driven at runtime.");
        Check(Get<float>(player, "secondsPerStack") == 15f, "Authored sober recovery is one stack every 15 seconds without another drink.");
        Set(player, "secondsPerStack", 0.2f); Set(player, "nextSoberStack", Time.time + 0.2f);
        yield return 0.25f; Check(player.DrunkStacks == 1, "First stack recovers gradually.");
        yield return 0.25f; Check(player.DrunkStacks == 0 && volume.weight == 0 && movement.MovementScale == 1, "Recovery returns movement and vision to sober defaults.");
        Set(player, "secondsPerStack", 15f);

        TrainingDummy close = FindTarget("Tutorial Target 1"); AimAtTarget(close, 1.7f);
        yield return 0.05f;
        for (int i = 0; i < 3; i++)
        {
            if (input) PressMouse(true, false); else Invoke(laser, "StartSwing");
            yield return 0.04f;
            if (input) PressMouse(false, false);
            yield return i == 2 ? 0.71f : 0.43f;
            float expected = i == 0 ? 80 : i == 1 ? 60 : 20;
            Check(close.GetComponent<Health>().Current == expected, "Combat-demo combo strike " + (i + 1) + " applies its original damage.");
        }
        Check(player.Health.Current == 100, "Melee never damages the player.");
        TrainingDummy mid = FindTarget("Tutorial Target 2"); AimAtTarget(mid, 4f);
        if (input)
        {
            PressMouse(false, true); yield return 1.3f;
            Check(laser.IsCharging && laser.Charge01 >= 0.99f, "Holding secondary fire reaches full charge.");
            Check(Get<Component>(laser, "chargeEffect").GetComponentsInChildren<Renderer>().Length > 0, "Original charge-effect renderers exist.");
            PressMouse(false, false);
        }
        else Invoke(laser, "FireBolt", 1f);
        yield return 0.5f;
        Check(mid.GetComponent<Health>().Current == 30f, "Full-charge bolt travels and deals the original 70 damage.");
        if (input)
        {
            yield return 0.2f;
            PressMouse(false, true); yield return 0.04f; PressMouse(false, false); yield return 0.35f;
            Check(mid.GetComponent<Health>().Current > 0f && mid.GetComponent<Health>().Current < 30f, "Short charge produces a weaker shot and respects the cooldown.");
        }

        for (int stack = 1; stack <= 8; stack++)
        {
            Check(player.TryDrink(), "Unlimited drink " + stack + " accepted, even at full HP."); yield return 1.3f;
            Check(player.DrunkStacks == stack && !player.IsRespawning, "Stack " + stack + " remains below the blackout threshold.");
        }
        Check(Mathf.Approximately(movement.MovementScale, 19f / 30f), "Eight stacks retain 63.33% walk/sprint speed: slowdown reduced by one third.");
        if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
        {
            laser.enabled = false;
            volume.weight = 0;
            Texture2D sober = CaptureView("TutorialSober", hud.GetComponent<Canvas>());
            volume.weight = 1;
            Texture2D drunk = CaptureView("TutorialDrunk", hud.GetComponent<Canvas>());
            Color32[] a = sober.GetPixels32(), b = drunk.GetPixels32();
            int changed = 0;
            for (int i = 0; i < a.Length; i++)
                if (Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b) > 30) changed++;
            Check(changed > a.Length / 100, "Actual URP-rendered image changes with intoxication: " + changed + " pixels.");
            UnityEngine.Object.Destroy(sober); UnityEngine.Object.Destroy(drunk);
            laser.enabled = true;
        }
        DisturbTargets();
        Check(player.TryDrink(), "Ninth drink accepted."); yield return 0.55f;
        Check(player.DrunkStacks == 9 && player.IsRespawning && !movement.enabled && !laser.enabled, "Blackout occurs above eight stacks, not at eight, and blocks movement/combat.");
        yield return 1.7f;
        CheckRespawn(spawn, hud, initialTargets);
        report.Add("Blackout restores stage-start enemy positions and full health, including a destroyed target.");
        DisturbTargets();
        Foot(zone.transform.position + Vector3.up * 0.2f);
        yield return 4.35f;
        Check(player.Health.IsDead && player.IsRespawning, "Remaining on the pad causes real health-system death.");
        yield return 1.7f;
        CheckRespawn(spawn, hud, initialTargets);
        Check(!worldFlask.gameObject.activeSelf && !worldLaser.gameObject.activeSelf, "Death does not duplicate or respawn collected basic pickups.");
        report.Add("Death retains laser/flask and resets all tutorial enemies to their stage-start state.");

        EditorSceneManager.LoadSceneInPlayMode(CombatTestBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        yield return 0.4f;
        MiningLaser demoLaser = UnityEngine.Object.FindFirstObjectByType<MiningLaser>();
        Check(demoLaser != null && demoLaser.enabled && Get<Transform>(demoLaser, "toolPivot").gameObject.activeInHierarchy, "Original combat demo still starts equipped and functional.");
        TrainingDummy demoTarget = UnityEngine.Object.FindFirstObjectByType<TrainingDummy>();
        demoTarget.GetComponent<Health>().TakeDamage(999f); yield return 3.2f;
        Check(!demoTarget.GetComponent<Health>().IsDead && demoTarget.GetComponent<Renderer>().enabled, "Original combat-demo target still auto-respawns after three seconds.");
    }

    private static Texture2D CaptureView(string name, Canvas canvas)
    {
        // Overlay UI is temporarily camera-bound only for the off-screen QA image.
        RenderMode mode = canvas.renderMode; Camera previousCamera = canvas.worldCamera;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = view; canvas.planeDistance = 0.35f;
        var output = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32); output.Create();
        // Standard requests run the normal camera-stack setup, including volume/post-processing updates.
        var request = new RenderPipeline.StandardRequest { destination = output };
        Check(RenderPipeline.SupportsRenderRequest(view, request), "URP off-screen capture supported.");
        Canvas.ForceUpdateCanvases(); RenderPipeline.SubmitRenderRequest(view, request);
        RenderTexture previous = RenderTexture.active; RenderTexture.active = output;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
        Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/" + name + ".png", image.EncodeToPNG());
        RenderTexture.active = previous; output.Release(); UnityEngine.Object.Destroy(output);
        canvas.renderMode = mode; canvas.worldCamera = previousCamera;
        return image;
    }

    private static void CheckRespawn(Vector3 spawn, HUDController hud, Dictionary<string, Vector3> initial)
    {
        Check(!player.IsRespawning && player.Health.Current == 100 && hud.healthSlider.value == 100, "Respawn restores full health/HUD.");
        Check(player.HasLaser && player.HasFlask && player.GetComponent<MiningLaser>().enabled && movement.enabled, "Respawn retains collected basic gear and restores controls.");
        Check(player.DrunkStacks == 0 && movement.MovementScale == 1 && Get<Volume>(player, "drunkVolume").weight == 0, "Respawn is sober, with no lingering movement or screen penalty.");
        Check(Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z), new Vector2(spawn.x, spawn.z)) < 0.1f, "Player returns to the starting location.");
        var targets = UnityEngine.Object.FindObjectsByType<TrainingDummy>();
        Check(targets.Length == initial.Count, "Every stage-start enemy respawns.");
        foreach (TrainingDummy target in targets)
            Check(Vector3.Distance(target.transform.position, initial[target.name]) < 0.01f && target.GetComponent<Health>().Current == 100f && target.GetComponent<Renderer>().enabled && target.GetComponent<Collider>().enabled, "Reset target: " + target.name);
    }

    private static void DisturbTargets()
    {
        var targets = UnityEngine.Object.FindObjectsByType<TrainingDummy>();
        foreach (TrainingDummy target in targets) target.GetComponent<Health>().TakeDamage(999f);
        targets[0].transform.position += Vector3.right * 7;
        UnityEngine.Object.Destroy(targets[1].gameObject);
    }

    private static TrainingDummy FindTarget(string name) => GameObject.Find(name).GetComponent<TrainingDummy>();

    private static void AimAtPickup(TutorialPickup pickup)
    {
        Collider collider = pickup.GetComponentInChildren<Collider>();
        for (int i = 0; i < 32; i++)
        {
            float a = i * Mathf.PI / 16;
            Vector3 point = collider.bounds.center + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 1.8f;
            point.y = terrain.SampleHeight(point) + terrain.transform.position.y + 0.05f;
            Foot(point); view.transform.LookAt(collider.bounds.center);
            if (Physics.Raycast(view.transform.position, view.transform.forward, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<TutorialPickup>() == pickup) return;
        }
        throw new Exception("No reachable interaction ray for " + pickup.name);
    }

    private static void AimAtTarget(TrainingDummy target, float distance)
    {
        Collider collider = target.GetComponent<Collider>();
        for (int i = 0; i < 32; i++)
        {
            float a = i * Mathf.PI / 16;
            Vector3 point = collider.bounds.center + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * distance;
            point.y = terrain.SampleHeight(point) + terrain.transform.position.y + 0.05f;
            Foot(point); view.transform.LookAt(collider.bounds.center);
            if (Physics.Raycast(view.transform.position, view.transform.forward, out RaycastHit hit, distance + 2, ~0, QueryTriggerInteraction.Ignore) && hit.collider == collider) return;
        }
        throw new Exception("No test aim ray to " + target.name);
    }

    private static void Foot(Vector3 point)
    { body.enabled = false; player.transform.position = point; body.enabled = true; Physics.SyncTransforms(); }
    private static void PressMouse(bool left, bool right)
    { var state = new MouseState(); if (left) state = state.WithButton(MouseButton.Left); if (right) state = state.WithButton(MouseButton.Right); InputSystem.QueueStateEvent(mouse, state); }
    private static void KeyDown(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static void Check(bool condition, string description)
    { if (!condition) throw new Exception(description); report.Add("PASS: " + description); }
    private static void Fail(Exception error)
    {
        SessionState.SetInt(SessionKey, 3);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/TutorialGameplayPlayMode.txt", string.Join("\n", report) + "\nFAIL: " + error);
        Debug.LogException(error);
        EditorApplication.Exit(1);
    }
}
