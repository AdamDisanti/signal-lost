using System.Collections;
using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Effects;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// The Coreworm's arena, and how it changes each phase:
    /// - Phase 1: clear, open dunes.
    /// - Phase 2: a sandstorm. Thick sand fog, sand streaming past, a steady wind pushing the player, wandering sand
    ///   cyclones that drag them in, and fodder pouring in.
    /// - Phase 3: collapse. Floor tiles shake and crack, then fall away in waves, leaving pits and islands around the
    ///   rock pillars. Falling in hurts and drops you back on the nearest solid ground.
    /// - Phase 4: meltdown. Lanes of fire sweep across what is left, so the safe spots keep moving.
    /// Everything is put back by <see cref="ResetArena"/> (the player died) and calmed by <see cref="Settle"/> (the boss died).
    /// </summary>
    public sealed class CorewormArena : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [Header("Layout")]
        [SerializeField] private Transform centre;
        [SerializeField, Min(10f)] private float radius = 34f;
        [Tooltip("Where the player is dropped when the worm drags them in.")]
        [SerializeField] private Transform playerDrop;
        [Tooltip("The floor, in tiles: these collapse in phase 3.")]
        [SerializeField] private Transform[] floorTiles = new Transform[0];
        [Tooltip("Pillars and wreckage: cover, and the floor around them never collapses.")]
        [SerializeField] private Transform[] cover = new Transform[0];
        [SerializeField] private FodderSpawner fodderSpawner;

        [Header("Sandstorm (phase 2)")]
        [SerializeField] private Color stormFog = new Color(0.78f, 0.62f, 0.4f);
        [SerializeField, Min(1f)] private float stormFogEnd = 28f;
        [SerializeField, Min(0f)] private float windSpeed = 1.3f;
        [SerializeField, Range(0, 6)] private int cyclones = 3;

        [Header("Collapse (phase 3)")]
        [Tooltip("Fraction of the floor that ends up falling away.")]
        [SerializeField, Range(0f, 0.8f)] private float collapseFraction = 0.5f;
        [SerializeField, Min(1)] private int tilesPerWave = 7;
        [SerializeField, Min(0.5f)] private float waveInterval = 4f;
        [SerializeField, Min(0.2f)] private float crackWarning = 1.8f;
        [Tooltip("Tiles this close to cover or the drop point never fall.")]
        [SerializeField, Min(0f)] private float safeRadius = 5f;
        [SerializeField, Min(0f)] private float pitDamage = 20f;

        [Header("Meltdown (phase 4)")]
        [SerializeField, Min(0.5f)] private float fireLaneInterval = 2.6f;
        [SerializeField, Min(0.2f)] private float fireLaneWarning = 1.1f;
        [SerializeField] private FireStyle fieldFire = new FireStyle { damage = 10f, interval = 0.5f, duration = 6f, smoke = 0.6f, fierceness = 0.8f };
        [SerializeField] private Material warningMaterial;

        private readonly List<Vector3> tileRest = new List<Vector3>();
        private readonly HashSet<Transform> fallen = new HashSet<Transform>();
        private readonly HashSet<Transform> safe = new HashSet<Transform>();
        private readonly List<SandCyclone> liveCyclones = new List<SandCyclone>();
        private readonly List<GameObject> spawned = new List<GameObject>();
        private Coroutine collapse, meltdown;
        private bool storm;
        private ParticleSystem stormParticles;
        private Transform player;
        private CharacterController playerBody;
        private Health playerHealth;
        private MaterialPropertyBlock block;
        private bool fogSaved, savedFog;
        private Color savedFogColor;
        private float savedFogStart, savedFogEnd;
        private FogMode savedFogMode;
        private Vector3 wind;

        public Vector3 Centre => centre != null ? centre.position : transform.position;
        public float Radius => radius;
        public float GroundY => Centre.y;
        public Transform PlayerDrop => playerDrop;
        public IReadOnlyList<Transform> Cover => cover;
        /// <summary>The fight is on (not resetting): pits rescue the player.</summary>
        public bool Active { get; set; }

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            foreach (Transform tile in floorTiles) tileRest.Add(tile != null ? tile.position : Vector3.zero);
            foreach (Transform tile in floorTiles)
            {
                if (tile == null) continue;
                bool nearCover = false;
                foreach (Transform item in cover)
                    if (item != null && Flat(item.position - tile.position) < safeRadius + 2.5f) nearCover = true;
                if (playerDrop != null && Flat(playerDrop.position - tile.position) < safeRadius + 2.5f) nearCover = true;
                if (nearCover) safe.Add(tile);
            }
            if (fodderSpawner != null) fodderSpawner.enabled = false;
        }

        private void Start()
        {
            var controller = FindAnyObjectByType<SignalLost.Player.FirstPersonController>();
            if (controller == null) return;
            player = controller.transform;
            playerBody = controller.GetComponent<CharacterController>();
            playerHealth = controller.GetComponent<Health>();
        }

        private static float Flat(Vector3 v)
        {
            v.y = 0f;
            return v.magnitude;
        }

        /// <summary>Is this point over solid floor (not a collapsed pit)?</summary>
        public bool IsSolid(Vector3 point)
        {
            Transform tile = TileAt(point);
            return tile != null && !fallen.Contains(tile);
        }

        private Transform TileAt(Vector3 point)
        {
            Transform best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < floorTiles.Length; i++)
            {
                if (floorTiles[i] == null) continue;
                float d = Flat(tileRest[i] - point);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = floorTiles[i];
                }
            }
            return best;
        }

        /// <summary>The nearest solid floor to a point, on top of the tiles.</summary>
        public Vector3 NearestSolid(Vector3 point)
        {
            Vector3 best = playerDrop != null ? playerDrop.position : Centre;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < floorTiles.Length; i++)
            {
                Transform tile = floorTiles[i];
                if (tile == null || fallen.Contains(tile)) continue;
                float d = Flat(tileRest[i] - point);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = new Vector3(tileRest[i].x, GroundY, tileRest[i].z);
                }
            }
            return best;
        }

        // ---------- Phases ----------

        public void BeginPhase(int phase)
        {
            switch (phase)
            {
                case 2: StartSandstorm(); break;
                case 3:
                    StopSandstorm();
                    if (fodderSpawner != null)
                    {
                        fodderSpawner.Clear();
                        fodderSpawner.enabled = false;
                    }
                    collapse = StartCoroutine(Collapse());
                    break;
                case 4: meltdown = StartCoroutine(Meltdown()); break;
            }
        }

        private void StartSandstorm()
        {
            if (storm) return;
            storm = true;
            SaveFog();
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = stormFog;
            RenderSettings.fogStartDistance = 2f;
            RenderSettings.fogEndDistance = stormFogEnd;
            float angle = Random.value * 360f;
            wind = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            BuildStormParticles();
            for (int i = 0; i < cyclones; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / Mathf.Max(1, cyclones);
                liveCyclones.Add(SandCyclone.Spawn(Centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * 0.55f, Centre, radius, player));
            }
            if (fodderSpawner != null) fodderSpawner.enabled = true;
            MiniBossHud.Message("A SANDSTORM ROLLS IN", new Color(1f, 0.8f, 0.5f), 2.5f);
        }

        private void StopSandstorm()
        {
            if (!storm) return;
            storm = false;
            foreach (SandCyclone cyclone in liveCyclones) if (cyclone != null) cyclone.Dissipate();
            liveCyclones.Clear();
            if (stormParticles != null) Destroy(stormParticles.gameObject);
            RestoreFog();
        }

        private void BuildStormParticles()
        {
            Camera view = Camera.main;
            if (view == null) return;
            var go = new GameObject("Sandstorm");
            go.transform.SetParent(view.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.rotation = Quaternion.LookRotation(wind);
            stormParticles = go.AddComponent<ParticleSystem>();
            stormParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = stormParticles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = 1.4f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(14f, 20f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
            main.startColor = new Color(0.85f, 0.7f, 0.48f, 0.5f);
            main.maxParticles = 900;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = stormParticles.emission;
            emission.rateOverTime = 420f;
            var shape = stormParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 8f, 4f);
            shape.position = new Vector3(0f, 0f, -14f); // upwind of the camera, blowing past it
            var noise = stormParticles.noise;
            noise.enabled = true;
            noise.strength = 1.2f;
            noise.frequency = 0.5f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = EffectMaterials.AlphaBlended(EffectMaterials.Look.Smoke);
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 2f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            stormParticles.Play();
        }

        private IEnumerator Collapse()
        {
            MiniBossHud.Message("THE GROUND IS GIVING WAY!", new Color(1f, 0.7f, 0.4f), 2.5f);
            var candidates = new List<Transform>();
            foreach (Transform tile in floorTiles)
                if (tile != null && !safe.Contains(tile)) candidates.Add(tile);
            int target = Mathf.RoundToInt(floorTiles.Length * collapseFraction);
            int dropped = 0;
            while (dropped < target && candidates.Count > 0)
            {
                // Favour tiles near the player, so the floor really disappears from under them.
                Vector3 near = player != null ? player.position : Centre;
                candidates.Sort((a, b) => Flat(a.position - near).CompareTo(Flat(b.position - near)));
                int wave = Mathf.Min(tilesPerWave, candidates.Count, target - dropped);
                var picks = new List<Transform>();
                for (int i = 0; i < wave; i++)
                {
                    int index = Mathf.Min(candidates.Count - 1, Random.Range(0, Mathf.Min(candidates.Count, wave * 3)));
                    picks.Add(candidates[index]);
                    candidates.RemoveAt(index);
                }
                foreach (Transform tile in picks) StartCoroutine(DropTile(tile));
                dropped += picks.Count;
                yield return new WaitForSeconds(waveInterval);
            }
        }

        private IEnumerator DropTile(Transform tile)
        {
            int index = System.Array.IndexOf(floorTiles, tile);
            Vector3 rest = tileRest[index];
            var renderer = tile.GetComponent<Renderer>();
            float t = 0f;
            float nextDust = 0f;
            while (t < crackWarning)
            {
                // Shaking and darkening, dust puffing up: get off it.
                t += Time.deltaTime;
                float k = t / crackWarning;
                tile.position = rest + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * 0.05f * k + Vector3.down * 0.15f * k;
                Tint(renderer, Color.Lerp(Color.white, new Color(0.55f, 0.4f, 0.3f), k));
                if (Time.time >= nextDust)
                {
                    nextDust = Time.time + 0.25f;
                    ParticleBurst.Spawn(ParticleBurst.Kind.Sand, rest + Vector3.up * 0.1f + Random.insideUnitSphere * 1.5f, Vector3.up, 3, 0.6f, 1.5f, 50f);
                }
                yield return null;
            }
            fallen.Add(tile);
            foreach (Collider collider in tile.GetComponentsInChildren<Collider>()) collider.enabled = false;
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, rest + Vector3.up * 0.3f, Vector3.up, 16, 1.4f, 4f, 70f);
            CameraShake.Add(0.06f);
            float fall = 0f;
            while (fall < 1.5f && fallen.Contains(tile))
            {
                fall += Time.deltaTime;
                tile.position = rest + Vector3.down * (fall * fall * 14f);
                yield return null;
            }
            if (fallen.Contains(tile) && renderer != null) renderer.enabled = false;
        }

        private IEnumerator Meltdown()
        {
            MiniBossHud.Message("THE CORE IS MELTING DOWN: THE SAND IS ON FIRE!", new Color(1f, 0.5f, 0.2f), 2.5f);
            while (true)
            {
                // A lane of fire across the arena, through or near the player, so where is safe keeps changing.
                Vector3 near = player != null ? player.position : Centre;
                Vector3 across = Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward;
                Vector3 through = near + Random.insideUnitSphere * 4f;
                through.y = GroundY;
                var points = new List<Vector3>();
                for (float d = -radius; d <= radius; d += 2.6f)
                {
                    Vector3 p = through + across * d;
                    if (Flat(p - Centre) > radius - 1.5f || !IsSolid(p)) continue;
                    points.Add(new Vector3(p.x, GroundY, p.z));
                }
                var markers = new List<GroundMarker>();
                foreach (Vector3 p in points) markers.Add(GroundMarker.Spawn(warningMaterial, null, p, 1.3f));
                float t = 0f;
                while (t < fireLaneWarning)
                {
                    t += Time.deltaTime;
                    foreach (GroundMarker marker in markers) if (marker != null) marker.Progress = t / fireLaneWarning;
                    yield return null;
                }
                foreach (GroundMarker marker in markers) if (marker != null) Destroy(marker.gameObject);
                foreach (Vector3 p in points) spawned.Add(FireZone.Spawn(fieldFire, p, 1.4f, gameObject, null).gameObject);
                spawned.RemoveAll(item => item == null);
                yield return new WaitForSeconds(fireLaneInterval);
            }
        }

        private void Tint(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColor, renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(BaseColor)
                ? renderer.sharedMaterial.GetColor(BaseColor) * color : color);
            renderer.SetPropertyBlock(block);
        }

        // ---------- Every frame ----------

        private void Update()
        {
            if (player == null || playerBody == null || !playerBody.enabled) return;
            if (storm) playerBody.Move(wind * windSpeed * Time.deltaTime);

            // Fell into a pit: hurt, and hauled back up onto the nearest solid floor.
            if (Active && Flat(player.position - Centre) < radius + 6f && player.position.y < GroundY - 3f)
            {
                Vector3 safeSpot = NearestSolid(player.position) + Vector3.up * 0.2f;
                playerBody.enabled = false;
                player.position = safeSpot;
                playerBody.enabled = true;
                if (playerHealth != null) playerHealth.TakeDamage(pitDamage, gameObject);
                ParticleBurst.Spawn(ParticleBurst.Kind.Sand, safeSpot, Vector3.up, 14, 0.8f, 3f, 60f);
                MiniBossHud.Message("You fell into the pit!", new Color(1f, 0.7f, 0.5f), 1.5f);
            }
        }

        // ---------- Reset ----------

        /// <summary>Puts everything back as it was before the fight (the player died or the fight restarts).</summary>
        public void ResetArena()
        {
            Active = false;
            StopAllCoroutines();
            collapse = meltdown = null;
            StopSandstorm();
            for (int i = 0; i < floorTiles.Length; i++)
            {
                Transform tile = floorTiles[i];
                if (tile == null) continue;
                tile.position = tileRest[i];
                foreach (Collider collider in tile.GetComponentsInChildren<Collider>()) collider.enabled = true;
                var renderer = tile.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.enabled = true;
                    renderer.SetPropertyBlock(null);
                }
            }
            fallen.Clear();
            ClearFires();
            if (fodderSpawner != null)
            {
                fodderSpawner.Clear();
                fodderSpawner.enabled = false;
            }
        }

        /// <summary>The boss is dead: the storm, the collapse and the fire lanes stop; fires die down; fodder stops.</summary>
        public void Settle()
        {
            Active = false;
            if (collapse != null) StopCoroutine(collapse);
            if (meltdown != null) StopCoroutine(meltdown);
            StopSandstorm();
            if (fodderSpawner != null) fodderSpawner.Stop();
        }

        private void ClearFires()
        {
            foreach (FireZone fire in FindObjectsByType<FireZone>())
            {
                Vector3 offset = fire.transform.position - Centre;
                if (Flat(offset) < radius + 8f) Destroy(fire.gameObject);
            }
            spawned.Clear();
        }

        private void SaveFog()
        {
            if (fogSaved) return;
            fogSaved = true;
            savedFog = RenderSettings.fog;
            savedFogColor = RenderSettings.fogColor;
            savedFogStart = RenderSettings.fogStartDistance;
            savedFogEnd = RenderSettings.fogEndDistance;
            savedFogMode = RenderSettings.fogMode;
        }

        private void RestoreFog()
        {
            if (!fogSaved) return;
            fogSaved = false;
            RenderSettings.fog = savedFog;
            RenderSettings.fogColor = savedFogColor;
            RenderSettings.fogStartDistance = savedFogStart;
            RenderSettings.fogEndDistance = savedFogEnd;
            RenderSettings.fogMode = savedFogMode;
        }

        private void OnDisable() => RestoreFog();

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.8f);
            Vector3 c = Centre;
            Vector3 previous = c + Vector3.right * radius;
            for (int i = 1; i <= 64; i++)
            {
                float a = i * Mathf.PI * 2f / 64f;
                Vector3 next = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
