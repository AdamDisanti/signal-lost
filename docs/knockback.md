# Directional combat knockback

The shared `Health.TakeDamage` call accepts optional [Knockback](../signal-lost/Assets/Scripts/Combat/Knockback.cs) settings and a **world-space incoming direction**. Existing two-argument calls remain damage-only in the new system. Existing enemy flinch/stagger reactions are retained; an explicit knockback payload replaces their old physical push rather than stacking a second one.

## Configure an attack

Each profile has:

- **Mode:** `None`, `Shove`, or `Launch`.
- **Speed:** initial horizontal velocity in metres/second.
- **Upward Speed:** initial upward velocity for `Launch`; ignored for `Shove`.
- **Deceleration:** horizontal slowdown in metres/second squared; must be positive (current presets use 18).

`None` is the default for optional player-weapon profiles. Set all values when enabling a previously empty profile. Shoves do not cancel a target's existing jump or fall. Launches add upward motion; player gravity and ceiling collision remain in the existing movement controller. Successive hits replace horizontal impulse rather than stacking unbounded speed; a weaker launch does not cancel a stronger upward player velocity. Normal movement input remains available.

For a melee impact, push away from the attack origin:

```csharp
[SerializeField] private Knockback impact = new Knockback(KnockbackMode.Launch, 8f, 5f);

// Use the actual strike's origin, not the target's forward or the owner's current facing.
targetHealth.TakeDamage(damage, gameObject, impact,
    targetHealth.transform.position - strikeOrigin);
```

For a projectile, use its **current travel direction**, even when its owner is far away or it has reflected. `LaserBolt.Spawn(..., onHit, knockback)` carries the optional profile through flight and reflection. On `MiningLaser`, **Shot Knockback** and the per-strike **Combo Knockback** array are default-off. If the combo array is shorter than the combo, its last entry is reused, matching the existing combo-arc convention. Empty means no added knockback. Existing legacy enemy hit reactions still occur when these profiles are off.

Explosions use the target position minus the blast center, not the attack owner's position. An exactly centered blast launches upward without choosing an arbitrary horizontal direction. `FodderExplosion.Explode(..., knockback)` initially applies the profile only to players while preserving its existing damage to enemies. The optional `knockbackEnemies: true` enables the same profile for enemy targets on future player-owned blasts; `ScrapBomb.Drop` forwards the same option. No current enemy blast opts into that flag.

Damage must be accepted and nonlethal for its impulse to apply. Invulnerable, dead, nonpositive-damage and invalid-number damage calls cannot push the target. Malformed knockback settings/directions produce no motion. Character death, health restoration and player control-disable/respawn clear pending controller knockback; dynamic Rigidbody momentum remains governed by its physics. `Health.Damaged` and other existing event signatures are unchanged. During damage callbacks, `HitKnockback` and `HitDirection` expose the current hit context, including through nested damage; the frog can, eel snorkel and mini-boss weak-spot relays preserve it without bypassing their existing body/part damage deduplication.

## Initially active attacks

Provisional speeds are tuning values, not a new balance specification. Configure the corresponding enemy component on a prefab/instance; `None` disables that attack's motion.

| Attack | Motion | Horizontal / upward speed |
| --- | --- | --- |
| Hermit/merchant crab heavy claw slam | Launch away from crab | 5 / 3 |
| Hubcap Armadillo rolling impact | Launch along roll travel | 7 / 3 |
| Burnout dash impact | Launch along dash travel | 9 / 5 |
| Burnout tail whip | Existing shove, migrated to shared system | 9 / 0 |
| Burnout exhaust blast | Launch along exhaust direction | 10 / 5 |
| Old Buzzard dive / stomp | Launch away from ground impact | 7 / 5 |
| Old Buzzard peck | Existing shove, migrated | 5.4 / 0 |
| Old Buzzard wing gust | Existing shove, migrated | 12 / 0 |
| Old Buzzard scrap-bomb blast | Launch away from blast | 8 / 5 |
| Sludge claw snaps | Existing shove, migrated | 5 / 0 |
| Sludge fuel-tank backfire | Launch away from blast | 8 / 5 |
| Jerry-Can Beetle explosion | Launch away from blast | 8 / 5 |

Ordinary frog goo, eel bites, tick damage, damage-test pads, fire ticks, and currently equipped player attacks gain no new knockback. Damage, attack timing and AI targeting are not rebalanced. In particular, Burnout multi-dash aiming is a separate task and is unchanged.

## Receiving motion

- **Player:** the existing `FirstPersonController` combines knockback with input/gravity in its single collision-tested `CharacterController.Move`. Enemies no longer independently move the player's controller after a hit.
- **Grounded NavMesh enemies:** [KnockbackReceiver](../signal-lost/Assets/Scripts/Combat/KnockbackReceiver.cs) is added on the first explicit hit. It temporarily takes position control from the agent and uses a collision-tested capsule controller for the shove/launch. Existing crab, frog and desert AI pause movement while it is active. The ground projection stays on connected navigation; the agent resumes its prior movement settings on landing. The temporary controller is disabled outside knockback.
- **Dynamic Rigidbody targets:** velocity-change impulses use Unity physics. Their own physics drag/gravity govern recovery; character-controller deceleration is not imposed on rigidbodies.
- **Weak-spot-only Health components:** forward the profile/direction to the body's Health; do not add movement to the detached hitbox itself.

Scripted flight with a disabled agent is not a grounded NavMesh receiver: it needs flight-specific handling before enabling player knockback against airborne bosses. This does not limit their attacks knocking the player back. Actors without a supported movement body remain damageable but are not translated arbitrarily through walls.

## Regression checks

Copy `tools/unity/KnockbackSmoke.cs` and `TutorialEnemiesSetup.cs` (temporary-project guard) into `Assets/Editor` of a disposable matching project under the system temporary directory. Do not install smoke scripts in the real project. Run Unity 6000.6.2f1 with:

```text
-batchmode -nographics -projectPath <temporary-project> -executeMethod KnockbackSmoke.Start -logFile <log-path>
```

Omit `-quit`; the harness exits with a pass/fail code. It drives real projectile collision, movement controllers, navigation and selected enemy attack entry points. `KnockbackSmoke.StartTutorial` additionally checks the saved tutorial, merchant surrender/navigation recovery, and the actual tutorial death/respawn/stage-reset flow. Tests use a fixed simulated frame interval so editor asset-import stalls cannot make a short launch finish before an assertion observes it. The existing editor-only SearchDatabase startup exception is excluded; gameplay exceptions fail the run.

Unity **6000.6.2f1** compilation and both knockback Play Mode scenarios passed, including:

- All four incoming shove directions independent of player facing/owner position; launch lift and gravity; no impulse for rejected/lethal damage; restore/control-disable cleanup.
- Swept wall and ceiling collision, retaining outward motion when leaving contact and sliding along blocking walls.
- Default-off and opt-in real projectiles, a reflected bolt's new direction, and player-owned bolts pushing agent enemies.
- Agent launch, landing/navigation recovery, repeated hits, active crab/frog AI handoff, and frog weak-spot relay.
- Explosion multi-collider deduplication, independent nested blasts, default player-only knockback, and explicit opt-in enemy blast knockback.
- Real crab AI damage and its slam entry point; Burnout dash, Buzzard dive/stomp, beetle detonation and falling scrap-bomb entry points. These selected entry-point checks are not full natural boss fights.
- Friendly merchant recovery leaves its knockback-only collider disabled; tutorial death restores the player without stale momentum and all seven enemy agents remain above the new terrain.
- Nested damage preserves hit context and raises death only once.

Repeat `TutorialTerrainSmoke.Start` after combat changes to ensure the tutorial navigation fix remains valid. These are not rendered, physical-input, target-hardware performance, or final balance/comfort checks.
