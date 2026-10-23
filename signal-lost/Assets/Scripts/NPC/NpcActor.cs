using System.Linq;
using SignalLost.Player;
using UnityEngine;

namespace SignalLost.NPC
{
    /// <summary>
    /// Makes an NPC talkable with E (<see cref="IInteractable"/>). The scene's story (<see cref="INpcStory"/>) gets
    /// the first say; without one (or when it has nothing to add), Cavendish makes small talk and the friendly
    /// merchant crab opens its shop. The hostile merchant cannot be talked to until it surrenders.
    /// </summary>
    public sealed class NpcActor : MonoBehaviour, IInteractable
    {
        public enum Role { Cavendish, Merchant }
        [SerializeField] private Role role;
        [Tooltip("The scene's story/quest flow (anything implementing INpcStory). Empty: found automatically.")]
        [SerializeField] private MonoBehaviour quest;
        [SerializeField] private MerchantCrab merchant;
        [Tooltip("Overhead small talk when the story has nothing to say.")]
        [SerializeField] private string[] smallTalk =
        {
            "Another lovely day stranded in paradise, eh?",
            "Don't mind me. Capybaras are excellent at standing still.",
            "That crab overcharges, by the way. Just so you know."
        };

        private INpcStory story;
        private int nextSmallTalk;

        public Role Kind => role;
        public bool IsQuestGiver => role == Role.Cavendish;
        public MerchantCrab Merchant => merchant;
        public NpcDialogue Dialogue => GetComponentInChildren<NpcDialogue>(true);
        public string Prompt => role == Role.Cavendish ? "Talk to Cavendish" : "Talk to Eugine";

        private void Awake()
        {
            story = quest as INpcStory;
            if (story == null)
                story = FindObjectsByType<MonoBehaviour>().OfType<INpcStory>().FirstOrDefault();
        }

        public bool CanInteract(PlayerInteractor player) =>
            isActiveAndEnabled && (role == Role.Cavendish || (merchant != null && merchant.IsDefeated));

        public bool Interact(PlayerInteractor player)
        {
            if (!CanInteract(player)) return false;
            if (story != null && story.TalkTo(this, player)) return true;
            if (role == Role.Merchant)
            {
                merchant.OpenShop();
                return true;
            }
            NpcDialogue dialogue = Dialogue;
            if (dialogue == null || smallTalk.Length == 0) return false;
            if (dialogue.AdvanceManual()) return true;
            dialogue.SpeakManual(smallTalk[nextSmallTalk++ % smallTalk.Length]);
            return true;
        }
    }
}
