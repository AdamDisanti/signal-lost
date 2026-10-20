using SignalLost.Player;
using SignalLost.UI;
using UnityEngine;

namespace SignalLost.NPC
{
    public sealed class TutorialQuestFlow : MonoBehaviour
    {
        [SerializeField] private TutorialPlayer player;
        [SerializeField] private NpcDialogue cavendish;
        [SerializeField] private MerchantCrab merchant;
        private bool completionDiscussed;
        public bool Accepted { get; private set; }
        public bool FlaskRecovered => player != null && player.HasFlask;
        private DialogueUi ui;
        public string Objective => !Accepted ? "Talk to Cavendish (E)." :
            !player.HasLaser ? "Recover the mining laser from the basin (E)." :
            !merchant.IsDefeated ? "Defeat the larger crab carrying your flask." :
            !FlaskRecovered ? "Collect the flask dropped by the merchant crab (E)." :
            "Flask recovered! Talk to Cavendish or browse the crab's shop (E).";

        private void Awake() => ResolveUi();

        private bool ResolveUi()
        {
            if (player == null) player = FindFirstObjectByType<TutorialPlayer>();
            if (merchant == null) merchant = FindFirstObjectByType<MerchantCrab>();
            if (cavendish == null)
                foreach (NpcActor actor in FindObjectsByType<NpcActor>())
                    if (actor.IsQuestGiver && actor.gameObject.scene == gameObject.scene)
                    { cavendish = actor.GetComponentInChildren<NpcDialogue>(true); break; }
            if (ui == null) ui = DialogueUi.Instance;
            if (ui == null || player == null || cavendish == null || merchant == null) return false;
            ui.BindPlayer(player.transform);
            return true;
        }

        public void TalkToCavendish()
        {
            if (!ResolveUi())
            {
                Debug.LogError("Tutorial NPC references are incomplete: TutorialQuestFlow needs the player, Cavendish and the merchant crab.", this);
                return;
            }
            if (!Accepted)
            {
                ui.BeginConversation(cavendish.transform.parent, "Cavendish", "RECOVER YOUR FLASK", new[]
                {
                    "Easy, mechanic. I'm Cavendish. I saw your ship come down. You're lucky to be standing!",
                    "A big crab took your flask. Look for the copper bucket and the bottle strapped to its back.",
                    "Grab your mining laser from the rock basin. Defeat that crab, then pick up your flask with E. I'll stay here."
                }, () => { Accepted = true; merchant.BeginQuest(); });
                return;
            }
            if (cavendish.AdvanceManual()) return;
            if (!merchant.IsDefeated)
                ui.BeginConversation(cavendish.transform.parent, "Cavendish", "QUEST UPDATE", new[]
                { "The bigger crab has your flask. Get your laser from the basin, defeat it, and recover the flask." });
            else if (!FlaskRecovered)
                ui.BeginConversation(cavendish.transform.parent, "Cavendish", "RECOVER YOUR FLASK", new[]
                { "The crab dropped your flask where you fought. Pick it up with E. It looks like you've made a trading partner, too." });
            else if (!completionDiscussed)
                ui.BeginConversation(cavendish.transform.parent, "Cavendish", "QUEST COMPLETE", new[]
                {
                    "Flask recovered, mechanic! F takes a sip to heal. Go easy: too much will knock you out.",
                    "I'll help you find your ship parts. Our new crab friend wants scrap for its wares, but the shop isn't stocked for this demo."
                }, () => completionDiscussed = true);
            else cavendish.SpeakManual("Another day stranded in paradise, eh?", "I'll be right here, mechanic.");
        }

        private void Update()
        {
            if (player != null && merchant != null)
                GameplayOverlay.Instance.SetQuest(Accepted ? "Recover Your Flask" : null, Objective, FlaskRecovered);
        }

        public void OnStageReset() { ui?.CloseAll(); merchant.ResetAfterPlayerRespawn(); }
    }
}
