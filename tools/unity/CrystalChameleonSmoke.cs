// Install only in a disposable project's Assets/Editor; never in the real project.
// Batch: -executeMethod CrystalChameleonSmoke.Start (no -quit; it exits itself).
using System;
using System.IO;
using SignalLost.Combat;
using SignalLost.Editor;
using SignalLost.Enemies;
using SignalLost.Enemies.Caves;
using SignalLost.Inventory;
using SignalLost.Player;
using SignalLost.Scanner;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CrystalChameleonSmoke
{
    private const string Key = "SignalLost.CrystalChameleonSmoke";
    private static float started = -1f;
    private static int stage, playerHits;
    private static CrystalChameleon hidden;
    private static string error;

    static CrystalChameleonSmoke()
    {
        EditorApplication.update += Update;
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return; // import-time package errors are not ours
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (message.StartsWith("ArgumentOutOfRangeException: Index was out of range.") && trace.Contains("UnityEditor.Search.SearchDatabase")) return;
            error = message + "\n" + trace;
        };
    }

    private static void Guard()
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
        string temporary = Path.GetFullPath(Path.GetTempPath()).Replace('\\', '/').TrimEnd('/') + "/";
        if (!Application.isBatchMode || !project.StartsWith(temporary, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use only a disposable batch-mode project under the system temporary directory.");
    }

    public static void Start()
    {
        Guard();
        CrystalChameleonBuilder.Validate();
        EditorSceneManager.OpenScene(CrystalChameleonBuilder.ScenePath);
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + "Deadline", (float)EditorApplication.timeSinceStartup + 240);
        EditorApplication.EnterPlaymode();
    }

    private static void Update()
    {
        if (!SessionState.GetBool(Key, false)) return;
        try
        {
            Require(error == null, error);
            Require(EditorApplication.timeSinceStartup < SessionState.GetFloat(Key + "Deadline", 0), "Play Mode smoke timed out at stage " + stage);
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (started < 0f) started = Time.time;
            float t = Time.time - started;
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            Require(player != null, "No player.");

            if (stage == 0 && t > 2.5f)
            {
                CrystalChameleon[] all = Object.FindObjectsByType<CrystalChameleon>(FindObjectsSortMode.None);
                Require(all.Length == 3, "Expected three chameleons, found " + all.Length);
                foreach (CrystalChameleon item in all)
                {
                    Require(!item.IsCamouflaged, item.name + " should start revealed in the test scene.");
                    Require(item.GetComponentInChildren<MeshRenderer>() != null, item.name + " has no renderers.");
                }
                // A camouflaged one, away from the player, to test the disguise and the Disrupt() hook.
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrystalChameleonBuilder.PrefabPath);
                hidden = Object.Instantiate(prefab, new Vector3(-12f, 0f, -12f), Quaternion.identity).GetComponent<CrystalChameleon>();
                // Stand in front of the middle chameleon so the tongues can reach.
                var body = player.GetComponent<CharacterController>();
                body.enabled = false;
                player.transform.SetPositionAndRotation(new Vector3(0f, 0.05f, 1f), Quaternion.identity);
                body.enabled = true;
                player.GetComponent<Health>().Damaged += (_, amount, source) => { if (source != null && source.GetComponent<CrystalChameleon>() != null) playerHits++; };
                stage = 1;
            }
            else if (stage == 1 && t > 4f)
            {
                Require(hidden.IsCamouflaged, "A new chameleon should start camouflaged.");
                Require(!hidden.GetComponent<CapsuleCollider>().enabled, "A camouflaged chameleon must not be hittable.");
                Require(!hidden.GetComponent<EnemySignalSource>().enabled, "A camouflaged chameleon must be hidden from the scanner.");
                Require(hidden.GetComponent<BurrowEmerge>().IsBuried, "A camouflaged chameleon should be buried.");
                Require(CrystalName(hidden) == hidden.CrystalMaterial.name, "Its crystals should wear the cave crystal material.");
                hidden.Disrupt();
                Require(CrystalName(hidden).Contains("Red"), "Disrupt() should turn its crystals red, got " + CrystalName(hidden));
                stage = 2;
            }
            else if (stage == 2 && t > 6.5f)
            {
                Require(!hidden.IsCamouflaged && !hidden.GetComponent<BurrowEmerge>().IsBuried, "It should have emerged after Disrupt().");
                Require(hidden.GetComponent<CapsuleCollider>().enabled && hidden.GetComponent<EnemySignalSource>().enabled, "Emerged: collider and scanner signal on.");
                stage = 3;
            }
            else if (stage == 3 && playerHits > 0)
            {
                CrystalChameleon victim = hidden;
                int before = Object.FindObjectsByType<ScrapPickup>(FindObjectsSortMode.None).Length;
                victim.GetComponent<Health>().TakeDamage(9999f, player.gameObject);
                int after = Object.FindObjectsByType<ScrapPickup>(FindObjectsSortMode.None).Length;
                Require(after > before, "A dead chameleon should drop scrap.");
                Require(CrystalName(victim).Contains("Spent"), "Its crystals should switch off when it dies, got " + CrystalName(victim));
                Report("CRYSTAL_CHAMELEON_PLAY_MODE_PASS: revealed x3, camouflage hidden/unhittable, Disrupt -> red + emerge, tongue hits=" + playerHits + ", scrap " + (after - before) + ", crystals off on death");
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(0);
            }
            else if (stage == 3 && t > 30f) throw new InvalidOperationException("No chameleon tongue hit the player within 24 s.");
        }
        catch (Exception exception)
        {
            SessionState.SetBool(Key, false);
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static string CrystalName(CrystalChameleon item)
    {
        var data = new SerializedObject(item);
        var first = (Renderer)data.FindProperty("crystals").GetArrayElementAtIndex(0).objectReferenceValue;
        return first.sharedMaterial.name;
    }

    private static void Report(string line)
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/CrystalChameleonPlayMode.txt", line);
        Debug.Log(line);
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
