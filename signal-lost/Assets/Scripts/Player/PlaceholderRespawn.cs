using System.Collections;
using SignalLost.Combat;
using UnityEngine;

namespace SignalLost.Player
{
    /// <summary>
    /// PLACEHOLDER so test scenes stay playable: when the player's Health hits zero, controls freeze briefly,
    /// then the player returns to where they started with full health. Real death, checkpoints and respawn
    /// are sequence step 4 (Amanda); replace this when that lands.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(CharacterController))]
    public sealed class PlaceholderRespawn : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float respawnDelay = 1.5f;

        private Health health;
        private CharacterController body;
        private Behaviour[] controls;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;

        public bool IsRespawning { get; private set; }

        private void Awake()
        {
            health = GetComponent<Health>();
            body = GetComponent<CharacterController>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;

            // Everything that lets the player act: movement/look and the mining laser.
            controls = new Behaviour[] { GetComponent<FirstPersonController>(), GetComponent<MiningLaser>() };
        }

        private void OnEnable() => health.Died += OnDied;
        private void OnDisable() => health.Died -= OnDied;

        private void OnDied(Health _)
        {
            if (!IsRespawning) StartCoroutine(Respawn());
        }

        private IEnumerator Respawn()
        {
            IsRespawning = true;
            SetControls(false);
            yield return new WaitForSeconds(respawnDelay);

            // A CharacterController must be off while it is teleported.
            body.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            body.enabled = true;
            health.Restore();
            SetControls(true);
            IsRespawning = false;
        }

        private void SetControls(bool on)
        {
            foreach (Behaviour item in controls)
                if (item != null) item.enabled = on;
        }
    }
}
