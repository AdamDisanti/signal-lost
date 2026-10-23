using System;
using System.Collections;
using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Player;
using UnityEngine;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// The desert stage boss: a 29 m sandworm that swallowed the ship's Engine Core. One continuous, dark desert-coloured
    /// body (<see cref="CorewormBody"/>) bends along its path, ringed like an earthworm, with veins of the core's energy
    /// pulsing over it. Its petal mouth opens on the core in its throat; glowing energy blisters along its sides are its
    /// weak spots. The head can only turn so far from its neck, so it never twists through its own body. Most of the time it swims
    /// under the sand (a racing ridge of sand gives it away) and erupts to attack.
    ///
    /// Four phases, each also changing the arena (<see cref="CorewormArena"/>):
    /// 1. The Hunt (100-75%): burrow charges, breach arcs that rain debris, core-slag spit, body slams.
    /// 2. Sandstorm (75-50%): fog, wind and cyclones; ambushes from several ridges at once (only one is real), with fodder.
    /// 3. Collapse (50-25%): the floor falls away; a sweeping core beam, shrapnel barrages, often both at once.
    /// 4. Meltdown (25-0%): lanes of fire; combos with almost no gaps, and the core nova, a huge blast you must hide
    ///    from behind cover, after which it lies stunned with its core open.
    ///
    /// Damage: its hide and plating shrug off most of a hit; the vents along its sides (only when they are above the
    /// sand) take full damage, and the core in its throat takes extra damage while the mouth is open. Each hit box is a
    /// part with its own Health that forwards hits here (one hit per swing). All numbers are placeholders.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class Coreworm : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private enum HitKind { Armor, Core, Vent }
        private enum Attack { Charge, Breach, Spit, Slam, Ambush, Beam, Barrage, BeamAndBarrage, ChargeThenSlam, SpitAndSpiral, Nova }

        private static readonly string[] PhaseNames = { "The Hunt", "Sandstorm", "Collapse", "Meltdown" };

        [Header("Identity")]
        [SerializeField] private string displayName = "CORE WORM";
        [SerializeField] private string partName = "Engine Core";
        [SerializeField, Range(0.05f, 0.95f)] private float[] phaseThresholds = { 0.75f, 0.5f, 0.25f };

        [Header("Damage taken")]
        [SerializeField, Range(0f, 1f)] private float armorMultiplier = 0.15f;
        [SerializeField, Min(0f)] private float ventMultiplier = 1f;
        [SerializeField, Min(0f)] private float closedCoreMultiplier = 0.4f;
        [SerializeField, Min(0f)] private float openCoreMultiplier = 1.6f;

        [Header("Body")]
        [SerializeField] private Transform head;
        [Tooltip("The continuous body mesh, rebuilt along the path every frame.")]
        [SerializeField] private CorewormBody body;
        [Tooltip("Tips of the exhaust stacks and the tail nozzle: they smoke, and belch fire as the core overloads.")]
        [SerializeField] private Transform[] exhaustStacks = new Transform[0];
        [Tooltip("Anchors along the body from the neck to the tail (hit boxes and ship parts); they follow the path the head took.")]
        [SerializeField] private Transform[] segments = new Transform[0];
        [SerializeField, Min(0.5f)] private float segmentSpacing = 1.7f;
        [Tooltip("Distance from the head pivot to the first segment.")]
        [SerializeField, Min(0f)] private float neckGap = 1.9f;
        [Tooltip("Most the head can turn away from the line of its neck, in degrees (stops it twisting through its body).")]
        [SerializeField, Range(10f, 90f)] private float maxNeckBend = 55f;
        [Tooltip("How deep it swims under the sand (keep its back below the floor).")]
        [SerializeField, Min(1f)] private float swimDepth = 3.6f;
        [Tooltip("How quickly the part of its body still above the sand sinks after its head has gone under (higher: faster).")]
        [SerializeField, Min(0f)] private float trailSinkRate = 3f;
        [Tooltip("The three jaws around the mouth, which open outward.")]
        [SerializeField] private Transform[] jaws = new Transform[0];
        [Tooltip("The Engine Core in its throat: the weak spot, and the part it drops.")]
        [SerializeField] private Transform core;
        [SerializeField] private Renderer coreRenderer;
        [SerializeField] private Light coreLight;
        [SerializeField] private Renderer[] ventRenderers = new Renderer[0];

        [Header("Hit boxes")]
        [SerializeField] private Health[] armorHitboxes = new Health[0];
        [SerializeField] private Health coreHitbox;
        [SerializeField] private Health[] ventHitboxes = new Health[0];

        [Header("Attacks")]
        [SerializeField, Min(0f)] private float eruptDamage = 28f;
        [SerializeField, Min(0f)] private float breachLandDamage = 24f;
        [SerializeField, Min(0f)] private float bodyContactDamage = 22f;
        [SerializeField, Min(0f)] private float slamDamage = 28f;
        [SerializeField, Min(0f)] private float slagDamage = 14f;
        [SerializeField, Min(0f)] private float geyserDamage = 10f;
        [SerializeField, Min(0f)] private float beamDamagePerTick = 9f;
        [SerializeField, Min(0f)] private float shrapnelDamage = 9f;
        [SerializeField, Min(0f)] private float novaDamage = 55f;
        [SerializeField, Min(5f)] private float novaRadius = 50f;
        [SerializeField] private FireStyle fire = new FireStyle { damage = 10f, interval = 0.5f, duration = 4f, smoke = 0.6f, tint = new Color(1f, 0.92f, 0.8f) };
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("References")]
        [SerializeField] private CorewormArena arena;
        [SerializeField] private Material warningMaterial;
        [SerializeField] private Material slagMaterial;
        [SerializeField] private Material shrapnelMaterial;
        [SerializeField] private Material rockMaterial;
        [SerializeField] private Material blastMaterial;
        [SerializeField] private Material dustMaterial;

        /// <summary>Raised once when it is defeated (before the Engine Core drops).</summary>
        public event Action Defeated;

        public string DisplayName => displayName;
        public int Phase { get; private set; } = 1;
        public string PhaseName => PhaseNames[Mathf.Clamp(Phase - 1, 0, 3)];
        public IReadOnlyList<float> PhaseThresholds => phaseThresholds;
        public float HealthFraction => health != null ? health.Normalized : 1f;
        public bool Invulnerable { get; private set; } = true;
        public bool IsDefeated { get; private set; }
        public bool Fighting { get; private set; }
        public Transform Head => head;

        private Health health;
        private Transform player;
        private CharacterController playerBody;
        private Health playerHealth;
        private Transform playerView;
        private readonly List<Vector3> trail = new List<Vector3>();
        private Vector3 heading = Vector3.forward; // flat direction it is actually travelling
        private float currentSpeed;                 // flat speed it is actually travelling at
        private bool movedThisFrame;
        private const float SwimAcceleration = 45f;
        private float mouth, mouthTarget, glow = 0.3f, glowTarget = 0.3f;
        private Quaternion[] jawRest = new Quaternion[0];
        private Color coreColor = new Color(1f, 0.75f, 0.35f), ventColor = new Color(1f, 0.5f, 0.15f);
        private MaterialPropertyBlock block;
        private Coroutine fight;
        private readonly List<Coroutine> side = new List<Coroutine>();
        private readonly List<GameObject> temporary = new List<GameObject>();
        private Attack lastAttack = Attack.Nova;
        private int attacksThisPhase;
        private float pendingDamage;
        private GameObject pendingSource;
        private Vector3 shoveDirection;
        private float shoveRemaining;
        private LineRenderer beam, beamHalo, beamCore;
        private Light beamMouthLight, beamImpactLight;
        private const int BeamPoints = 12;
        private FireEffect mouthFlame;
        private bool contactHit;
        private readonly List<Vector3> path = new List<Vector3>();
        private float nextExhaust;

        private float GroundY => arena != null ? arena.GroundY : 0f;
        private float UnderY => GroundY - swimDepth;
        private Vector3 Mouth => head.TransformPoint(new Vector3(0f, 0f, 2.1f));
        private float Fierce => 1f + 0.18f * (Phase - 1); // faster, denser each phase

        // ---------- Setup ----------

        private void Awake()
        {
            health = GetComponent<Health>();
            block = new MaterialPropertyBlock();
            jawRest = new Quaternion[jaws.Length];
            for (int i = 0; i < jaws.Length; i++) jawRest[i] = jaws[i] != null ? jaws[i].localRotation : Quaternion.identity;
            if (coreRenderer != null && coreRenderer.sharedMaterial != null && coreRenderer.sharedMaterial.HasProperty(BaseColor))
                coreColor = coreRenderer.sharedMaterial.GetColor(BaseColor);
            if (ventRenderers.Length > 0 && ventRenderers[0] != null && ventRenderers[0].sharedMaterial != null &&
                ventRenderers[0].sharedMaterial.HasProperty(BaseColor))
                ventColor = ventRenderers[0].sharedMaterial.GetColor(BaseColor);

            foreach (Health box in armorHitboxes) Relay(box, HitKind.Armor);
            Relay(coreHitbox, HitKind.Core);
            foreach (Health box in ventHitboxes) Relay(box, HitKind.Vent);
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            BuildBeam();
            // Its hit boxes move every frame: on a kinematic body that is cheap; without one Unity treats them as
            // static scenery and rebuilds its physics data for every move, which stutters.
            if (GetComponent<Rigidbody>() == null)
            {
                var rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
            }
        }

        private void Start()
        {
            var controller = FindAnyObjectByType<FirstPersonController>();
            if (controller != null)
            {
                player = controller.transform;
                playerBody = controller.GetComponent<CharacterController>();
                playerHealth = controller.GetComponent<Health>();
                Camera view = controller.GetComponentInChildren<Camera>();
                playerView = view != null ? view.transform : player;
            }
            if (head != null && mouthFlame == null)
            {
                mouthFlame = FireEffect.SpawnJet(head, new Vector3(0f, 0f, 2.2f), Quaternion.identity, 0.45f, 3.5f);
                mouthFlame.Strength = 0f;
            }
        }

        // The core beam in three layers (a wide glow, the orange beam, a white-hot core), with lights at both ends.
        private void BuildBeam()
        {
            beamHalo = BeamLayer("Core Beam Glow", new Color(1f, 0.45f, 0.1f, 0.35f), new Color(1f, 0.3f, 0.05f, 0.2f), 2);
            beam = BeamLayer("Core Beam", new Color(1f, 0.75f, 0.3f, 1f), new Color(1f, 0.45f, 0.12f, 0.95f), 3);
            beamCore = BeamLayer("Core Beam Heart", new Color(1f, 1f, 0.95f, 1f), new Color(1f, 0.92f, 0.75f, 1f), 4);
            beamMouthLight = BeamLight("Beam Light (mouth)");
            beamImpactLight = BeamLight("Beam Light (impact)");
        }

        private LineRenderer BeamLayer(string name, Color start, Color end, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = EffectMaterials.Additive(EffectMaterials.Look.Beam);
            line.positionCount = BeamPoints;
            line.useWorldSpace = true;
            line.numCapVertices = 6;
            line.textureMode = LineTextureMode.Stretch;
            line.startColor = start;
            line.endColor = end;
            line.sortingOrder = order;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        private Light BeamLight(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.25f);
            light.range = 16f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            return light;
        }

        private void ShowBeam(bool on)
        {
            foreach (LineRenderer line in new[] { beamHalo, beam, beamCore }) if (line != null) line.enabled = on;
            foreach (Light light in new[] { beamMouthLight, beamImpactLight }) if (light != null) light.enabled = on;
            if (!on && mouthFlame != null) mouthFlame.transform.localScale = Vector3.one;
        }

        /// <summary>Lays a beam layer from the mouth to its end, crackling sideways (more in the middle) by up to <paramref name="crackle"/> m.</summary>
        private void DrawBeam(LineRenderer line, Vector3 from, Vector3 to, float width, float crackle)
        {
            if (line == null) return;
            Vector3 along = to - from;
            Vector3 side = Vector3.Cross(along, Vector3.up);
            if (side.sqrMagnitude < 0.0001f) side = Vector3.right;
            side.Normalize();
            Vector3 lift = Vector3.Cross(side, along).normalized;
            for (int i = 0; i < BeamPoints; i++)
            {
                float k = i / (float)(BeamPoints - 1);
                float envelope = Mathf.Sin(k * Mathf.PI);
                float n = Time.time * 30f + i * 1.7f;
                Vector3 jitter = (side * (Mathf.PerlinNoise(n, 0.3f) - 0.5f) + lift * (Mathf.PerlinNoise(0.7f, n) - 0.5f)) * 2f * crackle * envelope;
                line.SetPosition(i, from + along * k + jitter);
            }
            float flicker = 1f + 0.18f * Mathf.Sin(Time.time * 47f) + 0.12f * Mathf.Sin(Time.time * 73f);
            line.startWidth = width * flicker;
            line.endWidth = width * flicker * 1.15f; // flares where it hits
        }

        private void Relay(Health box, HitKind kind)
        {
            if (box == null) return;
            box.Damaged += (h, amount, source) =>
            {
                h.Restore();
                if (Invulnerable || IsDefeated) return;
                float effective = amount * Multiplier(kind, h);
                if (effective > pendingDamage)
                {
                    pendingDamage = effective;
                    pendingSource = source;
                }
                if (kind != HitKind.Armor && effective > 0f) ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, h.transform.position, Vector3.up, 8, 0.1f, 5f, 70f);
            };
        }

        private float Multiplier(HitKind kind, Health box)
        {
            switch (kind)
            {
                case HitKind.Core: return mouth > 0.5f ? openCoreMultiplier : closedCoreMultiplier;
                case HitKind.Vent: return box.transform.position.y > GroundY + 0.2f ? ventMultiplier : armorMultiplier;
                default: return armorMultiplier;
            }
        }

        // ---------- Public control (used by the intro/stage exit) ----------

        /// <summary>Hides it under the sand beneath a point, body hanging straight down, idle.</summary>
        public void HideAt(Vector3 ground)
        {
            ground.y = GroundY;
            head.position = ground + Vector3.down * (swimDepth + 2f);
            head.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            trail.Clear();
            for (int i = 0; i < 160; i++) trail.Add(head.position + Vector3.down * (i * 0.25f));
            PlaceBody();
        }

        /// <summary>For the intro: bursts out of the sand at a point, rearing up.</summary>
        public IEnumerator IntroErupt(Vector3 ground, float height, Vector3 lookAt)
        {
            ground.y = GroundY;
            head.position = ground + Vector3.down * (swimDepth + 1f);
            yield return Rise(ground, height, 0.8f, false);
            mouthTarget = 1f;
            glowTarget = 1f;
            float t = 0f;
            while (t < 1.2f)
            {
                t += Time.deltaTime;
                Face(lookAt - head.position, 6f);
                head.position += Vector3.up * Mathf.Sin(Time.time * 3f) * 0.01f;
                yield return null;
            }
        }

        /// <summary>For the intro: lunges its open mouth at a point (the player's eyes).</summary>
        public IEnumerator IntroLunge(Vector3 at, float time)
        {
            Vector3 from = head.position;
            mouthTarget = 1f;
            mouth = 1f;
            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / time);
                head.position = Vector3.Lerp(from, at, k);
                Face(at - from, 20f);
                yield return null;
            }
        }

        /// <summary>For the intro: a roar (open jaws, glowing core, a burst of sand and a shake).</summary>
        public IEnumerator Roar(float time)
        {
            mouthTarget = 1f;
            glowTarget = 1.3f;
            CameraShake.Add(0.6f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, head.position, Vector3.up, 30, 2f, 6f, 80f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, Mouth, head.forward, 24, 0.12f, 9f, 40f);
            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                CameraShake.Add(Time.deltaTime * 0.5f);
                yield return null;
            }
            mouthTarget = 0f;
            glowTarget = 0.4f;
        }

        public IEnumerator DiveAway(Vector3 ground)
        {
            yield return DiveInto(ground, 0.8f);
        }

        /// <summary>Starts the fight at phase 1 (it should be under the arena).</summary>
        public void BeginFight()
        {
            IsDefeated = false;
            Fighting = true;
            Phase = 1;
            attacksThisPhase = 0;
            Invulnerable = false;
            health.Restore();
            CorewormHud.ShowBoss(this);
            if (arena != null) arena.Active = true;
            fight = StartCoroutine(Fight());
        }

        /// <summary>Stops everything and puts it back to full health, hidden under a point (the player died).</summary>
        public void ResetFight(Vector3 hideAt)
        {
            StopFight();
            Fighting = false;
            Invulnerable = true;
            Phase = 1;
            IsDefeated = false;
            health.Restore();
            mouthTarget = mouth = 0f;
            glowTarget = 0.3f;
            CorewormHud.ShowBoss(null);
            HideAt(hideAt);
        }

        // ---------- Fight flow ----------

        private void StopFight()
        {
            if (fight != null) StopCoroutine(fight);
            fight = null;
            foreach (Coroutine item in side) if (item != null) StopCoroutine(item);
            side.Clear();
            foreach (GameObject item in temporary) if (item != null) Destroy(item);
            temporary.Clear();
            ShowBeam(false);
            if (mouthFlame != null) mouthFlame.Strength = 0f;
            mouthTarget = 0f;
            shoveRemaining = 0f;
        }

        private void RunAlongside(IEnumerator routine) => side.Add(StartCoroutine(routine));

        private IEnumerator Fight()
        {
            yield return Wait(0.6f);
            while (true)
            {
                yield return NextAttack();
                attacksThisPhase++;
                yield return Circle(Mathf.Max(0.4f, 1.6f - 0.35f * (Phase - 1)));
            }
        }

        private IEnumerator NextAttack()
        {
            Attack[] pool;
            switch (Phase)
            {
                case 1: pool = new[] { Attack.Charge, Attack.Breach, Attack.Spit, Attack.Slam }; break;
                case 2: pool = new[] { Attack.Ambush, Attack.Ambush, Attack.Charge, Attack.Spit, Attack.Breach }; break;
                case 3: pool = new[] { Attack.Beam, Attack.Barrage, Attack.BeamAndBarrage, Attack.Breach, Attack.Charge }; break;
                default:
                    pool = attacksThisPhase % 4 == 3
                        ? new[] { Attack.Nova }
                        : new[] { Attack.BeamAndBarrage, Attack.ChargeThenSlam, Attack.SpitAndSpiral, Attack.Breach, Attack.Barrage };
                    break;
            }
            Attack pick = pool[UnityEngine.Random.Range(0, pool.Length)];
            if (pick == lastAttack && pool.Length > 1) pick = pool[(Array.IndexOf(pool, pick) + 1) % pool.Length];
            lastAttack = pick;
            switch (pick)
            {
                case Attack.Charge: yield return BurrowCharge(); break;
                case Attack.Breach: yield return BreachArc(); break;
                case Attack.Spit: yield return CoreSpit(Phase == 1 ? 4 : 6); break;
                case Attack.Slam: yield return BodySlam(); break;
                case Attack.Ambush: yield return Ambush(); break;
                case Attack.Beam: yield return CoreBeam(); break;
                case Attack.Barrage: yield return Barrage(); break;
                case Attack.BeamAndBarrage: yield return BeamAndBarrage(); break;
                case Attack.ChargeThenSlam:
                    yield return BurrowCharge();
                    yield return BodySlam();
                    break;
                case Attack.SpitAndSpiral: yield return SpitAndSpiral(); break;
                case Attack.Nova: yield return CoreNova(); break;
            }
        }

        private void CheckPhase()
        {
            if (IsDefeated || Invulnerable || Phase >= 4) return;
            if (health.Normalized > phaseThresholds[Mathf.Clamp(Phase - 1, 0, phaseThresholds.Length - 1)]) return;
            StopFight();
            fight = StartCoroutine(PhaseChange(Phase + 1));
        }

        private IEnumerator PhaseChange(int next)
        {
            Invulnerable = true;
            yield return GoUnder();
            Phase = next;
            attacksThisPhase = 0;
            Vector3 middle = ArenaCentre;
            yield return SwimTo(() => middle, 22f, 3f, 1.5f, false);
            yield return Rise(middle, 15f, 0.7f, true);
            MiniBossHud.Message($"PHASE {next}: {PhaseName.ToUpperInvariant()}", new Color(1f, 0.6f, 0.25f), 3f);
            yield return Roar(1.6f);
            if (arena != null) arena.BeginPhase(next);
            yield return Wait(0.4f);
            yield return DiveInto(middle + Flat(head.forward) * 6f, 0.8f);
            Invulnerable = false;
            fight = StartCoroutine(Fight());
        }

        // ---------- Attacks ----------

        /// <summary>Races at the player under the sand, then erupts under them (a ring warns a moment before).</summary>
        private IEnumerator BurrowCharge()
        {
            yield return GoUnder();
            float speed = 15f * Fierce;
            yield return SwimTo(() => PlayerGround(0.5f), speed, 3f, 2.5f, true);
            Vector3 spot = PlayerGround(0f);
            GroundMarker marker = Marker(spot, 3.5f);
            float warn = 0.6f / Fierce;
            yield return Countdown(marker, warn);
            Destroy(marker.gameObject);
            yield return Rise(spot, 9f, 0.35f, true, eruptDamage);
            mouthTarget = 1f;
            yield return Hold(1.6f / Mathf.Sqrt(Fierce), true);
            mouthTarget = 0f;
            yield return DiveInto(spot + Flat(head.forward) * 7f, 0.7f);
        }

        /// <summary>Leaps out of the sand in a huge arc over the player and dives back in; debris rains down.</summary>
        private IEnumerator BreachArc()
        {
            yield return GoUnder();
            Vector3 across = Flat(UnityEngine.Random.insideUnitSphere);
            if (across.sqrMagnitude < 0.01f) across = Vector3.right;
            across.Normalize();
            Vector3 start = ClampToArena(PlayerGround(0f) - across * 14f);
            Vector3 end = ClampToArena(PlayerGround(0.6f) + across * 14f);
            yield return SwimTo(() => start, 24f, 2.5f, 2f, true);
            GroundMarker startMarker = Marker(start, 3f);
            GroundMarker endMarker = Marker(end, 5f);
            yield return Countdown(startMarker, 0.6f / Fierce);
            Destroy(startMarker.gameObject);

            Burst(start, 2.2f);
            AreaHit(start, 3f, eruptDamage * 0.8f, 2f);
            contactHit = false;
            float time = 1.7f / Mathf.Sqrt(Fierce);
            Vector3 from = head.position, to = new Vector3(end.x, UnderY, end.z);
            for (int i = 0; i < 4 + Phase * 2; i++)
            {
                // Debris thrown up by the breach comes down around the player.
                Vector3 drop = ClampToArena(PlayerGround(0f) + Flat(UnityEngine.Random.insideUnitSphere) * 9f);
                ScrapBomb.Drop(rockMaterial, warningMaterial, blastMaterial, drop + Vector3.up * 16f, drop, 1.6f, 12f, gameObject, transform, hitMask);
            }
            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                float k = t / time;
                Vector3 p = Vector3.Lerp(from, to, k);
                p.y = Mathf.Lerp(from.y, UnderY, k) + Mathf.Sin(k * Mathf.PI) * (14f + swimDepth);
                MoveHead(p);
                BodyContact(2.6f, bodyContactDamage);
                if (endMarker != null) endMarker.Progress = k;
                yield return null;
            }
            if (endMarker != null) Destroy(endMarker.gameObject);
            Burst(end, 2.6f);
            AreaHit(end, 5f, breachLandDamage, 3f);
            CameraShake.Add(0.5f);
            yield return SwimTo(() => end + across * 6f, 14f, 0.8f, 1f, false);
        }

        /// <summary>Rears up at range, opens its mouth and lobs a volley of burning core slag at and around the player.</summary>
        private IEnumerator CoreSpit(int count)
        {
            yield return GoUnder();
            Vector3 spot = ClampToArena(PlayerGround(0f) + Flat(UnityEngine.Random.insideUnitSphere).normalized * 14f);
            yield return SwimTo(() => spot, 22f, 2.5f, 2f, true);
            yield return Rise(spot, 9f, 0.5f, true, eruptDamage * 0.6f);
            mouthTarget = 1f;
            glowTarget = 1.2f;
            if (mouthFlame != null) mouthFlame.Strength = 0.35f;
            yield return Hold(0.7f / Fierce, true);
            for (int i = 0; i < count; i++)
            {
                Vector3 target = i == 0 ? PlayerGround(0.7f) : PlayerGround(0.3f) + Flat(UnityEngine.Random.insideUnitSphere) * 5f;
                target = ClampToArena(target);
                CoreSlag.Launch(slagMaterial, warningMaterial, blastMaterial, fire, Mouth, target, 1f + Vector3.Distance(Mouth, target) * 0.025f,
                    2.2f, slagDamage, gameObject, hitMask);
                ParticleBurst.Spawn(ParticleBurst.Kind.Fireball, Mouth, head.forward, 6, 0.6f, 4f, 25f);
                yield return Hold(0.22f, true);
            }
            if (mouthFlame != null) mouthFlame.Strength = 0f;
            glowTarget = 0.5f;
            yield return Hold(1.2f / Mathf.Sqrt(Fierce), true); // the opening: mouth still open
            mouthTarget = 0f;
            yield return DiveInto(spot + Flat(head.forward) * 6f, 0.7f);
        }

        /// <summary>Surfaces beside the player, rears up and slams its body down in a line across them.</summary>
        private IEnumerator BodySlam()
        {
            yield return GoUnder();
            Vector3 start = ClampToArena(PlayerGround(0f) + Flat(UnityEngine.Random.insideUnitSphere).normalized * 7f);
            yield return SwimTo(() => start, 22f, 2.5f, 2f, true);
            Vector3 direction = Flat(PlayerGround(0f) - start).normalized;
            if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
            Vector3 end = start + direction * 18f;
            var markers = new List<GroundMarker>();
            for (float d = 2f; d <= 18f; d += 2.5f) markers.Add(Marker(start + direction * d, 1.6f));
            yield return Rise(start, 10f, 0.5f, true, eruptDamage * 0.6f);
            float warn = 0.8f / Fierce, t = 0f;
            while (t < warn)
            {
                t += Time.deltaTime;
                foreach (GroundMarker marker in markers) if (marker != null) marker.Progress = t / warn;
                Face(direction + Vector3.up * 0.6f, 8f);
                yield return null;
            }
            // Down it comes, the body following the head along the line.
            Vector3 from = head.position, to = new Vector3(end.x, GroundY + 0.8f, end.z);
            t = 0f;
            const float slamTime = 0.45f;
            while (t < slamTime)
            {
                t += Time.deltaTime;
                float k = t / slamTime;
                Vector3 p = Vector3.Lerp(from, to, k);
                p.y = Mathf.Lerp(from.y, to.y, k * k) + Mathf.Sin(k * Mathf.PI) * 2f;
                MoveHead(p);
                yield return null;
            }
            foreach (GroundMarker marker in markers) if (marker != null) Destroy(marker.gameObject);
            for (float d = 0f; d <= 18f; d += 3f) Burst(start + direction * d, 1.2f);
            CameraShake.Add(0.55f);
            if (player != null && DistanceToLine(player.position, start, end) <= 2.4f && Mathf.Abs(player.position.y - GroundY) < 3f && HurtPlayer(slamDamage))
            {
                Vector3 sideways = Vector3.Cross(Vector3.up, direction);
                if (Vector3.Dot(sideways, player.position - start) < 0f) sideways = -sideways;
                Shove(sideways, 3f);
            }
            yield return Hold(0.9f / Mathf.Sqrt(Fierce), false); // lying across the sand: vents exposed
            yield return DiveInto(end + direction * 5f, 0.6f);
        }

        /// <summary>
        /// Sandstorm: several ridges converge on the player at once. All but one are fakes that end in sand geysers; the
        /// real one (with a faint glow in its trail) erupts.
        /// </summary>
        private IEnumerator Ambush()
        {
            yield return GoUnder();
            int fakes = Phase >= 4 ? 4 : 3;
            const float travel = 1.7f;
            // First it slips unseen (deep, no ridge) to about 18 m from the player on its own side, so the real ridge
            // starts from where it actually is instead of it jumping there (which yanked its whole body through the sand).
            Vector3 ownSide = Flat(head.position - PlayerGround(0f));
            if (ownSide.sqrMagnitude < 0.01f) ownSide = Flat(UnityEngine.Random.insideUnitSphere);
            Vector3 staging = ClampToArena(PlayerGround(0f) + ownSide.normalized * 18f);
            yield return SwimTo(() => staging, 30f, 2f, 3f, false);
            Vector3 centre = PlayerGround(0.4f);
            float baseAngle = UnityEngine.Random.value * 360f;
            int real = UnityEngine.Random.Range(0, fakes + 1);
            var spots = new List<Vector3>();
            for (int i = 0; i <= fakes; i++)
            {
                float a = baseAngle + i * 360f / (fakes + 1);
                spots.Add(ClampToArena(centre + Quaternion.Euler(0f, a, 0f) * Vector3.forward * (i == real ? 0.5f : 5.5f)));
            }
            for (int i = 0; i <= fakes; i++)
            {
                if (i == real) continue;
                Vector3 from = ClampToArena(spots[i] + Flat(spots[i] - centre).normalized * 18f);
                RunAlongside(FakeRidge(from, spots[i], travel));
            }
            Vector3 strike = spots[real];
            float distance = Vector3.Distance(Flat(head.position), Flat(strike));
            yield return SwimTo(() => strike, Mathf.Max(8f, distance / travel), travel + 0.5f, 1f, true, true);
            GroundMarker marker = Marker(strike, 3.5f);
            yield return Countdown(marker, 0.45f);
            Destroy(marker.gameObject);
            yield return Rise(strike, 9f, 0.35f, true, eruptDamage);
            mouthTarget = 1f;
            yield return Hold(1.3f, true);
            mouthTarget = 0f;
            yield return DiveInto(strike + Flat(head.forward) * 7f, 0.7f);
        }

        private IEnumerator FakeRidge(Vector3 from, Vector3 to, float time)
        {
            float t = 0f, nextPuff = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                Vector3 p = Vector3.Lerp(from, to, t / time);
                if (Time.time >= nextPuff)
                {
                    nextPuff = Time.time + 0.07f;
                    Ridge(p, false);
                }
                yield return null;
            }
            GroundMarker marker = Marker(to, 2f);
            yield return Countdown(marker, 0.45f);
            Destroy(marker.gameObject);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, to, Vector3.up, 26, 1.2f, 9f, 18f);
            AreaHit(to, 2f, geyserDamage, 1.5f);
        }

        /// <summary>Rears up and sweeps a beam of core energy across the arena, scorching a line of fire. Cover blocks it.</summary>
        private IEnumerator CoreBeam()
        {
            yield return GoUnder();
            Vector3 spot = ClampToArena(PlayerGround(0f) + Flat(UnityEngine.Random.insideUnitSphere).normalized * 16f);
            yield return SwimTo(() => spot, 22f, 2.5f, 2f, true);
            yield return Rise(spot, 11f, 0.5f, true, eruptDamage * 0.6f);
            yield return BeamSweep(spot);
            yield return DiveInto(spot + Flat(head.forward) * 6f, 0.7f);
        }

        private IEnumerator BeamSweep(Vector3 spot)
        {
            // Charge: the core blazes up, the mouth opens wide, fire and sparks pour into its throat, the ground shakes.
            mouthTarget = 1f;
            glowTarget = 1.8f;
            float charge = 1.4f / Mathf.Sqrt(Fierce), t = 0f, nextSpark = 0f;
            if (beamMouthLight != null) beamMouthLight.enabled = true;
            while (t < charge)
            {
                t += Time.deltaTime;
                float k = t / charge;
                TrackPlayer(6f);
                if (mouthFlame != null)
                {
                    mouthFlame.Strength = 0.3f + 0.6f * k;
                    mouthFlame.transform.localScale = Vector3.one * (1f + k);
                }
                if (beamMouthLight != null)
                {
                    beamMouthLight.transform.position = Mouth;
                    beamMouthLight.intensity = 3f + 9f * k * (0.8f + 0.2f * Mathf.Sin(Time.time * 30f));
                }
                if (Time.time >= nextSpark)
                {
                    nextSpark = Time.time + Mathf.Lerp(0.1f, 0.04f, k);
                    Vector3 from = Mouth + UnityEngine.Random.onUnitSphere * (2.5f - k);
                    ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, from, Mouth - from, 4, 0.1f, 5f, 10f);
                    if (UnityEngine.Random.value < 0.4f) ParticleBurst.Spawn(ParticleBurst.Kind.Fireball, Mouth, head.forward, 3, 0.4f + k * 0.6f, 1.5f, 40f);
                }
                CameraShake.Add(Time.deltaTime * 0.25f * k);
                yield return null;
            }

            Vector3 toPlayer = Flat(PlayerGround(0f) - spot).normalized;
            if (toPlayer.sqrMagnitude < 0.01f) toPlayer = Flat(head.forward).normalized;
            float sweep = 70f, direction = UnityEngine.Random.value < 0.5f ? 1f : -1f;
            float sweepTime = 2.6f / Mathf.Sqrt(Fierce), nextTick = 0f, nextFire = 0f, nextBurst = 0f;
            const float hitRadius = 2f;
            ShowBeam(true);
            // It fires: a flash at the mouth and a jolt.
            ParticleBurst.Spawn(ParticleBurst.Kind.Fireball, Mouth, head.forward, 24, 1.6f, 7f, 35f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, Mouth, head.forward, 30, 0.14f, 12f, 40f);
            CameraShake.Add(0.5f);
            t = 0f;
            bool first = true;
            while (t < sweepTime)
            {
                t += Time.deltaTime;
                float angle = Mathf.Lerp(-sweep, sweep, t / sweepTime) * direction;
                Vector3 flat = Quaternion.Euler(0f, angle, 0f) * toPlayer;
                Vector3 aim = new Vector3(spot.x, GroundY, spot.z) + flat * 22f;
                Vector3 origin = Mouth;
                Face(aim - head.position, 12f);
                Vector3 end = BeamEnd(origin, aim - origin, 70f);
                DrawBeam(beamHalo, origin, end, 4.6f, 0.15f);
                DrawBeam(beam, origin, end, 1.7f, 0.25f);
                DrawBeam(beamCore, origin, end, 0.55f, 0.08f);
                if (mouthFlame != null)
                {
                    mouthFlame.Strength = 1f;
                    mouthFlame.transform.localScale = Vector3.one * 2.2f;
                }
                float flicker = 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 25f, 1.1f);
                if (beamMouthLight != null)
                {
                    beamMouthLight.transform.position = origin;
                    beamMouthLight.intensity = 12f * flicker;
                }
                if (beamImpactLight != null)
                {
                    beamImpactLight.transform.position = end - (end - origin).normalized * 1.2f + Vector3.up * 0.8f;
                    beamImpactLight.intensity = 10f * flicker;
                }
                if (first || Time.time >= nextBurst)
                {
                    // Where it lands: a fountain of fire, sparks and molten sand.
                    first = false;
                    nextBurst = Time.time + 0.08f;
                    ParticleBurst.Spawn(ParticleBurst.Kind.Fireball, end, Vector3.up + (origin - end).normalized * 0.4f, 6, 1.3f, 5f, 55f);
                    ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, end, origin - end + Vector3.up * 3f, 10, 0.14f, 9f, 70f);
                    if (UnityEngine.Random.value < 0.35f) ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, end + Vector3.up, Vector3.up, 2, 1.4f, 2f, 30f);
                }
                if (Time.time >= nextTick)
                {
                    nextTick = Time.time + 0.2f;
                    if (player != null && DistanceToLine(ChestOf(player), origin, end) <= hitRadius) HurtPlayer(beamDamagePerTick);
                }
                if (Time.time >= nextFire && end.y < GroundY + 0.6f)
                {
                    nextFire = Time.time + 0.22f;
                    FireZone.Spawn(fire, new Vector3(end.x, GroundY, end.z), 1.6f, gameObject, transform, 3f);
                    if (dustMaterial != null) ExpandingRing.Spawn(blastMaterial != null ? blastMaterial : dustMaterial, new Vector3(end.x, GroundY + 0.05f, end.z), 2.5f, 0.25f, 0.15f);
                }
                CameraShake.Add(Time.deltaTime * 0.6f);
                yield return null;
            }
            ShowBeam(false);
            ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, Mouth, head.forward + Vector3.up, 10, 1.2f, 2.5f, 35f);
            if (mouthFlame != null) mouthFlame.Strength = 0f;
            glowTarget = 0.6f;
            yield return Hold(0.8f / Mathf.Sqrt(Fierce), false); // spent: mouth still open, smoking
            mouthTarget = 0f;
        }

        /// <summary>Its plating opens and fires shrapnel in patterns: fans, rings with gaps, spirals. Weave through.</summary>
        private IEnumerator Barrage()
        {
            yield return GoUnder();
            Vector3 spot = ClampToArena(PlayerGround(0f) + Flat(UnityEngine.Random.insideUnitSphere).normalized * 12f);
            yield return SwimTo(() => spot, 22f, 2.5f, 2f, true);
            yield return Rise(spot, 6f, 0.4f, true, eruptDamage * 0.6f);
            yield return BarragePatterns(spot, Phase >= 4 ? 4 : 3);
            yield return DiveInto(spot + Flat(head.forward) * 6f, 0.6f);
        }

        private IEnumerator BarragePatterns(Vector3 spot, int volleys)
        {
            for (int v = 0; v < volleys; v++)
            {
                switch ((v + UnityEngine.Random.Range(0, 3)) % 3)
                {
                    case 0: yield return Fan(spot, 9 + Phase, 80f, 3); break;
                    case 1: yield return Ring(spot, 18 + Phase * 2, 3); break;
                    default: yield return Spiral(spot, Phase >= 4 ? 3 : 2, 2.2f); break;
                }
                yield return Hold(0.35f, true);
            }
        }

        private Vector3 ShrapnelOrigin(Vector3 spot) => new Vector3(spot.x, GroundY + 1.1f, spot.z);
        private float ShrapnelSpeed => 12f + 1.5f * Phase;

        private IEnumerator Fan(Vector3 spot, int count, float spread, int waves)
        {
            for (int w = 0; w < waves; w++)
            {
                Vector3 aim = Flat(PlayerGround(0f) - spot).normalized;
                float offset = (w % 2 == 0 ? 0f : spread / (count - 1) * 0.5f);
                for (int i = 0; i < count; i++)
                {
                    float a = -spread * 0.5f + spread * i / (count - 1) + offset;
                    Shoot(spot, Quaternion.Euler(0f, a, 0f) * aim);
                }
                yield return Hold(0.45f, true);
            }
        }

        private IEnumerator Ring(Vector3 spot, int count, int rings)
        {
            for (int r = 0; r < rings; r++)
            {
                // A ring with a gap to slip through; the gap moves each ring.
                float gapAt = UnityEngine.Random.value * 360f;
                for (int i = 0; i < count; i++)
                {
                    float a = i * 360f / count;
                    if (Mathf.Abs(Mathf.DeltaAngle(a, gapAt)) < 30f) continue;
                    Shoot(spot, Quaternion.Euler(0f, a, 0f) * Vector3.forward);
                }
                yield return Hold(0.65f, true);
            }
        }

        private IEnumerator Spiral(Vector3 spot, int arms, float time)
        {
            float t = 0f, nextShot = 0f, angle = UnityEngine.Random.value * 360f;
            float spin = (UnityEngine.Random.value < 0.5f ? 1f : -1f) * 140f;
            while (t < time)
            {
                t += Time.deltaTime;
                angle += spin * Time.deltaTime;
                if (Time.time >= nextShot)
                {
                    nextShot = Time.time + 0.09f;
                    for (int a = 0; a < arms; a++) Shoot(spot, Quaternion.Euler(0f, angle + a * 360f / arms, 0f) * Vector3.forward);
                }
                TrackPlayer(3f);
                yield return null;
            }
        }

        private void Shoot(Vector3 spot, Vector3 direction)
        {
            Vector3 from = ShrapnelOrigin(spot) + Flat(direction).normalized * 1.8f;
            temporary.Add(CoreShrapnel.Fire(shrapnelMaterial, from, direction, ShrapnelSpeed, shrapnelDamage, transform, hitMask).gameObject);
            if (temporary.Count > 200) temporary.RemoveAll(item => item == null);
        }

        /// <summary>Overlap: shrapnel from the plating while the beam sweeps.</summary>
        private IEnumerator BeamAndBarrage()
        {
            yield return GoUnder();
            Vector3 spot = ClampToArena(PlayerGround(0f) + Flat(UnityEngine.Random.insideUnitSphere).normalized * 15f);
            yield return SwimTo(() => spot, 22f, 2.5f, 2f, true);
            yield return Rise(spot, 11f, 0.5f, true, eruptDamage * 0.6f);
            RunAlongside(DelayedRings(spot));
            yield return BeamSweep(spot);
            yield return DiveInto(spot + Flat(head.forward) * 6f, 0.7f);
        }

        private IEnumerator DelayedRings(Vector3 spot)
        {
            yield return new WaitForSeconds(0.8f);
            yield return Ring(spot, 14 + Phase * 2, Phase >= 4 ? 4 : 3);
        }

        /// <summary>Overlap: a slag volley with a shrapnel spiral underneath it.</summary>
        private IEnumerator SpitAndSpiral()
        {
            yield return GoUnder();
            Vector3 spot = ClampToArena(PlayerGround(0f) + Flat(UnityEngine.Random.insideUnitSphere).normalized * 13f);
            yield return SwimTo(() => spot, 22f, 2.5f, 2f, true);
            yield return Rise(spot, 9f, 0.45f, true, eruptDamage * 0.6f);
            RunAlongside(Spiral(spot, 2, 2.6f));
            mouthTarget = 1f;
            glowTarget = 1.2f;
            for (int i = 0; i < 6; i++)
            {
                Vector3 target = ClampToArena(i == 0 ? PlayerGround(0.7f) : PlayerGround(0.3f) + Flat(UnityEngine.Random.insideUnitSphere) * 6f);
                CoreSlag.Launch(slagMaterial, warningMaterial, blastMaterial, fire, Mouth, target, 1.1f, 2.2f, slagDamage, gameObject, hitMask);
                yield return Hold(0.3f, true);
            }
            yield return Hold(0.9f, true);
            mouthTarget = 0f;
            glowTarget = 0.5f;
            yield return DiveInto(spot + Flat(head.forward) * 6f, 0.6f);
        }

        /// <summary>
        /// Meltdown: rears up in the middle and overloads the core for a huge blast that only cover can stop. Afterwards
        /// it collapses, stunned, mouth hanging open: the big opening.
        /// </summary>
        private IEnumerator CoreNova()
        {
            yield return GoUnder();
            Vector3 middle = ArenaCentre;
            yield return SwimTo(() => middle, 24f, 3f, 1.5f, true);
            yield return Rise(middle, 12f, 0.6f, true, eruptDamage);
            MiniBossHud.Message("THE CORE IS OVERLOADING!  GET BEHIND COVER!", new Color(1f, 0.35f, 0.15f), 3f);
            mouthTarget = 1f;
            float charge = 3f, t = 0f, nextSpark = 0f;
            Vector3 rest = head.position;
            while (t < charge)
            {
                t += Time.deltaTime;
                float k = t / charge;
                glowTarget = 1f + k * 2f;
                if (mouthFlame != null) mouthFlame.Strength = k;
                CameraShake.Add(Time.deltaTime * (0.2f + k * 0.6f));
                if (Time.time >= nextSpark)
                {
                    nextSpark = Time.time + Mathf.Lerp(0.15f, 0.04f, k);
                    ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, core != null ? core.position : Mouth, UnityEngine.Random.onUnitSphere, 6, 0.1f, 6f, 80f);
                }
                head.position = rest + Tremble(0.35f * k); // shakes in place
                yield return null;
            }
            Vector3 blastAt = core != null ? core.position : Mouth;
            FuelShell.Blast(blastAt, novaRadius, novaDamage, 0.5f, gameObject, transform, hitMask);
            ParticleBurst.Explosion(blastAt, 5f);
            if (blastMaterial != null) ExpandingRing.Spawn(blastMaterial, new Vector3(blastAt.x, GroundY + 0.1f, blastAt.z), novaRadius * 0.6f, 0.6f, 0.4f);
            CameraShake.Add(1f);
            if (mouthFlame != null) mouthFlame.Strength = 0f;
            glowTarget = 0.2f;

            // Collapses onto the sand, stunned, with the core open.
            Vector3 slumpTo = new Vector3(middle.x, GroundY + 1.3f, middle.z) + Flat(head.forward).normalized * 8f;
            Vector3 from = head.position;
            t = 0f;
            while (t < 0.6f)
            {
                t += Time.deltaTime;
                float k = t / 0.6f;
                MoveHead(Vector3.Lerp(from, slumpTo, k * k));
                yield return null;
            }
            Burst(slumpTo, 2f);
            MiniBossHud.Message("It's stunned!  Hit the core!", new Color(1f, 0.85f, 0.4f), 2f);
            mouthTarget = 1f;
            t = 0f;
            while (t < 3.5f)
            {
                t += Time.deltaTime;
                glowTarget = 0.6f + 0.4f * Mathf.Sin(Time.time * 10f);
                yield return null;
            }
            mouthTarget = 0f;
            glowTarget = 0.6f;
            yield return DiveInto(slumpTo + Flat(head.forward) * 5f, 0.8f);
        }

        // ---------- Movement ----------

        private Vector3 ArenaCentre => arena != null ? arena.Centre : transform.position;

        private Vector3 ClampToArena(Vector3 point)
        {
            Vector3 centre = ArenaCentre;
            Vector3 offset = Flat(point - centre);
            float limit = arena != null ? arena.Radius - 3f : 30f;
            if (offset.magnitude > limit) offset = offset.normalized * limit;
            return new Vector3(centre.x + offset.x, GroundY, centre.z + offset.z);
        }

        /// <summary>The ground under the player, optionally where they will be in a moment.</summary>
        private Vector3 PlayerGround(float lead)
        {
            if (player == null) return ArenaCentre;
            Vector3 velocity = playerBody != null ? Flat(playerBody.velocity) : Vector3.zero;
            return ClampToArena(player.position + velocity * lead);
        }

        /// <summary>Gets below the sand from wherever it is (it might be in the air when interrupted).</summary>
        private IEnumerator GoUnder()
        {
            if (head.position.y < GroundY - 1f)
            {
                yield break;
            }
            Vector3 into = ClampToArena(head.position + Flat(head.forward) * 5f);
            yield return DiveInto(into, 0.6f);
        }

        /// <summary>Swims under the sand toward a moving target, leaving a ridge of sand on the surface.</summary>
        private IEnumerator SwimTo(Func<Vector3> target, float speed, float maxTime, float arrive, bool ridge, bool glowing = false)
        {
            float t = 0f, nextPuff = 0f, v = currentSpeed;
            while (t < maxTime)
            {
                t += Time.deltaTime;
                Vector3 goal = target();
                Vector3 flat = Flat(goal - head.position);
                float distance = flat.magnitude;
                if (distance <= arrive) yield break;
                // Carries on from how it was actually moving: speeds up smoothly, and turns tighter when the target is
                // close so it never circles round it without arriving.
                v = Mathf.MoveTowards(v, speed, SwimAcceleration * Time.deltaTime);
                float turn = Mathf.Clamp(2.2f * v / Mathf.Max(1f, distance), 4f, 10f);
                heading = Vector3.RotateTowards(Flat(heading).normalized, flat / distance, turn * Time.deltaTime, 0f);
                if (heading.sqrMagnitude < 0.01f) heading = flat / distance;
                Vector3 p = head.position + heading * v * Time.deltaTime;
                p.y = Mathf.MoveTowards(head.position.y, UnderY, 12f * Time.deltaTime);
                MoveHead(p);
                if (ridge && Time.time >= nextPuff)
                {
                    nextPuff = Time.time + 0.06f;
                    Ridge(new Vector3(p.x, GroundY, p.z), glowing);
                }
                yield return null;
            }
        }

        /// <summary>Circles the player under the sand between attacks (the ridge shows where it is).</summary>
        private IEnumerator Circle(float time)
        {
            float t = 0f, nextPuff = 0f;
            float side = UnityEngine.Random.value < 0.5f ? 1f : -1f;
            yield return GoUnder();
            float v = currentSpeed;
            while (t < time)
            {
                t += Time.deltaTime;
                Vector3 centre = PlayerGround(0f);
                Vector3 radial = Flat(head.position - centre);
                if (radial.sqrMagnitude < 0.01f) radial = Vector3.forward;
                // Aim inside the arena, so it steers along the edge instead of grinding against it.
                Vector3 goal = ClampToArena(centre + Quaternion.Euler(0f, 40f * side, 0f) * radial.normalized * 13f);
                Vector3 flat = Flat(goal - head.position);
                if (flat.sqrMagnitude > 0.01f) heading = Vector3.RotateTowards(Flat(heading).normalized, flat.normalized, 3f * Time.deltaTime, 0f);
                v = Mathf.MoveTowards(v, 12f, SwimAcceleration * Time.deltaTime);
                Vector3 p = head.position + heading * v * Time.deltaTime;
                p.y = Mathf.MoveTowards(head.position.y, UnderY, 12f * Time.deltaTime);
                MoveHead(ClampToArena(p) + Vector3.up * (p.y - GroundY));
                if (Time.time >= nextPuff)
                {
                    nextPuff = Time.time + 0.08f;
                    Ridge(new Vector3(p.x, GroundY, p.z), false);
                }
                yield return null;
            }
        }

        /// <summary>Bursts straight up out of the sand at a point.</summary>
        private IEnumerator Rise(Vector3 ground, float height, float time, bool facePlayer, float damage = 0f)
        {
            ground.y = GroundY;
            // From where it actually is (no jump): it slides under the spot in the first part of the rise.
            Vector3 from = head.position;
            Vector3 to = ground + Vector3.up * height;
            bool burst = false;
            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                float k = 1f - (1f - Mathf.Clamp01(t / time)) * (1f - Mathf.Clamp01(t / time)); // fast out, easing at the top
                float across = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / (time * 0.45f)));
                Vector3 p = Vector3.Lerp(from, to, across);
                p.y = Mathf.Lerp(from.y, to.y, k);
                MoveHead(p);
                if (!burst && p.y >= GroundY - 0.5f)
                {
                    burst = true;
                    Burst(ground, 2f + height * 0.1f);
                    if (damage > 0f) AreaHit(ground, 3.5f, damage, 2.5f);
                }
                if (facePlayer && k > 0.6f) TrackPlayer(5f);
                yield return null;
            }
        }

        /// <summary>Arcs over and down into the sand at a point, then on below it.</summary>
        private IEnumerator DiveInto(Vector3 ground, float time)
        {
            ground = ClampToArena(ground);
            Vector3 from = head.position;
            Vector3 control = Vector3.Lerp(from, ground, 0.5f) + Vector3.up * Mathf.Max(2f, (from.y - GroundY) * 0.4f);
            Vector3 to = new Vector3(ground.x, UnderY, ground.z) + Flat(ground - from).normalized * 2f;
            bool burst = from.y < GroundY;
            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / time);
                Vector3 p = (1f - k) * (1f - k) * from + 2f * (1f - k) * k * control + k * k * to;
                MoveHead(p);
                if (!burst && p.y < GroundY)
                {
                    burst = true;
                    Burst(new Vector3(p.x, GroundY, p.z), 1.6f);
                }
                yield return null;
            }
            mouthTarget = 0f;
        }

        private IEnumerator Hold(float time, bool trackPlayer)
        {
            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                if (trackPlayer) TrackPlayer(4f);
                head.position += Vector3.up * Mathf.Sin(Time.time * 2.5f) * 0.3f * Time.deltaTime; // a slow sway
                yield return null;
            }
        }

        private static IEnumerator Wait(float time)
        {
            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator Countdown(GroundMarker marker, float time)
        {
            float t = 0f;
            while (t < time)
            {
                t += Time.deltaTime;
                if (marker != null) marker.Progress = t / time;
                yield return null;
            }
        }

        private void MoveHead(Vector3 position)
        {
            Vector3 travel = position - head.position;
            head.position = position;
            if (travel.sqrMagnitude > 0.00001f) Face(travel, 10f);
            Vector3 flat = Flat(travel);
            if (flat.sqrMagnitude > 0.000001f) heading = flat.normalized; // so the next swim carries on the same way
            if (Time.deltaTime > 0f) currentSpeed = flat.magnitude / Time.deltaTime;
            movedThisFrame = true;
            Record();
        }

        private void Face(Vector3 direction, float rate)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            direction = LimitToNeck(direction.normalized);
            // Upright normally; when pointing nearly straight up or down, keep its current roll instead (blended, so it
            // never snaps round, which would also spin the body).
            float vertical = Mathf.Abs(Vector3.Dot(direction, Vector3.up));
            Vector3 up = Vector3.Slerp(Vector3.up, head.up, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 0.97f, vertical)));
            head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(direction, up), 1f - Mathf.Exp(-rate * Time.deltaTime));
        }

        /// <summary>Keeps a wanted facing within <see cref="maxNeckBend"/> of the line of its neck.</summary>
        private Vector3 LimitToNeck(Vector3 want)
        {
            Vector3 back = TrailPointAt(3f) - head.position; // about 3 m back along the body (measured along the trail, so it changes smoothly)
            if (back.sqrMagnitude < 0.25f) return want;
            Vector3 neck = -back.normalized;
            float angle = Vector3.Angle(neck, want);
            return angle <= maxNeckBend ? want : Vector3.Slerp(neck, want, maxNeckBend / angle);
        }

        /// <summary>The point a distance back along the trail from the head.</summary>
        private Vector3 TrailPointAt(float distance)
        {
            Vector3 previous = head.position;
            foreach (Vector3 point in trail)
            {
                float span = Vector3.Distance(previous, point);
                if (span >= distance && span > 0.0001f) return Vector3.Lerp(previous, point, distance / span);
                distance -= span;
                previous = point;
            }
            return previous;
        }

        /// <summary>
        /// Once its head is under the sand, whatever of its body is still sticking out sinks after it, so the tail
        /// slides under smoothly instead of hanging in the air while the head waits or swims off.
        /// </summary>
        private void SinkTrail(float dt)
        {
            if (IsDefeated) return;
            // Eases in as the head gets deeper (no sudden start), sinks each point in proportion to its height (no hard
            // stop at the bottom), and leaves the neck alone so the head's own movement stays smooth.
            float depth = Mathf.SmoothStep(0f, 1f, (GroundY - 0.5f - head.position.y) / 2f);
            if (depth <= 0f) return;
            float floor = UnderY + 0.5f, amount = (1f - Mathf.Exp(-trailSinkRate * dt)) * depth, along = 0f;
            Vector3 previous = head.position;
            for (int i = 0; i < trail.Count; i++)
            {
                Vector3 point = trail[i];
                along += Vector3.Distance(previous, point);
                previous = point;
                if (point.y <= floor) continue;
                float weight = Mathf.SmoothStep(0f, 1f, (along - 2f) / 4f);
                point.y -= (point.y - floor) * amount * weight;
                trail[i] = point;
            }
        }

        private void TrackPlayer(float rate)
        {
            if (playerView != null) Face(playerView.position - head.position, rate);
        }

        // ---------- Body ----------

        private void Record()
        {
            if (trail.Count == 0 || (trail[0] - head.position).sqrMagnitude >= 0.0625f) trail.Insert(0, head.position);
            float needed = neckGap + segmentSpacing * (segments.Length + 2);
            float length = 0f;
            for (int i = 1; i < trail.Count; i++)
            {
                length += Vector3.Distance(trail[i - 1], trail[i]);
                if (length > needed + 2f)
                {
                    trail.RemoveRange(i + 1, trail.Count - i - 1);
                    break;
                }
            }
        }

        /// <summary>Lays the segments along the path the head took, each a fixed distance behind the one before.</summary>
        private void PlaceBody()
        {
            if (head == null) return;
            // The path the body lies along: the head, then everywhere it has been, with nothing skipped (skipping points
            // near the head made the body jump about whenever the trail passed close to it). The body bends the neck
            // out of the back of the skull itself.
            path.Clear();
            path.Add(head.position);
            foreach (Vector3 point in trail)
                if ((point - path[path.Count - 1]).sqrMagnitude > 0.0001f) path.Add(point);
            if (path.Count < 2) path.Add(head.position - head.forward * 3f);

            if (body != null)
            {
                body.Rebuild(path, head.up, head.forward);
                // Anchors (hit boxes and weak spots) on the smoothed curve the body is built on.
                for (int i = 0; i < segments.Length; i++)
                {
                    if (segments[i] == null) continue;
                    Vector3 p = body.PointAt(neckGap + i * segmentSpacing, out Vector3 towardTail);
                    segments[i].position = p;
                    if (towardTail.sqrMagnitude > 0.0001f)
                        segments[i].rotation = Quaternion.LookRotation(-towardTail, Mathf.Abs(towardTail.y) > 0.95f ? head.up : Vector3.up);
                }
                return;
            }

            // No body mesh: anchors straight along the raw path.
            int s = 0;
            float want = neckGap, walked = 0f;
            Vector3 previous = path[0];
            Vector3 lastDirection = (path[1] - path[0]).normalized;
            for (int i = 1; i <= path.Count && s < segments.Length; i++)
            {
                Vector3 next = i < path.Count ? path[i] : previous + lastDirection * 1000f;
                float step = Vector3.Distance(previous, next);
                if (step > 0.0001f) lastDirection = (next - previous) / step;
                while (s < segments.Length && walked + step >= want)
                {
                    Vector3 p = previous + lastDirection * (want - walked);
                    if (segments[s] != null)
                    {
                        segments[s].position = p;
                        if (lastDirection.sqrMagnitude > 0.0001f)
                            segments[s].rotation = Quaternion.LookRotation(-lastDirection, Mathf.Abs(lastDirection.y) > 0.95f ? head.up : Vector3.up);
                    }
                    s++;
                    want += segmentSpacing;
                }
                walked += step;
                previous = next;
            }
        }

        private void LateUpdate()
        {
            if (!movedThisFrame) currentSpeed = 0f;
            movedThisFrame = false;
            SinkTrail(Time.deltaTime);
            PlaceBody();
            if (pendingDamage > 0f)
            {
                float amount = pendingDamage;
                GameObject source = pendingSource;
                pendingDamage = 0f;
                pendingSource = null;
                if (!Invulnerable && !IsDefeated) health.TakeDamage(amount, source);
            }
            UpdateShove(Time.deltaTime);
            Animate(Time.deltaTime);
        }

        private void Animate(float dt)
        {
            mouth = Mathf.MoveTowards(mouth, mouthTarget, dt * 4f);
            glow = Mathf.Lerp(glow, glowTarget, 1f - Mathf.Exp(-6f * dt));
            for (int i = 0; i < jaws.Length; i++)
                if (jaws[i] != null)
                    jaws[i].localRotation = jawRest[i] * Quaternion.Euler(-48f * mouth + Mathf.Sin(Time.time * 7f + i) * 3f * mouth, 0f, 0f);

            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * (4f + glow * 6f));
            if (coreRenderer != null)
            {
                coreRenderer.GetPropertyBlock(block);
                block.SetColor(BaseColor, Color.Lerp(coreColor, Color.white, Mathf.Clamp01(glow - 1f)) * Mathf.Max(0.4f, glow) * pulse);
                coreRenderer.SetPropertyBlock(block);
            }
            if (coreLight != null)
            {
                coreLight.intensity = (1.5f + glow * 4f) * pulse * (0.3f + 0.7f * Mathf.Max(mouth, 0.2f));
                coreLight.range = 8f + glow * 6f;
            }
            if (body != null) body.SetGlow((0.7f + 0.3f * Mathf.Sin(Time.time * 2.2f)) * (0.6f + glow * 0.5f) * (0.85f + 0.15f * Phase));
            Exhaust();
            float vent = 0.6f + 0.4f * Mathf.Sin(Time.time * 3f) + glow * 0.3f;
            foreach (Renderer item in ventRenderers)
            {
                if (item == null) continue;
                item.GetPropertyBlock(block);
                block.SetColor(BaseColor, ventColor * vent);
                item.SetPropertyBlock(block);
            }
        }

        /// <summary>The ship's exhausts grown into it smoke when above the sand, and belch fire as the core runs hot.</summary>
        private void Exhaust()
        {
            if (exhaustStacks.Length == 0 || Time.time < nextExhaust || IsDefeated) return;
            nextExhaust = Time.time + UnityEngine.Random.Range(0.25f, 0.6f) / Mathf.Max(1f, glow);
            Transform stack = exhaustStacks[UnityEngine.Random.Range(0, exhaustStacks.Length)];
            if (stack == null || stack.position.y < GroundY + 0.3f) return;
            bool fiery = Phase >= 4 || glow > 1.1f;
            ParticleBurst.Spawn(fiery ? ParticleBurst.Kind.Fireball : ParticleBurst.Kind.Smoke, stack.position, stack.forward,
                fiery ? 5 : 3, fiery ? 0.5f : 0.6f, fiery ? 4f : 2f, 20f);
        }

        // ---------- Hurting the player ----------

        private bool HurtPlayer(float damage) => playerHealth != null && !playerHealth.IsDead && playerHealth.TakeDamage(damage, gameObject);

        private void AreaHit(Vector3 at, float radius, float damage, float shove)
        {
            if (player == null) return;
            Vector3 offset = player.position - at;
            if (Flat(offset).magnitude > radius || offset.y > 4f || offset.y < -2f) return;
            if (HurtPlayer(damage)) Shove(offset, shove);
        }

        private void BodyContact(float radius, float damage)
        {
            if (contactHit || player == null) return;
            if ((ChestOf(player) - head.position).magnitude > radius) return;
            contactHit = true;
            if (HurtPlayer(damage)) Shove(player.position - head.position, 2.5f);
        }

        private void Shove(Vector3 direction, float distance)
        {
            direction = Flat(direction);
            if (direction.sqrMagnitude < 0.0001f || playerBody == null) return;
            shoveDirection = direction.normalized;
            shoveRemaining = distance;
        }

        private void UpdateShove(float dt)
        {
            if (shoveRemaining <= 0f || playerBody == null || !playerBody.enabled) return;
            float step = Mathf.Min(shoveRemaining, 10f * dt);
            playerBody.Move(shoveDirection * step);
            shoveRemaining -= step;
        }

        // ---------- Effects ----------

        private GroundMarker Marker(Vector3 at, float radius)
        {
            GroundMarker marker = GroundMarker.Spawn(warningMaterial, null, new Vector3(at.x, GroundY, at.z), radius);
            temporary.Add(marker.gameObject);
            return marker;
        }

        /// <summary>A geyser of sand where it breaks the surface, with a shake that fades with distance.</summary>
        private void Burst(Vector3 at, float size)
        {
            at.y = GroundY;
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, at + Vector3.up * 0.2f, Vector3.up, Mathf.RoundToInt(18f + size * 8f), size, size * 4f, 35f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, at + Vector3.up * 0.2f, Vector3.up, 12, size * 0.8f, size * 2.5f, 85f);
            if (dustMaterial != null) ExpandingRing.Spawn(dustMaterial, at + Vector3.up * 0.05f, size * 2.5f, 0.4f, 0.2f);
            float distance = player != null ? Vector3.Distance(player.position, at) : 30f;
            CameraShake.Add(Mathf.Clamp01(size * 0.25f) * Mathf.Clamp01(1f - distance / 40f));
        }

        /// <summary>The sand ridge above it as it swims (an orange glint in the trail marks the real one in an ambush).</summary>
        private void Ridge(Vector3 at, bool glowing)
        {
            if (arena != null && !arena.IsSolid(at)) return; // over a pit you see the worm itself
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, at + Vector3.up * 0.1f, Vector3.up + UnityEngine.Random.insideUnitSphere * 0.3f, 3, 0.9f, 2.5f, 30f);
            if (glowing) ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, at + Vector3.up * 0.2f, Vector3.up, 2, 0.08f, 2f, 30f, new Color(1f, 0.55f, 0.15f));
        }

        private Vector3 BeamEnd(Vector3 origin, Vector3 direction, float range)
        {
            Vector3 end = origin + direction.normalized * range;
            float nearest = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction.normalized, range, hitMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.collider is CharacterController || hit.collider.GetComponentInParent<Health>() != null) continue; // it burns through creatures
                if (hit.collider.GetComponentInParent<OilSlick>() != null) continue;
                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                    end = hit.point;
                }
            }
            return end;
        }

        private static Vector3 ChestOf(Transform target) => target.position + Vector3.up * 1f;

        private static Vector3 Tremble(float amount)
        {
            float n = Time.time * 18f;
            return new Vector3(Mathf.PerlinNoise(n, 0.1f) - 0.5f, Mathf.PerlinNoise(0.4f, n) - 0.5f, Mathf.PerlinNoise(n, 0.8f) - 0.5f) * 2f * amount;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private static float DistanceToLine(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float k = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(point, a + ab * k);
        }

        // ---------- Damage and death ----------

        private void OnDamaged(Health _, float amount, GameObject source) => CheckPhase();

        private void OnDied(Health _)
        {
            if (IsDefeated) return;
            IsDefeated = true;
            Fighting = false;
            Invulnerable = true;
            StopFight();
            CorewormHud.ShowBoss(null);
            if (arena != null) arena.Settle();
            StartCoroutine(Death());
        }

        private IEnumerator Death()
        {
            MiniBossHud.Message($"{displayName} IS FINISHED!", new Color(1f, 0.85f, 0.35f), 3f);
            // Rears up, the core flickering out of control, then blows apart and the body crashes down.
            Vector3 spot = ClampToArena(head.position);
            if (head.position.y < GroundY) yield return Rise(spot, 12f, 0.6f, true);
            mouthTarget = 1f;
            float t = 0f, nextSpark = 0f;
            Vector3 rest = head.position;
            while (t < 2.2f)
            {
                t += Time.deltaTime;
                glowTarget = 1f + Mathf.PerlinNoise(Time.time * 20f, 0f) * 2f;
                head.position = rest + Tremble(0.5f);
                CameraShake.Add(Time.deltaTime * 0.6f);
                if (Time.time >= nextSpark)
                {
                    nextSpark = Time.time + 0.06f;
                    ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, core != null ? core.position : Mouth, UnityEngine.Random.onUnitSphere, 8, 0.1f, 7f, 80f);
                }
                yield return null;
            }
            Vector3 blast = core != null ? core.position : Mouth;
            ParticleBurst.Explosion(blast, 4f);
            CameraShake.Add(1f);
            glowTarget = 0f;

            // The Engine Core comes free and drops as the pickup.
            Vector3 dropAt = ClampToArena(head.position + Flat(head.forward) * 3f);
            if (core != null)
            {
                core.SetParent(null, true);
                foreach (Collider collider in core.GetComponentsInChildren<Collider>(true)) Destroy(collider);
                foreach (Health box in core.GetComponentsInChildren<Health>(true)) Destroy(box);
                EngineCorePickup.Create(core, partName, dropAt);
                coreRenderer = null; // it's the pickup's now
                coreLight = null;
                core = null;
            }
            Defeated?.Invoke();

            // The body crashes down and sinks, segment by segment.
            Vector3 from = head.position, to = new Vector3(dropAt.x, GroundY + 0.8f, dropAt.z) - Flat(head.forward) * 4f;
            t = 0f;
            while (t < 0.7f)
            {
                t += Time.deltaTime;
                MoveHead(Vector3.Lerp(from, to, (t / 0.7f) * (t / 0.7f)));
                PlaceBody();
                yield return null;
            }
            Burst(to, 3f);
            foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            yield return Wait(1.5f);
            t = 0f;
            while (t < 4f)
            {
                // Everything sinks together: the head, and the path the body is laid along.
                t += Time.deltaTime;
                Vector3 sink = Vector3.down * 1.5f * Time.deltaTime;
                head.position += sink;
                for (int i = 0; i < trail.Count; i++) trail[i] += sink;
                yield return null;
            }
            gameObject.SetActive(false);
        }
    }
}
