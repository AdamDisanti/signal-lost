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
        /// <summary>Raised when damage is refused because this is <see cref="Invulnerable"/>, e.g. a friendly NPC being hit.</summary>
        public event Action<Health, float, GameObject> Blocked;

        // Runtime protection for friendly NPCs; normal actors remain damageable by default.
        public bool Invulnerable { get; set; }

        /// <summary>
        /// Optional: rescales incoming damage by who dealt it (the final boss's robot mini-bosses hit the player at 70%).
        /// Gets the amount and the source; returns the amount to apply. Null: damage is unchanged.
        /// </summary>
        public Func<float, GameObject, float> DamageModifier { get; set; }

        /// <summary>
        /// Optional, for everything: rescales damage by who dealt it before <see cref="DamageModifier"/> (the player's
        /// rage stacks make all of their hits harder). Gets the amount, the source and the target; returns the amount.
        /// </summary>
        public static Func<float, GameObject, Health, float> OutgoingModifier { get; set; }
        /// <summary>Raised after damage is applied to anything (test arenas show damage numbers with it).</summary>
        public static event Action<Health, float, GameObject> AnyDamaged;
        /// <summary>Raised when anything dies, with what dealt the killing blow (may be null): rage counts the player's kills.</summary>
        public static event Action<Health, GameObject> AnyKilled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OutgoingModifier = null;
            AnyDamaged = null;
            AnyKilled = null;
        }
        public float Max => maxHealth;

        /// <summary>Changes the maximum (upgrades), keeping the same fraction of health.</summary>
        public void SetMax(float max)
        {
            max = Mathf.Max(1f, max);
            float fraction = maxHealth > 0f ? current / maxHealth : 1f;
            maxHealth = max;
            current = IsDead ? 0f : Mathf.Clamp(fraction * max, 1f, max);
            Changed?.Invoke(this);
        }
        public float Current => current;
        public float Normalized => current / maxHealth;
        public bool IsDead => current <= 0f;

        private void Awake() => current = maxHealth;

        /// <summary>Applies damage. Returns false if the amount is not positive or this is already dead.</summary>
        public bool TakeDamage(float amount, GameObject source = null)
        {
            if (amount <= 0f || IsDead) return false;
            // Testing cheat: the player's hits are lethal (never their hits on themselves).
            if (SignalLost.Core.CheatMode.Enabled && source != null && source.GetComponentInParent<SignalLost.Player.PlayerLife>() != null &&
                GetComponentInParent<SignalLost.Player.PlayerLife>() == null)
                amount = SignalLost.Core.CheatMode.Damage;
            if (OutgoingModifier != null)
            {
                amount = OutgoingModifier(amount, source, this);
                if (amount <= 0f) return false;
            }
            if (DamageModifier != null)
            {
                amount = DamageModifier(amount, source);
                if (amount <= 0f) return false;
            }
            if (Invulnerable)
            {
                Blocked?.Invoke(this, amount, source);
                return false;
            }

            float applied = Mathf.Min(amount, current);
            current -= applied;
            Damaged?.Invoke(this, applied, source);
            AnyDamaged?.Invoke(this, applied, source);
            Changed?.Invoke(this);

            if (current <= 0f)
            {
                current = 0f;
                Died?.Invoke(this);
                died.Invoke();
                AnyKilled?.Invoke(this, source);
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
