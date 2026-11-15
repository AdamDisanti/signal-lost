using System.Collections;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using UnityEngine;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// Cindermaw, the Ash Drake (the Mesa Pass; docs/desert-bosses.md): a wingless ground dragon with the ship's rocket
    /// thruster fused into its spine. Reworked 11-10 after Lawson's first look: real particle smoke
    /// (<see cref="SmokeCloud"/>), molten cracks that flow, and a more acrobatic, thruster-driven moveset.
    ///
    /// Phase 1 (Smoulder): the raking claws (right, left, then it rears with both claws up, holds, and crashes them
    /// down); the tail whip (one way, then back the other; jump them); the thruster pounce (a lit crouch, a rocket leap
    /// onto where you're heading, a crushing landing); the drift charge (a lunge that steers after you and ends in a
    /// skidding tail sweep); flame breath; the fire lob (three molten globs at marked spots).
    /// Phase 2 (Smokescreen): it vents smoke and hides in it, then pounces or charges out (the thruster's glow and roar
    /// give it away); afterburner sidesteps out of your line, straight into a follow-up; its charges leave smoke.
    /// Phase 3 (Afterburner): charges come in pairs; the rear slam (a thruster hop, then a ring of fire rolls out).
    /// Phase 4 (Meltdown): faster; the Inferno Circuit (a lap leaving a ring of fire, then charges across); the
    /// Supernova (it rears as the thruster goes white-hot for three seconds, then explodes round itself); it overheats
    /// after its big moves (stalled, venting, double damage).
    ///
    /// Weak spot: the thruster bell (double damage, triple while overheated).
    /// </summary>
    public sealed class Cindermaw : DuelBoss
    {
        [Header("Cindermaw")]
        [SerializeField] private Material flameMaterial;
        [Tooltip("The thruster bell's hitbox (a weak spot).")]
        [SerializeField] private Health thruster;
        [Tooltip("The orange light in the thruster: its glow is the tell for every charge, even through smoke.")]
        [SerializeField] private Light nozzleLight;
        [SerializeField] private FireStyle fire = new FireStyle { damage = 8f, duration = 4f, smoke = 0.6f };
        [SerializeField, Min(0f)] private float clawDamage = 20f;
        [SerializeField, Min(0f)] private float slamDamage = 32f;
        [SerializeField, Min(0f)] private float tailDamage = 24f;
        [SerializeField, Min(0f)] private float chargeDamage = 32f;
        [SerializeField, Min(0f)] private float pounceDamage = 30f;
        [SerializeField, Min(0f)] private float breathTick = 7f;
        [SerializeField, Min(0f)] private float globDamage = 18f;
        [SerializeField, Min(0f)] private float ringDamage = 22f;
        [SerializeField, Min(0f)] private float novaDamage = 45f;
        [SerializeField, Min(1f)] private float chargeDistance = 18f;
        [SerializeField, Min(1f)] private float chargeSpeed = 22f;

        private Material ember;
        private bool overheated;
        private float glowTarget = 1.5f, glow;

        protected override void OnBossStarted()
        {
            base.OnBossStarted();
            RelayPart(thruster, 0);
            SmokeCloud.AddStirrer(transform);
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
                foreach (Material m in r.materials)
                    if (m.name.StartsWith("CindermawEmber")) ember = m;
            if (nozzleLight != null) nozzleLight.intensity = 1.5f;
            Ember(1f);
        }

        protected override void DefineAttacks()
        {
            Attacks.Add(new Attack { Name = "RakeCombo", Run = RakeCombo, MaxRange = 7f, MaxAngle = 60f, Weight = 3f, Cooldown = 4f });
            Attacks.Add(new Attack { Name = "TailWhip", Run = TailWhip, MaxRange = 10f, MinAngle = 60f, Weight = 5f, Cooldown = 4f });
            Attacks.Add(new Attack { Name = "Pounce", Run = () => Pounce(false), MinRange = 8f, MaxRange = 24f, MaxAngle = 70f, Weight = 3f, Cooldown = 6f, HyperArmor = true });
            Attacks.Add(new Attack { Name = "DriftCharge", Run = () => DriftCharge(DuelPhase >= 3 ? 2 : 1), MinRange = 9f, MaxRange = 34f, MaxAngle = 80f, Weight = 2.5f, Cooldown = 6f, HyperArmor = true });
            Attacks.Add(new Attack { Name = "Breath", Run = Breath, MinRange = 4f, MaxRange = 14f, MaxAngle = 50f, Weight = 2.5f, Cooldown = 7f });
            Attacks.Add(new Attack { Name = "FireLob", Run = FireLob, MinRange = 8f, MaxRange = 28f, Weight = 2.5f, Cooldown = 8f });
            Attacks.Add(new Attack { Name = "Sidestep", Run = Sidestep, MaxRange = 14f, MinPhase = 2, Weight = 2f, Cooldown = 5f });
            Attacks.Add(new Attack { Name = "Smokescreen", Run = Smokescreen, MinPhase = 2, Weight = 2.5f, Cooldown = 15f, HyperArmor = true, When = () => SmokeCloud.All.Count < 2 });
            Attacks.Add(new Attack { Name = "RearSlam", Run = RearSlam, MinPhase = 3, MaxRange = 11f, Weight = 2.5f, Cooldown = 8f, HyperArmor = true });
            Attacks.Add(new Attack { Name = "InfernoCircuit", Run = InfernoCircuit, MinPhase = 4, Weight = 3f, Cooldown = 20f, HyperArmor = true });
            Attacks.Add(new Attack { Name = "Supernova", Run = Supernova, MinPhase = 4, Weight = 2.5f, Cooldown = 25f, HyperArmor = true });
        }

        protected override void OnDuelPhase(int phase) => Ember(phase >= 4 ? 3f : phase == 3 ? 2.2f : phase == 2 ? 1.5f : 1f);

        protected override void OnDuelReset()
        {
            overheated = false;
            Ember(1f);
        }

        protected override void OnMoveEnded()
        {
            overheated = false;
            glowTarget = 1.5f;
        }

        protected override float OnPartHit(int part, float amount, GameObject source) => amount * (overheated ? 3f : 2f);

        protected override void AnimateBoss(float dt)
        {
            base.AnimateBoss(dt);
            glow = Mathf.MoveTowards(glow, glowTarget, dt * 30f);
            float flicker = 1f + 0.12f * Mathf.Sin(Time.time * 23f) + (DuelPhase >= 4 ? 0.25f * Mathf.Sin(Time.time * 7f) : 0f);
            if (nozzleLight != null) nozzleLight.intensity = glow * flicker;
        }

        private void Ember(float strength)
        {
            if (ember == null) return;
            ember.SetFloat("_Glow", 1.6f * strength);
            ember.SetFloat("_FlowSpeed", 1.2f + strength * 0.5f);
        }

        private Vector3 Ahead => (BonePosition("Mouth") - BonePosition("Head")).normalized;

        private void Exhaust(float perSecond)
        {
            if (Emit("exhaust", perSecond) <= 0) return;
            Vector3 nozzle = BonePosition("Nozzle");
            Vector3 back = (nozzle - BonePosition("Spine")).normalized;
            ParticleBurst.Spawn(ParticleBurst.Kind.Fireball, nozzle, back, 2, 0.8f, 9f, 12f);
            if (DuelPhase >= 2 && Random.value < 0.3f)
                ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, nozzle, back + Vector3.up, 1, 1.4f, 3f, 25f);
        }

        // ---------- Raking claws ----------

        private IEnumerator RakeCombo()
        {
            LookDuringMove(true);
            Play("RakeCombo");
            float[] hits = Table.Times(Playing, "hit"), moves = Table.Times(Playing, "move");
            for (int i = 0; i < hits.Length; i++)
            {
                bool slam = i == 2;
                // The slam waits up high: track slowly so a dodge made too early still gets followed.
                yield return UntilTime(moves[i], () => CloseIn(slam ? 5f : 4f, slam ? 110f : 150f));
                Sfx.PlayAt(slam ? "laser_slam" : "chain_swish", transform.position, 1f, slam ? 0.55f : 0.6f, 45f);
                string part = i == 0 ? "HandR" : i == 1 ? "HandL" : null;
                yield return StrikeWindow(hits[i] - 0.1f, hits[i] + 0.12f,
                    () => part != null ? BonePosition(part) : (BonePosition("HandL") + BonePosition("HandR")) * 0.5f,
                    slam ? 3.2f : 2.2f, slam ? slamDamage : clawDamage, slam ? 4f : 0.8f, false, () => Lurch(slam ? 3f : 4.5f));
                if (slam)
                {
                    Vector3 ground = GroundBelow((BonePosition("HandL") + BonePosition("HandR")) * 0.5f);
                    ParticleBurst.Spawn(ParticleBurst.Kind.Sand, ground + Vector3.up * 0.3f, Vector3.up, 16, 1.2f, 6f, 60f);
                    if (DuelPhase >= 3) Keep(FireRing.Spawn(flameMaterial, ground, 1.5f, 8f, 9f * Tempo, ringDamage * 0.8f, 1.1f, gameObject));
                }
                if (i < 2 && Target != null && FlatDistance(Target.position) > 10f) break;
            }
            yield return Finish(() => Track(30f));
        }

        // ---------- Tail whip ----------

        private IEnumerator TailWhip()
        {
            Play("TailWhip");
            float[] starts = Table.Times(Playing, "hit_start"), ends = Table.Times(Playing, "hit_end");
            for (int i = 0; i < starts.Length; i++)
            {
                yield return UntilTime(starts[i] - 0.1f, () => Track(50f));
                Sfx.PlayAt("chain_swish", transform.position, 1f, 0.45f, 50f);
                yield return StrikeWindow(starts[i], ends[i], () =>
                {
                    Vector3 club = BonePosition("Club"), mid = BonePosition("Tail2");
                    return BossFx.DistanceToPlayer(club) < BossFx.DistanceToPlayer(mid) ? club : mid;
                }, 2.6f, tailDamage, 6f, true);
            }
            yield return Finish();
        }

        // ---------- Thruster pounce ----------

        private IEnumerator Pounce(bool fromSmoke)
        {
            LookDuringMove(false);
            Play("PounceStart", 0.1f);
            glowTarget = 9f;
            Sfx.PlayAt("rev", transform.position, 1f, 0.9f, 60f);
            if (fromSmoke) Sfx.PlayAt("kong_roar", transform.position, 0.8f, 1.2f, 80f);
            float launch = Moment("launch");
            yield return UntilTime(launch, () => Track(260f));
            // Leaps for where the player is heading.
            Vector3 target = BossFx.PlayerFeet;
            if (BossFx.PlayerBody != null) target += BossFx.PlayerBody.velocity * 0.45f;
            target = ClampToArena(target - FlatDirectionTo(target) * 2.2f, 3f);
            GroundMarker marker = Marker(target, 3.8f, true);
            SetAirborne(true);
            Play("PounceAir", 0.08f);
            glowTarget = 14f;
            Sfx.PlayAt("car_engine", transform.position, 1f, 1.5f, 70f);
            Vector3 from = transform.position;
            float distance = Vector3.Distance(from, target);
            float flight = Mathf.Clamp(distance / 20f, 0.55f, 1.1f) / Tempo, t = 0f, height = 3f + distance * 0.18f;
            Vector3 face = target - from;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(face);
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / flight);
                Vector3 p = Vector3.Lerp(from, target, t);
                p.y += Mathf.Sin(t * Mathf.PI) * height;
                transform.position = p;
                marker.Progress = t;
                Exhaust(40f);
                yield return null;
            }
            SetAirborne(false);
            Play("PounceLand", 0.04f);
            glowTarget = 3f;
            Sfx.PlayAt("car_explode", target, 1f, 0.6f, 70f);
            Strike(target + Vector3.up, 3.6f, pounceDamage, 5f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, target + Vector3.up * 0.3f, Vector3.up, 24, 1.6f, 7f, 70f);
            if (DuelPhase >= 2) Keep(SmokeCloud.Spawn(target, 3f, 6f));
            if (DuelPhase >= 3) Keep(FireRing.Spawn(flameMaterial, target, 1.5f, 9f, 10f * Tempo, ringDamage, 1.2f, gameObject));
            DamageTakenMultiplier = 1.25f;
            yield return Finish();
        }

        // ---------- Drift charge ----------

        private IEnumerator DriftCharge(int chain)
        {
            yield return Windup(1.0f);
            for (int i = 0; i < chain; i++)
            {
                var blocked = new Ref<bool>();
                yield return Charge(blocked, true);
                if (blocked.Value)
                {
                    yield return Crash();
                    yield break;
                }
                if (i < chain - 1)
                {
                    Play("Skid", 0.08f);
                    Sfx.PlayAt("car_skid", transform.position, 1f, 0.8f, 50f);
                    yield return Seconds(0.45f / Tempo, () => Track(520f));
                    yield return Windup(0.45f);
                }
            }
            // The end of the charge: a skidding tail sweep, then it shakes its head (the opening).
            Play("SkidSweep", 0.06f);
            Sfx.PlayAt("car_skid", transform.position, 1f, 1f, 50f);
            float from = Moment("hit_start"), to = Moment("hit_end");
            yield return StrikeWindow(from, to, () => BonePosition("Club"), 2.8f, tailDamage, 6f, true);
            DamageTakenMultiplier = 1.25f;
            yield return Finish();
            if (DuelPhase >= 4) yield return Overheat(2.5f);
        }

        /// <summary>The tell: it crouches, the thruster glows and whines; it aims for the first 70%, then locks.</summary>
        private IEnumerator Windup(float seconds)
        {
            LookDuringMove(false);
            Play("LungeWindup", 0.12f);
            glowTarget = 10f;
            Sfx.PlayAt("rev", transform.position, 1f, 0.7f + 0.1f * DuelPhase, 60f);
            seconds /= Tempo;
            float end = Time.time + seconds;
            yield return Seconds(seconds, () =>
            {
                if (end - Time.time > seconds * 0.3f) Track(280f);
            });
        }

        /// <summary>The charge. A drifting charge keeps steering toward the player (slowly: sidestep late).</summary>
        private IEnumerator Charge(Ref<bool> blocked, bool drift)
        {
            Play("Charge", 0.06f);
            glowTarget = 14f;
            Sfx.PlayAt("car_engine", transform.position, 1f, 1.4f, 70f);
            bool hit = false;
            float travelled = 0f, nextFire = 1f, nextSmoke = 4f;
            while (travelled < chargeDistance)
            {
                if (drift && Target != null) TurnToward(Target.position, 32f * Tempo * Time.deltaTime);
                float step = chargeSpeed * Tempo * Time.deltaTime;
                Vector3 before = transform.position;
                if (AgentReady) Agent.Move(transform.forward * step);
                float moved = Flat(transform.position - before);
                travelled += step;
                if (moved < step * 0.3f && travelled > 1.5f)
                {
                    blocked.Value = true;
                    break;
                }
                if (!hit && Strike(BonePosition("Chest"), 2.9f, chargeDamage, 6f)) hit = true;
                Exhaust(40f);
                if (travelled >= nextFire)
                {
                    nextFire += 2f;
                    Keep(FireZone.Spawn(fire, GroundBelow(transform.position - transform.forward * 2f), 1.4f, gameObject, transform, DuelPhase >= 3 ? 5f : 3.5f));
                }
                if (DuelPhase >= 2 && travelled >= nextSmoke)
                {
                    nextSmoke += 6f;
                    Keep(SmokeCloud.Spawn(GroundBelow(transform.position - transform.forward * 3f), 2.6f, 7f));
                }
                yield return null;
            }
            glowTarget = 3f;
        }

        private IEnumerator Crash()
        {
            Sfx.PlayAt("car_crash", transform.position, 1f, 0.7f, 60f);
            Play("Skid", 0.05f);
            DamageTakenMultiplier = 1.5f;
            yield return Seconds(Table.Length("Skid") * 1.4f);
        }

        // ---------- Breath and the fire lob ----------

        private IEnumerator Breath()
        {
            LookDuringMove(false);
            Play("Breath");
            float start = Moment("breath_start"), end = Moment("breath_end");
            yield return UntilTime(start - 0.4f, () => Track(130f));
            Sfx.PlayAt("magic_charge", transform.position, 0.9f, 0.5f, 50f);
            yield return UntilTime(start);
            Sfx.PlayAt("fire_crackle", transform.position, 1f, 0.7f, 60f);
            float nextTick = 0f, nextFire = 0f;
            yield return UntilTime(end, () =>
            {
                Vector3 mouth = BonePosition("Mouth");
                Vector3 dir = Ahead;
                dir.y = Mathf.Min(dir.y, -0.12f);
                dir.Normalize();
                if (Emit("breath", 30f) > 0) ParticleBurst.Spawn(ParticleBurst.Kind.Fireball, mouth, dir, 3, 1.1f, 15f, 9f);
                if (Time.time >= nextTick)
                {
                    nextTick = Time.time + 0.15f;
                    Vector3 to = BossFx.PlayerChest - mouth;
                    if (to.magnitude < 11.5f && Vector3.Angle(new Vector3(dir.x, 0f, dir.z), new Vector3(to.x, 0f, to.z)) < 17f) Hurt(breathTick);
                }
                if (Time.time >= nextFire)
                {
                    nextFire = Time.time + 0.22f;
                    Keep(FireZone.Spawn(fire, GroundBelow(mouth + new Vector3(dir.x, 0f, dir.z).normalized * 7f), 1.6f, gameObject, transform, 4f));
                }
            });
            yield return Finish();
        }

        private IEnumerator FireLob()
        {
            Play("FireLob");
            float[] spits = Table.Times(Playing, "spit");
            for (int i = 0; i < spits.Length; i++)
            {
                yield return UntilTime(spits[i], () => Track(160f));
                // The first glob at the player, the others where they'll dodge to.
                Vector3 aim = BossFx.PlayerFeet + (i == 0 ? Vector3.zero : (Vector3)(Random.insideUnitCircle * 4.5f).XZ());
                if (BossFx.PlayerBody != null) aim += BossFx.PlayerBody.velocity * 0.5f;
                Vector3 ground = ClampToArena(aim, 2f);
                StartCoroutine(Glob(BonePosition("Mouth"), ground, 1.1f + 0.1f * i));
                Sfx.PlayAt("laser_shot_weak", transform.position, 1f, 0.4f, 60f);
            }
            yield return Finish();
        }

        private IEnumerator Glob(Vector3 from, Vector3 ground, float flight)
        {
            GroundMarker marker = GroundMarker.Spawn(markerMaterial, null, ground, 2.4f);
            GameObject ball = BossFx.Prop("Molten Glob", puffMesh, flameMaterial, from, Random.rotation, Vector3.one * 0.7f);
            float t = 0f, height = 4f + Vector3.Distance(from, ground) * 0.2f;
            while (t < 1f && ball != null)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / flight);
                Vector3 p = Vector3.Lerp(from, ground, t);
                p.y += Mathf.Sin(t * Mathf.PI) * height;
                ball.transform.position = p;
                ball.transform.Rotate(300f * Time.deltaTime, 200f * Time.deltaTime, 0f);
                if (marker != null) marker.Progress = t;
                if (Random.value < 0.4f) ParticleBurst.Spawn(ParticleBurst.Kind.Fireball, p, Vector3.up, 1, 0.5f, 1f, 30f);
                yield return null;
            }
            if (ball != null) Destroy(ball);
            if (marker != null) Destroy(marker.gameObject);
            if (IsDead || !Engaged) yield break;
            ParticleBurst.Explosion(ground, 1.8f);
            Sfx.PlayAt("car_explode", ground, 0.7f, 1.1f, 50f);
            if (BossFx.DistanceToPlayer(ground + Vector3.up * 0.5f) < 2.3f) Hurt(globDamage);
            Keep(FireZone.Spawn(fire, ground, 1.8f, gameObject, transform, 5f));
        }

        // ---------- Sidestep, smokescreen ----------

        /// <summary>An afterburner sidestep out of the player's line, straight into a follow-up.</summary>
        private IEnumerator Sidestep()
        {
            bool left = Random.value < 0.5f;
            Play(left ? "DashL" : "DashR", 0.05f);
            yield return Until("go", 0);
            Sfx.PlayAt("rev", transform.position, 1f, 1.3f, 50f);
            Vector3 side = Vector3.Cross(Vector3.up, transform.forward) * (left ? -1f : 1f);
            yield return Travel(side, 6.5f, 0.3f, _ => Exhaust(40f), null, false);
            yield return Seconds(0.1f, () => Track(500f));
            float d = Target != null ? FlatDistance(Target.position) : 10f;
            if (d < 7f) yield return RakeCombo();
            else if (d < 14f) yield return Breath();
            else yield return Pounce(false);
        }

        private IEnumerator Smokescreen()
        {
            Play("Vent");
            yield return Until("smoke", 0, () => Track(90f));
            Sfx.PlayAt("rev", transform.position, 1f, 0.45f, 70f);
            float life = DuelPhase >= 4 ? 24f : 16f;
            Keep(SmokeCloud.Spawn(GroundBelow(transform.position), 5.5f, life));
            int extra = DuelPhase >= 3 ? 3 : 2;
            float start = Random.value * 360f;
            for (int i = 0; i < extra; i++)
            {
                float a = (start + i * 360f / extra) * Mathf.Deg2Rad;
                Vector3 p = Home + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ArenaRadius * 0.5f;
                Keep(SmokeCloud.Spawn(GroundBelow(p), 5.5f, life));
            }
            ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, BonePosition("Nozzle"), Vector3.up, 14, 2.5f, 6f, 30f);
            yield return Finish();
            yield return Ambush();
        }

        /// <summary>Slips into the cloud furthest from the player, waits, then flares and comes out of it.</summary>
        private IEnumerator Ambush()
        {
            SmokeCloud hide = null;
            float best = -1f;
            foreach (SmokeCloud c in SmokeCloud.All)
            {
                if (c == null || c.Contains(BossFx.PlayerChest, 2f)) continue;
                float d = BossFx.FlatDistanceToPlayer(c.transform.position);
                if (d > best)
                {
                    best = d;
                    hide = c;
                }
            }
            if (hide == null) yield break;
            glowTarget = 0.6f;
            float give = Time.time + 4f;
            Agent.updateRotation = true;
            Play("Run", 0.2f, false);
            while (Time.time < give && !hide.Contains(transform.position + Vector3.up, -1f))
            {
                Chase(hide.transform.position, RunSpeed);
                yield return null;
            }
            Halt();
            Agent.updateRotation = false;
            Play("Idle", 0.25f, false);
            yield return Seconds(Random.Range(1f, 2.2f), () =>
            {
                Track(200f);
                if (Emit("hidepuff", 3f) > 0) hide.Puff(transform.position + Vector3.up * 2f, 3, 2f);
            });
            // Out of the smoke: a pounce or a charge, both told by the thruster's flare and roar.
            if (Random.value < 0.5f)
            {
                yield return Pounce(true);
                yield break;
            }
            Sfx.PlayAt("kong_roar", transform.position, 0.8f, 1.2f, 80f);
            yield return Windup(0.8f);
            var blocked = new Ref<bool>();
            yield return Charge(blocked, false);
            if (blocked.Value) yield return Crash();
            else
            {
                Play("Skid", 0.08f);
                DamageTakenMultiplier = 1.25f;
                yield return Finish();
            }
        }

        // ---------- Rear slam ----------

        private IEnumerator RearSlam()
        {
            Play("RearSlam");
            float move = Moment("move"), hit = Moment("hit");
            yield return UntilTime(move - 0.3f, () => Track(150f));
            // A thruster hop at the top, for a heavier landing.
            glowTarget = 12f;
            Sfx.PlayAt("rev", transform.position, 1f, 1.1f, 60f);
            yield return UntilTime(hit, () =>
            {
                Lurch(5f);
                Exhaust(30f);
            });
            Vector3 ground = GroundBelow(BonePosition("Chest"));
            Strike(ground + Vector3.up, 4.2f, slamDamage, 5f);
            Sfx.PlayAt("car_explode", ground, 1f, 0.6f, 70f);
            Keep(FireRing.Spawn(flameMaterial, ground, 2.5f, 18f, 10f * Tempo, ringDamage, 1.4f, gameObject));
            if (DuelPhase >= 4)
            {
                yield return Seconds(0.6f);
                Keep(FireRing.Spawn(flameMaterial, ground, 2.5f, 18f, 12f * Tempo, ringDamage, 1.4f, gameObject));
            }
            glowTarget = 3f;
            yield return Finish();
        }

        // ---------- Inferno Circuit, Supernova, overheat ----------

        private IEnumerator InfernoCircuit()
        {
            Sfx.PlayAt("kong_roar", transform.position, 1f, 0.7f, 90f);
            yield return Windup(0.9f);
            Play("Charge", 0.06f);
            glowTarget = 16f;
            Sfx.PlayAt("car_engine", transform.position, 1f, 1.5f, 80f);
            float radius = ArenaRadius * 0.7f;
            Vector3 from = transform.position - Home;
            float angle = Mathf.Atan2(from.z, from.x);
            float lap = 0f, nextFire = 0f, speed = chargeSpeed * 0.9f;
            bool hit = false;
            while (lap < Mathf.PI * 2f)
            {
                lap += speed * Tempo * Time.deltaTime / radius;
                Vector3 goal = Home + new Vector3(Mathf.Cos(angle + lap), 0f, Mathf.Sin(angle + lap)) * radius;
                Vector3 step = goal - transform.position;
                step.y = 0f;
                if (step.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(step);
                if (AgentReady) Agent.Move(Vector3.ClampMagnitude(step, speed * Tempo * Time.deltaTime * 1.5f));
                if (!hit && Strike(BonePosition("Chest"), 2.9f, chargeDamage, 6f)) hit = true;
                Exhaust(30f);
                if (lap * radius >= nextFire)
                {
                    nextFire += 2.2f;
                    Keep(FireZone.Spawn(fire, GroundBelow(transform.position), 1.5f, gameObject, transform, 9f));
                }
                yield return null;
            }
            for (int i = 0; i < 2; i++)
            {
                Play("Skid", 0.08f);
                yield return Seconds(0.35f / Tempo, () => Track(600f));
                yield return Windup(0.5f);
                var blocked = new Ref<bool>();
                yield return Charge(blocked, true);
                if (blocked.Value) break;
            }
            yield return Overheat(4f);
        }

        private IEnumerator Supernova()
        {
            Play("SupernovaCharge", 0.2f, false);
            MiniBossHud.Message("Cindermaw's thruster is going critical!", new Color(1f, 0.85f, 0.6f), 2.5f);
            Sfx.PlayAt("magic_charge", transform.position, 1f, 0.4f, 90f);
            GroundMarker marker = Marker(GroundBelow(transform.position), 10f);
            float charge = 3f / Tempo, t = 0f;
            yield return Seconds(charge, () =>
            {
                t += Time.deltaTime;
                glowTarget = Mathf.Lerp(6f, 30f, t / charge);
                marker.Progress = t / charge;
                if (nozzleLight != null) nozzleLight.color = Color.Lerp(new Color(1f, 0.5f, 0.15f), Color.white, t / charge);
                Exhaust(20f);
            });
            Play("SupernovaBlast", 0.04f, false);
            yield return Until("blast", 0);
            Vector3 centre = GroundBelow(transform.position);
            ParticleBurst.Explosion(centre + Vector3.up, 6f);
            Sfx.PlayAt("car_explode", centre, 1f, 0.4f, 120f);
            if (BossFx.FlatDistanceToPlayer(centre) < 10f) Hurt(novaDamage);
            Keep(FireRing.Spawn(flameMaterial, centre, 10f, 20f, 12f, ringDamage, 1.4f, gameObject));
            if (nozzleLight != null) nozzleLight.color = new Color(1f, 0.5f, 0.15f);
            yield return Finish();
            yield return Overheat(5f);
        }

        private IEnumerator Overheat(float seconds)
        {
            overheated = true;
            DamageTakenMultiplier = 2f;
            glowTarget = 4f;
            Play("Overheat", 0.2f, false);
            Sfx.PlayAt("electric_sizzle", transform.position, 1f, 0.6f, 50f);
            yield return Seconds(seconds, () =>
            {
                if (Emit("vent", 8f) > 0)
                    ParticleBurst.Spawn(ParticleBurst.Kind.Smoke, BonePosition("Nozzle"), Vector3.up, 2, 1.6f, 3f, 25f);
            });
            overheated = false;
            DamageTakenMultiplier = 1f;
        }
    }
}
