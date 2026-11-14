using System.Collections;
using System.Collections.Generic;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Enemies.Desert;
using SignalLost.Player;
using UnityEngine;

namespace SignalLost.Enemies.Caves
{
    /// <summary>
    /// Grumble, the Geode Bear: a Crystal Caves mini-boss (docs/geode-bear.md). A grumpy cave bear woken early by the
    /// crash, with geode crystals grown on its hump and the ship's power cell strapped to its back. Built on
    /// <see cref="DuelBoss"/> like the Shard Mole and the desert bosses (boss bar, phases, poise, leash and reset, the
    /// part drop), with a Blender-made model and clips (tools/blender/cave_geode_bear.py).
    ///
    /// The geode back takes half damage; the power cell takes full damage, and hitting it wakes the bear from a
    /// hibernate attempt. Phase 1: paw swipe, boulder lob, big yawn (a dust cone that slows and clouds the player and
    /// leaves the bear dizzy). Phase 2 adds the belly flop (a crystal shockwave ring: jump it) and the bear-hug lunge
    /// (jump or slide). Phase 3 adds the hibernate attempt (curls up and heals until the cell is hit).
    /// Tuned a step above the desert's hardest duel (Pitchclaw), per Amanda (11-14).
    /// </summary>
    public sealed class GeodeBear : DuelBoss
    {
        [Header("Geode Bear")]
        [Tooltip("The cave crystal material: the boulder's crystals, the shockwave ring, the crest grown while hibernating.")]
        [SerializeField] private Material crystalMaterial;
        [SerializeField] private Material rockMaterial;
        [Tooltip("The power cell's hitbox: full damage, and a hit wakes it from hibernating.")]
        [SerializeField] private Health powerCell;
        [Tooltip("The geode back's hitbox: half damage.")]
        [SerializeField] private Health geodeBack;
        [SerializeField] private Light[] crestLights = new Light[0];
        [SerializeField] private Light cellLight;
        [SerializeField] private Color dustColor = new Color(0.55f, 0.52f, 0.6f);
        [SerializeField, Range(0f, 1f)] private float backDamageTaken = 0.5f;

        [Header("Moves")]
        [SerializeField, Min(0f)] private float swipeDamage = 24f;
        [SerializeField, Min(0f)] private float lobDamage = 28f;
        [SerializeField, Min(0.5f)] private float lobRadius = 2.6f;
        [SerializeField, Min(0.3f)] private float lobFlight = 1.1f;
        [SerializeField, Min(1f)] private float yawnRange = 9f;
        [Tooltip("Half-angle of the yawn's dust cone (degrees).")]
        [SerializeField, Range(5f, 80f)] private float yawnAngle = 32f;
        [Tooltip("Player speed while in the yawn's dust (1 = unslowed).")]
        [SerializeField, Range(0.1f, 1f)] private float yawnSlow = 0.55f;
        [SerializeField, Min(0.2f)] private float yawnTime = 2f;
        [SerializeField, Min(0f)] private float flopDamage = 34f;
        [SerializeField, Min(0.5f)] private float flopRadius = 3.6f;
        [SerializeField, Min(0f)] private float ringDamage = 18f;
        [SerializeField, Min(1f)] private float ringReach = 11f;
        [SerializeField, Min(0f)] private float hugGrabDamage = 10f;
        [SerializeField, Min(0f)] private float hugSqueezeDamage = 12f;
        [SerializeField, Min(0f)] private float hugThrowDamage = 12f;
        [SerializeField, Min(1f)] private float hugSpeed = 12f;

        [Header("Hibernate")]
        [Tooltip("Seconds it sleeps: the window to hit the power cell.")]
        [SerializeField, Min(1f)] private float hibernateWindow = 5f;
        [Tooltip("Fraction of its max health it regains if it sleeps the whole window.")]
        [SerializeField, Range(0f, 1f)] private float hibernateHeal = 0.15f;
        [SerializeField, Range(0, 5)] private int maxHibernates = 2;

        /// <summary>True while it is curled up asleep (for tests).</summary>
        public bool IsHibernating { get; private set; }
        /// <summary>True while the yawn's dust slows the player (for tests).</summary>
        public bool PlayerSlowed => slowed;

        private FirstPersonController movement;
        private Transform lids;
        private Vector3 lidRest;
        private float glowBoost = 1f, flare, slowUntil, nextSpark, lidDrop;
        private float[] lightBase = new float[0];
        private bool slowed, woken;
        private int hibernates;

        protected override string PartStage => ShardMole.Stage;

        protected override void OnBossStarted()
        {
            base.OnBossStarted();
            if (Target != null) movement = Target.GetComponentInParent<FirstPersonController>();
            RelayPart(powerCell, 0);
            RelayPart(geodeBack, 1);
            lightBase = new float[crestLights.Length];
            for (int i = 0; i < crestLights.Length; i++) lightBase[i] = crestLights[i] != null ? crestLights[i].intensity : 0f;
            lids = Bone("Lids");
            if (lids != null) lidRest = lids.localPosition;
        }

        protected override void DefineAttacks()
        {
            Attacks.Add(new Attack { Name = "Swipe", Run = Swipe, MaxRange = 4.5f, MaxAngle = 70f, Weight = 3f, Cooldown = 1.8f });
            Attacks.Add(new Attack { Name = "BoulderLob", Run = BoulderLob, MinRange = 6f, MaxRange = 18f, MaxAngle = 60f, Weight = 2.5f, Cooldown = 6f });
            Attacks.Add(new Attack { Name = "Yawn", Run = Yawn, MaxRange = 8f, MaxAngle = 40f, Weight = 1.5f, Cooldown = 9f });
            Attacks.Add(new Attack { Name = "BellyFlop", Run = BellyFlop, MinRange = 5f, MaxRange = 16f, MinPhase = 2, Weight = 2.5f, Cooldown = 8f, HyperArmor = true });
            Attacks.Add(new Attack { Name = "BearHug", Run = BearHug, MinRange = 3.5f, MaxRange = 9f, MaxAngle = 35f, MinPhase = 2, Weight = 2.5f, Cooldown = 8f });
            Attacks.Add(new Attack { Name = "Hibernate", Run = Hibernate, MinPhase = 3, Weight = 2f, Cooldown = 14f, HyperArmor = true,
                When = () => hibernates < maxHibernates && Health.Normalized < 0.95f });
        }

        // ---------- Hide, weak spot ----------

        protected override float OnPartHit(int part, float amount, GameObject source)
        {
            if (part == 1) return amount * backDamageTaken; // the geode back
            if (IsHibernating && !woken)
            {
                woken = true;
                Sfx.PlayAt("car_explode", BonePosition("Cell"), 0.5f, 1.4f, 40f);
                ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, BonePosition("Cell"), Vector3.up, 24, 0.4f, 6f, 60f);
            }
            return amount; // the power cell
        }

        // ---------- Phase, glow, reset ----------

        protected override void OnDuelPhase(int phase) => glowBoost = phase >= 3 ? 2f : phase == 2 ? 1.45f : 1f;

        protected override void OnDuelReset()
        {
            glowBoost = 1f;
            hibernates = 0;
            Unslow();
        }

        protected override void OnDuelDeath()
        {
            Unslow();
            foreach (Light l in crestLights)
                if (l != null) l.enabled = false;
            if (cellLight != null) cellLight.enabled = false;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Unslow();
        }

        protected override void OnMoveEnded()
        {
            flare = 0f;
            IsHibernating = false;
        }

        protected override void AnimateBoss(float dt)
        {
            base.AnimateBoss(dt);
            if (slowed && Time.time >= slowUntil) Unslow();
            for (int i = 0; i < crestLights.Length; i++)
                if (crestLights[i] != null) crestLights[i].intensity = lightBase[i] * glowBoost * (1f + flare);
            // Phase 3 and asleep: the cell sparks (the tell for where to hit).
            if ((DuelPhase >= 3 || IsHibernating) && Time.time >= nextSpark)
            {
                nextSpark = Time.time + (IsHibernating ? 0.25f : 0.6f);
                ParticleBurst.Spawn(ParticleBurst.Kind.Sparks, BonePosition("Cell"), Vector3.up, 6, 0.25f, 3f, 70f);
                if (cellLight != null) cellLight.intensity = Random.Range(1f, 2.5f);
            }
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            // Sleepy lids: half-shut in phase 3 (Sleepwalker), shut while hibernating. After the Animator, so it sticks.
            if (lids == null || IsDead) return;
            float want = IsHibernating ? 0.07f : DuelPhase >= 3 ? 0.04f : 0f;
            lidDrop = Mathf.MoveTowards(lidDrop, want, Time.deltaTime * 0.2f);
            lids.localPosition = lidRest + lids.parent.InverseTransformVector(Vector3.down * lidDrop);
        }

        private void Unslow()
        {
            if (!slowed) return;
            slowed = false;
            if (movement != null) movement.SetSpeedBonus(this, 1f);
        }

        private void Dust(Vector3 at, int count, float size, float speed) =>
            ParticleBurst.Spawn(ParticleBurst.Kind.Sand, at, Vector3.up, count, size, speed, 45f, dustColor);

        private Vector3 Paw(string side) => BonePosition("Hand" + side);

        /// <summary>Where the player will be a moment from now (for throws and leaps).</summary>
        private Vector3 Lead(float seconds)
        {
            Vector3 lead = BossFx.PlayerFeet;
            if (BossFx.PlayerBody != null) lead += BossFx.PlayerBody.velocity * seconds;
            return lead;
        }

        // ---------- Phase 1: swipe, lob, yawn ----------

        private IEnumerator Swipe()
        {
            LookDuringMove(true);
            Vector3 feet = BossFx.PlayerFeet;
            string side = (Paw("L") - feet).sqrMagnitude < (Paw("R") - feet).sqrMagnitude ? "L" : "R";
            // Sleepwalker: wider, sloppier swings.
            bool sloppy = DuelPhase >= 3;
            Play("Swipe" + side);
            float move = Moment("move"), hit = Moment("hit");
            yield return UntilTime(move, () => { CloseIn(3f, 170f); flare = Mathf.MoveTowards(flare, 0.6f, Time.deltaTime * 2f); });
            Sfx.PlayAt("chain_swish", transform.position, 0.9f, 0.55f, 40f);
            yield return StrikeWindow(hit - 0.1f, hit + 0.15f, () => Paw(side), sloppy ? 3f : 2.4f, swipeDamage, 2f, false,
                () => Lurch(sloppy ? 6f : 4f));
            flare = 0f;
            yield return Finish(() => Track(60f));
        }

        private IEnumerator BoulderLob()
        {
            LookDuringMove(true);
            Play("Lob");
            yield return Until("rip", 0, () => Track(120f));
            Vector3 hands = (Paw("L") + Paw("R")) * 0.5f;
            Sfx.PlayAt("wood_smash", hands, 0.9f, 0.5f, 50f);
            Dust(GroundBelow(hands) + Vector3.up * 0.2f, 16, 1.2f, 4f);
            Transform rock = Temp(BossFx.Prop("Geode Boulder", puffMesh, rockMaterial, hands, Random.rotation, Vector3.one * 0.9f, true).transform);
            for (int i = 0; i < 3; i++)
            {
                Transform crystal = BossFx.Prop("Boulder Crystal", spikeMesh, crystalMaterial, Vector3.zero, Quaternion.identity,
                    new Vector3(0.25f, 0.25f, 0.6f), false, rock).transform;
                crystal.localRotation = Quaternion.LookRotation(Random.onUnitSphere);
                crystal.localPosition = crystal.localRotation * Vector3.forward * 0.35f;
            }
            yield return Until("throw", 0, () =>
            {
                Track(150f);
                rock.position = (Paw("L") + Paw("R")) * 0.5f + Vector3.up * 0.35f;
            });

            Vector3 from = rock.position;
            Vector3 target = GroundBelow(Lead(0.5f), 4f); // not clamped: it lands on the player wherever they stand in the fight
            GroundMarker marker = Marker(target, lobRadius);
            Sfx.PlayAt("chain_swish", from, 1f, 0.35f, 50f);
            float flight = lobFlight / Tempo, t = 0f, height = 3f + Vector3.Distance(from, target) * 0.15f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / flight);
                Vector3 p = Vector3.Lerp(from, target + Vector3.up * 0.4f, t);
                p.y += Mathf.Sin(t * Mathf.PI) * height;
                rock.position = p;
                rock.Rotate(200f * Time.deltaTime, 90f * Time.deltaTime, 0f);
                marker.Progress = t;
                yield return null;
            }
            rock.gameObject.SetActive(false);
            Sfx.PlayAt("wood_smash", target, 1f, 0.7f, 50f);
            Sfx.PlayAt("crab_snip", target, 0.8f, 0.4f, 40f);
            Strike(target + Vector3.up, lobRadius, lobDamage, 3f);
            Dust(target + Vector3.up * 0.3f, 20, 1.4f, 6f);
            // It bursts into small crystal shards.
            for (int i = 0; i < 10; i++)
                BossPuff.Spawn(puffMesh, i % 2 == 0 ? crystalMaterial : rockMaterial, target + Vector3.up * 0.5f,
                    Random.onUnitSphere * 4f + Vector3.up * 4f, 1.2f, Random.Range(0.15f, 0.3f), 0.05f, 16f);
            yield return Finish();
        }

        private bool InYawnCone()
        {
            if (!TargetAlive) return false;
            if (FlatDistance(Target.position) > yawnRange) return false;
            return AngleToTarget() <= yawnAngle;
        }

        private IEnumerator Yawn()
        {
            LookDuringMove(false);
            Play("Yawn");
            yield return Until("yawn_start", 0, () => Track(120f));
            Sfx.PlayAt("kong_roar", transform.position, 0.7f, 0.45f, 50f);
            bool dusted = false;
            yield return Until("yawn_end", 0, () =>
            {
                Vector3 mouth = BonePosition("Jaw");
                int n = Emit("yawn", 40f);
                if (n > 0) ParticleBurst.Spawn(ParticleBurst.Kind.Sand, mouth, transform.forward, n, 1.3f, yawnRange * 0.9f, yawnAngle, dustColor);
                if (dusted || !InYawnCone()) return;
                dusted = true;
                slowed = true;
                slowUntil = Time.time + yawnTime;
                if (movement != null) movement.SetSpeedBonus(this, yawnSlow);
                MiniBossHud.Haze(new Color(dustColor.r, dustColor.g, dustColor.b, 0.85f), yawnTime);
            });
            DamageTakenMultiplier = 1.3f; // it yawned itself dizzy: the opening
            yield return Until("recovered", 0);
            DamageTakenMultiplier = 1f;
            yield return Finish();
        }

        // ---------- Phase 2: belly flop, bear hug ----------

        private IEnumerator BellyFlop()
        {
            Play("BellyFlop");
            float launch = Moment("launch"), land = Moment("land");
            yield return UntilTime(launch, () => Track(200f));
            Vector3 target = Lead(0.35f);
            target = ClampToArena(target - FlatDirectionTo(target) * 1.5f, 3f);
            GroundMarker marker = Marker(target, flopRadius, true);
            SetAirborne(true);
            Sfx.PlayAt("chain_swish", transform.position, 1f, 0.3f, 60f);
            Vector3 from = transform.position;
            float flight = (land - launch) / Tempo, t = 0f, height = 3.5f + Vector3.Distance(from, target) * 0.12f;
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
            Sfx.PlayAt("car_explode", target, 1f, 0.45f, 70f);
            Strike(target + Vector3.up, flopRadius, flopDamage, 5f);
            Keep(SpikeRing.Spawn(spikeMesh, crystalMaterial, target, flopRadius, ringReach, 9f * Tempo, ringDamage * DamageScale, gameObject));
            Dust(target + Vector3.up * 0.3f, 30, 1.8f, 7f);
            DamageTakenMultiplier = 1.25f; // flat on its belly: the opening
            yield return Until("recovered", 0);
            DamageTakenMultiplier = 1f;
            yield return Finish();
        }

        /// <summary>In front of its chest at hug height.</summary>
        private Vector3 HugPoint => transform.position + transform.forward * 1.6f + Vector3.up * 1.1f;

        private IEnumerator BearHug()
        {
            LookDuringMove(true);
            Play("HugLunge");
            // Rearing with arms wide is the warning.
            yield return Until("charge", 0, () => Track(160f));
            Sfx.PlayAt("kong_roar", transform.position, 0.6f, 0.9f, 50f);
            float catchAt = Moment("catch");
            bool caught = false;
            yield return Until("catch_end", 0, () =>
            {
                if (ClipTime < catchAt) { Lurch(hugSpeed); Track(70f); }
                if (caught || ClipTime < catchAt - 0.12f) return;
                // Jumping over the arms or sliding under them escapes.
                bool dodged = !BossFx.PlayerGrounded || (movement != null && movement.IsSliding);
                caught = !dodged && BossFx.DistanceToPlayer(HugPoint) < 1.9f && Hurt(hugGrabDamage);
            });
            if (!caught)
            {
                DamageTakenMultiplier = 1.25f; // hugged thin air: punish it
                yield return Finish();
                yield break;
            }
            MiniBossHud.Message("Bear hug!", new Color(1f, 0.6f, 0.3f), 1.5f);
            Play("HugSqueeze", 0.05f);
            if (BossFx.Player != null) PlayerStun.Apply(BossFx.Player, Table.Length("HugSqueeze") / Tempo);
            float[] squeezes = Table.Times(Playing, "squeeze");
            int next = 0;
            yield return Until("throw", 0, () =>
            {
                Hold(HugPoint - Vector3.up * 0.9f);
                if (next < squeezes.Length && ClipTime >= squeezes[next])
                {
                    next++;
                    Hurt(hugSqueezeDamage);
                    Sfx.PlayAt("crab_whimper", HugPoint, 0.8f, 0.6f, 40f);
                }
            });
            Hurt(hugThrowDamage);
            ShovePlayer(Quaternion.AngleAxis(40f, Vector3.up) * transform.forward, 8f);
            yield return Finish();
        }

        private static void Hold(Vector3 at)
        {
            if (BossFx.PlayerBody == null) return;
            Vector3 held = at - BossFx.PlayerBody.transform.position;
            BossFx.PlayerBody.Move(held * Mathf.Min(1f, 12f * Time.deltaTime));
        }

        // ---------- Phase 3: hibernate ----------

        /// <summary>
        /// Curls up and snores, healing a little at a time while crystals grow out of its back. A hit on the sparking
        /// power cell jolts it awake, dazed; sleeping the whole window gives it the full heal.
        /// </summary>
        private IEnumerator Hibernate()
        {
            hibernates++;
            LookDuringMove(false);
            Play("CurlUp");
            yield return Until("curl", 0);
            IsHibernating = true;
            woken = false;
            MiniBossHud.Message("Grumble is dozing off... hit the power cell!", new Color(0.6f, 1f, 0.7f), 2.5f);
            Play("Hibernate", 0.2f, false);

            // Crystals grow outward from the crest while it sleeps.
            Transform crest = Bone("Crest");
            var grown = new List<Transform>();
            for (int i = 0; i < 7; i++)
            {
                Quaternion tilt = Quaternion.Euler(Random.Range(-50f, 50f), Random.Range(0f, 360f), 0f);
                Transform c = Temp(BossFx.Prop("Grown Crystal", spikeMesh, crystalMaterial, BonePosition("Crest"),
                    Quaternion.LookRotation(tilt * Vector3.up), Vector3.zero, true).transform);
                if (crest != null) c.SetParent(crest, true);
                grown.Add(c);
            }

            float total = Health.Max * hibernateHeal, t = 0f, nextSnore = 0f;
            while (t < hibernateWindow && !woken && !IsDead)
            {
                t += Time.deltaTime;
                Health.Heal(total * Time.deltaTime / hibernateWindow);
                float grow = t / hibernateWindow;
                foreach (Transform c in grown) c.localScale = new Vector3(0.35f, 0.35f, 1.4f) * grow / Mathf.Max(0.0001f, c.parent != null ? c.parent.lossyScale.x : 1f);
                flare = grow;
                if (Time.time >= nextSnore)
                {
                    nextSnore = Time.time + 1.8f;
                    Sfx.PlayAt("kong_roar", transform.position, 0.35f, 0.3f, 40f);
                }
                yield return null;
            }
            IsHibernating = false;
            flare = 0f;
            foreach (Transform c in grown)
            {
                for (int k = 0; k < 3; k++)
                    BossPuff.Spawn(puffMesh, crystalMaterial, c.position, Random.onUnitSphere * 3f + Vector3.up * 3f, 0.9f,
                        Random.Range(0.12f, 0.25f), 0.05f, 16f);
                c.gameObject.SetActive(false);
            }

            Play("WakeUp", 0.05f);
            if (woken)
            {
                MiniBossHud.Message("Jolted awake!", new Color(1f, 0.85f, 0.4f), 1.5f);
                DamageTakenMultiplier = 1.3f; // dazed: the reward for waking it
                yield return Until("recovered", 0);
                DamageTakenMultiplier = 1f;
            }
            else MiniBossHud.Message("Grumble feels refreshed.", new Color(1f, 0.55f, 0.25f), 1.5f);
            yield return Finish();
        }
    }
}
