using System;
using System.Collections.Generic;

namespace SignalLost.Inventory
{
    /// <summary>Where an upgrade goes; also how the shop and the pockets menu group them.</summary>
    public enum UpgradeSlot { Laser, Scanner, Flask, Suit, Supply, Trade }

    /// <summary>The level whose shop sells it. Only the tutorial's are defined so far.</summary>
    public enum UpgradeRegion { Tutorial, DesertDunes }

    /// <summary>
    /// One thing Bucket &amp; Barter sells (meeting notes 10-23): a fixed recipe of scrap, sometimes plus a key item a
    /// mini-boss drops. Upgrades are bought once and change the player's stats (<see cref="PlayerUpgrades"/>) and
    /// show on the mechanic in the pockets menu; supplies stack and are used up; trades turn scrap into other scrap.
    /// </summary>
    public sealed class Upgrade
    {
        public string Id, Name, Description;
        public UpgradeSlot Slot;
        public UpgradeRegion Region;
        public (ScrapType type, int amount)[] Cost = Array.Empty<(ScrapType, int)>();
        /// <summary>A key item that must be in the pockets (it is used up), e.g. a mini-boss drop. Null: none.</summary>
        public string KeyItem;

        // Effects of an owned upgrade (summed over everything owned).
        public float BoltDamage, BoltCooldown, FinisherDamage, FinisherStun, ScannerRange, MaxHealth, SlideDistance;
        public int ExtraDrinks;

        // Supplies: what one use does.
        public int SoberUp;
        public float Heal;

        // Trades: what one purchase gives.
        public ScrapType Gives;
        public int GivesAmount;

        public bool IsSupply => Slot == UpgradeSlot.Supply;
        public bool IsTrade => Slot == UpgradeSlot.Trade;
        /// <summary>Upgrades are bought once; supplies and trades can be bought again and again.</summary>
        public bool OneOff => !IsSupply && !IsTrade;
    }

    /// <summary>
    /// Every upgrade, supply and trade in the game. Append new ones (with a new Id) as levels are built; never reuse
    /// or rename an Id, because the inventory stores Ids. Costs are placeholders until play-testing.
    /// </summary>
    public static class UpgradeCatalog
    {
        public static readonly Upgrade[] All =
        {
            // ---------- Tutorial upgrades ----------
            new Upgrade
            {
                Id = "overclocked_coil", Name = "Overclocked Coil", Slot = UpgradeSlot.Laser,
                Description = "A copper coil wrapped round the barrel. Charged bolts hit 25% harder.",
                Cost = new[] { (ScrapType.ScrapMetal, 10), (ScrapType.Wiring, 4) }, BoltDamage = 0.25f
            },
            new Upgrade
            {
                Id = "quickcool_fins", Name = "Quick-Cool Fins", Slot = UpgradeSlot.Laser,
                Description = "Cooling fins bolted along the top. Bolt cooldowns 25% shorter.",
                Cost = new[] { (ScrapType.ScrapMetal, 8), (ScrapType.CircuitBoards, 3) }, BoltCooldown = -0.25f
            },
            new Upgrade
            {
                Id = "heavy_slam", Name = "Heavy Slam", Slot = UpgradeSlot.Laser,
                Description = "An iron striking plate on the nose. The combo finisher hits 50% harder and stuns for 1.5 s.",
                Cost = new[] { (ScrapType.ScrapMetal, 12), (ScrapType.FuelCells, 2) }, FinisherDamage = 0.5f, FinisherStun = 0.5f
            },
            new Upgrade
            {
                Id = "longnose_antenna", Name = "Long-Nose Antenna", Slot = UpgradeSlot.Scanner,
                Description = "A telescoping antenna for the scanner. Finds critters 10 m further away.",
                Cost = new[] { (ScrapType.Wiring, 6), (ScrapType.CircuitBoards, 2) }, ScannerRange = 10f
            },
            new Upgrade
            {
                Id = "castiron_liver", Name = "Cast-Iron Liver", Slot = UpgradeSlot.Flask,
                Description = "Iron bands for the flask, and for you. One more sip before a blackout.",
                Cost = new[] { (ScrapType.Wiring, 4), (ScrapType.CircuitBoards, 2), (ScrapType.FuelCells, 1) }, ExtraDrinks = 1
            },
            new Upgrade
            {
                Id = "padded_overalls", Name = "Padded Overalls", Slot = UpgradeSlot.Suit,
                Description = "Shoulder and chest padding stitched into the suit. +25 max health.",
                Cost = new[] { (ScrapType.ScrapMetal, 14), (ScrapType.Wiring, 2) }, MaxHealth = 25f
            },
            new Upgrade
            {
                Id = "slick_kneepads", Name = "Slick Kneepads", Slot = UpgradeSlot.Suit,
                Description = "Low-friction knee plates. Slides go 30% further.",
                Cost = new[] { (ScrapType.ScrapMetal, 6), (ScrapType.Wiring, 3) }, SlideDistance = 0.3f
            },

            // ---------- Supplies ----------
            new Upgrade
            {
                Id = "hangover_pills", Name = "Hangover Pills", Slot = UpgradeSlot.Supply,
                Description = "Sober up by one drink. Probably safe.",
                Cost = new[] { (ScrapType.Wiring, 3) }, SoberUp = 1
            },
            new Upgrade
            {
                Id = "mystery_snack", Name = "Mystery Snack", Slot = UpgradeSlot.Supply,
                Description = "Heals 15 without getting you any drunker. The crab won't say what's in it.",
                Cost = new[] { (ScrapType.FuelCells, 1) }, Heal = 15f
            },

            // ---------- Trades ----------
            new Upgrade
            {
                Id = "trade_wiring", Name = "Metal for Wiring", Slot = UpgradeSlot.Trade, Description = "5 Scrap Metal into 1 Wiring.",
                Cost = new[] { (ScrapType.ScrapMetal, 5) }, Gives = ScrapType.Wiring, GivesAmount = 1
            },
            new Upgrade
            {
                Id = "trade_boards", Name = "Wiring for Boards", Slot = UpgradeSlot.Trade, Description = "4 Wiring into 1 Circuit Board.",
                Cost = new[] { (ScrapType.Wiring, 4) }, Gives = ScrapType.CircuitBoards, GivesAmount = 1
            },
            new Upgrade
            {
                Id = "trade_cell", Name = "Boards for a Cell", Slot = UpgradeSlot.Trade, Description = "3 Circuit Boards into 1 Fuel Cell.",
                Cost = new[] { (ScrapType.CircuitBoards, 3) }, Gives = ScrapType.FuelCells, GivesAmount = 1
            }
        };

        private static Dictionary<string, Upgrade> byId;

        public static Upgrade Find(string id)
        {
            if (byId == null)
            {
                byId = new Dictionary<string, Upgrade>();
                foreach (Upgrade upgrade in All) byId[upgrade.Id] = upgrade;
            }
            return id != null && byId.TryGetValue(id, out Upgrade found) ? found : null;
        }

        public static IEnumerable<Upgrade> For(UpgradeRegion region, params UpgradeSlot[] slots)
        {
            foreach (Upgrade upgrade in All)
                if (upgrade.Region == region && Array.IndexOf(slots, upgrade.Slot) >= 0) yield return upgrade;
        }
    }
}
