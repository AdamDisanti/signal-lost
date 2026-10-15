using SignalLost.Combat;
using SignalLost.Player;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.Enemies
{
    /// <summary>
    /// Basic enemy: a hermit crab with the design document's four states.
    /// Idle: wanders near home until it sees the player. Chase: follows them on the NavMesh, keeping its eyes
    /// and claws turned toward them (or, optionally, scuttling sideways like a real crab).
    /// Attack: raises its big claw, which glows hotter as it winds up (the telegraph), then lunges and slams it
    /// down. It stops turning partway through the wind-up, so a late sidestep or backstep dodges it; a miss gets
    /// the claw stuck in the ground for a long recovery, which is the player's opening. Dead: flips over and sinks.
    /// Light hits make it flinch; heavy hits (the slam, strong bolts) knock it back and make it hide in its
    /// shell for a moment, which also interrupts an attack.
    /// Shell hide: if it watches the player charge a shot for a moment, it tucks in and flips its bucket face down
    /// over itself. Once covered, laser bolts cannot hurt it: they bounce off the bucket and fly back at the player.
    /// A melee hit knocks it out of hiding, and it comes out by itself once the player stops charging. This stops
    /// charged shots from range being the only answer.
    /// All numbers are placeholders to tune.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(HermitCrabRig))]
    public sealed class HermitCrab : MonoBehaviour, IBoltReflector
    {
        private enum State { Idle, Chase, Attack, Stagger, Guard, Dead }
        private enum AttackPhase { Windup, Strike, Recover }

        [Header("Target")]
        [Tooltip("Who to chase. Empty finds the player automatically.")]
        [SerializeField] private Transform target;
        [SerializeField] private LayerMask sightMask = ~0;

        [Header("Senses")]
        [SerializeField, Min(1f)] private float detectRange = 12f;
        [Tooltip("Gives up the chase beyond this distance.")]
        [SerializeField, Min(1f)] private float loseRange = 18f;
        [Tooltip("Seconds without seeing the player before giving up.")]
        [SerializeField, Min(0f)] private float forgetTime = 3f;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float wanderSpeed = 1.2f;
        [Tooltip("Slower than the player's walk (4) so you can back off while fighting.")]
        [SerializeField, Min(0.1f)] private float chaseSpeed = 3.2f;
        [SerializeField, Min(0f)] private float wanderRadius = 4f;
        [Tooltip("While chasing, keep facing the player and side-step toward them. Off: scuttle sideways instead.")]
        [SerializeField] private bool facePlayerWhileChasing = true;
        [Tooltip("Degrees per second it turns to keep facing the player.")]
        [SerializeField, Min(10f)] private float chaseTurnSpeed = 360f;

        [Header("Attack")]
        [Tooltip("Starts an attack when the player is this close (centre to centre).")]
        [SerializeField, Min(0.5f)] private float attackRange = 1.6f;
        [Tooltip("The claw hits if the player is still this close when it slams down.")]
        [SerializeField, Min(0.5f)] private float attackReach = 1.9f;
        [Tooltip("Total angle in front of the crab the slam covers. Narrower makes sidestepping easier.")]
        [SerializeField, Range(10f, 180f)] private float attackArc = 90f;
        [SerializeField, Min(0f)] private float attackDamage = 15f;
        [Tooltip("Seconds the claw is raised before it strikes. This is the player's warning.")]
        [SerializeField, Min(0.05f)] private float windupTime = 0.9f;
        [Tooltip("Part of the wind-up (0 to 1) during which it keeps turning to aim at the player. After that its aim " +
                 "is locked, so moving late makes it miss.")]
        [SerializeField, Range(0f, 1f)] private float windupTracking = 0.5f;
        [SerializeField, Min(0.05f)] private float strikeTime = 0.14f;
        [Tooltip("Recovery after a slam that hits.")]
        [SerializeField, Min(0f)] private float recoverTime = 0.6f;
        [Tooltip("Recovery after a slam that misses: the claw is stuck in the ground. This is the player's opening.")]
        [SerializeField, Min(0f)] private float missRecoverTime = 1.6f;
        [Tooltip("How far it stumbles backwards when it rips a stuck claw free.")]
        [SerializeField, Min(0f)] private float stumbleDistance = 0.8f;
        [Tooltip("Pause after an attack before it can attack again.")]
        [SerializeField, Min(0f)] private float attackCooldown = 0.7f;
        [SerializeField, Min(0f)] private float lungeDistance = 0.6f;
        [Tooltip("How far a hit shoves the player.")]
        [SerializeField, Min(0f)] private float playerShove = 0.6f;

        [Header("Getting hit")]
        [Tooltip("Hits this strong or stronger knock it back, make it hide in its shell and cancel its attack. " +
                 "20 means the overhead slam and shots charged past about a third, but not jabs or bashes.")]
        [SerializeField, Min(0f)] private float staggerThreshold = 20f;
        [SerializeField, Min(0f)] private float staggerTime = 0.9f;
        [Tooltip("Knockback distance for a 20-damage hit (the slam); scales with damage, up to double.")]
        [SerializeField, Min(0f)] private float knockback = 1.1f;

        [Header("Shell hide")]
        [Tooltip("Hide in the shell and reflect laser bolts when it sees the player charging a shot.")]
        [SerializeField] private bool shellGuard = true;
        [Tooltip("Seconds it must watch the player charging before it hides. Quick shots fired sooner get through.")]
        [SerializeField, Min(0f)] private float hideAfterCharging = 0.5f;
        [Tooltip("Damage a reflected bolt keeps when it flies back at the player (0 to 1).")]
        [SerializeField, Range(0f, 1f)] private float reflectDamageMultiplier = 0.5f;
        [Tooltip("Random wobble, in degrees, on a reflected bolt's aim. 0 aims it straight at the player.")]
        [SerializeField, Range(0f, 30f)] private float reflectSpread = 4f;
        [Tooltip("Seconds after the player stops charging before it comes out.")]
        [SerializeField, Min(0f)] private float guardHoldTime = 0.9f;
        [Tooltip("Seconds after coming out before it can hide again.")]
        [SerializeField, Min(0f)] private float guardCooldown = 0.5f;

        [Header("Death")]
        [Tooltip("Seconds after dying before it vanishes.")]
        [SerializeField, Min(0f)] private float corpseTime = 3.5f;
        [Tooltip("Test scenes only: seconds after vanishing before it comes back. 0 means it stays dead.")]
        [SerializeField, Min(0f)] private float respawnDelay;

        private NavMeshAgent agent;
        private Health health;
        private HermitCrabRig rig;
        private Collider[] colliders;
        private Health targetHealth;
        private CharacterController targetBody;
        private MiningLaser targetLaser;
        private readonly RaycastHit[] sightHits = new RaycastHit[16];

        private State state = State.Idle;
        private AttackPhase phase;
        private float stateTime, phaseTime, lastSeen = float.NegativeInfinity, nextAttack, nextRepath, nextWander;
        private bool hitLanded, missed, started;
        private Vector3 home;
        private Quaternion homeRotation;
        private Vector3 knockDirection;
        private float knockRemaining, knockSpeed;
        private Vector3 shoveDirection;
        private float shoveRemaining;
        private float lastChargeSeen, nextGuard, chargeWatched;

        public bool IsDead => state == State.Dead;
        public bool IsAttacking => state == State.Attack;
        public bool IsGuarding => state == State.Guard;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            rig = GetComponent<HermitCrabRig>();
            // Only the crab's own collider on this object. Shell colliders are removed when the shell is put on,
            // so collecting children here would keep references to destroyed colliders.
            colliders = GetComponents<Collider>();
            home = transform.position;
            homeRotation = transform.rotation;
        }

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        // Start, not Awake: the NavMesh is built at runtime in Awake by NavMeshBaker, so it exists by now.
        private void Start()
        {
            if (target == null)
            {
                var player = FindAnyObjectByType<FirstPersonController>();
                if (player != null) target = player.transform;
            }
            if (target != null)
            {
                targetHealth = target.GetComponentInParent<Health>();
                targetBody = target.GetComponentInParent<CharacterController>();
                targetLaser = target.GetComponentInParent<MiningLaser>();
            }

            agent.enabled = true;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                home = hit.position;
            }
            else
            {
                Debug.LogWarning("Hermit crab is not near a NavMesh; it will stand still. Is there a NavMeshBaker in the scene?", this);
            }
            started = true;
            Enter(State.Idle);
        }

        private void Update()
        {
            if (!started) return;
            float dt = Time.deltaTime;
            stateTime += dt;
            UpdateShove(dt);

            if (state == State.Dead)
            {
                UpdateDead();
                return;
            }
            if (!agent.isOnNavMesh)
            {
                rig.MoveSpeed = 0f;
                return;
            }

            UpdateKnockback(dt);

            switch (state)
            {
                case State.Idle: UpdateIdle(); break;
                case State.Chase: UpdateChase(); break;
                case State.Attack: UpdateAttack(dt); break;
                case State.Stagger: UpdateStagger(); break;
                case State.Guard: UpdateGuard(); break;
            }

            rig.MoveSpeed = agent.velocity.magnitude;
            rig.Sideways = !facePlayerWhileChasing && state == State.Chase && agent.velocity.sqrMagnitude > 0.25f;
        }

        // ---------- States ----------

        private void Enter(State next)
        {
            state = next;
            stateTime = 0f;
            rig.Windup = rig.Strike = 0f;
            rig.ClawStuck = false;
            rig.Hidden = next == State.Stagger;
            rig.ShellDown = next == State.Guard;
            if (!agent.isOnNavMesh) return;

            agent.isStopped = next == State.Attack || next == State.Stagger || next == State.Guard || next == State.Dead;
            // In attacks, guarding, and while chasing face first, this script turns the crab itself.
            agent.updateRotation = next != State.Attack && next != State.Guard && !(next == State.Chase && facePlayerWhileChasing);
            if (next == State.Idle)
            {
                agent.speed = wanderSpeed;
                agent.stoppingDistance = 0.2f;
                agent.SetDestination(home);
                nextWander = Time.time + Random.Range(1f, 3f);
            }
            else if (next == State.Chase)
            {
                agent.speed = chaseSpeed;
                agent.stoppingDistance = attackRange * 0.8f;
                nextRepath = 0f;
            }
            else if (next == State.Guard)
            {
                lastChargeSeen = Time.time;
            }
            if (next != State.Guard) chargeWatched = 0f;
            if (agent.isStopped) agent.velocity = Vector3.zero;
        }

        private void UpdateIdle()
        {
            if (TargetAlive && CanSee(detectRange))
            {
                rig.Alert();
                lastSeen = Time.time;
                Enter(State.Chase);
                return;
            }

            // Wander: pause, then amble to a random spot near home.
            bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.05f;
            if (arrived && Time.time >= nextWander)
            {
                Vector3 random = home + Random.insideUnitSphere * wanderRadius;
                if (NavMesh.SamplePosition(random, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
                    agent.SetDestination(hit.position);
                nextWander = Time.time + Random.Range(2f, 4.5f);
            }
        }

        private void UpdateChase()
        {
            if (!TargetAlive)
            {
                Enter(State.Idle);
                return;
            }

            float distance = FlatDistance(target.position);
            if (CanSee(loseRange)) lastSeen = Time.time;
            if (distance > loseRange || Time.time - lastSeen > forgetTime)
            {
                Enter(State.Idle);
                return;
            }

            if (Time.time >= nextRepath)
            {
                agent.SetDestination(target.position);
                nextRepath = Time.time + 0.2f;
            }

            // Keep its eyes on the player. If it loses sight, face where it is walking instead.
            if (facePlayerWhileChasing)
            {
                bool seen = Time.time - lastSeen < 0.5f;
                Vector3 look = seen ? target.position : transform.position + agent.desiredVelocity;
                TurnToward(look, chaseTurnSpeed * Time.deltaTime);
            }

            WatchCharging(Time.time - lastSeen < 0.3f);
            if (ShouldGuard(distance))
            {
                Enter(State.Guard);
                return;
            }

            if (distance <= attackRange && Time.time >= nextAttack && Time.time - lastSeen < 0.3f)
                BeginAttack();
        }

        // ---------- Shell hide ----------

        /// <summary>Counts how long the crab has watched the player charge a shot; resets when they stop.</summary>
        private void WatchCharging(bool canSee)
        {
            bool charging = targetLaser != null && targetLaser.isActiveAndEnabled && targetLaser.IsCharging;
            chargeWatched = charging && canSee ? chargeWatched + Time.deltaTime : 0f;
        }

        /// <summary>True once it has watched the player charge long enough.</summary>
        private bool ShouldGuard(float distance)
        {
            if (!shellGuard || Time.time < nextGuard) return false;
            return chargeWatched >= hideAfterCharging;
        }

        private void UpdateGuard()
        {
            if (!TargetAlive)
            {
                Enter(State.Idle);
                return;
            }

            float distance = FlatDistance(target.position);
            if (CanSee(loseRange)) lastSeen = Time.time;
            if (targetLaser != null && targetLaser.IsCharging) lastChargeSeen = Time.time;

            // Come out once the player has stopped charging for a moment (or has gone). Getting close does not bring
            // it out; a melee hit does (see OnDamaged).
            if (Time.time - lastChargeSeen > guardHoldTime || distance > loseRange)
            {
                nextGuard = Time.time + guardCooldown;
                Enter(distance > loseRange ? State.Idle : State.Chase);
            }
        }

        /// <summary>
        /// IBoltReflector: while hiding, bolts bounce off the bucket and fly back toward whoever fired them, with a
        /// little random wobble. They no longer hurt the crab.
        /// </summary>
        public bool TryReflect(Vector3 hitPoint, Vector3 incoming, Transform shooter, out Vector3 newDirection, out float damageMultiplier)
        {
            newDirection = -incoming;
            damageMultiplier = reflectDamageMultiplier;
            // Only once the bucket has actually come down over it; a shot during the flip still hits.
            if (state != State.Guard || !rig.IsShellCovering) return false;

            Transform aimAt = shooter != null ? shooter : target;
            if (aimAt != null)
            {
                // Aim at the chest (the player's origin is at their feet).
                newDirection = aimAt.position + Vector3.up * 1.2f - hitPoint;
            }
            if (reflectSpread > 0f)
            {
                Vector3 axis = Vector3.Cross(newDirection, Random.onUnitSphere);
                if (axis.sqrMagnitude > 0.0001f)
                    newDirection = Quaternion.AngleAxis(Random.Range(-reflectSpread, reflectSpread), axis) * newDirection;
            }
            rig.Clang(-incoming);
            return true;
        }

        private void BeginAttack()
        {
            Enter(State.Attack);
            phase = AttackPhase.Windup;
            phaseTime = 0f;
            hitLanded = false;
            missed = false;
        }

        private void UpdateAttack(float dt)
        {
            phaseTime += dt;
            switch (phase)
            {
                case AttackPhase.Windup:
                    // Aim at the player early in the wind-up, then lock the aim so a late dodge works.
                    if (target != null && phaseTime < windupTime * windupTracking) TurnToward(target.position, 270f * dt);
                    rig.Windup = Mathf.Clamp01(phaseTime / windupTime);
                    if (phaseTime >= windupTime) NextPhase(AttackPhase.Strike);
                    break;

                case AttackPhase.Strike:
                    rig.Windup = 1f;
                    rig.Strike = Mathf.Clamp01(phaseTime / strikeTime);
                    agent.Move(transform.forward * (lungeDistance / strikeTime) * dt);
                    if (!hitLanded && phaseTime >= strikeTime * 0.5f)
                    {
                        hitLanded = true;
                        missed = !TryHitTarget();
                    }
                    if (phaseTime >= strikeTime) NextPhase(AttackPhase.Recover);
                    break;

                case AttackPhase.Recover:
                    // After a miss the claw stays stuck in the ground, then pulls free over the last 0.4 s.
                    float duration = missed ? missRecoverTime : recoverTime;
                    float pullOut = missed ? Mathf.Min(0.4f, duration) : duration;
                    float k = Mathf.Clamp01((phaseTime - (duration - pullOut)) / Mathf.Max(pullOut, 0.01f));
                    bool stuck = missed && k <= 0f;
                    // The moment it rips the claw free: the pull throws it backwards.
                    if (rig.ClawStuck && !stuck) StartKnockback(-transform.forward, stumbleDistance, 0.5f);
                    rig.ClawStuck = stuck;
                    rig.Windup = rig.Strike = 1f - k;
                    if (phaseTime >= duration)
                    {
                        nextAttack = Time.time + attackCooldown;
                        Enter(TargetAlive ? State.Chase : State.Idle);
                    }
                    break;
            }
        }

        private void NextPhase(AttackPhase next)
        {
            phase = next;
            phaseTime = 0f;
        }

        /// <summary>Lands the slam if the player is still in reach and in front. Returns false on a miss.</summary>
        private bool TryHitTarget()
        {
            if (!TargetAlive) return false;
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.magnitude > attackReach) return false;
            if (toTarget.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, toTarget) > attackArc * 0.5f) return false;

            if (targetHealth != null) targetHealth.TakeDamage(attackDamage, gameObject);
            shoveDirection = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : transform.forward;
            shoveRemaining = playerShove;
            return true;
        }

        private void UpdateStagger()
        {
            if (stateTime < staggerTime) return;
            nextAttack = Time.time + attackCooldown * 0.5f;
            Enter(TargetAlive ? State.Chase : State.Idle);
            lastSeen = Time.time;
        }

        private void UpdateDead()
        {
            rig.MoveSpeed = 0f;
            if (stateTime >= corpseTime && stateTime - Time.deltaTime < corpseTime) rig.SetVisible(false);
            if (respawnDelay > 0f && stateTime >= corpseTime + respawnDelay) Respawn();
        }

        private void Respawn()
        {
            health.Restore();
            foreach (Collider item in colliders) if (item != null) item.enabled = true;
            agent.enabled = true;
            agent.Warp(home);
            transform.rotation = homeRotation;
            rig.ResetPose();
            Enter(State.Idle);
        }

        // ---------- Getting hit ----------

        private void OnDamaged(Health _, float amount, GameObject source)
        {
            if (state == State.Dead) return;
            Vector3 away = source != null ? transform.position - source.transform.position : -transform.forward;
            away.y = 0f;
            away = away.sqrMagnitude > 0.001f ? away.normalized : -transform.forward;

            bool heavy = amount >= staggerThreshold;
            rig.Flinch(heavy);
            StartKnockback(away, heavy ? knockback * Mathf.Clamp(amount / 20f, 0.6f, 2f) : 0.12f);

            if (heavy && !health.IsDead) { nextGuard = Time.time + guardCooldown; Enter(State.Stagger); }
            else if (state == State.Guard) { lastSeen = Time.time; nextGuard = Time.time + guardCooldown; Enter(State.Chase); } // a melee hit pops it out
            else if (state == State.Idle) { lastSeen = Time.time; rig.Alert(); Enter(State.Chase); }
        }

        private void OnDied(Health _)
        {
            Enter(State.Dead);
            rig.PlayDeath();
            foreach (Collider item in colliders) if (item != null) item.enabled = false;
            if (agent.isOnNavMesh) agent.isStopped = true;
            agent.enabled = false;
        }

        private void StartKnockback(Vector3 direction, float distance, float duration = 0.22f)
        {
            direction.y = 0f;
            knockDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : -transform.forward;
            knockRemaining = distance;
            knockSpeed = distance / Mathf.Max(duration, 0.01f);
        }

        private void UpdateKnockback(float dt)
        {
            if (knockRemaining <= 0f) return;
            float step = Mathf.Min(knockRemaining, knockSpeed * dt);
            agent.Move(knockDirection * step);
            knockRemaining -= step;
            knockSpeed = Mathf.Max(knockSpeed * (1f - 4f * dt), 1f); // slides to a stop
        }

        // Pushes the player back a little when the claw connects.
        private void UpdateShove(float dt)
        {
            if (shoveRemaining <= 0f || targetBody == null || !targetBody.enabled) return;
            float step = Mathf.Min(shoveRemaining, 6f * dt);
            targetBody.Move(shoveDirection * step);
            shoveRemaining -= step;
        }

        // ---------- Helpers ----------

        private bool TargetAlive => target != null && (targetHealth == null || !targetHealth.IsDead);

        private float FlatDistance(Vector3 point)
        {
            Vector3 offset = point - transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }

        /// <summary>True if the target is within range and nothing but crabs or the player is in the way.</summary>
        private bool CanSee(float range)
        {
            if (target == null || FlatDistance(target.position) > range) return false;
            Vector3 eye = transform.position + Vector3.up * 0.6f;
            Vector3 look = target.position + Vector3.up * 1.4f;
            Vector3 toLook = look - eye;
            float distance = toLook.magnitude;
            int count = Physics.RaycastNonAlloc(eye, toLook / distance, sightHits, distance, sightMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform hit = sightHits[i].collider.transform;
                if (hit.IsChildOf(transform) || hit.IsChildOf(target)) continue;
                if (hit.GetComponentInParent<HermitCrab>() != null) continue; // other crabs don't block sight
                return false;
            }
            return true;
        }

        private void TurnToward(Vector3 point, float maxDegrees)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), maxDegrees);
        }
    }
}
