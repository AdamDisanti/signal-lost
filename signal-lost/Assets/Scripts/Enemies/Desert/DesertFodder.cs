using SignalLost.Combat;
using SignalLost.Player;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// Shared base for the desert fodder enemies (Spark Tick, Snorkel Eel, Jerry-Can Beetle, Hubcap Armadillo,
    /// Dish Meerkat). It handles what they all have in common so each enemy script only holds its own behaviour:
    /// finding the player, line of sight, the meerkat "spotted" alert, a white hit flash, a small knockback, and a
    /// default death (flop over, sink, then removed). Each enemy is a small state machine on top of this.
    ///
    /// Like the hermit crab and frog, the NavMeshAgent starts disabled in the prefab and is switched on in Start,
    /// once NavMeshBaker has built the NavMesh. All numbers are placeholders to tune in the Inspector.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health))]
    public abstract class DesertFodder : MonoBehaviour, IStunnable
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [Header("Senses")]
        [Tooltip("Who to fight. Empty finds the player automatically.")]
        [SerializeField] protected Transform target;
        [SerializeField] protected LayerMask sightMask = ~0;
        [SerializeField, Min(1f)] protected float detectRange = 14f;
        [Tooltip("Gives up beyond this distance.")]
        [SerializeField, Min(1f)] protected float loseRange = 24f;
        [Tooltip("While a Dish Meerkat has spotted the player, it is noticed from this far away, without line of sight.")]
        [SerializeField, Min(1f)] protected float alertedDetectRange = 40f;
        [Tooltip("Speed multiplier while the player is spotted by a Dish Meerkat.")]
        [SerializeField, Min(1f)] protected float alertedSpeedBonus = 1.25f;

        [Header("Body")]
        [Tooltip("The animated model. Empty uses a child named Visual.")]
        [SerializeField] protected Transform visual;
        [SerializeField, Min(0f)] protected float hitKnockback = 0.25f;
        [SerializeField, Min(0f)] protected float flashTime = 0.07f;
        [Tooltip("Seconds the body lies there after dying before it is removed.")]
        [SerializeField, Min(0.1f)] protected float corpseTime = 1.4f;

        protected NavMeshAgent Agent { get; private set; }
        protected Health Health { get; private set; }
        protected Health TargetHealth { get; private set; }
        protected CharacterController TargetBody { get; private set; }
        protected MiningLaser TargetLaser { get; private set; }
        protected Transform TargetView { get; private set; }
        protected Vector3 Home { get; set; }
        protected Vector3 VisualRest { get; private set; }
        protected Quaternion VisualRestRotation { get; private set; }
        protected bool Started { get; private set; }
        protected float DeathTime { get; private set; }
        protected float LastSeen = float.NegativeInfinity;

        public bool IsDead { get; private set; }

        private readonly RaycastHit[] sightHits = new RaycastHit[16];
        private Renderer[] renderers;
        private Collider[] colliders;
        private MaterialPropertyBlock block;
        private float flashUntil, stunnedUntil;
        private bool flashing, wasStunned;

        /// <summary>True while dazed by the mining laser's combo finisher; its state machine pauses meanwhile.</summary>
        public bool IsStunned => Time.time < stunnedUntil;

        /// <summary>Mini-bosses share this base class but are too big to be stunned by the combo.</summary>
        protected virtual bool CanBeStunned => true;

        // ---------- Unity ----------

        protected virtual void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Health = GetComponent<Health>();
            if (visual == null) visual = transform.Find("Visual");
            if (visual == null) visual = transform;
            VisualRest = visual.localPosition;
            VisualRestRotation = visual.localRotation;
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            block = new MaterialPropertyBlock();
            Home = transform.position;
        }

        protected virtual void OnEnable()
        {
            Health.Damaged += HandleDamaged;
            Health.Died += HandleDied;
        }

        protected virtual void OnDisable()
        {
            Health.Damaged -= HandleDamaged;
            Health.Died -= HandleDied;
        }

        protected virtual void Start()
        {
            if (target == null)
            {
                var player = FindAnyObjectByType<FirstPersonController>();
                if (player != null) target = player.transform;
            }
            if (target != null)
            {
                TargetHealth = target.GetComponentInParent<Health>();
                TargetBody = target.GetComponentInParent<CharacterController>();
                TargetLaser = target.GetComponentInParent<MiningLaser>();
                Camera view = target.GetComponentInChildren<Camera>();
                TargetView = view != null ? view.transform : target;
            }

            Agent.enabled = true;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                Agent.Warp(hit.position);
                Home = hit.position;
            }
            else
            {
                Debug.LogWarning(name + " is not near a NavMesh; it will stand still. Is there a NavMeshBaker in the scene?", this);
            }
            Started = true;
            OnStarted();
        }

        private void Update()
        {
            if (!Started) return;
            if (IsDead)
            {
                UpdateDeath(Time.time - DeathTime);
                if (Time.time - DeathTime >= corpseTime) Destroy(gameObject);
                return;
            }
            if (IsStunned)
            {
                // Dizzy: stands still, spinning and wobbling in place.
                wasStunned = true;
                Halt();
                float spin = (Time.time * 220f) % 360f;
                visual.localRotation = VisualRestRotation * Quaternion.Euler(0f, spin, Mathf.Sin(Time.time * 14f) * 12f);
                return;
            }
            if (wasStunned)
            {
                wasStunned = false;
                visual.localRotation = VisualRestRotation;
            }
            Tick(Time.deltaTime);
        }

        public void Stun(float seconds)
        {
            if (!CanBeStunned || IsDead || seconds <= 0f) return;
            stunnedUntil = Mathf.Max(stunnedUntil, Time.time + seconds);
        }

        protected virtual void LateUpdate()
        {
            bool flash = Time.time < flashUntil;
            if (flash || flashing)
            {
                foreach (Renderer item in renderers)
                {
                    if (item == null) continue;
                    if (flash)
                    {
                        item.GetPropertyBlock(block);
                        block.SetColor(BaseColor, Color.white);
                        item.SetPropertyBlock(block);
                    }
                    else item.SetPropertyBlock(null);
                }
                flashing = flash;
            }
            if (!flash) Paint();
        }

        // ---------- For each enemy ----------

        /// <summary>Called once in Start after the NavMesh placement.</summary>
        protected virtual void OnStarted() { }

        /// <summary>The enemy's state machine; runs every frame while alive.</summary>
        protected abstract void Tick(float dt);

        /// <summary>Called after every hit that did not kill it.</summary>
        protected virtual void OnHit(float amount, GameObject source, Vector3 away) { }

        /// <summary>Called once when it dies, after colliders and the agent are switched off.</summary>
        protected virtual void OnDeath() { }

        /// <summary>Death animation; t is seconds since death. Default: flop onto its back and sink.</summary>
        protected virtual void UpdateDeath(float t)
        {
            float flip = Mathf.SmoothStep(0f, 1f, t / 0.35f);
            float sink = Mathf.Clamp01((t - corpseTime * 0.6f) / (corpseTime * 0.4f));
            visual.localRotation = VisualRestRotation * Quaternion.Euler(0f, 0f, 180f * flip);
            visual.localPosition = VisualRest + Vector3.up * (0.25f * Mathf.Sin(flip * Mathf.PI) - 0.5f * sink);
        }

        /// <summary>Runs after the hit flash each frame, for enemies that tint parts themselves (glowing cans).</summary>
        protected virtual void Paint() { }

        // ---------- Shared helpers ----------

        protected bool TargetAlive => target != null && (TargetHealth == null || !TargetHealth.IsDead);
        protected Transform Target => target;
        protected Transform Visual => visual;

        /// <summary>True while a Dish Meerkat has spotted the player.</summary>
        protected bool Alerted => SpotterAlert.IsMarked;
        protected float SpeedScale => Alerted ? alertedSpeedBonus : 1f;

        /// <summary>
        /// True if the player is close enough and visible to be noticed. While spotted by a meerkat, every fodder
        /// enemy within the alerted range knows where the player is, walls or not.
        /// </summary>
        protected bool Notices()
        {
            if (!TargetAlive) return false;
            if (Alerted && FlatDistance(target.position) <= alertedDetectRange) return true;
            return CanSee(detectRange);
        }

        protected float FlatDistance(Vector3 point)
        {
            Vector3 offset = point - transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }

        protected Vector3 FlatDirectionTo(Vector3 point)
        {
            Vector3 offset = point - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude > 0.0001f ? offset.normalized : transform.forward;
        }

        /// <summary>Line of sight from this enemy to the player's chest. Other creatures don't block it.</summary>
        protected bool CanSee(float range)
        {
            if (target == null || FlatDistance(target.position) > range) return false;
            Vector3 eye = transform.position + Vector3.up * 0.5f;
            Vector3 look = target.position + Vector3.up * 1.4f;
            Vector3 toLook = look - eye;
            float distance = toLook.magnitude;
            if (distance < 0.01f) return true;
            int count = Physics.RaycastNonAlloc(eye, toLook / distance, sightHits, distance, sightMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform hit = sightHits[i].collider.transform;
                if (hit.IsChildOf(transform) || hit.IsChildOf(target)) continue;
                if (hit.GetComponentInParent<DesertFodder>() != null || hit.GetComponentInParent<HermitCrab>() != null ||
                    hit.GetComponentInParent<MortarFrog>() != null) continue;
                return false;
            }
            return true;
        }

        protected void TurnToward(Vector3 point, float maxDegrees)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), maxDegrees);
        }

        protected bool AgentReady => Agent != null && Agent.enabled && Agent.isOnNavMesh;

        protected void Chase(Vector3 point, float speed)
        {
            if (!AgentReady) return;
            Agent.isStopped = false;
            Agent.speed = speed * SpeedScale;
            Agent.SetDestination(point);
        }

        protected void Halt()
        {
            if (!AgentReady) return;
            Agent.isStopped = true;
            Agent.velocity = Vector3.zero;
        }

        /// <summary>Turns this enemy's colliders on or off (the hiding meerkat and burrowed eel use this).</summary>
        protected void SetColliders(bool on, Collider except = null)
        {
            foreach (Collider item in colliders)
                if (item != null && item != except) item.enabled = on;
        }

        protected void SetRenderersVisible(bool on)
        {
            foreach (Renderer item in renderers)
                if (item != null) item.enabled = on;
        }

        /// <summary>Kills it through Health, so death events and the spawner see it like any other kill.</summary>
        protected void Kill(GameObject source = null)
        {
            if (!Health.IsDead) Health.TakeDamage(Health.Current, source);
        }

        // ---------- Health events ----------

        private void HandleDamaged(Health _, float amount, GameObject source)
        {
            if (IsDead) return;
            flashUntil = Time.time + flashTime;
            LastSeen = Time.time; // being hit always draws its attention
            Vector3 away = source != null ? transform.position - source.transform.position : -transform.forward;
            away.y = 0f;
            away = away.sqrMagnitude > 0.001f ? away.normalized : -transform.forward;
            if (Health.IsDead) return;
            if (hitKnockback > 0f && AgentReady) Agent.Move(away * hitKnockback * Mathf.Clamp(amount / 20f, 0.3f, 1.5f));
            OnHit(amount, source, away);
        }

        private void HandleDied(Health _)
        {
            if (IsDead) return;
            IsDead = true;
            DeathTime = Time.time;
            SetColliders(false);
            if (AgentReady) Agent.isStopped = true;
            if (Agent != null) Agent.enabled = false;
            OnDeath();
        }
    }
}
