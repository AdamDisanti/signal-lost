using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Core;
using UnityEngine;

namespace SignalLost.Player
{
    /// <summary>
    /// The dodge (docs/desert-bosses.md, added with the boss revamp): tap Dodge (Left Alt by default) to dash about 4.5 m
    /// the way you're moving, or backwards if you're standing still. The first 0.22 s are invulnerable, so a well-timed
    /// dodge goes straight through an attack. 0.6 s cooldown; one dodge per jump in the air.
    ///
    /// Added to the player by <see cref="FirstPersonController"/> at start-up. It moves the CharacterController on top of
    /// the normal walking, and makes the player's Health refuse damage during the i-frames.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerDodge : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float distance = 4.5f;
        [SerializeField, Min(0.05f)] private float duration = 0.3f;
        [Tooltip("Invulnerable for this long from the start of the dodge.")]
        [SerializeField, Min(0f)] private float invulnerable = 0.22f;
        [SerializeField, Min(0f)] private float cooldown = 0.6f;

        private CharacterController body;
        private FirstPersonController controller;
        private Health health;
        private Transform view;
        private Vector3 direction;
        private float started = float.NegativeInfinity, readyAt, covered = 1f;
        private bool usedInAir, madeInvulnerable;

        /// <summary>True while dashing.</summary>
        public bool Dodging => Time.time - started < duration;
        /// <summary>True during the i-frames.</summary>
        public bool Invulnerable => Time.time - started < invulnerable;
        /// <summary>Raised when a dodge starts (for sounds, tests, tutorials).</summary>
        public event System.Action Dodged;

        private void Awake()
        {
            body = GetComponent<CharacterController>();
            controller = GetComponent<FirstPersonController>();
            health = GetComponent<Health>();
            Camera camera = GetComponentInChildren<Camera>();
            view = camera != null ? camera.transform : transform;
        }

        private void Update()
        {
            if (body.isGrounded) usedInAir = false;
            if (KeyBindings.WasPressed("dodge")) TryDodge();
            if (covered < 1f && started > float.NegativeInfinity)
            {
                // Front-loaded: fast out of the blocks, easing at the end. Moves by the exact distance covered so far,
                // so a long frame never overshoots.
                float t = Mathf.Clamp01((Time.time - started) / duration);
                float now = 1f - (1f - t) * (1f - t);
                body.Move(direction * (distance * (now - covered)));
                covered = now;
            }
            if (madeInvulnerable && !Invulnerable)
            {
                madeInvulnerable = false;
                if (health != null) health.Invulnerable = false;
            }
        }

        /// <summary>Starts a dodge if allowed now. The direction comes from the move input (backwards when there is none).</summary>
        public bool TryDodge(Vector2? input = null)
        {
            if (!enabled || Time.time < readyAt || Dodging) return false;
            if (controller != null && (!controller.enabled || controller.IsStunned)) return false;
            if (health != null && health.IsDead) return false;
            if (!body.isGrounded)
            {
                if (usedInAir) return false;
                usedInAir = true;
            }
            Vector2 move = input ?? (controller != null ? controller.MoveInput : Vector2.zero);
            Vector3 forward = view.forward, right = view.right;
            forward.y = right.y = 0f;
            forward.Normalize();
            right.Normalize();
            direction = move.sqrMagnitude > 0.04f ? (forward * move.y + right * move.x).normalized : -forward;
            started = Time.time;
            covered = 0f;
            readyAt = Time.time + cooldown;
            if (health != null && !health.Invulnerable)
            {
                health.Invulnerable = true;
                madeInvulnerable = true;
            }
            Sfx.Play("chain_swish", 0.35f, 1.6f);
            Dodged?.Invoke();
            return true;
        }

        private void OnDisable()
        {
            if (madeInvulnerable && health != null) health.Invulnerable = false;
            madeInvulnerable = false;
        }
    }
}
