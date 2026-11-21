using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SignalLost.Audio;
using SignalLost.Effects;
using SignalLost.Enemies.Desert;
using SignalLost.Inventory;
using SignalLost.NPC;
using SignalLost.Player;
using SignalLost.Progression;
using SignalLost.Scanner;
using SignalLost.UI;
using UnityEngine;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// The Mudlands' quests (docs/mudlands.md, section 4): one job at a time, and every job ends back at camp. Cavendish
    /// gives a quest, the tracker shows only that quest, the scanner points only at its objectives, and when it's done
    /// the scanner points at Cavendish. Reporting back pays (scrap, Eugene restocking, a ship part on the "safe keeping"
    /// pile at camp, a change out in the world) and he hands over the next job in the same conversation. Ten quests in
    /// four acts:
    /// <list type="number">
    /// <item><b>Rotwood:</b> the Written Test (the permit: Drunk Driving), the Practical (smash the haul road's plated
    /// barricades and still with the car, against the clock), Kart Kong.</item>
    /// <item><b>Sinkmire Bog:</b> Three Gongs (rung in order, each raising a third of the Spirit Road), Break the Dam
    /// (explosions only), Silt the Mudbender.</item>
    /// <item><b>Lantern Road:</b> Lights Out (five pole lanterns), Hex Lanterns (the curse jumps to three rooftops),
    /// the Sloth Witch (then the road is lit again).</item>
    /// <item><b>The Greasy Spoon:</b> Clutch McCheddar, then his Landing Gear to Cavendish.</item>
    /// </list>
    /// Jobs finished before they're given (a lantern rammed early) are caught up: Cavendish skips straight to the
    /// report. Progress is kept as <see cref="MudlandsProgress"/> flags (ready for the save branch).
    /// </summary>
    public sealed class MudlandsStory : MonoBehaviour, INpcStory
    {
        public enum Step
        {
            Arrive,
            WrittenTest, ReportTest, Practical, ReportPractical, Kong, KongLoot, ReportKong,
            Gongs, ReportGongs, Dam, ReportDam, Silt, SiltLoot, ReportSilt,
            Lanterns, ReportLanterns, HexLanterns, ReportHex, Witch, WitchLoot, ReportWitch,
            Clutch, ClutchLoot, ReportClutch,
            Done
        }

        [Header("Cast")]
        [SerializeField] private NpcActor cavendish;
        [SerializeField] private MerchantCrab merchant;
        [Tooltip("Where the ship parts Cavendish keeps \"for safe keeping\" are piled up at camp.")]
        [SerializeField] private Transform safeKeeping;

        [Header("Act 1: Rotwood")]
        [SerializeField] private DrivingTestKiosk kiosk;
        [SerializeField] private Destructible[] barricades = Array.Empty<Destructible>();
        [SerializeField] private Destructible still;
        [Tooltip("The log jam on the logging track off the camp road (a shortcut to Rotwood once the car shifts it).")]
        [SerializeField] private Destructible logJam;
        [SerializeField] private MudBossEncounter kong;

        [Header("Act 2: Sinkmire Bog")]
        [SerializeField] private SpiritGong[] gongs = Array.Empty<SpiritGong>();
        [SerializeField] private SpiritRoad spiritRoad;
        [SerializeField] private Destructible dam;
        [SerializeField] private GameObject floodWater;
        [SerializeField] private MudBossEncounter otter;

        [Header("Act 3: Lantern Road")]
        [SerializeField] private CursedLantern[] lanterns = Array.Empty<CursedLantern>();
        [SerializeField] private CursedLantern[] hexLanterns = Array.Empty<CursedLantern>();
        [Tooltip("Warm lanterns put up along the road once the witch is beaten (inactive until then).")]
        [SerializeField] private GameObject[] relitLanterns = Array.Empty<GameObject>();
        [SerializeField] private MudBossEncounter witch;

        [Header("Act 4: the Greasy Spoon")]
        [SerializeField] private RoadhouseGate gate;
        [SerializeField] private MudBossEncounter clutch;

        private const string LevelTitle = "The Mudlands";
        private const int QuestCount = 10;
        /// <summary>The Practical's par time, from the first barricade to the still.</summary>
        public const float PracticalPar = 12f;

        private Step step = Step.Arrive;
        private PlayerInventory pockets;
        private int stock, piled;
        private float courseStart = -1f, courseTime = -1f, nextCheck;
        private readonly Dictionary<MudBossEncounter, Loot> loot = new Dictionary<MudBossEncounter, Loot>();

        // What a beaten boss left: its ship part (and a copy of it kept back for the pile), its key item.
        private sealed class Loot
        {
            public string Part, KeyItem;
            public GameObject Copy;
            public ShipPartDrop Drop;
        }

        public Step Current => step;

        public void Configure(NpcActor cav, MerchantCrab crab, Transform pile, DrivingTestKiosk testKiosk, Destructible[] roadBarricades,
            Destructible moonshineStill, Destructible jam, MudBossEncounter kongFight, SpiritGong[] bogGongs, SpiritRoad road, Destructible mudDam,
            GameObject flood, MudBossEncounter otterFight, CursedLantern[] cursed, CursedLantern[] hex, GameObject[] relit,
            MudBossEncounter witchFight, RoadhouseGate roadhouse, MudBossEncounter clutchFight)
        {
            cavendish = cav;
            merchant = crab;
            safeKeeping = pile;
            kiosk = testKiosk;
            barricades = roadBarricades;
            still = moonshineStill;
            logJam = jam;
            kong = kongFight;
            gongs = bogGongs;
            spiritRoad = road;
            dam = mudDam;
            floodWater = flood;
            otter = otterFight;
            lanterns = cursed;
            hexLanterns = hex;
            relitLanterns = relit;
            witch = witchFight;
            gate = roadhouse;
            clutch = clutchFight;
        }

        // ---------- The quests ----------

        // Which quest (1-10) a step belongs to, and its name.
        private static int QuestNumber(Step s) => s switch
        {
            Step.Arrive => 0,
            Step.WrittenTest or Step.ReportTest => 1,
            Step.Practical or Step.ReportPractical => 2,
            Step.Kong or Step.KongLoot or Step.ReportKong => 3,
            Step.Gongs or Step.ReportGongs => 4,
            Step.Dam or Step.ReportDam => 5,
            Step.Silt or Step.SiltLoot or Step.ReportSilt => 6,
            Step.Lanterns or Step.ReportLanterns => 7,
            Step.HexLanterns or Step.ReportHex => 8,
            Step.Witch or Step.WitchLoot or Step.ReportWitch => 9,
            _ => 10
        };

        private static readonly string[] QuestNames =
        {
            "The Mudlands", "The Written Test", "The Practical", "Kart Kong", "Three Gongs", "Break the Dam", "Master Silt",
            "Lights Out", "Hex Lanterns", "Granny Drowse", "Clutch McCheddar"
        };

        private static bool IsReport(Step s) => s == Step.Arrive || s.ToString().StartsWith("Report", StringComparison.Ordinal);

        // ---------- Dialogue ----------

        private static readonly string[] Greeting =
        {
            "Ah, there you are! Mind your boots, mechanic. Welcome to the Mudlands.",
            "Charming, isn't it? Everything here is either sinking, rotting, or trying to bite you.",
            "Bad news: a gang of bikers rolled off with our landing gear. Their leader calls himself Clutch.",
            "Worse news: three of the local, ah, characters pinched the bits bolted round it. We'll want those back first.",
            "One job at a time, mind, and come back and tell me how each one went. I worry. Professionally.",
            "First job: you'll never get round this bog on foot. There's an old driving school in Rotwood, north-west of here.",
            "Pass the written test at the kiosk out front and they'll give you a licence. Any licence. Your scanner will point the way."
        };
        private static readonly string[] PracticalLines =
        {
            "A Learner's Permit! Look at that photo. Is that your thumb?",
            "Now, the practical. Some oaf has barricaded the Rotwood haul road. 'Property of Kart Kong Raceway', if you please.",
            "Four barricades and a moonshine still, all plated with ship scrap. Your laser will bounce right off them. The car won't.",
            "Hold the drink button to chug your flask and call your ride. The kiosk will time you, apparently. Try to beat par.",
            "Mind the turtle on that road. Hit him from the side and he flips. Head-on, he wins. Every time.",
            "Oh, and the old logging track off our road, heading north-west? Jammed with logs. A car would shift them. Short cut to Rotwood."
        };
        private static readonly string[] KongLines =
        {
            "Barricades down, still blown sky-high. The whole bog heard it.",
            "And so did Kart Kong. Big gorilla, tiny go-kart. He lives at the Banana Speedway, far north-west, past Rotwood.",
            "He's got our auxiliary starter motor strapped to his back. Happy as a clam until you break his toy...",
            "So break his toy. Then bring the motor here. For safe keeping."
        };
        private static readonly string[] GongLines =
        {
            "The starter motor! Splendid. I'll just pop it on the pile with the rest. For safe keeping.",
            "Eugene's very excited about that kart wheel, by the way. Something about tyres. Do have a look at his shelves.",
            "Next, the bog, north-east of here. Three spirit gongs stand on its islands, and a blindfolded fellow insists they be rung.",
            "In order, he says. One, two, three. The gong posts are carved with their numbers, which is more than I can say for the islands.",
            "The locals say the gongs wake something under the water. Probably nothing. Probably."
        };
        private static readonly string[] DamLines =
        {
            "All three gongs! I heard them from here. And a road came up out of the bog? Well I never.",
            "Handy, that. Straight across the water from our side to the far shore. Saves you the long way round.",
            "Now the fellow who wanted them rung has dammed the bog and flooded the old chapel. To 'still the waters', apparently.",
            "The dam's at the far end of your new road. Mud doesn't mind bullets, but it hates explosions. Barrels, or the car. Your call."
        };
        private static readonly string[] OtterLines =
        {
            "The water's draining! And the road past the chapel to the Still Water Dojo is open. North-east, past the bog.",
            "Master Silt lives there. An otter. Blindfolded. He 'listens to the earth', which is a polite way of saying he's nosy.",
            "He has our gyro stabilizer. He's blind as a bat, so tread softly. Crouch, even. He hears everything."
        };
        private static readonly string[] LanternLines =
        {
            "The gyro! A bit muddy. I'll polish it. For safe keeping, of course.",
            "Eugene's selling shock absorbers made out of that bead of Silt's. Don't ask me how. Don't ask him either.",
            "Now: someone's hexed the lanterns on the road north, past the Flats. Green flames. Very bad for the complexion.",
            "Five of them along the Lantern Road. Shoot them out, or knock the poles over with the car. Both very satisfying."
        };
        private static readonly string[] HexLines =
        {
            "Five lanterns out! The road's never looked so... dark.",
            "Mind you, a curse has to go somewhere. Curses are like that. Like cousins.",
            "...Speak of the devil. Look north, mechanic. Up on the rooftops."
        };
        private static readonly string[] WitchLines =
        {
            "That's the lot! Not a green flame from here to the Flats.",
            "Those curses were Granny Drowse's work. The sloth witch. Her clearing is at the very end of the Lantern Road, far north.",
            "She's using our antigrav coil to keep her broom up. She's dreadfully slow, but her magic isn't. Knock her out of the sky."
        };
        private static readonly string[] FinaleLines =
        {
            "The antigrav coil! That makes the set. Kong's motor, Silt's gyro, Granny's coil. All safe. With me.",
            "And look up the road: the lanterns are lit again. Proper fire this time. Lovely and orange. Eugene's got something with that ember, too.",
            "Which leaves Clutch McCheddar. He and his Swamp Rats hole up at the Greasy Spoon, the roadhouse far north.",
            "The gate's open now, I made sure of it. He rides a hovercycle on our landing gear. And mechanic: if he runs, chase him."
        };
        private static readonly string[] WrapUpLines =
        {
            "The landing gear! Ha! Clutch McCheddar, face down in his own mud.",
            "I'll keep that safe with the rest, naturally. And those aviators would look splendid on the trophy stand.",
            "The Mudlands is quiet for the first time in, well, ever. Rest up by the fire, mechanic. You've earned it."
        };
        private const string CaughtUpLine = "Hang on. You've already done it? You're quicker than you look.";

        // ---------- Unity ----------

        private void Awake()
        {
            pockets = FindAnyObjectByType<PlayerInventory>();
        }

        private void OnEnable()
        {
            if (kiosk != null) kiosk.PassedTest += Refresh;
            foreach (Destructible item in barricades) if (item != null) item.Broken += OnRoadwork;
            if (still != null) still.Broken += OnRoadwork;
            foreach (SpiritGong gong in gongs) if (gong != null) gong.RungEvent += OnGong;
            if (dam != null) dam.Broken += OnDam;
            foreach (CursedLantern lantern in lanterns.Concat(hexLanterns)) if (lantern != null) lantern.PutOut += OnLantern;
            foreach (MudBossEncounter fight in Fights) if (fight != null) fight.Defeated += Refresh;
            foreach (MudBossEncounter fight in Fights)
                if (fight != null && fight.Boss != null)
                {
                    MudBossEncounter captured = fight;
                    void Handler(MudBoss _) => StartCoroutine(CollectLoot(captured));
                    beatenHandlers[fight] = Handler;
                    fight.Boss.Beaten += Handler;
                }
        }

        private void OnDisable()
        {
            if (kiosk != null) kiosk.PassedTest -= Refresh;
            foreach (Destructible item in barricades) if (item != null) item.Broken -= OnRoadwork;
            if (still != null) still.Broken -= OnRoadwork;
            foreach (SpiritGong gong in gongs) if (gong != null) gong.RungEvent -= OnGong;
            if (dam != null) dam.Broken -= OnDam;
            foreach (CursedLantern lantern in lanterns.Concat(hexLanterns)) if (lantern != null) lantern.PutOut -= OnLantern;
            foreach (MudBossEncounter fight in Fights) if (fight != null) fight.Defeated -= Refresh;
            foreach (KeyValuePair<MudBossEncounter, Action<MudBoss>> pair in beatenHandlers)
                if (pair.Key != null && pair.Key.Boss != null) pair.Key.Boss.Beaten -= pair.Value;
            beatenHandlers.Clear();
        }

        private readonly Dictionary<MudBossEncounter, Action<MudBoss>> beatenHandlers = new Dictionary<MudBossEncounter, Action<MudBoss>>();

        private IEnumerable<MudBossEncounter> Fights => new[] { kong, otter, witch, clutch };

        private void Start()
        {
            MerchantMenuUi shop = MerchantMenuUi.Instance;
            if (shop != null)
            {
                shop.Region = UpgradeRegion.Mudlands;
                shop.Stocked = Stocked;
                shop.RestockNote = "Eugene: \"More stock's on its way. Help Cavendish out and check back after your next report.\"";
            }
            if (gate == null) Debug.LogWarning("Mudlands story has no roadhouse gate.");
            foreach (CursedLantern hex in hexLanterns) if (hex != null) hex.gameObject.SetActive(false);
            foreach (GameObject lamp in relitLanterns) if (lamp != null) lamp.SetActive(false);
            Setup(step);
            UpdateObjectives();
        }

        private void OnDestroy()
        {
            MerchantMenuUi shop = MerchantMenuUi.Current;
            if (shop != null && shop.Stocked == (Func<Upgrade, bool>)Stocked)
            {
                shop.Stocked = null;
                shop.RestockNote = null;
            }
        }

        private void Update()
        {
            // Pickups (ship parts, key items) and anything missed by an event: check the current job twice a second.
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 0.4f;
            Refresh();
        }

        private void Refresh()
        {
            if (!IsReport(step) && step != Step.Done && JobDone(step)) Finish(step);
            else UpdateObjectives();
        }

        // ---------- Talking ----------

        public bool TalkTo(NpcActor npc, PlayerInteractor talker)
        {
            if (npc != cavendish) return false; // Eugene keeps his shop
            if (IsReport(step))
            {
                Report(false);
                return true;
            }
            return Remind();
        }

        // The report for the current step, which also hands over the next job.
        private void Report(bool caughtUp)
        {
            string[] lines = ReportLines(step);
            if (caughtUp) lines = new[] { CaughtUpLine }.Concat(lines.Skip(LinesToSkipWhenCaughtUp(step))).ToArray();
            Step from = step;
            Converse(ReportTitle(step), lines, () => OnReported(from));
        }

        // A caught-up report drops the opening "well done" line (the caught-up line says it).
        private static int LinesToSkipWhenCaughtUp(Step s) => s == Step.ReportPractical || s == Step.ReportDam || s == Step.ReportHex ? 1 : 0;

        private string[] ReportLines(Step s)
        {
            switch (s)
            {
                case Step.Arrive: return Greeting;
                case Step.ReportTest: return PracticalLines;
                case Step.ReportPractical:
                {
                    var lines = KongLines.ToList();
                    if (courseTime > 0f)
                        lines.Insert(1, courseTime <= PracticalPar
                            ? $"And under par! {Clock(courseTime)}. The kiosk printed you a little gold star. Here, have some scrap to go with it."
                            : $"{Clock(courseTime)}, the kiosk says. Par was {PracticalPar:0} seconds. The kiosk can mind its own business.");
                    return lines.ToArray();
                }
                case Step.ReportKong: return GongLines;
                case Step.ReportGongs: return DamLines;
                case Step.ReportDam: return OtterLines;
                case Step.ReportSilt: return LanternLines;
                case Step.ReportLanterns: return HexLines;
                case Step.ReportHex: return WitchLines;
                case Step.ReportWitch: return FinaleLines;
                case Step.ReportClutch: return WrapUpLines;
                default: return new[] { "Splendid work, mechanic." };
            }
        }

        private static string ReportTitle(Step s) => s switch
        {
            Step.Arrive => "Welcome to the Mudlands",
            Step.ReportClutch => "Safe Keeping",
            _ => QuestNames[QuestNumber(s)] + ": done"
        };

        private void OnReported(Step from)
        {
            if (step != from) return; // already moved on (a debug skip)
            Reward(from);
            Step next = from + 1;
            Advance(next);
            if (next == Step.HexLanterns) StartCoroutine(RevealHex());
            // The next job was finished early: go straight on to its report.
            if (IsReport(step) && step != Step.Done && step != from) Report(true);
        }

        private bool Remind()
        {
            NpcDialogue bubble = cavendish.Dialogue;
            if (bubble == null) return false;
            if (bubble.AdvanceManual()) return true;
            string line = step switch
            {
                Step.WrittenTest => "The driving school's in Rotwood, north-west. Read the handbook at the kiosk first. Trust me.",
                Step.Practical => "The haul road, past Rotwood. Drive through the barricades; it's what they're for. And hit the turtle from the side.",
                Step.Kong => "Kart Kong's at the Banana Speedway, far north-west. Break his go-kart and he gets careless.",
                Step.Gongs => "The gongs in the bog, north-east. One, two, three. Don't stand on the wobbly stones for long.",
                Step.Dam => "The dam's at the end of the Spirit Road. Barrels. Boom. I'm told this is how swamp engineering works.",
                Step.Silt => "Master Silt is at the Still Water Dojo, past the chapel. Soft footsteps, mechanic.",
                Step.Lanterns => "Green lanterns on the road north. Shoot them, or knock the poles over. Both very satisfying.",
                Step.HexLanterns => "The rooftops: the windmill, the cabin on the Flats, the shack by the bridge. Aim high.",
                Step.Witch => "The witch's clearing is at the very end of the Lantern Road. Mind the clones.",
                Step.Clutch => "The Greasy Spoon, far north. And if Clutch runs, chase him down!",
                Step.KongLoot or Step.SiltLoot or Step.WitchLoot or Step.ClutchLoot => "Don't leave the part lying in the mud! Go and fetch it.",
                _ => "Splendid work, mechanic. Truly splendid."
            };
            bubble.SpeakManual(line);
            return true;
        }

        private void Converse(string title, string[] lines, Action done) =>
            DialogueUi.Instance.BeginConversation(cavendish.transform, "Cavendish", title, lines, done);

        // ---------- Moving on ----------

        private void Advance(Step next)
        {
            step = next;
            MudlandsProgress.Set("step." + next);
            Setup(next);
            if (!IsReport(step) && step != Step.Done && JobDone(step)) { Finish(step, true); return; }
            UpdateObjectives();
        }

        // Switches on what the current step needs (and only that), so the scanner points at the right thing.
        private void Setup(Step s)
        {
            if (kiosk != null) kiosk.Ready = s == Step.WrittenTest;
            foreach (SpiritGong gong in gongs)
                if (gong != null)
                {
                    gong.Ready = s == Step.Gongs;
                    gong.Next = s == Step.Gongs && gong.Order == NextGong;
                }
            foreach (CursedLantern lantern in lanterns) if (lantern != null) lantern.Beacon = s == Step.Lanterns;
            foreach (CursedLantern lantern in hexLanterns) if (lantern != null) lantern.Beacon = s == Step.HexLanterns;
            if (s == Step.Kong && kong != null) kong.Arm();
            if (s == Step.Silt && otter != null) otter.Arm();
            if (s == Step.Witch && witch != null) witch.Arm();
            if (s == Step.Clutch)
            {
                if (gate != null) gate.Open();
                if (clutch != null) clutch.Arm();
            }
        }

        private int NextGong => gongs.Where(g => g != null && !g.Rung).Select(g => g.Order).DefaultIfEmpty(4).Min();

        // The job is done: say so, and point back at Cavendish.
        private void Finish(Step s, bool quiet = false)
        {
            step = s + 1;
            MudlandsProgress.Set("step." + step);
            if (!quiet && IsReport(step))
            {
                Sfx.Play("quest_chime", 1f);
                MudHud.Shout(QuestNames[QuestNumber(s)].ToUpperInvariant() + ": DONE", new Color(0.6f, 1f, 0.6f), 2.4f);
                MudHud.Toast("Head back to camp and report to Cavendish. Your scanner points the way.", new Color(1f, 0.85f, 0.5f), 3.5f);
            }
            Setup(step);
            UpdateObjectives();
        }

        private bool JobDone(Step s)
        {
            switch (s)
            {
                case Step.WrittenTest: return pockets != null && pockets.HasKeyItem(KeyItems.LearnersPermit);
                case Step.Practical: return barricades.All(b => b == null || b.IsBroken) && (still == null || still.IsBroken);
                case Step.Kong: return Beaten(kong);
                case Step.Silt: return Beaten(otter);
                case Step.Witch: return Beaten(witch);
                case Step.Clutch: return Beaten(clutch);
                case Step.KongLoot: return LootTaken(kong);
                case Step.SiltLoot: return LootTaken(otter);
                case Step.WitchLoot: return LootTaken(witch);
                case Step.ClutchLoot: return LootTaken(clutch);
                case Step.Gongs: return gongs.All(g => g == null || g.Rung);
                case Step.Dam: return dam == null || dam.IsBroken;
                case Step.Lanterns: return lanterns.All(l => l == null || l.Out);
                case Step.HexLanterns: return hexLanterns.All(l => l == null || l.Out);
                default: return false;
            }
        }

        private static bool Beaten(MudBossEncounter fight) => fight == null || fight.Current == MudBossEncounter.State.Defeated;

        // The ship part is picked up, and the key item too, if one dropped and is still lying there.
        private bool LootTaken(MudBossEncounter fight)
        {
            if (fight == null || !loot.TryGetValue(fight, out Loot found)) return fight == null || fight.Boss == null;
            bool part = string.IsNullOrEmpty(found.Part) || ShipPartDrop.Recovered.Contains(found.Part);
            bool key = string.IsNullOrEmpty(found.KeyItem) || (pockets != null && pockets.HasKeyItem(found.KeyItem)) || !KeyItemLying(fight);
            return part && key;
        }

        private readonly Dictionary<MudBossEncounter, List<KeyItemPickup>> keyDrops = new Dictionary<MudBossEncounter, List<KeyItemPickup>>();

        private bool KeyItemLying(MudBossEncounter fight) => keyDrops.TryGetValue(fight, out var drops) && drops.Any(d => d != null);

        // ---------- Events ----------

        private void OnRoadwork(Destructible piece)
        {
            if (step == Step.Practical)
            {
                if (courseStart < 0f) courseStart = Time.time;
                int down = barricades.Count(b => b != null && b.IsBroken);
                bool stillDown = still != null && still.IsBroken;
                float time = Time.time - courseStart;
                if (down == barricades.Length && stillDown)
                {
                    courseTime = Mathf.Max(0.1f, time);
                    bool par = courseTime <= PracticalPar;
                    MudHud.Toast($"Course time {Clock(courseTime)}{(par ? ": under par!" : $" (par {PracticalPar:0} s)")}", par ? new Color(1f, 0.85f, 0.35f) : new Color(1f, 0.75f, 0.5f), 4f);
                }
                else MudHud.Toast($"{(piece == still ? "Still blown" : $"Barricade {down} / {barricades.Length}")}   {Clock(time)}  (par {PracticalPar:0} s)",
                    new Color(1f, 0.85f, 0.5f), 2.2f);
            }
            Refresh();
        }

        private void OnGong(SpiritGong gong)
        {
            if (spiritRoad != null) spiritRoad.RaiseNext();
            int rung = gongs.Count(g => g != null && g.Rung);
            if (rung < gongs.Length) MudHud.Toast($"Something stirs under the water... ({rung} / {gongs.Length})", SpiritRoad.Spirit, 2.6f);
            else MudHud.Toast("The Spirit Road rises out of the bog!", SpiritRoad.Spirit, 3f);
            Setup(step);
            Refresh();
        }

        private void OnDam(Destructible _)
        {
            if (floodWater != null && floodWater.GetComponent<Drain>() == null) floodWater.AddComponent<Drain>();
            Refresh();
        }

        private void OnLantern(CursedLantern _) => Refresh();

        // A boss was beaten: once its loot is out (the part drops after the Beaten event), mark it for the scanner and
        // keep a copy of the part back for the safe-keeping pile.
        private IEnumerator CollectLoot(MudBossEncounter fight)
        {
            Vector3 at = fight.Boss.transform.position;
            yield return null;
            var found = new Loot();
            ShipPartDrop drop = FindObjectsByType<ShipPartDrop>(FindObjectsSortMode.None)
                .OrderBy(d => Vector3.Distance(d.transform.position, at)).FirstOrDefault();
            if (drop != null)
            {
                found.Drop = drop;
                found.Part = fight.Boss.PartName;
                Signal(drop, false, true);
                found.Copy = CopyForPile(drop.gameObject);
            }
            var keys = FindObjectsByType<KeyItemPickup>(FindObjectsSortMode.None).Where(k => Vector3.Distance(k.transform.position, at) < 12f).ToList();
            foreach (KeyItemPickup key in keys) Signal(key, false, true);
            keyDrops[fight] = keys;
            found.KeyItem = keys.Count > 0 ? KeyItemOf(fight) : null;
            loot[fight] = found;
            Refresh();
        }

        private string KeyItemOf(MudBossEncounter fight) =>
            fight == kong ? KeyItems.KartWheel : fight == otter ? KeyItems.MudstoneBead : fight == witch ? KeyItems.WitchEmber : null;

        // ---------- Rewards ----------

        private void Reward(Step report)
        {
            var scrap = new List<(ScrapType, int)>();
            switch (report)
            {
                case Step.ReportTest: scrap.Add((ScrapType.ScrapMetal, 8)); scrap.Add((ScrapType.Wiring, 2)); Restock(1); break;
                case Step.ReportPractical:
                    scrap.Add((ScrapType.ScrapMetal, 10)); scrap.Add((ScrapType.CircuitBoards, 3));
                    if (courseTime > 0f && courseTime <= PracticalPar) scrap.Add((ScrapType.FuelCells, 2));
                    break;
                case Step.ReportKong: scrap.Add((ScrapType.ScrapMetal, 12)); scrap.Add((ScrapType.Wiring, 4)); scrap.Add((ScrapType.FuelCells, 2)); Pile(kong); Restock(2); break;
                case Step.ReportGongs: scrap.Add((ScrapType.ScrapMetal, 10)); scrap.Add((ScrapType.Wiring, 3)); break;
                case Step.ReportDam: scrap.Add((ScrapType.ScrapMetal, 8)); scrap.Add((ScrapType.CircuitBoards, 3)); scrap.Add((ScrapType.FuelCells, 1)); break;
                case Step.ReportSilt: scrap.Add((ScrapType.ScrapMetal, 12)); scrap.Add((ScrapType.CircuitBoards, 4)); scrap.Add((ScrapType.FuelCells, 2)); Pile(otter); Restock(3); break;
                case Step.ReportLanterns: scrap.Add((ScrapType.ScrapMetal, 10)); scrap.Add((ScrapType.Wiring, 4)); break;
                case Step.ReportHex: scrap.Add((ScrapType.ScrapMetal, 8)); scrap.Add((ScrapType.CircuitBoards, 3)); scrap.Add((ScrapType.FuelCells, 1)); break;
                case Step.ReportWitch:
                    scrap.Add((ScrapType.ScrapMetal, 14)); scrap.Add((ScrapType.Wiring, 4)); scrap.Add((ScrapType.FuelCells, 2));
                    Pile(witch);
                    Restock(4);
                    foreach (GameObject lamp in relitLanterns) if (lamp != null) lamp.SetActive(true);
                    break;
                case Step.ReportClutch: WrapUp(); return;
            }
            if (scrap.Count == 0 || pockets == null) return;
            foreach ((ScrapType type, int amount) in scrap) pockets.AddScrap(type, amount);
            Sfx.Play("scrap_vacuum", 0.8f);
            MudHud.Toast("Cavendish pays: " + string.Join(", ", scrap.Select(s => $"+{s.Item2} {ScrapInfo.Name(s.Item1)}")), new Color(0.75f, 1f, 0.75f), 3.5f);
        }

        // Eugene's Mudlands shelves fill up as the story goes (MerchantMenuUi.Stocked).
        private static readonly string[][] Shelves =
        {
            new string[0],
            new[] { "reinforced_bumper", "quick_chug", "spare_keys" },
            new[] { "mud_tires", "extra_proof" },
            new[] { "shockwave_shocks" },
            new[] { "bigger_boom" }
        };

        private bool Stocked(Upgrade upgrade)
        {
            if (upgrade == null || upgrade.Region != UpgradeRegion.Mudlands || !upgrade.OneOff) return true;
            for (int level = 1; level < Shelves.Length; level++)
                if (Shelves[level].Contains(upgrade.Id)) return stock >= level;
            return true;
        }

        private void Restock(int level)
        {
            if (level <= stock) return;
            stock = level;
            MudlandsProgress.Set("stock." + level);
            var names = UpgradeCatalog.All.Where(u => Shelves[level].Contains(u.Id)).Select(u => u.Name).ToList();
            if (names.Count == 0) return;
            Sfx.Play("shop_bell", 0.9f);
            MudHud.Toast("Eugene restocked: " + string.Join(", ", names), new Color(0.55f, 0.85f, 1f), 4.5f);
        }

        // ---------- The safe-keeping pile ----------

        private GameObject CopyForPile(GameObject part)
        {
            if (safeKeeping == null || part == null) return null;
            var bounds = new Bounds(part.transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer view in part.GetComponentsInChildren<Renderer>())
            {
                if (!any) { bounds = view.bounds; any = true; }
                else bounds.Encapsulate(view.bounds);
            }
            var holder = new GameObject("Kept: " + part.name);
            holder.SetActive(false);
            holder.transform.SetParent(safeKeeping, false);
            GameObject copy = Instantiate(part, part.transform.position, part.transform.rotation, holder.transform);
            copy.name = part.name;
            foreach (Component piece in copy.GetComponentsInChildren<Component>(true))
                if (!(piece is Transform) && !(piece is MeshFilter) && !(piece is MeshRenderer)) Destroy(piece);
            float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float scale = size > 0.01f ? Mathf.Clamp(0.8f / size, 0.2f, 3f) : 1f;
            copy.transform.localScale = part.transform.lossyScale * scale;
            // Sit it on the ground of its slot: centred over the slot, its bottom on the ground.
            copy.transform.position = holder.transform.position + (part.transform.position - bounds.center) * scale + Vector3.up * bounds.extents.y * scale;
            return holder;
        }

        private static readonly Vector3[] PileSlots = { new Vector3(0f, 0f, 0f), new Vector3(0.95f, 0f, 0.35f), new Vector3(0.2f, 0f, 1f), new Vector3(-0.8f, 0f, 0.55f) };

        private void Pile(MudBossEncounter fight)
        {
            if (fight == null || !loot.TryGetValue(fight, out Loot found) || found.Copy == null) return;
            Vector3 slot = PileSlots[Mathf.Min(piled++, PileSlots.Length - 1)];
            Transform holder = found.Copy.transform;
            Vector3 offset = holder.GetChild(0).position - holder.position;
            holder.localPosition = slot;
            holder.GetChild(0).position = holder.position + offset;
            found.Copy.SetActive(true);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, holder.position + Vector3.up * 0.5f, Vector3.up, 24, 0.06f, 2.5f, 70f, new Color(1f, 0.8f, 0.4f));
            Sfx.PlayAt("item_pickup", holder.position, 0.7f, 0.8f, 20f);
        }

        // ---------- The hex lanterns flare up ----------

        private IEnumerator RevealHex()
        {
            yield return new WaitForSeconds(0.4f);
            foreach (CursedLantern hex in hexLanterns)
            {
                if (hex == null || hex.Out) continue;
                hex.Reveal();
                CameraShake.Add(0.12f);
                yield return new WaitForSeconds(0.35f);
            }
            Sfx.Play("cackle", 0.6f);
            MudHud.Shout("THE CURSE JUMPED TO THE ROOFTOPS!", MudPalette.Curse, 3f);
            UpdateObjectives();
        }

        // ---------- The end ----------

        private void WrapUp()
        {
            Pile(clutch);
            StageProgress.RecoverMainPart(MudlandsProgress.MainPart);
            StageProgress.ClearStage(MudlandsProgress.Stage, MudlandsProgress.NextStage);
            MudlandsProgress.Set("complete");
        }

        // ---------- Tracker and scanner ----------

        private void UpdateObjectives()
        {
            if (GameplayOverlay.Instance == null) return;
            int number = QuestNumber(step);
            if (step == Step.Done)
            {
                GameplayOverlay.Instance.SetQuest(LevelTitle, "The Mudlands is clear. Set Clutch's aviators on a trophy stand, and rest by the fire.", true);
                ApplySignals();
                return;
            }
            string body = Objective(step);
            if (number > 0) body += $"\n<color=#8FA9A4>Mudlands quest {number} of {QuestCount}</color>";
            GameplayOverlay.Instance.SetQuest(QuestNames[number], body);
            ApplySignals();
        }

        private string Objective(Step s)
        {
            switch (s)
            {
                case Step.Arrive: return "Talk to Cavendish by the fire at camp.";
                case Step.WrittenTest:
                    return kiosk != null && kiosk.Failures > 0
                        ? "Failed! Read the handbook again at the kiosk, then retake the test (Bayou Driving School, Rotwood, north-west)."
                        : "Pass the written test at the kiosk outside the Bayou Driving School in Rotwood, north-west of camp.";
                case Step.Practical:
                {
                    int down = barricades.Count(b => b != null && b.IsBroken);
                    string text = $"Smash the haul road's barricades and still with the car (past Rotwood, north-west). " +
                                  $"Barricades {down} / {barricades.Length}, still {(still != null && still.IsBroken ? "blown" : "standing")}. Par {PracticalPar:0} s.";
                    if (logJam != null && !logJam.IsBroken) text += "\nShort cut: ram the log jam off the camp road.";
                    return text;
                }
                case Step.Kong: return "Beat Kart Kong at the Banana Speedway, far north-west past Rotwood. Break his go-kart first.";
                case Step.Silt: return "Beat Master Silt at the Still Water Dojo, north-east past the drained chapel. He hears footsteps: crouch to sneak.";
                case Step.Witch: return "Beat Granny Drowse in Hag's Clearing, at the far end of the Lantern Road (far north).";
                case Step.Clutch: return "Beat Clutch McCheddar at the Greasy Spoon, the roadhouse far north. If he runs, ram him with the car.";
                case Step.KongLoot: case Step.SiltLoot: case Step.WitchLoot: case Step.ClutchLoot: return LootText(Fight(s));
                case Step.Gongs:
                {
                    int rung = gongs.Count(g => g != null && g.Rung);
                    return $"Ring the spirit gongs in Sinkmire Bog (north-east of camp) in order: gong {SpiritGong.Numeral(Mathf.Min(NextGong, 3))} next, {rung} / {gongs.Length} rung. " +
                           "Don't stand on the stepping stones too long.";
                }
                case Step.Dam:
                    return "Break the mud dam at the far end of the Spirit Road. Only an explosion will do: shoot the barrels at its foot, or crash the car into it.";
                case Step.Lanterns:
                    return $"Put out the cursed lanterns on the Lantern Road, north of Gumbo Flats: shoot them or ram the poles. " +
                           $"{lanterns.Count(l => l != null && l.Out)} / {lanterns.Length} out.";
                case Step.HexLanterns:
                    return $"Shoot down the hex lanterns on the Flats windmill, the Flats cabin and the shack by the Lantern Road bridge. Aim high. " +
                           $"{hexLanterns.Count(l => l != null && l.Out)} / {hexLanterns.Length} out.";
                case Step.ReportTest: return "Passed! Show Cavendish your Learner's Permit back at camp.";
                case Step.ReportPractical: return "Practical passed! Report to Cavendish at camp.";
                case Step.ReportKong: return "Take the starter motor to Cavendish at camp.";
                case Step.ReportGongs: return "The Spirit Road has risen! Report to Cavendish: it's a short cut home across the bog.";
                case Step.ReportDam: return "The flood is draining. Report to Cavendish at camp.";
                case Step.ReportSilt: return "Take the gyro stabilizer to Cavendish at camp.";
                case Step.ReportLanterns: return "The road's dark. Report to Cavendish at camp.";
                case Step.ReportHex: return "Not a green flame left. Report to Cavendish at camp.";
                case Step.ReportWitch: return "Take the antigrav coil to Cavendish at camp.";
                case Step.ReportClutch: return "Take the landing gear to Cavendish at camp.";
                default: return "";
            }
        }

        private MudBossEncounter Fight(Step s) => s == Step.KongLoot ? kong : s == Step.SiltLoot ? otter : s == Step.WitchLoot ? witch : clutch;

        private string LootText(MudBossEncounter fight)
        {
            if (fight == null || !loot.TryGetValue(fight, out Loot found)) return "Pick up what the boss dropped.";
            bool part = string.IsNullOrEmpty(found.Part) || ShipPartDrop.Recovered.Contains(found.Part);
            bool key = string.IsNullOrEmpty(found.KeyItem) || !KeyItemLying(fight);
            string what = !part && !key ? $"the {found.Part} and the {KeyItems.Name(found.KeyItem)}" : !part ? $"the {found.Part}" : $"the {KeyItems.Name(found.KeyItem)}";
            return $"Pick up {what} {fight.Boss.DisplayName} dropped.";
        }

        private void ApplySignals()
        {
            Signal(cavendish, IsReport(step));
            Signal(kiosk, step == Step.WrittenTest);
            foreach (Destructible item in barricades) Signal(item, step == Step.Practical && item != null && !item.IsBroken);
            Signal(still, step == Step.Practical && still != null && !still.IsBroken);
            Signal(logJam, false);
            int next = NextGong;
            foreach (SpiritGong gong in gongs) Signal(gong, step == Step.Gongs && gong != null && !gong.Rung && gong.Order == next);
            Signal(dam, step == Step.Dam && dam != null && !dam.IsBroken);
            foreach (CursedLantern lantern in lanterns) Signal(lantern, step == Step.Lanterns && lantern != null && !lantern.Out);
            foreach (CursedLantern lantern in hexLanterns) Signal(lantern, step == Step.HexLanterns && lantern != null && !lantern.Out, true);
            // Loot: only the current boss's, while it's lying there.
            foreach (KeyValuePair<MudBossEncounter, Loot> pair in loot)
            {
                bool looting = (step == Step.KongLoot && pair.Key == kong) || (step == Step.SiltLoot && pair.Key == otter) ||
                               (step == Step.WitchLoot && pair.Key == witch) || (step == Step.ClutchLoot && pair.Key == clutch);
                if (keyDrops.TryGetValue(pair.Key, out var keys)) foreach (KeyItemPickup key in keys) if (key != null) Signal(key, looting);
                if (pair.Value.Drop != null) Signal(pair.Value.Drop, looting);
            }
        }

        private static void Signal(Component owner, bool on, bool add = false)
        {
            if (owner == null) return;
            if (!owner.TryGetComponent(out QuestSignalSource signal))
            {
                if (!add) return;
                signal = owner.gameObject.AddComponent<QuestSignalSource>();
            }
            signal.enabled = on;
        }

        private static string Clock(float seconds) => $"{(int)(seconds / 60f)}:{seconds % 60f:00.0}";

        // ---------- Saving and testing ----------

        /// <summary>The story's progress as flags (for the save branch's SaveData.flags).</summary>
        public List<string> CaptureFlags()
        {
            MudlandsProgress.Set("step." + step);
            return MudlandsProgress.CaptureFlags();
        }

        /// <summary>
        /// Testing: finishes the current job as if the player had done it (breaks its pieces, rings its gongs, passes the
        /// test). Boss steps are left to the fight. Returns what it did.
        /// </summary>
        public string CompleteCurrentForTests()
        {
            switch (step)
            {
                case Step.WrittenTest:
                    pockets?.AddKeyItem(KeyItems.LearnersPermit);
                    break;
                case Step.Practical:
                    foreach (Destructible piece in barricades.Append(still)) if (piece != null && !piece.IsBroken) piece.SmashByCar(null, Vector3.zero);
                    break;
                case Step.Gongs:
                    foreach (SpiritGong gong in gongs.OrderBy(g => g.Order)) { Setup(step); gong.Interact(FindAnyObjectByType<PlayerInteractor>()); }
                    break;
                case Step.Dam:
                    dam?.SmashByCar(null, Vector3.zero);
                    break;
                case Step.Lanterns:
                    foreach (CursedLantern lantern in lanterns) if (lantern != null && !lantern.Out) lantern.GetComponent<Destructible>().SmashByCar(null, Vector3.zero);
                    break;
                case Step.HexLanterns:
                    foreach (CursedLantern lantern in hexLanterns) if (lantern != null && !lantern.Out) lantern.GetComponent<Destructible>().SmashByCar(null, Vector3.zero);
                    break;
                case Step.Kong: case Step.Silt: case Step.Witch: case Step.Clutch:
                case Step.KongLoot: case Step.SiltLoot: case Step.WitchLoot: case Step.ClutchLoot:
                    // Testing only: skip the fight and its loot, straight to the report (nothing goes on the pile).
                    step = step == Step.Kong || step == Step.Silt || step == Step.Witch || step == Step.Clutch ? step + 2 : step + 1;
                    Setup(step);
                    UpdateObjectives();
                    return "skipped the boss: now " + step;
                default:
                    if (IsReport(step)) { DialogueUi.Instance.CloseAll(); OnReported(step); return "reported: now " + step; }
                    return "nothing to complete at " + step;
            }
            Refresh();
            return "completed: now " + step;
        }
    }

    /// <summary>Lowers flood water out of sight and switches its bog-water zones off (the dam broke).</summary>
    public sealed class Drain : MonoBehaviour
    {
        private Vector3 from;
        private float started;

        private void Start()
        {
            from = transform.position;
            started = Time.time;
            foreach (MudZone zone in GetComponentsInChildren<MudZone>()) zone.enabled = false;
            SignalLost.Audio.Sfx.PlayAt("water_lap", transform.position, 1f, 0.6f, 80f);
        }

        private void Update()
        {
            float k = (Time.time - started) / 4f;
            transform.position = from + Vector3.down * 2.5f * Mathf.SmoothStep(0f, 1f, k);
            if (k >= 1f) gameObject.SetActive(false);
        }
    }
}
