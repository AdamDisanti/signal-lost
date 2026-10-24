using System;
using System.Collections.Generic;
using UnityEngine;

namespace SignalLost.Inventory
{
    /// <summary>The player's three pieces of gear (meeting notes 2026-10-20).</summary>
    public enum PlayerItem { MiningLaser, Scanner, Flask }

    /// <summary>
    /// What the player carries: gear, scrap sorted by type, upgrades bought (by Id, see <see cref="UpgradeCatalog"/>),
    /// supplies and key items (mini-boss drops). Lives on the player root so it survives deaths and blackouts within a
    /// Play session; <see cref="SignalLost.Progression.SaveGame"/> saves it and puts it back with <see cref="Restore"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInventory : MonoBehaviour
    {
        private readonly HashSet<PlayerItem> items = new HashSet<PlayerItem>();
        private readonly int[] scrap = new int[ScrapInfo.All.Length];
        private readonly List<string> upgrades = new List<string>();
        private readonly Dictionary<string, int> supplies = new Dictionary<string, int>();
        private readonly HashSet<string> keyItems = new HashSet<string>();

        /// <summary>The player's inventory in the current scene (there is one player).</summary>
        public static PlayerInventory Current { get; private set; }

        /// <summary>Raised after an item is added for the first time.</summary>
        public event Action<PlayerItem> ItemAdded;
        /// <summary>Raised after scrap is added or spent. Arguments: type, change, new total.</summary>
        public event Action<ScrapType, int, int> ScrapChanged;

        /// <summary>Ids of upgrades bought so far, in order.</summary>
        public IReadOnlyList<string> Upgrades => upgrades;
        public IEnumerable<string> KeyItems => keyItems;
        /// <summary>The gear owned (laser, scanner, flask).</summary>
        public IEnumerable<PlayerItem> Items => items;
        /// <summary>Supplies by Id, with how many are in the pockets.</summary>
        public IReadOnlyDictionary<string, int> Supplies => supplies;
        /// <summary>Raised after anything is bought (upgrade, supply or trade).</summary>
        public event Action<Upgrade> Purchased;
        /// <summary>Raised when upgrades, supplies or key items change.</summary>
        public event Action Changed;

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

        public bool Spend(ScrapType type, int amount)
        {
            if (amount < 0 || scrap[(int)type] < amount) return false;
            scrap[(int)type] -= amount;
            if (amount > 0) ScrapChanged?.Invoke(type, -amount, scrap[(int)type]);
            return true;
        }

        public bool Owns(string upgradeId) => upgrades.Contains(upgradeId);
        public int SupplyCount(string id) => supplies.TryGetValue(id, out int count) ? count : 0;
        public bool HasKeyItem(string id) => keyItems.Contains(id);

        public void AddKeyItem(string id)
        {
            if (keyItems.Add(id)) Changed?.Invoke();
        }

        public void RemoveKeyItem(string id)
        {
            if (keyItems.Remove(id)) Changed?.Invoke();
        }

        /// <summary>Why it cannot be bought right now, or null if it can.</summary>
        public string WhyNot(Upgrade upgrade)
        {
            if (upgrade.OneOff && Owns(upgrade.Id)) return "Owned";
            if (upgrade.KeyItem != null && !HasKeyItem(upgrade.KeyItem)) return "Needs " + upgrade.KeyItem;
            foreach ((ScrapType type, int amount) in upgrade.Cost)
                if (Scrap(type) < amount) return "Not enough scrap";
            return null;
        }

        /// <summary>Pays for and receives an upgrade, supply or trade. Returns false if it cannot be bought.</summary>
        public bool Buy(Upgrade upgrade)
        {
            if (upgrade == null || WhyNot(upgrade) != null) return false;
            foreach ((ScrapType type, int amount) in upgrade.Cost) Spend(type, amount);
            if (upgrade.KeyItem != null) keyItems.Remove(upgrade.KeyItem);
            if (upgrade.IsTrade) AddScrap(upgrade.Gives, upgrade.GivesAmount);
            else if (upgrade.IsSupply) supplies[upgrade.Id] = SupplyCount(upgrade.Id) + 1;
            else upgrades.Add(upgrade.Id);
            Purchased?.Invoke(upgrade);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Takes one supply out of the pockets. Returns false if there is none.</summary>
        public bool TakeSupply(string id)
        {
            int count = SupplyCount(id);
            if (count <= 0) return false;
            supplies[id] = count - 1;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Loading a save: replaces everything in the pockets at once, quietly (no pickup cards, sounds or scrap
        /// popups). <paramref name="scrapCounts"/> follows the order of <see cref="ScrapInfo.All"/>. Starting items the
        /// scene gave are kept. Afterwards the gear in hand and the upgrades need refreshing (the level does that).
        /// </summary>
        public void Restore(IEnumerable<PlayerItem> owned, IList<int> scrapCounts, IEnumerable<string> bought,
            IDictionary<string, int> supplyCounts, IEnumerable<string> keys)
        {
            if (owned != null) foreach (PlayerItem item in owned) items.Add(item);
            Array.Clear(scrap, 0, scrap.Length);
            if (scrapCounts != null)
                for (int i = 0; i < scrapCounts.Count && i < ScrapInfo.All.Length; i++)
                    scrap[(int)ScrapInfo.All[i]] = Mathf.Max(0, scrapCounts[i]);
            upgrades.Clear();
            if (bought != null)
                foreach (string id in bought)
                    if (!string.IsNullOrEmpty(id) && !upgrades.Contains(id)) upgrades.Add(id);
            supplies.Clear();
            if (supplyCounts != null)
                foreach (KeyValuePair<string, int> supply in supplyCounts)
                    if (supply.Value > 0) supplies[supply.Key] = supply.Value;
            keyItems.Clear();
            if (keys != null) foreach (string id in keys) if (!string.IsNullOrEmpty(id)) keyItems.Add(id);
            Changed?.Invoke();
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
