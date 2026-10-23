# Fire effect

`FireEffect` is a general-purpose particle fire for anywhere in the game: stage dressing (burning wreckage, campfires, vents, torches) and gameplay (the desert mini-bosses' fire uses it). It is built entirely in code, so there are no textures, shaders or particle assets to maintain. Owner: Lawson.

A fire is made of:

- flame tongues that lick upward, shrink and redden as they rise
- a white-hot core low in the fire
- a glowing bed of coals on the ground
- rising embers
- smoke
- a flickering orange light

A **Jet** fire blows the same flame along the object's forward axis instead, for exhausts, torches and flamethrowers.

## Placing a fire in a scene

- **GameObject > Signal Lost > Fire**, **Fire (Large)**, **Fire (Small)** or **Fire Jet** drops a fire at the Scene view's centre (or under the selected object).
- Or drag a prefab from `Assets/Prefabs/Effects/`: `Fire` (campfire), `Fire Large` (burning wreckage), `Fire Small` (torch flame), `Fire Jet` (exhaust or vent).
- Or add **Signal Lost > Effects > Fire Effect** to any GameObject.

It previews live in the Scene view while you edit. Ground fires burn upward from the object's position, and jets point along its blue (Z) axis.

| Setting | What it does |
| --- | --- |
| Shape | **Ground**: a burning patch. **Jet**: a flame blowing forward. |
| Radius / Height | Size of the patch and how tall the flames reach (Jet: nozzle radius and flame length). |
| Intensity | How fierce: density, brightness and light. |
| Tint | Multiplies the flame colours (white is ordinary fire; try a pale blue for a gas flame). |
| Smoke, Smoke Amount, Smoke Colour | The smoke column. Burning fuel looks right with a lot of black smoke. |
| Embers, Ground Glow | The rising sparks and the coal bed. |
| Cast Light, Light Colour, Light Intensity | The flickering light. Only **8** fires light the scene at once (`FireEffect.MaxLights`); the rest still glow. Turn it off on fires that are close together. |
| Burn Time | 0 burns forever (level design). Above 0, it dies down by itself after that many seconds. |
| Destroy When Out | Removes the GameObject once it has died down (for spawned fires). |

It does no damage by itself. For a fire that hurts, use the mini-bosses' `FireZone` (below) or add your own trigger.

## From code

```csharp
using SignalLost.Effects;

// A burning patch, 1 m radius, that dies down after 5 s and removes itself.
FireEffect fire = FireEffect.Spawn(position, 1f, burnTime: 5f);

// A smoky fuel fire with a custom tint.
FireEffect.Spawn(position, 1.5f, tint: new Color(1f, 0.9f, 0.8f), smokeAmount: 0.9f);

// A flame jet on a nozzle, pointing along the nozzle's local +Z.
FireEffect jet = FireEffect.SpawnJet(nozzle, Vector3.zero, Quaternion.identity, radius: 0.2f, length: 2f);
jet.Strength = 0.5f;   // 0 to 1: grow, throttle or fade it
jet.Extinguish();      // dies down; Reignite() lights it again
```

`FireZone.Spawn(style, point, radius, source, immune, duration)` (namespace `SignalLost.Enemies.Desert`) is a `FireEffect` that also damages every `Health` standing in it and sets off oil and beetles. A `FireStyle` sets its tint, smoke, fierceness, damage and duration.

`ParticleBurst.Spawn(kind, position, direction, ...)` makes one-shot bursts with the same look: `Sand`, `Smoke`, `Sparks`, `Fireball`. `ParticleBurst.Explosion(position, radius)` combines them into an explosion. Each kind has one shared, long-lived particle system that bursts are emitted into, so a burst creates no objects. Call `ParticleBurst.Prewarm()` when a scene or fight loads to build them up front.

## How it is built

- **Textures:** a flame tongue, a soft glow and a smoke puff, drawn in code at startup (`EffectMaterials`).
- **Materials:** copied at runtime from two URP particle materials in `Assets/Resources/SignalLostEffects/`: `ParticleAdditive` for flames, embers and glows, and `ParticleAlpha` for smoke and sand. Keep these two files. They make sure builds include the URP particle shader's transparent variants. Without them it falls back to finding the shader by name, which works in the Editor but may draw nothing in a build.
- **Particle systems:** child systems of a hidden `Fire FX` object. In edit mode they are hidden preview objects that are never saved into the scene or prefab.

## Not checked yet

The code compiles against the Unity 6000.6.2f1 assemblies, but the effect has **not been viewed in Unity**. The look (sizes, colours, smoke density), how it performs with many fires at once, and the Scene-view preview need checking in the Editor. All settings are placeholders to tune.
