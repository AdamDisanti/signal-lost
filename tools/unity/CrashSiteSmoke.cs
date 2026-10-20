using System;
using System.Collections.Generic;
using System.IO;
using SignalLost.Editor;
using SignalLost.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Copy into a disposable temp project's Assets/Editor to run; never installed in the user's Assets.
[InitializeOnLoad]
public static class CrashSiteSmoke
{
    private const string Key = "SignalLost.CrashSiteSmoke";
    static CrashSiteSmoke() { EditorApplication.update += Tick; }

    public static void Start()
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Application.isBatchMode || !project.StartsWith(temp, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Run only in a disposable batch-mode project under the system temp directory.");
        EditorSceneManager.OpenScene(TutorialCrashSiteBuilder.ScenePath);
        if (GameObject.Find("Perimeter Cliffs") != null) CrashSiteUpgradeValidation.Validate();
        else TutorialCrashSiteBuilder.Validate();
        if (GameObject.Find(TutorialTraversalBuilder.LandscapeName) != null) TutorialTraversalSmoke.Validate();
        if (GameObject.Find("Lunar Landscape") != null) LunarTutorialSmoke.Validate();
        SessionState.SetInt(Key, 1);
        SessionState.SetFloat(Key + "Start", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (SessionState.GetInt(Key, 0) != 1) return;
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 120)
        { Fail(new Exception("Play Mode entry timed out.")); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetInt(Key, 2);
        try
        {
            var report = new List<string>();
            Terrain terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();
            var controller = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
            if (controller == null || !controller.enabled) throw new Exception("Player failed to initialize.");
            CharacterController cc = controller.GetComponent<CharacterController>();
            Vector3 start = controller.transform.position;
            controller.enabled = false; // Exercise the authored controller collision, without synthetic input/cursor changes.
            Physics.SyncTransforms();
            Settle(cc);
            if (!cc.isGrounded) throw new Exception("Player did not ground at spawn.");
            report.Add("Player initialized with input/camera references and grounded at saved spawn.");
            foreach (string name in new[] { "CapybaraAstronaut", "BurntRockBasin", "AlcoholFlask", "GrayboxExitGate" })
            {
                GameObject prop = GameObject.Find(name);
                if (prop == null) throw new Exception("Missing preserved prop: " + name);
                Reset(cc, start);
                Vector3 target = prop.transform.position;
                if (name != "GrayboxExitGate")
                {
                    Vector3 back = start - target; back.y = 0;
                    target += back.normalized * 3.2f;
                }
                Walk(cc, target, terrain);
                report.Add("Traversed spawn-to-" + name + " route without jumping.");
                if (name == "GrayboxExitGate")
                {
                    Walk(cc, target + prop.transform.forward * 50f, terrain);
                    report.Add("Passed through gate and traversed existing crater saddle without jumping.");
                }
            }
            Transform environment = GameObject.Find(TutorialCrashSiteBuilder.EnvironmentName).transform;
            Transform ship = environment.Find("Crashed Ship") ?? environment.Find("Enhanced Crashed Rocket/Crashed Ship");
            bool hitShip = false;
            foreach (MeshCollider collider in ship.GetComponentsInChildren<MeshCollider>())
                hitShip |= collider.Raycast(new Ray(collider.bounds.center + Vector3.up * 20, Vector3.down), out _, 40);
            if (!hitShip) throw new Exception("No valid crashed-ship collision surface.");
            report.Add("Ship mesh collision raycast passed; buried nose and scene references validated.");
            int cameras = 0, listeners = 0;
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var viewpoints = new List<string>();
            foreach (Camera camera in UnityEngine.Object.FindObjectsByType<Camera>())
            {
                viewpoints.Add("Camera " + camera.name + " scene=" + camera.gameObject.scene.name + " enabled=" + camera.enabled);
                if (camera.enabled && camera.gameObject.scene == activeScene) cameras++;
            }
            foreach (AudioListener listener in UnityEngine.Object.FindObjectsByType<AudioListener>())
            {
                viewpoints.Add("Listener " + listener.name + " scene=" + listener.gameObject.scene.name + " enabled=" + listener.enabled);
                if (listener.enabled && listener.gameObject.scene == activeScene) listeners++;
            }
            if (cameras != 1 || listeners != 1)
                throw new Exception("Expected one Tutorial gameplay camera/listener; got " + cameras + "/" + listeners + "\n" + string.Join("\n", viewpoints));
            report.Add("Exactly one gameplay camera and AudioListener active.");
            if (environment.Find(TutorialTraversalBuilder.LandscapeName) != null)
            {
                TutorialTraversalSmoke.TestRoutes(cc, terrain, environment, controller);
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) TutorialTraversalSmoke.CaptureViews(environment);
            }
            if (environment.Find("Lunar Landscape") != null)
            {
                LunarTutorialSmoke.TestRoutes(cc, terrain, environment, controller);
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) LunarTutorialSmoke.CaptureViews(environment, terrain);
            }
            if (environment.Find("Perimeter Cliffs") != null)
            {
                TestCliffs(cc, terrain, environment);
                report.Add("Cliff perimeter blocked sprinting/repeated 1.2m jumps from 64 directions, including formation seams.");
            }
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                Reset(cc, start);
                Capture(ship.parent.position + new Vector3(-70, 72, -72), ship.parent.position + Vector3.up * 1.5f, "ImpactSiteOverview");
                Capture(ship.parent.position + new Vector3(-20, 10, -21), ship.position + Vector3.up, "CrashedRocket");
                if (environment.Find("Perimeter Cliffs") != null)
                {
                    bool fog = RenderSettings.fog; RenderSettings.fog = false; // Unfogged bird's-eye inspection, not a gameplay setting.
                    Capture(environment.position + new Vector3(-185, 205, -205), environment.position, "ExpandedImpactSiteOverview");
                    RenderSettings.fog = fog;
                    Transform basin = GameObject.Find("BurntRockBasin").transform;
                    Capture(basin.position + new Vector3(1.5f, 2.8f, 1.5f), basin.position + Vector3.up * 0.75f, "MiningLaserInBasin");
                    Capture(environment.position + new Vector3(-75, 3, -90), environment.position + new Vector3(-110, 14, -100), "PerimeterCliffs");
                    Capture(ship.position + new Vector3(16, 11, -20), ship.position + Vector3.up * 2, "RocketEngines");
                }
                report.Add("Unity URP overview, wreck, basin laser and cliff screenshots rendered.");
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/CrashSitePlayMode.txt", report);
            Debug.Log("CRASH_SITE_PLAY_MODE_PASS\n" + string.Join("\n", report));
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Fail(error); }
    }

    private static void TestCliffs(CharacterController cc, Terrain terrain, Transform environment)
    {
        // Isolate the cliff itself rather than accidentally passing because a loose rock blocked the probe.
        foreach (Collider c in environment.Find("Basalt Rocks").GetComponentsInChildren<Collider>()) c.enabled = false;
        Transform outlying = environment.Find("Traversal Landscape/Outlying Rock Fields");
        if (outlying != null) foreach (Collider c in outlying.GetComponentsInChildren<Collider>()) c.enabled = false;
        Transform lunar = environment.Find("Lunar Landscape");
        if (lunar != null) foreach (Collider c in lunar.GetComponentsInChildren<Collider>()) c.enabled = false;
        float expansion = terrain.terrainData.size.x / 320f;
        for (int i = 0; i < 64; i++)
        {
            float a = i * Mathf.PI * 2 / 64;
            Vector3 direction = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            Vector3 start = environment.position + direction * (130 * expansion);
            start.y = terrain.SampleHeight(start) + terrain.transform.position.y + 0.1f;
            Reset(cc, start);
            float vertical = 0;
            const float dt = 1f / 60;
            for (int frame = 0; frame < 280; frame++)
            {
                if (cc.isGrounded && vertical <= 0) vertical = Mathf.Sqrt(2 * 20 * 1.2f);
                vertical -= 20 * dt;
                cc.Move(direction * (7 * dt) + Vector3.up * (vertical * dt));
                Vector3 radial = cc.transform.position - environment.position; radial.y = 0;
                if (radial.magnitude > 149 * expansion) throw new Exception("Player escaped cliff perimeter at direction " + i);
            }
        }
    }

    private static void Settle(CharacterController cc)
    { for (int i = 0; i < 40; i++) cc.Move(Vector3.down * 0.04f); }

    internal static void Reset(CharacterController cc, Vector3 start)
    { cc.enabled = false; cc.transform.position = start; cc.enabled = true; Settle(cc); }

    private static void Walk(CharacterController cc, Vector3 target, Terrain terrain)
    {
        Vector3 delta = target - cc.transform.position; delta.y = 0;
        int budget = Mathf.CeilToInt(delta.magnitude / 0.07f) * 3 + 200;
        while (budget-- > 0)
        {
            delta = target - cc.transform.position; delta.y = 0;
            if (delta.magnitude < 0.15f) break;
            cc.Move(delta.normalized * Mathf.Min(0.07f, delta.magnitude) + Vector3.down * 0.08f);
        }
        delta = target - cc.transform.position; delta.y = 0;
        if (delta.magnitude > 0.25f) throw new Exception("Route blocked at " + cc.transform.position + " towards " + target);
        float ground = terrain.SampleHeight(cc.transform.position) + terrain.transform.position.y;
        if (Mathf.Abs(cc.transform.position.y - ground) > 0.25f || !cc.isGrounded)
            throw new Exception("Controller not grounded on terrain at route end.");
    }

    private static void Capture(Vector3 position, Vector3 target, string name)
    {
        var obj = new GameObject("Temporary Preview Camera");
        Camera camera = obj.AddComponent<Camera>();
        camera.enabled = false;
        obj.AddComponent<UniversalAdditionalCameraData>();
        camera.transform.position = position;
        camera.transform.LookAt(target);
        camera.fieldOfView = 58;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 650;
        camera.clearFlags = CameraClearFlags.Skybox;
        var output = new RenderTexture(1400, 900, 24, RenderTextureFormat.ARGB32);
        output.Create();
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = output };
        if (!RenderPipeline.SupportsRenderRequest(camera, request)) throw new Exception("URP render request unavailable.");
        RenderPipeline.SubmitRenderRequest(camera, request);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = output;
        var image = new Texture2D(1400, 900, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1400, 900), 0, 0); image.Apply();
        Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/" + name + ".png", image.EncodeToPNG());
        RenderTexture.active = previous;
        UnityEngine.Object.Destroy(image); output.Release(); UnityEngine.Object.Destroy(output);
        UnityEngine.Object.Destroy(obj);
    }

    private static void Fail(Exception error)
    {
        SessionState.SetInt(Key, 3);
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/CrashSitePlayMode.txt", "FAIL: " + error);
        Debug.LogException(error); EditorApplication.Exit(1);
    }
}
