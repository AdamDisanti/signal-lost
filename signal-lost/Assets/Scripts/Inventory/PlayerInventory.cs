using System;
using System.Collections.Generic;
using UnityEngine;

namespace SignalLost.Inventory
{
    /// <summary>The player's three pieces of gear (meeting notes 2026-10-20).</summary>
    public enum PlayerItem { MiningLaser, Scanner, Flask }

    /// <summary>
    /// What the player carries: gear, scrap sorted by type, and completed upgrades. Lives on the player root so it
    /// survives deaths and blackouts within a Play session. There is no save/load yet.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInventory : MonoBehaviour
    {
        private readonly HashSet<PlayerItem> items = new HashSet<PlayerItem>();
        private readonly int[] scrap = new int[ScrapInfo.All.Length];
        private readonly List<string> upgrades = new List<string>();

        /// <summary>The player's inventory in the current scene (there is one player).</summary>
        public static PlayerInventory Current { get; private set; }

        /// <summary>Raised after an item is added for the first time.</summary>
        public event Action<PlayerItem> ItemAdded;
        /// <summary>Raised after scrap is added or spent. Arguments: type, change, new total.</summary>
        public event Action<ScrapType, int, int> ScrapChanged;

        /// <summary>Names of upgrades bought so far. Empty until the merchant sells any.</summary>
        public IReadOnlyList<string> Upgrades => upgrades;

        private void Awake() => Current = this;

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        public int Scrap(ScrapType type) => scrap[(int)type];

        public int TotalScrap
        {
            get
            {
                int total = 0;
                foreach (int count in scrap) total += count;
                return total;
            }
        }

        public void AddScrap(ScrapType type, int amount)
        {
            if (amount <= 0) return;
            scrap[(int)type] += amount;
            ScrapChanged?.Invoke(type, amount, scrap[(int)type]);
        }

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
