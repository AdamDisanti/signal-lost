using SignalLost.Player;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.Combat
{
    /// <summary>Opt-in motion for agent enemies; player motion stays in the existing controller.</summary>
    [DisallowMultipleComponent]
    public sealed class KnockbackReceiver : MonoBehaviour
    {
        private NavMeshAgent agent;
        private CharacterController body;
        private Vector3 velocity;
        private float deceleration;
        private bool stopped, updatePosition, updateRotation;
        public bool IsActive { get; private set; }

        public static bool IsMoving(GameObject target) =>
            target.TryGetComponent<KnockbackReceiver>(out var receiver) && receiver.IsActive;

        public static void Apply(Health target, Knockback settings, Vector3 direction)
        {
            Vector3 impulse = settings.Velocity(direction);
            if (impulse.sqrMagnitude == 0f) return;
            if (target.TryGetComponent<FirstPersonController>(out var player))
            {
                if (player.isActiveAndEnabled) player.ApplyKnockback(impulse, settings.deceleration);
                return;
            }
            if (target.TryGetComponent<NavMeshAgent>(out var navigation))
            {
                // shortcut: scripted flight is not an agent body; add a flight adapter before enabling knockback on airborne bosses.
                if (!navigation.enabled || !navigation.isOnNavMesh) return;
                var receiver = target.GetComponent<KnockbackReceiver>() ?? target.gameObject.AddComponent<KnockbackReceiver>();
                receiver.Begin(navigation, impulse, settings.deceleration);
                return;
            }
            if (target.TryGetComponent<Rigidbody>(out var rigid) && !rigid.isKinematic)
                rigid.AddForce(impulse, ForceMode.VelocityChange);
        }

        public static void Reset(Health target)
        {
            if (target.TryGetComponent<FirstPersonController>(out var player)) player.ClearKnockback();
            if (target.TryGetComponent<KnockbackReceiver>(out var receiver)) receiver.Stop();
        }

        private void Begin(NavMeshAgent navigation, Vector3 impulse, float drag)
        {
            agent = navigation;
            if (!IsActive)
            {
                if (body == null)
                {
                    // A controller supplies swept wall/terrain collision while the agent is airborne.
                    body = gameObject.AddComponent<CharacterController>();
                    body.enabled = false;
                    var capsule = GetComponent<CapsuleCollider>();
                    body.radius = capsule != null ? capsule.radius : agent.radius;
                    body.height = capsule != null ? capsule.height : agent.height;
                    body.center = capsule != null ? capsule.center : Vector3.up * (body.height * 0.5f);
                    body.skinWidth = 0.03f;
                    body.minMoveDistance = 0f;
                }
                stopped = agent.isStopped;
                updatePosition = agent.updatePosition;
                updateRotation = agent.updateRotation;
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                agent.updatePosition = agent.updateRotation = false;
                body.enabled = true;
                foreach (Collider own in GetComponentsInChildren<Collider>())
                    if (own != body && own.enabled) Physics.IgnoreCollision(body, own);
                IsActive = true;
            }
            velocity = impulse;
            deceleration = drag;
        }

        private void Update()
        {
            if (!IsActive) return;
            if (!agent.enabled || !agent.isOnNavMesh || !body.enabled) { Stop(); return; }
            float dt = Time.deltaTime;
            velocity.y -= 20f * dt;
            Vector3 step = velocity * dt;
            Vector3 flat = new Vector3(step.x, 0f, step.z);
            Vector3 from = agent.nextPosition;
            // Keep an enemy's ground projection on its connected NavMesh, including during a launch.
            if (NavMesh.Raycast(from, from + flat, out var edge, agent.areaMask))
            {
                Vector3 limited = Vector3.ClampMagnitude(edge.position - from, flat.magnitude);
                step.x = limited.x;
                step.z = limited.z;
                velocity.x = velocity.z = 0f;
            }
            CollisionFlags flags = body.Move(step);
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;
            Vector3 planar = Vector3.MoveTowards(new Vector3(velocity.x, 0f, velocity.z), Vector3.zero, deceleration * dt);
            velocity.x = planar.x; velocity.z = planar.z;
            Vector3 projection = new Vector3(transform.position.x, from.y, transform.position.z);
            if (NavMesh.SamplePosition(projection, out var ground, agent.height + 1f, agent.areaMask))
                agent.nextPosition = ground.position;
            if ((flags & CollisionFlags.Below) != 0 && velocity.y < 0f)
            {
                velocity.y = -2f;
                if (planar.sqrMagnitude < 0.001f) Stop();
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit) =>
            velocity = Knockback.SlideAlongWall(velocity, hit.normal);

        private void OnDisable() => Stop();

        private void Stop()
        {
            if (!IsActive) return;
            IsActive = false;
            velocity = Vector3.zero;
            if (body != null) body.enabled = false;
            if (agent == null) return;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.Warp(agent.nextPosition);
                agent.isStopped = stopped;
            }
            agent.updatePosition = updatePosition;
            agent.updateRotation = updateRotation;
        }
    }
}
