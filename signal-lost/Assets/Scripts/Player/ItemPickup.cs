using SignalLost.Inventory;
using UnityEngine;

namespace SignalLost.Player
{
    /// <summary>
    /// One of the player's three items lying in the world. E picks it up (<see cref="PlayerGear.Collect"/>), which
    /// hides this object. Put a collider on it (or a child) so the interaction ray can find it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ItemPickup : MonoBehaviour, IInteractable
    {
        // Append only: scenes store the item as its number.
        public enum Item { Laser, Flask, Scanner }
        [SerializeField] private Item item;
        public Item Kind => item;
        public PlayerItem Gear => ToGear(item);
        public string DisplayName => item == Item.Laser ? "mining laser" : item == Item.Scanner ? "scanner" : "Scotch flask";
        public string Prompt => "Pick up " + DisplayName;

        public static PlayerItem ToGear(Item item) =>
            item == Item.Laser ? PlayerItem.MiningLaser : item == Item.Scanner ? PlayerItem.Scanner : PlayerItem.Flask;

        public bool CanInteract(PlayerInteractor player) => isActiveAndEnabled && player.Gear != null && !player.Gear.Has(Gear);

        public bool Interact(PlayerInteractor player)
        {
            if (!CanInteract(player) || !player.Gear.Collect(Gear)) return false;
            gameObject.SetActive(false);
            return true;
        }
    }
}
