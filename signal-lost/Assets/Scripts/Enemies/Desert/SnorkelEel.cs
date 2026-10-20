using SignalLost.Combat;
using UnityEngine;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// Burrowing fodder: a sand eel breathing through a bent exhaust pipe from the ship. It swims under the sand
    /// toward the player with only the pipe showing, leaving a moving wake, then the sand bulges (the telegraph)
    /// and it bursts up to bite, stays exposed for a moment, and dives again. The scanner can track it while it is
    /// buried (it carries an EnemySignalSource), which is the design doc's buried-enemy idea.
    ///
    /// Counterplay: while it is under the sand only the pipe can be hit. Shoot or strike the pipe and the eel is
    /// yanked out and left stunned on the surface. Otherwise dodge the burst (step out of the bulge) and hit it
    /// while it is exposed. All numbers are placeholders.
    /// </summary>
    public sealed class SnorkelEel : DesertFodder
    {
        private enum State { Idle, Burrowed, Surfacing, Exposed, Diving, Stunned }

        [Header("Under the sand")]
        [SerializeField, Min(0.1f)] private float swimSpeed = 4.4f;
        [Tooltip("Starts surfacing when this close to the player.")]
        [SerializeField, Min(0.5f)] private float surfaceRange = 2.4f;
        [Tooltip("The bulge before it bursts out: the player's warning.")]
        [SerializeField, Min(0.1f)] private float surfaceTime = 0.7f;
        [Tooltip("Pause after diving before it can surface again.")]
        [SerializeField, Min(0f)] private float surfaceCooldown = 1.4f;
        [Tooltip("How deep the body hides; the pipe still sticks out.")]
        [SerializeField, Min(0.2f)] private float burrowDepth = 1.05f;

        [Header("Bite")]
        [SerializeField, Min(0f)] private float biteDamage = 14f;
        [SerializeField, Min(0.5f)] private float biteRange = 1.9f;
        [SerializeField, Min(0.1f)] private float exposedTime = 1.4f;
        [SerializeField, Min(0.1f)] private float diveTime = 0.45f;

        [Header("Snorkel weak spot")]
        [Tooltip("Health on the pipe's own collider. A hit while buried yanks the eel out, stunned.")]
        [SerializeField] private Health pipeHealth;
        [SerializeField, Min(0.1f)] private float stunTime = 2.4f;

        [Header("Rig")]
        [SerializeField] private Transform head;
        [SerializeField] private Transform jaw;
        [SerializeField] private Transform[] segments = new Transform[0];
        [Tooltip("The sand mound that travels with it while buried. Not under Visual, so it stays at ground level.")]
        [SerializeField] private Transform wake;
        [SerializeField] private Collider bodyCollider;
        [SerializeField] private Material sandMaterial;

        private State state = State.Idle;
        private float stateTime, nextSurface, nextRepath, nextPuff;
        private float depth; // 0 = fully out, 1 = fully buried
        private bool bitThisBurst;
        private int lastBodyHitFrame = -1;
        private float pendingPipeDamage;
        private GameObject pendingPipeSource;
        private Knockback pendingPipeKnockback;
        private Vector3 pendingPipeDirection;
        private Vector3 wakeRest;

        protected override void Awake()
        {
            base.Awake();
            if (wake != null) wakeRest = wake.localScale;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (pipeHealth != null) pipeHealth.Damaged += OnPipeHit;
            Health.Damaged += NoteBodyHit;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (pipeHealth != null) pipeHealth.Damaged -= OnPipeHit;
            Health.Damaged -= NoteBodyHit;
        }

        protected override void OnStarted()
        {
            depth = 1f;
            Enter(State.Idle);
        }

        private void Enter(State next)
        {
            state = next;
            stateTime = 0f;
            bool buried = next == State.Idle || next == State.Burrowed || next == State.Surfacing;
            // Underground only the pipe can be hit; the body collider is off so the player can walk over it.
            if (bodyCollider != null) bodyCollider.enabled = !buried && !IsDead;
            if (next == State.Surfacing || next == State.Exposed || next == State.Stunned || next == State.Diving) Halt();
            if (next == State.Surfacing) bitThisBurst = false;
        }

        protected override void Tick(float dt)
        {
            stateTime += dt;
            switch (state)
            {
                case State.Idle: UpdateIdle(); break;
                case State.Burrowed: UpdateBurrowed(); break;
                case State.Surfacing: UpdateSurfacing(); break;
                case State.Exposed: UpdateExposed(); break;
                case State.Diving: UpdateDiving(); break;
                case State.Stunned: UpdateStunned(); break;
            }
            Animate(dt);
        }

        // ---------- States ----------

        private void UpdateIdle()
        {
            depth = Mathf.MoveTowards(depth, 1f, Time.deltaTime * 3f);
            // It feels footsteps through the sand: no line of sight needed.
            if (TargetAlive && (FlatDistance(Target.position) <= detectRange || (Alerted && FlatDistance(Target.position) <= alertedDetectRange)))
            {
                LastSeen = Time.time;
                Enter(State.Burrowed);
            }
        }

        private void UpdateBurrowed()
        {
            depth = Mathf.MoveTowards(depth, 1f, Time.deltaTime * 3f);
            if (!TargetAlive) { Enter(State.Idle); return; }
            float distance = FlatDistance(Target.position);
            if (distance > loseRange) { Enter(State.Idle); return; }

            if (Time.time >= nextRepath)
            {
                Chase(Target.position, swimSpeed);
                nextRepath = Time.time + 0.2f;
            }
            if (distance <= surfaceRange && Time.time >= nextSurface) Enter(State.Surfacing);
        }

        private void UpdateSurfacing()
        {
            // The bulge grows and shakes; the eel keeps its spot, so stepping away dodges it.
            depth = Mathf.Lerp(1f, 0.8f, stateTime / surfaceTime);
            if (Target != null) TurnToward(Target.position, 360f * Time.deltaTime);
            if (stateTime < surfaceTime) return;

            Burst();
            Enter(State.Exposed);
        }

        private void Burst()
        {
            if (sandMaterial != null)
            {
                ImpactFlash.Spawn(sandMaterial, transform.position + Vector3.up * 0.3f, 1.4f, 0.25f);
                ExpandingRing.Spawn(sandMaterial, transform.position + Vector3.up * 0.05f, 1.8f, 0.3f, 0.12f);
            }
            if (bitThisBurst || !TargetAlive) return;
            bitThisBurst = true;
            if (FlatDistance(Target.position) <= biteRange && TargetHealth != null) TargetHealth.TakeDamage(biteDamage, gameObject);
        }

        private void UpdateExposed()
        {
            depth = Mathf.MoveTowards(depth, 0f, Time.deltaTime * 9f);
            if (Target != null) TurnToward(Target.position, 120f * Time.deltaTime);
            if (stateTime >= exposedTime) Enter(State.Diving);
        }

        private void UpdateDiving()
        {
            depth = Mathf.Clamp01(stateTime / diveTime);
            if (stateTime < diveTime) return;
            if (sandMaterial != null) ImpactFlash.Spawn(sandMaterial, transform.position + Vector3.up * 0.2f, 0.9f, 0.2f);
            nextSurface = Time.time + surfaceCooldown;
            Enter(TargetAlive ? State.Burrowed : State.Idle);
        }

        private void UpdateStunned()
        {
            depth = Mathf.MoveTowards(depth, 0.15f, Time.deltaTime * 8f);
            if (stateTime >= stunTime) Enter(State.Diving);
        }

        // ---------- Hits ----------

        private void NoteBodyHit(Health _, float amount, GameObject source) => lastBodyHitFrame = Time.frameCount;

        /// <summary>The snorkel was hit. Buried: yank it out, stunned. Above ground: the eel takes the hit.</summary>
        private void OnPipeHit(Health pipe, float amount, GameObject source)
        {
            pipe.Restore(); // the pipe itself never breaks
            if (IsDead || Health.IsDead) return;
            if (state == State.Idle || state == State.Burrowed || state == State.Surfacing)
            {
                if (sandMaterial != null) ImpactFlash.Spawn(sandMaterial, transform.position + Vector3.up * 0.4f, 1.2f, 0.25f);
                LastSeen = Time.time;
                Enter(State.Stunned);
                Health.TakeDamage(amount * 0.5f, source, pipe.HitKnockback, pipe.HitDirection);
                return;
            }
            // Exposed: the pipe is part of its head. Applied in LateUpdate and dropped if the same swing hit the body.
            if (amount > pendingPipeDamage)
            {
                pendingPipeDamage = amount; pendingPipeSource = source;
                pendingPipeKnockback = pipe.HitKnockback; pendingPipeDirection = pipe.HitDirection;
            }
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (pendingPipeDamage <= 0f) return;
            float amount = pendingPipeDamage;
            GameObject source = pendingPipeSource;
            pendingPipeDamage = 0f;
            pendingPipeSource = null;
            if (lastBodyHitFrame == Time.frameCount || IsDead) return;
            Health.TakeDamage(amount, source, pendingPipeKnockback, pendingPipeDirection);
        }

        protected override void OnHit(float amount, GameObject source, Vector3 away)
        {
            // A hit while exposed shortens its stay a little; it ducks back sooner when hurt.
            if (state == State.Exposed) stateTime = Mathf.Max(stateTime, exposedTime * 0.4f);
        }

        protected override void OnDeath()
        {
            if (wake != null) wake.gameObject.SetActive(false);
            if (sandMaterial != null) ImpactFlash.Spawn(sandMaterial, transform.position + Vector3.up * 0.3f, 1f, 0.2f);
        }

        protected override void UpdateDeath(float t)
        {
            // Flops on the sand, then sinks back in.
            float sink = Mathf.Clamp01((t - corpseTime * 0.5f) / (corpseTime * 0.5f));
            Visual.localPosition = VisualRest + Vector3.down * (burrowDepth * sink);
            Visual.localRotation = VisualRestRotation * Quaternion.Euler(0f, 0f, 70f * Mathf.Clamp01(t / 0.3f));
        }

        // ---------- Animation ----------

        private void Animate(float dt)
        {
            bool moving = AgentReady && Agent.velocity.sqrMagnitude > 0.2f;
            float shake = state == State.Surfacing ? Mathf.Sin(Time.time * 60f) * 0.04f * (stateTime / surfaceTime) : 0f;

            // The body rises out of the sand as depth goes to 0, rearing up when exposed.
            float rear = 1f - depth;
            Visual.localPosition = VisualRest + Vector3.down * (burrowDepth * depth) + new Vector3(shake, 0f, 0f);
            float pitch = state == State.Stunned ? 0f : -55f * rear;
            float roll = state == State.Stunned ? 70f + Mathf.Sin(Time.time * 9f) * 15f : 0f;
            Visual.localRotation = VisualRestRotation * Quaternion.Euler(pitch, 0f, roll);

            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == null) continue;
                float wave = Mathf.Sin(Time.time * (moving ? 10f : 4f) - i * 0.9f) * (moving ? 18f : 8f);
                segments[i].localRotation = Quaternion.Euler(0f, wave, 0f);
            }
            if (jaw != null)
            {
                float open = state == State.Exposed ? Mathf.Clamp01(1f - stateTime / 0.4f) * 45f + 8f : state == State.Stunned ? 25f : 0f;
                jaw.localRotation = Quaternion.Euler(open, 0f, 0f);
            }

            // Sand wake: a mound that travels with it while buried, bulging up before it bursts out.
            if (wake != null)
            {
                bool buried = state == State.Idle || state == State.Burrowed || state == State.Surfacing;
                float size = state == State.Surfacing ? 1f + 0.8f * (stateTime / surfaceTime) : moving ? 1f : 0.6f;
                wake.gameObject.SetActive(buried);
                wake.localScale = new Vector3(wakeRest.x * size, wakeRest.y * size * (1f + Mathf.Sin(Time.time * 14f) * 0.15f), wakeRest.z * size);
                if (buried && moving && sandMaterial != null && Time.time >= nextPuff)
                {
                    nextPuff = Time.time + 0.18f;
                    ImpactFlash.Spawn(sandMaterial, transform.position + Random.insideUnitSphere * 0.3f + Vector3.up * 0.1f, 0.35f, 0.25f);
                }
            }
        }
    }
}
