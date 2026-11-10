using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Enemies;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.Finale
{
    /// <summary>
    /// One of Eugene's crew, called in by <see cref="CrewCallIn"/>: Eugene or a Bucket Brother on the player's side.
    /// It is a copy of the crab's model (its <see cref="HermitCrabRig"/> still animates the legs and claws) with every
    /// enemy script stripped off. It scuttles at the nearest enemy (the adds first, then the mech), pinches, and
    /// draws some of the mech's attacks (<see cref="CavendishMech"/> picks crabs as targets). It can't be hurt; the
    /// mech's hits just bowl it over. When its time is up it scuttles off and vanishes in a puff of sand.
    /// </summary>
    public sealed class AllyCrab : MonoBehaviour
    {
        /// <summary>Crabs in the fight right now (the mech picks targets from these).</summary>
        public static readonly List<AllyCrab> Live = new List<AllyCrab>();

        private const float Reach = 2.2f, StrikeTime = 0.35f, WindupTime = 0.4f;

        private HermitCrabRig rig;
        private NavMeshAgent agent;
        private CavendishMech mech;
        private float leaveAt, nextStrike, strikeStart = -1f, knockedUntil;
        private Vector3 knock;
        private float damageToAdds, damageToMech;
        private bool leaving;
        private Health target;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Live.Clear();

        /// <summary>A crab from <paramref name="prefab"/> (any crab with a HermitCrabRig), stripped of its enemy scripts.</summary>
        public static AllyCrab Spawn(GameObject prefab, string name, Vector3 at, Quaternion facing, float seconds, float hitAdds, float hitMech, CavendishMech mech)
        {
            // Copied under an inactive holder, so nothing on the copy wakes up before it is stripped (and the prefab
            // asset itself is never touched).
            var holder = new GameObject("Crew Holder");
            holder.SetActive(false);
            GameObject crab = Instantiate(prefab, at, facing, holder.transform);
            crab.name = name + " (crew)";
            Strip(crab);
            var agent = crab.GetComponent<NavMeshAgent>();
            if (agent == null) agent = crab.AddComponent<NavMeshAgent>();
            agent.enabled = false;
            agent.speed = 7f;
            agent.acceleration = 30f;
            agent.angularSpeed = 600f;
            agent.radius = 0.6f;
            agent.stoppingDistance = 0.5f;
            var ally = crab.AddComponent<AllyCrab>();
            ally.leaveAt = Time.time + seconds;
            ally.damageToAdds = hitAdds;
            ally.damageToMech = hitMech;
            ally.mech = mech;
            crab.transform.SetParent(null, true); // out of the holder: it wakes up now, set up and stripped
            Destroy(holder);
            if (NavMesh.SamplePosition(at, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                agent.enabled = true;
                agent.Warp(hit.position);
            }
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, crab.transform.position, Vector3.up, 30, 0.6f, 5f);
            return ally;
        }

        // Everything but the crab's looks and its rig: no AI, health, drops, dialogue or colliders.
        private static void Strip(GameObject crab)
        {
            for (int pass = 0; pass < 4; pass++) // components that others depend on go on a later pass
                foreach (Component c in crab.GetComponentsInChildren<Component>(true))
                {
                    if (c == null || c is Transform || c is Renderer || c is MeshFilter || c is HermitCrabRig || c is NavMeshAgent) continue;
                    if (c is Behaviour || c is Collider || c is Rigidbody || c is Health) DestroyImmediate(c);
                }
        }

        private void Awake()
        {
            rig = GetComponentInChildren<HermitCrabRig>(true);
            agent = GetComponent<NavMeshAgent>();
        }

        private void OnEnable() => Live.Add(this);
        private void OnDisable() => Live.Remove(this);

        /// <summary>Bowled over by one of the mech's attacks: tumbles away, then gets back to it.</summary>
        public void Knock(Vector3 direction)
        {
            direction.y = 0f;
            knock = direction.sqrMagnitude > 0.001f ? direction.normalized * 9f : -transform.forward * 9f;
            knockedUntil = Time.time + 0.7f;
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, transform.position, Vector3.up, 12, 0.4f, 4f);
        }

        /// <summary>Sends it off early (a phase retry).</summary>
        public void Dismiss() => leaveAt = Time.time;

        private void Update()
        {
            float dt = Time.deltaTime;
            if (!leaving && Time.time >= leaveAt) leaving = true;
            if (rig != null) rig.Windup = rig.Strike = 0f;

            if (Time.time < knockedUntil)
            {
                if (agent.enabled && agent.isOnNavMesh) agent.Move(knock * dt);
                knock = Vector3.Lerp(knock, Vector3.zero, 4f * dt);
                if (rig != null) rig.Hidden = true;
                return;
            }
            if (rig != null) rig.Hidden = false;

            if (leaving)
            {
                if (!agent.enabled || !agent.isOnNavMesh || agent.remainingDistance < 1f || Time.time > leaveAt + 3f)
                {
                    ParticleBurst.Spawn(ParticleBurst.Kind.Sand, transform.position, Vector3.up, 30, 0.6f, 5f);
                    Destroy(gameObject);
                    return;
                }
                if (rig != null) rig.MoveSpeed = agent.velocity.magnitude;
                return;
            }

            target = PickTarget();
            if (target == null || !agent.enabled || !agent.isOnNavMesh)
            {
                if (rig != null) rig.MoveSpeed = 0f;
                return;
            }
            float reach = Reach + (target.GetComponentInParent<CavendishMech>() != null ? 3.2f : 0.4f);
            Vector3 offset = target.transform.position - transform.position;
            offset.y = 0f;
            if (offset.magnitude > reach)
            {
                agent.isStopped = false;
                agent.SetDestination(target.transform.position);
                strikeStart = -1f;
            }
            else
            {
                agent.isStopped = true;
                if (offset.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(offset), 540f * dt);
                Pinch(target);
            }
            if (rig != null) rig.MoveSpeed = agent.velocity.magnitude;
        }

        private void Pinch(Health victim)
        {
            if (strikeStart < 0f)
            {
                if (Time.time < nextStrike) return;
                strikeStart = Time.time;
            }
            float t = Time.time - strikeStart;
            if (rig != null)
            {
                rig.Windup = Mathf.Clamp01(t / WindupTime);
                rig.Strike = Mathf.Clamp01((t - WindupTime) / StrikeTime);
            }
            if (t < WindupTime + StrikeTime) return;
            bool isMech = victim.GetComponentInParent<CavendishMech>() != null;
            victim.TakeDamage(isMech ? damageToMech : damageToAdds, gameObject); // the shield blocks it like anything else
            strikeStart = -1f;
            nextStrike = Time.time + 0.8f;
        }

        // The nearest add within reach of the fight, else the mech.
        private Health PickTarget()
        {
            Health best = null;
            float bestDistance = 22f;
            foreach (FinaleEncounter.Add add in FinaleEncounter.Adds)
            {
                if (add.Health == null || add.Health.IsDead) continue;
                float d = Vector3.Distance(add.Health.transform.position, transform.position);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = add.Health;
                }
            }
            if (best != null) return best;
            return mech != null && mech.Current != CavendishMech.State.Defeated ? mech.Health : null;
        }

        /// <summary>Where crew members head when their time is up.</summary>
        public void Leave(Vector3 exit)
        {
            leaving = true;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(exit);
            }
        }
    }
}
