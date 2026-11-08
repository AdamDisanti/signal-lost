using UnityEngine;

namespace SignalLost.Player
{
    /// <summary>
    /// Brings the player back to the spawn point if they fall below <see cref="killHeight"/> (off a sea cliff
    /// or a dock), using the normal death fade and respawn. Put one in any level with water or drops the player
    /// can't climb back out of; keep the kill height below every walkable surface.
    /// </summary>
    public sealed class FallRespawn : MonoBehaviour
    {
        [SerializeField] private float killHeight = -3f;

        private PlayerLife player;

        private void Update()
        {
            if (player == null) player = FindAnyObjectByType<PlayerLife>();
            if (player != null && !player.IsRespawning && player.transform.position.y < killHeight)
                player.Blackout(PlayerLife.DeathMessage);
        }
    }
}
