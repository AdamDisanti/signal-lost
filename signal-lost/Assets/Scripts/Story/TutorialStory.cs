using System;
using System.Collections;
using SignalLost.Inventory;
using SignalLost.NPC;
using SignalLost.Player;
using SignalLost.Scanner;
using SignalLost.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SignalLost.Story
{
    /// <summary>
    /// The tutorial's story (meeting notes 10-22), from the crash to the merchant's shop:
    /// <list type="number">
    /// <item>Opening: the mechanic comes to in the burning cockpit, blacks out, catches two glimpses of Cavendish
    /// dragging them across the meadow, and wakes by his campfire with Cavendish sitting across from them.</item>
    /// <item>Cavendish's greeting sends them to search the crash site for their tools. The mining laser and the
    /// scanner are hidden in the wreck; coming back early gets a "keep looking".</item>
    /// <item>With both found, Cavendish tells of a big crab that left the wreck hugging a massive bottle.</item>
    /// <item>The crab (the merchant) waits in a hidden hollow with its ten cousins. Once the crab surrenders and the
    /// cousins are down, it walks back to camp and introduces itself and its shop. The tutorial stops there.</item>
    /// </list>
    /// NPCs ask this story what to say (<see cref="INpcStory"/>); quest signals steer the scanner and the tracker
    /// shows the current objective. Space skips the opening.
    /// </summary>
    public sealed class TutorialStory : MonoBehaviour, INpcStory
    {
        public enum Stage { Opening, Greeting, SearchGear, Fight, MerchantComing, MerchantIntro, Done }

        [Header("Cast")]
        [SerializeField] private NpcActor cavendish;
        [SerializeField] private MerchantCrab merchant;
        [SerializeField] private PlayerLife player;

        [Header("Opening")]
        [Tooltip("Off: start awake by the fire (handy while testing).")]
        [SerializeField] private bool playOpening = true;
        [Tooltip("The mechanic's eyes in the pilot seat.")]
        [SerializeField] private Transform cockpitView;
        [Tooltip("The smashed cockpit window Cavendish hauls the mechanic out through (its forward points out of the ship).")]
        [SerializeField] private Transform cockpitWindow;
        [Tooltip("Where the two glimpses of being dragged start, each facing back toward the wreck.")]
        [SerializeField] private Transform[] dragGlimpses = Array.Empty<Transform>();
        [SerializeField, Min(0.1f)] private float dragSpeed = 1.4f;

        [Header("Objectives")]
        [SerializeField] private ItemPickup laserPickup;
        [SerializeField] private ItemPickup scannerPickup;

        private const string SearchTitle = "Salvage Run";
        private const string CrabTitle = "The Big Crab";

        private static readonly string[] CampMumble = { "Ugh... where...?", "...Is that a capybara?" };
        private static readonly string[] Greeting =
        {
            "Ah! You're awake. Splendid. For a moment there I thought I'd dragged a very dramatic corpse across the meadow.",
            "Cavendish, at your service. Stranded capybara, astronaut and, as of this afternoon, your rescuer.",
            "Gleebinglorb Republic, Maintenance Corps, says your patch. A space mechanic! Now that is a stroke of luck.",
            "Bad news first: your ship came down hard. Good news: it didn't take you with it.",
            "Before we try anything clever we'll need your tools. Search the crash site. Just follow the smoke."
        };
        private static readonly string[] BigCrab =
        {
            "You found them both! Look at you, all tooled up.",
            "Now, I didn't want to worry you earlier, but while I was dragging you out of there...",
            "...a big crab scuttled out of your wreck hugging a massive bottle. Held it like a newborn.",
            "It went north-east, past the rocks. Raise your scanner with Q and it should sniff the creature out.",
            "Fetch that bottle back. And do mind the claws."
        };
        private static readonly string[] MerchantIntro =
        {
            "Right. Introductions. Name's Eugine, proprietor of Bucket & Barter, finest shop on this rock.",
            "The bottle's yours, I suppose. Finders keepers was more of a guideline.",
            "Here's the deal: you bring me scrap, I sell you upgrades. Bits of wreck, critter leftovers, whatever you knock loose.",
            "And by the look of your pockets, my cousins left you plenty. Enough for your first upgrade, I'd say.",
            "Shop's open whenever you are. Just... no more hitting."
        };

        private Stage stage;
        private PlayerGear gear;
        private WakeUpSequence wake;
        private FirstPersonController movement;
        private CharacterController body;
        private FirstPersonHands hands;
        private Transform view;
        private float eyes;
        private bool skipped;
        private int nextReminder;

        public Stage Current => stage;
        public event Action<Stage> StageChanged;

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<PlayerLife>();
            gear = player.GetComponent<PlayerGear>();
            wake = player.GetComponent<WakeUpSequence>();
            movement = player.GetComponent<FirstPersonController>();
            body = player.GetComponent<CharacterController>();
            hands = player.GetComponent<FirstPersonHands>();
            view = player.GetComponentInChildren<Camera>(true).transform;
        }

        private void OnEnable()
        {
            gear.Collected += OnCollected;
            player.Respawned += OnRespawned;
            merchant.ArrivedHome += OnMerchantHome;
        }

        private void OnDisable()
        {
            gear.Collected -= OnCollected;
            player.Respawned -= OnRespawned;
            merchant.ArrivedHome -= OnMerchantHome;
        }

        private void Start()
        {
            Signal(cavendish, false);
            Signal(merchant, false);
            Signal(laserPickup, false);
            if (playOpening) StartCoroutine(Opening());
            else BeginGreeting();
        }

        private void Update()
        {
            if (stage == Stage.Fight)
            {
                if (merchant.IsDefeated && merchant.CousinsDown) EnterMerchantComing();
                else UpdateFightObjective();
            }
        }

        // ---------- NPC conversations (INpcStory) ----------

        public bool TalkTo(NpcActor npc, PlayerInteractor talker)
        {
            if (npc == cavendish) return TalkToCavendish();
            if (npc.Merchant == merchant) return TalkToMerchant();
            return false;
        }

        private bool TalkToCavendish()
        {
            NpcDialogue bubble = cavendish.Dialogue;
            switch (stage)
            {
                case Stage.Opening:
                    return true;
                case Stage.Greeting:
                    Converse(cavendish.transform, "Cavendish", "A Rude Awakening", Greeting, EnterSearch);
                    return true;
                case Stage.SearchGear when HasAllGear:
                    Converse(cavendish.transform, "Cavendish", CrabTitle, BigCrab, EnterFight);
                    return true;
                case Stage.SearchGear:
                    return Remind(bubble, gear.Has(PlayerItem.Scanner)
                        ? new[] { "Ooh, a scanner! Splendid. Now find that laser of yours; let the scanner do the sniffing." }
                        : gear.Has(PlayerItem.MiningLaser)
                            ? new[] { "A laser! Excellent. But there's more in that wreck, I'm sure of it. Keep looking." }
                            : new[] { "Back already? Your tools are still out there. Have a proper rummage around the wreck.",
                                      "Keep looking, mechanic. Follow the smoke; the wreck won't search itself." });
                case Stage.Fight:
                    return Remind(bubble, new[] { "North-east, past the rocks. That crab can't have gone far with a bottle that size." });
                case Stage.MerchantComing:
                    return Remind(bubble, new[] { "Is that crab following you home? How marvellous. I'll put the kettle on." });
                case Stage.MerchantIntro:
                    return Remind(bubble, new[] { "Go on, say hello to our new friend. I'll be right here." });
                default:
                    return false; // his own small talk
            }
        }

        private bool TalkToMerchant()
        {
            if (stage == Stage.MerchantComing)
                return Remind(merchant.Dialogue, new[] { "Walk now, talk later. Camp first!" });
            if (stage != Stage.MerchantIntro || !merchant.IsHome) return false;
            Converse(merchant.transform, "Eugine", "Bucket & Barter", MerchantIntro, EnterDone);
            return true;
        }

        // Short overhead lines (E to continue); repeat visits cycle through the options.
        private bool Remind(NpcDialogue bubble, string[] options)
        {
            if (bubble == null) return false;
            if (bubble.AdvanceManual()) return true;
            bubble.SpeakManual(options[nextReminder++ % options.Length]);
            return true;
        }

        private static void Converse(Transform owner, string speaker, string title, string[] lines, Action done) =>
            DialogueUi.Instance.BeginConversation(owner, speaker, title, lines, done);

        // ---------- Stages ----------

        private void SetStage(Stage next)
        {
            stage = next;
            StageChanged?.Invoke(next);
        }

        private void BeginGreeting()
        {
            SetStage(Stage.Greeting);
            GameplayOverlay.Instance.SetQuest("A Rude Awakening", "Talk to Cavendish.");
            Signal(cavendish, true);
            Converse(cavendish.transform, "Cavendish", "A Rude Awakening", Greeting, EnterSearch);
        }

        private void EnterSearch()
        {
            SetStage(Stage.SearchGear);
            Signal(cavendish, false);
            UpdateSearchObjective();
        }

        private bool HasAllGear => gear.Has(PlayerItem.MiningLaser) && gear.Has(PlayerItem.Scanner);

        private void OnCollected(PlayerItem item)
        {
            if (stage == Stage.SearchGear) UpdateSearchObjective();
        }

        private void UpdateSearchObjective()
        {
            bool laser = gear.Has(PlayerItem.MiningLaser), scanner = gear.Has(PlayerItem.Scanner);
            if (laser && scanner)
            {
                GameplayOverlay.Instance.SetQuest(SearchTitle, "Got everything. Head back to Cavendish's camp.");
                Signal(laserPickup, false);
                Signal(cavendish, true);
                return;
            }
            GameplayOverlay.Instance.SetQuest(SearchTitle,
                "Search the crash site for your tools. Follow the smoke.\n" + Check(laser, "Mining laser") + "\n" + Check(scanner, "Scanner"));
            Signal(laserPickup, scanner && !laser); // once the scanner is back, it points at the laser
        }

        private static string Check(bool done, string label) =>
            done ? $"<color={UiTheme.Hex(UiTheme.Good)}>[x] {label}</color>" : "[  ] " + label;

        private void EnterFight()
        {
            SetStage(Stage.Fight);
            Signal(cavendish, false);
            Signal(merchant, true);
            merchant.BeginQuest();
            UpdateFightObjective();
        }

        private void UpdateFightObjective()
        {
            int left = 0;
            foreach (var cousin in merchant.Cousins) if (cousin != null && !cousin.IsDown) left++;
            string crab = merchant.IsDefeated ? "[x] The big crab gave up" : "[  ] Get the bottle back from the big crab";
            string cousins = left > 0 ? $"[  ] Bucket Brothers still swinging: {left}" : "[x] Bucket Brothers";
            GameplayOverlay.Instance.SetQuest(CrabTitle,
                "Find the crab north-east of camp, past the rocks. Raise the scanner with Q.\n" + crab + "\n" + cousins);
        }

        private void EnterMerchantComing()
        {
            SetStage(Stage.MerchantComing);
            Signal(merchant, false);
            GameplayOverlay.Instance.SetQuest(CrabTitle, gear.Has(PlayerItem.Flask)
                ? "Follow the crab back to camp."
                : "Grab your bottle, then follow the crab back to camp.");
        }

        private void OnMerchantHome()
        {
            if (stage != Stage.MerchantComing) return;
            SetStage(Stage.MerchantIntro);
            Signal(merchant, true);
            GameplayOverlay.Instance.SetQuest(CrabTitle, "Talk to the crab (Eugine) at camp.");
            if (Vector3.Distance(player.transform.position, merchant.transform.position) < 5.5f) TalkToMerchant();
        }

        private void EnterDone()
        {
            SetStage(Stage.Done);
            Signal(merchant, false);
            merchant.MarkGreeted();
            GameplayOverlay.Instance.SetQuest(CrabTitle, "Bottle recovered. Spend your scrap at Bucket & Barter.", true);
        }

        private void OnRespawned()
        {
            merchant.ResetAfterPlayerRespawn();
            if (stage == Stage.MerchantComing && merchant.IsHome) OnMerchantHome();
        }

        private static void Signal(Component owner, bool on)
        {
            if (owner != null && owner.TryGetComponent(out QuestSignalSource signal)) signal.enabled = on;
        }

        // ---------- Opening ----------

        private IEnumerator Opening()
        {
            SetStage(Stage.Opening);
            Transform cav = cavendish.transform;
            Vector3 seat = cav.position;
            Quaternion seatFacing = cav.rotation;
            movement.enabled = false;
            body.enabled = false;
            ShowHands(false);
            skipped = false;
            eyes = 0f;
            wake.SetEyes(0f);
            wake.SetHint("SPACE  skip");
            DialogueUi ui = DialogueUi.Instance;

            // 1. The cockpit: alarms, sparks and smoke through half-open eyes.
            if (cockpitView != null)
            {
                PlaceEyes(cockpitView.position, cockpitView.rotation);
                yield return Wait(1f);
                Say(ui, "Mechanic", "...ngh...");
                yield return EyesTo(0.3f, 0.8f);
                yield return Wait(0.6f);
                yield return EyesTo(0.05f, 0.5f);
                Say(ui, "Mechanic", "Why is everything... on fire...?");
                yield return EyesTo(0.55f, 0.9f);
                yield return Wait(1.8f);
                if (cockpitWindow != null)
                {
                    yield return PulledOut();
                }
                else
                {
                    yield return EyesTo(0f, 1f);
                    yield return Wait(0.8f);
                    Say(ui, "Cavendish", "Good heavens. Right, out you come, mechanic. Upsy-daisy.");
                    yield return Wait(2.6f);
                }
            }

            // 2. Dragged across the meadow: heels first, watching the wreck smoke behind, then a glimpse straight up
            //    at Cavendish's face, upside down.
            for (int i = 0; i < dragGlimpses.Length && !skipped; i++)
            {
                Transform glimpse = dragGlimpses[i];
                bool lookUp = i == dragGlimpses.Length - 1;
                Vector3 back = glimpse.forward;
                back.y = 0f;
                back.Normalize();
                Coroutine drag = StartCoroutine(Drag(glimpse, back, lookUp));
                yield return EyesTo(lookUp ? 0.5f : 0.4f, 0.8f);
                if (lookUp)
                {
                    Say(ui, "Cavendish", "You're heavier than you look, you know.");
                    yield return Wait(2.4f);
                    Say(ui, "Mechanic", "...capybara...?");
                    yield return Wait(1.6f);
                    Say(ui, "Cavendish", "Shh. Nearly there. Have a little nap.");
                }
                else
                {
                    Say(ui, "Cavendish", "Hang on, mate. I've got you.");
                }
                yield return Wait(2.2f);
                yield return EyesTo(0f, 0.9f);
                StopCoroutine(drag);
                yield return Wait(0.9f);
            }

            // 3. Awake by the fire, Cavendish across from them.
            if (skipped) ui.ClearSubtitle();
            cav.SetPositionAndRotation(seat, seatFacing);
            Transform spawn = PlayerSpawnPoint.Current != null ? PlayerSpawnPoint.Current.transform : player.transform;
            player.transform.SetPositionAndRotation(spawn.position, Quaternion.Euler(0f, spawn.eulerAngles.y, 0f));
            view.localRotation = Quaternion.identity;
            body.enabled = true;
            ShowHands(true);
            wake.HideEyes();
            wake.Play(true, CampMumble);
            yield return new WaitUntil(() => !wake.Playing);
            BeginGreeting();
        }

        // Cavendish's face rises into the smashed side window; he reaches in and hauls the mechanic out through it, the
        // view tumbling past the frame and down onto the grass beside the wreck, then the eyes close.
        private IEnumerator PulledOut()
        {
            Transform cav = cavendish.transform;
            DialogueUi ui = DialogueUi.Instance;
            Vector3 window = cockpitWindow.position;
            Vector3 outward = Vector3.ProjectOnPlane(cockpitWindow.forward, Vector3.up).normalized;
            float head = CavendishHeadHeight();
            Quaternion facingIn = Quaternion.LookRotation(-outward);

            // He clambers up outside the window and peers in; the mechanic's eyes drift toward him.
            Vector3 peek = window + outward * 0.8f + Vector3.up * (0.1f - head);
            Vector3 below = peek + Vector3.down * 0.7f;
            Vector3 eyeStart = view.position;
            Quaternion lookStart = view.rotation;
            Quaternion lookAtWindow = Quaternion.LookRotation(window + Vector3.up * 0.1f - eyeStart);
            for (float t = 0f; t < 1f && !CheckSkip(); t += Time.deltaTime / 1.1f)
            {
                float k = Mathf.SmoothStep(0f, 1f, t);
                view.rotation = Quaternion.Slerp(lookStart, lookAtWindow, k * 0.85f);
                cav.SetPositionAndRotation(Vector3.Lerp(below, peek, k), facingIn);
                yield return null;
            }
            Say(ui, "Cavendish", "Good heavens. Right, out you come, mechanic. Upsy-daisy.");
            yield return Wait(1.9f);

            // Hauled out head first through the frame, over the sill and down to the ground, looking up past him at
            // the sky and the smoking wreck.
            Vector3 landing = window + outward * 2.8f;
            landing.y = GroundBelow(landing) + 0.55f;
            Vector3 over = window + outward * 0.7f + Vector3.up * 0.35f;
            Quaternion lookOut = Quaternion.LookRotation(Vector3.up * 1.2f - outward, outward);
            Vector3 standing = landing + outward * 0.9f;
            standing.y = GroundBelow(standing);
            for (float t = 0f; t < 1f && !CheckSkip(); t += Time.deltaTime / 1.5f)
            {
                float k = t * t * (3f - 2f * t);
                Vector3 eye = (1f - k) * (1f - k) * eyeStart + 2f * (1f - k) * k * over + k * k * landing;
                float tumble = Mathf.Sin(t * Mathf.PI) * 18f;
                PlaceEyes(eye, Quaternion.Slerp(lookAtWindow, lookOut, k) * Quaternion.Euler(tumble * 0.4f, 0f, tumble));
                // He backs away from the window, pulling, and ends up standing over the mechanic.
                cav.SetPositionAndRotation(Vector3.Lerp(peek, standing, Mathf.SmoothStep(0f, 1f, t * 1.3f)), facingIn);
                yield return null;
            }
            Say(ui, "Mechanic", "...hhh...");
            yield return Wait(0.9f);
            yield return EyesTo(0f, 1f);
            yield return Wait(0.8f);
        }

        // Roughly how far Cavendish's head is above his feet (he has no rig), for lining his face up with the window.
        private float CavendishHeadHeight()
        {
            Transform cav = cavendish.transform;
            var bounds = new Bounds(cav.position, Vector3.zero);
            foreach (Renderer part in cavendish.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(part.bounds);
            return Mathf.Clamp(bounds.max.y - cav.position.y - 0.15f, 0.8f, 2.5f);
        }

        // Moves the mechanic (lying down, eyes at the glimpse's height) away from the wreck at walking pace, with
        // Cavendish backing along just ahead, bobbing with each step.
        private IEnumerator Drag(Transform glimpse, Vector3 back, bool lookUp)
        {
            Transform cav = cavendish.transform;
            Quaternion look = lookUp ? Quaternion.LookRotation(back) * Quaternion.Euler(-122f, 0f, 0f) : glimpse.rotation;
            for (float travelled = 0f; ; travelled += dragSpeed * Time.deltaTime)
            {
                Vector3 eye = glimpse.position - back * travelled;
                eye.y = GroundBelow(eye) + 0.55f;
                float sway = Mathf.Sin(Time.time * 4.2f);
                PlaceEyes(eye, look * Quaternion.Euler(sway * 1.5f, 0f, sway * 2.5f));
                Vector3 at = eye - back * 0.95f;
                at.y = GroundBelow(at) + Mathf.Abs(sway) * 0.06f;
                cav.SetPositionAndRotation(at, Quaternion.LookRotation(back) * Quaternion.Euler(12f, 0f, sway * 4f));
                yield return null;
            }
        }

        private void PlaceEyes(Vector3 eye, Quaternion look)
        {
            Transform root = player.transform;
            root.rotation = Quaternion.Euler(0f, look.eulerAngles.y, 0f);
            root.position += eye - view.position;
            view.rotation = look;
        }

        private static float GroundBelow(Vector3 point) =>
            Physics.Raycast(point + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore) &&
            hit.collider.GetComponentInParent<PlayerLife>() == null && hit.collider.GetComponentInParent<NpcActor>() == null
                ? hit.point.y
                : point.y - 0.55f;

        private void ShowHands(bool show)
        {
            if (hands == null) return;
            if (hands.RightHand != null) hands.RightHand.gameObject.SetActive(show);
            if (hands.LeftHand != null) hands.LeftHand.gameObject.SetActive(show);
        }

        private void Say(DialogueUi ui, string speaker, string line)
        {
            if (!skipped) ui.Subtitle(speaker, line, 3f);
        }

        private IEnumerator EyesTo(float open, float seconds)
        {
            float from = eyes;
            for (float t = 0f; t < seconds && !CheckSkip(); t += Time.deltaTime)
            {
                eyes = Mathf.Lerp(from, open, Mathf.SmoothStep(0f, 1f, t / seconds));
                wake.SetEyes(eyes);
                yield return null;
            }
            eyes = skipped ? 0f : open;
            wake.SetEyes(eyes);
        }

        private IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds && !CheckSkip(); t += Time.deltaTime) yield return null;
        }

        private bool CheckSkip()
        {
            if (!skipped && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) skipped = true;
            return skipped;
        }

        /// <summary>Skips the opening (as if Space was pressed). For tests and debugging.</summary>
        public void SkipOpening() => skipped = true;
    }
}
