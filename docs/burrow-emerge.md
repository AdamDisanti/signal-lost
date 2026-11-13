# Burrow and emerge

This is a reusable burrow/emergence prototype, currently exercised only by the `BurrowTestCrab` instance in the [BurrowEmergeTest scene](../signal-lost/Assets/Scenes/Initial%20Test%20Scenes/BurrowEmergeTest.unity). The shared Hermit Crab prefab is not given the burrow or signal components; the test scene adds them as instance overrides. Other enemies have not been wired into the behavior.

The implementation and integration points are:

- [`BurrowEmerge`](../signal-lost/Assets/Scripts/Enemies/Common/BurrowEmerge.cs): per-instance phase, proximity check, rise timing, grace timer, body-collider toggle, and signal state.
- [`IBurrowOffset`](../signal-lost/Assets/Scripts/Enemies/Common/IBurrowOffset.cs): the small adapter interface that lets a rig expose a burrow offset without `BurrowEmerge` knowing its hierarchy.
- [`HermitCrabRig`](../signal-lost/Assets/Scripts/Enemies/HermitCrabRig.cs): implements `IBurrowOffset` and adds the offset to its normal visual animation. This avoids moving the `Visual` transform that the rig itself animates.
- [`HermitCrab`](../signal-lost/Assets/Scripts/Enemies/HermitCrab.cs): opts in only when a `BurrowEmerge` component is present; while buried it clears its target, stops its NavMesh path, and skips normal AI. It allows movement after emergence, but gates attack initiation and damage until attack permission is granted.
- [`EnemySignalSource`](../signal-lost/Assets/Scripts/Scanner/EnemySignalSource.cs) and [`ScannerController`](../signal-lost/Assets/Scripts/Scanner/ScannerController.cs): keep underground signals scannable and exclude defeated enemies from selection.

## State and behavior

The component has four phases:

1. **Buried:** begins with its configured body colliders disabled, the signal marked underground, and the rig's burrow offset set to negative depth. The enemy root remains at ground level.
2. **Emerging:** starts when the player enters the horizontal trigger radius. A smoothstep interpolation raises the rig offset to zero over the emergence duration. The source becomes above-ground and body colliders are re-enabled when the rise finishes.
3. **Grace:** the enemy may target and move toward the player, but `CanAttack` remains false for the configured grace duration.
4. **Aggressive:** `CanAttack` becomes true and the enemy's normal attack behavior is allowed.

The phase, timer, signal source, and collider references are instance members. Each enemy with the component therefore runs its own trigger and phase. No static phase or timer is shared between instances.

The test crab's AI checks the optional component. Other Hermit Crabs that do not have `BurrowEmerge` keep their previous AI behavior; the rig's default burrow offset is zero. Other enemy types are not automatically supported: their rig must implement `IBurrowOffset`, and their own AI must enforce equivalent target/attack gates.

## Add burrowing to another enemy

Treat this as an opt-in integration for each enemy type; adding `BurrowEmerge` alone does not automatically make an arbitrary enemy's model move or its AI safe.

1. **Use a test instance first.** Open a test scene and add the components to one enemy instance, rather than editing a shared prefab used by other enemies. Keep the changes isolated until that enemy passes the checks below.
2. **Prepare the enemy rig.** Implement `IBurrowOffset` on a component on the same root GameObject as `BurrowEmerge`. Apply the supplied offset to the rig's computed visual position (for example, add it to the visual's local Y after the regular animation position is calculated). Do not overwrite the animation's base position or move the enemy root. If the rig does not implement the interface, the phase and timers still run, but the model will not visibly emerge.
3. **Add the scanner source.** Add `EnemySignalSource` on the enemy root. `BurrowEmerge` requires it and sets its underground/above-ground signal state as the enemy transitions.
4. **Add and configure `BurrowEmerge`.** Assign its required `Visual Root` reference, even though current visual movement is handled through `IBurrowOffset`. Add the enemy's body colliders to `Body Colliders`; those colliders are disabled while buried and re-enabled after emergence. Do not include unrelated colliders that must remain enabled.
5. **Wire the enemy AI gates.** In the enemy's AI component, reference the optional `BurrowEmerge`. While `IsBuried` is true, clear player/attack targets, stop navigation or movement, and return before target acquisition or offensive logic. Only acquire/retain a player target when `CanTargetPlayer` is true. Require `CanAttack` both before beginning an attack and at the actual damage/attack execution point, so an attack already in progress cannot deal damage during the grace period. Keep movement allowed during grace if desired. Enemies without the component should retain their existing behavior.
6. **Tune and test that instance.** Approach to trigger emergence, verify the collider and scanner transitions, confirm no targeting or damage while buried, confirm movement but no offense during grace, and verify the normal attack loop after grace. If a scene has multiple enemies, test that each one's buried/emerged state and timing are independent.

For a new enemy rig, the adapter contract is simply a writable float:

```csharp
public sealed class ExampleEnemyRig : MonoBehaviour, IBurrowOffset
{
    public float BurrowOffset { get; set; }

    private Vector3 GetAnimatedVisualPosition()
    {
        Vector3 position = CalculateNormalVisualPosition();
        position.y += BurrowOffset;
        return position;
    }
}
```

`CalculateNormalVisualPosition` above represents the rig's existing position calculation; use the project's actual animation/update method rather than adding a second transform writer. Put the interface implementation on the same GameObject as `BurrowEmerge`, because the component looks for the receiver on that object.

## Tuning values

The serialized values on `BurrowEmerge` can be changed in the Inspector; they are not fixed design rules. The script defaults provide starting points, and each enemy instance can use different values without changing another instance:

| Inspector setting | Current default | What it changes |
| --- | ---: | --- |
| Trigger Radius | 3 m | Horizontal distance at which the player starts emergence |
| Emergence Duration | 0.8 s | Time for the offset to rise from buried depth to the surface |
| Burrow Depth | 1.25 m | Starting negative visual offset |
| Post Emergence Grace | 1.25 s | Time after emergence before attacks are allowed |

Tune these values for the enemy's scale, movement, encounter pacing, and playtest results. The original request called for a 3-second grace period, while the current script default and test scene use 1.25 seconds; change the Inspector value on the desired instance if 3 seconds is the intended behavior. Values are constrained to non-negative numbers in the Inspector. Keep a note in the design or feature documentation if a value becomes an agreed game rule rather than a provisional tuning choice.

## Current test scene setup

The `BurrowEmergeTest` scene contains one crab scene instance, a player with a `ScannerRig`, and the existing enemy test arena/NavMesh. The crab remains a prefab instance with scene-added `BurrowEmerge` and `EnemySignalSource` components. The `BurrowEmerge` Visual Root points at the rig's existing `Visual`, but the current script does not write to that transform; motion is performed by `HermitCrabRig` through `IBurrowOffset`, not by reparenting the artwork or moving the crab root. Use the `feature/enemy-burrow-emerge` branch when opening this feature scene.

Current serialized values on the scene instance (also the current script defaults):

| Setting | Value |
| --- | ---: |
| Trigger radius | 3 m |
| Emergence duration | 0.8 s |
| Burrow depth | 1.25 m |
| Post-emergence grace | 1.25 s |

These are provisional test values. The initial feature request specified a 3-second grace period; the current scene and component use 1.25 seconds instead. Confirm the intended production value before tuning or reusing the component. The crab's test-scene `respawnDelay` override is 0, so this scene does not use the prefab's test respawn behavior.

## Scanner behavior

The burrowed crab's active `EnemySignalSource` changes to `SignalType.UnderGround` and remains eligible for scanner selection while buried. When emergence finishes, its signal returns to `AboveGround`. The scanner still observes its normal range limit and quest-signal priority.

Enemy signals with a parent `Health` component are not eligible when `Health.IsDead` is true. If the selected enemy dies, the controller immediately scans again and selects the next eligible target; if there is none, it clears the current target and signal strength, so scanner audio stops. A source without a `Health` in its parent hierarchy remains eligible. Scrap pickups are not scanner targets in this implementation.

## Test and validation

Open the scene in Unity **6000.6.2f1**, wait for compilation, raise the scanner with **Q**, and approach the crab. Verify it is detected while buried, rises when inside its trigger radius, can move but cannot attack during grace, and attacks after grace. If multiple live enemy signal sources are in scanner range, defeat the selected enemy and confirm the scanner switches to another; with no remaining eligible targets, confirm the signal and beeps stop.

The feature author reported successful Play Mode checks for emergence and scanner target handoff. This documentation update did not run Unity compilation or Play Mode tests independently. Check the Console for errors when repeating the test.

## Current limits

- Two enemies use it: the test Hermit Crab instance, and the [Crystal Chameleon](crystal-chameleon.md) prefab. The shared Hermit Crab prefab and other enemy types have not been modified to use the feature.
- **Triggering from code:** untick **Emerge On Proximity** (on by default) so only a call to `Emerge()` starts the rise. The chameleon does this, because only the scanner's camouflage disruption should reveal it. The chameleon's **Hide In Wall** option reuses the same rise, sliding out along the wall instead of up; the wall move and the drop to the floor are the chameleon's own code, not BurrowEmerge's (see [Crystal Chameleon](crystal-chameleon.md)).
- The rig must implement `IBurrowOffset` for visible emergence. Without an offset receiver, the phase still advances, but no visual rise occurs; the current component does not log a warning for a missing receiver. `visualRoot` is currently required as an Inspector reference but is not the transform used for motion.
- The player proximity check uses horizontal distance from the enemy root; there is no line-of-sight requirement.
- The placeholder emergence is a rig-applied vertical offset, not a dedicated animation clip.
- Corpse/respawn lifecycle does not reset `BurrowEmerge` to its initial buried phase. The current test scene has respawn disabled; add and test a reset hook before enabling enemy respawn.
- Scanner discovery depends on an active `EnemySignalSource`; quest signals continue to outrank enemy targets.
