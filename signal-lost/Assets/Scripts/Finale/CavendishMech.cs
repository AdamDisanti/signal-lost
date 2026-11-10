using System;
using System.Collections;
using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Enemies.Desert;
using SignalLost.Mudlands;
using SignalLost.Player;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace SignalLost.Finale
{
    /// <summary>
    /// The final boss (docs/final-boss.md): Cavendish piloting a mech built from the ship parts the player handed him.
    /// Three phases, raid style:
    /// <list type="bullet">
    /// <item>each phase is a damage window; at its threshold (66%, then 33%) a shield goes up and the mech is immune
    /// until the player finishes a task (<see cref="FinaleEncounter"/> runs the tasks and calls <see cref="BreakShield"/>);</item>
    /// <item>the next phase is faster and, as the fight is built out, adds weapons;</item>
    /// <item>dying restarts the current phase (<see cref="ResetToPhase"/>), not the fight.</item>
    /// </list>
    /// Phase 1's weapons (built): the <b>Cooling System</b> freezing sweep (a wedge on the ground, then a beam across
    /// it that leaves ice), the <b>Engine Core</b> thruster charge (a strip, then a dash that leaves fire) and a stomp
    /// if you stand under it. The glowing <b>Reactor Core</b> on the chest takes extra damage.
    /// The Drunk Driving car always blows up on it; the blast hurts (not while shielded) but never stuns it.
    /// It doesn't derive from <see cref="DesertMiniBoss"/>: that base resets to full health on a death and has two phases.
    /// All numbers are first guesses for a fight of at most 15 minutes, to tune in play.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(NavMeshAgent))]
    public sealed class CavendishMech : MonoBehaviour, IVehicleReactive, ICarBlastReactive
    {
        public enum State { Dormant, Waking, Fighting, Staggered, Defeated }

        [SerializeField] private string displayName = "Cavendish";
        [Tooltip("Cavendish's model, seated in the cockpit.")]
        [SerializeField] private GameObject pilotPrefab;

        [Header("Phases")]
        [Tooltip("Health fractions where the shield goes up (end of phase 1, end of phase 2).")]
        [SerializeField] private float[] shieldAt = { 0.66f, 0.33f };
        [Tooltip("Attack speed per phase (1, 2, 3).")]
        [SerializeField] private float[] tempo = { 1f, 1.2f, 1.4f };
        [Tooltip("Attack speed while shielded, so the player has room to do the task.")]
        [SerializeField, Range(0.3f, 1f)] private float shieldedTempo = 0.7f;
        [SerializeField, Min(1f)] private float reactorMultiplier = 1.5f;

        // Tuned "very hard" (Adam, 11-09: a new player should expect 5+ deaths): it hits hard, its warnings cover big
        // areas, it moves fast, and fire stays on the ground.
        [Header("Movement")]
        [SerializeField, Min(0.5f)] private float walkSpeed = 6.5f;
        [SerializeField, Min(4f)] private float preferredRange = 14f;
        [SerializeField, Min(10f)] private float turnRate = 120f;
        [SerializeField, Min(0f)] private float attackGap = 0.9f;

        [Header("Cooling System: freezing sweep")]
        [SerializeField] private float sweepWindup = 1.3f, sweepTime = 2f, sweepRange = 32f, sweepHalfAngle = 55f, sweepCooldown = 5f;
        [SerializeField] private float sweepDamage = 11f, sweepTick = 0.15f, sweepWidth = 2.6f;
        [SerializeField] private float iceRadius = 3.6f, iceLife = 12f, iceSlow = 0.5f;

        [Header("Engine Core: thruster charge")]
        [SerializeField] private float chargeWindup = 1f, chargeSpeed = 32f, chargeMaxLength = 42f, chargeWidth = 9f, chargeCooldown = 6f;
        [SerializeField] private float chargeDamage = 40f, chargeShove = 9f;
        [SerializeField] private FireStyle fireTrail = new FireStyle { damage = 9f, interval = 0.5f, duration = 22f, smoke = 0.5f, fierceness = 0.7f };

        [Header("Stomp")]
        [SerializeField] private float stompWindup = 0.75f, stompRadius = 11.5f, stompDamage = 38f, stompShove = 7f, stompCooldown = 3f;

        [Header("Engine Core: thruster lunge (phase 1+)")]
        [SerializeField] private float lungeWindup = 0.75f, lungeAir = 1f, lungeHeight = 9f, lungeRadius = 10f, lungeDamage = 45f, lungeShove = 8f;
        [SerializeField] private float lungeCooldown = 7f, lungeMaxRange = 36f;

        [Header("Flight Control Module: rocket barrage (phase 1+)")]
        [SerializeField, Range(1, 4)] private int barrageVolleys = 2;
        [SerializeField, Range(1, 16)] private int barrageMissiles = 7;
        [SerializeField] private float barrageSpread = 9f, barrageRadius = 3.6f, barrageDamage = 26f, barrageWarning = 1.1f, barrageCooldown = 9f;
        [SerializeField] private FireStyle craterFire = new FireStyle { damage = 8f, interval = 0.5f, duration = 10f, smoke = 0.6f, fierceness = 0.6f };

        [Header("Cooling System: tidal wave (phase 2+)")]
        [SerializeField] private float waveWindup = 0.9f, waveSpeed = 15f, waveReach = 48f, waveDamage = 32f, waveCooldown = 11f;

        [Header("Reactor: scrap magnet (phase 2+)")]
        [SerializeField] private float magnetTime = 2.4f, magnetPull = 5.6f, magnetRange = 45f, magnetCooldown = 13f;
        [SerializeField] private float slamRadius = 12f, slamDamage = 42f;

        [Header("Engine Core: napalm flyover (phase 3)")]
        [SerializeField] private float napalmHeight = 10f, napalmSpeed = 24f, napalmLength = 64f, napalmWidth = 7f, napalmCooldown = 14f;
        [SerializeField] private FireStyle napalm = new FireStyle { damage = 10f, interval = 0.5f, duration = 18f, smoke = 0.6f, fierceness = 0.7f };

        public event Action<int> ShieldRaised;
        public event Action<int> PhaseStarted;
        public event Action Beaten;

        public string DisplayName => displayName;
        public State Current { get; private set; } = State.Dormant;
        public int Phase { get; private set; } = 1;
        public bool Shielded { get; private set; }
        public Health Health { get; private set; }
        public float HealthFraction => Health != null ? Health.Normalized : 1f;
        public IReadOnlyList<float> ShieldThresholds => shieldAt;
        /// <summary>Health fraction the current phase started at (where a retry puts it back).</summary>
        public float PhaseStart => Phase <= 1 ? 1f : shieldAt[Mathf.Min(Phase - 2, shieldAt.Length - 1)];
        public string AttackName { get; private set; } = "";
        public MechRig Rig { get; private set; }
        public Vector3 Home { get; private set; }

        /// <summary>The arena the leaps and flyovers stay inside (set by the encounter).</summary>
        public void SetArena(Vector3 centre, float radius)
        {
            arenaCentre = centre;
            arenaRadius = radius;
        }

        private NavMeshAgent agent;
        private PlayerLife player;
        private Health playerHealth;
        private CharacterController playerBody;
        private Transform shield;
        private Light reactorLight, muzzleLight;
        private FireEffect jetL, jetR;
        private Transform beam;
        private Coroutine attack;
        private readonly List<Telegraph> telegraphs = new List<Telegraph>();
        private readonly List<FireZone> fires = new List<FireZone>();
        private readonly List<GameObject> temporaries = new List<GameObject>(); // missiles and waves in flight
        private float nextAttack, sweepReady, chargeReady, stompReady, lungeReady, barrageReady, waveReady, magnetReady, napalmReady, closeSince = -1f;
        private string lastAttack = "";
        private Vector3 arenaCentre;
        private float arenaRadius = 40f;
        private bool airborne;
        private bool settingHealth;
        private int lastBodyHitFrame = -1;

        // Pose targets, eased toward each frame.
        private float crouch, lean, aim, liftR, kneel, stride, walkWeight, thrusterHeat;
        private float crouchNow, leanNow, aimNow, liftNow, kneelNow = 1f, deadNow, waistYaw, nextSmoke, reactorBoost;
        private Vector3 shoveDirection;
        private float shoveRemaining;

        // ---------- Unity ----------

        private void Awake()
        {
            Health = GetComponent<Health>();
            agent = GetComponent<NavMeshAgent>();
            agent.updateRotation = false;
            agent.speed = walkSpeed;
            Transform visual = transform.Find("Visual");
            if (visual == null)
            {
                visual = new GameObject("Visual").transform;
                visual.SetParent(transform, false);
            }
            Rig = MechModel.Build(visual, pilotPrefab);
            shield = MechModel.Shield(transform, 9.5f);
            shield.gameObject.SetActive(false);
            reactorLight = AddLight(Rig.Reactor, FinalePalette.Reactor, 9f, 2.2f);
            muzzleLight = AddLight(Rig.CannonMuzzle, FinalePalette.Coolant, 10f, 0f);
            jetL = FireEffect.SpawnJet(Rig.ThrusterL, new Vector3(0f, 1.3f, 0f), Quaternion.Euler(-90f, 0f, 0f), 0.5f, 3.5f);
            jetR = FireEffect.SpawnJet(Rig.ThrusterR, new Vector3(0f, 1.3f, 0f), Quaternion.Euler(-90f, 0f, 0f), 0.5f, 3.5f);
            jetL.Strength = jetR.Strength = 0f;
            AddWeakSpot();
            AddLimbColliders();
            kneel = 1f; // dormant: kneeling by the wreck, powered down, until Wake
            Health.Invulnerable = true;
        }

        private void OnEnable()
        {
            Health.Damaged += OnDamaged;
            Health.Died += OnDied;
        }

        private void OnDisable()
        {
            Health.Damaged -= OnDamaged;
            Health.Died -= OnDied;
        }

        private void Start()
        {
            player = FindAnyObjectByType<PlayerLife>();
            if (player != null)
            {
                playerHealth = player.Health;
                playerBody = player.GetComponent<CharacterController>();
            }
            Home = transform.position;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 6f, NavMesh.AllAreas))
            {
                agent.enabled = true;
                agent.Warp(hit.position);
                Home = hit.position;
            }
            agent.isStopped = true;
            if (arenaCentre == Vector3.zero) arenaCentre = Home;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            UpdateShove(dt);
            if (Current == State.Fighting) Think(dt);
            if (Current == State.Defeated && Time.time >= nextSmoke)
            {
                nextSmoke = Time.time + Random.Range(0.3f, 0.8f);
                ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, Rig.Reactor.position, Vector3.up, 8, 1.4f, 2.5f);
                if (Random.value < 0.4f) ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, Rig.Reactor.position, Random.onUnitSphere, 14, 0.15f, 7f);
            }
            if (agent.enabled && agent.isOnNavMesh && agent.isStopped == false && Current != State.Fighting) agent.isStopped = true;
        }

        private void LateUpdate() => Animate(Time.deltaTime);

        // ---------- The fight's flow (called by FinaleEncounter) ----------

        /// <summary>The intro: the mech stands up from its kneel and powers on, then the fight begins.</summary>
        public void Wake()
        {
            if (Current != State.Dormant) return;
            StartCoroutine(Waking());
        }

        private IEnumerator Waking()
        {
            Current = State.Waking;
            kneel = 0f;
            ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, transform.position + Vector3.up * 1f, Vector3.up, 60, 2.2f, 4f);
            CameraShake.Add(0.4f);
            yield return new WaitForSeconds(2.4f);
            Health.Invulnerable = false;
            Current = State.Fighting;
            nextAttack = Time.time + 1.5f;
            PhaseStarted?.Invoke(Phase);
        }

        /// <summary>The task is done: the shield breaks, the mech staggers, and the next phase starts.</summary>
        public void BreakShield()
        {
            if (!Shielded) return;
            StopAttack();
            Shielded = false;
            Health.Invulnerable = false;
            shield.gameObject.SetActive(false);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, shield.position, Vector3.up, 120, 0.25f, 14f);
            CameraShake.Add(0.5f);
            StartCoroutine(Stagger());
        }

        private IEnumerator Stagger()
        {
            Current = State.Staggered;
            kneel = 0.6f;
            yield return new WaitForSeconds(3f);
            kneel = 0f;
            Phase = Mathf.Min(3, Phase + 1);
            Current = State.Fighting;
            nextAttack = Time.time + 1.2f;
            PhaseStarted?.Invoke(Phase);
        }

        /// <summary>
        /// A retry after the player dies: back to the start of <paramref name="phase"/> (its health, no shield, at home,
        /// hazards cleared). It waits, kneeling, until <see cref="Resume"/>.
        /// </summary>
        public void ResetToPhase(int phase)
        {
            StopAllCoroutines();
            StopAttack();
            foreach (FireZone fire in fires)
                if (fire != null) Destroy(fire.gameObject);
            fires.Clear();
            IcePatch.ClearAll();
            Phase = Mathf.Clamp(phase, 1, 3);
            Shielded = false;
            shield.gameObject.SetActive(false);
            SetHealthFraction(PhaseStart);
            Health.Invulnerable = true; // until it resumes
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.Warp(Home);
                agent.isStopped = true;
            }
            shoveRemaining = 0f;
            Current = State.Staggered;
            kneel = 0.4f;
        }

        /// <summary>Starts fighting again after <see cref="ResetToPhase"/>.</summary>
        public void Resume()
        {
            if (Current == State.Defeated) return;
            Health.Invulnerable = false;
            kneel = 0f;
            Current = State.Fighting;
            nextAttack = Time.time + 2.5f;
            PhaseStarted?.Invoke(Phase);
        }

        // ---------- Damage ----------

        private void OnDamaged(Health health, float amount, GameObject source)
        {
            if (settingHealth) return;
            lastBodyHitFrame = Time.frameCount;
            if (Phase > shieldAt.Length || Shielded) return;
            float threshold = shieldAt[Phase - 1];
            if (health.Normalized > threshold) return;
            // The threshold holds: the rest of the hit is lost, and the shield goes up.
            SetHealthFraction(threshold);
            RaiseShield();
        }

        private void RaiseShield()
        {
            Shielded = true;
            Health.Invulnerable = true;
            shield.gameObject.SetActive(true);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, shield.position, Vector3.up, 80, 0.2f, 8f);
            ShieldRaised?.Invoke(Phase);
        }

        private void SetHealthFraction(float fraction)
        {
            settingHealth = true;
            bool wasInvulnerable = Health.Invulnerable;
            Health.Invulnerable = false;
            Health.Restore();
            if (fraction < 0.999f) Health.TakeDamage(Health.Max * (1f - fraction), null);
            Health.Invulnerable = wasInvulnerable;
            settingHealth = false;
        }

        private void OnDied(Health _)
        {
            StopAllCoroutines();
            StopAttack();
            Current = State.Defeated;
            Shielded = false;
            shield.gameObject.SetActive(false);
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            kneel = 1f;
            ParticleBurst.Explosion(Rig.Reactor.position, 6f);
            CameraShake.Add(0.8f);
            Beaten?.Invoke();
        }

        // The Reactor Core on the chest: a weak spot that passes hits on to the body, bigger.
        private void AddWeakSpot()
        {
            var sphere = Rig.Reactor.gameObject.AddComponent<SphereCollider>();
            sphere.radius = 1.1f;
            var core = Rig.Reactor.gameObject.AddComponent<Health>();
            core.SetMax(100000f);
            core.Damaged += (h, amount, source) =>
            {
                h.Restore();
                if (Current == State.Defeated) return;
                // A swing that also hit the body this frame only adds the bonus.
                float bonus = lastBodyHitFrame == Time.frameCount ? reactorMultiplier - 1f : reactorMultiplier;
                Health.TakeDamage(amount * bonus, source);
            };
        }

        private void AddLimbColliders()
        {
            var body = gameObject.AddComponent<CapsuleCollider>();
            body.radius = 2.4f;
            body.height = 7.5f;
            body.center = new Vector3(0f, MechModel.HipHeight + 2.6f, 0f);
            foreach (Transform knee in new[] { Rig.KneeL, Rig.KneeR })
            {
                var shin = knee.gameObject.AddComponent<CapsuleCollider>();
                shin.radius = 0.8f;
                shin.height = MechModel.ShinLength + MechModel.ThighLength;
                shin.center = new Vector3(0f, (MechModel.ThighLength - MechModel.ShinLength) * 0.5f, 0f);
            }
        }

        // ---------- The Drunk Driving car ----------

        public VehicleResponse OnVehicleHit(VehicleHit hit) => VehicleResponse.Explode; // far too big to run over

        public void OnCarApproaching(DrunkCar car, float secondsAway) { }

        public float OnCarBlast(CarBlastInfo blast) => Shielded || Current == State.Dormant ? 0f : blast.Damage * (blast.Rammed ? 1.5f : 1f); // hurts; never stuns

        // ---------- Thinking ----------

        private float Tempo => Shielded ? tempo[Phase - 1] * shieldedTempo : tempo[Mathf.Clamp(Phase - 1, 0, tempo.Length - 1)];

        private void Think(float dt)
        {
            if (player == null || player.IsRespawning || playerHealth == null || playerHealth.IsDead) { Halt(); return; }
            if (attack != null) return;
            Transform target = PickTarget(false);
            float distance = Flat(target.position - transform.position);
            if (distance < stompRadius - 1f) { if (closeSince < 0f) closeSince = Time.time; }
            else closeSince = -1f;

            if (Time.time >= nextAttack)
            {
                IEnumerator next = ChooseAttack(target, distance);
                if (next != null)
                {
                    Halt();
                    attack = StartCoroutine(Run(next));
                    return;
                }
            }
            // Keep at a comfortable range, facing the target.
            Face(target.position, dt);
            if (!agent.enabled || !agent.isOnNavMesh) return;
            if (Mathf.Abs(distance - preferredRange) > 3f)
            {
                Vector3 away = transform.position - target.position;
                away.y = 0f;
                Vector3 spot = target.position + (away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward) * preferredRange;
                if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                {
                    agent.isStopped = false;
                    agent.speed = walkSpeed * Mathf.Lerp(1f, Tempo, 0.5f);
                    agent.SetDestination(hit.position);
                }
            }
            else Halt();
        }

        // Phase 1: sweep, charge, lunge, rocket barrage (and the stomp up close). Phase 2 adds the tidal wave and the
        // scrap magnet; phase 3 the napalm flyover. It never uses the same attack twice running.
        private IEnumerator ChooseAttack(Transform target, float distance)
        {
            float now = Time.time;
            if (closeSince >= 0f && now - closeSince > 0.9f && now >= stompReady) return Named("Stomp");
            var options = new List<string>();
            void Offer(string name, float ready, bool fits) { if (now >= ready && fits && name != lastAttack) options.Add(name); }
            Offer("Freezing sweep", sweepReady, distance < sweepRange + 4f);
            Offer("Thruster charge", chargeReady, distance > 9f);
            Offer("Thruster lunge", lungeReady, distance > 8f && distance < lungeMaxRange);
            Offer("Rocket barrage", barrageReady, true);
            if (Phase >= 2)
            {
                Offer("Coolant tidal wave", waveReady, distance < waveReach - 6f);
                Offer("Scrap magnet", magnetReady, distance > 10f && distance < magnetRange);
            }
            if (Phase >= 3) Offer("Napalm flyover", napalmReady, true);
            if (options.Count == 0) return now >= stompReady && distance < stompRadius + 2f ? Named("Stomp") : null;
            return Named(options[Random.Range(0, options.Count)]);
        }

        private IEnumerator Named(string attack)
        {
            lastAttack = attack;
            Transform target = PickTarget(false);
            return attack switch
            {
                "Freezing sweep" => Sweep(target),
                "Thruster charge" => Charge(target),
                "Thruster lunge" => Lunge(),
                "Rocket barrage" => Barrage(),
                "Coolant tidal wave" => TidalWave(),
                "Scrap magnet" => Magnet(),
                "Napalm flyover" => Napalm(),
                _ => Stomp()
            };
        }

        /// <summary>Starts a named attack straight away (for tests). Returns false if it's busy or not fighting.</summary>
        public bool ForceAttack(string name)
        {
            if (Current != State.Fighting || attack != null) return false;
            Halt();
            attack = StartCoroutine(Run(Named(name)));
            return true;
        }

        // While Eugene's crew is in, some attacks go after them instead.
        private Transform PickTarget(bool forAttack)
        {
            if (forAttack && AllyCrab.Live.Count > 0 && Random.value < 0.45f)
                return AllyCrab.Live[Random.Range(0, AllyCrab.Live.Count)].transform;
            return player.transform;
        }

        private IEnumerator Run(IEnumerator body)
        {
            yield return body;
            attack = null;
            AttackName = "";
            nextAttack = Time.time + attackGap / Tempo;
        }

        // Everything in progress stops: the attack, and any missiles or waves it already sent (they live in their own
        // coroutines).
        private void StopAttack()
        {
            StopAllCoroutines();
            attack = null;
            AttackName = "";
            foreach (Telegraph t in telegraphs)
                if (t != null) t.Clear();
            telegraphs.Clear();
            if (beam != null) beam.gameObject.SetActive(false);
            if (airborne) Settle(Ground(transform.position));
            foreach (GameObject temporary in temporaries)
                if (temporary != null) Destroy(temporary);
            temporaries.Clear();
            reactorBoost = 0f;
            if (muzzleLight != null) muzzleLight.intensity = 0f;
            crouch = lean = aim = liftR = 0f;
            thrusterHeat = 0f;
        }

        private Telegraph Paint(Telegraph.Shape shape, Vector3 origin, Quaternion facing, Vector2 size, Color color)
        {
            Telegraph t = Telegraph.Spawn(shape, origin, facing, size, color);
            telegraphs.Add(t);
            return t;
        }

        private void Unpaint(Telegraph t)
        {
            telegraphs.Remove(t);
            if (t != null) t.Clear();
        }

        // ---------- Cooling System: the freezing sweep ----------

        private IEnumerator Sweep(Transform target)
        {
            AttackName = "Freezing sweep";
            target = PickTarget(true);
            Vector3 origin = Ground(transform.position);
            Vector3 toward = Flat3(target.position - origin);
            Quaternion facing = Quaternion.LookRotation(toward.sqrMagnitude > 0.01f ? toward : transform.forward);
            float half = sweepHalfAngle + (Phase - 1) * 10f;
            Telegraph wedge = Paint(Telegraph.Shape.Wedge, origin, facing, new Vector2(sweepRange, half), FinalePalette.FrostWarning);
            float windup = sweepWindup / Tempo;
            for (float t = 0f; t < windup; t += Time.deltaTime)
            {
                Face(origin + facing * Vector3.forward * 10f, Time.deltaTime, 3f);
                wedge.Progress = t / windup;
                aim = 1f;
                muzzleLight.intensity = Mathf.Lerp(0f, 6f, t / windup);
                if (Random.value < 0.2f) ParticleBurst.Spawn(ParticleBurst.Kind.Mist, Rig.CannonMuzzle.position, Rig.CannonMuzzle.forward, 4, 0.5f, 2f);
                yield return null;
            }
            Unpaint(wedge);

            // Sweep the beam across the wedge, left to right or right to left.
            float from = Random.value < 0.5f ? -half : half;
            float duration = sweepTime / Mathf.Lerp(1f, Tempo, 0.5f);
            float nextTick = 0f, nextIce = 0f;
            Transform beamTransform = Beam();
            beamTransform.gameObject.SetActive(true);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float angle = Mathf.Lerp(from, -from, t / duration);
                Vector3 direction = facing * Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Face(origin + direction * 10f, Time.deltaTime, 8f);
                Vector3 muzzle = Rig.CannonMuzzle.position;
                Vector3 end = origin + direction * sweepRange + Vector3.up * 1.1f;
                end = Clip(muzzle, end, out bool blocked);
                PlaceBeam(beamTransform, muzzle, end);
                if (Random.value < 0.5f) ParticleBurst.Spawn(ParticleBurst.Kind.Mist, end, Vector3.up, 6, 0.9f, 3f);
                if (Time.time >= nextTick)
                {
                    nextTick = Time.time + sweepTick;
                    HitAlongBeam(muzzle, origin, end, sweepDamage);
                }
                if (Time.time >= nextIce)
                {
                    nextIce = Time.time + 0.42f;
                    IcePatch.Spawn(Ground(end), iceRadius, iceLife, iceSlow);
                }
                yield return null;
            }
            beamTransform.gameObject.SetActive(false);
            muzzleLight.intensity = 0f;
            aim = 0f;
            sweepReady = Time.time + sweepCooldown / Tempo;
            yield return new WaitForSeconds(0.5f);
        }

        // The jet floods the ground along its line, from the mech's feet to where it lands: the player (or a crab)
        // standing on that line is hit, unless something solid is between them and the cannon.
        private void HitAlongBeam(Vector3 muzzle, Vector3 groundFrom, Vector3 end, float damage)
        {
            Vector3 a = Flat3(groundFrom), b = Flat3(end);
            if (player != null && !player.IsRespawning)
            {
                Vector3 p = player.transform.position;
                if (DistanceToSegment(Flat3(p), a, b) < sweepWidth && p.y - groundFrom.y < 3.5f && !Covered(muzzle, p + Vector3.up * 0.9f))
                {
                    playerHealth.TakeDamage(damage, gameObject);
                    Shove(b - a, 0.35f);
                }
            }
            foreach (AllyCrab crab in AllyCrab.Live)
                if (crab != null && DistanceToSegment(Flat3(crab.transform.position), a, b) < sweepWidth + 0.5f) crab.Knock(b - a);
        }

        private bool Covered(Vector3 from, Vector3 to)
        {
            Clip(from, to, out bool blocked);
            return blocked;
        }

        private Transform Beam()
        {
            if (beam != null) return beam;
            beam = new GameObject("Freezing Beam").transform;
            MudArt.Part(beam, "Jet", Vector3.zero, Quaternion.identity, "finale_beam", k => { k.Smooth = true; k.Rod(Vector3.zero, Vector3.forward, 0.5f, 10, FinalePalette.Coolant, 1.5f, 0.75f); }, false);
            MudArt.Part(beam, "Core", Vector3.zero, Quaternion.identity, "finale_beamcore", k => { k.Smooth = true; k.Rod(Vector3.zero, Vector3.forward, 0.22f, 8, new Color(0.75f, 0.95f, 1f), 2f, 0.3f); }, false);
            return beam;
        }

        private static void PlaceBeam(Transform beam, Vector3 from, Vector3 to)
        {
            Vector3 along = to - from;
            beam.position = from;
            beam.rotation = Quaternion.LookRotation(along.sqrMagnitude > 0.001f ? along : Vector3.forward);
            beam.localScale = new Vector3(1f + Mathf.Sin(Time.time * 40f) * 0.08f, 1f, along.magnitude);
        }

        // ---------- Engine Core: the thruster charge ----------

        private IEnumerator Charge(Transform target)
        {
            AttackName = "Thruster charge";
            target = PickTarget(true);
            Vector3 start = transform.position;
            Vector3 toward = Flat3(target.position - start);
            if (toward.sqrMagnitude < 0.01f) toward = transform.forward;
            Vector3 direction = toward.normalized;
            float length = Mathf.Min(toward.magnitude + 8f, chargeMaxLength);
            Vector3 end = start + direction * length;
            if (agent.enabled && NavMesh.Raycast(start, end, out NavMeshHit edge, NavMesh.AllAreas)) end = edge.position;
            length = Flat(end - start);
            Telegraph strip = Paint(Telegraph.Shape.Strip, Ground(start), Quaternion.LookRotation(direction), new Vector2(chargeWidth, length + 2f), FinalePalette.Warning);
            float windup = chargeWindup / Tempo;
            for (float t = 0f; t < windup; t += Time.deltaTime)
            {
                Face(end, Time.deltaTime, 6f);
                strip.Progress = t / windup;
                crouch = 1f;
                lean = 0.6f;
                thrusterHeat = Mathf.Lerp(0.2f, 0.7f, t / windup);
                yield return null;
            }
            Unpaint(strip);

            // The dash: fire roars out of the thrusters and it scrapes along the strip, leaving burning ground.
            thrusterHeat = 1f;
            lean = 1f;
            CameraShake.Add(0.25f);
            bool hitPlayer = false;
            float travelled = 0f, nextFire = 0f;
            while (travelled < length)
            {
                float step = Mathf.Min(chargeSpeed * Time.deltaTime, length - travelled);
                if (agent.enabled && agent.isOnNavMesh) agent.Move(direction * step);
                else transform.position += direction * step;
                travelled += step;
                if (travelled >= nextFire)
                {
                    nextFire = travelled + 2.6f;
                    FireZone fire = FireZone.Spawn(fireTrail, Ground(transform.position - direction * 2.5f), 2.8f, gameObject, transform, fireTrail.duration);
                    if (fire != null) fires.Add(fire);
                    ParticleBurst.Spawn(ParticleBurst.Kind.Sand, transform.position, -direction + Vector3.up, 18, 0.6f, 6f);
                }
                if (!hitPlayer && player != null && !player.IsRespawning && Flat(player.transform.position - transform.position) < chargeWidth * 0.5f + 1.2f)
                {
                    hitPlayer = true;
                    playerHealth.TakeDamage(chargeDamage, gameObject);
                    Vector3 side = Vector3.Cross(Vector3.up, direction);
                    if (Vector3.Dot(player.transform.position - transform.position, side) < 0f) side = -side;
                    Shove(direction + side, chargeShove);
                    CameraShake.Add(0.5f);
                }
                foreach (AllyCrab crab in AllyCrab.Live)
                    if (crab != null && Flat(crab.transform.position - transform.position) < chargeWidth * 0.5f + 1f) crab.Knock(direction);
                yield return null;
            }
            fires.RemoveAll(f => f == null);
            CameraShake.Add(0.35f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, transform.position + direction * 2f, Vector3.up, 40, 1.5f, 3f);
            thrusterHeat = 0.15f;
            lean = 0f;
            yield return new WaitForSeconds(0.9f / Tempo);
            crouch = 0f;
            chargeReady = Time.time + chargeCooldown / Tempo;
        }

        // ---------- Stomp ----------

        private IEnumerator Stomp()
        {
            AttackName = "Stomp";
            Vector3 centre = Ground(transform.position + transform.forward * 1.5f);
            Telegraph disc = Paint(Telegraph.Shape.Disc, centre, Quaternion.identity, new Vector2(stompRadius, 0f), FinalePalette.Warning);
            float windup = stompWindup / Tempo;
            for (float t = 0f; t < windup; t += Time.deltaTime)
            {
                disc.Progress = t / windup;
                liftR = Mathf.SmoothStep(0f, 1f, t / windup);
                yield return null;
            }
            Unpaint(disc);
            liftR = 0f;
            CameraShake.Add(0.6f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, centre, Vector3.up, 90, 0.8f, 9f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, centre, Vector3.up, 30, 2f, 3f);
            if (player != null && !player.IsRespawning)
            {
                float distance = Flat(player.transform.position - centre);
                if (distance < stompRadius)
                {
                    playerHealth.TakeDamage(stompDamage * Mathf.Lerp(1f, 0.5f, distance / stompRadius), gameObject);
                    Shove(player.transform.position - centre, stompShove);
                }
            }
            foreach (AllyCrab crab in AllyCrab.Live)
                if (crab != null && Flat(crab.transform.position - centre) < stompRadius) crab.Knock(crab.transform.position - centre);
            closeSince = -1f;
            stompReady = Time.time + stompCooldown / Tempo;
            yield return new WaitForSeconds(0.6f);
        }

        // ---------- Engine Core: the thruster lunge ----------

        // The back engines roar and it launches itself at you: a ring on the ground marks where it will land (it
        // follows you for most of the wind-up, then locks), and the landing blasts everything round it and lights a
        // ring of fire.
        private IEnumerator Lunge()
        {
            AttackName = "Thruster lunge";
            Transform target = PickTarget(true);
            Vector3 land = ClampArena(Ground(Lead(target, 0.45f)));
            Telegraph ring = Paint(Telegraph.Shape.Disc, land, Quaternion.identity, new Vector2(lungeRadius, 0f), FinalePalette.Warning);
            float windup = lungeWindup / Tempo;
            for (float t = 0f; t < windup; t += Time.deltaTime)
            {
                if (t < windup * 0.65f)
                {
                    land = ClampArena(Ground(Lead(target, 0.45f)));
                    ring.transform.position = land + Vector3.up * 0.07f;
                }
                ring.Progress = t / windup;
                Face(land, Time.deltaTime, 6f);
                crouch = 1f;
                thrusterHeat = Mathf.Lerp(0.3f, 1f, t / windup);
                yield return null;
            }
            yield return Leap(land, lungeAir / Mathf.Lerp(1f, Tempo, 0.5f), lungeHeight);
            Unpaint(ring);
            Blast(land, lungeRadius, lungeDamage, lungeShove);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f + Random.Range(0f, 0.5f);
                Burn(fireTrail, land + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 5.5f, 2.6f, 14f);
            }
            thrusterHeat = 0.15f;
            yield return new WaitForSeconds(0.5f / Tempo);
            crouch = 0f;
            lungeReady = Time.time + lungeCooldown / Tempo;
        }

        // Off the ground in an arc to a point (the NavMeshAgent is off while airborne), then back on the NavMesh.
        private IEnumerator Leap(Vector3 land, float seconds, float height)
        {
            airborne = true;
            if (agent.enabled) agent.enabled = false;
            Vector3 from = transform.position;
            lean = 0.5f;
            crouch = 0.4f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                transform.position = Vector3.Lerp(from, land, Mathf.SmoothStep(0f, 1f, k)) + Vector3.up * Mathf.Sin(k * Mathf.PI) * height;
                if (Random.value < 0.4f) ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, Rig.ThrusterL.position, Vector3.down, 6, 1f, 3f);
                yield return null;
            }
            Settle(land);
            lean = 0f;
        }

        // Back on the NavMesh after a leap or a flight: the nearest walkable spot, or home if there's none near
        // (it must never be left with its agent off, or it could no longer move).
        private void Settle(Vector3 at)
        {
            Vector3 spot = NavMesh.SamplePosition(at, out NavMeshHit hit, 20f, NavMesh.AllAreas) ? hit.position : Home;
            transform.position = spot;
            agent.enabled = true;
            if (agent.isOnNavMesh || agent.Warp(spot)) agent.isStopped = true;
            airborne = false;
        }

        // ---------- Flight Control Module: the rocket barrage ----------

        // The shoulder pods fire a volley of missiles that rain down in big circles round you; then another where you
        // went. Each blast leaves a burning crater.
        private IEnumerator Barrage()
        {
            AttackName = "Rocket barrage";
            for (int volley = 0; volley < barrageVolleys; volley++)
            {
                Transform target = PickTarget(true);
                crouch = 0.3f;
                for (int i = 0; i < barrageMissiles; i++)
                {
                    Vector3 spot = i == 0 ? Lead(target, barrageWarning * 0.6f) : target.position + Flat3(Random.insideUnitSphere) * barrageSpread;
                    spot = ClampArena(Ground(spot));
                    Transform pod = i % 2 == 0 ? Rig.ShoulderL : Rig.ShoulderR;
                    StartCoroutine(Missile(pod.position + Vector3.up * 1.6f - transform.forward * 0.8f, spot, barrageWarning / Mathf.Lerp(1f, Tempo, 0.5f)));
                    ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, pod.position + Vector3.up * 1.5f, Vector3.up, 6, 0.8f, 3f);
                    yield return new WaitForSeconds(0.08f);
                }
                yield return new WaitForSeconds(1.3f / Tempo);
            }
            crouch = 0f;
            barrageReady = Time.time + barrageCooldown / Tempo;
        }

        private IEnumerator Missile(Vector3 from, Vector3 to, float flight)
        {
            Telegraph circle = Paint(Telegraph.Shape.Disc, to, Quaternion.identity, new Vector2(barrageRadius, 0f), FinalePalette.Warning);
            Transform rocket = MudArt.Part(null, "Missile", from, Quaternion.identity, "finale_missile", k =>
            {
                k.Prism(new Vector3(0f, -0.4f, 0f), Quaternion.identity, 0.14f, 0.14f, 0.7f, 8, FinalePalette.Steel);
                k.Cone(new Vector3(0f, 0.3f, 0f), Quaternion.identity, 0.14f, 0.3f, 8, FinalePalette.Trim);
                k.Prism(new Vector3(0f, -0.55f, 0f), Quaternion.identity, 0.1f, 0.12f, 0.12f, 8, FinalePalette.EngineGlow, 2f);
            }, false);
            temporaries.Add(rocket.gameObject);
            Vector3 peak = Vector3.Lerp(from, to, 0.4f) + Vector3.up * 14f;
            Vector3 last = from;
            for (float t = 0f; t < flight; t += Time.deltaTime)
            {
                float k = t / flight;
                if (circle != null) circle.Progress = k;
                Vector3 p = Vector3.Lerp(Vector3.Lerp(from, peak, k), Vector3.Lerp(peak, to, k), k);
                if (rocket != null)
                {
                    rocket.position = p;
                    if ((p - last).sqrMagnitude > 0.0001f) rocket.rotation = Quaternion.FromToRotation(Vector3.up, p - last);
                }
                last = p;
                if (Random.value < 0.5f) ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, p, Vector3.up, 2, 0.5f, 0.5f);
                yield return null;
            }
            if (rocket != null) Destroy(rocket.gameObject);
            Unpaint(circle);
            ParticleBurst.Explosion(to, barrageRadius * 0.7f);
            CameraShake.Add(0.15f);
            Blast(to, barrageRadius, barrageDamage, 3f);
            Burn(craterFire, to, 2.2f, craterFire.duration);
        }

        // ---------- Cooling System: the tidal wave ----------

        // It slams the cannon into the ground and a wall of freezing coolant races out across the whole arena. Jump it.
        // Phase 3 sends two, close together.
        private IEnumerator TidalWave()
        {
            AttackName = "Coolant tidal wave";
            Vector3 centre = Ground(transform.position);
            Telegraph warning = Paint(Telegraph.Shape.Disc, centre, Quaternion.identity, new Vector2(6f, 0f), FinalePalette.FrostWarning);
            float windup = waveWindup / Tempo;
            for (float t = 0f; t < windup; t += Time.deltaTime)
            {
                warning.Progress = t / windup;
                aim = 0.5f;
                crouch = t / windup;
                yield return null;
            }
            Unpaint(warning);
            aim = 0f;
            CameraShake.Add(0.5f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Water, centre, Vector3.up, 120, 0.5f, 12f);
            int waves = Phase >= 3 ? 2 : 1;
            for (int i = 0; i < waves; i++)
            {
                StartCoroutine(Wave(centre));
                yield return new WaitForSeconds(0.9f);
            }
            crouch = 0f;
            yield return new WaitForSeconds(waveReach / waveSpeed * 0.5f);
            waveReady = Time.time + waveCooldown / Tempo;
        }

        private IEnumerator Wave(Vector3 centre)
        {
            const int segments = 72;
            var ring = new Transform[segments];
            var root = new GameObject("Coolant Wave").transform;
            temporaries.Add(root.gameObject);
            root.position = centre;
            for (int i = 0; i < segments; i++)
            {
                ring[i] = MudArt.Part(root, "Crest", Vector3.zero, Quaternion.identity, "finale_wave", k =>
                {
                    k.Box(new Vector3(0f, 0.45f, 0f), new Vector3(1f, 0.9f, 0.7f), new Color(0.55f, 0.88f, 1f), 1.1f);
                    k.Box(new Vector3(0f, 1.05f, -0.1f), new Vector3(1f, 0.35f, 0.4f), new Color(0.85f, 0.97f, 1f), 1.4f);
                }, false);
            }
            bool hitPlayer = false;
            for (float r = 1f; r < waveReach; r += waveSpeed * Time.deltaTime)
            {
                if (root == null) yield break;
                float width = 2f * Mathf.PI * r / segments * 1.08f;
                for (int i = 0; i < segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    Vector3 offset = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    ring[i].position = centre + offset + Vector3.up * 0.05f; // the arena is level
                    ring[i].rotation = Quaternion.LookRotation(offset);
                    ring[i].localScale = new Vector3(width, 1f, 1f);
                }
                if (!hitPlayer && player != null && !player.IsRespawning && playerBody != null)
                {
                    float d = Flat(player.transform.position - centre);
                    if (Mathf.Abs(d - r) < 1.1f && playerBody.isGrounded)
                    {
                        hitPlayer = true;
                        playerHealth.TakeDamage(waveDamage, gameObject);
                        Shove(player.transform.position - centre, 6f);
                        IcePatch.Spawn(Ground(player.transform.position), 2f, 2.5f, 0.6f);
                    }
                }
                foreach (AllyCrab crab in AllyCrab.Live)
                    if (crab != null && Mathf.Abs(Flat(crab.transform.position - centre) - r) < 1.1f) crab.Knock(crab.transform.position - centre);
                yield return null;
            }
            Destroy(root.gameObject);
        }

        // ---------- Reactor: the scrap magnet ----------

        // The chest flares and the reactor drags everything toward it for a couple of seconds; sprint against it. Anyone
        // still close when it lets go gets a double-fisted ground slam.
        private IEnumerator Magnet()
        {
            AttackName = "Scrap magnet";
            Halt();
            float duration = magnetTime;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                lean = -0.35f;
                reactorBoost = 1f;
                if (player != null && !player.IsRespawning && playerBody != null && playerBody.enabled)
                {
                    Vector3 pull = transform.position - player.transform.position;
                    pull.y = 0f;
                    if (pull.magnitude < magnetRange && pull.magnitude > 4f) playerBody.Move(pull.normalized * magnetPull * Time.deltaTime);
                    if (Random.value < 0.6f) ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, player.transform.position + Vector3.up, pull.normalized, 4, 0.12f, 9f);
                }
                foreach (AllyCrab crab in AllyCrab.Live)
                    if (crab != null && Flat(crab.transform.position - transform.position) < magnetRange) crab.Knock(transform.position - crab.transform.position);
                yield return null;
            }
            lean = 0f;
            reactorBoost = 0f;
            if (player != null && Flat(player.transform.position - transform.position) < slamRadius + 1f)
            {
                Vector3 centre = Ground(transform.position + transform.forward * 2f);
                Telegraph disc = Paint(Telegraph.Shape.Disc, centre, Quaternion.identity, new Vector2(slamRadius, 0f), FinalePalette.Warning);
                for (float t = 0f; t < 0.55f; t += Time.deltaTime)
                {
                    disc.Progress = t / 0.55f;
                    crouch = 0.5f;
                    liftR = t / 0.55f;
                    yield return null;
                }
                Unpaint(disc);
                liftR = 0f;
                crouch = 0f;
                Blast(centre, slamRadius, slamDamage, 8f);
            }
            magnetReady = Time.time + magnetCooldown / Tempo;
            yield return new WaitForSeconds(0.4f);
        }

        // ---------- Engine Core: the napalm flyover ----------

        // It blasts into the air and strafes the arena along a line through you, carpeting it with fire that burns for
        // a long time, so the safe ground keeps shrinking.
        private IEnumerator Napalm()
        {
            AttackName = "Napalm flyover";
            Transform target = PickTarget(true);
            Vector3 start = transform.position;
            Vector3 direction = Flat3(target.position - start);
            direction = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
            Vector3 end = ClampArena(start + direction * napalmLength);
            float length = Flat(end - start);
            Telegraph strip = Paint(Telegraph.Shape.Strip, Ground(start), Quaternion.LookRotation(direction), new Vector2(napalmWidth, length), FinalePalette.Warning);
            for (float t = 0f; t < 1.3f / Tempo; t += Time.deltaTime)
            {
                strip.Progress = t * Tempo / 1.3f;
                Face(end, Time.deltaTime, 6f);
                crouch = 1f;
                thrusterHeat = 0.8f;
                yield return null;
            }
            Unpaint(strip);
            airborne = true;
            if (agent.enabled) agent.enabled = false;
            crouch = 0f;
            lean = 0.9f;
            thrusterHeat = 1f;
            Vector3 ground = transform.position;
            for (float t = 0f; t < 0.7f; t += Time.deltaTime)
            {
                transform.position = ground + Vector3.up * Mathf.SmoothStep(0f, napalmHeight, t / 0.7f);
                yield return null;
            }
            float travelled = 0f, nextDrop = 0f;
            while (travelled < length)
            {
                float step = napalmSpeed * Time.deltaTime;
                travelled += step;
                Vector3 over = start + direction * Mathf.Min(travelled, length);
                transform.position = new Vector3(over.x, Ground(over).y + napalmHeight, over.z);
                if (travelled >= nextDrop)
                {
                    nextDrop = travelled + 2.6f;
                    Burn(napalm, over, 2.8f, napalm.duration);
                    ParticleBurst.Spawn(ParticleBurst.Kind.Fireball, transform.position - Vector3.up * 2f, Vector3.down, 10, 0.8f, 6f);
                }
                yield return null;
            }
            Vector3 landing = Ground(end);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(transform.position, landing, t / 0.6f);
                yield return null;
            }
            Settle(landing);
            Blast(landing, 7f, 25f, 5f);
            lean = 0f;
            thrusterHeat = 0.15f;
            napalmReady = Time.time + napalmCooldown / Tempo;
            yield return new WaitForSeconds(0.6f);
        }

        // ---------- Shared bits for the new attacks ----------

        // Where a target will be in a moment (it leads a running player).
        private Vector3 Lead(Transform target, float seconds)
        {
            Vector3 velocity = Vector3.zero;
            if (player != null && target == player.transform && playerBody != null) velocity = playerBody.velocity;
            velocity.y = 0f;
            return target.position + velocity * seconds;
        }

        private Vector3 ClampArena(Vector3 point)
        {
            Vector3 offset = Flat3(point - arenaCentre);
            if (offset.magnitude > arenaRadius - 3f) offset = offset.normalized * (arenaRadius - 3f);
            Vector3 p = arenaCentre + offset;
            return new Vector3(p.x, point.y, p.z);
        }

        // Damage falling off to 60% at the edge, a shove, a dust ring, and the crew bowled over.
        private void Blast(Vector3 centre, float radius, float damage, float shove)
        {
            CameraShake.Add(0.45f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, centre, Vector3.up, 90, 0.8f, 10f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, centre, Vector3.up, 30, 2f, 3f);
            if (player != null && !player.IsRespawning)
            {
                float distance = Flat(player.transform.position - centre);
                if (distance < radius && player.transform.position.y - centre.y < 4f)
                {
                    playerHealth.TakeDamage(damage * Mathf.Lerp(1f, 0.6f, distance / radius), gameObject);
                    Shove(player.transform.position - centre, shove);
                }
            }
            foreach (AllyCrab crab in AllyCrab.Live)
                if (crab != null && Flat(crab.transform.position - centre) < radius) crab.Knock(crab.transform.position - centre);
        }

        private void Burn(FireStyle style, Vector3 at, float radius, float seconds)
        {
            FireZone fire = FireZone.Spawn(style, Ground(at), radius, gameObject, transform, seconds);
            if (fire != null) fires.Add(fire);
            fires.RemoveAll(f => f == null);
        }

        // ---------- Helpers ----------

        private void Halt()
        {
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
        }

        private void Face(Vector3 point, float dt, float speedScale = 1f)
        {
            Vector3 toward = Flat3(point - transform.position);
            if (toward.sqrMagnitude < 0.01f) return;
            Quaternion want = Quaternion.LookRotation(toward);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnRate * speedScale * dt);
        }

        private void Shove(Vector3 direction, float distance)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f || playerBody == null) return;
            shoveDirection = direction.normalized;
            shoveRemaining = distance;
        }

        private void UpdateShove(float dt)
        {
            if (shoveRemaining <= 0f || playerBody == null || !playerBody.enabled) return;
            float step = Mathf.Min(shoveRemaining, 12f * dt);
            playerBody.Move(shoveDirection * step);
            shoveRemaining -= step;
        }

        // The first solid thing between two points (not the mech, not anything alive), or the end point.
        private Vector3 Clip(Vector3 from, Vector3 to, out bool blocked)
        {
            blocked = false;
            Vector3 along = to - from;
            float best = along.magnitude;
            foreach (RaycastHit hit in Physics.RaycastAll(from, along.normalized, best, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.collider is CharacterController) continue;
                if (hit.collider.GetComponentInParent<Health>() != null || hit.collider.GetComponentInParent<AllyCrab>() != null) continue;
                if (hit.distance < best)
                {
                    best = hit.distance;
                    blocked = true;
                }
            }
            return from + along.normalized * best;
        }

        private Vector3 Ground(Vector3 point)
        {
            foreach (RaycastHit hit in Physics.RaycastAll(point + Vector3.up * 6f, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.collider is CharacterController || hit.collider.GetComponentInParent<Health>() != null) continue;
                if (hit.collider.GetComponentInParent<AllyCrab>() != null) continue;
                return hit.point;
            }
            return new Vector3(point.x, Home.y, point.z);
        }

        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
        private static Vector3 Flat3(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static Light AddLight(Transform parent, Color color, float range, float intensity)
        {
            var light = new GameObject("Glow").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        // ---------- Animation ----------

        private void Animate(float dt)
        {
            float ease = 1f - Mathf.Exp(-6f * dt);
            crouchNow = Mathf.Lerp(crouchNow, crouch, ease);
            leanNow = Mathf.Lerp(leanNow, lean, ease);
            aimNow = Mathf.Lerp(aimNow, aim, 1f - Mathf.Exp(-8f * dt));
            liftNow = Mathf.Lerp(liftNow, liftR, 1f - Mathf.Exp(-12f * dt));
            kneelNow = Mathf.Lerp(kneelNow, kneel, 1f - Mathf.Exp(-2.5f * dt));
            deadNow = Mathf.Lerp(deadNow, Current == State.Defeated ? 1f : 0f, 1f - Mathf.Exp(-1.5f * dt));

            float speed = agent.enabled ? new Vector2(agent.velocity.x, agent.velocity.z).magnitude : 0f;
            walkWeight = Mathf.Lerp(walkWeight, Mathf.Clamp01(speed / 2f), ease);
            stride += speed * dt / 4.5f * Mathf.PI * 2f;
            float swing = Mathf.Sin(stride) * 24f * walkWeight;
            float lift = Mathf.Max(0f, Mathf.Cos(stride)) * walkWeight;
            float bob = (Mathf.Abs(Mathf.Sin(stride)) * 0.25f) * walkWeight;
            float breathe = Mathf.Sin(Time.time * 1.6f) * 0.06f;

            float drop = crouchNow * 0.9f + kneelNow * 3.0f;
            Rig.Hips.localPosition = new Vector3(0f, MechModel.HipHeight - drop + bob + breathe, -kneelNow * 0.6f);
            Rig.Hips.localRotation = Quaternion.Euler(kneelNow * 8f, 0f, 0f);
            float bend = crouchNow * 30f + kneelNow * 55f;
            Rig.LegL.localRotation = Quaternion.Euler(-swing - bend, 0f, 0f);
            Rig.KneeL.localRotation = Quaternion.Euler(lift * 35f + bend * 1.9f, 0f, 0f);
            Rig.FootL.localRotation = Quaternion.Euler(-bend * 0.9f + swing * 0.3f, 0f, 0f);
            float stompLift = liftNow * 60f;
            Rig.LegR.localRotation = Quaternion.Euler(swing - bend - stompLift, 0f, 0f);
            Rig.KneeR.localRotation = Quaternion.Euler(Mathf.Max(0f, -Mathf.Cos(stride)) * walkWeight * 35f + bend * 1.9f + stompLift * 1.2f, 0f, 0f);
            Rig.FootR.localRotation = Quaternion.Euler(-bend * 0.9f - swing * 0.3f, 0f, 0f);

            // The torso leans into a charge, slumps when kneeling, and twists a little with the stride.
            waistYaw = Mathf.Sin(stride) * 4f * walkWeight;
            // Beaten, it slumps forward and to one side, arms hanging.
            Rig.Waist.localRotation = Quaternion.Euler(leanNow * 22f + kneelNow * 18f + deadNow * 22f + breathe * 20f * (1f - deadNow), waistYaw, deadNow * 14f);

            // Arms: the cannon arm comes up to aim; otherwise both swing with the walk.
            float armSwing = swing * 0.6f + deadNow * 25f;
            Rig.ShoulderL.localRotation = Quaternion.Euler(Mathf.Lerp(armSwing, -88f, aimNow) - kneelNow * 10f, 0f, Mathf.Lerp(-8f, 0f, aimNow));
            Rig.ElbowL.localRotation = Quaternion.Euler(Mathf.Lerp(-12f, 0f, aimNow), 0f, 0f);
            Rig.ShoulderR.localRotation = Quaternion.Euler(-armSwing - crouchNow * 25f - kneelNow * 10f, 0f, 8f + crouchNow * 10f);
            Rig.ElbowR.localRotation = Quaternion.Euler(-20f - crouchNow * 40f, 0f, 0f);

            // The reactor throbs (faster when hurt), the dish turns, the turbine spins with the thrusters.
            float hurt = 1f - HealthFraction;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (3f + hurt * 6f));
            bool alive = Current != State.Defeated;
            Rig.ReactorGlow.localScale = Vector3.one * (alive ? 0.92f + pulse * 0.16f : 0.6f);
            reactorLight.intensity = alive ? (Current == State.Dormant ? 0.4f : 1.6f + pulse * 1.6f + reactorBoost * 8f) : 0f;
            if (reactorBoost > 0f) Rig.ReactorGlow.localScale = Vector3.one * (1.3f + pulse * 0.3f);
            if (Rig.Dish != null) Rig.Dish.localRotation = Quaternion.Euler(0f, Time.time * 90f, 0f);
            Rig.Turbine.localRotation *= Quaternion.Euler(0f, 0f, (200f + thrusterHeat * 1400f) * dt);
            jetL.Strength = jetR.Strength = alive ? Mathf.Lerp(jetL.Strength, thrusterHeat, ease) : 0f;
            if (Shielded) shield.localScale = new Vector3(9.5f, 10.9f, 9.5f) * (1f + Mathf.Sin(Time.time * 3f) * 0.015f);
        }
    }
}
