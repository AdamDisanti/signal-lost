using System;
using System.IO;
using System.Reflection;
using SignalLost.Editor;
using SignalLost.NPC;
using SignalLost.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class TutorialNpcInteractionSmoke
{
    private const string Key = "SignalLost.NpcInteractionRepairSmoke";
    static TutorialNpcInteractionSmoke()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception) && !trace.Contains("UnityEditor.Search.SearchDatabase")) Fail(new Exception(message + "\n" + trace));
        };
    }
    public static void Start()
    {
        TutorialEnemiesSetup.RequireTemp();
        EditorSceneManager.OpenScene("Assets/Scenes/Tutorial Testing.unity");
        var player = UnityEngine.Object.FindFirstObjectByType<TutorialPlayer>();
        var quest = UnityEngine.Object.FindFirstObjectByType<TutorialQuestFlow>();
        var ui = UnityEngine.Object.FindFirstObjectByType<TutorialNpcUi>();
        var marker = new GameObject("Unsaved scene edit must survive repair"); marker.transform.position = new Vector3(1, 2, 3);
        Set(player, "npcUi", null); Set(quest, "ui", null); Set(quest, "cavendish", null);
        UnityEngine.Object.DestroyImmediate(ui.gameObject);
        TutorialNpcUiRepair.Repair();
        ui = UnityEngine.Object.FindFirstObjectByType<TutorialNpcUi>();
        Check(ui != null && Get<TutorialNpcUi>(quest, "ui") == ui && Get<TutorialNpcUi>(player, "npcUi") == ui, "Editor repair restores missing canvas and references.");
        Check(marker != null && marker.transform.position == new Vector3(1, 2, 3), "Unsaved scene edits survive repair.");
        TutorialNpcUiRepair.Repair();
        Check(UnityEngine.Object.FindObjectsByType<TutorialNpcUi>().Length == 1, "Repeated repair does not duplicate canvas.");
        // Reproduce missing serialized bindings with the canvas present: runtime must recover them too.
        Set(player, "npcUi", null); Set(quest, "ui", null); Set(quest, "cavendish", null); Set(ui, "player", null);
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + "Start", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 60) { Fail(new Exception("Interaction repair timed out.")); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < 0.5f) return;
        try
        {
            var player = UnityEngine.Object.FindFirstObjectByType<TutorialPlayer>();
            var quest = UnityEngine.Object.FindFirstObjectByType<TutorialQuestFlow>();
            var ui = UnityEngine.Object.FindFirstObjectByType<TutorialNpcUi>();
            Check(Get<TutorialNpcUi>(quest, "ui") == ui && Get<TutorialNpcUi>(player, "npcUi") == ui && Get<NpcDialogue>(quest, "cavendish") != null, "Runtime restores missing quest/player/Cavendish bindings.");
            var cav = GameObject.Find("CapybaraAstronaut");
            var collider = cav.GetComponent<Collider>();
            player.GetComponent<FirstPersonController>().enabled = false;
            var body = player.GetComponent<CharacterController>(); var camera = player.GetComponentInChildren<Camera>();
            var terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
            bool aimed = false;
            for (int i = 0; i < 32; i++)
            {
                float a = i * Mathf.PI / 16;
                Vector3 p = collider.bounds.center + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 2.1f;
                p.y = terrain.SampleHeight(p) + terrain.transform.position.y + 0.06f;
                body.enabled = false; player.transform.position = p; body.enabled = true; Physics.SyncTransforms();
                camera.transform.LookAt(collider.bounds.center);
                if (Physics.Raycast(camera.transform.position, camera.transform.forward, out var hit, 3) && hit.collider.GetComponentInParent<NpcActor>() == cav.GetComponent<NpcActor>()) { aimed = true; break; }
            }
            Check(aimed && player.TryInteract() && ui.ConversationActive, "Actual player interaction ray opens Cavendish quest popup after recovery.");
            Check(Cursor.lockState == CursorLockMode.None && Cursor.visible, "Popup releases and shows cursor.");
            var controller = player.GetComponent<FirstPersonController>();
            Check((bool)controller.GetType().GetField("menuOpen", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller), "Controller blocks click recapture during popup.");
            string first = ui.CurrentLine;
            Check(player.TryInteract() && ui.CurrentLine != first, "Next E interaction advances the recovered UI.");
            player.TryInteract(); player.TryInteract();
            Check(quest.Accepted && !ui.HasOpenWindow, "Repaired interaction completes quest acceptance.");
            Check(Cursor.lockState == (Application.isFocused ? CursorLockMode.Locked : CursorLockMode.None), "Closing popup restores capture when focused.");
            ui.OpenShop(cav.transform);
            Check(Cursor.lockState == CursorLockMode.None && Cursor.visible, "Shop releases cursor.");
            ui.CloseAll();
            Check(!(bool)controller.GetType().GetField("menuOpen", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller), "Closing shop releases menu input ownership.");
            SessionState.SetBool(Key, false);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/TutorialNpcInteractionSmoke.txt", "PASS: editor repairs stale scene without losing unsaved edits; runtime restores missing references; player ray opens and advances Cavendish quest through acceptance.");
            Debug.Log("TUTORIAL_NPC_INTERACTION_REPAIR_PASS"); EditorApplication.Exit(0);
        }
        catch (Exception error) { Fail(error); }
    }
    private static void Set(UnityEngine.Object target, string name, UnityEngine.Object value)
    { var data = new SerializedObject(target); data.FindProperty(name).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Debug.Log(message); }
    private static void Fail(Exception error) { SessionState.SetBool(Key, false); Debug.LogError(error); EditorApplication.Exit(1); }
}
