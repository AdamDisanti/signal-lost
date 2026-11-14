using SignalLost.Enemies.Desert;
using UnityEngine;

namespace SignalLost.Enemies.Caves
{
    /// <summary>
    /// Crystal Caves fodder: a stout, grumpy porcupine whose quills are glowing cyan and purple crystal spikes. A
    /// ranged enemy that stays in the open: it keeps 4-10 m from the player and fires a fan of three quills.
    ///
    /// The wind-up (it stops, raises its back, the quills glow brighter) is the warning; the aim follows the player
    /// for the first 75% and then locks, so a sidestep dodges. Quills are Briarwing's <see cref="ThornShot"/>: a miss
    /// sticks in the wall or floor for a few seconds and stings anyone who brushes it.
    ///
    /// Get within 2 m and it bristles for a moment: touching it hurts. Aim a charged laser shot at it and it curls
    /// into a quill ball: a full charged bolt then takes only half its health (smaller hits scale down with it), then pops up and fires straight away.
    ///
    /// Builds on <see cref="DesertFodder"/> like every other small enemy (territory, leash and heal, hit flash,
    /// combo stun, death). All numbers are placeholders.
    /// </summary>
    public sealed class QuillPorcupine : DesertFodder
    {
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private enum State { Idle, Keep, WindUp, Recover, Bristle, Curl }

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 2.6f;
        [Tooltip("Farther than this (or out of sight), it closes in.")]
        [SerializeField, Min(1f)] private float maxRange = 10f;
        [Tooltip("Closer than this, it backs off.")]
        [SerializeField, Min(0.5f)] private float tooCloseRange = 4f;

        [Header("Quill fan")]
        [SerializeField, Min(1f)] private float attackRange = 14f;
        [Tooltip("The warning before the fan: back raised, quills glowing brighter.")]
        [SerializeField, Min(0.1f)] private float windUpTime = 0.7f;
        [Tooltip("Fraction of the wind-up after which the aim stops following the player.")]
        [SerializeField, Range(0f, 1f)] private float aimLockAt = 0.75f;
        [SerializeField, Min(1)] private int quillsPerFan = 3;
        [Tooltip("Degrees between neighbouring quills in the fan.")]
        [SerializeField, Range(0f, 45f)] private float fanSpread = 14f;
        [SerializeField, Min(1f)] private float quillSpeed = 18f;
        [SerializeField, Min(0f)] private float quillDamage = 8f;
        [Tooltip("Seconds a missed quill stays stuck in the scenery (it stings for 40% of the quill damage).")]
        [SerializeField, Min(0.5f)] private float stuckQuillLife = 4f;
        [SerializeField, Min(0f)] private float attackCooldown = 3f;

        [Header("Bristle")]
        [Tooltip("It bristles when the player gets this close.")]
        [SerializeField, Min(0.5f)] private float bristleRange = 2f;
        [SerializeField, Min(0.1f)] private float bristleTime = 1.5f;
        [SerializeField, Min(0f)] private float bristleCooldown = 3f;
        [SerializeField, Min(0f)] private float thornDamage = 5f;
        [Tooltip("Player body within this distance of its body counts as touching.")]
        [SerializeField, Min(0.1f)] private float thornReach = 0.8f;
        [SerializeField, Min(0.1f)] private float thornInterval = 0.5f;

        [Header("Curl")]
        [Tooltip("Share of its max health one full charged bolt takes while curled; every other hit scales with it (0.5: two full shots).")]
        [SerializeField, Range(0.05f, 1f)] private float curledFullChargeShare = 0.5f;
        [SerializeField, Min(0f)] private float minCurlTime = 0.5f;
        [Tooltip("Stays curled this long after you stop aiming, so the bolt you just released still meets the ball.")]
        [SerializeField, Min(0f)] private float uncurlDelay = 0.6f;

        [Header("Rig")]
        [Tooltip("Parent of the back quills; it swells for the wind-up, bristle and curl.")]
        [SerializeField] private Transform quillRoot;
        [SerializeField] private Renderer[] quills = new Renderer[0];
        [Tooltip("Where fired quills start.")]
        [SerializeField] private Transform launch;
        [Tooltip("The fired quill (a +Z spike, scaled by ThornShot).")]
        [SerializeField] private Mesh quillShotMesh;
        [Tooltip("Fired quills take these in turn (cyan, purple).")]
        [SerializeField] private Material[] quillMaterials = new Material[0];
        [Tooltip("Its quills after it dies (dull).")]
        [SerializeField] private Material spentMaterial;
        [SerializeField] private Light glow;
        [SerializeField] private Transform head;
        [SerializeField] private Transform[] legs = new Transform[0];

        private State state = State.Idle;
        private float stateTime, nextAttack, nextBristle, nextThorn, nextRepath, nextWander;
        private float spread, curl, glowIntensity, lastAimed;
        private Vector3 aim, quillRest, headRest;
        private MaterialPropertyBlock quillBlock;

        protected override void Awake()
        {
            base.Awake();
            quillBlock = new MaterialPropertyBlock();
            if (quillRoot != null) quillRest = quillRoot.localScale;
            if (head != null) headRest = head.localPosition;
            if (glow != null) glowIntensity = glow.intensity;
            Health.DamageModifier = (amount, _) => state == State.Curl ? CurledDamage(amount) : amount;
        }

        // Curled, a full charged bolt (whatever upgrades make it) takes curledFullChargeShare of its max health.
        private float CurledDamage(float amount)
        {
            float fullCharge = TargetLaser != null ? TargetLaser.MaxShotDamage : 70f;
            return amount / Mathf.Max(1f, fullCharge) * Health.Max * curledFullChargeShare;
        }

        private void Enter(State next)
        {
            state = next;
            stateTime = 0f;
            if (next == State.Curl) lastAimed = Time.time;
        }

        private Vector3 TargetChest => Target.position + Vector3.up * 1.1f;
        private Vector3 LaunchPoint => launch != null ? launch.position : transform.position + Vector3.up * 0.75f;

        // The player is holding a laser charge and looking straight at it (the Jug Possum's duck check).
        private bool AimedAt => TargetLaser != null && TargetLaser.IsCharging && TargetView != null &&
                                Vector3.Dot(TargetView.forward, (transform.position + Vector3.up * 0.4f - TargetView.position).normalized) > 0.97f;

        protected override void Tick(float dt)
        {
            stateTime += dt;
            switch (state)
            {
                case State.Idle: UpdateIdle(); break;
                case State.Keep: UpdateKeep(); break;
                case State.WindUp: UpdateWindUp(); break;
                case State.Recover:
                    Halt();
                    if (AimedAt) { Enter(State.Curl); break; }
                    if (TargetAlive) TurnToward(Target.position, 240f * dt);
                    if (stateTime >= 0.4f) Enter(State.Keep);
                    break;
                case State.Bristle:
                    if (AimedAt) { Enter(State.Curl); break; }
                    UpdateBristle(dt);
                    break;
                case State.Curl:
                    Halt();
                    if (AimedAt) lastAimed = Time.time;
                    if (stateTime >= minCurlTime && Time.time - lastAimed >= uncurlDelay)
                    {
                        nextAttack = Mathf.Min(nextAttack, Time.time + 0.3f); // pops up and fires straight away
                        Enter(State.Keep);
                    }
                    break;
            }
            Animate(dt);
        }

        private void UpdateIdle()
        {
            if (TargetAlive && (Notices() || Time.time - LastSeen < 4f))
            {
                LastSeen = Time.time;
                Enter(State.Keep);
                return;
            }
            // Waddles about near its spot.
            if (Time.time < nextWander) return;
            nextWander = Time.time + Random.Range(3f, 5f);
            Vector2 step = Random.insideUnitCircle * 2f;
            Chase(Home + new Vector3(step.x, 0f, step.y), moveSpeed * 0.5f);
        }

        private void UpdateKeep()
        {
            if (!TargetAlive) { Halt(); Enter(State.Idle); return; }
            if (Notices()) LastSeen = Time.time;
            if (Time.time - LastSeen > 4f || GaveUp()) { Enter(State.Idle); return; }
            if (AimedAt) { Halt(); Enter(State.Curl); return; }

            float distance = FlatDistance(Target.position);
            if (distance < bristleRange && Time.time >= nextBristle) { Halt(); Enter(State.Bristle); return; }

            bool visible = CanSee(attackRange);
            if (visible) LastSeen = Time.time;
            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + 0.25f;
                if (!visible || distance > maxRange) Chase(Target.position, moveSpeed);
                else if (distance < tooCloseRange) Chase(transform.position - FlatDirectionTo(Target.position) * 3f, moveSpeed);
                else Halt();
            }
            if (visible) TurnToward(Target.position, 300f * Time.deltaTime);

            if (visible && distance <= attackRange && Time.time >= nextAttack &&
                Vector3.Angle(transform.forward, FlatDirectionTo(Target.position)) < 30f)
            {
                Halt();
                Enter(State.WindUp);
            }
        }

        private void UpdateWindUp()
        {
            Halt();
            if (!TargetAlive) { Enter(State.Keep); return; }
            float k = stateTime / windUpTime;
            if (AimedAt && k < 0.4f) { Enter(State.Curl); return; }
            if (k < aimLockAt)
            {
                TurnToward(Target.position, 360f * Time.deltaTime);
                aim = (TargetChest - LaunchPoint).normalized;
            }
            if (stateTime < windUpTime) return;
            FireFan();
            nextAttack = Time.time + attackCooldown;
            Enter(State.Recover);
        }

        private void FireFan()
        {
            Vector3 from = LaunchPoint;
            for (int i = 0; i < quillsPerFan; i++)
            {
                float yaw = (i - (quillsPerFan - 1) * 0.5f) * fanSpread;
                Vector3 direction = Quaternion.AngleAxis(yaw, Vector3.up) * aim;
                Material material = quillMaterials.Length > 0 ? quillMaterials[i % quillMaterials.Length] : null;
                ThornShot.Fire(quillShotMesh, material, from + direction * 0.3f, direction, quillSpeed, quillDamage, gameObject, stuckQuillLife);
            }
        }

        private void UpdateBristle(float dt)
        {
            Halt();
            if (TargetAlive) TurnToward(Target.position, 240f * dt);
            if (Time.time >= nextThorn && BossFx.DistanceToPlayer(transform.position + Vector3.up * 0.4f) < thornReach)
            {
                BossFx.HurtPlayer(thornDamage, gameObject);
                nextThorn = Time.time + thornInterval;
            }
            if (stateTime < bristleTime) return;
            nextBristle = Time.time + bristleCooldown;
            Enter(State.Keep);
        }

        protected override void OnHit(float amount, GameObject source, Vector3 away)
        {
            if (state == State.Idle) Enter(State.Keep); // a hit draws it in
            if (state == State.Keep) nextRepath = 0f;
        }

        protected override void OnDeath()
        {
            // The quills go dull and the glow goes out.
            if (spentMaterial != null)
                foreach (Renderer quill in quills)
                    if (quill != null) quill.sharedMaterial = spentMaterial;
            if (glow != null) glow.enabled = false;
        }

        // ---------- Look ----------

        protected override void Paint()
        {
            // Quills glow brighter through the wind-up (the warning) and flicker while it bristles.
            float boost = state == State.WindUp ? 2.5f * Mathf.Clamp01(stateTime / windUpTime)
                : state == State.Bristle ? 1.2f * (0.6f + 0.4f * Mathf.Sin(Time.time * 30f)) : 0f;
            foreach (Renderer quill in quills)
            {
                if (quill == null) continue;
                if (boost <= 0f || !quill.sharedMaterial.HasProperty(EmissionColor))
                {
                    quill.SetPropertyBlock(null);
                    continue;
                }
                quill.GetPropertyBlock(quillBlock);
                quillBlock.SetColor(EmissionColor, quill.sharedMaterial.GetColor(EmissionColor) * (1f + boost));
                quill.SetPropertyBlock(quillBlock);
            }
            if (glow != null && glow.enabled) glow.intensity = glowIntensity * (1f + boost * 0.6f);
        }

        private void Animate(float dt)
        {
            float speed = AgentReady ? Agent.velocity.magnitude : 0f;

            // Short-legged waddle: legs swing in diagonal pairs, the body rocks side to side.
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;
                float phase = (i == 0 || i == 3) ? 0f : Mathf.PI;
                float swing = Mathf.Sin(Time.time * (5f + speed * 4f) + phase) * Mathf.Clamp(speed * 18f, 0f, 30f);
                legs[i].localRotation = Quaternion.Euler(swing * (1f - curl), 0f, 0f);
            }

            float wantSpread = state switch
            {
                State.WindUp => 0.25f * Mathf.Clamp01(stateTime / windUpTime),
                State.Bristle => 0.4f,
                State.Curl => 0.2f,
                _ => 0f,
            };
            spread = Mathf.MoveTowards(spread, wantSpread, 3f * dt);
            curl = Mathf.MoveTowards(curl, state == State.Curl ? 1f : 0f, 5f * dt);

            float rock = Mathf.Sin(Time.time * (5f + speed * 4f)) * Mathf.Clamp01(speed) * 5f;
            float raise = state == State.WindUp ? 8f * Mathf.Clamp01(stateTime / windUpTime) : 0f;
            Visual.localPosition = VisualRest + Vector3.down * (0.12f * curl);
            Visual.localRotation = VisualRestRotation * Quaternion.Euler(raise + 30f * curl, 0f, rock);

            if (quillRoot != null)
            {
                Vector3 shake = state == State.Bristle ? Random.insideUnitSphere * 0.04f : Vector3.zero;
                quillRoot.localScale = quillRest * (1f + spread) + shake;
            }
            if (head != null) head.localPosition = headRest + new Vector3(0f, -0.12f, -0.14f) * curl;
        }
    }
}
