# Chief Toad (pond boss)

A boss for the tutorial meadow's pond, built in its own test arena first so it can be judged before it goes on the tutorial map. The player decides when it is ready to place.

The Chief Toad is a huge crowned toad chieftain. He tore the ship's **cooling system** out of the wreck and strapped it round his body: a pump on his back, segmented coolant tubes glowing blue over his shoulders and hips, glowing wrist cuffs, and a glowing coolant core on his chest. The look follows Lawson's concept art: green skin with red spiral warpaint, a cream belly and tabard, a red cape and head cloth, and a feathered crown with a glowing gem.

He lives at the pond but fights anywhere nearby. Every water attack makes its own water, so the beam, blades and splash waves work the same on dry land as in the pond.

## Try it

1. Open the project in Unity **6000.6.2f1** on `feature/chief-toad-boss` and let scripts compile.
2. Choose **Signal Lost > Chief Toad > Create Test Arena**. It builds the prefab if needed, then `Scenes/Bosses/ChiefToadTest.unity`. The Console prints `CHIEF_TOAD_TEST_ARENA_CREATED_AND_VALIDATED`.
3. Press Play. You start south of the pond with the laser, scanner and flask. Walk within 26 m of the pond's centre to start the fight.

The arena is a meadow bowl about 90 m across, ringed by hills and trees. It has its own pond (14 × 9 m half-widths, wading depth), reeds and lily pads, rocks on the banks, and eight boulders for cover. The pond water reuses the tutorial pond's stylized water material.

Other menu items:
- **Build Prefab** rebuilds `Prefabs/Enemies/Meadow/ChiefToad.prefab` in place (same GUID).
- **Validate** checks the prefab's links and the arena.

Batch builds use `SignalLost.Editor.ChiefToadBuilder.BuildBatch`.

## The fight

750 HP. He hops toward you and likes to fight from about 10 m.

| Attack | Tell | What happens | Counter |
| --- | --- | --- | --- |
| **Water beam** | 1.1 s: throat swells, mist is drawn into his mouth, and a thin aiming line shows the jet's path. | A high-pressure jet, 28 m long: 7 damage every 0.15 s and a steady shove backwards. Phase 1: it turns after you at 40°/s. Phases 2-3: it sweeps 35° each side across where you stood. | Get out of the line, or put a rock between you. |
| **Water blades** | 0.55 s: he rears his right forelimb. | He slings a fan of water crescents at chest height, 20 m/s, 12 damage each, a small shove. Phase 1: 3 blades in 30°. Phase 2: two volleys of 5 in 45°. Phase 3: three volleys. Alternate volleys fill the gaps. | Sidestep; cover stops them. |
| **Jump slam** | 0.55 s crouch, with a ring on the ground where he'll land (aimed a little ahead of where you're running). | He leaps (0.85 s, 5.5 m high) and lands for 24 damage (half at the 4.5 m edge) and a shove. A wall of water then races outward to 13 m for 14 damage. From phase 2 he hops twice and each landing sprays a ring of 6 blades. Phase 3: 8 blades, and a second, bigger wave. | Leave the ring, then **jump the wave** (it only hits you on the ground). |
| **Scream** | 1.1 s: throat sac balloons and he tilts his head back, with a 12 m ring around him. The first time, a banner warns you. | Shock rings and spray. If you're within 12 m and in his line of sight: **stunned for 1.5 s** (no moving, jumping, tools or flask; you can still look around) and 5 damage. | Get out of the ring or behind a boulder. |
| **Tidal spin** (phase 3) | A 1.3 s crouch with a ring around him, then "TIDAL SPIN: jump the beam!". | One full turn in 2.6 s with the beam skimming low (0.45 m above the ground at its 24 m reach). | Jump it as it passes, or hide behind cover. |

**Phases:**
- **1 (full to 66%):** the four attacks.
- **2, "coolant surge" (66% to 33%):** the cooling system cracks and sprays, and he pauses for 1.5 s. Everything is 20% faster: the beam sweeps, the blades fire wider double volleys, and the slam hops twice with blades.
- **3, "coolant overflow" (below 33%):** "CHIEF TOAD: COOLANT OVERFLOW!" and a 1.6 s pause. Everything is 40% faster: the tidal spin is added and every slam sends two waves.

**Weak spot:** the glowing coolant core on his chest takes ×1.5 damage.

**Shared rules:** he follows the mini-boss rules:
- the boss bar appears when he engages;
- the combo finisher can't stun him;
- he resets (full health, phase 1, home) if you die or get twice his 26 m arena radius away.

**Defeat:** the **cooling system** drops as a glowing pickup. It counts for the tutorial stage and not toward the desert exit's three parts.

All numbers are first-pass placeholders for play-testing.

## Files

- `Scripts/Enemies/Meadow/`:
  - `ChiefToad` (the boss; built on `DesertMiniBoss`)
  - `WaterBlade` (the crescents)
  - `SplashWave` (the landing wave)
- `Scripts/Audio/ToadAudio.cs`: his sounds, added by `SoundDirector` (see [sound effects](sound-effects.md)).
- `Scripts/Player/PlayerStun.cs`: the player stun, with a screen effect. `FirstPersonController.Stun` stops walking and jumping.
- `Scripts/Effects/ParticleBurst.cs`: new **Water** (droplets) and **Mist** (spray) kinds.
- `ShipPartDrop` and `StageProgress`: parts are now counted per stage (`StageProgress.Tutorial` for the cooling system).
- `Editor/ChiefToadBuilder.cs`: the prefab and the arena.

## Before it goes on the tutorial map

These are open questions:
- **Where he waits:** the tutorial pond is at (-60, 62), about 50 × 34 m, larger than the test pond. His 26 m arena radius may need to grow to match.
- **Story:** how he fits the tutorial, for example whether the cooling system is required before leaving.
- **Balance:** he is far tougher than anything else in the tutorial.
- **Missing pieces:** his own recorded sounds (`ToadAudio` reuses the placeholder set, pitched down), and a proper rig (he is animated in code from separate parts).

## Validation

- All scripts compile against Unity 6000.6.2f1's assemblies with no new warnings.
- `ChiefToadBuilder.BuildBatch` ran in Unity 6000.6.2f1. It built the prefab and the arena, both validated, and the Console showed no errors or shader errors.
- [ChiefToadSmoke.cs](../tools/unity/ChiefToadSmoke.cs) passed in batch Play Mode on a disposable copy. Over about 75 s across the three phases (the phase changes forced by damage):
  - **Phase 1:** blades, slam, beam and scream.
  - **Phase 2:** scream, double slam with blades, and the sweeping beam.
  - **Phase 3:** overflow, tidal spin, slam, scream and beam.
  - **Each phase change** triggered on time, and he was back on the NavMesh after each phase.
  - **Stun:** the scream stunned the player twice.
  - **Defeat:** the cooling system dropped and counted for the tutorial stage only.
- **Not checked yet:** the smoke is not a look-and-feel test. Hand play-testing is still needed for:
  - how the model reads against the concept art;
  - the effects, the timing and the dodge windows;
  - balance and performance.
