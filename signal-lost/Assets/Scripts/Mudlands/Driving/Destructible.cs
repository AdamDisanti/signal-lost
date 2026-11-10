using System;
using System.Collections.Generic;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Enemies.Desert;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// Something in the world that breaks: wooden barricades, fences, crates, hay bales, outhouses, the moonshine
    /// still and moonshine barrels. It has a <see cref="Health"/>, so the laser breaks it like anything else (it just
    /// takes a few hits); the Drunk Driving car smashes straight through it at speed. Broken, its parts fly off as
    /// tumbling debris and fade. Explosive ones (barrels, the still) blow up when they break, and fire lights them.
    /// Quests listen to <see cref="Broken"/>.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class Destructible : MonoBehaviour, IIgnitable
    {
        public enum Substance { Wood, Hay, Metal, Glass }

        [SerializeField] private Substance kind = Substance.Wood;
        [Tooltip("The car smashes through it at this speed (m/s) or faster; slower, it is a solid bump.")]
        [SerializeField, Min(0f)] private float carSmashSpeed = 4f;
        [Tooltip("Share of the car's speed lost when it smashes through.")]
        [SerializeField, Range(0f, 1f)] private float carSpeedLoss = 0.18f;
        [Header("Explosive")]
        [SerializeField] private bool explosive;
        [SerializeField, Min(0f)] private float blastRadius = 4f;
        [SerializeField, Min(0f)] private float blastDamage = 30f;
        [Tooltip("Seconds of fire left behind after an explosion (0: none).")]
        [SerializeField, Min(0f)] private float fireSeconds = 3f;

        private Health health;
        private bool broken;
        private GameObject lastAttacker;

        /// <summary>Raised when any destructible breaks.</summary>
        public static event Action<Destructible> AnyBroken;
        public event Action<Destructible> Broken;

        public bool IsBroken => broken;
        public float CarSmashSpeed => carSmashSpeed;
        public float CarSpeedLoss => carSpeedLoss;
        public bool Explosive => explosive;
        public Health Health => health;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => AnyBroken = null;

        /// <summary>Sets one up in code (the level builder and runtime props).</summary>
        public void Configure(Substance material, float hp, bool isExplosive = false, float radius = 4f, float damage = 30f)
        {
            kind = material;
            explosive = isExplosive;
            blastRadius = radius;
            blastDamage = damage;
            if (health == null) health = GetComponent<Health>();
            health.SetMax(hp);
            health.Restore();
        }

        private void Awake() => health = GetComponent<Health>();

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        private void OnDamaged(Health _, float amount, GameObject source)
        {
            lastAttacker = source;
            if (!broken) Wobble(Mathf.Clamp01(amount / 30f));
        }

        private void OnDied(Health _) => Break(lastAttacker, Vector3.zero);

        /// <summary>The car smashed into it.</summary>
        public void SmashByCar(DrunkCar car, Vector3 velocity)
        {
            if (broken) return;
            lastAttacker = car != null && car.Driver != null ? car.Driver.gameObject : null;
            Break(lastAttacker, velocity);
        }

        public void Ignite(GameObject source)
        {
            if (!explosive || broken) return;
            Break(source, Vector3.zero);
        }

        private float wobbleUntil;
        private Quaternion restRotation;

        private void Wobble(float amount)
        {
            if (Time.time >= wobbleUntil) restRotation = transform.localRotation;
            wobbleUntil = Time.time + 0.25f;
            Sfx.PlayAt(kind == Substance.Metal ? "crab_block" : "wood_smash", transform.position, 0.35f, Random.Range(1.3f, 1.6f), 25f);
            ParticleBurst.Spawn(kind == Substance.Hay ? ParticleBurst.Kind.Sand : ParticleBurst.Kind.Smoke, transform.position + Vector3.up * 0.8f,
                Vector3.up, 6, 0.3f, 2f, 60f, DustColor);
            wobbleAmount = 4f + amount * 8f;
        }

        private float wobbleAmount;

        private void Update()
        {
            if (broken || Time.time >= wobbleUntil) return;
            float k = (wobbleUntil - Time.time) / 0.25f;
            transform.localRotation = restRotation * Quaternion.Euler(Mathf.Sin(Time.time * 60f) * wobbleAmount * k, 0f, 0f);
            if (Time.time + Time.deltaTime >= wobbleUntil) transform.localRotation = restRotation;
        }

        private Color DustColor => kind switch
        {
            Substance.Hay => new Color(0.85f, 0.75f, 0.4f, 0.8f),
            Substance.Metal => new Color(0.5f, 0.5f, 0.5f, 0.6f),
            Substance.Glass => new Color(0.8f, 0.6f, 0.3f, 0.6f),
            _ => new Color(0.55f, 0.45f, 0.32f, 0.7f)
        };

        private void Break(GameObject attacker, Vector3 push)
        {
            if (broken) return;
            broken = true;
            if (!health.IsDead) health.Invulnerable = true;
            Vector3 centre = Centre;
            foreach (Collider part in GetComponentsInChildren<Collider>()) part.enabled = false;
            var obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle != null) obstacle.enabled = false;

            Sfx.PlayAt(kind == Substance.Glass ? "bottle_smash" : kind == Substance.Metal ? "car_crash" : "wood_smash", centre, 0.9f,
                Random.Range(0.85f, 1.1f), 40f);
            ParticleBurst.Spawn(kind == Substance.Hay ? ParticleBurst.Kind.Sand : ParticleBurst.Kind.Smoke, centre, Vector3.up, 26, 0.7f, 4f, 70f, DustColor);
            Shatter(push);

            if (explosive)
            {
                AreaBlastFor(centre, attacker);
                if (fireSeconds > 0f)
                    FireZone.Spawn(new FireStyle { damage = 6f, duration = fireSeconds }, centre, blastRadius * 0.5f, attacker, null);
            }
            Broken?.Invoke(this);
            AnyBroken?.Invoke(this);
        }

        private Vector3 Centre
        {
            get
            {
                var bounds = new Bounds(transform.position + Vector3.up * 0.5f, Vector3.zero);
                bool any = false;
                foreach (Renderer part in GetComponentsInChildren<Renderer>())
                {
                    if (!any) { bounds = part.bounds; any = true; }
                    else bounds.Encapsulate(part.bounds);
                }
                return bounds.center;
            }
        }

        private void AreaBlastFor(Vector3 centre, GameObject attacker)
        {
            // Credited to whoever set it off: the player's shots, fires and car make it hurt enemies.
            ParticleBurst.Explosion(centre, blastRadius);
            CameraShake.Add(0.35f);
            Sfx.PlayAt("car_explode", centre, 0.7f, Random.Range(1.15f, 1.3f), 50f);
            var hit = new HashSet<Health>();
            foreach (Collider collider in Physics.OverlapSphere(centre, blastRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                Health target = collider.GetComponentInParent<Health>();
                if (target == null || target == health || target.IsDead || !hit.Add(target)) continue;
                if (!SignalLost.Enemies.EnemyAllegiance.CanHurt(attacker, target)) continue;
                float share = Mathf.Lerp(1f, 0.4f, Mathf.Clamp01(Vector3.Distance(centre, collider.ClosestPoint(centre)) / blastRadius));
                target.TakeDamage(blastDamage * share, attacker);
            }
            foreach (Collider collider in Physics.OverlapSphere(centre, blastRadius, ~0, QueryTriggerInteraction.Collide))
            {
                var vent = collider.GetComponentInParent<BogGasVent>();
                if (vent != null) vent.Ignite(attacker);
                var other = collider.GetComponentInParent<Destructible>();
                if (other != null && other != this && other.explosive) other.Invoke(nameof(ChainIgnite), 0.15f);
            }
        }

        private void ChainIgnite() => Ignite(lastAttacker);

        // Every renderer becomes a tumbling piece of debris that fades away.
        private void Shatter(Vector3 push)
        {
            Vector3 centre = Centre;
            foreach (MeshRenderer part in GetComponentsInChildren<MeshRenderer>())
            {
                Transform piece = part.transform;
                piece.SetParent(null, true);
                var body = piece.gameObject.AddComponent<Rigidbody>();
                body.mass = 5f;
                var shape = piece.gameObject.AddComponent<BoxCollider>();
                shape.size = Vector3.Scale(shape.size, new Vector3(0.8f, 0.8f, 0.8f));
                Vector3 away = piece.position - centre + Random.insideUnitSphere * 0.4f;
                away.y = Mathf.Abs(away.y) + 0.6f;
                body.linearVelocity = away.normalized * Random.Range(3f, 7f) + push * 0.55f + Vector3.up * Random.Range(2f, 4f);
                body.angularVelocity = Random.insideUnitSphere * 9f;
                piece.gameObject.AddComponent<Debris>().Begin(Random.Range(2.5f, 3.5f));
            }
        }
    }

    /// <summary>A broken piece: tumbles, then sinks into the ground and is removed.</summary>
    public sealed class Debris : MonoBehaviour
    {
        private float die, sinkFrom;

        public void Begin(float seconds)
        {
            die = Time.time + seconds;
            sinkFrom = die - 0.8f;
            // Debris never pushes the player or creatures around.
            int layer = LayerMask.NameToLayer("Ignore Raycast");
            if (layer >= 0) gameObject.layer = layer;
        }

        private void Update()
        {
            if (Time.time >= die) { Destroy(gameObject); return; }
            if (Time.time < sinkFrom) return;
            var body = GetComponent<Rigidbody>();
            if (body != null) { Destroy(body); foreach (Collider c in GetComponents<Collider>()) Destroy(c); }
            transform.position += Vector3.down * Time.deltaTime * 0.8f;
            transform.localScale *= 1f - Time.deltaTime * 1.2f;
        }
    }

    /// <summary>
    /// A bubbling bog-gas vent: a full-charge bolt, a fire, a car explosion or a blast sets it off, a fireball for
    /// 25 in 3.5 m credited to whoever set it off, then it refills for 12 s. Hazard for both sides.
    /// </summary>
    public sealed class BogGasVent : MonoBehaviour, IIgnitable
    {
        [SerializeField, Min(0.5f)] private float radius = 3.5f;
        [SerializeField, Min(0f)] private float damage = 25f;
        [SerializeField, Min(1f)] private float refill = 12f;

        private float readyAt, nextBubble;
        private Health ownHealth;

        public bool Ready => Time.time >= readyAt;

        private void Awake()
        {
            // A big hitbox that soaks up bolts: a hard enough hit lights it.
            ownHealth = GetComponent<Health>();
            if (ownHealth != null) ownHealth.Damaged += (h, amount, source) =>
            {
                h.Restore();
                if (amount >= 25f) Ignite(source);
            };
        }

        private void Update()
        {
            if (!Ready || Time.time < nextBubble) return;
            nextBubble = Time.time + Random.Range(0.4f, 1.1f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Mist, transform.position + Random.insideUnitSphere.normalized * 0.6f, Vector3.up, 3, 0.35f, 0.8f,
                20f, new Color(0.55f, 0.75f, 0.35f, 0.5f));
        }

        public void Ignite(GameObject source)
        {
            if (!Ready) return;
            readyAt = Time.time + refill;
            Vector3 at = transform.position + Vector3.up * 0.6f;
            ParticleBurst.Explosion(at, radius);
            ParticleBurst.Spawn(ParticleBurst.Kind.Fireball, at, Vector3.up, 30, 1.2f, 7f, 30f, new Color(0.7f, 1f, 0.4f));
            CameraShake.Add(0.3f);
            Sfx.PlayAt("car_explode", at, 0.6f, 1.4f, 45f);
            var hit = new HashSet<Health>();
            foreach (Collider collider in Physics.OverlapSphere(at, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                Health target = collider.GetComponentInParent<Health>();
                if (target == null || target == ownHealth || target.IsDead || !hit.Add(target)) continue;
                if (!SignalLost.Enemies.EnemyAllegiance.CanHurt(source, target)) continue;
                target.TakeDamage(damage, source);
            }
        }
    }
}
