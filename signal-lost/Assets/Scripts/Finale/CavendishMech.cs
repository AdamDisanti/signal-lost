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

        [Header("Movement")]
        [SerializeField, Min(0.5f)] private float walkSpeed = 3.4f;
        [SerializeField, Min(4f)] private float preferredRange = 16f;
        [SerializeField, Min(10f)] private float turnRate = 70f;
        [SerializeField, Min(0f)] private float attackGap = 1.8f;

        [Header("Cooling System: freezing sweep")]
        [SerializeField] private float sweepWindup = 1.7f, sweepTime = 2.3f, sweepRange = 26f, sweepHalfAngle = 40f, sweepCooldown = 7f;
        [SerializeField] private float sweepDamage = 7f, sweepTick = 0.15f, sweepWidth = 1.5f;
        [SerializeField] private float iceRadius = 2.6f, iceLife = 9f, iceSlow = 0.45f;

        [Header("Engine Core: thruster charge")]
        [SerializeField] private float chargeWindup = 1.3f, chargeSpeed = 26f, chargeMaxLength = 34f, chargeWidth = 6f, chargeCooldown = 9f;
        [SerializeField] private float chargeDamage = 26f, chargeShove = 7f;
        [SerializeField] private FireStyle fireTrail = new FireStyle { damage = 6f, interval = 0.5f, duration = 7f, smoke = 0.5f };

        [Header("Stomp")]
        [SerializeField] private float stompWindup = 0.9f, stompRadius = 8f, stompDamage = 22f, stompShove = 5f, stompCooldown = 4f;

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
        private float nextAttack, sweepReady, chargeReady, stompReady, closeSince = -1f;
        private bool settingHealth;
        private int lastBodyHitFrame = -1;

        // Pose targets, eased toward each frame.
        private float crouch, lean, aim, liftR, kneel, stride, walkWeight, thrusterHeat;
        private float crouchNow, leanNow, aimNow, liftNow, kneelNow = 1f, deadNow, waistYaw, nextSmoke;
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

        private IEnumerator ChooseAttack(Transform target, float distance)
        {
            float now = Time.time;
            if (closeSince >= 0f && now - closeSince > 1.2f && now >= stompReady) return Stomp();
            var options = new List<IEnumerator>();
            if (now >= sweepReady && distance < sweepRange + 4f) options.Add(Sweep(target));
            if (now >= chargeReady && distance > 9f) options.Add(Charge(target));
            if (options.Count == 0) return now >= stompReady && distance < stompRadius + 2f ? Stomp() : null;
            return options[Random.Range(0, options.Count)];
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

        private void StopAttack()
        {
            if (attack != null) StopCoroutine(attack);
            attack = null;
            AttackName = "";
            foreach (Telegraph t in telegraphs)
                if (t != null) t.Clear();
            telegraphs.Clear();
            if (beam != null) beam.gameObject.SetActive(false);
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
                    nextFire = travelled + 3.2f;
                    FireZone fire = FireZone.Spawn(fireTrail, Ground(transform.position - direction * 2.5f), 1.9f, gameObject, transform, fireTrail.duration);
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
            reactorLight.intensity = alive ? (Current == State.Dormant ? 0.4f : 1.6f + pulse * 1.6f) : 0f;
            if (Rig.Dish != null) Rig.Dish.localRotation = Quaternion.Euler(0f, Time.time * 90f, 0f);
            Rig.Turbine.localRotation *= Quaternion.Euler(0f, 0f, (200f + thrusterHeat * 1400f) * dt);
            jetL.Strength = jetR.Strength = alive ? Mathf.Lerp(jetL.Strength, thrusterHeat, ease) : 0f;
            if (Shielded) shield.localScale = new Vector3(9.5f, 10.9f, 9.5f) * (1f + Mathf.Sin(Time.time * 3f) * 0.015f);
        }
    }
}
