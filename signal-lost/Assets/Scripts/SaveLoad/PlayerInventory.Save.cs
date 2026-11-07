using System;
using System.Collections.Generic;
using UnityEngine;

namespace SignalLost.Inventory
{
    // Save and load (Christian, 10-27): the save-only part of PlayerInventory. The rest is in Inventory/PlayerInventory.cs.
    public sealed partial class PlayerInventory
    {
        /// <summary>The gear owned (laser, scanner, flask).</summary>
        public IEnumerable<PlayerItem> Items => items;
        /// <summary>Supplies by Id, with how many are in the pockets.</summary>
        public IReadOnlyDictionary<string, int> Supplies => supplies;

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
    }
}
