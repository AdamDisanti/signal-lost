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
    private static CrystalChameleon hidden, wall;
    private static ScannerController scanner;
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
                Require(all.Length == 5, "Expected five chameleons, found " + all.Length);
                foreach (CrystalChameleon item in all)
                {
                    Require(item.IsCamouflaged, item.name + " should start hidden in the test scene.");
                    Require(item.GetComponentInChildren<MeshRenderer>() != null, item.name + " has no renderers.");
                    if (new SerializedObject(item).FindProperty("hideInWall").boolValue) wall = item;
                }
                Require(wall != null, "No wall chameleon.");
                scanner = player.GetComponentInChildren<ScannerController>(true);
                Require(scanner != null && scanner.DisruptsCamouflage, "The test scene player should start with the Crystal Disruptor.");
                // Hidden in the wall: the root waits on the floor below, the body clings to the wall up at its spot.
                Transform body = wall.transform.Find("Visual");
                Require(wall.transform.position.y < 0.3f, "A wall chameleon's root should wait on the floor, at y " + wall.transform.position.y);
                Require(Vector3.Distance(body.position, wall.DisguisePosition) < 1.2f && Vector3.Dot(body.up, wall.transform.forward) > 0.9f,
                    "A hidden wall chameleon's body should cling to the wall, facing out.");
                player.GetComponentInChildren<ScannerToggle>(true).SetRaised(true);
                Teleport(player, wall.transform.position + wall.transform.forward * 2f + Vector3.up * 0.05f);
                stage = 1;
            }
            else if (stage == 1 && t > 3.5f)
            {
                Require(!wall.IsCamouflaged, "The raised, upgraded scanner in front of a wall chameleon should reveal it.");
                stage = 2;
            }
            else if (stage == 2 && t > 6f)
            {
                Transform body = wall.transform.Find("Visual");
                Require(Vector3.Distance(body.position, wall.transform.position) < 0.6f && Vector3.Dot(body.up, Vector3.up) > 0.9f,
                    "A revealed wall chameleon should have dropped upright onto the floor.");
                Require(wall.GetComponent<UnityEngine.AI.NavMeshAgent>().isOnNavMesh, "A landed wall chameleon should be on the NavMesh.");
                // The rest of the checks need one chameleon alone: the others would outrank it on the scanner.
                foreach (CrystalChameleon item in Object.FindObjectsByType<CrystalChameleon>(FindObjectsSortMode.None))
                    item.gameObject.SetActive(false);
                // A fresh camouflaged floor one, away from the player, to test the disguise and the scanner upgrade.
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrystalChameleonBuilder.PrefabPath);
                hidden = Object.Instantiate(prefab, new Vector3(-12f, 0f, -12f), Quaternion.identity).GetComponent<CrystalChameleon>();
                player.GetComponent<Health>().Damaged += (_, amount, source) => { if (source != null && source.GetComponent<CrystalChameleon>() != null) playerHits++; };
                scanner.DisruptsCamouflage = false; // as if the upgrade weren't owned
                Teleport(player, hidden.transform.position + new Vector3(0f, 0.05f, 8f)); // in scanner range, outside the reveal radius
                stage = 3;
            }
            else if (stage == 3 && t > 7.5f)
            {
                Require(hidden.IsCamouflaged, "A new chameleon should start camouflaged.");
                Require(!hidden.GetComponent<CapsuleCollider>().enabled, "A camouflaged chameleon must not be hittable.");
                Require(hidden.GetComponent<BurrowEmerge>().IsBuried, "A camouflaged chameleon should be buried.");
                Require(CrystalName(hidden) == hidden.CrystalMaterial.name, "Its crystals should wear the cave crystal material.");
                Require(scanner.CurrentTarget != hidden.GetComponent<EnemySignalSource>(), "Without the Crystal Disruptor the scanner must ignore it.");
                scanner.DisruptsCamouflage = true;
                Require(UpgradeCatalog.Find("crystal_disruptor").DisruptsCamouflage, "crystal_disruptor should disrupt camouflage.");
                stage = 4;
            }
            else if (stage == 4 && t > 8.5f)
            {
                Require(hidden.IsCamouflaged, "Outside its reveal radius it should stay hidden.");
                Require(scanner.CurrentTarget == hidden.GetComponent<EnemySignalSource>(), "With the Crystal Disruptor the scanner should beep at it.");
                Teleport(player, hidden.transform.position + new Vector3(0f, 0.05f, 2f));
                stage = 5;
            }
            else if (stage == 5 && t > 9.5f)
            {
                Require(!hidden.IsCamouflaged, "The raised, upgraded scanner up close should disrupt it.");
                Require(CrystalName(hidden).Contains("Red"), "Disrupt() should turn its crystals red, got " + CrystalName(hidden));
                stage = 6;
            }
            else if (stage == 6 && t > 12f)
            {
                Require(!hidden.GetComponent<BurrowEmerge>().IsBuried, "It should have emerged after Disrupt().");
                Require(hidden.GetComponent<CapsuleCollider>().enabled, "Emerged: collider on.");
                stage = 7;
            }
            else if (stage == 7 && playerHits > 0)
            {
                CrystalChameleon victim = hidden;
                int before = Object.FindObjectsByType<ScrapPickup>(FindObjectsSortMode.None).Length;
                victim.GetComponent<Health>().TakeDamage(9999f, player.gameObject);
                int after = Object.FindObjectsByType<ScrapPickup>(FindObjectsSortMode.None).Length;
                Require(after > before, "A dead chameleon should drop scrap.");
                Require(CrystalName(victim).Contains("Spent"), "Its crystals should switch off when it dies, got " + CrystalName(victim));
                Report("CRYSTAL_CHAMELEON_PLAY_MODE_PASS: 5 hidden (2 in walls) + Crystal Disruptor given, wall one clings, is revealed, drops upright onto the NavMesh; scanner ignores a hidden one without the upgrade, beeps at it with it, reveals it up close -> red + emerge, tongue hits=" + playerHits + ", scrap " + (after - before) + ", crystals off on death");
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(0);
            }
            else if (stage == 7 && t > 36f) throw new InvalidOperationException("No chameleon tongue hit the player within 24 s.");
        }
        catch (Exception exception)
        {
            SessionState.SetBool(Key, false);
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void Teleport(FirstPersonController player, Vector3 at)
    {
        var body = player.GetComponent<CharacterController>();
        body.enabled = false;
        player.transform.position = at;
        body.enabled = true;
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
