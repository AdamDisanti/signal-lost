using UnityEngine;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// Walking bomb: a slow, fat beetle hauling one of the ship's leaky fuel cans. It waddles straight at the player;
    /// when close it stops, swells and hisses while the can blinks faster and faster (the fuse), then explodes,
    /// hurting everything nearby, enemies included.
    ///
    /// Counterplay: kill it before it gets close; a killed beetle still pops after a short delay, so it is best
    /// shot from range or next to other enemies, which makes it a crowd-clearing tool. Fire and other explosions
    /// ignite it (<see cref="IIgnitable"/>), so packs chain-react. All numbers are placeholders.
    /// </summary>
    public sealed class JerryCanBeetle : DesertFodder, IIgnitable
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private enum State { Idle, Chase, Fuse }

        [Header("Waddle")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 2.2f;

        [Header("Fuse")]
        [Tooltip("Lights its fuse when the player is this close.")]
        [SerializeField, Min(0.5f)] private float fuseRange = 2.4f;
        [Tooltip("The hiss and blinking before it explodes: the player's warning.")]
        [SerializeField, Min(0.1f)] private float fuseTime = 1.2f;
        [Tooltip("Fuse when set off by fire or another explosion.")]
        [SerializeField, Min(0.05f)] private float ignitedFuseTime = 0.5f;
        [Tooltip("Delay before a killed beetle's can goes off.")]
        [SerializeField, Min(0.05f)] private float deathFuseTime = 0.45f;

        [Header("Blast")]
        [SerializeField, Min(0.5f)] private float blastRadius = 3.6f;
        [SerializeField, Min(0f)] private float blastDamage = 32f;
        [Tooltip("Fraction of the damage dealt at the very edge of the blast.")]
        [SerializeField, Range(0f, 1f)] private float edgeDamage = 0.3f;
        [SerializeField] private LayerMask blastMask = ~0;
        [SerializeField] private Material explosionMaterial;

        [Header("Rig")]
        [SerializeField] private Transform can;
        [Tooltip("The fuel can's renderers; they blink while the fuse burns.")]
        [SerializeField] private Renderer[] canRenderers = new Renderer[0];
        [SerializeField] private Color canColor = new Color(0.8f, 0.12f, 0.08f);
        [SerializeField] private Color glowColor = new Color(1f, 0.85f, 0.3f);
        [SerializeField] private Transform[] legs = new Transform[0];

        private State state = State.Idle;
        private float stateTime, fuseLength, nextRepath, explodeAt = -1f;
        private bool exploded;
        private MaterialPropertyBlock canBlock;
        private Quaternion canRest = Quaternion.identity;

        private GameObject blame;

        /// <summary>Remembers the player (not an enemy) as the one who set it off.</summary>
        private void Blame(GameObject source)
        {
            if (source != null && !EnemyAllegiance.IsEnemy(source)) blame = source;
        }

        protected override void Awake()
        {
            base.Awake();
            Health.Damaged += (_, amount, source) => Blame(source); // shot by the player: the blast is the player's
            canBlock = new MaterialPropertyBlock();
            if (can != null) canRest = can.localRotation;
        }

        private void Enter(State next)
        {
            state = next;
            stateTime = 0f;
            if (next == State.Fuse) Halt();
        }

        protected override void Tick(float dt)
        {
            stateTime += dt;
            switch (state)
            {
                case State.Idle:
                    if (Notices()) { LastSeen = Time.time; Enter(State.Chase); }
                    break;
                case State.Chase: UpdateChase(); break;
                case State.Fuse:
                    if (Target != null) TurnToward(Target.position, 90f * dt);
                    if (stateTime >= fuseLength) Detonate();
                    break;
            }
            Animate();
        }

        private void UpdateChase()
        {
            if (!TargetAlive) { Enter(State.Idle); return; }
            float distance = FlatDistance(Target.position);
            if (CanSee(territory.LeashRadius) || Alerted) LastSeen = Time.time;
            if (GaveUp()) { Enter(State.Idle); return; }
            if (distance <= fuseRange)
            {
                LightFuse(fuseTime);
                return;
            }
            if (Time.time >= nextRepath)
            {
                Chase(Target.position, walkSpeed);
                nextRepath = Time.time + 0.25f;
            }
        }

        private void LightFuse(float seconds)
        {
            if (state == State.Fuse)
            {
                fuseLength = Mathf.Min(fuseLength, stateTime + seconds); // a new spark can only shorten it
                return;
            }
            fuseLength = seconds;
            Enter(State.Fuse);
        }

        /// <summary>IIgnitable: fire or a nearby blast sets it off with a short fuse.</summary>
        public void Ignite(GameObject source)
        {
            if (exploded) return;
            Blame(source);
            if (IsDead)
            {
                if (explodeAt > 0f) explodeAt = Mathf.Min(explodeAt, Time.time + ignitedFuseTime);
                return;
            }
            LastSeen = Time.time;
            LightFuse(ignitedFuseTime);
        }

        private void Detonate()
        {
            if (exploded) return;
            exploded = true;
            Vector3 point = transform.position;
            if (!IsDead) Kill(gameObject); // counts as a kill for the spawner
            // Credited to whoever set it off: the player's doing hurts enemies, an enemy's doesn't.
            FodderExplosion.Explode(point, blastRadius, blastDamage, edgeDamage, blame != null ? blame : gameObject, transform, explosionMaterial, blastMask);
            SetRenderersVisible(false); // blown to bits
        }

        protected override void OnDeath()
        {
            // Killed before it went off: the can still blows after a short delay.
            if (!exploded) explodeAt = Time.time + deathFuseTime;
        }

        protected override void UpdateDeath(float t)
        {
            if (!exploded && explodeAt > 0f && Time.time >= explodeAt)
            {
                Detonate();
                return;
            }
            if (!exploded)
            {
                // Twitching on its back while the can hisses.
                Visual.localRotation = VisualRestRotation * Quaternion.Euler(0f, 0f, 180f * Mathf.Clamp01(t / 0.2f));
                Visual.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(Time.time * 40f));
            }
        }

        protected override void Paint()
        {
            // The can blinks between its colour and a hot glow, faster as the fuse runs out.
            bool burning = !exploded && (state == State.Fuse || (IsDead && explodeAt > 0f));
            float blink = 0f;
            if (burning)
            {
                float left = IsDead ? explodeAt - Time.time : fuseLength - stateTime;
                float rate = Mathf.Lerp(26f, 6f, Mathf.Clamp01(left / fuseTime));
                blink = Mathf.Sin(Time.time * rate) > 0f ? 1f : 0.2f;
            }
            foreach (Renderer item in canRenderers)
            {
                if (item == null) continue;
                if (!burning)
                {
                    item.SetPropertyBlock(null);
                    continue;
                }
                item.GetPropertyBlock(canBlock);
                canBlock.SetColor(BaseColor, Color.Lerp(canColor, glowColor, blink));
                item.SetPropertyBlock(canBlock);
            }
        }

        private void Animate()
        {
            float speed = AgentReady ? Agent.velocity.magnitude : 0f;
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;
                float swing = Mathf.Sin(Time.time * (4f + speed * 4f) + i * 2.1f) * Mathf.Clamp(speed * 14f, 0f, 25f);
                legs[i].localRotation = Quaternion.Euler(0f, swing, 0f);
            }

            // Waddle side to side; swell and quiver while the fuse burns.
            float waddle = Mathf.Sin(Time.time * 7f) * Mathf.Clamp01(speed) * 7f;
            float swell = 1f;
            Vector3 jitter = Vector3.zero;
            if (state == State.Fuse)
            {
                float k = Mathf.Clamp01(stateTime / Mathf.Max(0.01f, fuseLength));
                swell = 1f + 0.18f * k;
                jitter = Random.insideUnitSphere * 0.015f * k;
            }
            Visual.localPosition = VisualRest + jitter;
            Visual.localRotation = VisualRestRotation * Quaternion.Euler(0f, 0f, waddle);
            Visual.localScale = new Vector3(swell, 1f + (swell - 1f) * 1.4f, swell);
            if (can != null) can.localRotation = canRest * Quaternion.Euler(0f, 0f, state == State.Fuse ? Mathf.Sin(Time.time * 50f) * 6f : 0f);
        }
    }
}
