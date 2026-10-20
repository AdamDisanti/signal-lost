using SignalLost.Combat;
using SignalLost.Player;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.Enemies
{
    /// <summary>
    /// Ranged enemy: a goofy alien frog with a tin can strapped to its back that it uses as a goo mortar.
    /// It moves only by hopping and keeps its distance: hops in when the player is far or out of sight, and hops
    /// away when they get close. To attack it swells its throat while the can rattles and glows (the telegraph),
    /// then lobs a goo blob in a high arc at where the player stands; a ring on the ground marks the landing spot.
    /// Hitting the can while it is loading makes it backfire: goo bursts over the frog, it takes heavy damage and
    /// is stunned. All numbers are placeholders to tune.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(MortarFrogRig))]
    public sealed class MortarFrog : MonoBehaviour
    {
        private enum State { Idle, Engage, Load, Stunned, Dead }

        [Header("Target")]
        [Tooltip("Who to attack. Empty finds the player automatically.")]
        [SerializeField] private Transform target;
        [SerializeField] private LayerMask sightMask = ~0;

        [Header("Senses")]
        [SerializeField, Min(1f)] private float detectRange = 18f;
        [SerializeField, Min(1f)] private float loseRange = 26f;
        [SerializeField, Min(0f)] private float forgetTime = 4f;

        [Header("Hopping")]
        [Tooltip("Hops away when the player is closer than this.")]
        [SerializeField, Min(0f)] private float retreatDistance = 6f;
        [Tooltip("Hops toward the player when they are further than this.")]
        [SerializeField, Min(1f)] private float maxAttackRange = 15f;
        [SerializeField, Min(0.5f)] private float hopDistance = 5f;
        [Tooltip("Seconds in the air per hop.")]
        [SerializeField, Min(0.1f)] private float hopTime = 0.7f;
        [Tooltip("Seconds of crouching before each hop.")]
        [SerializeField, Min(0f)] private float hopCrouch = 0.18f;
        [Tooltip("Pause on the ground between hops.")]
        [SerializeField, Min(0f)] private float hopRest = 0.25f;
        [Tooltip("Now and then it side-hops even when happy with its range, so it is not a sitting target.")]
        [SerializeField, Min(0f)] private float sideHopInterval = 3f;
        [SerializeField, Min(10f)] private float turnSpeed = 300f;

        [Header("Mortar")]
        [Tooltip("Seconds the throat swells and the can rattles before it fires. This is the player's warning.")]
        [SerializeField, Min(0.1f)] private float loadTime = 1f;
        [Tooltip("Seconds between shots.")]
        [SerializeField, Min(0f)] private float attackCooldown = 2.2f;
        [Tooltip("Goo flight time per metre of distance; clamped between the min and max below.")]
        [SerializeField, Min(0.01f)] private float flightTimePerMetre = 0.1f;
        [SerializeField, Min(0.2f)] private float minFlightTime = 0.8f;
        [SerializeField, Min(0.2f)] private float maxFlightTime = 1.4f;
        [SerializeField, Min(0f)] private float gooDamage = 12f;
        [SerializeField, Min(0.1f)] private float splashRadius = 1.5f;
        [SerializeField, Min(0.05f)] private float gooRadius = 0.18f;
        [Tooltip("Random scatter, in metres, on where the goo lands.")]
        [SerializeField, Min(0f)] private float aimScatter = 0.4f;

        [Header("Tin can (weak spot)")]
        [Tooltip("Health on the tin can's own collider. Hits to it while loading make the can backfire.")]
        [SerializeField] private Health canHealth;
        [SerializeField, Min(0f)] private float backfireDamage = 30f;
        [SerializeField, Min(0f)] private float stunTime = 2f;

        [Header("Getting hit")]
        [SerializeField, Min(0f)] private float knockback = 0.6f;

        [Header("Death")]
        [SerializeField, Min(0f)] private float corpseTime = 3.5f;
        [Tooltip("Test scenes only: seconds after vanishing before it comes back. 0 means it stays dead.")]
        [SerializeField, Min(0f)] private float respawnDelay;

        private NavMeshAgent agent;
        private Health health;
        private MortarFrogRig rig;
        private Collider[] colliders;
        private Health targetHealth;
        private readonly RaycastHit[] sightHits = new RaycastHit[16];

        private State state = State.Idle;
        private float stateTime, lastSeen = float.NegativeInfinity, nextAttack, nextSideHop;
        private Vector3 home;
        private Quaternion homeRotation;
        private bool started;
        private int lastBodyHitFrame = -1;
        private float pendingCanDamage;
        private GameObject pendingCanSource;
        private Knockback pendingCanKnockback;
        private Vector3 pendingCanDirection;

        // Current hop: crouch, then fly from hopFrom to hopTo.
        private bool hopping;
        private float hopStart, nextHop;
        private Vector3 hopFrom, hopTo;

        public bool IsDead => state == State.Dead;
        public bool IsLoading => state == State.Load;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            rig = GetComponent<MortarFrogRig>();
            colliders = GetComponentsInChildren<Collider>();
            home = transform.position;
            homeRotation = transform.rotation;
        }

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            if (canHealth != null) canHealth.Damaged += OnCanHit;
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
            if (canHealth != null) canHealth.Damaged -= OnCanHit;
        }

        // Start, not Awake: the NavMesh is built at runtime in Awake by NavMeshBaker.
        private void Start()
        {
            if (target == null)
            {
                var player = FindAnyObjectByType<FirstPersonController>();
                if (player != null) target = player.transform;
            }
            if (target != null) targetHealth = target.GetComponentInParent<Health>();

            agent.enabled = true;
            agent.updateRotation = false; // it turns itself; it only moves by hopping
            agent.isStopped = true;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                home = hit.position;
            }
            else
            {
                Debug.LogWarning("Mortar frog is not near a NavMesh; it will stay put. Is there a NavMeshBaker in the scene?", this);
            }
            started = true;
            Enter(State.Idle);
        }

        private void Update()
        {
            if (!started || KnockbackReceiver.IsMoving(gameObject)) return;
            stateTime += Time.deltaTime;

            if (state == State.Dead)
            {
                if (stateTime >= corpseTime && stateTime - Time.deltaTime < corpseTime) rig.SetVisible(false);
                if (respawnDelay > 0f && stateTime >= corpseTime + respawnDelay) Respawn();
                return;
            }
            if (!agent.isOnNavMesh) return;

            UpdateHop();
            switch (state)
            {
                case State.Idle: UpdateIdle(); break;
                case State.Engage: UpdateEngage(); break;
                case State.Load: UpdateLoad(); break;
                case State.Stunned: UpdateStunned(); break;
            }
        }

        private void Enter(State next)
        {
            state = next;
            stateTime = 0f;
            rig.Load = 0f;
            rig.Gooed = next == State.Stunned;
            if (next == State.Engage) nextSideHop = Time.time + Random.Range(1f, sideHopInterval);
        }

        // ---------- States ----------

        private void UpdateIdle()
        {
            // Someone right next to it is noticed even without line of sight (standing on it, behind it).
            if (TargetAlive && FlatDistance(target.position) < retreatDistance)
            {
                lastSeen = Time.time;
                nextAttack = Time.time + 0.6f;
                Enter(State.Engage);
                nextHop = 0f; // get away now
                return;
            }
            if (TargetAlive && CanSee(detectRange))
            {
                lastSeen = Time.time;
                nextAttack = Time.time + 0.6f; // a beat before the first shot
                Enter(State.Engage);
                return;
            }
            // Idle: an occasional lazy hop near home.
            if (!hopping && Time.time >= nextHop && Random.value < 0.004f)
                TryHop(Random.insideUnitSphere * hopDistance * 0.5f, false);
        }

        private void UpdateEngage()
        {
            if (!TargetAlive)
            {
                Enter(State.Idle);
                return;
            }
            float distance = FlatDistance(target.position);
            bool seen = distance < retreatDistance || CanSee(loseRange); // up close it can always sense you
            if (seen) lastSeen = Time.time;
            if (distance > loseRange || Time.time - lastSeen > forgetTime)
            {
                Enter(State.Idle);
                return;
            }

            if (!hopping) TurnToward(target.position, turnSpeed * Time.deltaTime);
            if (hopping || Time.time < nextHop) return;

            Vector3 toPlayer = target.position - transform.position;
            toPlayer.y = 0f;
            if (distance < retreatDistance)
            {
                // Too close: always hop away (only loading a shot or being stunned stops it).
                if (!EscapeHop(toPlayer)) nextHop = Time.time + 0.15f;
            }
            else if (distance > maxAttackRange || !seen)
            {
                // Too far, or out of sight: hop toward the player along the NavMesh path.
                TryHop(PathDirection(target.position) * hopDistance, false);
            }
            else if (Time.time >= nextAttack && seen)
            {
                Enter(State.Load);
            }
            else if (Time.time >= nextSideHop)
            {
                Vector3 side = Vector3.Cross(Vector3.up, toPlayer.normalized) * (Random.value < 0.5f ? -1f : 1f);
                TryHop(side * hopDistance * 0.7f, false);
                nextSideHop = Time.time + Random.Range(sideHopInterval * 0.6f, sideHopInterval * 1.4f);
            }
        }

        private void UpdateLoad()
        {
            if (!TargetAlive)
            {
                Enter(State.Idle);
                return;
            }
            TurnToward(target.position, turnSpeed * Time.deltaTime);
            rig.Load = Mathf.Clamp01(stateTime / loadTime);
            if (stateTime < loadTime) return;

            Fire();
            nextAttack = Time.time + attackCooldown;
            nextHop = Time.time + 0.35f;
            Enter(State.Engage);
        }

        private void Fire()
        {
            Vector3 landing = target.position;
            Vector2 scatter = Random.insideUnitCircle * aimScatter;
            landing += new Vector3(scatter.x, 0f, scatter.y);
            // Land on the ground under that point (the player's origin is at their feet already).
            if (Physics.Raycast(landing + Vector3.up * 1f, Vector3.down, out RaycastHit ground, 3f, sightMask, QueryTriggerInteraction.Ignore)
                && !ground.collider.transform.IsChildOf(target))
                landing = ground.point;

            Vector3 from = rig.CanMouth.position;
            float flight = Mathf.Clamp(FlatDistance(landing) * flightTimePerMetre, minFlightTime, maxFlightTime);
            GooBlob.Launch(rig.GooMaterial, from, landing, flight, gooRadius, gooDamage, splashRadius, sightMask, transform);
            rig.Fire();
        }

        private void UpdateStunned()
        {
            if (stateTime < stunTime) return;
            nextAttack = Time.time + attackCooldown * 0.5f;
            nextHop = Time.time + 0.4f; // let it finish scrambling to its feet before hopping
            Enter(TargetAlive ? State.Engage : State.Idle);
        }

        // ---------- Hopping ----------

        private static readonly float[] EscapeAngles = { 0f, 35f, -35f, 70f, -70f, 100f, -100f, 130f, -130f };

        /// <summary>
        /// Hops away from the player. Tries straight away first, then fans out to either side, and takes the first
        /// direction with a full hop of room (or the roomiest one) so walls and edges never pin it in place.
        /// Works even when the player is standing right on top of it.
        /// </summary>
        private bool EscapeHop(Vector3 toPlayer)
        {
            toPlayer.y = 0f;
            Vector3 away = toPlayer.sqrMagnitude > 0.09f ? -toPlayer.normalized : -transform.forward;
            float flip = Random.value < 0.5f ? 1f : -1f;
            Vector3 best = Vector3.zero;
            float bestRoom = 0f;
            foreach (float angle in EscapeAngles)
            {
                Vector3 direction = Quaternion.Euler(0f, angle * flip + Random.Range(-10f, 10f), 0f) * away;
                float room = HopRoom(direction * hopDistance);
                if (room > bestRoom)
                {
                    best = direction;
                    bestRoom = room;
                }
                if (room >= hopDistance * 0.8f) break;
            }
            return bestRoom >= 0.5f && TryHop(best * bestRoom, true);
        }

        /// <summary>How far a hop along <paramref name="offset"/> can go before a wall or the NavMesh edge.</summary>
        private float HopRoom(Vector3 offset)
        {
            Vector3 from = transform.position;
            if (NavMesh.Raycast(from, from + offset, out NavMeshHit blocked, NavMesh.AllAreas))
                return Vector3.Distance(from, blocked.position);
            return offset.magnitude;
        }

        /// <summary>Starts a hop by <paramref name="offset"/>, trimmed to stay on the NavMesh and clear of walls.</summary>
        private bool TryHop(Vector3 offset, bool urgent)
        {
            offset.y = 0f;
            if (offset.sqrMagnitude < 0.01f) return false;
            Vector3 from = transform.position;
            Vector3 to = from + offset;
            // Stop short of walls and edges.
            if (NavMesh.Raycast(from, to, out NavMeshHit blocked, NavMesh.AllAreas)) to = blocked.position;
            if (!NavMesh.SamplePosition(to, out NavMeshHit landing, 1f, NavMesh.AllAreas)) return false;
            to = landing.position;
            if ((to - from).sqrMagnitude < 0.25f)
            {
                // Cornered: try the opposite side instead next time.
                nextHop = Time.time + (urgent ? 0.1f : 0.4f);
                return false;
            }
            hopping = true;
            hopStart = Time.time;
            hopFrom = from;
            hopTo = to;
            return true;
        }

        private void UpdateHop()
        {
            if (!hopping)
            {
                rig.Hop = -1f;
                rig.Crouch = Mathf.MoveTowards(rig.Crouch, 0f, Time.deltaTime * 8f);
                return;
            }
            float t = Time.time - hopStart;
            rig.Crouch = hopCrouch > 0f ? Mathf.Clamp01(t / hopCrouch) : 0f;
            if (t < hopCrouch)
            {
                rig.Hop = -1f; // crouching
                TurnToward(hopTo, turnSpeed * 2f * Time.deltaTime);
                return;
            }
            float p = Mathf.Clamp01((t - hopCrouch) / hopTime);
            rig.Hop = p;
            rig.Crouch = 0f;
            Vector3 wanted = Vector3.Lerp(hopFrom, hopTo, p);
            agent.Move(wanted - transform.position);
            if (p >= 1f)
            {
                hopping = false;
                rig.Hop = -1f;
                nextHop = Time.time + hopRest;
            }
        }

        /// <summary>Direction of the first leg of the NavMesh path toward a point.</summary>
        private Vector3 PathDirection(Vector3 point)
        {
            var path = new NavMeshPath();
            Vector3 toward = point - transform.position;
            if (agent.CalculatePath(point, path) && path.corners.Length > 1)
                toward = path.corners[1] - transform.position;
            toward.y = 0f;
            return toward.sqrMagnitude > 0.001f ? toward.normalized : transform.forward;
        }

        // ---------- Getting hit ----------

        /// <summary>The tin can was hit. While loading, it backfires.</summary>
        private void OnCanHit(Health can, float amount, GameObject source)
        {
            can.Restore(); // the can itself never breaks
            if (state == State.Dead || health.IsDead) return;
            rig.Clang(source != null ? source.transform.position - transform.position : transform.forward);
            if (state != State.Load)
            {
                // Not loading: the can is just part of the frog's back, so the frog takes the hit as normal.
                // Applied in LateUpdate, and dropped if the same swing also hit the body, so it never counts twice.
                if (amount > pendingCanDamage)
                {
                    pendingCanDamage = amount; pendingCanSource = source;
                    pendingCanKnockback = can.HitKnockback; pendingCanDirection = can.HitDirection;
                }
                return;
            }
            rig.Backfire();
            Enter(State.Stunned);
            health.TakeDamage(backfireDamage, source, can.HitKnockback, can.HitDirection);
        }

        private void LateUpdate()
        {
            if (pendingCanDamage <= 0f) return;
            float amount = pendingCanDamage;
            GameObject source = pendingCanSource;
            pendingCanDamage = 0f;
            pendingCanSource = null;
            if (lastBodyHitFrame == Time.frameCount || state == State.Dead || health.IsDead) return;
            health.TakeDamage(amount, source, pendingCanKnockback, pendingCanDirection);
        }

        private void OnDamaged(Health _, float amount, GameObject source)
        {
            lastBodyHitFrame = Time.frameCount;
            if (state == State.Dead) return;
            Vector3 away = source != null ? transform.position - source.transform.position : -transform.forward;
            away.y = 0f;
            rig.Flinch(away);
            if (_.HitKnockback.mode != KnockbackMode.None)
            {
                hopping = false;
                rig.Hop = -1f;
            }
            else if (source != null && !hopping && agent.isOnNavMesh && away.sqrMagnitude > 0.001f)
                agent.Move(away.normalized * knockback * Mathf.Clamp01(amount / 20f));
            if (state == State.Idle) { lastSeen = Time.time; Enter(State.Engage); }
            // Hit up close: get out of there soon.
            if (state == State.Engage) nextHop = Mathf.Min(nextHop, Time.time + 0.15f);
        }

        private void OnDied(Health _)
        {
            Enter(State.Dead);
            hopping = false;
            rig.PlayDeath();
            foreach (Collider item in colliders) if (item != null) item.enabled = false;
            agent.enabled = false;
        }

        private void Respawn()
        {
            health.Restore();
            if (canHealth != null) canHealth.Restore();
            foreach (Collider item in colliders) if (item != null) item.enabled = true;
            agent.enabled = true;
            agent.Warp(home);
            agent.isStopped = true;
            agent.updateRotation = false;
            transform.rotation = homeRotation;
            rig.ResetPose();
            Enter(State.Idle);
        }

        // ---------- Helpers ----------

        private bool TargetAlive => target != null && (targetHealth == null || !targetHealth.IsDead);

        private float FlatDistance(Vector3 point)
        {
            Vector3 offset = point - transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }

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
                if (hit.GetComponentInParent<MortarFrog>() != null || hit.GetComponentInParent<HermitCrab>() != null) continue;
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
