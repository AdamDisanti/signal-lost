using SignalLost.Player;
using UnityEngine;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// Slows the player while they stand in an <see cref="OilSlick"/>, without changing FirstPersonController: after
    /// the player has moved each frame, it moves them back by part of that frame's horizontal movement. Added to the
    /// player automatically the first time a slick appears. Teleports (respawning) are ignored.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSlowReceiver : MonoBehaviour
    {
        private CharacterController body;
        private Vector3 last;
        private bool hasLast;

        /// <summary>The slow applied this frame (0 to 1), for UI.</summary>
        public static float CurrentSlow { get; private set; }

        public static void Ensure()
        {
            var player = FindAnyObjectByType<FirstPersonController>();
            if (player != null && player.GetComponent<PlayerSlowReceiver>() == null) player.gameObject.AddComponent<PlayerSlowReceiver>();
        }

        private void Awake() => body = GetComponent<CharacterController>();

        private void OnDisable()
        {
            hasLast = false;
            CurrentSlow = 0f;
        }

        // LateUpdate: after FirstPersonController.Update has moved the player this frame.
        private void LateUpdate()
        {
            if (body == null || !body.enabled)
            {
                hasLast = false;
                CurrentSlow = 0f;
                return;
            }
            Vector3 now = transform.position;
            float slow = OilSlick.SlowAt(now);
            CurrentSlow = slow;
            if (hasLast && slow > 0f)
            {
                Vector3 moved = now - last;
                moved.y = 0f;
                if (moved.sqrMagnitude < 4f) body.Move(-moved * slow);
            }
            last = transform.position;
            hasLast = true;
        }
    }
}
