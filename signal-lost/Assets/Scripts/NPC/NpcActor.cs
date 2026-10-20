using SignalLost.Player;
using UnityEngine;

namespace SignalLost.NPC
{
    public sealed class NpcActor : MonoBehaviour
    {
        public enum Role { Cavendish, Merchant }
        [SerializeField] private Role role;
        [SerializeField] private TutorialQuestFlow quest;
        [SerializeField] private MerchantCrab merchant;
        public bool IsQuestGiver => role == Role.Cavendish;
        public bool CanInteract => role == Role.Cavendish || merchant != null && merchant.IsDefeated;
        public string Prompt => !CanInteract ? "" : role == Role.Cavendish ? "E  Talk to Cavendish" : "E  Talk to the merchant crab";

        public bool Interact(TutorialPlayer player)
        {
            if (!CanInteract || player == null || player.IsRespawning || player.IsDrinking || player.Health.IsDead) return false;
            if (role == Role.Cavendish && quest != null) { quest.TalkToCavendish(); return true; }
            if (merchant != null) { merchant.Interact(); return true; }
            return false;
        }
    }
}
