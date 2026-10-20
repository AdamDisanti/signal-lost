using UnityEngine;

namespace SignalLost.Player
{
    [DisallowMultipleComponent]
    public sealed class TutorialPickup : MonoBehaviour
    {
        // Append only: scenes store the item as its number.
        public enum Item { Laser, Flask, Scanner }
        [SerializeField] private Item item;
        public Item Kind => item;
        public string DisplayName => item == Item.Laser ? "mining laser" : item == Item.Scanner ? "scanner" : "Scotch flask";

        public bool TryCollect(TutorialPlayer player)
        {
            if (!isActiveAndEnabled || player == null || !player.Collect(item)) return false;
            gameObject.SetActive(false);
            return true;
        }
    }
}
