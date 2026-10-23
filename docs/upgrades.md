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

| Upgrade | Fits | Effect | Cost |
| --- | --- | --- | --- |
| Overclocked Coil | Laser | Bolts hit 25% harder | 10 metal, 4 wiring |
| Quick-Cool Fins | Laser | Bolt cooldowns 25% shorter | 8 metal, 3 boards |
| Heavy Slam | Laser | Finisher +50% damage, stun 1.5 s | 12 metal, 2 fuel cells |
| Long-Nose Antenna | Scanner | +10 m critter range | 6 wiring, 2 boards |
| Cast-Iron Liver | Flask | One more sip before a blackout | 4 wiring, 2 boards, 1 fuel cell |
| Padded Overalls | Suit | +25 max health | 14 metal, 2 wiring |
| Slick Kneepads | Suit | Slides go 30% further | 6 metal, 3 wiring |

**Supplies** (stack, used from the pockets' Gear tab): Hangover Pills (sober up one drink; 3 wiring), Mystery Snack (heal 15 without getting drunker; 1 fuel cell).
**Trades:** 5 metal for 1 wiring, 4 wiring for 1 board, 3 boards for 1 fuel cell.

Each upgrade has a generated part (`Prefabs/Items/Upgrades/`, `Editor/UpgradeAssetBuilder.cs`) that the pockets menu's mechanic wears: a copper coil, fins and a striking plate on the laser, an antenna on the scanner, iron bands on the flask, padding and kneepads on the suit.

## Adding upgrades (desert and later)

1. Append an `Upgrade` to `UpgradeCatalog.All` (`Scripts/Inventory/Upgrades.cs`) with a new, never-reused `Id`, a `Region`, a `Cost`, and its effect fields. Set `KeyItem` for a mini-boss recipe; `PlayerInventory.AddKeyItem` gives the item.
2. For a new kind of effect, add a field to `Upgrade` and apply it in `PlayerUpgrades.Apply`.
3. Add a model to `UpgradeAssetBuilder.Models` and a placement in `GearFigure.Refresh`, then rebuild the assets.

The shop shows only the current region's upgrades (`MerchantMenuUi.Region`); supplies and trades are sold everywhere.

## Menus

- **Bucket & Barter** (talk to Eugene): Upgrades, Supplies and Trade Scrap tabs with icons, prices (greyed out when short) and Buy. A purchase shows an "upgrade installed" card.
- **Pockets** (Tab): four tabs, 1-4.
  - **Materials:** scrap with icons and counts; key items.
  - **Gear:** a blocky 3D mechanic (Minecraft-inspired, drag to turn) holding the laser and scanner with the flask on the belt, wearing every upgrade; close-ups of each tool with what is fitted; supplies with Use buttons.
  - **Upgrades:** every recipe for this area: installed, affordable or what it costs.
  - **Stats:** health, healing, drinks, scanner range, slide, combo and bolt numbers.

The 3D view (`UI/GearFigure.cs`) is built at runtime from boxes and the player's own tool models, on a hidden stage on layer 30, rendered only while the Gear tab is open.

## Ideas for the Chief Toad reward (not built)

The Chief Toad will drop a key item (say, a **Toad Gland**) that unlocks one water-themed upgrade, useful in Desert Dunes:

1. **Gland Canteen** (flask): every sip also gives 6 s of "Hydrated": heat and sun damage halved, no slowdown in sand.
2. **Bubble Bolt** (laser): full-charge bolts burst into a puddle that slows enemies and puts out fires (Burnout's oil fires, burning fodder).
3. **Croak Pulse** (scanner): a sonar croak that briefly reveals buried enemies (Snorkel Eels, buried scrap) through the sand.
4. **Webbed Boots** (suit): a longer slide and a double jump out of a slide, for crossing dunes.
