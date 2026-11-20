using SignalLost.Combat;
using SignalLost.Enemies.Desert;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.Enemies.Caves
{
    /// <summary>
    /// Crystal Caves fodder: a slow, calm cave snail (one pale cream body) carrying a glowing crystal prism shell. The
    /// caves' tank and area-denial enemy, the same job as the Mudlands' Roadblock Turtle.
    ///
    /// Once the player enters its territory it crawls in a circle around them, laying a glowing <see cref="SlimePatch"/>
    /// trail that slows and hurts anyone standing in it, so the ring closes them in. Get within 2 m, or hit it, and it
    /// puffs out a puddle of slime all around itself. Hit it and it pulls into its shell for a few seconds: the shell takes only
    /// a quarter damage and bounces laser bolts back (the Hubcap Armadillo's <see cref="IBoltReflector"/>), and a
    /// ring of light flares out. While its head is out the soft body takes full damage, and only then can the laser
    /// combo's third hit stun it. Counterplay: hit the head, stay out of the trail, wait for it to come out.
    ///
    /// Builds on <see cref="DesertFodder"/> like every other small enemy (territory, leash and heal, hit flash,
    /// combo stun, death). All numbers are placeholders.
    /// </summary>
    public sealed class PrismSnail : DesertFodder, IBoltReflector
    {
        private enum State { Idle, Crawl, Pulse, Shell, Poke }

        [Header("Crawl")]
        [SerializeField, Min(0.1f)] private float crawlSpeed = 1.5f;
        [Tooltip("It circles the player at this distance, laying a ring of slime around them.")]
        [SerializeField, Min(1f)] private float orbitRadius = 2.5f;
        [Tooltip("How far ahead around the circle it aims each repath, in degrees.")]
        [SerializeField, Range(10f, 90f)] private float orbitLead = 40f;

        [Header("Slime pulse")]
        [Tooltip("If the player gets this close, it puffs out a puddle of slime around itself.")]
        [SerializeField, Min(0.5f)] private float pulseRange = 2f;
        [Tooltip("The warning before the puddle: it swells and the shell glows brighter.")]
        [SerializeField, Min(0.1f)] private float pulseWindUp = 0.5f;
        [Tooltip("Distance of the outermost patch centres; the slime reaches one patch radius beyond (about 3.8 m).")]
        [SerializeField, Min(0.3f)] private float pulseRadius = 3f;
        [SerializeField, Min(0f)] private float pulseCooldown = 5f;

        [Header("Slime trail")]
        [Tooltip("A new patch every time it has crawled this far.")]
        [SerializeField, Min(0.2f)] private float trailSpacing = 0.7f;
        [SerializeField, Min(0.2f)] private float trailRadius = 0.8f;
        [Tooltip("Share of the player's speed lost while standing in it (0.4 = 40%).")]
        [SerializeField, Range(0f, 1f)] private float trailSlow = 0.4f;
        [Tooltip("Damage per half-second tick while standing in it, however many patches overlap.")]
        [SerializeField, Min(0f)] private float trailDamage = 4f;
        [Tooltip("Long enough to outlast one full circle around the player (2 x pi x orbitRadius / crawlSpeed, about 10 s).")]
        [SerializeField, Min(0.5f)] private float trailLife = 13f;

        [Header("Shell")]
        [SerializeField, Min(0.1f)] private float shellTime = 3f;
        [Tooltip("Time spent poking back out before it crawls again; a hit now sends it straight back in.")]
        [SerializeField, Min(0.05f)] private float pokeTime = 0.5f;
        [Tooltip("Share of damage the shell lets through (0.25 = a quarter).")]
        [SerializeField, Range(0.05f, 1f)] private float shellDamageShare = 0.25f;
        [Tooltip("Damage a bolt keeps when it bounces off the shell (0 to 1).")]
        [SerializeField, Range(0f, 1f)] private float deflectDamageMultiplier = 0.4f;
        [SerializeField, Min(0.5f)] private float flareSize = 3f;

        [Header("Rig")]
        [Tooltip("The crystal shell; it sinks onto the body while hiding.")]
        [SerializeField] private Transform shell;
        [Tooltip("Head and eyestalks together: they shrink into the body while hiding.")]
        [SerializeField] private Transform headRoot;
        [SerializeField] private Transform[] stalks = new Transform[0];
        [SerializeField] private Renderer[] shellParts = new Renderer[0];
        [Tooltip("The shell after it dies (dull).")]
        [SerializeField] private Material spentMaterial;
        [SerializeField] private Material trailMaterial;
        [SerializeField] private Material flareMaterial;
        [SerializeField] private Light glow;

        private State state = State.Idle;
        private float stateTime, nextRepath, nextWander, nextPulse, stuckTime, outAmount = 1f, glowIntensity;
        private float orbitSign = 1f; // +1 anticlockwise, -1 clockwise; flips when a wall is in the way
        private Vector3 lastTrail, shellRest, headRestPosition, headRestScale;
        private Quaternion[] stalkRest = new Quaternion[0];

        /// <summary>True while it is hiding in its shell.</summary>
        public bool InShell => state == State.Shell;

        // Hiding in the shell: it can't be stunned (head out only).
        protected override bool CanBeStunned => state != State.Shell;

        protected override void Awake()
        {
            base.Awake();
            if (shell != null) shellRest = shell.localPosition;
            if (headRoot != null)
            {
                headRestPosition = headRoot.localPosition;
                headRestScale = headRoot.localScale;
            }
            stalkRest = new Quaternion[stalks.Length];
            for (int i = 0; i < stalks.Length; i++) if (stalks[i] != null) stalkRest[i] = stalks[i].localRotation;
            if (glow != null) glowIntensity = glow.intensity;
            orbitSign = Random.value < 0.5f ? -1f : 1f;
            Health.DamageModifier = (amount, _) => state == State.Shell ? amount * shellDamageShare : amount;
        }

        private void Enter(State next)
        {
            state = next;
            stateTime = 0f;
            if (next == State.Shell || next == State.Poke || next == State.Pulse) Halt();
            if (next == State.Crawl) lastTrail = transform.position;
        }

        protected override void Tick(float dt)
        {
            stateTime += dt;
            switch (state)
            {
                case State.Idle: UpdateIdle(); break;
                case State.Crawl: UpdateCrawl(dt); break;
                case State.Pulse:
                    if (stateTime < pulseWindUp) break;
                    SlimePuddle();
                    nextPulse = Time.time + pulseCooldown;
                    Enter(State.Crawl);
                    break;
                case State.Shell:
                    if (stateTime >= shellTime) Enter(State.Poke);
                    break;
                case State.Poke:
                    if (stateTime >= pokeTime) { LastSeen = Time.time; Enter(State.Crawl); }
                    break;
            }
            Animate(dt);
        }

        private void UpdateIdle()
        {
            if (TargetAlive && (Notices() || Time.time - LastSeen < 4f))
            {
                LastSeen = Time.time;
                Enter(State.Crawl);
                return;
            }
            // Drifts about near its spot.
            if (Time.time < nextWander) return;
            nextWander = Time.time + Random.Range(4f, 7f);
            Vector2 step = Random.insideUnitCircle * 2f;
            Chase(Home + new Vector3(step.x, 0f, step.y), crawlSpeed * 0.5f);
        }

        private void UpdateCrawl(float dt)
        {
            if (!TargetAlive) { Halt(); Enter(State.Idle); return; }
            if (Notices()) LastSeen = Time.time;
            if (Time.time - LastSeen > 4f || GaveUp()) { Enter(State.Idle); return; }
            if (FlatDistance(Target.position) <= pulseRange && Time.time >= nextPulse) { Enter(State.Pulse); return; }

            // Stuck against a wall or rock: go round the other way.
            stuckTime = AgentReady && Agent.velocity.magnitude < 0.2f ? stuckTime + dt : 0f;
            if (stuckTime > 1.5f)
            {
                stuckTime = 0f;
                orbitSign = -orbitSign;
                nextRepath = 0f;
            }
            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + 0.25f;
                Chase(OrbitPoint(), crawlSpeed);
            }
            if (Vector3.Distance(transform.position, lastTrail) >= trailSpacing)
            {
                lastTrail = transform.position;
                Slime(transform.position);
            }
        }

        // A point on the circle around the player, a little ahead of where it is now. From far away the same point
        // pulls it in on a spiral, so one rule covers approaching and circling.
        private Vector3 OrbitPoint()
        {
            Vector3 fromPlayer = transform.position - Target.position;
            fromPlayer.y = 0f;
            if (fromPlayer.sqrMagnitude < 0.01f) fromPlayer = -transform.forward;
            Vector3 want = Target.position + Quaternion.AngleAxis(orbitLead * orbitSign, Vector3.up) * fromPlayer.normalized * orbitRadius;
            if (NavMesh.SamplePosition(want, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) return hit.position;
            orbitSign = -orbitSign; // off the walkable floor that way: turn round
            return Target.position;
        }

        // The close-range attack: a puddle of slime all around itself.
        private void SlimePuddle()
        {
            Slime(transform.position);
            // Rings of patches out to pulseRadius, spaced so neighbouring patches overlap with no gaps.
            float spacing = trailRadius * 1.5f;
            int rings = Mathf.Max(1, Mathf.CeilToInt(pulseRadius / spacing));
            for (int r = 1; r <= rings; r++)
            {
                float ring = pulseRadius * r / rings;
                int count = Mathf.CeilToInt(2f * Mathf.PI * ring / spacing);
                for (int i = 0; i < count; i++)
                {
                    float angle = (i + 0.5f * r) * Mathf.PI * 2f / count;
                    Vector3 at = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring;
                    if (NavMesh.SamplePosition(at, out NavMeshHit hit, 0.6f, NavMesh.AllAreas)) Slime(hit.position);
                }
            }
            if (trailMaterial != null) ImpactFlash.Spawn(trailMaterial, transform.position + Vector3.up * 0.2f, pulseRadius * 2f, 0.25f);
        }

        private void Slime(Vector3 at)
        {
            if (trailMaterial != null)
                SlimePatch.Spawn(trailMaterial, at + Vector3.up * 0.03f, trailRadius, trailSlow, trailDamage, gameObject, trailLife);
        }

        // ---------- Hits ----------

        /// <summary>IBoltReflector: bolts bounce off the shell, never off the soft body.</summary>
        public bool TryReflect(Vector3 hitPoint, Vector3 incoming, Transform shooter, out Vector3 newDirection, out float damageMultiplier)
        {
            damageMultiplier = deflectDamageMultiplier;
            newDirection = -incoming;
            if (state != State.Shell || IsDead) return false;
            Vector3 normal = hitPoint - (transform.position + Vector3.up * 0.6f);
            if (normal.sqrMagnitude < 0.0001f) normal = -incoming;
            newDirection = Vector3.Reflect(incoming.normalized, normal.normalized) + Vector3.up * 0.1f;
            if (flareMaterial != null) ImpactFlash.Spawn(flareMaterial, hitPoint, 0.3f, 0.1f);
            return true;
        }

        protected override void OnHit(float amount, GameObject source, Vector3 away)
        {
            // Hitting it (head out or shelled) also sets off the slime puddle, on the same cooldown.
            if (Time.time >= nextPulse)
            {
                SlimePuddle();
                nextPulse = Time.time + pulseCooldown;
            }
            if (state == State.Shell) return; // already hiding; the shell timer is not extended
            // The hit that landed on the head was full damage; now it hides, with a ring of light.
            Enter(State.Shell);
            if (flareMaterial != null) ImpactFlash.Spawn(flareMaterial, transform.position + Vector3.up * 0.6f, flareSize, 0.35f);
        }

        protected override void OnDeath()
        {
            // The shell cracks dull and the glow goes out.
            if (spentMaterial != null)
                foreach (Renderer part in shellParts)
                    if (part != null) part.sharedMaterial = spentMaterial;
            if (glow != null) glow.enabled = false;
        }

        // ---------- Look ----------

        private void Animate(float dt)
        {
            float rate = state == State.Shell ? 9f : 2.5f; // ducks in fast, eases back out
            outAmount = Mathf.MoveTowards(outAmount, state == State.Shell ? 0f : 1f, rate * dt);

            if (headRoot != null)
            {
                headRoot.localScale = headRestScale * Mathf.Max(0.05f, outAmount);
                headRoot.localPosition = headRestPosition + new Vector3(0f, -0.05f, -0.3f) * (1f - outAmount);
            }
            if (shell != null) shell.localPosition = shellRest + Vector3.down * (0.16f * (1f - outAmount));

            for (int i = 0; i < stalks.Length; i++)
                if (stalks[i] != null)
                    stalks[i].localRotation = stalkRest[i] * Quaternion.Euler(Mathf.Sin(Time.time * 1.7f + i * 2f) * 7f, Mathf.Sin(Time.time * 1.3f + i) * 9f, 0f);

            // Slow, muscular crawl: the body stretches and squashes a little.
            float moving = AgentReady ? Mathf.Clamp01(Agent.velocity.magnitude) : 0f;
            float pulse = Mathf.Sin(Time.time * 3.5f) * moving;
            Visual.localScale = state == State.Pulse
                ? Vector3.one * (1f + 0.12f * Mathf.Clamp01(stateTime / pulseWindUp))
                : new Vector3(1f - 0.02f * pulse, 1f - 0.03f * pulse, 1f + 0.05f * pulse);

            if (glow != null && glow.enabled)
                glow.intensity = glowIntensity * (1f + 0.15f * Mathf.Sin(Time.time * 2f) + (state == State.Shell ? 0.8f : state == State.Pulse ? 1.5f * stateTime / pulseWindUp : 0f));
        }
    }
}
