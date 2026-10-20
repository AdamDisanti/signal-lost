using SignalLost.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// Rolling charger: an armadillo with one of the ship's hubcaps fused into its shell. From mid range it curls up
    /// (spinning in place, the telegraph), then rolls at the player fast, curving slightly toward them and bouncing off
    /// walls and wreckage. A roll that connects knocks the player back.
    ///
    /// Counterplay: while curled it is armoured, so laser bolts glance off the hubcap (and can fly back at you).
    /// Sidestep the roll and it skids to a stop and uncurls dizzy, open to attack. A heavy hit (the overhead slam,
    /// 20+ damage) while it is rolling or dizzy flips it onto its back for a long opening. It deflects bolts only
    /// while moving, so it's a timing check, unlike the hermit crab's shell guard. All numbers are placeholders.
    /// </summary>
    public sealed class HubcapArmadillo : DesertFodder, IBoltReflector
    {
        private enum State { Idle, Approach, Curl, Roll, Skid, Dizzy, Recover, Flipped }

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 3f;
        [Tooltip("Rolls when the player is between these distances (and visible).")]
        [SerializeField, Min(1f)] private float minRollRange = 5f;
        [SerializeField, Min(2f)] private float maxRollRange = 15f;

        [Header("Roll")]
        [Tooltip("Curling up and spinning in place before the roll: the player's warning.")]
        [SerializeField, Min(0.1f)] private float curlTime = 0.75f;
        [SerializeField, Min(1f)] private float rollSpeed = 11f;
        [SerializeField, Min(0.2f)] private float rollTime = 2.2f;
        [Tooltip("Degrees per second it curves toward the player during the first part of the roll.")]
        [SerializeField, Min(0f)] private float rollHoming = 35f;
        [SerializeField, Min(0)] private int maxBounces = 2;
        [SerializeField, Min(0f)] private float rollDamage = 18f;
        [SerializeField, Min(0.3f)] private float rollHitRadius = 1f;
        [SerializeField, Min(0f)] private float playerShove = 1.4f;
        [SerializeField, Min(0f)] private float rollCooldown = 1.2f;

        [Header("Openings")]
        [SerializeField, Min(0f)] private float skidTime = 0.45f;
        [Tooltip("Uncurled and wobbling after a missed roll: the player's opening.")]
        [SerializeField, Min(0f)] private float dizzyTime = 2f;
        [SerializeField, Min(0f)] private float recoverTime = 0.7f;
        [Tooltip("Hits this strong while it is rolling or dizzy flip it onto its back.")]
        [SerializeField, Min(0f)] private float flipThreshold = 20f;
        [SerializeField, Min(0f)] private float flipTime = 3f;
        [Tooltip("Damage a glancing bolt keeps when it bounces off the hubcap (0 to 1).")]
        [SerializeField, Range(0f, 1f)] private float deflectDamageMultiplier = 0.4f;

        [Header("Rig")]
        [Tooltip("The shell/hubcap ball that spins while rolling.")]
        [SerializeField] private Transform shell;
        [Tooltip("Head, tail and legs: tucked away while curled.")]
        [SerializeField] private Transform[] limbs = new Transform[0];
        [SerializeField] private Transform[] legs = new Transform[0];
        [SerializeField] private Material sparkMaterial;
        [SerializeField] private Material dustMaterial;

        private State state = State.Idle;
        private float stateTime, nextRoll, nextRepath, spin, curl, nextDust;
        private Vector3 rollDirection;
        private int bounces;
        private bool rollHit;
        private Vector3 shoveDirection;
        private float shoveRemaining;
        private Vector3[] limbScales = new Vector3[0];
        private Quaternion shellRest = Quaternion.identity;

        private bool Curled => state == State.Curl || state == State.Roll || state == State.Skid;

        protected override void Awake()
        {
            base.Awake();
            limbScales = new Vector3[limbs.Length];
            for (int i = 0; i < limbs.Length; i++) if (limbs[i] != null) limbScales[i] = limbs[i].localScale;
            if (shell != null) shellRest = shell.localRotation;
        }

        private void Enter(State next)
        {
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
                    if (Notices()) { LastSeen = Time.time; Enter(State.Approach); }
                    break;
                case State.Approach: UpdateApproach(); break;
                case State.Curl: UpdateCurl(dt); break;
                case State.Roll: UpdateRoll(dt); break;
                case State.Skid: UpdateSkid(dt); break;
                case State.Dizzy: if (stateTime >= dizzyTime) EndOpening(); break;
                case State.Recover: if (stateTime >= recoverTime) EndOpening(); break;
                case State.Flipped: if (stateTime >= flipTime) EndOpening(); break;
            }
            Animate(dt);
        }

        // ---------- States ----------

        private void UpdateApproach()
        {
            if (!TargetAlive) { Enter(State.Idle); return; }
            float distance = FlatDistance(Target.position);
            bool seen = CanSee(maxRollRange + 2f);
            if (seen || Alerted) LastSeen = Time.time;
            if (distance > loseRange || Time.time - LastSeen > 5f) { Enter(State.Idle); return; }

            if (seen && distance >= minRollRange && distance <= maxRollRange && Time.time >= nextRoll)
            {
                Enter(State.Curl);
                return;
            }
            if (Time.time < nextRepath) return;
            nextRepath = Time.time + 0.3f;
            if (distance < minRollRange)
            {
                // Too close to build up speed: back off to rolling distance.
                Vector3 away = transform.position - FlatDirectionTo(Target.position) * (minRollRange + 2f - distance);
                if (NavMesh.SamplePosition(away, out NavMeshHit hit, 2f, NavMesh.AllAreas)) Chase(hit.position, walkSpeed);
            }
            else Chase(Target.position, walkSpeed);
        }

        private void UpdateCurl(float dt)
        {
            if (Target != null) TurnToward(Target.position, 400f * dt);
            if (dustMaterial != null && Time.time >= nextDust)
            {
                nextDust = Time.time + 0.1f;
                ImpactFlash.Spawn(dustMaterial, transform.position + Random.insideUnitSphere * 0.4f + Vector3.up * 0.1f, 0.4f, 0.25f);
            }
            if (stateTime < curlTime) return;
            rollDirection = Target != null ? FlatDirectionTo(Target.position) : transform.forward;
            bounces = 0;
            rollHit = false;
            Enter(State.Roll);
        }

        private void UpdateRoll(float dt)
        {
            // Curve toward the player early in the roll, then commit, so a late sidestep works.
            if (Target != null && stateTime < rollTime * 0.45f)
            {
                Vector3 want = FlatDirectionTo(Target.position);
                rollDirection = Vector3.RotateTowards(rollDirection, want, rollHoming * Mathf.Deg2Rad * dt, 0f);
            }

            Vector3 from = transform.position;
            float step = rollSpeed * SpeedScale * dt;
            if (NavMesh.Raycast(from, from + rollDirection * (step + 0.5f), out NavMeshHit wall, NavMesh.AllAreas))
            {
                // Bounce off the wall (or wreckage) with a spark and a puff.
                bounces++;
                Vector3 normal = wall.normal;
                normal.y = 0f;
                if (normal.sqrMagnitude < 0.01f) normal = -rollDirection;
                rollDirection = Vector3.Reflect(rollDirection, normal.normalized);
                rollDirection.y = 0f;
                rollDirection.Normalize();
                if (sparkMaterial != null) ImpactFlash.Spawn(sparkMaterial, wall.position + Vector3.up * 0.4f, 0.5f, 0.12f);
                if (bounces > maxBounces) { Enter(State.Skid); return; }
            }
            if (AgentReady) Agent.Move(rollDirection * step);
            transform.rotation = Quaternion.LookRotation(rollDirection);

            if (TargetAlive && !rollHit && HitsPlayer())
            {
                rollHit = true;
                if (TargetHealth != null) TargetHealth.TakeDamage(rollDamage, gameObject);
                shoveDirection = rollDirection;
                shoveRemaining = playerShove;
                Enter(State.Recover);
                return;
            }
            if (dustMaterial != null && Time.time >= nextDust)
            {
                nextDust = Time.time + 0.07f;
                ImpactFlash.Spawn(dustMaterial, transform.position - rollDirection * 0.4f + Vector3.up * 0.1f, 0.35f, 0.3f);
            }
            if (stateTime >= rollTime) Enter(State.Skid);
        }

        private bool HitsPlayer()
        {
            Vector3 offset = Target.position - transform.position;
            float height = offset.y;
            offset.y = 0f;
            float reach = rollHitRadius + (TargetBody != null ? TargetBody.radius : 0.4f);
            return offset.magnitude <= reach && height > -1f && height < 1.2f;
        }

        private void UpdateSkid(float dt)
        {
            // Slides to a stop, then pops open dizzy.
            float k = 1f - Mathf.Clamp01(stateTime / Mathf.Max(0.01f, skidTime));
            if (AgentReady) Agent.Move(rollDirection * rollSpeed * 0.5f * k * k * dt);
            if (stateTime >= skidTime) Enter(State.Dizzy);
        }

        private void EndOpening()
        {
            nextRoll = Time.time + rollCooldown;
            LastSeen = Time.time;
            Enter(TargetAlive ? State.Approach : State.Idle);
        }

        // ---------- Hits ----------

        /// <summary>IBoltReflector: while curled, bolts glance off the hubcap.</summary>
        public bool TryReflect(Vector3 hitPoint, Vector3 incoming, Transform shooter, out Vector3 newDirection, out float damageMultiplier)
        {
            damageMultiplier = deflectDamageMultiplier;
            newDirection = -incoming;
            if (!Curled || IsDead) return false;
            Vector3 normal = hitPoint - (transform.position + Vector3.up * 0.45f);
            if (normal.sqrMagnitude < 0.0001f) normal = -incoming;
            newDirection = Vector3.Reflect(incoming.normalized, normal.normalized) + Vector3.up * 0.15f;
            if (sparkMaterial != null) ImpactFlash.Spawn(sparkMaterial, hitPoint, 0.25f, 0.08f);
            return true;
        }

        protected override void OnHit(float amount, GameObject source, Vector3 away)
        {
            bool vulnerable = state == State.Roll || state == State.Skid || state == State.Dizzy || state == State.Curl;
            if (amount >= flipThreshold && vulnerable)
            {
                if (sparkMaterial != null) ImpactFlash.Spawn(sparkMaterial, transform.position + Vector3.up * 0.5f, 0.6f, 0.12f);
                Enter(State.Flipped);
                return;
            }
            if (state == State.Idle) { LastSeen = Time.time; Enter(State.Approach); }
        }

        private void UpdateShove(float dt)
        {
            if (shoveRemaining <= 0f || TargetBody == null || !TargetBody.enabled) return;
            float step = Mathf.Min(shoveRemaining, 7f * dt);
            TargetBody.Move(shoveDirection * step);
            shoveRemaining -= step;
        }

        // ---------- Animation ----------

        private void Animate(float dt)
        {
            float wantCurl = Curled ? 1f : 0f;
            if (state == State.Curl) wantCurl = Mathf.Clamp01(stateTime / (curlTime * 0.5f));
            curl = Mathf.MoveTowards(curl, wantCurl, dt * 6f);

            for (int i = 0; i < limbs.Length; i++)
                if (limbs[i] != null) limbs[i].localScale = limbScales[i] * (1f - curl);

            float spinRate = state == State.Roll ? rollSpeed * 140f : state == State.Curl ? 900f * (stateTime / curlTime) :
                state == State.Skid ? rollSpeed * 140f * (1f - stateTime / Mathf.Max(0.01f, skidTime)) : 0f;
            spin = curl > 0.01f ? spin + spinRate * dt : Mathf.MoveTowards(spin % 360f, 0f, dt * 720f);
            if (shell != null) shell.localRotation = shellRest * Quaternion.Euler(spin, 0f, 0f);

            float speed = AgentReady ? Agent.velocity.magnitude : 0f;
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;
                float swing = state == State.Flipped
                    ? Mathf.Sin(Time.time * 22f + i * 1.7f) * 40f
                    : Mathf.Sin(Time.time * (6f + speed * 3f) + i * Mathf.PI * 0.5f) * Mathf.Clamp(speed * 10f, 0f, 30f);
                legs[i].localRotation = Quaternion.Euler(swing, 0f, 0f);
            }

            Quaternion pose = Quaternion.identity;
            Vector3 offset = Vector3.zero;
            if (state == State.Flipped)
            {
                pose = Quaternion.Euler(0f, 0f, 180f);
                offset = Vector3.up * 0.8f;
            }
            else if (state == State.Dizzy)
            {
                pose = Quaternion.Euler(Mathf.Sin(Time.time * 7f) * 8f, Mathf.Sin(Time.time * 3f) * 25f, Mathf.Sin(Time.time * 9f) * 12f);
            }
            Visual.localPosition = VisualRest + offset;
            Visual.localRotation = VisualRestRotation * pose;
        }
    }
}
