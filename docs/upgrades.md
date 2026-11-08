# Scrap, upgrades and the shop

The scrap and upgrade system from the 10-23-26 meeting notes ("Final tutorial tweaking"). No crafting freedom: every upgrade is a set recipe of scrap, bought from Eugene at Bucket & Barter. Some later upgrades will also need a key item that a mini-boss drops.

## Scrap

Four types, each with a generated low-poly model (`Prefabs/Items/Scrap/`, built by `Editor/ScrapAssetBuilder.cs`, **Signal Lost > Items > Build Scrap Assets**). The same models show as floor drops and as icons in the HUD, pockets and shop.

| Type | Model |
| --- | --- |
| Scrap Metal | A bundle of bent gunmetal pipes and a drilled bracket with hex nuts and rust |
| Wiring | A cable spool with rusty-orange flanges and a loose end |
| Circuit Boards | A green board with gold traces, chips, teal capacitors, heat sinks and a glowing core chip |
| Fuel Cells | A glass tube with ribbed metal caps and a jagged cyan charge |

Runtime code finds models and icons through `ScrapCatalog` (in `Resources/SignalLost/`).

## Tutorial upgrades

Upgrades come in **tiers** (11-07). Each tier is a small step that adds to the ones before it and costs more scrap than the last, so the player can't max anything out early. The shop shows only the next tier of each upgrade, and a tier can't be bought before the one below it. Effects add up per tier; the "Total" column is all tiers together.

| Upgrade | Fits | Per tier | Total | Cost: I / II / III |
| --- | --- | --- | --- | --- |
| Overclocked Coil | Laser | Bolts +10% damage | +30% | 8 metal, 2 wiring / 14 metal, 5 wiring, 1 board / 22 metal, 8 wiring, 3 boards |
| Quick-Cool Fins | Laser | Bolt cooldowns -8%, -8%, -9% | -25% | 6 metal, 2 boards / 12 metal, 4 boards / 18 metal, 6 boards, 1 cell |
| Heavy Slam | Laser | Finisher +15%, +15%, +20% damage; stun +0.15, +0.15, +0.2 s | +50%, +0.5 s | 10 metal, 1 cell / 16 metal, 2 wiring, 2 cells / 24 metal, 3 boards, 3 cells |
| Long-Nose Antenna | Scanner | Critter range +4, +3, +3 m | +10 m | 5 wiring, 1 board / 9 wiring, 3 boards / 14 wiring, 5 boards |
| Cast-Iron Liver (2 tiers) | Flask | One more sip before a blackout | +2 sips | 4 wiring, 2 boards, 1 cell / 9 wiring, 5 boards, 3 cells |
| Padded Overalls | Suit | +10 max health | +30 | 10 metal, 1 wiring / 16 metal, 4 wiring / 24 metal, 6 wiring, 1 cell |
| Slick Kneepads | Suit | Slides +10% further | +30% | 5 metal, 2 wiring / 10 metal, 4 wiring / 16 metal, 6 wiring, 1 board |

Tier I keeps the old Id (`slick_kneepads`); later tiers are `slick_kneepads_2`, `slick_kneepads_3`. Every tier shares the line's model and icon (`Upgrade.Line`, `ArtId`). The pockets and the shop's Purchased filter show only the highest tier owned of each line (`UpgradeCatalog.Installed`). Costs are placeholders until play-testing.

**Supplies** (stack, used from the pockets' Gear tab): Hangover Pills (sober up one drink; 3 wiring), Mystery Snack (heal 15 without getting drunker; 1 fuel cell).
**Trades:** 5 metal for 1 wiring, 4 wiring for 1 board, 3 boards for 1 fuel cell.

Each upgrade has a generated part (`Prefabs/Items/Upgrades/`, `Editor/UpgradeAssetBuilder.cs`) that the pockets menu's mechanic wears: a copper coil, fins and a striking plate on the laser, an antenna on the scanner, iron bands on the flask, padding and kneepads on the suit.

## Adding upgrades (desert and later)

1. Add a tiered line with `Line(...)` in `UpgradeCatalog.Build` (`Scripts/Inventory/Upgrades.cs`): a new, never-reused Id, the effect text and field, and one (value, cost) pair per tier, each dearer than the last. A single-tier line is fine. For a one-off item, append an `Upgrade` instead, with a `Region`, a `Cost` and its effect fields. Set `KeyItem` for a mini-boss recipe; `PlayerInventory.AddKeyItem` gives the item.
2. For a new kind of effect, add a field to `Upgrade` and apply it in `PlayerUpgrades.Apply`.
3. Add a model to `UpgradeAssetBuilder.Models` and a placement in `GearFigure.Refresh`, then rebuild the assets.

The shop shows only the current region's upgrades (`MerchantMenuUi.Region`); supplies and trades are sold everywhere.

## Menus

- **Bucket & Barter** (talk to Eugene): filters by type (Laser, Scanner, Flask, Player, Supplies, Trades; keys 1-6) and a **Purchased** filter at the bottom for what you own, kept out of the way. Prices are amounts with scrap icons ("10 [icon]"), red when you are short. **BUY** selects an item and previews your scrap after the purchase ("60 → 50"); **CONFIRM** buys it. A purchase shows an "upgrade installed" card. Rows say which tier they are ("Tier 2 of 3"). Up to five rows show at once; longer lists (Purchased can hold all seven upgrades) scroll with the mouse wheel or the bar on the right. Descriptions get two lines and shrink slightly rather than spill out of their row.
- **Scrap amounts everywhere** (shop, HUD toast, trades) are written as a number and the scrap's icon, using the TextMeshPro sprite asset `Resources/SignalLost/ScrapIcons` (built with the scrap assets; `ScrapInfo.Inline`).
- **Pockets** (Tab): four tabs, 1-4.
  - **Materials:** scrap with icons and counts; key items.
  - **Gear:** a blocky 3D mechanic (Minecraft-inspired, drag to turn) holding the laser and scanner with the flask on the belt, wearing every upgrade; close-ups of each tool with what is fitted; supplies with Use buttons.
  - **Upgrades:** only the upgrades you have installed, grouped by type (Mining Laser, Scanner, Flask, Player).
  - **Stats:** health, healing, drinks, scanner range, slide, combo and bolt numbers.

The 3D view (`UI/GearFigure.cs`) is built at runtime from boxes and the player's own tool models, on a hidden stage on layer 30, rendered only while the Gear tab is open.

## Ideas for the Chief Toad reward (not built)

The Chief Toad will drop a key item (say, a **Toad Gland**) that unlocks one water-themed upgrade, useful in Desert Dunes:

1. **Gland Canteen** (flask): every sip also gives 6 s of "Hydrated": heat and sun damage halved, no slowdown in sand.
2. **Bubble Bolt** (laser): full-charge bolts burst into a puddle that slows enemies and puts out fires (Burnout's oil fires, burning fodder).
3. **Croak Pulse** (scanner): a sonar croak that briefly reveals buried enemies (Snorkel Eels, buried scrap) through the sand.
4. **Webbed Boots** (suit): a longer slide and a double jump out of a slide, for crossing dunes.
