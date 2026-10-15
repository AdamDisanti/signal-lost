using SignalLost.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Enemies
{
    /// <summary>
    /// The mortar frog's projectile: a glob of glowing goo lobbed in a high arc at a point on the ground.
    /// A pulsing ring marks where it will land so the player can get out of the way. On impact it splashes,
    /// damaging every Health in range except the frog that fired it, and leaves a puddle that fades away.
    /// It sphere-casts along its path each frame, so it splashes early if it hits a wall or the player mid-air.
    /// Visuals are placeholders built from primitives.
    /// </summary>
    public sealed class GooBlob : MonoBehaviour
    {
        private const float Gravity = 9.8f;
        private const int RingPoints = 28;

        private static readonly RaycastHit[] Hits = new RaycastHit[16];
        private static readonly Collider[] Overlaps = new Collider[32];

        private Transform owner;
        private Material material;
        private Vector3 velocity, previous;
        private float radius, damage, splashRadius, lifeLeft;
        private LayerMask mask;
        private LineRenderer marker;
        private Vector3 landing;

        /// <summary>
        /// Launches a blob from <paramref name="from"/> so it lands on <paramref name="target"/> after
        /// <paramref name="flightTime"/> seconds.
        /// </summary>
        public static GooBlob Launch(Material material, Vector3 from, Vector3 target, float flightTime, float radius,
            float damage, float splashRadius, LayerMask mask, Transform owner)
        {
            flightTime = Mathf.Max(0.2f, flightTime);
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Goo Blob";
            Collider collider = body.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var renderer = body.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            body.transform.position = from;
            body.transform.localScale = Vector3.one * radius * 2f;

            var trail = body.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = 0.18f;
            trail.startWidth = radius * 1.4f;
            trail.endWidth = 0f;
            trail.shadowCastingMode = ShadowCastingMode.Off;

            var blob = body.AddComponent<GooBlob>();
            blob.owner = owner;
            blob.material = material;
            blob.radius = radius;
            blob.damage = damage;
            blob.splashRadius = splashRadius;
            blob.mask = mask;
            blob.previous = from;
            blob.landing = target;
            blob.lifeLeft = flightTime + 2f; // safety: never fly forever
            // Ballistic launch velocity that reaches the target in flightTime.
            blob.velocity = (target - from) / flightTime + 0.5f * Gravity * flightTime * Vector3.up;
            blob.BuildMarker(target);
            return blob;
        }

        private void BuildMarker(Vector3 point)
        {
            var ring = new GameObject("Goo Landing Marker");
            ring.transform.position = point + Vector3.up * 0.03f;
            marker = ring.AddComponent<LineRenderer>();
            marker.sharedMaterial = material;
            marker.useWorldSpace = false;
            marker.loop = true;
            marker.positionCount = RingPoints;
            marker.shadowCastingMode = ShadowCastingMode.Off;
            marker.receiveShadows = false;
            marker.startWidth = marker.endWidth = 0.05f;
            for (int i = 0; i < RingPoints; i++)
            {
                float angle = i * Mathf.PI * 2f / RingPoints;
                marker.SetPosition(i, new Vector3(Mathf.Cos(angle) * splashRadius, 0f, Mathf.Sin(angle) * splashRadius));
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            lifeLeft -= dt;

            // Pulse the landing ring, faster as the blob comes down.
            if (marker != null)
            {
                float closeness = Mathf.Clamp01(1f - (transform.position.y - landing.y) / 6f);
                float pulse = 1f + 0.12f * Mathf.Sin(Time.time * Mathf.Lerp(8f, 26f, closeness));
                marker.transform.localScale = new Vector3(pulse, 1f, pulse);
                marker.startWidth = marker.endWidth = Mathf.Lerp(0.04f, 0.09f, closeness);
            }

            velocity += Vector3.down * Gravity * dt;
            Vector3 next = transform.position + velocity * dt;
            Vector3 path = next - previous;
            float length = path.magnitude;
            if (length > 0f && TryHit(previous, path / length, length, out RaycastHit hit))
            {
                Splash(hit.distance > 0f ? hit.point : transform.position);
                return;
            }
            previous = next;
            transform.position = next;
            transform.localScale = Vector3.one * radius * 2f * (1f + 0.15f * Mathf.Sin(Time.time * 30f)); // wobbly goo
            if (lifeLeft <= 0f) Splash(transform.position);
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

        private void Splash(Vector3 point)
        {
            // Damage everything with Health in range once (a Health can have several colliders).
            var damaged = new System.Collections.Generic.HashSet<Health>();
            int count = Physics.OverlapSphereNonAlloc(point, splashRadius, Overlaps, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (owner != null && Overlaps[i].transform.IsChildOf(owner)) continue;
                Health target = Overlaps[i].GetComponentInParent<Health>();
                if (target == null || target.IsDead || !damaged.Add(target)) continue;
                target.TakeDamage(damage, owner != null ? owner.gameObject : null);
            }

            ImpactFlash.Spawn(material, point, splashRadius * 0.9f, 0.18f);
            GooPuddle.Spawn(material, point, splashRadius);
            if (marker != null) Destroy(marker.gameObject);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (marker != null) Destroy(marker.gameObject);
        }
    }
}
