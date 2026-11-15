using System;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Inventory;
using SignalLost.Player;
using SignalLost.UI;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// Quest line 1, "The Written Test": the Bayou Driving School's old test kiosk. E starts a three-question quiz
    /// (every answer passes, with a different quip), then it stamps the Learner's Permit into the pockets and an item
    /// card explains Drunk Driving.
    /// </summary>
    public sealed class DrivingTestKiosk : MonoBehaviour, IInteractable
    {
        private static readonly (string question, string[] answers, string[] quips)[] Questions =
        {
            ("A traffic light turns red. You:", new[] { "Stop.", "Floor it.", "Take a long, thoughtful sip." },
                new[] { "Correct!", "Also correct, in the Mudlands.", "Correct. Hydration is important." }),
            ("A mudpuppy is crossing the road. You:", new[] { "Brake and wait.", "Honk.", "Pick up speed for momentum." },
                new[] { "How kind. Correct.", "Correct. They love that.", "Bold. Correct." }),
            ("What's the legal limit?", new[] { "Zero.", "Two, maybe three.", "Whatever's left in the bottle." },
                new[] { "Correct, legally.", "Correct, locally.", "That's the spirit. Correct." })
        };

        private int question = -1;
        private bool passed;

        public bool Ready { get; set; }
        public event Action Passed;

        public string Prompt => !Ready ? "" : passed ? "" : "Take the written test";
        public bool CanInteract(PlayerInteractor player) => isActiveAndEnabled && Ready && !passed && !MudHud.Asking;

        public bool Interact(PlayerInteractor player)
        {
            if (!CanInteract(player)) return false;
            question = 0;
            Sfx.Play("ui_open", 0.8f);
            Ask();
            return true;
        }

        private void Ask()
        {
            (string text, string[] answers, _) = Questions[question];
            MudHud.Ask($"BAYOU DRIVING SCHOOL  -  QUESTION {question + 1} OF {Questions.Length}", text, answers, Answered);
        }

        private void Answered(int choice)
        {
            MudHud.Toast(Questions[question].quips[choice], new Color(0.6f, 1f, 0.6f), 1.8f);
            Sfx.Play("ui_click", 0.8f);
            question++;
            if (question < Questions.Length) { Ask(); return; }
            passed = true;
            Sfx.Play("quest_chime", 1f);
            var pockets = PlayerInventory.Current;
            if (pockets != null) pockets.AddKeyItem(KeyItems.LearnersPermit);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, transform.position + Vector3.up * 1.5f, Vector3.up, 30, 0.08f, 3f, 70f, new Color(1f, 0.85f, 0.4f));
            GameplayOverlay.Instance.ShowItemCard("Learner's Permit",
                "\"Everyone passes.\" The photo is a thumb.\n" +
                "<b>Hold F</b>  chug your scotch flask dry, and a car drops out of the sky. Drive it until it blows up.\n" +
                "<b>WASD</b> drive, <b>Space</b> handbrake, <b>click</b> horn, <b>F</b> bail out.\n" +
                "Run over small critters and smash through wood. Hit anything big or solid head-on and it explodes, stunning everything nearby.\n" +
                "Costs 2 drinks (no healing), then 30 s before the next car.",
                new Color(1f, 0.78f, 0.34f), "NEW ABILITY: DRUNK DRIVING");
            Passed?.Invoke();
        }
    }
}
