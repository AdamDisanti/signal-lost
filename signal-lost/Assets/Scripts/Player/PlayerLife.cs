using System;
using System.Collections;
using SignalLost.Combat;
using SignalLost.UI;
using UnityEngine;

namespace SignalLost.Player
{
    /// <summary>
    /// Death and blackout for the player: the screen fades to black with a message, the player is moved to the
    /// <see cref="PlayerSpawnPoint"/> at full health, and a short <see cref="WakeUpSequence"/> plays if there is one.
    /// Systems that care listen to <see cref="RespawnStarted"/> and <see cref="Respawned"/> (the flask sobers up,
    /// <see cref="StageReset"/> restores enemies, quests close their popups) rather than being called from here.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(FirstPersonController))]
    public sealed class PlayerLife : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float fadeSeconds = 1.5f;

        private Health health;
        private FirstPersonController movement;
        private CharacterController body;
        private PlayerGear gear;
        private WakeUpSequence wakeUp;
        private Vector3 startPosition;
        private Quaternion startRotation;

        public Health Health => health;
        public bool IsRespawning { get; private set; }
        public bool IsDead => health.IsDead;

        /// <summary>Raised when the fade to black starts. Argument: the message shown ("YOU DIED", "BLACKOUT ...").</summary>
        public event Action<string> RespawnStarted;
        /// <summary>Raised once the player has been moved back and restored.</summary>
        public event Action Respawned;

        private void Awake()
        {
            health = GetComponent<Health>();
            movement = GetComponent<FirstPersonController>();
            body = GetComponent<CharacterController>();
            gear = GetComponent<PlayerGear>();
            wakeUp = GetComponent<WakeUpSequence>();
            startPosition = transform.position;
            startRotation = transform.rotation;
        }

        private void OnEnable() => health.Died += OnDied;
        private void OnDisable() => health.Died -= OnDied;

        private void OnDied(Health _) => Respawn("YOU DIED");

        /// <summary>Knocks the player out (too much Scotch) and brings them back at the spawn point.</summary>
        public void Blackout(string message = "BLACKOUT") => Respawn(message);

        private void Respawn(string message)
        {
            if (!IsRespawning) StartCoroutine(RespawnRoutine(message));
        }

        private IEnumerator RespawnRoutine(string message)
        {
            IsRespawning = true;
            gear?.Holster(this);
            movement.enabled = false;
            DialogueUi.Instance?.CloseAll();
            RespawnStarted?.Invoke(message);
            yield return new WaitForSeconds(fadeSeconds);

            PlayerSpawnPoint spawn = PlayerSpawnPoint.Current;
            body.enabled = false;
            if (spawn != null) transform.SetPositionAndRotation(spawn.transform.position, Quaternion.Euler(0f, spawn.transform.eulerAngles.y, 0f));
            else transform.SetPositionAndRotation(startPosition, startRotation);
            body.enabled = true;
            movement.ResetMotion();
            health.Restore();

            IsRespawning = false;
            movement.enabled = true;
            gear?.Unholster(this);
            Respawned?.Invoke();
            if (wakeUp != null) wakeUp.Play(false);
        }
    }
}
