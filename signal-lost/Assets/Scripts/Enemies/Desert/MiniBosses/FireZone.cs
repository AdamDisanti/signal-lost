using System;
using System.Collections.Generic;
using SignalLost.Combat;
using UnityEngine;

namespace SignalLost.Enemies.Desert
{
    /// <summary>How a fire looks and hurts. Set on the mini-boss that makes the fire.</summary>
    [Serializable]
    public sealed class FireStyle
    {
        [Tooltip("Flames (bright).")]
        public Material flame;
        [Tooltip("Glowing ground under the flames.")]
        public Material embers;
        [Min(0f)] public float damage = 7f;
        [Tooltip("Seconds between damage ticks.")]
        [Min(0.05f)] public float interval = 0.5f;
        [Min(0.1f)] public float duration = 4f;
        public LayerMask mask = ~0;
    }

    /// <summary>
    /// A patch of fire on the ground: Burnout's flame streak and exhaust, Sludge's burning oil. Every tick it damages
    /// each Health standing in it once (the player, fodder, and mini-bosses) and sets off anything
    /// <see cref="IIgnitable"/> (oil slicks, Jerry-Can Beetles), so fire spreads across slicks and chains into beetles.
    ///
    /// A fire can be immune to the one who started it (Burnout running through its own streak, Sludge lighting its own
    /// oil). Fires the player starts (shooting a slick) or a beetle blast starts hurt everyone, mini-bosses included.
    /// Visuals are placeholder flickering primitives.
    /// </summary>
    public sealed class FireZone : MonoBehaviour
    {
        private static readonly Collider[] Overlaps = new Collider[64];
        private static readonly HashSet<Health> Burned = new HashSet<Health>();
        private static readonly List<IIgnitable> ToIgnite = new List<IIgnitable>();

        private float radius, life, age, damage, interval, nextTick;
        private GameObject source;
        private Transform immune;
        private LayerMask mask;
        private Transform[] flames = new Transform[0];
        private Vector3[] flameSizes = new Vector3[0];
        private Transform embers;

        /// <param name="source">Credited with the damage (can be null).</param>
        /// <param name="immune">This object and its children are not hurt (can be null).</param>
        public static FireZone Spawn(FireStyle style, Vector3 point, float radius, GameObject source, Transform immune,
            float duration = -1f)
        {
            var go = new GameObject("Fire");
            go.transform.position = point;
            var fire = go.AddComponent<FireZone>();
            fire.radius = Mathf.Max(0.2f, radius);
            fire.life = duration > 0f ? duration : style.duration;
            fire.damage = style.damage;
            fire.interval = style.interval;
            fire.mask = style.mask;
            fire.source = source;
            fire.immune = immune;
            fire.nextTick = Time.time + 0.15f; // a beat to see it before it hurts
            fire.Build(style);
            return fire;
        }

        private void Build(FireStyle style)
        {
            GameObject disc = MiningLaser.VisualPrimitive(PrimitiveType.Cylinder, "Embers", style.embers);
            embers = disc.transform;
            embers.SetParent(transform, false);
            embers.localPosition = Vector3.up * 0.02f;
            embers.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);

            int count = Mathf.Clamp(Mathf.RoundToInt(radius * 4f), 3, 12);
            flames = new Transform[count];
            flameSizes = new Vector3[count];
            var random = new System.Random(UnityEngine.Random.Range(0, int.MaxValue));
            for (int i = 0; i < count; i++)
            {
                GameObject flame = MiningLaser.VisualPrimitive(PrimitiveType.Sphere, "Flame", style.flame);
                Transform t = flame.transform;
                t.SetParent(transform, false);
                float a = (float)random.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Sqrt((float)random.NextDouble()) * radius * 0.8f;
                float size = 0.2f + (float)random.NextDouble() * 0.25f + radius * 0.08f;
                t.localPosition = new Vector3(Mathf.Cos(a) * r, size, Mathf.Sin(a) * r);
                flameSizes[i] = new Vector3(size, size * 2.6f, size);
                t.localScale = flameSizes[i];
                flames[i] = t;
            }
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= life)
            {
                Destroy(gameObject);
                return;
            }

            // Grow in quickly, flicker, shrink away at the end.
            float grow = Mathf.Clamp01(age / 0.2f) * Mathf.Clamp01((life - age) / 0.6f);
            for (int i = 0; i < flames.Length; i++)
            {
                float flicker = 0.65f + 0.55f * Mathf.PerlinNoise(Time.time * 7f + i * 3.1f, i * 0.7f);
                Vector3 size = flameSizes[i] * grow;
                flames[i].localScale = new Vector3(size.x, size.y * flicker, size.z);
                Vector3 p = flames[i].localPosition;
                flames[i].localPosition = new Vector3(p.x, size.y * flicker * 0.4f, p.z);
            }
            if (embers != null) embers.localScale = new Vector3(radius * 2f * grow, 0.01f, radius * 2f * grow);

            if (Time.time < nextTick) return;
            nextTick = Time.time + interval;
            Burn();
        }

        private void Burn()
        {
            Burned.Clear();
            ToIgnite.Clear();
            Vector3 bottom = transform.position + Vector3.up * 0.3f;
            Vector3 top = transform.position + Vector3.up * 1.4f;
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius * 0.9f, Overlaps, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = Overlaps[i];
                if (immune != null && hit.transform.IsChildOf(immune)) continue;
                var ignitable = hit.GetComponentInParent<IIgnitable>();
                if (ignitable != null && !ToIgnite.Contains(ignitable)) ToIgnite.Add(ignitable);
                Health health = hit.GetComponentInParent<Health>();
                if (health == null || health.IsDead || !Burned.Add(health)) continue;
                health.TakeDamage(damage, source);
            }
            foreach (IIgnitable item in ToIgnite)
                if (item is UnityEngine.Object unityObject && unityObject != null) item.Ignite(source);
        }
    }
}
