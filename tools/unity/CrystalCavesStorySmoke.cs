// Install only in a disposable project's Assets/Editor; never in the real project.
// Batch: -executeMethod CrystalCavesStorySmoke.Start (no -quit; it exits itself).
// Plays CrystalCaves.unity through the whole story: welcome, the three jobs (tasks, boss, trophy on the rack, part
// handed to Cavendish), the Axolotl's gate (parts handed in, Disruptor), the fight with its chamber chameleons, the
// module, the homecoming and the boat; plus the scanner's route-chameleon override.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using SignalLost.Combat;
using SignalLost.Enemies.Caves;
using SignalLost.Enemies.Desert;
using SignalLost.Player;
using SignalLost.Progression;
using SignalLost.Scanner;
using SignalLost.Story;
using SignalLost.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CrystalCavesStorySmoke
{
    private const string Key = "SignalLost.CrystalCavesStorySmoke";
    private const string ScenePath = "Assets/Scenes/Large Areas/CrystalCaves.unity";
    internal static bool done;
    internal static string report = "";
    private static string error;
    private static string Backup => CampSave.FilePath + ".smoke-backup";

    // Puts the player's camp save back exactly as it was before the test.
    private static void RestoreSave()
    {
        if (SessionState.GetBool(Key + "HadSave", false) && File.Exists(Backup)) File.Copy(Backup, CampSave.FilePath, true);
        else File.Delete(CampSave.FilePath);
        File.Delete(Backup);
    }
    private static CrystalCavesStorySmokeRunner runner;

    static CrystalCavesStorySmoke()
    {
        EditorApplication.update += Update;
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (message.StartsWith("ArgumentOutOfRangeException: Index was out of range.") && trace.Contains("UnityEditor.Search.SearchDatabase")) return;
            error = message + "\n" + trace;
        };
    }

    public static void Start()
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
        string temporary = Path.GetFullPath(Path.GetTempPath()).Replace('\\', '/').TrimEnd('/') + "/";
        if (!Application.isBatchMode || !project.StartsWith(temporary, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use only a disposable batch-mode project under the system temporary directory.");
        // The camp save lives under the company/product name, shared with the real project: keep it out of the test.
        if (File.Exists(CampSave.FilePath)) File.Copy(CampSave.FilePath, Backup, true);
        else File.Delete(Backup);
        SessionState.SetBool(Key + "HadSave", File.Exists(CampSave.FilePath));
        File.Delete(CampSave.FilePath);
        EditorSceneManager.OpenScene(ScenePath);
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + "Deadline", (float)EditorApplication.timeSinceStartup + 900);
        EditorApplication.EnterPlaymode();
    }

    private static void Update()
    {
        if (!SessionState.GetBool(Key, false)) return;
        try
        {
            Require(error == null, error);
            Require(EditorApplication.timeSinceStartup < SessionState.GetFloat(Key + "Deadline", 0), "Smoke timed out. So far: " + report);
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (runner == null) runner = new GameObject("Crystal Caves Story Smoke").AddComponent<CrystalCavesStorySmokeRunner>();
            if (!done) return;
            Debug.Log("CRYSTAL_CAVES_STORY_PLAY_MODE_PASS:" + report);
            SessionState.SetBool(Key, false);
            RestoreSave();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            SessionState.SetBool(Key, false);
            RestoreSave();
            Debug.LogException(exception);
            Debug.Log("CRYSTAL_CAVES_STORY_PLAY_MODE_FAIL after:" + report);
            EditorApplication.Exit(1);
        }
    }

    internal static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}

public sealed class CrystalCavesStorySmokeRunner : MonoBehaviour
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private CavesStory story;
    private CharacterController body;
    private Health hp;
    private PlayerInteractor interactor;
    private ScannerController scanner;
    private AxolotlLair lair;

    private static void Require(bool ok, string message) => CrystalCavesStorySmoke.Require(ok, message);
    private static void Note(string line) => CrystalCavesStorySmoke.report += " " + line + ";";
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Any).GetValue(owner);

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(2f);
        story = FindAnyObjectByType<CavesStory>();
        var player = FindAnyObjectByType<FirstPersonController>();
        body = player.GetComponent<CharacterController>();
        hp = player.GetComponentInChildren<Health>();
        interactor = player.GetComponentInChildren<PlayerInteractor>(true);
        scanner = player.GetComponentInChildren<ScannerController>(true);
        lair = FindAnyObjectByType<AxolotlLair>();
        Require(story != null && interactor != null && scanner != null && lair != null, "Missing the story, player interactor, scanner or lair.");
        PlayerSpawnPoint spawn = FindAnyObjectByType<PlayerSpawnPoint>();
        Require(Vector3.Distance(player.transform.position, spawn.transform.position) < 2f &&
                Vector3.Distance(spawn.transform.position, IslandDock.Find(IslandDock.Role.Arrival).Landing.position) < 2f, "The player doesn't start on the dock.");
        Require(story.Current == CavesStory.Stage.Welcome, "Expected the welcome first.");
        // Before Cavendish: the raised scanner has nothing at all (no beeps, empty bar).
        var toggle = scanner.GetComponentInParent<FirstPersonController>().GetComponentInChildren<ScannerToggle>(true);
        toggle.SetRaised(true);
        yield return new WaitForSeconds(1f);
        Require(scanner.Silenced && scanner.CurrentTarget == null && scanner.SignalStrength01 == 0f, $"The scanner found {scanner.CurrentTarget} before the first talk with Cavendish.");
        story.SkipWelcome();
        yield return new WaitForSeconds(1f);
        Require(!scanner.Silenced && scanner.CurrentTarget is QuestSignalSource, "The scanner didn't pick up the first job's signals after Cavendish.");
        toggle.SetRaised(false);
        Note("silent until Cavendish");
        Require(story.Current == CavesStory.Stage.Tasks && story.JobIndex == 0, "Expected job 1's tasks.");
        Require(Vector3.Distance(PlayerSpawnPoint.Current.transform.position, Field<Transform>(story, "campRespawn").position) < 0.1f, "The respawn didn't move to the camp.");
        Note("welcome");

        // The chamber refuses before any part is handed in.
        yield return Visit(lair.transform.position + Vector3.back * 5f, 2f);
        Require(!lair.InCutsceneOrFight, "The Axolotl started with no parts handed in.");
        Note("gate shut");

        Array jobs = Field<Array>(story, "jobs");
        for (int j = 0; j < 3; j++)
        {
            object job = jobs.GetValue(j);
            var group = Field<GameObject>(job, "group");
            var bosses = Field<DesertMiniBoss[]>(job, "bosses");
            var tasks = Field<QuestTask[]>(job, "tasks");
            Require(!group.activeSelf, $"Job {j + 1}: the boss is out before its tasks.");
            foreach (QuestTask task in tasks)
            {
                Require(task.Armed && task.GetComponent<QuestSignalSource>().enabled, $"{task.name} isn't armed with its signal on.");
                if (j == 0)
                {
                    var floating = task.GetComponentInChildren<SignalLost.Inventory.FloatingPickup>();
                    Require(floating != null && floating.Display != null && floating.Display.position.y - task.transform.position.y > 0.5f, $"{task.name} isn't floating.");
                }
                Require(task.Interact(interactor), $"{task.name} refused E.");
                if (j == 0) Require(task.GetComponentsInChildren<Collider>().All(c => !c.enabled || !c.gameObject.activeInHierarchy), $"{task.name} still blocks the way after pickup.");
            }
            Require(story.Current == CavesStory.Stage.Hunt && group.activeSelf, $"Job {j + 1}: no hunt after the tasks.");
            yield return Visit(bosses[0].transform.position + Vector3.back * 8f, 1f);
            float give = Time.time + 60f;
            while (bosses.Any(b => !b.IsDead) && Time.time < give)
            {
                hp.Heal(1000f);
                foreach (DesertMiniBoss boss in bosses)
                    if (!boss.IsDead) boss.GetComponent<Health>().TakeDamage(80f, body.gameObject);
                yield return new WaitForSeconds(0.25f);
            }
            Require(bosses.All(b => b.IsDead), $"Job {j + 1}: the boss didn't die.");
            yield return new WaitForSeconds(1.5f);
            Require(story.Current == CavesStory.Stage.Loot, $"Job {j + 1}: expected the loot stage, got {story.Current}.");

            ShipPartDrop part = FindObjectsByType<ShipPartDrop>().FirstOrDefault();
            Require(part != null, $"Job {j + 1}: no ship part dropped.");
            yield return Visit(Field<Transform>(part, "display").position, 1.5f);
            TrophyPickup trophy = FindAnyObjectByType<TrophyPickup>();
            Require(trophy != null && trophy.Interact(interactor), $"Job {j + 1}: no trophy to pick up.");
            yield return null;
            TrophyStand[] rack = Field<TrophyStand[]>(story, "rack");
            yield return Visit(rack[j].transform.position + Vector3.back * 1.5f, 0.5f);
            Require(rack.Any(stand => stand.Interact(interactor)), $"Job {j + 1}: couldn't set the trophy on the caves' rack.");
            yield return null;
            Require(story.Current == CavesStory.Stage.Report, $"Job {j + 1}: expected the report, got {story.Current}.");
            story.SkipReport();
            Require(story.PartsHandedIn == j + 1, $"Job {j + 1}: the part wasn't handed in.");
            Note($"job {j + 1}");
        }

        // The finale: three parts handed in, but no Disruptor yet.
        Require(story.Current == CavesStory.Stage.Finale && Field<QuestSignalSource>(story, "chamberSignal").enabled, "Expected the finale with the chamber signal on.");
        Require(!scanner.DisruptsCamouflage, "The player shouldn't own the Disruptor yet.");
        yield return Visit(lair.transform.position + Vector3.back * 5f, 2f);
        Require(!lair.InCutsceneOrFight, "The Axolotl started without the Disruptor.");
        Note("gate needs disruptor");

        // The route-chameleon override (the chamber's quest signal is on).
        scanner.DisruptsCamouflage = true;
        CrystalChameleon routeChameleon = FindObjectsByType<CrystalChameleon>()
            .First(c => Field<float>(c, "questOverrideRadius") > 0f && !Field<bool>(c, "hideInWall"));
        MethodInfo scan = typeof(ScannerController).GetMethod("ScanForNearestTarget", Any);
        yield return Visit(routeChameleon.transform.position + routeChameleon.transform.forward * 12f, 0.5f);
        scan.Invoke(scanner, null);
        Require(scanner.CurrentTarget != null && scanner.CurrentTarget.GetComponentInParent<CrystalChameleon>() == routeChameleon,
            $"Within 20 m the scanner should follow {routeChameleon.name}, not {scanner.CurrentTarget}.");
        yield return Visit(routeChameleon.transform.position + routeChameleon.transform.forward * 40f, 0.5f);
        scan.Invoke(scanner, null);
        Require(scanner.CurrentTarget is QuestSignalSource, "Outside 20 m the quest signal should win.");
        Note("override");

        // The fight.
        yield return Visit(lair.transform.position + Vector3.back * 5f, 0.5f);
        float wait = Time.time + 30f;
        CaveAxolotl axolotl = Field<CaveAxolotl>(lair, "axolotl");
        while (!(lair.InCutsceneOrFight && axolotl.Engaged) && Time.time < wait) { hp.Heal(1000f); yield return null; }
        Require(axolotl.Engaged, "The Axolotl fight didn't start with parts and Disruptor.");
        yield return new WaitForSeconds(0.5f);
        Require(story.Current == CavesStory.Stage.FinaleFight && !Field<QuestSignalSource>(story, "chamberSignal").enabled, "Expected the finale fight with the chamber signal off.");
        CrystalChameleon[] chamber = Field<CrystalChameleon[]>(lair, "chamberChameleons");
        Require(chamber.All(c => c.IsDisguised), "Chamber chameleons rose too early.");
        Require(chamber.All(c => c.GetComponentsInChildren<EnemySignalSource>(true).All(s => !s.enabled)), "Chamber chameleons must have no scanner signal.");
        var axolotlHp = axolotl.GetComponent<Health>();
        wait = Time.time + 40f;
        while (axolotl.DuelPhase < 2 && Time.time < wait)
        {
            hp.Heal(1000f);
            axolotlHp.TakeDamage(40f, body.gameObject);
            yield return new WaitForSeconds(0.3f);
        }
        Require(axolotl.DuelPhase >= 2, "The Axolotl never reached phase 2.");
        wait = Time.time + 25f;
        while (chamber.Any(c => c.IsDisguised) && Time.time < wait) { hp.Heal(1000f); yield return null; }
        Require(chamber.All(c => !c.IsDisguised), "Not every chamber chameleon rose in phase 2.");
        Note($"{chamber.Length} chamber chameleons rose");
        foreach (CrystalChameleon c in chamber) c.GetComponent<Health>().TakeDamage(9999f, body.gameObject);
        wait = Time.time + 120f;
        while (!axolotl.IsDead && Time.time < wait)
        {
            hp.Heal(1000f);
            if (axolotl.IsDisguised) axolotl.Disrupt();
            axolotlHp.TakeDamage(40f, body.gameObject);
            yield return new WaitForSeconds(0.25f);
        }
        Require(axolotl.IsDead, "The Axolotl didn't die.");
        yield return new WaitForSeconds(1f);
        Require(story.Current == CavesStory.Stage.FinaleLoot, $"Expected the module to pick up, got {story.Current}.");
        wait = Time.time + 15f;
        EngineCorePickup module = null;
        while (module == null && Time.time < wait) { module = FindAnyObjectByType<EngineCorePickup>(); yield return null; }
        Require(module != null, "The Flight Control Module didn't drop.");
        yield return new WaitForSeconds(1.2f);
        yield return Visit(Field<Transform>(module, "display").position, 1.5f);
        Require(story.Current == CavesStory.Stage.Homecoming, $"Expected the homecoming, got {story.Current}.");
        Require(StageProgress.IsCleared(CaveAxolotl.Stage), "The Crystal Caves weren't cleared.");
        Require(IslandDock.Find(IslandDock.Role.Arrival).IsOpen, "The dock's boat should be able to sail now.");
        Note("module, boat open");
        CrystalCavesStorySmoke.done = true;
    }

    private IEnumerator Visit(Vector3 at, float seconds)
    {
        body.enabled = false;
        body.transform.position = at + Vector3.up * 0.3f;
        body.enabled = true;
        float end = Time.time + seconds;
        while (Time.time < end) { hp.Heal(1000f); yield return null; }
    }
}
