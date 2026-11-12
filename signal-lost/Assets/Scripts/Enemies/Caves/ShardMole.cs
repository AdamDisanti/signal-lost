using System.Collections;
using System.Collections.Generic;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Enemies.Desert;
using SignalLost.Melee;
using UnityEngine;

namespace SignalLost.Enemies.Caves
{
    /// <summary>
    /// The Shard Mole, the Crystal Caves mini-boss (docs/shard-mole.md): a blind, hulking cave mole with glowing
    /// cave-crystal claws, a crystal-bladed tail, and the ship's antenna array wired into its back. Built on
    /// <see cref="DuelBoss"/> like the desert bosses (boss bar, phases, poise, leash and reset, the part drop), with a
    /// Blender-made model and clips (tools/blender/cave_shard_mole.py).
    ///
    /// It is blind and hunts by sound. Moving, swinging, charging or firing the laser is noise. After
    /// <see cref="silenceBeforeListening"/> seconds of silence it stops and listens (head cocked, antenna sweeping)
    /// until it hears the player again; hitting it counts as noise.
    ///
    /// Phase 1: claw swipe (close), tail swoop (a full spin with the crystal blade, knocks you back; jump it), and the
    /// burrow (it digs in, a dirt mound with its tail blade showing tracks where it heard you, a ring marks the spot,
    /// and it bursts up under it). Phase 2 adds the claw flurry (right, left, then a held two-handed rake). Phase 3
    /// adds the ceiling shards (a floor slam shakes crystals loose onto marked circles near you).
    /// </summary>
    public sealed class ShardMole : DuelBoss
    {
        /// <summary>The stage its part counts toward (the Crystal Caves has no StageProgress constant yet).</summary>
        public const string Stage = "CrystalCaves";

        [Header("Hearing")]
        [Tooltip("Seconds without a sound from the player before it stops and listens.")]
        [SerializeField, Min(0.5f)] private float silenceBeforeListening = 3f;
        [Tooltip("The player is heard moving faster than this (m/s).")]
        [SerializeField, Min(0f)] private float footstepSpeed = 0.6f;

        [Header("Shard Mole")]
        [Tooltip("The cave crystal material (its claws use it): the falling shards and the mound's blade.")]
        [SerializeField] private Material crystalMaterial;
        [Tooltip("The dirt mound that tracks it underground.")]
        [SerializeField] private Material moundMaterial;
        [SerializeField] private Light[] clawLights = new Light[0];
        [SerializeField] private Color dustColor = new Color(0.42f, 0.43f, 0.5f);
        [SerializeField, Min(0f)] private float swipeDamage = 18f;
        [SerializeField, Min(0f)] private float flurryDamage = 16f;
        [SerializeField, Min(0f)] private float rakeDamage = 26f;
        [SerializeField, Min(0f)] private float swoopDamage = 22f;
        [SerializeField, Min(0f)] private float swoopKnockback = 5f;
        [SerializeField, Min(0f)] private float eruptDamage = 30f;
        [SerializeField, Min(0f)] private float shardDamage = 22f;

        [Header("Burrow")]
        [SerializeField, Min(1f)] private float tunnelSpeed = 7f;
        [Tooltip("Longest it hunts underground before it comes up wherever it is.")]
        [SerializeField, Min(0.5f)] private float maxTunnelTime = 3.5f;
        [Tooltip("How long the ring shows before it bursts out: the time to step off it.")]
        [SerializeField, Min(0.2f)] private float eruptWarning = 1f;
        [SerializeField, Min(0.5f)] private float eruptRadius = 3f;

        [Header("Ceiling shards")]
        [SerializeField, Range(1, 12)] private int shardCount = 5;
        [SerializeField, Min(0.3f)] private float shardRadius = 1.6f;
        [Tooltip("Seconds from the marker appearing to the shard landing.")]
        [SerializeField, Min(0.4f)] private float shardWarning = 1.3f;
        [SerializeField, Min(0f)] private float shardSpread = 6f;

        /// <summary>True while it stands still listening for the player.</summary>
        public bool IsListening => CurrentMove == "Listen";
        /// <summary>True while it is underground (for tests).</summary>
        public bool IsBurrowed { get; private set; }
        public float SilentFor => Time.time - lastNoise;

        private PlayerFists fists;
        private JumpSmash smash;
        private float lastNoise = float.NegativeInfinity, glowBoost = 1f, flare;
        private float[] lightBase = new float[0];
        private Vector3 heardAt, lastTargetPosition;

        protected override string PartStage => Stage;

        protected override void OnBossStarted()
        {
            base.OnBossStarted();
            if (Target != null)
            {
                fists = Target.GetComponentInParent<PlayerFists>();
                smash = Target.GetComponentInParent<JumpSmash>();
            }
            lightBase = new float[clawLights.Length];
            for (int i = 0; i < clawLights.Length; i++) lightBase[i] = clawLights[i] != null ? clawLights[i].intensity : 0f;
        }

        protected override void DefineAttacks()
        {
            Attacks.Add(new Attack { Name = "Swipe", Run = Swipe, MaxRange = 4.5f, MaxAngle = 70f, Weight = 3f, Cooldown = 1.8f });
            Attacks.Add(new Attack { Name = "TailSwoop", Run = TailSwoop, MaxRange = 6f, Weight = 1.5f, Cooldown = 5f, HyperArmor = true });
            Attacks.Add(new Attack { Name = "TailSwoopFlank", Run = TailSwoop, MaxRange = 6f, MinAngle = 70f, Weight = 5f, Cooldown = 5f, HyperArmor = true });
            Attacks.Add(new Attack { Name = "Burrow", Run = Burrow, MinRange = 7f, Weight = 3f, Cooldown = 9f, HyperArmor = true });
            Attacks.Add(new Attack { Name = "ClawFlurry", Run = ClawFlurry, MaxRange = 5f, MaxAngle = 60f, MinPhase = 2, Weight = 3f, Cooldown = 6f });
            Attacks.Add(new Attack { Name = "CeilingShards", Run = CeilingShards, MaxRange = 16f, MinPhase = 3, Weight = 2.5f, Cooldown = 10f });
        }

        // ---------- Hearing ----------

        /// <summary>Footsteps (or a jump), and the laser swinging, charging or just fired. Standing still with the weapon quiet is silent.</summary>
        private bool PlayerMakingNoise()
        {
            if (!TargetAlive) return false;
            // Measured from where the player actually went this frame, so walking, sliding and jumping all count.
            Vector3 v = (Target.position - lastTargetPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
            lastTargetPosition = Target.position;
            if (Mathf.Abs(v.y) > 1.5f) return true;
            v.y = 0f;
            if (v.magnitude > footstepSpeed) return true;
            if (TargetLaser != null && (TargetLaser.IsSwinging || TargetLaser.IsCharging ||
                (TargetLaser.IsCoolingDown && TargetLaser.ShotCooldownRemaining > TargetLaser.CurrentCooldownLength - 0.4f)))
                return true;
            return (fists != null && fists.IsBusy) || (smash != null && smash.IsDiving);
        }

        private void Hear()
        {
            lastNoise = Time.time;
            if (Target != null) heardAt = Target.position;
        }

        protected override void OnEngaged()
        {
            Hear(); // it engages because it heard the player come in
            base.OnEngaged();
        }

        protected override void OnBossHit(float amount, GameObject source, Vector3 away)
        {
            Hear();
            base.OnBossHit(amount, source, away);
        }

        protected override void TickCombat(float dt)
        {
            if (PlayerMakingNoise()) Hear();
            if (!Busy && TargetAlive && SilentFor > silenceBeforeListening) Run(Listen(), "Listen", null);
            base.TickCombat(dt);
        }

        private IEnumerator Listen()
        {
            Play("Listen", 0.3f, false);
            while (SilentFor > silenceBeforeListening) yield return null;
            // Heard something: the head snaps toward it, then it turns to the sound.
            Play("Startle", 0.08f);
            Sfx.PlayAt("crab_snip", transform.position, 0.6f, 0.4f, 30f);
            yield return Finish(() => TurnToward(heardAt, 360f * Time.deltaTime));
        }

        // ---------- Phase, glow, reset ----------

        protected override void OnDuelPhase(int phase) => glowBoost = phase >= 3 ? 2f : phase == 2 ? 1.45f : 1f;

        protected override void OnDuelReset()
        {
            glowBoost = 1f;
            lastNoise = float.NegativeInfinity;
        }

        protected override void OnDuelDeath()
        {
            foreach (Light l in clawLights)
                if (l != null) l.enabled = false;
        }

        protected override void OnMoveEnded()
        {
            flare = 0f;
            if (!IsBurrowed || IsDead) return;
            // A move cut short underground (a reset): come back up.
            IsBurrowed = false;
            SetRenderersVisible(true);
            SetColliders(true);
            SetLights(true);
        }

        protected override void AnimateBoss(float dt)
        {
            base.AnimateBoss(dt);
            for (int i = 0; i < clawLights.Length; i++)
                if (clawLights[i] != null) clawLights[i].intensity = lightBase[i] * glowBoost * (1f + 1.8f * flare);
        }

        private void SetLights(bool on)
        {
            foreach (Light l in clawLights)
                if (l != null) l.enabled = on;
        }

        private void Dust(Vector3 at, int count, float size, float speed) =>
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, at, Vector3.up, count, size, speed, 45f, dustColor);

        private Vector3 Claw(string side) => BonePosition("Hand" + side);

        // ---------- Phase 1: claws, tail, burrow ----------

        private IEnumerator Swipe()
        {
            LookDuringMove(true);
            Vector3 feet = BossFx.PlayerFeet;
            string side = (Claw("L") - feet).sqrMagnitude < (Claw("R") - feet).sqrMagnitude ? "L" : "R";
            Play("Swipe" + side);
            float move = Moment("move"), hit = Moment("hit");
            yield return UntilTime(move, () => { CloseIn(3f, 170f); flare = Mathf.MoveTowards(flare, 1f, Time.deltaTime * 3f); });
            Sfx.PlayAt("chain_swish", transform.position, 0.9f, 0.7f, 40f);
            yield return StrikeWindow(hit - 0.1f, hit + 0.12f, () => Claw(side), 2.2f, swipeDamage, 2f, false, () => Lurch(4f));
            flare = 0f;
            yield return Finish(() => Track(60f));
        }

        private IEnumerator TailSwoop()
        {
            Play("TailSwoop");
            float from = Moment("hit_start"), to = Moment("hit_end");
            // The coil with the blade raised is the warning.
            yield return UntilTime(from - 0.1f, () => { Track(90f); flare = Mathf.MoveTowards(flare, 1f, Time.deltaTime * 2f); });
            Sfx.PlayAt("chain_swish", transform.position, 1f, 0.45f, 50f);
            yield return StrikeWindow(from, to, ClosestTail, 1.6f, swoopDamage, swoopKnockback, true);
            flare = 0f;
            DamageTakenMultiplier = 1.2f; // it spun itself dizzy: the opening
            yield return Finish();
        }

        private Vector3 ClosestTail()
        {
            Vector3 best = BonePosition("BladeTip");
            float d = BossFx.DistanceToPlayer(best);
            foreach (string bone in new[] { "TailTip", "Tail2" })
            {
                Vector3 p = BonePosition(bone);
                float pd = BossFx.DistanceToPlayer(p);
                if (pd < d) { d = pd; best = p; }
            }
            return best;
        }

        /// <summary>
        /// Digs in, hunts underground toward where it last heard the player (a dirt mound with its crystal blade
        /// showing tracks it), stops, marks a ring, and bursts out under it. Standing still doesn't hide you once
        /// it's under you; stepping off the ring does.
        /// </summary>
        private IEnumerator Burrow()
        {
            Play("BurrowIn");
            Sfx.PlayAt("wood_smash", transform.position, 0.8f, 0.45f, 50f);
            yield return Until("under", 0, () =>
            {
                Track(120f);
                if (Emit("dig", 16f) > 0) Dust(transform.position + Random.insideUnitSphere * 1.2f, 6, 1f, 4f);
            });
            IsBurrowed = true;
            SetColliders(false);
            SetRenderersVisible(false);
            SetLights(false);

            Transform mound = Temp(BossFx.Prop("Mole Mound", puffMesh, moundMaterial, GroundBelow(transform.position),
                Quaternion.identity, new Vector3(1.3f, 0.35f, 1.5f)).transform);
            Transform fin = Temp(BossFx.Prop("Mole Blade", spikeMesh, crystalMaterial, mound.position, Quaternion.Euler(-90f, 0f, 0f),
                new Vector3(0.22f, 0.22f, 0.8f)).transform);
            float hunt = 0f;
            while (hunt < maxTunnelTime / Tempo)
            {
                hunt += Time.deltaTime;
                Vector3 toward = heardAt - transform.position;
                toward.y = 0f;
                if (toward.magnitude < 0.6f) break;
                if (AgentReady) Agent.Move(toward.normalized * Mathf.Min(toward.magnitude, tunnelSpeed * Tempo * Time.deltaTime));
                Vector3 ground = GroundBelow(transform.position);
                mound.position = ground + Vector3.up * (0.05f + Mathf.Sin(Time.time * 18f) * 0.04f);
                fin.position = ground + Vector3.up * 0.15f;
                if (Emit("trail", 14f) > 0) Dust(ground + Vector3.up * 0.2f, 4, 0.8f, 2.5f);
                yield return null;
            }

            Vector3 spot = GroundBelow(transform.position);
            GroundMarker marker = Marker(spot, eruptRadius);
            Sfx.PlayAt("stone_rise", spot, 1f, 0.5f, 60f);
            yield return Seconds(eruptWarning / Tempo, () =>
            {
                marker.Progress += Time.deltaTime / (eruptWarning / Tempo);
                mound.localScale = new Vector3(1.3f, 0.35f, 1.5f) * (1f + 0.5f * marker.Progress);
                if (Emit("shake", 10f) > 0) Dust(spot + Random.insideUnitSphere * eruptRadius * 0.8f, 2, 0.6f, 4f);
            });

            Vector3 face = BossFx.PlayerFeet - transform.position;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(face);
            IsBurrowed = false;
            SetRenderersVisible(true);
            SetColliders(true);
            SetLights(true);
            Play("BurrowOut", 0.02f);
            yield return Until("erupt", 0);
            Sfx.PlayAt("car_explode", spot, 0.8f, 0.6f, 60f);
            Dust(spot + Vector3.up * 0.4f, 30, 1.8f, 8f);
            for (int i = 0; i < 10; i++)
                BossPuff.Spawn(puffMesh, moundMaterial, spot + Random.insideUnitSphere, Random.onUnitSphere * 3f + Vector3.up * 6f,
                    1.6f, Random.Range(0.2f, 0.45f), 0.1f, 18f);
            Strike(spot + Vector3.up, eruptRadius, eruptDamage, 4f);
            DamageTakenMultiplier = 1.3f; // dazed after the landing: the opening
            yield return Until("recovered", 0);
            DamageTakenMultiplier = 1f;
            yield return Finish();
        }

        // ---------- Phase 2: claw flurry ----------

        private IEnumerator ClawFlurry()
        {
            LookDuringMove(true);
            Play("ClawFlurry");
            float[] hits = Table.Times(Playing, "hit"), moves = Table.Times(Playing, "move");
            for (int i = 0; i < hits.Length; i++)
            {
                bool rake = i == hits.Length - 1;
                yield return UntilTime(moves[i], () =>
                {
                    CloseIn(3f, rake ? 90f : 160f);
                    flare = Mathf.MoveTowards(flare, rake ? 1.5f : 1f, Time.deltaTime * 3f);
                });
                Sfx.PlayAt(rake ? "laser_slam" : "chain_swish", transform.position, 1f, rake ? 0.6f : 0.75f, 45f);
                int index = i;
                yield return StrikeWindow(hits[i] - 0.1f, hits[i] + 0.12f,
                    () => rake ? (Claw("L") + Claw("R")) * 0.5f : index == 0 ? Claw("R") : Claw("L"),
                    rake ? 2.8f : 2.2f, rake ? rakeDamage : flurryDamage, rake ? 4f : 1f, false, () => Lurch(rake ? 3f : 4f));
                flare = 0f;
            }
            yield return Finish();
        }

        // ---------- Phase 3: ceiling shards ----------

        private IEnumerator CeilingShards()
        {
            Play("Quake");
            yield return Until("slam", 0, () => { Track(120f); flare = Mathf.MoveTowards(flare, 1.5f, Time.deltaTime * 2f); });
            flare = 0f;
            Sfx.PlayAt("wood_smash", transform.position, 1f, 0.35f, 70f);
            Dust(GroundBelow(BonePosition("HandL")) + Vector3.up * 0.2f, 14, 1.4f, 5f);
            Dust(GroundBelow(BonePosition("HandR")) + Vector3.up * 0.2f, 14, 1.4f, 5f);

            // The first shard aims at the player (a little ahead if they're running), the rest round them.
            var spots = new List<Vector3>();
            Vector3 lead = BossFx.PlayerFeet;
            if (BossFx.PlayerBody != null) lead += BossFx.PlayerBody.velocity * 0.4f;
            spots.Add(GroundBelow(lead, 4f)); // not clamped: it lands on the player wherever they stand in the fight
            for (int i = 1; i < shardCount; i++)
                spots.Add(ClampToArena(BossFx.PlayerFeet + (Vector3)(Random.insideUnitCircle * shardSpread).XZ(), 2f));

            var markers = new List<GroundMarker>();
            var shards = new List<Transform>();
            var tops = new List<Vector3>();
            float stagger = 0.15f / Tempo, warning = shardWarning / Tempo, fall = 0.35f;
            foreach (Vector3 spot in spots)
            {
                markers.Add(Marker(spot, shardRadius));
                tops.Add(spot + Vector3.up * (Physics.Raycast(spot + Vector3.up * 0.5f, Vector3.up, out RaycastHit roof, 15f, sightMask,
                    QueryTriggerInteraction.Ignore) ? roof.distance : 8f));
                shards.Add(null);
            }
            var landed = new bool[spots.Count];
            float t = 0f;
            yield return Seconds(warning + stagger * spots.Count + 0.05f, () =>
            {
                t += Time.deltaTime;
                for (int i = 0; i < spots.Count; i++)
                {
                    float local = t - i * stagger;
                    if (local < 0f || landed[i]) continue;
                    markers[i].Progress = local / warning;
                    if (local >= warning - fall && shards[i] == null)
                        shards[i] = Temp(BossFx.Prop("Ceiling Shard", spikeMesh, crystalMaterial, tops[i], Quaternion.Euler(90f, 0f, 0f),
                            new Vector3(0.5f, 0.5f, 1.6f), true).transform);
                    if (shards[i] != null)
                        shards[i].position = Vector3.Lerp(tops[i], spots[i] + Vector3.up * 1.6f, Mathf.Clamp01((local - (warning - fall)) / fall));
                    if (local < warning) continue;
                    landed[i] = true;
                    Strike(spots[i] + Vector3.up, shardRadius, shardDamage, 1.5f);
                    Sfx.PlayAt("crab_snip", spots[i], 1f, 0.3f, 40f);
                    for (int k = 0; k < 6; k++)
                        BossPuff.Spawn(puffMesh, crystalMaterial, spots[i] + Vector3.up * 0.5f, Random.onUnitSphere * 3f + Vector3.up * 3f,
                            0.9f, Random.Range(0.12f, 0.25f), 0.05f, 16f);
                    if (shards[i] != null) shards[i].gameObject.SetActive(false);
                    markers[i].gameObject.SetActive(false);
                }
            });
            yield return Finish();
        }
    }
}
