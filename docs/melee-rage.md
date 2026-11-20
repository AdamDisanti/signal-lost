# Melee, rage and Moon Shoes

Adam's request (11-10): make clearing the adds fun and worth it. There are three parts:

- dedicated melee with Doom/Destiny-style finishers and spreading AOE;
- **rage stacks** from kills that make the player stronger (more damage to bosses, more speed);
- a speed-and-jump item, the **Moon Shoes**.

There is also a test arena to try it all on a crowd of the finale's robot enemies.

**Where it is on:** only the final boss level (Wasteland) and the melee test arena (Adam, 11-10: kept out of the rest of the game so it doesn't clash with the team's work). Both scenes' playtest loadout adds `MeleeLoadout`, which switches melee on and hands out Knuckles III and Moon Shoes III. Nothing is sold in a shop yet, and the shop, pockets menu, upgrade catalog and main HUD are unchanged. Everywhere else, Q still just swaps the laser and the scanner.

All numbers are provisional: they haven't been playtested.

## Controls

| Key | With the fists up |
| --- | --- |
| **Q** | Cycles laser → scanner → fists → laser (once the laser is found). Without the scanner, Q swaps laser and fists. |
| **Left click** | Punch combo: jab, cross, then a heavy hook that shoves and jolts small enemies. |
| **Right click** | Magnet pull (Knuckles II+). |
| **V** | Finisher (Knuckles III). Rebindable as "Finisher (knuckles)". No gamepad button yet. |
| **Space in mid-air** | Double jump (Moon Shoes III), in any hand. |

When the fists are up, both hands come up in a boxer's guard. The guard pumps when you sprint. The melee HUD lays its own panel over the status bar's hand section, saying **FISTS** (or **GAUSS KNUCKLES**). It shows what the knuckles can do now, and the magnet pull's recharge on the bar. The crosshair flashes when a punch lands.

## Gaussian Brass Knuckles

Brass dusters with a gauss coil on the back of the hand, in the mining laser's style: a dark housing, copper windings, a glowing cyan core. They are fitted onto the first-person gloves, and glow brighter with each hit and with rage. Each tier unlocks the next trick (Adam's choice):

| Tier | Unlocks | Punches | Speed with fists up |
| --- | --- | --- | --- |
| I | **Magnet lunge.** Punching an enemy in your sights up to 9 m away flings you onto it, so you bounce from enemy to enemy. | +35% | +8% |
| II | **Overcharge marks, chain lightning and the magnet pull.** See below. | +55% | +12% |
| III | **Finishers.** See below. | +75% | +16% |

Both scenes give tier III. The arena's 7 key steps through the tiers.

**Bare fists** (no knuckles) punch for 9 / 9 / 16, with no tricks.

### Spreading effects (Adam's wording)

- **Overcharge marks:** knuckle punches put an overcharge mark (a crackling cyan ring) on enemies. It lasts 6 s.
- **Chain lightning:** melee kills, and punches on marked enemies, throw chain lightning. It jumps to the nearest enemy not hit yet, 3 jumps within 7 m, 14 damage times the knuckles' multiplier, each jump 15% weaker. It briefly jolts small enemies.
- **Finishers:** they slam a shockwave (45 damage within 5 m, shoving and dazing small enemies), throw chain lightning and set off every overcharge mark nearby. Each mark bursts for 30 within 3.2 m.
- **Rage reach:** higher rage gives a bigger radius and more lightning jumps (see Rage).

### The magnet pull (II)

Right click on an enemy in your sights within 20 m:

- **Small enemy:** yanked to you and pounded (30 damage times the multiplier, a mark or lightning).
- **Big enemy or boss:** pulls you to it instead, then the pound.

The pull has a 2.5 s recharge.

"Small" means up to 200 health and never a mini-boss, boss or the mech.

### Finishers (III)

- **When:** a small enemy at or below 35% health glows: a spinning white diamond and a flickering ring. "[V] FINISH …" shows under the crosshair when one is in your sights or right next to you.
- **The move:** V lunges you onto it and both fists slam down. It always kills.
- **Protection:** you can't be hurt during the finisher.
- **Robot mini-bosses (11-10):** the final boss's robot copies (Burn-Bot, Witch-Bot, Mecha-Worm, Mecha-Buzzard, and the arena's Robo-Toad) can be finished below a quarter of their health.
  - V reaches them from up to 11 m, and anywhere roughly ahead, even flying.
  - The slam is bigger: a 1.5× shockwave, more lightning and an explosion.
  - They opt in with a `Finishable` component.
- **Never finished:** Cavendish's mech, and the islands' real bosses and mini-bosses.

## Rage

| Rule | Value |
| --- | --- |
| Kill (any weapon, the car included) | +1 stack |
| Melee kill (fists) | +2 |
| Finisher kill | +3 |
| Maximum | 10 stacks |
| Per stack | +6% damage on **every** hit the player deals (bosses included) and +3% run speed |
| At 10 stacks | +60% damage, +30% speed; the meter pulses white-hot |
| Lightning and shockwaves | +5% reach per stack, and one more lightning jump per 3 stacks |
| Drain | After 6 s without a kill, one stack every 1.2 s |
| Death | Clears it |

Kills by chain lightning count as ordinary kills (+1).

**HUD:** a meter just above the status bar, with ten pips, the current bonus and a bar running down to the drain.

## Moon Shoes

Back to the Future style self-lacing high-tops: silver uppers, a strap over the instep, glowing laces and soles, a light-up heel and copper springs. The model is built (`MeleeGearModels.MoonShoe`), ready for a shop icon and the pockets figure once the shoes are sold; you don't see your own feet in first person.

| Tier | Run speed | Jump height | Extra |
| --- | --- | --- | --- |
| I | +8% | +20% | |
| II | +16% | +40% | |
| III | +22% | +55% | **Double jump** |

Both scenes give tier III. The arena's 8 key steps through the tiers.

**The double jump:** one extra jump per trip into the air. It launches the way the movement keys point at that moment, at least at sprint speed, or straight up with no keys held. A cyan shock ring and a springy boing go with it.

## Test arena

**Scene:** `Assets/Scenes/Initial Test Scenes/MeleeArena.unity`. Build it with **Signal Lost > Melee > Build Melee Arena**; rebuilding replaces the scene.

**Layout:** the early Enemy Test box at about twice the size (60 × 60 m), with its cover, pillars and ramped platform, plus steps up to a 6 m tower for big jumps.

**The player:** end-game gear (`FinaleLoadout`), so every knuckle and shoe tier is owned.

**The horde:** the finale's robot creatures keep spawning around you, in the same mix as the boss fight (weighted to the rushers: crabs 3, armadillos 2, ticks 2, Mudpuppy 1, mortar frog 1). The rate and the cap rise over time: every 2.6 s down to 0.7 s, and from 8 alive up to 28.

**Damage numbers:** they float over everything you hit. White is normal, blue is rage-boosted, gold is a heavy hit.

| Key | Testing tool |
| --- | --- |
| 1 / 2 / 3 / 4 | Burn-Bot / Witch-Bot / Mecha-Buzzard / Robo-Toad (the finale's robot mini-bosses at half health; finishable below a quarter) |
| 5 | Boss dummy: 20 000 health, never fights back, shows damage per second, biggest hit and the rage multiplier |
| 6 | Pause or resume the horde |
| 7 | Knuckle tier: III → off → I → II → III |
| 8 | Moon Shoe tier, the same way |
| 9 | Max rage |
| 0 | Clear the arena |

The panel at the top left lists these.

## How it is built

**Melee (`Scripts/Melee/`):**

- `PlayerFists`: the moves, the hand poses and the knuckles on the gloves.
- `RageMeter` and `RageHud`.
- `ChainArcs`, `OverchargeMark`, `FinisherGlow` and `GaussFx`: the effects, built from LineRenderers and particles like the laser's.
- `MeleeTargets`: who counts as an enemy, small or big.
- `Finishable`: lets a big enemy (the finale's robots) be finished.
- `MeleeGearModels`: the knuckle and shoe models, built in code.
- `MoonShoesFx`: the double jump's effect.
- `MeleeLoadout`: switches it all on for the player and holds the knuckle and shoe tiers (added by the two scenes' `FinaleLoadout`).
- Arena only: `MeleeArena`, `ArenaBossDummy` and `DamageNumbers`.

`MeleeLoadout` adds the fists, rage and the shoe effect at runtime, and only those two scenes add it, so no shared scene or prefab changed.

**Editor:**

- `MeleeArenaBuilder`.

**Shared code touched:**

- `Health`:
  - a static `OutgoingModifier` (rage boosts the player's hits);
  - `AnyDamaged` and `AnyKilled` events.
- `FirstPersonController`:
  - speed bonuses by source;
  - `JumpHeightScale`;
  - the double jump;
  - `Lunge`/`EndLunge`.
- `ScannerToggle.SwapPressed`, so Q can cycle (unused unless the fists are added).
- `PlayerGear`: `Held.Fists` and `AddFists` (the cycle, off unless a scene adds the fists).
- `FirstPersonHands`: the guard and punch poses (only with the fists up).
- `MiningLaser.AimOrigin`/`InputActions`, read by the fists.
- Key bindings: one new line, `finisher` (V).

All of these are additions that do nothing outside the two scenes.

**Checks:** `tools/unity/MeleeSmoke.cs` runs in batch Play mode in the arena. It covers the loadout, Q cycling, each knuckle tier, rage gain, boost, cap and drain, finishers, the double jump, the four robots and the horde.

## Open questions

- **Balance:** all of the numbers above.
- **Health from finishers:** should finishers or melee kills give health back, like Doom's glory kills? Not added.
- **Gamepad:** the finisher has no gamepad button.
- **Story weapons:** the design document calls the laser the sole story weapon and extra weapons a stretch goal. The knuckles are treated as gear for the fists, with Adam's approval.
- **Jump smash:** should it also work with the fists up? It still needs the laser out.
- **Rolling it out:** whether, and where, the knuckles and shoes get sold in the rest of the game (shops, the pockets menu, the main HUD).
