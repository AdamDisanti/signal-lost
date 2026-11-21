using System;
using System.Collections.Generic;
using SignalLost.Audio;
using SignalLost.Effects;
using SignalLost.Inventory;
using SignalLost.Player;
using SignalLost.UI;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// Quest "The Written Test": the Bayou Driving School's old test kiosk. The first E hands you the Bayou Driver's
    /// Handbook (eight swamp road rules, which double as Drunk Driving's rules: what the car smashes, what blows it up,
    /// how to get out); the next starts the test: three questions drawn from the handbook, answers shuffled. Two right
    /// passes, and the kiosk stamps the Learner's Permit into the pockets. Fewer fails: the kiosk sparks, spits out a
    /// Mudpuppy "examiner", and hands back the handbook for a retake with new questions.
    /// </summary>
    public sealed class DrivingTestKiosk : MonoBehaviour, IInteractable
    {
        private struct Question
        {
            public string Text, Rule;
            public string[] Answers;
            public int Right;
            public string Cheer;
        }

        // Each question is one handbook rule; a wrong answer shows the rule.
        private static readonly Question[] Pool =
        {
            new Question { Text = "A mudpuppy is crossing the road. You:", Answers = new[] { "Honk twice.", "Brake and wait for it.", "Get out and carry it across." }, Right = 0,
                Rule = "Mudpuppies have right of way. Honk twice and they give it back.", Cheer = "Correct! They love that." },
            new Question { Text = "A Hog Brute is standing in the road. You:", Answers = new[] { "Go round him.", "Ram him. Momentum!", "Flash your headlights." }, Right = 0,
                Rule = "Hog Brutes are not speed bumps. Hit anything that big and the car blows.", Cheer = "Correct. You'll live longer." },
            new Question { Text = "A wooden fence is across the road. You:", Answers = new[] { "Drive straight through it.", "Stop and climb over.", "Knock and ask the owner." }, Right = 0,
                Rule = "Wood is not an obstacle, it is a suggestion. Drive through it.", Cheer = "Correct! Wood is a suggestion." },
            new Question { Text = "The road dips into bog water. You:", Answers = new[] { "Go round it.", "Floor it. The car can swim.", "Honk at the water." }, Right = 0,
                Rule = "The car does not swim. Bog water stalls it.", Cheer = "Correct. The car does not swim." },
            new Question { Text = "How do you start the car?", Answers = new[] { "Chug your flask.", "Turn the key.", "Hot-wire it." }, Right = 0,
                Rule = "Your flask is the ignition. Hold F, chug, and the car arrives.", Cheer = "Correct. Bottoms up." },
            new Question { Text = "How do you get out of a moving car?", Answers = new[] { "Press F.", "Wait until it stops.", "Open the door politely." }, Right = 0,
                Rule = "To leave a moving car, press F. The car won't survive. You will.", Cheer = "Correct. Tuck and roll." },
            new Question { Text = "Where may you park at camp?", Answers = new[] { "Nowhere. Everyone's sleeping.", "By the fire.", "On Cavendish." }, Right = 0,
                Rule = "No parking at camp. Everyone's sleeping.", Cheer = "Correct. Shh." },
            new Question { Text = "A red light means:", Answers = new[] { "Finish your drink first.", "Stop.", "The light is broken." }, Right = 0,
                Rule = "A red light means: finish your drink first.", Cheer = "Correct, locally." }
        };

        private const int Asked = 3, PassMark = 2;

        [Tooltip("Spat out when you fail (a Mudpuppy prefab).")]
        [SerializeField] private GameObject examiner;

        private readonly List<int> picked = new List<int>();
        private int[] order = Array.Empty<int>();
        private int question = -1, right;
        private bool passed, handbookRead;
        private float retakeAt;

        /// <summary>The quest is on (the story sets it).</summary>
        public bool Ready { get; set; }
        public bool Passed => passed;
        public int Attempts { get; private set; }
        public int Failures { get; private set; }
        public event Action PassedTest;

        public void Configure(GameObject examinerPrefab) => examiner = examinerPrefab;

        public string Prompt => !Ready || passed || question >= 0 ? "" : !handbookRead ? "Read the Bayou Driver's Handbook" :
            Time.time < retakeAt ? "Rebooting..." : Attempts > 0 ? "Retake the written test" : "Take the written test";

        public bool CanInteract(PlayerInteractor player) => isActiveAndEnabled && Ready && !passed && question < 0 && !MudHud.Asking &&
                                                            (!handbookRead || Time.time >= retakeAt);

        public bool Interact(PlayerInteractor player)
        {
            if (!CanInteract(player)) return false;
            Sfx.Play("ui_open", 0.8f);
            if (!handbookRead)
            {
                handbookRead = true;
                ShowHandbook();
                return true;
            }
            Attempts++;
            right = 0;
            question = 0;
            // Three different rules, not the ones from last time if there are enough left.
            var choices = new List<int>();
            for (int i = 0; i < Pool.Length; i++) if (!picked.Contains(i)) choices.Add(i);
            if (choices.Count < Asked) { picked.Clear(); choices.Clear(); for (int i = 0; i < Pool.Length; i++) choices.Add(i); }
            order = new int[Asked];
            for (int i = 0; i < Asked; i++)
            {
                int at = Random.Range(0, choices.Count);
                order[i] = choices[at];
                picked.Add(choices[at]);
                choices.RemoveAt(at);
            }
            Ask();
            return true;
        }

        private static void ShowHandbook()
        {
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < Pool.Length; i++) text.Append(i + 1).Append(". ").AppendLine(Pool[i].Rule);
            text.Append($"Test: {Asked} questions, {PassMark} right to pass.");
            GameplayOverlay.Instance.ShowItemCard("Driver's Handbook", text.ToString(), new Color(1f, 0.78f, 0.34f), "READ BEFORE THE TEST");
        }

        // The answers in a random order (the right one isn't always first).
        private int[] shuffle = Array.Empty<int>();

        private void Ask()
        {
            Question q = Pool[order[question]];
            shuffle = new[] { 0, 1, 2 };
            for (int i = 2; i > 0; i--) { int j = Random.Range(0, i + 1); (shuffle[i], shuffle[j]) = (shuffle[j], shuffle[i]); }
            var answers = new string[3];
            for (int i = 0; i < 3; i++) answers[i] = q.Answers[shuffle[i]];
            MudHud.Ask($"BAYOU DRIVING SCHOOL  -  QUESTION {question + 1} OF {Asked}", q.Text, answers, Answered);
        }

        private void Answered(int choice)
        {
            Question q = Pool[order[question]];
            bool correct = shuffle[choice] == q.Right;
            if (correct)
            {
                right++;
                MudHud.Toast(q.Cheer, new Color(0.6f, 1f, 0.6f), 1.8f);
                Sfx.Play("ui_click", 0.8f);
            }
            else
            {
                MudHud.Toast("Wrong! Rule: " + q.Rule, new Color(1f, 0.55f, 0.45f), 3.2f);
                Sfx.PlayAt("electric_sizzle", transform.position + Vector3.up * 1.5f, 0.5f, 1.4f, 20f);
            }
            question++;
            // A beat to read the feedback (the next question's panel would cover it).
            StartCoroutine(Next(correct ? 1.1f : 2.6f));
        }

        private System.Collections.IEnumerator Next(float pause)
        {
            yield return new WaitForSeconds(pause);
            if (question < Asked) { Ask(); yield break; }
            question = -1;
            if (right >= PassMark) Pass();
            else Fail();
        }

        private void Pass()
        {
            passed = true;
            Sfx.Play("quest_chime", 1f);
            MudHud.Toast($"Passed, {right} / {Asked}!", new Color(0.6f, 1f, 0.6f), 3f);
            var pockets = PlayerInventory.Current;
            if (pockets != null) pockets.AddKeyItem(KeyItems.LearnersPermit);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, transform.position + Vector3.up * 1.5f, Vector3.up, 30, 0.08f, 3f, 70f, new Color(1f, 0.85f, 0.4f));
            GameplayOverlay.Instance.ShowItemCard("Learner's Permit",
                "\"Everyone passes. Eventually.\" The photo is a thumb.\n" +
                "<b>Hold F</b>  chug your scotch flask dry, and a car drops out of the sky. Drive it until it blows up.\n" +
                "<b>WASD</b> drive, <b>Space</b> handbrake, <b>click</b> horn, <b>F</b> bail out.\n" +
                "Run over small critters and smash through wood. Hit anything big or solid head-on and it explodes, stunning everything nearby.\n" +
                "Costs 2 drinks (no healing), then 30 s before the next car.",
                new Color(1f, 0.78f, 0.34f), "NEW ABILITY: DRUNK DRIVING");
            PassedTest?.Invoke();
        }

        private void Fail()
        {
            Failures++;
            handbookRead = false; // the next E hands the handbook back, then the retake
            retakeAt = Time.time + 2f;
            Sfx.PlayAt("electric_sizzle", transform.position + Vector3.up * 1.5f, 1f, 0.9f, 30f);
            Sfx.PlayAt("pup_squeak", transform.position, 0.8f, 1.2f, 30f);
            MudHud.Shout($"FAILED  {right} / {Asked}", new Color(1f, 0.45f, 0.35f), 2.4f);
            MudHud.Toast("Read the handbook again (E), then retake the test.", new Color(1f, 0.82f, 0.45f), 3.5f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, transform.position + Vector3.up * 1.6f, Vector3.up, 40, 0.08f, 5f, 80f, new Color(1f, 0.85f, 0.4f));
            ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, transform.position + Vector3.up * 1.8f, Vector3.up, 20, 0.6f, 2f, 60f, new Color(0.3f, 0.3f, 0.3f, 0.7f));
            if (examiner == null) return;
            // The "examiner": a Mudpuppy out of the kiosk's slot (only a couple at a time).
            int awake = 0;
            foreach (MudCritter critter in MudCritter.Living) if (critter != null && critter.name.StartsWith("Examiner")) awake++;
            if (awake >= 2) return;
            Vector3 at = transform.position + transform.forward * 0.9f + Vector3.up * 1.2f;
            GameObject pup = Instantiate(examiner, at, transform.rotation, transform.parent);
            pup.name = "Examiner Mudpuppy";
            var body = pup.GetComponent<MudCritter>();
            if (body != null) body.Launch(transform.forward * 3f + Vector3.up * 5f, Random.Range(-360f, 360f));
        }
    }
}
