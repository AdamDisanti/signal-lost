using System;
using SignalLost.Enemies.Desert;
using SignalLost.Inventory;
using SignalLost.NPC;
using SignalLost.Player;
using SignalLost.Progression;
using SignalLost.Scanner;
using SignalLost.UI;
using SignalLost.World;
using UnityEngine;

namespace SignalLost.Story
{
    /// <summary>
    /// The Desert Dunes' story. Being revamped act by act (docs/desert-revamp.md, 11-22); built so far: <b>Act 1,
    /// Breakfast</b>. Cavendish is starving and sends the player round the desert's nests for four big speckled eggs
    /// ("abandoned, I'm sure"); Doris at Mirage Springs knows exactly whose they are. Back at camp they cook an omelet
    /// together (<see cref="CookingStation"/>), and mid-breakfast Briarwing snatches the player off their seat and carries
    /// them to its Roost (<see cref="WyvernSnatch"/>): the fight. Then Act 2 and 3 still run as the old jobs below (a task,
    /// then the boss), until they are rebuilt.
    ///
    /// The original flow (11-08): Cavendish's three jobs against the Chop Shop, then the Core Worm.
    /// <list type="number">
    /// <item>Cavendish (at his camp by the arrival dock) explains that three thieves took the parts bolted round the
    /// Engine Core, and that he has put up a rack for a trophy from each.</item>
    /// <item>Each job is a small task (three <see cref="QuestTask"/>s: pick up Old Buzzard's feathers, put out
    /// Burnout's fires, seal the leaks round Sludge's sump), then its mini-boss in its own themed arena. The boss only
    /// appears once the task is done. Beaten, it drops its ship part and a trophy; the trophy goes on the rack, then
    /// Cavendish takes the part "for safe keeping" and gives the next job.</item>
    /// <item>Setting the last of the three trophies on the rack springs the Core Worm: it bursts out beside the camp,
    /// swallows the player and drags them into its lair under the desert (<see cref="DesertStageExit"/> armed at the
    /// rack, <see cref="WormLair"/>). Dying sends the player back to the surface with the worm lurking at the rack again.</item>
    /// <item>Beaten, it leaves the Engine Core and a fang. Once both are picked up the cavern roof caves in, and the
    /// player climbs the rubble into daylight in the worm's old sand bowl, near the north dock.</item>
    /// <item>Back at camp: the fang goes on a stand and Cavendish wraps up. The boat on to the next island is open.</item>
    /// </list>
    /// Like the tutorial's story, NPCs ask this what to say (<see cref="INpcStory"/>), quest signals steer the scanner
    /// and the tracker shows the current objective. Progress is for the current Play session only (there is no save
    /// system yet), apart from the trophies, which the camp remembers.
    /// </summary>
    public sealed class DesertStory : MonoBehaviour, INpcStory
    {
        public enum Stage { Welcome, Tasks, Hunt, Fight, Loot, Report, WormWaiting, WormFight, WormLoot, Collapse, Climb, Homecoming, Done,
            Eggs, ReportEggs, Cooking, Breakfast }

        /// <summary>One of Cavendish's jobs: a task, then a mini-boss.</summary>
        [Serializable]
        public sealed class Job
        {
            public string title = "Job";
            public DesertMiniBoss boss;
            public string trophyId = "";
            [Tooltip("Where the boss's arena is, for the objective (e.g. \"the Wreck Field, west of camp\").")]
            public string where = "";
            public QuestTask[] tasks = Array.Empty<QuestTask>();
            [Tooltip("The objective while the tasks are being done.")]
            [TextArea] public string taskObjective = "";
            [Tooltip("Shown with the task count, e.g. \"Feathers\".")]
            public string taskLabel = "Done";
            [Tooltip("The objective once the boss is out.")]
            [TextArea] public string huntObjective = "";
            [Tooltip("The objective during the fight.")]
            [TextArea] public string fightObjective = "";
        }

        [Header("Cast")]
        [SerializeField] private NpcActor cavendish;
        [SerializeField] private PlayerLife player;

        [Header("Act 1: Breakfast")]
        [Tooltip("The four nests' eggs (E to take one).")]
        [SerializeField] private QuestTask[] eggs = Array.Empty<QuestTask>();
        [SerializeField] private NpcActor doris;
        [SerializeField] private CookingStation stove;
        [SerializeField] private WyvernSnatch snatch;

        [Header("The Chop Shop")]
        [SerializeField] private Job[] jobs = Array.Empty<Job>();
        [Tooltip("The camp's rack for the three trophies (the scanner points here while one is carried).")]
        [SerializeField] private TrophyStand[] rack = Array.Empty<TrophyStand>();

        [Header("The Core Worm")]
        [SerializeField] private DesertStageExit wormAmbush;
        [SerializeField] private WormLair lair;
        [SerializeField] private string wormTrophyId = "core_worm_fang";

        private const string WormTitle = "Something Big";
        private const string HomeTitle = "Daylight";

        private static readonly string[] Welcome =
        {
            "Ah, mechanic! Found your feet? Splendid. I had a little wander while you were sleeping off the crossing.",
            "The Engine Core's out here somewhere, and bits of the ship with it. The local wildlife has been... collecting them.",
            "But first things first. I am STARVING. A capybara cannot think on an empty stomach. Science.",
            "I've seen nests all over the place. Big ones. With big speckled eggs in them. Abandoned, I'm sure.",
            "Bring me four and I'll make us an omelet. There's one up on the hoodoos out west, one by the springs, one on that great stone arch...",
            "...and one in a dead tree along the dry riverbed. Your scanner will find them. Chop chop!"
        };
        private static readonly string[] EggReport =
        {
            "Four eggs! Look at the size of them. Whatever laid these must be enormous. And very far away. Abandoned, as I said.",
            "Right. I'll do the talking, you do the cooking. I've got a pan on the fire.",
            "Crack, whisk, flip. How hard can it be? Come on, over to the fire."
        };
        private static readonly string[] DorisEggLines =
        {
            "Oh, those aren't mine, dear. Mine are the small ones. Those are... well. Enjoy your breakfast.",
            "Mm. Big egg. Big nest. Big... never mind. Off you go.",
            "You'll want to eat that quickly, I think."
        };
        private static readonly string[] DorisLines =
        {
            "Welcome to Mirage Springs, dear. Towels are complimentary. The water is not drinkable. The view is.",
            "I've lived by this spring for ninety years. Nothing ever happens. Nothing nice, anyway.",
            "Don't mind the hoodoos. They creak at night. It's the wind. Mostly."
        };
        private static readonly string[] DorisAfterLines =
        {
            "I did try to tell you, dear. Big egg, big nest, big mother.",
            "Briarwing's not one to forgive a missing egg. Still, you're alive. That's a good spa day.",
            "Cucumber slice? For the nerves."
        };
        private static readonly string[][] Reports =
        {
            new[]
            {
                "You're alive! I saw the whole thing. Well, the first bit. Then you were a dot.",
                "Its eggs? Briarwing's? I'd never have guessed. Ostrich, I'd have said. Doris did look at me funny.",
                "The tail feathers! And a wing panel off your ship. It had woven it into its wing. Hand that here; I'll keep it safe.",
                "Next: Cindermaw. An ash drake that swallowed your rocket thruster and grew round it.",
                "It's been tearing round the Mesa Pass, east of here, and setting the dunes alight as it goes.",
                "Put out the fires it left on its way, before the whole desert goes up, then go and have a word with it."
            },
            new[]
            {
                "That crest! It goes beautifully with the feathers. And your thruster, only slightly singed. Safe keeping.",
                "Last of the Chop Shop: Pitchclaw. A scorpion of living rock with your fuel tank sunk in its back.",
                "It's been draining a buried hull north-west of here into a filthy great sump, and the pipes are leaking everywhere.",
                "Seal the three leaks round the sump. That'll bring it out. And mind its claws: when they spark, get out of the fuel."
            }
        };
        private static readonly string[] WrapUp =
        {
            "You're alive! The ground swallowed you whole and I thought, well, there goes another mechanic.",
            "Is that the Engine Core? Marvellous. Hand it over... for safe keeping.",
            "And a fang on the stand! The Chop Shop and their big friend, all in one trip. I'm rather proud of you.",
            "The boat's waiting at the north dock whenever you're ready. Next stop: the Fog Forest."
        };

        private Stage stage;
        private int job;
        private bool trophyPlaced, partHandled;
        private bool coreTaken, fangTaken, fangPlaced;
        private TrophyPickup droppedTrophy;
        private int nextReminder, nextDoris;
        private bool breakfastDone;
        private int partsAtStart;

        public Stage Current => stage;
        public int JobIndex => job;
        public Job CurrentJob => job < jobs.Length ? jobs[job] : null;
        public event Action<Stage> StageChanged;

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<PlayerLife>();
            foreach (Job item in jobs)
            {
                if (item.boss != null) item.boss.gameObject.SetActive(false); // each appears once its task is done
                foreach (QuestTask task in item.tasks) if (task != null) task.Armed = false;
            }
            foreach (QuestTask egg in eggs) if (egg != null) egg.Armed = false;
        }

        private void OnEnable()
        {
            foreach (Job item in jobs)
                foreach (QuestTask task in item.tasks)
                    if (task != null) task.Done += OnTaskDone;
            foreach (QuestTask egg in eggs) if (egg != null) egg.Done += OnEggTaken;
            if (stove != null) stove.Cooked += OnCooked;
            if (snatch != null) snatch.Landed += OnSnatchLanded;
            DesertMiniBoss.Defeated += OnBossDefeated;
            TrophyPickup.PickedUp += OnTrophyPickedUp;
            TrophyStand.Placed += OnTrophyPlaced;
            EngineCorePickup.Recovered += OnCoreRecovered;
            if (wormAmbush != null)
            {
                wormAmbush.FightStarted += OnWormFight;
                wormAmbush.FightLost += OnWormLost;
                if (wormAmbush.Worm != null) wormAmbush.Worm.Defeated += OnWormDefeated;
            }
            if (lair != null)
            {
                lair.Collapsed += OnLairOpen;
                lair.Emerged += OnEmerged;
            }
        }

        private void OnDisable()
        {
            foreach (Job item in jobs)
                foreach (QuestTask task in item.tasks)
                    if (task != null) task.Done -= OnTaskDone;
            foreach (QuestTask egg in eggs) if (egg != null) egg.Done -= OnEggTaken;
            if (stove != null) stove.Cooked -= OnCooked;
            if (snatch != null) snatch.Landed -= OnSnatchLanded;
            DesertMiniBoss.Defeated -= OnBossDefeated;
            TrophyPickup.PickedUp -= OnTrophyPickedUp;
            TrophyStand.Placed -= OnTrophyPlaced;
            EngineCorePickup.Recovered -= OnCoreRecovered;
            if (wormAmbush != null)
            {
                wormAmbush.FightStarted -= OnWormFight;
                wormAmbush.FightLost -= OnWormLost;
                if (wormAmbush.Worm != null) wormAmbush.Worm.Defeated -= OnWormDefeated;
            }
            if (lair != null)
            {
                lair.Collapsed -= OnLairOpen;
                lair.Emerged -= OnEmerged;
            }
        }

        private void Start()
        {
            foreach (TrophyStand stand in FindObjectsByType<TrophyStand>()) Signal(stand, false);
            SetStage(Stage.Welcome);
            Signal(cavendish, true);
            Quest("Breakfast", "Talk to Cavendish at his camp by the dock.");
        }

        private void Update()
        {
            Job current = CurrentJob;
            if (current == null || current.boss == null) return;
            if (stage == Stage.Hunt && current.boss.Engaged) EnterFight();
            else if (stage == Stage.Fight && !current.boss.Engaged && !current.boss.IsDead) EnterHunt(false);
            else if (stage == Stage.Loot && PartRecovered != partHandled)
            {
                partHandled = PartRecovered; // the part is picked up by walking over it
                UpdateLoot();
            }
        }

        // ---------- NPC conversations (INpcStory) ----------

        public bool TalkTo(NpcActor npc, PlayerInteractor talker)
        {
            if (npc != null && npc == doris) return TalkToDoris();
            if (npc != cavendish) return false;
            NpcDialogue bubble = cavendish.Dialogue;
            Job current = CurrentJob;
            switch (stage)
            {
                case Stage.Welcome:
                    Converse("Breakfast Run", Welcome, BeginEggs);
                    return true;
                case Stage.Eggs:
                    return Remind(bubble, new[]
                    {
                        "Four eggs, mechanic. My stomach is making noises a capybara should never make.",
                        "Hoodoos out west, the springs, the big stone arch over the canyon, and a dead tree in the dry riverbed.",
                        "Use your scanner. It's very good at eggs. I assume."
                    });
                case Stage.ReportEggs:
                    Converse("Breakfast Run", EggReport, BeginCooking);
                    return true;
                case Stage.Cooking:
                    return Remind(bubble, new[] { "The pan's on the fire. Go on, chef. I'll supervise. Loudly." });
                case Stage.Breakfast:
                    return Remind(bubble, new[] { "Mmf. Don't talk with your mouth full. Or with mine." });
                case Stage.Tasks:
                    return Remind(bubble, job == 0
                        ? new[] { "West, mechanic. Follow the feathers. Big grey ones; you can't miss them." }
                        : job == 1
                            ? new[] { "East, toward the Mesa Pass. Follow the smoke and smother those fires." }
                            : new[] { "North-west, to the sump. Seal those leaks and Pitchclaw will come to you." });
                case Stage.Hunt:
                case Stage.Fight:
                    return Remind(bubble, new[] { $"{current.boss.DisplayName} is out there in {current.where}. Off you go!" });
                case Stage.Loot:
                    return Remind(bubble, trophyPlaced
                        ? new[] { "Lovely. Now, what have you got for me?" }
                        : HasTrophy
                            ? new[] { "Put it on the new rack by the fire, go on. Pride of place." }
                            : new[] { $"Did {current.boss.DisplayName} leave anything behind? Bring me something to remember it by." });
                case Stage.Report:
                    Converse(current.title, Reports[Mathf.Min(job, Reports.Length - 1)], () => BeginJob(job + 1));
                    return true;
                case Stage.WormWaiting:
                    return Remind(bubble, new[] { "Something's down there, under the rack. I can feel it in my whiskers. Go carefully." });
                case Stage.Homecoming when fangPlaced:
                    Converse("Safe Keeping", WrapUp, EnterDone);
                    return true;
                case Stage.Homecoming:
                    return Remind(bubble, new[] { "You're back! What's that you've got? A fang? Put it on a stand, quick, before I faint." });
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

        // ---------- Act 1: Breakfast ----------

        private bool TalkToDoris()
        {
            NpcDialogue bubble = doris.Dialogue;
            if (bubble == null) return false;
            if (bubble.AdvanceManual()) return true;
            string[] lines = breakfastDone ? DorisAfterLines : stage == Stage.Eggs ? DorisEggLines : DorisLines;
            bubble.SpeakManual(lines[nextDoris++ % lines.Length]);
            return true;
        }

        private void BeginEggs()
        {
            Signal(cavendish, false);
            SetStage(Stage.Eggs);
            foreach (QuestTask egg in eggs) if (egg != null) { egg.Armed = !egg.IsDone; Signal(egg, !egg.IsDone); }
            UpdateEggs();
        }

        private int EggsTaken
        {
            get
            {
                int taken = 0;
                foreach (QuestTask egg in eggs) if (egg != null && egg.IsDone) taken++;
                return taken;
            }
        }

        private void UpdateEggs() =>
            Quest("Breakfast Run", "Cavendish wants four big speckled eggs from the nests he's seen. \"Abandoned, I'm sure.\"\n" +
                $"Eggs: {EggsTaken} / {eggs.Length}");

        private void OnEggTaken(QuestTask egg)
        {
            Signal(egg, false);
            if (stage != Stage.Eggs) return;
            MiniBossHud.Message($"EGG {EggsTaken} / {eggs.Length}", new Color(1f, 0.92f, 0.7f), 2f);
            // Doris sees you take one near the springs.
            if (doris != null && doris.Dialogue != null && Vector3.Distance(doris.transform.position, egg.transform.position) < 40f)
                doris.Dialogue.Speak(DorisEggLines[0]);
            if (EggsTaken < eggs.Length) { UpdateEggs(); return; }
            SetStage(Stage.ReportEggs);
            Signal(cavendish, true);
            Quest("Breakfast Run", "Four eggs! Take them back to Cavendish at camp.");
        }

        private void BeginCooking()
        {
            Signal(cavendish, false);
            SetStage(Stage.Cooking);
            if (stove != null)
            {
                stove.Ready = true;
                Signal(stove, true);
            }
            Quest("Omelet du Jour", "Cook Cavendish's omelet at the camp fire. Crack, whisk, flip.");
        }

        private void OnCooked(int score)
        {
            if (stage != Stage.Cooking) return;
            Signal(stove, false);
            SetStage(Stage.Breakfast);
            StartCoroutine(Breakfast(score));
        }

        // Cavendish rates it, you eat... and Briarwing comes for its eggs.
        private System.Collections.IEnumerator Breakfast(int score)
        {
            string verdict = score >= 9 ? $"A {score}. Magnificent. I may weep." :
                score >= 6 ? $"A {score}. The shell is a bold choice, but it works." :
                score >= 3 ? $"A {score}. It's... rustic. Like a campfire. Literally, some of it is campfire." :
                $"A {score}. I've eaten worse. Not recently. Not willingly.";
            Quest("Omelet du Jour", $"Breakfast is served. Cavendish gives it {score} / 10.");
            NpcDialogue bubble = cavendish != null ? cavendish.Dialogue : null;
            if (bubble != null) bubble.Speak(4f, verdict, "Mmf. Mmmf. Eat up, mechanic. Big day.", "...Mechanic. Don't look up.");
            // Space (Jump) skips ahead to the snatch (which can be skipped in turn).
            DialogueUi.Instance.Subtitle("", $"{SignalLost.Core.KeyBindings.Display("jump")} to skip", 3f);
            bool skipped = false;
            System.Collections.IEnumerator Wait(float seconds)
            {
                for (float t = 0f; t < seconds && !skipped; t += Time.deltaTime)
                {
                    if (t > 0.1f && SignalLost.Core.KeyBindings.WasPressed("jump")) skipped = true;
                    yield return null;
                }
            }
            for (int bite = 0; bite < 3 && !skipped; bite++)
            {
                yield return Wait(1.5f);
                if (skipped) break;
                SignalLost.Audio.Sfx.Play(bite == 2 ? "flask_burp" : "flask_glug", 0.6f, 1.3f);
                MiniBossHud.Message(bite == 0 ? "*nom*" : bite == 1 ? "*nom nom*" : "*burp*", new Color(1f, 0.9f, 0.7f), 1.2f);
            }
            yield return Wait(3.5f);
            yield return null; // so the same press doesn't also skip the snatch
            if (snatch != null && player != null) snatch.Begin(player.gameObject);
            else OnSnatchLanded();
        }

        private void OnSnatchLanded()
        {
            if (stage != Stage.Breakfast) return;
            breakfastDone = true;
            job = 0;
            trophyPlaced = partHandled = false;
            droppedTrophy = null;
            partsAtStart = ShipPartDrop.CountFor(StageProgress.Desert);
            EnterHunt(false);
        }

        // ---------- The Chop Shop ----------

        private void BeginJob(int index)
        {
            Signal(cavendish, false);
            if (index >= jobs.Length) return;
            job = index;
            trophyPlaced = partHandled = false;
            droppedTrophy = null;
            partsAtStart = ShipPartDrop.CountFor(StageProgress.Desert);
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

        // The boss turns up once its task is done (in a puff of sand, if anyone is watching).
        private void EnterHunt(bool appear)
        {
            Job current = jobs[job];
            SetStage(Stage.Hunt);
            if (appear && current.boss != null)
            {
                current.boss.gameObject.SetActive(true);
                Vector3 at = current.boss.transform.position;
                SignalLost.Effects.ParticleBurst.Spawn(SignalLost.Effects.ParticleBurst.Kind.Sand, at + Vector3.up, Vector3.up, 40, 2f, 6f, 70f);
                SignalLost.Effects.ParticleBurst.Spawn(SignalLost.Effects.ParticleBurst.Kind.Smoke, at + Vector3.up, Vector3.up, 16, 2f, 2f, 80f);
                MiniBossHud.Message($"{current.boss.DisplayName.ToUpperInvariant()} IS OUT THERE", new Color(1f, 0.75f, 0.4f), 3f);
            }
            Signal(current.boss, true);
            Quest(current.title, current.huntObjective);
        }

        private void EnterFight()
        {
            SetStage(Stage.Fight);
            Signal(CurrentJob.boss, false);
            Quest(CurrentJob.title, CurrentJob.fightObjective);
        }

        private void OnBossDefeated(DesertMiniBoss boss)
        {
            Job current = CurrentJob;
            if (current == null || boss != current.boss) return;
            SetStage(Stage.Loot);
            Signal(boss, false);
            if (CampSave.IsPlaced(current.trophyId))
            {
                // Already on the rack from an earlier session: nothing new to carry home.
                trophyPlaced = true;
                OnTrophyHome();
                return;
            }
            Vector3 at = GroundBelow(boss.transform.position + boss.transform.right * 2.5f) + Vector3.up * 0.2f;
            droppedTrophy = TrophyPickup.Create(current.trophyId, at);
            droppedTrophy.gameObject.AddComponent<QuestSignalSource>();
            UpdateLoot();
        }

        private bool HasTrophy => CurrentJob != null && PlayerInventory.Current != null && PlayerInventory.Current.HasKeyItem(CurrentJob.trophyId);
        private bool PartRecovered => ShipPartDrop.CountFor(StageProgress.Desert) > partsAtStart;

        private void UpdateLoot()
        {
            Job current = CurrentJob;
            Trophy trophy = Trophy.Find(current.trophyId);
            string name = trophy != null ? trophy.Name : "trophy";
            string trophyLine = trophyPlaced ? $"[x] {name} on the rack"
                : HasTrophy ? $"[  ] Set the {name.ToLowerInvariant()} on the new rack at camp"
                : $"[  ] Pick up {name.ToLowerInvariant()}";
            string partLine = PartRecovered ? $"[x] {current.boss.PartName}" : $"[  ] Grab the {current.boss.PartName.ToLowerInvariant()} it dropped";
            Quest(current.title, $"{current.boss.DisplayName} is beaten! Take the loot home.\n{trophyLine}\n{partLine}");
            bool carrying = HasTrophy && !trophyPlaced;
            foreach (TrophyStand stand in rack) Signal(stand, carrying && stand.TrophyId == null);
        }

        private void OnTrophyPickedUp(string id)
        {
            if (stage == Stage.Loot && CurrentJob != null && id == CurrentJob.trophyId) UpdateLoot();
            else if (stage == Stage.WormLoot && id == wormTrophyId)
            {
                fangTaken = true;
                UpdateWormLoot();
            }
        }

        private void OnTrophyPlaced(string id)
        {
            if (stage == Stage.Loot && CurrentJob != null && id == CurrentJob.trophyId)
            {
                trophyPlaced = true;
                OnTrophyHome();
            }
            else if (stage == Stage.Homecoming && id == wormTrophyId)
            {
                fangPlaced = true;
                UpdateHomecoming();
            }
        }

        private void OnTrophyHome()
        {
            foreach (TrophyStand stand in rack) Signal(stand, false);
            if (job >= jobs.Length - 1)
            {
                EnterWormWaiting(true);
                return;
            }
            SetStage(Stage.Report);
            Signal(cavendish, true);
            Quest(CurrentJob.title, $"The {Trophy.Find(CurrentJob.trophyId)?.Name.ToLowerInvariant() ?? "trophy"} is on the rack. Report to Cavendish.");
        }

        // ---------- The Core Worm ----------

        // The last trophy is on the rack: the worm bursts out beside it (the player is standing right there).
        private void EnterWormWaiting(bool first)
        {
            SetStage(Stage.WormWaiting);
            if (wormAmbush != null) wormAmbush.Arm();
            Quest(WormTitle, first
                ? "The Chop Shop's trophies are all on the rack. Wait... the ground is shaking."
                : "The Core Worm is lurking under the camp again. Go back to the trophy rack when you're ready.");
        }

        private void OnWormFight()
        {
            SetStage(Stage.WormFight);
            Quest(WormTitle, "Defeat the Core Worm. Hit the glowing blisters and its open mouth; hide behind a pillar when the core overloads.");
        }

        private void OnWormLost()
        {
            if (stage == Stage.WormFight) EnterWormWaiting(false);
        }

        private void OnWormDefeated()
        {
            SetStage(Stage.WormLoot);
            coreTaken = StageProgress.HasMainPart("Engine Core");
            fangTaken = false;
            if (CampSave.IsPlaced(wormTrophyId)) fangTaken = fangPlaced = true;
            else StartCoroutine(DropFang());
            UpdateWormLoot();
        }

        private System.Collections.IEnumerator DropFang()
        {
            // After the death throes, when the core floats free.
            yield return new WaitForSeconds(4f);
            CorewormArena arena = wormAmbush != null ? wormAmbush.Arena : null;
            Vector3 at = arena != null ? arena.Centre + Vector3.right * 3f : player.transform.position + player.transform.forward * 3f;
            if (arena != null) at = arena.NearestSolid(at);
            var fang = TrophyPickup.Create(wormTrophyId, at + Vector3.up * 0.2f);
            fang.gameObject.AddComponent<QuestSignalSource>();
        }

        private void OnCoreRecovered(string part)
        {
            coreTaken = true;
            if (stage == Stage.WormLoot) UpdateWormLoot();
        }

        private void UpdateWormLoot()
        {
            if (coreTaken && fangTaken)
            {
                SetStage(Stage.Collapse);
                Quest(WormTitle, "The roof is coming down! Get out of the red zone before the rubble lands.");
                if (lair != null) lair.BeginCollapse();
                else OnEmerged();
                return;
            }
            Quest(WormTitle, "The Core Worm is beaten! Grab the loot.\n" +
                (coreTaken ? "[x] Engine Core" : "[  ] Engine Core") + "\n" +
                (fangTaken ? "[x] Core Worm Fang" : "[  ] Core Worm Fang"));
        }

        private void OnLairOpen()
        {
            if (stage != Stage.Collapse) return;
            SetStage(Stage.Climb);
            if (lair.ExitPoint != null)
            {
                if (!lair.ExitPoint.TryGetComponent(out QuestSignalSource signal)) signal = lair.ExitPoint.gameObject.AddComponent<QuestSignalSource>();
                signal.enabled = true;
            }
            Quest(HomeTitle, "Climb the ledge round the rubble heap up into the daylight.");
        }

        private void OnEmerged()
        {
            if (stage != Stage.Climb && stage != Stage.Collapse) return;
            if (lair != null && lair.ExitPoint != null) Signal(lair.ExitPoint, false);
            SetStage(Stage.Homecoming);
            Signal(cavendish, true);
            UpdateHomecoming();
        }

        private void UpdateHomecoming()
        {
            bool carrying = PlayerInventory.Current != null && PlayerInventory.Current.HasKeyItem(wormTrophyId);
            foreach (TrophyStand stand in rack) Signal(stand, carrying && !fangPlaced && stand.TrophyId == null); // the desert's own stands
            Quest(HomeTitle, "Out in the open air again, in the Core Worm's old sand bowl. Head back to camp.\n" +
                (fangPlaced ? "[x] Core Worm Fang on a stand" : "[  ] Set the Core Worm Fang on a trophy stand") + "\n" +
                "[  ] Show Cavendish the Engine Core");
        }

        private void EnterDone()
        {
            SetStage(Stage.Done);
            Signal(cavendish, false);
            Quest(HomeTitle, "The desert is cleared! The boat at the north dock can take you on to the next island.", true);
        }

        // ---------- Helpers ----------

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

        /// <summary>Skips Cavendish's welcome and starts the egg hunt (tests and debugging).</summary>
        public void SkipWelcome()
        {
            if (stage == Stage.Welcome) BeginEggs();
        }

        /// <summary>Tests: takes every egg, hears the report and cooks (scoring <paramref name="score"/>), up to breakfast.</summary>
        public void SkipToBreakfastForTests(int score)
        {
            if (stage == Stage.Welcome) BeginEggs();
            foreach (QuestTask egg in eggs) if (egg != null && !egg.IsDone) egg.Complete();
            if (stage == Stage.ReportEggs) BeginCooking();
            if (stage == Stage.Cooking && stove != null) stove.CookForTests(score);
        }

        /// <summary>Tests: skips the snatch's flight (drops the player at the Roost and wakes Briarwing).</summary>
        public void SkipSnatchForTests()
        {
            if (stage != Stage.Breakfast || snatch == null || player == null) return;
            StopAllCoroutines();
            snatch.SkipForTests(player.gameObject);
        }

        /// <summary>Takes the report as if Cavendish had been talked to (tests and debugging).</summary>
        public void SkipReport()
        {
            if (stage == Stage.Report) BeginJob(job + 1);
        }
    }
}
