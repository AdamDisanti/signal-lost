using System;
using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Player;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// Keeps a fight topped up with fodder: while the player is near, it spawns waves of the listed enemies at the
    /// edge of its area, preferring spots the player isn't looking at, up to a cap on how many are alive at once.
    /// Built for the desert mini-boss arenas (fodder keeps coming while the mini-boss patrols), but works on its own.
    ///
    /// Put it at the centre of an area that has a NavMesh. Spawned enemies are its children and are removed once
    /// dead. Assign Stop When Dead (for example a mini-boss's Health) to stop spawning when that dies; the fodder
    /// already out keeps fighting. All numbers are placeholders.
    /// </summary>
    public sealed class FodderSpawner : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("An enemy prefab with a Health: desert fodder, or the hermit crab and mortar frog.")]
            public GameObject prefab;
            [Tooltip("Relative chance this entry is picked for a wave.")]
            [Min(0f)] public float weight = 1f;
            [Tooltip("How many spawn together, for pack enemies like Spark Ticks.")]
            [Min(1)] public int minGroup = 1;
            [Min(1)] public int maxGroup = 1;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        [Header("Where")]
        [SerializeField, Min(0f)] private float spawnRadiusMin = 8f;
        [SerializeField, Min(0.5f)] private float spawnRadiusMax = 14f;
        [Tooltip("Spawn away from where the player is looking when possible, so enemies don't pop in on screen.")]
        [SerializeField] private bool preferOutOfView = true;
        [Tooltip("Never spawn closer than this to the player.")]
        [SerializeField, Min(0f)] private float minPlayerDistance = 6f;

        [Header("When")]
        [Tooltip("Spawning runs while the player is within this distance of the spawner. 0: always.")]
        [SerializeField, Min(0f)] private float activationRange = 26f;
        [SerializeField, Min(1)] private int maxAlive = 8;
        [Tooltip("Seconds between waves.")]
        [SerializeField, Min(0.2f)] private float waveInterval = 5f;
        [Tooltip("Seconds before the first wave once active.")]
        [SerializeField, Min(0f)] private float firstWaveDelay = 0.5f;
        [Tooltip("Optional. Spawning stops for good when this dies (a mini-boss).")]
        [SerializeField] private Health stopWhenDead;

        private readonly List<GameObject> alive = new List<GameObject>();
        private Transform player;
        private Transform view;
        private float nextWave = -1f;
        private bool stopped;

        /// <summary>How many of its enemies have died so far.</summary>
        public int Killed { get; private set; }
        public int AliveCount => alive.Count;
        public bool IsStopped => stopped;
        /// <summary>Raised when one of its enemies dies.</summary>
        public event Action<FodderSpawner> EnemyKilled;

        /// <summary>Stops spawning for good. Enemies already out keep fighting.</summary>
        public void Stop() => stopped = true;

        /// <summary>Removes every enemy it has spawned that is still around (a mini-boss resetting its arena).</summary>
        public void Clear()
        {
            foreach (GameObject item in alive)
                if (item != null) Destroy(item);
            alive.Clear();
            nextWave = -1f;
        }

        private void OnEnable()
        {
            if (stopWhenDead != null) stopWhenDead.Died += OnStopTargetDied;
        }

        private void OnDisable()
        {
            if (stopWhenDead != null) stopWhenDead.Died -= OnStopTargetDied;
        }

        private void OnStopTargetDied(Health _) => Stop();

        private void Start()
        {
            var controller = FindAnyObjectByType<FirstPersonController>();
            if (controller != null)
            {
                player = controller.transform;
                Camera camera = controller.GetComponentInChildren<Camera>();
                view = camera != null ? camera.transform : player;
            }
        }

        private void Update()
        {
            alive.RemoveAll(item => item == null);
            if (stopped || player == null || entries.Length == 0) return;

            bool active = activationRange <= 0f || FlatDistance(player.position, transform.position) <= activationRange;
            if (!active)
            {
                nextWave = -1f;
                return;
            }
            if (nextWave < 0f) nextWave = Time.time + firstWaveDelay;
            if (Time.time < nextWave) return;
            nextWave = Time.time + waveInterval;
            SpawnWave();
        }

        private void SpawnWave()
        {
            Entry entry = Pick();
            if (entry == null || entry.prefab == null) return;
            int room = maxAlive - alive.Count;
            if (room <= 0) return;
            int group = Mathf.Min(room, Random.Range(entry.minGroup, Mathf.Max(entry.minGroup, entry.maxGroup) + 1));
            if (!TryFindSpawnPoint(out Vector3 centre)) return;

            for (int i = 0; i < group; i++)
            {
                Vector3 point = centre;
                if (i > 0)
                {
                    Vector2 jitter = Random.insideUnitCircle * 1.6f;
                    if (NavMesh.SamplePosition(centre + new Vector3(jitter.x, 0f, jitter.y), out NavMeshHit near, 1.5f, NavMesh.AllAreas))
                        point = near.position;
                }
                Vector3 toPlayer = player.position - point;
                toPlayer.y = 0f;
                Quaternion facing = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer) : Quaternion.identity;
                GameObject spawned = Instantiate(entry.prefab, point, facing, transform);
                spawned.name = entry.prefab.name;
                // Waves defend the spawner's whole area, not just the spot they appeared on.
                foreach (ITerritorial enemy in spawned.GetComponentsInChildren<ITerritorial>()) enemy.Territory.Zone = Zone;
                alive.Add(spawned);
                var health = spawned.GetComponent<Health>();
                if (health != null) health.Died += OnEnemyDied;
            }
        }

        private CombatZone zone;

        /// <summary>The area its waves defend: its own CombatZone, or one made from the activation range.</summary>
        private CombatZone Zone
        {
            get
            {
                if (zone != null) return zone;
                zone = GetComponent<CombatZone>();
                if (zone != null) return zone;
                zone = gameObject.AddComponent<CombatZone>();
                zone.Radius = activationRange > 0f ? activationRange : spawnRadiusMax + 6f;
                return zone;
            }
        }

        private void OnEnemyDied(Health health)
        {
            health.Died -= OnEnemyDied;
            Killed++;
            alive.Remove(health.gameObject);
            // Desert fodder removes its own body; other enemies (crabs, frogs) only hide theirs, so tidy them up.
            if (health.GetComponent<DesertFodder>() == null) Destroy(health.gameObject, 6f);
            EnemyKilled?.Invoke(this);
        }

        private Entry Pick()
        {
            float total = 0f;
            foreach (Entry entry in entries) if (entry != null && entry.prefab != null) total += entry.weight;
            if (total <= 0f) return null;
            float roll = Random.value * total;
            foreach (Entry entry in entries)
            {
                if (entry == null || entry.prefab == null) continue;
                roll -= entry.weight;
                if (roll <= 0f) return entry;
            }
            return entries[entries.Length - 1];
        }

        /// <summary>A NavMesh point on the ring around the spawner, preferably behind or beside the player's view.</summary>
        private bool TryFindSpawnPoint(out Vector3 point)
        {
            point = transform.position;
            bool found = false;
            float bestScore = float.NegativeInfinity;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float distance = Random.Range(spawnRadiusMin, spawnRadiusMax);
                Vector3 candidate = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;
                if (FlatDistance(hit.position, player.position) < minPlayerDistance) continue;

                float score = Random.value * 0.2f;
                if (preferOutOfView && view != null)
                {
                    Vector3 toPoint = hit.position - view.position;
                    toPoint.y = 0f;
                    Vector3 forward = view.forward;
                    forward.y = 0f;
                    if (toPoint.sqrMagnitude > 0.01f && forward.sqrMagnitude > 0.01f)
                        score -= Vector3.Dot(forward.normalized, toPoint.normalized); // behind the player scores highest
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    point = hit.position;
                    found = true;
                }
            }
            return found;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
            DrawCircle(spawnRadiusMin);
            DrawCircle(spawnRadiusMax);
            if (activationRange > 0f)
            {
                Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.5f);
                DrawCircle(activationRange);
            }
        }

        private void DrawCircle(float radius)
        {
            Vector3 previous = transform.position + Vector3.right * radius;
            for (int i = 1; i <= 40; i++)
            {
                float a = i * Mathf.PI * 2f / 40f;
                Vector3 next = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
