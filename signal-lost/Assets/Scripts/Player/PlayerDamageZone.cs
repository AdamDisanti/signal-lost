using UnityEngine;

namespace SignalLost.Player
{
    [RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
    public sealed class PlayerDamageZone : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float damage = 20f;
        [SerializeField, Min(0.1f)] private float interval = 1f;
        private float nextDamage;

        private void OnTriggerEnter(Collider other) => Damage(other);
        private void OnTriggerStay(Collider other) => Damage(other);

        private void Damage(Collider other)
        {
            TutorialPlayer player = other.GetComponentInParent<TutorialPlayer>();
            if (player == null || player.IsRespawning || player.Health.IsDead || Time.time < nextDamage) return;
            if (player.Health.TakeDamage(damage, gameObject)) nextDamage = Time.time + interval;
        }
    }
}
