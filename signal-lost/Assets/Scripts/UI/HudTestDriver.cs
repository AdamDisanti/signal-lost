using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Enemies;
using UnityEngine;

namespace SignalLost.UI
{
    /// <summary>
    /// TEST SCENE ONLY. Pretends to be the systems that don't exist yet so every part of the HUD can be seen working:
    /// - Scanner: points at the Signal Beacon object; strength rises as you walk toward it.
    /// - Objective: counts crab kills toward three, then sends you to the beacon.
    /// Delete this once the real scanner and quest systems call PlayerHud themselves.
    /// </summary>
    public sealed class HudTestDriver : MonoBehaviour
    {
        [SerializeField] private Transform signalBeacon;
        [Tooltip("Distance at which the signal is weakest (one pip).")]
        [SerializeField, Min(1f)] private float scannerRange = 40f;
        [SerializeField, Min(1)] private int encountersNeeded = 3;

        private readonly List<Health> watched = new List<Health>();
        private int kills;
        private Transform player;
        private bool signalFound;

        private void Start()
        {
            var controller = FindAnyObjectByType<Player.FirstPersonController>();
            if (controller != null) player = controller.transform;

            foreach (HermitCrab crab in FindObjectsByType<HermitCrab>(FindObjectsSortMode.None))
            {
                var health = crab.GetComponent<Health>();
                if (health == null) continue;
                health.Died += OnEnemyDied;
                watched.Add(health);
            }
            UpdateObjective();
        }

        private void OnDestroy()
        {
            foreach (Health health in watched)
                if (health != null) health.Died -= OnEnemyDied;
        }

        private void OnEnemyDied(Health _)
        {
            kills++;
            UpdateObjective();
        }

        private void Update()
        {
            PlayerHud hud = PlayerHud.Current;
            if (hud == null || signalBeacon == null || player == null) return;

            float distance = Vector3.Distance(player.position, signalBeacon.position);
            hud.SetScannerSignal(signalBeacon.position, 1f - distance / scannerRange);

            if (!signalFound && kills >= encountersNeeded && distance < 3f)
            {
                signalFound = true;
                hud.SetObjective("Signal found! (end of test)");
            }
        }

        private void UpdateObjective()
        {
            if (signalFound) return;
            PlayerHud.Current?.SetObjective(kills < encountersNeeded
                ? $"Defeat creatures ({kills}/{encountersNeeded})"
                : "Follow the signal");
        }
    }
}
