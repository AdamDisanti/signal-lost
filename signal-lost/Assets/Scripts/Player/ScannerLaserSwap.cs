using SignalLost.Combat;
using SignalLost.Scanner;
using UnityEngine;

namespace SignalLost.Player
{
    /// <summary>
    /// Holsters the mining laser while the scanner is raised, so the scanner's display isn't hidden behind the gun,
    /// and draws the laser again when the scanner is lowered. Turning the MiningLaser component off already hides
    /// the held model and cancels any swing or charge (the tutorial's flask drinking uses the same trick).
    ///
    /// Put it on the player root next to MiningLaser. It finds the laser, the ScannerToggle (on the ScannerRig under
    /// the camera) and PlaceholderRespawn by itself. Doesn't change either system's own code.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScannerLaserSwap : MonoBehaviour
    {
        [Tooltip("Found automatically on this object if left empty.")]
        [SerializeField] private MiningLaser laser;
        [Tooltip("Found automatically in this object's children (the ScannerRig) if left empty.")]
        [SerializeField] private ScannerToggle scanner;
        [Tooltip("Optional. While respawning, the respawn script controls the laser instead.")]
        [SerializeField] private PlaceholderRespawn respawn;

        private bool holstered;

        /// <summary>True while the laser is put away because the scanner is up.</summary>
        public bool IsHolstered => holstered;

        private void Awake()
        {
            if (laser == null) laser = GetComponent<MiningLaser>();
            if (scanner == null) scanner = GetComponentInChildren<ScannerToggle>(true);
            if (respawn == null) respawn = GetComponent<PlaceholderRespawn>();
            if (laser == null || scanner == null)
            {
                Debug.LogWarning("ScannerLaserSwap needs a MiningLaser on this object and a ScannerToggle below it; turning off.", this);
                enabled = false;
                return;
            }
            Debug.Log($"ScannerLaserSwap: {laser.name}'s laser holsters while {scanner.name} is raised.", this);
        }

        // LateUpdate so it runs after the scanner has handled this frame's Q press.
        private void LateUpdate()
        {
            if (scanner.IsRaised)
            {
                // Also catches the respawn script turning the laser back on while the scanner is still up.
                if (laser.enabled) laser.enabled = false;
                holstered = true;
            }
            else if (holstered)
            {
                if (respawn != null && respawn.IsRespawning) return; // respawn turns controls back on itself
                laser.enabled = true;
                holstered = false;
            }
        }

        private void OnDisable()
        {
            // Never leave the player stuck without a weapon if this script is switched off.
            if (holstered && laser != null && (respawn == null || !respawn.IsRespawning)) laser.enabled = true;
            holstered = false;
        }
    }
}
