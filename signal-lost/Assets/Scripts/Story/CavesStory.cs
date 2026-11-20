using System;
using SignalLost.Enemies.Caves;
using SignalLost.Enemies.Desert;
using SignalLost.Inventory;
using SignalLost.NPC;
using SignalLost.Player;
using SignalLost.Progression;
using SignalLost.Scanner;
using SignalLost.UI;
using UnityEngine;

namespace SignalLost.Story
{
    /// <summary>
    /// The Crystal Caves' story (docs/crystal-caves-flow.md), the caves' version of <see cref="DesertStory"/>.
    /// <list type="number">
    /// <item>Cavendish, at camp, gives three jobs in a fixed order. Each: three <see cref="QuestTask"/>s (all armed at
    /// once), then its mini-boss (hidden until the tasks are done), which drops its ship part and a trophy.</item>
    /// <item>The trophy goes on the caves' rack at camp; then a talk with Cavendish hands him the part, which he keeps.
    /// Only parts handed to him count toward the Axolotl's gate (<see cref="AxolotlLair.PartCount"/>).</item>
    /// <item>With all three handed in and the Crystal Disruptor owned, the deep chamber's <see cref="AxolotlLair"/> lets
    /// the fight start. The Axolotl drops the Flight Control Module and its trophy, a gill frond.</item>
    /// <item>The player walks back to camp and sets the gill frond on the rack; then Cavendish wraps up and the boat at
    /// the dock can depart (<see cref="HomeStage"/>).</item>
    /// </list>
    /// Progress is for the current Play session only, apart from the trophies, which the camp remembers.
    /// </summary>
    public sealed class CavesStory : MonoBehaviour, INpcStory
    {
        /// <summary>The Cave Axolotl's trophy (dropped by this story, like the mini-bosses').</summary>
        public const string AxolotlTrophyId = "cave_axolotl_gill";
        /// <summary>Cleared once Cavendish wraps up (the axolotl's trophy is on the rack): the dock's boat sails after it.</summary>
        public const string HomeStage = "CrystalCavesHome";

        public enum Stage { Welcome, Tasks, Hunt, Fight, Loot, Report, Finale, FinaleFight, FinaleLoot, Homecoming, Done }

        /// <summary>One of Cavendish's jobs: three tasks, then a mini-boss (the Beetles are four).</summary>
        [Serializable]
        public sealed class Job
        {
            public string title = "Job";
            [Tooltip("Hidden until the tasks are done (the boss, or the Beetles' band with its members inside).")]
            public GameObject group;
            [Tooltip("Every boss in the fight. The job's boss is beaten when all are dead.")]
            public DesertMiniBoss[] bosses = Array.Empty<DesertMiniBoss>();
            public string bossName = "";
            public string partName = "";
            public string trophyId = "";
            public QuestTask[] tasks = Array.Empty<QuestTask>();
            [TextArea] public string taskObjective = "";
            public string taskLabel = "Done";
            [TextArea] public string huntObjective = "";
            [TextArea] public string fightObjective = "";
            [Tooltip("Cavendish's reminders while the tasks are being done.")]
            public string[] taskReminders = Array.Empty<string>();
        }

        [Header("Cast")]
        [SerializeField] private NpcActor cavendish;
        [Tooltip("The respawn point moves here (by the camp fire) after the first talk with Cavendish. Until then it is at the dock.")]
        [SerializeField] private Transform campRespawn;

        [Header("Jobs")]
        [SerializeField] private Job[] jobs = Array.Empty<Job>();
        [Tooltip("The caves' trophy stands at camp (the scanner points here while a trophy is carried).")]
        [SerializeField] private TrophyStand[] rack = Array.Empty<TrophyStand>();

        [Header("Finale")]
        [SerializeField] private AxolotlLair lair;
        [Tooltip("Where the scanner leads for the finale (the chamber).")]
        [SerializeField] private QuestSignalSource chamberSignal;

        private const string StoryTitle = "Crystal Clear";
        private const string FinaleTitle = "The Deep Chamber";
        private const string HomeTitle = "All Packed";

        private static readonly string[] Welcome =
        {
            "Ah, there you are! Mind your step. The rocks are slippery and I've already lost a biscuit to the sea.",
            "Splendid news: the Flight Control Module is in that cave. My whiskers are quite certain of it.",
            "Less splendid news: something big is curled up in the deepest chamber, sitting on it like an egg.",
            "And three smaller bits of your ship have wandered off with the locals: a signal amplifier, an antenna array and a power cell.",
            "Bring them to me one at a time, and a little something from each thief for the trophy stands. I'll keep the parts safe.",
            "Start with the Beetles. A band, apparently, up on the ledge at the far west of the cave. They've dropped concert tickets all over the place. Follow the litter."
        };
        private static readonly string[][] Reports =
        {
            new[]
            {
                "The signal amplifier! And a wing case. Very shiny. Hand the amplifier here; I'll keep it safe.",
                "Next: the Shard Mole. It's been tunnelling about down on the deep level and knocking the cave's crystals loose.",
                "There's a ramp down just past where the Beetles were playing. Or you could simply jump. I wouldn't. But you could.",
                "Put the loose crystals back in their sockets as you go. The digging should lead you right to it."
            },
            new[]
            {
                "The antenna array! A bit muddy, but aren't we all. And a hide! I shall use it as a rug. Safe keeping, as ever.",
                "Last of the three: the Geode Bear. Big, sleepy, made of rock and very grumpy about it.",
                "It's sitting on your power cell by the east wall of the deep level, and it drained the old power nodes on the way.",
                "Get those nodes running again and it'll come stomping. And if it yawns, look away. Trust me."
            },
            new[]
            {
                "The power cell! That's all three. I'll hang on to them. They're quite safe in my pockets. Mostly.",
                "Now for the big one. At the very back of the cave is a chamber, and in it the Cave Axolotl, sitting on your Flight Control Module.",
                "It hides as a crystal. A perfectly ordinary-looking crystal. Your scanner won't see through it without the Crystal Disruptor.",
                "Eugene sells one, if you've the scrap. Then walk in, and go for the gills. That's where it's soft."
            }
        };
        private static readonly string[] WrapUp =
        {
            "You're back, and in one piece! Roughly one piece. Is that the Flight Control Module?",
            "Marvellous. That's everything the caves had of ours: three parts, a module and a rack full of souvenirs.",
            "We're done with the caves. The boat's waiting at the dock whenever you're ready.",
            "There are still bits of your ship out there somewhere. Let's go and find them."
        };

        private Stage stage;
        private int job;
        private int partsHandedIn;
        private bool trophyPlaced, partRecovered;
        private bool moduleTaken;
        private int partsAtStart;
        private int nextReminder;
        private bool hadDisruptor;
        private ScannerController scanner;

        public Stage Current => stage;
        public int JobIndex => job;
        public int PartsHandedIn => partsHandedIn;
        public event Action<Stage> StageChanged;

        private Job CurrentJob => job < jobs.Length ? jobs[job] : null;
        private bool HasDisruptor => scanner != null && scanner.DisruptsCamouflage;
        // Read from the camp and inventory, so it holds whichever order the loot is picked up and placed in.
        private static bool GillHome => CampSave.IsPlaced(AxolotlTrophyId);
        private static bool GillCarried => PlayerInventory.Current != null && PlayerInventory.Current.HasKeyItem(AxolotlTrophyId);

        private void Awake()
        {
            foreach (Job item in jobs)
            {
                if (item.group != null) item.group.SetActive(false); // each appears once its tasks are done
                foreach (QuestTask task in item.tasks) if (task != null) task.Armed = false;
            }
            if (lair != null) lair.PartCount = () => partsHandedIn;
            if (chamberSignal != null) chamberSignal.enabled = false;
        }

        private void OnEnable()
        {
            foreach (Job item in jobs)
                foreach (QuestTask task in item.tasks)
                    if (task != null) task.Done += OnTaskDone;
            DesertMiniBoss.Defeated += OnBossDefeated;
            TrophyPickup.PickedUp += OnTrophyPickedUp;
            TrophyStand.Placed += OnTrophyPlaced;
            EngineCorePickup.Recovered += OnModuleRecovered;
        }

        private void OnDisable()
        {
            foreach (Job item in jobs)
                foreach (QuestTask task in item.tasks)
                    if (task != null) task.Done -= OnTaskDone;
            DesertMiniBoss.Defeated -= OnBossDefeated;
            TrophyPickup.PickedUp -= OnTrophyPickedUp;
            TrophyStand.Placed -= OnTrophyPlaced;
            EngineCorePickup.Recovered -= OnModuleRecovered;
        }

        private void Start()
        {
            var controller = FindAnyObjectByType<FirstPersonController>();
            if (controller != null) scanner = controller.GetComponentInChildren<ScannerController>(true);
            if (scanner != null) scanner.Silenced = true; // nothing to find until Cavendish has given the first job
            foreach (TrophyStand stand in FindObjectsByType<TrophyStand>()) Signal(stand, false);
            SetStage(Stage.Welcome);
            Signal(cavendish, true);
            Quest(StoryTitle, "Talk to Cavendish at his camp by the dock.");
        }

        private void Update()
        {
            switch (stage)
            {
                case Stage.Hunt when AnyEngaged(CurrentJob):
                    EnterFight();
                    break;
                case Stage.Fight when !AnyEngaged(CurrentJob) && !AllDead(CurrentJob):
                    EnterHunt(false);
                    break;
                case Stage.Loot when PartRecovered != partRecovered:
                    partRecovered = PartRecovered; // picked up by walking over it
                    UpdateLoot();
                    break;
                case Stage.Finale when lair != null && lair.InCutsceneOrFight:
                    SetStage(Stage.FinaleFight);
                    if (chamberSignal != null) chamberSignal.enabled = false; // the scanner points only at the Axolotl now
                    Quest(FinaleTitle, "Defeat the Cave Axolotl. Roll it into a wall to expose its gills. When it hides, raise your scanner near the crystals to find it.");
                    break;
                case Stage.Finale when HasDisruptor != hadDisruptor:
                    UpdateFinale();
                    break;
                case Stage.FinaleFight when lair != null && !lair.InCutsceneOrFight && !lair.Cleared:
                    EnterFinale(); // it reset: the player died or left the chamber
                    break;
            }
        }

        // ---------- NPC conversations (INpcStory) ----------

        public bool TalkTo(NpcActor npc, PlayerInteractor talker)
        {
            if (npc != cavendish) return false;
            NpcDialogue bubble = cavendish.Dialogue;
            Job current = CurrentJob;
            switch (stage)
            {
                case Stage.Welcome:
                    Converse(StoryTitle, Welcome, () => BeginJob(0));
                    return true;
                case Stage.Tasks:
                    return Remind(bubble, current.taskReminders.Length > 0 ? current.taskReminders : new[] { current.taskObjective });
                case Stage.Hunt:
                case Stage.Fight:
                    return Remind(bubble, new[] { $"{current.bossName} {Is(current)} waiting for you in there. Off you go!" });
                case Stage.Loot:
                    return Remind(bubble, !partRecovered
                        ? new[] { $"Did {current.bossName} drop the {current.partName.ToLowerInvariant()}? Don't come back without it." }
                        : trophyPlaced
                            ? new[] { "Lovely. Now, what have you got for me?" }
                            : HasTrophy
                                ? new[] { "Put it on the new rack by the fire, go on. Pride of place." }
                                : new[] { $"Did {current.bossName} leave anything behind? Bring me something to remember it by." });
                case Stage.Report:
                    Converse(current.title, Reports[Mathf.Min(job, Reports.Length - 1)], HandIn);
                    return true;
                case Stage.Finale:
                case Stage.FinaleFight:
                    return Remind(bubble, HasDisruptor
                        ? new[] { "The chamber's right at the back of the cave. Go for the gills.", "If a crystal looks at you funny, raise your scanner." }
                        : new[] { "Eugene sells the Crystal Disruptor. Without it, that axolotl is just another crystal.", "Scrap for Eugene, Disruptor for you, axolotl for nobody." });
                case Stage.FinaleLoot:
                    return Remind(bubble, new[] { moduleTaken
                        ? "The module, lovely! But didn't it leave anything for the rack? Go and look."
                        : "You beat it? Then where's the module? Go and fetch it!" });
                case Stage.Homecoming when GillHome:
                    Converse(HomeTitle, WrapUp, EnterDone);
                    return true;
                case Stage.Homecoming:
                    return Remind(bubble, new[] { "Is that a gill? Goodness. Put it on the rack by the fire first, then we'll talk." });
                default:
                    return false; // his own small talk
            }
        }

        private bool Remind(NpcDialogue bubble, string[] options)
        {
            if (bubble == null) return false;
            if (bubble.AdvanceManual()) return true;
            bubble.SpeakManual(options[nextReminder++ % options.Length]);
            return true;
        }

        private void Converse(string title, string[] lines, Action done) =>
            DialogueUi.Instance.BeginConversation(cavendish.transform, "Cavendish", title, lines, done);

        // ---------- The jobs ----------

        private void BeginJob(int index)
        {
            Signal(cavendish, false);
            if (index == 0 && scanner != null) scanner.Silenced = false;
            if (index == 0 && campRespawn != null && PlayerSpawnPoint.Current != null)
                PlayerSpawnPoint.Current.transform.SetPositionAndRotation(campRespawn.position, campRespawn.rotation);
            job = index;
            trophyPlaced = partRecovered = false;
            partsAtStart = ShipPartDrop.CountFor(ShardMole.Stage);
            SetStage(Stage.Tasks);
            foreach (QuestTask task in jobs[job].tasks) if (task != null) task.Armed = true;
            UpdateTasks();
        }

        private int TasksDone
        {
            get
            {
                int done = 0;
                foreach (QuestTask task in jobs[job].tasks) if (task != null && task.IsDone) done++;
                return done;
            }
        }

        private void UpdateTasks()
        {
            Job current = jobs[job];
            Quest(current.title, $"{current.taskObjective}\n{current.taskLabel}: {TasksDone} / {current.tasks.Length}");
        }

        private void OnTaskDone(QuestTask task)
        {
            if (stage != Stage.Tasks || Array.IndexOf(jobs[job].tasks, task) < 0) return;
            if (!string.IsNullOrEmpty(task.DoneMessage)) MiniBossHud.Message(task.DoneMessage, new Color(1f, 0.9f, 0.6f), 2.5f);
            if (TasksDone < jobs[job].tasks.Length) { UpdateTasks(); return; }
            EnterHunt(true);
        }

        private void EnterHunt(bool appear)
        {
            Job current = jobs[job];
            SetStage(Stage.Hunt);
            if (appear && current.group != null)
            {
                current.group.SetActive(true);
                MiniBossHud.Message($"{current.bossName.ToUpperInvariant()} {Is(current).ToUpperInvariant()} WAITING IN THE CAVE", new Color(0.7f, 0.85f, 1f), 3f);
            }
            SignalBosses(current, true);
            Quest(current.title, current.huntObjective);
        }

        private void EnterFight()
        {
            SetStage(Stage.Fight);
            SignalBosses(CurrentJob, false);
            Quest(CurrentJob.title, CurrentJob.fightObjective);
        }

        private void OnBossDefeated(DesertMiniBoss boss)
        {
            if (boss is CaveAxolotl)
            {
                if (stage == Stage.FinaleFight) OnAxolotlDefeated(boss);
                return;
            }
            Job current = CurrentJob;
            if (current == null || (stage != Stage.Fight && stage != Stage.Hunt) || Array.IndexOf(current.bosses, boss) < 0 || !AllDead(current)) return;
            SetStage(Stage.Loot);
            SignalBosses(current, false);
            if (CampSave.IsPlaced(current.trophyId))
            {
                trophyPlaced = true; // already on the rack from an earlier session
            }
            else
            {
                Vector3 at = GroundBelow(boss.transform.position + boss.transform.right * 2.5f) + Vector3.up * 0.2f;
                TrophyPickup.Create(current.trophyId, at).gameObject.AddComponent<QuestSignalSource>();
            }
            UpdateLoot();
        }

        private bool HasTrophy => CurrentJob != null && PlayerInventory.Current != null && PlayerInventory.Current.HasKeyItem(CurrentJob.trophyId);
        private bool PartRecovered => ShipPartDrop.CountFor(ShardMole.Stage) > partsAtStart;

        private void UpdateLoot()
        {
            Job current = CurrentJob;
            if (trophyPlaced && partRecovered)
            {
                foreach (TrophyStand stand in rack) Signal(stand, false);
                SetStage(Stage.Report);
                Signal(cavendish, true);
                Quest(current.title, $"Take the {current.partName.ToLowerInvariant()} to Cavendish.");
                return;
            }
            string name = Trophy.Find(current.trophyId)?.Name ?? "trophy";
            string trophyLine = trophyPlaced ? $"[x] {name} on the rack"
                : HasTrophy ? $"[  ] Set the {name.ToLowerInvariant()} on the rack at camp"
                : $"[  ] Pick up the {name.ToLowerInvariant()}";
            string partLine = partRecovered ? $"[x] {current.partName}" : $"[  ] Grab the {current.partName.ToLowerInvariant()}";
            Quest(current.title, $"{current.bossName} {Is(current)} beaten! Take the loot home.\n{trophyLine}\n{partLine}");
            bool carrying = HasTrophy && !trophyPlaced;
            foreach (TrophyStand stand in rack) Signal(stand, carrying && stand.TrophyId == null);
        }

        private void OnTrophyPickedUp(string id)
        {
            if (stage == Stage.Loot && CurrentJob != null && id == CurrentJob.trophyId) UpdateLoot();
            else if (stage == Stage.FinaleLoot && id == AxolotlTrophyId) UpdateFinaleLoot();
        }

        private void OnTrophyPlaced(string id)
        {
            if (id == AxolotlTrophyId)
            {
                if (stage == Stage.FinaleLoot) UpdateFinaleLoot();
                else if (stage == Stage.Homecoming) UpdateHomecoming();
                return;
            }
            if (stage != Stage.Loot || CurrentJob == null || id != CurrentJob.trophyId) return;
            trophyPlaced = true;
            UpdateLoot();
        }

        // Cavendish takes the part and keeps it; only these count at the Axolotl's chamber.
        private void HandIn()
        {
            partsHandedIn++;
            MiniBossHud.Message($"Gave Cavendish the {CurrentJob.partName.ToLowerInvariant()} ({partsHandedIn}/{jobs.Length})", new Color(1f, 0.85f, 0.35f), 3f);
            if (job + 1 < jobs.Length) BeginJob(job + 1);
            else EnterFinale();
        }

        // ---------- The Cave Axolotl ----------

        private void EnterFinale()
        {
            Signal(cavendish, false);
            SetStage(Stage.Finale);
            if (chamberSignal != null) chamberSignal.enabled = true;
            UpdateFinale();
        }

        private void UpdateFinale()
        {
            hadDisruptor = HasDisruptor;
            Quest(FinaleTitle, hadDisruptor
                ? "Go to the deep chamber at the back of the cave and face the Cave Axolotl."
                : "Buy the Crystal Disruptor from Eugene at camp, then go to the deep chamber at the back of the cave.");
        }

        private void OnAxolotlDefeated(DesertMiniBoss boss)
        {
            SetStage(Stage.FinaleLoot);
            moduleTaken = StageProgress.HasMainPart(CaveAxolotl.MainPart);
            if (!GillHome && !GillCarried)
            {
                Vector3 at = GroundBelow(boss.transform.position - boss.transform.right * 3f) + Vector3.up * 0.2f; // the module drops in front
                TrophyPickup.Create(AxolotlTrophyId, at).gameObject.AddComponent<QuestSignalSource>();
            }
            UpdateFinaleLoot();
        }

        private void OnModuleRecovered(string part)
        {
            if (part != CaveAxolotl.MainPart) return;
            moduleTaken = true;
            if (stage == Stage.FinaleLoot) UpdateFinaleLoot();
        }

        private void UpdateFinaleLoot()
        {
            bool gillTaken = GillHome || GillCarried;
            if (moduleTaken && gillTaken)
            {
                SetStage(Stage.Homecoming);
                Signal(cavendish, true);
                UpdateHomecoming();
                return;
            }
            Quest(FinaleTitle, "The Cave Axolotl is beaten! Grab the loot.\n" +
                (moduleTaken ? "[x] Flight Control Module" : "[  ] Flight Control Module") + "\n" +
                (gillTaken ? "[x] Cave Axolotl Gill Frond" : "[  ] Cave Axolotl Gill Frond"));
        }

        private void UpdateHomecoming()
        {
            foreach (TrophyStand stand in rack) Signal(stand, GillCarried && !GillHome && stand.TrophyId == null);
            Quest(HomeTitle, "Walk back out of the cave to camp.\n" +
                (GillHome ? "[x] Gill frond on the rack" : "[  ] Set the gill frond on the rack at camp") + "\n" +
                "[  ] Show Cavendish the Flight Control Module");
        }

        private void EnterDone()
        {
            SetStage(Stage.Done);
            Signal(cavendish, false);
            foreach (TrophyStand stand in rack) Signal(stand, false);
            StageProgress.ClearStage(HomeStage, null);
            Quest(HomeTitle, "The Crystal Caves are cleared! Board the boat at the dock to keep searching for the rest of the ship.", true);
        }

        // ---------- Helpers ----------

        // "The Beetles are", "The Shard Mole is".
        private static string Is(Job item) => item.bosses.Length > 1 ? "are" : "is";

        private static bool AnyEngaged(Job item)
        {
            if (item == null) return false;
            foreach (DesertMiniBoss boss in item.bosses) if (boss != null && boss.Engaged) return true;
            return false;
        }

        private static bool AllDead(Job item)
        {
            if (item == null) return false;
            foreach (DesertMiniBoss boss in item.bosses) if (boss != null && !boss.IsDead) return false;
            return true;
        }

        private static void SignalBosses(Job item, bool on)
        {
            foreach (DesertMiniBoss boss in item.bosses) Signal(boss, on && !boss.IsDead);
        }

        private void SetStage(Stage next)
        {
            stage = next;
            StageChanged?.Invoke(next);
        }

        private static void Quest(string title, string objective, bool complete = false) =>
            GameplayOverlay.Instance.SetQuest(title, objective, complete);

        private static void Signal(Component owner, bool on)
        {
            if (owner != null && owner.TryGetComponent(out QuestSignalSource signal)) signal.enabled = on;
        }

        private static Vector3 GroundBelow(Vector3 point)
        {
            RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * 3f, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 ground = point;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider is CharacterController || hit.collider.GetComponentInParent<SignalLost.Combat.Health>() != null) continue;
                if (hit.distance >= best) continue;
                best = hit.distance;
                ground = hit.point;
            }
            return ground;
        }

        // ---------- For tests ----------

        /// <summary>Skips Cavendish's welcome and starts the first job (tests and debugging).</summary>
        public void SkipWelcome()
        {
            if (stage == Stage.Welcome) BeginJob(0);
        }

        /// <summary>Hands in the part as if Cavendish had been talked to (tests and debugging).</summary>
        public void SkipReport()
        {
            if (stage == Stage.Report) HandIn();
        }
    }
}
