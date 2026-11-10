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
- **Eugene's crew** (`CrewCallIn.cs`, `AllyCrab.cs`): G calls in Eugene and three Bucket Brothers.
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

### Placeholders (not built yet)

| Placeholder | Stand-in now |
| --- | --- |
| Phase 2 and phase 3 weapons | Phase 1's weapons, faster |
| Phase 2 task (coolant cells) | A second set of four conduits |
| Phase 3 task (launch clamps) | Phase 3 just fights to zero |
| Meltdown, escape run, launch, credits | A message says they aren't built |
| Cutscenes (arrival, reveal, ending) and the approach | Not built. The player starts at the arena edge, and the intro is subtitles only. |
| Each island's enemies as adds | Hermit crabs, Spark Ticks, Hubcap Armadillos and Mudpuppies |
| Island outfits for the wasteland | None (no `StageTheme` yet) |

## Try it

1. Open `Scenes/Large Areas/Wasteland.unity` and press Play, or right-arrow to it from the Desert Dunes (testing tool).
2. **Loadout:** health reads 130 / 130, and the pockets show every upgrade installed.
3. **Intro:** walk north into the arena. Cavendish and Eugene talk, and the mech stands up by the wreck.
4. **Weapons:**
   - **Freezing sweep:** a blue wedge appears, then the beam sweeps across it. Hide behind a boulder and you take nothing. Step on the ice and you slow down.
   - **Thruster charge:** a red strip appears, then the mech dashes along it and leaves fire behind.
   - **Stomp:** stand under the mech and it stamps.
5. **Reactor Core:** shoot the glowing core on its chest. It takes more damage than the body.
6. **Shield task:**
   - At two-thirds health the shield goes up. Raise the scanner (Q): it beeps toward the nearest conduit. Adds arrive.
   - Break the three conduits. The mech staggers, and phase 2 starts.
7. **Eugene's crew:** press G. Eugene and three cousins scuttle in and fight for 15 s, then the widget counts down 60 s.
8. **Dying:** die in phase 2. You respawn at the arena's south edge, and the boss bar is back at two-thirds, not full.
9. **The car:** with the car out (hold F), ram the mech. It explodes and hurts the mech, but the mech keeps fighting.
10. **The end:** finish phases 2 and 3. "THE SAFEKEEPER IS DOWN!" appears, and the mech slumps, smoking.

Checked on a disposable copy of the project in batch mode:

- `tools/unity/FinaleSmoke.cs` passes. It covers everything above, plus the crew and adds resetting on a retry.
- The tutorial-story and combat-rules smokes pass.
- The core-systems smoke fails at "Bought Bigger Boom.": its "buy every tier" step predates the Mudlands' key-item upgrades. This was already the case on main.
- Not yet hand-played in the editor; the numbers are first guesses.

## Open questions

- Balance: mech health (4500), damage, attack timings and the shield thresholds all need playtesting against the 15-minute cap.
- The mech's look is preliminary (Adam expects to revise it).
- Do crew pinches count for anything beyond chip damage (for example, interrupting an attack)?
- Should the shield phases have a time limit (an enrage)?
- Where the approach's enemies come from, once each island's enemies exist.
