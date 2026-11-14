# Prism Snail (Crystal Caves fodder idea)

A heavy, area-denial small enemy for the [Crystal Caves](crystal-caves.md), from Amanda's 11-13 request. **Built on `feature/prism-snail`, test scene only** (`Scenes/Enemies/PrismSnailTest`, built by `PrismSnailBuilder`). All numbers are placeholders to tune. It follows the same fodder rules as the other cave enemies ([enemy rules](enemy-rules.md)): a 14 m territory with the shared leash and heal rule, and the laser's combo finisher stuns it (only while its head is out).

## Concept

A slow, round snail with a crystal prism shell. It is the caves' tank. It controls space with a slime trail and shrugs off bolts when it hides in its shell. It is the same job as the Mudlands' Roadblock Turtle.

## Look (draft)

- A soft green body (one colour for every snail) with two stalked eyes and a calm face.
- A big faceted spiral shell of dark, glowing magenta crystal.
- A glowing slime trail behind it, a lighter green than the body.
- About 1.4 m long and 1 m tall.

## Role and health

- **Role:** heavy / area denial.
- **Health:** **115 HP.** More than the desert's Hubcap Armadillo (70) and less than the Mudlands' Roadblock Turtle (160).

## How it behaves

| State | What happens |
| --- | --- |
| **Crawl** | Once you enter its territory it circles you about 2.5 m away (about 1.5 m/s), so its trail forms a ring around you. A hit draws it in. |
| **Slime trail** | It leaves a glowing trail as it crawls. Standing in it slows you by about 40% and does 4 damage every half second (overlapping patches don't stack). Each patch lasts about 13 s. |
| **Slime puddle** | If you get within 2 m, or hit it, it puffs out slime about 3.8 m around itself (a short swell is the warning when you walk up; a hit sets it off at once). 5 s cooldown. |
| **Shell up** | When hit, it pulls into its shell for about 3 s. The shell takes **a quarter** damage and bounces bolts back (reusing `IBoltReflector`, 40% damage kept). A magenta ring of light flares from it (visual only). It can't be stunned while in its shell. |
| **Head out** | While its head is out, the soft body takes **full** damage. |
| **Poke back out** | It comes out again after the shell timer. |
| **Death** | The shell cracks and the glow goes out. Drops 1-2 scrap metal and 0-1 wiring, like the other cave fodder. |

**Counterplay:** swing at the head while it's out, don't stand in the trail, and wait for it to come out of the shell.

## Phobia check

No legs, no many-legged shape, nothing skittering. The slime is the one mild point (it's a snail trail, not a creepy crawly).

## Open questions

- Is a quarter damage on the shell too tough, now that the bat only takes crystal hits?
- Three snails bursting at once lay a lot of slime; check performance and readability in the real level.
- A player shooting from beyond about 3.8 m is outside the puddle; a ranged answer may be needed.
- Not run in Play Mode yet; no smoke test.

## Not decided or built

- No placement in the level. Level placement waits until Amanda asks.
