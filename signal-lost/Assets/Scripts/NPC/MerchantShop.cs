using SignalLost.UI;
using UnityEngine;

namespace SignalLost.NPC
{
    public sealed class MerchantShop : MonoBehaviour
    {
        private DialogueUi ui;
        [SerializeField] private NpcDialogue dialogue;
        private TutorialQuestFlow quest;
        private void Awake()
        {
            quest = FindFirstObjectByType<TutorialQuestFlow>();
            ui = DialogueUi.Instance;
            if (dialogue == null) dialogue = GetComponentInChildren<NpcDialogue>();
        }
        public bool IsOpen => ui != null && ui.PopupOpen && ui.Owner == transform;
        public void Toggle()
        {
            if (ui == null) return;
            if (ui.PopupOpen && ui.Owner == transform) { ui.Advance(); return; }
            bool rewardPending = quest != null && !quest.FlaskRecovered;
            string[] lines = rewardPending ? new[]
            {
                "You won fair and square. Your flask is where we fought. Pick it up with E.",
                "F takes a sip to heal once you've collected it. Go easy on the Scotch!",
                "I trade supplies for scrap. Stock and trading are coming later; you can browse the preview for now."
            } : new[]
            {
                "Welcome to Bucket & Barter, mechanic. Thanks for settling that little misunderstanding!",
                "Bring me scrap and I'll trade you supplies. Stock and trading are coming later; you can browse the preview for now."
            };
            ui.BeginConversation(transform, "Merchant Crab", rewardPending ? "Your flask" : "Bucket & Barter",
                lines, () => dialogue?.Speak("Shelves are still empty. Come back soon!"));
        }
        public void Close() { ui?.CloseFor(transform); if (dialogue != null) dialogue.Suppressed = false; }
        private void LateUpdate()
        {
            if (dialogue != null) dialogue.Suppressed = ui != null && ui.PopupOpen && ui.Owner == transform;
        }
        private void OnDisable() => Close();
    }
}
