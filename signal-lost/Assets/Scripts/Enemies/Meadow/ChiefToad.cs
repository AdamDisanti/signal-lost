using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Enemies.Desert;
using SignalLost.Player;
using SignalLost.Progression;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace SignalLost.Enemies.Meadow
{
    /// <summary>
    /// The Chief Toad: a huge, crowned toad chieftain who has torn the ship's cooling system out of the wreck and
    /// strapped it round his body, coolant tubes glowing along his back and shoulders. He lives at the meadow pond but
    /// hops out to fight on dry land too: every water attack makes its own water, in the pond or out of it.
    ///
    /// Attacks:
    /// - Water beam, in two variants used in every phase (Lawson, 11-07), each with its own unmistakable windup:
    ///   - Tracking jet: a long charge in which a water orb swells in his mouth, a sighting line locks onto you, a
    ///     target ring follows you on the ground and lock-on pings speed up. Then a jet that turns after you faster
    ///     than you can run: get behind cover.
    ///   - Torrent sweep: he plants himself and a wedge on the ground shows the arc it will cover, from the side his
    ///     head turns to. Then a huge, wide torrent sweeps slowly across it. Leave the wedge or take cover.
    /// - Water blades: a swipe that slings a fan of water crescents at chest height. Dodge sideways.
    /// - Jump slam: a crouch (a ring marks the landing), a leap, and a landing that hurts up close and sends a wall of
    ///   water racing outward. Jump the wave.
    /// - Scream: the throat sac swells while a ring shows its reach (12 m), then a scream that stuns you for 1.5 s if
    ///   you're inside it and in his line of sight. Back off or duck behind a rock.
    /// Three phases (the base class's two, plus a third here):
    /// - 1 (full to 66%): the four attacks.
    /// - 2 "Coolant surge" (66% to 33%): faster; two volleys of five blades; the slam hops twice and each landing
    ///   sprays a ring of blades.
    /// - 3 "Coolant overflow" (below 33%): faster again; adds the tidal spin, a full turn with the beam skimming low
    ///   across the ground (jump it or take cover); every slam sends two waves.
    /// The glowing coolant core on his chest is a weak spot (x1.5 damage). On defeat he drops the ship's cooling
    /// system (counted for the tutorial stage, not the desert).
    /// </summary>
    public sealed class ChiefToad : DesertMiniBoss
    {
        private enum State { Approach, Recover, BeamCharge, Beam, BladeWindup, Blades, SlamCrouch, SlamAir, ScreamWindup, Scream, SpinCharge, Spin, Overflow }

        [Header("Rig")]
        [SerializeField] private Transform body;
        [SerializeField] private Transform head;
        [SerializeField] private Transform jaw;
        [SerializeField] private Transform throat;
        [Tooltip("Where the beam leaves its mouth.")]
        [SerializeField] private Transform mouth;
        [SerializeField] private Transform crown;
        [SerializeField] private Transform[] cape = new Transform[0];
        [Tooltip("Front legs (shoulders), left then right. The right one swipes the water blades.")]
        [SerializeField] private Transform[] frontLegs = new Transform[0];
        [Tooltip("Back legs (hips), left then right.")]
        [SerializeField] private Transform[] backLegs = new Transform[0];
        [SerializeField] private Transform[] backKnees = new Transform[0];
        [Tooltip("The glowing coolant segments, lit from code: brighter each phase and while charging.")]
        [SerializeField] private Renderer[] coolantGlow = new Renderer[0];
        [SerializeField] private Color coolantColor = new Color(0.25f, 0.85f, 1f);
        [Tooltip("The coolant core on its chest: a weak spot.")]
        [SerializeField] private Health coreHealth;
        [SerializeField, Min(1f)] private float coreMultiplier = 1.5f;

        [Header("Looks")]
        [Tooltip("The water blades' and splash waves' material.")]
        [SerializeField] private Material waterMaterial;
        [Tooltip("Rings on the ground warning of a landing, the scream's reach and the spin.")]
        [SerializeField] private Material warningMaterial;
        [Tooltip("The scream's shock rings and the beam's ripples.")]
        [SerializeField] private Material shriekMaterial;

        [Header("Phases")]
        [Tooltip("Phase 3 (coolant overflow) starts at or below this fraction of its health. Phase 2 is Phase Two At.")]
        [SerializeField, Range(0.05f, 0.9f)] private float phaseThreeAt = 0.33f;
        [SerializeField, Min(0f)] private float overflowTime = 1.6f;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float hopSpeed = 3.6f;
        [Tooltip("Distance it likes to fight from.")]
        [SerializeField, Min(2f)] private float preferredRange = 10f;
        [SerializeField, Min(10f)] private float turnRate = 200f;
        [Tooltip("Seconds between one attack ending and the next starting.")]
        [SerializeField, Min(0f)] private float attackGap = 0.9f;

        [Header("Water beam (both variants)")]
        [SerializeField, Min(5f)] private float beamRange = 28f;
        [Tooltip("Half-width of the thin jet (the tracking jet and the tidal spin).")]
        [SerializeField, Min(0.2f)] private float beamHitRadius = 1.5f;
        [SerializeField, Min(0.05f)] private float beamTick = 0.15f;
        [Tooltip("How far each tick pushes you along the beam.")]
        [SerializeField, Min(0f)] private float beamPush = 0.35f;
        [SerializeField, Min(0f)] private float beamCooldown = 8f;

        [Header("Tracking jet")]
        [Tooltip("Its windup: long, so there is time to find cover. Faster phases shorten it, never below 1.6 s.")]
        [SerializeField, Min(0.5f)] private float trackCharge = 2.3f;
        [SerializeField, Min(0.2f)] private float trackTime = 2.4f;
        [Tooltip("Degrees per second it turns after you: faster than you can run round him, so hide instead.")]
        [SerializeField, Min(0f)] private float trackTurnRate = 75f;
        [SerializeField, Min(0f)] private float trackDamagePerTick = 6f;

        [Header("Torrent sweep")]
        [SerializeField, Min(0.2f)] private float sweepCharge = 1.5f;
        [Tooltip("Seconds to sweep from one edge of the arc to the other: slow, so it stays on you.")]
        [SerializeField, Min(0.2f)] private float sweepTime = 3f;
        [Tooltip("Half-width of the arc it sweeps, in degrees.")]
        [SerializeField, Range(10f, 80f)] private float sweepArc = 50f;
        [Tooltip("Half-width of the torrent: much wider than the tracking jet.")]
        [SerializeField, Min(0.5f)] private float sweepHitRadius = 3.4f;
        [Tooltip("How much bigger the torrent looks than the jet.")]
        [SerializeField, Min(1f)] private float sweepScale = 2.6f;
        [SerializeField, Min(0f)] private float sweepDamagePerTick = 6f;

        [Header("Water blades")]
        [SerializeField, Min(0.1f)] private float bladeWindup = 0.55f;
        [SerializeField, Range(1, 9)] private int bladesPerVolley = 3;
        [Tooltip("Blades per volley from phase 2 (two volleys in phase 2, three in phase 3).")]
        [SerializeField, Range(1, 9)] private int laterBladesPerVolley = 5;
        [Tooltip("Width of the fan, in degrees (wider from phase 2).")]
        [SerializeField, Range(0f, 120f)] private float bladeSpread = 30f;
        [SerializeField, Min(1f)] private float bladeSpeed = 20f;
        [SerializeField, Min(0f)] private float bladeDamage = 12f;
        [SerializeField, Min(0f)] private float bladeCooldown = 4f;

        [Header("Jump slam")]
        [SerializeField, Min(0.1f)] private float slamCrouch = 0.55f;
        [SerializeField, Min(0.2f)] private float slamAirTime = 0.85f;
        [SerializeField, Min(0.5f)] private float slamHeight = 5.5f;
        [SerializeField, Min(3f)] private float slamRange = 18f;
        [Tooltip("The landing hurts within this radius (full damage at the centre, half at the edge).")]
        [SerializeField, Min(0.5f)] private float slamRadius = 4.5f;
        [SerializeField, Min(0f)] private float slamDamage = 24f;
        [SerializeField, Min(1f)] private float waveRadius = 13f;
        [SerializeField, Min(1f)] private float waveSpeed = 11f;
        [SerializeField, Min(0f)] private float waveDamage = 14f;
        [Tooltip("Blades sprayed out from each landing (phase 2; two more in phase 3).")]
        [SerializeField, Range(0, 12)] private int landingBlades = 6;
        [SerializeField, Min(0f)] private float slamCooldown = 7f;

        [Header("Scream")]
        [SerializeField, Min(0.2f)] private float screamWindup = 1.1f;
        [SerializeField, Min(2f)] private float screamRange = 12f;
        [SerializeField, Min(0f)] private float stunTime = 1.5f;
        [SerializeField, Min(0f)] private float screamDamage = 5f;
        [SerializeField, Min(0f)] private float screamCooldown = 12f;

        [Header("Tidal spin (phase 3)")]
        [SerializeField, Min(0.2f)] private float spinCharge = 0.9f;
        [SerializeField, Min(0.5f)] private float spinTime = 2.6f;
        [SerializeField, Min(3f)] private float spinRange = 24f;
        [Tooltip("Height of the spinning beam above the ground at the edge of its reach: low enough to jump.")]
        [SerializeField, Min(0.1f)] private float spinBeamHeight = 0.45f;
        [SerializeField, Min(0f)] private float spinCooldown = 13f;

        private State state = State.Approach;
        private float stateTime, recoverFor, nextAttack, nextRepath;
        private float nextBeam, nextBlades, nextSlam, nextScream, nextSpin;
        private bool overflow, screamWarned;

        // Beam
        private LineRenderer beamHalo, beamMain, beamCore, aimLine;
        private Light mouthLight, impactLight;
        private const int BeamPoints = 14;
        private Vector3 beamFlat;
        private float beamBaseYaw, beamTargetY, beamDirection, nextBeamTick, nextBeamFx, nextRipple;

        /// <summary>The two water beams (every phase): one locks on and follows you, one sweeps a wide arc.</summary>
        public enum BeamVariant { Tracking, Sweep }
        private BeamVariant beamVariant;
        private BeamVariant? lastVariant;
        private SectorMarker sweepMarker;
        private Material lockRingMaterial; // the warning ring material, drawn over water (the player may stand in the pond)
        private float nextPing;
        private bool lockedOn;

        /// <summary>The variant being charged or fired (or last used).</summary>
        public BeamVariant CurrentBeam => beamVariant;

        // Blades, slam, scream, spin
        private int volleysLeft, volleysFired, hopsLeft;
        private bool jumpedOnce;
        private Vector3 jumpFrom, landing;
        private GroundMarker marker;
        private float pendingWaveAt, spinStartYaw, screamRings;

        // Animation
        private readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();
        private Vector3 bodyRest, bodyScale, throatScale;
        private float crouch, air, jawOpen, throatSize, headPitch, swipe, glow = 1f, hopPhase, landSquash, flinch;
        private MaterialPropertyBlock glowBlock;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        /// <summary>1, 2 or 3.</summary>
        public int Stage => overflow ? 3 : Phase;
        public override int DisplayPhase => Stage;
        public override float PhaseThreeAt => phaseThreeAt;
        public string StateName => state.ToString();

        /// <summary>Moments worth a sound (or other feedback), for ToadAudio.</summary>
        public enum Cue { BeamCharge, BeamFire, Blades, Jump, Land, ScreamCharge, Scream, SpinCharge, Overflow, CoolantCrack,
            TrackCharge, TrackPing, TrackLock, SweepCharge, SweepFire }
        public event System.Action<Cue> Cued;
        protected override string PartStage => StageProgress.Tutorial;

        // Faster windups and shorter cooldowns each phase.
        private float Tempo => Stage == 1 ? 1f : Stage == 2 ? 1.2f : 1.4f;

        // ---------- Lifecycle ----------

        protected override void OnBossStarted()
        {
            ParticleBurst.Prewarm();
            glowBlock = new MaterialPropertyBlock();
            foreach (Transform item in AllRig()) if (item != null && !rest.ContainsKey(item)) rest[item] = item.localRotation;
            if (body != null)
            {
                bodyRest = body.localPosition;
                bodyScale = body.localScale;
            }
            if (throat != null) throatScale = throat.localScale;
            BuildBeam();
            RelayPart(coreHealth, 0);
        }

        protected override void OnEngaged()
        {
            float now = Time.time;
            nextBlades = now + 1.2f;
            nextSlam = now + 3f;
            nextBeam = now + 4.5f;
            nextScream = now + 7f;
            nextSpin = now + 3f;
            nextAttack = now + 0.6f;
            state = State.Approach;
        }

        protected override void OnPhaseTwo()
        {
            CancelAttack();
            // The cooling system cracks: coolant sprays out of its back.
            Vector3 back = BodyPoint(new Vector3(0f, 1.6f, -0.6f));
            ParticleBurst.Spawn(ParticleBurst.Kind.Water, back, Vector3.up, 40, 0.22f, 7f, 50f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Mist, back, Vector3.up, 14, 1.4f, 3f, 60f);
            CameraShake.Add(0.4f);
            Cued?.Invoke(Cue.CoolantCrack);
        }

        protected override void UpdatePhaseShift(float dt)
        {
            if (state == State.SlamAir)
            {
                // Finish the jump it was in the middle of.
                stateTime += dt;
                UpdateSlamAir();
                return;
            }
            if (Random.value < dt * 12f) SprayFromRig(0.6f);
            if (Target != null) TurnToward(Target.position, 90f * dt);
        }

        protected override void OnPhaseShiftEnded()
        {
            nextAttack = Time.time + 0.3f;
            if (state != State.SlamAir) state = State.Approach;
        }

        protected override void OnBossHit(float amount, GameObject source, Vector3 away)
        {
            flinch = 1f;
            if (Phase == 2 && !overflow && HealthFraction <= phaseThreeAt) BeginOverflow();
        }

        protected override float OnPartHit(int part, float amount, GameObject source)
        {
            if (part != 0 || coreHealth == null) return amount;
            ParticleBurst.Spawn(ParticleBurst.Kind.Water, coreHealth.transform.position, coreHealth.transform.forward, 10, 0.16f, 5f, 40f);
            return amount * coreMultiplier;
        }

        protected override void OnReset()
        {
            CancelAttack();
            if (Agent != null && !Agent.enabled)
            {
                transform.position = Home;
                Agent.enabled = true;
                if (Agent.isOnNavMesh) Agent.Warp(Home);
            }
            overflow = false;
            screamWarned = false;
            jumpedOnce = false;
            pendingWaveAt = 0f;
            state = State.Approach;
            crouch = air = jawOpen = swipe = headPitch = landSquash = 0f;
        }

        protected override void OnBossDeath()
        {
            if (state == State.SlamAir) transform.position = landing;
            CancelAttack();
            ParticleBurst.Spawn(ParticleBurst.Kind.Water, BodyPoint(new Vector3(0f, 1.5f, 0f)), Vector3.up, 60, 0.25f, 8f, 70f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Mist, transform.position + Vector3.up, Vector3.up, 20, 1.8f, 3f, 80f);
        }

        // ---------- Combat ----------

        protected override void TickCombat(float dt)
        {
            UpdateShove(dt);
            stateTime += dt;
            if (pendingWaveAt > 0f && Time.time >= pendingWaveAt)
            {
                pendingWaveAt = 0f;
                SplashWave.Spawn(waterMaterial, GroundBelow(transform.position), waveRadius * 1.25f, waveSpeed * 0.85f, waveDamage, gameObject);
            }
            if (!TargetAlive && state != State.SlamAir)
            {
                CancelAttack();
                Halt();
                return;
            }

            switch (state)
            {
                case State.Approach: UpdateApproach(dt); break;
                case State.Recover:
                    Halt();
                    if (Target != null) TurnToward(Target.position, turnRate * 0.5f * dt);
                    if (stateTime >= recoverFor) Enter(State.Approach);
                    break;
                case State.BeamCharge: UpdateBeamCharge(dt); break;
                case State.Beam: UpdateBeam(dt); break;
                case State.BladeWindup: UpdateBladeWindup(dt); break;
                case State.Blades: UpdateBlades(); break;
                case State.SlamCrouch: UpdateSlamCrouch(dt); break;
                case State.SlamAir: UpdateSlamAir(); break;
                case State.ScreamWindup: UpdateScreamWindup(dt); break;
                case State.Scream: UpdateScream(); break;
                case State.SpinCharge: UpdateSpinCharge(); break;
                case State.Spin: UpdateSpin(); break;
                case State.Overflow: UpdateOverflow(dt); break;
            }
        }

        private void Enter(State next)
        {
            state = next;
            stateTime = 0f;
        }

        private void RecoverFor(float seconds)
        {
            HideBeam();
            ClearMarker();
            recoverFor = seconds;
            nextAttack = Time.time + seconds + attackGap / Tempo;
            Enter(State.Recover);
        }

        private void UpdateApproach(float dt)
        {
            float distance = FlatDistance(Target.position);
            if (Time.time >= nextAttack && ChooseAttack(distance)) return;
            if (distance > preferredRange + 1.5f)
            {
                if (Time.time < nextRepath) return;
                nextRepath = Time.time + 0.25f;
                Chase(Target.position, hopSpeed * (1f + 0.15f * (Stage - 1)));
            }
            else
            {
                Halt();
                TurnToward(Target.position, turnRate * dt);
            }
        }

        private bool ChooseAttack(float distance)
        {
            float now = Time.time;
            bool seen = InSight(HeadPosition, beamRange + 6f);
            if (Stage == 3 && now >= nextSpin && distance <= spinRange)
            {
                Halt();
                Enter(State.SpinCharge);
                marker = GroundMarker.Spawn(warningMaterial, null, GroundBelow(transform.position), spinRange * 0.6f);
                Cued?.Invoke(Cue.SpinCharge);
                return true;
            }
            if (now >= nextScream && distance <= screamRange * 0.85f && seen)
            {
                Halt();
                Enter(State.ScreamWindup);
                Cued?.Invoke(Cue.ScreamCharge);
                marker = GroundMarker.Spawn(warningMaterial, null, GroundBelow(transform.position), screamRange);
                if (!screamWarned)
                {
                    screamWarned = true;
                    MiniBossHud.Message("The Chief Toad is about to scream: get out of the ring or behind cover!", new Color(0.7f, 0.92f, 1f), 3f);
                }
                return true;
            }
            if (now >= nextSlam && distance >= 4f && distance <= slamRange)
            {
                hopsLeft = Stage >= 2 ? 1 : 0;
                BeginSlam();
                return true;
            }
            if (now >= nextBeam && seen && distance >= 5f && distance <= beamRange * 0.8f)
            {
                Halt();
                BeginBeam();
                return true;
            }
            if (now >= nextBlades && seen && distance >= 3.5f)
            {
                Halt();
                volleysLeft = Stage;
                volleysFired = 0;
                Enter(State.BladeWindup);
                return true;
            }
            if (distance < 4f && now >= nextSlam - slamCooldown * 0.5f)
            {
                // Pressed up close: it hops right onto you.
                hopsLeft = 0;
                BeginSlam();
                return true;
            }
            return false;
        }

        private void CancelAttack()
        {
            HideBeam();
            ClearMarker();
            if (state != State.SlamAir && state != State.Approach)
            {
                recoverFor = 0.4f;
                Enter(State.Recover);
            }
        }

        // ---------- Water beam ----------

        // The two variants take turns (the first one is random), so every phase uses both.
        private void BeginBeam()
        {
            beamVariant = lastVariant == null ? (Random.value < 0.5f ? BeamVariant.Tracking : BeamVariant.Sweep)
                : lastVariant == BeamVariant.Tracking ? BeamVariant.Sweep : BeamVariant.Tracking;
            lastVariant = beamVariant;
            lockedOn = false;
            nextPing = 0f;
            Enter(State.BeamCharge);
            if (beamVariant == BeamVariant.Tracking)
            {
                // A sighting line from his mouth to you, and a target ring that follows you on the ground.
                aimLine.enabled = true;
                marker = GroundMarker.Spawn(SectorMarker.OverWater(warningMaterial, ref lockRingMaterial), null, GroundBelow(Target.position), 1.4f);
                Cued?.Invoke(Cue.TrackCharge);
            }
            else
            {
                // The arc is fixed now, so the wedge shows exactly where the torrent will go.
                Vector3 flat = Level(Target.position - transform.position);
                beamBaseYaw = flat.sqrMagnitude > 0.01f ? Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg : transform.eulerAngles.y;
                beamDirection = Random.value < 0.5f ? -1f : 1f;
                sweepMarker = SectorMarker.Spawn(GroundBelow(transform.position), beamBaseYaw, sweepArc, beamRange, beamDirection,
                    new Color(1f, 0.15f, 0.08f));
                Cued?.Invoke(Cue.SweepCharge);
            }
        }

        // The tracking jet's windup is long on purpose (time to find cover) and never shorter than 1.6 s.
        private float BeamChargeTime => beamVariant == BeamVariant.Tracking ? Mathf.Max(1.6f, trackCharge / Tempo) : sweepCharge / Tempo;

        private void UpdateBeamCharge(float dt)
        {
            float duration = BeamChargeTime;
            float k = Mathf.Clamp01(stateTime / duration);
            Vector3 origin = MouthPosition;
            if (beamVariant == BeamVariant.Tracking) ChargeTracking(dt, k, origin, duration - stateTime);
            else ChargeSweep(dt, k, origin);
            if (stateTime < duration) return;
            FireBeam(origin);
        }

        private void ChargeTracking(float dt, float k, Vector3 origin, float remaining)
        {
            TurnToward(Target.position, 220f * dt); // he keeps his eyes on you the whole time
            bool locked = remaining < 0.4f;

            // The sighting line: thin and pale, hotter, thicker and flickering faster as it builds; solid white once locked.
            Vector3 aim = Target.position + Vector3.up * 1f;
            Vector3 end = BeamEnd(origin, aim - origin, beamRange);
            float flicker = locked ? 1f : Mathf.Sin(Time.time * Mathf.Lerp(10f, 48f, k)) > -0.2f ? 1f : 0.3f;
            Color line = locked ? Color.white : Color.Lerp(new Color(0.55f, 0.88f, 1f), new Color(1f, 0.55f, 0.45f), k);
            aimLine.startColor = new Color(line.r, line.g, line.b, Mathf.Lerp(0.5f, 1f, k) * flicker);
            aimLine.endColor = new Color(line.r, line.g, line.b, Mathf.Lerp(0.35f, 0.9f, k) * flicker);
            DrawLine(aimLine, origin, end, Mathf.Lerp(0.05f, 0.14f, k) * (locked ? 1.8f : 1f), 0f);

            // The target ring stays under you, pulsing faster.
            if (marker != null)
            {
                marker.MoveTo(GroundBelow(Target.position));
                marker.Progress = k;
            }

            // Lock-on pings that speed up, then one sharp tone as it locks.
            if (!locked && Time.time >= nextPing)
            {
                nextPing = Time.time + Mathf.Lerp(0.5f, 0.1f, k);
                Cued?.Invoke(Cue.TrackPing);
            }
            if (locked && !lockedOn)
            {
                lockedOn = true;
                Cued?.Invoke(Cue.TrackLock);
            }

            // A ball of pressurised water swells in his mouth, with mist pulled in.
            if (mouthLight != null)
            {
                mouthLight.enabled = true;
                mouthLight.transform.position = origin;
                mouthLight.intensity = Mathf.Lerp(1f, 6f, k) * (locked ? 1.3f : 1f);
            }
            if (Time.time >= nextBeamFx)
            {
                nextBeamFx = Time.time + 0.05f;
                ParticleBurst.Spawn(ParticleBurst.Kind.Fireball, origin, Vector3.zero, 1, Mathf.Lerp(0.25f, 1.1f, k), 0.15f, 90f,
                    new Color(0.6f, 0.9f, 1f, 0.55f));
                Vector3 from = origin + Random.onUnitSphere * 1.8f;
                ParticleBurst.Spawn(ParticleBurst.Kind.Mist, from, origin - from, 1, 0.5f, 4f, 5f);
            }
        }

        private void ChargeSweep(float dt, float k, Vector3 origin)
        {
            // He plants himself and swings his head to the edge the torrent starts from.
            float startYaw = beamBaseYaw - sweepArc * beamDirection;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, startYaw, 0f), 160f * dt);
            if (sweepMarker != null) sweepMarker.Progress = k;
            if (mouthLight != null)
            {
                mouthLight.enabled = true;
                mouthLight.transform.position = origin;
                mouthLight.intensity = 6f * k;
            }
            if (Time.time >= nextBeamFx)
            {
                // A huge breath: mist streams in from all round him, and his rig sprays.
                nextBeamFx = Time.time + 0.04f;
                for (int i = 0; i < 2; i++)
                {
                    Vector3 from = origin + Random.onUnitSphere * 3f;
                    ParticleBurst.Spawn(ParticleBurst.Kind.Mist, from, origin - from, 1, 0.9f, 6f, 5f);
                }
                if (Random.value < 0.3f) SprayFromRig(0.4f);
            }
        }

        private void FireBeam(Vector3 origin)
        {
            aimLine.enabled = false;
            ClearMarker(false);
            bool sweep = beamVariant == BeamVariant.Sweep;
            if (sweep)
            {
                beamFlat = Quaternion.Euler(0f, beamBaseYaw - sweepArc * beamDirection, 0f) * Vector3.forward;
                if (sweepMarker != null) sweepMarker.Fire(); // the wedge stays, fading, while the torrent crosses it
            }
            else
            {
                Vector3 flat = Level(Target.position - transform.position);
                beamFlat = flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward;
            }
            beamTargetY = Target.position.y + 1f;
            nextBeamTick = 0f;
            ShowBeam(true);
            float scale = sweep ? sweepScale : 1f;
            ParticleBurst.Spawn(ParticleBurst.Kind.Water, origin, beamFlat, Mathf.RoundToInt(24 * scale), 0.25f * scale, 9f, 25f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Mist, origin, beamFlat, Mathf.RoundToInt(8 * scale), 1.2f * scale, 4f, 30f);
            CameraShake.Add(sweep ? 0.45f : 0.25f);
            Cued?.Invoke(sweep ? Cue.SweepFire : Cue.BeamFire);
            Enter(State.Beam);
        }

        private void UpdateBeam(float dt)
        {
            bool sweep = beamVariant == BeamVariant.Sweep;
            float duration = sweep ? sweepTime / Mathf.Sqrt(Tempo) : trackTime;
            float t = Mathf.Clamp01(stateTime / duration);
            if (sweep)
            {
                // Slowly across the whole wedge, easing in and out at the edges.
                float yaw = beamBaseYaw + Mathf.Lerp(-sweepArc, sweepArc, Mathf.SmoothStep(0f, 1f, t)) * beamDirection;
                beamFlat = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            }
            else
            {
                // Turns after you faster than you can run round him.
                Vector3 want = Level(Target.position - transform.position);
                if (want.sqrMagnitude > 0.01f)
                    beamFlat = Vector3.RotateTowards(beamFlat, want.normalized, trackTurnRate * Mathf.Sqrt(Tempo) * Mathf.Deg2Rad * dt, 0f);
            }
            // The jet tilts up or down after the player (on slopes, banks, mid-jump).
            beamTargetY = Mathf.Lerp(beamTargetY, Target.position.y + 1f, dt * 3f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(beamFlat), 400f * dt);

            Vector3 origin = MouthPosition;
            // Aim at the player's real distance, so the tilt is right up close as well as far away.
            Vector3 toTarget = Target.position - origin;
            toTarget.y = 0f;
            float reach = Mathf.Clamp(toTarget.magnitude, 3f, beamRange);
            Vector3 aim = new Vector3(origin.x, beamTargetY, origin.z) + beamFlat * reach;
            Vector3 end = BeamEnd(origin, aim - origin, beamRange);
            DrawWaterBeam(origin, end, sweep ? sweepScale : 1f);
            BeamHits(origin, end, sweep ? sweepHitRadius : beamHitRadius, sweep ? sweepDamagePerTick : trackDamagePerTick);
            if (sweep && Target != null && Vector3.Distance(Target.position, end) < 12f) CameraShake.Add(0.6f * dt); // the ground shakes near the torrent
            if (stateTime < duration) return;
            nextBeam = Time.time + beamCooldown / Tempo;
            RecoverFor(sweep ? 1f : 0.7f);
        }

        // Damage and push while the player is in the jet.
        private void BeamHits(Vector3 origin, Vector3 end, float radius, float damage)
        {
            if (Time.time < nextBeamTick || !PlayerInBeam(origin, end, radius)) return;
            nextBeamTick = Time.time + beamTick;
            if (HurtPlayer(damage))
            {
                Vector3 push = end - origin;
                push.y = 0f;
                ShovePlayer(push, beamPush);
                CameraShake.Add(0.08f);
            }
        }

        private void DrawWaterBeam(Vector3 origin, Vector3 end, float scale)
        {
            // The torrent (scale > 1) leaves his mouth narrower than it ends, fanning out into a wall of water.
            float mouthShare = scale > 1.5f ? 0.35f : 0.8f;
            DrawLine(beamHalo, origin, end, 2.5f * scale, 0.12f * scale, mouthShare);
            DrawLine(beamMain, origin, end, 1.2f * scale, 0.08f * scale, mouthShare);
            DrawLine(beamCore, origin, end, 0.42f * scale, 0.03f * scale, mouthShare);
            float flicker = 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 20f, 0.4f);
            if (mouthLight != null)
            {
                mouthLight.transform.position = origin;
                mouthLight.intensity = 5f * flicker;
            }
            if (impactLight != null)
            {
                impactLight.transform.position = end - (end - origin).normalized * 0.8f + Vector3.up * 0.5f;
                impactLight.intensity = 4f * flicker;
            }
            if (Time.time >= nextBeamFx)
            {
                // Spray at the mouth, droplets shed along the jet, and a fountain where it lands.
                nextBeamFx = Time.time + 0.05f;
                Vector3 along = end - origin;
                int more = Mathf.RoundToInt(scale);
                ParticleBurst.Spawn(ParticleBurst.Kind.Mist, origin, along, more, 0.8f * scale, 3f, 20f);
                for (int i = 0; i < more; i++)
                    ParticleBurst.Spawn(ParticleBurst.Kind.Water, Vector3.Lerp(origin, end, Random.value), along + Vector3.down * 2f, 2, 0.14f * scale, 3f, 40f);
                ParticleBurst.Spawn(ParticleBurst.Kind.Water, end, Vector3.up - along.normalized * 0.7f, 6 * more, 0.2f * scale, 6f * Mathf.Sqrt(scale), 55f);
                if (Random.value < 0.4f * scale) ParticleBurst.Spawn(ParticleBurst.Kind.Mist, end, Vector3.up, 2 * more, 1.3f * scale, 2f, 40f);
            }
            if (Time.time >= nextRipple && shriekMaterial != null)
            {
                nextRipple = Time.time + 0.3f;
                ExpandingRing.Spawn(shriekMaterial, GroundBelow(end) + Vector3.up * 0.05f, 2.2f * scale, 0.6f, 0.12f * scale);
            }
        }

        // ---------- Water blades ----------

        private void UpdateBladeWindup(float dt)
        {
            TurnToward(Target.position, 300f * dt);
            if (stateTime < bladeWindup / Tempo) return;
            Enter(State.Blades);
            FireVolley();
        }

        private void UpdateBlades()
        {
            if (volleysFired < volleysLeft && stateTime >= volleysFired * 0.35f) FireVolley();
            if (volleysFired >= volleysLeft && stateTime >= (volleysLeft - 1) * 0.35f + 0.3f)
            {
                nextBlades = Time.time + bladeCooldown / Tempo;
                RecoverFor(0.5f);
            }
        }

        private void FireVolley()
        {
            Cued?.Invoke(Cue.Blades);
            int count = Stage == 1 ? bladesPerVolley : laterBladesPerVolley;
            float spread = Stage == 1 ? bladeSpread : bladeSpread * 1.5f;
            float offset = volleysFired % 2 == 1 && count > 1 ? spread / (count - 1) * 0.5f : 0f; // alternate volleys fill the gaps
            Vector3 hand = frontLegs.Length > 1 && frontLegs[1] != null ? frontLegs[1].position : transform.position;
            Vector3 from = hand + transform.forward * 1.2f;
            from.y = Mathf.Max(from.y, GroundBelow(from).y + 1f);
            Vector3 toPlayer = Target.position + Vector3.up * 1f - from;
            toPlayer.y = Mathf.Clamp(toPlayer.y, -2f, 2f);
            for (int i = 0; i < count; i++)
            {
                float angle = count == 1 ? 0f : Mathf.Lerp(-spread * 0.5f, spread * 0.5f, i / (float)(count - 1)) + offset;
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * toPlayer;
                WaterBlade.Launch(waterMaterial, from, direction, bladeSpeed * (1f + 0.1f * (Stage - 1)), bladeDamage, gameObject);
            }
            ParticleBurst.Spawn(ParticleBurst.Kind.Water, from, transform.forward, 14, 0.2f, 6f, 45f);
            volleysFired++;
            swipe = 1f;
        }

        // ---------- Jump slam ----------

        private void BeginSlam()
        {
            Halt();
            Vector3 predicted = Target.position;
            if (TargetBody != null)
            {
                Vector3 moving = TargetBody.velocity;
                moving.y = 0f;
                predicted += moving * 0.45f;
            }
            Vector3 offset = predicted - transform.position;
            offset.y = 0f;
            if (offset.magnitude > slamRange) predicted = transform.position + offset.normalized * slamRange;
            landing = NavMesh.SamplePosition(predicted, out NavMeshHit hit, 4f, NavMesh.AllAreas) ? hit.position : GroundBelow(predicted);
            ClearMarker();
            marker = GroundMarker.Spawn(warningMaterial, null, landing, slamRadius);
            Enter(State.SlamCrouch);
        }

        // The second hop of a double hop comes quicker.
        private float CrouchTime => (jumpedOnce ? 0.3f : slamCrouch) / Tempo;

        private void UpdateSlamCrouch(float dt)
        {
            TurnToward(landing, 400f * dt);
            if (marker != null) marker.Progress = stateTime / (CrouchTime + slamAirTime);
            if (stateTime < CrouchTime) return;
            jumpFrom = transform.position;
            if (Agent != null) Agent.enabled = false; // flying: moved by hand until it lands
            Cued?.Invoke(Cue.Jump);
            ParticleBurst.Spawn(ParticleBurst.Kind.Water, transform.position, Vector3.up, 20, 0.2f, 6f, 60f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Mist, transform.position, Vector3.up, 6, 1.2f, 2f, 70f);
            Enter(State.SlamAir);
        }

        private void UpdateSlamAir()
        {
            float t = Mathf.Clamp01(stateTime / slamAirTime);
            Vector3 p = Vector3.Lerp(jumpFrom, landing, t);
            p.y += slamHeight * 4f * t * (1f - t);
            transform.position = p;
            Vector3 flat = landing - jumpFrom;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat);
            if (marker != null) marker.Progress = (CrouchTime + stateTime) / (CrouchTime + slamAirTime);
            if (t >= 1f) Land();
        }

        private void Land()
        {
            transform.position = landing;
            if (Agent != null)
            {
                Agent.enabled = true;
                if (Agent.isOnNavMesh) Agent.Warp(landing);
            }
            ClearMarker();
            landSquash = 1f;
            Cued?.Invoke(Cue.Land);

            // The impact: hurts up close and shoves you away.
            if (TargetAlive)
            {
                Vector3 offset = Target.position - landing;
                float height = offset.y;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance <= slamRadius && Mathf.Abs(height) < 3f)
                {
                    HurtPlayer(slamDamage * Mathf.Lerp(1f, 0.5f, distance / slamRadius));
                    ShovePlayer(distance > 0.1f ? offset : transform.forward, 2f);
                }
                CameraShake.Add(Mathf.Lerp(0.8f, 0.2f, Mathf.Clamp01(distance / 30f)));
            }
            ParticleBurst.Spawn(ParticleBurst.Kind.Water, landing, Vector3.up, 70, 0.26f, 9f, 70f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Mist, landing + Vector3.up * 0.5f, Vector3.up, 16, 1.8f, 3.5f, 85f);
            SplashWave.Spawn(waterMaterial, landing, waveRadius * (Stage == 3 ? 1.2f : 1f), waveSpeed, waveDamage, gameObject);
            if (Stage == 3) pendingWaveAt = Time.time + 0.45f;
            if (Stage >= 2 && landingBlades > 0)
            {
                // The splash flings a ring of blades out.
                int count = landingBlades + (Stage == 3 ? 2 : 0);
                float turn = Random.value * 360f;
                for (int i = 0; i < count; i++)
                {
                    Vector3 direction = Quaternion.Euler(0f, turn + i * 360f / count, 0f) * Vector3.forward;
                    WaterBlade.Launch(waterMaterial, landing + Vector3.up * 0.9f + direction * 1.6f, direction, bladeSpeed * 0.75f, bladeDamage * 0.75f, gameObject, 1.4f);
                }
            }

            if (hopsLeft > 0 && TargetAlive)
            {
                hopsLeft--;
                jumpedOnce = true;
                BeginSlam();
                return;
            }
            jumpedOnce = false;
            nextSlam = Time.time + slamCooldown / Tempo;
            RecoverFor(0.8f);
        }

        // ---------- Scream ----------

        private void UpdateScreamWindup(float dt)
        {
            float duration = screamWindup / Tempo;
            TurnToward(Target.position, 120f * dt);
            if (marker != null)
            {
                marker.MoveTo(GroundBelow(transform.position));
                marker.Progress = stateTime / duration;
            }
            if (Random.value < dt * 10f && throat != null) ParticleBurst.Spawn(ParticleBurst.Kind.Mist, throat.position, Vector3.up, 1, 0.6f, 1.5f, 40f);
            if (stateTime < duration) return;

            // The scream: shock rings and a blast of spray. Stuns you if you're in range and in its line of sight.
            ClearMarker();
            Vector3 headAt = HeadPosition;
            ExpandingRing.Spawn(shriekMaterial != null ? shriekMaterial : warningMaterial, GroundBelow(transform.position) + Vector3.up * 0.1f, screamRange, 0.45f, 0.3f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Mist, headAt, transform.forward, 14, 1.4f, 7f, 70f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Water, headAt, transform.forward + Vector3.up, 24, 0.18f, 8f, 60f);
            CameraShake.Add(0.5f);
            screamRings = 0f;
            if (TargetAlive && FlatDistance(Target.position) <= screamRange && InSight(headAt, screamRange + 3f))
            {
                PlayerStun.Apply(Target, stunTime);
                HurtPlayer(screamDamage);
            }
            Cued?.Invoke(Cue.Scream);
            Enter(State.Scream);
        }

        private void UpdateScream()
        {
            // Three standing shock rings rolling out of its mouth.
            if (stateTime >= screamRings * 0.12f && screamRings < 3f)
            {
                screamRings++;
                Vector3 toward = Target != null ? Target.position - transform.position : transform.forward;
                toward.y = 0f;
                if (toward.sqrMagnitude < 0.01f) toward = transform.forward;
                ExpandingRing.Spawn(shriekMaterial != null ? shriekMaterial : warningMaterial, HeadPosition + toward.normalized * 1.5f,
                    3f + screamRings * 1.5f, 0.5f, 0.18f, true, Quaternion.LookRotation(toward));
            }
            if (stateTime < 0.7f) return;
            nextScream = Time.time + screamCooldown / Tempo;
            RecoverFor(0.6f);
        }

        // ---------- Tidal spin (phase 3) ----------

        private void UpdateSpinCharge()
        {
            float duration = spinCharge / Tempo * 1.4f; // a touch longer than the others: it's the big one
            if (marker != null) marker.Progress = stateTime / duration;
            if (Random.value < 0.25f) SprayFromRig(0.3f);
            if (stateTime < duration) return;
            ClearMarker();
            spinStartYaw = transform.eulerAngles.y;
            beamDirection = Random.value < 0.5f ? -1f : 1f;
            nextBeamTick = 0f;
            ShowBeam(true);
            MiniBossHud.Message("TIDAL SPIN: jump the beam!", new Color(0.7f, 0.92f, 1f), 2f);
            Enter(State.Spin);
        }

        private void UpdateSpin()
        {
            float t = Mathf.Clamp01(stateTime / spinTime);
            transform.rotation = Quaternion.Euler(0f, spinStartYaw + 360f * t * beamDirection, 0f);
            Vector3 origin = MouthPosition;
            Vector3 ground = GroundBelow(transform.position);
            Vector3 aim = ground + transform.forward * spinRange + Vector3.up * spinBeamHeight;
            Vector3 end = BeamEnd(origin, aim - origin, spinRange + 2f);
            DrawWaterBeam(origin, end, 0.85f);
            BeamHits(origin, end, beamHitRadius, trackDamagePerTick);
            if (Random.value < 0.2f) SprayFromRig(0.3f);
            if (t < 1f) return;
            nextSpin = Time.time + spinCooldown / Tempo;
            RecoverFor(0.9f);
        }

        // ---------- Phase 3: coolant overflow ----------

        private void BeginOverflow()
        {
            overflow = true;
            if (state == State.SlamAir) return; // takes effect on landing
            CancelAttack();
            Halt();
            Enter(State.Overflow);
            MiniBossHud.Message("CHIEF TOAD: COOLANT OVERFLOW!", new Color(0.55f, 0.85f, 1f));
            Cued?.Invoke(Cue.Overflow);
            CameraShake.Add(0.5f);
        }

        private void UpdateOverflow(float dt)
        {
            if (Random.value < dt * 16f) SprayFromRig(0.8f);
            if (stateTime < overflowTime) return;
            nextSpin = Time.time + 2f;
            RecoverFor(0.2f);
        }

        // ---------- Helpers ----------

        private Vector3 MouthPosition => mouth != null ? mouth.position : transform.position + Vector3.up * 1.6f + transform.forward * 1.6f;
        private Vector3 HeadPosition => head != null ? head.position : transform.position + Vector3.up * 2f;
        private Vector3 BodyPoint(Vector3 local) => transform.TransformPoint(local);

        // A burst of coolant spray from a random point along the rig.
        private void SprayFromRig(float strength)
        {
            if (coolantGlow.Length == 0) return;
            Renderer item = coolantGlow[Random.Range(0, coolantGlow.Length)];
            if (item == null) return;
            Vector3 at = item.bounds.center;
            Vector3 outward = at - (transform.position + Vector3.up * 1.2f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Mist, at, outward + Vector3.up, 2, 0.9f * strength + 0.3f, 2.5f, 30f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Water, at, outward + Vector3.up, Mathf.RoundToInt(6 * strength) + 1, 0.15f, 4f, 30f);
        }

        // Line of sight from a point to the player's chest. Creatures don't block it; rocks and walls do.
        private bool InSight(Vector3 from, float range)
        {
            if (Target == null) return false;
            Vector3 chest = Target.position + Vector3.up * 1.2f;
            Vector3 toward = chest - from;
            float distance = toward.magnitude;
            if (distance > range) return false;
            if (distance < 0.01f) return true;
            foreach (RaycastHit hit in Physics.RaycastAll(from, toward / distance, distance, sightMask, QueryTriggerInteraction.Ignore))
            {
                Transform item = hit.collider.transform;
                if (item.IsChildOf(transform) || item.IsChildOf(Target)) continue;
                if (EnemyAllegiance.IsEnemy(hit.collider.gameObject)) continue;
                if (hit.collider.GetComponentInParent<Health>() != null) continue;
                return false;
            }
            return true;
        }

        // Where a jet from origin stops: the first solid thing (not creatures or the player), or its full range.
        private Vector3 BeamEnd(Vector3 origin, Vector3 direction, float range)
        {
            if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;
            direction.Normalize();
            float best = range;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, range, sightMask, QueryTriggerInteraction.Ignore))
            {
                Transform item = hit.collider.transform;
                if (item.IsChildOf(transform) || (Target != null && item.IsChildOf(Target))) continue;
                if (EnemyAllegiance.IsEnemy(hit.collider.gameObject) || hit.collider.GetComponentInParent<Health>() != null) continue;
                if (hit.distance < best) best = hit.distance;
            }
            return origin + direction * best;
        }

        // True if the jet from origin to end passes through the player (judged at their feet-to-head height, so the
        // low spinning beam can be jumped).
        private bool PlayerInBeam(Vector3 origin, Vector3 end, float radius)
        {
            if (!TargetAlive) return false;
            float feetY = TargetBody != null ? TargetBody.bounds.min.y : Target.position.y;
            Vector3 flat = end - origin;
            flat.y = 0f;
            float length = flat.magnitude;
            if (length < 0.1f) return false;
            Vector3 along = flat / length;
            Vector3 relative = Target.position - origin;
            relative.y = 0f;
            float distance = Vector3.Dot(relative, along);
            if (distance < 0f || distance > length) return false;
            if ((relative - along * distance).magnitude > radius) return false;
            float beamY = Mathf.Lerp(origin.y, end.y, distance / length);
            return beamY >= feetY - 0.6f && beamY <= feetY + 2.3f; // a thick jet: forgiving top and bottom
        }

        /// <param name="sweepWedge">Also remove the torrent sweep's wedge (false while the torrent is still crossing it).</param>
        private void ClearMarker(bool sweepWedge = true)
        {
            if (marker != null) Destroy(marker.gameObject);
            marker = null;
            if (!sweepWedge) return;
            if (sweepMarker != null) Destroy(sweepMarker.gameObject);
            sweepMarker = null;
        }

        private static Vector3 Level(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // ---------- Beam visuals ----------

        // A water jet in three layers (pale spray, the blue jet, a white core) with a cool light at each end, and a
        // thin aiming line for the charge.
        private void BuildBeam()
        {
            beamHalo = BeamLayer("Water Beam Spray", new Color(0.7f, 0.9f, 1f, 0.35f), new Color(0.75f, 0.92f, 1f, 0.2f), 2, false);
            beamMain = BeamLayer("Water Beam", new Color(0.4f, 0.75f, 1f, 0.85f), new Color(0.5f, 0.8f, 1f, 0.75f), 3, false);
            beamCore = BeamLayer("Water Beam Core", new Color(0.92f, 0.98f, 1f, 1f), new Color(0.85f, 0.95f, 1f, 0.9f), 4, true);
            aimLine = BeamLayer("Water Beam Aim", new Color(0.6f, 0.9f, 1f, 0.7f), new Color(0.6f, 0.9f, 1f, 0.25f), 4, true);
            mouthLight = BeamLight("Water Beam Light (mouth)");
            impactLight = BeamLight("Water Beam Light (impact)");
        }

        private LineRenderer BeamLayer(string name, Color start, Color end, int order, bool additive)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = additive ? EffectMaterials.Additive(EffectMaterials.Look.Beam) : EffectMaterials.AlphaBlended(EffectMaterials.Look.Beam);
            line.positionCount = BeamPoints;
            line.useWorldSpace = true;
            line.numCapVertices = 6;
            line.textureMode = LineTextureMode.Stretch;
            line.startColor = start;
            line.endColor = end;
            line.sortingOrder = order;
            line.shadowCastingMode = ShadowCastingMode.Off;
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
            light.color = new Color(0.45f, 0.8f, 1f);
            light.range = 9f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            return light;
        }

        private void ShowBeam(bool on)
        {
            foreach (LineRenderer line in new[] { beamHalo, beamMain, beamCore }) if (line != null) line.enabled = on;
            foreach (Light light in new[] { mouthLight, impactLight }) if (light != null) light.enabled = on;
        }

        private void HideBeam()
        {
            ShowBeam(false);
            if (aimLine != null) aimLine.enabled = false;
        }

        // Lays a layer from the mouth to the end, rippling sideways (most in the middle) by up to wobble metres.
        // mouthShare: its width at the mouth, as a share of width (it widens to 1.25x at the far end).
        private static void DrawLine(LineRenderer line, Vector3 from, Vector3 to, float width, float wobble, float mouthShare = 0.8f)
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
                float wave = Time.time * 26f - k * 18f;
                Vector3 offset = (side * Mathf.Sin(wave) + lift * Mathf.Cos(wave * 0.7f)) * wobble * envelope;
                line.SetPosition(i, from + along * k + offset);
            }
            float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 40f);
            line.startWidth = width * mouthShare * pulse;
            line.endWidth = width * 1.25f * pulse; // spreads as it goes
        }

        // ---------- Animation ----------

        private IEnumerable<Transform> AllRig()
        {
            yield return head;
            yield return jaw;
            yield return crown;
            foreach (Transform item in cape) yield return item;
            foreach (Transform item in frontLegs) yield return item;
            foreach (Transform item in backLegs) yield return item;
            foreach (Transform item in backKnees) yield return item;
        }

        private Quaternion Rest(Transform item) => item != null && rest.TryGetValue(item, out Quaternion rotation) ? rotation : Quaternion.identity;

        protected override void AnimateBoss(float dt)
        {
            if (body == null) return;
            float time = Time.time;
            float breath = Mathf.Sin(time * 1.6f);

            // What the current state wants.
            float crouchTarget = 0f, airTarget = 0f, jawTarget = 0.05f, throatTarget = 0.15f + 0.08f * breath;
            float pitchTarget = 0f, glowTarget = 1f;
            float k = StateProgress();
            switch (state)
            {
                // Tracking jet: he rears up and his throat balloons, coolant glowing hotter and flickering as it locks.
                // Torrent sweep: he squats low and wide, jaw gaping, for the big one.
                case State.BeamCharge when beamVariant == BeamVariant.Tracking:
                    jawTarget = 0.35f + 0.25f * k; throatTarget = 1.6f * k; pitchTarget = -14f * k;
                    glowTarget = 1f + 2.2f * k * (0.85f + 0.15f * Mathf.Sin(time * 30f)); break;
                case State.BeamCharge:
                    crouchTarget = 0.8f * k; jawTarget = 0.5f + 0.4f * k; throatTarget = 1.1f * k; glowTarget = 1f + 1.8f * k; break;
                case State.Beam when beamVariant == BeamVariant.Sweep: crouchTarget = 0.7f; jawTarget = 1f; throatTarget = 0.5f; glowTarget = 2.6f; break;
                case State.Beam: jawTarget = 0.95f; throatTarget = 0.35f; glowTarget = 2.2f; break;
                case State.BladeWindup: glowTarget = 1.4f; throatTarget = 0.4f; break;
                case State.Blades: jawTarget = 0.3f; break;
                case State.SlamCrouch: crouchTarget = 1f; glowTarget = 1.3f; break;
                case State.SlamAir: airTarget = 1f; crouchTarget = -0.4f; jawTarget = 0.4f; break;
                case State.ScreamWindup: throatTarget = 0.4f + 2f * k; pitchTarget = -22f * k; jawTarget = 0.12f; glowTarget = 1f + k; break;
                case State.Scream: jawTarget = 1f; throatTarget = 0.25f; pitchTarget = -25f + Mathf.Sin(time * 50f) * 4f; glowTarget = 2f; break;
                case State.SpinCharge: crouchTarget = 0.7f; jawTarget = 0.6f; throatTarget = 1.2f * k; glowTarget = 1f + 1.5f * k; break;
                case State.Spin: crouchTarget = 0.6f; jawTarget = 0.95f; glowTarget = 2.4f; break;
                case State.Overflow: throatTarget = 1f + 0.6f * Mathf.Sin(time * 9f); glowTarget = 1.5f + 1.5f * Mathf.PerlinNoise(time * 12f, 0.2f); jawTarget = 0.5f; break;
            }
            if (InPhaseShift) { throatTarget = 1f + 0.5f * Mathf.Sin(time * 8f); glowTarget = 0.5f + 2f * Mathf.PerlinNoise(time * 15f, 0.7f); jawTarget = 0.6f; }

            float blend = 1f - Mathf.Exp(-12f * dt);
            crouch = Mathf.Lerp(crouch, crouchTarget, blend);
            air = Mathf.Lerp(air, airTarget, blend);
            jawOpen = Mathf.Lerp(jawOpen, jawTarget, blend);
            throatSize = Mathf.Lerp(throatSize, throatTarget, blend);
            headPitch = Mathf.Lerp(headPitch, pitchTarget, blend);
            glow = Mathf.Lerp(glow, glowTarget * (Stage == 1 ? 1f : Stage == 2 ? 1.25f : 1.6f), blend);
            swipe = Mathf.MoveTowards(swipe, 0f, dt * 3f);
            landSquash = Mathf.MoveTowards(landSquash, 0f, dt * 3.5f);
            flinch = Mathf.MoveTowards(flinch, 0f, dt * 6f);

            // Hopping gait while it moves.
            float speed = AgentReady ? Agent.velocity.magnitude : 0f;
            float moving = Mathf.Clamp01(speed / Mathf.Max(0.5f, hopSpeed));
            hopPhase += dt * (2.2f + speed * 1.2f);
            float hop = Mathf.Abs(Mathf.Sin(hopPhase)) * 0.35f * moving;
            Visual.localPosition = VisualRest + Vector3.up * hop;

            // Body: squash when crouched or landing, stretch in the air, breathing always.
            float squash = Mathf.Max(crouch, 0f) + landSquash * 0.8f;
            float stretch = Mathf.Max(-crouch, 0f) + air * 0.3f;
            body.localPosition = bodyRest + Vector3.up * (-0.45f * squash + 0.12f * stretch + 0.03f * breath);
            body.localScale = Vector3.Scale(bodyScale, new Vector3(1f + 0.1f * squash - 0.05f * stretch, 1f - 0.14f * squash + 0.1f * stretch, 1f + 0.06f * squash));
            body.localRotation = Quaternion.Euler(-6f * moving * Mathf.Cos(hopPhase) - 8f * air + 4f * flinch, 0f, 0f);

            if (head != null) head.localRotation = Rest(head) * Quaternion.Euler(headPitch - 6f * flinch, Mathf.Sin(time * 0.7f) * 3f, 0f);
            if (jaw != null) jaw.localRotation = Rest(jaw) * Quaternion.Euler(jawOpen * 32f, 0f, 0f);
            if (throat != null)
            {
                float s = throatSize;
                throat.localScale = Vector3.Scale(throatScale, new Vector3(1f + 0.5f * s, 1f + 0.75f * s, 1f + 0.55f * s));
            }
            if (crown != null) crown.localRotation = Rest(crown) * Quaternion.Euler(0f, 0f, Mathf.Sin(time * 2.3f) * 2f + flinch * 5f);

            // Front legs: planted, the right one swiping out the blades.
            for (int i = 0; i < frontLegs.Length; i++)
            {
                if (frontLegs[i] == null) continue;
                float lift = i == 1 ? (state == State.BladeWindup ? Mathf.Clamp01(k * 1.5f) : 0f) : 0f;
                float sweep = i == 1 ? swipe : 0f;
                float step = Mathf.Sin(hopPhase + i * Mathf.PI) * 18f * moving;
                frontLegs[i].localRotation = Rest(frontLegs[i]) * Quaternion.Euler(-70f * lift - 30f * air + step, -50f * sweep, 20f * sweep);
            }
            // Back legs: folded in the crouch, kicked out straight in the leap.
            for (int i = 0; i < backLegs.Length; i++)
            {
                if (backLegs[i] == null) continue;
                float step = Mathf.Sin(hopPhase + i * Mathf.PI) * 14f * moving;
                backLegs[i].localRotation = Rest(backLegs[i]) * Quaternion.Euler(18f * squash - 40f * air + step, 0f, 0f);
                if (i < backKnees.Length && backKnees[i] != null)
                    backKnees[i].localRotation = Rest(backKnees[i]) * Quaternion.Euler(-25f * squash + 55f * air, 0f, 0f);
            }
            // The cape streams back while it moves and flutters up in the air.
            for (int i = 0; i < cape.Length; i++)
            {
                if (cape[i] == null) continue;
                float sway = Mathf.Sin(time * 1.7f + i * 1.3f) * 4f + 10f * moving - 28f * air;
                cape[i].localRotation = Rest(cape[i]) * Quaternion.Euler(-sway, Mathf.Sin(time * 1.1f + i) * 3f, 0f);
            }
        }

        // 0 to 1 through the current timed state (0 for untimed ones).
        private float StateProgress()
        {
            float duration;
            switch (state)
            {
                case State.BeamCharge: duration = BeamChargeTime; break;
                case State.BladeWindup: duration = bladeWindup / Tempo; break;
                case State.ScreamWindup: duration = screamWindup / Tempo; break;
                case State.SpinCharge: duration = spinCharge / Tempo * 1.4f; break;
                case State.SlamCrouch: duration = CrouchTime; break;
                default: return 0f;
            }
            return Mathf.Clamp01(stateTime / Mathf.Max(0.01f, duration));
        }

        // The coolant segments glow, brighter each phase and while it charges.
        protected override void Paint()
        {
            if (glowBlock == null) return;
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 3f);
            Color emission = coolantColor * (glow * pulse * 2.2f);
            foreach (Renderer item in coolantGlow)
            {
                if (item == null) continue;
                item.GetPropertyBlock(glowBlock);
                glowBlock.SetColor(EmissionColor, emission);
                item.SetPropertyBlock(glowBlock);
            }
        }
    }
}
