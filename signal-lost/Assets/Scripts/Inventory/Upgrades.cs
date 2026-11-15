using System;
using System.Collections.Generic;
using UnityEngine;

namespace SignalLost.Inventory
{
    /// <summary>Where an upgrade goes; also how the shop and the pockets menu group them.</summary>
    public enum UpgradeSlot { Laser, Scanner, Flask, Suit, Supply, Trade, Car }

    /// <summary>The level whose shop sells it.</summary>
    public enum UpgradeRegion { Tutorial, DesertDunes, Mudlands }

    /// <summary>
    /// One thing Bucket &amp; Barter sells (meeting notes 10-23): a fixed recipe of scrap, sometimes plus a key item a
    /// mini-boss drops. Each upgrade tier is bought once and change the player's stats (<see cref="PlayerUpgrades"/>) and
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

        // Mudlands (docs/mudlands.md): the Drunk Driving car and swamp gear, applied by Mudlands.DrunkDriving.
        public float CarRunOverDamage, CarBlastRadius, CarBlastDamage, CarStun, ChugSeconds, CarCooldown;
        public int ChugDrinks;
        public bool CarReinforced, MudTires, SpillProof, Waders;

        // Supplies: what one use does.
        public int SoberUp;
        public float Heal;

        // Trades: what one purchase gives.
        public ScrapType Gives;
        public int GivesAmount;

        /// <summary>
        /// Upgrades come in tiers (Slick Kneepads I, II, III). Every tier of a line shares <see cref="Line"/> (the Id
        /// of tier 1, which also names the model and icon), adds its own effect to the tiers before it, and needs the
        /// tier before it (<see cref="Requires"/>). Supplies and trades have no line.
        /// </summary>
        public string Line;
        public int Tier = 1, Tiers = 1;
        /// <summary>The Id of the tier that must be owned first, or null.</summary>
        public string Requires;
        /// <summary>The Id whose model and icon this uses (the line's, or its own).</summary>
        public string ArtId => Line ?? Id;

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
        public static readonly Upgrade[] All = Build();

        // Tiered lines first (11-07: small steps, each dearer than the last, so nothing maxes out early), then the
        // supplies and trades.
        private static Upgrade[] Build()
        {
            var all = new List<Upgrade>();
            // ---------- Tutorial upgrade lines ----------
            all.AddRange(Line("overclocked_coil", "Overclocked Coil", UpgradeSlot.Laser,
                "A copper coil wrapped round the barrel.", v => $"Charged bolts hit {v * 100f:0}% harder.", (u, v) => u.BoltDamage = v,
                (0.10f, new[] { (ScrapType.ScrapMetal, 8), (ScrapType.Wiring, 2) }),
                (0.10f, new[] { (ScrapType.ScrapMetal, 14), (ScrapType.Wiring, 5), (ScrapType.CircuitBoards, 1) }),
                (0.10f, new[] { (ScrapType.ScrapMetal, 22), (ScrapType.Wiring, 8), (ScrapType.CircuitBoards, 3) })));
            all.AddRange(Line("quickcool_fins", "Quick-Cool Fins", UpgradeSlot.Laser,
                "Cooling fins bolted along the top.", v => $"Bolt cooldowns {-v * 100f:0}% shorter.", (u, v) => u.BoltCooldown = v,
                (-0.08f, new[] { (ScrapType.ScrapMetal, 6), (ScrapType.CircuitBoards, 2) }),
                (-0.08f, new[] { (ScrapType.ScrapMetal, 12), (ScrapType.CircuitBoards, 4) }),
                (-0.09f, new[] { (ScrapType.ScrapMetal, 18), (ScrapType.CircuitBoards, 6), (ScrapType.FuelCells, 1) })));
            all.AddRange(Line("heavy_slam", "Heavy Slam", UpgradeSlot.Laser,
                "An iron striking plate on the nose.", v => $"The combo finisher hits {v * 100f:0}% harder and stuns {v:0.##} s longer.",
                (u, v) => { u.FinisherDamage = v; u.FinisherStun = v; },
                (0.15f, new[] { (ScrapType.ScrapMetal, 10), (ScrapType.FuelCells, 1) }),
                (0.15f, new[] { (ScrapType.ScrapMetal, 16), (ScrapType.Wiring, 2), (ScrapType.FuelCells, 2) }),
                (0.20f, new[] { (ScrapType.ScrapMetal, 24), (ScrapType.CircuitBoards, 3), (ScrapType.FuelCells, 3) })));
            all.AddRange(Line("longnose_antenna", "Long-Nose Antenna", UpgradeSlot.Scanner,
                "A telescoping antenna for the scanner.", v => $"Finds critters {v:0} m further away.", (u, v) => u.ScannerRange = v,
                (4f, new[] { (ScrapType.Wiring, 5), (ScrapType.CircuitBoards, 1) }),
                (3f, new[] { (ScrapType.Wiring, 9), (ScrapType.CircuitBoards, 3) }),
                (3f, new[] { (ScrapType.Wiring, 14), (ScrapType.CircuitBoards, 5) })));
            all.AddRange(Line("castiron_liver", "Cast-Iron Liver", UpgradeSlot.Flask,
                "Iron bands for the flask, and for you.", v => v > 1.5f ? $"{v:0} more sips before a blackout." : "One more sip before a blackout.",
                (u, v) => u.ExtraDrinks = Mathf.RoundToInt(v),
                (1f, new[] { (ScrapType.Wiring, 4), (ScrapType.CircuitBoards, 2), (ScrapType.FuelCells, 1) }),
                (1f, new[] { (ScrapType.Wiring, 9), (ScrapType.CircuitBoards, 5), (ScrapType.FuelCells, 3) })));
            all.AddRange(Line("padded_overalls", "Padded Overalls", UpgradeSlot.Suit,
                "Shoulder and chest padding stitched into the suit.", v => $"+{v:0} max health.", (u, v) => u.MaxHealth = v,
                (10f, new[] { (ScrapType.ScrapMetal, 10), (ScrapType.Wiring, 1) }),
                (10f, new[] { (ScrapType.ScrapMetal, 16), (ScrapType.Wiring, 4) }),
                (10f, new[] { (ScrapType.ScrapMetal, 24), (ScrapType.Wiring, 6), (ScrapType.FuelCells, 1) })));
            all.AddRange(Line("slick_kneepads", "Slick Kneepads", UpgradeSlot.Suit,
                "Low-friction knee plates.", v => $"Slides go {v * 100f:0}% further.", (u, v) => u.SlideDistance = v,
                (0.10f, new[] { (ScrapType.ScrapMetal, 5), (ScrapType.Wiring, 2) }),
                (0.10f, new[] { (ScrapType.ScrapMetal, 10), (ScrapType.Wiring, 4) }),
                (0.10f, new[] { (ScrapType.ScrapMetal, 16), (ScrapType.Wiring, 6), (ScrapType.CircuitBoards, 1) })));

            all.AddRange(new[]
            {
            // ---------- Mudlands upgrades (docs/mudlands.md) ----------
            new Upgrade
            {
                Id = "swamp_waders", Name = "Swamp Waders", Slot = UpgradeSlot.Suit, Region = UpgradeRegion.Mudlands,
                Description = "Rubber waders up to the chest. Mud and bog water no longer slow you, and slides go 20% further.",
                Cost = new[] { (ScrapType.ScrapMetal, 10), (ScrapType.Wiring, 4) }, Waders = true, SlideDistance = 0.2f
            },
            new Upgrade
            {
                Id = "bootleg_capacitor", Name = "Bootleg Capacitor", Slot = UpgradeSlot.Laser, Region = UpgradeRegion.Mudlands,
                Description = "A capacitor wired up with moonshine-still copper. Bolts hit 15% harder and cool down 20% faster.",
                Cost = new[] { (ScrapType.ScrapMetal, 8), (ScrapType.CircuitBoards, 3), (ScrapType.FuelCells, 1) }, BoltDamage = 0.15f, BoltCooldown = -0.2f
            },
            new Upgrade
            {
                Id = "moonshine_distiller", Name = "Moonshine Distiller", Slot = UpgradeSlot.Flask, Region = UpgradeRegion.Mudlands,
                Description = "A tiny still clipped to the flask. One more sip before a blackout, and a chug only costs 1 drink.",
                Cost = new[] { (ScrapType.Wiring, 6), (ScrapType.CircuitBoards, 2), (ScrapType.FuelCells, 2) }, ExtraDrinks = 1, ChugDrinks = -1
            },
            new Upgrade
            {
                Id = "reinforced_bumper", Name = "Reinforced Bumper", Slot = UpgradeSlot.Car, Region = UpgradeRegion.Mudlands,
                Description = "A cow-catcher welded to the front. Run-overs hit 60% harder, and Hog Brutes and bikers get flattened instead of blowing up the car.",
                Cost = new[] { (ScrapType.ScrapMetal, 14), (ScrapType.CircuitBoards, 2) }, CarRunOverDamage = 0.6f, CarReinforced = true
            },
            new Upgrade
            {
                Id = "bigger_boom", Name = "Bigger Boom", Slot = UpgradeSlot.Car, Region = UpgradeRegion.Mudlands, KeyItem = "witch_ember",
                Description = "The witch's ember in the fuel line. The car's explosion reaches 8.5 m instead of 6 m.",
                Cost = new[] { (ScrapType.Wiring, 6), (ScrapType.FuelCells, 2) }, CarBlastRadius = 2.5f
            },
            new Upgrade
            {
                Id = "extra_proof", Name = "Extra Proof", Slot = UpgradeSlot.Car, Region = UpgradeRegion.Mudlands,
                Description = "Stronger stuff in the bottle. The car's explosion does 50% more damage.",
                Cost = new[] { (ScrapType.ScrapMetal, 8), (ScrapType.FuelCells, 3) }, CarBlastDamage = 0.5f
            },
            new Upgrade
            {
                Id = "shockwave_shocks", Name = "Shockwave Shocks", Slot = UpgradeSlot.Car, Region = UpgradeRegion.Mudlands, KeyItem = "mudstone_bead",
                Description = "Springs packed with the otter's mudstone. The explosion stuns for 4 s instead of 2.5 s.",
                Cost = new[] { (ScrapType.CircuitBoards, 4), (ScrapType.FuelCells, 2) }, CarStun = 1.5f
            },
            new Upgrade
            {
                Id = "mud_tires", Name = "Mud Tires", Slot = UpgradeSlot.Car, Region = UpgradeRegion.Mudlands, KeyItem = "kart_wheel",
                Description = "Knobbly tyres off Kart Kong's go-kart, scaled up. No drag in mud, and 30% more grip everywhere.",
                Cost = new[] { (ScrapType.ScrapMetal, 10), (ScrapType.Wiring, 4) }, MudTires = true
            },
            new Upgrade
            {
                Id = "quick_chug", Name = "Quick Chug", Slot = UpgradeSlot.Car, Region = UpgradeRegion.Mudlands,
                Description = "A funnel. The chug takes 0.9 s instead of 1.6 s, and getting hit no longer spills it.",
                Cost = new[] { (ScrapType.Wiring, 6), (ScrapType.CircuitBoards, 2) }, ChugSeconds = -0.7f, SpillProof = true
            },
            new Upgrade
            {
                Id = "spare_keys", Name = "Spare Keys", Slot = UpgradeSlot.Car, Region = UpgradeRegion.Mudlands,
                Description = "A spare set taped under the bumper. The car is ready again after 18 s instead of 30 s.",
                Cost = new[] { (ScrapType.ScrapMetal, 8), (ScrapType.Wiring, 4), (ScrapType.CircuitBoards, 1) }, CarCooldown = -12f
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
            });
            return all.ToArray();
        }

        /// <summary>
        /// One upgrade per tier. Tier 1 keeps the line's own Id (models and icons use it); later tiers are "id_2",
        /// "id_3"... Each tier's effect adds to the tiers before it, and it can only be bought once they are owned.
        /// </summary>
        private static IEnumerable<Upgrade> Line(string id, string name, UpgradeSlot slot, string flavour, Func<float, string> effect,
            Action<Upgrade, float> apply, params (float value, (ScrapType, int)[] cost)[] tiers)
        {
            string[] numerals = { "I", "II", "III", "IV", "V" }; // a local: All is built before later static fields are set
            float total = 0f;
            for (int t = 0; t < tiers.Length; t++)
            {
                total += tiers[t].value;
                string step = effect(tiers[t].value), all = effect(total);
                var upgrade = new Upgrade
                {
                    Id = t == 0 ? id : $"{id}_{t + 1}", Line = id, Tier = t + 1, Tiers = tiers.Length,
                    Requires = t == 0 ? null : t == 1 ? id : $"{id}_{t}",
                    Name = tiers.Length > 1 ? $"{name} {numerals[t]}" : name, Slot = slot, Cost = tiers[t].cost,
                    Description = $"{flavour} {step}" + (t > 0 && all != step ? $" ({all.TrimEnd('.')} in all.)" : "")
                };
                apply(upgrade, tiers[t].value);
                yield return upgrade;
            }
        }

        /// <summary>The highest tier owned of each line, in the order the lines were started (what the pockets show).</summary>
        public static IEnumerable<Upgrade> Installed(IEnumerable<string> owned)
        {
            var best = new Dictionary<string, Upgrade>();
            var order = new List<string>();
            foreach (string id in owned)
            {
                Upgrade upgrade = Find(id);
                if (upgrade == null) continue;
                if (!best.TryGetValue(upgrade.ArtId, out Upgrade had)) order.Add(upgrade.ArtId);
                if (had == null || upgrade.Tier > had.Tier) best[upgrade.ArtId] = upgrade;
            }
            foreach (string line in order) yield return best[line];
        }

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
