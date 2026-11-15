using System.Collections;
using System.Collections.Generic;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Player;
using UnityEngine;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// Pitchclaw, the Oilstone Scorpion (the Buried Hull; docs/desert-bosses.md). Reworked 11-10 after Lawson's first
    /// look: armoured all over in rock with fuel flowing in the cracks, a ten-metre tail it uses like a third arm, real
    /// liquid fuel (<see cref="FuelDrop"/>, <see cref="FuelPool"/>) and a far bigger moveset.
    ///
    /// Phase 1 (Stone): the nearer claw snaps; the pincer combo (left, right, a held-back left, a scissor of both); the
    /// tail lash (low and wide round its side, jump it); the stab (cocks, holds, throws the stinger over its head and
    /// sticks); the fuel spew (a stream of fuel that splashes, runs down walls and pools).
    /// Phase 2 (Spark): the stinger barrage (three fast tracking stabs); the tail snare (flings the tail out ten
    /// metres to hook you; caught, you're held up and crushed by both claws, then flung); the spark scrape (lights any
    /// fuel near its claws); the burrow (it sinks into the sand, a trail of churning sand hunts you, and it erupts
    /// claws-first under the marked spot).
    /// Phase 3 (Cracked): the leaping crush (springs across the arena onto you, splashing fuel round where it lands)
    /// and the fuel rain (sprays fuel high into the air to rain down round it). The pincer combo ends in a stab.
    /// Phase 4 (Pyre): its fuel burns: the spew is a flamethrower, every pool it makes is on fire, standing close
    /// burns; the inferno spin (two turns with the tail out, flinging burning fuel).
    ///
    /// Weak spot: the fuel tank on its back (extra damage; a charged shot backfires it into a long stagger). Burning
    /// pools hurt it too when the player lit them.
    /// </summary>
    public sealed class Pitchclaw : DuelBoss
    {
        [Header("Pitchclaw")]
        [Tooltip("The droplets (glossy black).")]
        [SerializeField] private Material oilMaterial;
        [Tooltip("The pools on the ground (the flowing-oil shader).")]
        [SerializeField] private Material poolMaterial;
        [SerializeField] private Material flameMaterial;
        [SerializeField] private Material sparkMaterial;
        [SerializeField] private Material rockMaterial;
        [Tooltip("The fuel tank's hitbox (a weak spot).")]
        [SerializeField] private Health tank;
        [SerializeField] private FireStyle fire = new FireStyle { damage = 9f, duration = 5f, smoke = 0.7f };
        [SerializeField, Min(0f)] private float snapDamage = 22f;
        [SerializeField, Min(0f)] private float comboDamage = 18f;
        [SerializeField, Min(0f)] private float scissorDamage = 30f;
        [SerializeField, Min(0f)] private float lashDamage = 24f;
        [SerializeField, Min(0f)] private float stabDamage = 28f;
        [SerializeField, Min(0f)] private float dropDamage = 4f;
        [SerializeField, Min(0f)] private float snareDamage = 8f;
        [SerializeField, Min(0f)] private float crushDamage = 12f;
        [SerializeField, Min(0f)] private float throwDamage = 14f;
        [SerializeField, Min(0f)] private float eruptDamage = 30f;
        [SerializeField, Min(0f)] private float leapDamage = 32f;
        [SerializeField, Min(0f)] private float spinDamage = 26f;
        [SerializeField, Min(0f)] private float auraTick = 5f;
        [Tooltip("How far in front of it the stinger lands: it shuffles to put you there before it stabs.")]
        [SerializeField, Min(1f)] private float stabReach = 5.4f;

        private Material oilGlow;
        private float nextAura;
        private bool Burning => DuelPhase >= 4;

        protected override void OnBossStarted()
        {
            base.OnBossStarted();
            RelayPart(tank, 0);
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
                foreach (Material m in r.materials)
                    if (m.name.StartsWith("PitchclawOil")) oilGlow = m;
            Glow(1f);
        }

        protected override void DefineAttacks()
        {
            Attacks.Add(new Attack { Name = "Snap", Run = Snap, MaxRange = 7.5f, MaxAngle = 65f, Weight = 2.5f, Cooldown = 2f });
            Attacks.Add(new Attack { Name = "PincerCombo", Run = PincerCombo, MaxRange = 8f, MaxAngle = 55f, Weight = 3f, Cooldown = 5f });
            Attacks.Add(new Attack { Name = "TailLash", Run = TailLash, MaxRange = 11f, MinAngle = 60f, Weight = 5f, Cooldown = 4f });
            Attacks.Add(new Attack { Name = "Stab", Run = Stab, MinRange = 2f, MaxRange = 8f, MaxAngle = 70f, Weight = 2.5f, Cooldown = 4f });
            Attacks.Add(new Attack { Name = "Spew", Run = Spew, MinRange = 6f, MaxRange = 18f, MaxAngle = 50f, Weight = 2.5f, Cooldown = 8f });
            Attacks.Add(new Attack { Name = "StingerBarrage", Run = StingerBarrage, MinRange = 3f, MaxRange = 10f, MaxAngle = 70f, MinPhase = 2, Weight = 2.5f, Cooldown = 7f, HyperArmor = true });
            Attacks.Add(new Attack { Name = "TailSnare", Run = TailSnare, MinRange = 5f, MaxRange = 11f, MaxAngle = 35f, MinPhase = 2, Weight = 2.5f, Cooldown = 10f, HyperArmor = true });
            Attacks.Add(new Attack { Name = "Scrape", Run = Scrape, MaxRange = 11f, MinPhase = 2, Weight = 3f, Cooldown = 7f, When = FuelNearby });
            Attacks.Add(new Attack { Name = "Burrow", Run = Burrow, MinRange = 7f, MinPhase = 2, Weight = 2f, Cooldown = 14f, HyperArmor = true });
            Attacks.Add(new Attack { Name = "LeapCrush", Run = LeapCrush, MinRange = 8f, MaxRange = 22f, MinPhase = 3, Weight = 2.5f, Cooldown = 9f, HyperArmor = true });
            Attacks.Add(new Attack { Name = "FuelRain", Run = FuelRain, MinPhase = 3, Weight = 2f, Cooldown = 12f });
            Attacks.Add(new Attack { Name = "InfernoSpin", Run = InfernoSpin, MaxRange = 10f, MinPhase = 4, Weight = 3f, Cooldown = 10f, HyperArmor = true });
        }

        protected override void OnDuelPhase(int phase)
        {
            if (phase == 3) Crumble();
            Glow(phase >= 4 ? 3.5f : phase == 3 ? 2f : phase == 2 ? 1.4f : 1f);
        }

        protected override void OnDuelReset() => Glow(1f);

        protected override float OnPartHit(int part, float amount, GameObject source)
        {
            if (amount >= HeavyHit && !Staggered)
            {
                MiniBossHud.Message("The fuel tank backfired!", new Color(1f, 0.7f, 0.3f), 2f);
                Sfx.PlayAt("car_explode", BonePosition("Abdomen"), 0.8f, 1.2f, 50f);
                ParticleBurst.Explosion(BonePosition("Abdomen") + Vector3.up, 1.6f);
                ForceStagger(2.5f);
            }
            return amount * 1.5f;
        }

        protected override void TickCombat(float dt)
        {
            base.TickCombat(dt);
            if (!Burning || Time.time < nextAura) return;
            nextAura = Time.time + 0.5f;
            Vector3 body = BonePosition("Body");
            if (BossFx.FlatDistanceToPlayer(body) < 5f) Hurt(auraTick);
            ParticleBurst.Spawn(ParticleBurst.Kind.Fireball, body + Random.insideUnitSphere * 1.5f + Vector3.up, Vector3.up, 2, 0.8f, 2f, 30f);
        }

        protected override void AnimateBoss(float dt)
        {
            base.AnimateBoss(dt);
            if (oilGlow != null && Burning) oilGlow.SetFloat("_Glow", 3.5f + 1.2f * Mathf.Sin(Time.time * 5f));
        }

        private void Glow(float strength)
        {
            if (oilGlow == null) return;
            oilGlow.SetFloat("_Glow", strength);
            oilGlow.SetColor("_GlowColor", strength >= 3f ? new Color(1f, 0.5f, 0.12f) : new Color(1f, 0.42f, 0.1f));
            oilGlow.SetFloat("_FlowSpeed", 2.2f + strength * 0.6f);
        }

        /// <summary>Phase 3: rock breaks off its back and claws.</summary>
        private void Crumble()
        {
            Sfx.PlayAt("wood_smash", transform.position, 1f, 0.5f, 60f);
            foreach (string bone in new[] { "Body", "Abdomen", "ClawL", "ClawR", "Tail3", "Tail7" })
                for (int i = 0; i < 3; i++)
                    BossPuff.Spawn(puffMesh, rockMaterial, BonePosition(bone) + Random.insideUnitSphere * 0.6f,
                        Random.onUnitSphere * 4f + Vector3.up * 6f, 2.4f, Random.Range(0.35f, 0.7f), 0.2f, 18f);
        }

        private bool FuelNearby()
        {
            if (Burning) return true;
            foreach (FuelPool pool in FuelPool.All)
                if (pool != null && !pool.Burning && Flat(pool.transform.position - transform.position) < 12f) return true;
            return false;
        }

        private Vector3 ClawTip(string side) => BonePosition("Claw" + side) * 0.4f + BonePosition("Finger" + side) * 0.6f;
        private Vector3 Jaws => (ClawTip("L") + ClawTip("R")) * 0.5f;

        /// <summary>Throws one drop of fuel (burning in the last phase).</summary>
        private void Drop(Vector3 from, Vector3 velocity, float volume)
        {
            Keep(FuelDrop.Throw(puffMesh, Burning ? flameMaterial : oilMaterial, poolMaterial, fire, from, velocity, volume, dropDamage,
                Burning, gameObject));
        }

        // ---------- Pincers ----------

        private IEnumerator Snap()
        {
            LookDuringMove(true);
            // The claw nearer the player.
            Vector3 feet = BossFx.PlayerFeet;
            string side = (ClawTip("L") - feet).sqrMagnitude < (ClawTip("R") - feet).sqrMagnitude ? "L" : "R";
            Play("Snap" + side);
            float move = Moment("move"), hit = Moment("hit");
            yield return UntilTime(move, () => CloseIn(5f, 180f));
            Sfx.PlayAt("crab_snip", transform.position, 1f, 0.5f, 40f);
            yield return StrikeWindow(hit - 0.1f, hit + 0.12f, () => ClawTip(side), 2.1f, snapDamage, 3f, false, () => Lurch(6f));
            yield return Finish(() => Track(40f));
        }

        private IEnumerator PincerCombo()
        {
            LookDuringMove(true);
            Play("PincerCombo");
            float[] hits = Table.Times(Playing, "hit"), moves = Table.Times(Playing, "move");
            for (int i = 0; i < hits.Length; i++)
            {
                bool scissor = i == 3, held = i == 2;
                yield return UntilTime(moves[i], () => CloseIn(5f, held ? 90f : 160f));
                Sfx.PlayAt(scissor ? "laser_slam" : "crab_snip", transform.position, 1f, scissor ? 0.6f : 0.5f, 50f);
                int index = i;
                yield return StrikeWindow(hits[i] - 0.1f, hits[i] + 0.12f,
                    () => index == 3 ? Jaws : index == 1 ? ClawTip("R") : ClawTip("L"),
                    scissor ? 2.8f : 2.1f, scissor ? scissorDamage : comboDamage, scissor ? 4f : 0.8f, false, () => Lurch(scissor ? 3f : 4f));
            }
            if (DuelPhase >= 3 && Target != null && FlatDistance(Target.position) < 8f)
            {
                yield return UntilTime(Table.Length(Playing) - 0.5f);
                yield return Stab();
                yield break;
            }
            yield return Finish();
        }

        // ---------- The tail ----------

        private IEnumerator TailLash()
        {
            Play("TailLash");
            float from = Moment("hit_start"), to = Moment("hit_end");
            yield return UntilTime(from - 0.12f, () => Track(60f));
            Sfx.PlayAt("chain_swish", transform.position, 1f, 0.4f, 50f);
            yield return StrikeWindow(from, to, ClosestTail, 2.3f, lashDamage, 6f, true);
            yield return Finish();
        }

        private Vector3 ClosestTail()
        {
            Vector3 best = BonePosition("Stinger");
            float d = BossFx.DistanceToPlayer(best);
            for (int i = 3; i <= 12; i += 3)
            {
                Vector3 p = BonePosition("Tail" + i);
                float pd = BossFx.DistanceToPlayer(p);
                if (pd < d)
                {
                    d = pd;
                    best = p;
                }
            }
            return best;
        }

        /// <summary>Shuffles to put the player where the stinger lands.</summary>
        private void Line(float rate)
        {
            Track(rate);
            if (Target == null) return;
            float d = FlatDistance(Target.position);
            if (d > stabReach + 0.6f) Lurch(3.5f);
            else if (d < stabReach - 1.2f) Lurch(-3f);
        }

        private IEnumerator Stab()
        {
            Play("Stab");
            float hit = Moment("hit");
            yield return UntilTime(hit - 0.3f, () => Line(150f));
            yield return UntilTime(hit);
            StingHit();
            DamageTakenMultiplier = 1.3f; // the stinger is stuck: your opening
            yield return Until("free", 0);
            DamageTakenMultiplier = 1f;
            yield return Finish();
        }

        private void StingHit()
        {
            Vector3 tip = BonePosition("StingTip");
            Strike(tip, 1.9f, stabDamage, 3f);
            Sfx.PlayAt("crab_snip", tip, 1f, 0.35f, 50f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, GroundBelow(tip) + Vector3.up * 0.2f, Vector3.up, 8, 0.6f, 4f, 40f);
            if (Burning) Keep(FireZone.Spawn(fire, GroundBelow(tip), 1.6f, gameObject, transform, 3f));
        }

        private IEnumerator StingerBarrage()
        {
            Play("StingerBarrage");
            float[] hits = Table.Times(Playing, "hit");
            Sfx.PlayAt("magic_charge", transform.position, 0.8f, 0.8f, 40f);
            foreach (float hit in hits)
            {
                yield return UntilTime(hit - 0.2f, () => Line(220f));
                yield return UntilTime(hit);
                StingHit();
            }
            DamageTakenMultiplier = 1.3f;
            yield return Until("free", 0);
            DamageTakenMultiplier = 1f;
            yield return Finish();
        }

        /// <summary>The tail flung out ten metres: if the hook catches the player, they're held up and crushed.</summary>
        private IEnumerator TailSnare()
        {
            Play("TailSnare");
            float hit = Moment("hit");
            yield return UntilTime(hit - 0.25f, () => Track(200f));
            Sfx.PlayAt("chain_swish", transform.position, 1f, 0.35f, 60f);
            bool caught = false;
            yield return UntilTime(hit + 0.15f, () =>
            {
                if (caught || ClipTime < hit - 0.1f) return;
                caught = BossFx.DistanceToPlayer(BonePosition("StingTip")) < 1.9f && Hurt(snareDamage);
            });
            if (!caught)
            {
                DamageTakenMultiplier = 1.25f; // the whiff leaves its tail out: punish it
                yield return Finish();
                yield break;
            }
            MiniBossHud.Message("Caught by the tail!", new Color(1f, 0.6f, 0.3f), 1.5f);
            yield return Crush();
        }

        private IEnumerator Crush()
        {
            Play("SnareCrush", 0.05f);
            float length = Table.Length("SnareCrush");
            if (BossFx.Player != null) PlayerStun.Apply(BossFx.Player, length / Tempo);
            float[] crushes = Table.Times(Playing, "crush");
            float toss = Moment("throw");
            int next = 0;
            yield return UntilTime(toss, () =>
            {
                Hold(BonePosition("StingTip") - Vector3.up * 1.2f);
                if (next < crushes.Length && ClipTime >= crushes[next])
                {
                    next++;
                    Hurt(crushDamage);
                    Sfx.PlayAt("crab_snip", Jaws, 1f, 0.4f, 50f);
                }
            });
            Hurt(throwDamage);
            ShovePlayer(Quaternion.AngleAxis(-50f, Vector3.up) * (BossFx.PlayerFeet - transform.position), 10f);
            yield return Finish();
        }

        private static void Hold(Vector3 at)
        {
            if (BossFx.PlayerBody == null) return;
            Vector3 held = at - BossFx.PlayerBody.transform.position;
            BossFx.PlayerBody.Move(held * Mathf.Min(1f, 12f * Time.deltaTime));
        }

        // ---------- Fuel ----------

        private IEnumerator Spew()
        {
            LookDuringMove(false);
            Play("Spew");
            float start = Moment("spew_start"), end = Moment("spew_end");
            yield return UntilTime(start - 0.35f, () => Track(130f));
            Sfx.PlayAt("mud_splat", transform.position, 1f, 0.5f, 50f);
            yield return UntilTime(start);
            if (Burning) Sfx.PlayAt("fire_crackle", transform.position, 1f, 0.6f, 60f);
            yield return UntilTime(end, () =>
            {
                Vector3 mouth = BonePosition("Mouth");
                Vector3 dir = mouth - BonePosition("Body");
                dir.y = 0f;
                dir = (dir.normalized + Vector3.up * 0.28f).normalized;
                for (int i = Emit("spew", 45f); i > 0; i--)
                    Drop(mouth, (dir + Random.insideUnitSphere * 0.06f) * Random.Range(11f, 14f), 0.022f);
            });
            yield return Finish();
        }

        private IEnumerator FuelRain()
        {
            Play("FuelRain");
            float start = Moment("rain_start"), end = Moment("rain_end");
            yield return UntilTime(start, () => Track(100f));
            Sfx.PlayAt("mud_splat", transform.position, 1f, 0.4f, 60f);
            // Marks where the heaviest rain will fall: round the player.
            Vector3 aim = BossFx.PlayerFeet;
            GroundMarker marker = Marker(GroundBelow(aim), 4f);
            yield return UntilTime(end, () =>
            {
                Vector3 tip = BonePosition("StingTip");
                for (int i = Emit("rain", 35f); i > 0; i--)
                {
                    // Half the drops come down around the marked spot, half anywhere near.
                    Vector3 goal = Random.value < 0.5f ? aim + (Vector3)(Random.insideUnitCircle * 4f).XZ()
                        : transform.position + (Vector3)(Random.insideUnitCircle * 13f).XZ();
                    Drop(tip, Ballistic(tip, goal, 1.3f + Random.value * 0.4f), 0.03f);
                }
                marker.Progress += Time.deltaTime;
            });
            yield return Finish();
        }

        /// <summary>The launch velocity that lands a drop on a point after a given flight time (drops fall at 14 m/s²).</summary>
        private static Vector3 Ballistic(Vector3 from, Vector3 to, float time)
        {
            Vector3 d = to - from;
            return new Vector3(d.x / time, d.y / time + 0.5f * 14f * time, d.z / time);
        }

        private IEnumerator Scrape()
        {
            Play("Scrape");
            float start = Moment("sparks_start"), end = Moment("sparks_end");
            yield return UntilTime(start, () => Track(120f));
            Sfx.PlayAt("electric_sizzle", transform.position, 1f, 0.8f, 50f);
            bool hit = false;
            yield return UntilTime(end, () =>
            {
                foreach (string side in new[] { "L", "R" })
                {
                    Vector3 tip = ClawTip(side);
                    if (Emit("sparks" + side, 20f) > 0)
                        ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, tip, Vector3.up + Random.insideUnitSphere, 6, 0.25f, 6f, 60f);
                    foreach (FuelPool pool in new List<FuelPool>(FuelPool.All))
                        if (pool != null && Flat(pool.transform.position - tip) < pool.Radius + 1.5f) pool.Ignite(gameObject);
                    if (!hit && Strike(tip, 1.9f, comboDamage * 0.8f, 2f)) hit = true;
                }
            });
            yield return Finish();
        }

        // ---------- Burrow, leap, spin ----------

        private IEnumerator Burrow()
        {
            Play("BurrowIn");
            Sfx.PlayAt("wood_smash", transform.position, 0.8f, 0.4f, 50f);
            yield return Until("under", 0, () =>
            {
                if (Emit("dig", 18f) > 0)
                    ParticleBurst.Spawn(ParticleBurst.Kind.Sand, transform.position + Random.insideUnitSphere * 2f, Vector3.up, 6, 1.2f, 4f, 40f);
            });
            SetColliders(false);
            SetRenderersVisible(false);
            // Underground: a trail of churning sand hunts the player, then stops and marks where it will burst out.
            float hunt = 2.6f / Tempo, t = 0f;
            yield return Seconds(hunt, () =>
            {
                t += Time.deltaTime;
                Vector3 toward = BossFx.PlayerFeet - transform.position;
                toward.y = 0f;
                if (toward.sqrMagnitude > 0.25f && AgentReady) Agent.Move(toward.normalized * Mathf.Min(toward.magnitude, 9f * Time.deltaTime));
                if (Emit("trail", 16f) > 0)
                    ParticleBurst.Spawn(ParticleBurst.Kind.Sand, GroundBelow(transform.position) + Vector3.up * 0.2f, Vector3.up, 4, 1f, 3f, 50f);
            });
            Vector3 spot = GroundBelow(transform.position);
            GroundMarker marker = Marker(spot, 3.6f);
            Sfx.PlayAt("stone_rise", spot, 1f, 0.5f, 60f);
            yield return Seconds(0.75f / Tempo, () =>
            {
                marker.Progress += Time.deltaTime * 1.4f;
                ParticleBurst.Spawn(ParticleBurst.Kind.Sand, spot + Random.insideUnitSphere * 2f, Vector3.up, 2, 0.8f, 5f, 40f);
            });
            Vector3 face = BossFx.PlayerFeet - transform.position;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(face);
            SetRenderersVisible(true);
            SetColliders(true);
            DamageTakenMultiplier = 1f;
            Play("BurrowOut", 0.02f);
            yield return Until("erupt", 0);
            Sfx.PlayAt("car_explode", spot, 1f, 0.5f, 70f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, spot + Vector3.up * 0.5f, Vector3.up, 30, 2f, 9f, 45f);
            Strike(spot + Vector3.up, 3.8f, eruptDamage, 5f);
            yield return Until("slam", 0);
            Strike(Jaws, 2.6f, eruptDamage * 0.6f, 3f);
            DamageTakenMultiplier = 1.3f;
            yield return Finish();
        }

        private IEnumerator LeapCrush()
        {
            Play("LeapCrush");
            float launch = Moment("launch"), land = Moment("land");
            yield return UntilTime(launch, () => Track(220f));
            // Aims where the player will be, a little ahead of them.
            Vector3 target = BossFx.PlayerFeet;
            if (BossFx.PlayerBody != null) target += BossFx.PlayerBody.velocity * 0.35f;
            target = ClampToArena(target - FlatDirectionTo(target) * 2.5f, 3f);
            GroundMarker marker = Marker(target, 4f, true);
            SetAirborne(true);
            Sfx.PlayAt("chain_swish", transform.position, 1f, 0.35f, 60f);
            Vector3 from = transform.position;
            float flight = (land - launch) / Tempo, t = 0f;
            float height = 4f + Vector3.Distance(from, target) * 0.12f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / flight);
                Vector3 p = Vector3.Lerp(from, target, t);
                p.y += Mathf.Sin(t * Mathf.PI) * height;
                transform.position = p;
                marker.Progress = t;
                yield return null;
            }
            SetAirborne(false);
            Sfx.PlayAt("car_explode", target, 1f, 0.55f, 70f);
            Strike(target + Vector3.up, 3.8f, leapDamage, 6f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, target + Vector3.up * 0.3f, Vector3.up, 24, 1.6f, 7f, 70f);
            // Fuel sloshes out of its cracks round where it lands.
            for (int i = 0; i < 18; i++)
            {
                float a = i * Mathf.PI * 2f / 18f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Drop(BonePosition("Body") + Vector3.up * 0.5f, d * Random.Range(4f, 7f) + Vector3.up * 4f, 0.04f);
            }
            DamageTakenMultiplier = 1.25f;
            yield return Finish();
        }

        private IEnumerator InfernoSpin()
        {
            Play("InfernoSpin");
            float from = Moment("hit_start"), to = Moment("hit_end");
            yield return UntilTime(from - 0.1f, () => Track(90f));
            Sfx.PlayAt("fire_crackle", transform.position, 1f, 0.5f, 60f);
            float nextHit = 0f;
            yield return UntilTime(to, () =>
            {
                if (Time.time >= nextHit && BossFx.DistanceToPlayer(ClosestTail()) < 2.4f && BossFx.PlayerGrounded && Hurt(spinDamage))
                    nextHit = Time.time + 0.8f;
                Vector3 tip = BonePosition("StingTip");
                Vector3 out_ = tip - transform.position;
                out_.y = 0f;
                for (int i = Emit("spin", 18f); i > 0; i--)
                    Drop(tip, out_.normalized * Random.Range(5f, 9f) + Vector3.up * 3f, 0.025f);
            });
            DamageTakenMultiplier = 1.3f; // dizzy
            yield return Finish();
        }
    }

    internal static class VectorXZ
    {
        /// <summary>A Vector2 on the ground plane (x, y) as a Vector3 (x, 0, y).</summary>
        public static Vector3 XZ(this Vector3 v) => new Vector3(v.x, 0f, v.y);

        /// <summary>A Vector2 on the ground plane as a Vector3 (x, 0, y).</summary>
        public static Vector3 XZ(this Vector2 v) => new Vector3(v.x, 0f, v.y);
    }
}
