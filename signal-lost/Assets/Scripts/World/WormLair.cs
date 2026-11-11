using System;
using System.Collections;
using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Enemies.Desert;
using SignalLost.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.World
{
    /// <summary>
    /// The Core Worm's lair under the desert (11-08): a cavern under its old sand bowl, where it drags the player for
    /// the fight (its <see cref="CorewormArena"/> is the cavern floor).
    /// <list type="bullet">
    /// <item><b>Cave light.</b> While the player is down here the sun, sky light and haze give way to a dim, dusty
    /// cave lit by the glowing crystals. It eases back as they climb out (or respawn on the surface).</item>
    /// <item><b>The collapse</b> (11-09: the same every time, with real physics between the rocks). Along one side of
    /// the roof runs a long gash choked with broken rock under a cracked face, with the desert's sand above (a separate
    /// lid in the dunes). <see cref="BeginCollapse"/>, started by the desert's story once the Engine Core and the trophy
    /// are picked up, brings it down. First the floor where it will land flashes red while the roof rumbles and sheds
    /// grit. Then the face gives way: sand pours through and piles into a debris cone under the gash, and the rocks come
    /// down onto it. Their fall is a <see cref="CollapseBake"/>: a physics simulation the level builder ran once and
    /// recorded, so every bounce and knock is the physics engine's, and it lands the same way every time, leaving a
    /// route the builder checked is open. Anyone still in the red zone is crushed.</item>
    /// <item><b>Climbing out:</b> up the debris cone, between the rocks, through the open gash to the dunes; reaching
    /// the surface raises <see cref="Emerged"/>. It all stays, so the player can go back down and up again.</item>
    /// </list>
    /// A player who falls into one of the fight's pits after the fight (when the arena no longer rescues) is put back
    /// on the floor.
    /// </summary>
    public sealed class WormLair : MonoBehaviour
    {
        [SerializeField] private CorewormArena arena;
        [Tooltip("The desert surface's height at the lip of the gash (where the climb comes out).")]
        [SerializeField] private float surfaceY;
        [Tooltip("How far from the arena's centre the cavern reaches.")]
        [SerializeField, Min(10f)] private float footprint = 44f;

        [Header("Cave light")]
        [SerializeField] private Color caveFog = new Color(0.2f, 0.13f, 0.09f);
        [SerializeField] private float caveFogStart = 12f;
        [SerializeField] private float caveFogEnd = 95f;
        [SerializeField] private Color caveSky = new Color(0.4f, 0.3f, 0.25f);
        [SerializeField] private Color caveEquator = new Color(0.42f, 0.29f, 0.2f);
        [SerializeField] private Color caveGround = new Color(0.22f, 0.15f, 0.1f);
        [SerializeField, Range(0f, 2f)] private float caveSun = 0.05f;

        [Header("The collapse")]
        [Tooltip("The broken rock choking the gash, each where it waits; in the same order as the recording's tracks.")]
        [SerializeField] private Transform[] rocks = Array.Empty<Transform>();
        [Tooltip("The recorded rockfall (a physics simulation the level builder ran).")]
        [SerializeField] private CollapseBake bake;
        [Tooltip("The debris cone the sand piles into (hidden until the collapse). Its mesh's second UV set holds, for " +
                 "every vertex, the point of the cone's surface it rides on as the cone grows.")]
        [SerializeField] private MeshFilter mound;
        [Tooltip("Where the sand lands (relative to the lair): the cone grows from here.")]
        [SerializeField] private Vector3 pourPoint;
        [Tooltip("The slope the growing cone keeps (rise over run).")]
        [SerializeField, Min(0.1f)] private float reposeTan = 0.65f;
        [SerializeField, Min(0.5f)] private float growTime = 2.6f;
        [Tooltip("The rock face holding it all up, under the sand lid; it gives way when it all comes down.")]
        [SerializeField] private GameObject ceilingCap;
        [Tooltip("The sand packed round the choked rocks. It crumbles and falls piece by piece as they come loose: its " +
                 "mesh's third UV holds each piece's middle, its fourth when (from the start of the fall) and how fast it goes.")]
        [SerializeField] private MeshFilter chokeSand;
        [Tooltip("The patch of desert sand covering the gash (in the environment), which drops through.")]
        [SerializeField] private GameObject surfaceLid;
        [Tooltip("The red warning on the floor: where the debris and every rock will land or roll (hidden until the warning).")]
        [SerializeField] private MeshFilter crushZone;
        [Tooltip("Which floor cells that covers (relative to the lair): where you get crushed.")]
        [SerializeField] private Vector2 crushOrigin;
        [SerializeField, Min(0.1f)] private float crushCell = 1.5f;
        [SerializeField, Min(1)] private int crushColumns = 1;
        [SerializeField] private bool[] crushCells = Array.Empty<bool>();
        [SerializeField, Min(0.5f)] private float warningTime = 4.5f;
        [Tooltip("The route the builder checked is open, from the foot of the debris to the surface (relative to the lair).")]
        [SerializeField] private Vector3[] climbPath = Array.Empty<Vector3>();
        [Tooltip("Where the climb comes out on the surface.")]
        [SerializeField] private Transform exitPoint;
        [Tooltip("The middle of the gash at the surface, facing along it.")]
        [SerializeField] private Transform hole;
        [Tooltip("The gash's size (across, along), for the sand pouring through it.")]
        [SerializeField] private Vector2 holeSize = new Vector2(11f, 17f);

        private Transform player;
        private CharacterController playerBody;
        private Health playerHealth;
        private float amount, shown = -1f;
        private Light sun;
        private float sunIntensity;
        private bool fog;
        private FogMode fogMode;
        private Color fogColor, sky, equator, ground;
        private float fogStart, fogEnd;
        private AmbientMode ambientMode;
        private bool captured, collapsing;

        /// <summary>Raised when the rubble has settled and the way out is open.</summary>
        public event Action Collapsed;
        /// <summary>Raised once, when the player is back on the surface after the collapse.</summary>
        public event Action Emerged;

        public CorewormArena Arena => arena;
        public bool IsOpen { get; private set; }
        public bool HasEmerged { get; private set; }
        /// <summary>The rubble is coming down right now (the red zone kills).</summary>
        public bool Falling { get; private set; }
        /// <summary>All the rocks are at rest, where the recording leaves them.</summary>
        public bool Settled { get; private set; }
        /// <summary>0 on the surface, 1 down in the cavern.</summary>
        public float Underground => amount;
        public Transform ExitPoint => exitPoint;
        /// <summary>The checked route up the debris, from its foot to the surface, in world space.</summary>
        public Vector3[] ClimbPath => Array.ConvertAll(climbPath, p => transform.TransformPoint(p));
        public IReadOnlyList<Transform> Rocks => rocks;
        public CollapseBake Bake => bake;
        public Transform Mound => mound != null ? mound.transform : null;
        public float SurfaceY => surfaceY;
        private Vector3 Centre => arena != null ? arena.Centre : transform.position;

        /// <summary>Is a point (world) where the debris or a rock will land or roll, i.e. in the red zone?</summary>
        public bool InCrushZone(Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point);
            int x = Mathf.FloorToInt((local.x - crushOrigin.x) / crushCell), z = Mathf.FloorToInt((local.z - crushOrigin.y) / crushCell);
            int rows = crushCells.Length / Mathf.Max(1, crushColumns);
            if (x < 0 || z < 0 || x >= crushColumns || z >= rows) return false;
            return crushCells[z * crushColumns + x];
        }

        private void Awake()
        {
            // The debris isn't there yet.
            if (mound != null) mound.gameObject.SetActive(false);
            if (crushZone != null) crushZone.gameObject.SetActive(false);
        }

        private void Start()
        {
            var controller = FindAnyObjectByType<FirstPersonController>();
            if (controller != null)
            {
                player = controller.transform;
                playerBody = controller.GetComponent<CharacterController>();
                playerHealth = controller.GetComponent<Health>();
            }
            Capture();
        }

        private void OnDisable() => Apply(0f, true);

        // The surface's lighting, to come back to.
        private void Capture()
        {
            if (captured) return;
            captured = true;
            sun = RenderSettings.sun;
            sunIntensity = sun != null ? sun.intensity : 1f;
            fog = RenderSettings.fog;
            fogMode = RenderSettings.fogMode;
            fogColor = RenderSettings.fogColor;
            fogStart = RenderSettings.fogStartDistance;
            fogEnd = RenderSettings.fogEndDistance;
            ambientMode = RenderSettings.ambientMode;
            sky = RenderSettings.ambientSkyColor;
            equator = RenderSettings.ambientEquatorColor;
            ground = RenderSettings.ambientGroundColor;
        }

        private bool Below(Vector3 point) => Flat(point - Centre).magnitude < footprint;

        private void Update()
        {
            if (player == null) return;
            bool below = Below(player.position);
            // Fully in the cave a few metres below the surface; easing out over the last stretch of the climb.
            float target = below ? Mathf.InverseLerp(surfaceY - 1f, surfaceY - 9f, player.position.y) : 0f;
            amount = Mathf.MoveTowards(amount, target, Time.deltaTime * (target > amount ? 1.2f : 0.7f));
            Apply(amount, false);

            // Fell into a pit once nothing else rescues: back onto the floor.
            if (below && arena != null && !arena.Active && playerBody != null && playerBody.enabled && player.position.y < arena.GroundY - 6f)
            {
                Vector3 safe = arena.NearestSolid(player.position) + Vector3.up * 0.2f;
                playerBody.enabled = false;
                player.position = safe;
                playerBody.enabled = true;
            }

            // Back in the open: up the debris, or respawned somewhere on the surface.
            if (IsOpen && !HasEmerged && (playerHealth == null || !playerHealth.IsDead) && (!below || player.position.y > surfaceY - 1.2f))
            {
                HasEmerged = true;
                Emerged?.Invoke();
            }
        }

        // Blends the surface's lighting toward the cave's. Leaves things alone while nothing changes, so the
        // arena's sandstorm can set its own fog meanwhile.
        private void Apply(float k, bool force)
        {
            if (!captured) return;
            if (!force && Mathf.Abs(k - shown) < 0.002f) return;
            shown = k;
            float s = k * k * (3f - 2f * k);
            if (sun != null) sun.intensity = Mathf.Lerp(sunIntensity, caveSun, s);
            RenderSettings.fog = fog || s > 0f;
            RenderSettings.fogMode = s > 0f ? FogMode.Linear : fogMode;
            RenderSettings.fogColor = Color.Lerp(fogColor, caveFog, s);
            RenderSettings.fogStartDistance = Mathf.Lerp(fogStart, caveFogStart, s);
            RenderSettings.fogEndDistance = Mathf.Lerp(fogEnd, caveFogEnd, s);
            RenderSettings.ambientMode = s > 0f ? AmbientMode.Trilight : ambientMode;
            RenderSettings.ambientSkyColor = Color.Lerp(sky, caveSky, s);
            RenderSettings.ambientEquatorColor = Color.Lerp(equator, caveEquator, s);
            RenderSettings.ambientGroundColor = Color.Lerp(ground, caveGround, s);
        }

        // ---------- The collapse ----------

        /// <summary>Brings the gash down into the cavern and opens the way out (once).</summary>
        public void BeginCollapse()
        {
            if (collapsing) return;
            collapsing = true;
            StartCoroutine(Collapse());
        }

        private IEnumerator Collapse()
        {
            float floor = arena != null ? arena.GroundY : Centre.y;
            if (arena != null) arena.ResetArena(); // the floor is whole again under the debris
            Vector3 top = hole != null ? hole.position : Centre + Vector3.up * 20f;
            Vector3 along = hole != null ? hole.forward : Vector3.forward;
            Vector3 across = Vector3.Cross(Vector3.up, along);
            MiniBossHud.Message("THE CEILING IS COMING DOWN! GET OUT OF THE RED!", new Color(1f, 0.45f, 0.3f), warningTime);

            // Warning: the floor where it will land flashes red, faster and faster; the roof rumbles and sheds grit
            // from between the choked rocks.
            Zone zone = crushZone != null ? Zone.Attach(crushZone) : null;
            for (float t = 0f; t < warningTime; t += Time.deltaTime)
            {
                float k = t / warningTime;
                if (zone != null) zone.Flash(k);
                CameraShake.Add(Time.deltaTime * (0.15f + k * 0.45f));
                if (UnityEngine.Random.value < Time.deltaTime * (6f + 22f * k))
                {
                    Vector3 from = top + along * UnityEngine.Random.Range(-0.45f, 0.45f) * holeSize.y + across * UnityEngine.Random.Range(-0.4f, 0.4f) * holeSize.x +
                                   Vector3.down * UnityEngine.Random.Range(5f, 8f);
                    ParticleBurst.Spawn(ParticleBurst.Kind.Sand, from, Vector3.down, 7, 0.5f, 3.5f, 12f);
                }
                yield return null;
            }

            // It gives way. The face breaks and the dunes above pour through, piling into the debris cone; the rocks come
            // down onto it exactly as the recording has them.
            Falling = true;
            CameraShake.Add(1f);
            if (ceilingCap != null) ceilingCap.SetActive(false);
            StartCoroutine(Drain());
            if (surfaceLid != null) StartCoroutine(DropLid(surfaceLid.transform, floor));
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, top + Vector3.down * 4f, Vector3.down, 80, 3f, 10f, 50f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, top + Vector3.down * 6f, Vector3.down, 40, 4f, 4f, 70f);
            Cascade(top, along);
            StartCoroutine(Grow());
            StartCoroutine(Billow(floor));
            // (Their colliders are off while they fall: nobody can be among them and live.)
            foreach (Transform rock in rocks)
                if (rock != null && rock.TryGetComponent(out Collider solid)) solid.enabled = false;

            float length = bake != null ? bake.Duration : 0f, danger = bake != null ? bake.DangerTime : 0f;
            int count = bake != null ? Mathf.Min(rocks.Length, bake.RockCount) : 0;
            int knock = 0, knocks = bake != null ? bake.ImpactCount : 0;
            for (float t = 0f; t < Mathf.Max(length, growTime); t += Time.deltaTime)
            {
                for (int i = 0; i < count; i++)
                {
                    if (rocks[i] == null || t < bake.StartTime(i)) continue;
                    bake.Sample(i, t, out Vector3 position, out Quaternion rotation);
                    rocks[i].SetPositionAndRotation(transform.TransformPoint(position), transform.rotation * rotation);
                }
                for (; knock < knocks && bake.ImpactTime(knock) <= t; knock++) Knock(transform.TransformPoint(bake.ImpactPoint(knock)), bake.ImpactSize(knock));
                if (zone != null)
                {
                    zone.Flash(1f);
                    if (t <= Mathf.Max(danger, growTime)) Crush(floor);
                    else
                    {
                        zone.gameObject.SetActive(false);
                        zone = null;
                    }
                }
                yield return null;
            }
            if (zone != null) zone.gameObject.SetActive(false);

            // At rest, exactly where the recording ends; solid again, to climb over and between.
            for (int i = 0; i < count; i++)
            {
                if (rocks[i] == null) continue;
                bake.Sample(i, length + 1f, out Vector3 position, out Quaternion rotation);
                rocks[i].SetPositionAndRotation(transform.TransformPoint(position), transform.rotation * rotation);
            }
            foreach (Transform rock in rocks)
                if (rock != null && rock.TryGetComponent(out Collider solid)) solid.enabled = true;
            Physics.SyncTransforms();
            Falling = false;
            Settled = true;

            BuildShaft(top, along, floor);
            IsOpen = true;
            MiniBossHud.Message("Daylight! Climb the rubble to get out.", new Color(1f, 0.95f, 0.7f), 4f);
            Collapsed?.Invoke();
        }

        // Anyone left where it lands is crushed.
        private void Crush(float floor)
        {
            if (player == null || playerHealth == null || playerHealth.IsDead) return;
            if (!InCrushZone(player.position) || player.position.y > floor + 6f) return;
            MiniBossHud.Message("CRUSHED BY THE FALLING ROCK!", new Color(1f, 0.4f, 0.3f), 3f);
            playerHealth.TakeDamage(playerHealth.Current + 1000f, gameObject);
        }

        // A recorded hard knock: a puff of dust where it hit, and a tremor.
        private static void Knock(Vector3 at, float size)
        {
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, at, Vector3.up, Mathf.RoundToInt(Mathf.Clamp(6f + size * 2.5f, 6f, 40f)), Mathf.Clamp(0.5f + size * 0.12f, 0.5f, 2.6f),
                Mathf.Clamp(2f + size * 0.25f, 2f, 7f), 70f);
            if (size > 9f) ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, at + Vector3.up * 0.5f, Vector3.up, 3, Mathf.Clamp(size * 0.18f, 1.5f, 4f), 1.2f, 80f,
                new Color(0.72f, 0.58f, 0.42f, 0.7f));
            CameraShake.Add(Mathf.Clamp(size * 0.006f, 0.01f, 0.16f));
        }

        // The debris cone builds the way a pile of poured sand does: a cone under the pour, its top rising and its foot
        // spreading at the sand's own slope until it fills the cone's finished shape. Every vertex of the cone's mesh
        // rides on the point of the surface its second UV names (a surface vertex names itself; a half-buried stone
        // names the ground under it).
        private IEnumerator Grow()
        {
            if (mound == null) yield break;
            Mesh final = mound.sharedMesh;
            mound.TryGetComponent(out Collider solid);
            if (solid != null) solid.enabled = false;
            mound.gameObject.SetActive(true);
            if (final == null) yield break;
            var anchors = new List<Vector3>();
            final.GetUVs(1, anchors);
            Vector3[] rest = final.vertices;
            if (anchors.Count != rest.Length)
            {
                if (solid != null) solid.enabled = true;
                yield break;
            }
            // (The cone's object sits at the lair's origin, unturned, so its vertices are already relative to the lair.)
            var run = new float[rest.Length];
            float peak = 0f;
            for (int i = 0; i < rest.Length; i++)
            {
                run[i] = new Vector2(anchors[i].x - pourPoint.x, anchors[i].z - pourPoint.z).magnitude * reposeTan;
                peak = Mathf.Max(peak, anchors[i].y + run[i]);
            }
            Mesh working = Instantiate(final);
            working.MarkDynamic();
            mound.sharedMesh = working;
            var moved = new Vector3[rest.Length];
            const float hidden = -0.4f; // just under the floor
            for (float t = 0f; t < growTime; t += Time.deltaTime)
            {
                float k = t / growTime;
                float apex = Mathf.Lerp(-1f, peak + 0.1f, 1f - (1f - k) * (1f - k)); // fast at first, easing as it fills
                for (int i = 0; i < rest.Length; i++)
                {
                    float grown = Mathf.Max(hidden, Mathf.Min(anchors[i].y, apex - run[i]));
                    Vector3 v = rest[i];
                    v.y -= Mathf.Max(0f, anchors[i].y - grown);
                    moved[i] = v;
                }
                working.vertices = moved;
                working.RecalculateNormals();
                working.RecalculateBounds();
                yield return null;
            }
            mound.sharedMesh = final;
            Destroy(working);
            if (solid == null) yield break;
            solid.enabled = true;
            // Anyone standing in the debris's shallow skirt (outside the red zone) ends up on top of it, not under it.
            if (player != null && playerBody != null && (playerHealth == null || !playerHealth.IsDead) &&
                solid.Raycast(new Ray(player.position + Vector3.up * 40f, Vector3.down), out RaycastHit top, 80f) && top.point.y > player.position.y)
            {
                playerBody.enabled = false;
                player.position = top.point + Vector3.up * 0.1f;
                playerBody.enabled = true;
            }
        }

        // The sand packed round the choked rocks crumbles away: clump by clump it breaks loose (each patch just as the
        // rocks in it come loose, the clumps in a patch a little apart), drops and crumbles to nothing as it falls.
        private IEnumerator Drain()
        {
            if (chokeSand == null) yield break;
            Mesh final = chokeSand.sharedMesh;
            var middles = new List<Vector3>();
            var timing = new List<Vector2>();
            if (final != null)
            {
                final.GetUVs(2, middles);
                final.GetUVs(3, timing);
            }
            Vector3[] rest = final != null ? final.vertices : Array.Empty<Vector3>();
            if (final == null || middles.Count != rest.Length || timing.Count != rest.Length)
            {
                chokeSand.gameObject.SetActive(false);
                yield break;
            }
            float end = 0f;
            foreach (Vector2 when in timing) end = Mathf.Max(end, when.x + when.y);
            Mesh working = Instantiate(final);
            working.MarkDynamic();
            chokeSand.sharedMesh = working;
            var moved = new Vector3[rest.Length];
            for (float t = 0f; t < end; t += Time.deltaTime)
            {
                for (int i = 0; i < rest.Length; i++)
                {
                    float since = Mathf.Max(0f, t - timing[i].x), k = Mathf.Clamp01(since / timing[i].y);
                    moved[i] = Vector3.Lerp(rest[i], middles[i], k * k) + Vector3.down * (4.9f * since * since);
                }
                working.vertices = moved;
                working.RecalculateBounds();
                yield return null;
            }
            chokeSand.gameObject.SetActive(false);
            chokeSand.sharedMesh = final;
            Destroy(working);
        }

        // Dust boiling up off the growing cone.
        private IEnumerator Billow(float floor)
        {
            Vector3 foot = transform.TransformPoint(pourPoint);
            foot.y = floor;
            for (float t = 0f; t < growTime + 2.5f; t += 0.1f)
            {
                float reach = Mathf.Lerp(3f, 16f, Mathf.Clamp01(t / growTime));
                Vector3 at = foot + Flat(UnityEngine.Random.insideUnitSphere) * reach + Vector3.up * UnityEngine.Random.Range(0.5f, 2f + reach * 0.5f);
                ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, at, Vector3.up, 4, 3.5f, 1.5f, 80f, new Color(0.75f, 0.6f, 0.42f, 0.8f));
                if (UnityEngine.Random.value < 0.5f) ParticleBurst.Spawn(ParticleBurst.Kind.Sand, at, Vector3.up, 8, 1.2f, 4f, 70f);
                CameraShake.Add(0.03f);
                yield return new WaitForSeconds(0.1f);
            }
        }

        // The dunes pouring through the gash: a thick curtain of sand along its length, thinning out as the cone fills.
        private void Cascade(Vector3 top, Vector3 along)
        {
            var go = new GameObject("Sand Cascade");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(top + Vector3.down * 0.5f, Quaternion.LookRotation(Vector3.down, along));
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = false;
            main.duration = growTime + 3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 3.8f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.88f, 0.72f, 0.5f, 1f), new Color(0.74f, 0.58f, 0.4f, 0.9f));
            main.gravityModifier = 1.3f;
            main.maxParticles = 5000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = system.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1700f), new Keyframe(0.45f, 1300f), new Keyframe(1f, 0f)));
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(holeSize.x * 0.8f, holeSize.y * 0.85f, 0.5f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = EffectMaterials.AlphaBlended(EffectMaterials.Look.Smoke);
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.18f;
            renderer.lengthScale = 2f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            system.Play();
            Destroy(go, growTime + 7f);
        }

        private static IEnumerator DropLid(Transform lid, float floor)
        {
            foreach (Collider collider in lid.GetComponentsInChildren<Collider>()) collider.enabled = false;
            Vector3 start = lid.position;
            for (float t = 0f; t < 1.4f && lid.position.y > floor - 20f; t += Time.deltaTime)
            {
                lid.position = start + Vector3.down * (t * t * 12f);
                yield return null;
            }
            lid.gameObject.SetActive(false);
        }

        // Sunlight through the open gash, down onto the debris, with drifting dust.
        private void BuildShaft(Vector3 top, Vector3 along, float floor)
        {
            var root = new GameObject("Sunlight Shaft");
            root.transform.SetParent(transform, false);
            root.transform.position = top;

            var spot = new GameObject("Sunbeam").AddComponent<Light>();
            spot.transform.SetParent(root.transform, false);
            spot.transform.position = top + Vector3.up * 10f;
            spot.transform.rotation = Quaternion.LookRotation(Flat(Centre - top).normalized * 0.35f + Vector3.down);
            spot.type = LightType.Spot;
            spot.spotAngle = 85f;
            spot.range = top.y - floor + 30f;
            spot.intensity = 160f;
            spot.color = new Color(1f, 0.9f, 0.72f);
            spot.shadows = LightShadows.None;

            foreach ((float offset, float width, float alpha) in new[] { (-0.25f, 9f, 0.07f), (0.05f, 11f, 0.08f), (0.3f, 8f, 0.06f) })
            {
                var line = new GameObject("Light Column").AddComponent<LineRenderer>();
                line.transform.SetParent(root.transform, false);
                line.sharedMaterial = EffectMaterials.Additive(EffectMaterials.Look.Glow);
                line.useWorldSpace = true;
                line.positionCount = 2;
                Vector3 from = top + along * offset * holeSize.y;
                line.SetPosition(0, from + Vector3.up * 2f);
                line.SetPosition(1, new Vector3(from.x, floor + 3f, from.z) + Flat(Centre - top).normalized * 5f);
                line.startWidth = width * 0.8f;
                line.endWidth = width;
                line.startColor = new Color(1f, 0.92f, 0.7f, alpha);
                line.endColor = new Color(1f, 0.85f, 0.6f, alpha * 0.3f);
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
            }

            var motes = new GameObject("Dust Motes").AddComponent<ParticleSystem>();
            motes.transform.SetParent(root.transform, false);
            motes.transform.SetPositionAndRotation(new Vector3(top.x, (top.y + floor) * 0.5f, top.z), Quaternion.LookRotation(along));
            motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = motes.main;
            main.loop = true;
            main.startLifetime = 7f;
            main.startSpeed = 0.15f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
            main.startColor = new Color(1f, 0.92f, 0.75f, 0.7f);
            main.maxParticles = 500;
            var emission = motes.emission;
            emission.rateOverTime = 60f;
            var shape = motes.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(holeSize.x, top.y - floor, holeSize.y);
            var noise = motes.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 0.3f;
            var renderer = motes.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = EffectMaterials.Additive(EffectMaterials.Look.Glow);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            motes.Play();
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        /// <summary>The red crush zone on the floor, flashing faster as the fall nears.</summary>
        private sealed class Zone : MonoBehaviour
        {
            private Mesh mesh;
            private Color[] baseColors, colors;
            private Light glow;
            private float phase;

            public static Zone Attach(MeshFilter filter)
            {
                filter.gameObject.SetActive(true);
                var zone = filter.gameObject.AddComponent<Zone>();
                zone.mesh = Instantiate(filter.sharedMesh);
                filter.sharedMesh = zone.mesh;
                zone.baseColors = zone.mesh.colors;
                if (zone.baseColors == null || zone.baseColors.Length != zone.mesh.vertexCount)
                {
                    zone.baseColors = new Color[zone.mesh.vertexCount];
                    for (int i = 0; i < zone.baseColors.Length; i++) zone.baseColors[i] = Color.white;
                }
                zone.colors = new Color[zone.baseColors.Length];
                if (!filter.TryGetComponent(out MeshRenderer renderer)) renderer = filter.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = EffectMaterials.Additive(EffectMaterials.Look.Glow);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                zone.glow = new GameObject("Red Light").AddComponent<Light>();
                zone.glow.transform.SetParent(filter.transform, false);
                zone.glow.transform.position = filter.transform.TransformPoint(zone.mesh.bounds.center) + Vector3.up * 4f;
                zone.glow.type = LightType.Point;
                zone.glow.color = new Color(1f, 0.15f, 0.08f);
                zone.glow.range = Mathf.Max(zone.mesh.bounds.extents.x, zone.mesh.bounds.extents.z) * 1.6f;
                zone.glow.shadows = LightShadows.None;
                zone.Flash(0f);
                return zone;
            }

            /// <summary>Flashes on and off, from slow (0) to frantic (1). The edge (bright in the base colours) stays brightest.</summary>
            public void Flash(float urgency)
            {
                phase += Time.deltaTime * Mathf.Lerp(2.5f, 9f, urgency) * Mathf.PI * 2f;
                float on = 0.5f + 0.5f * Mathf.Sin(phase);
                on *= on;
                for (int i = 0; i < colors.Length; i++)
                {
                    float edge = baseColors[i].a;
                    colors[i] = new Color(1f, 0.08f + 0.25f * edge * on, 0.05f, (0.25f + 0.75f * edge) * (0.35f + 0.65f * on));
                }
                mesh.colors = colors;
                glow.intensity = Mathf.Lerp(5f, 60f, on);
            }

            private void OnDestroy()
            {
                if (mesh != null) Destroy(mesh);
            }
        }
    }
}
