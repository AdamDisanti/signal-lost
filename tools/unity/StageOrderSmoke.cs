// Copy-only smoke (never in the real project; put it in Assets/Editor/Smoke of a disposable copy under %TEMP%).
// Batch: -executeMethod StageOrderSmoke.Run (it exits itself). Output folder: env STAGE_SMOKE_OUT.
// 1. Links the stage order (tutorial -> Mudlands -> desert -> caves) and validates it.
// 2. Rebuilds what the change touches: the desert camp, the caves' camp and story, the Mudlands level (checking that
//    nothing but the camp's stands and the dock changed: the builder keeps its random numbers), and Briarwing.
// 3. Renders each car style (front three-quarter, side, back three-quarter, top) and a frame strip of Briarwing's
//    imported Flap, Hover, Takeoff and Land clips.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SignalLost.Editor;
using SignalLost.Mudlands;
using SignalLost.Progression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class StageOrderSmoke
{
    private static readonly StringBuilder Report = new StringBuilder();
    private static int failures;
    private static string Out => Environment.GetEnvironmentVariable("STAGE_SMOKE_OUT") ?? Path.Combine(Path.GetTempPath(), "StageOrderSmoke");

    private static void Note(string line) { Report.AppendLine(line); Debug.Log("STAGESMOKE " + line); }
    private static void Check(bool ok, string what) { if (ok) Note("ok: " + what); else { failures++; Note("FAIL: " + what); } }

    public static void Run()
    {
        try
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            string temporary = Path.GetFullPath(Path.GetTempPath()).Replace('\\', '/').TrimEnd('/') + "/";
            if (!Application.isBatchMode || !project.StartsWith(temporary, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Use only a disposable batch-mode project under the system temporary directory.");
            Directory.CreateDirectory(Out);
            string only = Environment.GetEnvironmentVariable("STAGE_SMOKE_ONLY") ?? "";
            bool Step(string name) => only.Length == 0 || only.Split(',').Contains(name);

            if (Step("link"))
            {
                StageOrderLinker.LinkAll();
                StageOrderLinker.LinkAll(); // a second run must change nothing (logged)
            }
            if (Step("camps"))
            {
                BaseCampBuilder.BuildDesert();
                var desertCamp = AssetDatabase.LoadAssetAtPath<GameObject>(BaseCampBuilder.DesertPrefabPath);
                int[] desertStands = desertCamp.GetComponentsInChildren<TrophyStand>(true).Select(s => s.Index).OrderBy(i => i).ToArray();
                Check(desertStands.SequenceEqual(new[] { 0, 1, 2, 3, 4, 9 }), "desert camp stands are the tutorial's, the Mudlands' and the desert's: " + string.Join(",", desertStands));
                CrystalCavesBuilder.InstallStory();
                CrystalCavesBuilder.ValidateStory();
                Note("ok: Crystal Caves story installed and validated");
            }
            if (Step("mudlands"))
            {
                EditorSceneManager.OpenScene(MudlandsBuilder.ScenePath);
                Dictionary<string, Vector3> before = Scenery();
                MudlandsBuilder.Build();
                EditorSceneManager.OpenScene(MudlandsBuilder.ScenePath);
                Dictionary<string, Vector3> after = Scenery();
                var moved = before.Where(p => after.TryGetValue(p.Key, out Vector3 now) && (now - p.Value).sqrMagnitude > 1e-6f).Select(p => p.Key).ToList();
                var gone = before.Keys.Except(after.Keys).ToList();
                var added = after.Keys.Except(before.Keys).ToList();
                Note($"Mudlands scenery: {before.Count} before, {after.Count} after, {moved.Count} moved, {gone.Count} gone, {added.Count} new");
                foreach (string s in moved.Concat(gone).Concat(added).Take(20)) Note("  changed: " + s);
                Check(moved.Count == 0 && gone.Count == 0 && added.Count == 0, "the Mudlands rebuild moved no scenery outside the camp's stands");
                int[] stands = Object.FindObjectsByType<TrophyStand>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(s => s.Index).OrderBy(i => i).ToArray();
                Check(stands.SequenceEqual(new[] { 0, 9 }), "Mudlands camp stands are the tutorial's and Clutch's: " + string.Join(",", stands));
                var dock = Object.FindAnyObjectByType<SignalLost.World.IslandDock>();
                Check(dock != null && dock.Departs && dock.DestinationScene == "DesertDunes" && dock.RequiredStage == "Mudlands", "the Mudlands dock sails on to the desert once the Mudlands is cleared");
                StageOrderLinker.LinkAll(); // the rebuilt scene already has the links: no changes expected
            }
            if (Step("wyvern"))
            {
                DesertDuelBossBuilder.BuildBriarwing();
                AssetDatabase.SaveAssets();
                Note("ok: Briarwing rebuilt");
            }
            if (Step("validate")) StageOrderLinker.ValidateAll();
            if (Step("cars")) CarPictures();
            if (Step("wyvern")) WyvernStrip();
        }
        catch (Exception exception)
        {
            failures++;
            Note("FAIL: " + exception);
        }
        File.WriteAllText(Path.Combine(Out, "report.txt"), Report.ToString());
        Debug.Log(failures == 0 ? "STAGE_ORDER_SMOKE_PASS" : "STAGE_ORDER_SMOKE_FAIL " + failures);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    // Every renderer and collider in the open scene except the camp's trophy stands (which this change moves), by path.
    private static Dictionary<string, Vector3> Scenery()
    {
        var found = new Dictionary<string, Vector3>();
        var groups = new Dictionary<string, List<Vector3>>();
        foreach (Component c in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID).Cast<Component>()
                     .Concat(Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID)))
        {
            string path = PathOf(c.transform) + "|" + c.GetType().Name;
            if (path.Contains("Trophy Stand") || path.Contains("Rack") || path.Contains("Mudlands Stand")) continue;
            if (!groups.TryGetValue(path, out List<Vector3> list)) groups[path] = list = new List<Vector3>();
            list.Add(c.transform.position);
        }
        // Objects sharing a path are matched in position order (instance IDs change between loads).
        foreach (var group in groups)
        {
            var sorted = group.Value.OrderBy(v => v.x).ThenBy(v => v.y).ThenBy(v => v.z).ToList();
            for (int i = 0; i < sorted.Count; i++) found[group.Key + "#" + i] = sorted[i];
        }
        return found;
    }

    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

    // ---------- Pictures ----------

    private static Camera Studio(out RenderTexture target, int w, int h)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.78f);
        RenderSettings.ambientEquatorColor = new Color(0.6f, 0.56f, 0.5f);
        RenderSettings.ambientGroundColor = new Color(0.35f, 0.3f, 0.25f);
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.3f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = Vector3.one * 8f;
        var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        groundMat.SetColor("_BaseColor", new Color(0.78f, 0.62f, 0.45f));
        ground.GetComponent<Renderer>().sharedMaterial = groundMat;
        var camera = new GameObject("Camera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.55f, 0.66f, 0.78f);
        camera.fieldOfView = 35f;
        target = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
        camera.targetTexture = target;
        return camera;
    }

    private static void Shoot(Camera camera, RenderTexture target, Texture2D sheet, int cell, int cols, Vector3 eye, Vector3 at)
    {
        camera.transform.position = eye;
        camera.transform.LookAt(at);
        camera.Render();
        RenderTexture.active = target;
        int w = target.width, h = target.height;
        int x = cell % cols * w, y = (sheet.height / h - 1 - cell / cols) * h;
        sheet.ReadPixels(new Rect(0, 0, w, h), x, y);
        RenderTexture.active = null;
    }

    // The car as DrunkCar builds it (body pivot 0.55 m up, the wheels on its wheel spots), each style, four views.
    private static void CarPictures()
    {
        Vector3[] spots = { new Vector3(-0.82f, 0.38f, 1.32f), new Vector3(0.82f, 0.38f, 1.32f), new Vector3(-0.82f, 0.38f, -1.28f), new Vector3(0.82f, 0.38f, -1.28f) };
        foreach (CarModel.Style style in Enum.GetValues(typeof(CarModel.Style)))
        {
            Camera camera = Studio(out RenderTexture target, 640, 420);
            var root = new GameObject("Car " + style).transform;
            var visual = MudArt.Pivot(root, "Visual", Vector3.zero);
            var body = MudArt.Pivot(visual, "Body", new Vector3(0f, 0.55f, 0f));
            CarModel.BuildBody(body, style);
            var seat = MudArt.Pivot(body, "Seat", new Vector3(-0.38f, 0.32f, -0.25f));
            for (int i = 0; i < 4; i++) CarModel.Wheel(visual, "Wheel " + i, spots[i], spots[i].x < 0f, style);
            // The colliders DrunkCar adds (unchanged), drawn as a check that the body fits them.
            var hull = new Bounds(new Vector3(0f, 0.85f, 0f), new Vector3(1.85f, 0.85f, 4.2f));
            var parts = root.GetComponentsInChildren<Renderer>();
            var all = parts[0].bounds;
            foreach (Renderer r in parts) all.Encapsulate(r.bounds);
            Note($"car {style}: {parts.Length} parts, {parts.Sum(r => r.GetComponent<MeshFilter>()?.sharedMesh?.triangles.Length / 3 ?? 0)} triangles, bounds {all.size.x:0.00} x {all.size.y:0.00} x {all.size.z:0.00} (hull collider 1.85 x 0.85 x 4.20, cabin top 1.73), seat at {seat.position}");
            Check(all.size.x < 2.3f && all.size.z < 4.8f && all.max.y < 2.2f, $"car {style} stays near its colliders");
            var sheet = new Texture2D(640 * 2, 420 * 2, TextureFormat.RGB24, false);
            Vector3 c = new Vector3(0f, 0.9f, 0f);
            Shoot(camera, target, sheet, 0, 2, new Vector3(-5.2f, 2.4f, 6.4f), c);
            Shoot(camera, target, sheet, 1, 2, new Vector3(-8.5f, 1.6f, 0.2f), c);
            Shoot(camera, target, sheet, 2, 2, new Vector3(5.4f, 3.0f, -6.2f), c);
            Shoot(camera, target, sheet, 3, 2, new Vector3(0.01f, 9.5f, 0.6f), new Vector3(0f, 0.6f, 0f));
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Out, "car_" + style + ".png"), sheet.EncodeToPNG());
        }
        Check(CarModel.StyleFor("Mudlands") == CarModel.Style.MuscleCar && CarModel.StyleFor("DesertDunes") == CarModel.Style.DuneMule
              && CarModel.StyleFor("CrystalCaves") == CarModel.Style.GeodeCrawler && CarModel.StyleFor("Wasteland") == CarModel.Style.ScrapRunner,
            "each stage scene picks its car style");
    }

    // Samples the imported clips on the built prefab's model (as Unity plays them) into frame strips.
    private static void WyvernStrip()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Desert/Bosses/Briarwing/Briarwing.prefab");
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Prefabs/Enemies/Desert/Bosses/Briarwing/Briarwing.controller");
        Check(prefab != null && controller != null, "Briarwing prefab and controller");
        string[] want = { "Flap", "Glide", "Hover", "Takeoff", "Land", "Hop", "Plunge" };
        var clips = controller.animationClips.ToDictionary(a => a.name, a => a);
        foreach (string name in want) Check(clips.ContainsKey(name), "Briarwing controller has " + name);
        foreach (var plan in new[] { ("Flap", 12, new Vector3(0f, 6f, 30f)), ("Flap", 12, new Vector3(30f, 5f, 0f)), ("Takeoff", 12, new Vector3(30f, 6f, 0f)),
                     ("Hover", 10, new Vector3(30f, 5f, 0f)), ("Land", 12, new Vector3(30f, 5f, 0f)), ("Plunge", 8, new Vector3(30f, 5f, 0f)) })
        {
            (string clipName, int frames, Vector3 eyeOffset) = plan;
            if (!clips.TryGetValue(clipName, out AnimationClip clip)) continue;
            Camera camera = Studio(out RenderTexture target, 420, 300);
            camera.fieldOfView = 40f;
            var boss = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            foreach (MonoBehaviour behaviour in boss.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
            Animator animator = boss.GetComponentInChildren<Animator>();
            int cols = 4, rows = (frames + cols - 1) / cols;
            var sheet = new Texture2D(420 * cols, 300 * rows, TextureFormat.RGB24, false);
            for (int f = 0; f < frames; f++)
            {
                clip.SampleAnimation(animator.gameObject, clip.length * f / frames);
                Shoot(camera, target, sheet, f, cols, new Vector3(0f, 3f, 0f) + eyeOffset, new Vector3(0f, 3.2f, 0f));
            }
            sheet.Apply();
            string file = $"wyvern_{clipName}_{(eyeOffset.z > eyeOffset.x ? "front" : "side")}.png";
            File.WriteAllBytes(Path.Combine(Out, file), sheet.EncodeToPNG());
            Note($"wyvern strip {file}: {frames} frames over {clip.length:0.00} s, loop {clip.isLooping}");
        }
    }
}
