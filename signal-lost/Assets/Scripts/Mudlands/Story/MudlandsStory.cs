using System;
using System.Collections.Generic;
using System.Linq;
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
    /// The Mudlands' quests (docs/mudlands.md), the same loop as the tutorial: Cavendish at the base camp gives the
    /// work, the tracker shows it, the scanner points at it. Here he hands out three quest lines at once, to do in any
    /// order and side by side:
    /// <list type="number">
    /// <item><b>Learner's Permit:</b> the written test (the permit: Drunk Driving), then the practical (break the haul
    /// road's barricades and blow the moonshine still), then Kart Kong.</item>
    /// <item><b>Still Waters:</b> ring three spirit gongs in the bog, then break the mud dam, then Silt the Mudbender.</item>
    /// <item><b>Lights Out:</b> put out five cursed lanterns, then the Sloth Witch.</item>
    /// </list>
    /// Each line's end: report back, Cavendish gives directions to its mini-boss (<see cref="MudBossEncounter.Arm"/>),
    /// beat it, and its ship part goes to Cavendish. With all three down he points the way to the Greasy Spoon: the
    /// gate opens and Clutch McCheddar waits. Beaten, his Landing Gear goes to Cavendish and his aviators to the trophy
    /// stands. Progress is kept as <see cref="MudlandsProgress"/> flags (ready for the save branch).
    /// </summary>
    public sealed class MudlandsStory : MonoBehaviour, INpcStory
    {
        // Each line moves through these steps.
        private enum Step { Quest1, Report1, Quest2, Report2, Boss, BossBeaten, Done }

        [Header("Cast")]
        [SerializeField] private NpcActor cavendish;
        [SerializeField] private MerchantCrab merchant;

        [Header("Line 1: Learner's Permit")]
        [SerializeField] private DrivingTestKiosk kiosk;
        [SerializeField] private Destructible[] barricades = Array.Empty<Destructible>();
        [SerializeField] private Destructible still;
        [SerializeField] private MudBossEncounter kong;

        [Header("Line 2: Still Waters")]
        [SerializeField] private SpiritGong[] gongs = Array.Empty<SpiritGong>();
        [SerializeField] private Destructible dam;
        [SerializeField] private GameObject floodWater;
        [SerializeField] private MudBossEncounter otter;

        [Header("Line 3: Lights Out")]
        [SerializeField] private CursedLantern[] lanterns = Array.Empty<CursedLantern>();
        [SerializeField] private MudBossEncounter witch;

        [Header("Stage boss")]
        [SerializeField] private RoadhouseGate gate;
        [SerializeField] private MudBossEncounter clutch;

        private const string Title = "The Mudlands";
        private readonly Step[] steps = { Step.Quest1, Step.Quest1, Step.Quest1 };
        private bool greeted, finaleOffered, finaleArmed, clutchBeaten, partsGiven, landingGearGiven, wrapUpDone;
        private int nextReminder;
        private static readonly string[] LineNames = { "Learner's Permit", "Still Waters", "Lights Out" };

        public void Configure(NpcActor cav, MerchantCrab crab, DrivingTestKiosk testKiosk, Destructible[] roadBarricades, Destructible moonshineStill,
            MudBossEncounter kongFight, SpiritGong[] bogGongs, Destructible mudDam, GameObject flood, MudBossEncounter otterFight,
            CursedLantern[] cursed, MudBossEncounter witchFight, RoadhouseGate roadhouse, MudBossEncounter clutchFight)
        {
            cavendish = cav;
            merchant = crab;
            kiosk = testKiosk;
            barricades = roadBarricades;
            still = moonshineStill;
            kong = kongFight;
            gongs = bogGongs;
            dam = mudDam;
            floodWater = flood;
            otter = otterFight;
            lanterns = cursed;
            witch = witchFight;
            gate = roadhouse;
            clutch = clutchFight;
        }

        // ---------- Dialogue ----------

        private static readonly string[] Greeting =
        {
            "Ah, there you are! Mind your boots, mechanic. Welcome to the Mudlands.",
            "Charming, isn't it? Everything here is either sinking, rotting, or trying to bite you.",
            "Bad news: a gang of bikers rolled off with our landing gear. Whoever's leading them calls himself Clutch.",
            "Worse news: three of the local, ah, characters pinched the bits bolted round it. We'll need those first.",
            "So, three jobs. Do them in any order you like. Your scanner will point the way.",
            "One: the old driving school in Rotwood, west of here. Get yourself a licence. You'll want wheels out here.",
            "Two: the bog to the east has three spirit gongs. Some blindfolded fellow insists they be rung. Humour him.",
            "Three: someone's hexed the lanterns on the road north. Green flames. Very bad for the complexion. Put them out."
        };
        private static readonly string[] PracticalLines =
        {
            "A Learner's Permit! Look at that photo. Is that your thumb?",
            "Now, the practical. Some oaf has barricaded the Rotwood haul road. 'Property of Kart Kong Raceway', if you please.",
            "Four barricades, and a moonshine still at the end belching fumes. Break the lot. With the car, ideally. It's very cathartic.",
            "Hold the drink button to chug and call your ride. Do try to hit the barricades and not the trees."
        };
        private static readonly string[] KongLines =
        {
            "Barricades down, still blown sky-high. Marvellous. The whole bog heard it.",
            "And so did Kart Kong. Big gorilla, tiny go-kart. Lives at the Banana Speedway, north-west, past Rotwood.",
            "He's got our auxiliary starter motor strapped to his back. He seems happy enough until you break his toy...",
            "So break his toy."
        };
        private static readonly string[] DamLines =
        {
            "All three gongs! I heard them from here. Lovely tone. Very spiritual.",
            "Now the fellow who wanted them rung has dammed up the bog and flooded the old chapel. To 'still the waters', apparently.",
            "There's a pile of moonshine barrels at the foot of the dam. I'm sure you'll think of something."
        };
        private static readonly string[] OtterLines =
        {
            "The water's draining! And the path to the Still Water Dojo is open. East, then north.",
            "Master Silt lives there. An otter. Blindfolded. He 'listens to the earth', which is a polite way of saying he's nosy.",
            "He has our gyro stabilizer. He's blind as a bat, mind, so tread softly. Crouch, even. He hears everything."
        };
        private static readonly string[] WitchLines =
        {
            "Five lanterns out! The road's never looked so... dark.",
            "Those curses were Granny Drowse's work. The sloth witch. She lives in the clearing at the end of the Lantern Road, north-east.",
            "She's using our antigrav coil to keep her broom up. She's dreadfully slow, but her magic isn't. Knock her out of the sky."
        };
        private static readonly string[] FinaleLines =
        {
            "Kart Kong, Master Silt AND Granny Drowse! Any bits of ship you prise off them, hand them here. For... safe keeping.",
            "You've done more for this swamp in a day than the swamp's done for itself in a century.",
            "Which leaves Clutch McCheddar. He and his Swamp Rats hole up at the Greasy Spoon, the roadhouse up north.",
            "The gate's open now, I made sure of it. He rides a chopper on our landing gear. And mechanic: if he runs, chase him."
        };
        private static readonly string[] WrapUpLines =
        {
            "The landing gear! Ha! Clutch McCheddar, face down in his own mud.",
            "I'll keep that safe with the rest, naturally. And those aviators would look splendid on the trophy stand.",
            "The Mudlands is quiet for the first time in, well, ever. Rest up by the fire, mechanic. You've earned it."
        };

        // ---------- Unity ----------

        private PlayerInventory pockets;

        private void Awake()
        {
            pockets = FindAnyObjectByType<PlayerInventory>();
        }

        private void OnEnable()
        {
            if (kiosk != null) kiosk.Passed += OnPermit;
            foreach (Destructible item in barricades) if (item != null) item.Broken += OnRoadwork;
            if (still != null) still.Broken += OnRoadwork;
            foreach (SpiritGong gong in gongs) if (gong != null) gong.RungEvent += OnGong;
            if (dam != null) dam.Broken += OnDam;
            foreach (CursedLantern lantern in lanterns) if (lantern != null) lantern.PutOut += OnLantern;
            if (kong != null) kong.Defeated += OnKong;
            if (otter != null) otter.Defeated += OnOtter;
            if (witch != null) witch.Defeated += OnWitch;
            if (clutch != null) clutch.Defeated += OnClutch;
        }

        private void OnDisable()
        {
            if (kiosk != null) kiosk.Passed -= OnPermit;
            foreach (Destructible item in barricades) if (item != null) item.Broken -= OnRoadwork;
            if (still != null) still.Broken -= OnRoadwork;
            foreach (SpiritGong gong in gongs) if (gong != null) gong.RungEvent -= OnGong;
            if (dam != null) dam.Broken -= OnDam;
            foreach (CursedLantern lantern in lanterns) if (lantern != null) lantern.PutOut -= OnLantern;
            if (kong != null) kong.Defeated -= OnKong;
            if (otter != null) otter.Defeated -= OnOtter;
            if (witch != null) witch.Defeated -= OnWitch;
            if (clutch != null) clutch.Defeated -= OnClutch;
        }

        private void Start()
        {
            if (MerchantMenuUi.Instance != null) MerchantMenuUi.Instance.Region = UpgradeRegion.Mudlands;
            if (gate == null) Debug.LogWarning("Mudlands story has no roadhouse gate.");
            Signal(cavendish, true);
            GameplayOverlay.Instance.SetQuest(Title, "Talk to Cavendish at the camp.");
            UpdateObjectives();
        }

        private float nextPartCheck;

        private void Update()
        {
            // Ship parts are picked up by walking over them; refresh when the count changes.
            if (Time.time < nextPartCheck) return;
            nextPartCheck = Time.time + 0.5f;
            int carried = ShipPartDrop.CountFor(MudlandsProgress.Stage);
            if (carried != lastPartCount)
            {
                lastPartCount = carried;
                UpdateObjectives();
            }
        }

        private int lastPartCount = -1;

        // ---------- Talking ----------

        public bool TalkTo(NpcActor npc, PlayerInteractor talker)
        {
            if (npc != cavendish) return false; // Eugene keeps his shop
            if (!greeted)
            {
                Converse("Welcome to the Mudlands", Greeting, OnGreeted);
                return true;
            }
            if (clutchBeaten && !wrapUpDone && CarryingLandingGear)
            {
                Converse("Safe Keeping", WrapUpLines, OnWrapUp);
                return true;
            }
            if (AllMiniBossesBeaten && !finaleOffered)
            {
                Converse("The Greasy Spoon", FinaleLines, OnFinaleOffered);
                return true;
            }
            for (int line = 0; line < 3; line++)
            {
                if (steps[line] == Step.Report1)
                {
                    int captured = line;
                    if (line == 0) Converse(LineNames[0], PracticalLines, () => Advance(captured, Step.Quest2));
                    else if (line == 1) Converse(LineNames[1], DamLines, () => Advance(captured, Step.Quest2));
                    else Converse(LineNames[2], WitchLines, () => ArmBoss(captured));
                    return true;
                }
                if (steps[line] == Step.Report2)
                {
                    int captured = line;
                    Converse(LineNames[line], line == 0 ? KongLines : OtterLines, () => ArmBoss(captured));
                    return true;
                }
            }
            return Remind();
        }

        private bool Remind()
        {
            NpcDialogue bubble = cavendish.Dialogue;
            if (bubble == null) return false;
            if (bubble.AdvanceManual()) return true;
            var options = new List<string>();
            if (steps[0] == Step.Quest1) options.Add("The driving school's in Rotwood, west. Bring back a licence. Any licence.");
            if (steps[0] == Step.Quest2) options.Add("The haul road, mechanic. Barricades and a still. Drive through them; it's what they're for.");
            if (steps[1] == Step.Quest1) options.Add("Three gongs out in the bog, east. Don't stand on the wobbly stones for long.");
            if (steps[1] == Step.Quest2) options.Add("The dam. Barrels. Boom. I'm told this is how swamp engineering works.");
            if (steps[2] == Step.Quest1) options.Add("Green lanterns on the north road. Shoot them, or knock the poles over. Both very satisfying.");
            foreach (int line in new[] { 0, 1, 2 })
                if (steps[line] == Step.Boss) options.Add(line == 0 ? "Kart Kong's at the Banana Speedway, north-west. Break his go-kart." :
                    line == 1 ? "Master Silt is at the Still Water Dojo. Soft footsteps, mechanic." : "The witch's clearing is at the end of the Lantern Road. Mind the clones.");
            if (finaleOffered && !clutchBeaten) options.Add("The Greasy Spoon, up north. And if Clutch runs, chase him down!");
            if (clutchBeaten && !CarryingLandingGear && !wrapUpDone) options.Add("Clutch is beaten? Then bring me that landing gear. He'll have dropped it.");
            if (options.Count == 0) options.Add("Splendid work, mechanic. Truly splendid.");
            bubble.SpeakManual(options[nextReminder++ % options.Count]);
            return true;
        }

        private void Converse(string title, string[] lines, Action done) =>
            DialogueUi.Instance.BeginConversation(cavendish.transform, "Cavendish", title, lines, done);

        // ---------- Steps ----------

        private void OnGreeted()
        {
            greeted = true;
            MudlandsProgress.Set("greeted");
            if (kiosk != null) kiosk.Ready = true;
            foreach (SpiritGong gong in gongs) if (gong != null) gong.Ready = true;
            foreach (CursedLantern lantern in lanterns) if (lantern != null) lantern.Ready = true;
            CatchUp();
            UpdateObjectives();
        }

        private void Advance(int line, Step next)
        {
            steps[line] = next;
            MudlandsProgress.Set($"line{line + 1}.{next}");
            CatchUp();
            UpdateObjectives();
        }

        // Objectives can be done early (the barricades shot before the permit, the dam blown before the gongs, a
        // lantern knocked over before talking to Cavendish). Their events have already fired by the time the line
        // reaches that step, so check whenever a line moves on, or it would wait forever.
        private void CatchUp()
        {
            if (!greeted) return;
            if (steps[0] == Step.Quest1 && pockets != null && pockets.HasKeyItem(KeyItems.LearnersPermit)) Advance(0, Step.Report1);
            if (steps[0] == Step.Quest2 && barricades.All(b => b == null || b.IsBroken) && (still == null || still.IsBroken)) Advance(0, Step.Report2);
            if (steps[1] == Step.Quest1 && gongs.All(g => g == null || g.Rung)) Advance(1, Step.Report1);
            if (steps[1] == Step.Quest2 && (dam == null || dam.IsBroken)) Advance(1, Step.Report2);
            if (steps[2] == Step.Quest1 && lanterns.All(l => l == null || l.Out)) Advance(2, Step.Report1);
        }

        private void ArmBoss(int line)
        {
            Advance(line, Step.Boss);
            MudBossEncounter fight = line == 0 ? kong : line == 1 ? otter : witch;
            if (fight != null) fight.Arm();
            UpdateObjectives();
        }

        private void OnPermit()
        {
            MudlandsProgress.Set("permit");
            if (steps[0] == Step.Quest1) Advance(0, Step.Report1);
        }

        private void OnRoadwork(Destructible _)
        {
            if (steps[0] != Step.Quest2) { UpdateObjectives(); return; }
            if (barricades.All(b => b == null || b.IsBroken) && (still == null || still.IsBroken)) Advance(0, Step.Report2);
            else UpdateObjectives();
        }

        private void OnGong(SpiritGong _)
        {
            if (steps[1] == Step.Quest1 && gongs.All(g => g == null || g.Rung)) Advance(1, Step.Report1);
            else UpdateObjectives();
        }

        private void OnDam(Destructible _)
        {
            if (floodWater != null) floodWater.AddComponent<Drain>();
            if (steps[1] == Step.Quest2) Advance(1, Step.Report2);
            else UpdateObjectives();
        }

        private void OnLantern(CursedLantern _)
        {
            if (steps[2] == Step.Quest1 && lanterns.All(l => l == null || l.Out)) Advance(2, Step.Report1);
            else UpdateObjectives();
        }

        private void OnKong() => Advance(0, Step.BossBeaten);
        private void OnOtter() => Advance(1, Step.BossBeaten);
        private void OnWitch() => Advance(2, Step.BossBeaten);

        private bool AllMiniBossesBeaten => steps.All(s => s == Step.BossBeaten || s == Step.Done);

        private void OnFinaleOffered()
        {
            finaleOffered = true;
            partsGiven = true;
            for (int i = 0; i < 3; i++) steps[i] = Step.Done;
            MudlandsProgress.Set("finale");
            if (gate != null) gate.Open();
            if (clutch != null) clutch.Arm();
            UpdateObjectives();
        }

        private void OnClutch()
        {
            clutchBeaten = true;
            MudlandsProgress.Set("clutch.beaten");
            UpdateObjectives();
        }

        private bool CarryingLandingGear => ShipPartDrop.Recovered.Contains("Landing Gear") && !landingGearGiven;

        private void OnWrapUp()
        {
            wrapUpDone = true;
            landingGearGiven = true;
            StageProgress.RecoverMainPart(MudlandsProgress.MainPart);
            StageProgress.ClearStage(MudlandsProgress.Stage, MudlandsProgress.NextStage);
            MudlandsProgress.Set("complete");
            GameplayOverlay.Instance.SetQuest(Title, "The Mudlands is clear. Set Clutch's aviators on a trophy stand, and rest by the fire.", true);
            Signal(cavendish, false);
        }

        // ---------- Tracker and scanner ----------

        private void UpdateObjectives()
        {
            if (!greeted) return;
            if (wrapUpDone) return;
            var body = new List<string>();
            bool cavendishWaiting = false;

            if (finaleOffered)
            {
                body.Add(clutchBeaten
                    ? (CarryingLandingGear ? "Take the landing gear to Cavendish." : "Pick up the landing gear Clutch dropped.")
                    : "Beat Clutch McCheddar at the Greasy Spoon (north). If he runs, ram him with the car.");
                cavendishWaiting = clutchBeaten && CarryingLandingGear;
            }
            else
            {
                for (int line = 0; line < 3; line++) body.Add(LineSummary(line, ref cavendishWaiting));
                if (AllMiniBossesBeaten) { body.Add("All three are beaten! Report to Cavendish."); cavendishWaiting = true; }
            }
            GameplayOverlay.Instance.SetQuest(Title, string.Join("\n", body));
            Signal(cavendish, cavendishWaiting);

            Signal(kiosk, steps[0] == Step.Quest1);
            foreach (Destructible item in barricades) Signal(item, steps[0] == Step.Quest2 && item != null && !item.IsBroken);
            Signal(still, steps[0] == Step.Quest2 && still != null && !still.IsBroken);
            foreach (SpiritGong gong in gongs) Signal(gong, steps[1] == Step.Quest1 && gong != null && !gong.Rung);
            Signal(dam, steps[1] == Step.Quest2 && dam != null && !dam.IsBroken);
            foreach (CursedLantern lantern in lanterns) Signal(lantern, steps[2] == Step.Quest1 && lantern != null && !lantern.Out);
        }

        private string LineSummary(int line, ref bool cavendishWaiting)
        {
            string name = $"<b>{LineNames[line]}:</b> ";
            switch (steps[line])
            {
                case Step.Quest1:
                    if (line == 0) return name + "take the written test at the Bayou Driving School (Rotwood, west).";
                    if (line == 1) return name + $"ring the spirit gongs in the bog (east). {gongs.Count(g => g != null && g.Rung)} / {gongs.Length}";
                    return name + $"put out the cursed lanterns on the north road. {lanterns.Count(l => l != null && l.Out)} / {lanterns.Length}";
                case Step.Report1:
                case Step.Report2:
                    cavendishWaiting = true;
                    return name + "report back to Cavendish.";
                case Step.Quest2:
                    if (line == 0)
                    {
                        int down = barricades.Count(b => b != null && b.IsBroken);
                        string stillText = still != null && still.IsBroken ? "still blown" : "blow the still";
                        return name + $"clear the Rotwood haul road. Barricades {down} / {barricades.Length}, {stillText}.";
                    }
                    return name + "break the mud dam in the bog (the barrels at its foot might help).";
                case Step.Boss:
                    return name + (line == 0 ? "beat Kart Kong at the Banana Speedway (north-west)." :
                        line == 1 ? "beat Master Silt at the Still Water Dojo (east). He hears you; crouch to move quietly." :
                        "beat the Sloth Witch in Hag's Clearing (north-east).");
                case Step.BossBeaten:
                    return $"<color={UiTheme.Hex(UiTheme.Good)}>[x] {LineNames[line]}</color>";
                default:
                    return "";
            }
        }

        private static void Signal(Component owner, bool on)
        {
            if (owner != null && owner.TryGetComponent(out QuestSignalSource signal)) signal.enabled = on;
        }

        // ---------- Saving ----------

        /// <summary>The story's progress as flags (for the save branch's SaveData.flags).</summary>
        public List<string> CaptureFlags()
        {
            for (int line = 0; line < 3; line++) MudlandsProgress.Set($"line{line + 1}.{steps[line]}");
            return MudlandsProgress.CaptureFlags();
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
