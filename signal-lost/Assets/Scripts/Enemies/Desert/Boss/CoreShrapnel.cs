using SignalLost.Combat;
using SignalLost.Effects;
using UnityEngine;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// A white-hot piece of shrapnel fired by the Coreworm's plating in barrage patterns. It flies flat at chest height
    /// (so jumping doesn't help: weave between them), hurts whatever it hits, and stops on scenery (cover works).
    /// </summary>
    public sealed class CoreShrapnel : MonoBehaviour
    {
        private const float Radius = 0.3f;

        private Vector3 velocity;
        private float damage, life;
        private Transform owner;
        private LayerMask mask;

        public static CoreShrapnel Fire(Material material, Vector3 from, Vector3 direction, float speed, float damage, Transform owner, LayerMask mask)
        {
            GameObject body = MiningLaser.VisualPrimitive(PrimitiveType.Sphere, "Core Shrapnel", material);
            body.transform.position = from;
            body.transform.localScale = Vector3.one * Radius * 1.6f;
            direction.y = 0f;
            var shard = body.AddComponent<CoreShrapnel>();
            shard.velocity = (direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward) * speed;
            shard.damage = damage;
            shard.owner = owner;
            shard.mask = mask;
            shard.life = 6f;

            var trail = body.AddComponent<TrailRenderer>();
            trail.sharedMaterial = EffectMaterials.Additive(EffectMaterials.Look.Glow);
            trail.time = 0.18f;
            trail.startWidth = Radius * 1.4f;
            trail.endWidth = 0f;
            trail.startColor = new Color(1f, 0.85f, 0.5f, 1f);
            trail.endColor = new Color(1f, 0.35f, 0.1f, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return shard;
        }

        private void Update()
        {
            life -= Time.deltaTime;
            if (life <= 0f)
            {
                Destroy(gameObject);
                return;
            }
            Vector3 step = velocity * Time.deltaTime;
            float nearest = float.MaxValue;
            RaycastHit best = default;
            foreach (RaycastHit hit in Physics.SphereCastAll(transform.position, Radius, velocity.normalized, step.magnitude, mask, QueryTriggerInteraction.Ignore))
            {
                if (owner != null && hit.collider.transform.IsChildOf(owner)) continue;
                if (hit.collider.GetComponentInParent<OilSlick>() != null) continue;
                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                    best = hit;
                }
            }
            if (nearest < float.MaxValue)
            {
                Health target = best.collider.GetComponentInParent<Health>();
                if (target != null && !target.IsDead) target.TakeDamage(damage, owner != null ? owner.gameObject : null);
                Vector3 at = best.point.sqrMagnitude > 0f ? best.point : transform.position;
                ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, at, -velocity, 6, 0.08f, 4f, 50f);
                Destroy(gameObject);
                return;
            }
            transform.position += step;
        }
    }
}
