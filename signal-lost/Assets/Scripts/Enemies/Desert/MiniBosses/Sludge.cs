using System.Collections.Generic;
using SignalLost.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// Mini-boss: a giant scorpion with the ship's fuel tank strapped to its back and a fuel hose in place of its
    /// stinger, with a spark igniter on the tip.
    ///
    /// Phase 1:
    /// - Oil spray: the tail curls forward and the tank gurgles (the telegraph), then it lobs globs of fuel around the
    ///   player. Each leaves an oil slick that slows the player.
    /// - Ignite: when the player is near one of its slicks, the igniter flickers, then it sparks the slick into fire.
    ///   It never lights a slick it is standing in, and its own fires don't hurt it.
    /// - Claw snap: a double pincer snap when the player is close in front.
    ///
    /// Phase 2 (tank punctured): oil gushes out around it, it leaks a burning trail wherever it walks, and it sprays
    /// more globs, more often.
    ///
    /// Counterplay:
    /// - A heavy hit (a charged shot or the slam) on the fuel tank while it is winding up or spraying makes it
    ///   backfire: the tank explodes on its back, it takes big damage and is stunned, lying in its own fire.
    /// - Fires the player starts hurt it: shoot a slick it is standing in. Beetle blasts light slicks too.
    /// - Fire hurts fodder, so lure them into the flames.
    /// All numbers are placeholders.
    /// </summary>
    public sealed class Sludge : DesertMiniBoss
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private enum State { Stalk, SprayWindup, Spray, IgniteWindup, ClawWindup, Claw, Recover, Backfired }

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 3f;
        [Tooltip("Distance it likes to keep from the player while spraying.")]
        [SerializeField, Min(2f)] private float preferredRange = 7f;

        [Header("Oil spray")]
        [Tooltip("Tail curling forward and the tank gurgling: the warning, and the backfire window.")]
        [SerializeField, Min(0.1f)] private float sprayWindup = 0.8f;
        [SerializeField, Min(0.1f)] private float phaseTwoSprayWindup = 0.6f;
        [SerializeField, Min(0.1f)] private float sprayTime = 0.5f;
        [SerializeField, Range(1, 8)] private int sprayGlobs = 3;
        [SerializeField, Range(1, 10)] private int phaseTwoSprayGlobs = 5;
        [SerializeField, Min(2f)] private float sprayRange = 15f;
        [Tooltip("How far around the player the globs land (the first one lands on them).")]
        [SerializeField, Min(0f)] private float sprayScatter = 2.6f;
        [SerializeField, Min(0.5f)] private float slickRadius = 2f;
        [SerializeField, Min(1f)] private float slickLife = 14f;
        [Tooltip("Fraction of the player's movement taken away while standing in oil.")]
        [SerializeField, Range(0f, 0.95f)] private float slickSlow = 0.5f;
        [SerializeField, Min(0f)] private float sprayCooldown = 4.5f;
        [SerializeField, Min(0f)] private float phaseTwoSprayCooldown = 2.7f;

        [Header("Igniter")]
        [SerializeField, Min(0.1f)] private float igniteWindup = 0.6f;
        [SerializeField, Min(0f)] private float igniteCooldown = 3f;
        [Tooltip("Lights a slick when the player is within this distance of its edge.")]
        [SerializeField, Min(0f)] private float igniteNearPlayer = 3f;
        [SerializeField] private FireStyle fire = new FireStyle { damage = 9f, interval = 0.5f, duration = 4f };

        [Header("Claws")]
        [SerializeField, Min(0.5f)] private float clawRange = 3f;
        [SerializeField, Min(0.1f)] private float clawWindup = 0.5f;
        [SerializeField, Min(0.5f)] private float clawReach = 3.3f;
        [SerializeField, Range(10f, 180f)] private float clawArc = 100f;
        [Tooltip("Damage of each of the two snaps.")]
        [SerializeField, Min(0f)] private float clawDamage = 12f;
        [SerializeField] private Knockback clawKnockback = new Knockback(KnockbackMode.Shove, 5f);
        [SerializeField, Min(0f)] private float clawCooldown = 1.6f;

        [Header("Fuel tank weak spot")]
        [Tooltip("Health on the tank's own collider. A heavy hit while it sprays makes it backfire.")]
        [SerializeField] private Health tankHealth;
        [SerializeField, Min(0f)] private float backfireDamage = 70f;
        [SerializeField, Min(0.5f)] private float backfireBlastRadius = 3.2f;
        [SerializeField, Min(0f)] private float backfireBlastDamage = 22f;
        [SerializeField] private Knockback backfireKnockback = new Knockback(KnockbackMode.Launch, 8f, 5f);
        [SerializeField, Min(0f)] private float backfireStun = 3.2f;
        [SerializeField] private Material blastMaterial;

        [Header("Phase 2: punctured tank")]
        [Tooltip("Metres walked between patches of its burning trail.")]
        [SerializeField, Min(0.3f)] private float leakSpacing = 1.8f;
        [SerializeField, Min(0.2f)] private float leakRadius = 0.9f;
        [SerializeField, Min(0.2f)] private float leakDuration = 2.5f;

        [Header("Rig")]
        [SerializeField] private Transform[] tail = new Transform[0];
        [Tooltip("Where oil globs leave the hose.")]
        [SerializeField] private Transform hoseTip;
        [SerializeField] private Renderer igniter;
        [SerializeField] private Transform[] claws = new Transform[0];
        [Tooltip("The moving upper finger of each pincer.")]
        [SerializeField] private Transform[] clawFingers = new Transform[0];
        [SerializeField] private Transform[] legs = new Transform[0];
        [SerializeField] private Transform tank;
        [Tooltip("Gushing fuel, shown once the tank is punctured.")]
        [SerializeField] private GameObject leak;
        [SerializeField] private Material oilMaterial;
        [SerializeField] private Material warningMaterial;
        [SerializeField] private Material sparkMaterial;

        private State state = State.Stalk;
        private float stateTime, recoverFor, nextSpray, nextIgnite, nextClaw, nextRepath, nextLeakPuff;
        private int globsSent, snaps;
        private Vector3 sprayAt, lastLeak;
        private OilSlick igniteTarget;
        private Quaternion[] tailRest = new Quaternion[0], legRest = new Quaternion[0], fingerRest = new Quaternion[0];
        private Vector3 tankRest;
        private Color igniterColor = new Color(0.3f, 0.6f, 1f);
        private MaterialPropertyBlock block;

        protected override void Awake()
        {
            base.Awake();
            block = new MaterialPropertyBlock();
            tailRest = Capture(tail);
            legRest = Capture(legs);
            fingerRest = Capture(clawFingers);
            if (tank != null) tankRest = tank.localPosition;
            if (igniter != null && igniter.sharedMaterial != null && igniter.sharedMaterial.HasProperty(BaseColor))
                igniterColor = igniter.sharedMaterial.GetColor(BaseColor);
            if (leak != null) leak.SetActive(false);
        }

        private static Quaternion[] Capture(Transform[] items)
        {
            var rest = new Quaternion[items.Length];
            for (int i = 0; i < items.Length; i++) rest[i] = items[i] != null ? items[i].localRotation : Quaternion.identity;
            return rest;
        }

        protected override void OnBossStarted() => RelayPart(tankHealth, 0);

        private void Enter(State next)
        {
            state = next;
            stateTime = 0f;
            if (next != State.Stalk) Halt();
            if (next == State.Spray) globsSent = 0;
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
            lastLeak = transform.position;
        }

        protected override void TickCombat(float dt)
        {
            stateTime += dt;
            if (Phase == 2) Leak();
            if (!TargetAlive) return;
            switch (state)
            {
                case State.Stalk: UpdateStalk(); break;
                case State.SprayWindup:
                    if (stateTime < SprayWindupTime * 0.8f) TurnToward(Target.position, 200f * dt);
                    if (stateTime >= SprayWindupTime) Enter(State.Spray);
                    break;
                case State.Spray: UpdateSpray(); break;
                case State.IgniteWindup: UpdateIgnite(dt); break;
                case State.ClawWindup:
                    if (stateTime < clawWindup * 0.7f) TurnToward(Target.position, 260f * dt);
                    if (stateTime >= clawWindup) Enter(State.Claw);
                    break;
                case State.Claw: UpdateClaw(); break;
                case State.Recover: if (stateTime >= recoverFor) Enter(State.Stalk); break;
                case State.Backfired: if (stateTime >= backfireStun) RecoverFor(0.4f); break;
            }
        }

        private float SprayWindupTime => Phase == 2 ? phaseTwoSprayWindup : sprayWindup;

        private void UpdateStalk()
        {
            float distance = FlatDistance(Target.position);
            bool seen = CanSee(sprayRange + 6f);
            if (seen) LastSeen = Time.time;

            if (Time.time >= nextClaw && distance <= clawRange && AngleToTarget() < 50f) { Enter(State.ClawWindup); return; }
            if (Time.time >= nextSpray && seen && distance >= 2.5f && distance <= sprayRange) { Enter(State.SprayWindup); return; }
            if (Time.time >= nextIgnite && FindSlickToIgnite(out igniteTarget)) { Enter(State.IgniteWindup); return; }

            if (Time.time < nextRepath) return;
            nextRepath = Time.time + 0.3f;
            if (distance > preferredRange + 2f) Chase(Target.position, walkSpeed);
            else if (distance < 3.5f && Time.time < nextClaw)
            {
                Vector3 away = transform.position - FlatDirectionTo(Target.position) * 3.5f;
                if (NavMesh.SamplePosition(away, out NavMeshHit hit, 2f, NavMesh.AllAreas)) Chase(hit.position, walkSpeed);
            }
            else
            {
                Vector3 side = Vector3.Cross(Vector3.up, FlatDirectionTo(Target.position)) * (Mathf.Sin(Time.time * 0.3f) > 0f ? 1f : -1f);
                Chase(transform.position + side * 3f, walkSpeed * 0.8f);
            }
        }

        private void UpdateSpray()
        {
            int total = Phase == 2 ? phaseTwoSprayGlobs : sprayGlobs;
            int due = Mathf.Min(total, 1 + Mathf.FloorToInt(stateTime / sprayTime * total));
            while (globsSent < due)
            {
                Vector3 target = Target.position;
                if (globsSent > 0)
                {
                    float angle = globsSent * Mathf.PI * 2f / Mathf.Max(1, total - 1) + Random.Range(-0.4f, 0.4f);
                    target += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * sprayScatter * Random.Range(0.7f, 1.2f);
                }
                if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2.5f, NavMesh.AllAreas)) target = hit.position;
                Vector3 from = hoseTip != null ? hoseTip.position : transform.position + Vector3.up * 2f;
                OilGlob.Launch(oilMaterial, warningMaterial, fire, from, GroundBelow(target), 0.7f + FlatDistance(target) * 0.03f,
                    slickRadius, slickLife, slickSlow, gameObject);
                globsSent++;
            }
            if (stateTime < sprayTime) return;
            nextSpray = Time.time + (Phase == 2 ? phaseTwoSprayCooldown : sprayCooldown);
            nextIgnite = Mathf.Min(nextIgnite, Time.time + 1f);
            RecoverFor(0.3f);
        }

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
            Vector3 from = hoseTip != null ? hoseTip.position : transform.position + Vector3.up * 2f;
            if (sparkMaterial != null)
            {
                ImpactFlash.Spawn(sparkMaterial, from, 0.4f, 0.12f);
                for (int i = 1; i <= 4; i++)
                    ImpactFlash.Spawn(sparkMaterial, Vector3.Lerp(from, igniteTarget.transform.position + Vector3.up * 0.2f, i / 4f), 0.18f, 0.12f);
            }
            igniteTarget.Ignite(gameObject); // its own fire: doesn't hurt it
            igniteTarget = null;
            nextIgnite = Time.time + igniteCooldown;
            RecoverFor(0.4f);
        }

        private void UpdateClaw()
        {
            float[] at = { 0f, 0.2f };
            while (snaps < at.Length && stateTime >= at[snaps])
            {
                snaps++;
                if (FlatDistance(Target.position) <= clawReach && AngleToTarget() <= clawArc * 0.5f)
                    HurtPlayer(clawDamage, clawKnockback);
            }
            if (stateTime < 0.35f) return;
            nextClaw = Time.time + clawCooldown;
            RecoverFor(0.5f);
        }

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
            bool spraying = state == State.SprayWindup || state == State.Spray;
            if (part == 0 && amount >= HeavyHit && spraying && !InPhaseShift)
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
            FodderExplosion.Explode(at, backfireBlastRadius, backfireBlastDamage, 0.35f, source, transform, blastMaterial, sightMask, backfireKnockback);
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
            if (blastMaterial != null) ImpactFlash.Spawn(blastMaterial, tank != null ? tank.position : transform.position, 1.2f, 0.2f);
        }

        protected override void UpdatePhaseShift(float dt)
        {
            if (Time.time < nextLeakPuff || oilMaterial == null) return;
            nextLeakPuff = Time.time + 0.1f;
            Vector3 at = tank != null ? tank.position : transform.position + Vector3.up;
            ImpactFlash.Spawn(oilMaterial, at + Random.insideUnitSphere * 0.5f, 0.4f, 0.3f);
        }

        protected override void OnPhaseShiftEnded()
        {
            Enter(State.Stalk);
            lastLeak = transform.position;
            nextSpray = Time.time + 0.8f;
            MiniBossHud.Message("Sludge is leaking fire!", new Color(1f, 0.6f, 0.3f), 1.8f);
        }

        protected override void OnReset()
        {
            Enter(State.Stalk);
            igniteTarget = null;
            if (leak != null) leak.SetActive(false);
            // Its own oil goes with the reset.
            var mine = new List<OilSlick>();
            foreach (OilSlick slick in OilSlick.All) if (slick != null && slick.Owner == gameObject) mine.Add(slick);
            foreach (OilSlick slick in mine) Destroy(slick.gameObject);
        }

        protected override void OnBossDeath()
        {
            if (leak != null) leak.SetActive(false);
        }

        // ---------- Animation ----------

        protected override void AnimateBoss(float dt)
        {
            float speed = AgentReady ? Agent.velocity.magnitude : 0f;

            // Tail: idle sway; curls forward to aim the hose for the spray and the spark.
            float curl = state == State.SprayWindup ? Mathf.Clamp01(stateTime / SprayWindupTime) :
                state == State.Spray ? 1f - 0.3f * Mathf.Sin(stateTime / sprayTime * Mathf.PI) :
                state == State.IgniteWindup ? 0.6f : 0f;
            for (int i = 0; i < tail.Length; i++)
            {
                if (tail[i] == null) continue;
                float sway = Mathf.Sin(Time.time * 1.6f - i * 0.7f) * 6f;
                tail[i].localRotation = tailRest[i] * Quaternion.Euler(-14f * curl, sway, 0f);
            }

            // Pincers: open in the wind-up, snap shut twice.
            float open = state == State.ClawWindup ? Mathf.Clamp01(stateTime / clawWindup) * 40f :
                state == State.Claw ? (stateTime % 0.2f < 0.08f ? 0f : 30f) : 6f + Mathf.Sin(Time.time * 2f) * 4f;
            for (int i = 0; i < clawFingers.Length; i++)
                if (clawFingers[i] != null) clawFingers[i].localRotation = fingerRest[i] * Quaternion.Euler(-open, 0f, 0f);
            for (int i = 0; i < claws.Length; i++)
                if (claws[i] != null)
                    claws[i].localRotation = Quaternion.Euler(state == State.ClawWindup ? -20f * Mathf.Clamp01(stateTime / clawWindup) : 0f,
                        (i == 0 ? 1f : -1f) * (state == State.Claw ? -15f : 0f), 0f);

            float rate = 5f + speed * 4f;
            for (int i = 0; i < legs.Length; i++)
                if (legs[i] != null)
                {
                    float swing = state == State.Backfired ? Mathf.Sin(Time.time * 25f + i) * 30f :
                        Mathf.Sin(Time.time * rate + i * 1.3f) * Mathf.Clamp(speed * 10f, 0f, 22f);
                    legs[i].localRotation = legRest[i] * Quaternion.Euler(0f, swing, 0f);
                }

            // Tank gurgles before a spray; the body rolls over when it backfires.
            if (tank != null)
            {
                float gurgle = state == State.SprayWindup ? 0.03f : Phase == 2 ? 0.012f : 0f;
                tank.localPosition = tankRest + Random.insideUnitSphere * gurgle;
            }
            Quaternion lean = state == State.Backfired ? Quaternion.Euler(0f, 0f, 60f + Mathf.Sin(Time.time * 8f) * 8f) : Quaternion.identity;
            Visual.localPosition = VisualRest + (state == State.Backfired ? Vector3.up * 0.3f : Vector3.zero);
            Visual.localRotation = VisualRestRotation * lean;
        }

        protected override void Paint()
        {
            if (igniter == null) return;
            // The igniter flickers hot before a spark, idles as a small blue pilot light.
            bool lighting = state == State.IgniteWindup;
            Color color = lighting
                ? (Random.value < 0.5f ? new Color(1f, 0.95f, 0.6f) : new Color(1f, 0.5f, 0.15f))
                : igniterColor * (0.7f + 0.3f * Mathf.Sin(Time.time * 6f));
            color.a = 1f;
            igniter.GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            igniter.SetPropertyBlock(block);
        }
    }
}
