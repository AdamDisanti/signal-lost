using UnityEngine;

namespace SignalLost.NPC
{
    public sealed class MerchantShop : MonoBehaviour
    {
        [SerializeField] private TutorialNpcUi ui;
        [SerializeField] private NpcDialogue dialogue;
        private TutorialQuestFlow quest;
        private void Awake()
        {
            quest = FindFirstObjectByType<TutorialQuestFlow>();
            if (ui == null) ui = FindFirstObjectByType<TutorialNpcUi>();
            if (dialogue == null) dialogue = GetComponentInChildren<NpcDialogue>();
        }
        public bool IsOpen => ui != null && ui.ShopIsOpen && ui.Owner == transform;
        public void Toggle()
        {
            if (ui == null) return;
            if (ui.HasOpenWindow && ui.Owner == transform) { ui.Advance(); return; }
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
            ui.BeginConversation(transform, rewardPending ? "MERCHANT CRAB  /  YOUR FLASK" : "MERCHANT CRAB  /  BUCKET & BARTER",
                lines, () => ui.OpenShop(transform));
        }
        public void Close() { ui?.CloseFor(transform); if (dialogue != null) dialogue.Suppressed = false; }
        private void LateUpdate()
        {
            if (dialogue != null) dialogue.Suppressed = ui != null && ui.HasOpenWindow && ui.Owner == transform;
        }
        private void OnDisable() => Close();
    }
}
