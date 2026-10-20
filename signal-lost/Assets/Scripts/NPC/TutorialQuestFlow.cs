using System;
using System.Collections;
using SignalLost.Player;
using SignalLost.Scanner;
using SignalLost.UI;
using UnityEngine;

namespace SignalLost.NPC
{
    /// <summary>
    /// The tutorial's story, one step at a time (meeting notes 2026-10-20, "Crash site" 3-7):
    /// wake up beside Cavendish, find the mining laser and scanner, return to him, beat the crab that stole the
    /// flask, follow it home, hear its scrap-and-shop pitch, then report back. Quest information is always given
    /// in popups the player opens with E; idle chatter uses speech bubbles and subtitles.
    /// Progress lasts for the Play session (deaths and blackouts keep it); there is no save yet.
    /// </summary>
    public sealed class TutorialQuestFlow : MonoBehaviour
    {
        public enum Step
        {
            WakeUp, MeetCavendish, FindGear, ReturnToCavendish, RecoverFlask, CollectFlask,
            FollowMerchant, MeetMerchant, ReportToCavendish, Complete
        }

        [SerializeField] private TutorialPlayer player;
        [SerializeField] private NpcDialogue cavendish;
        [SerializeField] private MerchantCrab merchant;
        [SerializeField] private WakeUpSequence wakeUp;
        [Tooltip("Play the wake-up scene and Cavendish's greeting when the tutorial starts.")]
        [SerializeField] private bool playIntro = true;

        [Header("Scanner objectives (one is switched on per step)")]
        [SerializeField] private QuestSignalSource cavendishSignal;
        [SerializeField] private QuestSignalSource laserSignal;
        [SerializeField] private QuestSignalSource merchantSignal;
        [SerializeField] private QuestSignalSource flaskSignal;

        private DialogueUi ui;
        private int smallTalk;

        public Step Current { get; private set; }
        public bool Accepted => Current >= Step.FindGear;
        public bool FlaskRecovered => player != null && player.HasFlask;
        public event Action<Step> StepChanged;

        private static readonly string[][] SmallTalk =
        {
            new[] { "Another lovely day stranded in paradise, eh?" },
            new[] { "Don't mind me. Capybaras are excellent at standing still." },
            new[] { "That crab overcharges, by the way. Just so you know." }
        };

        public string QuestTitle => Current <= Step.MeetCavendish ? "Crash Landing" :
            Current <= Step.ReturnToCavendish ? "Gear Up" :
            Current < Step.Complete ? "Hair of the Crab" : "Tutorial Complete";

        public string Objective
        {
            get
            {
                switch (Current)
                {
                    case Step.WakeUp: return "Wake up...";
                    case Step.MeetCavendish: return "Talk to Cavendish (E).";
                    case Step.FindGear:
                        return Check(player.HasLaser) + " Mining laser: the scorched rock basin\n" +
                               Check(player.HasScanner) + " Scanner: somewhere in the wreckage";
                    case Step.ReturnToCavendish: return "Got everything. Return to Cavendish (E).";
                    case Step.RecoverFlask: return "Defeat the crab with the copper bucket.\nQ: your scanner points the way.";
                    case Step.CollectFlask: return "Pick up your flask (E).";
                    case Step.FollowMerchant: return "Follow the crab back to the crash site.";
                    case Step.MeetMerchant: return "Talk to the merchant crab (E).";
                    case Step.ReportToCavendish: return "Report back to Cavendish (E).";
                    default: return "Explore the crash site. Critters drop scrap; the crab trades.";
                }
            }
        }

        private static string Check(bool done) => done ? "<color=#8FE6A8>[x]</color>" : "[  ]";

        private void Awake()
        {
            if (player == null) player = FindFirstObjectByType<TutorialPlayer>();
            if (merchant == null) merchant = FindFirstObjectByType<MerchantCrab>();
            if (cavendish == null)
                foreach (NpcActor actor in FindObjectsByType<NpcActor>())
                    if (actor.IsQuestGiver && actor.gameObject.scene == gameObject.scene)
                    { cavendish = actor.GetComponentInChildren<NpcDialogue>(true); break; }
            if (wakeUp == null && player != null) wakeUp = player.GetComponent<WakeUpSequence>();
            if (player == null || cavendish == null || merchant == null)
            {
                Debug.LogError("TutorialQuestFlow needs the tutorial player, Cavendish's dialogue and the merchant crab.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (player != null) player.Respawned += OnPlayerRespawned;
        }

        private void OnDisable()
        {
            if (player != null) player.Respawned -= OnPlayerRespawned;
        }

        private void Start()
        {
            ui = DialogueUi.Instance;
            ui.BindPlayer(player.transform);
            if (playIntro && wakeUp != null) StartCoroutine(Intro());
            else Go(Step.MeetCavendish);
        }

        private IEnumerator Intro()
        {
            Go(Step.WakeUp);
            wakeUp.Play(true);
            yield return null;
            while (wakeUp.Playing) yield return null;
            cavendish.Speak(3.6f,
                "Oh, thank goodness, you're alive! That was quite the landing.",
                "Well. Crashing. It was quite the crashing.",
                "Name's Cavendish. Come talk to me when the world stops spinning.");
            Go(Step.MeetCavendish);
        }

        private void Go(Step next)
        {
            if (Current == next && next != Step.WakeUp) return;
            Current = next;
            StepChanged?.Invoke(next);
        }

        private void Update()
        {
            // Steps that finish through play rather than conversation.
            if (Current == Step.FindGear && player.HasLaser && player.HasScanner)
            {
                Go(Step.ReturnToCavendish);
                DialogueUi.Instance.Subtitle("Mechanic", "Laser, scanner... right. Back to the capybara.", 3.5f);
            }
            else if (Current == Step.RecoverFlask && merchant.IsDefeated) Go(Step.CollectFlask);
            else if (Current == Step.CollectFlask && player.HasFlask) Go(merchant.IsHome ? Step.MeetMerchant : Step.FollowMerchant);
            else if (Current == Step.FollowMerchant && merchant.IsHome) Go(Step.MeetMerchant);

            GameplayOverlay.Instance.SetQuest(QuestTitle, Objective, Current == Step.Complete);
            PointScanner(ScannerObjective);
        }

        /// <summary>What the scanner should point at for the current step: whoever or whatever the quest needs next.</summary>
        public QuestSignalSource ScannerObjective
        {
            get
            {
                switch (Current)
                {
                    case Step.WakeUp:
                    case Step.MeetCavendish:
                    case Step.ReturnToCavendish:
                    case Step.ReportToCavendish: return cavendishSignal;
                    case Step.FindGear: return player.HasLaser ? null : laserSignal;
                    case Step.RecoverFlask:
                    case Step.FollowMerchant:
                    case Step.MeetMerchant: return merchantSignal;
                    case Step.CollectFlask: return flaskSignal;
                    default: return null; // tutorial done: the scanner goes back to finding critters
                }
            }
        }

        private void PointScanner(QuestSignalSource objective)
        {
            SetSignal(cavendishSignal, objective);
            SetSignal(laserSignal, objective);
            SetSignal(merchantSignal, objective);
            SetSignal(flaskSignal, objective);
        }

        private static void SetSignal(QuestSignalSource signal, QuestSignalSource objective)
        {
            if (signal != null && signal.enabled != (signal == objective)) signal.enabled = signal == objective;
        }

        // ---------- Cavendish ----------

        public void TalkToCavendish()
        {
            if (ui == null) ui = DialogueUi.Instance;
            Transform owner = cavendish.transform.parent;
            if (cavendish.AdvanceManual()) return;
            switch (Current)
            {
                case Step.WakeUp:
                    return;
                case Step.MeetCavendish:
                    ui.BeginConversation(owner, "Cavendish", "Quest: Gear Up", new[]
                    {
                        "Easy there, mechanic. You blacked out at the controls and planted your ship nose-first into my favourite crater.",
                        "Your gear went flying. Your mining laser landed in that scorched rock basin right by me. It's a cutter, a club and a cannon all in one.",
                        "Your scanner's out in the wreckage, toward the ship. It sniffs out ship parts, critters, and anybody who needs a hand. Like me!",
                        "Grab both, then come back. I'll be right here. Capybaras are excellent at standing still."
                    }, () => Go(Step.FindGear), "Let's go!");
                    return;
                case Step.FindGear:
                    ui.BeginConversation(owner, "Cavendish", "Gear Up", new[]
                    {
                        !player.HasLaser && !player.HasScanner ? "Laser's in the scorched basin next to me. Scanner's out in the wreckage, look for the blinking light." :
                        !player.HasLaser ? "Your laser's in the scorched rock basin, right next to me." :
                        "Your scanner's out in the wreckage toward the ship. Look for the blinking teal light."
                    });
                    return;
                case Step.ReturnToCavendish:
                    ui.BeginConversation(owner, "Cavendish", "Quest: Hair of the Crab", new[]
                    {
                        "Laser AND scanner? Look at you, a functioning adult.",
                        "Now, you'll want your flask. A big crab in a copper bucket waddled off with it while you were snoring.",
                        "It went east. Press Q to raise your scanner and follow the arrow. Q again swaps back to your laser.",
                        "Fair warning: crabs hide in their buckets when you charge a shot. A good whack knocks them right out."
                    }, () => { merchant.BeginQuest(); Go(Step.RecoverFlask); }, "On it!");
                    return;
                case Step.RecoverFlask:
                    ui.BeginConversation(owner, "Cavendish", "Hair of the Crab", new[]
                    { "The big crab with the copper bucket, east of here. Q raises your scanner; follow the arrow." });
                    return;
                case Step.CollectFlask:
                    ui.BeginConversation(owner, "Cavendish", "Hair of the Crab", new[]
                    { "It dropped your flask where you fought. Go grab it with E!" });
                    return;
                case Step.FollowMerchant:
                case Step.MeetMerchant:
                    ui.BeginConversation(owner, "Cavendish", "Hair of the Crab", new[]
                    { "Go chat with our new crustacean friend. Something about a business proposal." });
                    return;
                case Step.ReportToCavendish:
                    ui.BeginConversation(owner, "Cavendish", "Quest Complete", new[]
                    {
                        "Laser, scanner and... your flask. Priorities, mechanic. I respect it.",
                        "F takes a sip to heal, but each sip does less while you're still tipsy. Too many and you'll be napping again.",
                        "Your ship's missing its big parts: the engine core, nav computer and flight controls. Critters out there grabbed them.",
                        "And the reactor core. No idea where THAT went. None at all. Anyway! Let's get you flying again."
                    }, () => Go(Step.Complete), "Finish tutorial");
                    return;
                default:
                    cavendish.SpeakManual(SmallTalk[smallTalk++ % SmallTalk.Length]);
                    return;
            }
        }

        // ---------- Merchant crab ----------

        /// <summary>Handles E on the friendly crab while the tutorial needs it. Returns false to let it run its shop.</summary>
        public bool TalkToMerchant()
        {
            if (ui == null) ui = DialogueUi.Instance;
            if (Current == Step.CollectFlask || Current == Step.FollowMerchant)
            {
                merchant.Dialogue.Speak(3.5f, player.HasFlask ? "Not here! Follow me back to the crash site." : "Your flask's on the ground over there. Grab it, then follow me!");
                return true;
            }
            if (Current != Step.MeetMerchant) return false;
            ui.BeginConversation(merchant.transform, "Merchant Crab", "Bucket & Barter", new[]
            {
                "Welcome to Bucket & Barter, the finest shop in this crater. Also the only one.",
                "See, every critter round here has bits of your ship stuck in it. Bonk 'em, and the scrap they drop flies right into your pockets.",
                "Four kinds: Scrap Metal, Wiring, Circuit Boards and Fuel Cells. Your pockets sort 'em for you. Press Tab to see what you're carrying.",
                "Bring me scrap and I'll soup up that sad little laser, your scanner, even your liver. Shelves are still being stocked, but have a look!"
            }, () => { Go(Step.ReportToCavendish); merchant.OpenShop(false); }, "Show me the goods");
            return true;
        }

        // ---------- Resets ----------

        private void OnPlayerRespawned()
        {
            if (wakeUp != null) wakeUp.Play(false);
        }

        public void OnStageReset()
        {
            ui?.CloseAll();
            merchant.ResetAfterPlayerRespawn();
        }
    }
}
