// Copy-only smoke (never in the real project; put it in Assets/Editor/Smoke of a disposable copy under %TEMP%).
// Batch: -executeMethod StageChainSmoke.Start (no -quit; it exits itself). Output folder: env STAGE_SMOKE_OUT.
// Plays the stage chain as the islands are sailed since 11-22: Crash Meadow -> the Mudlands -> the Desert Dunes -> the
// Crystal Caves (each by its own dock's boat and the real voyage), then the stage-skip tool on to the Wasteland. On
// each island: the boat it lands from, the camp's trophy stands, the car's style, and (from the desert on) that the
// Learner's Permit earned in the Mudlands still unlocks Drunk Driving and the car spawns with its usual colliders and seat.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using SignalLost.Mudlands;
using SignalLost.Progression;
using SignalLost.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class StageChainSmoke
{
    private const string Key = "SignalLost.StageChainSmoke";
    internal static bool done;
    internal static string report = "";
    private static string error;
    private static StageChainRunner runner;
    private static string Backup => CampSave.FilePath + ".smoke-backup";
    internal static string Out => Environment.GetEnvironmentVariable("STAGE_SMOKE_OUT") ?? Path.Combine(Path.GetTempPath(), "StageOrderSmoke");

    static StageChainSmoke()
    {
        EditorApplication.update += Update;
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (trace != null && trace.Contains("UnityEditor.Search")) return;
            error = message + "\n" + trace;
        };
    }

    private static void RestoreSave()
    {
        if (SessionState.GetBool(Key + "HadSave", false) && File.Exists(Backup)) File.Copy(Backup, CampSave.FilePath, true);
        else File.Delete(CampSave.FilePath);
        File.Delete(Backup);
    }

    public static void Start()
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
        string temporary = Path.GetFullPath(Path.GetTempPath()).Replace('\\', '/').TrimEnd('/') + "/";
        if (!Application.isBatchMode || !project.StartsWith(temporary, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use only a disposable batch-mode project under the system temporary directory.");
        if (File.Exists(CampSave.FilePath)) File.Copy(CampSave.FilePath, Backup, true);
        else File.Delete(Backup);
        SessionState.SetBool(Key + "HadSave", File.Exists(CampSave.FilePath));
        File.Delete(CampSave.FilePath);
        EditorSceneManager.OpenScene("Assets/Scenes/Tutorial/Tutorial Demo.unity");
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + "Deadline", (float)EditorApplication.timeSinceStartup + 1800);
        EditorApplication.EnterPlaymode();
    }

    private static void Update()
    {
        if (!SessionState.GetBool(Key, false)) return;
        try
        {
            Require(error == null, error);
            Require(EditorApplication.timeSinceStartup < SessionState.GetFloat(Key + "Deadline", 0), "timed out. So far: " + report);
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (runner == null)
            {
                var go = new GameObject("Stage Chain Smoke");
                Object.DontDestroyOnLoad(go);
                runner = go.AddComponent<StageChainRunner>();
            }
            if (!done) return;
            Finish(0, "STAGE_CHAIN_PLAY_MODE_PASS:");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish(1, "STAGE_CHAIN_PLAY_MODE_FAIL after:");
        }
    }

    private static void Finish(int code, string label)
    {
        SessionState.SetBool(Key, false);
        RestoreSave();
        Directory.CreateDirectory(Out);
        File.WriteAllText(Path.Combine(Out, "chain.txt"), label + report.Replace(";", ";\n"));
        Debug.Log(label + report);
        EditorApplication.Exit(code);
    }

    internal static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}

public sealed class StageChainRunner : MonoBehaviour
{
    private static void Note(string line) => StageChainSmoke.report += " " + line + ";";

    private IEnumerator Start()
    {
        IslandDock.IgnoreRequirements = true; // sail without playing each island through (the gates are checked below)
        yield return new WaitForSeconds(2f);
        yield return Island("Tutorial Demo", null, new int[0], false);
        yield return Sail("Mudlands");
        yield return Island("Mudlands", "DesertDunes", new[] { 0, 9 }, false);
        // As if the Learner's Permit had been earned in the Mudlands (the kiosk gives it; the story needs it for its first job).
        MudlandsProgress.Set(MudlandsProgress.PermitFlag);
        StageProgress.ClearStage(StageProgress.Mudlands, StageProgress.Desert);
        yield return Sail("DesertDunes");
        yield return Island("DesertDunes", "CrystalCaves", new[] { 0, 1, 2, 3, 4, 9 }, true);
        yield return Sail("CrystalCaves");
        yield return Island("CrystalCaves", null, Enumerable.Range(0, 10).ToArray(), true);
        // The Fog Forest isn't built, so the caves' boat has nowhere to go yet: the finale is reached by the stage skip.
        StageSkip.Go(1);
        float until = Time.realtimeSinceStartup + 60f;
        while (SceneManager.GetActiveScene().name != "Wasteland" && Time.realtimeSinceStartup < until) yield return null;
        StageChainSmoke.Require(SceneManager.GetActiveScene().name == "Wasteland", "stage skip from the caves didn't reach the Wasteland");
        yield return new WaitForSeconds(3f);
        yield return Car(CarModel.Style.ScrapRunner);
        StageChainSmoke.done = true;
    }

    // Takes the boat from the current island's departure dock and waits to be standing on the next island.
    private IEnumerator Sail(string to)
    {
        IslandDock from = Object.FindObjectsByType<IslandDock>(FindObjectsSortMode.None).FirstOrDefault(d => d.Departs);
        StageChainSmoke.Require(from != null, "no departing boat on " + SceneManager.GetActiveScene().name);
        StageChainSmoke.Require(from.DestinationScene == to, $"{SceneManager.GetActiveScene().name}'s boat sails to '{from.DestinationScene}', expected {to}");
        StageChainSmoke.Require(IslandVoyage.Begin(from), "the voyage didn't start from " + SceneManager.GetActiveScene().name);
        float started = Time.realtimeSinceStartup;
        float loaded = -1f;
        while ((SceneManager.GetActiveScene().name != to || IslandVoyage.InProgress) && Time.realtimeSinceStartup - started < 400f)
        {
            if (loaded < 0f && SceneManager.GetActiveScene().name == to) loaded = Time.realtimeSinceStartup - started;
            yield return null;
        }
        Note($"{to}: scene loaded after {loaded:0.0} s, {Time.frameCount} frames so far");
        StageChainSmoke.Require(SceneManager.GetActiveScene().name == to && !IslandVoyage.InProgress, "the voyage to " + to + " didn't finish");
        Note($"sailed to {to} in {Time.realtimeSinceStartup - started:0.0} s");
        yield return new WaitForSeconds(2f);
    }

    private IEnumerator Island(string scene, string boatTo, int[] stands, bool car)
    {
        StageChainSmoke.Require(SceneManager.GetActiveScene().name == scene, "expected " + scene);
        IslandDock arrival = IslandDock.Find(IslandDock.Role.Arrival);
        if (scene != "Tutorial Demo")
        {
            StageChainSmoke.Require(arrival != null, scene + " has no arrival dock");
            var player = Object.FindAnyObjectByType<SignalLost.Player.PlayerFlask>();
            float off = player != null ? Vector3.Distance(player.transform.position, arrival.Landing.position) : -1f;
            Note($"{scene}: landed {off:0.0} m from its dock's landing");
        }
        IslandDock departs = Object.FindObjectsByType<IslandDock>(FindObjectsSortMode.None).FirstOrDefault(d => d.Departs);
        if (boatTo != null)
        {
            StageChainSmoke.Require(departs != null && departs.DestinationScene == boatTo, $"{scene}'s boat should sail to {boatTo}");
            Note($"{scene}: boat to {departs.DestinationName}, waits for '{departs.RequiredStage}'");
        }
        int[] found = Object.FindObjectsByType<TrophyStand>(FindObjectsSortMode.None).Select(s => s.Index).OrderBy(i => i).ToArray();
        if (stands.Length > 0)
            StageChainSmoke.Require(found.SequenceEqual(stands), $"{scene}'s camp stands are {string.Join(",", found)}, expected {string.Join(",", stands)}");
        Note($"{scene}: stands {string.Join(",", found)}");
        if (car)
        {
            CarModel.Style want = scene == "DesertDunes" ? CarModel.Style.DuneMule : CarModel.Style.GeodeCrawler;
            yield return Car(want);
        }
        yield return null;
    }

    // The permit carries over: Drunk Driving is unlocked, and the car it would call is this island's style.
    private IEnumerator Car(CarModel.Style want)
    {
        string scene = SceneManager.GetActiveScene().name;
        yield return new WaitForSeconds(0.5f);
        DrunkDriving driving = DrunkDriving.Instance;
        StageChainSmoke.Require(driving != null, scene + ": the player has no Drunk Driving");
        StageChainSmoke.Require(driving.Unlocked, scene + ": Drunk Driving is locked although the permit was earned in the Mudlands");
        StageChainSmoke.Require(CarModel.Current == want, $"{scene}: car style {CarModel.Current}, expected {want}");
        Transform player = driving.transform;
        DrunkCar car = DrunkCar.Create(driving, player.position + player.forward * 6f + Vector3.up * 0.5f, player.rotation);
        yield return new WaitForSeconds(0.3f);
        BoxCollider[] boxes = car.GetComponents<BoxCollider>();
        StageChainSmoke.Require(boxes.Length == 2 && boxes[0].size == new Vector3(1.85f, 0.85f, 4.2f) && boxes[1].size == new Vector3(1.6f, 0.55f, 1.9f),
            scene + ": the car's colliders changed");
        StageChainSmoke.Require((car.Seat.localPosition - new Vector3(-0.38f, 0.32f, -0.25f)).sqrMagnitude < 1e-6f, scene + ": the seat moved");
        Note($"{scene}: car {want} spawned, {car.GetComponentsInChildren<MeshRenderer>().Length} parts, permit carried over");
        // A picture of it in place.
        var camera = new GameObject("Smoke Camera").AddComponent<Camera>();
        var target = new RenderTexture(800, 500, 24) { antiAliasing = 4 };
        camera.targetTexture = target;
        camera.fieldOfView = 40f;
        camera.transform.position = car.transform.position + car.transform.right * -5.5f + car.transform.forward * 5.5f + Vector3.up * 2.6f;
        camera.transform.LookAt(car.transform.position + Vector3.up * 0.9f);
        camera.Render();
        RenderTexture.active = target;
        var shot = new Texture2D(800, 500, TextureFormat.RGB24, false);
        shot.ReadPixels(new Rect(0, 0, 800, 500), 0, 0);
        shot.Apply();
        RenderTexture.active = null;
        Directory.CreateDirectory(StageChainSmoke.Out);
        File.WriteAllBytes(Path.Combine(StageChainSmoke.Out, $"ingame_{scene}_{want}.png"), shot.EncodeToPNG());
        Destroy(camera.gameObject);
        car.End(DrunkCar.EndReason.Parked, null);
        yield return new WaitForSeconds(0.5f);
    }
}
