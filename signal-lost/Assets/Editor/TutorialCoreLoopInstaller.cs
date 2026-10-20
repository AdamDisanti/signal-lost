using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies the tutorial core-loop changes (meeting notes 2026-10-20) to the saved Tutorial.unity.
/// Every step checks whether it has already run, so running it again changes nothing. It only adds or
/// rewires tutorial objects; it never regenerates terrain or the lunar layout.
/// Menu: Signal Lost > Tutorial > Install Core Loop.
/// </summary>
public static class TutorialCoreLoopInstaller
{
    private const string ScenePath = "Assets/Scenes/Tutorial.unity";
    private static readonly List<string> Log = new List<string>();

    [MenuItem("Signal Lost/Tutorial/Install Core Loop")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Exit Play Mode before installing the tutorial core loop.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Log.Clear();

        RemoveLegacyNpcWindows(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("TUTORIAL_CORE_LOOP_INSTALLED\n" + string.Join("\n", Log));
    }

    /// <summary>Batch-mode entry point: installs, then exits with 0 on success or 1 on an exception.</summary>
    public static void InstallBatch()
    {
        try
        {
            Install();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    // ---------- Package 1: dialogue UI ----------

    // The code-built DialogueUi and GameplayOverlay replace the old TutorialNpcWindows prefab.
    private static void RemoveLegacyNpcWindows(Scene scene)
    {
        // Once the prefab asset is gone the instance is named "Tutorial NPC Windows (Missing Prefab ...)".
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (!root.name.StartsWith("Tutorial NPC Windows", StringComparison.Ordinal)) continue;
            UnityEngine.Object.DestroyImmediate(root);
            Log.Add("Removed the legacy Tutorial NPC Windows canvas.");
        }
    }

    // ---------- Helpers ----------

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }
}
