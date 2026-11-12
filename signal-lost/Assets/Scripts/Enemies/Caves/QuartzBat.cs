using SignalLost.Combat;
using SignalLost.Enemies.Desert;
using SignalLost.Player;
using UnityEngine;

namespace SignalLost.Enemies.Caves
{
    /// <summary>
    /// Crystal Caves fodder: a cave bat with translucent crystal wings and a glowing quartz crystal set in its chest.
    ///
    /// It circles the player's head at a steady, predictable pace, out of melee reach. Every few seconds it stops,
    /// raises its wings and its crystal pulses (the warning), then swoops down at the player's head and bites. After a
    /// bite (or a miss) it hangs low for a moment, the melee window, then climbs back up and keeps circling.
    ///
    /// Damage rule: shots only hurt it when they hit the chest crystal; bolts that hit its wings or body glance off
    /// harmlessly. Melee swings hurt it anywhere, but it is only low enough to reach while swooping or hanging low.
    ///
    /// Builds on <see cref="DesertFodder"/> like every other small enemy (territory, leash and heal, hit flash, combo
    /// stun, death). Its NavMeshAgent walks the ground under it, which keeps it inside the cave; the model flies at
    /// <see cref="height"/> above that point. All numbers are placeholders.
    /// </summary>
    public sealed class QuartzBat : DesertFodder, IBoltReflector
    {
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private enum State { Hover, Circle, WindUp, Swoop, Hang, Climb }

        [Header("Flight")]
        [Tooltip("Height it circles at, above the ground. Keep it out of melee reach (the laser reaches 2.1 m).")]
        [SerializeField, Min(1f)] private float cruiseHeight = 3.3f;
        [SerializeField, Min(1f)] private float circleRadius = 3.6f;
        [Tooltip("Degrees per second around the player.")]
        [SerializeField, Min(5f)] private float circleSpeed = 65f;
        [SerializeField, Min(0.5f)] private float flySpeed = 5f;
        [SerializeField, Min(0.5f)] private float climbSpeed = 2.4f;

        [Header("Swoop and bite")]
        [Tooltip("Seconds of circling between swoops.")]
        [SerializeField, Min(0.5f)] private float swoopInterval = 3.5f;
        [Tooltip("The warning: it stops, raises its wings and its crystal pulses.")]
        [SerializeField, Min(0.1f)] private float windUpTime = 0.6f;
        [SerializeField, Min(1f)] private float swoopSpeed = 7.5f;
        [Tooltip("Gives up the swoop after this long.")]
        [SerializeField, Min(0.3f)] private float maxSwoopTime = 1.4f;
        [Tooltip("Height of the player's head above their feet: where it bites.")]
        [SerializeField, Min(0.5f)] private float biteHeight = 1.5f;
        [SerializeField, Min(0.2f)] private float biteRange = 0.9f;
        [SerializeField, Min(0f)] private float biteDamage = 8f;
        [Tooltip("Seconds it hangs low after a swoop: the melee window.")]
        [SerializeField, Min(0f)] private float hangTime = 1f;

        [Header("Rig")]
        [Tooltip("The chest crystal: the only place shots hurt it.")]
        [SerializeField] private Collider crystalCollider;
        [SerializeField] private Renderer crystal;
        [Tooltip("The crystal after it dies (switched off).")]
        [SerializeField] private Material spentMaterial;
        [SerializeField] private Light glow;
        [Tooltip("Wing pivots at the shoulders (left, right); they flap about their forward axis.")]
        [SerializeField] private Transform[] wings = new Transform[0];
        [SerializeField] private Transform jaw;
        [Tooltip("Where it bites from.")]
        [SerializeField] private Transform mouth;

        private State state = State.Hover;
        private float stateTime, nextSwoop, swoopStartDistance, swoopStartHeight, deathHeight, flap, glowIntensity;
        private float height;
        private float orbitAngle, orbitDirection = 1f;
        private bool bitten;
        private int crystalHitFrame = -1;
        private Quaternion[] wingRest = new Quaternion[0];
        private Quaternion jawRest = Quaternion.identity;
        private MaterialPropertyBlock crystalBlock;
        private Color crystalEmission;

        /// <summary>Current flying height above the ground under it.</summary>
        public float Height => height;
        public bool IsSwooping => state == State.Swoop;

        protected override void Awake()
        {
            base.Awake();
            Health.DamageModifier = FilterDamage;
            height = cruiseHeight;
            wingRest = new Quaternion[wings.Length];
            for (int i = 0; i < wings.Length; i++) wingRest[i] = wings[i] != null ? wings[i].localRotation : Quaternion.identity;
            if (jaw != null) jawRest = jaw.localRotation;
            crystalBlock = new MaterialPropertyBlock();
            if (crystal != null && crystal.sharedMaterial != null && crystal.sharedMaterial.HasProperty(EmissionColor))
                crystalEmission = crystal.sharedMaterial.GetColor(EmissionColor);
            if (glow != null) glowIntensity = glow.intensity;
        }

        protected override void OnStarted()
        {
            if (Agent != null) Agent.updateRotation = false; // it faces the player, not where it flies
            orbitDirection = Random.value < 0.5f ? -1f : 1f;
            Halt();
        }

        private void Enter(State next)
        {
            state = next;
            stateTime = 0f;
        }

        protected override void Tick(float dt)
        {
            stateTime += dt;
            switch (state)
            {
                case State.Hover: UpdateHover(dt); break;
                case State.Circle: UpdateCircle(dt); break;
                case State.WindUp: UpdateWindUp(dt); break;
                case State.Swoop: UpdateSwoop(dt); break;
                case State.Hang: UpdateHang(dt); break;
                case State.Climb: UpdateClimb(dt); break;
            }
            if (TargetAlive && state != State.Hover) TurnToward(Target.position, 360f * dt);
            Animate(dt);
        }

        // ---------- States ----------

        private void UpdateHover(float dt)
        {
            Halt();
            height = Mathf.MoveTowards(height, cruiseHeight, climbSpeed * dt);
            if (!Notices()) return;
            LastSeen = Time.time;
            StartCircling();
        }

        private void StartCircling()
        {
            Vector3 from = transform.position - Target.position;
            orbitAngle = Mathf.Atan2(from.z, from.x) * Mathf.Rad2Deg;
            nextSwoop = Mathf.Max(nextSwoop, Time.time + swoopInterval);
            Enter(State.Circle);
        }

        /// <summary>True (and drops to hovering) when the player is dead or past the leash (then it flies home and heals).</summary>
        private bool LostTarget()
        {
            if (!TargetAlive || GaveUp()) { Enter(State.Hover); return true; }
            return false;
        }

        private void UpdateCircle(float dt)
        {
            if (LostTarget()) return;
            height = Mathf.MoveTowards(height, cruiseHeight, climbSpeed * dt);
            orbitAngle += orbitDirection * circleSpeed * dt;
            Chase(OrbitPoint(orbitAngle + orbitDirection * 25f), flySpeed); // aim a little ahead so it never stalls
            // No CanSee: it looks from the ground under the bat, so a rock would stop every swoop. The swoop follows the NavMesh, so walls still do.
            if (Time.time >= nextSwoop && FlatDistance(Target.position) <= circleRadius * 3f) Enter(State.WindUp);
        }

        private Vector3 OrbitPoint(float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return Target.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * circleRadius;
        }

        private void UpdateWindUp(float dt)
        {
            Halt();
            if (!TargetAlive) { Enter(State.Hover); return; }
            if (stateTime < windUpTime) return;
            swoopStartDistance = Mathf.Max(0.5f, FlatDistance(Target.position));
            swoopStartHeight = height;
            bitten = false;
            Enter(State.Swoop);
        }

        private Vector3 TargetHead => Target.position + Vector3.up * biteHeight;
        private Vector3 MouthPoint => mouth != null ? mouth.position : Visual.position + Vector3.up * 0.4f;

        private void UpdateSwoop(float dt)
        {
            if (!TargetAlive) { Enter(State.Climb); return; }
            Chase(Target.position, swoopSpeed);
            // Drops toward head height as it closes in, so it arrives at the player's face.
            float progress = 1f - Mathf.Clamp01(FlatDistance(Target.position) / swoopStartDistance);
            float want = Mathf.Lerp(swoopStartHeight, biteHeight - 0.35f, Mathf.SmoothStep(0f, 1f, progress * 1.4f));
            height = Mathf.MoveTowards(height, want, swoopSpeed * dt);

            if (!bitten && Vector3.Distance(MouthPoint, TargetHead) <= biteRange)
            {
                bitten = true;
                if (TargetHealth != null) TargetHealth.TakeDamage(biteDamage, gameObject);
            }
            if (bitten || stateTime >= maxSwoopTime) Enter(State.Hang);
        }

        private void UpdateHang(float dt)
        {
            // Hovers low, drifting back a little: the melee window.
            if (TargetAlive && FlatDistance(Target.position) < 1.4f)
                Chase(transform.position - FlatDirectionTo(Target.position) * 1.5f, flySpeed * 0.4f);
            else Halt();
            height = Mathf.MoveTowards(height, biteHeight - 0.35f, climbSpeed * dt);
            if (stateTime >= hangTime) Enter(State.Climb);
        }

        private void UpdateClimb(float dt)
        {
            if (LostTarget()) return;
            Chase(OrbitPoint(orbitAngle), flySpeed);
            height = Mathf.MoveTowards(height, cruiseHeight, climbSpeed * dt);
            if (height >= cruiseHeight - 0.05f) StartCircling();
        }

        protected override void OnHit(float amount, GameObject source, Vector3 away)
        {
            if (state == State.Hover && TargetAlive) StartCircling(); // being hit draws it in
        }

        // ---------- Damage rule ----------

        // Shots only count on the crystal: bolts that hit anything else glance off (the bolt's own sparks show it).
        public bool TryReflect(Vector3 hitPoint, Vector3 incoming, Transform shooter, out Vector3 newDirection, out float damageMultiplier)
        {
            damageMultiplier = 0f;
            newDirection = -incoming;
            if (IsDead) return false;
            if (crystalCollider != null && crystalCollider.enabled &&
                (crystalCollider.ClosestPoint(hitPoint) - hitPoint).sqrMagnitude <= 0.08f * 0.08f)
            {
                crystalHitFrame = Time.frameCount; // the bolt's TakeDamage follows in the same frame
                return false;
            }
            Vector3 normal = hitPoint - Visual.position;
            if (normal.sqrMagnitude < 0.0001f) normal = -incoming;
            newDirection = Vector3.Reflect(incoming.normalized, normal.normalized) + Vector3.up * 0.3f;
            return true;
        }

        // The player's damage only lands from a crystal shot or a melee swing; blast splash and everything else from
        // the player is refused. Damage from anything else (hazards, scripted kills) passes unchanged.
        // shortcut: a bolt landing on the crystal during a swing counts as melee too; fine, it hit the crystal.
        private float FilterDamage(float amount, GameObject source)
        {
            if (Time.frameCount == crystalHitFrame) return amount;
            if (source == null || source.GetComponentInParent<PlayerLife>() == null) return amount;
            MiningLaser laser = source.GetComponentInParent<MiningLaser>();
            return laser != null && laser.IsSwinging ? amount : 0f;
        }

        // ---------- Death ----------

        protected override void OnDeath()
        {
            deathHeight = height;
            if (crystal != null && spentMaterial != null)
            {
                crystal.SetPropertyBlock(null);
                crystal.sharedMaterial = spentMaterial; // the crystal switches off
            }
            if (glow != null) glow.enabled = false;
        }

        protected override void UpdateDeath(float t)
        {
            // Drops out of the air, tumbling, then the usual flop and sink.
            float fallTime = Mathf.Sqrt(2f * deathHeight / 14f);
            if (t < fallTime)
            {
                height = Mathf.Max(0f, deathHeight - 7f * t * t);
                Visual.localPosition = VisualRest + Vector3.up * height;
                Visual.localRotation = VisualRestRotation * Quaternion.Euler(t * 200f, 0f, t * 320f);
                return;
            }
            base.UpdateDeath(t - fallTime);
        }

        // ---------- Look ----------

        protected override void LateUpdate()
        {
            // Wings keep beating whatever the state (even while stunned or flying home), until it dies.
            if (!IsDead) Flap(Time.deltaTime);
            base.LateUpdate();
        }

        private void Flap(float dt)
        {
            bool raised = state == State.WindUp;
            bool diving = state == State.Swoop;
            flap += dt * (diving ? 6f : raised ? 4f : 11f);
            for (int i = 0; i < wings.Length; i++)
            {
                if (wings[i] == null) continue;
                float side = i == 0 ? 1f : -1f; // left wing rotates +Z to go up
                float angle;
                if (raised) angle = Mathf.Lerp(0f, 65f, Mathf.Clamp01(stateTime / (windUpTime * 0.5f))) + Mathf.Sin(flap * 2f) * 6f;
                else if (diving) angle = 50f + Mathf.Sin(flap) * 8f; // swept up and back into a dive
                else angle = Mathf.Sin(flap) * 42f + 8f;
                wings[i].localRotation = wingRest[i] * Quaternion.Euler(0f, diving ? -25f * side : 0f, angle * side);
            }
        }

        private void Animate(float dt)
        {
            // Bobs with each wingbeat; rolls slightly into the circle.
            float bob = state == State.Swoop ? 0f : Mathf.Sin(flap) * 0.08f;
            float roll = state == State.Circle ? -orbitDirection * 10f : 0f;
            float pitch = state == State.Swoop ? 25f : 0f;
            Visual.localPosition = VisualRest + Vector3.up * (height + bob);
            Visual.localRotation = VisualRestRotation * Quaternion.Euler(pitch, 0f, roll);

            float open = state == State.WindUp || state == State.Swoop ? 1f : 0f;
            if (jaw != null) jaw.localRotation = Quaternion.Slerp(jaw.localRotation, jawRest * Quaternion.Euler(35f * open, 0f, 0f), 14f * dt);
        }

        protected override void Paint()
        {
            // The crystal pulses brighter during the wind-up: part of the swoop warning.
            if (crystal == null) return;
            float k = state == State.WindUp ? Mathf.Clamp01(stateTime / windUpTime) : 0f;
            if (k <= 0f)
            {
                crystal.SetPropertyBlock(null);
                if (glow != null && glow.enabled) glow.intensity = glowIntensity;
                return;
            }
            crystal.GetPropertyBlock(crystalBlock);
            crystalBlock.SetColor(EmissionColor, crystalEmission * (1f + 2.5f * k * (0.6f + 0.4f * Mathf.Sin(Time.time * 40f))));
            crystal.SetPropertyBlock(crystalBlock);
            if (glow != null && glow.enabled) glow.intensity = glowIntensity * (1f + 1.5f * k);
        }
    }
}
