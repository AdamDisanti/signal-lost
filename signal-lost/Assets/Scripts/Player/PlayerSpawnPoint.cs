using UnityEngine;

namespace SignalLost.Player
{
    /// <summary>
    /// Where the player comes back after dying or blacking out. Place one per scene (or move <see cref="Current"/>
    /// to a new one at a checkpoint). The player faces this object's forward direction. Without one, the player
    /// respawns where the scene started them.
    /// </summary>
    public sealed class PlayerSpawnPoint : MonoBehaviour
    {
        public static PlayerSpawnPoint Current { get; private set; }

        [Tooltip("Becomes the active respawn point when the scene starts.")]
        [SerializeField] private bool activeAtStart = true;

        private void OnEnable()
        {
            if (activeAtStart && Current == null) Current = this;
        }

        private void OnDisable()
        {
            if (Current == this) Current = null;
        }

        /// <summary>Makes this the place the player respawns (a checkpoint).</summary>
        public void Activate() => Current = this;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 0.85f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.45f);
            Gizmos.DrawLine(transform.position + Vector3.up * 0.9f, transform.position + Vector3.up * 0.9f + transform.forward * 1.2f);
        }
    }
}
