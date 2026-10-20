using System;
using UnityEngine;
using UnityEngine.Events;

namespace SignalLost.Combat
{
    /// <summary>
    /// Shared hit points for anything that can be damaged: the player, enemies, bosses, and test dummies.
    /// Other systems listen to its events instead of checking health every frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [Tooltip("Optional Inspector hook that runs once when health reaches zero.")]
        [SerializeField] private UnityEvent died = new UnityEvent();

        private float current;
        private bool deathRaised;

        /// <summary>Raised after any change to current health (damage, healing, or restore).</summary>
        public event Action<Health> Changed;
        /// <summary>Raised after damage is applied. Arguments: this, amount applied, source object (may be null).</summary>
        public event Action<Health, float, GameObject> Damaged;
        /// <summary>Raised once when health reaches zero.</summary>
        public event Action<Health> Died;

        // Runtime protection for friendly NPCs; normal actors remain damageable by default.
        public bool Invulnerable { get; set; }
        /// <summary>The current hit context, available to damage reactions and weak-spot relays.</summary>
        public Knockback HitKnockback { get; private set; }
        public Vector3 HitDirection { get; private set; }
        public float Max => maxHealth;
        public float Current => current;
        public float Normalized => current / maxHealth;
        public bool IsDead => current <= 0f;

        private void Awake() => current = maxHealth;

        /// <summary>Damage with optional attack-directed motion. Rejected and lethal hits do not push a target.</summary>
        public bool TakeDamage(float amount, GameObject source = null, Knockback knockback = default, Vector3 hitDirection = default)
        {
            if (Invulnerable || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount) || IsDead) return false;

            Knockback previousKnockback = HitKnockback;
            Vector3 previousDirection = HitDirection;
            HitKnockback = knockback;
            HitDirection = hitDirection;
            try
            {
                float applied = Mathf.Min(amount, current);
                current -= applied;
                Damaged?.Invoke(this, applied, source);
                Changed?.Invoke(this);

                if (current <= 0f && !deathRaised)
                {
                    current = 0f;
                    deathRaised = true;
                    KnockbackReceiver.Reset(this);
                    Died?.Invoke(this);
                    died.Invoke();
                }
                else if (!IsDead && !Invulnerable)
                    KnockbackReceiver.Apply(this, knockback, hitDirection);
                return true;
            }
            finally
            {
                HitKnockback = previousKnockback;
                HitDirection = previousDirection;
            }
        }

        /// <summary>
        /// Restores health up to the maximum. Does not revive the dead; use <see cref="Restore"/> for respawns.
        /// Overhealing for the flask is intentionally not implemented until its rules are decided.
        /// </summary>
        public bool Heal(float amount)
        {
            if (amount <= 0f || IsDead || current >= maxHealth) return false;
            current = Mathf.Min(maxHealth, current + amount);
            Changed?.Invoke(this);
            return true;
        }

        /// <summary>Resets to full health, including after death (respawn, checkpoint, dummy reset).</summary>
        public void Restore()
        {
            KnockbackReceiver.Reset(this);
            deathRaised = false;
            current = maxHealth;
            Changed?.Invoke(this);
        }
    }
}
