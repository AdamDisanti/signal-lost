using SignalLost.Player;
using SignalLost.UI;
using UnityEngine;

namespace SignalLost.NPC
{
    // The friendly merchant crab's shop: opens the Bucket & Barter menu for this crab.
    public sealed class MerchantShop : MonoBehaviour
    {
        private Transform customer;
        private void Awake()
        {
            var player = FindFirstObjectByType<FirstPersonController>();
            if (player != null) customer = player.transform;
        }
        public bool IsOpen => MerchantMenuUi.Current != null && MerchantMenuUi.Current.IsOpen && MerchantMenuUi.Current.Owner == transform;
        public void Toggle()
        {
            if (IsOpen) Close();
            else MerchantMenuUi.Instance.Open(transform, customer);
        }
        public void Close()
        {
            if (IsOpen) MerchantMenuUi.Current.Close();
            DialogueUi.Current?.CloseFor(transform);
        }
        private void OnDisable() => Close();
    }
}
