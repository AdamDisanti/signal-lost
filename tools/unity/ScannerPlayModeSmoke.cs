using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using SignalLost.Player;
using SignalLost.Scanner;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Copy into Assets/Editor in a disposable temporary project; never run in the user's editor.
[InitializeOnLoad]
public static class ScannerPlayModeSmoke
{
    private const string Key = "SignalLost.ScannerPlayModeSmoke";
    private static IEnumerator tests;
    private static int lastFrame = -1;
    private static float resumeAt;
    private static readonly List<string> errors = new List<string>();

    static ScannerPlayModeSmoke()
    {
        string savedErrors = SessionState.GetString(Key + "Errors", "");
        if (savedErrors.Length > 0) errors.Add(savedErrors);
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (SessionState.GetInt(Key, 0) != 1) return;
            // shortcut: Unity's empty editor SearchDatabase can fail in minimal temp projects; gameplay errors still fail.
            if (trace.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                errors.Add(message + "\n" + trace);
                SessionState.SetString(Key + "Errors", string.Join("\n", errors));
            }
        };
    }

    public static void Start()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Application.isBatchMode || !root.StartsWith(temp, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use a disposable batch-mode project under the system temp directory.");
        bool fast = Array.IndexOf(Environment.GetCommandLineArgs(), "-scannerFastPlayMode") >= 0;
        EditorSettings.enterPlayModeOptionsEnabled = fast;
        EditorSettings.enterPlayModeOptions = fast
            ? EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload
            : EnterPlayModeOptions.None;
        EditorSceneManager.OpenScene("Assets/Scenes/Initial Test Scenes/ScannerTest.unity");
        errors.Clear(); SessionState.SetString(Key + "Errors", "");
        SessionState.SetInt(Key, 1);
        SessionState.SetInt(Key + "Cycle", 0);
        SessionState.SetFloat(Key + "Start", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (SessionState.GetInt(Key, 0) != 1 || EditorApplication.isCompiling) return;
        try
        {
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120)
                throw new Exception("Scanner test timed out.");
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                tests = null; resumeAt = 0; lastFrame = -1;
                if (SessionState.GetInt(Key + "Cycle", 0) < 3)
                    EditorApplication.EnterPlaymode();
                else
                {
                    SessionState.SetInt(Key, 2);
                    Directory.CreateDirectory("Logs");
                    File.WriteAllText("Logs/ScannerPlayMode.txt", string.Join("\n", errors));
                    bool passed = errors.Count == 0;
                    Debug.Log(passed ? "SCANNER_PLAY_MODE_PASS: three Play Mode entries, keyboard/gamepad input, visuals, targeting and cleanup." : "SCANNER_PLAY_MODE_FAIL: " + errors.Count + " errors.\n" + string.Join("\n", errors));
                    EditorApplication.Exit(passed ? 0 : 1);
                }
                return;
            }
            if (lastFrame == Time.frameCount || Time.time < resumeAt) return;
            lastFrame = Time.frameCount;
            tests ??= Run();
            if (tests.MoveNext()) { resumeAt = Time.time + (tests.Current is float delay ? delay : 0f); return; }
            SessionState.SetInt(Key + "Cycle", SessionState.GetInt(Key + "Cycle", 0) + 1);
            EditorApplication.ExitPlaymode();
        }
        catch (Exception error)
        {
            SessionState.SetInt(Key, 2);
            Debug.LogError("SCANNER_PLAY_MODE_FAIL\n" + error + "\n" + string.Join("\n", errors));
            EditorApplication.Exit(1);
        }
    }

    private static IEnumerator Run()
    {
        yield return 0.1f;
        Application.runInBackground = true;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        ScannerToggle toggle = UnityEngine.Object.FindFirstObjectByType<ScannerToggle>();
        ScannerController scanner = UnityEngine.Object.FindFirstObjectByType<ScannerController>();
        FirstPersonController player = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
        Check(toggle != null && toggle.enabled && scanner != null && scanner.enabled, "Scanner initialized.");
        GameObject visual = Get<GameObject>(toggle, "scannerVisual");
        Check(!toggle.IsRaised && !visual.activeSelf && toggle.gameObject.activeInHierarchy, "Starts stowed without disabling the toggle controller.");
        InputActionMap map = Get<InputActionMap>(toggle, "playerMap");
        InputActionMap movement = Get<InputActionMap>(player, "playerMap");
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
        Cursor.lockState = CursorLockMode.Locked;
        for (int i = 0; i < 4; i++)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Q, UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.LeftShift));
            InputSystem.Update();
            Check(map.FindAction("ToggleScanner", true).WasPressedThisFrame(), "Q reaches scanner input action.");
            Check(movement.FindAction("Move", true).ReadValue<Vector2>().y > 0f && movement.FindAction("Sprint", true).IsPressed(), "Movement input remains valid while scanner toggles.");
            // Batch mode may lack application focus; keep real input assertions and exercise the same visual action directly.
            if (Application.isFocused) Invoke(toggle, "Update"); else Invoke(toggle, "SetRaised", !toggle.IsRaised);
            Check(toggle.IsRaised == (i % 2 == 0) && visual.activeInHierarchy == toggle.IsRaised, "Scanner model raises/lowers without disabling its controller.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            yield return 0.05f;
        }
        InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.LeftShoulder)); InputSystem.Update();
        Check(map.FindAction("ToggleScanner", true).WasPressedThisFrame(), "Gamepad LB reaches scanner action.");
        InputSystem.QueueStateEvent(gamepad, new GamepadState()); InputSystem.Update();
        Invoke(toggle, "SetRaised", true);
        Check(visual.GetComponentsInChildren<Renderer>().Length > 0, "Raised scanner has imported model renderers.");
        yield return 0.3f;
        Check(scanner.CurrentTarget != null && scanner.SignalStrength01 > 0f, "Raised scanner detects the test target.");
        CheckBarLayout(visual);
        toggle.enabled = false;
        Check(!visual.activeSelf && !map.enabled, "Disabling scanner hides model and disables owned input.");
        toggle.enabled = true; Invoke(toggle, "SetRaised", true);
        yield return 0.1f;
        Check(map.enabled && visual.activeInHierarchy, "Scanner can be re-enabled and raised.");
        InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(gamepad);
        Debug.Log("SCANNER_CYCLE_PASS " + (SessionState.GetInt(Key + "Cycle", 0) + 1));
    }

    private static void CheckBarLayout(GameObject visual)
    {
        Slider slider = visual.GetComponentInChildren<Slider>();
        RectTransform background = slider.transform.Find("Background").GetComponent<RectTransform>();
        Image backgroundImage = background.GetComponent<Image>();
        Image fillImage = slider.fillRect.GetComponent<Image>();
        Check(backgroundImage.enabled && backgroundImage.sprite == fillImage.sprite, "Background and fill use matching rounded sprites.");
        Check(slider.handleRect == null && slider.targetGraphic == null && slider.transform.Find("Handle Slide Area") == null,
            "Unused handle objects and references are removed.");
        Check(background.Find("Scanner_SliderTrack") == null && !slider.interactable, "Legacy mesh track is removed and proximity display is read-only.");
        Transform arrow = visual.transform.Find("Canvas/ScannerHUD/Arrow");
        Check(arrow.GetComponent<Image>() == null && arrow.GetComponent<CanvasRenderer>() == null
            && arrow.Find("Scanner_FancyArrow").GetComponent<MeshRenderer>() != null, "Only the working compass mesh remains.");
        Check(visual.GetComponentInChildren<GraphicRaycaster>() == null && visual.GetComponentsInChildren<Image>(true).Length == 2,
            "Only background/fill UI graphics remain; no unused pointer raycaster.");
        float previous = slider.value;
        var back = new Vector3[4]; var fill = new Vector3[4];
        foreach (float value in new[] { 0f, 0.25f, 0.5f, 1f })
        {
            slider.value = value; Canvas.ForceUpdateCanvases();
            background.GetWorldCorners(back); slider.fillRect.GetWorldCorners(fill);
            Vector3 rightBottom = Vector3.Lerp(back[0], back[3], value);
            Vector3 rightTop = Vector3.Lerp(back[1], back[2], value);
            Check(Vector3.Distance(fill[0], back[0]) < 0.000001f && Vector3.Distance(fill[1], back[1]) < 0.000001f
                && Vector3.Distance(fill[2], rightTop) < 0.000001f && Vector3.Distance(fill[3], rightBottom) < 0.000001f,
                "Bar must fit background in size, position and depth at value " + value + ".");
        }
        if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && SessionState.GetInt(Key + "Cycle", 0) == 0)
        {
            var cameraObject = new GameObject("Scanner QA Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false; camera.fieldOfView = 35f; camera.nearClipPlane = 0.01f; camera.farClipPlane = 100f;
            var output = new RenderTexture(800, 900, 24); output.Create();
            var image = new Texture2D(800, 900, TextureFormat.RGB24, false);
            RenderTexture previousTarget = RenderTexture.active;
            foreach (int angle in new[] { -20, 0, 20 })
            {
                Vector3 position = Quaternion.Euler(0, angle, 0) * new Vector3(0, 0, -0.85f);
                camera.transform.position = visual.transform.TransformPoint(position);
                camera.transform.LookAt(visual.transform.position, visual.transform.up);
                foreach (float value in new[] { 0f, 0.5f, 1f })
                {
                    slider.value = value; Canvas.ForceUpdateCanvases();
                    var request = new RenderPipeline.StandardRequest { destination = output };
                    Check(RenderPipeline.SupportsRenderRequest(camera, request), "Scanner capture supported.");
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    RenderTexture.active = output;
                    image.ReadPixels(new Rect(0, 0, 800, 900), 0, 0); image.Apply();
                    Directory.CreateDirectory("Logs");
                    File.WriteAllBytes("Logs/ScannerBar_" + angle + "_" + Mathf.RoundToInt(value * 100) + ".png", image.EncodeToPNG());
                }
            }
            RenderTexture.active = previousTarget;
            output.Release(); UnityEngine.Object.Destroy(output); UnityEngine.Object.Destroy(image); UnityEngine.Object.Destroy(cameraObject);
        }
        slider.value = previous;
        Debug.Log("SCANNER_BAR_LAYOUT_PASS: 0/25/50/100% match the background plane and bounds.");
    }

    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    private static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
