using System.Collections.Generic;
using SignalLost.Audio;
using SignalLost.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Melee
{
    /// <summary>
    /// An overcharge mark left on an enemy by a knuckle punch (Gaussian Brass Knuckles II): a crackling cyan ring that
    /// circles its middle for <see cref="Seconds"/>. Punching a marked enemy uses up the mark to throw chain lightning;
    /// a finisher nearby sets every mark off (<see cref="Detonate"/>), each bursting and hurting what is around it.
    /// </summary>
    public sealed class OverchargeMark : MonoBehaviour
    {
        public const float Seconds = 6f;
        private const int RingPoints = 20;

        private static readonly List<OverchargeMark> Live = new List<OverchargeMark>();

        private Health health;
        private LineRenderer ring;
        private float until, radius, height, redraw;

        /// <summary>Every mark in play (the finisher looks for nearby ones).</summary>
        public static IReadOnlyList<OverchargeMark> All => Live;
        public Health Health => health;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Live.Clear();

        public static bool Has(Health target) => target != null && target.GetComponent<OverchargeMark>() != null;

        /// <summary>Marks <paramref name="target"/> (or refreshes its mark).</summary>
        public static OverchargeMark Apply(Health target)
        {
            if (target == null || target.IsDead) return null;
            var mark = target.GetComponent<OverchargeMark>();
            if (mark == null) mark = target.gameObject.AddComponent<OverchargeMark>();
            mark.until = Time.time + Seconds;
            return mark;
        }

        /// <summary>Removes the mark from <paramref name="target"/>; true if it had one.</summary>
        public static bool Consume(Health target)
        {
            var mark = target != null ? target.GetComponent<OverchargeMark>() : null;
            if (mark == null) return false;
            Destroy(mark);
            return true;
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            Bounds bounds = new Bounds(transform.position + Vector3.up * 0.5f, Vector3.one);
            var colliders = GetComponentsInChildren<Collider>();
            if (colliders.Length > 0)
            {
                bounds = colliders[0].bounds;
                foreach (Collider c in colliders) bounds.Encapsulate(c.bounds);
            }
            radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z) + 0.25f, 0.45f, 3f);
            height = bounds.center.y - transform.position.y;
            ring = new GameObject("Overcharge Mark").AddComponent<LineRenderer>();
            ring.transform.SetParent(transform, false);
            ring.sharedMaterial = GaussFx.EnergyMaterial;
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = RingPoints;
            ring.widthMultiplier = 0.05f;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            Live.Add(this);
            if (health != null) health.Died += OnDied;
            Sfx.PlayAt("laser_charge", transform.position, 0.35f, 1.6f, 20f);
        }

        private void OnDestroy()
        {
            Live.Remove(this);
            if (health != null) health.Died -= OnDied;
            if (ring != null) Destroy(ring.gameObject);
        }

        private void OnDied(Health _) => Destroy(this);

        private void Update()
        {
            if (Time.time >= until) { Destroy(this); return; }
            if (Time.time < redraw) return;
            redraw = Time.time + 0.04f;
            // A jittery ring, tilted and spinning, that flickers faster as the mark runs out.
            float left = (until - Time.time) / Seconds;
            float spin = Time.time * 5f;
            Vector3 scale = transform.lossyScale;
            float sx = 1f / Mathf.Max(0.01f, scale.x), sy = 1f / Mathf.Max(0.01f, scale.y), sz = 1f / Mathf.Max(0.01f, scale.z);
            for (int i = 0; i < RingPoints; i++)
            {
                float a = i / (float)RingPoints * Mathf.PI * 2f + spin;
                float r = radius * (1f + Random.Range(-0.12f, 0.12f));
                var p = new Vector3(Mathf.Cos(a) * r, height + Mathf.Sin(a * 2f + spin) * 0.12f + Random.Range(-0.05f, 0.05f), Mathf.Sin(a) * r);
                ring.SetPosition(i, new Vector3(p.x * sx, p.y * sy, p.z * sz));
            }
            ring.enabled = left > 0.3f || Random.value > 0.35f;
            ring.widthMultiplier = 0.04f + 0.03f * Random.value;
        }

        /// <summary>Sets the mark off: a burst that hurts every enemy within <paramref name="reach"/> of it, then it's gone.</summary>
        public void Detonate(float damage, float reach, GameObject source)
        {
            Vector3 at = health != null ? MeleeTargets.Centre(health) : transform.position;
            GaussFx.Pop(at, Vector3.zero, 1.2f, 22);
            GaussFx.Ring(transform.position, reach, 0.35f, 0.25f);
            Sfx.PlayAt("laser_blast", at, 0.6f, Random.Range(1.2f, 1.4f), 35f);
            var nearby = MeleeTargets.Around(at, reach, source != null ? source.transform : null);
            foreach (Health target in nearby)
            {
                float share = Mathf.Lerp(1f, 0.5f, Vector3.Distance(at, target.transform.position) / reach);
                target.TakeDamage(damage * share, source);
                if (!target.IsDead) MeleeTargets.Shove(target, (target.transform.position - at).normalized * 1.2f * share);
            }
            Destroy(this);
        }
    }
}
