using System.Collections.Generic;
using SignalLost.Effects;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.Combat
{
    /// <summary>
    /// A burst of damage around a point: full damage at the centre, falling off to a fraction at the edge, with a
    /// small shove and a shockwave ring. The mining laser's full-charge bolt uses it to cut through crowds.
    /// </summary>
    public static class AreaBlast
    {
        private static readonly Collider[] Overlaps = new Collider[64];
        private static readonly HashSet<Health> Struck = new HashSet<Health>();

        /// <param name="edgeMultiplier">Share of the damage dealt at the very edge (0.5 = half).</param>
        /// <param name="source">Who caused it; their own Health is never hurt.</param>
        /// <param name="skip">Already hit directly (the bolt's target), so it is not hit twice.</param>
        /// <returns>How many things were hurt.</returns>
        public static int Explode(Vector3 centre, float radius, float damage, float edgeMultiplier, float knockback,
            GameObject source, Health skip, Material material)
        {
            Struck.Clear();
            int hurt = 0;
            int count = Physics.OverlapSphereNonAlloc(centre, radius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Health target = Overlaps[i].GetComponentInParent<Health>();
                if (target == null || target == skip || target.IsDead || !Struck.Add(target)) continue;
                if (source != null && target.transform.IsChildOf(source.transform)) continue;

                float distance = Vector3.Distance(centre, Overlaps[i].ClosestPoint(centre));
                float share = Mathf.Lerp(1f, edgeMultiplier, Mathf.Clamp01(distance / radius));
                if (!target.TakeDamage(damage * share, source)) continue;
                hurt++;

                // A small shove away from the centre for anything that walks on the NavMesh.
                var agent = target.GetComponent<NavMeshAgent>();
                Vector3 away = target.transform.position - centre;
                away.y = 0f;
                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && away.sqrMagnitude > 0.001f)
                    agent.Move(away.normalized * knockback * share);
            }

            if (material != null)
            {
                ExpandingRing.Spawn(material, centre + Vector3.up * 0.15f, radius, 0.45f, 0.35f);
                ImpactFlash.Spawn(material, centre, radius * 0.9f, 0.18f);
            }
            return hurt;
        }
    }
}
