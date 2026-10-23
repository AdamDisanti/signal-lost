using SignalLost.Combat;
using SignalLost.Effects;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// Spotter and support: a meerkat wearing a tiny radar dish from the ship's antenna array. It pops up out of its
    /// burrow, watches the player, stands tall and shrieks. That "spots" the player for a few seconds
    /// (<see cref="SpotterAlert"/>): every desert fodder enemy nearby knows where they are and moves faster. While
    /// it is up, its radar also jams the player's scanner within its range (<see cref="ScannerJamReceiver"/>).
    /// It never attacks directly.
    ///
    /// Counterplay: it is fragile but only hittable while popped up, and ducks back down when hit, then pops up
    /// somewhere else nearby. Taking meerkats out first makes the rest of a fight easier and frees the scanner.
    /// All numbers are placeholders.
    /// </summary>
    public sealed class DishMeerkat : DesertFodder, IScannerJammer
    {
        private enum State { Hidden, Rising, Watching, Shrieking, Ducking }

        [Header("Peeking")]
        [Tooltip("Pops up when the player is within this distance.")]
        [SerializeField, Min(1f)] private float noticeRange = 22f;
        [SerializeField, Min(0.05f)] private float riseTime = 0.3f;
        [SerializeField, Min(0.05f)] private float duckTime = 0.25f;
        [Tooltip("Seconds in its burrow before popping up again (random between the two).")]
        [SerializeField] private Vector2 hideTime = new Vector2(1.8f, 3.2f);
        [Tooltip("Ducks back down if it hasn't seen the player for this long.")]
        [SerializeField, Min(0.5f)] private float giveUpTime = 3f;
        [Tooltip("Pops up from a new hole up to this far from its first one.")]
        [SerializeField, Min(0f)] private float relocateRadius = 4f;

        [Header("Shriek")]
        [Tooltip("Seconds it must watch the player before it shrieks.")]
        [SerializeField, Min(0f)] private float watchTime = 0.8f;
        [Tooltip("Standing tall with the dish whirring before the shriek: the player's warning.")]
        [SerializeField, Min(0.05f)] private float shriekWindup = 0.8f;
        [Tooltip("Seconds the player stays spotted after a shriek.")]
        [SerializeField, Min(0.5f)] private float spotDuration = 6f;

        [Header("Radar jamming")]
        [Tooltip("Jams the scanner while it is popped up and the player is within this distance.")]
        [SerializeField, Min(0f)] private float jamRadius = 12f;

        [Header("Rig")]
        [SerializeField] private Transform dish;
        [SerializeField] private Transform head;
        [Tooltip("The hole it hides in. Not under Visual, so it stays on the ground.")]
        [SerializeField] private Transform burrow;
        [SerializeField] private Material shriekMaterial;
        [SerializeField] private Material dustMaterial;

        [Tooltip("How far below ground the body sits while hidden.")]
        [SerializeField, Min(0.2f)] private float hideDepth = 0.95f;

        private State state = State.Hidden;
        private float stateTime, hideFor, watched, rise, dishSpin;
        private Vector3 firstHome;
        private bool jamming;

        public Vector3 JamPosition => transform.position;
        public float JamRadius => jamRadius;

        protected override void OnStarted()
        {
            firstHome = Home;
            Halt();
            if (AgentReady) Agent.updateRotation = false;
            rise = 0f;
            EnterHidden(Random.Range(0.3f, 1.2f));
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            SetJamming(false);
        }

        private void Enter(State next)
        {
            state = next;
            stateTime = 0f;
            bool up = next == State.Rising || next == State.Watching || next == State.Shrieking;
            // Only hittable while popped up.
            SetColliders(up && !IsDead);
            SetJamming(up && !IsDead);
            if (next == State.Watching) watched = 0f;
        }

        private void EnterHidden(float seconds)
        {
            hideFor = seconds;
            Enter(State.Hidden);
        }

        private void SetJamming(bool on)
        {
            if (on == jamming) return;
            jamming = on;
            if (on) ScannerJamReceiver.Register(this);
            else ScannerJamReceiver.Unregister(this);
        }

        protected override void Tick(float dt)
        {
            stateTime += dt;
            switch (state)
            {
                case State.Hidden: UpdateHidden(); break;
                case State.Rising:
                    rise = Mathf.Clamp01(stateTime / riseTime);
                    if (stateTime >= riseTime) Enter(State.Watching);
                    break;
                case State.Watching: UpdateWatching(dt); break;
                case State.Shrieking: UpdateShrieking(dt); break;
                case State.Ducking:
                    rise = 1f - Mathf.Clamp01(stateTime / duckTime);
                    if (stateTime >= duckTime) EnterHidden(Random.Range(hideTime.x, Mathf.Max(hideTime.x, hideTime.y)));
                    break;
            }
            Animate(dt);
        }

        // ---------- States ----------

        private void UpdateHidden()
        {
            rise = 0f;
            if (stateTime < hideFor) return;
            if (!TargetAlive || FlatDistance(Target.position) > noticeRange)
            {
                stateTime = hideFor - 0.5f; // check again shortly
                return;
            }
            Relocate();
            if (Target != null) TurnToward(Target.position, 360f);
            if (dustMaterial != null) ImpactFlash.Spawn(dustMaterial, transform.position + Vector3.up * 0.15f, 0.6f, 0.2f);
            Enter(State.Rising);
        }

        /// <summary>Tunnels to a new hole near its first one, so it doesn't always pop up in the same spot.</summary>
        private void Relocate()
        {
            if (relocateRadius <= 0f || !AgentReady) return;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                Vector2 r = Random.insideUnitCircle * relocateRadius;
                if (NavMesh.SamplePosition(firstHome + new Vector3(r.x, 0f, r.y), out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                {
                    Agent.Warp(hit.position);
                    return;
                }
            }
        }

        private void UpdateWatching(float dt)
        {
            rise = 1f;
            if (!TargetAlive) { Enter(State.Ducking); return; }
            TurnToward(Target.position, 240f * dt);
            if (CanSee(noticeRange))
            {
                LastSeen = Time.time;
                watched += dt;
                if (watched >= watchTime) Enter(State.Shrieking);
            }
            else if (Time.time - LastSeen > giveUpTime && stateTime > giveUpTime) Enter(State.Ducking);
        }

        private void UpdateShrieking(float dt)
        {
            rise = 1f;
            if (Target != null) TurnToward(Target.position, 240f * dt);
            if (stateTime < shriekWindup) return;

            SpotterAlert.Mark(spotDuration, transform.position);
            if (shriekMaterial != null)
            {
                Vector3 mouth = head != null ? head.position : transform.position + Vector3.up * 0.9f;
                ExpandingRing.Spawn(shriekMaterial, mouth, 2.5f, 0.45f, 0.06f, true, transform.rotation);
                ExpandingRing.Spawn(shriekMaterial, transform.position + Vector3.up * 0.05f, 5f, 0.6f, 0.08f);
            }
            Enter(State.Ducking);
        }

        // ---------- Hits ----------

        protected override void OnHit(float amount, GameObject source, Vector3 away)
        {
            // Hit and still alive: dive back into the burrow and stay down a little longer.
            if (state == State.Hidden || state == State.Ducking) return;
            Enter(State.Ducking);
        }

        protected override void OnDeath() => SetJamming(false);

        protected override void UpdateDeath(float t)
        {
            // Keels over out of the hole, then sinks back into it.
            float fall = Mathf.SmoothStep(0f, 1f, t / 0.35f);
            float sink = Mathf.Clamp01((t - corpseTime * 0.6f) / (corpseTime * 0.4f));
            Visual.localRotation = VisualRestRotation * Quaternion.Euler(80f * fall, 0f, 0f);
            Visual.localPosition = VisualRest + Vector3.down * (hideDepth * sink);
        }

        // ---------- Animation ----------

        private void Animate(float dt)
        {
            float tall = state == State.Shrieking ? 1f + 0.18f * Mathf.Clamp01(stateTime / (shriekWindup * 0.4f)) : 1f;
            float quiver = state == State.Shrieking ? Mathf.Sin(Time.time * 45f) * 0.01f : 0f;
            Visual.localPosition = VisualRest + Vector3.down * (hideDepth * (1f - rise)) + new Vector3(quiver, 0f, 0f);
            Visual.localScale = new Vector3(1f, tall, 1f);

            // Meerkat look-around: the head swivels while watching.
            if (head != null && state == State.Watching)
                head.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 2.3f) * 20f * (1f - Mathf.Clamp01(watched / watchTime)), 0f);

            float rate = state == State.Shrieking ? 1500f : state == State.Watching || state == State.Rising ? 220f : 0f;
            dishSpin += rate * dt;
            if (dish != null) dish.localRotation = Quaternion.Euler(0f, dishSpin, 0f);
            if (burrow != null) burrow.gameObject.SetActive(!IsDead || Time.time - DeathTime < corpseTime);
        }
    }
}
