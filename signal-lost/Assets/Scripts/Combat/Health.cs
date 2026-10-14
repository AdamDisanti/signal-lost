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

        /// <summary>Raised after any change to current health (damage, healing, or restore).</summary>
        public event Action<Health> Changed;
        /// <summary>Raised after damage is applied. Arguments: this, amount applied, source object (may be null).</summary>
        public event Action<Health, float, GameObject> Damaged;
        /// <summary>Raised once when health reaches zero.</summary>
        public event Action<Health> Died;

        public float Max => maxHealth;
        public float Current => current;
        public float Normalized => current / maxHealth;
        public bool IsDead => current <= 0f;

        private void Awake() => current = maxHealth;

        /// <summary>Applies damage. Returns false if the amount is not positive or this is already dead.</summary>
        public bool TakeDamage(float amount, GameObject source = null)
        {
            if (amount <= 0f || IsDead) return false;

            float applied = Mathf.Min(amount, current);
            current -= applied;
            Damaged?.Invoke(this, applied, source);
            Changed?.Invoke(this);

            if (current <= 0f)
            {
                current = 0f;
                Died?.Invoke(this);
                died.Invoke();
            }
            return true;
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
            current = maxHealth;
            Changed?.Invoke(this);
        }
    }
}
