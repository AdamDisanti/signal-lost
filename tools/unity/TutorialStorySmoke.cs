using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SignalLost.Combat;
using SignalLost.Enemies;
using SignalLost.Inventory;
using SignalLost.NPC;
using SignalLost.Player;
using SignalLost.Scanner;
using SignalLost.Story;
using SignalLost.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

// Play Mode smoke for the new tutorial (tutorial overhaul phase 4): the opening, Cavendish's greeting, finding the
// hidden laser and scanner by the real interaction ray, the big-crab quest, the merchant and its ten cousins, the
// walk back to camp, the merchant's introduction (and enough scrap for a first upgrade), and the angry-merchant
// ambush bringing back the same ten cousins. Install only in a disposable project copy under the system temp
// directory (Assets/Editor), then run:
//   Unity -batchmode -projectPath <temp-project> -executeMethod TutorialStorySmoke.Start -logFile <log>
// Omit -quit. Add -storyRender (without -nographics) to save captures to Logs/Story_*.png. Not a physical-input test.
[InitializeOnLoad]
public static class TutorialStorySmoke
{
    private const string Key = "SignalLost.TutorialStorySmoke";
    private static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    private static readonly List<string> report = new List<string>();
    private static bool started;
    private static int frame = -1;
    private static float resume;
    private static Camera view;

    static TutorialStorySmoke()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += (m, trace, type) =>
        {
            if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception) &&
                !trace.Contains("UnityEditor.Search.SearchDatabase")) Fail(new Exception(m + "\n" + trace));
        };
    }

    public static void Start()
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        if (!Application.isBatchMode || !(project.StartsWith(Path.GetFullPath(Path.GetTempPath())) || project.StartsWith("/private/tmp/")))
            throw new InvalidOperationException("Run in a disposable batch-mode project under the system temporary directory.");
        EditorSceneManager.OpenScene("Assets/Scenes/Tutorial/Tutorial Demo.unity");
        SignalLost.Progression.CampSave.Clear(); // start (and finish) with an empty camp
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + "Time", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Time", 0) > 500) { Fail(new Exception("Timed out.")); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.unscaledTime < resume || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (!started) { started = true; steps.Push(Run()); }
            while (steps.Count > 0)
            {
                IEnumerator top = steps.Peek();
                if (!top.MoveNext()) { steps.Pop(); continue; }
                if (top.Current is IEnumerator nested) { steps.Push(nested); continue; }
                resume = Time.unscaledTime + (top.Current is float seconds ? seconds : 0);
                return;
            }
            SessionState.SetBool(Key, false);
            Directory.CreateDirectory("Logs"); File.WriteAllLines("Logs/TutorialStorySmoke.txt", report);
            Debug.Log("TUTORIAL_STORY_SMOKE_PASS\n" + string.Join("\n", report));
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static IEnumerator Run()
    {
        var story = UnityEngine.Object.FindAnyObjectByType<TutorialStory>();
        var life = UnityEngine.Object.FindAnyObjectByType<PlayerLife>();
        var gear = life.GetComponent<PlayerGear>();
        var interactor = life.GetComponent<PlayerInteractor>();
        var inventory = life.GetComponent<PlayerInventory>();
        Check(!SignalLost.Audio.PlayerAudio.FootstepsEnabled, "Player footsteps are switched off for now.");
        Check(life.GetComponentInChildren<ScannerController>(true).Range >= 200f, "The tutorial scanner uses its 200 m level range (Amanda's rework).");
        view = life.GetComponentInChildren<Camera>();
        NpcActor cavendish = UnityEngine.Object.FindObjectsByType<NpcActor>().First(n => n.Kind == NpcActor.Role.Cavendish);
        MerchantCrab merchant = UnityEngine.Object.FindAnyObjectByType<MerchantCrab>(FindObjectsInactive.Include);
        DialogueUi ui = DialogueUi.Instance;

        // ---------- Opening ----------
        Check(story.Current == TutorialStory.Stage.Opening, "The tutorial opens with the crash cutscene.");
        Check(!merchant.gameObject.activeSelf && merchant.Cousins.All(c => !c.gameObject.activeSelf), "The hollow is empty until Cavendish's big-crab quest.");
        Check(!gear.Has(PlayerItem.MiningLaser) && !gear.Has(PlayerItem.Scanner) && !gear.Has(PlayerItem.Flask), "The mechanic starts empty-handed.");
        Transform cockpit = GameObject.Find("Opening - Cockpit View").transform;
        yield return 3f;
        Check(Vector3.Distance(view.transform.position, cockpit.position) < 0.3f, "They come to in the pilot seat.");
        yield return Capture("Cockpit");
        yield return WaitFor(() => Vector3.Distance(cavendish.transform.position, view.transform.position) < 2.5f, 20f, "Cavendish drags the mechanic away.");
        yield return 1.2f;
        yield return Capture("Dragged");
        yield return WaitFor(() => story.Current == TutorialStory.Stage.Greeting, 40f, "They wake by the campfire.");
        Check(Vector3.Distance(life.transform.position, PlayerSpawnPoint.Current.transform.position) < 0.5f, "...at the camp spawn point.");
        Check(ui.PopupOpen && ui.Owner == cavendish.transform, "Cavendish greets them straight away.");
        Check(Vector3.Distance(cavendish.transform.position, GameObject.Find("Campsite").transform.position) < 5f, "Cavendish is back at his seat across the fire.");
        yield return Capture("Greeting");
        yield return ReadToEnd(ui);
        Check(story.Current == TutorialStory.Stage.SearchGear, "The greeting sends them to search the crash site.");

        // ---------- Search the wreck ----------
        Check(cavendish.Interact(interactor) && cavendish.Dialogue.IsManual && cavendish.Dialogue.CurrentLine.Contains("Back already"),
            "Coming back empty-handed: Cavendish says to keep looking.");
        cavendish.Dialogue.AdvanceManual();
        ItemPickup scanner = UnityEngine.Object.FindObjectsByType<ItemPickup>().First(p => p.Kind == ItemPickup.Item.Scanner);
        ItemPickup laser = UnityEngine.Object.FindObjectsByType<ItemPickup>().First(p => p.Kind == ItemPickup.Item.Laser);
        Light glint = scanner.GetComponentInChildren<Light>();
        Teleport(life, scanner.transform.position + (scanner.transform.position - laser.transform.position).normalized * 20f);
        yield return 0.2f;
        Check(!glint.enabled, "The scanner's glint is hidden from 20 m away.");
        Aim(life, scanner);
        yield return null;
        Check(glint.enabled, "...and shows up close.");
        yield return Capture("ScannerFound");
        Aim(life, scanner); // captures take a few frames, in which the player can settle off the spot
        Check(ReadCardsThen(interactor).TryInteract() && gear.Has(PlayerItem.Scanner), "The scanner can be picked up from under its panel by the interaction ray.");
        Check(QuestSignalSource.Nearest(life.transform.position) == laser.GetComponent<QuestSignalSource>(), "With the scanner back, it points at the laser.");
        Aim(life, laser);
        yield return null;
        yield return Capture("LaserFound");
        Aim(life, laser);
        Check(ReadCardsThen(interactor).TryInteract() && gear.Has(PlayerItem.MiningLaser), "The laser can be picked up in the reactor bay by the interaction ray.");
        Check(QuestSignalSource.Nearest(life.transform.position) == cavendish.GetComponent<QuestSignalSource>(), "With both found, the scanner points back to Cavendish.");

        // ---------- The big crab ----------
        Teleport(life, PlayerSpawnPoint.Current.transform.position);
        yield return 0.3f;
        Check(cavendish.Interact(interactor) && ui.PopupOpen && ui.CurrentLine.Contains("You found them both"), "Cavendish tells of the big crab.");
        yield return ReadToEnd(ui);
        yield return null;
        Check(story.Current == TutorialStory.Stage.Fight && merchant.gameObject.activeSelf && !merchant.GetComponent<Health>().Invulnerable,
            "The crab hunt is on: the crab and its cousins are now in the hollow.");
        Check(QuestSignalSource.Nearest(life.transform.position) == merchant.GetComponent<QuestSignalSource>(), "The scanner points at the crab.");
        CrabCousin[] cousins = merchant.Cousins.ToArray();
        Check(cousins.Length == 10 && cousins.Select(c => c.name).Distinct().Count() == 10, "Ten cousins with distinct names: " + string.Join(", ", cousins.Select(c => c.name)));
        Check(cousins.All(c => c.Crab.enabled && c.GetComponent<HermitCrabRig>().Shell != null), "Every cousin is awake and wears its shell.");
        Check(cousins.Select(c => c.GetComponent<HermitCrabRig>().Shell.name).Distinct().Count() == 10, "...and every shell is different.");
        CombatZone zone = merchant.GetComponentInParent<CombatZone>();
        Check(cousins.All(c => c.Crab.Territory.Zone == zone) && merchant.GetComponent<HermitCrab>().Territory.Zone == zone, "The crab family shares the hollow's fighting radius.");

        Vector3 entry = zone.Centre + new Vector3(10f, 0f, -3f);
        Teleport(life, entry);
        view.transform.LookAt(zone.Centre + Vector3.up * 0.5f);
        yield return 2f;
        Check(cousins.Count(c => GetState(c.Crab) != "Idle") >= 8, "Stepping into the hollow sets the whole family on the mechanic.");
        yield return Capture("BucketBrothers");
        life.Health.Invulnerable = true; // the checks below are about the quest, not about surviving ten crabs

        merchant.GetComponent<Health>().TakeDamage(10000f, life.gameObject);
        yield return 1.6f;
        Check(merchant.IsDefeated && !merchant.IsReturning && story.Current == TutorialStory.Stage.Fight, "The crab gives up first but waits while the cousins still fight.");
        foreach (CrabCousin cousin in cousins) cousin.GetComponent<Health>().TakeDamage(10000f, life.gameObject);
        yield return 3.5f;
        Check(cousins.All(c => !c.gameObject.activeSelf), "Defeated cousins despawn.");
        Check(story.Current == TutorialStory.Stage.MerchantComing, "With the family down, the crab heads for camp.");
        ItemPickup flask = UnityEngine.Object.FindObjectsByType<ItemPickup>().First(p => p.Kind == ItemPickup.Item.Flask);
        Check(flask.gameObject.activeInHierarchy, "The crab dropped the bottle.");
        Check(flask.Interact(interactor) && gear.Has(PlayerItem.Flask), "The flask is back.");
        foreach (ScrapPickup chunk in UnityEngine.Object.FindObjectsByType<ScrapPickup>())
        {
            if (chunk == null) continue; // already pulled in on the way past
            Teleport(life, chunk.transform.position);
            yield return 0.4f;
        }
        yield return 1f;
        report.Add($"Scrap after the fight: {inventory.Scrap(ScrapType.ScrapMetal)} metal, {inventory.Scrap(ScrapType.Wiring)} wiring, {inventory.Scrap(ScrapType.CircuitBoards)} boards, {inventory.Scrap(ScrapType.FuelCells)} fuel cells.");
        Check(inventory.Scrap(ScrapType.ScrapMetal) >= 10 && inventory.Scrap(ScrapType.Wiring) >= 4, "Enough scrap for the first upgrade (Overclocked Coil: 10 metal, 4 wiring).");

        // ---------- Back at camp ----------
        Teleport(life, PlayerSpawnPoint.Current.transform.position);
        Transform home = GameObject.Find("Merchant Spot At Camp").transform;
        var agent = merchant.GetComponent<NavMeshAgent>();
        Check(agent.isOnNavMesh && merchant.IsReturning, "The crab walks on the NavMesh.");
        Vector3 toCamp = merchant.transform.position - home.position;
        NavMeshPath path = new NavMeshPath();
        Check(NavMesh.CalculatePath(agent.nextPosition, home.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, "There is a complete path from the hollow to camp.");
        if (NavMesh.SamplePosition(home.position + toCamp.normalized * 14f, out NavMeshHit near, 4f, NavMesh.AllAreas)) agent.Warp(near.position); // skip most of the walk
        yield return WaitFor(() => merchant.IsHome, 40f, "The crab arrives at camp.");
        yield return 0.3f;
        Check(story.Current == TutorialStory.Stage.MerchantIntro && ui.PopupOpen && ui.Owner == merchant.transform, "It introduces itself right away.");
        yield return Capture("MerchantIntro");
        yield return ReadToEnd(ui);
        Check(story.Current == TutorialStory.Stage.FirstUpgrade && MerchantMenuUi.Current != null && MerchantMenuUi.Current.IsOpen,
            "After the introduction Eugene's shop opens for a first upgrade.");
        yield return Capture("FirstShop");
        MerchantMenuUi.Current.Close();
        yield return null;

        // ---------- First upgrade, then the pond frogs ----------
        Check(ReadCardsThen(interactor) != null && cavendish.Interact(interactor) && cavendish.Dialogue.CurrentLine.Contains("kitted out"),
            "Cavendish holds his next job until an upgrade is bought.");
        cavendish.Dialogue.AdvanceManual();
        Upgrade first = UpgradeCatalog.All.First(u => u.OneOff && inventory.WhyNot(u) == null);
        Check(inventory.Buy(first) && story.Current == TutorialStory.Stage.ShowCavendish, "Buying " + first.Name + " sends the player back to Cavendish.");
        Check(QuestSignalSource.Nearest(life.transform.position) == cavendish.GetComponent<QuestSignalSource>(), "The scanner points at Cavendish.");
        ReadCardsThen(interactor);
        Check(cavendish.Interact(interactor) && ui.PopupOpen && ui.CurrentLine.Contains("shiny"), "Cavendish admires the upgrade and asks about the pond frogs.");
        yield return ReadToEnd(ui);
        FrogWaves frogs = UnityEngine.Object.FindAnyObjectByType<FrogWaves>();
        Check(story.Current == TutorialStory.Stage.Frogs && frogs.Started && frogs.Total == 20 && frogs.WaveCount == 3, "The frog quest begins: 20 frogs in three waves.");
        yield return 1f;
        Check(frogs.Alive.Count == 5, "Wave 1: five frogs at the pond.");
        Check(QuestSignalSource.Nearest(life.transform.position).GetComponent<MortarFrog>() != null, "The scanner points at the nearest frog.");
        Teleport(life, frogs.transform.position + Vector3.forward * 20f);
        view.transform.LookAt(frogs.Alive[0].transform.position);
        yield return 1.5f;
        yield return Capture("PondFrogs");
        int[] expected = { 5, 7, 8 };
        for (int w = 0; w < 3; w++)
        {
            Check(frogs.Wave == w + 1 && frogs.Alive.Count == expected[w], $"Wave {w + 1}: {expected[w]} frogs.");
            foreach (MortarFrog frog in frogs.Alive.ToArray()) frog.GetComponent<Health>().TakeDamage(10000f, life.gameObject);
            yield return w < 2 ? 4f : 1f;
        }
        Check(frogs.Finished && frogs.Defeated == 20 && story.Current == TutorialStory.Stage.FrogsDone, "All 20 frogs cleared; report back to Cavendish.");
        Teleport(life, PlayerSpawnPoint.Current.transform.position);
        yield return 0.3f;
        ReadCardsThen(interactor);
        Check(cavendish.Interact(interactor) && ui.PopupOpen && ui.CurrentLine.Contains("Twenty frogs"), "Cavendish warns of the Chief Toad.");
        yield return ReadToEnd(ui);
        ChiefToadEncounter encounter = UnityEngine.Object.FindAnyObjectByType<ChiefToadEncounter>();
        Check(story.Current == TutorialStory.Stage.ChiefToad && encounter.Current == ChiefToadEncounter.State.Armed && !encounter.Toad.gameObject.activeSelf,
            "Cavendish gives the Chief Toad quest; the toad is not there until the player reaches the pond.");
        yield return Capture("ChiefToad");

        // ---------- The Chief Toad ----------
        Teleport(life, frogs.transform.position + Vector3.back * 30f);
        yield return WaitFor(() => encounter.Current == ChiefToadEncounter.State.Cutscene, 3f, "Reaching the pond starts the cutscene.");
        yield return 2.5f;
        Check(encounter.Toad.gameObject.activeSelf && !encounter.Toad.enabled && Camera.main != view, "The toad rises from the middle of the pond, seen from the bank.");
        var shot = UnityEngine.Object.FindObjectsByType<Camera>().FirstOrDefault(c => c.name.Contains("Cutscene"));
        if (shot != null) { Camera player = view; view = shot; yield return Capture("ToadRise"); view = player; }
        yield return WaitFor(() => encounter.Current == ChiefToadEncounter.State.Fighting, 8f, "Control returns for the fight.");
        Check(encounter.Toad.enabled && view.enabled && life.GetComponent<FirstPersonController>().enabled, "The player can move again.");
        yield return 1.5f;
        Check(encounter.Toad.Engaged, "The Chief Toad engages (his boss bar is up).");
        Check(encounter.Toad.PhaseThreeAt > 0f && encounter.Toad.PhaseThreeAt < encounter.Toad.PhaseTwoAt, "His boss bar marks where phases 2 and 3 start.");
        yield return 4f;
        Check(frogs.Sustaining && frogs.Alive.Count >= 3 && frogs.Alive.Count <= 5, $"3-5 frogs keep him company ({frogs.Alive.Count} out).");
        yield return Capture("ToadFight");
        life.Health.Invulnerable = true;
        var toadHealth = encounter.Toad.GetComponent<Health>();
        int topPhase = 1;
        for (int i = 0; i < 200 && encounter.Current != ChiefToadEncounter.State.Defeated; i++)
        {
            topPhase = Mathf.Max(topPhase, encounter.Toad.DisplayPhase);
            toadHealth.TakeDamage(120f, life.gameObject);
            yield return 0.2f;
        }
        Check(encounter.Current == ChiefToadEncounter.State.Defeated && story.Current == TutorialStory.Stage.ToadBeaten, "The Chief Toad is beaten.");
        Check(topPhase == 3, "His bar went through all three phases.");
        yield return 0.5f;
        Check(!frogs.Sustaining && frogs.Alive.Count == 0, "His frogs leave with him.");
        Check(encounter.Trophy != null && UnityEngine.Object.FindObjectsByType<ScrapPickup>().Length > 0, "He leaves a loot pile: scrap, the cooling system and his crown.");
        yield return 1f;
        yield return Capture("ToadLoot");
        Check(encounter.Trophy.Interact(interactor) && inventory.HasKeyItem("chief_toad_crown"), "The crown goes into the pockets.");
        ReadCardsThen(interactor);
        var part = UnityEngine.Object.FindAnyObjectByType<SignalLost.Enemies.Desert.ShipPartDrop>();
        Check(part != null, "The cooling system lies in the loot.");
        Teleport(life, part.transform.position);
        yield return WaitFor(() => SignalLost.Enemies.Desert.ShipPartDrop.CountFor(SignalLost.Progression.StageProgress.Tutorial) > 0, 5f, "Walking over the cooling system picks it up.");

        // ---------- The trophy at camp, and the end ----------
        Teleport(life, PlayerSpawnPoint.Current.transform.position);
        yield return 0.3f;
        var stand = UnityEngine.Object.FindObjectsByType<SignalLost.Progression.TrophyStand>().OrderBy(t => t.Index).First();
        Check(stand.Prompt.Contains("Crown") && stand.Interact(interactor) && stand.TrophyId == "chief_toad_crown", "The crown goes on a trophy stand at camp.");
        yield return null;
        Check(stand.GetComponentsInChildren<Transform>().Any(t => t.name == "Chief Toad's Crown") && File.Exists(SignalLost.Progression.CampSave.FilePath) &&
              File.ReadAllText(SignalLost.Progression.CampSave.FilePath).Contains("chief_toad_crown"), "It is shown on the stand and saved to disk.");
        Check(story.Current == TutorialStory.Stage.ToadBeaten, "With the crown placed, the cooling system still has to go to Cavendish.");
        Check(cavendish.Interact(interactor) && ui.PopupOpen && ui.CurrentLine.Contains("cooling system"), "Cavendish takes the cooling system...");
        yield return ReadToEnd(ui);
        Check(story.Current == TutorialStory.Stage.TrophyPlaced, "...for 'safe keeping'. Then: talk to Cavendish.");
        view.transform.LookAt(stand.transform.position + Vector3.up);
        yield return Capture("Trophy");
        string timesPath = TutorialRunTimer.FilePath;
        string timesBefore = File.Exists(timesPath) ? File.ReadAllText(timesPath) : null; // put back afterwards
        Check(story.Timer.Running && story.Timer.Elapsed > 0f, $"The tutorial clock is running ({TutorialRunTimer.Format(story.Timer.Elapsed)}).");
        Check(cavendish.Interact(interactor) && ui.PopupOpen && ui.CurrentLine.Contains("You did it"), "Cavendish wraps up the demo.");
        yield return ReadToEnd(ui);
        Check(story.Current == TutorialStory.Stage.DemoComplete && DemoEndCard.Current != null && DemoEndCard.Current.IsOpen, "The 'Thanks for playing' card appears.");
        string[] times = File.Exists(timesPath) ? File.ReadAllLines(timesPath) : new string[0];
        Check(!story.Timer.Running && times.Length >= 2 && times[0].StartsWith("finished_utc") && times[times.Length - 1].Contains(TutorialRunTimer.Format(story.Timer.Elapsed)),
            $"The completion time stops and is saved for playtesting: {times.LastOrDefault()}");
        Check(DemoEndCard.Current.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text.Contains("Completion time") && t.text.Contains(TutorialRunTimer.Format(story.Timer.Elapsed))),
            "The end card shows the completion time.");
        if (timesBefore != null) File.WriteAllText(timesPath, timesBefore); else File.Delete(timesPath);
        yield return Capture("EndCard");
        DemoEndCard.Current.KeepExploring();
        Check(!DemoEndCard.Current.IsOpen && !UiMenus.AnyOpen, "Keep Exploring returns the player to the meadow.");
        SignalLost.Progression.CampSave.Clear();

        // ---------- Angry merchant ----------
        for (int hit = 0; hit < 6; hit++) merchant.GetComponent<Health>().TakeDamage(10f, life.gameObject);
        yield return 0.3f;
        Check(merchant.AmbushActive && merchant.Ambush.Count == 10, "The sixth hit calls the cousins.");
        Check(merchant.Ambush.Select(c => c.name).OrderBy(n => n).SequenceEqual(cousins.Select(c => c.name).OrderBy(n => n)) &&
              cousins.All(c => c.gameObject.activeSelf && !c.Crab.IsDead), "...the same ten, back on their feet around the merchant.");
        Check(cousins.All(c => Vector3.Distance(c.transform.position, merchant.transform.position) < 10f && c.Crab.Territory.Zone == null), "...defending the spot they were called to.");
        yield return 1.5f;
        yield return Capture("Ambush");
        int scrapBefore = UnityEngine.Object.FindObjectsByType<ScrapPickup>().Length;
        foreach (CrabCousin cousin in cousins) cousin.GetComponent<Health>().TakeDamage(10000f, life.gameObject);
        yield return 0.5f;
        Check(UnityEngine.Object.FindObjectsByType<ScrapPickup>().Length == scrapBefore, "Called-back cousins drop no scrap (no farming the angry merchant).");
        yield return 3f;
        Check(!merchant.AmbushActive && cousins.All(c => !c.gameObject.activeSelf), "Beating them ends the ambush and they despawn again.");
    }

    // Item cards wait for the player (E dismisses them first), so read any open ones before interacting.
    private static PlayerInteractor ReadCardsThen(PlayerInteractor interactor)
    {
        while (GameplayOverlay.Current != null && GameplayOverlay.Current.CardOpen) GameplayOverlay.Current.DismissCard();
        return interactor;
    }

    private static IEnumerator ReadToEnd(DialogueUi ui)
    {
        for (int i = 0; i < 40 && ui.PopupOpen; i++)
        {
            ui.Advance(); // finish typing
            yield return 0.05f;
            ui.Advance(); // next line
            yield return 0.05f;
        }
        if (ui.PopupOpen) throw new Exception("Conversation did not finish.");
    }

    private static IEnumerator WaitFor(Func<bool> condition, float seconds, string message)
    {
        float until = Time.unscaledTime + seconds;
        while (!condition())
        {
            if (Time.unscaledTime > until) throw new Exception("Timed out waiting: " + message);
            yield return 0.1f;
        }
        report.Add(message);
    }

    private static string GetState(HermitCrab crab) =>
        typeof(HermitCrab).GetField("state", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(crab).ToString();

    private static void Teleport(PlayerLife player, Vector3 position, bool snapToGround = true)
    {
        if (snapToGround && Physics.Raycast(position + Vector3.up * 30f, Vector3.down, out RaycastHit ground, 60f, ~0, QueryTriggerInteraction.Ignore)) position = ground.point;
        var body = player.GetComponent<CharacterController>();
        body.enabled = false;
        player.transform.position = position + Vector3.up * 0.05f;
        body.enabled = true;
        Physics.SyncTransforms();
    }

    // Looks for a spot within reach where the interaction ray reaches the pickup, standing on whatever is below.
    private static void Aim(PlayerLife player, ItemPickup target)
    {
        Collider box = target.GetComponentInChildren<Collider>();
        foreach (float distance in new[] { 1.4f, 1.9f, 2.4f })
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI / 12f;
                Vector3 p = box.bounds.center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * distance;
                if (!Physics.Raycast(p + Vector3.up * 2.5f, Vector3.down, out RaycastHit ground, 6f, ~0, QueryTriggerInteraction.Ignore) || ground.normal.y < 0.6f) continue;
                Teleport(player, ground.point, false);
                view.transform.LookAt(box.bounds.center);
                if (Physics.Raycast(view.transform.position, view.transform.forward, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore) &&
                    hit.collider.transform.IsChildOf(target.transform))
                {
                    report.Add($"Reached the {target.DisplayName} from {distance:0.0} m.");
                    return;
                }
            }
        throw new Exception("No spot within reach sees the " + target.DisplayName);
    }

    private static IEnumerator Capture(string name)
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-storyRender") < 0) yield break;
        ShaderUtil.allowAsyncCompilation = false;
        const int width = 1600, height = 900;
        var texture = new RenderTexture(width, height, 24); texture.Create();
        var overlays = UnityEngine.Object.FindObjectsByType<Canvas>().Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        view.targetTexture = texture;
        foreach (var canvas in overlays) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = view; canvas.planeDistance = view.nearClipPlane + 0.02f; }
        yield return null;
        yield return null;
        Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(view, new RenderPipeline.StandardRequest { destination = texture });
        var old = RenderTexture.active; RenderTexture.active = texture;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/Story_" + name + ".png", image.EncodeToPNG());
        RenderTexture.active = old;
        foreach (var canvas in overlays) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
        view.targetTexture = null;
        UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(image);
        report.Add("Captured Logs/Story_" + name + ".png");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); report.Add(message); }

    private static void Fail(Exception ex)
    {
        SessionState.SetBool(Key, false); Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/TutorialStorySmokeFailure.txt", ex + "\n" + string.Join("\n", report));
        Debug.LogError(ex); EditorApplication.Exit(1);
    }
}
