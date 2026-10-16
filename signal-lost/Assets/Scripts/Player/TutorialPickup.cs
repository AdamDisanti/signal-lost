using UnityEngine;

namespace SignalLost.Player
{
    [DisallowMultipleComponent]
    public sealed class TutorialPickup : MonoBehaviour
    {
        public enum Item { Laser, Flask }
        [SerializeField] private Item item;
        public string DisplayName => item == Item.Laser ? "mining laser" : "Scotch flask";

        public bool TryCollect(TutorialPlayer player)
        {
            if (!isActiveAndEnabled || player == null || !player.Collect(item)) return false;
            gameObject.SetActive(false);
            return true;
        }
    }
}
