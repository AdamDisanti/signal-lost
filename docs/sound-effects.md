# Sound effects

66 original placeholder sounds and a small audio system that plays them. Owner: Nicole. Every sound was synthesized from oscillators and filtered noise by [make_sfx.py](../tools/audio/make_sfx.py), so there are no samples, packs or licenses to track. Swap any of them for a recorded or downloaded sound whenever you like.

## How it works

There's nothing to set up in a scene. `SoundDirector` (in `Assets/Scripts/Audio/`) starts itself when the game runs, and every scene it enters gets sound automatically. That includes the Tutorial, the Testing Sandbox, Desert Dunes and the title screen, and it keeps working after the tutorial is rebuilt. Each second it looks for things to add sound to:

| What it finds | What it adds |
| --- | --- |
| The player | `PlayerAudio`: laser, footsteps, hurt, flask, scanner, items, scrap, death and wake-up sounds |
| Every hermit crab (the basic crab, the Bucket Brothers, Eugene) | `CreatureAudio`: skittering, hits, blocks, death squeak, Eugene's surrender whimper |
| The Chief Toad | `ToadAudio`: hop thuds, hits, death, and a sound for each attack (it listens to `ChiefToad.Cued`) |
| Particle effects named "…Fire…", "Campfire", "…Sparks…", and any `FireEffect` | Crackling fire loop; electrical fires and sparks also fizz |
| "Pond Water" | Lapping water loop |
| Every button, slider and toggle | Hover tick and click |
| The scanner's beep | Swapped for a sonar ping. Amanda's timing, pitch and volume logic is unchanged |

The other sounds react to things that already happen in the game:
- **Menus** play a pop when one opens or closes.
- **The merchant** plays a cha-ching when its shop opens, and a bell when Eugene arrives back at camp.
- **The tutorial** plays a chime at each new stage.
- **A recovered main ship part** gets a fanfare.

Sounds only watch the existing systems and don't change how they behave. No existing scripts, scenes or prefabs were edited. Dialogue has no voice sounds.

**Distance:** world loops (fires, sizzle, pond) are loud only up close and drop off quickly. They're at half volume by 15% of their range, a quiet background from 40%, and silent at the edge (`Sfx.NearbyFalloff`). The wreck's fires carry about 32 m, so you hear it faintly as you approach. The campfire carries 20 m, and the small trench fires 18 m. Only the main ship fires fizz, so the cluster doesn't stack up.

**Volume:** gameplay sounds follow **Settings > Sound > Sound effects**, and the ambience loops follow the **Music** slider until the game has music. Master volume still controls everything. Menu sounds keep playing while the pause menu has the game's audio paused.

The audio system doesn't run in batch mode, so the team's automated smoke tests are unaffected.

## The sounds

All clips are in `Assets/Audio/Resources/SignalLostAudio/` and are played by file name.

| Group | Files |
| --- | --- |
| Laser | `laser_swing_1/2`, `laser_slam` (combo finisher), `laser_charge` (rising hum), `laser_charge_full` (ding), `laser_charge_hold` (loop at full charge), `laser_shot_weak/strong`, `bolt_impact`, `laser_blast` (full-charge explosion), `laser_fizzle` (clicking during cooldown), `hit_marker` |
| Player | `footstep_grass/sand/dirt_1–4` (grass in the tutorial, sand in the desert, dirt elsewhere), `player_jump`, `player_land`, `player_hurt_1–3`, `death_sting` ("SIGNAL LOST"), `blackout_whoosh`, `wake_ringing` (hangover ringing during the eyelid wake-ups) |
| Flask | `flask_uncork`, `flask_glug`, then `flask_ahh` when sober, `flask_hiccup` when tipsy and `flask_burp` from the third sip on. A tipsy mechanic also hiccups now and then |
| Low health | `low_health_heartbeat` (loop, added 10-23 by `make_sfx.py`): plays under everything at 30 health or less, louder the lower it goes |
| Music | `Signal lost main menu music` (Adam's track, not synthesized): the title screen, looped seamlessly; follows the **Music** slider |
| Gear and scrap | `scanner_raise/lower`, `scanner_ping`, `item_pickup`, `scrap_vacuum`, `scrap_clink_1–3` |
| Crabs | `crab_skitter` (loop, louder the faster it walks), `crab_snip` (claw hits you), `crab_hit_1/2` (bucket clang), `crab_block`, `crab_death`, `crab_whimper` |
| Chief Toad (reused, pitched down) | `footstep_grass_1-4` (hop thuds), `crab_hit_1/2` (hit), `crab_death` + `laser_blast` (death), `laser_charge` (beam and spin charge), `laser_blast` (beam fires), `water_lap` sped up (jet loop while the beam or spin runs; a shorter rush for the splash wave), `laser_swing_1/2` (blade volley), `bolt_impact` (blade splash), `player_jump` and `laser_slam` + `player_land` (slam), `laser_charge_hold` (scream charge), `crab_whimper` + `laser_blast` (the scream), `wake_ringing` (your ears while stunned), `laser_fizzle` and an `electric_sizzle` loop (coolant cracking and overflowing) |
| Shop | `shop_chaching`, `shop_bell` |
| Interface | `ui_hover`, `ui_click`, `ui_open`, `ui_close`, `quest_chime`, `part_fanfare` |
| Ambience loops | `amb_meadow` (wind and birds: tutorial, title screen), `amb_desert` (howling wind), `amb_wind` (sandbox), `fire_crackle`, `electric_sizzle`, `water_lap` |

## Changing a sound

- **Swap it:** replace the `.wav` with any sound that has the same file name. WAV, OGG and MP3 all work; keep the name and delete the old file. Record where a downloaded sound came from in [assets.md](assets.md).
- **Change how it's generated:** edit its function in `tools/audio/make_sfx.py`, run `python3 tools/audio/make_sfx.py signal-lost/Assets/Audio/Resources/SignalLostAudio` (needs Python with numpy and scipy), and Unity re-imports the changed files.
- **Change loudness or when it plays:** volumes are the numbers passed to `Sfx.Play`/`PlayAt` in `PlayerAudio.cs`, `CreatureAudio.cs` and `SoundDirector.cs`.
- **Pick a level's ambience:** edit `SoundDirector.AmbienceFor`.
- **Add a sound from code:** `Sfx.Play("name")` for a 2D sound, `Sfx.PlayAt("name", position)` for one in the world, `Sfx.AddLoop(...)` for a loop.

## Test checklist

1. Open `Scenes/MainMenu`, press Play. Meadow ambience fades in, and buttons tick on hover and click.
2. Start the Tutorial:
   - The wake-up has a ringing tone.
   - The campfire crackles louder as you approach, the wreck's fires crackle and fizz, and the pond laps.
3. Walk, sprint and jump on grass: you hear the jump and the landing. Footsteps are switched off for now (`PlayerAudio.FootstepsEnabled`); the clips stay for later.
4. With the laser:
   - Swing twice, then slam.
   - Hold right-click: a rising hum, a ding at full charge, then a steady crackle.
   - Release: a zap, then an impact or explosion where the bolt lands.
   - Right-click again during the cooldown: a fizzle.
5. Press Q: the scanner whirs up, and its beeps are now pings. Q again whirs down.
6. Fight the Bucket Brothers:
   - Crabs skitter as they move and clang when hit.
   - They squeak when they go down.
   - Their claws snip when they hit you, and you grunt.
   - Eugene whimpers when it gives up.
7. Pick up scrap (slurp and clink) and the flask (item jingle). Drink: uncork, glug, "ahh". Keep drinking for hiccups and a burp, then a woozy whoosh on blackout.
8. Open the merchant's shop: cha-ching. Open and close the inventory and pause menu: pops. Turn the Sound effects and Music sliders down: sounds and ambience get quieter.
9. Die: "SIGNAL LOST" sting.

## Limitations

- These are synthesized placeholders. Burps and grunts in particular will sound better recorded by the team, as the design document suggests.
- No music yet. Ambience uses the Music slider for now.
- Only hermit crabs and the Chief Toad have creature sounds. The frog, desert fodder, desert mini-bosses and the Core Worm only get their fires; they'd need their own sounds.
- Merchant purchases are still "coming soon". The cha-ching on spending scrap will start working once buying does.
- Not yet checked in Unity Play Mode by the author. Run the checklist above and report anything off.
