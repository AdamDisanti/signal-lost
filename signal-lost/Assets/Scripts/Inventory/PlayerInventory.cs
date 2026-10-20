using System;
using System.Collections.Generic;
using UnityEngine;

namespace SignalLost.Inventory
{
    /// <summary>The player's three pieces of gear (meeting notes 2026-10-20).</summary>
    public enum PlayerItem { MiningLaser, Scanner, Flask }

    /// <summary>
    /// What the player carries: gear and, later, sorted scrap. Lives on the player root so it survives deaths and
    /// blackouts within a Play session. There is no save/load yet.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInventory : MonoBehaviour
    {
        private readonly HashSet<PlayerItem> items = new HashSet<PlayerItem>();

        /// <summary>Raised after an item is added for the first time.</summary>
        public event Action<PlayerItem> ItemAdded;

        public bool Has(PlayerItem item) => items.Contains(item);

        /// <summary>Adds an item. Returns false if the player already has it.</summary>
        public bool Add(PlayerItem item)
        {
            if (!items.Add(item)) return false;
            ItemAdded?.Invoke(item);
            return true;
        }
    }
}
