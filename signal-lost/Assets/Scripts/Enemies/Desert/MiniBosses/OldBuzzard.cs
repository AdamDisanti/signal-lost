using SignalLost.Combat;
using SignalLost.Effects;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// Mini-boss: a huge, ragged vulture with panels of the ship's wings riveted over its own scrawny wings.
    ///
    /// Phase 1 (airborne, out of melee reach):
    /// - Circles above the player.
    /// - Dive-bomb: its shadow appears on the sand under the player and grows (it tracks the player for the first half,
    ///   then locks), then it swoops down onto the spot. It sits on the ground for a moment afterwards: a melee window.
    /// - Bombing run: flies a straight line over the player dropping hull scrap; rings mark where each piece lands.
    /// - A heavy hit (a charged shot) on a wing panel makes it wobble, interrupting the attack.
    ///
    /// Phase 2 (one wing shot off): the wing that took more damage tears away and it crashes down. Grounded, it stomps
    /// around lopsided: pecks, hop-stomps, a sand fling (it reaches its wing down into the sand and flings a spray of it
    /// at the player, shoving them and throwing sand in their eyes), and every
    /// so often a failed attempt to take off that leaves it dazed. Its remaining wing shields its front: bolts from the
    /// front glance off, so close in with melee or shoot it from behind.
    ///
    /// The torn-off wing is the ship part it drops when defeated. Being spotted by a Dish Meerkat makes it dive more
    /// often. All numbers are placeholders.
    /// </summary>
    public sealed class OldBuzzard : DesertMiniBoss, IBoltReflector
    {
        private enum State
        {
            Soar, DiveWindup, Dive, Landed, Climb, BombWindup, BombRun, Wobble,  // phase 1, airborne
            Fall,                                                                 // phase change
            Walk, PeckWindup, Peck, StompWindup, Stomp, GustWindup, Gust, Takeoff, Dazed, Recover, // phase 2
        }

        [Header("Flight")]
        [SerializeField, Min(2f)] private float soarHeight = 8f;
        [SerializeField, Min(2f)] private float patrolHeight = 12f;
        [SerializeField, Min(3f)] private float circleRadius = 11f;
        [SerializeField, Min(1f)] private float flySpeed = 10f;

        [Header("Dive-bomb")]
        [Tooltip("Its shadow grows under the player before it swoops: the player's warning.")]
        [SerializeField, Min(0.1f)] private float diveWindup = 1.1f;
        [SerializeField, Min(0.1f)] private float alertedDiveWindup = 0.8f;
        [Tooltip("Part of the wind-up the target keeps following the player; after that it is locked.")]
        [SerializeField, Range(0f, 1f)] private float diveTracking = 0.5f;
        [SerializeField, Min(0.1f)] private float diveTime = 0.55f;
        [SerializeField, Min(0f)] private float diveDamage = 22f;
        [SerializeField, Min(0.5f)] private float diveRadius = 2.4f;
        [Tooltip("Seconds it sits on the ground after a dive: the melee window.")]
        [SerializeField, Min(0f)] private float landedTime = 1.1f;
        [SerializeField, Min(0.1f)] private float climbTime = 1f;
        [SerializeField, Min(0f)] private float diveCooldown = 5f;

        [Header("Bombing run")]
        [SerializeField, Min(0.1f)] private float bombWindup = 0.6f;
        [SerializeField, Min(1f)] private float bombRunSpeed = 12f;
        [SerializeField, Min(2f)] private float bombHeight = 7f;
        [SerializeField, Min(0.05f)] private float bombInterval = 0.3f;
        [SerializeField, Min(4f)] private float bombRunLength = 26f;
        [Tooltip("Drops scrap while within this distance of where the player stood when the run started.")]
        [SerializeField, Min(1f)] private float bombSpread = 9f;
        [SerializeField, Min(0f)] private float bombDamage = 12f;
        [SerializeField, Min(0.3f)] private float bombRadius = 1.7f;
        [SerializeField, Min(0f)] private float bombCooldown = 9f;

        [Header("Wing hits")]
        [Tooltip("Hits on the wing panels' own colliders. A heavy hit makes it wobble; the more damaged wing tears off in phase 2.")]
        [SerializeField] private Health[] wingHitboxes = new Health[2];
        [SerializeField, Min(0f)] private float wobbleTime = 1f;

        [Header("Grounded (phase 2)")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 2.6f;
        [SerializeField, Min(0f)] private float attackCooldown = 1.3f;
        [SerializeField, Min(0.5f)] private float peckRange = 2.4f;
        [SerializeField, Min(0.1f)] private float peckWindup = 0.55f;
        [SerializeField, Min(0.5f)] private float peckReach = 2.8f;
        [SerializeField, Range(10f, 180f)] private float peckArc = 70f;
        [SerializeField, Min(0f)] private float peckDamage = 16f;
        [SerializeField, Min(1f)] private float stompRange = 7f;
        [SerializeField, Min(0.1f)] private float stompWindup = 0.7f;
        [SerializeField, Min(0.1f)] private float stompTime = 0.55f;
        [SerializeField, Min(0.5f)] private float stompRadius = 2.4f;
        [SerializeField, Min(0f)] private float stompDamage = 20f;
        [SerializeField, Min(1f)] private float gustRange = 9f;
        [Tooltip("Raising its wing before the gust: the player's warning.")]
        [SerializeField, Min(0.1f)] private float gustWindup = 1f;
        [SerializeField, Range(5f, 90f)] private float gustHalfAngle = 35f;
        [SerializeField, Min(0f)] private float gustDamage = 8f;
        [SerializeField, Min(0f)] private float gustPush = 4f;
        [Tooltip("Seconds of sand in the player's eyes after being caught by the gust.")]
        [SerializeField, Min(0f)] private float sandBlindTime = 2.5f;
        [Tooltip("Seconds between failed take-off attempts (random between the two).")]
        [SerializeField] private Vector2 takeoffInterval = new Vector2(9f, 13f);
        [SerializeField, Min(0.1f)] private float takeoffTime = 1.2f;
        [Tooltip("Dazed after a failed take-off: the player's opening.")]
        [SerializeField, Min(0f)] private float dazedTime = 1.8f;
        [Tooltip("Bolts hitting it from within this angle of its front glance off its remaining wing.")]
        [SerializeField, Range(0f, 180f)] private float shieldAngle = 70f;
        [SerializeField, Range(0f, 1f)] private float deflectDamageMultiplier = 0.3f;

        [Header("Rig")]
        [Tooltip("Wing pivots, left then right.")]
        [SerializeField] private Transform[] wings = new Transform[2];
        [Tooltip("Elbow pivots (the outer half of each wing), left then right. Optional: without them the wings bend at the shoulder only.")]
        [SerializeField] private Transform[] wingTips = new Transform[0];
        [SerializeField] private Transform neck;
        [Tooltip("Turns to watch the player. Optional: found under the neck by name if empty.")]
        [SerializeField] private Transform head;
        [Tooltip("Tail-feather pivot. Optional.")]
        [SerializeField] private Transform tail;
        [SerializeField] private Transform[] legs = new Transform[0];
        [SerializeField] private Material shadowMaterial;
        [SerializeField] private Material warningMaterial;
        [SerializeField] private Material scrapMaterial;
        [SerializeField] private Material blastMaterial;
        [SerializeField] private Material dustMaterial;
        [SerializeField] private Material sparkMaterial;

        private State state = State.Soar;
        private float stateTime, orbitAngle, nextDive, nextBombRun, nextAttack, nextTakeoff, nextBomb, nextRepath, verticalSpeed;
        private bool flying = true, struck;
        private readonly float[] wingDamage = new float[2];
        private Vector3 diveFrom, diveTarget, runStart, runDirection, runCentre, hopFrom, hopTo, wobbleFrom;
        private GroundMarker marker;

        private int tornSide = -1;
        private Transform tornWing, tornParent;
        private Vector3 tornPosition, tornScale, debrisVelocity;
        private Quaternion tornRotation;
        private bool debrisFalling;
        private Quaternion[] wingRest = new Quaternion[2], legRest = new Quaternion[0];
        private Quaternion neckRest = Quaternion.identity;
        private bool deathLanded;
        private float deathLandTime;

        private float GroundY => Home.y;

        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < wings.Length && i < 2; i++) wingRest[i] = wings[i] != null ? wings[i].localRotation : Quaternion.identity;
            for (int i = 0; i < wingTips.Length && i < 2; i++) tipRest[i] = wingTips[i] != null ? wingTips[i].localRotation : Quaternion.identity;
            legRest = new Quaternion[legs.Length];
            legPose = new Quaternion[legs.Length];
            for (int i = 0; i < legs.Length; i++)
            {
                legRest[i] = legs[i] != null ? legs[i].localRotation : Quaternion.identity;
                legPose[i] = Quaternion.identity;
            }
            if (neck != null) neckRest = neck.localRotation;
            if (head == null && neck != null) head = neck.Find("Head");
            if (head != null) headRest = headLook = head.localRotation;
            if (tail != null) tailRest = tail.localRotation;
            for (int i = 0; i < 2; i++) wingPose[i] = tipPose[i] = Quaternion.identity;
            lastYaw = transform.eulerAngles.y;
        }

        protected override void OnBossStarted()
        {
            for (int i = 0; i < wingHitboxes.Length; i++) RelayPart(wingHitboxes[i], i);
            TakeOff(patrolHeight);
            orbitAngle = Random.value * Mathf.PI * 2f;
        }

        private void TakeOff(float height)
        {
            flying = true;
            if (Agent != null) Agent.enabled = false;
            Vector3 p = transform.position;
            transform.position = new Vector3(p.x, GroundY + height, p.z);
        }

        private void Land()
        {
            flying = false;
            Vector3 p = transform.position;
            if (NavMesh.SamplePosition(new Vector3(p.x, GroundY, p.z), out NavMeshHit hit, 6f, NavMesh.AllAreas)) p = hit.position;
            else p.y = GroundY;
            transform.position = p;
            Agent.enabled = true;
            Agent.Warp(p);
            Halt();
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        }

        private void Enter(State next)
        {
            state = next;
            stateTime = 0f;
            struck = false;
            if (!flying && next != State.Walk) Halt();
        }

        // ---------- Patrol: circling high over its home ----------

        protected override void UpdatePatrol(float dt)
        {
            if (!flying) return;
            orbitAngle += 0.35f * dt;
            Vector3 want = Home + new Vector3(Mathf.Cos(orbitAngle), 0f, Mathf.Sin(orbitAngle)) * 8f;
            want.y = GroundY + patrolHeight;
            FlyToward(want, flySpeed * 0.6f, dt);
        }

        /// <summary>Moves toward a point in the air, facing where it is going.</summary>
        private void FlyToward(Vector3 point, float speed, float dt)
        {
            Vector3 step = Vector3.MoveTowards(transform.position, point, speed * dt) - transform.position;
            transform.position += step;
            Vector3 flat = new Vector3(step.x, 0f, step.z);
            if (flat.sqrMagnitude > 0.00001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), 240f * dt);
        }

        /// <summary>Keeps a point inside the arena.</summary>
        private Vector3 ClampToArena(Vector3 point)
        {
            Vector3 offset = point - Home;
            offset.y = 0f;
            if (offset.magnitude > ArenaRadius) offset = offset.normalized * ArenaRadius;
            return new Vector3(Home.x + offset.x, point.y, Home.z + offset.z);
        }

        // ---------- Combat ----------

        protected override void OnEngaged()
        {
            nextDive = Time.time + 2.5f;
            nextBombRun = Time.time + 6f;
            nextAttack = Time.time + 0.5f;
            if (flying) Enter(State.Soar);
            else Enter(State.Walk);
        }

        protected override void TickCombat(float dt)
        {
            stateTime += dt;
            UpdateShove(dt);
            if (!TargetAlive) return;
            switch (state)
            {
                case State.Soar: UpdateSoar(dt); break;
                case State.DiveWindup: UpdateDiveWindup(dt); break;
                case State.Dive: UpdateDive(); break;
                case State.Landed: if (stateTime >= landedTime) Enter(State.Climb); break;
                case State.Climb: UpdateClimb(dt); break;
                case State.BombWindup: UpdateBombWindup(dt); break;
                case State.BombRun: UpdateBombRun(dt); break;
                case State.Wobble: UpdateWobble(); break;
                case State.Walk: UpdateWalk(dt); break;
                case State.PeckWindup: Windup(peckWindup, 0.6f, State.Peck, dt); break;
                case State.Peck: UpdatePeck(); break;
                case State.StompWindup: UpdateStompWindup(dt); break;
                case State.Stomp: UpdateStomp(); break;
                case State.GustWindup: Windup(gustWindup, 0.7f, State.Gust, dt); break;
                case State.Gust: UpdateGust(); break;
                case State.Takeoff: UpdateTakeoff(); break;
                case State.Dazed: if (stateTime >= dazedTime) Enter(State.Walk); break;
                case State.Recover:
                    if (stateTime >= 0.6f)
                    {
                        nextAttack = Time.time + attackCooldown;
                        Enter(State.Walk);
                    }
                    break;
            }
        }

        private void UpdateSoar(float dt)
        {
            // Circle the player (kept inside the arena).
            orbitAngle += flySpeed / circleRadius * dt;
            Vector3 centre = ClampToArena(Target.position);
            Vector3 want = centre + new Vector3(Mathf.Cos(orbitAngle), 0f, Mathf.Sin(orbitAngle)) * circleRadius;
            want = ClampToArena(want);
            want.y = GroundY + soarHeight;
            FlyToward(want, flySpeed * 1.3f, dt);

            if (stateTime < 0.8f) return;
            if (Time.time >= nextDive && CanSee(ArenaRadius * 2f))
            {
                diveTarget = GroundBelow(Target.position);
                marker = GroundMarker.Spawn(warningMaterial, shadowMaterial, diveTarget, diveRadius);
                Enter(State.DiveWindup);
            }
            else if (Time.time >= nextBombRun) Enter(State.BombWindup);
        }

        private float DiveWindupTime => Alerted ? alertedDiveWindup : diveWindup;

        private void UpdateDiveWindup(float dt)
        {
            float windup = DiveWindupTime;
            if (stateTime < windup * diveTracking)
            {
                diveTarget = GroundBelow(Target.position);
                if (marker != null) marker.MoveTo(diveTarget);
            }
            if (marker != null) marker.Progress = stateTime / windup;
            // Hang in the air, turning to face the spot.
            Vector3 toTarget = diveTarget - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toTarget), 300f * dt);
            if (stateTime < windup) return;
            diveFrom = transform.position;
            Enter(State.Dive);
        }

        private void UpdateDive()
        {
            float t = Mathf.Clamp01(stateTime / diveTime);
            Vector3 end = diveTarget + Vector3.up * 0.1f;
            transform.position = Vector3.Lerp(diveFrom, end, t * t);
            if (marker != null) marker.Progress = 1f;
            if (t < 1f) return;

            ClearMarker();
            Dust(diveTarget, diveRadius);
            if (Flat(Target.position - diveTarget) <= diveRadius && Target.position.y - diveTarget.y < 2f && HurtPlayer(diveDamage))
                ShovePlayer(Target.position - diveTarget, 1.6f);
            nextDive = Time.time + (Alerted ? diveCooldown * 0.5f : diveCooldown);
            Enter(State.Landed);
        }

        private void UpdateClimb(float dt)
        {
            Vector3 want = transform.position;
            want.y = GroundY + soarHeight;
            float rise = (soarHeight / climbTime) * dt;
            transform.position = Vector3.MoveTowards(transform.position, want, rise);
            if (stateTime >= climbTime) Enter(State.Soar);
        }

        private void UpdateBombWindup(float dt)
        {
            if (runDirection == Vector3.zero)
            {
                // Line the run up across the player, starting from the side it's already on.
                runCentre = Target.position;
                Vector3 fromPlayer = transform.position - runCentre;
                fromPlayer.y = 0f;
                if (fromPlayer.sqrMagnitude < 1f) fromPlayer = -transform.forward;
                runDirection = -fromPlayer.normalized;
                runStart = ClampToArena(runCentre - runDirection * (bombRunLength * 0.5f));
                runStart.y = GroundY + bombHeight;
            }
            FlyToward(runStart, flySpeed * 2f, dt);
            if (stateTime < bombWindup) return;
            nextBomb = 0f;
            Enter(State.BombRun);
        }

        private void UpdateBombRun(float dt)
        {
            Vector3 p = transform.position + runDirection * bombRunSpeed * dt;
            p.y = Mathf.MoveTowards(p.y, GroundY + bombHeight, 4f * dt);
            transform.position = p;
            transform.rotation = Quaternion.LookRotation(runDirection);

            if (Flat(transform.position - runCentre) <= bombSpread && Time.time >= nextBomb)
            {
                nextBomb = Time.time + bombInterval;
                ScrapBomb.Drop(scrapMaterial, warningMaterial, blastMaterial, transform.position + Vector3.down * 0.6f,
                    GroundBelow(transform.position), bombRadius, bombDamage, gameObject, transform, sightMask);
            }
            if (stateTime * bombRunSpeed < bombRunLength && Flat(transform.position - Home) <= ArenaRadius + 6f) return;
            runDirection = Vector3.zero;
            nextBombRun = Time.time + bombCooldown;
            Enter(State.Soar);
        }

        private void UpdateWobble()
        {
            // Knocked off balance: drops a couple of metres, then climbs back.
            float t = Mathf.Clamp01(stateTime / Mathf.Max(0.01f, wobbleTime));
            Vector3 p = wobbleFrom;
            p.y = Mathf.Max(GroundY + 3f, wobbleFrom.y - 2.5f * Mathf.Sin(t * Mathf.PI * 0.5f));
            transform.position = p;
            if (t >= 1f) Enter(State.Climb);
        }

        // ---------- Phase 2: grounded ----------

        private void Windup(float time, float trackFor, State next, float dt)
        {
            if (stateTime < time * trackFor) TurnToward(Target.position, 260f * dt);
            if (stateTime >= time) Enter(next);
        }

        private void UpdateWalk(float dt)
        {
            float distance = FlatDistance(Target.position);
            if (CanSee(ArenaRadius * 2f)) LastSeen = Time.time;

            if (Time.time >= nextTakeoff && distance > 4f)
            {
                Enter(State.Takeoff);
                return;
            }
            if (Time.time >= nextAttack)
            {
                float angle = AngleToTarget();
                if (distance <= peckRange && angle < 40f) { Enter(State.PeckWindup); return; }
                if (distance > 3f && distance <= gustRange && angle < 35f && Random.value < 0.45f) { Enter(State.GustWindup); return; }
                if (distance > 3f && distance <= stompRange) { Enter(State.StompWindup); return; }
            }
            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + 0.3f;
                Chase(Target.position, walkSpeed);
            }
        }

        private void UpdatePeck()
        {
            if (!struck)
            {
                struck = true;
                if (AgentReady) Agent.Move(transform.forward * 0.4f);
                if (FlatDistance(Target.position) <= peckReach && AngleToTarget() <= peckArc * 0.5f && HurtPlayer(peckDamage))
                    ShovePlayer(FlatDirectionTo(Target.position), 0.8f);
            }
            if (stateTime >= 0.25f) Enter(State.Recover);
        }

        private void UpdateStompWindup(float dt)
        {
            if (stateTime < stompWindup * 0.8f) TurnToward(Target.position, 260f * dt);
            if (stateTime < stompWindup) return;
            hopFrom = transform.position;
            Vector3 target = Target.position;
            Vector3 offset = target - hopFrom;
            offset.y = 0f;
            if (offset.magnitude > stompRange) target = hopFrom + offset.normalized * stompRange;
            hopTo = NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas) ? hit.position : hopFrom;
            Enter(State.Stomp);
        }

        private void UpdateStomp()
        {
            float t = Mathf.Clamp01(stateTime / stompTime);
            if (AgentReady) Agent.Move(Vector3.Lerp(hopFrom, hopTo, t) - transform.position);
            if (t < 1f) return;
            Dust(hopTo, stompRadius);
            if (Flat(Target.position - hopTo) <= stompRadius && HurtPlayer(stompDamage)) ShovePlayer(Target.position - hopTo, 1.5f);
            Enter(State.Recover);
        }

        /// <summary>The wing still attached: the one that scoops and flings the sand.</summary>
        private int FlingSide => tornSide == 0 ? 1 : tornSide == 1 ? 0 : 1;

        /// <summary>Where the scooping wing's tip is now (the tip pivot's far end, or the wing's).</summary>
        private Vector3 WingTipPosition(int side)
        {
            float s = side == 0 ? -1f : 1f;
            Transform tip = side < wingTips.Length ? wingTips[side] : null;
            if (tip != null) return tip.TransformPoint(new Vector3(1.1f * s, 0f, -0.3f));
            Transform wing = side < wings.Length ? wings[side] : null;
            return wing != null ? wing.TransformPoint(new Vector3(2.1f * s, 0f, -0.3f)) : transform.position + Vector3.up;
        }

        private void UpdateGust()
        {
            // The wing whips forward out of the sand; the spray leaves its tip halfway through the swing.
            if (!struck && stateTime >= GustFlingAt)
            {
                struck = true;
                Vector3 forward = transform.forward;
                forward.y = 0f;
                Vector3 toPlayer = Target.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.magnitude <= gustRange && Vector3.Angle(forward, toPlayer) <= gustHalfAngle)
                {
                    HurtPlayer(gustDamage);
                    ShovePlayer(forward, gustPush);
                    MiniBossHud.SandBlind(sandBlindTime);
                }
                Vector3 tip = WingTipPosition(FlingSide);
                Vector3 aim = (TargetView != null ? TargetView.position : Target.position + Vector3.up * 1.4f) - tip;
                if (Vector3.Angle(new Vector3(aim.x, 0f, aim.z), forward) > gustHalfAngle) aim = forward * gustRange + Vector3.up * 0.5f;
                aim += Vector3.up * aim.magnitude * 0.08f; // a slight arc
                ParticleBurst.Spawn(ParticleBurst.Kind.Sand, tip, aim, 46, 0.55f, gustRange * 1.6f, 14f);
                ParticleBurst.Spawn(ParticleBurst.Kind.Sand, tip, aim + Vector3.up * aim.magnitude * 0.15f, 24, 0.9f, gustRange * 1.1f, 22f);
            }
            if (stateTime >= GustSwingTime + 0.25f) Enter(State.Recover);
        }

        private const float GustSwingTime = 0.2f;
        private const float GustFlingAt = 0.08f;

        private void UpdateTakeoff()
        {
            // Flaps frantically, gets about two metres up, and drops like a stone.
            if (stateTime >= takeoffTime)
            {
                Dust(transform.position, 1.6f);
                nextTakeoff = Time.time + Random.Range(takeoffInterval.x, Mathf.Max(takeoffInterval.x, takeoffInterval.y));
                MiniBossHud.Message("Old Buzzard can't fly!", new Color(1f, 0.85f, 0.5f), 1.2f);
                Enter(State.Dazed);
            }
        }

        // ---------- Hits ----------

        protected override float OnPartHit(int part, float amount, GameObject source)
        {
            if (part >= 0 && part < 2) wingDamage[part] += amount;
            bool airborneAttack = state == State.Soar || state == State.DiveWindup || state == State.BombWindup ||
                                  state == State.BombRun || state == State.Climb;
            if (Phase == 1 && flying && amount >= HeavyHit && airborneAttack && !InPhaseShift)
            {
                ClearMarker();
                runDirection = Vector3.zero;
                wobbleFrom = transform.position;
                if (sparkMaterial != null && part >= 0 && part < wings.Length && wings[part] != null)
                    ImpactFlash.Spawn(sparkMaterial, wings[part].position, 0.6f, 0.12f);
                nextDive = Mathf.Max(nextDive, Time.time + 2f);
                Enter(State.Wobble);
            }
            return amount;
        }

        /// <summary>IBoltReflector: in phase 2 its remaining wing shields its front.</summary>
        public bool TryReflect(Vector3 hitPoint, Vector3 incoming, Transform shooter, out Vector3 newDirection, out float damageMultiplier)
        {
            damageMultiplier = deflectDamageMultiplier;
            newDirection = -incoming;
            if (Phase != 2 || IsDead || InPhaseShift || state == State.Dazed || state == State.Takeoff) return false;
            Vector3 from = -incoming;
            from.y = 0f;
            if (Vector3.Angle(transform.forward, from) > shieldAngle) return false;
            if (shooter != null)
            {
                newDirection = shooter.position + Vector3.up * 1.2f - hitPoint;
                Vector3 axis = Vector3.Cross(newDirection, Random.onUnitSphere);
                if (axis.sqrMagnitude > 0.0001f) newDirection = Quaternion.AngleAxis(Random.Range(-6f, 6f), axis) * newDirection;
            }
            if (sparkMaterial != null) ImpactFlash.Spawn(sparkMaterial, hitPoint, 0.35f, 0.08f);
            return true;
        }

        // ---------- Phase change, reset, death ----------

        protected override void OnPhaseTwo()
        {
            ClearMarker();
            runDirection = Vector3.zero;
            TearWing();
            verticalSpeed = 0f;
            Enter(State.Fall);
        }

        private void TearWing()
        {
            if (tornSide >= 0) return;
            tornSide = wingDamage[1] > wingDamage[0] ? 1 : wingDamage[0] > wingDamage[1] ? 0 : Random.Range(0, 2);
            tornWing = wings[tornSide];
            if (tornWing == null) return;
            tornParent = tornWing.parent;
            tornPosition = tornWing.localPosition;
            tornRotation = tornWing.localRotation;
            tornScale = tornWing.localScale;
            foreach (Collider collider in tornWing.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            tornWing.SetParent(null, true);
            debrisVelocity = (tornSide == 0 ? -transform.right : transform.right) * 4f + Vector3.up * 2f;
            debrisFalling = true;
            ShipPart = tornWing; // the torn-off panels are what it drops when defeated
            if (sparkMaterial != null) ImpactFlash.Spawn(sparkMaterial, tornWing.position, 1.2f, 0.2f);
        }

        protected override void UpdatePhaseShift(float dt)
        {
            if (!flying) return;
            // Crashes down, spinning, then lands in a burst of sand.
            verticalSpeed += 25f * dt;
            Vector3 p = transform.position;
            p.y -= verticalSpeed * dt;
            transform.position = p;
            transform.Rotate(0f, 400f * dt, 0f);
            if (p.y > GroundY + 0.05f) return;
            Land();
            Dust(transform.position, 3f);
            MiniBossHud.Message("Old Buzzard crashed: it's grounded!", new Color(1f, 0.7f, 0.4f), 1.6f);
        }

        protected override void OnPhaseShiftEnded()
        {
            if (flying) Land();
            nextTakeoff = Time.time + Random.Range(takeoffInterval.x, Mathf.Max(takeoffInterval.x, takeoffInterval.y));
            nextAttack = Time.time + 0.6f;
            Enter(State.Walk);
        }

        protected override void OnReset()
        {
            ClearMarker();
            runDirection = Vector3.zero;
            wingDamage[0] = wingDamage[1] = 0f;
            if (tornWing != null)
            {
                tornWing.SetParent(tornParent, false);
                tornWing.localPosition = tornPosition;
                tornWing.localRotation = tornRotation;
                tornWing.localScale = tornScale;
                foreach (Collider collider in tornWing.GetComponentsInChildren<Collider>(true)) collider.enabled = true;
            }
            tornWing = null;
            tornSide = -1;
            debrisFalling = false;
            ShipPart = null;
            if (AgentReady) Agent.Warp(Home);
            transform.position = Home;
            TakeOff(patrolHeight);
            Enter(State.Soar);
        }

        protected override void OnBossDeath()
        {
            ClearMarker();
            if (tornWing == null) TearWing(); // defeated before the phase change: still drops a wing
            debrisFalling = false;
        }

        protected override void UpdateDeath(float t)
        {
            // Falls out of the sky first if it dies in the air, then flops over.
            if (!deathLanded)
            {
                verticalSpeed += 25f * Time.deltaTime;
                Vector3 p = transform.position;
                p.y = Mathf.Max(GroundY, p.y - verticalSpeed * Time.deltaTime);
                transform.position = p;
                if (p.y > GroundY + 0.01f) return;
                deathLanded = true;
                deathLandTime = t;
                Dust(p, 2.5f);
            }
            base.UpdateDeath(t - deathLandTime);
        }

        private void ClearMarker()
        {
            if (marker != null) Destroy(marker.gameObject);
            marker = null;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ClearMarker();
        }

        private void Dust(Vector3 point, float radius)
        {
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, point + Vector3.up * 0.15f, Vector3.up, Mathf.RoundToInt(10f + radius * 8f),
                0.4f + radius * 0.3f, 2f + radius * 2f, 80f);
            if (dustMaterial != null) ExpandingRing.Spawn(dustMaterial, point + Vector3.up * 0.05f, radius * 1.4f, 0.35f, 0.15f);
        }

        // ---------- Animation ----------
        //
        // Every joint eases toward a target pose instead of snapping to it, so moves blend into each other. Flapping
        // runs on its own phase (fast downstroke, slower recovery, wing tips trailing the arm), the body bobs with the
        // wingbeat and banks into turns, and the head keeps watching the player.

        private readonly Quaternion[] wingPose = new Quaternion[2], tipPose = new Quaternion[2];
        private readonly Quaternion[] tipRest = { Quaternion.identity, Quaternion.identity };
        private Quaternion[] legPose = new Quaternion[0];
        private Quaternion neckPose = Quaternion.identity, headLook = Quaternion.identity, tailPose = Quaternion.identity;
        private Quaternion bodyPose = Quaternion.identity, headRest = Quaternion.identity, tailRest = Quaternion.identity;
        private Vector3 bodyOffset;
        private float flapPhase, walkPhase, lastYaw, bank, glideFlapUntil, nextGlideFlap, nextScoopPuff;

        private struct WingTarget
        {
            public float Raise, Sweep, TipRaise, TipSweep;
            public WingTarget(float raise, float sweep, float tipRaise, float tipSweep)
            {
                Raise = raise; Sweep = sweep; TipRaise = tipRaise; TipSweep = tipSweep;
            }
        }

        /// <summary>Raise: degrees up from level (negative droops). Sweep: degrees forward (negative swept back).</summary>
        private static Quaternion WingRotation(int side, float raise, float sweep)
        {
            float sign = side == 0 ? 1f : -1f; // the left wing runs along -X, the right along +X
            return Quaternion.Euler(0f, sign * sweep, -sign * raise);
        }

        private static float Ease(float rate, float dt) => 1f - Mathf.Exp(-rate * dt);
        private static float Smooth(float t) => t * t * (3f - 2f * Mathf.Clamp01(t));

        protected override void AnimateBoss(float dt)
        {
            UpdateDebris(dt);
            float speed = AgentReady ? Agent.velocity.magnitude : 0f;

            // Bank into turns (flying) from how fast it is turning.
            float yaw = transform.eulerAngles.y;
            float turnRate = dt > 0f ? Mathf.DeltaAngle(lastYaw, yaw) / dt : 0f;
            lastYaw = yaw;
            bank = Mathf.Lerp(bank, Mathf.Clamp(-turnRate * 0.18f, -35f, 35f), Ease(4f, dt));

            float flapRate = 0f, flapAmp = 0f, dihedral = 0f;
            float pitch = 0f, roll = 0f, twist = 0f, neckPitch = 0f, legSwing = 0f, tailPitch = 0f, headWeight = 1f;
            float poseRate = 10f;
            Vector3 offset = Vector3.zero;
            var target = new WingTarget[2];
            bool useFlap = false;
            int scoopSide = FlingSide;

            if (flying)
            {
                useFlap = true;
                switch (state)
                {
                    case State.Dive:
                    {
                        // Wings tucked back, nose down; talons swing forward just before impact.
                        float t = Mathf.Clamp01(stateTime / diveTime);
                        useFlap = false;
                        target[0] = target[1] = new WingTarget(18f, -55f, -8f, -65f);
                        pitch = 38f;
                        neckPitch = 25f;
                        legSwing = t < 0.6f ? 55f : -45f;
                        tailPitch = -10f;
                        poseRate = 16f;
                        break;
                    }
                    case State.DiveWindup:
                        // Hovering over the target: quick, deep beats, body upright, peering down.
                        flapRate = 11f; flapAmp = 42f; dihedral = 5f;
                        pitch = -18f; neckPitch = 30f; legSwing = 10f; tailPitch = 15f;
                        break;
                    case State.Landed:
                        // Sat on the sand after the dive: wings spread and drooping for balance, catching its breath.
                        useFlap = false;
                        target[0] = target[1] = new WingTarget(-18f + Mathf.Sin(Time.time * 3f) * 4f, 8f, -12f, 5f);
                        pitch = 14f; neckPitch = -5f; legSwing = 0f;
                        offset = Vector3.down * (0.08f + Mathf.Sin(Time.time * 6f) * 0.02f);
                        break;
                    case State.Climb:
                    case State.BombWindup:
                        flapRate = 9.5f; flapAmp = 44f; dihedral = 4f;
                        pitch = state == State.Climb ? -20f : -6f; neckPitch = 15f; legSwing = 50f;
                        break;
                    case State.BombRun:
                        flapRate = 6.5f; flapAmp = 32f; dihedral = 6f;
                        pitch = 4f; neckPitch = 35f; legSwing = 25f; // peering down at its bombs
                        break;
                    case State.Wobble:
                        flapRate = 14f; flapAmp = 55f; dihedral = 0f;
                        pitch = Mathf.Sin(Time.time * 13f) * 20f;
                        roll = Mathf.Sin(Time.time * 11f) * 35f;
                        legSwing = Mathf.Sin(Time.time * 9f) * 30f;
                        poseRate = 14f;
                        break;
                    case State.Fall:
                        flapRate = 18f; flapAmp = 60f;
                        pitch = 20f; legSwing = Mathf.Sin(Time.time * 15f) * 40f;
                        break;
                    default: // Soar / patrol: gliding, with a few beats now and then
                        if (Time.time >= nextGlideFlap)
                        {
                            glideFlapUntil = Time.time + Random.Range(0.9f, 1.6f);
                            nextGlideFlap = glideFlapUntil + Random.Range(2.5f, 5f);
                        }
                        bool beating = Time.time < glideFlapUntil;
                        flapRate = beating ? 7f : 1.3f;
                        flapAmp = beating ? 34f : 5f;
                        dihedral = beating ? 6f : 12f;
                        pitch = 6f; neckPitch = 18f; legSwing = 55f;
                        roll = bank;
                        break;
                }
                if (state != State.Wobble && state != State.Dive && state != State.Landed) roll += bank * 0.6f;
            }
            else
            {
                float lopsided = tornSide == 0 ? -12f : tornSide == 1 ? 12f : 0f;
                roll = lopsided;
                // The remaining wing: folded along its side, half-open and fluttering for balance as it walks.
                float walking = Mathf.Clamp01(speed / Mathf.Max(0.1f, walkSpeed));
                WingTarget folded = new WingTarget(-4f + walking * 6f + Mathf.Sin(Time.time * 9f) * 3f * walking,
                    -68f + walking * 25f, -6f, -70f + walking * 30f);
                target[0] = target[1] = folded;

                switch (state)
                {
                    case State.PeckWindup:
                    {
                        float k = Smooth(Mathf.Clamp01(stateTime / peckWindup));
                        neckPitch = -35f * k; pitch = -10f * k; headWeight = 1f - k * 0.5f;
                        offset = Vector3.down * 0.12f * k;
                        poseRate = 14f;
                        break;
                    }
                    case State.Peck:
                        neckPitch = 65f; pitch = 22f; headWeight = 0.2f;
                        poseRate = 40f;
                        break;
                    case State.StompWindup:
                    {
                        float k = Smooth(Mathf.Clamp01(stateTime / stompWindup));
                        offset = Vector3.down * 0.25f * k;
                        pitch = 12f * k;
                        target[scoopSide] = new WingTarget(35f * k, -10f, 15f * k, 0f);
                        legSwing = -10f * k;
                        break;
                    }
                    case State.Stomp:
                    {
                        float t = Mathf.Clamp01(stateTime / stompTime);
                        offset = Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.6f;
                        // One big beat of the good wing to get airborne, legs tucked, then reaching for the landing.
                        target[scoopSide] = new WingTarget(Mathf.Lerp(55f, -25f, t), Mathf.Lerp(0f, 10f, t), Mathf.Lerp(25f, -15f, t), 0f);
                        legSwing = t < 0.6f ? 45f : -20f;
                        pitch = Mathf.Lerp(-8f, 14f, t);
                        poseRate = 22f;
                        break;
                    }
                    case State.GustWindup:
                    {
                        // Lean in, reach the good wing out and down until the tip is in the sand beside it, then drag it
                        // back along the ground: the scoop. (Poses worked out so the wingtip sits on the sand throughout.)
                        float k = Mathf.Clamp01(stateTime / gustWindup);
                        float reach = Smooth(Mathf.Clamp01(k / 0.45f));
                        float drag = Smooth(Mathf.Clamp01((k - 0.45f) / 0.55f));
                        WingTarget start = target[scoopSide];
                        var down = new WingTarget(-32f, 20f, -20f, 5f);
                        var back = new WingTarget(-40f, 0f, -26f, -5f);
                        target[scoopSide] = reach < 1f
                            ? new WingTarget(Mathf.Lerp(start.Raise, down.Raise, reach), Mathf.Lerp(start.Sweep, down.Sweep, reach),
                                Mathf.Lerp(start.TipRaise, down.TipRaise, reach), Mathf.Lerp(start.TipSweep, down.TipSweep, reach))
                            : new WingTarget(Mathf.Lerp(down.Raise, back.Raise, drag), Mathf.Lerp(down.Sweep, back.Sweep, drag),
                                Mathf.Lerp(down.TipRaise, back.TipRaise, drag), Mathf.Lerp(down.TipSweep, back.TipSweep, drag));
                        pitch = 25f * reach;
                        twist = (scoopSide == 1 ? 1f : -1f) * (15f * reach + 5f * drag);
                        roll = Mathf.Lerp(roll, (scoopSide == 1 ? 1f : -1f) * 10f, reach);
                        offset = Vector3.down * 0.1f * reach;
                        headWeight = 1f;
                        poseRate = 12f;
                        if (reach > 0.85f && Time.time >= nextScoopPuff)
                        {
                            nextScoopPuff = Time.time + 0.12f;
                            Vector3 tip = WingTipPosition(scoopSide);
                            tip.y = Mathf.Min(tip.y, GroundY + 0.1f);
                            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, tip, Vector3.up - transform.forward * 0.6f, 5, 0.3f, 1.6f, 35f);
                        }
                        break;
                    }
                    case State.Gust:
                    {
                        // The fling: the wing whips forward and up out of the sand, the body unwinding behind it.
                        float t = Smooth(Mathf.Clamp01(stateTime / GustSwingTime));
                        target[scoopSide] = new WingTarget(Mathf.Lerp(-40f, 55f, t), Mathf.Lerp(0f, 60f, t), Mathf.Lerp(-26f, 25f, t), Mathf.Lerp(-5f, 30f, t));
                        pitch = Mathf.Lerp(25f, -6f, t);
                        twist = (scoopSide == 1 ? 1f : -1f) * Mathf.Lerp(20f, -26f, t);
                        poseRate = 45f;
                        break;
                    }
                    case State.Takeoff:
                    {
                        float k = stateTime / takeoffTime;
                        useFlap = true;
                        flapRate = 21f; flapAmp = 58f; dihedral = 5f;
                        offset = Vector3.up * (k < 0.55f ? Mathf.Sin(k / 0.55f * Mathf.PI * 0.5f) * 1.8f : Mathf.Max(0f, 1.8f * (1f - (k - 0.55f) / 0.45f * 2.2f)));
                        roll = lopsided * 2.5f + Mathf.Sin(Time.time * 17f) * 10f; // one wing: it lurches sideways
                        pitch = -15f;
                        legSwing = k < 0.55f ? 30f : -10f;
                        break;
                    }
                    case State.Dazed:
                        target[scoopSide] = new WingTarget(-30f, 5f, -15f, 0f);
                        pitch = Mathf.Sin(Time.time * 7f) * 10f;
                        twist = Mathf.Sin(Time.time * 3f) * 25f;
                        neckPitch = 20f + Mathf.Sin(Time.time * 5f) * 15f;
                        headWeight = 0f;
                        break;
                    default: // Walk, Recover, Fall (landed), Landed
                    {
                        // Waddle: two steps a cycle, a bob on each, rolling from foot to foot, the head bobbing like a bird's.
                        walkPhase += dt * (3f + speed * 2.6f);
                        float step = Mathf.Abs(Mathf.Sin(walkPhase));
                        offset = Vector3.up * step * 0.07f * walking;
                        roll += Mathf.Sin(walkPhase) * 7f * walking;
                        legSwing = 0f;
                        neckPitch = Mathf.Sin(walkPhase * 2f) * 7f * walking + 4f;
                        pitch = 4f * walking;
                        break;
                    }
                }
            }

            // Wings: flapping or posed.
            if (useFlap) flapPhase += dt * flapRate;
            float cycle = flapPhase + 0.45f * Mathf.Sin(flapPhase); // quick downstroke, slower upstroke
            float beat = Mathf.Cos(cycle);
            if (useFlap)
            {
                for (int side = 0; side < 2; side++)
                    target[side] = new WingTarget(dihedral + beat * flapAmp, -4f + Mathf.Sin(cycle) * flapAmp * 0.15f,
                        Mathf.Cos(cycle - 0.9f) * flapAmp * 0.55f, -6f - Mathf.Sin(cycle - 0.9f) * flapAmp * 0.2f);
                if (flapAmp > 20f) offset += Vector3.up * Mathf.Sin(cycle) * 0.05f * (flapAmp / 40f); // lifts on each downstroke
            }
            float wingRate = useFlap ? 30f : poseRate;
            for (int side = 0; side < 2 && side < wings.Length; side++)
            {
                if (wings[side] == null || side == tornSide) continue;
                wingPose[side] = Quaternion.Slerp(wingPose[side], WingRotation(side, target[side].Raise, target[side].Sweep), Ease(wingRate, dt));
                wings[side].localRotation = wingRest[side] * wingPose[side];
                Transform tip = side < wingTips.Length ? wingTips[side] : null;
                if (tip == null) continue;
                tipPose[side] = Quaternion.Slerp(tipPose[side], WingRotation(side, target[side].TipRaise, target[side].TipSweep), Ease(wingRate, dt));
                tip.localRotation = tipRest[side] * tipPose[side];
            }

            // Body.
            if (state == State.Landed && flying) { pitch = 14f; roll = Mathf.Sin(Time.time * 5f) * 5f; }
            Quaternion lean = Quaternion.Euler(pitch, twist, roll);
            bodyPose = Quaternion.Slerp(bodyPose, lean, Ease(poseRate, dt));
            bodyOffset = Vector3.Lerp(bodyOffset, offset, Ease(state == State.Takeoff || state == State.Stomp ? 30f : 12f, dt));
            Visual.localPosition = VisualRest + bodyOffset;
            Visual.localRotation = VisualRestRotation * bodyPose;

            // Neck and head: the neck carries the big moves, the head keeps its eyes on the player.
            if (neck != null)
            {
                neckPose = Quaternion.Slerp(neckPose, Quaternion.Euler(neckPitch, 0f, 0f), Ease(poseRate, dt));
                neck.localRotation = neckRest * neckPose;
            }
            if (head != null && head.parent != null)
            {
                Quaternion want = headRest;
                if (headWeight > 0f && Target != null)
                {
                    Vector3 look = (TargetView != null ? TargetView.position : Target.position + Vector3.up * 1.5f) - head.position;
                    if (look.sqrMagnitude > 0.01f)
                    {
                        Quaternion local = Quaternion.Inverse(head.parent.rotation) * Quaternion.LookRotation(look, Vector3.up);
                        float angle = Quaternion.Angle(headRest, local);
                        if (angle > 70f) local = Quaternion.Slerp(headRest, local, 70f / angle);
                        want = Quaternion.Slerp(headRest, local, headWeight);
                    }
                }
                if (state == State.Dazed) want = headRest * Quaternion.Euler(Mathf.Sin(Time.time * 9f) * 15f, Mathf.Sin(Time.time * 4f) * 40f, 0f);
                headLook = Quaternion.Slerp(headLook, want, Ease(8f, dt));
                head.localRotation = headLook;
            }

            if (tail != null)
            {
                float fan = flying ? Mathf.Sin(Time.time * 2f) * 4f + bank * 0.3f : Mathf.Sin(walkPhase) * 6f;
                tailPose = Quaternion.Slerp(tailPose, Quaternion.Euler(tailPitch + (flying ? -pitch * 0.4f : 0f), fan, 0f), Ease(8f, dt));
                tail.localRotation = tailRest * tailPose;
            }

            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;
                float swing = legSwing;
                if (!flying && state != State.Stomp && state != State.Takeoff)
                {
                    float walking = Mathf.Clamp01(speed / Mathf.Max(0.1f, walkSpeed));
                    swing += Mathf.Sin(walkPhase + i * Mathf.PI) * 28f * walking;
                }
                legPose[i] = Quaternion.Slerp(legPose[i], Quaternion.Euler(swing, 0f, 0f), Ease(poseRate * 1.5f, dt));
                legs[i].localRotation = legRest[i] * legPose[i];
            }
        }

        private void UpdateDebris(float dt)
        {
            if (!debrisFalling || tornWing == null) return;
            debrisVelocity += Vector3.down * 20f * dt;
            tornWing.position += debrisVelocity * dt;
            tornWing.Rotate(new Vector3(260f, 120f, 340f) * dt, Space.World);
            if (tornWing.position.y > GroundY + 0.15f) return;
            tornWing.position = new Vector3(tornWing.position.x, GroundY + 0.15f, tornWing.position.z);
            tornWing.rotation = Quaternion.Euler(0f, tornWing.eulerAngles.y, 180f);
            debrisFalling = false;
            Dust(tornWing.position, 1.4f);
        }
    }
}
