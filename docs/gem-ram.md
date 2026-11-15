# Gem Ram (Crystal Caves fodder idea)

A pressure small enemy for the [Crystal Caves](crystal-caves.md), from Amanda's 11-13 request. **Built on `feature/gem-ram`, test scene only** (`Scenes/Enemies/GemRamTest`, built by `GemRamBuilder`). All numbers are placeholders to tune. It follows the same fodder rules as the other cave enemies ([enemy rules](enemy-rules.md)): a 14 m territory with the shared leash and heal rule, and the laser's combo finisher stuns it.

## Concept

A shaggy bighorn ram with crystal-capped horns. It is agile, hops like a mountain goat, and uses the cave walls. It is the caves' bruiser: the pressure type, like the Mudlands' Hog Brute, but with different moves so the two don't feel the same.

## Look (draft)

- A stocky body in thick cream-grey wool, a dark face and big curled horns.
- Each horn is capped with a glowing cave crystal shard in the cave's cyan crystal material (Amanda, 11-13).
- About 1.6 m long and 1.1 m tall at the shoulder.

## Role and health

- **Role:** pressure.
- **Health:** **80 HP.** More than the desert's Snorkel Eel (50) and less than the Mudlands' Hog Brute (110).

## How it behaves

| State | What happens |
| --- | --- |
| **Idle** | Grazes near its spot until you enter its territory. A hit draws it in. |
| **Close in** | Trots toward you, then picks a move by distance. |
| **Hop-and-butt (close range)** | **Warning:** it lifts its front legs. It does two quick sideways hops (3 m each, hard to line up), then rears and headbutts up and forward at close range. 18 damage and a small knockback. |
| **Ricochet (mid range)** | **Warning:** a ring appears on a nearby wall or stalagmite, not on you, and it lowers its head. It charges that wall and bounces off. After the bounce, a line locks on your spot and it rebounds along it. 22 damage. |
| **Daze** | If it rebounds into a second wall or pillar without hitting you, it bonks its head and is dazed for about 2 s. Hit it then. The horn crystals don't shatter (Amanda, 11-13). |
| **Death** | The horn crystals go dull. Drops 1-2 scrap metal and 0-1 wiring, like the other cave fodder. |

**Counterplay:**
- Hop-and-butt: sidestep after the second hop, then hit it as it lands.
- Ricochet: wait for the line to lock, then step off it. Lure it into a second wall to daze it.

## How it differs from the Hog Brute

| | Hog Brute | Gem Ram |
| --- | --- | --- |
| Approach | Straight charge at you | Hops, or bounces off a wall |
| Warning | Paws the ground | Raised legs, or a ring on a wall |
| Opening | Stuck in mud | Dazed after a wall hit |

## Phobia check

A furry hoofed mammal with no crawlies and no many-legged shape.

## How it is built

- `GemRam` (`Scripts/Enemies/Caves`) on `DesertFodder`, like the other cave fodder: territory, leash and heal, hit flash, combo stun, scrap drops (`ScrapDropper`: 1-2 scrap metal, 0-1 wiring), scanner signal.
- Ricochet: one bounce (Amanda, 11-13). It picks a NavMesh edge within 10 m (wall, column or stalagmite) whose bounce faces the player. The wall ring is the shared `GroundMarker` turned onto the wall. The rebound line is the Finale's `Telegraph` strip: it tracks you for 0.4 s, then locks for 0.35 s. The wall test and the player shove copy the Hubcap Armadillo's roll.
- With no usable wall in reach, it only hop-and-butts.
- `GemRamBuilder` (menu **Signal Lost > Crystal Caves Enemies > Gem Ram**) builds the prefab and a cave test room with three rams, columns and stalagmites.
- [GemRamSmoke.cs](../tools/unity/GemRamSmoke.cs): the Play Mode smoke check. Install it only in a disposable copy of the project. It passed twice (`GEM_RAM_PLAY_MODE_PASS`). It checks the hop-and-butt hits, a ricochet happens, scrap drops, and the horn crystals go dull on death. The daze isn't covered, because the test player stands still and the rebound always hits.

## Open questions

- **Placement:** should rams in the level be kept near walls? In the open they only hop-and-butt.

## Not decided or built

- No placement in the level. Level placement waits until Amanda asks.
