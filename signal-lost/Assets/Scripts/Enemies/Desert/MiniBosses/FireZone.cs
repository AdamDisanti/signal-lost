using System;
using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Effects;
using UnityEngine;

namespace SignalLost.Enemies.Desert
{
    /// <summary>How a fire looks and hurts. Set on the mini-boss that makes the fire.</summary>
    [Serializable]
    public sealed class FireStyle
    {
        [Tooltip("Multiplies the flame colours. White is an ordinary orange fire.")]
        public Color tint = Color.white;
        [Tooltip("How much smoke rises off it (burning fuel is smokier).")]
        [Range(0f, 1f)] public float smoke = 0.45f;
        [Tooltip("Flame density and brightness. Lower it for fires that appear in large numbers.")]
        [Range(0.2f, 2f)] public float fierceness = 1f;
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
    /// It looks like a <see cref="FireEffect"/> (the general particle fire); this adds the damage on top.
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
        private FireEffect effect;
        private bool spent;

        /// <param name="source">Credited with the damage (can be null).</param>
        /// <param name="immune">This object and its children are not hurt (can be null).</param>
        public static FireZone Spawn(FireStyle style, Vector3 point, float radius, GameObject source, Transform immune,
            float duration = -1f)
        {
            style ??= new FireStyle();
            radius = Mathf.Max(0.2f, radius);
            // The effect owns the GameObject: once the fire is spent it dies down and removes it.
            // Data saved before these settings existed reads as black and 0: fall back to an ordinary fire.
            Color tint = style.tint.a > 0f ? style.tint : Color.white;
            float fierceness = style.fierceness > 0f ? style.fierceness : 1f;
            FireEffect effect = FireEffect.Spawn(point, radius, 0f, 0f, null, tint, style.smoke, fierceness);
            effect.Strength = 0f;
            var fire = effect.gameObject.AddComponent<FireZone>();
            fire.effect = effect;
            fire.radius = radius;
            fire.life = duration > 0f ? duration : style.duration;
            fire.damage = style.damage;
            fire.interval = style.interval;
            fire.mask = style.mask;
            fire.source = source;
            fire.immune = immune;
            fire.nextTick = Time.time + 0.15f; // a beat to see it before it hurts
            return fire;
        }

        private void Update()
        {
            if (spent) return;
            age += Time.deltaTime;
            if (age >= life)
            {
                spent = true;
                if (effect != null) effect.Extinguish();
                else Destroy(gameObject);
                return;
            }

            // Flares up quickly, dies down over its last moments.
            if (effect != null) effect.Strength = Mathf.Clamp01(age / 0.25f) * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01((life - age) / 0.8f));

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
                if (!EnemyAllegiance.CanHurt(source, health)) continue; // enemy fire never burns enemies
                health.TakeDamage(damage, source);
            }
            foreach (IIgnitable item in ToIgnite)
                if (item is UnityEngine.Object unityObject && unityObject != null) item.Ignite(source);
        }
    }
}
