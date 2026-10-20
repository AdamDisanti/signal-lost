using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Player;
using UnityEngine;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// Something fire can set off: a Jerry-Can Beetle's fuel can. Explosions ignite these in their radius, and the
    /// future fuel scorpion's oil fires can call it too.
    /// </summary>
    public interface IIgnitable
    {
        void Ignite(GameObject source);
    }

    /// <summary>
    /// A blast that damages every Health in its radius once, the player and enemies alike, with damage falling off
    /// toward the edge. It also ignites anything <see cref="IIgnitable"/> nearby, which is how beetles chain-react.
    /// The visual is a placeholder flash and an expanding ring.
    /// </summary>
    public static class FodderExplosion
    {
        /// <param name="edgeDamage">Fraction of the damage still dealt at the very edge of the radius (0 to 1).</param>
        /// <param name="ignore">This object and its children are not damaged (the exploding beetle itself).</param>
        public static void Explode(Vector3 point, float radius, float damage, float edgeDamage, GameObject source,
            Transform ignore, Material flashMaterial, LayerMask mask, Knockback knockback = default, bool knockbackEnemies = false)
        {
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius)) return;
            // Per-blast collections keep nested death/chain explosions from corrupting the outer blast.
            var damaged = new HashSet<Health>();
            var ignited = new HashSet<IIgnitable>();
            foreach (Collider hit in Physics.OverlapSphere(point, radius, mask, QueryTriggerInteraction.Ignore))
            {
                if (ignore != null && hit.transform.IsChildOf(ignore)) continue;

                var ignitable = hit.GetComponentInParent<IIgnitable>();
                if (ignitable != null) ignited.Add(ignitable);

                Health health = hit.GetComponentInParent<Health>();
                if (health == null || health.IsDead || !damaged.Add(health)) continue;
                float distance = Vector3.Distance(point, hit.ClosestPoint(point));
                float falloff = Mathf.Lerp(1f, Mathf.Clamp01(edgeDamage), Mathf.Clamp01(distance / radius));
                Knockback motion = knockbackEnemies || health.GetComponent<FirstPersonController>() != null ? knockback : default;
                health.TakeDamage(damage * falloff, source, motion, health.transform.position - point);
            }

            foreach (IIgnitable item in ignited)
                if (item is Object unityObject && unityObject != null) item.Ignite(source);

            ImpactFlash.Spawn(flashMaterial, point + Vector3.up * 0.4f, radius * 0.9f, 0.22f);
            ExpandingRing.Spawn(flashMaterial, point + Vector3.up * 0.05f, radius, 0.3f, 0.12f);
        }
    }
}
