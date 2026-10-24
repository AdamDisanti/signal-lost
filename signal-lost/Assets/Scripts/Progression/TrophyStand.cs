using System;
using System.Linq;
using SignalLost.Inventory;
using SignalLost.Player;
using UnityEngine;

namespace SignalLost.Progression
{
    /// <summary>
    /// A trophy stand at the base camp (meeting notes 10-23). E on an empty stand while carrying a boss trophy sets it
    /// there; <see cref="CampSave"/> remembers it, so the camp shows it in every level and every later session.
    /// Each stand in a camp has its own <see cref="index"/>.
    /// </summary>
    public sealed class TrophyStand : MonoBehaviour, IInteractable
    {
        [SerializeField] private int index;
        [Tooltip("Where the trophy sits (its top).")]
        [SerializeField] private Transform mount;

        private GameObject shown;
        private string shownId;

        /// <summary>Raised when a trophy is set on any stand, with its Id.</summary>
        public static event Action<string> Placed;

        public int Index => index;
        public string TrophyId => CampSave.TrophyOn(index);

        public string Prompt
        {
            get
            {
                if (TrophyId != null) return "";
                Trophy carried = Carried(PlayerInventory.Current);
                return carried != null ? "Place " + carried.Name : "";
            }
        }

        private void OnEnable()
        {
            CampSave.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => CampSave.Changed -= Refresh;

        public bool CanInteract(PlayerInteractor player) =>
            isActiveAndEnabled && TrophyId == null && player.Gear != null && Carried(player.Gear.Inventory) != null;

        public bool Interact(PlayerInteractor player)
        {
            if (!CanInteract(player)) return false;
            Trophy trophy = Carried(player.Gear.Inventory);
            player.Gear.Inventory.RemoveKeyItem(trophy.Id);
            CampSave.Place(trophy.Id, index);
            Placed?.Invoke(trophy.Id);
            return true;
        }

        // The first boss trophy in the pockets that is not already on display.
        private static Trophy Carried(PlayerInventory inventory) =>
            inventory == null ? null : inventory.KeyItems.Select(Trophy.Find).FirstOrDefault(t => t != null && !CampSave.IsPlaced(t.Id));

        private void Refresh()
        {
            string id = TrophyId;
            if (id == shownId) return;
            if (shown != null) Destroy(shown);
            shown = null;
            shownId = id;
            Trophy trophy = Trophy.Find(id);
            GameObject model = trophy != null ? trophy.LoadModel() : null;
            if (model == null) return;
            shown = Instantiate(model, mount != null ? mount : transform, false);
            shown.name = trophy.Name;
            foreach (Collider part in shown.GetComponentsInChildren<Collider>()) Destroy(part);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Placed = null;
    }
}
