using SignalLost.Inventory;
using UnityEngine;

namespace SignalLost.Finale
{
    /// <summary>
    /// PLAYTEST LOADOUT for the final level (Adam, 11-09: "for playtesting they should have endgame gear/stats").
    /// Pockets don't carry between scenes until the save system lands, so on arrival this gives the player what a
    /// finished run would have: every upgrade line that exists at its top tier (the tutorial's, and the Mudlands' car
    /// and general upgrades), the Learner's Permit for Drunk Driving, two of each supply and some spare scrap.
    /// Upgrades from islands not built yet will be added here as they appear. Remove this once saves carry over.
    /// </summary>
    public sealed class FinaleLoadout : MonoBehaviour
    {
        private static readonly (ScrapType type, int amount)[] SpareScrap =
            { (ScrapType.ScrapMetal, 40), (ScrapType.Wiring, 20), (ScrapType.CircuitBoards, 12), (ScrapType.FuelCells, 6) };

        public int UpgradesGiven { get; private set; }

        private void Start()
        {
            var pockets = GetComponent<PlayerInventory>();
            if (pockets == null) return;
            // Mute the purchase listeners (each would pop an "upgrade installed" card); re-enabling applies it all once.
            var listeners = new System.Collections.Generic.List<Behaviour>();
            foreach (Behaviour b in new Behaviour[] { GetComponent<SignalLost.Player.PlayerUpgrades>(), GetComponent<SignalLost.Mudlands.DrunkDriving>() })
                if (b != null && b.enabled) { b.enabled = false; listeners.Add(b); }
            foreach (string item in new[] { KeyItems.LearnersPermit, KeyItems.KartWheel, KeyItems.MudstoneBead, KeyItems.WitchEmber })
                pockets.AddKeyItem(item);
            foreach (ScrapType type in System.Enum.GetValues(typeof(ScrapType))) pockets.AddScrap(type, 9999);

            // Buy every tier in order (a tier needs the one below it, so loop until nothing more can be bought).
            for (bool bought = true; bought;)
            {
                bought = false;
                foreach (Upgrade upgrade in UpgradeCatalog.All)
                    if (upgrade.OneOff && !pockets.Owns(upgrade.Id) && pockets.Buy(upgrade))
                    {
                        bought = true;
                        UpgradesGiven++;
                    }
            }
            foreach (Upgrade upgrade in UpgradeCatalog.All)
                if (upgrade.IsSupply)
                    for (int i = 0; i < 2; i++) pockets.Buy(upgrade);

            if (!pockets.HasKeyItem(KeyItems.LearnersPermit)) pockets.AddKeyItem(KeyItems.LearnersPermit);
            foreach (ScrapType type in System.Enum.GetValues(typeof(ScrapType)))
            {
                int keep = 0;
                foreach ((ScrapType t, int amount) in SpareScrap)
                    if (t == type) keep = amount;
                pockets.Spend(type, Mathf.Max(0, pockets.Scrap(type) - keep));
            }
            foreach (Behaviour b in listeners) b.enabled = true;
            var life = GetComponent<SignalLost.Player.PlayerLife>();
            if (life != null) life.Health.Restore();
        }
    }
}
