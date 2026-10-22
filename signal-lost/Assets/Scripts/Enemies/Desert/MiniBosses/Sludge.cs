using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Effects;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// Mini-boss: a giant scorpion with the ship's fuel tank strapped to its back and a fuel hose in place of its
    /// stinger, with a spark igniter on the tip. Its body is a chain of plates that bend and ripple as it walks and
    /// turns; the tail sways and whips with follow-through, the eight legs walk in a proper alternating gait.
    ///
    /// At range:
    /// - Oil spray: the tail curls forward and the tank gurgles (the telegraph), then it lobs a volley of fuel globs
    ///   that carpet the ground around the player in slicks that slow them.
    /// - Ignite: when the player is near one of its slicks, the igniter flickers, then it sparks the slick into fire.
    ///   It never lights a slick it is standing in, and its own fires don't hurt it.
    /// - Fuel cannon: it plants its legs, raises its hose at the player and pressurises the tank for a couple of
    ///   seconds (a red sight line shows where it is aiming, and whether you are covered), then fires a blazing shell.
    ///   The blast is big and only hurts what it can see: get behind a rock or a hull plate.
    ///
    /// Up close (it is dangerous here, not shy):
    /// - Claw combo: lunging pincer snaps at anything in front.
    /// - Tail sting: turns and stabs the hose down onto the player's spot (on any side), leaving a lick of fire.
    /// - Torch: lights the hose and sweeps a short flamethrower across the player.
    ///
    /// Phase 2 (tank punctured): oil gushes out around it, it leaks a burning trail wherever it walks, its volleys are
    /// bigger and come more often, its claw combo is longer, and the cannon charges faster.
    ///
    /// Counterplay:
    /// - A heavy hit (a charged shot or the slam) on the fuel tank while it sprays or charges the cannon makes it
    ///   backfire: the tank explodes on its back, it takes big damage and is stunned, lying in its own fire.
    /// - Fires the player starts hurt it: shoot a slick it is standing in. Beetle blasts light slicks too.
    /// - Fire and the cannon's blast hurt fodder, so lure them in.
    /// All numbers are placeholders.
    /// </summary>
    public sealed class Sludge : DesertMiniBoss
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private enum State
        {
            Stalk, SprayWindup, Spray, IgniteWindup, ClawWindup, Claw, StingWindup, Sting, TorchWindup, Torch,
            CannonCharge, CannonFire, Recover, Backfired,
        }

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 3.2f;
        [Tooltip("Distance it likes to keep from the player when nothing is ready.")]
        [SerializeField, Min(2f)] private float preferredRange = 7f;

        [Header("Oil spray")]
        [Tooltip("Tail curling forward and the tank gurgling: the warning, and the backfire window.")]
        [SerializeField, Min(0.1f)] private float sprayWindup = 0.8f;
        [SerializeField, Min(0.1f)] private float phaseTwoSprayWindup = 0.6f;
        [Tooltip("Seconds the volley takes to leave the hose.")]
        [SerializeField, Min(0.1f)] private float sprayTime = 0.9f;
        [SerializeField, Range(1, 16)] private int globsPerSpray = 6;
        [SerializeField, Range(1, 20)] private int phaseTwoGlobsPerSpray = 9;
        [SerializeField, Min(2f)] private float sprayRange = 16f;
        [Tooltip("How far around the player the volley spreads (the first glob lands on them).")]
        [SerializeField, Min(0f)] private float sprayScatter = 3.4f;
        [SerializeField, Min(0.5f)] private float slickRadius = 1.8f;
        [SerializeField, Min(1f)] private float slickLife = 14f;
        [Tooltip("Fraction of the player's movement taken away while standing in oil.")]
        [SerializeField, Range(0f, 0.95f)] private float slickSlow = 0.5f;
        [SerializeField, Min(0f)] private float sprayCooldown = 4.5f;
        [SerializeField, Min(0f)] private float phaseTwoSprayCooldown = 3f;

        [Header("Igniter")]
        [SerializeField, Min(0.1f)] private float igniteWindup = 0.6f;
        [SerializeField, Min(0f)] private float igniteCooldown = 3f;
        [Tooltip("Lights a slick when the player is within this distance of its edge.")]
        [SerializeField, Min(0f)] private float igniteNearPlayer = 3f;
        [SerializeField] private FireStyle fire = new FireStyle { damage = 9f, interval = 0.5f, duration = 4f, smoke = 0.9f };

        [Header("Claws (close range)")]
        [SerializeField, Min(0.5f)] private float clawRange = 3.6f;
        [SerializeField, Min(0.1f)] private float clawWindup = 0.4f;
        [SerializeField, Min(0.5f)] private float clawReach = 3.8f;
        [SerializeField, Range(10f, 180f)] private float clawArc = 120f;
        [Tooltip("Damage of each snap.")]
        [SerializeField, Min(0f)] private float clawDamage = 11f;
        [SerializeField, Range(1, 5)] private int clawSnaps = 2;
        [SerializeField, Range(1, 6)] private int phaseTwoClawSnaps = 3;
        [Tooltip("Metres it lunges forward over the combo.")]
        [SerializeField, Min(0f)] private float clawLunge = 0.6f;
        [SerializeField, Min(0f)] private float clawCooldown = 1.1f;

        [Header("Tail sting (close range, any side)")]
        [SerializeField, Min(0.5f)] private float stingRange = 4.2f;
        [Tooltip("Rearing the tail back while a ring marks the spot: the warning.")]
        [SerializeField, Min(0.1f)] private float stingWindup = 0.6f;
        [Tooltip("Furthest from its middle the sting lands.")]
        [SerializeField, Min(0.5f)] private float stingReach = 3.4f;
        [SerializeField, Min(0.2f)] private float stingRadius = 1.3f;
        [SerializeField, Min(0f)] private float stingDamage = 22f;
        [SerializeField, Min(0f)] private float stingCooldown = 2.6f;
        [SerializeField, Min(0f)] private float stingFireRadius = 0.9f;
        [SerializeField, Min(0.1f)] private float stingFireDuration = 2f;

        [Header("Torch (short flamethrower)")]
        [SerializeField, Min(0f)] private float torchMinRange = 2.5f;
        [SerializeField, Min(1f)] private float torchRange = 6.5f;
        [SerializeField, Min(0.1f)] private float torchWindup = 0.5f;
        [SerializeField, Min(0.1f)] private float torchTime = 1.4f;
        [Tooltip("Half-width of the flame, in degrees.")]
        [SerializeField, Range(3f, 45f)] private float torchHalfAngle = 16f;
        [Tooltip("How far it sweeps the flame to each side, in degrees.")]
        [SerializeField, Range(0f, 60f)] private float torchSweep = 25f;
        [Tooltip("Damage each tick while caught in the flame.")]
        [SerializeField, Min(0f)] private float torchDamage = 6f;
        [SerializeField, Min(0.05f)] private float torchTick = 0.25f;
        [SerializeField, Min(0f)] private float torchCooldown = 6.5f;

        [Header("Fuel cannon (take cover!)")]
        [SerializeField, Min(1f)] private float cannonMinRange = 6f;
        [Tooltip("Seconds it charges: the player's time to reach cover.")]
        [SerializeField, Min(0.3f)] private float cannonCharge = 2.4f;
        [SerializeField, Min(0.3f)] private float phaseTwoCannonCharge = 2f;
        [Tooltip("Part of the charge spent tracking the player; after this the aim is locked.")]
        [SerializeField, Range(0f, 1f)] private float cannonAimLock = 0.85f;
        [SerializeField, Min(1f)] private float cannonShellSpeed = 38f;
        [SerializeField, Min(5f)] private float cannonRange = 45f;
        [SerializeField, Min(1f)] private float cannonBlastRadius = 7f;
        [SerializeField, Min(0f)] private float cannonDamage = 45f;
        [Tooltip("Fraction of the damage still dealt at the edge of the blast.")]
        [SerializeField, Range(0f, 1f)] private float cannonEdgeDamage = 0.4f;
        [SerializeField, Min(0f)] private float cannonFireRadius = 2.4f;
        [SerializeField, Min(0.1f)] private float cannonFireDuration = 4f;
        [Tooltip("Seconds it stands venting after a shot: an opening.")]
        [SerializeField, Min(0f)] private float cannonRecover = 1.1f;
        [SerializeField, Min(0f)] private float firstCannonDelay = 8f;
        [SerializeField, Min(0f)] private float cannonCooldown = 15f;
        [SerializeField, Min(0f)] private float phaseTwoCannonCooldown = 11f;

        [Header("Fuel tank weak spot")]
        [Tooltip("Health on the tank's own collider. A heavy hit while it sprays or charges makes it backfire.")]
        [SerializeField] private Health tankHealth;
        [SerializeField, Min(0f)] private float backfireDamage = 70f;
        [SerializeField, Min(0.5f)] private float backfireBlastRadius = 3.2f;
        [SerializeField, Min(0f)] private float backfireBlastDamage = 22f;
        [SerializeField, Min(0f)] private float backfireStun = 3.2f;
        [SerializeField] private Material blastMaterial;

        [Header("Phase 2: punctured tank")]
        [Tooltip("Metres walked between patches of its burning trail.")]
        [SerializeField, Min(0.3f)] private float leakSpacing = 1.8f;
        [SerializeField, Min(0.2f)] private float leakRadius = 0.9f;
        [SerializeField, Min(0.2f)] private float leakDuration = 2.5f;

        [Header("Rig")]
        [Tooltip("Body plates from the head back, each parented to the one in front: they bend and ripple.")]
        [SerializeField] private Transform[] bodySegments = new Transform[0];
        [Tooltip("Tail joints from the base to the tip, each parented to the one before.")]
        [SerializeField] private Transform[] tail = new Transform[0];
        [Tooltip("Where oil globs, sparks, flame and shells leave the hose.")]
        [SerializeField] private Transform hoseTip;
        [SerializeField] private Renderer igniter;
        [Tooltip("Claw shoulders, left then right.")]
        [SerializeField] private Transform[] claws = new Transform[0];
        [Tooltip("Claw elbows, left then right (optional).")]
        [SerializeField] private Transform[] clawElbows = new Transform[0];
        [Tooltip("The moving upper finger of each pincer.")]
        [SerializeField] private Transform[] clawFingers = new Transform[0];
        [Tooltip("Leg hips: four on the left (front to back), then four on the right.")]
        [SerializeField] private Transform[] legs = new Transform[0];
        [Tooltip("Leg knees, in the same order as the hips (optional).")]
        [SerializeField] private Transform[] legKnees = new Transform[0];
        [Tooltip("Small mouthparts that chew (optional).")]
        [SerializeField] private Transform[] mouthparts = new Transform[0];
        [SerializeField] private Transform tank;
        [Tooltip("Gushing fuel, shown once the tank is punctured.")]
        [SerializeField] private GameObject leak;
        [SerializeField] private Material oilMaterial;
        [SerializeField] private Material warningMaterial;
        [SerializeField] private Material sparkMaterial;

        private State state = State.Stalk;
        private float stateTime, recoverFor, nextSpray, nextIgnite, nextClaw, nextSting, nextTorch, nextCannon, nextRepath,
            nextLeakPuff, nextTorchTick, nextTorchFire, nextChargeFx, sprayAngle;
        private int globsSent, snaps;
        private bool struck;
        private Vector3 lastLeak, sprayCentre, stingSpot, cannonAim, torchDirection;
        private OilSlick igniteTarget;
        private GroundMarker stingMarker;
        private LineRenderer sight;
        private FireEffect pilotFlame, torchFlame;
        private Vector3 tankRest;
        private Color igniterColor = new Color(0.3f, 0.6f, 1f);
        private MaterialPropertyBlock block;

        protected override void Awake()
        {
            base.Awake();
            block = new MaterialPropertyBlock();
            CaptureRig();
            if (tank != null) tankRest = tank.localPosition;
            if (igniter != null && igniter.sharedMaterial != null && igniter.sharedMaterial.HasProperty(BaseColor))
                igniterColor = igniter.sharedMaterial.GetColor(BaseColor);
            if (leak != null) leak.SetActive(false);
        }

        protected override void OnBossStarted()
        {
            RelayPart(tankHealth, 0);
            // The hose's flames: a small pilot flame (the cannon charging) and the torch. They follow the hose tip.
            pilotFlame = FireEffect.SpawnJet(transform, Vector3.up * 2f, Quaternion.identity, 0.07f, 0.5f);
            pilotFlame.Strength = 0f;
            torchFlame = FireEffect.SpawnJet(transform, Vector3.up * 2f, Quaternion.identity, 0.16f, torchRange * 0.85f);
            torchFlame.Strength = 0f;
            BuildSight();
        }

        private Vector3 HoseTipPosition => hoseTip != null ? hoseTip.position : transform.position + Vector3.up * 2f;

        private void Enter(State next)
        {
            if (state == State.StingWindup && next != State.Sting) ClearMarker();
            if (state == State.CannonCharge) ShowSight(false);
            state = next;
            stateTime = 0f;
            struck = false;
            if (next != State.Stalk) Halt();
            if (next == State.Spray)
            {
                globsSent = 0;
                Vector3 velocity = TargetBody != null ? TargetBody.velocity : Vector3.zero;
                velocity.y = 0f;
                sprayCentre = Target.position + velocity * 0.4f; // a little ahead of a moving player
                sprayAngle = Random.value * Mathf.PI * 2f;
            }
            if (next == State.Claw) snaps = 0;
        }

        private void RecoverFor(float seconds)
        {
            Enter(State.Recover);
            recoverFor = seconds;
        }

        // ---------- Combat ----------

        protected override void OnEngaged()
        {
            Enter(State.Stalk);
            nextSpray = Time.time + 1f;
            nextIgnite = Time.time + 2f;
            nextClaw = Time.time;
            nextSting = Time.time + 0.8f;
            nextTorch = Time.time + 3f;
            nextCannon = Time.time + firstCannonDelay;
            lastLeak = transform.position;
        }

        protected override void TickCombat(float dt)
        {
            stateTime += dt;
            UpdateShove(dt);
            if (Phase == 2) Leak();
            if (!TargetAlive)
            {
                if (state == State.CannonCharge || state == State.Torch || state == State.StingWindup) RecoverFor(0.5f);
                return;
            }
            switch (state)
            {
                case State.Stalk: UpdateStalk(dt); break;
                case State.SprayWindup:
                    if (stateTime < SprayWindupTime * 0.8f) TurnToward(Target.position, 200f * dt);
                    if (stateTime >= SprayWindupTime) Enter(State.Spray);
                    break;
                case State.Spray: UpdateSpray(); break;
                case State.IgniteWindup: UpdateIgnite(dt); break;
                case State.ClawWindup:
                    if (stateTime < clawWindup * 0.8f) TurnToward(Target.position, 320f * dt);
                    if (stateTime >= clawWindup) Enter(State.Claw);
                    break;
                case State.Claw: UpdateClaw(dt); break;
                case State.StingWindup: UpdateStingWindup(dt); break;
                case State.Sting: UpdateSting(dt); break;
                case State.TorchWindup:
                    TurnToward(Target.position, 240f * dt);
                    if (stateTime >= torchWindup) BeginTorch();
                    break;
                case State.Torch: UpdateTorch(dt); break;
                case State.CannonCharge: UpdateCannonCharge(dt); break;
                case State.CannonFire: if (stateTime >= cannonRecover) RecoverFor(0.2f); break;
                case State.Recover: if (stateTime >= recoverFor) Enter(State.Stalk); break;
                case State.Backfired: if (stateTime >= backfireStun) RecoverFor(0.4f); break;
            }
        }

        private float SprayWindupTime => Phase == 2 ? phaseTwoSprayWindup : sprayWindup;
        private float CannonChargeTime => Phase == 2 ? phaseTwoCannonCharge : cannonCharge;

        private void UpdateStalk(float dt)
        {
            float now = Time.time;
            float distance = FlatDistance(Target.position);
            float angle = AngleToTarget();
            bool seen = CanSee(Mathf.Max(sprayRange, cannonRange) + 6f);
            if (seen) LastSeen = now;

            // Close range first: it is a brawler up close.
            if (now >= nextClaw && distance <= clawRange && angle < 70f) { Enter(State.ClawWindup); return; }
            if (now >= nextSting && distance <= stingRange) { BeginSting(); return; }
            if (now >= nextTorch && seen && distance >= torchMinRange && distance <= torchRange && angle < 50f) { Enter(State.TorchWindup); return; }
            // Then range.
            if (now >= nextCannon && seen && distance >= cannonMinRange && distance <= cannonRange * 0.8f) { BeginCannon(); return; }
            if (now >= nextSpray && seen && distance >= 3f && distance <= sprayRange) { Enter(State.SprayWindup); return; }
            if (now >= nextIgnite && FindSlickToIgnite(out igniteTarget)) { Enter(State.IgniteWindup); return; }

            if (distance < 5f) TurnToward(Target.position, 160f * dt); // keeps its claws on the player
            if (now < nextRepath) return;
            nextRepath = now + 0.3f;
            if (distance > preferredRange + 2f) Chase(Target.position, walkSpeed);
            else if (distance < 5f)
            {
                // In close: press in to claw range rather than backing off.
                Vector3 inFront = Target.position - FlatDirectionTo(Target.position) * 2f;
                if (NavMesh.SamplePosition(inFront, out NavMeshHit hit, 2f, NavMesh.AllAreas)) Chase(hit.position, walkSpeed);
            }
            else
            {
                Vector3 side = Vector3.Cross(Vector3.up, FlatDirectionTo(Target.position)) * (Mathf.Sin(now * 0.3f) > 0f ? 1f : -1f);
                Chase(transform.position + side * 3f + FlatDirectionTo(Target.position) * 1f, walkSpeed * 0.8f);
            }
        }

        // ---------- Oil spray ----------

        private void UpdateSpray()
        {
            int total = Phase == 2 ? phaseTwoGlobsPerSpray : globsPerSpray;
            int due = Mathf.Min(total, 1 + Mathf.FloorToInt(stateTime / sprayTime * total));
            while (globsSent < due)
            {
                Vector3 target = SprayPoint(globsSent, total);
                if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2.5f, NavMesh.AllAreas)) target = hit.position;
                Vector3 from = HoseTipPosition;
                OilGlob.Launch(oilMaterial, warningMaterial, fire, from, GroundBelow(target), 0.7f + FlatDistance(target) * 0.03f,
                    slickRadius, slickLife, slickSlow, gameObject);
                globsSent++;
            }
            if (stateTime < sprayTime) return;
            nextSpray = Time.time + (Phase == 2 ? phaseTwoSprayCooldown : sprayCooldown);
            nextIgnite = Mathf.Min(nextIgnite, Time.time + 1f);
            RecoverFor(0.3f);
        }

        /// <summary>
        /// Where each glob of a volley lands: the first on the player, the rest filling a disc around them in a
        /// sunflower spiral, so every way out of the middle is covered in oil.
        /// </summary>
        private Vector3 SprayPoint(int index, int total)
        {
            if (index == 0) return sprayCentre;
            const float golden = 2.39996f;
            float a = sprayAngle + index * golden;
            float r = sprayScatter * 1.35f * Mathf.Sqrt(index / (float)Mathf.Max(1, total - 1));
            return sprayCentre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
        }

        // ---------- Igniter ----------

        /// <summary>One of its own slicks near the player, and not one it is standing in.</summary>
        private bool FindSlickToIgnite(out OilSlick best)
        {
            best = null;
            float bestDistance = float.MaxValue;
            foreach (OilSlick slick in OilSlick.All)
            {
                if (slick == null || slick.Owner != gameObject || slick.Slow <= 0f) continue;
                float toPlayer = Flat(slick.transform.position - Target.position);
                if (toPlayer > slick.Radius + igniteNearPlayer) continue;
                if (Flat(slick.transform.position - transform.position) < slick.Radius + 2f) continue;
                if (toPlayer < bestDistance)
                {
                    bestDistance = toPlayer;
                    best = slick;
                }
            }
            return best != null;
        }

        private void UpdateIgnite(float dt)
        {
            if (igniteTarget == null) { RecoverFor(0.2f); return; } // the slick dried up or burned already
            TurnToward(igniteTarget.transform.position, 200f * dt);
            if (stateTime < igniteWindup) return;
            Vector3 from = HoseTipPosition;
            Vector3 to = igniteTarget.transform.position + Vector3.up * 0.2f;
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, from, to - from, 18, 0.1f, Vector3.Distance(from, to) * 2.2f, 6f);
            if (sparkMaterial != null) ImpactFlash.Spawn(sparkMaterial, from, 0.35f, 0.1f);
            igniteTarget.Ignite(gameObject); // its own fire: doesn't hurt it
            igniteTarget = null;
            nextIgnite = Time.time + igniteCooldown;
            RecoverFor(0.4f);
        }

        // ---------- Claws ----------

        private const float SnapInterval = 0.22f;
        private int ComboSnaps => Phase == 2 ? phaseTwoClawSnaps : clawSnaps;

        private void UpdateClaw(float dt)
        {
            int total = ComboSnaps;
            float comboTime = total * SnapInterval;
            if (stateTime < comboTime && AgentReady) Agent.Move(transform.forward * (clawLunge / comboTime) * dt);
            while (snaps < total && stateTime >= snaps * SnapInterval)
            {
                snaps++;
                TurnToward(Target.position, 30f);
                if (FlatDistance(Target.position) <= clawReach && AngleToTarget() <= clawArc * 0.5f && HurtPlayer(clawDamage))
                    ShovePlayer(FlatDirectionTo(Target.position), 0.6f);
            }
            if (stateTime < comboTime + 0.15f) return;
            nextClaw = Time.time + clawCooldown;
            RecoverFor(0.35f);
        }

        // ---------- Tail sting ----------

        private const float StingStrikeAt = 0.12f;

        private void BeginSting()
        {
            Enter(State.StingWindup);
            stingSpot = StingSpot();
            ClearMarker();
            stingMarker = GroundMarker.Spawn(warningMaterial, null, stingSpot, stingRadius);
        }

        private Vector3 StingSpot()
        {
            Vector3 direction = FlatDirectionTo(Target.position);
            float distance = Mathf.Min(FlatDistance(Target.position), stingReach);
            return GroundBelow(transform.position + direction * distance);
        }

        private void UpdateStingWindup(float dt)
        {
            float k = stateTime / stingWindup;
            TurnToward(Target.position, 360f * dt);
            if (k < 0.7f)
            {
                stingSpot = StingSpot();
                if (stingMarker != null) stingMarker.MoveTo(stingSpot);
            }
            if (stingMarker != null) stingMarker.Progress = k;
            if (stateTime >= stingWindup) Enter(State.Sting);
        }

        private void UpdateSting(float dt)
        {
            // A short lunge, then the hose comes down on the marked spot.
            if (stateTime < StingStrikeAt && AgentReady) Agent.Move(transform.forward * (0.6f / StingStrikeAt) * dt);
            if (!struck && stateTime >= StingStrikeAt)
            {
                struck = true;
                ClearMarker();
                Vector3 offset = Target.position - stingSpot;
                if (Flat(offset) <= stingRadius && offset.y > -1f && offset.y < 2.2f && HurtPlayer(stingDamage))
                    ShovePlayer(offset, 1.2f);
                FireZone.Spawn(fire, stingSpot, stingFireRadius, gameObject, transform, stingFireDuration);
                ParticleBurst.Spawn(ParticleBurst.Kind.Sand, stingSpot + Vector3.up * 0.1f, Vector3.up, 14, 0.4f, 3.5f, 60f);
                ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, stingSpot + Vector3.up * 0.2f, Vector3.up, 16, 0.1f, 6f, 70f);
            }
            if (stateTime < 0.45f) return;
            nextSting = Time.time + stingCooldown;
            RecoverFor(0.35f);
        }

        private void ClearMarker()
        {
            if (stingMarker != null) Destroy(stingMarker.gameObject);
            stingMarker = null;
        }

        // ---------- Torch ----------

        private void BeginTorch()
        {
            Enter(State.Torch);
            nextTorchTick = Time.time + 0.1f;
            nextTorchFire = Time.time + 0.3f;
            torchDirection = TorchDirection(0f);
        }

        /// <summary>The flame's direction: from the hose tip down toward the ground near the end of its reach, swung sideways.</summary>
        private Vector3 TorchDirection(float sweep)
        {
            Vector3 flat = Quaternion.AngleAxis(sweep, Vector3.up) * transform.forward;
            flat.y = 0f;
            flat.Normalize();
            float drop = Mathf.Max(0.2f, HoseTipPosition.y - (transform.position.y + 0.6f));
            return (flat * torchRange * 0.8f + Vector3.down * drop).normalized;
        }

        private void UpdateTorch(float dt)
        {
            TurnToward(Target.position, 70f * dt); // follows the player slowly; the sweep does the rest
            float sweep = Mathf.Sin(stateTime / torchTime * Mathf.PI * 2f) * torchSweep;
            torchDirection = TorchDirection(sweep);
            Vector3 from = HoseTipPosition;

            if (Time.time >= nextTorchTick)
            {
                nextTorchTick = Time.time + torchTick;
                Vector3 toPlayer = Target.position + Vector3.up * 0.9f - from;
                if (toPlayer.magnitude <= torchRange && Vector3.Angle(torchDirection, toPlayer) <= torchHalfAngle) HurtPlayer(torchDamage);
                // It lights its own oil as the flame passes over it.
                foreach (OilSlick slick in new List<OilSlick>(OilSlick.All))
                {
                    if (slick == null) continue;
                    Vector3 toSlick = slick.transform.position - from;
                    if (toSlick.magnitude <= torchRange + slick.Radius && Vector3.Angle(torchDirection, toSlick) <= torchHalfAngle + 8f)
                        slick.Ignite(gameObject);
                }
            }
            if (Time.time >= nextTorchFire)
            {
                // Burning fuel drips where the flame licks the ground.
                nextTorchFire = Time.time + 0.4f;
                Vector3 end = from + torchDirection * torchRange * 0.85f;
                FireZone.Spawn(fire, GroundBelow(end), 0.9f, gameObject, transform, 2.2f);
            }
            if (stateTime < torchTime) return;
            nextTorch = Time.time + torchCooldown;
            RecoverFor(0.5f);
        }

        // ---------- Fuel cannon ----------

        private void BeginCannon()
        {
            Enter(State.CannonCharge);
            cannonAim = CannonAimPoint();
            ShowSight(true);
            MiniBossHud.Message("SLUDGE IS CHARGING ITS CANNON!  TAKE COVER!", new Color(1f, 0.45f, 0.2f), 2f);
        }

        private Vector3 CannonAimPoint() => Target.position + Vector3.up * 0.9f;

        private void UpdateCannonCharge(float dt)
        {
            float charge = CannonChargeTime;
            float k = stateTime / charge;
            if (k < cannonAimLock)
            {
                cannonAim = CannonAimPoint();
                TurnToward(Target.position, 150f * dt);
            }
            UpdateSight(k);
            if (Time.time >= nextChargeFx)
            {
                // Venting pressure: sparks at the hose and puffs from the tank, faster as it builds.
                nextChargeFx = Time.time + Mathf.Lerp(0.3f, 0.07f, k);
                ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, HoseTipPosition, Random.onUnitSphere, 4, 0.06f, 2.5f, 60f);
                if (tank != null && Random.value < 0.5f)
                    ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, tank.position + Vector3.up * 0.3f, Vector3.up, 2, 0.4f, 1.2f, 30f);
            }
            if (stateTime < charge) return;
            FireCannon();
        }

        private void FireCannon()
        {
            Vector3 from = HoseTipPosition;
            Vector3 direction = cannonAim - from;
            FuelShell.Launch(from, direction, cannonShellSpeed, cannonRange, cannonBlastRadius, cannonDamage, cannonEdgeDamage,
                fire, cannonFireRadius, cannonFireDuration, gameObject, sparkMaterial, blastMaterial, sightMask);
            ParticleBurst.Spawn(ParticleBurst.Kind.Fireball, from, direction, 14, 0.7f, 6f, 20f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, from, direction, 6, 0.6f, 2f, 30f);
            if (AgentReady) Agent.Move(-transform.forward * 0.4f); // recoil
            nextCannon = Time.time + (Phase == 2 ? phaseTwoCannonCooldown : cannonCooldown);
            Enter(State.CannonFire);
        }

        private void BuildSight()
        {
            var go = new GameObject("Cannon Sight");
            go.transform.SetParent(transform, false);
            sight = go.AddComponent<LineRenderer>();
            sight.sharedMaterial = warningMaterial;
            sight.useWorldSpace = true;
            sight.positionCount = 2;
            sight.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sight.receiveShadows = false;
            sight.numCapVertices = 2;
            sight.enabled = false;
        }

        private void ShowSight(bool on)
        {
            if (sight != null) sight.enabled = on;
        }

        /// <summary>
        /// The red sight line from the hose to whatever is in the way: it ends on the player when they are exposed,
        /// on the cover when they are not. It thickens and pulses faster as the charge builds, and goes solid on lock.
        /// </summary>
        private void UpdateSight(float k)
        {
            if (sight == null) return;
            Vector3 from = HoseTipPosition;
            Vector3 direction = cannonAim - from;
            float length = Mathf.Min(direction.magnitude + 2f, cannonRange);
            Vector3 end = from + direction.normalized * length;
            float nearest = float.MaxValue;
            foreach (RaycastHit hit in Physics.RaycastAll(from, direction.normalized, length, sightMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<OilSlick>() != null) continue;
                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                    end = hit.point;
                }
            }
            bool locked = k >= cannonAimLock;
            float pulse = locked ? 1f : 0.6f + 0.4f * Mathf.Sin(Time.time * Mathf.Lerp(8f, 30f, k));
            float width = Mathf.Lerp(0.025f, 0.08f, k) * pulse;
            sight.startWidth = width;
            sight.endWidth = width * 0.7f;
            sight.SetPosition(0, from);
            sight.SetPosition(1, end);
        }

        // ---------- Phase 2 leak ----------

        /// <summary>Phase 2: a short-lived patch of burning fuel every few steps.</summary>
        private void Leak()
        {
            if (Vector3.Distance(transform.position, lastLeak) < leakSpacing) return;
            lastLeak = transform.position;
            FireZone.Spawn(fire, transform.position - transform.forward * 1.1f, leakRadius, gameObject, transform, leakDuration);
        }

        // ---------- Tank weak spot ----------

        protected override float OnPartHit(int part, float amount, GameObject source)
        {
            bool exposed = state == State.SprayWindup || state == State.Spray || state == State.CannonCharge;
            if (part == 0 && amount >= HeavyHit && exposed && !InPhaseShift)
            {
                Backfire(source);
                return 0f;
            }
            return amount;
        }

        private void Backfire(GameObject source)
        {
            Vector3 at = tank != null ? tank.position : transform.position + Vector3.up;
            Enter(State.Backfired);
            MiniBossHud.Message("BACKFIRE!", new Color(1f, 0.6f, 0.2f), 1.5f);
            FodderExplosion.Explode(at, backfireBlastRadius, backfireBlastDamage, 0.35f, source, transform, blastMaterial, sightMask);
            ParticleBurst.Explosion(at, backfireBlastRadius * 0.5f);
            // Its own fuel burning under it: this fire isn't its own doing, so it hurts.
            FireZone.Spawn(fire, transform.position, 1.8f, source, null, backfireStun);
            Health.TakeDamage(backfireDamage, source);
        }

        // ---------- Phase change, reset, death ----------

        protected override void OnPhaseTwo()
        {
            Enter(State.Stalk);
            if (leak != null) leak.SetActive(true);
            // Fuel gushes out of the punctured tank all around it.
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Random.Range(-0.3f, 0.3f);
                Vector3 p = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 3.2f;
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    OilSlick.Spawn(oilMaterial, fire, hit.position, slickRadius * 0.9f, slickLife, slickSlow, gameObject);
            }
            Vector3 at = tank != null ? tank.position : transform.position + Vector3.up;
            ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, at, Vector3.up, 30, 0.12f, 7f, 80f);
            if (blastMaterial != null) ImpactFlash.Spawn(blastMaterial, at, 1.2f, 0.2f);
        }

        protected override void UpdatePhaseShift(float dt)
        {
            if (Time.time < nextLeakPuff) return;
            nextLeakPuff = Time.time + 0.12f;
            Vector3 at = tank != null ? tank.position : transform.position + Vector3.up;
            ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, at + Random.insideUnitSphere * 0.4f, Random.onUnitSphere + Vector3.up, 3,
                0.5f, 2.5f, 40f, new Color(0.05f, 0.04f, 0.03f, 0.85f));
        }

        protected override void OnPhaseShiftEnded()
        {
            Enter(State.Stalk);
            lastLeak = transform.position;
            nextSpray = Time.time + 0.8f;
            nextCannon = Mathf.Min(nextCannon, Time.time + 4f);
            MiniBossHud.Message("Sludge is leaking fire!", new Color(1f, 0.6f, 0.3f), 1.8f);
        }

        protected override void OnReset()
        {
            Enter(State.Stalk);
            igniteTarget = null;
            ClearMarker();
            ShowSight(false);
            if (leak != null) leak.SetActive(false);
            // Its own oil goes with the reset.
            var mine = new List<OilSlick>();
            foreach (OilSlick slick in OilSlick.All) if (slick != null && slick.Owner == gameObject) mine.Add(slick);
            foreach (OilSlick slick in mine) Destroy(slick.gameObject);
        }

        protected override void OnBossDeath()
        {
            if (leak != null) leak.SetActive(false);
            ClearMarker();
            ShowSight(false);
            if (pilotFlame != null) pilotFlame.Extinguish();
            if (torchFlame != null) torchFlame.Extinguish();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ClearMarker();
        }

        // ---------- Animation ----------
        //
        // Every joint eases toward a target pose (nothing snaps). The body plates bend against turns (the rear swings
        // wide) and ripple as it walks; the tail sways in a wave and its joints follow each other with a growing delay,
        // so strikes whip; the legs walk in two alternating sets of four; the claws bob, open and lunge.

        private Quaternion[] segmentRest = new Quaternion[0], segmentPose = new Quaternion[0];
        private Quaternion[] tailRest = new Quaternion[0], tailPose = new Quaternion[0];
        private Quaternion[] legRest = new Quaternion[0], legPose = new Quaternion[0];
        private Quaternion[] kneeRest = new Quaternion[0], kneePose = new Quaternion[0];
        private Quaternion[] clawRest = new Quaternion[0], clawPose = new Quaternion[0];
        private Quaternion[] elbowRest = new Quaternion[0], elbowPose = new Quaternion[0];
        private Quaternion[] fingerRest = new Quaternion[0], mouthRest = new Quaternion[0];
        private float[] fingerOpen = new float[0];
        private Quaternion bodyLean = Quaternion.identity;
        private Vector3 bodyOffset;
        private float gaitPhase, lastYaw, turnRate;

        private void CaptureRig()
        {
            Capture(bodySegments, out segmentRest, out segmentPose);
            Capture(tail, out tailRest, out tailPose);
            Capture(legs, out legRest, out legPose);
            Capture(legKnees, out kneeRest, out kneePose);
            Capture(claws, out clawRest, out clawPose);
            Capture(clawElbows, out elbowRest, out elbowPose);
            Capture(clawFingers, out fingerRest, out _);
            Capture(mouthparts, out mouthRest, out _);
            fingerOpen = new float[clawFingers.Length];
            lastYaw = transform.eulerAngles.y;
        }

        private static void Capture(Transform[] items, out Quaternion[] rest, out Quaternion[] pose)
        {
            rest = new Quaternion[items.Length];
            pose = new Quaternion[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                rest[i] = items[i] != null ? items[i].localRotation : Quaternion.identity;
                pose[i] = Quaternion.identity;
            }
        }

        private static float Ease(float rate, float dt) => 1f - Mathf.Exp(-rate * dt);
        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Raise: degrees up (negative down). Sweep: degrees forward. For a limb running out along -X (left) or +X (right).</summary>
        private static Quaternion LimbRotation(bool left, float raise, float sweep)
        {
            float sign = left ? 1f : -1f;
            return Quaternion.Euler(0f, sign * sweep, -sign * raise);
        }

        protected override void AnimateBoss(float dt)
        {
            float speed = AgentReady ? Agent.velocity.magnitude : 0f;
            float yaw = transform.eulerAngles.y;
            float rawTurn = dt > 0f ? Mathf.DeltaAngle(lastYaw, yaw) / dt : 0f;
            lastYaw = yaw;
            turnRate = Mathf.Lerp(turnRate, rawTurn, Ease(6f, dt));
            float walk = Mathf.Clamp01(speed / Mathf.Max(0.1f, walkSpeed));
            float turning = Mathf.Clamp01(Mathf.Abs(turnRate) / 120f);
            float stepping = Mathf.Max(walk, turning * 0.8f);
            bool backfired = state == State.Backfired;
            gaitPhase += dt * Mathf.Lerp(1.5f, 10f, stepping);
            if (backfired) gaitPhase += dt * 12f;

            float k = StateProgress();
            AnimateBody(dt, walk, backfired, k);
            AnimateTail(dt, k);
            AnimateClaws(dt, walk, k);
            AnimateLegs(dt, stepping, backfired);
            for (int i = 0; i < mouthparts.Length; i++)
                if (mouthparts[i] != null)
                    mouthparts[i].localRotation = mouthRest[i] * Quaternion.Euler(0f, (i % 2 == 0 ? 1f : -1f) * Mathf.Sin(Time.time * 8f + i * Mathf.PI) * 18f, 0f);

            // Tank: gurgles before a spray, shakes harder as the cannon pressurises.
            if (tank != null)
            {
                float gurgle = state == State.SprayWindup ? 0.03f : state == State.CannonCharge ? 0.01f + 0.04f * k : Phase == 2 ? 0.012f : 0f;
                tank.localPosition = tankRest + Random.insideUnitSphere * gurgle;
            }
            UpdateHoseFlames(k);
        }

        /// <summary>0 to 1 through the current wind-up or attack, for posing.</summary>
        private float StateProgress()
        {
            switch (state)
            {
                case State.SprayWindup: return Mathf.Clamp01(stateTime / SprayWindupTime);
                case State.Spray: return Mathf.Clamp01(stateTime / sprayTime);
                case State.ClawWindup: return Mathf.Clamp01(stateTime / clawWindup);
                case State.StingWindup: return Mathf.Clamp01(stateTime / stingWindup);
                case State.TorchWindup: return Mathf.Clamp01(stateTime / torchWindup);
                case State.Torch: return Mathf.Clamp01(stateTime / torchTime);
                case State.CannonCharge: return Mathf.Clamp01(stateTime / CannonChargeTime);
                case State.CannonFire: return Mathf.Clamp01(stateTime / Mathf.Max(0.1f, cannonRecover));
                case State.IgniteWindup: return Mathf.Clamp01(stateTime / igniteWindup);
                default: return 0f;
            }
        }

        private void AnimateBody(float dt, float walk, bool backfired, float k)
        {
            // Whole body: crouch to brace for the cannon, recoil when it fires, rock with the gait, roll over on a backfire.
            Vector3 offset = Vector3.up * (Mathf.Abs(Mathf.Sin(gaitPhase)) - 0.5f) * 0.03f * walk;
            float pitch = 0f, roll = Mathf.Sin(gaitPhase) * 1.5f * walk;
            switch (state)
            {
                case State.CannonCharge: offset += Vector3.down * 0.1f * Smooth(k / 0.3f); pitch = -4f * Smooth(k / 0.3f); break;
                case State.CannonFire: pitch = -9f * (1f - Smooth(k)); offset += Vector3.down * 0.06f * (1f - k); break;
                case State.SprayWindup: pitch = -3f * k; break;
                case State.ClawWindup: pitch = 3f * k; offset += Vector3.down * 0.05f * k; break;
                case State.Claw: pitch = 5f; break;
                case State.StingWindup: pitch = 5f * k; offset += Vector3.down * 0.05f * k; break;
                case State.Sting: pitch = 7f; break;
            }
            Quaternion lean = Quaternion.Euler(pitch, 0f, roll);
            if (backfired) { lean = Quaternion.Euler(0f, 0f, 60f + Mathf.Sin(Time.time * 8f) * 8f); offset = Vector3.up * 0.3f; }
            bodyLean = Quaternion.Slerp(bodyLean, lean, Ease(backfired ? 6f : 10f, dt));
            bodyOffset = Vector3.Lerp(bodyOffset, offset, Ease(12f, dt));
            Visual.localPosition = VisualRest + bodyOffset;
            Visual.localRotation = VisualRestRotation * bodyLean;

            // Plates: each one bends against the turn (so the rear swings wide), ripples with the steps and breathes.
            int count = bodySegments.Length;
            for (int i = 0; i < count; i++)
            {
                if (bodySegments[i] == null) continue;
                float bend = i == 0 ? 0f : Mathf.Clamp(-turnRate * 0.05f, -11f, 11f);
                float ripple = i == 0 ? 0f : Mathf.Sin(gaitPhase * 0.5f - i * 0.9f) * 3.5f * walk;
                float breathe = Mathf.Sin(Time.time * 1.7f - i * 0.6f) * 1.2f;
                float arch = 0f;
                if (state == State.CannonCharge || state == State.SprayWindup || state == State.Spray) arch = i == 0 ? -5f : 2.5f;
                if (state == State.StingWindup || state == State.Sting) arch = i == 0 ? 3f : -2f;
                if (backfired) { bend = Mathf.Sin(Time.time * 9f - i) * 12f; arch = Mathf.Sin(Time.time * 7f + i) * 6f; }
                Quaternion want = Quaternion.Euler(breathe + arch, bend + ripple, 0f);
                segmentPose[i] = Quaternion.Slerp(segmentPose[i], want, Ease(Mathf.Max(3f, 9f - i * 1.2f), dt));
                bodySegments[i].localRotation = segmentRest[i] * segmentPose[i];
            }
        }

        private void AnimateTail(float dt, float k)
        {
            // Curl: negative curls the tail further forward over the back; positive straightens and rears it back.
            float curl = Mathf.Sin(Time.time * 1.3f) * 2.5f, swayAmount = 6f, strikeRate = 14f, tremble = 0f, sweepYaw = 0f;
            switch (state)
            {
                case State.SprayWindup: curl = -14f * Smooth(k); swayAmount = 2f; break;
                case State.Spray: curl = -14f + Mathf.Sin(stateTime * 25f) * 4f; swayAmount = 1f; strikeRate = 20f; break;
                case State.IgniteWindup: curl = -10f; swayAmount = 2f; break;
                case State.CannonCharge: curl = 6f * Smooth(k / 0.4f); swayAmount = 0f; tremble = 1.5f * k; break;
                case State.CannonFire: curl = 16f * (1f - Smooth(k)); swayAmount = 0f; strikeRate = 30f; break;
                case State.StingWindup: curl = 16f * Smooth(k); swayAmount = 0f; strikeRate = 12f; break;
                case State.Sting: curl = stateTime < 0.25f ? -26f : -12f; swayAmount = 0f; strikeRate = 45f; break;
                case State.TorchWindup: curl = -12f * Smooth(k); swayAmount = 1f; break;
                case State.Torch:
                    curl = -12f; swayAmount = 0f;
                    sweepYaw = Mathf.Sin(stateTime / torchTime * Mathf.PI * 2f) * torchSweep * 0.35f;
                    break;
                case State.ClawWindup:
                case State.Claw: curl = -6f; swayAmount = 3f; break;
                case State.Backfired: curl = 20f + Mathf.Sin(Time.time * 6f) * 8f; swayAmount = 14f; strikeRate = 6f; break;
            }
            float turnLag = Mathf.Clamp(-turnRate * 0.04f, -10f, 10f);
            for (int i = 0; i < tail.Length; i++)
            {
                if (tail[i] == null) continue;
                float wave = Mathf.Sin(Time.time * 1.6f - i * 0.7f) * swayAmount;
                float yawOffset = wave + (i == 0 ? turnLag : 0f) + (i >= tail.Length - 2 ? sweepYaw : 0f);
                float shake = tremble > 0f ? Random.Range(-tremble, tremble) : 0f;
                Quaternion want = Quaternion.Euler(curl + shake, yawOffset, 0f);
                // Joints further out follow more slowly: strikes whip, swings trail.
                tailPose[i] = Quaternion.Slerp(tailPose[i], want, Ease(strikeRate * (1f - i * 0.1f), dt));
                tail[i].localRotation = tailRest[i] * tailPose[i];
            }
        }

        private void AnimateClaws(float dt, float walk, float k)
        {
            for (int c = 0; c < claws.Length; c++)
            {
                bool left = c == 0;
                float side = left ? -1f : 1f;
                // Shoulder: pitch (negative raises), yaw (positive turns outward). Elbow: negative bends the forearm up.
                float pitch = Mathf.Sin(gaitPhase + c * Mathf.PI) * 4f * walk + Mathf.Sin(Time.time * 1.4f + c) * 2f;
                float yawOut = 4f + Mathf.Sin(Time.time * 1.1f + c * 2f) * 3f;
                float elbow = -8f, open = 6f + Mathf.Sin(Time.time * 2f + c) * 4f, rate = 10f;
                switch (state)
                {
                    case State.ClawWindup:
                        pitch = -22f * Smooth(k); yawOut = 4f + 18f * Smooth(k); elbow = -8f - 30f * Smooth(k); open = 42f * Smooth(k); rate = 16f;
                        break;
                    case State.Claw:
                    {
                        // Alternate claws snap: the active one thrusts in and shuts, the other cocks back.
                        int snap = Mathf.Min(snaps, ComboSnaps) - 1;
                        bool active = snap >= 0 && snap % 2 == c;
                        float u = (stateTime - Mathf.Max(0, snap) * SnapInterval) / SnapInterval;
                        if (active) { pitch = -2f; yawOut = -10f; elbow = 4f; open = u < 0.35f ? 0f : 18f; }
                        else { pitch = -18f; yawOut = 16f; elbow = -30f; open = 38f; }
                        rate = 34f;
                        break;
                    }
                    case State.SprayWindup:
                    case State.Spray:
                    case State.CannonCharge:
                        pitch = 8f; yawOut = 12f; elbow = 4f; open = 10f; break; // braced
                    case State.Torch:
                    case State.TorchWindup:
                        pitch = -6f; yawOut = 24f; elbow = -20f; open = 14f; break; // held clear of the flame
                    case State.StingWindup:
                        pitch = -10f; yawOut = 20f; elbow = -24f; open = 25f; break;
                    case State.Backfired:
                        pitch = Mathf.Sin(Time.time * 14f + c) * 25f; yawOut = Mathf.Sin(Time.time * 11f + c) * 20f;
                        elbow = -20f; open = 30f; rate = 12f;
                        break;
                }
                if (claws[c] != null)
                {
                    clawPose[c] = Quaternion.Slerp(clawPose[c], Quaternion.Euler(pitch, side * yawOut, 0f), Ease(rate, dt));
                    claws[c].localRotation = clawRest[c] * clawPose[c];
                }
                if (c < clawElbows.Length && clawElbows[c] != null)
                {
                    elbowPose[c] = Quaternion.Slerp(elbowPose[c], Quaternion.Euler(elbow, 0f, 0f), Ease(rate, dt));
                    clawElbows[c].localRotation = elbowRest[c] * elbowPose[c];
                }
                if (c < clawFingers.Length && clawFingers[c] != null)
                {
                    fingerOpen[c] = Mathf.Lerp(fingerOpen[c], open, Ease(rate * 1.5f, dt));
                    clawFingers[c].localRotation = fingerRest[c] * Quaternion.Euler(-fingerOpen[c], 0f, 0f);
                }
            }
        }

        private void AnimateLegs(float dt, float stepping, bool backfired)
        {
            bool braced = state == State.CannonCharge || state == State.CannonFire || state == State.Spray || state == State.Torch;
            for (int i = 0; i < legs.Length; i++)
            {
                bool left = i < legs.Length / 2;
                int pair = left ? i : i - legs.Length / 2;
                // Two sets of four step in turn (left 1 and 3 with right 2 and 4), each pair a little behind the one in front.
                int set = (pair + (left ? 0 : 1)) % 2;
                float phase = gaitPhase + set * Mathf.PI + pair * 0.35f;
                float sweep = -Mathf.Cos(phase) * 16f * stepping;
                float lift = Mathf.Max(0f, Mathf.Sin(phase)) * 20f * stepping;
                float knee = lift * 0.7f;
                if (braced) { lift -= 6f; knee -= 8f; }
                if (backfired)
                {
                    sweep = Mathf.Sin(Time.time * 22f + i * 1.7f) * 28f;
                    lift = Mathf.Sin(Time.time * 19f + i) * 25f;
                    knee = Mathf.Sin(Time.time * 17f + i * 2f) * 25f;
                }
                if (legs[i] != null)
                {
                    legPose[i] = Quaternion.Slerp(legPose[i], LimbRotation(left, lift, sweep), Ease(22f, dt));
                    legs[i].localRotation = legRest[i] * legPose[i];
                }
                if (i < legKnees.Length && legKnees[i] != null)
                {
                    kneePose[i] = Quaternion.Slerp(kneePose[i], LimbRotation(left, knee, 0f), Ease(22f, dt));
                    legKnees[i].localRotation = kneeRest[i] * kneePose[i];
                }
            }
        }

        /// <summary>Keeps the pilot and torch flames on the hose tip, pointed where they should be.</summary>
        private void UpdateHoseFlames(float k)
        {
            Vector3 tip = HoseTipPosition;
            if (pilotFlame != null)
            {
                Vector3 aim = state == State.CannonCharge ? cannonAim - tip : hoseTip != null ? hoseTip.forward : transform.forward;
                pilotFlame.transform.position = tip;
                if (aim.sqrMagnitude > 0.0001f) pilotFlame.transform.rotation = Quaternion.LookRotation(aim);
                float want = state == State.CannonCharge ? 0.3f + 0.7f * k : state == State.IgniteWindup || state == State.TorchWindup ? 0.35f : 0f;
                pilotFlame.Strength = Mathf.MoveTowards(pilotFlame.Strength, want, Time.deltaTime * 4f);
                pilotFlame.transform.localScale = Vector3.one * (state == State.CannonCharge ? 1f + 1.5f * k : 1f);
            }
            if (torchFlame != null)
            {
                torchFlame.transform.position = tip;
                if (torchDirection.sqrMagnitude > 0.0001f) torchFlame.transform.rotation = Quaternion.LookRotation(torchDirection);
                float want = state == State.Torch ? 1f : 0f;
                torchFlame.Strength = Mathf.MoveTowards(torchFlame.Strength, want, Time.deltaTime * 6f);
            }
        }

        protected override void Paint()
        {
            if (igniter == null) return;
            // The igniter flickers hot before a spark, flame or shot, idles as a small blue pilot light.
            bool hot = state == State.IgniteWindup || state == State.TorchWindup || state == State.Torch || state == State.CannonCharge;
            Color color = hot
                ? (Random.value < 0.5f ? new Color(1f, 0.95f, 0.6f) : new Color(1f, 0.5f, 0.15f))
                : igniterColor * (0.7f + 0.3f * Mathf.Sin(Time.time * 6f));
            color.a = 1f;
            igniter.GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            igniter.SetPropertyBlock(block);
        }
    }
}
