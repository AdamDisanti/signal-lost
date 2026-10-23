using System;
using System.Collections.Generic;
using SignalLost.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// Shared base for the three desert mini-bosses (the "Chop Shop": Burnout, Old Buzzard and Sludge). Each one has
    /// bolted a piece of the ship's engine assembly onto itself and fights with it.
    ///
    /// What every mini-boss gets from here:
    /// - Patrol: walks a loop around its home until the player enters its arena (or hits it).
    /// - Engaged: a boss health bar appears (<see cref="MiniBossHud"/>), and its FodderSpawner starts sending fodder.
    /// - Two phases: at Phase Two At (default half health) the stolen part is damaged and the fight changes.
    /// - Leash: if the player leaves the arena for a few seconds, or dies, it resets to full health, phase 1, and
    ///   clears its fodder.
    /// - Defeat: the fodder spawner stops (fodder already out keeps fighting), and the stolen ship part drops as a
    ///   glowing pickup (<see cref="ShipPartDrop"/>). <see cref="Defeated"/> and <see cref="DefeatedCount"/> let a
    ///   future encounter/boss-unlock system count the three.
    /// - Weak-spot parts: <see cref="RelayPart"/> forwards hits on a part's own collider to the boss, once per swing.
    ///
    /// It builds on <see cref="DesertFodder"/> for finding the player, line of sight, the meerkat "spotted" alert,
    /// the hit flash and the death animation. All numbers are placeholders to tune.
    /// </summary>
    public abstract class DesertMiniBoss : DesertFodder
    {
        /// <summary>Hits at least this strong count as heavy: the overhead slam and charged shots past about a third.</summary>
        protected const float HeavyHit = 20f;

        /// <summary>How many mini-bosses each biome has (the design's three encounters before the main boss).</summary>
        public const int MiniBossesPerBiome = 3;

        [Header("Mini-boss")]
        [SerializeField] private string displayName = "Mini-boss";
        [Tooltip("The ship part it stole, shown when it is recovered.")]
        [SerializeField] private string partName = "Ship part";
        [Tooltip("Its fight radius: it engages when the player is this close to its home, and resets (full health) " +
                 "once they are twice as far away, the same rule as every enemy's Territory.")]
        [SerializeField, Min(5f)] private float arenaRadius = 20f;
        [Tooltip("Phase 2 starts at or below this fraction of its health.")]
        [SerializeField, Range(0.05f, 0.95f)] private float phaseTwoAt = 0.5f;
        [Tooltip("Seconds the phase change takes (the part breaking). It doesn't attack meanwhile.")]
        [SerializeField, Min(0f)] private float phaseShiftTime = 1.5f;

        [Header("Patrol")]
        [Tooltip("Optional patrol route. Empty: a loop of points around its home is generated.")]
        [SerializeField] private Transform[] patrolPoints = new Transform[0];
        [SerializeField, Min(0f)] private float patrolRadius = 9f;
        [SerializeField, Range(3, 12)] private int generatedPatrolPoints = 5;
        [SerializeField, Min(0.1f)] private float patrolSpeed = 2.2f;
        [SerializeField, Min(0f)] private float patrolPause = 1.2f;

        [Header("Fodder")]
        [Tooltip("Sends fodder while this mini-boss is engaged; stops for good when it dies.")]
        [SerializeField] private FodderSpawner fodderSpawner;
        [Tooltip("Untick to fight the mini-boss on its own.")]
        [SerializeField] private bool fodderWhileEngaged = true;

        [Header("Reward")]
        [Tooltip("The stolen part on its body. It detaches on defeat and becomes a pickup.")]
        [SerializeField] private Transform shipPart;

        /// <summary>Raised when any mini-boss is defeated.</summary>
        public static event Action<DesertMiniBoss> Defeated;
        /// <summary>Mini-bosses defeated since Play started.</summary>
        public static int DefeatedCount { get; private set; }

        public string DisplayName => displayName;
        public string PartName => partName;
        public float PhaseTwoAt => phaseTwoAt;
        public float HealthFraction => Health.Normalized;
        public int Phase { get; private set; } = 1;
        public bool Engaged { get; private set; }
        public bool InPhaseShift => Time.time < phaseShiftUntil;
        protected float ArenaRadius => arenaRadius;
        protected float PhaseShiftElapsed => Time.time - phaseShiftStart;
        protected float PhaseShiftTime => phaseShiftTime;
        protected FodderSpawner Spawner => fodderSpawner;

        /// <summary>Set by a mini-boss while it is exposed (for example Burnout overheating): extra damage taken.</summary>
        protected float DamageTakenMultiplier { get; set; } = 1f;
        /// <summary>The part dropped on defeat. Old Buzzard swaps it for the wing torn off in phase 2.</summary>
        protected Transform ShipPart { get => shipPart; set => shipPart = value; }

        private readonly List<Vector3> route = new List<Vector3>();
        private int routeIndex;
        private float routeWait, phaseShiftUntil, phaseShiftStart = float.NegativeInfinity;
        private bool applyingBonus, shiftEnded = true, patrolStarted;
        private int lastBodyHitFrame = -1;
        private float pendingPartDamage;
        private GameObject pendingPartSource;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            DefeatedCount = 0;
            Defeated = null;
        }

        // ---------- Unity / DesertFodder hooks ----------

        // The laser's combo finisher only stuns small enemies (meeting notes 2026-10-20).
        protected override bool CanBeStunned => false;

        protected override void OnEnable()
        {
            base.OnEnable();
            Health.Damaged += NoteBodyHit;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Health.Damaged -= NoteBodyHit;
            MiniBossHud.Unregister(this);
        }

        protected virtual void OnDestroy()
        {
            if (TargetHealth != null) TargetHealth.Died -= OnPlayerDied;
        }

        protected sealed override void OnStarted()
        {
            if (TargetHealth != null) TargetHealth.Died += OnPlayerDied;
            if (fodderSpawner != null) fodderSpawner.enabled = false;
            BuildRoute();
            MiniBossHud.Ensure();
            OnBossStarted();
        }

        protected sealed override void Tick(float dt)
        {
            if (!Engaged)
            {
                UpdatePatrol(dt);
                if (WantsToEngage()) Engage();
            }
            else
            {
                UpdateLeash(dt);
                if (!Engaged) { AnimateBoss(dt); return; } // just reset
                if (InPhaseShift) UpdatePhaseShift(dt);
                else
                {
                    if (!shiftEnded)
                    {
                        shiftEnded = true;
                        OnPhaseShiftEnded();
                    }
                    TickCombat(dt);
                }
            }
            AnimateBoss(dt);
        }

        protected sealed override void OnHit(float amount, GameObject source, Vector3 away)
        {
            if (applyingBonus) return; // the bonus part of a hit: nothing more to react to
            if (DamageTakenMultiplier > 1.001f)
            {
                applyingBonus = true;
                Health.TakeDamage(amount * (DamageTakenMultiplier - 1f), source);
                applyingBonus = false;
                if (IsDead || Health.IsDead) return;
            }
            if (!Engaged) Engage();
            if (Phase == 1 && Health.Normalized <= phaseTwoAt) BeginPhaseTwo();
            OnBossHit(amount, source, away);
        }

        protected sealed override void OnDeath()
        {
            Engaged = false;
            MiniBossHud.Unregister(this);
            if (fodderSpawner != null) fodderSpawner.Stop();
            DamageTakenMultiplier = 1f;

            OnBossDeath();
            if (shipPart != null)
            {
                ShipPartDrop.Create(shipPart, partName, transform.position);
                shipPart = null;
            }
            DefeatedCount++;
            MiniBossHud.Message($"{displayName.ToUpperInvariant()} DEFEATED!  The {partName.ToLowerInvariant()} dropped.", new Color(1f, 0.85f, 0.35f));
            Defeated?.Invoke(this);
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (pendingPartDamage <= 0f) return;
            float amount = pendingPartDamage;
            GameObject source = pendingPartSource;
            pendingPartDamage = 0f;
            pendingPartSource = null;
            // The same swing also hit the body: that hit already counted.
            if (lastBodyHitFrame == Time.frameCount || IsDead) return;
            Health.TakeDamage(amount, source);
        }

        // ---------- For each mini-boss ----------

        protected virtual void OnBossStarted() { }
        /// <summary>The fight, every frame while engaged and not changing phase.</summary>
        protected abstract void TickCombat(float dt);
        protected virtual void OnEngaged() { }
        /// <summary>Phase 2 begins: break the part, change visuals and moves.</summary>
        protected abstract void OnPhaseTwo();
        /// <summary>Every frame of the phase change.</summary>
        protected virtual void UpdatePhaseShift(float dt) { }
        protected virtual void OnPhaseShiftEnded() { }
        /// <summary>Called after every hit that didn't kill it (after the phase check).</summary>
        protected virtual void OnBossHit(float amount, GameObject source, Vector3 away) { }
        /// <summary>A relayed part was hit. Return the damage the boss should take (0 if handled here).</summary>
        protected virtual float OnPartHit(int part, float amount, GameObject source) => amount;
        /// <summary>Reset to the start-of-fight state (phase 1 visuals, idle).</summary>
        protected abstract void OnReset();
        protected virtual void OnBossDeath() { }
        /// <summary>Animation, every frame while alive.</summary>
        protected abstract void AnimateBoss(float dt);

        /// <summary>Walk the patrol loop. Old Buzzard overrides this to circle in the sky.</summary>
        protected virtual void UpdatePatrol(float dt)
        {
            if (!AgentReady || route.Count == 0) return;
            if (!patrolStarted)
            {
                // remainingDistance isn't meaningful before the first destination, so set one straight away.
                patrolStarted = true;
                Chase(route[routeIndex], patrolSpeed);
                return;
            }
            bool arrived = !Agent.pathPending && Agent.remainingDistance <= Agent.stoppingDistance + 0.2f;
            if (!arrived) return;
            if (routeWait <= 0f) routeWait = patrolPause;
            routeWait -= dt;
            if (routeWait > 0f) return;
            routeIndex = (routeIndex + 1) % route.Count;
            Chase(route[routeIndex], patrolSpeed);
            routeWait = 0f;
        }

        // ---------- Shared helpers ----------

        /// <summary>
        /// Makes a part with its own collider and Health (a weak spot) forward hits to this boss. The part's Health
        /// never runs out. If the same swing also hit the body, the part's hit is dropped so it never counts twice,
        /// but <see cref="OnPartHit"/> still runs (for backfires and similar).
        /// </summary>
        protected void RelayPart(Health part, int id)
        {
            if (part == null) return;
            part.Damaged += (h, amount, source) =>
            {
                h.Restore();
                if (IsDead) return;
                float forward = OnPartHit(id, amount, source);
                if (forward > pendingPartDamage)
                {
                    pendingPartDamage = forward;
                    pendingPartSource = source;
                }
            };
        }

        /// <summary>True if this hit came from the player's melee swing (rather than a bolt or an explosion).</summary>
        protected bool IsMeleeHit(GameObject source) =>
            source != null && TargetLaser != null && source == TargetLaser.gameObject && TargetLaser.IsSwinging;

        /// <summary>Angle in degrees between where it faces and the player (flat).</summary>
        protected float AngleToTarget()
        {
            if (Target == null) return 180f;
            return Vector3.Angle(transform.forward, FlatDirectionTo(Target.position));
        }

        /// <summary>Pushes the player along a direction over a few frames (CharacterController.Move).</summary>
        protected void ShovePlayer(Vector3 direction, float distance)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f || TargetBody == null) return;
            shoveDirection = direction.normalized;
            shoveRemaining = distance;
        }

        private Vector3 shoveDirection;
        private float shoveRemaining;

        protected void UpdateShove(float dt)
        {
            if (shoveRemaining <= 0f || TargetBody == null || !TargetBody.enabled) return;
            float step = Mathf.Min(shoveRemaining, 9f * dt);
            TargetBody.Move(shoveDirection * step);
            shoveRemaining -= step;
        }

        /// <summary>Damages the player if alive. Returns true if it landed.</summary>
        protected bool HurtPlayer(float damage) => TargetAlive && TargetHealth != null && TargetHealth.TakeDamage(damage, gameObject);

        /// <summary>Flat distance from the player to its home: how far into the arena they are.</summary>
        protected float PlayerDistanceFromHome() => Target == null ? float.MaxValue : Flat(Target.position - Home);

        protected static float Flat(Vector3 offset)
        {
            offset.y = 0f;
            return offset.magnitude;
        }

        /// <summary>The ground below a point (ignores this boss and anything with Health), or the point itself.</summary>
        protected Vector3 GroundBelow(Vector3 point, float up = 2f)
        {
            RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * up, Vector3.down, 60f, sightMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 ground = new Vector3(point.x, Home.y, point.z);
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.collider is CharacterController || hit.collider.GetComponentInParent<Health>() != null) continue;
                if (hit.distance >= best) continue;
                best = hit.distance;
                ground = hit.point;
            }
            return ground;
        }

        // ---------- Engage, leash, phases, reset ----------

        private bool WantsToEngage()
        {
            return TargetAlive && PlayerDistanceFromHome() <= arenaRadius;
        }

        private void Engage()
        {
            if (Engaged || IsDead) return;
            Engaged = true;
            LastSeen = Time.time;
            if (fodderSpawner != null) fodderSpawner.enabled = fodderWhileEngaged;
            MiniBossHud.Register(this);
            OnEngaged();
        }

        private void UpdateLeash(float dt)
        {
            if (TargetAlive && PlayerDistanceFromHome() > arenaRadius * Territory.LeashMultiplier) ResetBoss();
        }

        private void BeginPhaseTwo()
        {
            Phase = 2;
            phaseShiftStart = Time.time;
            phaseShiftUntil = Time.time + phaseShiftTime;
            shiftEnded = false;
            DamageTakenMultiplier = 1f;
            Halt();
            MiniBossHud.Message($"{displayName.ToUpperInvariant()}: {partName.ToUpperInvariant()} DAMAGED!", new Color(1f, 0.55f, 0.25f));
            OnPhaseTwo();
        }

        private void OnPlayerDied(Health _)
        {
            if (!IsDead && Engaged) ResetBoss();
        }

        /// <summary>Back to full health, phase 1, at home, not engaged; its fodder is cleared.</summary>
        protected void ResetBoss()
        {
            if (IsDead) return;
            Engaged = false;
            Phase = 1;
            phaseShiftUntil = 0f;
            shiftEnded = true;
            DamageTakenMultiplier = 1f;
            shoveRemaining = 0f;
            Health.Restore();
            MiniBossHud.Unregister(this);
            if (fodderSpawner != null)
            {
                fodderSpawner.Clear();
                fodderSpawner.enabled = false;
            }
            if (AgentReady)
            {
                Agent.Warp(Home);
                Halt();
            }
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            Visual.localPosition = VisualRest;
            Visual.localRotation = VisualRestRotation;
            routeIndex = 0;
            patrolStarted = false;
            OnReset();
        }

        private void NoteBodyHit(Health _, float amount, GameObject source) => lastBodyHitFrame = Time.frameCount;

        private void BuildRoute()
        {
            route.Clear();
            foreach (Transform point in patrolPoints)
                if (point != null) route.Add(point.position);
            if (route.Count > 0 || patrolRadius <= 0f) return;
            float offset = UnityEngine.Random.value * Mathf.PI * 2f;
            for (int i = 0; i < generatedPatrolPoints; i++)
            {
                float a = offset + i * Mathf.PI * 2f / generatedPatrolPoints;
                Vector3 p = Home + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * patrolRadius;
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, NavMesh.AllAreas)) route.Add(hit.position);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 centre = Application.isPlaying ? Home : transform.position;
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
            Vector3 previous = centre + Vector3.right * arenaRadius;
            for (int i = 1; i <= 48; i++)
            {
                float a = i * Mathf.PI * 2f / 48f;
                Vector3 next = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * arenaRadius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
