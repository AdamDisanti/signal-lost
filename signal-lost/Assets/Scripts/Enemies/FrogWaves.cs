using System;
using System.Collections.Generic;
using SignalLost.Scanner;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.Enemies
{
    /// <summary>
    /// Mortar frogs at the pond, in waves that grow (meeting notes 10-23: 20 frogs in 3 waves, 5 then 7 then 8). Nothing
    /// spawns until <see cref="Begin"/>; each wave hops in from the bank once the last one is beaten. Every frog
    /// carries a quest signal, so the scanner points at the nearest one. The frogs share this object's
    /// <see cref="CombatZone"/>. Defeated frogs are removed once their death has played.
    /// </summary>
    [RequireComponent(typeof(CombatZone))]
    public sealed class FrogWaves : MonoBehaviour
    {
        [SerializeField] private MortarFrog frogPrefab;
        [Tooltip("Frogs in each wave.")]
        [SerializeField] private int[] waves = { 5, 7, 8 };
        [Tooltip("Where frogs appear, around the bank.")]
        [SerializeField] private Transform[] spawnPoints = Array.Empty<Transform>();
        [SerializeField, Min(0f)] private float delayBetweenWaves = 3f;
        [SerializeField, Min(0f)] private float removeAfter = 4f;

        private readonly List<MortarFrog> alive = new List<MortarFrog>();
        private CombatZone zone;
        private int wave = -1;
        private float nextWave = -1f;

        public bool Started => wave >= 0;
        public bool Finished { get; private set; }
        public int Total
        {
            get
            {
                int total = 0;
                foreach (int count in waves) total += count;
                return total;
            }
        }
        public int Defeated { get; private set; }
        public int Wave => wave + 1;
        public int WaveCount => waves.Length;
        public IReadOnlyList<MortarFrog> Alive => alive;
        public event Action Cleared;

        private void Awake() => zone = GetComponent<CombatZone>();

        /// <summary>Starts the first wave (once).</summary>
        public void Begin()
        {
            if (Started || frogPrefab == null) return;
            Spawn(0);
        }

        private void Update()
        {
            if (!Started || Finished) return;
            for (int i = alive.Count - 1; i >= 0; i--)
            {
                MortarFrog frog = alive[i];
                if (frog != null && !frog.IsDead) continue;
                alive.RemoveAt(i);
                Defeated++;
                if (frog == null) continue;
                if (frog.TryGetComponent(out QuestSignalSource signal)) signal.enabled = false; // stop pointing at corpses
                Destroy(frog.gameObject, removeAfter);
            }
            if (alive.Count > 0) return;
            if (wave + 1 >= waves.Length)
            {
                Finished = true;
                Cleared?.Invoke();
                return;
            }
            if (nextWave < 0f) nextWave = Time.time + delayBetweenWaves;
            else if (Time.time >= nextWave) Spawn(wave + 1);
        }

        private void Spawn(int index)
        {
            wave = index;
            nextWave = -1f;
            int count = waves[index];
            int offset = UnityEngine.Random.Range(0, Mathf.Max(1, spawnPoints.Length));
            for (int i = 0; i < count; i++)
            {
                Vector3 at = spawnPoints.Length > 0 ? spawnPoints[(offset + i) % spawnPoints.Length].position : transform.position;
                at += new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), 0f, UnityEngine.Random.Range(-1.5f, 1.5f));
                if (NavMesh.SamplePosition(at, out NavMeshHit hit, 4f, NavMesh.AllAreas)) at = hit.position;
                Vector3 inward = transform.position - at;
                inward.y = 0f;
                MortarFrog frog = Instantiate(frogPrefab, at, inward.sqrMagnitude > 0.01f ? Quaternion.LookRotation(inward) : Quaternion.identity);
                frog.name = $"Pond Frog {Defeated + alive.Count + 1}";
                frog.Territory.Zone = zone;
                if (frog.GetComponent<QuestSignalSource>() == null) frog.gameObject.AddComponent<QuestSignalSource>();
                alive.Add(frog);
            }
        }
    }
}
