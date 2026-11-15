using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Enemies;
using SignalLost.Enemies.Desert;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.Mudlands
{
    /// <summary>What happens when the Drunk Driving car hits something.</summary>
    public enum VehicleResponse
    {
        /// <summary>Run over: the creature is launched and hurt, the car keeps going.</summary>
        RunOver,
        /// <summary>Shoved aside without harm to either (too slow, or it braced and the bumper held).</summary>
        Bounce,
        /// <summary>Too big or too solid: the car blows up.</summary>
        Explode,
        /// <summary>The car passes over or through it (playing dead, flat on the ground).</summary>
        Ignore
    }

    /// <summary>One collision between the car and a creature.</summary>
    public struct VehicleHit
    {
        public DrunkCar Car;
        /// <summary>Car speed in m/s at the moment of contact.</summary>
        public float Speed;
        public Vector3 Point;
        /// <summary>The car's direction of travel (flat).</summary>
        public Vector3 Direction;
        /// <summary>Damage a run-over deals (already includes the bumper upgrade).</summary>
        public float Damage;
        /// <summary>True if the car hit the creature's front (it was facing the car).</summary>
        public bool HeadOn;
        /// <summary>True when the car has the Reinforced Bumper.</summary>
        public bool Reinforced;
    }

    /// <summary>
    /// A creature that reacts to the car in its own way. <see cref="OnVehicleHit"/> decides the outcome; the creature
    /// applies its own damage and launch for RunOver (the car does nothing else to it). <see cref="OnCarApproaching"/>
    /// is called a few times a second while a car heads its way, for panics, dodges and bracing.
    /// </summary>
    public interface IVehicleReactive
    {
        VehicleResponse OnVehicleHit(VehicleHit hit);
        void OnCarApproaching(DrunkCar car, float secondsAway);
    }

    /// <summary>What the car explosion does to something that wants to handle it itself (mini-bosses).</summary>
    public struct CarBlastInfo
    {
        public Vector3 Centre;
        public float Radius, Damage, StunSeconds, Distance;
        /// <summary>True when the car crashed straight into this one (not just caught in the blast).</summary>
        public bool Rammed;
        public GameObject Source;
    }

    /// <summary>
    /// Mini-bosses and bosses answer the car explosion here (the kart breaks, the witch falls off her broom...),
    /// since the combo-finisher stun rule doesn't reach them. Return the damage to apply (0 to handle it entirely).
    /// </summary>
    public interface ICarBlastReactive
    {
        float OnCarBlast(CarBlastInfo blast);
    }

    /// <summary>The car explosion: damage that falls off to half at the edge, a shove, and a stun.</summary>
    public static class CarBlast
    {
        private static readonly Collider[] Overlaps = new Collider[96];
        private static readonly System.Collections.Generic.HashSet<Health> Struck = new System.Collections.Generic.HashSet<Health>();

        /// <param name="source">The player: their own Health is never hurt, and the hits count as theirs.</param>
        /// <param name="rammed">The creature the car crashed into, if any (bosses get told it was a ram).</param>
        public static int Explode(Vector3 centre, float radius, float damage, float stunSeconds, GameObject source, Health rammed)
        {
            Struck.Clear();
            int hurt = 0;
            int count = Physics.OverlapSphereNonAlloc(centre, radius, Overlaps, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider collider = Overlaps[i];
                if (collider == null) continue;
                // Gas vents and barrels go up in a chain.
                var vent = collider.GetComponentInParent<BogGasVent>();
                if (vent != null) vent.Ignite(source);
                if (collider.isTrigger) continue;
                Health target = collider.GetComponentInParent<Health>();
                if (target == null || target.IsDead || !Struck.Add(target)) continue;
                if (source != null && target.transform.IsChildOf(source.transform)) continue;
                if (target.GetComponentInParent<DrunkCar>() != null) continue;

                float distance = Vector3.Distance(centre, collider.ClosestPoint(centre));
                float share = Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(distance / radius));
                float amount = damage * share;
                var boss = target.GetComponentInParent<ICarBlastReactive>();
                if (boss != null)
                {
                    amount = boss.OnCarBlast(new CarBlastInfo
                    {
                        Centre = centre, Radius = radius, Damage = amount, StunSeconds = stunSeconds, Distance = distance,
                        Rammed = target == rammed, Source = source
                    });
                }
                if (amount > 0f && target.TakeDamage(amount, source)) hurt++;
                if (target.IsDead) continue;
                if (boss == null) target.GetComponentInParent<IStunnable>()?.Stun(stunSeconds);
                var agent = target.GetComponent<NavMeshAgent>();
                Vector3 away = target.transform.position - centre;
                away.y = 0f;
                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && away.sqrMagnitude > 0.001f)
                    agent.Move(away.normalized * 1.5f * share);
            }

            ParticleBurst.Explosion(centre, radius * 0.8f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Fireball, centre + Vector3.up * 0.8f, Vector3.up, 60, 1.1f, 9f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, centre + Vector3.up * 1.2f, Vector3.up, 40, 1.6f, 4f);
            ExpandingRing.Spawn(EffectMaterials.Additive(EffectMaterials.Look.Glow), centre + Vector3.up * 0.2f, radius, 0.5f, 0.6f);
            CameraShake.Add(0.85f);
            SignalLost.Audio.Sfx.PlayAt("car_explode", centre, 1f, Random.Range(0.92f, 1.05f), 80f);
            return hurt;
        }

        /// <summary>True for creatures the car should treat as too big to run over (mini-bosses, bosses, the Core Worm).</summary>
        public static bool IsLarge(Health target)
        {
            if (target == null) return false;
            if (target.GetComponentInParent<DesertMiniBoss>() != null || target.GetComponentInParent<Coreworm>() != null) return true;
            return target.Max >= 300f;
        }

        /// <summary>True for anything on the enemies' side.</summary>
        public static bool IsEnemy(GameObject item) => EnemyAllegiance.IsEnemy(item);
    }

    /// <summary>Turns a NavMesh agent back on without the "not close enough to the NavMesh" warning.</summary>
    /// <summary>Finds the ground under a point, skipping anything that moves or lives (bodies, cars, creatures,
    /// the player), so props are never set down on a boss's head or the roof of the car.</summary>
    public static class GroundProbe
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        /// <summary>Casts down from <paramref name="above"/> metres over the point; the nearest static surface wins.</summary>
        public static bool Find(Vector3 point, float above, float depth, out Vector3 ground)
        {
            int count = Physics.RaycastNonAlloc(point + Vector3.up * above, Vector3.down, Hits, above + depth, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            ground = point;
            for (int i = 0; i < count; i++)
            {
                Collider collider = Hits[i].collider;
                if (collider.attachedRigidbody != null || collider is CharacterController || collider.GetComponentInParent<Health>() != null) continue;
                if (Hits[i].distance >= best) continue;
                best = Hits[i].distance;
                ground = Hits[i].point;
            }
            return best < float.MaxValue;
        }

        /// <summary>The ground just under (or a little above) the point: a short probe first, so tree canopies and
        /// roofs overhead are ignored, then a tall one for points that are underground or far off the terrain.</summary>
        public static Vector3 Under(Vector3 point)
        {
            if (Find(point, 3f, 12f, out Vector3 ground)) return ground;
            if (Find(point, 60f, 120f, out ground)) return ground;
            return point;
        }
    }

    public static class AgentUtil
    {
        /// <summary>Snaps the body onto the nearest NavMesh point within <paramref name="radius"/> first, then
        /// enables the agent. Returns false (agent left off) when there is no NavMesh that close.</summary>
        public static bool Reenable(UnityEngine.AI.NavMeshAgent agent, float radius = 6f)
        {
            if (agent == null) return false;
            if (agent.enabled) return true;
            if (!UnityEngine.AI.NavMesh.SamplePosition(agent.transform.position, out UnityEngine.AI.NavMeshHit hit, radius, UnityEngine.AI.NavMesh.AllAreas)) return false;
            agent.transform.position = hit.position;
            agent.enabled = true;
            return true;
        }
    }
}
