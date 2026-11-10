# Final boss: Cavendish and the Safekeeper

The final level: the crash-site island laid waste, and Cavendish piloting a mech built from the ship parts the player handed him "for safe keeping". Planned with Adam on 11-06 and 11-09; this is the plan and the first playable slice.

"The Safekeeper" is a working name for the mech.

## The plan

### Story

- **The betrayal.** Every time the player rested at camp between islands, Cavendish slipped away at night to build the mech out of the parts he was "keeping safe".
- **Why.** He wants the ship for himself: he has been stranded far longer than the mechanic.
- **Eugene.** Eugene saw none of it coming and can't believe his friend did this.
- **After the win.** The player rebuilds the ship and flies off to the space casino.

### Level flow

1. **Arrival** (cutscene): the boat lands on the crash-site island, now a wasteland.
2. **The approach** (5-10 minutes): enemies from all the earlier islands, and storytelling along the way.
3. **The reveal** (cutscene): the mech rises.
4. **The fight:** three phases.
5. **The escape:**
   - the mech goes critical with Cavendish stuck inside;
   - the player grabs the Reactor Core and runs to the ship under a timer while Eugene's crew hauls in the other parts;
   - the ship launches as the island explodes.

All cutscenes are in-engine and skippable.

### The mech

- **Look:** a tall humanoid "power ranger" mech, about 15 m, with Cavendish visible in a glass cockpit.
- **Weapons:** each island's main part is one weapon.
- **Armour:** the mini-boss parts are bolted on as armour and trim, so players recognise what they handed over.
- **Weak spot:** the Reactor Core is its heart.

### The fight

- **Shape:** raid style, three phases, each harder than the last. At most 15 minutes for a new player.
- **Shields:** at 66% and at 33% a shield goes up and the mech is immune until the player finishes a task.
- **Tasks:** each phase has a new one.

| Phase | Weapons (main parts) | Task to break the shield |
| --- | --- | --- |
| 1 (100-66%) | **Cooling System:** freezing sweep, ice that slows. **Engine Core:** thruster charge, fire trail. | **Find the conduits:** the scanner finds three hidden power conduits; break them while adds harass you. |
| 2 (66-33%) | **Navigation Computer:** homing missiles and a target mark you clear by breaking line of sight. | **Vent the reactor:** carry coolant cells to vent sockets (slower, can't shoot while carrying, the mech focuses you). |
| 3 (33-0%) | **Flight Control Module:** jump-jets and aerial slams. **Landing Gear** too (Mudlands). Everything faster. | **Ground the mech:** hold the launch clamps one at a time while dodging; then the Reactor Core opens for the final damage window. |

Rules for the fight:

- **Dying** restarts the current phase, not the fight.
- **The flask** works as it does everywhere else.
- **Drunk Driving:** the car rams the mech for big damage, but never stuns it, so it can't skip mechanics.
- **Eugene's crew** (one rebindable key, G by default):
  - Eugene and three cousins fight beside you for 15 s, then a 60 s cooldown;
  - they draw some of the mech's attacks, chip it during damage windows and clear adds during tasks;
  - they can't be hurt.

### Stage order

The finale is last in the stage-skip testing tool. The islands in between (Mudlands, Fog Forest, Crystal Caves) go above it once their order is decided.

## What's built (first slice, 11-09)

The scene is `Scenes/Large Areas/Wasteland.unity`. Build it with **Signal Lost > Finale > Build Wasteland Arena**. It replaces the scene, so make edits in `Editor/FinaleBuilder.cs`.

- **The island:**
  - The tutorial island's shape, laid waste, so players recognise it.
  - Ash and cracked earth, dead trees and stumps (the Mudlands models), wreckage everywhere, and smoke columns on the horizon.
  - A dusty low sun.
  - The old camp knocked flat: the tent down, the fire cold, the stands toppled.
  - An arrival dock where the tutorial's departure dock was.
  - The arena is levelled ground south of the wreck, about 80 m across. It has boulder and hull-chunk cover, and seven scrap heaps for the conduits.
- **The mech** (`Scripts/Finale/MechModel.cs`, `CavendishMech.cs`):
  - Built in code like the Mudlands bosses, with the parts as described above.
  - The little capybara ears on the helmet are a nod to the pilot.
  - Phase 1's weapons work:
    - **Freezing sweep:** a blue wedge on the ground, then a beam that sweeps across it, leaving ice that slows you. Cover blocks it.
    - **Thruster charge:** a red strip on the ground, then a dash that leaves burning ground.
    - **Stomp:** if you stand under it.
  - The glowing Reactor Core on the chest takes ×1.5 damage.
- **The fight** (`FinaleEncounter.cs`):
  - Walking into the arena plays the intro dialogue (subtitles only for now), and the mech stands up.
  - At 66% the shield goes up. Three conduits come alive and show on the scanner, and adds come in. Break all three and the mech staggers, then phase 2 starts.
  - Dying puts the mech back to the start of the current phase.
- **Eugene's crew** (`CrewCallIn.cs`, `AllyCrab.cs`): G calls in Eugene and the whole gang of ten cousins (since the difficulty pass below).
- **HUD** (`FinaleHud.cs`):
  - a boss bar with phase notches and a teal "SHIELDED" cover with the conduit count;
  - the crew widget (ready, in the fight, or cooling down).
- **Playtest loadout** (`FinaleLoadout.cs`):
  - every upgrade that exists, at its top tier (the tutorial's and the Mudlands');
  - the Learner's Permit, so Drunk Driving works;
  - two of each supply, and spare scrap.
  - Remove it once saves carry between scenes.
- **Shared code touched (small):**
  - a Finale stage, last in `StageSkip`;
  - the `call_crew` key binding;
  - `PlayerSlowReceiver.ExtraSlow`, so the ice can slow you like Sludge's oil;
  - the scene added to the build list.

## Difficulty pass (11-09, after Adam's first playtest)

Adam found the first slice far too easy: he never dropped below half health. He asked for a **very hard** fight (a new player should expect five or more deaths). The conduit tasks stay as placeholders for now.

- **The mech hits harder, over bigger areas, and moves faster.**

  | | Before | Now |
  | --- | --- | --- |
  | Walk speed | 3.4 | 6.5 |
  | Gap between attacks | 1.8 s | 0.9 s |
  | Health | 4500 | 6000 |
  | Freezing sweep, per tick (about 70 a second) | 7 | 11 |
  | Freezing sweep wedge | 26 m, 40° | 32 m, 55° |
  | Thruster charge | 26 damage, 6 m wide | 40 damage, 9 m wide |
  | Stomp | 22 damage, 8 m | 38 damage, 11.5 m |
  | Fire trail | 7 s | 22 s |

- **New attacks.** Each phase adds some, and it never uses the same attack twice in a row.

  | Attack | Phase | What it does |
  | --- | --- | --- |
  | **Thruster lunge** | 1+ | The back engines launch it at you. A 10 m ring marks the landing; it follows you for most of the wind-up, then locks. The landing does 45 and leaves a ring of fire. |
  | **Rocket barrage** | 1+ | The shoulder pods rain two volleys of seven missiles into warning circles round you: 26 each, and each leaves a burning crater. |
  | **Coolant tidal wave** | 2+ | It slams the cannon down and a freezing wall races across the whole arena. Jump it, or take 32. Phase 3 sends two. |
  | **Scrap magnet** | 2+ | The reactor drags you in at 5.6 m/s for 2.4 s. Walking (4 m/s) loses; sprinting (7 m/s) escapes. Anyone still close gets a 42-damage double-fist slam. |
  | **Napalm flyover** | 3 | It jets up and strafes a line through you, carpeting it with fire that burns for 18 s, so the safe ground keeps shrinking. |

- **The horde.** Cavendish's robot versions of the creatures pour in for the whole fight: Robo-Crabs, Spark Ticks, Mortar Frogs, Hubcap Armadillos and Mudpuppies.
  - At the start: one every 5.5 s, at most 6 alive.
  - It grows each minute and each phase, and jumps while the shield is up, up to 26 alive and one every second.
  - When Cavendish goes down, they all power off.
- **Robot mini-bosses** (`FinaleRobots.cs`). When the shield goes up, three of Cavendish's robot copies of earlier bosses come in:

  | Shield | Robot mini-bosses |
  | --- | --- |
  | 66% | **Robo-Toad**, **Mecha-Buzzard**, **Burn-Bot** |
  | 33% | **Mecha-Worm**, **Witch-Bot**, **Silt-Bot** |

  - Each is the real boss (its own script, moves and attacks) in a robot skin, at 70% of its health.
  - They don't drop ship parts or key items, and they keep fighting after the shield breaks.
  - The finale HUD lists them under the boss bar instead of their own bars.
- **Robot skins** (`Robotify.cs`):
  - every colour becomes metal of the same lightness (gunmetal to the mech's off-white), keeping a hint of the original hue;
  - glowing parts turn Cavendish's teal (orange for Burn-Bot and the Mecha-Worm);
  - an antenna with a blinking light, a glowing visor and rivets are bolted on.
- **The whole crew.** G now brings Eugene and all ten Bucket Brothers. With eleven crabs, each pinch on the mech is smaller (about 5% of its health per call); they shred the horde.

## Repair stages (11-10, Adam's second playtest)

The difficulty pass went too far, so the shield phases became **repair stages**:

- **What happens.** At 66% and at 33% the mech kneels, hunched over, to repair itself:
  - it can't be hurt and doesn't move;
  - its arms work at its chest in a shower of welding sparks;
  - its only attack is a light rocket volley (five missiles round you) about every 10 s.
- **What ends it.** The next phase starts once **both robots are beaten and the conduits are broken**. The HUD bar reads "REPAIRING: ROBOTS x / 2  CONDUITS x / 3".
- **The robots, two at a time** (a 2v1):

  | Repair | Robots |
  | --- | --- |
  | First (66%) | **Burn-Bot** and **Witch-Bot** |
  | Second (33%) | **Mecha-Worm** and **Mecha-Buzzard** |

  - They have half the original boss's health, and their hits on the player do 70% (any hit from a robot, its fire or its projectiles).
  - This uses a small shared hook, `Health.DamageModifier`.
  - Robo-Toad and Silt-Bot still exist in code but aren't in the line-up.
- **The horde** keeps spawning as before.
- **Fix:** the phase now advances the moment the repair ends, not after the mech's stagger. A hit during the stagger used to raise the same shield again, re-spawning the robots and stalling the fight. This was most likely the "phase 2 didn't start" flake.

### Placeholders (not built yet)

| Placeholder | Stand-in now |
| --- | --- |
| The Navigation Computer and Flight Control Module weapons from the plan | The new attacks above stand in, by phase |
| Phase 2 task (coolant cells) | A second set of four conduits |
| Phase 3 task (launch clamps) | Phase 3 just fights to zero |
| Meltdown, escape run, launch, credits | A message says they aren't built |
| Cutscenes (arrival, reveal, ending) and the approach | Not built. The player starts at the arena edge, and the intro is subtitles only. |
| Each island's own enemies in the horde | Robot versions of the creatures built so far |
| Island outfits for the wasteland | None (no `StageTheme` yet) |

## Try it

1. Open `Scenes/Large Areas/Wasteland.unity` and press Play, or right-arrow to it from the Desert Dunes (testing tool).
2. **Loadout:** health reads 130 / 130, and the pockets show every upgrade installed.
3. **Intro:** walk north into the arena. Cavendish and Eugene talk, and the mech stands up by the wreck.
4. **Weapons:**
   - **Freezing sweep:** a blue wedge appears, then the beam sweeps across it. Hide behind a boulder and you take nothing. Step on the ice and you slow down.
   - **Thruster charge:** a red strip appears, then the mech dashes along it and leaves fire behind.
   - **Stomp:** stand under the mech and it stamps.
   - **Thruster lunge:** a big red ring chases you, then locks, and the mech lands on it with a ring of fire.
   - **Rocket barrage:** a cluster of red circles appears round you, then a second one where you ran.
   - **From phase 2:** the coolant tidal wave (jump the blue wall as it reaches you) and the scrap magnet (sprint away from the mech, or get slammed).
   - **Phase 3:** the napalm flyover leaves a long line of fire.
   - Robot creatures keep coming the whole time, more of them as the fight goes on.
5. **Reactor Core:** shoot the glowing core on its chest. It takes more damage than the body.
6. **Shield task:**
   - At two-thirds health the mech kneels to repair, shielded. Burn-Bot and Witch-Bot arrive and are listed under the boss bar. Raise the scanner (Q): it beeps toward the nearest conduit.
   - Beat both robots and break the three conduits. The mech staggers, and phase 2 starts.
7. **Eugene's crew:** press G. Eugene and all ten cousins scuttle in and fight for 15 s, then the widget counts down 60 s.
8. **Dying:** die in phase 2. You respawn at the arena's south edge, and the boss bar is back at two-thirds, not full.
9. **The car:** with the car out (hold F), ram the mech. It explodes and hurts the mech, but the mech keeps fighting.
10. **The end:** at one-third, it repairs again and the Mecha-Worm and Mecha-Buzzard arrive. Finish phases 2 and 3. "THE SAFEKEEPER IS DOWN!" appears, and the mech slumps, smoking.

Checked on a disposable copy of the project in batch mode:

- `tools/unity/FinaleSmoke.cs` passes. It covers everything above:
  - each attack forced at a player standing still, and checked to land;
  - the six robots spawning and staying up;
  - the horde, the eleven-crab crew, the phase retry, the car and the end;
  - no errors or exceptions anywhere in the fight.
- The "phase 2 didn't start" flake is fixed (see the repair stages above). The full smoke now passes run after run.
- The tutorial-story and combat-rules smokes pass.
- The core-systems smoke fails at "Bought Bigger Boom.": its "buy every tier" step predates the Mudlands' key-item upgrades. This was already the case on main.
- Not yet hand-played in the editor; the numbers are first guesses.

## Open questions

- Balance: mech health (6000), the robots' 70% health, the horde's growth, damage, attack timings and the shield thresholds all need playtesting against the 15-minute cap.
- The mech's look is preliminary (Adam expects to revise it).
- Do crew pinches count for anything beyond chip damage (for example, interrupting an attack)?
- Should the shield phases have a time limit (an enrage)?
- Where the approach's enemies come from, once each island's enemies exist.
