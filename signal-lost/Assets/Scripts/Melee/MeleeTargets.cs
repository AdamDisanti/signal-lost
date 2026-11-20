using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Enemies;
using SignalLost.Enemies.Desert;
using SignalLost.Finale;
using SignalLost.Player;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.Melee
{
    /// <summary>
    /// Who the fists can do what to (docs/melee-rage.md). Enemies are anything that hunts the player: it walks on the
    /// NavMesh, keeps to a territory or can be stunned (never the player, the merchant or Eugene's crew). Small enemies
    /// (critters up to <see cref="SmallMaxHealth"/> HP, never a mini-boss, boss or the mech) can be yanked in by the
    /// magnet pull and finished off; big ones pull the player to them instead and are never finished.
    /// </summary>
    public static class MeleeTargets
    {
        /// <summary>Critters with more health than this count as big (the hermit crab has 120, a Hog Brute more).</summary>
        public const float SmallMaxHealth = 200f;

        private static readonly Collider[] Overlaps = new Collider[96];
        private static readonly RaycastHit[] Hits = new RaycastHit[24];
        private static readonly HashSet<Health> Seen = new HashSet<Health>();
        private static readonly List<Health> Chain = new List<Health>();

        /// <summary>
        /// The enemy a collider belongs to: its outermost Health, so parts with health of their own (the mortar frog's
        /// tin can, the mech's reactor) count as the one enemy and chain lightning doesn't waste jumps on them.
        /// </summary>
        public static Health Owner(Collider collider)
        {
            collider.GetComponentsInParent(false, Chain);
            return Chain.Count > 0 ? Chain[Chain.Count - 1] : null;
        }

        public static bool IsEnemy(Health health)
        {
            if (health == null || health.GetComponentInParent<PlayerLife>() != null) return false;
            if (health.GetComponentInParent<SignalLost.NPC.MerchantCrab>() != null || health.GetComponentInParent<AllyCrab>() != null) return false;
            return health.GetComponentInParent<NavMeshAgent>() != null || health.GetComponentInParent<ITerritorial>() != null ||
                   health.GetComponentInParent<IStunnable>() != null || IsBoss(health);
        }

        /// <summary>A mini-boss, island boss or the mech.</summary>
        public static bool IsBoss(Health health) =>
            health != null && (health.GetComponentInParent<DesertMiniBoss>() != null || health.GetComponentInParent<Coreworm>() != null ||
                               health.GetComponentInParent<CavendishMech>() != null);

        public static bool IsSmall(Health health) => IsEnemy(health) && !IsBoss(health) && health.Max <= SmallMaxHealth;

        /// <summary>Somewhere to aim at on the enemy: the middle of its colliders (or a little above its feet).</summary>
        public static Vector3 Centre(Health health)
        {
            var collider = health.GetComponentInChildren<Collider>();
            if (collider != null && collider.enabled) return collider.bounds.center;
            return health.transform.position + Vector3.up * 0.6f;
        }

        /// <summary>
        /// The living enemy nearest the crosshair within <paramref name="range"/> metres and <paramref name="cone"/>
        /// degrees of it that can be seen from <paramref name="eye"/>, or null. Prefers the one closest to the line.
        /// </summary>
        public static Health InSights(Vector3 eye, Vector3 forward, float range, float cone, Transform ignore, System.Func<Health, bool> filter = null)
        {
            Health best = null;
            float bestScore = float.MaxValue;
            Seen.Clear();
            int count = Physics.OverlapSphereNonAlloc(eye, range, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Health target = Owner(Overlaps[i]);
                if (target == null || target.IsDead || !Seen.Add(target) || !IsEnemy(target)) continue;
                if (ignore != null && target.transform.IsChildOf(ignore)) continue;
                if (filter != null && !filter(target)) continue;
                Vector3 point = Overlaps[i].bounds.ClosestPoint(eye + forward * Mathf.Min(range, Vector3.Distance(eye, Overlaps[i].bounds.center)));
                Vector3 to = point - eye;
                float distance = to.magnitude;
                if (distance > range) continue;
                float angle = distance > 0.3f ? Vector3.Angle(forward, to) : 0f;
                if (angle > cone) continue;
                if (distance > 0.3f && Blocked(eye, to / distance, distance, target, ignore)) continue;
                float score = angle * 2f + distance; // near the crosshair first, then near
                if (score >= bestScore) continue;
                bestScore = score;
                best = target;
            }
            return best;
        }

        /// <summary>Living enemies within <paramref name="radius"/> of <paramref name="centre"/>, nearest first.</summary>
        public static List<Health> Around(Vector3 centre, float radius, Transform ignore, List<Health> into = null)
        {
            into ??= new List<Health>();
            into.Clear();
            Seen.Clear();
            int count = Physics.OverlapSphereNonAlloc(centre, radius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Health target = Owner(Overlaps[i]);
                if (target == null || target.IsDead || !Seen.Add(target) || !IsEnemy(target)) continue;
                if (ignore != null && target.transform.IsChildOf(ignore)) continue;
                into.Add(target);
            }
            into.Sort((a, b) => (a.transform.position - centre).sqrMagnitude.CompareTo((b.transform.position - centre).sqrMagnitude));
            return into;
        }

        /// <summary>
        /// Shoves an enemy along the ground: through its NavMeshAgent when it has one (so it stays on the mesh and its
        /// own AI isn't upset), otherwise by moving it.
        /// </summary>
        public static void Shove(Health target, Vector3 offset)
        {
            if (target == null) return;
            offset.y = 0f;
            var agent = target.GetComponentInParent<NavMeshAgent>();
            if (agent != null)
            {
                if (agent.isActiveAndEnabled && agent.isOnNavMesh) agent.Move(offset);
                return;
            }
            if (target.GetComponentInParent<Rigidbody>() is Rigidbody body && !body.isKinematic) body.MovePosition(body.position + offset);
            else if (!IsBoss(target)) target.transform.position += offset;
        }

        /// <summary>
        /// Drags an enemy to <paramref name="point"/> (the magnet pull): warped along the NavMesh when it walks on one,
        /// so its own movement can't fight the pull, otherwise moved. Returns false if there is no NavMesh there.
        /// </summary>
        public static bool Yank(Health target, Vector3 point)
        {
            var agent = target.GetComponentInParent<NavMeshAgent>();
            if (agent == null)
            {
                if (!IsBoss(target)) target.transform.position = point;
                return true;
            }
            if (!agent.isActiveAndEnabled || !NavMesh.SamplePosition(point, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) return false;
            return agent.Warp(hit.position);
        }

        private static bool Blocked(Vector3 origin, Vector3 direction, float distance, Health target, Transform ignore)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider other = Hits[i].collider;
                if (ignore != null && other.transform.IsChildOf(ignore)) continue;
                Health owner = other.GetComponentInParent<Health>();
                if (owner == target) continue;
                if (owner != null && IsEnemy(owner)) continue; // another enemy in the way doesn't hide this one
                if (Hits[i].distance < distance - 0.3f) return true;
            }
            return false;
        }
    }
}
