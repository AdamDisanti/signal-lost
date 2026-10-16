using System;
using System.IO;
using SignalLost.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Scratch-project-only smoke test: not shipped in the user's Assets.
[InitializeOnLoad]
public static class PropPlaySmoke
{
    const string Key = "SignalLost.PropPlaySmoke";
    static PropPlaySmoke() { EditorApplication.update += Tick; }
    public static void Start()
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Application.isBatchMode || !project.StartsWith(temp, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Run this test only in a disposable batch-mode project under the system temp directory.");
        GrayboxPropBuilder.CreateMissing();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        foreach (string name in new[] { "CapybaraAstronaut", "BurntRockBasin", "AlcoholFlask", "GrayboxExitGate" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GrayboxPropBuilder.Root + "/" + name + ".prefab");
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            obj.name = name;
            obj.transform.position = name == "GrayboxExitGate" ? Vector3.zero :
                name == "BurntRockBasin" ? new Vector3(5, 0, 0) :
                name == "CapybaraAstronaut" ? new Vector3(-5, 0, 0) : new Vector3(8, 0, 0);
        }
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "SmokeFloor";
        floor.transform.position = new Vector3(0, -0.25f, 0);
        floor.transform.localScale = new Vector3(30, 0.5f, 12);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/PropSmoke.unity");
        SessionState.SetInt(Key, 1);
        SessionState.SetFloat(Key + "Start", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (SessionState.GetInt(Key, 0) != 1) return;
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 90)
        { Fail(new Exception("Play Mode entry timed out.")); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetInt(Key, 2);
        try
        {
            Physics.SyncTransforms();
            var player = new GameObject("SmokePlayer");
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0, 0.9f, 0);
            cc.skinWidth = 0.035f;
            cc.stepOffset = 0.3f;
            cc.enabled = false;
            player.transform.position = new Vector3(0, 0.05f, -2);
            cc.enabled = true;
            for (int i = 0; i < 100; i++) cc.Move(new Vector3(0, -0.02f, 0.05f));
            if (player.transform.position.z < 2.8f) throw new Exception("Controller blocked in gate opening.");
            cc.enabled = false;
            player.transform.position = new Vector3(1.7f, 0.05f, -2);
            cc.enabled = true;
            for (int i = 0; i < 100; i++) cc.Move(new Vector3(0, -0.02f, 0.05f));
            if (player.transform.position.z > -0.60f) throw new Exception("Controller passed through gate post.");
            cc.enabled = false;
            player.transform.position = new Vector3(-5, 0.05f, -2);
            cc.enabled = true;
            for (int i = 0; i < 100; i++) cc.Move(new Vector3(0, -0.02f, 0.05f));
            if (player.transform.position.z > -0.60f) throw new Exception("Controller passed through capybara.");
            if (!Physics.Raycast(new Vector3(5, 2, 0), Vector3.down, out RaycastHit hit, 3) ||
                Mathf.Abs(hit.point.y - 0.64f) > 0.025f)
                throw new Exception("Runtime raycast failed to reach basin floor.");
            if (!Physics.Raycast(new Vector3(8, 0.16f, -1), Vector3.forward, out hit, 2) ||
                !hit.collider.transform.IsChildOf(GameObject.Find("AlcoholFlask").transform))
                throw new Exception("Runtime flask collider missing.");
            string result = "PLAY_MODE_PASS: 1.8m controller traverses gate, stops at post and capybara; " +
                "basin floor raycast hits 0.64m; flask collider hit. Four prefabs instantiated successfully.";
            File.WriteAllText("Logs/GrayboxPropsPlayMode.txt", result);
            Debug.Log(result);
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Fail(error); }
    }
    static void Fail(Exception error)
    {
        SessionState.SetInt(Key, 3);
        File.WriteAllText("Logs/GrayboxPropsPlayMode.txt", "FAIL: " + error);
        Debug.LogException(error);
        EditorApplication.Exit(1);
    }
}
