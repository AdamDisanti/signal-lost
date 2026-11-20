using SignalLost.Combat;
using SignalLost.Enemies.Desert;
using SignalLost.Finale;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.Enemies.Caves
{
    /// <summary>
    /// Crystal Caves pressure fodder: a shaggy bighorn ram whose curled horns end in glowing cave crystals.
    ///
    /// Close up it lifts its front legs (the warning), hops sideways twice, then rears and headbutts. At mid range a
    /// ring appears on a nearby wall or stalagmite and it lowers its head (the warning), charges the wall and bounces
    /// off; a line then tracks the player, locks, and it rebounds along it. If the rebound hits scenery instead of the
    /// player it bonks its head and stands dazed: the opening. With no wall in reach it only hop-and-butts.
    ///
    /// Builds on <see cref="DesertFodder"/> like every other small enemy (territory, leash and heal, hit flash,
    /// combo stun, death). All numbers are placeholders.
    /// </summary>
    public sealed class GemRam : DesertFodder
    {
        private enum State { Idle, Approach, HopWindUp, Hop, Rear, Butt, RicochetWindUp, Charge, Aim, Rebound, Daze, Recover }

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float trotSpeed = 4f;
        [Tooltip("Hop-and-butt when the player is closer than this.")]
        [SerializeField, Min(1f)] private float closeRange = 4.5f;
        [Tooltip("Ricochet when the player is between closeRange and this.")]
        [SerializeField, Min(2f)] private float midRange = 12f;

        [Header("Hop-and-butt")]
        [SerializeField, Min(0.1f)] private float hopWindUpTime = 0.5f;
        [SerializeField, Min(0.5f)] private float hopDistance = 3f;
        [SerializeField, Min(0.05f)] private float hopTime = 0.3f;
        [Tooltip("Rearing before the headbutt: the moment to sidestep.")]
        [SerializeField, Min(0.05f)] private float rearTime = 0.35f;
        [SerializeField, Min(0.05f)] private float buttTime = 0.18f;
        [SerializeField, Min(0f)] private float buttLunge = 1.5f;
        [SerializeField, Min(0.3f)] private float buttReach = 1.7f;
        [SerializeField, Min(0f)] private float buttDamage = 18f;
        [SerializeField, Min(0f)] private float buttShove = 1f;

        [Header("Ricochet")]
        [Tooltip("Only walls and pillars this close are used.")]
        [SerializeField, Min(2f)] private float wallSearchRange = 10f;
        [SerializeField, Min(0.1f)] private float ricochetWindUpTime = 0.8f;
        [SerializeField, Min(1f)] private float chargeSpeed = 12f;
        [Tooltip("After the bounce the line follows the player this long, then locks.")]
        [SerializeField, Min(0f)] private float aimTrackTime = 0.4f;
        [Tooltip("Locked line on screen before the rebound.")]
        [SerializeField, Min(0f)] private float aimLockTime = 0.35f;
        [SerializeField, Min(1f)] private float reboundRange = 16f;
        [SerializeField, Min(0f)] private float ricochetDamage = 22f;
        [SerializeField, Min(0f)] private float ricochetShove = 1.4f;
        [SerializeField, Min(0.3f)] private float chargeHitRadius = 0.9f;
        [SerializeField, Min(0f)] private float dazeTime = 2f;

        [SerializeField, Min(0f)] private float attackCooldown = 1.2f;

        [Header("Rig")]
        [SerializeField] private Transform head;
        [Tooltip("Front left, front right, back left, back right.")]
        [SerializeField] private Transform[] legs = new Transform[0];
        [SerializeField] private Renderer[] hornCrystals = new Renderer[0];
        [SerializeField] private Light glow;
        [Tooltip("Horn crystals after it dies (gone dull).")]
        [SerializeField] private Material spentMaterial;
        [SerializeField] private Material ringMaterial;
        [SerializeField] private Material dustMaterial;
        [SerializeField] private Color lineColor = new Color(1f, 0.45f, 0.15f, 0.55f);

        private State state = State.Idle;
        private float stateTime, nextAttack, nextRepath, nextDust, hopSide, hop, hopCloseIn;
        private Vector3 hopForward;
        private int hopsDone;
        private bool hitThisMove;
        private Vector3 moveDirection, wallPoint, shoveDirection;
        private float shoveRemaining;
        private Quaternion headRest = Quaternion.identity;
        private GroundMarker ring;
        private Telegraph line;

        public bool IsDazed => state == State.Daze;
        public bool IsRicocheting => state == State.RicochetWindUp || state == State.Charge || state == State.Aim || state == State.Rebound;

        protected override void Awake()
        {
            base.Awake();
            if (head != null) headRest = head.localRotation;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ClearMarkers();
        }

        private void Enter(State next)
        {
            if (next != State.Charge) ClearMarkers(); // the wall ring stays up through the charge
            state = next;
            stateTime = 0f;
            if (next != State.Approach && next != State.Idle) Halt();
        }

        protected override void Tick(float dt)
        {
            stateTime += dt;
            UpdateShove(dt);
            switch (state)
            {
                case State.Idle:
                    Halt();
                    if (Notices()) { LastSeen = Time.time; Enter(State.Approach); }
                    break;
                case State.Approach: UpdateApproach(); break;
                case State.HopWindUp:
                    if (Target != null) TurnToward(Target.position, 360f * dt);
                    if (stateTime >= hopWindUpTime) StartHop();
                    break;
                case State.Hop: UpdateHop(dt); break;
                case State.Rear:
                    if (Target != null) TurnToward(Target.position, 300f * dt);
                    if (stateTime >= rearTime) { hitThisMove = false; Enter(State.Butt); }
                    break;
                case State.Butt: UpdateButt(dt); break;
                case State.RicochetWindUp: UpdateRicochetWindUp(dt); break;
                case State.Charge: UpdateCharge(dt); break;
                case State.Aim: UpdateAim(dt); break;
                case State.Rebound: UpdateRebound(dt); break;
                case State.Daze: if (stateTime >= dazeTime) EndAttack(); break;
                case State.Recover: if (stateTime >= 0.5f) EndAttack(); break;
            }
            Animate(dt);
        }

        // ---------- Approach ----------

        private void UpdateApproach()
        {
            if (!TargetAlive) { Enter(State.Idle); return; }
            if (Notices()) LastSeen = Time.time;
            if (GaveUp()) { Enter(State.Idle); return; }
            if (Time.time - LastSeen > 4f) { Enter(State.Idle); return; }

            float distance = FlatDistance(Target.position);
            bool seen = CanSee(midRange + 2f);
            if (seen) LastSeen = Time.time;
            if (seen && Time.time >= nextAttack)
            {
                if (distance <= closeRange) { Enter(State.HopWindUp); return; }
                if (distance <= midRange && FindWall(out wallPoint, out Vector3 normal))
                {
                    Enter(State.RicochetWindUp);
                    ring = GroundMarker.Spawn(ringMaterial, null, wallPoint, 0.7f);
                    ring.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal); // flat against the wall
                    return;
                }
            }
            if (Time.time < nextRepath) return;
            nextRepath = Time.time + 0.25f;
            Chase(Target.position, trotSpeed);
        }

        /// <summary>
        /// A wall or pillar within reach whose bounce would send it back toward the player. The point is on the real
        /// surface (for the ring); the charge itself stops at the NavMesh edge in front of it.
        /// </summary>
        private bool FindWall(out Vector3 point, out Vector3 normal)
        {
            point = normal = Vector3.zero;
            if (!AgentReady) return false;
            Vector3 from = transform.position;
            float best = float.MinValue;
            for (int i = 0; i < 16; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                if (!NavMesh.Raycast(from, from + direction * wallSearchRange, out NavMeshHit hit, NavMesh.AllAreas)) continue;
                if (hit.distance < 2.5f) continue; // no room to build up speed
                Vector3 toPlayer = Target.position - hit.position;
                toPlayer.y = 0f;
                float score = Vector3.Dot(toPlayer.normalized, FlatNormal(hit.normal, direction)) - hit.distance * 0.03f;
                if (score <= best) continue;
                best = score;
                normal = FlatNormal(hit.normal, direction);
                point = hit.position + Vector3.up * 0.9f;
                if (Physics.Raycast(point, direction, out RaycastHit surface, 2f, sightMask, QueryTriggerInteraction.Ignore))
                {
                    point = surface.point + surface.normal * 0.05f;
                    normal = surface.normal;
                }
            }
            return best > 0f; // the bounce must face the player's side
        }

        private static Vector3 FlatNormal(Vector3 normal, Vector3 travel)
        {
            normal.y = 0f;
            return normal.sqrMagnitude > 0.01f ? normal.normalized : -travel;
        }

        // ---------- Hop-and-butt ----------

        private void StartHop()
        {
            hopsDone = 0;
            hopSide = Random.value < 0.5f ? -1f : 1f;
            hopForward = Target != null ? FlatDirectionTo(Target.position) : transform.forward; // fixed, so the zig-zag doesn't circle outward
            hopCloseIn = Target != null ? Mathf.Max(0f, FlatDistance(Target.position) - 2f) * 0.5f : 0f;
            BeginHop();
        }

        private void BeginHop()
        {
            // Zig-zag sideways while closing to about 2 m, so the butt can reach.
            Vector3 side = Vector3.Cross(Vector3.up, hopForward) * hopSide;
            moveDirection = (side * hopDistance + hopForward * hopCloseIn) / hopTime;
            Enter(State.Hop);
        }

        private void UpdateHop(float dt)
        {
            if (AgentReady) Agent.Move(moveDirection * dt);
            if (Target != null) TurnToward(Target.position, 400f * dt);
            if (stateTime < hopTime) return;
            Dust(transform.position);
            hopsDone++;
            hopSide = -hopSide;
            if (hopsDone < 2) BeginHop();
            else Enter(State.Rear);
        }

        private void UpdateButt(float dt)
        {
            if (AgentReady) Agent.Move(transform.forward * (buttLunge / buttTime) * dt);
            if (!hitThisMove && TargetAlive && InFront(buttReach))
            {
                hitThisMove = true;
                if (TargetHealth != null) TargetHealth.TakeDamage(buttDamage, gameObject);
                Shove(transform.forward, buttShove);
            }
            if (stateTime >= buttTime) Enter(State.Recover);
        }

        private bool InFront(float reach)
        {
            Vector3 offset = Target.position - transform.position;
            float height = offset.y;
            offset.y = 0f;
            float r = reach + (TargetBody != null ? TargetBody.radius : 0.4f);
            return offset.magnitude <= r && height > -1f && height < 1.6f && Vector3.Angle(transform.forward, offset) < 60f;
        }

        // ---------- Ricochet ----------

        private void UpdateRicochetWindUp(float dt)
        {
            TurnToward(wallPoint, 360f * dt);
            if (ring != null) ring.Progress = stateTime / ricochetWindUpTime;
            if (stateTime < ricochetWindUpTime) return;
            moveDirection = FlatDirectionTo(wallPoint);
            hitThisMove = false;
            Enter(State.Charge);
        }

        private void UpdateCharge(float dt)
        {
            // Runs into the wall; reaching the NavMesh edge in front of it is the bounce.
            if (Rush(dt, out _))
            {
                Dust(transform.position + moveDirection * 0.6f);
                Enter(State.Aim);
                return;
            }
            if (stateTime > wallSearchRange / chargeSpeed + 0.5f) Enter(State.Aim); // safety: never charge forever
        }

        private void UpdateAim(float dt)
        {
            if (!TargetAlive) { Enter(State.Recover); return; }
            if (stateTime <= aimTrackTime)
            {
                moveDirection = FlatDirectionTo(Target.position);
                TurnToward(Target.position, 720f * dt);
            }
            float length = reboundRange;
            Vector3 from = transform.position;
            if (AgentReady && NavMesh.Raycast(from, from + moveDirection * reboundRange, out NavMeshHit hit, NavMesh.AllAreas))
                length = Mathf.Max(1f, hit.distance + 0.5f);
            if (line == null) line = Telegraph.Spawn(Telegraph.Shape.Strip, from, Quaternion.LookRotation(moveDirection), new Vector2(1.4f, 1f), lineColor);
            line.transform.SetPositionAndRotation(from + Vector3.up * 0.07f, Quaternion.LookRotation(moveDirection));
            line.transform.localScale = new Vector3(1f, 1f, length);
            line.Progress = stateTime <= aimTrackTime ? 0f : (stateTime - aimTrackTime) / Mathf.Max(0.01f, aimLockTime);
            if (stateTime < aimTrackTime + aimLockTime) return;
            hitThisMove = false;
            Enter(State.Rebound);
        }

        private void UpdateRebound(float dt)
        {
            if (Rush(dt, out bool hitPlayer))
            {
                Dust(transform.position + moveDirection * 0.6f);
                Enter(hitPlayer ? State.Recover : State.Daze); // bonked a second wall: dazed
                return;
            }
            if (hitPlayer) { Enter(State.Recover); return; }
            if (stateTime * chargeSpeed * SpeedScale >= reboundRange) Enter(State.Recover);
        }

        /// <summary>One step of a straight charge. True when it reaches scenery; hits the player once on the way.</summary>
        private bool Rush(float dt, out bool hitPlayer)
        {
            hitPlayer = hitThisMove;
            transform.rotation = Quaternion.LookRotation(moveDirection);
            Vector3 from = transform.position;
            float step = chargeSpeed * SpeedScale * dt;
            bool wall = AgentReady && NavMesh.Raycast(from, from + moveDirection * (step + 0.3f), out _, NavMesh.AllAreas);
            if (!wall && AgentReady) Agent.Move(moveDirection * step);
            if (!hitThisMove && TargetAlive && InFront(chargeHitRadius))
            {
                hitThisMove = hitPlayer = true;
                if (TargetHealth != null) TargetHealth.TakeDamage(ricochetDamage, gameObject);
                Shove(moveDirection, ricochetShove);
                if (state == State.Rebound) return true;
            }
            if (Time.time >= nextDust)
            {
                nextDust = Time.time + 0.08f;
                Dust(transform.position - moveDirection * 0.5f);
            }
            return wall;
        }

        private void EndAttack()
        {
            nextAttack = Time.time + attackCooldown;
            LastSeen = Time.time;
            Enter(TargetAlive ? State.Approach : State.Idle);
        }

        private void ClearMarkers()
        {
            if (ring != null) Destroy(ring.gameObject);
            if (line != null) line.Clear();
            ring = null;
            line = null;
        }

        // ---------- Hits ----------

        protected override void OnHit(float amount, GameObject source, Vector3 away)
        {
            if (state == State.Idle) { LastSeen = Time.time; Enter(State.Approach); }
        }

        protected override void OnDeath()
        {
            ClearMarkers();
            if (spentMaterial != null)
                foreach (Renderer crystal in hornCrystals)
                    if (crystal != null) crystal.sharedMaterial = spentMaterial;
            if (glow != null) glow.enabled = false;
        }

        private void Shove(Vector3 direction, float distance)
        {
            shoveDirection = direction;
            shoveRemaining = distance;
        }

        // Same push as the Hubcap Armadillo's roll.
        private void UpdateShove(float dt)
        {
            if (shoveRemaining <= 0f || TargetBody == null || !TargetBody.enabled) return;
            float step = Mathf.Min(shoveRemaining, 7f * dt);
            TargetBody.Move(shoveDirection * step);
            shoveRemaining -= step;
        }

        private void Dust(Vector3 at)
        {
            if (dustMaterial != null) ImpactFlash.Spawn(dustMaterial, at + Random.insideUnitSphere * 0.3f + Vector3.up * 0.15f, 0.45f, 0.3f);
        }

        // ---------- Animation ----------

        private void Animate(float dt)
        {
            float speed = AgentReady ? Agent.velocity.magnitude : 0f;
            bool rushing = state == State.Charge || state == State.Rebound;
            float lift = state == State.HopWindUp ? Mathf.Clamp01(stateTime / (hopWindUpTime * 0.5f)) : 0f;
            float rear = state == State.Rear ? Mathf.Clamp01(stateTime / rearTime) : state == State.Butt ? 1f - stateTime / buttTime : 0f;
            hop = state == State.Hop ? Mathf.Sin(Mathf.Clamp01(stateTime / hopTime) * Mathf.PI) : 0f;

            // Trot: diagonal pairs swing together; front legs lift for the hop warning and the rear.
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;
                bool front = i < 2;
                float phase = (i == 0 || i == 3) ? 0f : Mathf.PI;
                float rate = rushing ? 18f : 6f + speed * 2.5f;
                float swing = Mathf.Sin(Time.time * rate + phase) * Mathf.Clamp(speed * 9f, 0f, rushing ? 45f : 32f);
                if (front) swing -= 55f * Mathf.Max(lift, rear);
                if (state == State.Hop) swing = front ? -25f : 25f; // tucked mid-air
                legs[i].localRotation = Quaternion.Euler(swing, 0f, 0f);
            }

            // Head: grazes while idle, lowered for the ricochet, thrown up for the headbutt.
            float pitch = 0f;
            if (state == State.Idle) pitch = 35f * Mathf.Clamp01(Mathf.Sin(Time.time * 0.6f + transform.position.x) * 1.5f);
            else if (state == State.RicochetWindUp || rushing) pitch = 30f;
            else if (state == State.Butt) pitch = Mathf.Lerp(30f, -35f, stateTime / buttTime);
            if (head != null) head.localRotation = Quaternion.Slerp(head.localRotation, headRest * Quaternion.Euler(pitch, 0f, 0f), 12f * dt);

            Quaternion pose = Quaternion.Euler(-28f * Mathf.Max(lift * 0.4f, rear), 0f, 0f);
            Vector3 offset = Vector3.up * (0.6f * hop + 0.25f * rear);
            if (state == State.Daze)
                pose = Quaternion.Euler(Mathf.Sin(Time.time * 6f) * 6f, Mathf.Sin(Time.time * 3f) * 18f, Mathf.Sin(Time.time * 8f) * 10f);
            Visual.localPosition = VisualRest + offset;
            Visual.localRotation = VisualRestRotation * pose;
        }
    }
}
