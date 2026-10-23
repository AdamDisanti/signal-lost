using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Effects;
using UnityEngine;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// Sludge's charged fuel shell: a blazing glob of pressurised fuel fired in a straight, fast line. It bursts on the
    /// first thing it hits (or at the end of its range) in a big blast that only reaches what it can see: anything
    /// with solid cover between it and the blast is safe. That is the counterplay — get behind a rock or a hull plate.
    /// It leaves a fire where it lands and sets off oil slicks and beetles it can see.
    /// </summary>
    public sealed class FuelShell : MonoBehaviour
    {
        private static readonly Collider[] Overlaps = new Collider[64];
        private static readonly HashSet<Health> Damaged = new HashSet<Health>();
        private static readonly List<IIgnitable> ToIgnite = new List<IIgnitable>();

        private const float ShellRadius = 0.3f;

        private Vector3 direction;
        private float speed, range, travelled, blastRadius, damage, edgeDamage, fireRadius, fireDuration;
        private GameObject owner;
        private Transform ownerRoot;
        private FireStyle fire;
        private Material flashMaterial;
        private LayerMask mask;
        private FireEffect trail;
        private bool exploded;

        public static FuelShell Launch(Vector3 from, Vector3 direction, float speed, float range, float blastRadius, float damage,
            float edgeDamage, FireStyle fire, float fireRadius, float fireDuration, GameObject owner, Material shellMaterial,
            Material flashMaterial, LayerMask mask)
        {
            GameObject body = MiningLaser.VisualPrimitive(PrimitiveType.Sphere, "Fuel Shell", shellMaterial);
            body.transform.position = from;
            body.transform.localScale = Vector3.one * ShellRadius * 2f;
            var shell = body.AddComponent<FuelShell>();
            shell.direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            shell.speed = Mathf.Max(1f, speed);
            shell.range = Mathf.Max(1f, range);
            shell.blastRadius = blastRadius;
            shell.damage = damage;
            shell.edgeDamage = edgeDamage;
            shell.fire = fire;
            shell.fireRadius = fireRadius;
            shell.fireDuration = fireDuration;
            shell.owner = owner;
            shell.ownerRoot = owner != null ? owner.transform : null;
            shell.flashMaterial = flashMaterial;
            shell.mask = mask;
            body.transform.rotation = Quaternion.LookRotation(shell.direction);
            // A tail of flame streaming behind it.
            shell.trail = FireEffect.SpawnJet(body.transform, Vector3.back * ShellRadius, Quaternion.Euler(0f, 180f, 0f), 0.2f, 1.6f);
            return shell;
        }

        private void Update()
        {
            if (exploded) return;
            float step = speed * Time.deltaTime;
            if (FirstHit(transform.position, direction, step + ShellRadius, out RaycastHit hit))
            {
                Explode(hit.point - direction * 0.25f);
                return;
            }
            transform.position += direction * step;
            travelled += step;
            if (travelled >= range) Explode(transform.position);
        }

        private bool FirstHit(Vector3 from, Vector3 dir, float distance, out RaycastHit best)
        {
            best = default;
            float nearest = float.MaxValue;
            foreach (RaycastHit hit in Physics.SphereCastAll(from, ShellRadius * 0.6f, dir, distance, mask, QueryTriggerInteraction.Ignore))
            {
                if (ownerRoot != null && hit.collider.transform.IsChildOf(ownerRoot)) continue;
                if (hit.collider.GetComponentInParent<OilSlick>() != null) continue; // flies over puddles
                if (hit.distance >= nearest) continue;
                nearest = hit.distance;
                best = hit;
            }
            return nearest < float.MaxValue;
        }

        private void Explode(Vector3 point)
        {
            exploded = true;
            Blast(point, blastRadius, damage, edgeDamage, owner, ownerRoot, mask);
            ParticleBurst.Explosion(point, blastRadius * 0.55f);
            if (flashMaterial != null) ExpandingRing.Spawn(flashMaterial, point + Vector3.up * 0.05f, blastRadius, 0.35f, 0.12f);
            if (fire != null && fireRadius > 0f && Physics.Raycast(point + Vector3.up * 0.5f, Vector3.down, out RaycastHit ground, 8f, mask, QueryTriggerInteraction.Ignore))
                FireZone.Spawn(fire, ground.point, fireRadius, owner, ownerRoot, fireDuration);
            if (trail != null)
            {
                trail.transform.SetParent(null, true);
                trail.Extinguish();
                Destroy(trail.gameObject, 3f);
            }
            Destroy(gameObject);
        }

        /// <summary>
        /// A blast that hurts every Health in range that it can see, falling off toward the edge. Solid scenery between
        /// the blast and a target's middle shields it completely; creatures and oil puddles don't count as cover.
        /// Ignitable things in sight are set off.
        /// </summary>
        public static void Blast(Vector3 point, float radius, float damage, float edgeDamage, GameObject source, Transform ignore, LayerMask mask)
        {
            Damaged.Clear();
            ToIgnite.Clear();
            int count = Physics.OverlapSphereNonAlloc(point, radius, Overlaps, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = Overlaps[i];
                if (ignore != null && hit.transform.IsChildOf(ignore)) continue;
                var ignitable = hit.GetComponentInParent<IIgnitable>();
                Health health = hit.GetComponentInParent<Health>();
                bool hurtable = health != null && !health.IsDead && !Damaged.Contains(health) && EnemyAllegiance.CanHurt(source, health);
                if (ignitable == null && !hurtable) continue;
                if (!InSight(point, hit.bounds.center, hit, ignore, mask)) continue;

                if (ignitable != null && !ToIgnite.Contains(ignitable)) ToIgnite.Add(ignitable);
                if (!hurtable) continue;
                Damaged.Add(health);
                float distance = Vector3.Distance(point, hit.ClosestPoint(point));
                float falloff = Mathf.Lerp(1f, Mathf.Clamp01(edgeDamage), Mathf.Clamp01(distance / radius));
                health.TakeDamage(damage * falloff, source);
            }
            foreach (IIgnitable item in ToIgnite)
                if (item is Object unityObject && unityObject != null) item.Ignite(source);
        }

        /// <summary>True if nothing solid (other than the target itself, creatures, puddles or the attacker) is in the way.</summary>
        private static bool InSight(Vector3 from, Vector3 to, Collider target, Transform ignore, LayerMask mask)
        {
            Vector3 offset = to - from;
            float distance = offset.magnitude;
            if (distance < 0.05f) return true;
            Health owner = target.GetComponentInParent<Health>();
            Transform targetRoot = owner != null ? owner.transform : target.transform;
            foreach (RaycastHit hit in Physics.RaycastAll(from, offset / distance, distance, mask, QueryTriggerInteraction.Ignore))
            {
                Collider blocker = hit.collider;
                if (blocker == target || blocker.transform.IsChildOf(targetRoot)) continue;
                if (ignore != null && blocker.transform.IsChildOf(ignore)) continue;
                if (blocker is CharacterController || blocker.GetComponentInParent<Health>() != null) continue;
                if (blocker.GetComponentInParent<OilSlick>() != null) continue;
                return false;
            }
            return true;
        }
    }
}
