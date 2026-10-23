using SignalLost.Combat;
using SignalLost.Effects;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// Mini-boss: a spiky thorny-devil lizard with one of the ship's rocket engines strapped to its back.
    ///
    /// Phase 1:
    /// - Thruster dash: the nozzle glows and the lizard crouches for 1 s (it aims for the first 60%, then locks), then
    ///   rockets across the arena in a straight line, leaving a streak of fire. A dodged dash overshoots: it skids,
    ///   spins out and is dizzy for a moment (the opening). Dashing into a wall or mesa stuns it for longer.
    /// - Tail whip when you are close in front or beside it; exhaust blast (a cone of fire) when you are close behind it.
    ///
    /// Phase 2 (thruster cracked): shorter wind-ups, and it chains three sputtering dashes. Every follow-up dash
    /// re-targets the player: it swings round to where they are heading, leads them a little, curves gently toward them
    /// mid-dash, and banks off walls back toward them. After a chain the engine overheats: it stalls, venting smoke,
    /// and takes extra damage.
    ///
    /// Redline (last 15% of its health): the engine runs away with it. It never stops dashing again, with only a
    /// short re-aim between dashes, until it is defeated.
    ///
    /// Counterplay: watch the glow, sidestep once it locks, punish the skid; in phase 2 survive the chain and unload
    /// on it while it stalls; on redline, shoot it during the brief re-aims and keep moving. Don't stand in its fire.
    /// All numbers are placeholders.
    /// </summary>
    public sealed class Burnout : DesertMiniBoss
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private enum State { Stalk, DashWindup, Dash, Skid, Crashed, WhipWindup, Whip, BlastWindup, Blast, Recover, Stall }

        [Header("Stalking")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 3.6f;
        [SerializeField, Min(1f)] private float dashMinRange = 5f;
        [SerializeField, Min(2f)] private float dashMaxRange = 24f;

        [Header("Thruster dash")]
        [Tooltip("Glow and crouch before a dash: the player's warning.")]
        [SerializeField, Min(0.1f)] private float dashWindup = 1f;
        [SerializeField, Min(0.1f)] private float phaseTwoDashWindup = 0.7f;
        [Tooltip("Re-aim between dashes in a phase 2 chain.")]
        [SerializeField, Min(0.1f)] private float chainRewindup = 0.4f;
        [Tooltip("Part of the wind-up spent aiming; after this its direction is locked, so a late sidestep dodges.")]
        [SerializeField, Range(0f, 1f)] private float aimLockAt = 0.6f;
        [SerializeField, Min(1f)] private float dashSpeed = 24f;
        [SerializeField, Min(1f)] private float phaseTwoDashSpeed = 27f;
        [Tooltip("How far past the player a dash carries on.")]
        [SerializeField, Min(0f)] private float overshoot = 5f;
        [SerializeField, Min(2f)] private float maxDashDistance = 30f;
        [SerializeField, Min(0f)] private float dashDamage = 24f;
        [SerializeField, Min(0.3f)] private float dashHitRadius = 1.6f;
        [SerializeField, Min(0f)] private float dashShove = 2.5f;
        [SerializeField, Min(0f)] private float dashCooldown = 2.2f;
        [SerializeField, Min(0f)] private float phaseTwoDashCooldown = 1.4f;

        [Header("Openings")]
        [SerializeField, Min(0f)] private float skidTime = 0.5f;
        [Tooltip("Dizzy after a dodged dash: the player's opening.")]
        [SerializeField, Min(0f)] private float dizzyTime = 1.4f;
        [Tooltip("Stunned after dashing into a wall.")]
        [SerializeField, Min(0f)] private float crashStunTime = 2.2f;

        [Header("Phase 2: cracked thruster")]
        [SerializeField, Range(1, 5)] private int chainLength = 3;
        [Tooltip("Degrees per second a follow-up dash curves toward the player mid-dash (0: dead straight).")]
        [SerializeField, Min(0f)] private float chainSteer = 28f;
        [Tooltip("How far ahead of a moving player follow-up dashes aim, in seconds of the player's movement.")]
        [SerializeField, Min(0f)] private float chainLead = 0.35f;
        [Tooltip("Overheated after a chain: stalls, venting smoke.")]
        [SerializeField, Min(0f)] private float stallTime = 3f;
        [Tooltip("Damage taken while stalled, as a multiple of normal.")]
        [SerializeField, Min(1f)] private float stallDamageMultiplier = 1.6f;

        [Header("Redline (final stand)")]
        [Tooltip("At or below this fraction of its health it dashes non-stop until defeated.")]
        [SerializeField, Range(0f, 0.5f)] private float redlineAt = 0.15f;
        [Tooltip("Re-aim between redline dashes: the only pause it takes.")]
        [SerializeField, Min(0.1f)] private float redlineRewindup = 0.45f;
        [SerializeField, Min(1f)] private float redlineDashSpeed = 29f;
        [SerializeField, Min(0f)] private float redlineSteer = 40f;
        [SerializeField, Min(0f)] private float redlineDashDamage = 18f;
        [Tooltip("Its fire streak burns this long while redlining (shorter, so the arena doesn't fill up).")]
        [SerializeField, Min(0.5f)] private float redlineFireDuration = 2.4f;

        [Header("Tail whip")]
        [SerializeField, Min(0.5f)] private float whipRange = 3.4f;
        [SerializeField, Min(0.1f)] private float whipWindup = 0.55f;
        [SerializeField, Min(0.5f)] private float whipReach = 3.9f;
        [SerializeField, Min(0f)] private float whipDamage = 16f;
        [SerializeField, Min(0f)] private float whipShove = 2.2f;
        [SerializeField, Min(0f)] private float meleeCooldown = 1.6f;

        [Header("Exhaust blast (player behind it)")]
        [SerializeField, Min(0.1f)] private float blastWindup = 0.6f;
        [SerializeField, Min(1f)] private float blastLength = 6f;
        [SerializeField, Range(5f, 90f)] private float blastHalfAngle = 50f;
        [SerializeField, Min(0f)] private float blastDamage = 20f;
        [SerializeField, Min(0f)] private float blastShove = 3f;

        [Header("Fire")]
        [SerializeField] private FireStyle fire = new FireStyle { damage = 6f, interval = 0.5f, duration = 3.5f };
        [Tooltip("Metres between fire patches in the dash streak.")]
        [SerializeField, Min(0.5f)] private float streakSpacing = 2f;
        [SerializeField, Min(0.2f)] private float streakRadius = 1.1f;

        [Header("Rig")]
        [SerializeField] private Transform body;
        [SerializeField] private Transform head;
        [SerializeField] private Transform[] tail = new Transform[0];
        [SerializeField] private Transform[] legs = new Transform[0];
        [Tooltip("The engine on its back (tilts when it cracks).")]
        [SerializeField] private Transform thruster;
        [Tooltip("The exhaust flame behind the nozzle.")]
        [SerializeField] private Transform plume;
        [SerializeField] private Renderer[] nozzleRenderers = new Renderer[0];
        [SerializeField] private Material sparkMaterial;
        [SerializeField] private Material smokeMaterial;

        private State state = State.Stalk;
        private float stateTime, recoverFor, nextDash, nextMelee, nextRepath, nextPuff, glow;
        private bool dizzy, dashHit, swingHit;
        private int chainLeft;
        private Vector3 dashDirection, lastStreak;
        private float dashLength, dashTravelled, skidSpeed;
        private Vector3 plumeScale = Vector3.one;
        private Quaternion thrusterRest = Quaternion.identity, bodyRest = Quaternion.identity;
        private Quaternion[] tailRest = new Quaternion[0], legRest = new Quaternion[0];
        private Color[] nozzleColors = new Color[0];
        private MaterialPropertyBlock block;
        private bool cracked, redline, followUp;
        private FireEffect jet;
        private Renderer plumeRenderer;
        private float jetStrength;

        protected override void Awake()
        {
            base.Awake();
            block = new MaterialPropertyBlock();
            if (plume != null)
            {
                plumeScale = plume.localScale;
                // A particle exhaust jet replaces the placeholder plume blob; the blob's transform marks where it is.
                plumeRenderer = plume.GetComponent<Renderer>();
                Vector3 nozzle = plume.localPosition + plume.localRotation * new Vector3(0f, 0f, plumeScale.z);
                jet = FireEffect.SpawnJet(plume.parent, nozzle, plume.localRotation * Quaternion.Euler(0f, 180f, 0f),
                    Mathf.Max(0.08f, plumeScale.x * 0.7f), Mathf.Max(0.6f, plumeScale.z * 3f));
                jet.Strength = 0f;
            }
            if (thruster != null) thrusterRest = thruster.localRotation;
            if (body != null) bodyRest = body.localRotation;
            tailRest = Capture(tail);
            legRest = Capture(legs);
            nozzleColors = new Color[nozzleRenderers.Length];
            for (int i = 0; i < nozzleRenderers.Length; i++)
            {
                Material m = nozzleRenderers[i] != null ? nozzleRenderers[i].sharedMaterial : null;
                nozzleColors[i] = m != null && m.HasProperty(BaseColor) ? m.GetColor(BaseColor) : Color.gray;
            }
            SetPlume(0f);
        }

        private static Quaternion[] Capture(Transform[] items)
        {
            var rest = new Quaternion[items.Length];
            for (int i = 0; i < items.Length; i++) rest[i] = items[i] != null ? items[i].localRotation : Quaternion.identity;
            return rest;
        }

        private void Enter(State next)
        {
            state = next;
            stateTime = 0f;
            swingHit = false;
            if (next != State.Stalk) Halt();
            if (next == State.Stall)
            {
                DamageTakenMultiplier = stallDamageMultiplier;
                MiniBossHud.Message("BURNOUT OVERHEATED!  Hit it now!", new Color(1f, 0.6f, 0.3f), 1.8f);
            }
            else DamageTakenMultiplier = 1f;
        }

        private void RecoverFor(float seconds, bool isDizzy)
        {
            Enter(State.Recover);
            recoverFor = seconds;
            dizzy = isDizzy;
        }

        // ---------- Engagement ----------

        protected override void OnEngaged()
        {
            Enter(State.Stalk);
            nextDash = Time.time + 1f;
            nextMelee = Time.time;
        }

        protected override void TickCombat(float dt)
        {
            stateTime += dt;
            UpdateShove(dt);
            if (!TargetAlive) return;
            if (!redline && Phase == 2 && HealthFraction <= redlineAt && (state == State.Stalk || state == State.Stall ||
                state == State.Recover || state == State.Crashed || state == State.Blast)) StartRedline();
            switch (state)
            {
                case State.Stalk: UpdateStalk(); break;
                case State.DashWindup: UpdateDashWindup(dt); break;
                case State.Dash: UpdateDash(dt); break;
                case State.Skid: UpdateSkid(dt); break;
                case State.Crashed: if (stateTime >= crashStunTime) RecoverFor(0.4f, false); break;
                case State.WhipWindup: UpdateWhipWindup(dt); break;
                case State.Whip: UpdateWhip(); break;
                case State.BlastWindup: if (stateTime >= blastWindup) Blast(); break;
                case State.Blast: if (stateTime >= 0.35f) RecoverFor(0.6f, false); break;
                case State.Recover:
                    if (stateTime >= recoverFor)
                    {
                        dizzy = false;
                        Enter(State.Stalk);
                    }
                    break;
                case State.Stall:
                    if (stateTime >= stallTime)
                    {
                        nextDash = Time.time + phaseTwoDashCooldown;
                        Enter(State.Stalk);
                    }
                    break;
            }
        }

        private void UpdateStalk()
        {
            float distance = FlatDistance(Target.position);
            bool seen = CanSee(dashMaxRange + 6f);
            if (seen) LastSeen = Time.time;

            if (Time.time >= nextMelee && distance <= whipRange)
            {
                Enter(AngleToTarget() > 120f ? State.BlastWindup : State.WhipWindup);
                return;
            }
            if (Time.time >= nextDash && seen && distance >= dashMinRange && distance <= dashMaxRange)
            {
                chainLeft = Phase == 2 ? chainLength : 1;
                followUp = false;
                Enter(State.DashWindup);
                return;
            }

            if (Time.time < nextRepath) return;
            nextRepath = Time.time + 0.3f;
            if (distance > 10f) Chase(Target.position, walkSpeed);
            else if (distance < 4f && Time.time < nextMelee)
            {
                Vector3 away = transform.position - FlatDirectionTo(Target.position) * 4f;
                if (NavMesh.SamplePosition(away, out NavMeshHit hit, 2f, NavMesh.AllAreas)) Chase(hit.position, walkSpeed);
            }
            else
            {
                // Circle at mid range, sizing the player up.
                Vector3 side = Vector3.Cross(Vector3.up, FlatDirectionTo(Target.position)) * (Mathf.Sin(Time.time * 0.4f) > 0f ? 1f : -1f);
                Chase(transform.position + side * 3f, walkSpeed * 0.8f);
            }
        }

        // ---------- Dash ----------

        private float CurrentWindup =>
            redline ? redlineRewindup : Phase == 1 ? dashWindup : followUp ? chainRewindup : phaseTwoDashWindup;

        private float CurrentDashSpeed => (redline ? redlineDashSpeed : Phase == 2 ? phaseTwoDashSpeed : dashSpeed) * SpeedScale;

        /// <summary>Where a follow-up dash aims: where the player is heading, a moment from now.</summary>
        private Vector3 AimPoint()
        {
            if (!followUp && !redline) return Target.position;
            Vector3 velocity = TargetBody != null ? TargetBody.velocity : Vector3.zero;
            velocity.y = 0f;
            float travel = FlatDistance(Target.position) / Mathf.Max(1f, CurrentDashSpeed);
            return Target.position + velocity * Mathf.Min(chainLead + travel * 0.5f, 0.9f);
        }

        private void UpdateDashWindup(float dt)
        {
            float windup = CurrentWindup;
            // Follow-ups whip round to face the player, however far they have to turn; the first dash turns steadily.
            float turnRate = followUp || redline ? 900f : 420f;
            if (stateTime < windup * aimLockAt) TurnToward(AimPoint(), turnRate * dt);
            if (stateTime < windup) return;

            dashDirection = transform.forward;
            dashDirection.y = 0f;
            dashDirection.Normalize();
            dashLength = Mathf.Clamp(FlatDistance(Target.position) + overshoot, 6f, maxDashDistance);
            dashTravelled = 0f;
            dashHit = false;
            lastStreak = transform.position;
            Enter(State.Dash);
        }

        /// <summary>
        /// After hitting a wall, the direction to bank off it: toward the player if that leads away from the wall,
        /// otherwise the plain bounce turned as far toward them as the wall allows.
        /// </summary>
        private Vector3 BankDirection(Vector3 wallNormal)
        {
            Vector3 bounce = Vector3.Reflect(dashDirection, wallNormal);
            bounce.y = 0f;
            Vector3 toPlayer = FlatDirectionTo(AimPoint());
            Vector3 result = Vector3.Dot(toPlayer, wallNormal) > 0.15f ? toPlayer : Vector3.Slerp(bounce.normalized, toPlayer, 0.5f);
            result.y = 0f;
            if (Vector3.Dot(result, wallNormal) < 0.1f) result = bounce; // still into the wall: just bounce
            return result.sqrMagnitude > 0.0001f ? result.normalized : wallNormal;
        }

        private void UpdateDash(float dt)
        {
            float speed = CurrentDashSpeed;
            if (Phase == 2) speed *= 0.8f + 0.35f * Mathf.PerlinNoise(Time.time * 9f, 0.5f); // the cracked engine sputters
            Steer(dt);
            float step = speed * dt;
            Vector3 from = transform.position;

            if (NavMesh.Raycast(from, from + dashDirection * (step + 0.9f), out NavMeshHit wall, NavMesh.AllAreas))
            {
                if (sparkMaterial != null) ImpactFlash.Spawn(sparkMaterial, wall.position + Vector3.up * 0.6f, 0.9f, 0.15f);
                if (Phase == 1)
                {
                    Enter(State.Crashed);
                    MiniBossHud.Message("Burnout crashed!", new Color(1f, 0.8f, 0.4f), 1.2f);
                    return;
                }
                if (chainLeft > 1 || redline)
                {
                    // Bank off the wall back toward the player and keep going: counts as the next dash in the chain.
                    if (!redline) chainLeft--;
                    followUp = true;
                    Vector3 normal = wall.normal;
                    normal.y = 0f;
                    if (normal.sqrMagnitude < 0.01f) normal = -dashDirection;
                    dashDirection = BankDirection(normal.normalized);
                    dashTravelled = 0f;
                    dashLength = Mathf.Clamp(FlatDistance(Target.position) + overshoot, 6f, maxDashDistance);
                    dashHit = false;
                    ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, wall.position + Vector3.up * 0.6f, normal, 14, 0.12f, 7f, 60f);
                    return;
                }
                Enter(State.Stall);
                return;
            }

            if (AgentReady) Agent.Move(dashDirection * step);
            dashTravelled += step;
            transform.rotation = Quaternion.LookRotation(dashDirection);

            if (Vector3.Distance(transform.position, lastStreak) >= streakSpacing)
            {
                float burn = redline ? redlineFireDuration : Phase == 2 ? fire.duration + 1f : -1f;
                FireZone.Spawn(fire, transform.position, streakRadius, gameObject, transform, burn);
                lastStreak = transform.position;
            }

            if (!dashHit && TouchesPlayer(dashHitRadius))
            {
                dashHit = true;
                HurtPlayer(redline ? redlineDashDamage : dashDamage);
                Vector3 side = Vector3.Cross(Vector3.up, dashDirection);
                if (Vector3.Dot(side, Target.position - transform.position) < 0f) side = -side;
                ShovePlayer(side + dashDirection * 0.5f, dashShove);
            }

            if (dashTravelled < dashLength) return;
            if (Phase == 1)
            {
                skidSpeed = speed * 0.5f;
                Enter(State.Skid);
                return;
            }
            if (redline)
            {
                followUp = true;
                Enter(State.DashWindup);
                return;
            }
            chainLeft--;
            followUp = chainLeft > 0;
            if (chainLeft > 0) Enter(State.DashWindup);
            else Enter(State.Stall);
        }

        /// <summary>Follow-up dashes curve toward the player while they are still ahead of it.</summary>
        private void Steer(float dt)
        {
            float rate = redline ? redlineSteer : followUp ? chainSteer : 0f;
            if (rate <= 0f) return;
            Vector3 toPlayer = Target.position - transform.position;
            toPlayer.y = 0f;
            // Only while the player is in front and not yet passed: a sidestep at the last moment still works.
            if (toPlayer.magnitude < 2.5f || Vector3.Angle(dashDirection, toPlayer) > 75f) return;
            dashDirection = Vector3.RotateTowards(dashDirection, toPlayer.normalized, rate * Mathf.Deg2Rad * dt, 0f);
            dashDirection.y = 0f;
            dashDirection.Normalize();
        }

        private void StartRedline()
        {
            redline = true;
            followUp = true;
            DamageTakenMultiplier = 1f;
            MiniBossHud.Message("BURNOUT IS REDLINING!  It won't stop now!", new Color(1f, 0.35f, 0.15f), 2.2f);
            Vector3 nozzle = thruster != null ? thruster.position : transform.position + Vector3.up;
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, nozzle, Vector3.up, 30, 0.12f, 8f, 70f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, nozzle, Vector3.up, 8, 0.8f, 2f, 40f);
            Enter(State.DashWindup);
        }

        private void UpdateSkid(float dt)
        {
            float k = 1f - Mathf.Clamp01(stateTime / Mathf.Max(0.01f, skidTime));
            if (AgentReady) Agent.Move(dashDirection * skidSpeed * k * dt);
            transform.Rotate(0f, 720f * k * dt, 0f); // spins out
            if (stateTime >= skidTime)
            {
                nextDash = Time.time + dashCooldown + dizzyTime;
                RecoverFor(dizzyTime, true);
            }
        }

        private bool TouchesPlayer(float radius)
        {
            Vector3 offset = Target.position - transform.position;
            float height = offset.y;
            offset.y = 0f;
            return offset.magnitude <= radius + (TargetBody != null ? TargetBody.radius : 0.4f) && height > -1.5f && height < 2f;
        }

        // ---------- Close range ----------

        private void UpdateWhipWindup(float dt)
        {
            if (stateTime < whipWindup * 0.6f) TurnToward(Target.position, 200f * dt);
            if (stateTime >= whipWindup) Enter(State.Whip);
        }

        private void UpdateWhip()
        {
            transform.Rotate(0f, 1100f * Time.deltaTime, 0f); // the full-body spin swings the tail round
            if (!swingHit && stateTime >= 0.12f)
            {
                swingHit = true;
                if (FlatDistance(Target.position) <= whipReach && HurtPlayer(whipDamage))
                    ShovePlayer(FlatDirectionTo(Target.position), whipShove);
            }
            if (stateTime >= 0.33f)
            {
                nextMelee = Time.time + meleeCooldown;
                RecoverFor(0.5f, false);
            }
        }

        private void Blast()
        {
            Vector3 back = -transform.forward;
            back.y = 0f;
            back.Normalize();
            Vector3 toPlayer = Target.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.magnitude <= blastLength && Vector3.Angle(back, toPlayer) <= blastHalfAngle && HurtPlayer(blastDamage))
                ShovePlayer(back, blastShove);
            FireZone.Spawn(fire, transform.position + back * 2.6f, 1.4f, gameObject, transform);
            FireZone.Spawn(fire, transform.position + back * 4.4f, 1.1f, gameObject, transform);
            Vector3 nozzle = thruster != null ? thruster.position : transform.position + Vector3.up;
            ParticleBurst.Spawn(ParticleBurst.Kind.Fireball, nozzle + back * 0.8f, back, 26, 1.1f, blastLength * 1.6f, blastHalfAngle * 0.6f);
            nextMelee = Time.time + meleeCooldown;
            Enter(State.Blast);
        }

        // ---------- Phase 2, reset, death ----------

        protected override void OnPhaseTwo()
        {
            Enter(State.Stalk);
            cracked = true;
            if (thruster != null) thruster.localRotation = thrusterRest * Quaternion.Euler(0f, 0f, 14f);
        }

        protected override void UpdatePhaseShift(float dt)
        {
            // The engine coughs and sparks as it cracks.
            if (Time.time >= nextPuff)
            {
                nextPuff = Time.time + 0.12f;
                Vector3 nozzle = thruster != null ? thruster.position : transform.position + Vector3.up;
                if (sparkMaterial != null) ImpactFlash.Spawn(sparkMaterial, nozzle + Random.insideUnitSphere * 0.4f, 0.3f, 0.1f);
                if (smokeMaterial != null) ImpactFlash.Spawn(smokeMaterial, nozzle + Vector3.up * 0.5f + Random.insideUnitSphere * 0.3f, 0.8f, 0.5f);
            }
        }

        protected override void OnPhaseShiftEnded()
        {
            Enter(State.Stalk);
            nextDash = Time.time + 0.5f;
            if (HealthFraction <= redlineAt) StartRedline();
            else MiniBossHud.Message("The thruster is sputtering: it chains dashes now!", new Color(1f, 0.6f, 0.3f), 2f);
        }

        protected override void OnReset()
        {
            Enter(State.Stalk);
            cracked = false;
            redline = false;
            followUp = false;
            dizzy = false;
            if (thruster != null) thruster.localRotation = thrusterRest;
            SetPlume(0f);
        }

        protected override void OnBossDeath()
        {
            redline = false;
            SetPlume(0f);
            if (jet != null) jet.Extinguish();
        }

        // ---------- Animation ----------

        protected override void AnimateBoss(float dt)
        {
            float speed = AgentReady ? Agent.velocity.magnitude : 0f;
            bool dashing = state == State.Dash || state == State.Skid;
            float windup = state == State.DashWindup ? Mathf.Clamp01(stateTime / CurrentWindup) : 0f;

            // Nozzle glow: builds through the wind-up, white-hot while dashing, red while stalled.
            float wantGlow = !Engaged ? 0f : redline ? 0.8f + 0.2f * Mathf.Sin(Time.time * 18f) : state == State.DashWindup ? windup : dashing ? 1f :
                state == State.BlastWindup ? Mathf.Clamp01(stateTime / blastWindup) : state == State.Stall ? 0.55f : 0f;
            glow = Mathf.MoveTowards(glow, wantGlow, dt * 4f);

            // Exhaust plume.
            float thrust = state == State.Dash ? 1f + 0.25f * Mathf.Sin(Time.time * 40f) :
                state == State.Blast ? 2.2f : state == State.DashWindup && (windup > 0.6f || redline) ? 0.35f + 0.25f * Random.value : 0f;
            if (cracked && state == State.Dash && Random.value < 0.15f) thrust = 0f; // sputtering
            SetPlume(thrust);

            // Body: crouch in the wind-up, shake, wobble when dizzy, slump when stalled.
            Vector3 offset = Vector3.zero;
            Quaternion lean = Quaternion.identity;
            if (state == State.DashWindup) { offset = Vector3.down * 0.12f * windup + Random.insideUnitSphere * 0.02f * windup; lean = Quaternion.Euler(8f * windup, 0f, 0f); }
            else if (state == State.WhipWindup) lean = Quaternion.Euler(0f, -25f * Mathf.Clamp01(stateTime / whipWindup), 0f);
            else if (state == State.Recover && dizzy) lean = Quaternion.Euler(Mathf.Sin(Time.time * 6f) * 6f, 0f, Mathf.Sin(Time.time * 9f) * 14f);
            else if (state == State.Crashed) lean = Quaternion.Euler(0f, 0f, 35f + Mathf.Sin(Time.time * 12f) * 6f);
            else if (state == State.Stall) { offset = Vector3.down * 0.15f; lean = Quaternion.Euler(6f, 0f, Mathf.Sin(Time.time * 3f) * 4f); }
            Visual.localPosition = VisualRest + offset;
            Visual.localRotation = VisualRestRotation * lean;

            // Legs scramble while dashing, walk while moving.
            float rate = dashing ? 26f : 4f + speed * 3f;
            float amount = dashing ? 40f : Mathf.Clamp(speed * 9f, 0f, 30f);
            for (int i = 0; i < legs.Length; i++)
                if (legs[i] != null)
                    legs[i].localRotation = legRest[i] * Quaternion.Euler(Mathf.Sin(Time.time * rate + (i % 2 == 0 ? 0f : Mathf.PI) + (i < 2 ? 0f : Mathf.PI)) * amount, 0f, 0f);

            // Tail sways; whips round in the spin; stiff while dashing.
            for (int i = 0; i < tail.Length; i++)
            {
                if (tail[i] == null) continue;
                float sway = state == State.Whip ? 25f : dashing ? 2f : 10f;
                tail[i].localRotation = tailRest[i] * Quaternion.Euler(0f, Mathf.Sin(Time.time * (state == State.Whip ? 20f : 2.5f) - i * 0.8f) * sway, 0f);
            }
            if (head != null) head.localRotation = Quaternion.Euler(state == State.DashWindup ? -10f * windup : Mathf.Sin(Time.time * 1.5f) * 4f, 0f, 0f);

            // Smoke while stalled or cracked.
            if ((state == State.Stall || redline || (cracked && Random.value < 0.02f)) && Time.time >= nextPuff && !InPhaseShift)
            {
                nextPuff = Time.time + (state == State.Stall ? 0.12f : redline ? 0.18f : 0.4f);
                Vector3 nozzle = thruster != null ? thruster.position : transform.position + Vector3.up;
                ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, nozzle + Vector3.up * 0.4f + Random.insideUnitSphere * 0.2f, Vector3.up,
                    redline ? 2 : 3, 0.6f, 1.2f, 25f);
                if (redline && Random.value < 0.5f)
                    ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, nozzle, Random.onUnitSphere + Vector3.up, 5, 0.08f, 5f, 40f);
            }
        }

        private void SetPlume(float thrust)
        {
            if (jet != null)
            {
                // The particle jet: the old blob stays hidden; strength eases so the flame breathes rather than pops.
                if (plumeRenderer != null) plumeRenderer.enabled = false;
                jetStrength = Mathf.MoveTowards(jetStrength, Mathf.Clamp01(thrust * 0.8f), Time.deltaTime * 8f);
                jet.Strength = jetStrength;
                jet.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.6f, Mathf.Clamp01((thrust - 1f) / 1.2f));
                return;
            }
            if (plume == null) return;
            bool on = thrust > 0.01f;
            plume.gameObject.SetActive(on);
            if (on) plume.localScale = new Vector3(plumeScale.x * Mathf.Sqrt(thrust), plumeScale.y * Mathf.Sqrt(thrust), plumeScale.z * thrust);
        }

        protected override void Paint()
        {
            for (int i = 0; i < nozzleRenderers.Length; i++)
            {
                Renderer item = nozzleRenderers[i];
                if (item == null) continue;
                Color baseColor = cracked ? nozzleColors[i] * 0.45f : nozzleColors[i];
                baseColor.a = 1f;
                Color hot = glow > 0.85f ? Color.Lerp(new Color(1f, 0.45f, 0.1f), new Color(1f, 0.95f, 0.8f), (glow - 0.85f) / 0.15f) : new Color(1f, 0.45f, 0.1f);
                item.GetPropertyBlock(block);
                block.SetColor(BaseColor, Color.Lerp(baseColor, hot, glow));
                item.SetPropertyBlock(block);
            }
        }
    }
}
