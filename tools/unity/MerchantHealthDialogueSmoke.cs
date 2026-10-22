using System;
using System.Collections;
using System.Reflection;
using SignalLost.Combat;
using SignalLost.Enemies;
using SignalLost.NPC;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class MerchantHealthDialogueSmoke
{
    const string Key = "SignalLost.MerchantHealthDialogueSmoke";
    static IEnumerator steps;
    static MerchantHealthDialogueSmoke() { EditorApplication.update += Tick; }
    public static void Start()
    {
        TutorialEnemiesSetup.RequireTemp();
        EditorSceneManager.OpenScene("Assets/Scenes/Tutorial Testing.unity");
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + "Start", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        try
        {
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0) > 60) throw new Exception("Timed out");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .5f) return;
            if (steps == null) steps = Run();
            if (steps.MoveNext()) return;
            SessionState.SetBool(Key, false);
            Debug.Log("MERCHANT_HEALTH_DIALOGUE_PASS"); EditorApplication.Exit(0);
        }
        catch (Exception e) { SessionState.SetBool(Key, false); Debug.LogError(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Run()
    {
        var crab = UnityEngine.Object.FindFirstObjectByType<MerchantCrab>();
        var hp = crab.GetComponent<Health>();
        var speech = crab.GetComponentInChildren<NpcDialogue>();
        var brain = crab.GetComponent<HermitCrab>();
        typeof(NpcDialogue).GetField("secondsPerLine", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(speech, .1f);
        Check(hp.Max == 500, "Authored health remains 500");
        var laser = UnityEngine.Object.FindFirstObjectByType<MiningLaser>(FindObjectsInactive.Include);
        var aim = new GameObject("Test aim").transform;
        var bounds = crab.GetComponent<Collider>().bounds;
        aim.position = bounds.center + Vector3.up * (bounds.extents.y + 3);
        aim.rotation = Quaternion.LookRotation(Vector3.down);
        typeof(MiningLaser).GetField("aimOrigin", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(laser, aim);
        var updateTarget = typeof(MiningLaser).GetMethod("UpdateTarget", BindingFlags.Instance | BindingFlags.NonPublic);
        Physics.SyncTransforms(); updateTarget.Invoke(laser, null);
        Check(laser.CurrentTarget == hp, "Hostile merchant still exposes health target");
        crab.BeginQuest(); brain.enabled = false; speech.Speak();
        hp.TakeDamage(124);
        Check(Stage(crab) == 0, "No combat section above 375 HP");
        hp.TakeDamage(1);
        Check(Stage(crab) == 1 && speech.CurrentLine.StartsWith("Finders keepers"), "375 HP triggers section one");
        hp.TakeDamage(125);
        Check(Stage(crab) == 2 && speech.CurrentLine.StartsWith("Finders keepers"), "250 HP queues section two without interrupting one");
        hp.TakeDamage(125);
        Check(Stage(crab) == 3, "125 HP queues section three");
        while (!speech.CurrentLine.StartsWith("All right, you've")) yield return null;
        while (!speech.CurrentLine.StartsWith("Easy!")) yield return null;
        while (speech.IsSpeaking) yield return null;
        hp.TakeDamage(1);
        Check(!speech.IsSpeaking && Stage(crab) == 3, "Further damage never repeats completed sections");
        crab.ResetAfterPlayerRespawn(); brain.enabled = false;
        Check(hp.Current == 500 && Stage(crab) == 0, "Respawn resets health and dialogue stages");
        hp.TakeDamage(400);
        Check(Stage(crab) == 3 && speech.CurrentLine.StartsWith("Finders keepers"), "Single large hit queues all crossed thresholds");
        hp.TakeDamage(100);
        Check(crab.IsDefeated && speech.CurrentLine == "All right! Claws down!", "Lethal damage immediately replaces queued lines with surrender");
        float until = Time.time + 1.5f;
        while (Time.time < until) yield return null;
        Check(!speech.IsSpeaking, "Queued combat sections stay cancelled after surrender");
        Check(!hp.IsDead, "Merchant has recovered living health");
        bounds = crab.GetComponent<Collider>().bounds;
        aim.position = bounds.center + Vector3.up * (bounds.extents.y + 3);
        Physics.SyncTransforms();
        updateTarget.Invoke(laser, null);
        Check(laser.CurrentTarget == null, "Friendly merchant no longer exposes health target");
    }
    static int Stage(MerchantCrab crab) => (int)typeof(MerchantCrab).GetField("nextHealthDialogue", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(crab);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Debug.Log(message); }
}
