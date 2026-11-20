using System.Collections;
using System.Collections.Generic;
using SignalLost.Audio;
using SignalLost.Combat;
using UnityEngine;

namespace SignalLost.Melee
{
    /// <summary>
    /// The knuckles' chain lightning (Adam, 11-10): from where it starts, a bolt jumps to the nearest enemy not hit yet
    /// within reach, hurts it, jolts small ones still for a moment, and jumps on from there, a little weaker each time.
    /// A kill along the way doesn't stop it. Rage adds reach and jumps (<see cref="RageMeter"/>).
    /// </summary>
    public static class ChainArcs
    {
        /// <summary>Seconds between jumps, so the chain can be seen travelling.</summary>
        private const float JumpDelay = 0.06f;
        private const float Falloff = 0.85f;
        private const float JoltSeconds = 0.5f;

        /// <param name="from">Where the first bolt starts (the struck enemy).</param>
        /// <param name="skip">Not hit by the chain (the enemy it starts from).</param>
        /// <param name="source">Who it belongs to (the player): their hits, their kills.</param>
        public static void Fire(Vector3 from, Health skip, int jumps, float reach, float damage, GameObject source) =>
            GaussFx.Run(Chain(from, skip, jumps, reach, damage, source));

        private static IEnumerator Chain(Vector3 from, Health skip, int jumps, float reach, float damage, GameObject source)
        {
            var struck = new HashSet<Health>();
            if (skip != null) struck.Add(skip);
            var nearby = new List<Health>();
            Vector3 at = from;
            Sfx.PlayAt("hex_zap", at, 0.7f, Random.Range(1.15f, 1.35f), 30f);
            for (int jump = 0; jump < jumps; jump++)
            {
                Health next = null;
                foreach (Health candidate in MeleeTargets.Around(at, reach, source != null ? source.transform : null, nearby))
                    if (!struck.Contains(candidate)) { next = candidate; break; }
                if (next == null) yield break;
                struck.Add(next);
                Vector3 to = MeleeTargets.Centre(next);
                GaussFx.Bolt(at, to, 0.08f, 0.25f, jump == 0);
                GaussFx.Pop(to, Vector3.zero, 0.6f, 10);
                next.TakeDamage(damage, source);
                if (!next.IsDead && MeleeTargets.IsSmall(next)) next.GetComponentInParent<IStunnable>()?.Stun(JoltSeconds);
                damage *= Falloff;
                at = to;
                yield return new WaitForSeconds(JumpDelay);
            }
        }
    }
}
