using SignalLost.Inventory;
using UnityEngine;

namespace SignalLost.Progression
{
    /// <summary>
    /// TESTING TOOL: put on the player in a test scene to start with these upgrades installed (Ids from
    /// <see cref="UpgradeCatalog"/>), without scrap or a shop. The chameleon test scene gives the Crystal Disruptor.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class StartingUpgrades : MonoBehaviour
    {
        [SerializeField] private string[] upgradeIds = new string[0];

        private void Start()
        {
            var pockets = GetComponent<PlayerInventory>();
            foreach (string id in upgradeIds)
            {
                Upgrade upgrade = UpgradeCatalog.Find(id);
                if (upgrade == null) { Debug.LogWarning("StartingUpgrades: no upgrade with Id " + id, this); continue; }
                if (pockets.Owns(id)) continue;
                foreach ((ScrapType type, int amount) in upgrade.Cost) pockets.AddScrap(type, amount); // paid for, so nothing is left over
                pockets.Buy(upgrade);
            }
        }
    }
}
