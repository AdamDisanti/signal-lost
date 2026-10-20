using SignalLost.UI;
using UnityEngine;

namespace SignalLost.NPC
{
    // The friendly merchant crab's shop. Placeholder until the merchant menu exists.
    public sealed class MerchantShop : MonoBehaviour
    {
        [SerializeField] private NpcDialogue dialogue;
        private void Awake()
        {
            if (dialogue == null) dialogue = GetComponentInChildren<NpcDialogue>();
        }
        public bool IsOpen => false;
        public void Toggle() => dialogue?.Speak(3.5f, "Shelves are still being stocked! Come back soon.");
        public void Close() => DialogueUi.Instance?.CloseFor(transform);
        private void OnDisable() => Close();
    }
}
