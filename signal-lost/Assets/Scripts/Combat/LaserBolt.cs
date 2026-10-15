using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Combat
{
    /// <summary>
    /// The mining laser's charged shot: a glowing bolt that flies straight, damages the first thing with
    /// Health it touches, and disappears on any hit or after travelling its range. It moves by sphere-casting
    /// each frame instead of using physics, so fast bolts cannot pass through thin walls. Something with an
    /// <see cref="IBoltReflector"/> can bounce it back instead; the bounced bolt can then hit whoever fired it.
    /// </summary>
    public sealed class LaserBolt : MonoBehaviour
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        private Transform owner;
        private Action<Health> onHit;
        private Vector3 direction, previous;
        private float damage, speed, radius, remaining, range;
        private int reflections;

        private const int MaxReflections = 3; // stops bolts bouncing between two reflectors forever
        private LayerMask mask;

        /// <summary>
        /// Creates and launches a bolt. <paramref name="castFrom"/> is where collision checks begin on the first
        /// frame (the camera), so a bolt fired with the tool tip inside a wall still hits that wall.
        /// </summary>
        public static LaserBolt Spawn(Material material, Vector3 position, Vector3 direction, Vector3 castFrom,
            float damage, float speed, float radius, float range, LayerMask mask, Transform owner, Action<Health> onHit)
        {
            GameObject body = MiningLaser.VisualPrimitive(PrimitiveType.Sphere, "Laser Bolt", material);
            body.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            body.transform.localScale = Vector3.one * radius * 2f;

            var trail = body.AddComponent<TrailRenderer>();
            if (material != null) trail.sharedMaterial = material;
            trail.time = 0.12f;
            trail.startWidth = radius * 1.6f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.05f;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;

            var bolt = body.AddComponent<LaserBolt>();
            bolt.owner = owner;
            bolt.onHit = onHit;
            bolt.direction = direction.normalized;
            bolt.previous = castFrom;
            bolt.damage = damage;
            bolt.speed = speed;
            bolt.radius = radius;
            bolt.remaining = range;
            bolt.range = range;
            bolt.mask = mask;
            return bolt;
        }

        private void Update()
        {
            float step = Mathf.Min(speed * Time.deltaTime, remaining);
            Vector3 next = transform.position + direction * step;
            remaining -= step;

            Vector3 path = next - previous;
            float length = path.magnitude;
            if (length > 0f && TryHit(previous, path / length, length, out RaycastHit hit))
            {
                Impact(hit);
                return;
            }

            previous = next;
            transform.position = next;
            if (remaining <= 0f) Destroy(gameObject);
        }

        private bool TryHit(Vector3 from, Vector3 along, float length, out RaycastHit nearest)
        {
            nearest = default;
            bool found = false;
            int count = Physics.SphereCastNonAlloc(from, radius, along, Hits, length, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (owner != null && Hits[i].collider.transform.IsChildOf(owner)) continue;
                if (found && Hits[i].distance >= nearest.distance) continue;
                nearest = Hits[i];
                found = true;
            }
            return found;
        }

        private void Impact(RaycastHit hit)
        {
            // A sphere cast that starts overlapping something reports no contact point; use the bolt's position.
            Vector3 point = hit.distance > 0f ? hit.point : transform.position;

            var reflector = hit.collider.GetComponentInParent<IBoltReflector>();
            if (reflector != null && reflections < MaxReflections &&
                reflector.TryReflect(point, direction, owner, out Vector3 bounce, out float damageMultiplier))
            {
                Reflect(point, bounce, ((Component)reflector).transform, damageMultiplier);
                return;
            }

            Health target = hit.collider.GetComponentInParent<Health>();
            if (target != null && target.TakeDamage(damage, owner != null ? owner.gameObject : null))
                onHit?.Invoke(target);

            var material = GetComponent<Renderer>().sharedMaterial;
            ImpactFlash.Spawn(material, point, radius * 4f);
            Destroy(gameObject);
        }

        /// <summary>
        /// Sends the bolt off in a new direction, owned by the reflector so it ignores the reflector and can hit the
        /// original shooter. It no longer reports hits to the shooter's laser (no hit marker for being hit yourself).
        /// </summary>
        private void Reflect(Vector3 point, Vector3 newDirection, Transform newOwner, float damageMultiplier)
        {
            reflections++;
            owner = newOwner;
            onHit = null;
            damage *= damageMultiplier;
            direction = newDirection.sqrMagnitude > 0.0001f ? newDirection.normalized : -direction;
            remaining = range;
            previous = point;
            transform.SetPositionAndRotation(point, Quaternion.LookRotation(direction));
            ImpactFlash.Spawn(GetComponent<Renderer>().sharedMaterial, point, radius * 3f, 0.08f);
        }
    }
}
