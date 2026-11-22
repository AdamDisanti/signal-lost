// Copy-only smoke (never in the real project). Batch: -executeMethod MudStorySmoke.Start (no -quit; it exits itself).
// Plays Mudlands.unity and walks the whole quest chain with MudlandsStory.CompleteCurrentForTests (jobs done, bosses
// skipped, every report given) to "Done", with no errors; checks the Blender buildings are shown in Play mode and the
// hex lanterns come out on their roofs after the Lights Out report.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using SignalLost.Mudlands;
using SignalLost.Progression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class MudStorySmoke
{
    private const string Key = "SignalLost.MudStorySmoke";
    internal static bool done;
    internal static string report = "";
    private static string error;
    private static MudStorySmokeRunner runner;
    private static string Backup => CampSave.FilePath + ".smoke-backup";

    static MudStorySmoke()
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
        EditorSceneManager.OpenScene("Assets/Scenes/Large Areas/Mudlands.unity");
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + "Deadline", (float)EditorApplication.timeSinceStartup + 600);
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
            if (runner == null) runner = new GameObject("Mud Story Smoke").AddComponent<MudStorySmokeRunner>();
            if (!done) return;
            Debug.Log("MUD_STORY_PLAY_MODE_PASS:" + report);
            SessionState.SetBool(Key, false);
            RestoreSave();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            SessionState.SetBool(Key, false);
            RestoreSave();
            Debug.LogException(exception);
            Debug.Log("MUD_STORY_PLAY_MODE_FAIL after:" + report);
            EditorApplication.Exit(1);
        }
    }

    internal static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}

public sealed class MudStorySmokeRunner : MonoBehaviour
{
    private static void Note(string line) => MudStorySmoke.report += " " + line + ";";

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(2f);
        var story = FindAnyObjectByType<MudlandsStory>();
        MudStorySmoke.Require(story != null, "no MudlandsStory");
        int shown = 0;
        foreach (MudProp prop in FindObjectsByType<MudProp>(FindObjectsSortMode.None))
        {
            var mesh = prop.transform.Find("Model/Mesh")?.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh != null && mesh.name.StartsWith("Mud") && mesh.name.Length > 3 && !mesh.name.StartsWith("Mud ")) shown++;
        }
        Note("Blender buildings shown in Play mode: " + shown);
        MudStorySmoke.Require(shown >= 20, "too few Blender buildings in Play mode: " + shown);
        for (int i = 0; i < 80 && story.Current != MudlandsStory.Step.Done; i++)
        {
            MudlandsStory.Step before = story.Current;
            string did = story.CompleteCurrentForTests();
            yield return new WaitForSeconds(0.6f);
            Note($"{before}: {did}");
            if (before == MudlandsStory.Step.ReportLanterns)
            {
                yield return new WaitForSeconds(2.5f); // the story flares them up one by one over about 1.5 s
                var hex = FindObjectsByType<CursedLantern>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(l => l.name.StartsWith("Hex Lantern")).ToList();
                MudStorySmoke.Require(hex.Count == 3 && hex.All(l => l.gameObject.activeInHierarchy), "hex lanterns not out after the Lights Out report");
                Note("hex lanterns out: " + string.Join(", ", hex.Select(l => $"{l.name} y {l.transform.position.y:0.00}")));
            }
        }
        MudStorySmoke.Require(story.Current == MudlandsStory.Step.Done, "story stuck at " + story.Current);
        Note("story Done");
        MudStorySmoke.done = true;
    }
}
